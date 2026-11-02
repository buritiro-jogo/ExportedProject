using System;
using UnityEngine;

namespace FreeBala
{
public static class FBExplosionFx
{
	private class Curtain
	{
		public Vector3 p;

		public float left;

		public float acc;

		public float radius;

		public Color col;
	}

	private static ParticleSystem psFire;

	private static ParticleSystem psSmoke;

	private static ParticleSystem psWave;

	private static ParticleSystem psEmber;

	private static ParticleSystem psFlash;

	private static ParticleSystem psCurtain;

	private static bool ready;

	private static readonly Curtain[] curtains = new Curtain[3]
	{
		new Curtain(),
		new Curtain(),
		new Curtain()
	};

	public static void Warm()
	{
		if (!ready)
		{
			ready = true;
			FBFxQuality.Ensure();
			bool flag = FBFxQuality.PoolDecals > 100;
			Material m = FBFxLib.Mat("Particles_Add", FBFxLib.Tex("fire"), Color.white, "ExplosaoFogo");
			Gradient gradient = new Gradient();
			gradient.SetKeys(new GradientColorKey[3]
			{
				new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0f),
				new GradientColorKey(new Color(1f, 0.55f, 0.18f), 0.35f),
				new GradientColorKey(new Color(0.5f, 0.12f, 0.03f), 1f)
			}, new GradientAlphaKey[3]
			{
				new GradientAlphaKey(1f, 0f),
				new GradientAlphaKey(0.8f, 0.4f),
				new GradientAlphaKey(0f, 1f)
			});
			psFire = FBFxLib.PS("ExplosaoFogo", m, flag ? 90 : 40, new FBFxLib.Opt
			{
				gravity = -0.15f,
				drag = 0.5f,
				tilesX = 2,
				tilesY = 2,
				spin = true,
				size = FBFxLib.Curve(0f, 0.5f, 0.25f, 1f, 1f, 1.35f),
				color = gradient
			});
			Material m2 = FBFxLib.Mat("Particles_Alpha", FBFxLib.Tex("smoke"), Color.white, "ExplosaoFumaca");
			psSmoke = FBFxLib.PS("ExplosaoFumaca", m2, flag ? 160 : 60, new FBFxLib.Opt
			{
				gravity = -0.05f,
				drag = 0.4f,
				tilesX = 2,
				tilesY = 2,
				spin = true,
				size = FBFxLib.Curve(0f, 0.5f, 0.4f, 1.4f, 1f, 2.4f),
				color = FBFxLib.Fade(0.05f, 0.55f),
				sortFudge = 10
			});
			psWave = FBFxLib.PS("ExplosaoPoeira", m2, flag ? 90 : 36, new FBFxLib.Opt
			{
				drag = 0.9f,
				tilesX = 2,
				tilesY = 2,
				spin = true,
				size = FBFxLib.Curve(0f, 0.6f, 1f, 1.8f),
				color = FBFxLib.Fade(0.04f, 0.4f),
				sortFudge = 5
			});
			psEmber = FBFxLib.PS("ExplosaoBrasas", FBFxLib.Mat("Particles_Add", FBFxLib.Tex("spark"), Color.white, "ExplosaoBrasa"), flag ? 120 : 40, new FBFxLib.Opt
			{
				gravity = 0.35f,
				mode = ParticleSystemRenderMode.Stretch,
				stretch = 0.02f,
				color = FBFxLib.Fade(0f, 0.6f, new Color(1f, 0.3f, 0.1f))
			});
			psFlash = FBFxLib.PS("ExplosaoClarao", m, 8, new FBFxLib.Opt
			{
				tilesX = 2,
				tilesY = 2,
				color = FBFxLib.Fade(0f, 0.15f)
			});
			psCurtain = FBFxLib.PS("FumacaGranada", m2, flag ? 220 : 80, new FBFxLib.Opt
			{
				gravity = -0.01f,
				drag = 0.6f,
				tilesX = 2,
				tilesY = 2,
				spin = true,
				size = FBFxLib.Curve(0f, 0.35f, 0.3f, 1f, 1f, 1.5f),
				color = FBFxLib.Fade(0.12f, 0.7f),
				sortFudge = 20
			});
		}
	}

	public static void Explode(Vector3 p, float r)
	{
		Warm();
		float lod = Mathf.Max(FBFxQuality.Lod(p), 0.35f);
		FBFxLib.Emit(psFlash, p, Vector3.zero, r * 1.6f, 0.18f, Color.white, UnityEngine.Random.Range(0f, 360f));
		int num = FBFxQuality.Count(14f, lod);
		for (int i = 0; i < num; i++)
		{
			FBFxLib.Emit(psFire, p + UnityEngine.Random.insideUnitSphere * r * 0.2f, UnityEngine.Random.insideUnitSphere * r * 1.6f + Vector3.up * r * 0.6f, r * UnityEngine.Random.Range(0.35f, 0.65f), UnityEngine.Random.Range(0.3f, 0.65f), Color.white, UnityEngine.Random.Range(0f, 360f));
		}
		num = FBFxQuality.Count(14f, lod);
		for (int j = 0; j < num; j++)
		{
			FBFxLib.Emit(psSmoke, p + UnityEngine.Random.insideUnitSphere * r * 0.3f, Vector3.up * UnityEngine.Random.Range(1f, 3.2f) + UnityEngine.Random.insideUnitSphere * 1.6f, r * UnityEngine.Random.Range(0.45f, 0.9f), UnityEngine.Random.Range(2.6f, 5f), FBFxLib.Jit(new Color(0.17f, 0.16f, 0.15f, 0.78f), 0.2f), UnityEngine.Random.Range(0f, 360f));
		}
		float num2 = FBWorld.GroundY(p.x, p.y, p.z) ?? (p.y - 0.5f);
		num = FBFxQuality.Count(22f, lod);
		for (int k = 0; k < num; k++)
		{
			float f = (float)k * (float)Math.PI * 2f / (float)Mathf.Max(1, num) + UnityEngine.Random.Range(-0.15f, 0.15f);
			Vector3 vector = new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f));
			FBFxLib.Emit(psWave, new Vector3(p.x, num2 + 0.25f, p.z) + vector * 0.3f, vector * UnityEngine.Random.Range(7, 12) * Mathf.Sqrt(r / 4f) + Vector3.up * 0.3f, r * UnityEngine.Random.Range(0.3f, 0.5f), UnityEngine.Random.Range(1.1f, 2f), new Color(0.55f, 0.5f, 0.42f, 0.55f), UnityEngine.Random.Range(0f, 360f));
		}
		num = FBFxQuality.Count(18f, lod);
		for (int l = 0; l < num; l++)
		{
			FBFxLib.Emit(psEmber, p, (UnityEngine.Random.onUnitSphere + Vector3.up * 0.8f) * UnityEngine.Random.Range(4, 11), UnityEngine.Random.Range(0.015f, 0.03f), UnityEngine.Random.Range(0.6f, 1.4f), new Color(1f, UnityEngine.Random.Range(0.5f, 0.8f), 0.25f));
		}
		FBImpactFx.Warm();
		FBImpactFx.Surface(new Vector3(p.x, num2, p.z), Vector3.up, null);
		if (Mathf.Abs(p.y - num2) < r)
		{
			FBDecalFx.Place(FBDecalFx.Atlas.Holes, 5, new Vector3(p.x, num2, p.z), Vector3.up, r * UnityEngine.Random.Range(0.8f, 1.05f), null, 1f, null, FBFxQuality.DecalLife * 1.5f, checkEdges: false);
		}
	}

	public static void SmokeGrenade(Vector3 p, float seconds = 14f, float radius = 3.5f, Color? color = null)
	{
		Warm();
		Curtain curtain = curtains[0];
		Curtain[] array = curtains;
		foreach (Curtain curtain2 in array)
		{
			if (curtain2.left < curtain.left)
			{
				curtain = curtain2;
			}
		}
		curtain.p = p;
		curtain.left = seconds;
		curtain.acc = 0f;
		curtain.radius = radius;
		curtain.col = color ?? new Color(0.78f, 0.78f, 0.76f, 0.85f);
	}

	public static void Tick(float dt)
	{
		if (!ready)
		{
			return;
		}
		Curtain[] array = curtains;
		foreach (Curtain curtain in array)
		{
			if (curtain.left <= 0f)
			{
				continue;
			}
			curtain.left -= dt;
			curtain.acc += dt * (float)((FBFxQuality.Level == 0) ? 3 : 6);
			while (curtain.acc >= 1f)
			{
				curtain.acc -= 1f;
				if (FBFxQuality.Take(1) == 0)
				{
					break;
				}
				Vector2 vector = UnityEngine.Random.insideUnitCircle * curtain.radius * 0.5f;
				FBFxLib.Emit(psCurtain, curtain.p + new Vector3(vector.x, 0.4f, vector.y), new Vector3(vector.x, 0f, vector.y) * 0.35f + Vector3.up * UnityEngine.Random.Range(0.15f, 0.5f), curtain.radius * UnityEngine.Random.Range(0.7f, 1.1f), UnityEngine.Random.Range(6, 9), FBFxLib.Jit(curtain.col, 0.06f), UnityEngine.Random.Range(0f, 360f));
			}
		}
	}
}
}
