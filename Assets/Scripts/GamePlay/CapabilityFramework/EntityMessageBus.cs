using System;
using System.Collections.Generic;

namespace GamePlay.CapabilityFramework
{
    /// <summary>
    /// Entity 内部消息总线（本地通讯）。
    ///
    /// 回答你的问题 #2：
    /// - 可以，让 Entity 负责自己 Capability 之间通讯；
    /// - 但建议通过消息总线间接通讯，不直接拿 Capability 实例互调。
    ///
    /// 这个总线是“同一 Entity 内”的轻量总线：
    /// - Publish<T>(msg)
    /// - Subscribe<T>(handler)
    /// - Unsubscribe<T>(handler)
    ///
    /// 注意：
    /// - 它适合即时本地事件；
    /// - 若要跨帧、可回放的命令，优先用 RequestBuffer。
    /// </summary>
    public sealed class EntityMessageBus
    {
        private readonly Dictionary<Type, Delegate> m_Handlers = new Dictionary<Type, Delegate>();

        public void Subscribe<T>(Action<T> handler)
        {
            if (handler == null)
            {
                return;
            }

            var type = typeof(T);
            if (m_Handlers.TryGetValue(type, out var existing))
            {
                m_Handlers[type] = Delegate.Combine(existing, handler);
            }
            else
            {
                m_Handlers[type] = handler;
            }
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null)
            {
                return;
            }

            var type = typeof(T);
            if (!m_Handlers.TryGetValue(type, out var existing))
            {
                return;
            }

            var updated = Delegate.Remove(existing, handler);
            if (updated == null)
            {
                m_Handlers.Remove(type);
            }
            else
            {
                m_Handlers[type] = updated;
            }
        }

        public void Publish<T>(T message)
        {
            var type = typeof(T);
            if (!m_Handlers.TryGetValue(type, out var existing))
            {
                return;
            }

            if (existing is Action<T> handler)
            {
                handler.Invoke(message);
            }
        }

        public void Clear()
        {
            m_Handlers.Clear();
        }
    }
}
