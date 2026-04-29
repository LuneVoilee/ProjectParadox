#region

using Core;
using Core.Capability;
using GamePlay.Map;
using GamePlay.World;
using UnityEngine;

#endregion

namespace GamePlay.Strategy
{
    // 玩家输入入口：把 InputManager 的本帧点击转换成一帧事件实体。
    public class PlayerInputCap : CapabilityBase
    {
        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.OrderPlayerInput;

        public override void Tick
            (CapabilityContext context, float deltaTime, float realElapsedSeconds)
        {
            if (!StrategyMapContext.TryCreate(context.World, out StrategyMapContext mapContext,
                    true))
            {
                return;
            }

            InputManager inputManager = InputManager.Instance;
            if (inputManager == null)
            {
                return;
            }

            if (inputManager.TryConsumeGameplayClick(out Vector2 left))
            {
                CreateClickEvent(context, mapContext, left, false);
            }

            if (inputManager.TryConsumeGameplayRightClick(out Vector2 right))
            {
                CreateClickEvent(context, mapContext, right, true);
            }
        }

        private static void CreateClickEvent
        (
            CapabilityContext context, StrategyMapContext mapContext,
            Vector2 screenPosition, bool isRightClick
        )
        {
            bool hasHex = mapContext.TryScreenToHex(screenPosition,
                out HexCoordinates hex, out Vector3Int cell);
            context.Commands.CreateEventEntity<GameplayClickEvent>(click =>
            {
                click.ScreenPosition = screenPosition;
                click.IsRightClick = isRightClick;
                click.Hex = hex;
                click.Cell = cell;
                click.HasHex = hasHex;
            }, isRightClick ? "GameplayClick_Right" : "GameplayClick_Left");
        }
    }
}
