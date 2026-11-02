using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
public static class FBEconomy
{
	public class Item
	{
		public string id;

		public string name;

		public string sub;

		public string icon;

		public string cls;

		public int price;

		public bool gear;

		public bool own;

		public bool rebuy = true;

		public bool best;

		public string note;

		public (string, float)[] stats;
	}

	public class Cat
	{
		public string cat;

		public string icon;

		public List<(string id, int price)> items;
	}

	public const int START = 1000;

	public const int BASE = 1500;

	public const int STEP = 250;

	public const int BASE_MAX = 3000;

	public const int WIN = 500;

	public const int LOSE = 250;

	public const int KILL = 250;

	public const int HEAD = 100;

	public const int MAX = 9999;

	public const string BOT_PISTOL = "g18";

	public static readonly List<Cat> SHOP = new List<Cat>
	{
		new Cat
		{
			cat = "PISTOLAS",
			icon = "pistol",
			items = new List<(string, int)>
			{
				("g18", 300),
				("pistola_308", 400),
				("beretta_gold", 450),
				("1911_gold", 500),
				("desert_eagle", 700),
				("desert_eagle_gold", 800)
			}
		},
		new Cat
		{
			cat = "SUBMETRALHADORAS",
			icon = "smg",
			items = new List<(string, int)>
			{
				("uzi", 800),
				("czskorpion", 900),
				("mp5", 1100),
				("ump", 1200),
				("p90", 1500)
			}
		},
		new Cat
		{
			cat = "ESCOPETAS",
			icon = "shotgun",
			items = new List<(string, int)>
			{
				("remington_870", 1000),
				("m31", 1200)
			}
		},
		new Cat
		{
			cat = "FUZIS",
			icon = "rifle",
			items = new List<(string, int)>
			{
				("ar15", 1900),
				("aks74", 2000),
				("ak", 2100),
				("m4", 2100),
				("m16", 2200),
				("galil_ace", 2300),
				("g36", 2300),
				("lr_300", 2300),
				("sig", 2400),
				("parafal_1", 2400),
				("acr", 2500),
				("scar", 2500),
				("fal", 2600),
				("ak12", 2700),
				("ar10", 2800),
				("rpk", 3000)
			}
		},
		new Cat
		{
			cat = "SNIPERS",
			icon = "scope",
			items = new List<(string, int)>
			{
				("parafal", 2600),
				("sniper_hunting", 2800),
				("barret_50", 4500)
			}
		},
		new Cat
		{
			cat = "EQUIPAMENTO",
			icon = "vest",
			items = new List<(string, int)>
			{
				("kit", 1000),
				("colete", 650),
				("capacete", 450),
				("medkit", 300)
			}
		}
	};

	private static readonly Dictionary<string, (string name, string sub)> GEAR = new Dictionary<string, (string, string)>
	{
		["colete"] = ("COLETE", "BLINDAGEM · ABSORVE 45%"),
		["capacete"] = ("CAPACETE", "HEADSHOT −40%"),
		["kit"] = ("KIT COLETE + CAPACETE", "PROTEÇÃO COMPLETA · ECONOMIZE $100"),
		["medkit"] = ("KIT MÉDICO", $"+{75f} DE VIDA · TECLA H")
	};

	private static readonly Dictionary<string, int> CLS_PRICE = new Dictionary<string, int>
	{
		["pistola"] = 400,
		["submetralhadora"] = 1000,
		["escopeta"] = 1000,
		["fuzil"] = 2200,
		["sniper"] = 2800,
		["explosivo"] = 5000
	};

	private static readonly Dictionary<string, int> TIER = new Dictionary<string, int>
	{
		["pistola"] = 0,
		["submetralhadora"] = 1,
		["escopeta"] = 1,
		["fuzil"] = 2,
		["sniper"] = 2,
		["explosivo"] = 0
	};

	public static readonly Dictionary<FBCharacter, int> money = new Dictionary<FBCharacter, int>();

	private static readonly int[] streakL = new int[2];

	public static bool buying;

	public static float buyT;

	private static readonly string[][] BOT_GUNS = new string[2][]
	{
		new string[11]
		{
			"m4", "ar15", "g36", "scar", "mp5", "ump", "m16", "lr_300", "sig", "acr",
			"remington_870"
		},
		new string[10] { "ak", "ak12", "fal", "rpk", "aks74", "parafal_1", "galil_ace", "g3", "uzi", "m31" }
	};

	private static readonly Dictionary<string, float> GUN_MUL = new Dictionary<string, float>
	{
		["pistola"] = 0.6f,
		["submetralhadora"] = 0.85f,
		["escopeta"] = 1f,
		["fuzil"] = 1f,
		["sniper"] = 1.25f,
		["explosivo"] = 1f
	};

	private static List<string>[] botGuns;

	private static bool Econ
	{
		get
		{
			if ((bool)FBMatch.I && FBMatch.I.mode != null)
			{
				return FBMatch.I.mode.econ;
			}
			return false;
		}
	}

	public static bool EconMode(string m)
	{
		if (!(m == "squad") && !(m == "4v4"))
		{
			return m == "pvl" || m == "10x10";
		}
		return true;
	}

	public static int PriceOf(string id)
	{
		foreach (Cat item in SHOP)
		{
			foreach (var (text, result) in item.items)
			{
				if (text == id)
				{
					return result;
				}
			}
		}
		if (!GameData.Weapons.TryGetValue(id, out var value) || !CLS_PRICE.TryGetValue(value.cls, out var value2))
		{
			return 2000;
		}
		return value2;
	}

	public static int Money(FBCharacter a)
	{
		if (!a || !money.TryGetValue(a, out var value))
		{
			return 0;
		}
		return value;
	}

	public static void Reset()
	{
		foreach (FBCharacter item in FBMatch.Actors())
		{
			money[item] = 1000;
			item.armor = 0f;
			item.helmet = false;
			if (!item.isPlayer && !item.isRemote && item.team < 2)
			{
				UnarmBot(item);
			}
		}
		streakL[0] = (streakL[1] = 0);
		Hud();
	}

	public static void Give(FBCharacter a, int v, string why)
	{
		if (!a || !money.ContainsKey(a))
		{
			return;
		}
		money[a] = Mathf.Min(9999, money[a] + v);
		if (a.isPlayer)
		{
			Hud(string.Format("+${0}{1}", v, (why != null) ? (" " + why) : ""));
			if (v > 0 && (bool)FBMatch.I && FBMatch.I.state == "play")
			{
				FBAudio.Play2D("sg_coin", 0.45f, 0f);
			}
		}
	}

	public static void Round(int winner, int round)
	{
		int num = Mathf.Min(3000, 1500 + 250 * (round - 1));
		for (int i = 0; i < 2; i++)
		{
			streakL[i] = ((i != winner) ? Mathf.Min(3, streakL[i] + 1) : 0);
		}
		foreach (FBCharacter item in FBMatch.Actors().ToList())
		{
			if (money.ContainsKey(item) && item.team <= 1)
			{
				Give(item, num + ((item.team == winner) ? 500 : (250 * streakL[item.team])), (item.team == winner) ? "VITÓRIA" : "RODADA");
			}
		}
	}

	public static void Hud(string plus = null)
	{
		FBCharacter player = FBMatch.Player;
		if (!Econ || !player)
		{
			FBHUD.I?.SetCash(-1, null, 0, helmet: false);
			return;
		}
		FBHUD.I?.SetCash(Money(player), plus, Mathf.RoundToInt(player.armor), player.helmet);
		if (FBShop.Open)
		{
			FBShop.Refresh();
		}
	}

	public static void ShopTime(float t)
	{
		buyT = t;
		FBShop.SetTime(t);
	}

	public static void BuyPhase(bool on)
	{
		buying = on;
		FBHUD.I?.BuyHint(on);
		if (on)
		{
			foreach (FBCharacter item in FBMatch.Bots.ToList())
			{
				if ((bool)item && item.alive && item.team < 2)
				{
					BotBuy(item);
				}
			}
			if (FBMatch.Player.alive)
			{
				FBShop.Show(on: true);
			}
		}
		else
		{
			FBShop.Show(on: false);
		}
		Hud();
	}

	private static void EnsureBotGuns()
	{
		if (botGuns != null)
		{
			return;
		}
		bool flag = Econ || FBSession.mode == "br";
		botGuns = new List<string>[2]
		{
			new List<string>(),
			new List<string>()
		};
		for (int i = 0; i < 2; i++)
		{
			List<string> source = (from id in BOT_GUNS[i]
				where GameData.Weapons.ContainsKey(id) && (bool)FBRegistry.I.Weapon(id)
				select id into _
				orderby Random.value
				select _).ToList();
			List<string> smg = source.Where((string id) => GameData.Weapons[id].cls == "submetralhadora").ToList();
			botGuns[i].AddRange(flag ? smg.Take(1).Concat(source.Where((string id) => !smg.Contains(id)).Take(2)) : source.Take(3));
			if (botGuns[i].Count == 0)
			{
				botGuns[i].AddRange(GameData.Weapons.Keys.Where((string id) => (bool)FBRegistry.I.Weapon(id) && GameData.Weapons[id].slot == 0).Take(3));
			}
		}
	}

	public static void ResetMatch()
	{
		botGuns = null;
		money.Clear();
	}

	public static void ArmBot(FBCharacter b, string forced = null)
	{
		EnsureBotGuns();
		string text = forced ?? ((botGuns[Mathf.Clamp(b.team, 0, 1)].Count > 0) ? FBUtil.Pick(botGuns[Mathf.Clamp(b.team, 0, 1)]) : null);
		if (text == null || !GameData.Weapons.TryGetValue(text, out var value))
		{
			return;
		}
		if (Econ && forced == null && !b.isPlayer)
		{
			UnarmBot(b);
			return;
		}
		b.GetComponent<FBWeaponUser>().Give(text);
		FBBot component = b.GetComponent<FBBot>();
		if ((bool)component)
		{
			component.gunMul = (GUN_MUL.TryGetValue(value.cls, out var value2) ? value2 : 1f);
		}
	}

	public static void UnarmBot(FBCharacter b)
	{
		b.GetComponent<FBWeaponUser>().Strip();
		FBBot component = b.GetComponent<FBBot>();
		if ((bool)component)
		{
			component.gunMul = 0.6f;
		}
	}

	public static void BotBuy(FBCharacter b)
	{
		if (!money.ContainsKey(b))
		{
			return;
		}
		EnsureBotGuns();
		FBWeaponUser component = b.GetComponent<FBWeaponUser>();
		bool flag = component.Cur != null;
		int value;
		int curT = ((!flag) ? (-1) : (TIER.TryGetValue(component.Cur.cls, out value) ? value : 0));
		List<string> source = GameData.Weapons.Keys.Where((string id) => (bool)FBRegistry.I.Weapon(id) && GameData.Weapons[id].cls != "explosivo").ToList();
		List<string> list = source.Where((string id) => GameData.Weapons[id].cls != "pistola").ToList();
		List<string> list2 = list.Where((string id) => botGuns[Mathf.Clamp(b.team, 0, 1)].Contains(id)).ToList();
		List<string> list3 = ((list2.Count > 0) ? list2 : list).Where((string id) => PriceOf(id) <= money[b] && TIER[GameData.Weapons[id].cls] > curT).ToList();
		if (!flag && list3.Count == 0)
		{
			list3 = source.Where((string id) => GameData.Weapons[id].cls == "pistola" && PriceOf(id) <= money[b]).ToList();
		}
		if (list3.Count > 0 && (!flag || money[b] >= 2000 || !(Random.value < 0.4f)))
		{
			int best = list3.Max((string id) => TIER[GameData.Weapons[id].cls]);
			string text = FBUtil.Pick(list3.Where((string x) => TIER[GameData.Weapons[x].cls] == best).ToList());
			money[b] -= PriceOf(text);
			ArmBot(b, text);
		}
		if (component.Cur == null && GameData.Weapons.ContainsKey("g18"))
		{
			ArmBot(b, "g18");
		}
		if (b.armor < 60f && money[b] >= 650)
		{
			money[b] -= 650;
			b.armor = 100f;
		}
		if (!b.helmet && money[b] >= 450 && Random.value < 0.6f)
		{
			money[b] -= 450;
			b.helmet = true;
		}
		if (b.kits < 2 && money[b] >= 700)
		{
			money[b] -= 300;
			b.kits++;
		}
	}

	public static List<(string cat, string icon, List<Item> items)> ShopData()
	{
		FBCharacter player = FBMatch.Player;
		FBWeaponUser component = player.GetComponent<FBWeaponUser>();
		List<(string, string, List<Item>)> list = new List<(string, string, List<Item>)>();
		foreach (Cat item2 in SHOP)
		{
			List<Item> list2 = new List<Item>();
			foreach (var (text, price) in item2.items)
			{
				WeaponDef value2;
				if (GEAR.TryGetValue(text, out (string, string) value))
				{
					Item item = new Item
					{
						id = text,
						price = price,
						name = value.Item1,
						sub = value.Item2,
						gear = true
					};
					if (text == "colete")
					{
						item.own = player.armor > 0f;
						item.rebuy = player.armor < 100f;
						item.note = $"{Mathf.RoundToInt(player.armor)}/100";
					}
					if (text == "capacete")
					{
						item.own = player.helmet;
					}
					if (text == "kit")
					{
						item.own = player.armor >= 100f && player.helmet;
						item.rebuy = false;
						item.best = true;
					}
					if (text == "medkit")
					{
						item.own = FBMed.kits >= 4;
						item.rebuy = FBMed.kits < 4;
						item.note = $"{FBMed.kits}/{4}";
					}
					list2.Add(item);
				}
				else if (GameData.Weapons.TryGetValue(text, out value2) && (bool)FBRegistry.I.Weapon(text))
				{
					list2.Add(new Item
					{
						id = text,
						price = price,
						name = value2.name.ToUpper(),
						cls = value2.cls,
						sub = value2.cls.ToUpper(),
						own = component.Has(text),
						rebuy = false,
						stats = WStats(text)
					});
				}
			}
			if (list2.Count > 0)
			{
				list.Add((item2.cat, item2.icon, list2));
			}
		}
		return list;
	}

	private static (string, float)[] WStats(string id)
	{
		Dictionary<string, object> o = Json.O(Json.O(GameData.WeaponsRaw, id), "stats");
		float num = Json.F(o, "roundsPerMinutes", 60f / Mathf.Max(0.01f, Json.F(o, "secondsBetweenShots", 0.1f)));
		float num2 = Json.F(o, "baseDamage", 25f) * Mathf.Max(1f, Json.F(o, "shotCount", 1f));
		return new(string, float)[4]
		{
			("DANO", Mathf.Min(1f, num2 / 130f)),
			("CADÊNCIA", Mathf.Min(1f, num / 1000f)),
			("PRECISÃO", Mathf.Clamp((100f - Json.F(o, "spread", 0.25f) * 60f) / 105f, 0.05f, 1f)),
			("MOBILIDADE", Mathf.Min(1f, Json.F(o, "multiplierMovementSpeed", 1f) * 100f / 115f))
		};
	}

	public static void Buy(Item it)
	{
		FBCharacter player = FBMatch.Player;
		if (!player.alive)
		{
			return;
		}
		FBWeaponUser component = player.GetComponent<FBWeaponUser>();
		if (FBMatch.I.mode.arsenal)
		{
			if (!it.gear && GameData.Weapons.ContainsKey(it.id))
			{
				component.Give(it.id);
				FBAudio.Play2D("reload", 0.5f);
				FBHUD.I?.Toast("ARSENAL", it.name, (new string[3] { "FUZIL", "SMG", "PISTOLA" })[Mathf.Clamp(GameData.Weapons[it.id].slot, 0, 2)], new Color(0.96f, 0.67f, 0.09f));
			}
			FBShop.Refresh();
		}
		else
		{
			if (!buying)
			{
				return;
			}
			if (it.own && !it.rebuy)
			{
				if (!it.gear)
				{
					component.SwitchTo(component.SlotOfId(it.id));
				}
			}
			else if (Money(player) < it.price)
			{
				FBAudio.Synth("deny");
			}
			else if (it.gear)
			{
				if (it.id == "medkit")
				{
					if (!FBMed.Add())
					{
						FBAudio.Synth("deny");
						return;
					}
				}
				else if (it.id == "colete")
				{
					player.armor = 100f;
				}
				else if (it.id == "capacete")
				{
					player.helmet = true;
				}
				else if (it.id == "kit")
				{
					player.armor = 100f;
					player.helmet = true;
				}
				money[player] -= it.price;
				FBAudio.Synth("buy");
				FBAudio.Play2D("reload", 0.4f);
				Hud();
			}
			else
			{
				money[player] -= it.price;
				component.Give(it.id);
				FBAudio.Synth("buy");
				FBAudio.Play2D("reload", 0.5f);
				FBHUD.I?.Toast("COMPRADO", it.name, string.Format("−${0} · {1}", it.price, (new string[3] { "FUZIL", "SMG", "PISTOLA" })[Mathf.Clamp(GameData.Weapons[it.id].slot, 0, 2)]), new Color(0.21f, 0.76f, 0.35f));
				Hud();
			}
		}
	}

	public static IEnumerable<Item> ArsenalItems()
	{
		return from d in GameData.Weapons.Values
			where FBRegistry.I.Weapon(d.id)
			orderby d.cls, d.name
			select new Item
			{
				id = d.id,
				name = d.name.ToUpper(),
				cls = d.cls,
				sub = d.cls.ToUpper(),
				price = 0
			};
	}
}
}
