#region

#endregion

namespace GamePlay.Strategy
{
    /*
        public static class NationManager
        {
            private static readonly Dictionary<int, CountryDef> m_CountryDefs =
                new Dictionary<int, CountryDef>(256);

            public static void Initialize(IEnumerable<CountryDef> allDefs)
            {
                m_CountryDefs.Clear();
                foreach (var def in allDefs)
                {
                    m_CountryDefs[def.Tag.Hash] = def;
                }
            }

            // 通过 Tag 极速获取国家静态信息
            public static CountryDef GetDef(GameplayTag tag)
            {
                if (m_CountryDefs.TryGetValue(tag.Hash, out var def))
                {
                    return def;
                }

                Debug.LogError($"[CountryRegistry] 未找到国家配置，Tag: {tag.Name}");
                return null;
            }
        }*/
}
