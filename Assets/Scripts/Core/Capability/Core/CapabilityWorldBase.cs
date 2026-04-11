using System.Collections.Generic;

namespace Core.Capability
{
    public abstract partial class CapabilityWorldBase : IEntity, IWorldSystem, IInitializeSystem<int>, IUpdateSystem, IFixedUpdateSystem
    {
        public IEntity Parent { get; private set; }

        public int Id { get; private set; }

        public string Name { get; set; }

        public int Version { get; private set; }

        public IEntity.EntityState State { get; private set; }

        public bool IsActive => State == IEntity.EntityState.Running;

        public int MaxComponentCount { get; private set; }

        protected float DeltaTime { get; private set; }

        protected float FixedDeltaTime { get; private set; }

        public float TimeScale { get; private set; }

        protected int m_EntitySerialId;

        private Dictionary<EntityMatcher, EntityGroup> m_Groups;

        private List<EntityGroup>[] m_GroupsByComponent;

        public void OnDirty(IEntity parent, int id)
        {
            State = IEntity.EntityState.Running;
            Parent = parent;
            Id = id;
            Version++;
            m_EntitySerialId = 0;
        }

        public virtual void OnInitialize(int maxComponentCount)
        {
            MaxComponentCount = maxComponentCount > 0 ? maxComponentCount : 1;
            m_Groups = new Dictionary<EntityMatcher, EntityGroup>(128);
            m_GroupsByComponent = new List<EntityGroup>[MaxComponentCount];
            InitializeChildren();
            SetTimeScale(1f);
            CapabilityWorldRegistry.Register(this);
        }

        private void EnsureGroupCapacity(int componentId)
        {
            if (componentId < 0)
            {
                return;
            }

            if (m_GroupsByComponent == null)
            {
                int initialCapacity = componentId + 1;
                m_GroupsByComponent = new List<EntityGroup>[initialCapacity];
                MaxComponentCount = initialCapacity;
                return;
            }

            if (componentId < m_GroupsByComponent.Length)
            {
                return;
            }

            int newCapacity = m_GroupsByComponent.Length;
            if (newCapacity <= 0)
            {
                newCapacity = 1;
            }

            while (newCapacity <= componentId)
            {
                newCapacity *= 2;
            }

            System.Array.Resize(ref m_GroupsByComponent, newCapacity);
            MaxComponentCount = newCapacity;
        }

        protected virtual void SetTimeScale(float timeScale)
        {
            TimeScale = timeScale;
        }

        public EntityGroup GetGroup(EntityMatcher matcher)
        {
            if (matcher == null)
            {
                return null;
            }

            if (m_Groups.TryGetValue(matcher, out EntityGroup existed))
            {
                return existed;
            }

            EntityGroup group = EntityGroup.Create(ChildrenCount, matcher);
            foreach (CEntity entity in Children)
            {
                group.HandleEntitySilently(entity);
            }

            m_Groups.Add(matcher, group);
            for (int i = 0; i < matcher.Indices.Length; i++)
            {
                int componentId = matcher.Indices[i];
                if (componentId < 0)
                {
                    continue;
                }

                EnsureGroupCapacity(componentId);

                m_GroupsByComponent[componentId] ??= new List<EntityGroup>(16);
                m_GroupsByComponent[componentId].Add(group);
            }

            return group;
        }

        public void NotifyComponentChanged(int componentId, CEntity entity)
        {
            if (entity == null || m_GroupsByComponent == null)
            {
                return;
            }

            if (componentId < 0)
            {
                return;
            }

            EnsureGroupCapacity(componentId);

            List<EntityGroup> groups = m_GroupsByComponent[componentId];
            if (groups == null)
            {
                return;
            }

            foreach (var group in groups)
            {
                group.HandleEntity(entity);
            }
        }

        public virtual void OnUpdate(float elapsedSeconds, float realElapsedSeconds)
        {
            DeltaTime = elapsedSeconds * TimeScale;
        }

        public virtual void OnFixedUpdate(float elapsedSeconds, float realElapsedSeconds)
        {
            FixedDeltaTime = elapsedSeconds * TimeScale;
        }

        public virtual void Dispose()
        {
            CapabilityWorldRegistry.Unregister(this);
            DisposeChildren();

            if (m_Groups == null)
            {
                return;
            }

            foreach (KeyValuePair<EntityMatcher, EntityGroup> pair in m_Groups)
            {
                pair.Value.Dispose();
            }

            m_Groups.Clear();
            m_Groups = null;
            m_GroupsByComponent = null;
            m_EntitySerialId = 0;
            Version++;
        }
    }
}
