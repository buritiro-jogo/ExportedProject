using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FreeBala
{
public static class FBSession
{
	public const string DefaultSkin = "r_tutu";

	public static string skin = "r_tutu";

	public static string look = "";

	public static string map = "vila_jardim";

	public static string mode = "classico";

	public static string primary = "ak";

	public static string smg = "p90";

	public static string secondary = "38";

	public static string name = "JOGADOR";

	public static int team;

	public static int diff = 1;

	public static bool online;

	public static string server = "ws://localhost:8123/ws";

	public static string room;

	public static readonly bool Lab = Array.Exists(Environment.GetCommandLineArgs(), (string x) => x.StartsWith("-fb") && x.EndsWith("lab"));

	public static string view = "tps";

	public static string aimAssist = "assist";

	public static string victory;

	public static float fov = 0.375f;

	public static float sens = 0.5f;

	public static float adsSens = 0.5f;

	public static float master = 0.9f;

	public static float music = 0.5f;

	public static bool holdRun;

	public static bool invertY;

	public static bool aimToggle;

	public static bool blood = true;

	public static bool pickRandom;

	public static bool freeLookToggle;

	public static int money = 2000;

	public static int xp;

	public static int passXp;

	public static int quality = -1;

	public static string emoteSlots = "";

	private static readonly Dictionary<string, (string time, string weather)> wx = new Dictionary<string, (string, string)>();

	public static bool nameChosen
	{
		get
		{
			if (PlayerPrefs.GetInt("fb_nameChosen", 0) == 1 && !string.IsNullOrWhiteSpace(name))
			{
				return name != "JOGADOR";
			}
			return false;
		}
	}

	public static string FullSkin
	{
		get
		{
			if (!string.IsNullOrEmpty(look))
			{
				return skin + "~" + look;
			}
			return skin;
		}
	}

	public static string CleanName(string s, out string why)
	{
		why = null;
		s = (s ?? "").Trim();
		StringBuilder stringBuilder = new StringBuilder();
		string text = s;
		foreach (char c in text)
		{
			if (char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '-' || c == ' ')
			{
				stringBuilder.Append(c);
			}
		}
		string text2 = Regex.Replace(stringBuilder.ToString(), " +", " ").Trim();
		if (text2.Length < 3)
		{
			why = "USE PELO MENOS 3 LETRAS OU NÚMEROS";
			return null;
		}
		if (text2.Length > 16)
		{
			text2 = text2.Substring(0, 16).Trim();
		}
		if (text2 != s.Trim())
		{
			why = "SÍMBOLOS TIRADOS (SÓ LETRAS, NÚMEROS, _ . -)";
		}
		return text2;
	}

	public static void SetName(string n)
	{
		name = n;
		PlayerPrefs.SetInt("fb_nameChosen", 1);
		Save();
	}

	public static (string time, string weather) WeatherFor(string mapId)
	{
		if (!wx.TryGetValue(mapId, out (string, string) value))
		{
			return (time: "dia", weather: "limpo");
		}
		return value;
	}

	public static void SetWeather(string mapId, string time, string weather)
	{
		wx[mapId] = (time, weather);
		Save();
	}

	public static void Save()
	{
		(string, string)[] array = new(string, string)[13]
		{
			("skin", skin),
			("look", look),
			("map", map),
			("mode", mode),
			("primary", primary),
			("smg", smg),
			("secondary", secondary),
			("name", name),
			("server", server),
			("view", view),
			("aimAssist", aimAssist),
			("victory", victory ?? ""),
			("emotes", emoteSlots)
		};
		for (int i = 0; i < array.Length; i++)
		{
			var (text, value) = array[i];
			PlayerPrefs.SetString("fb_" + text, value);
		}
		PlayerPrefs.SetInt("fb_diff", diff);
		PlayerPrefs.SetInt("fb_money", money);
		PlayerPrefs.SetInt("fb_xp", xp);
		PlayerPrefs.SetInt("fb_passXp", passXp);
		PlayerPrefs.SetInt("fb_quality", quality);
		PlayerPrefs.SetFloat("fb_fov", fov);
		PlayerPrefs.SetFloat("fb_sens", sens);
		PlayerPrefs.SetFloat("fb_adsSens", adsSens);
		PlayerPrefs.SetFloat("fb_master", master);
		PlayerPrefs.SetFloat("fb_music", music);
		PlayerPrefs.SetInt("fb_holdRun", holdRun ? 1 : 0);
		PlayerPrefs.SetInt("fb_invertY", invertY ? 1 : 0);
		PlayerPrefs.SetInt("fb_aimToggle", aimToggle ? 1 : 0);
		PlayerPrefs.SetInt("fb_blood", blood ? 1 : 0);
		PlayerPrefs.SetInt("fb_pickRandom", pickRandom ? 1 : 0);
		PlayerPrefs.SetInt("fb_freeLookToggle", freeLookToggle ? 1 : 0);
		StringBuilder stringBuilder = new StringBuilder();
		foreach (KeyValuePair<string, (string, string)> item in wx)
		{
			stringBuilder.Append(item.Key).Append('=').Append(item.Value.Item1)
				.Append(',')
				.Append(item.Value.Item2)
				.Append(';');
		}
		PlayerPrefs.SetString("fb_wx", stringBuilder.ToString());
		PlayerPrefs.Save();
		Apply();
	}

	public static void Load()
	{
		skin = G("skin", skin);
		look = G("look", look);
		map = G("map", map);
		mode = G("mode", mode);
		primary = G("primary", primary);
		smg = G("smg", smg);
		secondary = G("secondary", secondary);
		name = G("name", name);
		server = G("server", server);
		view = "tps";
		aimAssist = G("aimAssist", aimAssist);
		victory = G("victory", "");
		if (victory == "")
		{
			victory = null;
		}
		emoteSlots = G("emotes", "");
		diff = PlayerPrefs.GetInt("fb_diff", diff);
		money = PlayerPrefs.GetInt("fb_money", money);
		xp = PlayerPrefs.GetInt("fb_xp", xp);
		passXp = PlayerPrefs.GetInt("fb_passXp", passXp);
		quality = PlayerPrefs.GetInt("fb_quality", quality);
		fov = PlayerPrefs.GetFloat("fb_fov", fov);
		sens = PlayerPrefs.GetFloat("fb_sens", sens);
		adsSens = PlayerPrefs.GetFloat("fb_adsSens", adsSens);
		master = PlayerPrefs.GetFloat("fb_master", master);
		music = PlayerPrefs.GetFloat("fb_music", music);
		holdRun = PlayerPrefs.GetInt("fb_holdRun", 0) == 1;
		invertY = PlayerPrefs.GetInt("fb_invertY", 0) == 1;
		aimToggle = PlayerPrefs.GetInt("fb_aimToggle", 0) == 1;
		blood = PlayerPrefs.GetInt("fb_blood", 1) == 1;
		pickRandom = PlayerPrefs.GetInt("fb_pickRandom", 0) == 1;
		freeLookToggle = PlayerPrefs.GetInt("fb_freeLookToggle", 0) == 1;
		if ((bool)FBRegistry.I && !FBRegistry.I.HasChar(skin))
		{
			skin = (FBRegistry.I.HasChar("r_tutu") ? "r_tutu" : skin);
			look = "";
		}
		if (!FBCustomizeData.BetaChars.Contains(skin))
		{
			skin = "r_tutu";
			look = "";
		}
		if (mode == "4v4")
		{
			mode = "squad";
		}
		if (mode != "role" && mode != "classico" && mode != "squad" && mode != "10x10")
		{
			mode = "classico";
		}
		// mapas liberados em GameData.AllowedMaps, desde que a cena esteja no build (orla só entra quando a cena existir)
		if (!GameData.AllowedMaps.Contains(map) || !Application.CanStreamedLevelBeLoaded("Map_" + map))
		{
			map = "vila_jardim";
		}
		if (GameData.BannedWeapons.Contains(primary))
		{
			primary = "ak";
		}
		if (GameData.BannedWeapons.Contains(smg))
		{
			smg = "p90";
		}
		if (GameData.BannedWeapons.Contains(secondary))
		{
			secondary = "38";
		}
		wx.Clear();
		string[] array = G("wx", "").Split(';');
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split('=');
			if (array2.Length == 2)
			{
				string[] array3 = array2[1].Split(',');
				if (array3.Length == 2)
				{
					wx[array2[0]] = (array3[0], array3[1]);
				}
			}
		}
		Apply();
		static string G(string k, string d)
		{
			return PlayerPrefs.GetString("fb_" + k, d);
		}
	}

	private static void Apply()
	{
		FBInput.Sens = sens;
		FBInput.AdsSens = adsSens;
		FBInput.InvertY = invertY;
		FBInput.AimToggle = aimToggle;
		FBAudio.Master = master;
		FBAudio.MusicVol = music;
		FBFx.BloodOn = blood;
		if (quality >= 0 && quality < QualitySettings.names.Length && QualitySettings.GetQualityLevel() != quality)
		{
			QualitySettings.SetQualityLevel(quality, applyExpensiveChanges: true);
		}
	}
}
}
