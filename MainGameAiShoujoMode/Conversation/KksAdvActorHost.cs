using System;
using System.Collections.Generic;
using System.Linq;

namespace MainGameAiShoujoMode.Conversation
{
    internal sealed class KksAdvActorHost : KksAdvMachine.IHost
    {
        internal sealed class LookTarget
        {
            internal int Actor, Type;
            internal string Name;
            internal float Rate, Rotation, Range, Distance;
        }
        internal interface IBackend
        {
            void LookEyes(int actor, int pattern);
            void LookNeck(int actor, int pattern, float rate);
            void LookEyesTarget(LookTarget target);
            void LookNeckTarget(LookTarget target);
            void DefaultMotion(int actor);
        }
        private sealed class Immediate : KksAdvMachine.ICommand
        {
            private readonly Action _action;
            internal Immediate(Action action) { _action = action; }
            public void Start(Action completed, Action<Exception> failed) { _action(); completed(); }
            public void End(bool processEnd) { }
        }
        private readonly KksAdvMachine.IHost _next;
        private readonly IBackend _backend;
        private readonly Func<string, string> _replace;
        private readonly KksConversationRuntimeSettings _settings;
        internal KksAdvActorHost(KksAdvMachine.IHost next, IBackend backend, Func<string, string> replace, KksConversationRuntimeSettings settings)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next)); _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _replace = replace ?? throw new ArgumentNullException(nameof(replace)); _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }
        public bool IsRegulated => _next.IsRegulated;
        public bool TextComplete => _next.TextComplete;
        public void CompleteText() => _next.CompleteText();
        public void ClearText() => _next.ClearText();
        public void ClearCurrentCharaData() => _next.ClearCurrentCharaData();
        public KksAdvMachine.ICommand Create(KksAdvMachine.Context context)
        {
            switch (context.Entry.Row.Command)
            {
                case "CharaLookEyes": return new Immediate(() =>
                {
                    using (var cells = Args(context, new ADV.Commands.Chara.LookEyes().ArgsDefault).GetEnumerator())
                    { cells.MoveNext(); var actor = int.Parse(cells.Current); cells.MoveNext(); _backend.LookEyes(actor, int.Parse(cells.Current)); }
                });
                case "CharaLookNeck": return new Immediate(() =>
                {
                    using (var cells = Args(context, new ADV.Commands.Chara.LookNeck().ArgsDefault).GetEnumerator())
                    {
                        cells.MoveNext(); var actor = int.Parse(cells.Current); cells.MoveNext(); var pattern = int.Parse(cells.Current);
                        cells.MoveNext(); _backend.LookNeck(actor, pattern, float.Parse(cells.Current));
                    }
                });
                case "CharaLookEyesTarget": return new Immediate(() => _backend.LookEyesTarget(Target(context, true)));
                case "CharaLookNeckTarget": return new Immediate(() => _backend.LookNeckTarget(Target(context, false)));
                case "CharaMotionDefault": return new Immediate(() => _backend.DefaultMotion(int.Parse(Args(context,
                    new ADV.Commands.Chara.MotionDefault().ArgsDefault).First())));
                default: return _next.Create(context);
            }
        }
        private LookTarget Target(KksAdvMachine.Context context, bool eyes)
        {
            var defaults = eyes ? new ADV.Commands.Chara.LookEyesTarget().ArgsDefault : new ADV.Commands.Chara.LookNeckTarget().ArgsDefault;
            using (var cells = Args(context, defaults).GetEnumerator())
            {
                cells.MoveNext(); var actor = int.Parse(cells.Current); cells.MoveNext(); var type = int.Parse(cells.Current); cells.MoveNext(); var name = cells.Current;
                return new LookTarget
                {
                    Actor = actor, Type = type, Name = name,
                    Rate = Optional(cells, _settings.advLookTargetRate), Rotation = Optional(cells, _settings.advLookTargetRotation),
                    Range = Optional(cells, _settings.advLookTargetRange), Distance = Optional(cells, eyes ? _settings.advEyeTargetDistance : _settings.advNeckTargetDistance)
                };
            }
        }
        private static float Optional(IEnumerator<string> cells, float fallback) => cells.MoveNext() && !string.IsNullOrEmpty(cells.Current) ? float.Parse(cells.Current) : fallback;
        private IEnumerable<string> Args(KksAdvMachine.Context context, string[] defaults) => context.Entry.Row.InitializeArguments(defaults).Select(_replace);
    }
}
