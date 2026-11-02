using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public static class FBFx
{
	private class FL
	{
		public Light l;

		public float i;

		public float life;

		public float t;

		public bool on;
	}

	private class Tr
	{
		public LineRenderer lr;

		public Vector3 a;

		public Vector3 dir;

		public float len;

		public float streak;

		public float t;

		public float life;

		public bool on;
	}

	public static bool BloodOn = true;

	private static bool ready;

	private static GameObject root;

	private static ParticleSystem psFlash;

	private static ParticleSystem psCore;

	private static ParticleSystem psPuff;

	private static ParticleSystem psSpark;

	private static readonly List<FL> flash = new List<FL>();

	private static FL[] boom;

	private static readonly List<Tr> tracers = new List<Tr>();

	private static Vector3 lastHole;

	private static int lastHoleFrame = -1;

	private static Texture2D Tex(int n, Func<float, float, Color> f)
	{
		Texture2D texture2D = new Texture2D(n, n, TextureFormat.RGBA32, mipChain: true)
		{
			wrapMode = TextureWrapMode.Clamp
		};
		Color[] array = new Color[n * n];
		for (int i = 0; i < n; i++)
		{
			for (int j = 0; j < n; j++)
			{
				array[i * n + j] = f(((float)j + 0.5f) / (float)n * 2f - 1f, ((float)i + 0.5f) / (float)n * 2f - 1f);
			}
		}
		texture2D.SetPixels(array);
		texture2D.Apply(updateMipmaps: true, makeNoLongerReadable: true);
		return texture2D;
	}

	private static Color Grad(float d, params (float s, Color c)[] st)
	{
		if (d <= st[0].s)
		{
			return st[0].c;
		}
		for (int i = 1; i < st.Length; i++)
		{
			if (d <= st[i].s)
			{
				return Color.Lerp(st[i - 1].c, st[i].c, (d - st[i - 1].s) / (st[i].s - st[i - 1].s));
			}
		}
		return st[st.Length - 1].c;
	}

	private static Texture2D FlashTex()
	{
		return Tex(64, delegate(float x, float y)
		{
			float num = Mathf.Atan2(y, x);
			float num2 = Mathf.Sqrt(x * x + y * y);
			float num3 = Mathf.Lerp(0.375f, 31f / 32f, 0.5f + 0.5f * Mathf.Cos(num * 6f));
			Color result = Grad(num2 / 0.94f, (0f, new Color(1f, 1f, 0.9f, 1f)), (0.25f, new Color(1f, 0.78f, 0.31f, 0.9f)), (1f, new Color(1f, 0.47f, 0f, 0f)));
			if (num2 > num3)
			{
				result.a = 0f;
			}
			return result;
		});
	}

	private static Texture2D CoreTex()
	{
		return Tex(64, delegate(float x, float y)
		{
			float num = Mathf.Sqrt(x * x + y * y) / 0.875f;
			Color result = Grad(num, (0f, Color.white), (0.25f, new Color(1f, 0.96f, 0.78f, 0.95f)), (0.65f, new Color(1f, 0.67f, 0.2f, 0.45f)), (1f, new Color(1f, 0.4f, 0f, 0f)));
			if (num > 1f)
			{
				result.a = 0f;
			}
			return result;
		});
	}

	private static Texture2D PuffTex()
	{
		return Tex(64, delegate(float x, float y)
		{
			float value = Mathf.Sqrt(x * x + y * y) / 0.94f;
			return new Color(1f, 1f, 1f, Mathf.Lerp(0.9f, 0f, Mathf.Clamp01(value)));
		});
	}

	private static Material Mat(Texture2D t, bool additive, Color tint)
	{
		return FBFxLib.Mat(additive ? "Particles_Add" : "Particles_Alpha", t, tint, "Tiro");
	}

	private static ParticleSystem PS(string n, Material m, float gravity, int max = 600)
	{
		return FBFxLib.PS(n, m, max, new FBFxLib.Opt
		{
			gravity = gravity,
			color = FBFxLib.Fade()
		});
	}

	public static void Warm()
	{
		if (!ready)
		{
			ready = true;
			root = new GameObject("FB_FX");
			UnityEngine.Object.DontDestroyOnLoad(root);
			FBFxLib.Root = root.transform;
			FBFxQuality.Ensure();
			Texture2D t = FlashTex();
			Texture2D t2 = PuffTex();
			psFlash = PS("Clarao", Mat(t, additive: true, Color.white), 0f);
			psCore = PS("Nucleo", Mat(CoreTex(), additive: true, Color.white), 0f);
			psPuff = PS("Fumaca", Mat(t2, additive: false, Color.white), -0.06f);
			psSpark = PS("Faisca", Mat(t, additive: true, Color.white), 0f);
			ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = psPuff.sizeOverLifetime;
			sizeOverLifetime.enabled = true;
			sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 1.6f));
			for (int i = 0; i < 6; i++)
			{
				flash.Add(new FL
				{
					l = Light("LuzTiro" + i, 22f)
				});
			}
			boom = new FL[2]
			{
				new FL
				{
					l = Light("LuzExplosao0", 12f)
				},
				new FL
				{
					l = Light("LuzExplosao1", 12f)
				}
			};
			FL[] array = boom;
			for (int j = 0; j < array.Length; j++)
			{
				array[j].l.color = new Color(1f, 0.63f, 0.25f);
			}
			Material sharedMaterial = Mat(Tex(8, (float x, float y) => Color.white), additive: true, new Color(1f, 0.91f, 0.63f));
			for (int k = 0; k < 48; k++)
			{
				GameObject gameObject = new GameObject("Tracante" + k);
				gameObject.transform.SetParent(root.transform);
				LineRenderer lineRenderer = gameObject.AddComponent<LineRenderer>();
				lineRenderer.sharedMaterial = sharedMaterial;
				lineRenderer.positionCount = 2;
				lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
				lineRenderer.numCapVertices = 2;
				lineRenderer.enabled = false;
				tracers.Add(new Tr
				{
					lr = lineRenderer
				});
			}
			FBDecalFx.Warm();
			FBWoundFx.Warm();
			FBBloodFx.Warm();
			FBImpactFx.Warm();
			FBExplosionFx.Warm();
			FBShotFx.Warm();
			root.AddComponent<FBFxTicker>();
		}
	}

	private static Light Light(string n, float range)
	{
		GameObject gameObject = new GameObject(n);
		gameObject.transform.SetParent(root.transform);
		Light light = gameObject.AddComponent<Light>();
		light.type = LightType.Point;
		light.range = range;
		light.intensity = 0f;
		light.shadows = LightShadows.None;
		light.color = new Color(1f, 0.89f, 0.58f);
		return light;
	}

	private static void Emit(ParticleSystem ps, Vector3 p, Vector3 v, float size, float life, Color c, float rot = 0f)
	{
		FBFxLib.Emit(ps, p, v, size, life, c, rot);
	}

	public static void Muzzle(Vector3 p, float size, string cls, bool silenced = false)
	{
		Warm();
		float num = size * (silenced ? 0.45f : 1f);
		Emit(psFlash, p, Vector3.zero, num * UnityEngine.Random.Range(0.8f, 1.3f), silenced ? 0.035f : 0.05f, silenced ? new Color(0.8f, 0.6f, 0.4f) : Color.white, UnityEngine.Random.Range(0f, 360f));
		if (!silenced)
		{
			Emit(psCore, p, Vector3.zero, num * 0.62f * UnityEngine.Random.Range(0.9f, 1.2f), 0.04f, Color.white);
		}
		float num2 = 22f;
		float range = 22f;
		float life = 0.065f;
		if (silenced)
		{
			num2 = 4f;
			range = 7f;
			life = 0.045f;
		}
		else
		{
			switch (cls)
			{
			case "pistola":
				num2 = 13f;
				range = 15f;
				life = 0.055f;
				break;
			case "submetralhadora":
				num2 = 17f;
				range = 18f;
				life = 0.05f;
				break;
			case "escopeta":
				num2 = 30f;
				range = 26f;
				life = 0.08f;
				break;
			case "sniper":
				num2 = 38f;
				range = 32f;
				life = 0.09f;
				break;
			}
		}
		Color color = (silenced ? new Color(1f, 0.77f, 0.44f) : ((UnityEngine.Random.value < 0.4f) ? new Color(1f, 0.95f, 0.75f) : ((UnityEngine.Random.value < 0.6f) ? new Color(1f, 0.87f, 0.47f) : new Color(1f, 0.75f, 0.27f))));
		FL fL = flash.Find((FL f) => !f.on);
		if (fL == null)
		{
			fL = flash[0];
			foreach (FL item in flash)
			{
				if (item.t / item.life > fL.t / fL.life)
				{
					fL = item;
				}
			}
		}
		fL.l.transform.position = p;
		fL.l.color = color;
		fL.l.range = range;
		fL.i = num2 * UnityEngine.Random.Range(0.95f, 1.15f) * 0.12f;
		fL.l.intensity = fL.i;
		fL.life = life;
		fL.t = 0f;
		fL.on = true;
		Shooter(p, out var fwd, out var right, out var port);
		FBShotFx.Smoke(p, fwd, size, cls, silenced);
		FBShotFx.Shell(port, fwd, right, cls);
	}

	public static void Muzzle(Transform at, string cls)
	{
		if ((bool)at)
		{
			Muzzle(at.position, 0.55f, cls);
		}
	}

	private static void Shooter(Vector3 p, out Vector3 fwd, out Vector3 right, out Vector3 port)
	{
		FBPlayer i = FBPlayer.I;
		if ((bool)i && i.firstPerson && (bool)i.cam && (i.cam.transform.position - p).sqrMagnitude < 2.5f)
		{
			Transform transform = i.cam.transform;
			fwd = transform.forward;
			right = transform.right;
			port = transform.position + transform.forward * 0.32f + transform.right * 0.1f - transform.up * 0.1f;
			return;
		}
		FBCharacter fBCharacter = null;
		float num = 0.16f;
		foreach (FBCharacter item in FBMatch.All)
		{
			if ((bool)item)
			{
				float sqrMagnitude = (item.MuzzlePos - p).sqrMagnitude;
				if (sqrMagnitude < num)
				{
					num = sqrMagnitude;
					fBCharacter = item;
				}
			}
		}
		if ((bool)fBCharacter)
		{
			fwd = fBCharacter.transform.forward;
			right = fBCharacter.transform.right;
			port = p - fwd * 0.38f + Vector3.up * 0.03f;
		}
		else
		{
			fwd = Vector3.forward;
			right = Vector3.right;
			port = p;
		}
	}

	public static void Tracer(Vector3 a, Vector3 b, bool heavy = false)
	{
		Tracer(a, b, new Color(1f, 0.91f, 0.63f), heavy);
	}

	public static void Tracer(Vector3 a, Vector3 b, Color col, bool heavy = false)
	{
		Warm();
		float num = Vector3.Distance(a, b);
		if (!(num < 0.5f))
		{
			Tr tr = tracers.Find((Tr x) => !x.on) ?? tracers[0];
			tr.a = a;
			tr.dir = (b - a) / num;
			tr.len = num;
			tr.streak = Mathf.Min(num, heavy ? 6.5f : 4f);
			tr.life = Mathf.Clamp(num / (float)(heavy ? 280 : 360), 0.06f, 0.28f);
			tr.t = 0f;
			tr.on = true;
			tr.lr.enabled = true;
			tr.lr.startWidth = (heavy ? 0.038f : 0.024f);
			tr.lr.endWidth = 0.006f;
			LineRenderer lr = tr.lr;
			Color startColor = (tr.lr.endColor = col);
			lr.startColor = startColor;
		}
	}

	private static bool AfterHole(Vector3 p)
	{
		if (lastHoleFrame == Time.frameCount)
		{
			return (p - lastHole).sqrMagnitude < 0.04f;
		}
		return false;
	}

	public static void Puff(Vector3 p, Color c, float size = 0.35f, float life = 0.5f, float rise = 0.6f)
	{
		Warm();
		if (!AfterHole(p))
		{
			Emit(psPuff, p, new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), rise, UnityEngine.Random.Range(-0.3f, 0.3f)), size, life, new Color(c.r, c.g, c.b, 0.8f));
		}
	}

	public static void Sparks(Vector3 p, Vector3 n)
	{
		Warm();
		if (!AfterHole(p))
		{
			Emit(psSpark, p + n * 0.03f, Vector3.zero, 0.12f, 0.06f, Color.white);
		}
	}

	public static void Splash(Vector3 p, float k = 1f)
	{
		Warm();
		FBImpactFx.Water(p, k);
	}

	public static void BulletHole(Vector3 p, Vector3 n, Collider on = null)
	{
		Warm();
		FBImpactFx.Surface(p, n, on);
		lastHole = p;
		lastHoleFrame = Time.frameCount;
	}

	public static Transform BloodSplat(Vector3 p, Vector3 n, float size = 0.6f)
	{
		if (!BloodOn)
		{
			return null;
		}
		Warm();
		return FBDecalFx.Place(FBDecalFx.Atlas.Blood, UnityEngine.Random.Range(0, 6), p, n, size * UnityEngine.Random.Range(0.75f, 1.25f));
	}

	public static void BloodSpray(Vector3 p, Vector3 dir, float amount = 1f)
	{
		if (BloodOn)
		{
			Warm();
			FBBloodFx.Spray(p, dir, amount);
		}
	}

	public static void BloodPool(Vector3 p)
	{
		if (BloodOn)
		{
			Warm();
			FBBloodFx.Pool(p);
		}
	}

	public static void Explosion(Vector3 p, float r)
	{
		Warm();
		FBExplosionFx.Explode(p, r);
		FL obj = ((boom[0].t < boom[1].t) ? boom[0] : boom[1]);
		obj.l.transform.position = p;
		obj.l.range = r * 6f;
		obj.t = 0.3f;
		obj.on = true;
		obj.l.intensity = 6f;
	}

	public static void SmokeGrenade(Vector3 p, float seconds = 14f)
	{
		Warm();
		FBExplosionFx.SmokeGrenade(p, seconds);
	}

	public static void Impact(Vector3 p, Vector3 n)
	{
		Warm();
		FBImpactFx.Surface(p, n, null);
	}

	public static void Blood(Vector3 p, Vector3 n)
	{
		BloodSpray(p, -n);
	}

	public static void Tick(float dt)
	{
		if (!ready)
		{
			return;
		}
		foreach (FL item in flash)
		{
			if (item.on)
			{
				item.t += dt;
				float num = item.t / item.life;
				if (num >= 1f)
				{
					item.l.intensity = 0f;
					item.on = false;
				}
				else
				{
					item.l.intensity = item.i * (1f - num) * (1f - num);
				}
			}
		}
		FL[] array = boom;
		foreach (FL fL in array)
		{
			if (fL.on)
			{
				fL.t -= dt;
				fL.l.intensity = ((fL.t > 0f) ? (6f * fL.t / 0.3f) : 0f);
				if (fL.t <= 0f)
				{
					fL.on = false;
				}
			}
		}
		foreach (Tr tracer in tracers)
		{
			if (tracer.on)
			{
				tracer.t += dt;
				float num2 = tracer.t / tracer.life;
				if (num2 >= 1f)
				{
					tracer.on = false;
					tracer.lr.enabled = false;
					continue;
				}
				float num3 = tracer.len * Mathf.Min(1f, num2 * 1.5f);
				float num4 = Mathf.Max(0f, num3 - tracer.streak);
				float num5 = 1f - Mathf.Max(0f, (num2 - 0.5f) * 2f);
				tracer.lr.SetPosition(0, tracer.a + tracer.dir * num3);
				tracer.lr.SetPosition(1, tracer.a + tracer.dir * num4);
				Color startColor = tracer.lr.startColor;
				startColor.a = 0.95f * num5;
				tracer.lr.startColor = startColor;
				tracer.lr.endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);
			}
		}
	}

	public static void Clear()
	{
		FBDecalFx.Clear();
		FBWoundFx.Clear();
	}
}
}
