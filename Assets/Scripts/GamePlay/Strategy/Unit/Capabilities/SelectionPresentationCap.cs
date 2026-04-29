#region

using Common.Event;
using Core.Capability;
using GamePlay.Util;
using GamePlay.World;
using UnityEngine;

#endregion

namespace GamePlay.Strategy
{
    // 选择表现同步：根据 SelectionState 创建、更新或销毁选择指示器。
    public class SelectionPresentationCap : CapabilityBase
    {
        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.PresentationSelection;

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

            SyncSelectedHexToUnit(mapContext, selection);
            RefreshSelectionIndicator(mapContext, selection);
        }

        public override void Dispose()
        {
            if (World is not GameWorld gameWorld)
            {
                base.Dispose();
                return;
            }

            if (!gameWorld.TryGetPrimaryMapEntity(out CEntity mapEntity))
            {
                base.Dispose();
                return;
            }

            if (mapEntity.TryGetSelectionState(out SelectionState selection))
            {
                DestroySelectionIndicator(selection);
            }

            base.Dispose();
        }

        private static void SyncSelectedHexToUnit
            (StrategyMapContext mapContext, SelectionState selection)
        {
            if (selection.Kind != SelectionKind.Unit)
            {
                return;
            }

            CEntity unit = mapContext.World.GetChild(selection.SelectedUnitEntityId);
            if (unit == null || !unit.TryGetUnitPosition(out UnitPosition position))
            {
                ClearSelection(selection);
                return;
            }

            if (position.Hex.Equals(selection.SelectedHex))
            {
                return;
            }

            selection.SelectedHex = position.Hex;
            selection.IndicatorDirty = true;
        }

        private static void RefreshSelectionIndicator
            (StrategyMapContext mapContext, SelectionState selection)
        {
            if (selection.Kind == SelectionKind.None)
            {
                DestroySelectionIndicator(selection);
                selection.IndicatorDirty = false;
                return;
            }

            Vector3 reference = mapContext.Camera != null
                ? mapContext.Camera.transform.position
                : Vector3.zero;
            Vector3 position = mapContext.GetNearestWorldPosition(selection.SelectedHex, reference);
            if (selection.IndicatorId < 0)
            {
                selection.IndicatorId =
                    EventBus.GP_OnCreateSelectionIndicator?.Invoke(position) ?? -1;
                selection.IndicatorDirty = false;
                return;
            }

            EventBus.GP_OnUpdateSelectionIndicator?.Invoke(selection.IndicatorId, position);
            selection.IndicatorDirty = false;
        }

        private static void ClearSelection(SelectionState selection)
        {
            selection.SelectedUnitEntityId = -1;
            selection.Kind = SelectionKind.None;
            selection.IndicatorDirty = true;
        }

        private static void DestroySelectionIndicator(SelectionState selection)
        {
            if (selection.IndicatorId < 0)
            {
                return;
            }

            EventBus.GP_OnDestroySelectionIndicator?.Invoke(selection.IndicatorId);
            selection.IndicatorId = -1;
        }
    }
}
