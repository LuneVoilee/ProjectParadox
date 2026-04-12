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

            var gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                return;
            }

            m_World = gameManager.World;
            if (m_World == null)
            {
                return;
            }

            var config = new MapEntityPreset.Config
            {
                SeaLevel = SeaLevel,
                MountainLevel = MountainLevel,
                Width = Width,
                Height = Height,
                EnableSeamlessX = EnableSeamlessX,
                EnableSeamlessY = EnableSeamlessY,
                Seed = Seed,
                MinOffset = MinOffset,
                MaxOffset = MaxOffset,
                HeightScale = HeightScale,
                MinNoiseScale = MinNoiseScale,
                Tilemap = ParamTileMap,
                TerrainSettings = ParamTerrainSettings
            };

            m_MapEntity = MapEntityPreset.Create(m_World, config);
        }

        private void OnDestroy()
        {
            if (m_World != null && m_World.Children != null && m_MapEntity != null)
            {
                m_World.RemoveChild(m_MapEntity);
            }

            m_MapEntity = null;
            m_World = null;
        }
    }
}