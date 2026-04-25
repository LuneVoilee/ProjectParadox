#region

using Core.Capability;
using GamePlay.World;
using UnityEngine;

#endregion

namespace GamePlay.Camera
{
    public static class CameraEntityPreset
    {
        public struct Config
        {
            public Transform Target;
            public UnityEngine.Camera Camera;
            public int MapEntityId;
            public float MoveSpeed;
            public bool EnableZoom;
            public float ZoomSpeed;
            public float MinZoom;
            public float MaxZoom;
            public float InitialZoom;
            public bool WrapX;
            public bool WrapY;
            public bool ClampY;
        }

        public static CEntity Create
            (GameWorld world, in Config config, string entityName = "CameraEntity")
        {
            if (world == null)
            {
                return null;
            }

            CEntity entity = world.AddChild(entityName);
            if (entity == null)
            {
                return null;
            }

            world.BindCapability<ZoomCap>(entity);
            world.BindCapability<MoveCap>(entity);
            world.BindCapability<BoundsCap>(entity);

            var refComp = entity.AddComponent<Ref>();
            refComp.Target = config.Target;
            refComp.Camera = config.Camera;
            refComp.MapEntityId = config.MapEntityId;

            var moveComp = entity.AddComponent<Move>();
            moveComp.MoveSpeed = config.MoveSpeed;

            var zoomComp = entity.AddComponent<Zoom>();
            zoomComp.EnableZoom = config.EnableZoom;
            zoomComp.ZoomSpeed = config.ZoomSpeed;
            zoomComp.MinZoom = config.MinZoom;
            zoomComp.MaxZoom = config.MaxZoom;
            zoomComp.LastZoom = config.InitialZoom;

            var boundsComp = entity.AddComponent<Bounds>();
            boundsComp.IsWrapX = config.WrapX;
            boundsComp.IsWrapY = config.WrapY;
            boundsComp.IsClampY = config.ClampY;

            return entity;
        }
    }
}