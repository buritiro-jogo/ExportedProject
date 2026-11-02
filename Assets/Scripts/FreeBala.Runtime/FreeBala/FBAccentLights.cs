using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public static class FBAccentLights
{
	private enum Kind
	{
		Fonte,
		Portal,
		PortalVerde,
		Palmeira,
		Letreiro
	}

	private struct A
	{
		public Light l;

		public Kind k;

		public float i;

		public Vector3 cell;
	}

	private static readonly List<A> list = new List<A>();

	private static Transform root;

	private static bool fontes;

	private static readonly Color[] PAL = new Color[6]
	{
		new Color(0.15f, 0.35f, 1f),
		new Color(0.1f, 0.95f, 0.45f),
		new Color(1f, 0.2f, 0.75f),
		new Color(1f, 0.18f, 0.12f),
		new Color(1f, 0.62f, 0.12f),
		new Color(0.1f, 0.85f, 1f)
	};

	private static Light Make(Transform parent, string name, LightType t, Vector3 p, Quaternion r, float range, float angle = 60f, float inner = 30f)
	{
		Light light = new GameObject(name).AddComponent<Light>();
		light.transform.SetParent(parent);
		light.transform.SetPositionAndRotation(p, r);
		light.type = t;
		light.range = range;
		if (t == LightType.Spot)
		{
			light.spotAngle = angle;
			light.innerSpotAngle = inner;
		}
		light.shadows = LightShadows.None;
		light.intensity = 0f;
		light.enabled = false;
		light.renderMode = LightRenderMode.ForcePixel;
		return light;
	}

	private static Renderer[] MapRenderers()
	{
		return (from r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
			where r.gameObject.layer == 8 && r.enabled
			select r).ToArray();
	}

	private static void BuildFountains()
	{
		if (fontes || !root || FBWater.JetsScene != SceneManager.GetActiveScene() || FBWater.LastJets.Count == 0)
		{
			return;
		}
		fontes = true;
		bool flag = FBQuality.Level >= FBQLevel.Alto;
		Dictionary<Vector2Int, (Vector3, int, float)> dictionary = new Dictionary<Vector2Int, (Vector3, int, float)>();
		foreach (FBWater.Jet lastJet in FBWater.LastJets)
		{
			Vector2Int key = new Vector2Int(Mathf.FloorToInt(lastJet.p.x / 6f), Mathf.FloorToInt(lastJet.p.z / 6f));
			dictionary.TryGetValue(key, out var value);
			dictionary[key] = (value.Item1 + lastJet.p, value.Item2 + 1, (value.Item2 == 0) ? lastJet.p.y : Mathf.Min(value.Item3, lastJet.p.y));
		}
		foreach (KeyValuePair<Vector2Int, (Vector3, int, float)> item in dictionary.Take(flag ? 40 : 16))
		{
			Vector3 vector = item.Value.Item1 / item.Value.Item2;
			Light light = Make(root, "FonteRefletor", LightType.Point, new Vector3(vector.x, item.Value.Item3 + 0.9f, vector.z), Quaternion.identity, 8f);
			list.Add(new A
			{
				l = light,
				k = Kind.Fonte,
				i = 6f,
				cell = new Vector3(item.Key.x, 0f, item.Key.y)
			});
			FBVolumetric.Sources.Add(light);
		}
		Debug.Log($"[FreeBala][luz] fontes: {dictionary.Count} grupos de jatos, {list.Count((A a) => a.k == Kind.Fonte)} refletores coloridos");
	}

	public static void Build(Transform parent, string map)
	{
		list.Clear();
		root = null;
		if (FBQuality.Mobile)
		{
			return;
		}
		bool flag = FBQuality.Level >= FBQLevel.Alto;
		Renderer[] source = MapRenderers();
		root = parent;
		fontes = false;
		BuildFountains();
		using (IEnumerator<Renderer> enumerator = source.Where((Renderer r) => r.name.Contains("Portal_Milenio")).GetEnumerator())
		{
			if (enumerator.MoveNext())
			{
				Bounds bounds = enumerator.Current.bounds;
				bool num = bounds.size.x > bounds.size.z;
				Vector3 vector = (num ? Vector3.right : Vector3.forward);
				float num2 = (num ? bounds.extents.x : bounds.extents.z) - 0.9f;
				float[] array = new float[2] { -1f, 1f };
				foreach (float num3 in array)
				{
					Vector3 vector2 = new Vector3(bounds.center.x, bounds.min.y + 0.25f, bounds.center.z) + vector * (num2 * num3);
					Vector3 vector3 = Vector3.Cross(Vector3.up, vector);
					float[] array2 = new float[2] { -1f, 1f };
					foreach (float num4 in array2)
					{
						Light light = Make(parent, "PortalRefletor", LightType.Spot, vector2 + vector3 * (1.6f * num4) - vector * (0.6f * num3), Quaternion.LookRotation((vector2 + Vector3.up * bounds.size.y * 0.9f - (vector2 + vector3 * (1.6f * num4))).normalized), bounds.size.y * 1.4f, 34f, 16f);
						light.useColorTemperature = true;
						light.colorTemperature = 3200f;
						if (flag)
						{
							light.shadows = LightShadows.Soft;
							light.shadowNearPlane = 0.3f;
							FBLightUtil.MediumShadows(light);
						}
						list.Add(new A
						{
							l = light,
							k = Kind.Portal,
							i = 70f
						});
					}
					Light light2 = Make(parent, "PortalVerde", LightType.Point, vector2 + Vector3.up * 0.3f - vector * (1.2f * num3), Quaternion.identity, 5f);
					light2.color = new Color(0.2f, 1f, 0.45f);
					list.Add(new A
					{
						l = light2,
						k = Kind.PortalVerde,
						i = 5f
					});
				}
			}
		}
		if (flag)
		{
			foreach (Renderer item in source.Where((Renderer r) => r.name.Contains("Palmeira")))
			{
				Bounds bounds2 = item.bounds;
				if (!(bounds2.size.y < 3f))
				{
					Vector3 vector4 = new Vector3(bounds2.center.x, bounds2.min.y + 0.2f, bounds2.center.z) + new Vector3(0.7f, 0f, 0.4f);
					Light light3 = Make(parent, "PalmeiraRefletor", LightType.Spot, vector4, Quaternion.LookRotation((new Vector3(bounds2.center.x, bounds2.max.y - 1f, bounds2.center.z) - vector4).normalized), bounds2.size.y * 1.15f, 46f, 18f);
					light3.color = new Color(0.78f, 1f, 0.82f);
					list.Add(new A
					{
						l = light3,
						k = Kind.Palmeira,
						i = 16f
					});
				}
			}
		}
		using (IEnumerator<Renderer> enumerator = source.Where((Renderer r) => r.name.StartsWith("PA_Letreiro_Base")).GetEnumerator())
		{
			if (enumerator.MoveNext())
			{
				Bounds bounds3 = enumerator.Current.bounds;
				Vector3 left = Vector3.left;
				Vector3 vector5 = ((bounds3.size.z > bounds3.size.x) ? Vector3.forward : Vector3.right);
				Vector3 vector6 = bounds3.center + Vector3.up * 2.9f;
				float[] array = new float[2] { -1f, 1f };
				foreach (float num5 in array)
				{
					Vector3 vector7 = new Vector3(bounds3.center.x, bounds3.min.y + 1.6f, bounds3.center.z) + left * 10f + vector5 * (num5 * Mathf.Max(2.5f, ((vector5 == Vector3.forward) ? bounds3.extents.z : bounds3.extents.x) * 0.45f));
					Light light4 = Make(parent, "LetreiroRefletor", LightType.Spot, vector7, Quaternion.LookRotation((vector6 - vector7).normalized), 22f, 55f);
					light4.useColorTemperature = true;
					light4.colorTemperature = 4800f;
					list.Add(new A
					{
						l = light4,
						k = Kind.Letreiro,
						i = 24f
					});
				}
			}
		}
		Debug.Log($"[FreeBala][luz] destaque: {list.Count((A a) => a.k == Kind.Portal)} portal, {list.Count((A a) => a.k == Kind.Palmeira)} palmeiras, {list.Count((A a) => a.k == Kind.Letreiro)} letreiro");
	}

	public static Color Palette(float h)
	{
		h -= Mathf.Floor(h);
		float num = h * 6f;
		int num2 = Mathf.FloorToInt(num) % 6;
		float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 1f, num - Mathf.Floor(num)));
		return Color.Lerp(PAL[num2], PAL[(num2 + 1) % 6], t);
	}

	public static void Tick(float nightK)
	{
		float num = Mathf.InverseLerp(0.4f, 0.95f, nightK);
		if (!fontes && (bool)root)
		{
			BuildFountains();
		}
		float timeSinceLevelLoad = Time.timeSinceLevelLoad;
		foreach (A item in list)
		{
			if ((bool)item.l)
			{
				item.l.intensity = item.i * num;
				item.l.enabled = num > 0.01f;
				if (item.k == Kind.Fonte && item.l.enabled)
				{
					item.l.color = Palette(item.cell.x * 0.37f + item.cell.z * 0.23f + timeSinceLevelLoad * 0.025f);
				}
			}
		}
	}
}
}
