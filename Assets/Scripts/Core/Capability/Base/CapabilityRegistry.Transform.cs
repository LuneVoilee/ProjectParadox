using System.Collections.Generic;

namespace Core.Capability
{
    public partial class CapabilityRegistry
    {
        public void AddGlobal<TCapability>() where TCapability : CapabilityBase, new()
        {
            TCapability capability = new TCapability();
            if (capability.UpdateMode == CapabilityUpdateMode.Update)
            {
                int capabilityId = CapabilityId<TCapability, IUpdateSystem>.TId;
                SetGlobalCapability(m_GlobalUpdateCapabilities, capabilityId, capability);
                return;
            }

            int fixedCapabilityId = CapabilityId<TCapability, IFixedUpdateSystem>.TId;
            SetGlobalCapability(m_GlobalFixedUpdateCapabilities, fixedCapabilityId, capability);
        }

        private void SetGlobalCapability
            (List<CapabilityBase> capabilities, int capabilityId, CapabilityBase capability)
        {
            if (capabilityId < 0 || capability == null)
            {
                capability?.Dispose();
                return;
            }

            capability.InitGlobal(capabilityId, m_World);
            capabilities.Add(capability);
            capabilities.Sort(CompareGlobalCapability);
        }

        public void GetGlobalCapabilities
        (
            List<CapabilityBase> updateCapabilities,
            List<CapabilityBase> fixedUpdateCapabilities
        )
        {
            if (updateCapabilities != null)
            {
                updateCapabilities.AddRange(m_GlobalUpdateCapabilities);
            }

            if (fixedUpdateCapabilities != null)
            {
                fixedUpdateCapabilities.AddRange(m_GlobalFixedUpdateCapabilities);
            }
        }

        private static int CompareGlobalCapability
            (CapabilityBase left, CapabilityBase right)
        {
            int byOrder = left.TickGroupOrder.CompareTo(right.TickGroupOrder);
            if (byOrder != 0)
            {
                return byOrder;
            }

            int byMode = left.UpdateMode.CompareTo(right.UpdateMode);
            if (byMode != 0)
            {
                return byMode;
            }

            return string.CompareOrdinal(left.GetType().FullName, right.GetType().FullName);
        }
    }
}
