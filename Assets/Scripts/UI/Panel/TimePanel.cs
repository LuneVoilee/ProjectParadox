#region

using System;
using Core.Reactive;
using GamePlay.Strategy;

#endregion

namespace UI.Panel
{
    public class UITimeData : BasePanelData
    {
        public ReactiveValue<TimeType> TimeSpeed;
        public ReactiveValue<DateTime> CurrentDate;
    }

    public partial class TimePanel : BasePanel<UITimeData>
    {
        private int m_LastDisplayDay;

        protected override void OnBind()
        {
            Data.CurrentDate.Bind(ChangeDate);
        }

        protected override void OnUnbind()
        {
            Data.CurrentDate.Unbind(ChangeDate);
        }

        private void ChangeDate(DateTime _, DateTime newDate)
        {
            if (m_DateText == null) return;

            if (newDate.Day != m_LastDisplayDay)
            {
                m_DateText.text = newDate.ToString("yyyy-MM-dd");
                m_LastDisplayDay = newDate.Day;
            }
        }
    }
}