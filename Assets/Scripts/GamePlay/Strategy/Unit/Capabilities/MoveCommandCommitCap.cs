#region

using Common.Event;
using Core.Capability;
using GamePlay.Util;
using GamePlay.World;

#endregion

namespace GamePlay.Strategy
{
    // 移动命令提交：把 ValidMoveCommand 写入目标单位的 UnitMoveTarget。
    public class MoveCommandCommitCap : CapabilityBase
    {
        private int m_RequestVersion;

        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.OrderMoveCommandCommit;

        public override void Tick
            (CapabilityContext context, float deltaTime, float realElapsedSeconds)
        {
            EntityGroup group = context.Query<ValidMoveCommand>();
            if (group?.EntitiesMap == null)
            {
                return;
            }

            foreach (CEntity commandEntity in group.EntitiesMap)
            {
                CommitOne(context, commandEntity);
            }
        }

        private void CommitOne(CapabilityContext context, CEntity commandEntity)
        {
            if (commandEntity == null)
            {
                return;
            }

            if (!commandEntity.TryGetValidMoveCommand(out ValidMoveCommand command))
            {
                return;
            }

            if (!context.TryGetEntity(command.UnitEntityId, out CEntity selectedUnit))
            {
                return;
            }

            if (selectedUnit.TryGetUnitMoveTarget(out UnitMoveTarget oldTarget) &&
                oldTarget.PathIndicatorId >= 0)
            {
                EventBus.GP_OnDestroyPathIndicator?.Invoke(oldTarget.PathIndicatorId);
            }

            UnitMoveTarget target = selectedUnit.AddComponent<UnitMoveTarget>();
            target.DestinationHex = command.DestinationHex;
            target.Path = command.Path;
            target.NextPathIndex = 1;
            target.RequestVersion = ++m_RequestVersion;
            target.PathIndicatorId = -1;
            target.StepTimer = 0f;
            target.VisualLerpProgress = 1f;
        }
    }
}
