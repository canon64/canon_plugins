using System;

namespace MainGameDisplayEffectViewer
{
    [Serializable]
    public sealed class MainGameDisplayEffectViewerSettings
    {
        public int Version = 1;

        public bool EnablePlugin = true;
        public bool EnableFileLog = true;
        public bool EnableUnityLog = false;
        public bool AutoFlushFileLog = true;

        public bool StartWindowVisible = true;

        public float ResolveIntervalSeconds = 0.5f;
        public float AutoRescanSeconds = 1.0f;

        public bool ShowOnlyActiveByDefault = false;
        public bool InfoOnlyByDefault = false;
        public string InitialSearch = "";
        public int MaxRows = 300;

        public string TestTopicName = "DEV TOOL TOPIC";
        public string TestNormalMessage = "DEV TOOL NORMAL MESSAGE";
    }
}
