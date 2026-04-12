#region

using GamePlay.Faction;
using Map.Common;
using Map.Manager;

#endregion

namespace GamePlay.Nation
{
    public static class NationBehaviour
    {
        public static void Occupy(byte id, HexCoordinates coord)
        {
            var data = NationManager.Instance.Nations[id];
            data.Hexs.Add(coord);
            MapManager.Instance.SetColor(coord, data.NationalColor);
        }
    }
}