#region

using Core.Capability;

#endregion

namespace GamePlay.Strategy
{
    // 单位所属国家数据。Tag 是唯一的国家身份标识，需要 byte id 时通过 NationUtility.GetIdOrDefault 转换。
    public class Unit : CComponent
    {
        // 标准化后的国家 Tag，不依赖运行时 id 分配顺序，跨存档和配置引用都稳定。
        public NationTag Tag;

        // 单位在地图上的移动速度，MoveAlongHexPathCap 使用项目时间缩放后的 deltaTime 消耗它。
        public float MoveSpeed = 3f;
    }
}
