using System;
using System.Collections.Generic;
using System.Reflection;

namespace Core.Capability.Editor
{
    /// <summary>
    ///     把 TickGroupOrder 映射成更易读的执行阶段标题。
    /// </summary>
    internal sealed class CapabilityDebugStageResolver
    {
        private readonly List<StageInfo> m_Stages = new List<StageInfo>(16);

        public CapabilityDebugStageResolver()
        {
            LoadStages();
        }

        public string Resolve(int tickGroupOrder)
        {
            if (m_Stages.Count == 0)
            {
                return $"阶段 {tickGroupOrder}";
            }

            StageInfo selected = default;
            bool hasSelected = false;
            for (int i = 0; i < m_Stages.Count; i++)
            {
                StageInfo stage = m_Stages[i];
                if (tickGroupOrder < stage.Value)
                {
                    break;
                }

                selected = stage;
                hasSelected = true;
            }

            return hasSelected ? selected.Name : $"阶段 {tickGroupOrder}";
        }

        private void LoadStages()
        {
            Type orderType = FindType("GamePlay.World.CapabilityOrder");
            if (orderType == null)
            {
                return;
            }

            FieldInfo[] fields = orderType.GetFields(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (!field.IsLiteral || field.FieldType != typeof(int))
                {
                    continue;
                }

                if (!field.Name.StartsWith("Stage", StringComparison.Ordinal))
                {
                    continue;
                }

                m_Stages.Add(new StageInfo
                {
                    Value = (int)field.GetRawConstantValue(),
                    Name = ToDisplayName(field.Name)
                });
            }

            m_Stages.Sort((x, y) => x.Value.CompareTo(y.Value));
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static string ToDisplayName(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName))
            {
                return "阶段";
            }

            if (fieldName.StartsWith("Stage", StringComparison.Ordinal))
            {
                fieldName = fieldName.Substring("Stage".Length);
            }

            if (fieldName.Length == 0)
            {
                return "阶段";
            }

            // 常量名本身就是阶段语义，保持英文名可直接对应源码。
            return fieldName;
        }

        private struct StageInfo
        {
            public int Value;
            public string Name;
        }
    }
}
