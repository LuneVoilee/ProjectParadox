#region

using System.Collections.Generic;
using Common.Event;
using Core.Capability;
using GamePlay.Map;
using GamePlay.Util;
using GamePlay.World;
using UnityEngine;
using UnityEngine.Tilemaps;
using Grid = GamePlay.Map.Grid;

#endregion

namespace GamePlay.Strategy
{
    // 路径表现同步：根据 UnitMoveTarget 创建和更新路径线，规则移动不关心表现细节。
    public class CpPathPresentation : CapabilityBase
    {
        private readonly List<Vector3> m_WorldPathBuffer = new List<Vector3>(64);
        private readonly List<CEntity> m_Entities = new List<CEntity>(128);

        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.PresentationPath;

        public override string Pipeline => CapabilityPipeline.PathUI;

        public override void Tick
            (CapabilityContext context, float deltaTime, float realElapsedSeconds)
        {
            if (!TryResolveMapContext(context, out Grid grid, out DrawMap drawMap))
            {
                return;
            }

            context.QuerySnapshot<UnitPosition, UnitMotor, UnitMoveTarget>(m_Entities);
            for (int i = 0; i < m_Entities.Count; i++)
            {
                SyncOne(context, m_Entities[i], grid, drawMap.Tilemap);
            }
        }

        public override void Dispose()
        {
            if (World == null)
            {
                base.Dispose();
                return;
            }

            EntityGroup group = World.GetGroup(
                EntityMatcher.SetAll(Component<UnitMoveTarget>.TId));
            if (group?.EntitiesMap == null)
            {
                base.Dispose();
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
                if (!buffer[i].TryGetUnitMoveTarget(out UnitMoveTarget target))
                    continue;
                DestroyPathIndicator(target);
            }

            base.Dispose();
        }

        private void SyncOne(CapabilityContext context, CEntity entity, Grid grid, Tilemap tilemap)
        {
            if (entity == null) return;
            if (!entity.TryGetUnitMotor(out UnitMotor motor)) return;
            if (!entity.TryGetUnitMoveTarget(out UnitMoveTarget target)) return;
            if (motor.Transform == null) return;
            if (target.Path == null) return;
            if (target.Path.Length < 2) return;

            BuildRemainingWorldPath(target, grid, tilemap, motor.Transform.position);
            if (m_WorldPathBuffer.Count < 2)
            {
                DestroyPathIndicator(target);
                return;
            }

            if (target.PathIndicatorId < 0)
            {
                target.PathIndicatorId =
                    EventBus.GP_OnCreatePathIndicator?.Invoke(m_WorldPathBuffer) ?? -1;
                context.MarkWorked();
                return;
            }

            EventBus.GP_OnUpdatePathIndicator?.Invoke(target.PathIndicatorId,
                m_WorldPathBuffer);
            context.MarkWorked();
        }

        private static bool TryResolveMapContext
        (
            CapabilityContext context, out Grid grid, out DrawMap drawMap
        )
        {
            grid = null;
            drawMap = null;

            if (context.World is not GameWorld gameWorld) return false;
            if (!gameWorld.TryGetPrimaryMapEntity(out CEntity mapEntity)) return false;
            if (!mapEntity.TryGetGrid(out grid)) return false;
            if (!mapEntity.TryGetDrawMap(out drawMap)) return false;
            return drawMap.Tilemap != null;
        }

        private void BuildRemainingWorldPath
        (
            UnitMoveTarget target, Grid grid, Tilemap tilemap,
            Vector3 unitWorldPosition
        )
        {
            m_WorldPathBuffer.Clear();
            m_WorldPathBuffer.Add(unitWorldPosition);

            Vector3 previous = unitWorldPosition;
            for (int i = Mathf.Max(1, target.NextPathIndex); i < target.Path.Length; i++)
            {
                Vector3 next = HexMapUtility.GetNearestMirroredWorldPosition(
                    tilemap, grid, target.Path[i], previous);
                m_WorldPathBuffer.Add(next);
                previous = next;
            }
        }

        private static void DestroyPathIndicator(UnitMoveTarget target)
        {
            if (target == null || target.PathIndicatorId < 0)
            {
                return;
            }

            EventBus.GP_OnDestroyPathIndicator?.Invoke(target.PathIndicatorId);
            target.PathIndicatorId = -1;
        }
    }
}
