#region

using System.Collections.Generic;
using Common.Event;
using Core.Capability;
using GamePlay.Map;
using GamePlay.Util;
using GamePlay.World;
using UnityEngine;
using Grid = GamePlay.Map.Grid;

#endregion

namespace GamePlay.Strategy
{
    // 单位沿 hex 路径移动的执行能力。只推进逻辑格和 Transform 位移，不负责路径生成。
    public class CpMoveAlongHexPath : CapabilityBase
    {
        private static readonly int m_TargetId = Component<UnitMoveTarget>.TId;
        private const float VisualMoveDuration = 0.15f;

        private readonly List<CEntity> m_Entities = new List<CEntity>(128);

        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.ResolveUnitMovement;

        public override string Pipeline => CapabilityPipeline.MoveCommand;

        public override void Tick
            (CapabilityContext context, float deltaTime, float realElapsedSeconds)
        {
            if (deltaTime <= 0f) return;
            if (!TryResolveMapContext(context.World, out Grid grid, out DrawMap drawMap,
                    out UnitOccupancyIndex occupancyIndex,
                    out NationIndex nationIndex, out DiplomacyIndex diplomacyIndex)) return;

            context.QuerySnapshot<Unit, UnitPosition, UnitMotor, UnitMoveTarget>(m_Entities);
            for (int i = 0; i < m_Entities.Count; i++)
            {
                TickOne(context, m_Entities[i], deltaTime, grid, drawMap, occupancyIndex,
                    nationIndex, diplomacyIndex);
            }
        }

        private void TickOne
        (
            CapabilityContext context, CEntity entity, float deltaTime, Grid grid,
            DrawMap drawMap, UnitOccupancyIndex occupancyIndex, NationIndex nationIndex,
            DiplomacyIndex diplomacyIndex
        )
        {
            if (!entity.TryGetUnit(out Unit unit)) return;
            if (!entity.TryGetUnitPosition(out UnitPosition position)) return;
            if (!entity.TryGetUnitMotor(out UnitMotor motor)) return;
            if (!entity.TryGetUnitMoveTarget(out UnitMoveTarget target)) return;
            if (motor.Transform == null) return;
            if (target.Path == null) return;
            if (target.Path.Length < 2) return;

            target.StepTimer += deltaTime;
            float timePerHex = 1f / Mathf.Max(0.01f, unit.MoveSpeed);

            while (target.StepTimer >= timePerHex && target.NextPathIndex < target.Path.Length)
            {
                if (TryEngageCombatIfHostile(context, entity, target.Path[target.NextPathIndex],
                        grid, occupancyIndex, nationIndex, diplomacyIndex, target))
                {
                    target.StepTimer = 0f;
                    context.MarkWorked();
                    break;
                }

                Vector3 oldWorldPos = HexMapUtility.GetNearestMirroredWorldPosition(
                    drawMap.Tilemap, grid, position.Hex, motor.Transform.position);

                EnterPathHex(entity, target, position, occupancyIndex, grid,
                    target.NextPathIndex);
                target.NextPathIndex++;
                target.StepTimer -= timePerHex;

                Vector3 newWorldPos = HexMapUtility.GetNearestMirroredWorldPosition(
                    drawMap.Tilemap, grid, position.Hex, oldWorldPos);

                target.VisualLerpStart = oldWorldPos;
                target.VisualLerpTarget = newWorldPos;
                target.VisualLerpProgress = 0f;
                context.MarkWorked();
            }

            if (target.VisualLerpProgress < 1f)
            {
                target.VisualLerpProgress += deltaTime / VisualMoveDuration;
                if (target.VisualLerpProgress >= 1f)
                {
                    motor.Transform.position = target.VisualLerpTarget;
                }
                else
                {
                    motor.Transform.position =
                        Vector3.Lerp(target.VisualLerpStart, target.VisualLerpTarget,
                            target.VisualLerpProgress);
                }

                context.MarkWorked();
            }

            bool logicDone = target.NextPathIndex >= target.Path.Length;
            if (!logicDone)
            {
                return;
            }

            DestroyPathIndicator(target);
            if (target.VisualLerpProgress >= 1f)
            {
                context.Commands.RemoveComponent(entity, m_TargetId);
            }
        }

        // 进入路径上的下一格：更新占位索引和 UnitPosition。
        private static void EnterPathHex
        (
            CEntity entity, UnitMoveTarget target, UnitPosition position,
            UnitOccupancyIndex occupancyIndex, Grid grid, int pathIndex
        )
        {
            if (pathIndex < 0) return;
            if (pathIndex >= target.Path.Length) return;
            if (!HexMapUtility.TryNormalizeHex(grid, target.Path[pathIndex],
                    out HexCoordinates nextHex)) return;

            if (nextHex.Equals(position.Hex))
            {
                return;
            }

            occupancyIndex?.Remove(position.Hex, entity.Id);
            occupancyIndex?.Set(nextHex, entity.Id);
            position.Hex = nextHex;

            Vector2Int offset = nextHex.ToOffset();
            position.Cell = new Vector3Int(offset.x, offset.y, 0);
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

        private static bool TryResolveMapContext
        (
            CapabilityWorld world, out Grid grid, out DrawMap drawMap,
            out UnitOccupancyIndex occupancyIndex, out NationIndex nationIndex,
            out DiplomacyIndex diplomacyIndex
        )
        {
            grid = null;
            drawMap = null;
            occupancyIndex = null;
            nationIndex = null;
            diplomacyIndex = null;

            if (world is not GameWorld gameWorld) return false;
            if (!gameWorld.TryGetPrimaryMapEntity(out CEntity mapEntity)) return false;
            if (!mapEntity.TryGetGrid(out grid)) return false;
            if (!mapEntity.TryGetDrawMap(out drawMap)) return false;
            if (!mapEntity.TryGetUnitOccupancyIndex(out occupancyIndex)) return false;
            if (!mapEntity.TryGetNationIndex(out nationIndex)) return false;
            if (!mapEntity.TryGetDiplomacyIndex(out diplomacyIndex)) return false;
            if (drawMap.Tilemap == null) return false;
            return true;
        }

        // 检查目标格是否有敌方单位，如果是则触发战斗并清理移动状态。
        private bool TryEngageCombatIfHostile
        (
            CapabilityContext context, CEntity entity, HexCoordinates targetHex, Grid grid,
            UnitOccupancyIndex occupancyIndex, NationIndex nationIndex,
            DiplomacyIndex diplomacyIndex, UnitMoveTarget target
        )
        {
            if (!HexMapUtility.TryNormalizeHex(grid, targetHex, out HexCoordinates normalizedHex))
                return false;
            if (!occupancyIndex.TryGetUnit(normalizedHex, out int occupantEntityId))
                return false;
            if (occupantEntityId == entity.Id)
                return false;

            if (!context.TryGetEntity(occupantEntityId, out CEntity occupantEntity))
                return false;
            if (!occupantEntity.TryGetUnit(out Unit occupantUnit)) return false;
            if (!entity.TryGetUnit(out Unit myUnit)) return false;
            if (!entity.TryGetUnitCombat(out UnitCombat myCombat)) return false;
            if (!occupantEntity.TryGetUnitCombat(out _)) return false;

            byte myId = NationUtility.GetIdOrDefault(nationIndex, myUnit.Tag);
            byte otherId = NationUtility.GetIdOrDefault(nationIndex, occupantUnit.Tag);
            if (!diplomacyIndex.IsHostile(myId, otherId))
                return false;

            if (occupantEntity.HasCombatState()) return false;

            context.Commands.AddComponent<CombatState>(entity, combatState =>
            {
                combatState.OpponentEntityId = occupantEntityId;
            });
            context.Commands.AddComponent<CombatState>(occupantEntity, combatState =>
            {
                combatState.OpponentEntityId = entity.Id;
            });

            DestroyPathIndicator(target);
            context.Commands.RemoveComponent(entity, m_TargetId);
            context.Commands.RemoveComponent(occupantEntity, m_TargetId);
            return true;
        }
    }
}
