using System;
using System.IO;
using UnityEngine;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private void SaveFolderRoomLayoutProfileFromBar()
        {
            if (_settings == null || _roomLayoutProfiles == null)
            {
                LogWarn("[ui] SaveRoom(folder) ignored: settings/profile store unavailable");
                return;
            }

            string folderKey = NormalizeFolderPathForList(_settings.FolderPlayPath);
            if (string.IsNullOrWhiteSpace(folderKey))
            {
                LogWarn("[ui] SaveRoom(folder) ignored: folder path is empty");
                return;
            }

            RoomLayoutProfile profile = CaptureCurrentRoomLayoutProfile(includeAudioGain: false, markAsRoomLayout: true);
            if (_roomLayoutProfiles.TryGetFolder(folderKey, out RoomLayoutProfile existing))
                CopySectionValues(existing, profile);

            _roomLayoutProfiles.SetFolder(folderKey, profile);
            PersistRoomLayoutProfiles();
            ApplyRoomLayoutProfile(profile, persistSettings: true, applyToRoomTransform: true);

            LogInfo(
                $"[ui] room saved scope=folder key={folderKey} " +
                $"scale={profile.Scale:F3} " +
                $"offset=({profile.OffsetX:F3},{profile.OffsetY:F3},{profile.OffsetZ:F3}) " +
                $"rot=({profile.RotationX:F1},{profile.RotationY:F1},{profile.RotationZ:F1})");
        }

        private void SaveVideoRoomLayoutProfileFromBar()
        {
            if (_settings == null || _roomLayoutProfiles == null)
            {
                LogWarn("[ui] SaveRoom(video) ignored: settings/profile store unavailable");
                return;
            }

            string videoPath = ResolveCurrentVideoPathForProfile();
            string videoKey = NormalizeVideoProfileKey(videoPath);
            if (string.IsNullOrWhiteSpace(videoKey))
            {
                LogWarn("[ui] SaveRoom(video) ignored: current video path is empty");
                return;
            }

            RoomLayoutProfile profile = CaptureCurrentRoomLayoutProfile(includeAudioGain: false, markAsRoomLayout: true);
            if (_roomLayoutProfiles.TryGetVideo(videoKey, out RoomLayoutProfile existing))
                CopySectionValues(existing, profile);

            _roomLayoutProfiles.SetVideo(videoKey, profile);
            PersistRoomLayoutProfiles();
            ApplyRoomLayoutProfile(profile, persistSettings: true, applyToRoomTransform: true);

            LogInfo(
                $"[ui] room saved scope=video key={videoKey} " +
                $"scale={profile.Scale:F3} " +
                $"offset=({profile.OffsetX:F3},{profile.OffsetY:F3},{profile.OffsetZ:F3}) " +
                $"rot=({profile.RotationX:F1},{profile.RotationY:F1},{profile.RotationZ:F1})");
        }

        private void SaveFolderAudioGainProfileFromBar()
        {
            if (_settings == null || _roomLayoutProfiles == null)
            {
                LogWarn("[ui] SaveGain(folder) ignored: settings/profile store unavailable");
                return;
            }

            string folderKey = NormalizeFolderPathForList(_settings.FolderPlayPath);
            if (string.IsNullOrWhiteSpace(folderKey))
            {
                LogWarn("[ui] SaveGain(folder) ignored: folder path is empty");
                return;
            }

            RoomLayoutProfile profile = ResolveFolderProfileBase(folderKey);
            profile.HasAudioGain = true;
            profile.AudioGain = ResolveCurrentVideoAudioGain();
            profile.Normalize();

            _roomLayoutProfiles.SetFolder(folderKey, profile);
            PersistRoomLayoutProfiles();
            ApplyAudioGainFromProfile(profile, persistSettings: true);
            LogInfo($"[ui] gain saved scope=folder key={folderKey} gain={profile.AudioGain:F3}");
        }

        private void SaveVideoAudioGainProfileFromBar()
        {
            if (_settings == null || _roomLayoutProfiles == null)
            {
                LogWarn("[ui] SaveGain(video) ignored: settings/profile store unavailable");
                return;
            }

            string videoPath = ResolveCurrentVideoPathForProfile();
            string videoKey = NormalizeVideoProfileKey(videoPath);
            if (string.IsNullOrWhiteSpace(videoKey))
            {
                LogWarn("[ui] SaveGain(video) ignored: current video path is empty");
                return;
            }

            RoomLayoutProfile profile = ResolveVideoProfileBase(videoKey);
            profile.HasAudioGain = true;
            profile.AudioGain = ResolveCurrentVideoAudioGain();
            profile.Normalize();

            _roomLayoutProfiles.SetVideo(videoKey, profile);
            PersistRoomLayoutProfiles();
            ApplyAudioGainFromProfile(profile, persistSettings: true);
            LogInfo($"[ui] gain saved scope=video key={videoKey} gain={profile.AudioGain:F3}");
        }

        private void SaveFolderSpeedLimitBreakProfileFromBar()
        {
            if (_settings == null || _roomLayoutProfiles == null)
            {
                LogWarn("[ui] SaveLB(folder) ignored: settings/profile store unavailable");
                return;
            }

            if (!TryGetSpeedLimitBreakSnapshot(out SpeedLimitBreakSnapshot snapshot))
            {
                LogWarn("[ui] SaveLB(folder) ignored: SpeedLimitBreak unavailable");
                return;
            }

            string folderKey = NormalizeFolderPathForList(_settings.FolderPlayPath);
            if (string.IsNullOrWhiteSpace(folderKey))
            {
                LogWarn("[ui] SaveLB(folder) ignored: folder path is empty");
                return;
            }

            RoomLayoutProfile profile = ResolveFolderProfileBase(folderKey);
            profile.HasSpeedLimitBreak = true;
            profile.SpeedForceVanilla = snapshot.ForceVanillaSpeed;
            profile.SpeedEnableVideoTimeSpeedCues = snapshot.EnableVideoTimeSpeedCues;
            profile.SpeedAppliedBpmMax = snapshot.AppliedBpmMax;
            profile.Normalize();

            _roomLayoutProfiles.SetFolder(folderKey, profile);
            PersistRoomLayoutProfiles();
            LogInfo(
                $"[ui] speed-limit-break saved scope=folder key={folderKey} " +
                $"fv={profile.SpeedForceVanilla} tl={profile.SpeedEnableVideoTimeSpeedCues} bpmMax={profile.SpeedAppliedBpmMax:F1}");
        }

        private void SaveVideoSpeedLimitBreakProfileFromBar()
        {
            if (_settings == null || _roomLayoutProfiles == null)
            {
                LogWarn("[ui] SaveLB(video) ignored: settings/profile store unavailable");
                return;
            }

            if (!TryGetSpeedLimitBreakSnapshot(out SpeedLimitBreakSnapshot snapshot))
            {
                LogWarn("[ui] SaveLB(video) ignored: SpeedLimitBreak unavailable");
                return;
            }

            string videoPath = ResolveCurrentVideoPathForProfile();
            string videoKey = NormalizeVideoProfileKey(videoPath);
            if (string.IsNullOrWhiteSpace(videoKey))
            {
                LogWarn("[ui] SaveLB(video) ignored: current video path is empty");
                return;
            }

            RoomLayoutProfile profile = ResolveVideoProfileBase(videoKey);
            profile.HasSpeedLimitBreak = true;
            profile.SpeedForceVanilla = snapshot.ForceVanillaSpeed;
            profile.SpeedEnableVideoTimeSpeedCues = snapshot.EnableVideoTimeSpeedCues;
            profile.SpeedAppliedBpmMax = snapshot.AppliedBpmMax;
            profile.Normalize();

            _roomLayoutProfiles.SetVideo(videoKey, profile);
            PersistRoomLayoutProfiles();
            LogInfo(
                $"[ui] speed-limit-break saved scope=video key={videoKey} " +
                $"fv={profile.SpeedForceVanilla} tl={profile.SpeedEnableVideoTimeSpeedCues} bpmMax={profile.SpeedAppliedBpmMax:F1}");
        }

        private void SaveFolderBeatSyncProfileFromBar()
        {
            if (_settings == null || _roomLayoutProfiles == null)
            {
                LogWarn("[ui] SaveBeat(folder) ignored: settings/profile store unavailable");
                return;
            }

            if (!TryGetBeatSyncSnapshot(out BeatSyncSnapshot snapshot))
            {
                LogWarn("[ui] SaveBeat(folder) ignored: BeatSync unavailable");
                return;
            }

            string folderKey = NormalizeFolderPathForList(_settings.FolderPlayPath);
            if (string.IsNullOrWhiteSpace(folderKey))
            {
                LogWarn("[ui] SaveBeat(folder) ignored: folder path is empty");
                return;
            }

            RoomLayoutProfile profile = ResolveFolderProfileBase(folderKey);
            profile.HasBeatSync = true;
            profile.BeatEnabled = snapshot.Enabled;
            profile.BeatBpm = snapshot.Bpm;
            profile.BeatAutoMotionSwitch = snapshot.AutoMotionSwitch;
            profile.BeatAutoThreshold = snapshot.AutoThreshold;
            profile.BeatLowThreshold = snapshot.LowThreshold;
            profile.BeatHighThreshold = snapshot.HighThreshold;
            profile.BeatLowIntensity = snapshot.LowIntensity;
            profile.BeatMidIntensity = snapshot.MidIntensity;
            profile.BeatHighIntensity = snapshot.HighIntensity;
            profile.BeatSmoothTime = snapshot.SmoothTime;
            profile.BeatStrongMotionBeats = snapshot.StrongMotionBeats;
            profile.BeatWeakMotionBeats = snapshot.WeakMotionBeats;
            profile.BeatLowPassHz = snapshot.LowPassHz;
            profile.BeatVerboseLog = snapshot.VerboseLog;
            profile.Normalize();

            _roomLayoutProfiles.SetFolder(folderKey, profile);
            PersistRoomLayoutProfiles();
            LogInfo(
                $"[ui] beat-sync saved scope=folder key={folderKey} " +
                $"enabled={profile.BeatEnabled} bpm={profile.BeatBpm} autoMotion={profile.BeatAutoMotionSwitch} " +
                $"autoTh={profile.BeatAutoThreshold}");
        }

        private void SaveVideoBeatSyncProfileFromBar()
        {
            if (_settings == null || _roomLayoutProfiles == null)
            {
                LogWarn("[ui] SaveBeat(video) ignored: settings/profile store unavailable");
                return;
            }

            if (!TryGetBeatSyncSnapshot(out BeatSyncSnapshot snapshot))
            {
                LogWarn("[ui] SaveBeat(video) ignored: BeatSync unavailable");
                return;
            }

            string videoPath = ResolveCurrentVideoPathForProfile();
            string videoKey = NormalizeVideoProfileKey(videoPath);
            if (string.IsNullOrWhiteSpace(videoKey))
            {
                LogWarn("[ui] SaveBeat(video) ignored: current video path is empty");
                return;
            }

            RoomLayoutProfile profile = ResolveVideoProfileBase(videoKey);
            profile.HasBeatSync = true;
            profile.BeatEnabled = snapshot.Enabled;
            profile.BeatBpm = snapshot.Bpm;
            profile.BeatAutoMotionSwitch = snapshot.AutoMotionSwitch;
            profile.BeatAutoThreshold = snapshot.AutoThreshold;
            profile.BeatLowThreshold = snapshot.LowThreshold;
            profile.BeatHighThreshold = snapshot.HighThreshold;
            profile.BeatLowIntensity = snapshot.LowIntensity;
            profile.BeatMidIntensity = snapshot.MidIntensity;
            profile.BeatHighIntensity = snapshot.HighIntensity;
            profile.BeatSmoothTime = snapshot.SmoothTime;
            profile.BeatStrongMotionBeats = snapshot.StrongMotionBeats;
            profile.BeatWeakMotionBeats = snapshot.WeakMotionBeats;
            profile.BeatLowPassHz = snapshot.LowPassHz;
            profile.BeatVerboseLog = snapshot.VerboseLog;
            profile.Normalize();

            _roomLayoutProfiles.SetVideo(videoKey, profile);
            PersistRoomLayoutProfiles();
            LogInfo(
                $"[ui] beat-sync saved scope=video key={videoKey} " +
                $"enabled={profile.BeatEnabled} bpm={profile.BeatBpm} autoMotion={profile.BeatAutoMotionSwitch} " +
                $"autoTh={profile.BeatAutoThreshold}");
        }

        private bool TryApplySavedRoomLayoutForCurrentSelection(
            string trigger,
            string videoPathOverride = null)
        {
            if (_settings == null || _roomLayoutProfiles == null)
                return false;

            CaptureProfileResetBaselinesIfNeeded();
            TryCaptureExternalProfileResetBaselines();

            string videoKey = NormalizeVideoProfileKey(ResolveCurrentVideoPathForProfile(videoPathOverride));
            string folderKey = NormalizeFolderPathForList(_settings.FolderPlayPath);

            RoomLayoutProfile videoProfile = null;
            RoomLayoutProfile folderProfile = null;
            bool hasVideo = !string.IsNullOrWhiteSpace(videoKey) &&
                            _roomLayoutProfiles.TryGetVideo(videoKey, out videoProfile);
            bool hasFolder = !string.IsNullOrWhiteSpace(folderKey) &&
                             _roomLayoutProfiles.TryGetFolder(folderKey, out folderProfile);

            bool applied = false;

            if (hasVideo && videoProfile.HasRoomLayout)
            {
                ApplyRoomLayoutProfile(videoProfile, persistSettings: true, applyToRoomTransform: true);
                LogInfo($"room layout applied scope=video trigger={trigger} key={videoKey}");
                applied = true;
            }
            else if (hasFolder && folderProfile.HasRoomLayout)
            {
                ApplyRoomLayoutProfile(folderProfile, persistSettings: true, applyToRoomTransform: true);
                LogInfo($"room layout applied scope=folder trigger={trigger} key={folderKey}");
                applied = true;
            }
            else
            {
                RoomLayoutProfile resetProfile = CreateRoomLayoutResetProfile();
                ApplyRoomLayoutProfile(resetProfile, persistSettings: true, applyToRoomTransform: true);
                LogInfo($"room layout reset scope=baseline trigger={trigger}");
                applied = true;
            }

            if (hasVideo && videoProfile.HasAudioGain)
            {
                ApplyAudioGainFromProfile(videoProfile, persistSettings: true);
                LogInfo($"audio gain applied scope=video trigger={trigger} key={videoKey} gain={videoProfile.AudioGain:F3}");
                applied = true;
            }
            else if (hasFolder && folderProfile.HasAudioGain)
            {
                ApplyAudioGainFromProfile(folderProfile, persistSettings: true);
                LogInfo($"audio gain applied scope=folder trigger={trigger} key={folderKey} gain={folderProfile.AudioGain:F3}");
                applied = true;
            }
            else
            {
                RoomLayoutProfile resetGain = CreateAudioGainResetProfile();
                ApplyAudioGainFromProfile(resetGain, persistSettings: true);
                LogInfo($"audio gain reset scope=baseline trigger={trigger} gain={resetGain.AudioGain:F3}");
                applied = true;
            }

            if (hasVideo && videoProfile.HasSpeedLimitBreak)
            {
                var snap = new SpeedLimitBreakSnapshot
                {
                    ForceVanillaSpeed = videoProfile.SpeedForceVanilla,
                    EnableVideoTimeSpeedCues = videoProfile.SpeedEnableVideoTimeSpeedCues,
                    AppliedBpmMax = videoProfile.SpeedAppliedBpmMax
                };
                if (TryApplySpeedLimitBreakSnapshot(snap, $"profile-video:{trigger}"))
                {
                    LogInfo($"speed-limit-break applied scope=video trigger={trigger} key={videoKey}");
                    applied = true;
                }
            }
            else if (hasFolder && folderProfile.HasSpeedLimitBreak)
            {
                var snap = new SpeedLimitBreakSnapshot
                {
                    ForceVanillaSpeed = folderProfile.SpeedForceVanilla,
                    EnableVideoTimeSpeedCues = folderProfile.SpeedEnableVideoTimeSpeedCues,
                    AppliedBpmMax = folderProfile.SpeedAppliedBpmMax
                };
                if (TryApplySpeedLimitBreakSnapshot(snap, $"profile-folder:{trigger}"))
                {
                    LogInfo($"speed-limit-break applied scope=folder trigger={trigger} key={folderKey}");
                    applied = true;
                }
            }
            else
            {
                SpeedLimitBreakSnapshot resetSnap = CreateSpeedLimitBreakResetSnapshot();
                if (TryApplySpeedLimitBreakSnapshot(resetSnap, $"profile-reset:{trigger}"))
                {
                    LogInfo($"speed-limit-break reset scope=baseline trigger={trigger}");
                    applied = true;
                }
            }

            if (hasVideo && videoProfile.HasBeatSync)
            {
                var snap = new BeatSyncSnapshot
                {
                    Enabled = videoProfile.BeatEnabled,
                    Bpm = videoProfile.BeatBpm,
                    AutoMotionSwitch = videoProfile.BeatAutoMotionSwitch,
                    AutoThreshold = videoProfile.BeatAutoThreshold,
                    LowThreshold = videoProfile.BeatLowThreshold,
                    HighThreshold = videoProfile.BeatHighThreshold,
                    LowIntensity = videoProfile.BeatLowIntensity,
                    MidIntensity = videoProfile.BeatMidIntensity,
                    HighIntensity = videoProfile.BeatHighIntensity,
                    SmoothTime = videoProfile.BeatSmoothTime,
                    StrongMotionBeats = videoProfile.BeatStrongMotionBeats,
                    WeakMotionBeats = videoProfile.BeatWeakMotionBeats,
                    LowPassHz = videoProfile.BeatLowPassHz,
                    VerboseLog = videoProfile.BeatVerboseLog
                };
                if (TryApplyBeatSyncSnapshot(snap, $"profile-video:{trigger}"))
                {
                    LogInfo($"beat-sync applied scope=video trigger={trigger} key={videoKey}");
                    applied = true;
                }
            }
            else if (hasFolder && folderProfile.HasBeatSync)
            {
                var snap = new BeatSyncSnapshot
                {
                    Enabled = folderProfile.BeatEnabled,
                    Bpm = folderProfile.BeatBpm,
                    AutoMotionSwitch = folderProfile.BeatAutoMotionSwitch,
                    AutoThreshold = folderProfile.BeatAutoThreshold,
                    LowThreshold = folderProfile.BeatLowThreshold,
                    HighThreshold = folderProfile.BeatHighThreshold,
                    LowIntensity = folderProfile.BeatLowIntensity,
                    MidIntensity = folderProfile.BeatMidIntensity,
                    HighIntensity = folderProfile.BeatHighIntensity,
                    SmoothTime = folderProfile.BeatSmoothTime,
                    StrongMotionBeats = folderProfile.BeatStrongMotionBeats,
                    WeakMotionBeats = folderProfile.BeatWeakMotionBeats,
                    LowPassHz = folderProfile.BeatLowPassHz,
                    VerboseLog = folderProfile.BeatVerboseLog
                };
                if (TryApplyBeatSyncSnapshot(snap, $"profile-folder:{trigger}"))
                {
                    LogInfo($"beat-sync applied scope=folder trigger={trigger} key={folderKey}");
                    applied = true;
                }
            }
            else
            {
                BeatSyncSnapshot resetSnap = CreateBeatSyncResetSnapshot();
                if (TryApplyBeatSyncSnapshot(resetSnap, $"profile-reset:{trigger}"))
                {
                    LogInfo($"beat-sync reset scope=baseline trigger={trigger}");
                    applied = true;
                }
            }

            return applied;
        }

        private void CaptureProfileResetBaselinesIfNeeded()
        {
            if (_profileResetBaselinesCaptured)
                return;

            _profileResetBaselinesCaptured = true;

            float scale = _playbackRoomScale;
            if (_videoRoomRoot != null)
                scale = _videoRoomRoot.transform.localScale.x;
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
                scale = 1f;
            _profileResetRoomScale = Mathf.Clamp(scale, 0.25f, 4f);

            if (_settings != null)
            {
                _profileResetOffsetX = _settings.VideoRoomOffsetX;
                _profileResetOffsetY = _settings.VideoRoomOffsetY;
                _profileResetOffsetZ = _settings.VideoRoomOffsetZ;
                _profileResetRotationX = _settings.VideoRoomRotationX;
                _profileResetRotationY = _settings.VideoRoomRotationY;
                _profileResetRotationZ = _settings.VideoRoomRotationZ;
                _profileResetAudioGain = _settings.VideoAudioGain;
            }

            if (float.IsNaN(_profileResetAudioGain) || float.IsInfinity(_profileResetAudioGain) || _profileResetAudioGain <= 0f)
                _profileResetAudioGain = 1f;
            _profileResetAudioGain = Mathf.Clamp(_profileResetAudioGain, 0.1f, 6f);

            TryCaptureExternalProfileResetBaselines();
        }

        private void TryCaptureExternalProfileResetBaselines()
        {
            if (!_profileResetSpeedCaptured &&
                TryGetSpeedLimitBreakSnapshot(out SpeedLimitBreakSnapshot speedSnap) &&
                speedSnap != null)
            {
                _profileResetSpeedSnapshot = CloneSpeedLimitBreakSnapshot(speedSnap);
                _profileResetSpeedCaptured = true;
            }

            if (!_profileResetBeatCaptured &&
                TryGetBeatSyncSnapshot(out BeatSyncSnapshot beatSnap) &&
                beatSnap != null)
            {
                _profileResetBeatSnapshot = CloneBeatSyncSnapshot(beatSnap);
                _profileResetBeatCaptured = true;
            }
        }

        private RoomLayoutProfile CreateRoomLayoutResetProfile()
        {
            var profile = new RoomLayoutProfile
            {
                Scale = _profileResetRoomScale,
                OffsetX = _profileResetOffsetX,
                OffsetY = _profileResetOffsetY,
                OffsetZ = _profileResetOffsetZ,
                RotationX = _profileResetRotationX,
                RotationY = _profileResetRotationY,
                RotationZ = _profileResetRotationZ,
                HasRoomLayout = true
            };
            profile.Normalize();
            return profile;
        }

        private RoomLayoutProfile CreateAudioGainResetProfile()
        {
            var profile = new RoomLayoutProfile
            {
                HasAudioGain = true,
                AudioGain = _profileResetAudioGain
            };
            profile.Normalize();
            return profile;
        }

        private SpeedLimitBreakSnapshot CreateSpeedLimitBreakResetSnapshot()
        {
            if (_profileResetSpeedCaptured && _profileResetSpeedSnapshot != null)
                return CloneSpeedLimitBreakSnapshot(_profileResetSpeedSnapshot);

            return new SpeedLimitBreakSnapshot
            {
                ForceVanillaSpeed = false,
                EnableVideoTimeSpeedCues = false,
                AppliedBpmMax = 120f
            };
        }

        private BeatSyncSnapshot CreateBeatSyncResetSnapshot()
        {
            if (_profileResetBeatCaptured && _profileResetBeatSnapshot != null)
                return CloneBeatSyncSnapshot(_profileResetBeatSnapshot);

            return new BeatSyncSnapshot
            {
                Enabled = true,
                Bpm = 128,
                AutoMotionSwitch = true,
                AutoThreshold = true,
                LowThreshold = 0.3f,
                HighThreshold = 0.7f,
                LowIntensity = 0.25f,
                MidIntensity = 0.5f,
                HighIntensity = 1f,
                SmoothTime = 0.5f,
                StrongMotionBeats = 4f,
                WeakMotionBeats = 4f,
                LowPassHz = 150f,
                VerboseLog = false
            };
        }

        private static SpeedLimitBreakSnapshot CloneSpeedLimitBreakSnapshot(SpeedLimitBreakSnapshot source)
        {
            if (source == null)
                return null;

            return new SpeedLimitBreakSnapshot
            {
                ForceVanillaSpeed = source.ForceVanillaSpeed,
                EnableVideoTimeSpeedCues = source.EnableVideoTimeSpeedCues,
                AppliedBpmMax = source.AppliedBpmMax
            };
        }

        private static BeatSyncSnapshot CloneBeatSyncSnapshot(BeatSyncSnapshot source)
        {
            if (source == null)
                return null;

            return new BeatSyncSnapshot
            {
                Enabled = source.Enabled,
                Bpm = source.Bpm,
                AutoMotionSwitch = source.AutoMotionSwitch,
                AutoThreshold = source.AutoThreshold,
                LowThreshold = source.LowThreshold,
                HighThreshold = source.HighThreshold,
                LowIntensity = source.LowIntensity,
                MidIntensity = source.MidIntensity,
                HighIntensity = source.HighIntensity,
                SmoothTime = source.SmoothTime,
                StrongMotionBeats = source.StrongMotionBeats,
                WeakMotionBeats = source.WeakMotionBeats,
                LowPassHz = source.LowPassHz,
                VerboseLog = source.VerboseLog
            };
        }

        private RoomLayoutProfile CaptureCurrentRoomLayoutProfile(bool includeAudioGain, bool markAsRoomLayout)
        {
            var profile = new RoomLayoutProfile
            {
                Scale = Mathf.Clamp(_playbackRoomScale, 0.25f, 4f),
                OffsetX = _settings?.VideoRoomOffsetX ?? 0f,
                OffsetY = _settings?.VideoRoomOffsetY ?? -1f,
                OffsetZ = _settings?.VideoRoomOffsetZ ?? 0f,
                RotationX = _settings?.VideoRoomRotationX ?? 0f,
                RotationY = _settings?.VideoRoomRotationY ?? 0f,
                RotationZ = _settings?.VideoRoomRotationZ ?? 0f,
                HasAudioGain = includeAudioGain,
                AudioGain = includeAudioGain ? ResolveCurrentVideoAudioGain() : 1f,
                HasRoomLayout = markAsRoomLayout
            };

            if (_videoRoomRoot != null)
            {
                profile.Scale = Mathf.Clamp(_videoRoomRoot.transform.localScale.x, 0.25f, 4f);

                EnsureFemaleCharaRef();
                if (_femaleChara != null)
                {
                    Vector3 fp = _femaleChara.transform.position;
                    float baseY = _hasFemaleBaseY ? _femaleBaseY : fp.y;
                    Vector3 roomPos = _videoRoomRoot.transform.position;
                    profile.OffsetX = roomPos.x - fp.x;
                    profile.OffsetY = roomPos.y - baseY;
                    profile.OffsetZ = roomPos.z - fp.z;
                }

                Vector3 euler = _videoRoomRoot.transform.eulerAngles;
                profile.RotationX = euler.x;
                profile.RotationY = euler.y;
                profile.RotationZ = euler.z;
            }

            profile.Normalize();
            return profile;
        }

        private void ApplyRoomLayoutProfile(
            RoomLayoutProfile profile,
            bool persistSettings,
            bool applyToRoomTransform)
        {
            if (_settings == null || profile == null)
                return;

            RoomLayoutProfile normalized = profile.Clone();
            normalized.Normalize();

            bool changed =
                IsRoomLayoutValueChanged(_playbackRoomScale, normalized.Scale) ||
                IsRoomLayoutValueChanged(_settings.VideoRoomOffsetX, normalized.OffsetX) ||
                IsRoomLayoutValueChanged(_settings.VideoRoomOffsetY, normalized.OffsetY) ||
                IsRoomLayoutValueChanged(_settings.VideoRoomOffsetZ, normalized.OffsetZ) ||
                IsRoomLayoutValueChanged(_settings.VideoRoomRotationX, normalized.RotationX) ||
                IsRoomLayoutValueChanged(_settings.VideoRoomRotationY, normalized.RotationY) ||
                IsRoomLayoutValueChanged(_settings.VideoRoomRotationZ, normalized.RotationZ);

            _playbackRoomScale = normalized.Scale;
            _settings.VideoRoomOffsetX = normalized.OffsetX;
            _settings.VideoRoomOffsetY = normalized.OffsetY;
            _settings.VideoRoomOffsetZ = normalized.OffsetZ;
            _settings.VideoRoomRotationX = normalized.RotationX;
            _settings.VideoRoomRotationY = normalized.RotationY;
            _settings.VideoRoomRotationZ = normalized.RotationZ;

            _roomScaleInput = FormatRoomNumeric(_playbackRoomScale);
            _roomPosInputs[0] = FormatRoomNumeric(_settings.VideoRoomOffsetX);
            _roomPosInputs[1] = FormatRoomNumeric(_settings.VideoRoomOffsetY);
            _roomPosInputs[2] = FormatRoomNumeric(_settings.VideoRoomOffsetZ);
            _roomRotInputs[0] = FormatRoomNumeric(_settings.VideoRoomRotationX);
            _roomRotInputs[1] = FormatRoomNumeric(_settings.VideoRoomRotationY);
            _roomRotInputs[2] = FormatRoomNumeric(_settings.VideoRoomRotationZ);
            _videoGainInput = FormatRoomNumeric(_settings.VideoAudioGain);

            if (applyToRoomTransform)
                ApplyRoomLayoutToCurrentRoomTransform();

            if (persistSettings && changed)
            {
                SyncConfigEntriesFromSettings();
                SettingsStore.Save(Path.Combine(_pluginDir, "MapAddSettings.json"), _settings);
            }
        }

        private void ApplyAudioGainFromProfile(RoomLayoutProfile profile, bool persistSettings)
        {
            if (_settings == null || profile == null || !profile.HasAudioGain)
                return;

            RoomLayoutProfile normalized = profile.Clone();
            normalized.Normalize();
            float gain = normalized.AudioGain;
            bool changed = IsRoomLayoutValueChanged(_settings.VideoAudioGain, gain);
            _settings.VideoAudioGain = gain;
            _videoGainInput = FormatRoomNumeric(gain);
            ApplyRuntimeVideoAudioLevel(_settings.VideoVolume);

            if (persistSettings && changed)
            {
                SyncConfigEntriesFromSettings();
                SettingsStore.Save(Path.Combine(_pluginDir, "MapAddSettings.json"), _settings);
            }
        }

        private void ApplyRoomLayoutToCurrentRoomTransform()
        {
            if (_videoRoomRoot == null)
                return;

            _videoRoomRoot.transform.localScale = Vector3.one * Mathf.Clamp(_playbackRoomScale, 0.25f, 4f);
            _videoRoomRoot.transform.rotation = Quaternion.Euler(
                _settings.VideoRoomRotationX,
                _settings.VideoRoomRotationY,
                _settings.VideoRoomRotationZ);

            EnsureFemaleCharaRef();
            if (_femaleChara != null)
            {
                Vector3 fp = _femaleChara.transform.position;
                float baseY = _hasFemaleBaseY ? _femaleBaseY : fp.y;
                _videoRoomRoot.transform.position = new Vector3(
                    fp.x + _settings.VideoRoomOffsetX,
                    baseY + _settings.VideoRoomOffsetY,
                    fp.z + _settings.VideoRoomOffsetZ);
            }

            ConfigureVideoRoomAudioSource(_videoRoomAudioSource);
            SyncVideoAudioSourceToFemale(true);
        }

        private string ResolveCurrentVideoPathForProfile(string videoPathOverride = null)
        {
            string preferred = NormalizeVideoPathInput(NormalizeString(videoPathOverride));
            if (!string.IsNullOrWhiteSpace(preferred))
                return preferred;

            if (FolderIndex >= 0 && FolderFiles != null && FolderIndex < FolderFiles.Length)
                return NormalizeVideoPathInput(FolderFiles[FolderIndex]);

            return NormalizeVideoPathInput(_settings?.VideoPath);
        }

        private string NormalizeVideoProfileKey(string videoPath)
        {
            string normalized = NormalizeVideoPathInput(NormalizeString(videoPath));
            if (string.IsNullOrWhiteSpace(normalized))
                return string.Empty;

            if (IsStreamUrl(normalized) || IsWebCamUrl(normalized))
                return normalized;

            try
            {
                string fullPath = Path.IsPathRooted(normalized)
                    ? Path.GetFullPath(normalized)
                    : Path.GetFullPath(Path.Combine(_pluginDir ?? string.Empty, normalized));
                return fullPath.Replace('/', '\\');
            }
            catch
            {
                return normalized.Replace('/', '\\');
            }
        }

        private void PersistRoomLayoutProfiles()
        {
            if (_roomLayoutProfiles == null)
                return;

            RoomLayoutProfileStore.Save(
                RoomLayoutProfileStore.GetPath(_pluginDir),
                _roomLayoutProfiles);
        }

        private RoomLayoutProfile ResolveFolderProfileBase(string folderKey)
        {
            if (_roomLayoutProfiles != null &&
                _roomLayoutProfiles.TryGetFolder(folderKey, out RoomLayoutProfile existing) &&
                existing != null)
                return existing;

            return CaptureCurrentRoomLayoutProfile(includeAudioGain: false, markAsRoomLayout: false);
        }

        private RoomLayoutProfile ResolveVideoProfileBase(string videoKey)
        {
            if (_roomLayoutProfiles != null &&
                _roomLayoutProfiles.TryGetVideo(videoKey, out RoomLayoutProfile existing) &&
                existing != null)
                return existing;

            return CaptureCurrentRoomLayoutProfile(includeAudioGain: false, markAsRoomLayout: false);
        }

        private static void CopySectionValues(RoomLayoutProfile source, RoomLayoutProfile destination)
        {
            if (source == null || destination == null)
                return;

            destination.HasAudioGain = source.HasAudioGain;
            destination.AudioGain = source.AudioGain;
            destination.HasSpeedLimitBreak = source.HasSpeedLimitBreak;
            destination.SpeedForceVanilla = source.SpeedForceVanilla;
            destination.SpeedEnableVideoTimeSpeedCues = source.SpeedEnableVideoTimeSpeedCues;
            destination.SpeedAppliedBpmMax = source.SpeedAppliedBpmMax;
            destination.HasBeatSync = source.HasBeatSync;
            destination.BeatEnabled = source.BeatEnabled;
            destination.BeatBpm = source.BeatBpm;
            destination.BeatAutoMotionSwitch = source.BeatAutoMotionSwitch;
            destination.BeatAutoThreshold = source.BeatAutoThreshold;
            destination.BeatLowThreshold = source.BeatLowThreshold;
            destination.BeatHighThreshold = source.BeatHighThreshold;
            destination.BeatLowIntensity = source.BeatLowIntensity;
            destination.BeatMidIntensity = source.BeatMidIntensity;
            destination.BeatHighIntensity = source.BeatHighIntensity;
            destination.BeatSmoothTime = source.BeatSmoothTime;
            destination.BeatStrongMotionBeats = source.BeatStrongMotionBeats;
            destination.BeatWeakMotionBeats = source.BeatWeakMotionBeats;
            destination.BeatLowPassHz = source.BeatLowPassHz;
            destination.BeatVerboseLog = source.BeatVerboseLog;
        }

        private static bool IsRoomLayoutValueChanged(float a, float b)
        {
            return Mathf.Abs(a - b) > 0.0001f;
        }
    }
}
