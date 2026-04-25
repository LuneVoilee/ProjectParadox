#region

using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
#endif

#endregion

namespace Tool.Resource
{
    public struct ResourceKey
    {
        public string Path;
        public string Bundle;

        public override string ToString()
        {
            return string.IsNullOrEmpty(Bundle) ? Path : $"{Bundle}|{Path}";
        }

        public string GetFileName()
        {
            return System.IO.Path.GetFileName(Path);
        }

        public string GetFileNameWithoutExtension()
        {
            return System.IO.Path.GetFileNameWithoutExtension(Path);
        }

        public T Load<T>() where T : Object
        {
            return ResourceManager.Load<T>(Path);
        }

        public static List<ResourceKey> GetResourceKeysInDir(string dir)
        {
            List<ResourceKey> result = KListPool<ResourceKey>.Claim();

            if (KResManagerDef.IsEditorModel)
            {
#if UNITY_EDITOR && !BUNDLE
                if (Directory.Exists(dir))
                {
                    foreach (string file in Directory.GetFiles(dir, "*.*",
                                 SearchOption.AllDirectories))
                    {
                        if (file.Contains(".svn\\"))
                        {
                            continue;
                        }

                        string ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                        if (ext == ".meta")
                        {
                            continue;
                        }

                        result.Add(new ResourceKey
                        {
                            Path = KResManagerUtils.FormatAssetPath(file),
                            Bundle = null
                        });
                    }
                }
#endif
            }
            else
            {
                Dictionary<string, string> abNameRecords = KAssetBundleManager.GetBundlePairs(dir,
                    string.Empty,
                    KResManagerDef.BsonFileSuffix);
                foreach (KeyValuePair<string, string> t in abNameRecords)
                {
                    result.Add(new ResourceKey
                    {
                        Path = t.Key,
                        Bundle = t.Value
                    });
                }
            }

            return result;
        }
    }
}