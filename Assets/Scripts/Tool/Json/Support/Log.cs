using System;
using UnityEngine;

namespace Tool.Json
{
    internal static class Log
    {
        public static void Debug(string message)
        {
            UnityEngine.Debug.Log(message);
        }

        public static void Info(string message)
        {
            UnityEngine.Debug.Log(message);
        }

        public static void Warn(string message)
        {
            UnityEngine.Debug.LogWarning(message);
        }

        public static void Error(string message)
        {
            UnityEngine.Debug.LogError(message);
        }

        public static void Exception(Exception exception)
        {
            UnityEngine.Debug.LogException(exception);
        }

        public static void Exception(string format, Exception exception)
        {
            if (string.IsNullOrEmpty(format))
            {
                UnityEngine.Debug.LogException(exception);
                return;
            }

            string message = format.Contains("{0}", StringComparison.Ordinal)
                ? string.Format(format, exception.Message)
                : format;
            UnityEngine.Debug.LogError(message);
            UnityEngine.Debug.LogException(exception);
        }
    }
}
