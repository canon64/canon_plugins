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
        }

        internal void OnKissActionBlocked(string point)
        {
        }
    }
}
