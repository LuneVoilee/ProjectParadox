using System.Collections.Generic;
using System.Text.RegularExpressions;
using Tool;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tool.Json
{
    public class KResourceRouter : IResourceSchemaProcessor
    {
        private static readonly Dictionary<string, string> m_RouterMap = new Dictionary<string, string>();

        private readonly string m_Schema;
        private readonly string m_Prefix;

        private static readonly Regex m_Regex = new Regex(@"^(?<header>\S+):\/\/(?<path>\S+)");

        public KResourceRouter(string prefix, string schema = null)
        {
            m_Schema = schema;
            m_Prefix = prefix;

            if (!string.IsNullOrEmpty(m_Schema))
            {
                m_RouterMap[m_Schema] = m_Prefix;
            }
        }

        public T Load<T>(string address) where T : Object
        {
            string path = string.IsNullOrEmpty(m_Prefix) ? address : $"{m_Prefix}/{address}";
            return ResourceManager.Load<T>(path);
        }

        public List<T> LoadAll<T>(string address) where T : Object
        {
            string path = string.IsNullOrEmpty(m_Prefix) ? address : $"{m_Prefix}/{address}";
            return ResourceManager.LoadAll<T>(path);
        }

        public static string GetRealPath(string path)
        {
            path = path.Replace("\\", "/");

            Match m = m_Regex.Match(path);
            if (!m.Success)
            {
                return path;
            }

            string header = m.Groups["header"].Value;
            if (!m_RouterMap.TryGetValue(header, out string prefix))
            {
                return path;
            }

            string p = m.Groups["path"].Value;
            return $"{prefix}/{p}";
        }
    }
}
