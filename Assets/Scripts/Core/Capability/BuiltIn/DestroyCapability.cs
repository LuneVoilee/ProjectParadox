namespace Core.Capability
{
    public class DestroyCap : CapabilityBase
    {
        public override int TickGroupOrder { get; protected set; } = int.MaxValue;

        public override void Tick
            (CapabilityContext context, float deltaTime, float realElapsedSeconds)
        {
            EntityGroup group = context.Query<DestroyComponent>();
            if (group?.EntitiesMap == null)
            {
                return;
            }

            foreach (CEntity entity in group.EntitiesMap)
            {
                context.Commands.DestroyEntity(entity);
            }
        }
    }
}
