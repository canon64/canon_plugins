using System;
using System.Collections.Generic;
using UnityEngine;

namespace MainGameBlankMapAdd
{
    // Terrain sizes are expressed in world units and need their own conversion.
    internal sealed class AiIslandGeometry : MonoBehaviour
    {
        private TerrainData _data;
        private Material _material;
        private readonly Dictionary<Material, Material> _waterMaterials = new Dictionary<Material, Material>();
        private bool _converted;
        internal void ConvertUnits(float scale)
        {
            if (_converted) return;
            _converted = true;
            transform.localScale *= scale;
            ConvertWaterMaterials(scale);
            foreach (Light light in GetComponentsInChildren<Light>(true))
            { light.range *= scale; light.shadowNearPlane *= scale; }
            foreach (ParticleSystem particles in GetComponentsInChildren<ParticleSystem>(true))
            {
                bool playing = particles.isPlaying;
                // Local/Shape scaling ignores the converted parent and leaves flames/spray ten times too big.
                particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.cullingMode = ParticleSystemCullingMode.Automatic;
                if (playing) particles.Play(false);
            }
            Terrain terrain = GetComponentInChildren<Terrain>(true);
            if (terrain == null || terrain.terrainData == null) return;
            _data = Instantiate(terrain.terrainData);
            _data.name = "AI island KKS-sized terrain";
            _data.size *= scale;
            TreeInstance[] trees = _data.treeInstances;
            for (int i = 0; i < trees.Length; i++) { trees[i].widthScale *= scale; trees[i].heightScale *= scale; }
            _data.treeInstances = trees;
            DetailPrototype[] details = _data.detailPrototypes;
            foreach (var detail in details)
            {
                detail.minWidth *= scale; detail.maxWidth *= scale;
                detail.minHeight *= scale; detail.maxHeight *= scale;
            }
            _data.detailPrototypes = details;
            terrain.terrainData = _data;
            TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
            if (collider != null) collider.terrainData = _data;
            if (terrain.materialTemplate != null)
            {
                _material = Instantiate(terrain.materialTemplate);
                _material.name = "AI island KKS-sized CTS";
                for (int i = 1; i <= 16; i++) ScaleProperty("_Texture_" + i + "_Tiling", scale);
                ScaleProperty("_Geological_Tiling_Close", scale);
                ScaleProperty("_Geological_Tiling_Far", scale);
                ScaleProperty("_Perlin_Normal_Tiling_Close", scale);
                ScaleProperty("_Perlin_Normal_Tiling_Far", scale);
                terrain.materialTemplate = _material;
            }
            terrain.Flush();
        }
        private void ScaleProperty(string name, float scale)
        { if (_material.HasProperty(name)) _material.SetFloat(name, _material.GetFloat(name) * scale); }
        private void ConvertWaterMaterials(float scale)
        {
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null || source.shader == null ||
                        !source.shader.name.StartsWith("Lux Water/WaterSurface", StringComparison.Ordinal) ||
                        !source.HasProperty("_GerstnerVertexIntensity") || !source.HasProperty("_GFinalFrequency")) continue;
                    if (!_waterMaterials.TryGetValue(source, out Material converted))
                    {
                        converted = Instantiate(source);
                        _waterMaterials.Add(source, converted);
                        converted.name = source.name + " (KKS island waves)";
                        Vector4 displacement = source.GetVector("_GerstnerVertexIntensity");
                        displacement.x *= scale; displacement.y *= scale; displacement.z *= scale;
                        converted.SetVector("_GerstnerVertexIntensity", displacement);
                        converted.SetVector("_GFinalFrequency", source.GetVector("_GFinalFrequency") / scale);
                    }
                    materials[i] = converted;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }
        private void OnDestroy()
        {
            if (_data != null) Destroy(_data);
            if (_material != null) Destroy(_material);
            foreach (Material material in _waterMaterials.Values) if (material != null) Destroy(material);
            _waterMaterials.Clear();
        }
    }
}
