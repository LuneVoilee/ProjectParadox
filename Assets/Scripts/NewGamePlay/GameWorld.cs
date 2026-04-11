#region

using Core.Capability;

#endregion

namespace NewGamePlay
{
    public class GameWorld : CapabilityWorld
    {
        public override void OnInitialize(int maxComponentCount)
        {
            base.OnInitialize(maxComponentCount);
            int capabilityCount = AllCapability.TotalCapabilities > 0 ? AllCapability.TotalCapabilities : 1;
            InitCapabilities(maxCapabilityCount: capabilityCount, maxTag: 64, estimatedEntityCount: 512);
        }
    }
}
