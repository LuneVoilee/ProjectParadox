using UnityEngine;
using Map.Generation;

namespace Map.Util
{
    public static class SeamlessNoise
    {
        public static float Sample(int col, int row, int width, int height, float scale, NoiseSettings settings)
        {
            var nx = (col + settings.OffsetX) * scale;
            var ny = (row + settings.OffsetY) * scale;
            var seamlessX = settings.SeamlessX && width > 1;
            var seamlessY = settings.SeamlessY && height > 1;

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
                var px = periodX * scale;
                var py1 = periodY * scale;

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
                var px = periodX * scale;
                var a = Mathf.PerlinNoise(nx, ny);
                var b = Mathf.PerlinNoise(nx - px, ny);
                return Mathf.Lerp(a, b, tx);
            }

            var ty = periodY > 0 ? row / (float)periodY : 0f;
            var py = periodY * scale;
            var ay = Mathf.PerlinNoise(nx, ny);
            var by = Mathf.PerlinNoise(nx, ny - py);
            return Mathf.Lerp(ay, by, ty);
        }
    }
}
