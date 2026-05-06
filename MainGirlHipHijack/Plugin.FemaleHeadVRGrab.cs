using UnityEngine;
using VRGIN.Controls;
using VRGIN.Core;
using Valve.VR;
using MainGameTransformGizmo;

namespace MainGirlHipHijack
{
    /// <summary>
    /// 右VRコントローラーで女の頭ボーンを回転させる。
    /// グリップ中は直接追従。離した瞬間にアニメーションとの差分を計算して以降はアディティブ加算。
    /// デスクトップではスライダーまたは回転ギズモで操作可能。
    /// </summary>
    public sealed partial class Plugin
    {
        private bool       _femaleHeadGrabbing;
        private Quaternion _femaleHeadGrabCtrlOffset;  // Inverse(ctrlRot) * boneRot at grab start
        private Quaternion _femaleHeadAdditiveOffset;  // _desiredRot * Inverse(animRot) at release
        private bool       _femaleHeadHasAdditive;
        private Quaternion _femaleHeadDesiredRot;      // 離した瞬間にコントローラーが指定した回転
        private bool       _femaleHeadReleased;        // LateUpdate で差分計算するフラグ
        private Transform  _femaleHeadBoneCached;
        private Transform  _femaleHeadCtrlTf;
        private bool       _femaleHeadInRange;

        // 頭角度ギズモ
        private GameObject _femaleHeadGizmoProxyGo;
        private Transform  _femaleHeadGizmoProxy;
        private TransformGizmo _femaleHeadGizmo;
        private bool       _femaleHeadGizmoDragging;
        private Quaternion _femaleHeadGizmoBaseRot; // ギズモ操作前のアニメーション回転
        private System.Action<GizmoMode> _femaleHeadGizmoModeHandler;

        // ── Update から呼ぶ（入力処理） ────────────────────────────────

        private void UpdateFemaleHeadVRInput()
        {
            if (!VR.Active || VR.Mode == null) return;

            if (_femaleHeadBoneCached == null && _runtime.BoneCache != null)
                _femaleHeadBoneCached = FindBoneInCache(_runtime.BoneCache, "cf_j_head");

            if (_femaleHeadBoneCached == null) return;

            var ctrl = VR.Mode.Right;
            if (ctrl == null) return;
            _femaleHeadCtrlTf = ((Component)ctrl).transform;

            float grabDist = _settings != null ? _settings.FemaleHeadGrabDistance : 0.15f;
            _femaleHeadInRange = Vector3.Distance(_femaleHeadCtrlTf.position, _femaleHeadBoneCached.position) <= grabDist;

            var input = ctrl.Input;

            if (_femaleHeadInRange && !_femaleHeadGrabbing && _vrRightGrabIdx < 0
                && input.GetPressDown(EVRButtonId.k_EButton_Grip))
            {
                _femaleHeadGrabbing       = true;
                _femaleHeadGrabCtrlOffset = Quaternion.Inverse(_femaleHeadCtrlTf.rotation) * _femaleHeadBoneCached.rotation;
                LogInfo("[FemaleHeadGrab] grab start");
            }

            if (_femaleHeadGrabbing && input.GetPressUp(EVRButtonId.k_EButton_Grip))
            {
                // 離した瞬間にコントローラーが指定していた回転を記録
                // LateUpdate で animRot と比較してアディティブを確定する
                _femaleHeadDesiredRot = _femaleHeadCtrlTf.rotation * _femaleHeadGrabCtrlOffset;
                _femaleHeadReleased   = true;
                _femaleHeadGrabbing   = false;
                LogInfo("[FemaleHeadGrab] grab end");
            }
        }

        // NeckLook 診断ログ用
        private string _prevNeckTargetName = null;
        private int    _prevNeckPtnNo      = -999;

        // NeckLook ブレンドウェイト: 1=オフセット全適用, 0=オフセット無効
        private float _femaleHeadOffsetWeight     = 1f;
        private float _femaleHeadFadeStartWeight  = 1f;
        private float _femaleHeadFadeTargetWeight = 1f;
        private float _femaleHeadFadeElapsed      = 0f;

        // ── OnAfterHSceneLateUpdate から呼ぶ（ボーン上書き） ───────────

        private void ApplyFemaleHeadAdditiveRot()
        {
            if (_femaleHeadBoneCached == null && _runtime.BoneCache != null)
                _femaleHeadBoneCached = FindBoneInCache(_runtime.BoneCache, "cf_j_head");
            if (_femaleHeadBoneCached == null) return;

            // NeckLook 状態を読んでウェイトを更新
            var femaleCha = _runtime.TargetFemaleCha;
            if (femaleCha != null && femaleCha.neckLookCtrl != null)
            {
                var t = femaleCha.neckLookCtrl.target;
                string tName = t != null ? t.name : "null";
                int ptn = femaleCha.neckLookCtrl.ptnNo;

                // 診断ログ（変化時のみ）
                if (tName != _prevNeckTargetName || ptn != _prevNeckPtnNo)
                {
                    LogInfo($"[NeckLook] target={tName} ptnNo={ptn}");
                    _prevNeckTargetName = tName;
                    _prevNeckPtnNo      = ptn;
                }

                // ウェイト判定
                // ptnNo=5 かつ target が cm_J_ 系（男ボーン）→ 通常状態（ウェイト1）
                // ptnNo=5 かつ それ以外（Camera/aim）→ トリガー発火（ウェイト0）
                // それ以外 → 通常（ウェイト1）
                bool triggerActive = ptn == 5 && (t == null || !tName.StartsWith("cm_J_"));
                float newTarget = triggerActive ? 0f : 1f;
                if (!Mathf.Approximately(newTarget, _femaleHeadFadeTargetWeight))
                {
                    _femaleHeadFadeStartWeight  = _femaleHeadOffsetWeight;
                    _femaleHeadFadeTargetWeight = newTarget;
                    _femaleHeadFadeElapsed      = 0f;
                }
                float fadeTime = _settings != null ? Mathf.Max(0.05f, _settings.FemaleHeadOffsetFadeTime) : 1f;
                _femaleHeadFadeElapsed += Time.unscaledDeltaTime;
                float rawT = Mathf.Clamp01(_femaleHeadFadeElapsed / fadeTime);
                int easing = _settings != null ? _settings.FemaleHeadOffsetEasing : 0;
                float easedT = ApplyEasing(rawT, easing);
                _femaleHeadOffsetWeight = Mathf.Lerp(_femaleHeadFadeStartWeight, _femaleHeadFadeTargetWeight, easedT);
            }

            if (_femaleHeadGrabbing && _femaleHeadCtrlTf != null)
            {
                // グリップ中: コントローラーに直接追従
                _femaleHeadBoneCached.rotation = _femaleHeadCtrlTf.rotation * _femaleHeadGrabCtrlOffset;
                return;
            }

            if (_femaleHeadReleased)
            {
                // 離した直後の LateUpdate: この時点の bone.rotation = アニメーション結果
                // アディティブ = 希望回転 * Inverse(アニメーション回転)
                _femaleHeadAdditiveOffset = _femaleHeadDesiredRot * Quaternion.Inverse(_femaleHeadBoneCached.rotation);
                _femaleHeadHasAdditive    = true;
                _femaleHeadReleased       = false;
            }

            if (_femaleHeadOffsetWeight < 0.001f) return;

            // VR掴みによるアディティブ
            if (_femaleHeadHasAdditive)
            {
                Quaternion blended = Quaternion.Slerp(Quaternion.identity, _femaleHeadAdditiveOffset, _femaleHeadOffsetWeight);
                _femaleHeadBoneCached.rotation = blended * _femaleHeadBoneCached.rotation;
                return;
            }

            // デスクトップ: スライダーによる角度指定
            if (_settings != null && _settings.FemaleHeadAngleEnabled)
            {
                var euler = new Vector3(_settings.FemaleHeadAngleX, _settings.FemaleHeadAngleY, _settings.FemaleHeadAngleZ);
                if (euler.sqrMagnitude > 0.001f)
                {
                    Quaternion blended = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(euler), _femaleHeadOffsetWeight);
                    _femaleHeadBoneCached.rotation = _femaleHeadBoneCached.rotation * blended;
                }
            }
        }

        // ── 体位変更・HScene終了時のリセット ──────────────────────────

        private void ResetFemaleHeadAdditiveRot()
        {
            _femaleHeadGrabbing     = false;
            _femaleHeadHasAdditive  = false;
            _femaleHeadReleased     = false;
            _femaleHeadBoneCached   = null;
            _femaleHeadCtrlTf       = null;
            _femaleHeadInRange      = false;
            _femaleHeadOffsetWeight     = 1f;
            _femaleHeadFadeStartWeight  = 1f;
            _femaleHeadFadeTargetWeight = 1f;
            _femaleHeadFadeElapsed      = 0f;
            DestroyFemaleHeadGizmo();
        }

        private void HandleFemaleHeadAngleContextChange(string source)
        {
            bool keep = _settings != null && _settings.FemaleHeadAngleKeepOnMotionOrPostureChange;
            bool settingsChanged = false;

            if (!keep && _settings != null)
            {
                if (!Mathf.Approximately(_settings.FemaleHeadAngleX, 0f))
                {
                    _settings.FemaleHeadAngleX = 0f;
                    settingsChanged = true;
                }
                if (!Mathf.Approximately(_settings.FemaleHeadAngleY, 0f))
                {
                    _settings.FemaleHeadAngleY = 0f;
                    settingsChanged = true;
                }
                if (!Mathf.Approximately(_settings.FemaleHeadAngleZ, 0f))
                {
                    _settings.FemaleHeadAngleZ = 0f;
                    settingsChanged = true;
                }
            }

            ResetFemaleHeadAdditiveRot();

            if (settingsChanged)
                SaveSettings();

            if (_settings != null && _settings.DetailLogEnabled)
            {
                LogInfo("[FemaleHeadAngle] context changed source=" + source
                    + " keep=" + (keep ? "ON" : "OFF")
                    + " resetAngles=" + (!keep ? "ON" : "OFF"));
            }
        }

        private void SetFemaleHeadAdditiveRotForPreset(bool enabled, Quaternion offset)
        {
            _femaleHeadGrabbing = false;
            _femaleHeadReleased = false;
            _femaleHeadDesiredRot = Quaternion.identity;
            _femaleHeadHasAdditive = enabled;
            _femaleHeadAdditiveOffset = enabled
                ? NormalizeSafeQuaternion(offset)
                : Quaternion.identity;
        }

        // ── 頭角度ギズモ ─────────────────────────────────────────────────

        private void UpdateFemaleHeadAngleGizmo()
        {
            if (_settings == null) return;

            bool shouldShow = _settings.FemaleHeadAngleEnabled
                && _settings.FemaleHeadAngleGizmoVisible
                && _femaleHeadBoneCached != null
                && !VR.Active;

            if (shouldShow)
            {
                EnsureFemaleHeadGizmo();
                if (_femaleHeadGizmo != null)
                    _femaleHeadGizmo.SetVisible(true);
                // ギズモプロキシをボーン位置に追従
                if (_femaleHeadGizmoProxy != null && _femaleHeadBoneCached != null)
                {
                    _femaleHeadGizmoProxy.position = _femaleHeadBoneCached.position;

                    if (!_femaleHeadGizmoDragging)
                    {
                        // ドラッグ中でなければプロキシの回転をスライダー値に合わせる
                        _femaleHeadGizmoProxy.rotation = _femaleHeadBoneCached.rotation;
                    }
                }

                // ギズモドラッグ中はプロキシの回転からスライダー値を逆算
                if (_femaleHeadGizmoDragging && _femaleHeadGizmoProxy != null && _femaleHeadBoneCached != null)
                {
                    // プロキシ回転 = baseRot * Euler(angle) → angle = Inverse(baseRot) * proxyRot
                    Quaternion delta = Quaternion.Inverse(_femaleHeadGizmoBaseRot) * _femaleHeadGizmoProxy.rotation;
                    Vector3 euler = delta.eulerAngles;
                    // -180〜180に正規化
                    if (euler.x > 180f) euler.x -= 360f;
                    if (euler.y > 180f) euler.y -= 360f;
                    if (euler.z > 180f) euler.z -= 360f;
                    _settings.FemaleHeadAngleX = Mathf.Clamp(euler.x, -120f, 120f);
                    _settings.FemaleHeadAngleY = Mathf.Clamp(euler.y, -120f, 120f);
                    _settings.FemaleHeadAngleZ = Mathf.Clamp(euler.z, -120f, 120f);
                }
            }
            else
            {
                DestroyFemaleHeadGizmo();
            }
        }

        private void EnsureFemaleHeadGizmo()
        {
            if (_femaleHeadGizmoProxyGo != null) return;

            _femaleHeadGizmoProxyGo = new GameObject("__FemaleHeadAngleGizmo");
            _femaleHeadGizmoProxyGo.hideFlags = HideFlags.HideAndDontSave;
            _femaleHeadGizmoProxy = _femaleHeadGizmoProxyGo.transform;

            _femaleHeadGizmo = TransformGizmoApi.Attach(_femaleHeadGizmoProxyGo);
            if (_femaleHeadGizmo != null)
            {
                _femaleHeadGizmo.SetMode(GizmoMode.Rotate);
                EnforceNoScaleMode(_femaleHeadGizmo);
                ApplyConfiguredGizmoSize(_femaleHeadGizmo);
                _femaleHeadGizmo.SetVisible(true);
                _femaleHeadGizmoModeHandler = CreateNoScaleModeHandler(_femaleHeadGizmo);
                _femaleHeadGizmo.ModeChanged += _femaleHeadGizmoModeHandler;
                _femaleHeadGizmo.DragStateChanged += OnFemaleHeadGizmoDrag;
            }

            LogInfo("[FemaleHeadGizmo] created");
        }

        private void OnFemaleHeadGizmoDrag(bool dragging)
        {
            _femaleHeadGizmoDragging = dragging;
            if (dragging && _femaleHeadBoneCached != null)
            {
                // ドラッグ開始時のアニメーション回転を記録
                _femaleHeadGizmoBaseRot = _femaleHeadBoneCached.rotation;
                // プロキシを現在のボーン回転にセット
                if (_femaleHeadGizmoProxy != null)
                    _femaleHeadGizmoProxy.rotation = _femaleHeadBoneCached.rotation;
            }
            if (!dragging)
            {
                SaveSettings();
            }
        }

        private void DestroyFemaleHeadGizmo()
        {
            if (_femaleHeadGizmo != null)
            {
                _femaleHeadGizmo.DragStateChanged -= OnFemaleHeadGizmoDrag;
                if (_femaleHeadGizmoModeHandler != null)
                    _femaleHeadGizmo.ModeChanged -= _femaleHeadGizmoModeHandler;
            }
            if (_femaleHeadGizmoProxyGo != null)
            {
                TransformGizmoApi.Detach(_femaleHeadGizmoProxyGo);
                Destroy(_femaleHeadGizmoProxyGo);
            }
            _femaleHeadGizmoProxyGo = null;
            _femaleHeadGizmoProxy = null;
            _femaleHeadGizmo = null;
            _femaleHeadGizmoModeHandler = null;
            _femaleHeadGizmoDragging = false;
        }

        // 0=Linear 1=EaseIn 2=EaseOut 3=EaseInOut
        private static float ApplyEasing(float t, int mode)
        {
            switch (mode)
            {
                case 1: return t * t;                    // EaseIn
                case 2: return t * (2f - t);             // EaseOut
                case 3: return t < 0.5f
                        ? 2f * t * t
                        : -1f + (4f - 2f * t) * t;      // EaseInOut
                default: return t;                       // Linear
            }
        }
    }
}
