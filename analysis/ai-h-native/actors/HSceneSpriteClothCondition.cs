using System.Collections.Generic;
using AIChara;
using Manager;
using UnityEngine;
using UnityEngine.UI;

public class HSceneSpriteClothCondition : HSceneSpriteCategory
{
	public HSceneSpriteChaChoice hSceneSpriteChaChoice;

	public List<HSceneSpriteClothBtn> objs = new List<HSceneSpriteClothBtn>();

	public Button[] AllChange;

	private HScene hScene;

	private ChaControl[] females;

	private int[] allState = new int[2];

	[SerializeField]
	private HSceneSprite hSceneSprite;

	private HSceneManager hSceneManager;

	public void Init()
	{
		hScene = Singleton<HSceneFlagCtrl>.Instance.GetComponent<HScene>();
		hSceneManager = Singleton<HSceneManager>.Instance;
		females = hScene.GetFemales();
		SetClothCharacter(init: true);
		hSceneSpriteChaChoice.SetAction(delegate
		{
			SetClothCharacter();
		});
	}

	public void SetClothCharacter(bool init = false)
	{
		if (!base.gameObject.activeSelf && !init)
		{
			return;
		}
		if (females[hSceneManager.numFemaleClothCustom].objBodyBone != null && females[hSceneManager.numFemaleClothCustom].visibleAll)
		{
			int num = -1;
			bool flag = true;
			for (int i = 0; i < 8; i++)
			{
				objs[i].SetButton(females[hSceneManager.numFemaleClothCustom].fileStatus.clothesState[i]);
				bool flag2 = females[hSceneManager.numFemaleClothCustom].IsClothesStateKind(i);
				SetActive(flag2, i);
				if (flag2 && flag)
				{
					if (num < 0)
					{
						num = females[hSceneManager.numFemaleClothCustom].fileStatus.clothesState[i];
					}
					else
					{
						flag = num == females[hSceneManager.numFemaleClothCustom].fileStatus.clothesState[i];
					}
				}
			}
			if (!flag)
			{
				allState[hSceneManager.numFemaleClothCustom] = 1;
			}
			else if (num >= 0)
			{
				allState[hSceneManager.numFemaleClothCustom] = num;
				allState[hSceneManager.numFemaleClothCustom] %= 3;
			}
			for (int j = 0; j < AllChange.Length; j++)
			{
				if (allState[hSceneManager.numFemaleClothCustom] != j)
				{
					AllChange[j].gameObject.SetActive(value: false);
				}
				else
				{
					AllChange[j].gameObject.SetActive(value: true);
				}
			}
		}
		if (hSceneSpriteChaChoice.Content.activeSelf)
		{
			hSceneSpriteChaChoice.Content.SetActive(value: false);
		}
	}

	public void OnClickCloth(int _cloth)
	{
		if (Singleton<Scene>.Instance.IsNowLoading || Singleton<Scene>.Instance.IsNowLoadingFade || hSceneSprite.isFade || !hSceneManager.HSceneUISet.activeSelf || (hSceneManager.Player.ChaControl == females[hSceneManager.numFemaleClothCustom] && !Config.HData.Cloth))
		{
			return;
		}
		females[hSceneManager.numFemaleClothCustom].SetClothesStateNext(_cloth);
		int num = -1;
		bool flag = true;
		for (int i = 0; i < 8; i++)
		{
			if (!objs[i].gameObject.activeSelf)
			{
				continue;
			}
			objs[i].SetButton(females[hSceneManager.numFemaleClothCustom].fileStatus.clothesState[i]);
			if (flag)
			{
				if (num < 0)
				{
					num = females[hSceneManager.numFemaleClothCustom].fileStatus.clothesState[i];
				}
				else
				{
					flag = num == females[hSceneManager.numFemaleClothCustom].fileStatus.clothesState[i];
				}
			}
		}
		if (!flag)
		{
			allState[hSceneManager.numFemaleClothCustom] = 1;
		}
		else if (num >= 0)
		{
			allState[hSceneManager.numFemaleClothCustom] = num;
			allState[hSceneManager.numFemaleClothCustom] %= 3;
		}
		for (int j = 0; j < AllChange.Length; j++)
		{
			if (allState[hSceneManager.numFemaleClothCustom] != j)
			{
				AllChange[j].gameObject.SetActive(value: false);
			}
			else
			{
				AllChange[j].gameObject.SetActive(value: true);
			}
		}
	}

	public void OnClickAllCloth()
	{
		if (Singleton<Scene>.Instance.IsNowLoading || Singleton<Scene>.Instance.IsNowLoadingFade || hSceneSprite.isFade || !hSceneManager.HSceneUISet.activeSelf)
		{
			return;
		}
		allState[hSceneManager.numFemaleClothCustom]++;
		allState[hSceneManager.numFemaleClothCustom] %= 3;
		females[hSceneManager.numFemaleClothCustom].SetClothesStateAll((byte)allState[hSceneManager.numFemaleClothCustom]);
		for (int i = 0; i < AllChange.Length; i++)
		{
			if (allState[hSceneManager.numFemaleClothCustom] != i)
			{
				AllChange[i].gameObject.SetActive(value: false);
			}
			else
			{
				AllChange[i].gameObject.SetActive(value: true);
			}
		}
		for (int j = 0; j < 8; j++)
		{
			objs[j].SetButton(females[hSceneManager.numFemaleClothCustom].fileStatus.clothesState[j]);
		}
	}

	public override void SetActive(bool _active, int _array = -1)
	{
		if (_array < 0)
		{
			for (int i = 0; i < objs.Count; i++)
			{
				if (objs[i].gameObject.activeSelf != _active)
				{
					objs[i].gameObject.SetActive(_active);
				}
			}
		}
		else if (objs.Count > _array && objs[_array].gameObject.activeSelf != _active)
		{
			objs[_array].gameObject.SetActive(_active);
		}
	}
}
