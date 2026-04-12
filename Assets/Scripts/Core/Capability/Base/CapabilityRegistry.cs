#region

using System;
using Tool;

#endregion

namespace Core.Capability
{
    public partial class CapabilityRegistry
    {
        private IndexedObjectArray<CapabilityBase>[] m_UpdateCapabilities;

        private IndexedObjectArray<CapabilityBase>[] m_FixedUpdateCapabilities;

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
            ProcessCapabilities(m_UpdateCapabilities, deltaTime, realElapsedSeconds);
        }

        public void OnFixedUpdate(float deltaTime, float realElapsedSeconds)
        {
            ProcessCapabilities(m_FixedUpdateCapabilities, deltaTime, realElapsedSeconds);
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
            ClearCapabilities(m_UpdateCapabilities);
            ClearCapabilities(m_FixedUpdateCapabilities);
            m_UpdateCapabilities = null;
            m_FixedUpdateCapabilities = null;
            m_World = null;
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