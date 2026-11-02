using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FreeBala
{
public static class Json
{
	public static object Parse(string s)
	{
		int i = 0;
		return Value(s, ref i);
	}

	private static void Ws(string s, ref int i)
	{
		while (i < s.Length && char.IsWhiteSpace(s[i]))
		{
			i++;
		}
	}

	private static object Value(string s, ref int i)
	{
		Ws(s, ref i);
		if (i >= s.Length)
		{
			return null;
		}
		char c = s[i];
		switch (c)
		{
		case '{':
			return Obj(s, ref i);
		case '[':
			return Arr(s, ref i);
		case '"':
			return Str(s, ref i);
		case 't':
			if (string.CompareOrdinal(s, i, "true", 0, 4) == 0)
			{
				i += 4;
				return true;
			}
			break;
		}
		if (c == 'f' && string.CompareOrdinal(s, i, "false", 0, 5) == 0)
		{
			i += 5;
			return false;
		}
		if (c == 'n' && string.CompareOrdinal(s, i, "null", 0, 4) == 0)
		{
			i += 4;
			return null;
		}
		return Num(s, ref i);
	}

	private static Dictionary<string, object> Obj(string s, ref int i)
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		i++;
		while (true)
		{
			Ws(s, ref i);
			if (s[i] == '}')
			{
				i++;
				return dictionary;
			}
			string key = Str(s, ref i);
			Ws(s, ref i);
			i++;
			dictionary[key] = Value(s, ref i);
			Ws(s, ref i);
			if (s[i] != ',')
			{
				break;
			}
			i++;
		}
		if (s[i] == '}')
		{
			i++;
			return dictionary;
		}
		throw new FormatException("JSON: esperado , ou } em " + i);
	}

	private static List<object> Arr(string s, ref int i)
	{
		List<object> list = new List<object>();
		i++;
		while (true)
		{
			Ws(s, ref i);
			if (s[i] == ']')
			{
				i++;
				return list;
			}
			list.Add(Value(s, ref i));
			Ws(s, ref i);
			if (s[i] != ',')
			{
				break;
			}
			i++;
		}
		if (s[i] == ']')
		{
			i++;
			return list;
		}
		throw new FormatException("JSON: esperado , ou ] em " + i);
	}

	private static string Str(string s, ref int i)
	{
		StringBuilder stringBuilder = new StringBuilder();
		i++;
		while (i < s.Length)
		{
			char c = s[i++];
			switch (c)
			{
			default:
				stringBuilder.Append(c);
				continue;
			case '\\':
			{
				char c2 = s[i++];
				switch (c2)
				{
				case 'n':
					stringBuilder.Append('\n');
					break;
				case 't':
					stringBuilder.Append('\t');
					break;
				case 'r':
					stringBuilder.Append('\r');
					break;
				case 'b':
					stringBuilder.Append('\b');
					break;
				case 'f':
					stringBuilder.Append('\f');
					break;
				case 'u':
					stringBuilder.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
					i += 4;
					break;
				default:
					stringBuilder.Append(c2);
					break;
				}
				continue;
			}
			case '"':
				break;
			}
			break;
		}
		return stringBuilder.ToString();
	}

	private static object Num(string s, ref int i)
	{
		int num = i;
		while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0)
		{
			i++;
		}
		return double.Parse(s.Substring(num, i - num), NumberStyles.Float, CultureInfo.InvariantCulture);
	}

	public static Dictionary<string, object> O(object o, string k)
	{
		if (!(o is Dictionary<string, object>) || !((Dictionary<string, object>)o).TryGetValue(k, out var value))
		{
			return null;
		}
		return value as Dictionary<string, object>;
	}

	public static List<object> L(object o, string k)
	{
		if (!(o is Dictionary<string, object>) || !((Dictionary<string, object>)o).TryGetValue(k, out var value))
		{
			return null;
		}
		return value as List<object>;
	}

	public static object Get(object o, string k)
	{
		if (!(o is Dictionary<string, object>) || !((Dictionary<string, object>)o).TryGetValue(k, out var value))
		{
			return null;
		}
		return value;
	}

	public static float F(object o, string k, float def = 0f)
	{
		object obj = Get(o, k);
		if (!(obj is double num))
		{
			if (!(obj is bool flag))
			{
				return def;
			}
			return flag ? 1 : 0;
		}
		return (float)num;
	}

	public static int I(object o, string k, int def = 0)
	{
		if (!(Get(o, k) is double num))
		{
			return def;
		}
		return (int)num;
	}

	public static bool B(object o, string k, bool def = false)
	{
		object obj = Get(o, k);
		if (obj is bool)
		{
			return (bool)obj;
		}
		if (!(obj is double num))
		{
			return def;
		}
		return num != 0.0;
	}

	public static string S(object o, string k, string def = null)
	{
		return (Get(o, k) as string) ?? def;
	}

	public static float Fv(object v, float def = 0f)
	{
		if (!(v is double num))
		{
			return def;
		}
		return (float)num;
	}

	public static float[] Floats(List<object> l)
	{
		if (l == null)
		{
			return new float[0];
		}
		float[] array = new float[l.Count];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = Fv(l[i]);
		}
		return array;
	}
}
}
