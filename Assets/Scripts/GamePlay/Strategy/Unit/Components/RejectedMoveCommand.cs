#region

using Core.Capability;
using GamePlay.Map;

#endregion

namespace GamePlay.Strategy
{
    // 被拒绝的移动命令，便于调试器和后续 UI 显示失败原因。
    public class RejectedMoveCommand : CComponent
    {
        public int UnitEntityId = -1;
        public HexCoordinates DestinationHex;
        public MoveRejectReason Reason;
    }
}
