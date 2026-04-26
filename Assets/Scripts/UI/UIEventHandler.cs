#region

using System;
using System.Collections.Generic;
using Common.Contracts;
using Common.Event;
using Tool;
using UI.Panel;
using UnityEngine;

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
            EventBus.GP_OnCreateSelectionIndicator += OnCreateSelectionIndicator;
            EventBus.GP_OnDestroySelectionIndicator += OnDestroySelectionIndicator;
            EventBus.GP_OnCreatePathIndicator += OnCreatePathIndicator;
            EventBus.GP_OnDestroyPathIndicator += OnDestroyPathIndictor;
        }


        private void OnDisable()
        {
            EventBus.GP_OnTimeChange -= OnTimeChange;
            EventBus.GP_OnSpeedChange -= OnSpeedChanged;
            EventBus.GP_OnCreateSelectionIndicator -= OnCreateSelectionIndicator;
            EventBus.GP_OnDestroySelectionIndicator -= OnDestroySelectionIndicator;
            EventBus.GP_OnCreatePathIndicator -= OnCreatePathIndicator;
            EventBus.GP_OnDestroyPathIndicator -= OnDestroyPathIndictor;
        }

        private TimePanel m_TimePanel;
        private SelectionIndicatorPanel m_SelectionIndicatorPanel;
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

        #region SelectionIndicatorPanel

        private readonly List<SelectionIndicatorPanel> SIList = new();

        private int OnCreateSelectionIndicator()
        {
            var panel = UIManager.Instance.CreatePanel<SelectionIndicatorPanel>(UICanvasType.World);
            if (panel == null)
            {
                // 返回 -1 表示失败，让调用方处理
                return -1;
            }

            SIList.Add(panel);
            return SIList.Count - 1;
        }

        private void OnDestroySelectionIndicator(int index)
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

        #region PathIndicatorPanel

        private readonly List<PathIndicatorPanel> PIList = new();

        private int OnCreatePathIndicator(IReadOnlyList<Vector3> unitPathWorldPoints)
        {
            var panel =
                UIManager.Instance.CreatePanel<PathIndicatorPanel>(UICanvasType.World, false);

            if (panel == null)
            {
                // 返回 -1 表示失败，让调用方处理
                return -1;
            }

            panel.SetWorldPath(unitPathWorldPoints);

            /*
            如果你已经自己算好了 UI 本地坐标，就用：
            pathIndicatorPanel.SetLocalPath(localPathPoints);

            改配色用：
            pathIndicatorPanel.SetPalette(main, rim, shadow, highlight);
            */
            UIManager.Instance.ShowPanel(panel);

            PIList.Add(panel);
            return PIList.Count - 1;
        }

        private void OnDestroyPathIndictor(int index)
        {
            if (index < 0 || index >= PIList.Count) return;

            var panel = PIList[index];

            // 先把元素移出列表，保证索引干净
            PIList.RemoveAt(index);

            if (panel != null)
            {
                UIManager.Instance.RemovePanel(panel);
            }
        }

        #endregion
    }
}