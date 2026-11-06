using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Illusion.Component.Correct;
using Illusion.Component.Correct.Process;
using RootMotion.FinalIK;
using UnityEngine;

namespace MainGameSamabakeMotion
{
    // Configures the game's existing solvers only at selection/state/shape events.
    // MotionIK.Calc itself is not a frame callback. Existing native solver processes
    // continue to follow the animated PV goals; no custom update loop is installed.
    internal sealed class NativeContactRig : IContactRig
    {
        private readonly ChaControl _actor;
        private readonly ContactDefinition _definition;
        private readonly Dictionary<string, Transform> _bones;
        private readonly Dictionary<BaseData, DataSnapshot> _data = new Dictionary<BaseData, DataSnapshot>();
        private readonly Dictionary<Behaviour, bool> _processes = new Dictionary<Behaviour, bool>();
        private readonly Dictionary<BaseCorrect.Info, bool> _enables = new Dictionary<BaseCorrect.Info, bool>();
        private readonly List<Action> _restoreWeights = new List<Action>();
        private readonly Dictionary<string, BaseCorrect.Info> _frames = new Dictionary<string, BaseCorrect.Info>();
        private IDictionary<string, IContactRig> _rigs;
        private MotionIK _ik;
        private static readonly HashSet<MotionIK> Imported = new HashSet<MotionIK>();
        private sealed class DataSnapshot { internal Transform Bone; internal Vector3 Position; internal Quaternion Rotation; }
        internal static bool IsImported(MotionIK ik) => Imported.Contains(ik);
        internal static IContactRig Capture(ChaControl actor, ContactDefinition definition) => definition == null ? null : new NativeContactRig(actor, definition);
        private NativeContactRig(ChaControl actor, ContactDefinition definition)
        {
            _actor = actor; _definition = definition;
            _bones = actor.animBody.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            foreach (var value in actor.animBody.GetComponentsInChildren<BaseData>(true))
                _data[value] = new DataSnapshot { Bone = value.bone, Position = value.pos, Rotation = value.rot };
            foreach (var value in actor.animBody.GetComponentsInChildren<BaseProcess>(true)) _processes[value] = value.enabled;
            foreach (var owner in actor.animBody.GetComponentsInChildren<BaseCorrect>(true))
                foreach (var info in owner.list) _enables[info] = info.enabled;
            var solver = actor.animBody.GetComponent<FullBodyBipedIK>();
            if (solver == null) throw new InvalidDataException("KKSの身体IKがありません");
            foreach (var pair in MotionIK.IKTargetPair.GetPairs(solver.solver))
            {
                var effector = pair.effector; var bend = pair.bend;
                float pos = effector.positionWeight, ang = effector.rotationWeight, weight = bend.weight;
                _restoreWeights.Add(() => { effector.positionWeight = pos; effector.rotationWeight = ang; bend.weight = weight; });
            }
        }
        public float Height => _definition.height.Map(Find(_definition.height.bone).localScale.y);
        private Transform Find(string name) => _bones.TryGetValue(name, out var bone) ? bone : throw new InvalidDataException("接触補正の骨がありません: " + name);
        public void Configure(IDictionary<string, IContactRig> rigs)
        {
            _rigs = rigs;
            _ik = new MotionIK(_actor);
            Imported.Add(_ik);
            for (int i = 0; i < _ik.frameCorrect.GetFrameNames.Length; i++)
                _frames.Add(_ik.frameCorrect.GetFrameNames[i], _ik.frameCorrect.list[i]);
            // Assign all instances first; partners are resolved lazily in Apply.
        }
        public void Apply(string stateName)
        {
            if (_ik == null) return;
            var state = _definition.states.SingleOrDefault(s => s.name == stateName);
            if (state == null) throw new InvalidDataException("接触補正の状態がありません: " + stateName);
            var partners = _definition.partners.Select(s => (NativeContactRig)_rigs[s]).ToArray();
            _ik.SetPartners(partners.Select(r => r._ik));
            var native = new MotionIKData.State { name = stateName, frames = new MotionIKData.Frame[0] };
            for (int i = 0; i < 4; i++)
            {
                var part = state.parts[i];
                var p = native.parts[i];
                p.param2.sex = Array.IndexOf(_definition.partners, part.slot);
                p.param2.target = part.bone ?? "";
                p.param2.weightPos = part.position; p.param2.weightAng = part.rotation;
                p.param3.chein = part.bend ?? ""; p.param3.weight = part.bendWeight;
                if (!string.IsNullOrEmpty(part.bone)) ((NativeContactRig)_rigs[part.slot]).Find(part.bone);
                if (!string.IsNullOrEmpty(part.bend)) Find(part.bend);
            }
            _ik.data.states = new[] { native };
            _ik.frameCorrect.enabled = true; _ik.ikCorrect.enabled = true;
            _ik.Calc(stateName);
            foreach (var frame in state.frames)
            {
                var actor = (NativeContactRig)_rigs[frame.slot];
                Vector3 pos = Vector3.zero, ang = Vector3.zero;
                foreach (var shape in frame.shapes)
                {
                    float value = shape.sourceIndex == 0 ? actor.Height : shape.indices == null ? actor._actor.GetShapeBodyValue(shape.index)
                        : shape.indices.Select((index, i) => actor._actor.GetShapeBodyValue(index) * shape.weights[i]).Sum();
                    pos += Interpolate(shape.smallPos, shape.mediumPos, shape.largePos, value, false) * frame.scale;
                    ang += Interpolate(shape.smallAng, shape.mediumAng, shape.largeAng, value, true);
                }
                if (frame.index < 8)
                {
                    var pair = _ik.ikTargetPairs[frame.index / 2];
                    var target = frame.index % 2 == 0 ? pair.effector.target : pair.bend.bendGoal;
                    var data = target.GetComponent<BaseData>();
                    if (data == null) throw new InvalidDataException("接触先の補正データがありません");
                    data.pos = pos; data.ang = ang;
                }
                else
                {
                    if (!_frames.TryGetValue(frame.bone, out var correction)) continue; // Explicitly reported as unsupported in catalog.
                    correction.enabled = true; correction.pos = pos; correction.ang = ang;
                }
            }
        }
        private static Vector3 Interpolate(float[] small, float[] medium, float[] large, float value, bool angle)
        {
            var result = new Vector3();
            for (int i = 0; i < 3; i++) result[i] = angle ? ContactShape.Angle(small[i], medium[i], large[i], value)
                : ContactShape.Lerp(small[i], medium[i], large[i], value);
            return result;
        }
        public void Restore()
        {
            if (_ik != null) Imported.Remove(_ik);
            bool alive = _actor != null && _actor.animBody != null;
            _frames.Clear();
            foreach (var item in _data) if (item.Key != null) { item.Key.bone = item.Value.Bone; item.Key.pos = item.Value.Position; item.Key.rot = item.Value.Rotation; }
            foreach (var item in _processes) if (item.Key != null) item.Key.enabled = item.Value;
            if (alive) foreach (var item in _enables) item.Key.enabled = item.Value;
            foreach (var restore in _restoreWeights) restore();
            _ik = null; _rigs = null;
        }
    }
}
