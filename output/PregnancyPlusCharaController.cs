using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BepInEx.Configuration;
using BepInEx.Logging;
using ExtensibleSaveFormat;
using KKAPI;
using KKAPI.Chara;
using KKAPI.MainGame;
using KKAPI.Maker;
using KKAPI.Studio;
using MessagePack;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace KK_PregnancyPlus;

public class PregnancyPlusCharaController : CharaCustomFunctionController
{
	[MessagePackObject(true)]
	public class MeshBlendShape
	{
		public string MeshName;

		public int VertCount;

		public string UncensorGUID;

		public BlendShape BlendShape;

		public MeshBlendShape(string meshName, BlendShape blendShape, int vertCount, string uncensorGUID)
		{
			MeshName = meshName;
			BlendShape = blendShape;
			VertCount = vertCount;
			UncensorGUID = uncensorGUID;
		}
	}

	internal bool initialized;

	internal bool firstStart = true;

	internal bool isProcessing;

	public BellyInfo bellyInfo;

	public string charaFileName;

	public bool lastVisibleState;

	public bool uncensorChanged;

	public bool isReloading;

	internal bool ignoreNextUncensorHook;

	public PregnancyPlusBlendShapeGui blendShapeGui = new PregnancyPlusBlendShapeGui();

	public PregnancyPlusClothOffsetGui clothOffsetGui = new PregnancyPlusClothOffsetGui();

	public PregnancyPlusData infConfig = new PregnancyPlusData();

	internal PregnancyPlusData infConfigHistory = new PregnancyPlusData();

	internal Dictionary<string, ClothSkinningCoefficient[]> preservedClothCoefficients = new Dictionary<string, ClothSkinningCoefficient[]>();

	internal Dictionary<string, int> coefficientCounter = new Dictionary<string, int>();

	internal MeshInflateFlags lastMeshInflateFlags;

	public Dictionary<string, MeshData> md = new Dictionary<string, MeshData>();

	public List<string> ignoreMeshList = new List<string>();

	public BindPoseList bindPoseList;

	public const string UncensorCOMName = "com.deathweasel.bepinex.uncensorselector";

	public const string DefaultBodyFemaleGUID = "Default.Body.Female";

	public const string DefaultBodyMaleGUID = "Default.Body.Male";

	public const string KK_PregnancyPluginName = "KK_Pregnancy";

	internal Guid debounceGuid;

	public static string[] bellyBones = new string[3] { "cf_s_spine02", "cf_s_waist01", "cf_s_waist02" };

	public NativeDetourMesh nativeDetour;

	public AnimationCurve FastLerpAC = AnimCurve.FastLerpAnimCurve();

	public AnimationCurve SlowLerpAC = AnimCurve.SlowLerpAnimCurve();

	public AnimationCurve BellyTopAC = AnimCurve.BellyTopAnimCurve();

	public AnimationCurve BellyEdgeAC = AnimCurve.BellyEdgeAnimCurve();

	public AnimationCurve BellySidesAC = AnimCurve.BellySidesAnimCurve();

	public AnimationCurve MediumLerpAC = AnimCurve.MediumLerpAnimCurve();

	public AnimationCurve BellyGapLerpAC = AnimCurve.BellyGapAnimCurve();

	public float BellyButtonOffset = 0.155f;

	internal Vector3[] rayCastTargetPositions = (Vector3[])(object)new Vector3[4];

	internal string[] rayCastTargetNames = new string[4] { "cf_s_spine02", "cf_s_waist01", "cf_s_thigh01_L", "cf_s_thigh01_R" };

	internal string[] innerLayers = new string[7] { "o_bra_a", "o_bra_b", "o_shorts_a", "o_shorts_b", "o_panst_garter1", "o_panst_a", "o_panst_b" };

	private const float minBoneWeight = 0f;

	internal List<MeshIdentifier> meshWithBlendShapes = new List<MeshIdentifier>();

	internal string blendShapeTempTagName = "[temp]";

	internal float _inflationStartSize;

	internal float _inflationChange;

	internal float _targetPregPlusSize;

	internal int inflationSpeed = 3;

	internal int currentWeeks;

	internal float timeElapsed;

	internal List<BlendShapeController> blendShapeCtrlList = new List<BlendShapeController>();

	public string BodyMeshName
	{
		get
		{
			if (((ChaInfo)base.ChaControl).sex != 0)
			{
				return "o_body_a";
			}
			return "o_body_a";
		}
	}

	public float TargetPregPlusSize
	{
		get
		{
			return _targetPregPlusSize;
		}
		set
		{
			_targetPregPlusSize = Mathf.Clamp(value, 0f, 40f);
		}
	}

	public float CurrentInflationChange => _inflationChange;

	public float MaxStoryModeBellySize
	{
		get
		{
			if (!PregnancyPlusPlugin.OverrideBelly.Value)
			{
				return PregnancyPlusPlugin.MaxStoryModeBelly.Value;
			}
			return 40f;
		}
	}

	public bool isDuringInflationScene
	{
		get
		{
			if (!(TargetPregPlusSize > 0f))
			{
				return CurrentInflationChange > 0f;
			}
			return true;
		}
	}

	protected override void OnCardBeingSaved(GameMode currentGameMode)
	{
		if (StudioAPI.InsideStudio || MakerAPI.InsideMaker)
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogInfo((object)"+= $OnCardBeingSaved ");
			}
			CaptureNewBlendshapeWeights();
			SetExtendedData(infConfig.Save());
		}
	}

	protected override void Start()
	{
		uncensorChanged = false;
		firstStart = true;
		isProcessing = false;
		charaFileName = ((ChaFile)base.ChaFileControl).parameter.fullname;
		if (PregnancyPlusPlugin.DebugLog.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)" ");
		}
		if (PregnancyPlusPlugin.DebugLog.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)("+= $Start " + charaFileName));
		}
		ReadAndSetCardData();
		bindPoseList = new BindPoseList(charaFileName);
		GameAPI.StartH += delegate
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogInfo((object)("+= $StartH " + charaFileName));
			}
			MeshInflate(infConfig.inflationSize, "GameAPI.StartH", new MeshInflateFlags(this, _checkForNewMesh: false, _freshStart: false, _pluginConfigSliderChanged: false, _visibilityUpdate: false, _bypassWhen0: true));
		};
		GameAPI.EndH += delegate
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogInfo((object)("+= $EndH " + charaFileName));
			}
			ClearInflationStuff(fullReset: true);
		};
		nativeDetour = new NativeDetourMesh();
		base.Start();
	}

	protected override void OnCoordinateBeingLoaded(ChaFileCoordinate coordinate)
	{
		if (PregnancyPlusPlugin.DebugLog.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)("+= $OnCoordinateBeingLoaded " + coordinate.coordinateName));
		}
		OnCoordinateLoaded();
		base.OnCoordinateBeingLoaded(coordinate);
	}

	protected override void OnReload(GameMode currentGameMode)
	{
		if (PregnancyPlusPlugin.DebugLog.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)" ");
		}
		if (PregnancyPlusPlugin.DebugLog.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)$"+= $OnReload {currentGameMode}");
		}
		isReloading = true;
		lastVisibleState = false;
		ClearOnReload();
		IsNewChar(base.ChaFileControl);
		charaFileName = ((ChaFile)base.ChaFileControl).parameter.fullname;
		bindPoseList.charaFileName = charaFileName;
		ReadAndSetCardData();
		CheckBellyPreservation();
		CheckBlendShapes();
		((MonoBehaviour)this).StartCoroutine(ReloadStoryInflation(0.5f, "Reload-story"));
		((MonoBehaviour)this).StartCoroutine(ReloadStudioMakerInflation(1.5f, reMeasure: true, "Reload"));
		firstStart = false;
	}

	protected override void Update()
	{
		WatchForUserKeyPress();
		ComputeInflationChange();
		if (PregnancyPlusPlugin.DebugAnimations.Value && Time.frameCount % 90 == 0)
		{
			MeshInflate(new MeshInflateFlags(this, _checkForNewMesh: true, _freshStart: true, _pluginConfigSliderChanged: false, _visibilityUpdate: false, _bypassWhen0: false, _reMeasure: true), "Update");
		}
	}

	protected override void OnDestroy()
	{
		if (PregnancyPlusPlugin.DebugLog.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)("+= $OnDestroy " + charaFileName));
		}
		nativeDetour?.Dispose();
		if (PregnancyPlusPlugin.AnyDebugPrimitivesToggled())
		{
			DebugTools.ClearAllThingsFromCharacter();
		}
	}

	internal IEnumerator ILoadBlendshapes(float waitforSeconds, bool checkUncensor = false)
	{
		yield return (object)new WaitForSeconds(waitforSeconds);
		if (!infConfig.UseOldCalcLogic())
		{
			yield return (object)new WaitForEndOfFrame();
		}
		LoadBlendShapes(infConfig, checkUncensor);
	}

	public void ClearOnReload()
	{
		meshWithBlendShapes = new List<MeshIdentifier>();
		blendShapeGui.CloseBlendShapeGui();
		PregnancyPlusGui.StopTextCountIncrement();
		lastMeshInflateFlags = null;
		isProcessing = false;
	}

	internal IEnumerator UserTriggeredUncensorChange()
	{
		yield return (object)new WaitForSeconds(0.1f);
		if (uncensorChanged)
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogInfo((object)" -UserTriggeredUncensorChange");
			}
			uncensorChanged = false;
			ClearOnReload();
			if (StudioAPI.InsideStudio || MakerAPI.InsideMaker)
			{
				ResetInflation();
			}
			LoadBlendShapes(infConfig);
			((MonoBehaviour)this).StartCoroutine(ReloadStoryInflation(0.5f, "OnUncensorChanged-story", checkNewMesh: true, uncensorChanged: true));
			((MonoBehaviour)this).StartCoroutine(ReloadStudioMakerInflation(1f, reMeasure: false, "OnUncensorChanged", uncensorChanged: true));
		}
	}

	internal bool IsNewChar(ChaFileControl chaFileControl)
	{
		bool result = charaFileName != ((ChaFile)chaFileControl).parameter.fullname;
		if (PregnancyPlusPlugin.DebugLog.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)(" IsNewChar " + charaFileName + " -> " + ((ChaFile)chaFileControl).parameter.fullname + " "));
		}
		return result;
	}

	internal IEnumerator ReloadStoryInflation(float time, string callee, bool checkNewMesh = true, bool uncensorChanged = false)
	{
		if (PregnancyPlusPlugin.StoryMode != null && !PregnancyPlusPlugin.StoryMode.Value)
		{
			isReloading = false;
		}
		else if (!StudioAPI.InsideStudio && !MakerAPI.InsideMaker)
		{
			yield return (object)new WaitForSeconds(time);
			if (!infConfig.UseOldCalcLogic())
			{
				yield return (object)new WaitForEndOfFrame();
			}
			GetWeeksAndSetBellySize(checkNewMesh, slidersChanged: false, callee);
			isReloading = false;
		}
	}

	internal IEnumerator ReloadStudioMakerInflation(float time, bool reMeasure, string callee, bool uncensorChanged = false)
	{
		if (StudioAPI.InsideStudio || MakerAPI.InsideMaker)
		{
			yield return (object)new WaitForSeconds(time);
			if (!infConfig.UseOldCalcLogic())
			{
				yield return (object)new WaitForEndOfFrame();
			}
			if (StudioAPI.InsideStudio || (MakerAPI.InsideMaker && MakerAPI.InsideAndLoaded))
			{
				MeshInflate(new MeshInflateFlags(this, _checkForNewMesh: true, _freshStart: true, _pluginConfigSliderChanged: false, _visibilityUpdate: false, _bypassWhen0: false, _reMeasure: true, uncensorChanged), callee);
				isReloading = false;
			}
			else if (MakerAPI.InsideMaker && !MakerAPI.InsideAndLoaded)
			{
				((MonoBehaviour)this).StartCoroutine(WaitForMakerLoad());
			}
		}
	}

	internal IEnumerator WaitForMakerLoad()
	{
		while (!MakerAPI.InsideAndLoaded)
		{
			yield return (object)new WaitForSeconds(0.01f);
		}
		if (PregnancyPlusPlugin.DebugLog.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)" WaitForMakerLoad done, setting initial sliders");
		}
		PregnancyPlusGui.OnPasteBelly(PregnancyPlusGui.sliders, GetCardData());
		yield return null;
		isReloading = false;
	}

	internal void WatchForUserKeyPress()
	{
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		if (StudioAPI.InsideStudio || MakerAPI.InsideMaker || (Object)(object)((ChaInfo)base.ChaControl).rendBody == (Object)null || !((ChaInfo)base.ChaControl).rendBody.isVisible)
		{
			return;
		}
		KeyboardShortcut value = PregnancyPlusPlugin.StoryModeInflationIncrease.Value;
		if (((KeyboardShortcut)(ref value)).IsDown())
		{
			if (isDuringInflationScene)
			{
				TargetPregPlusSize += 2f;
				_inflationChange = TargetPregPlusSize;
				MeshInflate(TargetPregPlusSize, "WatchForUserKeyPress");
			}
			else
			{
				float inflationSize = infConfig.inflationSize + 2f;
				MeshInflate(inflationSize, "WatchForUserKeyPress");
			}
		}
		value = PregnancyPlusPlugin.StoryModeInflationDecrease.Value;
		if (((KeyboardShortcut)(ref value)).IsDown())
		{
			if (isDuringInflationScene)
			{
				TargetPregPlusSize -= 2f;
				_inflationChange = TargetPregPlusSize;
				MeshInflate(TargetPregPlusSize, "WatchForUserKeyPress");
			}
			else
			{
				float inflationSize2 = infConfig.inflationSize - 2f;
				MeshInflate(inflationSize2, "WatchForUserKeyPress");
			}
		}
		value = PregnancyPlusPlugin.StoryModeInflationReset.Value;
		if (((KeyboardShortcut)(ref value)).IsDown())
		{
			MeshInflate(0f, "WatchForUserKeyPress");
		}
	}

	internal void ReadAndSetCardData()
	{
		infConfig = GetCardData();
		if (PregnancyPlusPlugin.DebugLog.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)(" ReadAndSetCardData > " + infConfig.ValuesToString()));
		}
		initialized = true;
	}

	internal PregnancyPlusData GetCardData()
	{
		PluginData extendedData = GetExtendedData();
		PregnancyPlusData pregnancyPlusData = PregnancyPlusData.Load(extendedData);
		if (pregnancyPlusData == null)
		{
			pregnancyPlusData = new PregnancyPlusData();
			pregnancyPlusData.pluginVersion = "7.8";
		}
		return pregnancyPlusData;
	}

	internal void OnCoordinateLoaded()
	{
		if (initialized)
		{
			((MonoBehaviour)this).StartCoroutine(WaitForClothMeshToSettle(0.05f, checkNewClothMesh: true));
		}
	}

	internal IEnumerator WaitForClothMeshToSettle(float waitTime = 0.05f, bool checkNewClothMesh = false, bool forceRecalcVerts = false, bool checkNewAccessoryMesh = false, int identifier = -1)
	{
		Guid guid = (debounceGuid = Guid.NewGuid());
		yield return (object)new WaitForSeconds(waitTime);
		yield return (object)new WaitWhile((Func<bool>)(() => isReloading));
		yield return (object)new WaitForSeconds(0.1f);
		if (debounceGuid == guid)
		{
			CheckMeshVisibility();
			if (!infConfig.UseOldCalcLogic())
			{
				yield return (object)new WaitForEndOfFrame();
			}
			string text = ((identifier >= 0) ? $": {identifier}" : "N/A");
			string callee = "WaitForMeshToSettle(" + (checkNewClothMesh ? "kind" : "slot") + text + ")";
			if (checkNewClothMesh || forceRecalcVerts)
			{
				List<SkinnedMeshRenderer> meshRenderers = PregnancyPlusHelper.GetMeshRenderers(((ChaInfo)base.ChaControl).objClothes);
				clothOffsetGui.OnClothingChanged(meshRenderers);
			}
			MeshInflate(new MeshInflateFlags(this, _checkForNewMesh: false, forceRecalcVerts, _pluginConfigSliderChanged: false, _visibilityUpdate: false, _bypassWhen0: false, _reMeasure: false, _uncensorChanged: false, checkNewClothMesh, checkNewAccessoryMesh), callee);
		}
	}

	internal void CheckMeshVisibility()
	{
		if ((Object)(object)((ChaInfo)base.ChaControl).rendBody == (Object)null || !((ChaInfo)base.ChaControl).rendBody.isVisible)
		{
			lastVisibleState = false;
		}
	}

	internal void CheckBellyPreservation()
	{
		if (StudioAPI.InsideStudio && (infConfigHistory.HasAnyValue() || infConfigHistory.HasBlendShape()))
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogInfo((object)" -Character changed in place, using source's belly shape");
			}
			infConfig = infConfigHistory;
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogInfo((object)(" Updating Card Data > " + infConfig.ValuesToString()));
			}
		}
		if (!MakerAPI.InsideMaker || (!infConfigHistory.HasAnyValue() && !infConfigHistory.HasBlendShape()))
		{
			return;
		}
		if (PregnancyPlusPlugin.PerferTargetBelly.Value)
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogInfo((object)" -Character changed in place, using target's belly shape");
			}
			return;
		}
		if (PregnancyPlusPlugin.DebugLog.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)" -Character changed in place, using source's belly shape");
		}
		infConfig = infConfigHistory;
		if (PregnancyPlusPlugin.DebugLog.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)(" Updating Card Data > " + infConfig.ValuesToString()));
		}
	}

	internal void CheckBlendShapes()
	{
		if (uncensorChanged)
		{
			uncensorChanged = false;
			if (StudioAPI.InsideStudio || MakerAPI.InsideMaker)
			{
				ResetInflation();
			}
			((MonoBehaviour)this).StartCoroutine(ILoadBlendshapes(0.1f, checkUncensor: true));
		}
		else
		{
			bool checkUncensor = firstStart && (StudioAPI.InsideStudio || MakerAPI.InsideMaker);
			((MonoBehaviour)this).StartCoroutine(ILoadBlendshapes(0f, checkUncensor));
		}
	}

	public void MeshInflate(float inflationSize, string callee, MeshInflateFlags meshInflateFlags = null)
	{
		infConfig.inflationSize = Mathf.Clamp(inflationSize, 0f, 40f);
		if (meshInflateFlags == null)
		{
			meshInflateFlags = new MeshInflateFlags(this);
		}
		meshInflateFlags.infConfig = infConfig;
		MeshInflate(meshInflateFlags, callee);
	}

	public void MeshInflate(PregnancyPlusData cardData, string callee, MeshInflateFlags meshInflateFlags = null)
	{
		infConfig = cardData;
		if (meshInflateFlags == null)
		{
			meshInflateFlags = new MeshInflateFlags(this);
		}
		meshInflateFlags.infConfig = infConfig;
		MeshInflate(meshInflateFlags, callee);
	}

	public bool AllowedToInflate()
	{
		bool flag = PregnancyPlusPlugin.StoryMode != null && PregnancyPlusPlugin.StoryMode.Value;
		if (!StudioAPI.InsideStudio && !MakerAPI.InsideMaker)
		{
			if (flag)
			{
				return infConfig.GameplayEnabled;
			}
			return false;
		}
		return true;
	}

	internal float GetBellyButtonLocalHeight()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		return BoneChainYDistance("cf_j_foot_L", "cf_j_waist01");
	}

	internal float GetSphereRadius(float wasitToRibDist, float wasitWidth)
	{
		if (infConfig.IsPluginVersionBelow(6.0))
		{
			float num = Math.Min(wasitToRibDist / 1.25f, wasitWidth / 1.3f);
			float num2 = (infConfig.UseOldCalcLogic() ? 0.9f : 1f);
			return num * num2;
		}
		return 0.13f;
	}

	internal Vector3 GetBellyButtonOffsetVector(float currentHeight)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		return Vector3.up * GetBellyButtonOffset(currentHeight);
	}

	internal float GetBellyButtonOffset(float currentHeight)
	{
		return BellyButtonOffset * currentHeight;
	}

	internal float FastMagnitude(Vector3 firstPosition, Vector3 secondPosition)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = default(Vector3);
		val.x = firstPosition.x - secondPosition.x;
		val.y = firstPosition.y - secondPosition.y;
		val.z = firstPosition.z - secondPosition.z;
		return val.x * val.x + val.y * val.y + val.z * val.z;
	}

	public void CleanSlate()
	{
		ResetInflation();
		List<string> keysToRemove = new List<string>(md.Keys);
		RemoveRenderKeys(keysToRemove);
	}

	internal void RemoveRenderKeys(List<string> keysToRemove)
	{
		foreach (string item in keysToRemove)
		{
			RemoveRenderKey(item);
		}
	}

	internal void RemoveRenderKey(string keyToRemove)
	{
		if (md.ContainsKey(keyToRemove))
		{
			md.Remove(keyToRemove);
		}
	}

	internal string GetMeshKey(SkinnedMeshRenderer smr)
	{
		return PregnancyPlusHelper.GetMeshKey(smr);
	}

	public SkinnedMeshRenderer GetBodyMeshRenderer()
	{
		List<SkinnedMeshRenderer> meshRenderers = PregnancyPlusHelper.GetMeshRenderers(((ChaInfo)base.ChaControl).objBody, findAll: true);
		List<SkinnedMeshRenderer> list = meshRenderers.FindAll((SkinnedMeshRenderer x) => ((x != null) ? ((Object)x).name : null) == BodyMeshName);
		if (list == null || list.Count <= 0)
		{
			PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(charaFileName, ErrorCode.PregPlus_NoBodyMesh, " The body skin could not be found for this character: " + charaFileName + ".  That can't be healthy...");
			return null;
		}
		if (list.Count > 1 && PregnancyPlusPlugin.DebugLog.Value)
		{
			PregnancyPlusPlugin.Logger.LogWarning((object)(" More than one body mesh was found under .objBody, returning the first for " + charaFileName));
		}
		return list[0];
	}

	public bool IsBodySmrActive()
	{
		SkinnedMeshRenderer bodyMeshRenderer = GetBodyMeshRenderer();
		if (!Object.op_Implicit((Object)(object)bodyMeshRenderer))
		{
			return false;
		}
		return ((Renderer)bodyMeshRenderer).enabled;
	}

	public bool BodyNestedUnderCloth(SkinnedMeshRenderer smr, SkinnedMeshRenderer bodySmr)
	{
		if ((Object)(object)bodySmr == (Object)null)
		{
			return false;
		}
		if (((Renderer)smr).enabled && !((Renderer)bodySmr).enabled)
		{
			return ((Object)smr).name.Contains(BodyMeshName);
		}
		return false;
	}

	public void logCharMeshInfo(MeshData md, SkinnedMeshRenderer smr, Vector3 sphereCenter, bool isClothingMesh = false)
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		if (PregnancyPlusPlugin.DebugCalcs.Value)
		{
			ManualLogSource logger = PregnancyPlusPlugin.Logger;
			object[] obj = new object[14]
			{
				((Object)smr).name,
				((Component)base.ChaControl).transform.position,
				Round(((Component)smr).transform.position),
				((Component)smr).transform.localRotation,
				((Component)smr).transform.localScale,
				null,
				null,
				null,
				null,
				null,
				null,
				null,
				null,
				null
			};
			Transform rootBone = smr.rootBone;
			obj[5] = ((rootBone != null) ? ((Object)rootBone).name : null);
			Bounds val = smr.localBounds;
			obj[6] = Round(((Bounds)(ref val)).center);
			val = smr.sharedMesh.bounds;
			obj[7] = Round(((Bounds)(ref val)).center);
			obj[8] = bellyInfo.BellyButtonHeight;
			obj[9] = sphereCenter;
			obj[10] = isClothingMesh;
			obj[11] = MeshOffSet.GetMeshOffsetType(smr).ToString();
			obj[12] = smr.sharedMesh.vertexCount;
			obj[13] = smr.sharedMesh.isReadable;
			logger.LogInfo((object)string.Format(" \r\n    ******CharMeshInfo****** {0}       \r\n    ChaControl.position   {1}\r\n    smr.position          {2}\r\n    smr.lRotation         {3}\r\n    smr.lScale            {4}\r\n    smr.rootBone.name     {5}\r\n    smr.localBounds       {6}   \r\n    smr.bounds            {7}\r\n    bbHeight              {8}\r\n    sphereCenter          {9}\r\n    isClothingMesh        {10}\r\n    MeshOffsetType        {11}\r\n    VertexCount           {12}\r\n    mesh.isReadable       {13}\r\n    ************************\r\n             ", obj));
		}
	}

	public string Round(Vector3 vector, int digits = 4)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		return $"({Math.Round(vector.x, digits)}, {Math.Round(vector.y, digits)}, {Math.Round(vector.z, digits)})";
	}

	internal Vector3 GetUserMoveTransform()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		return Vector3.up * GetInflationMoveY() + Vector3.forward * GetInflationMoveZ();
	}

	internal Vector3 SculptBaseShape(Vector3 originalVerticeLs, Vector3 smoothedVectorLs, Vector3 sphereCenterLs)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		Vector2 val = default(Vector2);
		((Vector2)(ref val))..ctor(sphereCenterLs.x, sphereCenterLs.y);
		Vector2 val2 = default(Vector2);
		((Vector2)(ref val2))..ctor(originalVerticeLs.x, originalVerticeLs.y);
		Vector2 val3 = default(Vector2);
		((Vector2)(ref val3))..ctor(smoothedVectorLs.x, smoothedVectorLs.y);
		float num = Vector2.Distance(val, val3) / ((bellyInfo.OriginalSphereRadius + bellyInfo.OriginalSphereRadius) * 5f);
		Vector3 val4 = Vector3.Lerp(Vector2.op_Implicit(val3), Vector2.op_Implicit(val2), num);
		return new Vector3(val4.x, val4.y, smoothedVectorLs.z);
	}

	internal Vector3 GetUserShiftTransform(PregnancyPlusData infConfigClone, Vector3 smoothedVectorLs, Vector3 sphereCenterLs, float skinToCenterDist)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		if (GetInflationShiftY(infConfigClone) != 0f)
		{
			float num = smoothedVectorLs.y - sphereCenterLs.y;
			float num2 = Mathf.Lerp(GetInflationShiftY(infConfigClone), GetInflationShiftY(infConfigClone) / 4f, Math.Abs(num / (skinToCenterDist * 1.8f)));
			Vector3 val = smoothedVectorLs + Vector3.up * num2;
			float num3 = Math.Abs(smoothedVectorLs.x - sphereCenterLs.x);
			Vector3 val2 = Vector3.Slerp(val, smoothedVectorLs, Math.Abs(num3 / (skinToCenterDist * 3f) + 0.1f));
			smoothedVectorLs = val2;
		}
		if (GetInflationShiftZ(infConfigClone) != 0f)
		{
			float num4 = Mathf.Lerp(0f, GetInflationShiftZ(infConfigClone), (smoothedVectorLs.z - sphereCenterLs.z) / (skinToCenterDist * 2f));
			Vector3 val3 = smoothedVectorLs + Vector3.forward * num4;
			smoothedVectorLs = val3;
		}
		return smoothedVectorLs;
	}

	internal Vector3 GetUserStretchXTransform(PregnancyPlusData infConfigClone, Vector3 smoothedVectorLs, Vector3 sphereCenterLs)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		float num = smoothedVectorLs.x - sphereCenterLs.x;
		float num2 = num * (GetInflationStretchX(infConfigClone) + 1f);
		smoothedVectorLs.x = (sphereCenterLs + Vector3.right * num2).x;
		return smoothedVectorLs;
	}

	internal Vector3 GetUserStretchYTransform(PregnancyPlusData infConfigClone, Vector3 smoothedVectorLs, Vector3 sphereCenterLs)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		float num = smoothedVectorLs.y - sphereCenterLs.y;
		float num2 = num * (GetInflationStretchY(infConfigClone) + 1f);
		smoothedVectorLs.y = (sphereCenterLs + Vector3.up * num2).y;
		return smoothedVectorLs;
	}

	internal Vector3 GetUserTaperYTransform(PregnancyPlusData infConfigClone, Vector3 smoothedVectorLs, Vector3 sphereCenterLs, float skinToCenterDist)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		float num = smoothedVectorLs.y - sphereCenterLs.y;
		float num2 = smoothedVectorLs.x - sphereCenterLs.x;
		bool flag = num > 0f;
		bool flag2 = num2 > 0f;
		float num3 = Mathf.Lerp(0f, GetInflationTaperY(infConfigClone), Math.Abs(num) / skinToCenterDist);
		num3 = Mathf.Lerp(0f, num3, Math.Abs(num2) / skinToCenterDist);
		num3 = (flag2 ? num3 : (0f - num3));
		num3 = (flag ? num3 : (0f - num3));
		smoothedVectorLs.x = (smoothedVectorLs + Vector3.right * num3).x;
		return smoothedVectorLs;
	}

	internal Vector3 GetUserTaperZTransform(PregnancyPlusData infConfigClone, Vector3 originalVerticeLs, Vector3 smoothedVectorLs, Vector3 sphereCenterLs, float skinToCenterDist, Vector3 backExtentPosLs)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		float num = smoothedVectorLs.y - sphereCenterLs.y;
		float num2 = smoothedVectorLs.z - sphereCenterLs.z;
		bool flag = num > 0f;
		float num3 = Mathf.Lerp(0f, GetInflationTaperZ(infConfigClone), Math.Abs(num) / skinToCenterDist);
		num3 = (flag ? num3 : (0f - num3));
		Vector3 val = smoothedVectorLs + Vector3.forward * num3;
		float num4 = originalVerticeLs.z - backExtentPosLs.z;
		Vector3 val2 = Vector3.Lerp(smoothedVectorLs, val, num4 / skinToCenterDist - skinToCenterDist / 2f);
		smoothedVectorLs = Vector3.Lerp(val2, val, num2 / skinToCenterDist);
		return smoothedVectorLs;
	}

	internal Vector3 GetUserFatFoldTransform(PregnancyPlusData infConfigClone, Vector3 originalVerticeLs, Vector3 smoothedVectorLs, Vector3 sphereCenterLs, float sphereRadius, ThreadsafeCurve bellyGapLerpAC)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = smoothedVectorLs;
		float inflationFatFold = GetInflationFatFold(infConfigClone);
		float inflationFatFoldGap = GetInflationFatFoldGap(infConfigClone);
		float sphereRadius2 = bellyInfo.SphereRadius;
		float num = GetInflationFatFoldHeight(infConfigClone) * sphereRadius2;
		float num2 = Math.Abs(smoothedVectorLs.y - (sphereCenterLs.y + num));
		Vector3 val2 = smoothedVectorLs;
		if (num2 <= sphereRadius2)
		{
			smoothedVectorLs = Vector3.Slerp(originalVerticeLs, smoothedVectorLs, num2 / sphereRadius2 + (inflationFatFold - 1f));
			if (inflationFatFoldGap != 0f)
			{
				float num3 = smoothedVectorLs.y - (sphereCenterLs.y + num);
				float value = Math.Abs(smoothedVectorLs.x - sphereCenterLs.x);
				float num4 = bellyGapLerpAC.Evaluate(Math.Abs(num3) / bellyInfo.OriginalSphereRadius);
				float num5 = Mathf.LerpAngle(0f, 1f, (Math.Abs(value) - bellyInfo.OriginalSphereRadius / 2f) / bellyInfo.OriginalSphereRadius);
				float num6 = Mathf.Lerp(num3, 0f, num4);
				num6 = Mathf.Lerp(num6, 0f, num5);
				float num7 = Mathf.Lerp(0f, num6, Math.Abs(inflationFatFoldGap * 0.66f) / num4);
				smoothedVectorLs.y += num7 * (float)((inflationFatFoldGap > 0f) ? 1 : (-1));
			}
		}
		if (smoothedVectorLs.y > sphereCenterLs.y + num)
		{
			smoothedVectorLs = Vector3.Slerp(smoothedVectorLs, originalVerticeLs, num2 / (sphereRadius2 * 1.5f));
		}
		return smoothedVectorLs;
	}

	internal Vector3 GetUserRoundnessTransform(PregnancyPlusData infConfigClone, Vector3 originalVerticeLs, Vector3 smoothedVectorLs, Vector3 sphereCenterLs, float skinToCenterDist, ThreadsafeCurve bellyEdgeAC)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		float num = smoothedVectorLs.z - sphereCenterLs.z;
		float num2 = Mathf.Lerp(0f, GetInflationRoundness(infConfigClone), (num - bellyInfo.WaistThick / 2f) / bellyInfo.SphereRadius);
		float num3 = Mathf.Lerp(num2, 0f, bellyEdgeAC.Evaluate(skinToCenterDist / bellyInfo.SphereRadius));
		Vector3 val = smoothedVectorLs - (sphereCenterLs + Vector3.forward * (bellyInfo.SphereRadius / 3f));
		Vector3 normalized = ((Vector3)(ref val)).normalized;
		return smoothedVectorLs + normalized * num3;
	}

	internal Vector3 GetUserDropTransform(PregnancyPlusData infConfigClone, Vector3 meshRootTfUp, Vector3 smoothedVectorLs, Vector3 sphereCenterLs, float skinToCenterDist, float sphereRadius)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		float num = Mathf.Lerp(0f, GetInflationDrop(infConfigClone), (smoothedVectorLs.z - sphereCenterLs.z) / (bellyInfo.SphereRadius * 1.5f));
		return smoothedVectorLs + meshRootTfUp * (0f - sphereRadius * num);
	}

	internal Vector3 RoundToSides(Vector3 originalVerticeLs, Vector3 smoothedVectorLs, Vector3 backExtentPosLs, ThreadsafeCurve bellySidesAC)
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		float num = bellyInfo.OriginalSphereRadius / 1.8f;
		float num2 = bellyInfo.SphereRadius / 2.5f;
		float num3 = ((num2 > num) ? (num + (num2 - num) / 3f) : num);
		float num4 = originalVerticeLs.z - backExtentPosLs.z;
		if (num4 > num3)
		{
			return smoothedVectorLs;
		}
		return Vector3.Lerp(originalVerticeLs, smoothedVectorLs, bellySidesAC.Evaluate(num4 / num3));
	}

	internal Vector3 ReduceRibStretchingZ(Vector3 originalVerticeLs, Vector3 smoothedVectorLs, Vector3 topExtentPosLs, ThreadsafeCurve bellyTopAC)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		float yLimitOffset = bellyInfo.YLimitOffset;
		if (originalVerticeLs.y > topExtentPosLs.y)
		{
			return originalVerticeLs;
		}
		if (originalVerticeLs.y >= topExtentPosLs.y - yLimitOffset)
		{
			float num = topExtentPosLs.y - originalVerticeLs.y;
			float num2 = bellyTopAC.Evaluate(num / yLimitOffset);
			Vector3 val = Vector3.Lerp(originalVerticeLs, smoothedVectorLs, num2);
			smoothedVectorLs.z = val.z;
		}
		return smoothedVectorLs;
	}

	internal float GetInflationMultiplier(PregnancyPlusData infConfigInstance = null)
	{
		float num = infConfigInstance?.inflationMultiplier ?? infConfig.inflationMultiplier;
		if (StudioAPI.InsideStudio || MakerAPI.InsideMaker)
		{
			return num;
		}
		float num2 = ((PregnancyPlusPlugin.StoryModeInflationMultiplier != null) ? PregnancyPlusPlugin.StoryModeInflationMultiplier.Value : 0f);
		return num + num2;
	}

	internal float GetInflationMoveY(PregnancyPlusData infConfigInstance = null)
	{
		float num = infConfigInstance?.inflationMoveY ?? infConfig.inflationMoveY;
		if (StudioAPI.InsideStudio || MakerAPI.InsideMaker)
		{
			return num;
		}
		float num2 = ((PregnancyPlusPlugin.StoryModeInflationMoveY != null) ? PregnancyPlusPlugin.StoryModeInflationMoveY.Value : 0f);
		return num + num2;
	}

	internal float GetInflationMoveZ(PregnancyPlusData infConfigInstance = null)
	{
		float num = infConfigInstance?.inflationMoveZ ?? infConfig.inflationMoveZ;
		if (StudioAPI.InsideStudio || MakerAPI.InsideMaker)
		{
			return num;
		}
		float num2 = ((PregnancyPlusPlugin.StoryModeInflationMoveZ != null) ? PregnancyPlusPlugin.StoryModeInflationMoveZ.Value : 0f);
		return num + num2;
	}

	internal float GetInflationStretchX(PregnancyPlusData infConfigInstance = null)
	{
		float num = infConfigInstance?.inflationStretchX ?? infConfig.inflationStretchX;
		if (StudioAPI.InsideStudio || MakerAPI.InsideMaker)
		{
			return num;
		}
		float num2 = ((PregnancyPlusPlugin.StoryModeInflationStretchX != null) ? PregnancyPlusPlugin.StoryModeInflationStretchX.Value : 0f);
		return num + num2;
	}

	internal float GetInflationStretchY(PregnancyPlusData infConfigInstance = null)
	{
		float num = infConfigInstance?.inflationStretchY ?? infConfig.inflationStretchY;
		if (StudioAPI.InsideStudio || MakerAPI.InsideMaker)
		{
			return num;
		}
		float num2 = ((PregnancyPlusPlugin.StoryModeInflationStretchY != null) ? PregnancyPlusPlugin.StoryModeInflationStretchY.Value : 0f);
		return num + num2;
	}

	internal float GetInflationShiftY(PregnancyPlusData infConfigInstance = null)
	{
		float num = infConfigInstance?.inflationShiftY ?? infConfig.inflationShiftY;
		if (StudioAPI.InsideStudio || MakerAPI.InsideMaker)
		{
			return num;
		}
		float num2 = ((PregnancyPlusPlugin.StoryModeInflationShiftY != null) ? PregnancyPlusPlugin.StoryModeInflationShiftY.Value : 0f);
		return num + num2;
	}

	internal float GetInflationShiftZ(PregnancyPlusData infConfigInstance = null)
	{
		float num = infConfigInstance?.inflationShiftZ ?? infConfig.inflationShiftZ;
		if (StudioAPI.InsideStudio || MakerAPI.InsideMaker)
		{
			return num;
		}
		float num2 = ((PregnancyPlusPlugin.StoryModeInflationShiftZ != null) ? PregnancyPlusPlugin.StoryModeInflationShiftZ.Value : 0f);
		return num + num2;
	}

	internal float GetInflationTaperY(PregnancyPlusData infConfigInstance = null)
	{
		float num = infConfigInstance?.inflationTaperY ?? infConfig.inflationTaperY;
		if (StudioAPI.InsideStudio || MakerAPI.InsideMaker)
		{
			return num;
		}
		float num2 = ((PregnancyPlusPlugin.StoryModeInflationTaperY != null) ? PregnancyPlusPlugin.StoryModeInflationTaperY.Value : 0f);
		return num + num2;
	}

	internal float GetInflationTaperZ(PregnancyPlusData infConfigInstance = null)
	{
		float num = infConfigInstance?.inflationTaperZ ?? infConfig.inflationTaperZ;
		if (StudioAPI.InsideStudio || MakerAPI.InsideMaker)
		{
			return num;
		}
		float num2 = ((PregnancyPlusPlugin.StoryModeInflationTaperZ != null) ? PregnancyPlusPlugin.StoryModeInflationTaperZ.Value : 0f);
		return num + num2;
	}

	internal float GetInflationDrop(PregnancyPlusData infConfigInstance = null)
	{
		float num = infConfigInstance?.inflationDrop ?? infConfig.inflationDrop;
		if (StudioAPI.InsideStudio || MakerAPI.InsideMaker)
		{
			return num;
		}
		float num2 = ((PregnancyPlusPlugin.StoryModeInflationDrop != null) ? PregnancyPlusPlugin.StoryModeInflationDrop.Value : 0f);
		return num + num2;
	}

	internal float GetInflationClothOffset(PregnancyPlusData infConfigInstance = null)
	{
		float num = infConfigInstance?.inflationClothOffset ?? infConfig.inflationClothOffset;
		if (StudioAPI.InsideStudio || MakerAPI.InsideMaker)
		{
			return num;
		}
		float num2 = ((PregnancyPlusPlugin.StoryModeInflationClothOffset != null) ? PregnancyPlusPlugin.StoryModeInflationClothOffset.Value : 0f);
		return num + num2;
	}

	internal float GetInflationFatFold(PregnancyPlusData infConfigInstance = null)
	{
		float num = infConfigInstance?.inflationFatFold ?? infConfig.inflationFatFold;
		if (StudioAPI.InsideStudio || MakerAPI.InsideMaker)
		{
			return num;
		}
		float num2 = ((PregnancyPlusPlugin.StoryModeInflationFatFold != null) ? PregnancyPlusPlugin.StoryModeInflationFatFold.Value : 0f);
		return num + num2;
	}

	internal float GetInflationFatFoldHeight(PregnancyPlusData infConfigInstance = null)
	{
		float num = infConfigInstance?.inflationFatFoldHeight ?? infConfig.inflationFatFoldHeight;
		if (StudioAPI.InsideStudio || MakerAPI.InsideMaker)
		{
			return num;
		}
		float num2 = ((PregnancyPlusPlugin.StoryModeInflationFatFoldHeight != null) ? PregnancyPlusPlugin.StoryModeInflationFatFoldHeight.Value : 0f);
		return num + num2;
	}

	internal float GetInflationFatFoldGap(PregnancyPlusData infConfigInstance = null)
	{
		float num = infConfigInstance?.inflationFatFoldGap ?? infConfig.inflationFatFoldGap;
		if (StudioAPI.InsideStudio || MakerAPI.InsideMaker)
		{
			return num;
		}
		float num2 = ((PregnancyPlusPlugin.StoryModeInflationFatFoldGap != null) ? PregnancyPlusPlugin.StoryModeInflationFatFoldGap.Value : 0f);
		return num + num2;
	}

	internal float GetInflationRoundness(PregnancyPlusData infConfigInstance = null)
	{
		float num = infConfigInstance?.inflationRoundness ?? infConfig.inflationRoundness;
		if (StudioAPI.InsideStudio || MakerAPI.InsideMaker)
		{
			return num;
		}
		float num2 = ((PregnancyPlusPlugin.StoryModeInflationRoundness != null) ? PregnancyPlusPlugin.StoryModeInflationRoundness.Value : 0f);
		return num + num2;
	}

	internal bool ApplyInflation(SkinnedMeshRenderer smr, string renderKey, bool needsOverwrite, string blendShapeTag = null, bool bypassWhen0 = false)
	{
		if ((Object)(object)smr == (Object)null)
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogWarning((object)"ApplyInflation > smr is null");
			}
			return false;
		}
		float num = infConfig.inflationSize;
		if (isDuringInflationScene && !bypassWhen0)
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogInfo((object)$"ApplyInflation > using CurrentInflationChange instead {CurrentInflationChange}");
			}
			num = CurrentInflationChange;
		}
		if (!bypassWhen0 && (num.Equals(null) || num == 0f))
		{
			return false;
		}
		if (!md.TryGetValue(renderKey, out var value) || !value.HasOriginalVerts)
		{
			RemoveRenderKey(renderKey);
			return false;
		}
		if (!smr.sharedMesh.isReadable)
		{
			nativeDetour.Apply();
		}
		if (md[renderKey].VertexCount != smr.sharedMesh.vertexCount)
		{
			PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(charaFileName, ErrorCode.PregPlus_IncorrectVertCount, $"ApplyInflation > smr.sharedMesh '{renderKey}' has incorrect vert count {md[renderKey].VertexCount}|{smr.sharedMesh.vertexCount}");
			nativeDetour.Undo();
			return false;
		}
		nativeDetour.Undo();
		bool flag = ApplyBlendShapeWeight(smr, renderKey, needsOverwrite, blendShapeTempTagName);
		if (PregnancyPlusPlugin.DebugLog.Value && flag)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)(" Did ApplyInflation to " + ((Object)smr).name));
		}
		return flag;
	}

	internal void ResetInflation()
	{
		List<string> list = new List<string>(md.Keys);
		foreach (string item in list)
		{
			SkinnedMeshRenderer meshRenderer = PregnancyPlusHelper.GetMeshRenderer(base.ChaControl, item);
			if (!((Object)(object)meshRenderer == (Object)null))
			{
				ResetBlendShapeWeight(meshRenderer, item, blendShapeTempTagName);
			}
		}
	}

	internal async Task ApplySmoothing(bool includeClothMesh = false)
	{
		if (!infConfig.HasAnyValue() || infConfig.inflationSize == 0f)
		{
			return;
		}
		if (PregnancyPlusPlugin.DebugLog.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)$" ApplySmoothing({includeClothMesh})");
		}
		PregnancyPlusGui.StartTextCountIncrement();
		if (includeClothMesh)
		{
			List<string> list = new List<string>(md.Keys);
			foreach (string item in list)
			{
				SkinnedMeshRenderer meshRenderer = PregnancyPlusHelper.GetMeshRenderer(base.ChaControl, item, searchInactive: false);
				await SmoothSingleMesh(meshRenderer, item);
			}
		}
		else
		{
			SkinnedMeshRenderer meshRendererByName = PregnancyPlusHelper.GetMeshRendererByName(base.ChaControl, BodyMeshName);
			string meshKey = GetMeshKey(meshRendererByName);
			await SmoothSingleMesh(meshRendererByName, meshKey);
		}
		PregnancyPlusGui.StopTextCountIncrement();
	}

	internal async Task<bool> SmoothSingleMesh(SkinnedMeshRenderer smr, string renderKey)
	{
		if ((Object)(object)smr == (Object)null || renderKey == null)
		{
			return false;
		}
		if (!md.ContainsKey(renderKey) || !md[renderKey].HasInflatedVerts)
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogWarning((object)(" No inflated verts found for SmoothSingleMesh " + ((Object)smr).name));
			}
			return false;
		}
		Mesh meshCopyTarget = PregnancyPlusHelper.CopyMesh(smr.sharedMesh);
		if (!meshCopyTarget.isReadable)
		{
			nativeDetour.Apply();
		}
		meshCopyTarget.vertices = md[renderKey]._inflatedVertices;
		nativeDetour.Undo();
		Vector3[] newVerts = (Vector3[])(object)new Vector3[md[renderKey].VertexCount];
		await Task.Run(delegate
		{
			if (!meshCopyTarget.isReadable)
			{
				nativeDetour.Apply();
			}
			newVerts = SmoothMesh.Start(meshCopyTarget, md[renderKey].alteredVerticieIndexes);
			nativeDetour.Undo();
		});
		await ApplySmoothResults(newVerts, renderKey, smr);
		return true;
	}

	internal async Task ApplySmoothResults(Vector3[] newMesh, string renderKey, SkinnedMeshRenderer smr = null)
	{
		if (newMesh != null)
		{
			md[renderKey].smoothedVertices = newMesh;
		}
		if ((Object)(object)smr == (Object)null)
		{
			smr = PregnancyPlusHelper.GetMeshRenderer(base.ChaControl, renderKey, searchInactive: false);
		}
		if (!((Object)(object)smr == (Object)null))
		{
			MeshInflateFlags meshInflateFlags = new MeshInflateFlags(this, _checkForNewMesh: false, _freshStart: true);
			await ComputeDeltas(smr, renderKey, meshInflateFlags);
			FinalizeInflation(smr, meshInflateFlags, blendShapeTempTagName);
		}
	}

	public void MeshInflate(MeshInflateFlags meshInflateFlags, string callee)
	{
		if (isProcessing)
		{
			lastMeshInflateFlags = (MeshInflateFlags)meshInflateFlags.Clone();
		}
		else
		{
			if ((Object)(object)((ChaInfo)base.ChaControl).objBodyBone == (Object)null || (!PregnancyPlusPlugin.AllowMale.Value && ((ChaInfo)base.ChaControl).sex == 0) || !meshInflateFlags.NeedsToRun || !AllowedToInflate())
			{
				return;
			}
			if (!infConfig.GameplayEnabled)
			{
				if (infConfig.inflationSize > 0f)
				{
					Dictionary<string, MeshData> dictionary = md;
					if (dictionary != null && dictionary.Keys.Count > 0)
					{
						CleanSlate();
					}
				}
				return;
			}
			if (meshInflateFlags.freshStart)
			{
				CleanSlate();
			}
			if (infConfig.inflationSize <= 0f && !meshInflateFlags.bypassWhen0 && !isDuringInflationScene)
			{
				infConfigHistory.inflationSize = 0f;
				ResetInflation();
				return;
			}
			if (PregnancyPlusPlugin.DebugLog.Value || PregnancyPlusPlugin.DebugCalcs.Value)
			{
				PregnancyPlusPlugin.Logger.LogInfo((object)" ");
			}
			if (PregnancyPlusPlugin.DebugLog.Value || PregnancyPlusPlugin.DebugCalcs.Value)
			{
				PregnancyPlusPlugin.Logger.LogInfo((object)(" ---------- " + callee + "() "));
			}
			if (PregnancyPlusPlugin.DebugLog.Value || PregnancyPlusPlugin.DebugCalcs.Value)
			{
				PregnancyPlusPlugin.Logger.LogInfo((object)$" inflationSize > {Math.Round(infConfig.inflationSize, 2)} for {charaFileName} ");
			}
			meshInflateFlags.Log();
			if (PregnancyPlusPlugin.DebugLog.Value || PregnancyPlusPlugin.DebugCalcs.Value)
			{
				PregnancyPlusPlugin.Logger.LogInfo((object)" ");
			}
			bool flag = MeasureWaistAndSphere(base.ChaControl, meshInflateFlags.reMeasure);
			if (!flag && lastVisibleState)
			{
				PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(charaFileName, ErrorCode.PregPlus_BadMeasurement, "Could not get one or more belly measurements from character (This is normal when a character is loaded but inactive)");
			}
			else if (!flag && !lastVisibleState)
			{
				if (PregnancyPlusPlugin.DebugLog.Value)
				{
					PregnancyPlusPlugin.Logger.LogInfo((object)(" Character not visible, can't measure yet " + charaFileName));
				}
			}
			else
			{
				DoInflation(meshInflateFlags);
			}
		}
	}

	internal async void DoInflation(MeshInflateFlags meshInflateFlags)
	{
		if (isProcessing)
		{
			lastMeshInflateFlags = (MeshInflateFlags)meshInflateFlags.Clone();
			return;
		}
		isProcessing = true;
		await FindAndAffectAllMeshes(meshInflateFlags, await FindAndAffectBodyMesh(meshInflateFlags));
		PostInflationDebug.Start(base.ChaControl, md, nativeDetour, bellyBones);
		isProcessing = false;
		if (lastMeshInflateFlags == null)
		{
			RemoveMeshCollider();
			return;
		}
		MeshInflateFlags meshInflateFlags2 = (MeshInflateFlags)lastMeshInflateFlags.Clone();
		lastMeshInflateFlags = null;
		MeshInflate(meshInflateFlags2, "lastMeshInflateFlags");
		RemoveMeshCollider();
	}

	internal async Task<string> FindAndAffectBodyMesh(MeshInflateFlags meshInflateFlags)
	{
		SkinnedMeshRenderer bodyMeshRenderer = GetBodyMeshRenderer();
		bindPoseList.ComputeBindPose(base.ChaControl, bodyMeshRenderer, meshInflateFlags.uncensorChanged);
		if (bindPoseList.bindPoses.Count <= 0)
		{
			return null;
		}
		await StartMeshChanges(bodyMeshRenderer, meshInflateFlags, isClothingMesh: false, null, isMainBody: true);
		return GetMeshKey(bodyMeshRenderer);
	}

	internal async Task FindAndAffectAllMeshes(MeshInflateFlags meshInflateFlags, string ignoreKey = null)
	{
		SkinnedMeshRenderer bodyMeshRenderer = GetBodyMeshRenderer();
		List<Task> list = new List<Task>();
		List<SkinnedMeshRenderer> meshRenderers = PregnancyPlusHelper.GetMeshRenderers(((ChaInfo)base.ChaControl).objBody, findAll: true);
		list.AddRange(LoopAndApplyMeshChanges(meshRenderers, meshInflateFlags, isClothingMesh: false, null, ignoreKey));
		if (!meshInflateFlags.checkForNewAcchMesh)
		{
			List<SkinnedMeshRenderer> meshRenderers2 = PregnancyPlusHelper.GetMeshRenderers(((ChaInfo)base.ChaControl).objClothes);
			list.AddRange(LoopAndApplyMeshChanges(meshRenderers2, meshInflateFlags, isClothingMesh: true, bodyMeshRenderer));
		}
		if (!PregnancyPlusPlugin.IgnoreAccessories.Value)
		{
			List<SkinnedMeshRenderer> meshRenderers3 = PregnancyPlusHelper.GetMeshRenderers(((ChaInfo)base.ChaControl).objAccessory);
			list.AddRange(LoopAndApplyMeshChanges(meshRenderers3, meshInflateFlags, isClothingMesh: true, bodyMeshRenderer));
			await Task.WhenAll(list);
		}
	}

	internal Task[] LoopAndApplyMeshChanges(List<SkinnedMeshRenderer> smrs, MeshInflateFlags meshInflateFlags, bool isClothingMesh = false, SkinnedMeshRenderer bodySmr = null, string ignoreKey = null)
	{
		Task[] array = new Task[smrs.Count];
		for (int i = 0; i < smrs.Count; i++)
		{
			array[i] = StartMeshChanges(smrs[i], meshInflateFlags, isClothingMesh, bodySmr, isMainBody: false, ignoreKey);
		}
		return array;
	}

	internal async Task StartMeshChanges(SkinnedMeshRenderer smr, MeshInflateFlags meshInflateFlags, bool isClothingMesh = false, SkinnedMeshRenderer bodySmr = null, bool isMainBody = false, string ignoreKey = null)
	{
		string renderKey = GetMeshKey(smr);
		if (renderKey != null && !(renderKey == ignoreKey))
		{
			if (NeedsComputeVerts(smr, renderKey, meshInflateFlags) && await ComputeVerts(smr, isClothingMesh, bodySmr, meshInflateFlags, renderKey, isMainBody))
			{
				await ComputeDeltas(smr, renderKey, meshInflateFlags);
			}
			if (!ignoreMeshList.Contains(renderKey))
			{
				FinalizeInflation(smr, meshInflateFlags, blendShapeTempTagName);
			}
		}
	}

	public bool NeedsComputeVerts(SkinnedMeshRenderer smr, string renderKey, MeshInflateFlags meshInflateFlags)
	{
		if (ignoreMeshList.Contains(renderKey))
		{
			return false;
		}
		if (meshInflateFlags.freshStart)
		{
			return true;
		}
		if (!md.TryGetValue(renderKey, out var value))
		{
			return true;
		}
		if (value.bellyVerticieIndexes.Length == smr.sharedMesh.vertexCount)
		{
			if (meshInflateFlags.OnlyInflationSizeChanged)
			{
				return false;
			}
			return meshInflateFlags.SliderHaveChanged;
		}
		return true;
	}

	internal async Task<bool> ComputeVerts(SkinnedMeshRenderer smr, bool isClothingMesh, SkinnedMeshRenderer bodyMeshRenderer, MeshInflateFlags meshInflateFlags, string renderKey, bool isMainBody = false)
	{
		string[] boneFilters = bellyBones;
		string[] boneExclusions = new string[2] { "cf_s_bust03_L", "cf_s_bust03_R" };
		bool flag = true;
		MeshData value;
		bool flag2 = md.TryGetValue(renderKey, out value);
		if (PregnancyPlusPlugin.MakeBalloon.Value || PregnancyPlusPlugin.DebugVerts.Value)
		{
			boneFilters = null;
			boneExclusions = null;
		}
		if (meshInflateFlags.NeedsToComputeIndex || !flag2)
		{
			flag = await GetFilteredVerticieIndexes(smr, boneFilters, boneExclusions);
		}
		if (ignoreMeshList.Contains(renderKey))
		{
			return false;
		}
		if (!flag)
		{
			return false;
		}
		md.TryGetValue(renderKey, out value);
		if (value.isFirstPass)
		{
			await ComputeBindPoseMesh(smr, bodyMeshRenderer, isClothingMesh, meshInflateFlags, isMainBody);
		}
		await GetInflatedVerticies(smr, bellyInfo.SphereRadius, isClothingMesh, bodyMeshRenderer, meshInflateFlags);
		return true;
	}

	internal void FinalizeInflation(SkinnedMeshRenderer smr, MeshInflateFlags meshInflateFlags, string blendShapeTag = null)
	{
		string meshKey = GetMeshKey(smr);
		bool flag = ApplyInflation(smr, meshKey, meshInflateFlags.OverWriteMesh, blendShapeTempTagName, meshInflateFlags.bypassWhen0);
		if (isDuringInflationScene)
		{
			AppendToQuickInflateList(smr);
		}
		if (flag)
		{
			infConfigHistory = (PregnancyPlusData)infConfig.Clone();
		}
	}

	internal async Task ComputeBindPoseMesh(SkinnedMeshRenderer smr, SkinnedMeshRenderer bodySmr, bool isClothingMesh, MeshInflateFlags meshInflateFlags, bool isMainBody = false)
	{
		if ((Object)(object)smr == (Object)null)
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogWarning((object)" ComputeBindPoseMesh smr was null");
			}
			return;
		}
		if (PregnancyPlusPlugin.DebugCalcs.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)(" Computing BindPoseMesh for " + ((Object)smr).name));
		}
		if (!smr.sharedMesh.isReadable)
		{
			nativeDetour.Apply();
		}
		string rendererName = GetMeshKey(smr);
		if (!md.TryGetValue(rendererName, out var _))
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogWarning((object)(" ComputeBindPoseMesh cant find MeshData for " + rendererName));
			}
			return;
		}
		if (md[rendererName].originalVertices == null || !md[rendererName].HasOriginalVerts)
		{
			md[rendererName].originalVertices = (Vector3[])(object)new Vector3[smr.sharedMesh.vertexCount];
		}
		Matrix4x4[] boneMatrices = null;
		BoneWeight[] boneWeights = null;
		Vector3[] unskinnedVerts = null;
		if (PregnancyPlusPlugin.ShowBindPose.Value)
		{
			PostInflationDebug.ShowBindPose(base.ChaControl, smr, bindPoseList);
			PostInflationDebug.ShowRawBindPose(smr);
		}
		boneMatrices = MeshSkinning.GetBoneMatrices(base.ChaControl, smr, bindPoseList);
		boneWeights = smr.sharedMesh.boneWeights;
		unskinnedVerts = smr.sharedMesh.vertices;
		_ = md[rendererName].originalVertices;
		bool[] bellyVertIndex = md[rendererName].bellyVerticieIndexes;
		_ = smr.sharedMesh.vertexCount;
		Matrix4x4 smrTfTransPt = ((Component)smr).transform.localToWorldMatrix;
		Vector3 val = GetSphereCenter() - GetUserMoveTransform();
		float yBottomLimit = val.y - bellyInfo.OriginalSphereRadius * 1.5f;
		float ySphereCenter = val.y;
		Matrix4x4 scale = BindPose.GetScale(smr);
		float z = Matrix.GetScale(((Matrix4x4)(ref scale)).inverse).z;
		float backExtent = z * (0f - bellyInfo.ZLimit);
		nativeDetour.Undo();
		await Task.Run(delegate
		{
			if (!md.TryGetValue(rendererName, out var _))
			{
				if (PregnancyPlusPlugin.DebugLog.Value)
				{
					PregnancyPlusPlugin.Logger.LogWarning((object)(" ComputeBindPoseMesh.Task.Run cant find MeshData for " + rendererName));
				}
			}
			else
			{
				md[rendererName].originalVertices = ((IList<Vector3>)unskinnedVerts).RunParallel((Func<Vector3, int, Vector3>)delegate(Vector3 _, int i)
				{
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					//IL_000d: Unknown result type (might be due to invalid IL or missing references)
					//IL_001f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0024: Unknown result type (might be due to invalid IL or missing references)
					//IL_0029: Unknown result type (might be due to invalid IL or missing references)
					//IL_0094: Unknown result type (might be due to invalid IL or missing references)
					//IL_007b: Unknown result type (might be due to invalid IL or missing references)
					//IL_003c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0092: Unknown result type (might be due to invalid IL or missing references)
					//IL_004a: Unknown result type (might be due to invalid IL or missing references)
					//IL_0058: Unknown result type (might be due to invalid IL or missing references)
					//IL_006f: Unknown result type (might be due to invalid IL or missing references)
					Vector3 val2 = MeshSkinning.UnskinnedToSkinnedVertex(unskinnedVerts[i], smrTfTransPt, boneMatrices, boneWeights[i]);
					if (isClothingMesh && !bellyVertIndex[i] && val2.z > backExtent && val2.y > yBottomLimit && val2.y < ySphereCenter)
					{
						bellyVertIndex[i] = true;
						return val2;
					}
					if (bellyVertIndex[i] && val2.z < backExtent)
					{
						bellyVertIndex[i] = false;
						return val2;
					}
					return val2;
				}, -1);
				md[rendererName].isFirstPass = false;
			}
		});
	}

	internal async Task GetInflatedVerticies(SkinnedMeshRenderer smr, float sphereRadius, bool isClothingMesh, SkinnedMeshRenderer bodySmr, MeshInflateFlags meshInflateFlags)
	{
		if ((Object)(object)smr == (Object)null)
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogWarning((object)" GetInflatedVerticies smr was null");
			}
			return;
		}
		if (PregnancyPlusPlugin.DebugCalcs.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)(" GetInflatedVerticies for " + ((Object)smr).name));
		}
		if (isClothingMesh && BodyNestedUnderCloth(smr, bodySmr))
		{
			PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(charaFileName, ErrorCode.PregPlus_BodyMeshDisguisedAsCloth, " body mesh " + ((Object)smr).name + " was nested under cloth object " + ((Object)((Component)smr).transform.parent).name + ".  This is usually not an issue.");
			isClothingMesh = false;
		}
		if (!smr.sharedMesh.isReadable)
		{
			nativeDetour.Apply();
		}
		string rendererName = GetMeshKey(smr);
		if (!md.TryGetValue(rendererName, out var _))
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogWarning((object)(" GetInflatedVerticies cant find MeshData for " + rendererName));
			}
			return;
		}
		md[rendererName].inflatedVertices = (Vector3[])(object)new Vector3[smr.sharedMesh.vertexCount];
		md[rendererName].alteredVerticieIndexes = new bool[smr.sharedMesh.vertexCount];
		Vector3 sphereCenter = GetSphereCenter();
		if (NeedsClothMeasurement(smr, bodySmr, sphereCenter, isClothingMesh))
		{
			CreateMeshCollider(bodySmr);
		}
		float[] clothOffsets = DoClothMeasurement(smr, bodySmr, sphereCenter);
		if (clothOffsets == null)
		{
			clothOffsets = new float[md[rendererName].originalVertices.Length];
		}
		Vector3[] origVerts = md[rendererName].originalVertices;
		_ = md[rendererName].inflatedVertices;
		bool[] bellyVertIndex = md[rendererName].bellyVerticieIndexes;
		bool[] alteredVerts = md[rendererName].alteredVerticieIndexes;
		_ = origVerts.Length;
		Vector3 preMorphSphereCenter = sphereCenter - GetUserMoveTransform();
		Vector3 backExtentPos = new Vector3(preMorphSphereCenter.x, sphereCenter.y, preMorphSphereCenter.z) + Vector3.forward * (0f - bellyInfo.ZLimit);
		Vector3 topExtentPos = new Vector3(preMorphSphereCenter.x, preMorphSphereCenter.y, preMorphSphereCenter.z) + Vector3.up * bellyInfo.YLimit;
		float vertNormalCaluRadius = sphereRadius + bellyInfo.OriginalSphereRadius / 20f;
		float waistWidth = bellyInfo.WaistWidth;
		PregnancyPlusData infConfigClone = (PregnancyPlusData)infConfig.Clone();
		ThreadsafeCurve bellySidesAC = new ThreadsafeCurve(BellySidesAC);
		ThreadsafeCurve bellyTopAC = new ThreadsafeCurve(BellyTopAC);
		ThreadsafeCurve bellyEdgeAC = new ThreadsafeCurve(BellyEdgeAC);
		ThreadsafeCurve bellyGapLerpAC = new ThreadsafeCurve(BellyGapLerpAC);
		List<KeyValuePair<string, float>> individualClothingOffsets = infConfig.IndividualClothingOffsets;
		bool flag = individualClothingOffsets?.Any((KeyValuePair<string, float> o) => o.Key == rendererName) ?? false;
		float individualOffset = (flag ? (individualClothingOffsets.FirstOrDefault((KeyValuePair<string, float> o) => o.Key == rendererName).Value * 4f) : 0f);
		logCharMeshInfo(md[rendererName], smr, sphereCenter, isClothingMesh);
		nativeDetour.Undo();
		await Task.Run(delegate
		{
			if (!md.TryGetValue(rendererName, out var _))
			{
				if (PregnancyPlusPlugin.DebugLog.Value)
				{
					PregnancyPlusPlugin.Logger.LogWarning((object)(" GetInflatedVerticies.Task.Run cant find MeshData for " + rendererName));
				}
			}
			else
			{
				md[rendererName].inflatedVertices = ((IList<Vector3>)origVerts).RunParallel((Func<Vector3, int, Vector3>)delegate(Vector3 _, int i)
				{
					//IL_002a: Unknown result type (might be due to invalid IL or missing references)
					//IL_002f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0030: Unknown result type (might be due to invalid IL or missing references)
					//IL_0032: Unknown result type (might be due to invalid IL or missing references)
					//IL_001d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0059: Unknown result type (might be due to invalid IL or missing references)
					//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
					//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
					//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
					//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
					//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
					//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
					//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
					//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
					//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
					//IL_0103: Unknown result type (might be due to invalid IL or missing references)
					//IL_0104: Unknown result type (might be due to invalid IL or missing references)
					//IL_0106: Unknown result type (might be due to invalid IL or missing references)
					//IL_0112: Unknown result type (might be due to invalid IL or missing references)
					//IL_011e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0124: Unknown result type (might be due to invalid IL or missing references)
					//IL_014a: Unknown result type (might be due to invalid IL or missing references)
					//IL_0098: Unknown result type (might be due to invalid IL or missing references)
					//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
					if (!bellyVertIndex[i] && !PregnancyPlusPlugin.DebugVerts.Value)
					{
						return origVerts[i];
					}
					Vector3 val = origVerts[i];
					float num = Vector3.Distance(val, sphereCenter);
					if (num > vertNormalCaluRadius && !PregnancyPlusPlugin.DebugVerts.Value)
					{
						return origVerts[i];
					}
					if (num <= vertNormalCaluRadius || PregnancyPlusPlugin.DebugVerts.Value)
					{
						alteredVerts[i] = true;
					}
					float num2 = 0f;
					if (isClothingMesh)
					{
						num2 = GetClothesFixOffset(infConfigClone, sphereCenter, sphereRadius, waistWidth, val, ((Object)smr).name, clothOffsets[i], individualOffset);
					}
					Vector3 val2 = val - sphereCenter;
					Vector3 inflatedVerticieLs = ((Vector3)(ref val2)).normalized * (sphereRadius + num2) + sphereCenter;
					Tuple<Vector3, bool> tuple = SculptInflatedVerticie(infConfigClone, val, inflatedVerticieLs, sphereCenter, waistWidth, preMorphSphereCenter, sphereRadius, backExtentPos, topExtentPos, bellySidesAC, bellyTopAC, bellyEdgeAC, bellyGapLerpAC);
					return tuple.Item1;
				}, -1);
			}
		});
	}

	internal Vector3 GetSphereCenter()
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		float num = 0.97f;
		bellyInfo.BellyButtonHeight = num;
		Vector3 val = Vector3.up * num;
		return val + GetUserMoveTransform() + GetBellyButtonOffsetVector(num);
	}

	internal Tuple<Vector3, bool> SculptInflatedVerticie(PregnancyPlusData infConfigClone, Vector3 originalVerticeLs, Vector3 inflatedVerticieLs, Vector3 sphereCenterLs, float waistWidth, Vector3 preMorphSphereCenter, float sphereRadius, Vector3 backExtentPos, Vector3 topExtentPos, ThreadsafeCurve bellySidesAC, ThreadsafeCurve bellyTopAC, ThreadsafeCurve bellyEdgeAC, ThreadsafeCurve bellyGapLerpAC)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0172: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_0179: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0190: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		//IL_019c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01de: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0215: Unknown result type (might be due to invalid IL or missing references)
		//IL_0217: Unknown result type (might be due to invalid IL or missing references)
		//IL_0225: Unknown result type (might be due to invalid IL or missing references)
		//IL_0227: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0203: Unknown result type (might be due to invalid IL or missing references)
		//IL_020a: Unknown result type (might be due to invalid IL or missing references)
		//IL_023f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0247: Unknown result type (might be due to invalid IL or missing references)
		//IL_024e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0272: Unknown result type (might be due to invalid IL or missing references)
		//IL_0278: Unknown result type (might be due to invalid IL or missing references)
		//IL_0259: Unknown result type (might be due to invalid IL or missing references)
		//IL_0260: Unknown result type (might be due to invalid IL or missing references)
		//IL_0267: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_0281: Unknown result type (might be due to invalid IL or missing references)
		//IL_0287: Unknown result type (might be due to invalid IL or missing references)
		//IL_0292: Unknown result type (might be due to invalid IL or missing references)
		//IL_0299: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a0: Unknown result type (might be due to invalid IL or missing references)
		if (PregnancyPlusPlugin.MakeBalloon.Value || PregnancyPlusPlugin.DebugVerts.Value)
		{
			return new Tuple<Vector3, bool>(inflatedVerticieLs, item2: true);
		}
		float num = Math.Abs(Vector3.Distance(preMorphSphereCenter, originalVerticeLs));
		float num2 = Math.Abs(Vector3.Distance(preMorphSphereCenter, inflatedVerticieLs));
		float num3 = Math.Abs(Vector3.Distance(sphereCenterLs, originalVerticeLs));
		float num4 = Math.Abs(Vector3.Distance(sphereCenterLs, inflatedVerticieLs));
		if (num3 >= num4 || num > num2)
		{
			return new Tuple<Vector3, bool>(originalVerticeLs, item2: false);
		}
		Vector3 val = SculptBaseShape(originalVerticeLs, inflatedVerticieLs, sphereCenterLs);
		if (GetInflationShiftY(infConfigClone) != 0f || GetInflationShiftZ(infConfigClone) != 0f)
		{
			val = GetUserShiftTransform(infConfigClone, val, sphereCenterLs, num3);
		}
		if (GetInflationStretchX(infConfigClone) != 0f)
		{
			val = GetUserStretchXTransform(infConfigClone, val, sphereCenterLs);
		}
		if (GetInflationStretchY(infConfigClone) != 0f)
		{
			val = GetUserStretchYTransform(infConfigClone, val, sphereCenterLs);
		}
		if (GetInflationRoundness(infConfigClone) != 0f)
		{
			val = GetUserRoundnessTransform(infConfigClone, originalVerticeLs, val, sphereCenterLs, num3, bellyEdgeAC);
		}
		if (GetInflationTaperY(infConfigClone) != 0f)
		{
			val = GetUserTaperYTransform(infConfigClone, val, sphereCenterLs, num3);
		}
		if (GetInflationTaperZ(infConfigClone) != 0f)
		{
			val = GetUserTaperZTransform(infConfigClone, originalVerticeLs, val, sphereCenterLs, num3, backExtentPos);
		}
		if (GetInflationFatFold(infConfigClone) > 0f)
		{
			val = GetUserFatFoldTransform(infConfigClone, originalVerticeLs, val, sphereCenterLs, sphereRadius, bellyGapLerpAC);
		}
		if (GetInflationDrop(infConfigClone) > 0f)
		{
			val = GetUserDropTransform(infConfigClone, Vector3.up, val, sphereCenterLs, num3, sphereRadius);
		}
		val = RoundToSides(originalVerticeLs, val, backExtentPos, bellySidesAC);
		if (originalVerticeLs.y > preMorphSphereCenter.y)
		{
			val = ReduceRibStretchingZ(originalVerticeLs, val, topExtentPos, bellyTopAC);
		}
		if (((Vector3)(ref val)).Equals(originalVerticeLs))
		{
			return new Tuple<Vector3, bool>(val, item2: false);
		}
		Vector3 val2 = Vector3.up * originalVerticeLs.y;
		float num5 = Math.Abs(Vector3.Distance(originalVerticeLs, val2));
		Vector3 val3 = Vector3.up * val.y;
		float num6 = Math.Abs(Vector3.Distance(val, val3));
		if (num6 < num5)
		{
			((Vector3)(ref val))..ctor(originalVerticeLs.x, val.y, originalVerticeLs.z);
		}
		float num7 = Math.Abs(Vector3.Distance(sphereCenterLs, val));
		float num8 = Math.Abs(Vector3.Distance(preMorphSphereCenter, val));
		if (num3 > num7 || num > num8)
		{
			return new Tuple<Vector3, bool>(originalVerticeLs, item2: false);
		}
		if (backExtentPos.z > val.z)
		{
			((Vector3)(ref val))..ctor(val.x, val.y, originalVerticeLs.z);
		}
		if (originalVerticeLs.z > val.z && originalVerticeLs.z > sphereCenterLs.z)
		{
			((Vector3)(ref val))..ctor(val.x, val.y, originalVerticeLs.z);
		}
		return new Tuple<Vector3, bool>(val, item2: true);
	}

	internal async Task ComputeDeltas(SkinnedMeshRenderer smr, string rendererName, MeshInflateFlags meshInflateFlags)
	{
		if ((Object)(object)smr == (Object)null)
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogWarning((object)" ComputeDeltas smr was null");
			}
		}
		else
		{
			if (!md.TryGetValue(rendererName, out var _md) || (_md.HasDeltas && !meshInflateFlags.OverWriteMesh))
			{
				return;
			}
			Quaternion averageRotation = BindPose.GetAverageRotation(smr, bellyBones);
			if (PregnancyPlusPlugin.DebugLog.Value && averageRotation != Quaternion.identity)
			{
				PregnancyPlusPlugin.Logger.LogWarning((object)$" {((Object)smr).name} has bindpose rotation {averageRotation}");
			}
			Matrix4x4 val = MeshSkinning.UndoMeshAlignment(smr, averageRotation);
			Matrix4x4 rotationUndo = Matrix.SetRotation(averageRotation);
			Mesh val2 = PrepForBlendShape(smr, rendererName, rotationUndo);
			if (!Object.op_Implicit((Object)(object)val2))
			{
				return;
			}
			if (PregnancyPlusPlugin.DebugCalcs.Value)
			{
				PregnancyPlusPlugin.Logger.LogInfo((object)(" Compute BlendShape Deltas for " + ((Object)smr).name));
			}
			Matrix4x4 scale = BindPose.GetScale(smr);
			Matrix4x4 inverse = ((Matrix4x4)(ref scale)).inverse;
			Matrix4x4 undoTfMatrix = val * inverse;
			if (!smr.sharedMesh.isReadable)
			{
				nativeDetour.Apply();
			}
			Vector3[] sourceNormals = smr.sharedMesh.normals;
			Vector3[] targetNormals = val2.normals;
			Vector4[] sourceTangents = smr.sharedMesh.tangents;
			Vector4[] targetTangents = val2.tangents;
			Vector3[] originalVerts = _md.originalVertices;
			Vector3[] inflatedVerts = _md.inflatedVertices;
			bool[] alteredVerts = _md.alteredVerticieIndexes;
			bool hasTransform = undoTfMatrix != Matrix4x4.identity;
			nativeDetour.Undo();
			await Task.Run(delegate
			{
				Vector3[] deltaVerts = (Vector3[])(object)new Vector3[originalVerts.Length];
				Vector3[] deltaNormals = (Vector3[])(object)new Vector3[originalVerts.Length];
				sourceNormals.RunParallel(delegate(Vector3 _, int i)
				{
					//IL_009a: Unknown result type (might be due to invalid IL or missing references)
					//IL_0025: Unknown result type (might be due to invalid IL or missing references)
					//IL_0036: Unknown result type (might be due to invalid IL or missing references)
					//IL_0041: Unknown result type (might be due to invalid IL or missing references)
					//IL_0051: Unknown result type (might be due to invalid IL or missing references)
					//IL_0056: Unknown result type (might be due to invalid IL or missing references)
					//IL_006e: Unknown result type (might be due to invalid IL or missing references)
					//IL_007f: Unknown result type (might be due to invalid IL or missing references)
					//IL_008a: Unknown result type (might be due to invalid IL or missing references)
					//IL_0090: Unknown result type (might be due to invalid IL or missing references)
					//IL_0095: Unknown result type (might be due to invalid IL or missing references)
					if (alteredVerts[i])
					{
						deltaVerts[i] = BlendShapeTools.GetV3Delta(originalVerts[i], inflatedVerts[i], undoTfMatrix, hasTransform);
						deltaNormals[i] = BlendShapeTools.GetV3Delta(sourceNormals[i], targetNormals[i], undoTfMatrix, hasTransform: false);
					}
					return Vector3.zero;
				});
				_md.deltaVerticies = deltaVerts;
				_md.deltaNormals = deltaNormals;
				_md.deltaTangents = sourceTangents.RunParallel((Vector4 _, int i) => alteredVerts[i] ? BlendShapeTools.GetV3Delta(Vector4.op_Implicit(sourceTangents[i]), Vector4.op_Implicit(targetTangents[i]), undoTfMatrix, hasTransform) : Vector4.op_Implicit(sourceTangents[i]));
			});
		}
	}

	internal bool MeasureWaistAndSphere(ChaControl chaControl, bool forceRecalc = false)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		Vector3 bodyTopScale = PregnancyPlusHelper.GetBodyTopScale(base.ChaControl);
		Vector3 n_HeightScale = PregnancyPlusHelper.GetN_HeightScale(base.ChaControl);
		Vector3 localScale = ((Component)base.ChaControl).transform.localScale;
		bool flag = bellyInfo == null || bellyInfo.NeedsBoneDistanceRecalc(bodyTopScale, n_HeightScale, localScale);
		bool flag2 = bellyInfo == null || bellyInfo.NeedsSphereRecalc(infConfig, GetInflationMultiplier());
		if (forceRecalc)
		{
			bellyInfo = null;
		}
		if (bellyInfo != null)
		{
			if (flag2 && !flag)
			{
				bool result = MeasureSphere(chaControl, bodyTopScale, n_HeightScale);
				if (PregnancyPlusPlugin.DebugCalcs.Value)
				{
					PregnancyPlusPlugin.Logger.LogInfo((object)bellyInfo.Log());
				}
				return result;
			}
			if (flag && !flag2)
			{
				float waistToRibDist;
				float waistWidth;
				bool result2 = MeasureWaist(chaControl, bodyTopScale, out waistToRibDist, out waistWidth);
				MeasureSphere(chaControl, bodyTopScale, n_HeightScale);
				bellyInfo = new BellyInfo(waistWidth, waistToRibDist, bellyInfo.SphereRadius, bellyInfo.OriginalSphereRadius, bodyTopScale, GetInflationMultiplier(), n_HeightScale, localScale);
				if (PregnancyPlusPlugin.DebugCalcs.Value)
				{
					PregnancyPlusPlugin.Logger.LogInfo((object)bellyInfo.Log());
				}
				return result2;
			}
			if (!flag2 && !flag)
			{
				if (PregnancyPlusPlugin.DebugCalcs.Value)
				{
					PregnancyPlusPlugin.Logger.LogInfo((object)bellyInfo.Log());
				}
				if (bellyInfo.WaistWidth > 0f)
				{
					return bellyInfo.SphereRadius > 0f;
				}
				return false;
			}
		}
		if (PregnancyPlusPlugin.DebugLog.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)" MeasureWaistAndSphere init ");
		}
		if (!MeasureWaist(chaControl, bodyTopScale, out var waistToRibDist2, out var waistWidth2))
		{
			return false;
		}
		float sphereRadius = GetSphereRadius(waistToRibDist2, waistWidth2);
		float num = sphereRadius * (GetInflationMultiplier() + 1f);
		bellyInfo = new BellyInfo(waistWidth2, waistToRibDist2, num, sphereRadius, bodyTopScale, GetInflationMultiplier(), n_HeightScale, localScale);
		if (PregnancyPlusPlugin.DebugCalcs.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)bellyInfo.Log());
		}
		if (waistWidth2 > 0f)
		{
			return num > 0f;
		}
		return false;
	}

	internal bool MeasureWaist(ChaControl chaControl, Vector3 bodyTopScale, out float waistToRibDist, out float waistWidth)
	{
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		string text = "cf_d_bust00";
		string boneStart = "cf_s_spine02";
		string boneStart2 = "cf_s_waist02";
		string boneName = "cf_j_thigh00_L";
		string boneName2 = "cf_j_thigh00_R";
		string text2 = "a_n_back";
		string text3 = "cf_j_waist01";
		string boneEnd = "cf_j_hips";
		waistToRibDist = 0f;
		waistWidth = 0f;
		waistToRibDist = BoneChainYDistance(boneStart2, boneEnd) + BoneChainYDistance(boneStart, boneEnd, default(Vector3), includeNegative: true);
		Transform bone = PregnancyPlusHelper.GetBone(base.ChaControl, boneName);
		Transform bone2 = PregnancyPlusHelper.GetBone(base.ChaControl, boneName2);
		if ((Object)(object)bone == (Object)null || (Object)(object)bone2 == (Object)null)
		{
			if (waistWidth > 0f)
			{
				return waistToRibDist > 0f;
			}
			return false;
		}
		waistWidth = Vector3.Distance(((Component)bone).transform.InverseTransformPoint(bone.position), ((Component)bone).transform.InverseTransformPoint(bone2.position));
		if (PregnancyPlusPlugin.DebugLog.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)" MeasureWaist Recalc ");
		}
		if (waistWidth > 0f)
		{
			return waistToRibDist > 0f;
		}
		return false;
	}

	internal bool MeasureSphere(ChaControl chaControl, Vector3 charScale, Vector3 nHeightScale)
	{
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		float sphereRadius = GetSphereRadius(bellyInfo.WaistHeight, bellyInfo.WaistWidth);
		float sphereRadius2 = sphereRadius * (GetInflationMultiplier() + 1f);
		bellyInfo = new BellyInfo(bellyInfo.WaistWidth, bellyInfo.WaistHeight, sphereRadius2, sphereRadius, charScale, GetInflationMultiplier(), nHeightScale, ((Component)chaControl).transform.localScale);
		if (PregnancyPlusPlugin.DebugLog.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)" MeasureSphere Recalc ");
		}
		if (bellyInfo.WaistWidth > 0f)
		{
			return sphereRadius > 0f;
		}
		return false;
	}

	internal float BoneChainYDistance(string boneStart, string boneEnd = null, Vector3 totalCharScale = default(Vector3), bool includeNegative = false)
	{
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = PregnancyPlusHelper.GetBoneGO(base.ChaControl, boneStart);
		GameObject val2 = val;
		if ((Object)(object)val == (Object)null)
		{
			return 0f;
		}
		float num = 0f;
		while ((Object)(object)val != (Object)null && Object.op_Implicit((Object)(object)val.transform.parent) && (boneEnd == null || !(((Object)val).name.ToLower() == boneEnd.ToLower())))
		{
			float num2 = (((Object)(object)val2 != (Object)null) ? (val.transform.InverseTransformPoint(val.transform.position).y - val.transform.InverseTransformPoint(val2.transform.position).y) : 0f);
			if (includeNegative || num2 > 0f)
			{
				num += Math.Abs(num2);
				val2 = val;
			}
			val = ((Component)val.transform.parent).gameObject;
		}
		if (totalCharScale.y != 0f && totalCharScale.y != 1f)
		{
			if (PregnancyPlusPlugin.DebugCalcs.Value)
			{
				PregnancyPlusPlugin.Logger.LogInfo((object)$" applying BodyTop scale to distance: {num} scale: {totalCharScale.y}");
			}
			num *= totalCharScale.y;
		}
		return num;
	}

	public void OnOpenClothOffsetSelected()
	{
		List<SkinnedMeshRenderer> meshRenderers = PregnancyPlusHelper.GetMeshRenderers(((ChaInfo)base.ChaControl).objClothes);
		clothOffsetGui.OpenClothOffsetGui(meshRenderers, this);
	}

	public Mesh CreateMeshCollider(SkinnedMeshRenderer bodySmr = null)
	{
		MeshCollider meshCollider = GetMeshCollider(bodySmr);
		if ((Object)(object)meshCollider != (Object)null)
		{
			return null;
		}
		string meshKey = GetMeshKey(bodySmr);
		if (!md.TryGetValue(meshKey, out var value))
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogWarning((object)(" CreateMeshCollider cant find MeshData for " + meshKey));
			}
			return null;
		}
		if (!value.HasOriginalVerts)
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogWarning((object)" CreateMeshCollider bodySmr original verts have not computed yet");
			}
			return null;
		}
		MeshCollider val = ((Component)((Component)bodySmr).transform).gameObject.AddComponent<MeshCollider>();
		Vector3[] originalVertices = value.originalVertices;
		bodySmr.sharedMesh = bodySmr.sharedMesh;
		Mesh val2 = Object.Instantiate<Mesh>(bodySmr.sharedMesh);
		val2.vertices = OffSetMeshCollider(bodySmr, originalVertices);
		val.sharedMesh = val2;
		return val2;
	}

	public MeshCollider GetMeshCollider(SkinnedMeshRenderer bodySmr = null)
	{
		if ((Object)(object)bodySmr == (Object)null)
		{
			bodySmr = GetBodyMeshRenderer();
		}
		if ((Object)(object)bodySmr == (Object)null)
		{
			return null;
		}
		MeshCollider component = ((Component)bodySmr).gameObject.GetComponent<MeshCollider>();
		if ((Object)(object)component == (Object)null)
		{
			return null;
		}
		return component;
	}

	public Vector3[] OffSetMeshCollider(SkinnedMeshRenderer bodySmr, Vector3[] originalVerts)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		Vector3[] array = (Vector3[])(object)new Vector3[originalVerts.Length];
		for (int i = 0; i < originalVerts.Length; i++)
		{
			array[i] = ((Component)bodySmr).transform.InverseTransformPoint(originalVerts[i]);
		}
		return array;
	}

	public void RemoveMeshCollider()
	{
		MeshCollider meshCollider = GetMeshCollider();
		if (!((Object)(object)meshCollider == (Object)null))
		{
			Object.Destroy((Object)(object)meshCollider);
		}
	}

	internal float[] DoClothMeasurement(SkinnedMeshRenderer clothSmr, SkinnedMeshRenderer bodySmr, Vector3 sphereCenter, bool needsRecomputeOffsets = false)
	{
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
		if (!Object.op_Implicit((Object)(object)bodySmr))
		{
			return null;
		}
		if (((Object)clothSmr).name.Contains(BodyMeshName))
		{
			return null;
		}
		string meshKey = GetMeshKey(clothSmr);
		if (!md.TryGetValue(meshKey, out var _))
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogWarning((object)(" DoClothMeasurement cant find MeshData for " + meshKey));
			}
			return new float[clothSmr.sharedMesh.vertexCount];
		}
		bool hasClothingOffsets = md[meshKey].HasClothingOffsets;
		float[] array = new float[0];
		if (!hasClothingOffsets)
		{
			md[meshKey].clothingOffsets = new float[clothSmr.sharedMesh.vertexCount];
			array = md[meshKey].clothingOffsets;
		}
		else if (!needsRecomputeOffsets && hasClothingOffsets)
		{
			return md[meshKey].clothingOffsets;
		}
		Vector3[] originalVertices = md[meshKey].originalVertices;
		bool[] bellyVerticieIndexes = md[meshKey].bellyVerticieIndexes;
		float num = bellyInfo.OriginalSphereRadius / 2f;
		float num2 = bellyInfo.WaistWidth / 200f;
		MeshCollider meshCollider = GetMeshCollider();
		if ((Object)(object)meshCollider == (Object)null)
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogWarning((object)" MeshCollider is null when it shouldn't be");
			}
			return md[meshKey].clothingOffsets;
		}
		GetRayCastTargetPositions(sphereCenter);
		if (PregnancyPlusPlugin.DebugCalcs.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)(" Pre-calculating clothing offset " + ((Object)clothSmr).name));
		}
		int num3 = rayCastTargetPositions.Length + 1;
		RaycastHit[] array2 = ProcessRayCastCommands(clothSmr, originalVertices, bellyVerticieIndexes, sphereCenter, num);
		Type typeFromHandle = typeof(MeshCollider);
		for (int i = 0; i < originalVertices.Length; i++)
		{
			if (!bellyVerticieIndexes[i])
			{
				continue;
			}
			Vector3 val = originalVertices[i];
			float num4 = num;
			for (int j = 0; j < num3; j++)
			{
				int num5 = i * num3 + j;
				if (!((Object)(object)((RaycastHit)(ref array2[num5])).collider == (Object)null) && !(((object)((RaycastHit)(ref array2[num5])).collider).GetType() != typeFromHandle) && ((RaycastHit)(ref array2[num5])).distance < num4)
				{
					num4 = ((RaycastHit)(ref array2[num5])).distance;
				}
			}
			if (num4 >= num)
			{
				array[i] = num2;
			}
			else
			{
				array[i] = num4 + num2;
			}
		}
		return array;
	}

	internal RaycastHit[] ProcessRayCastCommands(SkinnedMeshRenderer clothSmr, Vector3[] origVerts, bool[] bellyVerticieIndexes, Vector3 sphereCenter, float maxDistance)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		int num = rayCastTargetPositions.Length + 1;
		Vector3[] array = (Vector3[])(object)new Vector3[origVerts.Length * num];
		Vector3[] array2 = (Vector3[])(object)new Vector3[origVerts.Length * num];
		for (int i = 0; i < origVerts.Length; i++)
		{
			if (bellyVerticieIndexes[i])
			{
				Vector3 val = origVerts[i];
				Vector3 zero = Vector3.zero;
				for (int j = 0; j < num; j++)
				{
					int num2 = i * num + j;
					zero = ((j != num - 1) ? (rayCastTargetPositions[j] - val) : (sphereCenter - val));
					array[num2] = val;
					array2[num2] = zero;
				}
			}
		}
		return ExecuteRayCastCommands(array, array2, maxDistance);
	}

	internal RaycastHit[] ExecuteRayCastCommands(Vector3[] origins, Vector3[] directions, float maxDistance)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		NativeArray<RaycastHit> val = default(NativeArray<RaycastHit>);
		val..ctor(origins.Length, (Allocator)2, (NativeArrayOptions)1);
		NativeArray<RaycastCommand> val2 = default(NativeArray<RaycastCommand>);
		val2..ctor(directions.Length, (Allocator)2, (NativeArrayOptions)1);
		for (int i = 0; i < origins.Length; i++)
		{
			val2[i] = new RaycastCommand(origins[i], directions[i], maxDistance, -5, 1);
		}
		JobHandle val3 = RaycastCommand.ScheduleBatch(val2, val, 1, default(JobHandle));
		((JobHandle)(ref val3)).Complete();
		RaycastHit[] result = val.ToArray();
		val.Dispose();
		val2.Dispose();
		return result;
	}

	public RaycastHit RayCastToMeshCollider(Vector3 origin, float maxDistance, Vector3 direction, MeshCollider meshCollider)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		Ray val = default(Ray);
		((Ray)(ref val))..ctor(origin, direction);
		RaycastHit result = default(RaycastHit);
		((Collider)meshCollider).Raycast(val, ref result, maxDistance);
		return result;
	}

	internal bool NeedsClothMeasurement(SkinnedMeshRenderer clothSmr, SkinnedMeshRenderer bodySmr, Vector3 sphereCenter, bool isClothingMesh)
	{
		if (!isClothingMesh)
		{
			return false;
		}
		if (!Object.op_Implicit((Object)(object)bodySmr))
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogWarning((object)(" NeedsClothMeasurement No bodySmr, skipping for " + ((Object)clothSmr).name));
			}
			return false;
		}
		if (((Object)clothSmr).name.Contains(BodyMeshName))
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogWarning((object)(" NeedsClothMeasurement smr " + ((Object)clothSmr).name + " contains " + BodyMeshName + " skpping"));
			}
			return false;
		}
		string meshKey = GetMeshKey(clothSmr);
		return !md[meshKey].HasClothingOffsets;
	}

	public void GetRayCastTargetPositions(Vector3 sphereCenter)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		SkinnedMeshRenderer bodyMeshRenderer = GetBodyMeshRenderer();
		if ((Object)(object)bodyMeshRenderer == (Object)null)
		{
			return;
		}
		int i;
		for (i = 0; i < rayCastTargetNames.Length; i++)
		{
			rayCastTargetPositions[i] = sphereCenter;
			int num = Array.FindIndex(bodyMeshRenderer.bones, (Transform b) => ((Object)b).name == rayCastTargetNames[i]);
			if (num < 0)
			{
				PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(charaFileName, ErrorCode.PregPlus_MeshNotSkinnedToBone, " This this bone `" + rayCastTargetNames[i] + "` is not skinned to the body mesh, so a measurement was skipped.");
				break;
			}
			Matrix4x4 bindPoseOffset = (Matrix4x4)(((??)MeshSkinning.GetBindPoseOffset(base.ChaControl, bindPoseList, bodyMeshRenderer, bodyMeshRenderer.sharedMesh.bindposes[num], bodyMeshRenderer.bones[num])) ?? Matrix4x4.identity);
			MeshSkinning.GetBindPoseBoneTransform(bodyMeshRenderer, bodyMeshRenderer.sharedMesh.bindposes[num], bindPoseOffset, out var position, out var _, bindPoseList, bodyMeshRenderer.bones[num]);
			rayCastTargetPositions[i] = position;
		}
	}

	public float GetEdgeLerp(Vector3 origVertWs, Vector3 center, float offset)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		float num = Vector3.Distance(origVertWs, center) - bellyInfo.SphereRadius;
		return Mathf.Lerp(offset, 0f, (num - bellyInfo.WaistWidth / 15f) / (bellyInfo.WaistWidth / 10f));
	}

	internal float GetClothesFixOffset(PregnancyPlusData infConfigClone, Vector3 sphereCenterWs, float sphereRadius, float waistWidth, Vector3 origVertWS, string meshName, float rayCastOffset, float individualOffset)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		return GetClothesFixOffsetV2(infConfigClone, sphereCenterWs, sphereRadius, waistWidth, origVertWS, meshName, rayCastOffset, individualOffset);
	}

	internal float GetClothesFixOffsetV2(PregnancyPlusData infConfigClone, Vector3 sphereCenterWs, float sphereRadius, float waistWidth, Vector3 origVertWS, string meshName, float rayCastOffset, float individualOffset)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		float num = GetInflationClothOffset(infConfigClone) + individualOffset;
		float num2 = rayCastOffset + bellyInfo.WaistWidth / 100f * num;
		float num3 = Vector3.Distance(sphereCenterWs, origVertWS) / sphereRadius;
		float num4 = Mathf.Lerp(num2, num2 / 3f, num3);
		return num4 + GetClothLayerOffsetV2(infConfigClone, meshName, num);
	}

	internal float GetClothLayerOffsetV2(PregnancyPlusData infConfigClone, string meshName, float offset)
	{
		if (innerLayers.Contains(meshName))
		{
			return 0f;
		}
		return bellyInfo.WaistWidth / 120f + bellyInfo.WaistWidth / 100f * offset;
	}

	internal async Task<bool> GetFilteredVerticieIndexes(SkinnedMeshRenderer smr, string[] boneFilters, string[] boneExclusions)
	{
		Mesh sharedMesh = smr.sharedMesh;
		string renderKey = GetMeshKey(smr);
		Transform[] bones = smr.bones;
		bool hasBellyVerticies = false;
		if (!sharedMesh.isReadable)
		{
			nativeDetour.Apply();
		}
		if (sharedMesh.boneWeights.Length == 0)
		{
			if (!ignoreMeshList.Contains(renderKey))
			{
				ignoreMeshList.Add(renderKey);
			}
			if (PregnancyPlusPlugin.DebugCalcs.Value)
			{
				PregnancyPlusPlugin.Logger.LogInfo((object)(" No Bone Weights found for " + ((Object)smr).name + " skipping"));
			}
			nativeDetour.Undo();
			return false;
		}
		if (!GetFilteredBoneIndexes(bones, boneFilters, boneExclusions, out var bellyBoneIndexes, out var bellyBoneExclusionIndexes))
		{
			if (!ignoreMeshList.Contains(renderKey))
			{
				ignoreMeshList.Add(renderKey);
			}
			if (PregnancyPlusPlugin.DebugCalcs.Value)
			{
				PregnancyPlusPlugin.Logger.LogInfo((object)(" No FilteredBoneIndexes found for " + ((Object)smr).name + " skipping"));
			}
			nativeDetour.Undo();
			return false;
		}
		if (PregnancyPlusPlugin.DebugCalcs.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)(" GetFilteredVerticieIndexes for " + ((Object)smr).name));
		}
		md[renderKey] = new MeshData(sharedMesh.vertexCount);
		Vector3[] verticies = sharedMesh.vertices;
		bool debugAnyVerts = PregnancyPlusPlugin.MakeBalloon.Value || PregnancyPlusPlugin.DebugVerts.Value;
		await Task.Run(delegate
		{
			if (!md.TryGetValue(renderKey, out var _))
			{
				md[renderKey] = new MeshData(verticies.Length);
			}
			md[renderKey].bellyVerticieIndexes = sharedMesh.boneWeights.RunParallel(delegate(BoneWeight bw, int i)
			{
				if (debugAnyVerts)
				{
					hasBellyVerticies = true;
					return true;
				}
				int[] array = new int[4]
				{
					((BoneWeight)(ref bw)).boneIndex0,
					((BoneWeight)(ref bw)).boneIndex1,
					((BoneWeight)(ref bw)).boneIndex2,
					((BoneWeight)(ref bw)).boneIndex3
				};
				float[] array2 = new float[4]
				{
					((BoneWeight)(ref bw)).weight0,
					((BoneWeight)(ref bw)).weight1,
					((BoneWeight)(ref bw)).weight2,
					((BoneWeight)(ref bw)).weight3
				};
				for (int j = 0; j < 4; j++)
				{
					if (bellyBoneExclusionIndexes != null && bellyBoneExclusionIndexes.Contains(array[j]))
					{
						return false;
					}
					if (array2[j] > 0f && bellyBoneIndexes.Contains(array[j]))
					{
						hasBellyVerticies = true;
						return true;
					}
				}
				return false;
			});
		});
		nativeDetour.Undo();
		if (!hasBellyVerticies)
		{
			if (!ignoreMeshList.Contains(renderKey))
			{
				ignoreMeshList.Add(renderKey);
			}
			if (PregnancyPlusPlugin.DebugCalcs.Value)
			{
				PregnancyPlusPlugin.Logger.LogInfo((object)(" hasBellyVerticies none found for " + ((Object)smr).name + " skipping"));
			}
			RemoveRenderKey(renderKey);
		}
		else if (ignoreMeshList.Contains(renderKey))
		{
			ignoreMeshList.Remove(renderKey);
		}
		return hasBellyVerticies;
	}

	internal bool GetFilteredBoneIndexes(Transform[] bones, string[] boneFilters, string[] boneExclusionFilters, out List<int> bellyBoneIndexes, out List<int> bellyBoneExclusionIndexes)
	{
		bellyBoneIndexes = new List<int>();
		bellyBoneExclusionIndexes = new List<int>();
		if (bones.Length == 0)
		{
			bellyBoneExclusionIndexes = null;
			return false;
		}
		bool flag = boneFilters != null && boneFilters.Length != 0;
		bool flag2 = boneExclusionFilters != null && boneExclusionFilters.Length != 0;
		int num = bones.Length;
		for (int i = 0; i < num; i++)
		{
			if (!Object.op_Implicit((Object)(object)bones[i]))
			{
				continue;
			}
			if (!flag)
			{
				bellyBoneIndexes.Add(i);
			}
			string name = ((Object)bones[i]).name;
			foreach (string text in boneFilters)
			{
				if (text == name)
				{
					bellyBoneIndexes.Add(i);
				}
			}
			if (!flag2)
			{
				continue;
			}
			foreach (string text2 in boneExclusionFilters)
			{
				if (text2 == name)
				{
					bellyBoneExclusionIndexes.Add(i);
				}
			}
		}
		bellyBoneExclusionIndexes = bellyBoneExclusionIndexes.Distinct().ToList();
		bellyBoneIndexes = bellyBoneIndexes.Distinct().ToList();
		if (bellyBoneExclusionIndexes.Count <= 0)
		{
			bellyBoneExclusionIndexes = null;
		}
		return bellyBoneIndexes.Count > 0;
	}

	internal bool OnCreateBlendShapeSelected(bool temporary = false)
	{
		if (PregnancyPlusPlugin.DebugLog.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)" ");
		}
		if (PregnancyPlusPlugin.DebugLog.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)" OnCreateBlendShapeSelected ");
		}
		List<MeshBlendShape> meshBlendShapes = new List<MeshBlendShape>();
		meshWithBlendShapes = new List<MeshIdentifier>();
		string uncensorGuid = GetUncensorGuid();
		List<SkinnedMeshRenderer> meshRenderers = PregnancyPlusHelper.GetMeshRenderers(((ChaInfo)base.ChaControl).objClothes);
		meshBlendShapes = LoopAndCreateBlendShape(meshRenderers, meshBlendShapes, uncensorGuid);
		if (!PregnancyPlusPlugin.IgnoreAccessories.Value)
		{
			List<SkinnedMeshRenderer> meshRenderers2 = PregnancyPlusHelper.GetMeshRenderers(((ChaInfo)base.ChaControl).objAccessory);
			meshBlendShapes = LoopAndCreateBlendShape(meshRenderers2, meshBlendShapes, uncensorGuid);
		}
		List<SkinnedMeshRenderer> meshRenderers3 = PregnancyPlusHelper.GetMeshRenderers(((ChaInfo)base.ChaControl).objBody);
		meshBlendShapes = LoopAndCreateBlendShape(meshRenderers3, meshBlendShapes, uncensorGuid);
		if (!temporary)
		{
			AddBlendShapesToData(infConfig, meshBlendShapes);
		}
		UnityUiTools.ResetSlider(PregnancyPlusGui.cat, "Pregnancy +");
		blendShapeGui.OnSkinnedMeshRendererBlendShapesCreated(meshWithBlendShapes);
		return meshBlendShapes.Count > 0;
	}

	internal void CaptureNewBlendshapeWeights()
	{
		List<MeshBlendShape> list = LoadBlendShapesFromCardData(infConfig.meshBlendShape);
		if (list.Count <= 0)
		{
			return;
		}
		string uncensorGuid = GetUncensorGuid();
		foreach (MeshBlendShape item in list)
		{
			if (item == null || item.BlendShape == null)
			{
				if (PregnancyPlusPlugin.DebugLog.Value)
				{
					PregnancyPlusPlugin.Logger.LogInfo((object)" Found a null saved BlendShape.  Is it corrupt?");
				}
				continue;
			}
			SkinnedMeshRenderer meshRendererByName = PregnancyPlusHelper.GetMeshRendererByName(base.ChaControl, item.MeshName, item.VertCount);
			if ((Object)(object)meshRendererByName == (Object)null)
			{
				continue;
			}
			string blendShapeName = GetBlendShapeName(GetMeshKey(meshRendererByName));
			BlendShapeController blendShapeController = new BlendShapeController(meshRendererByName, blendShapeName);
			if (blendShapeController.blendShape != null)
			{
				if (PregnancyPlusPlugin.DebugLog.Value && item.BlendShape.weight != blendShapeController.blendShape.weight)
				{
					PregnancyPlusPlugin.Logger.LogInfo((object)$" CaptureNewBlendshapeWeights > {item.MeshName} weight:{blendShapeController.blendShape.weight}");
				}
				item.BlendShape.weight = blendShapeController.blendShape.weight;
				Legacy_CheckNullUncensorGuid(list, item, meshRendererByName, uncensorGuid);
			}
		}
		AddBlendShapesToData(infConfig, list);
	}

	internal void AddUncensorGUID(List<MeshBlendShape> meshBlendShapes, string uncensorGUID)
	{
		foreach (MeshBlendShape meshBlendShape in meshBlendShapes)
		{
			meshBlendShape.UncensorGUID = uncensorGUID;
		}
	}

	internal string GetUncensorGuid()
	{
		return PregnancyPlusPlugin.Hooks_Uncensor.GetUncensorBodyGuid(base.ChaControl, "com.deathweasel.bepinex.uncensorselector");
	}

	internal void OnOpenBlendShapeSelected()
	{
		blendShapeGui.OpenBlendShapeGui(meshWithBlendShapes, this);
	}

	internal void OnRemoveAllGUIBlendShapes()
	{
		foreach (MeshIdentifier meshWithBlendShape in meshWithBlendShapes)
		{
			SkinnedMeshRenderer meshRendererByName = PregnancyPlusHelper.GetMeshRendererByName(base.ChaControl, meshWithBlendShape.name, meshWithBlendShape.vertexCount);
			if ((Object)(object)meshRendererByName == (Object)null)
			{
				continue;
			}
			for (int i = 0; i < meshRendererByName.sharedMesh.blendShapeCount; i++)
			{
				string blendShapeName = meshRendererByName.sharedMesh.GetBlendShapeName(i);
				if (blendShapeName.EndsWith("KK_PregnancyPlus"))
				{
					BlendShapeController blendShapeController = new BlendShapeController(meshRendererByName, blendShapeName);
					blendShapeController.RemoveBlendShape(meshRendererByName);
				}
			}
		}
		meshWithBlendShapes = new List<MeshIdentifier>();
		ClearBlendShapesFromCharData();
	}

	internal void ScrubTempBlendShapes()
	{
		List<SkinnedMeshRenderer> meshRenderers = PregnancyPlusHelper.GetMeshRenderers(((ChaInfo)base.ChaControl).objBody, findAll: true);
		List<SkinnedMeshRenderer> meshRenderers2 = PregnancyPlusHelper.GetMeshRenderers(((ChaInfo)base.ChaControl).objClothes);
		List<SkinnedMeshRenderer> meshRenderers3 = PregnancyPlusHelper.GetMeshRenderers(((ChaInfo)base.ChaControl).objAccessory);
		if (meshRenderers2 != null && meshRenderers2.Count > 0)
		{
			meshRenderers.AddRange(meshRenderers2);
		}
		if (meshRenderers3 != null && meshRenderers3.Count > 0 && !PregnancyPlusPlugin.IgnoreAccessories.Value)
		{
			meshRenderers.AddRange(meshRenderers3);
		}
		foreach (SkinnedMeshRenderer item in meshRenderers)
		{
			if (!Object.op_Implicit((Object)(object)item.sharedMesh))
			{
				continue;
			}
			for (int i = 0; i < item.sharedMesh.blendShapeCount; i++)
			{
				string blendShapeName = item.sharedMesh.GetBlendShapeName(i);
				string blendShapeName2 = GetBlendShapeName(GetMeshKey(item), blendShapeTempTagName);
				if (blendShapeName.EndsWith(blendShapeName2))
				{
					BlendShapeController blendShapeController = new BlendShapeController(item, blendShapeName);
					blendShapeController.RemoveBlendShape(item);
				}
			}
		}
	}

	internal List<MeshBlendShape> LoopAndCreateBlendShape(List<SkinnedMeshRenderer> smrs, List<MeshBlendShape> meshBlendShapes, string uncensorGUID)
	{
		foreach (SkinnedMeshRenderer smr in smrs)
		{
			string meshKey = GetMeshKey(smr);
			if (md.ContainsKey(meshKey) && md[meshKey].HasInflatedVerts)
			{
				BlendShapeController blendShapeController = CreateOrMergeBlendShape(smr, meshKey);
				MeshBlendShape meshBlendShape = ConvertToMeshBlendShape(((Object)smr).name, blendShapeController.blendShape, uncensorGUID);
				if (meshBlendShape != null)
				{
					meshBlendShapes.Add(meshBlendShape);
					meshWithBlendShapes.Add(new MeshIdentifier(((Object)smr).name, smr.sharedMesh.vertexCount));
				}
			}
		}
		return meshBlendShapes;
	}

	internal MeshBlendShape ConvertToMeshBlendShape(string smrMeshName, BlendShape blendShape, string uncensorGUID)
	{
		if (blendShape == null)
		{
			return null;
		}
		return new MeshBlendShape(smrMeshName, blendShape, blendShape.vertexCount, uncensorGUID);
	}

	internal void AddBlendShapesToData(PregnancyPlusData _infConfig, List<MeshBlendShape> meshBlendShapes)
	{
		_infConfig.meshBlendShape = LZ4MessagePackSerializer.Serialize<List<MeshBlendShape>>(meshBlendShapes);
	}

	internal void ClearBlendShapesFromCharData()
	{
		infConfig.meshBlendShape = null;
	}

	internal void LoadBlendShapes(PregnancyPlusData data, bool checkUncensor = false)
	{
		if (data.meshBlendShape == null)
		{
			return;
		}
		if (PregnancyPlusPlugin.DebugLog.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)$" MeshBlendShape size > {data.meshBlendShape.Length / 1024}KB ");
		}
		meshWithBlendShapes = new List<MeshIdentifier>();
		List<MeshBlendShape> list = LoadBlendShapesFromCardData(data.meshBlendShape);
		if (list.Count <= 0)
		{
			return;
		}
		if (PregnancyPlusPlugin.DebugLog.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)$" MeshBlendShape count > {list.Count} ");
		}
		string uncensorGuid = GetUncensorGuid();
		NeedsUncensorChanged(list, uncensorGuid, checkUncensor);
		foreach (MeshBlendShape item in list)
		{
			List<SkinnedMeshRenderer> meshRenderers = PregnancyPlusHelper.GetMeshRenderers(((ChaInfo)base.ChaControl).objClothes, findAll: true);
			LoopMeshAndAddSavedBlendShape(meshRenderers, item, uncensorGuid, isClothingMesh: true);
			if (!PregnancyPlusPlugin.IgnoreAccessories.Value)
			{
				List<SkinnedMeshRenderer> meshRenderers2 = PregnancyPlusHelper.GetMeshRenderers(((ChaInfo)base.ChaControl).objAccessory, findAll: true);
				LoopMeshAndAddSavedBlendShape(meshRenderers2, item, uncensorGuid, isClothingMesh: true);
			}
			List<SkinnedMeshRenderer> meshRenderers3 = PregnancyPlusHelper.GetMeshRenderers(((ChaInfo)base.ChaControl).objBody, findAll: true);
			LoopMeshAndAddSavedBlendShape(meshRenderers3, item, uncensorGuid);
		}
		infConfigHistory = (PregnancyPlusData)infConfig.Clone();
	}

	internal List<MeshBlendShape> LoadBlendShapesFromCardData(byte[] meshBlendShapesByte)
	{
		if (meshBlendShapesByte == null)
		{
			return new List<MeshBlendShape>();
		}
		List<MeshBlendShape> list = LZ4MessagePackSerializer.Deserialize<List<MeshBlendShape>>(meshBlendShapesByte);
		if (list == null)
		{
			return new List<MeshBlendShape>();
		}
		return list;
	}

	internal bool NeedsUncensorChanged(List<MeshBlendShape> meshBlendShapes, string uncensorGUID, bool updateUncensor = false)
	{
		string text = null;
		foreach (MeshBlendShape meshBlendShape in meshBlendShapes)
		{
			if (meshBlendShape.UncensorGUID != null)
			{
				text = meshBlendShape.UncensorGUID;
			}
		}
		if (text == null || uncensorGUID == text)
		{
			Legacy_CheckInitialUncensorGuid(meshBlendShapes, uncensorGUID);
			return false;
		}
		if (!updateUncensor)
		{
			PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(charaFileName, ErrorCode.PregPlus_BodyUncensorChanged, " !BlendShape uncensorGUID '" + text + "' does not match the current uncensor '" + uncensorGUID + "' for the body BlendShape.  Skipping");
			return false;
		}
		return TriggerUncensorChange(text, uncensorGUID);
	}

	internal bool TriggerUncensorChange(string validUncensorGUID, string uncensorGUID)
	{
		if (PregnancyPlusPlugin.DebugLog.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)(" Stored uncensorGUID '" + validUncensorGUID + "' does not match the current uncensor '" + uncensorGUID + "'. Attempting uncensor swap"));
		}
		ignoreNextUncensorHook = true;
		PregnancyPlusPlugin.Hooks_Uncensor.ChangeUncensorTo(base.ChaControl, "com.deathweasel.bepinex.uncensorselector", validUncensorGUID);
		return true;
	}

	internal void LoopMeshAndAddSavedBlendShape(List<SkinnedMeshRenderer> smrs, MeshBlendShape meshBlendShape, string uncensorGUID, bool isClothingMesh = false)
	{
		string meshName = meshBlendShape.MeshName;
		int vertCount = meshBlendShape.VertCount;
		foreach (SkinnedMeshRenderer smr in smrs)
		{
			smr.sharedMesh = smr.sharedMesh;
			if (((Object)smr).name == meshName && smr.sharedMesh.vertexCount == vertCount)
			{
				meshWithBlendShapes.Add(new MeshIdentifier(((Object)smr).name, smr.sharedMesh.vertexCount));
				AddSavedBlendShape(smr, meshBlendShape);
			}
			else if (((Object)smr).name == meshName && smr.sharedMesh.vertexCount != vertCount)
			{
				PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(charaFileName, ErrorCode.PregPlus_BodyMeshVertexChanged, $" Mesh '{((Object)smr).name}' has a different vertex count, and no longer fits the BlendShape saved to this card {smr.sharedMesh.vertexCount} => {vertCount}.  Blendshape {meshBlendShape.BlendShape.name} skipped.");
			}
		}
	}

	internal bool AddSavedBlendShape(SkinnedMeshRenderer smr, MeshBlendShape meshBlendShape)
	{
		if (BlendShapeAlreadyExists(smr, meshBlendShape.BlendShape.name))
		{
			BlendShapeController blendShapeController = new BlendShapeController(smr, meshBlendShape.BlendShape.name);
			if (blendShapeController.blendShape == null)
			{
				if (PregnancyPlusPlugin.DebugLog.Value)
				{
					PregnancyPlusPlugin.Logger.LogWarning((object)("LoopMeshAndAddExistingBlendShape > There was a problem finding the blendshape $" + meshBlendShape.BlendShape.name));
				}
				return false;
			}
			blendShapeController.ApplyBlendShapeWeight(smr, meshBlendShape.BlendShape.weight, floatLerp: false);
			return true;
		}
		BlendShapeController blendShapeController2 = new BlendShapeController(meshBlendShape.BlendShape, smr);
		blendShapeController2.ApplyBlendShapeWeight(smr, meshBlendShape.BlendShape.weight, floatLerp: false);
		return true;
	}

	internal bool BlendShapeAlreadyExists(SkinnedMeshRenderer smr, string blendShapeName)
	{
		int blendShapeIndex = new BlendShapeController().GetBlendShapeIndex(smr, blendShapeName);
		return blendShapeIndex >= 0;
	}

	internal BlendShapeController CreateOrMergeBlendShape(SkinnedMeshRenderer smr, string renderKey, string blendShapeTag = null)
	{
		if (!smr.sharedMesh.isReadable)
		{
			nativeDetour.Apply();
		}
		if (!md.TryGetValue(renderKey, out var value) || !value.HasInflatedVerts)
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogInfo((object)("CreateOrMergeBlendShape > smr '" + renderKey + "' meshData do not exists, skipping"));
			}
			nativeDetour.Undo();
			return new BlendShapeController();
		}
		if (!value.HasDeltas)
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogWarning((object)(" CreateOrMergeBlendShape must have deltas present in MeshData, no shape applied for " + ((Object)smr).name));
			}
			return new BlendShapeController();
		}
		string blendShapeName = GetBlendShapeName(renderKey, blendShapeTag);
		BlendShapeController blendShapeController;
		if (new BlendShapeController().HasBlendshape(smr, blendShapeName))
		{
			blendShapeController = new BlendShapeController(smr, blendShapeName);
			blendShapeController.MergeMeshDeltas(smr, value, infConfig.inflationSize / 40f);
		}
		else
		{
			blendShapeController = new BlendShapeController(value, blendShapeName, smr);
		}
		nativeDetour.Undo();
		return blendShapeController;
	}

	internal BlendShapeController CreateBlendShape(SkinnedMeshRenderer smr, string renderKey, string blendShapeTag = null)
	{
		if (!smr.sharedMesh.isReadable)
		{
			nativeDetour.Apply();
		}
		if (!md.TryGetValue(renderKey, out var value) || !value.HasInflatedVerts)
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogInfo((object)("CreateBlendShape > smr '" + renderKey + "' meshData do not exists, skipping"));
			}
			nativeDetour.Undo();
			return new BlendShapeController();
		}
		if (!value.HasDeltas)
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogWarning((object)(" CreateBlendShape must have deltas present in MeshData, no shape applied for " + ((Object)smr).name));
			}
			return new BlendShapeController();
		}
		string blendShapeName = GetBlendShapeName(renderKey, blendShapeTag);
		BlendShapeController result = new BlendShapeController(value, blendShapeName, smr);
		nativeDetour.Undo();
		return result;
	}

	internal Mesh PrepForBlendShape(SkinnedMeshRenderer smr, string renderKey, Matrix4x4 rotationUndo)
	{
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)smr == (Object)null)
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogWarning((object)" PrepForBlendShape smr was null");
			}
			return null;
		}
		if (!md.TryGetValue(renderKey, out var value) || !value.HasInflatedVerts)
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogInfo((object)("PrepForBlendShape > smr '" + renderKey + "' meshData do not exists, or does not have inflatedVerts, skipping"));
			}
			return null;
		}
		Mesh val = PregnancyPlusHelper.CopyMesh(smr.sharedMesh);
		if (!val.isReadable)
		{
			nativeDetour.Apply();
		}
		if (md[renderKey].VertexCount != val.vertexCount)
		{
			PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(charaFileName, ErrorCode.PregPlus_IncorrectVertCount, $"PrepForBlendShape > smr.sharedMesh '{renderKey}' has incorrect vert count {md[renderKey].VertexCount}|{val.vertexCount}");
			nativeDetour.Undo();
			return null;
		}
		val.vertices = md[renderKey].inflatedVertices;
		val.RecalculateBounds();
		val.RecalculateNormals(40f, md[renderKey].alteredVerticieIndexes, rotationUndo);
		if (val.isReadable)
		{
			val.RecalculateTangents();
		}
		nativeDetour.Undo();
		return val;
	}

	internal string GetBlendShapeName(string renderKey, string blendShapeTag = null)
	{
		if (blendShapeTag != null)
		{
			return renderKey + "_KK_PregnancyPlus_" + blendShapeTag;
		}
		return renderKey + "_KK_PregnancyPlus";
	}

	internal bool ApplyBlendShapeWeight(SkinnedMeshRenderer smr, string renderKey, bool needsOverwrite, string blendShapeTag = null)
	{
		string blendShapeName = GetBlendShapeName(renderKey, blendShapeTag);
		if (!smr.sharedMesh.isReadable)
		{
			nativeDetour.Apply();
		}
		BlendShapeController blendShapeController = new BlendShapeController(smr, blendShapeName);
		nativeDetour.Undo();
		if (blendShapeController.blendShape == null || needsOverwrite)
		{
			PregnancyPlusHelper.GetClothCoefficients(((Component)smr).gameObject, renderKey, preservedClothCoefficients, coefficientCounter);
			blendShapeController = CreateBlendShape(smr, renderKey, blendShapeTag);
		}
		if (blendShapeController == null || blendShapeController.blendShape == null)
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogWarning((object)("ApplyBlendShapeWeight > There was a problem creating the blendshape $" + blendShapeName));
			}
			return false;
		}
		PregnancyPlusHelper.SetClothCoefficients(((Component)smr).gameObject, renderKey, preservedClothCoefficients, coefficientCounter);
		float weight = (isDuringInflationScene ? CurrentInflationChange : infConfig.inflationSize);
		return blendShapeController.ApplyBlendShapeWeight(smr, weight);
	}

	internal bool ResetBlendShapeWeight(SkinnedMeshRenderer smr, string renderKey, string blendShapeTag = null)
	{
		string blendShapeName = GetBlendShapeName(renderKey, blendShapeTag);
		BlendShapeController blendShapeController = new BlendShapeController(smr, blendShapeName);
		if (blendShapeController.blendShape == null)
		{
			return false;
		}
		return blendShapeController.ApplyBlendShapeWeight(smr, 0f);
	}

	internal List<BlendShapeController> ComputeHSceneBlendShapes()
	{
		List<BlendShapeController> list = new List<BlendShapeController>();
		MeshInflate(0f, "ComputeHSceneBlendShapes", new MeshInflateFlags(this, _checkForNewMesh: false, _freshStart: false, _pluginConfigSliderChanged: false, _visibilityUpdate: false, _bypassWhen0: true));
		List<SkinnedMeshRenderer> meshRenderers = PregnancyPlusHelper.GetMeshRenderers(((ChaInfo)base.ChaControl).objClothes);
		foreach (SkinnedMeshRenderer item in meshRenderers)
		{
			string blendShapeName = GetBlendShapeName(GetMeshKey(item), blendShapeTempTagName);
			BlendShapeController blendShapeController = new BlendShapeController(item, blendShapeName);
			if (blendShapeController.blendShape != null)
			{
				list.Add(blendShapeController);
			}
		}
		if (!PregnancyPlusPlugin.IgnoreAccessories.Value)
		{
			List<SkinnedMeshRenderer> meshRenderers2 = PregnancyPlusHelper.GetMeshRenderers(((ChaInfo)base.ChaControl).objAccessory);
			foreach (SkinnedMeshRenderer item2 in meshRenderers2)
			{
				string blendShapeName2 = GetBlendShapeName(GetMeshKey(item2), blendShapeTempTagName);
				BlendShapeController blendShapeController2 = new BlendShapeController(item2, blendShapeName2);
				if (blendShapeController2.blendShape != null)
				{
					list.Add(blendShapeController2);
				}
			}
		}
		List<SkinnedMeshRenderer> meshRenderers3 = PregnancyPlusHelper.GetMeshRenderers(((ChaInfo)base.ChaControl).objBody, findAll: true);
		foreach (SkinnedMeshRenderer item3 in meshRenderers3)
		{
			string blendShapeName3 = GetBlendShapeName(GetMeshKey(item3), blendShapeTempTagName);
			BlendShapeController blendShapeController3 = new BlendShapeController(item3, blendShapeName3);
			if (blendShapeController3.blendShape != null)
			{
				list.Add(blendShapeController3);
			}
		}
		if (PregnancyPlusPlugin.DebugBlendShapeLog.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)$"ComputeHSceneBlendShapes > Found {list.Count} blendshapes ");
		}
		return list;
	}

	internal void Legacy_CheckInitialUncensorGuid(List<MeshBlendShape> meshBlendShapes, string uncensorGUID)
	{
		bool flag = false;
		bool flag2 = true;
		List<SkinnedMeshRenderer> meshRenderers = PregnancyPlusHelper.GetMeshRenderers(((ChaInfo)base.ChaControl).objBody, findAll: true);
		if (meshRenderers == null || meshRenderers.Count <= 0)
		{
			return;
		}
		foreach (MeshBlendShape meshBlendShape in meshBlendShapes)
		{
			if (meshBlendShape.UncensorGUID != null)
			{
				flag2 = false;
			}
			if (meshBlendShape.BlendShape.name.Contains("o_body_"))
			{
				SkinnedMeshRenderer val = meshRenderers.Find((SkinnedMeshRenderer smr) => ((Object)smr).name == meshBlendShape.MeshName);
				if (!((Object)(object)val == (Object)null) && val.sharedMesh.vertexCount == meshBlendShape.VertCount)
				{
					flag = true;
				}
			}
		}
		if (flag2 && flag)
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogInfo((object)(" meshBlendShape.UncensorGUID is null but the mesh matches.  setting " + uncensorGUID + " to card "));
			}
			PregnancyPlusData cardData = GetCardData();
			AddUncensorGUID(meshBlendShapes, uncensorGUID);
			AddBlendShapesToData(cardData, meshBlendShapes);
			AddBlendShapesToData(infConfig, meshBlendShapes);
			SetExtendedData(cardData.Save(hotSwap: true));
		}
	}

	internal void Legacy_CheckNullUncensorGuid(List<MeshBlendShape> meshBlendShapes, MeshBlendShape meshBlendShape, SkinnedMeshRenderer smr, string uncensorGUID)
	{
		if (meshBlendShape.UncensorGUID == null && meshBlendShape.VertCount == smr.sharedMesh.vertexCount && meshBlendShape.MeshName.Contains("o_body_"))
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogInfo((object)(" CaptureNewBlendshapeWeights > appending uncensorGUID " + uncensorGUID + " to all saved blendshapes since there was not one already"));
			}
			AddUncensorGUID(meshBlendShapes, uncensorGUID);
		}
	}

	internal void CheckVisibilityState(bool newState)
	{
		if (lastVisibleState || !newState)
		{
			return;
		}
		lastVisibleState = true;
		if (!StudioAPI.InsideStudio && !MakerAPI.InsideMaker && !isReloading)
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogInfo((object)$"+= CheckVisibilityState {charaFileName} {newState}");
			}
			MeshInflate(new MeshInflateFlags(this, _checkForNewMesh: false, _freshStart: false, _pluginConfigSliderChanged: false, _visibilityUpdate: true), "CheckVisibilityState");
		}
	}

	internal void ClothesStateChangeEvent(int chaID, int clothesKind)
	{
		if (initialized && chaID == ((ChaInfo)base.ChaControl).chaID)
		{
			float waitTime = 0.1f;
			((MonoBehaviour)this).StartCoroutine(WaitForClothMeshToSettle(waitTime, checkNewClothMesh: true, forceRecalcVerts: false, checkNewAccessoryMesh: false, clothesKind));
		}
	}

	internal void AccessoryStateChangeEvent(int chaID, int slotNum)
	{
		if (initialized && chaID == ((ChaInfo)base.ChaControl).chaID)
		{
			float waitTime = 0.1f;
			((MonoBehaviour)this).StartCoroutine(WaitForClothMeshToSettle(waitTime, checkNewClothMesh: false, forceRecalcVerts: false, checkNewAccessoryMesh: true, slotNum));
		}
	}

	internal void OnUncensorChanged()
	{
		if (ignoreNextUncensorHook)
		{
			ignoreNextUncensorHook = false;
			return;
		}
		if (PregnancyPlusPlugin.DebugLog.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)("+= OnUncensorChanged " + charaFileName));
		}
		if (initialized)
		{
			uncensorChanged = true;
			if (StudioAPI.InsideStudio || MakerAPI.InsideMaker)
			{
				((MonoBehaviour)this).StartCoroutine(UserTriggeredUncensorChange());
			}
		}
	}

	internal void GetWeeksAndSetBellySize(bool checkNewMesh = false, bool slidersChanged = false, string callee = "GetWeeksAndSetBellySize")
	{
		PregnancyPlusData cardData = GetCardData();
		if (cardData.inflationSize > 0f && cardData.GameplayEnabled)
		{
			MeshInflate(cardData, callee, new MeshInflateFlags(this, checkNewMesh, _freshStart: false, slidersChanged));
			return;
		}
		int weeksFromPregnancyPluginData = PregnancyPlusPlugin.Hooks_KK_Pregnancy.GetWeeksFromPregnancyPluginData(base.ChaControl, "KK_Pregnancy");
		if (PregnancyPlusPlugin.DebugLog.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)$" GetWeeksAndSetBellySize {((Object)base.ChaControl).name} >  Week:{weeksFromPregnancyPluginData} checkNewMesh:{checkNewMesh} slidersChanged:{slidersChanged}");
		}
		currentWeeks = weeksFromPregnancyPluginData;
		if (weeksFromPregnancyPluginData <= 0)
		{
			MeshInflate(0f, callee);
			return;
		}
		if (!infConfig.HasAnyValue(includeSize: false))
		{
			infConfig = GetDefaultShapeFor_KK_Pregnancy();
		}
		float inflationSize = Mathf.Lerp(0f, (float)weeksFromPregnancyPluginData, MaxStoryModeBellySize / 40f);
		MeshInflate(inflationSize, callee, new MeshInflateFlags(this, checkNewMesh, _freshStart: false, slidersChanged));
	}

	public void OnInflationChanged(float inflationCount, int maxInflationCount, int _inflationSpeed)
	{
		inflationSpeed = _inflationSpeed;
		float num = inflationCount / (float)maxInflationCount * 40f;
		if (PregnancyPlusPlugin.DebugLog.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)$" inflationCount {inflationCount} maxInflationCount {maxInflationCount} kkInflationSize {num}");
		}
		if ((!(infConfig.inflationSize > num) || !(num > 0f)) && MaxStoryModeBellySize != 0f)
		{
			if ((float)currentWeeks > _inflationStartSize)
			{
				float inflationChange = (TargetPregPlusSize = currentWeeks);
				_inflationStartSize = (_inflationChange = inflationChange);
			}
			if (!infConfig.HasAnyValue(includeSize: false))
			{
				infConfig = GetDefaultShapeFor_KK_Pregnancy();
			}
			blendShapeCtrlList = ComputeHSceneBlendShapes();
			TargetPregPlusSize = num * (MaxStoryModeBellySize / 40f);
			_inflationStartSize = _inflationChange;
		}
	}

	public void ComputeInflationChange()
	{
		if (!GameAPI.InsideHScene)
		{
			if (_inflationChange > 0f || TargetPregPlusSize > 0f)
			{
				ClearInflationStuff();
			}
			return;
		}
		if (_inflationChange == TargetPregPlusSize)
		{
			ClearInflationStuff();
			return;
		}
		_inflationChange = Mathf.Lerp(_inflationStartSize, TargetPregPlusSize, timeElapsed / (float)GetInflationSpeed());
		timeElapsed += Time.deltaTime;
		if (Math.Abs(_inflationChange - TargetPregPlusSize) < 0.05f)
		{
			_inflationChange = TargetPregPlusSize;
		}
		QuickInflate(_inflationChange, blendShapeCtrlList);
	}

	public int GetInflationSpeed()
	{
		return inflationSpeed;
	}

	public void QuickInflate(float inflationWeight, List<BlendShapeController> bscList)
	{
		foreach (BlendShapeController bsc in bscList)
		{
			if (!bsc.ApplyBlendShapeWeight(inflationWeight) && PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogInfo((object)" QuickInflate > an smr was null, skipping mesh");
			}
		}
	}

	public void AppendToQuickInflateList(SkinnedMeshRenderer smr)
	{
		if (!smr.sharedMesh.isReadable)
		{
			nativeDetour.Apply();
		}
		string blendShapeName = GetBlendShapeName(GetMeshKey(smr), blendShapeTempTagName);
		BlendShapeController blendShapeController = new BlendShapeController(smr, blendShapeName);
		nativeDetour.Undo();
		if (blendShapeController?.blendShape != null)
		{
			blendShapeCtrlList.Add(blendShapeController);
		}
	}

	public void ClearInflationStuff(bool fullReset = false)
	{
		timeElapsed = 0f;
		_inflationStartSize = _inflationChange;
		blendShapeCtrlList = new List<BlendShapeController>();
		if (fullReset)
		{
			_inflationStartSize = (_inflationChange = currentWeeks);
			TargetPregPlusSize = 0f;
			_inflationChange = 0f;
		}
	}

	public PregnancyPlusData GetDefaultShapeFor_KK_Pregnancy()
	{
		if (PregnancyPlusPlugin.DebugLog.Value)
		{
			PregnancyPlusPlugin.Logger.LogInfo((object)" GetDefaultInflationShape > Loading a Default belly shape");
		}
		PregnancyPlusData pregnancyPlusData = new PregnancyPlusData();
		pregnancyPlusData.pluginVersion = "7.8";
		if (PregnancyPlusPlugin.OverrideBelly != null && PregnancyPlusPlugin.OverrideBelly.Value)
		{
			if (PregnancyPlusPlugin.DebugLog.Value)
			{
				PregnancyPlusPlugin.Logger.LogInfo((object)" GetDefaultInflationShape > OverrideBelly is set, Loading custom belly shape");
			}
			float inflationSize = infConfig.inflationSize;
			PregnancyPlusData template = BellyTemplate.GetTemplate(PregnancyPlusPlugin.BellyShapeForOverride.Value);
			pregnancyPlusData.SetSliders(template);
			pregnancyPlusData.inflationSize = inflationSize;
			return pregnancyPlusData;
		}
		pregnancyPlusData.inflationMultiplier = 0.3f;
		pregnancyPlusData.inflationStretchX = -0.15f;
		pregnancyPlusData.inflationTaperY = -0.02f;
		pregnancyPlusData.inflationTaperZ = -0.003f;
		pregnancyPlusData.inflationShiftZ = -0.03f;
		pregnancyPlusData.inflationDrop = 0.15f;
		return pregnancyPlusData;
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.2.0.10050' (yours is '9.1.0.7988')
