#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Tool;

#endregion

namespace Core.Capability
{
    public partial class CapabilityRegistry
    {
        private IndexedObjectArray<CapabilityBase>[] m_UpdateCapabilities;

        private IndexedObjectArray<CapabilityBase>[] m_FixedUpdateCapabilities;

        private readonly List<CapabilityBase> m_GlobalUpdateCapabilities =
            new List<CapabilityBase>(64);

        private readonly List<CapabilityBase> m_GlobalFixedUpdateCapabilities =
            new List<CapabilityBase>(16);

        private readonly CapabilityContext m_Context = new CapabilityContext();

        private readonly CapabilityCommandBuffer m_CommandBuffer =
            new CapabilityCommandBuffer();

        private readonly Stopwatch m_Stopwatch = new Stopwatch();

        private int m_EstimatedEntityCount;

        private CapabilityWorld m_World;

        public void Init(CapabilityWorld world, int capabilityCount, int estimatedEntityCount)
        {
            m_World = world;
            m_EstimatedEntityCount = estimatedEntityCount;
            int capacity = Math.Max(1, capabilityCount);
            m_UpdateCapabilities = new IndexedObjectArray<CapabilityBase>[capacity];
            m_FixedUpdateCapabilities = new IndexedObjectArray<CapabilityBase>[capacity];
        }

        public void OnUpdate(float deltaTime, float realElapsedSeconds)
        {
            ProcessGlobalCapabilities(m_GlobalUpdateCapabilities, deltaTime, realElapsedSeconds);
            ProcessCapabilities(m_UpdateCapabilities, deltaTime, realElapsedSeconds);
        }

        public void OnFixedUpdate(float deltaTime, float realElapsedSeconds)
        {
            ProcessGlobalCapabilities(m_GlobalFixedUpdateCapabilities, deltaTime,
                realElapsedSeconds);
            ProcessCapabilities(m_FixedUpdateCapabilities, deltaTime, realElapsedSeconds);
        }

        private void ProcessGlobalCapabilities
        (
            List<CapabilityBase> capabilities, float deltaTime,
            float realElapsedSeconds
        )
        {
            if (capabilities == null || capabilities.Count == 0)
            {
                return;
            }

            for (int i = 0; i < capabilities.Count; i++)
            {
                CapabilityBase capability = capabilities[i];
                if (capability == null)
                {
                    continue;
                }

                m_CommandBuffer.Reset(m_World);
                m_Context.Reset(m_World, capability, m_CommandBuffer);
                m_Stopwatch.Restart();
                capability.Tick(m_Context, deltaTime, realElapsedSeconds);
                m_Stopwatch.Stop();
                capability.LastTickMilliseconds = m_Stopwatch.Elapsed.TotalMilliseconds;
                m_CommandBuffer.Flush();
            }
        }

        private void ProcessCapabilities
            (IndexedObjectArray<CapabilityBase>[] arrays, float deltaTime, float realElapsedSeconds)
        {
            if (arrays == null || arrays.Length == 0)
            {
                return;
            }

            foreach (var capabilityArray in arrays)
            {
                if (capabilityArray == null)
                {
                    continue;
                }

                UpdateCapabilityArray(capabilityArray, deltaTime, realElapsedSeconds);
            }
        }

        private void UpdateCapabilityArray
        (
            IndexedObjectArray<CapabilityBase> capabilityArray, float deltaTime,
            float realElapsedSeconds
        )
        {
            foreach (CapabilityBase capability in capabilityArray)
            {
                CEntity owner = capability.Owner;
                if (capability.TagList != null)
                {
                    CapabilityBlockComponent blockComponent =
                        (CapabilityBlockComponent)owner.GetComponent(
                            Component<CapabilityBlockComponent>.TId);
                    if (blockComponent != null && blockComponent.IsBlocked(capability.TagList))
                    {
                        continue;
                    }
                }

                if (capability.TryComponentChanged)
                {
                    if (!capability.IsActive)
                    {
                        bool shouldActivate = capability.ShouldActivate();
                        if (shouldActivate)
                        {
                            capability.Activated();
                        }
                    }
                    else
                    {
                        bool shouldDeactivate = capability.ShouldDeactivate();
                        if (shouldDeactivate)
                        {
                            capability.Deactivated();
                        }
                    }
                }

                if (capability.IsActive)
                {
                    capability.TickActive(deltaTime, realElapsedSeconds);
                }
            }
        }

        public void Dispose()
        {
            ClearGlobalCapabilities(m_GlobalUpdateCapabilities);
            ClearGlobalCapabilities(m_GlobalFixedUpdateCapabilities);
            ClearCapabilities(m_UpdateCapabilities);
            ClearCapabilities(m_FixedUpdateCapabilities);
            m_UpdateCapabilities = null;
            m_FixedUpdateCapabilities = null;
            m_World = null;
        }

        private void ClearGlobalCapabilities(List<CapabilityBase> capabilities)
        {
            if (capabilities == null)
            {
                return;
            }

            for (int i = 0; i < capabilities.Count; i++)
            {
                capabilities[i]?.Dispose();
            }

            capabilities.Clear();
        }

        private void ClearCapabilities(IndexedObjectArray<CapabilityBase>[] arrays)
        {
            if (arrays == null)
            {
                return;
            }

            for (int i = 0; i < arrays.Length; i++)
            {
                IndexedObjectArray<CapabilityBase> array = arrays[i];
                if (array == null)
                {
                    continue;
                }

                foreach (CapabilityBase capability in array)
                {
                    capability?.Dispose();
                }

                array.Dispose();
            }
        }
    }
}
