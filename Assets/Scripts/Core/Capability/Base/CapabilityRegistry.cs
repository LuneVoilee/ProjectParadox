#region

using System;
using System.Collections.Generic;
using System.Diagnostics;

#endregion

namespace Core.Capability
{
    public partial class CapabilityRegistry
    {
        private readonly List<CapabilityBase> m_GlobalUpdateCapabilities =
            new List<CapabilityBase>(64);

        private readonly List<CapabilityBase> m_GlobalFixedUpdateCapabilities =
            new List<CapabilityBase>(16);

        private readonly CapabilityContext m_Context = new CapabilityContext();

        private readonly CapabilityCommandBuffer m_CommandBuffer =
            new CapabilityCommandBuffer();

        private readonly Stopwatch m_Stopwatch = new Stopwatch();

        private CapabilityWorld m_World;

        public void Init(CapabilityWorld world, int capabilityCount, int estimatedEntityCount)
        {
            m_World = world;
        }

        public void OnUpdate(float deltaTime, float realElapsedSeconds)
        {
            ProcessGlobalCapabilities(m_GlobalUpdateCapabilities, deltaTime, realElapsedSeconds);
        }

        public void OnFixedUpdate(float deltaTime, float realElapsedSeconds)
        {
            ProcessGlobalCapabilities(m_GlobalFixedUpdateCapabilities, deltaTime,
                realElapsedSeconds);
        }

        private void ProcessGlobalCapabilities
        (
            List<CapabilityBase> capabilities, float deltaTime,
            float realElapsedSeconds
        )
        {
            if (capabilities == null || capabilities.Count == 0)
            {
                return;
            }

            for (int i = 0; i < capabilities.Count; i++)
            {
                CapabilityBase capability = capabilities[i];
                if (capability == null)
                {
                    continue;
                }

                m_Context.Reset(m_World, capability, m_CommandBuffer);
                m_CommandBuffer.Reset(m_World, m_Context);
                m_Stopwatch.Restart();
                try
                {
                    capability.Tick(m_Context, deltaTime, realElapsedSeconds);
                }
                catch (Exception exception)
                {
                    m_Context.MarkError(exception);
                    UnityEngine.Debug.LogException(exception);
                }
                finally
                {
                    m_Stopwatch.Stop();
                    capability.LastTickMilliseconds = m_Stopwatch.Elapsed.TotalMilliseconds;
                    m_CommandBuffer.Flush();
                }
            }
        }

        public void Dispose()
        {
            ClearGlobalCapabilities(m_GlobalUpdateCapabilities);
            ClearGlobalCapabilities(m_GlobalFixedUpdateCapabilities);
            m_World = null;
        }

        private void ClearGlobalCapabilities(List<CapabilityBase> capabilities)
        {
            if (capabilities == null)
            {
                return;
            }

            for (int i = 0; i < capabilities.Count; i++)
            {
                capabilities[i]?.Dispose();
            }

            capabilities.Clear();
        }
    }
}
