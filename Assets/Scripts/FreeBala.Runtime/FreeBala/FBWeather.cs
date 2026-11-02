using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public static class FBWeather
{
	public static bool Night;

	public static bool Rain;

	public static bool Dusk;

	private static float duskT;

	private static Light sun;

	private static readonly List<Light> heads = new List<Light>();

	private static List<Vector3> lamps;

	private static Color sunCol;

	private static Color ambSky;

	private static Color ambEq;

	private static Color ambGr;

	private static Color fogCol;

	private static float sunI;

	private static float fogNear;

	private static float fogFar;

	private static float groundY;

	private static float cover;

	private static Vector2 wind;

	private static ReflectionProbe[] probes;

	private static float[] probeI;

	private static float curN;

	private static float duskLt;

	private static float lampCct = 3000f;

	private static GameObject cones;

	private static Material coneMat;

	private static readonly Dictionary<Material, (bool on, Color c)> lampMats = new Dictionary<Material, (bool, Color)>();

	private static readonly Dictionary<VolumeProfile, (float exp, float sat, Color f)> gradeBase = new Dictionary<VolumeProfile, (float, float, Color)>();

	private static readonly Dictionary<VolumeProfile, (float i, float t)> bloomBase = new Dictionary<VolumeProfile, (float, float)>();

	private static readonly Dictionary<Material, (Color c, float s)> wet = new Dictionary<Material, (Color, float)>();

	private static readonly Dictionary<Material, bool> soilM = new Dictionary<Material, bool>();

	private static float wetK = -1f;

	private static readonly Regex WetRx = new Regex("asphalt|asfalto|road|sidewalk|calcada|paver|pav_|piso|concrete|concreto|conc|curb|meiofio|slab|laje|dirt|terra|sand|areia|granite|tile_floor|quadra|court", RegexOptions.IgnoreCase);

	public static IReadOnlyList<Vector3> Lamps => lamps;

	public static float NightK => curN;

	public static void Apply((string time, string weather) w)
	{
		Night = w.time == "noite";
		Dusk = w.time == "anoitecer";
		Rain = w.weather == "chuva";
		duskT = 0f;
		lamps = null;
		cones = null;
		RestoreWet();
		wet.Clear();
		sun = (RenderSettings.sun ? RenderSettings.sun : UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault((Light l) => l.type == LightType.Directional));
		if (!sun)
		{
			return;
		}
		sunCol = sun.color;
		sunI = sun.intensity;
		ambSky = RenderSettings.ambientSkyColor;
		ambEq = RenderSettings.ambientEquatorColor;
		ambGr = RenderSettings.ambientGroundColor;
		fogCol = RenderSettings.fogColor;
		fogNear = RenderSettings.fogStartDistance;
		fogFar = RenderSettings.fogEndDistance;
		probes = UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None);
		probeI = probes.Select((ReflectionProbe p) => p.intensity).ToArray();
		cover = UnityEngine.Random.Range(0.3f, 0.62f);
		float f = UnityEngine.Random.Range(0f, (float)Math.PI * 2f);
		wind = new Vector2(Mathf.Cos(f), Mathf.Sin(f));
		GameObject gameObject = new GameObject("FB_Clima");
		gameObject.AddComponent<FBWeatherTicker>();
		Bounds? bounds = null;
		MeshCollider[] array = UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None);
		foreach (MeshCollider meshCollider in array)
		{
			if (meshCollider.gameObject.layer == 8)
			{
				if (!bounds.HasValue)
				{
					bounds = meshCollider.bounds;
					continue;
				}
				Bounds value = bounds.Value;
				value.Encapsulate(meshCollider.bounds);
				bounds = value;
			}
		}
		FBLivingSky.Start(bounds);
		groundY = (FBMatch.Player ? FBMatch.Player.Pos.y : (bounds?.min.y ?? 0f));
		FBGfx.SetWind(wind, Rain ? 1.7f : UnityEngine.Random.Range(0.8f, 1.15f), groundY);
		if (Rain)
		{
			FBRain.Begin(gameObject.transform, groundY);
		}
		GraphicsSettings.lightsUseColorTemperature = true;
		if ((bool)sun)
		{
			sun.useColorTemperature = true;
			sun.colorTemperature = 5400f;
			sun.shadows = LightShadows.Soft;
		}
		GameObject[] rootGameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
		foreach (GameObject gameObject2 in rootGameObjects)
		{
			if (gameObject2.name.StartsWith("Map_"))
			{
				FBStreetPoles.Clean(gameObject2);
			}
		}
		string text = SceneManager.GetActiveScene().name.Replace("Map_", "");
		lampCct = (text.Contains("praca_aguas") ? 5200 : 3000);
		FBVolumetric.Clear();
		FBVolumetric.InScene = true;
		FBVolumetric.GroundY = groundY;
		FindWet();
		lamps = FindLamps();
		FindLampMats();
		FBStreetLights.Build(lamps, gameObject.transform, text);
		FBAccentLights.Build(gameObject.transform, text);
		BuildCones();
		heads.Clear();
		FBCarLights.Reset();
		FBWindowLights.Build();
		LightVolume();
		Set(Night ? 1 : 0);
		FBAmbientFx.Start(gameObject.transform, bounds);
		FBBirds.Begin(gameObject.transform, groundY, Night, Rain);
	}

	private static void Set(float n)
	{
		if (!sun)
		{
			return;
		}
		float num = (Rain ? 1 : 0);
		float num2 = Mathf.Lerp(Dusk ? 13 : 38, -14f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(n / 0.62f)));
		Quaternion rotation = Quaternion.Euler(Mathf.Max(num2, 3.5f), -35f, 0f);
		Quaternion quaternion = Quaternion.Euler(40f, 150f, 0f);
		float num3 = Mathf.Clamp01(Mathf.InverseLerp(-1.5f, 9f, num2));
		float num4 = 1f - Mathf.InverseLerp(3f, 28f, num2);
		float num5 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.85f, n));
		Color a = Color.Lerp(sunCol, new Color(0.75f, 0.75f, 0.78f), num * 0.6f);
		if (num3 > 0.001f || num5 <= 0f)
		{
			sun.transform.rotation = rotation;
			sun.color = Color.Lerp(a, new Color(1f, 0.6f, 0.36f), num4 * (1f - num * 0.6f) * 0.85f);
			sun.intensity = sunI * (1f - num * 0.55f) * Mathf.Pow(num3, 0.8f) * Mathf.Lerp(1f, 0.78f, num4);
			sun.shadowStrength = (Rain ? 0.35f : 0.9f);
		}
		else
		{
			sun.transform.rotation = quaternion;
			sun.color = new Color(0.58f, 0.68f, 1f);
			sun.intensity = sunI * 0.13f * num5 * (1f - num * 0.6f);
			sun.shadowStrength = 0.55f;
		}
		float num6 = Mathf.Clamp01(Mathf.Max(n, (1f - num3) * 0.8f));
		float num7 = num4 * (1f - num6) * (1f - num);
		RenderSettings.ambientSkyColor = Color.Lerp(Color.Lerp(Color.Lerp(ambSky, new Color(0.55f, 0.58f, 0.62f), num * 0.5f), new Color(0.7f, 0.6f, 0.62f), num7 * 0.35f), new Color(0.24f, 0.29f, 0.42f), num6);
		RenderSettings.ambientEquatorColor = Color.Lerp(Color.Lerp(Color.Lerp(ambEq, new Color(0.45f, 0.46f, 0.48f), num * 0.5f), new Color(0.72f, 0.55f, 0.45f), num7 * 0.3f), new Color(0.17f, 0.19f, 0.27f), num6);
		RenderSettings.ambientGroundColor = Color.Lerp(ambGr, new Color(0.07f, 0.07f, 0.09f), num6);
		Color color = Color.Lerp(Color.Lerp(Color.Lerp(fogCol, new Color(0.55f, 0.58f, 0.62f), num * 0.7f), new Color(0.86f, 0.62f, 0.48f), num7 * 0.45f), new Color(0.02f, 0.03f, 0.06f), num6);
		RenderSettings.fog = true;
		RenderSettings.fogColor = color;
		RenderSettings.fogStartDistance = Mathf.Lerp(fogNear * 0.75f, fogNear * 0.4f, Mathf.Max(n * 0.6f, num * 0.7f));
		RenderSettings.fogEndDistance = Mathf.Lerp(fogFar * 0.85f, fogFar * 0.45f, Mathf.Max(n * 0.5f, num * 0.6f));
		Shader.SetGlobalColor("_FBFogColor", Color.Lerp(color, Color.white, 0.08f * (1f - num6)));
		Shader.SetGlobalColor("_FBFogSunColor", (num3 > 0.001f) ? Color.Lerp(color, sun.color, 0.55f) : Color.Lerp(color, new Color(0.35f, 0.42f, 0.6f), 0.35f * num5));
		Shader.SetGlobalVector("_FBFogParams", new Vector4(Mathf.Lerp(Mathf.Lerp(0.0026f, 0.0034f, num6), 0.0065f, num), 0.045f, groundY, Mathf.Lerp(0.5f, 0.78f, Mathf.Max(num6, num))));
		Shader.SetGlobalVector("_FBFogSun", new Vector4(0f - sun.transform.forward.x, 0f - sun.transform.forward.y, 0f - sun.transform.forward.z, (1f - num * 0.7f) * ((num3 > 0.001f) ? 0.9f : 0.35f)));
		if (probes != null)
		{
			for (int i = 0; i < probes.Length; i++)
			{
				if ((bool)probes[i])
				{
					probes[i].intensity = probeI[i] * Mathf.Lerp(1f, 0.12f, num6) * (1f - num * 0.3f);
				}
			}
		}
		RenderSettings.reflectionIntensity = Mathf.Lerp(1f, 0.02f, num6) * (1f - num * 0.2f);
		RenderSettings.ambientIntensity = Mathf.Lerp(1f, 0.4f, num6);
		NightGrade(num6);
		ApplyWet(Mathf.Max(num, Mathf.SmoothStep(0f, 0.7f, Mathf.InverseLerp(0.3f, 1f, n))));
		float ambientIntensity = RenderSettings.ambientIntensity;
		SphericalHarmonicsL2 ambientProbe = default(SphericalHarmonicsL2);
		Color color2 = RenderSettings.ambientEquatorColor * ambientIntensity;
		ambientProbe.AddAmbientLight(color2);
		ambientProbe.AddDirectionalLight(Vector3.up, (RenderSettings.ambientSkyColor * ambientIntensity - color2) * 0.9f, 1f);
		ambientProbe.AddDirectionalLight(Vector3.down, (RenderSettings.ambientGroundColor * ambientIntensity - color2) * 0.9f, 1f);
		RenderSettings.ambientProbe = ambientProbe;
		DynamicGI.UpdateEnvironment();
		if ((bool)RenderSettings.skybox && RenderSettings.skybox.HasProperty("_Exposure"))
		{
			RenderSettings.skybox.SetFloat("_Exposure", Mathf.Lerp(Rain ? 0.55f : 1.15f, 0.12f, n));
		}
		if ((bool)RenderSettings.skybox && RenderSettings.skybox.HasProperty("_Tint"))
		{
			RenderSettings.skybox.SetColor("_Tint", Color.Lerp(Rain ? new Color(0.6f, 0.62f, 0.66f) : new Color(0.5f, 0.5f, 0.5f), new Color(0.2f, 0.25f, 0.45f), n));
		}
		FBVolumetric.Density = Mathf.Lerp(Mathf.Lerp(0.0016f, 0.0062f, num6), 0.0105f, num);
		FBVolumetric.Falloff = Mathf.Lerp(0.05f, 0.035f, num6);
		FBVolumetric.G = Mathf.Lerp(0.62f, 0.4f, num6);
		FBVolumetric.MaxDist = Mathf.Lerp(160f, 120f, num6);
		FBVolumetric.LampK = (Rain ? 3.6f : 2.6f);
		FBVolumetric.MainColor = sun.color.linear * sun.intensity * ((num3 > 0.001f) ? Mathf.Lerp(0.55f, 0.9f, num4) : 1.4f) * (1f - num * 0.5f);
		FBVolumetric.MainK = ((num3 > 0.001f) ? Mathf.Lerp(0.55f, 0.9f, num4) : 1.4f) * (1f - num * 0.5f);
		FBVolumetric.MainDir = -sun.transform.forward;
		FBVolumetric.Ambient = RenderSettings.ambientEquatorColor.linear * RenderSettings.ambientIntensity * 0.55f;
		curN = n;
		if ((bool)FBLivingSky.I)
		{
			FBLivingSky.I.SetWeather(n, Rain, Dusk ? 1 : 0, Quaternion.Euler(num2, -35f, 0f) * Vector3.back, quaternion * Vector3.back, cover, wind);
		}
	}

	private static void BuildCones()
	{
		cones = null;
		if (lamps == null || lamps.Count == 0)
		{
			return;
		}
		List<Vector3> list = new List<Vector3>();
		List<Vector2> list2 = new List<Vector2>();
		List<int> list3 = new List<int>();
		List<Color> list4 = new List<Color>();
		foreach (Vector3 lamp in lamps)
		{
			Color item = FBStreetLights.GlowColorAt(lamp);
			RaycastHit hitInfo;
			float num = (Physics.Raycast(lamp + Vector3.down * 0.3f, Vector3.down, out hitInfo, 30f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) ? hitInfo.point.y : (lamp.y - 8f));
			float num2 = Mathf.Max(2f, lamp.y - num);
			float num3 = 0.18f;
			float num4 = Mathf.Clamp(num2 * 0.42f, 2f, 5.5f);
			int count = list.Count;
			for (int i = 0; i <= 18; i++)
			{
				float f = (float)i / 18f * (float)Math.PI * 2f;
				Vector3 vector = new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f));
				list.Add(lamp + vector * num3 + Vector3.down * 0.05f);
				list2.Add(new Vector2((float)i / 18f, 1f));
				list.Add(new Vector3(lamp.x, num + 0.05f, lamp.z) + vector * num4);
				list2.Add(new Vector2((float)i / 18f, 0f));
				list4.Add(item);
				list4.Add(item);
			}
			for (int j = 0; j < 18; j++)
			{
				int num5 = count + j * 2;
				list3.AddRange(new int[6]
				{
					num5,
					num5 + 1,
					num5 + 2,
					num5 + 2,
					num5 + 1,
					num5 + 3
				});
			}
		}
		Mesh mesh = new Mesh
		{
			name = "cones_postes",
			indexFormat = IndexFormat.UInt32
		};
		mesh.SetVertices(list);
		mesh.SetUVs(0, list2);
		mesh.SetColors(list4);
		mesh.SetTriangles(list3, 0);
		mesh.RecalculateBounds();
		Texture2D texture2D = new Texture2D(4, 64, TextureFormat.RGBA32, mipChain: false)
		{
			wrapMode = TextureWrapMode.Clamp
		};
		for (int k = 0; k < 64; k++)
		{
			float num6 = (float)k / 63f;
			float a = Mathf.Pow(num6, 2.2f) * 0.9f + 0.1f * num6;
			for (int l = 0; l < 4; l++)
			{
				texture2D.SetPixel(l, k, new Color(1f, 1f, 1f, a));
			}
		}
		texture2D.Apply();
		coneMat = FBShaders.Transparent(new Material(FBShaders.ParticlesUnlit), additive: true);
		coneMat.SetTexture("_BaseMap", texture2D);
		coneMat.mainTexture = texture2D;
		coneMat.SetFloat("_Cull", 0f);
		coneMat.renderQueue = 3010;
		cones = new GameObject("FB_ConesPostes");
		cones.AddComponent<MeshFilter>().sharedMesh = mesh;
		MeshRenderer meshRenderer = cones.AddComponent<MeshRenderer>();
		meshRenderer.sharedMaterial = coneMat;
		meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
		meshRenderer.receiveShadows = false;
		cones.SetActive(value: false);
	}

	private static void LightVolume()
	{
		Volume volume = new GameObject("FB_Iluminacao").AddComponent<Volume>();
		volume.isGlobal = true;
		volume.priority = 50f;
		VolumeProfile volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
		volumeProfile.name = "FB_Iluminacao";
		volume.profile = volumeProfile;
		volumeProfile.Add<Tonemapping>(overrides: true).mode.value = TonemappingMode.Neutral;
		ColorAdjustments colorAdjustments = volumeProfile.Add<ColorAdjustments>(overrides: true);
		colorAdjustments.postExposure.value = 0f;
		colorAdjustments.contrast.value = 8f;
		colorAdjustments.saturation.value = 6f;
		colorAdjustments.colorFilter.value = Color.white;
		Bloom bloom = volumeProfile.Add<Bloom>(overrides: true);
		bloom.threshold.value = 1.05f;
		bloom.intensity.value = (FBQuality.Mobile ? 0.15f : 0.3f);
		bloom.scatter.value = 0.65f;
		bloom.highQualityFiltering.value = !FBQuality.Mobile;
		Vignette vignette = volumeProfile.Add<Vignette>(overrides: true);
		vignette.intensity.value = 0.18f;
		vignette.smoothness.value = 0.45f;
		gradeBase.Clear();
		bloomBase.Clear();
		FBVolumetric.AttachProfile(volumeProfile);
	}

	public static string Diag()
	{
		SphericalHarmonicsL2 ambientProbe = RenderSettings.ambientProbe;
		string text = "";
		Volume[] array = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
		foreach (Volume volume in array)
		{
			if ((bool)volume && volume.isGlobal && (bool)volume.profile && volume.profile.TryGet<ColorAdjustments>(out var component))
			{
				text += $" vol {volume.name} w{volume.weight:0.##} exp {(component.postExposure.overrideState ? component.postExposure.value : 0f):0.##} ativo {component.active}";
			}
		}
		Light[] source = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
		int num = source.Count((Light l) => l.enabled && l.intensity > 0.01f && l.type != LightType.Directional);
		string text2 = string.Join(" ", from l in source
			where l.type == LightType.Directional
			select $"{l.name}:{(l.enabled ? l.intensity : 0f):0.###}");
		return string.Format("n {0:0.##} · dir {1} · amb {2} k {3:0.##} céu {4} sonda {5:0.###}/{6:0.###}/{7:0.###} · refl {8:0.##} · luzes {9} postes {10} (reais {11}; {12}) · volumétrica {13} · {14}", curN, text2, RenderSettings.ambientMode, RenderSettings.ambientIntensity, RenderSettings.ambientSkyColor, ambientProbe[0, 0], ambientProbe[1, 0], ambientProbe[2, 0], RenderSettings.reflectionIntensity, num, lamps?.Count ?? 0, FBStreetLights.Lit, FBStreetLights.Diag(), (FBVolumetric.Active && FBVolumetric.InScene) ? $"sim dens {FBVolumetric.Density:0.0000} fontes {FBVolumetric.Sources.Count}" : "não", text);
	}

	private static void FindLampMats()
	{
		RestoreLampMats();
		Regex regex = new Regex("lamp|lampada|luminaria|refletor|light_on|_on$|farol_poste|_light$", RegexOptions.IgnoreCase);
		MeshRenderer[] array = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
		foreach (MeshRenderer meshRenderer in array)
		{
			if (meshRenderer.gameObject.layer != 8)
			{
				continue;
			}
			Material[] sharedMaterials = meshRenderer.sharedMaterials;
			foreach (Material material in sharedMaterials)
			{
				if ((bool)material && !lampMats.ContainsKey(material) && regex.IsMatch(material.name) && material.HasProperty("_EmissionColor"))
				{
					lampMats[material] = (material.IsKeywordEnabled("_EMISSION"), material.GetColor("_EmissionColor"));
				}
			}
		}
	}

	public static void RestoreLampMats()
	{
		foreach (KeyValuePair<Material, (bool, Color)> lampMat in lampMats)
		{
			if ((bool)lampMat.Key)
			{
				lampMat.Key.SetColor("_EmissionColor", lampMat.Value.Item2);
				if (!lampMat.Value.Item1)
				{
					lampMat.Key.DisableKeyword("_EMISSION");
				}
			}
		}
		lampMats.Clear();
	}

	private static void LampGlow(float k)
	{
		Color color = Mathf.CorrelatedColorTemperatureToRGB(lampCct) * (3.5f * k);
		foreach (KeyValuePair<Material, (bool, Color)> lampMat in lampMats)
		{
			if (!lampMat.Key)
			{
				continue;
			}
			if (k > 0.01f)
			{
				lampMat.Key.EnableKeyword("_EMISSION");
				lampMat.Key.SetColor("_EmissionColor", (lampMat.Value.Item2.maxColorComponent > 0.05f && !lampMat.Key.name.ToLower().Contains("lamp")) ? Accent(lampMat.Value.Item2, k) : (lampMat.Value.Item2 + color));
				continue;
			}
			lampMat.Key.SetColor("_EmissionColor", lampMat.Value.Item2);
			if (!lampMat.Value.Item1)
			{
				lampMat.Key.DisableKeyword("_EMISSION");
			}
		}
	}

	private static Color Accent(Color c0, float k)
	{
		float num = Mathf.Max(0.001f, c0.maxColorComponent);
		Color color = c0 / num;
		float t = 1f - Mathf.Min(color.r, Mathf.Min(color.g, color.b));
		return color * ((0.35f + 1.05f * k) * Mathf.Lerp(0.45f, 1f, t));
	}

	private static void NightGrade(float dk)
	{
		Volume[] array = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
		foreach (Volume volume in array)
		{
			if (!volume || !volume.isGlobal)
			{
				continue;
			}
			VolumeProfile profile = volume.profile;
			if (!profile)
			{
				continue;
			}
			if (!profile.TryGet<ColorAdjustments>(out var component))
			{
				component = profile.Add<ColorAdjustments>(overrides: true);
			}
			if (!gradeBase.TryGetValue(profile, out (float, float, Color) value))
			{
				value = (gradeBase[profile] = (component.postExposure.overrideState ? component.postExposure.value : 0f, component.saturation.overrideState ? component.saturation.value : 0f, component.colorFilter.overrideState ? component.colorFilter.value : Color.white));
			}
			component.postExposure.overrideState = true;
			component.postExposure.value = value.Item1 + Mathf.Lerp(0f, -0.45f, dk);
			component.saturation.overrideState = true;
			component.saturation.value = value.Item2 + Mathf.Lerp(0f, -30f, dk);
			component.colorFilter.overrideState = true;
			component.colorFilter.value = value.Item3 * Color.Lerp(Color.white, new Color(0.78f, 0.85f, 1f), dk);
			if (profile.TryGet<Bloom>(out var component2))
			{
				if (!bloomBase.TryGetValue(profile, out (float, float) value2))
				{
					value2 = (bloomBase[profile] = (component2.intensity.value, component2.threshold.value));
				}
				MinFloatParameter intensity = component2.intensity;
				bool overrideState = (component2.threshold.overrideState = true);
				intensity.overrideState = overrideState;
				component2.intensity.value = Mathf.Lerp(value2.Item1, Mathf.Max(value2.Item1, 0.95f), dk);
				component2.threshold.value = Mathf.Lerp(value2.Item2, Mathf.Min(value2.Item2, 0.75f), dk);
			}
		}
	}

	private static void ApplyWet(float k)
	{
		if (Mathf.Abs(k - wetK) < 0.02f)
		{
			return;
		}
		wetK = k;
		foreach (KeyValuePair<Material, (Color, float)> item3 in wet)
		{
			Material key = item3.Key;
			if ((bool)key)
			{
				(Color, float) value = item3.Value;
				Color item = value.Item1;
				float item2 = value.Item2;
				bool value2;
				bool flag = soilM.TryGetValue(key, out value2) && value2;
				key.SetColor("_BaseColor", Color.Lerp(item, new Color(item.r * 0.74f, item.g * 0.74f, item.b * 0.76f, item.a), k));
				key.SetFloat("_Smoothness", Mathf.Lerp(item2, Mathf.Max(item2, flag ? 0.32f : 0.62f), k));
			}
		}
	}

	private static void FindWet()
	{
		MeshRenderer[] array = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
		foreach (MeshRenderer meshRenderer in array)
		{
			if (meshRenderer.gameObject.layer != 8)
			{
				continue;
			}
			Material[] sharedMaterials = meshRenderer.sharedMaterials;
			foreach (Material material in sharedMaterials)
			{
				if ((bool)material && !wet.ContainsKey(material) && WetRx.IsMatch(material.name) && material.HasProperty("_Smoothness") && material.HasProperty("_BaseColor") && material.renderQueue < 2450)
				{
					Color color = material.GetColor("_BaseColor");
					float @float = material.GetFloat("_Smoothness");
					wet[material] = (color, @float);
					bool value = Regex.IsMatch(material.name, "dirt|terra|sand|areia", RegexOptions.IgnoreCase);
					soilM[material] = value;
				}
			}
		}
	}

	public static void ResetGlobals()
	{
		Shader.SetGlobalVector("_FBFogParams", Vector4.zero);
		Shader.SetGlobalVector("_FBFogSun", Vector4.zero);
	}

	public static void RestoreWet()
	{
		foreach (KeyValuePair<Material, (Color, float)> item in wet)
		{
			if ((bool)item.Key)
			{
				item.Key.SetColor("_BaseColor", item.Value.Item1);
				item.Key.SetFloat("_Smoothness", item.Value.Item2);
			}
		}
		wet.Clear();
		soilM.Clear();
		wetK = -1f;
	}

	private static List<Vector3> FindLamps()
	{
		FBLampData[] source = UnityEngine.Object.FindObjectsByType<FBLampData>(FindObjectsSortMode.None);
		List<Vector3> list = source.SelectMany((FBLampData d) => d.pos).ToList();
		HashSet<Vector3> sod = new HashSet<Vector3>(source.SelectMany((FBLampData d) => d.pos.Where((Vector3 _, int i) => d.sodium != null && i < d.sodium.Length && d.sodium[i])));
		FBStreetLights.SodiumAt = (Vector3 p) => sod.Contains(p);
		if (list.Count > 0)
		{
			return list;
		}
		List<Vector3> res = new List<Vector3>();
		Regex regex = new Regex("poste|lamp(_on|ada)|lumin|street_?light", RegexOptions.IgnoreCase);
		MeshRenderer[] array = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
		foreach (MeshRenderer meshRenderer in array)
		{
			if (meshRenderer.gameObject.layer != 8 || !meshRenderer.enabled)
			{
				continue;
			}
			string text = meshRenderer.name + " " + (meshRenderer.transform.parent ? meshRenderer.transform.parent.name : "") + " " + (meshRenderer.sharedMaterial ? meshRenderer.sharedMaterial.name : "");
			if (!regex.IsMatch(text))
			{
				continue;
			}
			Bounds bounds = meshRenderer.bounds;
			bool flag = text.ToLower().Contains("lamp");
			MeshFilter component = meshRenderer.GetComponent<MeshFilter>();
			Mesh mesh = (component ? component.sharedMesh : null);
			if ((bool)mesh && mesh.isReadable)
			{
				Vector3[] vertices = mesh.vertices;
				Matrix4x4 localToWorldMatrix = meshRenderer.transform.localToWorldMatrix;
				Dictionary<Vector2Int, (float, float, List<Vector3>)> dictionary = new Dictionary<Vector2Int, (float, float, List<Vector3>)>();
				Vector3[] array2 = vertices;
				foreach (Vector3 point in array2)
				{
					Vector3 item = localToWorldMatrix.MultiplyPoint3x4(point);
					Vector2Int key = new Vector2Int(Mathf.FloorToInt(item.x / 3f), Mathf.FloorToInt(item.z / 3f));
					if (!dictionary.TryGetValue(key, out var value))
					{
						value = (item.y, item.y, new List<Vector3>());
					}
					value.Item1 = Mathf.Min(value.Item1, item.y);
					value.Item2 = Mathf.Max(value.Item2, item.y);
					value.Item3.Add(item);
					dictionary[key] = value;
				}
				foreach (var value2 in dictionary.Values)
				{
					if (value2.Item2 - value2.Item1 < 3f && !flag)
					{
						continue;
					}
					Vector3 zero = Vector3.zero;
					int num = 0;
					foreach (Vector3 item2 in value2.Item3)
					{
						if (item2.y > value2.Item2 - 0.5f)
						{
							zero += item2;
							num++;
						}
					}
					Vector3 vector = zero / Mathf.Max(1, num);
					Add(new Vector3(vector.x, value2.Item2 - 0.9f, vector.z));
				}
			}
			else if (!(bounds.size.y < 3f) || flag)
			{
				Add(new Vector3(bounds.center.x, bounds.max.y - 0.9f, bounds.center.z));
			}
		}
		return res;
		void Add(Vector3 h)
		{
			if (!res.Any((Vector3 p) => (p - h).sqrMagnitude < 6.25f))
			{
				res.Add(h);
			}
		}
	}

	public static void ForceNight(bool on)
	{
		Night = on;
		Dusk = false;
		Set(on ? 1 : 0);
	}

	public static void SkipDusk(float secs)
	{
		if (Dusk)
		{
			duskT = Mathf.Max(0f, secs - 0.01f);
			duskLt = 0f;
			Tick(0.02f);
		}
	}

	public static void Tick(float dt)
	{
		if (Dusk && duskT < 420f && (duskT += dt) > 0f && (duskLt -= dt) <= 0f)
		{
			duskLt = 0.25f;
			float num = duskT / 420f;
			Set(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((num - 0.08f) / 0.84f)));
		}
		Camera main = Camera.main;
		if (!main)
		{
			return;
		}
		if ((bool)cones)
		{
			float num2 = Mathf.InverseLerp(0.4f, 1f, curN);
			cones.SetActive(num2 > 0.01f && (!FBVolumetric.Active || !FBVolumetric.InScene));
			if ((bool)coneMat)
			{
				coneMat.SetColor("_BaseColor", new Color(1f, 0.78f, 0.5f, 1f) * (0.09f * num2 * (Rain ? 1.6f : 1f)));
			}
		}
		FBCarLights.Tick(main, curN, Rain, Time.deltaTime);
		FBWindowLights.Tick(main, curN, Time.deltaTime);
		LampGlow(Mathf.InverseLerp(0.45f, 1f, curN));
		FBStreetLights.Tick(main, curN, Time.deltaTime);
		FBAccentLights.Tick(curN);
		FBVolumetric.Tick(main);
	}
}
}
