#region

using Core.Capability.Editor;
using GamePlay.Strategy;
using UnityEngine;
using UnityEngine.Tilemaps;

#endregion

namespace GamePlay.Map
{
    public partial class DrawMapCap
    {
        // 统一渲染入口：terrainDirty 时重建底图 tile；颜色脏时刷新 nation color pass。
        private bool TryRender
        (
            DrawMap drawMap, Grid grid, NationIndex nationIndex,
            TerritoryPaintState paintState, bool terrainDirty
        )
        {
            var tilemap = drawMap.Tilemap;
            var terrainSettings = drawMap.TerrainSettings;
            var cells = grid.Cells;
            if (tilemap is null) return false;
            if (terrainSettings is null) return false;
            if (cells == null) return false;
            if (nationIndex == null) return false;
            if (paintState == null) return false;

            var width = grid.Width;
            var height = grid.Height;
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            // 根据相机和地图设置计算横向 ghost columns，保证无缝 X 地图在屏幕边缘也能补绘。
            var extraColumns = ResolveGhostColumns(drawMap, tilemap);
            var total = (width + extraColumns * 2) * height;
            if (total <= 0)
            {
                return false;
            }

            // 颜色缓存长度必须跟 Cells 对齐；缓存重建时会自动触发全图颜色刷新。
            EnsureColorCache(paintState, cells.Length, NationIndex.NeutralColor);

            if (terrainDirty)
            {
                // terrain pass：只负责 tile 本体，国家颜色在后面的 color pass 单独处理。
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

                // 缓存数组可能比本次所需更长，尾部位置放到无效坐标并清空 tile。
                Vector3Int unusedCell = new Vector3Int(-extraColumns - 1, -1, 0);
                for (int clearIndex = total; clearIndex < m_CachedPositions.Length; clearIndex++)
                {
                    m_CachedPositions[clearIndex] = unusedCell;
                    m_CachedTiles[clearIndex] = null;
                }

                tilemap.ClearAllTiles();
                tilemap.SetTiles(m_CachedPositions, m_CachedTiles);
                tilemap.RefreshAllTiles();
            }

            // color pass：dirty 太多或 terrain 刚重建时走全量，否则只刷 DirtyCellIndices。
            bool useFullRepaint = ShouldUseFullRepaint(paintState, cells.Length);
            if (terrainDirty || useFullRepaint)
            {
                PaintAllCells(drawMap, grid, nationIndex, paintState, extraColumns);
            }
            else
            {
                PaintDirtyCells(drawMap, grid, nationIndex, paintState, extraColumns);
            }

            // 颜色 pass 已完成，本次 ColorDirtyAll 可以关闭；单格 dirty 由主 Tick 成功后清理。
            paintState.ColorDirtyAll = false;

            drawMap.LastGhostColumns = extraColumns;
            return true;
        }

        private static TileBase GetTileForCell(TerrainSettings terrainSettings, Cell cell)
        {
            return terrainSettings.GetTile(cell.TerrainType);
        }

        private void SetHexColor(DrawMap drawMap, Vector3Int cellPosition, Color32 color)
        {
            var hexTilemap = drawMap.Tilemap;

            // ghost columns 或无效位置可能没有 tile，空格子无需设置颜色。
            TileBase tile = hexTilemap.GetTile(cellPosition);
            if (tile == null)
            {
                return;
            }
            /*
            你执行代码解锁：SetTileFlags(None) -> 实例数据的 Flags 被成功清空。

            你执行代码染色：SetColor(Blue) -> 实例数据的颜色被成功设置为蓝色。

            （暗箱操作发生）网格重建触发：因为你修改了 Tilemap，Unity 的内部机制在这一帧稍后或下一帧触发了网格刷新。

            资产夺回控制权：Tilemap 调用该位置 Tile 资产的 GetTileData 方法。因为该 .asset 文件的 Lock Color 是勾选状态，Unity 官方默认的 Tile 类会在这个方法内部强行执行类似这样的逻辑：
            tileData.flags = TileFlags.LockColor;

            实例数据被洗刷：Tilemap 拿到资产返回的 tileData 后，用它覆盖了该坐标原本的实例数据。你的 None 被重新改写为 LockColor，随后计算顶点色时，你的 Blue 被丢弃，退回默认纯白。
            */

            //已解决，要在编辑器里直接设置对应tile asset的flag才行
            hexTilemap.SetTileFlags(cellPosition, TileFlags.None);
            hexTilemap.SetColor(cellPosition, color);
        }

        // 全量颜色刷新：根据每个 cell 的 OwnerId 查 NationIndex 颜色，并同步所有 ghost columns。
        private void PaintAllCells
        (
            DrawMap drawMap, Grid grid, NationIndex nationIndex,
            TerritoryPaintState paintState, int extraColumns
        )
        {
            Cell[] cells = grid.Cells;
            int width = grid.Width;
            int height = grid.Height;
            if (cells == null) return;
            if (width <= 0) return;
            if (height <= 0) return;

            // 先按真实 Cells 顺序重建颜色缓存，后续绘制 ghost columns 时复用缓存值。
            int count = Mathf.Min(cells.Length, paintState.CellColorCache?.Length ?? 0);
            for (int index = 0; index < count; index++)
            {
                Color32 color =
                    NationRegistryCap.GetColorOrNeutral(nationIndex, cells[index].OwnerId);
                paintState.CellColorCache[index] = color;
            }

            // 遍历可见列加 ghost columns；ghost 列通过 WrapIndex 映射回真实 cell。
            for (int row = 0; row < height; row++)
            {
                int rowStart = row * width;
                for (int col = -extraColumns; col < width + extraColumns; col++)
                {
                    int cellCol = extraColumns > 0 ? WrapIndex(col, width) : col;
                    int cellIndex = rowStart + cellCol;
                    if ((uint)cellIndex >= (uint)count)
                    {
                        continue;
                    }

                    SetHexColor(drawMap, new Vector3Int(col, row, 0),
                        paintState.CellColorCache[cellIndex]);
                }
            }

            drawMap.Tilemap.RefreshAllTiles();
        }

        // 增量颜色刷新：只处理 DirtyCellIndices，并补刷该 cell 在 ghost columns 中的镜像位置。
        private void PaintDirtyCells
        (
            DrawMap drawMap, Grid grid, NationIndex nationIndex,
            TerritoryPaintState paintState, int extraColumns
        )
        {
            Cell[] cells = grid.Cells;
            int width = grid.Width;
            int height = grid.Height;
            if (cells == null) return;
            if (width <= 0) return;
            if (height <= 0) return;
            if (paintState.CellColorCache == null) return;

            int maxCount = Mathf.Min(cells.Length, paintState.CellColorCache.Length);
            for (int i = 0; i < paintState.DirtyCellIndices.Count; i++)
            {
                // 防御旧 dirty index 或地图尺寸变化导致的越界。
                int cellIndex = paintState.DirtyCellIndices[i];
                if ((uint)cellIndex >= (uint)maxCount)
                {
                    continue;
                }

                int row = cellIndex / width;
                if ((uint)row >= (uint)height)
                {
                    continue;
                }

                int baseCol = cellIndex - row * width;

                // 从权威 OwnerId 重新取颜色，写回缓存后再刷 tilemap。
                Color32 color =
                    NationRegistryCap.GetColorOrNeutral(nationIndex, cells[cellIndex].OwnerId);

                this.Log("Color:" + color);

                paintState.CellColorCache[cellIndex] = color;

                // 计算当前真实列在 ghost column 范围内的所有镜像列。
                int minK = Mathf.CeilToInt((-extraColumns - baseCol) / (float)width);
                int maxK = Mathf.FloorToInt((width + extraColumns - 1 - baseCol) / (float)width);
                for (int k = minK; k <= maxK; k++)
                {
                    int worldCol = baseCol + k * width;
                    if (worldCol < -extraColumns || worldCol >= width + extraColumns)
                    {
                        continue;
                    }

                    SetHexColor(drawMap, new Vector3Int(worldCol, row, 0), color);
                }
            }

            drawMap.Tilemap.RefreshAllTiles();
        }

        // 把任意列号折回 [0, width)，用于无缝 X 地图的 ghost column 映射。
        private static int WrapIndex(int value, int max)
        {
            if (max <= 0)
            {
                return 0;
            }

            int wrapped = value % max;
            return wrapped < 0 ? wrapped + max : wrapped;
        }

        private static void EnsureColorCache
            (TerritoryPaintState paintState, int cellCount, Color32 defaultColor)
        {
            // 无格子时释放缓存；有格子时保证缓存长度与 Cells 完全一致。
            if (cellCount <= 0)
            {
                paintState.CellColorCache = null;
                return;
            }

            if (paintState.CellColorCache != null &&
                paintState.CellColorCache.Length == cellCount)
            {
                return;
            }

            // 新缓存先填 Neutral，随后全量颜色 pass 会按 OwnerId 覆盖真实颜色。
            paintState.CellColorCache = new Color32[cellCount];
            for (int i = 0; i < paintState.CellColorCache.Length; i++)
            {
                paintState.CellColorCache[i] = defaultColor;
            }

            paintState.ColorDirtyAll = true;
        }

        private static bool ShouldUseFullRepaint(TerritoryPaintState paintState, int totalCellCount)
        {
            // ColorDirtyAll 是外部显式全量刷新请求，例如国家颜色表刚初始化。
            if (paintState.ColorDirtyAll)
            {
                return true;
            }

            // dirty 数量超过绝对值或比例阈值时，走全量比大量散点刷新更可控。
            if (totalCellCount <= 0)
            {
                return false;
            }

            if (paintState.DirtyCellIndices.Count >= paintState.DirtyToFullThresholdAbs)
            {
                return true;
            }

            float ratio = paintState.DirtyCellIndices.Count / (float)totalCellCount;
            return ratio >= paintState.DirtyToFullThresholdRatio;
        }

        private void EnsureTileBuffers(int total)
        {
            // 只扩容不缩容，避免地图轻微尺寸变化时频繁分配数组。
            if (m_CachedPositions.Length < total)
            {
                m_CachedPositions = new Vector3Int[total];
                m_CachedTiles = new TileBase[total];
            }
        }
    }
}