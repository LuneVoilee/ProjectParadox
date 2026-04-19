using System;

namespace Tool.Json
{
    /// <summary>
    /// 实体模板 Key 路由描述：按模板集合类型（可选 slot）指定 key。
    /// </summary>
    public readonly struct TemplateKeySpec
    {
        public Type TemplateSetType { get; }

        public string Slot { get; }

        public object TemplateKey { get; }

        private TemplateKeySpec(Type templateSetType, string slot, object templateKey)
        {
            TemplateSetType = templateSetType;
            Slot = string.IsNullOrEmpty(slot) ? string.Empty : slot;
            TemplateKey = templateKey;
        }

        public static TemplateKeySpec For<TTemplateSet>(object templateKey)
        {
            return new TemplateKeySpec(typeof(TTemplateSet), string.Empty, templateKey);
        }

        public static TemplateKeySpec For<TTemplateSet>(string slot, object templateKey)
        {
            return new TemplateKeySpec(typeof(TTemplateSet), slot, templateKey);
        }

        public static TemplateKeySpec For(Type templateSetType, object templateKey, string slot = "")
        {
            if (templateSetType == null)
            {
                throw new ArgumentNullException(nameof(templateSetType));
            }

            return new TemplateKeySpec(templateSetType, slot, templateKey);
        }
    }
}
