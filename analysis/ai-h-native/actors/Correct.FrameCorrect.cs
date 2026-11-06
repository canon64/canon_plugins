using System.Collections.Generic;
using System.Linq;
using Correct.Process;
using UnityEngine;

namespace Correct;

public class FrameCorrect : BaseCorrect
{
	public static string[] FrameNames = new string[50]
	{
		"cf_J_Hips", "cf_J_Spine01", "cf_J_Spine02", "cf_J_Spine03", "cf_J_Neck", "cf_J_Head", "cf_J_Kosi01", "cf_J_Kosi02", "cf_J_Shoulder_L", "cf_J_Shoulder_R",
		"cf_J_Toes01_L", "cf_J_Toes01_R", "cf_J_Hand_Thumb01_L", "cf_J_Hand_Thumb02_L", "cf_J_Hand_Thumb03_L", "cf_J_Hand_Index01_L", "cf_J_Hand_Index02_L", "cf_J_Hand_Index03_L", "cf_J_Hand_Middle01_L", "cf_J_Hand_Middle02_L",
		"cf_J_Hand_Middle03_L", "cf_J_Hand_Ring01_L", "cf_J_Hand_Ring02_L", "cf_J_Hand_Ring03_L", "cf_J_Hand_Little01_L", "cf_J_Hand_Little02_L", "cf_J_Hand_Little03_L", "cf_J_Hand_Thumb01_R", "cf_J_Hand_Thumb02_R", "cf_J_Hand_Thumb03_R",
		"cf_J_Hand_Index01_R", "cf_J_Hand_Index02_R", "cf_J_Hand_Index03_R", "cf_J_Hand_Middle01_R", "cf_J_Hand_Middle02_R", "cf_J_Hand_Middle03_R", "cf_J_Hand_Ring01_R", "cf_J_Hand_Ring02_R", "cf_J_Hand_Ring03_R", "cf_J_Hand_Little01_R",
		"cf_J_Hand_Little02_R", "cf_J_Hand_Little03_R", "cf_J_Mune00_L", "cf_J_Mune01_L", "cf_J_Mune02_L", "cf_J_Mune03_L", "cf_J_Mune00_R", "cf_J_Mune01_R", "cf_J_Mune02_R", "cf_J_Mune03_R"
	};

	private static string[] bodyNames = new string[22]
	{
		"cf_J_Hips", "cf_J_Spine01", "cf_J_Spine02", "cf_J_Spine03", "cf_J_Neck", "cf_J_Head", "cf_J_Kosi01", "cf_J_Kosi02", "cf_J_Shoulder_L", "cf_J_Foot01_L",
		"cf_J_Foot02_L", "cf_J_Shoulder_R", "cf_J_Foot01_R", "cf_J_Foot02_R", "cf_J_Mune00_L", "cf_J_Mune01_L", "cf_J_Mune02_L", "cf_J_Mune03_L", "cf_J_Mune00_R", "cf_J_Mune01_R",
		"cf_J_Mune02_R", "cf_J_Mune03_R"
	};

	public static string[] RestoreBodyNames = new string[8] { "cf_J_Hips", "cf_J_Spine01", "cf_J_Spine02", "cf_J_Spine03", "cf_J_Neck", "cf_J_Head", "cf_J_Kosi01", "cf_J_Kosi02" };

	private void Start()
	{
		Init(this);
	}

	public static void Init(FrameCorrect frameCorrect)
	{
		List<Info> list = new List<Info>();
		for (int i = 0; i < FrameNames.Length; i++)
		{
			for (int j = 0; j < frameCorrect.list.Count; j++)
			{
				if (!(FrameNames[i] != frameCorrect.list[j].data.transform.name))
				{
					list.Add(frameCorrect.list[j]);
					if (list[list.Count - 1].data.bone == null)
					{
						list[list.Count - 1].data.bone = list[list.Count - 1].data.transform;
					}
					break;
				}
			}
		}
		frameCorrect.list = new List<Info>(list);
	}

	public static void AddFrameCorrect(GameObject gameObject)
	{
		FrameCorrect component = gameObject.GetComponent<FrameCorrect>();
		if (!(component == null))
		{
			return;
		}
		component = gameObject.AddComponent<FrameCorrect>();
		component.list.Clear();
		Transform[] componentsInChildren = component.GetComponentsInChildren<Transform>(includeInactive: true);
		IEnumerable<Transform> frames = component.GetFrames(componentsInChildren, component);
		List<Info> list = new List<Info>();
		Info info = null;
		bool flag = false;
		foreach (Transform item in frames)
		{
			info = new Info(item);
			string[] array = bodyNames;
			foreach (string text in array)
			{
				if (!(text != item.name))
				{
					flag = true;
				}
			}
			info.type = ((!flag) ? Info.ProcOrderType.Last : Info.ProcOrderType.First);
			info.bone = item;
			list.Add(info);
		}
		component.list.AddRange(list);
		foreach (Info item2 in component.list)
		{
			item2.process.type = BaseProcess.Type.Target;
		}
	}

	private IEnumerable<Transform> GetFrames(Transform[] t, FrameCorrect correct)
	{
		List<string> frameNames = null;
		Transform parent = correct.transform.parent.parent;
		TestChara component = parent.GetComponent<TestChara>();
		if (component != null)
		{
			frameNames = correct.GetFrameNames(FrameNames);
			return t.Where((Transform frame) => frameNames.Contains(frame.name)).OrderBy(delegate(Transform frame)
			{
				for (int i = 0; i < frameNames.Count; i++)
				{
					if (FrameNames[i] == frame.name)
					{
						return i;
					}
				}
				return -1;
			});
		}
		return null;
	}
}
