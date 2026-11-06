using System;
using System.Runtime.Serialization;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MainGameAiShoujoH.Contracts;
using UnityEngine;
using UnityEngine.Networking;

namespace MainGameAiShoujoH.Presentation
{
    #pragma warning disable CS0649
    [DataContract] internal sealed class SeCue
    {
        [DataMember] public int id, actor;
        [DataMember] public string table, state, parent, bundle, motionId, role, anchorRole, femaleRole;
        [DataMember] public float phase;
        [DataMember] public bool loop, loopOn, changeVolume, isShorts;
        [DataMember] public string[] names, shorts;
    }
    [DataContract] internal sealed class SeCueData { [DataMember] public SeCue[] cues; }
    #pragma warning restore CS0649
    internal sealed class NativeSePresenter : IDisposable
    {
        private sealed class Source
        {
            internal GameObject Root;
            internal AudioSource Audio;
            internal AudioClip Clip;
            internal UnityWebRequest Load;
            internal Coroutine Routine;
            internal string Name, Parent;
            internal Transform Anchor;
        }
        private readonly SessionContext context;
        private readonly NativeAudioCatalog audio;
        private readonly Dictionary<int, SeCue> cues;
        private readonly Dictionary<string, Dictionary<string, Transform>> bones = new Dictionary<string, Dictionary<string, Transform>>(StringComparer.Ordinal);
        private readonly List<Source> sources = new List<Source>();
        private bool disposed;
        internal NativeSePresenter(SessionContext context, NativeAudioCatalog audio, string directory)
        {
            this.context = context; this.audio = audio;
            var data = MainGameAiShoujoH.Motion.DedicatedMotionModule.ReadCatalog<SeCueData>(Path.Combine(directory, "se-cues.json"));
            if (data?.cues == null) throw new InvalidDataException("Native H SE cue catalog is unavailable.");
            cues = data.cues.ToDictionary(c => c.id);
            foreach (var actor in context.Request.Participants)
            {
                var map = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
                foreach (var bone in actor.Character.GetComponentsInChildren<Transform>(true)) if (!map.ContainsKey(bone.name)) map.Add(bone.name, bone);
                bones.Add(actor.Role, map);
            }
        }
        internal void Play(int id, string motionId, string state)
        {
            if (disposed || !context.IsCurrent()) return;
            if (!cues.TryGetValue(id, out var cue)) throw new InvalidDataException("Native H SE cue ID is missing: " + id);
            if (cue.motionId != motionId || cue.state != state) return;
            if (!bones.TryGetValue(cue.anchorRole, out var actor) || !actor.TryGetValue(cue.parent, out var parent)) throw new InvalidDataException("Native H SE parent is unavailable: " + cue.anchorRole + ":" + cue.parent);
            if (cue.loop && !cue.loopOn)
            {
                foreach (var source in sources.Where(s => s.Anchor == parent && cue.names.Contains(s.Name)).ToArray()) Release(source);
                return;
            }
            if (cue.names == null || cue.names.Length == 0) throw new InvalidDataException("Native H SE names are empty.");
            var selectedNames = cue.names;
            if (cue.isShorts && cue.shorts != null && cue.shorts.Length > 0)
            {
                var female = context.Request.Participants.FirstOrDefault(p => p.Role == cue.femaleRole);
                if (female == null) throw new InvalidDataException("Native SE clothing participant is unavailable: " + cue.femaleRole);
                if (female.Character.IsKokanHide()) selectedNames = cue.shorts;
            }
            string name = selectedNames[UnityEngine.Random.Range(0, selectedNames.Length)];
            if (cue.loop && sources.Any(s => s.Name == name && s.Anchor == parent)) return;
            string file = audio.EffectFile(name);
            var root = new GameObject("AI H SE " + name); root.transform.SetParent(parent, false);
            var sourceNew = new Source { Root = root, Audio = root.AddComponent<AudioSource>(), Name = name, Parent = cue.parent, Anchor = parent };
            sourceNew.Audio.playOnAwake = false; sourceNew.Audio.spatialBlend = 1; sourceNew.Audio.rolloffMode = AudioRolloffMode.Linear;
            sources.Add(sourceNew); sourceNew.Routine = context.Request.Owner.StartCoroutine(Load(sourceNew, file, cue.loop));
        }
        private IEnumerator Load(Source source, string file, bool loop)
        {
            source.Load = UnityWebRequestMultimedia.GetAudioClip(new Uri(file).AbsoluteUri, Path.GetExtension(file) == ".ogg" ? AudioType.OGGVORBIS : AudioType.WAV);
            yield return source.Load.SendWebRequest();
            if (disposed || !context.IsCurrent() || !sources.Contains(source)) yield break;
            try
            {
                if (source.Load.isHttpError || source.Load.isNetworkError) throw new IOException(source.Load.error);
                source.Clip = DownloadHandlerAudioClip.GetContent(source.Load);
                if (source.Clip == null) throw new IOException("Native H SE decode failed.");
                source.Audio.clip = source.Clip; source.Audio.loop = loop; source.Audio.Play();
            }
            catch (Exception ex) { context.ReportFailure("AI H SE: " + ex.Message); }
            finally { source.Load.Dispose(); source.Load = null; }
            if (loop || source.Clip == null) yield break;
            yield return new WaitForSeconds(source.Clip.length);
            source.Routine = null; Release(source);
        }
        private void Release(Source source)
        {
            if (source.Load != null) { source.Load.Abort(); source.Load.Dispose(); source.Load = null; }
            if (source.Routine != null && context.Request.Owner != null) context.Request.Owner.StopCoroutine(source.Routine);
            if (source.Audio != null) source.Audio.Stop();
            if (source.Root != null) UnityEngine.Object.Destroy(source.Root);
            if (source.Clip != null) UnityEngine.Object.Destroy(source.Clip);
            sources.Remove(source);
        }
        public void Dispose() { if (disposed) return; disposed = true; foreach (var source in sources.ToArray()) Release(source); bones.Clear(); }
    }
}
