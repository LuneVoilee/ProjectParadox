#region

using Core.Capability;
using Map.Data;
using Map.Settings;
using UnityEngine.Tilemaps;

#endregion

namespace GamePlay.Map
{
    public class DrawMap : CComponent
    {
        public Tilemap Tilemap;
        public TerrainSettings TerrainSettings;
        
        public bool AutoGhostColumns = true;
        
        public int m_GhostColumns = 6;
        public const int GhostPadding = 1;
        public const int DefaultGhostColumns = 6;
        
        public int GhostColumns => m_LastGhostColumns;
        private int m_LastGhostColumns;
        
    }
}