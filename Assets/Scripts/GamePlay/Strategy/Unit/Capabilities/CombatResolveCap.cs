#region

using System;
using Common.Event;
using Core.Capability;
using GamePlay.Map;
using GamePlay.Util;
using GamePlay.World;
using UnityEngine;
using Grid = GamePlay.Map.Grid;

#endregion

namespace GamePlay.Strategy
{
    // 战斗结算能力：监听每日变更事件，对对手造成伤害。
    // 每日士气伤害 = max(0, 我方攻击 - 敌方防御)
    // 每日生命伤害 = 士气伤害 × 0.1
    // 士气耗尽时对手撤退，生命耗尽时对手死亡。
    public class CombatResolveCap : CapabilityBase
    {
        private static readonly int m_UnitId = Component<Unit>.TId;
        private static readonly int m_UnitCombatId = Component<UnitCombat>.TId;
        private static readonly int m_CombatStateId = Component<CombatState>.TId;

        private Action<DateTime> m_OnTimeChange;
        private int m_LastProcessedDay = -1;

        // 在占领结算之后执行，确保当天移动已完成。
        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.ResolveCombatResolve;

        protected override void OnInit()
        {
            Filter(m_UnitId, m_UnitCombatId, m_CombatStateId);
        }

        public override bool ShouldActivate()
        {
            if (!Owner.HasComponent(m_UnitId)) return false;
            if (!Owner.HasComponent(m_UnitCombatId)) return false;
            if (!Owner.HasComponent(m_CombatStateId)) return false;
            return true;
        }

        public override bool ShouldDeactivate()
        {
            return !ShouldActivate();
        }

        protected override void OnActivated()
        {
            m_OnTimeChange = OnDayChanged;
            EventBus.GP_OnTimeChange += m_OnTimeChange;
            m_LastProcessedDay = -1;
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
            if (today == m_LastProcessedDay) return;
            m_LastProcessedDay = today;

            ProcessDailyCombat();
        }

        private void ProcessDailyCombat()
        {
            if (World is not GameWorld gameWorld) return;
            if (!Owner.TryGetUnit(out Unit unit)) return;
            if (!Owner.TryGetUnitCombat(out UnitCombat myCombat)) return;
            if (!Owner.TryGetCombatState(out CombatState combatState)) return;
            if (!gameWorld.TryGetPrimaryMapEntity(out CEntity mapEntity)) return;
            if (!mapEntity.TryGetDiplomacyIndex(out DiplomacyIndex diplomacyIndex)) return;
            if (!mapEntity.TryGetUnitOccupancyIndex(out UnitOccupancyIndex occupancyIndex)) return;
            if (!mapEntity.TryGetGrid(out Grid grid)) return;
            if (!mapEntity.TryGetNationIndex(out NationIndex nationIndex)) return;

            // 获取对手实体和战斗组件。
            CEntity opponentEntity = gameWorld.GetChild(combatState.OpponentEntityId);
            if (opponentEntity == null)
            {
                EndCombat();
                return;
            }

            if (!opponentEntity.TryGetUnitCombat(out UnitCombat opponentCombat))
            {
                EndCombat();
                return;
            }

            if (!opponentEntity.TryGetUnit(out Unit opponentUnit))
            {
                EndCombat();
                return;
            }

            // 将双方 Tag 解析为运行时 id，后续外交查询和格子归属比较都使用解析后的 id。
            byte myId = NationRegistryCap.GetIdOrDefault(nationIndex, unit.Tag);
            byte opponentId = NationRegistryCap.GetIdOrDefault(nationIndex, opponentUnit.Tag);

            // 若外交关系已变为非敌对，解除交战状态。
            if (!diplomacyIndex.IsHostile(myId, opponentId))
            {
                EndCombat();
                opponentEntity.RemoveComponent(m_CombatStateId);
                return;
            }

            // 计算我方对敌方造成的伤害。
            float moraleDamage = Mathf.Max(0f, myCombat.Attack - opponentCombat.Defense);
            float healthDamage = moraleDamage * 0.1f;

            // 对对手应用伤害。
            opponentCombat.Morale = Mathf.Max(0f, opponentCombat.Morale - moraleDamage);
            opponentCombat.Health = Mathf.Max(0f, opponentCombat.Health - healthDamage);

            // 检查对手生命耗尽 → 死亡。
            if (opponentCombat.Health <= 0f)
            {
                KillUnit(opponentEntity, occupancyIndex, grid);
                EndCombat();
                return;
            }

            // 检查对手士气耗尽 → 撤退。
            if (opponentCombat.Morale <= 0f)
            {
                if (!TryRetreat(opponentEntity, opponentUnit, opponentCombat,
                        grid, occupancyIndex, diplomacyIndex, mapEntity))
                {
                    KillUnit(opponentEntity, occupancyIndex, grid);
                }

                EndCombat();
            }
        }

        // 尝试让单位撤退至相邻的安全格。返回 true 表示撤退成功。
        private bool TryRetreat
        (
            CEntity entity, Unit unit, UnitCombat combat,
            Grid grid, UnitOccupancyIndex occupancyIndex, DiplomacyIndex diplomacyIndex,
            CEntity mapEntity
        )
        {
            if (!entity.TryGetUnitPosition(out UnitPosition position)) return false;

            // 遍历六邻格寻找合法撤退格。
            for (int dir = 0; dir < 6; dir++)
            {
                HexCoordinates neighborHex = position.Hex.GetNeighbor((HexDirection)dir);
                if (!HexMapUtility.TryGetCellIndex(grid, neighborHex, out int cellIndex)) continue;
                if (!HexMapUtility.IsPassable(grid, neighborHex)) continue;
                if (occupancyIndex.TryGetUnit(neighborHex, out _)) continue;

                // 获取格子的归属国。
                Cell cell = grid.Cells[cellIndex];
                byte ownerId = cell.OwnerId;

                // 中立格和本国格允许撤退。
                if (ownerId == NationIndex.NeutralId) goto occupyRetreatHex;
                if (ownerId == myId) goto occupyRetreatHex;

                // 仅允许撤退到联盟国格子上，盟友格子也视为安全。
                if (diplomacyIndex.IsAllied(myId, ownerId)) goto occupyRetreatHex;

                // 其余国家（敌对、和平）均不允许撤退。
                continue;

                occupyRetreatHex:

                // 找到合法撤退格，移动单位。
                occupancyIndex.Remove(position.Hex, entity.Id);
                position.Hex = neighborHex;
                Vector2Int offset = neighborHex.ToOffset();
                position.Cell = new Vector3Int(offset.x, offset.y, 0);
                occupancyIndex.Set(neighborHex, entity.Id);

                // 撤退后退出战斗。
                entity.RemoveCombatState();
                return true;
            }

            return false;
        }

        // 销毁单位实体并清理相关状态。
        private void KillUnit(CEntity entity, UnitOccupancyIndex occupancyIndex, Grid grid)
        {
            if (entity.TryGetUnitPosition(out UnitPosition position))
            {
                HexCoordinates hex = position.Hex;
                if (HexMapUtility.TryNormalizeHex(grid, hex, out hex))
                {
                    occupancyIndex.Remove(hex, entity.Id);
                }
            }

            // 清除对手的 CombatState（如果对手仍然存在且指向该单位）。
            if (entity.TryGetCombatState(out CombatState deadUnitState))
            {
                CEntity opponent = World.GetChild(deadUnitState.OpponentEntityId);
                if (opponent != null)
                {
                    opponent.RemoveCombatState();
                }
            }

            entity.RemoveCombatState();
            World.RemoveChild(entity);
        }

        // 结束自身战斗状态。
        private void EndCombat()
        {
            Owner.RemoveComponent(m_CombatStateId);
        }
    }
}