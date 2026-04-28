#region

using System;
using System.Collections.Generic;

#endregion

namespace Core.Capability
{
    public abstract class CapabilityBase : IDisposable
    {
        public int Id { get; private set; }

        public List<int> TagList { get; protected set; }

        public CapabilityWorld World { get; private set; }

        public CEntity Owner { get; private set; }

        public bool IsActive { get; internal set; }

        public virtual CapabilityUpdateMode UpdateMode { get; protected set; } =
            CapabilityUpdateMode.Update;

        public virtual int TickGroupOrder { get; protected set; }

        protected CapabilityCollector m_CapabilityCollector;

        internal bool ComponentChanged;

        internal bool TryComponentChanged
        {
            get
            {
                bool changed = ComponentChanged;
                if (m_CapabilityCollector != null)
                {
                    ComponentChanged = false;
                }

                return changed;
            }
        }

        public void Init(int id, CapabilityWorld world, CEntity owner)
        {
            Id = id;
            World = world;
            Owner = owner;
            ComponentChanged = true;
            OnInit();
        }

        protected virtual void OnInit()
        {
        }

        protected void DebugLog(string message)
        {
#if UNITY_EDITOR
            // Debugger 会在采样帧消费这些日志，并补上帧号后显示在 Inspector。
            CapabilityDebugLogBridge.Add(this, message);
#endif
        }

        protected void Filter(params int[] componentIds)
        {
            if (componentIds == null)
            {
                throw new ArgumentNullException(nameof(componentIds),
                    $"{GetType().Name} componentIds is null");
            }

            m_CapabilityCollector = CapabilityCollector.CreateCollector(World, this, componentIds);
            ComponentChanged = false;
        }

        public abstract bool ShouldActivate();

        public abstract bool ShouldDeactivate();


        public void Activated()
        {
            IsActive = true;
            OnActivated();
        }

        protected virtual void OnActivated()
        {
        }

        public void Deactivated()
        {
            IsActive = false;
            OnDeactivated();
        }

        protected virtual void OnDeactivated()
        {
        }

        public virtual void TickActive(float deltaTime, float realElapsedSeconds)
        {
        }

        public virtual void Dispose()
        {
            if (m_CapabilityCollector != null)
            {
                CapabilityCollector.Release(m_CapabilityCollector);
            }

            m_CapabilityCollector = null;
            IsActive = false;
            TagList = null;
            Owner = null;
            World = null;
        }
    }
}
