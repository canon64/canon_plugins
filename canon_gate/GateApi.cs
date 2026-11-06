using System;
using System.Collections.Generic;

namespace CanonGate
{
    /// <summary>Unity main-thread API. Permission is fixed for the lifetime of a session.</summary>
    public static partial class GateApi
    {
        public const string PluginGuid = "canon.gate";
        private static readonly Dictionary<string, Registration> Registrations = new Dictionary<string, Registration>(StringComparer.Ordinal);
        private static readonly Dictionary<string, ActivityRegistration> Activities = new Dictionary<string, ActivityRegistration>(StringComparer.Ordinal);
        private static bool _running;
        private static bool _strictAllowed;
        private static bool _ending;
        private static SessionKind _kind;
        private static ulong _sessionId;
        public static SessionContext Current { get; private set; }
        /// <summary>Last synchronous read/save boundary; existing permits and objects are still valid.</summary>
        public static event Action<SessionContext> SessionEnding;
        public enum SessionKind { None, H, Maker }
        public sealed class SessionContext
        {
            public SessionKind Kind { get; }
            public UnityEngine.MonoBehaviour Scene { get; }
            public HSceneProc HScene => Scene as HSceneProc;
            public ulong Id { get; }
            public bool IsAiShoujoMode { get; }
            public bool IsHotelMode { get; }
            public string HotelRoomId { get; }
            internal SessionContext(SessionKind kind, UnityEngine.MonoBehaviour scene, ulong id, bool aiShoujoMode, bool hotelMode)
            { Kind = kind; Scene = scene; Id = id; IsAiShoujoMode = aiShoujoMode; IsHotelMode = hotelMode; HotelRoomId = hotelMode ? GateApi.HotelMode?.RoomId : null; }
        }
        internal static Action<Exception> ReportError;
        internal static Action ReloadSettings;
        internal static Func<string, ActivityKind, bool> AllowActivity;

        public static bool IsAllowed => _running && _strictAllowed;

        internal static bool IsRegistered(string guid) => guid != null && (Registrations.ContainsKey(guid) || Activities.ContainsKey(guid));

        // Non-H work has an explicit owner event lifetime, independent of H permission.
        // A declaration is required; H clients must continue using Register.
        public enum ActivityKind { StartupService, Title, Studio, Preview, ActionScene, AiShoujoMode }
        // A process-owned notification surface must remain available while H/Maker permissions change.
        // This fixed infrastructure contract grants no gameplay permission to notification senders.
        public const string NotificationServiceGuid = "com.kks.maingame.notifications";
        public const string DevelopmentBridgeServiceGuid = "com.kks.maingame.developmentbridge";
        public static bool IsNotificationServiceActive(string ownerGuid) => ownerGuid == NotificationServiceGuid
            && Activities.TryGetValue(NotificationServiceGuid, out var activity) && activity.PersistentNotificationService && activity.IsActive;
        public static ActivityRegistration RegisterActivity(string pluginGuid, ActivityKind kind, Action onStart, Action onStop, bool includeSessions = true)
        {
            if (string.IsNullOrWhiteSpace(pluginGuid)) throw new ArgumentException("A plugin GUID is required.", nameof(pluginGuid));
            if (onStart == null) throw new ArgumentNullException(nameof(onStart));
            if (onStop == null) throw new ArgumentNullException(nameof(onStop));
            if (IsRegistered(pluginGuid)) throw new InvalidOperationException("Already registered: " + pluginGuid);
            var activity = new ActivityRegistration(pluginGuid, kind, onStart, onStop,
                (pluginGuid == NotificationServiceGuid || pluginGuid == DevelopmentBridgeServiceGuid) && kind == ActivityKind.StartupService && !includeSessions);
            Activities.Add(pluginGuid, activity);
            if (includeSessions)
                Registrations.Add(pluginGuid, new Registration(pluginGuid, activity.StartSession, activity.End));
            return activity;
        }

        public sealed class ActivityRegistration : IDisposable
        {
            public ActivityKind Kind { get; }
            public bool IsActive => _running && _active && _authorized && !_disposed;
            public ulong Id { get; private set; }
            public ActivityPermit Capture() => IsActive ? new ActivityPermit(this, Id) : default(ActivityPermit);
            private readonly string _guid;
            private readonly Action _start, _stop;
            private bool _active, _authorized, _disposed, _observed;
            internal bool PersistentNotificationService { get; }
            public UnityEngine.MonoBehaviour Scene { get; private set; }
            internal ActivityRegistration(string guid, ActivityKind kind, Action start, Action stop, bool persistentNotificationService)
            { _guid = guid; Kind = kind; _start = start; _stop = stop; PersistentNotificationService = persistentNotificationService; }
            public void Begin(UnityEngine.MonoBehaviour scene = null)
            {
                if (!_running || _ending || _disposed || _active || _observed || Current != null) return;
                if (Kind == ActivityKind.AiShoujoMode && (_aiMode == null || _aiMode.Suspended || !ReferenceEquals(scene, _aiMode.Owner))) return;
                _observed = true;
                // Evaluate the central allow-list only at the owner's start event.
                if (!PersistentNotificationService && (AllowActivity == null || !AllowActivity(_guid, Kind))) return;
                Scene = scene;
                StartSession();
            }
            internal void StartSession()
            {
                if (!_running || _disposed || _active) return;
                if (Current != null) Scene = Current.Scene;
                unchecked { ++Id; }
                _active = true;
                _authorized = true;
                try { _start(); }
                catch { End(); throw; }
            }
            public void End()
            {
                _observed = false;
                _authorized = false;
                if (!_active) { Scene = null; return; }
                _active = false;
                unchecked { ++Id; }
                try { _stop(); } catch (Exception ex) { Report(ex); }
                finally { Scene = null; }
            }
            public void Dispose()
            { if (_disposed) return; End(); _disposed = true; Activities.Remove(_guid); Registrations.Remove(_guid); }
            internal void Revoke() { _authorized = false; }
            internal Action PauseForActorLease(UnityEngine.MonoBehaviour owner)
            {
                if (!IsActive || Kind != ActivityKind.AiShoujoMode || !ReferenceEquals(Scene, owner)) return null;
                _active = false; unchecked { ++Id; }
                ulong paused = Id;
                try { _stop(); } catch { _authorized = false; Scene = null; throw; }
                return () =>
                {
                    if (_disposed || !_authorized || _active || Id != paused || !ReferenceEquals(Scene, owner)) return;
                    StartSession();
                };
            }
        }

        public readonly struct ActivityPermit
        {
            private readonly ActivityRegistration _registration;
            private readonly ulong _id;
            internal ActivityPermit(ActivityRegistration registration, ulong id) { _registration = registration; _id = id; }
            public bool IsValid => _registration != null && _registration.IsActive && _registration.Id == _id;
        }

        /// <summary>Register during plugin initialization. Late registrations wait for the next session.</summary>
        public static IDisposable Register(string pluginGuid, Action onStart, Action onStop)
        {
            if (string.IsNullOrWhiteSpace(pluginGuid)) throw new ArgumentException("A plugin GUID is required.", nameof(pluginGuid));
            if (onStart == null) throw new ArgumentNullException(nameof(onStart));
            if (onStop == null) throw new ArgumentNullException(nameof(onStop));
            if (IsRegistered(pluginGuid)) throw new InvalidOperationException("Already registered: " + pluginGuid);
            var registration = new Registration(pluginGuid, onStart, onStop);
            Registrations.Add(pluginGuid, registration);
            return registration;
        }

        /// <summary>A lookup of the already-decided result; never registers or evaluates a plugin.</summary>
        public static bool CanRun(string pluginGuid) => _running && pluginGuid != null
            && Registrations.TryGetValue(pluginGuid, out var registration) && registration.Allowed;

        public static void ReloadExceptions() => ReloadSettings?.Invoke();
        public static Permit Capture() => IsAllowed ? new Permit(null, _sessionId, true) : default(Permit);
        public static Permit Capture(string pluginGuid) => CanRun(pluginGuid)
            ? new Permit(Registrations[pluginGuid], _sessionId, false) : default(Permit);

        public readonly struct Permit
        {
            private readonly Registration _registration;
            private readonly ulong _session;
            private readonly bool _strict;
            internal Permit(Registration registration, ulong session, bool strict)
            { _registration = registration; _session = session; _strict = strict; }
            public bool IsValid => _running && _session != 0 && _session == _sessionId
                && (_strict ? _strictAllowed : _registration != null && !_registration.Disposed && _registration.Allowed);
        }

        internal static void SetRunning(bool running)
        {
            if (!running)
            {
                EndSession();
                _running = false;
                foreach (var activity in new List<ActivityRegistration>(Activities.Values)) activity.End();
            }
            else _running = true;
        }

        internal static void BeginSession(SessionKind kind, bool allPoseRoom, ExceptionPolicy policy,
            UnityEngine.MonoBehaviour scene = null, bool aiShoujoMode = false, bool hotelMode = false)
        {
            if (!_running || _ending || _kind != SessionKind.None || kind == SessionKind.None) return;
            // A non-H activity cannot carry authorization into a newly judged H/Maker.
            _ending = true;
            try
            {
                foreach (var activity in Activities.Values) if (!activity.PersistentNotificationService) activity.Revoke();
                foreach (var activity in new List<ActivityRegistration>(Activities.Values)) if (!activity.PersistentNotificationService) activity.End();
            }
            finally { _ending = false; }
            if (!_running) return;
            _kind = kind;
            unchecked { ++_sessionId; }
            ulong session = _sessionId;
            bool hotelSession = kind == SessionKind.H && hotelMode && HotelMode?.Owns(scene) == true;
            Current = new SessionContext(kind, scene, session, kind == SessionKind.H && aiShoujoMode, hotelSession);
            _strictAllowed = kind == SessionKind.H && (allPoseRoom || hotelSession);
            var snapshot = new List<Registration>(Registrations.Values);
            // Decide all permissions before invoking user code; config edits in callbacks cannot affect this session.
            foreach (var registration in snapshot)
                registration.Allowed = _strictAllowed || policy.Allows(registration.Guid, kind == SessionKind.Maker, Current.IsAiShoujoMode, Current.IsHotelMode);
            foreach (var registration in snapshot)
            {
                if (_sessionId != session || !_running) break;
                if (registration.Disposed || !registration.Allowed) continue;
                registration.Started = true;
                try { registration.Start(); }
                catch (Exception ex)
                {
                    Report(ex);
                    registration.Allowed = false;
                    StopRegistration(registration);
                }
            }
        }

        internal static void EndSession(SessionKind kind)
        {
            if (_kind == kind) EndSession();
        }

        private static void EndSession()
        {
            if (_kind == SessionKind.None || _ending) return;
            _ending = true;
            var ending = Current;
            if (SessionEnding != null)
                foreach (Action<SessionContext> observer in SessionEnding.GetInvocationList())
                    try { observer(ending); } catch (Exception ex) { Report(ex); }
            _kind = SessionKind.None;
            Current = null;
            _strictAllowed = false;
            unchecked { ++_sessionId; }
            var snapshot = new List<Registration>(Registrations.Values);
            // Revoke every result before calling any stop handler.
            foreach (var registration in snapshot) registration.Allowed = false;
            foreach (var activity in Activities.Values) if (!activity.PersistentNotificationService) activity.Revoke();
            try { foreach (var registration in snapshot) StopRegistration(registration); }
            finally { _ending = false; }
        }

        private static void StopRegistration(Registration registration)
        {
            if (!registration.Started) return;
            registration.Started = false;
            try { registration.Stop(); }
            catch (Exception ex) { Report(ex); }
        }

        private static void Report(Exception ex)
        {
            try { ReportError?.Invoke(ex); }
            catch { /* Logging must not interrupt another plugin's cleanup. */ }
        }

        internal sealed class Registration : IDisposable
        {
            internal readonly string Guid;
            internal readonly Action Start;
            internal readonly Action Stop;
            internal bool Allowed;
            internal bool Started;
            internal bool Disposed;
            internal Registration(string guid, Action start, Action stop) { Guid = guid; Start = start; Stop = stop; }
            public void Dispose()
            {
                if (Disposed) return;
                Disposed = true;
                Allowed = false;
                Registrations.Remove(Guid);
                StopRegistration(this);
            }
        }
    }
}
