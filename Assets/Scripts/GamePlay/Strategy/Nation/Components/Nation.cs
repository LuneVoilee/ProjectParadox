#region

using System;
using System.Collections.Generic;
using Core.Capability;
using Core.Json;
using UnityEngine;

#endregion

namespace GamePlay.Strategy
{
    // 单个国家实体上的运行时数据快照。Id 给地图格子和单位做高频判断，Tag 保留为 JSON 静态数据 key。
    public class Nation : CComponent
    {
        // 运行期压缩 id，0 保留给 Neutral，真实国家由 NationRegistryCap 从 1 开始分配。
        public byte Id;

        // JSON 主键，后续查询国家静态配置、调试输出、UI 显示时都应优先使用它。
        public string Tag;

        // 从 JSON 复制出的展示名、国家色和初始金钱，后续可被其它 Strategy 组件继续消费。
        public string Name;
        public Color NationalColor;
        public float Money;
    }

    // 启动标记组件：Preset 安装它，NationRegistryCap 看到后执行一次 JSON -> 运行时索引转换。
    public class NationBootstrap : CComponent
    {
    }

    // 国家运行时索引表。该组件只保存状态，不负责分配/注册逻辑，所有写入都由 NationRegistryCap 完成。
    public class NationIndex : CComponent
    {
        // byte 可表示 0-255；0 固定为 Neutral，因此最多支持 255 个真实国家。
        public const int Capacity = 256;
        public const byte NeutralId = 0;
        public const string NeutralTag = "NEUTRAL";
        public static readonly Color32 NeutralColor = new Color32(255, 255, 255, 255);

        // id -> Tag，用于校验 id 是否已注册，也方便调试时从格子 owner id 反查国家。
        public string[] TagById = new string[Capacity];

        // id -> 国家实体 id，后续经济/外交/AI 等系统可以从索引跳到具体国家实体。
        public int[] NationEntityIdById = new int[Capacity];

        // id -> 国家颜色，地图绘制只读这张表，不需要触碰 JSON 或国家实体。
        public Color32[] ColorById = new Color32[Capacity];

        // Tag -> id，用于命令、UI、配置引用等以 Tag 为输入的路径快速转成运行时 byte id。
        public Dictionary<string, byte> IdByTag = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        // 标记索引是否已经由 NationRegistryCap 完成初始化，便于其它逻辑做保护性判断。
        public bool IsInitialized;

        public override void Dispose()
        {
            // 释放引用型容器，避免地图实体销毁后旧索引被误用或保留大块数组。
            IdByTag?.Clear();
            TagById = null;
            NationEntityIdById = null;
            ColorById = null;
            IsInitialized = false;
            base.Dispose();
        }
    }

    // 国家 JSON 模板结构，对应 Config://Nation 下的单个 JSON 文件。
    public class NationTemplate
    {
        // 主键必须稳定；NationRegistryCap 会把它标准化后写入 Nation.Tag 和 NationIndex.IdByTag。
        [PrimaryKey] public string Tag;

        // 静态配置字段：注册时复制到 Nation 组件，并把颜色写入 NationIndex.ColorById。
        public string Name;
        public Color NationalColor = Color.white;
        public float Money;
    }

    // 国家模板集合，负责通过项目资源链读取 Config://Nation 下的所有国家 JSON。
    public class NationTemplateSet : JsonTemplateSet<NationTemplateSet, NationTemplate>
    {
        protected override string ConfigDir => "Config://Nation";
    }
}
