using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace MainGameAiShoujoMode.Conversation
{
    internal sealed class KksDialoguePresentation : KksDialogueMachine.IPresentation, IDisposable
    {
        private sealed class Lease : IDisposable
        {
            internal Action Release;
            internal bool Advancing;
            public void Dispose() { var release = Release; Release = null; release?.Invoke(); }
        }
        private readonly KksNativeConversationSession _session;
        private readonly KksConversationView _view;
        private readonly KksVoicePlayback _voices;
        private readonly KksActorPresentation _actor;
        private readonly Action<Exception> _failed;
        private readonly Action<string> _log;
        private readonly Dictionary<string, string> _names = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly SaveData.WorldData.CallFileData _call;
        private readonly KksVoiceBundleContext _voiceBundles;
        private readonly Func<KksActorPresentation, IKksAdvSession> _advFactory;
        private IKksAdvSession _adv;
        private readonly ADV.CommandController.FontColor _colors = new ADV.CommandController.FontColor();
        private Lease _active;
        private bool _disposed;
        internal KksDialoguePresentation(KksNativeConversationSession session, KksConversationView view, KksVoicePlayback voices, KksVoiceBundleContext voiceBundles,
            Action<Exception> failed, Action<string> log, Func<KksActorPresentation, IKksAdvSession> advFactory = null)
        {
            _session = session; _view = view; _voices = voices; _failed = failed; _log = log;
            _voiceBundles = voiceBundles;
            _advFactory = advFactory;
            _call = SaveData.WorldData.FindCallFileData(session.Heroine.personality, session.Heroine.callMyID);
            session.Heroine.callName = _call.name;
            foreach (var data in session.Player.param.datas.Concat(session.Heroine.param.datas))
                if (data.command == SaveData.CharaData.Params.Data.Command.Replace) _names[data.key] = (string)data.value ?? string.Empty;
            _actor = new KksActorPresentation(session, log);
        }
        internal void FocusConversationCamera() => _actor.FocusConversationCamera();
        internal void ApplyExternalExpression(string expression)
        {
            _actor.CaptureExternalFace();
            var native = _actor.FindExpression(expression) ?? throw new InvalidOperationException("現在のキャラに表情がありません：" + expression);
            native.Change(_session.Actor); _actor.FocusConversationCamera();
        }
        internal void RestoreExternalExpression() => _actor.RestoreExternalFace();
        private string Replace(string text) => Regex.Replace(text ?? string.Empty, @"\[([^\]]+)\]", match =>
        {
            if (_names.TryGetValue(match.Groups[_session.Settings.one].Value, out var name)) return name;
            throw new NotSupportedException("未解決のKKS会話変数：" + match.Value);
        });
        private Lease Begin()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KksDialoguePresentation));
            _active?.Dispose(); var lease = new Lease(); _active = lease;
            lease.Release = () =>
            {
                if (!ReferenceEquals(_active, lease)) return;
                _active = null; _view.AdvanceRequested = null; _view.SetCanAdvance(false);
                _view.SetChoices(Array.Empty<KksConversationView.Choice>());
            };
            return lease;
        }
        public IDisposable Present(KksDialogueProgram.Cue cue, Action completed)
        {
            if (cue.Kind == KksDialogueProgram.RowKind.OpenScenario)
                return OpenScenario(cue, completed);
            var lease = Begin(); KksAdvTextPlayback playback = null;
            var release = lease.Release;
            lease.Release = () => { try { playback?.End(false); } finally { release?.Invoke(); } };
            try
            {
                var heroine = cue.Kind == KksDialogueProgram.RowKind.HeroineText;
                var clips = heroine ? Clips(cue.Source).ToArray() : Array.Empty<KksVoicePlayback.Clip>();
                var voiceNo = _session.Heroine.voiceNo; var pitch = _session.Heroine.voicePitch;
                Action replay = clips.Any() ? (Action)(() => _voices.Play(clips, voiceNo, pitch, null, null,
                    exception => _log?.Invoke("KKS_CONVERSATION_LOG_VOICE_ERROR " + exception))) : null;
                _view.SetTextColor(_colors[heroine ? "[H名]" : "[P名]"]);
                _view.PresentLine(Replace(heroine ? "[H名]" : "[P名]"), Replace(cue.Source.text), replay);
                _view.SetCanAdvance(true); _view.AdvanceRequested = () =>
                {
                    if (!ReferenceEquals(_active, lease)) return;
                    lease.Advancing = true; completed();
                };
                if (Manager.Config.TextData.NextVoiceStop) _voices.Stop();
                if (!clips.Any()) _actor.Apply(cue);
                if (clips.Any())
                {
                    // A normal table cue has one appearance row and one voice group per file.
                    // The ADV choreography applies that row on the first group, or flushes it on advance.
                    playback = new KksAdvTextPlayback(new[] { new KksAdvTextPlayback.Performance(() => _actor.Apply(cue), null, null) },
                        _session.Settings.zero,
                        (changed, ended, failed) => _voices.Play(clips, voiceNo, pitch, _session.Actor, ended, failed, index => changed()),
                        () => !lease.Advancing, _session.Settings.zero, _session.Settings.one);
                    playback.Start(() => { }, exception => { if (ReferenceEquals(_active, lease)) _failed(exception); });
                }
                return lease;
            }
            catch { lease.Dispose(); throw; }
        }
        private IDisposable OpenScenario(KksDialogueProgram.Cue cue, Action completed)
        {
            if (_advFactory == null) throw new NotSupportedException("別ADVの実行サービスが未接続です：" + cue.Source.assetbundle + "/" + cue.Source.file);
            var lease = Begin();
            try
            {
                if (_adv == null)
                {
                    var session = _advFactory(_actor) ?? throw new InvalidOperationException("ADV session factory returned no session.");
                    if (_disposed || !ReferenceEquals(_active, lease))
                    { if (!ReferenceEquals(_adv, session)) session.Dispose(); return lease; }
                    _adv = session;
                }
                _view.AdvanceRequested = () => { if (ReferenceEquals(_active, lease)) _adv.Advance(); };
                var run = _adv.Open(cue.Source.assetbundle, cue.Source.file,
                    () => { if (ReferenceEquals(_active, lease)) completed(); },
                    exception => { if (ReferenceEquals(_active, lease)) _failed(exception); });
                var release = lease.Release;
                if (release == null) run?.Dispose();
                else lease.Release = () => { try { run?.Dispose(); } finally { release(); } };
                return lease;
            }
            catch { lease.Dispose(); throw; }
        }
        internal IEnumerable<KksVoicePlayback.Clip> Clips(KksConversationLayout.Row row)
        {
            if (string.IsNullOrEmpty(row.file)) yield break;
            foreach (var file in row.file.Split(','))
            {
                var call = Regex.Match(file, @"callName(\d*)");
                if (call.Success)
                {
                    if (!int.TryParse(call.Groups[_session.Settings.one].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
                        throw new NotSupportedException("KKSの呼び名音声番号を解決できません：" + file);
                    yield return new KksVoicePlayback.Clip { Bundle = _voiceBundles.Resolve(_session.Heroine.voiceNo, _call.bundle), Asset = _call.GetFileName(index) };
                }
                else yield return new KksVoicePlayback.Clip { Bundle = _voiceBundles.Resolve(_session.Heroine.voiceNo,
                    _voices.ResolveBundle(_session.Heroine.voiceNo, row.assetbundle, file)), Asset = file };
            }
        }
        public IDisposable Select(IReadOnlyList<string> labels, Action<int> selected)
        {
            var lease = Begin();
            try
            {
                _view.SetChoices(labels.Select((text, index) => new KksConversationView.Choice(Replace(text), () =>
                {
                    if (!ReferenceEquals(_active, lease)) return;
                    _view.AppendChoice(Replace("[P名]"), Replace(text)); selected(index);
                })));
                return lease;
            }
            catch { lease.Dispose(); throw; }
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            var errors = new List<Exception>();
            foreach (var close in new Action[] { () => _active?.Dispose(), () => _voices.Stop(), () => _adv?.Dispose(), () => _actor.Dispose() })
                try { close(); } catch (Exception exception) { errors.Add(exception); }
            if (errors.Any()) throw new AggregateException("KKS parent conversation cleanup failed.", errors);
        }
    }
}
