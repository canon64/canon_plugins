using UnityEngine;

public class HRotationScrollNode : ScrollCylinderNode
{
	public int id = -1;

	protected override void Update()
	{
		tmpColor = BG.color;
		float currentVelocity = 0f;
		float deltaTime = Time.deltaTime;
		tmpColor.a = Mathf.SmoothDamp(tmpColor.a, endA, ref currentVelocity, smoothTime, float.PositiveInfinity, deltaTime);
		BG.color = tmpColor;
		if (text != null)
		{
			text.color = tmpColor;
		}
		tmpScl = BG.transform.localScale;
		Vector3 currentVelocity2 = Vector3.zero;
		if ((prephaseScale == 0 && phaseScale == 1) || (prephaseScale == 1 && phaseScale == 0))
		{
			tmpScl = Vector3.SmoothDamp(tmpScl, endScl, ref currentVelocity2, smoothTime, float.PositiveInfinity, deltaTime);
		}
		else
		{
			tmpScl = Vector3.SmoothDamp(tmpScl, endScl, ref currentVelocity2, smoothTimeV2, float.PositiveInfinity, deltaTime);
		}
		BG.transform.localScale = tmpScl;
		if (text != null)
		{
			text.transform.localScale = tmpScl;
		}
	}
}
