#region

using System.Collections.Generic;
using Core.Capability;
using GamePlay.Strategy;
using GamePlay.Util;
using GamePlay.World;
using UnityEngine;
using UnityEngine.Tilemaps;

#endregion

namespace GamePlay.Map
{
    // Hex 地图公共工具：集中处理 offset/cube、无缝折返、寻路和世界坐标转换。
    public static class HexMapUtility
    {
        private static readonly HexDirection[] s_Directions =
        {
            HexDirection.NE,
            HexDirection.E,
            HexDirection.SE,
            HexDirection.SW,
            HexDirection.W,
            HexDirection.NW
        };

        public static bool TryNormalizeCell
            (Grid grid, Vector3Int cell, out Vector3Int normalizedCell)
        {
            normalizedCell = cell;
            if (grid == null) return false;
            if (grid.Width <= 0) return false;
            if (grid.Height <= 0) return false;

            int col = cell.x;
            int row = cell.y;

            if (grid.EnableSeamlessX)
            {
                col = WrapIndex(col, grid.Width);
            }

            if (grid.EnableSeamlessY)
            {
                row = WrapIndex(row, grid.Height);
            }

            if ((uint)col >= (uint)grid.Width || (uint)row >= (uint)grid.Height)
            {
                return false;
            }

            normalizedCell = new Vector3Int(col, row, cell.z);
            return true;
        }

        public static bool TryNormalizeHex
            (Grid grid, HexCoordinates hex, out HexCoordinates normalizedHex)
        {
            Vector2Int offset = hex.ToOffset();
            var cell = new Vector3Int(offset.x, offset.y, 0);
            if (!TryNormalizeCell(grid, cell, out Vector3Int normalizedCell))
            {
                normalizedHex = default;
                return false;
            }

            normalizedHex = HexCoordinates.FromOffset(normalizedCell.x, normalizedCell.y);
            return true;
        }

        public static bool TryGetCellIndex(Grid grid, HexCoordinates hex, out int cellIndex)
        {
            cellIndex = -1;
            if (!TryNormalizeHex(grid, hex, out HexCoordinates normalizedHex))
            {
                return false;
            }

            Vector2Int offset = normalizedHex.ToOffset();
            cellIndex = offset.y * grid.Width + offset.x;
            return grid.Cells != null && (uint)cellIndex < (uint)grid.Cells.Length;
        }

        public static bool IsPassable(Grid grid, HexCoordinates hex)
        {
            return TryGetCellIndex(grid, hex, out int cellIndex) &&
                   grid.Cells[cellIndex].TerrainType == TerrainType.Plain;
        }

        public static bool TryGetTileCenterWorld
        (
            Grid grid, DrawMap drawMap, HexCoordinates hex,
            out Vector3 worldPosition
        )
        {
            worldPosition = default;
            if (drawMap?.Tilemap == null ||
                !TryNormalizeHex(grid, hex, out HexCoordinates normalizedHex))
            {
                return false;
            }

            Vector2Int offset = normalizedHex.ToOffset();
            worldPosition =
                drawMap.Tilemap.GetCellCenterWorld(new Vector3Int(offset.x, offset.y, 0));
            return true;
        }

        public static bool TryGetClickedHex
        (
            UnityEngine.Camera camera, Tilemap tilemap, Grid grid,
            Vector2 screenPosition, out HexCoordinates hex, out Vector3Int cell,
            out Vector3 worldPosition
        )
        {
            hex = default;
            cell = default;
            worldPosition = default;
            if (camera == null) return false;
            if (tilemap == null) return false;
            if (grid == null) return false;

            Ray ray = camera.ScreenPointToRay(screenPosition);
            Plane plane = new Plane(tilemap.transform.forward, tilemap.transform.position);
            if (!plane.Raycast(ray, out float distance))
            {
                return false;
            }

            worldPosition = ray.GetPoint(distance);
            Vector3Int rawCell = tilemap.WorldToCell(worldPosition);
            if (!TryNormalizeCell(grid, rawCell, out var realCell))
            {
                return false;
            }

            if (tilemap.GetTile(rawCell) == null)
            {
                Debug.Log("真的有可能");
                return false;
            }

            hex = HexCoordinates.FromOffset(realCell.x, realCell.y);
            cell = rawCell;

            return true;
        }

        public static bool TryFindPath
        (
            Grid grid, UnitOccupancyIndex occupancyIndex, HexCoordinates start,
            HexCoordinates destination, int selfEntityId, List<HexCoordinates> result,
            GameWorld gameWorld, NationIndex nationIndex, DiplomacyIndex diplomacyIndex,
            byte myNationId
        )
        {
            result?.Clear();
            if (grid == null) return false;
            if (result == null) return false;
            if (!TryNormalizeHex(grid, start, out HexCoordinates normalizedStart)) return false;
            if (!TryNormalizeHex(grid, destination, out HexCoordinates normalizedDestination))
                return false;

            if (normalizedStart.Equals(normalizedDestination))
            {
                result.Add(normalizedStart);
                return true;
            }

            if (!IsPassable(grid, normalizedDestination))
            {
                return false;
            }

            var frontier = new Queue<HexCoordinates>();
            var cameFrom = new Dictionary<HexCoordinates, HexCoordinates>(256);
            frontier.Enqueue(normalizedStart);
            cameFrom[normalizedStart] = normalizedStart;

            while (frontier.Count > 0)
            {
                HexCoordinates current = frontier.Dequeue();
                for (int i = 0; i < s_Directions.Length; i++)
                {
                    HexCoordinates next = current.GetNeighbor(s_Directions[i]);
                    if (!TryNormalizeHex(grid, next, out next)) continue;
                    if (cameFrom.ContainsKey(next)) continue;
                    if (!IsPassable(grid, next)) continue;

                    // 获取格子归属信息，用于外交判定。
                    if (!TryGetCellIndex(grid, next, out int cellIndex)) continue;
                    byte hexOwnerId = grid.Cells[cellIndex].OwnerId;

                    // 不允许进入非己方的和平领土。
                    if (myNationId != hexOwnerId && diplomacyIndex.IsPeace(myNationId, hexOwnerId))
                        continue;

                    // 如果有其他单位占据该格，检查是否可通行。
                    if (occupancyIndex.TryGetUnit(next, out int occupantEntityId) &&
                        occupantEntityId != selfEntityId)
                    {
                        CEntity occupantEntity = gameWorld.GetChild(occupantEntityId);
                        if (occupantEntity == null ||
                            !occupantEntity.TryGetUnit(out Unit occupantUnit))
                        {
                            // 实体不存在或没有 Unit 组件，当作不可通行。
                            continue;
                        }

                        // 仅允许朝敌人移动（进攻），不允许朝盟友或己方移动。
                        byte occupantNationId =
                            NationUtility.GetIdOrDefault(nationIndex, occupantUnit.Tag);
                        if (!diplomacyIndex.IsHostile(myNationId, occupantNationId))
                            continue;
                    }

                    cameFrom[next] = current;
                    if (next.Equals(normalizedDestination))
                    {
                        BuildPath(cameFrom, normalizedStart, normalizedDestination, result);
                        return true;
                    }

                    frontier.Enqueue(next);
                }
            }

            return false;
        }


        public static Vector3 GetNearestMirroredWorldPosition
        (
            Tilemap tilemap, Grid grid, HexCoordinates hex,
            Vector3 previousWorldPosition
        )
        {
            Vector2Int offset = hex.ToOffset();
            var baseCell = new Vector3Int(offset.x, offset.y, 0);
            Vector3 best = tilemap.GetCellCenterWorld(baseCell);
            float bestDistance = (best - previousWorldPosition).sqrMagnitude;

            // 无缝 X/Y 镜像：在 baseCell 和 ±Width/Height 的镜像位置中选最近者，
            // 让单位在鬼列间移动时视觉位置跟随最近的副本。
            bool canMirrorX = grid != null && grid.EnableSeamlessX && grid.Width > 0;
            if (canMirrorX)
            {
                EvaluateMirroredCell(tilemap, baseCell + new Vector3Int(-grid.Width, 0, 0),
                    previousWorldPosition, ref best, ref bestDistance);
                EvaluateMirroredCell(tilemap, baseCell + new Vector3Int(grid.Width, 0, 0),
                    previousWorldPosition, ref best, ref bestDistance);
            }

            bool canMirrorY = grid != null && grid.EnableSeamlessY && grid.Height > 0;
            if (canMirrorY)
            {
                EvaluateMirroredCell(tilemap, baseCell + new Vector3Int(0, -grid.Height, 0),
                    previousWorldPosition, ref best, ref bestDistance);
                EvaluateMirroredCell(tilemap, baseCell + new Vector3Int(0, grid.Height, 0),
                    previousWorldPosition, ref best, ref bestDistance);
            }

            return best;
        }

        public static int WrapIndex(int value, int max)
        {
            if (max <= 0)
            {
                return 0;
            }

            int wrapped = value % max;
            return wrapped < 0 ? wrapped + max : wrapped;
        }

        private static void BuildPath
        (
            Dictionary<HexCoordinates, HexCoordinates> cameFrom, HexCoordinates start,
            HexCoordinates destination, List<HexCoordinates> result
        )
        {
            result.Clear();
            HexCoordinates current = destination;
            result.Add(current);
            while (!current.Equals(start))
            {
                current = cameFrom[current];
                result.Add(current);
            }

            result.Reverse();
        }

        private static void EvaluateMirroredCell
        (
            Tilemap tilemap, Vector3Int cell, Vector3 previousWorldPosition,
            ref Vector3 best, ref float bestDistance
        )
        {
            Vector3 candidate = tilemap.GetCellCenterWorld(cell);
            float distance = (candidate - previousWorldPosition).sqrMagnitude;
            if (distance >= bestDistance)
            {
                return;
            }

            best = candidate;
            bestDistance = distance;
        }
    }
}