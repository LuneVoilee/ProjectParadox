#region

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
}
