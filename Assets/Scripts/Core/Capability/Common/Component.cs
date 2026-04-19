#region

using System;
using System.Collections.Generic;

#endregion

namespace Core.Capability
{
    internal static class ComponentIdGenerator
    {
        private static int m_Next;
        private static readonly Dictionary<Type, int> m_TypeToId = new Dictionary<Type, int>(128);

        public static int GetId<TComponent>() where TComponent : CComponent
        {
            ComponentTypeRegistry.ComponentTypes.Add(typeof(TComponent));
            int id = m_Next++;
            m_TypeToId[typeof(TComponent)] = id;
            return id;
        }

        public static int GetId(Type componentType)
        {
            if (componentType == null)
            {
                throw new ArgumentNullException(nameof(componentType));
            }

            if (m_TypeToId.TryGetValue(componentType, out int id))
            {
                return id;
            }

            if (!typeof(CComponent).IsAssignableFrom(componentType))
            {
                throw new ArgumentException($"Type is not CComponent: {componentType.FullName}", nameof(componentType));
            }

            ComponentTypeRegistry.ComponentTypes.Add(componentType);
            id = m_Next++;
            m_TypeToId[componentType] = id;
            return id;
        }
    }

    public static class Component<TComponent> where TComponent : CComponent
    {
        public static readonly int TId = ComponentIdGenerator.GetId<TComponent>();
    }

    public static class ComponentTypeRegistry
    {
        public static readonly List<Type> ComponentTypes = new List<Type>(128);

        public static int Count => ComponentTypes.Count;
    }
}
