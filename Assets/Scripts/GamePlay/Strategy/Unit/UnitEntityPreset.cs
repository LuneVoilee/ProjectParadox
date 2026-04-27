#region

using Core.Capability;
using GamePlay.Map;
using GamePlay.Util;
using GamePlay.World;
using UnityEngine;

#endregion

namespace GamePlay.Strategy
{
    // 单位实体预设：负责把场景士兵 Transform 绑定进 Capability 世界。
    public static class UnitEntityPreset
    {
        public struct Config
        {
            public Transform Transform;
            public HexCoordinates StartHex;
            public Vector3Int StartCell;
            public byte NationId;
            public NationTag Tag;
            public float MoveSpeed;
            public float ArriveDistance;
        }

        public static CEntity Create
            (GameWorld world, CEntity mapEntity, in Config config, string entityName = "UnitEntity")
        {
            // 前置校验：世界、地图实体、场景 Transform 缺一不可。
            if (world == null) return null;
            if (mapEntity == null) return null;
            if (config.Transform == null) return null;

            // 起始格已被其他单位占据则拒绝创建，避免同格重叠。
            if (!mapEntity.TryGetUnitOccupancyIndex(out UnitOccupancyIndex occupancyIndex))
                return null;
            if (occupancyIndex.TryGetUnit(config.StartHex, out _)) return null;

            CEntity entity = world.AddChild(entityName);
            if (entity == null)
            {
                return null;
            }

            world.BindCapability<MoveAlongHexPathCap>(entity);
            world.BindCapability<OccupyCap>(entity);

            Unit unit = entity.AddComponent<Unit>();
            unit.NationId = config.NationId;
            unit.Tag = config.Tag;
            unit.MoveSpeed = Mathf.Max(0.01f, config.MoveSpeed);

            UnitPosition position = entity.AddComponent<UnitPosition>();
            position.Hex = config.StartHex;
            position.Cell = config.StartCell;

            UnitMotor motor = entity.AddComponent<UnitMotor>();
            motor.Transform = config.Transform;
            motor.ArriveDistance = Mathf.Max(0.001f, config.ArriveDistance);

            occupancyIndex.Set(config.StartHex, entity.Id);

            return entity;
        }
    }
}
