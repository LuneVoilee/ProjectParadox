#region

using System;
using Core.Capability;
using GamePlay.Strategy;
using GamePlay.Util;
using GamePlay.World;
using UnityEngine;
using UnityEngine.Tilemaps;

#endregion

namespace GamePlay.Map
{
    // 地图绘制 Cap：负责 terrain tile 重建和国家颜色 pass。
    // 规则状态来自 Grid.Cells.OwnerId，颜色表来自 NationIndex，绘制脏状态来自 TerritoryPaintState。
    public partial class DrawMapCap : CapabilityBase
    {
        // 绘制必须同时具备底图、格子、国家颜色索引和颜色脏状态。
        private static readonly int m_GridId = Component<Grid>.TId;
        private static readonly int m_DrawMapId = Component<DrawMap>.TId;
        private static readonly int m_NationIndexId = Component<NationIndex>.TId;
        private static readonly int m_TerritoryPaintStateId = Component<TerritoryPaintState>.TId;

        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.PresentationMapDraw;

        // terrain tile 批量 SetTiles 使用的缓存数组，复用它们可避免频繁分配。
        private Vector3Int[] m_CachedPositions = Array.Empty<Vector3Int>();
        private TileBase[] m_CachedTiles = Array.Empty<TileBase>();

        // 自动 ghost columns 的输入缓存；相机或屏幕参数变化后才重新计算横向补绘列数。
        private UnityEngine.Camera m_CachedCamera;
        private bool m_IsAutoGhostCacheValid;
        private int m_CachedAutoGhostColumns = DrawMap.DefaultGhostColumns;
        private int m_LastScreenWidth = -1;
        private int m_LastScreenHeight = -1;
        private int m_LastCameraInstanceId = int.MinValue;
        private bool m_LastCameraOrthographic;
        private float m_LastCameraOrthographicSize = -1f;
        private float m_LastCameraAspect = -1f;

        protected override void OnInit()
        {
            Filter(m_GridId, m_DrawMapId, m_NationIndexId, m_TerritoryPaintStateId);
        }

        public override bool ShouldActivate()
        {
            return Owner.HasComponent(m_GridId) &&
                   Owner.HasComponent(m_DrawMapId) &&
                   Owner.HasComponent(m_NationIndexId) &&
                   Owner.HasComponent(m_TerritoryPaintStateId);
        }

        public override bool ShouldDeactivate()
        {
            return !ShouldActivate();
        }

        protected override void OnActivated()
        {
            // Capability 首次激活时强制 terrain 和颜色都全量刷新，保证新地图不会沿用旧缓存。
            if (Owner.TryGetDrawMap(out var drawMap))
            {
                drawMap.IsDirty = true;
            }

            if (Owner.TryGetTerritoryPaintState(out var paintState))
            {
                paintState.ColorDirtyAll = true;
            }

            m_IsAutoGhostCacheValid = false;
        }

        public override void TickActive(float deltaTime, float realElapsedSeconds)
        {
            // 组件可被运行时移除，因此每帧取最新引用并做空保护。
            if (!Owner.TryGetDrawMap(out var drawMap)) return;
            if (!Owner.TryGetGrid(out var grid)) return;
            if (!Owner.TryGetNationIndex(out var nationIndex)) return;
            if (!Owner.TryGetTerritoryPaintState(out var paintState)) return;

            // terrainDirty 控制底图 tile 重建；colorDirty 控制国家颜色 pass。
            bool terrainDirty = drawMap.IsDirty;
            bool colorDirty = paintState.ColorDirtyAll || paintState.DirtyCellIndices.Count > 0;
            if (!terrainDirty && !colorDirty)
            {
                return;
            }

            // TryRender 内部会根据 dirty 类型选择 terrain 重建、全量颜色或增量颜色刷新。
            if (!TryRender(drawMap, grid, nationIndex, paintState, terrainDirty))
            {
                return;
            }

            // 成功绘制后清理本帧脏标记；权威 owner 数据仍保存在 Grid.Cells 中。
            drawMap.IsDirty = false;
            paintState.DirtyCellSet.Clear();
            paintState.DirtyCellIndices.Clear();
        }
    }
}
