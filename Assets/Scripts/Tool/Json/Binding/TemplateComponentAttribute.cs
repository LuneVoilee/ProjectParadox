using System;

namespace Tool.Json
{
    /// <summary>
    /// 声明组件可消费的模板类型。
    /// 约束：当前 JSON 流程按 1:1 设计，一个组件只允许声明一个模板类型。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class TemplateComponentAttribute : Attribute
    {
        public Type TemplateType { get; }

        public TemplateComponentAttribute(Type templateType)
        {
            TemplateType = templateType ?? throw new ArgumentNullException(nameof(templateType));
        }
    }
}
