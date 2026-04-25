#region

using Core;
using Core.Capability;
using GamePlay.Util;
using GamePlay.World;
using UnityEngine;

#endregion

namespace GamePlay.Camera
{
    public class ZoomCap : CapabilityBase
    {
        private static readonly int m_RefId = Component<Ref>.TId;
        private static readonly int m_ZoomId = Component<Zoom>.TId;

        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.PresentationCameraZoom;

        protected override void OnInit()
        {
            Filter(m_RefId, m_ZoomId);
        }

        public override bool ShouldActivate()
        {
            return Owner.HasComponent(m_RefId) &&
                   Owner.HasComponent(m_ZoomId);
        }

        public override bool ShouldDeactivate()
        {
            return !ShouldActivate();
        }

        protected override void OnActivated()
        {
            if (!Owner.TryGetRef(out var refComp) ||
                !Owner.TryGetZoom(out var zoomComp))
            {
                return;
            }

            var camera = refComp.Camera;
            if (camera != null && camera.orthographic)
            {
                zoomComp.LastZoom = camera.orthographicSize;
            }
        }

        public override void TickActive(float deltaTime, float realElapsedSeconds)
        {
            if (!Owner.TryGetRef(out var refComp) ||
                !Owner.TryGetZoom(out var zoomComp) ||
                !zoomComp.EnableZoom)
            {
                return;
            }

            var camera = refComp.Camera;
            if (camera == null || !camera.orthographic)
            {
                return;
            }

            var inputManager = InputManager.Instance;
            var scroll = inputManager != null ? inputManager.ScrollInput : 0f;
            if (Mathf.Abs(scroll) <= Mathf.Epsilon)
            {
                return;
            }

            var targetSize = Mathf.Clamp(camera.orthographicSize - scroll * zoomComp.ZoomSpeed,
                zoomComp.MinZoom, zoomComp.MaxZoom);
            if (Mathf.Abs(targetSize - camera.orthographicSize) <= Mathf.Epsilon)
            {
                return;
            }

            camera.orthographicSize = targetSize;
            if (Mathf.Abs(targetSize - zoomComp.LastZoom) > 0.001f)
            {
                zoomComp.LastZoom = targetSize;
                TryMarkMapDirty(refComp);
            }
        }

        private bool TryMarkMapDirty(Ref refComp)
        {
            if (!TryResolveMapEntity(refComp, out CEntity mapEntity))
            {
                return false;
            }

            return TryMarkMapDirty(mapEntity);
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

        private static bool TryMarkMapDirty(CEntity entity)
        {
            if (entity == null ||
                !entity.TryGetDrawMap(out var drawMap) ||
                drawMap == null)
            {
                return false;
            }

            drawMap.IsDirty = true;
            return true;
        }
    }
}