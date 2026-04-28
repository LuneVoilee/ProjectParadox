#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

#endregion

namespace Core.Capability.Editor
{
    public static class ComponentAccessCodeGenerator
    {
        private const string MenuGeneratePath = "框架工具/代码生成/生成 ComponentAccess";

        private const string MenuOpenGeneratedPath = "框架工具/代码生成/打开 ComponentAccess 输出文件";

        private const string OutputAssetPath =
            "Assets/Scripts/GamePlay/Util/Generated/ComponentAccess.g.cs";

        [MenuItem(MenuGeneratePath, false, 22)]
        public static void GenerateComponentAccess()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[ComponentAccessCodeGenerator] Play Mode 下不能执行代码生成。");
                return;
            }

            // 优先调用独立 Python 脚本，不依赖 Unity 编译状态。
            if (TryRunPythonScript("Tools/generate_component_access.py"))
                return;

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

        // 调用项目根目录下的 Python 脚本。成功返回 true 并自动 Refresh AssetDatabase。
        private static bool TryRunPythonScript(string scriptRelativePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string scriptPath = Path.Combine(projectRoot, scriptRelativePath);

            if (!File.Exists(scriptPath))
            {
                Debug.LogWarning($"[ComponentAccessCodeGenerator] Python 脚本不存在: {scriptPath}");
                return false;
            }

            try
            {
                var process = new Process();
                process.StartInfo.FileName = "python";
                process.StartInfo.Arguments = $"\"{scriptPath}\"";
                process.StartInfo.WorkingDirectory = projectRoot;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit(5000);

                if (process.ExitCode == 0)
                {
                    if (!string.IsNullOrWhiteSpace(stdout))
                        Debug.Log(stdout.TrimEnd());
                    AssetDatabase.Refresh();
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(stderr))
                    Debug.LogWarning($"[ComponentAccessCodeGenerator] Python 脚本输出:\n{stderr.TrimEnd()}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[ComponentAccessCodeGenerator] Python 调用失败，回退到内置程序集扫描: {ex.Message}");
                return false;
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

        // 在源码中扫描 CComponent 子类的完整类型名（namespace.class）。
        // 不再依赖编译后的程序集，避免 GamePlay 等程序集有编译错误时丢失组件。
        private sealed class SourceComponentInfo
        {
            public string FullName;  // namespace.class
            public string SourceFile;
        }

        private static List<ComponentMetadata> CollectComponents()
        {
            List<SourceComponentInfo> sourceInfos = CollectComponentsFromSource();

            List<ComponentMetadata> metadata = new List<ComponentMetadata>(sourceInfos.Count);
            HashSet<string> seenFullNames = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < sourceInfos.Count; i++)
            {
                SourceComponentInfo info = sourceInfos[i];
                if (!seenFullNames.Add(info.FullName))
                {
                    continue;
                }

                // 尝试从已加载程序集中解析 Type 以检测是否有无参构造函数。
                // 若程序集未加载（编译错误），保守假定有无参构造函数。
                Type type = TryResolveType(info.FullName);
                metadata.Add(new ComponentMetadata
                {
                    Type = type,
                    SourceFullName = info.FullName,
                    HasPublicParameterlessConstructor =
                        type == null || type.GetConstructor(Type.EmptyTypes) != null
                });
            }

            metadata.Sort((left, right) =>
                string.CompareOrdinal(
                    left.SourceFullName ?? left.Type?.FullName,
                    right.SourceFullName ?? right.Type?.FullName));
            AssignAliases(metadata);
            return metadata;
        }

        // 遍历 Assets/Scripts 下所有 .cs 文件，解析出继承 CComponent 的非抽象类。
        private static List<SourceComponentInfo> CollectComponentsFromSource()
        {
            List<SourceComponentInfo> result = new List<SourceComponentInfo>(96);
            string scriptsDir = Path.GetFullPath("Assets/Scripts");

            if (!Directory.Exists(scriptsDir))
            {
                return result;
            }

            string[] csFiles = Directory.GetFiles(scriptsDir, "*.cs", SearchOption.AllDirectories);

            for (int i = 0; i < csFiles.Length; i++)
            {
                string filePath = csFiles[i];
                string relativePath = filePath.Replace('\\', '/');

                // 跳过 Editor 目录，与旧版 IsRuntimeComponentType 行为一致。
                if (relativePath.Contains("/Editor/"))
                {
                    continue;
                }

                string content;
                try
                {
                    content = File.ReadAllText(filePath);
                }
                catch
                {
                    continue;
                }

                ParseComponentClasses(content, relativePath, result);
            }

            return result;
        }

        // 从单个 .cs 文件中提取 namespace + class 对。
        // 正则匹配：可选 abstract 关键字 → class 关键字 → 类名 → 可选泛型参数 →
        // 基类列表必须直接包含 CComponent（如 "class Foo : CComponent" 或 "class Foo : ISome, CComponent"）。
        // 这样的设计不处理间接继承（class A : B，而 B : CComponent），实际 gameplay 组件全部直接继承 CComponent。
        private static readonly System.Text.RegularExpressions.Regex s_ClassRegex =
            new System.Text.RegularExpressions.Regex(
                @"(?:^|\n)\s*(?:(?:public|internal|private|protected)\s+)?(?:sealed\s+)?(?:partial\s+)?(?!abstract\s+)class\s+(\w+)(?:<[^>]*>)?\s*:\s*[^\{]*?\bCComponent\b",
                System.Text.RegularExpressions.RegexOptions.Multiline |
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex s_NamespaceRegex =
            new System.Text.RegularExpressions.Regex(
                @"namespace\s+([\w.]+)",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static void ParseComponentClasses(string content, string sourcePath,
            List<SourceComponentInfo> result)
        {
            // 提取所有 namespace 声明。
            System.Text.RegularExpressions.MatchCollection nsMatches =
                s_NamespaceRegex.Matches(content);

            string defaultNamespace = null;
            if (nsMatches.Count == 0)
            {
                defaultNamespace = ""; // 无 namespace 的全局作用域
            }

            // 为每个 namespace 块搜索 CComponent 子类。
            if (nsMatches.Count > 0)
            {
                for (int i = 0; i < nsMatches.Count; i++)
                {
                    string ns = nsMatches[i].Groups[1].Value;

                    // 跳过 Editor 命名空间。
                    if (ns.Contains(".Editor") || ns.StartsWith("UnityEditor"))
                    {
                        continue;
                    }

                    // 确定该 namespace 块在源文件中的起止范围。
                    int blockStart = nsMatches[i].Index;
                    int blockEnd = (i + 1 < nsMatches.Count)
                        ? nsMatches[i + 1].Index
                        : content.Length;

                    string blockText = content.Substring(blockStart, blockEnd - blockStart);
                    AddClassesFromBlock(blockText, ns, result);
                }
            }
            else
            {
                AddClassesFromBlock(content, defaultNamespace, result);
            }
        }

        private static void AddClassesFromBlock(string blockText, string ns,
            List<SourceComponentInfo> result)
        {
            System.Text.RegularExpressions.MatchCollection classMatches =
                s_ClassRegex.Matches(blockText);

            for (int i = 0; i < classMatches.Count; i++)
            {
                string className = classMatches[i].Groups[1].Value;
                string fullName = string.IsNullOrEmpty(ns)
                    ? className
                    : ns + "." + className;

                result.Add(new SourceComponentInfo
                {
                    FullName = fullName
                });
            }
        }

        // 尝试从已加载程序集中反射出 Type。编译失败时返回 null。
        private static Type TryResolveType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type[] types = GetTypesSafely(assemblies[i]);
                if (types == null) continue;
                for (int j = 0; j < types.Length; j++)
                {
                    Type type = types[j];
                    if (type != null &&
                        (type.FullName == fullName || type.Name == fullName) &&
                        !type.IsAbstract &&
                        typeof(CComponent).IsAssignableFrom(type))
                    {
                        return type;
                    }
                }
            }

            return null;
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
                string simpleName = GetSimpleName(item);
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
                        ? SanitizeIdentifier(GetSimpleName(item))
                        : SanitizeIdentifier((GetFullName(item) ?? GetSimpleName(item))
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
            builder.AppendLine(
                "//------------------------------------------------------------------------------");
            builder.AppendLine("// <auto-generated>");
            builder.AppendLine("//     This code was generated by ComponentAccessCodeGenerator.");
            builder.AppendLine("// </auto-generated>");
            builder.AppendLine(
                "//------------------------------------------------------------------------------");

            builder.AppendLine("using Core.Capability;");
            builder.AppendLine("namespace GamePlay.Util");
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
                    string typeName = GetTypeName(item);
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
            string typeName = GetTypeName(item);
            string idName = $"{item.Alias}Id";

            builder.AppendLine(
                $"        public static bool Has{item.Alias}(this CEntity entity)");
            builder.AppendLine("        {");
            builder.AppendLine(
                $"            return entity != null && entity.HasComponent({idName});");
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
            builder.AppendLine(
                $"            if (entity == null || !entity.HasComponent({idName}))");
            builder.AppendLine("            {");
            builder.AppendLine("                return false;");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine($"            entity.RemoveComponent({idName});");
            builder.AppendLine("            return true;");
            builder.AppendLine("        }");
        }

        // 返回组件的完整类型名，优先用反射 Type，编译失败时降级为源码字符串。
        private static string GetTypeName(ComponentMetadata item)
        {
            if (item.Type != null)
            {
                return GetTypeName(item.Type);
            }

            // 编译失败时直接用源码解析的完整路径名。
            return (item.SourceFullName ?? item.Type?.FullName ?? "Unknown")
                .Replace('+', '.');
        }

        private static string GetSimpleName(ComponentMetadata item)
        {
            if (item.Type != null)
            {
                return item.Type.Name;
            }

            string fullName = item.SourceFullName;
            if (string.IsNullOrEmpty(fullName))
            {
                return "Unknown";
            }

            int lastDot = fullName.LastIndexOf('.');
            return lastDot >= 0 ? fullName.Substring(lastDot + 1) : fullName;
        }

        private static string GetFullName(ComponentMetadata item)
        {
            if (item.Type != null)
            {
                return item.Type.FullName;
            }

            return item.SourceFullName;
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
            // 编译后可解析的 Type；编译失败时为 null。
            public Type Type;

            // 从源码解析的完整类型名（namespace.class），始终非 null。
            public string SourceFullName;

            public string Alias;

            public bool HasPublicParameterlessConstructor;
        }
    }
}
