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
            EventBus.GP_OnUpdateSelectionIndicator += OnUpdateSelectionIndicator;
            EventBus.GP_OnDestroySelectionIndicator += OnDestroySelectionIndicator;
            EventBus.GP_OnCreatePathIndicator += OnCreatePathIndicator;
            EventBus.GP_OnUpdatePathIndicator += OnUpdatePathIndicator;
            EventBus.GP_OnDestroyPathIndicator += OnDestroyPathIndictor;
        }


        private void OnDisable()
        {
            EventBus.GP_OnTimeChange -= OnTimeChange;
            EventBus.GP_OnSpeedChange -= OnSpeedChanged;
            EventBus.GP_OnCreateSelectionIndicator -= OnCreateSelectionIndicator;
            EventBus.GP_OnUpdateSelectionIndicator -= OnUpdateSelectionIndicator;
            EventBus.GP_OnDestroySelectionIndicator -= OnDestroySelectionIndicator;
            EventBus.GP_OnCreatePathIndicator -= OnCreatePathIndicator;
            EventBus.GP_OnUpdatePathIndicator -= OnUpdatePathIndicator;
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

        private int OnCreateSelectionIndicator(Vector3 worldPosition)
        {
            var panel = UIManager.Instance.CreatePanel<SelectionIndicatorPanel>(UICanvasType.World);
            if (panel == null)
            {
                // 返回 -1 表示失败，让调用方处理
                return -1;
            }

            PlaceWorldPanel(panel, worldPosition);
            return AddStablePanel(SIList, panel);
        }

        private void OnDestroySelectionIndicator(int index)
        {
            if (index < 0 || index >= SIList.Count) return;

            var panel = SIList[index];
            // 置 null 而非 RemoveAt，保持其他面板的索引稳定。
            SIList[index] = null;

            if (panel != null)
            {
                UIManager.Instance.RemovePanel(panel);
                Destroy(panel.gameObject);
            }
        }

        // 鬼列随缩放动态增减后，选中指示器需要跟随移动到最近可见的镜像位置。
        private void OnUpdateSelectionIndicator(int index, Vector3 worldPosition)
        {
            if (index < 0 || index >= SIList.Count) return;
            SelectionIndicatorPanel panel = SIList[index];
            if (panel == null) return;
            PlaceWorldPanel(panel, worldPosition);
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

            return AddStablePanel(PIList, panel);
        }

        private void OnUpdatePathIndicator(int index, IReadOnlyList<Vector3> unitPathWorldPoints)
        {
            if (index < 0 || index >= PIList.Count)
            {
                return;
            }

            PathIndicatorPanel panel = PIList[index];
            if (panel == null)
            {
                return;
            }

            panel.SetWorldPath(unitPathWorldPoints);
        }

        private void OnDestroyPathIndictor(int index)
        {
            if (index < 0 || index >= PIList.Count) return;

            var panel = PIList[index];
            // 置 null 而非 RemoveAt，保持其他路径指示器的索引稳定。
            PIList[index] = null;

            if (panel != null)
            {
                UIManager.Instance.RemovePanel(panel);
                Destroy(panel.gameObject);
            }
        }

        #endregion

        // 稳定索引分配：优先复用列表中已有的 null 槽位，避免 RemoveAt 导致所有后续索引失效。
        private static int AddStablePanel<TPanel>(List<TPanel> panels, TPanel panel)
            where TPanel : BasePanel
        {
            for (int i = 0; i < panels.Count; i++)
            {
                if (panels[i] != null)
                {
                    continue;
                }

                panels[i] = panel;
                return i;
            }

            panels.Add(panel);
            return panels.Count - 1;
        }

        private static void PlaceWorldPanel(BasePanel panel, Vector3 worldPosition)
        {
            if (panel == null)
            {
                return;
            }

            var rectTransform = panel.transform as RectTransform;
            if (rectTransform == null)
            {
                panel.transform.position = worldPosition;
                return;
            }

            rectTransform.position = worldPosition;
        }
    }
}
