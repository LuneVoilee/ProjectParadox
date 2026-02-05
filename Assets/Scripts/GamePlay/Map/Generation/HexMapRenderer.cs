using UnityEngine;
using UnityEngine.Tilemaps;
using Map.Data;

namespace Map.View
{
    public class HexMapRenderer : MonoBehaviour
    {
        [SerializeField] private Tilemap m_Tilemap;
        [SerializeField] private TerrainPalette m_TerrainPalette;
        [SerializeField] private int m_GhostColumns = 6;
        [SerializeField] private bool m_AutoGhostColumns = true;

        private const int m_DefaultGhostColumns = 6;
        private const int m_GhostPadding = 1;

        public int MapWidth { get; private set; }
        public int MapHeight { get; private set; }
        public Tilemap Tilemap => m_Tilemap;

        public void Render(GridData data)
        {
            if (data == null || m_Tilemap == null)
            {
                return;
            }

            var width = data.Width;
            var height = data.Height;
            var extraColumns = ResolveGhostColumns();
            var total = (width + extraColumns * 2) * height;
            MapWidth = width;
            MapHeight = height;

            var positions = new Vector3Int[total];
            var tiles = new TileBase[total];

            var i = 0;
            for (var row = 0; row < height; row++)
            {
                for (var col = -extraColumns; col < width + extraColumns; col++)
                {
                    var cell = extraColumns > 0 ? data.GetCellWrappedX(col, row) : data.GetCell(col, row);
                    positions[i] = new Vector3Int(col, row, 0);
                    tiles[i] = GetTileForCell(cell);
                    i++;
                }
            }

            m_Tilemap.ClearAllTiles();
            m_Tilemap.SetTiles(positions, tiles);
            m_Tilemap.RefreshAllTiles();
        }

        private TileBase GetTileForCell(CellData cell)
        {
            if (cell == null || m_TerrainPalette == null)
            {
                return null;
            }

            return m_TerrainPalette.GetTile(cell.TerrainType);
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

            var camera = Camera.main;
            if (camera == null)
            {
                camera = FindFirstObjectByType<Camera>();
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
