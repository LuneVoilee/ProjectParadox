using System;
using System.Collections.Generic;

namespace GamePlay.CapabilityFramework
{
    /// <summary>
    /// Entity：Capability 架构里的宿主容器。
    ///
    /// 它负责：
    /// 1) 持有 State（数据）
    /// 2) 持有 Capability（逻辑）
    /// 3) 提供能力之间通讯基础设施（Tag / Request / Message）
    /// 4) 驱动 Scheduler Tick
    ///
    /// 它不负责：
    /// - 具体玩法算法（交给 Capability）
    /// - 渲染实现（交给 View/Bridge）
    /// </summary>
    public class Entity
    {
        private readonly Dictionary<Type, State> m_States = new Dictionary<Type, State>();
        private readonly List<Capability> m_Capabilities = new List<Capability>();

        private readonly HashSet<string> m_PersistentTags = new HashSet<string>(StringComparer.Ordinal);

        // 用引用计数管理激活标签，避免多个 Capability 共享同标签时提前被移除。
        private readonly Dictionary<string, int> m_ActiveTagRefCount = new Dictionary<string, int>(StringComparer.Ordinal);

        private readonly CapabilityScheduler m_Scheduler = new CapabilityScheduler();

        public TagMask Tags { get; } = new TagMask();
        public RequestBuffer Requests { get; } = new RequestBuffer();
        public EntityMessageBus Messages { get; } = new EntityMessageBus();

        public CapabilityScheduler Scheduler => m_Scheduler;

        /// <summary>
        /// 添加持久标签（不会随着 Capability 激活/退出自动移除）。
        /// </summary>
        public void AddPersistentTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return;
            }

            m_PersistentTags.Add(tag);
            RebuildTags();
        }

        /// <summary>
        /// 移除持久标签。
        /// </summary>
        public void RemovePersistentTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return;
            }

            if (m_PersistentTags.Remove(tag))
            {
                RebuildTags();
            }
        }

        /// <summary>
        /// 便捷接口：按条件设置持久标签。
        /// </summary>
        public void SetPersistentTag(string tag, bool value)
        {
            if (value)
            {
                AddPersistentTag(tag);
            }
            else
            {
                RemovePersistentTag(tag);
            }
        }

        /// <summary>
        /// 添加/替换 State。
        /// 同类型 State 只保留一个。
        /// </summary>
        public void SetState<TState>(TState state) where TState : State
        {
            m_States[typeof(TState)] = state;
        }

        /// <summary>
        /// 读取 State。
        /// 不存在会抛出异常，方便在开发期快速发现配置问题。
        /// </summary>
        public TState GetState<TState>() where TState : State
        {
            if (m_States.TryGetValue(typeof(TState), out var state))
            {
                return (TState)state;
            }

            throw new InvalidOperationException($"State not found: {typeof(TState).Name}");
        }

        public bool TryGetState<TState>(out TState state) where TState : State
        {
            if (m_States.TryGetValue(typeof(TState), out var found))
            {
                state = (TState)found;
                return true;
            }

            state = null;
            return false;
        }

        public void AddCapability(Capability capability)
        {
            if (capability == null || m_Capabilities.Contains(capability))
            {
                return;
            }

            m_Capabilities.Add(capability);
            capability.Attach(this);
            m_Scheduler.Add(capability);
        }

        public void RemoveCapability(Capability capability)
        {
            if (capability == null)
            {
                return;
            }

            if (!m_Capabilities.Remove(capability))
            {
                return;
            }

            m_Scheduler.Remove(capability);
            capability.Detach();
        }

        /// <summary>
        /// 外部驱动 Entity 执行一次指定 Phase。
        /// 例如：
        /// - 每帧调用 Always
        /// - 回合开始时依次调用 PreTurn / TurnLogic / Resolve / PostTurn / RenderSync
        /// </summary>
        public void Tick(CapabilityPhase phase, float deltaTime)
        {
            m_Scheduler.Tick(phase, deltaTime);
        }

        public void Clear()
        {
            for (int i = m_Capabilities.Count - 1; i >= 0; i--)
            {
                var capability = m_Capabilities[i];
                m_Scheduler.Remove(capability);
                capability.Detach();
            }

            m_Capabilities.Clear();
            m_States.Clear();
            m_PersistentTags.Clear();
            m_ActiveTagRefCount.Clear();
            Tags.Clear();
            Requests.ClearAll();
            Messages.Clear();
        }

        internal void AddActiveTags(Capability capability)
        {
            AddTagSet(capability?.GrantedTags);
            AddTagSet(capability?.BlockTags);
        }

        internal void RemoveActiveTags(Capability capability)
        {
            RemoveTagSet(capability?.GrantedTags);
            RemoveTagSet(capability?.BlockTags);
        }

        private void AddTagSet(TagMask tags)
        {
            if (tags == null)
            {
                return;
            }

            foreach (var tag in tags)
            {
                if (string.IsNullOrEmpty(tag))
                {
                    continue;
                }

                if (!m_ActiveTagRefCount.TryGetValue(tag, out var count))
                {
                    count = 0;
                }

                count += 1;
                m_ActiveTagRefCount[tag] = count;
                Tags.Add(tag);
            }
        }

        private void RemoveTagSet(TagMask tags)
        {
            if (tags == null)
            {
                return;
            }

            foreach (var tag in tags)
            {
                if (string.IsNullOrEmpty(tag))
                {
                    continue;
                }

                if (!m_ActiveTagRefCount.TryGetValue(tag, out var count))
                {
                    continue;
                }

                count -= 1;
                if (count <= 0)
                {
                    m_ActiveTagRefCount.Remove(tag);
                    // TagMask 目前是 add-only，清除时重建一次。
                    RebuildTags();
                }
                else
                {
                    m_ActiveTagRefCount[tag] = count;
                }
            }
        }

        private void RebuildTags()
        {
            Tags.Clear();

            foreach (var tag in m_PersistentTags)
            {
                Tags.Add(tag);
            }

            foreach (var kv in m_ActiveTagRefCount)
            {
                if (kv.Value > 0)
                {
                    Tags.Add(kv.Key);
                }
            }
        }
    }
}
