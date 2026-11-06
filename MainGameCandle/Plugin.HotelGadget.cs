using System;
using System.Linq;
using CanonPlugins.Shared;
using UnityEngine;
namespace MainGameCandle
{
    public sealed partial class Plugin
    {
        public bool HotelGadgetAllowed => CanAct;
        public void SetHotelGadgetVisible(bool visible)
        {
            if (!CanAct) throw new InvalidOperationException("ロウソクはこのHでは許可されていません。");
            if (visible && _model == null && !Recall()) throw new InvalidOperationException("ロウソクを生成できません。");
            else if (!visible) DeleteCandle();
        }
        public string CaptureHotelGadget(Transform room)
        {
            var data = GadgetRoomSnapshot.Capture(room, _model?.Root.transform);
            data.settings = GadgetConfigSnapshot.Capture(Config); return data.Encode();
        }
        public void RestoreHotelGadget(Transform room, string json)
        {
            if (!CanAct) throw new InvalidOperationException("ロウソクはこのHでは許可されていません。");
            var data = GadgetRoomSnapshot.Decode(json);
            if (data.visible && data.poses.Length != 1) throw new ArgumentException("ロウソクの配置データが不正です。");
            GadgetConfigSnapshot.Apply(Config, data.settings);
            SetHotelGadgetVisible(data.visible); if (!data.visible) return;
            ReleaseHand(); HideGizmo(); ClearFlying(); data.poses.Single().Apply(room, _model.Root.transform);
            _model.SetWickLength(_wickHeight.Value * .001f); ApplyLight(); SyncLoop();
        }
    }
}
