#region

using System;
using Common.Contracts;
using Common.Event;
using Tool;
using UI.Panel;

#endregion

namespace UI
{
    public class UIEventHandler : SingletonMono<UIEventHandler>
    {
        private void Start()
        {
            if (m_TimePanel == null)
            {
                m_TimePanel = UIManager.Instance.CreatePanel<TimePanel>();
            }
        }

        private void OnEnable()
        {
            EventBus.OnTimeChangeAction += OnTimeChange;
            EventBus.OnSpeedChanged += OnSpeedChanged;
        }


        private void OnDisable()
        {
            EventBus.OnTimeChangeAction -= OnTimeChange;
            EventBus.OnSpeedChanged -= OnSpeedChanged;
        }

        private TimePanel m_TimePanel;
        private UITimeData m_UITimeData;

        private void OnTimeChange(DateTime newTime)
        {
            EnsureTimeDataBound();
            if (m_UITimeData == null)
            {
                return;
            }

            m_UITimeData.CurrentDate.Value = newTime;
        }

        private void OnSpeedChanged(TimeType newSpeed)
        {
            EnsureTimeDataBound();
            if (m_UITimeData == null)
            {
                return;
            }

            if (m_UITimeData.TimeSpeed.Value == newSpeed)
            {
                return;
            }

            m_UITimeData.TimeSpeed.Value = newSpeed;
        }

        private void EnsureTimeDataBound()
        {
            if (m_UITimeData != null)
            {
                return;
            }

            if (m_TimePanel == null)
            {
                m_TimePanel = UIManager.Instance.CreatePanel<TimePanel>();
            }

            if (m_TimePanel == null)
            {
                return;
            }

            m_UITimeData = new UITimeData
            {
                GetCurrentTime = () => EventBus.GetCurrentTime?.Invoke()
            };

            m_TimePanel.Bind(m_UITimeData);
        }
    }
}