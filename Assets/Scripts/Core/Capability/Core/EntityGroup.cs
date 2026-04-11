using System;
using Tool;

namespace Core.Capability
{
    public class EntityGroup : IDisposable
    {
        private EntityMatcher m_Matcher;

        public event GroupChanged GroupAdded;
        public event GroupChanged GroupRemoved;
        public event GroupChanged GroupUpdated;

        public IndexedSet<CEntity> EntitiesMap { get; private set; }

        public static EntityGroup Create(int childCount, EntityMatcher matcher)
        {
            EntityGroup group = new EntityGroup();
            group.m_Matcher = matcher;
            group.EntitiesMap = new IndexedSet<CEntity>(childCount);
            return group;
        }

        public int HandleEntitySilently(CEntity entity)
        {
            return HandleEntityInternal(entity, true);
        }

        public int HandleEntity(CEntity entity)
        {
            return HandleEntityInternal(entity, false);
        }

        private int HandleEntityInternal(CEntity entity, bool silently)
        {
            bool match = m_Matcher.Match(entity);
            if (match)
            {
                AddOrUpdate(entity, silently);
            }
            else
            {
                Remove(entity, silently);
            }

            return EntitiesMap.Count;
        }

        private void AddOrUpdate(CEntity entity, bool silently)
        {
            bool isNew = EntitiesMap.Add(entity);
            if (silently)
            {
                return;
            }

            if (isNew)
            {
                GroupAdded?.Invoke(this, entity);
            }
            else
            {
                GroupUpdated?.Invoke(this, entity);
            }
        }

        private void Remove(CEntity entity, bool silently)
        {
            bool removed = EntitiesMap.Remove(entity);
            if (!silently && removed)
            {
                GroupRemoved?.Invoke(this, entity);
            }
        }

        public void Dispose()
        {
            GroupAdded = null;
            GroupRemoved = null;
            GroupUpdated = null;
            EntitiesMap?.Clear();
            EntitiesMap = null;
            m_Matcher = null;
        }
    }
}
