using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Tool.Json
{
    /// <summary>
    /// 通过反射把模板对象写入组件对象。
    /// 规则：
    /// 1) JsonComponent: 默认绑定 public 字段/属性，除非标记 [TemplateIgnore]。
    /// 2) 普通 CComponent: 仅绑定标记了 [TemplateField] 的成员。
    /// </summary>
    public static class JsonTemplateBinder
    {
        private sealed class MemberWriter
        {
            public string Key;
            public bool Optional;
            public Type ValueType;
            public Action<object, object> Setter;
        }

        private static readonly Dictionary<Type, List<MemberWriter>> s_Cache =
            new Dictionary<Type, List<MemberWriter>>();

        /// <summary>
        /// 将 source 模板中的字段写入 target 组件。
        /// </summary>
        public static void Apply(object source, object target, bool invokeApplyCallback = true)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            Dictionary<string, object> sourceMap = BuildSourceMap(source);
            List<MemberWriter> writers = GetWriters(target.GetType());

            foreach (MemberWriter writer in writers)
            {
                if (!sourceMap.TryGetValue(writer.Key, out object raw))
                {
                    if (!writer.Optional)
                    {
                        Debug.LogWarning($"[JsonTemplateBinder] Missing key '{writer.Key}' for target {target.GetType().Name}.");
                    }

                    continue;
                }

                if (!TryConvert(raw, writer.ValueType, out object converted))
                {
                    Debug.LogError(
                        $"[JsonTemplateBinder] Convert failed key='{writer.Key}' from {raw?.GetType().Name ?? "null"} to {writer.ValueType.Name}.");
                    continue;
                }

                writer.Setter(target, converted);
            }

            if (invokeApplyCallback && target is JsonComponent jsonComponent)
            {
                jsonComponent.OnTemplateApplied();
            }
        }

        private static Dictionary<string, object> BuildSourceMap(object source)
        {
            // 使用大小写不敏感映射，减少 JSON/字段命名大小写差异导致的绑定失败。
            var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            Type srcType = source.GetType();

            foreach (FieldInfo field in srcType.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                map[field.Name] = field.GetValue(source);
            }

            foreach (PropertyInfo prop in srcType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!prop.CanRead || prop.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                map[prop.Name] = prop.GetValue(source);
            }

            return map;
        }

        private static List<MemberWriter> GetWriters(Type targetType)
        {
            if (s_Cache.TryGetValue(targetType, out List<MemberWriter> cached))
            {
                return cached;
            }

            var writers = new List<MemberWriter>();
            // JsonComponent 是“全自动模式”，普通 CComponent 是“显式标注模式”。
            bool fullAuto = typeof(JsonComponent).IsAssignableFrom(targetType);

            foreach (FieldInfo field in targetType.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.IsInitOnly)
                {
                    continue;
                }

                if (field.GetCustomAttribute<TemplateIgnoreAttribute>() != null)
                {
                    continue;
                }

                TemplateFieldAttribute attr = field.GetCustomAttribute<TemplateFieldAttribute>();
                if (!fullAuto && attr == null)
                {
                    continue;
                }

                writers.Add(new MemberWriter
                {
                    Key = !string.IsNullOrEmpty(attr?.Key) ? attr.Key : field.Name,
                    Optional = attr?.Optional ?? false,
                    ValueType = field.FieldType,
                    Setter = (obj, val) => field.SetValue(obj, val)
                });
            }

            foreach (PropertyInfo prop in targetType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!prop.CanWrite || prop.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                if (prop.GetCustomAttribute<TemplateIgnoreAttribute>() != null)
                {
                    continue;
                }

                TemplateFieldAttribute attr = prop.GetCustomAttribute<TemplateFieldAttribute>();
                if (!fullAuto && attr == null)
                {
                    continue;
                }

                writers.Add(new MemberWriter
                {
                    Key = !string.IsNullOrEmpty(attr?.Key) ? attr.Key : prop.Name,
                    Optional = attr?.Optional ?? false,
                    ValueType = prop.PropertyType,
                    Setter = (obj, val) => prop.SetValue(obj, val)
                });
            }

            s_Cache[targetType] = writers;
            return writers;
        }

        private static bool TryConvert(object value, Type targetType, out object converted)
        {
            if (value == null)
            {
                converted = null;
                return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null;
            }

            Type nonNullTarget = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (nonNullTarget.IsInstanceOfType(value))
            {
                converted = value;
                return true;
            }

            try
            {
                if (nonNullTarget.IsEnum)
                {
                    if (value is string s)
                    {
                        converted = Enum.Parse(nonNullTarget, s, true);
                        return true;
                    }

                    converted = Enum.ToObject(nonNullTarget, value);
                    return true;
                }

                converted = Convert.ChangeType(value, nonNullTarget);
                return true;
            }
            catch
            {
                converted = null;
                return false;
            }
        }
    }
}
