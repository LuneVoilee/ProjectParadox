#region

using Core.Capability;
using GamePlay.Map;
using UnityEngine;

#endregion

namespace GamePlay.Strategy
{
    // 一帧点击事件实体。输入能力生成，选择/命令能力消费，帧末自动销毁。
    public class GameplayClickEvent : CComponent
    {
        public Vector2 ScreenPosition;
        public bool IsRightClick;
        public HexCoordinates Hex;
        public Vector3Int Cell;
        public bool HasHex;
    }
}
