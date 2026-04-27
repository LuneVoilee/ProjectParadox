#region

using System;
using Core.Capability;
using GamePlay.Map;

#endregion

namespace GamePlay.Strategy
{
    // 单位当前移动命令。路径包含起点和终点，NextPathIndex 指向下一个需要抵达的格。
    public class UnitMoveTarget : CComponent
    {
        public HexCoordinates DestinationHex;
        public HexCoordinates[] Path;
        public int NextPathIndex = 1;
        public int RequestVersion;
        public int PathIndicatorId = -1;

        public override void Dispose()
        {
            Path = Array.Empty<HexCoordinates>();
            PathIndicatorId = -1;
            base.Dispose();
        }
    }
}
