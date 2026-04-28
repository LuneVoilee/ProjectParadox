#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Capability
{
    /// <summary>
    ///     Editor 调试日志桥：运行时 Capability 只负责写入，窗口采样时再绑定到具体帧。
    /// </summary>
    public static class CapabilityDebugLogBridge
    {
        public struct Entry
        {
            public double Time;
            public string Message;
        }

        private static readonly Dictionary<CapabilityBase, List<Entry>> m_Entries =
            new Dictionary<CapabilityBase, List<Entry>>(128);

        public static void Add(CapabilityBase capability, string message)
        {
            if (capability == null)
            {
                return;
            }

            if (!m_Entries.TryGetValue(capability, out List<Entry> entries))
            {
                entries = new List<Entry>(8);
                m_Entries.Add(capability, entries);
            }

            entries.Add(new Entry
            {
                Time = Time.realtimeSinceStartup,
                Message = message ?? string.Empty
            });
        }

        public static List<Entry> Consume(CapabilityBase capability)
        {
            if (capability == null)
            {
                return null;
            }

            if (!m_Entries.TryGetValue(capability, out List<Entry> entries))
            {
                return null;
            }

            m_Entries.Remove(capability);
            return entries;
        }

        public static void Clear()
        {
            m_Entries.Clear();
        }
    }
}
#endif
