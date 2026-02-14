using System;
using UnityEngine;

namespace Map.Settings
{
    [CreateAssetMenu(menuName = "World/Territory Settings", fileName = "TerritorySettings")]
    public class TerritorySettings : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public byte Id;
            public Color Color;
        }

        [SerializeField] private Entry[] m_Entries;
        [SerializeField] private Color m_DefaultColor = new Color(0f, 0f, 0f, 0f);

        private const int m_PaletteSize = 256;
        private const string m_TextureNameSuffix = "_Palette";

        [NonSerialized] private Texture2D m_Texture;
        [NonSerialized] private Color32[] m_Colors;

        public Texture2D GetTexture()
        {
            EnsureCache();
            return m_Texture;
        }

        public Color32 GetColor(byte id)
        {
            EnsureCache();
            return m_Colors[id];
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
            if (m_Texture == null || m_Colors == null || m_Colors.Length != m_PaletteSize)
            {
                BuildCache();
            }
        }

        private void BuildCache()
        {
            if (m_Colors == null || m_Colors.Length != m_PaletteSize)
            {
                m_Colors = new Color32[m_PaletteSize];
            }

            var defaultColor = (Color32)m_DefaultColor;
            for (int i = 0; i < m_Colors.Length; i++)
            {
                m_Colors[i] = defaultColor;
            }

            if (m_Entries != null)
            {
                for (int i = 0; i < m_Entries.Length; i++)
                {
                    int index = m_Entries[i].Id;
                    if (index >= m_Colors.Length)
                    {
                        continue;
                    }

                    m_Colors[index] = m_Entries[i].Color;
                }
            }

            if (m_Texture == null || m_Texture.width != m_PaletteSize || m_Texture.height != 1)
            {
                ReleaseTexture();
                m_Texture = new Texture2D(m_PaletteSize, 1, TextureFormat.RGBA32, false, false)
                {
                    name = name + m_TextureNameSuffix,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave
                };
            }

            m_Texture.SetPixels32(m_Colors);
            m_Texture.Apply(false, false);
        }

        private void ReleaseTexture()
        {
            if (m_Texture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(m_Texture);
            }
            else
            {
                DestroyImmediate(m_Texture);
            }

            m_Texture = null;
        }
    }
}
