using System.Collections.Generic;
using System.Linq;
using Correct.Process;
using UnityEngine;

namespace Correct;

public class IKCorrect : BaseCorrect
{
	public static string[] FrameNames = new string[13]
	{
		"f_t_hips", "f_t_thigh_L", "f_t_thigh_R", "f_t_shoulder_L", "f_t_shoulder_R", "f_t_arm_L", "f_t_arm_R", "f_t_elbo_L", "f_t_elbo_R", "f_t_knee_L",
		"f_t_knee_R", "f_t_leg_L", "f_t_leg_R"
	};

	public static void AddIKCorrect(GameObject gameObject)
	{
		IKCorrect component = gameObject.GetComponent<IKCorrect>();
		if (!(component == null))
		{
			return;
		}
		component = gameObject.AddComponent<IKCorrect>();
		Transform parent = component.transform.parent.parent;
		component.list.Clear();
		Transform[] componentsInChildren = component.GetComponentsInChildren<Transform>(includeInactive: true);
		IEnumerable<Transform> iKFrames = component.GetIKFrames(componentsInChildren, parent, component);
		List<Info> list = new List<Info>();
		Info info = null;
		foreach (Transform item in iKFrames)
		{
			info = new Info(item);
			info.type = Info.ProcOrderType.Second;
			info.bone = null;
			list.Add(info);
		}
		component.list.AddRange(list);
		foreach (Info item2 in component.list)
		{
			item2.process.type = BaseProcess.Type.Sync;
		}
	}

	private IEnumerable<Transform> GetIKFrames(Transform[] t, Transform correctRoot, IKCorrect correct)
	{
		List<string> frameNames = null;
		TestChara component = correctRoot.GetComponent<TestChara>();
		if (component != null)
		{
			frameNames = correct.GetFrameNames(FrameNames);
			return t.Where((Transform frame) => frameNames.Contains(frame.name)).OrderBy(delegate(Transform frame)
			{
				for (int i = 0; i < frameNames.Count; i++)
				{
					if (frameNames[i] == frame.name)
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
