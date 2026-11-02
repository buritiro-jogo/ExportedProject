using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public class FBSkyPlanes : MonoBehaviour
{
	private class Trail
	{
		public Mesh mesh;

		public GameObject go;

		public float seed;

		public readonly List<(Vector3 p, float t, float u)> pts = new List<(Vector3, float, float)>();

		public Vector3[] P = new Vector3[760];

		public Color[] C = new Color[760];

		public Vector2[] UV = new Vector2[760];
	}

	private class Flight
	{
		public Vector3 pos;

		public Vector3 dir;

		public float speed;

		public float t;

		public float len;

		public float tt;

		public GameObject plane;

		public readonly List<Trail> trails = new List<Trail>();

		public bool smoke;

		public bool done;

		public AudioSource snd;
	}

	private const float R = 270f;

	private const float TRAIL_DT = 0.2f;

	private const float TRAIL_LIFE = 75f;

	private const int MAXF = 2;

	private const int TRAIL_N = 380;

	private static FBSkyPlanes I;

	private static GameObject proto;

	private static Material trailMat;

	private readonly List<Flight> flights = new List<Flight>();

	public static Transform Current
	{
		get
		{
			if (!I)
			{
				return null;
			}
			return I.flights.Find((Flight x) => !x.done && (bool)x.plane)?.plane.transform;
		}
	}

	public static GameObject Boeing(float len = 68f)
	{
		GameObject gameObject = (FBRegistry.I ? FBRegistry.I.Acc("boeing787") : null);
		if (!gameObject)
		{
			return null;
		}
		GameObject gameObject2 = new GameObject("Boeing787");
		GameObject gameObject3 = UnityEngine.Object.Instantiate(gameObject, gameObject2.transform);
		gameObject3.transform.localPosition = Vector3.zero;
		gameObject3.transform.localRotation = Quaternion.identity;
		Collider[] componentsInChildren = gameObject3.GetComponentsInChildren<Collider>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			UnityEngine.Object.Destroy(componentsInChildren[i]);
		}
		Bounds bounds = default(Bounds);
		bool flag = false;
		Renderer[] componentsInChildren2 = gameObject3.GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer renderer in componentsInChildren2)
		{
			if (!flag)
			{
				bounds = renderer.bounds;
				flag = true;
			}
			else
			{
				bounds.Encapsulate(renderer.bounds);
			}
		}
		float num = ((flag && bounds.size.z > 0.01f) ? (len / bounds.size.z) : 1f);
		gameObject3.transform.localScale *= num;
		gameObject3.transform.localPosition = -bounds.center * num;
		return gameObject2;
	}

	private static GameObject MakePlane()
	{
		GameObject gameObject = Boeing();
		if ((bool)gameObject)
		{
			gameObject.name = "aviao_proto";
			UnityEngine.Object.DontDestroyOnLoad(gameObject);
			Renderer[] componentsInChildren = gameObject.GetComponentsInChildren<Renderer>(includeInactive: true);
			Bounds bounds = default(Bounds);
			bool flag = false;
			Renderer[] array = componentsInChildren;
			foreach (Renderer renderer in array)
			{
				renderer.shadowCastingMode = ShadowCastingMode.Off;
				renderer.receiveShadows = false;
				if (!flag)
				{
					bounds = renderer.bounds;
					flag = true;
				}
				else
				{
					bounds.Encapsulate(renderer.bounds);
				}
			}
			(float, Color)[] array2 = new(float, Color)[2]
			{
				(bounds.max.x * 0.98f, new Color(0.13f, 1f, 0.25f)),
				(bounds.min.x * 0.98f, new Color(1f, 0.13f, 0.13f))
			};
			for (int i = 0; i < array2.Length; i++)
			{
				(float, Color) tuple = array2[i];
				float item = tuple.Item1;
				Color item2 = tuple.Item2;
				GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				UnityEngine.Object.Destroy(obj.GetComponent<Collider>());
				obj.transform.SetParent(gameObject.transform, worldPositionStays: false);
				obj.transform.localPosition = new Vector3(item, bounds.center.y, bounds.center.z);
				obj.transform.localScale = Vector3.one * 1.2f;
				Material material = new Material(FBShaders.Unlit);
				material.SetColor("_BaseColor", item2);
				Renderer component = obj.GetComponent<Renderer>();
				component.sharedMaterial = material;
				component.shadowCastingMode = ShadowCastingMode.Off;
			}
			gameObject.SetActive(value: false);
			return gameObject;
		}
		GameObject g = new GameObject("aviao_proto");
		g.SetActive(value: false);
		UnityEngine.Object.DontDestroyOnLoad(g);
		Material m2 = M(new Color(0.91f, 0.93f, 0.945f));
		Material m3 = M(new Color(0.6f, 0.64f, 0.68f));
		Material m4 = M(new Color(0.22f, 0.25f, 0.29f));
		Add(PrimitiveType.Cylinder, m2, Vector3.zero, new Vector3(4.2f, 17f, 4.2f), new Vector3(90f, 0f, 0f));
		Add(PrimitiveType.Sphere, m2, new Vector3(0f, 0f, 17f), new Vector3(4.2f, 4.2f, 8f), Vector3.zero);
		Add(PrimitiveType.Capsule, m2, new Vector3(0f, 0.5f, -20f), new Vector3(3.2f, 5f, 3.2f), new Vector3(90f, 0f, 0f));
		Add(PrimitiveType.Cube, m3, new Vector3(0f, -0.6f, 1.5f), new Vector3(36f, 0.5f, 6.5f), Vector3.zero);
		Add(PrimitiveType.Cube, m3, new Vector3(0f, 0.8f, -21f), new Vector3(13f, 0.35f, 3.2f), Vector3.zero);
		Add(PrimitiveType.Cube, m2, new Vector3(0f, 4f, -21.5f), new Vector3(0.4f, 7f, 4.5f), new Vector3(-20f, 0f, 0f));
		float[] array3 = new float[2] { -7.5f, 7.5f };
		foreach (float x in array3)
		{
			Add(PrimitiveType.Cylinder, m4, new Vector3(x, -2f, 3.5f), new Vector3(2.4f, 2.5f, 2.4f), new Vector3(90f, 0f, 0f));
		}
		Add(PrimitiveType.Sphere, M(new Color(1f, 0.13f, 0.13f), em: true), new Vector3(18f, -0.6f, 1.5f), Vector3.one, Vector3.zero);
		Add(PrimitiveType.Sphere, M(new Color(0.13f, 1f, 0.25f), em: true), new Vector3(-18f, -0.6f, 1.5f), Vector3.one, Vector3.zero);
		return g;
		void Add(PrimitiveType t, Material m, Vector3 p, Vector3 s, Vector3 r)
		{
			GameObject obj2 = GameObject.CreatePrimitive(t);
			UnityEngine.Object.Destroy(obj2.GetComponent<Collider>());
			obj2.transform.SetParent(g.transform, worldPositionStays: false);
			obj2.transform.localPosition = p;
			obj2.transform.localScale = s;
			obj2.transform.localEulerAngles = r;
			Renderer component2 = obj2.GetComponent<Renderer>();
			component2.sharedMaterial = m;
			component2.shadowCastingMode = ShadowCastingMode.Off;
		}
		static Material M(Color c, bool em = false)
		{
			Material material2 = new Material(em ? FBShaders.Unlit : FBShaders.Lit);
			material2.SetColor("_BaseColor", c);
			if (!em)
			{
				material2.SetFloat("_Smoothness", 0.35f);
			}
			return material2;
		}
	}

	private static void Ensure()
	{
		if ((bool)I)
		{
			return;
		}
		I = new GameObject("FB_AvioesCeu").AddComponent<FBSkyPlanes>();
		if (!proto)
		{
			proto = MakePlane();
		}
		if (!trailMat)
		{
			Shader contrail = FBShaders.Contrail;
			if ((bool)contrail)
			{
				trailMat = new Material(contrail)
				{
					name = "rastro_aviao"
				};
				trailMat.SetTexture("_NoiseTex", NoiseTex());
			}
			else
			{
				trailMat = FBShaders.Transparent(new Material(FBShaders.ParticlesUnlit));
				trailMat.SetFloat("_Cull", 0f);
			}
		}
	}

	private static Texture2D NoiseTex()
	{
		Texture2D texture2D = new Texture2D(128, 64, TextureFormat.R8, mipChain: true)
		{
			wrapMode = TextureWrapMode.Repeat,
			filterMode = FilterMode.Bilinear,
			name = "ruido_rastro"
		};
		byte[] array = new byte[8192];
		for (int i = 0; i < 64; i++)
		{
			for (int j = 0; j < 128; j++)
			{
				float num = (float)j / 128f;
				float num2 = (float)i / 64f;
				float num3 = 0f;
				float num4 = 0.55f;
				float num5 = 0f;
				int num6 = 4;
				for (int k = 0; k < 5; k++)
				{
					num3 += Vn(num * (float)num6, num2 * (float)num6, num6) * num4;
					num5 += num4;
					num4 *= 0.5f;
					num6 *= 2;
				}
				array[i * 128 + j] = (byte)(Mathf.Clamp01(num3 / num5) * 255f);
			}
		}
		texture2D.SetPixelData(array, 0);
		texture2D.Apply(updateMipmaps: true, makeNoLongerReadable: true);
		return texture2D;
		static float Hs(int x, int y, int p)
		{
			x = (x % p + p) % p;
			y = (y % p + p) % p;
			int num7 = x * 374761393 + y * 668265263 + p * 982451653;
			return (float)((uint)((num7 ^ (int)((uint)num7 >> 13)) * 1274126177) & 0xFFFFu) / 65535f;
		}
		static float Vn(float x, float y, int p)
		{
			int num8 = Mathf.FloorToInt(x);
			int num9 = Mathf.FloorToInt(y);
			float num10 = x - (float)num8;
			float num11 = y - (float)num9;
			num10 = num10 * num10 * (3f - 2f * num10);
			num11 = num11 * num11 * (3f - 2f * num11);
			return Mathf.Lerp(Mathf.Lerp(Hs(num8, num9, p), Hs(num8 + 1, num9, p), num10), Mathf.Lerp(Hs(num8, num9 + 1, p), Hs(num8 + 1, num9 + 1, p), num10), num11);
		}
	}

	private Trail MakeTrail()
	{
		Trail trail = new Trail
		{
			seed = UnityEngine.Random.value
		};
		trail.mesh = new Mesh();
		trail.mesh.MarkDynamic();
		trail.mesh.vertices = trail.P;
		trail.mesh.colors = trail.C;
		trail.mesh.uv = trail.UV;
		int[] array = new int[2274];
		for (int i = 0; i < 379; i++)
		{
			int num = (array[i * 6] = i * 2);
			array[i * 6 + 1] = num + 1;
			array[i * 6 + 2] = num + 2;
			array[i * 6 + 3] = num + 2;
			array[i * 6 + 4] = num + 1;
			array[i * 6 + 5] = num + 3;
		}
		trail.mesh.triangles = array;
		trail.mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
		trail.go = new GameObject("rastro");
		trail.go.transform.SetParent(base.transform, worldPositionStays: false);
		trail.go.AddComponent<MeshFilter>().sharedMesh = trail.mesh;
		MeshRenderer meshRenderer = trail.go.AddComponent<MeshRenderer>();
		meshRenderer.sharedMaterial = trailMat;
		meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
		return trail;
	}

	public static Vector3? TrailMid()
	{
		if (!I)
		{
			return null;
		}
		for (int num = I.flights.Count - 1; num >= 0; num--)
		{
			foreach (Trail trail in I.flights[num].trails)
			{
				if (trail.pts.Count > 4)
				{
					return trail.pts[trail.pts.Count / 2].p;
				}
			}
		}
		return null;
	}

	public static void Reset()
	{
		if (!I)
		{
			return;
		}
		foreach (Flight flight in I.flights)
		{
			I.Drop(flight);
		}
		I.flights.Clear();
	}

	private void Drop(Flight f)
	{
		if ((bool)f.plane)
		{
			UnityEngine.Object.Destroy(f.plane);
		}
		foreach (Trail trail in f.trails)
		{
			if ((bool)trail.go)
			{
				UnityEngine.Object.Destroy(trail.go);
			}
		}
		if ((bool)f.snd)
		{
			UnityEngine.Object.Destroy(f.snd.gameObject);
		}
	}

	public static void Flyover(Vector3 at, float baseY = 0f, bool? smoke = null)
	{
		Ensure();
		int num = 0;
		foreach (Flight flight2 in I.flights)
		{
			if (!flight2.done)
			{
				num++;
			}
		}
		if (num >= 2)
		{
			return;
		}
		float f = UnityEngine.Random.value * (float)Math.PI * 2f;
		Vector3 vector = new Vector3(Mathf.Sin(f), 0f, Mathf.Cos(f));
		Vector3 vector2 = new Vector3(vector.z, 0f, 0f - vector.x);
		float y = baseY + 450f + UnityEngine.Random.value * 300f;
		float num2 = (UnityEngine.Random.value - 0.5f) * 700f;
		float num3 = 120f + UnityEngine.Random.value * 45f;
		float num4 = 2600f;
		Flight flight = new Flight
		{
			pos = new Vector3(at.x, y, at.z) + vector2 * num2 - vector * num4,
			dir = vector,
			speed = num3,
			len = num4 * 2f / num3,
			smoke = (smoke ?? (UnityEngine.Random.value < 0.45f))
		};
		flight.plane = UnityEngine.Object.Instantiate(proto, I.transform);
		flight.plane.SetActive(value: true);
		flight.plane.transform.rotation = Quaternion.LookRotation(vector) * Quaternion.Euler(0f, 0f, (UnityEngine.Random.value - 0.5f) * 4.6f);
		if (flight.smoke)
		{
			for (int i = 0; i < 2; i++)
			{
				flight.trails.Add(I.MakeTrail());
			}
		}
		AudioClip audioClip = FBAudio.Clip("amb_plane");
		if ((bool)audioClip)
		{
			GameObject obj = new GameObject("som_aviao");
			obj.transform.SetParent(I.transform, worldPositionStays: false);
			obj.transform.position = flight.pos;
			AudioSource audioSource = obj.AddComponent<AudioSource>();
			audioSource.clip = audioClip;
			audioSource.loop = true;
			audioSource.spatialBlend = 1f;
			audioSource.dopplerLevel = 0f;
			audioSource.rolloffMode = AudioRolloffMode.Linear;
			audioSource.minDistance = 220f;
			audioSource.maxDistance = 3200f;
			audioSource.volume = 0f;
			audioSource.pitch = 0.85f + UnityEngine.Random.value * 0.2f;
			audioSource.Play();
			flight.snd = audioSource;
		}
		I.flights.Add(flight);
	}

	private static readonly int __fbt_LateUpdate = FBTempo.Id("FBSkyPlanes.LateUpdate");

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
		Camera main = Camera.main;
		if (!main || flights.Count == 0)
		{
			return;
		}
		float deltaTime = Time.deltaTime;
		Vector3 position = main.transform.position;
		if ((bool)trailMat && trailMat.HasProperty("_SunCol"))
		{
			Light sun = RenderSettings.sun;
			float t = (sun ? Mathf.Clamp01(sun.intensity / 1.25f) : 0.5f);
			Color color = (sun ? sun.color : Color.white);
			Color color2 = RenderSettings.ambientSkyColor * RenderSettings.ambientIntensity;
			trailMat.SetColor("_SunCol", Color.Lerp(color2 * 1.1f, color * 1.05f, Mathf.Lerp(0.15f, 1f, t)) + color2 * 0.25f);
			trailMat.SetColor("_ShadeCol", Color.Lerp(color2 * 0.55f, color2 * 0.85f + color * 0.15f, t));
			trailMat.SetFloat("_MaxAlpha", Mathf.Lerp(0.35f, 0.85f, t));
		}
		for (int num = flights.Count - 1; num >= 0; num--)
		{
			Flight flight = flights[num];
			if (!flight.done)
			{
				flight.t += deltaTime;
				flight.pos += flight.dir * (flight.speed * deltaTime);
				if ((bool)flight.snd)
				{
					flight.snd.transform.position = flight.pos;
					float num2 = Vector3.Distance(flight.pos, position);
					flight.snd.volume = Mathf.Lerp(flight.snd.volume, 0.13f * Mathf.Clamp01((2200f - num2) / 900f) * FBAudio.SfxVol * FBAudio.Master, 1f - Mathf.Exp((0f - deltaTime) / 0.3f));
				}
				if (flight.smoke && (flight.tt += deltaTime) >= 0.2f)
				{
					flight.tt = 0f;
					Vector3 vector = new Vector3(flight.dir.z, 0f, 0f - flight.dir.x);
					for (int i = 0; i < flight.trails.Count; i++)
					{
						Trail trail = flight.trails[i];
						trail.pts.Add((flight.pos - flight.dir * 8f + vector * ((i == 1) ? 7.5f : (-7.5f)) + Vector3.down * 2f, 0f, flight.t * flight.speed / 55f));
						if (trail.pts.Count > 380)
						{
							trail.pts.RemoveAt(0);
						}
					}
				}
				if (flight.t >= flight.len)
				{
					flight.done = true;
					if ((bool)flight.plane)
					{
						UnityEngine.Object.Destroy(flight.plane);
					}
					if ((bool)flight.snd)
					{
						UnityEngine.Object.Destroy(flight.snd.gameObject);
					}
				}
			}
			if (!flight.done && (bool)flight.plane)
			{
				Vector3 vector2 = flight.pos - position;
				float magnitude = vector2.magnitude;
				float num3 = 270f / magnitude;
				flight.plane.transform.position = position + vector2 * num3;
				flight.plane.transform.localScale = Vector3.one * num3;
				flight.plane.SetActive(magnitude < 3200f);
			}
			bool flag = false;
			Vector4 globalVector = Shader.GetGlobalVector("_FBWind");
			Vector3 vector3 = new Vector3(globalVector.x, 0f, globalVector.y) * (2.5f + 2f * globalVector.z) + Vector3.down * 0.35f;
			Vector3 vector4 = new Vector3(flight.dir.z, 0f, 0f - flight.dir.x);
			for (int j = 0; j < flight.trails.Count; j++)
			{
				Trail trail2 = flight.trails[j];
				float num4 = ((j != 1) ? 1 : (-1));
				for (int k = 0; k < trail2.pts.Count; k++)
				{
					(Vector3, float, float) tuple = trail2.pts[k];
					float num5 = ((tuple.Item2 < 30f) ? 0.16f : 0f);
					trail2.pts[k] = (tuple.Item1 + (vector3 + vector4 * (num4 * num5)) * deltaTime, tuple.Item2 + deltaTime, tuple.Item3);
				}
				while (trail2.pts.Count > 0 && trail2.pts[0].t > 75f)
				{
					trail2.pts.RemoveAt(0);
				}
				int count = trail2.pts.Count;
				for (int l = 0; l < trail2.C.Length; l++)
				{
					trail2.C[l] = default(Color);
				}
				if (count >= 2)
				{
					flag = true;
					for (int m = 0; m < count; m++)
					{
						(Vector3, float, float) tuple2 = trail2.pts[m];
						Vector3 item = trail2.pts[Mathf.Min(count - 1, m + 1)].p;
						Vector3 item2 = trail2.pts[Mathf.Max(0, m - 1)].p;
						Vector3 vector5 = tuple2.Item1 - position;
						float magnitude2 = vector5.magnitude;
						float num6 = 270f / magnitude2;
						Vector3 vector6 = Vector3.Cross((item - item2).normalized, vector5).normalized;
						if (Vector3.Dot(vector6, Vector3.up) < 0f)
						{
							vector6 = -vector6;
						}
						float num7 = (1.4f + Mathf.Sqrt(tuple2.Item2) * 3.1f + tuple2.Item2 * 0.25f) * num6;
						Vector3 vector7 = position + vector5 * num6;
						trail2.P[m * 2] = vector7 - vector6 * num7;
						trail2.P[m * 2 + 1] = vector7 + vector6 * num7;
						float a = Mathf.SmoothStep(0f, 1f, (tuple2.Item2 - 0.6f) * 1.4f) * Mathf.Max(0f, 1f - tuple2.Item2 / 75f) * (float)((m != count - 1 && m != 0) ? 1 : 0);
						trail2.C[m * 2] = (trail2.C[m * 2 + 1] = new Color(Mathf.Clamp01(tuple2.Item2 / 75f), trail2.seed, 0f, a));
						trail2.UV[m * 2] = new Vector2(tuple2.Item3, 0f);
						trail2.UV[m * 2 + 1] = new Vector2(tuple2.Item3, 1f);
					}
					for (int n = count; n < 380; n++)
					{
						trail2.P[n * 2] = trail2.P[(count - 1) * 2];
						trail2.P[n * 2 + 1] = trail2.P[(count - 1) * 2 + 1];
						trail2.UV[n * 2] = trail2.UV[(count - 1) * 2];
						trail2.UV[n * 2 + 1] = trail2.UV[(count - 1) * 2 + 1];
					}
				}
				trail2.mesh.vertices = trail2.P;
				trail2.mesh.colors = trail2.C;
				trail2.mesh.uv = trail2.UV;
			}
			if (flight.done && !flag)
			{
				Drop(flight);
				flights.RemoveAt(num);
			}
		}
	}
}
}
