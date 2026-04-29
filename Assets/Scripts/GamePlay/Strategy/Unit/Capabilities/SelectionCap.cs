#region

using Core.Capability;
using GamePlay.Map;
using GamePlay.Util;
using GamePlay.World;

#endregion

namespace GamePlay.Strategy
{
    // 玩家选择规则：消费左键点击事件，只修改 SelectionState，不直接创建表现物。
    public class SelectionCap : CapabilityBase
    {
        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.OrderUnitSelection;

        public override void Tick
            (CapabilityContext context, float deltaTime, float realElapsedSeconds)
        {
            if (!StrategyMapContext.TryCreate(context.World, out StrategyMapContext mapContext))
            {
                return;
            }

            if (!mapContext.MapEntity.TryGetSelectionState(out SelectionState selection))
            {
                return;
            }

            EntityGroup group = context.Query<GameplayClickEvent>();
            if (group?.EntitiesMap == null)
            {
                return;
            }

            foreach (CEntity eventEntity in group.EntitiesMap)
            {
                if (!TryGetLeftClick(eventEntity, out GameplayClickEvent click))
                {
                    continue;
                }

                HandleLeftClick(mapContext, selection, click.Hex);
            }
        }

        private static bool TryGetLeftClick
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

            if (click.IsRightClick)
            {
                return false;
            }

            return click.HasHex;
        }

        private static void HandleLeftClick
        (
            StrategyMapContext mapContext, SelectionState selection,
            HexCoordinates clickedHex
        )
        {
            bool hasUnit = mapContext.TryGetUnitAt(clickedHex, out CEntity clickedUnit);
            int unitId = hasUnit ? clickedUnit.Id : -1;

            if (selection.Kind == SelectionKind.None)
            {
                Select(selection, clickedHex, unitId,
                    hasUnit ? SelectionKind.Unit : SelectionKind.Cell);
                return;
            }

            if (clickedHex.Equals(selection.SelectedHex))
            {
                if (selection.Kind == SelectionKind.Unit)
                {
                    if (hasUnit)
                    {
                        selection.SelectedHex = clickedHex;
                        selection.SelectedUnitEntityId = unitId;
                        selection.Kind = SelectionKind.Cell;
                        selection.IndicatorDirty = true;
                        return;
                    }
                }

                ClearSelection(selection);
                return;
            }

            Select(selection, clickedHex, unitId,
                hasUnit ? SelectionKind.Unit : SelectionKind.Cell);
        }

        private static void Select
        (
            SelectionState selection, HexCoordinates hex, int unitEntityId,
            SelectionKind kind
        )
        {
            selection.SelectedHex = hex;
            selection.SelectedUnitEntityId = unitEntityId;
            selection.Kind = kind;
            selection.IndicatorDirty = true;
        }

        private static void ClearSelection(SelectionState selection)
        {
            selection.SelectedUnitEntityId = -1;
            selection.Kind = SelectionKind.None;
            selection.IndicatorDirty = true;
        }
    }
}
