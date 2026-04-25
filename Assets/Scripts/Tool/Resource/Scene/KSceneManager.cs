using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tool.Resource
{
    public static class KSceneManager
    {
        private static readonly List<string> m_TmpRemoveKey = new List<string>();
        private static readonly Dictionary<string, KSceneAsyncLoadHandle> m_LoadingSceneSet =
            new Dictionary<string, KSceneAsyncLoadHandle>();

        public static void Update()
        {
            foreach (KeyValuePair<string, KSceneAsyncLoadHandle> handlePair in m_LoadingSceneSet)
            {
                if (handlePair.Value == null || handlePair.Value.IsDone)
                {
                    m_TmpRemoveKey.Add(handlePair.Key);
                }
            }

            foreach (string key in m_TmpRemoveKey)
            {
                m_LoadingSceneSet.Remove(key);
            }

            m_TmpRemoveKey.Clear();
        }

        public static bool IsHighPriorityFinished(KSceneAsyncLoadHandle srcHandle)
        {
            foreach (KeyValuePair<string, KSceneAsyncLoadHandle> handlePair in m_LoadingSceneSet)
            {
                if (handlePair.Value == srcHandle)
                {
                    continue;
                }

                if (handlePair.Value.Priority > srcHandle.Priority)
                {
                    return false;
                }
            }

            return true;
        }

        public static void LoadScene(string sceneName)
        {
            LoadScene(sceneName, LoadSceneMode.Single);
        }

        public static void LoadScene(string sceneName, LoadSceneMode mode)
        {
            if (!KResManagerDef.IsEditorModel)
            {
                KeyValuePair<string, string> abNamePair = KAssetBundleManager.GetSceneABNamePair(sceneName);
                KAssetBundleManager.LoadAssetBundles(abNamePair.Value, abNamePair.Key);
            }

            SceneManager.LoadScene(sceneName, mode);
        }

        public static KSceneAsyncLoadHandle LoadSceneAsync(string sceneName, int priority)
        {
            return LoadSceneAsync(sceneName, LoadSceneMode.Single, priority);
        }

        public static KSceneAsyncLoadHandle LoadSceneAsync(string sceneName, LoadSceneMode mode, int priority)
        {
            if (m_LoadingSceneSet.TryGetValue(sceneName, out KSceneAsyncLoadHandle existed))
            {
                return existed;
            }

            KSceneAsyncLoadHandle sceneAsyncLoadHandle;
            if (!KResManagerDef.IsEditorModel)
            {
                KeyValuePair<string, string> abNamePair = KAssetBundleManager.GetSceneABNamePair(sceneName);
                KAssetBundleManager.LoadAssetBundlesAsync(abNamePair.Value, abNamePair.Key, out KABLoadAsyncHandle abLoadHandle);
                sceneAsyncLoadHandle = new KSceneAsyncLoadHandle(sceneName, abLoadHandle, mode, priority);
            }
            else
            {
                AsyncOperation asyncOpt = SceneManager.LoadSceneAsync(sceneName, mode);
                asyncOpt.priority = priority;
                sceneAsyncLoadHandle = new KSceneAsyncLoadHandle(sceneName, null, asyncOpt, mode, priority);
            }

            m_LoadingSceneSet.Add(sceneName, sceneAsyncLoadHandle);
            return sceneAsyncLoadHandle;
        }

        public static AsyncOperation UnloadSceneAsync(string sceneName)
        {
            if (m_LoadingSceneSet.TryGetValue(sceneName, out KSceneAsyncLoadHandle loadingHandle))
            {
                loadingHandle.SetDirty();
                return loadingHandle.UnloadSceneAsyncOpt;
            }

            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (scene.isLoaded)
            {
                return SceneManager.UnloadSceneAsync(sceneName);
            }

            return null;
        }
    }
}
