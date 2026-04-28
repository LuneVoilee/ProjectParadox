namespace Core.Capability.Editor
{
    public static class CapDebug
    {
        public static void Log(this CapabilityBase cap, string message)
        {
#if UNITY_EDITOR
            CapabilityDebugLogBridge.Add(cap, message);
#endif
        }
    }
}