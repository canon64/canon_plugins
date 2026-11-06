using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MainGameAiShoujoH.Contracts;
using UnityEngine;
using UnityEngine.Networking;

namespace MainGameAiShoujoH.Presentation
{
    public enum VoiceKind { Breath, ShortBreath, Voice, StartVoice, None }
    public sealed class VoiceRequest
    {
        public int Slot;
        public bool KksNative;
        public int KksFaceId = -1, KksVoiceAction;
        public VoiceKind Kind;
        public string Bundle, Asset, Group, LocalFile, State;
        public float Pitch = 1, Volume = 1;
        public bool Protect, Force, Loop, OnlyOnce;
        public int HowTalk;
        public FaceMapping Face;
        public NativeFaceRequest NativeFace;
        public Action Completed;
    }
    public sealed class FaceMapping
    {
        public int Eyes, Brows, Mouth;
        public string Evidence;
        public bool Confirmed;
    }
    /// <summary>Owns only session audio objects and actor face/lip state; never stops shared personality audio.</summary>
    public sealed partial class PresentationModule : ISessionModule
    {
        private sealed class Slot
        {
            internal ChaControl Actor;
            internal AudioSource Audio, PreviousLip;
            internal GameObject Root;
            internal VoiceRequest Current;
            internal Coroutine Completion;
            internal UnityWebRequest Request;
            internal AudioClip OwnedClip;
            internal int Eyes, Brows, Mouth, EyesLook, NeckLook, NeckTargetType;
            internal Transform NeckTarget;
            internal float NeckRate;
            internal string LastBreathState, LastVoiceState;
            internal NativeFaceLease FaceLease;
            internal KksNativeFaceLease KksFaceLease;
            internal byte[] Clothes;
            internal bool ClothesChanged;
            internal readonly List<VoiceRequest> Pending = new List<VoiceRequest>();
        }
        private readonly string catalogDirectory;
        private readonly Integration.NativeKksSessionRuntime nativeRuntime;
        private NativeAudioCatalog catalog;
        private KksSexyVoicePlayback kksVoice;
        private bool NativeKks => context?.Request.KksPose != null;
        private bool NativeEngineOwnsH => nativeRuntime?.Engine != null;
        private readonly HashSet<string> preparedEndingVoices = new HashSet<string>(StringComparer.Ordinal);
        private NativeSePresenter soundEffects;
        private NativeParticlePresenter particles;
        private NativeItemPresenter items;
        private NativeAppearanceLease appearance;
        private readonly List<Slot> slots = new List<Slot>();
        private SessionContext context;
        private bool disposed;
        private bool dialog;
        private bool finalizing;
        private bool completedNormally;
        private string currentState;
        private int currentActionMode, currentActionKind;
        public string Name => "presentation";
        public bool Dialog => dialog;
        internal void PlayKksRequest(string request, string state, Action completed)
        {
            if (kksVoice == null) throw new InvalidOperationException("Native KKS voice is not prepared.");
            var voice = kksVoice.OrdinaryRequest(request, state);
            if (voice == null) { completed?.Invoke(); return; }
            voice.Protect = true; voice.Completed = completed; Queue(voice);
        }
        public event Action<int> VoiceCompleted;
        public PresentationModule(string catalogDirectory) { this.catalogDirectory = catalogDirectory; }
        internal PresentationModule(string catalogDirectory, Integration.NativeKksSessionRuntime runtime) : this(catalogDirectory) { nativeRuntime = runtime; }
        public void Prepare(SessionContext value)
        {
            context = value ?? throw new ArgumentNullException(nameof(value));
            appearance = new NativeAppearanceLease(context);
            if (context.Request.UseKksOnePoseTrial)
            {
                foreach (var participant in context.Request.Participants)
                {
                    if (participant.Role == "male") continue;
                    if (participant.Role != "female" || slots.Count != 0) throw new InvalidDataException("KKS sexy session requires one female");
                    var actor = participant.Character;
                    var slot = new Slot { Actor = actor, PreviousLip = actor.asVoice, Eyes = actor.fileStatus.eyesPtn, Brows = actor.fileStatus.eyebrowPtn, Mouth = actor.fileStatus.mouthPtn, EyesLook = actor.fileStatus.eyesLookPtn, Clothes = (byte[])actor.fileStatus.clothesState.Clone(), NeckLook = actor.fileStatus.neckLookPtn, NeckTargetType = actor.fileStatus.neckTargetType, NeckTarget = actor.neckLookCtrl?.target, NeckRate = actor.neckLookCtrl == null ? 1 : actor.neckLookCtrl.rate };
                    slots.Add(slot);
                    slot.Root = new GameObject("KKS sexy session audio " + context.Id);
                    slot.Root.transform.SetParent(actor.transform, false); slot.Audio = slot.Root.AddComponent<AudioSource>();
                    slot.Audio.playOnAwake = false; slot.Audio.spatialBlend = 1; slot.Audio.rolloffMode = AudioRolloffMode.Linear;
                    slot.KksFaceLease = new KksNativeFaceLease(actor);
                    if (!NativeEngineOwnsH) kksVoice = new KksSexyVoicePlayback(context, actor, nativeRuntime);
                }
                if (slots.Count == default(int)) throw new InvalidDataException("KKS sexy female actor is missing");
                // AI ending dialogue remains exact content audio only. It does
                // not create an AI face, skeleton, animation, or H voice selector.
                catalog = new NativeAudioCatalog(Path.Combine(catalogDirectory, "catalog.json"));
                return;
            }
            if (string.IsNullOrWhiteSpace(catalogDirectory) || !Directory.Exists(catalogDirectory)) throw new DirectoryNotFoundException("Converted AI H audio directory is unavailable.");
            int femaleCount = 0;
            foreach (var participant in context.Request.Participants) if (participant.Role != "male") femaleCount++;
            if (context.Request.VoiceContexts == null || context.Request.VoiceContexts.Length != femaleCount) throw new InvalidDataException("Native AI voice contexts are missing; KKS personality/phase cannot be treated as AI equivalents.");
            foreach (var voiceContext in context.Request.VoiceContexts) voiceContext.Validate();
            catalog = new NativeAudioCatalog(Path.Combine(catalogDirectory, "catalog.json"));
            foreach (var voiceContext in context.Request.VoiceContexts) catalog.ValidateProfile(voiceContext);
            soundEffects = new NativeSePresenter(context, catalog, catalogDirectory);
            items = new NativeItemPresenter(context, Path.Combine(Path.GetDirectoryName(catalogDirectory), "HItems"));
            items.Select(MainGameAiShoujoH.Progression.NativeProcCatalog.Get(context.Request.MotionId));
            particles = new NativeParticlePresenter(context, Path.Combine(Path.GetDirectoryName(catalogDirectory), "HParticles"), bone => items.Anchor(default(int), bone));
            foreach (var participant in context.Request.Participants)
            {
                if (participant.Role == "male") continue;
                var actor = participant.Character;
                if (actor == null) throw new InvalidOperationException("AI H presentation actor is unavailable.");
                var slot = new Slot { Actor = actor, PreviousLip = actor.asVoice, Eyes = actor.fileStatus.eyesPtn, Brows = actor.fileStatus.eyebrowPtn, Mouth = actor.fileStatus.mouthPtn, EyesLook = actor.fileStatus.eyesLookPtn, Clothes = (byte[])actor.fileStatus.clothesState.Clone(), NeckLook = actor.fileStatus.neckLookPtn, NeckTargetType = actor.fileStatus.neckTargetType, NeckTarget = actor.neckLookCtrl?.target, NeckRate = actor.neckLookCtrl == null ? 1 : actor.neckLookCtrl.rate };
                slots.Add(slot);
                slot.FaceLease = new NativeFaceLease(actor, Path.Combine(Path.GetDirectoryName(catalogDirectory), "HFace"));
                slot.Root = new GameObject("AI H audio " + context.Id.ToString("N") + " " + participant.Identity);
                slot.Root.transform.SetParent(actor.transform, false);
                slot.Audio = slot.Root.AddComponent<AudioSource>();
                slot.Audio.playOnAwake = false; slot.Audio.spatialBlend = 1; slot.Audio.rolloffMode = AudioRolloffMode.Linear;
            }
        }
        public ParticleRenderState CaptureParticleRenderState() => particles?.CaptureRenderState() ?? new ParticleRenderState { ShaderSupportVerified = false, MaterialShaders = new string[0] };
        public bool ItemsReady(MainGameAiShoujoH.Progression.ProcDefinition definition) => NativeKks || items.IsReady(definition);
        public void SelectItems(MainGameAiShoujoH.Progression.ProcDefinition definition) { if (!NativeKks) items.Select(definition); }
        public void SetItemSpeed(float speed) => items?.Speed(speed);
        public void SuspendSelectionAudio() { foreach (var slot in slots) { StopSlot(slot); slot.Pending.Clear(); } }
        public void PlayStart(int timing, Action completed)
        {
            if (disposed || context == null || !context.IsCurrent()) return;
            if (kksVoice != null)
            {
                var native = kksVoice.Ordinary("Idle");
                if (native == null) { completed(); return; }
                native.Kind = VoiceKind.StartVoice; native.Completed = completed; Queue(native); return;
            }
            var contexts = context.Request.VoiceContexts;
            void Main(int value)
            {
                var request = catalog.SelectStart(contexts[0], value, n => UnityEngine.Random.Range(0, n));
                if (request == null) { completed(); return; }
                request.Completed = completed; Queue(request);
            }
            if (contexts.Length > 1)
            {
                var request = catalog.SelectStart(contexts[1], timing, n => UnityEngine.Random.Range(0, n));
                if (request != null) { request.Completed = () => Main(timing >= 1 && timing <= 3 ? timing + 4 : timing); Queue(request); return; }
            }
            Main(timing);
        }
        public void PlayShort(int actionMode, int actionKind, int requestKind)
        {
            if (disposed || context == null || !context.IsCurrent()) return;
            if (kksVoice != null) return; // No AI short-breath table is consulted in the native KKS session.
            foreach (var voice in context.Request.VoiceContexts)
            {
                var request = catalog.SelectShort(voice, actionMode, actionKind, requestKind, n => UnityEngine.Random.Range(0, n));
                if (request != null) Queue(request);
            }
        }
        public void NextClothes(string actor, int part) => appearance.NextClothes(actor, part);
        public void AllClothes(string actor, int state) => appearance.AllClothes(actor, state);
        public void SetAccessory(string actor, int part, bool visible) => appearance.Accessory(actor, part, visible);
        public void AllAccessories(string actor, bool visible) => appearance.AllAccessories(actor, visible);
        public void ToggleSiru(string actor) => appearance.ToggleSiru(actor);
        public void StopAtLoopChange(int count, bool requireVisible, bool requireBodyBone)
        {
            for (int i = 0; i < Math.Min(count, slots.Count); i++)
            {
                var slot = slots[i];
                if ((!requireVisible || slot.Actor.visibleAll) && (!requireVisible || (requireBodyBone ? slot.Actor.objBodyBone != null : slot.Actor.objTop != null))
                    && (slot.Current?.Kind == VoiceKind.Voice || slot.Current?.Kind == VoiceKind.StartVoice)) { StopSlot(slot); slot.Pending.Clear(); }
            }
            RefreshBreath();
        }
        public void PlayParticle(int id) { if (kksVoice != null) throw new NotSupportedException("KKS native particle effects are not connected"); particles.Play(id); }
        public void SetLowerClothes(int slot, int state)
        {
            if (disposed || context == null || !context.IsCurrent()) return;
            if (slot < 0 || slot >= slots.Count || state < 0 || state > 2) throw new ArgumentOutOfRangeException();
            slots[slot].ClothesChanged = true;
            GlobalMethod.SetAllClothState(slots[slot].Actor, false, state, true);
        }
        public void PlaySoundEffect(int cueId, string motionId, string state) => soundEffects?.Play(cueId, motionId, state);
        public void PlayOrdinary(string state, bool requested, int actionMode, bool femaleInitiative)
        {
            if (NativeEngineOwnsH) return;
            if (disposed || context == null || !context.IsCurrent()) return;
            if (kksVoice != null) { var native = kksVoice.Ordinary(state); if (native != null) Queue(native); return; }
            var contexts = context.Request.VoiceContexts;
            for (int slot = contexts.Length - 1; slot >= 0; slot--)
            {
                var c = contexts[slot];
                var request = catalog.SelectVoice(c, state, requested, dialog, contexts.Length > 1 && IsDialoguePlaying(slot ^ 1), actionMode == 4 && femaleInitiative, n => UnityEngine.Random.Range(0, n));
                if (request == null) continue;
                dialog = request.HowTalk == 1;
                if (request.HowTalk != 0 && contexts.Length > 1)
                    request.Completed = () => PlayOrdinary(state, false, actionMode, femaleInitiative);
                Queue(request);
            }
        }
        public void EnterState(string state, int actionMode, int actionKind, bool femaleInitiative)
        {
            if (NativeEngineOwnsH) return;
            items?.State(state);
            currentState = state; currentActionMode = actionMode; currentActionKind = actionKind;
            if (kksVoice == null) foreach (var c in context.Request.VoiceContexts) c.FemaleInitiative = femaleInitiative;
            PlayOrdinary(state, false, actionMode, femaleInitiative); RefreshBreath();
        }
        private void RefreshBreath()
        {
            if (NativeEngineOwnsH) return;
            if (disposed || context == null || !context.IsCurrent() || currentState == null) return;
            if (kksVoice != null) { var native = kksVoice.Breath(currentState); if (native != null) Queue(native); return; }
            foreach (var c in context.Request.VoiceContexts)
            {
                var request = catalog.SelectBreath(c, currentActionMode, currentActionKind, currentState, n => UnityEngine.Random.Range(0, n));
                if (request != null) Queue(request);
            }
        }
        public bool IsDialoguePlaying(int slot) => slot >= 0 && slot < slots.Count && (slots[slot].Current?.Kind == VoiceKind.Voice || slots[slot].Current?.Kind == VoiceKind.StartVoice);
        public void Queue(VoiceRequest request)
        {
            if (disposed || context == null || !context.IsCurrent()) return;
            if (request == null || request.Slot < 0 || request.Slot >= slots.Count) throw new ArgumentException("Invalid AI H voice slot.");
            if (NativeKks && !request.KksNative && !(finalizing && preparedEndingVoices.Contains(request.Slot + "|" + request.Bundle + "|" + request.Asset))) throw new InvalidDataException("AI audio cannot enter a native KKS sexy session");
            var slot = slots[request.Slot];
            if (request.Face != null && (!request.Face.Confirmed || string.IsNullOrWhiteSpace(request.Face.Evidence))) throw new InvalidDataException("AI/KKS face semantic correspondence has not been confirmed.");
            if (slot.Current?.Protect == true)
            {
                // Native ordinary voice remains pending; protected short breath is consumed.
                if (request.Kind == VoiceKind.Voice || request.Kind == VoiceKind.StartVoice) slot.Pending.Add(request);
                return;
            }
            if (!request.Force && slot.Current != null && (int)slot.Current.Kind > (int)request.Kind) return;
            if (request.Kind == VoiceKind.Breath && request.OnlyOnce && (slot.LastBreathState == request.State || slot.LastVoiceState == request.State)) return;
            if (request.Kind == VoiceKind.Breath && slot.Current?.Kind == VoiceKind.Breath && (slot.Current.Bundle == request.Bundle && slot.Current.Asset == request.Asset || slot.Current.Group == request.Group)) return;
            Play(slot, request);
        }
        private void Play(Slot slot, VoiceRequest request)
        {
            if (request.KksNative)
            {
                if (!NativeKks || !string.IsNullOrEmpty(request.LocalFile)) throw new InvalidDataException("Native KKS voice ownership differs");
                var clip = CommonLib.LoadAsset<AudioClip>(request.Bundle, request.Asset);
                if (clip == null) throw new InvalidDataException("Installed native KKS sexy voice asset missing: " + request.Bundle + "/" + request.Asset);
                StopSlot(slot); BeginClip(slot, request, clip); return;
            }
            if (!string.IsNullOrEmpty(request.LocalFile))
            {
                string localPath = Path.GetFullPath(Path.Combine(catalogDirectory, request.LocalFile));
                string localPrefix = Path.GetFullPath(catalogDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!localPath.StartsWith(localPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(localPath)) throw new FileNotFoundException("Converted native AI voice file is unavailable: " + request.LocalFile);
                StopSlot(slot); slot.Current = request;
                slot.Completion = context.Request.Owner.StartCoroutine(LoadConverted(slot, request, localPath));
                return;
            }
            throw new InvalidDataException("Native AI voice has not been converted for the KKS audio loader: " + request.Bundle + "/" + request.Asset);
        }
        private IEnumerator LoadConverted(Slot slot, VoiceRequest request, string path)
        {
            var type = Path.GetExtension(path).Equals(".ogg", StringComparison.OrdinalIgnoreCase) ? AudioType.OGGVORBIS : AudioType.WAV;
            var load = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, type); slot.Request = load;
            yield return load.SendWebRequest();
            if (disposed || context == null || !context.IsCurrent() && !finalizing || !ReferenceEquals(slot.Current, request)) { load.Dispose(); yield break; }
            try
            {
                if (load.isHttpError || load.isNetworkError) throw new IOException(load.error);
                slot.OwnedClip = DownloadHandlerAudioClip.GetContent(load);
                if (slot.OwnedClip == null) throw new IOException("Converted native voice decode failed: " + request.LocalFile);
                BeginClip(slot, request, slot.OwnedClip);
            }
            catch (Exception ex) { context.ReportFailure("AI H voice: " + ex.Message); }
            finally { if (ReferenceEquals(slot.Request, load)) slot.Request = null; load.Dispose(); }
        }
        private void BeginClip(Slot slot, VoiceRequest request, AudioClip clip)
        {
            if (request.Pitch <= 0 || float.IsNaN(request.Pitch) || float.IsInfinity(request.Pitch)) throw new InvalidDataException("Invalid AI H voice pitch.");
            slot.Current = request; slot.Audio.clip = clip; slot.Audio.pitch = request.Pitch; slot.Audio.volume = Mathf.Clamp01(request.Volume); slot.Audio.loop = request.Loop;
            if (request.KksNative && request.KksFaceId >= 0) slot.KksFaceLease.Apply(request.KksFaceId, request.Kind == VoiceKind.Breath ? 0 : 1, request.KksVoiceAction);
            if (request.Face != null) ApplyFace(slot.Actor, request.Face);
            if (request.NativeFace != null) slot.FaceLease.Apply(request.NativeFace);
            slot.Actor.SetLipSync(slot.Audio); slot.Audio.Play();
            if (request.Kind == VoiceKind.Breath) slot.LastBreathState = request.State;
            if (request.Kind == VoiceKind.Voice) slot.LastVoiceState = request.State;
            if (!request.Loop) slot.Completion = context.Request.Owner.StartCoroutine(Complete(slot, request, clip.length / request.Pitch));
        }
        private IEnumerator Complete(Slot slot, VoiceRequest request, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (disposed || context == null || !context.IsCurrent() && !finalizing || !ReferenceEquals(slot.Current, request)) yield break;
            slot.Current = null; slot.Completion = null;
            request.Completed?.Invoke(); VoiceCompleted?.Invoke(request.Slot);
            if (slot.Pending.Count != 0)
            {
                // Start > ordinary; native evaluation order is independent per actor slot.
                slot.Pending.Sort((a, b) => ((int)b.Kind).CompareTo((int)a.Kind));
                var next = slot.Pending[0]; slot.Pending.RemoveAt(0); Play(slot, next);
            }
            else if (!finalizing) RefreshBreath();
        }
        public void ApplyFace(int slot, FaceMapping face)
        {
            if (disposed || context == null || !context.IsCurrent()) return;
            if (slot < 0 || slot >= slots.Count) throw new ArgumentOutOfRangeException(nameof(slot));
            ApplyFace(slots[slot].Actor, face);
        }
        private static void ApplyFace(ChaControl actor, FaceMapping face)
        {
            if (face == null || !face.Confirmed || string.IsNullOrWhiteSpace(face.Evidence)) throw new InvalidDataException("Unconfirmed AI/KKS face mapping.");
            actor.ChangeEyesPtn(face.Eyes); actor.ChangeEyebrowPtn(face.Brows); actor.ChangeMouthPtn(face.Mouth);
        }
        private void StopSlot(Slot slot)
        {
            if (slot.Request != null) { slot.Request.Abort(); slot.Request.Dispose(); slot.Request = null; }
            if (slot.Completion != null && context?.Request.Owner != null) context.Request.Owner.StopCoroutine(slot.Completion);
            slot.Completion = null; slot.Current = null; if (slot.Audio != null) slot.Audio.Stop();
            if (slot.OwnedClip != null) { if (slot.Audio != null) slot.Audio.clip = null; UnityEngine.Object.Destroy(slot.OwnedClip); slot.OwnedClip = null; }
        }
        public void Execute(Command command) { }
        public void BeginEnding()
        {
            if (disposed || context == null) throw new InvalidOperationException("AI H presentation has been released.");
            if (!NativeKks) LoadEndingExpressions();
            finalizing = true;
            foreach (var slot in slots) { StopSlot(slot); slot.Pending.Clear(); }
        }
        public void EndEnding() { HideEndingText(); finalizing = false; foreach (var slot in slots) StopSlot(slot); }
        public void StopEndingVoice(int slot)
        {
            if (!finalizing || disposed) throw new InvalidOperationException("ADV voice requires an active ending lease.");
            if (slot < 0 || slot >= slots.Count) throw new ArgumentOutOfRangeException(nameof(slot));
            StopSlot(slots[slot]); slots[slot].Pending.Clear();
        }
        public void PlayEndingVoice(int slot, string bundle, string asset, Action completed)
        {
            if (NativeKks && !preparedEndingVoices.Contains(slot + "|" + bundle + "|" + asset)) throw new InvalidDataException("Exact AI ending content voice was not validated during preparation");
            if (!finalizing || disposed) throw new InvalidOperationException("ADV voice requires an active ending lease.");
            if (slot < 0 || slot >= slots.Count) throw new ArgumentOutOfRangeException(nameof(slot));
            Play(slots[slot], new VoiceRequest { Slot = slot, Kind = VoiceKind.StartVoice, Bundle = bundle, Asset = asset,
                LocalFile = catalog.VoiceFile(bundle, asset), Force = true, Protect = true, Completed = completed });
        }
        public void ValidateEndingVoice(int slot, string bundle, string asset)
        {
            if (disposed || context == null || slot < 0 || slot >= slots.Count) throw new InvalidOperationException("Ending content voice preparation ownership differs");
            string relative = catalog.VoiceFile(bundle, asset);
            string path = Path.GetFullPath(Path.Combine(catalogDirectory, relative));
            string prefix = Path.GetFullPath(catalogDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) throw new FileNotFoundException("Exact ending content voice file is missing", path);
            preparedEndingVoices.Add(slot + "|" + bundle + "|" + asset);
        }
        public void PlayNativeEndingVoice(int slot, string bundle, string asset, Action completed)
        {
            if (!NativeKks || !finalizing || disposed || slot < 0 || slot >= slots.Count) throw new InvalidOperationException("Native KKS ending voice requires its active female lease");
            string nativePath = (bundle ?? string.Empty).Replace('\\', '/');
            if (!nativePath.StartsWith("sound/data/pcm/c00/", StringComparison.OrdinalIgnoreCase) || nativePath.Contains("..") || string.IsNullOrWhiteSpace(asset)) throw new InvalidDataException("Native KKS c00 scenario voice reference is invalid");
            Play(slots[slot], new VoiceRequest { Slot = slot, Kind = VoiceKind.StartVoice, Bundle = nativePath, Asset = asset, KksNative = true, Protect = true, Force = true, Completed = completed });
        }
        public void End(EndReason reason)
        {
            if (reason != EndReason.Completed) { Dispose(); return; }
            completedNormally = true; appearance?.Complete();
            foreach (var actor in context.Request.Participants)
                try { NormalizeNativeCompleted(actor.Character); } catch (Exception ex) { context.ReportFailure("AI H normal actor cleanup: " + ex.Message); }
            foreach (var slot in slots) { StopSlot(slot); slot.Pending.Clear(); }
            soundEffects?.Dispose();
        }
        public static void NormalizeNativeCompleted(ChaControl actor)
        {
            if (actor == null) return;
            // Original H EndProc normalizes status only. Coordinate/card and extension array sizes remain intact.
            var errors = new List<Exception>();
            void Apply(Action action) { try { action(); } catch (Exception ex) { errors.Add(ex); } }
            for (int i = 0; i < actor.fileStatus.clothesState.Length; i++) { int index = i; Apply(() => actor.SetClothesState(index, 0)); }
            for (int i = 0; i < actor.fileStatus.showAccessory.Length; i++) { int index = i; Apply(() => actor.SetAccessoryState(index, true)); }
            for (int i = 0; i < actor.fileStatus.siruLv.Length; i++) { int index = i; Apply(() => actor.SetSiruFlags((ChaFileDefine.SiruParts)index, 0)); }
            if (errors.Count != 0) throw new AggregateException("Native normal H status cleanup failed.", errors);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            ReleaseEndingText();
            try { appearance?.Dispose(); } catch (Exception ex) { context?.ReportFailure("AI H appearance restore: " + ex.Message); }
            try { items?.Dispose(); } catch (Exception ex) { context?.ReportFailure("AI H item restore: " + ex.Message); }
            try { particles?.Dispose(); } catch (Exception ex) { context?.ReportFailure("AI H particle restore: " + ex.Message); }
            try { soundEffects?.Dispose(); } catch (Exception ex) { context?.ReportFailure("AI H SE restore: " + ex.Message); }
            foreach (var slot in slots)
            {
                void Restore(Action restore) { try { restore(); } catch (Exception ex) { context?.ReportFailure("AI H face/audio restore: " + ex.Message); } }
                Restore(() => StopSlot(slot)); slot.Pending.Clear(); Restore(() => slot.FaceLease?.Dispose()); Restore(() => slot.KksFaceLease?.Dispose());
                if (slot.Actor != null)
                {
                    if (slot.ClothesChanged && !completedNormally)
                        for (int i = 0; i < slot.Clothes.Length; i++) { int index = i; Restore(() => slot.Actor.SetClothesState(index, slot.Clothes[index])); }
                    Restore(() => slot.Actor.ChangeEyesPtn(slot.Eyes)); Restore(() => slot.Actor.ChangeEyebrowPtn(slot.Brows));
                    Restore(() => slot.Actor.ChangeMouthPtn(slot.Mouth)); Restore(() => slot.Actor.ChangeLookEyesPtn(slot.EyesLook));
                    Restore(() => slot.Actor.ChangeLookNeckPtn(slot.NeckLook, slot.NeckRate));
                    Restore(() => { slot.Actor.fileStatus.neckTargetType = slot.NeckTargetType; if (slot.Actor.neckLookCtrl != null) slot.Actor.neckLookCtrl.target = slot.NeckTarget; }); Restore(() => { if (slot.Actor.asVoice == slot.Audio) slot.Actor.SetLipSync(slot.PreviousLip); });
                }
                Restore(() => { if (slot.Root != null) UnityEngine.Object.Destroy(slot.Root); });
            }
            slots.Clear(); preparedEndingVoices.Clear(); kksVoice?.Dispose(); kksVoice = null; context = null;
        }
    }
}
