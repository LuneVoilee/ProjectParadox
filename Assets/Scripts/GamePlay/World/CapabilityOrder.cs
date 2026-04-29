namespace GamePlay.World
{
    public static class CapabilityOrder
    {
        // 阶段1：场景启动与静态数据准备。
        // 重点：只做一次性初始化，不放每帧规则逻辑。
        private const int ScenarioBootstrap = 0;

        // 阶段2：战场态势刷新（原 IntelRefresh）。
        // 重点：刷新视野/控制区/威胁与路径缓存，产出“可决策态势”。
        private const int VisibilityAndControlRefresh = 100;

        // 阶段3：命令草拟。
        // 重点：玩家/AI写入意图组件，不直接改权威状态。
        private const int OrderDraft = 200;

        // 阶段4：命令提交。
        // 重点：做合法性校验、资源锁定、优先级排序。
        private const int OrderCommit = 300;

        // 阶段5：规则结算。
        // 重点：集中处理移动/交战/伤害/占领等权威规则结果。
        private const int Resolve = 400;

        // 阶段6：表现同步。
        // 重点：相机、地图渲染、UI动画同步，尽量不改核心规则状态。
        private const int PresentationSync = 600;

        // 阶段7：清理由内置 DestroyCapability 负责（int.MaxValue），这里不重复定义。
        public const int StageScenarioBootstrap = ScenarioBootstrap;
        public const int StageVisibilityAndControlRefresh = VisibilityAndControlRefresh;
        public const int StageOrderDraft = OrderDraft;
        public const int StageOrderCommit = OrderCommit;
        public const int StageResolve = Resolve;
        public const int StagePresentationSync = PresentationSync;

        public const int ScenarioMapGenerate = StageScenarioBootstrap + 10;

        public const int OrderPlayerInput = StageOrderDraft + 5;
        public const int OrderUnitSelection = StageOrderDraft + 10;
        public const int OrderMoveCommandDraft = StageOrderDraft + 20;
        public const int OrderMoveCommandValidate = StageOrderCommit + 10;
        public const int OrderMoveCommandCommit = StageOrderCommit + 20;
        public const int ResolveCombatEngage = StageResolve - 10;
        public const int ResolveUnitMovement = StageResolve;
        public const int ResolveUnitOccupy = StageResolve + 10;
        public const int ResolveCombatResolve = StageResolve + 30;
        public const int ResolveCombatRecovery = StageResolve + 40;

        public const int PresentationSelection = StagePresentationSync + 5;
        public const int PresentationPath = StagePresentationSync + 6;
        public const int PresentationCameraZoom = StagePresentationSync + 10;
        public const int PresentationCameraMove = StagePresentationSync + 20;
        public const int PresentationCameraBounds = StagePresentationSync + 30;
        public const int PresentationMapDraw = StagePresentationSync + 40;
    }
}
