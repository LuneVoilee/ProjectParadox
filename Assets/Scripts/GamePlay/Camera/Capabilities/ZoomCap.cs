#region

using Core;
using Core.Capability;
using UnityEngine;

#endregion

namespace GamePlay.Camera
{
    public class ZoomCap : CapabilityBase
    {
        protected override void OnInit()
        {
            Filter(Component<Ref>.TId, Component<Zoom>.TId);
        }

        public override bool ShouldActivate()
        {
            return Owner.HasComponent(Component<Ref>.TId) &&
                   Owner.HasComponent(Component<Zoom>.TId);
        }

        public override bool ShouldDeactivate()
        {
            return !ShouldActivate();
        }

        public override void TickActive(float deltaTime, float realElapsedSeconds)
        {
            var @ref = Owner.GetComponent(Component<Ref>.TId) as Ref;
            var zoom = Owner.GetComponent(Component<Zoom>.TId) as Zoom;

            if (@ref == null || zoom == null)
            {
                return;
            }

            var camera = @ref.Camera;

            if (camera == null || !camera.gameObject.activeInHierarchy || !camera.orthographic)
            {
                return;
            }

            var inputManager = InputManager.Instance;
            var scroll = inputManager != null ? inputManager.ScrollInput : 0f;
            if (Mathf.Abs(scroll) <= Mathf.Epsilon)
            {
                return;
            }

            var targetSize = Mathf.Clamp(camera.orthographicSize - scroll * zoom.ZoomSpeed,
                zoom.MinZoom, zoom.MaxZoom);

            if (Mathf.Abs(targetSize - camera.orthographicSize) <= Mathf.Epsilon)
            {
                return;
            }

            camera.orthographicSize = targetSize;
            if (Mathf.Abs(targetSize - zoom.LastZoom) > 0.001f)
            {
                zoom.LastZoom = targetSize;
                if (@ref.MapRenderer != null)
                {
                    @ref.MapRenderer.RefreshGhostColumns();
                }
            }
        }
    }
}