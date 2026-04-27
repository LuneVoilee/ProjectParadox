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

        // HOI4 式步进移动：时间累积到 timePerHex 后推进一格，表现层独立插值。
        private float m_StepTimer;
        private Vector3 m_VisualLerpStart;
        private Vector3 m_VisualLerpTarget;
        private float m_VisualLerpProgress = 1f;
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
            if (Owner != null &&
                Owner.TryGetUnitMoveTarget(out UnitMoveTarget target))
            {
                DestroyPathIndicator(target);
            }

            base.Dispose();
        }

        protected override void OnActivated()
        {
            m_StepTimer = 0f;
            m_VisualLerpProgress = 1f;
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
                    out UnitOccupancyIndex occupancyIndex)) return;

            Tilemap tilemap = drawMap.Tilemap;
            EnsurePathIndicator(target, grid, tilemap, motor.Transform.position);

            // 逻辑阶段：时间累积到 timePerHex 则推进到路径下一格。
            m_StepTimer += deltaTime;
            float timePerHex = 1f / Mathf.Max(0.01f, unit.MoveSpeed);

            while (m_StepTimer >= timePerHex && target.NextPathIndex < target.Path.Length)
            {
                Vector3 oldWorldPos = HexMapUtility.GetNearestMirroredWorldPosition(
                    tilemap, grid, position.Hex, motor.Transform.position);

                EnterPathHex(target, position, occupancyIndex, grid, target.NextPathIndex);
                target.NextPathIndex++;
                m_StepTimer -= timePerHex;

                Vector3 newWorldPos = HexMapUtility.GetNearestMirroredWorldPosition(
                    tilemap, grid, position.Hex, oldWorldPos);

                m_VisualLerpStart = oldWorldPos;
                m_VisualLerpTarget = newWorldPos;
                m_VisualLerpProgress = 0f;
            }

            // 表现阶段：推进视觉插值（在逻辑阶段之后，确保最后一格的插值也能拿到 deltaTime）。
            if (m_VisualLerpProgress < 1f)
            {
                m_VisualLerpProgress += deltaTime / VisualMoveDuration;
                if (m_VisualLerpProgress >= 1f)
                {
                    motor.Transform.position = m_VisualLerpTarget;
                }
                else
                {
                    motor.Transform.position =
                        Vector3.Lerp(m_VisualLerpStart, m_VisualLerpTarget, m_VisualLerpProgress);
                }
            }

            bool logicDone = target.NextPathIndex >= target.Path.Length;
            if (logicDone)
            {
                DestroyPathIndicator(target);
                if (m_VisualLerpProgress >= 1f)
                {
                    Owner.RemoveComponent(m_TargetId);
                }

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
            }
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