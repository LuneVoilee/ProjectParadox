#region

using System;
using Common.Contracts;

#endregion

namespace Common.Event
{
    public static class EventBus
    {
        //命名规则：
        //事件的发起方作为前缀 : GamePlay层前缀GP；UI层前缀UI
        public static Action<DateTime> GP_OnTimeChange;
        public static Action<TimeType> UI_OnSpeedChange;
        public static Action<TimeType> GP_OnSpeedChange;
        public static Action GP_OnCreateSelectionIndictor;

        public static Func<DateTime?> UI_GetCurrentTime;
    }
}