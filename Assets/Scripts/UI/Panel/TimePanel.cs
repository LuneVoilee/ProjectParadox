#region

using System;
using Common.Contracts;
using Common.Event;
using Core.Reactive;

#endregion

namespace UI.Panel
{
    public class UITimeData : BasePanelData
    {
        public ReactiveValue<TimeType> TimeSpeed = new();
        public ReactiveValue<DateTime> CurrentDate = new();
    }

    public partial class TimePanel : BasePanel<UITimeData>
    {
        private int m_LastDisplayDay;

        protected override void OnBind()
        {
            Data.CurrentDate.Bind(WhenDateChanged);

            m_SpeedPauseButton.onClick.AddListener(ModifySpeedPause);
            m_Speed1Button.onClick.AddListener(ModifySpeed1);
            m_Speed2Button.onClick.AddListener(ModifySpeed2);
            m_Speed3Button.onClick.AddListener(ModifySpeed3);
            m_Speed4Button.onClick.AddListener(ModifySpeed4);
            m_Speed5Button.onClick.AddListener(ModifySpeed5);

            Data.TimeSpeed.Bind(WhenSpeedChanged);
        }

        protected override void OnUnbind()
        {
            Data.CurrentDate.Unbind(WhenDateChanged);

            m_SpeedPauseButton.onClick.RemoveListener(ModifySpeedPause);
            m_Speed1Button.onClick.RemoveListener(ModifySpeed1);
            m_Speed2Button.onClick.RemoveListener(ModifySpeed2);
            m_Speed3Button.onClick.RemoveListener(ModifySpeed3);
            m_Speed4Button.onClick.RemoveListener(ModifySpeed4);
            m_Speed5Button.onClick.RemoveListener(ModifySpeed5);

            Data.TimeSpeed.Unbind(WhenSpeedChanged);
        }

        private void WhenSpeedChanged(TimeType _, TimeType newSpeed)
        {
            EventBus.OnSpeedChangeRequest?.Invoke(newSpeed);
        }

        private void WhenDateChanged(DateTime _, DateTime newDate)
        {
            if (m_DateText == null) return;

            if (newDate.Day != m_LastDisplayDay)
            {
                m_DateText.text = newDate.ToString("yyyy-MM-dd");
                m_LastDisplayDay = newDate.Day;
            }
        }

        #region SpeedButton

        private void ModifySpeedPause()
        {
            if (Data.TimeSpeed.Value == TimeType.Pause)
            {
                return;
            }

            Data.TimeSpeed.Value = TimeType.Pause;
        }

        private void ModifySpeed1()
        {
            if (Data.TimeSpeed.Value == TimeType.Speed1)
            {
                return;
            }

            Data.TimeSpeed.Value = TimeType.Speed1;
        }

        private void ModifySpeed2()
        {
            if (Data.TimeSpeed.Value == TimeType.Speed2)
            {
                return;
            }

            Data.TimeSpeed.Value = TimeType.Speed2;
        }

        private void ModifySpeed3()
        {
            if (Data.TimeSpeed.Value == TimeType.Speed3)
            {
                return;
            }

            Data.TimeSpeed.Value = TimeType.Speed3;
        }

        private void ModifySpeed4()
        {
            if (Data.TimeSpeed.Value == TimeType.Speed4)
            {
                return;
            }

            Data.TimeSpeed.Value = TimeType.Speed4;
        }

        private void ModifySpeed5()
        {
            if (Data.TimeSpeed.Value == TimeType.Speed5)
            {
                return;
            }

            Data.TimeSpeed.Value = TimeType.Speed5;
        }

        #endregion
    }
}
