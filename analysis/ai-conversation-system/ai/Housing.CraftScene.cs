using System;
using System.Collections;
using System.Linq;
using System.Runtime.CompilerServices;
using AIProject;
using Manager;
using Sirenix.OdinInspector;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Housing;

public class CraftScene : Singleton<CraftScene>
{
	[SerializeField]
	private UICtrl uiCtrl;

	[SerializeField]
	private CraftCamera craftCamera;

	[SerializeField]
	private BoxCollider[] testColliders;

	[SerializeField]
	[Header("画像作成関係")]
	private Camera[] cameras;

	[SerializeField]
	private int width = 320;

	[SerializeField]
	private int height = 180;

	[SerializeField]
	[Header("終了時処理関係")]
	private Image imageEnd;

	private CraftInfo craftInfo;

	[ReadOnly]
	private InputField inputFieldNow;

	private byte[] bytesCapture;

	private ObjectCtrl overlapObjectCtrl;

	private string[] addSceneNames;

	public bool IsInit { get; private set; }

	public bool IsInputNow
	{
		[CompilerGenerated]
		get
		{
			return (bool)inputFieldNow && inputFieldNow.isFocused;
		}
	}

	public UICtrl UICtrl
	{
		[CompilerGenerated]
		get
		{
			return uiCtrl;
		}
	}

	public WorkingUICtrl WorkingUICtrl { get; private set; }

	public int HousingID
	{
		[CompilerGenerated]
		get
		{
			return Singleton<Map>.IsInstance() ? Singleton<Map>.Instance.HousingID : 0;
		}
	}

	public CraftInfo CraftInfo
	{
		[CompilerGenerated]
		get
		{
			return craftInfo;
		}
	}

	public bool DisplayTutorial { get; set; }

	public BoxCollider[] TestColliders
	{
		[CompilerGenerated]
		get
		{
			return testColliders;
		}
	}

	public bool IsEnd { get; private set; }

	public bool IsEndCheck { get; set; }

	public bool IsDialog
	{
		[CompilerGenerated]
		get
		{
			return Singleton<Game>.IsInstance() && ((bool)Singleton<Game>.Instance.Dialog || (bool)Singleton<Game>.Instance.ExitScene);
		}
	}

	public bool IsGuide
	{
		[CompilerGenerated]
		get
		{
			return Singleton<GuideManager>.IsInstance() && Singleton<GuideManager>.Instance.IsGuide;
		}
	}

	public bool IsShortcutUI
	{
		[CompilerGenerated]
		get
		{
			return Singleton<Game>.IsInstance() && (bool)Singleton<Game>.Instance.MapShortcutUI;
		}
	}

	public bool IsWorkingUI
	{
		[CompilerGenerated]
		get
		{
			return WorkingUICtrl != null && WorkingUICtrl.Visible;
		}
	}

	public void SceneEnd()
	{
		IsEnd = true;
		uiCtrl.ListUICtrl.Select(null);
		imageEnd.enabled = true;
	}

	public void SelectInputField(InputField _input)
	{
		inputFieldNow = _input;
	}

	public void DeselectInputField(InputField _input)
	{
		if (inputFieldNow == _input)
		{
			inputFieldNow = null;
		}
	}

	public void Capture(Action<byte[]> _onCompleted)
	{
		if (cameras.IsNullOrEmpty())
		{
			_onCompleted?.Invoke(null);
			return;
		}
		Observable.FromCoroutine(CaptureFunc).Subscribe((Action<Unit>)delegate
		{
		}, (Action)delegate
		{
			_onCompleted?.Invoke(bytesCapture);
		}).AddTo(this);
	}

	private IEnumerator CaptureFunc()
	{
		yield return new WaitForEndOfFrame();
		Texture2D screenShot2 = new Texture2D(width, height, TextureFormat.RGB24, mipChain: false);
		RenderTexture rt = RenderTexture.GetTemporary(antiAliasing: (QualitySettings.antiAliasing == 0) ? 1 : QualitySettings.antiAliasing, width: screenShot2.width, height: screenShot2.height, depthBuffer: 24, format: RenderTextureFormat.Default, readWrite: RenderTextureReadWrite.Default);
		bool grid = Singleton<GuideManager>.Instance.VisibleGrid;
		Singleton<GuideManager>.Instance.VisibleGrid = false;
		Graphics.SetRenderTarget(rt);
		GL.Clear(clearDepth: true, clearColor: true, Color.black);
		Graphics.SetRenderTarget(null);
		bool sRgbWrite = GL.sRGBWrite;
		GL.sRGBWrite = true;
		Camera[] array = cameras;
		foreach (Camera camera in array)
		{
			if (!(null == camera))
			{
				bool flag = camera.enabled;
				RenderTexture targetTexture = camera.targetTexture;
				Rect rect = camera.rect;
				camera.enabled = true;
				camera.targetTexture = rt;
				camera.Render();
				camera.targetTexture = targetTexture;
				camera.rect = rect;
				camera.enabled = flag;
			}
		}
		GL.sRGBWrite = sRgbWrite;
		RenderTexture.active = rt;
		screenShot2.ReadPixels(new Rect(0f, 0f, screenShot2.width, screenShot2.height), 0, 0);
		screenShot2.Apply();
		RenderTexture.active = null;
		bytesCapture = screenShot2.EncodeToPNG();
		RenderTexture.ReleaseTemporary(rt);
		UnityEngine.Object.Destroy(screenShot2);
		screenShot2 = null;
		Singleton<GuideManager>.Instance.VisibleGrid = grid;
		yield return null;
	}

	private bool CheckAddScene()
	{
		if (!Singleton<Scene>.IsInstance())
		{
			return false;
		}
		bool flag = false;
		foreach (string nowSceneName in Singleton<Scene>.Instance.NowSceneNames)
		{
			flag |= addSceneNames.Contains(nowSceneName);
		}
		return flag;
	}

	protected override void Awake()
	{
		if (CheckInstance())
		{
			addSceneNames = new string[2] { "Config", "Exit" };
		}
	}

	private IEnumerator Start()
	{
		craftInfo = null;
		if (Singleton<Game>.IsInstance() && Singleton<Game>.Instance.WorldData != null && Singleton<Map>.IsInstance())
		{
			Singleton<Game>.Instance.WorldData.HousingData.CraftInfos.TryGetValue(Singleton<Map>.Instance.HousingID, out craftInfo);
		}
		Transform tRoot = craftInfo.ObjRoot.transform;
		base.transform.SetPositionAndRotation(tRoot.position, tRoot.rotation);
		Singleton<Manager.Housing>.Instance.SetCraftInfo(craftInfo, _load: false);
		craftInfo.SetOverlapColliders(_flag: true);
		bool tutorial = Singleton<Game>.IsInstance() && Singleton<Game>.Instance.Environment.TutorialProgress <= 13;
		yield return uiCtrl.Init(craftCamera, tutorial);
		CraftCamera obj = craftCamera;
		obj.NoCtrlCondition = (VirtualCameraController.NoCtrlFunc)Delegate.Combine(obj.NoCtrlCondition, new VirtualCameraController.NoCtrlFunc(Singleton<GuideManager>.Instance.NoCameraCtrl));
		CraftCamera obj2 = craftCamera;
		obj2.KeyCondition = (VirtualCameraController.NoCtrlFunc)Delegate.Combine(obj2.KeyCondition, (VirtualCameraController.NoCtrlFunc)(() => (!IsEnd && !IsInputNow && !IsDialog && !IsShortcutUI && !CheckAddScene() && !IsWorkingUI) ? true : false));
		if (DisplayTutorial)
		{
			CraftCamera obj3 = craftCamera;
			obj3.NoCtrlCondition = (VirtualCameraController.NoCtrlFunc)Delegate.Combine(obj3.NoCtrlCondition, (VirtualCameraController.NoCtrlFunc)(() => DisplayTutorial));
			CraftCamera obj4 = craftCamera;
			obj4.KeyCondition = (VirtualCameraController.NoCtrlFunc)Delegate.Combine(obj4.KeyCondition, (VirtualCameraController.NoCtrlFunc)(() => !DisplayTutorial));
		}
		Singleton<GuideManager>.Instance.Init(craftInfo.LimitSize);
		Singleton<GuideManager>.Instance.TransformRoot = tRoot;
		imageEnd.enabled = false;
		uiCtrl.ListUICtrl.UpdateUI();
		Vector3 limit = craftInfo.LimitSize;
		Vector3 limithalf = limit * 0.5f;
		craftCamera.limitAreaMin = new Vector3(0f - limithalf.x, 2.5f, 0f - limithalf.z);
		craftCamera.limitAreaMax = new Vector3(limithalf.x, limit.y, limithalf.z);
		GameObject gameObject = CommonLib.LoadAsset<GameObject>("housing/base/08.unity3d", "Canvas Working", clone: true, string.Empty);
		gameObject.transform.SetParent(base.transform);
		WorkingUICtrl = gameObject.GetComponent<WorkingUICtrl>();
		IsInit = true;
	}

	private void Update()
	{
		if (DisplayTutorial)
		{
			return;
		}
		bool flag = UnityEngine.Input.GetKey(KeyCode.LeftControl) | UnityEngine.Input.GetKey(KeyCode.RightControl);
		if (uiCtrl.SaveLoadUICtrl.Visible || (Singleton<Scene>.IsInstance() && Singleton<Scene>.Instance.IsNowLoadingFade) || IsEndCheck || IsEnd || IsInputNow || IsGuide || IsDialog || IsShortcutUI || CheckAddScene() || IsWorkingUI)
		{
			return;
		}
		if (UnityEngine.Input.GetKeyDown(KeyCode.F2))
		{
			Singleton<Game>.Instance.LoadShortcut(4);
		}
		else
		{
			if (!flag)
			{
				return;
			}
			bool flag2 = UnityEngine.Input.GetKey(KeyCode.LeftShift) | UnityEngine.Input.GetKey(KeyCode.RightShift);
			if (UnityEngine.Input.GetKeyDown(KeyCode.Z))
			{
				if (flag2)
				{
					if (Singleton<UndoRedoManager>.IsInstance())
					{
						Singleton<UndoRedoManager>.Instance.Redo();
					}
				}
				else if (Singleton<UndoRedoManager>.IsInstance())
				{
					Singleton<UndoRedoManager>.Instance.Undo();
				}
			}
			else if (UnityEngine.Input.GetKeyDown(KeyCode.Q))
			{
				uiCtrl.SystemUICtrl.Axis = !uiCtrl.SystemUICtrl.Axis;
			}
			else if (UnityEngine.Input.GetKeyDown(KeyCode.A))
			{
				uiCtrl.SystemUICtrl.Grid = !uiCtrl.SystemUICtrl.Grid;
			}
			else if (UnityEngine.Input.GetKeyDown(KeyCode.S))
			{
				uiCtrl.SystemUICtrl.Save();
			}
			else if (UnityEngine.Input.GetKeyDown(KeyCode.D))
			{
				uiCtrl.ListUICtrl.Duplicate();
			}
			else if (UnityEngine.Input.GetKeyDown(KeyCode.F))
			{
				ObjectCtrl[] selectObjects = Singleton<Selection>.Instance.SelectObjects;
				ObjectCtrl objectCtrl = (selectObjects.IsNullOrEmpty() ? null : selectObjects.Where((ObjectCtrl v) => v.GameObject != null).FirstOrDefault((ObjectCtrl v) => v.Kind == 0));
				if (objectCtrl != null)
				{
					craftCamera.TargetPos = craftCamera.LookAt.parent.InverseTransformPoint(objectCtrl.Position);
				}
			}
			else if (UnityEngine.Input.GetKeyDown(KeyCode.G))
			{
				overlapObjectCtrl = craftInfo.FindOverlapObject(overlapObjectCtrl);
				if (overlapObjectCtrl != null)
				{
					uiCtrl.ListUICtrl.Select(overlapObjectCtrl);
				}
			}
		}
	}
}
