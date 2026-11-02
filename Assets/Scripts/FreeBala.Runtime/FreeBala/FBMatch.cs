using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace FreeBala
{
public class FBMatch : MonoBehaviour
{
	public class StatsT
	{
		public int kills;

		public int deaths;

		public int shots;

		public int hits;

		public int heads;
	}

	public class Line
	{
		public List<Vector3> spots = new List<Vector3>();

		public float yaw;

		public int next;
	}

	public string mapId;

	private static FBMatch inst;

	public static readonly List<FBCharacter> All = new List<FBCharacter>();

	public static readonly List<FBCharacter> Bots = new List<FBCharacter>();

	public static readonly List<FBCharacter> Civs = new List<FBCharacter>();

	public static readonly List<FBCharacter> Remotes = new List<FBCharacter>();

	public static bool FFA;

	public static bool Paused;

	public static bool God;

	public static FBCharacter Player;

	public static readonly StatsT Stats = new StatsT();

	public static readonly Dictionary<string, (int team, int k, int d)> Tally = new Dictionary<string, (int, int, int)>();

	public FBMode mode;

	public string state = "loading";

	public bool freeze;

	public static readonly string[] TEAM_NAME = new string[2] { "TIME AZUL", "TIME VERMELHO" };

	public static readonly string[] TEAM_SHORT = new string[2] { "AZUL", "VERMELHO" };

	public List<string>[] typePool = new List<string>[2]
	{
		new List<string>(),
		new List<string>()
	};

	private float respawnT = -1f;

	private FBCharacter spect;

	public static string BuildTimes = "";

	public readonly Line[] teamLine = new Line[2];

	private static readonly Vector2[] OFFS = new Vector2[8]
	{
		new Vector2(0f, 0f),
		new Vector2(1.8f, -0.4f),
		new Vector2(-1.8f, -0.4f),
		new Vector2(0f, -1.6f),
		new Vector2(3.2f, -0.8f),
		new Vector2(-3.2f, -0.8f),
		new Vector2(1.8f, -2.2f),
		new Vector2(-1.8f, -2.2f)
	};

	private static readonly string[] SQUAD_ROLES = new string[8] { "rusher", "flanker_left", "flanker_right", "tactical", "striker", "baianogmbarril", "halemph", "halemph2" };

	private int streakN;

	private float streakT;

	public FBKillstreak ks = new FBKillstreak();

	private readonly List<(float, Action)> timers = new List<(float, Action)>();

	public static FBMatch I
	{
		get
		{
			if (!inst)
			{
				return null;
			}
			return inst;
		}
		set
		{
			inst = value;
		}
	}

	public float RespawnLeft => respawnT;

	public MapDef Map
	{
		get
		{
			if (!GameData.Maps.TryGetValue(mapId, out var value))
			{
				return null;
			}
			return value;
		}
	}

	public Dictionary<string, object> Level => Map?.level;

	public string MapName => (Map?.name ?? mapId).ToUpper();

	public bool FFDamage
	{
		get
		{
			if (mode != null)
			{
				return mode.ffDamage;
			}
			return false;
		}
	}

	public static bool KnockOn
	{
		get
		{
			if ((bool)I && I.state == "play" && FBMechanics.KnockMode(FBSession.mode))
			{
				return !FBSession.online;
			}
			return false;
		}
	}

	public FBCharacter Spectating => spect;

	public static bool Hostile(FBCharacter a, FBCharacter b)
	{
		if ((bool)a && (bool)b)
		{
			if (!FFA)
			{
				return a.team != b.team;
			}
			return a != b;
		}
		return false;
	}

	public static IEnumerable<FBCharacter> TeamOf(FBCharacter a)
	{
		return All.Where((FBCharacter o) => (bool)o && o != a && !o.civ && !o.isRemote && o.team == a.team);
	}

	public static bool HasReviver(FBCharacter a)
	{
		return TeamOf(a).Any((FBCharacter o) => o.alive && !o.knocked);
	}

	public static void Feed(string t, bool me = false)
	{
		FBHUD.I?.Feed(t, me);
	}

	public static IEnumerable<FBCharacter> Actors()
	{
		return All.Where((FBCharacter a) => (bool)a && !a.civ);
	}

	private void Awake()
	{
		I = this;
		All.Clear();
		Bots.Clear();
		Civs.Clear();
		Remotes.Clear();
		GameData.Load();
		FBSession.Load();
		Paused = false;
		FFA = false;
		FBNav.Reset();
		FBFx.Warm();
	}

	private void Start()
	{
		Stopwatch bsw = Stopwatch.StartNew();
		StringBuilder bl = new StringBuilder("· montagem: ");
		double bt = 0.0;
		try
		{
			Start0(B);
		}
		finally
		{
			B("resto");
			BuildTimes = bl.ToString();
		}
		void B(string n)
		{
			double totalSeconds = bsw.Elapsed.TotalSeconds;
			if (totalSeconds - bt >= 0.05)
			{
				bl.Append($"{n} {totalSeconds - bt:0.00}s ");
			}
			bt = totalSeconds;
		}
	}

	private void Start0(Action<string> B)
	{
		new GameObject("HUD").AddComponent<FBHUD>();
		if (Map == null)
		{
			Debug.LogError("[FreeBala] mapa sem dados: " + mapId);
			return;
		}
		FBWorld.SetWater(Level);
		FBEconomy.ResetMatch();
		FBBotBrain.Reset();
		FBLoot.Clear();
		FBMarks.Clear();
		B("HUD/limpeza");
		FBVehicles.SpawnParked(this, (FBSession.mode == "role") ? 0.3f : 0f);
		new GameObject("FB_Veiculos").AddComponent<FBVehiclesTicker>();
		FBDoors.Reset();
		if (FBVehicles.Allowed)
		{
			FBDoors.Boot();
		}
		B("carros estacionados");
		EnsureLevel();
		B("pontos do nível");
		FBWeather.Apply(FBSession.WeatherFor(mapId));
		B("clima");
		if (FBSession.mode == "pvl")
		{
			List<string> list = Side(FBSession.team);
			if (!list.Contains(FBSession.skin) && list.Count > 0)
			{
				FBSession.skin = list[0];
			}
			typePool = new List<string>[2]
			{
				(from id in Side(0)
					where id != FBSession.skin
					select id).Take(6).ToList(),
				(from id in Side(1)
					where id != FBSession.skin
					select id).Take(6).ToList()
			};
		}
		else
		{
			List<string> source = (from k in GameData.Skins
				where !k.role && k.id != FBSession.skin && FBRegistry.I.HasChar(k.id)
				select k.id into _
				orderby UnityEngine.Random.value
				select _).ToList();
			typePool = new List<string>[2]
			{
				source.Take(6).ToList(),
				source.Skip(6).Take(6).ToList()
			};
		}
		for (int i = 0; i < 2; i++)
		{
			if (typePool[i].Count == 0)
			{
				typePool[i].Add(FBRegistry.I.charIds[0]);
			}
		}
		Stats.kills = (Stats.deaths = (Stats.shots = (Stats.hits = (Stats.heads = 0))));
		Tally.Clear();
		PickBases();
		Vector3 pos = NextSpot(FBSession.team);
		B("bases");
		Player = Spawn(FBSession.skin, FBSession.look, FBSession.team, pos, player: true);
		B("jogador");
		Player.transform.rotation = Quaternion.Euler(0f, teamLine[FBSession.team].yaw, 0f);
		bool flag = FBEconomy.EconMode(FBSession.mode) || FBSession.mode == "br" || FBSession.mode == "role";
		FBWeaponUser component = Player.GetComponent<FBWeaponUser>();
		if (FBSession.mode == "role")
		{
			component.Strip();
		}
		else if (!flag)
		{
			component.Give(FBSession.primary, FBSession.smg, FBSession.secondary);
		}
		else
		{
			component.Give();
		}
		FBMed.ResetPlayer(flag ? 1 : 2, FBSession.mode == "treino");
		if (FBSession.online)
		{
			new GameObject("Rede").AddComponent<FBNet>();
			mode = new FBModes.Online(this);
		}
		else
		{
			mode = FBModes.Create(FBSession.mode, this);
		}
		B("jogador (armas/rede)");
		mode.Setup();
		B("modo (bots)");
		if (mode.ffa)
		{
			FFA = true;
			Vector3 vector = mode.SpawnPoint();
			Teleport(Player, vector, SpawnYaw(vector));
		}
		if (!FBEconomy.EconMode(FBSession.mode) && !(FBSession.mode == "treino") && !(FBSession.mode == "ffa") && !FBSession.online && !(FBSession.mode == "role") && !(FBSession.mode == "br"))
		{
			SpawnCivilians(10);
		}
		B("civis");
		if (FBSession.map != "estande" && FBSession.map != "bermuda")
		{
			FBGraffiti.Place(FBSession.map);
		}
		B("grafites");
		if (FBSession.mode == "role")
		{
			FBRole.Setup(this);
		}
		else
		{
			FBAmbientScenery.Spawn(this);
			FBTactics.Spawn();
		}
		B((FBSession.mode == "role") ? "rolê (trânsito/NPCs)" : "cenário/táticas");
		FBStreetEvents.Boot(this);
		B("eventos de rua");
		if (FBSession.mode != "role" && FBSession.map != "estande" && FBSession.map != "bermuda")
		{
			FBVestirRua.Dress();
			B("objetos do 171");
		}
		state = "play";
		FBEconomy.Hud();
		FBAudio.SetLoop("amb", "amb", 0.45f);
		FBAmbience.Start(mapId);
		GameObject[] rootGameObjects = base.gameObject.scene.GetRootGameObjects();
		foreach (GameObject gameObject in rootGameObjects)
		{
			if (gameObject.name.StartsWith("Map_"))
			{
				Debug.Log($"[FreeBala] postes na pista escondidos: {FBStreetPoles.Clean(gameObject)} · destrutíveis: {FBDestructible.Setup(gameObject)} objetos");
			}
		}
		static List<string> Side(int t)
		{
			return (from k in GameData.Skins
				where !k.role && ((k.team < 0) ? 1 : k.team) == t && FBRegistry.I.HasChar(k.id)
				select k.id into _
				orderby UnityEngine.Random.value
				select _).ToList();
		}
	}

	private void EnsureLevel()
	{
		MapDef map = Map;
		if (map.spawnA.Count > 0 && map.spawnB.Count > 0 && map.points.Count > 0 && map.waypoints.Count > 0 && map.bots.Count > 0)
		{
			return;
		}
		List<Vector3> list = new List<Vector3>();
		for (int i = 0; i < 900; i++)
		{
			Vector3? vector = FBNav.RandomPoint();
			if (vector.HasValue)
			{
				Vector3 valueOrDefault = vector.GetValueOrDefault();
				if (!FBWorld.RayHit(valueOrDefault + Vector3.up * 0.5f, Vector3.up, 2.5f))
				{
					list.Add(valueOrDefault + Vector3.up * 0.1f);
				}
			}
		}
		if (list.Count < 8)
		{
			return;
		}
		Vector3 c = list.Aggregate(Vector3.zero, (Vector3 a, Vector3 b) => a + b) / list.Count;
		List<float> list2 = (from p in list
			select (p - c).Flat().magnitude into x
			orderby x
			select x).ToList();
		float lim = list2[list2.Count / 2];
		List<Vector3> ground = (from p in list
			where (p - c).Flat().magnitude <= lim
			orderby p.y
			select p).ToList();
		ground = ground.Take(Mathf.Max(8, ground.Count * 4 / 5)).ToList();
		Vector3 vector2 = ground[0];
		foreach (Vector3 item2 in ground)
		{
			if (D2(item2, ground[0]) > D2(vector2, ground[0]))
			{
				vector2 = item2;
			}
		}
		Vector3 vector3 = ground[0];
		foreach (Vector3 item3 in ground)
		{
			if (D2(item3, vector2) > D2(vector3, vector2))
			{
				vector3 = item3;
			}
		}
		if (map.spawnA.Count == 0)
		{
			map.spawnA = Near(vector2, 5);
		}
		if (map.spawnB.Count == 0)
		{
			map.spawnB = Near(vector3, 5);
		}
		if (map.points.Count == 0)
		{
			map.points = Fps(5, map.spawnA.Concat(map.spawnB).ToList());
		}
		if (map.waypoints.Count == 0)
		{
			map.waypoints = Fps(Mathf.Min(20, ground.Count), new List<Vector3>());
		}
		if (map.bots.Count == 0)
		{
			map.bots = Fps(Mathf.Min(18, ground.Count), map.spawnA);
		}
		static float D2(Vector3 a, Vector3 b)
		{
			return (a - b).Flat().sqrMagnitude;
		}
		List<Vector3> Fps(int n, List<Vector3> avoid)
		{
			List<Vector3> list3 = ground.Where((Vector3 p) => avoid.All((Vector3 a) => D2(p, a) > 400f)).ToList();
			if (list3.Count == 0)
			{
				list3 = ground;
			}
			List<Vector3> o = new List<Vector3> { list3[list3.Count / 2] };
			while (o.Count < n)
			{
				Vector3 item = list3.OrderByDescending((Vector3 p) => o.Min((Vector3 q) => D2(q, p))).First();
				o.Add(item);
			}
			return o;
		}
		List<Vector3> Near(Vector3 cc, int n)
		{
			return ground.OrderBy((Vector3 a) => D2(a, cc)).Take(n).ToList();
		}
	}

	public List<Vector3> TeamSpawns(int t)
	{
		MapDef map = Map;
		List<object> list = Json.L(Level, "spawnPolice");
		string text = Json.S(Level, "policeSpawn");
		List<Vector3> list2;
		if (FBSession.mode == "pvl" && list != null && list.Count > 0)
		{
			int num = ((!(text == "B")) ? 1 : 0);
			list2 = ((t == num) ? list.Select((object o) => Coords.FromWeb(o as List<object>)).ToList() : map.spawnA);
		}
		else
		{
			list2 = ((((FBSession.mode == "pvl" && text == "B") ? (1 - t) : t) == 0) ? map.spawnA : map.spawnB);
		}
		if (list2.Count <= 0)
		{
			return map.bots;
		}
		return list2;
	}

	public void PickBases()
	{
		int team = FBSession.team;
		int num = 1 - team;
		List<Vector3> list = TeamSpawns(team);
		List<Vector3> list2 = TeamSpawns(num);
		if (list.Count == 0 || list2.Count == 0)
		{
			return;
		}
		float num2 = -1f;
		Vector3 item = list[0];
		Vector3 item2 = list2[0];
		foreach (Vector3 item3 in list)
		{
			foreach (Vector3 item4 in list2)
			{
				float magnitude = (item3 - item4).Flat().magnitude;
				if (magnitude > num2)
				{
					num2 = magnitude;
					item = item3;
					item2 = item4;
				}
			}
		}
		List<(Vector3, Vector3)> list3 = new List<(Vector3, Vector3)>();
		foreach (Vector3 item5 in list)
		{
			foreach (Vector3 item6 in list2)
			{
				if ((item5 - item6).Flat().magnitude >= Mathf.Max(35f, num2 * 0.82f))
				{
					list3.Add((item5, item6));
				}
			}
		}
		(Vector3, Vector3) tuple = ((list3.Count > 0) ? list3[UnityEngine.Random.Range(0, list3.Count)] : (item, item2));
		teamLine[team] = LineSpots(tuple.Item1, 8, tuple.Item2);
		teamLine[num] = LineSpots(tuple.Item2, 8, tuple.Item1);
	}

	private Line LineSpots(Vector3 b, int n, Vector3? lookAt)
	{
		float num;
		if (lookAt.HasValue)
		{
			Vector3 valueOrDefault = lookAt.GetValueOrDefault();
			num = Quaternion.LookRotation((valueOrDefault - b).Flat()).eulerAngles.y;
		}
		else
		{
			num = SpawnYaw(b);
		}
		float num2 = num;
		Quaternion quaternion = Quaternion.Euler(0f, num2, 0f);
		Vector3 vector = quaternion * Vector3.right;
		Vector3 vector2 = quaternion * Vector3.forward;
		Vector3 o = b + Vector3.up * 1.2f;
		Line line = new Line
		{
			yaw = num2
		};
		Vector2[] oFFS = OFFS;
		for (int i = 0; i < oFFS.Length; i++)
		{
			Vector2 vector3 = oFFS[i];
			if (line.spots.Count >= n)
			{
				break;
			}
			Vector3 vector4 = vector * vector3.x + vector2 * vector3.y;
			float magnitude = vector4.magnitude;
			if (!(magnitude > 0.01f) || !FBWorld.RayHit(o, vector4 / magnitude, magnitude + 0.45f))
			{
				float? num3 = FBWorld.GroundY(b.x + vector4.x, b.y + 1.5f, b.z + vector4.z, 0.3f, 3f);
				if (num3.HasValue && !(Mathf.Abs(num3.Value - b.y) > 1.8f))
				{
					line.spots.Add(new Vector3(b.x + vector4.x, num3.Value, b.z + vector4.z));
				}
			}
		}
		while (line.spots.Count < n)
		{
			float f = (float)line.spots.Count / (float)n * (float)Math.PI * 2f;
			line.spots.Add(b + new Vector3(Mathf.Cos(f) * 1.5f, 0f, Mathf.Sin(f) * 1.5f));
		}
		return line;
	}

	public Vector3 NextSpot(int team)
	{
		Line[] array = teamLine;
		Line line = array[team] ?? (array[team] = LineSpots(FBUtil.Pick(TeamSpawns(team)), 8, null));
		Vector3 result = line.spots[line.next % line.spots.Count];
		line.next++;
		return result;
	}

	public float SpawnYaw(Vector3 sp)
	{
		MapDef map = Map;
		float num = FBUtil.YawOf(((map.points.Count > 0) ? map.points[0] : ((map.bots.Count > 0) ? map.bots[0] : Vector3.zero)) - sp);
		Vector3 o = sp + Vector3.up * 1.5f;
		float num2 = num;
		float num3 = -1E+09f;
		for (int i = 0; i < 32; i++)
		{
			float num4 = (float)i / 32f * (float)Math.PI * 2f;
			RaycastHit hit;
			float num5 = Mathf.Min(FBWorld.RayHit(o, new Vector3(Mathf.Sin(num4), 0f, Mathf.Cos(num4)), 30f, out hit) ? hit.distance : 30f, 20f) + Mathf.Cos(FBUtil.WrapA(num4 - num)) * 4f;
			if (num5 > num3)
			{
				num3 = num5;
				num2 = num4;
			}
		}
		return num2 * 57.29578f;
	}

	public FBCharacter Spawn(string skin, string look, int team, Vector3 pos, bool player, bool bot = false, bool civ = false)
	{
		GameObject gameObject = (FBRegistry.I ? FBRegistry.I.Char(skin) : null);
		if (!gameObject)
		{
			gameObject = FBRegistry.I.Char(FBRegistry.I.charIds[0]);
		}
		GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject, pos + Vector3.up * 0.05f, Quaternion.Euler(0f, UnityEngine.Random.Range(0, 360), 0f));
		gameObject2.name = (player ? "Jogador_" : (civ ? "Civil_" : "Bot_")) + skin;
		SetLayer(gameObject2, 10);
		FBCharacter orAdd = gameObject2.GetOrAdd<FBCharacter>();
		orAdd.team = team;
		orAdd.skinId = skin;
		orAdd.isPlayer = player;
		orAdd.isBot = bot;
		orAdd.civ = civ;
		FBWardrobe.ApplyTo(gameObject2, player ? look : null, !player && !GameData.Skins.Exists((SkinDef s) => s.id == skin && !string.IsNullOrEmpty(s.npc2)));
		CharacterController characterController = gameObject2.AddComponent<CharacterController>();
		float num = 1f / gameObject2.transform.lossyScale.y;
		float num2 = Json.F(GameData.Section("controller"), "m_Height", 1.8f);
		float a = Json.F(GameData.Section("controller"), "m_Radius", 0.3f);
		characterController.height = num2 * num;
		characterController.radius = Mathf.Max(a, 0.32f) * num;
		characterController.center = new Vector3(0f, num2 * 0.5f * num, 0f);
		characterController.stepOffset = 0.45f * num;
		characterController.slopeLimit = 50f;
		characterController.skinWidth = 0.04f * num;
		characterController.minMoveDistance = 0f;
		gameObject2.AddComponent<FBWeaponUser>();
		FBHitbox.Build(orAdd);
		if (player)
		{
			gameObject2.AddComponent<FBPlayer>();
		}
		else if (bot)
		{
			gameObject2.AddComponent<FBBot>();
		}
		else if (civ)
		{
			gameObject2.AddComponent<FBCivilian>();
		}
		orAdd.onDeath = OnDeathAnim;
		All.Add(orAdd);
		if (bot)
		{
			Bots.Add(orAdd);
		}
		if (civ)
		{
			Civs.Add(orAdd);
		}
		return orAdd;
	}

	private static void SetLayer(GameObject g, int l)
	{
		Transform[] componentsInChildren = g.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform in componentsInChildren)
		{
			if (!transform.GetComponent<FBHitbox>())
			{
				transform.gameObject.layer = l;
			}
		}
	}

	public static void Teleport(FBCharacter c, Vector3 p, float yawDeg)
	{
		CharacterController component = c.GetComponent<CharacterController>();
		if ((bool)component)
		{
			component.enabled = false;
		}
		c.transform.SetPositionAndRotation(p + Vector3.up * 0.05f, Quaternion.Euler(0f, yawDeg, 0f));
		if ((bool)component)
		{
			component.enabled = true;
		}
		c.velocity = Vector3.zero;
	}

	public FBCharacter MakeBot(string type, int team, Vector3 p, string ai, string forcedRole = null)
	{
		FBCharacter fBCharacter = Spawn(type, null, team, p, player: false, bot: true);
		fBCharacter.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(-170f, 170f), 0f);
		FBBot component = fBCharacter.GetComponent<FBBot>();
		string text = forcedRole ?? SQUAD_ROLES[UnityEngine.Random.Range(0, SQUAD_ROLES.Length)];
		float lane = text switch
		{
			"rusher" => FBUtil.Rnd(-4f, 4f), 
			"striker" => FBUtil.Rnd(-3f, 3f), 
			"baianogmbarril" => FBUtil.Rnd(-10f, 10f), 
			"halemph" => FBUtil.Rnd(-20f, 20f), 
			"halemph2" => FBUtil.Rnd(-26f, 26f), 
			"flanker_left" => FBUtil.Rnd(-22f, -14f), 
			"flanker_right" => FBUtil.Rnd(14f, 22f), 
			_ => FBUtil.Rnd(-8f, 8f), 
		};
		component.Setup(ai ?? "patrol", text, lane, p);
		fBCharacter.kits = (FBEconomy.EconMode(FBSession.mode) ? 1 : 2);
		if (text == "baianogmbarril")
		{
			fBCharacter.armor = 60f;
			fBCharacter.helmet = true;
		}
		if (team != 2)
		{
			FBEconomy.ArmBot(fBCharacter);
		}
		fBCharacter.combat = fBCharacter.weapon != null;
		if (state == "play")
		{
			Protect(fBCharacter);
		}
		return fBCharacter;
	}

	public void SpawnSquad(int team, int n, string ai)
	{
		for (int i = 0; i < n; i++)
		{
			MakeBot(typePool[team][i % typePool[team].Count], team, NextSpot(team), ai, SQUAD_ROLES[i % SQUAD_ROLES.Length]).transform.rotation = Quaternion.Euler(0f, teamLine[team].yaw, 0f);
		}
	}

	private List<Vector3> EnemySpawns(int en)
	{
		Vector3 mine = Cen(TeamSpawns(1 - en));
		Vector3 theirs = Cen(TeamSpawns(en));
		List<FBCharacter> friends = All.Where((FBCharacter a) => (bool)a && a.alive && a.team != en).ToList();
		List<Vector3> list = TeamSpawns(en).Where(Ok).ToList();
		List<Vector3> list2 = Map.bots.Where(Ok).ToList();
		if (list.Count <= 0)
		{
			if (list2.Count <= 0)
			{
				return TeamSpawns(en);
			}
			return list2;
		}
		return list.Concat(list2).ToList();
		static Vector3 Cen(List<Vector3> a)
		{
			return a.Aggregate(Vector3.zero, (Vector3 s, Vector3 p) => s + p.Flat()) / Mathf.Max(1, a.Count);
		}
		bool Ok(Vector3 p)
		{
			Vector3 q = p.Flat();
			if ((q - mine).magnitude > (q - theirs).magnitude * 1.35f + 20f && (q - mine).magnitude > 60f)
			{
				return friends.All((FBCharacter f) => (f.Pos.Flat() - q).magnitude > 45f);
			}
			return false;
		}
	}

	public void Reinforce(int n)
	{
		int num = 1 - FBSession.team;
		List<Vector3> list = EnemySpawns(num);
		for (int i = 0; i < n; i++)
		{
			if (list.Count <= 0)
			{
				break;
			}
			MakeBot(FBUtil.Pick(typePool[num]), num, FBUtil.Pick(list), "hunt").GetComponent<FBBot>().lastSeen = Player.Pos;
		}
		Feed("Reforços (" + TEAM_NAME[num] + ") chegando!");
	}

	public void Protect(FBCharacter a)
	{
		if (mode != null && mode.respawn && FBSession.mode != "treino")
		{
			a.protUntil = Time.time + FBMechanics.RollProtection();
		}
	}

	private void SpawnCivilians(int n)
	{
		List<string> list = (from _ in FBNpcAnims.Roster(FBSession.skin)
			orderby UnityEngine.Random.value
			select _).ToList();
		if (list.Count == 0)
		{
			return;
		}
		for (int i = 0; i < Mathf.Min(n, list.Count); i++)
		{
			Vector3? vector = FBNav.RandomPoint();
			if (vector.HasValue)
			{
				Vector3 valueOrDefault = vector.GetValueOrDefault();
				FBCharacter fBCharacter = Spawn(list[i], null, 2, valueOrDefault, player: false, bot: false, civ: true);
				fBCharacter.hp = 60f;
				fBCharacter.maxHp = 60f;
			}
		}
	}

	private void OnDeathAnim(FBCharacter dead, FBCharacter by)
	{
		Invoke2(1.1f, delegate
		{
			if ((bool)dead && (bool)dead.Hips)
			{
				float? num = FBWorld.GroundY(dead.Hips.position.x, dead.Hips.position.y + 0.5f, dead.Hips.position.z, 0.3f, 3f);
				if (num.HasValue)
				{
					float valueOrDefault = num.GetValueOrDefault();
					FBFx.BloodPool(new Vector3(dead.Hips.position.x, valueOrDefault, dead.Hips.position.z));
				}
			}
		});
	}

	public void OnKill(FBCharacter a, FBCharacter src, bool head, string why)
	{
		if (a.weapon != null && !a.isPlayer)
		{
			FBLoot.DropWeapon(a.weapon.id, a.Pos + new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f), 0f, UnityEngine.Random.Range(-0.5f, 0.5f)));
			a.SetWeaponVisible(v: false);
			if (UnityEngine.Random.value < 0.4f)
			{
				FBLoot.DropRandomAtt(a.weapon, a.Pos);
			}
		}
		if (mode != null && mode.econ && (bool)src && src != a && Hostile(src, a))
		{
			FBEconomy.Give(src, 250 + (head ? 100 : 0), head ? "HEADSHOT" : "ABATE");
		}
		if ((bool)src && src.isPlayer && a != src)
		{
			RewardKill(a.Name, head);
		}
		FBKillFeed.Kill(src, a, why, head, ((bool)src && src.weapon != null) ? src.weapon.id : null);
		if (!a.civ)
		{
			Tally.TryGetValue(a.Name, out (int, int, int) value);
			Tally[a.Name] = (a.team, value.Item2, value.Item3 + 1);
		}
		if ((bool)src && src != a && !src.civ)
		{
			Tally.TryGetValue(src.Name, out (int, int, int) value2);
			Tally[src.Name] = (src.team, value2.Item2 + 1, value2.Item3);
		}
		mode?.OnKill(a, src, head);
		TeamWipeCheck(a);
		if (a.isPlayer)
		{
			PlayerDie(src, why, head);
		}
		FBNet.I?.SendDie(a, src, head);
	}

	private void RewardKill(string name, bool head)
	{
		Stats.kills++;
		if (head)
		{
			Stats.heads++;
		}
		ks.Kill(Time.time);
		FBPass.AddXP(head ? 150 : 100, head ? "HEADSHOT" : "ABATE");
		FBWeaponUser component = Player.GetComponent<FBWeaponUser>();
		WeaponDef cur = component.Cur;
		string text = ((cur != null) ? cur.name.ToUpper() : "MÃOS LIVRES");
		if (cur != null)
		{
			component.reserve[component.cur] += Mathf.Max(10, cur.mag);
		}
		FBHUD.I?.Toast(head ? "HEADSHOT" : "ELIMINADO", name.ToUpper(), (cur != null) ? (text + " · +MUNIÇÃO") : text, head ? new Color(1f, 0.3f, 0.25f) : new Color(0.94f, 0.71f, 0.16f));
		streakN = ((!(streakT > 0f)) ? 1 : (streakN + 1));
		streakT = 4.5f;
		string[] array = new string[6] { "", "ABATE", "ABATE DUPLO", "ABATE TRIPLO", "ABATE QUÁDRUPLO", "MASSACRE" };
		FBHUD.I?.KillBanner(Stats.kills, (streakN < 2) ? (head ? "HEADSHOT!" : "ABATE") : ((streakN < array.Length) ? array[streakN] : "IMPARÁVEL"), name.ToUpper() + " · " + text, (streakN >= 2) ? new Color(0.96f, 0.67f, 0.09f) : (head ? new Color(1f, 0.35f, 0.29f) : Color.white));
	}

	private void TeamWipeCheck(FBCharacter victim)
	{
		if (!(FBSession.mode == "treino") && !FFA)
		{
			List<FBCharacter> list = Bots.Where((FBCharacter b) => (bool)b && b.team != FBSession.team).ToList();
			List<FBCharacter> source = All.Where((FBCharacter x) => (bool)x && !x.civ && x.team == FBSession.team).ToList();
			if (victim.team != FBSession.team && list.Count >= 2 && list.All((FBCharacter b) => !b.alive))
			{
				FBHUD.I?.WipeBanner("EQUIPE INIMIGA ELIMINADA", "TODOS OS INIMIGOS FORAM ABATIDOS", new Color(0.96f, 0.67f, 0.09f));
			}
			else if (victim.team == FBSession.team && source.All((FBCharacter m) => !m.alive))
			{
				FBHUD.I?.WipeBanner("SUA EQUIPE FOI ELIMINADA", "NENHUM ALIADO DE PÉ", new Color(1f, 0.29f, 0.23f));
			}
		}
	}

	private void PlayerDie(FBCharacter src, string why, bool head = false)
	{
		ks.Died();
		Stats.deaths++;
		FBAudio.Play2D("ie_knocked", 0.45f);
		FBVehicles.ExitIfDriving(Player);
		FBHUD.I?.Killcam(src ? src.Name.ToUpper() : ((why == "zone") ? "ZONA" : ((why == "fall") ? "QUEDA" : "—")), on: true);
		if (mode != null && !mode.respawn)
		{
			state = "spect";
			spect = All.FirstOrDefault((FBCharacter a) => (bool)a && a != Player && a.alive && a.team == Player.team && !a.civ);
			Invoke2(3.5f, delegate
			{
				FBHUD.I?.Killcam("", on: false);
			});
			return;
		}
		state = "dead";
		respawnT = ((mode != null && mode.autoRespawn > 0f) ? (mode.autoRespawn + 2.2f) : (-1f));
		Invoke2(2.2f, delegate
		{
			if (state == "dead")
			{
				FBHUD.I?.Killcam("", on: false);
				FBHUD.I?.DeathScreen(respawnT > 0f, Respawn, ToMenu, src, why, head);
			}
		});
	}

	public void Respawn()
	{
		if (!(state != "dead"))
		{
			Vector3 vector = (mode?.SpawnPoint() ?? ((teamLine[FBSession.team] != null) ? teamLine[FBSession.team].spots[0] : FBUtil.Pick(TeamSpawns(FBSession.team)))) + new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), 0f, UnityEngine.Random.Range(-1.5f, 1.5f));
			float yawDeg = SpawnYaw(vector);
			Player.Revive();
			Teleport(Player, vector, yawDeg);
			Player.hp = 100f;
			FBMed.ResetPlayer(2, FBSession.mode == "treino");
			FBWeaponUser component = Player.GetComponent<FBWeaponUser>();
			component.RefillAll(3);
			component.Equip(component.cur, instant: true);
			FBPlayer.I?.OnRespawn(yawDeg);
			state = "play";
			FBHUD.I?.CloseCards();
			FBCombat.EndKnock(Player);
			Protect(Player);
			FBNet.I?.SendSpawn(Player);
		}
	}

	public void ResetRound()
	{
		PickBases();
		FBLoot.Clear();
		bool flag = mode != null && mode.econ;
		bool flag2 = !Player.alive;
		// fisica 31/10: x.team < 2 deixa de fora atores de evento de rua (atirador do baculejo, time 2, civ=false); teamLine[2] lancava IndexOutOfRange
		foreach (FBCharacter item in new FBCharacter[1] { Player }.Concat(All.Where((FBCharacter x) => (bool)x && x != Player && !x.civ && x.team < 2)).ToList())
		{
			if (item.knocked || item.getUpT > 0f || item.sys != null)
			{
				FBCombat.EndKnock(item);
				item.getUpT = 0f;
				item.bleedAcc = 0f;
			}
			if (item.isRemote)
			{
				continue;
			}
			Vector3 p = NextSpot(item.team);
			if (flag && !item.alive)
			{
				item.armor = 0f;
				item.helmet = false;
				if (!item.isPlayer)
				{
					FBEconomy.UnarmBot(item);
				}
			}
			if (flag && !item.isPlayer)
			{
				item.kits = Mathf.Max(item.alive ? item.kits : 0, 1);
			}
			if (!item.alive || item.hp < 100f)
			{
				if (!item.alive)
				{
					item.Revive();
				}
				item.alive = true;
				item.hp = 100f;
			}
			Teleport(item, p, teamLine[item.team].yaw);
			if (!item.isPlayer)
			{
				item.GetComponent<FBBot>()?.ResetRound();
			}
		}
		FBPlayer.I?.OnRespawn(teamLine[FBSession.team].yaw);
		FBWeaponUser component = Player.GetComponent<FBWeaponUser>();
		if (flag && flag2)
		{
			component.Strip();
		}
		if (flag)
		{
			FBMed.ResetPlayer(Mathf.Max((!flag2) ? FBMed.kits : 0, 1), infinite: false);
		}
		component.RefillAll(4);
		spect = null;
		if (state == "spect" || state == "dead")
		{
			state = "play";
		}
		FBHUD.I?.Killcam("", on: false);
		FBHUD.I?.CloseCards();
		component.Equip(component.cur, instant: true);
	}

	public void OnLive()
	{
		if (mode != null && mode.econ && Player.alive && Player.GetComponent<FBWeaponUser>().Unarmed)
		{
			FBHUD.I?.Toast("VOCÊ ESTÁ DESARMADO", "PEGUE UMA ARMA DE UM INIMIGO ABATIDO", "Na próxima rodada, compre na loja (B)", new Color(1f, 0.3f, 0.25f));
			FBAudio.Synth("deny");
		}
	}

	public void EndMatch(bool win, string modeName, int a, int b, int place = 0, int total = 0)
	{
		if (state == "end")
		{
			return;
		}
		state = "end";
		freeze = true;
		if (win && Player.alive)
		{
			FBPlayer.I?.ForceThirdPerson();
			Player.PlayDance((FBSession.victory != null && (bool)FBRegistry.I.Clip(FBSession.victory)) ? FBSession.victory : FBEmotes.RandomVictory());
		}
		foreach (FBCharacter bot in Bots)
		{
			if ((bool)bot && bot.alive && bot.team == FBSession.team == win && UnityEngine.Random.value < 0.85f)
			{
				bot.PlayDance(FBEmotes.RandomVictory(bot.IsFemale));
			}
		}
		int reward = Stats.kills * 60 + (win ? 500 : 150);
		FBSession.money += reward;
		FBSession.xp += Stats.kills * 10 + (win ? 60 : 20);
		FBSession.Save();
		FBPass.AddXP(win ? 500 : 200, win ? "VITÓRIA" : "PARTIDA");
		Invoke2(win ? 4.2f : 1.8f, delegate
		{
			FBHUD.I?.VictoryCard(win, modeName, MapName, a, b, place, total, reward, delegate
			{
				SceneManager.LoadScene(SceneManager.GetActiveScene().name);
			}, ToMenu);
		});
	}

	public void ToMenu()
	{
		FBAudio.StopAllLoops();
		FBAmbience.Stop();
		FBNet.I?.Leave();
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
		SceneManager.LoadScene("Menu");
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBMatch.Update");

	private void Update()
	{
		if (!FBTempo.On)
		{
			Update__fbt();
			return;
		}
		long t0 = FBTempo.T0();
		Update__fbt();
		FBTempo.Soma(__fbt_Update, t0);
	}

	private void Update__fbt()
	{
		float deltaTime = Time.deltaTime;
		if (FBInput.PausePressed())
		{
			Paused = !Paused;
			FBHUD.I?.Pause(Paused, ToMenu);
			Cursor.lockState = ((!Paused && !FBGarage.Open_) ? CursorLockMode.Locked : CursorLockMode.None);
			Cursor.visible = Paused || FBGarage.Open_;
		}
		if (state == "loading" || mode == null)
		{
			return;
		}
		if (streakT > 0f)
		{
			streakT -= deltaTime;
		}
		if (state == "play" || state == "dead" || state == "spect")
		{
			mode.Update(deltaTime);
			mode.Hud();
		}
		if (state == "dead" && respawnT > 0f)
		{
			respawnT -= deltaTime;
			if (respawnT <= 0f)
			{
				Respawn();
			}
		}
		if ((bool)Player && Player.knocked)
		{
			FBCombat.KnockTick(Player, deltaTime);
		}
		string text = ks.Tick(Time.time);
		if (text != null)
		{
			FBHUD.I?.Toast("KILLSTREAK", FBKillstreak.STREAK[text].name + " DISPONÍVEL", "APERTE G PARA CHAMAR", new Color(0.96f, 0.67f, 0.09f));
		}
		if ((bool)Player && Player.alive && Player.Pos.y < -70f)
		{
			FBCombat.DamageActor(Player, 999f, head: false, null, "fall");
		}
		for (int num = timers.Count - 1; num >= 0; num--)
		{
			(float, Action) tuple = timers[num];
			float item = tuple.Item1;
			Action item2 = tuple.Item2;
			item -= deltaTime;
			if (item <= 0f)
			{
				timers.RemoveAt(num);
				item2();
			}
			else
			{
				timers[num] = (item, item2);
			}
		}
	}

	public void Invoke2(float t, Action f)
	{
		timers.Add((t, f));
	}

	private void OnDestroy()
	{
		mode?.Dispose();
		if (I == this)
		{
			I = null;
		}
	}
}
}
