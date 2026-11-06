using System;
using BepInEx;
using BepInEx.Configuration;
using CanonGate;
using MainGameUnifiedUi;
using MainGameUiInputCapture;
using UnityEngine;

namespace MainGameCandle
{
    [BepInPlugin(Guid, "MainGameCandle", "1.3.2")]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    [BepInDependency(GateApi.PluginGuid)]
    [BepInDependency("com.kks.maingame.transformgizmo")]
    [BepInDependency("com.kks.maingame.unifiedui")]
    [BepInDependency("com.kks.maingame.uiinputcapture")]
    [BepInDependency("com.kks.maingame.magicpaint", "1.3.1")]
    [BepInDependency("com.kks.maingame.voicefaceeventbridge", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.kks.maingame.candle";
        IDisposable _registration;
        GateApi.Permit _permit;
        MonoBehaviour _scene;
        ulong _lastSession;
        bool _active, _desktopFocused = true, _vrFocused = true, _paused;
        bool _focused => !_paused && (VRGIN.Core.VR.Active ? _vrFocused : _desktopFocused);
        ConfigEntry<bool> _enabled, _lit, _voice, _expression;
        ConfigEntry<float> _startAngle, _flowMultiplier, _dropVolume, _variation, _wickHeight, _waxThickness;
        string _status = "H開始待ち";
        bool _configDirty;
        bool CanAct => _active && isActiveAndEnabled && _enabled.Value && _permit.IsValid;

        void Awake()
        {
            Config.SaveOnConfigSet = false;
            _enabled = Config.Bind("General", "Enabled", true, "ロウソクを有効にする。再有効化は次のHから。");
            _lit = Config.Bind("Candle", "Lit", true, "炎・灯り・自動滴下を有効にする。");
            _startAngle = Config.Bind("Wax", "StartAngle", 45f, new ConfigDescription("直立から滴下開始までの角度（度）。", new AcceptableValueRange<float>(0, 85)));
            _flowMultiplier = Config.Bind("Wax", "FlowMultiplier", 1f, new ConfigDescription("滴下頻度の倍率。1倍で水平約1.5滴/秒。角度で頻度が上がり、135度以上で消火します。", new AcceptableValueRange<float>(0, 5)));
            _dropVolume = Config.Bind("Wax","DropVolume",3f,new ConfigDescription("一粒の基準体積。旧版を1として初期値3倍。",new AcceptableValueRange<float>(.25f,20)));
            _variation = Config.Bind("Wax","Variation",100f,new ConfigDescription("間隔と量のばらつき。0で一定、100で小粒と大粒を大きく変化させる。",new AcceptableValueRange<float>(0,100)));
            _wickHeight = Config.Bind("Candle","WickHeightMm",18f,new ConfigDescription("芯と炎の根元の高さ。芯の付け根からの長さ（mm）。",new AcceptableValueRange<float>(6,40)));
            _waxThickness=Config.Bind("Wax","ThicknessMm",1.2f,new ConfigDescription("付着した蝋の厚み（mm）。0で平面。付着済みの蝋にも即反映します。",new AcceptableValueRange<float>(0,5)));
            _voice = Config.Bind("Reaction", "Voice", true, "肌へ蝋が付着したとき既存Bridgeへ短い喘ぎ声を要求する。");
            _expression = Config.Bind("Reaction", "Expression", true, "肌へ蝋が付着したとき短い表情反応を出す。");
            _enabled.SettingChanged += EnabledChanged;
            _lit.SettingChanged += LitChanged;
            _expression.SettingChanged += ExpressionChanged;
            _voice.SettingChanged += VoiceChanged;
            _wickHeight.SettingChanged += WickHeightChanged;
            _waxThickness.SettingChanged += WaxThicknessChanged;
            _dropVolume.SettingChanged += FlowChanged;_variation.SettingChanged += FlowChanged;_flowMultiplier.SettingChanged += FlowChanged;
            Config.SettingChanged += ConfigChanged;
            _registration = GateApi.Register(Guid, StartSession, StopSession);
            UnifiedUiApi.RegisterTab(Guid, "ロウソク", DrawTab, 625);
        }
        void StartSession()
        {
            var c = GateApi.Current;
            if (!_enabled.Value || !isActiveAndEnabled || c == null || c.Kind != GateApi.SessionKind.H || c.Scene == null || c.Id == _lastSession) return;
            _lastSession = c.Id; _permit = GateApi.Capture(Guid);
            if (!_permit.IsValid) return;
            _scene = c.Scene; _active = true; _desktopFocused = Application.isFocused; _vrFocused = true; _paused = false;
            try { ConnectVr(); _status = "ロウソクを呼び出してください。"; }
            catch (Exception ex) { StopSession(); _status = "開始失敗：" + ex.Message; }
        }
        void StopSession()
        {
            _active = false; _permit = default;
            Cleanup(DisconnectVr);Cleanup(DeleteCandle);Cleanup(ClearWax);Cleanup(ClearReactions);Cleanup(ClearSurfaces);
            Cleanup(ClearUiCapture);Cleanup(SaveSettings);_scene = null;
        }
        void EnabledChanged(object sender, EventArgs e) { if (!_enabled.Value) StopSession(); }
        void LitChanged(object sender, EventArgs e)
        {
            if (_lit.Value && _model!=null && !CandleFlow.CanIgnite(Vector3.Angle(_model.Root.transform.up,Vector3.up)))
            {
                _lit.Value=false;_status="135度未満に戻してから点火してください。";return;
            }
            // Extinguishing does not detach a candle from the hand holding it.
            if (!_lit.Value) { ClearFlying(); ClearReactions(); }
            ApplyLight(); SyncLoop();
        }
        void ExpressionChanged(object sender, EventArgs e) { if (!_expression.Value) ClearExpressions(); }
        void VoiceChanged(object sender, EventArgs e) { if (!_voice.Value) StopReactionVoice(); }
        void FlowChanged(object sender,EventArgs e){_flow.Reset();}
        void WaxThicknessChanged(object sender,EventArgs e)
        {
            if(!CanAct)return;
            foreach(var layer in _waxLayers.Values)layer.SetThickness(_waxThickness.Value*.001f);
        }
        void WickHeightChanged(object sender,EventArgs e)
        {
            if(_model==null||!CanAct)return;
            _model.SetLit(false);_model.SetWickLength(_wickHeight.Value*.001f);ApplyLight();
        }
        void OnApplicationFocus(bool focused)
        {
            _desktopFocused = focused; RefreshInputFocus();
        }
        void OnVrInputFocus(bool focused) { _vrFocused = focused; RefreshInputFocus(); }
        void RefreshInputFocus()
        {
            if (!_focused) { ReleaseHand(); FinishRetiringLocks(); HideGizmo(); ClearFlying(); ClearReactions(); ClearUiCapture(); }
            ApplyLight(); SyncLoop();
        }
        void OnApplicationPause(bool paused) { _paused = paused; RefreshInputFocus(); }
        void OnDisable() { StopSession(); }
        void OnDestroy()
        {
            UnifiedUiApi.UnregisterTab(Guid);
            _enabled.SettingChanged -= EnabledChanged; _lit.SettingChanged -= LitChanged;
            _expression.SettingChanged -= ExpressionChanged; _voice.SettingChanged -= VoiceChanged;
            _wickHeight.SettingChanged -= WickHeightChanged;
            _waxThickness.SettingChanged -= WaxThicknessChanged;
            _dropVolume.SettingChanged -= FlowChanged;_variation.SettingChanged -= FlowChanged;_flowMultiplier.SettingChanged -= FlowChanged;
            Config.SettingChanged -= ConfigChanged;
            try { _registration?.Dispose(); } finally { StopSession(); }
        }
        void ClearUiCapture() { UiInputCaptureApi.EndOwner(Guid); UiInputCaptureApi.ClearInputBlockRects(Guid); UiInputCaptureApi.SetIdleCursorUnlock(Guid, false); }
        void ConfigChanged(object sender, SettingChangedEventArgs e) { _configDirty = true; }
        void SaveSettings() { if (_configDirty) { Config.Save(); _configDirty = false; } }
        void Cleanup(Action action) { try { action(); } catch (Exception ex) { _status = "後始末：" + ex.Message; } }
    }
}
