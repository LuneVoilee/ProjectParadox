#region

using Core.Capability;
using NewGamePlay;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Tilemaps;

#endregion

namespace GamePlay.Map
{
    public class MapInstaller : MonoBehaviour
    {
        private GameWorld m_World;
        private CEntity m_MapEntity;

        [BoxGroup("Biome参数")] public float SeaLevel = 0.3f;
        [BoxGroup("Biome参数")] public float MountainLevel = 0.8f;

        [BoxGroup("Map参数")] public int Width = 100;
        [BoxGroup("Map参数")] public int Height = 100;
        [BoxGroup("Map参数")] public bool EnableSeamlessX = true;
        [BoxGroup("Map参数")] public bool EnableSeamlessY;

        [BoxGroup("Noise参数")] public int Seed;
        [BoxGroup("Noise参数")] public int MinOffset = -100000;
        [BoxGroup("Noise参数")] public int MaxOffset = 100000;
        [BoxGroup("Noise参数")] public float HeightScale = 0.08f;
        [BoxGroup("Noise参数")] public float MinNoiseScale = 0.0001f;

        [BoxGroup("DrawMap参数")] [ShowInInspector]
        public Tilemap ParamTileMap;

        [BoxGroup("DrawMap参数")] [ShowInInspector]
        public TerrainSettings ParamTerrainSettings;

        private void Start()
        {
            if (m_MapEntity != null)
            {
                return;
            }

            m_World = GameManager.Instance.World;
            m_MapEntity = m_World.AddChild("MapEntity");

            m_World.BindCapability<GenerateMapDataCap>(m_MapEntity);
            m_World.BindCapability<DrawMapCap>(m_MapEntity);

            var biomeComp = m_MapEntity.AddComponent<Biome>();
            biomeComp.MountainLevel = MountainLevel;
            biomeComp.SeaLevel = SeaLevel;

            var mapComp = m_MapEntity.AddComponent<Map>();
            mapComp.Width = Width;
            mapComp.Height = Height;
            mapComp.EnableSeamlessX = EnableSeamlessX;
            mapComp.EnableSeamlessY = EnableSeamlessY;

            var Noise = m_MapEntity.AddComponent<Noise>();
            Noise.Seed = Seed;
            Noise.MinOffset = MinOffset;
            Noise.MaxOffset = MaxOffset;
            Noise.HeightScale = HeightScale;
            Noise.MinNoiseScale = MinNoiseScale;

            var drawMap = m_MapEntity.AddComponent<DrawMap>();
            drawMap.Tilemap = ParamTileMap;
            drawMap.TerrainSettings = ParamTerrainSettings;
        }
    }
}