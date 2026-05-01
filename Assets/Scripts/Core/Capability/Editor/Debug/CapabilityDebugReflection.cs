using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Core.Capability.Editor
{
    /// <summary>
    ///     反射采集器：负责把组件/能力字段转成稳定的 Inspector 展示快照。
    /// </summary>
    internal sealed class CapabilityDebugReflection
    {
        private const int MaxChildren = 5;
        private const int MaxDepth = 2;
        private const int MaxStringLength = 180;
        private const string BackingFieldSuffix = ">k__BackingField";

        private readonly Dictionary<Type, FieldInfo[]> m_AllFieldCache =
            new Dictionary<Type, FieldInfo[]>(128);

        private readonly Dictionary<Type, FieldInfo[]> m_CapabilityFieldCache =
            new Dictionary<Type, FieldInfo[]>(128);

        public void CaptureComponentFields
            (CComponent component, List<CapabilityDebugValueSnapshot> destination)
        {
            destination.Clear();
            if (component == null)
            {
                return;
            }

            FieldInfo[] fields = GetAllInstanceFields(component.GetType());
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (!ShouldDisplayField(field))
                {
                    continue;
                }

                object value = SafeGetValue(field, component);
                destination.Add(CreateValue(GetDisplayFieldName(field), field.FieldType, value, 0));
            }
        }

        public void CaptureCapabilityFields
            (CapabilityBase capability, List<CapabilityDebugValueSnapshot> destination)
        {
            destination.Clear();
            if (capability == null)
            {
                return;
            }

            FieldInfo[] fields = GetCapabilityDebugFields(capability.GetType());
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                object value = SafeGetValue(field, capability);
                destination.Add(CreateValue(GetDisplayFieldName(field), field.FieldType, value, 0));
            }
        }

        public void CollectSceneReferences
            (CComponent component, Dictionary<int, Transform> transforms)
        {
            if (component == null)
            {
                return;
            }

            FieldInfo[] fields = GetAllInstanceFields(component.GetType());
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (!ShouldDisplayField(field))
                {
                    continue;
                }

                object value = SafeGetValue(field, component);
                CollectSceneReference(value, transforms, 0);
            }
        }

        private FieldInfo[] GetAllInstanceFields(Type type)
        {
            if (type == null)
            {
                return Array.Empty<FieldInfo>();
            }

            if (m_AllFieldCache.TryGetValue(type, out FieldInfo[] fields))
            {
                return fields;
            }

            List<FieldInfo> result = new List<FieldInfo>(32);
            Type current = type;
            while (current != null && current != typeof(object))
            {
                FieldInfo[] declared = current.GetFields(
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (int i = 0; i < declared.Length; i++)
                {
                    FieldInfo field = declared[i];
                    if (field.IsStatic)
                    {
                        continue;
                    }

                    result.Add(field);
                }

                current = current.BaseType;
            }

            fields = result.ToArray();
            m_AllFieldCache.Add(type, fields);
            return fields;
        }

        private FieldInfo[] GetCapabilityDebugFields(Type type)
        {
            if (type == null)
            {
                return Array.Empty<FieldInfo>();
            }

            if (m_CapabilityFieldCache.TryGetValue(type, out FieldInfo[] fields))
            {
                return fields;
            }

            List<FieldInfo> result = new List<FieldInfo>(16);
            Type current = type;
            while (current != null &&
                   current != typeof(CapabilityBase) &&
                   current != typeof(object))
            {
                FieldInfo[] declared = current.GetFields(
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (int i = 0; i < declared.Length; i++)
                {
                    FieldInfo field = declared[i];
                    if (field.IsStatic)
                    {
                        continue;
                    }

                    if (!field.IsDefined(typeof(CapabilityDebugFieldAttribute), true))
                    {
                        continue;
                    }

                    result.Add(field);
                }

                current = current.BaseType;
            }

            fields = result.ToArray();
            m_CapabilityFieldCache.Add(type, fields);
            return fields;
        }

        private CapabilityDebugValueSnapshot CreateValue
            (string name, Type declaredType, object value, int depth)
        {
            CapabilityDebugValueSnapshot snapshot = new CapabilityDebugValueSnapshot
            {
                Name = name,
                TypeName = ToTypeName(declaredType),
                DisplayValue = ToDisplayValue(value, declaredType)
            };

            if (value == null || depth >= MaxDepth)
            {
                return snapshot;
            }

            if (value is string || value is Object)
            {
                return snapshot;
            }

            Type valueType = value.GetType();
            if (IsSimpleType(valueType))
            {
                return snapshot;
            }

            if (value is IDictionary dictionary)
            {
                AddDictionaryChildren(snapshot, dictionary, depth + 1);
                return snapshot;
            }

            if (value is IEnumerable enumerable)
            {
                AddEnumerableChildren(snapshot, enumerable, depth + 1);
                return snapshot;
            }

            if (valueType.IsValueType)
            {
                AddObjectChildren(snapshot, value, valueType, depth + 1);
            }

            return snapshot;
        }

        private void AddDictionaryChildren
            (CapabilityDebugValueSnapshot snapshot, IDictionary dictionary, int depth)
        {
            int index = 0;
            foreach (DictionaryEntry entry in dictionary)
            {
                if (index >= MaxChildren)
                {
                    snapshot.Children.Add(new CapabilityDebugValueSnapshot
                    {
                        Name = "...",
                        TypeName = string.Empty,
                        DisplayValue = $"剩余 {dictionary.Count - index} 项"
                    });
                    break;
                }

                string key = FormatObject(entry.Key);
                object value = entry.Value;
                snapshot.Children.Add(CreateValue(
                    $"[{key}]", value?.GetType() ?? typeof(object), value, depth));
                index++;
            }
        }

        private void AddEnumerableChildren
            (CapabilityDebugValueSnapshot snapshot, IEnumerable enumerable, int depth)
        {
            int index = 0;
            foreach (object item in enumerable)
            {
                if (index >= MaxChildren)
                {
                    snapshot.Children.Add(new CapabilityDebugValueSnapshot
                    {
                        Name = "...",
                        TypeName = string.Empty,
                        DisplayValue = "更多项已省略"
                    });
                    break;
                }

                snapshot.Children.Add(CreateValue(
                    $"[{index}]", item?.GetType() ?? typeof(object), item, depth));
                index++;
            }
        }

        private void AddObjectChildren
            (CapabilityDebugValueSnapshot snapshot, object value, Type type, int depth)
        {
            FieldInfo[] fields = GetAllInstanceFields(type);
            int displayedCount = 0;
            for (int i = 0; i < fields.Length; i++)
            {
                if (displayedCount >= MaxChildren)
                {
                    break;
                }

                FieldInfo field = fields[i];
                if (!ShouldDisplayField(field))
                {
                    continue;
                }

                object childValue = SafeGetValue(field, value);
                snapshot.Children.Add(CreateValue(
                    GetDisplayFieldName(field), field.FieldType, childValue, depth));
                displayedCount++;
            }

            int hiddenCount = CountDisplayableFields(fields) - displayedCount;
            if (hiddenCount > 0)
            {
                snapshot.Children.Add(new CapabilityDebugValueSnapshot
                {
                    Name = "...",
                    TypeName = string.Empty,
                    DisplayValue = $"剩余 {hiddenCount} 个字段"
                });
            }
        }

        private void CollectSceneReference
            (object value, Dictionary<int, Transform> transforms, int depth)
        {
            if (value == null || depth > 1)
            {
                return;
            }

            if (value is Transform transform)
            {
                AddTransform(transform, transforms);
                return;
            }

            if (value is GameObject gameObject)
            {
                AddTransform(gameObject.transform, transforms);
                return;
            }

            if (value is Component component)
            {
                AddTransform(component.transform, transforms);
                return;
            }

            // 集合里可能保存了 Transform 或 Component 引用，浅扫一层即可避免误入大对象图。
            if (value is IEnumerable enumerable && value is not string)
            {
                int count = 0;
                foreach (object item in enumerable)
                {
                    if (count >= MaxChildren)
                    {
                        break;
                    }

                    CollectSceneReference(item, transforms, depth + 1);
                    count++;
                }
            }
        }

        private static void AddTransform(Transform transform, Dictionary<int, Transform> transforms)
        {
            if (transform == null)
            {
                return;
            }

            int instanceId = transform.GetInstanceID();
            if (!transforms.ContainsKey(instanceId))
            {
                transforms.Add(instanceId, transform);
            }
        }

        private static object SafeGetValue(FieldInfo field, object owner)
        {
            try
            {
                return field.GetValue(owner);
            }
            catch (Exception e)
            {
                return $"<读取失败: {e.GetType().Name}>";
            }
        }

        private static string ToDisplayValue(object value, Type declaredType)
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
                if (unityObject == null)
                {
                    return "Missing Unity Object";
                }

                return $"{unityObject.name} ({unityObject.GetType().Name}, {unityObject.GetInstanceID()})";
            }

            Type valueType = value.GetType();
            if (value is IDictionary dictionary)
            {
                return $"Count={dictionary.Count}";
            }

            if (value is ICollection collection)
            {
                return $"Count={collection.Count}";
            }

            if (declaredType != null && declaredType.IsArray && value is Array array)
            {
                return $"Length={array.Length}";
            }

            return Trim(FormatObject(value));
        }

        private static string FormatObject(object value)
        {
            return value?.ToString() ?? "null";
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

        private static int CountDisplayableFields(FieldInfo[] fields)
        {
            int count = 0;
            for (int i = 0; i < fields.Length; i++)
            {
                if (ShouldDisplayField(fields[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool ShouldDisplayField(FieldInfo field)
        {
            if (field == null)
            {
                return false;
            }

            if (field.IsStatic)
            {
                return false;
            }

            string propertyName = TryGetBackingPropertyName(field.Name);
            if (propertyName == "Owner")
            {
                return false;
            }

            return true;
        }

        private static string GetDisplayFieldName(FieldInfo field)
        {
            string propertyName = TryGetBackingPropertyName(field.Name);
            return string.IsNullOrEmpty(propertyName)
                ? field.Name
                : propertyName;
        }

        private static string TryGetBackingPropertyName(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName))
            {
                return null;
            }

            if (!fieldName.StartsWith("<", StringComparison.Ordinal))
            {
                return null;
            }

            if (!fieldName.EndsWith(BackingFieldSuffix, StringComparison.Ordinal))
            {
                return null;
            }

            return fieldName.Substring(1, fieldName.Length - BackingFieldSuffix.Length - 1);
        }

        private static string ToTypeName(Type type)
        {
            if (type == null)
            {
                return string.Empty;
            }

            if (!type.IsGenericType)
            {
                return type.Name;
            }

            string name = type.Name;
            int tickIndex = name.IndexOf('`');
            if (tickIndex >= 0)
            {
                name = name.Substring(0, tickIndex);
            }

            Type[] args = type.GetGenericArguments();
            string[] argNames = new string[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                argNames[i] = ToTypeName(args[i]);
            }

            return $"{name}<{string.Join(", ", argNames)}>";
        }
    }
}
