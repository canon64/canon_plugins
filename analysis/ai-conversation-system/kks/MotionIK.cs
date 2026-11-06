using System;
using System.Collections.Generic;
using System.Linq;
using Illusion;
using Illusion.Component.Correct;
using Illusion.Component.Correct.Process;
using Illusion.Extensions;
using RootMotion.FinalIK;
using UnityEngine;

public class MotionIK
{
	public class IKTargetPair
	{
		public enum IKTarget
		{
			LeftHand,
			RightHand,
			LeftFoot,
			RightFoot
		}

		public IKEffector effector { get; private set; }

		public IKConstraintBend bend { get; private set; }

		public static int IKTargetLength => Utils.Enum<IKTarget>.Length * 2;

		public static IKTargetPair[] GetPairs(IKSolverFullBodyBiped solver)
		{
			return (from index in Enumerable.Range(0, Utils.Enum<IKTarget>.Length)
				select new IKTargetPair((IKTarget)index, solver)).ToArray();
		}

		public IKTargetPair(IKTarget target, IKSolverFullBodyBiped solver)
		{
			switch (target)
			{
			case IKTarget.LeftHand:
				effector = solver.leftHandEffector;
				bend = solver.leftArmChain.bendConstraint;
				break;
			case IKTarget.RightHand:
				effector = solver.rightHandEffector;
				bend = solver.rightArmChain.bendConstraint;
				break;
			case IKTarget.LeftFoot:
				effector = solver.leftFootEffector;
				bend = solver.leftLegChain.bendConstraint;
				break;
			case IKTarget.RightFoot:
				effector = solver.rightFootEffector;
				bend = solver.rightLegChain.bendConstraint;
				break;
			}
		}
	}

	public ChaControl info { get; private set; }

	public FullBodyBipedIK ik { get; private set; }

	public MotionIK[] partners { get; private set; }

	public IKTargetPair[] ikTargetPairs
	{
		get
		{
			if (!(ik == null))
			{
				return IKTargetPair.GetPairs(ik.solver);
			}
			return null;
		}
	}

	public MotionIKData data { get; private set; }

	public FrameCorrect frameCorrect { get; private set; }

	public IKCorrect ikCorrect { get; private set; }

	public bool enabled
	{
		get
		{
			if (ik != null)
			{
				return ik.enabled;
			}
			return false;
		}
		set
		{
			if (!(ik == null))
			{
				ik.enabled = value;
				ikCorrect.isEnabled = value;
			}
		}
	}

	public string[] stateNames
	{
		get
		{
			if (data.states != null)
			{
				return data.states.Select((MotionIKData.State p) => p.name).ToArray();
			}
			return new string[0];
		}
	}

	public static List<MotionIK> Setup(List<ChaControl> infos)
	{
		List<MotionIK> ret = (from i in Enumerable.Range(0, infos.Count)
			select new MotionIK(infos[i])).ToList();
		ret.ForEach(delegate(MotionIK p)
		{
			p.SetPartners(ret);
		});
		return ret;
	}

	public static Vector3 GetShapeLerpPositionValue(float shape, Vector3 min, Vector3 max)
	{
		if (!(shape >= 0.5f))
		{
			return Vector3.Lerp(min, Vector3.zero, Mathf.InverseLerp(0f, 0.5f, shape));
		}
		return Vector3.Lerp(Vector3.zero, max, Mathf.InverseLerp(0.5f, 1f, shape));
	}

	public static Vector3 GetShapeLerpAngleValue(float shape, Vector3 min, Vector3 max)
	{
		Vector3 zero = Vector3.zero;
		if (shape >= 0.5f)
		{
			float t = Mathf.InverseLerp(0.5f, 1f, shape);
			for (int i = 0; i < 3; i++)
			{
				zero[i] = Mathf.LerpAngle(0f, max[i], t);
			}
		}
		else
		{
			float t2 = Mathf.InverseLerp(0f, 0.5f, shape);
			for (int j = 0; j < 3; j++)
			{
				zero[j] = Mathf.LerpAngle(min[j], 0f, t2);
			}
		}
		return zero;
	}

	public MotionIK(ChaControl info, MotionIKData data = null)
	{
		this.info = info;
		this.data = data ?? new MotionIKData();
		Animator animBody = info.animBody;
		ik = animBody.GetComponent<FullBodyBipedIK>();
		if (ik != null)
		{
			frameCorrect = animBody.GetComponent<FrameCorrect>();
			ikCorrect = animBody.GetComponent<IKCorrect>();
		}
		SetPartners();
		Reset();
	}

	public void SetPartners(params MotionIK[] partners)
	{
		this.partners = new MotionIK[1] { this }.Concat(partners.Where((MotionIK p) => p != this)).ToArray();
	}

	public void SetPartners(IEnumerable<MotionIK> partners)
	{
		SetPartners(partners.ToArray());
	}

	public void Reset()
	{
		InitFrameCalc();
		enabled = false;
	}

	public void Release()
	{
		data.Release();
	}

	public bool LoadData(TextAsset ta)
	{
		return data.Read(ta);
	}

	public bool LoadData(string path)
	{
		return data.Read(path);
	}

	public void InitFrameCalc()
	{
		if (frameCorrect != null)
		{
			foreach (BaseCorrect.Info item in frameCorrect.list)
			{
				item.enabled = false;
				item.pos = Vector3.zero;
				item.ang = Vector3.zero;
			}
		}
		if (!(ikCorrect != null))
		{
			return;
		}
		foreach (BaseCorrect.Info item2 in ikCorrect.list)
		{
			item2.enabled = false;
			item2.pos = Vector3.zero;
			item2.ang = Vector3.zero;
			item2.bone = null;
		}
	}

	public MotionIKData.State InitState(string stateName)
	{
		return data.InitState(stateName);
	}

	public MotionIKData.State GetNowState(string stateName)
	{
		if (data.states == null)
		{
			return null;
		}
		int num = data.states.Check((MotionIKData.State v) => v.name == stateName);
		if (num == -1)
		{
			return null;
		}
		return data.states[num];
	}

	public MotionIKData.Frame[] GetNowFrames(string stateName)
	{
		return GetNowState(stateName)?.frames;
	}

	public void Calc(string stateName)
	{
		if (frameCorrect == null)
		{
			return;
		}
		InitFrameCalc();
		MotionIKData.State nowState = GetNowState(stateName);
		if (nowState != null)
		{
			int iKTargetLength = IKTargetPair.IKTargetLength;
			MotionIKData.Frame[] frames = nowState.frames;
			foreach (MotionIKData.Frame frame in frames)
			{
				int num = frame.frameNo - iKTargetLength;
				if (num >= 0)
				{
					BaseCorrect.Info obj = frameCorrect.list[num];
					obj.enabled = true;
					GetCorrectShapeValues(partners[frame.editNo].info, frame.shapes, out var _pos, out var _ang);
					obj.pos = _pos;
					obj.ang = _ang;
				}
			}
		}
		enabled = nowState != null;
		foreach (var item in ikTargetPairs.Select((IKTargetPair target, int index) => new { target, index }))
		{
			LinkIK(item.index, nowState, item.target);
		}
	}

	private void GetCorrectShapeValues(ChaControl chara, MotionIKData.Shape[] shapes, out Vector3 _pos, out Vector3 _ang)
	{
		Vector3 zero = Vector3.zero;
		Vector3 zero2 = Vector3.zero;
		foreach (MotionIKData.Shape shape in shapes)
		{
			float shapeBodyValue = chara.GetShapeBodyValue(shape.shapeNo);
			zero += GetShapeLerpPositionValue(shapeBodyValue, shape.small[0], shape.large[0]);
			zero2 += GetShapeLerpAngleValue(shapeBodyValue, shape.small[1], shape.large[1]);
		}
		_pos = zero;
		_ang = zero2;
	}

	private void LinkIK(int index, MotionIKData.State state, IKTargetPair pair)
	{
		Func<int, string, Transform> func = (int sex, string frameName) => (!frameName.IsNullOrEmpty()) ? partners[sex].info.GetComponentsInChildren<Transform>().FirstOrDefault((Transform p) => p.name == frameName) : null;
		MotionIKData.Parts parts = ((state == null) ? null : state[index]);
		Func<int, MotionIKData.Frame> func2 = (int no) => (state != null) ? state.frames.FirstOrDefault((MotionIKData.Frame p) => p.frameNo == no) : null;
		Transform transform = null;
		MotionIKData.Param2 param = parts?.param2;
		IKEffector effector = pair.effector;
		BaseData component = effector.target.GetComponent<BaseData>();
		if (component != null)
		{
			component.bone = ((param == null) ? null : func(param.sex, param.target));
			transform = component.bone;
			BaseProcess component2 = component.GetComponent<BaseProcess>();
			if (component2 != null)
			{
				component2.enabled = component.bone != null;
			}
			MotionIKData.Frame frame = func2(index * 2);
			if (frame == null)
			{
				component.pos = Vector3.zero;
				component.rot = Quaternion.identity;
			}
			else
			{
				GetCorrectShapeValues(partners[frame.editNo].info, frame.shapes, out var _pos, out var _ang);
				component.pos = _pos;
				component.rot = Quaternion.Euler(_ang);
			}
		}
		if (transform == null || param == null)
		{
			effector.positionWeight = 0f;
			effector.rotationWeight = 0f;
		}
		else
		{
			effector.positionWeight = param.weightPos;
			effector.rotationWeight = param.weightAng;
		}
		transform = null;
		MotionIKData.Param3 param2 = parts?.param3;
		IKConstraintBend bend = pair.bend;
		BaseData component3 = bend.bendGoal.GetComponent<BaseData>();
		if (component3 != null)
		{
			component3.bone = ((param2 == null) ? null : func(0, param2.chein));
			transform = component3.bone;
			BaseProcess component4 = component3.GetComponent<BaseProcess>();
			if (component4 != null)
			{
				component4.enabled = component3.bone != null;
			}
			MotionIKData.Frame frame2 = func2(index * 2 + 1);
			if (frame2 == null)
			{
				component3.pos = Vector3.zero;
				component3.rot = Quaternion.identity;
			}
			else
			{
				GetCorrectShapeValues(partners[frame2.editNo].info, frame2.shapes, out var _pos2, out var _ang2);
				component3.pos = _pos2;
				component3.rot = Quaternion.Euler(_ang2);
			}
		}
		if (transform == null || param2 == null)
		{
			bend.weight = 0f;
		}
		else
		{
			bend.weight = param2.weight;
		}
	}
}
