#region

using System.Collections.Generic;

#endregion

namespace Tool
{
    public static partial class Utility
    {
        #region Dictionary

        public static TValue GetOrAdd<TKey, TValue>
        (
            this Dictionary<TKey, TValue> dict, TKey key,
            TValue defaultVal = default
        )
        {
            if (!dict.TryGetValue(key, out TValue value))
            {
                value = defaultVal;
                dict.Add(key, value);
            }

            return value;
        }

        public static TValue GetOrNew<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key)
            where TValue : new()
        {
            TValue value = default;
            if (dict != null && !dict.TryGetValue(key, out value))
            {
                value = new TValue();
                dict.Add(key, value);
            }

            return value;
        }

        #endregion
    }
}