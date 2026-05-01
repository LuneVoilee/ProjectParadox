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
        private float m_LastSampleTime;

        internal void SetSession(CapabilityDebugSession session)
        {
            m_Session = session;
        }

        private Rect m_LayoutRect;

        private const float ToolbarHeight = 22f;
        private const float InspectorHeight = 150f;

        public void OnInternalGUI(Rect layoutRect)
        {
            m_LayoutRect = layoutRect;
            if (m_Session == null || !m_Session.HasFrames)
            {
                GUI.Label(new Rect(0f, 0f, layoutRect.width, 22f),
                    "暂无采样数据，请进入 Play Mode。");
                return;
            }

            CapabilityDebugFrame frame = m_Session.CurrentFrame;
            if (frame == null || frame.Worlds.Count == 0)
            {
                return;
            }

            Rect toolbarRect = new Rect(0f, 0f, layoutRect.width, ToolbarHeight);
            DrawPipelineSelector(toolbarRect, frame);

            float flowchartTop = ToolbarHeight + 2f;
            float flowchartHeight = Mathf.Max(80f, layoutRect.height - flowchartTop - InspectorHeight);
            Rect flowchartRect = new Rect(0f, flowchartTop, layoutRect.width, flowchartHeight);
            DrawFlowchart(frame, flowchartRect);

            Rect inspectorRect = new Rect(0f, flowchartTop + flowchartHeight,
                layoutRect.width, InspectorHeight);
            DrawInspector(frame, inspectorRect);
        }

        private void DrawPipelineSelector(Rect rect, CapabilityDebugFrame frame)
        {
            GUI.BeginGroup(rect, EditorStyles.toolbar);

            Rect labelRect = new Rect(4f, 3f, 54f, 16f);
            GUI.Label(labelRect, "Pipeline:");

            List<string> pipelines = BuildPipelines(frame.Worlds[0]);
            int currentIndex = pipelines.IndexOf(m_SelectedPipeline);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            Rect popupRect = new Rect(60f, 2f, 160f, 18f);
            int newIndex = EditorGUI.Popup(popupRect, currentIndex, pipelines.ToArray(),
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

            Rect frameRect = new Rect(rect.width - 104f, 3f, 100f, 16f);
            GUI.Label(frameRect,
                $"Frame: {Mathf.Max(0, m_Session.CurrentFrameIndex)}");

            GUI.EndGroup();
        }

        private void DrawFlowchart(CapabilityDebugFrame frame, Rect chartRect)
        {
            CapabilityDebugWorldSnapshot world = frame.Worlds[0];
            if (world == null || string.IsNullOrEmpty(m_SelectedPipeline))
            {
                return;
            }

            m_SelectedWorldKey = world.Key;

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
                EditorGUI.HelpBox(new Rect(chartRect.x + 4f, chartRect.y + 4f,
                    chartRect.width - 8f, 28f),
                    $"Pipeline '{m_SelectedPipeline}' 中没有 Cp。", MessageType.Info);
                return;
            }

            caps.Sort((a, b) => a.TickGroupOrder.CompareTo(b.TickGroupOrder));

            float nodeWidth = CapabilityDebugStyles.FlowchartNodeWidth;
            float nodeHeight = CapabilityDebugStyles.FlowchartNodeHeight;
            float spacing = CapabilityDebugStyles.FlowchartNodeSpacing;
            float arrowWidth = CapabilityDebugStyles.FlowchartArrowWidth;
            float totalWidth = caps.Count * (nodeWidth + arrowWidth + spacing) + 60f;

            Rect viewRect = new Rect(0f, 0f, Mathf.Max(totalWidth, chartRect.width - 4f),
                nodeHeight + 40f);
            m_FlowchartScroll = GUI.BeginScrollView(chartRect,
                m_FlowchartScroll, viewRect);

            float nodeY = Mathf.Max(4f, (chartRect.height - nodeHeight) * 0.3f);
            float x = 30f;

            for (int i = 0; i < caps.Count; i++)
            {
                CapabilityDebugCapabilitySnapshot cap = caps[i];
                Rect nodeRect = new Rect(x, nodeY, nodeWidth, nodeHeight);
                DrawFlowchartNode(nodeRect, cap);
                x += nodeWidth;

                if (i < caps.Count - 1)
                {
                    Rect arrowRect = new Rect(x + 4f,
                        nodeY + nodeHeight * 0.5f - 6f, arrowWidth, 14f);
                    DrawArrow(arrowRect);
                    x += arrowWidth + spacing;
                }
            }

            GUI.EndScrollView();

            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0)
            {
                Vector2 mouse = Event.current.mousePosition;
                mouse.x -= chartRect.x;
                mouse.y -= chartRect.y;
                mouse += m_FlowchartScroll;
                x = 30f;
                for (int i = 0; i < caps.Count; i++)
                {
                    Rect nodeRect = new Rect(x, nodeY, nodeWidth, nodeHeight);
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

        private void DrawInspector(CapabilityDebugFrame frame, Rect inspectorRect)
        {
            GUI.Box(inspectorRect, string.Empty, EditorStyles.helpBox);
            Rect innerRect = new Rect(inspectorRect.x + 4f, inspectorRect.y + 2f,
                inspectorRect.width - 8f, inspectorRect.height - 4f);

            if (string.IsNullOrEmpty(m_SelectedCapabilityKey))
            {
                EditorGUI.LabelField(innerRect, "点击节点查看详情",
                    EditorStyles.centeredGreyMiniLabel);
                return;
            }

            CapabilityDebugWorldSnapshot world = frame.FindWorld(m_SelectedWorldKey);
            CapabilityDebugCapabilitySnapshot cap =
                world?.FindGlobalCapability(m_SelectedCapabilityKey);
            if (cap == null)
            {
                m_SelectedCapabilityKey = null;
                return;
            }

            float y = innerRect.y;
            EditorGUI.LabelField(new Rect(innerRect.x, y, innerRect.width, 18f),
                cap.TypeName, EditorStyles.boldLabel);
            y += 18f;

            EditorGUI.LabelField(new Rect(innerRect.x, y, innerRect.width, 16f),
                "Pipeline", cap.Pipeline ?? string.Empty);
            y += 16f;
            EditorGUI.LabelField(new Rect(innerRect.x, y, innerRect.width, 16f),
                "Stage", $"{cap.StageName} ({cap.TickGroupOrder})");
            y += 16f;
            EditorGUI.LabelField(new Rect(innerRect.x, y, innerRect.width, 16f),
                "State", $"{cap.State}  hit:{cap.MatchedEntityCount}");
            y += 16f;
            EditorGUI.LabelField(new Rect(innerRect.x, y, innerRect.width, 16f),
                "LastTick", $"{cap.LastTickMilliseconds:F3} ms");
            y += 16f;

            if (!string.IsNullOrEmpty(cap.LastErrorMessage))
            {
                EditorGUI.LabelField(new Rect(innerRect.x, y, innerRect.width, 16f),
                    "Error", cap.LastErrorMessage);
                y += 16f;
            }

            // Log section
            float logTop = Mathf.Max(y + 2f, inspectorRect.y + 60f);
            Rect logRect = new Rect(innerRect.x, logTop,
                innerRect.width, Mathf.Max(20f, inspectorRect.yMax - logTop - 2f));
            GUI.Box(logRect, "点击节点查看详细日志", EditorStyles.helpBox);
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
            menu.AddItem(new GUIContent("返回工具箱"), false,
                () => { CapabilityDebugToolboxWindow.OpenToolbox(); });
        }
    }
}