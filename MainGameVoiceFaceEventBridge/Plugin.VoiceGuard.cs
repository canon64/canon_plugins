using System;
using UnityEngine;

namespace MainGameVoiceFaceEventBridge
{
    // Plugin.VoiceGuard.cs
    //
    // 責務: 外部音声再生中にゲーム本体のボイス/キスイベントをブロックするか
    //       否かを判定する public API (VoiceGuardHooks から呼ばれる)。
    //       ブロック判定とブロック検知時のログ/状態書き込みだけを置く。
    internal sealed partial class Plugin
    {
        internal bool ShouldBlockGameVoiceEvents()
        {
            var s = Settings;
            if (s == null || !s.Enabled || !s.BlockGameVoiceWhileExternalPlaying)
            {
                return false;
            }

            if (_externalVoicePlayer != null && _externalVoicePlayer.IsPlaying)
            {
                return true;
            }

            return Time.unscaledTime < _blockGameVoiceUntil;
        }

        internal bool ShouldBlockKissActions()
        {
            var s = Settings;
            if (s == null || !s.Enabled)
            {
                return false;
            }

            if (_externalVoicePlayer != null && _externalVoicePlayer.IsPlaying)
            {
                return true;
            }

            return Time.unscaledTime < _blockGameVoiceUntil;
        }

        internal void OnGameVoiceEventBlocked(string point)
        {
            var s = Settings;
            if (s == null || !s.VerboseLog || string.IsNullOrWhiteSpace(point))
            {
                return;
            }

            float now = Time.unscaledTime;
            if (_blockLogCooldownByKey.TryGetValue(point, out var nextAllowed) && now < nextAllowed)
            {
                return;
            }

            _blockLogCooldownByKey[point] = now + 1f;
            Log("[block] " + point);
            DumpRuntimeState("game_voice_blocked:" + point);
        }

        internal void OnKissActionBlocked(string point)
        {
            if (string.IsNullOrWhiteSpace(point))
            {
                return;
            }

            float now = Time.unscaledTime;
            string key = "kiss:" + point;
            if (_blockLogCooldownByKey.TryGetValue(key, out var nextAllowed) && now < nextAllowed)
            {
                return;
            }

            _blockLogCooldownByKey[key] = now + 1f;
            bool extPlaying = _externalVoicePlayer != null && _externalVoicePlayer.IsPlaying;
            float remain = Mathf.Max(0f, _blockGameVoiceUntil - now);
            Log(
                "[kiss-block] "
                + point
                + " extPlaying=" + extPlaying
                + " blockRemain=" + remain.ToString("0.###"));
            DumpRuntimeState("kiss_blocked:" + point);
        }
    }
}
