#region

using Core.Capability;
using GamePlay.Strategy;
using GamePlay.Util;
using GamePlay.World;
using UnityEngine;
using Random = System.Random;

#endregion

namespace GamePlay.Map
{
    // 负责无缝地图数据生成。
    public class CpGenerateMapData : CapabilityBase
    {
        private static readonly int m_MapId = Component<Map>.TId;
        private static readonly int m_NoiseId = Component<Noise>.TId;
        private static readonly int m_BiomeId = Component<Biome>.TId;
        private readonly System.Collections.Generic.List<CEntity> m_Entities =
            new System.Collections.Generic.List<CEntity>(2);

        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.ScenarioMapGenerate;

        public override string DebugCategory => CapabilityDebugCategory.Map;

        public override void Tick
            (CapabilityContext context, float deltaTime, float realElapsedSeconds)
        {
            context.QuerySnapshotByIds(m_Entities, m_MapId, m_NoiseId, m_BiomeId);
            for (int i = 0; i < m_Entities.Count; i++)
            {
                GenerateOne(context, m_Entities[i]);
            }
        }

        private void GenerateOne(CapabilityContext context, CEntity entity)
        {
            if (!entity.TryGetMap(out var map)) return;
            if (!entity.TryGetNoise(out var noise)) return;
            if (!entity.TryGetBiome(out var biome)) return;
            var width = Mathf.Max(1, map.Width);
            var height = Mathf.Max(1, map.Height);

            if (map.EnableSeamlessX && (width & 1) == 1)
            {
                width += 1;
            }


            var cells = new Cell[width * height];

            var random = new Random(noise.Seed);

            var heightNoise = CreateNoiseSettings(noise.HeightScale, random, noise, map);

            //避免在内层循环做高频的乘法和加法
            int index = 0;

            for (var row = 0; row < height; row++)
            {
                for (var col = 0; col < width; col++)
                {
                    //获取该数组元素内存地址的直接引用
                    ref var cell = ref cells[index];

                    cell.Coordinates = HexCoordinates.FromOffset(col, row);

                    var hexHeight = Sample(col, row, width, height, heightNoise);
                    cell.Height = hexHeight;

                    cell.TerrainType = Resolve(hexHeight, biome.SeaLevel, biome.MountainLevel);

                    index++;
                }
            }

            bool enableSeamlessX = map.EnableSeamlessX;
            bool enableSeamlessY = map.EnableSeamlessY;
            context.Commands.AddComponent<Grid>(entity, grid =>
            {
                grid.Cells = cells;
                grid.Width = width;
                grid.Height = height;
                grid.EnableSeamlessX = enableSeamlessX;
                grid.EnableSeamlessY = enableSeamlessY;
            });

            if (entity.TryGetDrawMap(out var drawMap))
            {
                drawMap.IsDirty = true;
            }

            context.Commands.RemoveComponent(entity, m_MapId);
            context.Commands.RemoveComponent(entity, m_NoiseId);
            context.Commands.RemoveComponent(entity, m_BiomeId);
            context.MarkWorked();
        }

        private static NoiseParam CreateNoiseSettings
        (
            float targetScale, Random random, Noise noise,
            Map map
        )
        {
            return new NoiseParam
            {
                Scale = Mathf.Max(noise.MinNoiseScale, targetScale),
                OffsetX = random.Next(noise.MinOffset, noise.MaxOffset),
                OffsetY = random.Next(noise.MinOffset, noise.MaxOffset),
                SeamlessX = map.EnableSeamlessX,
                SeamlessY = map.EnableSeamlessY
            };
        }

        private float Sample
        (
            int col, int row, int width,
            int height, NoiseParam param
        )
        {
            var nx = (col + param.OffsetX) * param.Scale;
            var ny = (row + param.OffsetY) * param.Scale;
            var seamlessX = param.SeamlessX && width > 1;
            var seamlessY = param.SeamlessY && height > 1;

            if (!seamlessX && !seamlessY)
            {
                return Mathf.PerlinNoise(nx, ny);
            }

            var periodX = seamlessX ? width - 1 : width;
            var periodY = seamlessY ? height - 1 : height;

            if (seamlessX && seamlessY)
            {
                var tx = periodX > 0 ? col / (float)periodX : 0f;
                var ty1 = periodY > 0 ? row / (float)periodY : 0f;
                var px = periodX * param.Scale;
                var py1 = periodY * param.Scale;

                var a = Mathf.PerlinNoise(nx, ny);
                var b = Mathf.PerlinNoise(nx - px, ny);
                var c = Mathf.PerlinNoise(nx, ny - py1);
                var d = Mathf.PerlinNoise(nx - px, ny - py1);
                var ab = Mathf.Lerp(a, b, tx);
                var cd = Mathf.Lerp(c, d, tx);
                return Mathf.Lerp(ab, cd, ty1);
            }

            if (seamlessX)
            {
                var tx = periodX > 0 ? col / (float)periodX : 0f;
                var px = periodX * param.Scale;
                var a = Mathf.PerlinNoise(nx, ny);
                var b = Mathf.PerlinNoise(nx - px, ny);
                return Mathf.Lerp(a, b, tx);
            }

            var ty = periodY > 0 ? row / (float)periodY : 0f;
            var py = periodY * param.Scale;
            var ay = Mathf.PerlinNoise(nx, ny);
            var by = Mathf.PerlinNoise(nx, ny - py);
            return Mathf.Lerp(ay, by, ty);
        }

        private static TerrainType Resolve(float height, float seaLevel, float mountainLevel)
        {
            if (height < seaLevel)
            {
                return TerrainType.Ocean;
            }

            if (height > mountainLevel)
            {
                return TerrainType.Mountain;
            }

            return TerrainType.Plain;
        }
    }
}
