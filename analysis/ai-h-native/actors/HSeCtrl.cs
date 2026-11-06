using System.Collections.Generic;
using System.Text;
using AIChara;
using AIProject;
using Illusion.Game;
using IllusionUtility.GetUtility;
using Manager;
using UniRx;
using UnityEngine;

public class HSeCtrl
{
	private struct KeyInfo
	{
		public GameObject objParent;

		public string objParentName;

		public float key;

		public bool isLoop;

		public int loopSwitch;

		public string assetPath;

		public List<string> nameSE;

		public bool isShorts;

		public List<string> nameShortsSE;

		public int nFemale;

		public bool bChangeVol;
	}

	private class Info
	{
		public delegate bool IsCheck<T0, T1, T2>(T0 arg0, T1 arg1, T2 arg2);

		public string nameAnimation;

		public List<KeyInfo> key = new List<KeyInfo>();

		public List<KeyInfo> IsKey(float _old, float _now)
		{
			IsCheck<float, float, float>[] array = new IsCheck<float, float, float>[2] { IsKeyLoop, IsKeyNormal };
			List<KeyInfo> list = new List<KeyInfo>();
			int num = ((!(_old > _now)) ? 1 : 0);
			for (int i = 0; i < key.Count; i++)
			{
				if (array[num](_old, _now, key[i].key))
				{
					list.Add(key[i]);
				}
			}
			return list;
		}

		private bool IsKeyLoop(float _old, float _now, float _key)
		{
			return _old < _key || _now > _key;
		}

		private bool IsKeyNormal(float _old, float _now, float _key)
		{
			return _old <= _key && _key < _now;
		}
	}

	private List<Info> lstInfo = new List<Info>();

	private Dictionary<string, Dictionary<string, Transform>> dicLoop = new Dictionary<string, Dictionary<string, Transform>>();

	private HSceneFlagCtrl hFlag;

	private int oldnameHash;

	private float oldNormalizeTime;

	private string abName = string.Empty;

	private string assetName = string.Empty;

	private ExcelData excelData;

	private List<string> row = new List<string>();

	private KeyInfo infoKey;

	private Info info;

	private string nameAnim;

	private bool[] dicloopContainKey = new bool[2];

	private StringBuilder procNameSE;

	public HSeCtrl()
	{
		hFlag = Singleton<Manager.Resources>.Instance.HSceneTable.HSceneSet.GetComponentInChildren<HSceneFlagCtrl>(includeInactive: true);
	}

	public bool Load(string _strAssetFolder, string _file, GameObject _objBoneMale, params GameObject[] _objBoneFemale)
	{
		lstInfo.Clear();
		dicLoop.Clear();
		oldnameHash = 0;
		oldNormalizeTime = 0f;
		excelData = null;
		Singleton<Manager.Sound>.Instance.Stop(Manager.Sound.Type.GameSE3D);
		if (_file == string.Empty)
		{
			return false;
		}
		GameObject[] array = ((_objBoneFemale.Length <= 1) ? new GameObject[2]
		{
			_objBoneMale,
			_objBoneFemale[0]
		} : new GameObject[3]
		{
			_objBoneMale,
			_objBoneFemale[0],
			_objBoneFemale[1]
		});
		List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(_strAssetFolder);
		assetBundleNameListFromPath.Sort();
		assetName = _file;
		for (int j = 0; j < assetBundleNameListFromPath.Count; j++)
		{
			abName = assetBundleNameListFromPath[j];
			bool flag = false;
			if (!GlobalMethod.AssetFileExist(abName, assetName, string.Empty))
			{
				continue;
			}
			excelData = CommonLib.LoadAsset<ExcelData>(abName, assetName, clone: false, string.Empty);
			Singleton<HSceneManager>.Instance.hashUseAssetBundle.Add(abName);
			if (excelData == null)
			{
				continue;
			}
			int num = 1;
			while (num < excelData.MaxCell)
			{
				row = excelData.list[num++].list;
				int index = 0;
				nameAnim = row.GetElement(index++);
				int num2 = int.Parse(row.GetElement(index++));
				infoKey.objParentName = row.GetElement(index++);
				array[num2].SafeProc(delegate(GameObject o)
				{
					if (o != null)
					{
						infoKey.objParent = o.transform.FindLoop(infoKey.objParentName);
					}
				});
				infoKey.key = float.Parse(row.GetElement(index++));
				infoKey.isLoop = row.GetElement(index++) == "1";
				infoKey.loopSwitch = ((row.GetElement(index++) == "1") ? 1 : 0);
				infoKey.bChangeVol = row.GetElement(index++) == "1";
				infoKey.assetPath = row.GetElement(index++);
				string element = row.GetElement(index++);
				if (element != string.Empty)
				{
					string[] array2 = element.Split('/');
					infoKey.nameSE = new List<string>();
					for (int k = 0; k < array2.Length; k++)
					{
						infoKey.nameSE.Add(array2[k]);
					}
				}
				infoKey.isShorts = row.GetElement(index++) == "1";
				element = row.GetElement(index++);
				if (element != string.Empty)
				{
					string[] array2 = element.Split('/');
					infoKey.nameShortsSE = new List<string>();
					for (int l = 0; l < array2.Length; l++)
					{
						infoKey.nameShortsSE.Add(array2[l]);
					}
				}
				infoKey.nFemale = 0;
				if (row.Count > 11)
				{
					infoKey.nFemale = ((!(row.GetElement(index) == string.Empty)) ? GlobalMethod.GetIntTryParse(row.GetElement(index++)) : 0);
				}
				info = lstInfo.Find((Info i) => i.nameAnimation == nameAnim);
				if (info == null)
				{
					info = new Info();
					info.nameAnimation = nameAnim;
					info.key.Add(infoKey);
					lstInfo.Add(info);
				}
				else
				{
					info.key.Add(infoKey);
				}
			}
		}
		return true;
	}

	public bool Proc(AnimatorStateInfo _ai, ChaControl[] _females)
	{
		if (_females == null)
		{
			return false;
		}
		if (oldnameHash != _ai.shortNameHash)
		{
			oldNormalizeTime = 0f;
		}
		float now = _ai.normalizedTime % 1f;
		procNameSE = StringBuilderPool.Get();
		string key = string.Empty;
		string key2 = string.Empty;
		for (int i = 0; i < lstInfo.Count; i++)
		{
			if (!_ai.IsName(lstInfo[i].nameAnimation))
			{
				continue;
			}
			List<KeyInfo> list = lstInfo[i].IsKey(oldNormalizeTime, now);
			for (int j = 0; j < list.Count; j++)
			{
				procNameSE.Clear();
				procNameSE.Append(GetSEName(list[j].nameSE));
				Illusion.Game.Utils.Sound.Setting setting;
				if (list[j].isLoop)
				{
					if (list[j].loopSwitch == 0)
					{
						bool flag = false;
						for (int k = 0; k < list[j].nameSE.Count; k++)
						{
							int index = k;
							if (dicLoop.ContainsKey(list[j].nameSE[index]))
							{
								key = list[j].nameSE[index];
								if (dicLoop[key].ContainsKey(list[j].objParentName))
								{
									flag = true;
									key2 = list[j].objParentName;
									break;
								}
							}
						}
						if (flag)
						{
							Singleton<Manager.Sound>.Instance.Stop(dicLoop[key][key2]);
							dicLoop[key].Remove(key2);
						}
						continue;
					}
					dicloopContainKey[0] = dicLoop.ContainsKey(procNameSE.ToString());
					dicloopContainKey[1] = dicloopContainKey[0] && dicLoop[procNameSE.ToString()].ContainsKey(list[j].objParentName);
					if (dicloopContainKey[0] && dicloopContainKey[1])
					{
						continue;
					}
					setting = new Illusion.Game.Utils.Sound.Setting();
					setting.type = Manager.Sound.Type.GameSE3D;
					setting.assetBundleName = list[j].assetPath;
					setting.assetName = procNameSE.ToString();
					Illusion.Game.Utils.Sound.Setting s = setting;
					Transform trans2 = Illusion.Game.Utils.Sound.Play(s);
					trans2.SafeProcObject(delegate(Transform _)
					{
						_.GetComponent<AudioSource>().SafeProcObject(delegate(AudioSource a)
						{
							a.loop = true;
						});
					});
					AudioSource component = trans2.GetComponent<AudioSource>();
					component.rolloffMode = AudioRolloffMode.Linear;
					GameObject parent2 = list[j].objParent;
					Observable.EveryLateUpdate().Subscribe(delegate
					{
						Vector3 pos2 = ((!parent2) ? Vector3.zero : parent2.transform.position);
						Quaternion rot2 = ((!parent2) ? Quaternion.identity : parent2.transform.rotation);
						trans2.SafeProcObject(delegate(Transform o)
						{
							o.SetPositionAndRotation(pos2, rot2);
						});
					}).AddTo(trans2);
					if (!dicloopContainKey[0])
					{
						dicLoop.Add(procNameSE.ToString(), new Dictionary<string, Transform>());
					}
					if (!dicloopContainKey[1])
					{
						dicLoop[procNameSE.ToString()].Add(list[j].objParentName, null);
					}
					dicLoop[procNameSE.ToString()][list[j].objParentName] = trans2;
					continue;
				}
				setting = new Illusion.Game.Utils.Sound.Setting();
				setting.type = Manager.Sound.Type.GameSE3D;
				setting.assetBundleName = list[j].assetPath;
				setting.assetName = procNameSE.ToString();
				Illusion.Game.Utils.Sound.Setting setting2 = setting;
				if (list[j].isShorts && list[j].nameShortsSE.Count > 0 && _females[list[j].nFemale].IsKokanHide())
				{
					setting2.assetName = GetSEName(list[j].nameShortsSE);
				}
				Transform trans = Illusion.Game.Utils.Sound.Play(setting2);
				AudioSource component2 = trans.GetComponent<AudioSource>();
				component2.rolloffMode = AudioRolloffMode.Linear;
				GameObject parent = list[j].objParent;
				Observable.EveryLateUpdate().Subscribe(delegate
				{
					Vector3 pos = ((!parent) ? Vector3.zero : parent.transform.position);
					Quaternion rot = ((!parent) ? Quaternion.identity : parent.transform.rotation);
					trans.SafeProcObject(delegate(Transform o)
					{
						o.SetPositionAndRotation(pos, rot);
					});
				}).AddTo(trans);
			}
			break;
		}
		oldNormalizeTime = now;
		oldnameHash = _ai.shortNameHash;
		StringBuilderPool.Release(procNameSE);
		return true;
	}

	public bool Proc(AnimatorStateInfo _ai, ChaControl _female, int main = 0)
	{
		if (_female == null)
		{
			return false;
		}
		if (oldnameHash != _ai.shortNameHash)
		{
			oldNormalizeTime = 0f;
		}
		float now = _ai.normalizedTime % 1f;
		procNameSE = StringBuilderPool.Get();
		string key = string.Empty;
		string key2 = string.Empty;
		for (int i = 0; i < lstInfo.Count; i++)
		{
			if (!_ai.IsName(lstInfo[i].nameAnimation))
			{
				continue;
			}
			List<KeyInfo> list = lstInfo[i].IsKey(oldNormalizeTime, now);
			for (int j = 0; j < list.Count; j++)
			{
				procNameSE.Clear();
				procNameSE.Append(GetSEName(list[j].nameSE));
				Illusion.Game.Utils.Sound.Setting setting;
				if (list[j].isLoop)
				{
					if (list[j].loopSwitch == 0)
					{
						bool flag = false;
						for (int k = 0; k < list[j].nameSE.Count; k++)
						{
							int index = k;
							if (dicLoop.ContainsKey(list[j].nameSE[index]))
							{
								key = list[j].nameSE[index];
								if (dicLoop[key].ContainsKey(list[j].objParentName))
								{
									flag = true;
									key2 = list[j].objParentName;
									break;
								}
							}
						}
						if (flag)
						{
							Singleton<Manager.Sound>.Instance.Stop(dicLoop[key][key2]);
							dicLoop[key].Remove(key2);
						}
						continue;
					}
					dicloopContainKey[0] = dicLoop.ContainsKey(procNameSE.ToString());
					dicloopContainKey[1] = dicloopContainKey[0] && dicLoop[procNameSE.ToString()].ContainsKey(list[j].objParentName);
					if (dicloopContainKey[0] && dicloopContainKey[1])
					{
						continue;
					}
					setting = new Illusion.Game.Utils.Sound.Setting();
					setting.type = Manager.Sound.Type.GameSE3D;
					setting.assetBundleName = list[j].assetPath;
					setting.assetName = procNameSE.ToString();
					Illusion.Game.Utils.Sound.Setting s = setting;
					Transform trans2 = Illusion.Game.Utils.Sound.Play(s);
					trans2.SafeProcObject(delegate(Transform _)
					{
						_.GetComponent<AudioSource>().SafeProcObject(delegate(AudioSource a)
						{
							a.loop = true;
						});
					});
					GameObject parent2 = list[j].objParent;
					Observable.EveryLateUpdate().Subscribe(delegate
					{
						Vector3 pos2 = ((!parent2) ? Vector3.zero : parent2.transform.position);
						Quaternion rot2 = ((!parent2) ? Quaternion.identity : parent2.transform.rotation);
						trans2.SafeProcObject(delegate(Transform o)
						{
							o.SetPositionAndRotation(pos2, rot2);
						});
					}).AddTo(trans2);
					if (!dicloopContainKey[0])
					{
						dicLoop.Add(procNameSE.ToString(), new Dictionary<string, Transform>());
					}
					if (!dicloopContainKey[1])
					{
						dicLoop[procNameSE.ToString()].Add(list[j].objParentName, null);
					}
					dicLoop[procNameSE.ToString()][list[j].objParentName] = trans2;
					continue;
				}
				setting = new Illusion.Game.Utils.Sound.Setting();
				setting.type = Manager.Sound.Type.GameSE3D;
				setting.assetBundleName = list[j].assetPath;
				setting.assetName = procNameSE.ToString();
				Illusion.Game.Utils.Sound.Setting setting2 = setting;
				if (list[j].isShorts && list[j].nameShortsSE.Count > 0 && _female.IsKokanHide())
				{
					setting2.assetName = GetSEName(list[j].nameShortsSE);
				}
				Transform trans = Illusion.Game.Utils.Sound.Play(setting2);
				GameObject parent = list[j].objParent;
				Observable.EveryLateUpdate().Subscribe(delegate
				{
					Vector3 pos = ((!parent) ? Vector3.zero : parent.transform.position);
					Quaternion rot = ((!parent) ? Quaternion.identity : parent.transform.rotation);
					trans.SafeProcObject(delegate(Transform o)
					{
						o.SetPositionAndRotation(pos, rot);
					});
				}).AddTo(trans);
			}
			break;
		}
		oldNormalizeTime = now;
		oldnameHash = _ai.shortNameHash;
		StringBuilderPool.Release(procNameSE);
		return true;
	}

	public void InitOldMember(int _init = -1)
	{
		if (_init == -1 || _init == 0)
		{
			oldNormalizeTime = 0f;
		}
		if (_init == -1 || _init == 1)
		{
			oldnameHash = 0;
		}
	}

	private string GetSEName(List<string> list)
	{
		if (list.Count < 1)
		{
			return string.Empty;
		}
		int index = Random.Range(0, list.Count);
		return list[index];
	}
}
