#region

using Core.Capability;
using GamePlay.Util;
using GamePlay.World;
using System.Collections.Generic;
using UnityEngine;

#endregion

namespace GamePlay.Strategy
{
    // 士气恢复能力：世界级每日扫描所有 SignalRecover 单位。
    public class CpCombatRecovery : CapabilityBase
    {
        private static readonly int m_SignalRecoverId = Component<SignalRecover>.TId;
        private int m_LastRecoveryDayVersion = -1;
        private readonly List<CEntity> m_Entities = new List<CEntity>(128);

        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.ResolveCombatRecovery;

        public override string Pipeline => CapabilityPipeline.Combat;

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
            context.QuerySnapshot<Unit, UnitCombat, SignalRecover>(m_Entities);
            for (int i = 0; i < m_Entities.Count; i++)
            {
                Recover(context, m_Entities[i]);
            }
        }

        private static void Recover(CapabilityContext context, CEntity entity)
        {
            if (entity == null) return;
            if (entity.HasCombatState()) return;
            if (!entity.TryGetUnitCombat(out UnitCombat combat)) return;

            combat.Morale = Mathf.Min(combat.MaxMorale,
                combat.Morale + combat.MoraleRecovery);
            if (combat.Morale >= combat.MaxMorale)
            {
                context.Commands.RemoveComponent(entity, m_SignalRecoverId);
            }

            context.MarkWorked();
        }
    }
}
