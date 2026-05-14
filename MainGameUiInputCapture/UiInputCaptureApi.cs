namespace MainGameUiInputCapture
{
    public static class UiInputCaptureApi
    {
        public static bool IsAvailable => Plugin.Instance != null;

        public static void Sync(string ownerKey, string sourceKey, bool active)
        {
            Plugin.Instance?.Runtime.Sync(ownerKey, sourceKey, active);
        }

        public static bool Begin(string ownerKey, string sourceKey)
        {
            return Plugin.Instance != null && Plugin.Instance.Runtime.Begin(ownerKey, sourceKey);
        }

        public static bool Tick(string ownerKey, string sourceKey)
        {
            return Plugin.Instance != null && Plugin.Instance.Runtime.Tick(ownerKey, sourceKey);
        }

        public static bool End(string ownerKey, string sourceKey)
        {
            return Plugin.Instance != null && Plugin.Instance.Runtime.End(ownerKey, sourceKey);
        }

        public static int EndOwner(string ownerKey)
        {
            return Plugin.Instance == null ? 0 : Plugin.Instance.Runtime.EndOwner(ownerKey);
        }

        public static bool SetIdleCursorUnlock(string ownerKey, bool enabled)
        {
            return Plugin.Instance != null && Plugin.Instance.Runtime.SetIdleCursorUnlock(ownerKey, enabled);
        }

        public static bool IsOwnerActive(string ownerKey)
        {
            return Plugin.Instance != null && Plugin.Instance.Runtime.IsOwnerActive(ownerKey);
        }

        public static void SetOwnerDebug(string ownerKey, bool enabled)
        {
            Plugin.Instance?.SetOwnerDebug(ownerKey, enabled);
        }

        public static bool IsAnyActive()
        {
            return Plugin.Instance != null && Plugin.Instance.Runtime.IsAnyActive;
        }

        public static string GetStateSummary()
        {
            return Plugin.Instance == null ? "plugin-unavailable" : Plugin.Instance.Runtime.GetStateSummary();
        }
    }
}
