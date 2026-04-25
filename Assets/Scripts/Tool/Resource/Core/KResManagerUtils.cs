#region

using System;
using System.IO;
using System.Text.RegularExpressions;
using Tool.Json;
using UnityEngine;

#endregion

namespace Tool.Resource
{
    public static class KResManagerUtils
    {
        public static string FormatAssetPath(string srcAssetPath)
        {
            if (string.IsNullOrEmpty(srcAssetPath))
            {
                return string.Empty;
            }

            if (srcAssetPath.Contains("\\"))
            {
                srcAssetPath = srcAssetPath.Replace('\\', '/');
            }

            srcAssetPath = Regex.Replace(srcAssetPath, "/+", "/");
            return srcAssetPath;
        }

        public static string GetTextForStreamingAssets(string path)
        {
            string localPath = Path.Combine(Application.streamingAssetsPath, path);
            if (!File.Exists(localPath))
            {
                Log.Error($"error while reading files : {localPath}");
                return string.Empty;
            }

            return File.ReadAllText(localPath);
        }

        public static byte[] GetBytesForStreamingAssets(string path, int offset = 0)
        {
            string localPath = Path.Combine(Application.streamingAssetsPath, path);
            if (!File.Exists(localPath))
            {
                Log.Error($"error while reading files : {localPath}");
                return Array.Empty<byte>();
            }

            byte[] bytes = File.ReadAllBytes(localPath);
            if (offset <= 0 || bytes.Length <= offset)
            {
                return bytes;
            }

            int len = bytes.Length - offset;
            var outBytes = new byte[len];
            Buffer.BlockCopy(bytes, offset, outBytes, 0, len);
            return outBytes;
        }

#if UNITY_EDITOR
        public static string GetEntirePathEditor(string assetPath)
        {
            int index = assetPath.IndexOf('/');
            string subAssetPath = assetPath.Substring(index);
            return Application.dataPath + subAssetPath;
        }
#endif
    }
}