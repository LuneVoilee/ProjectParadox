#region

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
    // 单位沿 hex 路径移动的执行能力。只改 Transform 位移，不处理动画。
    public class MoveAlongHexPathCap : CapabilityBase
    {
        private static readonly int m_UnitId = Component<Unit>.TId;
        private static readonly int m_PositionId = Component<UnitPosition>.TId;
        private static readonly int m_MotorId = Component<UnitMotor>.TId;
        private static readonly int m_TargetId = Component<UnitMoveTarget>.TId;

        // HOI4 式步进移动：时间累积到 timePerHex 后推进一格，表现层独立插值。
        private const float VisualMoveDuration = 0.15f;

        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.ResolveUnitMovement;

        protected override void OnInit()
        {
            Filter(m_UnitId, m_PositionId, m_MotorId, m_TargetId);
        }

        public override bool ShouldActivate()
        {
            // 四个组件全部就位才激活移动：Unit 提供速度，Position 提供当前格，
            // Motor 提供 Transform，MoveTarget 提供路径指令。
            if (!Owner.HasComponent(m_UnitId)) return false;
            if (!Owner.HasComponent(m_PositionId)) return false;
            if (!Owner.HasComponent(m_MotorId)) return false;
            if (!Owner.HasComponent(m_TargetId)) return false;
            return true;
        }

        public override bool ShouldDeactivate()
        {
            return !ShouldActivate();
        }

        protected override void OnDeactivated()
        {
            if (Owner != null &&
                Owner.TryGetUnitMoveTarget(out UnitMoveTarget target))
            {
                DestroyPathIndicator(target);
            }
        }

        public override void Dispose()
        {
            if (IsGlobal)
            {
                DestroyAllActivePathIndicators();
                base.Dispose();
                return;
            }

            if (Owner != null && Owner.TryGetUnitMoveTarget(out UnitMoveTarget target))
            {
                DestroyPathIndicator(target);
            }

            base.Dispose();
        }

        public override void TickActive(float deltaTime, float realElapsedSeconds)
        {
            if (deltaTime <= 0f) return;
            if (!Owner.TryGetUnit(out Unit unit)) return;
            if (!Owner.TryGetUnitPosition(out UnitPosition position)) return;
            if (!Owner.TryGetUnitMotor(out UnitMotor motor)) return;
            if (!Owner.TryGetUnitMoveTarget(out UnitMoveTarget target)) return;
            if (motor.Transform == null) return;
            if (target.Path == null) return;
            if (target.Path.Length < 2) return;
            if (!TryResolveMapContext(out Grid grid, out DrawMap drawMap,
                    out UnitOccupancyIndex occupancyIndex,
                    out NationIndex nationIndex, out DiplomacyIndex diplomacyIndex)) return;

            // 逻辑阶段：时间累积到 timePerHex 则推进到路径下一格。
            target.StepTimer += deltaTime;
            float timePerHex = 1f / Mathf.Max(0.01f, unit.MoveSpeed);

            while (target.StepTimer >= timePerHex && target.NextPathIndex < target.Path.Length)
            {
                // 检查即将进入的格子是否有敌方单位 → 触发战斗，停止移动。
                if (TryEngageCombatIfHostile(target.Path[target.NextPathIndex], grid,
                        occupancyIndex, nationIndex, diplomacyIndex, target))
                {
                    target.StepTimer = 0f;
                    break;
                }

                Vector3 oldWorldPos = HexMapUtility.GetNearestMirroredWorldPosition(
                    drawMap.Tilemap, grid, position.Hex, motor.Transform.position);

                EnterPathHex(target, position, occupancyIndex, grid, target.NextPathIndex);
                target.NextPathIndex++;
                target.StepTimer -= timePerHex;

                Vector3 newWorldPos = HexMapUtility.GetNearestMirroredWorldPosition(
                    drawMap.Tilemap, grid, position.Hex, oldWorldPos);

                target.VisualLerpStart = oldWorldPos;
                target.VisualLerpTarget = newWorldPos;
                target.VisualLerpProgress = 0f;
            }

            // 表现阶段：推进视觉插值（在逻辑阶段之后，确保最后一格的插值也能拿到 deltaTime）。
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
            }

            bool logicDone = target.NextPathIndex >= target.Path.Length;
            if (logicDone)
            {
                DestroyPathIndicator(target);
                if (target.VisualLerpProgress >= 1f)
                {
                    Owner.RemoveComponent(m_TargetId);
                }

                return;
            }
        }

        // 进入路径上的下一格：更新占位索引、写入 Position、提交占领请求 Hex。
        private void EnterPathHex
        (
            UnitMoveTarget target, UnitPosition position, UnitOccupancyIndex occupancyIndex,
            Grid grid, int pathIndex
        )
        {
            if (pathIndex < 0) return;
            if (pathIndex >= target.Path.Length) return;
            if (!HexMapUtility.TryNormalizeHex(grid, target.Path[pathIndex],
                    out HexCoordinates nextHex)) return;

            if (!nextHex.Equals(position.Hex))
            {
                occupancyIndex?.Remove(position.Hex, Owner.Id);
                occupancyIndex?.Set(nextHex, Owner.Id);
                position.Hex = nextHex;

                Vector2Int offset = nextHex.ToOffset();
                position.Cell = new Vector3Int(offset.x, offset.y, 0);
            }
        }

        private void DestroyPathIndicator(UnitMoveTarget target)
        {
            if (target == null || target.PathIndicatorId < 0)
            {
                return;
            }

            EventBus.GP_OnDestroyPathIndicator?.Invoke(target.PathIndicatorId);
            target.PathIndicatorId = -1;
        }

        private void DestroyAllActivePathIndicators()
        {
            if (World == null)
            {
                return;
            }

            foreach (int entityId in GlobalActiveEntityIds)
            {
                CEntity entity = World.GetChild(entityId);
                if (entity == null)
                {
                    continue;
                }

                if (!entity.TryGetUnitMoveTarget(out UnitMoveTarget target))
                {
                    continue;
                }

                DestroyPathIndicator(target);
            }
        }

        private bool TryResolveMapContext
        (
            out Grid grid, out DrawMap drawMap, out UnitOccupancyIndex occupancyIndex,
            out NationIndex nationIndex, out DiplomacyIndex diplomacyIndex
        )
        {
            grid = null;
            drawMap = null;
            occupancyIndex = null;
            nationIndex = null;
            diplomacyIndex = null;

            // 通过 GameWorld 拿到主地图实体，再逐项解析 Grid / DrawMap / 占位索引 / 外交索引。
            if (World is not GameWorld gameWorld) return false;
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
        // 在 MoveAlongHexPathCap 的步进循环中调用，确保战斗触发时单位恰好站在目标格前一格。
        private bool TryEngageCombatIfHostile
        (
            HexCoordinates targetHex, Grid grid,
            UnitOccupancyIndex occupancyIndex, NationIndex nationIndex,
            DiplomacyIndex diplomacyIndex, UnitMoveTarget target
        )
        {
            if (!HexMapUtility.TryNormalizeHex(grid, targetHex, out HexCoordinates normalizedHex))
                return false;
            if (!occupancyIndex.TryGetUnit(normalizedHex, out int occupantEntityId))
                return false;
            if (occupantEntityId == Owner.Id)
                return false;

            if (World is not GameWorld gameWorld) return false;
            CEntity occupantEntity = gameWorld.GetChild(occupantEntityId);
            if (occupantEntity == null) return false;
            if (!occupantEntity.TryGetUnit(out Unit occupantUnit)) return false;
            if (!Owner.TryGetUnit(out Unit myUnit)) return false;
            if (!Owner.TryGetUnitCombat(out UnitCombat myCombat)) return false;
            if (!occupantEntity.TryGetUnitCombat(out _)) return false;

            byte myId = NationUtility.GetIdOrDefault(nationIndex, myUnit.Tag);
            byte otherId = NationUtility.GetIdOrDefault(nationIndex, occupantUnit.Tag);
            if (!diplomacyIndex.IsHostile(myId, otherId))
                return false;

            // 对方已在战斗中，不重复触发。
            if (occupantEntity.HasCombatState()) return false;

            // 双方进入战斗。
            Owner.AddComponent<CombatState>().OpponentEntityId = occupantEntityId;
            occupantEntity.AddComponent<CombatState>().OpponentEntityId = Owner.Id;

            // 清理移动状态。
            DestroyPathIndicator(target);
            Owner.RemoveComponent(m_TargetId);
            occupantEntity.RemoveComponent(m_TargetId);
            return true;
        }
    }
}
