#region

using Core.Capability;
using GamePlay.Map;
using GamePlay.Util;
using GamePlay.World;
using UnityEngine;

#endregion

namespace GamePlay.Strategy
{
    // 单位占领 Cap：观察 UnitPosition 变化，将新进入的 Hex 写入 TerritoryOwnershipBuffer。
    // 它只写缓冲，不直接修改 Grid.Cells，权威落盘交给 ApplyTerritoryChangesCap。
    public class OccupyCap : CapabilityBase
    {
        private static readonly int m_UnitId = Component<Unit>.TId;
        private static readonly int m_PositionId = Component<UnitPosition>.TId;

        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.ResolveUnitOccupy;

        protected override void OnInit()
        {
            Filter(m_UnitId, m_PositionId);
        }

        public override bool ShouldActivate()
        {
            if (!Owner.HasComponent(m_UnitId)) return false;
            if (!Owner.HasComponent(m_PositionId)) return false;
            return true;
        }

        public override bool ShouldDeactivate()
        {
            return !ShouldActivate();
        }

        public override void TickActive(float deltaTime, float realElapsedSeconds)
        {
            if (!Owner.TryGetUnit(out var unit)) return;
            if (!Owner.TryGetUnitPosition(out var position)) return;
            if (World is not GameWorld gameWorld) return;
            if (!gameWorld.TryGetPrimaryMapEntity(out CEntity mapEntity)) return;
            if (!mapEntity.TryGetGrid(out var grid)) return;
            if (!mapEntity.TryGetTerritoryOwnershipBuffer(out var ownershipBuffer)) return;
            if (!mapEntity.TryGetNationIndex(out var nationIndex)) return;

            int width = grid.Width;
            int height = grid.Height;
            if (width <= 0 || height <= 0) return;

            Vector2Int offset = position.Hex.ToOffset();
            int col = offset.x;
            int row = offset.y;
            if ((uint)col >= (uint)width || (uint)row >= (uint)height) return;

            byte ownerId = NationUtility.GetIdOrDefault(nationIndex, unit.Tag);

            int cellIndex = row * width + col;
            ownershipBuffer.LatestOwnerByCell[cellIndex] = ownerId;
        }
    }
}
