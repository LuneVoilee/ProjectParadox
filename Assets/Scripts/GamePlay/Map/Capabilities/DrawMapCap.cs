#region

using Core.Capability;
using Map.Data;
using UnityEngine;
using UnityEngine.Tilemaps;

#endregion

namespace GamePlay.Map
{
    public class DrawHexCap : CapabilityBase
    {
        private static readonly int m_GridId = Component<Grid>.TId;
        private static readonly int m_DrawMapId = Component<DrawMap>.TId;

        private Tilemap m_Tilemap;
        private int m_Width;
        private int m_Height;

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
            if (!Owner.TryGetComponent<DrawMap>(m_DrawMapId, out var drawMap) ||
                !Owner.TryGetComponent<Grid>(m_GridId, out var grid))
            {
                Owner.RemoveComponent(m_DrawMapId);
                Owner.RemoveComponent(m_GridId);
                return;
            }

            m_Tilemap = drawMap.Tilemap;
            m_Width = grid.Width;
            m_Height = grid.Height;
        }

        public void Render(Grid grid)
        {
            if (data == null || m_Tilemap == null)
            {
                return;
            }

            m_LastData = data;
            var width = data.Width;
            var height = data.Height;
            var extraColumns = ResolveGhostColumns();
            var total = (width + extraColumns * 2) * height;
            MapWidth = width;
            MapHeight = height;
            m_LastGhostColumns = extraColumns;

            var positions = new Vector3Int[total];
            var tiles = new TileBase[total];

            var i = 0;
            for (var row = 0; row < height; row++)
            {
                for (var col = -extraColumns; col < width + extraColumns; col++)
                {
                    var cell = extraColumns > 0
                        ? data.GetCellWrappedX(col, row)
                        : data.GetCell(col, row);
                    positions[i] = new Vector3Int(col, row, 0);
                    tiles[i] = GetTileForCell(cell);
                    i++;
                }
            }

            m_Tilemap.ClearAllTiles();
            m_Tilemap.SetTiles(positions, tiles);
            m_Tilemap.RefreshAllTiles();
        }

        public void RefreshGhostColumns()
        {
            if (m_LastData == null)
            {
                return;
            }

            Render(m_LastData);
        }

        public void SetColor(Vector3Int pos, Color color)
        {
            m_Tilemap.SetColor(pos, color);
        }

        private TileBase GetTileForCell(CellData cellData)
        {
            if (cellData == null || m_TerrainSettings == null)
            {
                return null;
            }

            return m_TerrainSettings.GetTile(cellData.TerrainType);
        }

        private int ResolveGhostColumns()
        {
            var extraColumns = Mathf.Max(0, m_GhostColumns);
            if (!m_AutoGhostColumns)
            {
                return extraColumns;
            }

            if (m_Tilemap == null)
            {
                return Mathf.Max(extraColumns, m_DefaultGhostColumns);
            }

            var camera = UnityEngine.Camera.main;
            if (camera == null)
            {
                camera = FindFirstObjectByType<UnityEngine.Camera>();
            }

            if (camera == null || !camera.orthographic)
            {
                return Mathf.Max(extraColumns, m_DefaultGhostColumns);
            }

            var origin = m_Tilemap.CellToWorld(Vector3Int.zero);
            var next = m_Tilemap.CellToWorld(new Vector3Int(1, 0, 0));
            var columnWidth = Mathf.Abs(next.x - origin.x);
            if (columnWidth <= Mathf.Epsilon)
            {
                return Mathf.Max(extraColumns, m_DefaultGhostColumns);
            }

            var viewWidth = camera.orthographicSize * 2f * camera.aspect;
            var needed = Mathf.CeilToInt(viewWidth / columnWidth) + m_GhostPadding;
            return Mathf.Max(extraColumns, needed);
        }
    }
}

}