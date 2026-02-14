using System;
using Map.Components;
using Map.Settings;
using Map.Systems;
using Map.View;
using Tool;
using UnityEngine;

namespace Map.Manager
{
    public class MapManager : SingletonMono<MapManager>
    {
        [Header("Map Size")]
        [SerializeField] private int m_Width = 100;
        [SerializeField] private int m_Height = 100;

        [Header("Noise")]
        [SerializeField] private int m_Seed;
        [SerializeField] private float m_HeightScale = 0.08f;
        [SerializeField] private float m_MoistureScale = 0.12f;

        [Header("Wrap")]
        [SerializeField] private bool m_SeamlessX = true;
        [SerializeField] private bool m_SeamlessY;

        [Header("Biome")]
        [SerializeField] private BiomeSettings m_BiomeSettings;

        [Header("Links")]
        [SerializeField] private HexMapRenderer m_HexMapRenderer;
        [SerializeField] private TerritoryBorderRenderer m_TerritoryBorderRenderer;

        [Header("Debug")]
        [SerializeField] private bool m_GenerateOnStart = true;

        public GridData CurrentData { get; private set; }
        public TerritoryBorderRenderer TerritoryBorderRenderer => m_TerritoryBorderRenderer;
        public event Action<GridData> MapGenerated;

        private void Start()
        {
            if (m_GenerateOnStart)
            {
                GenerateAndRender();
            }
        }

        public void GenerateAndRender()
        {
            var settings = new MapGenerator.Settings
            {
                Width = m_Width,
                Height = m_Height,
                Seed = m_Seed,
                HeightScale = m_HeightScale,
                MoistureScale = m_MoistureScale,
                SeamlessX = m_SeamlessX,
                SeamlessY = m_SeamlessY,
                BiomeSettings = m_BiomeSettings
            };

            var data = MapGenerator.Generate(settings);
            CurrentData = data;

            if (m_HexMapRenderer != null)
            {
                m_HexMapRenderer.Render(data);
            }

            if (m_TerritoryBorderRenderer != null)
            {
                m_TerritoryBorderRenderer.Render(data);
            }

            MapGenerated?.Invoke(data);
        }
    }
}
