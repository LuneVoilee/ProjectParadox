#region

using System.Collections.Generic;

#endregion

namespace Tool
{
    public static class PoolExtensions
    {
        public static void Release<T>(this List<T> list)
        {
            KListPool<T>.Release(list);
        }
    }
}