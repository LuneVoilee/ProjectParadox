#region

using Core.Capability;

#endregion

namespace GamePlay.Strategy
{
    // 启动标记组件：Preset 安装它，CpNationRegistry 看到后执行一次 JSON -> 运行时索引转换。
    public class NationBootstrap : CComponent
    {
    }
}
