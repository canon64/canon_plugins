using System;
using BepInEx.Configuration;

namespace MainGameAiShoujoMode
{
    public sealed partial class Plugin
    {
        private ConfigEntry<bool> _aiConversationEnabled;
        private ConfigEntry<string> _aiConversationMaterials;
        internal static string AiConversationMaterials => _instance._aiConversationMaterials.Value;
        internal static bool AiConversationEnabled => _instance?._aiConversationEnabled?.Value == true;
        internal static event Action AiConversationSettingChanged;
        private void InitializeAiConversation()
        {
            _aiConversationMaterials = Config.Bind("AIConversation", "MaterialsDirectory", "F:/kks/UserData/AIShoujoMode/AIConversation",
                "利用者が編集するai-instructions.jsonとai-reference.jsonの場所。自動上書きしません。");
            _aiConversationEnabled = Config.Bind("AIConversation", "Enabled", false,
                "Human_2とVoiceFaceで会話する。Human_2側のAI少女会話受付も有効にしてください。OFFでは専用接続と録音を開始しません。");
            _aiConversationEnabled.SettingChanged += AiConversationChanged;
        }
        private void AiConversationChanged(object sender, EventArgs args) => AiConversationSettingChanged?.Invoke();
        private void DisposeAiConversation()
        { if (_aiConversationEnabled != null) _aiConversationEnabled.SettingChanged -= AiConversationChanged; }
    }
}
