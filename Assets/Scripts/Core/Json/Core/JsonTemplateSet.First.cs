#region

using System.Linq;
using UnityEngine;

#endregion

namespace Core.Json
{
    public partial class JsonTemplateSet<TSelf, TTemplate>
        where TSelf : JsonTemplateSet<TSelf, TTemplate>
        where TTemplate : class
    {
        protected virtual object DefaultTemplateKey => null;

        private TTemplate m_Default;

        public TTemplate Default
        {
            get
            {
                if (m_Default != null)
                {
                    return m_Default;
                }

                if (AllTemplates.Values.Count == 0)
                {
                    Debug.LogError($"Template set [{typeof(TTemplate).FullName}] count is zero!");
                    return null;
                }

                m_Default = DefaultTemplateKey != null
                    ? GetTemplate(DefaultTemplateKey)
                    : AllTemplates.Values.First();
                return m_Default;
            }
        }
    }
}