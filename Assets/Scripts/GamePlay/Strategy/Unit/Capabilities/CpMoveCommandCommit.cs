#region

using System.Collections.Generic;
using Common.Event;
using Core.Capability;
using GamePlay.Util;
using GamePlay.World;

#endregion

namespace GamePlay.Strategy
{
    // 移动命令提交：把 ValidMoveCommand 写入目标单位的 UnitMoveTarget。
    public class CpMoveCommandCommit : CapabilityBase
    {
        private static readonly int m_CombatStateId = Component<CombatState>.TId;
        private int m_RequestVersion;

        private readonly List<CEntity> m_Entities = new(16);

        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.OrderMoveCommandCommit;

        public override string Pipeline => CapabilityPipeline.MoveCommand;

        public override void Tick
            (CapabilityContext context, float deltaTime, float realElapsedSeconds)
        {
            context.QuerySnapshot<ValidMoveCommand>(m_Entities);
            for (int i = 0; i < m_Entities.Count; i++)
            {
                CommitOne(context, m_Entities[i]);
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

            // 如果单位正在战斗中，先退出战斗再执行移动。
            if (selectedUnit.TryGetCombatState(out CombatState combatState))
            {
                if (context.TryGetEntity(combatState.OpponentEntityId, out CEntity opponentUnit))
                {
                    context.Commands.RemoveComponent(opponentUnit, m_CombatStateId);
                }

                context.Commands.RemoveComponent(selectedUnit, m_CombatStateId);
            }

            if (selectedUnit.TryGetUnitMoveTarget(out UnitMoveTarget oldTarget) &&
                oldTarget.PathIndicatorId >= 0)
            {
                EventBus.GP_OnDestroyPathIndicator?.Invoke(oldTarget.PathIndicatorId);
            }

            int requestVersion = ++m_RequestVersion;
            context.Commands.AddComponent<UnitMoveTarget>(selectedUnit, target =>
            {
                target.DestinationHex = command.DestinationHex;
                target.Path = command.Path;
                target.NextPathIndex = 1;
                target.RequestVersion = requestVersion;
                target.PathIndicatorId = -1;
                target.StepTimer = 0f;
                target.VisualLerpProgress = 1f;
            });
        }
    }
}