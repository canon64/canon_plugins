using System.Collections.Generic;
using System.Runtime.Serialization;

namespace MainGameBlankMapAdd
{
    [DataContract]
    internal sealed class PluginSettings
    {
        private static int NormalizeCubeFaceTileCount(int value)
        {
            switch (value)
            {
                case 1:
                case 4:
                case 9:
                case 16:
                case 25:
                    return value;
                default:
                    return 1;
            }
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext ctx)
        {
            if (AddedThumbnailID <= 0) AddedThumbnailID = 9000;
            if (VideoPath == null) VideoPath = string.Empty;
            if (FloorOverrideVideoPath == null) FloorOverrideVideoPath = string.Empty;
            if (CeilingOverrideVideoPath == null) CeilingOverrideVideoPath = string.Empty;
            if (RoomWidth  <= 0.01f) RoomWidth  = 12f;
            if (RoomDepth  <= 0.01f) RoomDepth  = 12f;
            if (RoomHeight <= 0.01f) RoomHeight = 6f;
            if (VideoVolume < 0f)    VideoVolume = 0.5f;
            if (SphereRadius <= 0.01f) SphereRadius = 4f;
            if (string.IsNullOrWhiteSpace(VoiceReverbPreset)) VoiceReverbPreset = "Cave";
            if (VoiceReverbMinDistance < 0f) VoiceReverbMinDistance = 1.5f;
            if (VoiceReverbMaxDistance <= VoiceReverbMinDistance)
                VoiceReverbMaxDistance = VoiceReverbMinDistance + 8f;
            if (PlaybackBarShowMouseBottomPx < 0f) PlaybackBarShowMouseBottomPx = 20f;
            if (PlaybackBarHeight < 20f) PlaybackBarHeight = 72f;
            if (PlaybackBarMarginX < 0f) PlaybackBarMarginX = 8f;
            if (PlaybackBarButtonWidth < 36f) PlaybackBarButtonWidth = 64f;
            CubeFaceTileCount = NormalizeCubeFaceTileCount(CubeFaceTileCount);
            if (FolderFadeDuration < 0.01f) FolderFadeDuration = 1.0f;
            if (HttpPort <= 0 || HttpPort > 65535) HttpPort = 55782;
            FolderPlayEnabled = true;
            if (FolderPlayPath == null) FolderPlayPath = string.Empty;
            if (FolderPlayPaths == null) FolderPlayPaths = new List<string>();
            if (float.IsNaN(VideoAudioGain) || float.IsInfinity(VideoAudioGain) || VideoAudioGain <= 0f)
                VideoAudioGain = 1f;
            if (!string.IsNullOrWhiteSpace(FolderPlayPath))
            {
                bool exists = false;
                for (int i = 0; i < FolderPlayPaths.Count; i++)
                {
                    if (string.Equals(FolderPlayPaths[i], FolderPlayPath, System.StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                    FolderPlayPaths.Add(FolderPlayPath);
            }
        }

        [DataMember(Order = 0)]
        public int AddedMapNo = 900;

        [DataMember(Order = 1)]
        public int SourceMapNo = 1;

        [DataMember(Order = 2)]
        public string AddedMapName = "blank_test_900";

        [DataMember(Order = 3)]
        public string AddedDisplayName = "Blank Test";

        [DataMember(Order = 4)]
        public int AddedSort = 900;

        [DataMember(Order = 5)]
        public bool ForceIsGate = true;

        [DataMember(Order = 6)]
        public bool ForceIsFreeH = true;

        [DataMember(Order = 7)]
        public bool ForceIsH = false;

        [DataMember(Order = 8)]
        public bool BlankifySceneOnLoad = true;

        [DataMember(Order = 9)]
        public bool DisableRenderers = true;

        [DataMember(Order = 10)]
        public bool DisableTerrains = true;

        [DataMember(Order = 11)]
        public bool DisableLights = false;

        [DataMember(Order = 12)]
        public bool DisableParticles = true;

        [DataMember(Order = 13)]
        public bool VerboseLog = true;

        [DataMember(Order = 14)]
        public int AddedThumbnailID = 9000;

        [DataMember(Order = 15)]
        public bool EnableVideoRoom = true;

        [DataMember(Order = 16)]
        public string VideoPath = "";

        [DataMember(Order = 17)]
        public bool UseFloorVideoOverride = false;

        [DataMember(Order = 18)]
        public string FloorOverrideVideoPath = "";

        [DataMember(Order = 19)]
        public bool UseCeilingVideoOverride = false;

        [DataMember(Order = 20)]
        public string CeilingOverrideVideoPath = "";

        [DataMember(Order = 21)]
        public bool VideoLoop = true;

        [DataMember(Order = 22)]
        public bool MuteVideoAudio = true;

        [DataMember(Order = 26)]
        public bool AutoPlayOnMapLoad = false;

        [DataMember(Order = 23)]
        public float RoomWidth = 12f;

        [DataMember(Order = 24)]
        public float RoomDepth = 12f;

        [DataMember(Order = 25)]
        public float RoomHeight = 6f;

        [DataMember(Order = 27)]
        public float VideoVolume = 0.5f;

        [DataMember(Order = 28)]
        public bool DisableAudioSources = false;

        [DataMember(Order = 29)]
        public float VideoRoomOffsetX = 0f;

        [DataMember(Order = 30)]
        public float VideoRoomOffsetY = -1f;

        [DataMember(Order = 31)]
        public float VideoRoomOffsetZ = 0f;

        [DataMember(Order = 32)]
        public float VideoRoomRotationX = 0f;

        [DataMember(Order = 33)]
        public float VideoRoomRotationY = 0f;

        [DataMember(Order = 34)]
        public float VideoRoomRotationZ = 0f;

        /// <summary>true=球体、false=平面(Quad)</summary>
        [DataMember(Order = 35)]
        public bool UseSphere = true;

        [DataMember(Order = 36)]
        public float SphereRadius = 4f;

        [DataMember(Order = 37)]
        public bool EnableVoiceReverb = true;

        [DataMember(Order = 38)]
        public string VoiceReverbPreset = "Cave";

        [DataMember(Order = 39)]
        public float VoiceReverbMinDistance = 1.5f;

        [DataMember(Order = 40)]
        public float VoiceReverbMaxDistance = 18f;

        /// <summary>true=球体の内側面に表示、false=外側面に表示</summary>
        [DataMember(Order = 41)]
        public bool SphereInsideView = true;

        [DataMember(Order = 42)]
        public bool EnablePlaybackBar = true;

        [DataMember(Order = 43)]
        public float PlaybackBarShowMouseBottomPx = 20f;

        [DataMember(Order = 44)]
        public float PlaybackBarHeight = 72f;

        [DataMember(Order = 45)]
        public float PlaybackBarMarginX = 8f;

        [DataMember(Order = 46)]
        public float PlaybackBarButtonWidth = 64f;

        [DataMember(Order = 47)]
        public int CubeFaceTileCount = 1;

        [DataMember(Order = 50)]
        public bool FolderPlayEnabled = true;

        [DataMember(Order = 51)]
        public string FolderPlayPath = "";

        [DataMember(Order = 59)]
        public List<string> FolderPlayPaths = new List<string>();

        [DataMember(Order = 52)]
        public bool FolderPlayLoop = true;

        [DataMember(Order = 58)]
        public bool FolderPlaySingleLoop = false;

        /// <summary>"Name" or "Date"</summary>
        [DataMember(Order = 53)]
        public string FolderPlaySortMode = "Name";

        [DataMember(Order = 54)]
        public bool FolderPlaySortAscending = true;

        /// <summary>フォルダ再生時の動画切り替えクロスフェード時間（秒）。0で即時切替。</summary>
        [DataMember(Order = 55)]
        public float FolderFadeDuration = 1.0f;

        /// <summary>外部HTTP受信を有効にする</summary>
        [DataMember(Order = 56)]
        public bool HttpEnabled = true;

        /// <summary>HTTP受信ポート番号</summary>
        [DataMember(Order = 57)]
        public int HttpPort = 55782;

        /// <summary>再生中Hボイス音源(Voice/PlayObjectPCM)を動画部屋座標へ同期する</summary>
        [DataMember(Order = 60)]
        public bool SyncVoiceSourcesToVideoRoom = true;

        [DataMember(Order = 61)]
        public bool ApplyReverbToVideoAudio = false;

        /// <summary>動画音声の追加ゲイン倍率（最終適用はDualMonoFilter側で処理）</summary>
        [DataMember(Order = 62)]
        public float VideoAudioGain = 1f;

        /// <summary>再生バーの説明ポップアップ表示</summary>
        [DataMember(Order = 63)]
        public bool EnableUiHelpPopup = true;
    }
}
