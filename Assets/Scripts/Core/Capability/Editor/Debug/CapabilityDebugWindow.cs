using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Core.Capability.Editor
{
    public class CapabilityDebugWindow : EditorWindow
    {
        private const string MenuPath = "GX框架工具/Capability/调试/Capability 可视化";

        private static readonly Color m_NoneStateColor = new Color(0.25f, 0.25f, 0.25f, 0.4f);

        private static readonly Color m_InactiveStateColor = Color.white;

        private static readonly Color m_ActiveStateColor = Color.cyan;

        private static readonly Color m_BlockedStateColor = Color.yellow;

        private readonly List<CapabilityWorld> m_WorldOptions = new List<CapabilityWorld>(8);

        private readonly List<CEntity> m_EntityOptions = new List<CEntity>(128);

        private readonly List<CapabilityBase> m_UpdateCapabilities = new List<CapabilityBase>(64);

        private readonly List<CapabilityBase> m_FixedUpdateCapabilities = new List<CapabilityBase>(64);

        private readonly Dictionary<string, CapabilityTimelineTrack> m_UpdateTracks = new Dictionary<string, CapabilityTimelineTrack>(64);

        private readonly Dictionary<string, CapabilityTimelineTrack> m_FixedUpdateTracks = new Dictionary<string, CapabilityTimelineTrack>(64);

        private readonly List<CapabilityTimelineTrack> m_SortedTracks = new List<CapabilityTimelineTrack>(64);

        private CapabilityWorld m_SelectedWorld;

        private CEntity m_SelectedEntity;

        private Vector2 m_ScrollPosition;

        private int m_FrameSize = 500;

        private int m_SampleIndex;

        private bool m_IsSampling = true;

        private double m_NextRefreshTime;

        [MenuItem(MenuPath)]
        private static void OpenWindow()
        {
            CapabilityDebugWindow window = GetWindow<CapabilityDebugWindow>();
            window.titleContent = new GUIContent("Capability 可视化");
            window.minSize = new Vector2(860f, 480f);
            window.Show();
        }

        private void OnEnable()
        {
            m_NextRefreshTime = 0d;
            EditorApplication.update += OnEditorUpdate;
            RefreshSelectionOptions();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup >= m_NextRefreshTime)
            {
                RefreshSelectionOptions();
                m_NextRefreshTime = EditorApplication.timeSinceStartup + 0.2d;
            }

            if (m_IsSampling && EditorApplication.isPlaying)
            {
                SampleSelectedEntity();
            }

            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("进入 Play Mode 后开始实时采样。", MessageType.Info);
            }

            if (m_SelectedWorld == null)
            {
                EditorGUILayout.HelpBox("当前没有可用的 CapabilityWorld。", MessageType.Warning);
                return;
            }

            if (m_SelectedEntity == null || !m_SelectedEntity.IsActive)
            {
                EditorGUILayout.HelpBox("当前 World 中没有可调试的有效实体。", MessageType.Warning);
                return;
            }

            DrawLegend();

            m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);
            DrawTrackGroup("Update", m_UpdateTracks);
            EditorGUILayout.Space(12f);
            DrawTrackGroup("FixedUpdate", m_FixedUpdateTracks);
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawWorldSelector();
            DrawEntitySelector();

            int newFrameSize = EditorGUILayout.IntSlider("历史帧数", m_FrameSize, 60, 1500);
            if (newFrameSize != m_FrameSize)
            {
                m_FrameSize = newFrameSize;
                ResetHistory();
            }

            m_IsSampling = EditorGUILayout.ToggleLeft("自动采样（Play Mode）", m_IsSampling);

            if (GUILayout.Button("清空采样历史"))
            {
                ResetHistory();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawWorldSelector()
        {
            if (m_WorldOptions.Count == 0)
            {
                EditorGUILayout.LabelField("World", "无");
                return;
            }

            string[] options = new string[m_WorldOptions.Count];
            int currentIndex = 0;
            for (int i = 0; i < m_WorldOptions.Count; i++)
            {
                CapabilityWorld world = m_WorldOptions[i];
                options[i] = GetWorldDisplayName(world);
                if (world == m_SelectedWorld)
                {
                    currentIndex = i;
                }
            }

            int selectedIndex = EditorGUILayout.Popup("World", currentIndex, options);
            if (selectedIndex != currentIndex)
            {
                m_SelectedWorld = m_WorldOptions[selectedIndex];
                m_SelectedEntity = null;
                ResetHistory();
                RefreshEntityOptions();
            }
        }

        private void DrawEntitySelector()
        {
            if (m_EntityOptions.Count == 0)
            {
                EditorGUILayout.LabelField("Entity", "无");
                return;
            }

            string[] options = new string[m_EntityOptions.Count];
            int currentIndex = 0;
            for (int i = 0; i < m_EntityOptions.Count; i++)
            {
                CEntity entity = m_EntityOptions[i];
                options[i] = GetEntityDisplayName(entity);
                if (entity == m_SelectedEntity)
                {
                    currentIndex = i;
                }
            }

            int selectedIndex = EditorGUILayout.Popup("Entity", currentIndex, options);
            if (selectedIndex != currentIndex)
            {
                m_SelectedEntity = m_EntityOptions[selectedIndex];
                ResetHistory();
            }
        }

        private void DrawLegend()
        {
            EditorGUILayout.BeginHorizontal();
            DrawLegendItem(m_ActiveStateColor, "激活");
            DrawLegendItem(m_InactiveStateColor, "未激活");
            DrawLegendItem(m_BlockedStateColor, "被阻塞");
            DrawLegendItem(m_NoneStateColor, "不存在/未采样");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4f);
        }

        private void DrawLegendItem(Color color, string text)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 14f, GUILayout.Width(120f));
            Rect colorRect = new Rect(rect.x, rect.y + 2f, 16f, 10f);
            EditorGUI.DrawRect(colorRect, color);
            Rect labelRect = new Rect(colorRect.xMax + 6f, rect.y, rect.width - 22f, rect.height);
            EditorGUI.LabelField(labelRect, text);
        }

        private void DrawTrackGroup(string groupName, Dictionary<string, CapabilityTimelineTrack> tracks)
        {
            EditorGUILayout.LabelField(groupName, EditorStyles.boldLabel);
            if (tracks.Count == 0)
            {
                EditorGUILayout.HelpBox("暂无数据。", MessageType.Info);
                return;
            }

            m_SortedTracks.Clear();
            foreach (KeyValuePair<string, CapabilityTimelineTrack> pair in tracks)
            {
                m_SortedTracks.Add(pair.Value);
            }

            m_SortedTracks.Sort((x, y) => string.CompareOrdinal(x.Name, y.Name));

            float timelineWidth = Mathf.Max(240f, position.width - 280f);
            for (int i = 0; i < m_SortedTracks.Count; i++)
            {
                DrawTrack(m_SortedTracks[i], timelineWidth);
            }
        }

        private void DrawTrack(CapabilityTimelineTrack track, float timelineWidth)
        {
            Rect rowRect = EditorGUILayout.GetControlRect(false, 18f);
            Rect timelineRect = new Rect(rowRect.x, rowRect.y + 2f, timelineWidth, 14f);
            EditorGUI.DrawRect(timelineRect, new Color(0f, 0f, 0f, 0.15f));

            List<CapabilityTimelineSegment> segments = track.BuildSegments();
            float cursorX = timelineRect.x;
            for (int i = 0; i < segments.Count; i++)
            {
                CapabilityTimelineSegment segment = segments[i];
                float width = timelineRect.width * segment.Count / Mathf.Max(1, m_FrameSize);
                if (width <= 0f)
                {
                    continue;
                }

                Rect segmentRect = new Rect(cursorX, timelineRect.y, width, timelineRect.height);
                EditorGUI.DrawRect(segmentRect, ToStateColor(segment.State));
                cursorX += width;
                if (cursorX >= timelineRect.xMax)
                {
                    break;
                }
            }

            Rect labelRect = new Rect(timelineRect.xMax + 8f, rowRect.y, Mathf.Max(40f, position.width - timelineRect.width - 24f), rowRect.height);
            EditorGUI.LabelField(labelRect, track.Name);
        }

        private Color ToStateColor(CapabilityRuntimeState state)
        {
            switch (state)
            {
                case CapabilityRuntimeState.Active:
                    return m_ActiveStateColor;
                case CapabilityRuntimeState.Blocked:
                    return m_BlockedStateColor;
                case CapabilityRuntimeState.Inactive:
                    return m_InactiveStateColor;
                default:
                    return m_NoneStateColor;
            }
        }

        private void RefreshSelectionOptions()
        {
            RefreshWorldOptions();
            RefreshEntityOptions();
        }

        private void RefreshWorldOptions()
        {
            m_WorldOptions.Clear();
            IReadOnlyList<CapabilityWorldBase> worlds = CapabilityWorldRegistry.Worlds;
            for (int i = 0; i < worlds.Count; i++)
            {
                CapabilityWorldBase world = worlds[i];
                if (world is CapabilityWorld capabilityWorld && capabilityWorld.IsActive)
                {
                    m_WorldOptions.Add(capabilityWorld);
                }
            }

            CapabilityWorld previousWorld = m_SelectedWorld;
            if (m_SelectedWorld != null && !m_WorldOptions.Contains(m_SelectedWorld))
            {
                m_SelectedWorld = null;
            }

            if (m_SelectedWorld == null && m_WorldOptions.Count > 0)
            {
                m_SelectedWorld = m_WorldOptions[0];
            }

            if (!ReferenceEquals(previousWorld, m_SelectedWorld))
            {
                m_SelectedEntity = null;
                ResetHistory();
            }
        }

        private void RefreshEntityOptions()
        {
            m_EntityOptions.Clear();
            if (m_SelectedWorld == null || m_SelectedWorld.Children == null)
            {
                m_SelectedEntity = null;
                return;
            }

            foreach (CEntity entity in m_SelectedWorld.Children)
            {
                if (entity != null && entity.IsActive)
                {
                    m_EntityOptions.Add(entity);
                }
            }

            m_EntityOptions.Sort((x, y) => x.Id.CompareTo(y.Id));

            CEntity previousEntity = m_SelectedEntity;
            if (m_SelectedEntity != null && !m_EntityOptions.Contains(m_SelectedEntity))
            {
                m_SelectedEntity = null;
            }

            if (m_SelectedEntity == null && m_EntityOptions.Count > 0)
            {
                m_SelectedEntity = m_EntityOptions[0];
            }

            if (!ReferenceEquals(previousEntity, m_SelectedEntity))
            {
                ResetHistory();
            }
        }

        private void ResetHistory()
        {
            m_UpdateTracks.Clear();
            m_FixedUpdateTracks.Clear();
            m_UpdateCapabilities.Clear();
            m_FixedUpdateCapabilities.Clear();
            m_SampleIndex = 0;
        }

        private void SampleSelectedEntity()
        {
            if (m_SelectedWorld == null || m_SelectedEntity == null || !m_SelectedEntity.IsActive)
            {
                return;
            }

            m_SampleIndex++;

            m_UpdateCapabilities.Clear();
            m_FixedUpdateCapabilities.Clear();
            m_SelectedWorld.GetCapabilities(m_SelectedEntity, m_UpdateCapabilities, m_FixedUpdateCapabilities);

            SampleCapabilityList(m_UpdateCapabilities, m_UpdateTracks);
            SampleCapabilityList(m_FixedUpdateCapabilities, m_FixedUpdateTracks);
        }

        private void SampleCapabilityList(List<CapabilityBase> capabilities, Dictionary<string, CapabilityTimelineTrack> tracks)
        {
            for (int i = 0; i < capabilities.Count; i++)
            {
                CapabilityBase capability = capabilities[i];
                if (capability == null)
                {
                    continue;
                }

                string key = capability.GetType().FullName ?? capability.GetType().Name;
                if (!tracks.TryGetValue(key, out CapabilityTimelineTrack track))
                {
                    track = new CapabilityTimelineTrack(m_FrameSize, capability.GetType().Name);
                    tracks.Add(key, track);
                }

                track.Push(ResolveCapabilityState(capability), m_SampleIndex);
            }

            foreach (KeyValuePair<string, CapabilityTimelineTrack> pair in tracks)
            {
                if (pair.Value.LastSampleIndex != m_SampleIndex)
                {
                    pair.Value.Push(CapabilityRuntimeState.None, m_SampleIndex);
                }
            }
        }

        private CapabilityRuntimeState ResolveCapabilityState(CapabilityBase capability)
        {
            if (capability.TagList != null && m_SelectedWorld.IsCapabilityBlocked(m_SelectedEntity, capability.TagList))
            {
                return CapabilityRuntimeState.Blocked;
            }

            return capability.IsActive ? CapabilityRuntimeState.Active : CapabilityRuntimeState.Inactive;
        }

        private string GetWorldDisplayName(CapabilityWorld world)
        {
            string worldName = string.IsNullOrWhiteSpace(world.Name) ? world.GetType().Name : world.Name;
            return $"{worldName} [{world.GetType().Name}]";
        }

        private string GetEntityDisplayName(CEntity entity)
        {
            string entityName = string.IsNullOrWhiteSpace(entity.Name) ? entity.GetType().Name : entity.Name;
            return $"#{entity.Id} {entityName}";
        }
    }
}
