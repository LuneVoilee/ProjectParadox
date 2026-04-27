#region

using Core.Capability;
using GamePlay.Map;
using UnityEngine;

#endregion

namespace GamePlay.Strategy
{
    // 单位当前所在格。Hex 用于规则判断，Cell 用于 tilemap 世界坐标转换。
    public class UnitPosition : CComponent
    {
        public HexCoordinates Hex;
        public Vector3Int Cell;
    }
}
