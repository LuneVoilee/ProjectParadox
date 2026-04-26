#region

using System.Collections.Generic;
using Core.Capability;
using UnityEngine;

#endregion

namespace GamePlay.Map
{
    public struct Cell
    {
        // 六边形逻辑坐标，作为寻路、控制区、邻接关系等系统的稳定坐标。
        public HexCoordinates Coordinates;

        // 地形生成阶段写入的高度采样结果，用于决定 TerrainType。
        public float Height;

        // 地形类型决定底图 tile；国家填色只叠加颜色，不改变这个地形分类。
        public TerrainType TerrainType;

        // 当前格子的国家归属。0 是 Neutral，其余值对应 NationIndex 中的运行时国家 id。
        public byte OwnerId;
    }

    // 生成后的地图权威数据。组件只保存格子数组和尺寸，不负责占领或渲染逻辑。
    public class Grid : CComponent
    {
        // Width/Height 与 Cells 的行列展开规则保持一致：cellIndex = row * Width + col。
        public int Width;
        public int Height;

        // 地图格子线性数组，由 GenerateMapDataCap 创建并由各 Strategy Cap 读取/更新 OwnerId。
        public Cell[] Cells;
    }

    // 领土归属变更缓冲。生产者只写请求，ApplyTerritoryChangesCap 在 Resolve 阶段统一落盘。
    public class TerritoryOwnershipBuffer : CComponent
    {
        // 本帧实际要应用的变更列表，由 ApplyTerritoryChangesCap 从 LatestOwnerByCell 构建。
        public readonly List<OwnershipChange> Changes = new List<OwnershipChange>(256);

        // cellIndex -> 最新 ownerId；同一格多次写入时只保留最后一次，避免重复结算。
        public readonly Dictionary<int, byte> LatestOwnerByCell = new Dictionary<int, byte>(256);

        // 单格归属变更请求。NewOwnerId 必须先经过 NationRegistryCap 校验后才能写入 Cell.OwnerId。
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

    // 国家颜色表现状态。DrawMapCap 负责解释这些字段，组件本身不执行绘制或阈值判断。
    public class TerritoryPaintState : CComponent
    {
        // 需要重刷颜色的格子索引列表；HashSet 用于去重，List 用于保持可遍历集合。
        public readonly List<int> DirtyCellIndices = new List<int>(256);
        public readonly HashSet<int> DirtyCellSet = new HashSet<int>();

        // 全图颜色脏标记。国家表重建、首次绘制、阈值超限时会置 true。
        public bool ColorDirtyAll = true;

        // 每个 cell 的上一次绘制颜色缓存，用于增量重刷时只计算变化格。
        public Color32[] CellColorCache;

        // 增量 dirty 超过任一阈值时退化为全图重刷，避免大量散点刷新比全量更慢。
        public int DirtyToFullThresholdAbs = 4096;
        public float DirtyToFullThresholdRatio = 0.04f;

        public override void Dispose()
        {
            // 清理表现缓存；权威 OwnerId 存在 Grid.Cells 中，不在这里保存。
            DirtyCellSet.Clear();
            DirtyCellIndices.Clear();
            CellColorCache = null;
            base.Dispose();
        }
    }
}
