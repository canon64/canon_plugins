using AIChara;
using AIProject;
using Manager;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class HSceneSpriteChaChoice : MonoBehaviour
{
	public Button[] OpenCharList;

	public Text LabelText;

	public GameObject Content;

	[SerializeField]
	private Button[] charlist;

	private ChaControl[] females;

	private ChaControl[] Males;

	private Actor[] actor;

	private HScene hScene;

	private HSceneManager hSceneManager;

	public void Init()
	{
		hScene = Singleton<HSceneFlagCtrl>.Instance.GetComponent<HScene>();
		hSceneManager = Singleton<HSceneManager>.Instance;
		females = hScene.GetFemales();
		Males = hScene.GetMales();
		actor = new Actor[4];
		for (int i = 0; i < charlist.Length; i++)
		{
			int no = i;
			bool flag = false;
			flag = ((no >= 2) ? (Males[no - 2] != null && Males[no - 2].objTop != null) : (females[no] != null && females[no].objTop != null));
			charlist[no].gameObject.SetActive(flag);
			if (flag)
			{
				if (no < 2)
				{
					actor[no] = females[no].GetComponentInParent<Actor>();
				}
				else
				{
					actor[no] = Males[no - 2].GetComponentInParent<Actor>();
				}
				if (no == 0)
				{
					Text labelText = LabelText;
					string text = ((!actor[no]) ? charlist[no].name : actor[no].CharaName);
					charlist[no].GetComponentInChildren<Text>().text = text;
					labelText.text = text;
				}
				else
				{
					charlist[no].GetComponentInChildren<Text>().text = ((!actor[no]) ? charlist[no].name : actor[no].CharaName);
				}
			}
			charlist[no].onClick.AddListener(delegate
			{
				hSceneManager.numFemaleClothCustom = no;
				OpenCharList[0].gameObject.SetActive(value: true);
				OpenCharList[1].gameObject.SetActive(value: false);
				Content.SetActive(value: false);
				LabelText.text = ((!actor[no]) ? charlist[no].name : actor[no].CharaName);
			});
		}
		OpenCharList[0].onClick.AddListener(delegate
		{
			OpenCharList[0].gameObject.SetActive(value: false);
			OpenCharList[1].gameObject.SetActive(value: true);
			Content.SetActive(value: true);
		});
		OpenCharList[1].onClick.AddListener(delegate
		{
			OpenCharList[0].gameObject.SetActive(value: true);
			OpenCharList[1].gameObject.SetActive(value: false);
			Content.SetActive(value: false);
		});
	}

	public void SetAction(UnityAction action)
	{
		for (int i = 0; i < charlist.Length; i++)
		{
			int num = i;
			charlist[num].onClick.AddListener(action);
		}
	}

	public void CloseChoice()
	{
		OpenCharList[0].gameObject.SetActive(value: true);
		OpenCharList[1].gameObject.SetActive(value: false);
		Content.SetActive(value: false);
	}

	public void EndProc()
	{
		for (int i = 0; i < charlist.Length; i++)
		{
			int num = i;
			charlist[num].onClick.RemoveAllListeners();
		}
		OpenCharList[0].onClick.RemoveAllListeners();
		OpenCharList[1].onClick.RemoveAllListeners();
	}

	public void SetMaleSelectBtn(bool setVal)
	{
		for (int i = 0; i < Males.Length; i++)
		{
			bool flag = false;
			if (Males[i] != null && Males[i].objTop != null)
			{
				charlist[i + 2].gameObject.SetActive(setVal);
			}
			else if (charlist[i + 2].gameObject.activeSelf)
			{
				charlist[i + 2].gameObject.SetActive(value: false);
			}
		}
		if (!setVal && hSceneManager.numFemaleClothCustom > 1)
		{
			hSceneManager.numFemaleClothCustom = 0;
			LabelText.text = ((!actor[0]) ? charlist[0].name : actor[0].CharaName);
		}
	}
}
