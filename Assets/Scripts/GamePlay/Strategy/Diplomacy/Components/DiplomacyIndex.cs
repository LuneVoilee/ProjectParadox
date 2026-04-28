#region

using Core.Capability;

#endregion

namespace GamePlay.Strategy
{
    // 外交关系索引组件，挂载在地图实体上。
    // 使用 256×256 矩阵存储所有国家间的 DiplomacyStatus，默认值为 Peace。
    // 关系是对称的：SetRelation 同时写入 [a,b] 和 [b,a]。
    public class DiplomacyIndex : CComponent
    {
        public const int Capacity = 256;

        private readonly DiplomacyStatus[] m_Relations = new DiplomacyStatus[Capacity * Capacity];

        // 查询两国间外交状态。
        public DiplomacyStatus GetRelation(byte nationA, byte nationB)
        {
            return m_Relations[nationA * Capacity + nationB];
        }

        // 设置两国间外交状态，同时写入对称位置。
        public void SetRelation(byte nationA, byte nationB, DiplomacyStatus status)
        {
            m_Relations[nationA * Capacity + nationB] = status;
            m_Relations[nationB * Capacity + nationA] = status;
        }

        // 判断 A 对 B 是否为敌对关系。
        public bool IsHostile(byte nationA, byte nationB)
        {
            return GetRelation(nationA, nationB) == DiplomacyStatus.War;
        }

        public bool IsPeace(byte nationA, byte nationB)
        {
            return GetRelation(nationA, nationB) == DiplomacyStatus.Peace;
        }

        // 判断 A 与 B 是否为盟友关系。
        public bool IsAllied(byte nationA, byte nationB)
        {
            return GetRelation(nationA, nationB) == DiplomacyStatus.Alliance;
        }
    }
}