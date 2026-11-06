using System;
using System.Runtime.CompilerServices;
using MessagePack;
using UnityEngine;

namespace Housing;

[MessagePackObject(false)]
public class OIItem : IObjectInfo
{
	[IgnoreMember]
	public int Kind
	{
		[CompilerGenerated]
		get
		{
			return 0;
		}
	}

	[Key(0)]
	public Vector3 Pos { get; set; } = Vector3.zero;


	[Key(1)]
	public Vector3 Rot { get; set; } = Vector3.zero;


	[Key(2)]
	public int Category { get; set; }

	[Key(3)]
	public int ID { get; set; }

	[Key(4)]
	public int[] ActionPoints { get; set; }

	[Key(5)]
	public int[] FarmPoints { get; set; }

	[Key(6)]
	public Color Color1 { get; set; }

	[Key(7)]
	public Color Color2 { get; set; }

	[Key(8)]
	public Color Color3 { get; set; }

	[Key(9)]
	public Color EmissionColor { get; set; }

	[Key(10)]
	public int[] PetHomePoints { get; set; }

	[Key(11)]
	public int[] JukePoints { get; set; }

	[Key(12)]
	public int[] CraftPoints { get; set; }

	[Key(13)]
	public bool VisibleOption { get; set; } = true;


	[Key(14)]
	public int[] LightSwitchPoints { get; set; }

	public OIItem()
	{
		Color1 = Color.white;
		Color2 = Color.white;
		Color3 = Color.white;
		EmissionColor = Color.clear;
		VisibleOption = true;
	}

	public OIItem(OIItem _src)
	{
		Pos = _src.Pos;
		Rot = _src.Rot;
		Category = _src.Category;
		ID = _src.ID;
		Color1 = _src.Color1;
		Color2 = _src.Color2;
		Color3 = _src.Color3;
		EmissionColor = _src.EmissionColor;
		VisibleOption = _src.VisibleOption;
	}

	public OIItem(OIItem _src, bool _idCopy)
	{
		Pos = _src.Pos;
		Rot = _src.Rot;
		Category = _src.Category;
		ID = _src.ID;
		if (_idCopy)
		{
			ActionPoints = Clone(_src.ActionPoints);
			FarmPoints = Clone(_src.FarmPoints);
			PetHomePoints = Clone(_src.PetHomePoints);
			JukePoints = Clone(_src.JukePoints);
			CraftPoints = Clone(_src.CraftPoints);
			LightSwitchPoints = Clone(_src.LightSwitchPoints);
		}
		Color1 = _src.Color1;
		Color2 = _src.Color2;
		Color3 = _src.Color3;
		EmissionColor = _src.EmissionColor;
		VisibleOption = _src.VisibleOption;
	}

	private int[] Clone(int[] _src)
	{
		if (_src.IsNullOrEmpty())
		{
			return null;
		}
		int[] array = new int[_src.Length];
		Array.Copy(_src, array, _src.Length);
		return array;
	}
}
