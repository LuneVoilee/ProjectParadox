#region

using Core.Capability;
using GamePlay.Map;
using NewGamePlay;
using UnityEngine;

#endregion

namespace GamePlay.Camera
{
    public class CameraInstaller : MonoBehaviour
    {
        private static readonly int m_DrawMapId = Component<DrawMap>.TId;

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

            if (m_MapEntityId < 0)
            {
                m_MapEntityId = FindMapEntityId(m_World);
            }

            m_CameraEntity = m_World.AddChild("CameraEntity");
            if (m_CameraEntity == null)
            {
                return;
            }

            m_World.BindCapability<MoveCap>(m_CameraEntity);
            m_World.BindCapability<ZoomCap>(m_CameraEntity);
            m_World.BindCapability<BoundsCap>(m_CameraEntity);

            var refComp = m_CameraEntity.AddComponent<Ref>();
            refComp.Target = m_Target;
            refComp.Camera = m_Camera;
            refComp.MapEntityId = m_MapEntityId;

            var moveComp = m_CameraEntity.AddComponent<Move>();
            moveComp.MoveSpeed = m_MoveSpeed;

            var zoomComp = m_CameraEntity.AddComponent<Zoom>();
            zoomComp.EnableZoom = m_EnableZoom;
            zoomComp.ZoomSpeed = m_ZoomSpeed;
            zoomComp.MinZoom = m_MinZoom;
            zoomComp.MaxZoom = m_MaxZoom;
            zoomComp.LastZoom = ResolveInitialZoom();

            var boundsComp = m_CameraEntity.AddComponent<Bounds>();
            boundsComp.IsWrapX = m_WrapX;
            boundsComp.IsWrapY = m_WrapY;
            boundsComp.IsClampY = m_ClampY;
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

        private static int FindMapEntityId(CapabilityWorld world)
        {
            if (world == null || world.Children == null)
            {
                return -1;
            }

            foreach (var entity in world.Children)
            {
                if (entity != null && entity.HasComponent(m_DrawMapId))
                {
                    return entity.Id;
                }
            }

            return -1;
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