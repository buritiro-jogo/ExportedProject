using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FreeBala
{
public static class FBCarSound
{
	public class Gearbox
	{
		public float vmax;

		public float rpm = 1000f;

		public float shiftT;

		public float idleT;

		public int gear = 1;

		public bool Shifting => shiftT > 0f;

		public Gearbox(float vmax)
		{
			this.vmax = Mathf.Max(5f, vmax);
		}

		public float GearTop(int g)
		{
			return vmax * RATIO[Mathf.Clamp(g, 1, 5) - 1];
		}

		public float RpmAt(float sp, int g)
		{
			return sp / GearTop(g) * 7000f;
		}

		public (float rpm, int gear, int shifted) Step(float dt, float v, float throttle)
		{
			float num = Mathf.Abs(v);
			int item = 0;
			if (v < -0.5f)
			{
				gear = 1;
			}
			throttle = Mathf.Clamp01(throttle);
			float num2 = RpmAt(num, gear);
			if (num < 1.2f)
			{
				idleT += dt;
				num2 = Mathf.Max(num2, 1000f * (1f + 0.025f * Mathf.Sin(idleT * 5.3f)) + throttle * 7000f * 0.42f);
			}
			if (shiftT > 0f)
			{
				shiftT -= dt;
				num2 = Mathf.Min(num2, rpm);
			}
			num2 = Mathf.Clamp(num2, 1000f, 7000f);
			float b = ((shiftT > 0f) ? 0.07f : ((num2 > rpm) ? (0.1f * (1.4f - throttle * 0.6f)) : 0.16000001f));
			rpm += (num2 - rpm) * (1f - Mathf.Exp((0f - dt) / Mathf.Max(0.02f, b)));
			if (shiftT <= 0f && v > 0.5f)
			{
				float num3 = Mathf.Lerp(2600f, 6900f, Mathf.Pow(throttle, 1.5f));
				float num4 = Mathf.Lerp(1700f, 3200f, throttle);
				if (gear < 5 && throttle > 0.05f && rpm >= num3 && RpmAt(num, gear + 1) > 1500f)
				{
					gear++;
					item = 1;
					shiftT = 0.35f;
				}
				else if (gear > 1 && RpmAt(num, gear) < num4 && RpmAt(num, gear - 1) < 6210f)
				{
					gear--;
					item = -1;
					shiftT = 0.21000001f;
				}
			}
			if (rpm > 6895f && throttle > 0f)
			{
				rpm -= 210f * UnityEngine.Random.value;
			}
			return (rpm: rpm, gear: gear, shifted: item);
		}
	}

	public const float MIN_RPM = 1000f;

	public const float MAX_RPM = 7000f;

	public const float UP_RPM = 6900f;

	public const float DOWN_RPM = 3000f;

	public const float SHIFT_DELAY = 0.35f;

	public const float INERTIA = 0.1f;

	public const int GEARS = 5;

	public static readonly string[] LAYERS = new string[6] { "idle", "low_on", "med_on", "high_on", "very_high_on", "maxRPM" };

	private static readonly Dictionary<string, string[]> P = new Dictionary<string, string[]>
	{
		["boxer"] = new string[2] { "Fusca", "Kombi" },
		["i4vw"] = new string[13]
		{
			"GolG2", "GolQuadrado", "GolG5", "Up", "SaveiroG4", "SaveiroQuadrada", "SaveiroCross", "Santana2002", "ParatiSurf1994", "CrossFox",
			"Golf", "Jetta2012", "Nivus"
		},
		["i4sport"] = new string[4] { "CivicLXR_2014", "Corolla2018", "CivicG10", "i30" },
		["i6"] = new string[2] { "OmegaCD1997", "OpalaDiplomata85" },
		["diesel"] = new string[8] { "S10_2017", "Hilux2023Conquest", "L200", "F250", "RAM3500", "Amarok", "Sw4", "Toro" }
	};

	public static readonly Dictionary<string, string> HORN = new Dictionary<string, string>
	{
		["boxer"] = "reb_horn_fusca",
		["diesel"] = "reb_horn_caminhonetes",
		["i6"] = "reb_horn_chevette",
		["i4"] = "reb_horn_palio",
		["i4vw"] = "reb_horn_gol_saveiro",
		["i4sport"] = "reb_horn_gol_saveiro"
	};

	private static readonly string[] H_CARRO = new string[7] { "reb_horn_gol_saveiro", "reb_horn_palio", "horn_sedan", "horn_sedan_grave", "horn_palio_grave", "horn_gol_agudo", "reb_horn_chevette" };

	private static readonly string[] H_VELHO = new string[3] { "reb_horn_fusca", "reb_horn_chevette", "horn_palio_grave" };

	private static readonly string[] H_PICAPE = new string[4] { "reb_horn_caminhonetes", "reb_horn_caminhonetesv2", "horn_caminhonete_grave", "horn_sedan_grave" };

	private static readonly string[] H_GRANDE = new string[3] { "horn_navio", "horn_caminhonete_grave", "reb_horn_caminhonetesv2" };

	private static readonly string[] H_POLICIA = new string[3] { "horn_policia1", "horn_policia2", "horn_policia3" };

	private static readonly string[] H_MOTO = new string[3] { "horn_moto", "horn_moto_agudo", "horn_moto_grave" };

	public static readonly HashSet<string> TURBO = new HashSet<string> { "i4sport", "i4_Evo_Sport", "i4_240X_Sport", "i6_GTR", "V6_GTR", "i6_2JX_Sport", "i4_Ricer_1" };

	private static readonly float[] RATIO = new float[5] { 0.3f, 0.5f, 0.69f, 0.87f, 1.06f };

	private static float MaxLimit => Json.F(GameData.RES, "maxRPMLimit", 7000f);

	public static string HornFor(FBCar u)
	{
		if (!u)
		{
			return "reb_horn_palio";
		}
		if (u.horn != null)
		{
			return u.horn;
		}
		string text = u.id ?? "";
		string[] array = ((u.kind == "moto") ? H_MOTO : (text.Contains("Viatura") ? H_POLICIA : ((text == "F250" || text == "RAM3500") ? H_GRANDE : (Regex.IsMatch(text, "Fusca|Kombi|Chevet|Opala|Brasilia|Variant|Kadett") ? H_VELHO : ((u.heavy || Array.IndexOf(P["diesel"], text) >= 0) ? H_PICAPE : H_CARRO)))));
		uint num = 2166136261u;
		string text2 = u.syncId + "|" + text;
		foreach (char c in text2)
		{
			num = (num ^ c) * 16777619;
		}
		string[] array2 = Array.FindAll(array, FBAudio.Has);
		return u.horn = ((array2.Length != 0) ? array2[num % (uint)array2.Length] : "reb_horn_palio");
	}

	public static void Honk(FBCar u, float aggr = 0.5f)
	{
		if (!u)
		{
			return;
		}
		AudioClip audioClip = FBAudio.Clip(HornFor(u));
		if (!audioClip)
		{
			return;
		}
		float num = UnityEngine.Random.value + aggr * 0.5f;
		(float, float)[] obj = ((!(num < 0.55f)) ? ((!(num < 1.05f)) ? new(float, float)[1] { (0f, 0.75f + aggr * 0.5f) } : new(float, float)[2]
		{
			(0f, 0.14f),
			(0.24f, 0.16f)
		}) : new(float, float)[1] { (0f, 0.16f) });
		double num2 = AudioSettings.dspTime + 0.02;
		(float, float)[] array = obj;
		for (int i = 0; i < array.Length; i++)
		{
			(float, float) tuple = array[i];
			float item = tuple.Item1;
			float item2 = tuple.Item2;
			AudioSource audioSource = FBAudio.PlayAt(audioClip, u.transform.position + Vector3.up, 0.85f, 10f, 0.02f);
			if ((bool)audioSource)
			{
				audioSource.Stop();
				audioSource.loop = true;
				audioSource.PlayScheduled(num2 + (double)item);
				audioSource.SetScheduledEndTime(num2 + (double)item + (double)item2);
			}
		}
	}

	public static bool Sporty(string prof)
	{
		if (prof != null)
		{
			return Regex.IsMatch(prof, "^(v8|i4sport)$|V8|v8_|V10|v10|V12|v12|Rotary|rotary|Evo|GTR|2JX|240X|Ricer|Flat6");
		}
		return false;
	}

	public static string ProfileFor(string id, FBCar u)
	{
		if (u != null && u.ie)
		{
			if (!(u.kind == "moto"))
			{
				return "diesel";
			}
			return null;
		}
		string text = FBReb.Ronco(id);
		if (text != null)
		{
			return text;
		}
		foreach (KeyValuePair<string, string[]> item in P)
		{
			if (Array.IndexOf(item.Value, id) >= 0)
			{
				return item.Key;
			}
		}
		if (!(u != null) || !u.heavy)
		{
			return "i4";
		}
		return "diesel";
	}

	public static float EvalCurve(List<object> K, float t)
	{
		if (K == null || K.Count == 0)
		{
			return 0f;
		}
		float[] array = A(0);
		if (t <= array[0])
		{
			return array[1];
		}
		float[] array2 = A(K.Count - 1);
		if (t >= array2[0])
		{
			return array2[1];
		}
		for (int j = 0; j < K.Count - 1; j++)
		{
			float[] array3 = A(j);
			float[] array4 = A(j + 1);
			if (!(t > array4[0]))
			{
				float num = array4[0] - array3[0];
				if (num <= 0f)
				{
					return array4[1];
				}
				float num2 = (t - array3[0]) / num;
				float num3 = num2 * num2;
				float num4 = num3 * num2;
				float num5 = (float.IsFinite(array3[3]) ? (array3[3] * num) : 0f);
				float num6 = (float.IsFinite(array4[2]) ? (array4[2] * num) : 0f);
				return (2f * num4 - 3f * num3 + 1f) * array3[1] + (num4 - 2f * num3 + num2) * num5 + (-2f * num4 + 3f * num3) * array4[1] + (num4 - num3) * num6;
			}
		}
		return array2[1];
		float[] A(int i)
		{
			List<object> list = K[i] as List<object>;
			return new float[4]
			{
				Json.Fv(list[0]),
				Json.Fv(list[1]),
				Json.Fv(list[2]),
				Json.Fv(list[3])
			};
		}
	}

	public static void EngineAudio(string key, string profile, float rpm, float throttle, Vector3 pos, float vol = 0.42f, float refD = 4f, float rolloff = 0.8f, bool turbo = false)
	{
		Dictionary<string, object> dictionary = Json.O(Json.O(GameData.RES, "profiles"), profile) ?? Json.O(Json.O(GameData.RES, "profiles"), "i4");
		if (dictionary == null)
		{
			return;
		}
		float t = Mathf.Min(1f, rpm / MaxLimit);
		float num = vol * (0.6f + 0.4f * Mathf.Min(1f, throttle));
		string[] lAYERS = LAYERS;
		foreach (string text in lAYERS)
		{
			List<object> list = Json.L(dictionary, text);
			if (list != null)
			{
				float num2 = Mathf.Clamp(EvalCurve(list[0] as List<object>, t), 0f, 1.2f);
				float value = Mathf.Max(0.05f, EvalCurve(list[1] as List<object>, t));
				FBAudio.SetLoop(key + text, "reb_eng_" + profile + "_" + text, num2 * num, pos, value, refD, rolloff);
			}
		}
		if (TURBO.Contains(profile) || turbo)
		{
			Dictionary<string, object> o = Json.O(GameData.RES, "turbo");
			FBAudio.SetLoop(key + "turbo", "reb_turbo_loop", EvalCurve(Json.L(o, "loopVol"), t) * Mathf.Min(1f, throttle) * vol, pos, EvalCurve(Json.L(o, "loopPitch"), t), refD, rolloff);
		}
	}

	public static void StopEngine(string key)
	{
		string[] lAYERS = LAYERS;
		foreach (string text in lAYERS)
		{
			FBAudio.StopLoop(key + text);
		}
		FBAudio.StopLoop(key + "turbo");
	}

	public static void TurboBlow(string profile, float rpm, Vector3 pos, bool turbo = false)
	{
		if (!TURBO.Contains(profile ?? "") && !turbo)
		{
			return;
		}
		Dictionary<string, object> o = Json.O(GameData.RES, "turbo");
		float num = rpm / MaxLimit;
		float num2 = EvalCurve(Json.L(o, "blowVol"), num);
		if (num2 > 0.05f)
		{
			AudioSource audioSource = FBAudio.PlayAt((num > 0.75f) ? "reb_turbo_blow_long" : "reb_turbo_blow_short", pos, 0.5f * num2);
			if ((bool)audioSource)
			{
				audioSource.pitch = EvalCurve(Json.L(o, "blowPitch"), num);
			}
		}
	}
}
}
