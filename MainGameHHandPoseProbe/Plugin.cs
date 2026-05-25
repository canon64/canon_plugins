using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace MainGameHHandPoseProbe
{
    [BepInPlugin(Guid, PluginName, Version)]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private const string Guid = "canon.maingame.hscene.handposeprobe";
        private const string PluginName = "MainGameHHandPoseProbe";
        private const string Version = "0.1.0";
        private const int LeftHand = 0;
        private const int RightHand = 1;
        private const int WindowId = 0x48414E44;

        private static readonly FieldInfo FiLstFemale = typeof(HSceneProc).GetField("lstFemale", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo FiLstMale = typeof(HSceneProc).GetField("lstMale", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private ConfigEntry<bool> _cfgUiVisible;
        private Rect _windowRect = new Rect(40f, 40f, 520f, 500f);
        private Vector2 _scroll;
        private string _status = "未実行";
        private int _targetSex;
        private int _targetIndex;
        private int _leftPattern;
        private int _rightPattern;
        private int _lastTargetId;
        private string _leftPatternText = "0";
        private string _rightPatternText = "0";

        private void Awake()
        {
            _cfgUiVisible = Config.Bind("UI", "UiVisible", false, "手ポーズ検証UIを表示する");
            _cfgUiVisible.SettingChanged += (_, __) => { };

            Config.Bind(
                "UI",
                "OpenWindow",
                false,
                new ConfigDescription(
                    "ConfigurationManager上のUI開閉ボタン",
                    null,
                    new ConfigurationManager.ConfigurationManagerAttributes
                    {
                        Order = 100,
                        HideDefaultButton = true,
                        CustomDrawer = DrawOpenWindowButton
                    }));

            Logger.LogInfo("awake");
        }

        private void OnGUI()
        {
            if (!_cfgUiVisible.Value)
                return;

            _windowRect = GUI.Window(WindowId, _windowRect, DrawWindow, PluginName);
        }

        private void DrawOpenWindowButton(ConfigEntryBase _)
        {
            string label = _cfgUiVisible.Value ? "UIを閉じる" : "UIを開く";
            if (GUILayout.Button(label))
            {
                _cfgUiVisible.Value = !_cfgUiVisible.Value;
            }
        }

        private void DrawWindow(int id)
        {
            HSceneProc proc = FindObjectOfType<HSceneProc>();
            List<ChaControl> females = ResolveList(proc, FiLstFemale, 1);
            List<ChaControl> males = ResolveList(proc, FiLstMale, 0);
            List<ChaControl> targets = _targetSex == 0 ? females : males;

            GUILayout.BeginVertical();
            GUILayout.Label("Hシーン: " + (proc != null ? "検出中" : "未検出"));
            GUILayout.Label("状態: " + _status);
            GUILayout.Label("女: " + females.Count + " / 男: " + males.Count);

            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_targetSex == 0, "女", GUI.skin.button, GUILayout.Width(60f)))
                _targetSex = 0;
            if (GUILayout.Toggle(_targetSex == 1, "男", GUI.skin.button, GUILayout.Width(60f)))
                _targetSex = 1;
            GUILayout.EndHorizontal();

            if (_targetIndex >= targets.Count)
                _targetIndex = Mathf.Max(0, targets.Count - 1);

            GUILayout.BeginHorizontal();
            GUI.enabled = targets.Count > 0;
            if (GUILayout.Button("前", GUILayout.Width(50f)))
                _targetIndex = WrapIndex(_targetIndex - 1, targets.Count);
            if (GUILayout.Button("次", GUILayout.Width(50f)))
                _targetIndex = WrapIndex(_targetIndex + 1, targets.Count);
            GUI.enabled = true;
            string targetLabel = targets.Count > 0 ? DescribeCha(targets[_targetIndex], _targetIndex) : "<対象なし>";
            GUILayout.Label("対象: " + targetLabel);
            GUILayout.EndHorizontal();

            ChaControl target = targets.Count > 0 ? targets[_targetIndex] : null;
            SyncPatternFieldsFromTargetIfNeeded(target);

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(360f));
            DrawHandSection(target, LeftHand, "左手", ref _leftPattern);
            GUILayout.Space(12f);
            DrawHandSection(target, RightHand, "右手", ref _rightPattern);
            GUILayout.EndScrollView();

            if (GUILayout.Button("閉じる"))
                _cfgUiVisible.Value = false;

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 9999f, 22f));
        }

        private void DrawHandSection(ChaControl target, int hand, string label, ref int pattern)
        {
            GUILayout.Label(label);

            int maxCount = target != null ? Mathf.Max(0, target.GetShapeIndexCount()) : 0;
            bool enabled = target != null;
            int currentPattern = target != null ? target.GetShapeHandIndex(hand, 0) : 0;
            bool currentEnabled = target != null && target.GetEnableShapeHand(hand);

            GUILayout.Label("現在: index=" + currentPattern + " enabled=" + currentEnabled + " count=" + maxCount);

            GUILayout.BeginHorizontal();
            GUI.enabled = enabled && maxCount > 0;
            if (GUILayout.Button("-10", GUILayout.Width(60f)))
                pattern = ClampPattern(pattern - 10, maxCount);
            if (GUILayout.Button("-1", GUILayout.Width(60f)))
                pattern = ClampPattern(pattern - 1, maxCount);
            if (GUILayout.Button("+1", GUILayout.Width(60f)))
                pattern = ClampPattern(pattern + 1, maxCount);
            if (GUILayout.Button("+10", GUILayout.Width(60f)))
                pattern = ClampPattern(pattern + 10, maxCount);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("指定 index", GUILayout.Width(80f));
            string currentText = hand == LeftHand ? _leftPatternText : _rightPatternText;
            string nextText = GUILayout.TextField(currentText, GUILayout.Width(80f));
            if (hand == LeftHand)
                _leftPatternText = nextText;
            else
                _rightPatternText = nextText;
            if (int.TryParse(nextText, out int parsed))
                pattern = ClampPattern(parsed, maxCount);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = enabled;
            if (GUILayout.Button("適用", GUILayout.Width(100f)))
                ApplyHand(target, hand, pattern);
            if (GUILayout.Button("0に戻す", GUILayout.Width(100f)))
                ApplyHand(target, hand, 0);
            if (GUILayout.Button("無効化", GUILayout.Width(100f)))
                DisableHand(target, hand);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void ApplyHand(ChaControl target, int hand, int pattern)
        {
            if (target == null)
            {
                _status = "対象がありません";
                return;
            }

            int maxCount = Mathf.Max(0, target.GetShapeIndexCount());
            pattern = ClampPattern(pattern, maxCount);

            try
            {
                target.SetShapeHandValue(hand, pattern, 0, 0f);
                target.SetEnableShapeHand(hand, pattern != 0);
                if (hand == LeftHand)
                {
                    _leftPattern = pattern;
                    _leftPatternText = pattern.ToString();
                }
                else
                {
                    _rightPattern = pattern;
                    _rightPatternText = pattern.ToString();
                }
                _status = DescribeCha(target, _targetIndex) + " " + (hand == LeftHand ? "左" : "右") + "手 index=" + pattern + " 適用";
                Logger.LogInfo(_status);
            }
            catch (Exception ex)
            {
                _status = "適用失敗: " + ex.Message;
                Logger.LogWarning(_status);
            }
        }

        private void DisableHand(ChaControl target, int hand)
        {
            if (target == null)
            {
                _status = "対象がありません";
                return;
            }

            try
            {
                target.SetEnableShapeHand(hand, false);
                _status = DescribeCha(target, _targetIndex) + " " + (hand == LeftHand ? "左" : "右") + "手 無効化";
                Logger.LogInfo(_status);
            }
            catch (Exception ex)
            {
                _status = "無効化失敗: " + ex.Message;
                Logger.LogWarning(_status);
            }
        }

        private void SyncPatternFieldsFromTargetIfNeeded(ChaControl target)
        {
            if (target == null)
            {
                _lastTargetId = 0;
                return;
            }

            int targetId = target.GetInstanceID();
            if (_lastTargetId == targetId)
                return;

            _lastTargetId = targetId;
            _leftPattern = target.GetShapeHandIndex(LeftHand, 0);
            _rightPattern = target.GetShapeHandIndex(RightHand, 0);
            _leftPatternText = _leftPattern.ToString();
            _rightPatternText = _rightPattern.ToString();
        }

        private static List<ChaControl> ResolveList(HSceneProc proc, FieldInfo field, int sex)
        {
            var result = new List<ChaControl>();
            var seen = new HashSet<int>();

            if (proc != null && field != null)
            {
                IList list = field.GetValue(proc) as IList;
                if (list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        ChaControl cha = list[i] as ChaControl;
                        if (cha == null || cha.sex != sex)
                            continue;
                        if (seen.Add(cha.GetInstanceID()))
                            result.Add(cha);
                    }
                }
            }

            if (result.Count > 0)
                return result;

            ChaControl[] all = FindObjectsOfType<ChaControl>();
            for (int i = 0; i < all.Length; i++)
            {
                ChaControl cha = all[i];
                if (cha == null || cha.sex != sex)
                    continue;
                if (seen.Add(cha.GetInstanceID()))
                    result.Add(cha);
            }
            return result;
        }

        private static int WrapIndex(int index, int count)
        {
            if (count <= 0)
                return 0;
            if (index < 0)
                return count - 1;
            if (index >= count)
                return 0;
            return index;
        }

        private static int ClampPattern(int value, int maxCount)
        {
            if (maxCount <= 0)
                return 0;
            return Mathf.Clamp(value, 0, maxCount - 1);
        }

        private static string DescribeCha(ChaControl cha, int index)
        {
            if (cha == null)
                return "<null>";
            string fullName = string.Empty;
            try
            {
                fullName = cha.fileParam != null
                    ? cha.fileParam.fullname
                    : string.Empty;
            }
            catch
            {
                fullName = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(fullName))
                fullName = cha.name;

            return "#" + index + " " + fullName;
        }
    }
}
