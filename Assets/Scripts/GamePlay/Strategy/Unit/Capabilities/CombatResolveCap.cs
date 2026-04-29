#region

using Core.Capability;
using GamePlay.Map;
using GamePlay.Util;
using GamePlay.World;
using UnityEngine;
using Grid = GamePlay.Map.Grid;

#endregion

namespace GamePlay.Strategy
{
    // 战斗结算能力：世界级每日扫描所有 CombatState 单位。
    public class CombatResolveCap : CapabilityBase
    {
        private static readonly int m_CombatStateId = Component<CombatState>.TId;
        private int m_LastProcessedDayVersion = -1;

        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.ResolveCombatResolve;

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
            EntityGroup group = context.Query<Unit, UnitCombat, CombatState>();
            if (group?.EntitiesMap == null)
            {
                return;
            }

            foreach (CEntity entity in group.EntitiesMap)
            {
                ProcessDailyCombat(mapContext, entity);
            }
        }

        private void ProcessDailyCombat(StrategyMapContext mapContext, CEntity entity)
        {
            if (entity == null) return;
            if (!entity.TryGetUnit(out Unit unit)) return;
            if (!entity.TryGetUnitCombat(out UnitCombat myCombat)) return;
            if (!entity.TryGetCombatState(out CombatState combatState)) return;

            CEntity opponentEntity = mapContext.World.GetChild(combatState.OpponentEntityId);
            if (opponentEntity == null)
            {
                EndCombat(entity);
                return;
            }

            if (!opponentEntity.TryGetUnitCombat(out UnitCombat opponentCombat))
            {
                EndCombat(entity);
                return;
            }

            if (!opponentEntity.TryGetUnit(out Unit opponentUnit))
            {
                EndCombat(entity);
                return;
            }

            byte myId = NationUtility.GetIdOrDefault(mapContext.NationIndex, unit.Tag);
            byte opponentId =
                NationUtility.GetIdOrDefault(mapContext.NationIndex, opponentUnit.Tag);

            if (!mapContext.DiplomacyIndex.IsHostile(myId, opponentId))
            {
                EndCombat(entity);
                opponentEntity.RemoveComponent(m_CombatStateId);
                return;
            }

            float moraleDamage = Mathf.Max(0f, myCombat.Attack - opponentCombat.Defense);
            float healthDamage = moraleDamage * 0.1f;

            opponentCombat.Morale = Mathf.Max(0f, opponentCombat.Morale - moraleDamage);
            opponentCombat.Health = Mathf.Max(0f, opponentCombat.Health - healthDamage);

            if (opponentCombat.Health <= 0f)
            {
                KillUnit(opponentEntity, mapContext.Occupancy, mapContext.Grid);
                EndCombat(entity);
                return;
            }

            if (opponentCombat.Morale > 0f)
            {
                return;
            }

            if (!TryRetreat(opponentEntity, mapContext.Grid, mapContext.Occupancy,
                    mapContext.DiplomacyIndex, myId))
            {
                KillUnit(opponentEntity, mapContext.Occupancy, mapContext.Grid);
            }

            opponentEntity.AddComponent<SignalRecover>();
            entity.AddComponent<SignalRecover>();
            EndCombat(entity);
        }

        private static bool TryRetreat
        (
            CEntity entity, Grid grid, UnitOccupancyIndex occupancyIndex,
            DiplomacyIndex diplomacyIndex, byte myId
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

                UnitMoveTarget retreatTarget = entity.AddComponent<UnitMoveTarget>();
                retreatTarget.Path = new[] { position.Hex, neighborHex };
                retreatTarget.DestinationHex = neighborHex;
                retreatTarget.NextPathIndex = 1;
                retreatTarget.VisualLerpProgress = 1f;
                entity.RemoveCombatState();
                return true;
            }

            return false;
        }

        private static void KillUnit(CEntity entity, UnitOccupancyIndex occupancyIndex, Grid grid)
        {
            if (entity.TryGetUnitPosition(out UnitPosition position))
            {
                HexCoordinates hex = position.Hex;
                if (HexMapUtility.TryNormalizeHex(grid, hex, out hex))
                {
                    occupancyIndex.Remove(hex, entity.Id);
                }
            }

            entity.AddComponent<DestroyComponent>();
        }

        private static void EndCombat(CEntity entity)
        {
            entity.RemoveComponent(m_CombatStateId);
        }
    }
}
