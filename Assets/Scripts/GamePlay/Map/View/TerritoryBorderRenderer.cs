using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using Map.Components;
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
        [SerializeField, Range(0.001f, 0.2f)] private float m_BorderWidth = 0.04f;
        [SerializeField, Range(0.001f, 0.4f)] private float m_GlowWidth = 0.12f;
        [SerializeField, Range(0f, 2f)] private float m_GlowStrength = 0.6f;

        [Header("Sorting")]
        [SerializeField] private bool m_CopySortingFromTilemap = true;
        [SerializeField] private string m_SortingLayer = "Default";
        [SerializeField] private int m_SortingOrder = 1;

        private MeshFilter m_MeshFilter;
        private MeshRenderer m_MeshRenderer;
        private Mesh m_Mesh;
        private MaterialPropertyBlock m_PropertyBlock;

        private Texture2D m_OwnerTexture;
        private byte[] m_OwnerRaw;
        private int m_MapWidth;
        private int m_MapHeight;
        private int m_GhostColumns;
        private int m_TextureWidth;
        private bool m_TextureDirty;

        private static readonly int s_OwnerTexId = Shader.PropertyToID("_OwnerTex");
        private static readonly int s_PaletteTexId = Shader.PropertyToID("_PaletteTex");
        private static readonly int s_MapSizeId = Shader.PropertyToID("_MapSize");
        private static readonly int s_BorderWidthId = Shader.PropertyToID("_BorderWidth");
        private static readonly int s_GlowWidthId = Shader.PropertyToID("_GlowWidth");
        private static readonly int s_GlowStrengthId = Shader.PropertyToID("_GlowStrength");

        public int GhostColumns => m_GhostColumns;
        public int MapWidth => m_MapWidth;
        public int MapHeight => m_MapHeight;

        private void Awake()
        {
            CacheComponents();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (m_MeshRenderer != null)
            {
                ApplySorting();
            }
        }
#endif

        public void Render(CGrid data)
        {
            if (data == null || m_Tilemap == null)
            {
                return;
            }

            CacheComponents();

            m_MapWidth = data.Width;
            m_MapHeight = data.Height;
            m_GhostColumns = m_MapRenderer != null ? m_MapRenderer.GhostColumns : 0;
            m_TextureWidth = m_MapWidth + m_GhostColumns * 2;

            EnsureOwnerTexture(m_TextureWidth, m_MapHeight);
            FillOwnerTexture(data);
            BuildMesh();
            ApplyMaterial();
            ApplySorting();
        }

        public void SetOwner(int col, int row, byte ownerId)
        {
            if (m_OwnerRaw == null || m_OwnerRaw.Length == 0)
            {
                return;
            }

            if (row < 0 || row >= m_MapHeight || col < 0 || col >= m_MapWidth)
            {
                return;
            }

            SetOwnerInTexture(col + m_GhostColumns, row, ownerId);

            if (m_GhostColumns > 0)
            {
                if (col < m_GhostColumns)
                {
                    int texCol = m_GhostColumns + m_MapWidth + col;
                    SetOwnerInTexture(texCol, row, ownerId);
                }

                if (col >= m_MapWidth - m_GhostColumns)
                {
                    int texCol = col - (m_MapWidth - m_GhostColumns);
                    SetOwnerInTexture(texCol, row, ownerId);
                }
            }

            m_TextureDirty = true;
        }

        public void ApplyChanges()
        {
            if (!m_TextureDirty || m_OwnerTexture == null || m_OwnerRaw == null)
            {
                return;
            }

            m_OwnerTexture.SetPixelData(m_OwnerRaw, 0);
            m_OwnerTexture.Apply(false, false);
            m_TextureDirty = false;
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

            if (m_PropertyBlock == null)
            {
                m_PropertyBlock = new MaterialPropertyBlock();
            }
        }

        private void EnsureOwnerTexture(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            if (m_OwnerTexture == null || m_OwnerTexture.width != width || m_OwnerTexture.height != height)
            {
                ReleaseOwnerTexture();
                m_OwnerTexture = new Texture2D(width, height, TextureFormat.R8, false, false)
                {
                    name = "TerritoryOwnerTex",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave
                };
            }

            int size = width * height;
            if (m_OwnerRaw == null || m_OwnerRaw.Length != size)
            {
                m_OwnerRaw = new byte[size];
            }
        }

        private void ReleaseOwnerTexture()
        {
            if (m_OwnerTexture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(m_OwnerTexture);
            }
            else
            {
                DestroyImmediate(m_OwnerTexture);
            }

            m_OwnerTexture = null;
        }

        private void FillOwnerTexture(CGrid data)
        {
            if (m_OwnerRaw == null || data == null)
            {
                return;
            }

            int index = 0;
            for (int row = 0; row < m_MapHeight; row++)
            {
                for (int col = -m_GhostColumns; col < m_MapWidth + m_GhostColumns; col++)
                {
                    var cell = data.GetCellWrappedX(col, row);
                    m_OwnerRaw[index++] = cell == null ? (byte)0 : cell.OwnerId;
                }
            }

            m_OwnerTexture.SetPixelData(m_OwnerRaw, 0);
            m_OwnerTexture.Apply(false, false);
            m_TextureDirty = false;
        }

        private void BuildMesh()
        {
            if (m_Tilemap == null)
            {
                return;
            }

            int totalWidth = m_MapWidth + m_GhostColumns * 2;
            int totalCells = totalWidth * m_MapHeight;
            int vertexCount = totalCells * 4;
            int indexCount = totalCells * 6;

            if (m_Mesh == null)
            {
                m_Mesh = new Mesh { name = "TerritoryBorderMesh" };
            }
            else
            {
                m_Mesh.Clear();
            }

            if (vertexCount > 65535)
            {
                m_Mesh.indexFormat = IndexFormat.UInt32;
            }

            var vertices = new Vector3[vertexCount];
            var uv = new Vector2[vertexCount];
            var uv2 = new Vector2[vertexCount];
            var triangles = new int[indexCount];

            var cellSize = m_Tilemap.cellSize;
            var half = new Vector3(cellSize.x * 0.5f, cellSize.y * 0.5f, 0f);

            int v = 0;
            int t = 0;
            for (int row = 0; row < m_MapHeight; row++)
            {
                for (int col = -m_GhostColumns; col < m_MapWidth + m_GhostColumns; col++)
                {
                    var center = m_Tilemap.GetCellCenterLocal(new Vector3Int(col, row, 0));

                    vertices[v + 0] = center + new Vector3(-half.x, -half.y, 0f);
                    vertices[v + 1] = center + new Vector3(half.x, -half.y, 0f);
                    vertices[v + 2] = center + new Vector3(-half.x, half.y, 0f);
                    vertices[v + 3] = center + new Vector3(half.x, half.y, 0f);

                    uv[v + 0] = new Vector2(0f, 0f);
                    uv[v + 1] = new Vector2(1f, 0f);
                    uv[v + 2] = new Vector2(0f, 1f);
                    uv[v + 3] = new Vector2(1f, 1f);

                    int texCol = col + m_GhostColumns;
                    var coord = new Vector2(texCol, row);
                    uv2[v + 0] = coord;
                    uv2[v + 1] = coord;
                    uv2[v + 2] = coord;
                    uv2[v + 3] = coord;

                    triangles[t + 0] = v + 0;
                    triangles[t + 1] = v + 2;
                    triangles[t + 2] = v + 1;
                    triangles[t + 3] = v + 2;
                    triangles[t + 4] = v + 3;
                    triangles[t + 5] = v + 1;

                    v += 4;
                    t += 6;
                }
            }

            m_Mesh.vertices = vertices;
            m_Mesh.uv = uv;
            m_Mesh.uv2 = uv2;
            m_Mesh.triangles = triangles;
            m_Mesh.RecalculateBounds();

            m_MeshFilter.sharedMesh = m_Mesh;
        }

        private void ApplyMaterial()
        {
            if (m_MeshRenderer == null)
            {
                return;
            }

            if (m_Material != null && m_MeshRenderer.sharedMaterial != m_Material)
            {
                m_MeshRenderer.sharedMaterial = m_Material;
            }

            m_MeshRenderer.GetPropertyBlock(m_PropertyBlock);
            m_PropertyBlock.SetTexture(s_OwnerTexId, m_OwnerTexture);
            m_PropertyBlock.SetTexture(s_PaletteTexId, m_TerritorySettings != null ? m_TerritorySettings.GetTexture() : null);
            m_PropertyBlock.SetVector(s_MapSizeId, new Vector4(m_TextureWidth, m_MapHeight, 1f / m_TextureWidth, 1f / m_MapHeight));
            m_PropertyBlock.SetFloat(s_BorderWidthId, m_BorderWidth);
            m_PropertyBlock.SetFloat(s_GlowWidthId, m_GlowWidth);
            m_PropertyBlock.SetFloat(s_GlowStrengthId, m_GlowStrength);
            m_MeshRenderer.SetPropertyBlock(m_PropertyBlock);
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

        private void SetOwnerInTexture(int texCol, int row, byte ownerId)
        {
            if (texCol < 0 || texCol >= m_TextureWidth)
            {
                return;
            }

            int index = texCol + row * m_TextureWidth;
            if (index < 0 || index >= m_OwnerRaw.Length)
            {
                return;
            }

            m_OwnerRaw[index] = ownerId;
        }
    }
}
