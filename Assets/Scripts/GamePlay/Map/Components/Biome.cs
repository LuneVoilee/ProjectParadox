#region

using Core.Capability;
using UnityEngine;

#endregion

namespace GamePlay.Map
{
    public class Biome : CComponent
    {
        [Range(0f, 1f)] public float SeaLevel = 0.3f;
        [Range(0f, 1f)] public float MountainLevel = 0.8f;
    }
}