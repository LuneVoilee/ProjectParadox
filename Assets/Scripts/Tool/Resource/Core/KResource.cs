#region

using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using Object = UnityEngine.Object;

#endregion

namespace Tool.Resource
{
    public interface IResourceSchemaProcessor
    {
        T Load<T>(string address) where T : Object;

        List<T> LoadAll<T>(string address) where T : Object;
    }

    public static class KResource
    {
        private static readonly Regex m_Regex = new Regex(@"^(?<header>\S+):\/\/(?<path>.+)");

        private static readonly Dictionary<string, IResourceSchemaProcessor> m_Processor =
            new Dictionary<string, IResourceSchemaProcessor>();

        private static readonly IResourceSchemaProcessor m_DefaultProcessor =
            new KResourceRouter(null);

        private static readonly HashSet<string> m_MissingPath = new HashSet<string>();

        static KResource()
        {
            RegisterPrefix("ArtBase", "Assets/Resource/ArtBase");
            RegisterPrefix("ArtSource", "Assets/Resource/ArtSource");
            RegisterPrefix("Config", "Assets/Resource/Config");
            RegisterPrefix("UI", "Assets/Resource/UI");
        }

        public static void RegisterPrefix(string schema, string prefix)
        {
            m_Processor[schema] = new KResourceRouter(prefix, schema);
        }

        public static void Register(string schema, IResourceSchemaProcessor processor)
        {
            m_Processor[schema] = processor;
        }

        public static T Load<T>(string path) where T : Object
        {
            if (string.IsNullOrEmpty(path))
            {
                return default;
            }

            path = path.Replace("\\", "/");
            if (TryGetProcessor(path, out IResourceSchemaProcessor processor, out string address))
            {
                T data = processor.Load<T>(address);
                if (data == null && m_MissingPath.Add(path))
                {
                    Debug.LogWarning($"加载 {typeof(T).Name} 资源失败: {path}");
                }

                return data;
            }

            Debug.LogError($"加载资源失败: 找不到匹配的 Schema\n{path}");
            return null;
        }

        public static List<T> LoadAll<T>(string path) where T : Object
        {
            path = path.Replace("\\", "/");

            if (TryGetProcessor(path, out IResourceSchemaProcessor processor, out string address))
            {
                return processor.LoadAll<T>(address);
            }

            Debug.LogError($"加载资源失败: 找不到匹配的 Schema\n{path}");
            return new List<T>();
        }

        private static bool TryGetProcessor
            (string path, out IResourceSchemaProcessor processor, out string address)
        {
            Match m = m_Regex.Match(path);
            if (!m.Success)
            {
                processor = m_DefaultProcessor;
                address = path;
                return processor != null;
            }

            string header = m.Groups["header"].Value;
            m_Processor.TryGetValue(header, out processor);
            address = m.Groups["path"].Value;
            return processor != null;
        }
    }
}