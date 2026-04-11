using System.Collections.Generic;

namespace Core.Capability
{
    public static class CapabilityWorldRegistry
    {
        private static readonly List<CapabilityWorldBase> m_Worlds = new List<CapabilityWorldBase>(8);

        public static IReadOnlyList<CapabilityWorldBase> Worlds => m_Worlds;

        internal static void Register(CapabilityWorldBase world)
        {
            if (world == null)
            {
                return;
            }

            if (m_Worlds.Contains(world))
            {
                return;
            }

            m_Worlds.Add(world);
        }

        internal static void Unregister(CapabilityWorldBase world)
        {
            if (world == null)
            {
                return;
            }

            m_Worlds.Remove(world);
        }
    }
}
