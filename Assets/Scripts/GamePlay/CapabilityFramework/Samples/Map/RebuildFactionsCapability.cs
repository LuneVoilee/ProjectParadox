using Faction;

namespace GamePlay.CapabilityFramework.Samples.Map
{
    /// <summary>
    /// 示例 Capability：重建势力领地。
    ///
    /// 展示点：
    /// - 通过 RequiredTags 要求地图已就绪；
    /// - 通过 RequestBuffer 消费 RebuildFactionsRequest；
    /// - 不直接依赖 GenerateMapCapability，只依赖 Tag + Request。
    /// </summary>
    public class RebuildFactionsCapability : Capability
    {
        private bool m_Pending;

        public override CapabilityPhase Phase => CapabilityPhase.Resolve;
        public override int Priority => 20;
        public override TagMask RequiredTags => TagMask.From(MapCapabilityTags.StateMapReady);

        protected override bool ShouldActivate()
        {
            if (Requests.TryConsume<RebuildFactionsRequest>(out _))
            {
                m_Pending = true;
                return true;
            }

            return false;
        }

        protected override void OnActivated()
        {
            if (!m_Pending)
            {
                return;
            }

            // 兼容你当前项目：先复用已有 FactionManager。
            var factionManager = FactionManager.Instance;
            if (factionManager != null)
            {
                factionManager.Regenerate();
            }

            m_Pending = false;
        }

        protected override bool ShouldDeactivate()
        {
            return true;
        }
    }
}
