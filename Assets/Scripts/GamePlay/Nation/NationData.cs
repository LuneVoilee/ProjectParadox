#region

using System.Collections.Generic;
using Map.Common;
using UnityEngine;

#endregion

namespace Faction.Data
{
    [CreateAssetMenu(menuName = "World/NationData", fileName = "NationData")]
    public class NationData : ScriptableObject
    {
        public byte Id;

        public string Name;

        public Color NationalColor;

        public List<HexCoordinates> Hexs = new();
    }
}