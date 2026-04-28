#region

using System;
using Tool;
using UnityEngine;
using Object = UnityEngine.Object;

#endregion

namespace Core.Capability
{
    public class CEntity : IEntity
    {
        /// <summary>
        ///     组件新增通知。外部系统可订阅此事件做组件生命周期扩展。
        /// </summary>
        public static event Action<CEntity, CComponent> ComponentAdded;

        /// <summary>
        ///     实体释放通知。订阅方可清理缓存，避免残留引用。
        /// </summary>
        public static event Action<CEntity> EntityDisposed;

        public IEntity.EntityState State { get; private set; }

        public IEntity Parent { get; private set; }

        public int Id { get; private set; }

        public string Name { get; set; }

        public int Version { get; private set; }

        public GameObject Go;

        public CapabilityWorldBase World { get; private set; }

        public bool IsActive => State == IEntity.EntityState.Running;

        public IndexedObjectArray<CComponent> Components { get; private set; }

        public void OnDirty(IEntity parent, int id)
        {
            State = IEntity.EntityState.Running;
            Parent = parent;
            Id = id;
            Version++;
            Components = new IndexedObjectArray<CComponent>();
            Components.Init(World.MaxComponentCount);
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

            if (Go != null)
            {
                Object.Destroy(Go);
                Go = null;
            }
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