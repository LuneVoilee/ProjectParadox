#region

using Core.Capability;
using UnityEngine;

#endregion

namespace GamePlay.Camera
{
    public class BoundsCap : CapabilityBase
    {
        protected override void OnInit()
        {
            Filter(Component<Ref>.TId, Component<Bounds>.TId);
        }

        public override bool ShouldActivate()
        {
            return Owner.HasComponent(Component<Ref>.TId) &&
                   Owner.HasComponent(Component<Bounds>.TId);
        }

        public override bool ShouldDeactivate()
        {
            return !ShouldActivate();
        }

        public override void TickActive(float deltaTime, float realElapsedSeconds)
        {
            if (Owner.GetComponent(Component<Ref>.TId) is not Ref @ref)
            {
                return;
            }

            if (Owner.GetComponent(Component<Bounds>.TId) is not Bounds bounds)
            {
                return;
            }

            var camera = @ref.Camera;
            var target = @ref.Target;
            var isWrapX = bounds.IsWrapX;
            var isWrapY = bounds.IsWrapY;

            if (!TryRefreshMapMetrics())
            {
                return;
            }

            var position = target.position;

            var halfHeight = 0f;
            var halfWidth = 0f;
            if (camera != null && camera.orthographic)
            {
                halfHeight = camera.orthographicSize;
                halfWidth = halfHeight * camera.aspect;
            }

            // X 轴 Wrap (已移除冗余的 else 和无意义的 left/right 局部变量)
            if (isWrapX && m_MapWidthWorld > Mathf.Epsilon)
            {
                var wrapLeft = m_MapOriginWorld.x - halfWidth;
                var wrapRight = m_MapOriginWorld.x + m_MapWidthWorld + halfWidth;

                if (position.x < wrapLeft)
                {
                    position.x += m_MapWidthWorld;
                }
                else if (position.x > wrapRight)
                {
                    position.x -= m_MapWidthWorld;
                }
            }

            // Y 轴 Wrap (已移除冗余的 else)
            if (isWrapY && m_MapHeightWorld > Mathf.Epsilon)
            {
                var wrapBottom = m_MapOriginWorld.y - halfHeight;
                var wrapTop = m_MapOriginWorld.y + m_MapHeightWorld + halfHeight;

                if (position.y < wrapBottom)
                {
                    position.y += m_MapHeightWorld;
                }
                else if (position.y > wrapTop)
                {
                    position.y -= m_MapHeightWorld;
                }
            }
            // Y 轴 Clamp
            else if (m_ClampY && m_MapHeightWorld > Mathf.Epsilon)
            {
                var minY = m_MapOriginWorld.y + halfHeight;
                var maxY = m_MapOriginWorld.y + m_MapHeightWorld - halfHeight;

                if (minY > maxY)
                {
                    // 如果屏幕比地图还高，直接将相机固定在地图中心
                    position.y = m_MapOriginWorld.y + m_MapHeightWorld * 0.5f;
                }
                else
                {
                    position.y = Mathf.Clamp(position.y, minY, maxY);
                }
            }

            target.position = position;
        }

        private bool TryRefreshMapMetrics()
        {
            if (m_MapRenderer == null || m_MapRenderer.Tilemap == null)
            {
                return false;
            }

            var width = m_MapRenderer.MapWidth;
            var height = m_MapRenderer.MapHeight;
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            if (m_HasMapMetrics && width == m_MapWidth && height == m_MapHeight)
            {
                return true;
            }

            var tilemap = m_MapRenderer.Tilemap;
            var originCell = new Vector3Int(0, 0, 0);
            var widthCell = new Vector3Int(width, 0, 0);
            var heightCell = new Vector3Int(0, height, 0);

            m_MapOriginWorld = tilemap.CellToWorld(originCell);
            var widthWorld = tilemap.CellToWorld(widthCell);
            var heightWorld = tilemap.CellToWorld(heightCell);

            m_MapWidth = width;
            m_MapHeight = height;
            m_MapWidthWorld = Mathf.Abs(widthWorld.x - m_MapOriginWorld.x);
            m_MapHeightWorld = Mathf.Abs(heightWorld.y - m_MapOriginWorld.y);
            m_HasMapMetrics = true;
            return true;
        }
    }
}