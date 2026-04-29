#region

using System.Collections.Generic;
using Core.Capability;
using GamePlay.Map;
using GamePlay.Util;

#endregion

namespace GamePlay.Strategy
{
    // 单位路径服务。把外交/占用依赖从 HexMapUtility 中抽离出来。
    public static class PathfindingService
    {
        private static readonly HexDirection[] s_Directions =
        {
            HexDirection.NE,
            HexDirection.E,
            HexDirection.SE,
            HexDirection.SW,
            HexDirection.W,
            HexDirection.NW
        };

        public static bool TryFindUnitPath
        (
            in StrategyMapContext mapContext, CEntity unitEntity,
            HexCoordinates destination, List<HexCoordinates> result,
            out MoveRejectReason reason
        )
        {
            reason = MoveRejectReason.None;
            result?.Clear();
            if (unitEntity == null)
            {
                reason = MoveRejectReason.UnitMissing;
                return false;
            }

            if (result == null)
            {
                reason = MoveRejectReason.NoPath;
                return false;
            }

            if (!unitEntity.TryGetUnit(out Unit unit))
            {
                reason = MoveRejectReason.NoUnit;
                return false;
            }

            if (!unitEntity.TryGetUnitPosition(out UnitPosition position))
            {
                reason = MoveRejectReason.NoPosition;
                return false;
            }

            byte myNationId = NationUtility.GetIdOrDefault(mapContext.NationIndex, unit.Tag);
            if (!StrategyRules.CanUnitEnterHex(mapContext, unitEntity, destination, out reason))
            {
                return false;
            }

            bool found = TryFindPath(mapContext, position.Hex, destination,
                unitEntity.Id, myNationId, result);
            if (!found || result.Count < 2)
            {
                reason = MoveRejectReason.NoPath;
                return false;
            }

            return true;
        }

        private static bool TryFindPath
        (
            in StrategyMapContext mapContext, HexCoordinates start,
            HexCoordinates destination, int selfEntityId, byte myNationId,
            List<HexCoordinates> result
        )
        {
            if (!HexMapUtility.TryNormalizeHex(mapContext.Grid, start,
                    out HexCoordinates normalizedStart))
            {
                return false;
            }

            if (!HexMapUtility.TryNormalizeHex(mapContext.Grid, destination,
                    out HexCoordinates normalizedDestination))
            {
                return false;
            }

            if (!HexMapUtility.IsPassable(mapContext.Grid, normalizedDestination))
            {
                return false;
            }

            var frontier = new Queue<HexCoordinates>();
            var cameFrom = new Dictionary<HexCoordinates, HexCoordinates>(256);
            frontier.Enqueue(normalizedStart);
            cameFrom[normalizedStart] = normalizedStart;

            while (frontier.Count > 0)
            {
                HexCoordinates current = frontier.Dequeue();
                for (int i = 0; i < s_Directions.Length; i++)
                {
                    HexCoordinates next = current.GetNeighbor(s_Directions[i]);
                    if (!HexMapUtility.TryNormalizeHex(mapContext.Grid, next, out next))
                    {
                        continue;
                    }

                    if (cameFrom.ContainsKey(next))
                    {
                        continue;
                    }

                    if (!HexMapUtility.IsPassable(mapContext.Grid, next))
                    {
                        continue;
                    }

                    if (!CanPathThrough(mapContext, next, selfEntityId, myNationId))
                    {
                        continue;
                    }

                    cameFrom[next] = current;
                    if (next.Equals(normalizedDestination))
                    {
                        BuildPath(cameFrom, normalizedStart, normalizedDestination, result);
                        return true;
                    }

                    frontier.Enqueue(next);
                }
            }

            return false;
        }

        private static bool CanPathThrough
        (
            in StrategyMapContext mapContext, HexCoordinates hex,
            int selfEntityId, byte myNationId
        )
        {
            if (!HexMapUtility.TryGetCellIndex(mapContext.Grid, hex, out int cellIndex))
            {
                return false;
            }

            byte ownerId = mapContext.Grid.Cells[cellIndex].OwnerId;
            if (myNationId != ownerId &&
                mapContext.DiplomacyIndex.IsPeace(myNationId, ownerId))
            {
                return false;
            }

            if (!mapContext.Occupancy.TryGetUnit(hex, out int occupantEntityId))
            {
                return true;
            }

            if (occupantEntityId == selfEntityId)
            {
                return true;
            }

            CEntity occupant = mapContext.World.GetChild(occupantEntityId);
            if (occupant == null || !occupant.TryGetUnit(out Unit occupantUnit))
            {
                return false;
            }

            byte otherNationId =
                NationUtility.GetIdOrDefault(mapContext.NationIndex, occupantUnit.Tag);
            return mapContext.DiplomacyIndex.IsHostile(myNationId, otherNationId);
        }

        private static void BuildPath
        (
            Dictionary<HexCoordinates, HexCoordinates> cameFrom,
            HexCoordinates start, HexCoordinates destination,
            List<HexCoordinates> result
        )
        {
            result.Clear();
            HexCoordinates current = destination;
            result.Add(current);
            while (!current.Equals(start))
            {
                current = cameFrom[current];
                result.Add(current);
            }

            result.Reverse();
        }
    }
}
