using Manager;
using UnityEngine;
using UnityEngine.UI;
using UnityEx;

public class HSceneSpriteCategories : MonoBehaviour
{
	[SerializeField]
	private GameObject[] MainCategory;

	private Button[] btMainCategory;

	private bool[] bMainCategory;

	public bool[] MainCategoryActive;

	[SerializeField]
	private GameObject[] SubCategory;

	private Button[] btSubCategory;

	private bool[] bSubCategory;

	public bool[] SubCategoryActive;

	[SerializeField]
	[Space]
	private float[] MainPosX = new float[2];

	[SerializeField]
	private float[] SubPosX = new float[2];

	[SerializeField]
	[Space]
	private float smoothTime;

	private bool canUse = true;

	private void Start()
	{
		MainCategoryActive = new bool[MainCategory.Length];
		SubCategoryActive = new bool[SubCategory.Length];
		for (int i = 0; i < MainCategoryActive.Length; i++)
		{
			MainCategoryActive[i] = false;
		}
		for (int j = 0; j < SubCategoryActive.Length; j++)
		{
			SubCategoryActive[j] = false;
		}
		PointerEnterTrigger pointerEnterTrigger = null;
		UITrigger.TriggerEvent triggerEvent = new UITrigger.TriggerEvent();
		PointerExitTrigger pointerExitTrigger = null;
		UITrigger.TriggerEvent triggerEvent2 = new UITrigger.TriggerEvent();
		bMainCategory = new bool[MainCategory.Length];
		btMainCategory = new Button[MainCategory.Length];
		for (int k = 0; k < MainCategory.Length; k++)
		{
			int no = k;
			pointerEnterTrigger = null;
			triggerEvent = new UITrigger.TriggerEvent();
			pointerEnterTrigger = MainCategory[no].gameObject.AddComponent<PointerEnterTrigger>();
			pointerEnterTrigger.Triggers.Add(triggerEvent);
			triggerEvent.AddListener(delegate
			{
				if (Cursor.visible)
				{
					bMainCategory[no] = true;
				}
			});
			pointerExitTrigger = null;
			triggerEvent2 = new UITrigger.TriggerEvent();
			pointerExitTrigger = MainCategory[no].gameObject.AddComponent<PointerExitTrigger>();
			pointerExitTrigger.Triggers.Add(triggerEvent2);
			triggerEvent2.AddListener(delegate
			{
				bMainCategory[no] = false;
			});
			bMainCategory[no] = false;
			btMainCategory[no] = MainCategory[no].GetComponentInChildren<Button>();
		}
		bSubCategory = new bool[SubCategory.Length];
		btSubCategory = new Button[SubCategory.Length];
		for (int num = 0; num < SubCategory.Length; num++)
		{
			int no2 = num;
			pointerEnterTrigger = null;
			triggerEvent = new UITrigger.TriggerEvent();
			pointerEnterTrigger = SubCategory[no2].gameObject.AddComponent<PointerEnterTrigger>();
			pointerEnterTrigger.Triggers.Add(triggerEvent);
			triggerEvent.AddListener(delegate
			{
				if (Cursor.visible)
				{
					bSubCategory[no2] = true;
				}
			});
			pointerExitTrigger = null;
			triggerEvent2 = new UITrigger.TriggerEvent();
			pointerExitTrigger = SubCategory[no2].gameObject.AddComponent<PointerExitTrigger>();
			pointerExitTrigger.Triggers.Add(triggerEvent2);
			triggerEvent2.AddListener(delegate
			{
				bSubCategory[no2] = false;
			});
			bSubCategory[no2] = false;
			btSubCategory[no2] = SubCategory[no2].GetComponentInChildren<Button>();
		}
	}

	private void Update()
	{
		float deltaTime = Time.deltaTime;
		Vector3 zero = Vector3.zero;
		float num = 0f;
		RectTransform rectTransform = null;
		for (int i = 0; i < btMainCategory.Length; i++)
		{
			int num2 = i;
			num = 0f;
			rectTransform = btMainCategory[num2].GetComponent<RectTransform>();
			zero = rectTransform.anchoredPosition;
			if (bMainCategory[num2] || MainCategoryActive[num2])
			{
				zero.x = Mathf.SmoothDamp(zero.x, MainPosX[0], ref num, smoothTime, float.PositiveInfinity, deltaTime);
			}
			else
			{
				zero.x = Mathf.SmoothDamp(zero.x, MainPosX[1], ref num, smoothTime, float.PositiveInfinity, deltaTime);
			}
			rectTransform.anchoredPosition = zero;
		}
		for (int j = 0; j < btSubCategory.Length; j++)
		{
			int num3 = j;
			num = 0f;
			rectTransform = btSubCategory[num3].GetComponent<RectTransform>();
			zero = rectTransform.anchoredPosition;
			if (bSubCategory[num3] || SubCategoryActive[num3])
			{
				zero.x = Mathf.SmoothDamp(zero.x, SubPosX[0], ref num, smoothTime, float.PositiveInfinity, deltaTime);
			}
			else
			{
				zero.x = Mathf.SmoothDamp(zero.x, SubPosX[1], ref num, smoothTime, float.PositiveInfinity, deltaTime);
			}
			rectTransform.anchoredPosition = zero;
		}
	}

	public void Changebuttonactive(bool val)
	{
		if (canUse == val)
		{
			return;
		}
		canUse = val;
		for (int i = 0; i < btMainCategory.Length; i++)
		{
			if (!val)
			{
				bMainCategory[i] = val;
			}
			if (i <= 2 || Singleton<HSceneManager>.Instance.EventKind != HSceneManager.HEvent.GyakuYobai || Singleton<HSceneFlagCtrl>.Instance.initiative != 2)
			{
				btMainCategory[i].interactable = val;
			}
		}
		for (int j = 0; j < btSubCategory.Length; j++)
		{
			if (!val)
			{
				bSubCategory[j] = val;
			}
			btSubCategory[j].interactable = val;
		}
	}

	public void AllForceClose(int mode = 0)
	{
		RectTransform rectTransform = null;
		Vector3 zero = Vector3.zero;
		for (int i = 0; i < btMainCategory.Length; i++)
		{
			int num = i;
			rectTransform = btMainCategory[num].GetComponent<RectTransform>();
			zero.x = MainPosX[1];
			rectTransform.anchoredPosition = zero;
		}
		for (int j = 0; j < btSubCategory.Length; j++)
		{
			int num2 = j;
			if (mode != 1 || num2 != 1)
			{
				rectTransform = btSubCategory[num2].GetComponent<RectTransform>();
				zero.x = SubPosX[1];
				rectTransform.anchoredPosition = zero;
			}
		}
	}
}
