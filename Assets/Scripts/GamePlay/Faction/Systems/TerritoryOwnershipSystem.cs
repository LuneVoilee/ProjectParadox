using Map.Common;
using Map.Data;
using Map.View;

namespace Faction.Systems
{
    public class TerritoryOwnershipSystem
    {
        private readonly GridData m_Data;
        private readonly TerritoryBorderRenderer m_Renderer;

        public GridData Data => m_Data;

        public TerritoryOwnershipSystem(GridData data, TerritoryBorderRenderer renderer)
        {
            m_Data = data;
            m_Renderer = renderer;
        }

        public bool TryClaim(HexCoordinates coordinates, byte ownerId)
        {
            if (ownerId == 0 || m_Data == null)
            {
                return false;
            }

            var offset = coordinates.ToOffset();
            var cell = m_Data.GetCell(offset.x, offset.y);
            if (cell == null || cell.OwnerId != 0)
            {
                return false;
            }

            cell.OwnerId = ownerId;
            m_Renderer?.SetOwner(offset.x, offset.y, ownerId);
            return true;
        }

        public bool TryRelease(HexCoordinates coordinates, byte ownerId)
        {
            if (m_Data == null)
            {
                return false;
            }

            var offset = coordinates.ToOffset();
            var cell = m_Data.GetCell(offset.x, offset.y);
            if (cell == null || cell.OwnerId != ownerId)
            {
                return false;
            }

            cell.OwnerId = 0;
            m_Renderer?.SetOwner(offset.x, offset.y, 0);
            return true;
        }

        public void ApplyChanges()
        {
            m_Renderer?.ApplyChanges();
        }
    }
}
