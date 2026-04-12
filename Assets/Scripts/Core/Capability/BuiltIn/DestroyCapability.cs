namespace Core.Capability
{
    public class DestroyCapability : CapabilityBase
    {
        public override int TickGroupOrder { get; protected set; } = int.MaxValue;

        protected override void OnInit()
        {
            Filter(Component<DestroyComponent>.TId);
        }

        public override bool ShouldActivate()
        {
            return Owner.GetComponent(Component<DestroyComponent>.TId) != null;
        }

        protected override void OnActivated()
        {
            World.RemoveChild(Owner);
        }

        public override bool ShouldDeactivate()
        {
            return Owner.GetComponent(Component<DestroyComponent>.TId) == null;
        }
    }
}