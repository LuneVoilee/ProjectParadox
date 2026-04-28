using System.Collections.Generic;

namespace Core.Capability.Editor
{
    internal enum CapabilityRuntimeState
    {
        None = 0,
        Inactive = 1,
        Active = 2,
        Blocked = 3
    }

    internal struct CapabilityTimelineSegment
    {
        public int Count;

        public CapabilityRuntimeState State;
    }

    internal sealed class CapabilityTimelineTrack
    {
        private readonly List<int> m_States = new List<int>(1024);

        private readonly List<CapabilityTimelineSegment> m_Segments = new List<CapabilityTimelineSegment>(32);

        public string Name { get; }

        public int Count => m_States.Count;

        public CapabilityTimelineTrack(string name)
        {
            Name = name;
        }

        public void Push(CapabilityRuntimeState state)
        {
            m_States.Add((int)state);
        }

        public CapabilityRuntimeState GetState(int frameIndex)
        {
            if (frameIndex < 0 || frameIndex >= m_States.Count)
            {
                return CapabilityRuntimeState.None;
            }

            return (CapabilityRuntimeState)m_States[frameIndex];
        }

        public List<CapabilityTimelineSegment> BuildSegments()
        {
            m_Segments.Clear();
            if (m_States.Count == 0)
            {
                return m_Segments;
            }

            CapabilityRuntimeState previousState = (CapabilityRuntimeState)m_States[0];
            int segmentCount = 1;

            for (int i = 1; i < m_States.Count; i++)
            {
                CapabilityRuntimeState currentState = (CapabilityRuntimeState)m_States[i];
                if (currentState == previousState)
                {
                    segmentCount++;
                    continue;
                }

                m_Segments.Add(new CapabilityTimelineSegment
                {
                    Count = segmentCount,
                    State = previousState
                });

                previousState = currentState;
                segmentCount = 1;
            }

            m_Segments.Add(new CapabilityTimelineSegment
            {
                Count = segmentCount,
                State = previousState
            });

            return m_Segments;
        }
    }
}
