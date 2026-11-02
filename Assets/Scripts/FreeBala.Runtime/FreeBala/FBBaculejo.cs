using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FreeBala
{
public static class FBBaculejo
{
	private static class Voz
	{
		private class Line
		{
			public string c;

			public string t;

			public float d;
		}

		private static Dictionary<string, Dictionary<string, List<Line>>> cats;

		private static bool tried;

		public static int Said;

		public static string Last;

		private static readonly List<(float at, Act who, string cat, string rx)> q = new List<(float, Act, string, string)>();

		private static float busyT;

		public static bool Ready
		{
			get
			{
				Load();
				if (cats != null)
				{
					return cats.Count > 0;
				}
				return false;
			}
		}

		private static void Load()
		{
			if (tried)
			{
				return;
			}
			tried = true;
			TextAsset textAsset = Resources.Load<TextAsset>("Audio/171/policia/policia");
			if (!textAsset)
			{
				return;
			}
			cats = new Dictionary<string, Dictionary<string, List<Line>>>();
			foreach (KeyValuePair<string, object> item in Json.O(Json.Parse(textAsset.text), "cats") ?? new Dictionary<string, object>())
			{
				Dictionary<string, List<Line>> dictionary = new Dictionary<string, List<Line>>();
				foreach (KeyValuePair<string, object> item2 in (item.Value as Dictionary<string, object>) ?? new Dictionary<string, object>())
				{
					dictionary[item2.Key] = (from o in (item2.Value as List<object>) ?? new List<object>()
						select new Line
						{
							c = Json.S(o, "c"),
							t = (Json.S(o, "t") ?? ""),
							d = Json.F(o, "d", 1.5f)
						} into l
						where l.c != null
						select l).ToList();
				}
				cats[item.Key] = dictionary;
			}
		}

		public static void Say(Act who, string cat, string rx = null, float delay = 0f)
		{
			if ((bool)who?.c && Ready)
			{
				q.Add((Time.time + delay, who, cat, rx));
			}
		}

		public static void Clear()
		{
			q.Clear();
			busyT = 0f;
		}

		public static void Tick()
		{
			if (q.Count == 0 || Time.time < busyT)
			{
				return;
			}
			int num = q.FindIndex(((float at, Act who, string cat, string rx) x) => Time.time >= x.at);
			if (num < 0)
			{
				return;
			}
			(float, Act, string, string) tuple = q[num];
			Act who = tuple.Item2;
			string item = tuple.Item3;
			string rx = tuple.Item4;
			q.RemoveAt(num);
			if (!Ok(who) || !cats.TryGetValue(item, out var value))
			{
				return;
			}
			string voz = who.voz ?? "Homem1";
			if (!value.TryGetValue(voz, out var value2) || value2.Count == 0)
			{
				value2 = value.Where((KeyValuePair<string, List<Line>> kv) => kv.Key.StartsWith(voz.StartsWith("M") ? "Mulher" : "Homem")).SelectMany((KeyValuePair<string, List<Line>> kv) => kv.Value).ToList();
			}
			if (value2 == null || value2.Count == 0)
			{
				return;
			}
			List<Line> list = ((rx != null) ? value2.Where((Line x) => Regex.IsMatch(x.t, rx, RegexOptions.IgnoreCase)).ToList() : value2);
			if (list.Count == 0)
			{
				list = value2;
			}
			List<Line> list2 = list.Where((Line x) => !who.said.Contains(x.c)).ToList();
			if (list2.Count == 0)
			{
				list2 = value2.Where((Line x) => !who.said.Contains(x.c)).ToList();
			}
			Line line = ((list2.Count > 0) ? list2 : list)[UnityEngine.Random.Range(0, ((list2.Count > 0) ? list2 : list).Count)];
			who.said.Add(line.c);
			AudioClip audioClip = Resources.Load<AudioClip>("Audio/171/policia/" + line.c);
			if ((bool)audioClip)
			{
				Vector3 pos = who.c.Pos + Vector3.up * 1.62f * who.c.ScaleK;
				FBAudio.PlayAt(audioClip, pos, 1f, 7f, 0.02f);
				who.c.talking = true;
				who.talkUntil = Time.time + line.d;
				busyT = Time.time + line.d + UnityEngine.Random.Range(0.25f, 0.55f);
				Said++;
				Last = item + ":" + line.t;
				Log("fala (" + who.c.skinId + ", " + voz + "): \"" + line.t + "\"");
			}
		}

		public static void Radio(Vector3 p, bool fim)
		{
			AudioClip audioClip = FBAudio.Clip(fim ? "p_radio_fim" : "p_radio_loop");
			if (!audioClip)
			{
				audioClip = Resources.Load<AudioClip>("Audio/171/policia/" + (fim ? "p_radio_fim" : "p_radio_loop"));
			}
			if ((bool)audioClip)
			{
				FBAudio.PlayAt(audioClip, p + Vector3.up * 1.2f, fim ? 0.55f : 0.35f, 5f, 0f, fim ? 0f : 2.4f);
			}
		}
	}

	public class Act
	{
		public string voz;

		public float talkUntil;

		public readonly HashSet<string> said = new HashSet<string>();

		public FBCharacter c;

		public FBStreetActor a;

		public Vector3 spot;

		public Vector3 ring;

		public float yaw;

		public float idleT;

		public float shotT;

		public string idle;

		public string pose;

		public bool spawned;

		public bool fled;

		public bool gone;

		public bool armed;

		public bool down;

		public bool inPlace;

		public MonoBehaviour borrowed;

		public int ammo;
	}

	private const string ANOTA = "FF_REB_Policia_Multa";

	private const string FALA = "NPC_Talk_Serious";

	private const string MAOS = "SCR_Gunpoint_Hands_Up_Loop";

	private const string SAIR = "REB_CHAR_SairCarroEsquerda";

	private static readonly string[] ENQUADRO = new string[2] { "X171_EnquadroPolicial", "X171_EnquadroPolicial2_root_Enquadrado2" };

	private static readonly string[] RODA = new string[9] { "X171_055_Mocap101_SL_STATIC_B_Smoke_UE41", "X171_fumando_looped", "X171_051_Mocap101_SL_STATIC_B_WriteMobile_UE4", "X171_004_Mocap101_SL_IDLE_B_Armscross_UE4", "NPC_Talk_Serious", "NPC_Talk_M", "NPC_Stand_Talk", "X171_Idle_Male", "X171_053_Mocap101_SL_STATIC_B_Watch1_UE4" };

	private const string DEITA = "X171_Rendido_Start";

	private const string DEITADO = "X171_Rendido_Loop";

	private static readonly string[] VIATURAS = new string[2] { "ViaturaPMAzul", "ViaturaBM" };

	private static readonly string[] FUZIS = new string[4] { "ia2", "parafal", "m4", "ar15" };

	private static readonly string[] PISTOLAS = new string[3] { "g18", "pistola_308", "38" };

	private static Act frisker;

	private static Act cover;

	private static readonly List<Act> sus = new List<Act>();

	private static float afastaT;

	private static FBCar car;

	private static FBTransito.Car tc;

	private static string st;

	private static float stT;

	private static float life;

	private static float quiet;

	private static float talkT;

	private static int cur;

	private static int segId = -1;

	private static int speaker;

	private static Vector3 F;

	private static Vector3 along;

	private static Vector3 center;

	private static Vector3 carP;

	private static Vector3 carF;

	private static Vector3 ringC;

	private static float segS;

	private static bool wall;

	private static FBGiroflex.Bar bar;

	private static string outcome;

	public static string ForceOutcome;

	private static List<Vector3> path;

	private static float pathLen;

	private static float pathPos;

	private static float pathV;

	private static readonly List<Act> strays = new List<Act>();

	public static int SaiuClipe;

	private static Act runner;

	private static float chaseT;

	private static bool fugaFeita;

	private static Act shooter;

	private static float sirenT;

	private const float SIREN_VOL = 0.85f;

	private static string REVISTA => Pick("X171_ALS_RevistandoLoop", "FF_REB_Policia_Revista");

	private static string PM_ESPERA => Pick("X171_EnquadroPolicial1_root_Policial1", "X171_PosePolicial_root_Pose3");

	public static IReadOnlyList<Act> Suspects => sus;

	public static int Falas => Voz.Said;

	public static string UltimaFala => Voz.Last;

	public static Act Frisker => frisker;

	public static Act Cover => cover;

	public static string Outcome => outcome;

	public static bool Active
	{
		get
		{
			if (sus.Count <= 0 && !car)
			{
				return frisker != null;
			}
			return true;
		}
	}

	public static string State => st;

	public static FBCar Car => car;

	private static bool Has(string c)
	{
		if ((bool)FBRegistry.I)
		{
			return FBRegistry.I.Clip(c);
		}
		return false;
	}

	private static string Pick(params string[] l)
	{
		return l.FirstOrDefault(Has);
	}

	private static string VozAbordado(FBCharacter c)
	{
		if (!c)
		{
			return "Homem1";
		}
		uint num = (uint)((c.skinId ?? "").GetHashCode() * 31 + FBNpcVoz.Id(c));
		return "Homem" + (1 + num % 3);
	}

	public static float Cooldown()
	{
		return UnityEngine.Random.Range(200f, 420f);
	}

	private static float Yaw(Vector3 f)
	{
		return Mathf.Atan2(f.x, f.z) * 57.29578f;
	}

	private static bool Ok(Act a)
	{
		if (a != null && (bool)a.c)
		{
			return a.c.alive;
		}
		return false;
	}

	private static void Pose(FBCharacter c, string clip)
	{
		if ((bool)c && clip != null && c.PlayDance(clip) && c.dance != null)
		{
			c.dance.len = 1E+09f;
		}
	}

	private static void Log(string s)
	{
		Debug.Log("[FreeBala][ruas-eventos] baculejo: " + s);
	}

	public static bool Start()
	{
		if (!FBTransito.Running || !FBRegistry.I || !FBMatch.I)
		{
			return false;
		}
		if (!VIATURAS.Any((string v) => (bool)FBRegistry.I.Vehicle(v) && GameData.Vehicles.ContainsKey(v)))
		{
			return false;
		}
		int num = (FBQuality.Mobile ? 2 : ((UnityEngine.Random.value < 0.5f) ? 3 : 2));
		if (!Plan(num, out var pts))
		{
			FBStreetEvents.L("baculejo: nenhum trecho de guia/calçada livre");
			return false;
		}
		sus.Clear();
		ringC = center - F * 1f;
		for (int i = 0; i < num; i++)
		{
			float f = (float)i / (float)num * (float)Math.PI * 2f + UnityEngine.Random.Range(-0.3f, 0.3f);
			Vector3 p = ringC + new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f)) * UnityEngine.Random.Range(0.75f, 1f);
			if (!FBStreetEvents.Ground(ref p, center.y, 0.5f) || !FBStreetEvents.Free(p, 0.26f))
			{
				p = pts[i] - F * 1.2f;
			}
			Act act = Recruit(p, i);
			if (act != null)
			{
				act.spot = pts[Mathf.Min(i, pts.Count - 1)];
				act.ring = p;
				sus.Add(act);
			}
		}
		if (sus.Count == 0)
		{
			FBStreetEvents.L("baculejo: nenhum abordado disponível");
			End(now: true);
			return false;
		}
		HashSet<string> used = new HashSet<string>();
		foreach (Act su in sus)
		{
			su.a = su.c.gameObject.GetOrAdd<FBStreetActor>();
			su.a.Init(su.c);
			su.a.life = true;
			su.yaw = Yaw((ringC - su.c.Pos).Flat());
			su.c.Yaw = su.yaw;
			su.a.faceYaw = su.yaw;
			su.idle = (from x in RODA.Where(Has)
				where !used.Contains(x)
				select x into _
				orderby UnityEngine.Random.value
				select _).FirstOrDefault() ?? RODA.FirstOrDefault(Has);
			used.Add(su.idle);
			Pose(su.c, su.idle);
			su.idleT = UnityEngine.Random.Range(9f, 18f);
		}
		FBStreetEvents.NoCollide(sus.Select((Act a) => a.c));
		outcome = ForceOutcome ?? ((UnityEngine.Random.value < 0.62f) ? "normal" : ((UnityEngine.Random.value < 0.58f) ? "fuga" : "confronto"));
		ForceOutcome = null;
		fugaFeita = false;
		if (outcome == "confronto")
		{
			sus[UnityEngine.Random.Range(0, sus.Count)].armed = true;
		}
		st = "roda";
		stT = ((FBStreetEvents.ForceNear > 0f) ? 7f : UnityEngine.Random.Range(18f, 40f));
		life = 0f;
		quiet = 0f;
		cur = 0;
		speaker = 0;
		talkT = 0f;
		FBDamageEvents.OnDamage -= OnDamage;
		FBDamageEvents.OnDamage += OnDamage;
		Log(string.Format("roda com {0} na calçada ({1}), desfecho sorteado: {2}, a {3:0} m do jogador", sus.Count, string.Join(", ", sus.Select((Act s) => s.idle)), outcome, (ringC - FBStreetEvents.PlayerPos).Flat().magnitude));
		return true;
	}

	private static bool Plan(int n, out List<Vector3> pts)
	{
		pts = new List<Vector3>();
		Vector3 playerPos = FBStreetEvents.PlayerPos;
		bool flag = FBStreetEvents.ForceNear > 0f;
		List<FBSeg> list = FBRoads.segs.Where((FBSeg S) => S.kind == 0 && !S.ring && S.len > 40f).ToList();
		if (list.Count == 0)
		{
			list = FBRoads.segs.Where((FBSeg S) => S.kind == 0 && !S.ring && S.len > 26f).ToList();
		}
		if (list.Count == 0)
		{
			return false;
		}
		for (int i = 0; i < (flag ? 600 : 200); i++)
		{
			FBSeg fBSeg = list[UnityEngine.Random.Range(0, list.Count)];
			float num = UnityEngine.Random.Range(Mathf.Min(34f, fBSeg.len * 0.6f), fBSeg.len - 10f);
			if (num < 10f)
			{
				continue;
			}
			Vector3 tan;
			Vector3 vector = FBRoads.At(fBSeg, num, out tan);
			tan = tan.Flat().normalized;
			float magnitude = (vector - playerPos).Flat().magnitude;
			bool num2;
			if (!flag)
			{
				if (magnitude < 18f)
				{
					continue;
				}
				num2 = magnitude > (float)((i < 120) ? 50 : 70);
			}
			else
			{
				if (magnitude < 14f)
				{
					continue;
				}
				num2 = magnitude > 70f;
			}
			if (num2)
			{
				continue;
			}
			FBRoads.At(fBSeg, Mathf.Max(0f, num - 10f), out var tan2);
			FBRoads.At(fBSeg, Mathf.Min(fBSeg.len, num + 8f), out var tan3);
			if (Vector3.Angle(tan2.Flat(), tan3.Flat()) > 12f)
			{
				continue;
			}
			Vector3 vector2 = new Vector3(tan.z, 0f, 0f - tan.x);
			float num3 = -1f;
			for (float num4 = 0.75f; num4 < 7.5f; num4 += 0.25f)
			{
				if (!FBRoads.IsRoad(vector + vector2 * num4))
				{
					num3 = num4;
					break;
				}
			}
			if (num3 < 1.2f)
			{
				continue;
			}
			Vector3 cp = vector + vector2 * Mathf.Max(0f, num3 - 1.35f);
			if (!FBStreetEvents.Ground(ref cp, vector.y, 0.5f) || FBVehicles.list.Any((FBCar c) => (bool)c && (c.transform.position - cp).Flat().sqrMagnitude < 100f) || FBTransito.List.Any((FBTransito.Car c) => (bool)c.car && (c.pos - cp).Flat().sqrMagnitude < 256f) || Physics.OverlapBox(cp + Vector3.up * 1.05f, new Vector3(1.05f, 0.6f, 2.7f), Quaternion.LookRotation(tan), FBWorld.SolidMask, QueryTriggerInteraction.Ignore).Any((Collider c) => !FBStreetEvents.Loose(c)))
			{
				continue;
			}
			Vector3 p = vector + vector2 * (num3 + 1.1f);
			if (!FBStreetEvents.Ground(ref p, vector.y, 0.6f) || FBRoads.IsRoad(p))
			{
				continue;
			}
			RaycastHit hitInfo;
			bool flag2 = Physics.Raycast(p + Vector3.up * 1.2f, vector2, out hitInfo, 4.5f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) && hitInfo.distance > 1.6f && Mathf.Abs(hitInfo.normal.y) < 0.3f;
			Vector3 vector3;
			Vector3 p2;
			if (flag2)
			{
				vector3 = vector2;
				p2 = p + vector2 * (hitInfo.distance - 0.62f);
			}
			else
			{
				vector3 = -vector2;
				p2 = p + vector2 * 0.35f;
			}
			p2.y = p.y;
			if (!FBStreetEvents.Ground(ref p2, p.y, 0.5f))
			{
				continue;
			}
			List<Vector3> list2 = new List<Vector3>();
			bool flag3 = true;
			for (int j = 0; j < n && flag3; j++)
			{
				Vector3 p3 = p2 + tan * (((float)j - (float)(n - 1) / 2f) * 1.15f);
				if (!FBStreetEvents.Ground(ref p3, p2.y, 0.45f) || !FBStreetEvents.Free(p3, 0.26f) || FBRoads.IsRoad(p3))
				{
					flag3 = false;
				}
				else
				{
					list2.Add(p3);
				}
			}
			if (flag3)
			{
				Vector3 p4 = p2 - vector3 * 1f;
				if (FBStreetEvents.Ground(ref p4, p2.y, 0.5f) && FBStreetEvents.Free(p4, 0.5f) && FBNav.InsideNear(p2, 2f) && (flag || i >= 140 || (!FBStreetEvents.Visible(p2, 2f) && !FBStreetEvents.Visible(p4, 2f))))
				{
					carP = cp;
					carF = tan;
					pts = list2;
					F = vector3;
					along = tan;
					center = p2;
					wall = flag2;
					segId = fBSeg.id;
					segS = num;
					return true;
				}
			}
		}
		return false;
	}

	private static Act Recruit(Vector3 p, int i)
	{
		Vector3 pp = FBStreetEvents.PlayerPos;
		bool force = FBStreetEvents.ForceNear > 0f;
		if (FBSession.mode == "role")
		{
			foreach (FBCharacter item in from o in FBRole.npcs.Where(OkBorrow)
				orderby (o.Pos - p).sqrMagnitude
				select o)
			{
				FBRoleNpc component = item.GetComponent<FBRoleNpc>();
				if ((bool)component && component.enabled && !(component.st != "walk") && component.seat == null && (!item.IsFemale || !(UnityEngine.Random.value < 0.6f)))
				{
					component.st = "evento";
					component.enabled = false;
					item.StopDance(force: true);
					item.talking = false;
					FBMatch.Teleport(item, p, 0f);
					return new Act
					{
						c = item,
						borrowed = component
					};
				}
			}
		}
		List<string> used = (from x in sus
			where x.c
			select x.c.skinId).ToList();
		string id = FBStreetEvents.FreeCivilian();
		if (id == null || used.Contains(id) || (GameData.Skins.Find((SkinDef s) => s.id == id)?.sex == "f" && UnityEngine.Random.value < 0.6f))
		{
			List<string> list = (from r in FBNpcAnims.Roster(FBSession.skin)
				where FBRegistry.I.HasChar(r) && !used.Contains(r) && !FBNpcAnims.IsPolice(r)
				select r).ToList();
			if (list.Count == 0)
			{
				return null;
			}
			id = list[UnityEngine.Random.Range(0, list.Count)];
		}
		FBCharacter fBCharacter = FBStreetEvents.Spawn(id, p, 0f, "abordado" + i);
		if (!fBCharacter)
		{
			return null;
		}
		return new Act
		{
			c = fBCharacter,
			spawned = true
		};
		bool OkBorrow(FBCharacter o)
		{
			if ((bool)o && o.alive && !o.vehiclePose && (o.Pos - pp).magnitude > 30f)
			{
				if (!force)
				{
					return !FBStreetEvents.Visible(o.Pos);
				}
				return true;
			}
			return false;
		}
	}

	public static void Tick(float dt)
	{
		Vector3 pp = FBStreetEvents.PlayerPos;
		SirenTick(dt);
		for (int num = strays.Count - 1; num >= 0; num--)
		{
			Act act = strays[num];
			if (!act.c || !act.a)
			{
				strays.RemoveAt(num);
			}
			else if (act.a.Arrived)
			{
				if ((act.c.Pos - pp).magnitude > 35f && !FBStreetEvents.Visible(act.c.Pos))
				{
					FBStreetEvents.Despawn(act.c);
					strays.RemoveAt(num);
				}
				else
				{
					act.a.Go(FBTransito.SidewalkPoint(act.c.Pos, 40f) ?? (act.c.Pos + act.c.Fwd * 20f), 1.3f, 1.5f);
				}
			}
		}
		if (!Active)
		{
			return;
		}
		life += dt;
		stT -= dt;
		Voz.Tick();
		foreach (Act item in new Act[2] { frisker, cover }.Concat(sus))
		{
			if ((bool)item?.c && item.talkUntil > 0f && Time.time > item.talkUntil)
			{
				item.c.talking = false;
				item.talkUntil = 0f;
			}
		}
		if (Ok(cover) && (st == "enquadro" || st == "revista" || st == "anota" || st == "fala") && (bool)FBMatch.Player && FBMatch.Player.alive && (FBMatch.Player.Pos - center).Flat().magnitude < 4.2f && Time.time > afastaT)
		{
			afastaT = Time.time + UnityEngine.Random.Range(7f, 11f);
			Voz.Say(cover, "pm_afasta", "dist|longe|afast|perto|aproxima|segura");
		}
		if ((bool)car && bar != null)
		{
			bar.on = st != "partiu" && st != "fim" && st != "roda";
		}
		if (st != "roda" && st != "tumulto" && st != "confronto" && st != "abandono" && st != "volta" && st != "partiu" && st != "fim")
		{
			Vector3? lastNoiseP = FBBotBrain.lastNoiseP;
			if (lastNoiseP.HasValue)
			{
				Vector3 valueOrDefault = lastNoiseP.GetValueOrDefault();
				if (Time.time - FBBotBrain.lastNoiseT < 1.5f && (valueOrDefault - center).magnitude < 45f && !IsOurs(valueOrDefault))
				{
					Disperse(valueOrDefault);
				}
			}
		}
		if (st != "roda" && st != "chegada" && st != "encosta" && (frisker != null || cover != null) && (!Ok(frisker) || !Ok(cover)) && st != "tumulto" && st != "confronto" && st != "abandono" && st != "partiu" && st != "fim" && st != "volta")
		{
			Disperse(null);
		}
		switch (st)
		{
		case "roda":
			Roda(dt);
			if (stT <= 0f)
			{
				Chegada();
			}
			break;
		case "chegada":
			Roda(dt);
			TickChegada();
			break;
		case "encosta":
			TickEncosta(dt);
			break;
		case "desembarque":
			if (stT <= 0f)
			{
				Abordagem();
			}
			break;
		case "abordagem":
			TickAbordagem();
			break;
		case "enquadro":
			TickEnquadro();
			break;
		case "revista":
			TickRevista(dt);
			break;
		case "anota":
			if (stT <= 0f)
			{
				st = "fala";
				stT = UnityEngine.Random.Range(4f, 6.5f);
				Pose(frisker.c, "NPC_Talk_Serious");
			}
			break;
		case "fala":
			if (stT <= 0f)
			{
				Release();
			}
			break;
		case "libera":
			if (stT <= 0f)
			{
				st = "volta";
				stT = 25f;
				Board();
			}
			break;
		case "volta":
			if (!Ok(frisker) || !Ok(cover) || !car)
			{
				st = "abandono";
				break;
			}
			if (!Seated(cover) && cover.a.Arrived)
			{
				SitPassenger();
			}
			if ((frisker.a.Arrived && Seated(cover)) || stT <= 0f)
			{
				Depart();
			}
			break;
		case "perseguicao":
			TickPerseguicao(dt);
			break;
		case "confronto":
			TickConfronto(dt);
			break;
		case "tumulto":
			if ((quiet -= dt) <= 0f)
			{
				if (Ok(frisker) && Ok(cover) && (bool)car)
				{
					st = "volta";
					stT = 20f;
					Board(hurry: true);
				}
				else
				{
					st = "abandono";
				}
			}
			break;
		case "abandono":
			if ((center - pp).magnitude > 40f && !FBStreetEvents.Visible(center, 4f) && (!car || !FBStreetEvents.Visible(car.transform.position, 3f)))
			{
				Cleanup(now: true);
			}
			break;
		case "partiu":
			if (tc == null || tc.mode == "gone" || life > 400f)
			{
				Cleanup(now: false);
			}
			break;
		}
		foreach (Act su in sus)
		{
			if (!su.gone && (bool)su.c && su.fled && (bool)su.a && su.a.Arrived && (su.c.Pos - pp).magnitude > 35f && !FBStreetEvents.Visible(su.c.Pos))
			{
				Return(su);
			}
		}
		if (Ok(cover) && (st == "enquadro" || st == "revista" || st == "anota" || st == "fala"))
		{
			bool flag = (bool)FBMatch.Player && FBMatch.Player.alive && !FBMatch.Player.vehiclePose && sus.Any((Act s) => (bool)s.c && (s.c.Pos - pp).Flat().magnitude < 2.6f);
			Aim(cover, on: true, flag ? new Vector3?(pp) : null);
		}
	}

	private static bool IsOurs(Vector3 p)
	{
		return new Act[2] { frisker, cover }.Concat(sus).Any((Act a) => (bool)a?.c && (a.c.MuzzlePos - p).sqrMagnitude < 1.5f);
	}

	private static void Roda(float dt)
	{
		if ((talkT -= dt) <= 0f)
		{
			speaker = UnityEngine.Random.Range(0, sus.Count);
			talkT = UnityEngine.Random.Range(4f, 8f);
		}
		for (int i = 0; i < sus.Count; i++)
		{
			Act s = sus[i];
			if (!Ok(s) || s.fled)
			{
				continue;
			}
			if ((s.idleT -= dt) <= 0f)
			{
				HashSet<string> taken = (from o in sus
					where o != s
					select o.idle).ToHashSet();
				string text = (from x in RODA.Where(Has)
					where x != s.idle && !taken.Contains(x)
					select x into _
					orderby UnityEngine.Random.value
					select _).FirstOrDefault();
				if (text != null)
				{
					s.idle = text;
					Pose(s.c, text);
				}
				s.idleT = UnityEngine.Random.Range(10f, 20f);
			}
			Act act = sus[Mathf.Clamp(speaker, 0, sus.Count - 1)];
			s.a.faceYaw = ((act != s && (bool)act.c) ? Mathf.LerpAngle(Yaw((ringC - s.c.Pos).Flat()), Yaw((act.c.Pos - s.c.Pos).Flat()), 0.5f) : Yaw((ringC - s.c.Pos).Flat()));
			s.c.talking = act == s;
		}
	}

	private static void Chegada()
	{
		string text = VIATURAS.FirstOrDefault((string v) => (bool)FBRegistry.I.Vehicle(v) && GameData.Vehicles.ContainsKey(v));
		List<string> list = FBStreetEvents.FreeIds(FBNpcAnims.Police);
		if (list.Count == 0)
		{
			list = FBNpcAnims.Police.Where((string i) => FBRegistry.I.HasChar(i)).ToList();
		}
		List<string> list2 = list.Where((string i) => !Fem(i)).ToList();
		List<string> list3 = list.Where(Fem).ToList();
		if (list2.Count > 0)
		{
			string text2 = list2[0];
			string item = ((list3.Count > 0 && UnityEngine.Random.value < 0.35f) ? list3[0] : ((list2.Count > 1) ? list2[1] : text2));
			list = ((UnityEngine.Random.value < 0.5f) ? new List<string> { text2, item } : new List<string> { item, text2 });
		}
		FBSeg fBSeg = FBRoads.segs.FirstOrDefault((FBSeg x) => x.id == segId);
		if (text == null || list.Count == 0 || fBSeg == null)
		{
			Log("sem viatura/PM/faixa — a roda se desfaz");
			Release();
			return;
		}
		float num = Mathf.Max(3f, segS - UnityEngine.Random.Range(45f, 60f));
		Vector3 tan;
		Vector3 vector = FBRoads.At(fBSeg, num, out tan);
		List<object> list4 = Json.L(Json.O(GameData.Vehicles, text), "paint");
		uint? paintOpt = ((list4 != null) ? new uint?(((uint)(Json.Fv(list4[0]) * 255f) << 16) | ((uint)(Json.Fv(list4[1]) * 255f) << 8) | (uint)(Json.Fv(list4[2]) * 255f)) : null);
		car = FBVehicles.Make(text, vector, Mathf.Atan2(tan.x, tan.z) * 57.29578f, civil: false, () => 0.5f, true, paintOpt, false);
		if (!car)
		{
			Release();
			return;
		}
		car.name = "Viatura_Abordagem";
		car.syncId = "evt:viatura";
		FBCharacter fBCharacter = FBStreetEvents.Spawn(list[0], vector, 0f, "pm1");
		FBCharacter fBCharacter2 = FBStreetEvents.Spawn((list.Count > 1) ? list[1] : list[0], vector + Vector3.up * 0.1f, 0f, "pm2");
		if (!fBCharacter || !fBCharacter2)
		{
			Log("PM não nasceu");
			Cleanup(now: true);
			return;
		}
		frisker = new Act
		{
			c = fBCharacter,
			spawned = true
		};
		cover = new Act
		{
			c = fBCharacter2,
			spawned = true
		};
		string[] array = ((!(UnityEngine.Random.value < 0.5f)) ? new string[2] { "Homem3", "Homem1" } : new string[2] { "Homem1", "Homem3" });
		int num2 = 0;
		Act[] array2 = new Act[2] { frisker, cover };
		foreach (Act obj in array2)
		{
			obj.voz = (obj.c.IsFemale ? "Mulher2" : array[num2++]);
		}
		foreach (Act su in sus)
		{
			if ((bool)su.c)
			{
				su.voz = VozAbordado(su.c);
			}
		}
		Voz.Clear();
		Voz.Radio(vector, fim: false);
		Voz.Say(cover, "pm_radio", null, 1.2f);
		array2 = new Act[2] { frisker, cover };
		foreach (Act act in array2)
		{
			act.a = act.c.gameObject.GetOrAdd<FBStreetActor>();
			act.a.Init(act.c);
		}
		tc = FBTransito.SpawnSpecial(car, fBCharacter, "viatura", UnityEngine.Random.Range(8.5f, 10.5f), segId, num);
		if (tc == null)
		{
			Log("viatura sem faixa");
			Cleanup(now: true);
			return;
		}
		tc.lure = false;
		tc.maxLife = 60f;
		tc.onDrop = delegate
		{
			tc = null;
		};
		fBCharacter2.gameObject.GetOrAdd<FBPassengerSeat>().Sit(car);
		bar = FBGiroflex.Attach(car);
		Siren(on: true, 2.2f);
		st = "chegada";
		stT = 40f;
		Log($"viatura {text} vindo pela faixa a {segS - num:0} m da parada");
		static bool Fem(string id)
		{
			return GameData.Skins.Find((SkinDef s) => s.id == id)?.sex == "f";
		}
	}

	private static void TickChegada()
	{
		if (!car)
		{
			Cleanup(now: true);
			return;
		}
		float num = ((tc != null && tc.ri < tc.route.Count && tc.route[tc.ri] == segId) ? (segS - tc.s) : (-1f));
		if (tc != null && num >= 0f && num < 24f)
		{
			Vector3 position = car.transform.position;
			Vector3 normalized = car.transform.forward.Flat().normalized;
			float num2 = Mathf.Max(3f, tc.v);
			tc.onDrop = null;
			FBTransito.Forget(tc);
			tc = null;
			float magnitude = (carP - position).Flat().magnitude;
			path = new List<Vector3>();
			for (int i = 0; i <= 40; i++)
			{
				float num3 = (float)i / 40f;
				float num4 = 1f - num3;
				Vector3 item = num4 * num4 * num4 * position + 3f * num4 * num4 * num3 * (position + normalized * magnitude * 0.45f) + 3f * num4 * num3 * num3 * (carP - carF * magnitude * 0.4f) + num3 * num3 * num3 * carP;
				path.Add(item);
			}
			pathLen = 0f;
			for (int j = 1; j < path.Count; j++)
			{
				pathLen += (path[j] - path[j - 1]).Flat().magnitude;
			}
			pathPos = 0f;
			pathV = num2;
			st = "encosta";
			foreach (Act item2 in sus.Where(Ok))
			{
				item2.c.talking = false;
				item2.a.faceYaw = Yaw((car.transform.position - item2.c.Pos).Flat());
			}
			Log("encostando na guia");
		}
		else
		{
			if (!(stT <= 0f) && tc != null)
			{
				return;
			}
			if (!FBStreetEvents.Visible(carP, 3f))
			{
				if (tc != null)
				{
					tc.onDrop = null;
					FBTransito.Forget(tc);
					tc = null;
				}
				car.transform.SetPositionAndRotation(carP, FBDrive.CarRot(0f, Mathf.Atan2(0f - carF.x, carF.z), 0f));
				car.yaw = Mathf.Atan2(0f - carF.x, carF.z);
				Desembarque();
			}
			else
			{
				Log("viatura não chegou");
				Cleanup(now: false);
			}
		}
	}

	private static void TickEncosta(float dt)
	{
		if (!car || path == null)
		{
			Cleanup(now: true);
			return;
		}
		float b = pathLen - pathPos;
		float b2 = Mathf.Sqrt(4.4f * Mathf.Max(0f, b));
		pathV = Mathf.Max(Mathf.Min(pathV, b2), 0.25f);
		pathPos = Mathf.Min(pathLen, pathPos + pathV * dt);
		float num = 0f;
		List<Vector3> list = path;
		Vector3 vector = list[list.Count - 1];
		Vector3 normalized = carF;
		for (int i = 1; i < path.Count; i++)
		{
			float magnitude = (path[i] - path[i - 1]).Flat().magnitude;
			if (num + magnitude >= pathPos)
			{
				float t = ((magnitude > 0.0001f) ? ((pathPos - num) / magnitude) : 0f);
				vector = Vector3.Lerp(path[i - 1], path[i], t);
				normalized = (path[i] - path[i - 1]).Flat().normalized;
				break;
			}
			num += magnitude;
		}
		Vector3 p = vector;
		FBStreetEvents.Ground(ref p, vector.y, 1.2f);
		float yaw = Mathf.Atan2(0f - normalized.x, normalized.z);
		car.transform.SetPositionAndRotation(new Vector3(vector.x, p.y, vector.z), FBDrive.CarRot(0f, yaw, 0f));
		car.yaw = yaw;
		if (pathPos >= pathLen - 0.02f)
		{
			Desembarque();
		}
	}

	private static void Desembarque()
	{
		st = "desembarque";
		stT = 2.1f;
		if (FBDoors.HasPart(car, 3))
		{
			FBDoors.SetMask(car, 3);
		}
		FBDriverPose fBDriverPose = (frisker?.c ? frisker.c.GetComponent<FBDriverPose>() : null);
		if ((bool)fBDriverPose && FBDrive.StartCarOut(frisker.c, car, needFree: false))
		{
			stT = Mathf.Max(stT, fBDriverPose.transLen + 0.3f);
			SaiuClipe++;
			fBDriverPose.onOut = delegate(FBCharacter ch, FBCar cr)
			{
				FBDrive.OutPoint(ch, cr, out var at, out var yaw);
				FBDrive.Release(ch);
				FBMatch.Teleport(ch, at, yaw);
				CharacterController component3 = ch.GetComponent<CharacterController>();
				if ((bool)component3)
				{
					component3.enabled = true;
				}
			};
		}
		else if ((bool)frisker?.c)
		{
			FBDrive.Release(frisker.c);
			Vector3 p = DoorPoint(driver: true);
			FBMatch.Teleport(frisker.c, p, Yaw(-carF) + 90f);
			CharacterController component = frisker.c.GetComponent<CharacterController>();
			if ((bool)component)
			{
				component.enabled = true;
			}
			if (Has("REB_CHAR_SairCarroEsquerda"))
			{
				frisker.c.PlayDance("REB_CHAR_SairCarroEsquerda");
			}
		}
		if ((bool)cover?.c)
		{
			FBPassengerSeat component2 = cover.c.GetComponent<FBPassengerSeat>();
			if ((bool)component2)
			{
				component2.Leave();
			}
			FBMatch.Teleport(cover.c, DoorPoint(driver: false), Yaw(-carF) - 90f);
		}
		Siren(on: false);
		Log("desembarque");
	}

	private static void Abordagem()
	{
		Give(cover, FUZIS);
		Give(frisker, PISTOLAS);
		Act[] array = new Act[2] { frisker, cover };
		foreach (Act act in array)
		{
			if ((bool)act?.c)
			{
				act.c.StopDance(force: true);
				act.c.combat = true;
			}
		}
		Vector3 vector = (from s in sus.Where(Ok)
			select s.c.Pos).DefaultIfEmpty(ringC).Aggregate(Vector3.zero, (Vector3 x, Vector3 y) => x + y) / Mathf.Max(1, sus.Count(Ok));
		Vector3 p = center - F * 3.4f + along * ((UnityEngine.Random.value < 0.5f) ? 1.6f : (-1.6f));
		FBStreetEvents.Ground(ref p, center.y, 0.8f);
		cover.spot = p;
		cover.a.Go(p, 1.7f, 0.35f);
		Aim(cover, on: true, vector + Vector3.up * 0.2f);
		frisker.spot = vector - F * 2.2f;
		frisker.a.Go(frisker.spot, 1.6f, 0.4f);
		Aim(frisker, on: true, vector);
		foreach (Act item in sus.Where(Ok))
		{
			item.c.talking = false;
			item.c.StopDance(force: true);
			item.a.faceYaw = Yaw((frisker.c.Pos - item.c.Pos).Flat());
			Pose(item.c, Has("SCR_Gunpoint_Hands_Up_Loop") ? "SCR_Gunpoint_Hands_Up_Loop" : ENQUADRO[1]);
			item.inPlace = false;
		}
		st = "abordagem";
		stT = 3.5f;
		Log("abordagem");
		Voz.Say(frisker, "pm_render", "pol[ií]cia|levanta|parado|m[aã]o");
		Voz.Say(frisker, "pm_enquadro", "m[aã]o na cabe|abre as pernas|parado|de boa", 0.3f);
		Act act2 = sus.FirstOrDefault((Act s) => Ok(s) && !s.c.IsFemale);
		if (act2 != null)
		{
			Voz.Say(act2, "ab_enquadro", "calma|trabalhador|tranquil", 0.6f);
		}
	}

	private static void TickAbordagem()
	{
		if (!Ok(frisker) || !Ok(cover))
		{
			return;
		}
		Vector3 value = (from s in sus
			where Ok(s) && !s.fled
			select s.c.Pos).DefaultIfEmpty(ringC).Aggregate(Vector3.zero, (Vector3 x, Vector3 y) => x + y) / Mathf.Max(1, sus.Count((Act s) => Ok(s) && !s.fled));
		Aim(frisker, on: true, value);
		if (cover.a.Arrived)
		{
			Aim(cover, on: true, value);
		}
		if (outcome == "confronto" && stT < 1.5f)
		{
			Confronto();
		}
		else
		{
			if (stT > 0f)
			{
				return;
			}
			int num = 0;
			foreach (Act item in sus.Where((Act s) => Ok(s) && !s.fled))
			{
				item.c.StopDance(force: true);
				Vector3 spot = item.spot;
				Act ss = item;
				item.pose = ENQUADRO[num++ % 2];
				if (!Has(item.pose))
				{
					item.pose = ENQUADRO.FirstOrDefault(Has) ?? "FF_REB_Rendido";
				}
				item.a.Go(spot, 1.15f, 0.15f);
				item.a.onArrive = delegate
				{
					ss.a.faceYaw = Yaw(F);
					ss.c.Yaw = Yaw(F);
					Pose(ss.c, ss.pose);
					ss.inPlace = true;
				};
			}
			st = "enquadro";
			stT = 12f;
		}
	}

	private static void TickEnquadro()
	{
		List<Act> list = sus.Where((Act s) => Ok(s) && !s.fled).ToList();
		if (list.Count == 0)
		{
			Release();
		}
		else
		{
			if (!list.All((Act s) => s.inPlace) && !(stT <= 0f))
			{
				return;
			}
			foreach (Act item in list.Where((Act s) => !s.inPlace))
			{
				FBMatch.Teleport(item.c, item.spot, Yaw(F));
				item.a.Stop();
				Pose(item.c, item.pose);
				item.inPlace = true;
			}
			Aim(frisker, on: false);
			frisker.c.SetWeaponVisible(v: false);
			frisker.c.combat = false;
			cur = sus.IndexOf(list[0]);
			GoFrisk();
			st = "revista";
		}
	}

	private static void GoFrisk()
	{
		Act act = sus[cur];
		frisker.c.StopDance(force: true);
		frisker.a.Go(act.c.Pos - F * 0.44f, 1.1f, 0.12f);
		stT = UnityEngine.Random.Range(7f, 10f);
		Voz.Say(frisker, "pm_enquadro", (UnityEngine.Random.value < 0.15f && !frisker.said.Any((string x) => x.Contains("pm_enquadro"))) ? "pacot" : "droga|escondeu|BO|quieto", 1.6f);
		if ((bool)act.c && !act.c.IsFemale)
		{
			Voz.Say(act, "ab_enquadro", "nada|olhar|n[aã]o [eé] meu|bunda|sozinho|uso", 2.4f);
		}
	}

	private static void TickRevista(float dt)
	{
		Act s = ((cur < sus.Count) ? sus[cur] : null);
		if (s == null || !Ok(s) || s.fled)
		{
			NextSuspect();
			return;
		}
		Vector3 vector = s.c.Pos - F * 0.44f;
		if (!frisker.a.Arrived && stT < -6f)
		{
			if (FBStreetEvents.Visible(frisker.c.Pos, 0.6f) && !((frisker.c.Pos - vector).Flat().magnitude < 0.8f))
			{
				NextSuspect();
				return;
			}
			FBMatch.Teleport(frisker.c, vector, Yaw(F));
			frisker.a.Stop();
		}
		if (!frisker.a.Arrived || (frisker.c.Pos - vector).Flat().magnitude > 0.25f)
		{
			if (frisker.c.dance != null && (frisker.c.Pos - vector).Flat().magnitude > 0.3f)
			{
				frisker.c.StopDance(force: true);
				frisker.a.Go(vector, 1.1f, 0.12f);
			}
			if (frisker.a.Arrived)
			{
				frisker.c.Yaw = Yaw(F);
				frisker.a.faceYaw = Yaw(F);
				Pose(frisker.c, REVISTA);
			}
		}
		else if (outcome == "fuga" && !fugaFeita && sus.All((Act x) => !x.fled) && stT < 5f && sus.Count(Ok) > 0)
		{
			Flee(sus.Where((Act x) => Ok(x) && x != s).DefaultIfEmpty(s).First());
		}
		else if (outcome == "confronto" && stT < 4f)
		{
			Confronto();
		}
		else if (stT <= 0f)
		{
			NextSuspect();
		}
	}

	private static void NextSuspect()
	{
		cur++;
		while (cur < sus.Count && (!Ok(sus[cur]) || sus[cur].fled || sus[cur].down))
		{
			cur++;
		}
		if (cur < sus.Count)
		{
			GoFrisk();
			return;
		}
		st = "anota";
		stT = UnityEngine.Random.Range(6f, 9f);
		frisker.c.StopDance(force: true);
		Voz.Say(frisker, "pm_enquadro", "de boa|n[oó]ia|vagabundo", 0.8f);
		Vector3 p = center - F * 1.5f + along * UnityEngine.Random.Range(-0.6f, 0.6f);
		if (FBStreetEvents.Ground(ref p, center.y, 0.5f) && FBStreetEvents.Free(p, 0.26f))
		{
			frisker.a.Go(p, 1f, 0.15f);
			frisker.a.onArrive = delegate
			{
				frisker.a.faceYaw = Yaw(F);
				Pose(frisker.c, "FF_REB_Policia_Multa");
			};
		}
		else
		{
			Pose(frisker.c, "FF_REB_Policia_Multa");
		}
	}

	private static void Flee(Act s)
	{
		runner = s;
		s.fled = true;
		fugaFeita = true;
		s.c.StopDance(force: true);
		s.a.life = (s.a.flatFeet = false);
		s.c.fleeing = true;
		Vector3 vector = (s.c.Pos - (car ? car.transform.position : frisker.c.Pos)).Flat();
		vector = ((vector.sqrMagnitude > 0.01f) ? vector.normalized : along) + along * ((UnityEngine.Random.value < 0.5f) ? 1 : (-1));
		Vector3 p = FBNav.RandomPoint(s.c.Pos + vector.normalized * 60f, 12f) ?? (s.c.Pos + vector.normalized * 45f);
		s.a.run = true;
		s.a.Go(p, 5f, 1.2f);
		frisker.c.StopDance(force: true);
		frisker.c.SetWeaponVisible(v: true);
		frisker.c.combat = true;
		frisker.a.run = true;
		foreach (Act item in sus.Where((Act x) => x != s && Ok(x) && !x.fled))
		{
			Pose(item.c, Has("SCR_Gunpoint_Hands_Up_Loop") ? "SCR_Gunpoint_Hands_Up_Loop" : item.pose);
		}
		st = "perseguicao";
		chaseT = 0f;
		Log("abordado correu — perseguição");
		Voz.Clear();
		Voz.Say(frisker, "pm_render", "parado|pinote|n[aã]o vou falar");
		Voz.Say(cover, "pm_radio", null, 1.5f);
	}

	private static void TickPerseguicao(float dt)
	{
		chaseT += dt;
		Act act = runner;
		if (act == null || !Ok(act))
		{
			BackToFrisk();
			return;
		}
		if (act.down)
		{
			if (chaseT > 7f)
			{
				frisker.c.StopDance(force: true);
				act.c.StopDance(force: true);
				act.fled = false;
				act.down = false;
				act.a.Go(act.spot, 1.1f, 0.15f);
				Act rr = act;
				act.a.onArrive = delegate
				{
					rr.a.faceYaw = Yaw(F);
					Pose(rr.c, rr.pose ?? ENQUADRO[0]);
				};
				BackToFrisk();
			}
			return;
		}
		float magnitude = (act.c.Pos - frisker.c.Pos).Flat().magnitude;
		frisker.a.Go(act.c.Pos, (magnitude > 3f) ? 5.4f : 3.5f, 1f);
		Aim(frisker, magnitude < 9f, act.c.Pos + Vector3.up * 0.2f);
		if (magnitude < 1.4f && chaseT > 2.5f)
		{
			act.a.Stop();
			act.a.run = false;
			act.c.fleeing = false;
			act.down = true;
			act.c.PlayDance(Has("X171_Rendido_Start") ? "X171_Rendido_Start" : "SCR_Gunpoint_Hands_Up_Loop");
			act.a.faceYaw = Yaw((act.c.Pos - frisker.c.Pos).Flat());
			frisker.a.Stop();
			frisker.a.run = false;
			Aim(frisker, on: false);
			chaseT = 0f;
			Log("fugitivo alcançado — rendido no chão");
			Voz.Say(frisker, "pm_render", "deita|ch[aã]o|perdeu");
		}
		else if (chaseT > 14f || magnitude > 28f)
		{
			act.gone = false;
			Log("fugitivo escapou");
			BackToFrisk();
		}
	}

	private static void BackToFrisk()
	{
		runner = null;
		frisker.a.run = false;
		frisker.c.SetWeaponVisible(v: false);
		frisker.c.combat = false;
		Aim(frisker, on: false);
		foreach (Act item in sus.Where((Act x) => Ok(x) && !x.fled && !x.down))
		{
			Pose(item.c, item.pose ?? ENQUADRO[0]);
		}
		cur = -1;
		NextSuspect();
		if (st != "anota")
		{
			st = "revista";
		}
	}

	private static void Confronto()
	{
		shooter = sus.FirstOrDefault((Act s) => s.armed && Ok(s)) ?? sus.FirstOrDefault(Ok);
		if (shooter == null)
		{
			Release();
			return;
		}
		shooter.fled = true;
		shooter.c.StopDance(force: true);
		shooter.a.Stop();
		shooter.a.life = (shooter.a.flatFeet = false);
		shooter.c.civ = false;
		FBWeaponUser component = shooter.c.GetComponent<FBWeaponUser>();
		if ((bool)component)
		{
			component.Give("38");
			int num = Array.FindIndex(component.slots, (WeaponDef x) => x != null && x.id == "38");
			component.Equip((num >= 0) ? num : 0, instant: true);
		}
		shooter.c.SetWeaponVisible(v: true);
		shooter.c.combat = true;
		shooter.shotT = UnityEngine.Random.Range(0.5f, 0.9f);
		shooter.ammo = UnityEngine.Random.Range(4, 7);
		foreach (Act item in sus.Where((Act x) => x != shooter && Ok(x) && !x.fled))
		{
			item.c.StopDance(force: true);
			item.down = true;
			item.c.PlayDance(Has("X171_Rendido_Start") ? "X171_Rendido_Start" : "SCR_Gunpoint_Hands_Up_Loop");
		}
		Act[] array = new Act[2] { frisker, cover };
		foreach (Act act in array)
		{
			if (Ok(act))
			{
				act.c.StopDance(force: true);
				act.c.SetWeaponVisible(v: true);
				act.c.combat = true;
				act.a.Stop();
				act.shotT = UnityEngine.Random.Range(0.25f, 0.6f);
			}
		}
		st = "confronto";
		stT = 18f;
		Log("CONFRONTO: " + shooter.c.skinId + " sacou");
		Voz.Clear();
		Voz.Say(cover, "pm_render", "larga a arma|abaixa a arma|deita");
		Voz.Say(frisker, "pm_render", "atirar|ch[aã]o|encosta", 0.4f);
	}

	private static void TickConfronto(float dt)
	{
		bool flag = Ok(shooter);
		if (flag)
		{
			Act act = (from a in new Act[2] { frisker, cover }.Where(Ok)
				orderby (a.c.Pos - shooter.c.Pos).sqrMagnitude
				select a).FirstOrDefault();
			if (act != null)
			{
				Aim(shooter, on: true, act.c.Pos + Vector3.up * 0.25f);
				shooter.a.faceYaw = Yaw((act.c.Pos - shooter.c.Pos).Flat());
				if ((shooter.shotT -= dt) <= 0f && shooter.ammo > 0)
				{
					Shoot(shooter, act.c, 9f);
					shooter.ammo--;
					shooter.shotT = UnityEngine.Random.Range(0.35f, 0.8f);
				}
				if (shooter.ammo <= 0 && shooter.a.Arrived)
				{
					Vector3 normalized = (shooter.c.Pos - act.c.Pos).Flat().normalized;
					shooter.a.run = true;
					shooter.a.Go(FBNav.RandomPoint(shooter.c.Pos + normalized * 40f, 10f) ?? (shooter.c.Pos + normalized * 30f), 5f, 1.5f);
				}
			}
		}
		Act[] array = new Act[2] { frisker, cover };
		foreach (Act act2 in array)
		{
			if (!Ok(act2))
			{
				continue;
			}
			if (flag)
			{
				Aim(act2, on: true, shooter.c.Pos + Vector3.up * 0.2f);
				act2.a.faceYaw = Yaw((shooter.c.Pos - act2.c.Pos).Flat());
				if ((act2.shotT -= dt) <= 0f)
				{
					Shoot(act2, shooter.c, 3.5f);
					act2.shotT = ((act2 == cover) ? UnityEngine.Random.Range(0.12f, 0.2f) : UnityEngine.Random.Range(0.3f, 0.55f));
				}
			}
			else
			{
				Aim(act2, on: true, (shooter != null && (bool)shooter.c) ? shooter.c.Pos : center);
			}
		}
		if (flag && !(stT <= 0f) && (shooter == null || !shooter.c || !((shooter.c.Pos - center).Flat().magnitude > 35f)))
		{
			return;
		}
		Log(flag ? "confronto terminou (atirador fugiu)" : "confronto terminou (atirador abatido)");
		array = new Act[2] { frisker, cover };
		foreach (Act act3 in array)
		{
			if (Ok(act3))
			{
				act3.c.shooting = false;
			}
		}
		st = "tumulto";
		quiet = UnityEngine.Random.Range(10f, 14f);
	}

	private static void Shoot(Act a, FBCharacter t, float spreadDeg)
	{
		FBWeaponUser component = a.c.GetComponent<FBWeaponUser>();
		if ((bool)component && component.Cur != null && (bool)t)
		{
			Vector3 vector = (t.Chest ? t.Chest.position : (t.Pos + Vector3.up * 1.2f * t.ScaleK));
			a.c.aimPoint = vector;
			a.c.hasAim = (a.c.aiming = true);
			Vector3 muzzlePos = a.c.MuzzlePos;
			Vector3 hitPoint = vector - muzzlePos;
			Vector3 dir = FBBotSense.Scatter(hitPoint.normalized, spreadDeg);
			component.Fire(muzzlePos, dir, new FBRecoil.State
			{
				aiming = true,
				zoom = 1f,
				grip = 1f
			}, out hitPoint);
			a.c.lastShot = 0f;
			a.c.shooting = true;
		}
	}

	private static void Release()
	{
		st = "libera";
		stT = 2.2f;
		if ((bool)frisker?.c)
		{
			frisker.c.StopDance(force: true);
		}
		Aim(cover, on: false);
		Aim(frisker, on: false);
		int num = 0;
		foreach (Act item in sus.Where((Act x) => Ok(x) && !x.fled))
		{
			item.c.StopDance(force: true);
			item.down = false;
			item.a.life = false;
			item.a.flatFeet = false;
			Vector3 vector = along * ((num++ % 2 == 0) ? 1 : (-1));
			Vector3 vector2 = FBTransito.SidewalkPoint(item.c.Pos + vector * 45f, 15f) ?? (item.c.Pos + vector * 30f);
			if (item.borrowed is FBRoleNpc fBRoleNpc)
			{
				Return(item);
				fBRoleNpc.goalP = vector2;
				fBRoleNpc.stT = UnityEngine.Random.Range(12f, 20f);
			}
			else if ((bool)item.borrowed)
			{
				Return(item);
			}
			else
			{
				item.a.Go(vector2, UnityEngine.Random.Range(1.2f, 1.45f), 1.2f);
				item.gone = true;
				strays.Add(item);
			}
		}
		if (frisker == null)
		{
			st = "fim";
			Cleanup(now: false);
		}
	}

	private static Vector3 DoorPoint(bool driver)
	{
		Vector3 local = car.rigOut ?? new Vector3(-1.6f, 0f, 0f);
		if (!driver)
		{
			local = new Vector3(0f - local.x, local.y, local.z);
		}
		Vector3 p = car.L2W(local);
		FBStreetEvents.Ground(ref p, car.transform.position.y, 0.8f);
		return p;
	}

	private static void Board(bool hurry = false)
	{
		if (!car)
		{
			Cleanup(now: false);
			return;
		}
		Act[] array = new Act[2] { frisker, cover };
		foreach (Act act in array)
		{
			if ((bool)act?.c)
			{
				act.c.StopDance(force: true);
				Aim(act, on: false);
				act.c.SetWeaponVisible(v: false);
				act.a.run = hurry;
				act.a.Go(DoorPoint(act == frisker), hurry ? 3.6f : 1.35f, 0.35f);
			}
		}
	}

	private static bool Seated(Act a)
	{
		if (a != null && (bool)a.c)
		{
			if (!a.c.vehiclePose)
			{
				return !a.c.alive;
			}
			return true;
		}
		return false;
	}

	private static void SitPassenger()
	{
		if ((bool)cover?.c && (bool)car)
		{
			cover.c.aiming = (cover.c.hasAim = false);
			cover.c.gameObject.GetOrAdd<FBPassengerSeat>().Sit(car);
			cover.a.enabled = false;
		}
	}

	private static void Depart()
	{
		if (!car)
		{
			Cleanup(now: false);
			return;
		}
		if (FBDoors.HasPart(car, 3))
		{
			FBDoors.SetMask(car, 0, sound: true);
		}
		if ((bool)cover?.c && !cover.c.vehiclePose && cover.c.alive)
		{
			SitPassenger();
		}
		if ((bool)frisker?.c && frisker.c.alive)
		{
			frisker.a.enabled = false;
			frisker.c.StopDance(force: true);
			tc = FBTransito.LeaveCurb(car, frisker.c, "viatura", UnityEngine.Random.Range(7f, 9.5f));
			if (tc != null)
			{
				tc.maxLife = 60f;
				tc.onDrop = delegate
				{
					tc = null;
					Cleanup(now: false);
				};
			}
		}
		st = ((tc != null) ? "partiu" : "abandono");
		stT = 0f;
		Log("viatura saindo (" + ((tc != null) ? "trânsito" : "sem faixa") + ")");
		if ((bool)car)
		{
			Voz.Radio(car.transform.position, fim: true);
		}
	}

	private static void Disperse(Vector3? from)
	{
		if (st == "tumulto" || st == "volta" || st == "partiu" || st == "fim" || st == "confronto")
		{
			return;
		}
		st = "tumulto";
		quiet = UnityEngine.Random.Range(6f, 9f);
		foreach (Act item in sus.Where((Act x) => Ok(x) && !x.fled))
		{
			item.fled = true;
			item.c.StopDance(force: true);
			item.a.life = (item.a.flatFeet = false);
			item.c.fleeing = true;
			Vector3 normalized;
			if (from.HasValue)
			{
				Vector3 valueOrDefault = from.GetValueOrDefault();
				normalized = (item.c.Pos - valueOrDefault).Flat().normalized;
			}
			else
			{
				normalized = along;
			}
			Vector3 vector = normalized;
			item.a.run = true;
			item.a.Go(FBNav.RandomPoint(item.c.Pos + vector * 45f, 12f) ?? (item.c.Pos + vector * 35f), 5f, 1.5f);
		}
		if ((bool)frisker?.c)
		{
			frisker.c.StopDance(force: true);
			frisker.a.Stop();
		}
		if ((bool)cover?.c && from.HasValue)
		{
			Vector3 valueOrDefault2 = from.GetValueOrDefault();
			cover.c.combat = true;
			Aim(cover, on: true, valueOrDefault2);
		}
		Log("dispersou (tiro/ferido)");
	}

	private static void OnDamage(FBDamage d)
	{
		if (Active && (bool)d.victim && !(st == "confronto") && (d.victim == frisker?.c || d.victim == cover?.c || sus.Any((Act s) => s.c == d.victim)) && (!d.source || !IsOursChar(d.source)))
		{
			Disperse(d.source ? d.source.Pos : d.victim.Pos);
		}
	}

	private static bool IsOursChar(FBCharacter c)
	{
		if (!(c == frisker?.c) && !(c == cover?.c))
		{
			return sus.Any((Act s) => s.c == c);
		}
		return true;
	}

	private static void Aim(Act a, bool on, Vector3? at = null)
	{
		FBCharacter fBCharacter = a?.c;
		if (!fBCharacter)
		{
			return;
		}
		fBCharacter.aiming = on;
		fBCharacter.hasAim = on;
		if (on)
		{
			Vector3 vector = at ?? ((from s in sus
				where (bool)s.c && !s.fled
				select s.c.Pos).DefaultIfEmpty(center).Aggregate(Vector3.zero, (Vector3 x, Vector3 y) => x + y) / Mathf.Max(1, sus.Count((Act s) => (bool)s.c && !s.fled)));
			fBCharacter.aimPoint = vector + Vector3.up * 1.15f;
			if ((bool)a.a && a.a.Arrived)
			{
				a.a.faceYaw = Yaw((vector - fBCharacter.Pos).Flat());
			}
		}
	}

	private static void Give(Act a, string[] ids)
	{
		if (!(a?.c))
		{
			return;
		}
		FBWeaponUser component = a.c.GetComponent<FBWeaponUser>();
		string g = ids.FirstOrDefault((string x) => GameData.Weapons.ContainsKey(x) && (bool)FBRegistry.I.Weapon(x));
		if ((bool)component && g != null)
		{
			component.Give(g);
			int num = Array.FindIndex(component.slots, (WeaponDef x) => x != null && x.id == g);
			component.Equip((num >= 0) ? num : 0, instant: true);
			a.c.SetWeaponVisible(v: true);
		}
	}

	private static void Return(Act a)
	{
		a.gone = true;
		if ((bool)a.c)
		{
			a.c.fleeing = false;
			a.c.surrender = false;
			a.c.talking = false;
			if ((bool)a.a)
			{
				UnityEngine.Object.Destroy(a.a);
			}
			if (a.spawned)
			{
				FBStreetEvents.Despawn(a.c);
			}
			else if (a.borrowed is FBRoleNpc fBRoleNpc)
			{
				fBRoleNpc.st = "walk";
				fBRoleNpc.goalP = null;
				fBRoleNpc.stT = 0f;
				fBRoleNpc.enabled = true;
			}
			else if ((bool)a.borrowed)
			{
				a.borrowed.enabled = true;
			}
		}
	}

	private static void Siren(bool on, float secs = 9f)
	{
		if (!on || !car)
		{
			if ((bool)car)
			{
				FBSiren.Set(car, 0);
			}
			sirenT = 0f;
		}
		else
		{
			FBSiren.Set(car, (!(secs < 4f)) ? 1 : 2, 0.85f);
			sirenT = secs;
		}
	}

	private static void SirenTick(float dt)
	{
		if (sirenT <= 0f)
		{
			return;
		}
		if (!car)
		{
			sirenT = 0f;
			return;
		}
		sirenT -= dt;
		FBSiren.Volume(car, 0.85f * Mathf.Clamp01(sirenT / 1.2f));
		if (sirenT <= 0f)
		{
			Siren(on: false);
		}
	}

	private static void Cleanup(bool now)
	{
		Siren(on: false);
		if (st == "fim" && !now)
		{
			return;
		}
		st = "fim";
		foreach (Act su in sus)
		{
			if (!su.gone)
			{
				if (now || !su.c || (bool)su.borrowed || !su.c.alive || !FBStreetEvents.Visible(su.c.Pos))
				{
					Return(su);
					continue;
				}
				su.c.StopDance(force: true);
				su.a.life = (su.a.flatFeet = false);
				su.a.Go(FBTransito.SidewalkPoint(su.c.Pos, 40f) ?? su.c.Pos, 1.3f, 1.5f);
				su.gone = true;
				strays.Add(su);
			}
		}
		if (now)
		{
			foreach (Act stray in strays)
			{
				if ((bool)stray.c)
				{
					FBStreetEvents.Despawn(stray.c);
				}
			}
			strays.Clear();
		}
		Act[] array = new Act[2] { frisker, cover };
		foreach (Act act in array)
		{
			if ((bool)act?.c && act.spawned)
			{
				FBStreetEvents.Despawn(act.c);
			}
		}
		if (tc != null)
		{
			tc.onDrop = null;
			FBTransito.Forget(tc);
			tc = null;
		}
		FBGiroflex.Detach(bar);
		bar = null;
		if ((bool)car)
		{
			FBVehicles.list.Remove(car);
			UnityEngine.Object.Destroy(car.gameObject);
		}
		car = null;
		frisker = (cover = null);
		runner = (shooter = null);
		fugaFeita = false;
		sus.Clear();
		path = null;
		FBDamageEvents.OnDamage -= OnDamage;
		Voz.Clear();
	}

	public static void End(bool now)
	{
		if (Active)
		{
			Cleanup(now: true);
		}
		st = null;
	}
}
}
