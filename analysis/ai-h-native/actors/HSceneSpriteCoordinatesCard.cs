using System;
using System.Collections.Generic;
using AIChara;
using AIProject;
using AIProject.ColorDefine;
using CharaCustom;
using Manager;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using UnityEx;

public class HSceneSpriteCoordinatesCard : MonoBehaviour
{
	private class ListComparer : IComparer<HSceneSpriteCoordinatesNode>
	{
		public int nCompare;

		public bool bAscending;

		public int Compare(HSceneSpriteCoordinatesNode a, HSceneSpriteCoordinatesNode b)
		{
			switch (nCompare)
			{
			case 0:
				if (bAscending)
				{
					return SortCompare(a.coodeName.text, b.coodeName.text);
				}
				return SortCompare(b.coodeName.text, a.coodeName.text);
			case 1:
				if (bAscending)
				{
					return SortCompare(a.CreateCoodeTime, b.CreateCoodeTime);
				}
				return SortCompare(b.CreateCoodeTime, a.CreateCoodeTime);
			default:
				return 0;
			}
		}

		private int SortCompare<T>(T a, T b) where T : IComparable
		{
			return a.CompareTo(b);
		}
	}

	[SerializeField]
	private HSceneSpriteChaChoice hSceneSpriteChaChoice;

	[SerializeField]
	private HSceneSpriteClothCondition hSceneSpriteCloth;

	[SerializeField]
	private RawImage CardImage;

	[SerializeField]
	private Text SelectedLabel;

	private string filename;

	[SerializeField]
	private Button Sort;

	[SerializeField]
	private Button[] SortUpDown;

	[SerializeField]
	private GameObject SortPanel;

	[SerializeField]
	private HSceneSpriteCoordinatesNode CoordinatesNode;

	[SerializeField]
	private Transform Content;

	[SerializeField]
	private Button cross;

	[SerializeField]
	private List<Toggle> lstSortCategory = new List<Toggle>();

	private List<HSceneSpriteCoordinatesNode> lstCoordinates = new List<HSceneSpriteCoordinatesNode>();

	private List<CustomClothesFileInfo> lstCoordinatesBase = new List<CustomClothesFileInfo>();

	[SerializeField]
	private Button BeforeCoode;

	[SerializeField]
	private Button DecideCoode;

	private int sortKind;

	private bool Ascending;

	private HScene hScene;

	private HSceneManager hSceneManager;

	private ChaControl[] femailes;

	private IntReactiveProperty SelectedID = new IntReactiveProperty(-1);

	private int _SelectedID
	{
		get
		{
			return SelectedID.Value;
		}
		set
		{
			SelectedID.Value = value;
		}
	}

	private void Start()
	{
		SelectedID.Where((int x) => x >= 0 && x < lstCoordinates.Count).Subscribe(delegate(int x)
		{
			for (int i = 0; i < lstCoordinates.Count; i++)
			{
				if (lstCoordinates[i].id == x)
				{
					SelectedLabel.text = lstCoordinates[i].coodeName.text;
					filename = lstCoordinates[i].fileName;
					CardImage.texture = PngAssist.ChangeTextureFromByte((lstCoordinatesBase[x].pngData == null) ? PngFile.LoadPngBytes(lstCoordinatesBase[x].FullPath) : lstCoordinatesBase[x].pngData);
				}
			}
			if (!CardImage.gameObject.activeSelf)
			{
				CardImage.gameObject.SetActive(value: true);
			}
			for (int j = 0; j < lstCoordinates.Count; j++)
			{
				if (lstCoordinates[j].id != x)
				{
					lstCoordinates[j].image.color = Color.white;
				}
				else
				{
					lstCoordinates[j].image.color = Define.Get(Colors.Yellow);
				}
			}
		});
	}

	public void Init()
	{
		hScene = Singleton<HSceneFlagCtrl>.Instance.GetComponent<HScene>();
		femailes = hScene.GetFemales();
		hSceneManager = Singleton<HSceneManager>.Instance;
		Sort.onClick.AddListener(delegate
		{
			SortPanel.SetActive(SortPanel.activeSelf ^ true);
		});
		SortUpDown[0].onClick.AddListener(delegate
		{
			SortUpDown[0].gameObject.SetActive(value: false);
			SortUpDown[1].gameObject.SetActive(value: true);
			ListSortUpDown(0);
		});
		SortUpDown[1].onClick.AddListener(delegate
		{
			SortUpDown[0].gameObject.SetActive(value: true);
			SortUpDown[1].gameObject.SetActive(value: false);
			ListSortUpDown(1);
		});
		cross.onClick.AddListener(delegate
		{
			SortPanel.SetActive(value: false);
		});
		lstCoordinatesBase = CustomClothesFileInfoAssist.CreateClothesFileInfoList(useMale: false, useFemale: true);
		lstCoordinates.Clear();
		for (int i = 0; i < lstCoordinatesBase.Count; i++)
		{
			int no = i;
			HSceneSpriteCoordinatesNode hSceneSpriteCoordinatesNode = UnityEngine.Object.Instantiate(CoordinatesNode, Content);
			hSceneSpriteCoordinatesNode.gameObject.SetActive(value: true);
			lstCoordinates.Add(hSceneSpriteCoordinatesNode);
			lstCoordinates[no].id = no;
			lstCoordinates[no].coodeName.text = lstCoordinatesBase[no].name;
			lstCoordinates[no].CreateCoodeTime = lstCoordinatesBase[no].time;
			lstCoordinates[no].GetComponent<Button>().onClick.AddListener(delegate
			{
				_SelectedID = no;
			});
			lstCoordinates[no].image = lstCoordinates[no].GetComponent<Image>();
			lstCoordinates[no].fileName = lstCoordinatesBase[no].FileName;
		}
		PointerEnterTrigger pointerEnterTrigger = null;
		UITrigger.TriggerEvent triggerEvent = new UITrigger.TriggerEvent();
		PointerExitTrigger pointerExitTrigger = null;
		UITrigger.TriggerEvent triggerEvent2 = new UITrigger.TriggerEvent();
		for (int j = 0; j < lstSortCategory.Count; j++)
		{
			int no2 = j;
			lstSortCategory[no2].onValueChanged.RemoveAllListeners();
			lstSortCategory[no2].onValueChanged.AddListener(delegate(bool On)
			{
				if (On)
				{
					ListSort(no2);
				}
			});
			pointerEnterTrigger = null;
			triggerEvent = new UITrigger.TriggerEvent();
			pointerEnterTrigger = lstSortCategory[no2].gameObject.GetComponent<PointerEnterTrigger>();
			if (pointerEnterTrigger == null)
			{
				pointerEnterTrigger = lstSortCategory[no2].gameObject.AddComponent<PointerEnterTrigger>();
			}
			if (pointerEnterTrigger.Triggers.Count > 0)
			{
				pointerEnterTrigger.Triggers.Clear();
			}
			pointerEnterTrigger.Triggers.Add(triggerEvent);
			triggerEvent.AddListener(delegate
			{
				if (Cursor.visible)
				{
					lstSortCategory[no2].targetGraphic.enabled = true;
				}
			});
			pointerExitTrigger = null;
			triggerEvent2 = new UITrigger.TriggerEvent();
			pointerExitTrigger = lstSortCategory[no2].gameObject.GetComponent<PointerExitTrigger>();
			if (pointerExitTrigger == null)
			{
				pointerExitTrigger = lstSortCategory[no2].gameObject.AddComponent<PointerExitTrigger>();
			}
			if (pointerExitTrigger.Triggers.Count > 0)
			{
				pointerExitTrigger.Triggers.Clear();
			}
			pointerExitTrigger.Triggers.Add(triggerEvent2);
			triggerEvent2.AddListener(delegate
			{
				lstSortCategory[no2].targetGraphic.enabled = false;
			});
		}
		ListSort(0);
		BeforeCoode.onClick.AddListener(delegate
		{
			Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_L);
			femailes[hSceneManager.numFemaleClothCustom].ChangeNowCoordinate(reload: true);
			hSceneSpriteCloth.SetClothCharacter();
		});
		DecideCoode.onClick.AddListener(delegate
		{
			Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_L);
			if (SelectedID.Value == -1 || filename.IsNullOrEmpty())
			{
				femailes[hSceneManager.numFemaleClothCustom].ChangeNowCoordinate(reload: true);
			}
			else
			{
				femailes[hSceneManager.numFemaleClothCustom].ChangeNowCoordinate(filename, reload: true);
			}
			hSceneSpriteCloth.SetClothCharacter();
		});
		hSceneSpriteChaChoice.SetAction(delegate
		{
			SetCoordinatesCharacter();
		});
	}

	private void SetCoordinatesCharacter()
	{
		_SelectedID = -1;
	}

	public void ListSort(int sortkind)
	{
		ListComparer listComparer = new ListComparer();
		sortKind = sortkind;
		listComparer.nCompare = sortkind;
		listComparer.bAscending = Ascending;
		if (lstCoordinates != null && lstCoordinates.Count != 0)
		{
			lstCoordinates.Sort(listComparer);
			for (int i = 0; i < lstCoordinates.Count; i++)
			{
				int siblingIndex = i;
				lstCoordinates[i].transform.SetSiblingIndex(siblingIndex);
			}
		}
	}

	public void ListSortUpDown(int Ascending)
	{
		ListComparer listComparer = new ListComparer();
		this.Ascending = Ascending == 0;
		listComparer.nCompare = sortKind;
		listComparer.bAscending = this.Ascending;
		if (lstCoordinates != null && lstCoordinates.Count != 0)
		{
			lstCoordinates.Sort(listComparer);
			for (int i = 0; i < lstCoordinates.Count; i++)
			{
				int siblingIndex = i;
				lstCoordinates[i].transform.SetSiblingIndex(siblingIndex);
			}
		}
	}

	public void EndProc()
	{
		for (int i = 0; i < lstCoordinates.Count; i++)
		{
			UnityEngine.Object.Destroy(lstCoordinates[i].gameObject);
		}
		lstCoordinates.Clear();
		Sort.onClick.RemoveAllListeners();
		SortUpDown[0].onClick.RemoveAllListeners();
		SortUpDown[1].onClick.RemoveAllListeners();
		cross.onClick.RemoveAllListeners();
		BeforeCoode.onClick.RemoveAllListeners();
		DecideCoode.onClick.RemoveAllListeners();
		CloseSort();
	}

	private void OnDisable()
	{
		CloseSort();
	}

	public void CloseSort()
	{
		SortPanel.SetActive(value: false);
	}
}
