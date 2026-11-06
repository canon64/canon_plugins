using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Illusion.Game;
using IllusionUtility.GetUtility;
using Manager;
using SaveData;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class HandCtrl : MonoBehaviour
{
	public enum AibuColliderKind
	{
		none,
		mouth,
		muneL,
		muneR,
		kokan,
		anal,
		siriL,
		siriR,
		reac_head,
		reac_bodyup,
		reac_bodydown,
		reac_armL,
		reac_armR,
		reac_legL,
		reac_legR
	}

	public enum HandAction
	{
		none,
		judge,
		action,
		detach
	}

	public enum Ctrl
	{
		click,
		drag,
		kiss
	}

	[Serializable]
	public class AibuItem
	{
		public ItemBodyLayer layerAction = new ItemBodyLayer
		{
			body = -1,
			item = -1
		};

		public ItemBodyLayer layerIdle = new ItemBodyLayer
		{
			body = -1,
			item = -1
		};

		public AibuColliderKind kindTouch;

		public LayerInfo layer;

		public Material mHand;

		public Material mSilhouette;

		public GameObject objBody;

		public GameObject objSilhouette;

		public bool isSilhouetteChange;

		public string pathSEAsset = "";

		public string nameSEFile = "";

		public int saveID = -1;

		public bool isVirgin;

		public Transform transformSound;

		public IDisposable disposable;

		private Color colorBackUp = Color.black;

		public int id { get; private set; }

		public int idObj { get; private set; }

		public int idUse { get; private set; }

		public GameObject obj { get; private set; }

		public Animator anm { get; private set; }

		public SkinnedMeshRenderer renderBody { get; set; }

		public SkinnedMeshRenderer renderSilhouette { get; set; }

		public MeshRenderer meshRenderBody { get; set; }

		public MeshRenderer meshRenderSilhouette { get; set; }

		public void SetID(int _id)
		{
			id = _id;
		}

		public void SetIdObj(int _id)
		{
			idObj = _id;
		}

		public void SetIdUse(int _id)
		{
			idUse = _id;
		}

		public void SetObj(GameObject _obj)
		{
			obj = _obj;
		}

		public void SetAnm(Animator _anm)
		{
			anm = _anm;
		}

		public void SilhouetteColor()
		{
			if (!(colorBackUp == Manager.Config.HData.SilhouetteColor))
			{
				if ((bool)mSilhouette)
				{
					mSilhouette.SetColor(ChaShader._Color, Manager.Config.HData.SilhouetteColor);
				}
				colorBackUp = Manager.Config.HData.SilhouetteColor;
			}
		}

		public void SetHandColor(Color _color)
		{
			if ((bool)mHand)
			{
				mHand.SetColor(ChaShader._Color, _color);
			}
		}

		public void Visible()
		{
			if (isSilhouetteChange)
			{
				if ((bool)objBody)
				{
					objBody.SetActive(!Manager.Config.HData.SimpleBody);
				}
				if ((bool)objSilhouette)
				{
					objSilhouette.SetActive(Manager.Config.HData.SimpleBody);
				}
			}
		}
	}

	[Serializable]
	public class AibuIcon
	{
		public int id;

		public Texture2D[] texIcons = new Texture2D[3];
	}

	[Serializable]
	public struct ItemBodyLayer
	{
		public int item;

		public int body;
	}

	[Serializable]
	public class ActionLayer
	{
		public ItemBodyLayer front;

		public ItemBodyLayer back;

		public int dynamicArea = -1;

		public bool enableDynamic;
	}

	[Serializable]
	public class PathInfo
	{
		public string asset;

		public string file;
	}

	[Serializable]
	public class LayerInfo
	{
		public int id;

		public int idIocn;

		public int useArray;

		public int idVisible = -1;

		public int[] idObjects = new int[2] { -1, -1 };

		public string[] nameParents = new string[2] { "", "" };

		public Transform[] transParents = new Transform[2];

		public int actionClick;

		public int actionDrag = 1;

		public PathInfo pathClickSE = new PathInfo();

		public PathInfo pathDragSE = new PathInfo();

		public int[] plays = new int[3] { -1, -1, -1 };

		public ActionLayer[] layerActions = new ActionLayer[3]
		{
			new ActionLayer(),
			new ActionLayer(),
			new ActionLayer()
		};
	}

	[Serializable]
	public class MotionEnableState
	{
		public string nameAnimation = "";

		public bool[] isTouchAreas = new bool[7];

		public int stateMotion;
	}

	public class ReactionMinMax
	{
		public Vector3 min = Vector3.zero;

		public Vector3 max = Vector3.zero;
	}

	public class ReactionParam
	{
		public int id;

		public List<ReactionMinMax> lstMinMax = new List<ReactionMinMax>();
	}

	public class ReactionInfo
	{
		public bool isPlay;

		public float weight = 1f;

		public List<int> lstReleaseEffector = new List<int>();

		public List<ReactionParam> lstParam = new List<ReactionParam>();
	}

	public class KissCameraInfo
	{
		public BaseCameraControl_Ver2.CameraData dataCam;

		public Vector3 oldPos;

		public Quaternion oldRot;
	}

	public class LayerBlendCalc
	{
		public bool isBlend;

		public GlobalMethod.FloatBlend blend = new GlobalMethod.FloatBlend();

		public ItemBodyLayer layerSrc;

		public ItemBodyLayer layerDst;

		public Animator anim;
	}

	public class LayerBlend
	{
		public ChaControl female;

		public LayerBlendCalc[] blends = new LayerBlendCalc[3];

		public LayerBlend()
		{
			for (int i = 0; i < blends.Length; i++)
			{
				blends[i] = new LayerBlendCalc();
			}
		}

		public bool Set(int _arrayItem, Animator _anim, ItemBodyLayer _layerSrc, ItemBodyLayer _layerDst, float _timeBlend = 0.1f)
		{
			if (!female)
			{
				return false;
			}
			LayerBlendCalc layerBlendCalc = blends[_arrayItem];
			if (layerBlendCalc.isBlend)
			{
				if (layerBlendCalc.layerSrc.body != -1)
				{
					female.setLayerWeight(0f, layerBlendCalc.layerSrc.body);
				}
				if (layerBlendCalc.layerDst.body != -1)
				{
					female.setLayerWeight(1f, layerBlendCalc.layerDst.body);
				}
				if ((bool)layerBlendCalc.anim)
				{
					if (layerBlendCalc.layerSrc.item != -1)
					{
						layerBlendCalc.anim.SetLayerWeight(layerBlendCalc.layerSrc.item, 0f);
					}
					if (layerBlendCalc.layerDst.item != -1)
					{
						layerBlendCalc.anim.SetLayerWeight(layerBlendCalc.layerDst.item, 1f);
					}
				}
			}
			layerBlendCalc.anim = _anim;
			layerBlendCalc.layerSrc = _layerSrc;
			layerBlendCalc.layerDst = _layerDst;
			layerBlendCalc.blend.Start(0f, 1f, _timeBlend);
			layerBlendCalc.isBlend = true;
			return true;
		}

		public bool Proc()
		{
			if (!female)
			{
				return false;
			}
			LayerBlendCalc[] array = blends;
			foreach (LayerBlendCalc layerBlendCalc in array)
			{
				if (!layerBlendCalc.isBlend)
				{
					continue;
				}
				float _ans = 0f;
				layerBlendCalc.blend.Proc(ref _ans);
				if (layerBlendCalc.layerSrc.body != -1)
				{
					female.setLayerWeight(1f - _ans, layerBlendCalc.layerSrc.body);
				}
				if (layerBlendCalc.layerDst.body != -1)
				{
					female.setLayerWeight(_ans, layerBlendCalc.layerDst.body);
				}
				if ((bool)layerBlendCalc.anim)
				{
					if (layerBlendCalc.layerSrc.item != -1)
					{
						layerBlendCalc.anim.SetLayerWeight(layerBlendCalc.layerSrc.item, 1f - _ans);
					}
					if (layerBlendCalc.layerDst.item != -1)
					{
						layerBlendCalc.anim.SetLayerWeight(layerBlendCalc.layerDst.item, _ans);
					}
				}
				layerBlendCalc.isBlend = layerBlendCalc.blend.IsBlend();
			}
			return true;
		}

		public bool ForceStop(int _arrayItem = -1)
		{
			if (!female)
			{
				return false;
			}
			if (_arrayItem != -1)
			{
				LayerBlendCalc layerBlendCalc = blends[_arrayItem];
				layerBlendCalc.isBlend = false;
				if (layerBlendCalc.layerSrc.body != -1)
				{
					female.setLayerWeight(0f, layerBlendCalc.layerSrc.body);
				}
				if (layerBlendCalc.layerDst.body != -1)
				{
					female.setLayerWeight(1f, layerBlendCalc.layerDst.body);
				}
				if ((bool)layerBlendCalc.anim && layerBlendCalc.anim.isActiveAndEnabled)
				{
					if (layerBlendCalc.layerSrc.item != -1)
					{
						layerBlendCalc.anim.SetLayerWeight(layerBlendCalc.layerSrc.item, 0f);
					}
					if (layerBlendCalc.layerDst.item != -1)
					{
						layerBlendCalc.anim.SetLayerWeight(layerBlendCalc.layerDst.item, 1f);
					}
				}
			}
			else
			{
				LayerBlendCalc[] array = blends;
				foreach (LayerBlendCalc layerBlendCalc2 in array)
				{
					layerBlendCalc2.isBlend = false;
					if (layerBlendCalc2.layerSrc.body != -1)
					{
						female.setLayerWeight(0f, layerBlendCalc2.layerSrc.body);
					}
					if (layerBlendCalc2.layerDst.body != -1)
					{
						female.setLayerWeight(1f, layerBlendCalc2.layerDst.body);
					}
					if ((bool)layerBlendCalc2.anim && layerBlendCalc2.anim.isActiveAndEnabled)
					{
						if (layerBlendCalc2.layerSrc.item != -1)
						{
							layerBlendCalc2.anim.SetLayerWeight(layerBlendCalc2.layerSrc.item, 0f);
						}
						if (layerBlendCalc2.layerDst.item != -1)
						{
							layerBlendCalc2.anim.SetLayerWeight(layerBlendCalc2.layerDst.item, 1f);
						}
					}
				}
			}
			return true;
		}

		public bool ForceStopFlagOnly()
		{
			LayerBlendCalc[] array = blends;
			foreach (LayerBlendCalc obj in array)
			{
				obj.isBlend = false;
				obj.anim = null;
				obj.layerSrc.body = -1;
				obj.layerSrc.item = -1;
				obj.layerDst.body = -1;
				obj.layerDst.item = -1;
			}
			return true;
		}
	}

	public HFlag flags;

	public HSprite sprite;

	public YureCtrl yure;

	public HVoiceCtrl voice;

	public HandAction action;

	public List<int> lstTouchHistory = new List<int>();

	public GameObject[] objReferences = new GameObject[6];

	public MotionEnableState nowMES;

	public Dictionary<string, MotionEnableState> dicMES = new Dictionary<string, MotionEnableState>();

	public HitReaction hitReaction;

	private ChaControl female;

	private Dictionary<int, AibuItem> dicItem = new Dictionary<int, AibuItem>();

	private AibuItem[] useItems = new AibuItem[3];

	private AibuItem[] useAreaItems = new AibuItem[6];

	private int[] areaItem = new int[6];

	private int actionUseItem = -1;

	private Ctrl ctrl;

	private bool oneMoreLoop;

	private Vector3 xyKiss = Vector3.zero;

	private AibuColliderKind selectKindTouch;

	private Dictionary<string, Dictionary<int, ReactionInfo>> dicReaction = new Dictionary<string, Dictionary<int, ReactionInfo>>();

	private Dictionary<int, ReactionInfo> dicNowReaction = new Dictionary<int, ReactionInfo>();

	private List<int> lstIKEffectLateUpdate = new List<int>();

	private bool isKiss;

	private int voicePlayClickCount;

	private int voicePlayClickLoop = 10;

	[SerializeField]
	private float voicePlayActionMove;

	[SerializeField]
	private int voicePlayActionLoop = 500;

	private float voicePlayActionMoveOld;

	private int actionPlayClickCount;

	private int actionPlayClickLoop = 10;

	private float actionPlayActionMove;

	private int actionPlayActionLoop = 100;

	private float actionPlayActionMoveOld;

	private Dictionary<string, Transform> dicTransSEs = new Dictionary<string, Transform>
	{
		{ "muneL", null },
		{ "muneR", null },
		{ "kokan", null },
		{ "anal", null },
		{ "siriL", null },
		{ "siriR", null }
	};

	private KissCameraInfo infoKissCamera = new KissCameraInfo();

	private float oldHandNormalizeTime;

	private bool isClickDragVoice;

	private int numFemale;

	private List<Collider> lstHitReactionCollider = new List<Collider>();

	[SerializeField]
	private Texture2D[] cursorIcon = new Texture2D[3];

	private int oldCursor;

	private static List<AibuIcon> lstIcon = new List<AibuIcon>();

	private static Dictionary<int, LayerInfo>[] dicAreaLayerInfos = new Dictionary<int, LayerInfo>[6];

	private LayerBlend blend = new LayerBlend();

	private Vector2 calcDragLength;

	public AibuColliderKind SelectKindTouch => selectKindTouch;

	private void Awake()
	{
		action = HandAction.none;
		cursorIcon = new Texture2D[3];
		base.enabled = false;
	}

	private void Update()
	{
		foreach (KeyValuePair<int, AibuItem> item in dicItem)
		{
			item.Value.SilhouetteColor();
			item.Value.Visible();
		}
		blend.Proc();
	}

	private void OnDestroy()
	{
		for (int i = 2; i < objReferences.Length; i++)
		{
			if (!(objReferences[i] == null))
			{
				UnityEngine.Object.Destroy(objReferences[i]);
				objReferences[i] = null;
			}
		}
		for (int j = 0; j < cursorIcon.Length; j++)
		{
			cursorIcon[j] = null;
		}
		cursorIcon = null;
	}

	public static void LoadInitList(HFlag flag)
	{
		if (HFlag.lstABName.Count == 0)
		{
			HFlag.lstABName = CommonLib.GetAssetBundleNameListFromPath("h/list/");
			HFlag.lstABName.Sort();
		}
		LoadIconTexture(flag);
		LoadLayerInfo();
	}

	public void LoadInit()
	{
		LoadItemObject();
		base.enabled = true;
	}

	private bool LoadItemObject()
	{
		UnityEngine.SceneManagement.Scene sceneByName = SceneManager.GetSceneByName("HTest");
		sceneByName = ((sceneByName.name == "HTest") ? sceneByName : SceneManager.GetSceneByName("HProc"));
		string text = "AibuItemObject";
		foreach (string item in HFlag.lstABName)
		{
			if (!AssetBundleCheck.IsFile(item, text) || (!AssetBundleCheck.IsSimulation && !HFlag.BundleCheck(item, text)))
			{
				continue;
			}
			AibuItemObjectData aibuItemObjectData = CommonLib.LoadAsset<AibuItemObjectData>(item, text, clone: false, "", check: false);
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: true);
			if (aibuItemObjectData == null)
			{
				continue;
			}
			foreach (AibuItemObjectData.Param item2 in aibuItemObjectData.param)
			{
				if (!dicItem.TryGetValue(item2.id, out var value))
				{
					dicItem.Add(item2.id, new AibuItem());
					value = dicItem[item2.id];
				}
				value.SetID(item2.id);
				value.SetObj(CommonLib.LoadAsset<GameObject>(item2.bundle, item2.asset, clone: true, item2.manifest));
				flags.hashAssetBundle.Add(item2.bundle);
				if (!item2.nomalObj.IsNullOrEmpty())
				{
					value.objBody = value.obj.transform.FindLoop(item2.nomalObj);
					if ((bool)value.objBody)
					{
						value.renderBody = value.objBody.GetComponent<SkinnedMeshRenderer>();
						value.meshRenderBody = value.objBody.GetComponent<MeshRenderer>();
						if (item2.isMaterialGet)
						{
							if (value.renderBody != null)
							{
								value.mHand = value.renderBody.material;
							}
							else if (value.meshRenderBody != null)
							{
								value.mHand = value.meshRenderBody.material;
							}
						}
					}
				}
				value.isSilhouetteChange = item2.isSilhouetteChange;
				if (!item2.objSilhouette.IsNullOrEmpty())
				{
					value.objSilhouette = value.obj.transform.FindLoop(item2.objSilhouette);
					if ((bool)value.objSilhouette)
					{
						value.renderSilhouette = value.objSilhouette.GetComponent<SkinnedMeshRenderer>();
						value.meshRenderSilhouette = value.objSilhouette.GetComponent<MeshRenderer>();
						if (value.renderSilhouette != null)
						{
							value.mSilhouette = value.renderSilhouette.material;
						}
						else if (value.meshRenderSilhouette != null)
						{
							value.mSilhouette = value.meshRenderSilhouette.material;
						}
					}
				}
				value.SetIdObj(item2.objKind);
				value.SetIdUse(item2.setKind);
				if (value.obj != null)
				{
					EliminateScale[] componentsInChildren = value.obj.GetComponentsInChildren<EliminateScale>(includeInactive: true);
					if (componentsInChildren != null && componentsInChildren.Length != 0)
					{
						componentsInChildren[componentsInChildren.Length - 1].LoadList(value.id);
					}
					value.SetAnm(value.obj.GetComponent<Animator>());
					value.obj.SetActive(value: false);
				}
				value.pathSEAsset = item2.bundleSE;
				value.nameSEFile = item2.nameSE;
				value.saveID = item2.saveID;
				value.isVirgin = item2.isVirgin;
				SceneManager.MoveGameObjectToScene(value.obj, sceneByName);
			}
		}
		return true;
	}

	private static bool LoadIconTexture(HFlag flags)
	{
		lstIcon = new List<AibuIcon>();
		string text = "AibuIconTexture";
		foreach (string item in HFlag.lstABName)
		{
			if (!AssetBundleCheck.IsFile(item, text) || (!AssetBundleCheck.IsSimulation && !HFlag.BundleCheck(item, text)))
			{
				continue;
			}
			AibuItconData aibuItconData = CommonLib.LoadAsset<AibuItconData>(item, text, clone: false, "", check: false);
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: true);
			if (aibuItconData == null)
			{
				continue;
			}
			foreach (AibuItconData.Param param in aibuItconData.param)
			{
				AibuIcon aibuIcon = lstIcon.Find((AibuIcon i) => i.id == param.id);
				if (aibuIcon == null)
				{
					lstIcon.Add(new AibuIcon());
					aibuIcon = lstIcon[lstIcon.Count - 1];
				}
				aibuIcon.id = param.id;
				aibuIcon.texIcons[0] = CommonLib.LoadAsset<Texture2D>(param.IconPathS, param.IconS);
				aibuIcon.texIcons[1] = CommonLib.LoadAsset<Texture2D>(param.IconPathM, param.IconM);
				aibuIcon.texIcons[2] = CommonLib.LoadAsset<Texture2D>(param.IconPathL, param.IconL);
				flags.hashAssetBundle.Add(item);
			}
		}
		return true;
	}

	private static bool LoadLayerInfo()
	{
		string value = "aibulayer_";
		StringBuilder stringBuilder = new StringBuilder();
		dicAreaLayerInfos = new Dictionary<int, LayerInfo>[6];
		for (int i = 0; i < dicAreaLayerInfos.Length; i++)
		{
			dicAreaLayerInfos[i] = new Dictionary<int, LayerInfo>();
			stringBuilder.Clear();
			stringBuilder.Append(value).AppendFormat("{0:00}", i);
			foreach (string item in HFlag.lstABName)
			{
				if (!AssetBundleCheck.IsFile(item, stringBuilder.ToString()) || (!AssetBundleCheck.IsSimulation && !HFlag.BundleCheck(item, stringBuilder.ToString())))
				{
					continue;
				}
				AibuLayerData aibuLayerData = CommonLib.LoadAsset<AibuLayerData>(item, stringBuilder.ToString(), clone: false, "", check: false);
				AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: true);
				if (aibuLayerData == null)
				{
					continue;
				}
				foreach (AibuLayerData.Param item2 in aibuLayerData.param)
				{
					if (!dicAreaLayerInfos[i].TryGetValue(item2.id, out var value2))
					{
						dicAreaLayerInfos[i].Add(item2.id, new LayerInfo());
						value2 = dicAreaLayerInfos[i][item2.id];
					}
					value2.id = item2.id;
					value2.idIocn = item2.iconId;
					value2.useArray = item2.useKind;
					value2.idVisible = item2.idVisible;
					for (int j = 0; j < item2.idObjects.Length; j++)
					{
						value2.idObjects[j] = item2.idObjects[j];
					}
					for (int k = 0; k < item2.nameParents.Length; k++)
					{
						value2.nameParents[k] = item2.nameParents[k];
					}
					for (int l = 0; l < item2.plays.Length; l++)
					{
						value2.plays[l] = item2.plays[l];
					}
					value2.actionClick = item2.actionClick;
					value2.pathClickSE.asset = item2.ClickSE.asset;
					value2.pathClickSE.file = item2.ClickSE.file;
					value2.actionDrag = item2.actionDrag;
					value2.pathDragSE.asset = item2.DragSE.asset;
					value2.pathDragSE.file = item2.DragSE.file;
					for (int m = 0; m < 3; m++)
					{
						value2.layerActions[m].dynamicArea = item2.layerActions[m].dynamicArea;
						value2.layerActions[m].enableDynamic = item2.layerActions[m].enableDynamic;
						value2.layerActions[m].front.item = item2.layerActions[m].front.item;
						value2.layerActions[m].front.body = item2.layerActions[m].front.body;
						value2.layerActions[m].back.item = item2.layerActions[m].back.item;
						value2.layerActions[m].back.body = item2.layerActions[m].back.body;
					}
				}
			}
		}
		return true;
	}

	public bool LoadMotionEnableState(string _file)
	{
		dicMES.Clear();
		if (_file == "")
		{
			return false;
		}
		foreach (AibuEnableState item in GlobalMethod.LoadAllFolder<AibuEnableState>("h/list/", _file))
		{
			foreach (AibuEnableState.Param item2 in item.param)
			{
				if (!dicMES.TryGetValue(item2.AnimationName, out var value))
				{
					dicMES.Add(item2.AnimationName, new MotionEnableState());
					value = dicMES[item2.AnimationName];
				}
				value.nameAnimation = item2.AnimationName;
				bool[] array = new bool[7] { item2.Mouth, item2.Bust_L, item2.Bust_R, item2.Kokan, item2.Anal, item2.Hip_L, item2.Hip_R };
				for (int i = 0; i < value.isTouchAreas.Length; i++)
				{
					value.isTouchAreas[i] = array[i];
				}
				value.stateMotion = item2.MotionState;
			}
		}
		return true;
	}

	public bool LoadReactionList(string _file)
	{
		dicReaction.Clear();
		if (_file == null || _file == "")
		{
			return false;
		}
		foreach (string item in HFlag.lstABName)
		{
			if (!AssetBundleCheck.IsFile(item, _file) || (!AssetBundleCheck.IsSimulation && !HFlag.BundleCheck(item, _file)))
			{
				continue;
			}
			ReactionData reactionData = CommonLib.LoadAsset<ReactionData>(item, _file, clone: false, "", check: false);
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: true);
			if (reactionData == null)
			{
				continue;
			}
			foreach (ReactionData.Param item2 in reactionData.param)
			{
				if (!dicReaction.TryGetValue(item2.anim, out var value))
				{
					dicReaction.Add(item2.anim, new Dictionary<int, ReactionInfo>());
					value = dicReaction[item2.anim];
				}
				if (!value.TryGetValue(item2.areaID, out var value2))
				{
					value.Add(item2.areaID, new ReactionInfo());
					value2 = value[item2.areaID];
				}
				value2.isPlay = true;
				value2.weight = item2.weight;
				value2.lstReleaseEffector.Clear();
				value2.lstReleaseEffector.AddRange(item2.lstReleaseEffector);
				value2.lstParam.Clear();
				foreach (ReactionData.ReactionParam item3 in item2.lstParam)
				{
					value2.lstParam.Add(new ReactionParam());
					ReactionParam reactionParam = value2.lstParam[value2.lstParam.Count - 1];
					reactionParam.id = item3.id;
					foreach (ReactionData.ReactionMinMax item4 in item3.lstMinMax)
					{
						reactionParam.lstMinMax.Add(new ReactionMinMax
						{
							min = item4.min,
							max = item4.max
						});
					}
				}
			}
		}
		return true;
	}

	public bool SetAnimation(string _animation)
	{
		SetEnableChangeForAnimation(_animation);
		SetReactionListForAnimation(_animation);
		DetachForAnimation();
		for (int i = 0; i < useItems.Length; i++)
		{
			if (useItems[i] != null)
			{
				EnableShape(useItems[i].kindTouch, _enable: true);
			}
		}
		return false;
	}

	private bool SetEnableChangeForAnimation(string _animation)
	{
		if (dicMES.TryGetValue(_animation, out nowMES))
		{
			return true;
		}
		nowMES = new MotionEnableState();
		return false;
	}

	private bool DetachForAnimation()
	{
		for (int i = 1; i < nowMES.isTouchAreas.Length; i++)
		{
			if (!nowMES.isTouchAreas[i])
			{
				DetachItemByUseAreaItem(i - 1);
			}
		}
		return false;
	}

	private bool SetReactionListForAnimation(string _animation)
	{
		if (dicReaction.TryGetValue(_animation, out dicNowReaction))
		{
			return true;
		}
		dicNowReaction = new Dictionary<int, ReactionInfo>();
		return false;
	}

	public bool SetItemAnimatorParam()
	{
		if (!female)
		{
			return false;
		}
		AibuItem[] array = useItems;
		foreach (AibuItem aibuItem in array)
		{
			if (aibuItem != null && !(aibuItem.anm == null))
			{
				aibuItem.anm.SetFloat("Breast", female.GetShapeBodyValue(4));
				aibuItem.anm.SetFloat("speedHand", 1f + flags.speedItem);
				aibuItem.anm.SetFloat("muneL_X", flags.xy[0].x);
				aibuItem.anm.SetFloat("muneL_Y", flags.xy[0].y);
				aibuItem.anm.SetFloat("muneR_X", flags.xy[1].x);
				aibuItem.anm.SetFloat("muneR_Y", flags.xy[1].y);
				aibuItem.anm.SetFloat("kokan_X", flags.xy[2].x);
				aibuItem.anm.SetFloat("kokan_Y", flags.xy[2].y);
				aibuItem.anm.SetFloat("anal_X", flags.xy[3].x);
				aibuItem.anm.SetFloat("anal_Y", flags.xy[3].y);
				aibuItem.anm.SetFloat("siriL_X", flags.xy[4].x);
				aibuItem.anm.SetFloat("siriL_Y", flags.xy[4].y);
				aibuItem.anm.SetFloat("siriR_X", flags.xy[5].x);
				aibuItem.anm.SetFloat("siriR_Y", flags.xy[5].y);
			}
		}
		return true;
	}

	public bool Init(ChaControl _female, Color _colorMale, int _numFemale)
	{
		female = _female;
		numFemale = _numFemale;
		blend.female = _female;
		if (!female)
		{
			return false;
		}
		Dictionary<int, LayerInfo>[] array = dicAreaLayerInfos;
		for (int i = 0; i < array.Length; i++)
		{
			foreach (LayerInfo value in array[i].Values)
			{
				for (int j = 0; j < 2; j++)
				{
					GameObject gameObject = female.objBodyBone.transform.FindLoop(value.nameParents[j]);
					value.transParents[j] = (gameObject ? gameObject.transform : null);
				}
			}
		}
		string[] array2 = new string[6] { "cf_d_bust01_L", "cf_d_bust01_R", "cf_d_kokan", "cf_d_ana", "cf_d_siri01_L", "cf_d_siri01_R" };
		Quaternion[] array3 = new Quaternion[6]
		{
			Quaternion.Euler(0f, 180f, 0f),
			Quaternion.Euler(0f, 180f, 0f),
			Quaternion.Euler(270f, 180f, 0f),
			Quaternion.Euler(270f, 0f, 0f),
			Quaternion.Euler(0f, 0f, 0f),
			Quaternion.Euler(0f, 0f, 0f)
		};
		for (int k = 0; k < objReferences.Length; k++)
		{
			objReferences[k] = new GameObject
			{
				name = array2[k] + "_REF"
			};
			objReferences[k].transform.localRotation *= array3[k];
			GameObject gameObject2 = female.objBodyBone.transform.FindLoop(array2[k]);
			if ((bool)gameObject2)
			{
				objReferences[k].transform.SetParent(gameObject2.transform, worldPositionStays: false);
			}
		}
		foreach (KeyValuePair<int, AibuItem> item in dicItem)
		{
			item.Value.SetHandColor(_colorMale);
		}
		return true;
	}

	public bool InitHitReactionCollider()
	{
		if (!female)
		{
			return false;
		}
		lstHitReactionCollider = (from c in female.GetComponentsInChildren<Collider>(includeInactive: true)
			where c.tag.Contains("H/Aibu/Hit/")
			select c).ToList();
		return true;
	}

	public bool IsUseAreaItemActive(int _area)
	{
		if (useAreaItems.Length <= _area)
		{
			return false;
		}
		return useAreaItems[_area] != null;
	}

	public bool IsItemTouch()
	{
		for (int i = 0; i < useItems.Length; i++)
		{
			if (useItems[i] != null)
			{
				return true;
			}
		}
		return false;
	}

	public List<int> GetUseItemNumber()
	{
		List<int> list = new List<int>();
		for (int i = 0; i < useItems.Length; i++)
		{
			if (useItems[i] != null)
			{
				list.Add(i);
			}
		}
		return list;
	}

	public AibuColliderKind GetUseItemStickArea(int _array)
	{
		if (useItems.Length <= _array || 0 > _array)
		{
			return AibuColliderKind.none;
		}
		if (useItems[_array] == null)
		{
			return AibuColliderKind.none;
		}
		return useItems[_array].kindTouch;
	}

	public int GetUseItemStickObjectID(int _array)
	{
		if (useItems.Length <= _array || 0 > _array)
		{
			return -1;
		}
		if (useItems[_array] == null)
		{
			return -1;
		}
		return useItems[_array].idObj;
	}

	public int GetUseAreaItemActive()
	{
		return actionUseItem;
	}

	public int GetTouchHistoryAt(int _array)
	{
		if (lstTouchHistory.Count <= _array)
		{
			return -1;
		}
		return lstTouchHistory[_array];
	}

	public bool IsKissAction()
	{
		return isKiss;
	}

	public bool IsAction()
	{
		return action == HandAction.action;
	}

	public bool Proc(bool _isNotHitCollider = false)
	{
		if (!female)
		{
			return false;
		}
		if (Manager.Scene.NowSceneNames[0] != "HProc" && Manager.Scene.NowSceneNames[0] != "HTest")
		{
			return false;
		}
		switch (action)
		{
		case HandAction.none:
			if (!_isNotHitCollider)
			{
				SetIconTexture(ref selectKindTouch);
			}
			OnCollision();
			break;
		}
		return true;
	}

	public bool LateProc()
	{
		if (!female)
		{
			return false;
		}
		if (Manager.Scene.NowSceneNames[0] != "HProc" && Manager.Scene.NowSceneNames[0] != "HTest")
		{
			return false;
		}
		if (lstIKEffectLateUpdate.Count != 0)
		{
			hitReaction.ReleaseEffector();
			hitReaction.SetEffector(lstIKEffectLateUpdate);
			lstIKEffectLateUpdate.Clear();
		}
		switch (action)
		{
		case HandAction.none:
			if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl))
			{
				selectKindTouch = AibuColliderKind.none;
			}
			else if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
			{
				HitReaction();
				if (Mathf.Abs(Input.GetAxis("Mouse X")) > 0.01f || Mathf.Abs(Input.GetAxis("Mouse Y")) > 0.01f)
				{
					selectKindTouch = GetOnMouseAibuCollider();
				}
			}
			break;
		case HandAction.judge:
			JudgeProc();
			break;
		case HandAction.action:
			ActionProc();
			break;
		}
		if (flags.selectAnimationListInfo != null)
		{
			selectKindTouch = AibuColliderKind.none;
			SetIconTexture(ref selectKindTouch);
		}
		return true;
	}

	public bool OnCollision()
	{
		if (!female)
		{
			return false;
		}
		if (MathfEx.IsRange(AibuColliderKind.muneL, selectKindTouch, AibuColliderKind.siriR, isEqual: true))
		{
			int num = (int)(selectKindTouch - 2);
			LayerInfo layerInfo = dicAreaLayerInfos[num][areaItem[num]];
			if (Input.GetMouseButtonDown(0))
			{
				bool isFront = flags.nowAnimationInfo.paramFemale.lstFrontAndBehind[0] == 0;
				int useArray = layerInfo.useArray;
				if (useAreaItems[num] == null)
				{
					int num2 = IsDontTouchAnalAndMassager(selectKindTouch, layerInfo);
					if (num2 != -1 && !flags.isDebug)
					{
						if (num2 == 0)
						{
							flags.AddDontTouchAnal();
							flags.SetSelectArea(3, 0.1f, _isCheck: true);
							flags.click = HFlag.ClickKind.anal_dislikes;
							flags.voice.playShorts[numFemale] = num;
							flags.voice.isShortsPlayTouchWeak[numFemale] = false;
						}
						else
						{
							flags.AddDontTouchMassage();
							flags.AddSelectMassage(0.1f, (selectKindTouch != AibuColliderKind.kokan) ? 1 : 0, _isCheck: true);
							flags.click = ((num2 == 1) ? HFlag.ClickKind.massage_mune_dislikes : HFlag.ClickKind.massage_kokan_dislikes);
							flags.voice.playShorts[numFemale] = num;
							flags.voice.isShortsPlayTouchWeak[numFemale] = false;
						}
					}
					else
					{
						if (useArray != 3)
						{
							while (useItems[useArray] != null)
							{
								AibuColliderKind kindTouch = useItems[useArray].kindTouch;
								int arrayArea = (int)(kindTouch - 2);
								LayerInfo layer = useItems[useArray].layer;
								SetShapeON(useItems[useArray].kindTouch);
								DetachItem(useArray, (int)(useItems[useArray].kindTouch - 2));
								if (useArray == 2 || (kindTouch != AibuColliderKind.kokan && kindTouch != AibuColliderKind.anal) || useItems[useArray ^ 1] != null)
								{
									break;
								}
								SetItem(useArray ^ 1, arrayArea, layer, useArray ^ 1, kindTouch, isFront);
							}
							SetItem(useArray, num, layerInfo, 0, selectKindTouch, isFront);
						}
						else if (useItems[0] != null && useItems[1] != null)
						{
							int num3 = ((GetTouchHistoryAt(0) == 0) ? 1 : 0);
							SetShapeON(useItems[num3].kindTouch);
							DetachItem(num3, (int)(useItems[num3].kindTouch - 2));
							SetItem(num3, num, layerInfo, num3, selectKindTouch, isFront);
						}
						else
						{
							for (int i = 0; i < 2; i++)
							{
								if (useItems[i] == null)
								{
									SetItem(dicItem[layerInfo.idObjects[i]].idUse, num, layerInfo, i, selectKindTouch, isFront);
									break;
								}
							}
						}
						StartCoroutine(EnableShapeCoroutine(selectKindTouch));
						AnimatrotRestrart();
						flags.click = (HFlag.ClickKind)(16 + num);
						if (flags.IsAreaTouchAll() == 2)
						{
							flags.voice.playVoices[numFemale] = 145;
						}
						flags.voice.playShorts[numFemale] = num;
						int[] array = new int[6] { 1, 1, 2, 3, 4, 4 };
						bool flag = flags.lstHeroine[0].weakPoint == array[num];
						if (!flag)
						{
							flag = num == 0 && useAreaItems[num].idObj != 0 && flags.lstHeroine[0].weakPoint == 5;
						}
						flags.voice.isShortsPlayTouchWeak[numFemale] = flag;
						if (flags.mode != HFlag.EMode.aibu)
						{
							HitReactionPlay((num < 2) ? AibuColliderKind.reac_bodyup : AibuColliderKind.reac_bodydown, _playShort: false);
						}
						flags.DragStart();
						action = HandAction.judge;
						actionUseItem = useAreaItems[num].idUse;
						GlobalMethod.SetCameraMoveFlag(flags.ctrlCamera, _bPlay: false);
					}
				}
				else
				{
					flags.DragStart();
					action = HandAction.judge;
					actionUseItem = useAreaItems[num].idUse;
					GlobalMethod.SetCameraMoveFlag(flags.ctrlCamera, _bPlay: false);
					int num4 = (int)(useItems[actionUseItem].kindTouch - 1);
					int[] array2 = new int[7] { 0, 1, 1, 2, 3, 4, 4 };
					if (flags.mode == HFlag.EMode.aibu)
					{
						int[,] array3 = new int[10, 6]
						{
							{ -1, 111, 113, 115, 117, -1 },
							{ -1, 123, 119, 121, -1, -1 },
							{ -1, 131, 125, 127, 129, -1 },
							{ -1, 137, 133, -1, 135, -1 },
							{ -1, -1, 139, -1, -1, -1 },
							{ -1, -1, 146, -1, -1, -1 },
							{ -1, 150, 148, -1, -1, -1 },
							{ -1, -1, -1, -1, -1, -1 },
							{ -1, -1, -1, -1, -1, -1 },
							{ -1, -1, -1, -1, -1, -1 }
						};
						if (voice.nowVoices[numFemale].state == HVoiceCtrl.VoiceKind.breath)
						{
							flags.voice.playVoices[numFemale] = array3[useItems[actionUseItem].idObj, array2[num4]];
						}
						sprite.imageSpeedSlliderCover70.enabled = !flags.lstHeroine[0].denial.aibu && GetAreaExperience(selectKindTouch) == 0;
					}
					else
					{
						flags.voice.playShorts[numFemale] = num;
						int[] array4 = new int[6] { 1, 1, 2, 3, 4, 4 };
						bool flag2 = flags.lstHeroine[0].weakPoint == array4[num];
						if (!flag2)
						{
							flag2 = num == 0 && useAreaItems[num].idObj != 0 && flags.lstHeroine[0].weakPoint == 5;
						}
						flags.voice.isShortsPlayTouchWeak[numFemale] = flag2;
					}
				}
			}
			else if (Input.GetMouseButtonDown(1) && useAreaItems[num] != null)
			{
				SetShapeON(selectKindTouch);
				DetachItem(useAreaItems[num].idUse, num);
				flags.click = (HFlag.ClickKind)(22 + num);
			}
		}
		else if (selectKindTouch == AibuColliderKind.mouth && Input.GetMouseButtonDown(0))
		{
			if (!flags.isDebug && !flags.lstHeroine[0].isGirlfriend && !flags.lstHeroine[0].isKiss && !flags.lstHeroine[0].denial.kiss)
			{
				flags.AddNotKiss();
				if (flags.mode == HFlag.EMode.aibu)
				{
					flags.voice.playVoices[numFemale] = 103;
				}
				return true;
			}
			HSceneProc.FemaleParameter paramFemale = flags.nowAnimationInfo.paramFemale;
			SetDragStartLayer(paramFemale.lstFrontAndBehind[0] == 0);
			xyKiss = Vector3.zero;
			flags.click = HFlag.ClickKind.mouth;
			action = HandAction.action;
			ctrl = Ctrl.drag;
			GlobalMethod.SetCameraMoveFlag(flags.ctrlCamera, _bPlay: false);
			flags.ctrlCamera.isCursorLock = false;
			Singleton<GameCursor>.Instance.SetCursorLock(setLockFlag: true);
			voicePlayClickCount = 0;
			voicePlayActionMove = 0f;
			voicePlayClickLoop = 10;
			voicePlayActionLoop = 500;
			voicePlayActionMoveOld = 0f;
			actionPlayClickCount = 0;
			actionPlayActionMove = 0f;
			actionPlayClickLoop = 5;
			actionPlayActionLoop = 0;
			actionPlayActionMoveOld = 0f;
			oldHandNormalizeTime = 0f;
			isClickDragVoice = false;
			isKiss = true;
			if (!flags.isDebug && flags.mode == HFlag.EMode.aibu)
			{
				infoKissCamera.dataCam = flags.ctrlCamera.GetCameraData();
				flags.ctrlCamera.GetWorldBase(ref infoKissCamera.oldPos, ref infoKissCamera.oldRot);
				flags.ctrlCamera.SetWorldBase(female.objTop.transform);
				flags.ctrlCamera.SetCameraData(KissCameraDataLoad("h/list/", flags.nowAnimationInfo.nameCameraKiss, female.GetShapeBodyValue(0)));
				flags.ctrlCamera.GetComponent<CameraEffectorConfig>().useDOF = false;
				flags.ctrlCamera.GetComponent<CameraEffector>().dof.enabled = false;
			}
			flags.AddKiss();
		}
		return true;
	}

	private bool HitReaction()
	{
		if (!MathfEx.IsRange(AibuColliderKind.reac_head, selectKindTouch, AibuColliderKind.reac_legR, isEqual: true) || hitReaction == null)
		{
			return false;
		}
		if (!Input.GetMouseButtonDown(0))
		{
			return false;
		}
		Reaction(selectKindTouch);
		return true;
	}

	private bool Reaction(AibuColliderKind _kindTouch)
	{
		if (hitReaction.IsPlay())
		{
			return true;
		}
		HitReactionPlay(_kindTouch);
		return true;
	}

	private bool HitReactionPlay(AibuColliderKind _selectTouch, bool _playShort = true)
	{
		if (!female)
		{
			return false;
		}
		int key = (int)(_selectTouch - 8);
		if (!dicNowReaction.ContainsKey(key))
		{
			return false;
		}
		GameObject gameObject = female.gameObject;
		int index = UnityEngine.Random.Range(0, dicNowReaction[key].lstParam.Count);
		ReactionParam reactionParam = dicNowReaction[key].lstParam[index];
		Vector3[] array = new Vector3[reactionParam.lstMinMax.Count];
		for (int i = 0; i < reactionParam.lstMinMax.Count; i++)
		{
			array[i] = new Vector3(UnityEngine.Random.Range(reactionParam.lstMinMax[i].min.x, reactionParam.lstMinMax[i].max.x), UnityEngine.Random.Range(reactionParam.lstMinMax[i].min.y, reactionParam.lstMinMax[i].max.y), UnityEngine.Random.Range(reactionParam.lstMinMax[i].min.z, reactionParam.lstMinMax[i].max.z));
			array[i] = gameObject.transform.TransformDirection(array[i].normalized);
		}
		hitReaction.weight = dicNowReaction[key].weight;
		hitReaction.HitsEffector(reactionParam.id, array);
		lstIKEffectLateUpdate.AddRange(dicNowReaction[key].lstReleaseEffector);
		if (_playShort)
		{
			flags.voice.playShorts[numFemale] = 6;
			flags.voice.isShortsPlayTouchWeak[numFemale] = false;
		}
		return true;
	}

	public bool JudgeProc()
	{
		bool isFront = flags.nowAnimationInfo.paramFemale.lstFrontAndBehind[0] == 0;
		flags.SpeedUpClickItemAibu(flags.rateSpeedUpItem * 100f, flags.speedMaxAibuItem, _drag: false);
		if (flags.mode == HFlag.EMode.aibu)
		{
			flags.SpeedUpClickAibu(flags.rateSpeedUpAibu, flags.speedMaxAibuBody, _drag: false);
		}
		for (int i = 0; i < useItems.Length; i++)
		{
			if (useItems[i] != null)
			{
				SetLayerWeight(i, useItems[i].layer, isFront, 1);
				EnableDynamicBone(useItems[i].layer.layerActions[1]);
			}
		}
		AnimatrotRestrart();
		voicePlayClickCount = 0;
		voicePlayActionMove = 0f;
		voicePlayClickLoop = 10;
		voicePlayActionLoop = 500;
		voicePlayActionMoveOld = 0f;
		actionPlayClickCount = 0;
		actionPlayActionMove = 0f;
		actionPlayClickLoop = 5;
		actionPlayActionLoop = 100;
		actionPlayActionMoveOld = 0f;
		isClickDragVoice = false;
		ctrl = Ctrl.click;
		oneMoreLoop = false;
		action = HandAction.action;
		for (int j = 0; j < useItems.Length; j++)
		{
			if (useItems[j] != null)
			{
				string[] array = new string[6] { "muneL", "muneR", "kokan", "anal", "siriL", "siriR" };
				int num = (int)(useItems[j].kindTouch - 2);
				PlaySE(array[num], 1, useItems[j].obj.transform, useItems[j].layer.pathDragSE);
			}
		}
		return true;
	}

	public bool ActionProc()
	{
		if (ctrl == Ctrl.click)
		{
			ClickAction();
		}
		else if (ctrl == Ctrl.drag)
		{
			DragAction();
		}
		else
		{
			KissAction();
		}
		return true;
	}

	private void IsPointerOverUIObject()
	{
		PointerEventData pointerEventData = new PointerEventData(EventSystem.current);
		pointerEventData.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
		List<RaycastResult> raycastResults = new List<RaycastResult>();
		EventSystem.current.RaycastAll(pointerEventData, raycastResults);
	}

	private AibuColliderKind GetOnMouseAibuCollider()
	{
		List<RaycastHit> list = new List<RaycastHit>(Physics.RaycastAll(flags.ctrlCamera.thisCmaera.ScreenPointToRay(Input.mousePosition)));
		AibuColliderKind result = AibuColliderKind.none;
		if (EventSystem.current.IsPointerOverGameObject())
		{
			return result;
		}
		if (list.Count == 0 || !flags.isAibuSelect)
		{
			return result;
		}
		RaycastHit raycastHit = (from d in list
			where d.collider.tag.Contains("H/Aibu/Hit/")
			orderby d.distance
			select d).FirstOrDefault();
		if (raycastHit.collider == null)
		{
			return result;
		}
		if (!lstHitReactionCollider.Contains(raycastHit.collider))
		{
			return result;
		}
		switch (raycastHit.collider.tag)
		{
		case "H/Aibu/Hit/mouth":
			if (nowMES.isTouchAreas[0] && useItems[2] == null && (flags.mode == HFlag.EMode.aibu || flags.isDebug || flags.lstHeroine[0].isGirlfriend || flags.lstHeroine[0].isKiss || flags.lstHeroine[0].denial.kiss))
			{
				result = AibuColliderKind.mouth;
			}
			else if (dicNowReaction.ContainsKey(0) && dicNowReaction[0].isPlay)
			{
				result = AibuColliderKind.reac_head;
			}
			break;
		case "H/Aibu/Hit/muneL":
			if (nowMES.isTouchAreas[1])
			{
				result = AibuColliderKind.muneL;
			}
			else if (dicNowReaction.ContainsKey(1) && dicNowReaction[1].isPlay)
			{
				result = AibuColliderKind.reac_bodyup;
			}
			break;
		case "H/Aibu/Hit/muneR":
			if (nowMES.isTouchAreas[2])
			{
				result = AibuColliderKind.muneR;
			}
			else if (dicNowReaction.ContainsKey(1) && dicNowReaction[1].isPlay)
			{
				result = AibuColliderKind.reac_bodyup;
			}
			break;
		case "H/Aibu/Hit/kokan":
			if (nowMES.isTouchAreas[3])
			{
				result = AibuColliderKind.kokan;
			}
			else if (dicNowReaction.ContainsKey(2) && dicNowReaction[2].isPlay)
			{
				result = AibuColliderKind.reac_bodydown;
			}
			break;
		case "H/Aibu/Hit/anal":
			if (nowMES.isTouchAreas[4])
			{
				if (flags.mode == HFlag.EMode.sonyu || flags.mode == HFlag.EMode.houshi)
				{
					if (IsDontTouchAnalAndMassager(AibuColliderKind.anal, null) != 0 || flags.isDebug)
					{
						result = AibuColliderKind.anal;
					}
					else if (dicNowReaction.ContainsKey(2) && dicNowReaction[2].isPlay)
					{
						result = AibuColliderKind.reac_bodydown;
					}
				}
				else
				{
					result = AibuColliderKind.anal;
				}
			}
			else if (dicNowReaction.ContainsKey(2) && dicNowReaction[2].isPlay)
			{
				result = AibuColliderKind.reac_bodydown;
			}
			break;
		case "H/Aibu/Hit/siriL":
			if (nowMES.isTouchAreas[5])
			{
				result = AibuColliderKind.siriL;
			}
			else if (dicNowReaction.ContainsKey(2) && dicNowReaction[2].isPlay)
			{
				result = AibuColliderKind.reac_bodydown;
			}
			break;
		case "H/Aibu/Hit/siriR":
			if (nowMES.isTouchAreas[6])
			{
				result = AibuColliderKind.siriR;
			}
			else if (dicNowReaction.ContainsKey(2) && dicNowReaction[2].isPlay)
			{
				result = AibuColliderKind.reac_bodydown;
			}
			break;
		case "H/Aibu/Hit/Reaction/head":
			if (dicNowReaction.ContainsKey(0) && dicNowReaction[0].isPlay)
			{
				result = AibuColliderKind.reac_head;
			}
			break;
		case "H/Aibu/Hit/Reaction/bodyup":
			if (dicNowReaction.ContainsKey(1) && dicNowReaction[1].isPlay)
			{
				result = AibuColliderKind.reac_bodyup;
			}
			break;
		case "H/Aibu/Hit/Reaction/bodydown":
			if (dicNowReaction.ContainsKey(2) && dicNowReaction[2].isPlay)
			{
				result = AibuColliderKind.reac_bodydown;
			}
			break;
		case "H/Aibu/Hit/Reaction/armL":
			if (dicNowReaction.ContainsKey(3) && dicNowReaction[3].isPlay)
			{
				result = AibuColliderKind.reac_armL;
			}
			break;
		case "H/Aibu/Hit/Reaction/armR":
			if (dicNowReaction.ContainsKey(4) && dicNowReaction[4].isPlay)
			{
				result = AibuColliderKind.reac_armR;
			}
			break;
		case "H/Aibu/Hit/Reaction/legL":
			if (dicNowReaction.ContainsKey(5) && dicNowReaction[5].isPlay)
			{
				result = AibuColliderKind.reac_legL;
			}
			break;
		case "H/Aibu/Hit/Reaction/legR":
			if (dicNowReaction.ContainsKey(6) && dicNowReaction[6].isPlay)
			{
				result = AibuColliderKind.reac_legR;
			}
			break;
		}
		return result;
	}

	private bool SetIconTexture(ref AibuColliderKind _kindTouch)
	{
		if (MathfEx.IsRange(AibuColliderKind.muneL, _kindTouch, AibuColliderKind.siriR, isEqual: true))
		{
			bool isFront = flags.nowAnimationInfo.paramFemale.lstFrontAndBehind[0] == 0;
			int num = (int)(_kindTouch - 2);
			Dictionary<int, LayerInfo> dictionary = dicAreaLayerInfos[num];
			float axis = Input.GetAxis("Mouse ScrollWheel");
			int count = dictionary.Count;
			int num2 = areaItem[num];
			int cloth = GetClothState(_kindTouch);
			if (!dictionary.Any((KeyValuePair<int, LayerInfo> i) => i.Value.plays[cloth] != -1) || !nowMES.isTouchAreas[num + 1])
			{
				Singleton<GameCursor>.Instance.SetCursorTexture(-1);
				_kindTouch = AibuColliderKind.none;
				return true;
			}
			if (dictionary[num2].plays[cloth] == -1)
			{
				num2 = (areaItem[num] = 0);
			}
			do
			{
				if (axis < 0f)
				{
					areaItem[num] = GlobalMethod.Loop(areaItem[num] + 1, count);
				}
				else if (axis > 0f)
				{
					areaItem[num] = GlobalMethod.Loop(areaItem[num] - 1, count);
				}
				LayerInfo layerInfo = dictionary[areaItem[num]];
				if (layerInfo.plays[cloth] == -1)
				{
					continue;
				}
				if (flags.isDebug)
				{
					goto IL_028d;
				}
				WorldData saveData = Game.saveData;
				GlobalData globalData = Game.globalData;
				_ = globalData.buyList;
				HashSet<int> hashSet = (flags.isFreeH ? globalData.buyList : new HashSet<int>(saveData.player.buyNumTable.Keys));
				if (hashSet == null)
				{
					hashSet = new HashSet<int>();
				}
				int saveID = dicItem[layerInfo.idObjects[0]].saveID;
				if (saveID == -1)
				{
					goto IL_021b;
				}
				if (hashSet.Contains(saveID))
				{
					if (!dicItem[layerInfo.idObjects[0]].isVirgin || !flags.lstHeroine[0].isVirgin)
					{
						goto IL_021b;
					}
					if (axis == 0f)
					{
						areaItem[num] = GlobalMethod.Loop(areaItem[num] + 1, count);
					}
				}
				continue;
				IL_021b:
				if (flags.mode == HFlag.EMode.houshi || flags.mode == HFlag.EMode.sonyu)
				{
					switch (IsDontTouchAnalAndMassager(_kindTouch, layerInfo))
					{
					case 0:
						Singleton<GameCursor>.Instance.SetCursorTexture(-1);
						_kindTouch = AibuColliderKind.none;
						return true;
					case 1:
					case 2:
						if (axis == 0f)
						{
							areaItem[num] = GlobalMethod.Loop(areaItem[num] + 1, count);
						}
						continue;
					}
				}
				goto IL_028d;
				IL_028d:
				if (useAreaItems[num] == null)
				{
					break;
				}
				if ((layerInfo.idVisible != -1 && !flags.nowAnimationInfo.lstAibuSpecialItem.Contains(layerInfo.idVisible)) || (!flags.isDebug && IsDontTouchAnalAndMassager(_kindTouch, layerInfo) != -1))
				{
					continue;
				}
				int useArray = layerInfo.useArray;
				if (useArray != 3)
				{
					if (dicItem[layerInfo.idObjects[0]] == useAreaItems[num])
					{
						break;
					}
					if (useAreaItems[num].idUse == 2 && useItems[useArray] != null)
					{
						if ((useItems[useArray].kindTouch == AibuColliderKind.kokan || useItems[useArray].kindTouch == AibuColliderKind.anal) && useItems[useArray ^ 1] == null)
						{
							AibuColliderKind kindTouch = useItems[useArray].kindTouch;
							AibuItem aibuItem = useItems[useArray];
							DetachItem(useAreaItems[num].idUse, num);
							SetItem(dicItem[layerInfo.idObjects[0]].idUse, num, layerInfo, 0, _kindTouch, isFront, _isSetHistory: false);
							int num3 = (int)(kindTouch - 2);
							if (aibuItem != useItems[useAreaItems[num].idUse])
							{
								DetachItem(useAreaItems[num3].idUse, num3, aibuItem.idUse != useAreaItems[num].idUse);
							}
							LayerInfo layerInfo2 = dicAreaLayerInfos[num3][areaItem[num3]];
							SetItem(dicItem[layerInfo2.idObjects[useArray ^ 1]].idUse, num3, layerInfo2, useArray ^ 1, kindTouch, isFront);
							EnableShape(_kindTouch, _enable: true);
							break;
						}
					}
					else if (useArray != 2 || useItems[2] == null)
					{
						DetachItem(useAreaItems[num].idUse, num);
						SetItem(dicItem[layerInfo.idObjects[0]].idUse, num, layerInfo, 0, _kindTouch, isFront);
						EnableShape(_kindTouch, _enable: true);
						break;
					}
					continue;
				}
				if (useItems[0] == null && useItems[1] == null)
				{
					DetachItem(useAreaItems[num].idUse, num);
					SetItem(dicItem[layerInfo.idObjects[0]].idUse, num, layerInfo, 0, _kindTouch, isFront);
					EnableShape(_kindTouch, _enable: true);
					break;
				}
				if (dicItem[layerInfo.idObjects[0]] == useAreaItems[num] || dicItem[layerInfo.idObjects[1]] == useAreaItems[num])
				{
					break;
				}
				if (useItems[0] != null && useItems[0].kindTouch != _kindTouch && useItems[1] != null && useItems[1].kindTouch != _kindTouch)
				{
					continue;
				}
				for (int num4 = 0; num4 < 2; num4++)
				{
					if (useItems[num4] == null || useItems[num4].kindTouch == _kindTouch)
					{
						DetachItem(useAreaItems[num].idUse, num);
						SetItem(dicItem[layerInfo.idObjects[num4]].idUse, num, layerInfo, num4, _kindTouch, isFront);
						EnableShape(_kindTouch, _enable: true);
						break;
					}
				}
				break;
			}
			while (areaItem[num] != num2);
			int idIocn = dictionary[areaItem[num]].idIocn;
			int num5 = -1;
			for (int num6 = 0; num6 < lstIcon.Count; num6++)
			{
				if (lstIcon[num6].id == idIocn)
				{
					num5 = num6;
					break;
				}
			}
			if (num5 != -1 && oldCursor != idIocn)
			{
				cursorIcon = lstIcon[num5].texIcons;
				Singleton<GameCursor>.Instance.SetCursorTexture(idIocn, cursorIcon);
				oldCursor = idIocn;
			}
		}
		else if (_kindTouch == AibuColliderKind.mouth && nowMES.isTouchAreas[0] && useItems[2] == null)
		{
			if (oldCursor != 0)
			{
				cursorIcon = lstIcon[0].texIcons;
				Singleton<GameCursor>.Instance.SetCursorTexture(0, cursorIcon);
				oldCursor = 0;
			}
		}
		else if (MathfEx.IsRange(AibuColliderKind.reac_head, _kindTouch, AibuColliderKind.reac_legR, isEqual: true))
		{
			Singleton<GameCursor>.Instance.SetCursorTexture(-2);
			oldCursor = -2;
		}
		else
		{
			Singleton<GameCursor>.Instance.SetCursorTexture(-1);
			_kindTouch = AibuColliderKind.none;
			oldCursor = -1;
		}
		return true;
	}

	public bool DetachItem(int _arrayItem, int _arrayArea, bool _isDeleteUseItem = true)
	{
		if (!female)
		{
			return false;
		}
		SetLayerWeightDefault(_arrayItem);
		if (_isDeleteUseItem)
		{
			sprite.commonAibuIcons[_arrayItem].enabled = false;
			if ((bool)useItems[_arrayItem].transformSound)
			{
				Manager.Sound.Stop(useItems[_arrayItem].transformSound);
			}
			useItems[_arrayItem] = null;
		}
		AibuColliderKind kindTouch = useAreaItems[_arrayArea].kindTouch;
		useAreaItems[_arrayArea].kindTouch = AibuColliderKind.none;
		useAreaItems[_arrayArea].obj.SetActive(value: false);
		useAreaItems[_arrayArea].obj.transform.SetParent(null, worldPositionStays: false);
		UnityEngine.SceneManagement.Scene sceneByName = SceneManager.GetSceneByName("HTest");
		sceneByName = ((sceneByName.name == "HTest") ? sceneByName : SceneManager.GetSceneByName("HProc"));
		SceneManager.MoveGameObjectToScene(useAreaItems[_arrayArea].obj, sceneByName);
		useAreaItems[_arrayArea].layer = null;
		useAreaItems[_arrayArea] = null;
		HSceneProc.FemaleParameter paramFemale = flags.nowAnimationInfo.paramFemale;
		switch (kindTouch)
		{
		case AibuColliderKind.muneL:
			yure.Proc(flags.nowAnimStateName, !paramFemale.isYure, 0);
			break;
		case AibuColliderKind.muneR:
			yure.Proc(flags.nowAnimStateName, !paramFemale.isYure, 1);
			break;
		}
		return true;
	}

	public int DetachItemByUseItem(int _arrayItem)
	{
		if (!female)
		{
			return -1;
		}
		if (useItems[_arrayItem] == null)
		{
			return -1;
		}
		if (!MathfEx.IsRange(-1, _arrayItem, useItems.Length, isEqual: false))
		{
			return -1;
		}
		int num = (int)(useItems[_arrayItem].kindTouch - 2);
		SetShapeON(useItems[_arrayItem].kindTouch);
		SetLayerWeightDefault(_arrayItem);
		if ((bool)sprite.commonAibuIcons[_arrayItem])
		{
			sprite.commonAibuIcons[_arrayItem].enabled = false;
		}
		if ((bool)useItems[_arrayItem].transformSound)
		{
			Manager.Sound.Stop(useItems[_arrayItem].transformSound);
		}
		useItems[_arrayItem].layerAction = new ItemBodyLayer
		{
			body = -1,
			item = -1
		};
		useItems[_arrayItem] = null;
		AibuColliderKind kindTouch = useAreaItems[num].kindTouch;
		useAreaItems[num].kindTouch = AibuColliderKind.none;
		useAreaItems[num].obj.SetActive(value: false);
		useAreaItems[num].obj.transform.SetParent(null, worldPositionStays: false);
		UnityEngine.SceneManagement.Scene sceneByName = SceneManager.GetSceneByName("HTest");
		sceneByName = ((sceneByName.name == "HTest") ? sceneByName : SceneManager.GetSceneByName("HProc"));
		SceneManager.MoveGameObjectToScene(useAreaItems[num].obj, sceneByName);
		useAreaItems[num].layer = null;
		useAreaItems[num] = null;
		HSceneProc.FemaleParameter paramFemale = flags.nowAnimationInfo.paramFemale;
		switch (kindTouch)
		{
		case AibuColliderKind.muneL:
			yure.Proc(flags.nowAnimStateName, !paramFemale.isYure, 0);
			break;
		case AibuColliderKind.muneR:
			yure.Proc(flags.nowAnimStateName, !paramFemale.isYure, 1);
			break;
		}
		return num;
	}

	public void DetachAllItem()
	{
		if ((bool)female)
		{
			for (int i = 0; i < useItems.Length; i++)
			{
				DetachItemByUseItem(i);
			}
		}
	}

	public bool DetachItemByUseAreaItem(int _arrayArea)
	{
		if (!female)
		{
			return false;
		}
		if (!IsUseAreaItemActive(_arrayArea))
		{
			return false;
		}
		int num = -1;
		for (int i = 0; i < useItems.Length; i++)
		{
			if (useAreaItems[_arrayArea] == useItems[i])
			{
				num = i;
				break;
			}
		}
		if (num == -1)
		{
			return false;
		}
		SetShapeON(useItems[num].kindTouch);
		SetLayerWeightDefault(num);
		sprite.commonAibuIcons[num].enabled = false;
		if ((bool)useItems[num].transformSound)
		{
			Manager.Sound.Stop(useItems[num].transformSound);
		}
		useItems[num] = null;
		AibuColliderKind kindTouch = useAreaItems[_arrayArea].kindTouch;
		useAreaItems[_arrayArea].kindTouch = AibuColliderKind.none;
		useAreaItems[_arrayArea].obj.SetActive(value: false);
		useAreaItems[_arrayArea].obj.transform.SetParent(null, worldPositionStays: false);
		UnityEngine.SceneManagement.Scene sceneByName = SceneManager.GetSceneByName("HTest");
		sceneByName = ((sceneByName.name == "HTest") ? sceneByName : SceneManager.GetSceneByName("HProc"));
		SceneManager.MoveGameObjectToScene(useAreaItems[_arrayArea].obj, sceneByName);
		useAreaItems[_arrayArea].layer = null;
		useAreaItems[_arrayArea] = null;
		HSceneProc.FemaleParameter paramFemale = flags.nowAnimationInfo.paramFemale;
		switch (kindTouch)
		{
		case AibuColliderKind.muneL:
			yure.Proc(flags.nowAnimStateName, !paramFemale.isYure, 0);
			break;
		case AibuColliderKind.muneR:
			yure.Proc(flags.nowAnimStateName, !paramFemale.isYure, 1);
			break;
		}
		return true;
	}

	public int IsFrontTouch(int _arrayItem)
	{
		if (!female)
		{
			return -1;
		}
		if (useItems[_arrayItem] == null)
		{
			return -1;
		}
		if (!MathfEx.IsRange(-1, _arrayItem, useItems.Length, isEqual: false))
		{
			return -1;
		}
		HSceneProc.FemaleParameter paramFemale = flags.nowAnimationInfo.paramFemale;
		if (!MathfEx.IsRange(AibuColliderKind.muneL, useItems[_arrayItem].kindTouch, (paramFemale.lstFrontAndBehind[0] == 0) ? AibuColliderKind.kokan : AibuColliderKind.muneR, isEqual: true))
		{
			return 1;
		}
		return 0;
	}

	private bool SetItem(int _arrayItem, int _arrayArea, LayerInfo _infoLayer, int _LR, AibuColliderKind _kindTouch, bool _isFront, bool _isSetHistory = true)
	{
		if (!female)
		{
			return false;
		}
		useAreaItems[_arrayArea] = dicItem[_infoLayer.idObjects[_LR]];
		useAreaItems[_arrayArea].obj.SetActive(value: true);
		useAreaItems[_arrayArea].obj.transform.SetParent(_infoLayer.transParents[_LR], worldPositionStays: false);
		useAreaItems[_arrayArea].kindTouch = _kindTouch;
		int idUse = useAreaItems[_arrayArea].idUse;
		AibuItem useItem = (useItems[idUse] = useAreaItems[_arrayArea]);
		SetIdleLayerWeight(idUse, _infoLayer, _isFront);
		sprite.commonAibuIcons[_arrayItem].enabled = true;
		int num = -1;
		for (int i = 0; i < lstIcon.Count; i++)
		{
			if (lstIcon[i].id == _infoLayer.idIocn)
			{
				num = i;
				break;
			}
		}
		if (num != -1)
		{
			sprite.commonAibuIcons[_arrayItem].texture = lstIcon[num].texIcons[1];
		}
		if (_isSetHistory)
		{
			SetTouchHistory(_arrayItem);
		}
		useItem.layer = _infoLayer;
		EnableDynamicBone(_infoLayer.layerActions[0]);
		EliminateScale[] componentsInChildren = useItem.obj.GetComponentsInChildren<EliminateScale>(includeInactive: true);
		if (componentsInChildren != null && componentsInChildren.Length != 0)
		{
			int num2 = componentsInChildren.Length - 1;
			componentsInChildren[num2].SetShapeList(_arrayArea);
			componentsInChildren[num2].chaCtrl = female;
		}
		if (!useItem.pathSEAsset.IsNullOrEmpty() && !useItem.nameSEFile.IsNullOrEmpty())
		{
			Utils.Sound.Setting setting = new Utils.Sound.Setting();
			setting.loader.type = Manager.Sound.Type.GameSE3D;
			setting.loader.bundle = useItem.pathSEAsset;
			setting.loader.asset = useItem.nameSEFile;
			AudioSource audioSource = Utils.Sound.Play(setting);
			if (audioSource != null)
			{
				audioSource.loop = true;
			}
			useItem.transformSound = ((audioSource == null) ? null : audioSource.transform);
			if (useItem.transformSound != null)
			{
				(from _ in this.UpdateAsObservable()
					select useItem).TakeWhile((AibuItem item) => item != null).Subscribe(delegate(AibuItem item)
				{
					item.transformSound.SafeProcObject(delegate(Transform t)
					{
						t.SetPositionAndRotation(item.obj.transform.position, item.obj.transform.rotation);
					});
				});
			}
		}
		AnimatrotRestrart();
		return true;
	}

	private bool EnableDynamicBone(ActionLayer _actionLayer)
	{
		if (!female)
		{
			return false;
		}
		if (_actionLayer.dynamicArea == -1)
		{
			return true;
		}
		female.playDynamicBoneBust((ChaInfo.DynamicBoneKind)_actionLayer.dynamicArea, _actionLayer.enableDynamic);
		return true;
	}

	public bool SetLayerWeightDefault(int _arrayItem)
	{
		if (!female)
		{
			return false;
		}
		blend.ForceStop(_arrayItem);
		if (useItems[_arrayItem].layerAction.item != -1)
		{
			if (useItems[_arrayItem].anm.GetLayerWeight(useItems[_arrayItem].layerAction.item) >= 1f)
			{
				useItems[_arrayItem].anm.SetLayerWeight(useItems[_arrayItem].layerAction.item, 0f);
			}
			useItems[_arrayItem].layerAction.item = -1;
		}
		if (useItems[_arrayItem].layerAction.body != -1)
		{
			if (female.getLayerWeight(useItems[_arrayItem].layerAction.body) >= 1f)
			{
				female.setLayerWeight(0f, useItems[_arrayItem].layerAction.body);
			}
			useItems[_arrayItem].layerAction.body = -1;
		}
		if (useItems[_arrayItem].layerIdle.item != -1)
		{
			useItems[_arrayItem].anm.SetLayerWeight(useItems[_arrayItem].layerIdle.item, 0f);
			useItems[_arrayItem].layerIdle.item = -1;
		}
		if (useItems[_arrayItem].layerIdle.body != -1)
		{
			female.setLayerWeight(0f, useItems[_arrayItem].layerIdle.body);
			useItems[_arrayItem].layerIdle.body = -1;
		}
		return true;
	}

	public bool SetLayerWeight(int _arrayItem, LayerInfo _infoLayer, bool _isFront, int _action = 0)
	{
		ItemBodyLayer itemBodyLayer = default(ItemBodyLayer);
		if (_action == 0)
		{
			itemBodyLayer.item = -1;
			itemBodyLayer.body = -1;
		}
		else
		{
			itemBodyLayer = (_isFront ? _infoLayer.layerActions[_action].front : _infoLayer.layerActions[_action].back);
		}
		blend.Set(_arrayItem, useItems[_arrayItem].anm, useItems[_arrayItem].layerAction, itemBodyLayer);
		useItems[_arrayItem].layerAction = itemBodyLayer;
		return true;
	}

	public bool SetIdleLayerWeight(int _arrayItem, LayerInfo _infoLayer, bool _isFront)
	{
		if (!female)
		{
			return false;
		}
		useItems[_arrayItem].layerIdle = (_isFront ? _infoLayer.layerActions[0].front : _infoLayer.layerActions[0].back);
		useItems[_arrayItem].anm.SetLayerWeight(useItems[_arrayItem].layerIdle.item, 1f);
		female.setLayerWeight(1f, useItems[_arrayItem].layerIdle.body);
		return true;
	}

	private void SetTouchHistory(int _touch)
	{
		if (lstTouchHistory.Count == 0 || lstTouchHistory[0] != _touch)
		{
			if (lstTouchHistory.Count > 1 && lstTouchHistory[1] == _touch)
			{
				lstTouchHistory.RemoveAt(1);
			}
			lstTouchHistory.Insert(0, _touch);
			if (lstTouchHistory.Count > 3)
			{
				lstTouchHistory.RemoveAt(3);
			}
		}
	}

	private bool ClickAction()
	{
		if (!female)
		{
			return false;
		}
		if (useItems[actionUseItem] == null)
		{
			FinishAction();
			return false;
		}
		LayerInfo layer = useItems[actionUseItem].layer;
		int num = (int)(useItems[actionUseItem].kindTouch - 1);
		int[] array = new int[7] { 0, 1, 1, 2, 3, 4, 4 };
		bool flag = flags.lstHeroine[0].weakPoint == array[num];
		if (!flag)
		{
			flag = num == 1 && useItems[actionUseItem].idObj != 0 && flags.lstHeroine[0].weakPoint == 5;
		}
		flags.FemaleGaugeUp(Time.deltaTime * flags.rateClickGauge * (flag ? flags.rateWeakPoint : 1f), _force: false, nowMES.stateMotion == 0);
		for (int i = 0; i < useItems.Length; i++)
		{
			if (useItems[i] == null)
			{
				continue;
			}
			int num2 = (int)(useItems[i].kindTouch - 1);
			if (num2 == 1 || num2 == 2)
			{
				bool flag2 = useItems[i].idObj == 0;
				flags.SetSelectArea(flag2 ? 1 : 5, Time.deltaTime * (flags.rateClickGauge + flags.rateClickCheet));
				if (!flag2 && useItems[i].idObj == 3)
				{
					flags.AddSelectMassage(Time.deltaTime * (flags.rateClickGauge + flags.rateClickCheet), 1);
				}
			}
			else
			{
				flags.SetSelectArea(array[num2], Time.deltaTime * (flags.rateClickGauge + flags.rateClickCheet));
			}
			if (useItems[i].idObj > 2)
			{
				flags.SetSelectHobby(Time.deltaTime * (flags.rateClickGauge + flags.rateClickCheet));
			}
		}
		if (Input.GetMouseButtonDown(0))
		{
			flags.SpeedUpClickItemAibu(flags.rateSpeedUpItem, flags.speedMaxAibuItem, _drag: false);
			if (flags.mode == HFlag.EMode.aibu)
			{
				flags.SpeedUpClickAibu(flags.rateSpeedUpAibu, flags.speedMaxAibuBody, _drag: false);
			}
			flags.DragStart();
			oneMoreLoop = true;
		}
		if (Input.GetMouseButtonUp(0))
		{
			flags.FinishDrag();
		}
		bool flag3 = flags.nowAnimationInfo.paramFemale.lstFrontAndBehind[0] == 0;
		int nLayer = (flag3 ? layer.layerActions[1].front.body : layer.layerActions[1].back.body);
		if (female.getAnimatorStateInfo(nLayer).normalizedTime >= 1f)
		{
			int clothState = GetClothState(useItems[actionUseItem].kindTouch);
			if (flags.IsDrag() && layer.plays[clothState] != -1)
			{
				SetDragStartLayer(flag3);
				ctrl = Ctrl.drag;
				AnimatrotRestrart();
				flags.ctrlCamera.isCursorLock = false;
				Singleton<GameCursor>.Instance.SetCursorLock(setLockFlag: true);
				oldHandNormalizeTime = 0f;
				isClickDragVoice = false;
			}
			else if (oneMoreLoop)
			{
				AnimatrotRestrart();
				oneMoreLoop = false;
				voicePlayClickCount = (voicePlayClickCount + 1) % voicePlayClickLoop;
				if (voicePlayClickCount == 0)
				{
					voicePlayClickLoop = UnityEngine.Random.Range(10, 20);
					if (flags.mode == HFlag.EMode.aibu)
					{
						int[,] array2 = new int[10, 6]
						{
							{ -1, 111, 113, 115, 117, -1 },
							{ -1, 123, 119, 121, -1, -1 },
							{ -1, 131, 125, 127, 129, -1 },
							{ -1, 137, 133, -1, 135, -1 },
							{ -1, -1, 139, -1, -1, -1 },
							{ -1, -1, 146, -1, -1, -1 },
							{ -1, 150, 148, -1, -1, -1 },
							{ -1, -1, -1, -1, -1, -1 },
							{ -1, -1, -1, -1, -1, -1 },
							{ -1, -1, -1, -1, -1, -1 }
						};
						flags.voice.playVoices[numFemale] = array2[useItems[actionUseItem].idObj, array[num]];
						isClickDragVoice = true;
					}
				}
				else if (voicePlayClickCount == 5 && isClickDragVoice)
				{
					flags.SetMouseAction(useItems[actionUseItem].idObj, num, 0);
				}
				if (IsMotionIdle())
				{
					actionPlayClickCount = (actionPlayClickCount + 1) % actionPlayClickLoop;
					if (actionPlayClickCount == 0)
					{
						actionPlayClickLoop = UnityEngine.Random.Range(5, 10);
						AibuColliderKind kindTouch = AibuColliderKind.reac_bodyup;
						if (useItems[actionUseItem].kindTouch > AibuColliderKind.muneR)
						{
							kindTouch = AibuColliderKind.reac_bodydown;
						}
						Reaction(kindTouch);
					}
				}
				for (int j = 0; j < useItems.Length; j++)
				{
					if (useItems[j] != null)
					{
						string[] array3 = new string[6] { "muneL", "muneR", "kokan", "anal", "siriL", "siriR" };
						int num3 = (int)(useItems[j].kindTouch - 2);
						PlaySE(array3[num3], 1, useItems[j].obj.transform, useItems[j].layer.pathDragSE);
					}
				}
			}
			else
			{
				FinishAction();
			}
		}
		return true;
	}

	private bool DragAction()
	{
		if (!female)
		{
			return false;
		}
		if (actionUseItem != -1 && useItems[actionUseItem] == null)
		{
			flags.timeNoClick = 0f;
			flags.FinishDrag();
			FinishAction();
			if (isKiss && flags.mode == HFlag.EMode.aibu && !flags.isDebug)
			{
				flags.ctrlCamera.SetWorldBase(infoKissCamera.oldPos, infoKissCamera.oldRot);
				flags.ctrlCamera.SetCameraData(infoKissCamera.dataCam);
			}
			flags.ctrlCamera.isCursorLock = true;
			flags.ctrlCamera.GetComponent<CameraEffectorConfig>().useDOF = true;
			foreach (KeyValuePair<ChaInfo.DynamicBoneKind, DynamicBone_Ver02> item in female.dictDynamicBoneBust)
			{
				item.Value.Force = Vector3.zero;
			}
			isKiss = false;
			return true;
		}
		calcDragLength.Set(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
		if (flags.mode == HFlag.EMode.aibu)
		{
			flags.SpeedUpClickAibu(calcDragLength.magnitude * flags.rateDragSpeedUp, flags.speedMaxAibuBody, _drag: true);
		}
		flags.DragStart();
		int num = 0;
		if (actionUseItem != -1)
		{
			num = (int)(useItems[actionUseItem].kindTouch - 1);
		}
		int[] array = new int[7] { 0, 1, 1, 2, 3, 4, 4 };
		bool flag = flags.lstHeroine[0].weakPoint == array[num];
		if (!flag && actionUseItem != -1)
		{
			flag = num == 1 && useItems[actionUseItem].idObj != 0 && flags.lstHeroine[0].weakPoint == 5;
		}
		flags.FemaleGaugeUp(calcDragLength.magnitude * flags.rateDragGauge * (flag ? flags.rateWeakPoint : 1f), _force: false, nowMES.stateMotion == 0);
		LayerInfo layerInfo = null;
		if (actionUseItem != -1 && useItems[actionUseItem] != null)
		{
			layerInfo = useItems[actionUseItem].layer;
		}
		else
		{
			for (int i = 0; i < useItems.Length; i++)
			{
				if (useItems[i] != null)
				{
					layerInfo = useItems[i].layer;
					break;
				}
			}
		}
		if (layerInfo != null)
		{
			int nLayer = ((flags.nowAnimationInfo.paramFemale.lstFrontAndBehind[0] == 0) ? layerInfo.layerActions[2].front.body : layerInfo.layerActions[2].back.body);
			float num2 = female.getAnimatorStateInfo(nLayer).normalizedTime % 1f;
			if (num2 < oldHandNormalizeTime)
			{
				for (int j = 0; j < useItems.Length; j++)
				{
					if (useItems[j] != null)
					{
						string[] array2 = new string[6] { "muneL", "muneR", "kokan", "anal", "siriL", "siriR" };
						int num3 = (int)(useItems[j].kindTouch - 2);
						PlaySE(array2[num3], 0, useItems[j].obj.transform, useItems[j].layer.pathDragSE);
					}
				}
			}
			oldHandNormalizeTime = num2;
		}
		for (int k = 0; k < useItems.Length; k++)
		{
			if (useItems[k] == null)
			{
				continue;
			}
			int num4 = (int)(useItems[k].kindTouch - 1);
			if (num4 == 1 || num4 == 2)
			{
				bool flag2 = useItems[k].idObj == 0;
				flags.SetSelectArea(flag2 ? 1 : 5, calcDragLength.magnitude * (flags.rateDragGauge + flags.rateDragCheet));
				if (!flag2 && useItems[k].idObj == 3)
				{
					flags.AddSelectMassage(calcDragLength.magnitude * (flags.rateDragGauge + flags.rateDragCheet), 0);
				}
			}
			else
			{
				flags.SetSelectArea(array[num4], calcDragLength.magnitude * (flags.rateDragGauge + flags.rateDragCheet));
			}
			if (useItems[k].idObj > 2)
			{
				flags.SetSelectHobby(calcDragLength.magnitude * (flags.rateDragGauge + flags.rateDragCheet));
			}
		}
		if (!isKiss)
		{
			voicePlayActionMove = (voicePlayActionMove + calcDragLength.magnitude) % (float)voicePlayActionLoop;
			if (voicePlayActionMoveOld > voicePlayActionMove)
			{
				voicePlayActionLoop = UnityEngine.Random.Range(1000, 1500);
				if (flags.mode == HFlag.EMode.aibu)
				{
					int[,] array3 = new int[10, 6]
					{
						{ -1, 112, 114, 116, 118, -1 },
						{ -1, 124, 120, 122, -1, -1 },
						{ -1, 132, 126, 128, 130, -1 },
						{ -1, 138, 134, -1, 136, -1 },
						{ -1, -1, 140, -1, -1, -1 },
						{ -1, -1, 147, -1, -1, -1 },
						{ -1, 151, 149, -1, -1, -1 },
						{ -1, -1, -1, -1, -1, -1 },
						{ -1, -1, -1, -1, -1, -1 },
						{ -1, -1, -1, -1, -1, -1 }
					};
					flags.voice.playVoices[numFemale] = array3[useItems[actionUseItem].idObj, array[num]];
					isClickDragVoice = true;
				}
			}
			else if (MathfEx.IsRange(voicePlayActionMoveOld, 100f, voicePlayActionMove, isEqual: true) && isClickDragVoice)
			{
				flags.SetMouseAction(useItems[actionUseItem].idObj, (int)(useItems[actionUseItem].kindTouch - 1), 1);
			}
			voicePlayActionMoveOld = voicePlayActionMove;
			if (IsMotionIdle())
			{
				actionPlayActionMove = (actionPlayActionMove + calcDragLength.magnitude) % (float)actionPlayActionLoop;
				if (actionPlayActionMoveOld > actionPlayActionMove)
				{
					actionPlayActionLoop = UnityEngine.Random.Range(300, 400);
					AibuColliderKind kindTouch = AibuColliderKind.reac_bodyup;
					if (useItems[actionUseItem].kindTouch > AibuColliderKind.muneR)
					{
						kindTouch = AibuColliderKind.reac_bodydown;
					}
					Reaction(kindTouch);
				}
				actionPlayActionMoveOld = actionPlayActionMove;
			}
		}
		else
		{
			flags.SetSelectArea(0, calcDragLength.magnitude * (flags.rateDragGauge + flags.rateDragCheet));
			voicePlayActionMove = (voicePlayActionMove + calcDragLength.magnitude) % (float)voicePlayActionLoop;
			if (voicePlayActionMoveOld > voicePlayActionMove)
			{
				if (flags.mode == HFlag.EMode.aibu)
				{
					flags.voice.playVoices[numFemale] = 101;
				}
				voicePlayActionLoop = UnityEngine.Random.Range(1000, 1500);
			}
			else if (MathfEx.IsRange(voicePlayActionMoveOld, 100f, voicePlayActionMove, isEqual: true))
			{
				flags.SetMouseAction(0, 0, 1);
			}
			voicePlayActionMoveOld = voicePlayActionMove;
		}
		int num5 = -1;
		if (actionUseItem != -1 && useItems[actionUseItem] != null)
		{
			LayerInfo layer = useItems[actionUseItem].layer;
			int num6 = (int)(useItems[actionUseItem].kindTouch - 2);
			flags.SpeedUpClickItemAibu(calcDragLength.magnitude * flags.rateDragSpeedUpItem, flags.speedMaxAibuItem, _drag: true);
			if (layer.actionDrag == 2)
			{
				flags.xy[num6] = MoveMouseParameter(objReferences[num6], flags.xy[num6], flags.speedDynamicBoneMove, flags.forceLength);
				Vector3 force = objReferences[num6].transform.TransformDirection(new Vector3(flags.xy[num6].x, flags.xy[num6].y, 0f));
				ChaInfo.DynamicBoneKind[] array4 = new ChaInfo.DynamicBoneKind[6]
				{
					ChaInfo.DynamicBoneKind.BreastL,
					ChaInfo.DynamicBoneKind.BreastR,
					ChaInfo.DynamicBoneKind.BreastL,
					ChaInfo.DynamicBoneKind.BreastL,
					ChaInfo.DynamicBoneKind.HipL,
					ChaInfo.DynamicBoneKind.HipR
				};
				female.dictDynamicBoneBust[array4[num6]].Force = force;
			}
			else if (layer.actionDrag == 1 || layer.actionDrag == 3)
			{
				flags.xy[num6] = flags.xy[num6] * 2f - Vector2.one;
				flags.xy[num6] = MoveMouseParameter(objReferences[num6], flags.xy[num6], flags.speedXYMove, flags.forceLength);
				flags.xy[num6] = new Vector2(flags.xy[num6].x * 0.5f + 0.5f, flags.xy[num6].y * 0.5f + 0.5f);
			}
			num5 = useItems.Count((AibuItem c) => c != null) - 1;
			List<AibuItem> list = new List<AibuItem>();
			switch (num5)
			{
			case 1:
			{
				for (int num7 = 0; num7 < useItems.Length; num7++)
				{
					if (useItems[num7] != null && num7 != actionUseItem)
					{
						list.Add(useItems[num7]);
						break;
					}
				}
				break;
			}
			case 2:
			{
				bool[] array5 = new bool[3]
				{
					useItems[1].kindTouch == AibuColliderKind.muneL || useItems[1].kindTouch == AibuColliderKind.siriR,
					useItems[0].kindTouch == AibuColliderKind.muneR || useItems[0].kindTouch == AibuColliderKind.siriL,
					GetTouchHistoryAt(1) != 1
				};
				int[] array6 = new int[3] { 1, 0, 0 };
				int[] array7 = new int[3] { 2, 2, 1 };
				if (actionUseItem == 2)
				{
					if (useItems[2].kindTouch == AibuColliderKind.muneL || useItems[2].kindTouch == AibuColliderKind.siriR)
					{
						array5[2] = useItems[0].kindTouch == AibuColliderKind.muneR || useItems[0].kindTouch == AibuColliderKind.siriL;
					}
					else if (useItems[2].kindTouch == AibuColliderKind.muneR || useItems[2].kindTouch == AibuColliderKind.siriL)
					{
						array5[2] = useItems[1].kindTouch == AibuColliderKind.kokan || useItems[1].kindTouch == AibuColliderKind.anal;
					}
				}
				list.Add(useItems[array5[actionUseItem] ? array6[actionUseItem] : array7[actionUseItem]]);
				list.Add(useItems[array5[actionUseItem] ? array7[actionUseItem] : array6[actionUseItem]]);
				break;
			}
			}
			for (int num8 = 0; num8 < list.Count; num8++)
			{
				int num9 = (int)(list[num8].kindTouch - 2);
				if (list[num8].layer.actionDrag == 2)
				{
					if (layer.actionDrag == 2)
					{
						flags.xy[num9] = flags.xy[num6];
					}
					else if (layer.actionDrag == 1 || layer.actionDrag == 3)
					{
						flags.xy[num9] = Vector2.ClampMagnitude(new Vector2((flags.xy[num6].x * 2f - 1f) * flags.speedXYMove, (flags.xy[num6].y * 2f - 1f) * flags.speedXYMove), flags.forceLength);
					}
					if (num8 == 0)
					{
						flags.xy[num9].x = 0f - flags.xy[num9].x;
					}
					Vector3 force = objReferences[num9].transform.TransformDirection(new Vector3(flags.xy[num9].x, flags.xy[num9].y, 0f));
					ChaInfo.DynamicBoneKind[] array8 = new ChaInfo.DynamicBoneKind[6]
					{
						ChaInfo.DynamicBoneKind.BreastL,
						ChaInfo.DynamicBoneKind.BreastR,
						ChaInfo.DynamicBoneKind.BreastL,
						ChaInfo.DynamicBoneKind.BreastL,
						ChaInfo.DynamicBoneKind.HipL,
						ChaInfo.DynamicBoneKind.HipR
					};
					female.dictDynamicBoneBust[array8[num9]].Force = force;
				}
				else if (list[num8].layer.actionDrag == 3)
				{
					if (layer.actionDrag == 2)
					{
						flags.xy[num9] = Vector2.ClampMagnitude(new Vector2(flags.xy[num6].x / flags.speedDynamicBoneMove, flags.xy[num6].y / flags.speedDynamicBoneMove), flags.forceLength);
					}
					else if (layer.actionDrag == 1 || layer.actionDrag == 3)
					{
						flags.xy[num9] = flags.xy[num6];
					}
					if (flags.nowAnimationInfo.paramFemale.lstFrontAndBehind[0] == 1 && (useItems[actionUseItem].kindTouch == AibuColliderKind.kokan || list[num8].kindTouch == AibuColliderKind.kokan))
					{
						flags.xy[num9] = Vector2.one - flags.xy[num9];
					}
					if (num8 == 0)
					{
						flags.xy[num9].x = 1f - flags.xy[num9].x;
					}
					else if (list.Count > 1 && actionUseItem != 2 && list[1].idUse == 2 && ((useItems[actionUseItem].kindTouch == AibuColliderKind.muneL && (list[1].kindTouch == AibuColliderKind.muneR || list[1].kindTouch == AibuColliderKind.siriL)) || (useItems[actionUseItem].kindTouch == AibuColliderKind.muneR && (list[1].kindTouch == AibuColliderKind.muneL || list[1].kindTouch == AibuColliderKind.siriR)) || (useItems[actionUseItem].kindTouch == AibuColliderKind.siriL && (list[1].kindTouch == AibuColliderKind.siriR || list[1].kindTouch == AibuColliderKind.muneL)) || (useItems[actionUseItem].kindTouch == AibuColliderKind.siriR && (list[1].kindTouch == AibuColliderKind.siriL || list[1].kindTouch == AibuColliderKind.muneR))))
					{
						flags.xy[num9].x = 1f - flags.xy[num9].x;
					}
				}
			}
		}
		else
		{
			num5 = useItems.Count((AibuItem c) => c != null);
			List<AibuItem> list2 = new List<AibuItem>();
			switch (num5)
			{
			case 1:
			{
				for (int num10 = 0; num10 < useItems.Length; num10++)
				{
					if (useItems[num10] != null)
					{
						list2.Add(useItems[num10]);
						break;
					}
				}
				break;
			}
			case 2:
				list2.Add(useItems[0]);
				list2.Add(useItems[1]);
				break;
			}
			xyKiss = MoveMouseParameter(null, xyKiss, 1f, flags.forceLength);
			for (int num11 = 0; num11 < list2.Count; num11++)
			{
				int num12 = (int)(list2[num11].kindTouch - 2);
				if (list2[num11].layer.actionDrag == 2)
				{
					flags.xy[num12] = xyKiss;
					if (num11 == 0)
					{
						flags.xy[num12].x = 0f - flags.xy[num12].x;
					}
					Vector3 force = objReferences[num12].transform.TransformDirection(new Vector3(flags.xy[num12].x, flags.xy[num12].y, 0f));
					ChaInfo.DynamicBoneKind[] array9 = new ChaInfo.DynamicBoneKind[6]
					{
						ChaInfo.DynamicBoneKind.BreastL,
						ChaInfo.DynamicBoneKind.BreastR,
						ChaInfo.DynamicBoneKind.BreastL,
						ChaInfo.DynamicBoneKind.BreastL,
						ChaInfo.DynamicBoneKind.HipL,
						ChaInfo.DynamicBoneKind.HipR
					};
					female.dictDynamicBoneBust[array9[num12]].Force = force;
				}
				else if (list2[num11].layer.actionDrag == 3)
				{
					flags.xy[num12] = new Vector2(xyKiss.x * 0.5f + 0.5f, xyKiss.y * 0.5f + 0.5f);
					if (flags.nowAnimationInfo.paramFemale.lstFrontAndBehind[0] == 1 && list2[num11].kindTouch == AibuColliderKind.kokan)
					{
						flags.xy[num12] = Vector2.one - flags.xy[num12];
					}
					if (num11 == 0)
					{
						flags.xy[num12].x = 1f - flags.xy[num12].x;
					}
				}
			}
		}
		if (flags.isDebug && !Input.GetMouseButton(0) && !Input.GetKey(KeyCode.Return))
		{
			flags.timeNoClick = 0f;
			flags.FinishDrag();
		}
		else if (!flags.isDebug && !Input.GetMouseButton(0))
		{
			flags.timeNoClick = 0f;
			flags.FinishDrag();
		}
		if (!flags.drag)
		{
			FinishAction();
			if (isKiss && flags.mode == HFlag.EMode.aibu && !flags.isDebug)
			{
				flags.ctrlCamera.SetWorldBase(infoKissCamera.oldPos, infoKissCamera.oldRot);
				flags.ctrlCamera.SetCameraData(infoKissCamera.dataCam);
			}
			flags.ctrlCamera.isCursorLock = true;
			flags.ctrlCamera.GetComponent<CameraEffectorConfig>().useDOF = true;
			foreach (KeyValuePair<ChaInfo.DynamicBoneKind, DynamicBone_Ver02> item2 in female.dictDynamicBoneBust)
			{
				item2.Value.Force = Vector3.zero;
			}
			isKiss = false;
		}
		return true;
	}

	private bool KissAction()
	{
		calcDragLength.Set(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
		if (flags.mode == HFlag.EMode.aibu)
		{
			flags.SpeedUpClickAibu(calcDragLength.magnitude * flags.rateDragSpeedUp, flags.speedMaxAibuBody, _drag: true);
		}
		flags.DragStart();
		if (!Input.GetMouseButton(0))
		{
			ForceFinish();
		}
		return true;
	}

	private bool IsMotionIdle()
	{
		if (!female)
		{
			return false;
		}
		if (flags.mode != HFlag.EMode.houshi && flags.mode != HFlag.EMode.sonyu)
		{
			return false;
		}
		AnimatorStateInfo animatorStateInfo = female.getAnimatorStateInfo(0);
		if (animatorStateInfo.IsName("Idle") || animatorStateInfo.IsName("OUT_A") || animatorStateInfo.IsName("Drink_A") || animatorStateInfo.IsName("Vomit_A") || animatorStateInfo.IsName("InsertIdle") || animatorStateInfo.IsName("IN_A") || animatorStateInfo.IsName("A_Idle") || animatorStateInfo.IsName("A_InsertIdle") || animatorStateInfo.IsName("A_OUT_A") || animatorStateInfo.IsName("A_IN_A"))
		{
			return true;
		}
		return false;
	}

	public bool ForceFinish(bool _isCameraMoveStop = true)
	{
		flags.timeNoClick = 0f;
		flags.FinishDrag();
		if (_isCameraMoveStop)
		{
			GlobalMethod.SetCameraMoveFlag(flags.ctrlCamera, _bPlay: false);
		}
		action = HandAction.none;
		actionUseItem = -1;
		flags.ctrlCamera.isCursorLock = true;
		HSceneProc.FemaleParameter paramFemale = flags.nowAnimationInfo.paramFemale;
		bool isFront = paramFemale.lstFrontAndBehind.Count == 0 || paramFemale.lstFrontAndBehind[0] == 0;
		for (int i = 0; i < useItems.Length; i++)
		{
			if (useItems[i] != null)
			{
				SetLayerWeight(i, useItems[i].layer, isFront);
				EnableDynamicBone(useItems[i].layer.layerActions[0]);
				AnimatrotRestrart();
			}
		}
		if (isKiss && !flags.isDebug && flags.mode == HFlag.EMode.aibu)
		{
			flags.ctrlCamera.SetWorldBase(infoKissCamera.oldPos, infoKissCamera.oldRot);
			flags.ctrlCamera.SetCameraData(infoKissCamera.dataCam);
		}
		isKiss = false;
		foreach (KeyValuePair<string, Transform> dicTransS in dicTransSEs)
		{
			dicTransS.Value.SafeProcObject(delegate(Transform o)
			{
				Manager.Sound.Stop(o);
			});
		}
		return true;
	}

	private bool FinishAction()
	{
		GlobalMethod.SetCameraMoveFlag(flags.ctrlCamera, _bPlay: true);
		action = HandAction.none;
		actionUseItem = -1;
		bool isFront = flags.nowAnimationInfo.paramFemale.lstFrontAndBehind[0] == 0;
		for (int i = 0; i < useItems.Length; i++)
		{
			if (useItems[i] != null)
			{
				SetLayerWeight(i, useItems[i].layer, isFront);
				EnableDynamicBone(useItems[i].layer.layerActions[0]);
				AnimatrotRestrart();
			}
		}
		flags.voice.SetAibuIdleTime();
		foreach (KeyValuePair<string, Transform> dicTransS in dicTransSEs)
		{
			dicTransS.Value.SafeProcObject(delegate(Transform o)
			{
				Manager.Sound.Stop(o);
			});
		}
		return true;
	}

	private bool AnimatrotRestrart()
	{
		if (!female)
		{
			return false;
		}
		for (int i = 0; i < useItems.Length; i++)
		{
			if (useItems[i] != null)
			{
				useItems[i].anm.SetTrigger("Restart");
			}
		}
		female.setAnimatorParamTrigger("Restart");
		return true;
	}

	private bool SetDragStartLayer(bool _isFront)
	{
		for (int i = 0; i < useItems.Length; i++)
		{
			if (useItems[i] != null)
			{
				int clothState = GetClothState(useItems[i].kindTouch);
				int num = ((useItems[i].layer.plays[clothState] > 0) ? 2 : 0);
				SetLayerWeight(i, useItems[i].layer, _isFront, num);
				EnableDynamicBone(useItems[i].layer.layerActions[num]);
				int num2 = (int)(useItems[i].kindTouch - 2);
				switch (useItems[i].layer.actionDrag)
				{
				case 2:
					flags.xy[num2] = Vector2.zero;
					break;
				case 1:
				case 3:
					flags.xy[num2] = new Vector2(0.5f, 0.5f);
					break;
				}
			}
		}
		return true;
	}

	private bool EnableShape(AibuColliderKind _kindTouch, bool _enable)
	{
		if (!female)
		{
			return false;
		}
		switch (_kindTouch)
		{
		case AibuColliderKind.muneL:
			female.DisableShapeBust(0, _enable);
			female.DisableShapeNip(0, _enable);
			female.DisableShapeBodyID(0, ChaFileDefine.cf_ShapeMaskNipStand, _enable);
			break;
		case AibuColliderKind.muneR:
			female.DisableShapeBust(1, _enable);
			female.DisableShapeNip(1, _enable);
			female.DisableShapeBodyID(1, ChaFileDefine.cf_ShapeMaskNipStand, _enable);
			break;
		}
		return true;
	}

	public bool SetShapeON(AibuColliderKind _kindTouch)
	{
		if (!female)
		{
			return false;
		}
		EnableShape(_kindTouch, _enable: false);
		if (female.dictDynamicBoneBust == null)
		{
			return true;
		}
		switch (_kindTouch)
		{
		case AibuColliderKind.muneL:
			female.playDynamicBoneBust(ChaInfo.DynamicBoneKind.BreastL, _bPlay: true);
			break;
		case AibuColliderKind.muneR:
			female.playDynamicBoneBust(ChaInfo.DynamicBoneKind.BreastR, _bPlay: true);
			break;
		}
		return true;
	}

	private Vector3 MoveMouseParameter(GameObject _objRef, Vector2 _xy, float _fSpeed, float _fForceLen)
	{
		if (_objRef == null)
		{
			return Vector3.ClampMagnitude(_xy + new Vector2(Input.GetAxis("Mouse X") * _fSpeed, Input.GetAxis("Mouse Y") * _fSpeed), _fForceLen);
		}
		Vector3 calc = new Vector3(Input.GetAxis("Mouse X") * _fSpeed, Input.GetAxis("Mouse Y") * _fSpeed, 0f);
		calc = MoveParameterCalc(_objRef, calc, _fForceLen);
		return Vector3.ClampMagnitude(_xy + new Vector2(calc.x, calc.y), _fForceLen);
	}

	private Vector3 MoveParameterCalc(GameObject _objRef, Vector3 _calc, float _fForceLen)
	{
		if (_objRef == null)
		{
			return Vector3.zero;
		}
		Transform obj = _objRef.transform;
		_calc = flags.ctrlCamera.transform.TransformDirection(_calc);
		_calc = obj.InverseTransformDirection(_calc);
		_calc.z = 0f;
		return Vector3.ClampMagnitude(_calc, _fForceLen);
	}

	private int IsDontTouchAnalAndMassager(AibuColliderKind _kindTouch, LayerInfo _layer)
	{
		switch (_kindTouch)
		{
		case AibuColliderKind.anal:
			if (flags.lstHeroine[0].hAreaExps[3] + flags.lstHeroine[0].countAnalH == 0f && !flags.lstHeroine[0].denial.anal)
			{
				return 0;
			}
			break;
		case AibuColliderKind.muneL:
		case AibuColliderKind.muneR:
		case AibuColliderKind.kokan:
			if ((_layer.idObjects[0] == 5 || _layer.idObjects[0] == 6) && flags.lstHeroine[0].massageExps[(_kindTouch != AibuColliderKind.kokan) ? 1u : 0u] == 0f && !flags.lstHeroine[0].denial.massage)
			{
				if (_kindTouch == AibuColliderKind.kokan)
				{
					return 2;
				}
				return 1;
			}
			break;
		}
		return -1;
	}

	public bool SceneChangeItemEnable(bool _enable)
	{
		foreach (KeyValuePair<int, AibuItem> item in dicItem)
		{
			if ((bool)item.Value.renderBody)
			{
				item.Value.renderBody.enabled = _enable;
			}
			else if ((bool)item.Value.meshRenderBody)
			{
				item.Value.meshRenderBody.enabled = _enable;
			}
			if ((bool)item.Value.renderSilhouette)
			{
				item.Value.renderSilhouette.enabled = _enable;
			}
			else if ((bool)item.Value.meshRenderSilhouette)
			{
				item.Value.meshRenderSilhouette.enabled = _enable;
			}
		}
		return true;
	}

	private int GetClothState(AibuColliderKind _kindTouch)
	{
		if (!female)
		{
			return 0;
		}
		if (_kindTouch == AibuColliderKind.muneL || _kindTouch == AibuColliderKind.muneR)
		{
			return 2;
		}
		if (!female.IsKokanHide())
		{
			return 2;
		}
		return 0;
	}

	private bool PlaySE(string _key, byte _click, Transform _transParent, PathInfo _pathInfo)
	{
		if (_pathInfo.asset.IsNullOrEmpty() || _pathInfo.file.IsNullOrEmpty())
		{
			return false;
		}
		Utils.Sound.Setting setting = new Utils.Sound.Setting();
		setting.loader.type = Manager.Sound.Type.GameSE3D;
		setting.loader.bundle = _pathInfo.asset;
		setting.loader.asset = _pathInfo.file;
		Transform transform = (dicTransSEs[_key] = Utils.Sound.Play(setting).transform);
		Transform t = transform;
		if (t != null)
		{
			this.UpdateAsObservable().Subscribe(delegate
			{
				if (t != null)
				{
					t.SetPositionAndRotation(_transParent.position, _transParent.rotation);
				}
			}).AddTo(t);
		}
		return true;
	}

	private IEnumerator EnableShapeCoroutine(AibuColliderKind _kindTouch)
	{
		yield return null;
		EnableShape(_kindTouch, _enable: true);
	}

	private int GetAreaExperience(AibuColliderKind _touchArea)
	{
		if (_touchArea == AibuColliderKind.none)
		{
			return 0;
		}
		int num = (int)(_touchArea - 1);
		int num2 = (new int[7] { 0, 1, 1, 2, 3, 4, 4 })[num];
		if (GetUseItemStickObjectID(actionUseItem) != 0 && num2 == 1)
		{
			num2 = 5;
		}
		switch (num2)
		{
		case 2:
		{
			float num3 = flags.lstHeroine[0].hAreaExps[num2] + flags.lstHeroine[0].countKokanH;
			if (num3 != 0f)
			{
				if (!(num3 >= 100f))
				{
					return 1;
				}
				return 2;
			}
			return 0;
		}
		case 3:
		{
			float num4 = flags.lstHeroine[0].hAreaExps[num2] + flags.lstHeroine[0].countAnalH;
			if (num4 != 0f)
			{
				if (!(num4 >= 100f))
				{
					return 1;
				}
				return 2;
			}
			return 0;
		}
		default:
			if (flags.lstHeroine[0].hAreaExps[num2] != 0f)
			{
				if (!(flags.lstHeroine[0].hAreaExps[num2] >= 100f))
				{
					return 1;
				}
				return 2;
			}
			return 0;
		}
	}

	private BaseCameraControl_Ver2.CameraData KissCameraDataLoadSimple(string _assetbundleFolder, string _strFile)
	{
		BaseCameraControl_Ver2.CameraData result = default(BaseCameraControl_Ver2.CameraData);
		string text = GlobalMethod.LoadAllListText(_assetbundleFolder, _strFile);
		if (text.IsNullOrEmpty())
		{
			GlobalMethod.DebugLog("cameraファイル読み込めません", 1);
			return result;
		}
		GlobalMethod.GetListString(text, out var data);
		result.Pos.x = float.Parse(data[0, 0]);
		result.Pos.y = float.Parse(data[1, 0]);
		result.Pos.z = float.Parse(data[2, 0]);
		result.Dir.x = float.Parse(data[3, 0]);
		result.Dir.y = float.Parse(data[4, 0]);
		result.Dir.z = float.Parse(data[5, 0]);
		result.Rot.x = float.Parse(data[6, 0]);
		result.Rot.y = float.Parse(data[7, 0]);
		result.Rot.z = float.Parse(data[8, 0]);
		result.Fov = float.Parse(data[9, 0]);
		return result;
	}

	private BaseCameraControl_Ver2.CameraData KissCameraDataLoad(string _assetbundleFolder, string _strFile, float _t)
	{
		BaseCameraControl_Ver2.CameraData[] array = new BaseCameraControl_Ver2.CameraData[3];
		BaseCameraControl_Ver2.CameraData result = default(BaseCameraControl_Ver2.CameraData);
		for (int i = 0; i < 3; i++)
		{
			array[i] = KissCameraDataLoadSimple(_assetbundleFolder, _strFile + "_" + i.ToString("00"));
		}
		float t = Mathf.InverseLerp(0f, 0.5f, _t);
		float t2 = Mathf.InverseLerp(0.5f, 1f, _t);
		result.Pos = ((_t < 0.5f) ? Vector3.Lerp(array[0].Pos, array[1].Pos, t) : Vector3.Lerp(array[1].Pos, array[2].Pos, t2));
		result.Rot = ((_t < 0.5f) ? Vector3.Lerp(array[0].Rot, array[1].Rot, t) : Vector3.Lerp(array[1].Rot, array[2].Rot, t2));
		result.Dir = ((_t < 0.5f) ? Vector3.Lerp(array[0].Dir, array[1].Dir, t) : Vector3.Lerp(array[1].Dir, array[2].Dir, t2));
		result.Fov = ((_t < 0.5f) ? Mathf.Lerp(array[0].Fov, array[1].Fov, t) : Mathf.Lerp(array[1].Fov, array[2].Fov, t2));
		return result;
	}

	public static void ReleaseList()
	{
		for (int i = 0; i < lstIcon.Count; i++)
		{
			for (int j = 0; j < lstIcon[i].texIcons.Length; j++)
			{
				lstIcon[i].texIcons[j] = null;
			}
		}
		lstIcon.Clear();
		lstIcon = null;
		Dictionary<int, LayerInfo>[] array = dicAreaLayerInfos;
		for (int k = 0; k < array.Length; k++)
		{
			array[k].Clear();
		}
		dicAreaLayerInfos = null;
	}

	public void RotorReaction()
	{
		if (!hitReaction.IsPlay())
		{
			HitReactionPlay(AibuColliderKind.reac_bodyup);
			HitReactionPlay(AibuColliderKind.reac_bodydown);
		}
	}
}
