#region

using Map.Common;
using Map.Manager;

#endregion

namespace GamePlay.Faction
{
    public static class NationUtil
    {
        public static void Occupy(this byte id, HexCoordinates coord)
        {
            var color = NationManager.Instance.Nations[id].NationalColor;
            MapManager.Instance.SetColor(coord, color);
        }
    }
}