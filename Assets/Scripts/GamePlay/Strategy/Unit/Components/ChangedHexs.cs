#region

using System.Collections.Generic;
using Core.Capability;
using GamePlay.Map;

#endregion

namespace GamePlay.Strategy
{
    public class ChangedHexs : CComponent
    {
        public List<HexCoordinates> Hexs;

        public override void Dispose()
        {
            Hexs = null;
            base.Dispose();
        }
    }
}