using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AIChara;
using Correct;
using Manager;
using UnityEngine;

public class MotionIKData
{
	public class State
	{
		public string name = string.Empty;

		public Parts leftHand = new Parts();

		public Parts rightHand = new Parts();

		public Parts leftFoot = new Parts();

		public Parts rightFoot = new Parts();

		public Frame[] frames;

		public Parts this[int index] => parts[index];

		public Parts[] parts => new Parts[4] { leftHand, rightHand, leftFoot, rightFoot };

		public State()
		{
		}

		public State(State src)
		{
			name = src.name;
			leftHand = src.leftHand;
			rightHand = src.rightHand;
			leftFoot = src.leftFoot;
			rightFoot = src.rightFoot;
			frames = src.frames;
		}
	}

	public class Parts
	{
		public Param2 param2 = new Param2();

		public Param3 param3 = new Param3();
	}

	public class Param2
	{
		public int sex;

		public string target = string.Empty;

		public float weightPos;

		public float weightAng;

		public List<BlendWeightInfo>[] blendInfos = new List<BlendWeightInfo>[2]
		{
			new List<BlendWeightInfo>(),
			new List<BlendWeightInfo>()
		};

		public object this[int index]
		{
			get
			{
				return index switch
				{
					0 => sex, 
					1 => target, 
					2 => weightPos, 
					3 => weightAng, 
					_ => null, 
				};
			}
			set
			{
				switch (index)
				{
				case 0:
					sex = ((!(value is string)) ? ((int)value) : int.Parse((string)value));
					break;
				case 1:
					target = (string)value;
					break;
				case 2:
					weightPos = ((!(value is string)) ? ((float)value) : float.Parse((string)value));
					break;
				case 3:
					weightAng = ((!(value is string)) ? ((float)value) : float.Parse((string)value));
					break;
				}
			}
		}
	}

	public class Param3
	{
		public string chein = string.Empty;

		public float weight;

		public List<BlendWeightInfo> blendInfos = new List<BlendWeightInfo>();

		public object this[int index]
		{
			get
			{
				return index switch
				{
					0 => chein, 
					1 => weight, 
					_ => null, 
				};
			}
			set
			{
				switch (index)
				{
				case 0:
					chein = (string)value;
					break;
				case 1:
					weight = ((!(value is string)) ? ((float)value) : float.Parse((string)value));
					break;
				}
			}
		}
	}

	public struct Frame
	{
		public int frameNo;

		public int editNo;

		public Shape[] shapes;
	}

	public struct Shape
	{
		public int shapeNo;

		public PosAng small;

		public PosAng mediam;

		public PosAng large;

		public PosAng? this[int index]
		{
			get
			{
				return index switch
				{
					0 => small, 
					1 => mediam, 
					2 => large, 
					_ => null, 
				};
			}
			set
			{
				if (value.HasValue)
				{
					switch (index)
					{
					case 0:
						small = value.Value;
						break;
					case 1:
						mediam = value.Value;
						break;
					case 2:
						large = value.Value;
						break;
					}
				}
			}
		}
	}

	public struct PosAng
	{
		public Vector3 pos;

		public Vector3 ang;

		public Vector3 this[int index]
		{
			get
			{
				return index switch
				{
					0 => pos, 
					1 => ang, 
					_ => Vector3.zero, 
				};
			}
			set
			{
				switch (index)
				{
				case 0:
					pos = value;
					break;
				case 1:
					ang = value;
					break;
				}
			}
		}

		public float[] posArray => new float[3] { pos.x, pos.y, pos.z };

		public float[] angArray => new float[3] { ang.x, ang.y, ang.z };
	}

	public struct BlendWeightInfo
	{
		public int pattern;

		public float StartKey;

		public float EndKey;

		public Shape shape;
	}

	private ExcelData data;

	private List<string> row = new List<string>();

	private State[] _states;

	[CompilerGenerated]
	private static Dictionary<string, int> <>f__switch$mapB;

	[CompilerGenerated]
	private static Dictionary<string, int> <>f__switch$mapC;

	public State[] states
	{
		get
		{
			return _states;
		}
		set
		{
			_states = value;
		}
	}

	public MotionIKData Copy()
	{
		MotionIKData motionIKData = new MotionIKData();
		int num = states.Length;
		motionIKData.states = new State[num];
		for (int i = 0; i < num; i++)
		{
			State state = new State();
			State state2 = states[i];
			state.name = state2.name;
			int num2 = state.parts.Length;
			for (int j = 0; j < num2; j++)
			{
				Parts parts = state.parts[j];
				Parts parts2 = state2.parts[j];
				parts.param2.sex = parts2.param2.sex;
				parts.param2.target = parts2.param2.target;
				parts.param2.weightPos = parts2.param2.weightPos;
				parts.param2.weightAng = parts2.param2.weightAng;
				parts.param2.blendInfos[0] = new List<BlendWeightInfo>(parts2.param2.blendInfos[0]);
				parts.param2.blendInfos[1] = new List<BlendWeightInfo>(parts2.param2.blendInfos[1]);
				parts.param3.chein = parts2.param3.chein;
				parts.param3.weight = parts2.param3.weight;
				parts.param3.blendInfos = new List<BlendWeightInfo>(parts2.param3.blendInfos);
			}
			int num3 = state2.frames.Length;
			state.frames = new Frame[num3];
			for (int k = 0; k < num3; k++)
			{
				Frame frame = default(Frame);
				Frame frame2 = state2.frames[k];
				frame.frameNo = frame2.frameNo;
				frame.editNo = frame2.editNo;
				int num4 = frame2.shapes.Length;
				frame.shapes = new Shape[num4];
				for (int l = 0; l < num4; l++)
				{
					Shape shape = default(Shape);
					Shape shape2 = frame2.shapes[l];
					shape.shapeNo = shape2.shapeNo;
					for (int m = 0; m < 3; m++)
					{
						shape[m] = default(PosAng);
						PosAng value = shape[m].Value;
						for (int n = 0; n < 3; n++)
						{
							value.pos[n] = shape2[m].Value.pos[n];
						}
						for (int num5 = 0; num5 < 3; num5++)
						{
							value.ang[num5] = shape2[m].Value.ang[num5];
						}
						shape[m] = value;
					}
					frame.shapes[l] = shape;
				}
				state.frames[k] = frame;
			}
			motionIKData.states[i] = state;
		}
		return motionIKData;
	}

	public State InitState(string stateName, int sex)
	{
		int num = -1;
		if (states != null)
		{
			int num2 = -1;
			while (++num2 < states.Length && !(states[num2].name == stateName))
			{
			}
			num = ((num2 < states.Length) ? num2 : (-1));
		}
		if (num == -1)
		{
			State[] second = new State[1]
			{
				new State
				{
					name = stateName
				}
			};
			if (states == null)
			{
				states = second;
			}
			else
			{
				states = states.Concat(second).ToArray();
			}
			num = states.Length - 1;
		}
		State state = states[num];
		InitFrame(state, sex);
		return state;
	}

	public void Release()
	{
		states = null;
	}

	public static void InitFrame(State state, int sex)
	{
		int ikLen = MotionIK.IKTargetPair.IKTargetLength;
		IEnumerable<Frame> first = Enumerable.Range(0, ikLen).Select(delegate(int i)
		{
			Frame result2 = default(Frame);
			result2.frameNo = i;
			return result2;
		});
		IEnumerable<Frame> second = Enumerable.Range(0, FrameCorrect.FrameNames.Length).Select(delegate(int i)
		{
			Frame result = default(Frame);
			result.frameNo = i + ikLen;
			return result;
		});
		IEnumerable<Frame> source = first.Concat(second);
		state.frames = source.ToArray();
		for (int j = 0; j < state.frames.Length; j++)
		{
			InitShape(ref state.frames[j]);
		}
	}

	public static void InitShape(ref Frame frame)
	{
		frame.shapes = Enumerable.Range(0, ChaFileDefine.cf_bodyshapename.Length).Select(delegate(int i)
		{
			Shape result = default(Shape);
			result.shapeNo = i;
			return result;
		}).ToArray();
		for (int j = 0; j < frame.shapes.Length; j++)
		{
			Shape shape = frame.shapes[j];
			shape.small = default(PosAng);
			shape.mediam = default(PosAng);
			shape.large = default(PosAng);
		}
	}

	public void AIRead(string abName, string assetName, bool add = false)
	{
		bool flag = false;
		if (!GlobalMethod.AssetFileExist(abName, assetName, string.Empty))
		{
			return;
		}
		data = null;
		data = CommonLib.LoadAsset<ExcelData>(abName, assetName, clone: false, string.Empty);
		Singleton<HSceneManager>.Instance.hashUseAssetBundle.Add(abName);
		if (data == null)
		{
			return;
		}
		int index = 1;
		int num = data.list[index].list.Count - 4;
		int num2 = 0;
		int maxCell = data.MaxCell;
		State[] array;
		if (!add)
		{
			array = new State[num];
		}
		else
		{
			num2 = states.Length;
			array = new State[num + num2];
			for (int i = 0; i < num2; i++)
			{
				array[i] = new State(states[i]);
			}
		}
		int num3 = 0;
		string empty = string.Empty;
		string empty2 = string.Empty;
		string empty3 = string.Empty;
		bool flag2 = false;
		for (int j = 0; j < num; j++)
		{
			int num4 = j;
			int num5 = num4;
			if (add)
			{
				num5 += num2;
			}
			array[num5] = new State();
			array[num5].frames = new Frame[FrameCorrect.FrameNames.Length + 8];
			int num6 = -1;
			int num7 = -1;
			int num8 = -1;
			bool flag3 = true;
			index = 1;
			while (index < maxCell)
			{
				row = data.list[index++].list;
				int count = row.Count;
				num3 = 4 + num4;
				empty3 = ((num3 >= count) ? null : row[num3]);
				flag2 = empty3.IsNullOrEmpty();
				int value;
				if (index < 28)
				{
					empty2 = ((num3 >= count) ? null : row[3]);
					if (empty2 == null)
					{
						continue;
					}
					if (<>f__switch$mapB == null)
					{
						Dictionary<string, int> dictionary = new Dictionary<string, int>(7);
						dictionary.Add("anime name", 0);
						dictionary.Add("sex", 1);
						dictionary.Add("target", 2);
						dictionary.Add("chain", 3);
						dictionary.Add("weight pos", 4);
						dictionary.Add("weight rot", 5);
						dictionary.Add("weight", 6);
						<>f__switch$mapB = dictionary;
					}
					if (!<>f__switch$mapB.TryGetValue(empty2, out value))
					{
						continue;
					}
					switch (value)
					{
					case 0:
						array[num5].name = empty3;
						break;
					case 1:
						num6++;
						empty = ((num3 >= count) ? null : row[0]);
						if (empty != string.Empty)
						{
							int.TryParse(empty3, out array[num5].parts[num6].param2.sex);
						}
						break;
					case 2:
						array[num5].parts[num6].param2.target = empty3;
						break;
					case 3:
						array[num5].parts[num6].param3.chein = empty3;
						break;
					case 4:
						float.TryParse(empty3, out array[num5].parts[num6].param2.weightPos);
						break;
					case 5:
						float.TryParse(empty3, out array[num5].parts[num6].param2.weightAng);
						break;
					case 6:
						float.TryParse(empty3, out array[num5].parts[num6].param3.weight);
						break;
					}
					continue;
				}
				if (index < 86)
				{
					num7++;
					if (!flag2)
					{
						int.TryParse(empty3, out array[num5].frames[num7].editNo);
					}
					array[num5].frames[num7].frameNo = num7;
					array[num5].frames[num7].shapes = new Shape[ChaFileDefine.cf_bodyshapename.Length];
					num8 = -1;
					continue;
				}
				empty2 = ((count <= 3) ? null : row[3]);
				if (empty2 != null)
				{
					if (<>f__switch$mapC == null)
					{
						Dictionary<string, int> dictionary = new Dictionary<string, int>(9);
						dictionary.Add("Sx", 0);
						dictionary.Add("Sy", 1);
						dictionary.Add("Sz", 2);
						dictionary.Add("Mx", 3);
						dictionary.Add("My", 4);
						dictionary.Add("Mz", 5);
						dictionary.Add("Lx", 6);
						dictionary.Add("Ly", 7);
						dictionary.Add("Lz", 8);
						<>f__switch$mapC = dictionary;
					}
					if (<>f__switch$mapC.TryGetValue(empty2, out value))
					{
						switch (value)
						{
						case 0:
							if (flag3)
							{
								empty = ((count <= 0) ? null : row[0]);
								if (empty != string.Empty)
								{
									num7++;
									num8 = -1;
								}
								num8++;
								array[num5].frames[num7].shapes[num8].shapeNo = num8;
							}
							if (!flag2)
							{
								if (flag3)
								{
									float.TryParse(empty3, out array[num5].frames[num7].shapes[num8].small.pos.x);
								}
								else
								{
									float.TryParse(empty3, out array[num5].frames[num7].shapes[num8].small.ang.x);
								}
							}
							continue;
						case 1:
							if (!flag2)
							{
								if (flag3)
								{
									float.TryParse(empty3, out array[num5].frames[num7].shapes[num8].small.pos.y);
								}
								else
								{
									float.TryParse(empty3, out array[num5].frames[num7].shapes[num8].small.ang.y);
								}
							}
							continue;
						case 2:
							if (!flag2)
							{
								if (flag3)
								{
									float.TryParse(empty3, out array[num5].frames[num7].shapes[num8].small.pos.z);
								}
								else
								{
									float.TryParse(empty3, out array[num5].frames[num7].shapes[num8].small.ang.z);
								}
							}
							continue;
						case 3:
							if (!flag2)
							{
								if (flag3)
								{
									float.TryParse(empty3, out array[num5].frames[num7].shapes[num8].mediam.pos.x);
								}
								else
								{
									float.TryParse(empty3, out array[num5].frames[num7].shapes[num8].mediam.ang.x);
								}
							}
							continue;
						case 4:
							if (!flag2)
							{
								if (flag3)
								{
									float.TryParse(empty3, out array[num5].frames[num7].shapes[num8].mediam.pos.y);
								}
								else
								{
									float.TryParse(empty3, out array[num5].frames[num7].shapes[num8].mediam.ang.y);
								}
							}
							continue;
						case 5:
							if (!flag2)
							{
								if (flag3)
								{
									float.TryParse(empty3, out array[num5].frames[num7].shapes[num8].mediam.pos.z);
								}
								else
								{
									float.TryParse(empty3, out array[num5].frames[num7].shapes[num8].mediam.ang.z);
								}
							}
							continue;
						case 6:
							if (!flag2)
							{
								if (flag3)
								{
									float.TryParse(empty3, out array[num5].frames[num7].shapes[num8].large.pos.x);
								}
								else
								{
									float.TryParse(empty3, out array[num5].frames[num7].shapes[num8].large.ang.x);
								}
							}
							continue;
						case 7:
							if (!flag2)
							{
								if (flag3)
								{
									float.TryParse(empty3, out array[num5].frames[num7].shapes[num8].large.pos.y);
								}
								else
								{
									float.TryParse(empty3, out array[num5].frames[num7].shapes[num8].large.ang.y);
								}
							}
							continue;
						case 8:
							if (!flag2)
							{
								if (flag3)
								{
									float.TryParse(empty3, out array[num5].frames[num7].shapes[num8].large.pos.z);
								}
								else
								{
									float.TryParse(empty3, out array[num5].frames[num7].shapes[num8].large.ang.z);
								}
							}
							flag3 ^= true;
							continue;
						}
					}
				}
				empty = ((count <= 0) ? null : row[0]);
				if (empty == "フレーム")
				{
					num7 = -1;
				}
			}
		}
		states = new State[array.Length];
		for (int k = 0; k < array.Length; k++)
		{
			states[k] = array[k];
		}
	}
}
