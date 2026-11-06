using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AIProject;
using Manager;
using UniRx;
using UnityEngine;
using UnityEx;

public class HPointCtrl : Singleton<HPointCtrl>
{
	public class RayDistanceCompare : IComparer<RaycastHit>
	{
		public int Compare(RaycastHit x, RaycastHit y)
		{
			if (x.distance < y.distance)
			{
				return -1;
			}
			if (x.distance > y.distance)
			{
				return 1;
			}
			return 0;
		}
	}

	[Serializable]
	public struct MinHpoint
	{
		public int AreaID;

		public int PointID;

		public int yobaiPlace;
	}

	[Serializable]
	private class AreaGroupDefine
	{
		public List<int> IDs = new List<int>();
	}

	[SerializeField]
	private GameObject initNullObj;

	public List<UnityEx.ValueTuple<GameObject, HPoint>> lstMarker = new List<UnityEx.ValueTuple<GameObject, HPoint>>();

	private HScene hScene;

	private HSceneManager hSceneManager;

	private HSceneSprite hSceneSprite;

	private Camera Cam;

	private UnityEx.ValueTuple<Vector3, Quaternion, int> _InitNull;

	public int HEnterCategory = -1;

	private Dictionary<int, HPointList> hPointLists = new Dictionary<int, HPointList>();

	private HPoint[] housingHpoints;

	public bool IsMarker;

	private HSceneFlagCtrl ctrlFlag;

	public int playerSex;

	public bool ExistSecondfemale;

	private RaycastHit[] HitInfo = new RaycastHit[50];

	private RaycastHit[] HitInfo2 = new RaycastHit[100];

	public List<HScene.AnimationListInfo>[] lstAnimInfo = new List<HScene.AnimationListInfo>[6];

	private List<UnityEx.ValueTuple<HSceneManager.HEvent, int, HScene.StartMotion>> startList;

	private List<UnityEx.ValueTuple<HSceneManager.HEvent, int, HScene.StartMotion>> startListM;

	public bool InitUsePoint;

	[SerializeField]
	private int gpID = -1;

	private BasePoint[] BasePoints;

	private ShuffleRand rand = new ShuffleRand();

	private List<ForcedHideObject> CheckHitObjs = new List<ForcedHideObject>();

	private int[] LesPlaceID = new int[8] { 0, 1, 2, 3, 4, 5, 6, 13 };

	private int[] multiFemalePlaceID = new int[1];

	private int dildoPlaceID = 21;

	private int NotMerchantPlaceID = 5;

	private int kadOnaID = 20;

	private Dictionary<int, Dictionary<int, AreaGroupDefine>> Areagp = new Dictionary<int, Dictionary<int, AreaGroupDefine>>
	{
		{
			0,
			new Dictionary<int, AreaGroupDefine>
			{
				{
					0,
					new AreaGroupDefine
					{
						IDs = new List<int> { 0, 11, 14 }
					}
				},
				{
					1,
					new AreaGroupDefine
					{
						IDs = new List<int> { 4, 12, 15, 16 }
					}
				},
				{
					2,
					new AreaGroupDefine
					{
						IDs = new List<int> { 6, 13, 17 }
					}
				}
			}
		},
		{
			1,
			new Dictionary<int, AreaGroupDefine> { 
			{
				3,
				new AreaGroupDefine
				{
					IDs = new List<int> { 0, 1 }
				}
			} }
		}
	};

	private CrossFade fade;

	private float OffsetDownHeight = 5f;

	private float OffsetUpHeight = 20f;

	public UnityEx.ValueTuple<Vector3, Quaternion, int> InitNull
	{
		[CompilerGenerated]
		get
		{
			return _InitNull;
		}
	}

	public Dictionary<int, HPointList> HPointLists
	{
		[CompilerGenerated]
		get
		{
			return hPointLists;
		}
	}

	public HPoint[] HousingHpoints
	{
		[CompilerGenerated]
		get
		{
			return housingHpoints;
		}
	}

	public void InitHPoint()
	{
		hSceneManager = Singleton<HSceneManager>.Instance;
		startList = Singleton<Manager.Resources>.Instance.HSceneTable.lstStartAnimInfo;
		startListM = Singleton<Manager.Resources>.Instance.HSceneTable.lstStartAnimInfoM;
		lstAnimInfo = Singleton<Manager.Resources>.Instance.HSceneTable.lstAnimInfo;
		initNullObj = Singleton<Manager.Resources>.Instance.HSceneTable.HPointObj;
		AreagpInit();
		Dictionary<int, Dictionary<bool, ForcedHideObject[]>> areaOpenObjectTable = Singleton<Map>.Instance.AreaOpenObjectTable;
		CheckHitObjs.Clear();
		if (areaOpenObjectTable != null && areaOpenObjectTable.Count > 0)
		{
			foreach (KeyValuePair<int, Dictionary<bool, ForcedHideObject[]>> item in areaOpenObjectTable)
			{
				if (item.Value != null && item.Value.Count != 0)
				{
					int key = item.Key;
					if (item.Value.TryGetValue(key: false, out var value))
					{
						CheckHitObjs.AddRange(value);
					}
				}
			}
		}
		if (fade == null)
		{
			fade = hSceneManager.Player.CameraControl.CrossFade;
		}
	}

	public void MarkerObjSet(Vector3 PlayerPos, int mapID = 0, int AreaID = 0)
	{
		float num = 0f;
		if (!InitUsePoint)
		{
			num = (initNullObj.transform.position - PlayerPos).sqrMagnitude;
			if (num <= hSceneSprite.HpointSearchRange * hSceneSprite.HpointSearchRange)
			{
				lstMarker[0].Item2.SetEffectActive(set: true);
				Collider collider = lstMarker[0].Item2.GetCollider();
				if (collider != null)
				{
					collider.enabled = true;
				}
			}
		}
		bool flag = false;
		if (gpID == -1)
		{
			if (hPointLists.ContainsKey(mapID) && hPointLists[mapID].lst.ContainsKey(AreaID))
			{
				for (int i = 1; i < lstMarker.Count; i++)
				{
					int index = i;
					HPoint item = lstMarker[index].Item2;
					if (!hPointLists[mapID].lst[AreaID].Contains(item))
					{
						continue;
					}
					num = (item.transform.position - PlayerPos).sqrMagnitude;
					if (num > hSceneSprite.HpointSearchRange * hSceneSprite.HpointSearchRange)
					{
						continue;
					}
					if (playerSex == 1)
					{
						if (!CheckPlace(item._nPlace, 0))
						{
							continue;
						}
					}
					else if (ExistSecondfemale && !CheckPlace(item._nPlace, 1))
					{
						continue;
					}
					flag = false;
					foreach (UnityEx.ValueTuple<int, int> value2 in item._nPlace.Values)
					{
						if (value2.Item1 != dildoPlaceID)
						{
							continue;
						}
						flag = true;
						break;
					}
					if (!flag && CanInfo(item._nPlace) && !HitObstacle(PlayerPos, item) && HpointAreaOpen(item))
					{
						lstMarker[index].Item2.SetEffectActive(set: true);
						Collider collider = lstMarker[index].Item2.GetCollider();
						if (collider != null)
						{
							collider.enabled = true;
						}
					}
				}
			}
		}
		else if (hPointLists.ContainsKey(mapID))
		{
			if (!Areagp.TryGetValue(mapID, out var value))
			{
				return;
			}
			for (int j = 0; j < value[gpID].IDs.Count; j++)
			{
				if (!hPointLists[mapID].lst.ContainsKey(value[gpID].IDs[j]))
				{
					continue;
				}
				for (int k = 1; k < lstMarker.Count; k++)
				{
					int index = k;
					HPoint item2 = lstMarker[index].Item2;
					if (!hPointLists[mapID].lst[value[gpID].IDs[j]].Contains(item2))
					{
						continue;
					}
					num = (item2.transform.position - PlayerPos).sqrMagnitude;
					if (num > hSceneSprite.HpointSearchRange * hSceneSprite.HpointSearchRange)
					{
						continue;
					}
					if (playerSex == 1)
					{
						if (!CheckPlace(item2._nPlace, 0))
						{
							continue;
						}
					}
					else if (ExistSecondfemale && !CheckPlace(item2._nPlace, 1))
					{
						continue;
					}
					flag = false;
					foreach (UnityEx.ValueTuple<int, int> value3 in item2._nPlace.Values)
					{
						if (value3.Item1 != dildoPlaceID)
						{
							continue;
						}
						flag = true;
						break;
					}
					if (!flag && CanInfo(item2._nPlace) && !HitObstacle(PlayerPos, item2) && HpointAreaOpen(item2))
					{
						lstMarker[index].Item2.SetEffectActive(set: true);
						Collider collider = lstMarker[index].Item2.GetCollider();
						if (collider != null)
						{
							collider.enabled = true;
						}
					}
				}
			}
		}
		if (housingHpoints != null && housingHpoints.Length != 0)
		{
			for (int l = 1; l < lstMarker.Count; l++)
			{
				int index = l;
				HPoint item3 = lstMarker[index].Item2;
				if (!HousingListContain(item3))
				{
					continue;
				}
				num = (item3.transform.position - PlayerPos).sqrMagnitude;
				if (num > hSceneSprite.HpointSearchRange * hSceneSprite.HpointSearchRange)
				{
					continue;
				}
				if (playerSex == 1)
				{
					if (!CheckPlace(item3._nPlace, 0))
					{
						continue;
					}
				}
				else if (ExistSecondfemale && !CheckPlace(item3._nPlace, 1))
				{
					continue;
				}
				flag = false;
				foreach (UnityEx.ValueTuple<int, int> value4 in item3._nPlace.Values)
				{
					if (value4.Item1 != dildoPlaceID)
					{
						continue;
					}
					flag = true;
					break;
				}
				if (!flag && CanInfo(item3._nPlace) && !HitObstacle(PlayerPos, item3) && HpointAreaOpen(item3))
				{
					lstMarker[index].Item2.SetEffectActive(set: true);
					Collider collider = lstMarker[index].Item2.GetCollider();
					if (collider != null)
					{
						collider.enabled = true;
					}
				}
			}
		}
		IsMarker = true;
	}

	public void MarkerObjDel()
	{
		for (int i = 0; i < lstMarker.Count; i++)
		{
			if (lstMarker[i].Item2.EffectActive())
			{
				lstMarker[i].Item2.SetEffectActive(set: false);
				Collider collider = lstMarker[i].Item2.GetCollider();
				if (collider != null)
				{
					collider.enabled = false;
				}
			}
		}
		IsMarker = false;
	}

	private bool HousingListContain(HPoint target)
	{
		if (housingHpoints == null)
		{
			return false;
		}
		for (int i = 0; i < housingHpoints.Length; i++)
		{
			if (housingHpoints[i] == target)
			{
				return true;
			}
		}
		return false;
	}

	public void Init(int mapID = 0, int AreaID = 0)
	{
		lstMarker.Clear();
		housingHpoints = null;
		lstMarker.Add(new UnityEx.ValueTuple<GameObject, HPoint>(null, initNullObj.GetComponent<HPoint>()));
		hPointLists = Singleton<Manager.Resources>.Instance.HSceneTable.hPointLists;
		gpID = -1;
		HPointList hPointList = hPointLists[mapID];
		if (!Areagp.TryGetValue(mapID, out var value))
		{
			return;
		}
		foreach (KeyValuePair<int, AreaGroupDefine> item in value)
		{
			if (!item.Value.IDs.Contains(AreaID))
			{
				continue;
			}
			gpID = item.Key;
			for (int i = 0; i < item.Value.IDs.Count; i++)
			{
				int key = item.Value.IDs[i];
				if (hPointList.lst.ContainsKey(key))
				{
					for (int j = 0; j < hPointList.lst[key].Count; j++)
					{
						lstMarker.Add(new UnityEx.ValueTuple<GameObject, HPoint>(hPointList.lst[key][j].markerPos.gameObject, hPointList.lst[key][j]));
					}
				}
			}
			break;
		}
		if (gpID == -1 && hPointList.lst.ContainsKey(AreaID))
		{
			for (int k = 0; k < hPointList.lst[AreaID].Count; k++)
			{
				lstMarker.Add(new UnityEx.ValueTuple<GameObject, HPoint>(hPointList.lst[AreaID][k].markerPos.gameObject, hPointList.lst[AreaID][k]));
			}
		}
		if (!(Singleton<Map>.Instance.PointAgent != null) || Singleton<Map>.Instance.PointAgent.BasePoints == null)
		{
			return;
		}
		BasePoints = Singleton<Map>.Instance.PointAgent.BasePoints;
		for (int l = 0; l < BasePoints.Length; l++)
		{
			if (!(BasePoints[l].OwnerArea == null) && gpID != -1 && value[gpID].IDs.Contains(BasePoints[l].OwnerArea.AreaID))
			{
				housingHpoints = Singleton<Manager.Housing>.Instance.GetHPoint(BasePoints[l].ID);
			}
		}
		if (housingHpoints != null)
		{
			for (int m = 0; m < housingHpoints.Length; m++)
			{
				lstMarker.Add(new UnityEx.ValueTuple<GameObject, HPoint>(housingHpoints[m].markerPos.gameObject, housingHpoints[m]));
			}
		}
	}

	public void SetStartPos(Transform trans, int basho)
	{
		_InitNull = new UnityEx.ValueTuple<Vector3, Quaternion, int>(trans.position, trans.rotation, basho);
		HPoint component = initNullObj.GetComponent<HPoint>();
		component.id = -1;
		component.Init();
		if (!component._nPlace.ContainsKey(0))
		{
			component._nPlace.Add(0, new UnityEx.ValueTuple<int, int>(_InitNull.Item3, HEnterCategory));
		}
		else
		{
			component._nPlace[0] = new UnityEx.ValueTuple<int, int>(_InitNull.Item3, HEnterCategory);
		}
		lstMarker[0] = new UnityEx.ValueTuple<GameObject, HPoint>(component.markerPos.gameObject, component);
		if (initNullObj.transform.parent != base.transform)
		{
			initNullObj.transform.SetParent(base.transform);
		}
		initNullObj.transform.position = _InitNull.Item1;
		initNullObj.transform.rotation = _InitNull.Item2;
		if (!InitUsePoint)
		{
			ctrlFlag.nowHPoint = component;
		}
	}

	private void Update()
	{
		if (!IsMarker)
		{
			return;
		}
		int num = -1;
		if (!hSceneSprite.IsSpriteOver())
		{
			Ray ray = Cam.ScreenPointToRay(UnityEngine.Input.mousePosition);
			int layerMask = 1;
			int num2 = Physics.RaycastNonAlloc(ray, HitInfo, 1000f, layerMask);
			if (num2 > 0)
			{
				Array.Sort(HitInfo, 0, num2, new RayDistanceCompare());
				num = HitObjectFind(num2);
			}
		}
		if (num != -1 && !ctrlFlag.nowOrgasm && !hScene.NowChangeAnim && Singleton<Manager.Input>.Instance.IsPressedKey(ActionID.MouseLeft))
		{
			ctrlFlag.pointMoveAnimChange = true;
			HScene.AnimationListInfo nowAnimationInfo = ctrlFlag.nowAnimationInfo;
			fade.FadeStart(1.5f);
			OffSetMove(num, nowAnimationInfo);
			if (lstMarker[ctrlFlag.HPointID].Item2.id != lstMarker[num].Item2.id)
			{
				ChangeAnim(num);
			}
			else
			{
				ctrlFlag.pointMoveAnimChange = false;
			}
			ctrlFlag.HPointID = num;
			ChangeHItem componentInChildren = ctrlFlag.nowHPoint.GetComponentInChildren<ChangeHItem>();
			if (componentInChildren != null)
			{
				componentInChildren.ChangeActive(val: true);
			}
			lstMarker[ctrlFlag.HPointID].Item2.SetEffectActive(set: false);
			componentInChildren = lstMarker[ctrlFlag.HPointID].Item2.GetComponentInChildren<ChangeHItem>();
			if (componentInChildren != null)
			{
				componentInChildren.ChangeActive(val: false);
			}
			ctrlFlag.nowHPoint = lstMarker[ctrlFlag.HPointID].Item2;
			hSceneSprite.RefleshAutoButtom();
			hSceneSprite.usePoint = true;
			hSceneSprite.SetMotionListDraw(_active: false);
			hSceneSprite.LoadMotionList(ctrlFlag.categoryMotionList);
			hSceneSprite.categoryMain.gameObject.SetActive(value: false);
			hSceneSprite.MarkerObjSet();
		}
	}

	private void OffSetMove(int hitID, HScene.AnimationListInfo info)
	{
		Vector3 pos = Vector3.zero;
		Vector3 rot = Vector3.zero;
		int num = -1;
		for (int i = 0; i < info.lstOffset.Count; i++)
		{
			if (lstMarker[hitID].Item2._nPlace[0].Item1 == info.nPositons[i])
			{
				num = i;
				break;
			}
		}
		if (num >= 0 && info.lstOffset[num] != null && info.lstOffset[num] != string.Empty)
		{
			hScene.LoadMoveOffset(info.lstOffset[num], out pos, out rot);
		}
		hScene.SetMovePositionPoint(lstMarker[hitID].Item2.pivot, pos, rot);
	}

	private void ChangeAnim(int hitID)
	{
		List<HScene.AnimationListInfo> infos = new List<HScene.AnimationListInfo>();
		int item = lstMarker[hitID].Item2._nPlace[0].Item1;
		if (hSceneManager.bMerchant && item >= NotMerchantPlaceID)
		{
			for (int i = 0; i < lstMarker[hitID].Item2._nPlace.Count; i++)
			{
				if (lstMarker[hitID].Item2._nPlace[i].Item1 < NotMerchantPlaceID)
				{
					item = lstMarker[hitID].Item2._nPlace[i].Item1;
					break;
				}
			}
		}
		if (ctrlFlag.nowAnimationInfo.nPositons.Contains(item))
		{
			ctrlFlag.pointMoveAnimChange = false;
			ctrlFlag.nPlace = item;
			return;
		}
		List<UnityEx.ValueTuple<HSceneManager.HEvent, int, HScene.StartMotion>> list = startList;
		if (hSceneManager.bMerchant)
		{
			list = startListM;
		}
		if (playerSex == 0)
		{
			for (int j = 0; j < list.Count; j++)
			{
				if (list[j].Item2 != item || (ExistSecondfemale && list[j].Item3.mode != 5) || (!ExistSecondfemale && list[j].Item3.mode == 5))
				{
					continue;
				}
				ctrlFlag.nPlace = item;
				int mode = list[j].Item3.mode;
				int id = list[j].Item3.id;
				if (mode == 4 || (hSceneManager.bMerchant && hSceneManager.MerchantLimit == 1 && mode != 1) || lstMarker[hitID].Item2.notMotion[mode].motionID.Contains(id) || lstAnimInfo.Length < mode)
				{
					continue;
				}
				int num = -1;
				for (int k = 0; k < lstAnimInfo[mode].Count; k++)
				{
					if (lstAnimInfo[mode][k].id == id)
					{
						num = k;
					}
				}
				if (num == -1)
				{
					continue;
				}
				if (hSceneManager.bMerchant)
				{
					if (!lstAnimInfo[mode][num].bMerchantMotion || lstAnimInfo[mode][num].nIyaAction == 2)
					{
						continue;
					}
				}
				else if (hSceneManager.EventKind != HSceneManager.HEvent.Yobai)
				{
					if (hSceneManager.isForce)
					{
						if (lstAnimInfo[mode][num].nIyaAction == 0)
						{
							continue;
						}
					}
					else if (lstAnimInfo[mode][num].nIyaAction == 2)
					{
						continue;
					}
				}
				else if (!lstAnimInfo[mode][num].bSleep)
				{
					continue;
				}
				if (lstAnimInfo[mode][num].isNeedItem && !hSceneManager.CheckHadItem(lstAnimInfo[mode][num].ActionCtrl.Item1, lstAnimInfo[mode][num].id))
				{
					continue;
				}
				if (ctrlFlag.isFaintness)
				{
					if (lstAnimInfo[mode][num].nDownPtn == 0)
					{
						continue;
					}
				}
				else if (lstAnimInfo[mode][num].nFaintnessLimit == 1)
				{
					continue;
				}
				if (lstAnimInfo[mode][num].nInitiativeFemale == 0)
				{
					infos.Add(lstAnimInfo[mode][num]);
				}
			}
			if (infos.Count == 0)
			{
				return;
			}
			int randID2 = 0;
			rand.Init(infos.Count);
			randID2 = rand.Get();
			Observable.FromCoroutine(() => hScene.ChangeAnimation(infos[randID2], _isForceResetCamera: false, _isForceLoopAction: false, _UseFade: false)).Finally(delegate
			{
				ctrlFlag.selectAnimationListInfo = null;
				ctrlFlag.isAutoActionChange = false;
				if (ctrlFlag.pointMoveAnimChange)
				{
					ctrlFlag.pointMoveAnimChange = false;
				}
				GlobalMethod.setCameraMoveFlag(ctrlFlag.cameraCtrl, _bPlay: true);
				hSceneSprite.ChangeStart = false;
			}).Subscribe();
		}
		else
		{
			if (playerSex != 1)
			{
				return;
			}
			for (int l = 0; l < list.Count; l++)
			{
				if (list[l].Item2 != item || list[l].Item3.mode != 4)
				{
					continue;
				}
				ctrlFlag.nPlace = item;
				int mode2 = list[l].Item3.mode;
				int id2 = list[l].Item3.id;
				if (lstMarker[hitID].Item2.notMotion[mode2].motionID.Contains(id2) || lstAnimInfo.Length < mode2)
				{
					continue;
				}
				int num2 = -1;
				for (int m = 0; m < lstAnimInfo[mode2].Count; m++)
				{
					if (lstAnimInfo[mode2][m].id == id2)
					{
						num2 = m;
					}
				}
				if (num2 == -1 || (hSceneManager.bMerchant && !lstAnimInfo[mode2][num2].bMerchantMotion) || (lstAnimInfo[mode2][num2].isNeedItem && !hSceneManager.CheckHadItem(lstAnimInfo[mode2][num2].ActionCtrl.Item1, lstAnimInfo[mode2][num2].id)))
				{
					continue;
				}
				if (ctrlFlag.isFaintness)
				{
					if (lstAnimInfo[mode2][num2].nDownPtn == 0)
					{
						continue;
					}
				}
				else if (lstAnimInfo[mode2][num2].nFaintnessLimit == 1)
				{
					continue;
				}
				if (lstAnimInfo[mode2][num2].nInitiativeFemale == 0)
				{
					infos.Add(lstAnimInfo[mode2][num2]);
				}
			}
			if (infos.Count == 0)
			{
				return;
			}
			int randID = 0;
			rand.Init(infos.Count);
			randID = rand.Get();
			Observable.FromCoroutine(() => hScene.ChangeAnimation(infos[randID], _isForceResetCamera: false, _isForceLoopAction: false, _UseFade: false)).Finally(delegate
			{
				ctrlFlag.selectAnimationListInfo = null;
				ctrlFlag.isAutoActionChange = false;
				if (ctrlFlag.pointMoveAnimChange)
				{
					ctrlFlag.pointMoveAnimChange = false;
				}
				GlobalMethod.setCameraMoveFlag(ctrlFlag.cameraCtrl, _bPlay: true);
				hSceneSprite.ChangeStart = false;
			}).Subscribe();
		}
	}

	public static bool DicTupleContainsValue(Dictionary<int, UnityEx.ValueTuple<int, int>> dic, int target, int item)
	{
		foreach (KeyValuePair<int, UnityEx.ValueTuple<int, int>> item2 in dic)
		{
			if ((item == 0 && item2.Value.Item1 == target) || (item == 1 && item2.Value.Item2 == target))
			{
				return true;
			}
		}
		return false;
	}

	public static bool DicTupleContainsValue(Dictionary<int, UnityEx.ValueTuple<int, int>> dic, List<int> target, int item)
	{
		foreach (KeyValuePair<int, UnityEx.ValueTuple<int, int>> item2 in dic)
		{
			if ((item == 0 && target.Contains(item2.Value.Item1)) || (item == 1 && target.Contains(item2.Value.Item2)))
			{
				return true;
			}
		}
		return false;
	}

	public void SetHSceneSprite(HSceneSprite sprite, HScene HScene)
	{
		ctrlFlag = Singleton<HSceneFlagCtrl>.Instance;
		hSceneSprite = sprite;
		hScene = HScene;
	}

	public void CameraSet()
	{
		Cam = ctrlFlag.cameraCtrl.thisCamera;
	}

	private int HitObjectFind(int hitNum)
	{
		int result = -1;
		for (int i = 0; i < hitNum; i++)
		{
			for (int j = 0; j < lstMarker.Count; j++)
			{
				Collider collider = lstMarker[j].Item2.GetCollider();
				if (!(collider == null) && !(collider != HitInfo[i].collider) && j != ctrlFlag.HPointID)
				{
					return j;
				}
			}
		}
		return result;
	}

	public bool CheckStartPoint(ref Transform res, int place = -2)
	{
		int mapID = Singleton<Map>.Instance.MapID;
		int areaID = hSceneManager.Player.AreaID;
		MinHpoint minHpoint = default(MinHpoint);
		minHpoint.AreaID = -1;
		minHpoint.PointID = -1;
		minHpoint.yobaiPlace = -1;
		int num = -1;
		List<MinHpoint> list = new List<MinHpoint>();
		gpID = -1;
		if (!Areagp.TryGetValue(mapID, out var value))
		{
			return false;
		}
		foreach (KeyValuePair<int, AreaGroupDefine> item in value)
		{
			if (!item.Value.IDs.Contains(areaID))
			{
				continue;
			}
			gpID = item.Key;
			break;
		}
		List<bool> list2 = new List<bool>();
		if (gpID == -1)
		{
			list2.Add(hPointLists.Count == 0 || !hPointLists.ContainsKey(mapID) || !hPointLists[mapID].lst.ContainsKey(areaID) || hPointLists[mapID].lst[areaID].Count == 0);
		}
		else
		{
			for (int i = 0; i < value[gpID].IDs.Count; i++)
			{
				int key = value[gpID].IDs[i];
				list2.Add(hPointLists.Count == 0 || !hPointLists.ContainsKey(mapID) || !hPointLists[mapID].lst.ContainsKey(key) || hPointLists[mapID].lst[key].Count == 0);
			}
		}
		bool flag = housingHpoints == null || housingHpoints.Length == 0;
		bool[] array = new bool[2];
		if (!list2.Contains(item: false) && flag)
		{
			return false;
		}
		if (list2.Contains(item: false))
		{
			if (gpID == -1)
			{
				minHpoint.AreaID = areaID;
				minHpoint.PointID = PlayerClosePointID(res, hPointLists[mapID].lst[areaID], place);
			}
			else
			{
				for (int j = 0; j < value[gpID].IDs.Count; j++)
				{
					if (!list2[j])
					{
						list.Add(new MinHpoint
						{
							AreaID = value[gpID].IDs[j],
							PointID = PlayerClosePointID(res, hPointLists[mapID].lst[value[gpID].IDs[j]], place)
						});
					}
				}
				foreach (MinHpoint item2 in list)
				{
					if (item2.PointID == -1)
					{
						continue;
					}
					if (minHpoint.AreaID == -1 || minHpoint.PointID == -1)
					{
						minHpoint = item2;
						continue;
					}
					float sqrMagnitude = (hPointLists[mapID].lst[item2.AreaID][item2.PointID].transform.position - res.position).sqrMagnitude;
					float sqrMagnitude2 = (hPointLists[mapID].lst[minHpoint.AreaID][minHpoint.PointID].transform.position - res.position).sqrMagnitude;
					if (sqrMagnitude < sqrMagnitude2)
					{
						minHpoint.AreaID = item2.AreaID;
						minHpoint.PointID = item2.PointID;
					}
				}
			}
		}
		if (minHpoint.AreaID != -1 && minHpoint.PointID != -1)
		{
			array[0] = true;
		}
		if (!flag)
		{
			num = PlayerClosePointID(res, housingHpoints, place);
		}
		if (num != -1)
		{
			array[1] = true;
		}
		if (!array[0] && !array[1])
		{
			return false;
		}
		bool flag2 = true;
		if (array[0] && array[1])
		{
			float num2 = (hPointLists[mapID].lst[minHpoint.AreaID][minHpoint.PointID].transform.position - res.position).sqrMagnitude - (HousingHpoints[num].transform.position - res.position).sqrMagnitude;
			if (num2 >= 0f)
			{
				res.position = HousingHpoints[num].pivot.position;
				res.rotation = HousingHpoints[num].pivot.rotation;
				if (hSceneManager.females[1] == null || hSceneManager.females[1].GetComponent<PlayerActor>() != null)
				{
					if (place == -2)
					{
						hSceneManager.height = housingHpoints[num]._nPlace[0].Item1;
					}
					else
					{
						hSceneManager.height = place;
					}
				}
				else
				{
					hSceneManager.height = 0;
				}
				flag2 = false;
			}
			else
			{
				res.position = hPointLists[mapID].lst[minHpoint.AreaID][minHpoint.PointID].pivot.position;
				res.rotation = hPointLists[mapID].lst[minHpoint.AreaID][minHpoint.PointID].pivot.rotation;
				if (hSceneManager.females[1] == null || hSceneManager.females[1].GetComponent<PlayerActor>() != null)
				{
					if (place == -2)
					{
						hSceneManager.height = housingHpoints[num]._nPlace[0].Item1;
					}
					else
					{
						hSceneManager.height = place;
					}
				}
				else
				{
					hSceneManager.height = 0;
				}
				flag2 = true;
			}
		}
		else if (array[0])
		{
			res.position = hPointLists[mapID].lst[minHpoint.AreaID][minHpoint.PointID].pivot.position;
			res.rotation = hPointLists[mapID].lst[minHpoint.AreaID][minHpoint.PointID].pivot.rotation;
			if (hSceneManager.females[1] == null || hSceneManager.females[1].GetComponent<PlayerActor>() != null)
			{
				if (place == -2)
				{
					hSceneManager.height = housingHpoints[num]._nPlace[0].Item1;
				}
				else
				{
					hSceneManager.height = place;
				}
			}
			else
			{
				hSceneManager.height = 0;
			}
			flag2 = true;
		}
		else if (array[1])
		{
			res.position = HousingHpoints[num].pivot.position;
			res.rotation = HousingHpoints[num].pivot.rotation;
			if (hSceneManager.females[1] == null || hSceneManager.females[1].GetComponent<PlayerActor>() != null)
			{
				if (place == -2)
				{
					hSceneManager.height = housingHpoints[num]._nPlace[0].Item1;
				}
				else
				{
					hSceneManager.height = place;
				}
			}
			else
			{
				hSceneManager.height = 0;
			}
			flag2 = false;
		}
		if (flag2)
		{
			int num3 = 0;
			if (gpID != -1)
			{
				foreach (KeyValuePair<int, List<HPoint>> item3 in hPointLists[mapID].lst)
				{
					if (value.ContainsKey(gpID) && value[gpID].IDs.Contains(item3.Key) && item3.Key < minHpoint.AreaID)
					{
						num3 += item3.Value.Count;
					}
				}
			}
			ctrlFlag.HPointID = num3 + minHpoint.PointID + 1;
			ctrlFlag.nowHPoint = hPointLists[mapID].lst[minHpoint.AreaID][minHpoint.PointID];
		}
		else
		{
			if (!list2.Contains(item: false))
			{
				ctrlFlag.HPointID = num + 1;
			}
			else if (gpID == -1)
			{
				ctrlFlag.HPointID = num + 1 + hPointLists[mapID].lst[areaID].Count;
			}
			else
			{
				int num4 = 0;
				for (int k = 0; k < value[gpID].IDs.Count; k++)
				{
					if (!list2[k])
					{
						num4 += hPointLists[mapID].lst[value[gpID].IDs[k]].Count;
					}
				}
				ctrlFlag.HPointID = num + 1 + num4;
			}
			ctrlFlag.nowHPoint = housingHpoints[num];
		}
		ChangeHItem componentInChildren = ctrlFlag.nowHPoint.GetComponentInChildren<ChangeHItem>();
		if (componentInChildren != null)
		{
			componentInChildren.ChangeActive(val: false);
		}
		return true;
	}

	public bool CheckStartPointYobai(ref Transform res)
	{
		int mapID = Singleton<Map>.Instance.MapID;
		int areaID = hSceneManager.Player.AreaID;
		MinHpoint minHpoint = default(MinHpoint);
		minHpoint.AreaID = -1;
		minHpoint.PointID = -1;
		minHpoint.yobaiPlace = -1;
		int[] array = new int[2];
		int num = -1;
		int num2 = 0;
		List<MinHpoint> list = new List<MinHpoint>();
		gpID = -1;
		if (!Areagp.TryGetValue(mapID, out var value))
		{
			return false;
		}
		foreach (KeyValuePair<int, AreaGroupDefine> item in value)
		{
			if (!item.Value.IDs.Contains(areaID))
			{
				continue;
			}
			gpID = item.Key;
			break;
		}
		List<bool> list2 = new List<bool>();
		if (gpID == -1)
		{
			list2.Add(hPointLists.Count == 0 || !hPointLists.ContainsKey(mapID) || !hPointLists[mapID].lst.ContainsKey(areaID) || hPointLists[mapID].lst[areaID].Count == 0);
		}
		else
		{
			for (int i = 0; i < value[gpID].IDs.Count; i++)
			{
				int key = value[gpID].IDs[i];
				list2.Add(hPointLists.Count == 0 || !hPointLists.ContainsKey(mapID) || !hPointLists[mapID].lst.ContainsKey(key) || hPointLists[mapID].lst[key].Count == 0);
			}
		}
		bool flag = housingHpoints == null || housingHpoints.Length == 0;
		bool[] array2 = new bool[2];
		if (!list2.Contains(item: false) && flag)
		{
			return false;
		}
		if (list2.Contains(item: false))
		{
			if (gpID == -1)
			{
				minHpoint.AreaID = areaID;
				array[0] = PlayerClosePointID(res, hPointLists[mapID].lst[areaID], 0);
				array[1] = PlayerClosePointID(res, hPointLists[mapID].lst[areaID], 7);
				if (array[0] != array[1])
				{
					if (array[0] == -1)
					{
						minHpoint.PointID = array[1];
						minHpoint.yobaiPlace = 1;
					}
					else if (array[1] == -1)
					{
						minHpoint.PointID = array[0];
						minHpoint.yobaiPlace = 0;
					}
					else
					{
						minHpoint.yobaiPlace = YobaiMin(res.position, hPointLists[mapID].lst[areaID], array);
						minHpoint.PointID = array[minHpoint.yobaiPlace];
					}
				}
				else
				{
					minHpoint.PointID = array[0];
					minHpoint.yobaiPlace = 0;
				}
			}
			else
			{
				for (int j = 0; j < value[gpID].IDs.Count; j++)
				{
					if (list2[j])
					{
						continue;
					}
					array[0] = PlayerClosePointID(res, hPointLists[mapID].lst[value[gpID].IDs[j]], 0);
					array[1] = PlayerClosePointID(res, hPointLists[mapID].lst[value[gpID].IDs[j]], 7);
					int pointID;
					int num3;
					if (array[0] != array[1])
					{
						if (array[0] == -1)
						{
							pointID = array[1];
							num3 = 1;
						}
						else if (array[1] == -1)
						{
							pointID = array[0];
							num3 = 0;
						}
						else
						{
							num3 = YobaiMin(res.position, hPointLists[mapID].lst[areaID], array);
							pointID = array[num3];
						}
					}
					else
					{
						pointID = array[0];
						num3 = 0;
					}
					list.Add(new MinHpoint
					{
						AreaID = value[gpID].IDs[j],
						PointID = pointID,
						yobaiPlace = num3
					});
				}
				foreach (MinHpoint item2 in list)
				{
					if (item2.PointID == -1)
					{
						continue;
					}
					if (minHpoint.AreaID == -1 || minHpoint.PointID == -1)
					{
						minHpoint = item2;
						continue;
					}
					float sqrMagnitude = (hPointLists[mapID].lst[item2.AreaID][item2.PointID].transform.position - res.position).sqrMagnitude;
					float sqrMagnitude2 = (hPointLists[mapID].lst[minHpoint.AreaID][minHpoint.PointID].transform.position - res.position).sqrMagnitude;
					if (sqrMagnitude < sqrMagnitude2)
					{
						minHpoint.AreaID = item2.AreaID;
						minHpoint.PointID = item2.PointID;
						minHpoint.yobaiPlace = item2.yobaiPlace;
					}
				}
			}
		}
		if (minHpoint.AreaID != -1 && minHpoint.PointID != -1)
		{
			array2[0] = true;
		}
		if (!flag)
		{
			int[] array3 = new int[2]
			{
				PlayerClosePointID(res, housingHpoints, 0),
				PlayerClosePointID(res, housingHpoints, 7)
			};
			if (array3[0] != array3[1])
			{
				if (array3[0] == -1)
				{
					num = array3[1];
					num2 = 1;
				}
				else if (array3[1] == -1)
				{
					num = array3[0];
					num2 = 0;
				}
				else
				{
					num2 = YobaiMin(res.position, housingHpoints, array3);
					num = array3[num2];
				}
			}
			else
			{
				num = array3[0];
				num2 = 0;
			}
		}
		if (num != -1)
		{
			array2[1] = true;
		}
		if (!array2[0] && !array2[1])
		{
			return false;
		}
		bool flag2 = true;
		if (array2[0] && array2[1])
		{
			float num4 = (hPointLists[mapID].lst[minHpoint.AreaID][minHpoint.PointID].transform.position - res.position).sqrMagnitude - (HousingHpoints[num].transform.position - res.position).sqrMagnitude;
			if (num4 >= 0f)
			{
				res.position = HousingHpoints[num].pivot.position;
				res.rotation = HousingHpoints[num].pivot.rotation;
				if (hSceneManager.females[1] == null || hSceneManager.females[1].GetComponent<PlayerActor>() != null)
				{
					hSceneManager.height = ((num2 != 0) ? 7 : 0);
				}
				else
				{
					hSceneManager.height = 0;
				}
				flag2 = false;
			}
			else
			{
				res.position = hPointLists[mapID].lst[minHpoint.AreaID][minHpoint.PointID].pivot.position;
				res.rotation = hPointLists[mapID].lst[minHpoint.AreaID][minHpoint.PointID].pivot.rotation;
				if (hSceneManager.females[1] == null || hSceneManager.females[1].GetComponent<PlayerActor>() != null)
				{
					hSceneManager.height = ((minHpoint.yobaiPlace != 0) ? 7 : 0);
				}
				else
				{
					hSceneManager.height = 0;
				}
				flag2 = true;
			}
		}
		else if (array2[0])
		{
			res.position = hPointLists[mapID].lst[minHpoint.AreaID][minHpoint.PointID].pivot.position;
			res.rotation = hPointLists[mapID].lst[minHpoint.AreaID][minHpoint.PointID].pivot.rotation;
			if (hSceneManager.females[1] == null || hSceneManager.females[1].GetComponent<PlayerActor>() != null)
			{
				hSceneManager.height = ((minHpoint.yobaiPlace != 0) ? 7 : 0);
			}
			else
			{
				hSceneManager.height = 0;
			}
			flag2 = true;
		}
		else if (array2[1])
		{
			res.position = HousingHpoints[num].pivot.position;
			res.rotation = HousingHpoints[num].pivot.rotation;
			if (hSceneManager.females[1] == null || hSceneManager.females[1].GetComponent<PlayerActor>() != null)
			{
				hSceneManager.height = ((num2 != 0) ? 7 : 0);
			}
			else
			{
				hSceneManager.height = 0;
			}
			flag2 = false;
		}
		if (flag2)
		{
			int num5 = 0;
			if (gpID != -1)
			{
				foreach (KeyValuePair<int, List<HPoint>> item3 in hPointLists[mapID].lst)
				{
					if (value.ContainsKey(gpID) && value[gpID].IDs.Contains(item3.Key) && item3.Key < minHpoint.AreaID)
					{
						num5 += item3.Value.Count;
					}
				}
			}
			ctrlFlag.HPointID = num5 + minHpoint.PointID + 1;
			ctrlFlag.nowHPoint = hPointLists[mapID].lst[minHpoint.AreaID][minHpoint.PointID];
		}
		else
		{
			if (!list2.Contains(item: false))
			{
				ctrlFlag.HPointID = num + 1;
			}
			else if (gpID == -1)
			{
				ctrlFlag.HPointID = num + 1 + hPointLists[mapID].lst[areaID].Count;
			}
			else
			{
				int num6 = 0;
				for (int k = 0; k < value[gpID].IDs.Count; k++)
				{
					if (!list2[k])
					{
						num6 += hPointLists[mapID].lst[value[gpID].IDs[k]].Count;
					}
				}
				ctrlFlag.HPointID = num + 1 + num6;
			}
			ctrlFlag.nowHPoint = housingHpoints[num];
		}
		ChangeHItem componentInChildren = ctrlFlag.nowHPoint.GetComponentInChildren<ChangeHItem>();
		if (componentInChildren != null)
		{
			componentInChildren.ChangeActive(val: false);
		}
		return true;
	}

	public void HousingHStart(HPoint hPoint)
	{
		int mapID = Singleton<Map>.Instance.MapID;
		int areaID = hSceneManager.Player.AreaID;
		int num = -1;
		gpID = -1;
		if (!Areagp.TryGetValue(mapID, out var value))
		{
			return;
		}
		foreach (KeyValuePair<int, AreaGroupDefine> item in value)
		{
			if (!item.Value.IDs.Contains(areaID))
			{
				continue;
			}
			gpID = item.Key;
			break;
		}
		List<bool> list = new List<bool>();
		if (gpID == -1)
		{
			list.Add(hPointLists.Count == 0 || !hPointLists.ContainsKey(mapID) || !hPointLists[mapID].lst.ContainsKey(areaID) || hPointLists[mapID].lst[areaID].Count == 0);
		}
		else
		{
			for (int i = 0; i < value[gpID].IDs.Count; i++)
			{
				int key = value[gpID].IDs[i];
				list.Add(hPointLists.Count == 0 || !hPointLists.ContainsKey(mapID) || !hPointLists[mapID].lst.ContainsKey(key) || hPointLists[mapID].lst[key].Count == 0);
			}
		}
		num = PlayerClosePointID(hPoint, housingHpoints);
		if (gpID == -1)
		{
			ctrlFlag.HPointID = num + 1 + hPointLists[mapID].lst[areaID].Count;
		}
		else
		{
			int num2 = 0;
			for (int j = 0; j < value[gpID].IDs.Count; j++)
			{
				if (!list[j])
				{
					num2 += hPointLists[mapID].lst[value[gpID].IDs[j]].Count;
				}
			}
			ctrlFlag.HPointID = num + 1 + num2;
		}
		ctrlFlag.nowHPoint = housingHpoints[num];
		if (ctrlFlag.HPointID < lstMarker.Count)
		{
			ChangeHItem componentInChildren = lstMarker[ctrlFlag.HPointID].Item2.GetComponentInChildren<ChangeHItem>();
			if (componentInChildren != null)
			{
				componentInChildren.ChangeActive(val: false);
			}
		}
	}

	private int PlayerClosePointID(Transform nowPos, List<HPoint> hPoints, int nPlace = -2)
	{
		int result = -1;
		float num = hSceneSprite.HpointSearchRange * hSceneSprite.HpointSearchRange;
		float num2 = float.MaxValue;
		float y = nowPos.position.y;
		int num3 = 0;
		if (hSceneManager.EventKind != HSceneManager.HEvent.GyakuYobai && hSceneManager.EventKind != HSceneManager.HEvent.Yobai && hSceneManager.EventKind != HSceneManager.HEvent.FromFemale && hSceneManager.Agent[1] == null)
		{
			num3 = 1;
		}
		for (int i = 0; i < hPoints.Count; i++)
		{
			int num4 = i;
			switch (nPlace)
			{
			case -1:
				if (!DicTupleContainsValue(hPoints[num4]._nPlace, 0, 0) && !DicTupleContainsValue(hPoints[num4]._nPlace, 1, 0))
				{
					continue;
				}
				break;
			default:
				if (!DicTupleContainsValue(hPoints[num4]._nPlace, nPlace, 0))
				{
					continue;
				}
				break;
			case -2:
				break;
			}
			if (!CheckEvent(hPoints[num4]._nPlace, hSceneManager.EventKind) || ((hSceneManager.EventKind == HSceneManager.HEvent.Yobai || hSceneManager.EventKind == HSceneManager.HEvent.GyakuYobai) && hPoints[num4].id != 1 && hPoints[num4].id != 17))
			{
				continue;
			}
			if (hSceneManager.EventKind != HSceneManager.HEvent.TsukueBare)
			{
				if (hPoints[num4].id == kadOnaID)
				{
					continue;
				}
			}
			else if (hPoints[num4].id != kadOnaID)
			{
				continue;
			}
			if (!CanInfo(hPoints[num4]._nPlace))
			{
				continue;
			}
			switch (num3)
			{
			case 0:
			{
				if (hPoints[num4].transform.position.y < y - OffsetDownHeight || hPoints[num4].transform.position.y > y + OffsetUpHeight)
				{
					continue;
				}
				Vector2 vector = default(Vector2);
				vector.x = hPoints[num4].transform.position.x - nowPos.position.x;
				vector.y = hPoints[num4].transform.position.z - nowPos.position.z;
				num2 = vector.sqrMagnitude;
				break;
			}
			case 1:
				num2 = (hPoints[num4].transform.position - nowPos.position).sqrMagnitude;
				break;
			}
			if (!(num <= num2))
			{
				num = num2;
				result = num4;
			}
		}
		return result;
	}

	private int PlayerClosePointID(HPoint now, HPoint[] hPoints, int nPlace = -2)
	{
		int result = -1;
		for (int i = 0; i < hPoints.Length; i++)
		{
			if (!(now != hPoints[i]))
			{
				result = i;
				break;
			}
		}
		return result;
	}

	private int PlayerClosePointID(Transform nowPos, HPoint[] hPoints, int nPlace = -2)
	{
		int result = -1;
		float num = hSceneSprite.HpointSearchRange * hSceneSprite.HpointSearchRange;
		float num2 = float.MaxValue;
		float y = nowPos.position.y;
		int num3 = 0;
		if (hSceneManager.EventKind != HSceneManager.HEvent.GyakuYobai && hSceneManager.EventKind != HSceneManager.HEvent.Yobai && hSceneManager.EventKind != HSceneManager.HEvent.FromFemale && hSceneManager.Agent[1] == null)
		{
			num3 = 1;
		}
		for (int i = 0; i < hPoints.Length; i++)
		{
			int num4 = i;
			switch (nPlace)
			{
			case -1:
				if (!DicTupleContainsValue(hPoints[num4]._nPlace, 0, 0) && !DicTupleContainsValue(hPoints[num4]._nPlace, 1, 0))
				{
					continue;
				}
				break;
			default:
				if (!DicTupleContainsValue(hPoints[num4]._nPlace, nPlace, 0))
				{
					continue;
				}
				break;
			case -2:
				break;
			}
			if (!CheckEvent(hPoints[num4]._nPlace, hSceneManager.EventKind) || !CanInfo(hPoints[num4]._nPlace))
			{
				continue;
			}
			if (hSceneManager.EventKind != HSceneManager.HEvent.TsukueBare)
			{
				if (hPoints[num4].id == kadOnaID)
				{
					continue;
				}
			}
			else if (hPoints[num4].id != kadOnaID)
			{
				continue;
			}
			switch (num3)
			{
			case 0:
			{
				if (hPoints[num4].transform.position.y < y - OffsetDownHeight || hPoints[num4].transform.position.y > y + OffsetUpHeight)
				{
					continue;
				}
				Vector2 vector = default(Vector2);
				vector.x = hPoints[num4].transform.position.x - nowPos.position.x;
				vector.y = hPoints[num4].transform.position.z - nowPos.position.z;
				num2 = vector.sqrMagnitude;
				break;
			}
			case 1:
				num2 = (hPoints[num4].transform.position - nowPos.position).sqrMagnitude;
				break;
			}
			if (!(num <= num2))
			{
				num = num2;
				result = num4;
			}
		}
		return result;
	}

	private bool CheckEvent(Dictionary<int, UnityEx.ValueTuple<int, int>> dic, HSceneManager.HEvent Event)
	{
		switch (Event)
		{
		case HSceneManager.HEvent.Bath:
			return DicTupleContainsValue(dic, 11, 0);
		case HSceneManager.HEvent.Toilet1:
			return DicTupleContainsValue(dic, 13, 0);
		case HSceneManager.HEvent.Kitchen:
			return DicTupleContainsValue(dic, 9, 0);
		case HSceneManager.HEvent.Tachi:
		case HSceneManager.HEvent.MapBath:
			return DicTupleContainsValue(dic, 1, 0);
		case HSceneManager.HEvent.Stairs:
		case HSceneManager.HEvent.StairsBare:
			return DicTupleContainsValue(dic, 10, 0);
		case HSceneManager.HEvent.KabeanaBack:
		case HSceneManager.HEvent.KabeanaFront:
			return DicTupleContainsValue(dic, 15, 0);
		case HSceneManager.HEvent.Neonani:
			return DicTupleContainsValue(dic, 0, 0);
		case HSceneManager.HEvent.TsukueBare:
			return DicTupleContainsValue(dic, 4, 0);
		default:
			return true;
		}
	}

	public bool CheckPlace(Dictionary<int, UnityEx.ValueTuple<int, int>> place, int mode)
	{
		int[] array = null;
		switch (mode)
		{
		case 0:
			array = LesPlaceID;
			break;
		case 1:
			array = multiFemalePlaceID;
			break;
		}
		if (array == null)
		{
			return false;
		}
		for (int i = 0; i < array.Length; i++)
		{
			foreach (UnityEx.ValueTuple<int, int> value in place.Values)
			{
				if (value.Item1 == array[i])
				{
					return true;
				}
			}
		}
		return false;
	}

	private bool CanInfo(Dictionary<int, UnityEx.ValueTuple<int, int>> place)
	{
		foreach (UnityEx.ValueTuple<int, int> value in place.Values)
		{
			if (CheckMotionLimit(value.Item1))
			{
				return true;
			}
		}
		return false;
	}

	private bool CheckMotionLimit(int place)
	{
		if (hSceneManager.bMerchant && hSceneManager.MerchantLimit == 1)
		{
			if (playerSex == 0)
			{
				for (int i = 0; i < lstAnimInfo[1].Count; i++)
				{
					if (CheckMotionLimit(place, lstAnimInfo[1][i]))
					{
						return true;
					}
				}
			}
			else if (playerSex == 1)
			{
				for (int j = 0; j < lstAnimInfo[4].Count; j++)
				{
					if (CheckMotionLimit(place, lstAnimInfo[4][j]))
					{
						return true;
					}
				}
			}
		}
		else
		{
			for (int k = 0; k < lstAnimInfo.Length; k++)
			{
				for (int l = 0; l < lstAnimInfo[k].Count; l++)
				{
					if (CheckMotionLimit(place, lstAnimInfo[k][l]))
					{
						return true;
					}
				}
			}
		}
		return false;
	}

	private bool CheckMotionLimit(int place, HScene.AnimationListInfo lstAnimInfo)
	{
		if (playerSex == 0 || (playerSex == 1 && ctrlFlag.bFutanari))
		{
			if (hSceneManager.females[1] == null)
			{
				if (lstAnimInfo.nPromiscuity == 1)
				{
					return false;
				}
			}
			else if (lstAnimInfo.nPromiscuity != 1)
			{
				return false;
			}
			if (lstAnimInfo.nPromiscuity == 2)
			{
				return false;
			}
			if (lstAnimInfo.ActionCtrl.Item1 == 3 && !hSceneSprite.NonTokushuCheckIDs.Contains(lstAnimInfo.id) && lstAnimInfo.fileMale == string.Empty)
			{
				return false;
			}
		}
		else if (playerSex == 1 && lstAnimInfo.nPromiscuity < 2)
		{
			return false;
		}
		if (!hSceneManager.bMerchant)
		{
			if (lstAnimInfo.nHentai == 1 && hSceneManager.GetFlaverSkillLevel(2) < 100)
			{
				return false;
			}
			if (lstAnimInfo.nHentai == 2 && hSceneManager.GetFlaverSkillLevel(2) < 170)
			{
				return false;
			}
		}
		if (hSceneManager.bMerchant)
		{
			if (!lstAnimInfo.bMerchantMotion)
			{
				return false;
			}
			if (lstAnimInfo.nIyaAction == 2)
			{
				return false;
			}
		}
		else if (hSceneManager.EventKind != HSceneManager.HEvent.Yobai)
		{
			if (hSceneManager.isForce)
			{
				if (lstAnimInfo.nIyaAction < 1)
				{
					return false;
				}
			}
			else if (lstAnimInfo.nIyaAction == 2)
			{
				return false;
			}
		}
		else if (!lstAnimInfo.bSleep)
		{
			return false;
		}
		if (!hSceneManager.bMerchant)
		{
			if (!lstAnimInfo.nPositons.Contains(place))
			{
				return false;
			}
		}
		else if (place >= NotMerchantPlaceID)
		{
			return false;
		}
		if (lstAnimInfo.isNeedItem && !hSceneManager.CheckHadItem(lstAnimInfo.ActionCtrl.Item1, lstAnimInfo.id))
		{
			return false;
		}
		if (lstAnimInfo.nDownPtn == 0 && ctrlFlag.isFaintness)
		{
			return false;
		}
		if (lstAnimInfo.nFaintnessLimit == 1 && !ctrlFlag.isFaintness)
		{
			return false;
		}
		switch (ctrlFlag.initiative)
		{
		case 0:
			if (lstAnimInfo.nInitiativeFemale != 0)
			{
				return false;
			}
			break;
		case 1:
			if (lstAnimInfo.nInitiativeFemale == 0)
			{
				return false;
			}
			break;
		case 2:
			if (lstAnimInfo.nInitiativeFemale != 2)
			{
				return false;
			}
			break;
		}
		return true;
	}

	private int YobaiMin(Vector3 nowPos, List<HPoint> points, int[] check)
	{
		float sqrMagnitude = (points[check[0]].transform.position - nowPos).sqrMagnitude;
		float sqrMagnitude2 = (points[check[1]].transform.position - nowPos).sqrMagnitude;
		return (!(sqrMagnitude < sqrMagnitude2)) ? 1 : 0;
	}

	private int YobaiMin(Vector3 nowPos, HPoint[] points, int[] check)
	{
		float sqrMagnitude = (points[check[0]].transform.position - nowPos).sqrMagnitude;
		float sqrMagnitude2 = (points[check[1]].transform.position - nowPos).sqrMagnitude;
		return (!(sqrMagnitude < sqrMagnitude2)) ? 1 : 0;
	}

	public void endHScene()
	{
		for (int i = 0; i < lstMarker.Count; i++)
		{
			lstMarker[i].Item2.SetEffectActive(set: false);
			Collider collider = lstMarker[i].Item2.GetCollider();
			if (collider != null)
			{
				collider.enabled = false;
			}
		}
		IsMarker = false;
		lstMarker.Clear();
		hPointLists = null;
		InitUsePoint = false;
	}

	private bool HitObstacle(Vector3 pos, HPoint point)
	{
		pos.y += 20f;
		Vector3 position = point.transform.position;
		Ray ray = new Ray(pos, (position - pos).normalized);
		int layerMask = 2048;
		int num = Physics.RaycastNonAlloc(ray, HitInfo2, hSceneSprite.HpointSearchRange, layerMask);
		if (num > 0)
		{
			for (int i = 0; i < num; i++)
			{
				if (HitInfo2[i].distance > (position - pos).magnitude)
				{
					continue;
				}
				foreach (ForcedHideObject checkHitObj in CheckHitObjs)
				{
					Collider[] componentsInChildren = checkHitObj.GetComponentsInChildren<Collider>();
					Collider[] array = componentsInChildren;
					foreach (Collider collider in array)
					{
						if (HitInfo2[i].collider == collider)
						{
							return true;
						}
					}
				}
			}
		}
		return false;
	}

	private bool HpointAreaOpen(HPoint point)
	{
		if (point.OpenID == null || point.OpenID.Count == 0)
		{
			return true;
		}
		bool flag = true;
		for (int i = 0; i < point.OpenID.Count; i++)
		{
			int key = point.OpenID[i];
			flag &= Singleton<Game>.Instance.Environment.AreaOpenState[key];
		}
		return flag;
	}

	public void EndProc()
	{
		housingHpoints = null;
		ExistSecondfemale = false;
	}

	private void AreagpInit()
	{
		Dictionary<int, Dictionary<int, List<int>>> vanishHousingAreaGroup = Singleton<Manager.Resources>.Instance.Map.VanishHousingAreaGroup;
		if (vanishHousingAreaGroup == null)
		{
			return;
		}
		foreach (KeyValuePair<int, Dictionary<int, List<int>>> item in vanishHousingAreaGroup)
		{
			if (!Areagp.ContainsKey(item.Key))
			{
				Areagp.Add(item.Key, new Dictionary<int, AreaGroupDefine>());
			}
			foreach (KeyValuePair<int, List<int>> item2 in item.Value)
			{
				if (!Areagp[item.Key].ContainsKey(item2.Key))
				{
					Areagp[item.Key].Add(item2.Key, new AreaGroupDefine());
				}
				for (int i = 0; i < item2.Value.Count; i++)
				{
					if (!Areagp[item.Key][item2.Key].IDs.Contains(item2.Value[i]))
					{
						Areagp[item.Key][item2.Key].IDs.Add(item2.Value[i]);
					}
				}
			}
		}
	}
}
