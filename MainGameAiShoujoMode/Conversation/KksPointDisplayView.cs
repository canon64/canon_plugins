using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ActionGame.Information;
using DG.Tweening;
using TMPro;
using UnityEngine;
using MapEnvironmentRuntime;

namespace MainGameAiShoujoMode.Conversation
{
    // Native passive hierarchy, fonts/materials and finite effects. No Game.Player or global point UI.
    internal sealed class KksPointDisplayView : KksPointDisplayPlayback.IDevice, IDisposable
    {
#pragma warning disable CS0649
        [Serializable] private sealed class Document { public Settings point; }
        [Serializable] private sealed class Settings
        {
            public string moverPath, textPath, addPath, addTextPath;
            public int moverType, addEase;
            public float duration, waitTime, fadeDuration, addFadeDuration, up, addDuration;
        }
#pragma warning restore CS0649
        private sealed class Operation : IDisposable
        {
            internal KksPointDisplayView Owner;
            internal readonly List<Tween> Tweens = new List<Tween>();
            internal Coroutine Counter;
            internal bool Ended;
            internal Action Began, Completed;
            internal Action<Exception> Failed;
            internal int Target, Remaining;
            public void Dispose()
            {
                if (Ended) return; Ended = true;
                var errors = new List<Exception>();
                foreach (var tween in Tweens) try { tween.Kill(); } catch (Exception exception) { errors.Add(exception); }
                Tweens.Clear();
                var counter = Counter; Counter = null;
                try { if (counter != null && Owner._host != null) Owner._host.StopCoroutine(counter); }
                catch (Exception exception) { errors.Add(exception); }
                if (ReferenceEquals(Owner._active, this)) Owner._active = null;
                if (errors.Count != default(int)) throw new AggregateException("Point display animation cleanup failed.", errors);
            }
        }
        private readonly MonoBehaviour _host;
        private readonly KksUiHierarchy _ui;
        private readonly Settings _settings;
        private readonly RectTransform _mover, _add;
        private readonly TMP_Text _text, _addText;
        private readonly CanvasGroup _fade, _addFade;
        private readonly Vector2 _addPosition;
        private readonly int _zero, _one;
        private Operation _active;
        private bool _disposed;
        internal KksPointDisplayView(MonoBehaviour host, Transform parent, string contentDirectory)
        {
            _host = host != null ? host : throw new ArgumentNullException(nameof(host));
            _ui = new KksUiHierarchy(parent, contentDirectory, "kks-point-ui.json");
            try
            {
                _settings = JsonCodec.FromJson<Document>(File.ReadAllText(Path.Combine(contentDirectory, "kks-point-ui.json"))).point
                    ?? throw new InvalidDataException("Native point animation settings are missing.");
                _zero = _ui.Data.zero; _one = _ui.Data.one;
                _mover = _ui.Get<RectTransform>(_settings.moverPath); _add = _ui.Get<RectTransform>(_settings.addPath);
                _text = _ui.Get<TextMeshProUGUI>(_settings.textPath); _addText = _ui.Get<TextMeshProUGUI>(_settings.addTextPath);
                _fade = _ui.Root.GetComponent<CanvasGroup>(); _addFade = _ui.Get<CanvasGroup>(_settings.addPath);
                _addPosition = _add.anchoredPosition;
                if (_fade == null || !Enum.IsDefined(typeof(Mover.Type), _settings.moverType) || !Enum.IsDefined(typeof(Ease), _settings.addEase))
                    throw new InvalidDataException("Native point animation components or enums are invalid.");
                Reset(_zero);
            }
            catch { _ui.Dispose(); throw; }
        }
        public void Reset(int score)
        {
            if (_disposed) return;
            _active?.Dispose(); _text.text = score.ToString("0");
            _fade.alpha = _zero; _addFade.alpha = _zero; _add.anchoredPosition = _addPosition;
            _ui.Root.SetPivotMove((Mover.Type)_settings.moverType, false, _mover, _zero).Complete();
            _ui.Root.SetActive(false);
        }
        public IDisposable Play(int now, int target, Action beganAdding, Action completed, Action<Exception> failed)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KksPointDisplayView));
            if (_host == null || !_host.isActiveAndEnabled) throw new InvalidOperationException("Point display host is inactive.");
            _active?.Dispose(); Reset(now);
            var operation = new Operation { Owner = this, Target = target, Began = beganAdding, Completed = completed, Failed = failed };
            _active = operation; _ui.Root.SetActive(true);
            try
            {
                Track(operation, _fade.DOFade(_one, _settings.fadeDuration).SetEase(Ease.Linear));
                Track(operation, _ui.Root.SetPivotMove((Mover.Type)_settings.moverType, true, _mover, _settings.duration), () => Add(operation, now));
            }
            catch (Exception exception) { Fail(operation, exception); }
            return operation;
        }
        private void Track(Operation operation, Tween tween, Action completed = null)
        {
            tween.Pause(); tween.SetUpdate(true); operation.Tweens.Add(tween);
            tween.OnComplete(() => Guard(operation, completed));
            if (IsCurrent(operation)) tween.Play(); else tween.Kill();
        }
        private bool IsCurrent(Operation operation) => !_disposed && !operation.Ended && ReferenceEquals(_active, operation);
        private void Guard(Operation operation, Action action)
        { if (IsCurrent(operation)) try { action?.Invoke(); } catch (Exception exception) { Fail(operation, exception); } }
        private void Add(Operation operation, int now)
        {
            var delta = operation.Target - now;
            _addText.text = (delta > _zero ? "+" : string.Empty) + delta.ToString("0");
            _add.anchoredPosition = _addPosition;
            Track(operation, _addFade.DOFade(_one, _settings.addFadeDuration).SetEase(Ease.Linear));
            operation.Remaining = _one + _one;
            Track(operation, _add.DOAnchorPos(_addPosition + Vector2.up * _settings.up, _settings.addDuration).SetEase((Ease)_settings.addEase), () => Added(operation));
            operation.Began?.Invoke();
            if (!IsCurrent(operation)) return;
            var counter = _host.StartCoroutine(Count(operation, now));
            if (IsCurrent(operation)) operation.Counter = counter; else if (counter != null) _host.StopCoroutine(counter);
        }
        private IEnumerator Count(Operation operation, int now)
        {
            // A bounded native count-up animation: one visible increment and sound per rendered frame.
            // This coroutine exists only for this finite award; it never monitors world state.
            do
            {
                if (!IsCurrent(operation)) yield break;
                now += now < operation.Target ? _one : now > operation.Target ? -_one : _zero;
                Guard(operation, () => { _text.text = now.ToString("0"); Illusion.Game.Utils.Sound.Play(Illusion.Game.SystemSE.get_point_add); });
                if (!IsCurrent(operation)) yield break;
                yield return null;
            } while (now != operation.Target);
            operation.Counter = null;
            Guard(operation, () => { Illusion.Game.Utils.Sound.Play(Illusion.Game.SystemSE.get_point); Added(operation); });
        }
        private void Added(Operation operation)
        {
            operation.Remaining -= _one;
            if (operation.Remaining != _zero) return;
            Track(operation, DOTween.Sequence().AppendInterval(_settings.waitTime), () =>
                Track(operation, _addFade.DOFade(_zero, _settings.addFadeDuration).SetEase(Ease.Linear), () => Hide(operation)));
        }
        private void Hide(Operation operation)
        {
            Track(operation, _fade.DOFade(_zero, _settings.fadeDuration).SetEase(Ease.Linear));
            Track(operation, _ui.Root.SetPivotMove((Mover.Type)_settings.moverType, false, _mover, _settings.duration), () =>
            {
                var completed = operation.Completed; var failed = operation.Failed;
                try { operation.Dispose(); } catch (Exception exception) { failed?.Invoke(exception); return; }
                completed?.Invoke();
            });
        }
        private void Fail(Operation operation, Exception exception)
        {
            if (!IsCurrent(operation)) return;
            var failed = operation.Failed;
            try { operation.Dispose(); } catch (Exception cleanup) { exception = new AggregateException(exception, cleanup); }
            failed?.Invoke(exception);
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            try { _active?.Dispose(); } finally { _ui.Dispose(); }
        }
    }
}
