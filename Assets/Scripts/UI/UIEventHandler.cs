#region

using System;
using System.Collections.Generic;
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
            EventBus.GP_OnTimeChange += OnTimeChange;
            EventBus.GP_OnSpeedChange += OnSpeedChanged;
            EventBus.GP_OnCreateSelectionIndictor += OnCreateSelectionIndictor;
            EventBus.GP_OnDestroySelectionIndictor += OnDestroySelectionIndictor;
        }


        private void OnDisable()
        {
            EventBus.GP_OnTimeChange -= OnTimeChange;
            EventBus.GP_OnSpeedChange -= OnSpeedChanged;
            EventBus.GP_OnCreateSelectionIndictor -= OnCreateSelectionIndictor;
            EventBus.GP_OnDestroySelectionIndictor -= OnDestroySelectionIndictor;
        }

        private TimePanel m_TimePanel;
        private SelectionIndictorPanel m_SelectionIndictorPanel;
        private UITimeData m_UITimeData;

        #region TimePanel

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
                GetCurrentTime = () => EventBus.UI_GetCurrentTime?.Invoke()
            };

            m_TimePanel.Bind(m_UITimeData);
        }

        #endregion

        #region SelectionIndictorPanel

        private readonly List<SelectionIndictorPanel> SIList = new();

        private int OnCreateSelectionIndictor()
        {
            var panel =
                UIManager.Instance.CreatePanel<SelectionIndictorPanel>(UICanvasType.World, false);
            if (panel == null)
            {
                // 返回 -1 表示失败，让调用方处理
                return -1;
            }

            SIList.Add(panel);
            return SIList.Count - 1;
        }

        private void OnDestroySelectionIndictor(int index)
        {
            if (index < 0 || index >= SIList.Count) return;

            var panel = SIList[index];

            // 先把元素移出列表，保证索引干净
            SIList.RemoveAt(index);

            if (panel != null)
            {
                UIManager.Instance.RemovePanel(panel);
            }
        }

        #endregion
    }
}