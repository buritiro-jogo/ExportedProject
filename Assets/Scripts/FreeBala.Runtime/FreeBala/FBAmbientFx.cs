using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public class FBAmbientFx : MonoBehaviour
{
	private static FBAmbientFx I;

	private readonly List<ParticleSystem> bugs = new List<ParticleSystem>();

	private ParticleSystem dust;

	private readonly List<ParticleSystem> smokes = new List<ParticleSystem>();

	private ParticleSystem farSmoke;

	private float reassignT;

	private static Texture2D dotTex;

	private static Texture2D puffTex;

	public static void Start(Transform parent, Bounds? map)
	{
		if ((bool)I)
		{
			UnityEngine.Object.Destroy(I.gameObject);
		}
		GameObject obj = new GameObject("FB_AmbienteFX");
		obj.transform.SetParent(parent, worldPositionStays: false);
		I = obj.AddComponent<FBAmbientFx>();
		I.Build(map);
	}

	private static Texture2D Dot()
	{
		if ((bool)dotTex)
		{
			return dotTex;
		}
		dotTex = new Texture2D(32, 32, TextureFormat.RGBA32, mipChain: false)
		{
			wrapMode = TextureWrapMode.Clamp
		};
		for (int i = 0; i < 32; i++)
		{
			for (int j = 0; j < 32; j++)
			{
				float num = new Vector2((float)(j - 16) + 0.5f, (float)(i - 16) + 0.5f).magnitude / 16f;
				float a = Mathf.Exp((0f - num) * num * 5f);
				dotTex.SetPixel(j, i, new Color(1f, 1f, 1f, a));
			}
		}
		dotTex.Apply();
		return dotTex;
	}

	private static Texture2D Puff()
	{
		if ((bool)puffTex)
		{
			return puffTex;
		}
		puffTex = new Texture2D(64, 64, TextureFormat.RGBA32, mipChain: true)
		{
			wrapMode = TextureWrapMode.Clamp
		};
		for (int i = 0; i < 64; i++)
		{
			for (int j = 0; j < 64; j++)
			{
				float num = new Vector2((float)(j - 32) + 0.5f, (float)(i - 32) + 0.5f).magnitude / 32f;
				float num2 = Mathf.PerlinNoise((float)j * 0.11f, (float)i * 0.11f) * 0.6f + Mathf.PerlinNoise((float)j * 0.27f + 7f, (float)i * 0.27f + 3f) * 0.4f;
				float num3 = Mathf.Clamp01(1f - num) * Mathf.Clamp01(0.35f + num2);
				num3 *= num3;
				float num4 = Mathf.Lerp(0.82f, 1f, num2);
				puffTex.SetPixel(j, i, new Color(num4, num4, num4, num3));
			}
		}
		puffTex.Apply(updateMipmaps: true);
		return puffTex;
	}

	private static Material Mat(Texture2D t, bool additive)
	{
		Material material = FBShaders.Transparent(new Material(FBShaders.ParticlesUnlit), additive);
		material.SetTexture("_BaseMap", t);
		material.mainTexture = t;
		return material;
	}

	private static ParticleSystem MakePS(string name, Transform parent, Material mat)
	{
		GameObject obj = new GameObject(name);
		obj.transform.SetParent(parent, worldPositionStays: false);
		ParticleSystem particleSystem = obj.AddComponent<ParticleSystem>();
		particleSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
		ParticleSystem.MainModule main = particleSystem.main;
		main.loop = true;
		main.playOnAwake = false;
		main.simulationSpace = ParticleSystemSimulationSpace.World;
		main.scalingMode = ParticleSystemScalingMode.Shape;
		ParticleSystemRenderer component = obj.GetComponent<ParticleSystemRenderer>();
		component.sharedMaterial = mat;
		component.shadowCastingMode = ShadowCastingMode.Off;
		component.receiveShadows = false;
		return particleSystem;
	}

	private void Build(Bounds? map)
	{
		FBGfxFlags f = FBGfx.F;
		Material mat = Mat(Dot(), additive: true);
		Material mat2 = Mat(Puff(), additive: false);
		for (int i = 0; i < f.lampInsects; i++)
		{
			ParticleSystem particleSystem = MakePS("insetos" + i, base.transform, mat);
			ParticleSystem.MainModule main = particleSystem.main;
			main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.6f);
			main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
			main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.055f);
			main.startColor = new Color(1f, 0.93f, 0.74f, 0.85f);
			main.maxParticles = 24;
			ParticleSystem.EmissionModule emission = particleSystem.emission;
			emission.rateOverTime = 8f;
			ParticleSystem.ShapeModule shape = particleSystem.shape;
			shape.shapeType = ParticleSystemShapeType.Sphere;
			shape.radius = 0.75f;
			ParticleSystem.NoiseModule noise = particleSystem.noise;
			noise.enabled = true;
			noise.strength = 0.9f;
			noise.frequency = 1.3f;
			noise.scrollSpeed = 0.6f;
			noise.quality = ParticleSystemNoiseQuality.Low;
			ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
			colorOverLifetime.enabled = true;
			colorOverLifetime.color = Fade(new Color(1f, 0.93f, 0.74f));
			particleSystem.gameObject.SetActive(value: false);
			bugs.Add(particleSystem);
		}
		if (f.dustMotes > 0)
		{
			dust = MakePS("poeira", base.transform, mat);
			ParticleSystem.MainModule main2 = dust.main;
			main2.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
			main2.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.06f);
			main2.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
			main2.startColor = new Color(1f, 0.95f, 0.82f, 0.32f);
			main2.maxParticles = f.dustMotes;
			ParticleSystem.EmissionModule emission2 = dust.emission;
			emission2.rateOverTime = (float)f.dustMotes / 6f;
			ParticleSystem.ShapeModule shape2 = dust.shape;
			shape2.shapeType = ParticleSystemShapeType.Box;
			shape2.scale = new Vector3(16f, 6f, 16f);
			ParticleSystem.NoiseModule noise2 = dust.noise;
			noise2.enabled = true;
			noise2.strength = 0.12f;
			noise2.frequency = 0.4f;
			noise2.quality = ParticleSystemNoiseQuality.Low;
			ParticleSystem.ColorOverLifetimeModule colorOverLifetime2 = dust.colorOverLifetime;
			colorOverLifetime2.enabled = true;
			colorOverLifetime2.color = Fade(new Color(1f, 0.95f, 0.82f));
		}
		if (f.smokePlumes <= 0)
		{
			return;
		}
		List<Vector3> list = new List<Vector3>();
		MeshRenderer[] array = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
		foreach (MeshRenderer meshRenderer in array)
		{
			if (meshRenderer.gameObject.layer != 8 || !Regex.IsMatch((meshRenderer.sharedMaterial ? meshRenderer.sharedMaterial.name : "") + " " + meshRenderer.name, "churrasq|grill|carvao", RegexOptions.IgnoreCase))
			{
				continue;
			}
			Bounds bounds = meshRenderer.bounds;
			if (!(bounds.size.magnitude > 6f))
			{
				Vector3 top = new Vector3(bounds.center.x, bounds.max.y + 0.1f, bounds.center.z);
				if (!list.Any((Vector3 p) => (p - top).sqrMagnitude < 16f))
				{
					list.Add(top);
				}
			}
		}
		Vector3 c0 = map?.center ?? Vector3.zero;
		foreach (Vector3 item in list.OrderBy((Vector3 p) => (p - c0).sqrMagnitude).Take(Mathf.Max(1, f.smokePlumes - 1)))
		{
			ParticleSystem particleSystem2 = MakePS("fumaca_churrasco", base.transform, mat2);
			particleSystem2.transform.position = item;
			ParticleSystem.MainModule main3 = particleSystem2.main;
			main3.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6.5f);
			main3.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1f);
			main3.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
			main3.startRotation = new ParticleSystem.MinMaxCurve(0f, (float)Math.PI * 2f);
			main3.startColor = new Color(0.78f, 0.77f, 0.75f, 0.32f);
			main3.maxParticles = 40;
			ParticleSystem.EmissionModule emission3 = particleSystem2.emission;
			emission3.rateOverTime = 5f;
			ParticleSystem.ShapeModule shape3 = particleSystem2.shape;
			shape3.shapeType = ParticleSystemShapeType.Cone;
			shape3.angle = 12f;
			shape3.radius = 0.2f;
			shape3.rotation = new Vector3(-90f, 0f, 0f);
			ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem2.sizeOverLifetime;
			sizeOverLifetime.enabled = true;
			sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 3.2f));
			ParticleSystem.RotationOverLifetimeModule rotationOverLifetime = particleSystem2.rotationOverLifetime;
			rotationOverLifetime.enabled = true;
			rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
			ParticleSystem.ColorOverLifetimeModule colorOverLifetime3 = particleSystem2.colorOverLifetime;
			colorOverLifetime3.enabled = true;
			colorOverLifetime3.color = Fade(new Color(0.78f, 0.77f, 0.75f));
			smokes.Add(particleSystem2);
		}
		if (f.smokePlumes >= 3 && map.HasValue)
		{
			Bounds valueOrDefault = map.GetValueOrDefault();
			float f2 = UnityEngine.Random.Range(0f, (float)Math.PI * 2f);
			Vector3 vector = new Vector3(Mathf.Cos(f2), 0f, Mathf.Sin(f2));
			farSmoke = MakePS("queimada_longe", base.transform, mat2);
			farSmoke.transform.position = new Vector3(valueOrDefault.center.x, valueOrDefault.min.y, valueOrDefault.center.z) + vector * (valueOrDefault.extents.magnitude + 260f);
			ParticleSystem.MainModule main4 = farSmoke.main;
			main4.startLifetime = new ParticleSystem.MinMaxCurve(38f, 52f);
			main4.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4f);
			main4.startSize = new ParticleSystem.MinMaxCurve(18f, 30f);
			main4.startRotation = new ParticleSystem.MinMaxCurve(0f, (float)Math.PI * 2f);
			main4.startColor = new Color(0.64f, 0.62f, 0.6f, 0.38f);
			main4.maxParticles = 60;
			ParticleSystem.EmissionModule emission4 = farSmoke.emission;
			emission4.rateOverTime = 0.9f;
			ParticleSystem.ShapeModule shape4 = farSmoke.shape;
			shape4.shapeType = ParticleSystemShapeType.Cone;
			shape4.angle = 8f;
			shape4.radius = 12f;
			shape4.rotation = new Vector3(-90f, 0f, 0f);
			ParticleSystem.SizeOverLifetimeModule sizeOverLifetime2 = farSmoke.sizeOverLifetime;
			sizeOverLifetime2.enabled = true;
			sizeOverLifetime2.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 3.5f));
			ParticleSystem.ForceOverLifetimeModule forceOverLifetime = farSmoke.forceOverLifetime;
			forceOverLifetime.enabled = true;
			forceOverLifetime.space = ParticleSystemSimulationSpace.World;
			forceOverLifetime.x = vector.z * 0.4f;
			forceOverLifetime.z = (0f - vector.x) * 0.4f;
			ParticleSystem.ColorOverLifetimeModule colorOverLifetime4 = farSmoke.colorOverLifetime;
			colorOverLifetime4.enabled = true;
			colorOverLifetime4.color = Fade(new Color(0.64f, 0.62f, 0.6f));
			farSmoke.Simulate(40f, withChildren: true, restart: true);
			farSmoke.Play();
		}
	}

	private static ParticleSystem.MinMaxGradient Fade(Color c)
	{
		Gradient gradient = new Gradient();
		gradient.SetKeys(new GradientColorKey[2]
		{
			new GradientColorKey(c, 0f),
			new GradientColorKey(c, 1f)
		}, new GradientAlphaKey[4]
		{
			new GradientAlphaKey(0f, 0f),
			new GradientAlphaKey(1f, 0.2f),
			new GradientAlphaKey(0.8f, 0.7f),
			new GradientAlphaKey(0f, 1f)
		});
		return new ParticleSystem.MinMaxGradient(gradient);
	}

	private static void Run(ParticleSystem ps, bool on)
	{
		if ((bool)ps)
		{
			if (on && !ps.isPlaying)
			{
				ps.Play();
			}
			else if (!on && ps.isPlaying)
			{
				ps.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);
			}
		}
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBAmbientFx.Update");

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
		Camera main = Camera.main;
		if (!main)
		{
			return;
		}
		Vector3 cp = main.transform.position;
		float nightK = FBWeather.NightK;
		bool rain = FBWeather.Rain;
		if ((bool)dust)
		{
			dust.transform.position = cp + Vector3.up * 1.2f;
			Run(dust, nightK < 0.3f && !rain);
		}
		foreach (ParticleSystem smoke in smokes)
		{
			Run(smoke, !rain);
		}
		if ((bool)farSmoke)
		{
			Run(farSmoke, !rain && nightK < 0.55f);
		}
		if ((reassignT -= Time.deltaTime) > 0f)
		{
			return;
		}
		reassignT = 1f;
		IReadOnlyList<Vector3> lamps = FBWeather.Lamps;
		bool flag = nightK > 0.5f && !rain && lamps != null && lamps.Count > 0;
		List<Vector3> list = (flag ? lamps.OrderBy((Vector3 p) => (p - cp).sqrMagnitude).Take(bugs.Count).ToList() : null);
		for (int i = 0; i < bugs.Count; i++)
		{
			bool flag2 = flag && i < list.Count && (list[i] - cp).sqrMagnitude < 3600f;
			if (flag2 && (bugs[i].transform.position - (list[i] + Vector3.down * 0.6f)).sqrMagnitude > 0.01f)
			{
				bugs[i].Clear();
				bugs[i].transform.position = list[i] + Vector3.down * 0.6f;
			}
			if (bugs[i].gameObject.activeSelf != flag2)
			{
				bugs[i].gameObject.SetActive(flag2);
			}
			Run(bugs[i], flag2);
		}
	}
}
}
