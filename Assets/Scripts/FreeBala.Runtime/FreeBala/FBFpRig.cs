using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public class FBFpRig : MonoBehaviour
{
	private class Spring
	{
		public float damping = 20f;

		public float stiffness = 200f;

		public float mass = 1f;

		public float speed = 1f;

		public Vector3 x;

		public Vector3 v;

		public void Set(Dictionary<string, object> s)
		{
			if (s != null)
			{
				damping = Json.F(s, "damping", 20f);
				stiffness = Json.F(s, "stiffness", 200f);
				mass = Json.F(s, "mass", 1f);
				speed = Json.F(s, "speed", 1f);
			}
		}

		public Vector3 Update(Vector3 target, float dt)
		{
			float num = dt * speed;
			while (num > 1E-06f)
			{
				float num2 = Mathf.Min(num, 0.004166667f);
				num -= num2;
				Vector3 vector = ((x - target) * (0f - stiffness) - v * damping) / mass;
				v += vector * num2;
				x += v * num2;
			}
			return x;
		}
	}

	private class CurveMotion
	{
		private Dictionary<string, object> ac;

		public readonly Spring sl = new Spring();

		public readonly Spring sr = new Spring();

		public Vector3 tl;

		public Vector3 tr;

		public void Set(Dictionary<string, object> a)
		{
			ac = a;
			sl.Set(Json.O(a, "locationSpring"));
			sr.Set(Json.O(a, "rotationSpring"));
		}

		public void Target(float t)
		{
			if (ac == null)
			{
				tl = (tr = Vector3.zero);
				return;
			}
			List<object> list = Json.L(ac, "locationCurves");
			List<object> list2 = Json.L(ac, "rotationCurves");
			float num = Json.F(ac, "locationMultiplier", 1f);
			float num2 = Json.F(ac, "rotationMultiplier", 1f);
			tl = new Vector3(EvalCurve(list?[0] as List<object>, t), EvalCurve(list?[1] as List<object>, t), EvalCurve(list?[2] as List<object>, t)) * num;
			tr = new Vector3(EvalCurve(list2?[0] as List<object>, t), EvalCurve(list2?[1] as List<object>, t), EvalCurve(list2?[2] as List<object>, t)) * num2;
		}

		public void Zero()
		{
			tl = (tr = Vector3.zero);
		}

		public (Vector3, Vector3) Update(float dt)
		{
			return (sl.Update(tl, dt), sr.Update(tr, dt));
		}
	}

	private class Pose
	{
		public Vector3[] P;

		public Quaternion[] Q;

		public Vector3[] S;

		public Pose(int n)
		{
			P = new Vector3[n];
			Q = new Quaternion[n];
			S = new Vector3[n];
		}

		public void CopyFrom(Pose o)
		{
			Array.Copy(o.P, P, P.Length);
			Array.Copy(o.Q, Q, Q.Length);
			Array.Copy(o.S, S, S.Length);
		}
	}

	private class GClip
	{
		public string name;

		public string refName;

		public float len;

		public float fps;

		public bool loop;

		public readonly List<(int bi, char k, float[] d, int w)> tracks = new List<(int, char, float[], int)>();

		public GClip(string n, Dictionary<string, object> c, Func<string, int> index)
		{
			name = n;
			len = Json.F(c, "len");
			fps = Json.F(c, "fps", 30f);
			loop = Json.B(c, "loop");
			refName = Json.S(c, "ref");
			foreach (object item2 in Json.L(c, "t") ?? new List<object>())
			{
				int num = index(Json.S(item2, "p"));
				if (num < 0)
				{
					continue;
				}
				char c2 = (Json.S(item2, "k") ?? "p")[0];
				float[] array = Json.Floats(Json.L(item2, "d"));
				int item = ((c2 == 'q') ? 4 : 3);
				switch (c2)
				{
				case 'p':
				{
					for (int j = 0; j < array.Length; j += 3)
					{
						array[j] = 0f - array[j];
					}
					break;
				}
				case 'q':
				{
					for (int i = 0; i < array.Length; i += 4)
					{
						array[i + 1] = 0f - array[i + 1];
						array[i + 2] = 0f - array[i + 2];
					}
					break;
				}
				}
				tracks.Add((num, c2, array, item));
			}
		}

		public void Sample(float t, Pose pose)
		{
			t = (loop ? ((t % len + len) % len) : Mathf.Clamp(t, 0f, len));
			float a = t * fps;
			foreach (var track in tracks)
			{
				int item = track.bi;
				char item2 = track.k;
				float[] item3 = track.d;
				int item4 = track.w;
				int num = item3.Length / item4;
				float num2 = Mathf.Min(a, num - 1);
				int num3 = (int)num2;
				int num4 = Mathf.Min(num3 + 1, num - 1);
				float t2 = num2 - (float)num3;
				int num5 = num3 * item4;
				int num6 = num4 * item4;
				if (item2 == 'q')
				{
					Quaternion a2 = new Quaternion(item3[num5], item3[num5 + 1], item3[num5 + 2], item3[num5 + 3]);
					Quaternion b = new Quaternion(item3[num6], item3[num6 + 1], item3[num6 + 2], item3[num6 + 3]);
					pose.Q[item] = Quaternion.Slerp(a2, b, t2);
					continue;
				}
				Vector3 vector = Vector3.Lerp(new Vector3(item3[num5], item3[num5 + 1], item3[num5 + 2]), new Vector3(item3[num6], item3[num6 + 1], item3[num6 + 2]), t2);
				if (item2 == 'p')
				{
					pose.P[item] = vector;
				}
				else
				{
					pose.S[item] = vector;
				}
			}
		}
	}

	private class Entry
	{
		public string s;

		public float t;

		public float w = 1f;

		public float r;

		public float speed = 1f;

		public bool end;

		public bool bolted;
	}

	private class Layer
	{
		public List<Entry> list;

		public Entry cur
		{
			get
			{
				List<Entry> obj = list;
				return obj[obj.Count - 1];
			}
		}

		public Layer(string s)
		{
			list = new List<Entry>
			{
				new Entry
				{
					s = s
				}
			};
		}

		public void Go(string s, float dur, float t0 = 0f)
		{
			if (dur <= 0f)
			{
				list = new List<Entry>
				{
					new Entry
					{
						s = s,
						t = t0
					}
				};
				return;
			}
			foreach (Entry item in list)
			{
				item.r = -1f / dur;
			}
			list.Add(new Entry
			{
				s = s,
				t = t0,
				w = 0f,
				r = 1f / dur
			});
		}

		public void Step(float dt)
		{
			foreach (Entry item in list)
			{
				item.w = Mathf.Clamp01(item.w + item.r * dt);
			}
			list.RemoveAll((Entry e) => !(e.w > 0f) && !(e.r > 0f));
			if (list.Count == 0)
			{
				list.Add(new Entry
				{
					s = "none"
				});
			}
		}
	}

	public struct St
	{
		public bool aiming;

		public bool moving;

		public bool running;

		public bool crouching;

		public bool jumped;

		public bool inAir;

		public float axisX;

		public float axisY;

		public float lookX;

		public float lean;

		public float pitch;

		public float height;

		public float movementRate;
	}

	private static Dictionary<string, object> J;

	private static readonly Dictionary<string, Dictionary<string, object>> raw = new Dictionary<string, Dictionary<string, object>>();

	private static readonly Dictionary<string, string> PCH = new Dictionary<string, string>
	{
		["idlePose"] = "A_FP_PCH_AR_01_Idle_Pose",
		["aimPose"] = "A_FP_PCH_AR_01_Aim_Pose",
		["idle"] = "A_FP_PCH_Idle",
		["aimIdle"] = "A_FP_PCH_Aim_Idle",
		["walkF"] = "A_FP_PCH_Walk_F_Additive",
		["walkB"] = "A_FP_PCH_Walk_B_Additive",
		["walkL"] = "A_FP_PCH_Walk_L_Additive",
		["walkR"] = "A_FP_PCH_Walk_R_Additive",
		["aimWalkF"] = "A_FP_PCH_Aim_Walk_F_Additive",
		["walkPose"] = "A_FP_PCH_Walk_F_Pose_Additive",
		["run"] = "A_FP_PCH_Run_02_Additive",
		["fire"] = "A_FP_PCH_AR_Fire",
		["aimFire"] = "A_FP_PCH_AR_Aim_Fire",
		["fireEmpty"] = "A_FP_PCH_Fire_Empty",
		["bolt"] = "A_FP_PCH_Sniper_01_Reload_Bolt",
		["boltAim"] = "A_FP_PCH_Sniper_01_Reload_Bolt_Aim",
		["reload"] = "A_FP_PCH_AR_01_Reload",
		["aimReload"] = "A_FP_PCH_AR_01_Aim_Reload",
		["reloadEmpty"] = "A_FP_PCH_AR_01_Reload_Empty",
		["aimReloadEmpty"] = "A_FP_PCH_AR_01_Aim_Reload_Empty",
		["rOpen"] = "A_FP_PCH_Sniper_01_Reload_Open",
		["rInsert"] = "A_FP_PCH_Sniper_01_Reload_Insert_Additive",
		["rClose"] = "A_FP_PCH_Sniper_01_Reload_Close",
		["inspect"] = "A_FP_PCH_AR_01_Inspect",
		["holster"] = "A_FP_PCH_AR_01_Holster",
		["unholster"] = "A_FP_PCH_AR_01_Unholster",
		["grenadeAdd"] = "A_FP_PCH_Grenade_Throw_Additive",
		["grenade"] = "A_FP_PCH_Grenade_Throw",
		["knife"] = "A_FP_PCH_Knife_Attack_01",
		["aimIn"] = "A_FP_PCH_Aim_In_Transition",
		["aimOut"] = "A_FP_PCH_Aim_Out_Transition",
		["tactical"] = "A_FP_PCH_AG14W_Sprint_Tactical_F_Loop_Aligned"
	};

	private static readonly Dictionary<string, string> WEP = new Dictionary<string, string>
	{
		["fire"] = "A_WEP_AR_01_Fire",
		["reload"] = "A_FP_WEP_AR_01_Reload",
		["reloadEmpty"] = "A_FP_WEP_AR_01_Reload_Empty",
		["bolt"] = "A_FP_WEP_Sniper_01_Reload_Bolt",
		["boltAim"] = "A_FP_WEP_Sniper_01_Reload_Bolt_Aim",
		["rOpen"] = "A_FP_WEP_Sniper_01_Reload_Open",
		["rInsert"] = "A_FP_WEP_Sniper_01_Reload_Insert",
		["rClose"] = "A_FP_WEP_Sniper_01_Reload_Close"
	};

	public Transform pitchNode;

	public Transform motionNode;

	public Transform camNode;

	public Transform inv;

	private Transform[] A;

	private Dictionary<string, int> Amap;

	private Pose rest;

	private Pose pose;

	private Pose tmp;

	private Pose tmp2;

	private Pose dT;

	private int[] bonesA;

	private readonly Dictionary<string, GClip> clips = new Dictionary<string, GClip>();

	private Dictionary<string, object> over = new Dictionary<string, object>();

	private readonly Dictionary<string, Pose> refs = new Dictionary<string, Pose>();

	private float aiming;

	private float h;

	private float v;

	private float movement;

	private float turning;

	private float leanFwd;

	private float leanP;

	private float rateF = 1f;

	private float rateB = 0.9f;

	private float rateS = 0.9f;

	private float playRate = 1f;

	private bool aimSt;

	private bool runSt;

	private bool crouchSt;

	private readonly Layer loco = new Layer("loco");

	private readonly Layer overlay = new Layer("none");

	private readonly Layer actions = new Layer("none");

	private readonly Layer holsterL = new Layer("unholster");

	private readonly Layer armL = new Layer("none");

	private readonly Layer armR = new Layer("none");

	private readonly Layer wAim = new Layer("none");

	private readonly Layer wRun = new Layer("none");

	private readonly Layer wCrouch = new Layer("none");

	private readonly Layer tac = new Layer("none");

	private GameObject weapon;

	private WeaponDef def;

	private string wid;

	private Transform[] W;

	private Dictionary<string, int> Wmap;

	private Pose wrest;

	private Pose wpose;

	private readonly Dictionary<string, GClip> wclips = new Dictionary<string, GClip>();

	private int[] bonesW;

	private (GClip c, float t)? weaponAnim;

	private readonly Dictionary<GClip, Pose> wref = new Dictionary<GClip, Pose>();

	private Dictionary<string, object> IA = new Dictionary<string, object>();

	private Transform[] armLb;

	private Transform[] armRb;

	private Transform effL;

	private Transform effR;

	private Transform knifeMesh;

	private Transform sight;

	private Transform sightSock;

	private Transform pipNode;

	private Dictionary<string, object> scope;

	private Transform muzzleT;

	private Camera pipCam;

	private RenderTexture pipRT;

	private bool ironSight;

	private readonly CurveMotion recoil = new CurveMotion();

	private readonly CurveMotion jump = new CurveMotion();

	private readonly CurveMotion land = new CurveMotion();

	private readonly CurveMotion fall = new CurveMotion();

	private readonly CurveMotion leanItem = new CurveMotion();

	private readonly CurveMotion leanCam = new CurveMotion();

	private int shots;

	private float sinceShot = 9f;

	private float jumpT = 9f;

	private float landT = 9f;

	private float airT;

	private bool wasAir;

	private readonly Spring offL = new Spring();

	private readonly Spring offR = new Spring();

	private readonly Spring scopeL = new Spring
	{
		damping = 25f,
		stiffness = 300f
	};

	private Vector3 motionBase;

	private Quaternion motionBaseQ;

	private float camH;

	public Vector3 camLeanLoc;

	public Vector3 camLeanRot;

	public Quaternion recoilQ = Quaternion.identity;

	public bool ok;

	public readonly List<string> events = new List<string>();

	private int cycLeft;

	private Action onInsert;

	private Vector3 sightCorr;

	private Vector3 gunLast;

	private static Dictionary<string, object> D => GameData.FP;

	public Transform Muzzle
	{
		get
		{
			if (!muzzleT)
			{
				return inv;
			}
			return muzzleT;
		}
	}

	private bool Pistol
	{
		get
		{
			if (def != null)
			{
				return def.cls == "pistola";
			}
			return false;
		}
	}

	public bool Busy
	{
		get
		{
			if (!(actions.cur.s != "none"))
			{
				return armL.cur.s != "none";
			}
			return true;
		}
	}

	public string ActionState => actions.cur.s;

	public Vector3? SightWorld
	{
		get
		{
			if (!sightSock)
			{
				return null;
			}
			return sightSock.position;
		}
	}

	// v18 (thread de física): o wanims.json tem ~12 MB. Num exe de teste o TextAsset.text deu OutOfMemoryException dentro
	// do FBPlayer.Awake, o J ficou pela metade e o jogador ficou parado, com a câmera embaixo do mapa. Agora o J só é
	// guardado quando os dois arquivos carregam; na falta de memória tenta de novo depois de um GC e, se falhar, o J fica
	// nulo (a próxima criação tenta outra vez) e o jogo segue sem os braços de primeira pessoa.
	private static bool LoadJ()
	{
		if (J != null)
		{
			return true;
		}
		Dictionary<string, object> j = new Dictionary<string, object>();
		string[] array = new string[2] { "FP/wanims", "FP/fp_anims" };
		for (int i = 0; i < array.Length; i++)
		{
			Dictionary<string, object> dictionary = null;
			for (int tentativa = 0; tentativa < 2; tentativa++)
			{
				try
				{
					TextAsset textAsset = Resources.Load<TextAsset>(array[i]);
					dictionary = (textAsset ? (Json.Parse(textAsset.text) as Dictionary<string, object>) : null);
					break;
				}
				catch (OutOfMemoryException)
				{
					Debug.LogWarning("[FreeBala][fp] faltou memória lendo " + array[i] + (tentativa == 0 ? ": tentando de novo depois do GC" : ": seguindo sem os braços de primeira pessoa"));
					GC.Collect();
					GC.WaitForPendingFinalizers();
					if (tentativa == 1)
					{
						return false;
					}
				}
			}
			if (dictionary == null)
			{
				continue;
			}
			foreach (KeyValuePair<string, object> item in dictionary)
			{
				j[item.Key] = item.Value;
			}
		}
		J = j;
		return true;
	}

	private static string San(string n)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (char c in n)
		{
			if (c == ' ')
			{
				stringBuilder.Append('_');
			}
			else if ("[].:/".IndexOf(c) < 0)
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}

	private static float EvalCurve(List<object> k, float t)
	{
		if (k == null || k.Count == 0)
		{
			return 0f;
		}
		if (t <= K(0, 0))
		{
			return K(0, 1);
		}
		int num = k.Count - 1;
		if (t >= K(num, 0))
		{
			return K(num, 1);
		}
		int l;
		for (l = 0; l < num - 1 && t > K(l + 1, 0); l++)
		{
		}
		float num2 = K(l, 0);
		float num3 = K(l, 1);
		float num4 = K(l, 3);
		float num5 = K(l + 1, 0);
		float num6 = K(l + 1, 1);
		float num7 = K(l + 1, 2);
		float num8 = num5 - num2;
		if (!float.IsFinite(num4) || !float.IsFinite(num7) || Mathf.Abs(num4) > 1E+29f || Mathf.Abs(num7) > 1E+29f)
		{
			return num3;
		}
		float num9 = (t - num2) / num8;
		float num10 = num9 * num9;
		float num11 = num10 * num9;
		return (2f * num11 - 3f * num10 + 1f) * num3 + (num11 - 2f * num10 + num9) * num8 * num4 + (-2f * num11 + 3f * num10) * num6 + (num11 - num10) * num8 * num7;
		float K(int i, int j)
		{
			return Json.Fv((k[i] as List<object>)[j]);
		}
	}

	public static FBFpRig Create(Transform parent)
	{
		bool flag = LoadJ();
		GameObject gameObject = (FBRegistry.I ? FBRegistry.I.fpArms : null);
		if (!flag || !gameObject || J == null || J.Count == 0)
		{
			return null;
		}
		GameObject gameObject2 = new GameObject("FP_Rig");
		gameObject2.transform.SetParent(parent, worldPositionStays: false);
		FBFpRig fBFpRig = gameObject2.AddComponent<FBFpRig>();
		fBFpRig.Build(UnityEngine.Object.Instantiate(gameObject, gameObject2.transform));
		if (!fBFpRig.ok)
		{
			return null;
		}
		return fBFpRig;
	}

	private void Build(GameObject arms)
	{
		pitchNode = arms.transform;
		float y = Json.F(Json.O(D, "controller"), "m_Height", 1.8f);
		pitchNode.localPosition = new Vector3(0f, y, 0f);
		camH = y;
		(Transform[], Dictionary<string, int>) tuple = Index(arms.transform);
		A = tuple.Item1;
		Amap = tuple.Item2;
		motionNode = Get("Character Root Motion");
		camNode = Get("Camera");
		inv = Get("P_LPSP_Inventory");
		knifeMesh = Get("SM_Knife_01");
		Transform transform = Get("RadioSet");
		if ((bool)knifeMesh)
		{
			knifeMesh.gameObject.SetActive(value: false);
		}
		if ((bool)transform)
		{
			transform.gameObject.SetActive(value: false);
		}
		if (!motionNode || !camNode || !inv)
		{
			Debug.LogWarning("[FreeBala] rig FP incompleto");
			return;
		}
		Renderer[] componentsInChildren = arms.GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer obj in componentsInChildren)
		{
			obj.shadowCastingMode = ShadowCastingMode.Off;
			obj.gameObject.layer = 14;
		}
		int n2 = A.Length;
		rest = new Pose(n2);
		pose = new Pose(n2);
		tmp = new Pose(n2);
		tmp2 = new Pose(n2);
		dT = new Pose(n2);
		Read(A, rest);
		bonesA = J.Keys.Where((string k) => k.StartsWith("A_FP_PCH")).SelectMany((string k) => Clip(k)?.tracks.Select(((int bi, char k, float[] d, int w) t) => t.bi) ?? Enumerable.Empty<int>()).Distinct()
			.ToArray();
		armLb = new Transform[3]
		{
			Get("upperarm_l"),
			Get("lowerarm_l"),
			Get("hand_l")
		};
		armRb = new Transform[3]
		{
			Get("upperarm_r"),
			Get("lowerarm_r"),
			Get("hand_r")
		};
		effL = Get("Effector Hand Left");
		effR = Get("Effector Hand Right");
		Dictionary<string, object> o = Json.O(Json.O(D, "itemFeel"), "standing");
		recoil.Set(Json.O(Json.O(D, "weaponRecoil"), "standingState"));
		jump.Set(Json.O(o, "jumpingCurves"));
		land.Set(Json.O(o, "landingCurves"));
		fall.Set(Json.O(o, "fallingCurves"));
		leanItem.Set(Json.O(Json.O(D, "leaning"), "itemStanding"));
		leanCam.Set(Json.O(Json.O(D, "leaning"), "cameraStanding"));
		motionBase = motionNode.localPosition;
		motionBaseQ = motionNode.localRotation;
		ok = true;
		Transform Get(string n)
		{
			if (!Amap.TryGetValue(San(n), out var value))
			{
				return null;
			}
			return A[value];
		}
	}

	private static (Transform[], Dictionary<string, int>) Index(Transform root)
	{
		List<Transform> list = new List<Transform>();
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		Transform[] componentsInChildren = root.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform in componentsInChildren)
		{
			if (!(transform == root))
			{
				string key = San(transform.name);
				if (!dictionary.ContainsKey(key))
				{
					dictionary[key] = list.Count;
					list.Add(transform);
				}
			}
		}
		return (list.ToArray(), dictionary);
	}

	private static void Read(Transform[] I, Pose p)
	{
		for (int i = 0; i < I.Length; i++)
		{
			p.P[i] = I[i].localPosition;
			p.Q[i] = I[i].localRotation;
			p.S[i] = I[i].localScale;
		}
	}

	private static void Write(Transform[] I, Pose p, int[] bones)
	{
		foreach (int num in bones)
		{
			Transform obj = I[num];
			obj.localPosition = p.P[num];
			obj.localRotation = p.Q[num];
			obj.localScale = p.S[num];
		}
	}

	private GClip Clip(string n)
	{
		if (n == null)
		{
			return null;
		}
		if (!clips.TryGetValue(n, out var value))
		{
			if (!J.TryGetValue(n, out var value2) || !(value2 is Dictionary<string, object> c))
			{
				return null;
			}
			value = (clips[n] = new GClip(n, c, (string p) => (!Amap.TryGetValue(p, out var value3)) ? (-1) : value3));
		}
		return value;
	}

	private GClip C(string key)
	{
		string text = PCH[key];
		string text2 = Json.S(over, text);
		return ((text2 != null) ? Clip(text2) : null) ?? Clip(text);
	}

	private Pose Ref(string key)
	{
		string text = PCH[key];
		string key2 = text + "|" + wid;
		if (refs.TryGetValue(key2, out var value))
		{
			return value;
		}
		GClip gClip = C(key);
		object value2;
		string rb = (J.TryGetValue(text, out value2) ? Json.S(value2, "ref") : null);
		bool flag = Json.S(over, text) != null && Clip(Json.S(over, text)) != null;
		value = new Pose(A.Length);
		value.CopyFrom(rest);
		if (rb == "__end")
		{
			gClip?.Sample(gClip.len, value);
		}
		else if (rb != null)
		{
			string key3 = PCH.FirstOrDefault((KeyValuePair<string, string> x) => x.Value == rb).Key;
			((flag && key3 != null) ? C(key3) : (Clip(rb) ?? ((key3 != null) ? C(key3) : null)))?.Sample(0f, value);
		}
		else
		{
			gClip?.Sample(0f, value);
		}
		return refs[key2] = value;
	}

	public void SetWeapon(WeaponDef d)
	{
		if ((bool)weapon)
		{
			UnityEngine.Object.Destroy(weapon);
		}
		def = d;
		ironSight = false;
		sightSock = (pipNode = null);
		wid = d.id;
		refs.Clear();
		weaponAnim = null;
		wref.Clear();
		weapon = FBWeaponModel.Make(d, inv);
		weapon.transform.localPosition = Vector3.zero;
		weapon.transform.localRotation = Quaternion.identity;
		weapon.transform.localScale = Vector3.one;
		Renderer[] componentsInChildren = weapon.GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer obj in componentsInChildren)
		{
			obj.shadowCastingMode = ShadowCastingMode.Off;
			obj.gameObject.layer = 14;
		}
		Dictionary<string, object> o = Json.O(GameData.WeaponsRaw, d.id);
		over = Json.O(Json.O(o, "pch"), "map") ?? new Dictionary<string, object>();
		(Transform[], Dictionary<string, int>) tuple = Index(weapon.transform);
		W = tuple.Item1;
		Wmap = tuple.Item2;
		wrest = new Pose(W.Length);
		wpose = new Pose(W.Length);
		Read(W, wrest);
		wclips.Clear();
		Dictionary<string, object> o2 = Json.O(Json.O(o, "wep"), "map");
		foreach (KeyValuePair<string, string> item in WEP)
		{
			string text = Json.S(o2, item.Value) ?? item.Value;
			if (J.TryGetValue(text, out var value) && value is Dictionary<string, object> c2)
			{
				wclips[item.Key] = new GClip(text, c2, (string p) => (!Wmap.TryGetValue(p, out var value2)) ? (-1) : value2);
			}
		}
		bonesW = wclips.Values.SelectMany((GClip c) => c.tracks.Select(((int bi, char k, float[] d, int w) t) => t.bi)).Distinct().ToArray();
		Dictionary<string, object> o3 = Json.O(o, "itemAnim");
		IA = new Dictionary<string, object>
		{
			["itemOffsets"] = Json.O(o3, "itemOffsets") ?? Json.O(D, "itemOffsets"),
			["leaning"] = Json.O(o3, "leaningData") ?? Json.O(D, "leaning"),
			["weaponRecoil"] = Json.O(o3, "weaponRecoilData") ?? Json.O(D, "weaponRecoil")
		};
		scope = FBWeaponModel.Scope(d);
		sight = null;
		muzzleT = null;
		Dictionary<string, object> o4 = Json.O(o, "att");
		FBWeaponModel.Choice choice = FBWeaponModel.Of(d);
		Transform[] componentsInChildren2 = weapon.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform in componentsInChildren2)
		{
			if (transform.name.StartsWith("SOCKET_Muzzle") && transform.gameObject.activeInHierarchy)
			{
				muzzleT = transform;
			}
		}
		if (choice.scope >= 0)
		{
			string text2 = Json.S(Json.L(o4, "scopes")?[choice.scope], "node");
			componentsInChildren2 = weapon.GetComponentsInChildren<Transform>(includeInactive: true);
			foreach (Transform transform2 in componentsInChildren2)
			{
				if (transform2.name == text2)
				{
					sight = transform2;
				}
			}
		}
		else if ((Json.S(Json.O(o, "pch"), "name") ?? "").Contains("Sniper"))
		{
			string text3 = Json.S(o4, "scopeDefault");
			componentsInChildren2 = weapon.GetComponentsInChildren<Transform>(includeInactive: true);
			foreach (Transform transform3 in componentsInChildren2)
			{
				if (transform3.name == text3 && transform3.gameObject.activeInHierarchy)
				{
					sight = transform3;
				}
			}
		}
		else
		{
			string text4 = Json.S(o4, "scopeDefault");
			componentsInChildren2 = weapon.GetComponentsInChildren<Transform>(includeInactive: true);
			foreach (Transform transform4 in componentsInChildren2)
			{
				if (transform4.name == text4 && transform4.gameObject.activeInHierarchy)
				{
					sight = transform4;
					ironSight = true;
				}
			}
		}
		SightSetup();
		actions.Go("none", 0f);
		overlay.Go("none", 0f);
		Unholster();
		FBCharacter fBCharacter = GetComponentInParent<FBCharacter>() ?? FBMatch.Player;
		if ((bool)fBCharacter)
		{
			FBLaser.Bind(fBCharacter, weapon, d, fp: true);
		}
	}

	public void Fire(bool empty, bool automatic)
	{
		if (empty)
		{
			overlay.Go("fireEmpty", 0f);
			return;
		}
		overlay.Go("fire", 0f);
		weaponAnim = (wclips.TryGetValue("fire", out var value) ? new(GClip, float)?((value, 0f)) : null);
		shots = ((!automatic) ? 1 : Mathf.Min(shots + 1, 3));
		sinceShot = 0f;
	}

	public float Reload(bool empty)
	{
		actions.Go(empty ? "reloadEmpty" : "reload", 0f);
		if (wclips.TryGetValue(empty ? "reloadEmpty" : "reload", out var value))
		{
			weaponAnim = (value, 0f);
		}
		return C(empty ? "reloadEmpty" : "reload")?.len ?? 2f;
	}

	public void ReloadCycled(int count, Action ins)
	{
		cycLeft = count;
		onInsert = ins;
		actions.Go("rOpen", 0f);
		WAnim("rOpen");
	}

	public void StopCycled()
	{
		cycLeft = 0;
	}

	private void WAnim(string k)
	{
		if (wclips.TryGetValue(k, out var value))
		{
			weaponAnim = (value, 0f);
		}
	}

	public void Inspect()
	{
		if (actions.cur.s == "none")
		{
			actions.Go("inspect", 0f);
		}
	}

	public void Unholster()
	{
		holsterL.Go("unholster", 0f);
	}

	public float Holster()
	{
		holsterL.Go("holster", 0f);
		return C("holster")?.len ?? 0.5f;
	}

	private static void LerpPose(Pose dst, Pose src, float w, int[] bones)
	{
		foreach (int num in bones)
		{
			dst.P[num] = Vector3.Lerp(dst.P[num], src.P[num], w);
			dst.S[num] = Vector3.Lerp(dst.S[num], src.S[num], w);
			dst.Q[num] = Quaternion.Slerp(dst.Q[num], src.Q[num], w);
		}
	}

	private static void MakeDelta(Pose o, Pose p, Pose r, int[] bones)
	{
		foreach (int num in bones)
		{
			o.P[num] = p.P[num] - r.P[num];
			o.Q[num] = Quaternion.Inverse(r.Q[num]) * p.Q[num];
		}
	}

	private static void ZeroDelta(Pose d, int[] bones)
	{
		foreach (int num in bones)
		{
			d.P[num] = Vector3.zero;
			d.Q[num] = Quaternion.identity;
		}
	}

	private static void AccDelta(Pose acc, Pose d, float w, int[] bones)
	{
		if (!(w <= 0f))
		{
			foreach (int num in bones)
			{
				acc.P[num] += d.P[num] * w;
				acc.Q[num] = acc.Q[num] * Quaternion.Slerp(Quaternion.identity, d.Q[num], w);
			}
		}
	}

	private static void ApplyDelta(Pose p, Pose d, float w, int[] bones)
	{
		if (!(w <= 0f))
		{
			foreach (int num in bones)
			{
				p.P[num] += d.P[num] * w;
				p.Q[num] = p.Q[num] * Quaternion.Slerp(Quaternion.identity, d.Q[num], w);
			}
		}
	}

	private void AddClip(string key, float t, float w)
	{
		if (!(w < 0.001f))
		{
			GClip gClip = C(key);
			if (gClip != null)
			{
				tmp.CopyFrom(rest);
				gClip.Sample(t, tmp);
				MakeDelta(tmp2, tmp, Ref(key), bonesA);
				AccDelta(dT, tmp2, w, bonesA);
			}
		}
	}

	private float Len(string k)
	{
		return C(k)?.len ?? 1f;
	}

	private Pose StateDelta(Entry e, float dt)
	{
		ZeroDelta(dT, bonesA);
		string s = e.s;
		switch (s)
		{
		case "loco":
		{
			float num = (Pistol ? 1f : aiming);
			float num2 = 1f - num;
			float num3 = Mathf.Min(1f, new Vector2(h, v).magnitude);
			Dictionary<string, float> dictionary = new Dictionary<string, float>
			{
				["F"] = Mathf.Max(0f, v),
				["B"] = Mathf.Max(0f, 0f - v),
				["L"] = Mathf.Max(0f, 0f - h),
				["R"] = Mathf.Max(0f, h)
			};
			float num4 = dictionary.Values.Sum();
			if (num4 <= 0f)
			{
				num4 = 1f;
			}
			foreach (string item in dictionary.Keys.ToList())
			{
				dictionary[item] = dictionary[item] / num4 * num3;
			}
			float num5 = 1f - num3;
			float num6 = Mathf.Min(1f, turning);
			Dictionary<string, string> dictionary2 = new Dictionary<string, string>
			{
				["F"] = "walkF",
				["B"] = "walkB",
				["L"] = "walkL",
				["R"] = "walkR"
			};
			Dictionary<string, float> dictionary3 = new Dictionary<string, float>
			{
				["F"] = rateF,
				["B"] = rateB,
				["L"] = rateS,
				["R"] = rateS
			};
			float num7 = num2 * num5 * ((1f - num6) * Len("idle") + num6 * Len("walkF"));
			foreach (string key4 in dictionary.Keys)
			{
				num7 += num2 * dictionary[key4] * Len(dictionary2[key4]) / dictionary3[key4];
			}
			num7 += num * ((1f - movement) * Len("aimIdle") + movement * Len("aimWalkF"));
			e.t += dt * playRate / Mathf.Max(num7, 0.1f);
			float num8 = e.t % 1f;
			AddClip("idle", num8 * Len("idle"), num2 * num5 * (1f - num6));
			AddClip("walkF", num8 * Len("walkF"), num2 * num5 * num6);
			foreach (string key5 in dictionary.Keys)
			{
				AddClip(dictionary2[key5], num8 * Len(dictionary2[key5]), num2 * dictionary[key5]);
			}
			AddClip("aimIdle", num8 * Len("aimIdle"), num * (1f - movement));
			AddClip("aimWalkF", num8 * Len("aimWalkF"), num * movement);
			break;
		}
		case "running":
			e.t += dt * playRate;
			AddClip("run", e.t, 1f);
			break;
		case "fire":
		{
			float t3 = Once("fire");
			AddClip("fire", t3, 1f - aiming);
			AddClip("aimFire", t3, aiming);
			break;
		}
		case "fireEmpty":
			AddClip("fireEmpty", Once("fireEmpty"), 1f);
			break;
		case "bolt":
		{
			float t2 = Once("bolt");
			AddClip("bolt", t2, 1f - aiming);
			AddClip("boltAim", t2, aiming);
			break;
		}
		case "reload":
		case "reloadEmpty":
		{
			string key2 = ((s == "reload") ? "reload" : "reloadEmpty");
			string key3 = ((s == "reload") ? "aimReload" : "aimReloadEmpty");
			float t = Once(key2);
			AddClip(key2, t, 1f - aiming);
			AddClip(key3, t, aiming);
			break;
		}
		case "rOpen":
			AddClip("rOpen", Once("rOpen"), 1f);
			break;
		case "rInsA":
		case "rInsB":
			AddClip("rInsert", Once("rInsert", 1.15f), 1f);
			break;
		case "rClose":
			AddClip("rClose", Once("rClose"), 1f);
			break;
		case "inspect":
			AddClip("inspect", Once("inspect"), 1f - aiming);
			break;
		case "unholster":
			e.t += dt;
			AddClip("unholster", e.t, 1f);
			break;
		case "holster":
			e.t += dt;
			AddClip("holster", e.t, 1f);
			break;
		case "grenadeAdd":
			AddClip("grenadeAdd", Once("grenadeAdd", 1.1f), 1f);
			break;
		case "aimIn":
		case "aimOut":
			e.t += dt * e.speed;
			AddClip(s, e.t, 1f);
			break;
		}
		return dT;
		float Once(string key, float speed = 1f)
		{
			e.t += dt * speed;
			if (e.t >= Len(key))
			{
				e.end = true;
			}
			return e.t;
		}
	}

	private void EvalLayer(Layer L, float dt, float w = 1f)
	{
		L.Step(dt);
		foreach (Entry item in L.list)
		{
			if (!(item.s == "none"))
			{
				Pose d = StateDelta(item, dt);
				ApplyDelta(pose, d, item.w * w, bonesA);
			}
		}
	}

	private void EvalOverride(Layer L, float dt, Func<string, string> clipOf, float speed = 1f)
	{
		L.Step(dt);
		foreach (Entry item in L.list)
		{
			if (item.s == "none")
			{
				continue;
			}
			GClip gClip = C(clipOf(item.s));
			if (gClip != null)
			{
				item.t += dt * speed;
				if (!gClip.loop && item.t >= gClip.len)
				{
					item.end = true;
				}
				tmp.CopyFrom(pose);
				gClip.Sample(item.t, tmp);
				LerpPose(pose, tmp, item.w, bonesA);
			}
		}
	}

	public void Tick(float dt, St st)
	{
		if (!ok || dt <= 0f)
		{
			return;
		}
		Dictionary<string, object> o = Json.O(D, "character");
		Dictionary<string, object> o2 = Json.O(D, "movement");
		Dictionary<string, object> o3 = Json.O(Json.O(GameData.WeaponsRaw, wid ?? ""), "stats");
		aiming = Damp(aiming, st.aiming ? 1 : 0, Json.F(o, "dampTimeAiming", 0.08f));
		h = Damp(h, st.axisX, Json.F(o, "dampTimeLocomotion", 0.25f));
		v = Damp(v, st.axisY, Json.F(o, "dampTimeLocomotion", 0.25f));
		movement = Damp(movement, Mathf.Min(1f, Mathf.Abs(st.axisX) + Mathf.Abs(st.axisY)), Json.F(o, "dampTimeLocomotion", 0.25f));
		turning = Damp(turning, Mathf.Min(1f, Mathf.Abs(st.lookX) * 0.08f), Json.F(o, "dampTimeTurning", 0.4f));
		leanFwd = Damp(leanFwd, Mathf.Max(0f, st.axisY) * (float)(st.moving ? 1 : 0), Json.F(o, "dampTimeLocomotion", 0.25f));
		leanP = Damp(leanP, st.lean, 0.12f);
		rateF = Json.F(o2, "walkingMultiplierForward", 1f);
		rateB = Json.F(o2, "walkingMultiplierBackwards", 0.9f);
		rateS = Json.F(o2, "walkingMultiplierSideways", 0.9f);
		playRate = (st.aiming ? Json.F(o, "aimingSpeedMultiplier", 0.9f) : 1f) * Mathf.Clamp((st.movementRate <= 0f) ? 1f : st.movementRate, 0.4f, 1.18f);
		bool flag = st.running && Json.B(o3, "useTacticalSprintAnimation");
		if (st.running && !flag && loco.cur.s != "running")
		{
			loco.Go("running", 0.5f, loco.cur.t);
		}
		if ((!st.running || flag) && loco.cur.s == "running")
		{
			loco.Go("loco", 0.2f);
		}
		if (flag && tac.cur.s == "none")
		{
			tac.Go("tac", 0.251f);
		}
		if (!flag && tac.cur.s != "none")
		{
			tac.Go("none", 0.12f);
		}
		if (st.aiming != aimSt)
		{
			wAim.Go(st.aiming ? "aimIn" : "aimOut", 0.3f);
			aimSt = st.aiming;
		}
		if (st.running != runSt)
		{
			wRun.Go(st.running ? "aimIn" : "aimOut", st.running ? 0.35f : 0.2f);
			wRun.cur.speed = (st.running ? 0.6f : 0.8f);
			runSt = st.running;
		}
		if (st.crouching != crouchSt)
		{
			wCrouch.Go(st.crouching ? "aimIn" : "aimOut", 0.35f);
			crouchSt = st.crouching;
		}
		Entry cur2 = overlay.cur;
		if (cur2.s == "fire" && Json.B(o3, "boltAction") && cur2.t >= Len("fire") * 0.6f && !cur2.bolted)
		{
			cur2.bolted = true;
			overlay.Go("bolt", 0.2f);
			WAnim(st.aiming ? "boltAim" : "bolt");
			events.Add("bolt");
		}
		else if (cur2.end)
		{
			overlay.Go("none", 0f);
		}
		Entry cur3 = actions.cur;
		if (cur3.end)
		{
			if (cur3.s == "rOpen" || cur3.s == "rInsA" || cur3.s == "rInsB")
			{
				if (cur3.s != "rOpen")
				{
					cycLeft--;
					onInsert?.Invoke();
				}
				if (cycLeft > 0)
				{
					actions.Go((cur3.s == "rInsA") ? "rInsB" : "rInsA", 0f);
					WAnim("rInsert");
				}
				else
				{
					actions.Go("rClose", 0f);
					WAnim("rClose");
				}
			}
			else
			{
				actions.Go("none", 0f);
			}
		}
		if (armR.cur.end)
		{
			armR.Go("none", 0f);
		}
		if (armL.cur.end)
		{
			armL.Go("none", 0.1f);
			if ((bool)knifeMesh)
			{
				knifeMesh.gameObject.SetActive(value: false);
			}
		}
		pose.CopyFrom(rest);
		float num = (Pistol ? 1f : aiming);
		tmp.CopyFrom(rest);
		C("idlePose")?.Sample(0f, tmp);
		LerpPose(pose, tmp, 1f - num, bonesA);
		tmp.CopyFrom(rest);
		C("aimPose")?.Sample(0f, tmp);
		LerpPose(pose, tmp, num, bonesA);
		EvalLayer(loco, dt);
		GClip gClip = C("walkPose");
		if (gClip != null)
		{
			tmp.CopyFrom(rest);
			gClip.Sample(0f, tmp);
			MakeDelta(dT, tmp, Ref("walkPose"), bonesA);
			ApplyDelta(pose, dT, leanFwd * (1f - aiming), bonesA);
		}
		EvalLayer(overlay, dt);
		EvalLayer(actions, dt);
		EvalLayer(holsterL, dt);
		EvalLayer(armR, dt);
		EvalOverride(armL, dt, (string s) => (!(s == "grenade")) ? "knife" : "grenade", 1.1f);
		if (Pistol)
		{
			wAim.Step(dt);
		}
		else
		{
			EvalLayer(wAim, dt);
		}
		EvalLayer(wRun, dt);
		EvalLayer(wCrouch, dt);
		EvalOverride(tac, dt, (string _) => "tactical");
		Write(A, pose, bonesA);
		if ((bool)weapon && W != null)
		{
			wpose.CopyFrom(wrest);
			(GClip, float)? tuple = weaponAnim;
			if (tuple.HasValue)
			{
				(GClip, float) valueOrDefault = tuple.GetValueOrDefault();
				var (gClip2, _) = valueOrDefault;
				if (gClip2 != null)
				{
					float item = valueOrDefault.Item2;
					item += dt;
					if (!wref.TryGetValue(gClip2, out var value))
					{
						value = new Pose(W.Length);
						value.CopyFrom(wrest);
						gClip2.Sample(0f, value);
						wref[gClip2] = value;
					}
					gClip2.Sample(item, wpose);
					int[] array = bonesW;
					foreach (int num2 in array)
					{
						wpose.P[num2] = wrest.P[num2] + wpose.P[num2] - value.P[num2];
						wpose.S[num2] = Vector3.Scale(wrest.S[num2], new Vector3((value.S[num2].x != 0f) ? (wpose.S[num2].x / value.S[num2].x) : 1f, (value.S[num2].y != 0f) ? (wpose.S[num2].y / value.S[num2].y) : 1f, (value.S[num2].z != 0f) ? (wpose.S[num2].z / value.S[num2].z) : 1f));
						wpose.Q[num2] = wrest.Q[num2] * (Quaternion.Inverse(value.Q[num2]) * wpose.Q[num2]);
					}
					weaponAnim = ((item > gClip2.len) ? null : new(GClip, float)?((gClip2, item)));
				}
			}
			Write(W, wpose, bonesW);
		}
		camH += (st.height - camH) * (1f - Mathf.Exp((0f - dt) * 12f));
		pitchNode.localPosition = new Vector3(0f, camH, 0f);
		pitchNode.localRotation = Quaternion.Euler(0f - st.pitch, 0f, 0f);
		Motions(dt, st);
		AlignSight(dt);
		if (armL.cur.s == "none" || armL.list.Count > 1)
		{
			IK(armLb, effL);
		}
		IK(armRb, effR);
		float Damp(float cur, float tgt, float time)
		{
			return cur + (tgt - cur) * (1f - Mathf.Exp((0f - dt) / Mathf.Max(time, 0.001f) * 2.3f));
		}
	}

	private void Motions(float dt, St st)
	{
		Dictionary<string, object> o2 = Json.O(Json.O(D, "itemFeel"), st.crouching ? "crouching" : (st.aiming ? "aiming" : (st.running ? "running" : "standing")));
		sinceShot += dt;
		if (sinceShot > 0.25f)
		{
			shots = 0;
		}
		Dictionary<string, object> o3 = Json.O(IA, "weaponRecoil");
		float num = (st.aiming ? Json.F(o3, "aimingStateMultiplier", 1f) : Json.F(o3, "standingStateMultiplier", 1f));
		recoil.Set(Json.O(o3, st.aiming ? "aimingState" : "standingState"));
		if (shots > 0)
		{
			recoil.Target(shots);
			recoil.tl *= num;
			recoil.tr *= num;
		}
		else
		{
			recoil.Zero();
		}
		var (vector, vector2) = recoil.Update(dt);
		if (st.jumped)
		{
			jumpT = 0f;
		}
		if (st.inAir)
		{
			airT += dt;
		}
		else
		{
			if (wasAir && airT > 0.15f)
			{
				landT = 0f;
			}
			airT = 0f;
		}
		wasAir = st.inAir;
		jump.Set(Json.O(o2, "jumpingCurves"));
		land.Set(Json.O(o2, "landingCurves"));
		fall.Set(Json.O(o2, "fallingCurves"));
		jumpT += dt;
		landT += dt;
		if (jumpT < 1f)
		{
			jump.Target(jumpT);
		}
		else
		{
			jump.Zero();
		}
		if (landT < 1f)
		{
			land.Target(landT);
		}
		else
		{
			land.Zero();
		}
		if (st.inAir && airT > 0.1f)
		{
			fall.Target(airT);
		}
		else
		{
			fall.Zero();
		}
		(Vector3, Vector3) tuple2 = jump.Update(dt);
		Vector3 item = tuple2.Item1;
		Vector3 item2 = tuple2.Item2;
		(Vector3, Vector3) tuple3 = land.Update(dt);
		Vector3 item3 = tuple3.Item1;
		Vector3 item4 = tuple3.Item2;
		(Vector3, Vector3) tuple4 = fall.Update(dt);
		Vector3 item5 = tuple4.Item1;
		Vector3 item6 = tuple4.Item2;
		Dictionary<string, object> o4 = Json.O(IA, "leaning");
		leanItem.Set(Json.O(o4, st.aiming ? "itemAiming" : "itemStanding"));
		leanCam.Set(Json.O(o4, st.aiming ? "cameraAiming" : "cameraStanding"));
		leanItem.Target(leanP);
		leanCam.Target(leanP);
		(Vector3, Vector3) tuple5 = leanItem.Update(dt);
		Vector3 item7 = tuple5.Item1;
		Vector3 item8 = tuple5.Item2;
		(Vector3, Vector3) tuple6 = leanCam.Update(dt);
		Vector3 item9 = tuple6.Item1;
		Vector3 item10 = tuple6.Item2;
		camLeanLoc = item9;
		camLeanRot = item10;
		Dictionary<string, object> o5 = Json.O(IA, "itemOffsets");
		string text = (st.crouching ? "crouching" : (st.aiming ? "aiming" : (st.running ? "running" : "standing")));
		Dictionary<string, object> o6 = Json.O(o2, "offset");
		Vector3 vector3 = offL.Update(V(Json.O(o5, text + "Location")) + V(Json.O(o6, "offsetLocation")), dt);
		Vector3 vector4 = offR.Update(V(Json.O(o5, text + "Rotation")) + V(Json.O(o6, "offsetRotation")), dt);
		Dictionary<string, object> o7 = Json.O(scope, "aimLoc");
		Vector3 vector5 = scopeL.Update(V(o7) * aiming, dt);
		Vector3 vector6 = vector + item + item3 + item5 + vector3 + item7 + vector5;
		Vector3 vector7 = item2 + item4 + item6 + vector4 + item8;
		if (Pistol)
		{
			vector6 += new Vector3(0.025f, -0.075f, -0.03f) * (1f - aiming);
			vector7 += new Vector3(-14f, -4f, 0f) * (1f - aiming);
		}
		motionNode.localPosition = motionBase + vector6;
		Quaternion quaternion = Quaternion.Euler(vector7.x, vector7.y, vector7.z);
		recoilQ = Quaternion.Euler(vector2.x, vector2.y, vector2.z);
		motionNode.localRotation = motionBaseQ * quaternion * recoilQ;
		static Vector3 V(Dictionary<string, object> o)
		{
			if (o != null)
			{
				return new Vector3(Json.F(o, "x"), Json.F(o, "y"), Json.F(o, "z"));
			}
			return Vector3.zero;
		}
	}

	private void AlignSight(float dt)
	{
		Transform parent = inv.parent;
		if (!parent)
		{
			return;
		}
		if (parent.localPosition == gunLast)
		{
			parent.localPosition -= sightCorr;
		}
		Vector3 b = Vector3.zero;
		if ((bool)sight && sight.gameObject.activeInHierarchy && aiming > 0.001f)
		{
			Bounds bounds = default(Bounds);
			Bounds bounds2 = default(Bounds);
			bool flag = false;
			bool flag2 = false;
			Renderer[] componentsInChildren = sight.GetComponentsInChildren<Renderer>();
			foreach (Renderer renderer in componentsInChildren)
			{
				MeshFilter component = renderer.GetComponent<MeshFilter>();
				if (!component || !component.sharedMesh)
				{
					continue;
				}
				Bounds bounds3 = component.sharedMesh.bounds;
				List<Vector3> list = new List<Vector3>();
				for (int j = 0; j < 8; j++)
				{
					list.Add(camNode.InverseTransformPoint(renderer.transform.TransformPoint(bounds3.center + Vector3.Scale(bounds3.extents, new Vector3((((uint)j & (true ? 1u : 0u)) != 0) ? 1 : (-1), (((uint)j & 2u) != 0) ? 1 : (-1), (((uint)j & 4u) != 0) ? 1 : (-1))))));
				}
				Bounds bounds4 = new Bounds(list[0], Vector3.zero);
				foreach (Vector3 item in list)
				{
					bounds4.Encapsulate(item);
				}
				if (!flag)
				{
					bounds = bounds4;
					flag = true;
				}
				else
				{
					bounds.Encapsulate(bounds4);
				}
				Material sharedMaterial = renderer.sharedMaterial;
				if ((bool)sharedMaterial && (sharedMaterial.renderQueue >= 2450 || sharedMaterial.IsKeywordEnabled("_ALPHATEST_ON") || sharedMaterial.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")) && bounds4.size.z < 0.15f * Mathf.Max(bounds4.size.x, bounds4.size.y))
				{
					if (!flag2)
					{
						bounds2 = bounds4;
						flag2 = true;
					}
					else
					{
						bounds2.Encapsulate(bounds4);
					}
				}
			}
			if (flag)
			{
				Bounds bounds5 = (flag2 ? bounds2 : bounds);
				float z = bounds5.center.z;
				Vector3 position = (sightSock ? camNode.InverseTransformPoint(sightSock.position) : (ironSight ? new Vector3(bounds5.center.x, bounds5.max.y - bounds5.size.y * 0.08f, z) : new Vector3(bounds5.center.x, flag2 ? bounds5.center.y : (bounds5.min.y + bounds5.size.y * 0.62f), z)));
				z = position.z;
				Vector3 position2 = new Vector3(0f, 0f, z);
				Vector3 vector = parent.parent.InverseTransformPoint(camNode.TransformPoint(position));
				b = (parent.parent.InverseTransformPoint(camNode.TransformPoint(position2)) - vector) * aiming;
			}
		}
		sightCorr = Vector3.Lerp(sightCorr, b, 1f - Mathf.Exp((0f - dt) * 8f));
		parent.localPosition += sightCorr;
		gunLast = parent.localPosition;
	}

	private void SightSetup()
	{
		if (!sight)
		{
			return;
		}
		Transform[] componentsInChildren = sight.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform in componentsInChildren)
		{
			if (!sightSock && transform.name.StartsWith("SOCKET_Scope"))
			{
				sightSock = transform;
			}
			if (!pipNode && transform.name == "Render Camera")
			{
				pipNode = transform;
			}
		}
		if (!pipNode)
		{
			return;
		}
		MeshRenderer meshRenderer = null;
		MeshRenderer[] componentsInChildren2 = sight.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
		foreach (MeshRenderer meshRenderer2 in componentsInChildren2)
		{
			if (meshRenderer2.sharedMaterials.Length >= 5)
			{
				meshRenderer = meshRenderer2;
				break;
			}
		}
		if (!meshRenderer)
		{
			return;
		}
		if (FBQuality.Mobile)
		{
			Material[] sharedMaterials = meshRenderer.sharedMaterials;
			Material material = FBShaders.Transparent(new Material(FBShaders.Unlit));
			material.SetColor("_BaseColor", new Color(0.05f, 0.07f, 0.08f, 0.25f));
			sharedMaterials[sharedMaterials.Length - 1] = material;
			meshRenderer.sharedMaterials = sharedMaterials;
			return;
		}
		if (!pipRT)
		{
			pipRT = new RenderTexture(512, 512, 16)
			{
				name = "luneta_pip"
			};
		}
		if (!pipCam)
		{
			GameObject gameObject = new GameObject("luneta_camera");
			pipCam = gameObject.AddComponent<Camera>();
			pipCam.enabled = false;
			pipCam.targetTexture = pipRT;
			pipCam.nearClipPlane = 0.2f;
			pipCam.farClipPlane = 900f;
		}
		Material[] sharedMaterials2 = meshRenderer.sharedMaterials;
		Material material2 = new Material(FBShaders.Unlit);
		material2.SetTexture("_BaseMap", pipRT);
		material2.mainTexture = pipRT;
		material2.SetColor("_BaseColor", Color.white);
		sharedMaterials2[sharedMaterials2.Length - 1] = material2;
		meshRenderer.sharedMaterials = sharedMaterials2;
	}

	private static readonly int __fbt_LateUpdate = FBTempo.Id("FBFpRig.LateUpdate");

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
		if (!pipCam)
		{
			return;
		}
		Camera main = Camera.main;
		if (!pipNode || !pipNode.gameObject.activeInHierarchy || !(aiming > 0.6f) || FBScopes.Scoped || !main)
		{
			if (pipCam.enabled)
			{
				pipCam.enabled = false;
			}
		}
		else
		{
			float num = Mathf.Max(1.05f, FBScopes.Zoom(def));
			pipCam.enabled = true;
			pipCam.cullingMask = main.cullingMask & -16385;
			pipCam.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
			pipCam.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(main.fieldOfView * 0.5f * ((float)Math.PI / 180f)) / num) * 57.29578f;
		}
	}

	private void OnDestroy()
	{
		if ((bool)pipCam)
		{
			UnityEngine.Object.Destroy(pipCam.gameObject);
		}
		if ((bool)pipRT)
		{
			pipRT.Release();
		}
	}

	private static void IK(Transform[] arm, Transform eff)
	{
		if (arm == null || !eff || !arm[0] || !arm[1] || !arm[2])
		{
			return;
		}
		Vector3 position = eff.position;
		for (int i = 0; i < 6; i++)
		{
			Transform[] array = new Transform[2]
			{
				arm[1],
				arm[0]
			};
			foreach (Transform transform in array)
			{
				Vector3 normalized = (arm[2].position - transform.position).normalized;
				Vector3 normalized2 = (position - transform.position).normalized;
				if (!(Vector3.Dot(normalized, normalized2) > 0.99999f))
				{
					transform.rotation = Quaternion.FromToRotation(normalized, normalized2) * transform.rotation;
				}
			}
		}
		arm[2].rotation = eff.rotation;
	}

	public void CameraPose(out Vector3 pos, out Quaternion rot)
	{
		pos = camNode.position;
		rot = camNode.rotation;
		Transform transform = motionNode;
		Quaternion quaternion = transform.parent.rotation * transform.localRotation * Quaternion.Inverse(recoilQ);
		Quaternion quaternion2 = quaternion * Quaternion.Inverse(recoilQ) * Quaternion.Inverse(quaternion);
		Vector3 position = transform.position;
		pos = quaternion2 * (pos - position) + position;
		rot = quaternion2 * rot;
	}
}
}
