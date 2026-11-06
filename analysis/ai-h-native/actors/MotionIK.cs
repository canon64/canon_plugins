using System;
using System.Collections.Generic;
using AIChara;
using Correct;
using Correct.Process;
using IllusionUtility.GetUtility;
using Manager;
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
			RightFoot,
			TotalNum
		}

		private static int IKTargetNum = 4;

		private static IKTargetPair[] ret = new IKTargetPair[IKTargetNum];

		public IKEffector effector { get; private set; }

		public IKConstraintBend bend { get; private set; }

		public static int IKTargetLength => IKTargetNum * 2;

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

		public static IKTargetPair[] GetPairs(IKSolverFullBodyBiped solver)
		{
			int num = 0;
			for (int i = 0; i < ret.Length; i++)
			{
				num = i;
				ret[num] = new IKTargetPair((IKTarget)num, solver);
			}
			return ret;
		}
	}

	private BaseData tmpData;

	private BaseProcess tmpProcess;

	public bool MapIK = true;

	private Transform[] DefBone = new Transform[8];

	private List<GameObject> tmpBones;

	private float nowKeyFrame;

	public int layerNo;

	private MotionIKData tmpMapData;

	private Dictionary<int, Dictionary<int, List<MotionIKData.BlendWeightInfo>>>[] calcBlend = new Dictionary<int, Dictionary<int, List<MotionIKData.BlendWeightInfo>>>[4]
	{
		new Dictionary<int, Dictionary<int, List<MotionIKData.BlendWeightInfo>>>(),
		new Dictionary<int, Dictionary<int, List<MotionIKData.BlendWeightInfo>>>(),
		new Dictionary<int, Dictionary<int, List<MotionIKData.BlendWeightInfo>>>(),
		new Dictionary<int, Dictionary<int, List<MotionIKData.BlendWeightInfo>>>()
	};

	private Dictionary<int, List<MotionIKData.BlendWeightInfo>>[] calcBlendBend = new Dictionary<int, List<MotionIKData.BlendWeightInfo>>[4]
	{
		new Dictionary<int, List<MotionIKData.BlendWeightInfo>>(),
		new Dictionary<int, List<MotionIKData.BlendWeightInfo>>(),
		new Dictionary<int, List<MotionIKData.BlendWeightInfo>>(),
		new Dictionary<int, List<MotionIKData.BlendWeightInfo>>()
	};

	public ChaControl info { get; private set; }

	public List<GameObject> items { get; private set; }

	public FullBodyBipedIK ik { get; private set; }

	public MotionIK[] partners { get; private set; }

	public IKTargetPair[] ikTargetPairs => (!(ik == null)) ? IKTargetPair.GetPairs(ik.solver) : null;

	public MotionIKData data { get; private set; }

	public FrameCorrect frameCorrect { get; private set; }

	public IKCorrect ikCorrect { get; private set; }

	public bool enabled
	{
		get
		{
			return ik != null && ik.enabled;
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
			string[] array = new string[data.states.Length];
			int num = 0;
			for (int i = 0; i < array.Length; i++)
			{
				array[num] = data.states[num].name;
			}
			return (data.states != null) ? array : new string[0];
		}
	}

	public MotionIK(ChaControl info, bool isHscene = false, MotionIKData data = null)
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
		if (!isHscene)
		{
			tmpBones = new List<GameObject>();
			animBody.transform.FindLoopPrefix(tmpBones, "f_pv_");
			DefBone[0] = ListFindBone(tmpBones, "f_pv_arm_L");
			DefBone[1] = ListFindBone(tmpBones, "f_pv_elbo_L");
			DefBone[2] = ListFindBone(tmpBones, "f_pv_arm_R");
			DefBone[3] = ListFindBone(tmpBones, "f_pv_elbo_R");
			DefBone[4] = ListFindBone(tmpBones, "f_pv_leg_L");
			DefBone[5] = ListFindBone(tmpBones, "f_pv_knee_L");
			DefBone[6] = ListFindBone(tmpBones, "f_pv_leg_R");
			DefBone[7] = ListFindBone(tmpBones, "f_pv_knee_R");
		}
	}

	public static List<MotionIK> Setup(List<ChaControl> infos)
	{
		List<MotionIK> list = new List<MotionIK>();
		for (int i = 0; i < infos.Count; i++)
		{
			int index = i;
			list.Add(new MotionIK(infos[index]));
		}
		for (int j = 0; j < list.Count; j++)
		{
			list[j].SetPartners(list);
		}
		return list;
	}

	public static Vector3 GetShapeLerpPositionValue(float shape, Vector3 min, Vector3 med, Vector3 max, bool MapIK = false)
	{
		if (MapIK)
		{
			return (!(shape >= 0.5f)) ? Vector3.Lerp(min, Vector3.zero, Mathf.InverseLerp(0f, 0.5f, shape)) : Vector3.Lerp(Vector3.zero, max, Mathf.InverseLerp(0.5f, 1f, shape));
		}
		return (!(shape >= 0.5f)) ? Vector3.Lerp(min, med, Mathf.InverseLerp(0f, 0.5f, shape)) : Vector3.Lerp(med, max, Mathf.InverseLerp(0.5f, 1f, shape));
	}

	public static Vector3 GetShapeLerpAngleValue(float shape, Vector3 min, Vector3 med, Vector3 max, bool MapIK = false)
	{
		Vector3 zero = Vector3.zero;
		if (shape >= 0.5f)
		{
			float t = Mathf.InverseLerp(0.5f, 1f, shape);
			for (int i = 0; i < 3; i++)
			{
				if (MapIK)
				{
					zero[i] = Mathf.LerpAngle(0f, max[i], t);
				}
				else
				{
					zero[i] = Mathf.LerpAngle(med[i], max[i], t);
				}
			}
		}
		else
		{
			float t2 = Mathf.InverseLerp(0f, 0.5f, shape);
			for (int j = 0; j < 3; j++)
			{
				if (MapIK)
				{
					zero[j] = Mathf.LerpAngle(min[j], 0f, t2);
				}
				else
				{
					zero[j] = Mathf.LerpAngle(min[j], med[j], t2);
				}
			}
		}
		return zero;
	}

	public void SetPartners(params MotionIK[] partners)
	{
		this.partners = new MotionIK[(partners.Length == 0) ? 1 : partners.Length];
		this.partners[0] = this;
		int[] array = new int[2] { 1, 0 };
		for (int i = 0; i < partners.Length; i++)
		{
			array[1] = i;
			if (this.partners[0] != partners[array[1]])
			{
				this.partners[array[0]] = partners[array[1]];
				array[0]++;
			}
		}
	}

	public void SetPartners(List<MotionIK> partners)
	{
		MotionIK[] array = new MotionIK[partners.Count];
		int num = 0;
		for (int i = 0; i < array.Length; i++)
		{
			num = i;
			array[num] = partners[num];
		}
		SetPartners(array);
	}

	public void SetPartners(List<Tuple<int, int, MotionIK>> partners)
	{
		MotionIK[] array = new MotionIK[partners.Count];
		int num = 0;
		for (int i = 0; i < array.Length; i++)
		{
			num = i;
			array[num] = partners[num].Item3;
		}
		SetPartners(array);
	}

	public void SetItems(MotionIK motionIK, GameObject[] items)
	{
		for (int i = 0; i < partners.Length; i++)
		{
			for (int j = 0; j < items.Length; j++)
			{
				if (partners[i].items == null)
				{
					partners[i].items = new List<GameObject>();
				}
				if (!partners[i].items.Contains(items[j]))
				{
					partners[i].items.Add(items[j]);
				}
			}
		}
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

	public void LoadData(string abName, string assetName, bool add = false)
	{
		if (data == null)
		{
			data = new MotionIKData();
		}
		data.AIRead(abName, assetName, add);
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

	public MotionIKData.State InitState(string stateName, int sex)
	{
		return data.InitState(stateName, sex);
	}

	public MotionIKData.State GetNowState(string stateName)
	{
		if (data.states == null)
		{
			return null;
		}
		int num = -1;
		for (int i = 0; i < data.states.Length; i++)
		{
			if (!(data.states[i].name != stateName))
			{
				num = i;
				break;
			}
		}
		if (num == -1)
		{
			return null;
		}
		return data.states[num];
	}

	public MotionIKData.State GetNowState(int hashName)
	{
		if (data == null || data.states == null)
		{
			return null;
		}
		int num = -1;
		for (int i = 0; i < data.states.Length; i++)
		{
			if (Animator.StringToHash(data.states[i].name) == hashName)
			{
				num = i;
				break;
			}
		}
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

	public void SetMapIK(string AnimatorName)
	{
		if (!Singleton<Manager.Resources>.Instance.MapIKData.TryGetValue(AnimatorName, out tmpMapData))
		{
			tmpMapData = null;
		}
		data = ((tmpMapData != null) ? tmpMapData.Copy() : null);
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
			for (int i = 0; i < frames.Length; i++)
			{
				MotionIKData.Frame frame = frames[i];
				int num = frame.frameNo - iKTargetLength;
				if (num >= 0)
				{
					BaseCorrect.Info info = frameCorrect.list[num];
					info.enabled = true;
					Vector3[] correctShapeValues = GetCorrectShapeValues(partners[frame.editNo].info, frame.shapes);
					info.pos = correctShapeValues[0];
					info.ang = correctShapeValues[1];
				}
			}
		}
		enabled = nowState != null;
		int num2 = 0;
		for (int j = 0; j < ikTargetPairs.Length; j++)
		{
			num2 = j;
			LinkIK(num2, nowState, ikTargetPairs[num2]);
		}
	}

	public void Calc(int hashName)
	{
		if (frameCorrect == null)
		{
			return;
		}
		InitFrameCalc();
		MotionIKData.State nowState = GetNowState(hashName);
		if (nowState != null)
		{
			int iKTargetLength = IKTargetPair.IKTargetLength;
			MotionIKData.Frame[] frames = nowState.frames;
			for (int i = 0; i < frames.Length; i++)
			{
				MotionIKData.Frame frame = frames[i];
				int num = frame.frameNo - iKTargetLength;
				if (num >= 0)
				{
					BaseCorrect.Info info = frameCorrect.list[num];
					info.enabled = true;
					Vector3[] correctShapeValues = GetCorrectShapeValues(partners[frame.editNo].info, frame.shapes);
					info.pos = correctShapeValues[0];
					info.ang = correctShapeValues[1];
				}
			}
		}
		if (!MapIK)
		{
			enabled = nowState != null;
		}
		else
		{
			enabled = true;
		}
		int num2 = 0;
		if (!MapIK)
		{
			for (int j = 0; j < ikTargetPairs.Length; j++)
			{
				num2 = j;
				LinkIK(num2, nowState, ikTargetPairs[num2]);
			}
			return;
		}
		if (nowState != null)
		{
			for (int k = 0; k < ikTargetPairs.Length; k++)
			{
				num2 = k;
				LinkIK(num2, nowState, ikTargetPairs[num2]);
			}
			return;
		}
		for (int l = 0; l < ikTargetPairs.Length; l++)
		{
			num2 = l;
			tmpData = ikTargetPairs[num2].effector.target.GetComponent<BaseData>();
			if (tmpData.bone == null)
			{
				tmpData.bone = DefBone[num2 * 2];
				tmpData.GetComponent<BaseProcess>().enabled = true;
				ikTargetPairs[num2].effector.positionWeight = 1f;
				ikTargetPairs[num2].effector.rotationWeight = 1f;
			}
			tmpData = ikTargetPairs[num2].bend.bendGoal.GetComponent<BaseData>();
			if (tmpData.bone == null)
			{
				tmpData.GetComponent<BaseProcess>().enabled = true;
				tmpData.bone = DefBone[num2 * 2 + 1];
				ikTargetPairs[num2].bend.weight = 1f;
			}
		}
	}

	private Vector3[] GetCorrectShapeValues(ChaControl chara, MotionIKData.Shape[] shapes)
	{
		Vector3[] array = new Vector3[2]
		{
			Vector3.zero,
			Vector3.zero
		};
		for (int i = 0; i < shapes.Length; i++)
		{
			MotionIKData.Shape shape = shapes[i];
			float shapeBodyValue = chara.GetShapeBodyValue(shape.shapeNo);
			for (int j = 0; j < array.Length; j++)
			{
				if (j == 0)
				{
					array[j] += GetShapeLerpPositionValue(shapeBodyValue, shape.small[j], shape.mediam[j], shape.large[j], MapIK);
				}
				else
				{
					array[j] += GetShapeLerpAngleValue(shapeBodyValue, shape.small[j], shape.mediam[j], shape.large[j], MapIK);
				}
			}
		}
		return array;
	}

	private Vector3[] GetCorrectShapeValues(ChaControl chara, float nowKeyFrame, Dictionary<int, Dictionary<int, List<MotionIKData.BlendWeightInfo>>> calcBlend)
	{
		Vector3[] array = new Vector3[2]
		{
			Vector3.zero,
			Vector3.zero
		};
		float num = 0f;
		for (int i = 0; i < 2; i++)
		{
			foreach (KeyValuePair<int, List<MotionIKData.BlendWeightInfo>> item in calcBlend[i])
			{
				int num2 = -1;
				for (int j = 0; j < item.Value.Count; j++)
				{
					int index = j;
					if (!(item.Value[index].StartKey > nowKeyFrame))
					{
						num2 = j;
					}
				}
				if (num2 < 0)
				{
					num2 = 0;
				}
				MotionIKData.BlendWeightInfo blendWeightInfo = item.Value[num2];
				float shapeBodyValue = chara.GetShapeBodyValue(blendWeightInfo.shape.shapeNo);
				num = Mathf.InverseLerp(blendWeightInfo.StartKey, blendWeightInfo.EndKey, nowKeyFrame);
				if (i == 0)
				{
					Vector3 vector = GetShapeLerpPositionValue(shapeBodyValue, blendWeightInfo.shape.small[i], blendWeightInfo.shape.mediam[i], blendWeightInfo.shape.large[i], MapIK);
					if (blendWeightInfo.pattern == 1)
					{
						vector = Vector3.Lerp(Vector3.zero, vector, num);
					}
					else if (blendWeightInfo.pattern == 2)
					{
						vector = Vector3.Lerp(vector, Vector3.zero, num);
					}
					array[i] += vector;
				}
				else
				{
					Vector3 vector = GetShapeLerpAngleValue(shapeBodyValue, blendWeightInfo.shape.small[i], blendWeightInfo.shape.mediam[i], blendWeightInfo.shape.large[i], MapIK);
					if (blendWeightInfo.pattern == 1)
					{
						vector = Vector3.Lerp(Vector3.zero, vector, num);
					}
					else if (blendWeightInfo.pattern == 2)
					{
						vector = Vector3.Lerp(vector, Vector3.zero, num);
					}
					array[i] += vector;
				}
			}
		}
		return array;
	}

	private Vector3[] GetCorrectShapeValues(ChaControl chara, float nowKeyFrame, Dictionary<int, List<MotionIKData.BlendWeightInfo>> calcBlend)
	{
		Vector3[] array = new Vector3[2]
		{
			Vector3.zero,
			Vector3.zero
		};
		float num = 0f;
		foreach (KeyValuePair<int, List<MotionIKData.BlendWeightInfo>> item in calcBlend)
		{
			int num2 = -1;
			for (int i = 0; i < item.Value.Count; i++)
			{
				int index = i;
				if (!(item.Value[index].StartKey > nowKeyFrame))
				{
					num2 = i;
				}
			}
			if (num2 < 0)
			{
				num2 = 0;
			}
			MotionIKData.BlendWeightInfo blendWeightInfo = item.Value[num2];
			float shapeBodyValue = chara.GetShapeBodyValue(blendWeightInfo.shape.shapeNo);
			num = Mathf.InverseLerp(blendWeightInfo.StartKey, blendWeightInfo.EndKey, nowKeyFrame);
			Vector3 vector = GetShapeLerpPositionValue(shapeBodyValue, blendWeightInfo.shape.small[0], blendWeightInfo.shape.mediam[0], blendWeightInfo.shape.large[0], MapIK);
			if (blendWeightInfo.pattern == 1)
			{
				vector = Vector3.Lerp(Vector3.zero, vector, num);
			}
			else if (blendWeightInfo.pattern == 2)
			{
				vector = Vector3.Lerp(vector, Vector3.zero, num);
			}
			array[0] += vector;
			vector = GetShapeLerpAngleValue(shapeBodyValue, blendWeightInfo.shape.small[1], blendWeightInfo.shape.mediam[1], blendWeightInfo.shape.large[1], MapIK);
			if (blendWeightInfo.pattern == 1)
			{
				vector = Vector3.Lerp(Vector3.zero, vector, num);
			}
			else if (blendWeightInfo.pattern == 2)
			{
				vector = Vector3.Lerp(vector, Vector3.zero, num);
			}
			array[1] += vector;
		}
		return array;
	}

	private void LinkIK(int index, MotionIKData.State state, IKTargetPair pair)
	{
		nowKeyFrame = 0f;
		MotionIKData.Parts parts = state?[index];
		Transform transform = null;
		MotionIKData.Param2 param = parts?.param2;
		IKEffector effector = pair.effector;
		tmpData = effector.target.GetComponent<BaseData>();
		if (tmpData != null)
		{
			tmpData.bone = ((param != null) ? getTarget(param.sex, param.target) : null);
			transform = tmpData.bone;
			tmpProcess = tmpData.GetComponent<BaseProcess>();
			if (tmpProcess != null)
			{
				tmpProcess.enabled = tmpData.bone != null;
			}
			MotionIKData.Frame frame = FindFrame(state, index * 2);
			if (frame.frameNo == -1)
			{
				tmpData.pos = Vector3.zero;
				tmpData.rot = Quaternion.identity;
				return;
			}
			Vector3[] array = new Vector3[2];
			if (!MapIK)
			{
				array = GetCorrectShapeValues(partners[frame.editNo].info, frame.shapes);
			}
			else
			{
				ref Vector3 reference = ref array[0];
				reference = tmpData.pos;
				ref Vector3 reference2 = ref array[1];
				reference2 = tmpData.rot.eulerAngles;
				CalcBlendSet(ref calcBlend[index], parts.param2.blendInfos);
				array = GetCorrectShapeValues(partners[0].info, nowKeyFrame, calcBlend[index]);
			}
			tmpData.pos = array[0];
			tmpData.rot = Quaternion.Euler(array[1]);
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
		tmpData = bend.bendGoal.GetComponent<BaseData>();
		if (tmpData != null)
		{
			tmpData.bone = ((param2 != null) ? getTarget(0, param2.chein) : null);
			transform = tmpData.bone;
			tmpProcess = tmpData.GetComponent<BaseProcess>();
			if (tmpProcess != null)
			{
				tmpProcess.enabled = tmpData.bone != null;
			}
			MotionIKData.Frame frame2 = FindFrame(state, index * 2 + 1);
			if (frame2.frameNo == -1)
			{
				tmpData.pos = Vector3.zero;
				tmpData.rot = Quaternion.identity;
				return;
			}
			Vector3[] array2 = new Vector3[2];
			if (!MapIK)
			{
				array2 = GetCorrectShapeValues(partners[frame2.editNo].info, frame2.shapes);
			}
			else
			{
				ref Vector3 reference3 = ref array2[0];
				reference3 = tmpData.pos;
				ref Vector3 reference4 = ref array2[1];
				reference4 = tmpData.rot.eulerAngles;
				CalcBlendBendSet(ref calcBlendBend[index], parts.param3.blendInfos);
				array2 = GetCorrectShapeValues(partners[0].info, nowKeyFrame, calcBlendBend[index]);
			}
			tmpData.pos = array2[0];
			tmpData.rot = Quaternion.Euler(array2[1]);
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

	private Transform getTarget(int sex, string frameName)
	{
		if (frameName.IsNullOrEmpty())
		{
			return null;
		}
		Transform result = null;
		Transform[] array = null;
		if (sex < partners.Length)
		{
			array = partners[sex].info.GetComponentsInChildren<Transform>();
		}
		else
		{
			int index = sex - partners.Length;
			array = partners[0].items[index].GetComponentsInChildren<Transform>();
		}
		if (array == null)
		{
			return null;
		}
		for (int i = 0; i < array.Length; i++)
		{
			int num = i;
			if (!(array[num].name != frameName))
			{
				result = array[num];
				break;
			}
		}
		return result;
	}

	private MotionIKData.Frame FindFrame(MotionIKData.State state, int no)
	{
		MotionIKData.Frame result = default(MotionIKData.Frame);
		result.frameNo = -1;
		if (state == null)
		{
			return result;
		}
		for (int i = 0; i < state.frames.Length; i++)
		{
			int num = i;
			if (state.frames[num].frameNo == no)
			{
				result = state.frames[num];
			}
		}
		return result;
	}

	private Transform ListFindBone(List<GameObject> list, string name)
	{
		Transform result = null;
		foreach (GameObject item in list)
		{
			if (item.name != name)
			{
				continue;
			}
			result = item.transform;
			break;
		}
		return result;
	}

	public void ChangeWeight(int nameHash, AnimatorStateInfo info)
	{
		if (data == null)
		{
			return;
		}
		int num = -1;
		for (int i = 0; i < data.states.Length; i++)
		{
			if (Animator.StringToHash(data.states[i].name) == nameHash)
			{
				num = i;
				break;
			}
		}
		if (num >= 0)
		{
			if (info.loop)
			{
				nowKeyFrame = info.normalizedTime % 1f;
			}
			else
			{
				nowKeyFrame = Mathf.Clamp01(info.normalizedTime);
			}
			for (int j = 0; j < 4; j++)
			{
				tmpData = ikTargetPairs[j].effector.target.GetComponent<BaseData>();
				Vector3[] array = new Vector3[2]
				{
					tmpData.pos,
					tmpData.rot.eulerAngles
				};
				array = GetCorrectShapeValues(partners[0].info, nowKeyFrame, calcBlend[j]);
				tmpData.pos = array[0];
				tmpData.rot = Quaternion.Euler(array[1]);
				tmpData = ikTargetPairs[j].bend.bendGoal.GetComponent<BaseData>();
				array = new Vector3[2]
				{
					tmpData.pos,
					tmpData.rot.eulerAngles
				};
				array = GetCorrectShapeValues(partners[0].info, nowKeyFrame, calcBlendBend[j]);
				tmpData.pos = array[0];
				tmpData.rot = Quaternion.Euler(array[1]);
			}
		}
	}

	public void CalcBlendSet(ref Dictionary<int, Dictionary<int, List<MotionIKData.BlendWeightInfo>>> calcBlend, params List<MotionIKData.BlendWeightInfo>[] binfo)
	{
		calcBlend[0] = new Dictionary<int, List<MotionIKData.BlendWeightInfo>>();
		calcBlend[1] = new Dictionary<int, List<MotionIKData.BlendWeightInfo>>();
		for (int i = 0; i < binfo.Length; i++)
		{
			foreach (MotionIKData.BlendWeightInfo item in binfo[i])
			{
				if (!calcBlend[i].ContainsKey(item.shape.shapeNo))
				{
					calcBlend[i].Add(item.shape.shapeNo, new List<MotionIKData.BlendWeightInfo>());
				}
				calcBlend[i][item.shape.shapeNo].Add(item);
				calcBlend[i][item.shape.shapeNo].Sort(Compare);
			}
		}
	}

	public void CalcBlendBendSet(ref Dictionary<int, List<MotionIKData.BlendWeightInfo>> calcBlendBend, params List<MotionIKData.BlendWeightInfo>[] binfo)
	{
		calcBlendBend = new Dictionary<int, List<MotionIKData.BlendWeightInfo>>();
		for (int i = 0; i < binfo.Length; i++)
		{
			foreach (MotionIKData.BlendWeightInfo item in binfo[i])
			{
				if (!calcBlendBend.ContainsKey(item.shape.shapeNo))
				{
					calcBlendBend.Add(item.shape.shapeNo, new List<MotionIKData.BlendWeightInfo>());
				}
				calcBlendBend[item.shape.shapeNo].Add(item);
				calcBlendBend[item.shape.shapeNo].Sort(Compare);
			}
		}
	}

	private static int Compare(MotionIKData.BlendWeightInfo a, MotionIKData.BlendWeightInfo b)
	{
		if (a.StartKey - b.StartKey > 0f)
		{
			return 1;
		}
		if (a.StartKey - b.StartKey < 0f)
		{
			return -1;
		}
		return 0;
	}
}
