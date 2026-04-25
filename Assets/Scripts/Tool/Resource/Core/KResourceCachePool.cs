#region

using System.Collections.Generic;
using UnityEngine;

#endregion

namespace Tool.Resource
{
    public class KResourceCachePool
    {
        private class AssetInfo
        {
            public int DeadTime;
            public int LifeTime;
            public Object Asset;
        }

        private readonly Dictionary<string, AssetInfo> m_AssetInfo =
            new Dictionary<string, AssetInfo>();

        private readonly List<string> m_RemoveList =
            new List<string>();

        public void AddAsset(string name, Object asset, int lifeTime = -1)
        {
            if (KResManagerConfig.ResGlobalConfig.Verbose)
            {
                Debug.Log($"[ResourceCachePool] AddAsset {name}, lifeTime = {lifeTime}");
            }

            m_AssetInfo.Remove(name);

            var info = new AssetInfo
            {
                Asset = asset,
                LifeTime = lifeTime,
                DeadTime = lifeTime > 0 ? (int)Time.unscaledTime + lifeTime : -1
            };
            m_AssetInfo.Add(name, info);
        }

        public void RemoveAsset(string name)
        {
            if (KResManagerConfig.ResGlobalConfig.Verbose)
            {
                Debug.Log($"[ResourceCachePool] RemoveAsset by name : {name}");
            }

            m_AssetInfo.Remove(name);
        }

        public void RemoveAsset(Object asset)
        {
            foreach (var pair in m_AssetInfo)
            {
                if (!ReferenceEquals(pair.Value.Asset, asset))
                {
                    continue;
                }

                if (KResManagerConfig.ResGlobalConfig.Verbose)
                {
                    Debug.Log($"[ResourceCachePool] RemoveAsset by object : {pair.Key}");
                }

                m_AssetInfo.Remove(pair.Key);
                break;
            }
        }

        public Object GetAsset(string name, bool preload = false)
        {
            if (!m_AssetInfo.TryGetValue(name, out AssetInfo asset))
            {
                return null;
            }

            if (preload)
            {
                asset.LifeTime = -1;
            }

            asset.DeadTime = asset.LifeTime > 0 ? (int)Time.unscaledTime + asset.LifeTime : -1;
            return asset.Asset;
        }

        public void Clear()
        {
            Debug.Log("[ResourceCachePool] Clear.");
            m_AssetInfo.Clear();
        }

        public void Update()
        {
            m_RemoveList.Clear();

            foreach (var pair in m_AssetInfo)
            {
                if (pair.Value.DeadTime > 0 && pair.Value.DeadTime < Time.unscaledTime)
                {
                    m_RemoveList.Add(pair.Key);
                }
            }

            foreach (string name in m_RemoveList)
            {
                m_AssetInfo.Remove(name);
            }

            m_RemoveList.Clear();
        }
    }
}