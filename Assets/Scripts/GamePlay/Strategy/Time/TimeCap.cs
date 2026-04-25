#region

using Core.Capability;
using NewGamePlay;

#endregion

namespace GamePlay.Strategy
{
    public class TimeCap : CapabilityBase
    {
        private static readonly int m_TimeId = Component<Time>.TId;

        protected override void OnInit()
        {
            Filter(m_TimeId);
        }

        public override bool ShouldActivate()
        {
            return true;
        }

        public override bool ShouldDeactivate()
        {
            return true;
        }

        protected override void OnActivated()
        {
            if (!Owner.TryGetTime(out var time))
            {
                return;
            }

            if (World is not GameWorld gameWorld)
            {
                return;
            }

            gameWorld.ChangeGameSpeed(time.NewTimeType);
        }

        public override void TickActive(float deltaTime, float realElapsedSeconds)
        {
            if (!Owner.TryGetTime(out var time))
            {
                return;
            }

            // 定义基础时间流速比：现实 1 秒 = 游戏内 0.5 小时 (1小时 = 3600秒)
            double gameSecondsPerRealSecond = 1800.0;

            time.SubSecondAccumulator += deltaTime * gameSecondsPerRealSecond;

            var oldDay = time.CurrentDate.Day;

            // 当累积时间大于 1 秒时才写入 DateTime，避免极小值被 DateTime 内部结构截断
            if (time.SubSecondAccumulator >= 1.0)
            {
                // 向下取整获取完整的秒数
                int secondsToAdvance = (int)time.SubSecondAccumulator;

                time.CurrentDate = time.CurrentDate.AddSeconds(secondsToAdvance);

                // 扣除已使用的完整秒数，保留小数部分
                time.SubSecondAccumulator -= secondsToAdvance;
            }


            /* 跨天逻辑触发示例
            if (time.CurrentDate.Day != oldDay)
            {
                // 可以在这里触发跨天结算组件/事件，例如：
                // Owner.AddComponent<DailySettleEventComponent>();
            }
            */
        }
    }
}