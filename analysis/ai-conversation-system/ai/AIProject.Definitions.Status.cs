using System.Collections.Generic;

namespace AIProject.Definitions;

public static class Status
{
	public enum Type
	{
		Temperature,
		Mood,
		Hunger,
		Physical,
		Life,
		Motivation,
		Immoral,
		Morality
	}

	public static Dictionary<int, string> Names { get; } = new Dictionary<int, string>
	{
		[0] = "体温",
		[1] = "機嫌",
		[2] = "満腹",
		[3] = "体調",
		[4] = "生命維持[存在しない]",
		[5] = "やる気",
		[6] = "Hな気分",
		[7] = "善悪"
	};

}
