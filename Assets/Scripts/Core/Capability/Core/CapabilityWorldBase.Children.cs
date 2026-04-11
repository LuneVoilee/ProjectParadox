using System;
using System.Collections.Generic;
using Tool;

namespace Core.Capability
{
    public abstract partial class CapabilityWorldBase
    {
        public IndexedObjectArray<CEntity> Children { get; private set; }

        public int ChildrenCount => Children?.Count ?? 0;

        private Stack<int> m_RecycledEntityIds = new Stack<int>();

        private void InitializeChildren()
        {
            Children = new IndexedObjectArray<CEntity>();
        }

        public void EstimateChildrenCount(int count)
        {
            Children.Init(count);
        }

        public virtual TEntity AddChild<TEntity>() where TEntity : CEntity, new()
        {
            return (TEntity)CreateChild(typeof(TEntity));
        }

        public virtual CEntity AddChild()
        {
            return CreateChild(typeof(CEntity));
        }

        private CEntity CreateChild(Type type)
        {
            int entityId;
            if (!m_RecycledEntityIds.TryPop(out entityId))
            {
                entityId = m_EntitySerialId++;
            }

            CEntity entity = (CEntity)Activator.CreateInstance(type);
            entity.SetWorld(this);
            Children.Set(entityId, entity);
            entity.OnDirty(this, entityId);
            return entity;
        }

        public virtual void RemoveChild(CEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            CEntity removed = Children.Remove(entity.Id);
            if (removed == null)
            {
                return;
            }

            removed.Dispose();
            m_RecycledEntityIds.Push(entity.Id);
        }

        public CEntity GetChild(int entityId)
        {
            return Children[entityId];
        }

        private void DisposeChildren()
        {
            if (Children != null)
            {
                foreach (CEntity entity in Children)
                {
                    entity?.Dispose();
                }
            }

            Children?.Dispose();
            Children = null;
            m_RecycledEntityIds.Clear();
        }
    }
}
