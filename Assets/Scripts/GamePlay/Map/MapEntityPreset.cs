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
    // 主地图实体预设：只负责安装组件与绑定 Capability，不在这里执行运行时国家注册或占领逻辑。
    public static class MapEntityPreset
    {
        // 外部安装器传入的地图生成与渲染配置，Create 会把它拆成各个 CComponent。
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
            // Preset 只接受已创建好的 GameWorld，失败时不产生半初始化实体。
            if (world == null)
            {
                return null;
            }

            // 地图实体是后续相机、国家注册、占领结算共同访问的 primary map entity。
            CEntity entity = world.AddChild(entityName);
            if (entity == null)
            {
                return null;
            }

            // 绑定运行时流程：生成地图 -> 注册国家 -> 应用领土变更 -> 表现绘制。
            world.BindCapability<GenerateMapDataCap>(entity);
            world.BindCapability<SelectAndSetDestinationCap>(entity);
            world.BindCapability<ApplyTerritoryChangesCap>(entity);
            world.BindCapability<DrawMapCap>(entity);
            world.BindCapability<NationRegistryCap>(entity);
            world.BindCapability<TimeCap>(entity);
            world.BindCapability<DiplomacyRegistryCap>(entity);

            // 地图生成输入组件，GenerateMapDataCap 激活后会消费并移除这些一次性配置。
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

            // 国家与领土表现组件只安装状态，具体初始化由 NationRegistryCap/Apply/DrawMapCap 完成。
            entity.AddComponent<NationIndex>();
            entity.AddComponent<TerritoryOwnershipBuffer>();
            entity.AddComponent<TerritoryPaintState>();
            entity.AddComponent<UnitOccupancyIndex>();
            entity.AddComponent<NationBootstrap>();
            entity.AddComponent<DiplomacyIndex>();
            entity.AddComponent<DiplomacyBootstrap>();

            // 时间组件仍由地图实体承载，保持现有 TimeCap 绑定方式。
            var time = entity.AddComponent<Time>();
            time.CurrentDate = new DateTime(500, 1, 1);
            time.NewTimeType = TimeType.Speed1;

            // 注册为主地图实体后，其它 Cap 才能通过 GameWorld.TryGetPrimaryMapEntity 找到它。
            world.RegisterPrimaryMapEntity(entity);
            return entity;
        }
    }
}
