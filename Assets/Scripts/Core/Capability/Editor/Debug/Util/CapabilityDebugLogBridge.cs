#if UNITY_EDITOR

#region

using System.Collections.Generic;

#endregion

namespace Core.Capability.Editor
{
    /// <summary>
    ///     Editor 调试日志桥：运行时 Capability 只负责写入，窗口采样时再绑定到具体帧。
    /// </summary>
    public static class CapabilityDebugLogBridge
    {
        public static void Add(CapabilityBase capability, string message)
        {
            CapabilityDebugLogStream.Add(capability, message);
        }

        public static List<CapabilityDebugLogStream.Entry> Consume(CapabilityBase capability)
        {
            return CapabilityDebugLogStream.Consume(capability);
        }

        public static void Clear()
        {
            CapabilityDebugLogStream.Clear();
        }
    }
}
#endif
