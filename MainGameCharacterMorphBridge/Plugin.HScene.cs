using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MainGameCharacterMorphBridge
{
    public sealed partial class Plugin
    {
        private sealed class MorphSnapshot
        {
            internal float[] Body;
            internal float[] Face;

            internal bool HasBody => Body != null && Body.Length > 0;
            internal bool HasFace => Face != null && Face.Length > 0;
        }

        private static readonly FieldInfo FieldLstFemale = AccessTools.Field(typeof(HSceneProc), "lstFemale");
        private static readonly FieldInfo FieldLstMotionIk = AccessTools.Field(typeof(HSceneProc), "lstMotionIK");
        private static readonly FieldInfo FieldMale = AccessTools.Field(typeof(HSceneProc), "male");
        private static readonly FieldInfo FieldMale1 = AccessTools.Field(typeof(HSceneProc), "male1");
        private static readonly FieldInfo FieldItem = AccessTools.Field(typeof(HSceneProc), "item");

        private Coroutine _autoHSceneInitCoroutine;
        private Coroutine _blendTransitionCoroutine;
        private Coroutine _postMotionReapplyCoroutine;
        private bool _hasObservedAnimationState;
        private string _lastObservedAnimStateName = string.Empty;
        private int _lastObservedAnimationId = int.MinValue;
        private bool _hasDirectBodyOverride;
        private int _directBodyShapeIndex;
        private float _directBodyShapeValue;
        private bool _hasHeightOverride;
        private float _directHeightValue;
        private bool _hasBreastOverride;
        private float _directBreastValue;
        private bool _hasDirectFaceOverride;
        private int _directFaceShapeIndex;
        private float _directFaceShapeValue;

        private void Update()
        {
            TickAnimationChangeReapply();
        }

        internal bool CaptureOriginalFromConfig()
        {
            return CaptureOriginal(TargetFemaleIndex(), "config button");
        }

        internal bool LoadTargetCardFromConfig()
        {
            string path = _cfgTargetCardPath != null ? _cfgTargetCardPath.Value : _settings.TargetCardPath;
            if (string.IsNullOrWhiteSpace(path) && !string.IsNullOrWhiteSpace(_settings.SelectedCardWord))
                return LoadTargetCardByWord(_settings.SelectedCardWord, TargetFemaleIndex(), "config button");

            return LoadTargetCard(path, TargetFemaleIndex(), "config button");
        }

        internal bool ResetToOriginalFromConfig()
        {
            return ResetToOriginal(TargetFemaleIndex(), "config button");
        }

        internal bool CaptureOriginal(int femaleIndex, string source, bool writeLog = true)
        {
            femaleIndex = ResolveFemaleIndex(femaleIndex);
            if (!TryResolveTarget(femaleIndex, out ChaControl target, out string error))
            {
                if (writeLog)
                    LogWarnAlways("capture original failed: " + error);
                return false;
            }

            _originalSnapshot = SnapshotFrom(target);
            _originalFemaleIndex = femaleIndex;
            if (_originalSnapshot == null)
            {
                if (writeLog)
                    LogWarnAlways("capture original failed: empty snapshot");
                return false;
            }

            SyncHeightBreastFromTarget(target);
            if (writeLog)
                LogExecution("capture original: female=" + femaleIndex + " source=" + source);
            return true;
        }

        internal bool LoadTargetCard(string cardPath, int femaleIndex, string source, bool writeLog = true)
        {
            femaleIndex = ResolveFemaleIndex(femaleIndex);
            if (!IsEnabled())
            {
                if (writeLog)
                    LogExecution("load target card skipped: disabled");
                return false;
            }

            string normalizedPath = PluginSettings.NormalizePath(cardPath);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                if (writeLog)
                    LogWarnAlways("load target card failed: empty path");
                return false;
            }

            if (!File.Exists(normalizedPath))
            {
                if (writeLog)
                    LogWarnAlways("load target card failed: file not found: " + normalizedPath);
                return false;
            }

            if (!TryResolveTarget(femaleIndex, out ChaControl target, out string error))
            {
                if (writeLog)
                    LogWarnAlways("load target card failed: " + error);
                return false;
            }

            if (_settings.AutoCaptureOriginal && (_originalSnapshot == null || _originalFemaleIndex != femaleIndex))
                CaptureOriginal(femaleIndex, "auto before card load", writeLog);

            try
            {
                var card = new ChaFileControl();
                if (!card.LoadCharaFile(normalizedPath, target.sex, noLoadPng: true, noLoadStatus: true))
                {
                    if (writeLog)
                        LogWarnAlways("load target card returned false: " + normalizedPath);
                    return false;
                }

                _targetSnapshot = SnapshotFrom(card);
                _targetFemaleIndex = femaleIndex;
                _targetCardPath = normalizedPath;
                _settings.TargetCardPath = normalizedPath;
                if (_cfgTargetCardPath != null && !string.Equals(_cfgTargetCardPath.Value, normalizedPath, StringComparison.Ordinal))
                    _cfgTargetCardPath.Value = normalizedPath;

                if (writeLog)
                    LogExecution("load target card values: female=" + femaleIndex + " path=" + normalizedPath + " source=" + source);
                return true;
            }
            catch (Exception ex)
            {
                if (writeLog)
                    LogErrorAlways("load target card failed: " + ex.Message);
                return false;
            }
        }

        internal bool ApplyBlend(float blend, int femaleIndex, string source, bool writeLog = true)
        {
            femaleIndex = ResolveFemaleIndex(femaleIndex);
            if (!IsEnabled())
            {
                if (writeLog)
                    LogExecution("blend skipped: disabled");
                return false;
            }

            blend = PluginSettings.Round2(PluginSettings.Clamp01(blend));
            _settings.Blend = blend;

            if (!TryResolveTarget(femaleIndex, out ChaControl target, out string error))
            {
                if (writeLog)
                    LogWarnAlways("blend failed: " + error);
                return false;
            }

            if (_originalSnapshot == null || _originalFemaleIndex != femaleIndex)
            {
                if (!CaptureOriginal(femaleIndex, "auto before blend", writeLog))
                    return false;
            }

            if (_targetSnapshot == null || _targetFemaleIndex != femaleIndex)
            {
                if (!LoadActiveTargetCard(femaleIndex, "auto before blend", writeLog))
                    return false;
            }

            if (_originalSnapshot == null || _targetSnapshot == null)
                return false;

            bool bodyApplied = ApplyBodyBlend(target, _originalSnapshot.Body, _targetSnapshot.Body, blend);
            bool faceApplied = ApplyFaceBlend(target, _originalSnapshot.Face, _targetSnapshot.Face, blend);

            if (bodyApplied)
            {
                target.UpdateShapeBodyValueFromCustomInfo();
                RefreshHAfterBodyChange(femaleIndex, "blend", writeLog);
                SyncHeightBreastFromTarget(target);
            }

            if (faceApplied)
                target.UpdateShapeFaceValueFromCustomInfo();

            if (writeLog)
                LogExecution("blend applied: female=" + femaleIndex + " value=" + blend.ToString("0.00") + " source=" + source);
            return bodyApplied || faceApplied;
        }

        internal bool SetBodyShape(int index, float value, int femaleIndex, string source, bool writeLog = true, bool trackDirectOverride = true)
        {
            femaleIndex = ResolveFemaleIndex(femaleIndex);
            if (!IsEnabled())
            {
                if (writeLog)
                    LogExecution("body shape skipped: disabled");
                return false;
            }

            int safeIndex = PluginSettings.ClampInt(index, 0, 43);
            float safeValue = PluginSettings.Round2(PluginSettings.Clamp01(value));
            _settings.BodyShapeIndex = safeIndex;
            _settings.BodyShapeValue = safeValue;
            if (safeIndex == 0)
                _settings.Height = safeValue;
            if (safeIndex == 4)
                _settings.Breast = safeValue;
            if (trackDirectOverride)
                TrackDirectBodyOverride(safeIndex, safeValue);

            if (!TryResolveTarget(femaleIndex, out ChaControl target, out string error))
            {
                if (writeLog)
                    LogWarnAlways("body shape failed: " + error);
                return false;
            }

            bool changed = target.SetShapeBodyValue(safeIndex, safeValue);
            target.UpdateShapeBodyValueFromCustomInfo();
            RefreshHAfterBodyChange(femaleIndex, "body index " + safeIndex, writeLog);

            if (writeLog)
                LogExecution("body shape applied: female=" + femaleIndex + " index=" + safeIndex + " value=" + safeValue.ToString("0.00") + " source=" + source);
            return changed;
        }

        internal bool SetFaceShape(int index, float value, int femaleIndex, string source, bool writeLog = true, bool trackDirectOverride = true)
        {
            femaleIndex = ResolveFemaleIndex(femaleIndex);
            if (!IsEnabled())
            {
                if (writeLog)
                    LogExecution("face shape skipped: disabled");
                return false;
            }

            int safeIndex = PluginSettings.ClampInt(index, 0, 51);
            float safeValue = PluginSettings.Round2(PluginSettings.Clamp01(value));
            _settings.FaceShapeIndex = safeIndex;
            _settings.FaceShapeValue = safeValue;
            if (trackDirectOverride)
                TrackDirectFaceOverride(safeIndex, safeValue);

            if (!TryResolveTarget(femaleIndex, out ChaControl target, out string error))
            {
                if (writeLog)
                    LogWarnAlways("face shape failed: " + error);
                return false;
            }

            bool changed = target.SetShapeFaceValue(safeIndex, safeValue);
            target.UpdateShapeFaceValueFromCustomInfo();

            if (writeLog)
                LogExecution("face shape applied: female=" + femaleIndex + " index=" + safeIndex + " value=" + safeValue.ToString("0.00") + " source=" + source);
            return changed;
        }

        internal bool ResetToOriginal(int femaleIndex, string source)
        {
            femaleIndex = ResolveFemaleIndex(femaleIndex);
            if (!IsEnabled())
            {
                LogExecution("reset skipped: disabled");
                return false;
            }

            if (_originalSnapshot == null || _originalFemaleIndex != femaleIndex)
            {
                LogWarnAlways("reset failed: original snapshot not captured");
                return false;
            }

            if (!TryResolveTarget(femaleIndex, out ChaControl target, out string error))
            {
                LogWarnAlways("reset failed: " + error);
                return false;
            }

            bool bodyApplied = ApplySnapshotBody(target, _originalSnapshot.Body);
            bool faceApplied = ApplySnapshotFace(target, _originalSnapshot.Face);

            if (bodyApplied)
            {
                target.UpdateShapeBodyValueFromCustomInfo();
                RefreshHAfterBodyChange(femaleIndex, "reset original");
                SyncHeightBreastFromTarget(target);
            }

            if (faceApplied)
                target.UpdateShapeFaceValueFromCustomInfo();

            _settings.Blend = 0f;
            SetEntryValue(_cfgBlend, 0f);
            ClearDirectOverrideState();
            LogExecution("reset original applied: female=" + femaleIndex + " source=" + source);
            return bodyApplied || faceApplied;
        }

        internal bool UpsertSelectedCardFromConfig()
        {
            string word = _cfgSelectedCardWord != null ? _cfgSelectedCardWord.Value : _settings.SelectedCardWord;
            string path = _cfgTargetCardPath != null ? _cfgTargetCardPath.Value : _settings.TargetCardPath;
            string triggerWords = _cfgSelectedCardTriggerWords != null ? _cfgSelectedCardTriggerWords.Value : _settings.SelectedCardTriggerWords;
            return RegisterCard(word, path, triggerWords, "config button");
        }

        internal bool RegisterCard(string word, string cardPath, string triggerWords, string source)
        {
            word = (word ?? string.Empty).Trim();
            string path = PluginSettings.NormalizePath(cardPath);
            triggerWords = PluginSettings.NormalizeCsv(triggerWords);

            if (string.IsNullOrWhiteSpace(word))
            {
                LogWarnAlways("register card failed: selected card word is empty");
                return false;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                LogWarnAlways("register card failed: card path is empty word=" + word);
                return false;
            }

            var cards = new List<MorphCardRegistration>(_settings.RegisteredCards ?? new MorphCardRegistration[0]);
            MorphCardRegistration target = cards.FirstOrDefault(x => x != null && string.Equals(x.Word, word, StringComparison.Ordinal));
            if (target == null)
            {
                target = new MorphCardRegistration();
                cards.Add(target);
            }

            target.Enabled = true;
            target.Word = word;
            target.TriggerWords = triggerWords;
            target.CardPath = path;
            target.Normalize();

            _settings.SelectedCardWord = word;
            _settings.SelectedCardTriggerWords = triggerWords;
            _settings.TargetCardPath = path;
            _settings.RegisteredCards = cards.Where(x => x != null).Select(x => x.Normalize()).ToArray();
            SaveSettings("registered card: " + word);
            LogExecution("registered card: word=" + word + " path=" + path + " source=" + source);
            return true;
        }

        internal string[] GetRegisteredCardWords()
        {
            return EnabledRegisteredCards()
                .Select(x => x.Word)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
        }

        internal void SchedulePostMotionReapply(string reason)
        {
            if (!IsEnabled() || _currentHScene == null)
                return;

            StopPostMotionReapply();
            _postMotionReapplyCoroutine = StartCoroutine(PostMotionReapplyRoutine(reason));
        }

        internal void StopPostMotionReapply()
        {
            if (_postMotionReapplyCoroutine == null)
                return;

            StopCoroutine(_postMotionReapplyCoroutine);
            _postMotionReapplyCoroutine = null;
        }

        internal void ResetAnimationObservation()
        {
            _hasObservedAnimationState = false;
            _lastObservedAnimStateName = string.Empty;
            _lastObservedAnimationId = int.MinValue;
        }

        internal void ClearDirectOverrideState()
        {
            _hasDirectBodyOverride = false;
            _directBodyShapeIndex = 0;
            _directBodyShapeValue = 0f;
            _hasHeightOverride = false;
            _directHeightValue = 0f;
            _hasBreastOverride = false;
            _directBreastValue = 0f;
            _hasDirectFaceOverride = false;
            _directFaceShapeIndex = 0;
            _directFaceShapeValue = 0f;
        }

        internal bool LoadTargetCardByWord(string word, int femaleIndex, string source, bool writeLog = true)
        {
            femaleIndex = ResolveFemaleIndex(femaleIndex);
            MorphCardRegistration card = FindRegisteredCardByWord(word);
            if (card == null)
            {
                if (writeLog)
                    LogWarnAlways("load target card by word failed: card not registered word=" + (word ?? string.Empty));
                return false;
            }

            SelectRegisteredCard(card);
            return LoadTargetCard(card.CardPath, femaleIndex, source + " word=" + card.Word, writeLog);
        }

        internal bool BlendToCardWord(string word, float targetBlend, float seconds, int femaleIndex, string source)
        {
            femaleIndex = ResolveFemaleIndex(femaleIndex);
            if (!LoadTargetCardByWord(word, femaleIndex, source, true))
                return false;

            return BlendTo(targetBlend, seconds, femaleIndex, source + " word=" + word);
        }

        internal bool BlendToCardPath(string cardPath, float targetBlend, float seconds, int femaleIndex, string source)
        {
            femaleIndex = ResolveFemaleIndex(femaleIndex);
            if (!LoadTargetCard(cardPath, femaleIndex, source, true))
                return false;

            return BlendTo(targetBlend, seconds, femaleIndex, source + " path=" + PluginSettings.NormalizePath(cardPath));
        }

        internal bool BlendTo(float targetBlend, float seconds, int femaleIndex, string source)
        {
            femaleIndex = ResolveFemaleIndex(femaleIndex);
            if (!IsEnabled())
            {
                LogExecution("blend transition skipped: disabled");
                return false;
            }

            if (!EnsureMorphSnapshots(femaleIndex, source, true))
                return false;

            StopBlendTransition();
            targetBlend = PluginSettings.Round2(PluginSettings.Clamp01(targetBlend));
            seconds = Mathf.Max(0f, seconds);
            if (seconds <= 0f)
            {
                SetEntryValue(_cfgBlend, targetBlend);
                return ApplyBlend(targetBlend, femaleIndex, source, true);
            }

            float startBlend = PluginSettings.Round2(PluginSettings.Clamp01(_settings.Blend));
            _blendTransitionCoroutine = StartCoroutine(BlendTransitionRoutine(startBlend, targetBlend, seconds, femaleIndex, source));
            LogExecution("blend transition started: female=" + femaleIndex + " from=" + startBlend.ToString("0.00") + " to=" + targetBlend.ToString("0.00") + " seconds=" + seconds.ToString("0.00") + " source=" + source);
            return true;
        }

        internal bool TryResolveRegisteredCardFromText(string text, out string word, out string cardPath)
        {
            word = string.Empty;
            cardPath = string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            MorphCardRegistration bestCard = null;
            string bestKeyword = string.Empty;
            int bestIndex = -1;
            foreach (MorphCardRegistration card in EnabledRegisteredCards())
            {
                foreach (string keyword in GetCardKeywords(card))
                {
                    int index = text.IndexOf(keyword, StringComparison.Ordinal);
                    if (index < 0)
                        continue;

                    if (bestCard == null
                        || index < bestIndex
                        || (index == bestIndex && keyword.Length > bestKeyword.Length))
                    {
                        bestCard = card;
                        bestKeyword = keyword;
                        bestIndex = index;
                    }
                }
            }

            if (bestCard == null)
                return false;

            word = bestCard.Word;
            cardPath = bestCard.CardPath;
            return true;
        }

        private bool LoadActiveTargetCard(int femaleIndex, string source, bool writeLog)
        {
            MorphCardRegistration selected = FindSelectedRegisteredCard();
            if (selected != null)
            {
                SelectRegisteredCard(selected);
                return LoadTargetCard(selected.CardPath, femaleIndex, source + " word=" + selected.Word, writeLog);
            }

            return LoadTargetCard(_settings.TargetCardPath, femaleIndex, source, writeLog);
        }

        private bool EnsureMorphSnapshots(int femaleIndex, string source, bool writeLog)
        {
            if (_originalSnapshot == null || _originalFemaleIndex != femaleIndex)
            {
                if (!CaptureOriginal(femaleIndex, "auto before " + source, writeLog))
                    return false;
            }

            if (_targetSnapshot == null || _targetFemaleIndex != femaleIndex)
            {
                if (!LoadActiveTargetCard(femaleIndex, "auto before " + source, writeLog))
                    return false;
            }

            return _originalSnapshot != null && _targetSnapshot != null;
        }

        private IEnumerator BlendTransitionRoutine(float startBlend, float targetBlend, float seconds, int femaleIndex, string source)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                float t = seconds > 0f ? Mathf.Clamp01(elapsed / seconds) : 1f;
                float value = Mathf.Lerp(startBlend, targetBlend, t);
                ApplyBlend(value, femaleIndex, source + " transition", false);
                yield return null;
            }

            _blendTransitionCoroutine = null;
            SetEntryValue(_cfgBlend, targetBlend);
            ApplyBlend(targetBlend, femaleIndex, source + " transition end", true);
        }

        private void StartHSceneAutoInitialize(HSceneProc proc)
        {
            StopHSceneAutoInitialize();
            if (_settings == null || !_settings.AutoLoadOnHSceneStart || proc == null)
                return;

            _autoHSceneInitCoroutine = StartCoroutine(AutoInitializeHSceneRoutine(proc));
        }

        private void StopHSceneAutoInitialize()
        {
            if (_autoHSceneInitCoroutine == null)
                return;

            StopCoroutine(_autoHSceneInitCoroutine);
            _autoHSceneInitCoroutine = null;
        }

        private void StopBlendTransition()
        {
            if (_blendTransitionCoroutine == null)
                return;

            StopCoroutine(_blendTransitionCoroutine);
            _blendTransitionCoroutine = null;
        }

        private IEnumerator AutoInitializeHSceneRoutine(HSceneProc proc)
        {
            const int maxFrames = 600;
            int frame = 0;
            int femaleIndex = TargetFemaleIndex();
            while (frame < maxFrames && proc != null && ReferenceEquals(_currentHScene, proc))
            {
                List<ChaControl> females = GetFemales(proc);
                if (females != null && females.Count > 0)
                {
                    int index = PluginSettings.ClampInt(femaleIndex, 0, females.Count - 1);
                    if (females[index] != null)
                    {
                        femaleIndex = index;
                        break;
                    }
                }

                frame++;
                yield return null;
            }

            _autoHSceneInitCoroutine = null;
            if (proc == null || !ReferenceEquals(_currentHScene, proc))
                yield break;

            if (_settings.AutoCaptureOriginal)
                CaptureOriginal(femaleIndex, "hscene auto start", true);

            bool loaded = LoadActiveTargetCard(femaleIndex, "hscene auto start", true);
            if (_settings.AutoResetBlendOnHSceneStart)
            {
                _settings.Blend = 0f;
                SetEntryValue(_cfgBlend, 0f);
                if (loaded)
                    ApplyBlend(0f, femaleIndex, "hscene auto reset", false);
            }

            LogExecution("hscene auto initialize completed: female=" + femaleIndex + " targetLoaded=" + loaded);
        }

        private int ResolveFemaleIndex(int femaleIndex)
        {
            return femaleIndex < 0 ? TargetFemaleIndex() : PluginSettings.ClampInt(femaleIndex, 0, 1);
        }

        private MorphCardRegistration FindSelectedRegisteredCard()
        {
            MorphCardRegistration selected = FindRegisteredCardByWord(_settings.SelectedCardWord);
            if (selected != null)
                return selected;

            string path = PluginSettings.NormalizePath(_settings.TargetCardPath);
            if (!string.IsNullOrWhiteSpace(path))
            {
                selected = EnabledRegisteredCards()
                    .FirstOrDefault(x => string.Equals(PluginSettings.NormalizePath(x.CardPath), path, StringComparison.OrdinalIgnoreCase));
                if (selected != null)
                    return selected;
            }

            MorphCardRegistration[] cards = EnabledRegisteredCards().ToArray();
            return cards.Length == 1 ? cards[0] : null;
        }

        private MorphCardRegistration FindRegisteredCardByWord(string word)
        {
            string key = (word ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key))
                return null;

            foreach (MorphCardRegistration card in EnabledRegisteredCards())
            {
                foreach (string keyword in GetCardKeywords(card))
                {
                    if (string.Equals(keyword, key, StringComparison.Ordinal))
                        return card;
                }
            }

            return null;
        }

        private IEnumerable<MorphCardRegistration> EnabledRegisteredCards()
        {
            MorphCardRegistration[] cards = _settings != null ? _settings.RegisteredCards : null;
            if (cards == null)
                yield break;

            foreach (MorphCardRegistration card in cards)
            {
                if (card == null || !card.Enabled)
                    continue;

                card.Normalize();
                if (string.IsNullOrWhiteSpace(card.CardPath))
                    continue;

                yield return card;
            }
        }

        private static IEnumerable<string> GetCardKeywords(MorphCardRegistration card)
        {
            if (card == null)
                yield break;

            if (!string.IsNullOrWhiteSpace(card.Word))
                yield return card.Word.Trim();

            string csv = PluginSettings.NormalizeCsv(card.TriggerWords);
            if (string.IsNullOrWhiteSpace(csv))
                yield break;

            string[] parts = csv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                string keyword = (part ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(keyword))
                    yield return keyword;
            }
        }

        private void SelectRegisteredCard(MorphCardRegistration card)
        {
            if (card == null)
                return;

            card.Normalize();
            _settings.SelectedCardWord = card.Word;
            _settings.SelectedCardTriggerWords = card.TriggerWords;
            _settings.TargetCardPath = card.CardPath;
            if (_cfgSelectedCardWord != null && !string.Equals(_cfgSelectedCardWord.Value, card.Word, StringComparison.Ordinal))
                _cfgSelectedCardWord.Value = card.Word;
            if (_cfgSelectedCardTriggerWords != null && !string.Equals(_cfgSelectedCardTriggerWords.Value, card.TriggerWords, StringComparison.Ordinal))
                _cfgSelectedCardTriggerWords.Value = card.TriggerWords;
            if (_cfgTargetCardPath != null && !string.Equals(_cfgTargetCardPath.Value, card.CardPath, StringComparison.Ordinal))
                _cfgTargetCardPath.Value = card.CardPath;
        }

        private void TickAnimationChangeReapply()
        {
            if (!IsEnabled() || _currentHScene == null || _currentHScene.flags == null)
                return;

            string stateName = _currentHScene.flags.nowAnimStateName ?? string.Empty;
            int animationId = _currentHScene.flags.nowAnimationInfo != null ? _currentHScene.flags.nowAnimationInfo.id : -1;
            if (!_hasObservedAnimationState)
            {
                _hasObservedAnimationState = true;
                _lastObservedAnimStateName = stateName;
                _lastObservedAnimationId = animationId;
                return;
            }

            if (string.Equals(_lastObservedAnimStateName, stateName, StringComparison.Ordinal)
                && _lastObservedAnimationId == animationId)
            {
                return;
            }

            _lastObservedAnimStateName = stateName;
            _lastObservedAnimationId = animationId;
            SchedulePostMotionReapply("animation changed id=" + animationId + " state=" + stateName);
        }

        private IEnumerator PostMotionReapplyRoutine(string reason)
        {
            string safeReason = string.IsNullOrWhiteSpace(reason) ? "motion changed" : reason;
            int femaleIndex = TargetFemaleIndex();
            bool anyApplied = false;

            for (int i = 0; i < 6; i++)
            {
                yield return null;
                anyApplied |= ReapplyCurrentMorphState(femaleIndex, safeReason + " pass=" + (i + 1), false);
            }

            _postMotionReapplyCoroutine = null;
            if (anyApplied)
                LogExecution("post motion reapply completed: female=" + femaleIndex + " reason=" + safeReason);
        }

        private bool ReapplyCurrentMorphState(int femaleIndex, string reason, bool writeLog)
        {
            bool applied = false;
            bool hasBlendState =
                _originalSnapshot != null
                && _targetSnapshot != null
                && _originalFemaleIndex == ResolveFemaleIndex(femaleIndex)
                && _targetFemaleIndex == ResolveFemaleIndex(femaleIndex)
                && _settings != null
                && Math.Abs(_settings.Blend) > 0.0001f;

            if (hasBlendState)
                applied |= ApplyBlend(_settings.Blend, femaleIndex, reason + " blend", writeLog);

            if (_hasHeightOverride)
                applied |= SetBodyShape(0, _directHeightValue, femaleIndex, reason + " height", writeLog, false);

            if (_hasBreastOverride)
                applied |= SetBodyShape(4, _directBreastValue, femaleIndex, reason + " breast", writeLog, false);

            if (_hasDirectBodyOverride && _directBodyShapeIndex != 0 && _directBodyShapeIndex != 4)
                applied |= SetBodyShape(_directBodyShapeIndex, _directBodyShapeValue, femaleIndex, reason + " body", writeLog, false);

            if (_hasDirectFaceOverride)
                applied |= SetFaceShape(_directFaceShapeIndex, _directFaceShapeValue, femaleIndex, reason + " face", writeLog, false);

            return applied;
        }

        private void TrackDirectBodyOverride(int index, float value)
        {
            _hasDirectBodyOverride = true;
            _directBodyShapeIndex = PluginSettings.ClampInt(index, 0, 43);
            _directBodyShapeValue = PluginSettings.Round2(PluginSettings.Clamp01(value));

            if (_directBodyShapeIndex == 0)
            {
                _hasHeightOverride = true;
                _directHeightValue = _directBodyShapeValue;
            }
            else if (_directBodyShapeIndex == 4)
            {
                _hasBreastOverride = true;
                _directBreastValue = _directBodyShapeValue;
            }
        }

        private void TrackDirectFaceOverride(int index, float value)
        {
            _hasDirectFaceOverride = true;
            _directFaceShapeIndex = PluginSettings.ClampInt(index, 0, 51);
            _directFaceShapeValue = PluginSettings.Round2(PluginSettings.Clamp01(value));
        }

        private bool TryResolveTarget(int femaleIndex, out ChaControl target, out string error)
        {
            target = null;
            error = null;

            HSceneProc hscene = ResolveHScene();
            if (hscene == null)
            {
                error = "HSceneProc not found";
                return false;
            }

            List<ChaControl> females = GetFemales(hscene);
            if (females == null || females.Count == 0)
            {
                error = "female list empty";
                return false;
            }

            int index = PluginSettings.ClampInt(femaleIndex, 0, females.Count - 1);
            target = females[index];
            if (target == null)
            {
                error = "target female is null";
                return false;
            }

            _settings.TargetFemaleIndex = index;
            return true;
        }

        private HSceneProc ResolveHScene()
        {
            if (_currentHScene != null)
                return _currentHScene;

            _currentHScene = UnityEngine.Object.FindObjectOfType<HSceneProc>();
            return _currentHScene;
        }

        private static List<ChaControl> GetFemales(HSceneProc hscene)
        {
            return FieldLstFemale?.GetValue(hscene) as List<ChaControl>;
        }

        private static List<MotionIK> GetMotionIks(HSceneProc hscene)
        {
            return FieldLstMotionIk?.GetValue(hscene) as List<MotionIK>;
        }

        private static MorphSnapshot SnapshotFrom(ChaControl target)
        {
            if (target == null || target.chaFile == null || target.chaFile.custom == null)
                return null;

            return new MorphSnapshot
            {
                Body = CopyArray(target.chaFile.custom.body?.shapeValueBody),
                Face = CopyArray(target.chaFile.custom.face?.shapeValueFace)
            };
        }

        private static MorphSnapshot SnapshotFrom(ChaFileControl file)
        {
            if (file == null || file.custom == null)
                return null;

            return new MorphSnapshot
            {
                Body = CopyArray(file.custom.body?.shapeValueBody),
                Face = CopyArray(file.custom.face?.shapeValueFace)
            };
        }

        private static float[] CopyArray(float[] source)
        {
            if (source == null)
                return null;

            var copy = new float[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static bool ApplyBodyBlend(ChaControl target, float[] original, float[] targetValues, float blend)
        {
            if (target == null || target.chaFile == null || original == null || targetValues == null)
                return false;

            float[] values = target.chaFile.custom.body.shapeValueBody;
            int count = Math.Min(values.Length, Math.Min(original.Length, targetValues.Length));
            for (int i = 0; i < count; i++)
                values[i] = Mathf.Lerp(original[i], targetValues[i], blend);

            return count > 0;
        }

        private static bool ApplyFaceBlend(ChaControl target, float[] original, float[] targetValues, float blend)
        {
            if (target == null || target.chaFile == null || original == null || targetValues == null)
                return false;

            float[] values = target.chaFile.custom.face.shapeValueFace;
            int count = Math.Min(values.Length, Math.Min(original.Length, targetValues.Length));
            for (int i = 0; i < count; i++)
                values[i] = Mathf.Lerp(original[i], targetValues[i], blend);

            return count > 0;
        }

        private static bool ApplySnapshotBody(ChaControl target, float[] source)
        {
            if (target == null || target.chaFile == null || source == null)
                return false;

            float[] values = target.chaFile.custom.body.shapeValueBody;
            int count = Math.Min(values.Length, source.Length);
            Array.Copy(source, values, count);
            return count > 0;
        }

        private static bool ApplySnapshotFace(ChaControl target, float[] source)
        {
            if (target == null || target.chaFile == null || source == null)
                return false;

            float[] values = target.chaFile.custom.face.shapeValueFace;
            int count = Math.Min(values.Length, source.Length);
            Array.Copy(source, values, count);
            return count > 0;
        }

        private void SyncHeightBreastFromTarget(ChaControl target)
        {
            if (target == null)
                return;

            float height = PluginSettings.Round2(target.GetShapeBodyValue(0));
            float breast = PluginSettings.Round2(target.GetShapeBodyValue(4));
            _settings.Height = height;
            _settings.Breast = breast;
            SetEntryValue(_cfgHeight, height);
            SetEntryValue(_cfgBreast, breast);
        }

        private void RefreshHAfterBodyChange(int femaleIndex, string reason, bool writeLog = true)
        {
            HSceneProc hscene = ResolveHScene();
            if (hscene == null)
                return;

            RefreshAnimatorShapeParameters(hscene);
            RecalculateMotionIk(hscene, reason, writeLog);
        }

        private void RefreshAnimatorShapeParameters(HSceneProc hscene)
        {
            List<ChaControl> females = GetFemales(hscene);
            if (females == null || females.Count == 0)
                return;

            ChaControl female0 = females.Count > 0 ? females[0] : null;
            ChaControl female1 = females.Count > 1 ? females[1] : null;
            ChaControl male = FieldMale?.GetValue(hscene) as ChaControl;
            ChaControl male1 = FieldMale1?.GetValue(hscene) as ChaControl;
            ItemObject item = FieldItem?.GetValue(hscene) as ItemObject;

            float h0 = female0 != null ? female0.GetShapeBodyValue(0) : 0f;
            float b0 = female0 != null ? female0.GetShapeBodyValue(4) : 0f;
            float h1 = female1 != null ? female1.GetShapeBodyValue(0) : h0;
            float b1 = female1 != null ? female1.GetShapeBodyValue(4) : b0;

            SetAnimatorFloat(female0, "height", h0);
            SetAnimatorFloat(female0, "Breast", b0);
            SetAnimatorFloat(female0, "height1", h1);
            SetAnimatorFloat(female0, "Breast1", b1);

            if (female1 != null)
            {
                SetAnimatorFloat(female1, "height", h1);
                SetAnimatorFloat(female1, "Breast", b1);
                SetAnimatorFloat(female1, "height1", h0);
                SetAnimatorFloat(female1, "Breast1", b0);
            }

            SetAnimatorFloat(male, "height", h0);
            SetAnimatorFloat(male1, "height", h0);

            if (item != null)
            {
                int id = hscene.flags != null && hscene.flags.nowAnimationInfo != null ? hscene.flags.nowAnimationInfo.id : 0;
                bool secondIsPrimary = female1 != null && (id % 2) != 0;
                item.SetAnimatorParamFloat("height", secondIsPrimary ? h1 : h0);
                item.SetAnimatorParamFloat("height1", secondIsPrimary ? h0 : h1);
                item.SetAnimatorParamFloat("Breast", secondIsPrimary ? b1 : b0);
                item.SetAnimatorParamFloat("Breast1", secondIsPrimary ? b0 : b1);
            }
        }

        private static void SetAnimatorFloat(ChaControl cha, string key, float value)
        {
            if (cha != null)
                cha.setAnimatorParamFloat(key, value);
        }

        private void RecalculateMotionIk(HSceneProc hscene, string reason, bool writeLog = true)
        {
            List<MotionIK> motionIks = GetMotionIks(hscene);
            if (motionIks == null || motionIks.Count == 0)
                return;

            string stateName = hscene.flags != null ? hscene.flags.nowAnimStateName : null;
            if (string.IsNullOrEmpty(stateName))
                stateName = "Idle";

            int count = 0;
            foreach (MotionIK motionIk in motionIks.Where(x => x != null))
            {
                try
                {
                    motionIk.Calc(stateName);
                    count++;
                }
                catch (Exception ex)
                {
                    LogWarnAlways("MotionIK recalc failed: " + ex.Message);
                }
            }

            if (writeLog)
                LogExecution("MotionIK recalculated: state=" + stateName + " count=" + count + " reason=" + reason);
        }
    }
}
