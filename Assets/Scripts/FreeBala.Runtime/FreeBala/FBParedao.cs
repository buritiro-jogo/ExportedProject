using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace FreeBala
{
public static class FBParedao
{
	public class Wall
	{
		public FBCar car;

		public bool on;

		public bool playing;

		public bool trunk = true;

		public bool ended;

		public int owner = -1;

		public FBTrack tr;

		public float refPos;

		public double refT;

		public float vol = 0.85f;

		public float hue = UnityEngine.Random.value;

		public float level;

		public float bass;

		public float hb;

		public float bassAvg;

		public float kick;

		public Vector3 nodePos;

		public bool nodePosOk;

		public string ledM = "sinc";

		public string vu = "sinc";

		public Color ledC = new Color(1f, 0.165f, 0.427f);

		public AudioSource src;

		public FBAcTap tap;

		public readonly FBAcVoice[] dv = new FBAcVoice[2];

		public readonly FBAcVoice[] ev = new FBAcVoice[4];

		public FBAcReverb rev;

		public bool full;

		public AudioLowPassFilter lp;

		public string loadedH;

		public string wantH;

		public bool ready;

		public bool wait;

		public bool loading;

		public float st;

		public float ot;

		public float et;

		public float occK;

		public bool occ;

		public Bounds box;

		public bool boxOk;

		public readonly Echo[] echo = new Echo[4];

		public Vector3 echoFrom;

		public float echoT;

		public Stream stream;

		public string localPath;

		public AudioClip local;

		public bool loop;

		public List<Material> leds = new List<Material>();

		public List<Material> vus = new List<Material>();

		public List<(Transform t, Vector3 s)> spk = new List<(Transform, Vector3)>();

		public bool fxDone;
	}

	public class Stream
	{
		public FBTrack meta;

		public byte[][] chunks;

		public int n;

		public int got;

		public int next;

		public int asked = -1;

		public int owner;

		public bool done;

		public bool bad;

		public double last;
	}

	public class Echo
	{
		public Vector3 q;

		public float len;

		public float g;

		public float gT;

		public float cur;

		public bool live;
	}

	private class Runner : MonoBehaviour
	{
		private static readonly int __fbt_LateUpdate = FBTempo.Id("FBParedao.Runner.LateUpdate");

		private void LateUpdate()
		{
			if (!FBTempo.On)
			{
				LateUpdate__fbt();
				return;
			}
			long t0 = FBTempo.T0();
			LateUpdate__fbt();
			FBTempo.Soma(__fbt_LateUpdate, t0);
		}

		private void LateUpdate__fbt()
		{
			Transform ear = FBAudio.Ear;
			if ((bool)ear)
			{
				FBCar fBCar = FBVehicles.CarOf(FBMatch.Player);
				Tick(Time.deltaTime, ear.position, ((bool)fBCar && FBDrive.Cur == fBCar && InCabin(fBCar, ear.position)) ? fBCar : null);
			}
		}
	}

	public const int CHUNK = 11264;

	public const int WIN = 16;

	public const float HEAR = 380f;

	public const float HB = 3f;

	public const float SOUND_SPEED = 343f;

	public const long MAX_SHARE = 31457280L;

	public static readonly string[] LED_MODES = new string[5] { "off", "cor", "rgb", "grad", "sinc" };

	public static readonly string[] VU_MODES = new string[3] { "off", "rgb", "sinc" };

	private static readonly string[] SOM_MODS = new string[3] { "Caixa_Grave_03_04", "Caixa_Grave_01_04", "Caixa_Grave_03_02" };

	public static readonly HashSet<string> PICKUPS = new HashSet<string>
	{
		"S10_2017", "SaveiroG4", "SaveiroQuadrada", "SaveiroCross", "Montana", "Toro", "Amarok", "Hilux2023Conquest", "L200", "F250",
		"RAM3500"
	};

	public static Action<FBCar> OnEnded;

	public static Action<FBCar> OnNeedTrack;

	private static readonly Dictionary<FBCar, Wall> walls = new Dictionary<FBCar, Wall>();

	private static readonly Dictionary<string, Stream> streams = new Dictionary<string, Stream>();

	private static readonly HashSet<string> serving = new HashSet<string>();

	private static Runner runner;

	private const bool SIMPLE = false;

	public const float LEAD = 0.12f;

	private static double Now => Time.realtimeSinceStartupAsDouble;

	private static int MyId
	{
		get
		{
			if (!Online)
			{
				return 0;
			}
			return FBNet.I.myId;
		}
	}

	private static bool Online
	{
		get
		{
			if ((bool)FBNet.I)
			{
				return FBNet.I.open;
			}
			return false;
		}
	}

	private static Runner R
	{
		get
		{
			if (!runner)
			{
				GameObject gameObject = new GameObject("FBParedao");
				UnityEngine.Object.DontDestroyOnLoad(gameObject);
				runner = gameObject.AddComponent<Runner>();
			}
			return runner;
		}
	}

	public static event Action Changed;

	private static bool OkMime(string m)
	{
		if (m != null)
		{
			if (!m.StartsWith("audio/") && !(m == "video/mp4") && !(m == "video/webm"))
			{
				return m == "video/ogg";
			}
			return true;
		}
		return false;
	}

	private static bool Mine(Wall W)
	{
		return W.owner == MyId;
	}

	private static void Emit()
	{
		try
		{
			FBParedao.Changed?.Invoke();
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
	}

	public static Wall Get(FBCar car)
	{
		if (!walls.TryGetValue(car, out var value))
		{
			Dictionary<FBCar, Wall> dictionary = walls;
			Wall obj = new Wall
			{
				car = car
			};
			value = obj;
			dictionary[car] = obj;
		}
		return value;
	}

	public static Wall State(FBCar car)
	{
		if (!car || !walls.TryGetValue(car, out var value))
		{
			return null;
		}
		return value;
	}

	public static bool Has(FBCar car)
	{
		if ((bool)car)
		{
			return car.soundWallNode;
		}
		return false;
	}

	public static IEnumerable<Wall> List()
	{
		return walls.Values;
	}

	private static float Expected(Wall W)
	{
		return W.refPos + (W.playing ? ((float)(Now - W.refT)) : 0f);
	}

	private static bool InCabin(FBCar car, Vector3 ear)
	{
		Vector3 point = car.transform.InverseTransformPoint(ear);
		Bounds lbox = car.lbox;
		return lbox.Contains(point);
	}

	public static void Mount(FBCar car, string mod, Dictionary<string, object> d)
	{
		GameObject gameObject = (FBRegistry.I ? FBRegistry.I.Som(mod) : null);
		Dictionary<string, object> dictionary = Json.O(GameData.Som, mod);
		if (!gameObject || dictionary == null)
		{
			return;
		}
		Dictionary<string, object> o = Json.O(d, "som");
		List<object> list = Json.L(Json.O(d, "doors"), "mala");
		List<object> list2 = Json.L(o, "base");
		if (list == null && list2 == null)
		{
			return;
		}
		Vector3 vector = Vec(Json.L(dictionary, "min"));
		Vector3 vector2 = Vec(Json.L(dictionary, "max"));
		Transform transform = car.transform.Find("body/shell");
		GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject, transform ? transform : car.transform);
		gameObject2.name = "sound_wall";
		Collider[] componentsInChildren = gameObject2.GetComponentsInChildren<Collider>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			UnityEngine.Object.Destroy(componentsInChildren[i]);
		}
		float num = 1f;
		Dictionary<string, object> dictionary2 = Json.O(o, "bed");
		Vector3 vector3;
		if (dictionary2 != null && vector2.x - vector.x <= Json.F(dictionary2, "w") + 0.02f)
		{
			vector3 = new Vector3((vector.x + vector2.x) / 2f, Json.F(dictionary2, "floor") + 0.01f - vector.y, Json.F(dictionary2, "cab") - 0.04f + vector.z);
		}
		else
		{
			float num2 = 0f;
			foreach (object item in Json.L(d, "wheels") ?? new List<object>())
			{
				num2 = Mathf.Max(num2, 2f * Mathf.Abs(Json.Fv(Json.L(item, "p")[0])));
			}
			float b = vector2.x - vector.x;
			num = Mathf.Min(1f, num2 * 0.8f / Mathf.Max(0.01f, b));
			float num3 = ((list2 != null) ? Json.Fv(list2[1]) : (Json.Fv(list[1]) + 0.12f));
			float num4 = ((list != null) ? Mathf.Max(Json.Fv(list[2]) + 0.3f, ((list2 != null) ? Json.Fv(list2[2]) : (-1E+09f)) - num * (vector2.z - vector.z) / 2f) : Json.Fv(list2[2]));
			vector3 = new Vector3(num * (vector.x + vector2.x) / 2f, num3 - num * vector.y, num4 + num * vector2.z);
		}
		gameObject2.transform.localPosition = new Vector3(0f - vector3.x, vector3.y, vector3.z);
		gameObject2.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
		gameObject2.transform.localScale = Vector3.one * num;
		Wall wall = Get(car);
		wall.leds.Clear();
		wall.vus.Clear();
		wall.spk.Clear();
		Dress(gameObject2, wall);
		car.soundWallNode = gameObject2.transform;
		car.soundMod = mod;
		wall.fxDone = true;
	}

	public static GameObject Module(string mod, Wall W)
	{
		GameObject gameObject = (FBRegistry.I ? FBRegistry.I.Som(mod) : null);
		if (!gameObject)
		{
			return null;
		}
		GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject);
		gameObject2.name = "som_" + mod;
		Collider[] componentsInChildren = gameObject2.GetComponentsInChildren<Collider>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			UnityEngine.Object.Destroy(componentsInChildren[i]);
		}
		Dress(gameObject2, W);
		return gameObject2;
	}

	private static void Dress(GameObject o, Wall W)
	{
		Shader unlit = FBShaders.Unlit;
		Material material = new Material(unlit);
		material.SetColor("_BaseColor", new Color(0.13f, 0f, 0.03f));
		Material material2 = new Material(unlit);
		material2.SetColor("_BaseColor", new Color(0.07f, 0.07f, 0.07f));
		W.leds.Add(material);
		W.vus.Add(material2);
		Matrix4x4 worldToLocalMatrix = o.transform.worldToLocalMatrix;
		Bounds box = default(Bounds);
		bool flag = false;
		Renderer[] componentsInChildren = o.GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer renderer in componentsInChildren)
		{
			string text = Tag(renderer.transform);
			if (text == "led")
			{
				renderer.enabled = false;
			}
			else if (text == "vu")
			{
				renderer.sharedMaterial = material2;
			}
			else
			{
				if ((bool)renderer.transform.parent && Regex.IsMatch(renderer.transform.parent.name, "^spk\\d"))
				{
					continue;
				}
				MeshFilter component = renderer.GetComponent<MeshFilter>();
				Bounds bounds;
				Matrix4x4 matrix4x;
				if ((bool)component && (bool)component.sharedMesh)
				{
					bounds = component.sharedMesh.bounds;
					matrix4x = worldToLocalMatrix * renderer.transform.localToWorldMatrix;
				}
				else
				{
					bounds = renderer.bounds;
					matrix4x = worldToLocalMatrix;
				}
				foreach (Vector3 item5 in Corners(bounds))
				{
					Vector3 vector = matrix4x.MultiplyPoint3x4(item5);
					if (!flag)
					{
						box = new Bounds(vector, Vector3.zero);
						flag = true;
					}
					else
					{
						box.Encapsulate(vector);
					}
				}
			}
		}
		Transform[] componentsInChildren2 = o.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform in componentsInChildren2)
		{
			if (Regex.IsMatch(transform.name, "^spk\\d+$"))
			{
				W.spk.Add((transform, transform.localScale));
			}
		}
		if (flag)
		{
			W.box = box;
			W.boxOk = true;
			float num = 0.05f;
			float z = box.max.z + 0.015f;
			float x = box.size.x;
			float y = box.size.y;
			float x2 = box.center.x;
			float y2 = box.center.y;
			Transform transform2 = new GameObject("led").transform;
			transform2.SetParent(o.transform, worldPositionStays: false);
			(float, float, float, float)[] array = new(float, float, float, float)[4]
			{
				(x2, box.max.y - num / 2f, x, num),
				(x2, box.min.y + num / 2f, x, num),
				(box.min.x + num / 2f, y2, num, y),
				(box.max.x - num / 2f, y2, num, y)
			};
			for (int i = 0; i < array.Length; i++)
			{
				(float, float, float, float) tuple = array[i];
				float item = tuple.Item1;
				float item2 = tuple.Item2;
				float item3 = tuple.Item3;
				float item4 = tuple.Item4;
				GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
				UnityEngine.Object.Destroy(gameObject.GetComponent<Collider>());
				gameObject.name = "led_fita";
				gameObject.transform.SetParent(transform2, worldPositionStays: false);
				gameObject.transform.localPosition = new Vector3(item, item2, z);
				gameObject.transform.localScale = new Vector3(item3, item4, 0.02f);
				gameObject.GetComponent<Renderer>().sharedMaterial = material;
			}
		}
	}

	private static string Tag(Transform t)
	{
		if (t.name == "led" || t.name == "vu")
		{
			return t.name;
		}
		if ((bool)t.parent && (t.parent.name == "led" || t.parent.name == "vu"))
		{
			return t.parent.name;
		}
		return null;
	}

	private static IEnumerable<Vector3> Corners(Bounds b)
	{
		for (int i = 0; i < 8; i++)
		{
			yield return new Vector3(((i & 1) == 0) ? b.min.x : b.max.x, ((i & 2) == 0) ? b.min.y : b.max.y, ((i & 4) == 0) ? b.min.z : b.max.z);
		}
	}

	private static Vector3 Vec(List<object> l)
	{
		if (l != null)
		{
			return new Vector3(Json.Fv(l[0]), Json.Fv(l[1]), Json.Fv(l[2]));
		}
		return Vector3.zero;
	}

	public static string PickModule(Dictionary<string, object> d, Func<float> rng)
	{
		List<string> list = (from x in Json.L(Json.O(d, "som"), "fit") ?? new List<object>()
			select x as string into x
			where FBRegistry.I.Som(x)
			select x).ToList();
		if (list.Count <= 0)
		{
			return SOM_MODS[Mathf.Min(2, (int)(rng() * (float)SOM_MODS.Length))];
		}
		return list[Mathf.Min(list.Count - 1, (int)(rng() * (float)Mathf.Min(6, list.Count)))];
	}

	public static FBCar MineCar(Vector3 playerPos)
	{
		FBCar fBCar = FBVehicles.CarOf(FBMatch.Player);
		if ((bool)fBCar && Has(fBCar))
		{
			return fBCar;
		}
		FBCar result = null;
		float num = 40f;
		foreach (Wall value in walls.Values)
		{
			if (value.on && Mine(value) && !value.local && (bool)value.car)
			{
				float num2 = Vector3.Distance(value.car.transform.position, playerPos);
				if (num2 < num)
				{
					num = num2;
					result = value.car;
				}
			}
		}
		return result;
	}

	public static bool CanControl(FBCar car)
	{
		Wall wall = State(car);
		if (wall != null && wall.on && wall.owner >= 0)
		{
			return Mine(wall);
		}
		return true;
	}

	public static string OwnerName(FBCar car)
	{
		Wall wall = State(car);
		if (wall == null || wall.owner < 0 || Mine(wall))
		{
			return null;
		}
		return FBNet.I?.NameOf(wall.owner) ?? "OUTRO JOGADOR";
	}

	public static bool Toggle(FBCar car)
	{
		return SetOn(car, !(State(car)?.on ?? false));
	}

	public static bool SetOn(FBCar car, bool on, bool quiet = false)
	{
		if (!Has(car))
		{
			return false;
		}
		Wall wall = Get(car);
		_ = R;
		if (!CanControl(car))
		{
			Toast("PAREDÃO DE " + (OwnerName(car) ?? "OUTRO JOGADOR"), 1.4f);
			return false;
		}
		wall.on = on;
		wall.owner = MyId;
		if (on && wall.tr == null)
		{
			OnNeedTrack?.Invoke(car);
			if (wall.tr == null)
			{
				Toast("PAREDÃO LIGADO · ESCOLHA UMA MÚSICA NA CENTRAL (K)", 2.2f);
				Broadcast(wall);
				Emit();
				return true;
			}
		}
		if (!on)
		{
			wall.playing = false;
			StopPlayer(wall);
		}
		if (!quiet)
		{
			Toast(on ? "PAREDÃO LIGADO" : "PAREDÃO DESLIGADO", 1.2f);
		}
		Broadcast(wall);
		if (!on)
		{
			wall.owner = -1;
		}
		Emit();
		return true;
	}

	public static bool Play(FBCar car, FBTrack t, float at = 0f)
	{
		if (!Has(car) || t == null || !CanControl(car))
		{
			return false;
		}
		Wall wall = Get(car);
		_ = R;
		wall.on = true;
		wall.owner = MyId;
		wall.ended = false;
		wall.tr = new FBTrack
		{
			h = t.h,
			n = t.n,
			a = (t.a ?? ""),
			d = t.d,
			m = (t.m ?? "audio/mpeg"),
			s = t.s,
			url = t.url,
			path = t.path
		};
		wall.localPath = t.path;
		if (Online && (wall.tr.s > 31457280 || !OkMime(wall.tr.m)))
		{
			Toast($"FAIXA GRANDE DEMAIS PARA COMPARTILHAR (MÁX. {30L} MB) — SÓ VOCÊ OUVE", 3f);
		}
		wall.playing = true;
		wall.refPos = at;
		wall.refT = Now;
		StopPlayer(wall);
		Want(wall);
		Broadcast(wall);
		Emit();
		return true;
	}

	public static void Pause(FBCar car, bool p = true)
	{
		Wall wall = State(car);
		if (wall?.tr != null && Mine(wall))
		{
			wall.refPos = Expected(wall);
			wall.refT = Now;
			wall.playing = !p;
			if (!p && wall.ended)
			{
				wall.ended = false;
				wall.refPos = 0f;
			}
			Sync(wall, hard: true);
			Broadcast(wall);
			Emit();
		}
	}

	public static void Seek(FBCar car, float s)
	{
		Wall wall = State(car);
		if (wall?.tr != null && Mine(wall))
		{
			wall.refPos = Mathf.Max(0f, s);
			wall.refT = Now;
			wall.ended = false;
			Sync(wall, hard: true);
			Broadcast(wall);
			Emit();
		}
	}

	public static void SetVol(FBCar car, float v)
	{
		if (Has(car) && CanControl(car))
		{
			Wall wall = Get(car);
			wall.vol = Mathf.Clamp01(v);
			Broadcast(wall);
			Emit();
		}
	}

	public static void SetTrunk(FBCar car, bool open)
	{
		if (Has(car) && CanControl(car))
		{
			Wall wall = Get(car);
			wall.trunk = open;
			Broadcast(wall);
			Emit();
		}
	}

	public static void SetLed(FBCar car, string m, Color? c = null)
	{
		if (Has(car) && CanControl(car))
		{
			Wall wall = Get(car);
			wall.ledM = m ?? wall.ledM;
			if (c.HasValue)
			{
				Color valueOrDefault = c.GetValueOrDefault();
				wall.ledC = valueOrDefault;
			}
			Broadcast(wall);
			Emit();
		}
	}

	public static void SetVu(FBCar car, string m)
	{
		if (Has(car) && CanControl(car))
		{
			Wall wall = Get(car);
			wall.vu = m;
			Broadcast(wall);
			Emit();
		}
	}

	public static float Position(FBCar car)
	{
		Wall wall = State(car);
		if (wall == null)
		{
			return 0f;
		}
		return Mathf.Min(Expected(wall), (wall.tr != null && wall.tr.d > 0f) ? wall.tr.d : 1E+09f);
	}

	public static float Duration(FBCar car)
	{
		Wall wall = State(car);
		if (!(wall?.src) || !wall.src.clip)
		{
			return (wall?.tr?.d).GetValueOrDefault();
		}
		return wall.src.clip.length;
	}

	public static string Buffering(FBCar car)
	{
		Wall wall = State(car);
		if (wall == null)
		{
			return "";
		}
		Stream stream = wall.stream;
		if (wall.tr != null && wall.tr.ns && !Mine(wall))
		{
			return "FAIXA SÓ NO APARELHO DO DONO";
		}
		if (stream != null && stream.bad)
		{
			return "FALHA AO RECEBER A FAIXA";
		}
		if (!wall.wait && !wall.loading)
		{
			if (stream == null || stream.done)
			{
				return "";
			}
			return $"RECEBENDO {Mathf.RoundToInt(100f * (float)stream.got / (float)Mathf.Max(1, stream.n))}%";
		}
		return "SINCRONIZANDO…";
	}

	public static Wall PlayLocal(FBCar car, AudioClip clip, float vol = 0.85f, float at = 0f)
	{
		if (!car || !clip)
		{
			return null;
		}
		Wall wall = Get(car);
		_ = R;
		wall.local = clip;
		wall.loop = true;
		wall.on = true;
		wall.owner = MyId;
		wall.playing = true;
		wall.ended = false;
		wall.vol = vol;
		wall.trunk = true;
		wall.tr = new FBTrack
		{
			h = "local_" + clip.name,
			n = clip.name,
			d = clip.length,
			ns = true
		};
		wall.refPos = at;
		wall.refT = Now;
		StopPlayer(wall);
		Want(wall);
		return wall;
	}

	public static void StopLocal(FBCar car)
	{
		Wall wall = State(car);
		if (wall != null && (bool)wall.local)
		{
			StopPlayer(wall);
			walls.Remove(car);
		}
	}

	public static void Rebuilt(FBCar car)
	{
		Wall wall = State(car);
		if (wall != null && (bool)wall.src)
		{
			Transform transform = wall.src.transform;
			Transform parent = (car.soundWallNode ? car.soundWallNode : car.transform);
			transform.SetParent(parent, worldPositionStays: false);
			transform.localPosition = new Vector3(0f, 0.8f, 0f);
		}
	}

	public static void Reset()
	{
		foreach (Wall value in walls.Values)
		{
			StopPlayer(value);
		}
		walls.Clear();
		streams.Clear();
		serving.Clear();
		Emit();
	}

	public static void PeerLeft(int id)
	{
		foreach (Wall value in walls.Values)
		{
			if (value.owner == id)
			{
				value.on = false;
				value.owner = -1;
				StopPlayer(value);
			}
		}
		Emit();
	}

	private static void Toast(string s, float t)
	{
		FBHUD.I?.Center(s, t);
	}

	private static void MakePlayer(Wall W)
	{
		Transform parent = (W.car.soundWallNode ? W.car.soundWallNode : W.car.transform);
		GameObject gameObject = new GameObject("paredao_audio");
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		gameObject.transform.localPosition = new Vector3(0f, 0.8f, 0f);
		AudioSource audioSource = gameObject.AddComponent<AudioSource>();
		audioSource.playOnAwake = false;
		audioSource.dopplerLevel = 0f;
		audioSource.priority = 1;
		audioSource.outputAudioMixerGroup = FBAudio.Group(voice: false);
		audioSource.spatialBlend = 0f;
		audioSource.volume = 1f;
		audioSource.bypassListenerEffects = true;
		audioSource.bypassReverbZones = true;
		W.tap = gameObject.AddComponent<FBAcTap>();
		Transform transform = R.transform;
		W.dv[0] = FBAcVoice.Make(transform, W.tap, 0, "paredao_coluna_E");
		W.dv[1] = FBAcVoice.Make(transform, W.tap, 1, "paredao_coluna_D");
		for (int i = 0; i < W.ev.Length; i++)
		{
			W.ev[i] = FBAcVoice.Make(transform, W.tap, 2, "paredao_eco" + i);
			W.echo[i] = new Echo();
		}
		W.rev = FBAcReverb.Make(transform, W.tap);
		W.src = audioSource;
		W.loadedH = null;
		W.ready = false;
		W.echoT = 0f;
	}

	private static void StopPlayer(Wall W)
	{
		W.wantH = null;
		W.ready = false;
		W.loadedH = null;
		W.loading = false;
		if ((bool)W.src)
		{
			AudioClip clip = W.src.clip;
			W.src.Stop();
			UnityEngine.Object.Destroy(W.src.gameObject);
			if ((bool)clip && clip != W.local)
			{
				UnityEngine.Object.Destroy(clip);
			}
		}
		FBAcVoice[] dv = W.dv;
		foreach (FBAcVoice fBAcVoice in dv)
		{
			if ((bool)fBAcVoice)
			{
				UnityEngine.Object.Destroy(fBAcVoice.gameObject);
			}
		}
		dv = W.ev;
		foreach (FBAcVoice fBAcVoice2 in dv)
		{
			if ((bool)fBAcVoice2)
			{
				UnityEngine.Object.Destroy(fBAcVoice2.gameObject);
			}
		}
		if ((bool)W.rev)
		{
			UnityEngine.Object.Destroy(W.rev.gameObject);
		}
		W.src = null;
		W.tap = null;
		W.rev = null;
		for (int j = 0; j < 2; j++)
		{
			W.dv[j] = null;
		}
		for (int k = 0; k < W.ev.Length; k++)
		{
			W.ev[k] = null;
		}
	}

	private static void Feed(Wall W, string path)
	{
		if (!W.src)
		{
			MakePlayer(W);
		}
		if (!(W.loadedH == W.tr.h) && !W.loading)
		{
			W.loading = true;
			string h = W.tr.h;
			R.StartCoroutine(Load(W, path, h));
		}
	}

	private static IEnumerator Load(Wall W, string path, string h)
	{
		AudioType audioType = FBMediaLib.TypeOf(path);
		using UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, audioType);
		if (req.downloadHandler is DownloadHandlerAudioClip downloadHandlerAudioClip)
		{
			downloadHandlerAudioClip.streamAudio = false;
			downloadHandlerAudioClip.compressed = true;
		}
		yield return req.SendWebRequest();
		W.loading = false;
		if (W.tr == null || W.tr.h != h || !W.src)
		{
			yield break;
		}
		if (req.result != UnityWebRequest.Result.Success)
		{
			Debug.LogWarning("[paredão] " + req.error + " " + path);
			Toast("NÃO DEU PARA TOCAR ESTE FORMATO NESTA PLATAFORMA", 2f);
			yield break;
		}
		AudioClip content = DownloadHandlerAudioClip.GetContent(req);
		if ((bool)content)
		{
			content.name = W.tr.n ?? h;
			if ((bool)W.src.clip)
			{
				UnityEngine.Object.Destroy(W.src.clip);
			}
			W.src.clip = content;
			W.loadedH = h;
			W.ready = true;
			if (W.tr.d <= 0f)
			{
				W.tr.d = content.length;
			}
			Sync(W, hard: true);
		}
	}

	private static void Sync(Wall W, bool hard = false, float delay = 0f)
	{
		AudioSource src = W.src;
		if (!src || !W.ready || !src.clip)
		{
			return;
		}
		float num = Expected(W) - delay + 0.12f;
		float length = src.clip.length;
		if (W.loop)
		{
			num = Mathf.Repeat(num, length);
		}
		if (!W.playing)
		{
			if (src.isPlaying)
			{
				src.Pause();
			}
			if (num >= 0f && num < length && Mathf.Abs(src.time - num) > 0.3f)
			{
				src.time = num;
			}
			return;
		}
		if (!W.loop && num >= length - 0.05f)
		{
			if (src.isPlaying)
			{
				src.Pause();
			}
			return;
		}
		if (num < 0f)
		{
			if (src.isPlaying)
			{
				src.Pause();
			}
			W.wait = true;
			return;
		}
		float num2 = src.time - num;
		if (W.loop)
		{
			num2 = Mathf.Repeat(num2 + length / 2f, length) - length / 2f;
		}
		if (hard || !src.isPlaying || Mathf.Abs(num2) > 0.35f)
		{
			src.time = Mathf.Clamp(num, 0f, length - 0.01f);
			src.pitch = 1f;
		}
		else
		{
			src.pitch = 1f - Mathf.Clamp(num2 * 0.25f, -0.04f, 0.04f);
		}
		if (!src.isPlaying)
		{
			src.Play();
		}
		W.wait = false;
	}

	private static void Want(Wall W)
	{
		FBTrack tr = W.tr;
		if (tr == null)
		{
			return;
		}
		if ((bool)W.local)
		{
			if (!W.src)
			{
				MakePlayer(W);
			}
			W.src.clip = W.local;
			W.src.loop = W.loop;
			W.loadedH = tr.h;
			W.ready = true;
			Sync(W, hard: true);
			return;
		}
		if (!string.IsNullOrEmpty(W.localPath) && Mine(W) && File.Exists(W.localPath))
		{
			Feed(W, W.localPath);
			return;
		}
		FBTrack fBTrack = FBMediaLib.Find(tr.h);
		if (fBTrack != null && File.Exists(fBTrack.path))
		{
			W.stream = null;
			Feed(W, fBTrack.path);
		}
		else if (tr.url != null && tr.url.StartsWith("http"))
		{
			Feed(W, tr.url);
		}
		else if (!Mine(W))
		{
			Request(W, tr);
		}
	}

	private static void Request(Wall W, FBTrack t)
	{
		if (!t.ns && t.s > 0 && t.s <= 31457280 && OkMime(t.m) && t.h != null && Regex.IsMatch(t.h, "^[0-9a-f]{20}$"))
		{
			if (!streams.TryGetValue(t.h, out var value))
			{
				value = new Stream
				{
					meta = t,
					n = (int)((t.s + 11264 - 1) / 11264),
					owner = W.owner,
					last = Now
				};
				value.chunks = new byte[value.n][];
				streams[t.h] = value;
			}
			value.owner = W.owner;
			W.stream = value;
			Ask(value);
		}
	}

	private static void Ask(Stream S)
	{
		if (!S.done && S.asked != S.next)
		{
			S.asked = S.next;
			S.last = Now;
			FBNet.I?.SendEv("somreq", new Dictionary<string, object>
			{
				["to"] = S.owner,
				["h"] = S.meta.h,
				["i"] = S.next
			});
		}
	}

	private static void GotChunk(Dictionary<string, object> m)
	{
		string text = Json.S(m, "h");
		if (text == null || !streams.TryGetValue(text, out var value))
		{
			return;
		}
		int num = Json.I(m, "i");
		string text2 = Json.S(m, "d");
		if (num < 0 || num >= value.n || value.chunks[num] != null || Json.I(m, "id") != value.owner || text2 == null || (double)text2.Length > 15777.599999999999)
		{
			return;
		}
		try
		{
			value.chunks[num] = Convert.FromBase64String(text2);
		}
		catch
		{
			return;
		}
		value.got++;
		value.last = Now;
		while (value.next < value.n && value.chunks[value.next] != null)
		{
			value.next++;
		}
		if (value.next >= value.n)
		{
			value.done = true;
			byte[] array = new byte[((IEnumerable<byte[]>)value.chunks).Sum((Func<byte[], long>)((byte[] c) => c.Length))];
			int num2 = 0;
			byte[][] chunks = value.chunks;
			foreach (byte[] array2 in chunks)
			{
				Buffer.BlockCopy(array2, 0, array, num2, array2.Length);
				num2 += array2.Length;
			}
			if (array.Length != value.meta.s)
			{
				Debug.LogWarning($"[paredão] tamanho diferente {array.Length} {value.meta.s}");
				value.bad = true;
				return;
			}
			if (FBMediaLib.TrackId(array) != text)
			{
				Debug.LogWarning("[paredão] hash não confere — não vai para a biblioteca");
				value.bad = true;
				return;
			}
			FBTrack fBTrack = FBMediaLib.AddReceived(text, array, value.meta);
			foreach (Wall value2 in walls.Values)
			{
				if (value2.tr?.h == text)
				{
					value2.stream = null;
					value2.loadedH = null;
					Feed(value2, fBTrack.path);
				}
			}
			Emit();
		}
		else if (value.next >= Mathf.Max(0, value.asked) + 16)
		{
			Ask(value);
		}
	}

	private static IEnumerator Serve(int to, string h, int from)
	{
		string key = to + "|" + h;
		if (!serving.Add(key))
		{
			yield break;
		}
		try
		{
			Wall wall = walls.Values.FirstOrDefault((Wall x) => x.on && Mine(x) && x.tr?.h == h);
			if (wall == null || from < 0)
			{
				yield break;
			}
			string path = (File.Exists(wall.localPath ?? "") ? wall.localPath : FBMediaLib.Find(h)?.path);
			if (path == null || !File.Exists(path))
			{
				yield break;
			}
			long size = new FileInfo(path).Length;
			if (size > 31457280)
			{
				yield break;
			}
			int j = (int)((size + 11264 - 1) / 11264);
			for (int i = from; i < Mathf.Min(j, from + 16); i++)
			{
				byte[] array;
				using (FileStream fileStream = File.OpenRead(path))
				{
					fileStream.Position = (long)i * 11264L;
					int num = (int)Math.Min(11264L, size - fileStream.Position);
					array = new byte[num];
					int num2;
					for (int k = 0; k < num; k += num2)
					{
						num2 = fileStream.Read(array, k, num - k);
						if (num2 <= 0)
						{
							break;
						}
					}
				}
				FBNet.I?.SendEv("somchk", new Dictionary<string, object>
				{
					["to"] = to,
					["h"] = h,
					["i"] = i,
					["d"] = Convert.ToBase64String(array)
				});
				if ((i & 3) == 3)
				{
					yield return null;
				}
			}
		}
		finally
		{
			serving.Remove(key);
		}
	}

	private static Dictionary<string, object> Pack(Wall W)
	{
		FBTrack tr = W.tr;
		Dictionary<string, object> dictionary = null;
		if (tr != null)
		{
			dictionary = new Dictionary<string, object>
			{
				["h"] = tr.h,
				["n"] = tr.n,
				["a"] = tr.a ?? "",
				["d"] = Mathf.RoundToInt(tr.d),
				["m"] = tr.m,
				["s"] = tr.s
			};
			if (tr.url != null)
			{
				dictionary["url"] = tr.url;
			}
			if (tr.s > 31457280 || !OkMime(tr.m))
			{
				dictionary["ns"] = 1;
			}
		}
		return new Dictionary<string, object>
		{
			["car"] = W.car.syncId,
			["on"] = (W.on ? 1 : 0),
			["tr"] = dictionary,
			["at"] = Mathf.Round(Expected(W) * 1000f) / 1000f,
			["pl"] = (W.playing ? 1 : 0),
			["vol"] = W.vol,
			["tk"] = (W.trunk ? 1 : 0),
			["led"] = new Dictionary<string, object>
			{
				["m"] = W.ledM,
				["c"] = "#" + ColorUtility.ToHtmlStringRGB(W.ledC).ToLowerInvariant()
			},
			["vu"] = W.vu
		};
	}

	private static void Broadcast(Wall W)
	{
		if (Online && Mine(W) && !W.local && W.car.syncId != null)
		{
			FBNet.I.SendEv("som", Pack(W));
		}
		W.hb = 0f;
	}

	public static void OnNet(Dictionary<string, object> m)
	{
		string text = Json.S(m, "e");
		int myId = MyId;
		if (text == "somreq")
		{
			if (Json.I(m, "to") == myId)
			{
				R.StartCoroutine(Serve(Json.I(m, "id"), Json.S(m, "h"), Json.I(m, "i")));
			}
			return;
		}
		if (text == "somchk")
		{
			if (Json.I(m, "to") == myId)
			{
				GotChunk(m);
			}
			return;
		}
		object value;
		string sid = Json.S(m, "car") ?? ((!m.TryGetValue("car", out value)) ? null : value?.ToString());
		FBCar fBCar = FBVehicles.list.FirstOrDefault((FBCar c) => (bool)c && c.syncId == sid);
		if (!fBCar || !Has(fBCar))
		{
			return;
		}
		Wall wall = Get(fBCar);
		_ = R;
		if (Mine(wall) && wall.on)
		{
			return;
		}
		float num = Mathf.Min(0.5f, (FBNet.I ? FBNet.I.ping : 80f) / 1000f);
		string text2 = wall.tr?.h;
		wall.owner = ((wall.on = Json.I(m, "on") != 0) ? Json.I(m, "id") : (-1));
		wall.vol = Json.F(m, "vol", wall.vol);
		wall.trunk = Json.I(m, "tk") != 0;
		Dictionary<string, object> dictionary = Json.O(m, "led");
		if (dictionary != null)
		{
			wall.ledM = Json.S(dictionary, "m") ?? wall.ledM;
			if (ColorUtility.TryParseHtmlString(Json.S(dictionary, "c") ?? "", out var color))
			{
				wall.ledC = color;
			}
		}
		wall.vu = Json.S(m, "vu") ?? wall.vu;
		Dictionary<string, object> dictionary2 = Json.O(m, "tr");
		wall.tr = ((dictionary2 == null) ? null : new FBTrack
		{
			h = Json.S(dictionary2, "h"),
			n = Json.S(dictionary2, "n"),
			a = Json.S(dictionary2, "a"),
			d = Json.F(dictionary2, "d"),
			m = Json.S(dictionary2, "m"),
			s = (long)Json.F(dictionary2, "s"),
			url = Json.S(dictionary2, "url"),
			ns = (Json.I(dictionary2, "ns") != 0)
		});
		wall.localPath = null;
		wall.playing = Json.I(m, "pl") != 0;
		wall.refPos = Json.F(m, "at") + (wall.playing ? num : 0f);
		wall.refT = Now;
		wall.ended = false;
		if (!wall.on || wall.tr == null)
		{
			StopPlayer(wall);
		}
		else if (text2 != wall.tr.h)
		{
			StopPlayer(wall);
			wall.stream = null;
		}
		Emit();
	}

	private static void Tick(float dt, Vector3 camPos, FBCar insideCar)
	{
		int num = 0;
		foreach (Wall item in walls.Values.OrderBy((Wall w) => (!w.car) ? 1E+12f : (w.car.transform.position - camPos).sqrMagnitude).ToList())
		{
			FBCar car = item.car;
			if (!car)
			{
				StopPlayer(item);
				walls.Remove(car);
				continue;
			}
			if (item.on && Mine(item) && (item.hb += dt) > 3f)
			{
				Broadcast(item);
			}
			if (item.on && Mine(item) && item.playing && !item.loop && item.tr != null && item.tr.d > 0f && !item.ended && Expected(item) > item.tr.d + 0.25f)
			{
				item.ended = true;
				item.playing = false;
				OnEnded?.Invoke(car);
				Emit();
			}
			Transform soundWallNode = car.soundWallNode;
			Vector3 vector = (soundWallNode ? soundWallNode.position : car.transform.position);
			float num2 = Vector3.Distance(vector, camPos);
			if (!item.on || item.tr == null || !(num2 < 380f))
			{
				if ((bool)item.src)
				{
					StopPlayer(item);
				}
			}
			else
			{
				if ((!item.src || item.loadedH != item.tr.h) && item.wantH != item.tr.h)
				{
					item.wantH = item.tr.h;
					Want(item);
				}
				if ((bool)item.src)
				{
					if ((item.ot += dt) > 0.25f)
					{
						item.ot = 0f;
						item.occ = !(insideCar == car) && num2 > 4f && Occluded(camPos, vector + Vector3.up * 0.8f);
					}
					item.occK = Mathf.MoveTowards(item.occK, item.occ ? 1 : 0, dt * 3f);
					Acoustics(item, car, soundWallNode, vector, camPos, insideCar == car, dt, num++ < 2);
					float delay = 0f;
					if ((item.st += dt) > 0.5f || item.wait)
					{
						item.st = 0f;
						Sync(item, hard: false, delay);
					}
					if (item.stream != null && !item.stream.done && Now - item.stream.last > 4.0)
					{
						item.stream.asked = -1;
						Ask(item.stream);
					}
				}
			}
			float num3 = 0f;
			float num4 = 0f;
			if (item.playing && (bool)item.src && item.src.isPlaying && (bool)item.tap)
			{
				num4 = item.tap.bass;
				num3 = item.tap.level;
			}
			item.bass += (num4 - item.bass) * Mathf.Min(1f, dt * (float)((num4 > item.bass) ? 30 : 8));
			item.level += (num3 - item.level) * Mathf.Min(1f, dt * 12f);
			if (num2 > 140f)
			{
				continue;
			}
			bool on = item.on;
			item.hue = (item.hue + dt * (0.12f + item.bass * 0.8f)) % 1f;
			string text = (on ? item.ledM : "off");
			foreach (Material led in item.leds)
			{
				led.SetColor("_BaseColor", text switch
				{
					"off" => new Color(0.1f, 0.04f, 0.04f), 
					"cor" => item.ledC * (0.55f + item.bass * 0.9f), 
					"rgb" => Color.HSVToRGB(item.hue, 1f, 1f), 
					"grad" => Hsl((Hue(item.ledC) + Mathf.Sin(item.hue * (float)Math.PI * 2f) * 0.12f + 1f) % 1f, 1f, 0.3f + item.bass * 0.35f), 
					_ => Hsl((item.hue + item.bass * 0.4f) % 1f, 1f, 0.12f + item.bass * 0.55f), 
				});
			}
			string text2 = (on ? item.vu : "off");
			foreach (Material vu in item.vus)
			{
				vu.SetColor("_BaseColor", (text2 == "off") ? new Color(0.07f, 0.07f, 0.07f) : ((text2 == "rgb") ? Hsl((item.hue + 0.5f) % 1f, 1f, 0.45f) : Hsl(0.33f - Mathf.Min(0.33f, item.level * 0.9f), 1f, 0.15f + item.level * 0.6f)));
			}
			item.bassAvg += (item.bass - item.bassAvg) * Mathf.Min(1f, dt * 1.6f);
			float a = ((on && item.playing) ? Mathf.Clamp01((item.bass - item.bassAvg * 1.08f) / Mathf.Max(0.04f, item.bassAvg * 0.7f)) : 0f);
			item.kick = Mathf.Max(a, item.kick - dt * 7f);
			float num5 = item.kick * Mathf.Clamp01(item.vol * 1.2f);
			float num6 = ((on && item.playing) ? (item.bass * 0.25f) : 0f);
			foreach (var (transform, vector2) in item.spk)
			{
				if ((bool)transform)
				{
					transform.localScale = new Vector3(vector2.x * (1f + num5 * 0.07f), vector2.y * (1f + num5 * 0.07f), vector2.z * (1f + num6 * 0.6f + num5 * 1.6f));
				}
			}
			if ((bool)soundWallNode)
			{
				if (!item.nodePosOk)
				{
					item.nodePos = soundWallNode.localPosition;
					item.nodePosOk = true;
				}
				soundWallNode.localPosition = item.nodePos + new Vector3(0f, num5 * 0.006f, (0f - num5) * 0.004f);
			}
		}
	}

	private static float Dist(float d, float refD = 10f, float roll = 0.8f)
	{
		if (!(d <= refD))
		{
			return refD / (refD + roll * (d - refD));
		}
		return 1f;
	}

	private static float Air(float d)
	{
		return Mathf.Max(2400f, 20000f * Mathf.Exp((0f - d) / 170f));
	}

	private static float Fade(float d)
	{
		float num = Mathf.Clamp01((d - 120f) / 250f);
		return (1f - num) * (1f - num);
	}

	private static void Acoustics(Wall W, FBCar car, Transform node, Vector3 pos, Vector3 camPos, bool inCar, float dt, bool full)
	{
		Transform ear = FBAudio.Ear;
		if (!ear || !W.tap)
		{
			return;
		}
		Vector3 right = ear.right;
		Vector3 forward = ear.forward;
		Dictionary<string, object> o = Json.O(Json.O(GameData.Vehicles, car.id), "som");
		float a = ((!W.trunk && Json.F(o, "abafa", 1f) != 0f) ? 650 : 20000);
		float num = W.vol * W.vol * 0.95f * FBAudio.Master;
		W.tap.drive = Mathf.Max(0f, W.vol - 0.8f) * 5f;
		float occK = W.occK;
		Vector3 vector = (node ? node.forward : (W.local ? car.transform.forward : (-car.transform.forward)));
		Vector3 vector2;
		Vector3 vector3;
		if ((bool)node && W.boxOk)
		{
			Bounds box = W.box;
			float z = box.max.z;
			vector2 = node.TransformPoint(new Vector3(box.center.x + box.size.x * 0.27f, box.center.y, z));
			vector3 = node.TransformPoint(new Vector3(box.center.x - box.size.x * 0.27f, box.center.y, z));
		}
		else
		{
			Vector3 vector4 = pos + Vector3.up * 0.8f;
			Vector3 normalized = Vector3.Cross(Vector3.up, vector).normalized;
			vector2 = vector4 + normalized * 0.45f;
			vector3 = vector4 - normalized * 0.45f;
		}
		Vector3 vector5 = (vector2 + vector3) / 2f;
		float num2 = Mathf.Max(0.3f, Vector3.Distance(vector5, camPos));
		for (int i = 0; i < 2; i++)
		{
			FBAcVoice fBAcVoice = W.dv[i];
			if ((bool)fBAcVoice)
			{
				Vector3 vector6 = ((i == 0) ? vector2 : vector3);
				if (!full)
				{
					vector6 = vector5;
					fBAcVoice.chan = 2;
				}
				else
				{
					fBAcVoice.chan = i;
				}
				Vector3 vector7 = vector6 - camPos;
				float num3 = Mathf.Max(0.3f, vector7.magnitude);
				Vector3 vector8 = vector7 / num3;
				float value = Vector3.Angle(vector, -vector8);
				float num4 = Mathf.Lerp(1f, 0.55f, Mathf.InverseLerp(60f, 165f, value));
				float b = Mathf.Lerp(20000f, 1500f, Mathf.InverseLerp(50f, 170f, value));
				float cutoff = Mathf.Min(Mathf.Min(Air(num3), b), Mathf.Min(a, Mathf.Lerp(20000f, 650f, occK)));
				float num5 = num * Dist(num3) * Fade(num3) * num4 * Mathf.Lerp(1f, 0.32f, occK);
				if (inCar)
				{
					cutoff = Mathf.Min(a, 2600f);
					num5 = num * 0.85f;
				}
				fBAcVoice.gain = ((!full && i == 1) ? 0f : (num5 * (full ? 1f : 1.35f)));
				fBAcVoice.cutoff = cutoff;
				fBAcVoice.delay = 0.12f + (inCar ? 0f : (num3 / 343f));
				float num6 = Vector3.Dot(vector8, right);
				float num7 = Vector3.Dot(vector8, forward);
				fBAcVoice.pan = num6 * Mathf.Clamp01(num3 / 2.5f);
				fBAcVoice.behind = (inCar ? 0f : (Mathf.Clamp01(0f - num7) * 0.8f));
				fBAcVoice.width = (inCar ? 0.3f : 0.78f);
			}
		}
		int num8 = -1;
		if (full && !inCar)
		{
			if ((W.echoT -= dt) <= 0f || (W.echoFrom - vector5).sqrMagnitude > 1f)
			{
				W.echoT = 0.3f;
				W.echoFrom = vector5;
				num8 = FindEchoes(W, vector5 + vector * 0.4f + Vector3.up * 0.5f, vector, camPos, num2, num);
			}
			for (int j = 0; j < W.ev.Length; j++)
			{
				FBAcVoice fBAcVoice2 = W.ev[j];
				Echo echo = W.echo[j];
				if ((bool)fBAcVoice2 && echo != null)
				{
					float num9 = (echo.live ? echo.gT : 0f);
					if (echo.live && Mathf.Abs(echo.len - echo.cur) > 6f)
					{
						num9 = 0f;
					}
					echo.g = Mathf.MoveTowards(echo.g, num9, dt * 4f * Mathf.Max(0.05f, Mathf.Max(num9, echo.g)));
					if (echo.g < 0.002f && echo.live)
					{
						echo.cur = echo.len;
					}
					Vector3 vector9 = echo.q - camPos;
					float num10 = Mathf.Max(0.5f, vector9.magnitude);
					Vector3 lhs = vector9 / num10;
					fBAcVoice2.gain = echo.g * Mathf.Lerp(1f, 0.6f, occK);
					fBAcVoice2.delay = 0.12f + echo.cur / 343f;
					fBAcVoice2.cutoff = Mathf.Min(Air(echo.cur) * 0.75f, 9000f);
					fBAcVoice2.pan = Vector3.Dot(lhs, right);
					fBAcVoice2.behind = Mathf.Clamp01(0f - Vector3.Dot(lhs, forward)) * 0.8f;
					fBAcVoice2.width = 0.85f;
				}
			}
		}
		else
		{
			FBAcVoice[] ev = W.ev;
			foreach (FBAcVoice fBAcVoice3 in ev)
			{
				if ((bool)fBAcVoice3)
				{
					fBAcVoice3.gain = 0f;
				}
			}
		}
		if ((bool)W.rev)
		{
			W.rev.delay = 0.12f + num2 / 343f + 0.02f;
			W.rev.cutoff = Mathf.Min(Air(num2) * 0.6f, 5200f);
			W.rev.send = (full ? 1f : 0.6f) * num * 0.36f * Mathf.Sqrt(12f / (12f + num2)) * Fade(num2) * (inCar ? 0.2f : 1f) * Mathf.Lerp(1f, 0.8f, occK);
			if (num8 >= 0)
			{
				W.rev.rt60 = Mathf.Lerp(1f, 2.1f, Mathf.Clamp01((float)num8 / 6f));
			}
			else if (!full)
			{
				W.rev.rt60 = 1.4f;
			}
		}
	}

	private static int FindEchoes(Wall W, Vector3 S, Vector3 face, Vector3 L, float dDirect, float vol)
	{
		int layerMask = 256;
		int num = 0;
		List<(Vector3, float, float)> list = new List<(Vector3, float, float)>();
		for (int i = 0; i < 16; i++)
		{
			Vector3 direction = Quaternion.Euler(0f, (float)i * 22.5f, 0f) * Vector3.forward;
			if (!Physics.Raycast(S, direction, out var hitInfo, 110f, layerMask, QueryTriggerInteraction.Ignore) || Mathf.Abs(hitInfo.normal.y) > 0.35f)
			{
				continue;
			}
			num++;
			Vector3 normal = hitInfo.normal;
			normal.y = 0f;
			normal.Normalize();
			Vector3 vector = S - 2f * Vector3.Dot(S - hitInfo.point, normal) * normal;
			float num2 = Vector3.Dot(L - vector, normal);
			if (Mathf.Abs(num2) < 0.001f)
			{
				continue;
			}
			float num3 = Vector3.Dot(hitInfo.point - vector, normal) / num2;
			if (num3 <= 0.02f || num3 >= 0.98f)
			{
				continue;
			}
			Vector3 vector2 = vector + (L - vector) * num3;
			Vector3 vector3 = vector2 - S;
			float magnitude = vector3.magnitude;
			if (magnitude < 0.5f || (Physics.Raycast(S, vector3 / magnitude, out var hitInfo2, magnitude + 0.8f, layerMask, QueryTriggerInteraction.Ignore) && (hitInfo2.point - vector2).sqrMagnitude > 6.25f))
			{
				continue;
			}
			Vector3 vector4 = vector2 - L;
			float magnitude2 = vector4.magnitude;
			if (!(magnitude2 < 0.5f) && !Physics.Raycast(L, vector4 / magnitude2, magnitude2 - 0.7f, layerMask, QueryTriggerInteraction.Ignore))
			{
				float num4 = magnitude + magnitude2;
				if (!(num4 - dDirect < 5f) && !(num4 > 320f))
				{
					float num5 = Mathf.Lerp(1f, 0.5f, Mathf.InverseLerp(60f, 160f, Vector3.Angle(face, vector3)));
					list.Add((vector2, num4, vol * 0.45f * Dist(num4) * Fade(num4) * num5));
				}
			}
		}
		list.Sort(((Vector3 q, float len, float g) a, (Vector3 q, float len, float g) b) => b.g.CompareTo(a.g));
		Echo[] echo = W.echo;
		foreach (Echo echo2 in echo)
		{
			if (echo2 != null)
			{
				echo2.live = false;
			}
		}
		foreach (var item4 in list.Take(W.echo.Length))
		{
			Vector3 item = item4.Item1;
			float item2 = item4.Item2;
			float item3 = item4.Item3;
			Echo echo3 = null;
			float num6 = 16f;
			echo = W.echo;
			foreach (Echo echo4 in echo)
			{
				if (echo4 != null && !echo4.live && (echo4.q - item).sqrMagnitude < num6)
				{
					num6 = (echo4.q - item).sqrMagnitude;
					echo3 = echo4;
				}
			}
			if (echo3 == null)
			{
				echo3 = W.echo.FirstOrDefault((Echo E) => E != null && !E.live);
			}
			if (echo3 == null)
			{
				break;
			}
			if (echo3.g < 0.002f)
			{
				echo3.cur = item2;
			}
			echo3.q = item;
			echo3.len = item2;
			echo3.gT = item3;
			echo3.live = true;
		}
		return num;
	}

	private static void SimpleAudio(Wall W, FBCar car, Transform node, Vector3 pos, Vector3 camPos, bool inCar, float dt)
	{
		float d = Vector3.Distance(pos, camPos);
		float value = Vector3.Angle(node ? node.forward : (-car.transform.forward), camPos - pos);
		float num = Mathf.Lerp(1f, 0.5f, Mathf.InverseLerp(60f, 160f, value));
		W.src.volume = Mathf.Min(1f, W.vol * W.vol * 1.4f * Fade(d) * (inCar ? 1f : num) * Mathf.Lerp(1f, 0.5f, W.occK) * FBAudio.Master);
	}

	public static string Diag(FBCar car)
	{
		Wall W = State(car);
		if (W == null)
		{
			return "sem estado";
		}
		CultureInfo ic = CultureInfo.InvariantCulture;
		return string.Format("tocando {0} · tap {1} · colunas [{2}] [{3}] · ecos {4} · reverb {5} · prédio {6:0.0}", W.playing && (bool)W.src && W.src.isPlaying, W.tap ? W.tap.level.ToString("0.00", ic) : "-", V(W.dv[0]), V(W.dv[1]), string.Join(" ", W.ev.Select((FBAcVoice v, int i) => (W.echo[i] == null || !W.echo[i].live) ? "[-]" : $"[{V(v)} L{W.echo[i].len:0}m]")), W.rev ? ("s" + W.rev.send.ToString("0.000", ic) + " rt" + W.rev.rt60.ToString("0.0", ic)) : "-", W.occK);
		string V(FBAcVoice v)
		{
			if (!v)
			{
				return "-";
			}
			return string.Format("g{0} d{1}s c{2:0} p{3}", v.gain.ToString("0.000", ic), (v.delay - 0.12f).ToString("0.000", ic), v.cutoff, v.pan.ToString("0.00", ic));
		}
	}

	public static bool Occluded(Vector3 from, Vector3 to)
	{
		Vector3 vector = to - from;
		float num = vector.magnitude - 1.8f;
		if (num <= 0f)
		{
			return false;
		}
		return Physics.Raycast(from, vector.normalized, num, 256, QueryTriggerInteraction.Ignore);
	}

	private static float Hue(Color c)
	{
		Color.RGBToHSV(c, out var H, out var _, out var _);
		return H;
	}

	public static Color Hsl(float h, float s, float l)
	{
		float num = l + s * Mathf.Min(l, 1f - l);
		float s2 = ((num == 0f) ? 0f : (2f * (1f - l / num)));
		return Color.HSVToRGB(h, s2, num);
	}
}
}
