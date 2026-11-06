using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MainGameIPhoneCamera
{
    /// <summary>Additional apps register descriptors at startup; Unity work starts only when opened in a permitted H.</summary>
    public interface IPhoneApp
    {
        string Id { get; }
        string Title { get; }
        Sprite Icon { get; }
        void Open(RectTransform viewport);
        void Close();
        void Navigate(int horizontal, int vertical);
        void Confirm();
        void Trigger();
    }
    public static class IPhoneAppApi
    {
        private static readonly object Sync = new object();
        private static readonly List<IPhoneApp> Apps = new List<IPhoneApp>();
        internal static event Action Changed;
        internal static IPhoneApp[] Snapshot() { lock (Sync) return Apps.ToArray(); }
        public static IDisposable Register(IPhoneApp app)
        {
            if (app == null || string.IsNullOrWhiteSpace(app.Id) || string.IsNullOrWhiteSpace(app.Title)) throw new ArgumentException("App ID/title required");
            lock (Sync)
            {
                if (new[] { "photos", "camera", "settings" }.Contains(app.Id) || Apps.Any(x => x.Id == app.Id)) throw new ArgumentException("Duplicate app ID");
                Apps.Add(app);
            }
            Changed?.Invoke(); return new Registration(app);
        }
        private sealed class Registration : IDisposable
        {
            private IPhoneApp _app;
            internal Registration(IPhoneApp app) { _app = app; }
            public void Dispose()
            {
                if (_app == null) return;
                lock (Sync) Apps.Remove(_app);
                _app = null; Changed?.Invoke();
            }
        }
    }
}
