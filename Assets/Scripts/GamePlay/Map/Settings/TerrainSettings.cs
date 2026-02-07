using System;
using UnityEngine;
using UnityEngine.Tilemaps;
using Map.Common;

namespace Map.Settings
{
    [CreateAssetMenu(menuName = "World/Terrain Palette", fileName = "TerrainPalette")]
    public class TerrainSettings : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public TerrainType TerrainType;
            public TileBase Tile;
        }

        [SerializeField] private Entry[] entries;

        [NonSerialized] private TileBase[] m_TilesByType;

        public TileBase GetTile(TerrainType type)
        {
            EnsureCache();

            int index = (int)type;
            if (index < 0 || index >= m_TilesByType.Length)
            {
                return null;
            }

            return m_TilesByType[index];
        }

        private void OnEnable()
        {
            BuildCache();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            BuildCache();
        }
#endif

        private void EnsureCache()
        {
            if (m_TilesByType == null || m_TilesByType.Length == 0)
            {
                BuildCache();
            }
        }

        private void BuildCache()
        {
            int count = Enum.GetValues(typeof(TerrainType)).Length;
            if (m_TilesByType == null || m_TilesByType.Length != count)
            {
                m_TilesByType = new TileBase[count];
            }

            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                int index = (int)entries[i].TerrainType;
                if (index < 0 || index >= m_TilesByType.Length)
                {
                    continue;
                }

                m_TilesByType[index] = entries[i].Tile;
            }
        }
    }
}
