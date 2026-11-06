using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cinemachine;
using Manager;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AIProject;

public class VirtualCameraController : CinemachineVirtualCameraBase
{
	[Serializable]
	public class VisibleObjectH : Map.VisibleObject
	{
		public bool initEnable;
	}

	[Serializable]
	protected struct CameraData
	{
		public Vector3 Pos;

		public Vector3 Dir;

		public Vector3 Rot;

		[HideInInspector]
		public float Fov;

		public void Copy(ResetData copy)
		{
			Pos = copy.Pos;
			Dir = copy.Dir;
			Rot = copy.Rot;
			Fov = copy.Fov;
		}
	}

	protected struct ResetData
	{
		public Vector3 Pos;

		public Vector3 Dir;

		public Vector3 Rot;

		public Quaternion RotQ;

		public float Fov;

		public void Copy(CameraData copy, Quaternion rot)
		{
			Pos = copy.Pos;
			Dir = copy.Dir;
			Rot = copy.Rot;
			RotQ = rot;
			Fov = copy.Fov;
		}
	}

	public enum Config
	{
		MoveXZ,
		Rotation,
		Translation,
		MoveXY
	}

	public delegate bool NoCtrlFunc();

	protected Manager.Input input;

	[SerializeField]
	protected Transform follow;

	[SerializeField]
	protected Transform lookAtTarget;

	protected Renderer targetRender;

	protected CameraState m_State = CameraState.Default;

	public float xRotSpeed = 0.5f;

	public float yRotSpeed = 0.5f;

	public float moveSpeed = 0.5f;

	public float keyMoveSpeed = 5f;

	public float limitFov = 40f;

	public bool isLimitPos;

	public bool isLimitDir;

	public bool isLimitRotX;

	public float limitRotX = 90f;

	public float limitPos;

	public float limitDir;

	protected bool isZoomNow;

	protected Manager.Housing housingMgr;

	private const float INIT_FOV = 23f;

	protected bool isConfigVanish = true;

	[SerializeField]
	protected List<VisibleObjectH> lstMapVanish = new List<VisibleObjectH>();

	[SerializeField]
	protected List<Collider> listCollider = new List<Collider>();

	public readonly int CONFIG_SIZE = Enum.GetNames(typeof(Config)).Length;

	[SerializeField]
	protected CameraData CamDat;

	protected Config cameraType = Config.Rotation;

	protected bool[] isDrags;

	protected bool[] isButtons;

	protected ResetData CamReset;

	protected bool isInit;

	protected CapsuleCollider viewCollider;

	protected float rateAddSpeed = 1f;

	public NoCtrlFunc NoCtrlCondition;

	public NoCtrlFunc ZoomCondition;

	public NoCtrlFunc KeyCondition;

	public LensSettings Lens;

	protected bool craft;

	public const string PipelineName = "cm";

	public Camera thisCamera { get; private set; }

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
				visibleForceVanish(_visible: true);
				if ((object)housingMgr != null)
				{
					housingMgr.VisibleShield();
				}
			}
		}
	}

	public bool isControlNow { get; protected set; }

	public override CameraState State => m_State;

	public override Transform LookAt
	{
		get
		{
			return lookAtTarget;
		}
		set
		{
			lookAtTarget = value;
		}
	}

	public override Transform Follow
	{
		get
		{
			return follow;
		}
		set
		{
			follow = value;
		}
	}

	public LensSettings lens
	{
		get
		{
			return Lens;
		}
		set
		{
			Lens = value;
		}
	}

	public Vector3 TargetPos
	{
		get
		{
			return CamDat.Pos;
		}
		set
		{
			CamDat.Pos = value;
		}
	}

	public Vector3 CameraAngle
	{
		get
		{
			return CamDat.Rot;
		}
		set
		{
			base.transform.rotation = Quaternion.Euler(value);
			CamDat.Rot = value;
		}
	}

	protected override void Start()
	{
		base.Start();
		isCursorLock = true;
		viewCollider = base.gameObject.AddComponent<CapsuleCollider>();
		viewCollider.radius = 0.05f;
		viewCollider.isTrigger = true;
		viewCollider.direction = 2;
		Rigidbody rigidbody = base.gameObject.AddComponent<Rigidbody>();
		rigidbody.useGravity = false;
		rigidbody.isKinematic = true;
	}

	public override void InternalUpdateCameraState(Vector3 worldUp, float deltaTime)
	{
		if (base.enabled)
		{
			ForceMoveCam(deltaTime);
		}
	}

	public virtual void ForceMoveCam(float deltaTime = 0f)
	{
		if (craft)
		{
			if (isLimitDir)
			{
				CamDat.Dir.z = Mathf.Clamp(CamDat.Dir.z, 0f - limitDir, 10f);
			}
			if (isLimitPos)
			{
				CamDat.Pos = Vector3.ClampMagnitude(CamDat.Pos, limitPos);
				if (CamDat.Pos.y < 2.5f)
				{
					CamDat.Pos.y = 2.5f;
				}
			}
			if (isLimitRotX)
			{
				CamDat.Rot.x = Mathf.Clamp(CamDat.Rot.x, 0f, limitRotX);
			}
		}
		else
		{
			if (isLimitDir)
			{
				CamDat.Dir.z = Mathf.Clamp(CamDat.Dir.z, 0f - limitDir, 10f);
			}
			if (isLimitPos)
			{
				CamDat.Pos = Vector3.ClampMagnitude(CamDat.Pos, limitPos);
			}
			if (isLimitRotX)
			{
				CamDat.Rot.x = Mathf.Clamp(CamDat.Rot.x, 0f, limitRotX);
			}
		}
		if (follow != null)
		{
			m_State.RawOrientation = follow.rotation * Quaternion.Euler(CamDat.Rot);
			m_State.RawPosition = m_State.RawOrientation * CamDat.Dir + follow.TransformPoint(CamDat.Pos);
		}
		else
		{
			m_State.RawOrientation = Quaternion.Euler(CamDat.Rot);
			m_State.RawPosition = m_State.RawOrientation * CamDat.Dir + CamDat.Pos;
		}
		m_State.Lens = Lens;
		base.transform.position = State.RawPosition;
		base.transform.rotation = State.RawOrientation;
		if (viewCollider != null)
		{
			viewCollider.height = CamDat.Dir.z;
			viewCollider.center = -Vector3.forward * CamDat.Dir.z * 0.5f;
		}
		lookAtTarget.localPosition = CamDat.Pos;
		Vector3 position = base.transform.position;
		position.y = lookAtTarget.position.y;
		lookAtTarget.transform.LookAt(position);
		lookAtTarget.Rotate(new Vector3(90f, 0f, 0f));
		if ((bool)targetRender)
		{
			targetRender.enabled = isControlNow & isOutsideTargetTex & isConfigTargetTex & !isZoomNow;
		}
		if (Singleton<GameCursor>.IsInstance() && isCursorLock)
		{
			Singleton<GameCursor>.Instance.SetCursorLock(isControlNow & isOutsideTargetTex);
		}
		VanishProc();
		foreach (CinemachineCore.Stage value in Enum.GetValues(typeof(CinemachineCore.Stage)))
		{
			InvokePostPipelineStageCallback(this, value, ref m_State, deltaTime);
		}
	}

	public void Init()
	{
		thisCamera = CinemachineCore.Instance.GetActiveBrain(0).OutputCamera;
		housingMgr = Singleton<Manager.Housing>.Instance;
		CamDat.Fov = 23f;
		CamReset.Fov = 23f;
		m_State.Lens = lens;
		isDrags = new bool[CONFIG_SIZE];
		for (int i = 0; i < isDrags.Length; i++)
		{
			isDrags[i] = false;
		}
		if (!isInit)
		{
			isButtons = new bool[CONFIG_SIZE];
			targetRender = lookAtTarget.GetComponent<Renderer>();
			TargetSet(lookAtTarget, isReset: true);
			isOutsideTargetTex = true;
			isConfigTargetTex = true;
			input = Singleton<Manager.Input>.Instance;
			isInit = true;
		}
	}

	protected override void Update()
	{
	}

	protected virtual void LateUpdate()
	{
		isControlNow = false;
		bool flag = false;
		if (!craft && Singleton<HSceneFlagCtrl>.Instance.BeforeHWait)
		{
			return;
		}
		if (!isControlNow)
		{
			NoCtrlFunc noCtrlCondition = NoCtrlCondition;
			flag = false;
			if (noCtrlCondition != null)
			{
				flag = noCtrlCondition();
			}
			if (!flag)
			{
				if (InputTouchProc())
				{
					isControlNow = true;
				}
				else if (InputMouseProc())
				{
					isControlNow = true;
				}
			}
		}
		if (!isControlNow)
		{
			NoCtrlFunc keyCondition = KeyCondition;
			flag = true;
			if (keyCondition != null)
			{
				flag = keyCondition();
			}
			isControlNow |= flag && InputKeyProc();
		}
	}

	protected bool InputTouchProc()
	{
		if (UnityEngine.Input.touchCount < 1)
		{
			return false;
		}
		float num = 10f * Time.deltaTime;
		if (UnityEngine.Input.touchCount == 3)
		{
			Reset(0);
		}
		else if (UnityEngine.Input.touchCount == 1)
		{
			Touch touch = UnityEngine.Input.touches[0];
			TouchPhase phase = touch.phase;
			if (phase != TouchPhase.Began && phase == TouchPhase.Moved)
			{
				float num2 = 0.01f;
				float num3 = 0.1f;
				Vector3 zero = Vector3.zero;
				if (cameraType == Config.Rotation)
				{
					zero.y += touch.deltaPosition.x * xRotSpeed * num * num2;
					zero.x -= touch.deltaPosition.y * yRotSpeed * num * num2;
					zero += base.transform.rotation.eulerAngles;
					base.transform.rotation = Quaternion.Euler(zero);
				}
				else if (cameraType == Config.Translation)
				{
					CamDat.Dir.z -= touch.deltaPosition.x * xRotSpeed * num * num3;
					CamDat.Pos.y += touch.deltaPosition.y * yRotSpeed * num * num3;
				}
				else if (cameraType == Config.MoveXY)
				{
					zero.x = touch.deltaPosition.x * xRotSpeed * num * num3;
					zero.y = touch.deltaPosition.y * yRotSpeed * num * num3;
					CamDat.Pos += base.transform.TransformDirection(zero);
				}
				else if (cameraType == Config.MoveXZ)
				{
					zero.x = touch.deltaPosition.x * xRotSpeed * num * num3;
					zero.z = touch.deltaPosition.y * yRotSpeed * num * num3;
					CamDat.Pos += base.transform.TransformDirection(zero);
				}
			}
		}
		return true;
	}

	protected bool InputMouseProc()
	{
		bool flag = false;
		float deltaTime = Time.deltaTime;
		bool flag2 = false;
		for (int i = 0; i < CONFIG_SIZE; i++)
		{
			if (isDrags[i])
			{
				flag2 = true;
				break;
			}
		}
		if (!flag2 && (EventSystem.current == null || EventSystem.current.IsPointerOverGameObject()))
		{
			if (Singleton<GameCursor>.IsInstance())
			{
				if (flag)
				{
					Singleton<GameCursor>.Instance.SetCursorLock(setLockFlag: true);
				}
				else
				{
					Singleton<GameCursor>.Instance.UnLockCursor();
				}
			}
			return flag;
		}
		isButtons[1] = input.IsDown(ActionID.MouseLeft);
		isButtons[2] = input.IsDown(ActionID.MouseRight);
		isButtons[3] = input.IsDown(ActionID.MouseCenter);
		isButtons[0] = input.IsDown(ActionID.MouseLeft) && input.IsDown(ActionID.MouseRight);
		for (int j = 0; j < CONFIG_SIZE; j++)
		{
			if (isButtons[j])
			{
				isDrags[j] = true;
			}
		}
		for (int k = 0; k < CONFIG_SIZE; k++)
		{
			if (isDrags[k] && !isButtons[k])
			{
				isDrags[k] = false;
			}
		}
		float x = input.MouseAxis.x;
		float y = input.MouseAxis.y;
		for (int l = 0; l < CONFIG_SIZE; l++)
		{
			if (!isDrags[l])
			{
				continue;
			}
			Vector3 zero = Vector3.zero;
			switch (l)
			{
			case 0:
				zero.x = x * moveSpeed * deltaTime * rateAddSpeed;
				zero.z = y * moveSpeed * deltaTime * rateAddSpeed;
				if (follow != null)
				{
					CamDat.Pos += follow.InverseTransformDirection(base.transform.TransformDirection(zero));
				}
				else
				{
					CamDat.Pos += base.transform.TransformDirection(zero);
				}
				break;
			case 1:
				zero.y += x * xRotSpeed * deltaTime * rateAddSpeed;
				zero.x -= y * yRotSpeed * deltaTime * rateAddSpeed;
				CamDat.Rot.y = (CamDat.Rot.y + zero.y) % 360f;
				CamDat.Rot.x = (CamDat.Rot.x + zero.x) % 360f;
				break;
			case 2:
				CamDat.Pos.y += y * moveSpeed * deltaTime * rateAddSpeed;
				CamDat.Dir.z -= x * moveSpeed * deltaTime * rateAddSpeed;
				CamDat.Dir.z = Mathf.Min(0f, CamDat.Dir.z);
				break;
			case 3:
				zero.x = x * moveSpeed * deltaTime * rateAddSpeed;
				zero.y = y * moveSpeed * deltaTime * rateAddSpeed;
				if (follow != null)
				{
					CamDat.Pos += follow.InverseTransformDirection(base.transform.TransformDirection(zero));
				}
				else
				{
					CamDat.Pos += base.transform.TransformDirection(zero);
				}
				break;
			}
			flag = true;
			break;
		}
		return flag;
	}

	protected bool InputKeyProc()
	{
		bool flag = false;
		if (input.IsDown(KeyCode.R))
		{
			Reset(0);
		}
		else if (input.IsDown(KeyCode.Keypad5))
		{
			CamDat.Rot.x = CamReset.Rot.x;
			CamDat.Rot.y = CamReset.Rot.y;
		}
		else if (input.IsDown(KeyCode.Slash))
		{
			CamDat.Rot.z = 0f;
		}
		else if (input.IsDown(KeyCode.Semicolon))
		{
			CamDat.Fov = CamReset.Fov;
			Lens.FieldOfView = CamDat.Fov;
		}
		float deltaTime = Time.deltaTime;
		if (input.IsDown(KeyCode.Home))
		{
			flag = true;
			CamDat.Dir.z += deltaTime * keyMoveSpeed * rateAddSpeed;
			CamDat.Dir.z = Mathf.Min(0f, CamDat.Dir.z);
		}
		else if (input.IsDown(KeyCode.End))
		{
			flag = true;
			CamDat.Dir.z -= deltaTime * keyMoveSpeed * rateAddSpeed;
		}
		if (input.IsDown(KeyCode.RightArrow))
		{
			flag = true;
			if (follow != null)
			{
				CamDat.Pos += follow.InverseTransformDirection(base.transform.TransformDirection(new Vector3(deltaTime * keyMoveSpeed * rateAddSpeed, 0f, 0f)));
			}
			else
			{
				CamDat.Pos += base.transform.TransformDirection(new Vector3(deltaTime * keyMoveSpeed * rateAddSpeed, 0f, 0f));
			}
		}
		else if (input.IsDown(KeyCode.LeftArrow))
		{
			flag = true;
			if (follow != null)
			{
				CamDat.Pos += follow.InverseTransformDirection(base.transform.TransformDirection(new Vector3((0f - deltaTime) * keyMoveSpeed * rateAddSpeed, 0f, 0f)));
			}
			else
			{
				CamDat.Pos += base.transform.TransformDirection(new Vector3((0f - deltaTime) * keyMoveSpeed * rateAddSpeed, 0f, 0f));
			}
		}
		if (input.IsDown(KeyCode.UpArrow))
		{
			flag = true;
			if (follow != null)
			{
				CamDat.Pos += follow.InverseTransformDirection(base.transform.TransformDirection(new Vector3(0f, 0f, deltaTime * keyMoveSpeed * rateAddSpeed)));
			}
			else
			{
				CamDat.Pos += base.transform.TransformDirection(new Vector3(0f, 0f, deltaTime * keyMoveSpeed * rateAddSpeed));
			}
		}
		else if (input.IsDown(KeyCode.DownArrow))
		{
			flag = true;
			if (follow != null)
			{
				CamDat.Pos += follow.InverseTransformDirection(base.transform.TransformDirection(new Vector3(0f, 0f, (0f - deltaTime) * keyMoveSpeed * rateAddSpeed)));
			}
			else
			{
				CamDat.Pos += base.transform.TransformDirection(new Vector3(0f, 0f, (0f - deltaTime) * keyMoveSpeed * rateAddSpeed));
			}
		}
		if (input.IsDown(KeyCode.PageUp))
		{
			flag = true;
			CamDat.Pos.y += deltaTime * keyMoveSpeed * rateAddSpeed;
		}
		else if (input.IsDown(KeyCode.PageDown))
		{
			flag = true;
			CamDat.Pos.y -= deltaTime * keyMoveSpeed * rateAddSpeed;
		}
		float num = 10f * Time.deltaTime;
		Vector3 zero = Vector3.zero;
		if (input.IsDown(KeyCode.Period))
		{
			flag = true;
			zero.z += num;
		}
		else if (input.IsDown(KeyCode.Backslash))
		{
			flag = true;
			zero.z -= num;
		}
		if (input.IsDown(KeyCode.Keypad2))
		{
			flag = true;
			zero.x -= num * yRotSpeed;
		}
		else if (input.IsDown(KeyCode.Keypad8))
		{
			flag = true;
			zero.x += num * yRotSpeed;
		}
		if (input.IsDown(KeyCode.Keypad4))
		{
			flag = true;
			zero.y += num * xRotSpeed;
		}
		else if (input.IsDown(KeyCode.Keypad6))
		{
			flag = true;
			zero.y -= num * xRotSpeed;
		}
		if (flag)
		{
			CamDat.Rot.y = (CamDat.Rot.y + zero.y) % 360f;
			CamDat.Rot.x = (CamDat.Rot.x + zero.x) % 360f;
			CamDat.Rot.z = (CamDat.Rot.z + zero.z) % 360f;
		}
		float deltaTime2 = Time.deltaTime;
		if (input.IsDown(KeyCode.Equals))
		{
			flag = true;
			CamDat.Fov = Mathf.Max(CamDat.Fov - deltaTime2 * 15f, 1f);
			Lens.FieldOfView = CamDat.Fov;
			m_State.Lens = Lens;
		}
		else if (input.IsDown(KeyCode.RightBracket))
		{
			flag = true;
			CamDat.Fov = Mathf.Min(CamDat.Fov + deltaTime2 * 15f, limitFov);
			Lens.FieldOfView = CamDat.Fov;
			m_State.Lens = Lens;
		}
		return flag;
	}

	public void Reset(int mode)
	{
		int num = 0;
		if (mode == num++)
		{
			CamDat.Copy(CamReset);
			Lens = m_State.Lens;
			Lens.FieldOfView = CamDat.Fov;
		}
		else if (mode == num++)
		{
			CamDat.Pos = CamReset.Pos;
		}
		else if (mode == num++)
		{
			base.transform.rotation = CamReset.RotQ;
		}
		else if (mode == num++)
		{
			CamDat.Dir = CamReset.Dir;
		}
	}

	public void TargetSet(Transform target, bool isReset)
	{
		if ((bool)target)
		{
			lookAtTarget = target;
		}
		if ((bool)lookAtTarget)
		{
			CamDat.Pos = lookAtTarget.localPosition;
		}
		Transform transform = base.transform;
		CamDat.Dir = Vector3.zero;
		CamDat.Dir.z = 0f - Vector3.Distance(LookAt.position, transform.position);
		transform.LookAt(CamDat.Pos);
		CamDat.Rot = base.transform.rotation.eulerAngles;
		if (isReset)
		{
			CamReset.Copy(CamDat, base.transform.rotation);
		}
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

	public bool CameraDataLoad(string _assetbundleFolder, string _strFile, bool _isDirect = false)
	{
		string text = string.Empty;
		if (!_isDirect)
		{
			text = GlobalMethod.LoadAllListText(_assetbundleFolder, _strFile);
		}
		else
		{
			TextAsset textAsset = CommonLib.LoadAsset<TextAsset>(_assetbundleFolder, _strFile, clone: false, string.Empty);
			AssetBundleManager.UnloadAssetBundle(_assetbundleFolder, isUnloadForceRefCount: true);
			if ((bool)textAsset)
			{
				text = textAsset.text;
			}
		}
		if (text == string.Empty)
		{
			GlobalMethod.DebugLog("cameraファイル読み込めません", 1);
			return false;
		}
		GlobalMethod.GetListString(text, out var data);
		CamDat.Pos.x = float.Parse(data[0][0]);
		CamDat.Pos.y = float.Parse(data[1][0]);
		CamDat.Pos.z = float.Parse(data[2][0]);
		CamDat.Dir.x = float.Parse(data[3][0]);
		CamDat.Dir.y = float.Parse(data[4][0]);
		CamDat.Dir.z = float.Parse(data[5][0]);
		CamDat.Rot.x = float.Parse(data[6][0]);
		CamDat.Rot.y = float.Parse(data[7][0]);
		CamDat.Rot.z = float.Parse(data[8][0]);
		CamDat.Fov = float.Parse(data[9][0]);
		if (thisCamera != null)
		{
			thisCamera.fieldOfView = CamDat.Fov;
		}
		CamReset.Copy(CamDat, Quaternion.identity);
		ForceMoveCam();
		if (!isInit)
		{
			isInit = true;
		}
		return true;
	}

	public bool CameraResetDataLoad(string _assetbundleFolder, string _strFile, bool _isDirect = false)
	{
		string text = string.Empty;
		if (!_isDirect)
		{
			text = GlobalMethod.LoadAllListText(_assetbundleFolder, _strFile);
		}
		else
		{
			TextAsset textAsset = CommonLib.LoadAsset<TextAsset>(_assetbundleFolder, _strFile, clone: false, string.Empty);
			AssetBundleManager.UnloadAssetBundle(_assetbundleFolder, isUnloadForceRefCount: true);
			if ((bool)textAsset)
			{
				text = textAsset.text;
			}
		}
		if (text == string.Empty)
		{
			GlobalMethod.DebugLog("cameraファイル読み込めません", 1);
			return false;
		}
		GlobalMethod.GetListString(text, out var data);
		CameraData copy = new CameraData
		{
			Pos = 
			{
				x = float.Parse(data[0][0]),
				y = float.Parse(data[1][0]),
				z = float.Parse(data[2][0])
			},
			Dir = 
			{
				x = float.Parse(data[3][0]),
				y = float.Parse(data[4][0]),
				z = float.Parse(data[5][0])
			},
			Rot = 
			{
				x = float.Parse(data[6][0]),
				y = float.Parse(data[7][0]),
				z = float.Parse(data[8][0])
			},
			Fov = float.Parse(data[9][0])
		};
		CamReset.Copy(copy, Quaternion.identity);
		return true;
	}

	public void CameraDataReset()
	{
		CamDat.Pos = Vector3.zero;
		CamDat.Dir = Vector3.zero;
		CamDat.Rot = Vector3.zero;
		CamDat.Pos = LookAt.localPosition;
		CamDat.Dir.z = 0f - Vector3.Distance(LookAt.position, base.transform.position);
	}

	public bool loadVanish()
	{
		lstMapVanish.Clear();
		List<Map.VisibleObject> list = Singleton<Map>.Instance.LstMapVanish;
		for (int i = 0; i < list.Count; i++)
		{
			int index = i;
			if (!(list[index].collider == null) && list[index].collider.gameObject.activeSelf)
			{
				VisibleObjectH visibleObjectH = new VisibleObjectH();
				visibleObjectH.nameCollider = list[index].nameCollider;
				visibleObjectH.collider = list[index].collider;
				visibleObjectH.vanishObj = list[index].vanishObj;
				visibleObjectH.initEnable = list[index].collider.enabled;
				lstMapVanish.Add(visibleObjectH);
				visibleObjectH.collider.enabled = true;
			}
		}
		return true;
	}

	public void visibleForceVanish(bool _visible)
	{
		foreach (VisibleObjectH item in lstMapVanish)
		{
			if (item.vanishObj != null)
			{
				item.vanishObj.SetActive(_visible);
			}
			item.isVisible = _visible;
			item.delay = ((!_visible) ? 0f : 0.3f);
		}
	}

	private void VisibleForceVanish(VisibleObjectH _obj, bool _visible)
	{
		if (_obj != null && !(_obj.vanishObj == null))
		{
			_obj.vanishObj.SetActive(_visible);
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
			List<Collider> list = listCollider.FindAll((Collider x) => x != null && lstMapVanish[i].nameCollider == x.name);
			if (list == null || list.Count == 0)
			{
				VanishDelayVisible(lstMapVanish[i]);
			}
			else if (lstMapVanish[i].isVisible)
			{
				VisibleForceVanish(lstMapVanish[i], _visible: false);
			}
		}
		if (viewCollider != null && (object)housingMgr != null)
		{
			housingMgr.ShieldProc(viewCollider);
		}
		return true;
	}

	private bool VanishDelayVisible(VisibleObjectH _visible)
	{
		if (_visible.isVisible)
		{
			return false;
		}
		_visible.delay += Time.deltaTime;
		if (_visible.delay >= 0.3f)
		{
			VisibleForceVanish(_visible, _visible: true);
		}
		return true;
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		visibleForceVanish(_visible: true);
	}

	protected void OnTriggerEnter(Collider other)
	{
		List<Collider> list = listCollider.FindAll((Collider x) => x != null && other.name == x.name);
		if (list == null)
		{
			listCollider.Add(other);
		}
	}

	protected void OnTriggerStay(Collider other)
	{
		Collider collider = listCollider.Find((Collider x) => x != null && other.name == x.name);
		if (collider == null)
		{
			listCollider.Add(other);
		}
	}

	protected void OnTriggerExit(Collider other)
	{
		listCollider.Clear();
	}

	public void ResetVanish()
	{
		for (int i = 0; i < lstMapVanish.Count; i++)
		{
			if (!(lstMapVanish[i].collider == null))
			{
				lstMapVanish[i].collider.enabled = lstMapVanish[i].initEnable;
			}
		}
	}
}
