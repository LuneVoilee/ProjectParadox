using Map.Manager;
using UnityEngine;

namespace GamePlay.CapabilityFramework.Samples.Map
{
    /// <summary>
    /// 可选桥接器：把现有 MapManager 的“生成命令”转给 MapEntityDriver。
    ///
    /// 用途：
    /// - 迁移期间不一次性删旧系统；
    /// - 先让旧入口继续工作，再逐步替换。
    ///
    /// 用法：
    /// - 把本脚本和 MapEntityDriver 挂在同一对象（或手动关联）；
    /// - 调用 BridgeGenerate() 即可触发 Entity 请求流。
    /// </summary>
    public class MapEntityDriverBridge : MonoBehaviour
    {
        [SerializeField] private MapEntityDriver m_MapEntityDriver;

        private void Awake()
        {
            if (m_MapEntityDriver == null)
            {
                m_MapEntityDriver = GetComponent<MapEntityDriver>();
            }
        }

        public void BridgeGenerate()
        {
            if (m_MapEntityDriver == null)
            {
                return;
            }

            var mapManager = MapManager.Instance;
            if (mapManager != null)
            {
                // 这里不读取旧 Manager 参数，仅触发一次生成；
                // Seed 由 MapEntityDriver 当前配置决定。
                m_MapEntityDriver.RequestGenerateMap(0);
            }
            else
            {
                m_MapEntityDriver.RequestGenerateMap(0);
            }
        }
    }
}
