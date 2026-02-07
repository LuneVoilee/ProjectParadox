using System;
using System.Collections.Generic;

namespace GamePlay.CapabilityFramework
{
    /// <summary>
    /// 请求缓冲区（Request / Consume 模式）。
    ///
    /// 设计意图：
    /// - 把“瞬时输入/命令”从 Capability 中抽离，避免 A Capability 直接调 B Capability；
    /// - 例如 InputCapability 写入请求，GenerateMapCapability 消费请求。
    ///
    /// 用法：
    /// - Push<T>(request) 生产请求
    /// - TryConsume<T>(out request) 消费一条
    /// - ConsumeAll<T>() 批量消费
    /// </summary>
    public sealed class RequestBuffer
    {
        private readonly Dictionary<Type, Queue<object>> m_Queues = new Dictionary<Type, Queue<object>>();

        public int GetCount<T>()
        {
            return TryGetQueue(typeof(T), out var queue) ? queue.Count : 0;
        }

        public void Push<T>(T request)
        {
            var type = typeof(T);
            var queue = GetOrCreateQueue(type);
            queue.Enqueue(request);
        }

        public bool TryConsume<T>(out T request)
        {
            var type = typeof(T);
            if (TryGetQueue(type, out var queue) && queue.Count > 0)
            {
                request = (T)queue.Dequeue();
                return true;
            }

            request = default;
            return false;
        }

        public List<T> ConsumeAll<T>()
        {
            var result = new List<T>();
            while (TryConsume<T>(out var request))
            {
                result.Add(request);
            }

            return result;
        }

        public void Clear<T>()
        {
            var type = typeof(T);
            if (TryGetQueue(type, out var queue))
            {
                queue.Clear();
            }
        }

        public void ClearAll()
        {
            foreach (var kv in m_Queues)
            {
                kv.Value.Clear();
            }
        }

        private Queue<object> GetOrCreateQueue(Type type)
        {
            if (!m_Queues.TryGetValue(type, out var queue))
            {
                queue = new Queue<object>();
                m_Queues[type] = queue;
            }

            return queue;
        }

        private bool TryGetQueue(Type type, out Queue<object> queue)
        {
            return m_Queues.TryGetValue(type, out queue);
        }
    }
}
