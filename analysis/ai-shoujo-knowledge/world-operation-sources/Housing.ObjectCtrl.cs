using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AIProject;
using AIProject.Animal;
using UnityEngine;

namespace Housing;

public class ObjectCtrl
{
	private Transform m_transform;

	public IObjectInfo ObjectInfo { get; private set; }

	public GameObject GameObject { get; private set; }

	public CraftInfo CraftInfo { get; private set; }

	public ObjectCtrl Parent { get; private set; }

	public virtual string Name
	{
		[CompilerGenerated]
		get
		{
			return string.Empty;
		}
	}

	public Transform Transform
	{
		[CompilerGenerated]
		get
		{
			return m_transform ?? (m_transform = GameObject?.transform);
		}
	}

	public int InfoIndex
	{
		get
		{
			int num = -1;
			if (Parent == null)
			{
				return CraftInfo.ObjectInfos.IndexOf(ObjectInfo);
			}
			return (!(Parent.ObjectInfo is OIFolder oIFolder)) ? (-1) : oIFolder.Child.IndexOf(ObjectInfo);
		}
	}

	public int Kind
	{
		[CompilerGenerated]
		get
		{
			return ObjectInfo.Kind;
		}
	}

	public virtual bool IsOverlapNow
	{
		[CompilerGenerated]
		get
		{
			return false;
		}
	}

	public virtual Vector3 LocalPosition
	{
		get
		{
			return ObjectInfo.Pos;
		}
		set
		{
			ObjectInfo.Pos = value;
			CalcTransform();
		}
	}

	public virtual Vector3 LocalEulerAngles
	{
		get
		{
			return ObjectInfo.Rot;
		}
		set
		{
			ObjectInfo.Rot = value;
			CalcTransform();
		}
	}

	public virtual Quaternion LocalRotation
	{
		get
		{
			return Quaternion.Euler(LocalEulerAngles);
		}
		set
		{
			LocalEulerAngles = value.eulerAngles;
		}
	}

	public virtual Vector3 Position
	{
		get
		{
			return Transform.position;
		}
		set
		{
			Transform.position = value;
			ObjectInfo.Pos = Transform.localPosition;
			if (ObjectInfo.Pos.y < 0f)
			{
				LocalPosition = new Vector3(ObjectInfo.Pos.x, 0f, ObjectInfo.Pos.z);
			}
		}
	}

	public virtual Vector3 EulerAngles
	{
		get
		{
			return Transform.eulerAngles;
		}
		set
		{
			Transform.eulerAngles = value;
			ObjectInfo.Rot = Transform.eulerAngles;
		}
	}

	public virtual Quaternion Rotation
	{
		get
		{
			return Transform.rotation;
		}
		set
		{
			EulerAngles = value.eulerAngles;
		}
	}

	public ObjectCtrl(IObjectInfo _objectInfo, GameObject _gameObject, CraftInfo _craftInfo)
	{
		ObjectInfo = _objectInfo;
		GameObject = _gameObject;
		CraftInfo = _craftInfo;
	}

	public virtual void OnAttach(ObjectCtrl _parentCtrl, int _insert = -1)
	{
		if (Parent != null)
		{
			if (Parent.ObjectInfo is OIFolder oIFolder)
			{
				oIFolder.Child.Remove(ObjectInfo);
			}
			if (Parent is OCFolder oCFolder)
			{
				oCFolder.Child.Remove(ObjectInfo);
			}
			Parent = null;
			Transform?.SetParent(CraftInfo.ObjRoot.transform, worldPositionStays: true);
		}
		else
		{
			CraftInfo.ObjectInfos.Remove(ObjectInfo);
			CraftInfo.ObjectCtrls.Remove(ObjectInfo);
		}
		if (!(_parentCtrl is OCFolder { OIFolder: var oIFolder2 } oCFolder2))
		{
			ListAdd(CraftInfo.ObjectInfos, ObjectInfo, _insert);
			if (!CraftInfo.ObjectCtrls.ContainsKey(ObjectInfo))
			{
				CraftInfo.ObjectCtrls.Add(ObjectInfo, this);
			}
			Parent = null;
			Transform?.SetParent(CraftInfo.ObjRoot.transform, worldPositionStays: true);
		}
		else
		{
			ListAdd(oIFolder2.Child, ObjectInfo, _insert);
			if (!oCFolder2.Child.ContainsKey(ObjectInfo))
			{
				oCFolder2.Child.Add(ObjectInfo, this);
			}
			Parent = oCFolder2;
			Transform?.SetParent(Parent.Transform, worldPositionStays: true);
		}
	}

	private void ListAdd(List<IObjectInfo> _lst, IObjectInfo _info, int _insert)
	{
		if (_insert != -1)
		{
			_lst.Remove(_info);
			if (MathfEx.RangeEqualOn(0, _insert, _lst.Count - 1))
			{
				_lst.Insert(_insert, _info);
			}
			else
			{
				_lst.Add(_info);
			}
		}
		else if (!_lst.Contains(_info))
		{
			_lst.Add(_info);
		}
	}

	public virtual bool OnRemoving()
	{
		return true;
	}

	public virtual void OnDelete()
	{
		OnAttach(null);
		CraftInfo.ObjectInfos.Remove(ObjectInfo);
		CraftInfo.ObjectCtrls.Remove(ObjectInfo);
		OnDestroy();
	}

	public virtual void OnDeleteChild()
	{
	}

	public virtual void OnDestroy()
	{
		Object.DestroyImmediate(GameObject);
		GameObject = null;
		m_transform = null;
	}

	public virtual void OnSelected()
	{
	}

	public virtual void OnDeselected()
	{
	}

	public virtual void RestoreObject(GameObject _gameObject)
	{
		GameObject = _gameObject;
	}

	public virtual void CalcTransform()
	{
		Transform.localPosition = LocalPosition;
		Transform.localRotation = LocalRotation;
	}

	public virtual void GetLocalMinMax(Vector3 _pos, Quaternion _rot, Transform _root, ref Vector3 _min, ref Vector3 _max)
	{
		_min = Vector3.one;
		_max = Vector3.one;
	}

	public virtual void GetActionPoint(ref List<ActionPoint> _points)
	{
	}

	public virtual void GetFarmPoint(ref List<FarmPoint> _points)
	{
	}

	public virtual void GetHPoint(ref List<HPoint> _points)
	{
	}

	public virtual void GetColInfo(ref List<ItemComponent.ColInfo> _infos)
	{
	}

	public virtual void GetPetHomePoint(ref List<PetHomePoint> _points)
	{
	}

	public virtual void GetJukePoint(ref List<JukePoint> _points)
	{
	}

	public virtual void GetCraftPoint(ref List<CraftPoint> _points)
	{
	}

	public virtual void GetLightSwitchPoint(ref List<LightSwitchPoint> _points)
	{
	}

	public virtual void GetUsedNum(int _no, ref int _num)
	{
	}

	public virtual bool CheckOverlap(ObjectCtrl _oc, bool _load = false)
	{
		return false;
	}

	public virtual void BeforeCheckOverlap()
	{
	}

	public virtual void AfterCheckOverlap()
	{
	}

	public virtual void SetOverlapColliders(bool _flag)
	{
	}

	public virtual void GetOverlapObject(ref List<ObjectCtrl> _lst)
	{
	}
}
