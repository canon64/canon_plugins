using System.Collections.Generic;
using ReMotion;
using UnityEngine;
using UnityEngine.UI;

public class HsceneSpriteTaiiCategory : HSceneSpriteCategory
{
	[SerializeField]
	private HSceneSprite hSprite;

	[SerializeField]
	private RotationScroll hSceneScroll;

	[SerializeField]
	private Button leftArrow;

	[SerializeField]
	private Image imgLeftArrow;

	[SerializeField]
	private Button rightArrow;

	[SerializeField]
	private Image imgRightArrow;

	[Space(2f)]
	[SerializeField]
	private float[] ArrowInitPos;

	[SerializeField]
	private float ArrowMoveVal;

	[Space(2f)]
	private Color[] arrowColor = new Color[3]
	{
		Color.white,
		Color.gray,
		Color.green
	};

	private float ArrowWaitingTime;

	private int ArrowWaitingMovePtn;

	[SerializeField]
	private float ArrowFirstHurfWaitingAnimTimeLimit;

	[SerializeField]
	private float ArrowLaterHurfWaitingAnimTimeLimit;

	private int[] ArrowChangeState = new int[2] { -1, -1 };

	private float[] ArrowAnimTime = new float[2];

	[SerializeField]
	private float ArrowBigAnimTimeLimit;

	[SerializeField]
	private float ArrowWaitAnimTimeLimit;

	[SerializeField]
	private float ArrowSmallAnimTimeLimit;

	private LinkedListNode<ScrollCylinderNode> target;

	private HRotationScrollNode targetNode;

	private LinkedList<ScrollCylinderNode> lstScrollNode = new LinkedList<ScrollCylinderNode>();

	private LinkedListNode<ScrollCylinderNode> tmp;

	private LinkedListNode<ScrollCylinderNode> btOld;

	public void Init()
	{
		lstScrollNode = hSceneScroll.GetList();
		if (lstScrollNode.Count <= 1)
		{
			leftArrow.interactable = false;
			imgLeftArrow.color = arrowColor[1];
			rightArrow.interactable = false;
			imgRightArrow.color = arrowColor[1];
		}
		if (!leftArrow.interactable)
		{
			leftArrow.interactable = true;
			imgLeftArrow.color = arrowColor[0];
		}
		if (!rightArrow.interactable)
		{
			rightArrow.interactable = true;
			imgRightArrow.color = arrowColor[0];
		}
		leftArrow.onClick.AddListener(delegate
		{
			btOld = new LinkedListNode<ScrollCylinderNode>(target.Value);
			LinkedListNode<ScrollCylinderNode> linkedListNode;
			if (lstScrollNode.Count >= 3)
			{
				linkedListNode = target.Previous;
			}
			else
			{
				RectTransform component = target.Value.GetComponent<RectTransform>();
				linkedListNode = target.Previous ?? lstScrollNode.Last;
				RectTransform component2 = linkedListNode.Value.GetComponent<RectTransform>();
				component2.anchoredPosition = component.anchoredPosition + new Vector2((hSceneScroll.scrollMode != ScrollDir.Vertical) ? ((0f - component.rect.width) * 2f) : 0f, (hSceneScroll.scrollMode != ScrollDir.Horizontal) ? (component.rect.height * 2f) : 0f);
				if (linkedListNode == lstScrollNode.Last)
				{
					LinkedListNode<ScrollCylinderNode> node = new LinkedListNode<ScrollCylinderNode>(lstScrollNode.Last.Value);
					lstScrollNode.RemoveLast();
					lstScrollNode.AddFirst(node);
					linkedListNode = lstScrollNode.First;
				}
			}
			if (btOld.Value == linkedListNode.Next?.Value)
			{
				ArrowChangeState[0] = 0;
				ArrowAnimTime[0] = 0f;
			}
			hSceneScroll.SetTarget(linkedListNode.Value);
		});
		rightArrow.onClick.AddListener(delegate
		{
			btOld = new LinkedListNode<ScrollCylinderNode>(target.Value);
			LinkedListNode<ScrollCylinderNode> linkedListNode;
			if (lstScrollNode.Count >= 3)
			{
				linkedListNode = target.Next;
			}
			else
			{
				RectTransform component = target.Value.GetComponent<RectTransform>();
				linkedListNode = target.Next ?? lstScrollNode.First;
				RectTransform component2 = linkedListNode.Value.GetComponent<RectTransform>();
				component2.anchoredPosition = component.anchoredPosition + new Vector2((hSceneScroll.scrollMode != ScrollDir.Vertical) ? (component.rect.width * 2f) : 0f, (hSceneScroll.scrollMode != ScrollDir.Horizontal) ? ((0f - component.rect.height) * 2f) : 0f);
				if (linkedListNode == lstScrollNode.First)
				{
					LinkedListNode<ScrollCylinderNode> node = new LinkedListNode<ScrollCylinderNode>(lstScrollNode.First.Value);
					lstScrollNode.RemoveFirst();
					lstScrollNode.AddLast(node);
					linkedListNode = lstScrollNode.Last;
				}
			}
			if (btOld.Value == linkedListNode.Previous?.Value)
			{
				ArrowChangeState[1] = 0;
				ArrowAnimTime[1] = 0f;
			}
			hSceneScroll.SetTarget(linkedListNode.Value);
		});
	}

	private void Update()
	{
		if (hSceneScroll.GetTarget().Item2 == null)
		{
			return;
		}
		tmp = new LinkedListNode<ScrollCylinderNode>((target == null) ? null : target.Value);
		target = lstScrollNode.Find(hSceneScroll.GetTarget().Item2);
		if (tmp.Value != null && target != null && target.Value != null)
		{
			targetNode = target.Value.GetComponent<HRotationScrollNode>();
			if (targetNode.id != tmp.Value.GetComponent<HRotationScrollNode>().id)
			{
				if (tmp.Value == target.Previous?.Value)
				{
					ArrowChangeState[1] = 0;
					ArrowAnimTime[1] = 0f;
				}
				else if (tmp.Value == target.Next?.Value)
				{
					ArrowChangeState[0] = 0;
					ArrowAnimTime[0] = 0f;
				}
				hSprite.OnClickMotion(targetNode.id);
			}
		}
		float deltaTime = Time.deltaTime;
		if (ArrowChangeState[0] != -1)
		{
			ButtonReaction(imgLeftArrow, ref ArrowChangeState[0], ref ArrowAnimTime[0], deltaTime);
		}
		if (ArrowChangeState[1] != -1)
		{
			ButtonReaction(imgRightArrow, ref ArrowChangeState[1], ref ArrowAnimTime[1], deltaTime);
		}
		ButtonWaitReaction(deltaTime);
	}

	public RotationScroll GetHScroll()
	{
		return hSceneScroll;
	}

	private void ButtonReaction(Image _image, ref int ArrowChangeState, ref float ArrowAnimTime, float deltaTime)
	{
		if (ArrowChangeState == 0)
		{
			ArrowAnimTime += deltaTime / ArrowBigAnimTimeLimit;
			float time = Mathf.InverseLerp(0f, 1f, ArrowAnimTime);
			time = EasingFunctions.EaseOutQuint(time, 1f);
			_image.color = new Color(Mathf.Lerp(arrowColor[0].r, arrowColor[2].r, time), Mathf.Lerp(arrowColor[0].g, arrowColor[2].g, time), Mathf.Lerp(arrowColor[0].b, arrowColor[2].b, time), Mathf.Lerp(arrowColor[0].a, arrowColor[2].a, time));
			_image.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 1.3f, time);
			if (time == 1f)
			{
				ArrowAnimTime = 0f;
				ArrowChangeState = 1;
			}
		}
		else if (ArrowChangeState == 1)
		{
			ArrowAnimTime += deltaTime / ArrowWaitAnimTimeLimit;
			float num = Mathf.InverseLerp(0f, 1f, ArrowAnimTime);
			if (num == 1f)
			{
				ArrowAnimTime = 1f;
				ArrowChangeState = 2;
			}
		}
		else if (ArrowChangeState == 2)
		{
			ArrowAnimTime -= deltaTime / ArrowSmallAnimTimeLimit;
			float time2 = Mathf.InverseLerp(0f, 1f, ArrowAnimTime);
			time2 = EasingFunctions.EaseOutQuint(time2, 1f);
			_image.color = new Color(Mathf.Lerp(arrowColor[0].r, arrowColor[2].r, time2), Mathf.Lerp(arrowColor[0].g, arrowColor[2].g, time2), Mathf.Lerp(arrowColor[0].b, arrowColor[2].b, time2), Mathf.Lerp(arrowColor[0].a, arrowColor[2].a, time2));
			_image.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 1.3f, time2);
			if (time2 == 0f)
			{
				ArrowAnimTime = 0f;
				ArrowChangeState = -1;
			}
		}
	}

	private void ButtonWaitReaction(float deltaTime)
	{
		float num = 0f;
		if (ArrowWaitingMovePtn == 0)
		{
			ArrowWaitingTime += deltaTime / ArrowFirstHurfWaitingAnimTimeLimit;
			num = Mathf.InverseLerp(0f, 1f, ArrowWaitingTime);
			if (num == 1f)
			{
				ArrowWaitingMovePtn = 1;
			}
		}
		else
		{
			ArrowWaitingTime -= deltaTime / ArrowLaterHurfWaitingAnimTimeLimit;
			num = Mathf.InverseLerp(0f, 1f, ArrowWaitingTime);
			if (num == 0f)
			{
				ArrowWaitingMovePtn = 0;
			}
		}
		Vector3 zero = Vector3.zero;
		if (ArrowChangeState[0] == -1 && leftArrow.interactable)
		{
			zero = imgLeftArrow.transform.localPosition;
			zero.x = Mathf.Lerp(ArrowInitPos[0], ArrowInitPos[0] - ArrowMoveVal, num);
			imgLeftArrow.transform.localPosition = zero;
		}
		else
		{
			zero = imgLeftArrow.transform.localPosition;
			zero.x = ArrowInitPos[0];
			imgLeftArrow.transform.localPosition = zero;
		}
		if (ArrowChangeState[1] == -1 && rightArrow.interactable)
		{
			zero = imgRightArrow.transform.localPosition;
			zero.x = Mathf.Lerp(ArrowInitPos[1], ArrowInitPos[1] + ArrowMoveVal, num);
			imgRightArrow.transform.localPosition = zero;
		}
		else
		{
			zero = imgRightArrow.transform.localPosition;
			zero.x = ArrowInitPos[1];
			imgRightArrow.transform.localPosition = zero;
		}
	}

	public override void SetActive(bool _active, int _array = -1)
	{
		if (_array < 0)
		{
			for (int i = 0; i < lstButton.Count; i++)
			{
				if (lstButton[i].gameObject.activeSelf != _active)
				{
					lstButton[i].gameObject.SetActive(_active);
				}
			}
		}
		else if (lstButton.Count > _array && lstButton[_array].gameObject.activeSelf != _active)
		{
			lstButton[_array].gameObject.SetActive(_active);
		}
	}

	public void EndProc()
	{
		leftArrow.onClick.RemoveAllListeners();
		rightArrow.onClick.RemoveAllListeners();
	}
}
