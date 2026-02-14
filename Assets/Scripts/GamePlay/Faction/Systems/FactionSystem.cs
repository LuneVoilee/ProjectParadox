using Faction.Data;
using Map.Common;

namespace Faction.Systems
{
    public class FactionSystem
    {
        private readonly FactionData m_FactionData;
        private readonly TerritoryOwnershipSystem m_TerritoryOwnershipSystem;

        public FactionData Data => m_FactionData;

        public FactionSystem(FactionData factionData, TerritoryOwnershipSystem territoryOwnershipSystem)
        {
            m_FactionData = factionData;
            m_TerritoryOwnershipSystem = territoryOwnershipSystem;
        }

        public bool TryAddTerritory(HexCoordinates coordinates)
        {
            if (m_FactionData == null || m_TerritoryOwnershipSystem == null)
            {
                return false;
            }

            if (m_FactionData.ContainsTerritory(coordinates))
            {
                return false;
            }

            if (!m_TerritoryOwnershipSystem.TryClaim(coordinates, m_FactionData.Id))
            {
                return false;
            }

            return m_FactionData.AddTerritory(coordinates);
        }

        public bool TryRemoveTerritory(HexCoordinates coordinates)
        {
            if (m_FactionData == null || m_TerritoryOwnershipSystem == null)
            {
                return false;
            }

            if (!m_FactionData.ContainsTerritory(coordinates))
            {
                return false;
            }

            if (!m_TerritoryOwnershipSystem.TryRelease(coordinates, m_FactionData.Id))
            {
                return false;
            }

            return m_FactionData.RemoveTerritory(coordinates);
        }
    }
}
