#region

using System;
using Common.Contracts;

#endregion

namespace Common.Event
{
    public static class EventBus
    {
        public static Action<DateTime> OnTimeChangeAction;
        public static Action<TimeType> OnSpeedChangeRequest;
        public static Action<TimeType> OnSpeedChanged;
        public static Func<DateTime?> GetCurrentTime;
    }
}
