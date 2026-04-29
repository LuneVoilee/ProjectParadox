#region

using System.Collections.Generic;
using Core.Capability;

#endregion

namespace GamePlay.Map
{
    // 领土归属变更缓冲。生产者只写请求，CpApplyTerritoryChanges 在 Resolve 阶段统一落盘。
    public class TerritoryOwnershipBuffer : CComponent
    {
        // 本帧实际要应用的变更列表，由 CpApplyTerritoryChanges 从 LatestOwnerByCell 构建。
        public readonly List<OwnershipChange> Changes = new(256);

        // cellIndex -> 最新 ownerId；同一格多次写入时只保留最后一次，避免重复结算。
        public readonly Dictionary<int, byte> LatestOwnerByCell = new(256);

        // 单格归属变更请求。NewOwnerId 必须先经过 CpNationRegistry 校验后才能写入 Cell.OwnerId。
        public struct OwnershipChange
        {
            public int CellIndex;
            public byte NewOwnerId;
        }

        public override void Dispose()
        {
            // 缓冲数据只在地图实体生命周期内有效，销毁时清空避免持有旧格子索引。
            LatestOwnerByCell.Clear();
            Changes.Clear();
            base.Dispose();
        }
    }
}