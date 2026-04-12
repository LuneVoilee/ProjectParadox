#region

using Core.Capability;
using GamePlay.Map;
using UnityEngine;
using Grid = GamePlay.Map.Grid;

#endregion

namespace GamePlay.Camera
{
    public class BoundsCap : CapabilityBase
    {
        private static readonly int m_RefId = Component<Ref>.TId;
        private static readonly int m_BoundsId = Component<Bounds>.TId;
        private static readonly int m_DrawMapId = Component<DrawMap>.TId;
        private static readonly int m_GridId = Component<Grid>.TId;

        protected override void OnInit()
        {
            Filter(m_RefId, m_BoundsId);
        }

        public override bool ShouldActivate()
        {
            return Owner.HasComponent(m_RefId) &&
                   Owner.HasComponent(m_BoundsId);
        }

        public override bool ShouldDeactivate()
        {
            return !ShouldActivate();
        }

        public override void TickActive(float deltaTime, float realElapsedSeconds)
        {
            if (!Owner.TryGetComponent<Ref>(m_RefId, out var refComp) ||
                !Owner.TryGetComponent<Bounds>(m_BoundsId, out var boundsComp))
            {
                return;
            }

            var target = refComp.Target;
            if (target == null)
            {
                return;
            }

            if (!boundsComp.IsWrapX && !boundsComp.IsWrapY && !boundsComp.IsClampY)
            {
                return;
            }

            if (!TryResolveMapData(refComp, out var drawMap, out var grid) ||
                !TryRefreshMapMetrics(drawMap, grid, boundsComp))
            {
                return;
            }

            var position = target.position;

            var halfHeight = 0f;
            var halfWidth = 0f;
            var camera = refComp.Camera;
            if (camera != null && camera.orthographic)
            {
                halfHeight = camera.orthographicSize;
                halfWidth = halfHeight * camera.aspect;
            }

            if (boundsComp.IsWrapX && boundsComp.MapWidthWorld > Mathf.Epsilon)
            {
                var left = boundsComp.MapOriginWorld.x;
                var right = left + boundsComp.MapWidthWorld;
                var wrapLeft = left - halfWidth;
                var wrapRight = right + halfWidth;

                if (wrapLeft <= wrapRight)
                {
                    if (position.x < wrapLeft)
                    {
                        position.x += boundsComp.MapWidthWorld;
                    }
                    else if (position.x > wrapRight)
                    {
                        position.x -= boundsComp.MapWidthWorld;
                    }
                }
                else
                {
                    if (position.x < left)
                    {
                        position.x += boundsComp.MapWidthWorld;
                    }
                    else if (position.x >= right)
                    {
                        position.x -= boundsComp.MapWidthWorld;
                    }
                }
            }

            if (boundsComp.IsWrapY && boundsComp.MapHeightWorld > Mathf.Epsilon)
            {
                var bottom = boundsComp.MapOriginWorld.y;
                var top = bottom + boundsComp.MapHeightWorld;
                var wrapBottom = bottom - halfHeight;
                var wrapTop = top + halfHeight;

                if (wrapBottom <= wrapTop)
                {
                    if (position.y < wrapBottom)
                    {
                        position.y += boundsComp.MapHeightWorld;
                    }
                    else if (position.y > wrapTop)
                    {
                        position.y -= boundsComp.MapHeightWorld;
                    }
                }
                else
                {
                    if (position.y < bottom)
                    {
                        position.y += boundsComp.MapHeightWorld;
                    }
                    else if (position.y >= top)
                    {
                        position.y -= boundsComp.MapHeightWorld;
                    }
                }
            }
            else if (boundsComp.IsClampY && boundsComp.MapHeightWorld > Mathf.Epsilon)
            {
                var minY = boundsComp.MapOriginWorld.y + halfHeight;
                var maxY = boundsComp.MapOriginWorld.y + boundsComp.MapHeightWorld - halfHeight;
                if (minY > maxY)
                {
                    position.y = boundsComp.MapOriginWorld.y + boundsComp.MapHeightWorld * 0.5f;
                }
                else
                {
                    position.y = Mathf.Clamp(position.y, minY, maxY);
                }
            }

            target.position = position;
        }

        private bool TryResolveMapData(Ref refComp, out DrawMap drawMap, out Grid grid)
        {
            drawMap = null;
            grid = null;

            if (World == null || World.Children == null)
            {
                return false;
            }

            if (refComp.MapEntityId >= 0)
            {
                var cachedEntity = World.GetChild(refComp.MapEntityId);
                if (TryGetMapComponents(cachedEntity, out drawMap, out grid))
                {
                    return true;
                }

                refComp.MapEntityId = -1;
            }

            foreach (var entity in World.Children)
            {
                if (!TryGetMapComponents(entity, out drawMap, out grid))
                {
                    continue;
                }

                refComp.MapEntityId = entity.Id;
                return true;
            }

            return false;
        }

        private static bool TryGetMapComponents(CEntity entity, out DrawMap drawMap, out Grid grid)
        {
            drawMap = null;
            grid = null;

            if (entity == null ||
                !entity.TryGetComponent(m_DrawMapId, out drawMap) ||
                !drawMap.Tilemap ||
                !entity.TryGetComponent(m_GridId, out grid))
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

            var width = grid.Width;
            var height = grid.Height;
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            if (boundsComp.HasMapMetrics && width == boundsComp.MapWidth &&
                height == boundsComp.MapHeight)
            {
                return true;
            }

            var originCell = new Vector3Int(0, 0, 0);
            var widthCell = new Vector3Int(width, 0, 0);
            var heightCell = new Vector3Int(0, height, 0);

            boundsComp.MapOriginWorld = tilemap.CellToWorld(originCell);
            var widthWorld = tilemap.CellToWorld(widthCell);
            var heightWorld = tilemap.CellToWorld(heightCell);

            boundsComp.MapWidth = width;
            boundsComp.MapHeight = height;
            boundsComp.MapWidthWorld = Mathf.Abs(widthWorld.x - boundsComp.MapOriginWorld.x);
            boundsComp.MapHeightWorld = Mathf.Abs(heightWorld.y - boundsComp.MapOriginWorld.y);
            boundsComp.HasMapMetrics = true;
            return true;
        }
    }
}