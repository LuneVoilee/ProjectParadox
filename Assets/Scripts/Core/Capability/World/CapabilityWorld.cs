using System.Collections.Generic;

namespace Core.Capability
{
    public abstract class CapabilityWorld : CapabilityWorldBase
    {
        private CapabilityRegistry m_CapabilityRegistry;

        protected void InitCapabilities
            (int maxCapabilityCount, int maxTag, int estimatedEntityCount)
        {
            m_CapabilityRegistry = new CapabilityRegistry();
            m_CapabilityRegistry.Init(this, maxCapabilityCount, estimatedEntityCount);
        }

        public void BindGlobalCapability<TCapability>()
            where TCapability : CapabilityBase, new()
        {
            if (m_CapabilityRegistry == null)
            {
                return;
            }

            m_CapabilityRegistry.AddGlobal<TCapability>();
        }

        public override void RemoveChild(CEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            base.RemoveChild(entity);
        }

        public void GetGlobalCapabilities
        (
            List<CapabilityBase> updateCapabilities,
            List<CapabilityBase> fixedUpdateCapabilities
        )
        {
            if (m_CapabilityRegistry == null)
            {
                return;
            }

            m_CapabilityRegistry.GetGlobalCapabilities(updateCapabilities,
                fixedUpdateCapabilities);
        }

        public override void OnUpdate(float elapsedSeconds, float realElapsedSeconds)
        {
            base.OnUpdate(elapsedSeconds, realElapsedSeconds);
            m_CapabilityRegistry?.OnUpdate(DeltaTime, realElapsedSeconds);
        }

        public override void OnFixedUpdate(float elapsedSeconds, float realElapsedSeconds)
        {
            base.OnFixedUpdate(elapsedSeconds, realElapsedSeconds);
            m_CapabilityRegistry?.OnFixedUpdate(FixedDeltaTime, realElapsedSeconds);
        }

        public override void Dispose()
        {
            m_CapabilityRegistry?.Dispose();
            m_CapabilityRegistry = null;
            base.Dispose();
        }
    }
}
