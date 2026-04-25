using Core.Capability;

namespace Core.Json
{
    /// <summary>
    /// CEntity JSON template consumption extensions.
    /// </summary>
    public static class CEntityTemplateExtensions
    {
        public static TComponent AddComponentFromTemplate<TComponent>
            (this CEntity entity, object templateKey)
            where TComponent : CComponent, new()
        {
            return JsonTemplateProcessor.AddComponentFromTemplate<TComponent>(entity, templateKey);
        }

        public static TComponent ApplyTemplate<TComponent>
            (this CEntity entity, object templateKey)
            where TComponent : CComponent, new()
        {
            return JsonTemplateProcessor.ApplyTemplate<TComponent>(entity, templateKey);
        }

    }
}
