using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public static class FBStreetLights
{
	public struct Lamp
	{
		public Vector3 p;

		public float cct;

		public float intensity;

		public float range;

		public float angle;

		public float inner;

		public bool sodium;
	}

	private static readonly List<Lamp> lamps = new List<Lamp>();

	private static float[] w;

	private static readonly List<Light> slots = new List<Light>();

	private static readonly Dictionary<int, int> slotOf = new Dictionary<int, int>();

	private static readonly HashSet<int> sel = new HashSet<int>();

	private static float selT;

	private static int K;

	private static int maxShadow;

	private static float maxDist;

	public static Func<Vector3, bool> SodiumAt;

	private static bool[] dead = new bool[0];

	private static bool[] flkOn = new bool[0];

	private static float[] flk = new float[0];

	private static float[] flkNext = new float[0];

	private static ParticleSystem glowPs;

	private static ParticleSystem.Particle[] glowP;

	private static bool[] cut = new bool[0];

	private static GameObject glow;

	public static IReadOnlyList<Lamp> Lamps => lamps;

	public static int Lit { get; private set; }

	private static uint Hash(Vector3 p)
	{
		return (uint)((Mathf.RoundToInt(p.x * 3f) * 73856093) ^ (Mathf.RoundToInt(p.z * 3f) * 83492791));
	}

	private static Lamp Classify(Vector3 p, string map)
	{
		bool flag = map.Contains("praca_aguas");
		bool flag2 = map.Contains("vila_jardim");
		Lamp result;
		if ((Physics.Raycast(p + Vector3.down * 0.2f, Vector3.down, out var hitInfo, 30f, 256, QueryTriggerInteraction.Ignore) ? (p.y - hitInfo.point.y) : 99f) < 6f)
		{
			result = default(Lamp);
			result.p = p;
			result.cct = 4300f;
			result.intensity = 18f;
			result.range = 11f;
			result.angle = 150f;
			result.inner = 100f;
			return result;
		}
		if (SodiumAt == null || !SodiumAt(p))
		{
			bool num;
			if (!flag2)
			{
				if (flag)
				{
					goto IL_0149;
				}
				num = Hash(p) % 100 < 30;
			}
			else
			{
				num = Hash(p) % 100 < 45;
			}
			if (!num)
			{
				goto IL_0149;
			}
		}
		result = default(Lamp);
		result.p = p;
		result.cct = 2100f;
		result.intensity = 85f;
		result.range = 32f;
		result.angle = 128f;
		result.inner = 60f;
		result.sodium = true;
		return result;
		IL_0149:
		result = default(Lamp);
		result.p = p;
		result.cct = (flag ? 4700 : 4100);
		result.intensity = (flag ? 125 : 100);
		result.range = (flag ? 40 : 34);
		result.angle = (flag ? 150 : 135);
		result.inner = (flag ? 80 : 65);
		return result;
	}

	public static Color GlowColor(Lamp l)
	{
		if (!l.sodium)
		{
			if (!(l.cct > 4400f))
			{
				return new Color(1f, 0.9f, 0.74f);
			}
			return new Color(0.93f, 0.96f, 1f);
		}
		return new Color(1f, 0.62f, 0.26f);
	}

	public static Color GlowColorAt(Vector3 p)
	{
		foreach (Lamp lamp in lamps)
		{
			if ((lamp.p - p).sqrMagnitude < 0.01f)
			{
				return GlowColor(lamp);
			}
		}
		return new Color(1f, 0.86f, 0.6f);
	}

	public static bool IsDead(int i)
	{
		if (i >= 0 && i < dead.Length)
		{
			return dead[i];
		}
		return false;
	}

	public static bool IsCut(int i)
	{
		if (i >= 0 && i < cut.Length)
		{
			return cut[i];
		}
		return false;
	}

	public static void Cut(int i, bool off)
	{
		if (i < 0 || i >= lamps.Count)
		{
			return;
		}
		if (cut.Length != lamps.Count)
		{
			Array.Resize(ref cut, lamps.Count);
		}
		if (cut[i] == off)
		{
			return;
		}
		cut[i] = off;
		if (!dead[i])
		{
			SetGlow(i, !off);
			SetHead(i, !off);
			if (off)
			{
				FBFx.Sparks(lamps[i].p, Vector3.down);
			}
		}
	}

	public static int Nearest(Vector3 p, float maxD)
	{
		int result = -1;
		float num = maxD * maxD;
		for (int i = 0; i < lamps.Count; i++)
		{
			if (!IsDead(i))
			{
				float sqrMagnitude = (lamps[i].p - p).sqrMagnitude;
				if (sqrMagnitude < num)
				{
					num = sqrMagnitude;
					result = i;
				}
			}
		}
		return result;
	}

	public static void Kill(int i, float flicker, bool hideGlowNow, bool shot = false)
	{
		if (i >= 0 && i < lamps.Count && !dead[i])
		{
			dead[i] = true;
			flk[i] = flicker;
			flkNext[i] = 0f;
			flkOn[i] = true;
			Vector3 p = lamps[i].p;
			FBFx.Sparks(p, Vector3.down);
			FBAudio.PlayAt("glass_break", p, shot ? 0.75f : 0.45f, 9f);
			if (shot)
			{
				FBImpactFx.Glass(p, Vector3.down, Vector3.down);
			}
			if (hideGlowNow)
			{
				SetGlow(i, on: false);
				FBLampHead.SetLamp(i, on: false, p, lamps);
			}
		}
	}

	private static void SetHead(int i, bool on)
	{
		FBLampHead.SetLamp(i, on, lamps[i].p, lamps);
	}

	private static void SetGlow(int i, bool on)
	{
		if ((bool)glowPs)
		{
			if (glowP == null || glowP.Length != glowPs.particleCount)
			{
				glowP = new ParticleSystem.Particle[glowPs.particleCount];
				glowPs.GetParticles(glowP);
			}
			if (i < glowP.Length)
			{
				Color color = GlowColor(lamps[i]);
				glowP[i].startColor = (on ? color : new Color(0f, 0f, 0f, 0f));
				glowPs.SetParticles(glowP, glowP.Length);
			}
		}
	}

	public static void Build(IReadOnlyList<Vector3> pos, Transform parent, string map)
	{
		lamps.Clear();
		slots.Clear();
		slotOf.Clear();
		sel.Clear();
		selT = 0f;
		Lit = 0;
		glowPs = null;
		glowP = null;
		if (pos == null)
		{
			w = new float[0];
			dead = new bool[0];
			flkOn = new bool[0];
			flk = new float[0];
			flkNext = new float[0];
			return;
		}
		foreach (Vector3 po in pos)
		{
			lamps.Add(Classify(po, map));
		}
		w = new float[lamps.Count];
		dead = new bool[lamps.Count];
		flkOn = new bool[lamps.Count];
		flk = new float[lamps.Count];
		flkNext = new float[lamps.Count];
		FBLampHead.ResetScene();
		FBLampHead.Bind(lamps);
		bool mobile = FBQuality.Mobile;
		FBQLevel level = FBQuality.Level;
		K = (mobile ? 6 : (level switch
		{
			FBQLevel.Medio => 24, 
			FBQLevel.Alto => 40, 
			FBQLevel.Ultra => 56, 
			_ => 10, 
		}));
		maxShadow = ((!mobile) ? (level switch
		{
			FBQLevel.Medio => 3, 
			FBQLevel.Alto => 8, 
			FBQLevel.Ultra => 14, 
			_ => 0, 
		}) : 0);
		maxDist = (mobile ? 70 : ((level >= FBQLevel.Alto) ? 190 : 140));
		K = Mathf.Min(K, lamps.Count);
		for (int i = 0; i < K + Mathf.Min(6, lamps.Count - K); i++)
		{
			Light light = new GameObject("Poste" + i).AddComponent<Light>();
			light.transform.SetParent(parent);
			light.type = LightType.Spot;
			light.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
			light.useColorTemperature = true;
			light.color = Color.white;
			light.intensity = 0f;
			light.enabled = false;
			light.shadows = LightShadows.None;
			light.shadowStrength = 0.9f;
			light.shadowBias = 0.04f;
			light.shadowNormalBias = 0.3f;
			light.shadowNearPlane = 0.7f;
			FBLightUtil.MediumShadows(light);
			slots.Add(light);
			FBVolumetric.Sources.Add(light);
		}
		glow = null;
		if (lamps.Count > 0)
		{
			glow = new GameObject("PostesBrilho");
			glow.transform.SetParent(parent);
			Texture2D texture2D = new Texture2D(64, 64);
			for (int j = 0; j < 64; j++)
			{
				for (int k = 0; k < 64; k++)
				{
					float num = new Vector2(k - 32, j - 32).magnitude / 32f;
					float num2 = Mathf.Clamp01(1f - num);
					texture2D.SetPixel(k, j, new Color(1f, 1f, 1f, num2 * num2 * (0.55f + 0.45f * Mathf.Pow(Mathf.Clamp01(1f - num * 3f), 2f))));
				}
			}
			texture2D.Apply();
			Material material = new Material(FBShaders.ParticlesUnlit);
			material.SetTexture("_BaseMap", texture2D);
			material = FBShaders.Transparent(material, additive: true);
			material.renderQueue = 3100;
			ParticleSystem particleSystem = glow.AddComponent<ParticleSystem>();
			particleSystem.Stop();
			ParticleSystem.MainModule main = particleSystem.main;
			main.loop = false;
			main.startLifetime = 1000000f;
			main.startSpeed = 0f;
			main.maxParticles = lamps.Count + 1;
			main.playOnAwake = false;
			main.simulationSpace = ParticleSystemSimulationSpace.World;
			ParticleSystem.EmissionModule emission = particleSystem.emission;
			emission.enabled = false;
			ParticleSystemRenderer component = glow.GetComponent<ParticleSystemRenderer>();
			component.material = material;
			component.shadowCastingMode = ShadowCastingMode.Off;
			particleSystem.Play();
			foreach (Lamp lamp in lamps)
			{
				particleSystem.Emit(new ParticleSystem.EmitParams
				{
					position = lamp.p + Vector3.up * 0.55f,
					startSize = (lamp.sodium ? 4.2f : 3.6f),
					startLifetime = 1000000f,
					startColor = GlowColor(lamp)
				}, 1);
			}
			glowPs = particleSystem;
			glow.SetActive(value: false);
		}
		Debug.Log($"[FreeBala][luz] postes: {lamps.Count} luminárias ({lamps.Count((Lamp x) => x.sodium)} sódio), {K} luzes reais, {maxShadow} com sombra, alcance {maxDist:0} m");
	}

	public static void Tick(Camera cam, float nightK, float dt)
	{
		bool flag = nightK > 0.45f;
		if ((bool)glow)
		{
			glow.SetActive(flag);
		}
		if (lamps.Count == 0 || w == null)
		{
			return;
		}
		float num = Mathf.InverseLerp(0.45f, 1f, nightK);
		Vector3 cp = cam.transform.position;
		Vector3 forward = cam.transform.forward;
		if ((selT -= dt) <= 0f)
		{
			selT = 0.2f;
			sel.Clear();
			if (flag)
			{
				List<(float, int)> list = new List<(float, int)>();
				for (int j = 0; j < lamps.Count; j++)
				{
					if ((!dead[j] || !(flk[j] <= 0f)) && !IsCut(j))
					{
						Vector3 lhs = lamps[j].p - cp;
						float magnitude = lhs.magnitude;
						if (!(magnitude > maxDist))
						{
							bool flag2 = Vector3.Dot(lhs, forward) < -0.2f * magnitude && magnitude > lamps[j].range * 0.6f;
							list.Add((magnitude * (flag2 ? 1.45f : 1f) * ((w[j] > 0f) ? 0.8f : 1f), j));
						}
					}
				}
				list.Sort(((float s, int i) a, (float s, int i) b) => a.s.CompareTo(b.s));
				for (int k = 0; k < Mathf.Min(K, list.Count); k++)
				{
					sel.Add(list[k].Item2);
				}
			}
		}
		for (int l = 0; l < lamps.Count; l++)
		{
			if (!dead[l] || flk[l] <= 0f)
			{
				continue;
			}
			flk[l] -= dt;
			if (Time.time >= flkNext[l])
			{
				flkOn[l] = !flkOn[l];
				flkNext[l] = Time.time + (flkOn[l] ? UnityEngine.Random.Range(0.02f, 0.12f) : UnityEngine.Random.Range(0.04f, 0.3f));
				if (flag)
				{
					SetGlow(l, flkOn[l] && flk[l] > 0f);
				}
				SetHead(l, flkOn[l] && flk[l] > 0f);
				if (flkOn[l] && UnityEngine.Random.value < 0.35f)
				{
					FBFx.Sparks(lamps[l].p, Vector3.down);
				}
			}
			if (flk[l] <= 0f)
			{
				flkOn[l] = false;
				SetGlow(l, on: false);
				SetHead(l, on: false);
			}
		}
		for (int m = 0; m < lamps.Count; m++)
		{
			float num2 = (sel.Contains(m) ? 1 : 0);
			if (w[m] == num2 && num2 == 0f)
			{
				continue;
			}
			w[m] = Mathf.MoveTowards(w[m], num2, dt * 1.6f);
			if (w[m] > 0f && !slotOf.ContainsKey(m))
			{
				int num3 = FreeSlot();
				if (num3 < 0)
				{
					w[m] = 0f;
					continue;
				}
				slotOf[m] = num3;
				Light light = slots[num3];
				light.transform.position = lamps[m].p;
				light.colorTemperature = lamps[m].cct;
				light.range = lamps[m].range;
				light.spotAngle = lamps[m].angle;
				light.innerSpotAngle = lamps[m].inner;
			}
			if (w[m] <= 0f && slotOf.TryGetValue(m, out var value))
			{
				slots[value].enabled = false;
				slots[value].intensity = 0f;
				slots[value].shadows = LightShadows.None;
				slotOf.Remove(m);
			}
		}
		HashSet<int> hashSet = (from i in slotOf.Keys
			where w[i] > 0.95f
			orderby (lamps[i].p - cp).sqrMagnitude
			select i).Take(maxShadow).ToHashSet();
		Lit = 0;
		foreach (KeyValuePair<int, int> item in slotOf)
		{
			float num4 = (IsCut(item.Key) ? 0f : ((!dead[item.Key]) ? 1f : ((flk[item.Key] > 0f && flkOn[item.Key]) ? UnityEngine.Random.Range(0.6f, 1.1f) : 0f)));
			Light light2 = slots[item.Value];
			light2.intensity = lamps[item.Key].intensity * num * w[item.Key] * num4;
			light2.enabled = light2.intensity > 0.01f;
			if (light2.enabled)
			{
				Lit++;
			}
			LightShadows lightShadows = (hashSet.Contains(item.Key) ? LightShadows.Soft : LightShadows.None);
			if (light2.shadows != lightShadows)
			{
				light2.shadows = lightShadows;
			}
		}
	}

	public static string Diag()
	{
		Camera main = Camera.main;
		float num = 1E+09f;
		if ((bool)main)
		{
			foreach (Lamp lamp in lamps)
			{
				num = Mathf.Min(num, (lamp.p - main.transform.position).magnitude);
			}
		}
		float num2 = 0f;
		if (w != null)
		{
			float[] array = w;
			foreach (float b in array)
			{
				num2 = Mathf.Max(num2, b);
			}
		}
		return $"postes sel {sel.Count} slots {slotOf.Count}/{slots.Count} vivos {slots.Count((Light x) => x)} wmax {num2:0.00} mais perto {num:0} m alcance {maxDist:0} K {K}";
	}

	private static int FreeSlot()
	{
		HashSet<int> hashSet = new HashSet<int>(slotOf.Values);
		for (int i = 0; i < slots.Count; i++)
		{
			if (!hashSet.Contains(i))
			{
				return i;
			}
		}
		return -1;
	}
}
}
