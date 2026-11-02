using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
public static class FBNpcRotina
{
	public class Act
	{
		public string id;

		public string walk;

		public float rs;

		public string[] idle;

		public bool fem;

		public string voice;
	}

	private class Roda
	{
		public List<FBCharacter> m = new List<FBCharacter>();

		public int spk;

		public int step;

		public string topic;

		public float nextT;

		public float seen;
	}

	public static bool On = Array.IndexOf(Environment.GetCommandLineArgs(), "-fbsemrotina171") < 0;

	public static int Pauses;

	public static int Reacts;

	public static int Convs;

	private static readonly Act[] ACT = new Act[7]
	{
		new Act
		{
			id = "celular",
			walk = "X171_Walking_Texting_looped",
			rs = 1.11f,
			idle = new string[2] { "X171_texting_looped", "X171_051_Mocap101_SL_STATIC_B_WriteMobile_UE4" },
			fem = true
		},
		new Act
		{
			id = "telefone",
			walk = "X171_023_Mocap101_SL_WALK_B_TalkMobile_UE4",
			rs = 0.76f,
			idle = new string[1] { "X171_051_Mocap101_SL_STATIC_B_WriteMobile_UE4" },
			fem = true,
			voice = "celular"
		},
		new Act
		{
			id = "procura",
			walk = "X171_022_Mocap101_SL_WALK_B_SearchMobile_UE4",
			rs = 0.54f,
			idle = new string[1] { "X171_texting_looped" },
			fem = true
		},
		new Act
		{
			id = "cigarro",
			walk = "X171_Walk_Fumando_looped",
			rs = 0.96f,
			idle = new string[2] { "X171_fumando_looped", "X171_055_Mocap101_SL_STATIC_B_Smoke_UE41" }
		},
		new Act
		{
			id = "cigarro2",
			walk = "X171_037_Mocap101_SL_WALK_B_Smoke_UE4",
			rs = 0.95f,
			idle = new string[2] { "X171_fumando_looped", "X171_055_Mocap101_SL_STATIC_B_Smoke_UE41" }
		},
		new Act
		{
			id = "bebida",
			walk = "X171_Bebendo_looped",
			rs = 0.96f,
			idle = new string[1] { "X171_004_Mocap101_SL_IDLE_B_Armscross_UE4" }
		},
		new Act
		{
			id = "olhando",
			walk = "X171_Walk_06_Look_Around_Loop_IP",
			rs = 1.22f,
			idle = new string[2] { "X171_053_Mocap101_SL_STATIC_B_Watch1_UE4", "X171_004_Mocap101_SL_IDLE_B_Armscross_UE4" },
			fem = true
		}
	};

	private static readonly string[] PAUSE = new string[5] { "X171_053_Mocap101_SL_STATIC_B_Watch1_UE4", "X171_054_Mocap101_SL_STATIC_B_Watch2_UE4", "X171_051_Mocap101_SL_STATIC_B_WriteMobile_UE4", "X171_texting_looped", "X171_004_Mocap101_SL_IDLE_B_Armscross_UE4" };

	private static readonly string[] PAUSE_M = new string[2] { "X171_fumando_looped", "X171_055_Mocap101_SL_STATIC_B_Smoke_UE41" };

	public const string TALK = "X171_Convo_02_Up_Beat_Loop";

	public const string TALK2 = "X171_Convo_01_Low_Key_Loop";

	public const string LISTEN = "X171_Convo_11_Listening_Loop";

	private static readonly Dictionary<int, Act> actOf = new Dictionary<int, Act>();

	private static readonly Dictionary<string, string[]> CLS_IDLE = new Dictionary<string, string[]>
	{
		["gari"] = new string[1] { "X171_ObraDeRua_Varrendo" },
		["pedreiro"] = new string[3] { "X171_ObraDeRua_Medindo", "X171_ObraDeRua_VendoPlanos", "X171_004_Mocap101_SL_IDLE_B_Armscross_UE4" },
		["eletricista"] = new string[2] { "X171_ObraDeRua_VendoPlanos", "X171_053_Mocap101_SL_STATIC_B_Watch1_UE4" },
		["comerciante"] = new string[2] { "X171_004_Mocap101_SL_IDLE_B_Armscross_UE4", "X171_051_Mocap101_SL_STATIC_B_WriteMobile_UE4" },
		["servico"] = new string[2] { "X171_004_Mocap101_SL_IDLE_B_Armscross_UE4", "X171_053_Mocap101_SL_STATIC_B_Watch1_UE4" },
		["motoboy"] = new string[2] { "X171_051_Mocap101_SL_STATIC_B_WriteMobile_UE4", "X171_053_Mocap101_SL_STATIC_B_Watch1_UE4" },
		["criminoso"] = new string[3] { "X171_055_Mocap101_SL_STATIC_B_Smoke_UE41", "X171_fumando_looped", "X171_004_Mocap101_SL_IDLE_B_Armscross_UE4" }
	};

	private static readonly Dictionary<long, Roda> rodas = new Dictionary<long, Roda>();

	private static readonly string[] TOPICS = new string[3] { "algo", "alguem", "lugar" };

	private static readonly string[] STEPS = new string[3] { "com", "resp", "fim" };

	private static float grauT;

	private static bool Has(string n)
	{
		if ((bool)FBRegistry.I)
		{
			return FBRegistry.I.Clip(n);
		}
		return false;
	}

	public static Act ActOf(FBCharacter c)
	{
		if (!c || !actOf.TryGetValue(FBNpcVoz.Id(c), out var value))
		{
			return null;
		}
		return value;
	}

	public static void Decorate(FBCharacter.NpcSet set, FBCharacter c)
	{
		if (!On || set == null || !c)
		{
			return;
		}
		actOf.Remove(FBNpcVoz.Id(c));
		if (!(UnityEngine.Random.value > 0.35f))
		{
			bool f = set.sex == "f";
			List<Act> list = ACT.Where((Act a) => (a.fem || !f) && Has(a.walk) && a.idle.Any(Has)).ToList();
			if (list.Count != 0)
			{
				Act act = list[UnityEngine.Random.Range(0, list.Count)];
				actOf[FBNpcVoz.Id(c)] = act;
				set.walk = act.walk;
				set.ovr["WalkFwdLoop"] = act.walk;
				set.walkRs = act.rs;
				set.activity = true;
			}
		}
	}

	public static string ClsOf(FBCharacter c)
	{
		if (!c)
		{
			return null;
		}
		return GameData.Skins.Find((SkinDef s) => s.id == c.skinId)?.cls;
	}

	public static float Pause(FBCharacter c)
	{
		if (!On || !c || c.dance != null || UnityEngine.Random.value > 0.38f)
		{
			return 0f;
		}
		Act act = ActOf(c);
		IEnumerable<string> source;
		if (act == null)
		{
			source = PAUSE.Concat((c.npcSet?.sex == "f") ? new string[0] : PAUSE_M);
		}
		else
		{
			IEnumerable<string> idle = act.idle;
			source = idle;
		}
		List<string> list = source.Where(Has).ToList();
		string text = ClsOf(c);
		if (text != null && CLS_IDLE.TryGetValue(text, out var value) && UnityEngine.Random.value < 0.7f)
		{
			List<string> list2 = value.Where(Has).ToList();
			if (list2.Count > 0)
			{
				list = list2;
			}
		}
		if (list.Count == 0)
		{
			return 0f;
		}
		string clip = list[UnityEngine.Random.Range(0, list.Count)];
		if (!c.PlayDance(clip))
		{
			return 0f;
		}
		float num = UnityEngine.Random.Range(6f, 16f);
		c.dance.len = num + 1f;
		Pauses++;
		if (act != null && act.voice != null && UnityEngine.Random.value < 0.6f)
		{
			FBNpcVoz.Say(c, act.voice, 0.7f, 2.6f, 6f, 0f, UnityEngine.Random.Range(0.5f, 2.5f));
		}
		return num;
	}

	public static void WalkTick(FBCharacter c, float dt)
	{
		Act act = ActOf(c);
		if (act != null && act.voice != null && !(UnityEngine.Random.value > dt / 14f))
		{
			FBNpcVoz.Say(c, act.voice, 0.65f, 2.4f, 10f, 0f);
		}
	}

	private static long Key(Vector3 p)
	{
		return ((long)Mathf.RoundToInt(p.x * 4f) << 32) ^ (uint)Mathf.RoundToInt(p.z * 4f);
	}

	public static void TalkTick(FBCharacter c, Vector3 center, float dt)
	{
		if (!On || !c)
		{
			return;
		}
		long key = Key(center);
		if (!rodas.TryGetValue(key, out var value))
		{
			value = new Roda
			{
				nextT = Time.time + UnityEngine.Random.Range(0.8f, 2.5f),
				topic = TOPICS[UnityEngine.Random.Range(0, 3)],
				spk = UnityEngine.Random.Range(0, 2)
			};
			rodas[key] = value;
			Convs++;
		}
		if (!value.m.Contains(c))
		{
			value.m.Add(c);
		}
		value.m.RemoveAll((FBCharacter x) => !x || !x.alive || !x.talking);
		value.seen = Time.time;
		if (value.m.IndexOf(c) != 0 || Time.time < value.nextT || value.m.Count < 2)
		{
			return;
		}
		FBCharacter fBCharacter = value.m[value.spk % value.m.Count];
		float b = FBNpcVoz.Say(fBCharacter, "conv_" + STEPS[value.step] + "_" + value.topic, 0.6f, 2.2f, 0f, 0f);
		foreach (FBCharacter item in value.m)
		{
			Gesture(item, item == fBCharacter);
		}
		value.nextT = Time.time + Mathf.Max(1.2f, b) + UnityEngine.Random.Range(0.35f, 0.9f);
		if (++value.step >= 3)
		{
			value.step = 0;
			value.topic = TOPICS[UnityEngine.Random.Range(0, 3)];
			value.nextT += UnityEngine.Random.Range(3f, 8f);
			value.spk++;
		}
		else
		{
			value.spk = ((value.step == 1) ? (value.spk + 1) : (value.spk - 1));
		}
		if (rodas.Count <= 64)
		{
			return;
		}
		foreach (long item2 in (from kv in rodas
			where Time.time - kv.Value.seen > 5f
			select kv.Key).ToList())
		{
			rodas.Remove(item2);
		}
	}

	private static void Gesture(FBCharacter c, bool speaking)
	{
		string text = ((!speaking) ? "X171_Convo_11_Listening_Loop" : ((UnityEngine.Random.value < 0.6f) ? "X171_Convo_02_Up_Beat_Loop" : "X171_Convo_01_Low_Key_Loop"));
		if (Has(text) && (c.dance == null || (!(c.dance.clip == text) && (!speaking || (!(c.dance.clip == "X171_Convo_02_Up_Beat_Loop") && !(c.dance.clip == "X171_Convo_01_Low_Key_Loop"))))) && (c.dance == null || !c.dance.clip.Contains("Handshake")) && c.PlayDance(text))
		{
			c.dance.len = 1000000f;
		}
	}

	public static bool Moves(string clip)
	{
		if (clip != null)
		{
			if (!clip.Contains("SCR_Stand_"))
			{
				return clip.Contains("Tropeco");
			}
			return true;
		}
		return false;
	}

	public static float OneShot(FBCharacter c, string clip, float maxLen = 99f)
	{
		if (!Has(clip) || !c.PlayDance(clip))
		{
			return 0f;
		}
		float num = Mathf.Min(maxLen, FBRegistry.I.Clip(clip).length);
		c.dance.len = num;
		Reacts++;
		if (!Moves(clip))
		{
			return num;
		}
		return num - 0.3f;
	}

	public static string Side(FBCharacter c, Vector3 from)
	{
		Vector3 to = (from - c.Pos).Flat();
		float num = Vector3.SignedAngle(Quaternion.Euler(0f, c.Yaw, 0f) * Vector3.forward, to, Vector3.up);
		if (!(Mathf.Abs(num) < 50f))
		{
			if (!(Mathf.Abs(num) > 130f))
			{
				if (!(num > 0f))
				{
					return "Left";
				}
				return "Right";
			}
			return "Back";
		}
		return "Front";
	}

	public static float Bump(FBCharacter c, Vector3 from)
	{
		string text = Side(c, from);
		if (text == "Back")
		{
			text = ((UnityEngine.Random.value < 0.5f) ? "Left" : "Right");
		}
		float result = OneShot(c, "X171_BumpReaction_HitFrom" + text + "_LightStandingStumble");
		FBNpcVoz.Say(c, "veiculo", 0.9f, 3.5f, 6f, 0.8f, 0.35f);
		return result;
	}

	public static float NearMiss(FBCharacter c, Vector3 carP, float passDist)
	{
		float num = 0f;
		if (passDist < 1.6f && UnityEngine.Random.value < 0.45f)
		{
			string text = Side(c, carP);
			num = OneShot(c, "X171_SCR_Stand_Spooked_Fall_" + ((text == "Left") ? "Right" : ((text == "Right") ? "Left" : "Back")) + "_Stand");
		}
		else if (UnityEngine.Random.value < 0.5f)
		{
			c.Yaw = Quaternion.LookRotation((carP - c.Pos).Flat()).eulerAngles.y;
			num = OneShot(c, (UnityEngine.Random.value < 0.5f) ? "X171_InteracaoPersonagem_Hostil1" : "X171_InteracaoPersonagem_Hostil2");
		}
		FBNpcVoz.Say(c, "veiculo", 1f, 4.5f, 6f, 0.6f, (num > 0f) ? 0.25f : 0f);
		return num;
	}

	public static void Event(string cat, Vector3 p, float range = 32f, int talkers = 1)
	{
		if (!On || FBRole.npcs == null)
		{
			return;
		}
		List<FBCharacter> list = (from n in FBRole.npcs
			where (bool)n && n.alive && !n.vehiclePose && (n.Pos - p).sqrMagnitude < range * range
			orderby (n.Pos - p).sqrMagnitude
			select n).Take(6).ToList();
		int num = 0;
		foreach (FBCharacter item in list)
		{
			FBRoleNpc component = item.GetComponent<FBRoleNpc>();
			if ((bool)component && component.Free)
			{
				component.LookAt(p, UnityEngine.Random.Range(2.5f, 4.5f));
				if (num < talkers && FBNpcVoz.Say(item, cat, 1f, 4.5f, 6f, 0.4f, UnityEngine.Random.Range(0.25f, 0.9f)) > 0f)
				{
					num++;
				}
			}
		}
	}

	public static void Grau(FBCar car)
	{
		if (!On || Time.time < grauT)
		{
			return;
		}
		grauT = Time.time + UnityEngine.Random.Range(4f, 8f);
		Vector3 p = car.transform.position;
		FBCharacter fBCharacter = (from x in FBRole.npcs?.Where((FBCharacter x) => (bool)x && x.alive && !x.vehiclePose && (x.Pos - p).sqrMagnitude < 484f)
			orderby UnityEngine.Random.value
			select x).FirstOrDefault();
		if ((bool)fBCharacter)
		{
			FBRoleNpc component = fBCharacter.GetComponent<FBRoleNpc>();
			if ((bool)component && component.Free)
			{
				component.LookAt(p, 2.5f);
			}
			FBNpcVoz.Say(fBCharacter, "grau", 1f, 4.5f, 10f, 1f, 0.2f);
		}
	}
}
}
