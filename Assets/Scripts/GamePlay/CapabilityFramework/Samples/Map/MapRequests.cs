namespace GamePlay.CapabilityFramework.Samples.Map
{
    /// <summary>
    /// 请求：要求重新生成地图。
    /// 由输入/UI/调试器 Push 到 RequestBuffer，
    /// 由 GenerateMapCapability 消费。
    /// </summary>
    public struct GenerateMapRequest
    {
        public int Seed;
    }

    /// <summary>
    /// 请求：要求重建势力领地。
    /// 由地图生成成功后自动触发，
    /// 也可以由调试按钮主动触发。
    /// </summary>
    public struct RebuildFactionsRequest
    {
    }
}
