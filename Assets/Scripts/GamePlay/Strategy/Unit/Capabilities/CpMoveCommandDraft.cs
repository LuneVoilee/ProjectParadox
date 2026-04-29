#region

using Core.Capability;
using GamePlay.Map;
using GamePlay.Util;
using GamePlay.World;

#endregion

namespace GamePlay.Strategy
{
    // 移动命令草拟：消费右键点击和当前选择，产出未校验的 MoveCommandRequest。
    public class CpMoveCommandDraft : CapabilityBase
    {
        private readonly System.Collections.Generic.List<CEntity> m_Entities =
            new System.Collections.Generic.List<CEntity>(16);

        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.OrderMoveCommandDraft;

        public override string DebugCategory => CapabilityDebugCategory.Command;

        public override void Tick
            (CapabilityContext context, float deltaTime, float realElapsedSeconds)
        {
            if (!TryGetSelection(context, out SelectionState selection))
            {
                return;
            }

            context.QuerySnapshot<GameplayClickEvent>(m_Entities);
            for (int i = 0; i < m_Entities.Count; i++)
            {
                CEntity eventEntity = m_Entities[i];
                if (!TryGetRightClick(eventEntity, out GameplayClickEvent click))
                {
                    continue;
                }

                if (selection.Kind != SelectionKind.Unit)
                {
                    context.Log("No selected unit.");
                    continue;
                }

                CreateRequest(context, selection.SelectedUnitEntityId, click.Hex);
            }
        }

        private static bool TryGetSelection
            (CapabilityContext context, out SelectionState selection)
        {
            selection = null;
            if (context.World is not GameWorld gameWorld)
            {
                return false;
            }

            if (!gameWorld.TryGetPrimaryMapEntity(out CEntity mapEntity))
            {
                return false;
            }

            return mapEntity.TryGetSelectionState(out selection);
        }

        private static bool TryGetRightClick
            (CEntity eventEntity, out GameplayClickEvent click)
        {
            click = null;
            if (eventEntity == null)
            {
                return false;
            }

            if (!eventEntity.TryGetGameplayClickEvent(out click))
            {
                return false;
            }

            if (!click.IsRightClick)
            {
                return false;
            }

            return click.HasHex;
        }

        private static void CreateRequest
        (
            CapabilityContext context, int unitEntityId,
            HexCoordinates destinationHex
        )
        {
            context.Commands.CreateEventEntity<MoveCommandRequest>(request =>
            {
                request.UnitEntityId = unitEntityId;
                request.DestinationHex = destinationHex;
            }, "MoveCommandRequest");
        }
    }
}
