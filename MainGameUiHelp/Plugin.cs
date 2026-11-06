using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace MainGameUiHelp
{
    [BepInDependency("canon.gate", "2.3.0")]
    [BepInPlugin(Guid, "MainGameUiHelp", "1.0.0")]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    [BepInProcess("CharaStudio")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.kks.maingame.uihelp";
        private static Plugin _instance;
        private Harmony _harmony;
        private readonly HelpCatalog _catalog = new HelpCatalog();
        private readonly Dictionary<Assembly, string> _owners = new Dictionary<Assembly, string>();
        private readonly Dictionary<string, string> _names = new Dictionary<string, string>();
        private ConfigEntry<bool> _enabled;
        private ConfigEntry<float> _delay;
        private ConfigEntry<int> _fontSize;
        private ConfigEntry<float> _width;
        private Func<Rect> _visibleRect;
        private string _pluginRoot;
        private bool _drawing;
        private bool _reportedError;
        private GUIStyle _popupStyle;
        private Texture2D _background;
        private string _hoverKey;
        private string _hoverText;
        private Rect _hoverScreenRect;
        private int _hoverFrame = -10;
        private float _hoverSince;
        private int _rowFrame = -10;
        private int _rowDepth;
        private Rect _rowScreenRect;
        private string _rowOwner;
        private string _rowLabel;
        private string _rowHelp;
        private string _rowRange;
        private readonly HostHelpCapture _hostCapture = new HostHelpCapture();

        // Optional host integration: the host renders the captured explanation itself.
        public static void BeginHostCapture()
        {
            if (_instance == null || !CanonPlugins.Shared.PluginActivityGate.Allowed) return;
            _instance._hostCapture.Begin();
            _instance._rowFrame = -10;
        }

        public static string TakeHostHelp()
        {
            Plugin p = _instance;
            return p == null ? null : p._hostCapture.Take(CanonPlugins.Shared.PluginActivityGate.Allowed && p._enabled.Value && Application.isFocused);
        }

        public static void EndHostCapture()
        {
            if (_instance == null) return;
            _instance._hostCapture.End();
            _instance._rowFrame = -10;
        }

        // For table cells whose heading is on a different row (no row inference).
        public static void ReportControl(Rect rect, string label, string help)
        {
            Plugin p = _instance;
            if (p == null || !p.CanObserve(rect) || !rect.Contains(Event.current.mousePosition)) return;
            string owner = p.ResolveOwner("field", out string context);
            if (owner == null) return;
            string explanation = p._catalog.Find(owner, label, null, context);
            if (!string.IsNullOrWhiteSpace(help))
                explanation = string.IsNullOrWhiteSpace(explanation) || help.StartsWith(explanation, StringComparison.Ordinal)
                    ? help : explanation + "\n" + help;
            if (!string.IsNullOrWhiteSpace(explanation)) p.Hover(owner, label, explanation, ScreenRect(rect));
        }

        private void Awake()
        {
            _instance = this;
            _pluginRoot = Path.GetFullPath(Path.Combine(Paths.PluginPath, "canon_plugins")) + Path.DirectorySeparatorChar;
            _enabled = Config.Bind("説明ポップアップ", "有効", true, "canon_plugins の設定項目にマウスを重ねると、その項目の説明を表示します。");
            _delay = Config.Bind("説明ポップアップ", "表示までの秒数", 0.3f,
                new ConfigDescription("同じ項目にマウスを置いてから説明が出るまでの時間。0ならすぐ表示します。", new AcceptableValueRange<float>(0f, 3f)));
            _fontSize = Config.Bind("説明ポップアップ", "文字サイズ", 15,
                new ConfigDescription("説明文の文字の大きさです。", new AcceptableValueRange<int>(10, 28)));
            _width = Config.Bind("説明ポップアップ", "横幅", 460f,
                new ConfigDescription("説明枠の横幅（画面ピクセル）。長文は自動で折り返します。", new AcceptableValueRange<float>(240f, 800f)));
            string path = Path.Combine(Path.GetDirectoryName(Info.Location), "help_catalog.json");
            if (!File.Exists(path)) throw new FileNotFoundException("説明カタログがありません", path);
            var file = JsonUtility.FromJson<HelpCatalogFile>(File.ReadAllText(path));
            foreach (var entry in file.entries ?? Array.Empty<HelpEntry>())
                _catalog.Add(entry.owner, entry.label, entry.text, entry.fallback);

            Type clip = typeof(GUI).Assembly.GetType("UnityEngine.GUIClip", true);
            _visibleRect = (Func<Rect>)Delegate.CreateDelegate(typeof(Func<Rect>),
                AccessTools.PropertyGetter(clip, "visibleRect"));
            _harmony = new Harmony(Guid);
            Patch("DoLabel", 3, nameof(LabelPrefix));
            Patch("DoButton", 4, nameof(ButtonPrefix));
            Patch("DoToggle", 5, nameof(TogglePrefix));
            Patch("DoTextField", 8, nameof(TextPrefix));
            Patch("Slider", 10, nameof(SliderPrefix));
            Patch("DoButtonGrid", 11, nameof(GridPrefix));
            Logger.LogInfo("Help popups ready: " + _catalog.Count + " explanations; canon_plugins UI only.");
            CanonPlugins.Shared.PluginActivityGate.Bind(this, Guid, CanonGate.GateApi.ActivityKind.Preview,
                () => { _hoverFrame = -10; _hostCapture.End(); }, () => { _hoverFrame = -10; _hostCapture.End(); _rowFrame = -10; });
            CanonPlugins.Shared.PluginActivityGate.Begin();
        }

        private void Patch(string name, int count, string prefix)
        {
            MethodInfo target = Array.Find(typeof(GUI).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
                m => m.Name == name && m.GetParameters().Length == count);
            if (target == null || target.GetMethodBody() == null)
                throw new MissingMethodException("UnityEngine.GUI", name + "/" + count);
            _harmony.Patch(target, prefix: new HarmonyMethod(typeof(Plugin), prefix));
        }

        private void LoadConfigHelp(Assembly assembly, string owner)
        {
            foreach (var info in Chainloader.PluginInfos.Values)
            {
                if (info.Instance == null || info.Instance.GetType().Assembly != assembly || !IsCanonPath(info.Location)) continue;
                _names[owner] = info.Metadata.Name;
                foreach (var pair in info.Instance.Config)
                {
                    string label = pair.Key.Key;
                    // The curated UI wording takes precedence over terse ConfigManager descriptions.
                    string description = pair.Value.Description.Description;
                    if (string.IsNullOrWhiteSpace(description)) continue;
                    if (!_catalog.HasItemHelp(owner, label)) _catalog.Add(owner, label, description);
                    foreach (object tag in pair.Value.Description.Tags ?? Array.Empty<object>())
                    {
                        if (tag == null) continue;
                        var field = tag.GetType().GetField("DispName", BindingFlags.Public | BindingFlags.Instance);
                        string alias = field?.GetValue(tag) as string;
                        if (!string.IsNullOrWhiteSpace(alias) && !_catalog.HasItemHelp(owner, alias))
                            _catalog.Add(owner, alias, description);
                    }
                }
            }
        }

        private bool IsCanonPath(string path)
        {
            return !string.IsNullOrEmpty(path) && Path.GetFullPath(path).StartsWith(_pluginRoot, StringComparison.OrdinalIgnoreCase);
        }

        private string ResolveOwner(string kind, out string context)
        {
            context = null;
            string firstOwner = null;
            foreach (StackFrame frame in new StackTrace(2, false).GetFrames() ?? Array.Empty<StackFrame>())
            {
                Assembly assembly = frame.GetMethod()?.DeclaringType?.Assembly;
                if (assembly == null || assembly == typeof(Plugin).Assembly || assembly == typeof(GUI).Assembly) continue;
                if (!_owners.TryGetValue(assembly, out string owner))
                {
                    owner = !assembly.IsDynamic && IsCanonPath(assembly.Location) ? assembly.GetName().Name : "";
                    _owners[assembly] = owner;
                    if (owner.Length > 0) LoadConfigHelp(assembly, owner);
                }
                if (owner.Length == 0) continue;
                if (firstOwner == null) firstOwner = owner;
                if (owner != firstOwner) break;
                string method = frame.GetMethod().Name;
                if (method.StartsWith("<", StringComparison.Ordinal) && method.IndexOf('>') > 1)
                    method = method.Substring(1, method.IndexOf('>') - 1);
                context = _catalog.Find(owner, "@" + method + ":" + kind);
                if (!string.IsNullOrWhiteSpace(context)) return owner;
            }
            return firstOwner;
        }

        private bool CanObserve(Rect rect)
        {
            if (!CanonPlugins.Shared.PluginActivityGate.Allowed) return false;
            if (_drawing || (_hostCapture.Active && !_hostCapture.Accepting)
                || !_enabled.Value || !Application.isFocused || Event.current == null
                || Event.current.type != EventType.Repaint || rect.width <= 0f || rect.height <= 0f) return false;
            Vector2 mouse = Event.current.mousePosition;
            return mouse.y >= rect.yMin && mouse.y < rect.yMax && _visibleRect().Contains(mouse);
        }

        private static Rect ScreenRect(Rect rect)
        {
            Vector2 a = GUIUtility.GUIToScreenPoint(new Vector2(rect.xMin, rect.yMin));
            Vector2 b = GUIUtility.GUIToScreenPoint(new Vector2(rect.xMax, rect.yMax));
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        private void ObserveLabel(Rect rect, GUIContent content, string kind, bool? toggle = null)
        {
            if (!CanObserve(rect) || content == null) return;
            string owner = ResolveOwner(kind, out string context);
            if (owner == null) return;
            string label = HelpCatalog.Normalize(content.text);
            if (label.Length == 0)
            {
                if (string.IsNullOrWhiteSpace(context) && string.IsNullOrWhiteSpace(content.tooltip)) return;
                label = kind == "toggle" ? "チェック" : "選択項目";
            }
            string help = _catalog.Find(owner, label, content.tooltip, context);
            if (string.IsNullOrWhiteSpace(help)) return;
            _rowFrame = Time.frameCount;
            _rowDepth = GUI.depth;
            _rowOwner = owner;
            _rowLabel = label;
            _rowHelp = help;
            _rowRange = null;
            _rowScreenRect = ScreenRect(rect);
            if (rect.Contains(Event.current.mousePosition))
                Hover(owner, label, help + (toggle.HasValue ? "\n現在: " + (toggle.Value ? "ON（有効）" : "OFF（無効）") : ""), _rowScreenRect);
        }

        private void ObserveField(Rect rect, string value, string range)
        {
            if (!CanObserve(rect)) return;
            string owner = ResolveOwner("field", out string context);
            Rect screen = ScreenRect(rect);
            if (owner == null || owner != _rowOwner || _rowFrame != Time.frameCount || _rowDepth != GUI.depth
                || !HelpCatalog.IsSameRow(_rowScreenRect.xMin, _rowScreenRect.xMax, _rowScreenRect.yMin,
                    _rowScreenRect.yMax, screen.xMin, screen.yMin, screen.yMax))
            {
                if (owner != null && !string.IsNullOrWhiteSpace(context) && rect.Contains(Event.current.mousePosition))
                    Hover(owner, "入力欄", context + "\n" + (range ?? "現在: " + value), screen);
                return;
            }
            if (!string.IsNullOrEmpty(range)) _rowRange = range;
            if (!rect.Contains(Event.current.mousePosition)) return;
            string suffix = !string.IsNullOrEmpty(_rowRange) ? "\n" + _rowRange : "";
            if (_hostCapture.Active && suffix.Length == 0 && !string.IsNullOrEmpty(value))
                suffix = "\n現在: " + value;
            Hover(owner, _rowLabel, _rowHelp + suffix, screen);
        }

        private void Hover(string owner, string label, string help, Rect rect)
        {
            if (_hostCapture.Offer(label + "\n\n" + help))
            {
                // A hosted item must not be drawn again by our standalone OnGUI,
                // including the following frame when Unity invokes us before the host.
                _hoverFrame = -10;
                _hoverKey = null;
                return;
            }
            string key = owner + "\n" + label;
            if (key != _hoverKey || _hoverFrame < Time.frameCount - 1)
            {
                _hoverKey = key;
                _hoverSince = Time.realtimeSinceStartup;
            }
            string name = _names.TryGetValue(owner, out string title) ? title : owner;
            _hoverText = name + "\n" + label + "\n\n" + help;
            _hoverScreenRect = rect;
            _hoverFrame = Time.frameCount;
        }

        private static void ReportObservationError(Exception ex)
        {
            Plugin p = _instance;
            if (p == null || p._reportedError) return;
            p._reportedError = true;
            p.Logger.LogError("Help observation failed (original UI continues): " + ex);
        }

        private static void ObserveLabelSafely(Rect rect, GUIContent content, string kind, bool? toggle = null)
        {
            try { _instance.ObserveLabel(rect, content, kind, toggle); }
            catch (Exception ex) { ReportObservationError(ex); }
        }

        private static void ObserveFieldSafely(Rect rect, string value, string range)
        {
            try { _instance.ObserveField(rect, value, range); }
            catch (Exception ex) { ReportObservationError(ex); }
        }

        private static bool Relevant(Rect rect) => _instance != null && _instance.CanObserve(rect);
        private static void LabelPrefix(Rect __0, GUIContent __1)
        {
            if (Relevant(__0)) ObserveLabelSafely(__0, __1, "label");
        }
        private static void ButtonPrefix(Rect __0, GUIContent __2)
        {
            if (Relevant(__0)) ObserveLabelSafely(__0, __2, "button");
        }
        private static void TogglePrefix(Rect __0, bool __2, GUIContent __3)
        {
            if (Relevant(__0)) ObserveLabelSafely(__0, __3, "toggle", __2);
        }
        private static void TextPrefix(Rect __0, GUIContent __2)
        {
            if (Relevant(__0)) ObserveFieldSafely(__0, __2?.text, null);
        }
        private static void SliderPrefix(Rect __0, float __1, float __3, float __4)
        {
            if (Relevant(__0))
            {
                try { _instance.ObserveField(__0, null, HelpCatalog.Range(__1, __3, __4)); }
                catch (Exception ex) { ReportObservationError(ex); }
            }
        }
        private static void GridPrefix(Rect __0, GUIContent[] __2, int __4)
        {
            if (!Relevant(__0)) return;
            try
            {
                if (__2 == null || __2.Length == 0 || __4 <= 0 || !_instance.CanObserve(__0)) return;
                int rows = (__2.Length + __4 - 1) / __4;
                for (int i = 0; i < __2.Length; i++)
                {
                    Rect cell = new Rect(__0.x + i % __4 * __0.width / __4,
                        __0.y + i / __4 * __0.height / rows, __0.width / __4, __0.height / rows);
                    if (cell.Contains(Event.current.mousePosition)) _instance.ObserveLabel(cell, __2[i], "selection");
                }
            }
            catch (Exception ex) { ReportObservationError(ex); }
        }

        private void OnGUI()
        {
            if (!CanonPlugins.Shared.PluginActivityGate.Allowed) return;
            if (_hostCapture.Active || !_enabled.Value || !Application.isFocused || _hoverFrame < Time.frameCount - 1
                || Event.current.type != EventType.Repaint || Time.realtimeSinceStartup - _hoverSince < _delay.Value
                || !_hoverScreenRect.Contains(GUIUtility.GUIToScreenPoint(Event.current.mousePosition))) return;
            Matrix4x4 matrix = GUI.matrix;
            int depth = GUI.depth;
            Color color = GUI.color, background = GUI.backgroundColor, content = GUI.contentColor;
            bool enabled = GUI.enabled;
            _drawing = true;
            try
            {
                GUI.matrix = Matrix4x4.identity;
                GUI.depth = -30000;
                GUI.color = GUI.backgroundColor = GUI.contentColor = Color.white;
                GUI.enabled = true;
                if (_popupStyle == null)
                {
                    _background = new Texture2D(1, 1);
                    _background.SetPixel(0, 0, new Color(0.045f, 0.045f, 0.06f, 0.98f));
                    _background.Apply();
                    _popupStyle = new GUIStyle(GUI.skin.box)
                    {
                        wordWrap = true, richText = false, alignment = TextAnchor.UpperLeft,
                        padding = new RectOffset(14, 14, 12, 12)
                    };
                    _popupStyle.normal.background = _background;
                    _popupStyle.normal.textColor = Color.white;
                }
                _popupStyle.fontSize = _fontSize.Value;
                float width = Mathf.Min(_width.Value, Screen.width - 20f);
                var text = new GUIContent(_hoverText);
                float height = Mathf.Min(_popupStyle.CalcHeight(text, width), Screen.height - 20f);
                Vector2 mouse = Event.current.mousePosition;
                float x = Mathf.Clamp(mouse.x + 18f, 10f, Mathf.Max(10f, Screen.width - width - 10f));
                float y = mouse.y + 22f;
                if (y + height > Screen.height - 10f) y = mouse.y - height - 12f;
                y = Mathf.Clamp(y, 10f, Mathf.Max(10f, Screen.height - height - 10f));
                GUI.Label(new Rect(x, y, width, height), text, _popupStyle);
            }
            finally
            {
                GUI.matrix = matrix;
                GUI.depth = depth;
                GUI.color = color; GUI.backgroundColor = background; GUI.contentColor = content;
                GUI.enabled = enabled;
                _drawing = false;
            }
        }

        private void OnDestroy()
        {
            CanonPlugins.Shared.PluginActivityGate.Dispose();
            _hostCapture.End();
            _harmony?.UnpatchSelf();
            if (_background != null) Destroy(_background);
            if (_instance == this) _instance = null;
        }
        private void OnDisable() => CanonPlugins.Shared.PluginActivityGate.End();
    }
}
