using System;
using Map.Common;

namespace Map.Components
{
    [Serializable]
    public class CGrid
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public CCell[] Cells { get; private set; }

        public CGrid(int width, int height)
        {
            Width = width;
            Height = height;
            Cells = new CCell[width * height];
        }

        public bool IsInBounds(int col, int row)
        {
            return col >= 0 && row >= 0 && col < Width && row < Height;
        }

        public CCell GetCellWrappedX(int col, int row)
        {
            if (row < 0 || row >= Height)
            {
                return null;
            }

            var wrappedCol = WrapIndex(col, Width);
            return Cells[wrappedCol + row * Width];
        }

        public CCell GetCellWrappedX(HexCoordinates coordinates)
        {
            var offset = coordinates.ToOffset();
            return GetCellWrappedX(offset.x, offset.y);
        }

        public CCell GetCell(int col, int row)
        {
            if (!IsInBounds(col, row))
            {
                return null;
            }

            return Cells[col + row * Width];
        }

        public void SetCell(int col, int row, CCell cell)
        {
            Cells[col + row * Width] = cell;
        }

        public CCell GetCell(HexCoordinates coordinates)
        {
            var offset = coordinates.ToOffset();
            return GetCell(offset.x, offset.y);
        }

        private static int WrapIndex(int value, int max)
        {
            if (max <= 0)
            {
                return 0;
            }

            var wrapped = value % max;
            return wrapped < 0 ? wrapped + max : wrapped;
        }
    }
}
