#region

using System.Collections.Generic;
using Core.Capability;
using UnityEngine;

#endregion

namespace GamePlay.Map
{
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
