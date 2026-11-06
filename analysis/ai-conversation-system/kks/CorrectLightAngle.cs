using System;
using System.Collections;
using System.Linq;
using Manager;
using UnityEngine;

public class CorrectLightAngle : MonoBehaviour
{
	[SerializeField]
	private Light dlight;

	[SerializeField]
	private float correctRX = 5f;

	[SerializeField]
	private float correctRY = 190f;

	private Quaternion initRot = Quaternion.identity;

	public Func<ChaControl> condition { get; set; }

	public Vector2 offset { get; set; }

	public Transform lightTrans { get; private set; }

	public Transform GetNeck(ChaControl _cha)
	{
		if (_cha == null || !_cha.hiPoly)
		{
			return null;
		}
		GameObject referenceInfo = _cha.GetReferenceInfo(ChaReference.RefObjKey.HeadParent);
		if (!(referenceInfo == null))
		{
			return referenceInfo.transform;
		}
		return null;
	}

	public void Reset()
	{
		dlight = GetComponentInChildren<Light>();
		if (dlight != null)
		{
			lightTrans = dlight.transform;
			initRot = lightTrans.localRotation;
		}
		else
		{
			lightTrans = null;
			initRot = Quaternion.identity;
		}
		correctRX = 5f;
		correctRY = 190f;
	}

	private void Awake()
	{
		lightTrans = dlight.transform;
		initRot = lightTrans.localRotation;
	}

	private IEnumerator Start()
	{
		base.enabled = false;
		yield return new WaitUntil(() => Singleton<Character>.IsInstance());
		base.enabled = true;
	}

	private void LateUpdate()
	{
		ChaControl cha;
		if (!condition.IsNullOrEmpty())
		{
			cha = condition();
		}
		else
		{
			ChaControl[] array = Character.ChaControls.Where((ChaControl p) => p.hiPoly).ToArray();
			cha = ((array.Length != 1) ? null : array.FirstOrDefault());
		}
		Transform neck = GetNeck(cha);
		if (neck == null)
		{
			lightTrans.localRotation = initRot;
			return;
		}
		lightTrans.rotation = neck.rotation;
		lightTrans.Rotate(correctRX + offset.x, correctRY + offset.y, 0f);
	}
}
