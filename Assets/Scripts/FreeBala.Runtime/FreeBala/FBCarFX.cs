using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public static class FBCarFX
{
	private class Track
	{
		public bool has;

		public Vector3 last;

		public Vector3 e0;

		public Vector3 e1;

		public float v;

		public float emitAcc;

		public float gy;

		public int surf;

		public int chk;
	}

	private const int MAX_SEG = 1600;

	private const int MAX_P = 360;

	private const float SEG_MIN = 0.22f;

	private const float MARK_W = 0.26f;

	private const float TEX_LEN = 1.6f;

	private static readonly Dictionary<int, Color> SURF_COL = new Dictionary<int, Color>
	{
		[0] = new Color(0.012f, 0.012f, 0.012f, 0.97f),
		[4] = new Color(0.02f, 0.02f, 0.02f, 0.75f),
		[1] = new Color(0.24f, 0.17f, 0.1f, 0.5f),
		[2] = new Color(0.33f, 0.27f, 0.17f, 0.42f),
		[3] = new Color(0.2f, 0.14f, 0.08f, 0.55f)
	};

	private static readonly Dictionary<int, Color> SMOKE_COL = new Dictionary<int, Color>
	{
		[0] = new Color(0.92f, 0.92f, 0.92f),
		[4] = new Color(0.92f, 0.92f, 0.92f),
		[1] = new Color(0.62f, 0.5f, 0.36f),
		[2] = new Color(0.78f, 0.7f, 0.52f),
		[3] = new Color(0.5f, 0.4f, 0.3f)
	};

	private static GameObject root;

	private static Mesh mesh;

	private static Vector3[] P;

	private static Vector2[] U;

	private static Color[] C;

	private static int segN;

	private static float fadeT;

	private static bool dirty;

	private static ParticleSystem smoke;

	private static readonly Dictionary<object, Track> tracks = new Dictionary<object, Track>();

	private static Texture2D smokeOld;

	private static Texture2D smokeLight;

	private static Material smokeMat;

	public static int LastSurf = -1;

	public static void SmokeTex(bool light)
	{
		Texture2D texture2D = (light ? smokeLight : smokeOld);
		if ((bool)smokeMat && (bool)texture2D)
		{
			smokeMat.SetTexture("_BaseMap", texture2D);
			smokeMat.mainTexture = texture2D;
		}
	}

	private static void Init()
	{
		if (!root)
		{
			root = new GameObject("FB_CarFX");
			Object.DontDestroyOnLoad(root);
			mesh = new Mesh
			{
				name = "skidmarks"
			};
			mesh.MarkDynamic();
			P = new Vector3[6400];
			U = new Vector2[6400];
			C = new Color[6400];
			int[] array = new int[9600];
			for (int i = 0; i < 1600; i++)
			{
				int num = (array[i * 6] = i * 4);
				array[i * 6 + 1] = num + 2;
				array[i * 6 + 2] = num + 1;
				array[i * 6 + 3] = num + 2;
				array[i * 6 + 4] = num + 3;
				array[i * 6 + 5] = num + 1;
			}
			mesh.indexFormat = IndexFormat.UInt32;
			mesh.vertices = P;
			mesh.uv = U;
			mesh.colors = C;
			mesh.triangles = array;
			mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 200000f);
			GameObject gameObject = new GameObject("marcas");
			gameObject.transform.SetParent(root.transform, worldPositionStays: false);
			gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
			MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
			Texture2D texture2D = Resources.Load<Texture2D>("CarFX/skidmarks");
			if ((bool)texture2D)
			{
				texture2D.wrapMode = TextureWrapMode.Repeat;
			}
			Material material = FBShaders.Transparent(new Material(FBShaders.ParticlesUnlit));
			if ((bool)texture2D)
			{
				material.SetTexture("_BaseMap", texture2D);
				material.mainTexture = texture2D;
			}
			material.SetFloat("_Cull", 0f);
			material.renderQueue = 2990;
			meshRenderer.sharedMaterial = material;
			meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
			meshRenderer.receiveShadows = false;
			GameObject gameObject2 = new GameObject("fumaca");
			gameObject2.transform.SetParent(root.transform, worldPositionStays: false);
			smoke = gameObject2.AddComponent<ParticleSystem>();
			smoke.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
			ParticleSystem.MainModule main = smoke.main;
			main.maxParticles = 360;
			main.simulationSpace = ParticleSystemSimulationSpace.World;
			main.playOnAwake = false;
			main.loop = false;
			main.startSpeed = 0f;
			main.gravityModifier = -0.025f;
			ParticleSystem.EmissionModule emission = smoke.emission;
			emission.enabled = false;
			ParticleSystem.ShapeModule shape = smoke.shape;
			shape.enabled = false;
			ParticleSystem.LimitVelocityOverLifetimeModule limitVelocityOverLifetime = smoke.limitVelocityOverLifetime;
			limitVelocityOverLifetime.enabled = true;
			limitVelocityOverLifetime.drag = 1.6f;
			limitVelocityOverLifetime.multiplyDragByParticleSize = false;
			limitVelocityOverLifetime.multiplyDragByParticleVelocity = false;
			ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = smoke.sizeOverLifetime;
			sizeOverLifetime.enabled = true;
			sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.18f), new Keyframe(0.25f, 0.6f), new Keyframe(1f, 1f)));
			ParticleSystem.ColorOverLifetimeModule colorOverLifetime = smoke.colorOverLifetime;
			colorOverLifetime.enabled = true;
			Gradient gradient = new Gradient();
			gradient.SetKeys(new GradientColorKey[2]
			{
				new GradientColorKey(Color.white, 0f),
				new GradientColorKey(Color.white, 1f)
			}, new GradientAlphaKey[4]
			{
				new GradientAlphaKey(0f, 0f),
				new GradientAlphaKey(1f, 0.1f),
				new GradientAlphaKey(0.5f, 0.5f),
				new GradientAlphaKey(0f, 1f)
			});
			colorOverLifetime.color = gradient;
			ParticleSystem.TextureSheetAnimationModule textureSheetAnimation = smoke.textureSheetAnimation;
			textureSheetAnimation.enabled = true;
			textureSheetAnimation.numTilesX = 6;
			textureSheetAnimation.numTilesY = 5;
			textureSheetAnimation.animation = ParticleSystemAnimationType.WholeSheet;
			textureSheetAnimation.startFrame = new ParticleSystem.MinMaxCurve(0f, 29f);
			textureSheetAnimation.cycleCount = 2;
			ParticleSystemRenderer component = gameObject2.GetComponent<ParticleSystemRenderer>();
			smokeOld = Resources.Load<Texture2D>("CarFX/smoke");
			smokeLight = Resources.Load<Texture2D>("CarFX/smoke_light");
			Texture2D texture2D2 = (smokeLight ? smokeLight : smokeOld);
			Material material2 = FBShaders.Transparent(new Material(FBShaders.ParticlesUnlit));
			if ((bool)texture2D2)
			{
				material2.SetTexture("_BaseMap", texture2D2);
				material2.mainTexture = texture2D2;
			}
			smokeMat = material2;
			component.sharedMaterial = material2;
			component.shadowCastingMode = ShadowCastingMode.Off;
			component.sortingFudge = -2f;
			smoke.Play();
		}
	}

	public static void Reset()
	{
		Init();
		tracks.Clear();
		smoke.Clear();
		for (int i = 0; i < C.Length; i++)
		{
			C[i] = default(Color);
		}
		segN = 0;
		dirty = true;
	}

	private static void WriteSeg(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1, float v0, float v1, Color c)
	{
		int num = segN % 1600 * 4;
		P[num] = a0;
		P[num + 1] = a1;
		P[num + 2] = b0;
		P[num + 3] = b1;
		U[num] = new Vector2(0f, v0);
		U[num + 1] = new Vector2(1f, v0);
		U[num + 2] = new Vector2(0f, v1);
		U[num + 3] = new Vector2(1f, v1);
		for (int i = 0; i < 4; i++)
		{
			C[num + i] = c;
		}
		segN++;
		dirty = true;
	}

	private static void Emit(Vector3 p, Vector3 vel, Color col, float a, float s0, float s1, float life)
	{
		ParticleSystem.EmitParams emitParams = default(ParticleSystem.EmitParams);
		emitParams.position = p;
		emitParams.velocity = vel;
		emitParams.startLifetime = life;
		emitParams.startSize = s1 * 1f;
		emitParams.startColor = new Color(Mathf.Lerp(col.r, 1f, 0.6f) * col.r, Mathf.Lerp(col.g, 1f, 0.6f) * col.g, Mathf.Lerp(col.b, 1f, 0.6f) * col.b, a);
		emitParams.applyShapeToPosition = false;
		ParticleSystem.EmitParams emitParams2 = emitParams;
		smoke.Emit(emitParams2, 1);
	}

	public static void Wheel(object key, Vector3 center, float r, float slip, Vector3 vel, Vector3 side, float dt, bool burn = false, bool noSmoke = false)
	{
		Init();
		if (!tracks.TryGetValue(key, out var value))
		{
			value = (tracks[key] = new Track());
		}
		if (slip < 0.06f)
		{
			value.has = false;
			return;
		}
		if (!value.has || (value.chk = (value.chk + 1) % 2) == 0)
		{
			if (!FBWorld.RayHit(center, Vector3.down, r + 0.7f, out var hit))
			{
				value.has = false;
				return;
			}
			value.surf = FBWorld.SurfaceAt(hit);
			value.gy = hit.point.y;
		}
		if (!SURF_COL.ContainsKey(value.surf))
		{
			value.has = false;
			return;
		}
		Vector3 vector = new Vector3(center.x, value.gy + 0.025f, center.z);
		float num = 0.13f;
		if (!value.has)
		{
			value.has = true;
			value.last = vector;
			value.e0 = vector + side * num;
			value.e1 = vector - side * num;
			value.v = 0f;
		}
		else
		{
			float num2 = Vector3.Distance(vector, value.last);
			if (num2 >= 0.22f)
			{
				Vector3 normalized = (vector - value.last).normalized;
				Vector3 vector2 = Vector3.Cross(Vector3.up, normalized).normalized;
				if (Vector3.Dot(vector2, side) < 0f)
				{
					vector2 = -vector2;
				}
				Vector3 vector3 = vector + vector2 * num;
				Vector3 vector4 = vector - vector2 * num;
				Color color = SURF_COL[value.surf];
				float num3 = value.v + num2 / 1.6f;
				WriteSeg(value.e0, value.e1, vector3, vector4, value.v, num3, new Color(color.r, color.g, color.b, color.a * Mathf.Min(1f, 0.35f + slip)));
				value.e0 = vector3;
				value.e1 = vector4;
				value.v = num3 % 64f;
				value.last = vector;
			}
		}
		if (!noSmoke)
		{
			LastSurf = value.surf;
			bool flag = value.surf == 0 || value.surf == 4;
			float num4 = (float)(burn ? 34 : (flag ? 16 : 14)) * slip;
			value.emitAcc += num4 * dt;
			while (value.emitAcc >= 1f)
			{
				value.emitAcc -= 1f;
				Vector3 vel2 = new Vector3((Random.value - 0.5f) * 1.6f, 0.5f + Random.value * 0.9f, (Random.value - 0.5f) * 1.6f) + vel * 0.35f;
				Emit(vector + new Vector3((Random.value - 0.5f) * 0.3f, 0.12f + Random.value * 0.15f, (Random.value - 0.5f) * 0.3f), vel2, SMOKE_COL.TryGetValue(value.surf, out var value2) ? value2 : SMOKE_COL[0], (flag ? 0.55f : 0.5f) * Mathf.Min(1f, 0.4f + slip), 0.55f + Random.value * 0.3f, (burn ? 3.2f : 2.3f) + Random.value * 0.8f, (burn ? 2.4f : 1.6f) + Random.value * 0.8f);
			}
		}
	}

	public static void Puff(Vector3 p, Vector3 vel, Color col, float a, float s0, float s1, float life)
	{
		Init();
		Emit(p, vel, col, a, s0, s1, life);
	}

	public static void Update(float dt)
	{
		if (!root)
		{
			return;
		}
		if ((fadeT += dt) > 0.5f && (float)segN > 1200f)
		{
			fadeT = 0f;
			int num = segN - 1600;
			float num2 = 400f;
			for (int i = Mathf.Max(0, num); (float)i < (float)num + num2; i++)
			{
				int num3 = i % 1600 * 4;
				float num4 = Mathf.Max(0f, (float)(i - num) / num2);
				for (int j = 0; j < 4; j++)
				{
					C[num3 + j].a = Mathf.Min(C[num3 + j].a, 0.97f * num4);
				}
			}
			dirty = true;
		}
		if (dirty)
		{
			dirty = false;
			mesh.vertices = P;
			mesh.uv = U;
			mesh.colors = C;
		}
	}
}
}
