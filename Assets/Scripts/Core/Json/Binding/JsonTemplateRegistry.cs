using System;
using System.Collections.Generic;
using System.Reflection;
using Tool;

namespace Core.Json
{
    /// <summary>
    /// Resolves the unique TemplateSet that owns a template type.
    /// </summary>
    internal static class JsonTemplateRegistry
    {
        private sealed class TemplateSetInfo
        {
            public Type TemplateSetType;
        }

        private static readonly object s_Lock = new object();
        private static Dictionary<Type, TemplateSetInfo> s_ByTemplateType;

        public static Type GetTemplateSetType(Type templateType)
        {
            if (templateType == null)
            {
                throw new ArgumentNullException(nameof(templateType));
            }

            EnsureBuilt();

            if (s_ByTemplateType.TryGetValue(templateType, out TemplateSetInfo info))
            {
                return info.TemplateSetType;
            }

            throw new InvalidOperationException(
                $"No JsonTemplateSet/BaseTemplateSet found for template type {templateType.FullName}.");
        }

        private static void EnsureBuilt()
        {
            if (s_ByTemplateType != null)
            {
                return;
            }

            lock (s_Lock)
            {
                if (s_ByTemplateType != null)
                {
                    return;
                }

                Build();
            }
        }

        private static void Build()
        {
            var byTemplateType = new Dictionary<Type, TemplateSetInfo>();
            foreach (Type type in Utility.Reflection.AllTypes(typeof(IJsonTemplateSet)))
            {
                if (type == null || type.IsAbstract || type.ContainsGenericParameters)
                {
                    continue;
                }

                Type baseSetType = FindBaseTemplateSet(type);
                if (baseSetType == null)
                {
                    continue;
                }

                Type[] args = baseSetType.GetGenericArguments();
                Type templateType = args[1];
                if (byTemplateType.TryGetValue(templateType, out TemplateSetInfo existing))
                {
                    throw new InvalidOperationException(
                        $"Template type {templateType.FullName} is mapped to multiple template sets: " +
                        $"{existing.TemplateSetType.FullName}, {type.FullName}.");
                }

                byTemplateType[templateType] = new TemplateSetInfo
                {
                    TemplateSetType = type
                };
            }

            s_ByTemplateType = byTemplateType;
        }

        private static Type FindBaseTemplateSet(Type type)
        {
            while (type != null && type != typeof(object))
            {
                if (type.IsGenericType &&
                    type.GetGenericTypeDefinition() == typeof(BaseTemplateSet<,>))
                {
                    return type;
                }

                type = type.BaseType;
            }

            return null;
        }
    }
}
