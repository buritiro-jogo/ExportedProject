using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public static class FBVolumetric
{
	public const int MaxLights = 32;

	public static bool Active;

	public static bool InScene;

	public static float ResolutionScale = 0.5f;

	public static int Steps = 24;

	public static readonly List<Light> Sources = new List<Light>();

	public static float Density = 0.002f;

	public static float Falloff = 0.045f;

	public static float GroundY;

	public static float MaxDist = 150f;

	public static float G = 0.6f;

	public static float LampK = 3f;

	public static Color MainColor;

	public static Color Ambient;

	public static Vector3 MainDir = Vector3.up;

	private static readonly Vector4[] P = new Vector4[32];

	private static readonly Vector4[] D = new Vector4[32];

	private static readonly Vector4[] C = new Vector4[32];

	private static readonly List<(float d, Light l)> tmp = new List<(float, Light)>();

	private static readonly int idParams = Shader.PropertyToID("_FBVolParams");

	private static readonly int idMain = Shader.PropertyToID("_FBVolMain");

	private static readonly int idMainDir = Shader.PropertyToID("_FBVolMainDir");

	private static readonly int idAmb = Shader.PropertyToID("_FBVolAmbient");

	private static readonly int idSteps = Shader.PropertyToID("_FBVolSteps");

	private static readonly int idCount = Shader.PropertyToID("_FBVolLightCount");

	private static readonly int idPos = Shader.PropertyToID("_FBVolLightPos");

	private static readonly int idDir = Shader.PropertyToID("_FBVolLightDir");

	private static readonly int idCol = Shader.PropertyToID("_FBVolLightCol");

	public static float MainK = 1f;

	private const float CQDensity = 0.6f;

	private static VolumetricFogVolumeComponent cq;

	private static readonly HashSet<Light> cqLights = new HashSet<Light>();

	public static void Clear()
	{
		Sources.Clear();
		InScene = false;
	}

	public static Color LightColor(Light l)
	{
		Color result = l.color.linear * l.intensity;
		if (l.useColorTemperature)
		{
			result *= Mathf.CorrelatedColorTemperatureToRGB(l.colorTemperature);
		}
		return result;
	}

	public static void Tick(Camera cam)
	{
		if (!Active || !InScene || !cam)
		{
			return;
		}
		Vector3 position = cam.transform.position;
		tmp.Clear();
		for (int num = Sources.Count - 1; num >= 0; num--)
		{
			Light light = Sources[num];
			if (!light)
			{
				Sources.RemoveAt(num);
			}
			else if (light.isActiveAndEnabled && !(light.intensity < 0.01f) && light.type != LightType.Directional)
			{
				float sqrMagnitude = (light.transform.position - position).sqrMagnitude;
				if (!(sqrMagnitude > (MaxDist + light.range) * (MaxDist + light.range)))
				{
					tmp.Add((sqrMagnitude, light));
				}
			}
		}
		tmp.Sort(((float d, Light l) a, (float d, Light l) b) => a.d.CompareTo(b.d));
		int num2 = Mathf.Min(32, tmp.Count);
		for (int i = 0; i < num2; i++)
		{
			Light item = tmp[i].l;
			Transform transform = item.transform;
			float w = Mathf.Min(item.range, 26f);
			P[i] = new Vector4(transform.position.x, transform.position.y, transform.position.z, w);
			bool num3 = item.type == LightType.Spot;
			float w2 = (num3 ? Mathf.Cos(item.spotAngle * 0.5f * ((float)Math.PI / 180f)) : (-2f));
			float w3 = (num3 ? Mathf.Cos(item.innerSpotAngle * 0.5f * ((float)Math.PI / 180f)) : 1f);
			D[i] = new Vector4(transform.forward.x, transform.forward.y, transform.forward.z, w2);
			Color color = LightColor(item) * LampK;
			C[i] = new Vector4(color.r, color.g, color.b, w3);
		}
		Shader.SetGlobalVector(idParams, new Vector4(Density, Falloff, GroundY, MaxDist));
		Shader.SetGlobalVector(idMain, new Vector4(MainColor.r, MainColor.g, MainColor.b, G));
		Shader.SetGlobalVector(idMainDir, MainDir);
		Shader.SetGlobalVector(idAmb, Ambient);
		Shader.SetGlobalVector(idSteps, new Vector4(Steps, Time.frameCount % 64, 0f, 0f));
		Shader.SetGlobalFloat(idCount, num2);
		Shader.SetGlobalVectorArray(idPos, P);
		Shader.SetGlobalVectorArray(idDir, D);
		Shader.SetGlobalVectorArray(idCol, C);
		DriveCQ(num2);
	}

	public static void AttachProfile(VolumeProfile p)
	{
		cqLights.Clear();
		cq = null;
		if ((bool)p)
		{
			if (!p.TryGet<VolumetricFogVolumeComponent>(out cq))
			{
				cq = p.Add<VolumetricFogVolumeComponent>(overrides: true);
			}
			cq.enabled.Override(x: false);
		}
	}

	private static void DriveCQ(int nLights)
	{
		if (!cq)
		{
			return;
		}
		bool flag = Active && InScene;
		cq.enabled.Override(flag);
		if (!flag)
		{
			return;
		}
		bool flag2 = FBGfx.Tier == FBGfxTier.PCAlto;
		cq.density.Override(0.6f);
		cq.attenuationDistance.Override(Mathf.Max(0.05f, 0.6f / Mathf.Max(0.0001f, Density) * 1.4f));
		cq.distance.Override(Mathf.Min(MaxDist, flag2 ? 90 : 70));
		cq.baseHeight.Override(GroundY);
		cq.maximumHeight.Override(GroundY + 2.5f / Mathf.Max(0.01f, Falloff));
		cq.enableMainLightContribution.Override(x: true);
		cq.anisotropy.Override(Mathf.Clamp(G, -0.95f, 0.95f));
		cq.scattering.Override(Mathf.Clamp01(Density / 0.6f * MainK * 1.1f));
		cq.tint.Override(Color.white);
		cq.enableAdditionalLightsContribution.Override(Sources.Count > 0);
		cq.maxSteps.Override(Mathf.Clamp(Steps + 14, 20, 44));
		cq.blurIterations.Override((!flag2) ? 1 : 2);
		float scattering = Mathf.Clamp(Density / 0.6f * LampK * 1.3f, 0f, 16f);
		foreach (Light source in Sources)
		{
			if (!source || source.type == LightType.Directional)
			{
				continue;
			}
			if (!cqLights.Contains(source))
			{
				if (!source.TryGetComponent<VolumetricAdditionalLight>(out var _))
				{
					source.gameObject.AddComponent<VolumetricAdditionalLight>();
				}
				cqLights.Add(source);
			}
			VolumetricAdditionalLight component2 = source.GetComponent<VolumetricAdditionalLight>();
			component2.Scattering = scattering;
			component2.Anisotropy = 0.2f;
			component2.Radius = 0.25f;
		}
	}
}
}
