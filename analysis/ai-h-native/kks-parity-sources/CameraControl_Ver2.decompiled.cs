using System.Collections.Generic;
using System.IO;
using System.Text;
using ActionGame.Point;
using Illusion.Extensions;
using IllusionUtility.GetUtility;
using Manager;
using UniRx;
using UnityEngine;

public class CameraControl_Ver2 : BaseCameraControl_Ver2
{
	public class VisibleObject
	{
		public string nameCollider;

		public float delay;

		public bool isVisible = true;

		public List<GameObject> listObj = new List<GameObject>();
	}

	public bool isOutsideTargetTex;

	public bool isCursorLock;

	private bool isConfigVanish = true;

	private Renderer targetRender;

	public bool isFlashVisible;

	private List<VisibleObject> lstMapVanish = new List<VisibleObject>();

	private List<Collider> listCollider = new List<Collider>();

	public List<GameObject> lstIgnoreObj = new List<GameObject>();

	private bool SecretOpen;

	private const string ishigakiGateClose = "gate_close_col";

	private const string ishigakiGateOpen = "gate_open_col";

	private int closeIdleState = Animator.StringToHash("close_Idle");

	private bool suiteRoomOpen;

	private const string suiteRoomPartition = "move_partion00";

	public bool isConfigTargetTex { get; set; }

	public bool ConfigVanish
	{
		get
		{
			return isConfigVanish;
		}
		set
		{
			if (isConfigVanish != value)
			{
				isConfigVanish = value;
				VisibleFroceVanish(_visible: true, Ignore: true);
			}
		}
	}

	public Transform targetTex { get; private set; }

	protected new void Start()
	{
		base.Start();
		targetTex = base.transform.Find("CameraTarget");
		if ((bool)targetTex)
		{
			targetTex.localScale = Vector3.one * 0.01f;
			targetRender = targetTex.GetComponent<Renderer>();
		}
		isOutsideTargetTex = true;
		isConfigTargetTex = true;
		isConfigVanish = true;
		isCursorLock = true;
		viewCollider = base.gameObject.AddComponent<CapsuleCollider>();
		viewCollider.radius = 0.05f;
		viewCollider.isTrigger = true;
		viewCollider.direction = 2;
		Rigidbody rigidbody = base.gameObject.AddComponent<Rigidbody>();
		rigidbody.useGravity = false;
		rigidbody.isKinematic = true;
		isInit = true;
		listCollider.Clear();
	}

	protected new void LateUpdate()
	{
		if (!SingletonInitializer<Scene>.initialized || Scene.IsOverlap)
		{
			return;
		}
		if (!Scene.IsNowLoading && !Scene.IsNowLoadingFade)
		{
			base.LateUpdate();
		}
		if ((bool)targetTex)
		{
			if (transBase != null)
			{
				targetTex.position = transBase.TransformPoint(CamDat.Pos);
			}
			else
			{
				targetTex.position = CamDat.Pos;
			}
			Vector3 position = base.transform.position;
			position.y = targetTex.position.y;
			targetTex.transform.LookAt(position);
			targetTex.Rotate(new Vector3(90f, 0f, 0f));
			if ((bool)targetRender)
			{
				targetRender.enabled = base.isControlNow & isOutsideTargetTex & isConfigTargetTex;
			}
			if (Singleton<GameCursor>.IsInstance() && isCursorLock)
			{
				Singleton<GameCursor>.Instance.SetCursorLock(base.isControlNow & isOutsideTargetTex);
			}
		}
		VanishProc();
	}

	private void OnDisable()
	{
		VisibleFroceVanish(_visible: true);
	}

	protected void OnTriggerEnter(Collider other)
	{
		if (listCollider.Find((Collider x) => other.name == x.name) == null)
		{
			listCollider.Add(other);
		}
	}

	protected void OnTriggerStay(Collider other)
	{
		if (listCollider.Find((Collider x) => other.name == x.name) == null)
		{
			listCollider.Add(other);
		}
	}

	protected void OnTriggerExit(Collider other)
	{
		listCollider.Clear();
	}

	public void CleraVanishCollider()
	{
		listCollider.Clear();
	}

	public void autoCamera(float _fSpeed)
	{
		CamDat.Rot.y = (CamDat.Rot.y + _fSpeed * Time.deltaTime) % 360f;
	}

	public void CameraDataSave(string _strCreateAssetPath, string _strFile)
	{
		using StreamWriter streamWriter = new StreamWriter(new FileData().Create(_strCreateAssetPath) + _strFile + ".txt", append: false, Encoding.GetEncoding("UTF-8"));
		streamWriter.Write(CamDat.Pos.x);
		streamWriter.Write('\n');
		streamWriter.Write(CamDat.Pos.y);
		streamWriter.Write('\n');
		streamWriter.Write(CamDat.Pos.z);
		streamWriter.Write('\n');
		streamWriter.Write(CamDat.Dir.x);
		streamWriter.Write('\n');
		streamWriter.Write(CamDat.Dir.y);
		streamWriter.Write('\n');
		streamWriter.Write(CamDat.Dir.z);
		streamWriter.Write('\n');
		streamWriter.Write(CamDat.Rot.x);
		streamWriter.Write('\n');
		streamWriter.Write(CamDat.Rot.y);
		streamWriter.Write('\n');
		streamWriter.Write(CamDat.Rot.z);
		streamWriter.Write('\n');
		streamWriter.Write(CamDat.Fov);
		streamWriter.Write('\n');
	}

	public void CameraDataSaveBinary(BinaryWriter bw)
	{
		bw.Write(CamDat.Pos.x);
		bw.Write(CamDat.Pos.y);
		bw.Write(CamDat.Pos.z);
		bw.Write(CamDat.Dir.x);
		bw.Write(CamDat.Dir.y);
		bw.Write(CamDat.Dir.z);
		bw.Write(CamDat.Rot.x);
		bw.Write(CamDat.Rot.y);
		bw.Write(CamDat.Rot.z);
		bw.Write(CamDat.Fov);
	}

	public bool CameraDataLoad(string _assetbundleFolder, string _strFile, bool _isDirect = false)
	{
		string text = "";
		if (!_isDirect)
		{
			text = GlobalMethod.LoadAllListText(_assetbundleFolder, _strFile);
		}
		else
		{
			TextAsset textAsset = CommonLib.LoadAsset<TextAsset>(_assetbundleFolder, _strFile);
			AssetBundleManager.UnloadAssetBundle(_assetbundleFolder, isUnloadForceRefCount: true);
			if ((bool)textAsset)
			{
				text = textAsset.text;
			}
		}
		if (text == "")
		{
			GlobalMethod.DebugLog("cameraファイル読み込めません", 1);
			return false;
		}
		GlobalMethod.GetListString(text, out var data);
		CamDat.Pos.x = float.Parse(data[0, 0]);
		CamDat.Pos.y = float.Parse(data[1, 0]);
		CamDat.Pos.z = float.Parse(data[2, 0]);
		CamDat.Dir.x = float.Parse(data[3, 0]);
		CamDat.Dir.y = float.Parse(data[4, 0]);
		CamDat.Dir.z = float.Parse(data[5, 0]);
		CamDat.Rot.x = float.Parse(data[6, 0]);
		CamDat.Rot.y = float.Parse(data[7, 0]);
		CamDat.Rot.z = float.Parse(data[8, 0]);
		CamDat.Fov = float.Parse(data[9, 0]);
		if (base.thisCmaera != null)
		{
			base.thisCmaera.fieldOfView = CamDat.Fov;
		}
		CamReset.Copy(CamDat, Quaternion.identity);
		CameraUpdate();
		if (!isInit)
		{
			isInit = true;
		}
		return true;
	}

	public bool CameraResetDataLoad(string _assetbundleFolder, string _strFile, bool _isDirect = false)
	{
		string text = "";
		if (!_isDirect)
		{
			text = GlobalMethod.LoadAllListText(_assetbundleFolder, _strFile);
		}
		else
		{
			TextAsset textAsset = CommonLib.LoadAsset<TextAsset>(_assetbundleFolder, _strFile);
			AssetBundleManager.UnloadAssetBundle(_assetbundleFolder, isUnloadForceRefCount: true);
			if ((bool)textAsset)
			{
				text = textAsset.text;
			}
		}
		if (text == "")
		{
			GlobalMethod.DebugLog("cameraファイル読み込めません", 1);
			return false;
		}
		GlobalMethod.GetListString(text, out var data);
		CameraData copy = new CameraData
		{
			Pos = 
			{
				x = float.Parse(data[0, 0]),
				y = float.Parse(data[1, 0]),
				z = float.Parse(data[2, 0])
			},
			Dir = 
			{
				x = float.Parse(data[3, 0]),
				y = float.Parse(data[4, 0]),
				z = float.Parse(data[5, 0])
			},
			Rot = 
			{
				x = float.Parse(data[6, 0]),
				y = float.Parse(data[7, 0]),
				z = float.Parse(data[8, 0])
			},
			Fov = float.Parse(data[9, 0])
		};
		CamReset.Copy(copy, Quaternion.identity);
		return true;
	}

	public bool CameraDataLoadBinary(BinaryReader br, bool isUpdate)
	{
		CameraData copy = new CameraData
		{
			Pos = 
			{
				x = br.ReadSingle(),
				y = br.ReadSingle(),
				z = br.ReadSingle()
			},
			Dir = 
			{
				x = br.ReadSingle(),
				y = br.ReadSingle(),
				z = br.ReadSingle()
			},
			Rot = 
			{
				x = br.ReadSingle(),
				y = br.ReadSingle(),
				z = br.ReadSingle()
			},
			Fov = br.ReadSingle()
		};
		CamReset.Copy(copy, Quaternion.identity);
		if (isUpdate)
		{
			CamDat.Copy(copy);
			if (base.thisCmaera != null)
			{
				base.thisCmaera.fieldOfView = copy.Fov;
			}
			CameraUpdate();
			if (!isInit)
			{
				isInit = true;
			}
		}
		return true;
	}

	public bool LoadVanish(string _assetbundleFolder, string _strMap, GameObject _objMap)
	{
		lstMapVanish.Clear();
		if (_objMap == null)
		{
			return false;
		}
		GlobalMethod.GetListString(GlobalMethod.LoadAllListText(_assetbundleFolder, _strMap), out var data);
		int length = data.GetLength(0);
		int length2 = data.GetLength(1);
		for (int i = 0; i < length; i++)
		{
			VisibleObject visibleObject = new VisibleObject();
			visibleObject.nameCollider = data[i, 0];
			for (int j = 1; j < length2; j++)
			{
				string text = data[i, j];
				if (text == "")
				{
					break;
				}
				GameObject gameObject = _objMap.transform.FindLoop(text);
				if (!(gameObject == null))
				{
					visibleObject.listObj.Add(gameObject);
				}
			}
			lstMapVanish.Add(visibleObject);
		}
		return true;
	}

	public bool LoadVanishData(string _assetbundleFolder, string _strMap, GameObject _objMap)
	{
		lstMapVanish.Clear();
		if (_objMap == null)
		{
			return false;
		}
		List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(_assetbundleFolder);
		assetBundleNameListFromPath.Sort();
		foreach (string item in assetBundleNameListFromPath)
		{
			if (!AssetBundleCheck.IsFile(item, _strMap) || (!AssetBundleCheck.IsSimulation && !BundleCheck(item, _strMap)))
			{
				continue;
			}
			HCameraVanishData hCameraVanishData = CommonLib.LoadAsset<HCameraVanishData>(item, _strMap);
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: true);
			if (hCameraVanishData == null)
			{
				continue;
			}
			foreach (HCameraVanishData.Param item2 in hCameraVanishData.param)
			{
				VisibleObject visibleObject = new VisibleObject();
				visibleObject.nameCollider = item2.nameCollider;
				foreach (string item3 in item2.nameVisibleObj)
				{
					GameObject gameObject = _objMap.transform.FindLoop(item3);
					if (!(gameObject == null))
					{
						visibleObject.listObj.Add(gameObject);
					}
				}
				lstMapVanish.Add(visibleObject);
			}
		}
		return true;
	}

	public void VisibleFroceVanish(bool _visible, bool Ignore = false)
	{
		int? num = null;
		ActionScene instance = SingletonInitializer<ActionScene>.instance;
		if (instance != null && instance.Map != null)
		{
			num = instance.Map.no;
		}
		foreach (VisibleObject item in lstMapVanish)
		{
			foreach (GameObject item2 in item.listObj)
			{
				if (item2 == null || (Ignore && lstIgnoreObj.Contains(item2)))
				{
					continue;
				}
				if (!num.HasValue || (num.Value != 8 && num.Value != 11))
				{
					item2.SetActive(_visible);
				}
				else
				{
					int num2 = ((!(item.nameCollider == "gate_close_col")) ? ((item.nameCollider == "gate_open_col") ? 1 : 2) : 0);
					if (num2 == 2)
					{
						if (item2.name == "move_partion00")
						{
							item2.SetActive((!_visible) ? _visible : (!suiteRoomOpen && _visible));
						}
						else
						{
							item2.SetActive(_visible);
						}
					}
					else
					{
						item2.SetActive((num2 == 0) ? (!SecretOpen) : SecretOpen);
					}
				}
				if (_visible && !item.isVisible)
				{
					VanishDoorVisible(item2);
				}
			}
			item.isVisible = _visible;
			item.delay = (_visible ? 0.3f : 0f);
		}
	}

	private void VisibleFroceVanish(VisibleObject _obj, bool _visible)
	{
		if (_obj == null || _obj.listObj == null)
		{
			return;
		}
		int num = ((!(_obj.nameCollider == "gate_close_col")) ? ((_obj.nameCollider == "gate_open_col") ? 1 : 2) : 0);
		for (int i = 0; i < _obj.listObj.Count; i++)
		{
			if (lstIgnoreObj.Contains(_obj.listObj[i]))
			{
				continue;
			}
			if (num == 2)
			{
				if (_obj.listObj[i].name == "move_partion00")
				{
					_obj.listObj[i].SetActive((!_visible) ? _visible : (!suiteRoomOpen && _visible));
				}
				else
				{
					_obj.listObj[i].SetActive(_visible);
				}
			}
			else if ((num == 0) ? (!SecretOpen) : SecretOpen)
			{
				_obj.listObj[i].SetActive(_visible);
			}
			if (_visible && !_obj.isVisible)
			{
				VanishDoorVisible(_obj.listObj[i]);
			}
		}
		_obj.delay = (_visible ? 0.3f : 0f);
		_obj.isVisible = _visible;
	}

	private bool VanishProc()
	{
		if (!isConfigVanish)
		{
			return false;
		}
		int i = 0;
		while (i < lstMapVanish.Count)
		{
			if (listCollider.Find((Collider x) => lstMapVanish[i].nameCollider == x.name) == null)
			{
				VanishDelayVisible(lstMapVanish[i]);
			}
			else if (lstMapVanish[i].isVisible)
			{
				VisibleFroceVanish(lstMapVanish[i], _visible: false);
			}
			int num = i + 1;
			i = num;
		}
		return true;
	}

	private bool VanishDelayVisible(VisibleObject _visible)
	{
		if (_visible.isVisible)
		{
			return false;
		}
		if (!isFlashVisible)
		{
			_visible.delay += Time.deltaTime;
			if (_visible.delay >= 0.3f)
			{
				VisibleFroceVanish(_visible, _visible: true);
			}
		}
		else
		{
			VisibleFroceVanish(_visible, _visible: true);
		}
		return true;
	}

	private void VanishDoorVisible(GameObject obj)
	{
		if (Scene.NowSceneNames != null && Scene.NowSceneNames.Contains("HPointMove"))
		{
			return;
		}
		Door componentInChildren = obj.GetComponentInChildren<Door>();
		if (componentInChildren == null)
		{
			return;
		}
		Renderer rend = null;
		Renderer[] componentsInChildren = componentInChildren.GetComponentsInChildren<Renderer>();
		foreach (Renderer renderer in componentsInChildren)
		{
			if (!(renderer.name == componentInChildren.cachedCollider.name))
			{
				rend = renderer;
				break;
			}
		}
		if (rend == null)
		{
			return;
		}
		Animator animator = componentInChildren.GetAnimator();
		if (!(animator == null))
		{
			rend.enabled = false;
			if (animator.gameObject.activeInHierarchy)
			{
				animator.Play(closeIdleState);
			}
			Observable.NextFrame().Subscribe(delegate
			{
				rend.enabled = true;
			});
		}
	}

	public void SetSecretOpen(bool val)
	{
		SecretOpen = val;
	}

	private bool BundleCheck(string path, string targetName)
	{
		bool result = false;
		string[] allAssetName = AssetBundleCheck.GetAllAssetName(path, _WithExtension: false, "");
		for (int i = 0; i < allAssetName.Length; i++)
		{
			if (allAssetName[i].Compare(targetName, ignoreCase: true))
			{
				result = true;
				break;
			}
		}
		return result;
	}

	public void SetSuiteRoomOpen(bool val)
	{
		suiteRoomOpen = val;
	}
}
