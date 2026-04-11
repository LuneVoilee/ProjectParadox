using System;

namespace Core.Capability
{
    public class CapabilityCollector : IDisposable
    {
        private EntityGroup[] m_Groups;

        private GroupChanged m_GroupChanged;

        private CapabilityBase m_Capability;

        public ChangeEventState.EventMask State = ChangeEventState.EventMask.AddRemoveUpdate;

        public static CapabilityCollector CreateCollector(CapabilityWorldBase world, CapabilityBase capability, params int[] componentIds)
        {
            EntityGroup[] groups = new EntityGroup[componentIds.Length];
            for (int i = 0; i < componentIds.Length; i++)
            {
                EntityMatcher matcher = EntityMatcher.SetAll(componentIds[i]);
                groups[i] = world.GetGroup(matcher);
            }

            CapabilityCollector collector = new CapabilityCollector();
            collector.m_Capability = capability;
            collector.InitCollector(groups);
            return collector;
        }

        public static void Release(CapabilityCollector capabilityCollector)
        {
            capabilityCollector?.Dispose();
        }

        private void InitCollector(EntityGroup[] groups)
        {
            m_Groups = groups;
            m_GroupChanged = OnGroupChanged;
            foreach (var group in m_Groups)
            {
                if ((State & ChangeEventState.EventMask.Add) != 0)
                {
                    group.GroupAdded += m_GroupChanged;
                }

                if ((State & ChangeEventState.EventMask.Remove) != 0)
                {
                    group.GroupRemoved += m_GroupChanged;
                }

                if ((State & ChangeEventState.EventMask.Update) != 0)
                {
                    group.GroupUpdated += m_GroupChanged;
                }

                AddExisting(group);
            }
        }

        private void AddExisting(EntityGroup group)
        {
            foreach (CEntity entity in group.EntitiesMap)
            {
                OnGroupChanged(group, entity);
            }
        }

        private void OnGroupChanged(EntityGroup group, CEntity entity)
        {
            m_Capability.ComponentChanged = true;
        }

        public void Dispose()
        {
            if (m_Groups != null)
            {
                for (int i = 0; i < m_Groups.Length; i++)
                {
                    EntityGroup group = m_Groups[i];
                    if (group == null)
                    {
                        continue;
                    }

                    group.GroupAdded -= m_GroupChanged;
                    group.GroupRemoved -= m_GroupChanged;
                    group.GroupUpdated -= m_GroupChanged;
                }
            }

            m_Groups = null;
            m_Capability = null;
            m_GroupChanged = null;
        }
    }
}
