#region

using System;
using System.Collections.Generic;
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

        // ── Session & Sampling ──────────────────────────────────────────
        private readonly CapabilityDebugSampler m_Sampler = new CapabilityDebugSampler();

        internal CapabilityDebugSession Session { get; } = new CapabilityDebugSession();

        internal CapabilityDebugTraceCapture TraceCapture { get; } =
            new CapabilityDebugTraceCapture();

        // ── Timeline State ──────────────────────────────────────────────
        private bool m_IsToolPlaying = true;
        private bool m_WasEditorPausedByDebugger;
        private string m_FrameInput = "0";
        private int m_LastAppliedFrameIndex = -1;
        private int m_LastSampledUnityFrameCount = -1;

        // ── Timeline Tracks ─────────────────────────────────────────────
        private readonly Dictionary<string, CapabilityTimelineTrack> m_TimelineTracks =
            new Dictionary<string, CapabilityTimelineTrack>(128);

        private readonly List<CapabilityTimelineTrack> m_SortedTracks =
            new List<CapabilityTimelineTrack>(128);

        private Vector2 m_TimelineScroll;

        // ── Log Buffer (调试面板 Inspector 用) ──────────────────────────
        internal readonly List<CapabilityDebugLogSnapshot> LogBuffer =
            new List<CapabilityDebugLogSnapshot>(64);

        [MenuItem(MenuPath)]
        public static void OpenToolbox()
        {
            CapabilityDebugToolboxWindow window =
                GetWindow<CapabilityDebugToolboxWindow>();
            window.titleContent = new GUIContent("Capability 工具箱");
            window.minSize = new Vector2(1060f, 620f);
            window.Show();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void OnEnable()
        {
            if (!m_WindowsCreated)
            {
                CreateSubWindows();
            }

            m_Sampler.SetTraceCapture(TraceCapture);
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            // 如果窗口被手动关闭但 Play Mode 仍在运行，解除暂停。
            if (m_WasEditorPausedByDebugger && EditorApplication.isPaused)
            {
                EditorApplication.isPaused = false;
            }

            EditorApplication.update -= OnEditorUpdate;
            TraceCapture.Unregister();

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
            m_DebugWindow.SetToolbox(this);
            m_FlowchartWindow.SetSession(Session);
            m_WindowsCreated = true;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Editor Update — 采样
        // ══════════════════════════════════════════════════════════════════

        private void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying)
            {
                if (Session.HasFrames)
                {
                    // 退出 Play Mode 时先把场景恢复到初始帧，再清空会话。
                    RestoreToFrameZero();
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
            if (!Session.HasFrames) return;
            if (Session.IsAtLatestFrame) return;
            if (EditorApplication.isPaused) return;

            EditorApplication.isPaused = true;
            m_WasEditorPausedByDebugger = true;
        }

        private bool ShouldSample()
        {
            if (!m_IsToolPlaying) return false;
            if (EditorApplication.isPaused) return false;
            if (!Session.IsAtLatestFrame) return false;
            if (Time.frameCount == m_LastSampledUnityFrameCount) return false;
            return true;
        }

        private void SampleFrame()
        {
            int nextFrameIndex = Session.FrameCount;
            CapabilityDebugFrame frame = m_Sampler.Sample(nextFrameIndex);
            Session.AddFrame(frame);
            m_LastSampledUnityFrameCount = frame.UnityFrameCount;
            AppendTimelineFrame(frame);
            EnsureSelection(frame);
            SyncFrameInput();
            m_DebugWindow?.OnFrameSampled();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Timeline — Controls
        // ══════════════════════════════════════════════════════════════════

        private void JumpToFrame(int frameIndex)
        {
            if (!Session.HasFrames) return;

            Session.SetCurrentFrameIndex(frameIndex);
            SyncFrameInput();
            PauseToolAndUnity();
            ApplyCurrentFrameToScene();

            if (Session.CurrentFrame != null)
            {
                EnsureSelection(Session.CurrentFrame);
            }
        }

        private void PlayTool()
        {
            m_IsToolPlaying = true;

            if (!Session.HasFrames)
            {
                if (m_WasEditorPausedByDebugger)
                {
                    EditorApplication.isPaused = false;
                    m_WasEditorPausedByDebugger = false;
                }

                return;
            }

            if (!Session.IsAtLatestFrame)
            {
                Session.SetCurrentFrameIndex(Session.FrameCount - 1);
                SyncFrameInput();
                ApplyCurrentFrameToScene();
                if (Session.CurrentFrame != null)
                {
                    EnsureSelection(Session.CurrentFrame);
                }
            }

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
            m_FrameInput = Mathf.Max(0, Session.CurrentFrameIndex).ToString();
        }

        private void ApplyCurrentFrameToScene()
        {
            if (Session.CurrentFrameIndex == m_LastAppliedFrameIndex) return;
            CapabilityDebugSceneState.Restore(Session.CurrentFrame);
            m_LastAppliedFrameIndex = Session.CurrentFrameIndex;
        }

        private void RestoreToFrameZero()
        {
            if (!Session.HasFrames) return;
            if (Session.CurrentFrameIndex == 0) return;

            Session.SetCurrentFrameIndex(0);
            CapabilityDebugSceneState.Restore(Session.CurrentFrame);
            m_LastAppliedFrameIndex = 0;

            if (m_WasEditorPausedByDebugger && EditorApplication.isPaused)
            {
                EditorApplication.isPaused = false;
            }
        }

        private void ClearSession()
        {
            // 先保存选中状态（需要读 CurrentFrame），再清空 Session。
            m_DebugWindow?.OnSessionCleared();

            Session.Clear();
            TraceCapture.Unregister();
            TraceCapture.Clear();
            m_TimelineTracks.Clear();
            m_LastAppliedFrameIndex = -1;
            m_LastSampledUnityFrameCount = -1;
            m_FrameInput = "0";

            if (m_WasEditorPausedByDebugger && EditorApplication.isPaused)
            {
                EditorApplication.isPaused = false;
            }

            m_WasEditorPausedByDebugger = false;
            m_IsToolPlaying = true;

#if UNITY_EDITOR
            CapabilityDebugLogBridge.Clear();
#endif
        }

        // ══════════════════════════════════════════════════════════════════
        //  Timeline — Tracks
        // ══════════════════════════════════════════════════════════════════

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
                    string trackKey = $"{world.Key}:global:{capability.Key}";
                    string logKey = $"{world.Key}:cap:{capability.Key}";

                    if (!Session.LogIndex.TryGetValue(logKey,
                            out List<CapabilityDebugLogSnapshot> logList))
                    {
                        logList = new List<CapabilityDebugLogSnapshot>(capability.Logs.Count);
                        Session.LogIndex.Add(logKey, logList);
                    }

                    logList.AddRange(capability.Logs);

                    if (!m_TimelineTracks.TryGetValue(trackKey,
                            out CapabilityTimelineTrack track))
                    {
                        track = new CapabilityTimelineTrack(
                            $"{world.DisplayName}/{capability.TypeName}",
                            capability.Pipeline ?? string.Empty);
                        BackfillTrack(track, Session.FrameCount - 1);
                        m_TimelineTracks.Add(trackKey, track);
                    }

                    track.Push(capability.State);
                    touched.Add(trackKey);
                }
            }

            foreach (KeyValuePair<string, CapabilityTimelineTrack> pair in m_TimelineTracks)
            {
                if (!touched.Contains(pair.Key) && pair.Value.Count < Session.FrameCount)
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
                return;
            }

            // Delegate to DebugWindow for selection state management.
            m_DebugWindow?.EnsureSelectionForFrame(frame);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Toolbar Message
        // ══════════════════════════════════════════════════════════════════

        private string GetToolbarMessage(out MessageType messageType)
        {
            messageType = MessageType.Info;
            if (!EditorApplication.isPlaying)
            {
                return "进入 Play Mode 后开始记录 Temporal Debug 会话。";
            }

            if (EditorApplication.isPaused && !Session.IsAtLatestFrame)
            {
                return "当前停在历史帧。回到最新帧后才允许 Unity 继续运行。";
            }

            return null;
        }

        // ══════════════════════════════════════════════════════════════════
        //  OnGUI
        // ══════════════════════════════════════════════════════════════════

        private Rect m_LayoutRect;

        private void OnGUI()
        {
            m_LayoutRect = new Rect(0f, 0f, position.width, position.height);
            float tabWidth = CapabilityDebugStyles.ToolboxTabWidth;

            // ── 工具栏 ──
            float toolbarHeight = DrawTopToolbar();

            // ── 左侧标签栏 + 内容区 ──
            Rect bodyRect = new Rect(0f, toolbarHeight, m_LayoutRect.width,
                Mathf.Max(0f, m_LayoutRect.height - toolbarHeight));
            float timelineHeight = Mathf.Max(CapabilityDebugStyles.TimelineMinHeight,
                bodyRect.height * 0.3f);
            Rect upperRect = new Rect(bodyRect.x, bodyRect.y, bodyRect.width,
                Mathf.Max(100f, bodyRect.height - timelineHeight));

            Rect tabBarRect = new Rect(upperRect.x, upperRect.y, tabWidth, upperRect.height);
            DrawTabBar(tabBarRect);

            Rect contentRect = new Rect(tabWidth + 2f, upperRect.y,
                Mathf.Max(1f, upperRect.width - tabWidth - 2f), upperRect.height);
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

            // ── 共用 Timeline ──
            Rect timelineRect = new Rect(0f, upperRect.yMax,
                m_LayoutRect.width, timelineHeight);
            DrawTimelinePanel(timelineRect);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Toolbar
        // ══════════════════════════════════════════════════════════════════

        private float DrawTopToolbar()
        {
            const float toolbarHeight = 21f;
            float y = 0f;
            Rect toolbarRect = new Rect(0f, y, m_LayoutRect.width, toolbarHeight);
            GUILayout.BeginArea(toolbarRect, EditorStyles.toolbar);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Capability Temporal Debugger", EditorStyles.boldLabel,
                GUILayout.ExpandWidth(true));

            GUI.enabled = Session.HasFrames;
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
                    new GUIContent(message), Mathf.Max(1f, m_LayoutRect.width - 8f)));
                Rect helpRect = new Rect(4f, y + 4f, Mathf.Max(1f, m_LayoutRect.width - 8f),
                    helpHeight);
                EditorGUI.HelpBox(helpRect, message, messageType);
                y = helpRect.yMax + 4f;
            }

            Rect separatorRect = new Rect(0f, y, m_LayoutRect.width, 1f);
            EditorGUI.DrawRect(separatorRect, CapabilityDebugStyles.SeparatorColor);
            return y + 3f;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Tab Bar
        // ══════════════════════════════════════════════════════════════════

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

        // ══════════════════════════════════════════════════════════════════
        //  Tab Content
        // ══════════════════════════════════════════════════════════════════

        private void DrawDebugPanel(Rect contentRect)
        {
            if (m_DebugWindow != null)
            {
                m_DebugWindow.OnInternalGUI(contentRect);
            }
        }

        private void DrawFlowchartPanel(Rect contentRect)
        {
            if (m_FlowchartWindow != null)
            {
                m_FlowchartWindow.OnInternalGUI(contentRect);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Timeline Panel
        // ══════════════════════════════════════════════════════════════════

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

            GUI.enabled = Session.HasFrames;
            if (GUILayout.Button("暂停", GUILayout.Width(54f)))
            {
                PauseToolAndUnity();
            }

            int totalFrames = Session.FrameCount;
            int current = Mathf.Max(0, Session.CurrentFrameIndex);
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
            if (!Session.HasFrames || m_TimelineTracks.Count == 0)
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox("暂无 Timeline 数据。", MessageType.Info);
                return;
            }

            // 根据当前活动页签的选中项筛选时间轴轨道。
            string filterPipeline = null;
            string filterCapKey = null;
            if (m_SelectedTab == 0 && m_DebugWindow != null)
            {
                filterCapKey = m_DebugWindow.SelectedCapabilityKeyForTimeline;
                if (filterCapKey == null)
                {
                    filterPipeline = m_DebugWindow.SelectedPipelineForTimeline;
                }
            }
            else if (m_SelectedTab == 1 && m_FlowchartWindow != null)
            {
                filterCapKey = m_FlowchartWindow.SelectedCapabilityKey;
                if (filterCapKey == null)
                {
                    filterPipeline = m_FlowchartWindow.SelectedPipeline;
                }
            }

            m_SortedTracks.Clear();
            foreach (KeyValuePair<string, CapabilityTimelineTrack> pair in m_TimelineTracks)
            {
                CapabilityTimelineTrack track = pair.Value;

                // 选中具体 Cap → 只显示该 Cap 的轨道。
                if (filterCapKey != null)
                {
                    if (!pair.Key.EndsWith(":global:" + filterCapKey,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                }
                // 选中 Pipeline → 只显示属于该 Pipeline 的轨道。
                else if (filterPipeline != null)
                {
                    if (!TrackPipelineContains(track.Pipeline, filterPipeline))
                    {
                        continue;
                    }
                }

                m_SortedTracks.Add(track);
            }

            m_SortedTracks.Sort((x, y) => string.CompareOrdinal(x.Name, y.Name));
            m_TimelineScroll = EditorGUILayout.BeginScrollView(m_TimelineScroll,
                GUILayout.ExpandHeight(true));
            float width = Mathf.Max(280f, m_LayoutRect.width - 260f);
            for (int i = 0; i < m_SortedTracks.Count; i++)
            {
                DrawTrack(m_SortedTracks[i], width);
            }

            EditorGUILayout.EndScrollView();
        }

        private static bool TrackPipelineContains(string pipeline, string target)
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

            if (Session.CurrentFrameIndex >= 0 && frameCount > 1)
            {
                float normalized = Session.CurrentFrameIndex / (float)(frameCount - 1);
                float markerX = Mathf.Lerp(timelineRect.x, timelineRect.xMax, normalized);
                EditorGUI.DrawRect(new Rect(markerX, timelineRect.y - 1f, 2f,
                    timelineRect.height + 2f), Color.red);
            }

            Rect labelRect = new Rect(timelineRect.xMax + 8f, rowRect.y,
                Mathf.Max(40f, m_LayoutRect.width - timelineRect.width - 24f), rowRect.height);
            EditorGUI.LabelField(labelRect, track.Name);
        }
    }
}