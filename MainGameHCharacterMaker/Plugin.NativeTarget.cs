using System;
using System.Collections.Generic;
using UnityEngine;

namespace MainGameHCharacterMaker
{
    public sealed partial class Plugin
    {
        private List<MotionIK> _motionIks;
        private ChaControl _male, _male1;
        private ItemObject _item;

        private void CacheMotionActors()
        {
            _motionIks = ReadScene<List<MotionIK>>(_scene, "lstMotionIK");
            _male = ReadScene<ChaControl>(_scene, "male");
            _male1 = ReadScene<ChaControl>(_scene, "male1");
            _item = ReadScene<ItemObject>(_scene, "item");
        }

        private void ClearMotionActors() { _motionIks = null; _male = null; _male1 = null; _item = null; }

        private void RefreshHBody()
        {
            if (_scene == null || _actors.Count == 0) return;
            var first = _actors[0];
            if (first == null) return;
            var second = _actors.Count > 1 ? _actors[1] : null;
            float h0 = first.GetShapeBodyValue(0), b0 = first.GetShapeBodyValue(4);
            float h1 = second != null ? second.GetShapeBodyValue(0) : h0;
            float b1 = second != null ? second.GetShapeBodyValue(4) : b0;
            ShapeParameters(first, h0, b0, h1, b1);
            ShapeParameters(second, h1, b1, h0, b0);
            if (_male != null) _male.setAnimatorParamFloat("height", h0);
            if (_male1 != null) _male1.setAnimatorParamFloat("height", h0);
            if (_item != null)
            {
                bool secondPrimary = second != null && _scene.flags?.nowAnimationInfo != null
                    && (_scene.flags.nowAnimationInfo.id % 2) != 0;
                _item.SetAnimatorParamFloat("height", secondPrimary ? h1 : h0);
                _item.SetAnimatorParamFloat("height1", secondPrimary ? h0 : h1);
                _item.SetAnimatorParamFloat("Breast", secondPrimary ? b1 : b0);
                _item.SetAnimatorParamFloat("Breast1", secondPrimary ? b0 : b1);
            }
            if (_motionIks == null) return;
            string state = _scene.flags?.nowAnimStateName;
            if (string.IsNullOrEmpty(state)) return;
            foreach (var ik in _motionIks) ik?.Calc(state);
        }

        private static void ShapeParameters(ChaControl actor, float height, float breast, float otherHeight, float otherBreast)
        {
            if (actor == null) return;
            actor.setAnimatorParamFloat("height", height);
            actor.setAnimatorParamFloat("Breast", breast);
            actor.setAnimatorParamFloat("height1", otherHeight);
            actor.setAnimatorParamFloat("Breast1", otherBreast);
        }

        private sealed class NativeTarget : IShapeTarget
        {
            private readonly Plugin _owner;
            private readonly ChaControl _actor;
            private readonly ChaFileCustom _custom;
            private readonly float[] _bodyValues, _faceValues;
            internal NativeTarget(Plugin owner, ChaControl actor)
            {
                _owner = owner; _actor = actor; _custom = actor.chaFile.custom;
                _bodyValues = _custom.body.shapeValueBody; _faceValues = _custom.face.shapeValueFace;
            }
            public bool IsAlive => _actor != null && ReferenceEquals(_actor.chaFile?.custom, _custom)
                && ReferenceEquals(_custom.body?.shapeValueBody, _bodyValues)
                && ReferenceEquals(_custom.face?.shapeValueFace, _faceValues);
            public float[] Read(bool body)
            {
                if (!body) return _actor.chaFile.custom.face.shapeValueFace;
                var data = _actor.chaFile.custom.body;
                var result = new float[data.shapeValueBody.Length + 3];
                Array.Copy(data.shapeValueBody, result, data.shapeValueBody.Length);
                result[result.Length - 3] = data.bustSoftness;
                result[result.Length - 2] = data.bustWeight;
                result[result.Length - 1] = data.areolaSize;
                return result;
            }
            public bool Write(bool body, int index, float value)
            {
                if (!IsAlive) return false;
                if (!body) return _actor.SetShapeFaceValue(index, value);
                var data = _actor.chaFile.custom.body;
                int extra = index - data.shapeValueBody.Length;
                if (extra < 0) return _actor.SetShapeBodyValue(index, value);
                switch (extra)
                {
                    case 0: data.bustSoftness = value; return true;
                    case 1: data.bustWeight = value; return true;
                    case 2: data.areolaSize = value; return true;
                    default: return false;
                }
            }
            public void Refresh(bool body, bool face)
            {
                if (!IsAlive) return;
                if (body)
                {
                    _actor.UpdateShapeBodyValueFromCustomInfo();
                    _actor.UpdateBustSoftnessAndGravity();
                    _actor.ChangeSettingAreolaSize();
                    _owner.RefreshHBody();
                }
                if (face) _actor.UpdateShapeFaceValueFromCustomInfo();
            }
        }
    }
}
