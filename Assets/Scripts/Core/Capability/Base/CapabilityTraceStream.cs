using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Core.Capability
{
    /// <summary>
    ///     Capability 调试证据接收器。默认没有接收器时，运行时代码只做一次 HasSinks 判断。
    /// </summary>
    public interface ICapabilityTraceSink
    {
        void OnCapabilityBeforeTick(CapabilityTraceContext context);

        void OnCapabilityAfterTick(CapabilityTraceContext context);

        void OnTraceEvent(CapabilityTraceEvent traceEvent);
    }

    public readonly struct CapabilityTraceContext
    {
        public readonly CapabilityWorldBase World;
        public readonly CapabilityBase Capability;
        public readonly double Time;

        public CapabilityTraceContext
        (
            CapabilityWorldBase world,
            CapabilityBase capability,
            double time
        )
        {
            World = world;
            Capability = capability;
            Time = time;
        }
    }

    public sealed class CapabilityTraceEvent
    {
        public string Event;
        public CapabilityWorldBase World;
        public CapabilityBase Capability;
        public CEntity Entity;
        public int EntityId = -1;
        public string Path;
        public string Value;
        public string Prev;
        public double Time;
    }

    /// <summary>
    ///     Runtime-safe 的调试证据流。Editor Debugger 会注册 Sink 消费这些事件。
    /// </summary>
    public static class CapabilityTraceStream
    {
        private const int MaxDepth = 2;
        private const int MaxItems = 24;
        private const int MaxStringLength = 220;

        private static readonly List<ICapabilityTraceSink> m_Sinks =
            new List<ICapabilityTraceSink>(4);

        private static readonly List<ICapabilityTraceSink> m_NotifyBuffer =
            new List<ICapabilityTraceSink>(4);

        public static bool HasSinks => m_Sinks.Count > 0;

        public static void Register(ICapabilityTraceSink sink)
        {
            if (sink == null || m_Sinks.Contains(sink))
            {
                return;
            }

            m_Sinks.Add(sink);
        }

        public static void Unregister(ICapabilityTraceSink sink)
        {
            if (sink == null)
            {
                return;
            }

            m_Sinks.Remove(sink);
        }

        public static void CapabilityBeforeTick
            (CapabilityWorldBase world, CapabilityBase capability)
        {
            if (!HasSinks)
            {
                return;
            }

            var context = new CapabilityTraceContext(
                world, capability, Time.realtimeSinceStartup);
            Notify(context, beforeTick: true);
        }

        public static void CapabilityAfterTick
            (CapabilityWorldBase world, CapabilityBase capability)
        {
            if (!HasSinks)
            {
                return;
            }

            var context = new CapabilityTraceContext(
                world, capability, Time.realtimeSinceStartup);
            Notify(context, beforeTick: false);
        }

        public static void Phase
        (
            CapabilityBase capability,
            string phase,
            CEntity entity = null,
            string path = null,
            object value = null
        )
        {
            if (!HasSinks)
            {
                return;
            }

            Emit(new CapabilityTraceEvent
            {
                Event = "phase",
                World = capability?.World ?? entity?.World,
                Capability = capability,
                Entity = entity,
                EntityId = entity?.Id ?? -1,
                Path = string.IsNullOrEmpty(path) ? phase : $"{phase}.{path}",
                Value = value == null ? null : FormatValue(value),
                Time = Time.realtimeSinceStartup
            });
        }

        public static void CommandQueued
        (
            CapabilityBase capability,
            string command,
            CEntity entity,
            string path,
            string value
        )
        {
            EmitCommand("command.queue", capability, command, entity, path, value);
        }

        public static void CommandFlushed
        (
            CapabilityBase capability,
            string command,
            CEntity entity,
            string path,
            string value
        )
        {
            EmitCommand("command.flush", capability, command, entity, path, value);
        }

        public static void EntityLifecycle(string eventName, CEntity entity)
        {
            if (!HasSinks || entity == null)
            {
                return;
            }

            Emit(new CapabilityTraceEvent
            {
                Event = eventName,
                World = entity.World,
                Entity = entity,
                EntityId = entity.Id,
                Path = "entity",
                Value = entity.Name,
                Time = Time.realtimeSinceStartup
            });
        }

        public static void ComponentLifecycle
        (
            string eventName,
            CEntity entity,
            CComponent component
        )
        {
            if (!HasSinks || entity == null || component == null)
            {
                return;
            }

            Emit(new CapabilityTraceEvent
            {
                Event = eventName,
                World = entity.World,
                Entity = entity,
                EntityId = entity.Id,
                Path = $"component.{component.GetType().FullName}",
                Value = CaptureObjectFields(component),
                Time = Time.realtimeSinceStartup
            });
        }

        public static void Log(CapabilityBase capability, string message)
        {
            if (!HasSinks)
            {
                return;
            }

            Emit(new CapabilityTraceEvent
            {
                Event = "log",
                World = capability?.World,
                Capability = capability,
                Path = "log",
                Value = message ?? string.Empty,
                Time = Time.realtimeSinceStartup
            });
        }

        public static string CaptureObjectFields(object value)
        {
            if (value == null)
            {
                return "null";
            }

            var builder = new StringBuilder(256);
            AppendObject(builder, string.Empty, value, 0);
            return builder.ToString();
        }

        public static string FormatValue(object value)
        {
            if (value == null)
            {
                return "null";
            }

            if (value is string text)
            {
                return Trim(text);
            }

            if (value is Object unityObject)
            {
                return unityObject == null
                    ? "Missing Unity Object"
                    : $"{unityObject.name} ({unityObject.GetType().Name}, {unityObject.GetInstanceID()})";
            }

            return Trim(value.ToString());
        }

        private static void EmitCommand
        (
            string eventName,
            CapabilityBase capability,
            string command,
            CEntity entity,
            string path,
            string value
        )
        {
            if (!HasSinks)
            {
                return;
            }

            Emit(new CapabilityTraceEvent
            {
                Event = eventName,
                World = capability?.World ?? entity?.World,
                Capability = capability,
                Entity = entity,
                EntityId = entity?.Id ?? -1,
                Path = string.IsNullOrEmpty(path) ? command : $"{command}.{path}",
                Value = value,
                Time = Time.realtimeSinceStartup
            });
        }

        private static void Notify(CapabilityTraceContext context, bool beforeTick)
        {
            m_NotifyBuffer.Clear();
            m_NotifyBuffer.AddRange(m_Sinks);
            for (int i = 0; i < m_NotifyBuffer.Count; i++)
            {
                try
                {
                    if (beforeTick)
                    {
                        m_NotifyBuffer[i].OnCapabilityBeforeTick(context);
                    }
                    else
                    {
                        m_NotifyBuffer[i].OnCapabilityAfterTick(context);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static void Emit(CapabilityTraceEvent traceEvent)
        {
            m_NotifyBuffer.Clear();
            m_NotifyBuffer.AddRange(m_Sinks);
            for (int i = 0; i < m_NotifyBuffer.Count; i++)
            {
                try
                {
                    m_NotifyBuffer[i].OnTraceEvent(traceEvent);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static void AppendObject
            (StringBuilder builder, string prefix, object value, int depth)
        {
            if (value == null)
            {
                AppendPair(builder, prefix, "null");
                return;
            }

            Type type = value.GetType();
            if (IsSimpleType(type) || value is Object)
            {
                AppendPair(builder, prefix, FormatValue(value));
                return;
            }

            if (depth >= MaxDepth)
            {
                AppendPair(builder, prefix, FormatValue(value));
                return;
            }

            if (value is IDictionary dictionary)
            {
                int index = 0;
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (index >= MaxItems)
                    {
                        AppendPair(builder, $"{prefix}...", $"remaining={dictionary.Count - index}");
                        break;
                    }

                    string childPrefix = AppendPath(prefix, $"[{FormatValue(entry.Key)}]");
                    AppendObject(builder, childPrefix, entry.Value, depth + 1);
                    index++;
                }

                return;
            }

            if (value is IEnumerable enumerable && value is not string)
            {
                int index = 0;
                foreach (object item in enumerable)
                {
                    if (index >= MaxItems)
                    {
                        AppendPair(builder, $"{prefix}...", "more");
                        break;
                    }

                    AppendObject(builder, AppendPath(prefix, $"[{index}]"), item, depth + 1);
                    index++;
                }

                return;
            }

            FieldInfo[] fields = type.GetFields(
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            for (int i = 0; i < fields.Length && i < MaxItems; i++)
            {
                FieldInfo field = fields[i];
                if (field.IsStatic || field.Name == "Owner")
                {
                    continue;
                }

                object fieldValue;
                try
                {
                    fieldValue = field.GetValue(value);
                }
                catch (Exception exception)
                {
                    fieldValue = $"<read failed: {exception.GetType().Name}>";
                }

                AppendObject(builder, AppendPath(prefix, field.Name), fieldValue, depth + 1);
            }
        }

        private static void AppendPair(StringBuilder builder, string path, string value)
        {
            if (builder.Length > 0)
            {
                builder.Append("; ");
            }

            builder.Append(string.IsNullOrEmpty(path) ? "value" : path);
            builder.Append('=');
            builder.Append(Trim(value));
        }

        private static string AppendPath(string prefix, string name)
        {
            return string.IsNullOrEmpty(prefix) ? name : $"{prefix}.{name}";
        }

        private static bool IsSimpleType(Type type)
        {
            if (type.IsPrimitive || type.IsEnum)
            {
                return true;
            }

            return type == typeof(string) ||
                   type == typeof(decimal) ||
                   type == typeof(DateTime) ||
                   type == typeof(Vector2) ||
                   type == typeof(Vector2Int) ||
                   type == typeof(Vector3) ||
                   type == typeof(Vector3Int) ||
                   type == typeof(Vector4) ||
                   type == typeof(Quaternion) ||
                   type == typeof(Color) ||
                   type == typeof(Color32) ||
                   type == typeof(Rect) ||
                   type == typeof(Bounds);
        }

        private static string Trim(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value ?? string.Empty;
            }

            return value.Length <= MaxStringLength
                ? value
                : value.Substring(0, MaxStringLength) + "...";
        }
    }
}
