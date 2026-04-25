#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

#endregion

namespace Tool.Resource
{
    public static class KResourceLoadingProfiler
    {
        internal class Item
        {
            public DateTime Timestamp;
            public bool IsDir;
            public string Path;
            public float Cost;
        }

        internal static readonly List<Item> Records = new List<Item>();

        private static readonly Stopwatch m_Stopwatch = new Stopwatch();

        public static void Dump(string filePath)
        {
            string dirPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dirPath) && !Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            using (var writer = new StreamWriter(filePath))
            {
                foreach (Item record in Records)
                {
                    writer.Write($"{record.Timestamp}\t");
                    writer.Write($"{record.Path}\t");
                    writer.WriteLine($"{(long)record.Cost}");
                }
            }
        }

        public static void Clear()
        {
            Records.Clear();
        }

        public static Watch AutoProfile(string name, string path, bool isDir = false)
        {
            var watch = new Watch
            {
                SampleName = name,
                IsDir = isDir,
                Path = path
            };
            watch.Start();
            return watch;
        }

        public struct Watch : IDisposable
        {
            public string SampleName;
            public bool IsDir;
            public string Path;

            public void Start()
            {
                if (KResManagerConfig.ResGlobalConfig.AssetLoadProfile)
                {
                    m_Stopwatch.Restart();
                }

                Profiler.BeginSample(SampleName);
            }

            public void StopAndRecord()
            {
                Profiler.EndSample();

                if (!KResManagerConfig.ResGlobalConfig.AssetLoadProfile)
                {
                    return;
                }

                m_Stopwatch.Stop();
                if (m_Stopwatch.ElapsedMilliseconds >
                    KResManagerConfig.ResGlobalConfig.AssetLoadTimeLogThresholdMs)
                {
                    Debug.Log(
                        $"[KResourceLoadingProfiler] {SampleName} {Path} {m_Stopwatch.ElapsedMilliseconds} ms");
                }

                Records.Add(new Item
                {
                    Timestamp = DateTime.Now,
                    Path = Path,
                    IsDir = IsDir,
                    Cost = m_Stopwatch.ElapsedMilliseconds
                });
            }

            public void Dispose()
            {
                StopAndRecord();
            }
        }
    }
}