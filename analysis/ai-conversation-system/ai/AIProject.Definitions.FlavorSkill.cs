using System.Collections.Generic;

namespace AIProject.Definitions;

public static class FlavorSkill
{
	public enum Type
	{
		Pheromone,
		Reliability,
		Reason,
		Instinct,
		Dirty,
		Wariness,
		Darkness,
		Sociability
	}

	public static Dictionary<int, string> NameTable { get; } = new Dictionary<int, string>
	{
		[0] = "女子力",
		[1] = "信頼",
		[2] = "人間性",
		[3] = "本能",
		[4] = "変態",
		[5] = "危機管理",
		[6] = "闇",
		[7] = "社交性"
	};

}
