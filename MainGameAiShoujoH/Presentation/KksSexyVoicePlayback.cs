using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MainGameAiShoujoH.Contracts;
using UnityEngine;

namespace MainGameAiShoujoH.Presentation
{
    // Session-only c00 selector. Native dictionaries and compiled condition/
    // experience selectors are reused; no card or saved Heroine is modified.
    internal sealed class KksSexyVoicePlayback : IDisposable
    {
        internal const string Personality = "c00";
        readonly GameObject root;
        readonly HVoiceCtrl native;
        readonly HFlag flags;
        readonly ChaControl actor;
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly int slot = default(int);
        readonly int action = (int)HFlag.EMode.houshi + 1;
        internal KksSexyVoicePlayback(SessionContext context, ChaControl actor, Integration.NativeKksSessionRuntime runtime)
        {
            this.actor = actor;
            if (context.Request.MotionId != KksOnePosePlayback.MotionId) throw new InvalidDataException("Native KKS sexy voice currently requires the selected KKS houshi pose");
            root = new GameObject("KKS sexy session selector " + context.Id); root.SetActive(false);
            try
            {
                flags = runtime?.Flags ?? throw new InvalidOperationException("Native KKS session state must be prepared before voice.");
                native = root.AddComponent<HVoiceCtrl>(); native.enabled = false;
                native.flags = flags; native.hand = runtime.Hand;
                var previous = HFlag.lstABName;
                try { HFlag.lstABName = CommonLib.GetAssetBundleNameListFromPath("h/list/"); HFlag.lstABName.Sort(); native.InitD(Personality, string.Empty); }
                finally { HFlag.lstABName = previous; }
                if (!native.dicBreathPtn[slot][(int)flags.mode].ContainsKey("Idle") || native.dicBreathVoiceIntos[slot][(int)flags.mode].Values.All(v => v.Count == default(int)))
                    throw new InvalidDataException("Installed KKS c00 native houshi breath assets are missing");
            }
            catch { Dispose(); throw; }
        }
        T Call<T>(string name, params object[] arguments)
        {
            var method = typeof(HVoiceCtrl).GetMethod(name, Private) ?? throw new MissingMethodException(typeof(HVoiceCtrl).FullName, name);
            try { return (T)method.Invoke(native, arguments); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        internal VoiceRequest Breath(string upperState)
        {
            string state = KksNativePoseDefinition.NativeState(upperState);
            // The shared native progression owner supplies speed/gauge conditions.
            if (!native.dicBreathPtn[slot][(int)flags.mode].TryGetValue(state, out var pattern)) return null;
            var dictionary = native.dicBreathVoiceIntos[slot][(int)flags.mode];
            var patterns = pattern.lstInfo.Where(p => Call<bool>("IsPlayBreathVoicePtn", p, slot)).ToList();
            var choices = Call<List<HVoiceCtrl.VoiceSelect>>("GetPLayNumBreathList", patterns, dictionary, slot);
            if (choices.Count == default(int))
            {
                foreach (var group in dictionary.Values) foreach (var voice in group.Values) voice.isPlay = false;
                choices = Call<List<HVoiceCtrl.VoiceSelect>>("GetPLayNumBreathList", patterns, dictionary, slot);
            }
            if (choices.Count == default(int)) return null;
            var chosen = choices[UnityEngine.Random.Range(default(int), choices.Count)];
            var info = dictionary[chosen.state][chosen.playnum]; info.isPlay = true;
            return new VoiceRequest { Slot = slot, Kind = VoiceKind.Breath, Bundle = info.pathAsset, Asset = info.nameFile, Group = info.group.ToString(), State = upperState, KksNative = true,
                KksFaceId = info.lstFaceNormal.Count == default(int) ? -1 : info.lstFaceNormal[UnityEngine.Random.Range(default(int), info.lstFaceNormal.Count)], KksVoiceAction = (int)flags.mode };
        }
        internal VoiceRequest Ordinary(string state)
        {
            // HHoushi.PlayIdle/LoopProc use native request 200/201/202.
            string key = state == "Idle" ? "0" : state == "WLoop" ? "1" : state == "SLoop" || state == "OLoop" ? "2" : null;
            return key == null ? null : OrdinaryPattern(int.Parse(key), state);
        }
        internal VoiceRequest OrdinaryRequest(string request, string state)
        {
            string prefix = action.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (request == null || !request.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidDataException("Native KKS voice action differs.");
            return OrdinaryPattern(int.Parse(request.Substring(prefix.Length), System.Globalization.CultureInfo.InvariantCulture), state);
        }
        VoiceRequest OrdinaryPattern(int key, string state)
        {
            if (!native.dicVoicePtn[slot][action].TryGetValue(key, out var pattern)) return null;
            var dictionary = native.dicVoiceIntos[slot][action];
            var patterns = pattern.lstInfo.Where(p => Call<bool>("IsPlayVoicePtn", p, action, actor, slot)).ToList();
            var choices = Call<List<HVoiceCtrl.VoiceSelect>>("GetPLayNumVoiceList", patterns, dictionary, action, actor, slot);
            if (choices.Count == default(int)) return null;
            var chosen = choices.OrderBy(c => dictionary[c.state][c.playnum].priority).First();
            var info = dictionary[chosen.state][chosen.playnum]; info.isPlay = true;
            return new VoiceRequest { Slot = slot, Kind = VoiceKind.Voice, Bundle = info.pathAsset, Asset = info.nameFile, State = state, Protect = info.notOverwrite, KksNative = true, KksFaceId = info.face, KksVoiceAction = action };
        }
        public void Dispose() { if (root != null) UnityEngine.Object.Destroy(root); }
    }
}
