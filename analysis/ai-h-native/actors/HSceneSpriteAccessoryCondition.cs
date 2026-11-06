using System;
using System.Text;
using AIChara;
using AIProject;
using AIProject.ColorDefine;
using AIProject.SaveData;
using Manager;
using UnityEngine;
using UnityEngine.UI;

public class HSceneSpriteAccessoryCondition : MonoBehaviour
{
	[Serializable]
	public class EquipBt
	{
		public GameObject Base;

		public Button On;

		public Button Off;

		public ChaControl chara;

		public int id = -1;

		public bool active;

		public void ChangeState()
		{
			if (chara == null)
			{
				return;
			}
			HSceneManager instance = Singleton<HSceneManager>.Instance;
			if (id != 3)
			{
				if (active)
				{
					chara.ShowExtraAccessory((ChaControlDefine.ExtraAccessoryParts)id, show: false);
				}
				else
				{
					chara.ShowExtraAccessory((ChaControlDefine.ExtraAccessoryParts)id, show: true);
				}
			}
			else if (active)
			{
				chara.ShowExtraAccessory((ChaControlDefine.ExtraAccessoryParts)id, show: false);
				if (instance.Player != null && instance.Player.ChaControl == chara && instance.Player.EquipedItem != null && instance.Player.EquipedItem.AsGameObject != null)
				{
					instance.Player.EquipedItem.AsGameObject.SetActive(value: false);
				}
			}
			else
			{
				chara.ShowExtraAccessory((ChaControlDefine.ExtraAccessoryParts)id, show: true);
				if (instance.Player != null && instance.Player.ChaControl == chara && instance.Player.EquipedItem != null && instance.Player.EquipedItem.AsGameObject != null)
				{
					instance.Player.EquipedItem.AsGameObject.SetActive(value: true);
				}
			}
			On.gameObject.SetActive(!active);
			Off.gameObject.SetActive(active);
			active ^= true;
		}
	}

	public HSceneSpriteChaChoice hSceneSpriteChaChoice;

	public HSceneSpriteToggleCategory AccessorySlots;

	public Toggle AllChange;

	public EquipBt[] EquipBts = new EquipBt[4];

	private HScene hScene;

	private ChaControl[] females;

	private ChaControl[] Males;

	private bool[] allState = new bool[4] { true, true, true, true };

	[SerializeField]
	private HSceneSprite hSceneSprite;

	private HSceneManager hSceneManager;

	private StringBuilder sbAcsName;

	private bool[,] before = new bool[4, 4];

	private bool player;

	public void Init()
	{
		hScene = Singleton<HSceneFlagCtrl>.Instance.GetComponent<HScene>();
		hSceneManager = Singleton<HSceneManager>.Instance;
		females = hScene.GetFemales();
		Males = hScene.GetMales();
		for (int i = 0; i < 4; i++)
		{
			ChaControl chaControl = ((i >= 2) ? Males[i - 2] : females[i]);
			if (chaControl == null)
			{
				continue;
			}
			for (int j = 0; j < 4; j++)
			{
				if (chaControl.objExtraAccessory[j] == null)
				{
					before[i, j] = false;
				}
				else if (j != 3)
				{
					before[i, j] = chaControl.objExtraAccessory[j].activeSelf;
				}
				else if (i < 2)
				{
					if (i == 1 && hSceneManager.Player != null && hSceneManager.Player.ChaControl == chaControl)
					{
						if (hSceneManager.Player.EquipedItem != null && hSceneManager.Player.EquipedItem.AsGameObject != null)
						{
							before[i, j] = hSceneManager.Player.EquipedItem.AsGameObject.activeSelf;
						}
						else
						{
							before[i, j] = false;
						}
					}
					else
					{
						before[i, j] = hSceneManager.FemaleLumpActive[i];
					}
				}
				else if (hSceneManager.Player != null && hSceneManager.Player.ChaControl == chaControl)
				{
					if (hSceneManager.Player.EquipedItem != null && hSceneManager.Player.EquipedItem.AsGameObject != null)
					{
						before[i, j] = hSceneManager.Player.EquipedItem.AsGameObject.activeSelf;
					}
					else
					{
						before[i, j] = false;
					}
				}
				else
				{
					before[i, j] = chaControl.objExtraAccessory[j].activeSelf;
				}
			}
		}
		sbAcsName = new StringBuilder();
		SetAccessoryCharacter(init: true);
		hSceneSpriteChaChoice.SetAction(delegate
		{
			SetAccessoryCharacter();
		});
	}

	public void SetAccessoryCharacter(bool init = false)
	{
		if (!init && !base.gameObject.activeSelf)
		{
			return;
		}
		Text text = null;
		Color color = default(Color);
		for (int i = 0; i < AccessorySlots.GetToggleNum(); i++)
		{
			sbAcsName.Clear();
			int num = i;
			ListInfoBase listInfoBase = ((hSceneManager.numFemaleClothCustom >= 2) ? Males[hSceneManager.numFemaleClothCustom - 2].infoAccessory[num] : females[hSceneManager.numFemaleClothCustom].infoAccessory[num]);
			text = AccessorySlots.lstToggle[num].GetComponentInChildren<Text>();
			if (listInfoBase != null)
			{
				sbAcsName.Append(listInfoBase.Name);
				AccessorySlots.lstToggle[num].GetComponent<Image>().raycastTarget = true;
				if (hSceneManager.numFemaleClothCustom < 2)
				{
					AccessorySlots.lstToggle[num].isOn = females[hSceneManager.numFemaleClothCustom].fileStatus.showAccessory[num];
				}
				else
				{
					AccessorySlots.lstToggle[num].isOn = Males[hSceneManager.numFemaleClothCustom - 2].fileStatus.showAccessory[num];
				}
				if (AccessorySlots.lstToggle[num].isOn)
				{
					Define.Set(ref color, Colors.White);
					text.color = color;
				}
				else
				{
					text.color = new Color32(141, 136, 129, byte.MaxValue);
				}
			}
			else
			{
				sbAcsName.AppendFormat("スロット{0}", num);
				AccessorySlots.lstToggle[num].GetComponent<Image>().raycastTarget = false;
				text.color = new Color32(141, 136, 129, byte.MaxValue);
			}
			text.text = sbAcsName.ToString();
		}
		if (hSceneManager.numFemaleClothCustom < 2)
		{
			AccessorySlots.SetActive(females[hSceneManager.numFemaleClothCustom].objBodyBone != null && females[hSceneManager.numFemaleClothCustom].visibleAll);
		}
		else
		{
			AccessorySlots.SetActive(Males[hSceneManager.numFemaleClothCustom - 2].objBodyBone != null && Males[hSceneManager.numFemaleClothCustom - 2].visibleAll);
		}
		if (hSceneManager.numFemaleClothCustom > 1 || (hSceneManager.Player != null && hSceneManager.Player.ChaControl == females[hSceneManager.numFemaleClothCustom]))
		{
			if (!Config.HData.Accessory)
			{
				AllChange.isOn = false;
			}
			else
			{
				AllChange.isOn = true;
			}
		}
		else
		{
			AllChange.isOn = true;
		}
		allState[hSceneManager.numFemaleClothCustom] = AllChange.isOn;
		text = AllChange.GetComponentInChildren<Text>();
		Color color2 = text.color;
		if (allState[hSceneManager.numFemaleClothCustom])
		{
			Define.Set(ref color2, Colors.White);
		}
		else
		{
			color2 = new Color32(141, 136, 129, byte.MaxValue);
		}
		text.color = color2;
		AgentActor agentActor = null;
		if (hSceneManager.numFemaleClothCustom < 2)
		{
			if (hSceneManager.females[hSceneManager.numFemaleClothCustom] != null)
			{
				agentActor = hSceneManager.females[hSceneManager.numFemaleClothCustom].GetComponent<AgentActor>();
			}
		}
		else if (hSceneManager.male != null)
		{
			agentActor = hSceneManager.male.GetComponent<AgentActor>();
		}
		AgentData agentData = ((!(agentActor != null)) ? null : agentActor.AgentData);
		bool active = false;
		bool active2 = false;
		bool active3 = false;
		bool active4 = false;
		if (agentData != null)
		{
			player = false;
			active = agentData.EquipedHeadItem != null && agentData.EquipedHeadItem.ID != -1;
			active2 = agentData.EquipedBackItem != null && agentData.EquipedBackItem.ID != -1;
			active3 = agentData.EquipedNeckItem != null && agentData.EquipedNeckItem.ID != -1;
			active4 = agentData.EquipedLampItem != null && agentData.EquipedLampItem.ID != -1;
		}
		else
		{
			PlayerActor playerActor = null;
			if (hSceneManager.numFemaleClothCustom < 2)
			{
				if (hSceneManager.females[hSceneManager.numFemaleClothCustom] != null)
				{
					playerActor = hSceneManager.females[hSceneManager.numFemaleClothCustom].GetComponent<PlayerActor>();
				}
			}
			else if (hSceneManager.male != null)
			{
				playerActor = hSceneManager.male.GetComponent<PlayerActor>();
			}
			PlayerData playerData = ((!(playerActor != null)) ? null : playerActor.PlayerData);
			if (playerData != null)
			{
				player = true;
				active = playerData.EquipedHeadItem != null && playerData.EquipedHeadItem.ID != -1;
				active2 = playerData.EquipedBackItem != null && playerData.EquipedBackItem.ID != -1;
				active3 = playerData.EquipedNeckItem != null && playerData.EquipedNeckItem.ID != -1;
				active4 = playerData.EquipedLampItem != null && playerData.EquipedLampItem.ID != -1;
			}
		}
		if (hSceneManager.numFemaleClothCustom < 2)
		{
			EquipBts[0].chara = females[hSceneManager.numFemaleClothCustom];
			EquipBts[1].chara = females[hSceneManager.numFemaleClothCustom];
			EquipBts[2].chara = females[hSceneManager.numFemaleClothCustom];
			EquipBts[3].chara = females[hSceneManager.numFemaleClothCustom];
		}
		else
		{
			EquipBts[0].chara = Males[hSceneManager.numFemaleClothCustom - 2];
			EquipBts[1].chara = Males[hSceneManager.numFemaleClothCustom - 2];
			EquipBts[2].chara = Males[hSceneManager.numFemaleClothCustom - 2];
			EquipBts[3].chara = Males[hSceneManager.numFemaleClothCustom - 2];
		}
		EquipBts[0].Base.SetActive(active);
		EquipBts[1].Base.SetActive(active2);
		EquipBts[2].Base.SetActive(active3);
		EquipBts[3].Base.SetActive(active4);
		EquipBts[0].id = 0;
		EquipBts[1].id = 1;
		EquipBts[2].id = 2;
		EquipBts[3].id = 3;
		if (EquipBts[0].chara != null && EquipBts[0].chara.objExtraAccessory[0] != null && EquipBts[0].chara.objExtraAccessory[0].activeSelf != EquipBts[0].active)
		{
			EquipBts[0].ChangeState();
		}
		if (EquipBts[1].chara != null && EquipBts[1].chara.objExtraAccessory[1] != null && EquipBts[1].chara.objExtraAccessory[1].activeSelf != EquipBts[1].active)
		{
			EquipBts[1].ChangeState();
		}
		if (EquipBts[2].chara != null && EquipBts[2].chara.objExtraAccessory[2] != null && EquipBts[2].chara.objExtraAccessory[2].activeSelf != EquipBts[2].active)
		{
			EquipBts[2].ChangeState();
		}
		if (EquipBts[3].chara != null)
		{
			GameObject gameObject = null;
			if (!player)
			{
				gameObject = EquipBts[3].chara.objExtraAccessory[3];
			}
			else if (hSceneManager.numFemaleClothCustom < 2)
			{
				if (hSceneManager.females[hSceneManager.numFemaleClothCustom].EquipedItem != null)
				{
					gameObject = hSceneManager.females[hSceneManager.numFemaleClothCustom].EquipedItem.AsGameObject;
				}
			}
			else if (hSceneManager.male.EquipedItem != null)
			{
				gameObject = hSceneManager.male.EquipedItem.AsGameObject;
			}
			if (gameObject != null && gameObject.activeSelf != EquipBts[3].active)
			{
				EquipBts[3].ChangeState();
			}
		}
		if (hSceneSpriteChaChoice.Content.activeSelf)
		{
			hSceneSpriteChaChoice.Content.SetActive(value: false);
		}
	}

	public void OnClickAccessory(int _accessory)
	{
		if (Singleton<Scene>.Instance.IsNowLoading || Singleton<Scene>.Instance.IsNowLoadingFade || hSceneSprite.isFade || !hSceneManager.HSceneUISet.activeSelf)
		{
			return;
		}
		Text text = null;
		for (int i = 0; i < AccessorySlots.GetToggleNum(); i++)
		{
			int num = i;
			if (num == _accessory)
			{
				text = AccessorySlots.lstToggle[num].GetComponentInChildren<Text>();
				Color color = text.color;
				if ((hSceneManager.numFemaleClothCustom > 1 || (hSceneManager.Player != null && hSceneManager.Player.ChaControl == females[hSceneManager.numFemaleClothCustom])) && !Config.HData.Accessory)
				{
					AccessorySlots.lstToggle[_accessory].isOn = false;
				}
				if (AccessorySlots.lstToggle[_accessory].isOn)
				{
					Define.Set(ref color, Colors.White);
				}
				else
				{
					color = new Color32(141, 136, 129, byte.MaxValue);
				}
				text.color = color;
				break;
			}
		}
		if (hSceneManager.numFemaleClothCustom < 2)
		{
			females[hSceneManager.numFemaleClothCustom].SetAccessoryState(_accessory, AccessorySlots.lstToggle[_accessory].isOn);
		}
		else
		{
			Males[hSceneManager.numFemaleClothCustom - 2].SetAccessoryState(_accessory, AccessorySlots.lstToggle[_accessory].isOn);
		}
	}

	public void OnClickAllAccessory()
	{
		if (Singleton<Scene>.Instance.IsNowLoading || Singleton<Scene>.Instance.IsNowLoadingFade || hSceneSprite.isFade || !hSceneManager.HSceneUISet.activeSelf)
		{
			return;
		}
		if ((hSceneManager.numFemaleClothCustom > 1 || (hSceneManager.Player != null && hSceneManager.Player.ChaControl == females[hSceneManager.numFemaleClothCustom])) && !Config.HData.Accessory)
		{
			AllChange.isOn = false;
		}
		allState[hSceneManager.numFemaleClothCustom] = AllChange.isOn;
		Text componentInChildren;
		for (int i = 0; i < AccessorySlots.lstToggle.Count; i++)
		{
			componentInChildren = AccessorySlots.lstToggle[i].GetComponentInChildren<Text>();
			if (!GlobalMethod.StartsWith(componentInChildren.text, "スロット"))
			{
				AccessorySlots.SetCheck(AllChange.isOn, i);
			}
		}
		componentInChildren = AllChange.GetComponentInChildren<Text>();
		Color color = componentInChildren.color;
		if (allState[hSceneManager.numFemaleClothCustom])
		{
			Define.Set(ref color, Colors.White);
		}
		else
		{
			color = new Color32(141, 136, 129, byte.MaxValue);
		}
		componentInChildren.color = color;
		if (hSceneManager.numFemaleClothCustom < 2)
		{
			females[hSceneManager.numFemaleClothCustom].SetAccessoryStateAll(allState[hSceneManager.numFemaleClothCustom]);
		}
		else
		{
			Males[hSceneManager.numFemaleClothCustom - 2].SetAccessoryStateAll(allState[hSceneManager.numFemaleClothCustom]);
		}
	}

	public void EndProc()
	{
		for (int i = 0; i < 4; i++)
		{
			ChaControl chaControl = ((i >= 2) ? Males[i - 2] : females[i]);
			if (!(chaControl == null))
			{
				for (int j = 0; j < 4; j++)
				{
					chaControl.ShowExtraAccessory((ChaControlDefine.ExtraAccessoryParts)j, show: true);
				}
			}
		}
	}

	public void ChangeState(int id)
	{
		EquipBts[id].ChangeState();
	}

	public void ChangeStateAllEquip(bool val)
	{
		if (hSceneManager.Player == null)
		{
			return;
		}
		for (int i = 0; i < 4; i++)
		{
			if (i != 3)
			{
				hSceneManager.Player.ChaControl.ShowExtraAccessory((ChaControlDefine.ExtraAccessoryParts)i, val);
				continue;
			}
			hSceneManager.Player.ChaControl.ShowExtraAccessory((ChaControlDefine.ExtraAccessoryParts)i, val);
			if (hSceneManager.Player.EquipedItem != null && hSceneManager.Player.EquipedItem.AsGameObject != null)
			{
				hSceneManager.Player.EquipedItem.AsGameObject.SetActive(val);
			}
		}
	}
}
