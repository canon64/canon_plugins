using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MainGameAiShoujoH.Contracts;
using MainGameAiShoujoH.Progression;
using UnityEngine;
using UnityEngine.Animations;

namespace MainGameAiShoujoH.Presentation
{

    // Native item rigs, shaders, physics and IK remain in the copied original bundle closure.
    // No body retarget, script stripping, polling or transform copy loop is introduced.
    internal sealed class NativeItemPresenter : IDisposable
    {
        private sealed class Item { internal GameObject Root; internal Animator Animator; internal readonly Dictionary<GameObject, bool> Visibility = new Dictionary<GameObject, bool>(); internal readonly List<UnityEngine.Object> Owned = new List<UnityEngine.Object>(); }
        private readonly SessionContext context;
        private GameObject owner;
        private readonly string directory;
        private readonly ItemCatalog catalog;
        private NativeItemDynamics dynamics;
        private readonly Dictionary<string, AssetBundle> bundles = new Dictionary<string, AssetBundle>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Item>> poses = new Dictionary<string, List<Item>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, Transform>> actors = new Dictionary<string, Dictionary<string, Transform>>(StringComparer.Ordinal);
        private List<Item> active;
        private bool disposed;
        internal NativeItemPresenter(SessionContext context, string directory)
        {
            this.context = context;
            catalog = MainGameAiShoujoH.Motion.DedicatedMotionModule.ReadCatalog<ItemCatalog>(Path.Combine(directory, "catalog.json"));
            if (catalog?.items == null || catalog.files == null || catalog.actorRoles == null) throw new InvalidDataException("Native H-item product closure is missing.");
            this.directory = directory;
            foreach (var actor in context.Request.Participants)
            {
                var map = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
                foreach (var bone in actor.Character.GetComponentsInChildren<Transform>(true)) if (!map.ContainsKey(bone.name)) map.Add(bone.name, bone);
                actors.Add(actor.Role, map);
            }
        }
        private void LoadBundle(ItemFile file)
        {
            if (bundles.ContainsKey(file.source)) return;
            string path = Path.GetFullPath(Path.Combine(directory, file.local));
            string prefix = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || new FileInfo(path).Length != file.bytes) throw new InvalidDataException("H-item product path/size mismatch.");
            using (var sha = System.Security.Cryptography.SHA256.Create())
                if (!string.Equals(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""), file.sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("H-item product hash mismatch.");
            var loaded = AssetBundle.LoadFromFile(path); if (loaded == null) throw new IOException("Native H-item bundle could not be loaded: " + file.source);
            bundles.Add(file.source, loaded);
        }
        private void PreparePose(ProcDefinition definition)
        {
            var plan = NativeItemLoadPlan.Create(catalog, definition.ActionMode, definition.MotionId);
            if (plan.Items.Length == default(int) || poses.ContainsKey(Key(definition.ActionMode, definition.MotionId))) return;
            if (context == null || !context.IsCurrent()) throw new InvalidOperationException("H-item preparation belongs to an expired session.");
            if (context.CanInteract != null && context.CanInteract()) throw new InvalidOperationException("H-item assets require the loading preparation boundary.");
            if (plan.Items.SelectMany(d => d.parents).Any(p => p.actor < catalog.actorRoles.Length && !actors.ContainsKey(Role(p.actor)))) throw new InvalidOperationException("Native H-item participant set is unavailable.");
            plan.Load(LoadBundle);
            if (owner == null) { owner = new GameObject("Dedicated native H items"); owner.SetActive(false); }
            if (plan.Items.Any(d => d.asset == "p_item_omori_L" || d.asset == "p_item_omori_R"))
                dynamics = dynamics ?? new NativeItemDynamics(directory, catalog.files.Single(f => f.source == "h/h_item/01.unity3d").sha256);
            var group = plan.Items;
            var items = new List<Item>(); poses.Add(Key(definition.ActionMode, definition.MotionId), items);
            foreach (var itemDefinition in group)
            {
                var prefab = bundles[itemDefinition.bundle].LoadAsset<GameObject>(itemDefinition.asset);
                if (prefab == null) throw new InvalidDataException("Native H-item prefab is missing: " + itemDefinition.asset);
                var root = UnityEngine.Object.Instantiate(prefab, owner.transform, false); root.SetActive(false);
                var item = new Item { Root = root }; item.Visibility.Add(root, prefab.activeSelf); items.Add(item);
                // Missing native scripts must not turn a physics/IK item into a silently static object.
                if (root.GetComponentsInChildren<MonoBehaviour>(true).Any(c => c == null)) throw new InvalidDataException("Native H-item script binding is unavailable: " + itemDefinition.asset);
                dynamics?.Apply(itemDefinition.asset, root);
                item.Animator = root.GetComponentInChildren<Animator>(true);
                if (!string.IsNullOrEmpty(itemDefinition.controller))
                {
                    if (item.Animator == null) throw new InvalidDataException("Native H-item Animator is missing.");
                    var basis = bundles[itemDefinition.baseBundle].LoadAsset<RuntimeAnimatorController>(itemDefinition.baseController);
                    var pose = bundles[itemDefinition.controllerBundle].LoadAsset<RuntimeAnimatorController>(itemDefinition.controller);
                    if (basis == null || pose == null) throw new InvalidDataException("Native H-item controller is missing.");
                    if (basis.animationClips.Distinct().GroupBy(c => c.name).Any(g => g.Skip((int)Vector3.one.x).Any())) throw new InvalidDataException("Native H-item override has ambiguous clip names.");
                    var controller = Illusion.Utils.Animator.SetupAnimatorOverrideController(basis, pose);
                    if (controller == null) throw new InvalidDataException("Native H-item override could not be constructed.");
                    item.Animator.runtimeAnimatorController = controller; item.Owned.Add(controller);
                }
            }
            int index = default(int);
            foreach (var itemDefinition in group)
            {
                foreach (var parent in itemDefinition.parents)
                {
                    Transform anchor;
                    if (parent.actor < catalog.actorRoles.Length) { if (!actors[Role(parent.actor)].TryGetValue(parent.bone, out anchor)) throw new InvalidDataException("Native H-item actor bone missing: " + parent.bone); }
                    else { int itemIndex = parent.actor - catalog.actorRoles.Length; if (itemIndex < default(int) || itemIndex >= items.Count) throw new InvalidDataException("Native H-item parent index is invalid."); anchor = Find(items[itemIndex].Root.transform, parent.bone); }
                    var child = string.IsNullOrEmpty(parent.self) ? items[index].Root.transform : Find(items[index].Root.transform, parent.self);
                    if (!items[index].Visibility.ContainsKey(child.gameObject)) items[index].Visibility.Add(child.gameObject, child.gameObject.activeSelf);
                    if (parent.direct && parent.scale) { child.SetParent(anchor, false); child.localPosition = Vector3.zero; child.localRotation = Quaternion.identity; }
                    else
                    {
                        child.SetParent(owner.transform, false); child.position = anchor.position; child.rotation = anchor.rotation;
                        if (!parent.scale)
                        {
                            child.localScale = Vector3.one;
                            var constraint = child.gameObject.AddComponent<ParentConstraint>();
                            constraint.AddSource(new ConstraintSource { sourceTransform = anchor, weight = Vector3.one.x }); constraint.weight = Vector3.one.x;
                            constraint.translationAxis = Axis.X | Axis.Y | Axis.Z; constraint.rotationAxis = Axis.X | Axis.Y | Axis.Z;
                            constraint.locked = true; constraint.constraintActive = true;
                        } // Native !direct && scale keeps only the initial placement; it does not join ParentScaleReject.
                    }
                }
                foreach (var node in items[index].Visibility.Keys) node.SetActive(false);
                index++;
            }
        }

        private string Role(int actor) => catalog.actorRoles[actor];
        private static string Key(int mode, int id) => mode.ToString() + ":" + id;
        private static Transform Find(Transform root, string name) { var found = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name); return found ?? throw new InvalidDataException("Native H-item self/parent bone missing: " + name); }
        internal bool IsReady(ProcDefinition definition) => !catalog.items.Any(d => d.mode == definition.ActionMode && d.motionId == definition.MotionId) || (catalog.runtimeVerified && catalog.items.Where(d => d.mode == definition.ActionMode && d.motionId == definition.MotionId).SelectMany(d => d.parents).All(p => p.actor >= catalog.actorRoles.Length || actors.ContainsKey(Role(p.actor))));
        internal void Select(ProcDefinition definition)
        {
            if (disposed) throw new ObjectDisposedException(nameof(NativeItemPresenter));
            PreparePose(definition);
            if (active != null) foreach (var item in active) foreach (var node in item.Visibility.Keys) node.SetActive(false);
            poses.TryGetValue(Key(definition.ActionMode, definition.MotionId), out active);
            if (active == null && catalog.items.Any(d => d.mode == definition.ActionMode && d.motionId == definition.MotionId)) throw new InvalidOperationException("Native H-item rig/binding verification or participant set is unavailable.");
            if (owner != null) owner.SetActive(active != null); if (active != null) foreach (var item in active) foreach (var node in item.Visibility) node.Key.SetActive(node.Value);
        }
        internal void State(string state) { if (active != null) foreach (var item in active) if (item.Animator != null) item.Animator.Play(state, default(int), default(float)); }
        internal void Speed(float speed) { if (active != null) foreach (var item in active) if (item.Animator != null) item.Animator.speed = speed; }
        internal Transform Anchor(int itemIndex, string bone) { if (active == null || itemIndex < default(int) || itemIndex >= active.Count) throw new ArgumentOutOfRangeException(nameof(itemIndex)); return Find(active[itemIndex].Root.transform, bone); }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (var pose in poses.Values) foreach (var item in pose) { foreach (var node in item.Visibility.Keys) if (node != null) UnityEngine.Object.Destroy(node); foreach (var obj in item.Owned) if (obj != null) UnityEngine.Object.Destroy(obj); }
            if (owner != null) UnityEngine.Object.Destroy(owner);
            foreach (var bundle in bundles.Values) if (bundle != null) bundle.Unload(false);
            bundles.Clear(); poses.Clear(); actors.Clear(); active = null;
        }
    }
}
