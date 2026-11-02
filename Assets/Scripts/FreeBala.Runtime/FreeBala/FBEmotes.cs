using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FreeBala
{
public static class FBEmotes
{
	public class Def
	{
		public string id;

		public string name;

		public string cat;

		public float len;

		public bool loop;
	}

	public static readonly string[] PICKS = new string[9] { "FF_BR_Passinho", "FF_aura_free_fire_emote", "FF_Hurricane_Red", "FF_Hurricane_Blue", "FF_Hurricane_BBQ", "FF_BR_Master", "FF_Emote_ElaborateBow", "FF_Apocalypse", "FF_Cut Throat" };

	private static List<Def> all;

	private static readonly Regex FEM = new Regex("(_F\\d?$|_F\\d?_|Female|Sensual|Sophie|Tiffany|Twirl_Hair|Viewing_Mirror|Catwalk|Girl|Open_Curtains)", RegexOptions.IgnoreCase);

	private static readonly Regex MASC = new Regex("(_M\\d?$|_M\\d?_)");

	private static readonly Regex NOSTAGE = new Regex("Sit|Stand_to|Saloon|Fast_Forward|Against_Wall|Break_Door", RegexOptions.IgnoreCase);

	public static List<Def> All
	{
		get
		{
			if (all != null)
			{
				return all;
			}
			all = new List<Def>();
			GameData.Load();
			if (GameData.Emotes != null)
			{
				foreach (object emote in GameData.Emotes)
				{
					if (PICKS.Contains(Json.S(emote, "id")) || (Json.S(emote, "id") ?? "").StartsWith("NPC_"))
					{
						all.Add(new Def
						{
							id = Json.S(emote, "id"),
							name = Json.S(emote, "name", Json.S(emote, "id")),
							cat = Json.S(emote, "cat", "fun"),
							len = Json.F(emote, "len", 9f),
							loop = Json.B(emote, "loop")
						});
					}
				}
			}
			return all;
		}
	}

	public static List<string> Victory => (GameData.Victory ?? new List<object>()).Select((object o) => o as string).Where(Ok).ToList();

	public static bool FitsSex(string id, bool female)
	{
		if (id != null)
		{
			if (!female)
			{
				return !FEM.IsMatch(id);
			}
			return !MASC.IsMatch(id);
		}
		return false;
	}

	private static bool Ok(string id)
	{
		if ((bool)FBRegistry.I)
		{
			return FBRegistry.I.Clip(id);
		}
		return false;
	}

	public static string RandomVictory(bool? female = null)
	{
		List<string> list = Victory.Where((string id) => !female.HasValue || FitsSex(id, female.Value)).ToList();
		if (list.Count == 0)
		{
			list = (from e in All
				where e.cat == "dance" && Ok(e.id) && (!female.HasValue || FitsSex(e.id, female.Value))
				select e.id).ToList();
		}
		if (list.Count <= 0)
		{
			return null;
		}
		return list[Random.Range(0, list.Count)];
	}

	public static string ConfirmPose(bool female)
	{
		List<string> list = ((!female) ? new string[2] { "NPC_Posing_M", "NPC_Emote_M" } : new string[2] { "NPC_Posing_F", "NPC_Emote_F" }).Where(Ok).ToList();
		if (list.Count <= 0)
		{
			return RandomVictory(female);
		}
		return list[Random.Range(0, list.Count)];
	}

	public static IEnumerable<string> DanceIds()
	{
		return Victory.Concat(from e in All
			where e.cat == "dance"
			select e.id).Distinct();
	}

	public static string RandomDance(bool? female = null)
	{
		List<string> list = (from e in All
			where e.cat == "dance" && Ok(e.id) && (!female.HasValue || FitsSex(e.id, female.Value))
			select e.id).ToList();
		if (list.Count <= 0)
		{
			return null;
		}
		return list[Random.Range(0, list.Count)];
	}

	public static string NameOf(string id)
	{
		return All.Find((Def e) => e.id == id)?.name ?? id;
	}

	public static List<string> Equipped()
	{
		return (string.IsNullOrEmpty(FBSession.emoteSlots) ? PICKS.ToList() : (from id in FBSession.emoteSlots.Split('|')
			where PICKS.Contains(id) || id.StartsWith("NPC_")
			select id).ToList()).Where((string id) => (bool)FBRegistry.I && (bool)FBRegistry.I.Clip(id)).ToList().Take(8)
			.ToList();
	}

	public static string RandomShort(bool? female = null)
	{
		List<Def> list = All.FindAll((Def e) => (e.cat == "pose" || e.cat == "fun") && e.len < 7f && Ok(e.id) && !NOSTAGE.IsMatch(e.id) && (!female.HasValue || FitsSex(e.id, female.Value)));
		if (list.Count <= 0)
		{
			return null;
		}
		return list[Random.Range(0, list.Count)].id;
	}
}
}
