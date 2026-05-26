namespace MainGameCharacterMorphBridge
{
    public static class CharacterMorphBridgeApi
    {
        public static bool IsAvailable => Plugin.Instance != null;

        public static bool CaptureOriginal(int femaleIndex = -1)
        {
            return Plugin.Instance != null && Plugin.Instance.CaptureOriginal(femaleIndex, "api");
        }

        public static bool LoadTargetCard(string cardPath, int femaleIndex = -1)
        {
            return Plugin.Instance != null && Plugin.Instance.LoadTargetCard(cardPath, femaleIndex, "api");
        }

        public static bool LoadTargetCardByWord(string word, int femaleIndex = -1)
        {
            return Plugin.Instance != null && Plugin.Instance.LoadTargetCardByWord(word, femaleIndex, "api");
        }

        public static bool RegisterCard(string word, string cardPath, string triggerWords = "")
        {
            return Plugin.Instance != null && Plugin.Instance.RegisterCard(word, cardPath, triggerWords, "api");
        }

        public static string[] GetRegisteredCardWords()
        {
            return Plugin.Instance != null ? Plugin.Instance.GetRegisteredCardWords() : new string[0];
        }

        public static bool SetBlend(float blend, int femaleIndex = -1)
        {
            return Plugin.Instance != null && Plugin.Instance.ApplyBlend(blend, femaleIndex, "api");
        }

        public static bool BlendTo(float targetBlend, float seconds, int femaleIndex = -1)
        {
            return Plugin.Instance != null && Plugin.Instance.BlendTo(targetBlend, seconds, femaleIndex, "api");
        }

        public static bool BlendToCardWord(string word, float targetBlend, float seconds, int femaleIndex = -1)
        {
            return Plugin.Instance != null && Plugin.Instance.BlendToCardWord(word, targetBlend, seconds, femaleIndex, "api");
        }

        public static bool BlendToCardPath(string cardPath, float targetBlend, float seconds, int femaleIndex = -1)
        {
            return Plugin.Instance != null && Plugin.Instance.BlendToCardPath(cardPath, targetBlend, seconds, femaleIndex, "api");
        }

        public static bool TryResolveRegisteredCardFromText(string text, out string word, out string cardPath)
        {
            word = string.Empty;
            cardPath = string.Empty;
            return Plugin.Instance != null && Plugin.Instance.TryResolveRegisteredCardFromText(text, out word, out cardPath);
        }

        public static bool SetBodyShape(int shapeIndex, float value, int femaleIndex = -1)
        {
            return Plugin.Instance != null && Plugin.Instance.SetBodyShape(shapeIndex, value, femaleIndex, "api");
        }

        public static bool SetFaceShape(int shapeIndex, float value, int femaleIndex = -1)
        {
            return Plugin.Instance != null && Plugin.Instance.SetFaceShape(shapeIndex, value, femaleIndex, "api");
        }

        public static bool SetHeight(float value, int femaleIndex = -1)
        {
            return SetBodyShape(0, value, femaleIndex);
        }

        public static bool SetBreast(float value, int femaleIndex = -1)
        {
            return SetBodyShape(4, value, femaleIndex);
        }

        public static bool ResetToOriginal(int femaleIndex = -1)
        {
            return Plugin.Instance != null && Plugin.Instance.ResetToOriginal(femaleIndex, "api");
        }
    }
}
