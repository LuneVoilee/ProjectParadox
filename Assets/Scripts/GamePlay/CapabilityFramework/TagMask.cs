using System;
using System.Collections;
using System.Collections.Generic;

namespace GamePlay.CapabilityFramework
{
    /// <summary>
    /// Tag 集合。
    ///
    /// 用途：
    /// - Capability 声明自己的条件（Required / Forbidden / BlockedBy）；
    /// - Capability 声明自己激活后要施加的标签（Granted / Block）；
    /// - Scheduler 通过 TagMask 做快速判定，而不是让 Capability 直接互相引用。
    ///
    /// 这里使用 string Tag（例如 "State.MapGenerated"、"Block.MapEdit"）
    /// 是为了可读性和调试便利。若后期追求极致性能，可替换为 int TagId。
    /// </summary>
    public sealed class TagMask : IEnumerable<string>
    {
        private readonly HashSet<string> m_Tags;

        public static TagMask Empty => new TagMask();

        public int Count => m_Tags.Count;

        public TagMask()
        {
            m_Tags = new HashSet<string>(StringComparer.Ordinal);
        }

        public TagMask(params string[] tags)
            : this()
        {
            AddRange(tags);
        }

        public TagMask(IEnumerable<string> tags)
            : this()
        {
            AddRange(tags);
        }

        public static TagMask From(params string[] tags)
        {
            return new TagMask(tags);
        }

        public bool Contains(string tag)
        {
            return !string.IsNullOrEmpty(tag) && m_Tags.Contains(tag);
        }

        public bool ContainsAll(TagMask other)
        {
            if (other == null || other.Count == 0)
            {
                return true;
            }

            foreach (var tag in other.m_Tags)
            {
                if (!m_Tags.Contains(tag))
                {
                    return false;
                }
            }

            return true;
        }

        public bool Intersects(TagMask other)
        {
            if (other == null || other.Count == 0 || Count == 0)
            {
                return false;
            }

            // 遍历较小集合，减少查找次数。
            if (Count <= other.Count)
            {
                foreach (var tag in m_Tags)
                {
                    if (other.m_Tags.Contains(tag))
                    {
                        return true;
                    }
                }

                return false;
            }

            foreach (var tag in other.m_Tags)
            {
                if (m_Tags.Contains(tag))
                {
                    return true;
                }
            }

            return false;
        }

        public void Add(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return;
            }

            m_Tags.Add(tag);
        }

        public bool Remove(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return false;
            }

            return m_Tags.Remove(tag);
        }

        public void Clear()
        {
            m_Tags.Clear();
        }

        public void AddRange(IEnumerable<string> tags)
        {
            if (tags == null)
            {
                return;
            }

            foreach (var tag in tags)
            {
                Add(tag);
            }
        }

        public void AddRange(TagMask other)
        {
            if (other == null)
            {
                return;
            }

            AddRange(other.m_Tags);
        }

        public IEnumerator<string> GetEnumerator()
        {
            return m_Tags.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override string ToString()
        {
            return string.Join(", ", m_Tags);
        }
    }
}
