using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Core.Capability.Editor
{
    /// <summary>
    ///     Editor 侧证据接收器：把 Runtime trace stream 转成 Temporal Debugger 帧内事件。
    /// </summary>
    internal sealed class CapabilityDebugTraceCapture : ICapabilityTraceSink
    {
        private const int MaxDepth = 2;
        private const int MaxItems = 5;

        private readonly List<CapabilityDebugTraceSnapshot> m_Pending =
            new List<CapabilityDebugTraceSnapshot>(128);
        private readonly HashSet<int> m_WatchedEntityIds = new HashSet<int>();
        private readonly Dictionary<int, Dictionary<string, string>> m_Before =
            new Dictionary<int, Dictionary<string, string>>(128);
        private readonly Dictionary<int, Dictionary<string, string>> m_After =
            new Dictionary<int, Dictionary<string, string>>(128);

        private bool m_IsRegistered;
        private bool m_IsRecording;
        private bool m_FollowTouchedEntities = true;

        public void Configure
        (
            bool isRecording,
            IEnumerable<int> watchedEntityIds,
            bool followTouchedEntities
        )
        {
            m_IsRecording = isRecording;
            m_FollowTouchedEntities = followTouchedEntities;
            m_WatchedEntityIds.Clear();
            if (watchedEntityIds != null)
            {
                foreach (int entityId in watchedEntityIds)
                {
                    if (entityId >= 0)
                    {
                        m_WatchedEntityIds.Add(entityId);
                    }
                }
            }
        }

        public void Register()
        {
            if (m_IsRegistered)
            {
                return;
            }

            CapabilityTraceStream.Register(this);
            m_IsRegistered = true;
        }

        public void Unregister()
        {
            if (!m_IsRegistered)
            {
                return;
            }

            CapabilityTraceStream.Unregister(this);
            m_IsRegistered = false;
            m_Before.Clear();
            m_After.Clear();
        }

        public void Clear()
        {
            m_Pending.Clear();
            m_Before.Clear();
            m_After.Clear();
        }

        public void Consume(int frameIndex, List<CapabilityDebugTraceSnapshot> destination)
        {
            if (destination == null)
            {
                return;
            }

            for (int i = 0; i < m_Pending.Count; i++)
            {
                CapabilityDebugTraceSnapshot trace = m_Pending[i];
                trace.FrameIndex = frameIndex;
                destination.Add(trace);
            }

            m_Pending.Clear();
        }

        public void OnCapabilityBeforeTick(CapabilityTraceContext context)
        {
            if (!m_IsRecording)
            {
                return;
            }

            CaptureWorld(context.World, m_Before);
        }

        public void OnCapabilityAfterTick(CapabilityTraceContext context)
        {
            if (!m_IsRecording)
            {
                return;
            }

            CaptureWorld(context.World, m_After);
            EmitCapabilityDelta(context);
            m_Before.Clear();
            m_After.Clear();
        }

        public void OnTraceEvent(CapabilityTraceEvent traceEvent)
        {
            if (!m_IsRecording || traceEvent == null)
            {
                return;
            }

            m_Pending.Add(new CapabilityDebugTraceSnapshot
            {
                Time = traceEvent.Time,
                Event = traceEvent.Event,
                WorldId = traceEvent.World?.Id ?? -1,
                WorldName = traceEvent.World?.Name,
                CapabilityId = traceEvent.Capability?.Id ?? -1,
                CapabilityType = traceEvent.Capability?.GetType().FullName,
                EntityId = traceEvent.EntityId,
                Path = traceEvent.Path,
                Value = traceEvent.Value,
                Prev = traceEvent.Prev
            });
        }

        private void EmitCapabilityDelta(CapabilityTraceContext context)
        {
            foreach (KeyValuePair<int, Dictionary<string, string>> pair in m_After)
            {
                int entityId = pair.Key;
                Dictionary<string, string> afterValues = pair.Value;
                m_Before.TryGetValue(entityId, out Dictionary<string, string> beforeValues);

                foreach (KeyValuePair<string, string> valuePair in afterValues)
                {
                    string previous = null;
                    bool hadPrevious = beforeValues != null &&
                                       beforeValues.TryGetValue(valuePair.Key, out previous);
                    if (hadPrevious && previous == valuePair.Value)
                    {
                        continue;
                    }

                    AddCapabilityDelta(context, entityId, valuePair.Key, valuePair.Value,
                        hadPrevious ? previous : null);
                }
            }

            foreach (KeyValuePair<int, Dictionary<string, string>> pair in m_Before)
            {
                if (m_After.ContainsKey(pair.Key))
                {
                    continue;
                }

                AddCapabilityDelta(context, pair.Key, "entity", "missing", "present");
            }
        }

        private void AddCapabilityDelta
        (
            CapabilityTraceContext context,
            int entityId,
            string path,
            string value,
            string previous
        )
        {
            m_Pending.Add(new CapabilityDebugTraceSnapshot
            {
                Time = context.Time,
                Event = "capability.delta",
                WorldId = context.World?.Id ?? -1,
                WorldName = context.World?.Name,
                CapabilityId = context.Capability?.Id ?? -1,
                CapabilityType = context.Capability?.GetType().FullName,
                EntityId = entityId,
                Pipeline = context.Capability?.Pipeline,
                Path = path,
                Value = value,
                Prev = previous
            });
        }

        private void CaptureWorld
        (
            CapabilityWorldBase world,
            Dictionary<int, Dictionary<string, string>> destination
        )
        {
            destination.Clear();
            if (world?.Children == null)
            {
                return;
            }

            bool captureAll = m_FollowTouchedEntities || m_WatchedEntityIds.Count == 0;
            foreach (CEntity entity in world.Children)
            {
                if (entity == null || !entity.IsActive)
                {
                    continue;
                }

                if (!captureAll && !m_WatchedEntityIds.Contains(entity.Id))
                {
                    continue;
                }

                var values = new Dictionary<string, string>(64);
                CaptureEntity(entity, values);
                destination[entity.Id] = values;
            }
        }

        private static void CaptureEntity
            (CEntity entity, Dictionary<string, string> destination)
        {
            destination["entity.name"] = entity.Name ?? string.Empty;
            destination["entity.version"] = entity.Version.ToString();
            if (entity.Components?.IndexList == null)
            {
                return;
            }

            List<int> indices = entity.Components.IndexList;
            for (int i = 0; i < indices.Count; i++)
            {
                CComponent component = entity.GetComponent(indices[i]);
                if (component == null)
                {
                    continue;
                }

                string prefix = $"comp.{component.GetType().FullName}";
                AppendValue(destination, prefix, component, 0);
            }
        }

        private static void AppendValue
        (
            Dictionary<string, string> destination,
            string path,
            object value,
            int depth
        )
        {
            if (value == null)
            {
                destination[path] = "null";
                return;
            }

            Type type = value.GetType();
            if (IsSimpleType(type) || value is Object)
            {
                destination[path] = CapabilityTraceStream.FormatValue(value);
                return;
            }

            if (depth >= MaxDepth)
            {
                destination[path] = CapabilityTraceStream.FormatValue(value);
                return;
            }

            if (value is IDictionary dictionary)
            {
                destination[$"{path}.Count"] = dictionary.Count.ToString();
                int index = 0;
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (index >= MaxItems)
                    {
                        destination[$"{path}.More"] = (dictionary.Count - index).ToString();
                        break;
                    }

                    AppendValue(destination, $"{path}[{entry.Key}]", entry.Value, depth + 1);
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
                        destination[$"{path}.More"] = "true";
                        break;
                    }

                    AppendValue(destination, $"{path}[{index}]", item, depth + 1);
                    index++;
                }

                destination[$"{path}.Count"] = index.ToString();
                return;
            }

            Type current = type;
            int fieldCount = 0;
            while (current != null && current != typeof(object))
            {
                FieldInfo[] fields = current.GetFields(
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (int i = 0; i < fields.Length; i++)
                {
                    if (fieldCount >= MaxItems)
                    {
                        destination[$"{path}.More"] = "true";
                        return;
                    }

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

                    AppendValue(destination, $"{path}.{DisplayFieldName(field.Name)}",
                        fieldValue, depth + 1);
                    fieldCount++;
                }

                current = current.BaseType;
            }
        }

        private static string DisplayFieldName(string fieldName)
        {
            const string suffix = ">k__BackingField";
            if (fieldName != null &&
                fieldName.StartsWith("<", StringComparison.Ordinal) &&
                fieldName.EndsWith(suffix, StringComparison.Ordinal))
            {
                return fieldName.Substring(1, fieldName.Length - suffix.Length - 1);
            }

            return fieldName;
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
    }
}
