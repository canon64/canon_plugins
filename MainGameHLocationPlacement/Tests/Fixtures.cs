using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using State = MainGirlHipHijack.Runtime.BodyIkOperationPointState;

namespace MainGirlHipHijack.Runtime
{
    public sealed class BodyIkOperationPointState
    {
        public bool ExternalOwner, Running = true, HasPostDragHold, GizmoDragging, HasFollowDistanceBaseline;
        public Transform Proxy = new GameObject("proxy").transform, FollowBone, PendingFollowBone;
        public Vector3 PostDragHoldPos;
        public Quaternion PostDragHoldRot = Quaternion.identity;
    }
}
namespace MainGameHLocationPlacement
{
    public sealed class Data
    {
        public string id = "root", parentKind = "external", parentRefId = "char:root", parentId, name = "test";
        public Vector3 localPosition, localEulerAngles, pistonAxis = new Vector3(0,0,1);
        public int motionMode;
        public bool autoRotate, autoRotateLocalSpace = true, angleLocalSpace = true, pistonLocalSpace = true;
    }
    public sealed class ComposerPreset
    {
        public string key = "sonyu/4/S", displayName = "kijoui";
        public bool useLoopSplit = true;
        public List<string> loop1Json = new List<string>(), loop2Json = new List<string>(), ikLinks = new List<string>();
        public List<string> motionPositionRulesJson = new List<string>(), movementRulesJson = new List<string>(), motionIntensityTargetIds = new List<string>();
    }
    public sealed class RuntimeObject
    { public Data Data; public GameObject GameObject; }
    public sealed class EditorState { public int FixedSlot = 1; }
    public sealed class MovementRule
    {
        public string objectId,field;public bool enabled=true,l1ZeroSet,l1FullSet,l2ZeroSet,l2FullSet;
        public float l1Zero,l1Full,l2Zero,l2Full;
    }
    public class ComposerFixture
    {
        public sealed class RuntimeState { public ChaControl MainFemale; public RuntimeFlags Flags; }
        public sealed class RuntimeFlags { public float motion; }
        public RuntimeState _runtime;
        public List<ComposerPreset> _presets = new List<ComposerPreset>();
        public ComposerPreset _presetSource, _presetDraft, _activePreset;
        public ComposerPreset _movementReturnPreset;
        public int _motionIntensityLevel=2,EvaluationSlot=2;
        public float Motion=1f;
        public string _presetsPath;
        public bool _presetSaveSucceeded, _presetSaveFailed, FailWrite;
        public Dictionary<string, RuntimeObject> _runtimeObjects = new Dictionary<string, RuntimeObject>();
        public Dictionary<string, Data> _activeL1ById, _activeL2ById;
        public EditorState _presetEditorState = new EditorState();
        public Transform NativeParent = new GameObject("female").transform;
        public Data NextData = new Data();
        [MethodImpl(MethodImplOptions.NoInlining)] public void SaveCurrentToPreset(ComposerPreset target = null, string name = null, int loop = 0)
        {
            if (target == null) { target = new ComposerPreset { displayName = name ?? "new" }; _presets.Add(target); }
            if (loop != 2) target.loop1Json = new List<string> { JsonUtility.ToJson(NextData) };
            if (loop != 1) target.loop2Json = new List<string> { JsonUtility.ToJson(NextData) };
            SavePresets();
        }
        [MethodImpl(MethodImplOptions.NoInlining)] public bool CommitPresetDraft(bool keepEditing, string newName)
        {
            var candidate = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<ComposerPreset>(JsonUtility.ToJson(_presetDraft));
            int index = newName != null ? _presets.Count : _presets.IndexOf(_presetSource);
            if (index < 0) return false;
            if (newName != null) candidate.displayName = newName;
            if (index == _presets.Count) _presets.Add(candidate); else _presets[index] = candidate;
            SavePresets();return _presetSaveSucceeded;
        }
        [MethodImpl(MethodImplOptions.NoInlining)] public void SavePresets()
        {
            _presetSaveSucceeded = false;
            try { SaveJsonAtomic(_presetsPath, JsonUtility.ToJson(new { presetsJson = _presets.Select(JsonUtility.ToJson).ToArray() }), true);_presetSaveSucceeded=true; }
            catch (Exception) { }
        }
        [MethodImpl(MethodImplOptions.NoInlining)] public void SaveJsonAtomic(string path, string json, bool backup)
        {
            if (FailWrite) throw new IOException("simulated Composer write failure");
            string temp=path+".tmp";File.WriteAllText(temp,json);
            if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
        }
        [MethodImpl(MethodImplOptions.NoInlining)] public void ActivatePreset(ComposerPreset preset) { _activePreset=preset;RebuildActivePresetCache(); }
        [MethodImpl(MethodImplOptions.NoInlining)] public void LoadPresetSlot(ComposerPreset preset, bool second) { }
        [MethodImpl(MethodImplOptions.NoInlining)] public void ApplyPresetEditorFixedSlot() { }
        [MethodImpl(MethodImplOptions.NoInlining)] public void DeactivatePreset() { _activePreset=null; }
        [MethodImpl(MethodImplOptions.NoInlining)] public void RebuildActivePresetCache()
        {
            if(_activePreset==null)return;
            _activeL1ById=_activePreset.loop1Json.Select(s=>(Data)JsonUtility.FromJson(s,typeof(Data))).ToDictionary(d=>d.id);
            _activeL2ById=_activePreset.loop2Json.Select(s=>(Data)JsonUtility.FromJson(s,typeof(Data))).ToDictionary(d=>d.id);
        }
        [MethodImpl(MethodImplOptions.NoInlining)] public Transform ResolveConfiguredParentTransform(Data data,string self,out string kind,out string reference)
        {kind=data.parentKind;reference=data.parentRefId;return NativeParent;}
        [MethodImpl(MethodImplOptions.NoInlining)] public void SyncDataFromRuntime(RuntimeObject value)
        {value.Data.localPosition=value.GameObject.transform.localPosition;value.Data.localEulerAngles=value.GameObject.transform.localEulerAngles;value.Data.parentKind="root";value.Data.parentRefId="";}
        [MethodImpl(MethodImplOptions.NoInlining)] public void DrawMainBody() { }
        public bool IsMotionTarget(ComposerPreset preset,string id)=>preset.motionIntensityTargetIds.Contains(id);
        [MethodImpl(MethodImplOptions.NoInlining)] public void SaveMotionPresetMutation() { }
        [MethodImpl(MethodImplOptions.NoInlining)] public Vector3 GetEffectiveMotionPositionOffset(string id) => Vector3.zero;
        [MethodImpl(MethodImplOptions.NoInlining)] public MovementRule FindMovementField(ComposerPreset preset,string id,string field)
        {return preset.movementRulesJson.Select(s=>(MovementRule)JsonUtility.FromJson(s,typeof(MovementRule))).FirstOrDefault(r=>r.objectId==id&&r.field==field);}
        [MethodImpl(MethodImplOptions.NoInlining)] public void ApplyMovementPose(RuntimeObject rr,Data first,Data second,float motion)
        {
            float Value(string field,float baseline){var rule=FindMovementField(_activePreset,rr.Data.id,field);return rule==null?baseline:EvaluationSlot==2?rule.l2Zero:rule.l1Zero;}
            rr.Data.localPosition=new Vector3(Value("px",second.localPosition.x),Value("py",second.localPosition.y),Value("pz",second.localPosition.z));
        }
        [MethodImpl(MethodImplOptions.NoInlining)] public void BeginMovementEdit(ComposerPreset preset,int slot,bool full) { }
        [MethodImpl(MethodImplOptions.NoInlining)] public void EndMovementEdit(bool capture) { }
    }
    public sealed class HipEntry
    {
        public float weight = 1;
        public bool enabled = true, hasProxyPose = true, hasFollowBone, followIsHmd;
        public string followBonePath;
        public Vector3 proxyPosition;
        public Quaternion proxyRotation = Quaternion.identity;
    }
    public sealed class HipPreset
    {public string id=Guid.NewGuid().ToString("N"),name="hands";public int postureMode=1,postureId=4;public HipEntry[] entries=Enumerable.Range(0,4).Select(_=>new HipEntry()).ToArray();}
    public sealed class HipSettings
    {public bool[] FollowComposerEnabled=new bool[4];public string[] FollowComposerObjectIds=new string[4];}
    public sealed class Point
    {
        public int Index;public bool UseFollowLocalTransition,HasPose=true;
        public Vector3 StartPos,TargetPos;
        public Quaternion StartRot=Quaternion.identity,TargetRot=Quaternion.identity;
    }
    public sealed class HipFixture
    {
        public string _posePresetIndexPath;
        public List<HipPreset> _posePresets=new List<HipPreset>();
        public HipSettings _settings=new HipSettings();
        public object[] _runtimeWrists=new object[2];
        public State[] States=Enumerable.Range(0,4).Select(_=>new State()).ToArray();
        public List<Point> _activeTransitionPoints=new List<Point>();
        public HipPreset NextPreset=new HipPreset(),LastApplied;
        public List<int> Touched=new List<int>();
        public string[] Keys={"leftHand","rightHand","body","leftFoot"};
        public Action PrepareRuntime, BeforeSnapshot, BeforeWrite;
        public string SingleJson(HipPreset preset)=>JsonUtility.ToJson(new {presets=new[]{preset}});
        [MethodImpl(MethodImplOptions.NoInlining)] public void SaveCurrentPosePresetWithScreenshot(string name)
        {PrepareRuntime?.Invoke();_posePresets.Insert(0,BuildCurrentPosePresetSnapshot(name));BeforeWrite?.Invoke();SavePosePresetIndex();}
        [MethodImpl(MethodImplOptions.NoInlining)] public HipPreset BuildCurrentPosePresetSnapshot(string name)
        {BeforeSnapshot?.Invoke();return NextPreset;}
        [MethodImpl(MethodImplOptions.NoInlining)] public void OverwritePosePresetById(string id)
        {PrepareRuntime?.Invoke();int i=_posePresets.FindIndex(p=>p.id==id);if(i<0)return;var snapshot=BuildCurrentPosePresetSnapshot(_posePresets[i].name);snapshot.id=id;_posePresets[i]=snapshot;BeforeWrite?.Invoke();SavePosePresetIndex();}
        [MethodImpl(MethodImplOptions.NoInlining)] public void SavePosePresetIndex()
        {try{File.WriteAllText(_posePresetIndexPath,JsonUtility.ToJson(new{presets=_posePresets}),Encoding.UTF8);}catch(Exception){} }
        [MethodImpl(MethodImplOptions.NoInlining)] public bool ApplyPosePreset(HipPreset preset,bool require,string reason)
        {
            LastApplied=preset;Touched.Clear();_activeTransitionPoints=new List<Point>();
            for(int i=0;i<States.Length;i++)
            {
                if(i==2)continue;
                if(States[i].ExternalOwner)continue;
                Touched.Add(i);States[i].Running=preset.entries[i].enabled;
                _activeTransitionPoints.Add(new Point{Index=i,StartPos=States[i].Proxy.position,StartRot=States[i].Proxy.rotation,
                    TargetPos=preset.entries[i].proxyPosition,TargetRot=preset.entries[i].proxyRotation});
            }
            return true;
        }
        [MethodImpl(MethodImplOptions.NoInlining)] public void DrawPosePresetSection() { }
    }
    public sealed class NativeScene
    {
        public Action Move;
        [MethodImpl(MethodImplOptions.NoInlining)] public void ChangeAnimator(){SetLocalPosition();}
        [MethodImpl(MethodImplOptions.NoInlining)] public void SetLocalPosition(){Move?.Invoke();}
    }
}
