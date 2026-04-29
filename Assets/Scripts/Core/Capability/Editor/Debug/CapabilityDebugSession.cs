using System.Collections.Generic;
using UnityEngine;

namespace Core.Capability.Editor
{
    /// <summary>
    ///     Inspector 当前选中项的类型。
    /// </summary>
    internal enum CapabilityDebugItemKind
    {
        None = 0,
        Component = 1,
        Capability = 2,
        Category = 3
    }

    /// <summary>
    ///     一次 Play Mode 调试会话的完整内存记录。
    /// </summary>
    internal sealed class CapabilityDebugSession
    {
        public readonly List<CapabilityDebugFrame> Frames = new List<CapabilityDebugFrame>(1024);

        public int CurrentFrameIndex { get; private set; } = -1;

        public bool HasFrames => Frames.Count > 0;

        public bool IsAtLatestFrame => CurrentFrameIndex < 0 || CurrentFrameIndex == Frames.Count - 1;

        public CapabilityDebugFrame CurrentFrame
        {
            get
            {
                if (CurrentFrameIndex < 0 || CurrentFrameIndex >= Frames.Count)
                {
                    return null;
                }

                return Frames[CurrentFrameIndex];
            }
        }

        public void AddFrame(CapabilityDebugFrame frame)
        {
            if (frame == null)
            {
                return;
            }

            Frames.Add(frame);
            CurrentFrameIndex = Frames.Count - 1;
        }

        public void SetCurrentFrameIndex(int index)
        {
            if (Frames.Count == 0)
            {
                CurrentFrameIndex = -1;
                return;
            }

            CurrentFrameIndex = Mathf.Clamp(index, 0, Frames.Count - 1);
        }

        public void Clear()
        {
            Frames.Clear();
            CurrentFrameIndex = -1;
        }

    }

    /// <summary>
    ///     单个采样帧，保存所有 World 的可观察状态。
    /// </summary>
    internal sealed class CapabilityDebugFrame
    {
        public int FrameIndex;
        public int UnityFrameCount;
        public double RealtimeSinceStartup;
        public readonly List<CapabilityDebugWorldSnapshot> Worlds =
            new List<CapabilityDebugWorldSnapshot>(8);

        public CapabilityDebugEntitySnapshot FindEntity(string entityKey)
        {
            for (int i = 0; i < Worlds.Count; i++)
            {
                CapabilityDebugEntitySnapshot entity = Worlds[i].FindEntity(entityKey);
                if (entity != null)
                {
                    return entity;
                }
            }

            return null;
        }

        public CapabilityDebugWorldSnapshot FindWorld(string worldKey)
        {
            for (int i = 0; i < Worlds.Count; i++)
            {
                CapabilityDebugWorldSnapshot world = Worlds[i];
                if (world.Key == worldKey)
                {
                    return world;
                }
            }

            return null;
        }
    }

    /// <summary>
    ///     World 层级快照，用于组织 Entity 导航。
    /// </summary>
    internal sealed class CapabilityDebugWorldSnapshot
    {
        public string Key;
        public int WorldId;
        public int WorldVersion;
        public string DisplayName;
        public readonly List<CapabilityDebugEntitySnapshot> Entities =
            new List<CapabilityDebugEntitySnapshot>(128);
        public readonly List<CapabilityDebugCapabilitySnapshot> GlobalCapabilities =
            new List<CapabilityDebugCapabilitySnapshot>(64);

        public CapabilityDebugEntitySnapshot FindEntity(string entityKey)
        {
            for (int i = 0; i < Entities.Count; i++)
            {
                CapabilityDebugEntitySnapshot entity = Entities[i];
                if (entity.Key == entityKey)
                {
                    return entity;
                }
            }

            return null;
        }

        public CapabilityDebugCapabilitySnapshot FindGlobalCapability(string itemKey)
        {
            for (int i = 0; i < GlobalCapabilities.Count; i++)
            {
                CapabilityDebugCapabilitySnapshot capability = GlobalCapabilities[i];
                if (capability.Key == itemKey)
                {
                    return capability;
                }
            }

            return null;
        }
    }

    /// <summary>
    ///     Entity 层级快照，包含组件、能力和可回放的场景 Transform。
    /// </summary>
    internal sealed class CapabilityDebugEntitySnapshot
    {
        public string Key;
        public string WorldKey;
        public int EntityId;
        public int EntityVersion;
        public string DisplayName;
        public readonly List<CapabilityDebugComponentSnapshot> Components =
            new List<CapabilityDebugComponentSnapshot>(32);
        public readonly List<CapabilityDebugTransformSnapshot> Transforms =
            new List<CapabilityDebugTransformSnapshot>(8);

        public CapabilityDebugComponentSnapshot FindComponent(string itemKey)
        {
            for (int i = 0; i < Components.Count; i++)
            {
                CapabilityDebugComponentSnapshot component = Components[i];
                if (component.Key == itemKey)
                {
                    return component;
                }
            }

            return null;
        }
    }

    /// <summary>
    ///     CComponent 字段快照，记录该帧组件的全部实例字段。
    /// </summary>
    internal sealed class CapabilityDebugComponentSnapshot
    {
        public string Key;
        public int ComponentId;
        public string TypeName;
        public string TypeFullName;
        public readonly List<CapabilityDebugValueSnapshot> Fields =
            new List<CapabilityDebugValueSnapshot>(16);
    }

    /// <summary>
    ///     Capability 字段与运行状态快照，只记录被 Attribute 标记的本地字段。
    /// </summary>
    internal sealed class CapabilityDebugCapabilitySnapshot
    {
        public string Key;
        public int CapabilityId;
        public string TypeName;
        public string TypeFullName;
        public CapabilityUpdateMode UpdateMode;
        public int TickGroupOrder;
        public string StageName;
        public CapabilityRuntimeState State;
        public string DebugCategory;
        public string DebugTag;
        public string LastErrorMessage;
        public double LastTickMilliseconds;
        public int MatchedEntityCount;
        public readonly List<int> MatchedEntityIds = new List<int>(32);
        public readonly List<CapabilityDebugValueSnapshot> Fields =
            new List<CapabilityDebugValueSnapshot>(16);
        public readonly List<CapabilityDebugLogSnapshot> Logs =
            new List<CapabilityDebugLogSnapshot>(4);
    }

    /// <summary>
    ///     Inspector 字段展示节点，支持少量嵌套子节点。
    /// </summary>
    internal sealed class CapabilityDebugValueSnapshot
    {
        public string Name;
        public string TypeName;
        public string DisplayValue;
        public readonly List<CapabilityDebugValueSnapshot> Children =
            new List<CapabilityDebugValueSnapshot>(8);
    }

    /// <summary>
    ///     Capability 在某个采样帧产生的调试日志。
    /// </summary>
    internal sealed class CapabilityDebugLogSnapshot
    {
        public int FrameIndex;
        public double Time;
        public string Message;
    }

    /// <summary>
    ///     Entity 绑定场景对象的 Transform 状态，用于 Timeline 回放。
    /// </summary>
    internal sealed class CapabilityDebugTransformSnapshot
    {
        public int InstanceId;
        public string Path;
        public Transform Transform;
        public bool ActiveSelf;
        public Vector3 Position;
        public Vector3 LocalPosition;
        public Quaternion Rotation;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;
    }
}
