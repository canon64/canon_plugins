using System;
using System.Collections.Generic;
using Illusion;

namespace ADV.Commands.Base;

public class IFVAR : CommandBase
{
	private string left;

	private string right;

	public override string[] ArgsLabel => new string[8] { "Type", "Variable", "CheckType", "Left", "Center", "Right", "True", "False" };

	public override string[] ArgsDefault => new string[8]
	{
		"int",
		string.Empty,
		"int",
		string.Empty,
		string.Empty,
		string.Empty,
		string.Empty,
		string.Empty
	};

	private Type type { get; set; }

	private string name { get; set; }

	private Type checkType { get; set; }

	private int refCntLeft { get; set; }

	private int refCntRight { get; set; }

	private string center { get; set; }

	private string jumpTrue { get; set; }

	private string jumpFalse { get; set; }

	public override void ConvertBeforeArgsProc()
	{
		base.ConvertBeforeArgsProc();
		int num = 0;
		type = Type.GetType(args[num++]);
		name = args[num++];
		checkType = Type.GetType(args[num++]);
		left = args.SafeGet(num++);
		refCntLeft = VAR.RefCheck(ref left);
		center = args[num++];
		right = args.SafeGet(num++);
		refCntRight = VAR.RefCheck(ref right);
	}

	public override void Do()
	{
		base.Do();
		Dictionary<string, ValData> vars = base.scenario.Vars;
		VAR.RefGet(checkType, refCntLeft, left, vars).SafeProc(delegate(string s)
		{
			left = s;
		});
		VAR.RefGet(checkType, refCntRight, right, vars).SafeProc(delegate(string s)
		{
			right = s;
		});
		ValData value = null;
		ValData value2 = null;
		int num = int.Parse(center);
		if (num < Utils.Enum<Utils.Comparer.Type>.Length)
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
		flag = (Utils.Comparer.Type)num switch
		{
			Utils.Comparer.Type.Equal => value.o.Equals(value2.o), 
			Utils.Comparer.Type.NotEqual => !value.o.Equals(value2.o), 
			Utils.Comparer.Type.Greater => value > value2, 
			Utils.Comparer.Type.Lesser => value < value2, 
			Utils.Comparer.Type.Over => value >= value2, 
			Utils.Comparer.Type.Under => value <= value2, 
			_ => base.scenario.Vars.ContainsKey(left), 
		};
		jumpTrue = args[6];
		jumpFalse = args[7];
		string o = (flag ? jumpTrue : jumpFalse);
		vars[name] = new ValData(ValData.Cast(o, type));
	}

	public override void Convert(string fileName, ref string[] args)
	{
		string text = args[0];
		Type type = null;
		switch (text)
		{
		case "int":
			type = typeof(int);
			break;
		case "float":
			type = typeof(float);
			break;
		case "string":
			type = typeof(string);
			break;
		case "bool":
			type = typeof(bool);
			break;
		default:
		{
			object obj = VAR.CheckLiterals(args[1]);
			args = new string[3]
			{
				obj.GetType().ToString(),
				args[0],
				obj.ToString()
			};
			return;
		}
		}
		args[0] = type.ToString();
		IF.Cast(ref args[4]);
	}
}
