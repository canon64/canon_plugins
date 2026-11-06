using System;
using System.Collections.Generic;
using System.Linq;
namespace UnityEngine { public class Sprite { } public class RectTransform { } }
namespace Valve.VR
{
    public enum SteamVR_Input_Sources { Any, LeftHand, RightHand }
    public interface ISteamVR_Action_In { }
    public class SteamVR_ActionSet
    {
        public string Path;
        public ulong handle;
        public bool Active;
        public int Priority;
        public static T Create<T>(string path) where T : SteamVR_ActionSet, new() => new T { Path = path };
        public void FinishPreInitialize() { }
        public void Initialize(bool create) { handle = SteamVR_Input.FailHandles ? 0UL : 10UL; }
        public void Activate(SteamVR_Input_Sources source, int priority, bool disable)
        {
            if (source != SteamVR_Input_Sources.RightHand || disable) throw new Exception("Unexpected global input override");
            Active = true; Priority = priority;
        }
        public void Deactivate(SteamVR_Input_Sources source) { Active = false; }
    }
    public class SteamVR_Action
    {
        public string Path;
        public ulong handle;
        public static T Create<T>(string path) where T : SteamVR_Action, new() => new T { Path = path };
        public void Initialize(bool create) { handle = SteamVR_Input.FailHandles ? 0UL : 20UL; }
    }
    public class SteamVR_Action_Boolean : SteamVR_Action, ISteamVR_Action_In
    {
        public delegate void StateDownHandler(SteamVR_Action_Boolean action, SteamVR_Input_Sources source);
        private readonly Dictionary<SteamVR_Input_Sources, StateDownHandler> _handlers = new();
        public void AddOnStateDownListener(StateDownHandler action, SteamVR_Input_Sources source) { _handlers.TryGetValue(source, out var old); _handlers[source] = old + action; }
        public void RemoveOnStateDownListener(StateDownHandler action, SteamVR_Input_Sources source) { _handlers.TryGetValue(source, out var old); _handlers[source] = old - action; }
        public void Down(SteamVR_Input_Sources source)
        {
            var set = SteamVR_Input.actionSets.FirstOrDefault(x => Path.StartsWith(x.Path + "/"));
            if (set != null && !set.Active) return;
            _handlers.TryGetValue(source, out var action); action?.Invoke(this, source);
        }
        public int Listeners => _handlers.Values.Sum(x => x?.GetInvocationList().Length ?? 0);
    }
    public static class SteamVR_Input
    {
        public static bool FailHandles;
        public static SteamVR_ActionSet[] actionSets = Array.Empty<SteamVR_ActionSet>();
        public static SteamVR_Action[] actions = Array.Empty<SteamVR_Action>();
        public static ISteamVR_Action_In[] actionsIn = Array.Empty<ISteamVR_Action_In>();
        public static SteamVR_Action_Boolean[] actionsBoolean = Array.Empty<SteamVR_Action_Boolean>();
        public static ISteamVR_Action_In[] actionsNonPoseNonSkeletonIn = Array.Empty<ISteamVR_Action_In>();
        public static void PreinitializeActionSetDictionaries() { }
        public static void PreinitializeActionDictionaries() { }
    }
}
