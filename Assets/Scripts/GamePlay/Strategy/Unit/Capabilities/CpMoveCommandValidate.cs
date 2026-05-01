#region

using System.Collections.Generic;
using Core.Capability;
using GamePlay.Map;
using GamePlay.Util;
using GamePlay.World;

#endregion

namespace GamePlay.Strategy
{
    // 移动命令校验：用地图上下文、规则和寻路服务把请求转换为 Valid/Rejected 命令。
    public class CpMoveCommandValidate : CapabilityBase
    {
        private readonly List<HexCoordinates> m_PathBuffer = new List<HexCoordinates>(128);
        private readonly List<CEntity> m_Requests = new List<CEntity>(16);

        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.OrderMoveCommandValidate;

        public override string Pipeline => CapabilityPipeline.MoveCommand;

        public override void Tick
            (CapabilityContext context, float deltaTime, float realElapsedSeconds)
        {
            context.QuerySnapshot<MoveCommandRequest>(m_Requests);

            if (!StrategyMapContext.TryCreate(context.World, out StrategyMapContext mapContext))
            {
                RejectAll(context, m_Requests, MoveRejectReason.NoMapContext);
                return;
            }

            for (int i = 0; i < m_Requests.Count; i++)
            {
                ValidateOne(context, mapContext, m_Requests[i]);
            }
        }

        private void ValidateOne
        (
            CapabilityContext context, StrategyMapContext mapContext,
            CEntity requestEntity
        )
        {
            if (requestEntity == null)
            {
                return;
            }

            if (!requestEntity.TryGetMoveCommandRequest(out MoveCommandRequest request))
            {
                return;
            }

            if (!context.TryGetEntity(request.UnitEntityId, out CEntity selectedUnit))
            {
                CreateRejectedMove(context, request.UnitEntityId, request.DestinationHex,
                    MoveRejectReason.UnitMissing);
                return;
            }

            if (!PathfindingService.TryFindUnitPath(mapContext, selectedUnit,
                    request.DestinationHex, m_PathBuffer, out MoveRejectReason reason))
            {
                CreateRejectedMove(context, request.UnitEntityId, request.DestinationHex,
                    reason);
                context.Log($"Move rejected: {reason}");
                return;
            }

            CreateValidMove(context, selectedUnit.Id, request.DestinationHex);
        }

        private static void RejectAll
        (
            CapabilityContext context, List<CEntity> requests,
            MoveRejectReason reason
        )
        {
            for (int i = 0; i < requests.Count; i++)
            {
                CEntity requestEntity = requests[i];
                if (requestEntity == null)
                {
                    continue;
                }

                if (!requestEntity.TryGetMoveCommandRequest(out MoveCommandRequest request))
                {
                    continue;
                }

                CreateRejectedMove(context, request.UnitEntityId, request.DestinationHex,
                    reason);
            }
        }

        private void CreateValidMove
        (
            CapabilityContext context, int unitEntityId,
            HexCoordinates destinationHex
        )
        {
            context.Commands.CreateEventEntity<ValidMoveCommand>(valid =>
            {
                valid.UnitEntityId = unitEntityId;
                valid.DestinationHex = destinationHex;
                valid.Path = m_PathBuffer.ToArray();
            }, "ValidMoveCommand");
        }

        private static void CreateRejectedMove
        (
            CapabilityContext context, int unitId, HexCoordinates destinationHex,
            MoveRejectReason reason
        )
        {
            context.Commands.CreateEventEntity<RejectedMoveCommand>(rejected =>
            {
                rejected.UnitEntityId = unitId;
                rejected.DestinationHex = destinationHex;
                rejected.Reason = reason;
            }, "RejectedMoveCommand");
        }
    }
}
