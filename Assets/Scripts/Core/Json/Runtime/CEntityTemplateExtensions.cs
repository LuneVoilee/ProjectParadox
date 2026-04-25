using System;
using Core.Capability;

namespace Core.Json
{
    /// <summary>
    /// CEntity 模板 key 配置扩展。
    /// </summary>
    public static class CEntityTemplateExtensions
    {
        public static CEntity UseTemplateKey(this CEntity entity, object templateKey)
        {
            if (entity == null)
            {
                return null;
            }

            entity.SetDefaultTemplateKey(templateKey);
            return entity;
        }

        public static CEntity UseTemplateKey<TTemplateSet>
            (this CEntity entity, object templateKey, string slot = "")
        {
            if (entity == null)
            {
                return null;
            }

            entity.SetTemplateKey(typeof(TTemplateSet), templateKey, slot);
            return entity;
        }

        public static CEntity UseTemplateKeys(this CEntity entity, params TemplateKeySpec[] specs)
        {
            if (entity == null || specs == null || specs.Length == 0)
            {
                return entity;
            }

            foreach (TemplateKeySpec spec in specs)
            {
                if (spec.TemplateSetType == null)
                {
                    continue;
                }

                entity.SetTemplateKey(spec.TemplateSetType, spec.TemplateKey, spec.Slot);
            }

            return entity;
        }
    }
}
