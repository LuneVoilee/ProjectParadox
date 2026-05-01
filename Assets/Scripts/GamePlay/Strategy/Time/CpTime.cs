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
    // 世界级时间能力：读取地图实体上的 Time 组件并推进游戏日期。
    public class CpTime : CapabilityBase
    {
        public override string Pipeline => CapabilityPipeline.MapBootstrap;

        private Func<DateTime?> m_ReadCurrentTime;
        private bool m_TimeInitialized;

        protected override void OnCreated()
        {
            EventBus.UI_OnSpeedChange += ChangeTimeSpeed;
            m_ReadCurrentTime ??= ReadCurrentTime;
            EventBus.UI_GetCurrentTime = m_ReadCurrentTime;
        }

        public override void Dispose()
        {
            EventBus.UI_OnSpeedChange -= ChangeTimeSpeed;
            if (EventBus.UI_GetCurrentTime == m_ReadCurrentTime)
            {
                EventBus.UI_GetCurrentTime = null;
            }

            base.Dispose();
        }

        private DateTime? ReadCurrentTime()
        {
            return TryGetTime(out Time time) ? time.CurrentDate : null;
        }

        private void ChangeTimeSpeed(TimeType timeType)
        {
            if (World is not GameWorld gameWorld)
            {
                return;
            }

            if (TryGetTime(out Time time))
            {
                time.NewTimeType = timeType;
            }

            gameWorld.ChangeGameSpeed(timeType);
            EventBus.GP_OnSpeedChange?.Invoke(timeType);
        }

        public override void Tick
            (CapabilityContext context, float deltaTime, float realElapsedSeconds)
        {
            if (!TryGetTime(out Time time))
            {
                return;
            }

            if (!m_TimeInitialized)
            {
                m_TimeInitialized = true;
                ChangeTimeSpeed(time.NewTimeType);
                EventBus.GP_OnTimeChange?.Invoke(time.CurrentDate);
            }

            double gameSecondsPerRealSecond = 3600.0;
            time.SubSecondAccumulator += deltaTime * gameSecondsPerRealSecond;

            int oldDay = time.CurrentDate.Day;
            if (time.SubSecondAccumulator >= 1.0)
            {
                int secondsToAdvance = (int)time.SubSecondAccumulator;
                time.CurrentDate = time.CurrentDate.AddSeconds(secondsToAdvance);
                time.SubSecondAccumulator -= secondsToAdvance;
            }

            if (time.CurrentDate.Day != oldDay)
            {
                time.DayVersion++;
                EventBus.GP_OnTimeChange?.Invoke(time.CurrentDate);
            }
        }

        private bool TryGetTime(out Time time)
        {
            time = null;
            if (World is not GameWorld gameWorld)
            {
                return false;
            }

            if (!gameWorld.TryGetPrimaryMapEntity(out CEntity mapEntity))
            {
                return false;
            }

            return mapEntity.TryGetTime(out time);
        }
    }
}
