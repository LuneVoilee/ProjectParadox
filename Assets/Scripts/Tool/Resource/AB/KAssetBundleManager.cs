#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

#endregion

namespace Tool.Resource
{
    internal class AssetBundleInfo
    {
        public AssetBundle MAssetBundle;
        public int MReferencedCount;
    }

    public static class KAssetBundleManager
    {
        private static AssetBundle s_GlobalManifestAb;
        private static AssetBundleManifest s_GlobalAssetBundleManifest;

        private static readonly Stopwatch s_Stopwatch = new Stopwatch();
        private static readonly List<string> s_PermanentAbName = new List<string>();
        private static readonly float s_MaxAbLoadWaitSeconds = 2f;

        private static int ABLoadOffset =>
            KResManagerDef.EnableEncryptABOffest
                ? DataEncryptUtil.EncryptedEncryptFlagBytes.Length
                : 0;

        private static readonly Dictionary<string, AssetBundleInfo> MLoadedAssetBundles =
            new Dictionary<string, AssetBundleInfo>(StringComparer.InvariantCultureIgnoreCase);

        private static readonly Dictionary<string, string> MAbNameRecords =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static readonly Dictionary<string, KABLoadAsyncHandle> ABLoadingDic =
            new Dictionary<string, KABLoadAsyncHandle>();

        static KAssetBundleManager()
        {
            if (!KResManagerDef.IsEditorModel)
            {
                LoadRecordFile();
            }
        }

        private static void LoadRecordFile()
        {
            Debug.Log($"DataPath: {Application.dataPath}");
            Debug.Log($"StreamingAssetPath: {Application.streamingAssetsPath}");
            LoadRecordFileOtherPlatform();
        }

        public static AssetBundle LoadAssetBundles(string assetBundleName, string assetPath)
        {
            AssetBundle dstAssetBundle = null;
            List<string> dependenceAbNames = GetDependAb(assetBundleName, assetPath);

            foreach (string abName in dependenceAbNames)
            {
                if ((MLoadedAssetBundles.ContainsKey(abName) &&
                     MLoadedAssetBundles[abName].MAssetBundle != null) ||
                    ABLoadingDic.ContainsKey(abName))
                {
                    continue;
                }

                string abRealPath = Path.Combine(KResManagerDef.MAssetBundleRootPath, abName);
                AssetBundle assetBundleNow = LoadAbFromFile(abRealPath);
                if (assetBundleNow != null)
                {
                    var assetBundleInfoNow = new AssetBundleInfo
                    {
                        MAssetBundle = assetBundleNow,
                        MReferencedCount = 1
                    };
                    MLoadedAssetBundles[abName] = assetBundleInfoNow;
                }
                else
                {
                    Debug.LogWarning("Load depends asset bundle " + abName + " failed!");
                }

                ResourceManager.RecordLoadingAB(assetPath, abRealPath);
            }

            if (MLoadedAssetBundles.ContainsKey(assetBundleName) &&
                MLoadedAssetBundles[assetBundleName].MAssetBundle != null)
            {
                dstAssetBundle = MLoadedAssetBundles[assetBundleName].MAssetBundle;
            }
            else
            {
                string abRealPath =
                    Path.Combine(KResManagerDef.MAssetBundleRootPath, assetBundleName);

                AssetBundle abSelf;
                if (ABLoadingDic.TryGetValue(assetBundleName, out KABLoadAsyncHandle loadingHandle))
                {
                    DateTime startTime = DateTime.Now;
                    while (true)
                    {
                        TimeSpan timeTrans = DateTime.Now - startTime;
                        if (loadingHandle.IsDone)
                        {
                            abSelf = loadingHandle.AssetBundle;
                            break;
                        }

                        if (timeTrans.TotalSeconds > s_MaxAbLoadWaitSeconds)
                        {
                            Debug.LogWarning(
                                $"Load ab while wait seconds {timeTrans.TotalSeconds}, over max, break!");
                            abSelf = null;
                            break;
                        }
                    }
                }
                else
                {
                    abSelf = LoadAbFromFile(abRealPath);
                }

                if (abSelf != null)
                {
                    var assetBundleInfo = new AssetBundleInfo
                    {
                        MAssetBundle = abSelf,
                        MReferencedCount = 1
                    };
                    dstAssetBundle = abSelf;
                    MLoadedAssetBundles[assetBundleName] = assetBundleInfo;
                }
                else
                {
                    Debug.LogError("Load asset bundle" + assetBundleName + "failed!");
                }

                ResourceManager.RecordLoadingAB(assetPath, abRealPath);
            }

            return dstAssetBundle;
        }

        public static AssetBundle LoadABSelfOnly(string assetBundleName)
        {
            if (MLoadedAssetBundles.TryGetValue(assetBundleName, out AssetBundleInfo existed) &&
                existed.MAssetBundle != null)
            {
                return existed.MAssetBundle;
            }

            string abRealPath = Path.Combine(KResManagerDef.MAssetBundleRootPath, assetBundleName);
            AssetBundle abSelf = LoadAbFromFile(abRealPath);
            if (abSelf != null)
            {
                MLoadedAssetBundles[assetBundleName] = new AssetBundleInfo
                {
                    MAssetBundle = abSelf,
                    MReferencedCount = 1
                };
                return abSelf;
            }

            Debug.LogError("Load asset bundle" + assetBundleName + "failed!");
            return null;
        }

        public static void LoadAssetBundlesAsync
        (
            string assetBundleName,
            string assetPath,
            out KABLoadAsyncHandle mainKabLoadHandle
        )
        {
            List<string> dependenceAbNames = GetDependAb(assetBundleName, assetPath);
            var dependABLoadHandleSet = new List<KABLoadAsyncHandle>();

            foreach (string abName in dependenceAbNames)
            {
                if (MLoadedAssetBundles.TryGetValue(abName, out AssetBundleInfo info) &&
                    info.MAssetBundle != null)
                {
                    continue;
                }

                if (ABLoadingDic.TryGetValue(abName, out KABLoadAsyncHandle existing))
                {
                    dependABLoadHandleSet.Add(existing);
                    continue;
                }

                string abRealPath = Path.Combine(KResManagerDef.MAssetBundleRootPath, abName);
                AssetBundleCreateRequest abRequest = LoadAbAsync(abRealPath);
                var abAsyncLoadHandle = new KABLoadAsyncHandle(abName, abRequest, null);
                dependABLoadHandleSet.Add(abAsyncLoadHandle);
                ABLoadingDic[abName] = abAsyncLoadHandle;

                ResourceManager.RecordLoadingAB(assetPath, abRealPath);
            }

            if (MLoadedAssetBundles.TryGetValue(assetBundleName, out AssetBundleInfo mainInfo) &&
                mainInfo.MAssetBundle != null)
            {
                mainKabLoadHandle =
                    new KABLoadAsyncHandle(assetBundleName, null, dependABLoadHandleSet)
                    {
                        AssetBundle = mainInfo.MAssetBundle
                    };
                return;
            }

            string mainAbRealPath =
                Path.Combine(KResManagerDef.MAssetBundleRootPath, assetBundleName);
            AssetBundleCreateRequest mainRequest = LoadAbAsync(mainAbRealPath);
            mainKabLoadHandle =
                new KABLoadAsyncHandle(assetBundleName, mainRequest, dependABLoadHandleSet);
            ResourceManager.RecordLoadingAB(assetPath, mainAbRealPath);
        }

        public static void GetBundleByAssetName(string pakAssetPath, out AssetBundle assetBundle)
        {
            pakAssetPath = KResManagerUtils.FormatAssetPath(pakAssetPath);
            string bundleName = GetBundleName(ref pakAssetPath);

            if (MLoadedAssetBundles.TryGetValue(bundleName, out AssetBundleInfo info) &&
                info.MAssetBundle != null)
            {
                assetBundle = info.MAssetBundle;
            }
            else
            {
                assetBundle = LoadAssetBundles(bundleName, pakAssetPath);
            }
        }

        private static AssetBundle LoadAbFromFile(string abName)
        {
            if (KResManagerConfig.ResGlobalConfig.BundleLoadProfile)
            {
                if (KResManagerConfig.ResGlobalConfig.Verbose)
                {
                    Debug.Log($"Begin LoadAssetBundle : {abName}.");
                }

                Profiler.BeginSample("LoadAbFromFile");
                s_Stopwatch.Restart();
                AssetBundle ab1 = AssetBundle.LoadFromFile(abName, 0U, (ulong)ABLoadOffset);
                s_Stopwatch.Stop();
                Profiler.EndSample();

                if (s_Stopwatch.ElapsedMilliseconds >=
                    KResManagerConfig.ResGlobalConfig.BundleLoadTimeLogThresholdMs)
                {
                    Debug.Log(
                        $"End LoadAssetBundle : {abName} in {s_Stopwatch.ElapsedMilliseconds}(ms).");
                }

                return ab1;
            }

            if (KResManagerConfig.ResGlobalConfig.Verbose)
            {
                Debug.Log($"LoadAssetBundle : {abName}.");
            }

            return AssetBundle.LoadFromFile(abName, 0U, (ulong)ABLoadOffset);
        }

        private static AssetBundleCreateRequest LoadAbAsync(string abName)
        {
            return AssetBundle.LoadFromFileAsync(abName, 0, (ulong)ABLoadOffset);
        }

        public static void UnloadAllBundles()
        {
            Profiler.BeginSample("UnloadAllBundles");

            var removeKeyList = new List<string>();
            foreach (KeyValuePair<string, AssetBundleInfo> bundlePair in MLoadedAssetBundles)
            {
                if (bundlePair.Value?.MAssetBundle == null ||
                    s_PermanentAbName.Contains(bundlePair.Key))
                {
                    continue;
                }

                bundlePair.Value.MAssetBundle.Unload(false);
                removeKeyList.Add(bundlePair.Key);
            }

            foreach (string removeKey in removeKeyList)
            {
                MLoadedAssetBundles.Remove(removeKey);
            }

            Profiler.EndSample();
        }

        public static void AddPermanentAbName
            (string bundleName, string assetPath, bool includeDependence = true)
        {
            if (includeDependence)
            {
                List<string> dependAbNames = GetDependAb(bundleName, assetPath);
                foreach (string abName in dependAbNames)
                {
                    if (string.IsNullOrEmpty(abName) || s_PermanentAbName.Contains(abName))
                    {
                        continue;
                    }

                    s_PermanentAbName.Add(abName);
                }
            }

            if (!s_PermanentAbName.Contains(bundleName))
            {
                s_PermanentAbName.Add(bundleName);
            }
        }

        public static void LoadRecordFileOtherPlatform()
        {
            string recordFilePath = Path.Combine(KResManagerDef.MAssetBundleRootPath,
                KResManagerDef.MAssetBundleRecordFile);
            if (!File.Exists(recordFilePath))
            {
                return;
            }

            string strRecord = File.ReadAllText(recordFilePath);
            strRecord = DataEncryptUtil.RC4Decrypt(strRecord, DataEncryptUtil.EncryptKeyStr);

            string[] lineArray = strRecord.Split('\n');
            foreach (string line in lineArray)
            {
                if (line.Length == 0 || !line.Contains(",", StringComparison.Ordinal))
                {
                    continue;
                }

                string[] strArray = line.Split(',');
                if (strArray[0].Length == 0 || strArray[1].Length == 0)
                {
                    continue;
                }

                string value = strArray[1].ToLowerInvariant().TrimEnd('\r', '\n');
                MAbNameRecords[strArray[0]] = value;
            }

            Debug.LogWarning($"Asset Bundle records count {MAbNameRecords.Count}");
        }

        public static string GetBundleName(ref string assetName, bool showError = true)
        {
            if (assetName == null)
            {
                return null;
            }

            string dstAssetBundle = null;
            string assetNameBytes = assetName + ".bytes";

            if (MAbNameRecords.ContainsKey(assetName))
            {
                dstAssetBundle = MAbNameRecords[assetName];
            }
            else if (MAbNameRecords.ContainsKey(assetNameBytes))
            {
                assetName = assetNameBytes;
                dstAssetBundle = MAbNameRecords[assetName];
            }
            else if (MAbNameRecords.ContainsKey($"{assetName}_fui.bytes"))
            {
                dstAssetBundle = MAbNameRecords[$"{assetName}_fui.bytes"];
            }
            else if (showError)
            {
                Debug.LogError($"Does not find the record of {assetName}");
            }

            return string.IsNullOrEmpty(dstAssetBundle) ? null : dstAssetBundle.TrimEnd('\r', '\n');
        }

        public static string GetSceneAssetName(string sceneName)
        {
            if (sceneName == null)
            {
                return null;
            }

            string assetName = $"/{sceneName}.unity";
            foreach (KeyValuePair<string, string> record in MAbNameRecords)
            {
                if (record.Key.Contains(assetName, StringComparison.Ordinal))
                {
                    return record.Key;
                }
            }

            return null;
        }

        public static string GetSceneABName(string sceneName)
        {
            if (sceneName == null)
            {
                return null;
            }

            string assetName = $"/{sceneName}.unity";
            foreach (KeyValuePair<string, string> record in MAbNameRecords)
            {
                if (record.Key.Contains(assetName, StringComparison.Ordinal))
                {
                    return record.Value;
                }
            }

            return null;
        }

        public static KeyValuePair<string, string> GetSceneABNamePair(string sceneName)
        {
            if (sceneName == null)
            {
                return new KeyValuePair<string, string>(string.Empty, string.Empty);
            }

            string assetName = $"/{sceneName}.unity";
            foreach (KeyValuePair<string, string> record in MAbNameRecords)
            {
                if (record.Key.Contains(assetName, StringComparison.Ordinal))
                {
                    return record;
                }
            }

            return new KeyValuePair<string, string>(string.Empty, string.Empty);
        }

        public static List<string> GetBundleNames
            (string assetPath, string includeSuffix = "", string excludeSuffix = "")
        {
            var dstBundleNames = new List<string>();
            if (assetPath.Length == 0)
            {
                Debug.LogError($"The input asset path {assetPath} is invalid");
                return dstBundleNames;
            }

            if (!assetPath.EndsWithOrdinal('/'))
            {
                assetPath += "/";
            }

            foreach (KeyValuePair<string, string> kvRecord in MAbNameRecords)
            {
                if (includeSuffix != string.Empty &&
                    !kvRecord.Key.EndsWithOrdinalIgnoreCase(includeSuffix))
                {
                    continue;
                }

                if (excludeSuffix != string.Empty &&
                    kvRecord.Key.EndsWithOrdinalIgnoreCase(excludeSuffix))
                {
                    continue;
                }

                if (!kvRecord.Key.StartsWithOrdinalIgnoreCase(assetPath))
                {
                    continue;
                }

                string formatBundleName = kvRecord.Value.TrimEnd('\r', '\n');
                if (!dstBundleNames.Contains(formatBundleName))
                {
                    dstBundleNames.Add(formatBundleName);
                }
            }

            return dstBundleNames;
        }

        public static Dictionary<string, string> GetBundlePairs
        (
            string assetDirPath,
            string includeSuffix = "",
            string excludeSuffix = "",
            string excludeDir = ""
        )
        {
            var dstBundleRecords = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(assetDirPath))
            {
                Debug.LogError($"The input asset path {assetDirPath} is invalid");
                return dstBundleRecords;
            }

            if (!assetDirPath.EndsWithOrdinal('/'))
            {
                assetDirPath += "/";
            }

            foreach (KeyValuePair<string, string> kvRecord in MAbNameRecords)
            {
                if (!string.IsNullOrEmpty(includeSuffix) &&
                    !kvRecord.Key.EndsWithOrdinalIgnoreCase(includeSuffix))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(excludeSuffix) &&
                    kvRecord.Key.EndsWithOrdinalIgnoreCase(excludeSuffix))
                {
                    continue;
                }

                if (!kvRecord.Key.StartsWithOrdinalIgnoreCase(assetDirPath))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(excludeDir) &&
                    kvRecord.Key.StartsWithOrdinalIgnoreCase(excludeDir))
                {
                    continue;
                }

                if (!dstBundleRecords.ContainsKey(kvRecord.Key))
                {
                    dstBundleRecords.Add(kvRecord.Key, kvRecord.Value);
                }
            }

            return dstBundleRecords;
        }

        public static void UnloadAssetBundles
        (
            string assetBundleName,
            string assetPath,
            bool selfOnly,
            bool destroyAsset = false
        )
        {
            if (s_PermanentAbName.Contains(assetBundleName))
            {
                return;
            }

            if (!selfOnly)
            {
                List<string> dependenceAb = GetDependAb(assetBundleName, assetPath);
                for (int i = 0; i < dependenceAb.Count; i++)
                {
                    if (!MLoadedAssetBundles.ContainsKey(dependenceAb[i]))
                    {
                        continue;
                    }

                    if (MLoadedAssetBundles[dependenceAb[i]].MAssetBundle != null &&
                        !s_PermanentAbName.Contains(dependenceAb[i]))
                    {
                        MLoadedAssetBundles[dependenceAb[i]].MAssetBundle.Unload(false);
                        MLoadedAssetBundles[dependenceAb[i]].MAssetBundle = null;
                    }

                    MLoadedAssetBundles.Remove(dependenceAb[i]);
                }
            }

            if (!MLoadedAssetBundles.ContainsKey(assetBundleName))
            {
                return;
            }

            if (MLoadedAssetBundles[assetBundleName].MAssetBundle != null &&
                !s_PermanentAbName.Contains(assetBundleName))
            {
                MLoadedAssetBundles[assetBundleName].MAssetBundle.Unload(destroyAsset);
            }

            MLoadedAssetBundles.Remove(assetBundleName);
        }

        public static GameObject LoadAssetFromAb(AssetBundle assetBundle, string assetName)
        {
            if (KResManagerConfig.ResGlobalConfig.Verbose)
            {
                Debug.Log($"Load asset [{assetName}] from {assetBundle.name}!");
            }

            GameObject gameObject = assetBundle.LoadAsset<GameObject>(assetName);
            if (gameObject == null)
            {
                Debug.LogError("Load asset " + assetName + " failed!");
            }

            return gameObject;
        }

        public static T LoadAssetFromAb<T>
            (AssetBundle assetBundle, string assetName) where T : Object
        {
            T result = null;
            if (KResManagerConfig.ResGlobalConfig.Verbose)
            {
                Debug.Log($"Load asset [{assetName}] from {assetBundle.name}!");
            }

            string lowerAssetName = assetName.ToLowerInvariant();
            if (!assetBundle.Contains(lowerAssetName))
            {
                Debug.LogError(lowerAssetName + "does not exist in " + assetBundle.name);
                return null;
            }

            result = assetBundle.LoadAsset<T>(lowerAssetName);
            if (result == null)
            {
                Debug.LogError("Load asset " + lowerAssetName + " failed!");
            }

            return result;
        }

        public static T[] LoadAllAssetFromAb<T>
        (
            AssetBundle assetBundle,
            string assetPathPrefix,
            string includeSuffix = ""
        ) where T : Object
        {
            if (KResManagerConfig.ResGlobalConfig.Verbose)
            {
                Debug.Log($"Load all asset [{assetPathPrefix}] from {assetBundle.name}!");
            }

            var result = new List<T>();
            string[] assetNames = assetBundle.GetAllAssetNames();
            string lowerPrefix = assetPathPrefix.ToLowerInvariant();

            foreach (string assetName in assetNames)
            {
                if (includeSuffix != string.Empty &&
                    !assetName.EndsWithOrdinalIgnoreCase(includeSuffix))
                {
                    continue;
                }

                if (assetName.StartsWithOrdinalIgnoreCase(lowerPrefix) &&
                    !assetName.EndsWithOrdinalIgnoreCase(".meta"))
                {
                    result.Add(assetBundle.LoadAsset<T>(assetName));
                }
            }

            return result.ToArray();
        }

        public static Dictionary<string, T> LoadAllAssetFromAbWithPath<T>
        (
            AssetBundle assetBundle,
            string assetPathPrefix,
            string includeSuffix = ""
        ) where T : Object
        {
            var result = new Dictionary<string, T>();
            string[] assetNames = assetBundle.GetAllAssetNames();
            string lowerPrefix = assetPathPrefix.ToLowerInvariant();

            foreach (string assetName in assetNames)
            {
                if (includeSuffix != string.Empty && !assetName.EndsWithOrdinal(includeSuffix))
                {
                    continue;
                }

                if (!assetName.StartsWithOrdinal(lowerPrefix) || assetName.EndsWithOrdinal(".meta"))
                {
                    continue;
                }

                T loadAsset = assetBundle.LoadAsset<T>(assetName);
                if (loadAsset != null)
                {
                    result[assetName] = loadAsset;
                }
            }

            return result;
        }

        private static List<string> GetDependenceAbNames(string assetBundleName)
        {
            if (s_GlobalManifestAb == null)
            {
                s_GlobalManifestAb = LoadAbFromFile(KResManagerDef.GlobalManifestAbName);
            }

            return GetDependenceAbNames(s_GlobalManifestAb, assetBundleName);
        }

        public static List<string> GetDependenceAbNames
            (AssetBundle manifestAb, string assetBundleName)
        {
            var dependenceAbNames = new List<string>();
            if (manifestAb != null && s_GlobalAssetBundleManifest == null)
            {
                s_GlobalAssetBundleManifest =
                    manifestAb.LoadAsset("AssetBundleManifest") as AssetBundleManifest;
            }

            if (s_GlobalAssetBundleManifest != null)
            {
                string[] dependenceAb =
                    s_GlobalAssetBundleManifest.GetAllDependencies(assetBundleName);
                if (KResManagerConfig.ResGlobalConfig.Verbose && Debug.isDebugBuild)
                {
                    Debug.LogError(
                        $"[ResourceManager] GetDependenceAbNames: {assetBundleName}, dependencies: {dependenceAb.Length}.");
                }

                dependenceAbNames = dependenceAb.ToList();
            }

            return dependenceAbNames;
        }

        public static void AddAbRecord(string abName, AssetBundle assetBundle)
        {
            if (!MLoadedAssetBundles.ContainsKey(abName))
            {
                MLoadedAssetBundles.Add(abName, new AssetBundleInfo
                {
                    MAssetBundle = assetBundle,
                    MReferencedCount = 1
                });
            }
        }

        public static List<string> GetDependAb(string abName, string assetPath)
        {
            return GetDependenceAbNames(abName);
        }

        public static List<string> GetDependAbWithPath(string abRootPath, string abName)
        {
            string manifestAbPath = Path.Combine(abRootPath, KResManagerDef.GetPlatformFolder());

            if (!s_GlobalManifestAb)
            {
                s_GlobalManifestAb = LoadAbFromFile(manifestAbPath);
            }

            if (!s_GlobalManifestAb)
            {
                Debug.LogError($"Load manifest asset bundle failed-[{manifestAbPath}]");
                return null;
            }

            return GetDependenceAbNames(s_GlobalManifestAb, abName);
        }

        public static void ReleaseManifestAB()
        {
            if (!s_GlobalManifestAb)
            {
                return;
            }

            s_GlobalManifestAb.Unload(false);
        }
    }
}