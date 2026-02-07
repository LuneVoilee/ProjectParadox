using System.Collections.Generic;
using Map.Common;

namespace Faction
{
    public class CFaction
    {
        private readonly HashSet<HexCoordinates> m_Territories = new HashSet<HexCoordinates>();
        private readonly CTerritoryOwnership m_TerritoryOwnership;

        public byte Id { get; }
        public string Name { get; }
        public int TerritoryCount => m_Territories.Count;
        public IReadOnlyCollection<HexCoordinates> Territories => m_Territories;

        public CFaction(byte id, string name, CTerritoryOwnership territoryOwnership)
        {
            Id = id;
            Name = name;
            m_TerritoryOwnership = territoryOwnership;
        }

        public bool TryAddTerritory(HexCoordinates coordinates)
        {
            if (m_TerritoryOwnership == null || m_Territories.Contains(coordinates))
            {
                return false;
            }

            if (!m_TerritoryOwnership.TryClaim(coordinates, Id))
            {
                return false;
            }

            m_Territories.Add(coordinates);
            return true;
        }

        public bool TryRemoveTerritory(HexCoordinates coordinates)
        {
            if (m_TerritoryOwnership == null || !m_Territories.Contains(coordinates))
            {
                return false;
            }

            if (!m_TerritoryOwnership.TryRelease(coordinates, Id))
            {
                return false;
            }

            m_Territories.Remove(coordinates);
            return true;
        }
    }
}
