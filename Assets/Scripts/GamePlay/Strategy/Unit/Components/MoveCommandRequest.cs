#region

using Core.Capability;
using GamePlay.Map;

#endregion

namespace GamePlay.Strategy
{
    // 移动命令请求。由右键和当前选择生成，提交前仍未通过规则校验。
    public class MoveCommandRequest : CComponent
    {
        public int UnitEntityId = -1;
        public HexCoordinates DestinationHex;
    }
}
