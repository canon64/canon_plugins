using System;
using ADV.Commands.Base;
using Illusion;

namespace ADV;

[Serializable]
public class ValData
{
	public object o { get; private set; }

	public ValData(object o)
	{
		this.o = o;
	}

	public object Convert(object val)
	{
		return Convert(val, o.GetType());
	}

	public static object Convert(object val, Type type)
	{
		return System.Convert.ChangeType(val, type);
	}

	public static object Cast(object o, Type type)
	{
		if (o == null)
		{
			return Convert(o, type);
		}
		if (type == typeof(int))
		{
			int? num = null;
			int result;
			if (o is int || o is float)
			{
				num = (int)o;
			}
			else if (o is bool)
			{
				num = (((bool)o) ? 1 : 0);
			}
			else if (int.TryParse(o.ToString(), out result))
			{
				num = result;
			}
			return num.HasValue ? num.Value : 0;
		}
		if (type == typeof(float))
		{
			float? num2 = null;
			float result2;
			if (o is float)
			{
				num2 = (float)o;
			}
			else if (float.TryParse(o.ToString(), out result2))
			{
				num2 = result2;
			}
			return num2.HasValue ? num2.Value : 0f;
		}
		if (type == typeof(bool))
		{
			bool? flag = null;
			bool result3;
			if (o is bool)
			{
				flag = (bool)o;
			}
			else if (o is int || o is float)
			{
				flag = (int)o > 0;
			}
			else if (bool.TryParse(o.ToString(), out result3))
			{
				flag = result3;
			}
			return flag.HasValue && flag.Value;
		}
		return o.ToString();
	}

	public static bool operator <(ValData a, ValData b)
	{
		return IF(Utils.Comparer.Type.Lesser, a.o, b.o);
	}

	public static bool operator >(ValData a, ValData b)
	{
		return IF(Utils.Comparer.Type.Greater, a.o, b.o);
	}

	public static bool operator <=(ValData a, ValData b)
	{
		return IF(Utils.Comparer.Type.Under, a.o, b.o);
	}

	public static bool operator >=(ValData a, ValData b)
	{
		return IF(Utils.Comparer.Type.Over, a.o, b.o);
	}

	public static ValData operator +(ValData a, ValData b)
	{
		return Calculate(Calc.Formula1.PlusEqual, a.o, b.o);
	}

	public static ValData operator -(ValData a, ValData b)
	{
		return Calculate(Calc.Formula1.MinusEqual, a.o, b.o);
	}

	public static ValData operator *(ValData a, ValData b)
	{
		return Calculate(Calc.Formula1.AstaEqual, a.o, b.o);
	}

	public static ValData operator /(ValData a, ValData b)
	{
		return Calculate(Calc.Formula1.SlashEqual, a.o, b.o);
	}

	private static bool IF(Utils.Comparer.Type type, object a, object b)
	{
		return Utils.Comparer.Check((IComparable)a, type, (IComparable)b);
	}

	private static ValData Calculate(Calc.Formula1 numerical, object a, object b)
	{
		if (a == null)
		{
			goto IL_01d3;
		}
		if (!(a is int num))
		{
			if (!(a is float num2))
			{
				if (!(a is bool flag))
				{
					goto IL_01d3;
				}
				bool flag2 = flag;
				bool flag3 = (bool)Cast(b, typeof(bool));
				switch (numerical)
				{
				case Calc.Formula1.PlusEqual:
					return new ValData((flag2 ? 1 : 0) + (flag3 ? 1 : 0) > 0);
				case Calc.Formula1.MinusEqual:
					return new ValData((flag2 ? 1 : 0) - (flag3 ? 1 : 0) > 0);
				case Calc.Formula1.AstaEqual:
					return new ValData(flag2 || flag3);
				case Calc.Formula1.SlashEqual:
					return new ValData(flag2 && flag3);
				}
			}
			else
			{
				float num3 = num2;
				float num4 = (float)Cast(b, typeof(float));
				switch (numerical)
				{
				case Calc.Formula1.PlusEqual:
					return new ValData(num3 + num4);
				case Calc.Formula1.MinusEqual:
					return new ValData(num3 - num4);
				case Calc.Formula1.AstaEqual:
					return new ValData(num3 * num4);
				case Calc.Formula1.SlashEqual:
					return new ValData(num3 / num4);
				}
			}
		}
		else
		{
			int num5 = num;
			int num6 = (int)Cast(b, typeof(int));
			switch (numerical)
			{
			case Calc.Formula1.PlusEqual:
				return new ValData(num5 + num6);
			case Calc.Formula1.MinusEqual:
				return new ValData(num5 - num6);
			case Calc.Formula1.AstaEqual:
				return new ValData(num5 * num6);
			case Calc.Formula1.SlashEqual:
				return new ValData(num5 / num6);
			}
		}
		goto IL_0210;
		IL_0210:
		return new ValData(null);
		IL_01d3:
		string text = a.ToString();
		string text2 = b.ToString();
		switch (numerical)
		{
		case Calc.Formula1.PlusEqual:
			return new ValData(text + text2);
		case Calc.Formula1.MinusEqual:
			return new ValData(text.Replace(text2, string.Empty));
		}
		goto IL_0210;
	}
}
