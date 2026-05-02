#region

using Core.Capability;
using GamePlay.Map;
using GamePlay.Util;
using GamePlay.World;
using Sirenix.OdinInspector;
using UnityEngine;
using Grid = GamePlay.Map.Grid;

#endregion

namespace GamePlay.Strategy
{
    // 场景单位安装器：挂在士兵 GameObject 上，把 Transform 注册为 Capability 单位实体。
    public class UnitInstaller : EntityInstaller<CEntity>
    {
        [BoxGroup("Unit参数")] public string NationTag;
        [BoxGroup("Unit参数")] public float MoveSpeed = 3f;
        [BoxGroup("Unit参数")] public float ArriveDistance = 0.03f;
        [BoxGroup("Unit参数")] public bool SnapToCellCenter = true;
        [BoxGroup("战斗参数")] public float MaxMorale = 100f;
        [BoxGroup("战斗参数")] public float MaxHealth = 100f;
        [BoxGroup("战斗参数")] public float Attack = 10f;
        [BoxGroup("战斗参数")] public float Defense = 5f;
        [BoxGroup("战斗参数")] public float MoraleRecovery = 5f;

        private GameWorld m_World;

        private void Update()
        {
            TryCreateEntity();
        }

        private void TryCreateEntity()
        {
            if (Entity != null)
            {
                return;
            }

            var gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                return;
            }

            m_World = gameManager.World;
            // 逐项解析地图上下文：世界 → 主地图实体 → Grid → DrawMap → Tilemap。
            if (m_World == null) return;
            if (!m_World.TryGetPrimaryMapEntity(out CEntity mapEntity)) return;
            if (!mapEntity.TryGetGrid(out Grid grid)) return;
            if (!mapEntity.TryGetDrawMap(out DrawMap drawMap)) return;
            if (drawMap.Tilemap == null) return;

            Vector3Int rawCell = drawMap.Tilemap.WorldToCell(transform.position);
            if (!HexMapUtility.TryNormalizeCell(grid, rawCell, out Vector3Int cell))
            {
                return;
            }

            HexCoordinates hex = HexCoordinates.FromOffset(cell.x, cell.y);
            if (mapEntity.TryGetUnitOccupancyIndex(out UnitOccupancyIndex occupancyIndex) &&
                occupancyIndex.TryGetUnit(hex, out _))
            {
                return;
            }

            if (SnapToCellCenter)
            {
                transform.position = drawMap.Tilemap.GetCellCenterWorld(cell);
            }

            var config = new UnitEntityPreset.Config
            {
                Transform = transform,
                StartHex = hex,
                StartCell = cell,
                Tag = new NationTag(NationTag),
                MoveSpeed = MoveSpeed,
                ArriveDistance = ArriveDistance,
                MaxMorale = MaxMorale,
                MaxHealth = MaxHealth,
                Attack = Attack,
                Defense = Defense,
                MoraleRecovery = MoraleRecovery
            };

            Entity = UnitEntityPreset.Create(m_World, mapEntity, config, gameObject);
            enabled = Entity == null;
        }

        private void OnDestroy()
        {
            // 销毁 GameObject 时清理 Capability 世界中的单位实体和占位记录。
            // 若 Entity 已被 World.RemoveChild 提前 Dispose，Components 为 null，此时无需再次清理。
            if (m_World == null) return;
            if (m_World.Children == null) return;
            if (Entity == null || !Entity.IsActive) return;

            if (m_World.TryGetPrimaryMapEntity(out CEntity mapEntity))
            {
                if (mapEntity.TryGetUnitOccupancyIndex(out UnitOccupancyIndex occupancyIndex))
                {
                    if (Entity.TryGetUnitPosition(out UnitPosition position))
                    {
                        occupancyIndex.Remove(position.Hex, Entity.Id);
                    }
                }
            }

            m_World.RemoveChild(Entity);
            Entity = null;
            m_World = null;
        }
    }
}