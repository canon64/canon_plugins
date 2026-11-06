using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace MainGameHLocationPlacement
{
    // Validate only the members used by placement. UI and hotel additions need no new DLL pin.
    internal static class ComposerContract
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        internal static void Check(Type plugin)
        {
            Type Model(string name) => plugin.Assembly.GetType("MainGameObjectComposer." + name, true);
            Check(plugin, Model("PresetEntry"), Model("ManagedObjectData"), Model("RuntimeObjectRef"), Model("MovementFieldRule"), typeof(ChaControl));
        }
        internal static void Check(Type plugin, Type preset, Type data, Type runtimeObject, Type rule, Type actor)
        {
            foreach (string name in new[] { "_presetSource", "_presetDraft", "_activePreset", "_movementReturnPreset" }) Field(plugin, name, preset);
            Field(plugin, "_presets", typeof(List<>).MakeGenericType(preset));
            Field(plugin, "_runtimeObjects", typeof(Dictionary<,>).MakeGenericType(typeof(string), runtimeObject));
            foreach (string name in new[] { "_activeL1ById", "_activeL2ById" }) Field(plugin, name, typeof(Dictionary<,>).MakeGenericType(typeof(string), data));
            foreach (string name in new[] { "_presetSaveSucceeded", "_presetSaveFailed" }) Field(plugin, name, typeof(bool));
            Field(plugin, "_presetsPath", typeof(string)); Field(plugin, "_motionIntensityLevel", typeof(int));
            var runtime = MemberType(plugin, "_runtime");
            MemberType(runtime, "MainFemale", actor);
            MemberType(MemberType(runtime, "Flags"), "motion", typeof(float));
            MemberType(MemberType(plugin, "_presetEditorState"), "FixedSlot", typeof(int));
            foreach (string name in new[] { "key", "displayName" }) Field(preset, name, typeof(string));
            Field(preset, "useLoopSplit", typeof(bool));
            foreach (string name in new[] { "loop1Json", "loop2Json", "ikLinks", "motionPositionRulesJson", "movementRulesJson" }) Field(preset, name, typeof(List<string>));
            foreach (string name in new[] { "id", "parentKind", "parentRefId", "parentId" }) Field(data, name, typeof(string));
            foreach (string name in new[] { "localPosition", "localEulerAngles", "pistonAxis" }) Field(data, name, typeof(Vector3));
            Field(data, "motionMode", typeof(int));
            MemberType(runtimeObject, "Data", data); MemberType(runtimeObject, "GameObject", typeof(GameObject));
            foreach (string name in new[] { "objectId", "field" }) Field(rule, name, typeof(string));
            foreach (string name in new[] { "enabled", "l1ZeroSet", "l1FullSet", "l2ZeroSet", "l2FullSet" }) Field(rule, name, typeof(bool));
            foreach (string name in new[] { "l1Zero", "l1Full", "l2Zero", "l2Full" }) Field(rule, name, typeof(float));
            if (rule.GetConstructor(Flags, null, Type.EmptyTypes, null) == null) throw Missing(rule, ".ctor");

            Method(plugin, "SaveCurrentToPreset", typeof(void), preset, typeof(string), typeof(int));
            Method(plugin, "CommitPresetDraft", typeof(bool), typeof(bool), typeof(string));
            Method(plugin, "SaveJsonAtomic", typeof(void), typeof(string), typeof(string), typeof(bool));
            foreach (string name in new[] { "SavePresets", "SaveMotionPresetMutation", "ApplyPresetEditorFixedSlot", "DeactivatePreset", "RebuildActivePresetCache" }) Method(plugin, name, typeof(void));
            Method(plugin, "ActivatePreset", typeof(void), preset);
            Method(plugin, "LoadPresetSlot", typeof(void), preset, typeof(bool));
            Method(plugin, "GetEffectiveMotionPositionOffset", typeof(Vector3), typeof(string));
            Method(plugin, "ApplyMovementPose", typeof(void), runtimeObject, data, data, typeof(float));
            Method(plugin, "FindMovementField", rule, preset, typeof(string), typeof(string));
            Method(plugin, "BeginMovementEdit", typeof(void), preset, typeof(int), typeof(bool));
            Method(plugin, "EndMovementEdit", typeof(void), typeof(bool));
            Method(plugin, "ResolveConfiguredParentTransform", typeof(Transform), data, typeof(string), typeof(string).MakeByRefType(), typeof(string).MakeByRefType());
            Method(plugin, "SyncDataFromRuntime", typeof(void), runtimeObject);
            Method(plugin, "IsMotionTarget", typeof(bool), preset, typeof(string));
        }
        private static MissingMemberException Missing(Type type, string member)
            => new MissingMemberException("ComposerのH場所補正に必要な接続が変わっています: " + type.FullName + "." + member);
        private static Type MemberType(Type type, string name, Type expected = null)
        {
            var member = R.Member(type, name);
            Type actual;
            if (member is FieldInfo field && !field.IsStatic) actual = field.FieldType;
            else if (member is PropertyInfo property && property.GetGetMethod(true)?.IsStatic == false) actual = property.PropertyType;
            else throw Missing(type, name);
            if (expected != null && actual != expected) throw Missing(type, name);
            return actual;
        }
        private static void Field(Type type, string name, Type expected)
        {
            if (!(R.Member(type, name) is FieldInfo field) || field.IsStatic || field.FieldType != expected) throw Missing(type, name);
        }
        private static void Method(Type type, string name, Type result, params Type[] args)
        {
            var methods = type.GetMethods(Flags).Where(m => m.Name == name).ToArray();
            if (methods.Length != 1 || methods[0].IsStatic || methods[0].ContainsGenericParameters || methods[0].ReturnType != result
                || !methods[0].GetParameters().Select(p => p.ParameterType).SequenceEqual(args)) throw Missing(type, name);
        }
    }
}
