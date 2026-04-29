#region

using Core.Capability;
using GamePlay.Map;
using GamePlay.Util;
using GamePlay.World;
using System.Collections.Generic;
using UnityEngine;
using Grid = GamePlay.Map.Grid;

#endregion

namespace GamePlay.Strategy
{
    // 战斗结算能力：世界级每日扫描所有 CombatState 单位。
    public class CpCombatResolve : CapabilityBase
    {
        private static readonly int m_CombatStateId = Component<CombatState>.TId;
        private static readonly int m_MoveTargetId = Component<UnitMoveTarget>.TId;
        private int m_LastProcessedDayVersion = -1;
        private readonly List<CEntity> m_Entities = new List<CEntity>(128);
        private readonly HashSet<int> m_ProcessedEntities = new HashSet<int>();

        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.ResolveCombatResolve;

        public override string DebugCategory => CapabilityDebugCategory.Combat;

        public override void Tick
            (CapabilityContext context, float deltaTime, float realElapsedSeconds)
        {
            if (!StrategyMapContext.TryCreate(context.World, out StrategyMapContext mapContext))
            {
                return;
            }

            if (!mapContext.MapEntity.TryGetTime(out Time time))
            {
                return;
            }

            if (time.DayVersion == m_LastProcessedDayVersion)
            {
                return;
            }

            m_LastProcessedDayVersion = time.DayVersion;
            context.QuerySnapshot<Unit, UnitCombat, CombatState>(m_Entities);
            m_ProcessedEntities.Clear();
            for (int i = 0; i < m_Entities.Count; i++)
            {
                CEntity entity = m_Entities[i];
                if (entity == null) continue;
                if (m_ProcessedEntities.Contains(entity.Id)) continue;
                ProcessDailyCombat(context, mapContext, entity);
            }
        }

        private void ProcessDailyCombat
            (CapabilityContext context, StrategyMapContext mapContext, CEntity entity)
        {
            if (entity == null) return;
            if (!entity.TryGetUnit(out Unit unit)) return;
            if (!entity.TryGetUnitCombat(out UnitCombat myCombat)) return;
            if (!entity.TryGetCombatState(out CombatState combatState)) return;
            m_ProcessedEntities.Add(entity.Id);
            m_ProcessedEntities.Add(combatState.OpponentEntityId);

            CEntity opponentEntity = mapContext.World.GetChild(combatState.OpponentEntityId);
            if (opponentEntity == null)
            {
                EndCombat(context, entity);
                return;
            }

            if (!opponentEntity.TryGetUnitCombat(out UnitCombat opponentCombat))
            {
                EndCombat(context, entity);
                return;
            }

            if (!opponentEntity.TryGetUnit(out Unit opponentUnit))
            {
                EndCombat(context, entity);
                return;
            }

            byte myId = NationUtility.GetIdOrDefault(mapContext.NationIndex, unit.Tag);
            byte opponentId =
                NationUtility.GetIdOrDefault(mapContext.NationIndex, opponentUnit.Tag);

            if (!mapContext.DiplomacyIndex.IsHostile(myId, opponentId))
            {
                EndCombat(context, entity);
                EndCombat(context, opponentEntity);
                return;
            }

            float moraleDamage = Mathf.Max(0f, myCombat.Attack - opponentCombat.Defense);
            float healthDamage = moraleDamage * 0.1f;

            opponentCombat.Morale = Mathf.Max(0f, opponentCombat.Morale - moraleDamage);
            opponentCombat.Health = Mathf.Max(0f, opponentCombat.Health - healthDamage);

            if (opponentCombat.Health <= 0f)
            {
                KillUnit(context, opponentEntity, mapContext.Occupancy, mapContext.Grid);
                EndCombat(context, entity);
                return;
            }

            if (opponentCombat.Morale > 0f)
            {
                return;
            }

            if (!TryRetreat(context, opponentEntity, mapContext.Grid, mapContext.Occupancy,
                    mapContext.DiplomacyIndex, myId))
            {
                KillUnit(context, opponentEntity, mapContext.Occupancy, mapContext.Grid);
            }

            context.Commands.AddComponent<SignalRecover>(opponentEntity);
            context.Commands.AddComponent<SignalRecover>(entity);
            EndCombat(context, entity);
        }

        private static bool TryRetreat
        (
            CapabilityContext context, CEntity entity, Grid grid,
            UnitOccupancyIndex occupancyIndex, DiplomacyIndex diplomacyIndex, byte myId
        )
        {
            if (!entity.TryGetUnitPosition(out UnitPosition position)) return false;

            for (int dir = 0; dir < 6; dir++)
            {
                HexCoordinates neighborHex = position.Hex.GetNeighbor((HexDirection)dir);
                if (!HexMapUtility.TryGetCellIndex(grid, neighborHex, out int cellIndex))
                    continue;
                if (!HexMapUtility.IsPassable(grid, neighborHex)) continue;
                if (occupancyIndex.TryGetUnit(neighborHex, out _)) continue;

                Cell cell = grid.Cells[cellIndex];
                byte ownerId = cell.OwnerId;

                if (ownerId != NationIndex.NeutralId &&
                    ownerId != myId &&
                    !diplomacyIndex.IsAllied(myId, ownerId))
                {
                    continue;
                }

                context.Commands.AddComponent<UnitMoveTarget>(entity, retreatTarget =>
                {
                    retreatTarget.Path = new[] { position.Hex, neighborHex };
                    retreatTarget.DestinationHex = neighborHex;
                    retreatTarget.NextPathIndex = 1;
                    retreatTarget.VisualLerpProgress = 1f;
                });
                EndCombat(context, entity);
                return true;
            }

            return false;
        }

        private static void KillUnit
            (CapabilityContext context, CEntity entity, UnitOccupancyIndex occupancyIndex, Grid grid)
        {
            if (entity.TryGetUnitPosition(out UnitPosition position))
            {
                HexCoordinates hex = position.Hex;
                if (HexMapUtility.TryNormalizeHex(grid, hex, out hex))
                {
                    occupancyIndex.Remove(hex, entity.Id);
                }
            }

            context.Commands.RemoveComponent(entity, m_MoveTargetId);
            context.Commands.AddComponent<DestroyComponent>(entity);
        }

        private static void EndCombat(CapabilityContext context, CEntity entity)
        {
            context.Commands.RemoveComponent(entity, m_CombatStateId);
        }
    }
}
