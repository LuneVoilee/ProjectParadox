#region

using Core.Capability;
using GamePlay.Map;
using GamePlay.Util;
using GamePlay.World;
using UnityEngine;
using Grid = GamePlay.Map.Grid;

#endregion

namespace GamePlay.Strategy
{
    // 玩法规则所需的地图上下文。集中解析主地图实体，避免各能力重复爬 World。
    public readonly struct StrategyMapContext
    {
        public readonly GameWorld World;
        public readonly CEntity MapEntity;
        public readonly Grid Grid;
        public readonly DrawMap DrawMap;
        public readonly UnitOccupancyIndex Occupancy;
        public readonly NationIndex NationIndex;
        public readonly DiplomacyIndex DiplomacyIndex;
        public readonly UnityEngine.Camera Camera;

        private StrategyMapContext
        (
            GameWorld world, CEntity mapEntity, Grid grid, DrawMap drawMap,
            UnitOccupancyIndex occupancy, NationIndex nationIndex,
            DiplomacyIndex diplomacyIndex, UnityEngine.Camera camera
        )
        {
            World = world;
            MapEntity = mapEntity;
            Grid = grid;
            DrawMap = drawMap;
            Occupancy = occupancy;
            NationIndex = nationIndex;
            DiplomacyIndex = diplomacyIndex;
            Camera = camera;
        }

        public static bool TryCreate
            (CapabilityWorld world, out StrategyMapContext context, bool requireCamera = false)
        {
            context = default;
            if (world is not GameWorld gameWorld)
            {
                return false;
            }

            if (!gameWorld.TryGetPrimaryMapEntity(out CEntity mapEntity))
            {
                return false;
            }

            if (!mapEntity.TryGetGrid(out Grid grid))
            {
                return false;
            }

            if (!mapEntity.TryGetDrawMap(out DrawMap drawMap))
            {
                return false;
            }

            if (!mapEntity.TryGetUnitOccupancyIndex(out UnitOccupancyIndex occupancy))
            {
                return false;
            }

            if (!mapEntity.TryGetNationIndex(out NationIndex nationIndex))
            {
                return false;
            }

            if (!mapEntity.TryGetDiplomacyIndex(out DiplomacyIndex diplomacyIndex))
            {
                return false;
            }

            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (requireCamera && camera == null)
            {
                return false;
            }

            context = new StrategyMapContext(gameWorld, mapEntity, grid, drawMap,
                occupancy, nationIndex, diplomacyIndex, camera);
            return true;
        }

        public bool TryScreenToHex
            (Vector2 screenPosition, out HexCoordinates hex, out Vector3Int cell)
        {
            hex = default;
            cell = default;
            if (Camera == null || DrawMap?.Tilemap == null)
            {
                return false;
            }

            return HexMapUtility.TryGetClickedHex(Camera, DrawMap.Tilemap, Grid,
                screenPosition, out hex, out cell, out _);
        }

        public bool TryGetUnitAt(HexCoordinates hex, out CEntity unitEntity)
        {
            unitEntity = null;
            if (Occupancy == null)
            {
                return false;
            }

            if (!Occupancy.TryGetUnit(hex, out int unitEntityId))
            {
                return false;
            }

            unitEntity = World.GetChild(unitEntityId);
            return unitEntity != null && unitEntity.HasUnit();
        }

        public Vector3 GetNearestWorldPosition
            (HexCoordinates hex, Vector3 referenceWorldPosition)
        {
            return HexMapUtility.GetNearestMirroredWorldPosition(DrawMap.Tilemap,
                Grid, hex, referenceWorldPosition);
        }
    }
}
