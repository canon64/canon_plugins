using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    // Diagnostic only: no source/listener changes, timers, or frame callbacks.
    internal sealed class HotelVoiceDiagnostics : IDisposable
    {
        private const string HarmonyId = Plugin.Guid + ".hotel-voice-diagnostics";
        private static HotelVoiceDiagnostics _current;
        private readonly Func<bool> _enabled;
        private readonly Func<AiModeHSession> _session;
        private readonly Action<string> _log;
        private readonly Harmony _harmony;
        private readonly MethodInfo _play;
        private bool _disposed;
        private bool Allowed => !_disposed && _enabled() && _session()?.Busy == true;

        internal HotelVoiceDiagnostics(Func<bool> enabled, Func<AiModeHSession> session, Action<string> log)
        {
            _enabled = enabled; _session = session; _log = log;
            // Even accidental construction with diagnostics OFF cannot install a hook.
            if (!Allowed) return;
            _play = AccessTools.Method(typeof(Manager.Voice), "Play_Standby", new[] { typeof(AudioSource), typeof(Manager.Voice.Loader) });
            if (_play == null) throw new MissingMethodException("Manager.Voice.Play_Standby");
            _harmony = new Harmony(HarmonyId);
            _harmony.Patch(_play, postfix: new HarmonyMethod(typeof(HotelVoiceDiagnostics), nameof(Played)));
            _current = this;
        }
        private bool Owns(Transform mouth)
        {
            var mouths = _session()?.RoomFlags?.transVoiceMouth;
            return mouth != null && mouths != null && mouths.Any(item => item == mouth);
        }
        private static void Played(AudioSource __0, Manager.Voice.Loader __1)
        {
            var owner = _current;
            if (owner == null || !owner.Allowed || __1 == null || !owner.Owns(__1.voiceTrans)) return;
            // Native position-follow runs on its next Update. Record the intended mouth too;
            // the manual snapshot records settled source coordinates after camera movement.
            owner.Record("standby-before-first-follow", __0, __1.voiceTrans);
        }
        internal void Snapshot(string trigger)
        {
            if (!Allowed) return;
            try
            {
                var sources = UnityEngine.Object.FindObjectsOfType<AudioSource>();
                bool found = false;
                foreach (var source in sources)
                {
                    if (source == null || !source.isPlaying) continue;
                    var voice = source.GetComponent<Voice_Component>();
                    if (voice == null || !Owns(voice.voiceTrans)) continue;
                    found = true; Record(trigger, source, voice.voiceTrans);
                }
                if (!found) Record(trigger + ":no-active-native-voice", null, null);
            }
            catch (Exception ex) { if (Allowed) _log("HOTEL_VOICE_POSITION failed=" + ex.Message); }
        }
        private void Record(string trigger, AudioSource source, Transform mouth)
        {
            if (!Allowed) return;
            try
            {
                var session = _session(); var camera = session.ActiveCamera;
                var cameraTransform = camera != null ? camera.transform : null;
                var soundListener = Manager.Sound.Listener;
                var listeners = UnityEngine.Object.FindObjectsOfType<AudioListener>().Where(item => item != null && item.enabled && item.gameObject.activeInHierarchy).ToArray();
                var sourceTransform = source != null ? source.transform : null;
                _log("HOTEL_VOICE_POSITION trigger=" + trigger + " utc=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                    + " floorPose=" + session.ReadRoomSelection()?.name
                    + " source={" + Pose(sourceTransform) + "} mouth={" + Pose(mouth) + "}"
                    + " clip=" + (source != null && source.clip != null ? source.clip.name : "none")
                    + " channels=" + (source != null && source.clip != null ? source.clip.channels.ToString(CultureInfo.InvariantCulture) : "none")
                    + " panStereo=" + (source != null ? Number(source.panStereo) : "none")
                    + " spatialBlend=" + (source != null ? Number(source.spatialBlend) : "none")
                    + " spatialize=" + (source != null && source.spatialize)
                    + " mixer=" + (source != null && source.outputAudioMixerGroup != null ? source.outputAudioMixerGroup.name : "none")
                    + " nativeCamera={" + Pose(cameraTransform) + "} mainCamera={" + Pose(Camera.main != null ? Camera.main.transform : null) + "}"
                    + " soundListener={" + Pose(soundListener) + "} activeListeners=" + listeners.Length
                    + " sourceFromNativeCamera={" + Relative(cameraTransform, sourceTransform) + "} mouthFromNativeCamera={" + Relative(cameraTransform, mouth) + "}");
                foreach (var listener in listeners)
                    _log("HOTEL_VOICE_LISTENER trigger=" + trigger + " listener={" + Pose(listener.transform) + "}"
                        + " equalsNativeCamera=" + (listener.transform == cameraTransform)
                        + " equalsSoundListener=" + (listener.transform == soundListener)
                        + " attachedCamera=" + (listener.GetComponent<Camera>() != null)
                        + " sourceRelative={" + Relative(listener.transform, sourceTransform) + "} mouthRelative={" + Relative(listener.transform, mouth) + "}");
            }
            catch (Exception ex) { if (Allowed) _log("HOTEL_VOICE_POSITION failed=" + ex.Message); }
        }
        private static string Number(float value) => value.ToString("0.######", CultureInfo.InvariantCulture);
        private static string Vector(Vector3 value) => "(" + Number(value.x) + "," + Number(value.y) + "," + Number(value.z) + ")";
        private static string Pose(Transform value)
        {
            if (value == null) return "none";
            var path = value.name;
            for (var parent = value.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            return "path=" + path + " pos=" + Vector(value.position) + " rot=" + Vector(value.eulerAngles);
        }
        private static string Relative(Transform listener, Transform source)
        {
            if (listener == null || source == null) return "none";
            var delta = source.position - listener.position;
            return "right_m=" + Number(Vector3.Dot(delta, listener.right)) + " up_m=" + Number(Vector3.Dot(delta, listener.up))
                + " forward_m=" + Number(Vector3.Dot(delta, listener.forward)) + " distance_m=" + Number(delta.magnitude);
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (ReferenceEquals(_current, this)) _current = null;
            if (_harmony != null && _play != null) _harmony.Unpatch(_play, HarmonyPatchType.All, HarmonyId);
        }
    }

}
