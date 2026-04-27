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
    // 单位沿 hex 路径移动的执行能力。只改 Transform 位移，不处理动画。
    public class MoveAlongHexPathCap : CapabilityBase
    {
        private static readonly int m_UnitId = Component<Unit>.TId;
        private static readonly int m_PositionId = Component<UnitPosition>.TId;
        private static readonly int m_MotorId = Component<UnitMotor>.TId;
        private static readonly int m_TargetId = Component<UnitMoveTarget>.TId;

        private readonly List<Vector3> m_WorldPathBuffer = new List<Vector3>(64);

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
            if (Owner != null &&
                Owner.TryGetUnitMoveTarget(out UnitMoveTarget target))
            {
                DestroyPathIndicator(target);
            }

            base.Dispose();
        }

        public override void TickActive(float deltaTime, float realElapsedSeconds)
        {
            // 逐条守卫：任意前置条件不满足就本帧不做移动推进，避免多层条件叠在一起难以调试。
            if (deltaTime <= 0f) return;
            if (!Owner.TryGetUnit(out Unit unit)) return;
            if (!Owner.TryGetUnitPosition(out UnitPosition position)) return;
            if (!Owner.TryGetUnitMotor(out UnitMotor motor)) return;
            if (!Owner.TryGetUnitMoveTarget(out UnitMoveTarget target)) return;
            if (motor.Transform == null) return;
            if (target.Path == null) return;
            if (target.Path.Length < 2) return;
            if (!TryResolveMapContext(out Grid grid, out DrawMap drawMap,
                    out UnitOccupancyIndex occupancyIndex)) return;

            Tilemap tilemap = drawMap.Tilemap;
            EnsurePathIndicator(target, grid, tilemap, motor.Transform.position);

            // 逐帧推进：用本帧可移动距离沿路径消耗，跨格时自动进入下一格并更新占位。
            float remainingDistance = Mathf.Max(0.01f, unit.MoveSpeed) * deltaTime;
            while (remainingDistance > 0f && target.NextPathIndex < target.Path.Length)
            {
                Vector3 nextWorldPosition = HexMapUtility.GetNearestMirroredWorldPosition(
                    tilemap, grid, target.Path[target.NextPathIndex], motor.Transform.position);
                Vector3 toTarget = nextWorldPosition - motor.Transform.position;
                float distance = toTarget.magnitude;
                float arriveDistance = Mathf.Max(0.001f, motor.ArriveDistance);

                if (distance <= arriveDistance)
                {
                    EnterPathHex(target, position, occupancyIndex, grid, target.NextPathIndex);
                    target.NextPathIndex++;
                    continue;
                }

                float step = Mathf.Min(remainingDistance, distance);
                motor.Transform.position += toTarget / distance * step;
                remainingDistance -= step;

                if (distance - step <= arriveDistance)
                {
                    motor.Transform.position = nextWorldPosition;
                    EnterPathHex(target, position, occupancyIndex, grid, target.NextPathIndex);
                    target.NextPathIndex++;
                }
                else
                {
                    break;
                }
            }

            // 到达路径终点：销毁指示器、移除 MoveTarget 组件以触发 ShouldDeactivate。
            if (target.NextPathIndex >= target.Path.Length)
            {
                FinishMove(target);
                return;
            }

            UpdatePathIndicator(target, grid, tilemap, motor.Transform.position);
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
                EmitOccupyRequest(nextHex);
            }
        }

        // 向 ChangedHexs 数组追加一个 Hex，OccupyCap 会在同帧稍后消费它。
        private void EmitOccupyRequest(HexCoordinates hex)
        {
            if (!Owner.TryGetChangedHexs(out ChangedHexs changedHexs))
            {
                changedHexs = Owner.AddComponent<ChangedHexs>();
            }

            if (changedHexs.Hexs == null)
            {
                changedHexs.Hexs = new List<HexCoordinates> { hex };
                return;
            }

            changedHexs.Hexs.Add(hex);
        }

        private void FinishMove(UnitMoveTarget target)
        {
            DestroyPathIndicator(target);
            Owner.RemoveComponent(m_TargetId);
        }

        private void EnsurePathIndicator
        (
            UnitMoveTarget target, Grid grid, Tilemap tilemap,
            Vector3 unitWorldPosition
        )
        {
            if (target.PathIndicatorId >= 0)
            {
                UpdatePathIndicator(target, grid, tilemap, unitWorldPosition);
                return;
            }

            BuildRemainingWorldPath(target, grid, tilemap, unitWorldPosition);
            if (m_WorldPathBuffer.Count < 2)
            {
                return;
            }

            target.PathIndicatorId =
                EventBus.GP_OnCreatePathIndicator?.Invoke(m_WorldPathBuffer) ?? -1;
        }

        private void UpdatePathIndicator
        (
            UnitMoveTarget target, Grid grid, Tilemap tilemap,
            Vector3 unitWorldPosition
        )
        {
            if (target.PathIndicatorId < 0)
            {
                return;
            }

            BuildRemainingWorldPath(target, grid, tilemap, unitWorldPosition);
            EventBus.GP_OnUpdatePathIndicator?.Invoke(target.PathIndicatorId, m_WorldPathBuffer);
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

        private bool TryResolveMapContext
        (
            out Grid grid, out DrawMap drawMap, out UnitOccupancyIndex occupancyIndex
        )
        {
            grid = null;
            drawMap = null;
            occupancyIndex = null;

            // 通过 GameWorld 拿到主地图实体，再逐项解析 Grid / DrawMap / 占位索引。
            if (World is not GameWorld gameWorld) return false;
            if (!gameWorld.TryGetPrimaryMapEntity(out CEntity mapEntity)) return false;
            if (!mapEntity.TryGetGrid(out grid)) return false;
            if (!mapEntity.TryGetDrawMap(out drawMap)) return false;
            if (!mapEntity.TryGetUnitOccupancyIndex(out occupancyIndex)) return false;
            if (drawMap.Tilemap == null) return false;
            return true;
        }
    }
}