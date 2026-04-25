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
            m_TimePanel = UIManager.Instance.CreatePanel<TimePanel>();
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
            if (m_UITimeData == null)
            {
                m_UITimeData = new UITimeData();
                m_TimePanel.Bind(m_UITimeData);
            }

            m_UITimeData.CurrentDate.Value = newTime;
        }

        private void OnSpeedChanged(TimeType newSpeed)
        {
            if (m_UITimeData == null)
            {
                m_UITimeData = new UITimeData();
                m_TimePanel.Bind(m_UITimeData);
            }

            if (m_UITimeData.TimeSpeed.Value == newSpeed)
            {
                return;
            }

            m_UITimeData.TimeSpeed.Value = newSpeed;
        }
    }
}
