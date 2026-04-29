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
    // 单位占领 Cap：观察 UnitPosition 变化，将新进入的 Hex 写入 TerritoryOwnershipBuffer。
    // 它只写缓冲，不直接修改 Grid.Cells，权威落盘交给 CpApplyTerritoryChanges。
    public class CpOccupy : CapabilityBase
    {
        private readonly System.Collections.Generic.List<CEntity> m_Entities =
            new System.Collections.Generic.List<CEntity>(128);

        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.ResolveUnitOccupy;

        public override string DebugCategory => CapabilityDebugCategory.Territory;

        public override void Tick
            (CapabilityContext context, float deltaTime, float realElapsedSeconds)
        {
            if (context.World is not GameWorld gameWorld) return;
            if (!gameWorld.TryGetPrimaryMapEntity(out CEntity mapEntity)) return;
            if (!mapEntity.TryGetGrid(out var grid)) return;
            if (!mapEntity.TryGetTerritoryOwnershipBuffer(out var ownershipBuffer)) return;
            if (!mapEntity.TryGetNationIndex(out var nationIndex)) return;

            context.QuerySnapshot<Unit, UnitPosition>(m_Entities);
            for (int i = 0; i < m_Entities.Count; i++)
            {
                TickOne(context, m_Entities[i], grid, ownershipBuffer, nationIndex);
            }
        }

        private static void TickOne
        (
            CapabilityContext context, CEntity entity, Grid grid,
            TerritoryOwnershipBuffer ownershipBuffer, NationIndex nationIndex
        )
        {
            if (!entity.TryGetUnit(out var unit)) return;
            if (!entity.TryGetUnitPosition(out var position)) return;
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
            context.MarkWorked();
        }
    }
}
