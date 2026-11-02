using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FreeBala
{
public static class FBCustomizeData
{
	public enum Origin
	{
		All,
		Original,
		Npc,
		Fav
	}

	public class Entry
	{
		public SkinDef def;

		public string id;

		public string name;

		public string key;

		public bool fem;

		public bool npc;

		public int index;
	}

	public static readonly HashSet<string> BetaChars = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "r_pm_almeida", "r_perseu", "r_silva", "r_caboco", "r_marcin", "r_tutu" };

	private static List<Entry> roster;

	private static HashSet<string> favs;

	private static Dictionary<string, string> looks;

	public static List<Entry> Roster
	{
		get
		{
			if (roster != null)
			{
				return roster;
			}
			GameData.Load();
			List<Entry> all = new List<Entry>();
			HashSet<string> seen = new HashSet<string>();
			foreach (SkinDef skin in GameData.Skins)
			{
				Add(skin);
			}
			TextAsset textAsset = Resources.Load<TextAsset>("UI/Personagem/roster_extra");
			if ((bool)textAsset)
			{
				foreach (object item in Json.L(Json.Parse(textAsset.text), "skins") ?? new List<object>())
				{
					Add(new SkinDef
					{
						id = Json.S(item, "id"),
						name = Json.S(item, "name"),
						sex = Json.S(item, "sex", "m"),
						src = Json.S(item, "src", ""),
						team = -1
					});
				}
			}
			if ((bool)FBRegistry.I)
			{
				foreach (string charId in FBRegistry.I.charIds)
				{
					Add(new SkinDef
					{
						id = charId,
						name = charId,
						sex = "m",
						src = (charId.StartsWith("r_") ? "" : "npc"),
						team = -1
					});
				}
			}
			roster = all.Where((Entry e) => !e.npc).Concat(all.Where((Entry e) => e.npc)).ToList();
			for (int i = 0; i < roster.Count; i++)
			{
				roster[i].index = i;
			}
			return roster;
			void Add(SkinDef s)
			{
				if (s != null && s.id != null && BetaChars.Contains(s.id) && seen.Add(s.id) && (bool)FBRegistry.I && FBRegistry.I.HasChar(s.id))
				{
					string text = PrettyName(s.id, s.name);
					all.Add(new Entry
					{
						def = s,
						id = s.id,
						name = (string.IsNullOrEmpty(text) ? s.id : text),
						key = Norm(text ?? s.id) + " " + Norm(s.id),
						fem = (s.sex == "f"),
						npc = IsNpc(s)
					});
				}
			}
		}
	}

	public static HashSet<string> Favs
	{
		get
		{
			if (favs == null)
			{
				favs = new HashSet<string>(from x in PlayerPrefs.GetString("fb_favs", "").Split('|')
					where x.Length > 0
					select x);
			}
			return favs;
		}
	}

	private static Dictionary<string, string> Looks
	{
		get
		{
			if (looks != null)
			{
				return looks;
			}
			looks = new Dictionary<string, string>();
			string[] array = PlayerPrefs.GetString("fb_looks", "").Split(';');
			foreach (string text in array)
			{
				int num = text.IndexOf('=');
				if (num > 0)
				{
					looks[text.Substring(0, num)] = text.Substring(num + 1);
				}
			}
			if (!string.IsNullOrEmpty(FBSession.look) && !looks.ContainsKey(FBSession.skin))
			{
				looks[FBSession.skin] = FBSession.look;
			}
			return looks;
		}
	}

	public static string PrettyName(string id, string fallback = null)
	{
		return id switch
		{
			"r_pm_almeida" => "PM ALMEIDA", 
			"r_perseu" => "PERSEU", 
			"r_silva" => "SILVA", 
			"r_caboco" => "CABOCO", 
			"r_marcin" => "MARCIN", 
			"r_tutu" => "TUTU", 
			_ => fallback ?? id, 
		};
	}

	public static void Reset()
	{
		roster = null;
	}

	public static Entry Find(string id)
	{
		return Roster.Find((Entry e) => e.id == id);
	}

	private static bool IsNpc(SkinDef s)
	{
		if (!s.role && !s.id.StartsWith("n_") && !s.id.StartsWith("n2_") && !s.id.StartsWith("p_"))
		{
			return s.src == "npc";
		}
		return true;
	}

	public static string Norm(string t)
	{
		if (string.IsNullOrEmpty(t))
		{
			return "";
		}
		string text = t.Normalize(NormalizationForm.FormD);
		StringBuilder stringBuilder = new StringBuilder(text.Length);
		string text2 = text;
		foreach (char c in text2)
		{
			if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
			{
				stringBuilder.Append(char.ToLowerInvariant(c));
			}
		}
		return stringBuilder.ToString();
	}

	public static List<Entry> Filter(string sex, Origin origin, string query)
	{
		string q = Norm(query ?? "").Trim();
		HashSet<string> favs = Favs;
		return Roster.Where((Entry e) => Match(e, sex, origin, q, favs)).ToList();
	}

	public static bool Match(Entry e, string sex, Origin origin, string q, HashSet<string> favs)
	{
		if ((sex == "m" && e.fem) || (sex == "f" && !e.fem))
		{
			return false;
		}
		if ((origin == Origin.Original && e.npc) || (origin == Origin.Npc && !e.npc) || (origin == Origin.Fav && !favs.Contains(e.id)))
		{
			return false;
		}
		if (!string.IsNullOrEmpty(q))
		{
			return e.key.Contains(q);
		}
		return true;
	}

	public static bool IsFav(string id)
	{
		return Favs.Contains(id);
	}

	public static bool ToggleFav(string id)
	{
		bool num = !Favs.Remove(id);
		if (num)
		{
			Favs.Add(id);
		}
		PlayerPrefs.SetString("fb_favs", string.Join("|", Favs));
		PlayerPrefs.Save();
		return num;
	}

	public static string LookFor(string id)
	{
		if (id == null || !Looks.TryGetValue(id, out var value))
		{
			return "";
		}
		return value;
	}

	public static void SetLook(string id, string code)
	{
		if (string.IsNullOrEmpty(code))
		{
			Looks.Remove(id);
		}
		else
		{
			Looks[id] = code;
		}
		PlayerPrefs.SetString("fb_looks", string.Join(";", Looks.Select((KeyValuePair<string, string> kv) => kv.Key + "=" + kv.Value)));
		if (id == FBSession.skin)
		{
			FBSession.look = code ?? "";
			FBSession.Save();
		}
		else
		{
			PlayerPrefs.Save();
		}
	}

	public static void Use(string id)
	{
		FBSession.skin = id;
		FBSession.look = LookFor(id);
		FBSession.pickRandom = false;
		FBSession.Save();
	}

	public static void Reload()
	{
		favs = null;
		looks = null;
		FBSession.Load();
	}
}
}
