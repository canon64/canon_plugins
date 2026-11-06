using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using CanonGate;
using MainGameHLocationCoordinates;
using UnityEngine;

namespace MainGameHLocationPlacement
{
    internal sealed class RuntimeBridge
    {
        internal const string ComposerGuid = "com.kks.main.gameobjectcomposer", HipGuid = "com.kks.main.girlbodyikgizmo", MapGuid = "com.kks.maingameblankmapadd";
        internal readonly BaseUnityPlugin Composer, Hip, Map;
        internal readonly Type PresetType, DataType, HipPresetType;
        internal readonly string[] IkKeys;
        private readonly System.Reflection.MethodInfo _isMotionTarget;
        private readonly object _scene;
        private readonly BaseMap _ownedMap;
        private readonly bool _hotel, _ai;
        private readonly string _roomId;
        private readonly System.Reflection.MemberInfo _nativePoint;
        internal RuntimeBridge()
        {
            Composer = Find(ComposerGuid); Hip = Find(HipGuid); Map = Find(MapGuid);
            ComposerContract.Check(Composer.GetType()); Compatibility.CheckMap(Map.GetType());
            PresetType = Composer.GetType().Assembly.GetType("MainGameObjectComposer.PresetEntry", true);
            DataType = Composer.GetType().Assembly.GetType("MainGameObjectComposer.ManagedObjectData", true);
            HipPresetType = Hip.GetType().GetNestedType("PosePresetRuntime", System.Reflection.BindingFlags.NonPublic) ?? throw new TypeLoadException("PosePresetRuntime");
            _isMotionTarget = R.Method(Composer.GetType(), "IsMotionTarget", PresetType, typeof(string));
            _scene = GateApi.Current.Scene;
            _hotel = GateApi.Current.IsHotelMode; _ai = GateApi.Current.IsAiShoujoMode; _roomId = GateApi.Current.HotelRoomId;
            _ownedMap = (_hotel || _ai) ? GateApi.Current.Scene.GetComponent<BaseMap>() : null;
            try { _nativePoint = R.Member(_scene.GetType(), "nowHpointData"); } catch (MissingMemberException) { }
            IkKeys = ((IEnumerable)R.Call(Hip.GetType(), "GetEffectorKeys")).Cast<string>().ToArray();
            foreach (string member in new[] { "_runtime", "_runtimeObjects", "_presets", "_presetsPath", "_presetSaveSucceeded", "_presetSource", "_presetDraft", "_activePreset", "_activeL1ById", "_activeL2ById" }) R.Member(Composer.GetType(), member);
            foreach (string member in new[] { "_posePresets", "_posePresetIndexPath", "_bikEff", "_settings", "_activeTransitionPoints", "_runtimeWrists" }) R.Member(Hip.GetType(), member);
            var hipSession = R.Member(Hip.GetType(), "_femaleSession");
            var hipSessionType = hipSession is System.Reflection.PropertyInfo hipProperty ? hipProperty.PropertyType : ((System.Reflection.FieldInfo)hipSession).FieldType;
            var hipCharacter = R.Member(hipSessionType, "Character");
            var hipCharacterType = hipCharacter is System.Reflection.PropertyInfo characterProperty ? characterProperty.PropertyType : ((System.Reflection.FieldInfo)hipCharacter).FieldType;
            if (hipCharacterType != typeof(ChaControl)) throw new MissingMemberException("HipHijackの保存用キャラ接続が未対応です。");
        }
        internal static BaseUnityPlugin Find(string guid)
        { if (!Chainloader.PluginInfos.TryGetValue(guid, out var info) || info.Instance == null) throw new InvalidOperationException("必要なプラグインがありません: " + guid); return info.Instance; }
        internal Transform Female
        {
            get { var female = R.Get(R.Get(Composer, "_runtime"), "MainFemale") as ChaControl; if (female == null) throw new InvalidOperationException("女性キャラの基準が未準備です。"); return female.transform; }
        }
        internal static RigidPose Pose(Transform value)
        { if (value == null) throw new InvalidOperationException("座標の基準がありません。"); return new RigidPose(value.position, value.rotation); }
        internal static void Unit(Transform transform)
        { for (var p = transform; p != null; p = p.parent) if ((p.localScale - Vector3.one).sqrMagnitude > 1e-8f) throw new InvalidOperationException("非単位の基準スケールは補正対象外です。"); }
        internal IList ComposerPresets => (IList)R.Get(Composer, "_presets");
        internal IList HipPresets => (IList)R.Get(Hip, "_posePresets");
        internal IDictionary Objects => (IDictionary)R.Get(Composer, "_runtimeObjects");
        internal Array States => (Array)R.Get(Hip, "_bikEff");
        internal string ComposerPath => (string)R.Get(Composer, "_presetsPath");
        internal string HipPath => (string)R.Get(Hip, "_posePresetIndexPath");
        internal bool MotionTarget(object preset, string id) => (bool)_isMotionTarget.Invoke(Composer, new[] { preset, id });
        internal int MotionLevel => R.Get<int>(Composer, "_motionIntensityLevel");
        internal float MotionBlend
        {
            get { var flags = R.Get(R.Get(Composer, "_runtime"), "Flags"); return flags == null ? 0f : Convert.ToSingle(R.Get(flags, "motion")); }
        }
        internal Transform ObjectTransform(string id) => Objects.Contains(id) ? R.Get<GameObject>(Objects[id], "GameObject")?.transform : null;
        internal SavedPreset ComposerSnapshot(object preset) => PresetDocuments.ComposerPreset(JsonUtility.ToJson(preset));
        internal string HipJson(object preset)
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(HipPresetType)); list.Add(preset);
            return (string)R.Call(Hip.GetType(), "SerializePosePresetFileData", R.Call(Hip, "BuildPosePresetFileData", list));
        }
        internal SavedPreset HipSnapshot(object preset) => PresetDocuments.Collection(PresetDocuments.Hip, HipJson(preset)).Single();
        internal object CloneHip(object preset) => ((IList)R.Call(Hip.GetType(), "DeserializePosePresetIndex", HipJson(preset)))[0];
        internal bool Linked(int index)
        {
            var active = R.Get(Composer, "_activePreset");
            return IkEligibility.Linked(index, IkKeys, R.Get(Hip, "_settings"), active == null ? null : ((IEnumerable)R.Get(active, "ikLinks")).Cast<string>());
        }
        internal bool Independent(int index, bool allowSavedFollow = false)
        {
            return IkEligibility.Independent(States.GetValue(index), index, Linked(index), (Array)R.Get(Hip, "_runtimeWrists"), allowSavedFollow);
        }
        internal PlaceOrigin Capture(bool composerObjects = true)
        {
            var female = composerObjects ? Female : (R.Get(R.Get(Hip, "_femaleSession"), "Character") as ChaControl)?.transform;
            if (female == null) throw new InvalidOperationException("HipHijackの女性キャラが未準備です。");
            Unit(female);
            bool mapActive = R.Get<bool>(Map, "_sessionStarted") && R.Get<ChaControl>(Map, "_femaleChara")?.transform == female;
            if (mapActive && R.Get<bool>(Map, "_roomOverlayLoading")) throw new InvalidOperationException("マップ読込中は保存元を記録できません。");
            string scene = (string)R.Get(Map, "_roomOverlaySceneName");
            string bundle = (string)R.Get(Map, "_roomOverlayBundleName");
            string display = (string)R.Get(Map, "_roomOverlayDisplayName");
            var source = (_hotel || _ai) ? _ownedMap : (mapActive ? R.Get(Map, "LastReservedMap") as BaseMap : null);
            string sourceKey = source == null ? null : "source:" + source.no;
            if (!_hotel && !_ai && source?.infoDic != null && source.infoDic.TryGetValue(source.no, out var info) && info != null && !string.IsNullOrEmpty(info.AssetBundleName) && !string.IsNullOrEmpty(info.AssetName))
                sourceKey = "bundle:" + info.AssetBundleName.Replace('\\', '/') + "\nscene:" + info.AssetName;
            var map = MapContextResolver.Resolve(new MapContextInput { MapActive = mapActive, Hotel = _hotel, Ai = _ai, RoomId = _roomId,
                Scene = scene, Bundle = bundle, Display = display, SourceKey = sourceKey, SourceRoot = source?.mapRoot?.transform, SourceName = source?.mapRoot?.name,
                OverlayRoot = R.Get<GameObject>(Map, "_roomOverlayPositionRoot")?.transform, HotelPivot = R.Get<GameObject>(Map, "_vamHotelPivot")?.transform,
                BoundRoot = R.Get<Transform>(Map, "_hPlaceRoot"), BoundKey = (string)R.Get(Map, "_hPlaceKey") });
            var root = map.Root; string key = map.Key;
            var activePlace = mapActive ? R.Get(Map, "_hPlaceDraft") ?? R.Get(Map, "_hPlaceActive") : null;
            bool matchesPlace = activePlace != null && R.Get<string>(activePlace, "MapKey") == key;
            string nativePoint = _nativePoint is System.Reflection.FieldInfo pointField ? pointField.GetValue(_scene) as string :
                _nativePoint is System.Reflection.PropertyInfo pointProperty ? pointProperty.GetValue(_scene, null) as string : null;
            var femalePose = Pose(female);
            var numbers = PoseNumbers.From(femalePose);
            string nativeId = !string.IsNullOrEmpty(nativePoint) ? "native:" + nativePoint : "native:" + PresetDocuments.Hash(string.Join(",", numbers.position.Concat(numbers.rotation).Select(n => n.ToString("R", System.Globalization.CultureInfo.InvariantCulture))));
            var context = new PlaceOrigin { mapKey = key, mapName = map.Name,
                placeId = matchesPlace ? R.Get<string>(activePlace, "Id") : nativeId,
                placeName = matchesPlace ? R.Get<string>(activePlace, "Name") : nativePoint ?? "標準H場所",
                poseKey = composerObjects ? (string)R.Call(Composer, "BuildCurrentPositionKey") : "HipHijack/snapshot", utc = DateTime.UtcNow.ToString("O"),
                femaleRoot = numbers, mapRoot = PoseNumbers.From(Pose(root)) };
            if (composerObjects) foreach (DictionaryEntry item in Objects)
            {
                object data = R.Get(item.Value, "Data");
                if (R.Get<string>(data, "parentKind") == "managed") continue;
                var transform = R.Get<GameObject>(item.Value, "GameObject")?.transform;
                if (transform?.parent == null) continue;
                Unit(transform.parent);
                context.parents.Add(new ParentOrigin { objectId = (string)item.Key, kind = R.Get<string>(data, "parentKind"), reference = R.Get<string>(data, "parentRefId"), world = PoseNumbers.From(Pose(transform.parent)) });
            }
            for (int i = 0; i < IkKeys.Length; i++) if (Linked(i))
            {
                var settings = R.Get(Hip, "_settings"); var ids = R.Get<string[]>(settings, "FollowComposerObjectIds");
                context.composerLinks.Add(IkKeys[i] + "\t" + (i < ids.Length ? ids[i] : ""));
            }
            context.Validate(); return context;
        }
    }
}
