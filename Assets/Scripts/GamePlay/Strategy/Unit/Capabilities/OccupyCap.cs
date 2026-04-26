#region

using Core.Capability;
using GamePlay.Map;
using GamePlay.Strategy;
using GamePlay.Util;
using GamePlay.World;
using UnityEngine;

#endregion

namespace GamePlay.Strategy
{
    // 单位占领 Cap：把单位当前控制的 Hex 列表转换为地图格子的国家归属请求。
    // 它只写 TerritoryOwnershipBuffer，不直接修改 Grid.Cells，权威落盘交给 ApplyTerritoryChangesCap。
    public class OccupyCap : CapabilityBase
    {
        // Unit 提供占领方 NationId，ChangedHexs 提供本单位当前影响/占领的格子集合。
        private static readonly int m_UnitId = Component<Unit>.TId;
        private static readonly int m_HexsId = Component<ChangedHexs>.TId;

        // 占领请求属于规则结算阶段，后续 ApplyTerritoryChangesCap 会在同阶段稍晚顺序消费。
        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.StageResolve;

        protected override void OnInit()
        {
            Filter(m_UnitId, m_HexsId);
        }

        public override bool ShouldActivate()
        {
            return Owner.HasComponent(m_UnitId) &&
                   Owner.HasComponent(m_HexsId);
        }

        public override bool ShouldDeactivate()
        {
            return !ShouldActivate();
        }

        public override void TickActive(float deltaTime, float realElapsedSeconds)
        {
            // 每帧重新取组件和主地图实体，保证不依赖组件字段突变触发 Capability 重新激活。
            if (!Owner.TryGetUnit(out var unit) ||
                !Owner.TryGetChangedHexs(out var changedHexs) ||
                World is not GameWorld gameWorld ||
                !gameWorld.TryGetPrimaryMapEntity(out CEntity mapEntity) ||
                !mapEntity.TryGetGrid(out var grid) ||
                !mapEntity.TryGetTerritoryOwnershipBuffer(out var ownershipBuffer) ||
                !mapEntity.TryGetNationIndex(out var nationIndex))
            {
                return;
            }

            // ChangedHexs 是其它玩法逻辑产出的控制区快照；为空时本帧没有占领请求。
            HexCoordinates[] hexes = changedHexs.Hexs;
            if (hexes == null || hexes.Length == 0)
            {
                return;
            }

            // 地图尺寸异常时不转换坐标，避免 row * width + col 产生无意义 index。
            int width = grid.Width;
            int height = grid.Height;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            // 单位的 NationId 必须存在于 NationIndex；未知 id 降级为 Neutral，占领结果仍保持合法。
            byte ownerId = unit.NationId;
            if (!NationRegistryCap.IsValidNationId(nationIndex, ownerId))
            {
                ownerId = NationIndex.NeutralId;
            }

            // Hex 坐标转 offset 后映射到 Grid.Cells 的线性 index；越界格跳过。
            for (int i = 0; i < hexes.Length; i++)
            {
                HexCoordinates hex = hexes[i];
                Vector2Int offset = hex.ToOffset();
                int col = offset.x;
                int row = offset.y;
                
                /*if ((uint)col >= (uint)width || (uint)row >= (uint)height)
                {
                    continue;
                }*/

                // 同一格被多个来源写入时，缓冲字典保留本帧最后一次写入，统一交给应用阶段处理。
                int cellIndex = row * width + col;
                ownershipBuffer.LatestOwnerByCell[cellIndex] = ownerId;
            }
        }
    }
}
