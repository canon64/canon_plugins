using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HarmonyLib;
using MainGameAiShoujoMode.Conversation;
using MapEnvironmentRuntime;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    public sealed partial class AiAnimationTrial
    {
        private CharacterLoadTrace _bodyTrace;
        private float[] _bodySource;
        private string _bodySourcePath, _bodySourceHash, _bodyCoordinatePath;
        private void BeginBodyTrace(ChaFileControl file, string path, string coordinate)
        {
            _bodyTrace = Plugin.BeginCharacterLoadTrace();
            if (_bodyTrace == null) return;
            Plugin.CharacterLoadTraceStopped += EndBodyTrace;
            _bodySource = file?.custom?.body?.shapeValueBody?.ToArray();
            _bodySourcePath = path; _bodyCoordinatePath = coordinate;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                try { using (var stream = File.OpenRead(path)) using (var hash = SHA256.Create()) _bodySourceHash = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty); }
                catch (IOException) { _bodySourceHash = "unreadable"; }
            }
            MarkBodyTrace("card_read");
        }
        private void EndBodyTrace()
        {
            Plugin.CharacterLoadTraceStopped -= EndBodyTrace;
            var trace = _bodyTrace; _bodyTrace = null; trace?.Dispose();
            _bodySource = null; _bodySourcePath = _bodySourceHash = _bodyCoordinatePath = null;
        }
        [Serializable] private sealed class BodyLoadSnapshot
        {
            public string phase, utc, source, sourceSha256, coordinate;
            public float[] sourceBody, actorBody;
            public bool ready, highPoly;
            public BodyBoneSnapshot[] bones;
            public string[] modifiers;
        }
        [Serializable] private sealed class BodyBoneSnapshot
        { public string path; public Vector3 position, scale; public Quaternion rotation; }
        private void MarkBodyTrace(string phase)
        {
            var trace = _bodyTrace;
            if (trace?.Enabled != true) return;
            trace.Mark(() =>
            {
                var actor = _actor;
                var bones = actor?.objBodyBone == null ? Array.Empty<BodyBoneSnapshot>() : actor.objBodyBone.GetComponentsInChildren<Transform>(true)
                    .Where(bone => bone.name.StartsWith("cf_s_", StringComparison.Ordinal) || bone.name.StartsWith("cf_d_", StringComparison.Ordinal)
                        || bone.name.IndexOf("bust", StringComparison.OrdinalIgnoreCase) >= default(int))
                    .Select(bone => new BodyBoneSnapshot { path = BonePath(bone, actor.objBodyBone.transform), position = bone.localPosition, rotation = bone.localRotation, scale = bone.localScale }).ToArray();
                return JsonCodec.ToJson(new BodyLoadSnapshot
                {
                    phase = phase, utc = DateTime.UtcNow.ToString("o"), source = _bodySourcePath, sourceSha256 = _bodySourceHash,
                    coordinate = _bodyCoordinatePath, sourceBody = _bodySource,
                    actorBody = actor?.chaFile?.custom?.body?.shapeValueBody?.ToArray(), ready = actor != null && actor.loadEnd,
                    highPoly = actor != null && actor.hiPoly, bones = bones,
                    modifiers = actor == null ? Array.Empty<string>() : actor.GetComponents<Behaviour>().Where(component => component != null)
                        .Where(component => component.GetType().FullName.IndexOf("ABMX", StringComparison.OrdinalIgnoreCase) >= default(int)
                            || component.GetType().FullName.IndexOf("Pushup", StringComparison.OrdinalIgnoreCase) >= default(int))
                        .Select(component => component.GetType().FullName + ":enabled=" + component.enabled + ":baseline=" + AccessTools.Field(component.GetType(), "_baselineKnown")?.GetValue(component)
                            + ":refresh=" + AccessTools.Property(component.GetType(), "NeedsFullRefresh")?.GetValue(component, null)).ToArray()
                }, false);
            });
        }
        private static string BonePath(Transform bone, Transform root)
        {
            var parts = new Stack<string>();
            for (var current = bone; current != null; current = current.parent)
            { parts.Push(current.name); if (current == root) break; }
            return string.Join("/", parts);
        }
        private void CaptureBodyAfterStart()
        {
            var trace = _bodyTrace; var actor = _actor;
            if (trace?.Enabled != true) return;
            Coroutine pending = null;
            IEnumerator Capture()
            {
                yield return null;
                pending = null;
                if (!ReferenceEquals(trace, _bodyTrace) || !trace.Enabled || actor == null || actor != _actor) yield break;
                MarkBodyTrace("after_next_frame");
            }
            pending = StartCoroutine(Capture());
            trace.SetCancellation(() => { if (pending != null && this != null) StopCoroutine(pending); pending = null; });
        }
    }
}
