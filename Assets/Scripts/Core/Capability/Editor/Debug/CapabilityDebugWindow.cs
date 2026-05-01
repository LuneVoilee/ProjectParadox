#region

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#endregion

namespace Core.Capability.Editor
{
    public class CapabilityDebugWindow : EditorWindow
    {
        private CapabilityDebugToolboxWindow m_Toolbox;
        private CapabilityDebugSession m_Session;
        private CapabilityDebugTraceCapture m_TraceCapture;

        private Vector2 m_NavigationScroll;
        private Vector2 m_DetailScroll;
        private Vector2 m_InspectorScroll;
        private Vector2 m_LogScroll;

        private string m_SelectedWorldKey;
        private string m_SelectedEntityKey;
        private string m_SelectedItemKey;
        private string m_SelectedPipeline;
        private CapabilityDebugItemKind m_SelectedItemKind;

        private bool m_ComponentsFoldout = true;
        private int m_NavigationMode;
        private float m_InspectorPanelWidth = CapabilityDebugStyles.InspectorMinWidth;

        private readonly HashSet<string> m_ExpandedFoldouts = new HashSet<string>();
        private readonly HashSet<int> m_EvidenceEntityIds = new HashSet<int>();
        private readonly HashSet<string> m_EvidencePipelines = new HashSet<string>();

        private bool m_PendingRestoreSelection;
        private int m_PendingRestoreNavMode;
        private string m_PendingRestorePipeline;
        private string m_PendingRestoreCapType;      // Capability.TypeFullName
        private int m_PendingRestoreEntityId = -1;    // CEntity.Id
        private string m_PendingRestoreCompType;      // Component.TypeFullName

        private bool m_EvidenceFoldout = true;
        private bool m_EvidenceRecording;
        private bool m_EvidenceFollowTouchedEntities = true;
        private bool m_EvidenceIncludeTransforms = true;
        private string m_EvidenceDescription = string.Empty;
        private string m_EvidenceReproSteps = string.Empty;
        private string m_EvidenceExpected = string.Empty;
        private string m_EvidenceSearch = string.Empty;
        private string m_EvidenceLastExport = string.Empty;
        private int m_EvidenceStartFrame = -1;
        private int m_EvidenceMarkedFrame = -1;

        private void OnEnable()
        {
            // Toolbox 在 CreateSubWindows 之后才注入，OnEnable 时不做任何事。
        }

        internal void SetToolbox(CapabilityDebugToolboxWindow toolbox)
        {
            m_Toolbox = toolbox;
            m_Session = toolbox.Session;
            m_TraceCapture = toolbox.TraceCapture;
        }

        // 采样帧后由 ToolboxWindow 回调，用于维护选中状态。
        internal void EnsureSelectionForFrame(CapabilityDebugFrame frame)
        {
            if (frame == null || frame.Worlds.Count == 0)
            {
                m_SelectedWorldKey = null;
                m_SelectedEntityKey = null;
                m_SelectedItemKey = null;
                m_SelectedItemKind = CapabilityDebugItemKind.None;
                return;
            }

            CapabilityDebugWorldSnapshot world = frame.Worlds[0];

            if (m_PendingRestoreSelection)
            {
                m_NavigationMode = m_PendingRestoreNavMode;

                if (m_NavigationMode == 0 && !string.IsNullOrEmpty(m_PendingRestorePipeline))
                {
                    // 恢复 Pipeline 选择。
                    SelectPipeline(world.Key, m_PendingRestorePipeline);

                    // 进一步恢复 Capability 选择（通过稳定字段 TypeFullName 匹配）。
                    if (!string.IsNullOrEmpty(m_PendingRestoreCapType))
                    {
                        for (int i = 0; i < world.GlobalCapabilities.Count; i++)
                        {
                            if (world.GlobalCapabilities[i].TypeFullName ==
                                m_PendingRestoreCapType)
                            {
                                SelectItem(world.GlobalCapabilities[i].Key,
                                    CapabilityDebugItemKind.Capability);
                                break;
                            }
                        }
                    }
                }
                else if (m_NavigationMode == 1 && m_PendingRestoreEntityId > 0)
                {
                    // Entity 模式：通过稳定字段 EntityId 查找 Entity。
                    CapabilityDebugEntitySnapshot entity = null;
                    for (int i = 0; i < world.Entities.Count; i++)
                    {
                        if (world.Entities[i].EntityId == m_PendingRestoreEntityId)
                        {
                            entity = world.Entities[i];
                            break;
                        }
                    }

                    if (entity != null)
                    {
                        SelectEntity(world.Key, entity.Key);

                        // 进一步恢复 Component 选择（通过 TypeFullName 匹配）。
                        if (!string.IsNullOrEmpty(m_PendingRestoreCompType))
                        {
                            for (int i = 0; i < entity.Components.Count; i++)
                            {
                                if (entity.Components[i].TypeFullName ==
                                    m_PendingRestoreCompType)
                                {
                                    SelectItem(entity.Components[i].Key,
                                        CapabilityDebugItemKind.Component);
                                    break;
                                }
                            }
                        }
                    }
                }

                m_PendingRestoreSelection = false;
            }

            if (m_NavigationMode == 0)
            {
                m_SelectedWorldKey = world.Key;
                if (string.IsNullOrEmpty(m_SelectedPipeline))
                {
                    List<string> pipelines = BuildPipelines(world);
                    if (pipelines.Count > 0)
                    {
                        SelectPipeline(world.Key, pipelines[0]);
                    }
                }

                return;
            }

            CapabilityDebugEntitySnapshot selectedEntity = frame.FindEntity(m_SelectedEntityKey);
            if (selectedEntity == null)
            {
                selectedEntity = world.Entities.Count > 0 ? world.Entities[0] : null;
                m_SelectedWorldKey = world.Key;
                m_SelectedEntityKey = selectedEntity?.Key;
            }

            if (selectedEntity == null) return;
            if (!HasSelectedItem(selectedEntity))
            {
                SelectFirstItem(selectedEntity);
            }
        }

        // 会话清空时由 ToolboxWindow 回调，清除选中状态并标记恢复。
        internal void OnSessionCleared()
        {
            m_PendingRestoreNavMode = m_NavigationMode;
            m_PendingRestorePipeline = null;
            m_PendingRestoreCapType = null;
            m_PendingRestoreEntityId = -1;
            m_PendingRestoreCompType = null;

            if (m_NavigationMode == 0)
            {
                // Pipeline 模式：保存 pipeline 名 + 选中的 Capability TypeFullName。
                m_PendingRestorePipeline = m_SelectedPipeline;
                if (m_SelectedItemKind == CapabilityDebugItemKind.Capability)
                {
                    CapabilityDebugCapabilitySnapshot cap = GetSelectedGlobalCapability();
                    m_PendingRestoreCapType = cap?.TypeFullName;
                }
            }
            else
            {
                // Entity 模式：保存 EntityId + 选中的 Component TypeFullName。
                CapabilityDebugEntitySnapshot entity = GetSelectedEntity();
                if (entity != null)
                {
                    m_PendingRestoreEntityId = entity.EntityId;
                    if (m_SelectedItemKind == CapabilityDebugItemKind.Component)
                    {
                        CapabilityDebugComponentSnapshot comp =
                            entity.FindComponent(m_SelectedItemKey);
                        m_PendingRestoreCompType = comp?.TypeFullName;
                    }
                }
            }

            m_PendingRestoreSelection = true;

            m_SelectedWorldKey = null;
            m_SelectedEntityKey = null;
            m_SelectedItemKey = null;
            m_SelectedItemKind = CapabilityDebugItemKind.None;
            m_EvidenceRecording = false;
            ConfigureEvidenceTraceCapture();
            m_ExpandedFoldouts.Clear();
        }

        internal string SelectedPipelineForTimeline => m_SelectedPipeline;

        internal string SelectedCapabilityKeyForTimeline =>
            m_SelectedItemKind == CapabilityDebugItemKind.Capability
                ? m_SelectedItemKey
                : null;

        internal void OnFrameSampled()
        {
        }

        private void OnGUI()
        {
            // Toolbox 通过 SetToolbox 注入，CreateInstance 时可能尚未设置。
            if (m_Toolbox == null) return;
            OnInternalGUI(position);
        }

        private Rect m_LayoutRect;

        public void OnInternalGUI(Rect layoutRect)
        {
            m_LayoutRect = layoutRect;
            float contentTop = DrawEvidencePanel(0f);
            Rect contentRect = new Rect(0f, contentTop, m_LayoutRect.width,
                Mathf.Max(0f, m_LayoutRect.height - contentTop));
            DrawUpperPanels(contentRect);
        }

        private float DrawEvidencePanel(float y)
        {
            float height = m_EvidenceFoldout ? 218f : 26f;
            Rect rect = new Rect(4f, y, Mathf.Max(1f, m_LayoutRect.width - 8f), height);
            GUILayout.BeginArea(rect, EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            m_EvidenceFoldout = EditorGUILayout.Foldout(m_EvidenceFoldout, "AI Evidence",
                true);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(m_EvidenceRecording ? "Recording" : "Idle",
                GUILayout.Width(78f));
            GUI.enabled = EditorApplication.isPlaying && !m_EvidenceRecording;
            if (GUILayout.Button("开始录制", GUILayout.Width(76f)))
            {
                StartEvidenceRecording();
            }

            GUI.enabled = m_EvidenceRecording;
            if (GUILayout.Button("标记异常帧", GUILayout.Width(88f)))
            {
                MarkEvidenceFrame();
            }

            GUI.enabled = m_Session.HasFrames;
            if (GUILayout.Button("结束并导出", GUILayout.Width(88f)))
            {
                StopAndExportEvidence();
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (!m_EvidenceFoldout)
            {
                GUILayout.EndArea();
                return height;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("现象", GUILayout.Width(36f));
            m_EvidenceDescription = EditorGUILayout.TextField(m_EvidenceDescription);
            EditorGUILayout.LabelField("期望", GUILayout.Width(36f));
            m_EvidenceExpected = EditorGUILayout.TextField(m_EvidenceExpected);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("复现", GUILayout.Width(36f));
            m_EvidenceReproSteps = EditorGUILayout.TextField(m_EvidenceReproSteps);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("加入当前选择", GUILayout.Width(104f)))
            {
                AddSelectedEvidenceTarget();
            }

            if (GUILayout.Button("加入场景选择", GUILayout.Width(104f)))
            {
                AddSceneSelectionEvidenceTargets();
            }

            m_EvidenceSearch = EditorGUILayout.TextField(m_EvidenceSearch);
            if (GUILayout.Button("搜索加入", GUILayout.Width(76f)))
            {
                AddSearchEvidenceTargets();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            m_EvidenceFollowTouchedEntities = EditorGUILayout.ToggleLeft(
                "Follow touched entities", m_EvidenceFollowTouchedEntities,
                GUILayout.Width(168f));
            m_EvidenceIncludeTransforms = EditorGUILayout.ToggleLeft(
                "Transform", m_EvidenceIncludeTransforms, GUILayout.Width(96f));
            EditorGUILayout.LabelField($"Entity: {FormatEvidenceIds()}");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Pipeline: {FormatEvidencePipelines()}");
            if (GUILayout.Button("清空目标", GUILayout.Width(76f)))
            {
                m_EvidenceEntityIds.Clear();
                m_EvidencePipelines.Clear();
                ConfigureEvidenceTraceCapture();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                $"Start:{m_EvidenceStartFrame}  Marked:{m_EvidenceMarkedFrame}  Export:{m_EvidenceLastExport}",
                EditorStyles.miniLabel);
            if (m_EvidenceRecording)
            {
                ConfigureEvidenceTraceCapture();
            }

            GUILayout.EndArea();
            return height;
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
            EditorGUILayout.LabelField("导航", EditorStyles.boldLabel);
            CapabilityDebugFrame frame = m_Session.CurrentFrame;
            if (frame == null)
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox("暂无采样帧。", MessageType.Info);
                return;
            }

            m_NavigationMode = GUILayout.Toolbar(m_NavigationMode,
                new[] { "Capabilities", "Entities" });
            EditorGUILayout.Space(4f);
            m_NavigationScroll = EditorGUILayout.BeginScrollView(m_NavigationScroll);
            for (int i = 0; i < frame.Worlds.Count; i++)
            {
                CapabilityDebugWorldSnapshot world = frame.Worlds[i];
                EditorGUILayout.LabelField(world.DisplayName, EditorStyles.boldLabel);

                if (m_NavigationMode == 0)
                {
                    DrawCapabilityPipelineNavigation(world);
                }
                else
                {
                    DrawEntityNavigation(world);
                }

                EditorGUILayout.Space(6f);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawCapabilityPipelineNavigation(CapabilityDebugWorldSnapshot world)
        {
            List<string> pipelines = BuildPipelines(world);
            for (int i = 0; i < pipelines.Count; i++)
            {
                string pipeline = pipelines[i];
                int count = CountCapabilitiesInPipeline(world, pipeline);
                bool selected = m_SelectedPipeline == pipeline &&
                                m_SelectedWorldKey == world.Key;
                if (DrawSelectableRow($"{pipeline} ({count})", selected,
                        CapabilityDebugStyles.MatchedStateColor))
                {
                    SelectPipeline(world.Key, pipeline);
                }
            }
        }

        private void DrawEntityNavigation(CapabilityDebugWorldSnapshot world)
        {
            for (int j = 0; j < world.Entities.Count; j++)
            {
                CapabilityDebugEntitySnapshot entity = world.Entities[j];
                bool selected = entity.Key == m_SelectedEntityKey;
                if (DrawSelectableRow(entity.DisplayName, selected,
                        CapabilityDebugStyles.NoMatchStateColor))
                {
                    SelectEntity(world.Key, entity.Key);
                }
            }
        }

        private void DrawEntityDetailPanel()
        {
            EditorGUILayout.LabelField(
                m_NavigationMode == 0 ? "Capability 列表" : "Entity 详情",
                EditorStyles.boldLabel);

            // Pipeline 模式下始终显示能力列表，不随单个 Capability 选中而切换。
            if (!string.IsNullOrEmpty(m_SelectedPipeline))
            {
                DrawSelectedPipelineCapabilities();
                return;
            }

            CapabilityDebugEntitySnapshot entity = GetSelectedEntity();
            if (entity == null)
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox("请选择一个 Pipeline 或 Entity。", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(entity.DisplayName, EditorStyles.miniBoldLabel);
            m_DetailScroll = EditorGUILayout.BeginScrollView(m_DetailScroll);
            DrawComponentList(entity);
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
                        CapabilityDebugStyles.NoMatchStateColor))
                {
                    SelectItem(component.Key, CapabilityDebugItemKind.Component);
                }
            }
        }

        private void DrawSelectedPipelineCapabilities()
        {
            CapabilityDebugFrame frame = m_Session.CurrentFrame;
            CapabilityDebugWorldSnapshot world = frame?.FindWorld(m_SelectedWorldKey);
            if (world == null || string.IsNullOrEmpty(m_SelectedPipeline))
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox("请选择一个 Pipeline。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(m_SelectedPipeline, EditorStyles.miniBoldLabel);
            m_DetailScroll = EditorGUILayout.BeginScrollView(m_DetailScroll);
            for (int i = 0; i < world.GlobalCapabilities.Count; i++)
            {
                CapabilityDebugCapabilitySnapshot capability = world.GlobalCapabilities[i];
                if (!PipelineContains(capability.Pipeline, m_SelectedPipeline))
                {
                    continue;
                }

                bool selected = m_SelectedItemKind == CapabilityDebugItemKind.Capability &&
                                m_SelectedItemKey == capability.Key;
                string label =
                    $"{capability.TypeName} [{capability.State}] hit:{capability.MatchedEntityCount} {capability.LastTickMilliseconds:F2} ms";
                if (DrawSelectableRow(label, selected,
                        CapabilityDebugStyles.ToStateColor(capability.State)))
                {
                    SelectItem(capability.Key, CapabilityDebugItemKind.Capability);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawInspectorPanel()
        {
            EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);
            CapabilityDebugEntitySnapshot entity = GetSelectedEntity();
            if (entity == null)
            {
                if (m_SelectedItemKind == CapabilityDebugItemKind.Capability)
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
            DrawInspectorMetaRow("Pipeline", string.Empty,
                capability.Pipeline ?? string.Empty, layout);
            DrawInspectorMetaRow("Tag", string.Empty,
                capability.DebugTag ?? string.Empty, layout);
            DrawInspectorMetaRow("LastTickMs", string.Empty,
                capability.LastTickMilliseconds.ToString("F3"), layout);
            DrawInspectorMetaRow("MatchedEntities", string.Empty,
                FormatMatchedEntities(capability), layout);
            if (!string.IsNullOrEmpty(capability.LastErrorMessage))
            {
                DrawInspectorMetaRow("Error", string.Empty, capability.LastErrorMessage, layout);
            }
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

            // 从增量索引 O(1) 获取日志，不再需要遍历历史帧做 Deep Clone。
            CollectGlobalCapabilityLogs(capability, m_Toolbox.LogBuffer);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(140f));
            m_LogScroll = EditorGUILayout.BeginScrollView(m_LogScroll);
            if (m_Toolbox.LogBuffer.Count == 0)
            {
                EditorGUILayout.LabelField("暂无日志。", EditorStyles.miniLabel);
            }
            else
            {
                for (int i = 0; i < m_Toolbox.LogBuffer.Count; i++)
                {
                    CapabilityDebugLogSnapshot log = m_Toolbox.LogBuffer[i];
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


        private bool HasSelectedItem(CapabilityDebugEntitySnapshot entity)
        {
            if (m_SelectedItemKind == CapabilityDebugItemKind.Capability)
            {
                return GetSelectedGlobalCapability() != null;
            }

            if (m_SelectedItemKind == CapabilityDebugItemKind.Pipeline)
            {
                return !string.IsNullOrEmpty(m_SelectedPipeline);
            }

            if (m_SelectedItemKind == CapabilityDebugItemKind.Component)
            {
                return entity.FindComponent(m_SelectedItemKey) != null;
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

            m_SelectedItemKey = null;
            m_SelectedItemKind = CapabilityDebugItemKind.None;
        }

        private void SelectEntity(string worldKey, string entityKey)
        {
            m_SelectedWorldKey = worldKey;
            m_SelectedEntityKey = entityKey;
            m_SelectedItemKey = null;
            m_SelectedPipeline = null;
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

        private void SelectPipeline(string worldKey, string pipeline)
        {
            m_SelectedWorldKey = worldKey;
            m_SelectedEntityKey = null;
            m_SelectedItemKey = null;
            m_SelectedPipeline = pipeline;
            m_SelectedItemKind = CapabilityDebugItemKind.Pipeline;
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

        private static List<string> BuildPipelines(CapabilityDebugWorldSnapshot world)
        {
            List<string> pipelines = new List<string>(16);
            if (world == null)
            {
                return pipelines;
            }

            // 收集所有 Pipeline 名并记录首个 Cp 的 TickGroupOrder 用于排序。
            Dictionary<string, int> pipelineOrder = new Dictionary<string, int>(16);
            for (int i = 0; i < world.GlobalCapabilities.Count; i++)
            {
                string pipeline = world.GlobalCapabilities[i].Pipeline;
                if (string.IsNullOrEmpty(pipeline))
                {
                    pipeline = CapabilityPipeline.Other;
                }

                string[] parts = pipeline.Split(',');
                for (int j = 0; j < parts.Length; j++)
                {
                    string part = parts[j].Trim();
                    if (string.IsNullOrEmpty(part))
                    {
                        continue;
                    }

                    if (!pipelines.Contains(part))
                    {
                        pipelines.Add(part);
                    }

                    if (!pipelineOrder.ContainsKey(part))
                    {
                        pipelineOrder[part] = world.GlobalCapabilities[i].TickGroupOrder;
                    }
                }
            }

            pipelines.Sort((a, b) =>
            {
                int orderA = pipelineOrder.ContainsKey(a) ? pipelineOrder[a] : int.MaxValue;
                int orderB = pipelineOrder.ContainsKey(b) ? pipelineOrder[b] : int.MaxValue;
                int cmp = orderA.CompareTo(orderB);
                return cmp != 0 ? cmp : string.CompareOrdinal(a, b);
            });
            return pipelines;
        }

        private static int CountCapabilitiesInPipeline
            (CapabilityDebugWorldSnapshot world, string pipeline)
        {
            int count = 0;
            if (world == null)
            {
                return count;
            }

            for (int i = 0; i < world.GlobalCapabilities.Count; i++)
            {
                if (PipelineContains(world.GlobalCapabilities[i].Pipeline, pipeline))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool PipelineContains(string pipeline, string target)
        {
            if (string.IsNullOrEmpty(pipeline))
            {
                return target == CapabilityPipeline.Other;
            }

            string[] parts = pipeline.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.Equals(parts[i].Trim(), target, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void StartEvidenceRecording()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            if (m_EvidenceEntityIds.Count == 0 && m_EvidencePipelines.Count == 0)
            {
                AddSelectedEvidenceTarget();
            }

            m_EvidenceRecording = true;
            m_EvidenceStartFrame = Mathf.Max(0, m_Session.FrameCount);
            m_EvidenceMarkedFrame = -1;
            m_EvidenceLastExport = string.Empty;
            m_TraceCapture.Clear();
            ConfigureEvidenceTraceCapture();
            m_TraceCapture.Register();
        }

        private void MarkEvidenceFrame()
        {
            if (!m_Session.HasFrames)
            {
                return;
            }

            m_EvidenceMarkedFrame = Mathf.Max(0, m_Session.CurrentFrameIndex);
        }

        private void StopAndExportEvidence()
        {
            if (!m_Session.HasFrames)
            {
                return;
            }

            int startFrame = m_EvidenceStartFrame >= 0 ? m_EvidenceStartFrame : 0;
            var request = new CapabilityEvidenceExportRequest
            {
                Description = m_EvidenceDescription,
                ReproSteps = m_EvidenceReproSteps,
                Expected = m_EvidenceExpected,
                StartFrame = startFrame,
                EndFrame = m_Session.FrameCount - 1,
                MarkedFrame = m_EvidenceMarkedFrame,
                FollowTouchedEntities = m_EvidenceFollowTouchedEntities,
                IncludeTransforms = m_EvidenceIncludeTransforms
            };
            request.EntityIds.AddRange(m_EvidenceEntityIds);
            request.Pipelines.AddRange(m_EvidencePipelines);

            m_EvidenceRecording = false;
            ConfigureEvidenceTraceCapture();
            FlushPendingEvidenceTraceToCurrentFrame();
            m_TraceCapture.Unregister();

            if (CapabilityEvidenceExporter.Export(m_Session, request, out string jsonlPath,
                    out string markdownPath, out string error))
            {
                m_EvidenceLastExport = jsonlPath;
                Debug.Log($"Capability evidence exported: {jsonlPath}\n{markdownPath}");
            }
            else
            {
                m_EvidenceLastExport = "Export failed";
                Debug.LogError(error);
            }
        }

        private void AddSelectedEvidenceTarget()
        {
            CapabilityDebugEntitySnapshot entity = GetSelectedEntity();
            if (entity != null)
            {
                m_EvidenceEntityIds.Add(entity.EntityId);
            }

            CapabilityDebugCapabilitySnapshot capability = GetSelectedGlobalCapability();
            if (capability != null && !string.IsNullOrEmpty(capability.Pipeline))
            {
                m_EvidencePipelines.Add(capability.Pipeline);
            }

            if (m_SelectedItemKind == CapabilityDebugItemKind.Pipeline &&
                !string.IsNullOrEmpty(m_SelectedPipeline))
            {
                m_EvidencePipelines.Add(m_SelectedPipeline);
            }

            ConfigureEvidenceTraceCapture();
        }

        private void FlushPendingEvidenceTraceToCurrentFrame()
        {
            CapabilityDebugFrame frame = m_Session.CurrentFrame;
            if (frame == null)
            {
                m_TraceCapture.Clear();
                return;
            }

            m_TraceCapture.Consume(frame.FrameIndex, frame.Traces);
        }

        private void AddSceneSelectionEvidenceTargets()
        {
            GameObject[] selection = Selection.gameObjects;
            for (int i = 0; i < selection.Length; i++)
            {
                GameObject go = selection[i];
                if (go == null)
                {
                    continue;
                }

                EntityInstaller installer = go.GetComponentInParent<EntityInstaller>();
                if (installer?.Entity != null)
                {
                    m_EvidenceEntityIds.Add(installer.Entity.Id);
                }
            }

            ConfigureEvidenceTraceCapture();
        }

        private void AddSearchEvidenceTargets()
        {
            string query = m_EvidenceSearch?.Trim();
            if (string.IsNullOrEmpty(query))
            {
                return;
            }

            if (int.TryParse(query, out int entityId))
            {
                m_EvidenceEntityIds.Add(entityId);
            }

            CapabilityDebugFrame frame = m_Session.CurrentFrame;
            if (frame == null)
            {
                ConfigureEvidenceTraceCapture();
                return;
            }

            for (int worldIndex = 0; worldIndex < frame.Worlds.Count; worldIndex++)
            {
                CapabilityDebugWorldSnapshot world = frame.Worlds[worldIndex];
                for (int entityIndex = 0; entityIndex < world.Entities.Count; entityIndex++)
                {
                    CapabilityDebugEntitySnapshot entity = world.Entities[entityIndex];
                    if (Contains(entity.DisplayName, query))
                    {
                        m_EvidenceEntityIds.Add(entity.EntityId);
                        continue;
                    }

                    for (int componentIndex = 0;
                         componentIndex < entity.Components.Count;
                         componentIndex++)
                    {
                        CapabilityDebugComponentSnapshot component =
                            entity.Components[componentIndex];
                        if (Contains(component.TypeName, query) ||
                            Contains(component.TypeFullName, query))
                        {
                            m_EvidenceEntityIds.Add(entity.EntityId);
                            break;
                        }
                    }
                }

                for (int capabilityIndex = 0;
                     capabilityIndex < world.GlobalCapabilities.Count;
                     capabilityIndex++)
                {
                    CapabilityDebugCapabilitySnapshot capability =
                        world.GlobalCapabilities[capabilityIndex];
                    if (Contains(capability.Pipeline, query) ||
                        Contains(capability.TypeName, query) ||
                        Contains(capability.TypeFullName, query))
                    {
                        m_EvidencePipelines.Add(capability.Pipeline);
                    }
                }
            }

            ConfigureEvidenceTraceCapture();
        }

        private void ConfigureEvidenceTraceCapture()
        {
            m_TraceCapture.Configure(m_EvidenceRecording, m_EvidenceEntityIds,
                m_EvidenceFollowTouchedEntities);
        }

        private string FormatEvidenceIds()
        {
            return m_EvidenceEntityIds.Count == 0
                ? "(all)"
                : string.Join(", ", m_EvidenceEntityIds);
        }

        private string FormatEvidencePipelines()
        {
            return m_EvidencePipelines.Count == 0
                ? "(auto)"
                : string.Join(", ", m_EvidencePipelines);
        }

        private static bool Contains(string value, string query)
        {
            return value != null &&
                   value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
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

            // 从 Session 的增量索引 O(1) 查询。
            string logKey = $"{m_SelectedWorldKey}:cap:{capability.Key}";
            if (m_Session.LogIndex.TryGetValue(logKey,
                    out List<CapabilityDebugLogSnapshot> logList))
            {
                destination.AddRange(logList);
            }
        }
    }
}
