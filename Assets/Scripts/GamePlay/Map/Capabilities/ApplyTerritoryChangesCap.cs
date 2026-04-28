#region

using Core.Capability;
using GamePlay.Strategy;
using GamePlay.Util;
using GamePlay.World;

#endregion

namespace GamePlay.Map
{
    // 领土变更应用 Cap：把玩法侧提交的占领请求写入 Grid.Cells，并维护地图填色脏状态。
    public class ApplyTerritoryChangesCap : CapabilityBase
    {
        // 同时依赖格子、请求缓冲、绘制脏状态和国家索引，缺任一项都不应进入结算。
        private static readonly int m_GridId = Component<Grid>.TId;

        private static readonly int m_TerritoryOwnershipBufferId =
            Component<TerritoryOwnershipBuffer>.TId;

        private static readonly int m_TerritoryPaintStateId = Component<TerritoryPaintState>.TId;
        private static readonly int m_NationIndexId = Component<NationIndex>.TId;

        // 放在规则结算阶段，晚于 OccupyCap 写请求，早于表现阶段的 DrawMapCap 读脏格。
        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.StageResolve + 20;

        protected override void OnInit()
        {
            Filter(m_GridId, m_TerritoryOwnershipBufferId, m_TerritoryPaintStateId,
                m_NationIndexId);
        }

        public override bool ShouldActivate()
        {
            if (!Owner.HasComponent(m_GridId)) return false;
            if (!Owner.HasComponent(m_TerritoryOwnershipBufferId)) return false;
            if (!Owner.HasComponent(m_TerritoryPaintStateId)) return false;
            if (!Owner.HasComponent(m_NationIndexId)) return false;
            return true;
        }

        public override bool ShouldDeactivate()
        {
            return !ShouldActivate();
        }

        public override void TickActive(float deltaTime, float realElapsedSeconds)
        {
            // Tick 时重新取组件，避免运行中组件被移除后继续持有旧引用。
            if (!Owner.TryGetGrid(out var grid)) return;
            if (!Owner.TryGetTerritoryOwnershipBuffer(out var ownershipBuffer)) return;
            if (!Owner.TryGetTerritoryPaintState(out var paintState)) return;
            if (!Owner.TryGetNationIndex(out var nationIndex)) return;

            if (grid.Cells == null || grid.Cells.Length == 0)
            {
                // 地图尚未生成或被清理时，丢弃积压请求，避免之后用旧 cellIndex 写入新地图。
                Clear(ownershipBuffer);
                return;
            }

            // 把"同格最后一次写入"的字典快照转换成可遍历列表，本帧只消费这批请求。
            BuildChanges(ownershipBuffer);
            if (ownershipBuffer.Changes.Count == 0)
            {
                return;
            }

            int changedCount = 0;
            for (int i = 0; i < ownershipBuffer.Changes.Count; i++)
            {
                // 过滤越界 cellIndex，防止单位控制区数据和当前地图尺寸不一致时写坏数组。
                TerritoryOwnershipBuffer.OwnershipChange change = ownershipBuffer.Changes[i];
                int cellIndex = change.CellIndex;
                if ((uint)cellIndex >= (uint)grid.Cells.Length)
                {
                    continue;
                }

                byte ownerId = change.NewOwnerId;
                // 未注册国家 id 不允许写进权威格子，统一回退到 Neutral。
                if (!NationUtility.IsValidNationId(nationIndex, ownerId))
                {
                    ownerId = NationIndex.NeutralId;
                }

                ref Cell cell = ref grid.Cells[cellIndex];
                if (cell.OwnerId == ownerId)
                {
                    continue;
                }

                // 只有归属真正变化时才标脏，避免 DrawMapCap 做无意义颜色刷新。
                cell.OwnerId = ownerId;
                MarkDirty(paintState, cellIndex);
                changedCount++;
            }

            // Changes 是本帧消费缓存；LatestOwnerByCell 已在 BuildChanges 中清空。
            ownershipBuffer.Changes.Clear();
            if (changedCount <= 0)
            {
                return;
            }

            // dirty 太多时让表现层全图重刷，比维护大量散点更稳定。
            if (ShouldUseFullRepaint(paintState, grid.Cells.Length))
            {
                paintState.ColorDirtyAll = true;
                ClearDirty(paintState);
            }
        }

        private static void BuildChanges(TerritoryOwnershipBuffer ownershipBuffer)
        {
            // 用字典聚合后再转列表，保证同一格在同一帧多次占领只留下最终 owner。
            ownershipBuffer.Changes.Clear();
            foreach (var pair in ownershipBuffer.LatestOwnerByCell)
            {
                ownershipBuffer.Changes.Add(new TerritoryOwnershipBuffer.OwnershipChange
                {
                    CellIndex = pair.Key,
                    NewOwnerId = pair.Value
                });
            }

            ownershipBuffer.LatestOwnerByCell.Clear();
        }

        private static void Clear(TerritoryOwnershipBuffer ownershipBuffer)
        {
            // 同时清理生产缓冲和消费列表，恢复到无待处理请求状态。
            ownershipBuffer.LatestOwnerByCell.Clear();
            ownershipBuffer.Changes.Clear();
        }

        private static void MarkDirty(TerritoryPaintState paintState, int cellIndex)
        {
            // HashSet 负责去重，List 负责给 DrawMapCap 提供紧凑遍历。
            if (paintState.DirtyCellSet.Add(cellIndex))
            {
                paintState.DirtyCellIndices.Add(cellIndex);
            }
        }

        private static void ClearDirty(TerritoryPaintState paintState)
        {
            // 全图脏时不需要保存单格脏列表，避免 DrawMapCap 误走增量路径。
            paintState.DirtyCellSet.Clear();
            paintState.DirtyCellIndices.Clear();
        }

        private static bool ShouldUseFullRepaint(TerritoryPaintState paintState, int totalCellCount)
        {
            // 外部已经指定全图脏时直接全量刷新。
            if (paintState.ColorDirtyAll) return true;

            // 无有效格子时没有必要触发全量刷新。
            if (totalCellCount <= 0) return false;

            // 绝对数量阈值用于大地图保护，比例阈值用于小地图保护。
            if (paintState.DirtyCellIndices.Count >= paintState.DirtyToFullThresholdAbs) return true;

            float ratio = paintState.DirtyCellIndices.Count / (float)totalCellCount;
            return ratio >= paintState.DirtyToFullThresholdRatio;
        }
    }
}
