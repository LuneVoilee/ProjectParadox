#region

using System;
using Newtonsoft.Json;

#endregion

namespace Tool.Json
{
    public abstract partial class JsonTemplateSet<TSelf, TTemplate>
        where TSelf : JsonTemplateSet<TSelf, TTemplate>
        where TTemplate : class
    {
        public class ReferenceCvt : JsonConverter
        {
            public override void WriteJson
                (JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (value is TTemplate template)
                {
                    writer.WriteValue(GetPrimaryKey(template));
                }
                else if (value is Ref r)
                {
                    writer.WriteValue(r.Key);
                }
                else
                {
                    writer.WriteNull();
                }
            }

            public override object ReadJson
            (
                JsonReader reader,
                Type objectType,
                object existingValue,
                JsonSerializer serializer
            )
            {
                object primaryKey = reader.Value;
                if (primaryKey == null)
                {
                    return null;
                }

                switch (primaryKey)
                {
                    case string strKey when string.IsNullOrWhiteSpace(strKey):
                    case int intKey when intKey < 0:
                        return null;
                }

                if (objectType == typeof(Ref))
                {
                    return new Ref(primaryKey);
                }

                if (ms_IsLoading)
                {
                    throw new Exception($"循环依赖: {typeof(TSelf)} 还在加载");
                }

                return Instance.GetTemplate(primaryKey);
            }

            public override bool CanConvert(Type objectType)
            {
                return typeof(TTemplate) == objectType || typeof(Ref) == objectType;
            }
        }

        public class Ref
        {
            private TTemplate m_Template;
            private bool m_HasQuery;

            public Ref(object key)
            {
                Key = key;
                m_Template = null;
                m_HasQuery = false;
            }

            public object Key { get; }

            public TTemplate Get()
            {
                Query();
                return m_Template;
            }

            public override string ToString()
            {
                return m_Template?.ToString() ?? Key?.ToString() ?? string.Empty;
            }

            public static implicit operator TTemplate(Ref r)
            {
                if (r == null)
                {
                    return null;
                }

                r.Query();
                return r.m_Template;
            }

            public static implicit operator Ref(TTemplate t)
            {
                return new Ref(GetPrimaryKey(t))
                {
                    m_Template = t
                };
            }

            public static implicit operator bool(Ref r)
            {
                if (r == null)
                {
                    return false;
                }

                r.Query();
                return r.m_Template != null;
            }

            public static bool operator ==(Ref r, TTemplate t)
            {
                return r?.Get() == t;
            }

            public static bool operator !=(Ref r, TTemplate t)
            {
                return !(r == t);
            }

            private void Query()
            {
                if (m_Template != null || m_HasQuery)
                {
                    return;
                }

                Instance.TryGetTemplate(Key, out m_Template);
                m_HasQuery = true;
            }

            protected bool Equals(Ref other)
            {
                return Equals(Key, other.Key);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj))
                {
                    return false;
                }

                if (ReferenceEquals(this, obj))
                {
                    return true;
                }

                if (obj.GetType() != GetType())
                {
                    return false;
                }

                return Equals((Ref)obj);
            }

            public override int GetHashCode()
            {
                return Key != null ? Key.GetHashCode() : 0;
            }
        }
    }
}