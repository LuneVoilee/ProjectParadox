#region

using Core.Capability;
using GamePlay.Map;
using GamePlay.Util;

#endregion

namespace GamePlay.Strategy
{
    // Strategy 层权威规则集合。能力负责调度，规则类负责表达业务判断。
    public static class StrategyRules
    {
        public static bool CanUnitEnterHex
        (
            in StrategyMapContext mapContext, CEntity selectedUnit,
            HexCoordinates destinationHex, out MoveRejectReason reason
        )
        {
            reason = MoveRejectReason.None;
            if (selectedUnit == null)
            {
                reason = MoveRejectReason.UnitMissing;
                return false;
            }

            if (!selectedUnit.TryGetUnit(out Unit unit))
            {
                reason = MoveRejectReason.NoUnit;
                return false;
            }

            if (!selectedUnit.TryGetUnitPosition(out UnitPosition position))
            {
                reason = MoveRejectReason.NoPosition;
                return false;
            }

            if (position.Hex.Equals(destinationHex))
            {
                reason = MoveRejectReason.AlreadyThere;
                return false;
            }

            byte myNationId = NationUtility.GetIdOrDefault(mapContext.NationIndex, unit.Tag);
            if (!HexMapUtility.TryGetCellIndex(mapContext.Grid, destinationHex,
                    out int cellIndex))
            {
                reason = MoveRejectReason.NoPath;
                return false;
            }

            byte ownerId = mapContext.Grid.Cells[cellIndex].OwnerId;
            if (myNationId != ownerId &&
                mapContext.DiplomacyIndex.IsPeace(myNationId, ownerId))
            {
                reason = MoveRejectReason.ForbiddenByDiplomacy;
                return false;
            }

            if (!mapContext.Occupancy.TryGetUnit(destinationHex, out int occupantEntityId))
            {
                return true;
            }

            if (occupantEntityId == selectedUnit.Id)
            {
                return true;
            }

            CEntity occupant = mapContext.World.GetChild(occupantEntityId);
            if (occupant == null || !occupant.TryGetUnit(out Unit occupantUnit))
            {
                reason = MoveRejectReason.NoPath;
                return false;
            }

            byte otherNationId =
                NationUtility.GetIdOrDefault(mapContext.NationIndex, occupantUnit.Tag);
            if (!mapContext.DiplomacyIndex.IsHostile(myNationId, otherNationId))
            {
                reason = MoveRejectReason.ForbiddenByDiplomacy;
                return false;
            }

            return true;
        }
    }
}
