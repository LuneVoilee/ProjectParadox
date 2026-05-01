#region

using System;
using System.Collections.Generic;
using Tool;
using UnityEngine;

#endregion

namespace Core.Capability
{
    public enum CapabilityRunState
    {
        NotRun = 0,
        NoMatch = 1,
        Matched = 2,
        Worked = 3,
        Error = 4
    }

    public abstract class CapabilityBase : IDisposable
    {
        public int Id { get; private set; }

        public CapabilityWorld World { get; private set; }

        public virtual CapabilityUpdateMode UpdateMode { get; protected set; } =
            CapabilityUpdateMode.Update;

        public virtual int TickGroupOrder { get; protected set; }

        public virtual string Pipeline => CapabilityPipeline.Other;

        public virtual string DebugTag => Pipeline;

        public double LastTickMilliseconds { get; internal set; }

        public CapabilityRunState LastRunState { get; internal set; } =
            CapabilityRunState.NotRun;

        public string LastErrorMessage { get; internal set; }

        public int LastMatchedEntityCount { get; internal set; }

        public List<int> LastMatchedEntityIds { get; } = new List<int>(32);

        internal void InitGlobal(int id, CapabilityWorld world)
        {
            Id = id;
            World = world;
            OnCreated();
        }

        protected virtual void OnCreated()
        {
        }

        protected virtual void OnDestroyed()
        {
        }

        public virtual void Tick
            (CapabilityContext context, float deltaTime, float realElapsedSeconds)
        {
        }

        public virtual void Dispose()
        {
            OnDestroyed();
            World = null;
            LastTickMilliseconds = 0d;
            LastRunState = CapabilityRunState.NotRun;
            LastErrorMessage = null;
            LastMatchedEntityCount = 0;
            LastMatchedEntityIds.Clear();
        }
    }

    public static class CapabilityPipeline
    {
        public const string MapBootstrap = "MapBootstrap";
        public const string ScenarioSetup = "ScenarioSetup";
        public const string MoveCommand = "MoveCommand";
        public const string Combat = "Combat";
        public const string Territory = "Territory";
        public const string Camera = "Camera";
        public const string MapRender = "MapRender";
        public const string SelectionUI = "SelectionUI";
        public const string PathUI = "PathUI";
        public const string Cleanup = "Cleanup";
        public const string Other = "Other";

        public static string List(params string[] pipelines)
        {
            return string.Join(",", pipelines);
        }
    }

    public sealed class CapabilityContext
    {
        public CapabilityWorld World { get; private set; }

        public CapabilityBase Capability { get; private set; }

        public CapabilityCommandBuffer Commands { get; private set; }

        internal void Reset
        (
            CapabilityWorld world, CapabilityBase capability,
            CapabilityCommandBuffer commands
        )
        {
            World = world;
            Capability = capability;
            Commands = commands;
            if (Capability == null)
            {
                return;
            }

            Capability.LastMatchedEntityCount = 0;
            Capability.LastMatchedEntityIds.Clear();
            Capability.LastErrorMessage = null;
            Capability.LastRunState = CapabilityRunState.NoMatch;
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
            where T1 : CComponent
            where T2 : CComponent
            where T3 : CComponent
            where T4 : CComponent
        {
            return QueryByIds(Component<T1>.TId, Component<T2>.TId,
                Component<T3>.TId, Component<T4>.TId);
        }

        public EntityGroup QueryByIds(params int[] componentIds)
        {
            if (World == null)
            {
                return null;
            }

            EntityGroup group = World.GetGroup(EntityMatcher.SetAll(componentIds));
            RecordMatchedEntities(group?.EntitiesMap);
            return group;
        }

        public void QuerySnapshot<T1>(List<CEntity> buffer)
            where T1 : CComponent
        {
            QuerySnapshotByIds(buffer, Component<T1>.TId);
        }

        public void QuerySnapshot<T1, T2>(List<CEntity> buffer)
            where T1 : CComponent where T2 : CComponent
        {
            QuerySnapshotByIds(buffer, Component<T1>.TId, Component<T2>.TId);
        }

        public void QuerySnapshot<T1, T2, T3>(List<CEntity> buffer)
            where T1 : CComponent where T2 : CComponent where T3 : CComponent
        {
            QuerySnapshotByIds(buffer, Component<T1>.TId, Component<T2>.TId,
                Component<T3>.TId);
        }

        public void QuerySnapshot<T1, T2, T3, T4>(List<CEntity> buffer)
            where T1 : CComponent
            where T2 : CComponent
            where T3 : CComponent
            where T4 : CComponent
        {
            QuerySnapshotByIds(buffer, Component<T1>.TId, Component<T2>.TId,
                Component<T3>.TId, Component<T4>.TId);
        }

        public void QuerySnapshotByIds(List<CEntity> buffer, params int[] componentIds)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();
            EntityGroup group = QueryByIds(componentIds);
            if (group?.EntitiesMap == null)
            {
                RecordSnapshot(buffer);
                return;
            }

            foreach (CEntity entity in group.EntitiesMap)
            {
                if (entity == null)
                {
                    continue;
                }

                if (!entity.IsActive)
                {
                    continue;
                }

                buffer.Add(entity);
            }

            RecordSnapshot(buffer);
        }

        public bool TryGetEntity(int entityId, out CEntity entity)
        {
            entity = null;
            if (World == null || entityId < 0)
            {
                return false;
            }

            entity = World.GetChild(entityId);
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

        public void MarkWorked()
        {
            if (Capability == null)
            {
                return;
            }

            Capability.LastRunState = CapabilityRunState.Worked;
        }

        public void Log(string message)
        {
            MarkWorked();
#if UNITY_EDITOR
            CapabilityDebugLogStream.Add(Capability, message);
#endif
            CapabilityTraceStream.Log(Capability, message);
            Debug.Log(message);
        }

        public void TracePhase(string phase, CEntity entity = null)
        {
            CapabilityTraceStream.Phase(Capability, phase, entity);
        }

        public void TracePhase
        (
            string phase, CEntity entity, string key,
            object value
        )
        {
            CapabilityTraceStream.Phase(Capability, phase, entity, key, value);
        }

        internal void MarkError(Exception exception)
        {
            if (Capability == null)
            {
                return;
            }

            Capability.LastRunState = CapabilityRunState.Error;
            Capability.LastErrorMessage = exception?.ToString();
#if UNITY_EDITOR
            CapabilityDebugLogStream.Add(Capability, Capability.LastErrorMessage);
#endif
            CapabilityTraceStream.Log(Capability, Capability.LastErrorMessage);
        }

        private void RecordMatchedEntities(IndexedSet<CEntity> entities)
        {
            if (Capability == null || entities == null)
            {
                return;
            }

            Capability.LastMatchedEntityIds.Clear();
            foreach (CEntity entity in entities)
            {
                if (entity == null)
                {
                    continue;
                }

                Capability.LastMatchedEntityIds.Add(entity.Id);
            }

            Capability.LastMatchedEntityCount = Capability.LastMatchedEntityIds.Count;
            if (Capability.LastRunState != CapabilityRunState.Worked &&
                Capability.LastRunState != CapabilityRunState.Error)
            {
                Capability.LastRunState = Capability.LastMatchedEntityCount > 0
                    ? CapabilityRunState.Matched
                    : CapabilityRunState.NoMatch;
            }
        }

        private void RecordSnapshot(List<CEntity> entities)
        {
            if (Capability == null)
            {
                return;
            }

            Capability.LastMatchedEntityIds.Clear();
            if (entities != null)
            {
                for (int i = 0; i < entities.Count; i++)
                {
                    if (entities[i] != null)
                    {
                        Capability.LastMatchedEntityIds.Add(entities[i].Id);
                    }
                }
            }

            Capability.LastMatchedEntityCount = Capability.LastMatchedEntityIds.Count;
            if (Capability.LastRunState != CapabilityRunState.Worked &&
                Capability.LastRunState != CapabilityRunState.Error)
            {
                Capability.LastRunState = Capability.LastMatchedEntityCount > 0
                    ? CapabilityRunState.Matched
                    : CapabilityRunState.NoMatch;
            }
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
                Time = Time.realtimeSinceStartup,
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
        private readonly List<CommandEntry> m_Commands = new List<CommandEntry>(64);
        private readonly List<CEntity> m_EntityBuffer = new List<CEntity>(32);
        private CapabilityWorld m_World;
        private CapabilityContext m_Context;

        private sealed class CommandEntry
        {
            public string Name;
            public CEntity Entity;
            public string Path;
            public string Value;
            public CapabilityBase Capability;
            public Action Execute;
        }

        internal void Reset(CapabilityWorld world, CapabilityContext context)
        {
            m_World = world;
            m_Context = context;
        }

        public void AddComponent<TComponent>
            (CEntity entity, Action<TComponent> configure = null)
            where TComponent : CComponent, new()
        {
            if (entity == null)
            {
                return;
            }

            m_Context?.MarkWorked();
            CapabilityBase capability = m_Context?.Capability;
            Enqueue(new CommandEntry
            {
                Name = "AddComponent",
                Entity = entity,
                Path = typeof(TComponent).FullName,
                Capability = capability,
                Execute = () =>
                {
                    if (entity.State != IEntity.EntityState.Running)
                    {
                        return;
                    }

                    TComponent component = entity.AddComponent<TComponent>();
                    configure?.Invoke(component);
                    CapabilityTraceStream.CommandFlushed(
                        capability,
                        "AddComponent",
                        entity,
                        typeof(TComponent).FullName,
                        CapabilityTraceStream.CaptureObjectFields(component));
                }
            });
        }

        public void RemoveComponent(CEntity entity, int componentId)
        {
            if (entity == null)
            {
                return;
            }

            m_Context?.MarkWorked();
            CapabilityBase capability = m_Context?.Capability;
            Enqueue(new CommandEntry
            {
                Name = "RemoveComponent",
                Entity = entity,
                Path = componentId.ToString(),
                Capability = capability,
                Execute = () =>
                {
                    if (entity.State != IEntity.EntityState.Running)
                    {
                        return;
                    }

                    CComponent component = entity.GetComponent(componentId);
                    string componentPath = component != null
                        ? component.GetType().FullName
                        : componentId.ToString();
                    string value = component != null
                        ? CapabilityTraceStream.CaptureObjectFields(component)
                        : "missing";
                    entity.RemoveComponent(componentId);
                    CapabilityTraceStream.CommandFlushed(
                        capability, "RemoveComponent", entity, componentPath, value);
                }
            });
        }

        public void DestroyEntity(CEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            m_Context?.MarkWorked();
            CapabilityBase capability = m_Context?.Capability;
            Enqueue(new CommandEntry
            {
                Name = "DestroyEntity",
                Entity = entity,
                Path = "entity",
                Capability = capability,
                Execute = () =>
                {
                    if (m_World == null || entity.State != IEntity.EntityState.Running)
                    {
                        return;
                    }

                    string value = CaptureComponentList(entity);
                    m_World.RemoveChild(entity);
                    CapabilityTraceStream.CommandFlushed(
                        capability, "DestroyEntity", entity, "entity", value);
                }
            });
        }

        public void CreateEventEntity<TComponent>
            (Action<TComponent> configure = null, string name = null)
            where TComponent : CComponent, new()
        {
            m_Context?.MarkWorked();
            CapabilityBase capability = m_Context?.Capability;
            Enqueue(new CommandEntry
            {
                Name = "CreateEventEntity",
                Path = typeof(TComponent).FullName,
                Capability = capability,
                Execute = () =>
                {
                    if (m_World == null)
                    {
                        return;
                    }

                    CEntity entity = m_World.AddChild(name ?? typeof(TComponent).Name);
                    TComponent component = entity.AddComponent<TComponent>();
                    configure?.Invoke(component);
                    CapabilityTraceStream.CommandFlushed(
                        capability,
                        "CreateEventEntity",
                        entity,
                        typeof(TComponent).FullName,
                        CapabilityTraceStream.CaptureObjectFields(component));
                }
            });
        }

        public void RemoveEventEntities<TComponent>() where TComponent : CComponent
        {
            EntityGroup group = m_World?.GetGroup(
                EntityMatcher.SetAll(Component<TComponent>.TId));
            if (group?.EntitiesMap is not { Count: > 0 })
            {
                return; // 没有实体就不入队
            }

            m_Context?.MarkWorked();
            CapabilityBase capability = m_Context?.Capability;
            Enqueue(new CommandEntry
            {
                Name = "RemoveEventEntities",
                Path = typeof(TComponent).FullName,
                Capability = capability,
                Execute = () =>
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

                    m_EntityBuffer.Clear();
                    foreach (CEntity entity in group.EntitiesMap)
                    {
                        if (entity != null)
                        {
                            m_EntityBuffer.Add(entity);
                        }
                    }

                    string value = FormatEntityIds(m_EntityBuffer);
                    for (int i = 0; i < m_EntityBuffer.Count; i++)
                    {
                        m_World.RemoveChild(m_EntityBuffer[i]);
                    }

                    m_EntityBuffer.Clear();

                    CapabilityTraceStream.CommandFlushed(
                        capability,
                        "RemoveEventEntities",
                        null,
                        typeof(TComponent).FullName,
                        value);
                }
            });
        }

        private void Enqueue(CommandEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            m_Commands.Add(entry);
            CapabilityTraceStream.CommandQueued(
                entry.Capability, entry.Name, entry.Entity, entry.Path, entry.Value);
        }

        public void Flush()
        {
            for (int i = 0; i < m_Commands.Count; i++)
            {
                m_Commands[i]?.Execute?.Invoke();
            }

            m_Commands.Clear();
        }

        private static string CaptureComponentList(CEntity entity)
        {
            if (entity?.Components?.IndexList == null)
            {
                return string.Empty;
            }

            List<string> names = new List<string>(entity.Components.IndexList.Count);
            List<int> indices = entity.Components.IndexList;
            for (int i = 0; i < indices.Count; i++)
            {
                CComponent component = entity.GetComponent(indices[i]);
                if (component != null)
                {
                    names.Add(component.GetType().FullName);
                }
            }

            return string.Join(",", names);
        }

        private static string FormatEntityIds(List<CEntity> entities)
        {
            if (entities == null || entities.Count == 0)
            {
                return string.Empty;
            }

            List<string> ids = new List<string>(entities.Count);
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i] != null)
                {
                    ids.Add(entities[i].Id.ToString());
                }
            }

            return string.Join(",", ids);
        }
    }
}