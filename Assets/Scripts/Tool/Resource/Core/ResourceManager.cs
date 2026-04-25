#region

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

#endregion

namespace Tool.Resource
{
    public static class ResourceManager
    {
        public static readonly List<string> ValidResourceLoadPath = new List<string>();

        public static bool templateLoaded;

        public static EGameLoadingState LoadingState = EGameLoadingState.BeforeStartUp;

        public static readonly KResourceCachePool resCachePool = new KResourceCachePool();

        public static readonly Dictionary<string, KAssetAsyncLoadHandle> AssetAsyncLoadHandleDic =
            new Dictionary<string, KAssetAsyncLoadHandle>();

        private static bool s_EnableCacheAsset;
        private static readonly int s_DefaultAssetCacheTime = 120;

        public static void ReleaseMemory()
        {
            Resources.UnloadUnusedAssets();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        public static void EnableCacheAsset(bool enable)
        {
            s_EnableCacheAsset = enable;
        }

        public static bool IsAssetCacheEnabled()
        {
            return s_EnableCacheAsset;
        }

        public static void ClearAssetCache()
        {
            resCachePool.Clear();
        }

        public static void RemoveCachedAsset(string assetPath)
        {
            resCachePool.RemoveAsset(assetPath);
        }

        public static void UpdateAssetCache()
        {
            resCachePool.Update();
        }

        public static string GetTextFromSA(string path)
        {
            return KResManagerUtils.GetTextForStreamingAssets(path);
        }

        public static byte[] GetBytesForStreamingAssets(string path)
        {
            return KResManagerUtils.GetBytesForStreamingAssets(path);
        }

        public static bool IsAssetExist(string assetName, bool isFolder = false)
        {
            assetName = KResManagerUtils.FormatAssetPath(assetName);
            GameObject gameObject = resCachePool.GetAsset(assetName) as GameObject;
            if (gameObject != null)
            {
                return true;
            }

            if (KResManagerDef.IsEditorModel)
            {
#if UNITY_EDITOR && !BUNDLE
                if (isFolder)
                {
                    return AssetDatabase.IsValidFolder(assetName);
                }

                return AssetDatabase.GetMainAssetTypeAtPath(assetName) != null;
#else
                return false;
#endif
            }

            if (isFolder)
            {
                List<string> dstBundleNames = KAssetBundleManager.GetBundleNames(assetName,
                    string.Empty,
                    KResManagerDef.BsonFileSuffix);
                return dstBundleNames != null && dstBundleNames.Count > 0;
            }

            string bundleName = KAssetBundleManager.GetBundleName(ref assetName, false);
            return !string.IsNullOrEmpty(bundleName);
        }

        public static bool IsValidResourceLoadPath(string path)
        {
            if (!templateLoaded || ValidResourceLoadPath.Count == 0)
            {
                return true;
            }

            foreach (string rootPath in ValidResourceLoadPath)
            {
                if (path.StartsWithOrdinal(rootPath))
                {
                    return true;
                }
            }

            if (ValidResourceLoadPath.Count > 0)
            {
                Debug.LogError($"非法路径{path}不以{ValidResourceLoadPath[0]}开头");
            }

            return false;
        }

        public static GameObject Load(string assetName, bool setAbPermanent = false)
        {
            assetName = KResManagerUtils.FormatAssetPath(assetName);

            GameObject gameObject = resCachePool.GetAsset(assetName) as GameObject;
            if (gameObject != null)
            {
                return gameObject;
            }

            if (KResManagerDef.IsEditorModel)
            {
#if UNITY_EDITOR && !BUNDLE
                gameObject = AssetDatabase.LoadAssetAtPath<GameObject>(assetName);
#endif
            }
            else
            {
                using (KResourceLoadingProfiler.AutoProfile("ResourceManager.Load", assetName))
                {
                    string assetBundleName = KAssetBundleManager.GetBundleName(ref assetName);
                    if (string.IsNullOrEmpty(assetBundleName))
                    {
                        goto Exit0;
                    }

                    if (setAbPermanent)
                    {
                        KAssetBundleManager.AddPermanentAbName(assetBundleName, assetName);
                    }

                    AssetBundle assetBundle =
                        KAssetBundleManager.LoadAssetBundles(assetBundleName, assetName);
                    if (assetBundle != null)
                    {
                        gameObject = KAssetBundleManager.LoadAssetFromAb(assetBundle, assetName);
                        if (s_EnableCacheAsset && gameObject != null)
                        {
                            resCachePool.AddAsset(assetName, gameObject, s_DefaultAssetCacheTime);
                        }
                    }
                }
            }

            Exit0:
            if (gameObject == null)
            {
                Debug.LogError("ResourceManager Load asset " + assetName + " failed!");
            }

            return gameObject;
        }

        public static T Load<T>(string assetName, bool preload = false, bool setAbPermanent = false)
            where T : Object
        {
            assetName = KResManagerUtils.FormatAssetPath(assetName);

            T result = GetCachedAsset<T>(assetName, preload);
            if (result)
            {
                return result;
            }

            if (KResManagerDef.IsEditorModel)
            {
#if UNITY_EDITOR && !BUNDLE
                if (assetName.EndsWithOrdinal(".json") || assetName.EndsWithOrdinal(".txt"))
                {
                    string assetNameBytes = Application.dataPath + "/../" + assetName + ".bytes";
                    if (File.Exists(assetNameBytes))
                    {
                        assetName += ".bytes";
                    }
                }

                result = AssetDatabase.LoadAssetAtPath<T>(assetName);
                if ((s_EnableCacheAsset || preload) && result != null)
                {
                    resCachePool.AddAsset(assetName, result,
                        preload ? -1 : s_DefaultAssetCacheTime);
                }
#endif
            }
            else
            {
                using (KResourceLoadingProfiler.AutoProfile("ResourceManager.Load", assetName))
                {
                    string assetBundleName = KAssetBundleManager.GetBundleName(ref assetName);
                    if (string.IsNullOrEmpty(assetBundleName))
                    {
                        goto Exit0;
                    }

                    if (setAbPermanent)
                    {
                        KAssetBundleManager.AddPermanentAbName(assetBundleName, assetName);
                    }

                    AssetBundle assetBundle =
                        KAssetBundleManager.LoadAssetBundles(assetBundleName, assetName);
                    if (assetBundle != null)
                    {
                        result = KAssetBundleManager.LoadAssetFromAb<T>(assetBundle, assetName);
                        if ((s_EnableCacheAsset || preload) && result != null)
                        {
                            resCachePool.AddAsset(assetName, result,
                                preload ? -1 : s_DefaultAssetCacheTime);
                        }
                    }
                }
            }

            Exit0:
            return result;
        }

        public static List<string> GetFilesInDirectory(string path)
        {
            List<string> result = KListPool<string>.Claim();
            path = KResManagerUtils.FormatAssetPath(path);

            using (KResourceLoadingProfiler.AutoProfile("ResourceManager.GetFilesInDirectory", path,
                       true))
            {
                List<ResourceKey> files = ResourceKey.GetResourceKeysInDir(path);
                foreach (ResourceKey key in files)
                {
                    result.Add(key.Path);
                }

                files.Release();
            }

            return result;
        }

        public static List<T> LoadAll<T>(string path) where T : Object
        {
            path = KResManagerUtils.FormatAssetPath(path);

            List<T> result = KListPool<T>.Claim();
            using (KResourceLoadingProfiler.AutoProfile("ResourceManager.LoadAll", path, true))
            {
                List<ResourceKey> keys = ResourceKey.GetResourceKeysInDir(path);
                foreach (ResourceKey key in keys)
                {
                    T o = key.Load<T>();
                    if (o)
                    {
                        result.Add(o);
                    }
                }

                keys.Release();
            }

            if (result.Count == 0)
            {
                Debug.LogWarning($"[{typeof(T).Name}] 未读取到资源: {path}");
            }

            return result;
        }

        public static KAssetAsyncLoadHandle LoadAsync<T>
        (
            string assetName,
            Action<KAssetAsyncLoadHandle> completeCallBack,
            bool setAbPermanent = false
        ) where T : Object
        {
            KAssetAsyncLoadHandle asyncLoadHandle;

            assetName = KResManagerUtils.FormatAssetPath(assetName);

            Object cached = resCachePool.GetAsset(assetName);
            if (cached != null)
            {
                asyncLoadHandle =
                    new KAssetAsyncLoadHandle(assetName, typeof(T), cached, completeCallBack);
                AssetAsyncLoadHandleDic[assetName] = asyncLoadHandle;
                return asyncLoadHandle;
            }

            if (AssetAsyncLoadHandleDic.TryGetValue(assetName, out asyncLoadHandle))
            {
                return asyncLoadHandle;
            }

            if (KResManagerDef.IsEditorModel)
            {
#if UNITY_EDITOR && !BUNDLE
                if (assetName.EndsWithOrdinal(".json") || assetName.EndsWithOrdinal(".txt"))
                {
                    string assetNameBytes = Application.dataPath + "/../" + assetName + ".bytes";
                    if (File.Exists(assetNameBytes))
                    {
                        assetName += ".bytes";
                    }
                }

                Object result = AssetDatabase.LoadAssetAtPath<T>(assetName);
                if (result == null)
                {
                    Debug.LogError($"Load asset from {assetName} result null!");
                }

                asyncLoadHandle =
                    new KAssetAsyncLoadHandle(assetName, typeof(T), result, completeCallBack);
#else
                asyncLoadHandle =
                    new KAssetAsyncLoadHandle(assetName, typeof(T), (Object)null, completeCallBack);
#endif
            }
            else
            {
                using (KResourceLoadingProfiler.AutoProfile("ResourceManager.LoadAsync", assetName))
                {
                    string assetBundleName = KAssetBundleManager.GetBundleName(ref assetName);
                    if (setAbPermanent)
                    {
                        KAssetBundleManager.AddPermanentAbName(assetBundleName, assetName);
                    }

                    KAssetBundleManager.LoadAssetBundlesAsync(assetBundleName, assetName,
                        out KABLoadAsyncHandle mainKabLoadHandle);
                    asyncLoadHandle = new KAssetAsyncLoadHandle(assetName, typeof(T),
                        mainKabLoadHandle, completeCallBack);
                }
            }

            AssetAsyncLoadHandleDic[assetName] = asyncLoadHandle;
            return asyncLoadHandle;
        }

        public static KAssetAsyncLoadHandle LoadAllAsync<T>
        (
            string path,
            Action<KAssetAsyncLoadHandle> completeCallBack
        ) where T : Object
        {
            KAssetAsyncLoadHandle asyncLoadHandle;

            path = KResManagerUtils.FormatAssetPath(path);
            if (AssetAsyncLoadHandleDic.TryGetValue(path, out asyncLoadHandle))
            {
                return asyncLoadHandle;
            }

            if (KResManagerDef.IsEditorModel)
            {
#if UNITY_EDITOR && !BUNDLE
                var objList = new List<Object>();
                if (Directory.Exists(path))
                {
                    foreach (string file in Directory.GetFiles(path, "*.*",
                                 SearchOption.AllDirectories))
                    {
                        if (file.Contains(".svn\\") ||
                            file.ToLowerInvariant().EndsWithOrdinal(".meta"))
                        {
                            continue;
                        }

                        string assetPath = file.Replace('\\', '/');
                        T dstObject = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                        if (dstObject)
                        {
                            objList.Add(dstObject);
                        }
                    }
                }

                asyncLoadHandle =
                    new KAssetAsyncLoadHandle(path, typeof(T), objList, completeCallBack);
#else
                asyncLoadHandle =
                    new KAssetAsyncLoadHandle(path, typeof(T), new List<Object>(), completeCallBack);
#endif
            }
            else
            {
                using (KResourceLoadingProfiler.AutoProfile("ResourceManager.LoadAllAsync", path,
                           true))
                {
                    var abLoadHandleSet = new List<KABLoadAsyncHandle>();
                    Dictionary<string, string> abNameRecords =
                        KAssetBundleManager.GetBundlePairs(path, string.Empty,
                            KResManagerDef.BsonFileSuffix);
                    foreach (KeyValuePair<string, string> t in abNameRecords)
                    {
                        KAssetBundleManager.LoadAssetBundlesAsync(t.Value, t.Key,
                            out KABLoadAsyncHandle mainKabLoadHandle);
                        if (mainKabLoadHandle != null)
                        {
                            abLoadHandleSet.Add(mainKabLoadHandle);
                        }
                    }

                    asyncLoadHandle = new KAssetAsyncLoadHandle(path, typeof(T), abLoadHandleSet,
                        completeCallBack);
                }
            }

            AssetAsyncLoadHandleDic[path] = asyncLoadHandle;
            return asyncLoadHandle;
        }

        public static void UnloadAsset
        (
            string assetName,
            bool selfOnly = false,
            bool unloadAB = false,
            bool releaseAsset = false
        )
        {
            if (!KResManagerDef.IsEditorModel && unloadAB)
            {
                string assetBundleName = KAssetBundleManager.GetBundleName(ref assetName);
                if (string.IsNullOrEmpty(assetBundleName))
                {
                    return;
                }

                KAssetBundleManager.UnloadAssetBundles(assetBundleName, assetName, selfOnly,
                    releaseAsset);
            }
        }

        public static void UnloadUnusedAssetsAndGC()
        {
            Resources.UnloadUnusedAssets();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        public static void RecordLoadingAB(string tAsset, string abRealPath)
        {
            // 保留API，当前项目默认关闭记录
        }

        public static bool WritePersistentFile
            (string relativePath, byte[] data, EWriteMode writeMode)
        {
            string persistentDir = KResManagerDef.PersistentDataPath;
            string fullPath = Path.Combine(persistentDir, relativePath);

            try
            {
                string dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                FileMode fileMode = writeMode == EWriteMode.Overwrite
                    ? FileMode.Create
                    : FileMode.Append;
                using (var stream = new FileStream(fullPath, fileMode))
                {
                    stream.Write(data, 0, data.Length);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Write data to file [{fullPath}] exception [{e}]!");
                Debug.LogException(e);
                return false;
            }

            return true;
        }

        public static void CopyTo(string source, string target, bool canOverWrite = true)
        {
            try
            {
                string dir = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.Copy(source, target, canOverWrite);
            }
            catch (IOException e)
            {
                Debug.LogError($"Copy [{source}] to [{target}] exception {e}!");
            }
        }

        public static void OnApplicationQuit()
        {
        }

        private static T GetCachedAsset<T>(string assetName, bool preload) where T : Object
        {
            Object cached = resCachePool.GetAsset(assetName, preload);
            if (cached is T result)
            {
                return result;
            }

            if (typeof(T) != typeof(Sprite) || !(cached is Texture2D cachedTexture))
            {
                return null;
            }

            var sprite = Texture2DToSprite(cachedTexture) as T;
            resCachePool.AddAsset(assetName, sprite);
            return sprite;
        }

        private static Sprite Texture2DToSprite(Texture2D texture)
        {
            return Sprite.Create(texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));
        }
    }

    public enum EWriteMode
    {
        Overwrite,
        Append
    }

    public enum EGameLoadingState
    {
        BeforeStartUp,
        InStartUp,
        LoadingScene,
        SceneLoaded
    }
}