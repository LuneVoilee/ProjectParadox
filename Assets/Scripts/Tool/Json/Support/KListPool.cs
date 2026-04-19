#region

using System.Collections.Generic;

#endregion

namespace Tool.Json
{
    public static class KListPool<T>
    {
        // ReSharper disable once StaticMemberInGenericType
        public static int Capacity = 50;

        private static readonly Queue<List<T>> Pool = new Queue<List<T>>();

        public static List<T> Claim()
        {
            if (Pool.Count > 0)
            {
                List<T> list = Pool.Dequeue();
                list.Clear();
                return list;
            }

            return new List<T>();
        }

        public static List<T> Claim(IEnumerable<T> items)
        {
            List<T> list = Claim();
            if (items != null)
            {
                list.AddRange(items);
            }

            return list;
        }

        public static void Release(ref List<T> list)
        {
            if (list == null)
            {
                return;
            }

            Release(list);
            list = null;
        }

        public static void Release(List<T> list)
        {
            if (list == null)
            {
                return;
            }

            list.Clear();
            if (Capacity > 0 && Pool.Count < Capacity)
            {
                Pool.Enqueue(list);
            }
        }
    }
}