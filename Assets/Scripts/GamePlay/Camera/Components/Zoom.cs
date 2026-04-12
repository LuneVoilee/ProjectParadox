#region

using Core.Capability;

#endregion

namespace GamePlay.Camera
{
    public class Zoom : CComponent
    {
        public bool EnableZoom = true;
        public float ZoomSpeed = 5f;
        public float MinZoom = 4f;
        public float MaxZoom = 20f;
        public float LastZoom;
    }
}