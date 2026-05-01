using System;
using System.Collections.Generic;

namespace Core.Capability
{
    public class CapabilityCollector : IDisposable
    {
        private EntityGroup[] m_Groups;

        private GroupChanged m_GroupChanged;

        private HashSet<CEntity> m_Collected;

        public ChangeEventState.EventMask State = ChangeEventState.EventMask.AddRemoveUpdate;

        public CapabilityBase Capability { get; private set; }

        public int Count => m_Collected?.Count ?? 0;

        public static CapabilityCollector CreateCollector(
            CapabilityWorldBase world,
            CapabilityBase capability,
            ChangeEventState.EventMask state,
            params int[] componentIds)
        {
            EntityGroup[] groups = new EntityGroup[componentIds.Length];
            for (int i = 0; i < componentIds.Length; i++)
            {
                EntityMatcher matcher = EntityMatcher.SetAll(componentIds[i]);
                groups[i] = world.GetGroup(matcher);
            }

            CapabilityCollector collector = new CapabilityCollector();
            collector.State = state;
            collector.Capability = capability;
            collector.InitCollector(groups);
            return collector;
        }

        public static CapabilityCollector CreateCollector(
            CapabilityWorldBase world,
            CapabilityBase capability,
            params int[] componentIds)
        {
            return CreateCollector(
                world, capability,
                ChangeEventState.EventMask.AddRemoveUpdate, componentIds);
        }

        public static void Release(CapabilityCollector capabilityCollector)
        {
            capabilityCollector?.Dispose();
        }

        private void InitCollector(EntityGroup[] groups)
        {
            m_Groups = groups;
            m_Collected = new HashSet<CEntity>();
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
            if (entity != null)
            {
                m_Collected.Add(entity);
            }
        }

        /// <summary>
        ///     将收集到的活跃实体转移到外部 buffer 并清空内部集合。
        /// </summary>
        public void Drain(List<CEntity> buffer)
        {
            if (buffer == null || m_Collected == null)
            {
                return;
            }

            buffer.Clear();
            foreach (var entity in m_Collected)
            {
                if (entity != null && entity.IsActive)
                {
                    buffer.Add(entity);
                }
            }

            m_Collected.Clear();
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
            m_GroupChanged = null;
            m_Collected?.Clear();
            m_Collected = null;
            Capability = null;
        }
    }
}
