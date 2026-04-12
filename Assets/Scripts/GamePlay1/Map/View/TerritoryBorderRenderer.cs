using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using Map.Common;
using Map.Data;
using Map.Settings;

namespace Map.View
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class TerritoryBorderRenderer : MonoBehaviour
    {
        [Header("Links")]
        [SerializeField] private Tilemap m_Tilemap;
        [SerializeField] private HexMapRenderer m_MapRenderer;
        [SerializeField] private TerritorySettings m_TerritorySettings;
        [SerializeField] private Material m_Material;

        [Header("Visual")]
        [SerializeField, Range(0.001f, 0.4f)] private float m_BorderWidth = 0.04f;
        [SerializeField, Range(0f, 1f)] private float m_FillStrength = 0.2f;
        [SerializeField, Range(0f, 0.5f)] private float m_InnerGlowWidth = 0.15f;
        [SerializeField, Range(0f, 2f)] private float m_GlowStrength = 0.6f;

        [Header("Sorting")]
        [SerializeField] private bool m_CopySortingFromTilemap = true;
        [SerializeField] private string m_SortingLayer = "Default";
        [SerializeField] private int m_SortingOrder = 1;

        [Header("Debug")]
        [SerializeField] private bool m_LogBuildSummary;

        private MeshFilter m_MeshFilter;
        private MeshRenderer m_MeshRenderer;
        private Mesh m_Mesh;
        private Material m_RuntimeMaterial;

        private byte[] m_Owners;
        private int m_MapWidth;
        private int m_MapHeight;
        private int m_GhostColumns;
        private bool m_MeshDirty;

        private readonly List<Vector3> m_Vertices = new();
        private readonly List<Color32> m_Colors = new();
        private readonly List<int> m_Indices = new();

        public int GhostColumns => m_GhostColumns;
        public int MapWidth => m_MapWidth;
        public int MapHeight => m_MapHeight;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnDestroy()
        {
            ReleaseMesh();
            ReleaseRuntimeMaterial();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (m_MeshRenderer != null)
            {
                ApplySorting();
            }

            m_MeshDirty = true;
        }
#endif

        public void Render(GridData data)
        {
            if (data == null || m_Tilemap == null)
            {
                return;
            }

            CacheComponents();

            m_MapWidth = data.Width;
            m_MapHeight = data.Height;
            m_GhostColumns = m_MapRenderer != null ? m_MapRenderer.GhostColumns : 0;

            EnsureOwnerBuffer(m_MapWidth, m_MapHeight);
            FillOwnerBuffer(data);

            RebuildMesh();
            ApplyMaterial();
            ApplySorting();
            m_MeshDirty = false;
        }

        public void SetOwner(int col, int row, byte ownerId)
        {
            if (m_Owners == null || m_Owners.Length == 0)
            {
                return;
            }

            if (row < 0 || row >= m_MapHeight || col < 0 || col >= m_MapWidth)
            {
                return;
            }

            int index = col + row * m_MapWidth;
            if (m_Owners[index] == ownerId)
            {
                return;
            }

            m_Owners[index] = ownerId;
            m_MeshDirty = true;
        }

        public void ApplyChanges()
        {
            if (!m_MeshDirty)
            {
                return;
            }

            RebuildMesh();
            ApplyMaterial();
            m_MeshDirty = false;
        }

        private void CacheComponents()
        {
            if (m_MeshFilter == null)
            {
                m_MeshFilter = GetComponent<MeshFilter>();
            }

            if (m_MeshRenderer == null)
            {
                m_MeshRenderer = GetComponent<MeshRenderer>();
            }
        }

        private void EnsureOwnerBuffer(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                m_Owners = null;
                return;
            }

            int size = width * height;
            if (m_Owners == null || m_Owners.Length != size)
            {
                m_Owners = new byte[size];
            }
        }

        private void FillOwnerBuffer(GridData data)
        {
            if (m_Owners == null || data == null)
            {
                return;
            }

            int index = 0;
            for (int row = 0; row < m_MapHeight; row++)
            {
                for (int col = 0; col < m_MapWidth; col++)
                {
                    var cell = data.GetCell(col, row);
                    m_Owners[index++] = cell == null ? (byte)0 : cell.OwnerId;
                }
            }
        }

        private void RebuildMesh()
        {
            if (m_Tilemap == null || m_MapWidth <= 0 || m_MapHeight <= 0 || m_Owners == null)
            {
                ClearMesh();
                return;
            }

            EnsureMesh();

            m_Vertices.Clear();
            m_Colors.Clear();
            m_Indices.Clear();

            var cellSize = m_Tilemap.cellSize;
            float halfWidth = cellSize.x * 0.5f;
            float halfHeight = cellSize.y * 0.5f;

            // Pointy-top hexagon vertices relative to center
            Vector2 top = new Vector2(0f, halfHeight);
            Vector2 upperRight = new Vector2(halfWidth, halfHeight * 0.5f);
            Vector2 lowerRight = new Vector2(halfWidth, -halfHeight * 0.5f);
            Vector2 bottom = new Vector2(0f, -halfHeight);
            Vector2 lowerLeft = new Vector2(-halfWidth, -halfHeight * 0.5f);
            Vector2 upperLeft = new Vector2(-halfWidth, halfHeight * 0.5f);

            // Map HexDirection to visual edge start/end vertices
            // Direction Order: NE, E, SE, SW, W, NW
            var edgeStarts = new[] { top, upperRight, lowerRight, bottom, lowerLeft, upperLeft };
            var edgeEnds = new[] { upperRight, lowerRight, bottom, lowerLeft, upperLeft, top };

            float borderThickness = Mathf.Max(0.001f, m_BorderWidth) * cellSize.y;
            
            int cellCount = 0;
            var directions = (HexDirection[])Enum.GetValues(typeof(HexDirection));

            for (int row = 0; row < m_MapHeight; row++)
            {
                for (int col = -m_GhostColumns; col < m_MapWidth + m_GhostColumns; col++)
                {
                    byte owner = GetOwnerWrapped(col, row);
                    if (owner == 0)
                    {
                        continue;
                    }

                    var center = m_Tilemap.GetCellCenterLocal(new Vector3Int(col, row, 0));
                    Color32 baseColor = ResolveOwnerColor(owner);
                    
                    // A. Draw Base Fill
                    AddBaseFill(center, edgeStarts, edgeEnds, baseColor);

                    bool hasEdge = false;
                    HexCoordinates currentCoords = HexCoordinates.FromOffset(col, row);

                    for (int i = 0; i < directions.Length; i++)
                    {
                        HexDirection dir = directions[i];
                        HexCoordinates neighborCoords = currentCoords.GetNeighbor(dir);
                        Vector2Int neighborOffset = neighborCoords.ToOffset();

                        byte neighborOwner = GetOwnerWrapped(neighborOffset.x, neighborOffset.y);
                        
                        // Skip if same owner
                        if (neighborOwner == owner)
                        {
                            continue;
                        }

                        // B. Draw Edge Effects
                        hasEdge = true;

                        // 1. Inner Glow (Inward from edge)
                        AddInnerGlow(center, edgeStarts[i], edgeEnds[i], baseColor);

                        // 2. Hard Border (Shared edge, draw once)
                        if (owner > neighborOwner)
                        {
                            AddHardBorder(center, edgeStarts[i], edgeEnds[i], borderThickness, baseColor);
                        }
                    }

                    if (hasEdge)
                    {
                        cellCount++;
                    }
                }
            }

            if (m_Vertices.Count == 0)
            {
                m_Mesh.Clear();
                m_MeshFilter.sharedMesh = m_Mesh;

                if (m_LogBuildSummary)
                {
                    Debug.Log($"[TerritoryBorderRenderer] Rebuild complete: owners={CountOwnedCells()}, vertices=0", this);
                }

                return;
            }

            m_Mesh.indexFormat = m_Vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            m_Mesh.SetVertices(m_Vertices);
            m_Mesh.SetColors(m_Colors);
            m_Mesh.SetTriangles(m_Indices, 0, true);
            m_Mesh.RecalculateBounds();
            m_MeshFilter.sharedMesh = m_Mesh;

            if (m_LogBuildSummary)
            {
                Debug.Log($"[TerritoryBorderRenderer] Rebuild complete: owners={CountOwnedCells()}, vertices={m_Vertices.Count}", this);
            }
        }

        private void AddBaseFill(Vector3 center, Vector2[] starts, Vector2[] ends, Color32 color)
        {
            if (m_FillStrength <= 0.001f)
            {
                return;
            }

            color.a = (byte)Mathf.Clamp(color.a * m_FillStrength, 0, 255);
            
            // Draw 6 triangles for the hexagon background
            // Center is index 0
            int centerIndex = m_Vertices.Count;
            m_Vertices.Add(center); // Z=0
            m_Colors.Add(color);

            // Add 6 corners
            for (int i = 0; i < 6; i++)
            {
                m_Vertices.Add(center + (Vector3)starts[i]);
                m_Colors.Add(color);
            }

            // Triangles
            for (int i = 0; i < 6; i++)
            {
                m_Indices.Add(centerIndex);
                m_Indices.Add(centerIndex + 1 + i);
                m_Indices.Add(centerIndex + 1 + ((i + 1) % 6));
            }
        }

        private void AddInnerGlow(Vector3 center, Vector2 start, Vector2 end, Color32 color)
        {
            if (m_InnerGlowWidth <= 0.001f || m_GlowStrength <= 0.001f)
            {
                return;
            }

            Color32 outerColor = color;
            outerColor.a = (byte)Mathf.Clamp(255 * m_GlowStrength, 0, 255);
            
            Color32 innerColor = outerColor;
            innerColor.a = 0;

            // Offset slightly above fill to avoid Z-fighting
            Vector3 zOffset = new Vector3(0, 0, -0.001f);

            Vector3 vStart = (Vector3)start;
            Vector3 vEnd = (Vector3)end;
            
            // Lerp towards center (0,0)
            Vector3 vInnerStart = Vector3.Lerp(vStart, Vector3.zero, m_InnerGlowWidth);
            Vector3 vInnerEnd = Vector3.Lerp(vEnd, Vector3.zero, m_InnerGlowWidth);

            int baseIndex = m_Vertices.Count;
            m_Vertices.Add(center + vStart + zOffset);
            m_Vertices.Add(center + vEnd + zOffset);
            m_Vertices.Add(center + vInnerEnd + zOffset);
            m_Vertices.Add(center + vInnerStart + zOffset);

            m_Colors.Add(outerColor);
            m_Colors.Add(outerColor);
            m_Colors.Add(innerColor);
            m_Colors.Add(innerColor);

            m_Indices.Add(baseIndex);
            m_Indices.Add(baseIndex + 2);
            m_Indices.Add(baseIndex + 3);

            m_Indices.Add(baseIndex);
            m_Indices.Add(baseIndex + 1);
            m_Indices.Add(baseIndex + 2);
        }

        private void AddHardBorder(Vector3 center, Vector2 start, Vector2 end, float thickness, Color32 color)
        {
            if (thickness <= 0f || color.a == 0)
            {
                return;
            }
            
            // Offset even more to be on top of glow
            Vector3 zOffset = new Vector3(0, 0, -0.002f);

            Vector3 a = center + new Vector3(start.x, start.y, 0f) + zOffset;
            Vector3 b = center + new Vector3(end.x, end.y, 0f) + zOffset;

            Vector3 direction = b - a;
            float length = direction.magnitude;
            if (length <= 0.00001f)
            {
                return;
            }

            direction /= length;
            Vector3 normal = new Vector3(-direction.y, direction.x, 0f) * (thickness * 0.5f);

            int vertexStart = m_Vertices.Count;
            m_Vertices.Add(a - normal);
            m_Vertices.Add(a + normal);
            m_Vertices.Add(b - normal);
            m_Vertices.Add(b + normal);

            m_Colors.Add(color);
            m_Colors.Add(color);
            m_Colors.Add(color);
            m_Colors.Add(color);

            m_Indices.Add(vertexStart + 0);
            m_Indices.Add(vertexStart + 1);
            m_Indices.Add(vertexStart + 2);
            m_Indices.Add(vertexStart + 2);
            m_Indices.Add(vertexStart + 1);
            m_Indices.Add(vertexStart + 3);
        }

        private void EnsureMesh()
        {
            if (m_Mesh != null)
            {
                return;
            }

            m_Mesh = new Mesh
            {
                name = "TerritoryBorderMesh"
            };
        }

        private void ClearMesh()
        {
            if (m_Mesh == null)
            {
                return;
            }

            m_Mesh.Clear();
            if (m_MeshFilter != null)
            {
                m_MeshFilter.sharedMesh = m_Mesh;
            }
        }

        private void ReleaseMesh()
        {
            if (m_Mesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(m_Mesh);
            }
            else
            {
                DestroyImmediate(m_Mesh);
            }

            m_Mesh = null;
        }

        private void ApplyMaterial()
        {
            if (m_MeshRenderer == null)
            {
                return;
            }

            Material target = m_Material;
            if (target == null)
            {
                var shader = Shader.Find("Map/TerritoryBorder");
                if (shader != null)
                {
                    if (m_RuntimeMaterial == null || m_RuntimeMaterial.shader != shader)
                    {
                        ReleaseRuntimeMaterial();
                        m_RuntimeMaterial = new Material(shader)
                        {
                            name = "TerritoryBorderRuntime",
                            hideFlags = HideFlags.DontSave
                        };
                    }

                    target = m_RuntimeMaterial;
                }
            }

            if (target != null && m_MeshRenderer.sharedMaterial != target)
            {
                m_MeshRenderer.sharedMaterial = target;
            }
        }

        private void ReleaseRuntimeMaterial()
        {
            if (m_RuntimeMaterial == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(m_RuntimeMaterial);
            }
            else
            {
                DestroyImmediate(m_RuntimeMaterial);
            }

            m_RuntimeMaterial = null;
        }

        private void ApplySorting()
        {
            if (m_MeshRenderer == null)
            {
                return;
            }

            if (m_CopySortingFromTilemap && m_Tilemap != null)
            {
                var tilemapRenderer = m_Tilemap.GetComponent<Renderer>();
                if (tilemapRenderer != null)
                {
                    m_MeshRenderer.sortingLayerID = tilemapRenderer.sortingLayerID;
                    m_MeshRenderer.sortingOrder = tilemapRenderer.sortingOrder + m_SortingOrder;
                    return;
                }
            }

            m_MeshRenderer.sortingLayerName = m_SortingLayer;
            m_MeshRenderer.sortingOrder = m_SortingOrder;
        }

        private byte GetOwnerWrapped(int col, int row)
        {
            if (m_Owners == null || m_Owners.Length == 0)
            {
                return 0;
            }

            if (row < 0 || row >= m_MapHeight || m_MapWidth <= 0)
            {
                return 0;
            }

            int wrappedCol = WrapIndex(col, m_MapWidth);
            int index = wrappedCol + row * m_MapWidth;
            if (index < 0 || index >= m_Owners.Length)
            {
                return 0;
            }

            return m_Owners[index];
        }

        private static int WrapIndex(int value, int max)
        {
            if (max <= 0)
            {
                return 0;
            }

            int wrapped = value % max;
            return wrapped < 0 ? wrapped + max : wrapped;
        }

        private Color32 ResolveOwnerColor(byte ownerId)
        {
            if (m_TerritorySettings == null)
            {
                return new Color32(255, 255, 255, 255);
            }

            var color = m_TerritorySettings.GetColor(ownerId);
            if (color.a == 0)
            {
                color.a = 255;
            }

            return color;
        }

        private int CountOwnedCells()
        {
            if (m_Owners == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < m_Owners.Length; i++)
            {
                if (m_Owners[i] != 0)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
