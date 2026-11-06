using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ADV;
using ActionGame;
using ActionGame.H;
using Cysharp.Threading.Tasks;
using FreeH;
using H;
using Illusion.Game.Elements.EasyLoader;
using IllusionUtility.GetUtility;
using Manager;
using SaveData;
using UnityEngine;

public class HScene : MonoBehaviour
{
	public class AddParameter
	{
		public class Exp
		{
			public int kind = -1;

			public float val;
		}

		public float kiss;

		public float[] aibus = new float[5];

		public float houshi;

		public float[] sonyus = new float[2];

		public List<Exp> hexp = new List<Exp>();

		public Exp hexpBonus = new Exp();

		public float hexpMax;
	}

	public class InfoADVFile
	{
		public string assetbudle;

		public string adv;
	}

	public class ResultTopic
	{
		public int id;

		public int topic;

		public float rate;
	}

	public class HObjOffSetData
	{
		public HPointData hpoint;

		public int targetID = -1;

		public int groupID = -1;

		public bool UsePos;

		public bool UseAng;

		public Vector3 offsetPos = Vector3.zero;

		public Vector3 offsetAng = Vector3.zero;

		public Vector3 backupPos = Vector3.zero;

		public Vector3 backupAng = Vector3.zero;

		public Transform _trs;

		public Transform trs
		{
			get
			{
				if (_trs == null)
				{
					if (hpoint == null || (targetID < 0 && groupID < 0))
					{
						_trs = null;
					}
					else if (targetID >= 0)
					{
						_trs = ((targetID < hpoint.objTargets.Length) ? hpoint.objTargets[targetID] : null);
					}
					else
					{
						_trs = ((groupID < hpoint.objGroups.Length) ? hpoint.objGroups[groupID] : null);
					}
				}
				return _trs;
			}
		}
	}

	public class ADVParam : SceneParameter
	{
		public bool isInVisibleChara = true;

		public float distanceCamera = 1f;

		public Vector3 correctionCamera = Vector3.zero;

		public float MaxOffset = 0.04f;

		public ADVParam(MonoBehaviour scene)
			: base(scene)
		{
		}

		public override void Init(Data data)
		{
			ADVScene aDVScene = SceneParameter.advScene;
			TextScenario scenario = aDVScene.Scenario;
			scenario.BackCamera.fieldOfView = Camera.main.fieldOfView;
			scenario.LoadBundleName = data.bundleName;
			scenario.LoadAssetName = data.assetName;
			aDVScene.Stand.SetPositionAndRotation(data.position, data.rotation);
			scenario.heroineList = data.heroineList;
			scenario.transferList = data.transferList;
			if (!data.heroineList.IsNullOrEmpty())
			{
				for (int i = 0; i < data.heroineList.Count; i++)
				{
					Heroine heroine = data.heroineList[i];
					SingletonInitializer<Game>.instance.advAnimePack.SetDefalut(heroine.chaCtrl, out var motionIK);
					IKMotion iKMotion = new IKMotion();
					iKMotion.Create(heroine.chaCtrl, motionIK);
					scenario.commandController.AddChara(i, new ADV.CharaData(heroine, heroine.chaCtrl, scenario, new ADV.CharaData.MotionReserver
					{
						ikMotion = iKMotion
					}, hiPoly: true, data.isParentChara));
				}
				scenario.ChangeCurrentChara(0);
			}
			if (!data.isParentChara)
			{
				var array = (from p in data.heroineList
					select p.chaCtrl into chaCtrl
					where chaCtrl != null
					select new
					{
						chaCtrl = chaCtrl,
						height = chaCtrl.GetShapeBodyValue(0),
						bust = chaCtrl.GetReferenceInfo(ChaReference.RefObjKey.BUSTUP_TARGET).transform
					}).ToArray();
				var anon = array[0];
				Vector3 position = anon.chaCtrl.transform.position;
				array = array.OrderBy(p => p.height).ToArray();
				var array2 = array;
				foreach (var obj in array2)
				{
					obj.chaCtrl.transform.position = position;
					obj.chaCtrl.animBody.Update(0f);
				}
				anon = array[0];
				Transform bust = anon.bust;
				Vector3 position2 = bust.position;
				var anon2 = array.Last();
				position2.y += (anon2.bust.position.y - bust.position.y) * 0.5f;
				float t = anon2.height - anon.height;
				position2.y += Mathf.Lerp(0f, MaxOffset, t);
				position2.y += Mathf.Lerp(0f, 0.05f, anon2.chaCtrl.GetShapeBodyValue(1));
				position2.y -= MotionIK.GetShapeLerpPositionValue(array.Average(p => p.height), new Vector3(0f, -0.09f, 0f), new Vector3(0f, 0.11f, 0f)).y;
				Vector3 targetPos = position2;
				position2 += anon.chaCtrl.transform.forward * distanceCamera;
				position2 += anon.chaCtrl.transform.TransformDirection(correctionCamera);
				Quaternion rotation = OpenData.CameraData.ToTargetRotation(position2, targetPos);
				Transform transform = anon.chaCtrl.transform;
				scenario.Characters.SetPositionAndRotation(transform.position, transform.rotation);
				scenario.BackCamera.transform.SetPositionAndRotation(position2, rotation);
			}
			aDVScene.Map = (SingletonInitializer<ActionScene>.initialized ? SingletonInitializer<ActionScene>.instance.Map : null);
			scenario.Vars["Topic"] = new ValData(ValData.Convert((base.mono as HScene).selectTopic, typeof(int)));
			scenario.Vars["Orgasm"] = new ValData(ValData.Convert((base.mono as HScene).orgasm, typeof(bool)));
			float fadeInTime = data.fadeInTime;
			if (fadeInTime > 0f)
			{
				aDVScene.fadeTime = fadeInTime;
			}
			else
			{
				isInVisibleChara = false;
			}
		}

		public override void Release()
		{
			SceneParameter.advScene.gameObject.SetActive(value: false);
		}

		public override void WaitEndProc()
		{
		}
	}

	private Player player;

	private List<Heroine> heroineList;

	private OpenHData.Data dataH;

	private int finishADV;

	private int kiss;

	private bool myRoom;

	private bool spHpoint;

	private bool changeTaii;

	private bool actYukaTaii;

	private bool orgasm;

	private int selectTopic = -1;

	private bool isFreeH;

	private AddParameter addParameter = new AddParameter();

	private HFlag.EMode mode = HFlag.EMode.none;

	private List<Camera> cameras = new List<Camera>();

	private Dictionary<int, InfoADVFile> dicADVInfo = new Dictionary<int, InfoADVFile>();

	private Dictionary<int, ResultTopic> dicResultTopic = new Dictionary<int, ResultTopic>();

	private List<string> lstABName;

	private List<HObjOffSetData> offSetData = new List<HObjOffSetData>();

	private void Awake()
	{
		ParameterList.Add(new ADVParam(this)
		{
			distanceCamera = 2.2f,
			correctionCamera = new Vector3(0f, 0f, 0f)
		});
	}

	private void OnDestroy()
	{
		ParameterList.Remove(this);
		lstABName.Clear();
	}

	private IEnumerator Start()
	{
		lstABName = CommonLib.GetAssetBundleNameListFromPath("h/list/");
		lstABName.Sort();
		offSetData.Clear();
		HFlag.lstABName = lstABName;
		if (SingletonInitializer<Scene>.initialized)
		{
			GameObject commonSpace = Scene.commonSpace;
			if (commonSpace != null)
			{
				OpenHData component = commonSpace.GetComponent<OpenHData>();
				if (component != null && component.data != null)
				{
					dataH = component.data as OpenHData.Data;
				}
				Object.Destroy(component);
			}
		}
		dataH.SafeProc(delegate(OpenHData.Data data)
		{
			isFreeH = data.isFreeH;
		});
		ActionScene actscene = SingletonInitializer<ActionScene>.instance;
		if ((bool)actscene)
		{
			actscene.isPenetration = false;
			dataH.newHeroione = actscene.GetHSceneOtherHeroine(dataH.lstFemale[0]);
		}
		dataH.isFirstPlayMasturbation = dataH.peepCategory.Any((int c) => MathfEx.IsRange(1100, c, 1199, isEqual: true));
		dataH.isMasturbationInitPoint = false;
		string levelName = "";
		int HScenePlayCount = 0;
		while (true)
		{
			levelName = "HProc";
			HSceneProc hSceneProc = null;
			Scene.LoadReserveAsyncForget(new Scene.Data
			{
				levelName = levelName,
				isAdd = true,
				onLoad = delegate
				{
					hSceneProc = Scene.GetRootComponent<HSceneProc>(levelName);
					hSceneProc.dataH = dataH;
					hSceneProc.offSetData = offSetData;
				}
			}, isLoadingImageDraw: false);
			yield return new WaitWhile(() => hSceneProc == null);
			yield return StartCoroutine(HProcEndWait(hSceneProc));
			bool flag = (dataH != null && dataH.peepCategory.Where((int c) => MathfEx.IsRange(2000, c, 2002, isEqual: true)).Any()) || mode == HFlag.EMode.peeping || mode == HFlag.EMode.masturbation || mode == HFlag.EMode.lesbian;
			if (isFreeH || (flag && (actscene == null || !actscene.isPenetration)))
			{
				if (mode == HFlag.EMode.masturbation || mode == HFlag.EMode.lesbian)
				{
					foreach (Heroine item in dataH.lstFemale)
					{
						item.lewdness /= 2;
					}
				}
				Scene.Unload();
				if (isFreeH)
				{
					GameObject commonSpace2 = Scene.commonSpace;
					if (commonSpace2 != null)
					{
						FreeHBackData freeHBackData = commonSpace2.AddComponent<FreeHBackData>();
						freeHBackData.heroine = dataH.lstFemale[0];
						freeHBackData.partner = ((dataH.lstFemale.Count > 1) ? dataH.lstFemale[1] : null);
						freeHBackData.player = dataH.player;
						freeHBackData.map = dataH.mapNoFreeH;
						freeHBackData.timeZone = dataH.timezoneFreeH;
						freeHBackData.statusH = dataH.statusFreeH;
						freeHBackData.stageH1 = dataH.stageFreeH1;
						freeHBackData.stageH2 = dataH.stageFreeH2;
						freeHBackData.discovery = dataH.isFound;
						freeHBackData.categorys = dataH.peepCategory;
						freeHBackData.SecretBeachStateFreeH = dataH.SecretBeachStateFreeH;
					}
				}
				yield break;
			}
			yield return ResultTalk(this.GetCancellationTokenOnDestroy()).ToCoroutine();
			HResultADVCameraSetting(_isStart: false);
			if ((bool)actscene && !actscene.isPenetration)
			{
				break;
			}
			if (HScenePlayCount > 0)
			{
				if (mode != HFlag.EMode.houshi3P && mode != HFlag.EMode.sonyu3P)
				{
					break;
				}
				if ((bool)actscene)
				{
					foreach (Heroine item2 in dataH.lstFemale)
					{
						actscene.actCtrl.SetDesire(5, item2, actscene.actCtrl.GetDesire(5, item2) / 2);
						item2.lewdness /= 2;
					}
				}
				Scene.Unload();
				yield break;
			}
			ActionMap map = null;
			if ((bool)actscene)
			{
				map = actscene.GetComponent<ActionMap>();
			}
			if (mode == HFlag.EMode.masturbation)
			{
				int pose = 0;
				bool num = dataH.peepCategory.Any((int c) => c == 1010 || c == 1011 || c == 1015 || c == 1016);
				bool flag2 = dataH.peepCategory.Any((int c) => MathfEx.IsRange(2000, c, 2999, isEqual: true));
				if (num || flag2)
				{
					pose = 1;
				}
				dataH.peepCategory = new List<int> { 0 };
				dataH.newHeroione = null;
				dataH.isLoadPeepLoom = false;
				bool flag3 = map != null && map.no == 36 && dataH.peepCategory.Contains(2004);
				if (flag2 && (bool)map && !flag3)
				{
					int no = 14;
					if (map.no == 34)
					{
						no = map.prevNo;
					}
					map.Change(no, FadeCanvas.Fade.None);
					yield return new WaitUntil(() => !map.isMapLoading);
				}
				if (pose == 1)
				{
					GlobalMethod.HPointAppointNullDatail detail = new GlobalMethod.HPointAppointNullDatail
					{
						mode = HFlag.EMode.aibu,
						idMap = (map ? map.no : 0),
						kindGet = 2,
						lstCategory = new List<int> { 0 },
						pos = dataH.position
					};
					offSetData.Clear();
					GlobalMethod.HPointAppointNullGetData hPointAppointNull = GlobalMethod.GetHPointAppointNull(detail, ref offSetData, map.mapObjectGroup);
					dataH.position = hPointAppointNull.pos;
					dataH.rotation = hPointAppointNull.rot;
					dataH.isMasturbationInitPoint = true;
				}
			}
			else if (mode == HFlag.EMode.lesbian)
			{
				dataH.newHeroione = null;
				dataH.kind = null;
				GlobalMethod.HPointAppointNullDatail detail2 = new GlobalMethod.HPointAppointNullDatail
				{
					mode = HFlag.EMode.houshi3P,
					idMap = (map ? map.no : 0),
					kindGet = 2,
					lstCategory = new List<int> { 3000, 3001 },
					pos = dataH.position
				};
				offSetData.Clear();
				GlobalMethod.HPointAppointNullGetData hPointAppointNull2 = GlobalMethod.GetHPointAppointNull(detail2, ref offSetData, map.mapObjectGroup);
				dataH.position = hPointAppointNull2.pos;
				dataH.rotation = hPointAppointNull2.rot;
				dataH.peepCategory = hPointAppointNull2.category;
			}
			else
			{
				dataH.lstFemaleCoordinateType.Add(dataH.newHeroione.chaCtrl.fileStatus.coordinateType);
				dataH.newHeroione = null;
				dataH.kind = null;
				GlobalMethod.HPointAppointNullDatail detail3 = new GlobalMethod.HPointAppointNullDatail
				{
					mode = HFlag.EMode.houshi3P,
					idMap = (map ? map.no : 0),
					kindGet = 2,
					lstCategory = new List<int> { 3000, 3001 },
					pos = dataH.position
				};
				offSetData.Clear();
				GlobalMethod.HPointAppointNullGetData hPointAppointNull3 = GlobalMethod.GetHPointAppointNull(detail3, ref offSetData, map.mapObjectGroup);
				dataH.position = hPointAppointNull3.pos;
				dataH.rotation = hPointAppointNull3.rot;
				dataH.peepCategory = hPointAppointNull3.category;
			}
			dataH.clothStates.Clear();
			dataH.accessoryStates.Clear();
			dataH.lstFemaleCoordinateType.Clear();
			for (int num2 = 0; num2 < dataH.lstFemale.Count; num2++)
			{
				dataH.clothStates.Add(new List<int>());
				for (int num3 = 0; num3 < 9; num3++)
				{
					dataH.clothStates[num2].Add(dataH.lstFemale[num2].chaCtrl.fileStatus.clothesState[num3]);
				}
				dataH.accessoryStates.Add(new List<bool>());
				for (int num4 = 0; num4 < dataH.lstFemale[num2].chaCtrl.fileStatus.showAccessory.Length; num4++)
				{
					dataH.accessoryStates[num2].Add(dataH.lstFemale[num2].chaCtrl.fileStatus.showAccessory[num4]);
				}
				dataH.lstFemaleCoordinateType.Add(dataH.lstFemale[num2].chaCtrl.fileStatus.coordinateType);
			}
			if (dataH.lstFemale[0].chaCtrl.hiPoly)
			{
				Character.DeleteChara(dataH.lstFemale[0].chaCtrl);
			}
			if (dataH.player.chaCtrl.hiPoly)
			{
				Character.DeleteChara(dataH.player.chaCtrl);
			}
			if (dataH.lstFemale.Count > 1 && dataH.lstFemale[1].chaCtrl.hiPoly)
			{
				Character.DeleteChara(dataH.lstFemale[1].chaCtrl);
			}
			Camera main = Camera.main;
			if ((bool)main)
			{
				Object.Destroy(main.transform.parent.gameObject);
			}
			HScenePlayCount++;
			dataH.hScenePlayCount = HScenePlayCount;
		}
		if (dataH.lstFemale.Count != 0 && (bool)dataH.lstFemale[0].chaCtrl)
		{
			dataH.lstFemale[0].chaCtrl.SetClothesStateAll(0);
			dataH.lstFemale[0].chaCtrl.SetAccessoryStateAll(show: true);
			dataH.lstFemale[0].chaCtrl.mouthCtrl.SafeProc(delegate(FBSCtrlMouth m)
			{
				m.OpenMin = 0f;
			});
			dataH.lstFemale[0].chaCtrl.ChangeLookNeckTarget(0);
			dataH.lstFemale[0].chaCtrl.ChangeLookEyesTarget(0);
		}
		if (mode == HFlag.EMode.houshi3PMMF || mode == HFlag.EMode.sonyu3PMMF)
		{
			Scene.Unload();
			yield break;
		}
		levelName = "HSceneResult";
		HSceneResult hSceneResult = null;
		Scene.LoadReserveAsyncForget(new Scene.Data
		{
			levelName = levelName,
			isAdd = true,
			onLoad = delegate
			{
				hSceneResult = Scene.GetRootComponent<HSceneResult>(levelName);
				hSceneResult.finishADV = finishADV;
				hSceneResult.lstHeroine = heroineList;
				hSceneResult.player = player;
				addParameter.aibus.CopyTo(hSceneResult.addParameter.aibus, 0);
				hSceneResult.addParameter.kiss = addParameter.kiss;
				hSceneResult.addParameter.houshi = addParameter.houshi;
				addParameter.sonyus.CopyTo(hSceneResult.addParameter.sonyus, 0);
				hSceneResult.addParameter.hexp = addParameter.hexp;
				hSceneResult.addParameter.hexpBonus = addParameter.hexpBonus;
				hSceneResult.addParameter.hexpMax = addParameter.hexpMax;
			}
		}, isLoadingImageDraw: false);
		yield return new WaitWhile(() => hSceneResult == null);
		if (!Scene.isReturnTitle && !ExitDialog.isGameEnd)
		{
			yield return Scene.sceneFadeCanvas.StartFadeAysnc(FadeCanvas.Fade.Out).ToCoroutine();
			yield return StartCoroutine(HResult(hSceneResult));
			yield return Scene.UnloadAsync().ToCoroutine();
		}
	}

	private IEnumerator HProcEndWait(HSceneProc hSceneProc)
	{
		yield return new WaitUntil(() => hSceneProc.isEnd);
		finishADV = hSceneProc.finishADV;
		kiss = hSceneProc.countKiss;
		myRoom = hSceneProc.myRoom;
		spHpoint = hSceneProc.spHpoint;
		changeTaii = hSceneProc.changeTaii;
		actYukaTaii = hSceneProc.actYukaTaii;
		orgasm = hSceneProc.Orgasm;
		mode = hSceneProc.flags.mode;
		hSceneProc.addParameter.aibus.CopyTo(addParameter.aibus, 0);
		addParameter.houshi = hSceneProc.addParameter.houshi;
		addParameter.kiss = hSceneProc.addParameter.kiss;
		hSceneProc.addParameter.sonyus.CopyTo(addParameter.sonyus, 0);
		addParameter.hexp = hSceneProc.addParameter.hexp;
		addParameter.hexpBonus = hSceneProc.addParameter.hexpBonus;
		addParameter.hexpMax = hSceneProc.addParameter.hexpMax;
		yield return Scene.sceneFadeCanvas.StartFadeAysnc(FadeCanvas.Fade.In).ToCoroutine();
		yield return StartCoroutine(hSceneProc.ResetScene());
		yield return null;
		if ((bool)hSceneProc.flags.lstHeroine[0].chaCtrl)
		{
			hSceneProc.flags.lstHeroine[0].chaCtrl.ReSetupDynamicBoneBust();
		}
		heroineList = hSceneProc.flags.lstHeroine;
		player = hSceneProc.flags.player;
		ActionScene actscene = SingletonInitializer<ActionScene>.instance;
		if ((bool)actscene && actscene.isPenetration && (mode == HFlag.EMode.aibu || mode == HFlag.EMode.houshi || mode == HFlag.EMode.sonyu) && dataH.newHeroione != null)
		{
			yield return StartCoroutine(hSceneProc.CreateFemaleCharacterAppoint(dataH.newHeroione, dataH.newHeroione.chaCtrl.fileStatus.coordinateType));
			heroineList.Add(dataH.newHeroione);
		}
		hSceneProc.enabled = false;
		yield return Scene.UnloadAsync().ToCoroutine();
		if (actscene != null)
		{
			actscene.ShortcutKeyEnable(isOn: true);
		}
	}

	private async UniTask ResultTalk(CancellationToken token)
	{
		LoadADVInfo();
		LoadResultTopic();
		selectTopic = TopicSelect();
		List<Program.Transfer> list = new List<Program.Transfer>();
		list.Add(Program.Transfer.Create(false, Command.SceneFadeRegulate, bool.FalseString));
		Heroine heroine = heroineList[0];
		Program.SetParam(player, heroine, list);
		if (!dicADVInfo.TryGetValue(finishADV, out var value))
		{
			GlobalMethod.DebugLog("Hシーン終了ADVの番号取得エラー [取得番号:" + finishADV + "]", 1);
			finishADV = 6;
			value = dicADVInfo[finishADV];
		}
		if (MathfEx.IsRange(100, finishADV, 199, isEqual: true))
		{
			list.Add(Program.Transfer.Open(value.assetbudle, value.adv, bool.TrueString));
		}
		else
		{
			list.Add(Program.Transfer.Open(Program.FindADVBundleFilePath(value.adv, heroine), value.adv, bool.TrueString));
		}
		bool isParentChara = false;
		Transform transform = Camera.main.transform;
		Object.Destroy(transform.GetComponent<CameraControl_Ver2>());
		await Program.Open(new Data
		{
			fadeInTime = 0f,
			position = Vector3.zero,
			rotation = Quaternion.identity,
			camera = new OpenData.CameraData
			{
				position = transform.position,
				rotation = transform.rotation
			},
			scene = this,
			transferList = list,
			heroineList = heroineList,
			isParentChara = isParentChara
		}, token);
		Camera main = Camera.main;
		if ((bool)main)
		{
			main.clearFlags = CameraClearFlags.Depth;
			main.cullingMask = 1 << LayerMask.NameToLayer("Chara");
			GameObject gameObject = main.transform.FindLoop("CameraMap");
			if ((bool)gameObject)
			{
				Camera component = gameObject.GetComponent<Camera>();
				if ((bool)component)
				{
					component.enabled = true;
				}
			}
			CameraEffector component2 = main.GetComponent<CameraEffector>();
			if ((bool)component2)
			{
				component2.config.SetFog(enable: false);
			}
		}
		HResultADVCameraSetting(_isStart: true);
		await Program.Wait("H", token);
	}

	private IEnumerator HResult(HSceneResult _hSceneResult)
	{
		yield return new WaitUntil(() => _hSceneResult.isEnd);
		yield return Scene.sceneFadeCanvas.StartFadeAysnc(FadeCanvas.Fade.In).ToCoroutine();
		yield return Scene.UnloadAsync().ToCoroutine();
		ActionScene instance = SingletonInitializer<ActionScene>.instance;
		if (instance != null)
		{
			instance.ShortcutKeyEnable(isOn: true);
		}
	}

	private bool LoadADVInfo()
	{
		dicADVInfo.Clear();
		string text = "gotoADV";
		foreach (string item in lstABName)
		{
			if (!AssetBundleCheck.IsFile(item, text) || (!AssetBundleCheck.IsSimulation && !HFlag.BundleCheck(item, text)))
			{
				continue;
			}
			GotoADVData gotoADVData = CommonLib.LoadAsset<GotoADVData>(item, text, clone: false, "", check: false);
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: true);
			if (gotoADVData == null)
			{
				continue;
			}
			foreach (GotoADVData.Param item2 in gotoADVData.param)
			{
				int key = item2.finishADV;
				if (!dicADVInfo.TryGetValue(key, out var value))
				{
					value = (dicADVInfo[key] = new InfoADVFile());
				}
				value.assetbudle = item2.bundle;
				value.adv = item2.adv;
			}
		}
		return true;
	}

	private bool LoadResultTopic()
	{
		dicResultTopic.Clear();
		string text = "result_topic";
		foreach (string item in lstABName)
		{
			if (!AssetBundleCheck.IsFile(item, text) || (!AssetBundleCheck.IsSimulation && !HFlag.BundleCheck(item, text)))
			{
				continue;
			}
			ResultTopicData resultTopicData = CommonLib.LoadAsset<ResultTopicData>(item, text, clone: false, "", check: false);
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: true);
			if (resultTopicData == null)
			{
				continue;
			}
			foreach (ResultTopicData.Param item2 in resultTopicData.param)
			{
				int id = item2.id;
				if (!dicResultTopic.TryGetValue(id, out var value))
				{
					value = (dicResultTopic[id] = new ResultTopic());
				}
				value.topic = item2.topic;
				value.rate = item2.rate;
			}
		}
		return true;
	}

	private int TopicSelect()
	{
		if (dicResultTopic == null)
		{
			return -1;
		}
		ShuffleRand shuffleRand = new ShuffleRand(10);
		float num = Mathf.InverseLerp(0f, 9f, shuffleRand.Get());
		if (finishADV == 0)
		{
			if (!dicResultTopic.ContainsKey(1) || !(dicResultTopic[1].rate >= num))
			{
				return -1;
			}
			return dicResultTopic[1].topic;
		}
		bool flag = false;
		if ((mode == HFlag.EMode.houshi3P || mode == HFlag.EMode.sonyu3P) ? (!orgasm) : (finishADV == 6 || finishADV == 7))
		{
			if (!dicResultTopic.ContainsKey(10) || !(dicResultTopic[10].rate >= num))
			{
				return -1;
			}
			return dicResultTopic[10].topic;
		}
		if (spHpoint)
		{
			shuffleRand.Init(10);
			num = Mathf.InverseLerp(0f, 9f, shuffleRand.Get());
			if (dicResultTopic.ContainsKey(8) && dicResultTopic[8].rate >= num)
			{
				return dicResultTopic[8].topic;
			}
		}
		List<int> list = new List<int>();
		if (addParameter.kiss >= 20f)
		{
			shuffleRand.Init(10);
			num = Mathf.InverseLerp(0f, 9f, shuffleRand.Get());
			if (dicResultTopic.ContainsKey(0) && dicResultTopic[0].rate >= num)
			{
				list.Add(dicResultTopic[0].topic);
			}
		}
		if (actYukaTaii)
		{
			shuffleRand.Init(10);
			num = Mathf.InverseLerp(0f, 9f, shuffleRand.Get());
			if (dicResultTopic.ContainsKey(2) && dicResultTopic[2].rate >= num)
			{
				list.Add(dicResultTopic[2].topic);
			}
		}
		else
		{
			shuffleRand.Init(10);
			num = Mathf.InverseLerp(0f, 9f, shuffleRand.Get());
			if (dicResultTopic.ContainsKey(3) && dicResultTopic[3].rate >= num)
			{
				list.Add(dicResultTopic[3].topic);
			}
		}
		if (myRoom)
		{
			shuffleRand.Init(10);
			num = Mathf.InverseLerp(0f, 9f, shuffleRand.Get());
			if (dicResultTopic.ContainsKey(4) && dicResultTopic[4].rate >= num)
			{
				list.Add(dicResultTopic[4].topic);
			}
		}
		if (addParameter.aibus[0] > 20f)
		{
			shuffleRand.Init(10);
			num = Mathf.InverseLerp(0f, 9f, shuffleRand.Get());
			if (dicResultTopic.ContainsKey(5) && dicResultTopic[5].rate >= num)
			{
				list.Add(dicResultTopic[5].topic);
			}
		}
		if (addParameter.aibus[2] + addParameter.aibus[3] + addParameter.sonyus[1] > 20f)
		{
			shuffleRand.Init(10);
			num = Mathf.InverseLerp(0f, 9f, shuffleRand.Get());
			if (dicResultTopic.ContainsKey(6) && dicResultTopic[6].rate >= num)
			{
				list.Add(dicResultTopic[6].topic);
			}
		}
		if (addParameter.aibus[1] + addParameter.sonyus[0] > 20f)
		{
			shuffleRand.Init(10);
			num = Mathf.InverseLerp(0f, 9f, shuffleRand.Get());
			if (dicResultTopic.ContainsKey(7) && dicResultTopic[7].rate >= num)
			{
				list.Add(dicResultTopic[7].topic);
			}
		}
		if (changeTaii)
		{
			shuffleRand.Init(10);
			num = Mathf.InverseLerp(0f, 9f, shuffleRand.Get());
			if (dicResultTopic.ContainsKey(9) && dicResultTopic[9].rate >= num)
			{
				list.Add(dicResultTopic[9].topic);
			}
		}
		if (list.Count == 0)
		{
			return -1;
		}
		shuffleRand.Init(list.Count);
		return list[shuffleRand.Get()];
	}

	private void HResultADVCameraSetting(bool _isStart)
	{
		GameScreenShot component = SingletonInitializer<ActionScene>.instance.GetComponent<GameScreenShot>();
		if (!component)
		{
			return;
		}
		cameras.Clear();
		cameras.AddRange(component.renderCam);
		Camera main = Camera.main;
		if (!main)
		{
			return;
		}
		GameObject gameObject = main.transform.FindLoop("CameraMap");
		if (!gameObject)
		{
			return;
		}
		Camera component2 = gameObject.GetComponent<Camera>();
		if ((bool)component2)
		{
			if (_isStart)
			{
				cameras.Add(component2);
				cameras.Add(main);
			}
			else
			{
				cameras.Remove(component2);
				cameras.Remove(main);
			}
			component.renderCam = cameras.ToArray();
		}
	}
}
