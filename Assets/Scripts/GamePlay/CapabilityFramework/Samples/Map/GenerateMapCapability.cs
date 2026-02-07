using GamePlay.CapabilityFramework.Samples.Map;
using Map.Systems;

namespace GamePlay.CapabilityFramework.Samples.Map
{
    /// <summary>
    /// 示例 Capability：生成地图。
    ///
    /// 展示点：
    /// 1) 从 RequestBuffer 消费 GenerateMapRequest；
    /// 2) 通过 Tag 表达“生成中”和“阻塞地图修改”；
    /// 3) 生成成功后发布 Message 并写入后续 Request。
    /// </summary>
    public class GenerateMapCapability : Capability
    {
        private bool m_HasPendingRequest;
        private GenerateMapRequest m_PendingRequest;

        public override CapabilityPhase Phase => CapabilityPhase.TurnLogic;
        public override int Priority => 10;

        public override TagMask GrantedTags => TagMask.From(MapCapabilityTags.StateMapGenerating);
        public override TagMask BlockTags => TagMask.From(MapCapabilityTags.BlockMapMutation);

        protected override bool ShouldActivate()
        {
            if (Requests.TryConsume<GenerateMapRequest>(out var request))
            {
                m_PendingRequest = request;
                m_HasPendingRequest = true;
                return true;
            }

            return false;
        }

        protected override void OnActivated()
        {
            var state = GetState<MapRuntimeState>();
            if (m_HasPendingRequest && m_PendingRequest.Seed != 0)
            {
                state.Seed = m_PendingRequest.Seed;
            }

            var settings = new MapGenerator.Settings
            {
                Width = state.Width,
                Height = state.Height,
                Seed = state.Seed,
                HeightScale = state.HeightScale,
                MoistureScale = state.MoistureScale,
                SeamlessX = state.SeamlessX,
                SeamlessY = state.SeamlessY,
                BiomeSettings = state.BiomeSettings
            };

            state.Grid = MapGenerator.Generate(settings);

            // 地图生成完成后，打上“已就绪”标签。
            Entity.AddPersistentTag(MapCapabilityTags.StateMapReady);

            // 通知其它 Capability（本地消息）。
            Messages.Publish(new MapGeneratedMessage(state.Grid));

            // 触发后续流程（请求链）。
            Requests.Push(new RebuildFactionsRequest());

            m_HasPendingRequest = false;
        }

        protected override bool ShouldDeactivate()
        {
            // 这个 Capability 是“瞬时任务”型：激活后执行一次即退出。
            return true;
        }
    }
}
