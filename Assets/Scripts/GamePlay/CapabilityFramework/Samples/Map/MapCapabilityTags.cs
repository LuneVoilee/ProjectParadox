namespace GamePlay.CapabilityFramework.Samples.Map
{
    /// <summary>
    /// 示例 Tag 常量。
    ///
    /// 建议：
    /// - 用“域.语义”格式命名，便于筛选与调试；
    /// - State.* 表示状态标签；
    /// - Block.* 表示阻塞标签。
    /// </summary>
    public static class MapCapabilityTags
    {
        public const string StateMapGenerating = "State.MapGenerating";
        public const string StateMapReady = "State.MapReady";

        public const string BlockMapMutation = "Block.MapMutation";
    }
}
