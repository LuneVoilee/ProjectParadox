using System.Collections.Generic;
using Map.Common;

namespace Faction.Data
{
    public class FactionData
    {
        private readonly HashSet<HexCoordinates> m_Territories = new();

        public byte Id { get; }
        public string Name { get; }
        public int TerritoryCount => m_Territories.Count;
        public IReadOnlyCollection<HexCoordinates> Territories => m_Territories;

        public FactionData(byte id, string name)
        {
            Id = id;
            Name = name;
        }

        public bool ContainsTerritory(HexCoordinates coordinates)
        {
            return m_Territories.Contains(coordinates);
        }

        public bool AddTerritory(HexCoordinates coordinates)
        {
            return m_Territories.Add(coordinates);
        }

        public bool RemoveTerritory(HexCoordinates coordinates)
        {
            return m_Territories.Remove(coordinates);
        }
    }
}
