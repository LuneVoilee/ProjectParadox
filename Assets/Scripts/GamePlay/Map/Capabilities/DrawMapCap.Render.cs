#region

using UnityEngine;
using UnityEngine.Tilemaps;

#endregion

namespace GamePlay.Map
{
    public partial class DrawMapCap
    {
        private bool TryRender(DrawMap drawMap, Grid grid)
        {
            var tilemap = drawMap.Tilemap;
            var terrainSettings = drawMap.TerrainSettings;
            var cells = grid.Cells;
            if (tilemap == null || terrainSettings == null || cells == null)
            {
                return false;
            }

            var width = grid.Width;
            var height = grid.Height;
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            var extraColumns = ResolveGhostColumns(drawMap, tilemap);
            var total = (width + extraColumns * 2) * height;
            if (total <= 0)
            {
                return false;
            }

            EnsureTileBuffers(total);

            int i = 0;
            for (int row = 0; row < height; row++)
            {
                int rowStart = row * width;
                for (int col = -extraColumns; col < width + extraColumns; col++)
                {
                    int cellCol = extraColumns > 0 ? WrapIndex(col, width) : col;
                    int index = rowStart + cellCol;
                    m_CachedTiles[i] = (uint)index >= (uint)cells.Length
                        ? null
                        : GetTileForCell(terrainSettings, cells[index]);
                    m_CachedPositions[i] = new Vector3Int(col, row, 0);
                    i++;
                }
            }

            Vector3Int unusedCell = new Vector3Int(-extraColumns - 1, -1, 0);
            for (int clearIndex = total; clearIndex < m_CachedPositions.Length; clearIndex++)
            {
                m_CachedPositions[clearIndex] = unusedCell;
                m_CachedTiles[clearIndex] = null;
            }

            tilemap.ClearAllTiles();
            tilemap.SetTiles(m_CachedPositions, m_CachedTiles);
            tilemap.RefreshAllTiles();
            drawMap.LastGhostColumns = extraColumns;
            return true;
        }

        private static TileBase GetTileForCell(TerrainSettings terrainSettings, Cell cell)
        {
            return terrainSettings.GetTile(cell.TerrainType);
        }

        private static int WrapIndex(int value, int max)
        {
            if (max <= 0)
            {
                return 0;
            }

            int wrapped = value % max;
            return wrapped < 0 ? wrapped + max : wrapped;
        }

        private void EnsureTileBuffers(int total)
        {
            if (m_CachedPositions.Length < total)
            {
                m_CachedPositions = new Vector3Int[total];
                m_CachedTiles = new TileBase[total];
            }
        }
    }
}
