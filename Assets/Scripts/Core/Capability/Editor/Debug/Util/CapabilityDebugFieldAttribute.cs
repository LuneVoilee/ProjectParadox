#region

using System;

#endregion

namespace Core.Capability.Editor
{
    /// <summary>
    ///     标记 Capability 中需要被 Temporal Debugger 记录的运行时字段。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class CapabilityDebugFieldAttribute : Attribute
    {
    }
}