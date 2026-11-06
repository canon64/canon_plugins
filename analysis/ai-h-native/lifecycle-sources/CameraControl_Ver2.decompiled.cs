using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Manager;
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

	private bool isConfigVanish = true;

	private Renderer targetRender;

	private List<VisibleObject> lstMapVanish = new List<VisibleObject>();

	private List<Collider> listCollider = new List<Collider>();

	public bool isOutsideTargetTex { get; set; }

	public bool isCursorLock { get; set; }

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
				visibleFroceVanish(_visible: true);
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
			targetTex.localScale = Vector3.one * 0.1f;
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
		Rigidbody orAddComponent = base.gameObject.GetOrAddComponent<Rigidbody>();
		orAddComponent.useGravity = false;
		orAddComponent.isKinematic = true;
		isInit = true;
		listCollider.Clear();
	}

	protected new void LateUpdate()
	{
		if (Singleton<Scene>.IsInstance())
		{
			if (Singleton<Scene>.Instance.NowSceneNames.Any((string s) => s == "Config"))
			{
				return;
			}
			if (!Singleton<Scene>.Instance.IsNowLoading && !Singleton<Scene>.Instance.IsNowLoadingFade)
			{
				base.LateUpdate();
			}
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
			if (null != targetRender)
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
		visibleFroceVanish(_visible: true);
	}

	public void ClearListCollider()
	{
		listCollider.Clear();
	}

	protected void OnTriggerEnter(Collider other)
	{
		Collider collider = listCollider.Find((Collider x) => other.name == x.name);
		if (collider == null)
		{
			listCollider.Add(other);
		}
	}

	protected void OnTriggerStay(Collider other)
	{
		Collider collider = listCollider.Find((Collider x) => other.name == x.name);
		if (collider == null)
		{
			listCollider.Add(other);
		}
	}

	protected void OnTriggerExit(Collider other)
	{
		listCollider.Clear();
	}

	public void autoCamera(float _fSpeed)
	{
		CamDat.Rot.y = (CamDat.Rot.y + _fSpeed * Time.deltaTime) % 360f;
	}

	public void CameraDataSave(string _strCreateAssetPath, string _strFile)
	{
		FileData fileData = new FileData(string.Empty);
		string text = fileData.Create(_strCreateAssetPath) + _strFile + ".txt";
		Debug.Log(text);
		using StreamWriter streamWriter = new StreamWriter(text, append: false, Encoding.GetEncoding("UTF-8"));
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

	public bool CameraDataLoadBinary(BinaryReader br, bool isUpdate)
	{
		CameraData cameraData = new CameraData
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
		CamReset.Copy(cameraData, Quaternion.identity);
		if (isUpdate)
		{
			CamDat.Copy(cameraData);
			if (base.thisCamera != null)
			{
				base.thisCamera.fieldOfView = cameraData.Fov;
			}
			CameraUpdate();
			if (!isInit)
			{
				isInit = true;
			}
		}
		return true;
	}

	public void visibleFroceVanish(bool _visible)
	{
		foreach (VisibleObject item in lstMapVanish)
		{
			foreach (GameObject item2 in item.listObj)
			{
				if ((bool)item2)
				{
					item2.SetActive(_visible);
				}
			}
			item.isVisible = _visible;
			item.delay = ((!_visible) ? 0f : 0.3f);
		}
	}

	private void visibleFroceVanish(VisibleObject _obj, bool _visible)
	{
		if (_obj != null && _obj.listObj != null)
		{
			for (int i = 0; i < _obj.listObj.Count; i++)
			{
				_obj.listObj[i].SetActive(_visible);
			}
			_obj.delay = ((!_visible) ? 0f : 0.3f);
			_obj.isVisible = _visible;
		}
	}

	private bool VanishProc()
	{
		if (!isConfigVanish)
		{
			return false;
		}
		int i;
		for (i = 0; i < lstMapVanish.Count; i++)
		{
			Collider collider = listCollider.Find((Collider x) => lstMapVanish[i].nameCollider == x.name);
			if (collider == null)
			{
				VanishDelayVisible(lstMapVanish[i]);
			}
			else if (lstMapVanish[i].isVisible)
			{
				visibleFroceVanish(lstMapVanish[i], _visible: false);
			}
		}
		return true;
	}

	private bool VanishDelayVisible(VisibleObject _visible)
	{
		if (_visible.isVisible)
		{
			return false;
		}
		_visible.delay += Time.deltaTime;
		if (_visible.delay >= 0.3f)
		{
			visibleFroceVanish(_visible, _visible: true);
		}
		return true;
	}
}
