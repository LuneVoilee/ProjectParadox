using System.Collections;
using System.Collections.Generic;

namespace Tool
{
    public class IndexedSet<T> : IEnumerable<T> where T : class
    {
        private readonly HashSet<T> m_Set;

        public IndexedSet(int capacity = 0)
        {
            m_Set = capacity > 0 ? new HashSet<T>() : new HashSet<T>();
        }

        public int Count => m_Set.Count;

        public bool Add(T item)
        {
            return m_Set.Add(item);
        }

        public bool Remove(T item)
        {
            return m_Set.Remove(item);
        }

        public void Clear()
        {
            m_Set.Clear();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return m_Set.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
