namespace Core.Capability
{
    public class CpDestroy : CapabilityBase
    {
        private readonly System.Collections.Generic.List<CEntity> m_Entities =
            new System.Collections.Generic.List<CEntity>(64);

        public override int TickGroupOrder { get; protected set; } = int.MaxValue;

        public override string DebugCategory => CapabilityDebugCategory.Cleanup;

        public override void Tick
            (CapabilityContext context, float deltaTime, float realElapsedSeconds)
        {
            context.QuerySnapshot<DestroyComponent>(m_Entities);
            for (int i = 0; i < m_Entities.Count; i++)
            {
                context.Commands.DestroyEntity(m_Entities[i]);
            }
        }
    }
}
