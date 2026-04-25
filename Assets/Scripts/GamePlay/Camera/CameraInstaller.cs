#region

using Core.Capability;
using GamePlay.World;
using UnityEngine;

#endregion

namespace GamePlay.Camera
{
    public class CameraInstaller : MonoBehaviour
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

        [Header("Map")] [SerializeField] private int m_MapEntityId = -1;

        private GameWorld m_World;
        private CEntity m_CameraEntity;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            if (m_CameraEntity != null)
            {
                return;
            }

            var gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                return;
            }

            m_World = gameManager.World;
            if (m_World == null)
            {
                return;
            }

            int mapEntityId = m_MapEntityId;
            if (mapEntityId < 0 && m_World.TryGetPrimaryMapEntity(out var mapEntity))
            {
                mapEntityId = mapEntity.Id;
            }

            var config = new CameraEntityPreset.Config
            {
                Target = m_Target,
                Camera = m_Camera,
                MapEntityId = mapEntityId,
                MoveSpeed = m_MoveSpeed,
                EnableZoom = m_EnableZoom,
                ZoomSpeed = m_ZoomSpeed,
                MinZoom = m_MinZoom,
                MaxZoom = m_MaxZoom,
                InitialZoom = ResolveInitialZoom(),
                WrapX = m_WrapX,
                WrapY = m_WrapY,
                ClampY = m_ClampY
            };

            m_CameraEntity = CameraEntityPreset.Create(m_World, config);
        }

        private void OnDestroy()
        {
            if (m_World != null && m_World.Children != null && m_CameraEntity != null)
            {
                m_World.RemoveChild(m_CameraEntity);
            }

            m_CameraEntity = null;
            m_World = null;
        }

        private void ResolveReferences()
        {
            if (m_Target == null)
            {
                m_Target = transform;
            }

            if (m_Camera == null)
            {
                m_Camera = GetComponent<UnityEngine.Camera>();
                if (m_Camera == null)
                {
                    m_Camera = GetComponentInChildren<UnityEngine.Camera>();
                }

                if (m_Camera == null)
                {
                    m_Camera = UnityEngine.Camera.main;
                }
            }
        }

        private float ResolveInitialZoom()
        {
            if (m_Camera != null && m_Camera.orthographic)
            {
                return m_Camera.orthographicSize;
            }

            return 0f;
        }
    }
}