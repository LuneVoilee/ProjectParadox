#region

using Core.Capability;
using GamePlay.Util;
using GamePlay.World;
using System.Collections.Generic;

#endregion

namespace GamePlay.Strategy
{
    // 外交注册能力：当 DiplomacyBootstrap 和 DiplomacyIndex 组件就位时激活，
    // 初始化外交关系矩阵（默认全为 Peace），完成后移除 DiplomacyBootstrap 标记。
    public class CpDiplomacyRegistry : CapabilityBase
    {
        private static readonly int m_BootstrapId = Component<DiplomacyBootstrap>.TId;
        private static readonly int m_DiplomacyIndexId = Component<DiplomacyIndex>.TId;
        private readonly List<CEntity> m_Entities = new List<CEntity>(2);

        // 晚于 CpNationRegistry(ScenarioBootstrap+20) 执行，确保 NationIndex 已就绪。
        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.StageScenarioBootstrap + 30;

        public override string Pipeline => CapabilityPipeline.ScenarioSetup;

        public override void Tick
            (CapabilityContext context, float deltaTime, float realElapsedSeconds)
        {
            context.QuerySnapshotByIds(m_Entities, m_BootstrapId, m_DiplomacyIndexId);
            for (int i = 0; i < m_Entities.Count; i++)
            {
                RegisterOne(context, m_Entities[i]);
            }
        }

        private void RegisterOne(CapabilityContext context, CEntity entity)
        {
            if (World is not GameWorld gameWorld) return;
            if (!gameWorld.TryGetPrimaryMapEntity(out CEntity mapEntity)) return;
            if (entity.Id != mapEntity.Id) return;
            if (!mapEntity.TryGetDiplomacyIndex(out DiplomacyIndex diplomacyIndex)) return;
            if (!mapEntity.TryGetNationIndex(out NationIndex nationIndex)) return;

            // NationIndex 中的有效国家 id 从 1 开始，
            // 临时让所有国家互相敌对
            for (int a = 1; a < NationIndex.Capacity; a++)
            {
                if (string.IsNullOrEmpty(nationIndex.TagById[a])) continue;
                for (int b = a + 1; b < NationIndex.Capacity; b++)
                {
                    if (string.IsNullOrEmpty(nationIndex.TagById[b])) continue;
                    diplomacyIndex.SetRelation((byte)a, (byte)b, DiplomacyStatus.War);
                }
            }

            // 0 为 Neutral 没有外交状态，固定与所有人为敌。
            for (int a = 1; a < NationIndex.Capacity; a++)
            {
                diplomacyIndex.SetRelation((byte)a, 0, DiplomacyStatus.War);
            }

            // 移除标记，该能力退回非激活状态。
            context.Commands.RemoveComponent(entity, m_BootstrapId);
        }
    }
}
