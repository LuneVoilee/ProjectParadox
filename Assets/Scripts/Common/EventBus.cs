#region

using System;
using System.Collections.Generic;
using Common.Contracts;
using UnityEngine;

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

        public static Func<int> GP_OnCreateSelectionIndicator;
        public static Action<int> GP_OnDestroySelectionIndicator;

        public static Func<IReadOnlyList<Vector3>, int> GP_OnCreatePathIndicator;
        public static Action<int> GP_OnDestroyPathIndicator;

        public static Func<DateTime?> UI_GetCurrentTime;
    }
}