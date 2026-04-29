#region

using System;
using Core.Capability;
using GamePlay.Map;
using UnityEngine;

#endregion

namespace GamePlay.Strategy
{
    // 单位当前移动命令。路径包含起点和终点，NextPathIndex 指向下一个需要抵达的格。
    public class UnitMoveTarget : CComponent
    {
        public HexCoordinates DestinationHex;
        public HexCoordinates[] Path;
        public int NextPathIndex = 1;
        public int RequestVersion;
        public int PathIndicatorId = -1;
        public float StepTimer;
        public Vector3 VisualLerpStart;
        public Vector3 VisualLerpTarget;
        public float VisualLerpProgress = 1f;

        public override void Dispose()
        {
            Path = Array.Empty<HexCoordinates>();
            PathIndicatorId = -1;
            VisualLerpStart = Vector3.zero;
            VisualLerpTarget = Vector3.zero;
            VisualLerpProgress = 1f;
            StepTimer = 0f;
            base.Dispose();
        }
    }
}
