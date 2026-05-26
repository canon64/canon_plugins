using System.Collections.Generic;
using UnityEngine;

namespace MainGirlHipHijack
{
    public sealed partial class Plugin
    {
        private void DrawComposerFollowRow(int idx)
        {
            if (!ObjectComposerBridge.IsAvailable) return;
            if (_settings == null) return;
            if (_settings.FollowComposerEnabled == null || _settings.FollowComposerObjectIds == null) return;
            if (idx < 0 || idx >= _settings.FollowComposerEnabled.Length) return;

            GUILayout.BeginHorizontal();
            GUILayout.Space(16f);

            bool wasOn = _settings.FollowComposerEnabled[idx];
            bool isOn = GUILayout.Toggle(wasOn, new GUIContent("Composer連動",
                "MainGameObjectComposer のオブジェクトに追従する（Composer 側 UI でオブジェクトを選択）"),
                GUILayout.Width(110f));
            if (isOn != wasOn)
            {
                _settings.FollowComposerEnabled[idx] = isOn;
                if (!isOn)
                {
                    // OFF にしたら ID もクリアして、近傍追従に戻れるようにする
                    _settings.FollowComposerObjectIds[idx] = null;
                }
                SaveSettings();
            }

            string id = _settings.FollowComposerObjectIds[idx];
            string label = string.IsNullOrEmpty(id) ? "(未設定)" : ObjectComposerBridge.GetObjectName(id) ?? id;
            GUILayout.Label("target: " + label);

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// MainGameObjectComposer の管理オブジェクト Transform を、有効化されている各エフェクタの
        /// FollowBone に詰める。既存の近傍追従と排他で、毎フレーム UpdateFollowBones 冒頭で呼ばれる。
        /// </summary>
        private void UpdateComposerFollowTargets()
        {
            if (_settings == null) return;
            if (_settings.FollowComposerEnabled == null || _settings.FollowComposerObjectIds == null) return;
            if (!ObjectComposerBridge.IsAvailable) return;

            int len = Mathf.Min(_settings.FollowComposerEnabled.Length, BIK_TOTAL);
            for (int i = 0; i < len; i++)
            {
                if (!_settings.FollowComposerEnabled[i]) continue;
                string id = _settings.FollowComposerObjectIds[i];
                if (string.IsNullOrEmpty(id)) continue;

                BIKEffectorState state = _bikEff[i];
                if (state == null || !state.Running || state.Proxy == null) continue;
                if (state.GizmoDragging) continue;

                if (ObjectComposerBridge.TryGetTransform(id, out Transform tf))
                {
                    state.FollowBone = tf;
                    // オフセットは追加せず、Composer の Transform にぴったり追従させる
                    state.FollowBonePositionOffset = Vector3.zero;
                    state.FollowBoneRotationOffset = Quaternion.identity;
                }
                // Composer 側が消えても FollowBone は最後の値を維持
                // （唐突に元位置に戻るより、最後の位置で固まった方が安全）
            }
        }
    }
}
