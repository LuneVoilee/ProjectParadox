using System;
using Map.Core;

namespace Map.Data
{
    [Serializable]
    public class GridData
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public CellData[] Cells { get; private set; }

        public GridData(int width, int height)
        {
            Width = width;
            Height = height;
            Cells = new CellData[width * height];
        }

        public bool IsInBounds(int col, int row)
        {
            return col >= 0 && row >= 0 && col < Width && row < Height;
        }

        public CellData GetCellWrappedX(int col, int row)
        {
            if (row < 0 || row >= Height)
            {
                return null;
            }

            var wrappedCol = WrapIndex(col, Width);
            return Cells[wrappedCol + row * Width];
        }

        public CellData GetCellWrappedX(HexCoordinates coordinates)
        {
            var offset = coordinates.ToOffset();
            return GetCellWrappedX(offset.x, offset.y);
        }

        public CellData GetCell(int col, int row)
        {
            if (!IsInBounds(col, row))
            {
                return null;
            }

            return Cells[col + row * Width];
        }

        public void SetCell(int col, int row, CellData cell)
        {
            Cells[col + row * Width] = cell;
        }

        public CellData GetCell(HexCoordinates coordinates)
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
