using UnityEngine;

namespace Core.Capability
{
    /// <summary>
    ///     Installer 非泛型基类，供 FindObjectsOfType 发现所有 Installer。
    /// </summary>
    public abstract class EntityInstaller : MonoBehaviour
    {
        /// <summary>此 Installer 创建的实体。</summary>
        public CEntity Entity { get; protected set; }

        /// <summary>关联的场景 Transform。</summary>
        public Transform EntityTransform => transform;
    }

    /// <summary>
    ///     Installer 泛型基类，提供类型安全的实体访问。
    /// </summary>
    public abstract class EntityInstaller<TEntity> : EntityInstaller
        where TEntity : CEntity
    {
        /// <summary>类型安全的实体访问器。</summary>
        public new TEntity Entity
        {
            get => (TEntity)base.Entity;
            protected set => base.Entity = value;
        }
    }
}
