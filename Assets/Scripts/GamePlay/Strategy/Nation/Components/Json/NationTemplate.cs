#region

using Core.Json;
using UnityEngine;

#endregion

namespace GamePlay.Strategy
{
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
}
