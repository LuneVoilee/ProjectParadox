#region

using Core.Capability;

#endregion

namespace GamePlay.Map
{
    public class Noise : CComponent
    {
        public int Seed = 0;
        public float HeightScale = 0.08f;

        public int MinOffset = -100000;
        public int MaxOffset = 100000;
        public float MinNoiseScale = 0.0001f;
    }
}