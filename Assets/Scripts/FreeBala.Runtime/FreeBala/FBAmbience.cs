using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
public static class FBAmbience
{
	private class P
	{
		public float birds;

		public float birds2;

		public float city;

		public float street;

		public float wind;

		public Dictionary<string, float> rate = new Dictionary<string, float>();
	}

	private static readonly Dictionary<string, P> PROFILES = new Dictionary<string, P>
	{
		["favela"] = new P
		{
			birds = 0.11f,
			birds2 = 0.05f,
			city = 0.035f,
			street = 0.02f,
			wind = 0.05f,
			rate = new Dictionary<string, float>
			{
				["dogs"] = 5f,
				["rooster"] = 1.2f,
				["kids"] = 1.4f,
				["pigeon"] = 1.2f,
				["plane"] = 0.35f,
				["sino"] = 0.2f,
				["sirene"] = 0.3f
			}
		},
		["centro"] = new P
		{
			birds = 0.06f,
			birds2 = 0.03f,
			city = 0.09f,
			street = 0.06f,
			wind = 0.045f,
			rate = new Dictionary<string, float>
			{
				["dogs"] = 2f,
				["rooster"] = 0.3f,
				["kids"] = 0.9f,
				["pigeon"] = 2f,
				["plane"] = 0.5f,
				["sino"] = 0.25f,
				["sirene"] = 0.45f
			}
		},
		["parque"] = new P
		{
			birds = 0.13f,
			birds2 = 0.08f,
			city = 0.04f,
			street = 0.03f,
			wind = 0.055f,
			rate = new Dictionary<string, float>
			{
				["dogs"] = 3f,
				["rooster"] = 0.4f,
				["kids"] = 2f,
				["pigeon"] = 1.5f,
				["plane"] = 0.4f,
				["sino"] = 0.2f,
				["sirene"] = 0.25f
			}
		},
		["estande"] = new P
		{
			birds = 0.07f,
			birds2 = 0.04f,
			city = 0.02f,
			street = 0f,
			wind = 0.07f,
			rate = new Dictionary<string, float>
			{
				["dogs"] = 1.2f,
				["rooster"] = 0.3f,
				["kids"] = 0f,
				["pigeon"] = 0.6f,
				["plane"] = 0.3f
			}
		}
	};

	private static readonly Dictionary<string, (string[] clips, Vector2 dist, Vector2 h, float vol, float refD)> SHOTS = new Dictionary<string, (string[], Vector2, Vector2, float, float)>
	{
		["dogs"] = ((from i in Enumerable.Range(0, 10)
			select "amb_dog" + i).ToArray(), new Vector2(18f, 75f), new Vector2(-2f, 2f), 0.9f, 9f),
		["rooster"] = (new string[2] { "amb_rooster0", "amb_rooster1" }, new Vector2(35f, 90f), new Vector2(0f, 8f), 0.8f, 14f),
		["kids"] = (new string[2] { "amb_kids0", "amb_kids1" }, new Vector2(28f, 65f), new Vector2(-1f, 4f), 0.55f, 10f),
		["pigeon"] = (new string[1] { "amb_pigeon0" }, new Vector2(5f, 16f), new Vector2(3f, 9f), 0.7f, 4f),
		["plane"] = (new string[1] { "amb_plane" }, new Vector2(60f, 160f), new Vector2(140f, 220f), 0.8f, 160f),
		["sirene"] = (new string[1] { "amb_sirene" }, new Vector2(160f, 300f), new Vector2(0f, 6f), 0.5f, 90f)
	};

	private static bool on;

	private static P prof;

	private static readonly Dictionary<string, float> next = new Dictionary<string, float>();

	private static float cover;

	private static float coverT;

	private static float cov;

	private static float hush;

	private static float baseY;

	private static Bounds bounds;

	public static void Start(string mapId)
	{
		prof = PROFILES[mapId.Contains("praca_aguas") ? "centro" : (mapId.Contains("praca_pt") ? "parque" : (mapId.Contains("estande") ? "estande" : "favela"))];
		on = true;
		hush = 0f;
		cover = 0f;
		baseY = (FBMatch.Player ? FBMatch.Player.Pos.y : 0f);
		MeshCollider[] array = UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None);
		if (array.Length != 0)
		{
			bounds = array[0].bounds;
			MeshCollider[] array2 = array;
			foreach (MeshCollider meshCollider in array2)
			{
				bounds.Encapsulate(meshCollider.bounds);
			}
		}
		foreach (string key in SHOTS.Keys)
		{
			next[key] = Gap(key) * UnityEngine.Random.Range(0.15f, 0.8f);
		}
		if (!UnityEngine.Object.FindAnyObjectByType<FBWeatherTicker>())
		{
			new GameObject("FB_Ambiente").AddComponent<FBWeatherTicker>();
		}
	}

	public static void Stop()
	{
		on = false;
		string[] array = new string[9] { "birds", "birds2", "city", "street", "wind", "whistle", "night", "rain", "people" };
		foreach (string text in array)
		{
			FBAudio.StopLoop("amb_" + text);
		}
	}

	public static void Shot()
	{
		if (on)
		{
			_ = hush;
			hush = Mathf.Min(1f, hush + 0.35f);
		}
	}

	private static float Gap(string s)
	{
		float value;
		float num = (prof.rate.TryGetValue(s, out value) ? value : 0f);
		if (!(num > 0f))
		{
			return 1E+09f;
		}
		return 60f / num * UnityEngine.Random.Range(0.5f, 1.5f);
	}

	public static void Tick(float dt)
	{
		if (!on || !Camera.main)
		{
			return;
		}
		Vector3 position = Camera.main.transform.position;
		if ((coverT -= dt) <= 0f)
		{
			coverT = 0.4f;
			cov = (FBWorld.RayHit(position + Vector3.up * 0.3f, Vector3.up, 14f) ? 1 : 0);
		}
		cover += (cov - cover) * Mathf.Min(1f, dt * 2.5f);
		if (Time.time - FBBotBrain.lastNoiseT < 0.1f)
		{
			Shot();
		}
		hush = Mathf.Max(0f, hush - dt / 14f);
		float num = 1f - cover * 0.65f;
		float num2 = Mathf.Clamp01((position.y - baseY - 2f) / 14f);
		bool night = FBWeather.Night;
		bool rain = FBWeather.Rain;
		float num3 = (1f - hush * 0.85f) * num * (night ? 0f : (rain ? 0.15f : 1f));
		FBAudio.SetLoop("amb_birds", "amb_bed_birds", prof.birds * num3);
		FBAudio.SetLoop("amb_birds2", "amb_bed_birds2", prof.birds2 * num3);
		FBAudio.SetLoop("amb_city", "amb_bed_city", prof.city * (1f - cover * 0.4f));
		FBAudio.SetLoop("amb_street", "amb_bed_street", prof.street * (1f - cover * 0.5f) * (1f - num2 * 0.5f));
		FBAudio.SetLoop("amb_wind", "amb_bed_wind", prof.wind * (1f + num2 * 2.2f) * (1f - cover * 0.8f));
		FBAudio.SetLoop("amb_whistle", "amb_bed_whistle", 0.05f * num2 * num2 * (1f - cover));
		FBAudio.SetLoop("amb_people", "amb_pessoas", ((prof == PROFILES["centro"] || prof == PROFILES["parque"]) && !night && !rain) ? (prof.city * 1.6f * (1f - hush * 0.8f) * num) : 0f);
		FBAudio.SetLoop("amb_night", "amb_bed_night", (night && !rain) ? (0.3f * (1f - cover * 0.6f)) : 0f);
		FBAudio.SetLoop("amb_rain", "amb_bed_rain", rain ? (0.55f * (1f - cover * 0.45f)) : 0f);
		foreach (string item in SHOTS.Keys.ToList())
		{
			if ((next[item] -= dt) > 0f)
			{
				continue;
			}
			next[item] = Gap(item);
			if (item == "pigeon" && cover > 0.5f)
			{
				continue;
			}
			switch (item)
			{
			case "kids":
			case "rooster":
			case "pigeon":
				if (hush > 0.3f || night || rain)
				{
					continue;
				}
				break;
			}
			if (item == "sino" && night)
			{
				continue;
			}
			if (item == "plane")
			{
				FBSkyPlanes.Flyover(position, baseY);
				continue;
			}
			(string[], Vector2, Vector2, float, float) tuple = SHOTS[item];
			Vector3 pos;
			switch (item)
			{
			case "dogs":
			case "kids":
			case "rooster":
			{
				int num5 = UnityEngine.Random.Range(0, 4);
				float num6 = UnityEngine.Random.Range(bounds.min.x + 5f, bounds.max.x - 5f);
				float num7 = UnityEngine.Random.Range(bounds.min.z + 5f, bounds.max.z - 5f);
				pos = new Vector3(num5 switch
				{
					1 => bounds.max.x - 5f, 
					0 => bounds.min.x + 5f, 
					_ => num6, 
				}, position.y + UnityEngine.Random.Range(tuple.Item3.x, tuple.Item3.y), num5 switch
				{
					3 => bounds.max.z - 5f, 
					2 => bounds.min.z + 5f, 
					_ => num7, 
				});
				break;
			}
			default:
			{
				float f = UnityEngine.Random.value * (float)Math.PI * 2f;
				float num4 = UnityEngine.Random.Range(tuple.Item2.x, tuple.Item2.y);
				pos = new Vector3(position.x + Mathf.Cos(f) * num4, position.y + UnityEngine.Random.Range(tuple.Item3.x, tuple.Item3.y), position.z + Mathf.Sin(f) * num4);
				break;
			}
			}
			FBAudio.PlayAt(tuple.Item1[UnityEngine.Random.Range(0, tuple.Item1.Length)], pos, tuple.Item4 * (1f - cover * 0.45f), tuple.Item5, 0.12f, 0f, 1f);
		}
	}
}
}
