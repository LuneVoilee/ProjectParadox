#region

using System;
using Core.Capability;
using NewGamePlay;
using UnityEngine;
using UnityEngine.Tilemaps;

#endregion

namespace GamePlay.Map
{
    public partial class DrawMapCap : CapabilityBase
    {
        private static readonly int m_GridId = Component<Grid>.TId;
        private static readonly int m_DrawMapId = Component<DrawMap>.TId;
        public override int TickGroupOrder { get; protected set; } = CapabilityOrder.PresentationMapDraw;

        // 复用缓存数组，避免高频重绘时产生 GC 抖动。
        private Vector3Int[] m_CachedPositions = Array.Empty<Vector3Int>();
        private TileBase[] m_CachedTiles = Array.Empty<TileBase>();
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
            Filter(m_GridId, m_DrawMapId);
        }

        public override bool ShouldActivate()
        {
            return Owner.HasComponent(m_GridId) &&
                   Owner.HasComponent(m_DrawMapId);
        }

        public override bool ShouldDeactivate()
        {
            return !ShouldActivate();
        }

        protected override void OnActivated()
        {
            if (Owner.TryGetDrawMap(out var drawMap))
            {
                drawMap.IsDirty = true;
            }

            m_IsAutoGhostCacheValid = false;
        }

        public override void TickActive(float deltaTime, float realElapsedSeconds)
        {
            if (!Owner.TryGetDrawMap(out var drawMap) ||
                !Owner.TryGetGrid(out var grid))
            {
                return;
            }

            if (!drawMap.IsDirty)
            {
                return;
            }

            if (!TryRender(drawMap, grid))
            {
                return;
            }

            drawMap.IsDirty = false;
        }
    }
}
