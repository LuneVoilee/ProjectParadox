#region

using System;
using Common.Event;
using Core.Capability;
using GamePlay.Util;
using GamePlay.World;
using UnityEngine;

#endregion

namespace GamePlay.Strategy
{
    // 士气恢复能力：当单位不处于战斗中时，每日按 MoraleRecovery 恢复士气。
    public class CombatRecoveryCap : CapabilityBase
    {
        private static readonly int m_UnitId = Component<Unit>.TId;
        private static readonly int m_UnitCombatId = Component<UnitCombat>.TId;

        private Action<DateTime> m_OnTimeChange;
        private int m_LastRecoveryDay = -1;

        // 在战斗结算之后执行，确保当天战斗已处理完毕。
        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.ResolveCombatRecovery;

        protected override void OnInit()
        {
            Filter(m_UnitId, m_UnitCombatId);
        }

        public override bool ShouldActivate()
        {
            if (!Owner.HasComponent(m_UnitId)) return false;

            if (!Owner.TryGetComponent(m_UnitCombatId, out UnitCombat combat)) return false;

            return combat.Morale < combat.MaxMorale;
        }

        public override bool ShouldDeactivate()
        {
            return !ShouldActivate();
        }

        protected override void OnActivated()
        {
            m_OnTimeChange = OnDayChanged;
            EventBus.GP_OnTimeChange += m_OnTimeChange;
            m_LastRecoveryDay = -1;
        }

        protected override void OnDeactivated()
        {
            if (m_OnTimeChange != null)
            {
                EventBus.GP_OnTimeChange -= m_OnTimeChange;
                m_OnTimeChange = null;
            }
        }

        private void OnDayChanged(DateTime currentDate)
        {
            int today = currentDate.DayOfYear + currentDate.Year * 1000;
            if (today == m_LastRecoveryDay) return;
            m_LastRecoveryDay = today;

            // 仅在非战斗状态下恢复士气。
            if (Owner.HasCombatState()) return;
            if (!Owner.TryGetUnitCombat(out UnitCombat combat)) return;

            combat.Morale = Mathf.Min(combat.MaxMorale, combat.Morale + combat.MoraleRecovery);
        }
    }
}