using System;
using System.Collections.Generic;
using System.Reflection;

namespace HarmonyLib
{
    public static class AccessTools
    {
        public static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        public static MethodInfo Method(Type type, string name) => type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }
}
namespace UnityEngine
{
    // Geometry is outside this test. Any accidental use must fail instead of simulating Unity.
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float a, float b, float c) { x = a; y = b; z = c; }
        public static Vector3 operator +(Vector3 a, Vector3 b) => throw new NotSupportedException();
        public static Vector3 operator -(Vector3 a, Vector3 b) => throw new NotSupportedException();
    }
    public struct Quaternion
    {
        public Vector3 eulerAngles => throw new NotSupportedException();
        public static Quaternion Inverse(Quaternion q) => throw new NotSupportedException();
        public static Quaternion Euler(float x, float y, float z) => throw new NotSupportedException();
        public static Quaternion operator *(Quaternion a, Quaternion b) => throw new NotSupportedException();
        public static Vector3 operator *(Quaternion a, Vector3 b) => throw new NotSupportedException();
    }
    public class Transform
    {
        public Vector3 position;
        public Quaternion rotation;
        public T GetComponent<T>() => throw new NotSupportedException();
        public Vector3 InverseTransformPoint(Vector3 p) => throw new NotSupportedException();
        public Vector3 TransformPoint(Vector3 p) => throw new NotSupportedException();
        public void SetPositionAndRotation(Vector3 p, Quaternion q) => throw new NotSupportedException();
    }
    public class GameObject { public Transform transform; }
}
public class ChaControl { public UnityEngine.Transform transform; public UnityEngine.GameObject objTop; }
public class HFlag
{
    public HSceneProc.AnimationListInfo nowAnimationInfo;
    public CameraController ctrlCamera;
}
public class CameraController { public void SetWorldBase(UnityEngine.Transform t) => throw new NotSupportedException(); }
public class HSceneProc
{
    public sealed class Category { public int category; }
    public sealed class ResourcePath { public string assetpath, file; }
    public sealed class Param { public ResourcePath path; }
    public sealed class AnimationListInfo
    {
        public string mode, nameAnimation;
        public int id;
        public Param paramFemale;
        public List<Category> lstCategory = new List<Category>();
    }
    public List<AnimationListInfo>[] lstAnimInfo = { null, null }, lstUseAnimInfo = { null, null };
    public List<int> categorys = new List<int>(), useCategorys = new List<int>();
    public List<object> closeHpointData = new List<object>();
    public bool closeInitPoint, nowInitPoint, useInitPosPoint;
    public HFlag flags = new HFlag();
    public void ChangeAnimator(AnimationListInfo info, bool reset)
    {
        if (info == null) throw new ArgumentNullException(nameof(info));
        flags.nowAnimationInfo = info;
    }
}
namespace MainGameAiShoujoMode
{
    public sealed partial class AiModeHSession
    {
        private static AiModeHSession _active;
        private HSceneProc _proc;
        private Location _location;
        private Point _point;
        private Parameters _p = new Parameters();
        private sealed class Location { public int[] Categories; }
        private sealed class Point { public int[] category; }
        private sealed class Parameters
        {
            public int I(string key) => Convert.ToInt32(true);
            public float N(string key) => default(float);
        }
        internal static AiModeHSession Attach(HSceneProc proc, int category, bool hotel)
        {
            return _active = new AiModeHSession { _proc = proc, _location = new Location { Categories = new[] { category } },
                _point = new Point { category = new[] { category } }, AllRoomSelections = hotel };
        }
        internal static bool Categories(HSceneProc proc) => BeforeCategories(proc);
        internal static void Catalog(HSceneProc proc) => AfterRoomCatalog(proc);
        internal void Ready() { RoomReady = true; }
    }
}
