using System.Collections.Generic;
using UnityEngine;

namespace Core.Capability.Editor
{
    /// <summary>
    ///     Inspector 当前选中项的类型。
    /// </summary>
    internal enum CapabilityDebugItemKind
    {
        None = 0,
        Component = 1,
        Capability = 2,
        Pipeline = 3
    }

    /// <summary>
    ///     一次 Play Mode 调试会话的完整内存记录。
    /// </summary>
    internal sealed class CapabilityDebugSession
    {
        private const int CheckpointInterval = 120;

        private readonly List<CapabilityDebugSparseFrame> m_Frames =
            new List<CapabilityDebugSparseFrame>(1024);

        // 增量日志索引：采样时追加，Inspector 直接 O(1) 查询。
        public readonly Dictionary<string, List<CapabilityDebugLogSnapshot>> LogIndex =
            new Dictionary<string, List<CapabilityDebugLogSnapshot>>(64);

        private CapabilityDebugFrame m_LatestFrame;
        private CapabilityDebugFrame m_CachedFrame;
        private int m_CachedFrameIndex = -1;

        public int CurrentFrameIndex { get; private set; } = -1;

        public int FrameCount => m_Frames.Count;

        public bool HasFrames => m_Frames.Count > 0;

        public bool IsAtLatestFrame =>
            CurrentFrameIndex < 0 || CurrentFrameIndex == m_Frames.Count - 1;

        public CapabilityDebugFrame CurrentFrame => GetFrame(CurrentFrameIndex);

        public void AddFrame(CapabilityDebugFrame frame)
        {
            if (frame == null)
            {
                return;
            }

            bool isCheckpoint = m_Frames.Count == 0 ||
                                m_Frames.Count % CheckpointInterval == 0;
            CapabilityDebugSparseFrame sparse = isCheckpoint
                ? CapabilityDebugSparseFrame.CreateCheckpoint(frame)
                : CapabilityDebugSparseFrame.CreateDelta(frame, m_LatestFrame);

            m_Frames.Add(sparse);
            CurrentFrameIndex = m_Frames.Count - 1;
            m_LatestFrame = CloneFrame(frame);
            m_CachedFrame = m_LatestFrame;
            m_CachedFrameIndex = CurrentFrameIndex;
        }

        public void SetCurrentFrameIndex(int index)
        {
            if (m_Frames.Count == 0)
            {
                CurrentFrameIndex = -1;
                return;
            }

            CurrentFrameIndex = Mathf.Clamp(index, 0, m_Frames.Count - 1);
        }

        public CapabilityDebugFrame GetFrame(int index)
        {
            if (index < 0 || index >= m_Frames.Count)
            {
                return null;
            }

            if (m_CachedFrameIndex == index && m_CachedFrame != null)
            {
                return m_CachedFrame;
            }

            int checkpointIndex = FindCheckpointIndex(index);
            CapabilityDebugFrame frame = CloneFrame(m_Frames[checkpointIndex].Checkpoint);
            for (int i = checkpointIndex + 1; i <= index; i++)
            {
                m_Frames[i].ApplyTo(frame);
            }

            m_CachedFrame = frame;
            m_CachedFrameIndex = index;
            return m_CachedFrame;
        }

        public void Clear()
        {
            m_Frames.Clear();
            LogIndex.Clear();
            CurrentFrameIndex = -1;
            m_LatestFrame = null;
            m_CachedFrame = null;
            m_CachedFrameIndex = -1;
        }

        private int FindCheckpointIndex(int index)
        {
            for (int i = index; i >= 0; i--)
            {
                if (m_Frames[i].IsCheckpoint)
                {
                    return i;
                }
            }

            return 0;
        }

        public static CapabilityDebugFrame CloneFrame(CapabilityDebugFrame source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = new CapabilityDebugFrame
            {
                FrameIndex = source.FrameIndex,
                UnityFrameCount = source.UnityFrameCount,
                RealtimeSinceStartup = source.RealtimeSinceStartup
            };

            for (int i = 0; i < source.Worlds.Count; i++)
            {
                clone.Worlds.Add(CloneWorld(source.Worlds[i]));
            }

            for (int i = 0; i < source.Traces.Count; i++)
            {
                clone.Traces.Add(CloneTrace(source.Traces[i]));
            }

            return clone;
        }

        private static CapabilityDebugWorldSnapshot CloneWorld
            (CapabilityDebugWorldSnapshot source)
        {
            var clone = new CapabilityDebugWorldSnapshot
            {
                Key = source.Key,
                WorldId = source.WorldId,
                WorldVersion = source.WorldVersion,
                DisplayName = source.DisplayName
            };

            for (int i = 0; i < source.Entities.Count; i++)
            {
                clone.Entities.Add(CloneEntity(source.Entities[i]));
            }

            for (int i = 0; i < source.GlobalCapabilities.Count; i++)
            {
                clone.GlobalCapabilities.Add(CloneCapability(source.GlobalCapabilities[i]));
            }

            return clone;
        }

        private static CapabilityDebugEntitySnapshot CloneEntity
            (CapabilityDebugEntitySnapshot source)
        {
            var clone = new CapabilityDebugEntitySnapshot
            {
                Key = source.Key,
                WorldKey = source.WorldKey,
                EntityId = source.EntityId,
                EntityVersion = source.EntityVersion,
                DisplayName = source.DisplayName
            };

            for (int i = 0; i < source.Components.Count; i++)
            {
                clone.Components.Add(CloneComponent(source.Components[i]));
            }

            for (int i = 0; i < source.Transforms.Count; i++)
            {
                clone.Transforms.Add(CloneTransform(source.Transforms[i]));
            }

            return clone;
        }

        private static CapabilityDebugComponentSnapshot CloneComponent
            (CapabilityDebugComponentSnapshot source)
        {
            var clone = new CapabilityDebugComponentSnapshot
            {
                Key = source.Key,
                ComponentId = source.ComponentId,
                TypeName = source.TypeName,
                TypeFullName = source.TypeFullName
            };

            for (int i = 0; i < source.Fields.Count; i++)
            {
                clone.Fields.Add(CloneValue(source.Fields[i]));
            }

            return clone;
        }

        private static CapabilityDebugCapabilitySnapshot CloneCapability
            (CapabilityDebugCapabilitySnapshot source)
        {
            var clone = new CapabilityDebugCapabilitySnapshot
            {
                Key = source.Key,
                CapabilityId = source.CapabilityId,
                TypeName = source.TypeName,
                TypeFullName = source.TypeFullName,
                UpdateMode = source.UpdateMode,
                TickGroupOrder = source.TickGroupOrder,
                StageName = source.StageName,
                State = source.State,
                Pipeline = source.Pipeline,
                DebugTag = source.DebugTag,
                LastErrorMessage = source.LastErrorMessage,
                LastTickMilliseconds = source.LastTickMilliseconds,
                MatchedEntityCount = source.MatchedEntityCount
            };

            clone.MatchedEntityIds.AddRange(source.MatchedEntityIds);
            for (int i = 0; i < source.Fields.Count; i++)
            {
                clone.Fields.Add(CloneValue(source.Fields[i]));
            }

            for (int i = 0; i < source.Logs.Count; i++)
            {
                clone.Logs.Add(CloneLog(source.Logs[i]));
            }

            return clone;
        }

        private static CapabilityDebugValueSnapshot CloneValue
            (CapabilityDebugValueSnapshot source)
        {
            var clone = new CapabilityDebugValueSnapshot
            {
                Name = source.Name,
                TypeName = source.TypeName,
                DisplayValue = source.DisplayValue
            };

            for (int i = 0; i < source.Children.Count; i++)
            {
                clone.Children.Add(CloneValue(source.Children[i]));
            }

            return clone;
        }

        private static CapabilityDebugLogSnapshot CloneLog(CapabilityDebugLogSnapshot source)
        {
            return new CapabilityDebugLogSnapshot
            {
                FrameIndex = source.FrameIndex,
                Time = source.Time,
                Message = source.Message
            };
        }

        private static CapabilityDebugTransformSnapshot CloneTransform
            (CapabilityDebugTransformSnapshot source)
        {
            return new CapabilityDebugTransformSnapshot
            {
                InstanceId = source.InstanceId,
                Path = source.Path,
                Transform = source.Transform,
                ActiveSelf = source.ActiveSelf,
                Position = source.Position,
                LocalPosition = source.LocalPosition,
                Rotation = source.Rotation,
                LocalRotation = source.LocalRotation,
                LocalScale = source.LocalScale
            };
        }

        private static CapabilityDebugTraceSnapshot CloneTrace
            (CapabilityDebugTraceSnapshot source)
        {
            return new CapabilityDebugTraceSnapshot
            {
                FrameIndex = source.FrameIndex,
                Time = source.Time,
                Event = source.Event,
                WorldId = source.WorldId,
                WorldName = source.WorldName,
                CapabilityId = source.CapabilityId,
                CapabilityType = source.CapabilityType,
                EntityId = source.EntityId,
                Path = source.Path,
                Value = source.Value,
                Prev = source.Prev
            };
        }

    }

    internal sealed class CapabilityDebugSparseFrame
    {
        public int FrameIndex;
        public int UnityFrameCount;
        public double RealtimeSinceStartup;
        public bool IsCheckpoint;
        public CapabilityDebugFrame Checkpoint;
        public readonly List<CapabilityDebugWorldDelta> WorldDeltas =
            new List<CapabilityDebugWorldDelta>(8);
        public readonly List<CapabilityDebugTraceSnapshot> Traces =
            new List<CapabilityDebugTraceSnapshot>(32);

        public static CapabilityDebugSparseFrame CreateCheckpoint(CapabilityDebugFrame frame)
        {
            return new CapabilityDebugSparseFrame
            {
                FrameIndex = frame.FrameIndex,
                UnityFrameCount = frame.UnityFrameCount,
                RealtimeSinceStartup = frame.RealtimeSinceStartup,
                IsCheckpoint = true,
                Checkpoint = CapabilityDebugSession.CloneFrame(frame)
            };
        }

        public static CapabilityDebugSparseFrame CreateDelta
        (
            CapabilityDebugFrame frame,
            CapabilityDebugFrame previous
        )
        {
            var sparse = new CapabilityDebugSparseFrame
            {
                FrameIndex = frame.FrameIndex,
                UnityFrameCount = frame.UnityFrameCount,
                RealtimeSinceStartup = frame.RealtimeSinceStartup
            };

            for (int i = 0; i < frame.Traces.Count; i++)
            {
                sparse.Traces.Add(CloneTraceLocal(frame.Traces[i]));
            }

            BuildWorldDeltas(frame, previous, sparse.WorldDeltas);
            return sparse;
        }

        public void ApplyTo(CapabilityDebugFrame frame)
        {
            frame.FrameIndex = FrameIndex;
            frame.UnityFrameCount = UnityFrameCount;
            frame.RealtimeSinceStartup = RealtimeSinceStartup;
            frame.Traces.Clear();
            for (int i = 0; i < Traces.Count; i++)
            {
                frame.Traces.Add(CloneTraceLocal(Traces[i]));
            }

            for (int i = 0; i < WorldDeltas.Count; i++)
            {
                ApplyWorldDelta(frame, WorldDeltas[i]);
            }
        }

        private static void BuildWorldDeltas
        (
            CapabilityDebugFrame frame,
            CapabilityDebugFrame previous,
            List<CapabilityDebugWorldDelta> destination
        )
        {
            for (int i = 0; i < frame.Worlds.Count; i++)
            {
                CapabilityDebugWorldSnapshot currentWorld = frame.Worlds[i];
                CapabilityDebugWorldSnapshot previousWorld =
                    previous?.FindWorld(currentWorld.Key);
                if (previousWorld == null)
                {
                    destination.Add(new CapabilityDebugWorldDelta
                    {
                        Key = currentWorld.Key,
                        FullWorld = CloneWorldLocal(currentWorld)
                    });
                    continue;
                }

                CapabilityDebugWorldDelta delta =
                    BuildExistingWorldDelta(currentWorld, previousWorld);
                if (delta.HasChanges)
                {
                    destination.Add(delta);
                }
            }

            if (previous == null)
            {
                return;
            }

            for (int i = 0; i < previous.Worlds.Count; i++)
            {
                CapabilityDebugWorldSnapshot previousWorld = previous.Worlds[i];
                if (frame.FindWorld(previousWorld.Key) != null)
                {
                    continue;
                }

                destination.Add(new CapabilityDebugWorldDelta
                {
                    Key = previousWorld.Key,
                    Removed = true
                });
            }
        }

        private static CapabilityDebugWorldDelta BuildExistingWorldDelta
        (
            CapabilityDebugWorldSnapshot current,
            CapabilityDebugWorldSnapshot previous
        )
        {
            var delta = new CapabilityDebugWorldDelta
            {
                Key = current.Key,
                WorldId = current.WorldId,
                WorldVersion = current.WorldVersion,
                DisplayName = current.DisplayName
            };

            for (int i = 0; i < current.Entities.Count; i++)
            {
                CapabilityDebugEntitySnapshot currentEntity = current.Entities[i];
                CapabilityDebugEntitySnapshot previousEntity =
                    previous.FindEntity(currentEntity.Key);
                if (previousEntity == null ||
                    EntitySignature(currentEntity) != EntitySignature(previousEntity))
                {
                    delta.Entities.Add(CloneEntityLocal(currentEntity));
                }
            }

            for (int i = 0; i < previous.Entities.Count; i++)
            {
                CapabilityDebugEntitySnapshot previousEntity = previous.Entities[i];
                if (current.FindEntity(previousEntity.Key) == null)
                {
                    delta.RemovedEntityKeys.Add(previousEntity.Key);
                }
            }

            for (int i = 0; i < current.GlobalCapabilities.Count; i++)
            {
                CapabilityDebugCapabilitySnapshot currentCapability =
                    current.GlobalCapabilities[i];
                CapabilityDebugCapabilitySnapshot previousCapability =
                    previous.FindGlobalCapability(currentCapability.Key);
                if (previousCapability == null ||
                    CapabilitySignature(currentCapability) !=
                    CapabilitySignature(previousCapability))
                {
                    delta.Capabilities.Add(CloneCapabilityLocal(currentCapability));
                }
            }

            for (int i = 0; i < previous.GlobalCapabilities.Count; i++)
            {
                CapabilityDebugCapabilitySnapshot previousCapability =
                    previous.GlobalCapabilities[i];
                if (current.FindGlobalCapability(previousCapability.Key) == null)
                {
                    delta.RemovedCapabilityKeys.Add(previousCapability.Key);
                }
            }

            return delta;
        }

        private static void ApplyWorldDelta
            (CapabilityDebugFrame frame, CapabilityDebugWorldDelta delta)
        {
            if (delta.Removed)
            {
                RemoveWorld(frame, delta.Key);
                return;
            }

            CapabilityDebugWorldSnapshot world = frame.FindWorld(delta.Key);
            if (world == null)
            {
                if (delta.FullWorld != null)
                {
                    frame.Worlds.Add(CloneWorldLocal(delta.FullWorld));
                    frame.Worlds.Sort((x, y) => x.WorldId.CompareTo(y.WorldId));
                }

                return;
            }

            world.WorldId = delta.WorldId;
            world.WorldVersion = delta.WorldVersion;
            world.DisplayName = delta.DisplayName;

            for (int i = 0; i < delta.RemovedEntityKeys.Count; i++)
            {
                RemoveEntity(world, delta.RemovedEntityKeys[i]);
            }

            for (int i = 0; i < delta.Entities.Count; i++)
            {
                ReplaceEntity(world, delta.Entities[i]);
            }

            for (int i = 0; i < delta.RemovedCapabilityKeys.Count; i++)
            {
                RemoveCapability(world, delta.RemovedCapabilityKeys[i]);
            }

            for (int i = 0; i < delta.Capabilities.Count; i++)
            {
                ReplaceCapability(world, delta.Capabilities[i]);
            }

            world.Entities.Sort((x, y) => x.EntityId.CompareTo(y.EntityId));
            world.GlobalCapabilities.Sort((x, y) =>
            {
                int byOrder = x.TickGroupOrder.CompareTo(y.TickGroupOrder);
                if (byOrder != 0)
                {
                    return byOrder;
                }

                int byMode = x.UpdateMode.CompareTo(y.UpdateMode);
                return byMode != 0
                    ? byMode
                    : string.CompareOrdinal(x.TypeName, y.TypeName);
            });
        }

        private static void RemoveWorld(CapabilityDebugFrame frame, string key)
        {
            for (int i = frame.Worlds.Count - 1; i >= 0; i--)
            {
                if (frame.Worlds[i].Key == key)
                {
                    frame.Worlds.RemoveAt(i);
                    return;
                }
            }
        }

        private static void RemoveEntity(CapabilityDebugWorldSnapshot world, string key)
        {
            for (int i = world.Entities.Count - 1; i >= 0; i--)
            {
                if (world.Entities[i].Key == key)
                {
                    world.Entities.RemoveAt(i);
                    return;
                }
            }
        }

        private static void ReplaceEntity
            (CapabilityDebugWorldSnapshot world, CapabilityDebugEntitySnapshot entity)
        {
            for (int i = 0; i < world.Entities.Count; i++)
            {
                if (world.Entities[i].Key == entity.Key)
                {
                    world.Entities[i] = CloneEntityLocal(entity);
                    return;
                }
            }

            world.Entities.Add(CloneEntityLocal(entity));
        }

        private static void RemoveCapability
            (CapabilityDebugWorldSnapshot world, string key)
        {
            for (int i = world.GlobalCapabilities.Count - 1; i >= 0; i--)
            {
                if (world.GlobalCapabilities[i].Key == key)
                {
                    world.GlobalCapabilities.RemoveAt(i);
                    return;
                }
            }
        }

        private static void ReplaceCapability
        (
            CapabilityDebugWorldSnapshot world,
            CapabilityDebugCapabilitySnapshot capability
        )
        {
            for (int i = 0; i < world.GlobalCapabilities.Count; i++)
            {
                if (world.GlobalCapabilities[i].Key == capability.Key)
                {
                    world.GlobalCapabilities[i] = CloneCapabilityLocal(capability);
                    return;
                }
            }

            world.GlobalCapabilities.Add(CloneCapabilityLocal(capability));
        }

        private static string EntitySignature(CapabilityDebugEntitySnapshot entity)
        {
            var parts = new List<string>(entity.Components.Count + entity.Transforms.Count + 4)
            {
                entity.EntityVersion.ToString(),
                entity.DisplayName ?? string.Empty
            };
            for (int i = 0; i < entity.Components.Count; i++)
            {
                parts.Add(ComponentSignature(entity.Components[i]));
            }

            for (int i = 0; i < entity.Transforms.Count; i++)
            {
                parts.Add(TransformSignature(entity.Transforms[i]));
            }

            return string.Join("|", parts);
        }

        private static string ComponentSignature(CapabilityDebugComponentSnapshot component)
        {
            var parts = new List<string>(component.Fields.Count + 3)
            {
                component.Key,
                component.TypeFullName
            };
            AddValueSignatures(component.Fields, parts);
            return string.Join("|", parts);
        }

        private static string CapabilitySignature
            (CapabilityDebugCapabilitySnapshot capability)
        {
            var parts = new List<string>(capability.Fields.Count + capability.Logs.Count + 12)
            {
                capability.State.ToString(),
                capability.LastErrorMessage ?? string.Empty,
                capability.LastTickMilliseconds.ToString("F4"),
                capability.MatchedEntityCount.ToString(),
                string.Join(",", capability.MatchedEntityIds),
                capability.Pipeline ?? string.Empty,
                capability.DebugTag ?? string.Empty
            };
            AddValueSignatures(capability.Fields, parts);
            for (int i = 0; i < capability.Logs.Count; i++)
            {
                parts.Add($"{capability.Logs[i].FrameIndex}:{capability.Logs[i].Message}");
            }

            return string.Join("|", parts);
        }

        private static void AddValueSignatures
            (List<CapabilityDebugValueSnapshot> values, List<string> parts)
        {
            for (int i = 0; i < values.Count; i++)
            {
                CapabilityDebugValueSnapshot value = values[i];
                parts.Add($"{value.Name}:{value.TypeName}:{value.DisplayValue}");
                AddValueSignatures(value.Children, parts);
            }
        }

        private static string TransformSignature(CapabilityDebugTransformSnapshot transform)
        {
            return $"{transform.InstanceId}:{transform.Path}:{transform.ActiveSelf}:" +
                   $"{transform.Position}:{transform.LocalPosition}:{transform.Rotation}:" +
                   $"{transform.LocalRotation}:{transform.LocalScale}";
        }

        private static CapabilityDebugWorldSnapshot CloneWorldLocal
            (CapabilityDebugWorldSnapshot source)
        {
            var frame = new CapabilityDebugFrame();
            frame.Worlds.Add(source);
            return CapabilityDebugSession.CloneFrame(frame).Worlds[0];
        }

        private static CapabilityDebugEntitySnapshot CloneEntityLocal
            (CapabilityDebugEntitySnapshot source)
        {
            var sourceWorld = new CapabilityDebugWorldSnapshot { Key = "clone" };
            sourceWorld.Entities.Add(source);
            CapabilityDebugWorldSnapshot world = CloneWorldLocal(sourceWorld);
            return world.Entities[0];
        }

        private static CapabilityDebugCapabilitySnapshot CloneCapabilityLocal
            (CapabilityDebugCapabilitySnapshot source)
        {
            var sourceWorld = new CapabilityDebugWorldSnapshot { Key = "clone" };
            sourceWorld.GlobalCapabilities.Add(source);
            CapabilityDebugWorldSnapshot world = CloneWorldLocal(sourceWorld);
            return world.GlobalCapabilities[0];
        }

        private static CapabilityDebugTraceSnapshot CloneTraceLocal
            (CapabilityDebugTraceSnapshot source)
        {
            return new CapabilityDebugTraceSnapshot
            {
                FrameIndex = source.FrameIndex,
                Time = source.Time,
                Event = source.Event,
                WorldId = source.WorldId,
                WorldName = source.WorldName,
                CapabilityId = source.CapabilityId,
                CapabilityType = source.CapabilityType,
                EntityId = source.EntityId,
                Path = source.Path,
                Value = source.Value,
                Prev = source.Prev
            };
        }
    }

    internal sealed class CapabilityDebugWorldDelta
    {
        public string Key;
        public int WorldId;
        public int WorldVersion;
        public string DisplayName;
        public bool Removed;
        public CapabilityDebugWorldSnapshot FullWorld;
        public readonly List<CapabilityDebugEntitySnapshot> Entities =
            new List<CapabilityDebugEntitySnapshot>(16);
        public readonly List<string> RemovedEntityKeys = new List<string>(8);
        public readonly List<CapabilityDebugCapabilitySnapshot> Capabilities =
            new List<CapabilityDebugCapabilitySnapshot>(16);
        public readonly List<string> RemovedCapabilityKeys = new List<string>(8);

        public bool HasChanges =>
            Removed ||
            FullWorld != null ||
            Entities.Count > 0 ||
            RemovedEntityKeys.Count > 0 ||
            Capabilities.Count > 0 ||
            RemovedCapabilityKeys.Count > 0;
    }

    /// <summary>
    ///     单个采样帧，保存所有 World 的可观察状态。
    /// </summary>
    internal sealed class CapabilityDebugFrame
    {
        public int FrameIndex;
        public int UnityFrameCount;
        public double RealtimeSinceStartup;
        public readonly List<CapabilityDebugWorldSnapshot> Worlds =
            new List<CapabilityDebugWorldSnapshot>(8);
        public readonly List<CapabilityDebugTraceSnapshot> Traces =
            new List<CapabilityDebugTraceSnapshot>(32);

        public CapabilityDebugEntitySnapshot FindEntity(string entityKey)
        {
            for (int i = 0; i < Worlds.Count; i++)
            {
                CapabilityDebugEntitySnapshot entity = Worlds[i].FindEntity(entityKey);
                if (entity != null)
                {
                    return entity;
                }
            }

            return null;
        }

        public CapabilityDebugWorldSnapshot FindWorld(string worldKey)
        {
            for (int i = 0; i < Worlds.Count; i++)
            {
                CapabilityDebugWorldSnapshot world = Worlds[i];
                if (world.Key == worldKey)
                {
                    return world;
                }
            }

            return null;
        }
    }

    /// <summary>
    ///     World 层级快照，用于组织 Entity 导航。
    /// </summary>
    internal sealed class CapabilityDebugWorldSnapshot
    {
        public string Key;
        public int WorldId;
        public int WorldVersion;
        public string DisplayName;
        public readonly List<CapabilityDebugEntitySnapshot> Entities =
            new List<CapabilityDebugEntitySnapshot>(128);
        public readonly List<CapabilityDebugCapabilitySnapshot> GlobalCapabilities =
            new List<CapabilityDebugCapabilitySnapshot>(64);

        public CapabilityDebugEntitySnapshot FindEntity(string entityKey)
        {
            for (int i = 0; i < Entities.Count; i++)
            {
                CapabilityDebugEntitySnapshot entity = Entities[i];
                if (entity.Key == entityKey)
                {
                    return entity;
                }
            }

            return null;
        }

        public CapabilityDebugCapabilitySnapshot FindGlobalCapability(string itemKey)
        {
            for (int i = 0; i < GlobalCapabilities.Count; i++)
            {
                CapabilityDebugCapabilitySnapshot capability = GlobalCapabilities[i];
                if (capability.Key == itemKey)
                {
                    return capability;
                }
            }

            return null;
        }
    }

    /// <summary>
    ///     Entity 层级快照，包含组件、能力和可回放的场景 Transform。
    /// </summary>
    internal sealed class CapabilityDebugEntitySnapshot
    {
        public string Key;
        public string WorldKey;
        public int EntityId;
        public int EntityVersion;
        public string DisplayName;
        public readonly List<CapabilityDebugComponentSnapshot> Components =
            new List<CapabilityDebugComponentSnapshot>(32);
        public readonly List<CapabilityDebugTransformSnapshot> Transforms =
            new List<CapabilityDebugTransformSnapshot>(8);

        public CapabilityDebugComponentSnapshot FindComponent(string itemKey)
        {
            for (int i = 0; i < Components.Count; i++)
            {
                CapabilityDebugComponentSnapshot component = Components[i];
                if (component.Key == itemKey)
                {
                    return component;
                }
            }

            return null;
        }
    }

    /// <summary>
    ///     CComponent 字段快照，记录该帧组件的全部实例字段。
    /// </summary>
    internal sealed class CapabilityDebugComponentSnapshot
    {
        public string Key;
        public int ComponentId;
        public string TypeName;
        public string TypeFullName;
        public readonly List<CapabilityDebugValueSnapshot> Fields =
            new List<CapabilityDebugValueSnapshot>(16);
    }

    /// <summary>
    ///     Capability 字段与运行状态快照，只记录被 Attribute 标记的本地字段。
    /// </summary>
    internal sealed class CapabilityDebugCapabilitySnapshot
    {
        public string Key;
        public int CapabilityId;
        public string TypeName;
        public string TypeFullName;
        public CapabilityUpdateMode UpdateMode;
        public int TickGroupOrder;
        public string StageName;
        public CapabilityRuntimeState State;
        public string Pipeline;
        public string DebugTag;
        public string LastErrorMessage;
        public double LastTickMilliseconds;
        public int MatchedEntityCount;
        public readonly List<int> MatchedEntityIds = new List<int>(32);
        public readonly List<CapabilityDebugValueSnapshot> Fields =
            new List<CapabilityDebugValueSnapshot>(16);
        public readonly List<CapabilityDebugLogSnapshot> Logs =
            new List<CapabilityDebugLogSnapshot>(4);
    }

    /// <summary>
    ///     Inspector 字段展示节点，支持少量嵌套子节点。
    /// </summary>
    internal sealed class CapabilityDebugValueSnapshot
    {
        public string Name;
        public string TypeName;
        public string DisplayValue;
        public readonly List<CapabilityDebugValueSnapshot> Children =
            new List<CapabilityDebugValueSnapshot>(8);
    }

    /// <summary>
    ///     Capability 在某个采样帧产生的调试日志。
    /// </summary>
    internal sealed class CapabilityDebugLogSnapshot
    {
        public int FrameIndex;
        public double Time;
        public string Message;
    }

    internal sealed class CapabilityDebugTraceSnapshot
    {
        public int FrameIndex;
        public double Time;
        public string Event;
        public int WorldId = -1;
        public string WorldName;
        public int CapabilityId = -1;
        public string CapabilityType;
        public int EntityId = -1;
        public string Pipeline;
        public string Path;
        public string Value;
        public string Prev;
    }

    /// <summary>
    ///     Entity 绑定场景对象的 Transform 状态，用于 Timeline 回放。
    /// </summary>
    internal sealed class CapabilityDebugTransformSnapshot
    {
        public int InstanceId;
        public string Path;
        public Transform Transform;
        public bool ActiveSelf;
        public Vector3 Position;
        public Vector3 LocalPosition;
        public Quaternion Rotation;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;
    }
}
