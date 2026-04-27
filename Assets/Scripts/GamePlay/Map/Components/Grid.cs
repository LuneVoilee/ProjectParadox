#region

using Core.Capability;

#endregion

namespace GamePlay.Map
{
    // 生成后的地图权威数据。组件只保存格子数组和尺寸，不负责占领或渲染逻辑。
    public class Grid : CComponent
    {
        // Width/Height 与 Cells 的行列展开规则保持一致：cellIndex = row * Width + col。
        public int Width;
        public int Height;
        public bool EnableSeamlessX;
        public bool EnableSeamlessY;

        // 地图格子线性数组，由 GenerateMapDataCap 创建并由各 Strategy Cap 读取/更新 OwnerId。
        public Cell[] Cells;
    }
}
