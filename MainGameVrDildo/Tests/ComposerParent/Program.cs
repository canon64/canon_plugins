using System;
using System.Collections.Generic;
using UnityEngine;

// Boundary harness: substitutes Unity/gate state; does not simulate game rendering or motion.
namespace UnityEngine
{
    public struct Vector3 { public float x; public static Vector3 zero => default; }
    public struct Quaternion { public float x; }
    public class Transform
    {
        public Transform parent;
        public Vector3 localPosition, localScale;
        public Quaternion localRotation;
        public bool KeptWorld;
        public void SetParent(Transform value, bool keepWorld) { parent = value; KeptWorld = keepWorld; }
        public bool IsChildOf(Transform value) => parent != null && (parent == value || parent.IsChildOf(value));
    }
    public class GameObject { public Transform transform = new Transform(); }
    public static class GUILayout
    {
        public static void Space(float v) { }
        public static void Label(string v) { }
        public static void BeginHorizontal() { }
        public static void EndHorizontal() { }
        public static bool Button(string v) => false;
    }
}
namespace CanonPlugins.Shared { public static class PluginSessionGate { public static bool Allowed; } }
namespace CanonPlugins.Local { public static class SessionGate { public static bool Allowed; } }
namespace MainGameVrDildo
{
    public class Settings { public bool Enabled = true; }
    public sealed partial class Plugin
    {
        public static Plugin Instance;
        internal GameObject _model = new GameObject();
        internal Settings _settings = new Settings();
        internal int Resets, Releases;
        private void ResetAibuMotion() { Resets++; }
        private void ReleaseGrab() { Releases++; }
        private void RebuildPistonClip() { }
        public bool Driven => IsComposerDriven;
    }
}
namespace MainGameObjectComposer
{
    public class ManagedObjectData { public string id, name; }
    public class RuntimeObjectRef { public GameObject GameObject = new GameObject(); public ManagedObjectData Data; }
    public sealed partial class Plugin
    {
        public Dictionary<string, RuntimeObjectRef> Objects = new Dictionary<string, RuntimeObjectRef>();
        private RuntimeObjectRef FindRuntimeById(string id) => id != null && Objects.TryGetValue(id, out var value) ? value : null;
        public void Bind() => BindDildoParentApi();
        public void Attach(string id, bool snap) => AttachDildo(id, snap);
        public void BeforeDestroy() => PreserveDildoParentBeforeDestroy();
        public void AfterRebuild() => RestoreDildoParentAfterRebuild();
        public void Stop() => EndDildoParentSession();
    }
}
class Program
{
    static void Check(bool result, string name) { if (!result) throw new Exception(name); Console.WriteLine("PASS " + name); }
    static void Main()
    {
        var dildo = new MainGameVrDildo.Plugin(); MainGameVrDildo.Plugin.Instance = dildo;
        var composer = new MainGameObjectComposer.Plugin(); composer.Bind();
        var hook = new MainGameObjectComposer.RuntimeObjectRef { Data = new MainGameObjectComposer.ManagedObjectData { id = "hook", name = "Hook" } };
        composer.Objects["hook"] = hook;
        composer.Attach("hook", false);
        Check(!dildo.Driven, "denied composer cannot attach");
        CanonPlugins.Local.SessionGate.Allowed = true;
        Check(!MainGameVrDildo.Plugin.TrySetComposerParent(hook.GameObject.transform, false), "denied dildo cannot attach");
        CanonPlugins.Shared.PluginSessionGate.Allowed = true;
        dildo._model.transform.localPosition = new Vector3 { x = 3 };
        composer.Attach("hook", false);
        Check(dildo.Driven && dildo._model.transform.parent == hook.GameObject.transform && dildo._model.transform.KeptWorld, "reflection API attaches with world pose preservation");
        Check(dildo.Resets == 1 && dildo.Releases == 1, "attachment resets movement sample and releases grip");
        var child = new Transform { parent = dildo._model.transform };
        Check(!MainGameVrDildo.Plugin.TrySetComposerParent(child, false), "hierarchy cycle refused");
        dildo._model.transform.localPosition = new Vector3 { x = 7 };
        dildo._model.transform.localRotation = new Quaternion { x = 8 };
        dildo._model.transform.localScale = new Vector3 { x = 9 };
        composer.BeforeDestroy();
        Check(!dildo.Driven && dildo._model.transform.parent == null, "detach before parent destruction");
        var replacement = new MainGameObjectComposer.RuntimeObjectRef { Data = hook.Data };
        composer.Objects["hook"] = replacement; composer.AfterRebuild();
        Check(dildo._model.transform.parent == replacement.GameObject.transform && dildo._model.transform.localPosition.x == 7 && dildo._model.transform.localRotation.x == 8 && dildo._model.transform.localScale.x == 9, "rebuild restores same ID and local pose");
        composer.BeforeDestroy(); composer.Objects.Clear(); composer.AfterRebuild();
        Check(!dildo.Driven, "deleted parent leaves dildo detached");
        composer.Objects["hook"] = replacement; composer.Attach("hook", true);
        Check(dildo._model.transform.localPosition.x == 0, "snap to hook origin");
        CanonPlugins.Local.SessionGate.Allowed = false; CanonPlugins.Shared.PluginSessionGate.Allowed = false;
        composer.Stop(); composer.AfterRebuild();
        Check(!dildo.Driven && dildo._model.transform.parent == null, "cleanup after gate invalidation cannot reattach");
        composer.Stop(); Check(!dildo.Driven, "repeated cleanup safe");
        CanonPlugins.Local.SessionGate.Allowed = true; CanonPlugins.Shared.PluginSessionGate.Allowed = true;
        composer.Bind(); dildo._model = null; composer.Attach("hook", true);
        Check(!dildo.Driven, "despawned dildo cannot attach in next session");
    }
}
