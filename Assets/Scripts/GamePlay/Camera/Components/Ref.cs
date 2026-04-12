#region

using Core.Capability;
using UnityEngine;

#endregion

namespace GamePlay.Camera
{
    public class Ref : CComponent
    {
        //
        public Transform Target;
        public UnityEngine.Camera Camera;
        public int MapEntityId = -1;

        public override void Dispose()
        {
            Target = null;
            Camera = null;
            MapEntityId = -1;
        }
    }
}
