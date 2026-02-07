namespace GamePlay.CapabilityFramework
{
    /// <summary>
    /// Capability 基类（玩法逻辑单元）。
    /// 
    /// 标准生命周期：
    /// - ShouldActivate：是否进入激活态
    /// - OnActivated：进入激活态时执行一次
    /// - TickActive：激活态每次调度执行
    /// - ShouldDeactivate：是否退出激活态
    /// - OnDeactivated：退出激活态时执行一次
    ///
    /// 关键原则：
    /// - Capability 不直接依赖其它 Capability；
    /// - 通过 Entity 的 State/Tag/Request/Message 进行协作。
    /// </summary>
    public abstract class Capability
    {
        /// <summary>
        /// 该 Capability 的宿主 Entity。
        /// </summary>
        public Entity Entity { get; private set; }

        /// <summary>
        /// 是否当前处于激活态。
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// 调度阶段。默认 Always。
        /// </summary>
        public virtual CapabilityPhase Phase => CapabilityPhase.Always;

        /// <summary>
        /// 同阶段内优先级。值越小越先执行。
        /// </summary>
        public virtual int Priority => 0;

        /// <summary>
        /// 激活所需标签（Entity 必须全部具备）。
        /// </summary>
        public virtual TagMask RequiredTags => TagMask.Empty;

        /// <summary>
        /// 激活禁止标签（Entity 只要命中一个就不能激活）。
        /// </summary>
        public virtual TagMask ForbiddenTags => TagMask.Empty;

        /// <summary>
        /// 可被这些标签阻塞（命中任意一个则不可激活/会被关闭）。
        /// 与 ForbiddenTags 语义接近，但建议用来表达“外部阻塞”概念。
        /// </summary>
        public virtual TagMask BlockedByTags => TagMask.Empty;

        /// <summary>
        /// 激活期间附加给 Entity 的状态标签。
        /// 例如：State.GeneratingMap。
        /// </summary>
        public virtual TagMask GrantedTags => TagMask.Empty;

        /// <summary>
        /// 激活期间附加给 Entity 的阻塞标签。
        /// 例如：Block.MapEdit。
        /// </summary>
        public virtual TagMask BlockTags => TagMask.Empty;

        internal void Attach(Entity entity)
        {
            Entity = entity;
            OnAttached();
        }

        internal void Detach()
        {
            if (IsActive)
            {
                ForceDeactivate();
            }

            OnDetached();
            Entity = null;
        }

        internal void SchedulerTick(float deltaTime)
        {
            if (!IsActive)
            {
                if (CanActivate() && ShouldActivate())
                {
                    Activate();
                }

                return;
            }

            if (!CanStayActive() || ShouldDeactivate())
            {
                Deactivate();
                return;
            }

            TickActive(deltaTime);
        }

        /// <summary>
        /// Capability 挂载到 Entity 时触发一次。
        /// </summary>
        protected virtual void OnAttached()
        {
        }

        /// <summary>
        /// Capability 从 Entity 卸载时触发一次。
        /// </summary>
        protected virtual void OnDetached()
        {
        }

        /// <summary>
        /// 是否允许激活（可覆写做额外判定）。
        /// </summary>
        protected virtual bool ShouldActivate()
        {
            return true;
        }

        /// <summary>
        /// 激活时回调。
        /// </summary>
        protected virtual void OnActivated()
        {
        }

        /// <summary>
        /// 激活态 Tick。
        /// </summary>
        protected virtual void TickActive(float deltaTime)
        {
        }

        /// <summary>
        /// 是否允许/需要退出激活态。
        /// </summary>
        protected virtual bool ShouldDeactivate()
        {
            return false;
        }

        /// <summary>
        /// 退出激活态回调。
        /// </summary>
        protected virtual void OnDeactivated()
        {
        }

        /// <summary>
        /// 快捷访问宿主 State。
        /// </summary>
        protected TState GetState<TState>() where TState : State
        {
            return Entity.GetState<TState>();
        }

        /// <summary>
        /// 快捷访问宿主请求缓冲区。
        /// </summary>
        protected RequestBuffer Requests => Entity.Requests;

        /// <summary>
        /// 快捷访问宿主消息总线。
        /// </summary>
        protected EntityMessageBus Messages => Entity.Messages;

        /// <summary>
        /// 快捷访问宿主标签集合。
        /// </summary>
        protected TagMask Tags => Entity.Tags;

        private bool CanActivate()
        {
            if (Entity == null)
            {
                return false;
            }

            if (!Entity.Tags.ContainsAll(RequiredTags))
            {
                return false;
            }

            if (Entity.Tags.Intersects(ForbiddenTags))
            {
                return false;
            }

            if (Entity.Tags.Intersects(BlockedByTags))
            {
                return false;
            }

            return true;
        }

        private bool CanStayActive()
        {
            // 这里复用同一套判定，保证标签状态变化会及时让 Capability 下线。
            return CanActivate();
        }

        private void Activate()
        {
            IsActive = true;
            Entity.AddActiveTags(this);
            OnActivated();
        }

        private void Deactivate()
        {
            OnDeactivated();
            Entity.RemoveActiveTags(this);
            IsActive = false;
        }

        private void ForceDeactivate()
        {
            OnDeactivated();
            Entity.RemoveActiveTags(this);
            IsActive = false;
        }
    }
}
