using MainGameBlankMapAdd.UI;
using UnityEngine;

Console.WriteLine("Tooltip window event-sequence regression started (no Unity/game launch)");
int checks = 0;
void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
var window = new PlaybackHelpWindow();
window.Draw();
Check(GUI.Windows.Count == 0, "No window when no help is available.");
var text = new GUIContent("拡張タブの説明");
window.Set(new Rect(200,100,460,150), text, new GUIStyle());
foreach (string phase in new[] { "Layout", "MouseMove", "MouseDown", "MouseDrag", "MouseUp", "KeyDown", "Repaint" })
{
    GUI.Windows.Clear(); GUI.RegistrationEnabled.Clear();
    GUI.enabled = true;
    GUI.color = new Color(.2f,.3f,.4f,.5f);
    GUI.Window(0x50425549, new Rect(0,0,1000,600), id => { }, GUIContent.none, GUIStyle.none);
    window.Draw();
    Check(GUI.Windows.SequenceEqual(new[] { 0x50425549, PlaybackHelpWindow.WindowId }), phase + ": tooltip is registered after and brought above expanded panel.");
    Check(!GUI.RegistrationEnabled.Last(), phase + ": tooltip window is registered disabled for input.");
    Check(GUI.enabled && GUI.color.r == .2f && GUI.color.a == .5f, phase + ": GUI state restored.");
    Check(GUI.LastBox.x == 0 && GUI.LastBox.y == 0 && GUI.LastBox.width == 460 && ReferenceEquals(GUI.LastContent, text), phase + ": content drawn in window-local coordinates.");
}
foreach (bool paintFailure in new[] { false, true })
{
    GUI.enabled = true;
    GUI.color = new Color(.2f,.3f,.4f,.5f);
    GUI.ThrowOnWindow = !paintFailure; GUI.ThrowOnBox = paintFailure;
    try { window.Draw(); throw new Exception("Expected native API failure"); }
    catch (InvalidOperationException) { }
    finally { GUI.ThrowOnWindow = GUI.ThrowOnBox = false; }
    Check(GUI.enabled && GUI.color.r == .2f && GUI.color.a == .5f, "Restore state after native API or callback exception.");
}
var delayedCallback=GUI.LastCallback;
window.Clear();
delayedCallback(PlaybackHelpWindow.WindowId);
Check(true,"Deferred native callback after Clear safely skips null content/style.");
GUI.Windows.Clear();
window.Draw();
Check(GUI.Windows.Count == 0, "Closing, focus loss or session end removes the window.");
window.Set(new Rect(20,30,100,50), new GUIContent("new item"), new GUIStyle());
window.Draw();
Check(GUI.LastContent.text == "new item" && GUI.Windows.Single() == PlaybackHelpWindow.WindowId, "Reopen uses new text and the same native window ID.");
Console.WriteLine($"PASS: {checks} window registration/state checks. Actual Unity pixel ordering remains untested.");
