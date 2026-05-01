using UnityEditor;
using UnityEngine;

namespace Core.Capability.Editor
{
    /// <summary>
    ///     Temporal Debugger 的 IMGUI 颜色与样式集中定义。
    /// </summary>
    internal static class CapabilityDebugStyles
    {
        public static readonly Color NoneStateColor = new Color(0.25f, 0.25f, 0.25f, 0.4f);
        public static readonly Color NoMatchStateColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        public static readonly Color MatchedStateColor = new Color(0.30f, 0.58f, 0.95f, 1f);
        public static readonly Color WorkedStateColor = new Color(0.28f, 0.85f, 0.38f, 1f);
        public static readonly Color ErrorStateColor = new Color(1f, 0.28f, 0.22f, 1f);
        public static readonly Color SelectedRowColor = new Color(0.24f, 0.46f, 0.85f, 0.55f);
        public static readonly Color RowHoverColor = new Color(1f, 1f, 1f, 0.06f);
        public static readonly Color PanelBackgroundColor = new Color(0f, 0f, 0f, 0.08f);
        public static readonly Color SeparatorColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);

        public const float TimelineMinHeight = 180f;
        public const float LeftPanelMinWidth = 180f;
        public const float MiddlePanelMinWidth = 240f;
        public const float InspectorMinWidth = 280f;
        public const float PanelSpacing = 4f;
        public const float InspectorRowHeight = 22f;
        public const float InspectorIndentWidth = 14f;
        public const float InspectorColumnGap = 8f;
        public const float InspectorTypeColumnWidth = 110f;
        public const float InspectorNameMinWidth = 96f;
        public const float InspectorNameMaxWidth = 420f;
        public const float InspectorValueMinWidth = 160f;
        public const float InspectorValueMaxWidth = 680f;
        public const float FlowchartNodeWidth = 140f;
        public const float FlowchartNodeHeight = 58f;
        public const float FlowchartNodeSpacing = 24f;
        public const float FlowchartArrowWidth = 36f;
        public static readonly Color FlowchartArrowColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
        public const float ToolboxTabWidth = 72f;

        private static GUIStyle m_StageHeaderStyle;
        private static GUIStyle m_RowButtonStyle;
        private static GUIStyle m_FieldNameStyle;
        private static GUIStyle m_TypeNameStyle;
        private static GUIStyle m_WrappedValueStyle;
        private static GUIStyle m_FoldoutValueStyle;
        private static GUIStyle m_LogStyle;

        public static GUIStyle StageHeaderStyle
        {
            get
            {
                if (m_StageHeaderStyle == null)
                {
                    m_StageHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 15,
                        alignment = TextAnchor.MiddleLeft,
                        padding = new RectOffset(4, 4, 5, 3)
                    };
                }

                return m_StageHeaderStyle;
            }
        }

        public static GUIStyle RowButtonStyle
        {
            get
            {
                if (m_RowButtonStyle == null)
                {
                    m_RowButtonStyle = new GUIStyle(EditorStyles.label)
                    {
                        padding = new RectOffset(6, 6, 3, 3),
                        clipping = TextClipping.Clip
                    };
                }

                return m_RowButtonStyle;
            }
        }

        public static GUIStyle FieldNameStyle
        {
            get
            {
                if (m_FieldNameStyle == null)
                {
                    m_FieldNameStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        wordWrap = false,
                        clipping = TextClipping.Clip,
                        alignment = TextAnchor.MiddleLeft
                    };
                }

                return m_FieldNameStyle;
            }
        }

        public static GUIStyle TypeNameStyle
        {
            get
            {
                if (m_TypeNameStyle == null)
                {
                    m_TypeNameStyle = new GUIStyle(EditorStyles.label)
                    {
                        fontSize = 10,
                        normal = { textColor = new Color(0.6f, 0.6f, 0.6f, 1f) },
                        alignment = TextAnchor.MiddleLeft,
                        clipping = TextClipping.Clip
                    };
                }

                return m_TypeNameStyle;
            }
        }

        public static GUIStyle WrappedValueStyle
        {
            get
            {
                if (m_WrappedValueStyle == null)
                {
                    m_WrappedValueStyle = new GUIStyle(EditorStyles.label)
                    {
                        wordWrap = false,
                        clipping = TextClipping.Clip,
                        alignment = TextAnchor.MiddleLeft
                    };
                }

                return m_WrappedValueStyle;
            }
        }

        public static GUIStyle FoldoutValueStyle
        {
            get
            {
                if (m_FoldoutValueStyle == null)
                {
                    m_FoldoutValueStyle = new GUIStyle(EditorStyles.foldout)
                    {
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = new Color(0.9f, 0.9f, 0.9f, 1f) },
                        clipping = TextClipping.Clip
                    };
                }

                return m_FoldoutValueStyle;
            }
        }

        public static GUIStyle LogStyle
        {
            get
            {
                if (m_LogStyle == null)
                {
                    m_LogStyle = new GUIStyle(EditorStyles.textArea)
                    {
                        wordWrap = true
                    };
                }

                return m_LogStyle;
            }
        }

        public static Color ToStateColor(CapabilityRuntimeState state)
        {
            switch (state)
            {
                case CapabilityRuntimeState.Worked:
                    return WorkedStateColor;
                case CapabilityRuntimeState.Matched:
                    return MatchedStateColor;
                case CapabilityRuntimeState.NoMatch:
                    return NoMatchStateColor;
                case CapabilityRuntimeState.Error:
                    return ErrorStateColor;
                default:
                    return NoneStateColor;
            }
        }

        /// <summary>
        ///     在自动布局中绘制一条全宽水平分隔线。
        /// </summary>
        public static void DrawHorizontalSeparator(float thickness = 1f)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, thickness);
            EditorGUI.DrawRect(rect, SeparatorColor);
        }
    }
}
