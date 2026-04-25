#region

using System;
using Common.Contracts;
using Core.Capability;

#endregion

namespace GamePlay.Strategy
{
    public class Time : CComponent
    {
        public TimeType NewTimeType;
        public DateTime CurrentDate;

        // 用于缓存不足 1 秒的微小时间流逝
        public double SubSecondAccumulator;
    }
}
