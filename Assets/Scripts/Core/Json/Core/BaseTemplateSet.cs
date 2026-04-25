#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Tool;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

#endregion

namespace Core.Json
{
    [AttributeUsage(AttributeTargets.Field)]
    public class PrimaryKeyAttribute : Attribute
    {
        public string Name = string.Empty;

        public FieldInfo FieldInfo { get; internal set; }
    }

    /// <summary>
    ///     模板集合基类：
    ///     负责主键索引、模板注册、按主键查询。
    /// </summary>
    public abstract class BaseTemplateSet<TSelf, TTemplate> : Singleton<TSelf>
        where TSelf : BaseTemplateSet<TSelf, TTemplate>
        where TTemplate : class
    {
        protected static readonly List<PrimaryKeyAttribute> ms_PrimaryKeys;

        protected readonly Dictionary<object, Dictionary<object, TTemplate>> m_PrimaryMap;

        protected BaseTemplateSet()
        {
            m_PrimaryMap = new Dictionary<object, Dictionary<object, TTemplate>>();
        }

        static BaseTemplateSet()
        {
            ms_PrimaryKeys = new List<PrimaryKeyAttribute>();

            foreach (FieldInfo fieldInfo in typeof(TTemplate).GetFields())
            {
                PrimaryKeyAttribute primaryKeyAttr =
                    fieldInfo.GetCustomAttribute<PrimaryKeyAttribute>();
                if (primaryKeyAttr == null)
                {
                    continue;
                }

                primaryKeyAttr.FieldInfo = fieldInfo;
                ms_PrimaryKeys.Add(primaryKeyAttr);
            }

            if (ms_PrimaryKeys.Count == 0)
            {
                throw new Exception($"{typeof(TTemplate).Name}: 需要使用 PrimaryKey 标记模板主键");
            }
        }

        protected void RegisterTemplate(TTemplate template)
        {
            // 同一个模板会被所有主键索引一次（默认主键名为 string.Empty）。
            foreach (PrimaryKeyAttribute primaryKey in ms_PrimaryKeys)
            {
                object key = primaryKey.FieldInfo.GetValue(template);
                if (key == null)
                {
                    throw new Exception(
                        $"主键不能为空: {typeof(TTemplate).Name}.{primaryKey.FieldInfo.Name}");
                }

                Dictionary<object, TTemplate> destDic = m_PrimaryMap.GetOrNew(primaryKey.Name);
                if (!destDic.TryAdd(key, template))
                {
                    throw new Exception(
                        $"主键重复: {typeof(TTemplate).Name}.{primaryKey.FieldInfo.Name} = {key}");
                }
            }
        }

        protected void Clear()
        {
            m_PrimaryMap.Clear();
        }

        public TTemplate GetTemplate(object primaryKey)
        {
            return GetTemplate(string.Empty, primaryKey);
        }

        public TTemplate GetTemplate(object name, object primaryKey)
        {
            if (TryGetTemplate(name, primaryKey, out TTemplate template))
            {
                return template;
            }

            throw new Exception(
                $"找不到模板: [{typeof(TTemplate).Name}]{primaryKey}\n{TemplateEnv.GetFullPath()}");
        }

        public bool TryGetTemplate(object name, object primaryKey, out TTemplate template)
        {
            if (primaryKey != null &&
                m_PrimaryMap.TryGetValue(name, out Dictionary<object, TTemplate> destDic))
            {
                return destDic.TryGetValue(primaryKey, out template);
            }

            template = default;
            return false;
        }

        public bool TryGetTemplate(object primaryKey, out TTemplate template)
        {
            return TryGetTemplate(string.Empty, primaryKey, out template);
        }

        public bool TryGetTemplateWithError(object primaryKey, out TTemplate template)
        {
            if (TryGetTemplate(string.Empty, primaryKey, out template))
            {
                return true;
            }

            Debug.LogError(
                $"找不到模板: [{typeof(TTemplate).Name}]{primaryKey}\n{TemplateEnv.GetFullPath()}");
            return false;
        }

        public Dictionary<object, TTemplate> AllTemplates => m_PrimaryMap.Count > 0
            ? m_PrimaryMap.First().Value
            : new Dictionary<object, TTemplate>();

        public Dictionary<object, TTemplate>.KeyCollection Keys => AllTemplates.Keys;

        public Dictionary<object, TTemplate>.ValueCollection Values => AllTemplates.Values;

        public static object GetPrimaryKey(object keyName, TTemplate template)
        {
            foreach (PrimaryKeyAttribute attr in ms_PrimaryKeys)
            {
                if (Equals(attr.Name, keyName))
                {
                    return attr.FieldInfo.GetValue(template);
                }
            }

            return null;
        }

        public static object GetPrimaryKey(TTemplate template)
        {
            return GetPrimaryKey(string.Empty, template);
        }
    }

    /// <summary>
    ///     基于 TextAsset(JSON) 的模板集合实现，含编辑器热重载能力。
    /// </summary>
    public abstract class JsonAssetTemplateSet<TSelf, TTemplate> : BaseTemplateSet<TSelf, TTemplate>
        where TSelf : JsonAssetTemplateSet<TSelf, TTemplate>
        where TTemplate : class
    {
        private static readonly object ms_DefaultPrimaryKeyName;

        protected class ReloadableUnit
        {
            public object PrimaryKey;
            public TTemplate Template;
            public TextAsset TextAsset;
        }

#if UNITY_EDITOR
        private readonly Dictionary<string, ReloadableUnit> m_ReloadableUnits =
            new Dictionary<string, ReloadableUnit>();
#endif

        public event Action<IList<TTemplate>> OnReloadAllEvent;

        static JsonAssetTemplateSet()
        {
            ms_DefaultPrimaryKeyName = ms_PrimaryKeys.First().Name;
        }

        protected ReloadableUnit RegisterAsset(TextAsset textAsset)
        {
            string textAssetName = textAsset.name.EndsWithOrdinal(".json")
                ? textAsset.name.Substring(0, textAsset.name.Length - 5)
                : textAsset.name;

            string jsonStr = textAsset.text;

            try
            {
                TemplateEnv.BeginTemplate(textAssetName);
                TTemplate template = DeserializeTemplate(jsonStr) ??
                                     throw new Exception("DeserializeTemplate 返回 null");

#if UNITY_EDITOR
                var unit = new ReloadableUnit
                {
                    PrimaryKey = GetPrimaryKey(ms_DefaultPrimaryKeyName, template),
                    TextAsset = textAsset,
                    Template = template
                };

                if (unit.PrimaryKey is string key && key != textAssetName)
                {
                    throw new Exception("PrimaryKey 与文件名不匹配");
                }

                m_ReloadableUnits[AssetDatabase.GetAssetPath(textAsset)] = unit;
                RegisterTemplate(template);
                return unit;
#else
                RegisterTemplate(template);
                return null;
#endif
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                TemplateEnv.EndTemplate();
            }

            return null;
        }

        protected virtual TTemplate DeserializeTemplate(string text)
        {
            return JsonConvert.DeserializeObject<TTemplate>(text);
        }

        protected void ReloadAll(IEnumerable<string> importedAssets)
        {
#if UNITY_EDITOR
            // 仅编辑器下支持增量热重载：优先就地更新旧模板对象，减少运行时引用失效。
            var changedTemplates = new List<TTemplate>();

            foreach (string path in importedAssets)
            {
                if (m_ReloadableUnits.TryGetValue(path, out ReloadableUnit unit))
                {
                    string jsonStr = unit.TextAsset.text;
                    try
                    {
                        TemplateEnv.BeginTemplate(unit.PrimaryKey?.ToString() ?? "Unknown");
                        TTemplate template = DeserializeTemplate(jsonStr) ??
                                             throw new Exception("DeserializeTemplate 返回 null");

                        if (unit.Template is IReloadable<TTemplate> reloadable)
                        {
                            reloadable.OnReload(template);
                        }
                        else
                        {
                            Utility.Reflection.SoftCopy(template, unit.Template);
                        }

                        Debug.LogWarning(
                            $"[{typeof(TTemplate).Name}] 重载 JSON 配置: {unit.PrimaryKey}");

                        object newPrimaryKey =
                            GetPrimaryKey(ms_DefaultPrimaryKeyName, unit.Template);
                        if (!Equals(unit.PrimaryKey, newPrimaryKey))
                        {
                            Debug.LogError(
                                $"主键发生改变, 无法正确重载, 请重新运行：{unit.PrimaryKey} -> {newPrimaryKey}");
                        }

                        changedTemplates.Add(unit.Template);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                    finally
                    {
                        TemplateEnv.EndTemplate();
                    }
                }
                else
                {
                    TextAsset newAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                    if (newAsset == null)
                    {
                        continue;
                    }

                    ReloadableUnit newUnit = RegisterAsset(newAsset);
                    if (newUnit == null)
                    {
                        continue;
                    }

                    Debug.LogWarning(
                        $"[{typeof(TTemplate).Name}] 新建 JSON 配置: {newUnit.PrimaryKey}");
                    changedTemplates.Add(newUnit.Template);
                }
            }

            OnReloadAllEvent?.Invoke(changedTemplates);
#endif
        }
    }
}