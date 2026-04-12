#region

using Core.Capability;
using Map.View;
using UnityEngine;

#endregion

namespace GamePlay.Camera
{
    public class Ref : CComponent
    {
        public Transform Target;
        public UnityEngine.Camera Camera;
        public HexMapRenderer MapRenderer;

        public override void Dispose()
        {
            Target = null;
            Camera = null;
            MapRenderer = null;
        }
    }
}