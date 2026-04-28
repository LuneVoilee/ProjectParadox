using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Core.Capability.Editor
{
    /// <summary>
    ///     采集并还原 Entity 关联的场景 Transform 状态。
    /// </summary>
    internal static class CapabilityDebugSceneState
    {
#if UNITY_EDITOR
        /// <summary>
        ///     通过场景中所有 EntityInstaller 建立 CEntity → Transform 的稳定映射。
        /// </summary>
        public static Dictionary<CEntity, Transform> BuildEntityTransformMap()
        {
            var map = new Dictionary<CEntity, Transform>();
            var installers = Object.FindObjectsOfType<EntityInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                EntityInstaller installer = installers[i];
                if (installer == null || installer.Entity == null)
                {
                    continue;
                }

                Transform t = installer.EntityTransform;
                if (t == null)
                {
                    continue;
                }

                if (!map.ContainsKey(installer.Entity))
                {
                    map[installer.Entity] = t;
                }
            }

            return map;
        }
#endif

        /// <summary>
        ///     记录单个 Transform 到快照列表（Installer 主路径使用）。
        /// </summary>
        public static void CaptureSingle
        (
            Transform t,
            List<CapabilityDebugTransformSnapshot> destination
        )
        {
            destination.Clear();
            if (t == null)
            {
                return;
            }

            destination.Add(new CapabilityDebugTransformSnapshot
            {
                InstanceId = t.GetInstanceID(),
                Path = BuildPath(t),
                Transform = t,
                ActiveSelf = t.gameObject.activeSelf,
                Position = t.position,
                LocalPosition = t.localPosition,
                Rotation = t.rotation,
                LocalRotation = t.localRotation,
                LocalScale = t.localScale
            });
        }

        public static void Capture
        (
            Dictionary<int, Transform> transforms,
            List<CapabilityDebugTransformSnapshot> destination
        )
        {
            destination.Clear();
            foreach (KeyValuePair<int, Transform> pair in transforms)
            {
                Transform transform = pair.Value;
                if (transform == null)
                {
                    continue;
                }

                destination.Add(new CapabilityDebugTransformSnapshot
                {
                    InstanceId = pair.Key,
                    Path = BuildPath(transform),
                    Transform = transform,
                    ActiveSelf = transform.gameObject.activeSelf,
                    Position = transform.position,
                    LocalPosition = transform.localPosition,
                    Rotation = transform.rotation,
                    LocalRotation = transform.localRotation,
                    LocalScale = transform.localScale
                });
            }
        }

        public static void Restore(CapabilityDebugFrame frame)
        {
            if (frame == null)
            {
                return;
            }

            for (int worldIndex = 0; worldIndex < frame.Worlds.Count; worldIndex++)
            {
                CapabilityDebugWorldSnapshot world = frame.Worlds[worldIndex];
                for (int entityIndex = 0; entityIndex < world.Entities.Count; entityIndex++)
                {
                    RestoreEntity(world.Entities[entityIndex]);
                }
            }
        }

        private static void RestoreEntity(CapabilityDebugEntitySnapshot entity)
        {
            for (int i = 0; i < entity.Transforms.Count; i++)
            {
                CapabilityDebugTransformSnapshot snapshot = entity.Transforms[i];
                Transform transform = snapshot.Transform;
                if (transform == null)
                {
                    continue;
                }

                // 回放只改场景表现层，不反写 Capability 组件数据。
                transform.localPosition = snapshot.LocalPosition;
                transform.localRotation = snapshot.LocalRotation;
                transform.localScale = snapshot.LocalScale;
                if (transform.gameObject.activeSelf != snapshot.ActiveSelf)
                {
                    transform.gameObject.SetActive(snapshot.ActiveSelf);
                }
            }
        }

        private static string BuildPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            Stack<string> names = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names.ToArray());
        }
    }
}
