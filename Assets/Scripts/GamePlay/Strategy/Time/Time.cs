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

        // 每次跨天递增，世界级能力用它判断是否需要执行每日逻辑。
        public int DayVersion;
    }
}
