using UnityEngine;

namespace Map.Settings
{
    [CreateAssetMenu(menuName = "World/Biome Settings", fileName = "BiomeSettings")]
    public class BiomeSettings : ScriptableObject
    {
        [Range(0f, 1f)] public float SeaLevel = 0.3f;
        [Range(0f, 1f)] public float MountainLevel = 0.8f;
        [Range(0f, 1f)] public float DesertMoisture = 0.4f;
    }
}
