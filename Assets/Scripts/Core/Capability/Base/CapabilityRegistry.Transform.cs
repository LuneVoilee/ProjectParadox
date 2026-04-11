using System.Collections.Generic;
using Tool;

namespace Core.Capability
{
    public partial class CapabilityRegistry
    {
        public void Add<TCapability>(CEntity entity) where TCapability : CapabilityBase, new()
        {
            if (entity == null)
            {
                return;
            }

            TCapability capability = new TCapability();
            if (capability.UpdateMode == CapabilityUpdateMode.Update)
            {
                int capabilityId = CapabilityId<TCapability, IUpdateSystem>.TId;
                SetCapability(m_UpdateCapabilities, entity, capabilityId, capability);
            }
            else
            {
                int capabilityId = CapabilityId<TCapability, IFixedUpdateSystem>.TId;
                SetCapability(m_FixedUpdateCapabilities, entity, capabilityId, capability);
            }
        }

        private void SetCapability(IndexedObjectArray<CapabilityBase>[] arrays, CEntity entity, int capabilityId, CapabilityBase capability)
        {
            if (capabilityId < 0)
            {
                capability.Dispose();
                return;
            }

            if (capabilityId >= arrays.Length)
            {
                if (ReferenceEquals(arrays, m_UpdateCapabilities))
                {
                    System.Array.Resize(ref m_UpdateCapabilities, capabilityId + 1);
                    arrays = m_UpdateCapabilities;
                }
                else if (ReferenceEquals(arrays, m_FixedUpdateCapabilities))
                {
                    System.Array.Resize(ref m_FixedUpdateCapabilities, capabilityId + 1);
                    arrays = m_FixedUpdateCapabilities;
                }
            }

            IndexedObjectArray<CapabilityBase> array = arrays[capabilityId];
            if (array == null)
            {
                array = new IndexedObjectArray<CapabilityBase>();
                array.Init(m_EstimatedEntityCount);
                arrays[capabilityId] = array;
            }

            CapabilityBase stored = array.Set(entity.Id, capability);
            stored.Init(capabilityId, m_World, entity);
        }

        public void GetCapabilitiesByEntity(CEntity entity, List<CapabilityBase> updateCapabilities, List<CapabilityBase> fixedUpdateCapabilities)
        {
            GetCapabilitiesByEntityInternal(entity, m_UpdateCapabilities, updateCapabilities);
            GetCapabilitiesByEntityInternal(entity, m_FixedUpdateCapabilities, fixedUpdateCapabilities);
        }

        private void GetCapabilitiesByEntityInternal(CEntity entity, IndexedObjectArray<CapabilityBase>[] source, List<CapabilityBase> destination)
        {
            for (int i = 0; i < source.Length; i++)
            {
                IndexedObjectArray<CapabilityBase> array = source[i];
                if (array == null)
                {
                    continue;
                }

                CapabilityBase capability = array[entity.Id];
                if (capability != null)
                {
                    destination.Add(capability);
                }
            }
        }

        public void Remove(CEntity entity, int capabilityId)
        {
            RemoveFromArray(m_UpdateCapabilities, entity, capabilityId);
            RemoveFromArray(m_FixedUpdateCapabilities, entity, capabilityId);
        }

        private void RemoveFromArray(IndexedObjectArray<CapabilityBase>[] arrays, CEntity entity, int capabilityId)
        {
            if (capabilityId < 0 || capabilityId >= arrays.Length)
            {
                return;
            }

            IndexedObjectArray<CapabilityBase> array = arrays[capabilityId];
            if (array == null)
            {
                return;
            }

            RemoveArrayEntry(array, entity);
        }

        public void RemoveAllByEntity(CEntity entity)
        {
            RemoveAllByEntityInternal(m_UpdateCapabilities, entity);
            RemoveAllByEntityInternal(m_FixedUpdateCapabilities, entity);
        }

        private void RemoveAllByEntityInternal(IndexedObjectArray<CapabilityBase>[] arrays, CEntity entity)
        {
            for (int i = 0; i < arrays.Length; i++)
            {
                IndexedObjectArray<CapabilityBase> array = arrays[i];
                if (array == null)
                {
                    continue;
                }

                RemoveArrayEntry(array, entity);
            }
        }

        private void RemoveArrayEntry(IndexedObjectArray<CapabilityBase> array, CEntity entity)
        {
            CapabilityBase capability = array.Remove(entity.Id);
            capability?.Dispose();
        }
    }
}
