#region

using Core.Capability;
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
}
