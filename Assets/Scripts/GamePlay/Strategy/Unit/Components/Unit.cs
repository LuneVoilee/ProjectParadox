#region

using Core.Capability;

#endregion

namespace GamePlay.Strategy
{
    // 单位所属国家数据。NationId 服务于运行时格子占领，Tag 保留给配置查询和可读逻辑。
    public class Unit : CComponent
    {
        // 与 Cell.OwnerId 共用的运行时 byte id，热路径直接比较这个字段。
        public byte NationId;

        // JSON 国家主键，不依赖运行时 id 分配顺序，适合做静态数据查询。
        public NationTag Tag;

        // 单位在地图上的移动速度，MoveAlongHexPathCap 使用项目时间缩放后的 deltaTime 消耗它。
        public float MoveSpeed = 3f;
    }
}
