using System.Collections.Generic;
using UnityEngine;

namespace Tool.Json
{
    public class KABLoadAsyncHandle
    {
        public string AbName;
        public AssetBundle AssetBundle;
        public AssetBundleCreateRequest ABCreateRequest;

        private readonly List<KABLoadAsyncHandle> m_DependAbLoadHandleSet;
        private readonly List<KABLoadAsyncHandle> m_RemoveList = new List<KABLoadAsyncHandle>();

        public KABLoadAsyncHandle(string abName, AssetBundleCreateRequest abCreateRequest, List<KABLoadAsyncHandle> depend)
        {
            AbName = abName;
            ABCreateRequest = abCreateRequest;
            m_DependAbLoadHandleSet = depend;
        }

        private bool IsLocalDone =>
            (AssetBundle != null || (ABCreateRequest != null && ABCreateRequest.isDone)) &&
            (m_DependAbLoadHandleSet == null || m_DependAbLoadHandleSet.Count == 0);

        public bool IsDone
        {
            get
            {
                Update();
                return IsLocalDone;
            }
        }

        public void Update()
        {
            if (ABCreateRequest != null && ABCreateRequest.isDone)
            {
                AssetBundle = ABCreateRequest.assetBundle;
                KAssetBundleManager.AddAbRecord(AbName, AssetBundle);
            }

            if (m_DependAbLoadHandleSet == null || m_DependAbLoadHandleSet.Count == 0)
            {
                return;
            }

            foreach (KABLoadAsyncHandle abLoadHandle in m_DependAbLoadHandleSet)
            {
                if (!abLoadHandle.IsDone)
                {
                    continue;
                }

                KAssetBundleManager.AddAbRecord(abLoadHandle.AbName, abLoadHandle.AssetBundle);
                m_RemoveList.Add(abLoadHandle);
            }

            foreach (KABLoadAsyncHandle handle in m_RemoveList)
            {
                m_DependAbLoadHandleSet.Remove(handle);
            }

            m_RemoveList.Clear();
        }

        public override bool Equals(object obj)
        {
            return obj is KABLoadAsyncHandle handle && handle.AbName == AbName;
        }

        public override int GetHashCode()
        {
            return AbName != null ? AbName.GetHashCode() : 0;
        }
    }
}
