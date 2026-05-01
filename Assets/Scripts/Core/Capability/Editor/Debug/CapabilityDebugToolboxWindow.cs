#region

using UnityEditor;
using UnityEngine;

#endregion

namespace Core.Capability.Editor
{
    public class CapabilityDebugToolboxWindow : EditorWindow
    {
        private const string MenuPath = "框架工具/调试/Capability 工具箱";

        private int m_SelectedTab;
        private readonly string[] m_TabNames = { "调试", "流程图" };

        private CapabilityDebugWindow m_DebugWindow;
        private CapabilityDebugFlowchartWindow m_FlowchartWindow;
        private bool m_WindowsCreated;

        [MenuItem(MenuPath)]
        public static void OpenToolbox()
        {
            CapabilityDebugToolboxWindow window =
                GetWindow<CapabilityDebugToolboxWindow>();
            window.titleContent = new GUIContent("Capability 工具箱");
            window.minSize = new Vector2(1060f, 580f);
            window.Show();
        }

        private void OnEnable()
        {
            if (!m_WindowsCreated)
            {
                CreateSubWindows();
            }
        }

        private void OnDisable()
        {
            if (m_DebugWindow != null)
            {
                DestroyImmediate(m_DebugWindow);
                m_DebugWindow = null;
            }

            if (m_FlowchartWindow != null)
            {
                DestroyImmediate(m_FlowchartWindow);
                m_FlowchartWindow = null;
            }

            m_WindowsCreated = false;
        }

        private void CreateSubWindows()
        {
            m_DebugWindow = CreateInstance<CapabilityDebugWindow>();
            m_FlowchartWindow = CreateInstance<CapabilityDebugFlowchartWindow>();
            m_WindowsCreated = true;
        }

        private void OnGUI()
        {
            float tabWidth = CapabilityDebugStyles.ToolboxTabWidth;

            // 左侧标签栏。
            Rect tabBarRect = new Rect(0f, 0f, tabWidth, position.height);
            DrawTabBar(tabBarRect);

            // 右侧内容区。
            Rect contentRect = new Rect(tabWidth + 2f, 0f,
                Mathf.Max(1f, position.width - tabWidth - 2f), position.height);
            GUILayout.BeginArea(contentRect);

            if (m_DebugWindow == null || m_FlowchartWindow == null)
            {
                CreateSubWindows();
            }

            if (m_SelectedTab == 0)
            {
                DrawDebugPanel(contentRect);
            }
            else
            {
                DrawFlowchartPanel(contentRect);
            }

            GUILayout.EndArea();
        }

        private void DrawTabBar(Rect rect)
        {
            GUILayout.BeginArea(rect, EditorStyles.helpBox);
            GUILayout.Space(8f);

            GUIStyle tabStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                fixedHeight = 36f,
                fontSize = 13
            };

            for (int i = 0; i < m_TabNames.Length; i++)
            {
                bool isSelected = m_SelectedTab == i;
                GUI.backgroundColor = isSelected
                    ? new Color(0.35f, 0.55f, 0.85f)
                    : Color.white;
                if (GUILayout.Button(m_TabNames[i], tabStyle, GUILayout.Width(64f),
                        GUILayout.Height(36f)))
                {
                    m_SelectedTab = i;
                }

                GUILayout.Space(4f);
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndArea();
        }

        private void DrawDebugPanel(Rect contentRect)
        {
            if (m_DebugWindow != null)
            {
                m_DebugWindow.OnInternalGUI(contentRect);
            }
        }

        private void DrawFlowchartPanel(Rect contentRect)
        {
            if (m_FlowchartWindow == null)
            {
                return;
            }

            SyncSessionToFlowchart();
            m_FlowchartWindow.OnInternalGUI(contentRect);
        }

        private void SyncSessionToFlowchart()
        {
            if (m_DebugWindow != null)
            {
                m_FlowchartWindow.SetSession(m_DebugWindow.GetSession());
            }
        }
    }
}
