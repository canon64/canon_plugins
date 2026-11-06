using System;

namespace MainGameAiShoujoH.Motion
{
    public sealed class MotionCatalog
    {
        public int version;
        public MotionDefinition[] motions;
    }
    public sealed class MotionDefinition
    {
        public string id, name, category, initialState, sourceHash;
        public bool packedVerified, constraintsVerified, contactVerified, sourceDataVerified;
        public int verificationVersion; public string sourceVerificationHash, nativeVerificationId;
        public string[] issues;
        public MotionState[] states;
        public NativeGraphActor[] nativeActors;
        public bool nativeGraphVerified;
    }
    public sealed class NativeGraphActor
    {
        public string role, bundle, sha256, controller, baseBundle, baseSha256, baseController;
        public string[] bindingPaths;
        public NativeClipBinding[] overrides;
        public NativeSourceRotationDriver sourceRotations;
    }
    public sealed class NativeSourceRotationDriver
    {
        public string bundle, sha256, controller, baseController;
        public bool nativeDriverVerified, coverageIncomplete;
        public string verificationScope, verificationReceipt, verificationReceiptSha256;
        public NativeSourceNode[] nodes;
        public NativeSourceClip[] baseClips, poseClips;
        public NativeSourceRotation[] rotations;
        public NativeSourcePosition[] positions;
    }
    public sealed class NativeSourceNode { public string path; public UnityEngine.Vector3 position, scale; public UnityEngine.Quaternion rotation; }
    public sealed class NativeSourceClip { public string sourceId, asset, name; public float duration; }
    public sealed class NativeSourceRotation
    {
        public string sourceBone, sourceParent, targetBone, targetParent;
        public UnityEngine.Quaternion targetRestParent, targetRestBone;
    }
    public sealed class NativeSourcePosition
    {
        public string sourceBone, sourceParent, targetBone, targetParent;
        public float unitScale;
    }
    public sealed class NativeClipBinding : MotionClipReference { public string key; public bool poseOverride; }
    public sealed class MotionState
    {
        public string name;
        public MotionActor[] actors;
        public float duration;
        public bool loop;
    }
    public class MotionClipReference
    {
        public string bundle, asset, sha256;
        public float duration;
    }
    public sealed class MotionVariant : MotionClipReference { public float height; }
    public sealed class MotionActor : MotionClipReference
    {
        public string role, sourceBundle, sourceController;
        public MotionVariant[] variants;
        public MotionContact[] contacts;
        public MotionFrame[] frames;
        public BodyCorrection[] corrections;
        public MotionLayer[] layers;
    }
    public sealed class MotionLayer { public int index; public float weight; }
    public sealed class BodyCorrection
    {
        public string bone, shapeRole;
        public bool ikTarget, beforeIK;
        public ShapeCorrection[] shapes;
    }
    public sealed class ShapeCorrection
    {
        public int[] indices;
        public float[] weights;
        public float[] smallPosition, mediumPosition, largePosition;
        public float[] smallRotation, mediumRotation, largeRotation;
    }
    public sealed class MotionContact
    {
        public int part;
        public string partner, target, bend;
        public float positionWeight, rotationWeight, bendWeight;
    }
    public sealed class MotionFrame
    {
        public string path, parentRole, parentBone;
        public float[] smallPosition, mediumPosition, largePosition;
        public float[] smallRotation, mediumRotation, largeRotation;
    }
    public sealed class EndingMotionCatalog { public int version; public EndingMotionClip[] clips; public string[] bindingPaths; }
    public sealed class EndingMotionClip : MotionClipReference { public int posture, pose; public string state, sourceHash, sourcePathID, sourceRawHash, conversionHash; public float sourceDuration; public int verificationVersion; public bool sourceDataVerified, sourceCurveVerified, packedVerified; }
}
