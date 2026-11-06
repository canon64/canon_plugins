using System;
using System.Linq;

namespace MainGameAiShoujoMode.Conversation
{
    // Control instructions execute against the owned tape, never the global ADV scene.
    internal sealed class KksAdvCoreHost : KksAdvMachine.IHost
    {
        internal interface IAssets
        {
            string ResolveBundle(string bundle, string loadBundle);
            KksAdvScript Load(string bundle, string asset, string loadBundle);
            bool TryParameter(string name, out KksAdvScript script);
        }
        private sealed class Immediate : KksAdvMachine.ICommand
        {
            private readonly Action _action;
            internal Immediate(Action action) { _action = action; }
            public void Start(Action completed, Action<Exception> failed) { _action(); completed(); }
            public void End(bool processEnd) { }
        }
        private readonly KksAdvMachine.IHost _next;
        private readonly IAssets _assets;
        private readonly Func<string, string> _replaceVariable;
        private readonly Action<string, string> _setFileVariables;
        private readonly Action<string> _log;
        private readonly int _zero;
        internal KksAdvCoreHost(KksAdvMachine.IHost next, IAssets assets, Func<string, string> replaceVariable,
            Action<string, string> setFileVariables, Action<string> log, int zero)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _replaceVariable = replaceVariable ?? throw new ArgumentNullException(nameof(replaceVariable));
            _setFileVariables = setFileVariables ?? throw new ArgumentNullException(nameof(setFileVariables));
            _log = log; _zero = zero;
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
                case "Tag": return new Immediate(() => { });
                case "Close": return new Immediate(context.Close);
                case "ParameterFile": return new Immediate(() =>
                {
                    var args = Args(context, new[] { string.Empty });
                    if (_assets.TryParameter(args[_zero], out var script)) context.InsertParameters(script);
                    else _log?.Invoke("KKS_ADV_PARAMETER_KEY_NOT_FOUND key=" + args[_zero]);
                });
                case "Jump": return new Immediate(() =>
                {
                    var target = Args(context, null)[_zero];
                    if (!context.Jump(target, _replaceVariable)) _log?.Invoke("KKS_ADV_TAG_NOT_FOUND tag=" + target);
                });
                case "Open": return new Immediate(() =>
                {
                    var args = Args(context, new[] { string.Empty, string.Empty, bool.FalseString, bool.TrueString, bool.TrueString });
                    using (var cells = args.AsEnumerable().GetEnumerator())
                    {
                        cells.MoveNext(); var bundle = cells.Current;
                        cells.MoveNext(); var asset = cells.Current;
                        cells.MoveNext(); var add = bool.Parse(cells.Current);
                        cells.MoveNext(); var check = bool.Parse(cells.Current);
                        cells.MoveNext(); var next = bool.Parse(cells.Current);
                        bundle = _assets.ResolveBundle(bundle, context.LoadBundle);
                        _setFileVariables(bundle, asset);
                        if (!add) _next.ClearText();
                        context.Open(_assets.Load(bundle, asset, context.LoadBundle), !add, check, next);
                    }
                });
                default: return _next.Create(context);
            }
        }
        private string[] Args(KksAdvMachine.Context context, string[] defaults) => context.Entry.Row.InitializeArguments(defaults).Select(_replaceVariable).ToArray();
    }
}
