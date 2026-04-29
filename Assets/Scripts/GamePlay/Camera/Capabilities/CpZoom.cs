#region

using Core;
using Core.Capability;
using GamePlay.Util;
using GamePlay.World;
using UnityEngine;

#endregion

namespace GamePlay.Camera
{
    public class CpZoom : CapabilityBase
    {
        private readonly System.Collections.Generic.List<CEntity> m_Entities =
            new System.Collections.Generic.List<CEntity>(4);

        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.PresentationCameraZoom;

        public override string DebugCategory => CapabilityDebugCategory.Camera;

        public override void Tick
            (CapabilityContext context, float deltaTime, float realElapsedSeconds)
        {
            var inputManager = InputManager.Instance;
            var scroll = inputManager != null ? inputManager.ScrollInput : 0f;
            if (Mathf.Abs(scroll) <= Mathf.Epsilon)
            {
                return;
            }

            context.QuerySnapshot<Ref, Zoom>(m_Entities);
            for (int i = 0; i < m_Entities.Count; i++)
            {
                CEntity entity = m_Entities[i];
                if (!entity.TryGetRef(out var refComp)) continue;
                if (!entity.TryGetZoom(out var zoomComp)) continue;
                if (!zoomComp.EnableZoom) continue;

                var camera = refComp.Camera;
                if (camera == null || !camera.orthographic) continue;

                if (Mathf.Abs(zoomComp.LastZoom) <= Mathf.Epsilon)
                {
                    zoomComp.LastZoom = camera.orthographicSize;
                }

                var targetSize = Mathf.Clamp(
                    camera.orthographicSize - scroll * zoomComp.ZoomSpeed,
                    zoomComp.MinZoom, zoomComp.MaxZoom);
                if (Mathf.Abs(targetSize - camera.orthographicSize) <= Mathf.Epsilon)
                {
                    continue;
                }

                camera.orthographicSize = targetSize;
                zoomComp.LastZoom = targetSize;
                TryMarkMapDirty(refComp);
                context.MarkWorked();
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
