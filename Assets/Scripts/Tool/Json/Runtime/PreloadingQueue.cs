using System.Collections;
using System.Collections.Generic;

namespace Tool.Json
{
    internal static class PreloadingQueue
    {
        private static readonly Queue<ICoroutine> m_Coroutines = new Queue<ICoroutine>();

        private static bool m_IsProcessing;

        public static bool IsProcessing => m_IsProcessing || m_Coroutines.Count > 0;

        public static void Push(ICoroutine coroutine)
        {
            if (coroutine == null)
            {
                return;
            }

            m_Coroutines.Enqueue(coroutine);

            if (m_IsProcessing)
            {
                return;
            }

            m_IsProcessing = true;
            CoroutineModule.Instance.StartRLCoroutine(CoProcess());
        }

        private static IEnumerator CoProcess()
        {
            while (m_Coroutines.Count > 0)
            {
                ICoroutine coroutine = m_Coroutines.Dequeue();

                while (coroutine != null && coroutine.Update())
                {
                    yield return null;
                }
            }

            m_IsProcessing = false;
        }
    }
}
