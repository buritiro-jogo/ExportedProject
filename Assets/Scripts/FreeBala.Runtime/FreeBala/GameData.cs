using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FreeBala
{
public static class GameData
{
	public static float[] Dist = new float[8] { 0f, 10f, 20f, 30f, 45f, 60f, 80f, 120f };

	public static readonly Dictionary<string, WeaponDef> Weapons = new Dictionary<string, WeaponDef>();

	public static readonly Dictionary<string, RecoilClass> Recoil = new Dictionary<string, RecoilClass>();

	public static readonly Dictionary<string, MapDef> Maps = new Dictionary<string, MapDef>();

	public static readonly List<SkinDef> Skins = new List<SkinDef>();

	public static readonly Dictionary<string, float> ClipRS = new Dictionary<string, float>();

	public static List<object> Emotes;

	public static List<object> NpcAnims;

	public static List<object> Victory;

	public static Dictionary<string, object> Root;

	public static Dictionary<string, object> WeaponsRaw;

	public static Dictionary<string, object> Vehicles;

	public static Dictionary<string, object> TpsGrip;

	public static Dictionary<string, object> Som;

	public static Dictionary<string, object> Wheels;

	public static Dictionary<string, object> IE;

	public static Dictionary<string, object> RES;

	public static Dictionary<string, object> Npc2;

	public static Dictionary<string, object> RebOficina;

	public static Dictionary<string, object> FP;

	private static bool loaded;

	public static readonly HashSet<string> BannedWeapons = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "m79", "garruncha_12", "submetralhadora_caseira" };

	public static readonly HashSet<string> AllowedMaps = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "vila_jardim", "praca_aguas", "praca_pt", "orla", "centro_civico" };

	public static void Load()
	{
		if (loaded)
		{
			return;
		}
		loaded = true;
		string text = null;
		string path = Path.Combine(Application.streamingAssetsPath, "fb_data.json");
		if (text == null && File.Exists(path))
		{
			text = File.ReadAllText(path);
		}
		if (text == null)
		{
			TextAsset textAsset = Resources.Load<TextAsset>("fb_data");
			if ((bool)textAsset)
			{
				text = textAsset.text;
			}
		}
		if (text == null)
		{
			Debug.LogError("[FreeBala] fb_data.json não encontrado (rode FreeBala > Importar tudo)");
			return;
		}
		object obj = Json.Parse(text);
		Dist = Json.Floats(Json.L(obj, "dist"));
		foreach (KeyValuePair<string, object> item in Json.O(obj, "weapons"))
		{
			if (!BannedWeapons.Contains(item.Key))
			{
				object value = item.Value;
				Weapons[item.Key] = new WeaponDef
				{
					id = item.Key,
					name = Json.S(value, "name"),
					cls = Json.S(value, "cls"),
					slot = Json.I(value, "slot"),
					auto = Json.B(value, "auto"),
					bolt = Json.B(value, "bolt"),
					cycled = Json.B(value, "cycled"),
					pellets = Json.I(value, "pellets", 1),
					mag = Json.I(value, "mag", 30),
					rpm = Json.F(value, "rpm", 600f),
					spread = Json.F(value, "spread", 0.25f),
					recoilScale = Json.F(value, "recoilScale", 1f),
					move = Json.F(value, "move", 1f),
					dmg = Json.Floats(Json.L(value, "dmg")),
					dmgHead = Json.Floats(Json.L(value, "dmgHead")),
					sounds = Json.O(value, "sounds"),
					fire = Json.L(value, "fire"),
					tail = Json.L(value, "tail")
				};
			}
		}
		foreach (KeyValuePair<string, object> item2 in Json.O(obj, "recoil"))
		{
			object value2 = item2.Value;
			Recoil[item2.Key] = new RecoilClass
			{
				v = Json.F(value2, "v"),
				h = Json.F(value2, "h"),
				ramp = Json.F(value2, "ramp", 1f),
				spread = Json.F(value2, "spread"),
				ads = Json.F(value2, "ads"),
				bloom = Json.F(value2, "bloom"),
				bloomMax = Json.F(value2, "bloomMax"),
				rec = Json.F(value2, "rec", 6f)
			};
		}
		foreach (KeyValuePair<string, object> item3 in Json.O(obj, "maps"))
		{
			object o = item3.Value;
			bool disabled = Json.B(o, "disabled") || !AllowedMaps.Contains(item3.Key);
			MapDef mapDef = new MapDef
			{
				id = item3.Key,
				name = Json.S(o, "name"),
				disabled = disabled,
				level = Json.O(o, "level")
			};
			Fill(mapDef.spawnA, "spawnA");
			Fill(mapDef.spawnB, "spawnB");
			Fill(mapDef.bots, "bots");
			Fill(mapDef.points, "points");
			Fill(mapDef.waypoints, "waypoints");
			Maps[item3.Key] = mapDef;
			void Fill(List<Vector3> dst, string k)
			{
				List<object> list = Json.L(o, k);
				if (list != null)
				{
					foreach (object item4 in list)
					{
						dst.Add(Coords.FromWeb(item4 as List<object>));
					}
				}
			}
		}
		foreach (object item5 in Json.L(obj, "skins"))
		{
			Skins.Add(new SkinDef
			{
				id = Json.S(item5, "id"),
				name = Json.S(item5, "name"),
				sex = Json.S(item5, "sex", "m"),
				src = Json.S(item5, "src", ""),
				team = Json.I(item5, "team", -1),
				role = Json.B(item5, "role"),
				sheet = Json.B(item5, "sheet"),
				npc2 = Json.S(item5, "npc2", ""),
				cls = Json.S(item5, "cls", ""),
				x171 = Json.B(item5, "x171")
			});
		}
		FP = Json.O(obj, "fp");
		Root = obj as Dictionary<string, object>;
		Emotes = Json.L(obj, "emotes");
		NpcAnims = Json.L(obj, "npcAnims");
		Victory = Json.L(obj, "victory");
		WeaponsRaw = Json.O(obj, "weaponsRaw");
		Vehicles = Json.O(obj, "vehicles");
		TpsGrip = Json.O(obj, "tpsGrip");
		Som = Json.O(obj, "som");
		Wheels = Json.O(obj, "wheels");
		IE = Json.O(obj, "ie");
		RES = Json.O(obj, "res");
		Npc2 = Json.O(obj, "npc2");
		RebOficina = Json.O(obj, "rebOficina");
		Dictionary<string, object> dictionary = Json.O(obj, "clipRS");
		if (dictionary == null)
		{
			return;
		}
		foreach (KeyValuePair<string, object> item6 in dictionary)
		{
			ClipRS[item6.Key] = Json.Fv(item6.Value);
		}
	}

	public static float Move(string k, float def)
	{
		return Json.F(Json.O(FP, "movement"), k, def);
	}

	public static Dictionary<string, object> Section(string k)
	{
		return Json.O(FP, k);
	}
}
}
