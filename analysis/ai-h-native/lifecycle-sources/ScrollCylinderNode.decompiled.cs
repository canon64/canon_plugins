using UnityEngine;
using UnityEngine.UI;

public class ScrollCylinderNode : MonoBehaviour
{
	public Image BG;

	public Text text;

	public float smoothTime;

	public float smoothTimeV2;

	public float[] alpha = new float[4];

	public float[] scale = new float[4];

	protected int phaseScale;

	protected int prephaseScale;

	protected Image ToggleCheckMark;

	protected float endA;

	protected Vector3 endScl = Vector3.zero;

	protected Color tmpColor;

	protected Vector3 tmpScl;

	protected virtual void Update()
	{
		tmpColor = BG.color;
		float currentVelocity = 0f;
		float deltaTime = Time.deltaTime;
		tmpColor.a = Mathf.SmoothDamp(tmpColor.a, endA, ref currentVelocity, smoothTime, float.PositiveInfinity, deltaTime);
		BG.color = tmpColor;
		Toggle component = GetComponent<Toggle>();
		if (component != null)
		{
			ToggleCheckMark = component.graphic.GetComponent<Image>();
			if (ToggleCheckMark != null)
			{
				tmpColor.r = ToggleCheckMark.color.r;
				tmpColor.g = ToggleCheckMark.color.g;
				tmpColor.b = ToggleCheckMark.color.b;
				ToggleCheckMark.color = tmpColor;
			}
		}
		if (text != null)
		{
			tmpColor.r = text.color.r;
			tmpColor.g = text.color.g;
			tmpColor.b = text.color.b;
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

	public void ChangeBGAlpha(int id)
	{
		endA = alpha[id];
	}

	public void ChangeScale(int id)
	{
		prephaseScale = phaseScale;
		endScl = new Vector3(scale[id], scale[id], scale[id]);
		phaseScale = id;
	}
}
