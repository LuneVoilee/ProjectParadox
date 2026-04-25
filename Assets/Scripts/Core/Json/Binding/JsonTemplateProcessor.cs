#region

using System;
using System.Reflection;
using Core.Capability;

#endregion

namespace Core.Json
{
    /// <summary>
    /// JSON 模板绑定入口（单组件版本）。
    /// 约束：一次调用只绑定一个组件类型，一个组件类型只对应一个模板类型。
    /// </summary>
    public static class JsonTemplateProcessor
    {
        /// <summary>
        /// 从模板集合中读取模板，然后绑定到指定组件。
        /// </summary>
        public static TComponent AddFromTemplateSet<TComponent, TSet, TTemplate>
        (
            CEntity entity,
            object templateKey,
            string keyName = ""
        )
            where TComponent : CComponent, new()
            where TSet : BaseTemplateSet<TSet, TTemplate>
            where TTemplate : class
        {
            if (entity == null)
            {
                return null;
            }

            // 先校验映射关系，避免加载模板后才发现类型不匹配。
            ValidateTemplateMapping(typeof(TComponent), typeof(TTemplate));

            BaseTemplateSet<TSet, TTemplate> set = BaseTemplateSet<TSet, TTemplate>.Instance;
            TTemplate template = string.IsNullOrEmpty(keyName)
                ? set.GetTemplate(templateKey)
                : set.GetTemplate(keyName, templateKey);

            return AddFromTemplate<TComponent>(entity, template, templateKey?.ToString());
        }

        public static TComponent AddComponentFromTemplate<TComponent>
            (CEntity entity, object templateKey)
            where TComponent : CComponent, new()
        {
            if (entity == null)
            {
                return null;
            }

            TemplateComponentAttribute attr = GetTemplateComponentAttribute(typeof(TComponent));
            object template = GetTemplate(attr.TemplateType, templateKey);
            return AddFromTemplate<TComponent>(entity, template, templateKey?.ToString());
        }

        public static TComponent ApplyTemplate<TComponent>
            (CEntity entity, object templateKey)
            where TComponent : CComponent, new()
        {
            if (entity == null)
            {
                return null;
            }

            TemplateComponentAttribute attr = GetTemplateComponentAttribute(typeof(TComponent));
            object template = GetTemplate(attr.TemplateType, templateKey);

            int componentId = Component<TComponent>.TId;
            TComponent component = entity.GetComponent(componentId) as TComponent;
            if (component == null)
            {
                component = entity.AddComponent<TComponent>();
            }

            ValidateTemplateMapping(typeof(TComponent), template.GetType());
            ApplyTemplateToComponent(component, template, templateKey?.ToString());
            return component;
        }

        /// <summary>
        /// 将模板数据绑定到运行时组件（反射版本，适用于运行时动态类型）。
        /// </summary>
        public static CComponent AddFromTemplate
        (
            CEntity entity,
            Type componentType,
            object template,
            string templateId = null
        )
        {
            if (entity == null)
            {
                return null;
            }

            if (componentType == null)
            {
                throw new ArgumentNullException(nameof(componentType));
            }

            if (!typeof(CComponent).IsAssignableFrom(componentType))
            {
                throw new ArgumentException($"Type is not CComponent: {componentType.FullName}",
                    nameof(componentType));
            }

            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            ValidateTemplateMapping(componentType, template.GetType());

            int componentId = ComponentIdGenerator.GetId(componentType);
            CComponent component = entity.GetComponent(componentId);
            if (component == null)
            {
                component = entity.AddComponent(componentType);
            }

            ApplyTemplateToComponent(component, template, templateId);
            return component;
        }

        /// <summary>
        /// 将模板数据绑定到指定组件（泛型版本）。
        /// </summary>
        public static TComponent AddFromTemplate<TComponent>
        (
            CEntity entity,
            object template,
            string templateId = null
        )
            where TComponent : CComponent, new()
        {
            if (entity == null)
            {
                return null;
            }

            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            ValidateTemplateMapping(typeof(TComponent), template.GetType());

            TComponent component = entity.AddComponent<TComponent>();
            ApplyTemplateToComponent(component, template, templateId);

            return component;
        }

        /// <summary>
        /// 组件与模板必须显式一一映射：组件上必须有 [TemplateComponent] 且类型兼容。
        /// </summary>
        private static void ValidateTemplateMapping(Type componentType, Type actualTemplateType)
        {
            TemplateComponentAttribute attr = GetTemplateComponentAttribute(componentType);

            if (!attr.TemplateType.IsAssignableFrom(actualTemplateType))
            {
                throw new ArgumentException(
                    $"Template type mismatch for {componentType.FullName}. " +
                    $"Expected {attr.TemplateType.FullName}, actual {actualTemplateType.FullName}.");
            }
        }

        private static TemplateComponentAttribute GetTemplateComponentAttribute(Type componentType)
        {
            TemplateComponentAttribute attr =
                componentType.GetCustomAttribute<TemplateComponentAttribute>(true);
            if (attr == null)
            {
                throw new InvalidOperationException(
                    $"{componentType.FullName} missing [TemplateComponent].");
            }

            if (attr.TemplateType == null)
            {
                throw new InvalidOperationException(
                    $"{componentType.FullName} has invalid [TemplateComponent].");
            }

            return attr;
        }

        private static object GetTemplate(Type templateType, object templateKey)
        {
            if (templateKey == null)
            {
                throw new ArgumentNullException(nameof(templateKey));
            }

            Type templateSetType = JsonTemplateRegistry.GetTemplateSetType(templateType);
            Type baseSetType = typeof(BaseTemplateSet<,>).MakeGenericType(templateSetType, templateType);
            object setInstance = baseSetType.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                ?.GetValue(null);

            if (setInstance == null)
            {
                throw new InvalidOperationException(
                    $"Template set instance is null: {templateSetType.FullName}.");
            }

            MethodInfo getTemplate = baseSetType.GetMethod("GetTemplate",
                BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(object) }, null);
            if (getTemplate == null)
            {
                throw new MissingMethodException(baseSetType.FullName, "GetTemplate");
            }

            return getTemplate.Invoke(setInstance, new[] { templateKey });
        }

        private static void ApplyTemplateToComponent
            (CComponent component, object template, string templateId)
        {
            if (component is JsonComponent jsonComponent)
            {
                jsonComponent.ApplyTemplate(template, templateId);
                return;
            }

            JsonTemplateBinder.Apply(template, component);
        }
    }
}
