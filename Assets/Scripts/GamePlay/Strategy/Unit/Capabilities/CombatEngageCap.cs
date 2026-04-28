#region

using Core.Capability;
using GamePlay.Map;
using GamePlay.Util;
using GamePlay.World;

#endregion

namespace GamePlay.Strategy
{
    // 交战检测能力：在移动执行前检查路径下一格是否存在敌方单位。
    // 若检测到敌方单位，双方停止移动并进入战斗状态。
    public class CombatEngageCap : CapabilityBase
    {
        private static readonly int m_UnitId = Component<Unit>.TId;
        private static readonly int m_PositionId = Component<UnitPosition>.TId;
        private static readonly int m_MoveTargetId = Component<UnitMoveTarget>.TId;
        private static readonly int m_UnitCombatId = Component<UnitCombat>.TId;

        // 在 MoveAlongHexPathCap(ResolveUnitMovement = StageResolve) 之前执行，
        // 确保先检测到敌方单位再决定是否继续移动。
        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.ResolveCombatEngage;

        protected override void OnInit()
        {
            Filter(m_UnitId, m_PositionId, m_MoveTargetId, m_UnitCombatId);
        }

        public override bool ShouldActivate()
        {
            if (!Owner.HasComponent(m_UnitId)) return false;
            if (!Owner.HasComponent(m_PositionId)) return false;
            if (!Owner.HasComponent(m_MoveTargetId)) return false;
            if (!Owner.HasComponent(m_UnitCombatId)) return false;
            return true;
        }

        public override bool ShouldDeactivate()
        {
            return !ShouldActivate();
        }

        public override void TickActive(float deltaTime, float realElapsedSeconds)
        {
            // 已处于战斗中则跳过，避免重复触发。
            if (Owner.HasCombatState()) return;
            if (!Owner.TryGetUnit(out Unit unit)) return;
            if (!Owner.TryGetUnitMoveTarget(out UnitMoveTarget target)) return;
            if (target.Path == null) return;
            if (target.Path.Length < 2) return;
            if (target.NextPathIndex >= target.Path.Length) return;
            if (World is not GameWorld gameWorld) return;
            if (!gameWorld.TryGetPrimaryMapEntity(out CEntity mapEntity)) return;
            if (!mapEntity.TryGetUnitOccupancyIndex(out UnitOccupancyIndex occupancyIndex)) return;
            if (!mapEntity.TryGetGrid(out Grid grid)) return;
            if (!mapEntity.TryGetDiplomacyIndex(out DiplomacyIndex diplomacyIndex)) return;

            // 获取路径下一格的坐标和占据情况。
            HexCoordinates nextHex = target.Path[target.NextPathIndex];
            if (!HexMapUtility.TryNormalizeHex(grid, nextHex, out nextHex)) return;
            if (!occupancyIndex.TryGetUnit(nextHex, out int otherEntityId)) return;
            if (otherEntityId == Owner.Id) return;

            // 获取对方实体，确认其所属国家。
            CEntity otherEntity = gameWorld.GetChild(otherEntityId);
            if (otherEntity == null) return;
            if (!otherEntity.TryGetUnit(out Unit otherUnit)) return;
            if (!otherEntity.TryGetUnitCombat(out _)) return;

            // 通过外交索引判断是否敌对。
            if (!diplomacyIndex.IsHostile(unit.NationId, otherUnit.NationId)) return;

            // 若对方已在战斗中，不再重复添加 CombatState。
            if (otherEntity.HasCombatState()) return;

            // 双方进入战斗：添加 CombatState，移除移动目标以停止移动。
            UnitCombat myCombat = Owner.GetUnitCombatOrNull();
            if (myCombat == null) return;

            CombatState myState = Owner.AddComponent<CombatState>();
            myState.OpponentEntityId = otherEntityId;

            CombatState otherState = otherEntity.AddComponent<CombatState>();
            otherState.OpponentEntityId = Owner.Id;

            Owner.RemoveComponent(m_MoveTargetId);
            otherEntity.RemoveComponent(m_MoveTargetId);
        }
    }
}
