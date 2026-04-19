#region

using System.Collections.Generic;

#endregion

namespace Core.Capability
{
    public abstract class CapabilityWorld : CapabilityWorldBase
    {
        private int m_MaxCapabilityTag;

        private CapabilityRegistry m_CapabilityRegistry;

        static CapabilityWorld()
        {
            // 触发 Tool.Json 自动绑定器初始化（若未引入 Tool.Json，不产生实际绑定行为）。
            Tool.Json.JsonTemplateAutoBinder.EnsureInitialized();
        }

        private void InitializeEntityCapabilities(CEntity entity)
        {
            if (entity == null || m_CapabilityRegistry == null)
            {
                return;
            }

            BindCapability<DestroyCapability>(entity);
            CapabilityBlockComponent blockComponent =
                entity.AddComponent<CapabilityBlockComponent>();
            blockComponent.Init(m_MaxCapabilityTag);
        }

        protected void InitCapabilities
            (int maxCapabilityCount, int maxTag, int estimatedEntityCount)
        {
            m_MaxCapabilityTag = maxTag;
            m_CapabilityRegistry = new CapabilityRegistry();
            m_CapabilityRegistry.Init(this, maxCapabilityCount, estimatedEntityCount);
        }

        public override TEntity AddChild<TEntity>()
        {
            TEntity child = base.AddChild<TEntity>();
            InitializeEntityCapabilities(child);
            return child;
        }

        public override CEntity AddChild(string name = null)
        {
            CEntity child = base.AddChild(name);
            InitializeEntityCapabilities(child);
            return child;
        }


        public override void RemoveChild(CEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            m_CapabilityRegistry?.RemoveAllByEntity(entity);
            base.RemoveChild(entity);
        }

        public void GetCapabilities
        (
            CEntity entity, List<CapabilityBase> updateCapabilities,
            List<CapabilityBase> fixedUpdateCapabilities
        )
        {
            if (entity == null || m_CapabilityRegistry == null)
            {
                return;
            }

            m_CapabilityRegistry.GetCapabilitiesByEntity(entity, updateCapabilities,
                fixedUpdateCapabilities);
        }

        public void BindCapability<TCapability>
            (CEntity entity) where TCapability : CapabilityBase, new()
        {
            if (entity == null || m_CapabilityRegistry == null)
            {
                return;
            }

            m_CapabilityRegistry.Add<TCapability>(entity);
        }

        public void UnbindCapability<TCapability>
            (CEntity entity) where TCapability : CapabilityBase, new()
        {
            if (entity == null || m_CapabilityRegistry == null)
            {
                return;
            }

            TCapability capability = new TCapability();
            int capabilityId = capability.UpdateMode == CapabilityUpdateMode.FixedUpdate
                ? CapabilityId<TCapability, IFixedUpdateSystem>.TId
                : CapabilityId<TCapability, IUpdateSystem>.TId;

            UnbindCapability(entity, capabilityId);
        }

        public void UnbindCapability(CEntity entity, int capabilityId)
        {
            if (entity == null || m_CapabilityRegistry == null)
            {
                return;
            }

            m_CapabilityRegistry.Remove(entity, capabilityId);
        }

        public bool IsCapabilityBlocked(CEntity entity, List<int> tagIndices)
        {
            if (entity == null || tagIndices == null)
            {
                return false;
            }

            CapabilityBlockComponent blockComponent =
                (CapabilityBlockComponent)entity.GetComponent(Component<CapabilityBlockComponent>
                    .TId);
            return blockComponent != null && blockComponent.IsBlocked(tagIndices);
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
