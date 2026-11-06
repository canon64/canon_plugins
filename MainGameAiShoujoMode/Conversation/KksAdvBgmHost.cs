using System;
using System.Linq;

namespace MainGameAiShoujoMode.Conversation
{
    // Parse the installed command contract without entering the global Sound player.
    internal sealed class KksAdvBgmHost : KksAdvMachine.IHost
    {
        internal interface ISource
        {
            float Pitch { set; }
            float PanStereo { set; }
            float Time { set; }
            float Volume { set; }
        }
        internal interface IBackend
        {
            ISource Play(Illusion.Game.Utils.Sound.SettingBGM setting);
        }
        private sealed class Command : KksAdvMachine.ICommand
        {
            private readonly Action _play;
            internal Command(Action play) { _play = play; }
            public void Start(Action completed, Action<Exception> failed) { _play(); completed(); }
            public void End(bool processEnd) { }
        }
        private readonly KksAdvMachine.IHost _next;
        private readonly IBackend _backend;
        private readonly Func<string, string> _replace;
        private readonly int _zero;
        internal KksAdvBgmHost(KksAdvMachine.IHost next, IBackend backend, Func<string, string> replace, int zero)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _replace = replace ?? throw new ArgumentNullException(nameof(replace)); _zero = zero;
        }
        public bool IsRegulated => _next.IsRegulated;
        public bool TextComplete => _next.TextComplete;
        public void CompleteText() => _next.CompleteText();
        public void ClearText() => _next.ClearText();
        public void ClearCurrentCharaData() => _next.ClearCurrentCharaData();
        public KksAdvMachine.ICommand Create(KksAdvMachine.Context context) => context.Entry.Row.Command == "BGMPlay"
            ? new Command(() => Play(context.Entry.Row)) : _next.Create(context);

        private void Play(KksAdvScript.Row row)
        {
            using (var cells = row.InitializeArguments(new ADV.Commands.Sound.BGM.Play().ArgsDefault).Select(_replace).GetEnumerator())
            {
                cells.MoveNext(); var name = cells.Current;
                Illusion.Game.Utils.Sound.SettingBGM setting;
                if (int.TryParse(name, out var number)) setting = new Illusion.Game.Utils.Sound.SettingBGM(number);
                else
                {
                    var index = Illusion.Utils.Enum<Illusion.Game.BGM>.FindIndex(name, true);
                    setting = index >= _zero ? new Illusion.Game.Utils.Sound.SettingBGM(index) : new Illusion.Game.Utils.Sound.SettingBGM(name);
                }
                cells.MoveNext(); if (!string.IsNullOrEmpty(cells.Current)) setting.loader.asset = cells.Current;
                cells.MoveNext(); float.Parse(cells.Current); // Native Delay is validated but its result is discarded.
                cells.MoveNext(); if (!string.IsNullOrEmpty(cells.Current)) setting.loader.fadeTime = float.Parse(cells.Current);
                cells.MoveNext(); bool.Parse(cells.Current); // Native isAsync is also discarded; playback is synchronous.
                var source = _backend.Play(setting);
                if (source == null) return; // Native only parses the optional tail when a source exists.
                cells.MoveNext(); if (!string.IsNullOrEmpty(cells.Current)) source.Pitch = float.Parse(cells.Current);
                cells.MoveNext(); if (!string.IsNullOrEmpty(cells.Current)) source.PanStereo = float.Parse(cells.Current);
                cells.MoveNext(); if (!string.IsNullOrEmpty(cells.Current)) source.Time = float.Parse(cells.Current);
                cells.MoveNext(); if (!string.IsNullOrEmpty(cells.Current)) source.Volume = float.Parse(cells.Current);
            }
        }
    }
}
