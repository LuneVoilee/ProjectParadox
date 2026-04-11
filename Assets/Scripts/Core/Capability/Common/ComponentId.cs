using System;
using System.Collections.Generic;

namespace Core.Capability
{
    internal static class ComponentIdGenerator
    {
        private static int m_Next;

        public static int GetId<TComponent>() where TComponent : CComponent
        {
            ComponentTypeRegistry.ComponentTypes.Add(typeof(TComponent));
            return m_Next++;
        }
    }

    public static class ComponentId<TComponent> where TComponent : CComponent
    {
        public static readonly int TId = ComponentIdGenerator.GetId<TComponent>();
    }

    public static class ComponentTypeRegistry
    {
        public static readonly List<Type> ComponentTypes = new List<Type>(128);

        public static int Count => ComponentTypes.Count;
    }
}
