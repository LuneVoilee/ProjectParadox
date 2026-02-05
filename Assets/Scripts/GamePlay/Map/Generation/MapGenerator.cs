using UnityEngine;
using Map.Core;
using Map.Data;
using Map.View;

namespace Map.Generation
{
    public class MapGenerator : MonoBehaviour
    {
        [Header("Map Size")]
        public int Width = 100;
        public int Height = 100;

        [Header("Noise")]
        public int Seed;
        public float HeightScale = 0.08f;
        public float MoistureScale = 0.12f;

        [Header("Wrap")]
        public bool SeamlessX = true;
        public bool SeamlessY;

        [Header("Biome")]
        public BiomeSettings BiomeSettings;

        [Header("Links")]
        public HexMapRenderer Renderer;

        [Header("Debug")]
        public bool GenerateOnStart = true;

        private const int m_MinOffset = -100000;
        private const int m_MaxOffset = 100000;
        private const float m_MinNoiseScale = 0.0001f;

        private void Start()
        {
            if (GenerateOnStart)
            {
                GenerateAndRender();
            }
        }

        private void GenerateAndRender()
        {
            var data = Generate();
            if (Renderer != null)
            {
                Renderer.Render(data);
            }
        }

        private GridData Generate()
        {
            var width = Mathf.Max(1, Width);
            var height = Mathf.Max(1, Height);

            if (SeamlessX && (width & 1) == 1)
            {
                width += 1;
            }

            if (width != Width)
            {
                Width = width;
            }

            if (height != Height)
            {
                Height = height;
            }

            var data = new GridData(width, height);
            InitializeCells(data);

            var random = new System.Random(Seed);
            var heightNoise = CreateNoiseSettings(random, HeightScale, SeamlessX, SeamlessY);
            var moistureNoise = CreateNoiseSettings(random, MoistureScale, SeamlessX, SeamlessY);

            HeightSystem.Apply(data, heightNoise);
            ClimateSystem.Apply(data, moistureNoise);
            BiomeSystem.Apply(data, BiomeSettings);

            return data;
        }

        private void InitializeCells(GridData data)
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

        private NoiseSettings CreateNoiseSettings(System.Random random, float scale, bool seamlessX, bool seamlessY)
        {
            var validScale = Mathf.Max(m_MinNoiseScale, scale);
            return new NoiseSettings
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
