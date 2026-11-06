using System;
using System.Threading;

namespace MainGameAiShoujoMode.Conversation
{
    // Request-owned callbacks only. No frame polling, global pump or background Unity access.
    internal sealed class AiReplyRun : IDisposable
    {
        internal interface IBackend
        {
            string Stamp { get; }
            void Validate(AiReplyResponse reply);
            void Present(AiReplyResponse reply);
            void Synthesize(string text, CancellationToken cancellation, Action<byte[]> ready, Action<Exception> failed);
            IDisposable Play(byte[] wave, Action completed, Action<Exception> failed);
            void Execute(AiReplyResponse reply);
            void Completed();
            void Failed(Exception error);
        }
        private readonly IBackend _backend;
        private readonly AiReplyResponse _reply;
        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();
        private readonly string _stamp;
        private IDisposable _voice;
        private bool _ended, _started, _voiceReady;
        internal bool Active => _started && !_ended;
        internal AiReplyRun(IBackend backend, AiReplyResponse reply)
        { _backend = backend; _reply = reply; _stamp = backend.Stamp; }
        internal void Start()
        {
            if (_started || _ended) throw new InvalidOperationException("AI返答は開始済みです。");
            _started = true;
            try
            {
                _backend.Validate(_reply); EnsureCurrent(); _backend.Present(_reply);
                if (_ended) return;
                _backend.Synthesize(_reply.text, _cancel.Token, Ready, Fail);
            }
            catch (Exception error) { Fail(error); }
        }
        private void EnsureCurrent()
        {
            if (_ended || _backend.Stamp != _stamp) throw new OperationCanceledException("返答待ちの間に会話または状態が変わりました。現在の状態でやり直してください。");
        }
        private void Ready(byte[] wave)
        {
            if (_ended || _voiceReady) return;
            _voiceReady = true;
            try
            {
                EnsureCurrent();
                var voice = _backend.Play(wave, Finish, Fail);
                if (_ended) voice?.Dispose(); else _voice = voice;
            }
            catch (Exception error) { Fail(error); }
        }
        private void Finish()
        {
            if (_ended) return;
            try
            {
                EnsureCurrent(); _backend.Validate(_reply);
                // End before executing: action callbacks may close/dispose the conversation reentrantly.
                End(); _backend.Execute(_reply); _backend.Completed();
            }
            catch (Exception error) { if (!_ended) Fail(error); else _backend.Failed(error); }
        }
        private void Fail(Exception error)
        {
            if (_ended) return;
            End(); _backend.Failed(error);
        }
        private void End()
        {
            if (_ended) return; _ended = true;
            _cancel.Cancel(); _voice?.Dispose(); _voice = null;
        }
        public void Dispose() => End();
    }
}
