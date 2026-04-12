#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

#endregion

namespace Core.Capability.Editor
{
    public static class ComponentAccessCodeGenerator
    {
        private const string MenuGeneratePath = "框架工具/代码生成/生成 ComponentAccess";

        private const string MenuOpenGeneratedPath = "框架工具/代码生成/打开 ComponentAccess 输出文件";

        private const string OutputAssetPath =
            "Assets/Scripts/Core/Capability/Generated/ComponentAccess.g.cs";

        [MenuItem(MenuGeneratePath, false, 22)]
        public static void GenerateComponentAccess()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[ComponentAccessCodeGenerator] Play Mode 下不能执行代码生成。");
                return;
            }

            try
            {
                List<ComponentMetadata> metadata = CollectComponents();
                string content = BuildContent(metadata);

                string outputFullPath = Path.GetFullPath(OutputAssetPath);
                string outputDirectory = Path.GetDirectoryName(outputFullPath);
                if (!string.IsNullOrWhiteSpace(outputDirectory) &&
                    !Directory.Exists(outputDirectory))
                {
                    if (outputDirectory != null)
                    {
                        Directory.CreateDirectory(outputDirectory);
                    }
                }

                File.WriteAllText(outputFullPath, content, Encoding.UTF8);
                AssetDatabase.Refresh();
                Debug.Log(
                    $"[ComponentAccessCodeGenerator] 生成完成: {OutputAssetPath} (共 {metadata.Count} 个组件)。");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[ComponentAccessCodeGenerator] 生成失败: {exception}");
            }
        }

        [MenuItem(MenuOpenGeneratedPath, false, 23)]
        public static void OpenGeneratedFile()
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(OutputAssetPath);
            if (asset == null)
            {
                Debug.LogWarning($"[ComponentAccessCodeGenerator] 输出文件不存在: {OutputAssetPath}");
                return;
            }

            AssetDatabase.OpenAsset(asset);
        }

        private static List<ComponentMetadata> CollectComponents()
        {
            List<ComponentMetadata> metadata = new List<ComponentMetadata>(64);
            HashSet<Type> componentTypes = new HashSet<Type>();
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type[] types = GetTypesSafely(assemblies[i]);
                for (int j = 0; j < types.Length; j++)
                {
                    Type type = types[j];
                    if (type == null || !type.IsClass || type.IsAbstract)
                    {
                        continue;
                    }

                    if (type.ContainsGenericParameters)
                    {
                        continue;
                    }

                    if (!typeof(CComponent).IsAssignableFrom(type))
                    {
                        continue;
                    }

                    if (!IsRuntimeComponentType(type))
                    {
                        continue;
                    }

                    if (!componentTypes.Add(type))
                    {
                        continue;
                    }

                    metadata.Add(new ComponentMetadata
                    {
                        Type = type,
                        HasPublicParameterlessConstructor = type.GetConstructor(Type.EmptyTypes) != null
                    });
                }
            }

            metadata.Sort((left, right) =>
                string.CompareOrdinal(left.Type.FullName, right.Type.FullName));
            AssignAliases(metadata);
            return metadata;
        }

        private static Type[] GetTypesSafely(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types;
            }
        }

        private static void AssignAliases(List<ComponentMetadata> metadata)
        {
            Dictionary<string, List<ComponentMetadata>> groups =
                new Dictionary<string, List<ComponentMetadata>>(64);
            for (int i = 0; i < metadata.Count; i++)
            {
                ComponentMetadata item = metadata[i];
                string simpleName = item.Type.Name;
                if (!groups.TryGetValue(simpleName, out List<ComponentMetadata> group))
                {
                    group = new List<ComponentMetadata>(2);
                    groups.Add(simpleName, group);
                }

                group.Add(item);
            }

            HashSet<string> usedAliases = new HashSet<string>(metadata.Count * 2);
            foreach (KeyValuePair<string, List<ComponentMetadata>> pair in groups)
            {
                List<ComponentMetadata> group = pair.Value;
                for (int i = 0; i < group.Count; i++)
                {
                    ComponentMetadata item = group[i];
                    string alias = group.Count == 1
                        ? SanitizeIdentifier(item.Type.Name)
                        : SanitizeIdentifier((item.Type.FullName ?? item.Type.Name)
                            .Replace('.', '_')
                            .Replace('+', '_'));

                    if (!usedAliases.Add(alias))
                    {
                        int suffix = 2;
                        string candidate = alias;
                        while (!usedAliases.Add(candidate))
                        {
                            candidate = $"{alias}_{suffix}";
                            suffix++;
                        }

                        alias = candidate;
                    }

                    item.Alias = alias;
                }
            }
        }

        private static bool IsRuntimeComponentType(Type type)
        {
            string namespaceName = type.Namespace ?? string.Empty;
            if (namespaceName.StartsWith("UnityEditor", StringComparison.Ordinal) ||
                namespaceName.Contains(".Editor"))
            {
                return false;
            }

            string assemblyName = type.Assembly.GetName().Name ?? string.Empty;
            return !assemblyName.EndsWith(".Editor", StringComparison.Ordinal);
        }

        private static string SanitizeIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "UnknownComponent";
            }

            StringBuilder builder = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (char.IsLetterOrDigit(ch) || ch == '_')
                {
                    builder.Append(ch);
                }
                else
                {
                    builder.Append('_');
                }
            }

            if (builder.Length == 0 || !char.IsLetter(builder[0]) && builder[0] != '_')
            {
                builder.Insert(0, '_');
            }

            return builder.ToString();
        }

        private static string BuildContent(List<ComponentMetadata> metadata)
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("//------------------------------------------------------------------------------");
            builder.AppendLine("// <auto-generated>");
            builder.AppendLine("//     This code was generated by ComponentAccessCodeGenerator.");
            builder.AppendLine("// </auto-generated>");
            builder.AppendLine("//------------------------------------------------------------------------------");
            builder.AppendLine("namespace Core.Capability");
            builder.AppendLine("{");
            builder.AppendLine("    public static class ComponentAccess");
            builder.AppendLine("    {");

            if (metadata.Count == 0)
            {
                builder.AppendLine("        public static int Count => 0;");
            }
            else
            {
                for (int i = 0; i < metadata.Count; i++)
                {
                    ComponentMetadata item = metadata[i];
                    string typeName = GetTypeName(item.Type);
                    builder.AppendLine(
                        $"        public static readonly int {item.Alias}Id = Component<{typeName}>.TId;");
                }

                builder.AppendLine();
                builder.AppendLine($"        public static int Count => {metadata.Count};");
                builder.AppendLine();

                for (int i = 0; i < metadata.Count; i++)
                {
                    ComponentMetadata item = metadata[i];
                    AppendMethods(builder, item);
                    if (i < metadata.Count - 1)
                    {
                        builder.AppendLine();
                    }
                }
            }

            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static void AppendMethods(StringBuilder builder, ComponentMetadata item)
        {
            string typeName = GetTypeName(item.Type);
            string idName = $"{item.Alias}Id";

            builder.AppendLine(
                $"        public static bool Has{item.Alias}(this CEntity entity)");
            builder.AppendLine("        {");
            builder.AppendLine($"            return entity != null && entity.HasComponent({idName});");
            builder.AppendLine("        }");
            builder.AppendLine();

            builder.AppendLine(
                $"        public static bool TryGet{item.Alias}(this CEntity entity, out {typeName} component)");
            builder.AppendLine("        {");
            builder.AppendLine("            component = null;");
            builder.AppendLine(
                $"            return entity != null && entity.TryGetComponent({idName}, out component);");
            builder.AppendLine("        }");
            builder.AppendLine();

            builder.AppendLine(
                $"        public static {typeName} Get{item.Alias}OrNull(this CEntity entity)");
            builder.AppendLine("        {");
            builder.AppendLine(
                $"            return entity != null ? entity.GetComponent({idName}) as {typeName} : null;");
            builder.AppendLine("        }");
            builder.AppendLine();

            if (item.HasPublicParameterlessConstructor)
            {
                builder.AppendLine(
                    $"        public static {typeName} Add{item.Alias}(this CEntity entity)");
                builder.AppendLine("        {");
                builder.AppendLine(
                    $"            return entity != null ? entity.AddComponent<{typeName}>() : null;");
                builder.AppendLine("        }");
                builder.AppendLine();
            }

            builder.AppendLine(
                $"        public static bool Remove{item.Alias}(this CEntity entity)");
            builder.AppendLine("        {");
            builder.AppendLine($"            if (entity == null || !entity.HasComponent({idName}))");
            builder.AppendLine("            {");
            builder.AppendLine("                return false;");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine($"            entity.RemoveComponent({idName});");
            builder.AppendLine("            return true;");
            builder.AppendLine("        }");
        }

        private static string GetTypeName(Type type)
        {
            if (!type.IsGenericType)
            {
                return (type.FullName ?? type.Name).Replace('+', '.');
            }

            string typeName = type.GetGenericTypeDefinition().FullName;
            if (string.IsNullOrWhiteSpace(typeName))
            {
                typeName = type.Name;
            }

            if (typeName == null)
            {
                return type.Name;
            }

            int tickIndex = typeName.IndexOf('`');
            if (tickIndex >= 0)
            {
                typeName = typeName.Substring(0, tickIndex);
            }

            Type[] arguments = type.GetGenericArguments();
            StringBuilder builder = new StringBuilder(typeName.Length + arguments.Length * 12);
            builder.Append(typeName.Replace('+', '.'));
            builder.Append('<');
            for (int i = 0; i < arguments.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(GetTypeName(arguments[i]));
            }

            builder.Append('>');
            return builder.ToString();
        }

        private sealed class ComponentMetadata
        {
            public Type Type;

            public string Alias;

            public bool HasPublicParameterlessConstructor;
        }
    }
}
