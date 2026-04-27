#region

using Core.Capability;
using UnityEngine;

#endregion

namespace GamePlay.Strategy
{
    // 单位位移执行所需的场景引用和到达阈值；动画与表现状态暂不放在这里。
    public class UnitMotor : CComponent
    {
        public Transform Transform;
        public float ArriveDistance = 0.03f;

        public override void Dispose()
        {
            Transform = null;
            base.Dispose();
        }
    }
}
