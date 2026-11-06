using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ADV;

[Serializable]
public class OpenData
{
	[SerializeField]
	private string _bundle = string.Empty;

	[SerializeField]
	private string _asset = string.Empty;

	private ScenarioData _data;

	public string bundle
	{
		get
		{
			return _bundle;
		}
		set
		{
			_bundle = value;
		}
	}

	public string asset
	{
		get
		{
			return _asset;
		}
		set
		{
			_asset = value;
		}
	}

	public ScenarioData data
	{
		[CompilerGenerated]
		get
		{
			return _data;
		}
	}

	public bool HasData
	{
		[CompilerGenerated]
		get
		{
			return _data != null;
		}
	}

	public void Set(OpenData openData)
	{
		_asset = openData._asset;
		_bundle = openData._bundle;
		_data = openData._data;
	}

	public void ClearData()
	{
		_data = null;
	}

	public void Clear()
	{
		_asset = null;
		_bundle = null;
		ClearData();
	}

	public void Load()
	{
		_data = AssetBundleManager.LoadAsset(_bundle, _asset, typeof(ScenarioData)).GetAsset<ScenarioData>();
		AssetBundleManager.UnloadAssetBundle(_bundle, isUnloadForceRefCount: false);
	}

	public void Load(string bundle, string asset)
	{
		bool flag = !HasData;
		if (_asset != asset)
		{
			_asset = asset;
			flag = true;
		}
		if (_bundle != bundle)
		{
			_bundle = bundle;
			flag = true;
		}
		if (flag)
		{
			Load();
		}
	}

	public void FindLoad(string asset, string path)
	{
		_asset = asset;
		_bundle = Program.FindADVBundleFilePath(path, asset, out _data);
	}

	public void FindLoad(string asset, int charaID, int category)
	{
		_asset = asset;
		_bundle = Program.FindADVBundleFilePath(charaID, category, asset, out _data);
	}

	public void FindLoadMessage(string category, string asset)
	{
		_asset = asset;
		_bundle = Program.FindMessageADVBundleFilePath(category, asset, out _data);
	}
}
