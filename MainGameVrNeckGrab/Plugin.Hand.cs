using System;
using System.Linq;
using UnityEngine;

namespace MainGameVrNeckGrab
{
    public sealed partial class Plugin
    {
        void EnsureHand(ActorState s, int side)
        {
            if (s.Hand != null) return;
            var items = ReflectionAccess.Items(s.NativeHand);
            if (items == null) throw new InvalidOperationException("愛撫の実アイテム一覧がありません。");
            object item = items.Cast<object>().FirstOrDefault(v => Equals(ReflectionAccess.Get(v, "idObj"), 0) && Equals(ReflectionAccess.Get(v, "idUse"), side))
                ?? items.Cast<object>().FirstOrDefault(v => Equals(ReflectionAccess.Get(v, "idObj"), 0));
            var original = ReflectionAccess.Get<GameObject>(item, "obj");
            if (original == null) throw new InvalidOperationException("愛撫に使用する手モデルが未ロードです。");
            var root = new GameObject("NeckGrab hand"); root.SetActive(false);
            try
            {
                root.transform.SetParent(s.Neck, false);
                var model = Instantiate(original, root.transform, false);
                model.name = "Native caress hand (neck)";
                // No native hand controller, EliminateScale, animator or collider may run on this copy.
                foreach (var behaviour in model.GetComponentsInChildren<Behaviour>(true)) behaviour.enabled = false;
                foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (var audio in model.GetComponentsInChildren<AudioSource>(true)) { audio.playOnAwake = false; audio.enabled = false; }
                foreach (var rigidbody in model.GetComponentsInChildren<Rigidbody>(true)) { rigidbody.isKinematic = true; rigidbody.detectCollisions = false; }
                model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity; model.transform.localScale = Vector3.one;
                var bodyOriginal = ReflectionAccess.Get<GameObject>(item, "objBody");
                var silhouetteOriginal = ReflectionAccess.Get<GameObject>(item, "objSilhouette");
                if (bodyOriginal != null)
                    foreach (var t in model.GetComponentsInChildren<Transform>(true))
                        if (t.name == bodyOriginal.name) t.gameObject.SetActive(true);
                        else if (silhouetteOriginal != null && t.name == silhouetteOriginal.name) t.gameObject.SetActive(false);
                // Preserve the native mesh/skin; change only this copy's finger joint pose.
                var joints = model.GetComponentsInChildren<Transform>(true);
                var palm = joints.FirstOrDefault(t => t.name.Equals(side == 0 ? "cf_j_hand_L" : "cf_j_hand_R", StringComparison.OrdinalIgnoreCase));
                if (palm == null) throw new InvalidOperationException("愛撫の手に掌の骨がありません。");
                var closeClip = new AnimationClip { legacy = true, name = "Neck grip", wrapMode = WrapMode.Once };
                var owner = root.AddComponent<OwnedHandClip>(); owner.Clip = closeClip;
                var close = model.AddComponent<Animation>(); close.playAutomatically = false;
                var finish = model.AddComponent<HandGripAnimation>(); finish.Animation = close;
                int curled = 0;
                foreach (var joint in joints)
                {
                    string n = joint.name.ToLowerInvariant();
                    if (!(n.Contains("index") || n.Contains("middle") || n.Contains("ring") || n.Contains("little") || n.Contains("thumb"))) continue;
                    if (!(n.Contains("01") || n.Contains("02") || n.Contains("03"))) continue;
                    float curl = n.Contains("thumb") ? 28 : n.Contains("01") ? 38 : 58;
                    Quaternion from = joint.localRotation;
                    Quaternion to = from * Quaternion.Euler(0, 0, side == 0 ? -curl : curl);
                    string path = joint.name;
                    for (Transform parent = joint.parent; parent != model.transform && parent != null; parent = parent.parent) path = parent.name + "/" + path;
                    closeClip.SetCurve(path, typeof(Transform), "localRotation.x", AnimationCurve.Linear(0, from.x, .2f, to.x));
                    closeClip.SetCurve(path, typeof(Transform), "localRotation.y", AnimationCurve.Linear(0, from.y, .2f, to.y));
                    closeClip.SetCurve(path, typeof(Transform), "localRotation.z", AnimationCurve.Linear(0, from.z, .2f, to.z));
                    closeClip.SetCurve(path, typeof(Transform), "localRotation.w", AnimationCurve.Linear(0, from.w, .2f, to.w));
                    finish.Joints.Add(joint); finish.Rotations.Add(to);
                    ++curled;
                }
                if (curled < 8) throw new InvalidOperationException("指の関節を特定できません。別の手モデルには置き換えません。");
                float scale = _handScale.Value;
                root.transform.localScale = Vector3.one * scale;
                // The hand root is positioned from the actual native palm bone, not prefab origin.
                Quaternion wanted = Quaternion.Euler(0, side == 0 ? 90 : -90, 90);
                var corrected = PalmPose.CorrectRotation(new System.Numerics.Quaternion(wanted.x, wanted.y, wanted.z, wanted.w));
                wanted = new Quaternion(corrected.X, corrected.Y, corrected.Z, corrected.W);
                model.transform.localRotation = wanted * Quaternion.Inverse(Quaternion.Inverse(model.transform.rotation) * palm.rotation);
                var indexBase = joints.First(t => t.name == (side == 0 ? "cf_j_index01_L" : "cf_j_index01_R"));
                var littleBase = joints.First(t => t.name == (side == 0 ? "cf_j_little01_L" : "cf_j_little01_R"));
                // The wrist joint is NOT the center of the palm. Use the native knuckle bases
                // and palm pad, then place that contact face on this actor's actual neck skin.
                var center = PalmPose.Center(ToNumeric(palm.position), ToNumeric(indexBase.position), ToNumeric(littleBase.position));
                Vector3 inward = palm.TransformDirection(side == 0 ? Vector3.up : Vector3.down).normalized;
                Vector3 surface = NeckSkinSurface(s);
                var delta = PalmPose.AlignmentDelta(center, ToNumeric(inward), ToNumeric(surface));
                model.transform.position += new Vector3(delta.X, delta.Y, delta.Z);
                closeClip.EnsureQuaternionContinuity();
                closeClip.AddEvent(new AnimationEvent { time = .2f, functionName = nameof(HandGripAnimation.Finish) });
                close.AddClip(closeClip, closeClip.name);
                model.SetActive(true); root.SetActive(true); close.Play(closeClip.name); s.Hand = root;
            }
            catch { Destroy(root); throw; }
        }
        static System.Numerics.Vector3 ToNumeric(Vector3 v) => new System.Numerics.Vector3(v.x, v.y, v.z);
        static Vector3 NeckSkinSurface(ActorState s)
        {
            if (s.Actor.objBody == null) throw new InvalidOperationException("首の肌を取得できません。");
            var head = ReflectionAccess.Bone(s.Actor, "cf_j_head");
            Vector3 center = head != null ? Vector3.Lerp(s.Neck.position, head.position, .35f) : s.Neck.position;
            Vector3 outward = s.Neck.forward.normalized;
            Vector3 origin = center + outward * .18f;
            float best = float.PositiveInfinity;
            var errors = new System.Collections.Generic.List<string>();
            foreach (var renderer in s.Actor.objBody.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer == null || renderer.sharedMesh == null || !renderer.name.StartsWith("o_body", StringComparison.OrdinalIgnoreCase)) continue;
                var mesh = new Mesh();
                try
                {
                    renderer.BakeMesh(mesh);
                    var vertices = mesh.vertices;
                    var triangles = mesh.triangles;
                    var world = vertices.Select(v => ToNumeric(renderer.transform.TransformPoint(v))).ToArray();
                    for (int i = 0; i + 2 < triangles.Length; i += 3)
                    {
                        if (!MainGameSyringe.SkinGeometry.RayTriangle(ToNumeric(origin), ToNumeric(-outward),
                            world[triangles[i]], world[triangles[i + 1]], world[triangles[i + 2]], .24f, out float distance)) continue;
                        Vector3 hit = origin - outward * distance;
                        if ((hit - center).sqrMagnitude > .01f) continue;
                        if (distance < best) best = distance;
                    }
                }
                catch (Exception e) { errors.Add(renderer.name + ": " + e.Message); }
                finally { Destroy(mesh); }
            }
            if (float.IsPositiveInfinity(best)) throw new InvalidOperationException("首の実肌へ手のひらを合わせられません。" + string.Join(" / ", errors));
            return origin - outward * best;
        }
    }
    internal sealed class OwnedHandClip : MonoBehaviour
    {
        internal AnimationClip Clip;
        void OnDestroy() { if (Clip != null) Destroy(Clip); }
    }
    internal sealed class HandGripAnimation : MonoBehaviour
    {
        internal readonly System.Collections.Generic.List<Transform> Joints = new System.Collections.Generic.List<Transform>();
        internal readonly System.Collections.Generic.List<Quaternion> Rotations = new System.Collections.Generic.List<Quaternion>();
        internal Animation Animation;
        public void Finish()
        {
            Animation.Stop();
            for (int i = 0; i < Joints.Count; ++i) if (Joints[i] != null) Joints[i].localRotation = Rotations[i];
        }
    }
}
