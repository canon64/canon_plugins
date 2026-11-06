using System;
using BepInEx.Bootstrap;
using UnityEngine;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private readonly GadgetBarConnection _chainBar = new GadgetBarConnection("com.kks.maingame.vrneckchain");
        private readonly GadgetBarConnection _candleBar = new GadgetBarConnection("com.kks.maingame.candle",
            "左クリック：初回だけロウソクを目の前へ呼び出し→以後ギズモ表示／非表示 / 右クリック：ロウソク削除（付着した蝋は残す）");
        private readonly GadgetBarConnection _magicPaintBar = new GadgetBarConnection("com.kks.maingame.magicpaint",
            "左クリック：初回だけマジック・スプレーを目の前に呼び出し→以後ギズモ表示／非表示 / 右クリック：アイテム削除（落書きは残す）");
        private readonly GadgetBarConnection _iPhoneBar = new GadgetBarConnection("com.kks.maingame.iphonecamera",
            "左クリック：初回だけiPhoneを目の前に呼び出し→以後ギズモ表示／非表示 / 右クリック：削除");
        private readonly GadgetBarConnection _desktopBar = new GadgetBarConnection("com.kks.maingame.desktopdisplay",
            "左クリック：初回だけディスプレイを目の前へ呼び出し→以後ギズモ表示／非表示 / 右クリック：削除・デスクトップ取得停止");
        private Action _syringePrimaryAction;
        private Action _syringeDelete;
        private Func<string> _syringeHint;

        private void OperateSyringeFromBar(bool delete)
        {
            try
            {
                // Resolve on explicit interaction only; no scene search or retry polling.
                if (_syringePrimaryAction == null || _syringeDelete == null)
                {
                    if (!Chainloader.PluginInfos.TryGetValue("com.kks.maingame.syringe", out var info) || info.Instance == null)
                    { LogWarn("注射器プラグインが読み込まれていません。"); return; }
                    var type = info.Instance.GetType();
                    var primary = (Action)Delegate.CreateDelegate(typeof(Action), info.Instance, type.GetMethod("PrimaryAction"));
                    var remove = (Action)Delegate.CreateDelegate(typeof(Action), info.Instance, type.GetMethod("DeleteSyringe"));
                    var hint = (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), info.Instance, type.GetMethod("GetPrimaryActionHint"));
                    _syringePrimaryAction = primary; _syringeDelete = remove; _syringeHint = hint;
                }
                if (delete) _syringeDelete(); else _syringePrimaryAction();
            }
            catch (Exception ex) { LogWarn("注射器の操作: " + ex.Message); }
        }
        private Action _kaleidoscopeToggleManual;
        private Func<bool> _kaleidoscopeIsManual;
        private float _kaleidoscopeResolveAt;
        private Action _fpsToggleDisplay;
        private Func<bool> _fpsIsDisplayEnabled;
        private float _fpsResolveAt;

        private void DrawDisplayToggleButtons(Rect kaleidoscopeRect, Rect fpsRect)
        {
            if (_kaleidoscopeToggleManual == null && Time.unscaledTime >= _kaleidoscopeResolveAt)
            {
                _kaleidoscopeResolveAt = Time.unscaledTime + 3f;
                try
                {
                    if (Chainloader.PluginInfos.TryGetValue("com.kks.maingame.kaleidoscope", out var info) && info.Instance != null)
                    {
                        var type = info.Instance.GetType();
                        _kaleidoscopeToggleManual = (Action)Delegate.CreateDelegate(typeof(Action), info.Instance, type.GetMethod("ToggleManualActive"));
                        _kaleidoscopeIsManual = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), info.Instance, type.GetMethod("IsManualActive"));
                    }
                }
                catch (Exception ex) { LogWarn("Kaleidoscope bar connection: " + ex.Message); }
            }

            if (_fpsToggleDisplay == null && Time.unscaledTime >= _fpsResolveAt)
            {
                _fpsResolveAt = Time.unscaledTime + 3f;
                try
                {
                    if (Chainloader.PluginInfos.TryGetValue("com.kks.maingame.fpsdisplay", out var info) && info.Instance != null)
                    {
                        var type = info.Instance.GetType();
                        _fpsToggleDisplay = (Action)Delegate.CreateDelegate(typeof(Action), info.Instance, type.GetMethod("ToggleDisplay"));
                        _fpsIsDisplayEnabled = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), info.Instance, type.GetMethod("IsDisplayEnabled"));
                    }
                }
                catch (Exception ex) { LogWarn("FPS display bar connection: " + ex.Message); }
            }

            bool original = GUI.enabled;
            GUI.enabled = original && _kaleidoscopeToggleManual != null;
            string kaleidoscopeLabel = _kaleidoscopeIsManual != null && _kaleidoscopeIsManual() ? "万華鏡OFF" : "万華鏡ON";
            if (GUI.Button(kaleidoscopeRect, new GUIContent(kaleidoscopeLabel, "万華鏡の手動表示を切り替える")))
                _kaleidoscopeToggleManual?.Invoke();

            GUI.enabled = original && _fpsToggleDisplay != null;
            string fpsLabel = _fpsIsDisplayEnabled != null && _fpsIsDisplayEnabled() ? "FPS OFF" : "FPS ON";
            if (GUI.Button(fpsRect, new GUIContent(fpsLabel, "右上のFPS表示を切り替える（0.5秒平均・設定を保存）")))
                _fpsToggleDisplay?.Invoke();
            GUI.enabled = original;
        }

        private void DrawEffectButtons(Func<float, Rect> nextRect)
        {
            bool original = GUI.enabled;
            DrawGadgetBarButton(nextRect(58f), "鎖", _chainBar);

            GUI.enabled = original;
            var syringeRect = nextRect(58f);
            RegisterVrGadgetDelete(syringeRect, "注射器", () => OperateSyringeFromBar(true));
            var syringeEvent = Event.current;
            int syringeDeleteControl = GUIUtility.GetControlID("SyringeDelete".GetHashCode(), FocusType.Passive, syringeRect);
            if (GUI.enabled && syringeEvent != null && syringeEvent.type == EventType.MouseDown &&
                syringeEvent.button == 1 && syringeRect.Contains(syringeEvent.mousePosition))
            {
                GUIUtility.hotControl = syringeDeleteControl;
                syringeEvent.Use();
            }
            if (syringeEvent != null && GUIUtility.hotControl == syringeDeleteControl &&
                syringeEvent.type == EventType.MouseUp && syringeEvent.button == 1)
            {
                GUIUtility.hotControl = 0;
                if (GUI.enabled && syringeRect.Contains(syringeEvent.mousePosition)) OperateSyringeFromBar(true);
                syringeEvent.Use();
            }
            string syringeHint = _syringeHint != null ? _syringeHint() : "左クリックごとに呼出→刺入→投与→抜く→補充 / 右クリック：削除";
            if (GUI.Button(syringeRect, new GUIContent("注射器", syringeHint + "\nVR：ボタンを指してBクリックで削除")))
                OperateSyringeFromBar(false);

            var itemGroup = nextRect(PlaybackButtonFlow.PhoneMagicCandleWidth);
            DrawGadgetBarButton(new Rect(itemGroup.x, itemGroup.y, 58f, itemGroup.height), "iPhone", _iPhoneBar);
            DrawGadgetBarButton(new Rect(itemGroup.x + 62f, itemGroup.y, 74f, itemGroup.height), "マジック", _magicPaintBar);
            DrawGadgetBarButton(new Rect(itemGroup.x + 140f, itemGroup.y, 86f, itemGroup.height), "ロウソク", _candleBar);
            DrawGadgetBarButton(nextRect(110f), "ディスプレイ", _desktopBar);

            GUI.enabled = original;
        }
    }
}
