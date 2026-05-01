using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Core.Capability.Editor
{
    internal sealed class CapabilityEvidenceExportRequest
    {
        public string Description;
        public string ReproSteps;
        public string Expected;
        public int StartFrame;
        public int EndFrame;
        public int MarkedFrame = -1;
        public bool FollowTouchedEntities = true;
        public bool IncludeTransforms = true;
        public readonly List<int> EntityIds = new List<int>(16);
        public readonly List<string> Pipelines = new List<string>(8);
    }

    internal static class CapabilityEvidenceExporter
    {
        public static bool Export
        (
            CapabilityDebugSession session,
            CapabilityEvidenceExportRequest request,
            out string jsonlPath,
            out string markdownPath,
            out string error
        )
        {
            jsonlPath = null;
            markdownPath = null;
            error = null;

            if (session == null || !session.HasFrames)
            {
                error = "No debug session frames to export.";
                return false;
            }

            request ??= new CapabilityEvidenceExportRequest();
            int start = Mathf.Clamp(request.StartFrame, 0, session.FrameCount - 1);
            int end = Mathf.Clamp(request.EndFrame <= 0 ? session.FrameCount - 1 : request.EndFrame,
                start, session.FrameCount - 1);

            try
            {
                string dir = Path.GetFullPath(Path.Combine(
                    Application.dataPath, "..", "Temp", "CapabilityEvidence"));
                Directory.CreateDirectory(dir);

                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss",
                    CultureInfo.InvariantCulture);
                jsonlPath = Path.Combine(dir, $"capability_evidence_{stamp}.jsonl");
                markdownPath = Path.Combine(dir, $"capability_evidence_{stamp}.md");

                File.WriteAllText(jsonlPath, BuildJsonl(session, request, start, end),
                    Encoding.UTF8);
                File.WriteAllText(markdownPath, BuildMarkdown(request, start, end, jsonlPath),
                    Encoding.UTF8);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.ToString();
                return false;
            }
        }

        private static string BuildJsonl
        (
            CapabilityDebugSession session,
            CapabilityEvidenceExportRequest request,
            int start,
            int end
        )
        {
            var builder = new StringBuilder(1024 * 32);
            var lastValues = new Dictionary<string, string>(4096);
            var watchedIds = new HashSet<int>(request.EntityIds);
            var pipelines = new HashSet<string>(request.Pipelines);
            ExpandWatchlist(session, request, start, end, watchedIds, pipelines);
            CapabilityDebugFrame previousFrame = null;

            for (int frameIndex = start; frameIndex <= end; frameIndex++)
            {
                CapabilityDebugFrame frame = session.GetFrame(frameIndex);
                if (frame == null)
                {
                    continue;
                }

                double dt = previousFrame == null
                    ? 0d
                    : frame.RealtimeSinceStartup - previousFrame.RealtimeSinceStartup;
                ExportFrame(builder, frame, previousFrame, dt, request, watchedIds,
                    pipelines, lastValues);
                previousFrame = frame;
            }

            return builder.ToString();
        }

        private static void ExpandWatchlist
        (
            CapabilityDebugSession session,
            CapabilityEvidenceExportRequest request,
            int start,
            int end,
            HashSet<int> watchedIds,
            HashSet<string> pipelines
        )
        {
            if (!request.FollowTouchedEntities)
            {
                return;
            }

            if (watchedIds.Count == 0 && pipelines.Count == 0)
            {
                return;
            }

            for (int pass = 0; pass < 3; pass++)
            {
                bool changed = false;
                for (int frameIndex = start; frameIndex <= end; frameIndex++)
                {
                    CapabilityDebugFrame frame = session.GetFrame(frameIndex);
                    if (frame == null)
                    {
                        continue;
                    }

                    changed |= ExpandFromFrame(frame, watchedIds, pipelines);
                }

                if (!changed)
                {
                    break;
                }
            }
        }

        private static bool ExpandFromFrame
        (
            CapabilityDebugFrame frame,
            HashSet<int> watchedIds,
            HashSet<string> pipelines
        )
        {
            bool changed = false;
            for (int worldIndex = 0; worldIndex < frame.Worlds.Count; worldIndex++)
            {
                CapabilityDebugWorldSnapshot world = frame.Worlds[worldIndex];
                for (int capabilityIndex = 0;
                     capabilityIndex < world.GlobalCapabilities.Count;
                     capabilityIndex++)
                {
                    CapabilityDebugCapabilitySnapshot capability =
                        world.GlobalCapabilities[capabilityIndex];
                    bool includeCapability =
                        pipelines.Contains(capability.Pipeline) ||
                        HasAnyMatchedId(capability, watchedIds);
                    if (!includeCapability)
                    {
                        continue;
                    }

                    for (int idIndex = 0;
                         idIndex < capability.MatchedEntityIds.Count;
                         idIndex++)
                    {
                        changed |= watchedIds.Add(capability.MatchedEntityIds[idIndex]);
                    }
                }

                for (int entityIndex = 0; entityIndex < world.Entities.Count; entityIndex++)
                {
                    CapabilityDebugEntitySnapshot entity = world.Entities[entityIndex];
                    if (!watchedIds.Contains(entity.EntityId))
                    {
                        continue;
                    }

                    for (int componentIndex = 0;
                         componentIndex < entity.Components.Count;
                         componentIndex++)
                    {
                        changed |= ExpandFromValues(entity.Components[componentIndex].Fields,
                            string.Empty, watchedIds);
                    }
                }
            }

            for (int traceIndex = 0; traceIndex < frame.Traces.Count; traceIndex++)
            {
                CapabilityDebugTraceSnapshot trace = frame.Traces[traceIndex];
                if (trace.EntityId >= 0 &&
                    (IsTraceTouchEvent(trace.Event) || watchedIds.Contains(trace.EntityId)))
                {
                    changed |= watchedIds.Add(trace.EntityId);
                }

                if (LooksLikeEntityIdPath(trace.Path))
                {
                    changed |= AddIdsFromText(trace.Value, watchedIds);
                }
            }

            return changed;
        }

        private static bool HasAnyMatchedId
        (
            CapabilityDebugCapabilitySnapshot capability,
            HashSet<int> watchedIds
        )
        {
            if (watchedIds.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < capability.MatchedEntityIds.Count; i++)
            {
                if (watchedIds.Contains(capability.MatchedEntityIds[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ExpandFromValues
        (
            List<CapabilityDebugValueSnapshot> values,
            string parentPath,
            HashSet<int> watchedIds
        )
        {
            bool changed = false;
            for (int i = 0; i < values.Count; i++)
            {
                CapabilityDebugValueSnapshot value = values[i];
                string path = string.IsNullOrEmpty(parentPath)
                    ? value.Name
                    : parentPath + "." + value.Name;
                if (LooksLikeEntityIdPath(path))
                {
                    changed |= AddIdsFromText(value.DisplayValue, watchedIds);
                }

                changed |= ExpandFromValues(value.Children, path, watchedIds);
            }

            return changed;
        }

        private static bool IsTraceTouchEvent(string eventName)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                return false;
            }

            return eventName.StartsWith("command", StringComparison.Ordinal) ||
                   eventName.StartsWith("entity", StringComparison.Ordinal) ||
                   eventName.StartsWith("component", StringComparison.Ordinal);
        }

        private static bool LooksLikeEntityIdPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            return path.IndexOf("EntityId", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("EntityIds", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.EndsWith(".Id", StringComparison.OrdinalIgnoreCase);
        }

        private static bool AddIdsFromText(string text, HashSet<int> watchedIds)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            bool changed = false;
            int current = -1;
            bool reading = false;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch >= '0' && ch <= '9')
                {
                    current = reading ? current * 10 + (ch - '0') : ch - '0';
                    reading = true;
                    continue;
                }

                if (reading)
                {
                    changed |= watchedIds.Add(current);
                    current = -1;
                    reading = false;
                }
            }

            if (reading)
            {
                changed |= watchedIds.Add(current);
            }

            return changed;
        }

        private static void ExportFrame
        (
            StringBuilder builder,
            CapabilityDebugFrame frame,
            CapabilityDebugFrame previousFrame,
            double dt,
            CapabilityEvidenceExportRequest request,
            HashSet<int> watchedIds,
            HashSet<string> pipelines,
            Dictionary<string, string> lastValues
        )
        {
            // 预先收集 capability.delta 覆盖的 (entity, path) 对，用来跳过去重。
            HashSet<string> cpDeltaCovered = BuildCpDeltaCoveredSet(frame);

            for (int worldIndex = 0; worldIndex < frame.Worlds.Count; worldIndex++)
            {
                CapabilityDebugWorldSnapshot world = frame.Worlds[worldIndex];
                CapabilityDebugWorldSnapshot previousWorld =
                    previousFrame?.FindWorld(world.Key);

                ExportEntities(builder, frame, world, previousWorld, dt, request,
                    watchedIds, lastValues, cpDeltaCovered);
                ExportCapabilities(builder, frame, world, dt, watchedIds, pipelines,
                    lastValues);
            }

            ExportTraces(builder, frame, dt, request, watchedIds, pipelines);
        }

        private static HashSet<string> BuildCpDeltaCoveredSet(CapabilityDebugFrame frame)
        {
            HashSet<string> covered = new HashSet<string>();
            for (int i = 0; i < frame.Traces.Count; i++)
            {
                CapabilityDebugTraceSnapshot trace = frame.Traces[i];
                if (trace.Event == "capability.delta" && trace.EntityId >= 0)
                {
                    covered.Add($"{trace.EntityId}/{trace.Path}");
                }
            }

            return covered;
        }

        private static void ExportEntities
        (
            StringBuilder builder,
            CapabilityDebugFrame frame,
            CapabilityDebugWorldSnapshot world,
            CapabilityDebugWorldSnapshot previousWorld,
            double dt,
            CapabilityEvidenceExportRequest request,
            HashSet<int> watchedIds,
            Dictionary<string, string> lastValues,
            HashSet<string> cpDeltaCovered
        )
        {
            for (int entityIndex = 0; entityIndex < world.Entities.Count; entityIndex++)
            {
                CapabilityDebugEntitySnapshot entity = world.Entities[entityIndex];
                if (!ShouldIncludeEntity(entity.EntityId, watchedIds))
                {
                    continue;
                }

                CapabilityDebugEntitySnapshot previousEntity =
                    previousWorld?.FindEntity(entity.Key);
                ExportEntityLifecycle(builder, frame, world, entity, previousEntity, dt);
                ExportComponentFields(builder, frame, world, entity, dt, lastValues,
                    cpDeltaCovered);
                if (request.IncludeTransforms)
                {
                    ExportTransformFields(builder, frame, world, entity, dt, lastValues,
                        cpDeltaCovered);
                }
            }

            if (previousWorld == null)
            {
                return;
            }

            for (int entityIndex = 0; entityIndex < previousWorld.Entities.Count; entityIndex++)
            {
                CapabilityDebugEntitySnapshot previousEntity =
                    previousWorld.Entities[entityIndex];
                if (!ShouldIncludeEntity(previousEntity.EntityId, watchedIds))
                {
                    continue;
                }

                if (world.FindEntity(previousEntity.Key) != null)
                {
                    continue;
                }

                AppendLine(builder, frame, dt, world.DisplayName, previousEntity.EntityId,
                    null, "entity.removed", "entity", null, previousEntity.DisplayName);
            }
        }

        private static void ExportEntityLifecycle
        (
            StringBuilder builder,
            CapabilityDebugFrame frame,
            CapabilityDebugWorldSnapshot world,
            CapabilityDebugEntitySnapshot entity,
            CapabilityDebugEntitySnapshot previousEntity,
            double dt
        )
        {
            if (previousEntity == null)
            {
                AppendLine(builder, frame, dt, world.DisplayName, entity.EntityId,
                    null, "entity.added", "entity", entity.DisplayName, null);
                return;
            }

            for (int i = 0; i < entity.Components.Count; i++)
            {
                CapabilityDebugComponentSnapshot component = entity.Components[i];
                if (previousEntity.FindComponent(component.Key) == null)
                {
                    AppendLine(builder, frame, dt, world.DisplayName, entity.EntityId,
                        null, "component.added", component.TypeFullName,
                        component.TypeName, null);
                }
            }

            for (int i = 0; i < previousEntity.Components.Count; i++)
            {
                CapabilityDebugComponentSnapshot component = previousEntity.Components[i];
                if (entity.FindComponent(component.Key) == null)
                {
                    AppendLine(builder, frame, dt, world.DisplayName, entity.EntityId,
                        null, "component.removed", component.TypeFullName,
                        null, component.TypeName);
                }
            }
        }

        private static void ExportComponentFields
        (
            StringBuilder builder,
            CapabilityDebugFrame frame,
            CapabilityDebugWorldSnapshot world,
            CapabilityDebugEntitySnapshot entity,
            double dt,
            Dictionary<string, string> lastValues,
            HashSet<string> cpDeltaCovered
        )
        {
            for (int i = 0; i < entity.Components.Count; i++)
            {
                CapabilityDebugComponentSnapshot component = entity.Components[i];
                ExportValues(builder, frame, dt, world.DisplayName, entity.EntityId,
                    null, $"comp.{component.TypeFullName}", component.Fields,
                    lastValues, $"w:{world.Key}/e:{entity.EntityId}/c:{component.Key}",
                    cpDeltaCovered);
            }
        }

        private static void ExportTransformFields
        (
            StringBuilder builder,
            CapabilityDebugFrame frame,
            CapabilityDebugWorldSnapshot world,
            CapabilityDebugEntitySnapshot entity,
            double dt,
            Dictionary<string, string> lastValues,
            HashSet<string> cpDeltaCovered
        )
        {
            for (int i = 0; i < entity.Transforms.Count; i++)
            {
                CapabilityDebugTransformSnapshot transform = entity.Transforms[i];
                ExportValue(builder, frame, dt, world.DisplayName, entity.EntityId, null,
                    $"tf.{transform.InstanceId}.activeSelf",
                    transform.ActiveSelf.ToString(), lastValues,
                    $"w:{world.Key}/e:{entity.EntityId}/tf:{transform.InstanceId}/active",
                    cpDeltaCovered);
                ExportValue(builder, frame, dt, world.DisplayName, entity.EntityId, null,
                    $"tf.{transform.InstanceId}.position",
                    transform.Position.ToString("F3"), lastValues,
                    $"w:{world.Key}/e:{entity.EntityId}/tf:{transform.InstanceId}/pos",
                    cpDeltaCovered);
                ExportValue(builder, frame, dt, world.DisplayName, entity.EntityId, null,
                    $"tf.{transform.InstanceId}.localPosition",
                    transform.LocalPosition.ToString("F3"), lastValues,
                    $"w:{world.Key}/e:{entity.EntityId}/tf:{transform.InstanceId}/lpos",
                    cpDeltaCovered);
                ExportValue(builder, frame, dt, world.DisplayName, entity.EntityId, null,
                    $"tf.{transform.InstanceId}.rotation",
                    transform.Rotation.eulerAngles.ToString("F3"), lastValues,
                    $"w:{world.Key}/e:{entity.EntityId}/tf:{transform.InstanceId}/rot",
                    cpDeltaCovered);
                ExportValue(builder, frame, dt, world.DisplayName, entity.EntityId, null,
                    $"tf.{transform.InstanceId}.localScale",
                    transform.LocalScale.ToString("F3"), lastValues,
                    $"w:{world.Key}/e:{entity.EntityId}/tf:{transform.InstanceId}/scale",
                    cpDeltaCovered);
            }
        }

        private static void ExportCapabilities
        (
            StringBuilder builder,
            CapabilityDebugFrame frame,
            CapabilityDebugWorldSnapshot world,
            double dt,
            HashSet<int> watchedIds,
            HashSet<string> pipelines,
            Dictionary<string, string> lastValues
        )
        {
            for (int i = 0; i < world.GlobalCapabilities.Count; i++)
            {
                CapabilityDebugCapabilitySnapshot capability = world.GlobalCapabilities[i];
                if (!ShouldIncludeCapability(capability, watchedIds, pipelines))
                {
                    continue;
                }

                string baseKey = $"w:{world.Key}/cap:{capability.Key}";
                ExportValue(builder, frame, dt, world.DisplayName, null, capability.TypeFullName,
                    "cap.state", capability.State.ToString(), lastValues, $"{baseKey}/state",
                    null);
                if (!string.IsNullOrEmpty(capability.LastErrorMessage))
                {
                    AppendLine(builder, frame, dt, world.DisplayName, null,
                        capability.TypeFullName, "error", "cap.error",
                        capability.LastErrorMessage, null);
                }

                for (int logIndex = 0; logIndex < capability.Logs.Count; logIndex++)
                {
                    CapabilityDebugLogSnapshot log = capability.Logs[logIndex];
                    AppendLine(builder, frame, dt, world.DisplayName, null,
                        capability.TypeFullName, "log", "cap.log", log.Message, null);
                }
            }
        }

        private static void ExportTraces
        (
            StringBuilder builder,
            CapabilityDebugFrame frame,
            double dt,
            CapabilityEvidenceExportRequest request,
            HashSet<int> watchedIds,
            HashSet<string> pipelines
        )
        {
            for (int i = 0; i < frame.Traces.Count; i++)
            {
                CapabilityDebugTraceSnapshot trace = frame.Traces[i];
                if (!ShouldIncludeTrace(trace, request, watchedIds, pipelines))
                {
                    continue;
                }

                AppendLine(builder, frame, dt, trace.WorldName, trace.EntityId >= 0
                        ? trace.EntityId
                        : null, trace.CapabilityType, trace.Event, trace.Path,
                    trace.Value, trace.Prev, trace.Pipeline);
            }
        }

        private static void ExportValues
        (
            StringBuilder builder,
            CapabilityDebugFrame frame,
            double dt,
            string world,
            int? entityId,
            string capability,
            string prefix,
            List<CapabilityDebugValueSnapshot> values,
            Dictionary<string, string> lastValues,
            string keyPrefix,
            HashSet<string> cpDeltaCovered
        )
        {
            for (int i = 0; i < values.Count; i++)
            {
                ExportValueNode(builder, frame, dt, world, entityId, capability,
                    prefix, values[i], lastValues, keyPrefix, cpDeltaCovered);
            }
        }

        private static void ExportValueNode
        (
            StringBuilder builder,
            CapabilityDebugFrame frame,
            double dt,
            string world,
            int? entityId,
            string capability,
            string prefix,
            CapabilityDebugValueSnapshot value,
            Dictionary<string, string> lastValues,
            string keyPrefix,
            HashSet<string> cpDeltaCovered
        )
        {
            string path = $"{prefix}.{value.Name}";
            string key = $"{keyPrefix}/{value.Name}";
            ExportValue(builder, frame, dt, world, entityId, capability, path,
                value.DisplayValue, lastValues, key, cpDeltaCovered);

            for (int i = 0; i < value.Children.Count; i++)
            {
                ExportValueNode(builder, frame, dt, world, entityId, capability,
                    path, value.Children[i], lastValues, key, cpDeltaCovered);
            }
        }

        private static void ExportValue
        (
            StringBuilder builder,
            CapabilityDebugFrame frame,
            double dt,
            string world,
            int? entityId,
            string capability,
            string path,
            string value,
            Dictionary<string, string> lastValues,
            string key,
            HashSet<string> cpDeltaCovered
        )
        {
            bool isCoveredByCp = entityId.HasValue &&
                cpDeltaCovered != null &&
                cpDeltaCovered.Contains($"{entityId.Value}/{path}");

            if (!lastValues.TryGetValue(key, out string previous))
            {
                lastValues[key] = value;
                if (isCoveredByCp)
                {
                    return;
                }

                AppendLine(builder, frame, dt, world, entityId, capability,
                    "baseline", path, value, null);
                return;
            }

            if (previous == value)
            {
                return;
            }

            lastValues[key] = value;
            if (isCoveredByCp)
            {
                return;
            }

            AppendLine(builder, frame, dt, world, entityId, capability,
                "delta", path, value, previous);
        }

        private static bool ShouldIncludeEntity(int entityId, HashSet<int> watchedIds)
        {
            return watchedIds.Count == 0 || watchedIds.Contains(entityId);
        }

        private static bool ShouldIncludeCapability
        (
            CapabilityDebugCapabilitySnapshot capability,
            HashSet<int> watchedIds,
            HashSet<string> pipelines
        )
        {
            if (pipelines.Count > 0 && pipelines.Contains(capability.Pipeline))
            {
                return true;
            }

            if (watchedIds.Count == 0)
            {
                return pipelines.Count == 0;
            }

            for (int i = 0; i < capability.MatchedEntityIds.Count; i++)
            {
                if (watchedIds.Contains(capability.MatchedEntityIds[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldIncludeTrace
        (
            CapabilityDebugTraceSnapshot trace,
            CapabilityEvidenceExportRequest request,
            HashSet<int> watchedIds,
            HashSet<string> pipelines
        )
        {
            if (watchedIds.Count == 0 && pipelines.Count == 0)
            {
                return true;
            }

            if (trace.EntityId >= 0 && watchedIds.Contains(trace.EntityId))
            {
                return true;
            }

            return request.FollowTouchedEntities && trace.Event != "capability.delta";
        }

        private static void AppendLine
        (
            StringBuilder builder,
            CapabilityDebugFrame frame,
            double dt,
            string world,
            int? entityId,
            string capability,
            string eventName,
            string path,
            string value,
            string previous,
            string pipeline = null
        )
        {
            builder.Append('{');
            AppendJson(builder, "f", frame.FrameIndex, false);
            AppendJson(builder, "t", frame.RealtimeSinceStartup.ToString("F4",
                CultureInfo.InvariantCulture));
            AppendJson(builder, "dt", dt.ToString("F4", CultureInfo.InvariantCulture));
            AppendJson(builder, "world", world);
            AppendJson(builder, "entity", entityId.HasValue ? entityId.Value.ToString() : null);
            AppendJson(builder, "capability", capability);
            AppendJson(builder, "event", eventName);
            if (!string.IsNullOrEmpty(pipeline))
            {
                AppendJson(builder, "pipeline", pipeline);
            }
            AppendJson(builder, "path", path);
            AppendJson(builder, "value", value);
            AppendJson(builder, "prev", previous);
            builder.AppendLine("}");
        }

        private static void AppendJson
            (StringBuilder builder, string name, int value, bool comma = true)
        {
            if (comma)
            {
                builder.Append(',');
            }

            builder.Append('"').Append(name).Append("\":").Append(value);
        }

        private static void AppendJson
            (StringBuilder builder, string name, string value, bool comma = true)
        {
            if (comma)
            {
                builder.Append(',');
            }

            builder.Append('"').Append(name).Append("\":");
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append('"').Append(Escape(value)).Append('"');
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value ?? string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        private static string BuildMarkdown
        (
            CapabilityEvidenceExportRequest request,
            int start,
            int end,
            string jsonlPath
        )
        {
            var builder = new StringBuilder(4096);
            builder.AppendLine("# Capability AI Evidence");
            builder.AppendLine();
            builder.AppendLine($"- JSONL: `{jsonlPath}`");
            builder.AppendLine($"- Frames: {start} - {end}");
            builder.AppendLine($"- Marked anomaly frame: {request.MarkedFrame}");
            builder.AppendLine($"- Entity watchlist: {string.Join(", ", request.EntityIds)}");
            builder.AppendLine($"- Capability pipelines: {string.Join(", ", request.Pipelines)}");
            builder.AppendLine($"- Follow touched entities: {request.FollowTouchedEntities}");
            builder.AppendLine();
            builder.AppendLine("## Description");
            builder.AppendLine(request.Description ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("## Repro Steps");
            builder.AppendLine(request.ReproSteps ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("## Expected");
            builder.AppendLine(request.Expected ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("## AI Debug Prompt");
            builder.AppendLine("请把 JSONL 当作原始轨迹证据读取。重点比较 baseline/delta/command/capability.delta/log/error 事件，按时间顺序找出最早异常因果链，并给出可验证的代码定位假设。");
            return builder.ToString();
        }
    }
}
