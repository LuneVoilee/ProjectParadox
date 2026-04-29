#region

using Core.Capability;
using GamePlay.Util;
using GamePlay.World;
using UnityEngine;

#endregion

namespace GamePlay.Strategy
{
    // 士气恢复能力：世界级每日扫描所有 SignalRecover 单位。
    public class CombatRecoveryCap : CapabilityBase
    {
        private static readonly int m_SignalRecoverId = Component<SignalRecover>.TId;
        private int m_LastRecoveryDayVersion = -1;

        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.ResolveCombatRecovery;

        public override void Tick
            (CapabilityContext context, float deltaTime, float realElapsedSeconds)
        {
            if (context.World is not GameWorld gameWorld)
            {
                return;
            }

            if (!gameWorld.TryGetPrimaryMapEntity(out CEntity mapEntity))
            {
                return;
            }

            if (!mapEntity.TryGetTime(out Time time))
            {
                return;
            }

            if (time.DayVersion == m_LastRecoveryDayVersion)
            {
                return;
            }

            m_LastRecoveryDayVersion = time.DayVersion;
            EntityGroup group = context.Query<Unit, UnitCombat, SignalRecover>();
            if (group?.EntitiesMap == null)
            {
                return;
            }

            foreach (CEntity entity in group.EntitiesMap)
            {
                Recover(entity);
            }
        }

        private static void Recover(CEntity entity)
        {
            if (entity == null) return;
            if (entity.HasCombatState()) return;
            if (!entity.TryGetUnitCombat(out UnitCombat combat)) return;

            combat.Morale = Mathf.Min(combat.MaxMorale,
                combat.Morale + combat.MoraleRecovery);
            if (combat.Morale >= combat.MaxMorale)
            {
                entity.RemoveComponent(m_SignalRecoverId);
            }
        }
    }
}
