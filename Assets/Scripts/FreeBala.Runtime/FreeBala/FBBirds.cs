using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public class FBBirds : MonoBehaviour
{
	private class Bird
	{
		public Transform root;

		public Transform head;

		public Transform wL;

		public Transform wR;

		public Transform wL2;

		public Transform wR2;

		public Vector3 pos;

		public Vector3 vel;

		public Vector3 off;

		public float yaw;

		public float phase;

		public float rate = 6f;

		public float amp;

		public float delay;

		public float peckT;

		public float stepT;

		public float glide;

		public Vector3 walkTo;

		public float walkWait;
	}

	private enum St
	{
		Ground,
		TakeOff,
		Fly,
		Land,
		Roost
	}

	private class Flock
	{
		public List<Bird> b = new List<Bird>();

		public St st;

		public Vector3 spot;

		public Vector3 center;

		public Vector3 fleeFrom;

		public float t;

		public float ang;

		public float radius;

		public float alt;

		public float flyTime;

		public bool roof;

		// BURITIRO: bando pousado longe da câmera fica desligado
		public bool oculto;
	}

	private class Vulture
	{
		public Bird b;

		public Vector3 c;

		public float r;

		public float a;

		public float alt;

		public float spd;

		public float flapT;

		public Vector2 drift;
	}

	private readonly List<Flock> flocks = new List<Flock>();

	private readonly List<Vulture> vultures = new List<Vulture>();

	private readonly List<(Vector3 p, bool roof)> spots = new List<(Vector3, bool)>();

	private static Mesh mBody;

	private static Mesh mHead;

	private static Mesh mWing;

	private static Mesh mWing2;

	private static Mesh vBody;

	private static Mesh vHead;

	private static Mesh vWing;

	private static Mesh vWing2;

	private static Material matP;

	private static Material matPW;

	private static Material matV;

	private static Material matVT;

	private static Material matHead;

	private float groundY;

	private bool night;

	private bool rain;

	private bool mobile;

	private AudioClip flapClip;

	private float lastNoise = -99f;

	public static FBBirds I;

	public static FBBirds Begin(Transform parent, float groundY, bool night, bool rain)
	{
		GameObject obj = new GameObject("FB_Passaros");
		obj.transform.SetParent(parent, worldPositionStays: false);
		FBBirds fBBirds = obj.AddComponent<FBBirds>();
		fBBirds.groundY = groundY;
		fBBirds.night = night;
		fBBirds.rain = rain;
		return fBBirds;
	}

	private void Start()
	{
		I = this;
		mobile = FBQuality.Mobile;
		Meshes();
		FindSpots();
		if (spots.Count == 0)
		{
			return;
		}
		System.Random random = new System.Random(1234);
		int num = (mobile ? 2 : 4);
		List<(Vector3, bool)> list = spots.FindAll(((Vector3 p, bool roof) s) => !s.roof);
		List<(Vector3, bool)> list2 = spots.FindAll(((Vector3 p, bool roof) s) => s.roof);
		for (int i = 0; i < num; i++)
		{
			List<(Vector3, bool)> list3 = ((night && list2.Count > 0) ? list2 : ((list.Count > 0) ? list : list2));
			if (list3.Count == 0)
			{
				break;
			}
			(Vector3, bool) tuple = list3[(i * 7 + random.Next(list3.Count)) % list3.Count];
			Flock flock = new Flock
			{
				spot = tuple.Item1,
				roof = tuple.Item2,
				st = (night ? St.Roost : St.Ground)
			};
			int num2 = random.Next(mobile ? 5 : 6, mobile ? 8 : 13);
			for (int j = 0; j < num2; j++)
			{
				Bird bird = NewBird(vulture: false);
				float f2 = (float)random.NextDouble() * 6.28f;
				float num3 = (float)random.NextDouble() * 1.8f;
				bird.off = new Vector3(Mathf.Cos(f2) * num3, 0f, Mathf.Sin(f2) * num3);
				bird.pos = Snap(tuple.Item1 + bird.off, tuple.Item1.y);
				bird.yaw = (float)random.NextDouble() * 360f;
				bird.walkTo = bird.pos;
				bird.walkWait = (float)random.NextDouble() * 2f;
				flock.b.Add(bird);
			}
			flocks.Add(flock);
		}
		if (!night && !rain)
		{
			Vector3 zero = Vector3.zero;
			foreach (var item in list)
			{
				zero += item.Item1;
			}
			zero /= (float)Mathf.Max(1, list.Count);
			int num4 = (mobile ? 2 : 4);
			for (int k = 0; k < num4; k++)
			{
				Vulture vulture = new Vulture
				{
					b = NewBird(vulture: true),
					c = zero + new Vector3((float)random.NextDouble() * 120f - 60f, 0f, (float)random.NextDouble() * 120f - 60f),
					r = 25f + (float)random.NextDouble() * 22f,
					a = (float)random.NextDouble() * 6.28f,
					alt = groundY + 70f + (float)random.NextDouble() * 40f,
					spd = 9f + (float)random.NextDouble() * 2.5f,
					flapT = 5f + (float)random.NextDouble() * 20f,
					drift = new Vector2((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f) * 0.6f
				};
				if (k % 2 == 1 && vultures.Count > 0)
				{
					vulture.c = vultures[k - 1].c + new Vector3(8f, 0f, -6f);
					vulture.drift = vultures[k - 1].drift;
				}
				vultures.Add(vulture);
			}
		}
		flapClip = FlapClip();
		Debug.Log($"[FreeBala][passaros] lugares {spots.Count} (telhados {spots.FindAll(((Vector3 p, bool roof) s) => s.roof).Count}) · bandos {flocks.Count} ({flocks.Sum((Flock f) => f.b.Count)} pombos) · urubus {vultures.Count}");
	}

	private void OnDestroy()
	{
		if (I == this)
		{
			I = null;
		}
	}

	public Vector3? GroundFlock(Vector3 near)
	{
		Vector3? result = null;
		float num = 1E+09f;
		foreach (Flock flock in flocks)
		{
			if (flock.st == St.Ground || flock.st == St.Roost)
			{
				float sqrMagnitude = (flock.center - near).sqrMagnitude;
				if (sqrMagnitude < num)
				{
					num = sqrMagnitude;
					result = flock.center;
				}
			}
		}
		return result;
	}

	public Vector3? VulturePos()
	{
		if (vultures.Count <= 0)
		{
			return null;
		}
		return vultures[0].b.pos;
	}

	private static Material Mat(Color c, float sm = 0.2f)
	{
		Material material = new Material(FBShaders.Lit);
		material.name = "FB_passaro";
		material.enableInstancing = true;
		material.SetColor("_BaseColor", c);
		material.SetFloat("_Smoothness", sm);
		material.SetFloat("_Metallic", 0f);
		return material;
	}

	private static void Meshes()
	{
		if (!mBody)
		{
			matP = Mat(new Color(0.36f, 0.38f, 0.44f));
			matHead = Mat(new Color(0.22f, 0.26f, 0.3f), 0.35f);
			matPW = Mat(new Color(0.55f, 0.57f, 0.62f));
			matV = Mat(new Color(0.05f, 0.05f, 0.055f), 0.25f);
			matVT = Mat(new Color(0.62f, 0.62f, 0.6f));
			mBody = Body(0.15f, 0.11f, 0.062f, 0.075f, 0.05f, 0.2f, 0.05f);
			mHead = Head(0.032f, 0.028f, new Color(0.9f, 0.7f, 0.2f));
			mWing = Wing(0.16f, 0.11f, 0.085f);
			mWing2 = Wing(0.17f, 0.085f, 0.04f);
			vBody = Body(0.3f, 0.22f, 0.1f, 0.12f, 0.09f, 0.35f, 0.1f);
			vHead = Head(0.045f, 0.05f, Color.gray);
			vWing = Wing(0.34f, 0.3f, 0.27f);
			vWing2 = Wing(0.4f, 0.27f, 0.12f, 5);
		}
	}

	private static Mesh Body(float front, float back, float w, float top, float bot, float tail, float tailW)
	{
		Vector3[] array = new Vector3[6]
		{
			new Vector3(0f, 0.01f, front),
			new Vector3(0f, 0f, 0f - back),
			new Vector3(0f, top, 0f),
			new Vector3(0f, 0f - bot, 0.01f),
			new Vector3(0f - w, 0f, 0f),
			new Vector3(w, 0f, 0f)
		};
		List<int[]> list = new List<int[]>();
		list.Add(new int[3] { 0, 2, 5 });
		list.Add(new int[3] { 0, 4, 2 });
		list.Add(new int[3] { 0, 5, 3 });
		list.Add(new int[3] { 0, 3, 4 });
		list.Add(new int[3] { 1, 5, 2 });
		list.Add(new int[3] { 1, 2, 4 });
		list.Add(new int[3] { 1, 3, 5 });
		list.Add(new int[3] { 1, 4, 3 });
		List<Vector3> v = new List<Vector3>();
		List<int> t = new List<int>();
		foreach (int[] item in list)
		{
			int count = v.Count;
			v.Add(array[item[0]]);
			v.Add(array[item[1]]);
			v.Add(array[item[2]]);
			t.AddRange(new int[3]
			{
				count,
				count + 1,
				count + 2
			});
		}
		Q(new Vector3((0f - tailW) * 0.5f, 0.01f, (0f - back) * 0.8f), new Vector3(tailW * 0.5f, 0.01f, (0f - back) * 0.8f), new Vector3(tailW, 0f, 0f - back - tail * 0.5f), new Vector3(0f - tailW, 0f, 0f - back - tail * 0.5f));
		return Make(v, t, "FB_ave_corpo");
		void Q(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
		{
			int count2 = v.Count;
			v.Add(a);
			v.Add(b);
			v.Add(c);
			v.Add(d);
			t.AddRange(new int[12]
			{
				count2,
				count2 + 1,
				count2 + 2,
				count2,
				count2 + 2,
				count2 + 3,
				count2,
				count2 + 2,
				count2 + 1,
				count2,
				count2 + 3,
				count2 + 2
			});
		}
	}

	private static Mesh Head(float r, float beak, Color _)
	{
		Vector3[] array = new Vector3[7]
		{
			new Vector3(0f, 0f, r),
			new Vector3(0f, 0f, 0f - r),
			new Vector3(0f, r, 0f),
			new Vector3(0f, (0f - r) * 0.8f, 0f),
			new Vector3((0f - r) * 0.8f, 0f, 0f),
			new Vector3(r * 0.8f, 0f, 0f),
			new Vector3(0f, (0f - r) * 0.2f, r + beak)
		};
		int[][] obj = new int[12][]
		{
			new int[3] { 0, 2, 5 },
			new int[3] { 0, 4, 2 },
			new int[3] { 0, 5, 3 },
			new int[3] { 0, 3, 4 },
			new int[3] { 1, 5, 2 },
			new int[3] { 1, 2, 4 },
			new int[3] { 1, 3, 5 },
			new int[3] { 1, 4, 3 },
			new int[3] { 6, 0, 5 },
			new int[3] { 6, 4, 0 },
			new int[3] { 6, 3, 4 },
			new int[3] { 6, 5, 3 }
		};
		List<Vector3> list = new List<Vector3>();
		List<int> list2 = new List<int>();
		int[][] array2 = obj;
		foreach (int[] array3 in array2)
		{
			int count = list.Count;
			list.Add(array[array3[0]]);
			list.Add(array[array3[1]]);
			list.Add(array[array3[2]]);
			list2.AddRange(new int[3]
			{
				count,
				count + 1,
				count + 2
			});
		}
		return Make(list, list2, "FB_ave_cabeca");
	}

	private static Mesh Wing(float len, float rootC, float tipC, int fingers = 0)
	{
		List<Vector3> v = new List<Vector3>();
		List<int> t = new List<int>();
		Vector3 a2 = new Vector3(0f, 0f, rootC * 0.35f);
		Vector3 c2 = new Vector3(0f, 0f, (0f - rootC) * 0.65f);
		Vector3 b2 = new Vector3(len, 0f, tipC * 0.2f - len * 0.12f);
		Vector3 vector = new Vector3(len * 0.96f, 0f, (0f - tipC) * 0.8f - len * 0.18f);
		Tri(a2, b2, vector);
		Tri(a2, vector, c2);
		for (int i = 0; i < fingers; i++)
		{
			float num = Mathf.Lerp(b2.z, vector.z, ((float)i + 0.2f) / (float)fingers);
			float num2 = Mathf.Lerp(b2.z, vector.z, ((float)i + 0.8f) / (float)fingers);
			Tri(new Vector3(len * 0.97f, 0f, num), new Vector3(len * 1.14f, 0f, (num + num2) * 0.5f - 0.02f), new Vector3(len * 0.97f, 0f, num2));
		}
		return Make(v, t, "FB_ave_asa");
		void Tri(Vector3 a, Vector3 b, Vector3 c)
		{
			int count = v.Count;
			v.Add(a);
			v.Add(b);
			v.Add(c);
			t.AddRange(new int[3]
			{
				count,
				count + 1,
				count + 2
			});
			count = v.Count;
			v.Add(a);
			v.Add(c);
			v.Add(b);
			t.AddRange(new int[3]
			{
				count,
				count + 1,
				count + 2
			});
		}
	}

	private static Mesh Make(List<Vector3> v, List<int> t, string n)
	{
		Mesh mesh = new Mesh();
		mesh.name = n;
		mesh.SetVertices(v);
		mesh.SetTriangles(t, 0);
		mesh.RecalculateNormals();
		mesh.RecalculateBounds();
		return mesh;
	}

	private Bird NewBird(bool vulture)
	{
		Bird bird = new Bird();
		Transform transform = new GameObject(vulture ? "Urubu" : "Pombo").transform;
		transform.SetParent(base.transform, worldPositionStays: false);
		bird.root = transform;
		// BURITIRO: urubu voa a 70-110 m (sombra não aparece) e a cabeça é pequena demais para sombra
		Part(transform, vulture ? vBody : mBody, vulture ? matV : matP, !vulture);
		bird.head = new GameObject("cabeca").transform;
		bird.head.SetParent(transform, worldPositionStays: false);
		bird.head.localPosition = (vulture ? new Vector3(0f, 0.07f, 0.3f) : new Vector3(0f, 0.08f, 0.14f));
		Part(bird.head, vulture ? vHead : mHead, vulture ? matHead : matHead, shadow: false);
		float num = (vulture ? 0.07f : 0.04f);
		float y = (vulture ? 0.06f : 0.035f);
		float x = (vulture ? 0.34f : 0.16f);
		bird.wR = W("asaD", transform, new Vector3(num, y, 0.01f), vulture ? vWing : mWing, vulture ? matV : matPW, mirror: false);
		bird.wL = W("asaE", transform, new Vector3(0f - num, y, 0.01f), vulture ? vWing : mWing, vulture ? matV : matPW, mirror: true);
		bird.wR2 = W("pontaD", bird.wR, new Vector3(x, 0f, 0f), vulture ? vWing2 : mWing2, vulture ? matVT : matPW, mirror: false);
		bird.wL2 = W("pontaE", bird.wL, new Vector3(x, 0f, 0f), vulture ? vWing2 : mWing2, vulture ? matVT : matPW, mirror: false);
		bird.phase = UnityEngine.Random.value * 6.28f;
		bird.rate = (vulture ? 2.4f : UnityEngine.Random.Range(5.5f, 7.5f));
		if (vulture)
		{
			transform.localScale = Vector3.one * UnityEngine.Random.Range(0.95f, 1.1f);
		}
		else
		{
			transform.localScale = Vector3.one * UnityEngine.Random.Range(1.05f, 1.25f);
		}
		return bird;
		static Transform W(string n, Transform par, Vector3 lp, Mesh m, Material mt, bool mirror)
		{
			Transform transform2 = new GameObject(n).transform;
			transform2.SetParent(par, worldPositionStays: false);
			transform2.localPosition = lp;
			if (mirror)
			{
				transform2.localScale = new Vector3(-1f, 1f, 1f);
			}
			Part(transform2, m, mt, shadow: false);
			return transform2;
		}
	}

	private static void Part(Transform t, Mesh m, Material mt, bool shadow = true)
	{
		t.gameObject.AddComponent<MeshFilter>().sharedMesh = m;
		MeshRenderer meshRenderer = t.gameObject.AddComponent<MeshRenderer>();
		meshRenderer.sharedMaterial = mt;
		meshRenderer.shadowCastingMode = (shadow ? ShadowCastingMode.On : ShadowCastingMode.Off);
		meshRenderer.receiveShadows = true;
		meshRenderer.lightProbeUsage = LightProbeUsage.Off;
		meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
	}

	private void FindSpots()
	{
		if (!FBNav.Ready)
		{
			return;
		}
		List<Vector3> list = FBNav.Fixed();
		int num = Mathf.Max(1, list.Count / 160);
		for (int i = 0; i < list.Count; i += num)
		{
			if (Physics.Raycast(list[i] + Vector3.up * 2f, Vector3.down, out var hitInfo, 5f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) && !(hitInfo.normal.y < 0.95f) && !Physics.Raycast(hitInfo.point + Vector3.up * 0.3f, Vector3.up, 25f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) && !FBWorld.WaterAt(hitInfo.point.x, hitInfo.point.z).HasValue && !Physics.CheckSphere(hitInfo.point + Vector3.up * 1.9f, 1.3f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore))
			{
				spots.Add((hitInfo.point, false));
				if (Physics.Raycast(hitInfo.point + new Vector3(Mathf.Sin((float)i * 1.7f), 0f, Mathf.Cos((float)i * 1.7f)) * 18f + Vector3.up * 40f, Vector3.down, out var hitInfo2, 60f, 256, QueryTriggerInteraction.Ignore) && hitInfo2.normal.y > 0.9f && hitInfo2.point.y > hitInfo.point.y + 3.2f && hitInfo2.point.y < hitInfo.point.y + 20f)
				{
					spots.Add((hitInfo2.point, true));
				}
			}
		}
	}

	private static Vector3 Snap(Vector3 p, float fallback)
	{
		if (!Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var hitInfo, 4f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore))
		{
			return new Vector3(p.x, fallback, p.z);
		}
		return hitInfo.point;
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBBirds.Update");

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
		float dt = Mathf.Min(Time.deltaTime, 0.05f);
		_ = Camera.main;
		bool flag = Time.time - FBBotBrain.lastNoiseT < 0.25f && FBBotBrain.lastNoiseT != lastNoise;
		Vector3 shotP = FBBotBrain.lastNoiseP ?? Vector3.zero;
		if (flag)
		{
			lastNoise = FBBotBrain.lastNoiseT;
		}
		foreach (Flock flock in flocks)
		{
			TickFlock(flock, dt, flag, shotP);
		}
		foreach (Vulture vulture in vultures)
		{
			TickVulture(vulture, dt);
		}
	}

	private Vector3? Threat(Vector3 c, float r)
	{
		foreach (FBCharacter item in FBMatch.All)
		{
			if ((bool)item && item.alive)
			{
				Vector3 pos = item.Pos;
				if ((pos - c).sqrMagnitude < r * r && Mathf.Abs(pos.y - c.y) < 4f)
				{
					return pos;
				}
			}
		}
		Camera main = Camera.main;
		if ((bool)main && (main.transform.position - c).sqrMagnitude < r * r * 0.6f)
		{
			return main.transform.position;
		}
		return null;
	}

	private void TickFlock(Flock f, float dt, bool shot, Vector3 shotP)
	{
		f.t += dt;
		Vector3 zero = Vector3.zero;
		foreach (Bird item in f.b)
		{
			zero += item.pos;
		}
		zero /= (float)f.b.Count;
		f.center = zero;
		// BURITIRO: pombo de 15 cm a mais de 80 m vira 1 ou 2 pixels; pousado assim ele some (menos draw calls e sombras) e volta a menos de 70 m
		bool longe = (f.st == St.Ground || f.st == St.Roost) && cAm(zero) > (f.oculto ? 70f : 80f);
		if (longe != f.oculto)
		{
			f.oculto = longe;
			foreach (Bird bd in f.b)
			{
				if ((bool)bd.root)
				{
					bd.root.gameObject.SetActive(!longe);
				}
			}
		}
		switch (f.st)
		{
		case St.Ground:
		case St.Roost:
		{
			Vector3? vector2 = Threat(zero, (f.st == St.Roost) ? 3.5f : 5.5f);
			if ((shot && (shotP - zero).sqrMagnitude < 2025f) || vector2.HasValue)
			{
				TakeOff(f, vector2.GetValueOrDefault(shotP));
				break;
			}
			if (f.st == St.Ground && !night && f.t > 25f && UnityEngine.Random.value < dt / 40f)
			{
				TakeOff(f, zero + UnityEngine.Random.onUnitSphere * 3f);
			}
			if (!f.oculto)
			{
				foreach (Bird item2 in f.b)
				{
					GroundBird(item2, f, dt);
				}
			}
			if (!night && UnityEngine.Random.value < dt / 9f && cAm(zero) < 30f)
			{
				FBAudio.PlayAt("amb_pigeon0", zero, 0.35f, 3f, 0.12f);
			}
			break;
		}
		case St.TakeOff:
		case St.Fly:
		{
			f.ang += dt * 9.5f / Mathf.Max(6f, f.radius);
			Vector3 tgt2 = f.spot + new Vector3(Mathf.Cos(f.ang) * f.radius, 0f, Mathf.Sin(f.ang) * f.radius);
			tgt2.y = f.alt;
			if (f.st == St.TakeOff && f.t > 1.2f)
			{
				f.st = St.Fly;
			}
			if (f.st == St.Fly && f.t > f.flyTime)
			{
				ChooseLanding(f);
				f.st = St.Land;
				f.t = 0f;
			}
			foreach (Bird item3 in f.b)
			{
				FlyBird(item3, f, tgt2, dt, (f.st == St.TakeOff) ? 9.5f : 6.2f);
			}
			break;
		}
		case St.Land:
		{
			int num = 0;
			foreach (Bird item4 in f.b)
			{
				Vector3 vector = f.spot + item4.off;
				float magnitude = new Vector2(vector.x - item4.pos.x, vector.z - item4.pos.z).magnitude;
				if (item4.vel == Vector3.zero)
				{
					num++;
					GroundBird(item4, f, dt);
					continue;
				}
				Vector3 tgt = vector + Vector3.up * Mathf.Clamp(magnitude * 0.35f, 0f, 6f);
				FlyBird(item4, f, tgt, dt, (magnitude < 3f) ? 11f : 6.5f, Mathf.Clamp(magnitude / 9f, 0.25f, 1f));
				if (magnitude < 0.35f && item4.pos.y - vector.y < 0.4f)
				{
					item4.pos = Snap(vector, vector.y);
					item4.vel = Vector3.zero;
					item4.walkTo = item4.pos;
					Fold(item4);
					num++;
				}
			}
			if (num == f.b.Count)
			{
				f.st = ((night || f.roof) ? St.Roost : St.Ground);
				f.t = 0f;
			}
			if (!(f.t > 25f))
			{
				break;
			}
			foreach (Bird item5 in f.b)
			{
				item5.pos = Snap(f.spot + item5.off, f.spot.y);
				item5.vel = Vector3.zero;
				Fold(item5);
			}
			break;
		}
		}
		foreach (Bird item6 in f.b)
		{
			Pose(item6, dt, item6.vel == Vector3.zero);
		}
	}

	private float cAm(Vector3 p)
	{
		Camera main = Camera.main;
		if (!main)
		{
			return 999f;
		}
		return Vector3.Distance(main.transform.position, p);
	}

	private void TakeOff(Flock f, Vector3 from)
	{
		f.st = St.TakeOff;
		f.t = 0f;
		f.fleeFrom = from;
		Vector3 vector = f.center - from;
		vector.y = 0f;
		if (vector.sqrMagnitude < 0.01f)
		{
			vector = UnityEngine.Random.insideUnitSphere;
		}
		vector.y = 0f;
		vector.Normalize();
		f.radius = UnityEngine.Random.Range(10f, 18f);
		f.alt = f.spot.y + UnityEngine.Random.Range(9f, 15f) + (float)(rain ? (-3) : 0);
		f.flyTime = (rain ? UnityEngine.Random.Range(4f, 7f) : UnityEngine.Random.Range(7f, 14f));
		f.ang = Mathf.Atan2(vector.z, vector.x);
		f.spot = f.center + vector * 6f;
		foreach (Bird item in f.b)
		{
			item.delay = UnityEngine.Random.Range(0f, 0.45f);
			Vector3 vector2 = vector + UnityEngine.Random.insideUnitSphere * 0.5f;
			vector2.y = 0f;
			item.vel = vector2.normalized * UnityEngine.Random.Range(2.5f, 3.5f) + Vector3.up * UnityEngine.Random.Range(3.5f, 4.8f);
			item.rate = UnityEngine.Random.Range(8.5f, 10f);
		}
		if ((bool)flapClip && cAm(f.center) < 70f)
		{
			FBAudio.PlayAt(flapClip, f.center, 0.9f, 6f, 0.1f);
		}
	}

	private void ChooseLanding(Flock f)
	{
		Vector3 spot = f.spot;
		float num = -1f;
		bool flag = false;
		for (int i = 0; i < 12; i++)
		{
			if (spots.Count <= 0)
			{
				break;
			}
			(Vector3, bool) tuple = spots[UnityEngine.Random.Range(0, spots.Count)];
			float num2 = Vector3.Distance(tuple.Item1, f.center);
			if (!(num2 > 70f) && !(num2 < 8f) && (!night || tuple.Item2))
			{
				float num3 = Vector3.Distance(tuple.Item1, f.fleeFrom) + (float)(tuple.Item2 ? 6 : 0) + UnityEngine.Random.value * 10f;
				if (Threat(tuple.Item1, 9f).HasValue)
				{
					num3 -= 50f;
				}
				if (num3 > num)
				{
					num = num3;
					(spot, flag) = tuple;
				}
			}
		}
		f.spot = spot;
		f.roof = flag;
		foreach (Bird item in f.b)
		{
			float f2 = UnityEngine.Random.value * 6.28f;
			float num4 = UnityEngine.Random.value * (flag ? 1.2f : 1.9f);
			item.off = new Vector3(Mathf.Cos(f2) * num4, 0f, Mathf.Sin(f2) * num4);
		}
	}

	private void GroundBird(Bird b, Flock f, float dt)
	{
		b.walkWait -= dt;
		b.peckT -= dt;
		Vector3 vector = b.walkTo - b.pos;
		vector.y = 0f;
		if (vector.magnitude > 0.04f)
		{
			Vector3 vector2 = vector.normalized * Mathf.Min(vector.magnitude, 0.45f * dt);
			b.pos += vector2;
			b.stepT += dt * 7f;
			b.yaw = Mathf.MoveTowardsAngle(b.yaw, Mathf.Atan2(vector.x, vector.z) * 57.29578f, 360f * dt);
			if (UnityEngine.Random.value < dt * 2f)
			{
				b.pos.y = Snap(b.pos, b.pos.y).y;
			}
		}
		else if (b.walkWait <= 0f)
		{
			float f2 = UnityEngine.Random.value * 6.28f;
			float num = UnityEngine.Random.Range(0.2f, 0.9f);
			Vector3 vector3 = f.spot + b.off + new Vector3(Mathf.Cos(f2) * num, 0f, Mathf.Sin(f2) * num);
			b.walkTo = new Vector3(vector3.x, b.pos.y, vector3.z);
			b.walkWait = UnityEngine.Random.Range(0.6f, 3.5f);
			if (f.st == St.Roost)
			{
				b.walkTo = b.pos;
			}
		}
		if (b.peckT <= -0.28f)
		{
			b.peckT = UnityEngine.Random.Range(0.8f, 4f);
		}
	}

	private void FlyBird(Bird b, Flock f, Vector3 tgt, float dt, float speed, float flapK = 1f)
	{
		if (b.delay > 0f)
		{
			b.delay -= dt;
			return;
		}
		Vector3 vector = (tgt + new Vector3(b.off.x * 1.6f, b.off.x * 0.6f, b.off.z * 1.6f) - b.pos).normalized * speed - b.vel;
		foreach (Bird item in f.b)
		{
			if (item != b)
			{
				Vector3 vector2 = b.pos - item.pos;
				float magnitude = vector2.magnitude;
				if (magnitude < 0.9f && magnitude > 0.001f)
				{
					vector += vector2 / magnitude * (1.2f - magnitude) * 6f;
				}
			}
		}
		b.vel += Vector3.ClampMagnitude(vector, 14f * dt);
		b.pos += b.vel * dt;
		Vector2 vector3 = new Vector2(b.vel.x, b.vel.z);
		if (vector3.sqrMagnitude > 0.01f)
		{
			b.yaw = Mathf.MoveTowardsAngle(b.yaw, Mathf.Atan2(vector3.x, vector3.y) * 57.29578f, 240f * dt);
		}
		b.glide = Mathf.MoveTowards(b.glide, (b.vel.y < -1.2f && flapK > 0.5f) ? 1 : 0, dt * 2f);
		b.amp = Mathf.Lerp(62f, 18f, b.glide) * Mathf.Lerp(1.15f, 1f, flapK);
	}

	private void Fold(Bird b)
	{
		b.amp = 0f;
		b.glide = 0f;
	}

	private void Pose(Bird b, float dt, bool onGround)
	{
		Transform root = b.root;
		root.position = b.pos;
		if (onGround)
		{
			float num = Mathf.Repeat(b.stepT, 1f);
			root.rotation = Quaternion.Euler(0f, b.yaw, 0f);
			float num2 = ((b.peckT < 0f) ? Mathf.Sin((0f - b.peckT) / 0.28f * (float)Math.PI) : 0f);
			b.head.localPosition = new Vector3(0f, 0.08f - num2 * 0.06f, 0.14f + ((num < 0.5f) ? (num * 0.05f) : ((1f - num) * 0.01f)) + num2 * 0.02f);
			b.head.localRotation = Quaternion.Euler(num2 * 65f, 0f, 0f);
			b.wR.localRotation = Quaternion.Euler(0f, 82f, -8f);
			b.wL.localRotation = Quaternion.Euler(0f, -82f, 8f);
			b.wR2.localRotation = Quaternion.Euler(0f, 172f, 0f);
			b.wL2.localRotation = Quaternion.Euler(0f, 172f, 0f);
		}
		else
		{
			float x = Mathf.Clamp((0f - b.vel.y) * 4f, -35f, 35f);
			float z = 0f;
			root.rotation = Quaternion.Euler(x, b.yaw, z);
			b.head.localPosition = new Vector3(0f, 0.07f, 0.15f);
			b.head.localRotation = Quaternion.identity;
			b.phase += dt * b.rate * 6.2832f;
			float num3 = Mathf.Sin(b.phase);
			float num4 = Mathf.Sin(b.phase - 0.9f);
			float num5 = b.amp * num3 + 6f;
			float z2 = b.amp * 0.55f * num4;
			b.wR.localRotation = Quaternion.Euler(0f, 0f, num5);
			b.wL.localRotation = Quaternion.Euler(0f, 0f, 0f - num5);
			b.wR2.localRotation = Quaternion.Euler(0f, 0f, z2);
			b.wL2.localRotation = Quaternion.Euler(0f, 0f, z2);
		}
	}

	private void TickVulture(Vulture v, float dt)
	{
		Bird b = v.b;
		v.c += new Vector3(v.drift.x, 0f, v.drift.y) * dt;
		v.a += dt * v.spd / v.r;
		float y = v.alt + Mathf.Sin(v.a * 0.37f) * 6f;
		Vector3 vector = v.c + new Vector3(Mathf.Cos(v.a) * v.r, 0f, Mathf.Sin(v.a) * v.r);
		vector.y = y;
		Vector3 vector2 = (vector - b.pos) / Mathf.Max(dt, 0.001f);
		if (b.pos == Vector3.zero)
		{
			vector2 = Vector3.zero;
		}
		b.pos = vector;
		float y2 = (b.yaw = Mathf.Atan2(0f - Mathf.Sin(v.a), Mathf.Cos(v.a)) * 57.29578f);
		b.root.position = vector;
		b.root.rotation = Quaternion.Euler(Mathf.Clamp((0f - vector2.y) * 2f, -8f, 8f), y2, 24f);
		v.flapT -= dt;
		float num = 0f;
		if (v.flapT < 0f)
		{
			num = 38f;
			if (v.flapT < -1.25f)
			{
				v.flapT = UnityEngine.Random.Range(12f, 30f);
			}
		}
		b.phase += dt * 2.4f * 6.2832f;
		float num2 = ((num > 0f) ? Mathf.Sin(b.phase) : 0f);
		float num3 = 9f;
		b.wR.localRotation = Quaternion.Euler(0f, 0f, num3 + num * num2);
		b.wL.localRotation = Quaternion.Euler(0f, 0f, 0f - num3 - num * num2);
		b.wR2.localRotation = Quaternion.Euler(0f, 0f, -4f + num * 0.5f * Mathf.Sin(b.phase - 0.9f));
		b.wL2.localRotation = b.wR2.localRotation;
	}

	private static AudioClip FlapClip()
	{
		int num = 20947;
		float[] array = new float[num];
		System.Random random = new System.Random(7);
		for (int i = 0; i < 9; i++)
		{
			float num2 = (float)random.NextDouble() * 0.35f;
			float num3 = 8f + (float)random.NextDouble() * 3f;
			float num4 = 0.55f + (float)random.NextDouble() * 0.45f;
			for (float num5 = num2; num5 < 0.9f; num5 += 1f / num3)
			{
				int num6 = (int)(num5 * 22050f);
				int num7 = 617;
				float num8 = 0f;
				float num9 = num4 * Mathf.Clamp01(1.2f - num5 * 1.1f);
				for (int j = 0; j < num7 && num6 + j < num; j++)
				{
					float num10 = Mathf.Sin((float)Math.PI * (float)j / (float)num7);
					float num11 = (float)(random.NextDouble() * 2.0 - 1.0);
					num8 += (num11 - num8) * 0.35f;
					array[num6 + j] += num8 * num10 * num9 * 0.5f;
				}
			}
		}
		float num12 = 0.0001f;
		float[] array2 = array;
		foreach (float f in array2)
		{
			num12 = Mathf.Max(num12, Mathf.Abs(f));
		}
		for (int l = 0; l < num; l++)
		{
			array[l] = array[l] / num12 * 0.8f;
		}
		AudioClip audioClip = AudioClip.Create("revoada", num, 1, 22050, stream: false);
		audioClip.SetData(array, 0);
		return audioClip;
	}
}
}
