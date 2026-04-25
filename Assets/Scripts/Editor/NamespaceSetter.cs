using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class NamespaceSetter : EditorWindow
{
    private bool m_IncludeSubfolders = true;
    private string m_NewNamespace = "MyProject.Scripts";
    private DefaultAsset m_TargetFolder;

    private void OnGUI()
    {
        GUILayout.Label("Set C# File Namespaces", EditorStyles.boldLabel);

        m_TargetFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "Target Folder",
            m_TargetFolder,
            typeof(DefaultAsset),
            false);

        m_NewNamespace = EditorGUILayout.TextField("New Namespace", m_NewNamespace);
        m_IncludeSubfolders = EditorGUILayout.Toggle("Include Subfolders", m_IncludeSubfolders);

        if (GUILayout.Button("Apply Namespace"))
        {
            if (m_TargetFolder == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a target folder.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(m_NewNamespace))
            {
                EditorUtility.DisplayDialog("Error", "Namespace cannot be empty.", "OK");
                return;
            }

            var folderPath = AssetDatabase.GetAssetPath(m_TargetFolder);
            ProcessCsFilesInFolder(folderPath);
        }
    }

    [MenuItem("Tools/Namespace Setter")]
    public static void ShowWindow()
    {
        GetWindow<NamespaceSetter>("Namespace Setter");
    }

    private void ProcessCsFilesInFolder(string folderAssetPath)
    {
        var fullFolderPath = Path.GetFullPath(folderAssetPath);

        var allCsFilePaths = Directory.GetFiles(fullFolderPath, "*.cs",
            m_IncludeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

        var processedCount = 0;
        float totalFiles = allCsFilePaths.Length;

        if (totalFiles == 0)
        {
            EditorUtility.DisplayDialog("Info", $"No C# scripts found in '{folderAssetPath}'.",
                "OK");
            return;
        }

        try
        {
            for (var i = 0; i < allCsFilePaths.Length; i++)
            {
                var absolutePath = allCsFilePaths[i];
                var normalizedPath = absolutePath.Replace('\\', '/');

                // 安全检查：跳过Editor文件夹内的脚本
                //if (normalizedPath.Contains("/Editor/"))
                //{
                //continue;
                //}

                EditorUtility.DisplayProgressBar(
                    "Setting Namespaces",
                    $"Processing: {Path.GetFileName(normalizedPath)}",
                    (i + 1) / totalFiles);

                var fileContent = File.ReadAllText(absolutePath);

                // 正则表达式查找已存在的namespace
                var pattern = @"(^\s*namespace\s+)[^;\{\s]+";

                string newContent;
                if (Regex.IsMatch(fileContent, pattern, RegexOptions.Multiline))
                {
                    // Case 1: Namespace exists, replace it.
                    newContent = Regex.Replace(fileContent, pattern, "$1" + m_NewNamespace,
                        RegexOptions.Multiline);
                }
                else
                {
                    // Case 2: No namespace, add one.
                    newContent = AddNamespace(fileContent, m_NewNamespace);
                }

                if (newContent != fileContent)
                {
                    File.WriteAllText(absolutePath, newContent);
                    processedCount++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        // 刷新资产数据库以触发重新编译
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Success",
            $"Processed {processedCount} C# scripts in '{folderAssetPath}'.", "OK");
        Debug.Log(
            $"[NamespaceSetter] Processed {processedCount} scripts and set their namespace to '{m_NewNamespace}'.");
    }

    private string AddNamespace(string fileContent, string newNamespace)
    {
        var lines = fileContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
            .ToList();

        var firstTypeCodeIndex = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            var trimmedLine = lines[i].Trim();
            if (trimmedLine.StartsWith("public") || trimmedLine.StartsWith("internal") ||
                trimmedLine.StartsWith("class") || trimmedLine.StartsWith("struct") ||
                trimmedLine.StartsWith("enum") || trimmedLine.StartsWith("interface") ||
                trimmedLine.StartsWith("abstract") || trimmedLine.StartsWith("sealed") ||
                trimmedLine.StartsWith("static class") || trimmedLine.StartsWith("partial"))
            {
                firstTypeCodeIndex = i;
                break;
            }
        }

        if (firstTypeCodeIndex != -1)
        {
            // 向上查找类型声明之前的所有属性（以 [ 开头的行）
            var namespaceInsertIndex = firstTypeCodeIndex;
            while (namespaceInsertIndex > 0)
            {
                var prevLine = lines[namespaceInsertIndex - 1].Trim();
                // 检查是否是属性行（以 [ 开头）或属性的一部分
                if (prevLine.StartsWith("[") || prevLine.EndsWith("]") || prevLine.EndsWith(","))
                {
                    namespaceInsertIndex--;
                }
                else if (string.IsNullOrWhiteSpace(prevLine))
                {
                    // 跳过空行，继续向上查找
                    namespaceInsertIndex--;
                }
                else
                {
                    break;
                }
            }

            // 跳过向上搜索时遇到的空行（保持属性紧贴类型声明）
            while (namespaceInsertIndex < firstTypeCodeIndex &&
                   string.IsNullOrWhiteSpace(lines[namespaceInsertIndex].Trim()))
            {
                namespaceInsertIndex++;
            }

            lines.Insert(namespaceInsertIndex, $"namespace {newNamespace}");
            lines.Insert(namespaceInsertIndex + 1, "{");

            // 为代码添加缩进（从属性/类型声明开始，到文件末尾）
            for (var i = namespaceInsertIndex + 2; i < lines.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                {
                    lines[i] = "    " + lines[i];
                }
            }

            lines.Add("}");
        }
        else
        {
            // 如果没有找到代码，则不作修改
            return fileContent;
        }

        return string.Join("\r\n", lines);
    }
}