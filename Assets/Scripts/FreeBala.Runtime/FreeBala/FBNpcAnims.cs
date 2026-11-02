using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FreeBala
{
public static class FBNpcAnims
{
	private static readonly Dictionary<string, Dictionary<string, string[]>> POOL = new Dictionary<string, Dictionary<string, string[]>>
	{
		["f"] = new Dictionary<string, string[]>
		{
			["walk"] = new string[4] { "NPC_Elegant_Walk_2L", "NPC_Walk_relaxed_loop", "NPC_Walk_F", "NPC_Walk_relaxed_loop" },
			["walkPlayer"] = new string[3] { "NPC_Walk_relaxed_loop", "NPC_Walk_F", "NPC_Elegant_Walk_2L" },
			["run"] = new string[2],
			["idle"] = new string[7] { "NPC_Idle01_F", "NPC_Idle02_F", "NPC_Idle03_F", "NPC_Female_Idle_1", "NPC_Female_Idle_2", "NPC_Stand_Looking_F", "NPC_Tiffany_Idle" },
			["talk"] = new string[7] { "NPC_Talk_F", "NPC_Stand_talk", "NPC_Stand_Talk", "NPC_Chat_Relax_F", "NPC_Argument_Bossy_F", "NPC_Ambitious_Speech", "NPC_Emote_F" },
			["sit"] = new string[3] { "NPC_Sit_talk", "NPC_Sit_Talk_2", "NPC_Sit_Look_Up_Laugh" }
		},
		["m"] = new Dictionary<string, string[]>
		{
			["walk"] = new string[5] { "NPC_Walk_2Loop", "NPC_Walk01_loop", "NPC_Walk_Relaxed_2Loop", "NPC_Walk_Relaxed_loop", "NPC_Walk_M" },
			["walkPlayer"] = new string[3] { "NPC_Walk_M", "NPC_Walk_2Loop", "NPC_Walk01_loop" },
			["run"] = new string[1],
			["idle"] = new string[3] { "NPC_Idle01_M", "NPC_Idle02_M", "NPC_Idle03_M" },
			["talk"] = new string[4] { "NPC_Talk_M", "NPC_Talk_Serious", "NPC_Meeting_Presenter_M", "NPC_Emote_M" },
			["sit"] = new string[2] { "NPC_Relax_Sit", "NPC_Sit_Talk" }
		}
	};

	public const string HANDSHAKE = "NPC_Greeting_Handshakes_M";

	public static readonly Dictionary<string, (string enter, string outC)> SIT = new Dictionary<string, (string, string)>
	{
		["m"] = ("NPC_Stand_to_Sit", "NPC_Sit_to_Stand"),
		["f"] = ("NPC_Stand_to_sit", "NPC_Sit_to_Stand_2")
	};

	private static readonly HashSet<string> FRIDAY_GIRLS = new HashSet<string> { "n_jennifer", "n_alicia", "n_margie", "n_cat", "n_latoya", "n_mona", "n_torrance", "n_yasmine", "n_maggy" };

	public static readonly string[] POLICE = new string[4] { "r_pm_almeida", "r_pm_silva", "r_silva", "r_elaine" };

	public static bool Use171 = Array.IndexOf(Environment.GetCommandLineArgs(), "-fbsem171chars") < 0;

	private static readonly HashSet<string> STREET171 = new HashSet<string> { "pedestre", "comerciante", "servico", "motoboy", "gari", "criminoso" };

	public static List<string> Police => (from i in POLICE.Concat(ByCls("policia"))
		where (bool)FBRegistry.I && FBRegistry.I.HasChar(i)
		select i).Distinct().ToList();

	private static uint Hash(string s)
	{
		uint num = 2166136261u;
		foreach (char c in s)
		{
			num = (num ^ c) * 16777619;
		}
		return num;
	}

	private static float Rs(string id)
	{
		foreach (object item in GameData.NpcAnims ?? new List<object>())
		{
			if (Json.S(item, "id") == id)
			{
				return Json.F(item, "rs");
			}
		}
		return 0f;
	}

	public static List<string> ByCls(string cls)
	{
		if (Use171)
		{
			return (from s in GameData.Skins
				where s.x171 && s.cls == cls && (bool)FBRegistry.I && FBRegistry.I.HasChar(s.id)
				select s.id).ToList();
		}
		return new List<string>();
	}

	public static bool IsPolice(string id)
	{
		if (Array.IndexOf(POLICE, id) < 0)
		{
			return GameData.Skins.Exists((SkinDef s) => s.id == id && s.cls == "policia");
		}
		return true;
	}

	public static List<string> Roster(string except = null)
	{
		List<string> list = (from s in GameData.Skins
			where !string.IsNullOrEmpty(s.npc2) && s.id != except && FBRegistry.I.HasChar(s.id)
			select s.id).ToList();
		if (list.Count == 0)
		{
			List<string> collection = (from s in GameData.Skins
				where s.role && s.id.StartsWith("r_") && s.id != except && FBRegistry.I.HasChar(s.id)
				select s.id).ToList();
			list = (from s in GameData.Skins
				where !s.role && s.id.StartsWith("r_") && s.id != except && Array.IndexOf(POLICE, s.id) < 0 && FBRegistry.I.HasChar(s.id)
				select s.id).ToList();
			list.AddRange(collection);
			if (Use171)
			{
				list.AddRange(from s in GameData.Skins
					where s.x171 && STREET171.Contains(s.cls) && s.id != except && FBRegistry.I.HasChar(s.id)
					select s.id);
			}
		}
		return list;
	}

	private static Dictionary<string, object> Npc2Of(string id)
	{
		Dictionary<string, object> o = Json.O(GameData.Npc2, "chars");
		string text = GameData.Skins.Find((SkinDef s) => s.id == id)?.npc2;
		return Json.O(o, (text != null && text != "") ? text : id);
	}

	private static List<string> Npc2Cat(Dictionary<string, object> ch, string cat)
	{
		return (from o in Json.L(Json.O(Json.O(ch, "sets"), cat), "clips") ?? new List<object>()
			select o as string into x
			where x != null
			select x).ToList();
	}

	private static float Rs2(string id)
	{
		foreach (object item in Json.L(GameData.Npc2, "clips") ?? new List<object>())
		{
			if (Json.S(item, "id") == id)
			{
				return Json.F(item, "rs");
			}
		}
		return 0f;
	}

	public static string SitEnter(FBCharacter c)
	{
		Dictionary<string, object> dictionary = Npc2Of(c ? c.skinId : null);
		if (dictionary != null)
		{
			return Npc2Cat(dictionary, "sit").FirstOrDefault((string n) => Regex.IsMatch(n, "Stand_to_sit", RegexOptions.IgnoreCase));
		}
		return null;
	}

	public static FBCharacter.NpcSet SetFor(string id, string sex, string seed, bool player = false)
	{
		uint h = Hash(id + "|" + seed);
		Dictionary<string, object> dictionary = (player ? null : Npc2Of(id));
		if (dictionary != null)
		{
			FBCharacter.NpcSet npcSet = new FBCharacter.NpcSet
			{
				sex = ((Json.S(dictionary, "sex") == "f") ? "f" : "m")
			};
			List<string> list = Npc2Cat(dictionary, "walk");
			if (list.Count > 0)
			{
				npcSet.walk = list[(int)(h % (uint)Mathf.Min(2, list.Count))];
				npcSet.ovr["WalkFwdLoop"] = npcSet.walk;
			}
			npcSet.idle.AddRange(Npc2Cat(dictionary, "idle"));
			npcSet.talk.AddRange(Npc2Cat(dictionary, "talk"));
			npcSet.sit.AddRange(from n in Npc2Cat(dictionary, "sit")
				where !Regex.IsMatch(n, "_to_|Stand_to", RegexOptions.IgnoreCase)
				select n);
			npcSet.dance.AddRange(Npc2Cat(dictionary, "dance"));
			float num = ((npcSet.walk != null) ? Rs2(npcSet.walk) : 0f);
			npcSet.walkRs = ((num > 0f) ? num : 1f);
			return npcSet;
		}
		Dictionary<string, string[]> dictionary2 = POOL[(sex == "f") ? "f" : "m"];
		FBCharacter.NpcSet npcSet2 = new FBCharacter.NpcSet
		{
			sex = ((sex == "f") ? "f" : "m")
		};
		npcSet2.walk = At(player ? dictionary2["walkPlayer"] : dictionary2["walk"], 0);
		string text = At(dictionary2["run"], 5);
		if (player)
		{
			string text2 = ((npcSet2.sex == "f") ? "REG_Walk_F" : "REG_Walk_M");
			if ((bool)FBRegistry.I && (bool)FBRegistry.I.Clip(text2))
			{
				npcSet2.walk = text2;
			}
		}
		if (player && npcSet2.sex == "m")
		{
			npcSet2.idle.Add("IE_Unarmed_Idle01");
			npcSet2.idle.Add("Idle");
		}
		else if (FRIDAY_GIRLS.Contains(id))
		{
			npcSet2.idle.Add("NPC_Friday13_Idle");
		}
		else
		{
			int[] array = new int[3] { 0, 7, 13 };
			foreach (int k2 in array)
			{
				string item = At(dictionary2["idle"], k2);
				if (!npcSet2.idle.Contains(item))
				{
					npcSet2.idle.Add(item);
				}
			}
		}
		npcSet2.ovr["WalkFwdLoop"] = npcSet2.walk;
		if (text != null)
		{
			npcSet2.ovr["RunFwdLoop"] = text;
		}
		npcSet2.talk.AddRange(dictionary2["talk"]);
		npcSet2.sit.AddRange(dictionary2["sit"]);
		float num2 = Rs(npcSet2.walk);
		npcSet2.walkRs = ((num2 > 0f) ? num2 : 1f);
		return npcSet2;
		string At(IList<string> l, int k)
		{
			return l[(int)((h >> k) % (uint)l.Count)];
		}
	}

	public static void Apply(FBCharacter c, string seed, bool player = false)
	{
		string sex = GameData.Skins.Find((SkinDef s) => s.id == c.skinId)?.sex ?? "m";
		FBCharacter.NpcSet npcSet = SetFor(c.skinId, sex, seed, player);
		if (!player)
		{
			FBNpcRotina.Decorate(npcSet, c);
		}
		c.SetNpcSet(npcSet);
		c.walkSpeed = Mathf.Clamp(npcSet.walkRs * 1.25f * c.ScaleK * UnityEngine.Random.Range(0.86f, 1.18f) * (Regex.IsMatch(c.skinId ?? "", "idos|veio|mendigo") ? 0.78f : 1f), 0.62f, 1.9f);
	}
}
}
