using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tool.Json
{
    public class KAssetAsyncLoadHandle
    {
        public string AssetID;
        public Type AssetType;
        public Action<KAssetAsyncLoadHandle> CompleteCallBack;
        public List<Object> AssetObjSet = new List<Object>();

        private readonly List<AssetBundleRequest> m_AssetRequestHandle = new List<AssetBundleRequest>();
        private readonly List<AssetBundleRequest> m_LoadAllAssetRequestHandle = new List<AssetBundleRequest>();
        private readonly List<KABLoadAsyncHandle> m_LoadAllAbLoadHandleSet;
        private readonly Dictionary<string, KABLoadAsyncHandle> m_AbLoadHandleDic;

        private readonly List<string> m_RemoveABHandleKey = new List<string>();

        private bool m_UpdateDone;

        public KAssetAsyncLoadHandle(
            string assetID,
            Type assetType,
            Dictionary<string, KABLoadAsyncHandle> abHandleDic,
            Action<KAssetAsyncLoadHandle> completeCallBack)
        {
            AssetID = assetID;
            AssetType = assetType;
            m_AbLoadHandleDic = abHandleDic ?? new Dictionary<string, KABLoadAsyncHandle>();
            m_LoadAllAbLoadHandleSet = new List<KABLoadAsyncHandle>();
            CompleteCallBack = completeCallBack;
        }

        public KAssetAsyncLoadHandle(
            string assetID,
            Type assetType,
            List<KABLoadAsyncHandle> loadAllAbHandleSet,
            Action<KAssetAsyncLoadHandle> completeCallBack)
        {
            AssetID = assetID;
            AssetType = assetType;
            m_AbLoadHandleDic = new Dictionary<string, KABLoadAsyncHandle>();
            m_LoadAllAbLoadHandleSet = loadAllAbHandleSet ?? new List<KABLoadAsyncHandle>();
            CompleteCallBack = completeCallBack;
        }

        public KAssetAsyncLoadHandle(
            string assetID,
            Type assetType,
            KABLoadAsyncHandle abLoadAsyncHandle,
            Action<KAssetAsyncLoadHandle> completeCallBack)
        {
            AssetID = assetID;
            AssetType = assetType;
            m_AbLoadHandleDic = new Dictionary<string, KABLoadAsyncHandle>();
            if (abLoadAsyncHandle != null)
            {
                m_AbLoadHandleDic.Add(assetID, abLoadAsyncHandle);
            }

            m_LoadAllAbLoadHandleSet = new List<KABLoadAsyncHandle>();
            CompleteCallBack = completeCallBack;
        }

        public KAssetAsyncLoadHandle(
            string assetID,
            Type assetType,
            Object assetObj,
            Action<KAssetAsyncLoadHandle> completeCallBack)
        {
            AssetID = assetID;
            AssetType = assetType;
            if (assetObj != null)
            {
                AssetObjSet.Add(assetObj);
            }

            m_AbLoadHandleDic = new Dictionary<string, KABLoadAsyncHandle>();
            m_LoadAllAbLoadHandleSet = new List<KABLoadAsyncHandle>();
            CompleteCallBack = completeCallBack;
        }

        public KAssetAsyncLoadHandle(
            string assetID,
            Type assetType,
            List<Object> assetObjSet,
            Action<KAssetAsyncLoadHandle> completeCallBack)
        {
            AssetID = assetID;
            AssetType = assetType;
            AssetObjSet = assetObjSet ?? new List<Object>();
            m_AbLoadHandleDic = new Dictionary<string, KABLoadAsyncHandle>();
            m_LoadAllAbLoadHandleSet = new List<KABLoadAsyncHandle>();
            CompleteCallBack = completeCallBack;
        }

        private bool IsInternalDone =>
            m_AbLoadHandleDic.Count == 0 &&
            m_LoadAllAbLoadHandleSet.Count == 0 &&
            m_LoadAllAssetRequestHandle.Count == 0 &&
            m_AssetRequestHandle.Count == 0;

        public bool IsDone
        {
            get
            {
                Update();
                return IsInternalDone && m_UpdateDone;
            }
        }

        public void Update()
        {
            if (m_UpdateDone)
            {
                return;
            }

            foreach (KeyValuePair<string, KABLoadAsyncHandle> pair in m_AbLoadHandleDic)
            {
                if (pair.Value == null)
                {
                    m_RemoveABHandleKey.Add(pair.Key);
                    continue;
                }

                if (!pair.Value.IsDone)
                {
                    continue;
                }

                if (pair.Value.AssetBundle != null)
                {
                    AssetBundleRequest request = pair.Value.AssetBundle.LoadAssetAsync(pair.Key, AssetType);
                    if (!m_AssetRequestHandle.Contains(request))
                    {
                        m_AssetRequestHandle.Add(request);
                    }
                }

                m_RemoveABHandleKey.Add(pair.Key);
            }

            foreach (string key in m_RemoveABHandleKey)
            {
                m_AbLoadHandleDic.Remove(key);
            }

            m_RemoveABHandleKey.Clear();

            for (int i = m_LoadAllAbLoadHandleSet.Count - 1; i >= 0; i--)
            {
                KABLoadAsyncHandle abHandle = m_LoadAllAbLoadHandleSet[i];
                if (abHandle == null)
                {
                    m_LoadAllAbLoadHandleSet.RemoveAt(i);
                    continue;
                }

                if (!abHandle.IsDone)
                {
                    continue;
                }

                if (abHandle.AssetBundle != null)
                {
                    AssetBundleRequest request = abHandle.AssetBundle.LoadAllAssetsAsync(AssetType);
                    if (!m_LoadAllAssetRequestHandle.Contains(request))
                    {
                        m_LoadAllAssetRequestHandle.Add(request);
                    }
                }

                m_LoadAllAbLoadHandleSet.RemoveAt(i);
            }

            for (int i = m_AssetRequestHandle.Count - 1; i >= 0; i--)
            {
                AssetBundleRequest request = m_AssetRequestHandle[i];
                if (!request.isDone)
                {
                    continue;
                }

                if (request.asset != null && !AssetObjSet.Contains(request.asset))
                {
                    AssetObjSet.Add(request.asset);
                }

                m_AssetRequestHandle.RemoveAt(i);
            }

            for (int i = m_LoadAllAssetRequestHandle.Count - 1; i >= 0; i--)
            {
                AssetBundleRequest request = m_LoadAllAssetRequestHandle[i];
                if (!request.isDone)
                {
                    continue;
                }

                if (request.allAssets != null)
                {
                    foreach (Object assetObj in request.allAssets)
                    {
                        if (assetObj != null && !AssetObjSet.Contains(assetObj))
                        {
                            AssetObjSet.Add(assetObj);
                        }
                    }
                }

                m_LoadAllAssetRequestHandle.RemoveAt(i);
            }

            if (!IsInternalDone)
            {
                return;
            }

            CompleteCallBack?.Invoke(this);
            m_UpdateDone = true;
        }

        public override bool Equals(object obj)
        {
            return obj is KAssetAsyncLoadHandle handle &&
                   !string.IsNullOrEmpty(handle.AssetID) &&
                   handle.AssetID.Equals(AssetID, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            return AssetID != null ? AssetID.GetHashCode() : 0;
        }
    }

    public static class KAssetAsyncLoadHandleRunner
    {
        public static IEnumerator CoUpdateUntilDone(KAssetAsyncLoadHandle handle)
        {
            while (handle != null && !handle.IsDone)
            {
                handle.Update();
                yield return null;
            }
        }
    }
}
