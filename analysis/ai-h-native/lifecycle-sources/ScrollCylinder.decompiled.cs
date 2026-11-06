using System;
using System.Collections.Generic;
using Manager;
using UniRx;
using UnityEngine;
using UnityEx;

public class ScrollCylinder : MonoBehaviour
{
	public GameObject Contents;

	[Space]
	public RectTransform Atari;

	[Space]
	public ScrollCylinderNode blankObject;

	[Space]
	[SerializeField]
	private GameObject cursor;

	[SerializeField]
	private float cursorInitPos;

	[SerializeField]
	private float cursorMoveRange;

	private float cursorTime;

	private int cursorMovePtn;

	[SerializeField]
	private float cursorFirstHurfAnimTimeLimit;

	[SerializeField]
	private float cursorLaterHurfAnimTimeLimit;

	[Space]
	public float moveVal = 0.05f;

	[SerializeField]
	private float moveTime = 0.05f;

	[SerializeField]
	private List<ScrollCylinderNode> lstNodes = new List<ScrollCylinderNode>();

	[SerializeField]
	private List<ScrollCylinderNode> lstBlankNodes = new List<ScrollCylinderNode>();

	[SerializeField]
	private UnityEx.ValueTuple<int, Vector2, ScrollCylinderNode> targetNode = default(UnityEx.ValueTuple<int, Vector2, ScrollCylinderNode>);

	private Manager.Input input;

	private Subject<int> _onValueChange;

	[SerializeField]
	private bool InitList;

	private bool onEnter;

	private GameObject OldRect;

	public bool enableInternalScroll = true;

	public IObservable<int> OnValueChangeAsObservable()
	{
		return _onValueChange ?? (_onValueChange = new Subject<int>());
	}

	private void Awake()
	{
		OldRect = new GameObject("Rect2");
		OldRect.AddComponent<RectTransform>();
		OldRect.transform.SetParent(base.transform, worldPositionStays: false);
		OldRect.transform.localPosition = Vector3.zero;
		OldRect.SetActive(value: false);
		GC.Collect();
	}

	private void Start()
	{
		input = Singleton<Manager.Input>.Instance;
		PointerEnterTrigger pointerEnterTrigger = Atari.gameObject.AddComponent<PointerEnterTrigger>();
		UITrigger.TriggerEvent triggerEvent = new UITrigger.TriggerEvent();
		pointerEnterTrigger.Triggers.Add(triggerEvent);
		triggerEvent.AddListener(delegate
		{
			onEnter = true;
		});
		PointerExitTrigger pointerExitTrigger = Atari.gameObject.AddComponent<PointerExitTrigger>();
		triggerEvent = new UITrigger.TriggerEvent();
		pointerExitTrigger.Triggers.Add(triggerEvent);
		triggerEvent.AddListener(delegate
		{
			onEnter = false;
		});
		if (InitList)
		{
			ListNodeSet();
		}
	}

	public void BlankSet(ScrollCylinderNode Node, bool AddLast = false)
	{
		if (!AddLast)
		{
			lstBlankNodes.Clear();
		}
		if (blankObject == null)
		{
			return;
		}
		Vector2 sizeDelta = Node.GetComponent<RectTransform>().sizeDelta;
		RectTransform component = base.transform.GetComponent<RectTransform>();
		int[] array = new int[2]
		{
			(int)(component.rect.height / sizeDelta.y),
			(int)(component.rect.width / sizeDelta.x)
		};
		int num = Mathf.Max(array[0], array[1]);
		if (lstBlankNodes.Count == ((num % 2 == 0) ? num : (num - 1)))
		{
			return;
		}
		array[0] /= 2;
		array[1] /= 2;
		for (int i = 0; i < array.Length; i++)
		{
			for (int j = 0; j < array[i]; j++)
			{
				ScrollCylinderNode scrollCylinderNode = UnityEngine.Object.Instantiate(blankObject);
				scrollCylinderNode.transform.SetParent(Contents.transform, worldPositionStays: false);
				scrollCylinderNode.gameObject.SetActive(value: true);
				lstBlankNodes.Add(scrollCylinderNode);
			}
		}
	}

	public void ClearBlank()
	{
		foreach (ScrollCylinderNode lstBlankNode in lstBlankNodes)
		{
			UnityEngine.Object.Destroy(lstBlankNode.gameObject);
		}
		lstBlankNodes.Clear();
	}

	public void ListNodeSet(List<ScrollCylinderNode> hSceneScrollNodes = null)
	{
		lstNodes.Clear();
		if (hSceneScrollNodes != null)
		{
			lstNodes.AddRange(hSceneScrollNodes);
		}
		else
		{
			lstNodes.AddRange(Contents.GetComponentsInChildren<ScrollCylinderNode>());
		}
		if (lstNodes.Count == 0)
		{
			return;
		}
		if (cursor != null)
		{
			cursor.SetActive(lstNodes.Count != 0);
		}
		BlankSet(lstNodes[0], AddLast: true);
		for (int i = 0; i < lstNodes.Count; i++)
		{
			int index = i;
			lstNodes[index].transform.localScale = new Vector3(1f, 1f, 1f);
		}
		for (int j = 0; j < lstBlankNodes.Count; j++)
		{
			int index2 = j;
			lstBlankNodes[index2].transform.localScale = new Vector3(1f, 1f, 1f);
		}
		RectTransform ContentsRt = Contents.GetComponent<RectTransform>();
		Vector3 ContentsPosition = ContentsRt.anchoredPosition;
		if (!OldRect.activeSelf)
		{
			OldRect.SetActive(value: true);
		}
		RectTransform rt2 = OldRect.GetComponent<RectTransform>();
		OldRectSet(ContentsRt, ref rt2);
		Vector3 position2 = ContentsPosition;
		ContentsPosition = LimitPos(ContentsRt, ContentsPosition);
		ContentsRt.anchoredPosition = ContentsPosition;
		PointerEnterTrigger pointerEnterTrigger = null;
		UITrigger.TriggerEvent triggerEvent = new UITrigger.TriggerEvent();
		for (int k = 0; k < lstNodes.Count; k++)
		{
			int index3 = k;
			pointerEnterTrigger = null;
			pointerEnterTrigger = lstNodes[index3].gameObject.GetComponent<PointerEnterTrigger>();
			lstNodes[index3].gameObject.SetActive(value: true);
			if (!(pointerEnterTrigger != null))
			{
				pointerEnterTrigger = lstNodes[index3].gameObject.AddComponent<PointerEnterTrigger>();
				triggerEvent = new UITrigger.TriggerEvent();
				pointerEnterTrigger.Triggers.Add(triggerEvent);
				triggerEvent.AddListener(delegate
				{
					onEnter = true;
				});
			}
		}
		if (ContentsRt.rect.width == 0f || ContentsRt.rect.height == 0f)
		{
			Observable.NextFrame().Subscribe(delegate
			{
				InitTargrt(ContentsRt, ContentsPosition);
				OldRect.SetActive(value: false);
			});
			return;
		}
		Observable.NextFrame().Subscribe(delegate
		{
			rt2.sizeDelta = ContentsRt.sizeDelta;
			InitTargrt(rt2, position2);
			OldRect.SetActive(value: false);
		});
	}

	private void Update()
	{
		if (lstNodes.Count != 0)
		{
			MoveContents();
			ChangeNode();
		}
	}

	private void MoveContents()
	{
		RectTransform component = Contents.GetComponent<RectTransform>();
		Vector3 contentsPosition = component.anchoredPosition;
		if (enableInternalScroll)
		{
			float num = input.ScrollValue();
			if (onEnter)
			{
				if (num < 0f && targetNode.Item1 < lstNodes.Count - 1)
				{
					int num2 = targetNode.Item1 + 1;
					RectTransform component2 = lstNodes[num2].GetComponent<RectTransform>();
					Vector2 i = default(Vector2);
					i.x = component.anchoredPosition.x - component.sizeDelta.x / 2f + component2.anchoredPosition.x;
					i.y = component.anchoredPosition.y + component.sizeDelta.y / 2f + component2.anchoredPosition.y;
					i.x = contentsPosition.x - i.x;
					i.y = contentsPosition.y - i.y;
					targetNode = new UnityEx.ValueTuple<int, Vector2, ScrollCylinderNode>(num2, i, lstNodes[num2]);
					if (_onValueChange != null)
					{
						_onValueChange.OnNext(num2);
					}
				}
				else if (num > 0f && targetNode.Item1 > 0)
				{
					int num3 = targetNode.Item1 - 1;
					RectTransform component3 = lstNodes[num3].GetComponent<RectTransform>();
					Vector2 i2 = default(Vector2);
					i2.x = component.anchoredPosition.x - component.sizeDelta.x / 2f + component3.anchoredPosition.x;
					i2.y = component.anchoredPosition.y + component.sizeDelta.y / 2f + component3.anchoredPosition.y;
					i2.x = contentsPosition.x - i2.x;
					i2.y = contentsPosition.y - i2.y;
					targetNode = new UnityEx.ValueTuple<int, Vector2, ScrollCylinderNode>(num3, i2, lstNodes[num3]);
					if (_onValueChange != null)
					{
						_onValueChange.OnNext(num3);
					}
				}
			}
		}
		Vector2 item = targetNode.Item2;
		float currentVelocity = 0f;
		contentsPosition.x = Mathf.SmoothDamp(contentsPosition.x, item.x, ref currentVelocity, moveTime, float.PositiveInfinity, Time.deltaTime);
		currentVelocity = 0f;
		contentsPosition.y = Mathf.SmoothDamp(contentsPosition.y, item.y, ref currentVelocity, moveTime, float.PositiveInfinity, Time.deltaTime);
		contentsPosition = LimitPos(component, contentsPosition);
		component.anchoredPosition = contentsPosition;
	}

	private void ChangeNode()
	{
		float deltaTime = Time.deltaTime;
		ChangeNodeColor();
		ChangeNodeScl();
		if (cursor != null)
		{
			CursorMove(deltaTime);
		}
	}

	private void ChangeNodeColor()
	{
		for (int i = 0; i < lstNodes.Count; i++)
		{
			int num = i;
			if (num == targetNode.Item1)
			{
				lstNodes[num].ChangeBGAlpha(0);
			}
			else if (num == targetNode.Item1 - 1 || num == targetNode.Item1 + 1)
			{
				lstNodes[num].ChangeBGAlpha(1);
			}
			else if (num == targetNode.Item1 - 2 || num == targetNode.Item1 + 2)
			{
				lstNodes[num].ChangeBGAlpha(2);
			}
			else
			{
				lstNodes[num].ChangeBGAlpha(3);
			}
		}
		int num2 = lstBlankNodes.Count / 2;
		for (int j = 0; j < lstBlankNodes.Count; j++)
		{
			if (lstNodes.Count == 1)
			{
				if (j == num2 - 2 || j == num2 + 1)
				{
					lstBlankNodes[j].ChangeBGAlpha(2);
				}
				else if (j == num2 - 1 || j == num2)
				{
					lstBlankNodes[j].ChangeBGAlpha(1);
				}
			}
			else if (targetNode.Item1 == 0)
			{
				if (j == num2 - 2)
				{
					lstBlankNodes[j].ChangeBGAlpha(2);
				}
				else if (j == num2 - 1)
				{
					lstBlankNodes[j].ChangeBGAlpha(1);
				}
				else
				{
					lstBlankNodes[j].ChangeBGAlpha(3);
				}
			}
			else if (targetNode.Item1 == lstNodes.Count - 1)
			{
				if (j == num2)
				{
					lstBlankNodes[j].ChangeBGAlpha(1);
				}
				else if (j == num2 + 1)
				{
					lstBlankNodes[j].ChangeBGAlpha(2);
				}
				else
				{
					lstBlankNodes[j].ChangeBGAlpha(3);
				}
			}
			else
			{
				lstBlankNodes[j].ChangeBGAlpha(3);
			}
		}
	}

	private void ChangeNodeScl()
	{
		for (int i = 0; i < lstNodes.Count; i++)
		{
			int num = i;
			if (num == targetNode.Item1)
			{
				lstNodes[num].ChangeScale(0);
			}
			else if (num == targetNode.Item1 - 1 || num == targetNode.Item1 + 1)
			{
				lstNodes[num].ChangeScale(1);
			}
			else if (num == targetNode.Item1 - 2 || num == targetNode.Item1 + 2)
			{
				lstNodes[num].ChangeScale(2);
			}
			else
			{
				lstNodes[num].ChangeScale(3);
			}
		}
		int num2 = lstBlankNodes.Count / 2;
		for (int j = 0; j < lstBlankNodes.Count; j++)
		{
			if (lstNodes.Count == 1)
			{
				if (j == num2 - 2 || j == num2 + 1)
				{
					lstBlankNodes[j].ChangeScale(2);
				}
				else if (j == num2 - 1 || j == num2)
				{
					lstBlankNodes[j].ChangeScale(1);
				}
			}
			else if (targetNode.Item1 == 0)
			{
				if (j == num2 - 2)
				{
					lstBlankNodes[j].ChangeScale(2);
				}
				else if (j == num2 - 1)
				{
					lstBlankNodes[j].ChangeScale(1);
				}
				else
				{
					lstBlankNodes[j].ChangeScale(3);
				}
			}
			else if (targetNode.Item1 == lstNodes.Count - 1)
			{
				if (j == num2)
				{
					lstBlankNodes[j].ChangeScale(1);
				}
				else if (j == num2 + 1)
				{
					lstBlankNodes[j].ChangeScale(2);
				}
				else
				{
					lstBlankNodes[j].ChangeScale(3);
				}
			}
			else
			{
				lstBlankNodes[j].ChangeScale(3);
			}
		}
	}

	private void CursorMove(float deltaTime)
	{
		float num = 0f;
		if (cursorMovePtn == 0)
		{
			cursorTime += deltaTime / cursorFirstHurfAnimTimeLimit;
			num = Mathf.InverseLerp(0f, 1f, cursorTime);
			if (num == 1f)
			{
				cursorMovePtn = 1;
			}
		}
		else
		{
			cursorTime -= deltaTime / cursorLaterHurfAnimTimeLimit;
			num = Mathf.InverseLerp(0f, 1f, cursorTime);
			if (num == 0f)
			{
				cursorMovePtn = 0;
			}
		}
		Vector3 zero = Vector3.zero;
		zero = cursor.GetComponent<RectTransform>().anchoredPosition;
		zero.x = Mathf.Lerp(cursorInitPos - cursorMoveRange / 2f, cursorInitPos + cursorMoveRange / 2f, num);
		cursor.GetComponent<RectTransform>().anchoredPosition = zero;
	}

	public List<ScrollCylinderNode> GetList()
	{
		return lstNodes;
	}

	public UnityEx.ValueTuple<int, Vector2, ScrollCylinderNode> GetTarget()
	{
		return targetNode;
	}

	public void SetTarget(ScrollCylinderNode target)
	{
		RectTransform component = Contents.GetComponent<RectTransform>();
		Vector3 vector = component.anchoredPosition;
		Vector2 i2 = default(Vector2);
		for (int i = 0; i < lstNodes.Count; i++)
		{
			int num = i;
			if (!(lstNodes[num] != target))
			{
				RectTransform component2 = lstNodes[num].GetComponent<RectTransform>();
				i2.x = component.anchoredPosition.x - component.rect.width / 2f + component2.anchoredPosition.x;
				i2.y = component.anchoredPosition.y + component.rect.height / 2f + component2.anchoredPosition.y;
				i2.x = vector.x - i2.x;
				i2.y = vector.y - i2.y;
				targetNode = new UnityEx.ValueTuple<int, Vector2, ScrollCylinderNode>(num, i2, target);
				if (_onValueChange != null)
				{
					_onValueChange.OnNext(num);
				}
				break;
			}
		}
	}

	private Vector3 LimitPos(RectTransform ContentsRt, Vector3 ContentsPosition)
	{
		RectTransform component = base.transform.GetComponent<RectTransform>();
		float num = 0f;
		float num2 = 0f;
		if (component.sizeDelta.y / ContentsRt.rect.height % 2f != 0f)
		{
			num = ContentsPosition.y + ContentsRt.rect.height / 2f - component.sizeDelta.y / 2f;
			num2 = ContentsPosition.y - ContentsRt.rect.height / 2f - (0f - component.sizeDelta.y) / 2f;
		}
		else if (lstNodes[0] != null)
		{
			RectTransform component2 = lstNodes[0].GetComponent<RectTransform>();
			num = ContentsPosition.y + ContentsRt.rect.height / 2f - component2.sizeDelta.y / 2f - component.sizeDelta.y / 2f;
			num2 = ContentsPosition.y - ContentsRt.rect.height / 2f + component2.sizeDelta.y / 2f - (0f - component.sizeDelta.y) / 2f;
		}
		if (num <= 0f)
		{
			ContentsPosition.y += Mathf.Abs(num);
		}
		else if (num2 >= 0f)
		{
			ContentsPosition.y -= Mathf.Abs(num2);
		}
		float num3 = 0f;
		float num4 = 0f;
		if (component.sizeDelta.x / ContentsRt.rect.width % 2f != 0f)
		{
			num3 = ContentsPosition.x - ContentsRt.rect.width / 2f - (0f - component.sizeDelta.x) / 2f;
			num4 = ContentsPosition.x + ContentsRt.rect.width / 2f - component.sizeDelta.x / 2f;
		}
		else if (lstNodes[0] != null)
		{
			RectTransform component3 = lstNodes[0].GetComponent<RectTransform>();
			num3 = ContentsPosition.x + ContentsRt.rect.width / 2f - component3.sizeDelta.x / 2f - component.sizeDelta.x / 2f;
			num4 = ContentsPosition.x - ContentsRt.rect.width / 2f + component3.sizeDelta.x / 2f - (0f - component.sizeDelta.x) / 2f;
		}
		if (num3 >= 0f)
		{
			ContentsPosition.x -= Mathf.Abs(num3);
		}
		else if (num4 <= 0f)
		{
			ContentsPosition.x += Mathf.Abs(num4);
		}
		return ContentsPosition;
	}

	private void InitTargrt(RectTransform ContentsRt, Vector3 position)
	{
		float num = 0f;
		float num2 = 0f;
		RectTransform component = base.transform.GetComponent<RectTransform>();
		if (component.sizeDelta.x / ContentsRt.rect.width % 2f != 0f)
		{
			num = position.x - ContentsRt.rect.width / 2f - (0f - component.sizeDelta.x) / 2f;
		}
		else if (lstNodes[0] != null)
		{
			RectTransform component2 = lstNodes[0].GetComponent<RectTransform>();
			num = position.x - ContentsRt.rect.width / 2f - component2.sizeDelta.x / 2f - (0f - component.sizeDelta.x) / 2f;
		}
		if (component.sizeDelta.y / ContentsRt.rect.height % 2f != 0f)
		{
			num2 = position.y + ContentsRt.rect.height / 2f - component.sizeDelta.y / 2f;
		}
		else if (lstNodes[0] != null)
		{
			RectTransform component3 = lstNodes[0].GetComponent<RectTransform>();
			num2 = position.y + ContentsRt.rect.height / 2f - component3.sizeDelta.y / 2f - component.sizeDelta.y / 2f;
		}
		num = position.x - num;
		num2 = position.y - num2;
		targetNode = new UnityEx.ValueTuple<int, Vector2, ScrollCylinderNode>(0, new Vector2(num, num2), lstNodes[0]);
		if (_onValueChange != null)
		{
			_onValueChange.OnNext(0);
		}
	}

	private void OldRectSet(RectTransform src, ref RectTransform newRect)
	{
		newRect.anchoredPosition = src.anchoredPosition;
		newRect.anchoredPosition3D = src.anchoredPosition3D;
		newRect.anchorMax = src.anchorMax;
		newRect.anchorMin = src.anchorMin;
		newRect.offsetMax = src.offsetMax;
		newRect.offsetMin = src.offsetMin;
		newRect.pivot = src.pivot;
		newRect.sizeDelta = src.sizeDelta;
	}
}
