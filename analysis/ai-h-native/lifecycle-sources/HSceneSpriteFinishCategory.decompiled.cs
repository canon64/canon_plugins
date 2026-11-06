using UniRx;
using UnityEngine;
using UnityEngine.UI;
using UnityEx;

public class HSceneSpriteFinishCategory : HSceneSpriteCategory
{
	[SerializeField]
	private GameObject atari;

	[SerializeField]
	private Button Finish;

	private bool onFinish;

	[SerializeField]
	private Button[] FinishPattern;

	[Space]
	[SerializeField]
	private float[] MainPosY = new float[2];

	[SerializeField]
	[Space]
	private float smoothTime;

	private GameObject BeforeObj;

	private RectTransform rt;

	private BoolReactiveProperty BeforeActive = new BoolReactiveProperty(initialValue: false);

	private bool oldBeforeActive;

	public void Init()
	{
		if (atari.GetComponent<PointerEnterTrigger>() == null)
		{
			PointerEnterTrigger pointerEnterTrigger = null;
			UITrigger.TriggerEvent triggerEvent = new UITrigger.TriggerEvent();
			pointerEnterTrigger = atari.AddComponent<PointerEnterTrigger>();
			pointerEnterTrigger.Triggers.Add(triggerEvent);
			triggerEvent.AddListener(delegate
			{
				if (Cursor.visible)
				{
					onFinish = true;
				}
			});
		}
		if (atari.GetComponent<PointerExitTrigger>() == null)
		{
			PointerExitTrigger pointerExitTrigger = null;
			UITrigger.TriggerEvent triggerEvent2 = new UITrigger.TriggerEvent();
			pointerExitTrigger = atari.AddComponent<PointerExitTrigger>();
			pointerExitTrigger.Triggers.Add(triggerEvent2);
			triggerEvent2.AddListener(delegate
			{
				onFinish = false;
			});
		}
		rt = Finish.GetComponent<RectTransform>();
		BeforeObj = Finish.gameObject;
		BeforeActive.Where((bool active) => active != oldBeforeActive).Subscribe(delegate(bool active)
		{
			oldBeforeActive = active;
			atari.SetActive(active);
		});
		base.gameObject.SetActive(value: true);
	}

	private void Update()
	{
		BeforeActive.Value = BeforeObj.activeSelf;
		if (atari.activeSelf)
		{
			float deltaTime = Time.deltaTime;
			Vector3 zero = Vector3.zero;
			float currentVelocity = 0f;
			zero = rt.anchoredPosition;
			if (onFinish)
			{
				zero.y = Mathf.SmoothDamp(zero.y, MainPosY[0], ref currentVelocity, smoothTime, float.PositiveInfinity, deltaTime);
			}
			else
			{
				zero.y = Mathf.SmoothDamp(zero.y, MainPosY[1], ref currentVelocity, smoothTime, float.PositiveInfinity, deltaTime);
			}
			rt.anchoredPosition = zero;
		}
	}
}
