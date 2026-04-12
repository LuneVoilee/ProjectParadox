#region

using Core.Capability;
using NewGamePlay;
using UnityEngine;

#endregion

namespace GamePlay.Camera
{
    public partial class BoundsCap : CapabilityBase
    {
        private static readonly int m_RefId = Component<Ref>.TId;
        private static readonly int m_BoundsId = Component<Bounds>.TId;

        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.PresentationCameraBounds;

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
            if (!Owner.TryGetRef(out var refComp) ||
                !Owner.TryGetBounds(out var boundsComp))
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
    }
}