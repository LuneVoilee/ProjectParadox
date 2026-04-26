#region

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#endregion

namespace UI.Panel
{
    public class PathIndicatorPanel : BasePanel
    {
        private enum LayerKind
        {
            Shadow,
            Outline,
            Body,
            Highlight
        }

        private sealed class MeshLayer
        {
            public RectTransform rectTransform;
            public CanvasRenderer canvasRenderer;
            public Mesh mesh;
            public LayerKind kind;
        }

        [Header("Path Style")] [SerializeField] private float width = 0.12f;
        [SerializeField] private float outlineWidth = 0.045f;
        [SerializeField] private float arrowLength = 0.46f;
        [SerializeField] private float arrowWidth = 0.34f;

        [Header("Curve Settings")] [SerializeField] private int samplesPerSegment = 10;
        [SerializeField] private float minPointDistance = 0.02f;

        [Header("Palette")] [SerializeField] private Color mainColor = new Color(1f, 0.72f, 0.24f, 0.92f);
        [SerializeField] private Color rimColor = new Color(0.04f, 0.08f, 0.12f, 0.94f);
        [SerializeField] private Color shadowColor = new Color(0.01f, 0.025f, 0.04f, 0.42f);
        [SerializeField] private Color highlightColor = new Color(1f, 0.95f, 0.72f, 0.72f);

        private readonly List<Vector2> filteredPoints = new List<Vector2>();
        private readonly List<Vector2> sampledPoints = new List<Vector2>();
        private readonly List<Vector2> strokePoints = new List<Vector2>();
        private readonly List<Vector3> parentLocalWorldPoints = new List<Vector3>();
        private readonly MeshLayer[] layers = new MeshLayer[4];
        private RectTransform selfRectTransform;

        protected override void Awake()
        {
            base.Awake();

            selfRectTransform = (RectTransform)transform;
            EnsureLayers();
        }

        private void OnEnable()
        {
            if (filteredPoints.Count >= 2)
            {
                RebuildPath();
            }
        }

        private void OnDisable()
        {
            ClearMeshes();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            for (var i = 0; i < layers.Length; i++)
            {
                if (layers[i]?.mesh == null)
                {
                    continue;
                }

                Destroy(layers[i].mesh);
                layers[i].mesh = null;
            }
        }

        public void SetWorldPath(IReadOnlyList<Vector3> worldPoints)
        {
            if (worldPoints == null || worldPoints.Count < 2)
            {
                ClearPath();
                return;
            }

            EnsureReady();
            filteredPoints.Clear();

            if (transform.parent is RectTransform parentRectTransform)
            {
                SetPathRelativeToCanvasParent(worldPoints, parentRectTransform);
                RebuildPath();
                return;
            }

            for (var i = 0; i < worldPoints.Count; i++)
            {
                var localPoint = selfRectTransform.InverseTransformPoint(worldPoints[i]);
                AddFilteredPoint(new Vector2(localPoint.x, localPoint.y));
            }

            RebuildPath();
        }

        public void SetLocalPath(IReadOnlyList<Vector2> localPoints)
        {
            if (localPoints == null || localPoints.Count < 2)
            {
                ClearPath();
                return;
            }

            EnsureReady();
            filteredPoints.Clear();

            for (var i = 0; i < localPoints.Count; i++)
            {
                AddFilteredPoint(localPoints[i]);
            }

            RebuildPath();
        }

        public void SetPalette(Color main, Color rim, Color shadow, Color highlight)
        {
            mainColor = main;
            rimColor = rim;
            shadowColor = shadow;
            highlightColor = highlight;

            if (sampledPoints.Count >= 2)
            {
                RebuildPath();
            }
        }

        public void SetStyle(float width, float outlineWidth, float arrowLength, float arrowWidth)
        {
            this.width = Mathf.Max(0.001f, width);
            this.outlineWidth = Mathf.Max(0f, outlineWidth);
            this.arrowLength = Mathf.Max(this.width, arrowLength);
            this.arrowWidth = Mathf.Max(this.width, arrowWidth);

            if (sampledPoints.Count >= 2)
            {
                RebuildPath();
            }
        }

        public void ClearPath()
        {
            EnsureLayers();

            filteredPoints.Clear();
            sampledPoints.Clear();
            strokePoints.Clear();
            ClearMeshes();
        }

        private void ClearMeshes()
        {
            for (var i = 0; i < layers.Length; i++)
            {
                layers[i]?.mesh.Clear();
                layers[i]?.canvasRenderer.SetMesh(layers[i].mesh);
            }
        }

        private void EnsureReady()
        {
            if (selfRectTransform == null)
            {
                selfRectTransform = (RectTransform)transform;
            }

            EnsureLayers();
        }

        private void EnsureLayers()
        {
            EnsureLayer(0, LayerKind.Shadow);
            EnsureLayer(1, LayerKind.Outline);
            EnsureLayer(2, LayerKind.Body);
            EnsureLayer(3, LayerKind.Highlight);
        }

        private void EnsureLayer(int index, LayerKind kind)
        {
            if (layers[index] != null)
            {
                return;
            }

            var layerObject = new GameObject(kind.ToString(), typeof(RectTransform), typeof(CanvasRenderer));
            layerObject.transform.SetParent(transform, false);
            layerObject.layer = gameObject.layer;

            var layerRect = (RectTransform)layerObject.transform;
            layerRect.anchorMin = Vector2.zero;
            layerRect.anchorMax = Vector2.one;
            layerRect.offsetMin = Vector2.zero;
            layerRect.offsetMax = Vector2.zero;
            layerRect.pivot = new Vector2(0.5f, 0.5f);

            var mesh = new Mesh
            {
                name = $"{nameof(PathIndicatorPanel)}_{kind}_Mesh"
            };
            mesh.MarkDynamic();

            layers[index] = new MeshLayer
            {
                rectTransform = layerRect,
                canvasRenderer = layerObject.GetComponent<CanvasRenderer>(),
                mesh = mesh,
                kind = kind
            };

            layers[index].canvasRenderer.materialCount = 1;
            layers[index].canvasRenderer.SetMaterial(Graphic.defaultGraphicMaterial, 0);
            layers[index].canvasRenderer.SetTexture(Texture2D.whiteTexture);
        }

        private void SetPathRelativeToCanvasParent(
            IReadOnlyList<Vector3> worldPoints,
            RectTransform parentRectTransform)
        {
            parentLocalWorldPoints.Clear();

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            var zSum = 0f;

            for (var i = 0; i < worldPoints.Count; i++)
            {
                var parentLocalPoint = parentRectTransform.InverseTransformPoint(worldPoints[i]);
                parentLocalWorldPoints.Add(parentLocalPoint);

                min = Vector2.Min(min, new Vector2(parentLocalPoint.x, parentLocalPoint.y));
                max = Vector2.Max(max, new Vector2(parentLocalPoint.x, parentLocalPoint.y));
                zSum += parentLocalPoint.z;
            }

            var padding = Mathf.Max(width + outlineWidth * 3f, arrowLength, arrowWidth) * 2f;
            var center2D = (min + max) * 0.5f;
            var centerZ = zSum / parentLocalWorldPoints.Count;

            // 世界路径指示器必须移动到路径所在的 Canvas 局部空间，否则 mesh 会画在 prefab 原本的旧平面上。
            selfRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            selfRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            selfRectTransform.pivot = new Vector2(0.5f, 0.5f);
            selfRectTransform.sizeDelta = new Vector2(
                Mathf.Max(max.x - min.x + padding, padding),
                Mathf.Max(max.y - min.y + padding, padding));
            selfRectTransform.localPosition = new Vector3(center2D.x, center2D.y, centerZ);

            for (var i = 0; i < parentLocalWorldPoints.Count; i++)
            {
                var point = parentLocalWorldPoints[i];
                AddFilteredPoint(new Vector2(point.x - center2D.x, point.y - center2D.y));
            }
        }

        private void AddFilteredPoint(Vector2 point)
        {
            if (filteredPoints.Count > 0)
            {
                var previous = filteredPoints[filteredPoints.Count - 1];
                if ((point - previous).sqrMagnitude < minPointDistance * minPointDistance)
                {
                    return;
                }
            }

            filteredPoints.Add(point);
        }

        private void RebuildPath()
        {
            if (filteredPoints.Count < 2)
            {
                ClearPath();
                return;
            }

            BuildSampledCurve();

            if (sampledPoints.Count < 2)
            {
                ClearPath();
                return;
            }

            var finalDirection = GetFinalDirection();
            var arrowTip = sampledPoints[sampledPoints.Count - 1];
            var arrowBase = arrowTip - finalDirection * Mathf.Min(arrowLength, GetPolylineLength(sampledPoints) * 0.45f);

            BuildStrokePoints(arrowBase);
            BuildLayerMesh(layers[0], width + outlineWidth * 2.8f, arrowWidth + outlineWidth * 4.2f,
                arrowLength + outlineWidth * 2.2f, shadowColor, LayerKind.Shadow, arrowTip, finalDirection);
            BuildLayerMesh(layers[1], width + outlineWidth * 2f, arrowWidth + outlineWidth * 2f,
                arrowLength + outlineWidth * 1.35f, rimColor, LayerKind.Outline, arrowTip, finalDirection);
            BuildLayerMesh(layers[2], width, arrowWidth, arrowLength, mainColor, LayerKind.Body, arrowTip,
                finalDirection);
            BuildLayerMesh(layers[3], width * 0.28f, arrowWidth * 0.26f, arrowLength * 0.62f,
                highlightColor, LayerKind.Highlight, arrowTip, finalDirection);
        }

        private void BuildSampledCurve()
        {
            sampledPoints.Clear();

            if (filteredPoints.Count == 2)
            {
                sampledPoints.Add(filteredPoints[0]);
                sampledPoints.Add(filteredPoints[1]);
                return;
            }

            var segmentSamples = Mathf.Max(2, samplesPerSegment);
            for (var i = 0; i < filteredPoints.Count - 1; i++)
            {
                var p0 = filteredPoints[Mathf.Max(i - 1, 0)];
                var p1 = filteredPoints[i];
                var p2 = filteredPoints[i + 1];
                var p3 = filteredPoints[Mathf.Min(i + 2, filteredPoints.Count - 1)];

                for (var j = 0; j < segmentSamples; j++)
                {
                    if (i > 0 && j == 0)
                    {
                        continue;
                    }

                    var t = j / (float)segmentSamples;
                    sampledPoints.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }

            sampledPoints.Add(filteredPoints[filteredPoints.Count - 1]);
        }

        private void BuildStrokePoints(Vector2 arrowBase)
        {
            strokePoints.Clear();

            if (sampledPoints.Count < 2)
            {
                return;
            }

            strokePoints.Add(sampledPoints[0]);

            for (var i = 1; i < sampledPoints.Count; i++)
            {
                var previous = sampledPoints[i - 1];
                var current = sampledPoints[i];
                var previousToBase = (previous - arrowBase).sqrMagnitude;
                var currentToBase = (current - arrowBase).sqrMagnitude;

                if (Vector2.Dot(current - previous, arrowBase - previous) >= 0f &&
                    currentToBase <= previousToBase)
                {
                    strokePoints.Add(current);
                    continue;
                }

                var segment = current - previous;
                var segmentLength = segment.magnitude;
                if (segmentLength > Mathf.Epsilon)
                {
                    var t = Mathf.Clamp01(Vector2.Dot(arrowBase - previous, segment) / (segmentLength * segmentLength));
                    strokePoints.Add(Vector2.Lerp(previous, current, t));
                }

                break;
            }

            if (strokePoints.Count < 2)
            {
                strokePoints.Add(arrowBase);
            }
        }

        private void BuildLayerMesh(
            MeshLayer layer,
            float layerWidth,
            float layerArrowWidth,
            float layerArrowLength,
            Color baseColor,
            LayerKind kind,
            Vector2 arrowTip,
            Vector2 finalDirection)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            AppendRibbon(vertices, colors, triangles, strokePoints, layerWidth, baseColor, kind);
            AppendArrow(vertices, colors, triangles, arrowTip, finalDirection, layerArrowLength, layerArrowWidth,
                baseColor, kind);

            layer.mesh.Clear();
            layer.mesh.SetVertices(vertices);
            layer.mesh.SetColors(colors);
            layer.mesh.SetTriangles(triangles, 0);
            layer.mesh.RecalculateBounds();
            layer.canvasRenderer.SetMesh(layer.mesh);
        }

        private void AppendRibbon(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            List<Vector2> points,
            float ribbonWidth,
            Color baseColor,
            LayerKind kind)
        {
            if (points.Count < 2)
            {
                return;
            }

            var startIndex = vertices.Count;
            var halfWidth = ribbonWidth * 0.5f;
            var pointCount = points.Count;

            for (var i = 0; i < pointCount; i++)
            {
                var tangent = GetPointDirection(points, i);
                var normal = new Vector2(-tangent.y, tangent.x);
                var progress = pointCount <= 1 ? 1f : i / (float)(pointCount - 1);
                var color = EvaluateLayerColor(baseColor, progress, kind, false);

                vertices.Add(points[i] + normal * halfWidth);
                vertices.Add(points[i] - normal * halfWidth);
                colors.Add(color);
                colors.Add(color);
            }

            for (var i = 0; i < pointCount - 1; i++)
            {
                var a = startIndex + i * 2;
                var b = a + 1;
                var c = a + 2;
                var d = a + 3;

                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(d);
                triangles.Add(b);
            }
        }

        private void AppendArrow(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 arrowTip,
            Vector2 direction,
            float layerArrowLength,
            float layerArrowWidth,
            Color baseColor,
            LayerKind kind)
        {
            var startIndex = vertices.Count;
            var normal = new Vector2(-direction.y, direction.x);
            var baseCenter = arrowTip - direction * layerArrowLength;
            var backInset = baseCenter + direction * Mathf.Min(layerArrowLength * 0.28f, layerArrowLength - 0.001f);
            var halfWidth = layerArrowWidth * 0.5f;

            var baseColorBack = EvaluateLayerColor(baseColor, 0.78f, kind, true);
            var baseColorTip = EvaluateLayerColor(baseColor, 1f, kind, true);

            vertices.Add(arrowTip);
            vertices.Add(baseCenter + normal * halfWidth);
            vertices.Add(backInset);
            vertices.Add(baseCenter - normal * halfWidth);

            colors.Add(baseColorTip);
            colors.Add(baseColorBack);
            colors.Add(baseColorBack);
            colors.Add(baseColorBack);

            triangles.Add(startIndex);
            triangles.Add(startIndex + 1);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 3);
        }

        private Vector2 GetFinalDirection()
        {
            for (var i = sampledPoints.Count - 1; i > 0; i--)
            {
                var direction = sampledPoints[i] - sampledPoints[i - 1];
                if (direction.sqrMagnitude > Mathf.Epsilon)
                {
                    return direction.normalized;
                }
            }

            return Vector2.right;
        }

        private static Vector2 GetPointDirection(List<Vector2> points, int index)
        {
            if (points.Count < 2)
            {
                return Vector2.right;
            }

            Vector2 direction;
            if (index == 0)
            {
                direction = points[1] - points[0];
            }
            else if (index == points.Count - 1)
            {
                direction = points[index] - points[index - 1];
            }
            else
            {
                direction = points[index + 1] - points[index - 1];
            }

            return direction.sqrMagnitude <= Mathf.Epsilon ? Vector2.right : direction.normalized;
        }

        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            var t2 = t * t;
            var t3 = t2 * t;

            return 0.5f * ((2f * p1) +
                           (-p0 + p2) * t +
                           (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                           (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static float GetPolylineLength(List<Vector2> points)
        {
            var length = 0f;
            for (var i = 1; i < points.Count; i++)
            {
                length += Vector2.Distance(points[i - 1], points[i]);
            }

            return length;
        }

        private static Color EvaluateLayerColor(Color color, float progress, LayerKind kind, bool arrow)
        {
            var alpha = color.a;
            var value = 1f;

            switch (kind)
            {
                case LayerKind.Shadow:
                    alpha *= arrow ? 1.08f : Mathf.Lerp(0.72f, 1f, progress);
                    value = 0.88f;
                    break;
                case LayerKind.Outline:
                    alpha *= arrow ? 1f : Mathf.Lerp(0.82f, 1f, progress);
                    value = Mathf.Lerp(0.82f, 1.04f, progress);
                    break;
                case LayerKind.Body:
                    alpha *= arrow ? 1f : Mathf.Lerp(0.62f, 1f, progress);
                    value = Mathf.Lerp(0.72f, 1.12f, progress);
                    break;
                case LayerKind.Highlight:
                    alpha *= arrow ? 0.92f : Mathf.Lerp(0.18f, 0.78f, progress);
                    value = Mathf.Lerp(0.88f, 1.18f, progress);
                    break;
            }

            return new Color(
                Mathf.Clamp01(color.r * value),
                Mathf.Clamp01(color.g * value),
                Mathf.Clamp01(color.b * value),
                Mathf.Clamp01(alpha));
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            width = Mathf.Max(0.001f, width);
            outlineWidth = Mathf.Max(0f, outlineWidth);
            arrowLength = Mathf.Max(width, arrowLength);
            arrowWidth = Mathf.Max(width, arrowWidth);
            samplesPerSegment = Mathf.Max(2, samplesPerSegment);
            minPointDistance = Mathf.Max(0.001f, minPointDistance);
        }
#endif
    }
}
