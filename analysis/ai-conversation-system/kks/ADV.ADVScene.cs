using System;
using System.Linq;
using ADV.Backup;
using ActionGame;
using Manager;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityStandardAssets.ImageEffects;

namespace ADV;

public class ADVScene : MonoBehaviour
{
	public class Directing
	{
		private GameObject obj { get; }

		private Action action { get; }

		public Directing(GameObject obj, Action action)
		{
			this.obj = obj;
			this.action = action;
		}

		public void Release()
		{
			action?.Invoke();
			UnityEngine.Object.Destroy(obj);
		}
	}

	private FadeData bkFadeDat;

	private CameraData bkCamDat;

	private bool backDOFConfigEnable;

	[SerializeField]
	private TextScenario scenario;

	[SerializeField]
	private Transform stand;

	[SerializeField]
	private ADVFade advFade;

	[SerializeField]
	private EMTransition emtFade;

	[SerializeField]
	private Transform dofTarget;

	private Camera advCamera;

	private bool isReleased;

	private const string CreateCameraName = "FrontCamera";

	private const string CameraAssetName = "ActionCamera";

	private IDisposable updateDis;

	public TextScenario Scenario => scenario;

	public Transform Stand => stand;

	public ADVFade AdvFade => advFade;

	public EMTransition EMTFade => emtFade;

	public ActionMap Map { get; set; }

	public MonoBehaviour nowScene { get; set; }

	public string startAddSceneName { get; private set; }

	public CorrectLightAngle correctLightAngle { get; private set; }

	public CameraEffector cameraEffector { get; private set; }

	public CameraEffectorConfig cameraEffectorConfig { get; private set; }

	public DepthOfField dof => cameraEffector.dof;

	public Blur blur => cameraEffector.blur;

	public float? fadeTime { get; set; }

	private Directing directing { get; set; }

	private Vector3 dofTargetBackup { get; set; }

	public void SetDOFTargetADVDefault()
	{
		cameraEffector.SetDOFTarget(dofTarget);
	}

	public void DefaultDOF()
	{
		bkCamDat.DefaultDOF(advCamera);
	}

	public void DirectingRegister(Directing directing)
	{
		this.directing?.Release();
		this.directing = directing;
	}

	public void DirectingRelease()
	{
		directing?.Release();
		directing = null;
	}

	private void Init()
	{
		isReleased = false;
		fadeTime = null;
		stand.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
		dofTargetBackup = dofTarget.localPosition;
		emtFade.SetThreshold(0f);
		scenario.AdvCamera = (advCamera = Camera.main);
		if (advCamera == null)
		{
			GameObject asset = AssetBundleManager.LoadAsset("camera/action.unity3d", "ActionCamera", typeof(GameObject)).GetAsset<GameObject>();
			asset = UnityEngine.Object.Instantiate(asset);
			scenario.AdvCamera = (advCamera = asset.GetComponent<Camera>());
			advCamera.name = "FrontCamera";
			AssetBundleManager.UnloadAssetBundle("camera/action.unity3d", isUnloadForceRefCount: false);
		}
		Manager.Sound.Listener = advCamera.transform;
		cameraEffector = SingletonInitializer<Game>.instance.cameraEffector;
		bkCamDat = new CameraData(advCamera);
		if (blur != null)
		{
			blur.iterations = 0;
			blur.blurSpread = 0.5f;
			blur.enabled = false;
		}
		cameraEffectorConfig = cameraEffector.config;
		backDOFConfigEnable = cameraEffectorConfig.useDOF;
		cameraEffectorConfig.SetDOF(enable: false);
		scenario.isAspect = scenario.isAspect;
		correctLightAngle = scenario.AdvCamera.GetComponent<CorrectLightAngle>();
		if (correctLightAngle != null)
		{
			correctLightAngle.condition = delegate
			{
				ChaControl[] array = (from p in scenario.commandController.Characters.Values
					select p.chaCtrl into p
					where p != null
					where p.visibleAll
					select p).ToArray();
				return (array.Length != 1) ? null : array[0];
			};
		}
		ParameterList.Init();
		if (Map == null)
		{
			Map = base.gameObject.AddComponent<ActionMap>();
		}
		if (scenario.isWait || fadeTime.HasValue)
		{
			float time = 0f;
			if (fadeTime.HasValue)
			{
				time = fadeTime.Value;
			}
			advFade.CrossFadeAlpha(isFront: true, 1f, time, ignoreTimeScale: true);
			scenario.isWait = true;
			scenario.isWindowImage = false;
			updateDis = (from _ in this.UpdateAsObservable()
				where advFade.IsFadeInEnd
				select _).Take(1).Subscribe(delegate
			{
				scenario.isWait = false;
				scenario.isWindowImage = true;
			}, ParameterList.WaitEndProc);
		}
		scenario.ConfigProc();
	}

	public void Release()
	{
		if (ExitDialog.isGameEnd || isReleased)
		{
			return;
		}
		isReleased = true;
		if (updateDis != null)
		{
			updateDis.Dispose();
		}
		updateDis = null;
		scenario.Release();
		if (!SingletonInitializer<Scene>.initialized)
		{
			return;
		}
		if (advCamera != null)
		{
			if (advCamera.name == "FrontCamera")
			{
				UnityEngine.Object.Destroy(advCamera.gameObject);
			}
			else if (bkCamDat != null)
			{
				bkCamDat.Load(advCamera);
				if (cameraEffectorConfig != null)
				{
					cameraEffectorConfig.SetDOF(backDOFConfigEnable);
				}
			}
		}
		ActionMap component = GetComponent<ActionMap>();
		if (component != null)
		{
			UnityEngine.Object.Destroy(component);
			if (SingletonInitializer<Scene>.initialized)
			{
				Scene.UnloadBaseScene();
			}
		}
		ParameterList.Release();
		if (correctLightAngle != null)
		{
			correctLightAngle.condition = null;
			correctLightAngle.offset = Vector2.zero;
			correctLightAngle.enabled = true;
			correctLightAngle = null;
		}
		dofTarget.localPosition = dofTargetBackup;
	}

	private void OnEnable()
	{
		SceneParameter.advScene = this;
		scenario.OnInitializedAsync.TakeUntilDestroy(this).Subscribe(delegate
		{
			Init();
		});
		startAddSceneName = Scene.AddSceneName;
	}

	private void OnDisable()
	{
		SceneParameter.advScene = null;
	}

	private void Update()
	{
		if (Scene.IsOverlap && Scene.IsOverlapFirst(SingletonInitializer<ConfigScene>.instance))
		{
			scenario.ConfigProc();
		}
	}
}
