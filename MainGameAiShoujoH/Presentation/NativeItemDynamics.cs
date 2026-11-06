using System;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MainGameAiShoujoH.Presentation
{
#pragma warning disable CS0649
    [DataContract] internal sealed class ItemDynamicsRow
    {
        [DataMember] public string asset, componentPath, rootPath, sourceId;
        [DataMember] public int updateMode, m_FreezeAxis;
        [DataMember] public float m_UpdateRate, m_Damping, m_Elasticity, m_Stiffness, m_Inert, m_Radius, m_EndLength, m_DistanceToObject;
        [DataMember] public Vector3 m_EndOffset, m_Gravity, m_Force;
        [DataMember] public bool m_DistantDisable;
    }
    [DataContract] internal sealed class ItemDynamicsData { [DataMember] public string sourceHash; [DataMember] public ItemDynamicsRow[] rows; }
#pragma warning restore CS0649
    /// <summary>Explicit selected-payload adapter; AI Normal and KKS native Update/LateUpdate phases correspond.</summary>
    internal sealed class NativeItemDynamics
    {
        private readonly ItemDynamicsData data;
        internal NativeItemDynamics(string directory, string prefabSourceHash)
        {
            data = MainGameAiShoujoH.Motion.DedicatedMotionModule.ReadCatalog<ItemDynamicsData>(Path.Combine(directory, "dynamic-bones.json"));
            if (data?.rows == null || data.sourceHash != prefabSourceHash) throw new InvalidDataException("Native item DynamicBone source settings differ.");
        }
        internal void Apply(string asset, GameObject instance)
        {
            foreach (var row in data.rows.Where(r => r.asset == asset))
            {
                // AI Normal=0: OnUpdate→PreUpdate→InitTransforms; LateUpdate uses Time.deltaTime.
                // The KKS component itself already owns these same native phases. No extra driver is installed.
                if (row.updateMode != default(int)) throw new NotSupportedException("Only the source-proven Normal item DynamicBone payload is supported.");
                var owner = string.IsNullOrEmpty(row.componentPath) ? instance.transform : instance.transform.Find(row.componentPath);
                var root = string.IsNullOrEmpty(row.rootPath) ? instance.transform : instance.transform.Find(row.rootPath);
                if (owner == null || root == null) throw new InvalidDataException("Native item DynamicBone root capture failed: " + row.sourceId);
                var dynamic = owner.GetComponent<DynamicBone>();
                if (dynamic == null) throw new InvalidDataException("KKS DynamicBone binding is missing: " + row.sourceId);
                dynamic.m_Root = root; dynamic.m_UpdateRate = row.m_UpdateRate;
                dynamic.m_Damping = row.m_Damping; dynamic.m_Elasticity = row.m_Elasticity; dynamic.m_Stiffness = row.m_Stiffness;
                dynamic.m_Inert = row.m_Inert; dynamic.m_Radius = row.m_Radius;
                dynamic.m_EndLength = row.m_EndLength; dynamic.m_EndOffset = row.m_EndOffset;
                dynamic.m_Gravity = row.m_Gravity; dynamic.m_Force = row.m_Force;
                dynamic.m_FreezeAxis = (DynamicBone.FreezeAxis)row.m_FreezeAxis;
                dynamic.m_DistantDisable = row.m_DistantDisable; dynamic.m_DistanceToObject = row.m_DistanceToObject;
                // Both saved active components explicitly have empty curves/collider/exclusion/notRolls arrays and a null reference.
                dynamic.m_DampingDistrib = new AnimationCurve(); dynamic.m_ElasticityDistrib = new AnimationCurve();
                dynamic.m_StiffnessDistrib = new AnimationCurve(); dynamic.m_InertDistrib = new AnimationCurve(); dynamic.m_RadiusDistrib = new AnimationCurve();
                dynamic.m_Colliders = new List<DynamicBoneCollider>(); dynamic.m_Exclusions = new List<Transform>();
                dynamic.m_notRolls = new List<Transform>(); dynamic.m_ReferenceObject = null;
                dynamic.enabled = true;
            }
        }
    }
}
