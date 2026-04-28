using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Core.Capability.Editor
{
    /// <summary>
    ///     从运行中的 CapabilityWorldRegistry 采集一帧 Temporal Debug 数据。
    /// </summary>
    internal sealed class CapabilityDebugSampler
    {
        private readonly CapabilityDebugReflection m_Reflection = new CapabilityDebugReflection();
        private readonly CapabilityDebugStageResolver m_StageResolver =
            new CapabilityDebugStageResolver();
        private readonly List<CapabilityBase> m_UpdateCapabilities =
            new List<CapabilityBase>(64);
        private readonly List<CapabilityBase> m_FixedUpdateCapabilities =
            new List<CapabilityBase>(64);
        private int m_CurrentFrameIndex;

        public CapabilityDebugFrame Sample(int frameIndex)
        {
            m_CurrentFrameIndex = frameIndex;

#if UNITY_EDITOR
            // 每帧构建 Installer → Entity 映射，优先用 Installer 定位场景 Transform。
            Dictionary<CEntity, Transform> entityTransformMap =
                CapabilityDebugSceneState.BuildEntityTransformMap();
#else
            Dictionary<CEntity, Transform> entityTransformMap = null;
#endif

            CapabilityDebugFrame frame = new CapabilityDebugFrame
            {
                FrameIndex = frameIndex,
                UnityFrameCount = Time.frameCount,
                RealtimeSinceStartup = Time.realtimeSinceStartup
            };

            IReadOnlyList<CapabilityWorldBase> worlds = CapabilityWorldRegistry.Worlds;
            for (int i = 0; i < worlds.Count; i++)
            {
                if (worlds[i] is not CapabilityWorld world)
                {
                    continue;
                }

                if (!world.IsActive)
                {
                    continue;
                }

                CapabilityDebugWorldSnapshot worldSnapshot =
                    SampleWorld(world, entityTransformMap);
                if (worldSnapshot.Entities.Count > 0)
                {
                    frame.Worlds.Add(worldSnapshot);
                }
            }

            return frame;
        }

        private CapabilityDebugWorldSnapshot SampleWorld
        (
            CapabilityWorld world,
            Dictionary<CEntity, Transform> entityTransformMap
        )
        {
            CapabilityDebugWorldSnapshot snapshot = new CapabilityDebugWorldSnapshot
            {
                Key = BuildWorldKey(world),
                WorldId = world.Id,
                WorldVersion = world.Version,
                DisplayName = GetWorldDisplayName(world)
            };

            if (world.Children == null)
            {
                return snapshot;
            }

            foreach (CEntity entity in world.Children)
            {
                if (entity == null || !entity.IsActive)
                {
                    continue;
                }

                snapshot.Entities.Add(
                    SampleEntity(world, snapshot.Key, entity, entityTransformMap));
            }

            snapshot.Entities.Sort((x, y) => x.EntityId.CompareTo(y.EntityId));
            return snapshot;
        }

        private CapabilityDebugEntitySnapshot SampleEntity
        (
            CapabilityWorld world, string worldKey, CEntity entity,
            Dictionary<CEntity, Transform> entityTransformMap
        )
        {
            CapabilityDebugEntitySnapshot snapshot = new CapabilityDebugEntitySnapshot
            {
                Key = BuildEntityKey(world, entity),
                WorldKey = worldKey,
                EntityId = entity.Id,
                EntityVersion = entity.Version,
                DisplayName = GetEntityDisplayName(entity)
            };

            SampleComponents(entity, snapshot);
            SampleCapabilities(world, entity, snapshot);

            // 主路径：通过 Installer 映射获取该 Entity 的场景 Transform。
#if UNITY_EDITOR
            if (entityTransformMap != null &&
                entityTransformMap.TryGetValue(entity, out Transform installerTransform))
            {
                CapabilityDebugSceneState.CaptureSingle(
                    installerTransform, snapshot.Transforms);
            }
            else
#endif
            {
                // 回退路径：反射扫描组件字段查找 Transform/GameObject 引用。
                CollectFallbackSceneReferences(entity, snapshot);
            }

            return snapshot;
        }

        private void SampleComponents(CEntity entity, CapabilityDebugEntitySnapshot snapshot)
        {
            if (entity.Components?.IndexList == null)
            {
                return;
            }

            List<int> indices = entity.Components.IndexList;
            for (int i = 0; i < indices.Count; i++)
            {
                int componentId = indices[i];
                CComponent component = entity.GetComponent(componentId);
                if (component == null)
                {
                    continue;
                }

                CapabilityDebugComponentSnapshot componentSnapshot =
                    new CapabilityDebugComponentSnapshot
                    {
                        Key = BuildComponentKey(componentId, component),
                        ComponentId = componentId,
                        TypeName = component.GetType().Name,
                        TypeFullName = component.GetType().FullName
                    };

                m_Reflection.CaptureComponentFields(component, componentSnapshot.Fields);
                snapshot.Components.Add(componentSnapshot);
            }

            snapshot.Components.Sort((x, y) => string.CompareOrdinal(x.TypeName, y.TypeName));
        }

        /// <summary>
        ///     回退路径：反射扫描组件字段收集场景 Transform 引用。
        /// </summary>
        private void CollectFallbackSceneReferences
        (
            CEntity entity,
            CapabilityDebugEntitySnapshot snapshot
        )
        {
            if (entity.Components?.IndexList == null)
            {
                return;
            }

            Dictionary<int, Transform> buffer = new Dictionary<int, Transform>(16);
            List<int> indices = entity.Components.IndexList;
            for (int i = 0; i < indices.Count; i++)
            {
                CComponent component = entity.GetComponent(indices[i]);
                if (component == null)
                {
                    continue;
                }

                m_Reflection.CollectSceneReferences(component, buffer);
            }

            CapabilityDebugSceneState.Capture(buffer, snapshot.Transforms);
        }

        private void SampleCapabilities
            (CapabilityWorld world, CEntity entity, CapabilityDebugEntitySnapshot snapshot)
        {
            m_UpdateCapabilities.Clear();
            m_FixedUpdateCapabilities.Clear();
            world.GetCapabilities(entity, m_UpdateCapabilities, m_FixedUpdateCapabilities);
            SampleCapabilityList(world, entity, m_UpdateCapabilities, snapshot);
            SampleCapabilityList(world, entity, m_FixedUpdateCapabilities, snapshot);
            snapshot.Capabilities.Sort(CompareCapability);
        }

        private void SampleCapabilityList
        (
            CapabilityWorld world, CEntity entity, List<CapabilityBase> capabilities,
            CapabilityDebugEntitySnapshot snapshot
        )
        {
            for (int i = 0; i < capabilities.Count; i++)
            {
                CapabilityBase capability = capabilities[i];
                if (capability == null)
                {
                    continue;
                }

                CapabilityDebugCapabilitySnapshot capabilitySnapshot =
                    new CapabilityDebugCapabilitySnapshot
                    {
                        Key = BuildCapabilityKey(capability),
                        CapabilityId = capability.Id,
                        TypeName = capability.GetType().Name,
                        TypeFullName = capability.GetType().FullName,
                        UpdateMode = capability.UpdateMode,
                        TickGroupOrder = capability.TickGroupOrder,
                        StageName = m_StageResolver.Resolve(capability.TickGroupOrder),
                        State = ResolveCapabilityState(world, entity, capability)
                    };

                m_Reflection.CaptureCapabilityFields(capability, capabilitySnapshot.Fields);
                ConsumeLogs(capability, capabilitySnapshot);
                snapshot.Capabilities.Add(capabilitySnapshot);
            }
        }

        private void ConsumeLogs
        (
            CapabilityBase capability, CapabilityDebugCapabilitySnapshot capabilitySnapshot
        )
        {
#if UNITY_EDITOR
            List<CapabilityDebugLogBridge.Entry> entries =
                CapabilityDebugLogBridge.Consume(capability);
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                CapabilityDebugLogBridge.Entry entry = entries[i];
                capabilitySnapshot.Logs.Add(new CapabilityDebugLogSnapshot
                {
                    FrameIndex = m_CurrentFrameIndex,
                    Time = entry.Time,
                    Message = entry.Message
                });
            }
#endif
        }

        private static int CompareCapability
            (CapabilityDebugCapabilitySnapshot left, CapabilityDebugCapabilitySnapshot right)
        {
            int byOrder = left.TickGroupOrder.CompareTo(right.TickGroupOrder);
            if (byOrder != 0)
            {
                return byOrder;
            }

            int byMode = left.UpdateMode.CompareTo(right.UpdateMode);
            if (byMode != 0)
            {
                return byMode;
            }

            return string.CompareOrdinal(left.TypeName, right.TypeName);
        }

        private static CapabilityRuntimeState ResolveCapabilityState
            (CapabilityWorld world, CEntity entity, CapabilityBase capability)
        {
            if (capability.TagList != null &&
                world.IsCapabilityBlocked(entity, capability.TagList))
            {
                return CapabilityRuntimeState.Blocked;
            }

            return capability.IsActive
                ? CapabilityRuntimeState.Active
                : CapabilityRuntimeState.Inactive;
        }

        private static string BuildWorldKey(CapabilityWorld world)
        {
            return $"{world.GetType().FullName}:{world.Id}:{RuntimeHelpers.GetHashCode(world)}";
        }

        private static string BuildEntityKey(CapabilityWorld world, CEntity entity)
        {
            return $"{BuildWorldKey(world)}:{entity.Id}:{RuntimeHelpers.GetHashCode(entity)}";
        }

        private static string BuildComponentKey(int componentId, CComponent component)
        {
            return $"{componentId}:{component.GetType().FullName}";
        }

        private static string BuildCapabilityKey(CapabilityBase capability)
        {
            return $"{capability.UpdateMode}:{capability.Id}:{capability.GetType().FullName}";
        }

        private static string GetWorldDisplayName(CapabilityWorld world)
        {
            string worldName = string.IsNullOrWhiteSpace(world.Name)
                ? world.GetType().Name
                : world.Name;
            return $"{worldName} [{world.GetType().Name}]";
        }

        private static string GetEntityDisplayName(CEntity entity)
        {
            string entityName = string.IsNullOrWhiteSpace(entity.Name)
                ? entity.GetType().Name
                : entity.Name;
            return $"{entityName} - {entity.Id}";
        }
    }
}
