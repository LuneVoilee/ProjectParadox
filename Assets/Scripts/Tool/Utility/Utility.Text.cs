#region

using System;

#endregion

namespace Tool
{
    public static partial class Utility
    {
        /// <summary>
        ///     字符相关的实用函数。
        /// </summary>
        public static class Text
        {
            /// <summary>
            ///     根据类型和名称获取完整名称。
            /// </summary>
            /// <typeparam name="T">类型。</typeparam>
            /// <param name="name">名称。</param>
            /// <returns>完整名称。</returns>
            public static string GetFullName<T>(string name)
            {
                return GetFullName(typeof(T), name);
            }

            /// <summary>
            ///     根据类型和名称获取完整名称。
            /// </summary>
            /// <param name="type">类型。</param>
            /// <param name="name">名称。</param>
            /// <returns>完整名称。</returns>
            public static string GetFullName(Type type, string name)
            {
                if (type == null)
                {
                    throw new Exception("Type is invalid.");
                }

                string typeName = type.FullName;
                return string.IsNullOrEmpty(name) ? typeName : $"{typeName}.{name}";
            }
        }

        /// <summary>
        ///     检查字符串非空
        /// </summary>
        public static bool IsNotEmpty(this string str)
        {
            return !string.IsNullOrEmpty(str);
        }

        /// <summary>
        ///     默认的比较方式开销很大！Ordinal比较是最快的比较操作，因为它在确定结果时不应用任何语言规则
        ///     https://learn.microsoft.com/en-us/dotnet/standard/base-types/best-practices-strings
        /// </summary>
        /// <param name="str"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool StartsWithOrdinal(this string str, string value)
        {
            return str.StartsWith(value, StringComparison.Ordinal);
        }

        /// <summary>
        ///     默认的比较方式开销很大！Ordinal比较是最快的比较操作，因为它在确定结果时不应用任何语言规则
        ///     https://learn.microsoft.com/en-us/dotnet/standard/base-types/best-practices-strings
        /// </summary>
        /// <param name="str"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool StartsWithOrdinalIgnoreCase(this string str, string value)
        {
            return str.StartsWith(value, StringComparison.OrdinalIgnoreCase);
        }

        public static bool StartsWithOrdinal(this string str, char value)
        {
            return str.StartsWith(value);
        }

        /// <summary>
        ///     默认的比较方式开销很大！Ordinal比较是最快的比较操作，因为它在确定结果时不应用任何语言规则
        ///     https://learn.microsoft.com/en-us/dotnet/standard/base-types/best-practices-strings
        /// </summary>
        /// <param name="str"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool EndsWithOrdinal(this string str, string value)
        {
            return str.EndsWith(value, StringComparison.Ordinal);
        }

        public static bool EndsWithOrdinal(this string str, char value)
        {
            return str.EndsWith(value);
        }

        /// <summary>
        ///     默认的比较方式开销很大！Ordinal比较是最快的比较操作，因为它在确定结果时不应用任何语言规则
        ///     https://learn.microsoft.com/en-us/dotnet/standard/base-types/best-practices-strings
        /// </summary>
        /// <param name="str"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool EndsWithOrdinalIgnoreCase(this string str, string value)
        {
            return str.EndsWith(value, StringComparison.OrdinalIgnoreCase);
        }
    }
}