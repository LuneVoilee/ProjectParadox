#region

using Core.Capability;

#endregion

namespace GamePlay.Strategy
{
    // 单位战斗属性组件，记录士气、生命、攻击、防御等数值。
    public class UnitCombat : CComponent
    {
        // 最大士气值，创建时与当前士气设为相同。
        public float MaxMorale = 100f;

        // 当前士气值。士气耗尽时触发撤退。
        public float Morale = 100f;

        // 最大生命值。
        public float MaxHealth = 100f;

        // 当前生命值。生命耗尽时单位死亡销毁。
        public float Health = 100f;

        // 攻击力，影响每日造成的伤害。
        public float Attack = 10f;

        // 防御值，降低敌方每日造成的伤害。
        public float Defense = 5f;

        // 每日非战斗状态下的士气恢复量。
        public float MoraleRecovery = 5f;
    }
}
