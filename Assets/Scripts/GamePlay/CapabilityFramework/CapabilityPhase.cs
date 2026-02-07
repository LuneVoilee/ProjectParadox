namespace GamePlay.CapabilityFramework
{
    /// <summary>
    /// Capability 的执行阶段。
    ///
    /// 设计意图：
    /// - 不把所有逻辑都堆在一个 Update；
    /// - 把大流程切成可读、可控的阶段；
    /// - 在不同阶段调度不同 Capability，方便做回合制/结算制玩法。
    ///
    /// 你可以按项目需要继续扩展阶段。
    /// </summary>
    public enum CapabilityPhase
    {
        /// <summary>
        /// 每次 Tick 都会参与调度，不受阶段过滤。
        /// </summary>
        Always = 0,

        /// <summary>
        /// 启动阶段：初始化、首次装载。
        /// </summary>
        Bootstrap = 1,

        /// <summary>
        /// 回合前处理：收集输入、准备缓存。
        /// </summary>
        PreTurn = 2,

        /// <summary>
        /// 回合主逻辑：规则推进、AI 决策、命令消费。
        /// </summary>
        TurnLogic = 3,

        /// <summary>
        /// 结算阶段：战斗、资源、状态变化落地。
        /// </summary>
        Resolve = 4,

        /// <summary>
        /// 回合后处理：清理临时数据、产生事件。
        /// </summary>
        PostTurn = 5,

        /// <summary>
        /// 视图同步阶段：把 State 变化同步给渲染/UI。
        /// </summary>
        RenderSync = 6
    }
}
