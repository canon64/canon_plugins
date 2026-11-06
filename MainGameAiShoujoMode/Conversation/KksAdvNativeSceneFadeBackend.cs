using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Illusion.Extensions;
using UnityEngine;

namespace MainGameAiShoujoMode.Conversation
{
    // Borrow the installed KKS fade canvas only while no other transition is running.
    // Native FadeCanvas's finite transition and events do the animation; no polling is added.
    internal sealed class KksAdvNativeSceneFadeBackend : KksAdvSceneFadeHost.IBackend, IDisposable
    {
        private sealed class Operation : KksAdvSceneFadeHost.IOperation
        {
            internal KksAdvNativeSceneFadeBackend Owner;
            internal FadeCanvas.Fade Direction;
            internal Action Completed;
            internal Action<Exception> Failed;
            internal bool Finished;
            public void Finish()
            {
                if (Finished) return; Finished = true;
                var owner = Owner;
                if (owner == null || owner._disposed) return;
                owner._operations.Remove(this);
                try { owner._fade.Force(Direction); } finally { owner._fade.DefaultTime(); }
            }
            public void Dispose()
            {
                var owner = Owner; Owner = null; Completed = null; Failed = null;
                owner?._operations.Remove(this);
            }
        }
        private readonly MonoBehaviour _host;
        private readonly SceneFadeCanvas _fade;
        private readonly Action _changed;
        private readonly float _originalTime, _alpha;
        private readonly Color _color;
        private readonly bool _canvasEnabled, _covered, _blocksRaycasts, _interactable;
        private readonly HashSet<Operation> _operations = new HashSet<Operation>();
        private Coroutine _settle;
        private bool _owned, _disposed;
        internal KksAdvNativeSceneFadeBackend(MonoBehaviour host, Action changed)
        {
            _host = host != null ? host : throw new ArgumentNullException(nameof(host));
            _changed = changed; _fade = Manager.Scene.sceneFadeCanvas;
            if (_fade == null || _fade.canvas == null || _fade.canvasGroup == null || _fade.fadeImage == null || !_fade.isEnd)
                throw new InvalidOperationException("KKS scene fade is missing or owned by another transition.");
            var time = typeof(FadeCanvas).GetField("_time", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(typeof(FadeCanvas).FullName, "_time");
            _originalTime = (float)time.GetValue(_fade);
            _alpha = _fade.canvasGroup.alpha; _color = _fade.fadeImage.color;
            _canvasEnabled = _fade.canvas.enabled; _covered = _fade.isFading;
            _blocksRaycasts = _fade.canvasGroup.blocksRaycasts; _interactable = _fade.canvasGroup.interactable;
            _fade.onComplete += FadeCompleted;
        }
        public bool IsFading => !_disposed && _fade != null && _fade.isFading;
        public KksAdvSceneFadeHost.IOperation StartFade(bool fadeIn, Action completed, Action<Exception> failed)
        {
            if (_disposed || _host == null || _fade == null) throw new ObjectDisposedException(nameof(KksAdvNativeSceneFadeBackend));
            if (!_host.isActiveAndEnabled) throw new InvalidOperationException("KKS ADV fade callback host is inactive.");
            var operation = new Operation { Owner = this, Direction = fadeIn ? FadeCanvas.Fade.In : FadeCanvas.Fade.Out, Completed = completed, Failed = failed };
            _operations.Add(operation); _owned = true;
            try { _fade.StartFade(operation.Direction); }
            catch { operation.Dispose(); throw; }
            return operation;
        }
        public void SetTime(float duration) => _fade.time = duration;
        public void SetColor(string color) => _fade.SetColor(color.GetColor());
        private void FadeCompleted(FadeCanvas.Fade direction)
        {
            if (_disposed || !_owned || _host == null) return;
            if (_settle != null) _host.StopCoroutine(_settle);
            _settle = _host.StartCoroutine(Settle());
        }
        private IEnumerator Settle()
        {
            // onComplete is raised before native FadeCanvas clears its cancellation handle (isEnd).
            // Observe once after that stack has returned; another completion event handles a superseding fade.
            yield return new WaitForEndOfFrame();
            _settle = null;
            if (_disposed || _fade == null || !_fade.isEnd) yield break;
            foreach (var operation in _operations.ToArray())
            {
                if (!_operations.Contains(operation) || operation.Finished) continue;
                var completed = operation.Completed; operation.Completed = null;
                try { completed?.Invoke(); } catch (Exception exception) { operation.Failed?.Invoke(exception); }
            }
            _changed?.Invoke();
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            if (_fade != null) _fade.onComplete -= FadeCompleted;
            if (_settle != null && _host != null) _host.StopCoroutine(_settle);
            _settle = null;
            foreach (var operation in _operations.ToArray()) operation.Dispose();
            if (!_owned || _fade == null) return;
            var errors = new List<Exception>();
            foreach (var restore in new Action[]
            {
                () => _fade.Force(_covered ? FadeCanvas.Fade.In : FadeCanvas.Fade.Out),
                () => _fade.time = _originalTime, () => _fade.fadeImage.color = _color,
                () => _fade.canvasGroup.alpha = _alpha, () => _fade.canvasGroup.blocksRaycasts = _blocksRaycasts,
                () => _fade.canvasGroup.interactable = _interactable, () => _fade.canvas.enabled = _canvasEnabled
            }) try { restore(); } catch (Exception exception) { errors.Add(exception); }
            if (errors.Any()) throw new AggregateException("KKS ADV scene fade restoration failed.", errors);
        }
    }
}
