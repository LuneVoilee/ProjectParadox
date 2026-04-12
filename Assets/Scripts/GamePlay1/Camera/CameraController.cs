#region

using Core;
using Map.View;
using UnityEngine;

#endregion

namespace GamePlay.KCamera
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private float m_MoveSpeed = 10f;
        [SerializeField] private Transform m_Target;
        [SerializeField] private UnityEngine.Camera m_Camera;

        [Header("Zoom")] [SerializeField] private bool m_EnableZoom = true;
        [SerializeField] private float m_ZoomSpeed = 5f;
        [SerializeField] private float m_MinZoom = 4f;
        [SerializeField] private float m_MaxZoom = 20f;

        [Header("Wrap")] [SerializeField] private bool m_WrapX = true;
        [SerializeField] private bool m_WrapY;

        [Header("Clamp")] [SerializeField] private bool m_ClampY = true;
        [SerializeField] private HexMapRenderer m_MapRenderer;
        private bool m_HasMapMetrics;
        private Vector3 m_MapOriginWorld;
        private float m_MapWidthWorld;
        private float m_MapHeightWorld;
        private int m_MapWidth;
        private int m_MapHeight;
        private float m_LastZoom;

        private void Awake()
        {
            if (m_Target == null)
            {
                m_Target = transform;
            }

            if (m_Camera == null)
            {
                m_Camera = GetComponent<Camera>();
                if (m_Camera == null)
                {
                    m_Camera = GetComponentInChildren<Camera>();
                }

                if (m_Camera == null)
                {
                    m_Camera = Camera.main;
                }
            }

            if (m_Camera != null && m_Camera.orthographic)
            {
                m_LastZoom = m_Camera.orthographicSize;
            }

            if (m_MapRenderer == null)
            {
                m_MapRenderer = FindFirstObjectByType<HexMapRenderer>();
            }
        }

        private void Update()
        {
            MoveCamera();
            ApplyZoom();

            if (m_WrapX || m_WrapY || m_ClampY)
            {
                ApplyBounds();
            }
        }

        private void MoveCamera()
        {
            var inputManager = InputManager.Instance;
            var input = inputManager != null ? inputManager.MoveInput : Vector2.zero;
            if (input == Vector2.zero)
            {
                return;
            }

            var delta = new Vector3(input.x, input.y, 0f) * (m_MoveSpeed * Time.deltaTime);
            m_Target.position += delta;
        }

        private void ApplyBounds()
        {
            if (!TryRefreshMapMetrics())
            {
                return;
            }

            var position = m_Target.position;

            var halfHeight = 0f;
            var halfWidth = 0f;
            if (m_Camera != null && m_Camera.orthographic)
            {
                halfHeight = m_Camera.orthographicSize;
                halfWidth = halfHeight * m_Camera.aspect;
            }

            if (m_WrapX && m_MapWidthWorld > Mathf.Epsilon)
            {
                var left = m_MapOriginWorld.x;
                var right = left + m_MapWidthWorld;
                var wrapLeft = left - halfWidth;
                var wrapRight = right + halfWidth;

                if (wrapLeft <= wrapRight)
                {
                    if (position.x < wrapLeft)
                    {
                        position.x += m_MapWidthWorld;
                    }
                    else if (position.x > wrapRight)
                    {
                        position.x -= m_MapWidthWorld;
                    }
                }
                else
                {
                    if (position.x < left)
                    {
                        position.x += m_MapWidthWorld;
                    }
                    else if (position.x >= right)
                    {
                        position.x -= m_MapWidthWorld;
                    }
                }
            }

            if (m_WrapY && m_MapHeightWorld > Mathf.Epsilon)
            {
                var bottom = m_MapOriginWorld.y;
                var top = bottom + m_MapHeightWorld;
                var wrapBottom = bottom - halfHeight;
                var wrapTop = top + halfHeight;

                if (wrapBottom <= wrapTop)
                {
                    if (position.y < wrapBottom)
                    {
                        position.y += m_MapHeightWorld;
                    }
                    else if (position.y > wrapTop)
                    {
                        position.y -= m_MapHeightWorld;
                    }
                }
                else
                {
                    if (position.y < bottom)
                    {
                        position.y += m_MapHeightWorld;
                    }
                    else if (position.y >= top)
                    {
                        position.y -= m_MapHeightWorld;
                    }
                }
            }
            else if (m_ClampY && m_MapHeightWorld > Mathf.Epsilon)
            {
                var minY = m_MapOriginWorld.y + halfHeight;
                var maxY = m_MapOriginWorld.y + m_MapHeightWorld - halfHeight;
                if (minY > maxY)
                {
                    position.y = m_MapOriginWorld.y + m_MapHeightWorld * 0.5f;
                }
                else
                {
                    position.y = Mathf.Clamp(position.y, minY, maxY);
                }
            }

            m_Target.position = position;
        }

        private void ApplyZoom()
        {
            if (!m_EnableZoom)
            {
                return;
            }

            if (m_Camera == null || !m_Camera.orthographic)
            {
                return;
            }

            var inputManager = InputManager.Instance;
            var scroll = inputManager != null ? inputManager.ScrollInput : 0f;
            if (Mathf.Abs(scroll) <= Mathf.Epsilon)
            {
                return;
            }

            var targetSize = Mathf.Clamp(m_Camera.orthographicSize - scroll * m_ZoomSpeed,
                m_MinZoom, m_MaxZoom);
            if (Mathf.Abs(targetSize - m_Camera.orthographicSize) <= Mathf.Epsilon)
            {
                return;
            }

            m_Camera.orthographicSize = targetSize;
            if (Mathf.Abs(targetSize - m_LastZoom) > 0.001f)
            {
                m_LastZoom = targetSize;
                if (m_MapRenderer != null)
                {
                    m_MapRenderer.RefreshGhostColumns();
                }
            }
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