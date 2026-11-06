using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Json;
using BepInEx.Bootstrap;
using HarmonyLib;
using MainGameDollMode;
using UnityEngine;

namespace MainGameVrNeckGrab
{
    internal sealed class DollSnapshot
    {
        internal static bool InternalWrite => _writeDepth > 0;
        static int _writeDepth;
        readonly ChaControl _actor;
        readonly PluginSettings _settings;
        readonly HFlag _flags;
        readonly Action<string, List<string>> _report;
        readonly List<string> _applied = new List<string>();
        readonly List<KeyValuePair<string, Action>> _restore = new List<KeyValuePair<string, Action>>();
        bool _restored;
        internal DollSnapshot(ChaControl actor, HFlag flags, PluginSettings settings, Action<string, List<string>> report)
        { _actor = actor; _flags = flags; _settings = settings; _report = report; }
        internal bool TryMotion(out float value) => MotionValue.TryResolve(_flags.nowAnimStateName,
            _settings.DollModeMotionLockWEnabled, _settings.DollModeMotionLockWValue,
            _settings.DollModeMotionLockSEnabled, _settings.DollModeMotionLockSValue, out value);
        void Own<T>(string name, Func<T> read, Action<T> write, T value)
        {
            T previous = read();
            // Register before applying, so a partially failing setter still has an undo.
            _restore.Add(new KeyValuePair<string, Action>(name, () => { if (_actor != null) write(previous); }));
            write(value);
            _applied.Add(name);
        }
        internal void Apply()
        {
            ++_writeDepth;
            var errors = new List<string>();
            Action<string, Action> apply = (name, action) => { try { action(); } catch (Exception e) { errors.Add(name + ": " + e.Message); } };
            try
            {
                var a = _actor; var s = _settings;
                apply("目のハイライト", () => Own("目のハイライト", () => a.fileStatus.hideEyesHighlight, a.HideEyeHighlight, true));
                apply("瞬き", () => Own("瞬き", () => a.fileStatus.eyesBlink, a.ChangeEyesBlinkFlag, false));
                apply("眉", () => Own("眉", () => a.fileStatus.eyebrowPtn, v => a.ChangeEyebrowPtn(v, true), s.DollModeEyebrowPattern));
                apply("目", () => Own("目", () => a.fileStatus.eyesPtn, v => a.ChangeEyesPtn(v, true), s.DollModeEyePattern));
                apply("口", () => Own("口", () => a.fileStatus.mouthPtn, v => a.ChangeMouthPtn(v, true), s.DollModeMouthPattern));
                apply("頬", () => Own("頬", () => a.fileStatus.hohoAkaRate, a.ChangeHohoAkaRate, s.DollModeCheekRate));
                apply("開眼", () => Own("開眼", () => a.fileStatus.eyesOpenMax, a.ChangeEyesOpenMax, s.DollModeEyesOpen));
                apply("開口", () => Own("開口", () => a.fileStatus.mouthOpenMax, a.ChangeMouthOpenMax, s.DollModeMouthOpen));
                apply("涙", () => Own("涙", () => a.tearsLv, v => a.tearsLv = v, (byte)s.DollModeTearsLevel));
                apply("口固定", () => Own("口固定", a.GetMouthFixed, a.ChangeMouthFixed, true));
                if (a.mouthCtrl != null) apply("口固定値", () => Own("口固定値", () => a.mouthCtrl.FixedRate, v => a.mouthCtrl.FixedRate = v, s.DollModeMouthOpen));
                if (a.eyeLookCtrl != null) apply("視線", () => Own("視線", () => a.eyeLookCtrl.enabled, v => a.eyeLookCtrl.enabled = v, false));
                if (!s.AllowFaceSiruDuringDollMode)
                    apply("顔の汗", () => Own("顔の汗", () => a.fileStatus.siruLv[(int)ChaFileDefine.SiruParts.SiruKao], v => a.SetSiruFlags(ChaFileDefine.SiruParts.SiruKao, v), (byte)s.DollModeFaceSweatLevel));
                if (s.DollModeModEffectsEnabled) apply("追加液体効果", ApplySkinEffects);
                if (s.DollModeSpiralOverlayEnabled) apply("渦目", ApplySpiral);
                if (s.DollModeEyeOverlayEnabled) apply("目の画像", ApplyEyeOverlay);
                if (s.DollModeMotionLockWEnabled || s.DollModeMotionLockSEnabled)
                    apply("動きの値", () =>
                    {
                        // Only this actor's Animator parameter is changed. Shared HFlag motion and
                        // the partner's animation remain owned by their existing controllers.
                        _restore.Add(new KeyValuePair<string, Action>("動きの値", () => { if (_actor != null) _actor.setAnimatorParamFloat("motion", _flags.motion); }));
                        if (TryMotion(out float value)) _actor.setAnimatorParamFloat("motion", value);
                    });
            }
            finally { --_writeDepth; }
            if (errors.Count > 0)
            {
                errors.Insert(0, "適用済み: " + string.Join("、", _applied));
                _report(_applied.Count == 0 ? "全項目適用失敗" : "一部適用失敗", errors);
            }
            if (_applied.Count == 0) throw new InvalidOperationException("人形の外見を適用できませんでした。");
        }
        void ApplySkinEffects()
        {
            var type = AccessTools.TypeByName("KK_SkinEffects.SkinEffectsController");
            var component = type == null ? null : _actor.GetComponent(type);
            if (component == null) throw new InvalidOperationException("SkinEffects がありません。");
            string[] names = { "TearLevel", "DroolLevel", "SalivaLevel", "SweatLevel", "CumInNoseLevel", "BukkakeLevel" };
            int[] values = { _settings.DollModeModTears, _settings.DollModeModDrool, _settings.DollModeModSaliva, _settings.DollModeModSweat, _settings.DollModeModNose, _settings.DollModeModBukkake };
            var errors = new List<string>();
            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    var property = AccessTools.Property(type, names[i]) ?? throw new MissingMemberException(names[i]);
                    Own(names[i], () => property.GetValue(component, null), v => property.SetValue(component, v, null), (object)values[i]);
                }
                catch (Exception e) { errors.Add(names[i] + ": " + e.Message); }
            }
            if (errors.Count > 0) _report("追加液体効果の一部適用失敗", errors);
        }
        void ApplySpiral()
        {
            var type = AccessTools.TypeByName("MainGameSpiralEyes.PublicApi");
            var begin = type == null ? null : AccessTools.Method(type, "BeginPositionedOverlay");
            var end = type == null ? null : AccessTools.Method(type, "EndOverlay");
            if (begin == null || end == null) throw new InvalidOperationException("渦目APIがありません。");
            _restore.Add(new KeyValuePair<string, Action>("渦目", () => end.Invoke(null, new object[] { Plugin.Guid, _actor })));
            if (!Equals(begin.Invoke(null, new object[] { Plugin.Guid, _actor, _settings.DollModeSpiralOffsetX, _settings.DollModeSpiralOffsetY }), true))
                throw new InvalidOperationException("渦目APIが適用を拒否しました。");
        }
        void ApplyEyeOverlay()
        {
            var type = AccessTools.TypeByName("KoiSkinOverlayX.KoiSkinOverlayController");
            var texType = AccessTools.TypeByName("KoiSkinOverlayX.TexType");
            var component = type == null ? null : _actor.GetComponent(type);
            if (component == null || texType == null) throw new InvalidOperationException("KSOXがありません。");
            string path = _settings.DollModeEyeOverlayPngPath;
            if (!Path.IsPathRooted(path)) path = Path.Combine(Plugin.DollSettingsDirectory, path);
            byte[] png = File.ReadAllBytes(path);
            var set = AccessTools.Method(type, "SetOverlayTex", new[] { typeof(byte[]), texType });
            var storage = ReflectionAccess.Get(component, "OverlayStorage");
            var get = AccessTools.Method(storage.GetType(), "GetTexture", new[] { texType });
            var errors = new List<string>();
            foreach (string side in new[] { "EyeOverL", "EyeOverR" })
                try
                {
                    object key = Enum.Parse(texType, side);
                    var prior = CaptureTexture(get.Invoke(storage, new[] { key }) as Texture);
                    _restore.Add(new KeyValuePair<string, Action>(side, () => set.Invoke(component, new object[] { prior, key })));
                    set.Invoke(component, new object[] { png, key });
                }
                catch (Exception e) { errors.Add(side + ": " + e.Message); }
            if (errors.Count > 0) _report("目の画像の一部適用失敗", errors);
        }
        static byte[] CaptureTexture(Texture texture)
        {
            if (texture == null) return null;
            var previous = RenderTexture.active;
            var temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
            Texture2D copy = null;
            try
            {
                Graphics.Blit(texture, temporary); RenderTexture.active = temporary;
                copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); copy.Apply();
                return copy.EncodeToPNG();
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(temporary); if (copy != null) UnityEngine.Object.Destroy(copy); }
        }
        internal void Restore()
        {
            if (_restored) return; _restored = true;
            var errors = new List<string>(); ++_writeDepth;
            try
            {
                for (int i = _restore.Count - 1; i >= 0; --i)
                    try { _restore[i].Value(); } catch (Exception e) { errors.Add(_restore[i].Key + ": " + e.Message); }
            }
            finally { --_writeDepth; _restore.Clear(); }
            if (errors.Count > 0) _report("一部復元失敗", errors);
        }
    }
    public sealed partial class Plugin
    {
        internal static string DollSettingsDirectory => Chainloader.PluginInfos.TryGetValue("canon.maingame.dollmode", out var info)
            ? Path.GetDirectoryName(info.Location) : Path.Combine(BepInEx.Paths.PluginPath, "canon_plugins", "MainGameDollMode");
        Func<ChaControl, bool> _otherDoll;
        bool OtherDollActive(ChaControl actor)
        {
            if (_otherDoll == null)
            {
                var type = AccessTools.TypeByName("MainGameDollMode.Plugin");
                var method = type == null ? null : AccessTools.Method(type, "IsDollModeActive");
                _otherDoll = method == null ? (Func<ChaControl, bool>)(_ => false) : a => (bool)method.Invoke(null, new object[] { a });
            }
            return _otherDoll(actor);
        }
        void InstallOtherDollHook()
        {
            var type = AccessTools.TypeByName("MainGameDollMode.Plugin");
            var method = type == null ? null : AccessTools.Method(type, "ApplyDollModeState");
            if (method != null) Patch(method, nameof(OtherDollApplying));
        }
        static void OtherDollApplying(object __instance)
        {
            var p = Instance;
            if (p == null || !p.Allowed) return;
            object settings = ReflectionAccess.Get(__instance, "_settings");
            if (!Equals(ReflectionAccess.Get(settings, "Enabled"), true) || !Equals(ReflectionAccess.Get(settings, "DollModeEnabled"), true)) return;
            var target = AccessTools.Method(__instance.GetType(), "IsTargetCharacter");
            foreach (var s in p._actors)
            {
                if (!s.State.Doll || target == null || !Equals(target.Invoke(__instance, new object[] { s.Actor }), true)) continue;
                p.ReleaseHold(s); s.Doll?.Restore(); s.Doll = null; s.State.CancelDoll(); p.HideAwakening(s);
            }
        }
        PluginSettings LoadDollSettings()
        {
            // Read-only. Do NOT call the old LoadOrCreate/SetDollModeEnabled APIs, which save files.
            string path = Path.Combine(DollSettingsDirectory, "config.json");
            if (!File.Exists(path)) return new PluginSettings();
            using (var stream = File.OpenRead(path))
            {
                var settings = (PluginSettings)new DataContractJsonSerializer(typeof(PluginSettings)).ReadObject(stream);
                if (settings == null) throw new InvalidDataException("人形設定が空です: " + path);
                settings.Normalize(); return settings;
            }
        }
    }
}
