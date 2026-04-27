#region

using System.Collections.Generic;
using Common.Event;
using Core;
using Core.Capability;
using GamePlay.Map;
using GamePlay.Util;
using GamePlay.World;
using UnityEngine;
using Grid = GamePlay.Map.Grid;

#endregion

namespace GamePlay.Strategy
{
    // 玩家选择与目的地设定能力。它不使用 Filter，便于通过 gameplay 点击逐帧激活调试。
    public class SelectAndSetDestinationCap : CapabilityBase
    {
        private readonly List<HexCoordinates> m_PathBuffer = new List<HexCoordinates>(128);
        private SelectionKind m_SelectionKind = SelectionKind.None;
        private HexCoordinates m_SelectedHex;
        private int m_SelectedUnitEntityId = -1;
        private int m_SelectionIndicatorId = -1;
        private int m_RequestVersion;

        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.OrderUnitSelection;

        public override bool ShouldActivate()
        {
            InputManager inputManager = InputManager.Instance;
            return inputManager != null && inputManager.HasGameplayClickThisFrame;
        }

        public override bool ShouldDeactivate()
        {
            if (m_SelectionKind != SelectionKind.None) return false;

            InputManager inputManager = InputManager.Instance;
            if (inputManager == null) return true;
            if (!inputManager.HasGameplayClickThisFrame) return true;
            return false;
        }

        public override void TickActive(float deltaTime, float realElapsedSeconds)
        {
            // 处理本帧点击（如果有）
            InputManager inputManager = InputManager.Instance;
            if (inputManager != null &&
                inputManager.TryConsumeGameplayClick(out Vector2 screenPosition))
            {
                HandleGameplayClick(screenPosition);
            }

            // 选中存在时每帧刷新指示器世界位置：
            // 鬼列数量随相机缩放动态增减，缩放后原鬼列可能消失，需将指示器移到最近可见的镜像位置。
            if (m_SelectionIndicatorId >= 0)
            {
                RefreshIndicatorPosition();
            }
        }

        // 处理一次 gameplay 点击：解析格坐标 → 空选 / 同格 / 移动 / 新选 分支。
        private void HandleGameplayClick(Vector2 screenPosition)
        {
            if (!TryResolveMapContext(out Grid grid, out DrawMap drawMap,
                    out UnitOccupancyIndex occupancyIndex, out UnityEngine.Camera camera)) return;
            if (!HexMapUtility.TryGetClickedHex(camera, drawMap.Tilemap, grid, screenPosition,
                    out HexCoordinates clickedHex, out Vector3Int clickedCell, out _)) return;

            Vector3 indicatorPosition = drawMap.Tilemap.GetCellCenterWorld(clickedCell);

            bool hasUnit = TryGetUnitAt(occupancyIndex, clickedHex, out CEntity clickedUnit);
            if (m_SelectionKind == SelectionKind.None)
            {
                Select(clickedHex, indicatorPosition, hasUnit ? clickedUnit.Id : -1,
                    hasUnit ? SelectionKind.Unit : SelectionKind.Cell);
                return;
            }

            if (clickedHex.Equals(m_SelectedHex))
            {
                HandleSameHexClick(clickedHex, indicatorPosition, hasUnit ? clickedUnit.Id : -1,
                    hasUnit);
                return;
            }

            if (m_SelectionKind == SelectionKind.Unit &&
                TryGetSelectedUnit(out CEntity selectedUnit))
            {
                if (TryIssueMove(selectedUnit, clickedHex, grid, occupancyIndex))
                {
                    ClearSelection();
                }

                return;
            }

            Select(clickedHex, indicatorPosition, hasUnit ? clickedUnit.Id : -1,
                hasUnit ? SelectionKind.Unit : SelectionKind.Cell);
        }

        // 利用 HexMapUtility 的镜像位置选择，将指示器移到离相机最近的 hex 可见副本上。
        private void RefreshIndicatorPosition()
        {
            if (!TryResolveMapContext(out Grid grid, out DrawMap drawMap,
                    out _, out UnityEngine.Camera camera)) return;

            Vector3 newPosition = HexMapUtility.GetNearestMirroredWorldPosition(
                drawMap.Tilemap, grid, m_SelectedHex, camera.transform.position);

            EventBus.GP_OnUpdateSelectionIndicator?.Invoke(m_SelectionIndicatorId, newPosition);
        }

        protected override void OnDeactivated()
        {
            // 外部阻塞或组件移除导致能力停用时，也要保证选择指示器不会残留。
            if (m_SelectionKind == SelectionKind.None)
            {
                return;
            }

            ClearSelection();
        }

        public override void Dispose()
        {
            ClearSelection();
            base.Dispose();
        }

        private void HandleSameHexClick
        (
            HexCoordinates clickedHex, Vector3 indicatorPosition, int unitEntityId,
            bool hasUnit
        )
        {
            if (m_SelectionKind == SelectionKind.Unit && hasUnit)
            {
                // 第二次点同一个有单位的格子时，只降级为 cell 选择，保留现有指示器。
                m_SelectedHex = clickedHex;
                m_SelectedUnitEntityId = unitEntityId;
                m_SelectionKind = SelectionKind.Cell;
                return;
            }

            ClearSelection();
        }

        private bool TryIssueMove
        (
            CEntity selectedUnit, HexCoordinates destinationHex, Grid grid,
            UnitOccupancyIndex occupancyIndex
        )
        {
            // 逐条校验移动指令的合法性：单位存在、不在原地、目标可通行、路径至少两格。
            if (selectedUnit == null) return false;
            if (!selectedUnit.TryGetUnitPosition(out UnitPosition position)) return false;
            if (position.Hex.Equals(destinationHex)) return false;
            if (occupancyIndex != null &&
                occupancyIndex.IsOccupiedByOther(destinationHex, selectedUnit.Id)) return false;
            if (!HexMapUtility.TryFindPath(grid, occupancyIndex, position.Hex, destinationHex,
                    selectedUnit.Id, m_PathBuffer)) return false;
            if (m_PathBuffer.Count < 2) return false;

            if (!selectedUnit.TryGetUnitMoveTarget(out UnitMoveTarget target))
            {
                target = selectedUnit.AddComponent<UnitMoveTarget>();
            }

            if (target.PathIndicatorId >= 0)
            {
                EventBus.GP_OnDestroyPathIndicator?.Invoke(target.PathIndicatorId);
            }

            target.DestinationHex = m_PathBuffer[m_PathBuffer.Count - 1];
            target.Path = m_PathBuffer.ToArray();
            target.NextPathIndex = 1;
            target.RequestVersion = ++m_RequestVersion;
            target.PathIndicatorId = -1;
            return true;
        }

        private void Select
        (
            HexCoordinates hex, Vector3 indicatorPosition, int unitEntityId,
            SelectionKind kind
        )
        {
            DestroySelectionIndicator();
            m_SelectedHex = hex;
            m_SelectedUnitEntityId = unitEntityId;
            m_SelectionKind = kind;
            m_SelectionIndicatorId =
                EventBus.GP_OnCreateSelectionIndicator?.Invoke(indicatorPosition) ?? -1;
        }

        private void ClearSelection()
        {
            DestroySelectionIndicator();
            m_SelectedUnitEntityId = -1;
            m_SelectionKind = SelectionKind.None;
        }

        private void DestroySelectionIndicator()
        {
            if (m_SelectionIndicatorId < 0)
            {
                return;
            }

            EventBus.GP_OnDestroySelectionIndicator?.Invoke(m_SelectionIndicatorId);
            m_SelectionIndicatorId = -1;
        }

        private bool TryResolveMapContext
        (
            out Grid grid, out DrawMap drawMap, out UnitOccupancyIndex occupancyIndex,
            out UnityEngine.Camera camera
        )
        {
            grid = null;
            drawMap = null;
            occupancyIndex = null;
            camera = null;

            // 从当前实体（地图实体）逐项解析 Grid / DrawMap / 占位索引 / Camera。
            if (!Owner.TryGetGrid(out grid)) return false;
            if (!Owner.TryGetDrawMap(out drawMap)) return false;
            if (!Owner.TryGetUnitOccupancyIndex(out occupancyIndex)) return false;
            if (drawMap.Tilemap == null) return false;

            camera = UnityEngine.Camera.main;
            return camera != null;
        }

        private bool TryGetUnitAt
            (UnitOccupancyIndex occupancyIndex, HexCoordinates hex, out CEntity unitEntity)
        {
            unitEntity = null;
            if (occupancyIndex == null) return false;
            if (!occupancyIndex.TryGetUnit(hex, out int unitEntityId)) return false;
            if (World.GetChild(unitEntityId) is not CEntity entity) return false;
            if (!entity.HasUnit()) return false;

            unitEntity = entity;
            return true;
        }

        private bool TryGetSelectedUnit(out CEntity selectedUnit)
        {
            selectedUnit = null;
            if (m_SelectedUnitEntityId < 0)
            {
                return false;
            }

            selectedUnit = World.GetChild(m_SelectedUnitEntityId);
            return selectedUnit != null && selectedUnit.HasUnit();
        }

        private enum SelectionKind
        {
            None,
            Unit,
            Cell
        }
    }
}