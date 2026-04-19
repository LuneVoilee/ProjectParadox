using System;

namespace Tool.Json
{
    public interface IReusable
    {
        void OnUnSpawn();
        void OnSpawn();
    }

    public interface ISpawner
    {
        object Claim();
    }

    public interface IUnSpawner
    {
        void Release(object obj);
    }

    public static class ReusableUtil
    {
        public static void Reset(this IReusable reusable)
        {
            if (reusable == null)
            {
                throw new ArgumentNullException(nameof(reusable));
            }

            reusable.OnUnSpawn();
            reusable.OnSpawn();
        }
    }
}
