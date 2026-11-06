using System;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using MainGameBlankMapAdd.Diagnostics;
using UnityEngine;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private ConfigEntry<bool> _hLocationDiagnostics;
        private long _hLocationTraceSequence;
        private void InitializeHLocationDiagnostics()
        {
            _hLocationDiagnostics = Config.Bind("HLocationDiagnostics", "Enabled", false,
                "H場所の保存・読込・復元と座標の上書きを記録します。OFFでは診断用の状態収集とファイル出力を停止します。");
        }
        private void TraceHPlace(string stage, Func<string> details)
        {
            HLocationTrace.Write(_hLocationDiagnostics?.Value == true,
                () => DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + " seq=" + (++_hLocationTraceSequence) +
                    " frame=" + Time.frameCount + " stage=" + stage + " " + details(),
                line => {
                    string directory = Path.Combine(_pluginDir, "_logs");
                    Directory.CreateDirectory(directory);
                    File.AppendAllText(Path.Combine(directory, "h-location.log"), line + Environment.NewLine);
                });
        }
        private string HPlaceRecord(HLocationPreset p) => p == null ? "preset=null" :
            "id=" + p.Id + " map=" + HTraceText(p.MapKey) + " name=" + HTraceText(p.Name) + " default=" + p.IsDefault +
            " position=" + HTraceVector(p.Position) + " rotation=" + HTraceVector(p.Rotation) +
            " camera=" + HTraceVector(p.CameraPosition) + " focus=" + HTraceVector(p.CameraFocus) + " fov=" + p.Fov.ToString("R", CultureInfo.InvariantCulture);
        private static string HTraceText(string value) => (value ?? "null").Replace("\r", "\\r").Replace("\n", "\\n");
        private static string HTraceVector(float[] value) => value == null ? "null" :
            "[" + string.Join(",", value.Select(v => v.ToString("R", CultureInfo.InvariantCulture))) + "]";
        private string HPlaceActual(Transform root) => " root=" + (root == null ? "null" : root.name + "#" + root.GetInstanceID()) +
            " active=" + (_hPlaceActive?.Id ?? "null") + " actors=" + string.Join(";", HPlaceActors().Select(t =>
                t.name + "#" + t.GetInstanceID() + " world=" + HTraceVector(HA(t.position)) +
                " local=" + (root == null ? "null" : HTraceVector(HA(root.InverseTransformPoint(t.position)))) +
                " rotation=" + HTraceVector(HA(t.eulerAngles))));
        private void TraceHPlaceDocument(string stage, HLocationDocument document)
        {
            TraceHPlace(stage, () => "path=" + HPlacePath + " records=" + document.Presets.Count + " data=" +
                string.Join(";", document.Presets.Select(HPlaceRecord)));
        }
        private void SaveHLocationDocument(HLocationDocument document)
        {
            TraceHPlaceDocument("save.begin", document);
            try { HLocationPresetStore.Save(HPlacePath, document); TraceHPlaceDocument("save.committed", document); }
            catch (Exception ex) { TraceHPlace("save.failed", () => "path=" + HPlacePath + " reason=" + HTraceText(ex.ToString())); throw; }
        }
        private void SaveHLocationImage(HLocationDocument document, HLocationPreset preset, byte[] png)
        {
            TraceHPlace("save-image.begin", () => "path=" + HPlacePath + " " + HPlaceRecord(preset));
            try { HLocationPresetStore.SaveImage(HPlacePath, HPlaceImageDir, document, preset, png); TraceHPlaceDocument("save-image.committed", document); }
            catch (Exception ex) { TraceHPlace("save-image.failed", () => "path=" + HPlacePath + " reason=" + HTraceText(ex.ToString())); throw; }
        }
        private void SaveHLocationPoses(PoseLocationDocument document)
        {
            TraceHPlacePoses("pose-save.begin", document);
            try { RoomCustomizationStore.Save(CategoryLocationPath, document, d => d.Validate()); TraceHPlacePoses("pose-save.committed", document); }
            catch (Exception ex) { TraceHPlace("pose-save.failed", () => "path=" + CategoryLocationPath + " reason=" + HTraceText(ex.ToString())); throw; }
        }
        private void TraceHPlacePoses(string stage, PoseLocationDocument document)
        {
            TraceHPlace(stage, () => "path=" + CategoryLocationPath + " automatic=" + document.Automatic +
                " initial=" + string.Join(";", document.Initial.Select(HPlaceRecord)) + " entries=" +
                string.Join(";", document.Entries.Select(e => "pose=" + HTraceText(e.Pose) + " " + HPlaceRecord(e.Placement))));
        }
    }
}
