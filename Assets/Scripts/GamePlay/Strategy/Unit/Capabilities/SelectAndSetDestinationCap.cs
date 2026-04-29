using Core.Capability;

namespace GamePlay.Strategy
{
    // 旧选择移动能力已拆分到 PlayerInput/Selection/MoveCommand/Presentation 系列 Cap。
    // 保留抽象类型只用于兼容文件资产，不参与 AllCapability 注册和运行时调度。
    public abstract class SelectAndSetDestinationCap : CapabilityBase
    {
    }
}
