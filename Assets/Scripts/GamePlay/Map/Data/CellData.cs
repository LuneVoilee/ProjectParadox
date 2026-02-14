using System;
using Map.Common;

namespace Map.Components
{
    [Serializable]
    public class CellData
    {
        public HexCoordinates Coordinates;
        public TerrainType TerrainType;
        public byte OwnerId;
        public float Height;
        public float Moisture;

        public CellData(HexCoordinates coordinates)
        {
            Coordinates = coordinates;
        }
    }
}
