#region

using System;
using Common.Contracts;
using Common.Event;
using Core.Capability;
using GamePlay.Util;
using GamePlay.World;

#endregion

namespace GamePlay.Strategy
{
    public class TimeCap : CapabilityBase
    {
        private static readonly int m_TimeId = Component<Time>.TId;
        private Func<DateTime?> m_ReadCurrentTime;

        protected override void OnInit()
        {
            Filter(m_TimeId);
        }

        public override bool ShouldActivate()
        {
            return Owner.HasComponent(m_TimeId);
        }

        public override bool ShouldDeactivate()
        {
            return !ShouldActivate();
        }

        protected override void OnActivated()
        {
            if (!Owner.TryGetTime(out var time))
            {
                return;
            }

            EventBus.UI_OnSpeedChange += ChangeTimeSpeed;
            m_ReadCurrentTime ??= ReadCurrentTime;
            EventBus.UI_GetCurrentTime = m_ReadCurrentTime;
            ChangeTimeSpeed(time.NewTimeType);

            //Day1
            EventBus.GP_OnTimeChange?.Invoke(time.CurrentDate);
        }

        protected override void OnDeactivated()
        {
            EventBus.UI_OnSpeedChange -= ChangeTimeSpeed;
            if (EventBus.UI_GetCurrentTime == m_ReadCurrentTime)
            {
                EventBus.UI_GetCurrentTime = null;
            }
        }

        private DateTime? ReadCurrentTime()
        {
            return Owner.TryGetTime(out var time) ? time.CurrentDate : null;
        }

        private void ChangeTimeSpeed(TimeType timeType)
        {
            if (World is not GameWorld gameWorld)
            {
                return;
            }

            if (Owner.TryGetTime(out var time))
            {
                time.NewTimeType = timeType;
            }

            gameWorld.ChangeGameSpeed(timeType);
            EventBus.GP_OnSpeedChange?.Invoke(timeType);
        }

        public override void TickActive(float deltaTime, float realElapsedSeconds)
        {
            if (!Owner.TryGetTime(out var time))
            {
                return;
            }

            // 定义基础时间流速比：现实 1 秒 = 游戏内 0.5 小时 (1小时 = 3600秒)
            double gameSecondsPerRealSecond = 3600.0;

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


            //NOTICE: 跨天逻辑触发
            if (time.CurrentDate.Day != oldDay)
            {
                EventBus.GP_OnTimeChange?.Invoke(time.CurrentDate);
            }
        }
    }
}