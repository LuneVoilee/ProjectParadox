#region

using System;
using System.Collections.Generic;
using Tool;

#endregion

namespace Core.Capability
{
    public abstract class CapabilityBase : IDisposable
    {
        public int Id { get; private set; }

        public List<int> TagList { get; protected set; }

        public CapabilityWorld World { get; private set; }

        public CEntity Owner { get; private set; }

        public bool IsActive { get; internal set; }

        public virtual CapabilityUpdateMode UpdateMode { get; protected set; } =
            CapabilityUpdateMode.Update;

        public virtual int TickGroupOrder { get; protected set; }

        protected CapabilityCollector m_CapabilityCollector;

        private int[] m_FilterComponentIds;

        private readonly HashSet<int> m_GlobalActiveEntityIds = new HashSet<int>();

        private readonly List<int> m_GlobalRemoveBuffer = new List<int>(16);

        private readonly List<CEntity> m_GlobalQueryBuffer = new List<CEntity>(64);

        internal bool ComponentChanged;

        internal bool TryComponentChanged
        {
            get
            {
                bool changed = ComponentChanged;
                if (m_CapabilityCollector != null)
                {
                    ComponentChanged = false;
                }

                return changed;
            }
        }

        public virtual bool IsGlobal { get; protected set; }

        public IReadOnlyCollection<int> GlobalActiveEntityIds => m_GlobalActiveEntityIds;

        public double LastTickMilliseconds { get; internal set; }

        public int LastMatchedEntityCount { get; internal set; }

        public List<int> LastMatchedEntityIds { get; } = new List<int>(32);

        public void InitGlobal(int id, CapabilityWorld world)
        {
            Id = id;
            World = world;
            Owner = null;
            ComponentChanged = true;
            IsGlobal = true;
            OnInit();
            OnCreated();
        }

        public void Init(int id, CapabilityWorld world, CEntity owner)
        {
            Id = id;
            World = world;
            Owner = owner;
            ComponentChanged = true;
            OnInit();
            OnCreated();
        }

        protected virtual void OnInit()
        {
        }

        protected virtual void OnCreated()
        {
        }

        protected void Filter(params int[] componentIds)
        {
            if (componentIds == null)
            {
                throw new ArgumentNullException(nameof(componentIds),
                    $"{GetType().Name} componentIds is null");
            }

            m_FilterComponentIds = componentIds;
            m_CapabilityCollector = CapabilityCollector.CreateCollector(World, this, componentIds);
            ComponentChanged = false;
        }

        public virtual bool ShouldActivate()
        {
            return false;
        }

        public virtual bool ShouldDeactivate()
        {
            return true;
        }


        public void Activated()
        {
            IsActive = true;
            OnActivated();
        }

        protected virtual void OnActivated()
        {
        }

        public void Deactivated()
        {
            IsActive = false;
            OnDeactivated();
        }

        protected virtual void OnDeactivated()
        {
        }

        public virtual void TickActive(float deltaTime, float realElapsedSeconds)
        {
        }

        public virtual void Tick
            (CapabilityContext context, float deltaTime, float realElapsedSeconds)
        {
            if (m_FilterComponentIds == null || m_FilterComponentIds.Length == 0)
            {
                return;
            }

            EntityGroup group = context.QueryByIds(m_FilterComponentIds);
            if (group?.EntitiesMap == null)
            {
                DeactivateMissingGlobalEntities(context);
                return;
            }

            m_GlobalQueryBuffer.Clear();
            foreach (CEntity entity in group.EntitiesMap)
            {
                if (entity != null)
                {
                    m_GlobalQueryBuffer.Add(entity);
                }
            }

            for (int i = 0; i < m_GlobalQueryBuffer.Count; i++)
            {
                CEntity entity = m_GlobalQueryBuffer[i];
                if (entity == null || !entity.IsActive)
                {
                    continue;
                }

                Owner = entity;
                if (!m_GlobalActiveEntityIds.Contains(entity.Id))
                {
                    if (!ShouldActivate())
                    {
                        continue;
                    }

                    m_GlobalActiveEntityIds.Add(entity.Id);
                    IsActive = true;
                    OnActivated();
                }
                else if (ShouldDeactivate())
                {
                    m_GlobalActiveEntityIds.Remove(entity.Id);
                    OnDeactivated();
                    IsActive = m_GlobalActiveEntityIds.Count > 0;
                    continue;
                }

                TickActive(deltaTime, realElapsedSeconds);
            }

            Owner = null;
            IsActive = m_GlobalActiveEntityIds.Count > 0;
            DeactivateMissingGlobalEntities(context);
        }

        private void DeactivateMissingGlobalEntities(CapabilityContext context)
        {
            if (m_GlobalActiveEntityIds.Count == 0)
            {
                IsActive = false;
                return;
            }

            m_GlobalRemoveBuffer.Clear();
            foreach (int entityId in m_GlobalActiveEntityIds)
            {
                if (!context.TryGetEntity(entityId, out CEntity entity))
                {
                    m_GlobalRemoveBuffer.Add(entityId);
                    continue;
                }

                Owner = entity;
                if (m_FilterComponentIds != null && entity.HasComponents(m_FilterComponentIds))
                {
                    continue;
                }

                m_GlobalRemoveBuffer.Add(entityId);
                OnDeactivated();
            }

            Owner = null;
            for (int i = 0; i < m_GlobalRemoveBuffer.Count; i++)
            {
                m_GlobalActiveEntityIds.Remove(m_GlobalRemoveBuffer[i]);
            }

            IsActive = m_GlobalActiveEntityIds.Count > 0;
        }

        public virtual void Dispose()
        {
            if (m_CapabilityCollector != null)
            {
                CapabilityCollector.Release(m_CapabilityCollector);
            }

            m_CapabilityCollector = null;
            IsActive = false;
            TagList = null;
            Owner = null;
            World = null;
            LastTickMilliseconds = 0d;
            LastMatchedEntityCount = 0;
            LastMatchedEntityIds.Clear();
            m_FilterComponentIds = null;
            m_GlobalActiveEntityIds.Clear();
            m_GlobalRemoveBuffer.Clear();
            m_GlobalQueryBuffer.Clear();
        }
    }

    public sealed class CapabilityContext
    {
        private CapabilityWorld m_World;
        private CapabilityBase m_Capability;
        private CapabilityCommandBuffer m_Commands;

        public CapabilityWorld World => m_World;

        public CapabilityBase Capability => m_Capability;

        public CapabilityCommandBuffer Commands => m_Commands;

        internal void Reset
        (
            CapabilityWorld world, CapabilityBase capability,
            CapabilityCommandBuffer commands
        )
        {
            m_World = world;
            m_Capability = capability;
            m_Commands = commands;
            if (m_Capability == null)
            {
                return;
            }

            m_Capability.LastMatchedEntityCount = 0;
            m_Capability.LastMatchedEntityIds.Clear();
        }

        public EntityGroup Query<T1>() where T1 : CComponent
        {
            return QueryByIds(Component<T1>.TId);
        }

        public EntityGroup Query<T1, T2>()
            where T1 : CComponent where T2 : CComponent
        {
            return QueryByIds(Component<T1>.TId, Component<T2>.TId);
        }

        public EntityGroup Query<T1, T2, T3>()
            where T1 : CComponent where T2 : CComponent where T3 : CComponent
        {
            return QueryByIds(Component<T1>.TId, Component<T2>.TId, Component<T3>.TId);
        }

        public EntityGroup Query<T1, T2, T3, T4>()
            where T1 : CComponent where T2 : CComponent
            where T3 : CComponent where T4 : CComponent
        {
            return QueryByIds(Component<T1>.TId, Component<T2>.TId,
                Component<T3>.TId, Component<T4>.TId);
        }

        public EntityGroup QueryByIds(params int[] componentIds)
        {
            if (m_World == null)
            {
                return null;
            }

            EntityGroup group = m_World.GetGroup(EntityMatcher.SetAll(componentIds));
            RecordMatchedEntities(group?.EntitiesMap);
            return group;
        }

        public bool TryGetEntity(int entityId, out CEntity entity)
        {
            entity = null;
            if (m_World == null || entityId < 0)
            {
                return false;
            }

            entity = m_World.GetChild(entityId);
            return entity != null && entity.IsActive;
        }

        public bool TryGetSingleton<TComponent>(out TComponent component)
            where TComponent : CComponent
        {
            component = null;
            if (!TryGetSingletonEntity<TComponent>(out CEntity entity))
            {
                return false;
            }

            return entity.TryGetComponent(Component<TComponent>.TId, out component);
        }

        public bool TryGetSingletonEntity<TComponent>(out CEntity entity)
            where TComponent : CComponent
        {
            entity = null;
            EntityGroup group = Query<TComponent>();
            if (group?.EntitiesMap == null)
            {
                return false;
            }

            foreach (CEntity candidate in group.EntitiesMap)
            {
                if (candidate == null || !candidate.IsActive)
                {
                    continue;
                }

                entity = candidate;
                return true;
            }

            return false;
        }

        public void Log(string message)
        {
            CapabilityDebugLogStream.Add(m_Capability, message);
            UnityEngine.Debug.Log(message);
        }

        public void SetMatchedEntities(IndexedSet<CEntity> entities)
        {
            RecordMatchedEntities(entities);
        }

        private void RecordMatchedEntities(IndexedSet<CEntity> entities)
        {
            if (m_Capability == null || entities == null)
            {
                return;
            }

            m_Capability.LastMatchedEntityIds.Clear();
            foreach (CEntity entity in entities)
            {
                if (entity == null)
                {
                    continue;
                }

                m_Capability.LastMatchedEntityIds.Add(entity.Id);
            }

            m_Capability.LastMatchedEntityCount = m_Capability.LastMatchedEntityIds.Count;
        }
    }

    public static class CapabilityDebugLogStream
    {
        public struct Entry
        {
            public double Time;
            public string Message;
        }

        private static readonly Dictionary<CapabilityBase, List<Entry>> m_Entries =
            new Dictionary<CapabilityBase, List<Entry>>(128);

        public static void Add(CapabilityBase capability, string message)
        {
            if (capability == null)
            {
                return;
            }

            if (!m_Entries.TryGetValue(capability, out List<Entry> entries))
            {
                entries = new List<Entry>(8);
                m_Entries.Add(capability, entries);
            }

            entries.Add(new Entry
            {
                Time = UnityEngine.Time.realtimeSinceStartup,
                Message = message ?? string.Empty
            });
        }

        public static List<Entry> Consume(CapabilityBase capability)
        {
            if (capability == null)
            {
                return null;
            }

            if (!m_Entries.TryGetValue(capability, out List<Entry> entries))
            {
                return null;
            }

            m_Entries.Remove(capability);
            return entries;
        }

        public static void Clear()
        {
            m_Entries.Clear();
        }
    }

    public sealed class CapabilityCommandBuffer
    {
        private readonly List<Action> m_Commands = new List<Action>(64);
        private CapabilityWorld m_World;

        internal void Reset(CapabilityWorld world)
        {
            m_World = world;
        }

        public void AddComponent<TComponent>
            (CEntity entity, Action<TComponent> configure = null)
            where TComponent : CComponent, new()
        {
            if (entity == null)
            {
                return;
            }

            m_Commands.Add(() =>
            {
                if (entity.State != IEntity.EntityState.Running)
                {
                    return;
                }

                TComponent component = entity.AddComponent<TComponent>();
                configure?.Invoke(component);
            });
        }

        public void RemoveComponent(CEntity entity, int componentId)
        {
            if (entity == null)
            {
                return;
            }

            m_Commands.Add(() =>
            {
                if (entity.State != IEntity.EntityState.Running)
                {
                    return;
                }

                entity.RemoveComponent(componentId);
            });
        }

        public void DestroyEntity(CEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            m_Commands.Add(() =>
            {
                if (m_World == null || entity.State != IEntity.EntityState.Running)
                {
                    return;
                }

                m_World.RemoveChild(entity);
            });
        }

        public void CreateEventEntity<TComponent>
            (Action<TComponent> configure = null, string name = null)
            where TComponent : CComponent, new()
        {
            m_Commands.Add(() =>
            {
                if (m_World == null)
                {
                    return;
                }

                CEntity entity = m_World.AddChild(name ?? typeof(TComponent).Name);
                TComponent component = entity.AddComponent<TComponent>();
                configure?.Invoke(component);
            });
        }

        public void RemoveEventEntities<TComponent>() where TComponent : CComponent
        {
            m_Commands.Add(() =>
            {
                if (m_World == null)
                {
                    return;
                }

                EntityGroup group = m_World.GetGroup(
                    EntityMatcher.SetAll(Component<TComponent>.TId));
                if (group?.EntitiesMap == null)
                {
                    return;
                }

                List<CEntity> buffer = new List<CEntity>(group.EntitiesMap.Count);
                foreach (CEntity entity in group.EntitiesMap)
                {
                    if (entity != null)
                    {
                        buffer.Add(entity);
                    }
                }

                for (int i = 0; i < buffer.Count; i++)
                {
                    m_World.RemoveChild(buffer[i]);
                }
            });
        }

        public void Flush()
        {
            for (int i = 0; i < m_Commands.Count; i++)
            {
                m_Commands[i]?.Invoke();
            }

            m_Commands.Clear();
        }
    }
}
