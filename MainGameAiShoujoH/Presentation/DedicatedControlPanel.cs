using System;
using System.Globalization;
using MainGameAiShoujoH.Contracts;
using MainGameAiShoujoH.Progression;
using MainGameUiInputCapture;
using UnityEngine;

namespace MainGameAiShoujoH.Presentation
{
    public sealed partial class DedicatedControlPanel : MonoBehaviour
    {
        private ProgressionModule progression;
        private PresentationModule presentation;
        private SessionContext context;
        private Rect window = new Rect(24, 64, 400, 310);
        private string owner, speedText = "0";
        private bool drag, released, captureActive, focused = true;
        private Vector2 scroll;
        private string error;
        private Integration.NativeResidentProfile profile;
        private bool profileOpen;
        private bool stopFemale, stopMale;
        private string skillsText;
        private bool motionsOpen;
        private string selectedMotion;
        private readonly System.Collections.Generic.Dictionary<string, string> numberText = new System.Collections.Generic.Dictionary<string, string>();
        public void Initialize(ProgressionModule value, SessionContext session, PresentationModule view)
        {
            if (!UiInputCaptureApi.IsAvailable) throw new InvalidOperationException("AI H controls require the common UI input service.");
            progression = value; presentation = view; context = session; stopFemale = session.Request.Settings.StopFemale; stopMale = session.Request.Settings.StopMale; owner = "ai-h:" + context.Id.ToString("N");
            foreach (var participant in session.Request.Participants)
                if (participant.Role != "male") { profile = Plugin.ReadResidentProfile(participant.Identity); skillsText = string.Join(",", profile.Skills ?? new int[0]); break; }
        }
        private void OnGUI()
        {
            if (released || !focused || context == null || context.CanInteract == null || !context.CanInteract()) { if (captureActive) ClearCapture(); return; }
            if (!captureActive) { captureActive = true; UiInputCaptureApi.SetIdleCursorUnlock(owner, true); }
            window.x = Mathf.Clamp(window.x, 0, Mathf.Max(0, Screen.width - window.width));
            window.y = Mathf.Clamp(window.y, 0, Mathf.Max(0, Screen.height - window.height));
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && window.Contains(e.mousePosition)) drag = true;
            if (e.type == EventType.MouseUp && e.button == 0) drag = false;
            // Original Proc wheel uses only its sign and is blocked over UI. Consume the actual input event once.
            if (e.type == EventType.ScrollWheel && !window.Contains(e.mousePosition) && !UiInputCaptureApi.IsPointerInputBlockedForOwner(owner))
            { Send("wheel", null, -Math.Sign(e.delta.y)); e.Use(); }
            UiInputCaptureApi.Sync(owner, "drag", drag);
            UiInputCaptureApi.SetInputBlockRects(owner, new[] { window });
            window = GUILayout.Window(GetInstanceID(), window, Draw, context.Request.UseKksOnePoseTrial ? "AI少女 専用H / KKS純正体位" : "AI少女 専用H");
            UiInputCaptureApi.SetInputBlockRects(owner, new[] { window });
        }
        private void Draw(int id)
        {
            var engine = progression.Engine;
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(Mathf.Max(140, Mathf.Min(Screen.height - 150, (profileOpen || appearanceOpen || sceneControlsOpen) ? 530 : 280))));
            GUILayout.Label("進行: " + engine.State);
            GUILayout.Label("女性 " + engine.Female.ToString("P0") + "  男性 " + engine.Male.ToString("P0"));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("開始 / 加速", GUILayout.ExpandWidth(false))) Send("wheel", null, 1);
            if (GUILayout.Button("減速 / 抜く", GUILayout.ExpandWidth(false))) Send("wheel", null, -1);
            if (context.Request.KksPose != null && (engine.State == "WLoop" || engine.State == "SLoop") && GUILayout.Button("弱 / 強を切替", GUILayout.ExpandWidth(false))) Send("loop-change");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); GUILayout.Label("速度", GUILayout.Width(40));
            float selected = GUILayout.HorizontalSlider(progression.ManualSpeed, 0, progression.ManualSpeedMaximum, GUILayout.Width(180));
            if (Math.Abs(selected - progression.ManualSpeed) > .00001f) { speedText = selected.ToString("0.00", CultureInfo.InvariantCulture); Send("speed", null, selected); }
            GUI.SetNextControlName("ai-h-speed");
            string text = GUILayout.TextField(speedText, GUILayout.Width(65));
            if (text != speedText) { speedText = text; if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float n) && !float.IsNaN(n) && !float.IsInfinity(n)) Send("speed", null, n); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(engine.Definition.Initiative == 0, "手動", "Button", GUILayout.Width(80)) && engine.Definition.Initiative != 0) Send("initiative", null, 0);
            if (GUILayout.Toggle(engine.Definition.Initiative != 0, "自動", "Button", GUILayout.Width(80)) && engine.Definition.Initiative == 0) Send("initiative", null, 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            bool femaleLock = GUILayout.Toggle(stopFemale, "女性ゲージ固定");
            if (femaleLock != stopFemale) { stopFemale = femaleLock; Send("lock-female", null, stopFemale ? 1 : 0); }
            bool maleLock = GUILayout.Toggle(stopMale, "男性ゲージ固定");
            if (maleLock != stopMale) { stopMale = maleLock; Send("lock-male", null, stopMale ? 1 : 0); }
            GUILayout.EndHorizontal();
            if (GUILayout.Button((motionsOpen ? "▼ " : "▶ ") + "体位選択", GUILayout.ExpandWidth(false))) motionsOpen = !motionsOpen;
            if (motionsOpen)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                var choices = progression.AvailableMotions;
                selectedMotion = progression.CurrentMotionId;
                GUILayout.Label("表示中 " + choices.Length + "体位（参加者・場所・解放条件・準備済み資産で絞り込み）");
                GUILayout.Label("体位名をクリックすると切り替わります。");
                foreach (string motion in choices)
                    if (GUILayout.Button((selectedMotion == motion ? "● " : "○ ") + NativeProcCatalog.Get(motion).Name)) ApplyMotionSelection(motion, choices);
                GUILayout.EndVertical();
            }
            DrawAppearance();
            DrawSceneControls();
            GUILayout.BeginVertical(GUI.skin.box); GUILayout.Label("終了動作");
            GUILayout.BeginHorizontal();
            Button("直前へ", Finish.Before); Button("中", Finish.Inside); Button("外", Finish.Outside); Button("同時", Finish.Same);
            GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); Button("飲む", Finish.Drink); Button("吐く", Finish.Vomit);
            if (GUILayout.Button("弱体から回復", GUILayout.ExpandWidth(false))) Send("recover");
            GUILayout.EndHorizontal(); GUILayout.EndVertical();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("正常終了", GUILayout.ExpandWidth(false))) Plugin.End(EndReason.Completed);
            if (GUILayout.Button("中断して戻る", GUILayout.ExpandWidth(false))) Plugin.End(EndReason.Cancelled);
            GUILayout.EndHorizontal();
            if (GUILayout.Button((profileOpen ? "▼ " : "▶ ") + "AI専用の音声・関係設定", GUILayout.ExpandWidth(false))) profileOpen = !profileOpen;
            if (profileOpen && profile != null)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("この住人の専用設定（次回開始から反映）");
                bool merchantProfile = GUILayout.Toggle(profile.Merchant, "商人プロフィール（原本-90）");
                if (merchantProfile != profile.Merchant) { profile.Merchant = merchantProfile; profile.Personality = merchantProfile ? -90 : 0; }
                GUILayout.BeginHorizontal(); GUILayout.Label("音声", GUILayout.Width(42));
                GUI.enabled = !profile.Merchant;
                for (int voice = 0; voice < 6; voice++)
                    if (GUILayout.Toggle(profile.Personality == voice, (voice + 1).ToString(), "Button", GUILayout.Width(36))) profile.Personality = voice;
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                profile.PersonalPhase = Integer("関係段階", profile.PersonalPhase);
                profile.Bust = Integer("体格分類", profile.Bust);
                profile.Mood = Quantity("機嫌", profile.Mood, 0, 100);
                profile.Skill2 = Quantity("習熟", profile.Skill2, 0, 300);
                if (profile.Settings != null)
                {
                    var settings = profile.Settings;
                    GUILayout.Label("専用H開始設定（次回から）");
                    settings.StopFemale = GUILayout.Toggle(settings.StopFemale, "女性ゲージ固定");
                    settings.StopMale = GUILayout.Toggle(settings.StopMale, "男性ゲージ固定");
                    settings.Drug5 = GUILayout.Toggle(settings.Drug5, "原本薬効5");
                    settings.Force = GUILayout.Toggle(settings.Force, "強制条件");
                    settings.Futanari = GUILayout.Toggle(settings.Futanari, "両性条件");
                    settings.PlayerClothesEnabled = GUILayout.Toggle(settings.PlayerClothesEnabled, "主人公の個別衣服操作");
                    settings.PlayerAccessoriesEnabled = GUILayout.Toggle(settings.PlayerAccessoriesEnabled, "主人公・男性のアクセサリ操作");
                    settings.Constraint = GUILayout.Toggle(settings.Constraint, "拘束条件");
                    settings.SleepStart = GUILayout.Toggle(settings.SleepStart, "睡眠開始条件");
                    settings.FemaleSkillMultiplier = Quantity("女性倍率", settings.FemaleSkillMultiplier, .1f, 5);
                    settings.MaleSkillMultiplier = Quantity("男性倍率", settings.MaleSkillMultiplier, .1f, 5);
                    settings.YobaiBareRate = Quantity("発覚率", settings.YobaiBareRate, 0, 100);
                    GUILayout.Label("所持技能ID（カンマ区切り）");
                    skillsText = GUILayout.TextField(skillsText ?? string.Empty);
                }
                if (GUILayout.Button("次回Hへ保存", GUILayout.ExpandWidth(false)))
                    try {
                        var ids = new System.Collections.Generic.List<int>();
                        foreach (string token in (skillsText ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                        { if (!int.TryParse(token.Trim(), out int skill) || skill < 0) throw new ArgumentException("技能IDは0以上の整数を入力してください。"); if (!ids.Contains(skill)) ids.Add(skill); }
                        profile.Skills = ids.ToArray(); Plugin.SaveResidentProfile(profile); error = null;
                    } catch (Exception ex) { error = ex.Message; }
                GUILayout.EndVertical();
            }
            if (!string.IsNullOrEmpty(error)) GUILayout.Label(error);
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, window.width, 22));
        }
        private int Integer(string label, int value)
        {
            GUILayout.BeginHorizontal(); GUILayout.Label(label, GUILayout.Width(80));
            if (!numberText.TryGetValue(label, out string buffer)) buffer = value.ToString(CultureInfo.InvariantCulture);
            string text = GUILayout.TextField(buffer, GUILayout.Width(65)); numberText[label] = text;
            if (text != buffer && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed >= 0) value = parsed;
            GUILayout.EndHorizontal(); return value;
        }
        private float Quantity(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal(); GUILayout.Label(label, GUILayout.Width(80));
            float selected = GUILayout.HorizontalSlider(value, Math.Min(min, value), Math.Max(max, value), GUILayout.Width(180));
            if (!numberText.TryGetValue(label, out string buffer)) buffer = value.ToString("0.##", CultureInfo.InvariantCulture);
            if (Math.Abs(selected - value) > .00001f) { value = selected; buffer = value.ToString("0.##", CultureInfo.InvariantCulture); }
            string text = GUILayout.TextField(buffer, GUILayout.Width(65)); numberText[label] = text;
            if (text != buffer && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) && !float.IsNaN(parsed) && !float.IsInfinity(parsed)) value = parsed;
            GUILayout.EndHorizontal(); return value;
        }
        private void Button(string text, Finish finish) { bool enabled = GUI.enabled; GUI.enabled = enabled && progression.Engine.CanFinish(finish); if (GUILayout.Button(text, GUILayout.ExpandWidth(false))) Send("finish", finish.ToString()); GUI.enabled = enabled; }
        private void ApplyMotionSelection(string id, string[] choices)
        { MotionSelectionAction.Choose(id, progression.CurrentMotionId, choices, motion => Send("motion", motion)); }
        private bool Send(string name, string value = null, float amount = 0)
        { try { progression.Execute(new Command { Name = name, Value = value, Amount = amount }); error = null; return true; } catch (Exception ex) { error = ex.Message; context.ReportFailure(ex.Message); return false; } }
        private void OnApplicationFocus(bool value) { focused = value; if (!focused) ClearCapture(); else if (!released && owner != null && context?.CanInteract?.Invoke() == true) UiInputCaptureApi.SetIdleCursorUnlock(owner, true); }
        private void OnEnable() { if (!released && focused && owner != null && context?.CanInteract?.Invoke() == true) UiInputCaptureApi.SetIdleCursorUnlock(owner, true); }
        private void ClearCapture()
        {
            drag = false; captureActive = false; if (owner == null) return;
            UiInputCaptureApi.Sync(owner, "drag", false); UiInputCaptureApi.ClearInputBlockRects(owner); UiInputCaptureApi.SetIdleCursorUnlock(owner, false); UiInputCaptureApi.EndOwner(owner);
        }
        public void Release() { if (released) return; released = true; ClearCapture(); }
        private void OnDisable() => ClearCapture();
        private void OnDestroy() => Release();
    }
}
