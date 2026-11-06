using System;
using System.IO;
using BepInEx.Configuration;
using MapEnvironmentRuntime;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    public sealed partial class Plugin
    {
        private const string HotelLabel = "ホテルモード";
        private HotelModeSession _hotel;
        private ConfigEntry<bool> _hotelDiagnostics;
        private AiModeHelpScreen _hotelFailure;
        private string _hotelResultNotification;
        public void TraceHotelPoseCatalog(string stage, object source, bool allowed)
            => _hotel?.TraceHotelPoseCatalog(stage, source, allowed);
        internal static void ReportHotelSaveResult(bool success, string message)
        {
            if (_instance == null) return;
            if (success) return; // Unacknowledged failures belong to the common notification service.
            _instance._hotelResultNotification = MainGameNotifications.NotificationApi.Publish(Guid,
                "ホテル：保存・復元の結果", message, MainGameNotifications.NotificationSeverity.Failure);
        }
        private bool TitleModeBusy => _mode != null || _hotel != null || _hotelFailure != null;
        private bool HandleHotelClick(TitleScene title)
        {
            if (!isActiveAndEnabled || !_bindings.ContainsKey(title)) return true;
            if (TitleModeBusy) return false;
            if (_hotelDiagnostics == null)
            {
                _hotelDiagnostics = Config.Bind("HotelMode", "DiagnosticsEnabled", false, "ホテルモードの追加診断ログ。有効時だけ開始・終了・失敗・本編音声の定位を記録します。");
                _hotelDiagnostics.SettingChanged += HotelDiagnosticsChanged;
            }
            string content = Path.Combine(Path.GetDirectoryName(Info.Location), "Content", "Hotel");
            var trace = BeginStartupTrace("hotel");
            try
            {
                trace?.Mark("content.validate.begin");
                var parameters = Parameters.Load(Path.Combine(content, "parameters.json"));
                var floors = JsonCodec.FromJson<HotelMapManifest>(File.ReadAllText(Path.Combine(content, "floors.json")));
                floors.Validate();
                foreach (var floor in floors.floors)
                    if (!File.Exists(Path.Combine(content, floor.bundle))) throw new FileNotFoundException("ホテル用のマップデータがありません：" + floor.id);
                trace?.Mark("content.validate.end");
                var confirmation = Illusion.Game.Utils.Sound.Play(Illusion.Game.SystemSE.ok_l);
                var root = new GameObject("Hotel Mode Session");
                _hotel = root.AddComponent<HotelModeSession>();
                _hotel.StartupDiagnostics = trace;
                _hotel.Initialize(title, content, parameters, confirmation,
                    () => _hotelDiagnostics.Value,
                    message => { if (_hotelDiagnostics.Value) Logger.LogInfo(message); },
                    exception => { if (_hotelDiagnostics.Value) Logger.LogError(exception); },
                    () => _hotel = null,
                    message => ShowHotelFailure(title, parameters, message));
            }
            catch (Exception ex)
            {
                trace?.Mark("entry.failed", () => ex.ToString()); trace?.Dispose();
                if (_hotelDiagnostics.Value) Logger.LogError(ex);
                if (_hotel != null) { _hotel.Cancel(); return false; }
                var binding = _bindings[title];
                binding.hotelText.text = HotelLabel + "（データ読込失敗）";
            }
            return false;
        }
        private void HotelDiagnosticsChanged(object sender, EventArgs args) => _hotel?.RefreshVoiceDiagnostics();
        private void ShowHotelFailure(TitleScene title, Parameters parameters, string message)
        {
            if (!isActiveAndEnabled || title == null) return;
            _hotelFailure = new AiModeHelpScreen(title, transform, parameters, () =>
            {
                if (_hotelFailure == null || _hotelFailure.Visible) return;
                _hotelFailure.Dispose(); _hotelFailure = null;
                MainGameUiInputCapture.UiInputCaptureApi.ClearInputBlockRects(Guid);
                MainGameUiInputCapture.UiInputCaptureApi.SetIdleCursorUnlock(Guid, false);
            }, null, HotelLabel, "ホテルを読み込めませんでした。\n" + message + "\n\n「閉じる」でタイトルに戻ります。");
            _hotelFailure.SetVisible(true);
            MainGameUiInputCapture.UiInputCaptureApi.SetInputBlockRects(Guid, new[] { _hotelFailure.InputRect });
            MainGameUiInputCapture.UiInputCaptureApi.SetIdleCursorUnlock(Guid, true);
        }
        private void CloseHotelImmediately()
        {
            _hotel?.Cancel();
            if (_hotelFailure != null)
            {
                _hotelFailure.Dispose(); _hotelFailure = null;
                MainGameUiInputCapture.UiInputCaptureApi.ClearInputBlockRects(Guid);
                MainGameUiInputCapture.UiInputCaptureApi.SetIdleCursorUnlock(Guid, false);
            }
        }
    }
}
