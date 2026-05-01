#region

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#endregion

namespace Core.Capability.Editor
{
    public class CapabilityDebugFlowchartWindow : EditorWindow, IHasCustomMenu
    {
        private readonly CapabilityDebugSampler m_Sampler = new CapabilityDebugSampler();
        private CapabilityDebugSession m_Session;

        private Vector2 m_FlowchartScroll;
        private Vector2 m_InspectorScroll;
        private string m_SelectedPipeline;
        private string m_SelectedCapabilityKey;
        private string m_SelectedWorldKey;
        private int m_CurrentFrameIndex = -1;
        private float m_LastSampleTime;

        internal void SetSession(CapabilityDebugSession session)
        {
            m_Session = session;
        }

        public void OnInternalGUI()
        {
            if (m_Session == null || !m_Session.HasFrames)
            {
                EditorGUILayout.HelpBox("暂无采样数据，请进入 Play Mode。", MessageType.Info);
                return;
            }

            CapabilityDebugFrame frame = m_Session.CurrentFrame;
            if (frame == null || frame.Worlds.Count == 0)
            {
                return;
            }

            DrawPipelineSelector(frame);
            DrawFlowchart(frame);
            DrawInspector(frame);
        }

        private void DrawPipelineSelector(CapabilityDebugFrame frame)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Pipeline:", GUILayout.Width(54f));

            List<string> pipelines = BuildPipelines(frame.Worlds[0]);
            int currentIndex = pipelines.IndexOf(m_SelectedPipeline);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int newIndex = EditorGUILayout.Popup(currentIndex, pipelines.ToArray(),
                EditorStyles.toolbarPopup);
            if (newIndex >= 0 && newIndex < pipelines.Count)
            {
                string selected = pipelines[newIndex];
                if (selected != m_SelectedPipeline)
                {
                    m_SelectedPipeline = selected;
                    m_SelectedCapabilityKey = null;
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"Frame: {Mathf.Max(0, m_Session.CurrentFrameIndex)}",
                GUILayout.Width(100f));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFlowchart(CapabilityDebugFrame frame)
        {
            CapabilityDebugWorldSnapshot world = frame.Worlds[0];
            if (world == null || string.IsNullOrEmpty(m_SelectedPipeline))
            {
                return;
            }

            m_SelectedWorldKey = world.Key;

            // 收集当前 Pipeline 的 Cp 并按 TickGroupOrder 排序。
            List<CapabilityDebugCapabilitySnapshot> caps =
                new List<CapabilityDebugCapabilitySnapshot>();
            for (int i = 0; i < world.GlobalCapabilities.Count; i++)
            {
                CapabilityDebugCapabilitySnapshot cap = world.GlobalCapabilities[i];
                if (PipelineContains(cap.Pipeline, m_SelectedPipeline))
                {
                    caps.Add(cap);
                }
            }

            if (caps.Count == 0)
            {
                EditorGUILayout.HelpBox($"Pipeline '{m_SelectedPipeline}' 中没有 Cp。",
                    MessageType.Info);
                return;
            }

            caps.Sort((a, b) => a.TickGroupOrder.CompareTo(b.TickGroupOrder));

            float nodeWidth = CapabilityDebugStyles.FlowchartNodeWidth;
            float nodeHeight = CapabilityDebugStyles.FlowchartNodeHeight;
            float spacing = CapabilityDebugStyles.FlowchartNodeSpacing;
            float arrowWidth = CapabilityDebugStyles.FlowchartArrowWidth;
            float totalWidth = caps.Count * (nodeWidth + arrowWidth + spacing) + 60f;

            Rect viewRect = new Rect(0f, 0f, Mathf.Max(totalWidth, position.width - 30f),
                nodeHeight + 120f);
            m_FlowchartScroll = GUI.BeginScrollView(
                new Rect(0f, 26f, position.width, position.height - 180f),
                m_FlowchartScroll, viewRect);

            float y = 40f;
            float x = 30f;

            for (int i = 0; i < caps.Count; i++)
            {
                CapabilityDebugCapabilitySnapshot cap = caps[i];
                Rect nodeRect = new Rect(x, y, nodeWidth, nodeHeight);
                DrawFlowchartNode(nodeRect, cap);
                x += nodeWidth;

                // 绘制箭头。
                if (i < caps.Count - 1)
                {
                    Rect arrowRect = new Rect(x + 4f, y + nodeHeight * 0.5f - 6f,
                        arrowWidth, 14f);
                    DrawArrow(arrowRect);
                    x += arrowWidth + spacing;
                }
            }

            GUI.EndScrollView();

            // 点击检测。
            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0)
            {
                Vector2 mouse = Event.current.mousePosition;
                mouse.y -= 26f;
                mouse += m_FlowchartScroll;
                x = 30f;
                for (int i = 0; i < caps.Count; i++)
                {
                    Rect nodeRect = new Rect(x, 40f, nodeWidth, nodeHeight);
                    if (nodeRect.Contains(mouse))
                    {
                        m_SelectedCapabilityKey = caps[i].Key;
                        Event.current.Use();
                        Repaint();
                        break;
                    }

                    x += nodeWidth;
                    if (i < caps.Count - 1)
                    {
                        x += arrowWidth + spacing;
                    }
                }
            }
        }

        private void DrawFlowchartNode(Rect rect, CapabilityDebugCapabilitySnapshot cap)
        {
            bool isSelected = m_SelectedCapabilityKey == cap.Key;
            Color bgColor = CapabilityDebugStyles.ToStateColor(cap.State);
            if (isSelected)
            {
                bgColor = Color.Lerp(bgColor, Color.white, 0.4f);
            }

            Color borderColor = isSelected ? Color.white : new Color(0.4f, 0.4f, 0.4f);
            float borderWidth = isSelected ? 2f : 1f;

            // 背景。
            EditorGUI.DrawRect(rect, new Color(bgColor.r, bgColor.g, bgColor.b, 0.25f));

            // 边框。
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, borderWidth), borderColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - borderWidth, rect.width, borderWidth),
                borderColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, borderWidth, rect.height), borderColor);
            EditorGUI.DrawRect(new Rect(rect.xMax - borderWidth, rect.y, borderWidth, rect.height),
                borderColor);

            // 状态色条（左侧）。
            Rect stateBar = new Rect(rect.x + 3f, rect.y + 4f, 4f, rect.height - 8f);
            EditorGUI.DrawRect(stateBar, CapabilityDebugStyles.ToStateColor(cap.State));

            // 类型名。
            Rect nameRect = new Rect(rect.x + 14f, rect.y + 6f, rect.width - 18f, 20f);
            EditorGUI.LabelField(nameRect, cap.TypeName, EditorStyles.boldLabel);

            // 状态标签。
            Rect stateRect = new Rect(rect.x + 14f, rect.y + 26f, rect.width - 18f, 16f);
            EditorGUI.LabelField(stateRect, cap.State.ToString(), EditorStyles.miniLabel);

            // Tick 耗时。
            Rect msRect = new Rect(rect.x + 14f, rect.y + 40f, rect.width - 18f, 14f);
            EditorGUI.LabelField(msRect,
                $"{cap.LastTickMilliseconds:F2} ms  hit:{cap.MatchedEntityCount}",
                CapabilityDebugStyles.TypeNameStyle);
        }

        private static void DrawArrow(Rect rect)
        {
            Vector2 start = new Vector2(rect.x, rect.y + rect.height * 0.5f);
            Vector2 end = new Vector2(rect.x + rect.width, rect.y + rect.height * 0.5f);
            Handles.color = CapabilityDebugStyles.FlowchartArrowColor;
            Handles.DrawLine(start, end);

            // 箭头三角形。
            Vector2 tip = new Vector2(rect.x + rect.width - 2f, rect.y + rect.height * 0.5f);
            Vector2 top = new Vector2(tip.x - 10f, tip.y - 5f);
            Vector2 bottom = new Vector2(tip.x - 10f, tip.y + 5f);
            Handles.DrawAAConvexPolygon(tip, top, bottom);
        }

        private void DrawInspector(CapabilityDebugFrame frame)
        {
            float inspectorHeight = 160f;
            Rect inspectorRect = new Rect(0f, position.height - inspectorHeight,
                position.width, inspectorHeight);
            GUILayout.BeginArea(inspectorRect, EditorStyles.helpBox);

            if (string.IsNullOrEmpty(m_SelectedCapabilityKey))
            {
                EditorGUILayout.LabelField("点击节点查看详情", EditorStyles.centeredGreyMiniLabel);
                GUILayout.EndArea();
                return;
            }

            CapabilityDebugWorldSnapshot world = frame.FindWorld(m_SelectedWorldKey);
            CapabilityDebugCapabilitySnapshot cap =
                world?.FindGlobalCapability(m_SelectedCapabilityKey);
            if (cap == null)
            {
                m_SelectedCapabilityKey = null;
                GUILayout.EndArea();
                return;
            }

            EditorGUILayout.LabelField(cap.TypeName, EditorStyles.boldLabel);
            m_InspectorScroll = EditorGUILayout.BeginScrollView(m_InspectorScroll);
            EditorGUILayout.LabelField("Pipeline", cap.Pipeline ?? string.Empty);
            EditorGUILayout.LabelField("Stage",
                $"{cap.StageName} ({cap.TickGroupOrder})");
            EditorGUILayout.LabelField("State",
                $"{cap.State}  hit:{cap.MatchedEntityCount}");
            EditorGUILayout.LabelField("LastTick",
                $"{cap.LastTickMilliseconds:F3} ms");
            if (!string.IsNullOrEmpty(cap.LastErrorMessage))
            {
                EditorGUILayout.LabelField("Error", cap.LastErrorMessage);
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static List<string> BuildPipelines(CapabilityDebugWorldSnapshot world)
        {
            List<string> pipelines = new List<string>(16);
            Dictionary<string, int> pipelineOrder = new Dictionary<string, int>(16);
            if (world == null)
            {
                return pipelines;
            }

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

        void IHasCustomMenu.AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("返回工具箱"), false, () =>
            {
                CapabilityDebugToolboxWindow.OpenToolbox();
            });
        }
    }
}
