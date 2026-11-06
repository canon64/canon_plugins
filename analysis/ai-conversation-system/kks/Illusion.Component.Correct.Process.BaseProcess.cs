using System.Collections;
using UnityEngine;

namespace Illusion.Component.Correct.Process;

[RequireComponent(typeof(BaseData))]
public abstract class BaseProcess : MonoBehaviour
{
	public enum Type
	{
		Target,
		Sync
	}

	private BaseData _data;

	public Type type;

	public bool noRestore;

	private ChaControl _chaCtrl;

	private WaitForEndOfFrame waitForEndOfFrame;

	public BaseData data
	{
		get
		{
			if (!(_data != null))
			{
				return _data = GetComponent<BaseData>();
			}
			return _data;
		}
	}

	private ChaControl chaCtrl
	{
		get
		{
			if (!(_chaCtrl != null))
			{
				return _chaCtrl = GetComponentInParent<ChaControl>();
			}
			return _chaCtrl;
		}
	}

	private WaitForEndOfFrame WaitForEndOfFrame
	{
		get
		{
			if (waitForEndOfFrame == null)
			{
				return waitForEndOfFrame = new WaitForEndOfFrame();
			}
			return waitForEndOfFrame;
		}
	}

	protected virtual void LateUpdate()
	{
		Transform bone = data.bone;
		if (bone == null)
		{
			return;
		}
		switch (type)
		{
		case Type.Target:
		{
			Vector3 localPosition = bone.localPosition;
			Quaternion localRotation = bone.localRotation;
			bone.localPosition = localPosition + data.pos;
			bone.localRotation = localRotation * data.rot;
			if (!noRestore)
			{
				StartCoroutine(Restore(bone, localPosition, localRotation));
			}
			break;
		}
		case Type.Sync:
			if (chaCtrl != null)
			{
				base.transform.SetPositionAndRotation(bone.position + chaCtrl.objBodyBone.transform.TransformDirection(data.pos), bone.rotation * data.rot);
			}
			break;
		}
	}

	protected IEnumerator Restore(Transform t, Vector3 pos, Quaternion rot)
	{
		yield return WaitForEndOfFrame;
		t.localPosition = pos;
		t.localRotation = rot;
	}
}
