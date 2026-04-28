#region

using Core.Capability;

#endregion

namespace GamePlay.Strategy
{
    // 一次性标记组件。挂载在地图实体上，触发 DiplomacyRegistryCap 执行初始化。
    // 初始化完成后该组件被移除。
    public class DiplomacyBootstrap : CComponent
    {
    }
}
