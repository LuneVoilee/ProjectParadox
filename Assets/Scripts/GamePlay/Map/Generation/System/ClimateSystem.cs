using UnityEngine;
using Map.Data;
using Map.Util;

namespace Map.Generation
{
    public static class ClimateSystem
    {
        private const float m_MinNoiseScale = 0.0001f;

        public static void Apply(GridData data, NoiseSettings settings)
        {
            if (data == null)
            {
                return;
            }

            var scale = Mathf.Max(m_MinNoiseScale, settings.Scale);
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

                    var m = SeamlessNoise.Sample(col, row, width, height, scale, settings);
                    cell.Moisture = m;
                }
            }
        }
    }
}
