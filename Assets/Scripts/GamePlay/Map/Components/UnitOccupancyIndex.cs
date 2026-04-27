#region

using System.Collections.Generic;
using Core.Capability;

#endregion

namespace GamePlay.Map
{
    // 地图上的单位占位索引。它是运行时查询缓存，不写入 Cell，避免单位移动污染地形/归属数据。
    public class UnitOccupancyIndex : CComponent
    {
        public readonly Dictionary<HexCoordinates, int> UnitEntityIdByHex =
            new Dictionary<HexCoordinates, int>(256);

        public bool TryGetUnit(HexCoordinates hex, out int unitEntityId)
        {
            return UnitEntityIdByHex.TryGetValue(hex, out unitEntityId);
        }

        public bool IsOccupiedByOther(HexCoordinates hex, int selfEntityId)
        {
            return TryGetUnit(hex, out int unitEntityId) && unitEntityId != selfEntityId;
        }

        public void Set(HexCoordinates hex, int unitEntityId)
        {
            UnitEntityIdByHex[hex] = unitEntityId;
        }

        // 仅当该格确实由当前单位占据时才移除，防止 A 单位误删 B 单位的占位记录。
        public void Remove(HexCoordinates hex, int unitEntityId)
        {
            if (!TryGetUnit(hex, out int existedId)) return;
            if (existedId != unitEntityId) return;
            UnitEntityIdByHex.Remove(hex);
        }

        public override void Dispose()
        {
            UnitEntityIdByHex.Clear();
            base.Dispose();
        }
    }
}
