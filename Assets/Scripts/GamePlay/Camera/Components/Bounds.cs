#region

using Core.Capability;
using UnityEngine;

#endregion

namespace GamePlay.Camera
{
    public class Bounds : CComponent
    {
        public bool IsWrapX = true;
        public bool IsWrapY;
        public bool IsClampY = true;

        public bool HasMapMetrics;
        public Vector3 MapOriginWorld;
        public float MapWidthWorld;
        public float MapHeightWorld;
        public int MapWidth;
        public int MapHeight;

        public override void Dispose()
        {
            HasMapMetrics = false;
            MapOriginWorld = Vector3.zero;
            MapWidthWorld = 0f;
            MapHeightWorld = 0f;
            MapWidth = 0;
            MapHeight = 0;
        }
    }
}
