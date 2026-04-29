#region

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#endregion

namespace Core.Capability.Editor
{
    public class CapabilityDebugWindow : EditorWindow
    {
        private const string MenuPath = "框架工具/调试/Capability 可视化";

        private readonly CapabilityDebugSession m_Session = new CapabilityDebugSession();
        private readonly CapabilityDebugSampler m_Sampler = new CapabilityDebugSampler();
        private readonly List<CapabilityDebugLogSnapshot> m_LogBuffer =
            new List<CapabilityDebugLogSnapshot>(64);
        private readonly Dictionary<string, CapabilityTimelineTrack> m_TimelineTracks =
            new Dictionary<string, CapabilityTimelineTrack>(128);
        private readonly List<CapabilityTimelineTrack> m_SortedTracks =
            new List<CapabilityTimelineTrack>(128);

        private Vector2 m_NavigationScroll;
        private Vector2 m_DetailScroll;
        private Vector2 m_InspectorScroll;
        private Vector2 m_LogScroll;
        private Vector2 m_TimelineScroll;

        private string m_SelectedWorldKey;
        private string m_SelectedEntityKey;
        private string m_SelectedItemKey;
        private CapabilityDebugItemKind m_SelectedItemKind;

        private bool m_IsToolPlaying = true;
        private bool m_ComponentsFoldout = true;
        private bool m_CapabilitiesFoldout = true;
        private bool m_GlobalCapabilitiesFoldout = true;
        private bool m_WasEditorPausedByDebugger;
        private string m_FrameInput = "0";
        private int m_LastAppliedFrameIndex = -1;
        private int m_LastSampledUnityFrameCount = -1;
        private float m_InspectorPanelWidth = CapabilityDebugStyles.InspectorMinWidth;

        private readonly HashSet<string> m_ExpandedFoldouts = new HashSet<string>();

        [MenuItem(MenuPath)]
        private static void OpenWindow()
        {
            CapabilityDebugWindow window = GetWindow<CapabilityDebugWindow>();
            window.titleContent = new GUIContent("Capability Temporal Debugger");
            window.minSize = new Vector2(980f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying)
            {
                if (m_Session.HasFrames)
                {
                    ClearSession();
                }

                Repaint();
                return;
            }

            EnforceHistoricalPause();

            if (ShouldSample())
            {
                SampleFrame();
            }

            Repaint();
        }

        private void EnforceHistoricalPause()
        {
            if (!m_Session.HasFrames)
            {
                return;
            }

            if (m_Session.IsAtLatestFrame)
            {
                return;
            }

            if (EditorApplication.isPaused)
            {
                return;
            }

            // 历史帧必须冻结 Unity，否则刚还原的场景状态会被下一帧运行逻辑覆盖。
            EditorApplication.isPaused = true;
            m_WasEditorPausedByDebugger = true;
        }

        private bool ShouldSample()
        {
            if (!m_IsToolPlaying)
            {
                return false;
            }

            if (EditorApplication.isPaused)
            {
                return false;
            }

            if (!m_Session.IsAtLatestFrame)
            {
                return false;
            }

            if (Time.frameCount == m_LastSampledUnityFrameCount)
            {
                return false;
            }

            return true;
        }

        private void SampleFrame()
        {
            int nextFrameIndex = m_Session.Frames.Count;
            CapabilityDebugFrame frame = m_Sampler.Sample(nextFrameIndex);
            m_Session.AddFrame(frame);
            m_LastSampledUnityFrameCount = frame.UnityFrameCount;
            AppendTimelineFrame(frame);
            EnsureSelection(frame);
            SyncFrameInput();
        }

        private void OnGUI()
        {
            float contentTop = DrawTopToolbar();
            Rect contentRect = new Rect(0f, contentTop, position.width,
                Mathf.Max(0f, position.height - contentTop));
            float timelineHeight = Mathf.Max(CapabilityDebugStyles.TimelineMinHeight,
                contentRect.height * 0.33f);
            Rect upperRect = new Rect(contentRect.x, contentRect.y, contentRect.width,
                Mathf.Max(100f, contentRect.height - timelineHeight));
            Rect timelineRect = new Rect(contentRect.x, upperRect.yMax, contentRect.width,
                timelineHeight);

            DrawUpperPanels(upperRect);
            DrawTimelinePanel(timelineRect);
        }

        private float DrawTopToolbar()
        {
            const float toolbarHeight = 21f;
            float y = 0f;
            Rect toolbarRect = new Rect(0f, y, position.width, toolbarHeight);
            GUILayout.BeginArea(toolbarRect, EditorStyles.toolbar);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Capability Temporal Debugger", EditorStyles.boldLabel,
                GUILayout.ExpandWidth(true));

            GUI.enabled = m_Session.HasFrames;
            if (GUILayout.Button("清空当前会话", EditorStyles.toolbarButton, GUILayout.Width(96f)))
            {
                ClearSession();
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            GUILayout.EndArea();
            y += toolbarHeight;

            string message = GetToolbarMessage(out MessageType messageType);
            if (!string.IsNullOrEmpty(message))
            {
                float helpHeight = Mathf.Max(38f, EditorStyles.helpBox.CalcHeight(
                    new GUIContent(message), Mathf.Max(1f, position.width - 8f)));
                Rect helpRect = new Rect(4f, y + 4f, Mathf.Max(1f, position.width - 8f),
                    helpHeight);
                EditorGUI.HelpBox(helpRect, message, messageType);
                y = helpRect.yMax + 4f;
            }

            Rect separatorRect = new Rect(0f, y, position.width, 1f);
            EditorGUI.DrawRect(separatorRect, CapabilityDebugStyles.SeparatorColor);
            return y + 3f;
        }

        private string GetToolbarMessage(out MessageType messageType)
        {
            messageType = MessageType.Info;
            if (!EditorApplication.isPlaying)
            {
                return "进入 Play Mode 后开始记录 Temporal Debug 会话。";
            }

            if (EditorApplication.isPaused && !m_Session.IsAtLatestFrame)
            {
                return "当前停在历史帧。回到最新帧后才允许 Unity 继续运行。";
            }

            return null;
        }

        private void DrawUpperPanels(Rect rect)
        {
            float spacing = CapabilityDebugStyles.PanelSpacing;
            float availableWidth = Mathf.Max(1f, rect.width - spacing * 2f);
            float leftWidth = availableWidth * 0.25f;
            float middleWidth = availableWidth * 0.35f;
            float inspectorWidth = availableWidth - leftWidth - middleWidth;

            if (availableWidth >= CapabilityDebugStyles.LeftPanelMinWidth +
                CapabilityDebugStyles.MiddlePanelMinWidth +
                CapabilityDebugStyles.InspectorMinWidth)
            {
                leftWidth = Mathf.Max(CapabilityDebugStyles.LeftPanelMinWidth, leftWidth);
                middleWidth = Mathf.Max(CapabilityDebugStyles.MiddlePanelMinWidth, middleWidth);
                inspectorWidth = availableWidth - leftWidth - middleWidth;
                if (inspectorWidth < CapabilityDebugStyles.InspectorMinWidth)
                {
                    float deficit = CapabilityDebugStyles.InspectorMinWidth - inspectorWidth;
                    leftWidth = Mathf.Max(CapabilityDebugStyles.LeftPanelMinWidth,
                        leftWidth - deficit * 0.4f);
                    middleWidth = Mathf.Max(CapabilityDebugStyles.MiddlePanelMinWidth,
                        middleWidth - deficit * 0.6f);
                    inspectorWidth = availableWidth - leftWidth - middleWidth;
                }
            }

            Rect leftRect = new Rect(rect.x, rect.y, leftWidth, rect.height);
            Rect middleRect = new Rect(leftRect.xMax + spacing, rect.y, middleWidth, rect.height);
            Rect rightRect = new Rect(middleRect.xMax + spacing, rect.y,
                Mathf.Max(1f, inspectorWidth), rect.height);
            m_InspectorPanelWidth = rightRect.width;

            GUILayout.BeginArea(leftRect, EditorStyles.helpBox);
            DrawNavigationPanel();
            GUILayout.EndArea();

            GUILayout.BeginArea(middleRect, EditorStyles.helpBox);
            DrawEntityDetailPanel();
            GUILayout.EndArea();

            GUILayout.BeginArea(rightRect, EditorStyles.helpBox);
            DrawInspectorPanel();
            GUILayout.EndArea();
        }

        private void DrawNavigationPanel()
        {
            EditorGUILayout.LabelField("Entity 导航", EditorStyles.boldLabel);
            CapabilityDebugFrame frame = m_Session.CurrentFrame;
            if (frame == null)
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox("暂无采样帧。", MessageType.Info);
                return;
            }

            m_NavigationScroll = EditorGUILayout.BeginScrollView(m_NavigationScroll);
            for (int i = 0; i < frame.Worlds.Count; i++)
            {
                CapabilityDebugWorldSnapshot world = frame.Worlds[i];
                EditorGUILayout.LabelField(world.DisplayName, EditorStyles.boldLabel);
                DrawGlobalCapabilityNavigation(world);
                for (int j = 0; j < world.Entities.Count; j++)
                {
                    CapabilityDebugEntitySnapshot entity = world.Entities[j];
                    bool selected = entity.Key == m_SelectedEntityKey;
                    if (DrawSelectableRow(entity.DisplayName, selected,
                            CapabilityDebugStyles.InactiveStateColor))
                    {
                        SelectEntity(world.Key, entity.Key);
                    }
                }

                EditorGUILayout.Space(6f);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawGlobalCapabilityNavigation(CapabilityDebugWorldSnapshot world)
        {
            m_GlobalCapabilitiesFoldout = EditorGUILayout.Foldout(m_GlobalCapabilitiesFoldout,
                $"Global Cap ({world.GlobalCapabilities.Count})", true);
            if (!m_GlobalCapabilitiesFoldout)
            {
                return;
            }

            string previousStage = null;
            for (int i = 0; i < world.GlobalCapabilities.Count; i++)
            {
                CapabilityDebugCapabilitySnapshot capability = world.GlobalCapabilities[i];
                if (previousStage != capability.StageName)
                {
                    previousStage = capability.StageName;
                    EditorGUILayout.LabelField(previousStage, EditorStyles.miniBoldLabel);
                }

                bool selected = m_SelectedItemKind == CapabilityDebugItemKind.GlobalCapability &&
                                m_SelectedItemKey == capability.Key;
                string label =
                    $"{capability.TypeName} ({capability.MatchedEntityCount}, {capability.LastTickMilliseconds:F2} ms)";
                if (DrawSelectableRow(label, selected,
                        CapabilityDebugStyles.ToStateColor(capability.State)))
                {
                    m_SelectedWorldKey = world.Key;
                    m_SelectedEntityKey = null;
                    SelectItem(capability.Key, CapabilityDebugItemKind.GlobalCapability);
                }
            }

            EditorGUILayout.Space(4f);
        }

        private void DrawEntityDetailPanel()
        {
            EditorGUILayout.LabelField("Entity 详情", EditorStyles.boldLabel);
            CapabilityDebugEntitySnapshot entity = GetSelectedEntity();
            if (entity == null)
            {
                if (m_SelectedItemKind == CapabilityDebugItemKind.GlobalCapability)
                {
                    DrawSelectedGlobalCapabilityDetail();
                    return;
                }

                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox("请选择一个 Entity。", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(entity.DisplayName, EditorStyles.miniBoldLabel);
            m_DetailScroll = EditorGUILayout.BeginScrollView(m_DetailScroll);
            DrawComponentList(entity);
            EditorGUILayout.Space(10f);
            DrawCapabilityList(entity);
            EditorGUILayout.EndScrollView();
        }

        private void DrawComponentList(CapabilityDebugEntitySnapshot entity)
        {
            m_ComponentsFoldout = EditorGUILayout.Foldout(m_ComponentsFoldout,
                $"Comp ({entity.Components.Count})", true);
            if (!m_ComponentsFoldout)
            {
                return;
            }

            for (int i = 0; i < entity.Components.Count; i++)
            {
                CapabilityDebugComponentSnapshot component = entity.Components[i];
                bool selected = m_SelectedItemKind == CapabilityDebugItemKind.Component &&
                                m_SelectedItemKey == component.Key;
                if (DrawSelectableRow(component.TypeName, selected,
                        CapabilityDebugStyles.InactiveStateColor))
                {
                    SelectItem(component.Key, CapabilityDebugItemKind.Component);
                }
            }
        }

        private void DrawCapabilityList(CapabilityDebugEntitySnapshot entity)
        {
            m_CapabilitiesFoldout = EditorGUILayout.Foldout(m_CapabilitiesFoldout,
                $"Cap ({entity.Capabilities.Count})", true);
            if (!m_CapabilitiesFoldout)
            {
                return;
            }

            string previousStage = null;
            for (int i = 0; i < entity.Capabilities.Count; i++)
            {
                CapabilityDebugCapabilitySnapshot capability = entity.Capabilities[i];
                if (previousStage != capability.StageName)
                {
                    previousStage = capability.StageName;
                    Rect headerRect = EditorGUILayout.GetControlRect(false, 26f);
                    EditorGUI.LabelField(headerRect,
                        $"{capability.StageName} ({capability.TickGroupOrder})",
                        CapabilityDebugStyles.StageHeaderStyle);
                }

                bool selected = m_SelectedItemKind == CapabilityDebugItemKind.Capability &&
                                m_SelectedItemKey == capability.Key;
                Color stateColor = CapabilityDebugStyles.ToStateColor(capability.State);
                string label = $"{capability.TypeName} [{capability.UpdateMode}]";
                if (DrawSelectableRow(label, selected, stateColor))
                {
                    SelectItem(capability.Key, CapabilityDebugItemKind.Capability);
                }
            }
        }

        private void DrawInspectorPanel()
        {
            EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);
            CapabilityDebugEntitySnapshot entity = GetSelectedEntity();
            if (entity == null)
            {
                if (m_SelectedItemKind == CapabilityDebugItemKind.GlobalCapability)
                {
                    m_InspectorScroll = EditorGUILayout.BeginScrollView(m_InspectorScroll,
                        true, true);
                    DrawCapabilityInspector(GetSelectedGlobalCapability());
                    EditorGUILayout.EndScrollView();
                    return;
                }

                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox("请选择一个 Entity。", MessageType.Info);
                return;
            }

            m_InspectorScroll = EditorGUILayout.BeginScrollView(m_InspectorScroll,
                true, true);
            if (m_SelectedItemKind == CapabilityDebugItemKind.Component)
            {
                DrawComponentInspector(entity.FindComponent(m_SelectedItemKey));
            }
            else if (m_SelectedItemKind == CapabilityDebugItemKind.Capability)
            {
                DrawCapabilityInspector(entity.FindCapability(m_SelectedItemKey));
            }
            else if (m_SelectedItemKind == CapabilityDebugItemKind.GlobalCapability)
            {
                DrawCapabilityInspector(GetSelectedGlobalCapability());
            }
            else
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox("请选择一个 Comp 或 Cap。", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawComponentInspector(CapabilityDebugComponentSnapshot component)
        {
            if (component == null)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox("当前帧中该组件不存在。", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField(component.TypeName, EditorStyles.boldLabel);
            InspectorColumnLayout layout = BuildInspectorLayout(component.Fields,
                new[] { "ComponentId", "Type" },
                new[] { component.ComponentId.ToString(), component.TypeFullName });
            DrawInspectorMetaRow("ComponentId", string.Empty,
                component.ComponentId.ToString(), layout);
            DrawInspectorMetaRow("Type", string.Empty, component.TypeFullName, layout);
            EditorGUILayout.Space(6f);
            DrawValueList(component.Fields, layout, 0);
        }

        private void DrawCapabilityInspector(CapabilityDebugCapabilitySnapshot capability)
        {
            if (capability == null)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox("当前帧中该 Capability 不存在。", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField(capability.TypeName, EditorStyles.boldLabel);
            InspectorColumnLayout layout = BuildInspectorLayout(capability.Fields,
                new[] { "CapabilityId", "Type", "UpdateMode", "Stage", "State" },
                new[]
                {
                    capability.CapabilityId.ToString(),
                    capability.TypeFullName,
                    capability.UpdateMode.ToString(),
                    $"{capability.StageName} ({capability.TickGroupOrder})",
                    capability.State.ToString()
                });
            DrawInspectorMetaRow("CapabilityId", string.Empty,
                capability.CapabilityId.ToString(), layout);
            DrawInspectorMetaRow("Type", string.Empty, capability.TypeFullName, layout);
            DrawInspectorMetaRow("UpdateMode", string.Empty,
                capability.UpdateMode.ToString(), layout);
            DrawInspectorMetaRow("Stage", string.Empty,
                $"{capability.StageName} ({capability.TickGroupOrder})", layout);
            DrawInspectorMetaRow("State", string.Empty, capability.State.ToString(), layout);
            DrawInspectorMetaRow("LastTickMs", string.Empty,
                capability.LastTickMilliseconds.ToString("F3"), layout);
            DrawInspectorMetaRow("MatchedEntities", string.Empty,
                FormatMatchedEntities(capability), layout);
            EditorGUILayout.Space(6f);

            EditorGUILayout.LabelField("标记字段", EditorStyles.boldLabel);
            if (capability.Fields.Count == 0)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox("没有标记 CapabilityDebugField 的字段。", MessageType.Info);
            }
            else
            {
                DrawValueList(capability.Fields, layout, 0);
            }

            EditorGUILayout.Space(8f);
            DrawCapabilityLogs(capability);
        }

        private void DrawCapabilityLogs(CapabilityDebugCapabilitySnapshot capability)
        {
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
            m_Session.CollectLogs(m_SelectedEntityKey, capability.Key,
                m_Session.CurrentFrameIndex, m_LogBuffer);
            if (m_SelectedItemKind == CapabilityDebugItemKind.GlobalCapability)
            {
                CollectGlobalCapabilityLogs(capability, m_LogBuffer);
            }
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(140f));
            m_LogScroll = EditorGUILayout.BeginScrollView(m_LogScroll);
            if (m_LogBuffer.Count == 0)
            {
                EditorGUILayout.LabelField("暂无日志。", EditorStyles.miniLabel);
            }
            else
            {
                for (int i = 0; i < m_LogBuffer.Count; i++)
                {
                    CapabilityDebugLogSnapshot log = m_LogBuffer[i];
                    EditorGUILayout.SelectableLabel(
                        $"[第 {log.FrameIndex} 帧] {log.Message}",
                        CapabilityDebugStyles.LogStyle,
                        GUILayout.MinHeight(20f));
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawValueList(List<CapabilityDebugValueSnapshot> values,
            InspectorColumnLayout layout, int indent, string parentPath = "")
        {
            for (int i = 0; i < values.Count; i++)
            {
                string path = string.IsNullOrEmpty(parentPath)
                    ? values[i].Name
                    : parentPath + "/" + values[i].Name;
                DrawValue(values[i], layout, indent, path);
            }
        }

        private void DrawValue(CapabilityDebugValueSnapshot value,
            InspectorColumnLayout layout, int indent, string path)
        {
            if (value.Children.Count == 0)
            {
                DrawFlatValue(value, layout, indent);
            }
            else
            {
                DrawFoldoutValue(value, layout, indent, path);
            }
        }

        private void DrawFlatValue
            (CapabilityDebugValueSnapshot value, InspectorColumnLayout layout, int indent)
        {
            DrawInspectorRow(value.Name, value.TypeName, value.DisplayValue,
                indent, false, false, layout);
        }

        private void DrawFoldoutValue
            (CapabilityDebugValueSnapshot value, InspectorColumnLayout layout, int indent,
                string path)
        {
            bool wasExpanded = m_ExpandedFoldouts.Contains(path);
            bool isExpanded = DrawInspectorRow(value.Name, value.TypeName,
                value.DisplayValue, indent, true, wasExpanded, layout);

            if (isExpanded != wasExpanded)
            {
                if (isExpanded)
                {
                    m_ExpandedFoldouts.Add(path);
                }
                else
                {
                    m_ExpandedFoldouts.Remove(path);
                }
            }

            if (isExpanded)
            {
                DrawValueList(value.Children, layout, indent + 1, path);
            }
        }

        private void DrawInspectorMetaRow
            (string name, string typeName, string displayValue, InspectorColumnLayout layout)
        {
            DrawInspectorRow(name, typeName, displayValue, 0, false, false, layout);
        }

        private bool DrawInspectorRow
        (
            string name, string typeName, string displayValue, int indent,
            bool hasFoldout, bool isExpanded, InspectorColumnLayout layout
        )
        {
            Rect rowRect = GUILayoutUtility.GetRect(layout.ContentWidth,
                CapabilityDebugStyles.InspectorRowHeight, GUILayout.ExpandWidth(false));
            float indentWidth = indent * CapabilityDebugStyles.InspectorIndentWidth;
            float gap = CapabilityDebugStyles.InspectorColumnGap;

            Rect nameRect = new Rect(rowRect.x + indentWidth, rowRect.y,
                Mathf.Max(1f, layout.NameWidth - indentWidth), rowRect.height);
            Rect typeRect = new Rect(rowRect.x + layout.NameWidth + gap, rowRect.y,
                layout.TypeWidth, rowRect.height);
            Rect valueRect = new Rect(typeRect.xMax + gap, rowRect.y,
                layout.ValueWidth, rowRect.height);

            if (hasFoldout)
            {
                isExpanded = EditorGUI.Foldout(nameRect, isExpanded, name, true,
                    CapabilityDebugStyles.FoldoutValueStyle);
            }
            else
            {
                EditorGUI.LabelField(nameRect, name, CapabilityDebugStyles.FieldNameStyle);
            }

            EditorGUI.LabelField(typeRect, typeName, CapabilityDebugStyles.TypeNameStyle);
            EditorGUI.LabelField(valueRect, displayValue,
                CapabilityDebugStyles.WrappedValueStyle);
            return isExpanded;
        }

        private InspectorColumnLayout BuildInspectorLayout
            (List<CapabilityDebugValueSnapshot> values, string[] metaNames, string[] metaValues)
        {
            float nameWidth = 0f;
            float valueWidth = 0f;
            for (int i = 0; i < metaNames.Length; i++)
            {
                nameWidth = Mathf.Max(nameWidth, MeasureName(metaNames[i], 0, false));
            }

            for (int i = 0; i < metaValues.Length; i++)
            {
                valueWidth = Mathf.Max(valueWidth, MeasureValue(metaValues[i]));
            }

            MeasureInspectorValues(values, 0, ref nameWidth, ref valueWidth);
            nameWidth = Mathf.Clamp(nameWidth, CapabilityDebugStyles.InspectorNameMinWidth,
                CapabilityDebugStyles.InspectorNameMaxWidth);

            float visibleWidth = Mathf.Max(CapabilityDebugStyles.InspectorMinWidth,
                m_InspectorPanelWidth - 32f);
            float typeWidth = CapabilityDebugStyles.InspectorTypeColumnWidth;
            float fixedWidth = nameWidth + typeWidth +
                               CapabilityDebugStyles.InspectorColumnGap * 2f;
            float fillValueWidth = visibleWidth - fixedWidth;
            valueWidth = Mathf.Clamp(Mathf.Max(valueWidth, fillValueWidth,
                    CapabilityDebugStyles.InspectorValueMinWidth),
                CapabilityDebugStyles.InspectorValueMinWidth,
                CapabilityDebugStyles.InspectorValueMaxWidth);

            InspectorColumnLayout layout = new InspectorColumnLayout
            {
                NameWidth = nameWidth,
                TypeWidth = typeWidth,
                ValueWidth = valueWidth
            };
            layout.ContentWidth = layout.NameWidth + layout.TypeWidth + layout.ValueWidth +
                                  CapabilityDebugStyles.InspectorColumnGap * 2f;
            return layout;
        }

        private static void MeasureInspectorValues
        (
            List<CapabilityDebugValueSnapshot> values, int indent,
            ref float nameWidth, ref float valueWidth
        )
        {
            for (int i = 0; i < values.Count; i++)
            {
                CapabilityDebugValueSnapshot value = values[i];
                bool hasFoldout = value.Children.Count > 0;
                nameWidth = Mathf.Max(nameWidth,
                    MeasureName(value.Name, indent, hasFoldout));
                valueWidth = Mathf.Max(valueWidth, MeasureValue(value.DisplayValue));
                if (hasFoldout)
                {
                    MeasureInspectorValues(value.Children, indent + 1,
                        ref nameWidth, ref valueWidth);
                }
            }
        }

        private static float MeasureName(string value, int indent, bool hasFoldout)
        {
            float foldoutWidth = hasFoldout ? 16f : 0f;
            float indentWidth = indent * CapabilityDebugStyles.InspectorIndentWidth;
            return indentWidth + foldoutWidth +
                   CapabilityDebugStyles.FieldNameStyle.CalcSize(
                       new GUIContent(value ?? string.Empty)).x + 12f;
        }

        private static float MeasureValue(string value)
        {
            return CapabilityDebugStyles.WrappedValueStyle.CalcSize(
                new GUIContent(value ?? string.Empty)).x + 12f;
        }

        private struct InspectorColumnLayout
        {
            public float NameWidth;
            public float TypeWidth;
            public float ValueWidth;
            public float ContentWidth;
        }

        private void DrawTimelinePanel(Rect rect)
        {
            GUILayout.BeginArea(rect, EditorStyles.helpBox);
            DrawTimelineControls();
            EditorGUILayout.Space(4f);
            DrawTimelineTracks();
            GUILayout.EndArea();
        }

        private void DrawTimelineControls()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("播放", GUILayout.Width(54f)))
            {
                PlayTool();
            }

            GUI.enabled = m_Session.HasFrames;
            if (GUILayout.Button("暂停", GUILayout.Width(54f)))
            {
                PauseToolAndUnity();
            }

            int totalFrames = m_Session.Frames.Count;
            int current = Mathf.Max(0, m_Session.CurrentFrameIndex);
            EditorGUILayout.LabelField($"共 {totalFrames} 帧", GUILayout.Width(84f));

            if (GUILayout.Button("<", GUILayout.Width(28f)))
            {
                JumpToFrame(current - 1);
            }

            GUI.SetNextControlName("CapabilityFrameInput");
            string newInput = EditorGUILayout.TextField(m_FrameInput, GUILayout.Width(68f));
            if (newInput != m_FrameInput)
            {
                m_FrameInput = newInput;
            }

            if (Event.current.type == EventType.KeyDown &&
                Event.current.keyCode == KeyCode.Return &&
                GUI.GetNameOfFocusedControl() == "CapabilityFrameInput")
            {
                if (int.TryParse(m_FrameInput, out int targetFrame))
                {
                    JumpToFrame(targetFrame);
                }

                Event.current.Use();
            }

            if (GUILayout.Button(">", GUILayout.Width(28f)))
            {
                JumpToFrame(current + 1);
            }

            int sliderValue = current;
            int maxFrame = Mathf.Max(0, totalFrames - 1);
            EditorGUI.BeginChangeCheck();
            sliderValue = EditorGUILayout.IntSlider(sliderValue, 0, maxFrame);
            if (EditorGUI.EndChangeCheck())
            {
                JumpToFrame(sliderValue);
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTimelineTracks()
        {
            if (!m_Session.HasFrames || m_TimelineTracks.Count == 0)
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox("暂无 Timeline 数据。", MessageType.Info);
                return;
            }

            m_SortedTracks.Clear();
            foreach (KeyValuePair<string, CapabilityTimelineTrack> pair in m_TimelineTracks)
            {
                m_SortedTracks.Add(pair.Value);
            }

            m_SortedTracks.Sort((x, y) => string.CompareOrdinal(x.Name, y.Name));
            m_TimelineScroll = EditorGUILayout.BeginScrollView(m_TimelineScroll,
                GUILayout.ExpandHeight(true));
            float width = Mathf.Max(280f, position.width - 260f);
            for (int i = 0; i < m_SortedTracks.Count; i++)
            {
                DrawTrack(m_SortedTracks[i], width);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawTrack(CapabilityTimelineTrack track, float timelineWidth)
        {
            Rect rowRect = EditorGUILayout.GetControlRect(false, 17f);
            Rect timelineRect = new Rect(rowRect.x, rowRect.y + 2f, timelineWidth, 12f);
            EditorGUI.DrawRect(timelineRect, CapabilityDebugStyles.PanelBackgroundColor);

            List<CapabilityTimelineSegment> segments = track.BuildSegments();
            float cursorX = timelineRect.x;
            int frameCount = Mathf.Max(1, track.Count);
            for (int i = 0; i < segments.Count; i++)
            {
                CapabilityTimelineSegment segment = segments[i];
                float width = timelineRect.width * segment.Count / frameCount;
                Rect segmentRect = new Rect(cursorX, timelineRect.y, width, timelineRect.height);
                EditorGUI.DrawRect(segmentRect, CapabilityDebugStyles.ToStateColor(segment.State));
                cursorX += width;
            }

            if (m_Session.CurrentFrameIndex >= 0 && frameCount > 1)
            {
                float normalized = m_Session.CurrentFrameIndex / (float)(frameCount - 1);
                float markerX = Mathf.Lerp(timelineRect.x, timelineRect.xMax, normalized);
                EditorGUI.DrawRect(new Rect(markerX, timelineRect.y - 1f, 2f,
                    timelineRect.height + 2f), Color.red);
            }

            Rect labelRect = new Rect(timelineRect.xMax + 8f, rowRect.y,
                Mathf.Max(40f, position.width - timelineRect.width - 24f), rowRect.height);
            EditorGUI.LabelField(labelRect, track.Name);
        }

        private bool DrawSelectableRow(string label, bool selected, Color markerColor)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 22f);
            if (selected)
            {
                EditorGUI.DrawRect(rect, CapabilityDebugStyles.SelectedRowColor);
            }
            else if (rect.Contains(Event.current.mousePosition))
            {
                EditorGUI.DrawRect(rect, CapabilityDebugStyles.RowHoverColor);
            }

            Rect markerRect = new Rect(rect.x + 2f, rect.y + 4f, 6f, rect.height - 8f);
            EditorGUI.DrawRect(markerRect, markerColor);
            Rect labelRect = new Rect(markerRect.xMax + 6f, rect.y, rect.width - 14f, rect.height);
            EditorGUI.LabelField(labelRect, label, CapabilityDebugStyles.RowButtonStyle);

            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                rect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                return true;
            }

            return false;
        }

        private void AppendTimelineFrame(CapabilityDebugFrame frame)
        {
            HashSet<string> touched = new HashSet<string>();
            for (int worldIndex = 0; worldIndex < frame.Worlds.Count; worldIndex++)
            {
                CapabilityDebugWorldSnapshot world = frame.Worlds[worldIndex];
                for (int capIndex = 0; capIndex < world.GlobalCapabilities.Count; capIndex++)
                {
                    CapabilityDebugCapabilitySnapshot capability =
                        world.GlobalCapabilities[capIndex];
                    string key = $"{world.Key}:global:{capability.Key}";
                    if (!m_TimelineTracks.TryGetValue(key, out CapabilityTimelineTrack track))
                    {
                        track = new CapabilityTimelineTrack(
                            $"{world.DisplayName}/{capability.TypeName}");
                        BackfillTrack(track, m_Session.Frames.Count - 1);
                        m_TimelineTracks.Add(key, track);
                    }

                    track.Push(capability.State);
                    touched.Add(key);
                }

                for (int entityIndex = 0; entityIndex < world.Entities.Count; entityIndex++)
                {
                    CapabilityDebugEntitySnapshot entity = world.Entities[entityIndex];
                    for (int capIndex = 0; capIndex < entity.Capabilities.Count; capIndex++)
                    {
                        CapabilityDebugCapabilitySnapshot capability =
                            entity.Capabilities[capIndex];
                        string key = $"{entity.Key}:{capability.Key}";
                        if (!m_TimelineTracks.TryGetValue(key, out CapabilityTimelineTrack track))
                        {
                            track = new CapabilityTimelineTrack(
                                $"{entity.DisplayName}/{capability.TypeName}");
                            BackfillTrack(track, m_Session.Frames.Count - 1);
                            m_TimelineTracks.Add(key, track);
                        }

                        track.Push(capability.State);
                        touched.Add(key);
                    }
                }
            }

            foreach (KeyValuePair<string, CapabilityTimelineTrack> pair in m_TimelineTracks)
            {
                if (!touched.Contains(pair.Key) && pair.Value.Count < m_Session.Frames.Count)
                {
                    pair.Value.Push(CapabilityRuntimeState.None);
                }
            }
        }

        private static void BackfillTrack(CapabilityTimelineTrack track, int count)
        {
            for (int i = 0; i < count; i++)
            {
                track.Push(CapabilityRuntimeState.None);
            }
        }

        private void EnsureSelection(CapabilityDebugFrame frame)
        {
            if (frame == null || frame.Worlds.Count == 0)
            {
                m_SelectedWorldKey = null;
                m_SelectedEntityKey = null;
                m_SelectedItemKey = null;
                m_SelectedItemKind = CapabilityDebugItemKind.None;
                return;
            }

            CapabilityDebugEntitySnapshot selectedEntity = frame.FindEntity(m_SelectedEntityKey);
            if (selectedEntity == null)
            {
                CapabilityDebugWorldSnapshot world = frame.Worlds[0];
                selectedEntity = world.Entities.Count > 0 ? world.Entities[0] : null;
                m_SelectedWorldKey = world.Key;
                m_SelectedEntityKey = selectedEntity?.Key;
                if (selectedEntity == null && world.GlobalCapabilities.Count > 0)
                {
                    SelectItem(world.GlobalCapabilities[0].Key,
                        CapabilityDebugItemKind.GlobalCapability);
                    return;
                }

                if (m_SelectedItemKind != CapabilityDebugItemKind.GlobalCapability)
                {
                    m_SelectedItemKey = null;
                    m_SelectedItemKind = CapabilityDebugItemKind.None;
                }
            }

            if (selectedEntity == null)
            {
                return;
            }

            if (!HasSelectedItem(selectedEntity))
            {
                SelectFirstItem(selectedEntity);
            }
        }

        private bool HasSelectedItem(CapabilityDebugEntitySnapshot entity)
        {
            if (m_SelectedItemKind == CapabilityDebugItemKind.GlobalCapability)
            {
                return GetSelectedGlobalCapability() != null;
            }

            if (m_SelectedItemKind == CapabilityDebugItemKind.Component)
            {
                return entity.FindComponent(m_SelectedItemKey) != null;
            }

            if (m_SelectedItemKind == CapabilityDebugItemKind.Capability)
            {
                return entity.FindCapability(m_SelectedItemKey) != null;
            }

            return false;
        }

        private void SelectFirstItem(CapabilityDebugEntitySnapshot entity)
        {
            if (entity.Components.Count > 0)
            {
                SelectItem(entity.Components[0].Key, CapabilityDebugItemKind.Component);
                return;
            }

            if (entity.Capabilities.Count > 0)
            {
                SelectItem(entity.Capabilities[0].Key, CapabilityDebugItemKind.Capability);
                return;
            }

            m_SelectedItemKey = null;
            m_SelectedItemKind = CapabilityDebugItemKind.None;
        }

        private void SelectEntity(string worldKey, string entityKey)
        {
            m_SelectedWorldKey = worldKey;
            m_SelectedEntityKey = entityKey;
            m_SelectedItemKey = null;
            m_SelectedItemKind = CapabilityDebugItemKind.None;
            CapabilityDebugEntitySnapshot entity = GetSelectedEntity();
            if (entity != null)
            {
                SelectFirstItem(entity);
            }
        }

        private void SelectItem(string itemKey, CapabilityDebugItemKind kind)
        {
            m_SelectedItemKey = itemKey;
            m_SelectedItemKind = kind;
            m_LogScroll = Vector2.zero;
            m_ExpandedFoldouts.Clear();
        }

        private CapabilityDebugEntitySnapshot GetSelectedEntity()
        {
            CapabilityDebugFrame frame = m_Session.CurrentFrame;
            return frame?.FindEntity(m_SelectedEntityKey);
        }

        private CapabilityDebugCapabilitySnapshot GetSelectedGlobalCapability()
        {
            CapabilityDebugFrame frame = m_Session.CurrentFrame;
            CapabilityDebugWorldSnapshot world = frame?.FindWorld(m_SelectedWorldKey);
            return world?.FindGlobalCapability(m_SelectedItemKey);
        }

        private void DrawSelectedGlobalCapabilityDetail()
        {
            CapabilityDebugCapabilitySnapshot capability = GetSelectedGlobalCapability();
            if (capability == null)
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox("请选择一个 Entity。", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Global Capability", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(capability.TypeName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Stage",
                $"{capability.StageName} ({capability.TickGroupOrder})");
            EditorGUILayout.LabelField("Matched Entities",
                FormatMatchedEntities(capability));
            EditorGUILayout.LabelField("Last Tick",
                $"{capability.LastTickMilliseconds:F3} ms");
        }

        private static string FormatMatchedEntities(CapabilityDebugCapabilitySnapshot capability)
        {
            if (capability == null || capability.MatchedEntityIds.Count == 0)
            {
                return "0";
            }

            return string.Join(", ", capability.MatchedEntityIds);
        }

        private void CollectGlobalCapabilityLogs
        (
            CapabilityDebugCapabilitySnapshot capability,
            List<CapabilityDebugLogSnapshot> destination
        )
        {
            destination.Clear();
            if (capability == null || string.IsNullOrEmpty(m_SelectedWorldKey))
            {
                return;
            }

            int endIndex = Mathf.Clamp(m_Session.CurrentFrameIndex, 0,
                m_Session.Frames.Count - 1);
            for (int i = 0; i <= endIndex; i++)
            {
                CapabilityDebugWorldSnapshot world =
                    m_Session.Frames[i].FindWorld(m_SelectedWorldKey);
                CapabilityDebugCapabilitySnapshot current =
                    world?.FindGlobalCapability(capability.Key);
                if (current == null)
                {
                    continue;
                }

                destination.AddRange(current.Logs);
            }
        }

        private void JumpToFrame(int frameIndex)
        {
            if (!m_Session.HasFrames)
            {
                return;
            }

            m_Session.SetCurrentFrameIndex(frameIndex);
            SyncFrameInput();
            PauseToolAndUnity();
            ApplyCurrentFrameToScene();

            if (m_Session.CurrentFrame != null)
            {
                EnsureSelection(m_Session.CurrentFrame);
            }
        }

        private void ApplyCurrentFrameToScene()
        {
            if (m_Session.CurrentFrameIndex == m_LastAppliedFrameIndex)
            {
                return;
            }

            CapabilityDebugSceneState.Restore(m_Session.CurrentFrame);
            m_LastAppliedFrameIndex = m_Session.CurrentFrameIndex;
        }

        private void PlayTool()
        {
            m_IsToolPlaying = true;

            if (!m_Session.HasFrames)
            {
                // 无帧数据时只解除 Unity 暂停，让 OnEditorUpdate 自然开始采样。
                if (m_WasEditorPausedByDebugger)
                {
                    EditorApplication.isPaused = false;
                    m_WasEditorPausedByDebugger = false;
                }

                return;
            }

            if (!m_Session.IsAtLatestFrame)
            {
                m_Session.SetCurrentFrameIndex(m_Session.Frames.Count - 1);
                SyncFrameInput();
                ApplyCurrentFrameToScene();
                if (m_Session.CurrentFrame != null)
                {
                    EnsureSelection(m_Session.CurrentFrame);
                }
            }

            // 只有回到最新帧时才解除由 Debugger 触发的 Unity 暂停。
            if (m_WasEditorPausedByDebugger)
            {
                EditorApplication.isPaused = false;
                m_WasEditorPausedByDebugger = false;
            }
        }

        private void PauseToolAndUnity()
        {
            m_IsToolPlaying = false;
            if (EditorApplication.isPlaying && !EditorApplication.isPaused)
            {
                EditorApplication.isPaused = true;
                m_WasEditorPausedByDebugger = true;
            }
        }

        private void SyncFrameInput()
        {
            m_FrameInput = Mathf.Max(0, m_Session.CurrentFrameIndex).ToString();
        }

        private void ClearSession()
        {
            // 如果正在查看历史帧，先把场景恢复到最新帧状态再清除数据。
            if (m_Session.HasFrames && !m_Session.IsAtLatestFrame)
            {
                m_Session.SetCurrentFrameIndex(m_Session.Frames.Count - 1);
                SyncFrameInput();
                ApplyCurrentFrameToScene();
            }

            bool wasPaused = !m_IsToolPlaying;

            m_Session.Clear();
            m_TimelineTracks.Clear();
            m_SelectedWorldKey = null;
            m_SelectedEntityKey = null;
            m_SelectedItemKey = null;
            m_SelectedItemKind = CapabilityDebugItemKind.None;
            m_LastAppliedFrameIndex = -1;
            m_LastSampledUnityFrameCount = -1;
            m_FrameInput = "0";
            m_ExpandedFoldouts.Clear();

            if (wasPaused)
            {
                // 清除前是暂停状态 → 保持暂停，不解锁 Unity，不自动开始采样。
                m_IsToolPlaying = false;
            }
            else
            {
                m_IsToolPlaying = true;
                if (m_WasEditorPausedByDebugger && EditorApplication.isPaused)
                {
                    EditorApplication.isPaused = false;
                }

                m_WasEditorPausedByDebugger = false;
            }
#if UNITY_EDITOR
            CapabilityDebugLogBridge.Clear();
#endif
        }
    }
}
