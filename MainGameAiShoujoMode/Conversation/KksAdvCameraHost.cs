using System;
using System.Linq;

namespace MainGameAiShoujoMode.Conversation
{
    internal sealed class KksAdvCameraHost : KksAdvMachine.IHost
    {
        internal sealed class Vector
        {
            internal readonly float X, Y, Z;
            internal Vector(float x, float y, float z) { X = x; Y = y; Z = z; }
        }
        internal interface IBackend
        {
            void SetLightActive(bool active);
            void SetLightAngle(bool local, float x, float y);
            void SetDepthOfFieldTarget(int actor);
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
        private readonly Func<string, Vector> _findVector;
        internal KksAdvCameraHost(KksAdvMachine.IHost next, IBackend backend, Func<string, string> replace, Func<string, Vector> findVector)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next)); _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _replace = replace ?? throw new ArgumentNullException(nameof(replace)); _findVector = findVector ?? throw new ArgumentNullException(nameof(findVector));
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
                case "CameraLightActive": return new Immediate(() => _backend.SetLightActive(bool.Parse(context.Entry.Row.InitializeArguments(
                    new ADV.Commands.Game.CameraCorrectLightActive().ArgsDefault).Select(_replace).First())));
                case "DOFTarget": return new Immediate(() => _backend.SetDepthOfFieldTarget(int.Parse(context.Entry.Row.InitializeArguments(
                    new ADV.Commands.CameraEffect.DOFTarget().ArgsDefault).Select(_replace).First())));
                case "CameraLightAngle": return new Immediate(() =>
                {
                    using (var cells = context.Entry.Row.InitializeArguments(new ADV.Commands.Game.CameraCorrectLightAngle().ArgsDefault).Select(_replace).GetEnumerator())
                    {
                        cells.MoveNext(); var local = bool.Parse(cells.Current);
                        cells.MoveNext(); var first = cells.Current;
                        var vector = !string.IsNullOrEmpty(first) && !float.TryParse(first, out _) ? _findVector(first) : null;
                        if (vector != null) _backend.SetLightAngle(local, vector.X, vector.Y);
                        else
                        {
                            // CountAddV3 uses TryParse/SafeGet: missing or invalid coordinates remain zero.
                            float.TryParse(first, out var x); float.TryParse(cells.MoveNext() ? cells.Current : null, out var y);
                            _backend.SetLightAngle(local, x, y); // Native rotation always discards Z.
                        }
                    }
                });
                default: return _next.Create(context);
            }
        }
    }
}
