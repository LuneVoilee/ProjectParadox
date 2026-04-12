#region

using Core.Capability;

#endregion

namespace GamePlay.Map
{
    public struct Cell
    {
        public HexCoordinates Coordinates;

        public float Height;

        public TerrainType TerrainType;

        public byte OwnerId;
    }

    public class Grid : CComponent
    {
        public int Width;
        public int Height;

        public Cell[] Cells;
    }
}