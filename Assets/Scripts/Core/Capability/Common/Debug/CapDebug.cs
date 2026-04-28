namespace Core.Capability.Editor
{
    public static class CapDebug
    {
        public static void Log(this CapabilityBase cap, string message)
        {
            CapabilityDebugLogBridge.Add(cap, message);
        }
    }
}