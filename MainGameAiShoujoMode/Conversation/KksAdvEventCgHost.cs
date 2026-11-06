using System;
using System.Linq;

namespace MainGameAiShoujoMode.Conversation
{
    internal sealed class KksAdvEventCgHost : KksAdvMachine.IHost
    {
        internal interface IBackend
        {
            bool Release();
            void Load(string bundle, string asset);
            void SetCameraCorrection(bool active);
            void Initialize();
            void ResetHeroineMotions();
            void StartCrossFade();
            void Next(int scene);
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
        internal KksAdvEventCgHost(KksAdvMachine.IHost next, IBackend backend, Func<string, string> replace)
        { _next = next ?? throw new ArgumentNullException(nameof(next)); _backend = backend ?? throw new ArgumentNullException(nameof(backend)); _replace = replace ?? throw new ArgumentNullException(nameof(replace)); }
        public bool IsRegulated => _next.IsRegulated;
        public bool TextComplete => _next.TextComplete;
        public void CompleteText() => _next.CompleteText();
        public void ClearText() => _next.ClearText();
        public void ClearCurrentCharaData() => _next.ClearCurrentCharaData();
        public KksAdvMachine.ICommand Create(KksAdvMachine.Context context)
        {
            switch (context.Entry.Row.Command)
            {
                case "EventCGSetting": return new Immediate(() =>
                {
                    _backend.Release();
                    using (var cells = context.Entry.Row.InitializeArguments(new ADV.Commands.EventCG.Setting().ArgsDefault).Select(_replace).GetEnumerator())
                    {
                        cells.MoveNext(); var bundle = cells.Current; cells.MoveNext(); _backend.Load(bundle, cells.Current);
                    }
                    _backend.SetCameraCorrection(false); _backend.Initialize();
                });
                case "EventCGRelease": return new Immediate(() =>
                {
                    // Native Release parses isMotionContinue only after an existing CG was released.
                    if (!_backend.Release()) return;
                    var keepMotion = bool.Parse(context.Entry.Row.InitializeArguments(new ADV.Commands.EventCG.Release().ArgsDefault).Select(_replace).First());
                    if (!keepMotion) _backend.ResetHeroineMotions();
                    _backend.SetCameraCorrection(true);
                });
                case "EventCGNext": return new Immediate(() =>
                {
                    var scene = int.Parse(context.Entry.Row.InitializeArguments(new ADV.Commands.EventCG.Next().ArgsDefault).Select(_replace).First());
                    _backend.StartCrossFade(); _backend.Next(scene);
                });
                default: return _next.Create(context);
            }
        }
    }
}
