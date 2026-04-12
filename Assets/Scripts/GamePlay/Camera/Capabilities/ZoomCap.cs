#region

using Core;
using Core.Capability;
using GamePlay.Map;
using UnityEngine;

#endregion

namespace GamePlay.Camera
{
    public class ZoomCap : CapabilityBase
    {
        private static readonly int m_RefId = Component<Ref>.TId;
        private static readonly int m_ZoomId = Component<Zoom>.TId;
        private static readonly int m_DrawMapId = Component<DrawMap>.TId;

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
            if (!Owner.TryGetComponent<Ref>(m_RefId, out var refComp) ||
                !Owner.TryGetComponent<Zoom>(m_ZoomId, out var zoomComp))
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
            if (!Owner.TryGetComponent<Ref>(m_RefId, out var refComp) ||
                !Owner.TryGetComponent<Zoom>(m_ZoomId, out var zoomComp) ||
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
            if (World == null || World.Children == null)
            {
                return false;
            }

            if (refComp.MapEntityId >= 0)
            {
                var cachedEntity = World.GetChild(refComp.MapEntityId);
                if (TryMarkMapDirty(cachedEntity))
                {
                    return true;
                }

                refComp.MapEntityId = -1;
            }

            foreach (var entity in World.Children)
            {
                if (!TryMarkMapDirty(entity))
                {
                    continue;
                }

                refComp.MapEntityId = entity.Id;
                return true;
            }

            return false;
        }

        private static bool TryMarkMapDirty(CEntity entity)
        {
            if (entity == null ||
                !entity.TryGetComponent<DrawMap>(m_DrawMapId, out var drawMap) ||
                drawMap == null)
            {
                return false;
            }

            drawMap.IsDirty = true;
            return true;
        }
    }
}
