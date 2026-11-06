using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AIProject.Definitions;

public static class Sickness
{
	public const int GoodHealthID = -1;

	public const int ColdID = 0;

	public const int StomachacheID = 1;

	public const int OverworkID = 2;

	public const int HeatStrokeID = 3;

	public const int HurtID = 4;

	public static ReadOnlyDictionary<int, string> NameTable { get; } = new ReadOnlyDictionary<int, string>(new Dictionary<int, string>
	{
		[0] = "風邪",
		[1] = "腹痛",
		[2] = "過労",
		[3] = "熱中",
		[4] = "ケガ"
	});


	public static Dictionary<string, int> TagTable { get; } = new Dictionary<string, int>
	{
		["cold"] = 0,
		["stomach"] = 1,
		["overwork"] = 2,
		["heatstroke"] = 3,
		["drunkenness"] = 4
	};

}
