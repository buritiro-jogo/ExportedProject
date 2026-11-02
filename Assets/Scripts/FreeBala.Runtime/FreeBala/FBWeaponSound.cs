using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FreeBala
{
public static class FBWeaponSound
{
	private static readonly Regex SUPP = new Regex("silenc|suppress", RegexOptions.IgnoreCase);

	private static readonly Dictionary<string, string[]> OPEN = new Dictionary<string, string[]>
	{
		["pistola"] = new string[4] { "A_Pistol_Short_1", "A_Pistol_Short_2", "A_Pistol_Short_3", "S_WEP_Fire_002" },
		["submetralhadora"] = new string[4] { "A_MachineGun_F_1", "A_MachineGun_F_2", "A_MachineGun_F_3", "A_MachineGun_F_4" },
		["fuzil"] = new string[4] { "AK_ONE_S_HOT", "AKM_Single_Shot2", "AR_Single_Shot_2", "S_WEP_Fire_003" },
		["escopeta"] = new string[1] { "S_WEP_Fire_008" },
		["sniper"] = new string[5] { "A_Sniper_B_1", "A_Sniper_B_2", "A_Sniper_B_3", "A_Sniper_B_4", "A_Sniper_B_5" },
		["explosivo"] = new string[1] { "S_WEP_Fire_Launcher" }
	};

	private static readonly Dictionary<string, (string[] open, string[] supp, string[] tail)> cache = new Dictionary<string, (string[], string[], string[])>();

	private static Dictionary<string, object> table;

	private static bool tableTried;

	private static Dictionary<string, object> T(WeaponDef d)
	{
		if (!tableTried)
		{
			tableTried = true;
			TextAsset textAsset = Resources.Load<TextAsset>("Audio/Weapons/weapon_sounds");
			if ((bool)textAsset)
			{
				table = Json.O(Json.Parse(textAsset.text) as Dictionary<string, object>, "armas");
			}
		}
		if (d != null)
		{
			return Json.O(table, d.id);
		}
		return null;
	}

	private static string[] L(WeaponDef d, string k)
	{
		return (from o in Json.L(T(d), k) ?? new List<object>()
			select o as string into n
			where n != null && FBAudio.Has(n)
			select n).ToArray();
	}

	private static string One(WeaponDef d, string k)
	{
		string text = Json.S(T(d), k);
		if (text == null || !FBAudio.Has(text))
		{
			return null;
		}
		return text;
	}

	private static string Pick(string[] l)
	{
		if (l.Length == 0)
		{
			return null;
		}
		return l[Random.Range(0, l.Length)];
	}

	public static string Draw(WeaponDef d)
	{
		return One(d, "draw");
	}

	public static string Holster(WeaponDef d)
	{
		return One(d, "holster");
	}

	public static string ReloadOf(WeaponDef d)
	{
		return One(d, "reload");
	}

	public static string Insert(WeaponDef d)
	{
		return Pick(L(d, "insert"));
	}

	public static string Close(WeaponDef d)
	{
		return One(d, "close");
	}

	public static int FireCount(WeaponDef d)
	{
		return Sets(d).open.Length;
	}

	private static (string[] open, string[] supp, string[] tail) Sets(WeaponDef d)
	{
		if (cache.TryGetValue(d.id, out (string[], string[], string[]) value))
		{
			return value;
		}
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		List<string> list3 = new List<string>();
		string[] array = L(d, "fire");
		if (array.Length != 0)
		{
			list.AddRange(array);
			string text = ((d.cls == "escopeta") ? "Shotgun_Boom_103__Suppressed_" : "S_WEP_Fire_Silencer");
			if (FBAudio.Has(text))
			{
				list2.Add(text);
			}
			if (d.tail != null)
			{
				foreach (object item in d.tail)
				{
					if (item is string text2 && FBAudio.Has(text2))
					{
						list3.Add(text2);
					}
				}
			}
			return cache[d.id] = (list.ToArray(), list2.ToArray(), list3.ToArray());
		}
		if (d.fire != null)
		{
			foreach (object item2 in d.fire)
			{
				if (item2 is string text3 && FBAudio.Has(text3))
				{
					(SUPP.IsMatch(text3) ? list2 : list).Add(text3);
				}
			}
		}
		if (d.tail != null)
		{
			foreach (object item3 in d.tail)
			{
				if (item3 is string text4 && FBAudio.Has(text4))
				{
					list3.Add(text4);
				}
			}
		}
		if (list.Count == 0 && OPEN.TryGetValue(d.cls ?? "", out var value2))
		{
			string[] array2 = value2;
			foreach (string text5 in array2)
			{
				if (FBAudio.Has(text5))
				{
					list.Add(text5);
				}
			}
		}
		if (list.Count == 0)
		{
			string[] array2 = new string[3] { "disparo1", "disparo2", "disparo3" };
			foreach (string text6 in array2)
			{
				if (FBAudio.Has(text6))
				{
					list.Add(text6);
				}
			}
		}
		if (list2.Count == 0)
		{
			string text7 = ((d.cls == "escopeta") ? "Shotgun_Boom_103__Suppressed_" : "S_WEP_Fire_Silencer");
			if (FBAudio.Has(text7))
			{
				list2.Add(text7);
			}
		}
		return cache[d.id] = (list.ToArray(), list2.ToArray(), list3.ToArray());
	}

	public static bool Silenced(WeaponDef d)
	{
		if (d == null)
		{
			return false;
		}
		FBWeaponModel.Choice choice = FBWeaponModel.Of(d);
		if (choice == null || choice.muzzle < 0)
		{
			return false;
		}
		string input = Json.S(FBWeaponModel.Att(d, "muzzles", choice.muzzle), "node") ?? "";
		return SUPP.IsMatch(input);
	}

	public static string Fire(WeaponDef d, bool silenced)
	{
		(string[], string[], string[]) tuple = Sets(d);
		string[] array;
		if (!silenced || tuple.Item2.Length == 0)
		{
			if (tuple.Item1.Length != 0)
			{
				(array, _, _) = tuple;
			}
			else
			{
				array = tuple.Item2;
			}
		}
		else
		{
			array = tuple.Item2;
		}
		string[] array2 = array;
		if (array2.Length == 0)
		{
			return null;
		}
		return array2[Random.Range(0, array2.Length)];
	}

	public static string Tail(WeaponDef d)
	{
		string[] item = Sets(d).tail;
		if (item.Length == 0)
		{
			return null;
		}
		return item[Random.Range(0, item.Length)];
	}

	public static string Far(WeaponDef d)
	{
		object obj;
		if (d != null)
		{
			obj = Pick(L(d, "far"));
			if (obj == null)
			{
				return FBAudio.Weapon171(d.id, d.cls, "far");
			}
		}
		else
		{
			obj = null;
		}
		return (string)obj;
	}

	public static string Reload(WeaponDef d)
	{
		if (d != null)
		{
			return FBAudio.Weapon171(d.id, d.cls, "reload");
		}
		return null;
	}

	public static string Empty(WeaponDef d)
	{
		if (d != null)
		{
			return FBAudio.Weapon171(d.id, d.cls, "empty");
		}
		return null;
	}
}
}
