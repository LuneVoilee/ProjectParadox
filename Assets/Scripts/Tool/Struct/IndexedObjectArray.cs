using System;
using System.Collections;
using System.Collections.Generic;

namespace Tool
{
    public class IndexedObjectArray<T> : IEnumerable<T>, IDisposable where T : class
    {
        private T[] m_Items;

        public List<int> IndexList { get; private set; }

        public int Count => IndexList?.Count ?? 0;

        public bool IsInitialized => IndexList != null;

        public T this[int index]
        {
            get
            {
                if (m_Items == null || index < 0 || index >= m_Items.Length)
                {
                    return null;
                }

                return m_Items[index];
            }
        }

        public void Init(int maxCount)
        {
            if (maxCount <= 0)
            {
                maxCount = 1;
            }

            if (m_Items == null || m_Items.Length != maxCount)
            {
                m_Items = new T[maxCount];
            }

            IndexList = new List<int>(maxCount);
        }

        private void EnsureIndex(int index)
        {
            if (m_Items == null)
            {
                m_Items = new T[Math.Max(1, index + 1)];
                IndexList ??= new List<int>();
                return;
            }

            if (index < m_Items.Length)
            {
                return;
            }

            int oldLength = m_Items.Length;
            int multiplier = index / oldLength + 1;
            int newLength = oldLength * multiplier;
            T[] newItems = new T[newLength];
            Array.Copy(m_Items, 0, newItems, 0, oldLength);
            m_Items = newItems;
        }

        public T Set(int index, T item)
        {
            EnsureIndex(index);
            if (m_Items[index] == null)
            {
                IndexList.Add(index);
            }

            m_Items[index] = item;
            return item;
        }

        public T Remove(int index)
        {
            if (m_Items == null || index < 0 || index >= m_Items.Length)
            {
                return null;
            }

            T item = m_Items[index];
            if (item == null)
            {
                return null;
            }

            IndexList.RemoveSwapBack(index);
            m_Items[index] = null;
            return item;
        }

        public void Clear()
        {
            if (!IsInitialized)
            {
                return;
            }

            for (int i = 0; i < IndexList.Count; i++)
            {
                m_Items[IndexList[i]] = null;
            }

            IndexList.Clear();
        }

        public void Dispose()
        {
            Clear();
            IndexList = null;
            m_Items = null;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(IndexList, m_Items);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public struct Enumerator : IEnumerator<T>
        {
            private readonly List<int> m_Indices;
            private readonly T[] m_Items;
            private int m_Cursor;

            internal Enumerator(List<int> indices, T[] items)
            {
                m_Indices = indices;
                m_Items = items;
                m_Cursor = indices?.Count ?? 0;
            }

            public T Current => m_Items[m_Indices[m_Cursor]];

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                return --m_Cursor >= 0;
            }

            public void Reset()
            {
                m_Cursor = m_Indices?.Count ?? 0;
            }

            public void Dispose()
            {
            }
        }
    }
}
