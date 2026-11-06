using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEx;

namespace AIProject;

[Serializable]
public class PlayState
{
	[Serializable]
	public class PlayStateInfo
	{
		public AssetBundleInfo AssetBundleInfo { get; set; }

		public AnimStateInfo InStateInfo { get; set; }

		public AnimStateInfo OutStateInfo { get; set; }

		public float FadeOutTime { get; set; } = 1f;


		public bool IsLoop { get; set; }

		public int LoopMin { get; set; }

		public int LoopMax { get; set; }
	}

	[Serializable]
	public class AnimStateInfo
	{
		[SerializeField]
		private Info[] _stateInfos;

		[SerializeField]
		private bool _enableFade;

		[SerializeField]
		private float _fadeTime;

		public Info[] StateInfos
		{
			get
			{
				return _stateInfos;
			}
			set
			{
				_stateInfos = value;
			}
		}

		public bool EnableFade
		{
			get
			{
				return _enableFade;
			}
			set
			{
				_enableFade = value;
			}
		}

		public float FadeSecond
		{
			get
			{
				return _fadeTime;
			}
			set
			{
				_fadeTime = value;
			}
		}
	}

	[Serializable]
	public struct Info
	{
		public string stateName;

		public int layer;

		public int ShortNameStateHash
		{
			[CompilerGenerated]
			get
			{
				return Animator.StringToHash(stateName);
			}
		}

		public Info(string name_, int layer_)
		{
			stateName = name_;
			layer = layer_;
		}
	}

	public struct ItemInfo
	{
		public string parentName;

		public bool fromEquipedItem;

		public int itemID;

		public bool isSync;
	}

	private List<ItemInfo> _itemInfos = new List<ItemInfo>();

	public int Layer { get; set; }

	public int DirectionType { get; set; }

	public PlayStateInfo MainStateInfo { get; private set; } = new PlayStateInfo();


	public List<PlayStateInfo> SubStateInfos { get; private set; } = new List<PlayStateInfo>();


	public ActionInfo ActionInfo { get; set; }

	public Info MaskStateInfo { get; set; }

	public int ItemInfoCount
	{
		[CompilerGenerated]
		get
		{
			int? num = ((_itemInfos != null) ? new int?(_itemInfos.Count) : null);
			return num.HasValue ? num.Value : 0;
		}
	}

	public bool EndEnableBlend { get; set; }

	public float EndBlendRate { get; set; }

	public bool UseNeckLook { get; set; }

	public PlayState()
	{
	}

	public PlayState(int layer, string[] inStateNames, string[] outStateNames)
	{
		Layer = layer;
		MainStateInfo.InStateInfo = new AnimStateInfo();
		if (!inStateNames.IsNullOrEmpty())
		{
			Info[] array = new Info[inStateNames.Length];
			MainStateInfo.InStateInfo.StateInfos = array;
			Info[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				ref Info reference = ref array2[i];
				reference = new Info(inStateNames[i], layer);
			}
		}
		MainStateInfo.OutStateInfo = new AnimStateInfo();
		if (!outStateNames.IsNullOrEmpty())
		{
			Info[] array = new Info[outStateNames.Length];
			MainStateInfo.OutStateInfo.StateInfos = array;
			Info[] array3 = array;
			for (int j = 0; j < array3.Length; j++)
			{
				ref Info reference2 = ref array3[j];
				reference2 = new Info(outStateNames[j], layer);
			}
		}
	}

	public ItemInfo GetItemInfo(int index)
	{
		ItemInfo? itemInfo = ((_itemInfos != null) ? new ItemInfo?(_itemInfos.GetElement(index)) : null);
		return (!itemInfo.HasValue) ? default(ItemInfo) : itemInfo.Value;
	}

	~PlayState()
	{
	}

	public void AddItemInfo(ItemInfo itemInfo)
	{
		_itemInfos.Add(itemInfo);
	}
}
