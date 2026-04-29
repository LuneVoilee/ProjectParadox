#region

using Core.Capability;
using GamePlay.Map;
using GamePlay.Util;
using GamePlay.World;
using UnityEngine;
using Grid = GamePlay.Map.Grid;

#endregion

namespace GamePlay.Camera
{
    public partial class CpBounds
    {
        private bool TryResolveMapData(Ref refComp, out DrawMap drawMap, out Grid grid)
        {
            drawMap = null;
            grid = null;
            if (!TryResolveMapEntity(refComp, out CEntity mapEntity))
            {
                return false;
            }

            return TryGetMapComponents(mapEntity, out drawMap, out grid);
        }

        private bool TryResolveMapEntity(Ref refComp, out CEntity entity)
        {
            entity = null;
            if (refComp == null || World == null)
            {
                return false;
            }

            if (World is GameWorld gameWorld &&
                gameWorld.TryGetPrimaryMapEntity(out entity))
            {
                refComp.MapEntityId = entity.Id;
                return true;
            }

            if (refComp.MapEntityId < 0)
            {
                return false;
            }

            entity = World.GetChild(refComp.MapEntityId);
            return entity != null;
        }

        private static bool TryGetMapComponents(CEntity entity, out DrawMap drawMap, out Grid grid)
        {
            drawMap = null;
            grid = null;
            if (entity == null ||
                !entity.TryGetDrawMap(out drawMap) ||
                !drawMap.Tilemap ||
                !entity.TryGetGrid(out grid))
            {
                return false;
            }

            return true;
        }

        private static bool TryRefreshMapMetrics(DrawMap drawMap, Grid grid, Bounds boundsComp)
        {
            var tilemap = drawMap.Tilemap;
            if (tilemap == null)
            {
                return false;
            }

            int width = grid.Width;
            int height = grid.Height;
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            if (boundsComp.HasMapMetrics && width == boundsComp.MapWidth &&
                height == boundsComp.MapHeight)
            {
                return true;
            }

            Vector3Int originCell = new Vector3Int(0, 0, 0);
            Vector3Int widthCell = new Vector3Int(width, 0, 0);
            Vector3Int heightCell = new Vector3Int(0, height, 0);
            boundsComp.MapOriginWorld = tilemap.CellToWorld(originCell);
            Vector3 widthWorld = tilemap.CellToWorld(widthCell);
            Vector3 heightWorld = tilemap.CellToWorld(heightCell);

            boundsComp.MapWidth = width;
            boundsComp.MapHeight = height;
            boundsComp.MapWidthWorld = Mathf.Abs(widthWorld.x - boundsComp.MapOriginWorld.x);
            boundsComp.MapHeightWorld = Mathf.Abs(heightWorld.y - boundsComp.MapOriginWorld.y);
            boundsComp.HasMapMetrics = true;
            return true;
        }
    }
}