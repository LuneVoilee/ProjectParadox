using System;
using System.Collections;
using System.Collections.Generic;

namespace Core.Json
{
    public interface ICoroutine
    {
        bool IsFinished { get; }

        bool Update();

        event Action OnFinish;
    }

    public interface ICoroutineModule
    {
        ICoroutine StartCoroutine(IEnumerator enumerator);

        void StopCoroutine(ICoroutine coroutine);

        ICoroutine StartRLCoroutine(IEnumerator enumerator);

        void StopRLCoroutine(ICoroutine coroutine);

        ICoroutine CreateCoroutine(IEnumerator routine);
    }

    public static class CoroutineModule
    {
        public static readonly ICoroutineModule Instance = new CoroutineModuleImpl();
    }

    internal sealed class CoroutineModuleImpl : ICoroutineModule
    {
        public ICoroutine StartCoroutine(IEnumerator enumerator)
        {
            return new SimpleCoroutine(enumerator);
        }

        public void StopCoroutine(ICoroutine coroutine)
        {
            if (coroutine is SimpleCoroutine simple)
            {
                simple.ForceFinish();
            }
        }

        public ICoroutine StartRLCoroutine(IEnumerator enumerator)
        {
            return new SimpleCoroutine(enumerator);
        }

        public void StopRLCoroutine(ICoroutine coroutine)
        {
            if (coroutine is SimpleCoroutine simple)
            {
                simple.ForceFinish();
            }
        }

        public ICoroutine CreateCoroutine(IEnumerator routine)
        {
            return new SimpleCoroutine(routine);
        }
    }

    internal sealed class SimpleCoroutine : ICoroutine
    {
        private readonly Stack<IEnumerator> m_Stack = new Stack<IEnumerator>();
        private bool m_IsUpdating;

        public event Action OnFinish;

        public bool IsFinished => m_Stack.Count == 0;

        public SimpleCoroutine(IEnumerator rootEnumerator)
        {
            if (rootEnumerator != null)
            {
                m_Stack.Push(rootEnumerator);
            }
        }

        public void ForceFinish()
        {
            m_Stack.Clear();
            OnFinish?.Invoke();
            OnFinish = null;
        }

        public bool Update()
        {
            if (m_IsUpdating)
            {
                throw new Exception("调用链错误: 同一协程被循环调用");
            }

            if (m_Stack.Count == 0)
            {
                return false;
            }

            m_IsUpdating = true;
            bool wasRunning = m_Stack.Count > 0;

            try
            {
                while (m_Stack.Count > 0)
                {
                    IEnumerator top = m_Stack.Peek();
                    bool moved;
                    try
                    {
                        moved = top.MoveNext();
                    }
                    catch
                    {
                        m_Stack.Clear();
                        throw;
                    }

                    if (!moved)
                    {
                        m_Stack.Pop();
                        continue;
                    }

                    if (top.Current is IEnumerator next)
                    {
                        m_Stack.Push(next);
                        continue;
                    }

                    break;
                }
            }
            finally
            {
                m_IsUpdating = false;
            }

            bool isRunning = m_Stack.Count > 0;
            if (!isRunning && wasRunning)
            {
                OnFinish?.Invoke();
                OnFinish = null;
            }

            return isRunning;
        }
    }

    public static class JsonCoroutineUtil
    {
        public static ICoroutine ToCoroutine(this IEnumerator e)
        {
            return CoroutineModule.Instance.CreateCoroutine(e);
        }

        public static ICoroutine Schedule(this IEnumerator e)
        {
            return CoroutineModule.Instance.StartCoroutine(e);
        }

        public static void Cancel(this ICoroutine c)
        {
            CoroutineModule.Instance.StopCoroutine(c);
        }

        public static ICoroutine Start(Func<IEnumerator> coroutine)
        {
            return coroutine().Schedule();
        }

        public static ICoroutine ScheduleRL(this IEnumerator e)
        {
            return CoroutineModule.Instance.StartRLCoroutine(e);
        }

        public static void CancelRL(this ICoroutine c)
        {
            CoroutineModule.Instance.StopRLCoroutine(c);
        }

        public static ICoroutine StartRL(Func<IEnumerator> coroutine)
        {
            return coroutine().ScheduleRL();
        }

        public static IEnumerator Wait(float seconds)
        {
            float waitUntil = UnityEngine.Time.time + seconds;
            while (waitUntil > UnityEngine.Time.time)
            {
                yield return null;
            }
        }

        public static void Complete(this IEnumerator e)
        {
            while (e != null && e.MoveNext())
            {
            }
        }

        public static void Complete(this ICoroutine c)
        {
            while (c != null && c.Update())
            {
            }
        }
    }
}
