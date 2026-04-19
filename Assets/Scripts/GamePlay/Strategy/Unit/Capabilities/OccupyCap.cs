#region

using Core.Capability;

#endregion

namespace GamePlay.Strategy.Capabilities
{
    public class OccupyCap : CapabilityBase
    {
        private static readonly int m_UnitId = Component<Unit>.TId;
        private static readonly int m_HexsId = Component<ChangedHexs>.TId;

        protected override void OnInit()
        {
            Filter(m_UnitId, m_HexsId);
        }

        public override bool ShouldActivate()
        {
            return true;
        }

        public override bool ShouldDeactivate()
        {
            return true;
        }

        protected override void OnActivated()
        {
            if (Owner.TryGetChangedHexs(out var changedHexs))
            {
                var hexes = changedHexs.Hexs;
                foreach (var hex in hexes)
                {
                }
            }

            if (Owner.TryGetDrawMap(out var drawMap))
            {
                drawMap.IsDirty = true;
            }
        }
    }
}