using System;
using UnityEngine;
using VRGIN.Controls;

namespace MainGirlHipHijack.Runtime
{
    internal sealed class VrInteractionState
    {
        internal bool GrabMode;
        internal Controller.Lock LeftGrabLock;
        internal Controller.Lock RightGrabLock;
        internal int LeftGrabIndex = -1;
        internal int RightGrabIndex = -1;
        internal readonly Transform[] WristParents = new Transform[2];
        internal readonly bool[] WristResetUntilGripUp = new bool[2];
        internal readonly VrElbowAimState[] ElbowAims = { new VrElbowAimState(), new VrElbowAimState() };
        internal bool InputFocused = true;
        internal bool BodyControlLinkEnabled;
        internal Controller.Lock BodyControlLock;
        internal Renderer[] HiddenControllerRenderers = Array.Empty<Renderer>();
        internal bool[] HiddenControllerRendererStates = Array.Empty<bool>();
        internal Collider[] HiddenControllerColliders = Array.Empty<Collider>();
        internal bool[] HiddenControllerColliderStates = Array.Empty<bool>();
        internal bool ControllerHidden;
    }

    internal sealed class VrElbowAimState
    {
        internal int Index = -1;
        internal Plugin.RuntimeWristBinding WristOwner;
        internal Transform PreviousParent;
        internal Vector3 BaseLocalPosition;
        internal Vector3 ExtensionLocalDirection;
    }
}
