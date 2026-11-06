using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MainGameAiShoujoMode.Conversation
{
    internal sealed class KksAdvNativeMediaBackend : KksAdvMediaHost.IBackend, IDisposable
    {
        internal sealed class Target
        {
            internal SaveData.CharaData Data { get; }
            internal KksActorPresentation Presentation { get; }
            internal Target(SaveData.CharaData data, KksActorPresentation presentation)
            {
                Data = data ?? throw new ArgumentNullException(nameof(data)); Presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
                if (Data.chaCtrl != Presentation.Actor) throw new ArgumentException("KKS ADV target and presentation refer to different actors.");
            }
        }
        private readonly KksConversationView _view;
        private readonly KksVoicePlayback _voices;
        private readonly KksVoiceGroupPlayback _groups;
        private readonly KksVoiceBundleContext _bundles;
        private readonly Func<int, Target> _target;
        private readonly Func<IReadOnlyList<ADV.Commands.Base.Motion.Data>, Action, Action<Exception>, IDisposable> _waitMotions;
        private readonly KksAdvMachine.IHost _effects;
        private readonly ADV.Info.Audio _audio;
        private readonly ADV.Info.Anime.Play _anime;
        private readonly Action<string> _log;
        private readonly Action _changed;
        private bool _disposed;
        private IDisposable _voiceLease;
        internal readonly Dictionary<string, string[]> MotionAliases = new Dictionary<string, string[]>(StringComparer.Ordinal);
        internal readonly Dictionary<string, Manager.Game.Expression> ExpressionAliases = new Dictionary<string, Manager.Game.Expression>(StringComparer.Ordinal);
        internal ADV.CommandController.FontColor Colors { get; } = new ADV.CommandController.FontColor();
        internal KksAdvNativeMediaBackend(KksConversationView view, KksVoicePlayback voices, KksVoiceBundleContext bundles, Func<int, Target> target,
            Func<IReadOnlyList<ADV.Commands.Base.Motion.Data>, Action, Action<Exception>, IDisposable> waitMotions,
            KksAdvMachine.IHost effects, ADV.Info.Audio audio, ADV.Info.Anime.Play anime, Action<string> log, Action changed = null)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view)); _voices = voices ?? throw new ArgumentNullException(nameof(voices));
            _bundles = bundles ?? throw new ArgumentNullException(nameof(bundles)); _target = target ?? throw new ArgumentNullException(nameof(target));
            _waitMotions = waitMotions ?? throw new ArgumentNullException(nameof(waitMotions)); _effects = effects ?? throw new ArgumentNullException(nameof(effects));
            _audio = audio ?? throw new ArgumentNullException(nameof(audio)); _anime = anime ?? throw new ArgumentNullException(nameof(anime)); _log = log;
            _groups = voices.Groups;
            _changed = changed;
        }
        public bool IsRegulated => _effects.IsRegulated;
        public bool TextComplete => _effects.TextComplete;
        public void CompleteText() => _effects.CompleteText();
        public void ClearText() { _view.PresentLine(string.Empty, string.Empty, addToLog: false); _effects.ClearText(); }
        public void ClearCurrentCharaData() => _effects.ClearCurrentCharaData();
        public KksAdvMachine.ICommand Create(KksAdvMachine.Context context) => _effects.Create(context);
        public void ResolveVoice(ADV.Commands.Base.Voice.Data voice)
        {
            var target = GetTarget(voice.no); var actor = target.Data.chaCtrl;
            if (!voice.usePersonality) voice.personality = target.Data.voiceNo;
            if (!voice.usePitch) voice.pitch = target.Data.voicePitch;
            voice.chaCtrl = actor;
            var head = actor.GetReferenceInfo(ChaReference.RefObjKey.HeadParent); voice.transform = head == null ? null : head.transform;
            voice.bundle = _bundles.Resolve(voice.personality, voice.bundle);
            voice.is2D = _audio.is2D; voice.isNotMoveMouth = _audio.isNotMoveMouth;
            voice.eco = !_audio.eco.use ? null : new ADV.Info.Audio.Eco
            { use = _audio.eco.use, delay = _audio.eco.delay, decayRatio = _audio.eco.decayRatio, wetMix = _audio.eco.wetMix, dryMix = _audio.eco.dryMix };
        }
        public Manager.Game.Expression FindExpression(int actor, string name)
        {
            if (ExpressionAliases.TryGetValue(name, out var expression)) return expression;
            return GetTarget(actor).Presentation.FindExpression(name);
        }
        public string[] FindMotionAlias(string name) => MotionAliases.TryGetValue(name, out var alias) ? alias : null;
        public void ApplyExpressions(IReadOnlyList<ADV.Commands.Base.Expression.Data> expressions)
        {
            foreach (var expression in expressions) GetTarget(expression.no).Presentation.ApplyNativeExpression(expression);
        }
        public void ApplyMotions(IReadOnlyList<ADV.Commands.Base.Motion.Data> motions)
        {
            foreach (var motion in motions) GetTarget(motion.no).Presentation.ApplyNativeMotion(motion, _anime);
        }
        public IDisposable WaitMotions(IReadOnlyList<ADV.Commands.Base.Motion.Data> motions, Action completed, Action<Exception> failed) => _waitMotions(motions, completed, failed);
        public IDisposable PlayVoices(IReadOnlyList<ADV.Commands.Base.Voice.Data[]> voices, Action changed, Action completed, Action<Exception> failed)
        {
            var lease = _groups.Play(voices, changed, completed, failed);
            if (_disposed) lease.Dispose(); else _voiceLease = lease;
            return lease;
        }
        public void PresentText(string colorKey, string name, string text, IReadOnlyList<ADV.Commands.Base.Voice.Data[]> voices)
        {
            if (Manager.Config.TextData.NextVoiceStop) _voices.Stop();
            Action replay = voices.Any(group => group.Any()) ? (Action)(() => _groups.Play(voices, null, null,
                exception => _log?.Invoke("KKS_ADV_LOG_VOICE_ERROR " + exception), replay: true)) : null;
            _view.SetTextColor(Colors[colorKey ?? string.Empty]);
            _view.PresentLine(name, text, replay);
            _changed?.Invoke();
        }
        private Target GetTarget(int actor)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KksAdvNativeMediaBackend));
            var target = _target(actor);
            if (target?.Data?.chaCtrl == null || target.Presentation == null || target.Presentation.Actor != target.Data.chaCtrl)
                throw new InvalidOperationException("KKS ADV actor is unavailable: " + actor);
            return target;
        }
        public void Dispose() { if (_disposed) return; _disposed = true; var lease = _voiceLease; _voiceLease = null; lease?.Dispose(); }
    }
}
