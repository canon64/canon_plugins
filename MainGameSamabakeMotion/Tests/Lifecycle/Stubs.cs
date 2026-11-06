using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HarmonyLib { internal static class AccessTools { internal static FieldInfo Field(Type t, string name) => t.GetField(name); } }
namespace UnityEngine
{
    public class Behaviour { public bool enabled = true; }
    public struct Vector3 { public float x; }
    public struct Quaternion { public float w; }
    public class Transform { public Vector3 localPosition, localScale; public Quaternion localRotation; }
    public class RuntimeAnimatorController { public bool Missing; public string name; }
    public class AnimatorOverrideController : RuntimeAnimatorController { public RuntimeAnimatorController runtimeAnimatorController; }
    public enum AnimatorControllerParameterType { Float }
    public enum AnimatorCullingMode { AlwaysAnimate, CullUpdateTransforms, CullCompletely }
    public class AnimatorControllerParameter { public int nameHash; public AnimatorControllerParameterType type; }
    public struct AnimatorStateInfo { public int fullPathHash; public float normalizedTime; }
    public class Animator
    {
        public bool enabled = true;
        public AnimatorCullingMode cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        public string PlayedState;
        public int PlayCount;
        public RuntimeAnimatorController runtimeAnimatorController;
        public float speed = 1;
        public int Layers = 1;
        public int layerCount => runtimeAnimatorController == null ? 0 : Layers;
        public readonly List<object> Components = new List<object>();
        public readonly Dictionary<int, float> Floats = new Dictionary<int, float>();
        public readonly Dictionary<int, float> LayerWeights = new Dictionary<int, float>();
        public AnimatorControllerParameter[] parameters = new[] { "motion", "height1", "partner_height", "Breast1", "speed", "speedBody" }
            .Select(n => new AnimatorControllerParameter { nameHash = StringToHash(n) }).ToArray();
        public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layer) => new AnimatorStateInfo { fullPathHash = 1, normalizedTime = .5f };
        public float GetLayerWeight(int layer) => .7f;
        public T[] GetComponentsInChildren<T>(bool include) => Components.OfType<T>().ToArray();
        public bool HasState(int layer, int hash) => !runtimeAnimatorController.Missing;
        public static int StringToHash(string s) => s.GetHashCode();
        public void SetFloat(int hash, float value) { Floats[hash] = value; }
        public void Play(string state, int layer, float time) { PlayedState = state; PlayCount++; }
        public void Play(int state, int layer, float time) { }
        public void Update(float time) { }
        public void SetLayerWeight(int layer, float value) { LayerWeights[layer] = value; }
    }
    public static class Mathf { public static float Repeat(float value, float length) => value % length; }
}
namespace RootMotion.FinalIK { public class FullBodyBipedIK : UnityEngine.Behaviour { } }
namespace Illusion.Component.Correct { public class FrameCorrect : UnityEngine.Behaviour { } }
public class ChaControl
{
    public UnityEngine.Animator animBody = new UnityEngine.Animator();
    public int sex;
    public bool visibleAll, resetDynamicBoneAll;
    public float GetShapeBodyValue(int index) => .5f;
}
public class HandCtrl
{
    public class MotionEnableState { public string nameAnimation; public bool[] isTouchAreas; public int stateMotion; }
    public MotionEnableState nowMES = new MotionEnableState();
}
public class HSceneProc
{
    public class AnimationListInfo { public int id; }
    public class Flags { public AnimationListInfo nowAnimationInfo; public string nowAnimStateName = "Idle"; public float motion = .4f, speed = .2f; }
    public Flags flags = new Flags();
    public List<ChaControl> lstFemale = new List<ChaControl>();
    public ChaControl male;
    public HandCtrl hand = new HandCtrl();
}
public static class AssetBundleManager
{
    public class Request
    {
        public object Asset;
        public T GetAsset<T>() where T : class => Asset as T;
    }
    public static readonly Dictionary<string, UnityEngine.RuntimeAnimatorController> Controllers = new Dictionary<string, UnityEngine.RuntimeAnimatorController>();
    public static Request LoadAsset(string path, string name, Type type, string manifest) => new Request { Asset = Controllers.TryGetValue(name, out var c) ? c : null };
}
namespace AnimationLoader
{
    public class SwapAnimationInfo
    {
        public string Guid, ControllerFemale, ControllerFemale1, ControllerMale, PathFemale, PathFemale1, PathMale;
        public int StudioId, Mode;
    }
    public static class SwapAnim { public static Dictionary<HSceneProc.AnimationListInfo, SwapAnimationInfo> SwapAnimationMapping = new Dictionary<HSceneProc.AnimationListInfo, SwapAnimationInfo>(); }
}
namespace MainGameSamabakeMotion
{
    internal static class NativeContactRig
    {
        internal static Func<ChaControl, ContactDefinition, IContactRig> Factory;
        internal static IContactRig Capture(ChaControl actor, ContactDefinition definition) => definition == null ? null : Factory?.Invoke(actor, definition);
    }
}
