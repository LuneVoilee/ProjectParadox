using Core;
using Map.View;
using UnityEngine;

namespace GamePlay.KCamera
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private float m_MoveSpeed = 10f;
        [SerializeField] private Transform m_Target;
        [SerializeField] private Camera m_Camera;

        [Header("Clamp")]
        [SerializeField] private bool m_ClampToMap = true;
        [SerializeField] private HexMapRenderer m_MapRenderer;
        private bool m_HasMapBounds;
        private Bounds m_MapBoundsWorld;
        private BoundsInt m_MapCellBounds;

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

            if (m_MapRenderer == null)
            {
                m_MapRenderer = FindFirstObjectByType<HexMapRenderer>();
            }
        }

        private void Update()
        {
            MoveCamera();

            if (m_ClampToMap)
            {
                ClampToMap();
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

        private void ClampToMap()
        {
            if (m_Camera == null || !m_Camera.orthographic)
            {
                return;
            }

            if (!TryRefreshMapBounds())
            {
                return;
            }

            var position = m_Target.position;
            var halfHeight = m_Camera.orthographicSize;
            var halfWidth = halfHeight * m_Camera.aspect;

            var min = m_MapBoundsWorld.min;
            var max = m_MapBoundsWorld.max;

            var minX = min.x + halfWidth;
            var maxX = max.x - halfWidth;
            if (minX > maxX)
            {
                position.x = (min.x + max.x) * 0.5f;
            }
            else
            {
                position.x = Mathf.Clamp(position.x, minX, maxX);
            }

            var minY = min.y + halfHeight;
            var maxY = max.y - halfHeight;
            if (minY > maxY)
            {
                position.y = (min.y + max.y) * 0.5f;
            }
            else
            {
                position.y = Mathf.Clamp(position.y, minY, maxY);
            }

            m_Target.position = position;
        }

        private bool TryRefreshMapBounds()
        {
            if (m_MapRenderer == null || m_MapRenderer.Tilemap == null)
            {
                return false;
            }

            var tilemap = m_MapRenderer.Tilemap;
            var cellBounds = tilemap.cellBounds;
            if (m_HasMapBounds && cellBounds.Equals(m_MapCellBounds))
            {
                return true;
            }

            m_MapCellBounds = cellBounds;
            var localBounds = tilemap.localBounds;
            var min = tilemap.transform.TransformPoint(localBounds.min);
            var max = tilemap.transform.TransformPoint(localBounds.max);
            m_MapBoundsWorld = new Bounds();
            m_MapBoundsWorld.SetMinMax(min, max);
            m_HasMapBounds = true;
            return true;
        }
    }
}
