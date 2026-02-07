namespace GamePlay.CapabilityFramework
{
    /// <summary>
    /// 所有运行时数据的基类。
    ///
    /// 设计意图：
    /// 1) State 只存数据，不放玩法流程逻辑；
    /// 2) Capability 只读/写 State，不直接互相调用；
    /// 3) 这样可以让逻辑可替换、可回放、可做存档序列化。
    /// </summary>
    public abstract class State
    {
    }
}
