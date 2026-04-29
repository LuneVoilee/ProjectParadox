#region

using Core.Capability;
using GamePlay.World;

#endregion

namespace GamePlay.Strategy
{
    // 一帧输入/命令事件清理：所有命令阶段消费结束后统一移除事件实体。
    public class InputEventCleanupCap : CapabilityBase
    {
        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.OrderMoveCommandCommit + 1;

        public override void Tick
            (CapabilityContext context, float deltaTime, float realElapsedSeconds)
        {
            context.Commands.RemoveEventEntities<GameplayClickEvent>();
            context.Commands.RemoveEventEntities<MoveCommandRequest>();
            context.Commands.RemoveEventEntities<ValidMoveCommand>();
            context.Commands.RemoveEventEntities<RejectedMoveCommand>();
        }
    }
}
