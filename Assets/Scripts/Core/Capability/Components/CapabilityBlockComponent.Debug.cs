#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Core.Capability
{
    public partial class CapabilityBlockComponent
    {
        private Dictionary<CapabilityBase, List<int>> m_InstigatorIndices = new Dictionary<CapabilityBase, List<int>>();

        private bool CanBlock(int index, CapabilityBase instigator)
        {
            if (!m_InstigatorIndices.TryGetValue(instigator, out List<int> list))
            {
                list = new List<int>(8);
                m_InstigatorIndices.Add(instigator, list);
            }

            if (list.Contains(index))
            {
                Debug.LogError($"{instigator.GetType().Name} already blocked {index}");
                return false;
            }

            list.Add(index);
            return true;
        }

        private bool CanUnblock(int index, CapabilityBase instigator)
        {
            if (!m_InstigatorIndices.TryGetValue(instigator, out List<int> list))
            {
                return false;
            }

            if (!list.Contains(index))
            {
                return false;
            }

            list.Remove(index);
            return true;
        }
    }
}
#endif
