using Map.Common;
using UnityEngine;
using Map.Components;

namespace Map.Systems
{
    public static class HeightSystem
    {
        private const float m_MinNoiseScale = 0.0001f;

        public static void Apply(GridData data, NoiseParam param)
        {
            if (data == null)
            {
                return;
            }

            var scale = Mathf.Max(m_MinNoiseScale, param.Scale);
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

                    var h = SeamlessNoise.Sample(col, row, width, height, scale, param);
                    cell.Height = h;
                }
            }
        }
    }
}
