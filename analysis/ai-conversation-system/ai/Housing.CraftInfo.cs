using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using AIProject;
using AIProject.Animal;
using Manager;
using MessagePack;
using UnityEngine;

namespace Housing;

[MessagePackObject(false)]
public class CraftInfo
{
	public class AboutInfo
	{
		public int ProductNo { get; private set; }

		public string FileMark { get; private set; }

		public Version Version { get; private set; }

		public string UUID { get; private set; }

		public string HUID { get; private set; }

		public int AreaType { get; private set; }

		public int LanguageInt { get; private set; }

		public AboutInfo(BinaryReader _reader)
		{
			ProductNo = _reader.ReadInt32();
			FileMark = _reader.ReadString();
			Version = new Version(_reader.ReadString());
			UUID = _reader.ReadString();
			HUID = _reader.ReadString();
			AreaType = _reader.ReadInt32();
			if (Version.CompareTo(new Version("0.0.2")) >= 0)
			{
				LanguageInt = _reader.ReadInt32();
			}
		}
	}

	[IgnoreMember]
	private readonly Version _version = new Version(0, 0, 2);

	[Key(0)]
	public List<IObjectInfo> ObjectInfos { get; set; } = new List<IObjectInfo>();


	[Key(1)]
	public Vector3 LimitSize { get; set; }

	[Key(2)]
	public int AreaNo { get; set; }

	[IgnoreMember]
	public Dictionary<IObjectInfo, ObjectCtrl> ObjectCtrls { get; private set; } = new Dictionary<IObjectInfo, ObjectCtrl>();


	[IgnoreMember]
	public GameObject ObjRoot { get; set; }

	[IgnoreMember]
	public bool IsOverlapNow
	{
		[CompilerGenerated]
		get
		{
			return ObjectCtrls.Any((KeyValuePair<IObjectInfo, ObjectCtrl> v) => v.Value.IsOverlapNow);
		}
	}

	public CraftInfo()
	{
	}

	public CraftInfo(Vector3 _size, int _area)
	{
		LimitSize = _size;
		AreaNo = _area;
	}

	public CraftInfo(CraftInfo _src)
	{
		Copy(_src);
	}

	public CraftInfo(CraftInfo _src, bool _idCopy)
	{
		Copy(_src, _idCopy);
	}

	public void Copy(CraftInfo _src)
	{
		if (_src == null)
		{
			return;
		}
		ObjectInfos.Clear();
		foreach (IObjectInfo objectInfo in _src.ObjectInfos)
		{
			switch (objectInfo.Kind)
			{
			case 0:
				ObjectInfos.Add(new OIItem(objectInfo as OIItem));
				break;
			case 1:
				ObjectInfos.Add(new OIFolder(objectInfo as OIFolder));
				break;
			}
		}
		LimitSize = _src.LimitSize;
		AreaNo = _src.AreaNo;
	}

	public void Copy(CraftInfo _src, bool _idCopy)
	{
		if (_src == null)
		{
			return;
		}
		ObjectInfos.Clear();
		foreach (IObjectInfo objectInfo in _src.ObjectInfos)
		{
			switch (objectInfo.Kind)
			{
			case 0:
				ObjectInfos.Add(new OIItem(objectInfo as OIItem, _idCopy));
				break;
			case 1:
				ObjectInfos.Add(new OIFolder(objectInfo as OIFolder, _idCopy));
				break;
			}
		}
		LimitSize = _src.LimitSize;
		AreaNo = _src.AreaNo;
	}

	public void GetActionPoint(ref List<ActionPoint> _points)
	{
		foreach (KeyValuePair<IObjectInfo, ObjectCtrl> objectCtrl in ObjectCtrls)
		{
			if (objectCtrl.Value != null)
			{
				objectCtrl.Value.GetActionPoint(ref _points);
			}
		}
	}

	public void GetFarmPoint(ref List<FarmPoint> _points)
	{
		foreach (KeyValuePair<IObjectInfo, ObjectCtrl> objectCtrl in ObjectCtrls)
		{
			if (objectCtrl.Value != null)
			{
				objectCtrl.Value.GetFarmPoint(ref _points);
			}
		}
	}

	public void GetHPoint(ref List<HPoint> _points)
	{
		foreach (KeyValuePair<IObjectInfo, ObjectCtrl> objectCtrl in ObjectCtrls)
		{
			if (objectCtrl.Value != null)
			{
				objectCtrl.Value.GetHPoint(ref _points);
			}
		}
	}

	public void GetColInfo(ref List<ItemComponent.ColInfo> _infos)
	{
		foreach (KeyValuePair<IObjectInfo, ObjectCtrl> objectCtrl in ObjectCtrls)
		{
			if (objectCtrl.Value != null)
			{
				objectCtrl.Value.GetColInfo(ref _infos);
			}
		}
	}

	public void GetPetHomePoint(ref List<PetHomePoint> _points)
	{
		foreach (KeyValuePair<IObjectInfo, ObjectCtrl> objectCtrl in ObjectCtrls)
		{
			if (objectCtrl.Value != null)
			{
				objectCtrl.Value.GetPetHomePoint(ref _points);
			}
		}
	}

	public void GetJukePoint(ref List<JukePoint> _points)
	{
		foreach (KeyValuePair<IObjectInfo, ObjectCtrl> objectCtrl in ObjectCtrls)
		{
			if (objectCtrl.Value != null)
			{
				objectCtrl.Value.GetJukePoint(ref _points);
			}
		}
	}

	public void GetCraftPoint(ref List<CraftPoint> _points)
	{
		foreach (KeyValuePair<IObjectInfo, ObjectCtrl> objectCtrl in ObjectCtrls)
		{
			if (objectCtrl.Value != null)
			{
				objectCtrl.Value.GetCraftPoint(ref _points);
			}
		}
	}

	public void GetLightSwitchPoint(ref List<LightSwitchPoint> _points)
	{
		foreach (KeyValuePair<IObjectInfo, ObjectCtrl> objectCtrl in ObjectCtrls)
		{
			if (objectCtrl.Value != null)
			{
				objectCtrl.Value.GetLightSwitchPoint(ref _points);
			}
		}
	}

	public int GetUsedNum(int _no)
	{
		int _num = 0;
		foreach (KeyValuePair<IObjectInfo, ObjectCtrl> objectCtrl in ObjectCtrls)
		{
			if (objectCtrl.Value != null)
			{
				objectCtrl.Value.GetUsedNum(_no, ref _num);
			}
		}
		return _num;
	}

	public void SetOverlapColliders(bool _flag)
	{
		foreach (KeyValuePair<IObjectInfo, ObjectCtrl> objectCtrl in ObjectCtrls)
		{
			if (objectCtrl.Value != null)
			{
				objectCtrl.Value.SetOverlapColliders(_flag);
			}
		}
	}

	public ObjectCtrl FindOverlapObject(ObjectCtrl _old)
	{
		List<ObjectCtrl> _lst = new List<ObjectCtrl>();
		foreach (KeyValuePair<IObjectInfo, ObjectCtrl> objectCtrl in ObjectCtrls)
		{
			if (objectCtrl.Value != null)
			{
				objectCtrl.Value.GetOverlapObject(ref _lst);
			}
		}
		if (_lst.IsNullOrEmpty())
		{
			return null;
		}
		int num = _lst.FindIndex((ObjectCtrl v) => v == _old);
		return _lst.SafeGet(GlobalMethod.ValLoop(num + 1, _lst.Count));
	}

	public void DeleteObject()
	{
		ObjectCtrl[] array = ObjectCtrls.Values.ToArray();
		for (int i = 0; i < array.Length; i++)
		{
			array[i]?.OnDelete();
		}
		ObjectCtrls.Clear();
	}

	public void Save(string _path, byte[] _pngData)
	{
		using FileStream output = new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.Write);
		using BinaryWriter binaryWriter = new BinaryWriter(output);
		binaryWriter.Write(_pngData);
		binaryWriter.Write(100);
		binaryWriter.Write("【AIS_Housing】");
		binaryWriter.Write(_version.ToString());
		binaryWriter.Write(Singleton<GameSystem>.Instance.UserUUID);
		binaryWriter.Write(YS_Assist.CreateUUID());
		binaryWriter.Write(Singleton<Manager.Housing>.Instance.GetSizeType(AreaNo));
		binaryWriter.Write(Singleton<GameSystem>.Instance.languageInt);
		byte[] buffer = MessagePackSerializer.Serialize(this);
		binaryWriter.Write(buffer);
	}

	public bool Load(string _path)
	{
		try
		{
			using FileStream fileStream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
			if (fileStream.Length == 0)
			{
				Debug.LogError("空データ");
				return false;
			}
			using BinaryReader binaryReader = new BinaryReader(fileStream);
			long num = PngFile.SkipPng(binaryReader);
			binaryReader.ReadInt32();
			binaryReader.ReadString();
			Version version = new Version(binaryReader.ReadString());
			binaryReader.ReadString();
			binaryReader.ReadString();
			binaryReader.ReadInt32();
			if (version.CompareTo(new Version("0.0.2")) >= 0)
			{
				binaryReader.ReadInt32();
			}
			byte[] bytes = binaryReader.ReadBytes((int)(binaryReader.BaseStream.Length - binaryReader.BaseStream.Position));
			return Load(bytes);
		}
		catch (Exception ex)
		{
			if (ex is FileNotFoundException)
			{
				Debug.Log("指定されたデータは存在しません: " + _path);
				return false;
			}
			Debug.LogException(ex);
			return false;
		}
	}

	public bool Load(string _assetBundle, string _asset)
	{
		TextAsset textAsset = CommonLib.LoadAsset<TextAsset>(_assetBundle, _asset, clone: false, string.Empty);
		return !(textAsset == null) && Load(textAsset.bytes);
	}

	public bool Load(byte[] _bytes)
	{
		try
		{
			if (_bytes.IsNullOrEmpty())
			{
				return false;
			}
			CraftInfo src = MessagePackSerializer.Deserialize<CraftInfo>(_bytes);
			Copy(src);
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
			return false;
		}
		return true;
	}

	public static CraftInfo LoadStatic(string _path)
	{
		using FileStream fileStream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
		if (fileStream.Length == 0)
		{
			Debug.LogError("空データ");
			return null;
		}
		using BinaryReader binaryReader = new BinaryReader(fileStream);
		long num = PngFile.SkipPng(binaryReader);
		try
		{
			binaryReader.ReadInt32();
			binaryReader.ReadString();
			Version version = new Version(binaryReader.ReadString());
			binaryReader.ReadString();
			binaryReader.ReadString();
			binaryReader.ReadInt32();
			if (version.CompareTo(new Version("0.0.2")) >= 0)
			{
				binaryReader.ReadInt32();
			}
			byte[] array = binaryReader.ReadBytes((int)(binaryReader.BaseStream.Length - binaryReader.BaseStream.Position));
			if (array.IsNullOrEmpty())
			{
				Debug.LogError("画像データ");
				return null;
			}
			return MessagePackSerializer.Deserialize<CraftInfo>(array);
		}
		catch (Exception)
		{
			Debug.LogWarning("画像データ");
			return null;
		}
	}

	public static AboutInfo LoadAbout(string _path)
	{
		using FileStream input = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		using BinaryReader binaryReader = new BinaryReader(input);
		PngFile.SkipPng(binaryReader);
		return new AboutInfo(binaryReader);
	}
}
