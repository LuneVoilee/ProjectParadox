#region

using System;
using Common.Contracts;
using Core.Capability;
using GamePlay.Strategy;
using GamePlay.World;
using UnityEngine.Tilemaps;

#endregion

namespace GamePlay.Map
{
    public static class MapEntityPreset
    {
        public struct Config
        {
            public float SeaLevel;
            public float MountainLevel;
            public int Width;
            public int Height;
            public bool EnableSeamlessX;
            public bool EnableSeamlessY;
            public int Seed;
            public int MinOffset;
            public int MaxOffset;
            public float HeightScale;
            public float MinNoiseScale;
            public Tilemap Tilemap;
            public TerrainSettings TerrainSettings;
        }

        public static CEntity Create
            (GameWorld world, in Config config, string entityName = "MapEntity")
        {
            if (world == null)
            {
                return null;
            }

            CEntity entity = world.AddChild(entityName);
            if (entity == null)
            {
                return null;
            }

            world.BindCapability<GenerateMapDataCap>(entity);
            world.BindCapability<DrawMapCap>(entity);
            world.BindCapability<TimeCap>(entity);

            var biomeComp = entity.AddComponent<Biome>();
            biomeComp.SeaLevel = config.SeaLevel;
            biomeComp.MountainLevel = config.MountainLevel;

            var mapComp = entity.AddComponent<Map>();
            mapComp.Width = config.Width;
            mapComp.Height = config.Height;
            mapComp.EnableSeamlessX = config.EnableSeamlessX;
            mapComp.EnableSeamlessY = config.EnableSeamlessY;

            var noiseComp = entity.AddComponent<Noise>();
            noiseComp.Seed = config.Seed;
            noiseComp.MinOffset = config.MinOffset;
            noiseComp.MaxOffset = config.MaxOffset;
            noiseComp.HeightScale = config.HeightScale;
            noiseComp.MinNoiseScale = config.MinNoiseScale;

            var drawMap = entity.AddComponent<DrawMap>();
            drawMap.Tilemap = config.Tilemap;
            drawMap.TerrainSettings = config.TerrainSettings;

            var time = entity.AddComponent<Time>();
            time.CurrentDate = new DateTime(500, 1, 1);
            time.NewTimeType = TimeType.Speed1;

            world.RegisterPrimaryMapEntity(entity);
            return entity;
        }
    }
}
