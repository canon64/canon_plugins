using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using SessionGate = CanonPlugins.Local.SessionGate;

namespace MainGameDollMode
{
    public partial class Plugin
    {
        private static readonly string[] PresetEffectProperties = { "TearLevel", "DroolLevel", "SalivaLevel", "SweatLevel", "CumInNoseLevel", "BukkakeLevel" };
        private sealed class PresetExtraState
        {
            internal ChaControl Actor;
            internal object Controller;
            internal readonly int[] Original = new int[PresetEffectProperties.Length], Applied = new int[PresetEffectProperties.Length];
            internal readonly bool[] Owned = new bool[PresetEffectProperties.Length];
            internal bool Spiral;
            internal bool OwnNativeLiquid;
            internal byte NativeLiquidOriginal, NativeLiquidApplied;
        }
        private readonly Dictionary<ChaControl, PresetExtraState> _presetExtras = new Dictionary<ChaControl, PresetExtraState>();
        private Type _presetEffectsType;
        private readonly PropertyInfo[] _presetEffectProperties = new PropertyInfo[PresetEffectProperties.Length];
        private MethodInfo _presetSpiralBegin, _presetSpiralEnd;
        private string _presetExtraStatus = "";

        private void OnDisable() => ReleasePresetExtras();

        // Called only by session entry and explicit preset application, never a frame callback.
        private void SyncPresetExtras()
        {
            if (!SessionGate.Allowed) return;
            _presetExtraStatus = "";
            if (!_settings.Enabled || !_settings.DollModeEnabled) { ReleasePresetExtras(); return; }
            _presetEffectsType = Type.GetType("KK_SkinEffects.SkinEffectsController, KKS_SkinEffects", false);
            for (int i = 0; i < PresetEffectProperties.Length; i++) _presetEffectProperties[i] = _presetEffectsType?.GetProperty(PresetEffectProperties[i]);
            var spiral = Type.GetType("MainGameSpiralEyes.PublicApi, MainGameSpiralEyes", false);
            _presetSpiralBegin = spiral?.GetMethod("BeginPositionedOverlay", new[] { typeof(string), typeof(ChaControl), typeof(float), typeof(float) });
            _presetSpiralEnd = spiral?.GetMethod("EndOverlay", new[] { typeof(string), typeof(ChaControl) });
            foreach (var pair in new List<KeyValuePair<ChaControl, PresetExtraState>>(_presetExtras))
            {
                bool participant = Array.Exists(SessionGate.Actors, actor => ReferenceEquals(actor, pair.Key));
                if (participant && pair.Key != null && IsTargetCharacter(pair.Key) && !CanonPlugins.Shared.IndependentActorFilter.IsIndependent(pair.Key)) continue;
                ReleasePresetExtra(pair.Value); _presetExtras.Remove(pair.Key);
            }
            int[] desired = { _settings.DollModeModTears, _settings.DollModeModDrool, _settings.DollModeModSaliva, _settings.DollModeModSweat, _settings.DollModeModNose, _settings.DollModeModBukkake };
            foreach (var actor in SessionGate.Actors)
            {
                if (actor == null || !IsTargetCharacter(actor) || CanonPlugins.Shared.IndependentActorFilter.IsIndependent(actor)) continue;
                if (!_presetExtras.TryGetValue(actor, out var state))
                    _presetExtras.Add(actor, state = new PresetExtraState { Actor = actor });
                try
                {
                    // Allow external liquid writes between operations, but an explicit selection must show immediately.
                    if (_settings.AllowFaceSiruDuringDollMode || state.OwnNativeLiquid)
                    {
                        if (!state.OwnNativeLiquid)
                            state.NativeLiquidOriginal = _originalFaceExpressionByCha.TryGetValue(actor.GetInstanceID(), out var originalFace)
                                ? originalFace.FaceSiruLevel : ReadCurrentFaceSiruLevel(actor);
                        state.OwnNativeLiquid = true;
                        state.NativeLiquidApplied = (byte)_settings.DollModeFaceSweatLevel;
                        using (BeginInternalFaceWrite()) TrySetFaceSiruLevel(actor, state.NativeLiquidApplied, out _);
                    }
                    if (_settings.DollModeModEffectsEnabled)
                    {
                        object controller = _presetEffectsType == null ? null : actor.GetComponent(_presetEffectsType);
                        if (!ReferenceEquals(controller, state.Controller)) { RestorePresetModEffects(state); state.Controller = controller; }
                        if (controller == null) { _presetExtraStatus = "SkinEffectsのキャラ制御が見つからないため、MOD効果は未適用です。"; }
                        else for (int i = 0; i < PresetEffectProperties.Length; i++)
                        {
                            var property = _presetEffectProperties[i];
                            if (property == null || !property.CanRead || !property.CanWrite) continue;
                            if (!state.Owned[i]) state.Original[i] = (int)property.GetValue(controller, null);
                            // Own before invoking: partial setter failure must still be cleaned up.
                            state.Owned[i] = true; state.Applied[i] = desired[i];
                            property.SetValue(controller, desired[i], null);
                            state.Applied[i] = (int)property.GetValue(controller, null);
                        }
                    }
                    else RestorePresetModEffects(state);
                    if (_settings.DollModeSpiralOverlayEnabled)
                    {
                        state.Spiral = _presetSpiralBegin != null && (bool)_presetSpiralBegin.Invoke(null, new object[] { GUID, actor, _settings.DollModeSpiralOffsetX, _settings.DollModeSpiralOffsetY });
                        if (!state.Spiral) _presetExtraStatus += " 螺旋瞳は未適用です。MainGameSpiralEyesの有効設定とgate許可を確認してください。";
                    }
                    else if (!_settings.DollModeSpiralOverlayEnabled && state.Spiral)
                    {
                        _presetSpiralEnd?.Invoke(null, new object[] { GUID, actor }); state.Spiral = false;
                    }
                }
                catch (Exception ex) { LogWarn("[preset-effects] " + ex.GetBaseException().Message); ReleasePresetExtra(state); }
            }
        }

        private void RestorePresetModEffects(PresetExtraState state)
        {
            if (state.Controller == null || state.Controller is UnityEngine.Object obj && obj == null) return;
            for (int i = 0; i < PresetEffectProperties.Length; i++)
            {
                if (!state.Owned[i]) continue;
                try
                {
                    var property = _presetEffectProperties[i];
                    if (property != null && (int)property.GetValue(state.Controller, null) == state.Applied[i])
                        property.SetValue(state.Controller, state.Original[i], null);
                }
                catch (Exception ex) { LogWarn("[preset-effects] restore " + PresetEffectProperties[i] + ": " + ex.GetBaseException().Message); }
                finally { state.Owned[i] = false; }
            }
        }
        private void ReleasePresetExtra(PresetExtraState state)
        {
            if (state.OwnNativeLiquid)
            {
                try
                {
                    if (state.Actor != null && ReadCurrentFaceSiruLevel(state.Actor) == state.NativeLiquidApplied)
                        using (BeginInternalFaceWrite()) TrySetFaceSiruLevel(state.Actor, state.NativeLiquidOriginal, out _);
                }
                catch (Exception ex) { LogWarn("[preset-effects] restore native liquid: " + ex.Message); }
                finally { state.OwnNativeLiquid = false; }
            }
            RestorePresetModEffects(state);
            if (!state.Spiral) return;
            try { _presetSpiralEnd?.Invoke(null, new object[] { GUID, state.Actor }); }
            catch (Exception ex) { LogWarn("[preset-effects] release spiral: " + ex.GetBaseException().Message); }
            finally { state.Spiral = false; }
        }
        private void ReleasePresetExtras()
        {
            // Cleanup deliberately remains legal after SessionGate invalidation.
            foreach (var state in _presetExtras.Values) ReleasePresetExtra(state);
            _presetExtras.Clear();
        }
    }
}
