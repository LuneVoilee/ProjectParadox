#region

using Core.Capability;

#endregion

namespace NewGamePlay
{
    public class GameWorld : CapabilityWorld
    {
        private int m_PrimaryMapEntityId = -1;

        public override void OnInitialize(int maxComponentCount)
        {
            base.OnInitialize(maxComponentCount);
            int capabilityCount = AllCapability.TotalCapabilities > 0 ? AllCapability.TotalCapabilities : 1;
            InitCapabilities(maxCapabilityCount: capabilityCount, maxTag: 64, estimatedEntityCount: 512);
        }

        // 统一维护“主地图实体”入口，避免各能力自行扫描 World.Children。
        public void RegisterPrimaryMapEntity(CEntity entity)
        {
            m_PrimaryMapEntityId = entity?.Id ?? -1;
        }

        public bool TryGetPrimaryMapEntity(out CEntity entity)
        {
            entity = null;
            if (m_PrimaryMapEntityId < 0 || Children == null)
            {
                return false;
            }

            entity = GetChild(m_PrimaryMapEntityId);
            if (entity != null)
            {
                return true;
            }

            m_PrimaryMapEntityId = -1;
            return false;
        }

        public override void RemoveChild(CEntity entity)
        {
            if (entity != null && entity.Id == m_PrimaryMapEntityId)
            {
                m_PrimaryMapEntityId = -1;
            }

            base.RemoveChild(entity);
        }
    }
}
