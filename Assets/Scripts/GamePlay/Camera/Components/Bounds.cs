#region

using System.Numerics;
using Core.Capability;

#endregion

namespace GamePlay.Camera
{
    public class Bounds : CComponent
    {
        public bool IsWrapX = true;
        public bool IsWrapY;
        public bool IsClampY = true;

        public Vector3 m_MapWidthWorld;
    }
}