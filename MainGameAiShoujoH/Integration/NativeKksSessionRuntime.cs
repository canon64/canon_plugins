using System;
using System.Globalization;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using MainGameAiShoujoH.Contracts;
using UnityEngine;

namespace MainGameAiShoujoH.Integration
{
    // One transient native state owner for body/progression/voice. It never
    // registers a Heroine in Game or writes the actor's card/save profile.
    internal sealed class NativeKksSessionRuntime : ISessionModule
    {
        GameObject root;
        bool disposed;
        readonly string defaultsPath;
        string tableBundle;
        internal NativeKksSessionRuntime(string defaultsPath) { this.defaultsPath = defaultsPath; }
        internal HFlag Flags { get; private set; }
        internal HandCtrl Hand { get; private set; }
        internal SaveData.Heroine Heroine { get; private set; }
        internal NativeKksProtocol Protocol { get; private set; }
        internal NativeKksOwnedScene Engine { get; private set; }
        internal HSceneProc.AnimationListInfo SelectedAnimation { get; private set; }
        internal readonly HashSet<HFlag.EMode> SelectedModes = new HashSet<HFlag.EMode>();
        internal void ObserveSelection(HSceneProc.AnimationListInfo info)
        { if (info != null) { SelectedAnimation = info; SelectedModes.Add(info.mode); } }
        internal int? FinishAdv { get; set; }
        internal IReadOnlyDictionary<string, object> EndingVariables { get; set; } = new Dictionary<string, object>();
        public string Name => "kks-native-state";
        public void Prepare(SessionContext context)
        {
            if (root != null || disposed) throw new InvalidOperationException("KKS native state ownership is invalid.");
            Presentation.KksNativePoseDefinition.ValidateIdentity(context.Request.KksPose);
            root = new GameObject("KKS native state " + context.Id);
            root.SetActive(false);
            Flags = root.AddComponent<HFlag>(); Flags.enabled = false;
            ApplySerializedDefaults();
            Hand = root.AddComponent<HandCtrl>(); Hand.enabled = false; Hand.flags = Flags;
            Heroine = new SaveData.Heroine(false);
            Flags.lstHeroine.Add(Heroine);
            Flags.mode = (HFlag.EMode)context.Request.KksPose.Mode;
            tableBundle = context.Request.KksPose.TableBundle;
            var table = CommonLib.LoadAsset<AnimationInfoData>(tableBundle, context.Request.KksPose.TableAsset);
            if (table == null) throw new InvalidDataException("Installed KKS animation table is missing.");
            var row = table.param.Single(p => p.id == context.Request.KksPose.Id);
            Flags.nowAnimationInfo = new HSceneProc.AnimationListInfo();
            CopyFields(row, Flags.nowAnimationInfo);
            Flags.nowAnimationInfo.mode = Flags.mode;
            SelectedAnimation = Flags.nowAnimationInfo;
            // Native ChangeAnimator clears the pending selection once applied.
            Flags.selectAnimationListInfo = null;
        }
        public void Execute(Command command) { }
        public void End(EndReason reason) { }
        internal void AdoptEngine(NativeKksOwnedScene engine, HFlag flags, HandCtrl hand, SaveData.Heroine heroine)
        {
            if (Engine != null || engine == null || flags == null || hand == null || heroine == null)
                throw new InvalidOperationException("Native engine state can be adopted exactly once.");
            Engine = engine; Flags = flags; Hand = hand; Heroine = heroine;
        }
        internal void CaptureEnding()
        {
            if (FinishAdv.HasValue) return;
            if (Engine?.Proc == null) throw new InvalidOperationException("Native ending requires the owned HProc.");
            var categories = Engine.Proc.categorys;
            // Normal one-woman/one-man owned scene: no ActionScene witness or
            // girlfriend-affinity branch. Preserve native EndProc's decision order
            // without calling its normal-game save and character-result writes.
            bool orgasm = Flags.GetOrgCount() != 0 || categories.Contains(1200) || categories.Contains(12) || categories.Contains(1306);
            FinishAdv = NativeKksEndingDecision.Select(Flags.count.isInsertKokan && Heroine.isVirgin,
                Flags.GetOrgCount(), Flags.GetInsideAndOutsideCount(), categories, (int)Heroine.HExperience, Heroine.lewdness,
                (bool)typeof(HSceneProc).GetField("isInvited", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Engine.Proc));
            EndingVariables = new Dictionary<string, object> { { "Orgasm", orgasm } };
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Flags = null; Hand = null; Heroine = null;
            if (root != null) { root.transform.SetParent(null, false); UnityEngine.Object.Destroy(root); root = null; }
            if (tableBundle != null) { AssetBundleManager.UnloadAssetBundle(tableBundle, false); tableBundle = null; }
        }
        static void CopyFields(object source, object target)
        {
            foreach (var field in source.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                var destination = target.GetType().GetField(field.Name, BindingFlags.Instance | BindingFlags.Public);
                if (destination != null && destination.FieldType == field.FieldType) destination.SetValue(target, CloneData(field.GetValue(source)));
            }
        }
        static object CloneData(object value)
        {
            if (value == null) return null;
            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type.IsValueType || value is string) return value;
            if (value is Array array) { var clone = (Array)array.Clone(); for (int i = default(int); i < clone.Length; ++i) clone.SetValue(CloneData(array.GetValue(i)), i); return clone; }
            if (value is IList list) { var clone = (IList)Activator.CreateInstance(type); foreach (var item in list) clone.Add(CloneData(item)); return clone; }
            if (type.DeclaringType != typeof(HSceneProc)) throw new InvalidDataException("Unexpected native animation metadata type: " + type.FullName);
            var result = Activator.CreateInstance(type); CopyFields(value, result); return result;
        }
        void ApplySerializedDefaults()
        {
            var data = Motion.DedicatedMotionModule.ReadCatalog<Defaults>(defaultsPath);
            if (data?.scalars == null || data.curves == null || data.protocol == null || !(data.protocol.gaugeMax > data.protocol.orgasmBoundary) || string.IsNullOrWhiteSpace(data.sourceSha256)) throw new InvalidDataException("Native KKS HFlag serialized defaults are missing.");
            Protocol = data.protocol;
            foreach (var scalar in data.scalars)
            {
                var field = typeof(HFlag).GetField(scalar.name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null || field.IsInitOnly || (!field.FieldType.IsPrimitive && !field.FieldType.IsEnum)) throw new InvalidDataException("Native HFlag scalar contract differs: " + scalar.name);
                object value = field.FieldType.IsEnum ? Enum.ToObject(field.FieldType, int.Parse(scalar.value, CultureInfo.InvariantCulture)) : Convert.ChangeType(scalar.value, field.FieldType, CultureInfo.InvariantCulture);
                field.SetValue(Flags, value);
            }
            foreach (var source in data.curves)
            {
                if (source.name != nameof(HFlag.speedHoushiCurve) && source.name != nameof(HFlag.speedSonyuCurve)) throw new InvalidDataException("Unexpected native speed curve: " + source.name);
                if (source.keys == null || source.keys.Length == default(int)) throw new InvalidDataException("Native speed curve is empty.");
                var keys = new Keyframe[source.keys.Length];
                for (int i = default(int); i < keys.Length; ++i)
                {
                    var key = source.keys[i];
                    keys[i] = new Keyframe(key.time, key.value, key.inSlope, key.outSlope, key.inWeight, key.outWeight) { weightedMode = (WeightedMode)key.weightedMode };
                }
                // HHoushi SpeedUpClick bounds speedCalc to [0,1]. Preserve every
                // native key/tangent/weight; raw infinity metadata is retained in
                // the artifact, without assuming it equals WrapMode's enum.
                typeof(HFlag).GetField(source.name).SetValue(Flags, new AnimationCurve(keys));
            }
            if (Flags.speedHoushiCurve == null || Flags.speedSonyuCurve == null) throw new InvalidDataException("Native KKS speed curves are incomplete.");
        }
        [DataContract] sealed class Defaults { [DataMember] public string sourceSha256 { get; set; } [DataMember] public Scalar[] scalars { get; set; } [DataMember] public Curve[] curves { get; set; } [DataMember] public NativeKksProtocol protocol { get; set; } }
        [DataContract] sealed class Scalar { [DataMember] public string name { get; set; } [DataMember] public string value { get; set; } }
        [DataContract] sealed class Curve { [DataMember] public string name { get; set; } [DataMember] public Key[] keys { get; set; } }
        [DataContract] sealed class Key
        {
            [DataMember] public float time { get; set; } [DataMember] public float value { get; set; }
            [DataMember] public float inSlope { get; set; } [DataMember] public float outSlope { get; set; }
            [DataMember] public float inWeight { get; set; } [DataMember] public float outWeight { get; set; }
            [DataMember] public int weightedMode { get; set; }
        }
    }
    [DataContract] internal sealed class NativeKksProtocol
    {
        [DataMember] public float gaugeMax { get; set; } [DataMember] public float orgasmBoundary { get; set; }
        [DataMember] public float clipEnd { get; set; } [DataMember] public float outsideLoopEnd { get; set; }
        [DataMember] public int finishWithoutOrgasm { get; set; } [DataMember] public int finishWithoutOrgasmLewd { get; set; }
    }
}
