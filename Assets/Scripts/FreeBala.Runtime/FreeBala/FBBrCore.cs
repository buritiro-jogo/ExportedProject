using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public static class FBBrCore
{
	public class FFW
	{
		public string key;

		public float dmg;

		public float iv;

		public float bite;

		public float range;

		public float full;

		public float minD;

		public float minDmg;

		public float head;

		public float body;

		public float limb;

		public float mag;

		public float move;

		public float? hsFull;

		public float? hsMin;

		public float? hsMinRatio;

		public int pellets = 1;
	}

	public class Armor
	{
		public string key;

		public string kind;

		public int lv;

		public float max;

		public float red;

		public float dur;
	}

	public struct HitResult
	{
		public float dmg;

		public float absorbed;

		public float raw;

		public string part;
	}

	public struct SkyP
	{
		public float vMin;

		public float vMax;

		public float hMin;

		public float hMax;

		public float delta;

		public float rot;

		public float turnR;
	}

	public struct Route
	{
		public Vector2 a;

		public Vector2 b;

		public Vector2 dir;

		public float len;
	}

	public struct Phase
	{
		public float wait;

		public float shrink;

		public float k;

		public float dmg;
	}

	public const float HEAD = 5.5f;

	public const float BODY = 1f;

	public const float LIMB = 0.75f;

	public const float NO_HEAD_AFTER_POSE = 0.7f;

	public const float HP = 200f;

	private static Dictionary<string, FFW> weapons;

	private static Dictionary<string, object> equip;

	private static Dictionary<string, object> byId;

	private static Dictionary<string, object> byClass;

	public static readonly SkyP Dive = new SkyP
	{
		vMin = -9f,
		vMax = -15f,
		hMin = 0f,
		hMax = 5f,
		delta = 2f,
		rot = 45f
	};

	public static readonly SkyP Surf = new SkyP
	{
		vMin = -6f,
		vMax = -12f,
		hMin = 0f,
		hMax = 20f,
		delta = 10f,
		rot = 45f
	};

	public static readonly SkyP Para = new SkyP
	{
		vMin = -4f,
		vMax = -8f,
		hMin = 1.5f,
		hMax = 7f,
		turnR = 9f
	};

	public const float AutoOpenT = 10f;

	public const float SurfMinH = 160f;

	public const float SafeOpenH = 35f;

	public const float AirlineStart = 500f;

	public const float CamDiveUp = 2.8f;

	public const float CamDiveBack = 0.7f;

	public const float CamParaUp = 2.5f;

	public const float CamParaBack = 6f;

	public const float LandShake = 1.5f;

	public const float LandShakeT = 1f;

	public static readonly Phase[] PHASES = new Phase[5]
	{
		new Phase
		{
			wait = 45f,
			shrink = 35f,
			k = 0.62f,
			dmg = 2f
		},
		new Phase
		{
			wait = 35f,
			shrink = 30f,
			k = 0.55f,
			dmg = 4f
		},
		new Phase
		{
			wait = 30f,
			shrink = 25f,
			k = 0.5f,
			dmg = 7f
		},
		new Phase
		{
			wait = 25f,
			shrink = 20f,
			k = 0.45f,
			dmg = 11f
		},
		new Phase
		{
			wait = 20f,
			shrink = 18f,
			k = 0.05f,
			dmg = 16f
		}
	};

	private static void Load()
	{
		if (weapons != null)
		{
			return;
		}
		GameData.Load();
		weapons = new Dictionary<string, FFW>();
		Dictionary<string, object> o2 = Json.O(GameData.Root, "ff");
		foreach (KeyValuePair<string, object> item in Json.O(o2, "weapons") ?? new Dictionary<string, object>())
		{
			object o = item.Value;
			weapons[item.Key] = new FFW
			{
				key = item.Key,
				dmg = Json.F(o, "dmg"),
				iv = Json.F(o, "iv"),
				bite = Json.F(o, "bite"),
				range = Json.F(o, "range"),
				full = Json.F(o, "full"),
				minD = Json.F(o, "minD"),
				minDmg = Json.F(o, "minDmg"),
				head = Json.F(o, "head"),
				body = Json.F(o, "body"),
				limb = Json.F(o, "limb"),
				mag = Json.F(o, "mag"),
				move = Json.F(o, "move", 1f),
				pellets = Json.I(o, "pellets", 1),
				hsFull = N("hsFull"),
				hsMin = N("hsMin"),
				hsMinRatio = N("hsMinRatio")
			};
			float? N(string k)
			{
				if (!(Json.Get(o, k) is double num))
				{
					return null;
				}
				return (float)num;
			}
		}
		equip = Json.O(o2, "equip");
		byId = Json.O(o2, "byId");
		byClass = Json.O(o2, "byClass");
	}

	public static FFW Weapon(string key)
	{
		Load();
		if (key == null || !weapons.TryGetValue(key, out var value))
		{
			return null;
		}
		return value;
	}

	public static FFW FFWeapon(string id, string cls)
	{
		Load();
		string text = Json.S(byId, id ?? "");
		if (text != null && weapons.TryGetValue(text, out var value))
		{
			return value;
		}
		text = Json.S(byClass, cls ?? "");
		if (text != null && weapons.TryGetValue(text, out value))
		{
			return value;
		}
		if (!weapons.TryGetValue("M4A1", out value))
		{
			return new FFW
			{
				dmg = 29f,
				full = 40f,
				minD = 78f,
				minDmg = 11f
			};
		}
		return value;
	}

	public static float BaseDamage(FFW w, float dist)
	{
		if (dist <= w.full)
		{
			return w.dmg;
		}
		if (dist >= w.minD)
		{
			return w.minDmg;
		}
		return w.dmg + (w.minDmg - w.dmg) * (dist - w.full) / (w.minD - w.full);
	}

	public static float PartRatio(FFW w, string part, float dist)
	{
		if (part == "head")
		{
			float num = 5.5f + w.head;
			if (!w.hsFull.HasValue || !w.hsMin.HasValue || !w.hsMinRatio.HasValue)
			{
				return num;
			}
			float num2 = num + w.hsMinRatio.Value;
			if (dist <= w.hsFull)
			{
				return num;
			}
			if (dist >= w.hsMin)
			{
				return num2;
			}
			return num + (num2 - num) * (dist - w.hsFull.Value) / (w.hsMin.Value - w.hsFull.Value);
		}
		if (part == "limb")
		{
			return 0.75f + w.limb;
		}
		return 1f + w.body;
	}

	public static float ArmorFactor(Armor item, float bite = 0f)
	{
		if (item == null || item.dur <= 0f)
		{
			return 1f;
		}
		float num = Mathf.Clamp01(item.dur / item.max);
		return 1f - num * item.red * (1f - Mathf.Clamp01(bite));
	}

	public static Armor NewArmor(string key)
	{
		Load();
		Dictionary<string, object> dictionary = Json.O(equip, key);
		if (dictionary != null)
		{
			return new Armor
			{
				key = key,
				kind = Json.S(dictionary, "kind"),
				lv = Json.I(dictionary, "lv"),
				max = Json.F(dictionary, "max"),
				red = Json.F(dictionary, "red"),
				dur = Json.F(dictionary, "max")
			};
		}
		return null;
	}

	public static HitResult Hit(FFW w, string part, float dist, Armor vest = null, Armor helmet = null, float poseSwitchAgo = 9f)
	{
		if (part == "head" && poseSwitchAgo < 0.7f)
		{
			part = "body";
		}
		float num = BaseDamage(w, dist) * PartRatio(w, part, dist);
		Armor armor = ((part == "head") ? helmet : vest);
		float num2 = ArmorFactor(armor, w.bite);
		float num3 = num * num2;
		float num4 = num - num3;
		if (armor != null && num4 > 0f)
		{
			armor.dur = Mathf.Max(0f, armor.dur - num4);
		}
		HitResult result = default(HitResult);
		result.dmg = num3;
		result.part = part;
		result.absorbed = num4;
		result.raw = num;
		return result;
	}

	private static SkyP Of(string st)
	{
		if (!(st == "para"))
		{
			if (!(st == "surf"))
			{
				return Dive;
			}
			return Surf;
		}
		return Para;
	}

	public static Vector2 SkyVelocity(string st, float fwd, Vector2 cur, float dt)
	{
		SkyP skyP = Of(st);
		float num2;
		float num3;
		if (st == "para")
		{
			float num = (Mathf.Clamp(fwd, -1f, 1f) + 1f) / 2f;
			num2 = skyP.hMin + (skyP.hMax - skyP.hMin) * num;
			num3 = skyP.vMin + (skyP.vMax - skyP.vMin) * num;
		}
		else
		{
			float num4 = Mathf.Clamp01(fwd);
			num2 = skyP.hMin + (skyP.hMax - skyP.hMin) * num4;
			num3 = skyP.vMin + (skyP.vMax - skyP.vMin) * num4;
		}
		float num5 = 1f - Mathf.Exp((0f - ((st == "para") ? 3f : skyP.delta)) * dt);
		return new Vector2(cur.x + (num2 - cur.x) * num5, cur.y + (num3 - cur.y) * num5);
	}

	public static float SkyTurnRate(string st, float h)
	{
		if (!(st == "para"))
		{
			return Of(st).rot * ((float)Math.PI / 180f);
		}
		return Mathf.Max(0.15f, h / Para.turnR);
	}

	public static bool ShouldOpen(float fallT, float hAboveGround)
	{
		if (!(fallT >= 10f))
		{
			return hAboveGround <= 35f;
		}
		return true;
	}

	public static bool CanSurf(float hAboveGround)
	{
		return hAboveGround > 160f;
	}

	public static Route Airline(float cx, float cz, float r, Func<float> rand = null, float scale = 1f)
	{
		if (rand == null)
		{
			rand = () => UnityEngine.Random.value;
		}
		float f = rand() * (float)Math.PI * 2f;
		float num = (rand() - 0.5f) * r * 0.6f;
		float num2 = r + 500f * scale;
		float num3 = Mathf.Cos(f);
		float num4 = Mathf.Sin(f);
		float num5 = (0f - num4) * num;
		float num6 = num3 * num;
		Route result = default(Route);
		result.a = new Vector2(cx + num5 - num3 * num2, cz + num6 - num4 * num2);
		result.b = new Vector2(cx + num5 + num3 * num2, cz + num6 + num4 * num2);
		result.dir = new Vector2(num3, num4);
		result.len = 2f * num2;
		return result;
	}

	public static float JumpPointFor(Route r, float tx, float tz)
	{
		float num = tx - r.a.x;
		float num2 = tz - r.a.y;
		return Mathf.Clamp(num * r.dir.x + num2 * r.dir.y, 0f, r.len);
	}
}
}
