#region

using System.Collections.Generic;
using Core.Capability;
using UnityEngine;

#endregion

namespace GamePlay.Strategy
{
    // 国家运行时索引表。该组件只保存状态，不负责分配/注册逻辑，所有写入都由 CpNationRegistry 完成。
    public class NationIndex : CComponent
    {
        // byte 可表示 0-255；0 固定为 Neutral，因此最多支持 255 个真实国家。
        public const int Capacity = 256;
        public const byte NeutralId = 0;
        public const string NeutralTag = "NEUTRAL";
        public static readonly Color32 NeutralColor = new Color32(255, 255, 255, 255);

        // id -> Tag，用于校验 id 是否已注册，也方便调试时从格子 owner id 反查国家。
        public string[] TagById = new string[Capacity];

        // id -> 国家实体 id，后续经济/外交/AI 等系统可以从索引跳到具体国家实体。
        public int[] NationEntityIdById = new int[Capacity];

        // id -> 国家颜色，地图绘制只读这张表，不需要触碰 JSON 或国家实体。
        public Color32[] ColorById = new Color32[Capacity];

        // Tag -> id，用于命令、UI、配置引用等以 Tag 为输入的路径快速转成运行时 byte id。
        public Dictionary<NationTag, byte> IdByTag = new Dictionary<NationTag, byte>();

        // 标记索引是否已经由 CpNationRegistry 完成初始化，便于其它逻辑做保护性判断。
        public bool IsInitialized;

        public override void Dispose()
        {
            // 释放引用型容器，避免地图实体销毁后旧索引被误用或保留大块数组。
            IdByTag?.Clear();
            TagById = null;
            NationEntityIdById = null;
            ColorById = null;
            IsInitialized = false;
            base.Dispose();
        }
    }
}
