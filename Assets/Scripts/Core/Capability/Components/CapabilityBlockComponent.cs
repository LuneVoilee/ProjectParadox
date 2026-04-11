using System.Collections.Generic;
using UnityEngine;

namespace Core.Capability
{
    public partial class CapabilityBlockComponent : CComponent
    {
        private int[] m_Tags;

        public void Init(int maxCount)
        {
            m_Tags = new int[maxCount];
        }

        public void Block(int index, CapabilityBase instigator)
        {
#if UNITY_EDITOR
            if (!CanBlock(index, instigator))
            {
                return;
            }
#endif
            m_Tags[index]++;
        }

        public void Unblock(int index, CapabilityBase instigator)
        {
#if UNITY_EDITOR
            CanUnblock(index, instigator);
#endif
            m_Tags[index] = Mathf.Max(0, --m_Tags[index]);
        }

        public bool IsBlocked(List<int> indices)
        {
            for (int i = 0; i < indices.Count; i++)
            {
                if (m_Tags[indices[i]] > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
