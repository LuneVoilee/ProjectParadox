using System;
using System.Collections.Generic;
using System.Reflection;
using Core.Capability;
using Tool.Json.GameFramework;

namespace Tool.Json
{
    /// <summary>
    /// CEntity 组件自动模板绑定器。
    /// 触发点：
    /// 1) 组件新增时尝试绑定。
    /// 2) 实体模板 key 变更时，重试此前等待绑定的组件。
    /// </summary>
    internal static class JsonTemplateAutoBinder
    {
        private sealed class PendingBinding
        {
            public CComponent Component;
            public TemplateBindingAttribute Binding;
        }

        private static readonly Dictionary<Type, TemplateBindingAttribute[]> s_BindingCache =
            new Dictionary<Type, TemplateBindingAttribute[]>();

        private static readonly Dictionary<int, List<PendingBinding>> s_PendingByEntityId =
            new Dictionary<int, List<PendingBinding>>();

        private static readonly object s_Bootstrap = typeof(JsonTemplateAutoBinder);

        static JsonTemplateAutoBinder()
        {
            CEntity.ComponentAdded += OnComponentAdded;
            CEntity.TemplateKeysChanged += OnTemplateKeysChanged;
            CEntity.EntityDisposed += OnEntityDisposed;
        }

        public static void EnsureInitialized()
        {
            // 触发类型初始化，确保静态构造函数执行。
            _ = s_Bootstrap;
        }

        private static void OnComponentAdded(CEntity entity, CComponent component)
        {
            if (entity == null || component == null)
            {
                return;
            }

            TryBindComponent(entity, component);
        }

        private static void OnTemplateKeysChanged(CEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            if (!s_PendingByEntityId.TryGetValue(entity.Id, out List<PendingBinding> list) || list == null)
            {
                return;
            }

            for (int i = list.Count - 1; i >= 0; i--)
            {
                PendingBinding pending = list[i];
                if (pending?.Component == null || pending.Component.Owner != entity)
                {
                    list.RemoveAt(i);
                    continue;
                }

                if (TryBindOne(entity, pending.Component, pending.Binding, false))
                {
                    list.RemoveAt(i);
                }
            }

            if (list.Count == 0)
            {
                s_PendingByEntityId.Remove(entity.Id);
            }
        }

        private static void OnEntityDisposed(CEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            s_PendingByEntityId.Remove(entity.Id);
        }

        private static void TryBindComponent(CEntity entity, CComponent component)
        {
            TemplateBindingAttribute[] bindings = GetBindings(component.GetType());
            if (bindings.Length == 0)
            {
                return;
            }

            for (int i = 0; i < bindings.Length; i++)
            {
                TemplateBindingAttribute binding = bindings[i];
                if (!TryBindOne(entity, component, binding, true))
                {
                    AddPending(entity, component, binding);
                }
            }
        }

        private static bool TryBindOne
            (CEntity entity, CComponent component, TemplateBindingAttribute binding, bool logOnMissingKey)
        {
            if (!TryResolveTemplateKey(entity, binding, out object templateKey))
            {
                if (logOnMissingKey && !binding.Optional)
                {
                    Log.Warn(
                        $"[JsonAutoBinder] Missing template key for {component.GetType().Name} " +
                        $"set={binding.TemplateSetType.Name} slot={NormalizeSlot(binding.Slot)}.");
                }

                return false;
            }

            try
            {
                object template = GetTemplate(binding.TemplateSetType, binding.TemplateType, templateKey);
                if (template == null)
                {
                    if (logOnMissingKey && !binding.Optional)
                    {
                        Log.Error(
                            $"[JsonAutoBinder] Template not found. set={binding.TemplateSetType.Name} key={templateKey}");
                    }

                    return false;
                }

                JsonTemplateProcessor.AddFromTemplate(entity, component.GetType(), template, templateKey?.ToString());
                return true;
            }
            catch (Exception e)
            {
                if (!binding.Optional)
                {
                    Log.Exception(
                        $"[JsonAutoBinder] Bind failed component={component.GetType().Name} " +
                        $"set={binding.TemplateSetType.Name} key={templateKey}", e);
                }

                return false;
            }
        }

        private static bool TryResolveTemplateKey
            (CEntity entity, TemplateBindingAttribute binding, out object templateKey)
        {
            string slot = NormalizeSlot(binding.Slot);

            if (entity.TryGetTemplateKey(binding.TemplateSetType, out templateKey, slot))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(slot) &&
                entity.TryGetTemplateKey(binding.TemplateSetType, out templateKey, string.Empty))
            {
                return true;
            }

            return entity.TryGetDefaultTemplateKey(out templateKey);
        }

        private static object GetTemplate(Type templateSetType, Type templateType, object key)
        {
            Type baseSetType = typeof(BaseTemplateSet<,>).MakeGenericType(templateSetType, templateType);
            object setInstance = baseSetType.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                ?.GetValue(null);

            if (setInstance == null)
            {
                return null;
            }

            MethodInfo getTemplate = baseSetType.GetMethod("GetTemplate",
                BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(object) }, null);
            return getTemplate?.Invoke(setInstance, new[] { key });
        }

        private static TemplateBindingAttribute[] GetBindings(Type componentType)
        {
            if (componentType == null)
            {
                return Array.Empty<TemplateBindingAttribute>();
            }

            if (s_BindingCache.TryGetValue(componentType, out TemplateBindingAttribute[] cached))
            {
                return cached;
            }

            object[] attrs = componentType.GetCustomAttributes(typeof(TemplateBindingAttribute), true);
            if (attrs == null || attrs.Length == 0)
            {
                cached = Array.Empty<TemplateBindingAttribute>();
                s_BindingCache[componentType] = cached;
                return cached;
            }

            var list = new List<TemplateBindingAttribute>(attrs.Length);
            foreach (object attr in attrs)
            {
                if (attr is TemplateBindingAttribute binding)
                {
                    list.Add(binding);
                }
            }

            cached = list.ToArray();
            s_BindingCache[componentType] = cached;
            return cached;
        }

        private static void AddPending(CEntity entity, CComponent component, TemplateBindingAttribute binding)
        {
            List<PendingBinding> list = s_PendingByEntityId.GetOrNew(entity.Id);
            for (int i = 0; i < list.Count; i++)
            {
                PendingBinding pending = list[i];
                if (pending?.Component == component &&
                    pending.Binding != null &&
                    pending.Binding.TemplateSetType == binding.TemplateSetType &&
                    string.Equals(NormalizeSlot(pending.Binding.Slot), NormalizeSlot(binding.Slot), StringComparison.Ordinal))
                {
                    return;
                }
            }

            list.Add(new PendingBinding
            {
                Component = component,
                Binding = binding
            });
        }

        private static string NormalizeSlot(string slot)
        {
            return string.IsNullOrEmpty(slot) ? string.Empty : slot;
        }
    }
}
