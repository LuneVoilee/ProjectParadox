using System;
using System.Collections.Generic;

namespace Core.Capability
{
    internal static class CapabilityIdGenerator<TUpdateMode>
    {
        private static int m_Next;

        public static int GetId<TCapability>()
        {
            CapabilityTypeRegistry.CapabilityTypes.Add(typeof(TCapability));
            return m_Next++;
        }
    }

    public static class CapabilityId<TCapability, TUpdateMode>
    {
        public static readonly int TId = CapabilityIdGenerator<TUpdateMode>.GetId<TCapability>();
    }

    public static class CapabilityTypeRegistry
    {
        public static readonly List<Type> CapabilityTypes = new List<Type>(128);

        public static int Count => CapabilityTypes.Count;
    }
}
