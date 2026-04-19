using System;

namespace Tool.Json
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TemplateFieldAttribute : Attribute
    {
        public string Key { get; }

        public bool Optional { get; set; }

        public TemplateFieldAttribute()
        {
        }

        public TemplateFieldAttribute(string key)
        {
            Key = key;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TemplateIgnoreAttribute : Attribute
    {
    }

    /// <summary>
    /// JSON 驱动组件基类。
    /// 绑定时会记录 TemplateId，并触发应用前后回调。
    /// </summary>
    public abstract class JsonComponent : Core.Capability.CComponent
    {
        [TemplateIgnore]
        public string TemplateId { get; private set; }

        /// <summary>
        /// 将模板应用到当前组件。
        /// </summary>
        public void ApplyTemplate(object template, string templateId = null)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            TemplateId = templateId;
            OnTemplateApplying();
            JsonTemplateBinder.Apply(template, this, false);
            OnTemplateApplied();
        }

        protected virtual void OnTemplateApplying()
        {
        }

        /// <summary>
        /// 模板应用完成后的扩展点。
        /// </summary>
        public virtual void OnTemplateApplied()
        {
        }
    }
}
