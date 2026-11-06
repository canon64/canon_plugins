using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AIProject;
using AIProject.Animal;
using Housing.Extension;
using Manager;
using UniRx;
using UnityEngine;

namespace Housing;

public class OCItem : ObjectCtrl
{
	private ItemComponent m_itemComponent;

	private Subject<bool> subjectColor1 = new Subject<bool>();

	private Subject<bool> subjectColor2 = new Subject<bool>();

	private Subject<bool> subjectColor3 = new Subject<bool>();

	private Subject<bool> subjectEmissionColor = new Subject<bool>();

	private static Vector3 correctionSize = new Vector3(0.2f, 0.2f, 0.2f);

	public OIItem OIItem
	{
		[CompilerGenerated]
		get
		{
			return base.ObjectInfo as OIItem;
		}
	}

	public Manager.Housing.LoadInfo LoadInfo { get; private set; }

	public ItemComponent ItemComponent
	{
		[CompilerGenerated]
		get
		{
			return m_itemComponent ?? (m_itemComponent = base.GameObject?.GetComponent<ItemComponent>());
		}
	}

	public HashSet<OCItem> HashOverlap { get; private set; } = new HashSet<OCItem>();


	public HashSet<OCItem> CheckedOverlap { get; private set; } = new HashSet<OCItem>();


	public override string Name
	{
		[CompilerGenerated]
		get
		{
			return LoadInfo.name;
		}
	}

	public int Category
	{
		[CompilerGenerated]
		get
		{
			return LoadInfo.category;
		}
	}

	public ActionPoint[] ActionPoints
	{
		[CompilerGenerated]
		get
		{
			return ItemComponent?.actionPoints;
		}
	}

	public FarmPoint[] FarmPoints
	{
		[CompilerGenerated]
		get
		{
			return ItemComponent?.farmPoints;
		}
	}

	public HPoint[] HPoints
	{
		[CompilerGenerated]
		get
		{
			return ItemComponent?.hPoints;
		}
	}

	public ItemComponent.ColInfo[] ColInfos
	{
		[CompilerGenerated]
		get
		{
			return ItemComponent?.colInfos;
		}
	}

	public PetHomePoint[] PetHomePoints
	{
		[CompilerGenerated]
		get
		{
			return ItemComponent?.petHomePoints;
		}
	}

	public JukePoint[] JukePoints
	{
		[CompilerGenerated]
		get
		{
			return ItemComponent?.jukePoints;
		}
	}

	public CraftPoint[] CraftPoints
	{
		[CompilerGenerated]
		get
		{
			return ItemComponent?.craftPoints;
		}
	}

	public LightSwitchPoint[] LightSwitchPoints
	{
		[CompilerGenerated]
		get
		{
			return ItemComponent?.lightSwitchPoints;
		}
	}

	public bool IsColor
	{
		[CompilerGenerated]
		get
		{
			return (bool)ItemComponent && ItemComponent.IsColor;
		}
	}

	public bool IsColor1
	{
		[CompilerGenerated]
		get
		{
			return (bool)ItemComponent && ItemComponent.IsColor1;
		}
	}

	public bool IsColor2
	{
		[CompilerGenerated]
		get
		{
			return (bool)ItemComponent && ItemComponent.IsColor2;
		}
	}

	public bool IsColor3
	{
		[CompilerGenerated]
		get
		{
			return (bool)ItemComponent && ItemComponent.IsColor3;
		}
	}

	public bool IsEmissionColor
	{
		[CompilerGenerated]
		get
		{
			return (bool)ItemComponent && ItemComponent.IsEmissionColor;
		}
	}

	public Color Color1
	{
		get
		{
			return OIItem.Color1;
		}
		set
		{
			OIItem.Color1 = value;
			subjectColor1.OnNext(value: true);
		}
	}

	public Color Color2
	{
		get
		{
			return OIItem.Color2;
		}
		set
		{
			OIItem.Color2 = value;
			subjectColor2.OnNext(value: true);
		}
	}

	public Color Color3
	{
		get
		{
			return OIItem.Color1;
		}
		set
		{
			OIItem.Color3 = value;
			subjectColor3.OnNext(value: true);
		}
	}

	public Color EmissionColor
	{
		get
		{
			return OIItem.EmissionColor;
		}
		set
		{
			OIItem.EmissionColor = value;
			subjectEmissionColor.OnNext(value: true);
		}
	}

	public bool IsOption => LoadInfo.useOption & ((bool)ItemComponent && ItemComponent.IsOption);

	public bool VisibleOption
	{
		get
		{
			return OIItem.VisibleOption;
		}
		set
		{
			OIItem.VisibleOption = value;
			ItemComponent?.SetVisibleOption(value);
		}
	}

	public override bool IsOverlapNow
	{
		[CompilerGenerated]
		get
		{
			return HashOverlap.Count() != 0;
		}
	}

	public IEnumerable<Collider> OverlapColliders => (!ItemComponent) ? null : ((!ItemComponent.overlapColliders.IsNullOrEmpty()) ? ItemComponent.overlapColliders.Where((Collider v) => v != null) : null);

	public OCItem(OIItem _oiItem, GameObject _gameObject, CraftInfo _craftInfo, Manager.Housing.LoadInfo _loadInfo)
		: base(_oiItem, _gameObject, _craftInfo)
	{
		LoadInfo = _loadInfo;
		if (base.GameObject != null)
		{
			m_itemComponent = base.GameObject.GetComponent<ItemComponent>();
		}
		else
		{
			m_itemComponent = null;
		}
		if (m_itemComponent == null)
		{
			m_itemComponent = base.GameObject.AddComponent<ItemComponent>();
			m_itemComponent.Setup();
		}
		if ((object)m_itemComponent != null)
		{
			m_itemComponent.SetHPoint();
		}
		Observable.Merge(new IObservable<bool>[4] { subjectColor1, subjectColor2, subjectColor3, subjectEmissionColor }).BatchFrame(0, FrameCountType.Update).Subscribe(delegate
		{
			UpdateColor();
		});
	}

	public override bool OnRemoving()
	{
		bool flag = true;
		if (ItemComponent != null)
		{
			if (!ItemComponent.petHomePoints.IsNullOrEmpty())
			{
				PetHomePoint[] petHomePoints = ItemComponent.petHomePoints;
				foreach (PetHomePoint petHomePoint in petHomePoints)
				{
					flag &= petHomePoint.CanDelete();
				}
			}
			if (!ItemComponent.craftPoints.IsNullOrEmpty())
			{
				CraftPoint[] craftPoints = ItemComponent.craftPoints;
				foreach (CraftPoint craftPoint in craftPoints)
				{
					flag &= craftPoint.CanDelete();
				}
			}
		}
		return flag;
	}

	public override void OnDelete()
	{
		foreach (OCItem item in HashOverlap)
		{
			item.HashOverlap.Remove(this);
		}
		HashOverlap.Clear();
		base.OnDelete();
	}

	public override void OnDeleteChild()
	{
		foreach (OCItem item in HashOverlap)
		{
			item.HashOverlap.Remove(this);
		}
		HashOverlap.Clear();
	}

	public override void OnDestroy()
	{
		foreach (ActionPoint item in ActionPoints.Where((ActionPoint v) => v != null))
		{
			Singleton<Map>.Instance.RemoveAppendActionPoint(item);
			Singleton<Map>.Instance.RemoveAppendCommandable(item);
			Singleton<Map>.Instance.RemoveAgentSearchActionPoint(item);
			Singleton<Map>.Instance.UnregisterRuntimePoint(item, removeEntry: false);
		}
		foreach (FarmPoint item2 in FarmPoints.Where((FarmPoint v) => v != null))
		{
			Singleton<Map>.Instance.RemoveRuntimeFarmPoint(item2);
			Singleton<Map>.Instance.RemoveAppendCommandable(item2);
			Singleton<Map>.Instance.UnregisterRuntimePoint(item2, removeEntry: false);
		}
		foreach (PetHomePoint item3 in PetHomePoints.Where((PetHomePoint v) => v != null))
		{
			Singleton<Map>.Instance.RemovePetHomePoint(item3);
			Singleton<Map>.Instance.RemoveAppendCommandable(item3);
			Singleton<Map>.Instance.UnregisterRuntimePoint(item3, removeEntry: false);
		}
		foreach (JukePoint item4 in JukePoints.Where((JukePoint v) => v != null))
		{
			Singleton<Map>.Instance.RemoveJukePoint(item4);
			Singleton<Map>.Instance.RemoveAppendCommandable(item4);
			Singleton<Map>.Instance.UnregisterRuntimePoint(item4, removeEntry: false);
		}
		foreach (CraftPoint item5 in CraftPoints.Where((CraftPoint v) => v != null))
		{
			Singleton<Map>.Instance.RemoveCraftPoint(item5);
			Singleton<Map>.Instance.RemoveAppendCommandable(item5);
			Singleton<Map>.Instance.UnregisterRuntimePoint(item5, removeEntry: false);
		}
		foreach (LightSwitchPoint item6 in LightSwitchPoints.Where((LightSwitchPoint v) => v != null))
		{
			Singleton<Map>.Instance.RemoveRuntimeLightSwitchPoint(item6);
			Singleton<Map>.Instance.RemoveAppendCommandable(item6);
			Singleton<Map>.Instance.UnregisterRuntimePoint(item6, removeEntry: false);
		}
		m_itemComponent = null;
		base.OnDestroy();
	}

	public override void RestoreObject(GameObject _gameObject)
	{
		base.RestoreObject(_gameObject);
		m_itemComponent = base.GameObject.GetComponent<ItemComponent>();
		if (m_itemComponent == null)
		{
			m_itemComponent = base.GameObject.AddComponent<ItemComponent>();
			m_itemComponent.Setup();
		}
		if ((object)m_itemComponent != null)
		{
			m_itemComponent.SetHPoint();
		}
	}

	public override void GetLocalMinMax(Vector3 _pos, Quaternion _rot, Transform _root, ref Vector3 _min, ref Vector3 _max)
	{
		Matrix4x4 matrix4x = Matrix4x4.TRS(_root.position, _root.rotation, Vector3.one);
		Matrix4x4 matrix4x2 = Matrix4x4.TRS(_pos, _rot, Vector3.one);
		matrix4x2 = matrix4x.inverse * matrix4x2;
		Vector3 min = ItemComponent.min;
		Vector3 max = ItemComponent.max;
		Vector3[] source = new Vector3[8]
		{
			matrix4x2.MultiplyPoint3x4(new Vector3(min.x, min.y, min.z)),
			matrix4x2.MultiplyPoint3x4(new Vector3(min.x, min.y, max.z)),
			matrix4x2.MultiplyPoint3x4(new Vector3(max.x, min.y, min.z)),
			matrix4x2.MultiplyPoint3x4(new Vector3(max.x, min.y, max.z)),
			matrix4x2.MultiplyPoint3x4(new Vector3(min.x, max.y, min.z)),
			matrix4x2.MultiplyPoint3x4(new Vector3(min.x, max.y, max.z)),
			matrix4x2.MultiplyPoint3x4(new Vector3(max.x, max.y, min.z)),
			matrix4x2.MultiplyPoint3x4(new Vector3(max.x, max.y, max.z))
		};
		_min = new Vector3(source.Min((Vector3 _v) => _v.x), source.Min((Vector3 _v) => _v.y), source.Min((Vector3 _v) => _v.z));
		_max = new Vector3(source.Max((Vector3 _v) => _v.x), source.Max((Vector3 _v) => _v.y), source.Max((Vector3 _v) => _v.z));
	}

	public override void GetActionPoint(ref List<ActionPoint> _points)
	{
		ActionPoint[] actionPoints = ActionPoints;
		if (!actionPoints.IsNullOrEmpty())
		{
			_points.AddRange(actionPoints.Where((ActionPoint v) => v != null));
		}
	}

	public override void GetFarmPoint(ref List<FarmPoint> _points)
	{
		FarmPoint[] farmPoints = FarmPoints;
		if (!farmPoints.IsNullOrEmpty())
		{
			_points.AddRange(farmPoints.Where((FarmPoint v) => v != null));
		}
	}

	public override void GetHPoint(ref List<HPoint> _points)
	{
		HPoint[] hPoints = HPoints;
		if (!hPoints.IsNullOrEmpty())
		{
			_points.AddRange(hPoints.Where((HPoint v) => v != null));
		}
	}

	public override void GetColInfo(ref List<ItemComponent.ColInfo> _infos)
	{
		ItemComponent.ColInfo[] colInfos = ColInfos;
		if (!colInfos.IsNullOrEmpty())
		{
			_infos.AddRange(colInfos.Where((ItemComponent.ColInfo v) => v != null));
		}
	}

	public override void GetPetHomePoint(ref List<PetHomePoint> _points)
	{
		PetHomePoint[] petHomePoints = PetHomePoints;
		if (!petHomePoints.IsNullOrEmpty())
		{
			_points.AddRange(petHomePoints.Where((PetHomePoint v) => v != null));
		}
	}

	public override void GetJukePoint(ref List<JukePoint> _points)
	{
		JukePoint[] jukePoints = JukePoints;
		if (!jukePoints.IsNullOrEmpty())
		{
			_points.AddRange(jukePoints.Where((JukePoint v) => v != null));
		}
	}

	public override void GetCraftPoint(ref List<CraftPoint> _points)
	{
		CraftPoint[] craftPoints = CraftPoints;
		if (!craftPoints.IsNullOrEmpty())
		{
			_points.AddRange(craftPoints.Where((CraftPoint v) => v != null));
		}
	}

	public override void GetLightSwitchPoint(ref List<LightSwitchPoint> _points)
	{
		LightSwitchPoint[] lightSwitchPoints = LightSwitchPoints;
		if (!lightSwitchPoints.IsNullOrEmpty())
		{
			_points.AddRange(lightSwitchPoints.Where((LightSwitchPoint v) => v != null));
		}
	}

	public override void GetUsedNum(int _no, ref int _num)
	{
		if (OIItem.ID == _no)
		{
			_num++;
		}
	}

	public void ResetColor()
	{
		if (IsColor1)
		{
			Color1 = ItemComponent.defColor1;
		}
		if (IsColor2)
		{
			Color2 = ItemComponent.defColor2;
		}
		if (IsColor3)
		{
			Color3 = ItemComponent.defColor3;
		}
		if (IsEmissionColor)
		{
			EmissionColor = ItemComponent.defEmissionColor;
		}
	}

	public void UpdateColor()
	{
		if (ItemComponent == null || !ItemComponent.IsColor)
		{
			return;
		}
		OIItem info = OIItem;
		ItemComponent.Info[] renderers = ItemComponent.renderers;
		foreach (ItemComponent.Info info2 in renderers)
		{
			Material[] materials = info2.meshRenderer.materials;
			for (int k = 0; k < materials.Length; k++)
			{
				Material i = materials[k];
				if (i == null)
				{
					continue;
				}
				info2.materialInfos.SafeProc(k, delegate(ItemComponent.MaterialInfo _info)
				{
					if (_info.isColor1)
					{
						i.SetColor(ItemShader._Color, info.Color1);
					}
					if (_info.isColor2)
					{
						i.SetColor(ItemShader._Color2, info.Color2);
					}
					if (_info.isColor3)
					{
						i.SetColor(ItemShader._Color3, info.Color3);
					}
					if (_info.isEmission)
					{
						i.SetColor(ItemShader._EmissionColor, info.EmissionColor);
					}
				});
			}
			info2.meshRenderer.materials = materials;
		}
	}

	public override bool CheckOverlap(ObjectCtrl _oc, bool _load = false)
	{
		if (ItemComponent == null)
		{
			return false;
		}
		if (!(_oc is OCItem oCItem) || this == oCItem)
		{
			return false;
		}
		if (_load && CheckedOverlap.Contains(oCItem))
		{
			return HashOverlap.Contains(oCItem);
		}
		if (!oCItem.ItemComponent.overlap || !ItemComponent.overlap)
		{
			if (_load)
			{
				CheckedOverlap.Add(oCItem);
				oCItem.CheckedOverlap.Add(this);
			}
			return false;
		}
		bool flag = false;
		IEnumerable<Collider> overlapColliders = oCItem.OverlapColliders;
		if (!overlapColliders.IsNullOrEmpty())
		{
			Vector3 direction = Vector3.zero;
			float distance = 0f;
			IEnumerable<Collider> overlapColliders2 = OverlapColliders;
			if (overlapColliders2.IsNullOrEmpty())
			{
				Bounds bounds = default(Bounds);
				bounds.SetMinMax(ItemComponent.min, ItemComponent.max);
				BoxCollider boxCollider = Singleton<CraftScene>.Instance.TestColliders.SafeGet(0);
				boxCollider.center = bounds.center;
				boxCollider.size = bounds.size - correctionSize;
				foreach (Collider item in overlapColliders)
				{
					flag |= Physics.ComputePenetration(boxCollider, Position, Rotation, item, item.transform.position, item.transform.rotation, out direction, out distance);
				}
			}
			else
			{
				foreach (Collider item2 in overlapColliders)
				{
					foreach (Collider item3 in overlapColliders2)
					{
						flag |= Physics.ComputePenetration(item2, item2.transform.position, item2.transform.rotation, item3, item3.transform.position, item3.transform.rotation, out direction, out distance);
					}
				}
			}
		}
		else
		{
			flag = CheckOverlapSize(oCItem);
		}
		if (flag)
		{
			HashOverlap.Add(oCItem);
			oCItem.HashOverlap.Add(this);
		}
		else
		{
			HashOverlap.Remove(oCItem);
			oCItem.HashOverlap.Remove(this);
		}
		if (_load)
		{
			CheckedOverlap.Add(oCItem);
			oCItem.CheckedOverlap.Add(this);
		}
		return flag;
	}

	private bool CheckOverlapSize(OCItem _oc)
	{
		bool flag = false;
		IEnumerable<Collider> overlapColliders = OverlapColliders;
		if (overlapColliders.IsNullOrEmpty())
		{
			Vector3 _min = Vector3.zero;
			Vector3 _max = Vector3.zero;
			_oc.GetLocalMinMax(_oc.Position, _oc.Rotation, base.CraftInfo.ObjRoot.transform, ref _min, ref _max);
			Bounds bounds = default(Bounds);
			bounds.SetMinMax(_min, _max);
			bounds.size -= correctionSize;
			Vector3 _min2 = Vector3.zero;
			Vector3 _max2 = Vector3.zero;
			GetLocalMinMax(Position, Rotation, base.CraftInfo.ObjRoot.transform, ref _min2, ref _max2);
			Bounds bounds2 = default(Bounds);
			bounds2.SetMinMax(_min2, _max2);
			bounds2.size -= correctionSize;
			return bounds.Intersects(bounds2);
		}
		Bounds bounds3 = default(Bounds);
		bounds3.SetMinMax(_oc.ItemComponent.min, _oc.ItemComponent.max);
		BoxCollider boxCollider = Singleton<CraftScene>.Instance.TestColliders.SafeGet(0);
		boxCollider.center = bounds3.center;
		boxCollider.size = bounds3.size - correctionSize;
		Vector3 direction = Vector3.zero;
		float distance = 0f;
		foreach (Collider item in overlapColliders)
		{
			flag |= Physics.ComputePenetration(boxCollider, _oc.Position, _oc.Rotation, item, item.transform.position, item.transform.rotation, out direction, out distance);
		}
		return flag;
	}

	public override void BeforeCheckOverlap()
	{
		CheckedOverlap.Clear();
		SetOverlapColliders(_flag: true);
	}

	public override void AfterCheckOverlap()
	{
		CheckedOverlap.Clear();
	}

	public override void SetOverlapColliders(bool _flag)
	{
		ItemComponent?.SetOverlapColliders(_flag);
	}

	public override void GetOverlapObject(ref List<ObjectCtrl> _lst)
	{
		if (IsOverlapNow)
		{
			_lst.Add(this);
		}
	}
}
