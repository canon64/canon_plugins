using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AIProject.Scene;
using AIProject.UI;
using Cinemachine;
using Cinemachine.Utility;
using ConfigScene;
using Illusion.Extensions;
using LuxWater;
using Manager;
using PlaceholderSoftware.WetStuff;
using Sirenix.OdinInspector;
using UniRx;
using UnityEngine;

namespace AIProject;

[RequireComponent(typeof(CinemachineBrain))]
public class ActorCameraControl : SerializedMonoBehaviour, IActionCommand
{
	[Serializable]
	public class LocomotionSettingData
	{
		[Header("Normal")]
		[SerializeField]
		private Transform _follow;

		[SerializeField]
		private Transform _lookAt;

		[SerializeField]
		private Transform _lookAtPOV;

		[Header("Action")]
		[SerializeField]
		private Transform _actionFollow;

		[SerializeField]
		private Transform _actionLookAt;

		[SerializeField]
		private Transform _actionLookAtPOV;

		public Transform Follow
		{
			[CompilerGenerated]
			get
			{
				return _follow;
			}
		}

		public Transform LookAt
		{
			[CompilerGenerated]
			get
			{
				return _lookAt;
			}
		}

		public Transform LookAtPOV
		{
			[CompilerGenerated]
			get
			{
				return _lookAtPOV;
			}
		}

		public Transform ActionFollow
		{
			[CompilerGenerated]
			get
			{
				return _actionFollow;
			}
		}

		public Transform ActionLookAt
		{
			[CompilerGenerated]
			get
			{
				return _actionLookAt;
			}
		}

		public Transform ActionLookAtPOV
		{
			[CompilerGenerated]
			get
			{
				return _actionLookAtPOV;
			}
		}
	}

	private class HeadingTracker
	{
		private struct Item
		{
			public Vector3 velocity;

			public float weight;

			public float time;
		}

		private Item[] mHistory;

		private int mTop;

		private int mBottom;

		private int mCount;

		private Vector3 mHeadingSum;

		private float mWeightSum;

		private float mWeightTime;

		private Vector3 mLastGoodHeading = Vector3.zero;

		private static float mDecayExponent;

		public int FilterSize
		{
			[CompilerGenerated]
			get
			{
				return mHistory.Length;
			}
		}

		public HeadingTracker(int filterSize)
		{
			mHistory = new Item[filterSize];
			float num = (float)filterSize / 5f;
			mDecayExponent = (0f - Mathf.Log(2f)) / num;
			ClearHistory();
		}

		private void ClearHistory()
		{
			mTop = (mBottom = (mCount = 0));
			mWeightSum = 0f;
			mHeadingSum = Vector3.zero;
		}

		private static float Decay(float time)
		{
			return Mathf.Exp(time * mDecayExponent);
		}

		public void Add(Vector3 velocity)
		{
			if (FilterSize == 0)
			{
				mLastGoodHeading = velocity;
				return;
			}
			float magnitude = velocity.magnitude;
			if (magnitude > 0.0001f)
			{
				Item item = new Item
				{
					velocity = velocity,
					weight = magnitude,
					time = Time.time
				};
				if (mCount == FilterSize)
				{
					PopBottom();
				}
				mCount++;
				mHistory[mTop] = item;
				if (++mTop == FilterSize)
				{
					mTop = 0;
				}
				mWeightSum *= Decay(item.time - mWeightTime);
				mWeightTime = item.time;
				mWeightSum += magnitude;
				mHeadingSum += item.velocity;
			}
		}

		private void PopBottom()
		{
			if (mCount > 0)
			{
				float time = Time.time;
				Item item = mHistory[mBottom];
				if (++mBottom == FilterSize)
				{
					mBottom = 0;
				}
				mCount--;
				float num = Decay(time - item.time);
				mWeightSum -= item.weight * num;
				mHeadingSum -= item.velocity * num;
				if (mWeightSum <= 0.0001f || mCount == 0)
				{
					ClearHistory();
				}
			}
		}

		public void DecayHistory()
		{
			float time = Time.time;
			float num = Decay(time - mWeightTime);
			mWeightSum *= num;
			mWeightTime = time;
			if (mWeightSum < 0.0001f)
			{
				ClearHistory();
			}
			else
			{
				mHeadingSum *= num;
			}
		}

		public Vector3 GetReliableHeading()
		{
			if (mWeightSum > 0.0001f && (mCount == mHistory.Length || mLastGoodHeading.AlmostZero()))
			{
				Vector3 v = mHeadingSum / mWeightSum;
				if (!v.AlmostZero())
				{
					mLastGoodHeading = v.normalized;
				}
			}
			return mLastGoodHeading;
		}
	}

	[SerializeField]
	private CinemachineBrain _brain;

	[SerializeField]
	private CameraConfig _cameraConfig;

	private Camera _cameraComponent;

	[SerializeField]
	private GameScreenShot _screenShot;

	[SerializeField]
	private Dictionary<ShotType, CinemachineVirtualCameraBase> _virtualCameraNormalTable = new Dictionary<ShotType, CinemachineVirtualCameraBase>();

	[SerializeField]
	private Dictionary<ShotType, CinemachineVirtualCameraBase> _virtualCameraActionTable = new Dictionary<ShotType, CinemachineVirtualCameraBase>();

	[SerializeField]
	private CinemachineVirtualCameraBase _actionCameraNotMove;

	[SerializeField]
	private CinemachineVirtualCameraBase _advCamera;

	[SerializeField]
	private CinemachineVirtualCameraBase _advNotStandCamera;

	[SerializeField]
	private CinemachineVirtualCameraBase _eventCamera;

	[SerializeField]
	private CinemachineVirtualCameraBase _hCamera;

	[SerializeField]
	private CinemachineVirtualCameraBase _craftingCamera;

	[SerializeField]
	private Transform _advNotStandRoot;

	[SerializeField]
	private Animator _eventCameraLocator;

	[SerializeField]
	private Transform _eventCameraParent;

	[SerializeField]
	private Transform _virtualCameraRoot;

	[SerializeField]
	private CrossFade _crossFade;

	[SerializeField]
	private VanishControl _vanishControl;

	[SerializeField]
	private LocomotionSettingData _locomotionSetting;

	[SerializeField]
	private CinemachineVirtualCameraBase _activeVirtualCamera;

	private Dictionary<ShotType, CinemachineVirtualCameraBase> _activeCameraTable;

	[SerializeField]
	private CustomAxisState _yAxis = new CustomAxisState(0f, 1f, wrap_: false, rangeLocked: true, 2f, 0.2f, 0.1f, ActionID.CameraVertical, "Mouse Y", invert: false);

	[SerializeField]
	private CustomAxisState.Recentering _yAxisRecentering = new CustomAxisState.Recentering(enabled_: false, 1f, 2f);

	[SerializeField]
	private CustomAxisState _xAxis = new CustomAxisState(-180f, 180f, wrap_: true, rangeLocked: false, 300f, 0.1f, 0.1f, ActionID.CameraHorizontal, "Mouse X", invert: true);

	[SerializeField]
	private CustomAxisState.Recentering _recenterToTargetHeading = new CustomAxisState.Recentering(enabled_: true, 1f, 2f);

	[SerializeField]
	private LensSettings _lensSetting = default(LensSettings);

	private LocomotionProfile.LensSettings _defaultLensSetting = default(LocomotionProfile.LensSettings);

	[SerializeField]
	private GameObject _charaLightNormal;

	[SerializeField]
	private GameObject _charaLightCustom;

	private Light _enviroLight;

	public bool _updateCustomLight = true;

	[SerializeField]
	private Light _normalKeyLight;

	[SerializeField]
	private Light _customKeyLight;

	private List<KeyCodeDownCommand> _keyCommands = new List<KeyCodeDownCommand>();

	private List<KeyCodeCommand> _keyDownCommands = new List<KeyCodeCommand>();

	private Subject<bool> _isBlendingChange = new Subject<bool>();

	private LuxWater_UnderWaterRendering _underWaterFX;

	private LuxWater_UnderWaterBlur _underWaterBlurFX;

	private WetStuff _wetStuff;

	[SerializeField]
	private WetDecal _wetDecal;

	[SerializeField]
	private Vector3 _wetDecalOffset = Vector3.zero;

	private bool _prevAmb;

	private Transform _prevTarget;

	private Vector3 _lastTargetPosition = Vector3.zero;

	private Rigidbody _targetRigidbody;

	private HeadingTracker _headingTracker;

	private CameraMode _mode;

	private static Dictionary<CameraMode, Action<ActorCameraControl, CameraMode, ShotType>> _activationEventTable = new Dictionary<CameraMode, Action<ActorCameraControl, CameraMode, ShotType>>
	{
		[CameraMode.Normal] = delegate(ActorCameraControl x, CameraMode y, ShotType z)
		{
			foreach (KeyValuePair<ShotType, CinemachineVirtualCameraBase> item in x._virtualCameraNormalTable)
			{
				item.Value.enabled = y == CameraMode.Normal && item.Key == z;
			}
		},
		[CameraMode.ActionFreeLook] = delegate(ActorCameraControl x, CameraMode y, ShotType z)
		{
			if (x._virtualCameraActionTable == null)
			{
				return;
			}
			foreach (KeyValuePair<ShotType, CinemachineVirtualCameraBase> item2 in x._virtualCameraActionTable)
			{
				item2.Value.enabled = y == CameraMode.ActionFreeLook && item2.Key == z;
			}
		},
		[CameraMode.Event] = delegate(ActorCameraControl x, CameraMode y, ShotType z)
		{
			if (x._eventCamera != null)
			{
				x._eventCamera.enabled = y == CameraMode.Event;
			}
		},
		[CameraMode.H] = delegate(ActorCameraControl x, CameraMode y, ShotType z)
		{
			if (x._hCamera != null)
			{
				x._eventCamera.enabled = y == CameraMode.H;
			}
		}
	};

	private ShotType _shotType;

	private ShotType _recovShotType;

	private Dictionary<ShotType, Transform> _oldNormalLookAtTransform;

	public ActionCameraData _actionCameraData = default(ActionCameraData);

	private Vector3 prevCamPos;

	private Quaternion prevCamrot;

	public CinemachineBrain CinemachineBrain
	{
		[CompilerGenerated]
		get
		{
			return (!(_brain == null)) ? _brain : (_brain = GetComponent<CinemachineBrain>());
		}
	}

	public CameraConfig CameraConfig
	{
		[CompilerGenerated]
		get
		{
			return _cameraConfig;
		}
	}

	public Camera CameraComponent
	{
		[CompilerGenerated]
		get
		{
			return (!(_cameraComponent == null)) ? _cameraComponent : (_cameraComponent = GetComponent<Camera>());
		}
	}

	public GameScreenShot ScreenShot
	{
		[CompilerGenerated]
		get
		{
			return _screenShot;
		}
	}

	public CinemachineVirtualCameraBase ActionCameraNotMove
	{
		get
		{
			return _actionCameraNotMove;
		}
		set
		{
			_actionCameraNotMove = value;
		}
	}

	public CinemachineVirtualCameraBase ADVCamera
	{
		get
		{
			return _advCamera;
		}
		set
		{
			_advCamera = value;
		}
	}

	public CinemachineVirtualCameraBase ADVNotStandCamera
	{
		get
		{
			return _advNotStandCamera;
		}
		set
		{
			_advNotStandCamera = value;
		}
	}

	public CinemachineVirtualCameraBase EventCamera
	{
		get
		{
			return _eventCamera;
		}
		set
		{
			_eventCamera = value;
		}
	}

	public CinemachineVirtualCameraBase HCamera
	{
		get
		{
			return _hCamera;
		}
		set
		{
			_hCamera = value;
		}
	}

	public CinemachineVirtualCameraBase FishingCamera { get; set; }

	public Transform ADVNotStandRoot
	{
		[CompilerGenerated]
		get
		{
			return _advNotStandRoot;
		}
	}

	public Animator EventCameraLocator
	{
		get
		{
			return _eventCameraLocator;
		}
		set
		{
			_eventCameraLocator = value;
		}
	}

	public Transform EventCameraParent
	{
		[CompilerGenerated]
		get
		{
			return _eventCameraParent;
		}
	}

	public Transform VirtualCameraRoot
	{
		[CompilerGenerated]
		get
		{
			return _virtualCameraRoot;
		}
	}

	public CrossFade CrossFade
	{
		[CompilerGenerated]
		get
		{
			return _crossFade;
		}
	}

	public VanishControl VanishControl
	{
		[CompilerGenerated]
		get
		{
			return _vanishControl;
		}
	}

	public LocomotionSettingData LocomotionSetting
	{
		get
		{
			return _locomotionSetting;
		}
		set
		{
			_locomotionSetting = value;
		}
	}

	public CinemachineFreeLook ActiveFreeLookCamera
	{
		[CompilerGenerated]
		get
		{
			return _activeVirtualCamera as CinemachineFreeLook;
		}
	}

	public CinemachineVirtualCamera ActiveVirtualCamera
	{
		[CompilerGenerated]
		get
		{
			return _activeVirtualCamera as CinemachineVirtualCamera;
		}
	}

	public bool IsChangeable { get; set; } = true;

	public float XAxisValue
	{
		get
		{
			return _xAxis.value;
		}
		set
		{
			if (value > _xAxis.maxSpeed || value < _xAxis.minValue)
			{
				if (_xAxis.wrap)
				{
					if (value > _xAxis.maxValue)
					{
						_xAxis.value = _xAxis.minValue + (value - _xAxis.maxValue);
					}
					else
					{
						_xAxis.value = _xAxis.maxValue + (value - _xAxis.minValue);
					}
				}
				else
				{
					_xAxis.value = Mathf.Clamp(value, _xAxis.minValue, _xAxis.maxValue);
				}
			}
			else
			{
				_xAxis.value = value;
			}
		}
	}

	public float YAxisValue
	{
		get
		{
			return _yAxis.value;
		}
		set
		{
			_yAxis.value = Mathf.Clamp(value, _yAxis.minValue, _yAxis.maxValue);
		}
	}

	public LensSettings LensSetting
	{
		get
		{
			return _lensSetting;
		}
		set
		{
			_lensSetting = value;
		}
	}

	public bool AmbientLight
	{
		set
		{
			if (_charaLightNormal != null)
			{
				_charaLightNormal.SetActiveIfDifferent(value);
			}
			if (_charaLightCustom != null)
			{
				_charaLightCustom.SetActiveIfDifferent(!value);
			}
			if (_prevAmb != value && Singleton<Map>.IsInstance() && Singleton<Map>.Instance.Simulator != null && Singleton<Map>.Instance.Simulator.EnviroSky != null)
			{
				Light component = Singleton<Map>.Instance.Simulator.EnviroSky.Components.DirectLight.GetComponent<Light>();
				if (value)
				{
					component.cullingMask = Singleton<Manager.Resources>.Instance.DefinePack.MapDefines.EnvLightCulMask;
				}
				else
				{
					component.cullingMask = Singleton<Manager.Resources>.Instance.DefinePack.MapDefines.EnvLightCulMaskCustom;
				}
			}
			_prevAmb = value;
		}
	}

	private Light EnviroLight
	{
		get
		{
			if (!Singleton<Map>.IsInstance())
			{
				return null;
			}
			if (_enviroLight == null)
			{
				EnviroSky enviroSky = Singleton<Map>.Instance.Simulator.EnviroSky;
				if (enviroSky != null)
				{
					_enviroLight = enviroSky.Components.DirectLight.GetComponent<Light>();
				}
			}
			return _enviroLight;
		}
	}

	public Light NormalKeyLight
	{
		[CompilerGenerated]
		get
		{
			return _normalKeyLight;
		}
	}

	public Light CustomKeyLight
	{
		[CompilerGenerated]
		get
		{
			return _customKeyLight;
		}
	}

	public Action OnCameraBlended { get; set; }

	public LuxWater_UnderWaterRendering UnderWaterFX
	{
		get
		{
			if (_underWaterFX == null)
			{
				_underWaterFX = GetComponent<LuxWater_UnderWaterRendering>();
			}
			return _underWaterFX;
		}
	}

	public LuxWater_UnderWaterBlur UnderWaterBlurFX
	{
		get
		{
			if (_underWaterBlurFX == null)
			{
				_underWaterBlurFX = GetComponent<LuxWater_UnderWaterBlur>();
			}
			return _underWaterBlurFX;
		}
	}

	public WetStuff WetStuff
	{
		get
		{
			if (_wetStuff == null)
			{
				_wetStuff = GetComponent<WetStuff>();
			}
			return _wetStuff;
		}
	}

	public WetDecal WetDecal
	{
		[CompilerGenerated]
		get
		{
			return _wetDecal;
		}
	}

	public Vector3 WetDecalOffset
	{
		get
		{
			return _wetDecalOffset;
		}
		set
		{
			_wetDecalOffset = value;
		}
	}

	public bool EnabledInput { get; set; }

	public CameraMode Mode
	{
		get
		{
			return _mode;
		}
		set
		{
			if (_mode == value)
			{
				return;
			}
			_mode = value;
			CinemachineVirtualCameraBase cinemachineVirtualCameraBase = null;
			switch (value)
			{
			case CameraMode.Normal:
				_activeCameraTable = _virtualCameraNormalTable;
				break;
			case CameraMode.ActionFreeLook:
				_activeCameraTable = _virtualCameraActionTable;
				break;
			default:
				_activeCameraTable = null;
				switch (value)
				{
				case CameraMode.ActionNotMove:
					cinemachineVirtualCameraBase = _actionCameraNotMove;
					prevCamPos = _brain.transform.position;
					prevCamrot = _brain.transform.rotation;
					break;
				case CameraMode.ADV:
					cinemachineVirtualCameraBase = _advCamera;
					break;
				case CameraMode.ADVExceptStand:
					cinemachineVirtualCameraBase = _advNotStandCamera;
					break;
				case CameraMode.Event:
					cinemachineVirtualCameraBase = _eventCamera;
					break;
				case CameraMode.H:
					cinemachineVirtualCameraBase = _hCamera;
					break;
				case CameraMode.Fishing:
					cinemachineVirtualCameraBase = FishingCamera;
					break;
				case CameraMode.Crafting:
					cinemachineVirtualCameraBase = _craftingCamera;
					break;
				}
				break;
			}
			bool active = value == CameraMode.ADV || value == CameraMode.ADVExceptStand;
			_vanishControl.gameObject.SetActiveIfDifferent(active);
			if (_activeCameraTable != null)
			{
				_activeCameraTable.TryGetValue(_shotType, out var value2);
				cinemachineVirtualCameraBase = value2;
			}
			if (_activeVirtualCamera is CinemachineFreeLook && cinemachineVirtualCameraBase is CinemachineFreeLook)
			{
				CameraState state = GetLiveChildOrSelf(_activeVirtualCamera as CinemachineFreeLook).State;
				_activeVirtualCamera.enabled = false;
				_activeVirtualCamera = cinemachineVirtualCameraBase;
				GetLiveChildOrSelf(_activeVirtualCamera as CinemachineFreeLook).GetComponentOwner().localRotation = state.RawOrientation;
				_activeVirtualCamera.UpdateCameraState(state.ReferenceUp, Time.unscaledDeltaTime);
				_activeVirtualCamera.enabled = true;
			}
			else
			{
				_activeVirtualCamera.enabled = false;
				(_activeVirtualCamera = cinemachineVirtualCameraBase).enabled = true;
			}
		}
	}

	public ShotType ShotType
	{
		get
		{
			return _shotType;
		}
		set
		{
			if (_mode != CameraMode.Event && _mode != CameraMode.H && _mode != CameraMode.ActionNotMove && _shotType != value)
			{
				_shotType = value;
				ChangeActiveCameraByShotType(value);
			}
		}
	}

	public void EnableUpdateCustomLight()
	{
		_updateCustomLight = true;
	}

	public void DisableUpdateCustomLight()
	{
		_updateCustomLight = false;
	}

	private void Start()
	{
		_wetStuff = GetComponent<WetStuff>();
		(from isOn in _isBlendingChange.TakeUntilDestroy(base.gameObject).DistinctUntilChanged()
			where !isOn
			select isOn).Subscribe(delegate
		{
			if (OnCameraBlended != null)
			{
				OnCameraBlended();
				OnCameraBlended = null;
			}
		});
		foreach (KeyValuePair<ShotType, CinemachineVirtualCameraBase> item in _virtualCameraNormalTable)
		{
			item.Value.enabled = false;
		}
		foreach (KeyValuePair<ShotType, CinemachineVirtualCameraBase> item2 in _virtualCameraActionTable)
		{
			item2.Value.enabled = false;
		}
		(_activeVirtualCamera = (_activeCameraTable = _virtualCameraNormalTable)[ShotType.Near]).enabled = true;
		KeyCodeDownCommand keyCodeDownCommand = new KeyCodeDownCommand();
		keyCodeDownCommand.KeyCode = KeyCode.Alpha1;
		keyCodeDownCommand.TriggerEvent.AddListener(delegate
		{
			if (CinemachineBrain.enabled && (_mode == CameraMode.Normal || _mode == CameraMode.ActionFreeLook) && IsChangeable)
			{
				ShotType = ShotType.Near;
			}
		});
		KeyCodeDownCommand keyCodeDownCommand2 = new KeyCodeDownCommand();
		keyCodeDownCommand2.KeyCode = KeyCode.Alpha2;
		keyCodeDownCommand2.TriggerEvent.AddListener(delegate
		{
			if (CinemachineBrain.enabled && (_mode == CameraMode.Normal || _mode == CameraMode.ActionFreeLook) && IsChangeable)
			{
				ShotType = ShotType.PointOfView;
			}
		});
		KeyCodeDownCommand keyCodeDownCommand3 = new KeyCodeDownCommand();
		keyCodeDownCommand3.KeyCode = KeyCode.Semicolon;
		KeyCodeDownCommand keyCodeDownCommand4 = keyCodeDownCommand3;
		keyCodeDownCommand4.TriggerEvent.AddListener(delegate
		{
			_lensSetting.FieldOfView = _defaultLensSetting.FieldOfView;
		});
		keyCodeDownCommand3 = new KeyCodeDownCommand();
		keyCodeDownCommand3.KeyCode = KeyCode.Slash;
		KeyCodeDownCommand keyCodeDownCommand5 = keyCodeDownCommand3;
		keyCodeDownCommand5.TriggerEvent.AddListener(delegate
		{
			_lensSetting.Dutch = _defaultLensSetting.Dutch;
		});
		KeyCodeCommand keyCodeCommand = new KeyCodeCommand();
		keyCodeCommand.KeyCode = KeyCode.Equals;
		KeyCodeCommand keyCodeCommand2 = keyCodeCommand;
		keyCodeCommand2.TriggerEvent.AddListener(delegate(bool isDown)
		{
			if (isDown)
			{
				float deltaTime = Time.deltaTime;
				_lensSetting.FieldOfView = Mathf.Max(_lensSetting.FieldOfView - _defaultLensSetting.KeyZoomScale * deltaTime, _defaultLensSetting.MinFOV);
			}
		});
		keyCodeCommand = new KeyCodeCommand();
		keyCodeCommand.KeyCode = KeyCode.RightBracket;
		KeyCodeCommand keyCodeCommand3 = keyCodeCommand;
		keyCodeCommand3.TriggerEvent.AddListener(delegate(bool isDown)
		{
			if (isDown)
			{
				float deltaTime = Time.deltaTime;
				_lensSetting.FieldOfView = Mathf.Min(_lensSetting.FieldOfView + _defaultLensSetting.KeyZoomScale * deltaTime, _defaultLensSetting.MaxFOV);
			}
		});
		keyCodeCommand = new KeyCodeCommand();
		keyCodeCommand.KeyCode = KeyCode.Period;
		KeyCodeCommand keyCodeCommand4 = keyCodeCommand;
		keyCodeCommand4.TriggerEvent.AddListener(delegate(bool isDown)
		{
			if (isDown)
			{
				float deltaTime = Time.deltaTime;
				float num = _lensSetting.Dutch + _defaultLensSetting.KeyRotateScale * deltaTime;
				if (num <= -180f)
				{
					num %= 360f;
					if (num <= -180f)
					{
						num += 360f;
					}
				}
				else if (180f < num)
				{
					num %= 360f;
					if (180f < num)
					{
						num -= 360f;
					}
				}
				_lensSetting.Dutch = num;
			}
		});
		keyCodeCommand = new KeyCodeCommand();
		keyCodeCommand.KeyCode = KeyCode.Backslash;
		KeyCodeCommand keyCodeCommand5 = keyCodeCommand;
		keyCodeCommand5.TriggerEvent.AddListener(delegate(bool isDown)
		{
			if (isDown)
			{
				float deltaTime = Time.deltaTime;
				float num = _lensSetting.Dutch - _defaultLensSetting.KeyRotateScale * deltaTime;
				if (num <= -180f)
				{
					num %= 360f;
					if (num <= -180f)
					{
						num += 360f;
					}
				}
				else if (180f < num)
				{
					num %= 360f;
					if (180f < num)
					{
						num -= 360f;
					}
				}
				_lensSetting.Dutch = num;
			}
		});
		_keyCommands.Add(keyCodeDownCommand);
		_keyCommands.Add(keyCodeDownCommand2);
		_keyCommands.Add(keyCodeDownCommand4);
		_keyCommands.Add(keyCodeDownCommand5);
		_keyDownCommands.Add(keyCodeCommand2);
		_keyDownCommands.Add(keyCodeCommand3);
		_keyDownCommands.Add(keyCodeCommand4);
		_keyDownCommands.Add(keyCodeCommand5);
		if (Singleton<Map>.IsInstance() && Singleton<Map>.Instance.Simulator != null && Singleton<Map>.Instance.Simulator.EnviroSky != null)
		{
			Light component = Singleton<Map>.Instance.Simulator.EnviroSky.Components.DirectLight.GetComponent<Light>();
			if (Config.GraphicData.AmbientLight)
			{
				component.cullingMask = Singleton<Manager.Resources>.Instance.DefinePack.MapDefines.EnvLightCulMask;
			}
			else
			{
				component.cullingMask = Singleton<Manager.Resources>.Instance.DefinePack.MapDefines.EnvLightCulMaskCustom;
			}
		}
		_prevAmb = Config.GraphicData.AmbientLight;
	}

	public void OnUpdateInput()
	{
		Manager.Input instance = Singleton<Manager.Input>.Instance;
		foreach (KeyCodeDownCommand keyCommand in _keyCommands)
		{
			keyCommand.Invoke(instance);
		}
		foreach (KeyCodeCommand keyDownCommand in _keyDownCommands)
		{
			keyDownCommand.Invoke(instance);
		}
	}

	private void Update()
	{
		if (Singleton<Manager.Scene>.Instance.IsNowLoadingFade)
		{
			CinemachineBrain.enabled = false;
		}
		else if (!CinemachineBrain.enabled && Singleton<MapScene>.IsInstance())
		{
			CinemachineBrain.enabled = !Singleton<MapScene>.Instance.IsLoading;
		}
		AmbientLight = Config.GraphicData.AmbientLight;
		if (_updateCustomLight)
		{
			Light enviroLight = EnviroLight;
			if (enviroLight != null)
			{
				_customKeyLight.color = enviroLight.color;
				_customKeyLight.intensity = enviroLight.intensity;
			}
		}
		if (Mode == CameraMode.Normal || Mode == CameraMode.ActionFreeLook)
		{
			LocomotionProfile locomotionProfile = Singleton<Manager.Resources>.Instance.LocomotionProfile;
			Vector2 defaultCameraAxisPow = locomotionProfile.DefaultCameraAxisPow;
			ActionSystem actData = Config.ActData;
			if (ShotType == ShotType.PointOfView)
			{
				float t = Mathf.InverseLerp(0f, 100f, actData.FPSSensitivityX);
				float t2 = Mathf.InverseLerp(0f, 100f, actData.FPSSensitivityY);
				_xAxis.maxSpeed = locomotionProfile.CameraPowX.Lerp(t);
				_yAxis.maxSpeed = locomotionProfile.CameraPowY.Lerp(t2);
			}
			else
			{
				float t3 = Mathf.InverseLerp(0f, 100f, actData.TPSSensitivityX);
				float t4 = Mathf.InverseLerp(0f, 100f, actData.TPSSensitivityY);
				_xAxis.maxSpeed = locomotionProfile.CameraPowX.Lerp(t3);
				_yAxis.maxSpeed = locomotionProfile.CameraPowY.Lerp(t4);
			}
			_xAxis.invertInput = actData.InvertMoveX;
			_yAxis.invertInput = !actData.InvertMoveY;
			Vector3 cameraAccelRate = locomotionProfile.CameraAccelRate;
			_xAxis.accelTime = cameraAccelRate.x;
			_yAxis.accelTime = cameraAccelRate.y;
		}
	}

	private void LateUpdate()
	{
		UpdateLensSetting();
		if (Singleton<Manager.Scene>.Instance.IsNowLoadingFade)
		{
			return;
		}
		if (_isBlendingChange != null)
		{
			_isBlendingChange.OnNext(_brain.IsBlending);
		}
		if (_vanishControl != null)
		{
			_vanishControl.transform.position = _activeVirtualCamera.transform.position;
			_vanishControl.transform.rotation = _activeVirtualCamera.transform.rotation;
		}
		if (_brain.IsBlending || !(_activeVirtualCamera is CinemachineFreeLook))
		{
			return;
		}
		bool flag = false;
		float smoothDeltaTime = Time.smoothDeltaTime;
		if (EnabledInput && smoothDeltaTime >= 0f && CinemachineCore.Instance.IsLive(_activeVirtualCamera))
		{
			if (_yAxis.Update(smoothDeltaTime))
			{
				_yAxisRecentering.CancelRecentering();
			}
			flag = _xAxis.Update(smoothDeltaTime);
		}
		CinemachineFreeLook cinemachineFreeLook = _activeVirtualCamera as CinemachineFreeLook;
		cinemachineFreeLook.m_YAxis.m_MaxValue = _yAxis.maxValue;
		cinemachineFreeLook.m_YAxis.m_MinValue = _yAxis.minValue;
		cinemachineFreeLook.m_YAxis.Value = _yAxis.value;
		cinemachineFreeLook.m_XAxis.Value = _xAxis.value;
		cinemachineFreeLook.m_RecenterToTargetHeading.m_enabled = _recenterToTargetHeading.enabled;
		cinemachineFreeLook.m_RecenterToTargetHeading.m_RecenteringTime = _recenterToTargetHeading.recenteringTime;
		cinemachineFreeLook.m_RecenterToTargetHeading.m_WaitTime = _recenterToTargetHeading.waitTime;
		CinemachineOrbitalTransposer cinemachineOrbitalTransposer = null;
		if (flag)
		{
			CinemachineVirtualCamera liveChildOrSelf = GetLiveChildOrSelf(cinemachineFreeLook);
			cinemachineOrbitalTransposer = liveChildOrSelf.GetCinemachineComponent<CinemachineOrbitalTransposer>();
			if (cinemachineOrbitalTransposer != null)
			{
				cinemachineOrbitalTransposer.m_RecenterToTargetHeading.CancelRecentering();
			}
		}
		if (cinemachineOrbitalTransposer != null)
		{
			if (cinemachineOrbitalTransposer.m_BindingMode != CinemachineTransposer.BindingMode.SimpleFollowWithWorldUp)
			{
				float targetHeading = GetTargetHeading(_xAxis.value, cinemachineOrbitalTransposer.GetReferenceOrientation(_activeVirtualCamera.State.ReferenceUp), smoothDeltaTime, cinemachineOrbitalTransposer);
				AxisState axis = cinemachineFreeLook.m_XAxis;
				cinemachineOrbitalTransposer.m_RecenterToTargetHeading.DoRecentering(ref axis, smoothDeltaTime, targetHeading);
			}
			if (cinemachineOrbitalTransposer.m_BindingMode == CinemachineTransposer.BindingMode.SimpleFollowWithWorldUp)
			{
				_xAxis.value = 0f;
			}
		}
	}

	private void UpdateLensSetting()
	{
		if (Mode == CameraMode.ADV || Mode == CameraMode.ADVExceptStand || Mode == CameraMode.Event)
		{
			return;
		}
		if (_activeVirtualCamera is CinemachineVirtualCamera)
		{
			CinemachineVirtualCamera cinemachineVirtualCamera = _activeVirtualCamera as CinemachineVirtualCamera;
			LensSettings lens = cinemachineVirtualCamera.m_Lens;
			if (_lensSetting.FieldOfView != lens.FieldOfView)
			{
				cinemachineVirtualCamera.m_Lens.FieldOfView = _lensSetting.FieldOfView;
			}
			if (_lensSetting.NearClipPlane != lens.NearClipPlane)
			{
				cinemachineVirtualCamera.m_Lens.NearClipPlane = _lensSetting.NearClipPlane;
			}
			if (_lensSetting.FarClipPlane != lens.FarClipPlane)
			{
				cinemachineVirtualCamera.m_Lens.FarClipPlane = _lensSetting.FarClipPlane;
			}
			if (_lensSetting.Dutch != lens.Dutch)
			{
				cinemachineVirtualCamera.m_Lens.Dutch = _lensSetting.Dutch;
			}
		}
		else if (_activeVirtualCamera is CinemachineFreeLook)
		{
			CinemachineFreeLook cinemachineFreeLook = _activeVirtualCamera as CinemachineFreeLook;
			LensSettings lens2 = cinemachineFreeLook.m_Lens;
			if (_lensSetting.FieldOfView != lens2.FieldOfView)
			{
				cinemachineFreeLook.m_Lens.FieldOfView = _lensSetting.FieldOfView;
			}
			if (_lensSetting.NearClipPlane != lens2.NearClipPlane)
			{
				cinemachineFreeLook.m_Lens.NearClipPlane = _lensSetting.NearClipPlane;
			}
			if (_lensSetting.FarClipPlane != lens2.FarClipPlane)
			{
				cinemachineFreeLook.m_Lens.FarClipPlane = _lensSetting.FarClipPlane;
			}
			if (_lensSetting.Dutch != lens2.Dutch)
			{
				cinemachineFreeLook.m_Lens.Dutch = _lensSetting.Dutch;
			}
		}
	}

	private CinemachineVirtualCamera GetLiveChildOrSelf(CinemachineFreeLook freeLook)
	{
		float yAxisValue = GetYAxisValue(freeLook);
		if (yAxisValue < 0.33f)
		{
			return freeLook.GetRig(2);
		}
		if (yAxisValue > 0.66f)
		{
			return freeLook.GetRig(0);
		}
		return freeLook.GetRig(1);
	}

	private float GetYAxisValue(CinemachineFreeLook freeLook)
	{
		float num = freeLook.m_YAxis.m_MaxValue - freeLook.m_YAxis.m_MinValue;
		return (!(num > 0.0001f)) ? 0.5f : (freeLook.m_YAxis.Value / num);
	}

	private float GetTargetHeading(float currentHeading, Quaternion targetOrientation, float deltaTime, CinemachineOrbitalTransposer transposer)
	{
		if (transposer.m_BindingMode == CinemachineTransposer.BindingMode.SimpleFollowWithWorldUp)
		{
			return 0f;
		}
		if (transposer.FollowTarget == null)
		{
			return currentHeading;
		}
		CinemachineOrbitalTransposer.Heading.HeadingDefinition headingDefinition = transposer.m_Heading.m_Definition;
		if (_prevTarget != transposer.FollowTarget)
		{
			_prevTarget = transposer.FollowTarget;
			_targetRigidbody = (((object)_prevTarget != null) ? _prevTarget.GetComponent<Rigidbody>() : null);
			Vector3? vector = (((object)_prevTarget != null) ? new Vector3?(_prevTarget.position) : ((Vector3?)null));
			_lastTargetPosition = ((!vector.HasValue) ? Vector3.zero : vector.Value);
		}
		if (headingDefinition == CinemachineOrbitalTransposer.Heading.HeadingDefinition.Velocity && _targetRigidbody == null)
		{
			headingDefinition = CinemachineOrbitalTransposer.Heading.HeadingDefinition.PositionDelta;
		}
		Vector3 vector2 = Vector3.zero;
		switch (headingDefinition)
		{
		case CinemachineOrbitalTransposer.Heading.HeadingDefinition.PositionDelta:
			vector2 = transposer.FollowTargetPosition - _lastTargetPosition;
			break;
		case CinemachineOrbitalTransposer.Heading.HeadingDefinition.Velocity:
			vector2 = _targetRigidbody.velocity;
			break;
		case CinemachineOrbitalTransposer.Heading.HeadingDefinition.TargetForward:
			vector2 = transposer.FollowTargetRotation * Vector3.forward;
			break;
		case CinemachineOrbitalTransposer.Heading.HeadingDefinition.WorldForward:
			return 0f;
		}
		int num = transposer.m_Heading.m_VelocityFilterStrength * 5;
		if (_headingTracker == null || _headingTracker.FilterSize != num)
		{
			_headingTracker = new HeadingTracker(num);
		}
		_headingTracker.DecayHistory();
		Vector3 vector3 = targetOrientation * Vector3.up;
		vector2 = vector2.ProjectOntoPlane(vector3);
		if (!vector2.AlmostZero())
		{
			return UnityVectorExtensions.SignedAngle(targetOrientation * Vector3.forward, vector2, vector3);
		}
		return currentHeading;
	}

	public void LoadInitialState()
	{
		foreach (KeyValuePair<CameraMode, Action<ActorCameraControl, CameraMode, ShotType>> item in _activationEventTable)
		{
			item.Value?.Invoke(this, _mode, _shotType);
		}
	}

	public void SetShotTypeForce(ShotType type)
	{
		_recovShotType = _shotType;
		_shotType = type;
		if (_activeCameraTable != null)
		{
			ChangeActiveCameraByShotType(type);
		}
	}

	public void RecoverShotType()
	{
		SetShotTypeForce(_recovShotType);
	}

	private void ChangeActiveCameraByShotType(ShotType type)
	{
		CameraState state = GetLiveChildOrSelf(_activeVirtualCamera as CinemachineFreeLook).State;
		_activeVirtualCamera.enabled = false;
		_activeVirtualCamera = _activeCameraTable[type];
		GetLiveChildOrSelf(_activeVirtualCamera as CinemachineFreeLook).GetComponentOwner().localRotation = state.RawOrientation;
		_activeVirtualCamera.UpdateCameraState(state.ReferenceUp, Time.unscaledDeltaTime);
		_activeVirtualCamera.enabled = true;
	}

	public void AssignCameraTable(CameraTable table, CameraTable actionTable)
	{
		if (table != null)
		{
			ShotType[] keys = table.Keys;
			ShotType[] array = keys;
			foreach (ShotType shotType in array)
			{
				CinemachineVirtualCameraBase cinemachineVirtualCameraBase = table[shotType];
				_virtualCameraNormalTable[shotType] = cinemachineVirtualCameraBase;
				if (shotType != ShotType.PointOfView)
				{
					cinemachineVirtualCameraBase.Follow = _locomotionSetting.Follow;
					cinemachineVirtualCameraBase.LookAt = _locomotionSetting.LookAt;
				}
				else
				{
					cinemachineVirtualCameraBase.Follow = _locomotionSetting.LookAtPOV;
					cinemachineVirtualCameraBase.LookAt = _locomotionSetting.LookAtPOV;
				}
			}
		}
		if (!(actionTable != null))
		{
			return;
		}
		ShotType[] keys2 = actionTable.Keys;
		ShotType[] array2 = keys2;
		foreach (ShotType shotType2 in array2)
		{
			CinemachineVirtualCameraBase cinemachineVirtualCameraBase2 = actionTable[shotType2];
			_virtualCameraActionTable[shotType2] = cinemachineVirtualCameraBase2;
			if (shotType2 != ShotType.PointOfView)
			{
				cinemachineVirtualCameraBase2.Follow = _locomotionSetting.ActionLookAt;
				cinemachineVirtualCameraBase2.LookAt = _locomotionSetting.ActionLookAt;
			}
			else
			{
				cinemachineVirtualCameraBase2.Follow = _locomotionSetting.ActionLookAtPOV;
				cinemachineVirtualCameraBase2.LookAt = _locomotionSetting.ActionLookAtPOV;
			}
		}
	}

	public void SetNormalLookAt(Transform lookAt)
	{
		if (lookAt == null)
		{
			return;
		}
		ShotType[] array = _virtualCameraNormalTable.Keys.ToArray();
		ShotType[] array2 = array;
		foreach (ShotType key in array2)
		{
			CinemachineVirtualCameraBase cinemachineVirtualCameraBase = _virtualCameraNormalTable[key];
			if (cinemachineVirtualCameraBase != null)
			{
				cinemachineVirtualCameraBase.LookAt = lookAt;
			}
		}
	}

	public void SetNormalPrevLookAt()
	{
		ShotType[] array = _virtualCameraNormalTable.Keys.ToArray();
		ShotType[] array2 = array;
		foreach (ShotType key in array2)
		{
			CinemachineVirtualCameraBase cinemachineVirtualCameraBase = _virtualCameraNormalTable[key];
			Transform lookAt = _oldNormalLookAtTransform[key];
			if (cinemachineVirtualCameraBase != null)
			{
				cinemachineVirtualCameraBase.LookAt = lookAt;
			}
		}
	}

	public void SetLensSetting(LocomotionProfile.LensSettings lensSetting)
	{
		_defaultLensSetting = lensSetting;
		_lensSetting.FieldOfView = lensSetting.FieldOfView;
		_lensSetting.NearClipPlane = lensSetting.NearClipPlane;
		_lensSetting.FarClipPlane = lensSetting.FarClipPlane;
		_lensSetting.Dutch = lensSetting.Dutch;
	}

	private void Reset()
	{
		_brain = GetComponent<CinemachineBrain>();
	}

	public bool LoadActionCameraFile(int _eventTypeID, int _poseID, Transform point = null)
	{
		if (_mode != CameraMode.ActionFreeLook && _mode != CameraMode.ActionNotMove)
		{
			return false;
		}
		if (_mode == CameraMode.ActionFreeLook)
		{
			return LoadActionFreeCameraFile(_eventTypeID, _poseID);
		}
		return LoadActionNonMoveCameraFile(_eventTypeID, _poseID, point);
	}

	private bool LoadActionFreeCameraFile(int _eventTypeID, int _poseID)
	{
		if (!Singleton<Manager.Resources>.Instance.Map.ActionCameraDataTable.TryGetValue(_eventTypeID, out var value))
		{
			_activeVirtualCamera.LookAt.position = prevCamPos;
			return false;
		}
		if (!value.TryGetValue(_poseID, out _actionCameraData))
		{
			_activeVirtualCamera.LookAt.position = prevCamPos;
			return false;
		}
		_activeVirtualCamera.LookAt.position = _actionCameraData.freePos + LocomotionSetting.Follow.position;
		return true;
	}

	private bool LoadActionNonMoveCameraFile(int _eventTypeID, int _poseID, Transform point)
	{
		CinemachineVirtualCamera component = _activeVirtualCamera.GetComponent<CinemachineVirtualCamera>();
		CinemachineTransposer cinemachineComponent = component.GetCinemachineComponent<CinemachineTransposer>();
		if (!Singleton<Manager.Resources>.Instance.Map.ActionCameraDataTable.TryGetValue(_eventTypeID, out var value))
		{
			_activeVirtualCamera.transform.position = prevCamPos;
			_activeVirtualCamera.transform.rotation = prevCamrot;
			cinemachineComponent.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
			cinemachineComponent.m_FollowOffset = prevCamPos - _activeVirtualCamera.Follow.position;
			return false;
		}
		if (!value.TryGetValue(_poseID, out _actionCameraData))
		{
			_activeVirtualCamera.transform.position = prevCamPos;
			_activeVirtualCamera.transform.rotation = prevCamrot;
			cinemachineComponent.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
			cinemachineComponent.m_FollowOffset = prevCamPos - _activeVirtualCamera.Follow.position;
			return false;
		}
		_activeVirtualCamera.transform.position = point.rotation * _actionCameraData.nonMovePos + _activeVirtualCamera.Follow.position;
		_activeVirtualCamera.transform.localRotation = Quaternion.Euler(_actionCameraData.nonMoveRot);
		_activeVirtualCamera.transform.rotation = point.rotation * _activeVirtualCamera.transform.rotation;
		cinemachineComponent.m_BindingMode = CinemachineTransposer.BindingMode.LockToTargetOnAssign;
		cinemachineComponent.m_FollowOffset = _actionCameraData.nonMovePos;
		return true;
	}
}
