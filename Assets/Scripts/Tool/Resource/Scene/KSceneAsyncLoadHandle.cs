#region

using System;
using Tool.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

#endregion

namespace Tool.Resource
{
    public class KSceneAsyncLoadHandle
    {
        public string SceneName;
        public int Priority;
        public bool IsDirty;
        public KABLoadAsyncHandle SceneAbLoadHandle;
        public AsyncOperation SceneAsyncOpt;
        public AsyncOperation UnloadSceneAsyncOpt;
        public LoadSceneMode Mode;
        public Action<AsyncOperation> AbLoadedCallBack;
        public Action<KSceneAsyncLoadHandle> SceneLoadedCallBack;

        public KSceneAsyncLoadHandle
        (
            string sceneName,
            KABLoadAsyncHandle sceneAbLoadHandle,
            LoadSceneMode sceneMode = LoadSceneMode.Single,
            int priority = 0,
            Action<AsyncOperation> abLoadedCallBack = null
        )
        {
            Mode = sceneMode;
            SceneName = sceneName;
            Priority = priority;
            IsDirty = false;
            SceneAbLoadHandle = sceneAbLoadHandle;
            AbLoadedCallBack = abLoadedCallBack;
        }

        public KSceneAsyncLoadHandle
        (
            string sceneName,
            KABLoadAsyncHandle sceneAbLoadHandle,
            AsyncOperation sceneAsyncOpt,
            LoadSceneMode sceneMode = LoadSceneMode.Single,
            int priority = 0,
            Action<AsyncOperation> abLoadedCallBack = null
        )
        {
            Mode = sceneMode;
            SceneName = sceneName;
            Priority = priority;
            IsDirty = false;
            SceneAbLoadHandle = sceneAbLoadHandle;
            AbLoadedCallBack = abLoadedCallBack;
            SceneAsyncOpt = sceneAsyncOpt;
            if (SceneAsyncOpt != null)
            {
                AbLoadedCallBack?.Invoke(SceneAsyncOpt);
            }
        }

        private bool m_IsDone;

        public bool IsDone
        {
            get
            {
                Update();
                return m_IsDone;
            }
        }

        public void Update()
        {
            if (m_IsDone)
            {
                return;
            }

            if (SceneAsyncOpt == null &&
                SceneAbLoadHandle != null &&
                SceneAbLoadHandle.IsDone &&
                KSceneManager.IsHighPriorityFinished(this))
            {
                if (!IsDirty)
                {
                    SceneAsyncOpt = SceneManager.LoadSceneAsync(SceneName, Mode);
                    AbLoadedCallBack?.Invoke(SceneAsyncOpt);
                    Log.Info($"[SceneManager] Asset loaded for {SceneName} with mode [{Mode}].");
                }
                else
                {
                    m_IsDone = true;
                }
            }

            if (SceneAsyncOpt != null && SceneAsyncOpt.isDone)
            {
                m_IsDone = true;
                if (!IsDirty)
                {
                    SceneLoadedCallBack?.Invoke(this);
                    Log.Info($"[SceneManager] Scene async done for {SceneName}.");
                }
                else
                {
                    Scene scene = SceneManager.GetSceneByName(SceneName);
                    if (scene.isLoaded)
                    {
                        UnloadSceneAsyncOpt = SceneManager.UnloadSceneAsync(SceneName);
                    }
                    else
                    {
                        Log.Error(
                            $"[SceneManager] [{scene.name}] is not loaded when try unload async!");
                    }
                }
            }
        }

        public void SetDirty()
        {
            IsDirty = true;
        }
    }
}