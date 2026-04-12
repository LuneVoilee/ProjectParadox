#region

using Core.Capability;

#endregion

namespace GamePlay.Map
{
    public class Map : CComponent
    {
        public int Width = 100;
        public int Height = 100;

        public bool EnableSeamlessX = true;
        public bool EnableSeamlessY = false;
    }
}