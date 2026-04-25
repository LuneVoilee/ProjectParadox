#region

using System;
using System.Collections.Generic;
using Tool;
using UnityEngine;

#endregion

namespace Core.Capability
{
    public class CEntity : IEntity
    {
        private readonly struct TemplateKeyRoute : IEquatable<TemplateKeyRoute>
        {
            public readonly Type TemplateSetType;
            public readonly string Slot;

            public TemplateKeyRoute(Type templateSetType, string slot)
            {
                TemplateSetType = templateSetType;
                Slot = slot;
            }

            public bool Equals(TemplateKeyRoute other)
            {
                return TemplateSetType == other.TemplateSetType &&
                       string.Equals(Slot, other.Slot, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is TemplateKeyRoute other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((TemplateSetType != null ? TemplateSetType.GetHashCode() : 0) * 397) ^
                           (Slot != null ? Slot.GetHashCode() : 0);
                }
            }
        }

        /// <summary>
        ///     组件新增通知。Core.Json 等系统可订阅此事件做自动绑定。
        /// </summary>
        public static event Action<CEntity, CComponent> ComponentAdded;

        /// <summary>
        ///     模板 Key 变化通知。用于触发“延迟组件”的补绑定。
        /// </summary>
        public static event Action<CEntity> TemplateKeysChanged;

        /// <summary>
        ///     实体释放通知。订阅方可清理缓存，避免残留引用。
        /// </summary>
        public static event Action<CEntity> EntityDisposed;

        public IEntity.EntityState State { get; private set; }

        public IEntity Parent { get; private set; }

        public int Id { get; private set; }

        public string Name { get; set; }

        public int Version { get; private set; }

        public CapabilityWorldBase World { get; private set; }

        public bool IsActive => State == IEntity.EntityState.Running;

        public IndexedObjectArray<CComponent> Components { get; private set; }

        private object m_DefaultTemplateKey;

        private Dictionary<TemplateKeyRoute, object> m_TemplateKeys;

        public void OnDirty(IEntity parent, int id)
        {
            State = IEntity.EntityState.Running;
            Parent = parent;
            Id = id;
            Version++;
            Components = new IndexedObjectArray<CComponent>();
            Components.Init(World.MaxComponentCount);
            m_DefaultTemplateKey = null;
            m_TemplateKeys?.Clear();
        }

        public void SetWorld(CapabilityWorldBase world)
        {
            World = world;
        }

        public TComponent AddComponent<TComponent>() where TComponent : CComponent, new()
        {
            int componentId = Component<TComponent>.TId;
            if (Components[componentId] != null)
            {
                Debug.LogWarning($"Entity already has component: {typeof(TComponent).FullName}");
                return (TComponent)Components[componentId];
            }

            TComponent component = new TComponent();
            Components.Set(componentId, component);
            component.Owner = this;
            InvokeComponentAdded(component);
            World.NotifyComponentChanged(componentId, this);
            return component;
        }

        public CComponent AddComponent(Type componentType)
        {
            if (componentType == null)
            {
                return null;
            }

            if (!typeof(CComponent).IsAssignableFrom(componentType))
            {
                Debug.LogError($"Type is not CComponent: {componentType.FullName}");
                return null;
            }

            int componentId = ComponentIdGenerator.GetId(componentType);
            if (Components[componentId] != null)
            {
                return Components[componentId];
            }

            var component = (CComponent)Activator.CreateInstance(componentType);
            Components.Set(componentId, component);
            component.Owner = this;
            InvokeComponentAdded(component);
            World.NotifyComponentChanged(componentId, this);
            return component;
        }

        /// <summary>
        ///     设置实体默认模板 Key。自动绑定找不到“按模板集的 Key”时会回退到这里。
        /// </summary>
        public void SetDefaultTemplateKey(object templateKey)
        {
            m_DefaultTemplateKey = templateKey;
            TemplateKeysChanged?.Invoke(this);
        }

        public bool TryGetDefaultTemplateKey(out object templateKey)
        {
            templateKey = m_DefaultTemplateKey;
            return templateKey != null;
        }

        /// <summary>
        ///     设置某个模板集的模板 Key，可选 slot 用于同一模板集多路配置。
        ///     slot 为空字符串表示该模板集的默认路由。
        /// </summary>
        public void SetTemplateKey(Type templateSetType, object templateKey, string slot = "")
        {
            if (templateSetType == null)
            {
                throw new ArgumentNullException(nameof(templateSetType));
            }

            TemplateKeyRoute route = new TemplateKeyRoute(templateSetType, NormalizeSlot(slot));
            m_TemplateKeys ??= new Dictionary<TemplateKeyRoute, object>();

            if (templateKey == null)
            {
                m_TemplateKeys.Remove(route);
            }
            else
            {
                m_TemplateKeys[route] = templateKey;
            }

            TemplateKeysChanged?.Invoke(this);
        }

        public void SetTemplateKey<TTemplateSet>(object templateKey, string slot = "")
        {
            SetTemplateKey(typeof(TTemplateSet), templateKey, slot);
        }

        public bool TryGetTemplateKey
            (Type templateSetType, out object templateKey, string slot = "")
        {
            if (templateSetType == null || m_TemplateKeys == null)
            {
                templateKey = null;
                return false;
            }

            return m_TemplateKeys.TryGetValue(
                new TemplateKeyRoute(templateSetType, NormalizeSlot(slot)),
                out templateKey);
        }

        public bool TryGetTemplateKey<TTemplateSet>(out object templateKey, string slot = "")
        {
            return TryGetTemplateKey(typeof(TTemplateSet), out templateKey, slot);
        }

        public CComponent GetComponent(int componentId)
        {
            return Components[componentId];
        }

        public bool TryGetComponent<TComponent>
            (int componentId, out TComponent component) where TComponent : CComponent
        {
            component = Components[componentId] as TComponent;

            return component != null;
        }

        public bool HasComponent(int componentId)
        {
            return Components[componentId] != null;
        }

        public bool HasComponents(params int[] componentIds)
        {
            foreach (var id in componentIds)
            {
                if (Components[id] == null)
                {
                    return false;
                }
            }

            return true;
        }

        public bool HasAnyComponent(int[] componentIds)
        {
            for (int i = 0; i < componentIds.Length; i++)
            {
                if (Components[componentIds[i]] != null)
                {
                    return true;
                }
            }

            return false;
        }

        public void RemoveComponent(int componentId)
        {
            CComponent component = Components[componentId];
            if (component == null)
            {
                return;
            }

            component.Owner = null;
            component.Dispose();
            Components.Remove(componentId);
            World.NotifyComponentChanged(componentId, this);
        }

        private void ClearAllComponents()
        {
            for (int i = Components.IndexList.Count - 1; i >= 0; i--)
            {
                int componentId = Components.IndexList[i];
                RemoveComponent(componentId);
            }

            Components.Dispose();
            Components = null;
        }

        public void Dispose()
        {
            State = IEntity.EntityState.Cleared;
            Version++;
            EntityDisposed?.Invoke(this);
            ClearAllComponents();
            m_DefaultTemplateKey = null;
            m_TemplateKeys?.Clear();
            m_TemplateKeys = null;
        }

        private static string NormalizeSlot(string slot)
        {
            return string.IsNullOrEmpty(slot) ? string.Empty : slot;
        }

        private void InvokeComponentAdded(CComponent component)
        {
            Action<CEntity, CComponent> handlers = ComponentAdded;
            if (handlers == null)
            {
                return;
            }

            foreach (Delegate del in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<CEntity, CComponent>)del)?.Invoke(this, component);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}