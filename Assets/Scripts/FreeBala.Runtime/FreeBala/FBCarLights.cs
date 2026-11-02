using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace FreeBala
{
public static class FBCarLights
{
	private enum G
	{
		Head,
		Tail,
		Rev,
		IndL,
		IndR
	}

	private class Lamp
	{
		public G g;

		public Vector3 c;

		public Vector3 n;

		public Transform glow;

		public float size;

		public int side;

		public bool dead;

		public float flickT;

		public float flickNext;

		public bool flickOn;
	}

	private class Rig
	{
		public FBCar car;

		public Transform shell;

		public readonly List<Lamp> lamps = new List<Lamp>();

		public readonly MeshRenderer[] lens = new MeshRenderer[10];

		public Vector3 hl;

		public Vector3 hr;

		public Vector3 bl;

		public Vector3 br;

		public bool hasHead;

		public bool hasTail;

		public bool headL = true;

		public bool headR = true;

		public bool lensOn;

		public bool hadBeam;

		public bool hadTail;

		public bool hadRev;

		public float lastV;

		public float brakeT;

		public float accSm;
	}

	private static readonly Dictionary<FBCar, Rig> rigs = new Dictionary<FBCar, Rig>();

	private static readonly Dictionary<string, Mesh[]> meshCache = new Dictionary<string, Mesh[]>();

	private static readonly List<Light> beams = new List<Light>();

	private static readonly List<Light> tailPool = new List<Light>();

	private static readonly List<Light> revPool = new List<Light>();

	private static Material glowMat;

	private static Material lensMat;

	private static Mesh quad;

	private static Texture2D glowTex;

	private static Texture2D cookie;

	private static Dictionary<string, object> data;

	private static bool dataTried;

	private static readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();

	public static int headMode;

	public static int LensToggles;

	public static int BeamSwaps;

	public static int BeamsOn;

	private static readonly Rig[] beamOwner = new Rig[12];

	private static int playerBlink;

	private static bool playerHazard;

	private static float steerPeak;

	public static bool PlayerBrakeNow;

	public static int PlayerBlinkNow
	{
		get
		{
			if (!playerHazard)
			{
				return playerBlink;
			}
			return 2;
		}
	}

	public static void TestBlink(int b)
	{
		playerHazard = b == 2;
		playerBlink = ((b != 2) ? b : 0);
		steerPeak = 0f;
	}

	public static bool LensOn(FBCar car, int g, int side)
	{
		if (!car || !rigs.TryGetValue(car, out var value))
		{
			return false;
		}
		MeshRenderer meshRenderer = ((g * 2 + side < value.lens.Length) ? value.lens[g * 2 + side] : null);
		if ((bool)meshRenderer)
		{
			return meshRenderer.enabled;
		}
		return false;
	}

	public static void Reset()
	{
		rigs.Clear();
		beams.Clear();
		tailPool.Clear();
		revPool.Clear();
	}

	private static Material Mat()
	{
		if ((bool)glowMat)
		{
			return glowMat;
		}
		glowTex = new Texture2D(64, 64, TextureFormat.RGBA32, mipChain: false)
		{
			wrapMode = TextureWrapMode.Clamp
		};
		for (int i = 0; i < 64; i++)
		{
			for (int j = 0; j < 64; j++)
			{
				float num = new Vector2((float)j - 31.5f, (float)i - 31.5f).magnitude / 31.5f;
				float num2 = Mathf.Clamp01(1f - num);
				glowTex.SetPixel(j, i, new Color(1f, 1f, 1f, num2 * num2 * (1f + 2f * Mathf.Clamp01(1f - num * 3f))));
			}
		}
		glowTex.Apply();
		glowMat = FBShaders.Transparent(new Material(FBShaders.ParticlesUnlit), additive: true);
		glowMat.SetTexture("_BaseMap", glowTex);
		glowMat.mainTexture = glowTex;
		glowMat.SetFloat("_Cull", 0f);
		glowMat.renderQueue = 3050;
		lensMat = FBShaders.Transparent(new Material(FBShaders.ParticlesUnlit), additive: true);
		lensMat.SetTexture("_BaseMap", Texture2D.whiteTexture);
		lensMat.mainTexture = Texture2D.whiteTexture;
		lensMat.SetFloat("_Cull", 0f);
		lensMat.renderQueue = 3020;
		quad = new Mesh
		{
			name = "brilho"
		};
		quad.SetVertices(new Vector3[4]
		{
			new Vector3(-0.5f, -0.5f),
			new Vector3(0.5f, -0.5f),
			new Vector3(-0.5f, 0.5f),
			new Vector3(0.5f, 0.5f)
		});
		quad.SetUVs(0, new Vector2[4]
		{
			new Vector2(0f, 0f),
			new Vector2(1f, 0f),
			new Vector2(0f, 1f),
			new Vector2(1f, 1f)
		});
		quad.SetTriangles(new int[6] { 0, 2, 1, 1, 2, 3 }, 0);
		quad.RecalculateBounds();
		cookie = new Texture2D(128, 128, TextureFormat.R8, mipChain: false)
		{
			wrapMode = TextureWrapMode.Clamp
		};
		for (int k = 0; k < 128; k++)
		{
			for (int l = 0; l < 128; l++)
			{
				float num3 = ((float)l + 0.5f) / 128f * 2f - 1f;
				float num4 = ((float)k + 0.5f) / 128f * 2f - 1f;
				float num5 = Mathf.Sqrt(num3 * num3 * 0.55f + num4 * num4);
				float num6 = Mathf.Clamp01((0.12f + ((num3 > 0.1f) ? 0.1f : 0f) - num4) * 9f);
				float f = Mathf.Clamp01(1f - num5) * num6;
				f = Mathf.Pow(f, 0.7f) * (0.55f + 0.45f * Mathf.Clamp01(1f - Mathf.Abs(num3) * 1.1f));
				cookie.SetPixel(l, k, new Color(f, f, f, f));
			}
		}
		cookie.Apply();
		return glowMat;
	}

	private static Dictionary<string, object> Data()
	{
		if (!dataTried)
		{
			dataTried = true;
			TextAsset textAsset = Resources.Load<TextAsset>("FBLamps/carlamps");
			if ((bool)textAsset)
			{
				data = Json.Parse(textAsset.text) as Dictionary<string, object>;
			}
		}
		return data;
	}

	private static G GroupOf(string kind, string side)
	{
		switch (kind)
		{
		default:
			if (!(side == "L"))
			{
				return G.IndR;
			}
			return G.IndL;
		case "rev":
			return G.Rev;
		case "brake":
			return G.Tail;
		case "head":
			return G.Head;
		}
	}

	private static int LensIdx(G g, string side)
	{
		return (int)g * 2 + g switch
		{
			G.IndR => 1, 
			G.IndL => 0, 
			_ => (side == "R") ? 1 : 0, 
		};
	}

	public static int BreakNear(FBCar car, Vector3 wp, float radius)
	{
		if (!car)
		{
			return 0;
		}
		if (!rigs.TryGetValue(car, out var value))
		{
			Mat();
			value = (rigs[car] = Build(car));
		}
		int num = 0;
		foreach (Lamp lamp in value.lamps)
		{
			if (!lamp.dead)
			{
				Vector3 vector = value.shell.TransformPoint(lamp.c);
				if (!((vector - wp).sqrMagnitude > radius * radius))
				{
					lamp.dead = true;
					lamp.flickT = Random.Range(0.5f, 1.5f);
					lamp.flickNext = 0f;
					lamp.flickOn = true;
					num++;
					FBFx.Sparks(vector, value.shell.TransformDirection(lamp.n));
				}
			}
		}
		if (num > 0)
		{
			FBAudio.PlayAt("glass_break", wp, 0.3f, 3f);
		}
		return num;
	}

	public static int DeadCount(FBCar car)
	{
		if (!car || !rigs.TryGetValue(car, out var value))
		{
			return 0;
		}
		return value.lamps.Count((Lamp l) => l.dead);
	}

	public static void Repair(FBCar car)
	{
		if (!car || !rigs.TryGetValue(car, out var value))
		{
			return;
		}
		foreach (Lamp lamp in value.lamps)
		{
			lamp.dead = false;
			lamp.flickT = 0f;
		}
	}

	private static bool Alive(Lamp lp, float dt)
	{
		if (!lp.dead)
		{
			return true;
		}
		if (lp.flickT <= 0f)
		{
			return false;
		}
		lp.flickT -= dt;
		if (Time.time >= lp.flickNext)
		{
			lp.flickOn = !lp.flickOn;
			lp.flickNext = Time.time + (lp.flickOn ? Random.Range(0.02f, 0.1f) : Random.Range(0.03f, 0.25f));
		}
		if (lp.flickOn)
		{
			return lp.flickT > 0f;
		}
		return false;
	}

	private static Rig Build(FBCar c)
	{
		Rig rig = new Rig
		{
			car = c,
			shell = (c.shell ? c.shell : (c.transform.Find("body/shell") ?? c.transform))
		};
		Mat();
		Dictionary<string, object> dictionary = Data();
		Dictionary<string, object> dictionary2 = ((dictionary != null) ? Json.O(dictionary, c.id) : null);
		Dictionary<string, object> Ls;
		if (dictionary2 != null)
		{
			if (!meshCache.TryGetValue(c.id, out var value))
			{
				value = new Mesh[10];
				List<Vector3>[] array = new List<Vector3>[10];
				for (int i = 0; i < 10; i++)
				{
					array[i] = new List<Vector3>();
				}
				foreach (KeyValuePair<string, object> item in dictionary2)
				{
					foreach (object item2 in item.Value as List<object>)
					{
						Dictionary<string, object> o2 = item2 as Dictionary<string, object>;
						G g = GroupOf(item.Key, Json.S(o2, "s"));
						List<object> list = Json.L(o2, "t");
						Vector3 vector = V3(Json.L(o2, "n"));
						if (list != null)
						{
							int num = LensIdx(g, Json.S(o2, "s"));
							for (int j = 0; j + 2 < list.Count; j += 3)
							{
								array[num].Add(new Vector3(Json.Fv(list[j]), Json.Fv(list[j + 1]), Json.Fv(list[j + 2])) + vector * 0.004f);
							}
						}
					}
				}
				for (int m = 0; m < 10; m++)
				{
					if (array[m].Count >= 3)
					{
						Mesh mesh = new Mesh
						{
							name = "lente_" + m
						};
						mesh.SetVertices(array[m]);
						int[] array2 = new int[array[m].Count];
						for (int n = 0; n < array2.Length; n++)
						{
							array2[n] = n;
						}
						mesh.SetTriangles(array2, 0);
						mesh.RecalculateBounds();
						value[m] = mesh;
					}
				}
				meshCache[c.id] = value;
			}
			for (int num2 = 0; num2 < 10; num2++)
			{
				if ((bool)value[num2])
				{
					GameObject gameObject = new GameObject("lente_" + num2);
					gameObject.transform.SetParent(rig.shell, worldPositionStays: false);
					gameObject.layer = c.gameObject.layer;
					gameObject.AddComponent<MeshFilter>().sharedMesh = value[num2];
					MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
					meshRenderer.sharedMaterial = lensMat;
					meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
					meshRenderer.receiveShadows = false;
					meshRenderer.lightProbeUsage = LightProbeUsage.Off;
					meshRenderer.enabled = false;
					rig.lens[num2] = meshRenderer;
				}
			}
			foreach (KeyValuePair<string, object> item3 in dictionary2)
			{
				foreach (object item4 in item3.Value as List<object>)
				{
					Dictionary<string, object> o3 = item4 as Dictionary<string, object>;
					G g2 = GroupOf(item3.Key, Json.S(o3, "s"));
					Lamp lamp = new Lamp
					{
						g = g2,
						c = V3(Json.L(o3, "c")),
						n = V3(Json.L(o3, "n")),
						side = (LensIdx(g2, Json.S(o3, "s")) & 1)
					};
					if (lamp.n.sqrMagnitude < 0.1f)
					{
						lamp.n = new Vector3(0f, 0f, (lamp.c.z > 0f) ? 1 : (-1));
					}
					lamp.size = g2 switch
					{
						G.Tail => 0.36f, 
						G.Head => 0.5f, 
						_ => 0.3f, 
					};
					lamp.glow = Quad(rig.shell, lamp.c + lamp.n * 0.03f, "brilho_" + g2);
					rig.lamps.Add(lamp);
				}
			}
		}
		else
		{
			Ls = Json.O(Json.O(GameData.Vehicles, c.id), "lights");
			foreach (Vector3 item5 in from p in Pts("HeadLight")
				where Mathf.Abs(p.x) > 0.15f
				select p)
			{
				rig.lamps.Add(new Lamp
				{
					g = G.Head,
					c = item5,
					n = Vector3.forward,
					size = 0.5f,
					glow = Quad(rig.shell, item5, "brilho_farol"),
					side = ((!(item5.x < 0f)) ? 1 : 0)
				});
			}
			foreach (Vector3 item6 in Pts("BrakeLight"))
			{
				rig.lamps.Add(new Lamp
				{
					g = G.Tail,
					c = item6,
					n = Vector3.back,
					size = 0.36f,
					glow = Quad(rig.shell, item6, "brilho_freio"),
					side = ((!(item6.x < 0f)) ? 1 : 0)
				});
			}
			foreach (Vector3 item7 in Pts("ReverseLight"))
			{
				rig.lamps.Add(new Lamp
				{
					g = G.Rev,
					c = item7,
					n = Vector3.back,
					size = 0.3f,
					glow = Quad(rig.shell, item7, "brilho_re"),
					side = ((!(item7.x < 0f)) ? 1 : 0)
				});
			}
			foreach (Vector3 item8 in Pts("IndicatorLight"))
			{
				rig.lamps.Add(new Lamp
				{
					g = ((item8.x < 0f) ? G.IndL : G.IndR),
					c = item8,
					n = ((item8.z > 0f) ? Vector3.forward : Vector3.back),
					size = 0.3f,
					glow = Quad(rig.shell, item8, "brilho_seta"),
					side = ((!(item8.x < 0f)) ? 1 : 0)
				});
			}
		}
		List<Lamp> list2 = rig.lamps.Where((Lamp l) => l.g == G.Head).ToList();
		if (list2.Count > 0)
		{
			List<Lamp> source = list2.OrderByDescending((Lamp l) => l.c.y).Take(Mathf.Max(2, list2.Count / 2)).ToList();
			rig.hl = source.OrderBy((Lamp l) => l.c.x).First().c;
			rig.hr = source.OrderBy((Lamp l) => l.c.x).Last().c;
			rig.hasHead = true;
		}
		List<Lamp> list3 = rig.lamps.Where((Lamp l) => l.g == G.Tail).ToList();
		if (list3.Count > 0)
		{
			rig.bl = list3.OrderBy((Lamp l) => l.c.x).First().c;
			rig.br = list3.OrderBy((Lamp l) => l.c.x).Last().c;
			rig.hasTail = true;
		}
		return rig;
		List<Vector3> Pts(string k)
		{
			return (from o in Json.L(Ls, k) ?? new List<object>()
				select o as List<object> into p
				where p != null && p.Count >= 3
				select new Vector3(0f - Json.Fv(p[0]), Json.Fv(p[1]), Json.Fv(p[2]))).ToList();
		}
	}

	private static Vector3 V3(List<object> l)
	{
		if (l != null && l.Count >= 3)
		{
			return new Vector3(Json.Fv(l[0]), Json.Fv(l[1]), Json.Fv(l[2]));
		}
		return Vector3.zero;
	}

	private static Transform Quad(Transform parent, Vector3 p, string n)
	{
		GameObject gameObject = new GameObject(n);
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		gameObject.transform.localPosition = p;
		gameObject.AddComponent<MeshFilter>().sharedMesh = quad;
		MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
		meshRenderer.sharedMaterial = glowMat;
		meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
		meshRenderer.receiveShadows = false;
		meshRenderer.lightProbeUsage = LightProbeUsage.Off;
		gameObject.SetActive(value: false);
		return gameObject.transform;
	}

	private static void Pools()
	{
		if (beams.Count <= 0)
		{
			bool mobile = FBQuality.Mobile;
			Mat();
			for (int i = 0; i < (mobile ? 2 : 6); i++)
			{
				Light light = new GameObject("FacoFarol" + i).AddComponent<Light>();
				light.type = LightType.Spot;
				light.range = 42f;
				light.spotAngle = 62f;
				light.innerSpotAngle = 26f;
				light.useColorTemperature = true;
				light.colorTemperature = 4700f;
				light.color = Color.white;
				light.cookie = cookie;
				light.shadows = ((i < 2 && !mobile) ? LightShadows.Soft : LightShadows.None);
				light.shadowNearPlane = 0.3f;
				light.enabled = false;
				beams.Add(light);
				FBVolumetric.Sources.Add(light);
			}
			for (int j = 0; j < ((!mobile) ? 3 : 0); j++)
			{
				Light light2 = new GameObject("LuzFreio" + j).AddComponent<Light>();
				light2.type = LightType.Spot;
				light2.range = 6.5f;
				light2.spotAngle = 125f;
				light2.innerSpotAngle = 45f;
				light2.color = new Color(1f, 0.08f, 0.04f);
				light2.shadows = LightShadows.None;
				light2.enabled = false;
				tailPool.Add(light2);
			}
			for (int k = 0; k < (mobile ? 1 : 2); k++)
			{
				Light light3 = new GameObject("LuzRe" + k).AddComponent<Light>();
				light3.type = LightType.Spot;
				light3.range = 9f;
				light3.spotAngle = 100f;
				light3.innerSpotAngle = 50f;
				light3.color = new Color(1f, 0.97f, 0.92f);
				light3.shadows = LightShadows.None;
				light3.enabled = false;
				revPool.Add(light3);
			}
		}
	}

	public static void PlayerControl(FBCar car)
	{
		Keyboard current = Keyboard.current;
		if (current == null || !car)
		{
			return;
		}
		if (current.lKey.wasPressedThisFrame)
		{
			headMode = (headMode + 1) % 3;
			FBHUD.I?.Center((headMode == 0) ? "FARÓIS: AUTOMÁTICO" : ((headMode == 1) ? "FARÓIS: LIGADOS" : "FARÓIS: DESLIGADOS"), 1.1f);
		}
		if (current.qKey.wasPressedThisFrame)
		{
			playerBlink = ((playerBlink != -1) ? (-1) : 0);
			steerPeak = 0f;
		}
		if (current.eKey.wasPressedThisFrame)
		{
			playerBlink = ((playerBlink != 1) ? 1 : 0);
			steerPeak = 0f;
		}
		if (current.xKey.wasPressedThisFrame)
		{
			playerHazard = !playerHazard;
			FBHUD.I?.Center(playerHazard ? "PISCA-ALERTA LIGADO" : "PISCA-ALERTA DESLIGADO", 1f);
		}
		if (playerBlink != 0)
		{
			float num = car.steerN * (float)(-playerBlink);
			steerPeak = Mathf.Max(steerPeak, 0f - num);
			if (steerPeak > 0.35f && Mathf.Abs(car.steerN) < 0.08f)
			{
				playerBlink = 0;
			}
		}
	}

	public static void Tick(Camera cam, float night, bool rain, float dt)
	{
		if (!cam)
		{
			return;
		}
		Pools();
		Vector3 position = cam.transform.position;
		_ = cam.transform.forward;
		bool flag = night > 0.3f || rain;
		float num = Mathf.Max(rain ? 0.75f : 0f, Mathf.InverseLerp(0.25f, 0.75f, night));
		float num2 = Mathf.InverseLerp(0.3f, 0.8f, night);
		bool flag2 = Time.time % 0.72f < 0.38f;
		List<(Rig, float, bool)> list = new List<(Rig, float, bool)>();
		List<(Rig, float, float)> list2 = new List<(Rig, float, float)>();
		List<(Rig, float)> list3 = new List<(Rig, float)>();
		FBCar cur = FBDrive.Cur;
		foreach (FBCar item3 in FBVehicles.list)
		{
			if (!item3)
			{
				continue;
			}
			float sqrMagnitude = (item3.transform.position - position).sqrMagnitude;
			bool flag3 = (bool)item3.driver || item3.remoteDriver >= 0 || (item3.aiLights && (bool)item3.driver);
			if (!rigs.TryGetValue(item3, out var value))
			{
				if (!flag3 && sqrMagnitude > 3600f)
				{
					continue;
				}
				value = (rigs[item3] = Build(item3));
			}
			bool flag4 = sqrMagnitude < 48400f;
			float b = (item3.v - value.lastV) / Mathf.Max(0.001f, dt);
			value.lastV = item3.v;
			value.accSm = Mathf.Lerp(value.accSm, b, Mathf.Min(1f, dt * 8f));
			bool flag5 = item3 == cur;
			bool flag8;
			bool flag7;
			bool flag6;
			int num3;
			if (!flag3)
			{
				flag8 = (flag7 = (flag6 = false));
				num3 = 0;
			}
			else if (item3.aiLights && !flag5)
			{
				flag8 = flag && !item3.lHeadOff;
				flag6 = item3.lRev;
				num3 = item3.lBlink;
				if (item3.lBrake || Mathf.Abs(item3.v) < 0.15f)
				{
					value.brakeT = 0.35f;
				}
				else
				{
					value.brakeT -= dt;
				}
				flag7 = value.brakeT > 0f;
			}
			else if (flag5)
			{
				flag8 = headMode == 1 || (headMode == 0 && flag);
				flag6 = item3.v < -0.3f;
				if ((item3.prevThr < -0.05f && item3.v > 0.3f) || (item3.prevThr < -0.05f && Mathf.Abs(item3.v) < 0.3f && item3.v >= -0.05f) || (Mathf.Abs(item3.v) < 0.15f && item3.prevThr <= 0.05f) || FBDrive.hold)
				{
					value.brakeT = 0.2f;
				}
				else
				{
					value.brakeT -= dt;
				}
				flag7 = value.brakeT > 0f;
				num3 = (playerHazard ? 2 : playerBlink);
				PlayerBrakeNow = flag7;
			}
			else
			{
				flag6 = item3.v < -0.3f;
				if (item3.netLights >= 0)
				{
					int num4 = (item3.netLights >> 3) & 3;
					int num5 = (item3.netLights >> 5) & 3;
					flag8 = num4 == 1 || (num4 == 0 && flag);
					num3 = num5 switch
					{
						1 => -1, 
						3 => 2, 
						_ => (num5 == 2) ? 1 : 0, 
					};
					if (((uint)item3.netLights & 0x80u) != 0 || Mathf.Abs(item3.v) < 0.15f)
					{
						value.brakeT = 0.25f;
					}
					else
					{
						value.brakeT -= dt;
					}
					flag7 = value.brakeT > 0f;
				}
				else
				{
					flag8 = flag;
					if ((value.accSm < -2.2f && item3.v > 0.8f) || Mathf.Abs(item3.v) < 0.15f)
					{
						value.brakeT = 0.25f;
					}
					else
					{
						value.brakeT -= dt;
					}
					flag7 = value.brakeT > 0f;
					num3 = 0;
				}
			}
			bool flag9 = flag8;
			bool flag10 = ((num3 == 2) ? flag2 : (num3 == -1 && flag2));
			bool flag11 = ((num3 == 2) ? flag2 : (num3 == 1 && flag2));
			bool flag12 = (value.lensOn = sqrMagnitude < (float)(value.lensOn ? 10000 : 8100));
			Color color = item3.headTint ?? new Color(1f, 0.93f, 0.8f);
			bool[] aliveSide = new bool[10];
			foreach (Lamp lamp in value.lamps)
			{
				lamp.flickOn = lamp.flickOn;
				if (Alive(lamp, dt))
				{
					aliveSide[(int)lamp.g * 2 + lamp.side] = true;
				}
			}
			bool anyLamp = value.lamps.Count > 0;
			int sd;
			for (sd = 0; sd < 2; sd++)
			{
				SetLens(value, G.Head, sd, flag12 && flag8 && A(G.Head), color * (2.2f + 1.2f * num));
				SetLens(value, G.Tail, sd, flag12 && (flag7 || flag9) && A(G.Tail), new Color(1f, 0.04f, 0.02f) * (flag7 ? 3.4f : 1f) * ((flag || flag7) ? 1f : 0.8f));
				SetLens(value, G.Rev, sd, flag12 && flag6 && A(G.Rev), new Color(1f, 1f, 1f) * 2.4f);
				SetLens(value, G.IndL, sd, flag12 && flag10 && A(G.IndL), new Color(1f, 0.42f, 0.02f) * 3.2f);
				SetLens(value, G.IndR, sd, flag12 && flag11 && A(G.IndR), new Color(1f, 0.42f, 0.02f) * 3.2f);
			}
			foreach (Lamp lamp2 in value.lamps)
			{
				if (!lamp2.glow)
				{
					continue;
				}
				float num6 = lamp2.size;
				bool flag13;
				Color color2;
				switch (lamp2.g)
				{
				case G.Head:
					flag13 = flag8;
					color2 = color * (1.5f * num + 0.2f);
					num6 *= 1f + num * 0.4f;
					break;
				case G.Tail:
					flag13 = flag7 || flag9;
					color2 = new Color(1f, 0.05f, 0.03f) * (flag7 ? 2.1f : 0.75f);
					num6 *= (flag7 ? 1.15f : 0.8f);
					break;
				case G.Rev:
					flag13 = flag6;
					color2 = new Color(1f, 1f, 1f) * 1.3f;
					break;
				case G.IndL:
					flag13 = flag10;
					color2 = new Color(1f, 0.45f, 0.05f) * 2f;
					break;
				default:
					flag13 = flag11;
					color2 = new Color(1f, 0.45f, 0.05f) * 2f;
					break;
				}
				if (!(flag13 & (flag4 && aliveSide[(int)lamp2.g * 2 + lamp2.side] && (!lamp2.dead || lamp2.flickOn))))
				{
					if (lamp2.glow.gameObject.activeSelf)
					{
						lamp2.glow.gameObject.SetActive(value: false);
					}
					continue;
				}
				Vector3 position2 = lamp2.glow.position;
				Vector3 vector = position - position2;
				float magnitude = vector.magnitude;
				float num7 = Mathf.Clamp01((Vector3.Dot(value.shell.TransformDirection(lamp2.n), vector / Mathf.Max(0.001f, magnitude)) - 0.05f) / 0.55f);
				if (num7 <= 0.01f)
				{
					if (lamp2.glow.gameObject.activeSelf)
					{
						lamp2.glow.gameObject.SetActive(value: false);
					}
					continue;
				}
				if (!lamp2.glow.gameObject.activeSelf)
				{
					lamp2.glow.gameObject.SetActive(value: true);
				}
				lamp2.glow.rotation = Quaternion.LookRotation(-vector);
				lamp2.glow.localScale = Vector3.one * num6 * Mathf.Clamp(magnitude / 16f, 1f, 2.6f) * (0.55f + 0.45f * num7) / Mathf.Max(0.01f, value.shell.lossyScale.x);
				MeshRenderer component = lamp2.glow.GetComponent<MeshRenderer>();
				mpb.SetColor("_BaseColor", color2 * num7 * (flag ? 1f : 0.55f));
				component.SetPropertyBlock(mpb);
			}
			value.headL = aliveSide[0];
			value.headR = aliveSide[1];
			if (flag8 && value.hasHead && flag4 && flag && (value.headL || value.headR))
			{
				list.Add((value, flag5 ? (-1f) : sqrMagnitude, true));
			}
			if (flag && value.hasTail && (flag7 || flag9) && sqrMagnitude < 4900f && (aliveSide[2] || aliveSide[3]))
			{
				list2.Add((value, sqrMagnitude, flag7 ? 1f : 0.25f));
			}
			if (flag6 && sqrMagnitude < 3600f)
			{
				list3.Add((value, sqrMagnitude));
			}
			bool A(G g)
			{
				if (anyLamp)
				{
					return aliveSide[(int)g * 2 + sd];
				}
				return true;
			}
		}
		int i = 0;
		List<(Rig, float, bool)> list4 = list.OrderBy<(Rig, float, bool), float>(((Rig r, float d2, bool head) x) => x.d2 * (x.r.hadBeam ? 0.5f : 1f)).ToList();
		foreach (Rig value2 in rigs.Values)
		{
			value2.hadBeam = false;
		}
		foreach (var item4 in list4)
		{
			Rig item = item4.Item1;
			(Vector3, bool)[] array = new(Vector3, bool)[2]
			{
				(item.hl, item.headL),
				(item.hr, item.headR)
			};
			for (int j = 0; j < array.Length; j++)
			{
				(Vector3, bool) tuple = array[j];
				var (position3, _) = tuple;
				if (!tuple.Item2)
				{
					continue;
				}
				if (i >= beams.Count)
				{
					break;
				}
				if (i < beamOwner.Length)
				{
					if (beamOwner[i] != null && beamOwner[i] != item)
					{
						BeamSwaps++;
					}
					beamOwner[i] = item;
				}
				Light light = beams[i++];
				light.enabled = true;
				item.hadBeam = true;
				light.transform.position = item.shell.TransformPoint(position3) + item.car.transform.forward * 0.12f;
				light.transform.rotation = item.car.transform.rotation * Quaternion.Euler(4.5f, (position3.x < 0f) ? (-2.5f) : 2.5f, 0f);
				light.intensity = 55f * num;
				Color? headTint = item.car.headTint;
				light.useColorTemperature = !headTint.HasValue;
				light.color = headTint ?? Color.white;
			}
			if (i >= beams.Count)
			{
				break;
			}
		}
		for (BeamsOn = i; i < beams.Count; i++)
		{
			if ((bool)beams[i])
			{
				beams[i].enabled = false;
			}
			if (i < beamOwner.Length)
			{
				beamOwner[i] = null;
			}
		}
		int k = 0;
		List<(Rig, float, float)> list5 = list2.OrderBy<(Rig, float, float), float>(((Rig r, float d2, float k) x) => x.d2 * (x.r.hadTail ? 0.5f : 1f)).ToList();
		foreach (Rig value3 in rigs.Values)
		{
			value3.hadTail = false;
		}
		foreach (var (rig2, _, num8) in list5)
		{
			if (k >= tailPool.Count)
			{
				break;
			}
			Light light2 = tailPool[k++];
			light2.enabled = true;
			rig2.hadTail = true;
			Vector3 position4 = (rig2.bl + rig2.br) / 2f;
			Transform transform = rig2.car.transform;
			light2.transform.SetPositionAndRotation(rig2.shell.TransformPoint(position4) - transform.forward * 0.14f - transform.up * 0.04f, transform.rotation * Quaternion.Euler(30f, 180f, 0f));
			light2.intensity = ((num8 > 0.5f) ? 4.2f : 1.1f) * num2 + 0.2f;
		}
		for (; k < tailPool.Count; k++)
		{
			tailPool[k].enabled = false;
		}
		int l = 0;
		List<(Rig, float)> list6 = list3.OrderBy<(Rig, float), float>(((Rig r, float d2) x) => x.d2 * (x.r.hadRev ? 0.5f : 1f)).ToList();
		foreach (Rig value4 in rigs.Values)
		{
			value4.hadRev = false;
		}
		foreach (var item5 in list6)
		{
			Rig item2 = item5.Item1;
			if (l >= revPool.Count)
			{
				break;
			}
			Light light3 = revPool[l++];
			light3.enabled = true;
			item2.hadRev = true;
			Vector3 position5 = (item2.bl + item2.br) / 2f;
			light3.transform.position = item2.shell.TransformPoint(position5) - item2.car.transform.forward * 0.14f;
			light3.transform.rotation = item2.car.transform.rotation * Quaternion.Euler(28f, 180f, 0f);
			light3.intensity = 2.6f + 4f * num2;
		}
		for (; l < revPool.Count; l++)
		{
			revPool[l].enabled = false;
		}
	}

	private static void SetLens(Rig r, G g, int side, bool on, Color col)
	{
		MeshRenderer meshRenderer = r.lens[(int)g * 2 + side];
		if ((bool)meshRenderer)
		{
			if (meshRenderer.enabled != on)
			{
				meshRenderer.enabled = on;
				LensToggles++;
			}
			if (on)
			{
				mpb.SetColor("_BaseColor", col);
				meshRenderer.SetPropertyBlock(mpb);
			}
		}
	}
}
}
