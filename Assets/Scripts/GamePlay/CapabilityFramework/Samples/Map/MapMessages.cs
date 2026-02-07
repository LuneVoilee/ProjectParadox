using Map.Components;

namespace GamePlay.CapabilityFramework.Samples.Map
{
    /// <summary>
    /// Entity 内部消息：地图生成完成。
    ///
    /// 用于示范 MessageBus：
    /// - GenerateMapCapability 发布该消息；
    /// - 其它 Capability（例如日志、统计、任务系统）订阅响应。
    /// </summary>
    public readonly struct MapGeneratedMessage
    {
        public MapGeneratedMessage(CGrid grid)
        {
            Grid = grid;
        }

        public CGrid Grid { get; }
    }
}
