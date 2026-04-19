#region

using Core.Capability;
using GamePlay.Map;

#endregion

namespace GamePlay.Strategy
{
    public class ChangedHexs : CComponent
    {
        public HexCoordinates[] Hexs;

        public override void Dispose()
        {
            Hexs = null;
            base.Dispose();
        }
    }
}