//#define KG_EDIT_JSON_ON_LOAD

#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Tool.Resource;
using UnityEngine;
using UnityEngine.Profiling;
#if UNITY_EDITOR
using UnityEditor;
#endif

#endregion

namespace Tool.Json
{
#if UNITY_EDITOR
    internal class JsonAssetPostProcessor : AssetPostprocessor
    {
        public static event Action<List<string>> OnJsonAssetReimportEvent;

        private static void OnPostprocessAllAssets
        (
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths
        )
        {
            List<string> jsonPath = importedAssets.Where(s => s.EndsWithOrdinal(".json")).ToList();
            if (jsonPath.Count > 0)
            {
                OnJsonAssetReimportEvent?.Invoke(jsonPath);
            }
        }
    }
#endif
}

namespace Tool.Json
{
    public interface ITemplateRelated<out TTemplate>
    {
        TTemplate BaseTemplate { get; }
    }

    public interface IReloadable<in TTemplate>
    {
        void OnReload(TTemplate t);
    }

    public abstract partial class JsonTemplateSet<TSelf, TTemplate> :
        JsonAssetTemplateSet<TSelf, TTemplate>,
        IJsonTemplateSet
        where TSelf : JsonTemplateSet<TSelf, TTemplate>
        where TTemplate : class
    {
        protected abstract string ConfigDir { get; }

        private string m_RealPath;

        protected static bool ms_IsLoading;
        private static bool ms_IsLoadingFinish;

        protected JsonTemplateSet(bool load = true)
        {
            if (ms_PreloadCoroutine == null && load)
            {
                Load(ConfigDir);
            }

#if UNITY_EDITOR
            JsonAssetPostProcessor.OnJsonAssetReimportEvent += OnReloadJsonAssets;
#endif
        }

        ~JsonTemplateSet()
        {
#if UNITY_EDITOR
            JsonAssetPostProcessor.OnJsonAssetReimportEvent -= OnReloadJsonAssets;
#endif
        }

        protected void Load(string dir)
        {
            Profiler.BeginSample($"{GetType()}:Load");

            ms_UseFrameLimit = false;
            CoLoad(dir).Complete();

            Profiler.EndSample();
        }

        private IEnumerator CoLoad(string dir)
        {
            float realTime = Time.realtimeSinceStartup;
            ms_IsLoading = true;

            List<TextAsset> textAssets = KResource.LoadAll<TextAsset>(dir);
            foreach (TextAsset textAsset in textAssets)
            {
                RegisterAsset(textAsset);

                if (ms_UseFrameLimit && Time.realtimeSinceStartup - realTime > 0.1f)
                {
                    yield return null;
                    realTime = Time.realtimeSinceStartup;
                }
            }

            textAssets.Release();

            ms_IsLoading = false;
            OnLoadFinish();
        }

        private void OnLoadFinish()
        {
            try
            {
                OnTemplateLoaded(AllTemplates.Values.ToList(), false);
            }
            catch (Exception e)
            {
                Log.Exception(e);
            }

            OnReloadAllEvent += changedTemplate => { OnTemplateLoaded(changedTemplate, true); };
            ms_IsLoadingFinish = true;
        }

        private static ICoroutine ms_PreloadCoroutine;
        private static bool ms_UseFrameLimit = true;

        protected override void OnAccess()
        {
            if (ms_PreloadCoroutine == null || ms_PreloadCoroutine.IsFinished)
            {
                return;
            }

            Stopwatch watch = Stopwatch.StartNew();
            Profiler.BeginSample($"{typeof(TSelf).Name}:WaitLoadingComplete");

            ms_UseFrameLimit = false;
            ms_PreloadCoroutine.Complete();

            Profiler.EndSample();
            Log.Warn(
                $"[{typeof(TSelf).Name}] 等待预加载结束， 造成 {watch.Elapsed.TotalSeconds:F}s 卡顿\n" +
                $"TemplateContext: {TemplateEnv.GetFullPathStack()}");
        }

        public static void Preload()
        {
            if (ms_PreloadCoroutine != null || ms_IsLoadingFinish)
            {
                return;
            }

            ms_PreloadCoroutine = CoroutineModule.Instance.CreateCoroutine(CoPreload());
            ms_PreloadCoroutine.Update();
            PreloadingQueue.Push(ms_PreloadCoroutine);
        }

        private static IEnumerator CoPreload(string dir = null)
        {
            TSelf instance = Instance;
            if (dir == null)
            {
                dir = instance.ConfigDir;
            }

            yield return null;
            yield return instance.CoLoad(dir);

            Log.Info($"[{instance.GetType().Name}] [预加载结束] 读取配置:{dir}");
        }

        internal void OnReloadJsonAssets(List<string> importedPath)
        {
            if (m_RealPath == null)
            {
                m_RealPath = KResourceRouter.GetRealPath(ConfigDir);
                if (!m_RealPath.EndsWithOrdinal('/'))
                {
                    m_RealPath += '/';
                }
            }

            List<string> changedFiles = KListPool<string>.Claim();
            foreach (string path in importedPath)
            {
                if (path.StartsWithOrdinal(m_RealPath))
                {
                    changedFiles.Add(path);
                }
            }

            if (changedFiles.Count > 0)
            {
                ReloadAll(changedFiles);
            }

            changedFiles.Release();
        }

        public static void Unload()
        {
            if (m_Instance == null)
            {
                return;
            }

            m_Instance.Clear();
            m_Instance = null;
            ms_PreloadCoroutine = null;
            ms_IsLoadingFinish = false;
            ms_IsLoading = false;
        }

        public static void UnLoad()
        {
            Unload();
        }

        protected virtual void OnTemplateLoaded(IList<TTemplate> changedList, bool isReload)
        {
        }

        public abstract class Classifier<TClassifier, TKey> : Singleton<TClassifier>
            where TClassifier : Classifier<TClassifier, TKey>
        {
            private readonly Dictionary<TKey, List<TTemplate>> m_IndexMap =
                new Dictionary<TKey, List<TTemplate>>();

            protected Classifier()
            {
                List<TTemplate> allTemplate = JsonTemplateSet<TSelf, TTemplate>.Instance
                    .AllTemplates.Values
                    .ToList();
                OnReloadAll(allTemplate);
                JsonTemplateSet<TSelf, TTemplate>.Instance.OnReloadAllEvent += OnReloadAll;
            }

            private void OnReloadAll(IList<TTemplate> templates)
            {
                if (m_IndexMap.Count > 0)
                {
                    foreach (List<TTemplate> list in m_IndexMap.Values)
                    {
                        list.RemoveAll(templates.Contains);
                    }
                }

                var classifyResults = new List<TKey>();
                foreach (TTemplate template in templates)
                {
                    classifyResults.Clear();
                    GetKeys(template, ref classifyResults);

                    foreach (TKey key in classifyResults)
                    {
                        List<TTemplate> destList = m_IndexMap.GetOrNew(key);
                        destList.Add(template);
                    }
                }
            }

            public IReadOnlyList<TTemplate> GetTemplates(TKey key)
            {
                return m_IndexMap.TryGetValue(key, out List<TTemplate> destList)
                    ? destList
                    : new List<TTemplate>();
            }

            public bool TryGetTemplates(TKey key, out IReadOnlyList<TTemplate> templates)
            {
                if (m_IndexMap.TryGetValue(key, out List<TTemplate> destList))
                {
                    templates = destList;
                    return true;
                }

                templates = null;
                return false;
            }

            protected abstract void GetKeys(TTemplate template, ref List<TKey> classifyResults);
        }

        public abstract class Classifier<TClassifier, TKey, TValue> : Singleton<TClassifier>
            where TClassifier : Classifier<TClassifier, TKey, TValue>
            where TValue : ITemplateRelated<TTemplate>
        {
            private readonly Dictionary<TKey, List<TValue>> m_IndexMap =
                new Dictionary<TKey, List<TValue>>();

            protected Classifier()
            {
                List<TTemplate> allTemplate = JsonTemplateSet<TSelf, TTemplate>.Instance
                    .AllTemplates.Values
                    .ToList();
                OnReloadAll(allTemplate);
                JsonTemplateSet<TSelf, TTemplate>.Instance.OnReloadAllEvent += OnReloadAll;
            }

            private void OnReloadAll(IList<TTemplate> templates)
            {
                if (m_IndexMap.Count > 0)
                {
                    foreach (List<TValue> list in m_IndexMap.Values)
                    {
                        list.RemoveAll(v => templates.Contains(v.BaseTemplate));
                    }
                }

                var classifyResult = new List<KeyValuePair<TKey, TValue>>();
                foreach (TTemplate template in templates)
                {
                    classifyResult.Clear();
                    GetKeyValues(template, ref classifyResult);

                    foreach (KeyValuePair<TKey, TValue> pair in classifyResult)
                    {
                        List<TValue> destList = m_IndexMap.GetOrNew(pair.Key);
                        destList.Add(pair.Value);
                    }
                }
            }

            public IReadOnlyList<TValue> GetTemplates(TKey key)
            {
                return m_IndexMap.TryGetValue(key, out List<TValue> destList)
                    ? destList
                    : new List<TValue>();
            }

            public bool TryGetTemplates(TKey key, out IReadOnlyList<TValue> templates)
            {
                if (m_IndexMap.TryGetValue(key, out List<TValue> destList))
                {
                    templates = destList;
                    return true;
                }

                templates = null;
                return false;
            }

            public IEnumerable<TValue> AllValues => m_IndexMap.SelectMany(pair => pair.Value);

            protected abstract void GetKeyValues
            (
                TTemplate template,
                ref List<KeyValuePair<TKey, TValue>> classifyResults
            );
        }
    }
}