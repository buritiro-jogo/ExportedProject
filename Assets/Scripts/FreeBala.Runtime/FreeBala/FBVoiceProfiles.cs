using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FreeBala
{
public class FBVoiceProfiles
{
	public readonly Dictionary<string, FBVoiceProfile> profiles = new Dictionary<string, FBVoiceProfile>();

	public readonly Dictionary<string, (string sex, float pitch)> chars = new Dictionary<string, (string, float)>();

	public readonly FBVoiceRule[] rules = new FBVoiceRule[Enum.GetValues(typeof(FBVoiceBank)).Length];

	private static readonly Regex FEM = new Regex("^(p_(bianca|brenda|bruna|clara|claudia|cristiana)|c_(mia|andreia|spec|clara|dezenove|renata|helena|adoida|fzera)\\d*)$");

	private static FBVoiceProfiles inst;

	public static FBVoiceProfiles I
	{
		get
		{
			if (inst == null)
			{
				TextAsset textAsset = Resources.Load<TextAsset>("Audio/Voice/voice_profiles");
				inst = Parse(textAsset ? textAsset.text : null);
			}
			return inst;
		}
	}

	public FBVoiceProfiles()
	{
		for (int i = 0; i < rules.Length; i++)
		{
			rules[i] = FBVoiceRule.Default((FBVoiceBank)i);
		}
	}

	public FBVoiceRule Rule(FBVoiceBank b)
	{
		return rules[(uint)b];
	}

	public static FBVoiceProfiles Parse(string json)
	{
		FBVoiceProfiles fBVoiceProfiles = new FBVoiceProfiles();
		Dictionary<string, object> o = (string.IsNullOrEmpty(json) ? null : (Json.Parse(json) as Dictionary<string, object>));
		Dictionary<string, object> dictionary = Json.O(o, "profiles");
		if (dictionary != null)
		{
			foreach (KeyValuePair<string, object> item in dictionary)
			{
				FBVoiceProfile fBVoiceProfile = new FBVoiceProfile
				{
					sex = item.Key
				};
				float[] array = Json.Floats(Json.L(item.Value, "pitch"));
				if (array.Length == 2)
				{
					fBVoiceProfile.pitch = new Vector2(array[0], array[1]);
				}
				Dictionary<string, object> dictionary2 = Json.O(item.Value, "banks");
				if (dictionary2 != null)
				{
					foreach (KeyValuePair<string, object> item2 in dictionary2)
					{
						if (Enum.TryParse<FBVoiceBank>(item2.Key, out var result) && item2.Value is List<object> list)
						{
							string[] array2 = new string[list.Count];
							for (int i = 0; i < array2.Length; i++)
							{
								array2[i] = list[i] as string;
							}
							fBVoiceProfile.banks[result] = array2;
						}
					}
				}
				fBVoiceProfiles.profiles[item.Key] = fBVoiceProfile;
			}
		}
		Dictionary<string, object> dictionary3 = Json.O(o, "rules");
		if (dictionary3 != null)
		{
			foreach (KeyValuePair<string, object> item3 in dictionary3)
			{
				if (Enum.TryParse<FBVoiceBank>(item3.Key, out var result2))
				{
					FBVoiceRule fBVoiceRule = fBVoiceProfiles.rules[(uint)result2];
					object value = item3.Value;
					fBVoiceProfiles.rules[(uint)result2] = new FBVoiceRule(Json.I(value, "prio", fBVoiceRule.prio), Json.F(value, "cd", fBVoiceRule.cd), Json.F(value, "vol", fBVoiceRule.vol), Json.F(value, "pitch", fBVoiceRule.pitch), Json.F(value, "refD", fBVoiceRule.refD), Json.F(value, "npcChance", fBVoiceRule.npcChance));
				}
			}
		}
		Dictionary<string, object> dictionary4 = Json.O(o, "chars");
		if (dictionary4 != null)
		{
			foreach (KeyValuePair<string, object> item4 in dictionary4)
			{
				fBVoiceProfiles.chars[item4.Key] = (Json.S(item4.Value, "sex"), Json.F(item4.Value, "pitch"));
			}
		}
		if (!fBVoiceProfiles.profiles.ContainsKey("m"))
		{
			fBVoiceProfiles.profiles["m"] = new FBVoiceProfile
			{
				sex = "m"
			};
		}
		if (!fBVoiceProfiles.profiles.ContainsKey("f"))
		{
			fBVoiceProfiles.profiles["f"] = new FBVoiceProfile
			{
				sex = "f"
			};
		}
		return fBVoiceProfiles;
	}

	private static string Base(string id)
	{
		if (!string.IsNullOrEmpty(id))
		{
			return id.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
		}
		return id;
	}

	public string SexOf(string skinId, string npcSex = null)
	{
		if (!string.IsNullOrEmpty(skinId))
		{
			if (chars.TryGetValue(skinId, out (string, float) value) && !string.IsNullOrEmpty(value.Item1))
			{
				if (!(value.Item1 == "f"))
				{
					return "m";
				}
				return "f";
			}
			string text = Base(skinId);
			if (text != skinId && chars.TryGetValue(text, out value) && !string.IsNullOrEmpty(value.Item1))
			{
				if (!(value.Item1 == "f"))
				{
					return "m";
				}
				return "f";
			}
			string text2 = GameData.Skins?.Find((SkinDef k) => k.id == skinId)?.sex;
			if (!string.IsNullOrEmpty(text2))
			{
				if (!(text2 == "f"))
				{
					return "m";
				}
				return "f";
			}
			if (FEM.IsMatch(skinId))
			{
				return "f";
			}
		}
		if (!(npcSex == "f"))
		{
			return "m";
		}
		return "f";
	}

	public FBVoiceProfile ProfileOf(string sex)
	{
		if (!profiles.TryGetValue((sex == "f") ? "f" : "m", out var value))
		{
			return profiles["m"];
		}
		return value;
	}

	public float PitchOf(string skinId, FBVoiceProfile prof)
	{
		if (!string.IsNullOrEmpty(skinId) && chars.TryGetValue(skinId, out (string, float) value) && value.Item2 > 0f)
		{
			return value.Item2;
		}
		return Mathf.Lerp(prof.pitch.x, prof.pitch.y, Hash01(skinId));
	}

	public static float Hash01(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return 0.5f;
		}
		uint num = 2166136261u;
		foreach (char c in s)
		{
			num ^= c;
			num *= 16777619;
		}
		return (float)(num % 10007) / 10006f;
	}
}
}
