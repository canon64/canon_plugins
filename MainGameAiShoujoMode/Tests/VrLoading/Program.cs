using System;
using System.Collections.Generic;
using System.Linq;
using MainGameAiShoujoMode;

namespace UnityEngine
{
    public struct Vector3 { public float x; public Vector3(float value) { x = value; } }
    public struct Quaternion { public float y; public Quaternion(float value) { y = value; } }
    public class Component
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : Component => gameObject.Components.OfType<T>().FirstOrDefault();
        public string name => gameObject.name;
    }
    public class Transform
    {
        public GameObject gameObject;
        public Transform Parent;
        public Transform root => Parent?.root ?? this;
        public string name => gameObject.name;
        public Vector3 position;
        public Quaternion rotation;
        public void SetPositionAndRotation(Vector3 p, Quaternion q) { position = p; rotation = q; }
    }
    public class GameObject
    {
        public string name;
        public Transform transform;
        public bool activeSelf = true;
        public readonly List<Component> Components = new();
        public readonly List<GameObject> Children = new();
        public GameObject(string value) { name = value; transform = new Transform { gameObject = this }; }
        public T Add<T>() where T : Component, new() { var value = new T { gameObject = this }; Components.Add(value); return value; }
        public GameObject Child(string value) { var node = new GameObject(value); node.transform.Parent = transform; Children.Add(node); return node; }
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : Component =>
            Components.OfType<T>().Concat(Children.Where(child => includeInactive || child.activeSelf).SelectMany(child => child.GetComponentsInChildren<T>(includeInactive))).ToArray();
    }
    public class Camera : Component { public static Camera main; public bool enabled = true; }
    public class AudioListener : Component { public bool enabled = true; }
    public class Canvas : Component { public Camera worldCamera; public string renderMode = "ScreenSpaceCamera"; }
}
public class FadeCanvas : UnityEngine.Component
{
    public enum Fade { In, Out }
    public bool isEnd = true;
    public bool Covered;
    public event Action<Fade> onComplete;
    public void StartFade(Fade direction) { Covered = direction == Fade.In; onComplete?.Invoke(direction); }
}
public class SceneFadeCanvas : FadeCanvas { public UnityEngine.Canvas canvas; }
namespace Manager { public static class Scene { public static SceneFadeCanvas sceneFadeCanvas; public static UnityEngine.Component loadingIconJob; } }
public class Rig : UnityEngine.Component
{
    public UnityEngine.Transform Origin { get; set; }
    public int Copies;
    public UnityEngine.Camera Blueprint;
    public void Copy(UnityEngine.Camera view, bool canBeMain, bool otherConsumers)
    {
        if (!canBeMain || otherConsumers) throw new Exception("Incorrect VR camera registration");
        Copies++; Blueprint = view;
    }
}
public static class Vr
{
    public static bool Active { get; set; }
    public static Rig Rig;
    public static UnityEngine.Component Gui;
    public static int Reads;
    public static Rig Camera { get { Reads++; return Rig; } }
    public static UnityEngine.Component GUI { get { Reads++; return Gui; } }
}
public class Mover
{
    public static Mover Instance { get; } = new Mover();
    public int Moves;
    public bool Fail;
    public void MoveTo(UnityEngine.Vector3 position, UnityEngine.Quaternion rotation, bool keepHeight, bool quiet)
    {
        if (keepHeight || !quiet) throw new Exception("Incorrect VR relocation flags");
        Vr.Rig.Origin.SetPositionAndRotation(position, rotation); Moves++;
        if (Fail) throw new InvalidOperationException("Relocation failed");
    }
}
internal static class Program
{
    private static readonly System.Text.Json.JsonElement Settings = System.Text.Json.JsonDocument.Parse(
        System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "settings.json"))).RootElement;
    private static int N(string key) => Settings.GetProperty(key).GetInt32();
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static AiModeVrResources Capture() => new AiModeVrResources(typeof(Vr), typeof(Mover));
    private static void Main()
    {
        Vr.Active = false;
        var desktop = new AiModeVrResources(typeof(Vr), null);
        Require(!desktop.Roots.Any() && !desktop.BindView(null) && Vr.Reads == N("zero"), "Desktop accessed VR runtime");
        Require(!new AiModeVrResources(null, null).Roots.Any(), "Missing VR installation failed");
        var origin = new UnityEngine.GameObject("VR origin");
        var eye = origin.Child("Head");
        Vr.Rig = eye.Add<Rig>(); Vr.Rig.Origin = origin.transform;
        origin.transform.SetPositionAndRotation(new UnityEngine.Vector3(N("initialPosition")), new UnityEngine.Quaternion(N("initialRotation")));
        var eyeCamera = eye.Add<UnityEngine.Camera>();
        var listener = eye.Add<UnityEngine.AudioListener>();
        var inactiveEye = origin.Child("Inactive eye"); inactiveEye.activeSelf = false;
        var inactiveCamera = inactiveEye.Add<UnityEngine.Camera>(); inactiveCamera.enabled = false;
        var gui = new UnityEngine.GameObject("VR GUI"); Vr.Gui = gui.Add<UnityEngine.Component>();
        var guiCamera = gui.Child("GUI camera").Add<UnityEngine.Camera>();
        Vr.Active = true;
        var captured = Capture();
        int previousCopies = Vr.Rig.Copies, previousMoves = Mover.Instance.Moves;
        captured.CaptureNativeView();
        origin.transform.position = new UnityEngine.Vector3(N("initialPosition") + N("one"));
        captured.CaptureNativeView();
        captured.RestoreView();
        Require(origin.transform.position.x == N("initialPosition") && origin.transform.rotation.y == N("initialRotation"), "Native walking lost the original title VR pose");
        Require(Vr.Rig.Copies == previousCopies && Mover.Instance.Moves == previousMoves, "Native walking capture replaced or moved the native camera");
        var manager = new UnityEngine.GameObject("Manager");
        var fade = manager.Add<SceneFadeCanvas>(); fade.canvas = manager.Add<UnityEngine.Canvas>();
        var fadeCamera = manager.Child("Fade camera").Add<UnityEngine.Camera>(); fade.canvas.worldCamera = fadeCamera;
        Manager.Scene.sceneFadeCanvas = fade;
        var logs = N("zero");
        var screen = new AiModeLoadingScreen(_ => logs++, () => false, captured);
        Require(screen.Protect(origin) && screen.Protect(gui) && screen.Protect(manager), "VR/fade roots were not retained");
        Require(screen.Protect(eyeCamera) && screen.Protect(guiCamera) && screen.Protect(inactiveCamera) && screen.Protect(fadeCamera), "VR/fade cameras were not retained");
        Require(screen.Protect(listener), "VR audio listener was not retained");
        var titleCamera = new UnityEngine.GameObject("Title camera").Add<UnityEngine.Camera>();
        var titleListener = titleCamera.gameObject.Add<UnityEngine.AudioListener>();
        var cameras = new[] { titleCamera, eyeCamera, guiCamera, inactiveCamera, fadeCamera };
        var before = cameras.ToDictionary(camera => camera, camera => camera.enabled);
        bool covered = false, revealed = false;
        screen.Cover(() => covered = true);
        foreach (var camera in cameras) if (!screen.Protect(camera)) camera.enabled = false;
        foreach (var audio in new[] { titleListener, listener }) if (!screen.Protect(audio)) audio.enabled = false;
        Require(covered && !titleCamera.enabled && !titleListener.enabled && eyeCamera.enabled && guiCamera.enabled && listener.enabled && !inactiveCamera.enabled, "Entry disabled VR output or enabled an inactive eye");
        var view = new UnityEngine.GameObject("Sandbox Camera").Add<UnityEngine.Camera>();
        view.transform.SetPositionAndRotation(new UnityEngine.Vector3(N("destinationPosition")), new UnityEngine.Quaternion(N("destinationRotation")));
        Require(screen.BindView(view) && Vr.Rig.Blueprint == view && origin.transform.position.x == N("destinationPosition"), "Sandbox camera was not connected/relocated");
        screen.BindView(view);
        Require(Vr.Rig.Copies == N("one") && Mover.Instance.Moves == N("one"), "Repeated registration moved the view twice");
        screen.Reveal(() => revealed = true);
        Require(revealed && !fade.Covered && eyeCamera.enabled && guiCamera.enabled && logs == N("zero"), "Reveal lost VR output or logged with diagnostics off");
        screen.RestoreView();
        Require(origin.transform.position.x == N("initialPosition") && origin.transform.rotation.y == N("initialRotation"), "Title rig pose was not restored");
        origin.transform.position = new UnityEngine.Vector3(N("laterPose"));
        screen.Dispose(); screen.Dispose();
        Require(origin.transform.position.x == N("laterPose"), "Repeated cleanup overwrote a later pose");
        foreach (var pair in before) pair.Key.enabled = pair.Value;
        Require(titleCamera.enabled && !inactiveCamera.enabled, "Camera snapshot restoration changed original state");
        origin.transform.position = new UnityEngine.Vector3(N("initialPosition"));
        var failure = Capture(); Mover.Instance.Fail = true;
        try { failure.BindView(view); throw new Exception("Relocation error swallowed"); }
        catch (System.Reflection.TargetInvocationException) { }
        Require(origin.transform.position.x == N("initialPosition"), "Partial relocation failure left the rig moved");
        origin.transform.position = new UnityEngine.Vector3(N("failedLaterPose")); failure.RestoreView();
        Require(origin.transform.position.x == N("failedLaterPose"), "Failure cleanup retained ownership");
        try { new AiModeVrResources(typeof(Vr), null); throw new Exception("Missing active VR API accepted"); }
        catch (InvalidOperationException) { }
        Console.WriteLine("VR_LOADING_PASS desktop=true camerasPreserved=true guiPreserved=true audioPreserved=true inactivePreserved=true bindOnce=true reveal=true titleRestore=true repeatedCleanup=true partialFailure=true diagnosticsOff=true unity=mocked");
    }
}
