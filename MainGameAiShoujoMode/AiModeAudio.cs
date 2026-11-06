using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapEnvironmentRuntime;
using UnityEngine;
using UnityEngine.Networking;

namespace MainGameAiShoujoMode
{
    public sealed class AiModeAudio : MonoBehaviour
    {
        public sealed class Clip { public string id, path, sha256; }
        public sealed class Area { public int areaId; public string name, dayClip, nightClip; public float dayStartHour, nightStartHour; }
        public sealed class Volume { public int areaId; public Vector3 center, size; }
        public sealed class Cue
        {
            public string name; public string[] clips; public Vector3[] points;
            public bool line, morning, day, night, clear, cloud, rain, fog, loop;
            public float intervalMin, intervalMax, minDistance, maxDistance;
        }
        public sealed class Defaults
        {
            public float bgmFadeSeconds, environmentFadeSeconds, volume, pitch, spatialBlend;
            public float morningStartHour, dayStartHour, nightStartHour;
        }
        public sealed class Manifest { public Clip[] clips; public Area[] bgmAreas; public Volume[] areaVolumes; public Cue[] environmentSources; public Defaults settings; }
        private sealed class Voice
        {
            public AudioSource source; public Cue cue; public bool desired, music; public float nextPlay;
            public Vector3[] points;
            public Vector3[] sourcePoints;
            public bool resumePlaying;
        }
        private sealed class MusicLease : IDisposable
        {
            internal Action Release;
            public void Dispose() { var release = Release; Release = null; release?.Invoke(); }
        }
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly List<Voice> _voices = new List<Voice>();
        private readonly List<Voice> _suspendedMusic = new List<Voice>();
        private readonly HashSet<MusicLease> _musicLeases = new HashSet<MusicLease>();
        private Manifest _manifest;
        private SandboxHost _host;
        private Action<string> _log;
        private string _music;
        private Area _area;
        private float _nextCondition;
        private bool _ready;
        private bool _destroyed;
        private Parameters P => _host.Session.Settings;
        private float A(string key) => P.N("aiMode.audio." + key);
        public IEnumerator Initialize(SandboxHost host, string content, Action<string> log)
        {
            _host = host; _log = log;
            string directory = Path.GetFullPath(Path.Combine(content, P.T("aiMode.audio.directory")));
            _manifest = JsonCodec.FromJson<Manifest>(File.ReadAllText(Path.Combine(directory, "manifest.json")));
            if (_manifest?.clips == null || _manifest.settings == null || _manifest.bgmAreas == null || _manifest.environmentSources == null)
                throw new InvalidDataException("AI少女の音声manifestが不完全です。");
            foreach (var entry in _manifest.clips)
            {
                string path = Path.GetFullPath(Path.Combine(directory, entry.path));
                if (!path.StartsWith(directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Audio path outside directory");
                using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, AudioType.WAV))
                {
                    yield return request.SendWebRequest();
                    if (request.isNetworkError || request.isHttpError) throw new IOException(entry.id + ": " + request.error);
                    var clip = DownloadHandlerAudioClip.GetContent(request);
                    if (clip == null) throw new InvalidDataException("Audio decode failed: " + entry.id);
                    clip.name = entry.id; _clips.Add(entry.id, clip);
                }
            }
            foreach (var cue in _manifest.environmentSources)
            {
                if (cue.points == null || !cue.points.Any() || cue.clips == null || !cue.clips.Any() || cue.clips.Any(id => !_clips.ContainsKey(id)))
                    throw new InvalidDataException("Environment cue is incomplete: " + cue.name);
                var points = cue.points;
                if (cue.line) AddVoice(cue, points);
                else foreach (var point in points) AddVoice(cue, new[] { point });
            }
            _ready = true;
            _log("AI_SHOUJO_AUDIO_READY clips=" + _clips.Count + " ambientSources=" + _voices.Count);
        }
        private AudioSource Source(string label, bool music)
        {
            var node = new GameObject(label); node.transform.SetParent(transform, false);
            var source = node.AddComponent<AudioSource>(); source.playOnAwake = false;
            source.volume = P.N("math.zero"); source.pitch = A("pitch");
            source.spatialBlend = music ? P.N("math.zero") : A("spatialBlend");
            source.rolloffMode = AudioRolloffMode.Linear;
            source.dopplerLevel = P.N("math.zero");
            return source;
        }
        private void AddVoice(Cue cue, Vector3[] points)
        {
            var sourcePoints = points; points = points.Select(_host.FromIslandSourcePosition).ToArray();
            var source = Source(cue.name, false); source.loop = cue.loop;
            source.minDistance = cue.minDistance * P.N("island.scale"); source.maxDistance = cue.maxDistance * P.N("island.scale");
            source.transform.position = points.First();
            _voices.Add(new Voice { source = source, cue = cue, points = points, sourcePoints = sourcePoints });
        }
        private bool Matches(Cue cue)
        {
            float hour = _host.Session.World.Hour;
            bool time = hour < A("morningStartHour") || hour >= A("nightStartHour") ? cue.night : hour < A("dayStartHour") ? cue.morning : cue.day;
            string weather = _host.Session.World.Weather;
            return time && (weather == "clear" ? cue.clear : cue.cloud);
        }
        private void Conditions(Vector3 listener)
        {
            Area area = null;
            var groundPosition = _host.ToIslandSourcePosition(listener - Vector3.up * (P.N("movement.bodyHeight") - P.N("movement.bodyRadius")));
            if (_manifest.areaVolumes != null)
                foreach (var volume in _manifest.areaVolumes.OrderBy(item => item.size.x * item.size.y * item.size.z))
                {
                    var bounds = new Bounds(volume.center, volume.size);
                    if (bounds.Contains(groundPosition))
                    {
                        if (area == null || (_area != null && volume.areaId == _area.areaId)) area = _manifest.bgmAreas.FirstOrDefault(item => item.areaId == volume.areaId);
                        if (_area != null && volume.areaId == _area.areaId) break;
                    }
                }
            if (area == null) area = _area ?? _manifest.bgmAreas.First();
            _area = area;
            float hour = _host.Session.World.Hour;
            string id = hour >= area.dayStartHour && hour < area.nightStartHour ? area.dayClip : area.nightClip;
            if (_music != id)
            {
                if (!_clips.ContainsKey(id)) throw new InvalidDataException("BGM clip missing: " + id);
                foreach (var voice in _voices.Concat(_suspendedMusic).Where(item => item.music)) voice.desired = false;
                var source = Source("Island BGM " + id, true); source.clip = _clips[id]; source.loop = true;
                var next = new Voice { source = source, music = true, desired = true };
                if (_musicLeases.Count == P.I("math.zero")) { source.Play(); _voices.Add(next); }
                else _suspendedMusic.Add(next);
                _music = id;
                _log("AI_SHOUJO_BGM " + area.name + " " + id);
            }
            foreach (var voice in _voices.Where(item => !item.music))
            {
                voice.points = voice.sourcePoints.Select(_host.FromIslandSourcePosition).ToArray();
                voice.source.transform.position = Closest(listener, voice.points, P); voice.desired = false;
            }
            foreach (var voice in _voices.Where(item => !item.music && Matches(item.cue) && Vector3.Distance(listener, item.source.transform.position) < item.source.maxDistance)
                .OrderBy(item => (item.source.transform.position - listener).sqrMagnitude).Take(P.I("aiMode.audio.environmentMaxCount"))) voice.desired = true;
        }
        private static Vector3 Closest(Vector3 listener, Vector3[] points, Parameters parameters)
        {
            Vector3 best = points.First(); float distance = (best - listener).sqrMagnitude;
            for (int index = parameters.I("math.one"); index < points.Length; index++)
            {
                Vector3 start = points[index - parameters.I("math.one")], delta = points[index] - start;
                Vector3 candidate = delta.sqrMagnitude > parameters.N("render.epsilon") ? start + delta * Mathf.Clamp01(Vector3.Dot(listener - start, delta) / delta.sqrMagnitude) : start;
                float next = (candidate - listener).sqrMagnitude;
                if (next < distance) { best = candidate; distance = next; }
            }
            return best;
        }
        private void Update()
        {
            if (!_ready || _host.Session == null || _host.Walker?.ViewCamera == null) return;
            Vector3 listener = _host.Walker.ViewCamera.transform.position;
            try
            {
                if (Time.unscaledTime >= _nextCondition) { Conditions(listener); _nextCondition = Time.unscaledTime + P.N("aiMode.audio.conditionInterval"); }
                foreach (var voice in _voices.ToArray())
                {
                    var source = voice.source;
                    if (!voice.music)
                    {
                        source.transform.position = Closest(listener, voice.points, P);
                        bool nearby = Vector3.Distance(listener, source.transform.position) < source.maxDistance;
                        if (voice.desired && nearby && !source.isPlaying && Time.unscaledTime >= voice.nextPlay)
                        {
                            source.clip = _clips[voice.cue.clips[UnityEngine.Random.Range(P.I("math.zero"), voice.cue.clips.Length)]];
                            source.Play(); voice.nextPlay = Time.unscaledTime + source.clip.length + UnityEngine.Random.Range(voice.cue.intervalMin, voice.cue.intervalMax);
                        }
                    }
                    float fade = A(voice.music ? "bgmFadeSeconds" : "environmentFadeSeconds");
                    float gain = A(voice.music ? "bgmGain" : "environmentGain") * A("volume");
                    source.pitch = A("pitch");
                    source.volume = Mathf.MoveTowards(source.volume, voice.desired ? gain : P.N("math.zero"), Time.unscaledDeltaTime * P.N("math.one") / Mathf.Max(P.N("render.epsilon"), fade));
                    if (!voice.desired && source.volume <= P.N("math.zero"))
                    {
                        source.Stop();
                        if (voice.music) { Destroy(source.gameObject); _voices.Remove(voice); }
                    }
                }
            }
            catch (Exception ex) { _ready = false; foreach (var voice in _voices.Concat(_suspendedMusic)) if (voice.source != null) voice.source.Stop(); Debug.LogError("AI_SHOUJO_AUDIO_FAILED " + ex); }
        }
        internal IDisposable SuspendMusic()
        {
            if (_destroyed || !_ready) throw new InvalidOperationException("AI mode island audio is not ready.");
            var lease = new MusicLease();
            if (_musicLeases.Count == P.I("math.zero"))
            {
                foreach (var voice in _voices.Where(item => item.music).ToArray())
                {
                    voice.resumePlaying = voice.source != null && voice.source.isPlaying;
                    if (voice.resumePlaying) voice.source.Pause();
                    _voices.Remove(voice); _suspendedMusic.Add(voice);
                }
            }
            _musicLeases.Add(lease);
            lease.Release = () => ResumeMusic(lease);
            return lease;
        }
        private void ResumeMusic(MusicLease lease)
        {
            if (_destroyed || !_musicLeases.Remove(lease) || _musicLeases.Count != P.I("math.zero")) return;
            foreach (var voice in _suspendedMusic)
            {
                _voices.Add(voice);
                if (_ready && voice.source != null)
                {
                    if (voice.resumePlaying) voice.source.UnPause();
                    else if (voice.desired) voice.source.Play();
                }
                voice.resumePlaying = false;
            }
            _suspendedMusic.Clear();
        }
        private void OnDestroy()
        {
            _destroyed = true; _ready = false;
            foreach (var lease in _musicLeases) lease.Release = null;
            _musicLeases.Clear();
            foreach (var voice in _voices.Concat(_suspendedMusic)) if (voice.source != null) { voice.source.Stop(); Destroy(voice.source.gameObject); }
            _voices.Clear(); _suspendedMusic.Clear();
            foreach (var clip in _clips.Values) if (clip != null) Destroy(clip);
        }
    }
}
