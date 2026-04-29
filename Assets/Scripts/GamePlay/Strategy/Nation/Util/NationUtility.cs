#region

using UnityEngine;

#endregion

namespace GamePlay.Strategy
{
    // 国家相关的纯函数辅助方法。Capability 之间禁止 static 调用，这些工具方法从 CpNationRegistry
    // 中抽离出来，供 CpOccupy、CpCombatResolve、CpDrawMap、CpMoveAlongHexPath 等能力使用。
    public static class NationUtility
    {
        // Tag 是 JSON 与人类可读配置的稳定 key，进入运行时索引前统一大写去空白。
        public static string NormalizeTag(string tag)
        {
            return string.IsNullOrWhiteSpace(tag)
                ? string.Empty
                : tag.Trim().ToUpperInvariant();
        }

        // 用于热路径保护：未知 id 不直接参与占领/绘制，而是降级成 Neutral。
        public static bool IsValidNationId(NationIndex nationIndex, byte nationId)
        {
            return nationId == NationIndex.NeutralId ||
                   nationIndex?.TagById != null &&
                   nationId < nationIndex.TagById.Length &&
                   !string.IsNullOrEmpty(nationIndex.TagById[nationId]);
        }

        // 地图绘制只关心颜色；任何缺失、越界或未注册 id 都统一返回 NeutralColor。
        public static Color32 GetColorOrNeutral(NationIndex nationIndex, byte nationId)
        {
            if (nationIndex?.TagById == null ||
                nationIndex.ColorById == null ||
                nationId >= nationIndex.TagById.Length ||
                nationId >= nationIndex.ColorById.Length ||
                string.IsNullOrEmpty(nationIndex.TagById[nationId]))
            {
                return NationIndex.NeutralColor;
            }

            return nationIndex.ColorById[nationId];
        }

        // 保留通过 Tag 查询运行时 byte id 的入口，后续 UI/指令/配置引用可以走这个路径。
        public static bool TryGetIdByTag(NationIndex nationIndex, NationTag tag, out byte id)
        {
            id = NationIndex.NeutralId;
            return !tag.IsNone &&
                   nationIndex?.IdByTag != null &&
                   nationIndex.IdByTag.TryGetValue(tag, out id);
        }

        // 一站式 Tag→byte id 解析：空/无效/未注册 Tag 统一返回 NeutralId，调用方无需重复判空。
        public static byte GetIdOrDefault(NationIndex nationIndex, NationTag tag)
        {
            if (tag.IsNone) return NationIndex.NeutralId;
            if (nationIndex?.IdByTag != null &&
                nationIndex.IdByTag.TryGetValue(tag, out byte id))
            {
                return id;
            }
            return NationIndex.NeutralId;
        }
    }
}
