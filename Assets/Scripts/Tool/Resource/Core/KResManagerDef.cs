#region

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

#endregion

namespace Tool.Resource
{
    public class ResGlobalConfig
    {
        public int PrefabCacheMax = 10;
        public bool UnloadAbImmediately = true;
        public bool Verbose;
        public bool AssetLoadProfile;
        public float AssetLoadTimeLogThresholdMs = 100f;
        public bool BundleLoadProfile;
        public float BundleLoadTimeLogThresholdMs = 100f;
        public string ExternPreloadFile;
        public bool PreloadFileTrace;
        public bool PreloadTraceModeNew;
        public float PreloadFrameTime = 0.2f;
        public bool PreloadAsync;
        public bool EncryptSaveData = true;
        public bool EnableSRDebugger;
    }

    public static class KResManagerConfig
    {
        private static ResGlobalConfig m_ResGlobalConfig;

        public static ResGlobalConfig ResGlobalConfig
        {
            get
            {
                if (m_ResGlobalConfig == null)
                {
                    m_ResGlobalConfig = LoadResManagerConfig();
                }

                return m_ResGlobalConfig;
            }
        }

        public static ResGlobalConfig LoadResManagerConfig()
        {
            var result = new ResGlobalConfig();

            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, "Debug",
                    KResManagerDef.ResManagerConfigFile);
                if (!File.Exists(path))
                {
                    return result;
                }

                string content = File.ReadAllText(path);
                ResGlobalConfig loaded = JsonConvert.DeserializeObject<ResGlobalConfig>(content);
                if (loaded != null)
                {
                    result = loaded;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"LoadResManagerConfig failed: {e.Message}");
            }

            return result;
        }
    }

    public static class KResManagerDef
    {
#if UNITY_EDITOR && !BUNDLE
        public static bool IsEditorModel = true;
#else
        public static bool IsEditorModel = false;
#endif

#if ENABLE_ENCRYPT_AB_OFFSET
        public static bool EnableEncryptABOffest = true;
#else
        public static bool EnableEncryptABOffest = false;
#endif

        public const string MAssetBundleRecordFile = "AssetBundleBuildRecord.txt";
        public const string LoadingAbRecordFile = "LoadingAbRecords.txt";
        public const string ResManagerConfigFile = "ResManagerConfig.json";
        public const string BsonFileSuffix = ".bson.bytes";
        public const string BundleDependBytes = "BundleDepend.bytes";

        public static readonly List<string> PermanentAssetPath = new List<string>();

        public static readonly List<string> PermanentAssetDirPath = new List<string>
        {
            "Assets/Resource/ArtBase/UI/"
        };

        public static readonly List<string> PermanentExcludeAssetDirPath = new List<string>
        {
            "Assets/Resource/ArtBase/UI/Common/Font"
        };

        public static readonly string MAssetBundleRootPath =
            Path.Combine(Application.streamingAssetsPath, "Bundles", GetPlatformFolder());

#if UNITY_ANDROID
        public static readonly string MAssetBundleRootPath_Android =
            Path.Combine("Bundles", GetPlatformFolder());
#endif

        public static readonly string LoadingAbRecordFilePath =
            Path.Combine("Debug", LoadingAbRecordFile);

#if UNITY_ANDROID
        public static readonly string GlobalManifestAbName =
            Path.Combine(MAssetBundleRootPath_Android, GetPlatformFolder(Application.platform));
#else
        public static readonly string GlobalManifestAbName =
            Path.Combine(MAssetBundleRootPath, GetPlatformFolder());
#endif

        public static readonly string BundleDependBytesPath =
            Path.Combine("Bundles", GetPlatformFolder(), BundleDependBytes);

        public static string SessionID { get; private set; }

        public static string PersistentDataPath
        {
            get
            {
                if (string.IsNullOrEmpty(m_PersistentDataPath))
                {
                    RefreshPersistentDataPath();
                }

                return m_PersistentDataPath;
            }
        }

        public static event Action OnPersistentDataPathChanged;

        private static string m_PersistentDataPath = string.Empty;

        public static void SetSessionID(string sessionId)
        {
            Debug.Log($"Set SessionID: {sessionId}");
            SessionID = sessionId;
            RefreshPersistentDataPath();
        }

        private static void RefreshPersistentDataPath()
        {
            string oldPath = m_PersistentDataPath;
            m_PersistentDataPath = Application.persistentDataPath;

            try
            {
                string branchInfo = "default";
                m_PersistentDataPath = string.IsNullOrEmpty(SessionID)
                    ? Path.Combine(Application.persistentDataPath, branchInfo, "guest")
                    : Path.Combine(Application.persistentDataPath, branchInfo, SessionID);

                if (!Directory.Exists(m_PersistentDataPath))
                {
                    Directory.CreateDirectory(m_PersistentDataPath);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (!string.Equals(oldPath, m_PersistentDataPath, StringComparison.Ordinal))
            {
                OnPersistentDataPathChanged?.Invoke();
            }
        }

        public static string GetPlatformFolder()
        {
#if UNITY_ANDROID
            return "Android";
#elif UNITY_IOS
            return "IOS";
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_SWITCH
            return "Windows";
#elif UNITY_STANDALONE_OSX
            return "OSX";
#else
            return "Unknown";
#endif
        }

        private static string GetPlatformFolder(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.Android:
                    return "Android";
                case RuntimePlatform.IPhonePlayer:
                    return "IOS";
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.Switch:
                    return "Windows";
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                    return "OSX";
                default:
                    return "Unknown";
            }
        }
    }
}