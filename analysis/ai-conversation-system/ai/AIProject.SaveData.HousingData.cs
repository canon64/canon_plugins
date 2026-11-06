using System.Collections.Generic;
using System.Linq;
using Housing;
using Manager;
using MessagePack;
using UnityEngine;

namespace AIProject.SaveData;

[MessagePackObject(false)]
public class HousingData : IDiffComparer
{
	[Key(0)]
	public Dictionary<int, Dictionary<int, bool>> Unlock { get; set; } = new Dictionary<int, Dictionary<int, bool>>();


	[Key(1)]
	public Dictionary<int, CraftInfo> CraftInfos { get; set; } = new Dictionary<int, CraftInfo>();


	public HousingData()
	{
		UpdateDiff();
	}

	public HousingData(HousingData _src)
	{
		Unlock = _src.Unlock.ToDictionary((KeyValuePair<int, Dictionary<int, bool>> x) => x.Key, (KeyValuePair<int, Dictionary<int, bool>> x) => x.Value?.ToDictionary((KeyValuePair<int, bool> y) => y.Key, (KeyValuePair<int, bool> y) => y.Value));
		CraftInfos = _src.CraftInfos.ToDictionary((KeyValuePair<int, CraftInfo> x) => x.Key, (KeyValuePair<int, CraftInfo> x) => (x.Value == null) ? null : new CraftInfo(x.Value, _idCopy: true));
	}

	public void UpdateDiff()
	{
		if (Singleton<Manager.Housing>.IsInstance())
		{
			HashSet<int> hashSet = new HashSet<int>(Singleton<Manager.Housing>.Instance.dicLoadInfo.Select((KeyValuePair<int, Manager.Housing.LoadInfo> v) => v.Value.category));
			foreach (int c in hashSet)
			{
				Dictionary<int, bool> value = null;
				if (!Unlock.TryGetValue(c, out value))
				{
					value = new Dictionary<int, bool>();
					Unlock.Add(c, value);
				}
				foreach (KeyValuePair<int, Manager.Housing.LoadInfo> item in Singleton<Manager.Housing>.Instance.dicLoadInfo.Where((KeyValuePair<int, Manager.Housing.LoadInfo> v) => v.Value.category == c))
				{
					if (!value.ContainsKey(item.Key))
					{
						value.Add(item.Key, item.Value.requiredMaterials.IsNullOrEmpty());
					}
				}
			}
		}
		CraftInfo value2 = null;
		if (!Singleton<Manager.Housing>.IsInstance())
		{
			if (!CraftInfos.TryGetValue(0, out value2))
			{
				CraftInfos.Add(0, new CraftInfo(new Vector3(100f, 80f, 100f), 0));
			}
			if (!CraftInfos.TryGetValue(1, out value2))
			{
				CraftInfos.Add(1, new CraftInfo(new Vector3(100f, 80f, 100f), 1));
			}
			if (!CraftInfos.TryGetValue(2, out value2))
			{
				CraftInfos.Add(2, new CraftInfo(new Vector3(200f, 100f, 200f), 2));
			}
			if (!CraftInfos.TryGetValue(3, out value2))
			{
				CraftInfos.Add(3, new CraftInfo(new Vector3(500f, 150f, 500f), 3));
			}
			return;
		}
		foreach (KeyValuePair<int, Manager.Housing.AreaInfo> item2 in Singleton<Manager.Housing>.Instance.dicAreaInfo)
		{
			Manager.Housing.AreaSizeInfo value3 = null;
			if (!CraftInfos.ContainsKey(item2.Key))
			{
				Vector3Int vector3Int = ((!Singleton<Manager.Housing>.Instance.dicAreaSizeInfo.TryGetValue(item2.Value.size, out value3)) ? new Vector3Int(100, 80, 100) : value3.limitSize);
				CraftInfo value4 = new CraftInfo(vector3Int, item2.Value.no);
				CraftInfos.Add(item2.Key, value4);
			}
		}
	}

	public void CopyInstances(HousingData _src)
	{
		if (!Singleton<Manager.Housing>.IsInstance())
		{
			return;
		}
		foreach (KeyValuePair<int, CraftInfo> craftInfo2 in _src.CraftInfos)
		{
			CraftInfo craftInfo = CraftInfos[craftInfo2.Key];
			for (int i = 0; i < craftInfo2.Value.ObjectInfos.Count; i++)
			{
				IObjectInfo objectInfo = craftInfo2.Value.ObjectInfos[i];
				IObjectInfo objectInfo2 = craftInfo.ObjectInfos[i];
				if (craftInfo2.Value.ObjectCtrls.TryGetValue(objectInfo, out var value))
				{
					craftInfo.ObjectCtrls[objectInfo2] = CopyObjectCtrl(objectInfo2, objectInfo, value, null, craftInfo, craftInfo2.Value);
				}
			}
			craftInfo.ObjRoot = craftInfo2.Value.ObjRoot;
		}
	}

	private ObjectCtrl CopyObjectCtrl(IObjectInfo _dstObjectInfo, IObjectInfo _srcObjectInfo, ObjectCtrl _srcCtrl, ObjectCtrl _parent, CraftInfo _craftInfo, CraftInfo _srcCraftInfo)
	{
		if (_srcCtrl == null)
		{
			return null;
		}
		ObjectCtrl result = null;
		switch (_dstObjectInfo.Kind)
		{
		case 0:
			result = CopyObject(_dstObjectInfo as OIItem, _srcObjectInfo as OIItem, _srcCtrl.GameObject, _parent, _craftInfo, _srcCraftInfo);
			break;
		case 1:
			result = CopyFolder(_dstObjectInfo as OIFolder, _srcObjectInfo as OIFolder, _srcCtrl as OCFolder, _parent, _craftInfo, _srcCraftInfo);
			break;
		}
		return result;
	}

	private ObjectCtrl CopyFolder(OIFolder _oiFolder, OIFolder _srcOIFolder, OCFolder _srcOCFolder, ObjectCtrl _parent, CraftInfo craftInfo, CraftInfo _srcCraftInfo)
	{
		OCFolder oCFolder = new OCFolder(_oiFolder, _srcOCFolder.GameObject, craftInfo);
		if (_parent != null)
		{
			oCFolder.OnAttach(_parent);
		}
		List<IObjectInfo> list = new List<IObjectInfo>();
		for (int i = 0; i < _oiFolder.Child.Count; i++)
		{
			IObjectInfo objectInfo = _oiFolder.Child[i];
			IObjectInfo objectInfo2 = _srcOIFolder.Child[i];
			if (_srcOCFolder.Child.TryGetValue(objectInfo2, out var value))
			{
				oCFolder.Child[objectInfo] = CopyObjectCtrl(objectInfo, objectInfo2, value, oCFolder, craftInfo, _srcCraftInfo);
			}
		}
		return oCFolder;
	}

	private ObjectCtrl CopyObject(OIItem _oiItem, OIItem _srcOIFolder, GameObject _gameObject, ObjectCtrl _parent, CraftInfo craftInfo, CraftInfo _srcCraftInfo)
	{
		if (!Singleton<Manager.Housing>.Instance.dicLoadInfo.TryGetValue(_oiItem.ID, out var value))
		{
			return null;
		}
		if (_gameObject == null)
		{
			return null;
		}
		return new OCItem(_oiItem, _gameObject, craftInfo, value);
	}
}
