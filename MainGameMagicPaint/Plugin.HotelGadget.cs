using System;
using System.Linq;
using CanonPlugins.Shared;
using UnityEngine;
namespace MainGameMagicPaint
{
    public sealed partial class Plugin
    {
        public bool HotelGadgetAllowed => CanAct;
        public void SetHotelGadgetVisible(bool visible)
        {
            if (!CanAct) throw new InvalidOperationException("マジック・スプレーはこのHでは許可されていません。");
            if (visible && _model == null) SpawnTool();
            else if (!visible) DeleteTool();
            if (visible && _model == null) throw new InvalidOperationException("道具を生成できません。");
        }
        public string CaptureHotelGadget(Transform room)
        {
            var data = GadgetRoomSnapshot.Capture(room, _model?.Root.transform);
            data.settings = GadgetConfigSnapshot.Capture(Config); return data.Encode();
        }
        public void RestoreHotelGadget(Transform room, string json)
        {
            if (!CanAct) throw new InvalidOperationException("マジック・スプレーはこのHでは許可されていません。");
            var data = GadgetRoomSnapshot.Decode(json);
            if (data.visible && data.poses.Length != 1) throw new ArgumentException("マジック・スプレーの配置データが不正です。");
            GadgetConfigSnapshot.Apply(Config, data.settings);
            // Changing tools persists the current live colour. Keep the saved colour before
            // that operation, then apply it to both the tool and its persisted selection.
            var savedTool = _preferredTool.Value; int savedColor = _preferredColor.Value;
            ReleaseHand(); ChangeTool(savedTool);
            _colorIndex = Mathf.Clamp(savedColor, 0, _tool == PaintTool.Marker ? 9 : 10);
            _preferredColor.Value = _colorIndex;
            SetHotelGadgetVisible(data.visible); if (!data.visible) return;
            data.poses.Single().Apply(room, _model.Root.transform); _model.SetColor(InkColor);
        }
    }
}
