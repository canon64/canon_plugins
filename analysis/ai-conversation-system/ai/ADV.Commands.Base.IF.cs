using Illusion;
using Illusion.Extensions;
using UnityEngine;

namespace ADV.Commands.Base;

public class IF : CommandBase
{
	private const string compStr = "check";

	private string left;

	private string center;

	private string right;

	private string jumpTrue;

	private string jumpFalse;

	public override string[] ArgsLabel => new string[5] { "Left", "Center", "Right", "True", "False" };

	public override string[] ArgsDefault => new string[5]
	{
		"a",
		string.Empty,
		"b",
		string.Empty,
		string.Empty
	};

	public override void Convert(string fileName, ref string[] args)
	{
		if (!Cast(ref args[1]))
		{
			Debug.LogErrorFormat("FileName:{0},IF:Formula Cast Error:{1}\nArgs:{2}", fileName, args[1], string.Join(",", args));
		}
	}

	private static bool Cast(ref string arg)
	{
		int num = Illusion.Utils.Comparer.STR.Check(arg);
		bool result = true;
		if (num == -1)
		{
			if (arg.Compare("check", ignoreCase: true))
			{
				num = Illusion.Utils.Enum<Illusion.Utils.Comparer.Type>.Length;
			}
			else
			{
				num = 0;
				result = false;
			}
		}
		arg = num.ToString();
		return result;
	}

	public override void ConvertBeforeArgsProc()
	{
		base.ConvertBeforeArgsProc();
		int num = 0;
		left = args[num++];
		center = args[num++];
		right = args[num++];
	}

	public override void Do()
	{
		base.Do();
		ValData value = null;
		ValData value2 = null;
		int num = int.Parse(center);
		if (num < Illusion.Utils.Enum<Illusion.Utils.Comparer.Type>.Length)
		{
			if (!base.scenario.Vars.TryGetValue(left, out value))
			{
				value = new ValData(VAR.CheckLiterals(left));
			}
			if (!base.scenario.Vars.TryGetValue(right, out value2))
			{
				value2 = new ValData(value.Convert(right));
			}
		}
		bool flag = false;
		flag = (Illusion.Utils.Comparer.Type)num switch
		{
			Illusion.Utils.Comparer.Type.Equal => value.o.Equals(value2.o), 
			Illusion.Utils.Comparer.Type.NotEqual => !value.o.Equals(value2.o), 
			Illusion.Utils.Comparer.Type.Greater => value > value2, 
			Illusion.Utils.Comparer.Type.Lesser => value < value2, 
			Illusion.Utils.Comparer.Type.Over => value >= value2, 
			Illusion.Utils.Comparer.Type.Under => value <= value2, 
			_ => base.scenario.Vars.ContainsKey(left), 
		};
		jumpTrue = args[3];
		jumpFalse = args[4];
		string text = ((!flag) ? jumpFalse : jumpTrue);
		if (text.IsNullOrEmpty())
		{
			Debug.LogWarning(string.Format("IF[tag:Empty]{0}:{1}", "answer", flag));
		}
		else
		{
			base.scenario.SearchTagJumpOrOpenFile(text, base.localLine);
		}
	}
}
