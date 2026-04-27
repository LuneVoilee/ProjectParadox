#region

using UnityEngine;
using UnityEngine.Tilemaps;

#endregion

namespace GamePlay.Map
{
    public partial class DrawMapCap
    {
        private int ResolveGhostColumns(DrawMap drawMap, Tilemap tilemap)
        {
            int extraColumns = Mathf.Max(0, drawMap.GhostColumnsFloor);
            if (!drawMap.AutoGhostColumns)
            {
                return extraColumns;
            }

            UnityEngine.Camera camera = ResolveCamera();
            if (!m_IsAutoGhostCacheValid || HasGhostColumnsInputChanged(camera))
            {
                m_CachedAutoGhostColumns = CalculateAutoGhostColumns(tilemap, camera);
                CacheGhostColumnsInput(camera);
                m_IsAutoGhostCacheValid = true;
            }

            return Mathf.Max(extraColumns, m_CachedAutoGhostColumns);
        }

        private UnityEngine.Camera ResolveCamera()
        {
            if (m_CachedCamera != null)
            {
                return m_CachedCamera;
            }

            m_CachedCamera = UnityEngine.Camera.main;
            return m_CachedCamera;
        }

        private static int CalculateAutoGhostColumns(Tilemap tilemap, UnityEngine.Camera camera)
        {
            if (camera == null || !camera.orthographic)
            {
                return DrawMap.DefaultGhostColumns;
            }

            Vector3 origin = tilemap.CellToWorld(Vector3Int.zero);
            Vector3 next = tilemap.CellToWorld(new Vector3Int(1, 0, 0));
            float columnWidth = Mathf.Abs(next.x - origin.x);
            if (columnWidth <= Mathf.Epsilon)
            {
                return DrawMap.DefaultGhostColumns;
            }

            float viewWidth = camera.orthographicSize * 2f * camera.aspect;
            return Mathf.CeilToInt(viewWidth / columnWidth) + DrawMap.GhostPadding;
        }

        private bool HasGhostColumnsInputChanged(UnityEngine.Camera camera)
        {
            if (m_LastScreenWidth != Screen.width || m_LastScreenHeight != Screen.height)
            {
                return true;
            }

            int cameraInstanceId = camera == null ? int.MinValue : camera.GetInstanceID();
            if (cameraInstanceId != m_LastCameraInstanceId)
            {
                return true;
            }

            bool isOrthographic = camera != null && camera.orthographic;
            if (isOrthographic != m_LastCameraOrthographic)
            {
                return true;
            }

            if (!isOrthographic)
            {
                return false;
            }

            return !Mathf.Approximately(m_LastCameraOrthographicSize, camera.orthographicSize) ||
                   !Mathf.Approximately(m_LastCameraAspect, camera.aspect);
        }

        private void CacheGhostColumnsInput(UnityEngine.Camera camera)
        {
            m_LastScreenWidth = Screen.width;
            m_LastScreenHeight = Screen.height;
            m_LastCameraInstanceId = camera == null ? int.MinValue : camera.GetInstanceID();
            m_LastCameraOrthographic = camera != null && camera.orthographic;
            if (!m_LastCameraOrthographic)
            {
                m_LastCameraOrthographicSize = -1f;
                m_LastCameraAspect = -1f;
                return;
            }

            m_LastCameraOrthographicSize = camera.orthographicSize;
            m_LastCameraAspect = camera.aspect;
        }
    }
}