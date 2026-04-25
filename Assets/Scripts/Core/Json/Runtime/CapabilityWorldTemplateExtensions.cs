using Core.Capability;

namespace Core.Json
{
    /// <summary>
    /// World 级模板实体创建扩展，统一处理实体模板 key 上下文注入。
    /// </summary>
    public static class CapabilityWorldTemplateExtensions
    {
        public static CEntity AddChildFromTemplate
            (this CapabilityWorldBase world, string name, object templateKey)
        {
            if (world == null)
            {
                return null;
            }

            CEntity entity = world.AddChild(name);
            entity.SetDefaultTemplateKey(templateKey);
            return entity;
        }

        public static CEntity AddChildFromTemplates
        (
            this CapabilityWorldBase world,
            string name,
            params TemplateKeySpec[] templateKeys
        )
        {
            if (world == null)
            {
                return null;
            }

            CEntity entity = world.AddChild(name);
            entity.UseTemplateKeys(templateKeys);
            return entity;
        }
    }
}
