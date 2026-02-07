using System;
using Map.Common;

namespace Map.Components
{
    [Serializable]
    public class CCell
    {
        public HexCoordinates Coordinates;
        public TerrainType TerrainType;
        public byte OwnerId;
        public float Height;
        public float Moisture;

        public CCell(HexCoordinates coordinates)
        {
            Coordinates = coordinates;
        }
    }
}
