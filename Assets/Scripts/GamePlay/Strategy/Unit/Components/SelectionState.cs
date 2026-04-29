#region

using Core.Capability;
using GamePlay.Map;

#endregion

namespace GamePlay.Strategy
{
    // 玩家当前选择状态。作为世界级状态挂在地图实体上，由选择和表现能力共同读写。
    public class SelectionState : CComponent
    {
        public SelectionKind Kind = SelectionKind.None;
        public HexCoordinates SelectedHex;
        public int SelectedUnitEntityId = -1;
        public int IndicatorId = -1;
        public bool IndicatorDirty = true;
    }
}
