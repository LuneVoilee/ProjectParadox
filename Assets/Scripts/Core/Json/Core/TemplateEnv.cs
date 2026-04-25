using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Json
{
    public static class TemplateEnv
    {
        private class EnvTemplate
        {
            private readonly string m_Prefix;
            private readonly List<string> m_Contexts = new List<string>();

            public string FullPath { get; private set; }

            public EnvTemplate(string prefix, string templateName)
            {
                m_Prefix = string.IsNullOrEmpty(prefix) ? string.Empty : $"{prefix}|";
                InternalPushContext(templateName);
            }

            public void InternalPushContext(string name)
            {
                m_Contexts.Add(name);
                FullPath = $"{m_Prefix}{string.Join(".", m_Contexts)}";
            }

            public void InternalPopContext()
            {
                if (m_Contexts.Count > 0)
                {
                    m_Contexts.RemoveAt(m_Contexts.Count - 1);
                }

                FullPath = $"{m_Prefix}{string.Join(".", m_Contexts)}";
            }
        }

        private class Env
        {
            private readonly string m_Prefix;
            private readonly Stack<EnvTemplate> m_EnvTemplates = new Stack<EnvTemplate>();

            public Env(string prefix)
            {
                m_Prefix = prefix;
                m_EnvTemplates.Push(new EnvTemplate(m_Prefix, "None"));
            }

            public string FullContext => m_EnvTemplates.Peek().FullPath;

            public void InternalBeginTemplate(string name)
            {
                m_EnvTemplates.Push(new EnvTemplate(m_Prefix, name));
            }

            public void InternalEndTemplate()
            {
                if (m_EnvTemplates.Count > 1)
                {
                    m_EnvTemplates.Pop();
                }
            }

            public void InternalBeginContext(string name)
            {
                m_EnvTemplates.Peek().InternalPushContext(name);
            }

            public void InternalEndContext()
            {
                m_EnvTemplates.Peek().InternalPopContext();
            }
        }

        private static readonly Stack<Env> m_Envs = new Stack<Env>();

        static TemplateEnv()
        {
            m_Envs.Push(new Env(null));
        }

        public static string GetFullPath()
        {
            return m_Envs.Peek().FullContext;
        }

        public static string GetFullPathStack()
        {
            var sb = new StringBuilder();
            foreach (Env env in m_Envs)
            {
                sb.AppendLine(env.FullContext);
            }

            return sb.ToString();
        }

        public static void BeginEnv(string name)
        {
            m_Envs.Push(new Env(name));
        }

        public static void EndEnv()
        {
            if (m_Envs.Count > 1)
            {
                m_Envs.Pop();
            }
        }

        public static void BeginTemplate(string name)
        {
            m_Envs.Peek().InternalBeginTemplate(name);
        }

        public static void EndTemplate()
        {
            m_Envs.Peek().InternalEndTemplate();
        }

        public static void BeginContext(string name)
        {
            m_Envs.Peek().InternalBeginContext(name);
        }

        public static void EndContext()
        {
            m_Envs.Peek().InternalEndContext();
        }

        public readonly struct EnvScope : IDisposable
        {
            public EnvScope(string name)
            {
                BeginEnv(name);
            }

            public void Dispose()
            {
                EndEnv();
            }

            public static EnvScope Begin(string name)
            {
                return new EnvScope(name);
            }
        }

        public readonly struct TemplateScope : IDisposable
        {
            public TemplateScope(string name)
            {
                BeginTemplate(name);
            }

            public void Dispose()
            {
                EndTemplate();
            }

            public static TemplateScope Begin(string name)
            {
                return new TemplateScope(name);
            }
        }

        public readonly struct ContextScope : IDisposable
        {
            public ContextScope(string name)
            {
                BeginContext(name);
            }

            public void Dispose()
            {
                EndContext();
            }

            public static ContextScope Begin(string name)
            {
                return new ContextScope(name);
            }
        }
    }
}
