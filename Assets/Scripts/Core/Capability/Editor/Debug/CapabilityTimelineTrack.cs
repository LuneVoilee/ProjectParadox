using System.Collections.Generic;
using UnityEngine;

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
        private readonly int[] m_States;

        private readonly List<CapabilityTimelineSegment> m_Segments = new List<CapabilityTimelineSegment>(32);

        private int m_WriteIndex;

        private int m_Count;

        public string Name { get; }

        public int LastSampleIndex { get; private set; } = -1;

        public CapabilityTimelineTrack(int frameSize, string name)
        {
            m_States = new int[Mathf.Max(1, frameSize)];
            Name = name;
        }

        public void Push(CapabilityRuntimeState state, int sampleIndex)
        {
            m_States[m_WriteIndex] = (int)state;
            m_WriteIndex = (m_WriteIndex + 1) % m_States.Length;
            m_Count = Mathf.Min(m_Count + 1, m_States.Length);
            LastSampleIndex = sampleIndex;
        }

        public List<CapabilityTimelineSegment> BuildSegments()
        {
            m_Segments.Clear();
            if (m_Count == 0)
            {
                return m_Segments;
            }

            int startIndex = m_WriteIndex - m_Count;
            if (startIndex < 0)
            {
                startIndex += m_States.Length;
            }

            CapabilityRuntimeState previousState = (CapabilityRuntimeState)m_States[startIndex];
            int segmentCount = 1;

            for (int i = 1; i < m_Count; i++)
            {
                int index = (startIndex + i) % m_States.Length;
                CapabilityRuntimeState currentState = (CapabilityRuntimeState)m_States[index];
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
