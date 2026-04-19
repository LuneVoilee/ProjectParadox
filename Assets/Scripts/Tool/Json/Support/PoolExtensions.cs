using System.Collections.Generic;

namespace Tool.Json
{
    internal static class PoolExtensions
    {
        public static void Release<T>(this List<T> list)
        {
            KListPool<T>.Release(list);
        }
    }
}
