using UnityEngine;
using Map.Components;
using Map.Common;
using Map.Settings;

namespace Map.Systems
{
    public static class MapGenerator
    {
        public struct Settings
        {
            public int Width;
            public int Height;
            public int Seed;
            public float HeightScale;
            public float MoistureScale;
            public bool SeamlessX;
            public bool SeamlessY;
            public BiomeSettings BiomeSettings;
        }

        private const int m_MinOffset = -100000;
        private const int m_MaxOffset = 100000;
        private const float m_MinNoiseScale = 0.0001f;

        public static GridData Generate(Settings settings)
        {
            var width = Mathf.Max(1, settings.Width);
            var height = Mathf.Max(1, settings.Height);

            if (settings.SeamlessX && (width & 1) == 1)
            {
                width += 1;
            }

            var data = new GridData(width, height);
            InitializeCells(data);

            var random = new System.Random(settings.Seed);
            var heightNoise = CreateNoiseSettings(random, settings.HeightScale, settings.SeamlessX, settings.SeamlessY);
            var moistureNoise = CreateNoiseSettings(random, settings.MoistureScale, settings.SeamlessX, settings.SeamlessY);

            HeightSystem.Apply(data, heightNoise);
            ClimateSystem.Apply(data, moistureNoise);
            BiomeSystem.Apply(data, settings.BiomeSettings);

            return data;
        }

        private static void InitializeCells(GridData data)
        {
            var width = data.Width;
            var height = data.Height;

            for (var row = 0; row < height; row++)
            {
                for (var col = 0; col < width; col++)
                {
                    var coords = HexCoordinates.FromOffset(col, row);
                    var cell = new CellData(coords);
                    data.SetCell(col, row, cell);
                }
            }
        }

        private static NoiseParam CreateNoiseSettings(System.Random random, float scale, bool seamlessX, bool seamlessY)
        {
            var validScale = Mathf.Max(m_MinNoiseScale, scale);
            return new NoiseParam
            {
                Scale = validScale,
                OffsetX = random.Next(m_MinOffset, m_MaxOffset),
                OffsetY = random.Next(m_MinOffset, m_MaxOffset),
                SeamlessX = seamlessX,
                SeamlessY = seamlessY
            };
        }
    }
}
