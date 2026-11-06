using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Canonical source; prepare_luxury.py copies this into the dedicated Unity build project.
public static class HotelSecondFloorFinishBuild
{
    private const string Data = "Assets/HotelSecondFloorData/";
    private const string Generated = "Assets/GeneratedHotelLuxury/";
    private const string ScenePath = "Assets/GeneratedHotelSecondFloor/CanonHS2HotelSecondFloor.unity";
    [Serializable] public sealed class MaterialDefinition { public string name; }
    [Serializable] public sealed class MeshDefinition { public string name, material; }
    [Serializable] public sealed class Manifest { public MaterialDefinition[] materials; public MeshDefinition[] meshes; }
    [Serializable] public sealed class Rule
    {
        public string name, texture, normal;
        public string[] sourceMaterials, objects;
        public Color color;
        public Vector2 tiling;
        public float metallic, smoothness, normalScale;
    }
    [Serializable] public sealed class AtlasNormal
    {
        public string sourceMaterial, normal;
        public float strength, smoothness;
    }
    [Serializable] public sealed class Settings
    {
        public int zero, one, normalMaxSize;
        public float ambientIntensity, reflectionIntensity, lightIntensity, lightRange;
        public Color ambient, lightColor;
        public Rule[] rules;
        public AtlasNormal[] atlasNormals;
    }

    private static Texture2D Texture(string file, bool normal, Settings cfg)
    {
        string path = Data + file;
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) throw new InvalidDataException("Missing finish texture: " + path);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.convertToNormalmap = false;
        importer.sRGBTexture = !normal;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.maxTextureSize = cfg.normalMaxSize;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    public static void Build()
    {
        try
        {
            AssetDatabase.Refresh();
            var cfg = JsonUtility.FromJson<Settings>(File.ReadAllText(Data + "luxury-finish.json"));
            Directory.CreateDirectory(Generated);
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var renderers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
            var baseManifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Data + "second_floor.json"));
            var decorationManifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Data + "decorations.json"));
            var baseMaterials = baseManifest.materials;
            var decorationMaterials = decorationManifest.materials;
            // CreateAsset names generated materials after their asset filenames, so retain the source mapping.
            var originalNames = baseMaterials.Concat(decorationMaterials).Select((m, index) => new
                { material = AssetDatabase.LoadAssetAtPath<Material>("Assets/GeneratedHotelSecondFloor/material_" + index + ".mat"), name = m.name })
                .ToDictionary(item => item.material, item => item.name);
            Func<Material, string> originalName = material => originalNames.TryGetValue(material, out var name) ? name : material.name;
            // Restore source assignments first, including finishes removed from the settings.
            // This also makes repeated finish-only builds independent of the previous finish pass.
            var materialsByName = originalNames.ToDictionary(pair => pair.Value, pair => pair.Key);
            var meshMaterials = baseManifest.meshes.ToDictionary(mesh => mesh.name, mesh => mesh.material);
            foreach (var mesh in decorationManifest.meshes) meshMaterials[mesh.name] = mesh.material;
            foreach (var renderer in renderers) renderer.sharedMaterial = materialsByName[meshMaterials[renderer.name]];
            foreach (var rule in cfg.rules)
            {
                var targets = renderers.Where(r => rule.objects.Contains(r.name)
                    || rule.sourceMaterials.Contains(originalName(r.sharedMaterial))).ToArray();
                if (!targets.Any()) throw new InvalidDataException("No finish targets: " + rule.name);
                var material = new Material(Shader.Find("Standard")) { name = rule.name, color = rule.color };
                material.mainTexture = Texture(rule.texture, false, cfg);
                material.mainTextureScale = rule.tiling;
                material.SetTexture("_BumpMap", Texture(rule.normal, true, cfg));
                material.SetFloat("_BumpScale", rule.normalScale);
                material.SetFloat("_Metallic", rule.metallic);
                material.SetFloat("_Glossiness", rule.smoothness);
                material.EnableKeyword("_NORMALMAP");
                string path = Generated + rule.name + ".mat";
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (existing == null) AssetDatabase.CreateAsset(material, path);
                else { EditorUtility.CopySerialized(material, existing); UnityEngine.Object.DestroyImmediate(material); material = existing; }
                foreach (var renderer in targets) renderer.sharedMaterial = material;
            }
            foreach (var rule in cfg.atlasNormals)
            {
                var materials = renderers.Select(r => r.sharedMaterial).Distinct().Where(m => originalName(m) == rule.sourceMaterial).ToArray();
                if (!materials.Any()) throw new InvalidDataException("No native material: " + rule.sourceMaterial);
                var texture = Texture(rule.normal, true, cfg);
                foreach (var material in materials)
                {
                    material.SetTexture("_BumpMap", texture); material.SetFloat("_BumpScale", rule.strength);
                    material.SetFloat("_Glossiness", rule.smoothness); material.EnableKeyword("_NORMALMAP");
                    EditorUtility.SetDirty(material);
                }
            }
            // Warm white replaces the clipped yellow diffuser while retaining the existing fixtures.
            foreach (var material in renderers.Select(r => r.sharedMaterial).Distinct().Where(m => originalName(m) == "2F_WarmDiffuser"))
            {
                material.color = cfg.lightColor;
                material.SetColor("_EmissionColor", cfg.lightColor * cfg.lightIntensity);
                EditorUtility.SetDirty(material);
            }
            foreach (var light in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true)))
            { light.color = cfg.lightColor; light.intensity = cfg.lightIntensity; light.range = cfg.lightRange; }
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = cfg.ambient;
            RenderSettings.ambientIntensity = cfg.ambientIntensity; RenderSettings.reflectionIntensity = cfg.reflectionIntensity;
            EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
            var builds = new[] { new AssetBundleBuild { assetBundleName = "hs2_hotel_second_floor.unity3d", assetNames = new[] { ScenePath } } };
            if (BuildPipeline.BuildAssetBundles("BuildSecondFloor", builds, BuildAssetBundleOptions.ChunkBasedCompression
                | BuildAssetBundleOptions.StrictMode | BuildAssetBundleOptions.ForceRebuildAssetBundle, BuildTarget.StandaloneWindows64) == null)
                throw new InvalidOperationException("Luxury map bundle build failed.");
            Debug.Log("HOTEL_LUXURY_FINISH_BUILD_PASS"); EditorApplication.Exit(cfg.zero);
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(Convert.ToInt32(true)); }
    }
}
