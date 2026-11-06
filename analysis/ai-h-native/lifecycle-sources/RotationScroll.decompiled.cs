using System.Collections.Generic;
using AIProject;
using Manager;
using UniRx;
using UnityEngine;
using UnityEx;

public class RotationScroll : MonoBehaviour
{
	public GameObject Contents;

	[Space]
	[SerializeField]
	public RectTransform SelectRect;

	[Space]
	public RectTransform Atari;

	[SerializeField]
	private float moveTime = 0.05f;

	[SerializeField]
	private LinkedList<ScrollCylinderNode> lstNodes = new LinkedList<ScrollCylinderNode>();

	[SerializeField]
	private ValueTuple<Vector2, ScrollCylinderNode> targetNode = default(ValueTuple<Vector2, ScrollCylinderNode>);

	private Manager.Input input;

	[SerializeField]
	private bool InitList;

	[SerializeField]
	private int MaxView = 3;

	private bool onEnter;

	public HRotationScrollNode[] NodeList;

	public ScrollDir scrollMode;

	private void Start()
	{
		input = Singleton<Manager.Input>.Instance;
		PointerEnterTrigger pointerEnterTrigger = Atari.gameObject.AddComponent<PointerEnterTrigger>();
		UITrigger.TriggerEvent triggerEvent = new UITrigger.TriggerEvent();
		pointerEnterTrigger.Triggers.Add(triggerEvent);
		triggerEvent.AddListener(delegate
		{
			if (Cursor.visible)
			{
				onEnter = true;
			}
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

	public void ListNodeSet(List<ScrollCylinderNode> ScrollNodes = null, bool targetInit = true)
	{
		lstNodes.Clear();
		if (ScrollNodes != null)
		{
			for (int i = 0; i < ScrollNodes.Count; i++)
			{
				RectTransform component = ScrollNodes[i].GetComponent<RectTransform>();
				if (ScrollNodes.Count < MaxView)
				{
					component.anchoredPosition = new Vector2(component.rect.width / 2f, (0f - component.rect.height) / 2f) + new Vector2((scrollMode != ScrollDir.Vertical) ? (component.rect.width * (float)(MaxView - 1)) : 0f, (scrollMode != ScrollDir.Horizontal) ? ((0f - component.rect.height) * (float)(MaxView - 1)) : 0f) * i;
				}
				else
				{
					component.anchoredPosition = new Vector2(component.rect.width / 2f, (0f - component.rect.height) / 2f) + new Vector2((scrollMode != ScrollDir.Vertical) ? component.rect.width : 0f, (scrollMode != ScrollDir.Horizontal) ? (0f - component.rect.height) : 0f) * i;
				}
				lstNodes.AddLast(ScrollNodes[i]);
			}
		}
		else
		{
			ScrollCylinderNode[] componentsInChildren = Contents.GetComponentsInChildren<ScrollCylinderNode>();
			for (int j = 0; j < componentsInChildren.Length; j++)
			{
				RectTransform component2 = componentsInChildren[j].GetComponent<RectTransform>();
				if (componentsInChildren.Length < MaxView)
				{
					component2.anchoredPosition = new Vector2(component2.rect.width / 2f, (0f - component2.rect.height) / 2f) + new Vector2((scrollMode != ScrollDir.Vertical) ? (component2.rect.width * (float)(MaxView - 1)) : 0f, (scrollMode != ScrollDir.Horizontal) ? ((0f - component2.rect.height) * (float)(MaxView - 1)) : 0f) * j;
				}
				else
				{
					component2.anchoredPosition = new Vector2(component2.rect.width / 2f, (0f - component2.rect.height) / 2f) + new Vector2((scrollMode != ScrollDir.Vertical) ? component2.rect.width : 0f, (scrollMode != ScrollDir.Horizontal) ? (0f - component2.rect.height) : 0f) * j;
				}
				lstNodes.AddLast(componentsInChildren[j]);
			}
		}
		if (lstNodes.Count == 0)
		{
			return;
		}
		RectTransform ContentsRt = Contents.GetComponent<RectTransform>();
		Vector3 ContentsPosition = ContentsRt.anchoredPosition;
		RectTransform rt2 = Object.Instantiate(ContentsRt);
		Vector3 position2 = ContentsPosition;
		ContentsPosition = LimitPos(ContentsRt, ContentsPosition, (scrollMode == ScrollDir.Vertical) ? 1 : 0);
		ContentsRt.anchoredPosition = ContentsPosition;
		PointerEnterTrigger pointerEnterTrigger = null;
		UITrigger.TriggerEvent triggerEvent = new UITrigger.TriggerEvent();
		foreach (ScrollCylinderNode lstNode in lstNodes)
		{
			pointerEnterTrigger = null;
			pointerEnterTrigger = lstNode.gameObject.GetComponent<PointerEnterTrigger>();
			lstNode.gameObject.SetActive(value: true);
			if (!(pointerEnterTrigger != null))
			{
				pointerEnterTrigger = lstNode.gameObject.AddComponent<PointerEnterTrigger>();
				triggerEvent = new UITrigger.TriggerEvent();
				pointerEnterTrigger.Triggers.Add(triggerEvent);
				triggerEvent.AddListener(delegate
				{
					onEnter = true;
				});
			}
		}
		if (!targetInit)
		{
			if (rt2 != null && rt2.gameObject != null)
			{
				Object.Destroy(rt2.gameObject);
			}
			return;
		}
		if (ContentsRt.rect.width == 0f || ContentsRt.rect.height == 0f)
		{
			Observable.NextFrame().Subscribe(delegate
			{
				InitTargrt(ContentsRt, ContentsPosition);
				Object.Destroy(rt2.gameObject);
			});
			return;
		}
		Observable.NextFrame().Subscribe(delegate
		{
			rt2.sizeDelta = ContentsRt.sizeDelta;
			InitTargrt(rt2, position2);
			Object.Destroy(rt2.gameObject);
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
		float num = input.ScrollValue();
		LinkedListNode<ScrollCylinderNode> linkedListNode = lstNodes.Find(targetNode.Item2);
		if (linkedListNode == null)
		{
			return;
		}
		if (lstNodes.Count == 1)
		{
			num = 0f;
		}
		if (onEnter)
		{
			if (num < 0f)
			{
				LinkedListNode<ScrollCylinderNode> linkedListNode2 = ((lstNodes.Count < MaxView) ? (linkedListNode.Next ?? lstNodes.First) : linkedListNode.Next);
				RectTransform component2 = linkedListNode2.Value.GetComponent<RectTransform>();
				if (lstNodes.Count < MaxView)
				{
					RectTransform component3 = linkedListNode.Value.GetComponent<RectTransform>();
					component2.anchoredPosition = component3.anchoredPosition + new Vector2((scrollMode != ScrollDir.Vertical) ? (component3.rect.width * (float)(MaxView - 1)) : 0f, (scrollMode != ScrollDir.Horizontal) ? ((0f - component3.rect.height) * (float)(MaxView - 1)) : 0f);
					if (linkedListNode2 == lstNodes.First)
					{
						LinkedListNode<ScrollCylinderNode> node = new LinkedListNode<ScrollCylinderNode>(lstNodes.First.Value);
						lstNodes.RemoveFirst();
						lstNodes.AddLast(node);
						linkedListNode2 = lstNodes.Last;
						component2 = linkedListNode2.Value.GetComponent<RectTransform>();
					}
				}
				Vector2 i = default(Vector2);
				i.x = component.anchoredPosition.x - component.sizeDelta.x / 2f + component2.anchoredPosition.x;
				i.y = component.anchoredPosition.y + component.sizeDelta.y / 2f + component2.anchoredPosition.y;
				i.x = contentsPosition.x - i.x;
				i.y = contentsPosition.y - i.y;
				targetNode = new ValueTuple<Vector2, ScrollCylinderNode>(i, linkedListNode2.Value);
				linkedListNode = linkedListNode2;
				Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.Select);
			}
			else if (num > 0f)
			{
				LinkedListNode<ScrollCylinderNode> linkedListNode3 = ((lstNodes.Count < MaxView) ? (linkedListNode.Previous ?? lstNodes.Last) : linkedListNode.Previous);
				RectTransform component4 = linkedListNode3.Value.GetComponent<RectTransform>();
				if (lstNodes.Count < MaxView)
				{
					RectTransform component5 = linkedListNode.Value.GetComponent<RectTransform>();
					component4.anchoredPosition = component5.anchoredPosition + new Vector2((scrollMode != ScrollDir.Vertical) ? ((0f - component5.rect.width) * (float)(MaxView - 1)) : 0f, (scrollMode != ScrollDir.Horizontal) ? (component5.rect.height * (float)(MaxView - 1)) : 0f);
					if (linkedListNode3 == lstNodes.Last)
					{
						LinkedListNode<ScrollCylinderNode> node2 = new LinkedListNode<ScrollCylinderNode>(lstNodes.Last.Value);
						lstNodes.RemoveLast();
						lstNodes.AddFirst(node2);
						linkedListNode3 = lstNodes.First;
						component4 = linkedListNode3.Value.GetComponent<RectTransform>();
					}
				}
				Vector2 i2 = default(Vector2);
				i2.x = component.anchoredPosition.x - component.sizeDelta.x / 2f + component4.anchoredPosition.x;
				i2.y = component.anchoredPosition.y + component.sizeDelta.y / 2f + component4.anchoredPosition.y;
				i2.x = contentsPosition.x - i2.x;
				i2.y = contentsPosition.y - i2.y;
				targetNode = new ValueTuple<Vector2, ScrollCylinderNode>(i2, linkedListNode3.Value);
				linkedListNode = linkedListNode3;
				Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.Select);
			}
		}
		if (lstNodes.Count >= MaxView)
		{
			if (linkedListNode.Previous == null)
			{
				LinkedListNode<ScrollCylinderNode> linkedListNode4 = new LinkedListNode<ScrollCylinderNode>(lstNodes.Last.Value);
				lstNodes.RemoveLast();
				lstNodes.AddFirst(linkedListNode4);
				RectTransform[] array = new RectTransform[2]
				{
					linkedListNode4.Value.GetComponent<RectTransform>(),
					linkedListNode.Value.GetComponent<RectTransform>()
				};
				Vector2 anchoredPosition = array[0].anchoredPosition;
				if (array[0].anchoredPosition.x == array[1].anchoredPosition.x)
				{
					anchoredPosition.y = array[1].anchoredPosition.y + array[1].rect.height;
				}
				if (array[0].anchoredPosition.y == array[1].anchoredPosition.y)
				{
					anchoredPosition.x = array[1].anchoredPosition.x - array[1].rect.width;
				}
				array[0].anchoredPosition = anchoredPosition;
			}
			if (linkedListNode.Next == null)
			{
				LinkedListNode<ScrollCylinderNode> linkedListNode4 = new LinkedListNode<ScrollCylinderNode>(lstNodes.First.Value);
				lstNodes.RemoveFirst();
				lstNodes.AddLast(linkedListNode4);
				RectTransform[] array2 = new RectTransform[2]
				{
					linkedListNode4.Value.GetComponent<RectTransform>(),
					linkedListNode.Value.GetComponent<RectTransform>()
				};
				Vector2 anchoredPosition2 = array2[0].anchoredPosition;
				if (array2[0].anchoredPosition.x == array2[1].anchoredPosition.x)
				{
					anchoredPosition2.y = array2[1].anchoredPosition.y - array2[0].rect.height;
				}
				if (array2[0].anchoredPosition.y == array2[1].anchoredPosition.y)
				{
					anchoredPosition2.x = array2[1].anchoredPosition.x + array2[0].rect.width;
				}
				array2[0].anchoredPosition = anchoredPosition2;
			}
		}
		Vector2 item = targetNode.Item1;
		float currentVelocity = 0f;
		contentsPosition.x = Mathf.SmoothDamp(contentsPosition.x, item.x, ref currentVelocity, moveTime, float.PositiveInfinity, Time.deltaTime);
		currentVelocity = 0f;
		contentsPosition.y = Mathf.SmoothDamp(contentsPosition.y, item.y, ref currentVelocity, moveTime, float.PositiveInfinity, Time.deltaTime);
		contentsPosition = LimitPos(component, contentsPosition, (scrollMode == ScrollDir.Vertical) ? 1 : 0);
		component.anchoredPosition = contentsPosition;
	}

	private void ChangeNode()
	{
		float deltaTime = Time.deltaTime;
		ChangeNodeColor();
		ChangeNodeScl();
	}

	private void ChangeNodeColor()
	{
		LinkedListNode<ScrollCylinderNode> linkedListNode = lstNodes.Find(targetNode.Item2);
		if (linkedListNode == null)
		{
			return;
		}
		foreach (ScrollCylinderNode lstNode in lstNodes)
		{
			if (lstNode == linkedListNode.Value)
			{
				lstNode.ChangeBGAlpha(0);
			}
			else if (lstNode == linkedListNode.Previous?.Value || lstNode == linkedListNode.Next?.Value)
			{
				lstNode.ChangeBGAlpha(1);
			}
			else
			{
				lstNode.ChangeBGAlpha(3);
			}
		}
	}

	private void ChangeNodeScl()
	{
		LinkedListNode<ScrollCylinderNode> linkedListNode = lstNodes.Find(targetNode.Item2);
		if (linkedListNode == null)
		{
			return;
		}
		foreach (ScrollCylinderNode lstNode in lstNodes)
		{
			if (lstNode == linkedListNode.Value)
			{
				lstNode.ChangeScale(0);
			}
			else if (lstNode == linkedListNode.Previous?.Value || lstNode == linkedListNode.Next?.Value)
			{
				lstNode.ChangeScale(1);
			}
			else
			{
				lstNode.ChangeScale(3);
			}
		}
	}

	public LinkedList<ScrollCylinderNode> GetList()
	{
		return lstNodes;
	}

	public ValueTuple<Vector2, ScrollCylinderNode> GetTarget()
	{
		return targetNode;
	}

	public void SetTarget(ScrollCylinderNode target)
	{
		RectTransform component = Contents.GetComponent<RectTransform>();
		Vector3 vector = component.anchoredPosition;
		Vector2 i = default(Vector2);
		foreach (ScrollCylinderNode lstNode in lstNodes)
		{
			if (lstNode != target)
			{
				continue;
			}
			RectTransform component2 = lstNode.GetComponent<RectTransform>();
			i.x = component.anchoredPosition.x - component.rect.width / 2f + component2.anchoredPosition.x;
			i.y = component.anchoredPosition.y + component.rect.height / 2f + component2.anchoredPosition.y;
			i.x = vector.x - i.x;
			i.y = vector.y - i.y;
			targetNode = new ValueTuple<Vector2, ScrollCylinderNode>(i, target);
			break;
		}
	}

	public void SetTarget(int taii)
	{
		RectTransform component = Contents.GetComponent<RectTransform>();
		Vector3 vector = component.anchoredPosition;
		Vector2 i = default(Vector2);
		foreach (ScrollCylinderNode lstNode in lstNodes)
		{
			int id = lstNode.GetComponent<HRotationScrollNode>().id;
			if (id != taii)
			{
				continue;
			}
			RectTransform component2 = lstNode.GetComponent<RectTransform>();
			i.x = component.anchoredPosition.x - component.rect.width / 2f + component2.anchoredPosition.x;
			i.y = component.anchoredPosition.y + component.rect.height / 2f + component2.anchoredPosition.y;
			i.x = vector.x - i.x;
			i.y = vector.y - i.y;
			targetNode = new ValueTuple<Vector2, ScrollCylinderNode>(i, lstNode);
			break;
		}
	}

	private Vector3 LimitPos(RectTransform ContentsRt, Vector3 ContentsPosition, int LimitDir)
	{
		RectTransform component = base.transform.GetComponent<RectTransform>();
		if (LimitDir == 0)
		{
			float num = 0f;
			float num2 = 0f;
			if (component.sizeDelta.y / ContentsRt.rect.height % 2f != 0f)
			{
				num = ContentsPosition.y + ContentsRt.rect.height / 2f - component.sizeDelta.y / 2f;
				num2 = ContentsPosition.y - ContentsRt.rect.height / 2f - (0f - component.sizeDelta.y) / 2f;
			}
			else if (lstNodes.First.Value != null)
			{
				RectTransform component2 = lstNodes.First.Value.GetComponent<RectTransform>();
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
		}
		else
		{
			float num3 = 0f;
			float num4 = 0f;
			if (component.sizeDelta.x / ContentsRt.rect.width % 2f != 0f)
			{
				num3 = ContentsPosition.x - ContentsRt.rect.width / 2f - (0f - component.sizeDelta.x) / 2f;
				num4 = ContentsPosition.x + ContentsRt.rect.width / 2f - component.sizeDelta.x / 2f;
			}
			else if (lstNodes.First.Value != null)
			{
				RectTransform component3 = lstNodes.First.Value.GetComponent<RectTransform>();
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
		}
		return ContentsPosition;
	}

	private void InitTargrt(RectTransform ContentsRt, Vector3 position)
	{
		float num = 0f;
		float num2 = 0f;
		RectTransform selectRect = SelectRect;
		if (selectRect.sizeDelta.x / ContentsRt.rect.width % 2f != 0f)
		{
			num = position.x - ContentsRt.rect.width / 2f - (0f - selectRect.sizeDelta.x) / 2f;
		}
		else if (lstNodes.First.Value != null)
		{
			RectTransform component = lstNodes.First.Value.GetComponent<RectTransform>();
			num = position.x - ContentsRt.rect.width / 2f - component.sizeDelta.x / 2f - (0f - selectRect.sizeDelta.x) / 2f;
		}
		if (selectRect.sizeDelta.y / ContentsRt.rect.height % 2f != 0f)
		{
			num2 = position.y + ContentsRt.rect.height / 2f - selectRect.sizeDelta.y / 2f;
		}
		else if (lstNodes.First.Value != null)
		{
			RectTransform component2 = lstNodes.First.Value.GetComponent<RectTransform>();
			num2 = position.y + ContentsRt.rect.height / 2f - component2.sizeDelta.y / 2f - selectRect.sizeDelta.y / 2f;
		}
		num = position.x - num;
		num2 = position.y - num2;
		ScrollCylinderNode i = null;
		HRotationScrollNode[] nodeList = NodeList;
		foreach (HRotationScrollNode hRotationScrollNode in nodeList)
		{
			if (hRotationScrollNode.gameObject.activeSelf)
			{
				i = hRotationScrollNode;
				break;
			}
		}
		targetNode = new ValueTuple<Vector2, ScrollCylinderNode>(new Vector2(num, num2), i);
	}

	public void ListClear()
	{
		lstNodes.Clear();
	}
}
