using System;
using System.Collections.Generic;
using MainGameAiShoujoMode;
using UnityEngine;

internal static class MenuChecks
{
    internal static void Run()
    {
        bool saved = false, loaded = false, helped = false;
        var menu = new AiMomentSaveMenu(() => { saved = true; return "保存成功"; }, () => { loaded = true; return "再開開始"; }, () => helped = true);
        void Check(bool result, string label) { if (!result) throw new Exception(label); Console.WriteLine("PASS " + label); }
        menu.Draw();
        Check(!saved && !loaded && !helped, "opening the existing menu does not save or load");
        GUILayout.Click = "セーブ"; menu.Draw();
        Check(saved && !loaded && GUILayout.Labels.Contains("保存成功"), "menu Save invokes only save and shows its result");
        GUILayout.Click = "ロード"; menu.Draw();
        Check(loaded && GUILayout.Labels.Contains("再開開始"), "menu Load invokes resume and shows its result");
        GUILayout.Click = "操作説明"; menu.Draw();
        Check(helped, "help remains accessible through the existing menu");
        menu = new AiMomentSaveMenu(() => throw new InvalidOperationException("動作中は保存不可"), () => "", () => { });
        GUILayout.Click = "セーブ"; menu.Draw(); GUILayout.Click = null;
        Check(GUILayout.Labels.Contains("動作中は保存不可"), "save rejection remains visible in the menu");
    }
}
namespace UnityEngine
{
    internal static class GUI { internal static Skin skin = new Skin(); }
    internal sealed class Skin { internal object box = new object(); }
    internal sealed class GUILayoutOption { }
    internal static class GUILayout
    {
        internal static string Click;
        internal static readonly List<string> Labels = new List<string>();
        internal static void BeginVertical(object style) { }
        internal static void EndVertical() { }
        internal static void BeginHorizontal() { }
        internal static void EndHorizontal() { }
        internal static void Label(string label) => Labels.Add(label);
        internal static bool Button(string label, params GUILayoutOption[] options) => Click == label;
        internal static GUILayoutOption ExpandWidth(bool value) => new GUILayoutOption();
    }
}
