#region

using Core.Capability;
using UnityEngine;
using Random = System.Random;

#endregion

namespace GamePlay.Map
{
    //Seamless
    public class GenerateMapDataCap : CapabilityBase
    {
        private static readonly int m_MapId = Component<Map>.TId;
        private static readonly int m_NoiseId = Component<Noise>.TId;
        private static readonly int m_BiomeId = Component<Biome>.TId;

        protected override void OnInit()
        {
            Filter(m_MapId, m_NoiseId, m_BiomeId);
        }

        public override bool ShouldActivate()
        {
            return Owner.HasComponent(m_MapId) &&
                   Owner.HasComponent(m_NoiseId) &&
                   Owner.HasComponent(m_BiomeId);
        }

        public override bool ShouldDeactivate()
        {
            return !ShouldActivate();
        }

        protected override void OnActivated()
        {
            var map = Owner.GetComponent(m_MapId) as Map;
            var noise = Owner.GetComponent(m_NoiseId) as Noise;
            var biome = Owner.GetComponent(m_BiomeId) as Biome;
            if (map == null || noise == null || biome == null)
            {
                Debug.LogError("MapGenerateCap: Missing required components.");
                return;
            }

            var width = Mathf.Max(1, map.Width);
            var height = Mathf.Max(1, map.Height);

            if (map.EnableSeamlessX && (width & 1) == 1)
            {
                width += 1;
            }


            var grid = Owner.AddComponent<Grid>();

            grid.Cells = new Cell[width * height];
            grid.Width = width;
            grid.Height = height;

            var random = new Random(noise.Seed);

            var heightNoise = CreateNoiseSettings(noise.HeightScale, random, noise, map);

            //避免在内层循环做高频的乘法和加法
            int index = 0;

            for (var row = 0; row < height; row++)
            {
                for (var col = 0; col < width; col++)
                {
                    //获取该数组元素内存地址的直接引用
                    ref var cell = ref grid.Cells[index];

                    cell.Coordinates = HexCoordinates.FromOffset(col, row);

                    var hexHeight = Sample(col, row, width, height, heightNoise);
                    cell.Height = hexHeight;

                    cell.TerrainType = Resolve(hexHeight, biome.SeaLevel, biome.MountainLevel);

                    index++;
                }
            }

            Owner.RemoveComponent(m_MapId);
            Owner.RemoveComponent(m_NoiseId);
            Owner.RemoveComponent(m_BiomeId);
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