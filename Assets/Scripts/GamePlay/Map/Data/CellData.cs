using System;
using Map.Core;

namespace Map.Data
{
    [Serializable]
    public class CellData
    {
        public HexCoordinates Coordinates;
        public TerrainType TerrainType;
        public float Height;
        public float Moisture;

        public CellData(HexCoordinates coordinates)
        {
            Coordinates = coordinates;
        }
    }
}
