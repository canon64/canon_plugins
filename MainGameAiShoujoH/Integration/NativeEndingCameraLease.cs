using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using MainGameAiShoujoH.Contracts;
using UnityEngine;

namespace MainGameAiShoujoH.Integration
{
#pragma warning disable CS0649 // Exact preserved native camera JSON.
    [DataContract] internal sealed class NativeEndingCameraData
    {
        [DataMember] public string sourceSha256,sourceProfileSha256,targetBone;
        [DataMember] public Vector3 communicationOffset;
        [DataMember] public Lens lens;
        [DataMember] public Composer composer;
        [DataContract] internal sealed class Lens { [DataMember] public float FieldOfView,OrthographicSize,NearClipPlane,FarClipPlane,Dutch; }
        [DataContract] internal sealed class Composer { [DataMember] public Vector3 m_TrackedObjectOffset; [DataMember] public float m_LookaheadTime,m_LookaheadSmoothing,m_HorizontalDamping,m_VerticalDamping,m_ScreenX,m_ScreenY,m_DeadZoneWidth,m_DeadZoneHeight,m_SoftZoneWidth,m_SoftZoneHeight,m_BiasX,m_BiasY; [DataMember] public int m_LookaheadIgnoreY; }
        internal static NativeEndingCameraData Load(string path)
        {
            var data=Motion.DedicatedMotionModule.ReadCatalog<NativeEndingCameraData>(path);
            if(data?.lens==null||data.composer==null||string.IsNullOrWhiteSpace(data.sourceSha256)||string.IsNullOrWhiteSpace(data.sourceProfileSha256)||string.IsNullOrWhiteSpace(data.targetBone))throw new InvalidDataException("原本終了ADVカメラの根拠/値がありません。");
            return data;
        }
    }
#pragma warning restore CS0649
    // The native communication offset/lens are applied once after each ending pose.
    // KKS ending poses face the opposite longitudinal axis to the native camera space.
    internal sealed class NativeEndingCameraLease : IDisposable
    {
        readonly Camera camera;
        readonly StartRequest request;
        readonly NativeEndingCameraData data;
        readonly Transform breast, head;
        readonly Vector3 oldPosition;
        readonly Quaternion oldRotation;
        readonly float oldFov, oldNear, oldFar;
        bool disposed;
        internal NativeEndingCameraLease(StartRequest request, NativeEndingCameraData data)
        {
            this.request=request;this.data=data;camera=request.Camera;
            if(camera==null||request.SourceWorldScale<=default(float)||float.IsNaN(request.SourceWorldScale)||float.IsInfinity(request.SourceWorldScale))throw new InvalidDataException("終了ADVの実カメラ/島単位倍率がありません。");
            if(data.targetBone!="cf_J_Mune00")throw new InvalidDataException("終了ADVカメラの未対応原本注視骨: "+data.targetBone);
            var actor=request.Participants.Single(p=>p.Role=="female").Character;
            const string chestPath="cf_j_root/cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_bust00";
            const string headPath="cf_j_root/cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_j_neck/cf_j_head";
            breast=actor.animBody.transform.Find(chestPath);head=actor.animBody.transform.Find(headPath);
            if(breast==null||head==null)throw new InvalidDataException("終了ADVのKKS胸/頭部骨がありません。");
            oldPosition=camera.transform.position;oldRotation=camera.transform.rotation;oldFov=camera.fieldOfView;oldNear=camera.nearClipPlane;oldFar=camera.farClipPlane;
        }
        internal void Apply()
        {
            if(disposed)throw new ObjectDisposedException(nameof(NativeEndingCameraLease));
            float two=Vector3.one.x+Vector3.one.x;
            var target=(breast.position+head.position)/two;
            var native=data.communicationOffset*request.SourceWorldScale;
            var offset=request.Rotation*new Vector3(native.x,native.y,-native.z);
            if(offset.sqrMagnitude<=Mathf.Epsilon)throw new InvalidDataException("終了ADVの会話距離がありません。");
            camera.transform.SetPositionAndRotation(target+offset,Quaternion.LookRotation(-offset,Vector3.up));
            camera.fieldOfView=data.lens.FieldOfView;camera.nearClipPlane=data.lens.NearClipPlane*request.SourceWorldScale;camera.farClipPlane=data.lens.FarClipPlane*request.SourceWorldScale;
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            if(camera!=null){camera.transform.SetPositionAndRotation(oldPosition,oldRotation);camera.fieldOfView=oldFov;camera.nearClipPlane=oldNear;camera.farClipPlane=oldFar;}
        }
    }
}
