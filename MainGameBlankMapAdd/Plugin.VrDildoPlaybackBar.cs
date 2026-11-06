using System;
using BepInEx.Bootstrap;
using UnityEngine;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private const string VrDildoGuid = "com.kks.maingame.vrdildo";
        private readonly GadgetBarConnection _vrDildoBar = new GadgetBarConnection(VrDildoGuid);
        private GUIStyle _vrDildoButtonStyle;

        private void DrawVrDildoButtons(Rect rect)
        {
            bool original = GUI.enabled;
            if (_vrDildoButtonStyle == null)
                _vrDildoButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, padding = new RectOffset(2, 2, 0, 0), wordWrap = false };
            DrawGadgetBarButton(new Rect(rect.x, rect.y, 58f, rect.height), "ディルド", _vrDildoBar, _vrDildoButtonStyle);
            GUI.enabled = original;
        }
    }
}
