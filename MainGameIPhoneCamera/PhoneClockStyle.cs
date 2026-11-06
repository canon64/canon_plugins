using UnityEngine;
using UnityEngine.UI;

namespace MainGameIPhoneCamera
{
    internal static class PhoneClockStyle
    {
        internal static Font CreateFont()
            => Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial" }, 64);

        internal static void Apply(Text text, Font font)
        {
            text.font = font;
            text.fontStyle = FontStyle.Bold;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, .9f);
            outline.effectDistance = new Vector2(2, -2);
        }
    }
}
