using Map.Components;
using Map.Settings;
using Map.View;

namespace GamePlay.CapabilityFramework.Samples.Map
{
    /// <summary>
    /// Map 运行时状态（示例）。
    ///
    /// 说明：
    /// - 这里只放 Capability 共享的数据；
    /// - Unity 侧引用（Renderer/Settings）也可以临时放这里，
    ///   之后你可以再拆成更细的 State（如 MapDataState、MapViewState）。
    /// </summary>
    public class MapRuntimeState : State
    {
        public int Width;
        public int Height;
        public int Seed;
        public float HeightScale;
        public float MoistureScale;
        public bool SeamlessX;
        public bool SeamlessY;

        public BiomeSettings BiomeSettings;

        public CGrid Grid;
        public HexMapRenderer HexMapRenderer;
        public TerritoryBorderRenderer TerritoryBorderRenderer;
    }
}
