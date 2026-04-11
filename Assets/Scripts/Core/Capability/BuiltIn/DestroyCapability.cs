namespace Core.Capability
{
    public class DestroyCapability : CapabilityBase
    {
        public override int TickGroupOrder { get; protected set; } = int.MaxValue;

        protected override void OnInit()
        {
            Filter(ComponentId<DestroyComponent>.TId);
        }

        public override bool ShouldActivate()
        {
            return Owner.GetComponent(ComponentId<DestroyComponent>.TId) != null;
        }

        public override void OnActivated()
        {
            base.OnActivated();
            World.RemoveChild(Owner);
        }

        public override bool ShouldDeactivate()
        {
            return Owner.GetComponent(ComponentId<DestroyComponent>.TId) == null;
        }
    }
}
