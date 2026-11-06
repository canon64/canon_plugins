using System;
using System.Collections.Generic;
using System.Reflection;

namespace CanonPlugins.Local
{
    internal static class SessionGate { internal static bool Allowed = true; }
}

namespace UnityEngine
{
    internal class MonoBehaviour
    {
        internal GameObject gameObject;
        internal Transform transform => gameObject.transform;
        internal bool Destroyed;
        internal static void Destroy(object value)
        {
            if (value is MonoBehaviour b) b.Destroyed = true;
            if (value is Material m) m.Destroyed = true;
            if (value is GameObject g) g.Destroyed = true;
        }
    }
    internal class Transform
    {
        internal Vector3 position, forward = new Vector3(0, 0, 1);
        internal Quaternion rotation = new Quaternion(0, 0, 0, 1);
        internal void SetPositionAndRotation(Vector3 p, Quaternion q) { position = p; rotation = q; }
    }
    internal class GameObject
    {
        internal readonly Transform transform = new Transform();
        internal readonly List<MonoBehaviour> Components = new List<MonoBehaviour>();
        internal bool Destroyed;
        internal HideFlags hideFlags;
        internal GameObject(string name) { }
        internal T AddComponent<T>() where T : MonoBehaviour, new()
        {
            var c = new T { gameObject = this }; Components.Add(c); return c;
        }
    }
    internal class Shader { }
    internal class Material
    {
        internal bool Destroyed;
        internal bool FailPass = false;
        internal Color Color;
        internal HideFlags hideFlags;
        internal Material(Shader shader) { }
        internal void SetColor(string name, Color color) { Color = color; GL.Material = this; }
        internal bool SetPass(int pass) => !Destroyed && !FailPass;
    }
    internal static class GL
    {
        internal const int QUADS = 7;
        internal static int Vertices;
        internal static Material Material;
        internal static void Begin(int mode) { Vertices = 0; }
        internal static void Vertex3(float x, float y, float z) { ++Vertices; }
        internal static void End() { }
    }
    internal class RenderTexture
    {
        internal static RenderTexture active;
        internal int width, height;
        internal RenderTextureFormat format;
        internal HideFlags hideFlags;
        internal bool Released;
        internal RenderTexture(int width, int height, int depth, RenderTextureFormat format)
        { this.width = width; this.height = height; this.format = format; }
        internal void Create() { }
        internal void Release() { Released = true; }
    }
    internal static class Graphics
    {
        internal static RenderTexture LastSource, LastDestination;
        internal static void Blit(RenderTexture source, RenderTexture destination)
        { LastSource = source; LastDestination = destination; }
    }
    internal class Camera : MonoBehaviour
    {
        internal enum StereoscopicEye { Left, Right }
        internal enum MonoOrStereoscopicEye { Left, Right, Mono }
        internal static Camera LastRendered;
        internal Matrix4x4 NativeLeft, NativeRight, NativeLeftView, NativeRightView;
        internal MonoOrStereoscopicEye stereoActiveEye = MonoOrStereoscopicEye.Left;
        internal StereoTargetEyeMask stereoTargetEye;
        internal RenderTexture targetTexture;
        internal Rect rect;
        internal bool enabled, allowMSAA;
        internal int ProjectionWrites, ViewWrites;
        internal int StereoWrites = 0;
        private Matrix4x4 _projection, _view;
        internal Matrix4x4 projectionMatrix
        {
            get => ProjectionWrites == 0 ? NativeLeft : _projection;
            set { _projection = value; ++ProjectionWrites; }
        }
        internal Matrix4x4 worldToCameraMatrix
        {
            get => ViewWrites == 0 ? NativeLeftView : _view;
            set { _view = value; ++ViewWrites; }
        }
        internal bool InEyeRender;
        internal Matrix4x4 GetStereoProjectionMatrix(StereoscopicEye eye)
            => !InEyeRender || eye == StereoscopicEye.Left ? NativeLeft : NativeRight;
        internal Matrix4x4 GetStereoViewMatrix(StereoscopicEye eye)
            => !InEyeRender || eye == StereoscopicEye.Left ? NativeLeftView : NativeRightView;
        internal void CopyFrom(Camera camera) { NativeLeft = camera.NativeLeft; NativeRight = camera.NativeRight; }
        internal void ResetCullingMatrix() { }
        internal void Render()
        {
            LastRendered = this;
            foreach (var component in gameObject.Components)
                component.GetType().GetMethod("OnPostRender", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(component, null);
        }
    }
}
