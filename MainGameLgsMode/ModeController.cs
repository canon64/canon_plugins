using System;

namespace MainGameLgsMode
{
    public enum GameSituation { Other, FreeH, Walking }

    public sealed class ModeController : IDisposable
    {
        private readonly object _sync = new object();
        private readonly ICommandMailbox _mailbox;
        private readonly Action<string> _log;
        private readonly Action<Exception> _error;
        private ModeSettings _settings;
        private GameSituation _situation;
        private int _manualMode;
        private int _lastRequestedMode;
        private bool _disposed;

        public ModeController(ModeSettings settings, ICommandMailbox mailbox,
            Action<string> log = null, Action<Exception> error = null)
        {
            _settings = settings; _mailbox = mailbox; _log = log; _error = error;
            Publish(GameSituation.Other);
        }

        public void Enter(GameSituation situation)
        {
            lock (_sync)
            {
                if (_disposed || _situation == situation) return;
                _situation = situation;
                _manualMode = 0;
                Publish(situation);
            }
        }

        public void CycleManual()
        {
            lock (_sync)
            {
                if (_disposed || !_settings.Enabled || !_settings.ManualCycleEnabled) return;
                _manualMode = _lastRequestedMode % 3 + 1;
                Publish(_situation);
            }
        }

        public void Configure(ModeSettings settings)
        {
            lock (_sync)
            {
                if (_disposed) return;
                _settings = settings;
                Publish(_situation);
            }
        }

        private void Publish(GameSituation situation)
        {
            int mode = !_settings.Enabled ? 0 : _manualMode != 0 ? _manualMode : situation == GameSituation.FreeH
                ? _settings.FreeHMode : situation == GameSituation.Walking ? _settings.WalkingMode : 0;
            try
            {
                _mailbox.Write(mode, _settings.CommandLifetimeSeconds);
                if (mode != 0) _lastRequestedMode = mode;
                if (_settings.EnableLogs) _log?.Invoke("LGS request: " + situation + " -> " + (mode == 0 ? "hold" : "M" + mode));
            }
            catch (Exception error) { Report(error); }
        }

        public void Report(Exception error)
        {
            lock (_sync) { if (!_disposed && _settings.EnableLogs) _error?.Invoke(error); }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                // Cancel unread commands; no restore M-key is sent on shutdown.
                try { _mailbox.Write(0, _settings.CommandLifetimeSeconds); }
                catch (Exception error) { Report(error); }
                _disposed = true;
            }
        }
    }
}
