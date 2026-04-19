#region

using System;

#endregion

namespace Tool
{
    public abstract class Singleton<T> where T : Singleton<T>
    {
        protected static volatile T m_Instance;

        // ReSharper disable once StaticMemberInGenericType
        private static readonly object m_Lock = new object();

        public static T Instance
        {
            get
            {
                if (m_Instance != null)
                {
                    m_Instance.OnAccess();
                    return m_Instance;
                }

                lock (m_Lock)
                {
                    // 双重检查
                    if (m_Instance != null)
                    {
                        m_Instance.OnAccess();
                        return m_Instance;
                    }

                    m_Instance = m_Instance = (T)Activator.CreateInstance(typeof(T), true);
                    return m_Instance;
                }
            }
        }

        /// <summary>
        ///     每次访问实例前，触发此回调
        ///     首次创建实例时，不会触发(若需要触发，可以在其构造函数内手动调用此接口)
        /// </summary>
        protected virtual void OnAccess()
        {
        }

        public static void Dispose()
        {
            lock (m_Lock)
            {
                m_Instance = null;
            }
        }
    }
}