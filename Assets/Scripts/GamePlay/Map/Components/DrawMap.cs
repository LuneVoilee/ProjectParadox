#region

using Core.Capability;
using UnityEngine.Tilemaps;

#endregion

namespace GamePlay.Map
{
    public class DrawMap : CComponent
    {
        public Tilemap Tilemap;
        public TerrainSettings TerrainSettings;

        public bool AutoGhostColumns = true;

        // Manual lower-bound for ghost columns. Auto mode can increase this at runtime.
        public int GhostColumnsFloor = 6;

        public const int GhostPadding = 1;
        public const int DefaultGhostColumns = 6;

        public int LastGhostColumns { get; internal set; }
        public bool IsDirty = true;
        public int GhostColumns => LastGhostColumns;
    }
}