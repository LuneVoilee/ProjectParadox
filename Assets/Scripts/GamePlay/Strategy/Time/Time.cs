#region

using System;
using Core.Capability;

#endregion

namespace GamePlay.Strategy
{
    public enum TimeType
    {
        Pause,
        Speed1,
        Speed2,
        Speed3,
        Speed4,
        Speed5
    }

    public class Time : CComponent
    {
        public DateTime StartDate;
        public TimeType NewTimeType;
        public DateTime CurrentDate;

        // 用于缓存不足 1 秒的微小时间流逝
        public double SubSecondAccumulator;
    }
}