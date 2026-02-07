using System;
using System.Collections.Generic;

namespace GamePlay.CapabilityFramework
{
    /// <summary>
    /// Capability 调度器。
    ///
    /// 负责：
    /// - 根据 Phase 过滤 Capability；
    /// - 按 Priority 排序；
    /// - 驱动生命周期（Activate / Tick / Deactivate）。
    ///
    /// 注意：
    /// - 它不关心具体玩法，只做通用调度；
    /// - 玩法在 Capability 子类里实现。
    /// </summary>
    public sealed class CapabilityScheduler
    {
        private readonly List<Capability> m_Capabilities = new List<Capability>();
        private readonly List<Capability> m_TickBuffer = new List<Capability>();

        private bool m_IsDirty = true;

        public int Count => m_Capabilities.Count;

        public void Add(Capability capability)
        {
            if (capability == null || m_Capabilities.Contains(capability))
            {
                return;
            }

            m_Capabilities.Add(capability);
            m_IsDirty = true;
        }

        public void Remove(Capability capability)
        {
            if (capability == null)
            {
                return;
            }

            if (m_Capabilities.Remove(capability))
            {
                m_IsDirty = true;
            }
        }

        public void Clear()
        {
            m_Capabilities.Clear();
            m_TickBuffer.Clear();
            m_IsDirty = false;
        }

        /// <summary>
        /// 调度指定 Phase。
        /// </summary>
        /// <param name="phase">当前流程阶段</param>
        /// <param name="deltaTime">时间步长</param>
        public void Tick(CapabilityPhase phase, float deltaTime)
        {
            if (m_IsDirty)
            {
                SortByPriority();
            }

            // 先收集后执行，避免 Tick 中途改列表造成迭代问题。
            m_TickBuffer.Clear();
            for (int i = 0; i < m_Capabilities.Count; i++)
            {
                var capability = m_Capabilities[i];
                if (capability == null)
                {
                    continue;
                }

                if (capability.Phase == CapabilityPhase.Always || capability.Phase == phase)
                {
                    m_TickBuffer.Add(capability);
                }
            }

            for (int i = 0; i < m_TickBuffer.Count; i++)
            {
                m_TickBuffer[i].SchedulerTick(deltaTime);
            }
        }

        private void SortByPriority()
        {
            m_Capabilities.Sort(CompareCapability);
            m_IsDirty = false;
        }

        private static int CompareCapability(Capability a, Capability b)
        {
            if (ReferenceEquals(a, b))
            {
                return 0;
            }

            if (a == null)
            {
                return 1;
            }

            if (b == null)
            {
                return -1;
            }

            int phaseCompare = a.Phase.CompareTo(b.Phase);
            if (phaseCompare != 0)
            {
                return phaseCompare;
            }

            int priorityCompare = a.Priority.CompareTo(b.Priority);
            if (priorityCompare != 0)
            {
                return priorityCompare;
            }

            return string.CompareOrdinal(a.GetType().Name, b.GetType().Name);
        }
    }
}
