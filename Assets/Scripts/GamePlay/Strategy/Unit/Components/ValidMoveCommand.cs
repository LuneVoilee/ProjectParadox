#region

using Core.Capability;
using GamePlay.Map;

#endregion

namespace GamePlay.Strategy
{
    // 已通过规则校验的移动命令。Commit 能力把它转换为 UnitMoveTarget。
    public class ValidMoveCommand : CComponent
    {
        public int UnitEntityId = -1;
        public HexCoordinates DestinationHex;
        public HexCoordinates[] Path;
    }
}
