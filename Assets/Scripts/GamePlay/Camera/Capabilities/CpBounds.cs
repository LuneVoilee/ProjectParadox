#region

using Core.Capability;
using GamePlay.Util;
using GamePlay.World;
using UnityEngine;

#endregion

namespace GamePlay.Camera
{
    public partial class CpBounds : CapabilityBase
    {
        private readonly System.Collections.Generic.List<CEntity> m_Entities =
            new System.Collections.Generic.List<CEntity>(4);

        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.PresentationCameraBounds;

        public override string DebugCategory => CapabilityDebugCategory.Camera;

        public override void Tick
            (CapabilityContext context, float deltaTime, float realElapsedSeconds)
        {
            context.QuerySnapshot<Ref, Bounds>(m_Entities);
            for (int i = 0; i < m_Entities.Count; i++)
            {
                TickOne(context, m_Entities[i]);
            }
        }

        private void TickOne(CapabilityContext context, CEntity entity)
        {
            if (!entity.TryGetRef(out var refComp)) return;
            if (!entity.TryGetBounds(out var boundsComp)) return;
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
            context.MarkWorked();
        }
    }
}
