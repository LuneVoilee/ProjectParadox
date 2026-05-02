#region

using System.Collections.Generic;
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
    public class CpCombatResolve : CapabilityBase
    {
        private static readonly int m_CombatStateId = Component<CombatState>.TId;
        private static readonly int m_MoveTargetId = Component<UnitMoveTarget>.TId;
        private int m_LastProcessedDayVersion = -1;
        private readonly List<CEntity> m_Entities = new List<CEntity>(128);
        private readonly HashSet<int> m_ProcessedEntities = new HashSet<int>();

        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.ResolveCombatResolve;

        public override string Pipeline => CapabilityPipeline.Combat;

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

        // ── 每日单对战斗结算 ────────────────────────────────────────

        private void ProcessDailyCombat
            (CapabilityContext context, StrategyMapContext mapContext, CEntity entity)
        {
            if (entity == null) return;
            if (!entity.TryGetUnit(out Unit unit)) return;
            if (!entity.TryGetUnitCombat(out UnitCombat myCombat)) return;
            if (!entity.TryGetCombatState(out CombatState combatState)) return;
            m_ProcessedEntities.Add(entity.Id);
            m_ProcessedEntities.Add(combatState.OpponentEntityId);

            if (!ResolveOpponent(context, mapContext, entity, combatState, unit,
                    out CEntity opponentEntity, out UnitCombat opponentCombat,
                    out byte myId, out byte opponentId))
            {
                return;
            }

            ApplyDamage(myCombat, opponentCombat);

            ResolveOutcome(context, mapContext, entity, opponentEntity,
                opponentCombat, opponentId);
        }

        // ── 对手解析 ────────────────────────────────────────────────

        /// <summary>
        ///     解析对手实体并验证组件完整性、外交关系。
        ///     验证失败时自行清理相关 CombatState。
        /// </summary>
        private static bool ResolveOpponent
        (
            CapabilityContext context, StrategyMapContext mapContext,
            CEntity entity, CombatState combatState, Unit unit,
            out CEntity opponentEntity, out UnitCombat opponentCombat,
            out byte myId, out byte opponentId
        )
        {
            opponentEntity = null;
            opponentCombat = null;
            myId = 0;
            opponentId = 0;

            opponentEntity = mapContext.World.GetChild(combatState.OpponentEntityId);
            if (opponentEntity == null)
            {
                EndCombat(context, entity);
                return false;
            }

            if (!opponentEntity.TryGetUnitCombat(out opponentCombat))
            {
                EndCombat(context, entity);
                return false;
            }

            if (!opponentEntity.TryGetUnit(out Unit opponentUnit))
            {
                EndCombat(context, entity);
                return false;
            }

            myId = NationUtility.GetIdOrDefault(mapContext.NationIndex, unit.Tag);
            opponentId =
                NationUtility.GetIdOrDefault(mapContext.NationIndex, opponentUnit.Tag);

            if (!mapContext.DiplomacyIndex.IsHostile(myId, opponentId))
            {
                EndCombat(context, entity);
                EndCombat(context, opponentEntity);
                return false;
            }

            return true;
        }

        // ── 伤害计算 ────────────────────────────────────────────────

        /// <summary>
        ///     计算并施加攻击方对防御方的伤害（士气 + 生命）。
        /// </summary>
        private static void ApplyDamage(UnitCombat attacker, UnitCombat defender)
        {
            float moraleDamage = Mathf.Max(0f, attacker.Attack - defender.Defense);
            float healthDamage = moraleDamage * 0.1f;

            defender.Morale = Mathf.Max(0f, defender.Morale - moraleDamage);
            defender.Health = Mathf.Max(0f, defender.Health - healthDamage);
        }

        // ── 结果判定 ────────────────────────────────────────────────

        /// <summary>
        ///     根据伤害后对手状态判定战斗结果：击杀、溃退、或双方进入恢复。
        /// </summary>
        private static void ResolveOutcome
        (
            CapabilityContext context, StrategyMapContext mapContext,
            CEntity entity, CEntity opponentEntity, UnitCombat opponentCombat,
            byte opponentId
        )
        {
            // 生命归零 → 直接击杀。
            if (opponentCombat.Health <= 0f)
            {
                KillUnit(context, opponentEntity, mapContext.Occupancy, mapContext.Grid);
                EndCombat(context, entity);
                return;
            }

            // 士气尚存 → 存活，战斗继续。
            if (opponentCombat.Morale > 0f)
            {
                return;
            }

            // 士气崩溃 → 尝试溃退，失败则击杀。
            if (!TryRetreat(context, opponentEntity, mapContext.Grid,
                    mapContext.Occupancy, mapContext.DiplomacyIndex, opponentId))
            {
                KillUnit(context, opponentEntity, mapContext.Occupancy, mapContext.Grid);
            }

            // 双方进入恢复状态，攻击方退出战斗。
            context.Commands.AddComponent<SignalRecover>(opponentEntity);
            context.Commands.AddComponent<SignalRecover>(entity);
            EndCombat(context, entity);
        }

        // ── 溃退 ────────────────────────────────────────────────────

        /// <summary>
        ///     尝试让单位溃退到相邻的友方 / 中立格。
        ///     retreaterId 为溃退方的 nation id。
        /// </summary>
        private static bool TryRetreat
        (
            CapabilityContext context, CEntity entity, Grid grid,
            UnitOccupancyIndex occupancyIndex, DiplomacyIndex diplomacyIndex,
            byte retreaterId
        )
        {
            if (!entity.TryGetUnitPosition(out UnitPosition position)) return false;

            for (int dir = 0; dir < 6; dir++)
            {
                HexCoordinates neighborHex =
                    position.Hex.GetNeighbor((HexDirection)dir);
                if (!HexMapUtility.TryGetCellIndex(grid, neighborHex,
                        out int cellIndex))
                    continue;
                if (!HexMapUtility.IsPassable(grid, neighborHex)) continue;
                if (occupancyIndex.TryGetUnit(neighborHex, out _)) continue;

                Cell cell = grid.Cells[cellIndex];
                byte ownerId = cell.OwnerId;

                if (ownerId != NationIndex.NeutralId &&
                    ownerId != retreaterId &&
                    !diplomacyIndex.IsAllied(retreaterId, ownerId))
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

        // ── 击杀 ────────────────────────────────────────────────────

        private static void KillUnit
        (
            CapabilityContext context, CEntity entity,
            UnitOccupancyIndex occupancyIndex, Grid grid
        )
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

        // ── 结束战斗 ────────────────────────────────────────────────

        private static void EndCombat(CapabilityContext context, CEntity entity)
        {
            context.Commands.RemoveComponent(entity, m_CombatStateId);
        }
    }
}
