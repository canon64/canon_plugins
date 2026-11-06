using System.Collections;
using System.Collections.Generic;
using AIProject;
using AIProject.SaveData;
using Manager;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using UnityEx;

public class HSceneSpriteHitem : MonoBehaviour
{
	public struct HitemInfo
	{
		public int id;

		public int iconId;

		public string ItemName;

		public string ExplanatoryText;
	}

	public ScrollCylinder hSceneScroll;

	public HItemNode Node;

	public GameObject lstHItemTop;

	public Text ExplanatoryText;

	public GameObject ConfirmPanel;

	public Button Yes;

	public Button No;

	private Dictionary<int, ValueTuple<int, HitemInfo>> lstHItem = new Dictionary<int, ValueTuple<int, HitemInfo>>();

	private Dictionary<int, bool> bHItemEffect = new Dictionary<int, bool>();

	private List<HitemInfo> hitemInfos = new List<HitemInfo>();

	private HSceneNodePool hSceneNodePool = new HSceneNodePool();

	private List<GameObject> pool = new List<GameObject>();

	private List<ScrollCylinderNode> nodes;

	private HItemNode tmpNode;

	private Toggle toggle;

	private bool canUse = true;

	private int ItemId;

	private Image[] tmpImgs;

	private PointerClickTrigger PointerClickTrigger;

	private UITrigger.TriggerEvent onClick;

	private Image[] images;

	[SerializeField]
	[Tooltip("薬のCategoryIDを指定")]
	private int ItemCategory = 8;

	private const string IconObjName = "ItemIcon";

	private const string OnCursorName = "OnCursor";

	public IEnumerator Init()
	{
		lstHItem = new Dictionary<int, ValueTuple<int, HitemInfo>>();
		bHItemEffect = new Dictionary<int, bool>();
		nodes = new List<ScrollCylinderNode>();
		hitemInfos = new List<HitemInfo>();
		canUse = true;
		CheckHadItem(3, ref hitemInfos);
		CheckHadItem(5, ref hitemInfos);
		CheckHadItem(6, ref hitemInfos);
		CheckHadItem(7, ref hitemInfos);
		hSceneScroll.BlankSet(Node);
		hSceneNodePool.CreatePool(Node.gameObject, lstHItemTop.transform, 5);
		pool = hSceneNodePool.GetList();
		yield return null;
		Yes.onClick.AddListener(delegate
		{
			SetUse(hitemInfos[ItemId].id, use: true);
			RemoveItem(hitemInfos[ItemId].id);
			ConfirmPanel.SetActive(value: false);
			Toggle component2 = pool[ItemId].GetComponent<Toggle>();
			component2.isOn = false;
			hSceneScroll.enabled = true;
			Singleton<HSceneFlagCtrl>.Instance.AddParam(10, 1);
		});
		No.onClick.AddListener(delegate
		{
			ConfirmPanel.SetActive(value: false);
			Toggle component = pool[ItemId].GetComponent<Toggle>();
			component.isOn = false;
			hSceneScroll.enabled = true;
		});
		for (int i = 0; i < hitemInfos.Count; i++)
		{
			int no = i;
			tmpNode = pool[no].GetComponent<HItemNode>();
			tmpNode.text.text = hitemInfos[no].ItemName;
			int num = CheckHadItemNum(hitemInfos[no].id);
			if (num > 999)
			{
				tmpNode.NumTxt.text = "999+";
			}
			else
			{
				tmpNode.NumTxt.text = $"{num}";
			}
			tmpNode.ScaleSet.SetActive(value: true);
			toggle = tmpNode.Toggle;
			onClick = new UITrigger.TriggerEvent();
			if (toggle != null)
			{
				images = toggle.GetComponentsInChildren<Image>();
				PointerClickTrigger = toggle.GetComponent<PointerClickTrigger>();
				if (PointerClickTrigger == null)
				{
					PointerClickTrigger = toggle.gameObject.AddComponent<PointerClickTrigger>();
				}
				PointerClickTrigger.Triggers.Clear();
				PointerClickTrigger.Triggers.Add(onClick);
				onClick.AddListener(delegate
				{
					if (!ConfirmPanel.activeSelf)
					{
						if (no != hSceneScroll.GetTarget().Item1)
						{
							hSceneScroll.SetTarget(pool[no].GetComponent<HItemNode>());
						}
						else
						{
							ConfirmPanel.SetActive(value: true);
							hSceneScroll.enabled = false;
							ItemId = no;
							pool[no].GetComponent<Toggle>().isOn = true;
						}
					}
				});
				if (toggle.interactable)
				{
					toggle.interactable = false;
				}
				for (int j = 0; j < images.Length; j++)
				{
					if (images[j].transform.name == "ItemIcon")
					{
						Manager.Resources.ItemIconTables.SetIcon(Manager.Resources.ItemIconTables.IconCategory.Item, hitemInfos[no].iconId, images[j], imageName: false);
						if (images[j].sprite != null)
						{
							images[j].enabled = true;
						}
					}
					if (!images[j].raycastTarget)
					{
						images[j].raycastTarget = true;
					}
				}
			}
			nodes.Add(tmpNode);
			lstHItem.Add(hitemInfos[no].id, new ValueTuple<int, HitemInfo>(no, hitemInfos[no]));
			bHItemEffect.Add(hitemInfos[no].id, value: false);
		}
		yield return null;
		if (lstHItem.Count == 0)
		{
			canUse = false;
			if (pool.Count == 0)
			{
				hSceneNodePool.Get();
			}
			lstHItem.Add(-1, new ValueTuple<int, HitemInfo>(-1, default(HitemInfo)));
			bHItemEffect.Add(-1, value: false);
			toggle = pool[0].GetComponent<Toggle>();
			PointerClickTrigger = pool[0].GetComponent<PointerClickTrigger>();
			PointerClickTrigger.Triggers.Clear();
			if (toggle != null)
			{
				if (toggle.interactable)
				{
					toggle.interactable = false;
				}
				images = toggle.GetComponentsInChildren<Image>();
				for (int k = 0; k < images.Length; k++)
				{
					if (images[k].raycastTarget)
					{
						images[k].raycastTarget = false;
					}
				}
			}
			nodes.Add(pool[0].GetComponent<ScrollCylinderNode>());
		}
		yield return null;
	}

	private void Update()
	{
		if (lstHItem.Count == 0 || !canUse)
		{
			return;
		}
		int item = hSceneScroll.GetTarget().Item1;
		int key = ConvertScrollIDToItemID(item);
		if (!lstHItem.ContainsKey(key))
		{
			return;
		}
		ExplanatoryText.text = lstHItem[key].Item2.ExplanatoryText;
		for (int i = 0; i < nodes.Count; i++)
		{
			tmpImgs = nodes[i].GetComponentsInChildren<Image>();
			for (int j = 0; j < tmpImgs.Length; j++)
			{
				if (!(tmpImgs[j].name != "OnCursor"))
				{
					if (i == item && !nodes[i].GetComponent<Toggle>().isOn)
					{
						tmpImgs[j].enabled = true;
					}
					else
					{
						tmpImgs[j].enabled = false;
					}
				}
			}
		}
	}

	public void SetVisible(bool visible)
	{
		for (int i = 0; i < pool.Count; i++)
		{
			if (i < lstHItem.Count)
			{
				pool[i].SetActive(visible);
			}
		}
		if (visible)
		{
			hSceneScroll.ListNodeSet(nodes);
		}
		if (canUse)
		{
			int key = ConvertScrollIDToItemID(hSceneScroll.GetTarget().Item1);
			ExplanatoryText.text = lstHItem[key].Item2.ExplanatoryText;
		}
		else
		{
			ExplanatoryText.text = "Hアイテムを所持していません";
		}
	}

	public void SetUse(int no, bool use)
	{
		if (pool.Count != 0)
		{
			bHItemEffect[no] = use;
		}
	}

	public bool Effect(int id)
	{
		return bHItemEffect.ContainsKey(id) && bHItemEffect[id];
	}

	public void ItemRemove()
	{
		canUse = true;
		lstHItem.Clear();
		hitemInfos.Clear();
		nodes.Clear();
		List<GameObject> list = hSceneNodePool.GetList();
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i].gameObject.activeSelf)
			{
				list[i].GetComponent<HItemNode>().ScaleSet.SetActive(value: false);
				list[i].gameObject.SetActive(value: false);
			}
		}
		CheckHadItem(3, ref hitemInfos);
		CheckHadItem(5, ref hitemInfos);
		CheckHadItem(6, ref hitemInfos);
		CheckHadItem(7, ref hitemInfos);
		for (int j = 0; j < hitemInfos.Count; j++)
		{
			int no = j;
			hSceneNodePool.Get();
			tmpNode = pool[no].GetComponent<HItemNode>();
			tmpNode.text.text = hitemInfos[no].ItemName;
			int num = CheckHadItemNum(hitemInfos[no].id);
			if (num > 999)
			{
				tmpNode.NumTxt.text = "999+";
			}
			else
			{
				tmpNode.NumTxt.text = $"{num}";
			}
			tmpNode.ScaleSet.SetActive(value: true);
			toggle = tmpNode.Toggle;
			onClick = new UITrigger.TriggerEvent();
			if (toggle != null)
			{
				images = toggle.GetComponentsInChildren<Image>();
				PointerClickTrigger = toggle.GetComponent<PointerClickTrigger>();
				if (PointerClickTrigger == null)
				{
					PointerClickTrigger = toggle.gameObject.AddComponent<PointerClickTrigger>();
				}
				PointerClickTrigger.Triggers.Clear();
				PointerClickTrigger.Triggers.Add(onClick);
				onClick.AddListener(delegate
				{
					if (!ConfirmPanel.activeSelf)
					{
						if (no != hSceneScroll.GetTarget().Item1)
						{
							hSceneScroll.SetTarget(pool[no].GetComponent<HItemNode>());
						}
						else
						{
							ConfirmPanel.SetActive(value: true);
							ItemId = no;
							pool[no].GetComponent<Toggle>().isOn = true;
						}
					}
				});
				if (toggle.interactable)
				{
					toggle.interactable = false;
				}
				for (int k = 0; k < images.Length; k++)
				{
					if (images[k].transform.name == "ItemIcon")
					{
						Manager.Resources.ItemIconTables.SetIcon(Manager.Resources.ItemIconTables.IconCategory.Item, hitemInfos[no].iconId, images[k], imageName: false);
						if (images[k].sprite != null)
						{
							images[k].enabled = true;
						}
					}
					if (!images[k].raycastTarget)
					{
						images[k].raycastTarget = true;
					}
				}
			}
			nodes.Add(tmpNode);
			lstHItem.Add(hitemInfos[no].id, new ValueTuple<int, HitemInfo>(no, hitemInfos[no]));
			pool[no].SetActive(value: true);
		}
		if (lstHItem.Count == 0)
		{
			canUse = false;
			hSceneNodePool.Get();
			lstHItem.Add(-1, new ValueTuple<int, HitemInfo>(-1, default(HitemInfo)));
			toggle = pool[0].GetComponent<Toggle>();
			PointerClickTrigger = pool[0].GetComponent<PointerClickTrigger>();
			PointerClickTrigger.Triggers.Clear();
			if (toggle != null)
			{
				if (toggle.interactable)
				{
					toggle.interactable = false;
				}
				images = toggle.GetComponentsInChildren<Image>();
				for (int l = 0; l < images.Length; l++)
				{
					if (images[l].transform.name == "ItemIcon")
					{
						images[l].enabled = false;
					}
					if (images[l].raycastTarget)
					{
						images[l].raycastTarget = false;
					}
				}
			}
			tmpNode = pool[0].GetComponent<HItemNode>();
			tmpNode.text.text = string.Empty;
			nodes.Add(tmpNode);
		}
		Observable.NextFrame().Subscribe(delegate
		{
			hSceneScroll.ListNodeSet(nodes);
			if (canUse)
			{
				int key = ConvertScrollIDToItemID(hSceneScroll.GetTarget().Item1);
				if (lstHItem.ContainsKey(key))
				{
					ExplanatoryText.text = lstHItem[key].Item2.ExplanatoryText;
				}
			}
			else
			{
				ExplanatoryText.text = "Hアイテムを所持していません";
			}
		});
	}

	protected void RemoveItem(int _ID)
	{
		int num = -1;
		List<StuffItem> itemList = Singleton<Map>.Instance.Player.PlayerData.ItemList;
		for (int i = 0; i < itemList.Count; i++)
		{
			if (itemList[i].CategoryID == ItemCategory && itemList[i].ID == _ID)
			{
				num = i;
				break;
			}
		}
		if (num != -1)
		{
			StuffItem stuffItem = itemList[num];
			StuffItem item = new StuffItem(stuffItem.CategoryID, stuffItem.ID, 1);
			Singleton<Map>.Instance.Player.PlayerData.ItemList.RemoveItem(item);
		}
		else
		{
			itemList = Singleton<Game>.Instance.Environment.ItemListInStorage;
			for (int j = 0; j < itemList.Count; j++)
			{
				if (itemList[j].CategoryID == ItemCategory && itemList[j].ID == _ID)
				{
					num = j;
					break;
				}
			}
			StuffItem stuffItem2 = itemList[num];
			StuffItem item2 = new StuffItem(stuffItem2.CategoryID, stuffItem2.ID, 1);
			itemList.RemoveItem(item2);
		}
		Observable.NextFrame().Subscribe(delegate
		{
			ItemRemove();
		});
	}

	private bool CheckHadItem(int id, ref List<HitemInfo> hitemInfos)
	{
		bool flag = false;
		flag = CheckHadItem(id, 0, ref hitemInfos);
		if (!flag)
		{
			flag = CheckHadItem(id, 1, ref hitemInfos);
		}
		return flag;
	}

	private bool CheckHadItem(int id, int mode, ref List<HitemInfo> hitemInfos)
	{
		List<StuffItem> list = ((mode != 0) ? Singleton<Game>.Instance.Environment.ItemListInStorage : Singleton<Map>.Instance.Player.PlayerData.ItemList);
		HitemInfo item2 = default(HitemInfo);
		foreach (StuffItem item3 in list)
		{
			if (item3.CategoryID == ItemCategory && item3.ID == id)
			{
				StuffItemInfo item = Singleton<Manager.Resources>.Instance.GameInfo.GetItem(item3.CategoryID, id);
				if (item != null)
				{
					item2.id = item.ID;
					item2.iconId = item.IconID;
					item2.ItemName = item.Name;
					item2.ExplanatoryText = item.Explanation;
					hitemInfos.Add(item2);
					return true;
				}
			}
		}
		return false;
	}

	private int CheckHadItemNum(int id)
	{
		int num = 0;
		List<StuffItem> itemList = Singleton<Map>.Instance.Player.PlayerData.ItemList;
		foreach (StuffItem item in itemList)
		{
			if (item.CategoryID != ItemCategory || item.ID != id)
			{
				continue;
			}
			num = item.Count;
			break;
		}
		itemList = Singleton<Game>.Instance.Environment.ItemListInStorage;
		foreach (StuffItem item2 in itemList)
		{
			if (item2.CategoryID != ItemCategory || item2.ID != id)
			{
				continue;
			}
			num += item2.Count;
			break;
		}
		return num;
	}

	private int ConvertScrollIDToItemID(int ScrollId)
	{
		foreach (KeyValuePair<int, ValueTuple<int, HitemInfo>> item in lstHItem)
		{
			if (item.Value.Item1 != ScrollId)
			{
				continue;
			}
			return item.Key;
		}
		return -1;
	}

	public void EndProc()
	{
		hSceneScroll.ClearBlank();
		List<GameObject> list = hSceneNodePool.GetList();
		for (int i = 0; i < list.Count; i++)
		{
			int index = i;
			Object.Destroy(list[index].gameObject);
		}
		list.Clear();
		Yes.onClick.RemoveAllListeners();
		No.onClick.RemoveAllListeners();
		if (ConfirmPanel.activeSelf)
		{
			ConfirmPanel.SetActive(value: false);
		}
	}
}
