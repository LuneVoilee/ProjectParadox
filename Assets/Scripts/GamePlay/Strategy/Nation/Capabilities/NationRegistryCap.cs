#region

using System;
using System.Collections.Generic;
using Core.Capability;
using GamePlay.Map;
using GamePlay.Util;
using GamePlay.World;
using UnityEngine;

#endregion

namespace GamePlay.Strategy
{
    // 国家注册 Capability：负责把 Config://Nation 下的 JSON 模板转换为运行时国家索引。
    // Preset 只安装 NationBootstrap/NationIndex；真正的分配、建实体、写索引都在这里完成。
    public class NationRegistryCap : CapabilityBase
    {
        // 只监听启动标记和索引组件，确保该 Cap 只在主地图实体准备注册国家时激活一次。
        private static readonly int m_NationBootstrapId = Component<NationBootstrap>.TId;
        private static readonly int m_NationIndexId = Component<NationIndex>.TId;

        // 国家注册必须早于占领和地图表现，因此放在场景启动阶段、地图生成之后的预留顺序。
        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.StageScenarioBootstrap + 20;

        protected override void OnInit()
        {
            Filter(m_NationBootstrapId, m_NationIndexId);
        }

        public override bool ShouldActivate()
        {
            return Owner.HasComponent(m_NationBootstrapId) &&
                   Owner.HasComponent(m_NationIndexId);
        }

        public override bool ShouldDeactivate()
        {
            return !ShouldActivate();
        }

        protected override void OnActivated()
        {
            // 注册表只允许写入主地图实体，避免别的实体误装 NationBootstrap 后生成第二套国家索引。
            if (World is not GameWorld gameWorld ||
                !gameWorld.TryGetPrimaryMapEntity(out CEntity mapEntity) ||
                Owner.Id != mapEntity.Id ||
                !mapEntity.TryGetNationIndex(out NationIndex nationIndex))
            {
                return;
            }

            ResetIndex(nationIndex);
            RegisterTemplates(gameWorld, nationIndex);
            MarkTerritoryColorsDirty(mapEntity);

            // 移除 marker，让 Capability 生命周期自然退回非激活状态；国家索引数据继续保留。
            Owner.RemoveComponent(m_NationBootstrapId);
        }

        private void ResetIndex(NationIndex nationIndex)
        {
            // 每次启动注册前重建索引，避免编辑器热重载或重复创建地图时残留旧国家数据。
            EnsureArrays(nationIndex);
            nationIndex.IdByTag ??= new Dictionary<NationTag, byte>();

            Array.Fill(nationIndex.TagById, null);
            Array.Fill(nationIndex.NationEntityIdById, -1);
            Array.Fill(nationIndex.ColorById, NationIndex.NeutralColor);
            nationIndex.IdByTag.Clear();

            nationIndex.TagById[NationIndex.NeutralId] = NationIndex.NeutralTag;
            nationIndex.ColorById[NationIndex.NeutralId] = NationIndex.NeutralColor;
            nationIndex.NationEntityIdById[NationIndex.NeutralId] = -1;
            nationIndex.IsInitialized = true;
        }

        private void EnsureArrays(NationIndex nationIndex)
        {
            // NationIndex 是纯数据组件，数组容量修复由 Cap 负责，保证 Dispose/反序列化后仍可恢复。
            nationIndex.TagById ??= new string[NationIndex.Capacity];
            nationIndex.NationEntityIdById ??= new int[NationIndex.Capacity];
            nationIndex.ColorById ??= new Color32[NationIndex.Capacity];

            if (nationIndex.TagById.Length != NationIndex.Capacity)
            {
                nationIndex.TagById = new string[NationIndex.Capacity];
            }

            if (nationIndex.NationEntityIdById.Length != NationIndex.Capacity)
            {
                nationIndex.NationEntityIdById = new int[NationIndex.Capacity];
            }

            if (nationIndex.ColorById.Length != NationIndex.Capacity)
            {
                nationIndex.ColorById = new Color32[NationIndex.Capacity];
            }
        }

        private void RegisterTemplates(GameWorld gameWorld, NationIndex nationIndex)
        {
            // JsonTemplateSet 会通过 Config://Nation 读取所有国家 JSON，每个模板分配一个运行期 byte id。
            var templates = NationTemplateSet.Instance.AllTemplates;
            if (templates == null || templates.Count == 0)
            {
                return;
            }

            // 0 固定保留给 Neutral；真实国家从 1 开始连续分配，便于直接写入 Cell.OwnerId。
            byte nextId = 1;
            foreach (var pair in templates)
            {
                if (nextId == 0)
                {
                    Debug.LogWarning(
                        "[NationRegistryCap] Nation capacity exceeded; extra templates were ignored.");
                    return;
                }

                NationTemplate template = pair.Value;
                if (template == null ||
                    !TryRegisterTemplate(gameWorld, nationIndex, template, nextId))
                {
                    continue;
                }

                nextId++;
            }
        }

        private bool TryRegisterTemplate
        (
            GameWorld gameWorld, NationIndex nationIndex,
            NationTemplate template, byte nationId
        )
        {
            // 模板 Tag 是国家身份的源头：空 Tag 或重复 Tag 都跳过，防止运行时 id 指向不明确。
            string tag = NationUtility.NormalizeTag(template.Tag);
            if (string.IsNullOrEmpty(tag) || nationIndex.IdByTag.ContainsKey(tag))
            {
                return false;
            }

            // 每个国家仍然拥有独立实体，方便后续挂载经济、外交、AI 等 Strategy 组件。
            CEntity nationEntity = gameWorld.AddChild($"Nation_{tag}");
            if (nationEntity == null)
            {
                return false;
            }

            // Nation 组件保存运行时 id 与 JSON 静态字段的快照；Tag 继续作为后续查配置的 key。
            Nation nation = nationEntity.AddComponent<Nation>();
            nation.Id = nationId;
            nation.Tag = new NationTag(tag);
            nation.Name = string.IsNullOrWhiteSpace(template.Name) ? tag : template.Name;
            nation.NationalColor = template.NationalColor;
            nation.Money = template.Money;

            // 同步写入三类索引：id->tag、id->颜色、tag->id；地图绘制和占领都只读这些表。
            nationIndex.TagById[nationId] = tag;
            nationIndex.ColorById[nationId] = template.NationalColor;
            nationIndex.NationEntityIdById[nationId] = nationEntity.Id;
            nationIndex.IdByTag[tag] = nationId;
            return true;
        }

        private void MarkTerritoryColorsDirty(CEntity mapEntity)
        {
            // 国家颜色表初始化后，已有地图格子的颜色缓存都需要重新计算。
            if (!mapEntity.TryGetTerritoryPaintState(out TerritoryPaintState paintState))
            {
                return;
            }

            paintState.ColorDirtyAll = true;
            paintState.DirtyCellIndices.Clear();
            paintState.DirtyCellSet.Clear();
        }
    }
}
