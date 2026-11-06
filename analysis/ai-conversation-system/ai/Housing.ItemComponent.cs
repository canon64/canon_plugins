using System;
using System.Linq;
using System.Runtime.CompilerServices;
using AIProject;
using AIProject.Animal;
using Illusion.Extensions;
using Manager;
using UnityEngine;

namespace Housing;

public class ItemComponent : MonoBehaviour
{
	[Serializable]
	public class MaterialInfo
	{
		public bool isColor1;

		public bool isColor2;

		public bool isColor3;

		public bool isEmission;
	}

	[Serializable]
	public class ColInfo
	{
		public Collider[] colliders;

		public Renderer[] renderers;

		private bool visible = true;

		public bool Visible
		{
			set
			{
				if (renderers.IsNullOrEmpty())
				{
					return;
				}
				visible = value;
				foreach (Renderer item in renderers.Where((Renderer v) => v != null))
				{
					item.enabled = value;
				}
			}
		}

		public void CheckCollision(Collider _collider)
		{
			Vector3 position = _collider.transform.position;
			Quaternion rotation = _collider.transform.rotation;
			bool flag = false;
			foreach (Collider item in colliders.Where((Collider v) => v != null))
			{
				flag |= Physics.ComputePenetration(_collider, position, rotation, item, item.transform.position, item.transform.rotation, out var _, out var _);
				MeshCollider meshCollider = item as MeshCollider;
				if (meshCollider != null)
				{
					CapsuleCollider capsuleCollider = _collider as CapsuleCollider;
					flag |= meshCollider.Raycast(new Ray(position, _collider.transform.TransformDirection(Vector3.forward)), out var _, Mathf.Abs(capsuleCollider.height));
				}
			}
			if (visible != !flag)
			{
				Visible = !flag;
			}
		}
	}

	[Serializable]
	public class Info
	{
		public MeshRenderer meshRenderer;

		public MaterialInfo[] materialInfos;

		public Info()
		{
		}

		public Info(MeshRenderer _renderer)
		{
			meshRenderer = _renderer;
			if (!(meshRenderer == null))
			{
				Material[] sharedMaterials = meshRenderer.sharedMaterials;
				materialInfos = new MaterialInfo[sharedMaterials.Length];
				for (int i = 0; i < sharedMaterials.Length; i++)
				{
					materialInfos[i] = new MaterialInfo();
				}
			}
		}

		public Color GetDefColor(int _id)
		{
			Material[] sharedMaterials = meshRenderer.sharedMaterials;
			for (int i = 0; i < sharedMaterials.Length; i++)
			{
				MaterialInfo materialInfo = materialInfos.SafeGet(i);
				switch (_id)
				{
				case 0:
					if (materialInfo.isColor1)
					{
						return sharedMaterials[i].GetColor("_Color");
					}
					break;
				case 1:
					if (materialInfo.isColor2)
					{
						return sharedMaterials[i].GetColor("_Color2");
					}
					break;
				case 2:
					if (materialInfo.isColor3)
					{
						return sharedMaterials[i].GetColor("_Color3");
					}
					break;
				case 4:
					if (materialInfo.isEmission)
					{
						return sharedMaterials[i].GetColor("_EmissionColor");
					}
					break;
				}
			}
			return Color.white;
		}
	}

	[Header("色替え関係")]
	public Info[] renderers;

	public Color defColor1 = Color.white;

	public Color defColor2 = Color.white;

	public Color defColor3 = Color.white;

	[ColorUsage(false, true)]
	public Color defEmissionColor = Color.clear;

	[Header("行動関係")]
	public ActionPoint[] actionPoints;

	public FarmPoint[] farmPoints;

	public PetHomePoint[] petHomePoints;

	public JukePoint[] jukePoints;

	public CraftPoint[] craftPoints;

	public LightSwitchPoint[] lightSwitchPoints;

	public HPoint[] hPoints;

	[Header("オプション関係")]
	public GameObject[] objOption;

	[Header("遮蔽関係")]
	public ColInfo[] colInfos;

	[Header("サイズ関係")]
	public bool autoSize = true;

	public Vector3 min = Vector3.zero;

	public Vector3 max = Vector3.zero;

	[Header("重なり関係")]
	public bool overlap;

	public Collider[] overlapColliders;

	[Header("初期関係")]
	public Vector3 initPos = Vector3.zero;

	public Vector3 position
	{
		[CompilerGenerated]
		get
		{
			return base.transform.position;
		}
	}

	public bool IsColor
	{
		[CompilerGenerated]
		get
		{
			return IsColor1 | IsColor2 | IsColor3 | IsEmissionColor;
		}
	}

	public bool IsColor1
	{
		get
		{
			if (renderers.IsNullOrEmpty())
			{
				return false;
			}
			return renderers.Any((Info _info) => !_info.materialInfos.IsNullOrEmpty() && _info.materialInfos.Any((MaterialInfo v) => v.isColor1));
		}
	}

	public bool IsColor2
	{
		get
		{
			if (renderers.IsNullOrEmpty())
			{
				return false;
			}
			return renderers.Any((Info _info) => !_info.materialInfos.IsNullOrEmpty() && _info.materialInfos.Any((MaterialInfo v) => v.isColor2));
		}
	}

	public bool IsColor3
	{
		get
		{
			if (renderers.IsNullOrEmpty())
			{
				return false;
			}
			return renderers.Any((Info _info) => !_info.materialInfos.IsNullOrEmpty() && _info.materialInfos.Any((MaterialInfo v) => v.isColor3));
		}
	}

	public bool IsEmissionColor
	{
		get
		{
			if (renderers.IsNullOrEmpty())
			{
				return false;
			}
			return renderers.Any((Info _info) => !_info.materialInfos.IsNullOrEmpty() && _info.materialInfos.Any((MaterialInfo v) => v.isEmission));
		}
	}

	public bool IsOption => !objOption.IsNullOrEmpty() && objOption.Where((GameObject v) => v != null).Count() > 0;

	public void SetupRendererInfo()
	{
		MeshRenderer[] componentsInChildren = GetComponentsInChildren<MeshRenderer>();
		if (!componentsInChildren.IsNullOrEmpty())
		{
			renderers = componentsInChildren.Select((MeshRenderer _r) => new Info(_r)).ToArray();
		}
	}

	public void Setup(bool _force = false)
	{
		SetMinMax(_force);
		SetActionPoint();
		SetFarmPoint();
		SetHPoint();
		SetPetHomePoint();
		SetJukePoint();
		SetCraftPoint();
		SetLightSwitchPoint();
	}

	public void SetMinMax(bool _force = false)
	{
		if (!autoSize && !_force)
		{
			return;
		}
		MeshRenderer[] componentsInChildren = base.gameObject.GetComponentsInChildren<MeshRenderer>();
		if (!componentsInChildren.IsNullOrEmpty())
		{
			Bounds bounds = componentsInChildren[0].bounds;
			MeshRenderer[] array = componentsInChildren;
			foreach (MeshRenderer meshRenderer in array)
			{
				bounds.Encapsulate(meshRenderer.bounds);
			}
			min = bounds.min;
			max = bounds.max;
			min = new Vector3(Ceil(min.x), Ceil(min.y), Ceil(min.z));
			max = new Vector3(Ceil(max.x), Ceil(max.y), Ceil(max.z));
		}
	}

	public void SetVisibleOption(bool _flag)
	{
		if (!objOption.IsNullOrEmpty())
		{
			GameObject[] array = objOption;
			foreach (GameObject self in array)
			{
				self.SetActiveIfDifferent(_flag);
			}
		}
	}

	public void SetActionPoint()
	{
		actionPoints = base.gameObject.GetComponentsInChildren<ActionPoint>(includeInactive: true);
	}

	public void SetFarmPoint()
	{
		farmPoints = base.gameObject.GetComponentsInChildren<FarmPoint>(includeInactive: true);
	}

	public void SetHPoint()
	{
		if (hPoints.IsNullOrEmpty())
		{
			hPoints = base.gameObject.GetComponentsInChildren<HPoint>(includeInactive: true);
			if (hPoints.IsNullOrEmpty())
			{
				return;
			}
		}
		if (Singleton<Manager.Resources>.IsInstance())
		{
			HPoint[] array = hPoints;
			foreach (HPoint hPoint in array)
			{
				hPoint.Init();
				hPoint.SetEffectActive(set: false);
			}
		}
	}

	public void SetPetHomePoint()
	{
		petHomePoints = base.gameObject.GetComponentsInChildren<PetHomePoint>(includeInactive: true);
	}

	public void SetJukePoint()
	{
		jukePoints = base.gameObject.GetComponentsInChildren<JukePoint>(includeInactive: true);
	}

	public void SetCraftPoint()
	{
		craftPoints = base.gameObject.GetComponentsInChildren<CraftPoint>(includeInactive: true);
	}

	public void SetLightSwitchPoint()
	{
		lightSwitchPoints = base.gameObject.GetComponentsInChildren<LightSwitchPoint>(includeInactive: true);
	}

	public void SetDefColor()
	{
		if (renderers.IsNullOrEmpty())
		{
			return;
		}
		Info info = renderers.Where((Info r) => r.meshRenderer != null && !r.materialInfos.IsNullOrEmpty()).FirstOrDefault((Info _i) => _i.materialInfos.Any((MaterialInfo _m) => _m.isColor1));
		if (info != null)
		{
			defColor1 = info.GetDefColor(0);
		}
		Info info2 = renderers.Where((Info r) => r.meshRenderer != null && !r.materialInfos.IsNullOrEmpty()).FirstOrDefault((Info _i) => _i.materialInfos.Any((MaterialInfo _m) => _m.isColor2));
		if (info2 != null)
		{
			defColor2 = info2.GetDefColor(1);
		}
		Info info3 = renderers.Where((Info r) => r.meshRenderer != null && !r.materialInfos.IsNullOrEmpty()).FirstOrDefault((Info _i) => _i.materialInfos.Any((MaterialInfo _m) => _m.isColor3));
		if (info3 != null)
		{
			defColor3 = info3.GetDefColor(2);
		}
		Info info4 = renderers.Where((Info r) => r.meshRenderer != null && !r.materialInfos.IsNullOrEmpty()).FirstOrDefault((Info _i) => _i.materialInfos.Any((MaterialInfo _m) => _m.isEmission));
		if (info4 != null)
		{
			defEmissionColor = info4.GetDefColor(4);
		}
	}

	public void SetOverlapColliders(bool _flag)
	{
		if (overlapColliders.IsNullOrEmpty())
		{
			return;
		}
		foreach (Collider item in overlapColliders.Where((Collider v) => v != null))
		{
			item.enabled = _flag;
			item.gameObject.SetActiveIfDifferent(_flag);
			item.gameObject.layer = LayerMask.NameToLayer("Housing/Overlap");
		}
	}

	private float Ceil(float _value)
	{
		bool flag = _value < 0f;
		return Mathf.Ceil(Mathf.Abs(_value)) * (float)((!flag) ? 1 : (-1));
	}
}
