#region

using Core.Capability;
using GamePlay.Util;
using GamePlay.World;

#endregion

namespace GamePlay.Strategy
{
    // 外交注册能力：当 DiplomacyBootstrap 和 DiplomacyIndex 组件就位时激活，
    // 初始化外交关系矩阵（默认全为 Peace），完成后移除 DiplomacyBootstrap 标记。
    public class DiplomacyRegistryCap : CapabilityBase
    {
        private static readonly int m_BootstrapId = Component<DiplomacyBootstrap>.TId;
        private static readonly int m_DiplomacyIndexId = Component<DiplomacyIndex>.TId;

        // 晚于 NationRegistryCap(ScenarioBootstrap+20) 执行，确保 NationIndex 已就绪。
        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.StageScenarioBootstrap + 30;

        protected override void OnInit()
        {
            Filter(m_BootstrapId, m_DiplomacyIndexId);
        }

        public override bool ShouldActivate()
        {
            return Owner.HasComponent(m_BootstrapId) &&
                   Owner.HasComponent(m_DiplomacyIndexId);
        }

        public override bool ShouldDeactivate()
        {
            return !ShouldActivate();
        }

        protected override void OnActivated()
        {
            if (World is not GameWorld gameWorld) return;
            if (!gameWorld.TryGetPrimaryMapEntity(out CEntity mapEntity)) return;
            if (Owner.Id != mapEntity.Id) return;
            if (!mapEntity.TryGetDiplomacyIndex(out DiplomacyIndex diplomacyIndex)) return;
            if (!mapEntity.TryGetNationIndex(out NationIndex nationIndex)) return;

            // 初始化所有国家间关系为 Peace（默认值已经是 Peace，这里显式设一遍确保安全）。
            // NationIndex 中的有效国家 id 从 1 开始，0 为 Neutral 不参与外交。
            for (int a = 1; a < NationIndex.Capacity; a++)
            {
                if (string.IsNullOrEmpty(nationIndex.TagById[a])) continue;
                for (int b = a + 1; b < NationIndex.Capacity; b++)
                {
                    if (string.IsNullOrEmpty(nationIndex.TagById[b])) continue;
                    diplomacyIndex.SetRelation((byte)a, (byte)b, DiplomacyStatus.Peace);
                }
            }

            // 移除标记，该能力退回非激活状态。
            Owner.RemoveComponent(m_BootstrapId);
        }
    }
}