using System;

namespace Core.Capability
{
    public interface IEntity : IDisposable
    {
        enum EntityState
        {
            Running,
            Cleared
        }

        IEntity Parent { get; }
        int Id { get; }
        string Name { get; set; }
        int Version { get; }
        EntityState State { get; }
        bool IsActive { get; }
        void OnDirty(IEntity parent, int id);
    }

    public interface ISystem
    {
    }

    public interface IWorldSystem : ISystem
    {
    }

    public interface IUpdateSystem : ISystem
    {
        void OnUpdate(float elapsedSeconds, float realElapsedSeconds);
    }

    public interface IFixedUpdateSystem : ISystem
    {
        void OnFixedUpdate(float elapsedSeconds, float realElapsedSeconds);
    }

    public interface IInitializeSystem : ISystem
    {
        void OnInitialize();
    }

    public interface IInitializeSystem<in T1> : ISystem
    {
        void OnInitialize(T1 p1);
    }

    public interface IInitializeSystem<in T1, in T2> : ISystem
    {
        void OnInitialize(T1 p1, T2 p2);
    }

    public interface IInitializeSystem<in T1, in T2, in T3> : ISystem
    {
        void OnInitialize(T1 p1, T2 p2, T3 p3);
    }
}
