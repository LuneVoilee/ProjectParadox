#region

using System;
using Core.Capability;
using UnityEngine;
using UnityEngine.Tilemaps;

#endregion

namespace GamePlay.Map
{
    public class DrawMapCap : CapabilityBase
    {
        private static readonly int m_GridId = Component<Grid>.TId;
        private static readonly int m_DrawMapId = Component<DrawMap>.TId;

        // Cache arrays to avoid high-frequency GC allocations.
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
            if (Owner.GetComponent(m_DrawMapId) is DrawMap drawMap)
            {
                drawMap.IsDirty = true;
            }

            m_IsAutoGhostCacheValid = false;
        }

        public override void TickActive(float deltaTime, float realElapsedSeconds)
        {
            if (!Owner.TryGetComponent<DrawMap>(m_DrawMapId, out var drawMap) ||
                !Owner.TryGetComponent<Grid>(m_GridId, out var grid))
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

            // 鬼列：在地图左右各补若干列，避免镜头横向移动时看到空白边界。
            var extraColumns = ResolveGhostColumns(drawMap, tilemap);
            // 实际绘制宽度 = 原始宽度 + 左右鬼列。
            var total = (width + extraColumns * 2) * height;
            if (total <= 0)
            {
                return false;
            }

            EnsureTileBuffers(total);

            var i = 0;
            for (var row = 0; row < height; row++)
            {
                var rowStart = row * width;
                // col 覆盖 [-extraColumns, width + extraColumns)，含左右鬼列区间。
                for (var col = -extraColumns; col < width + extraColumns; col++)
                {
                    // 鬼列不读“越界列”，而是回卷到 [0, width) 读取原地图，实现横向无缝循环。
                    var cellCol = extraColumns > 0 ? WrapIndex(col, width) : col;
                    var index = rowStart + cellCol;
                    if ((uint)index >= (uint)cells.Length)
                    {
                        m_CachedTiles[i] = null;
                    }
                    else
                    {
                        m_CachedTiles[i] = GetTileForCell(terrainSettings, cells[index]);
                    }

                    // 位置依然使用真实绘制列号（可为负数或超出 width），让 Tilemap 真正补出左右鬼列。
                    m_CachedPositions[i] = new Vector3Int(col, row, 0);
                    i++;
                }
            }

            var unusedCell = new Vector3Int(-extraColumns - 1, -1, 0);
            for (var clearIndex = total; clearIndex < m_CachedPositions.Length; clearIndex++)
            {
                m_CachedPositions[clearIndex] = unusedCell;
                m_CachedTiles[clearIndex] = null;
            }

            tilemap.ClearAllTiles();
            tilemap.SetTiles(m_CachedPositions, m_CachedTiles);
            tilemap.RefreshAllTiles();

            // 记录本次鬼列结果，供后续能力/系统读取（例如相机或输入坐标换算时做边界处理）。
            drawMap.LastGhostColumns = extraColumns;
            return true;
        }

        private static TileBase GetTileForCell(TerrainSettings terrainSettings, Cell cell)
        {
            var terrainType = cell.TerrainType;
            return terrainSettings.GetTile(terrainType);
        }

        private static int WrapIndex(int value, int max)
        {
            if (max <= 0)
            {
                return 0;
            }

            var wrapped = value % max;
            return wrapped < 0 ? wrapped + max : wrapped;
        }

        private int ResolveGhostColumns(DrawMap drawMap, Tilemap tilemap)
        {
            // 规则1：先取手动保底值（负数按 0 处理）。
            var extraColumns = Mathf.Max(0, drawMap.GhostColumnsFloor);
            // 规则2：关闭自动计算时，直接返回保底值。
            if (!drawMap.AutoGhostColumns)
            {
                return extraColumns;
            }

            var camera = ResolveCamera();
            // 自动估算仅在分辨率或缩放参数变化时重算，避免每次渲染都重复计算。
            if (!m_IsAutoGhostCacheValid || HasGhostColumnsInputChanged(camera))
            {
                m_CachedAutoGhostColumns = CalculateAutoGhostColumns(tilemap, camera);
                CacheGhostColumnsInput(camera);
                m_IsAutoGhostCacheValid = true;
            }

            // 最终值取 max(保底值, 自动估算值)。
            return Mathf.Max(extraColumns, m_CachedAutoGhostColumns);
        }

        private void EnsureTileBuffers(int total)
        {
            if (m_CachedPositions.Length < total)
            {
                m_CachedPositions = new Vector3Int[total];
                m_CachedTiles = new TileBase[total];
            }
        }

        private UnityEngine.Camera ResolveCamera()
        {
            if (m_CachedCamera != null)
            {
                return m_CachedCamera;
            }

            m_CachedCamera = UnityEngine.Camera.main;

            return m_CachedCamera;
        }

        private static int CalculateAutoGhostColumns(Tilemap tilemap, UnityEngine.Camera camera)
        {
            // 退化路径：无正交相机时，用默认值防止边缘漏空。
            if (camera == null || !camera.orthographic)
            {
                return DrawMap.DefaultGhostColumns;
            }

            // 通过相邻两列 CellToWorld 的 x 差，计算“单列世界宽度”。
            var origin = tilemap.CellToWorld(Vector3Int.zero);
            var next = tilemap.CellToWorld(new Vector3Int(1, 0, 0));
            var columnWidth = Mathf.Abs(next.x - origin.x);
            if (columnWidth <= Mathf.Epsilon)
            {
                return DrawMap.DefaultGhostColumns;
            }

            // needed = ceil(相机可视宽度 / 单列宽度) + 安全补边列数。
            var viewWidth = camera.orthographicSize * 2f * camera.aspect;
            return Mathf.CeilToInt(viewWidth / columnWidth) + DrawMap.GhostPadding;
        }

        private bool HasGhostColumnsInputChanged(UnityEngine.Camera camera)
        {
            if (m_LastScreenWidth != Screen.width || m_LastScreenHeight != Screen.height)
            {
                return true;
            }

            var cameraInstanceId = camera == null ? int.MinValue : camera.GetInstanceID();
            if (cameraInstanceId != m_LastCameraInstanceId)
            {
                return true;
            }

            var isOrthographic = camera != null && camera.orthographic;
            if (isOrthographic != m_LastCameraOrthographic)
            {
                return true;
            }

            if (!isOrthographic)
            {
                return false;
            }

            return !Mathf.Approximately(m_LastCameraOrthographicSize, camera.orthographicSize) ||
                   !Mathf.Approximately(m_LastCameraAspect, camera.aspect);
        }

        private void CacheGhostColumnsInput(UnityEngine.Camera camera)
        {
            m_LastScreenWidth = Screen.width;
            m_LastScreenHeight = Screen.height;
            m_LastCameraInstanceId = camera == null ? int.MinValue : camera.GetInstanceID();
            m_LastCameraOrthographic = camera != null && camera.orthographic;

            if (!m_LastCameraOrthographic)
            {
                m_LastCameraOrthographicSize = -1f;
                m_LastCameraAspect = -1f;
                return;
            }

            m_LastCameraOrthographicSize = camera.orthographicSize;
            m_LastCameraAspect = camera.aspect;
        }
    }
}