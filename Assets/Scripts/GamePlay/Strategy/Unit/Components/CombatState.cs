#region

using Core.Capability;

#endregion

namespace GamePlay.Strategy
{
    // 战斗状态组件。该组件存在即表示单位正处于战斗中。
    // 记录对手单位的 entity id，用于每日战斗结算。
    public class CombatState : CComponent
    {
        // 对手单位的 CEntity.Id。
        public int OpponentEntityId;
    }
}
