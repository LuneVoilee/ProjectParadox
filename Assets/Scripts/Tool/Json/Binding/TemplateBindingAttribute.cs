using System;

namespace Tool.Json
{
    /// <summary>
    /// 声明组件绑定来源：模板集合类型 + 模板类型 + 可选 slot。
    /// 一个组件可以声明多个绑定（典型场景：同模板集不同 slot）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class TemplateBindingAttribute : Attribute
    {
        public Type TemplateSetType { get; }

        public Type TemplateType { get; }

        public string Slot { get; set; } = string.Empty;

        public bool Optional { get; set; }

        public TemplateBindingAttribute(Type templateSetType, Type templateType)
        {
            TemplateSetType = templateSetType ?? throw new ArgumentNullException(nameof(templateSetType));
            TemplateType = templateType ?? throw new ArgumentNullException(nameof(templateType));
        }
    }
}
