using Map.Components;
using Map.Common;
using Map.Settings;

namespace Map.Systems
{
    public static class BiomeSystem
    {
        private const float m_DefaultSeaLevel = 0.3f;
        private const float m_DefaultMountainLevel = 0.8f;
        private const float m_DefaultDesertMoisture = 0.4f;

        public static void Apply(CGrid data, BiomeSettings settings)
        {
            if (data == null)
            {
                return;
            }

            var seaLevel = settings != null ? settings.SeaLevel : m_DefaultSeaLevel;
            var mountainLevel = settings != null ? settings.MountainLevel : m_DefaultMountainLevel;
            var desertMoisture = settings != null ? settings.DesertMoisture : m_DefaultDesertMoisture;

            var width = data.Width;
            var height = data.Height;

            for (var row = 0; row < height; row++)
            {
                for (var col = 0; col < width; col++)
                {
                    var cell = data.GetCell(col, row);
                    if (cell == null)
                    {
                        continue;
                    }

                    cell.TerrainType = Resolve(cell.Height, cell.Moisture, seaLevel, mountainLevel, desertMoisture);
                }
            }
        }

        private static TerrainType Resolve(float heightValue, float moistureValue, float seaLevel, float mountainLevel, float desertMoisture)
        {
            if (heightValue < seaLevel)
            {
                return TerrainType.Ocean;
            }

            if (heightValue > mountainLevel)
            {
                return TerrainType.Mountain;
            }

            if (moistureValue < desertMoisture)
            {
                return TerrainType.Desert;
            }

            return TerrainType.Grass;
        }
    }
}
