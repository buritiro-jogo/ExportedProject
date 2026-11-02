using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public static class FBGiroflex
{
	public class Bar
	{
		public FBCar car;

		public Light red;

		public Light blue;

		public Renderer gr;

		public Renderer gb;

		public bool on = true;

		public readonly List<Renderer> ownR = new List<Renderer>();

		public readonly List<Renderer> ownB = new List<Renderer>();

		public bool litNow;

		public bool rNow;

		public bool bNow;

		public float phase;

		public Vector3 lr;

		public Vector3 lb;
	}

	private const int PAIRS = 4;

	private static Light[] pool;

	private static Material mR;

	private static Material mB;

	private static Material mOff;

	private static Material gR;

	private static Material gB;

	private static readonly List<Bar> bars = new List<Bar>();

	private static float t;

	private static readonly Regex LENS = new Regex("giroflex|lightbar|sirene|siren|strobo|cbf0507a", RegexOptions.IgnoreCase);

	private static readonly Dictionary<(Mesh, int), (Mesh l, Mesh r)> cut = new Dictionary<(Mesh, int), (Mesh, Mesh)>();

	private const float CYC = 0.9f;

	private static readonly (float a, float b)[] FR = new(float, float)[2]
	{
		(0f, 0.1f),
		(0.2f, 0.3f)
	};

	private static readonly (float a, float b)[] FB = new(float, float)[2]
	{
		(0.45f, 0.55f),
		(0.65f, 0.75f)
	};

	public static void Warm()
	{
		if (pool != null && Array.TrueForAll(pool, (Light l) => l))
		{
			return;
		}
		if (pool == null)
		{
			pool = new Light[8];
		}
		for (int i = 0; i < pool.Length; i++)
		{
			if (!pool[i])
			{
				GameObject gameObject = new GameObject("Giroflex_" + i);
				UnityEngine.Object.DontDestroyOnLoad(gameObject);
				Light light = gameObject.AddComponent<Light>();
				light.type = LightType.Point;
				light.range = 22f;
				light.intensity = 0f;
				light.shadows = LightShadows.None;
				light.color = ((i % 2 == 0) ? new Color(1f, 0.08f, 0.04f) : new Color(0.1f, 0.35f, 1f));
				gameObject.transform.position = Vector3.down * 500f;
				pool[i] = light;
			}
		}
		mR = M(new Color(1f, 0.12f, 0.08f));
		mB = M(new Color(0.15f, 0.4f, 1f));
		mOff = M(new Color(0.12f, 0.12f, 0.14f));
		gR = M(new Color(5f, 0.45f, 0.3f));
		gB = M(new Color(0.35f, 1.1f, 6f));
		static Material M(Color c)
		{
			Material material = new Material(FBShaders.Unlit);
			material.SetColor("_BaseColor", c);
			material.color = c;
			return material;
		}
	}

	private static (Mesh l, Mesh r) Cut(Mesh m, int sub)
	{
		if (cut.TryGetValue((m, sub), out (Mesh, Mesh) value))
		{
			return value;
		}
		if (!m.isReadable)
		{
			return cut[(m, sub)] = (null, null);
		}
		Vector3[] V = m.vertices;
		Vector3[] N = m.normals;
		int[] T = m.GetTriangles(sub);
		bool hasN = N != null && N.Length == V.Length;
		return cut[(m, sub)] = (Half(left: true), Half(left: false));
		Mesh Half(bool left)
		{
			Dictionary<int, int> dictionary = new Dictionary<int, int>();
			List<Vector3> list = new List<Vector3>();
			List<int> list2 = new List<int>();
			for (int i = 0; i + 2 < T.Length; i += 3)
			{
				if ((V[T[i]].x + V[T[i + 1]].x + V[T[i + 2]].x) / 3f < 0f == left)
				{
					for (int j = 0; j < 3; j++)
					{
						int num = T[i + j];
						if (!dictionary.TryGetValue(num, out var value2))
						{
							int num2 = (dictionary[num] = list.Count);
							value2 = num2;
							list.Add(V[num] + (hasN ? (N[num] * 0.003f) : Vector3.zero));
						}
						list2.Add(value2);
					}
				}
			}
			if (list2.Count == 0)
			{
				return null;
			}
			Mesh mesh = new Mesh
			{
				name = m.name + (left ? "_giro_E" : "_giro_D")
			};
			if (list.Count > 65000)
			{
				mesh.indexFormat = IndexFormat.UInt32;
			}
			mesh.SetVertices(list);
			mesh.SetTriangles(list2, 0);
			mesh.RecalculateBounds();
			return mesh;
		}
	}

	public static Bar Attach(FBCar car)
	{
		Warm();
		if (!car)
		{
			return null;
		}
		Bar bar = Find(car);
		if (bar != null)
		{
			return bar;
		}
		int num = -1;
		int i;
		for (i = 0; i < 4; i++)
		{
			if (num >= 0)
			{
				break;
			}
			if (!bars.Any((Bar o) => o.red == pool[i * 2]))
			{
				num = i;
			}
		}
		uint num2 = 2166136261u;
		string text = car.syncId ?? (car.name + car.transform.position.x.ToString("0"));
		foreach (char c in text)
		{
			num2 = (num2 ^ c) * 16777619;
		}
		Bar bar2 = new Bar
		{
			car = car,
			red = ((num >= 0) ? pool[num * 2] : null),
			blue = ((num >= 0) ? pool[num * 2 + 1] : null),
			phase = (float)(num2 % 1000) / 1000f * 0.9f
		};
		Bounds lb = car.lbox;
		float top = lb.max.y;
		float zc = lb.center.z + lb.size.z * 0.05f;
		Dictionary<string, (MeshRenderer, Mesh, int)> dictionary = new Dictionary<string, (MeshRenderer, Mesh, int)>();
		MeshRenderer[] componentsInChildren = car.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
		foreach (MeshRenderer meshRenderer in componentsInChildren)
		{
			MeshFilter component = meshRenderer.GetComponent<MeshFilter>();
			Mesh mesh = (component ? component.sharedMesh : null);
			if (!mesh)
			{
				continue;
			}
			Material[] sharedMaterials = meshRenderer.sharedMaterials;
			for (int k = 0; k < sharedMaterials.Length && k < mesh.subMeshCount; k++)
			{
				if ((bool)sharedMaterials[k] && LENS.IsMatch(sharedMaterials[k].name))
				{
					string name = sharedMaterials[k].name;
					if (!dictionary.TryGetValue(name, out var value) || mesh.vertexCount > value.Item2.vertexCount)
					{
						dictionary[name] = (meshRenderer, mesh, k);
					}
				}
			}
		}
		foreach (var value2 in dictionary.Values)
		{
			MeshRenderer item = value2.Item1;
			Mesh item2 = value2.Item2;
			int item3 = value2.Item3;
			(Mesh l, Mesh r) tuple = Cut(item2, item3);
			Mesh item4 = tuple.l;
			Mesh item5 = tuple.r;
			(Mesh, bool)[] array = new(Mesh, bool)[2]
			{
				(item4, true),
				(item5, false)
			};
			for (int j = 0; j < array.Length; j++)
			{
				var (mesh2, flag) = array[j];
				if ((bool)mesh2)
				{
					GameObject gameObject = new GameObject(flag ? "giroflex_vermelho" : "giroflex_azul");
					gameObject.transform.SetParent(item.transform, worldPositionStays: false);
					gameObject.layer = item.gameObject.layer;
					gameObject.AddComponent<MeshFilter>().sharedMesh = mesh2;
					MeshRenderer meshRenderer2 = gameObject.AddComponent<MeshRenderer>();
					meshRenderer2.sharedMaterial = (flag ? gR : gB);
					meshRenderer2.shadowCastingMode = ShadowCastingMode.Off;
					meshRenderer2.receiveShadows = false;
					meshRenderer2.enabled = false;
					(flag ? bar2.ownR : bar2.ownB).Add(meshRenderer2);
				}
			}
		}
		if (bar2.ownR.Count + bar2.ownB.Count <= 0 && dictionary.Count <= 0 && !car.GetComponentsInChildren<Transform>(includeInactive: true).Any((Transform x) => LENS.IsMatch(x.name)))
		{
			bar2.gr = Block(lb.size.x * 0.17f, "giro_vermelho");
			bar2.gb = Block((0f - lb.size.x) * 0.17f, "giro_azul");
		}
		bar2.lr = new Vector3(lb.size.x * 0.2f, top + 0.25f, zc);
		bar2.lb = new Vector3((0f - lb.size.x) * 0.2f, top + 0.25f, zc);
		bars.Add(bar2);
		return bar2;
		Renderer Block(float x, string n)
		{
			GameObject gameObject2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
			UnityEngine.Object.Destroy(gameObject2.GetComponent<Collider>());
			gameObject2.name = n;
			gameObject2.transform.SetParent(car.transform, worldPositionStays: false);
			gameObject2.transform.localPosition = new Vector3(x, top + 0.04f, zc);
			gameObject2.transform.localScale = new Vector3(lb.size.x * 0.2f, 0.07f, 0.16f);
			gameObject2.layer = 11;
			Renderer component2 = gameObject2.GetComponent<Renderer>();
			component2.sharedMaterial = mOff;
			component2.shadowCastingMode = ShadowCastingMode.Off;
			return component2;
		}
	}

	public static void Detach(Bar b)
	{
		if (b == null)
		{
			return;
		}
		bars.Remove(b);
		foreach (Renderer item in b.ownR.Concat(b.ownB))
		{
			if ((bool)item)
			{
				UnityEngine.Object.Destroy(item.gameObject);
			}
		}
		Light[] array = new Light[2] { b.red, b.blue };
		foreach (Light light in array)
		{
			if ((bool)light)
			{
				light.intensity = 0f;
				light.transform.position = Vector3.down * 500f;
			}
		}
	}

	public static Bar Find(FBCar car)
	{
		return bars.FirstOrDefault((Bar b) => b.car == car);
	}

	private static bool Flash((float a, float b)[] w, float t0, float t1)
	{
		if (t1 - t0 >= 0.9f)
		{
			return true;
		}
		float num = t0 % 0.9f;
		float num2 = t1 % 0.9f;
		if (num < 0f)
		{
			num += 0.9f;
		}
		if (num2 < 0f)
		{
			num2 += 0.9f;
		}
		for (int i = 0; i < w.Length; i++)
		{
			var (num3, num4) = w[i];
			bool num5;
			if (!(num2 >= num))
			{
				if (num4 > num)
				{
					goto IL_007a;
				}
				num5 = num3 < num2;
			}
			else
			{
				if (!(num3 < num2))
				{
					continue;
				}
				num5 = num4 > num;
			}
			if (!num5)
			{
				continue;
			}
			goto IL_007a;
			IL_007a:
			return true;
		}
		return false;
	}

	public static void Tick(float dt)
	{
		FBGiroflex.t += dt;
		float num = ((!FBQuality.Mobile) ? 1 : 0);
		foreach (Bar bar in bars)
		{
			if (!bar.car)
			{
				continue;
			}
			bool on = bar.on;
			float num2 = FBGiroflex.t + bar.phase;
			float t = num2 - Mathf.Max(dt, 0.0001f);
			bool flag = on && Flash(FR, t, num2);
			bool flag2 = on && Flash(FB, t, num2);
			Transform transform = bar.car.transform;
			if ((bool)bar.red)
			{
				bar.red.transform.position = transform.TransformPoint(bar.lr);
				bar.red.intensity = (flag ? (4.5f * num) : 0f);
			}
			if ((bool)bar.blue)
			{
				bar.blue.transform.position = transform.TransformPoint(bar.lb);
				bar.blue.intensity = (flag2 ? (4.5f * num) : 0f);
			}
			if ((bool)bar.gr)
			{
				bar.gr.sharedMaterial = (flag ? mR : mOff);
			}
			if ((bool)bar.gb)
			{
				bar.gb.sharedMaterial = (flag2 ? mB : mOff);
			}
			foreach (Renderer item in bar.ownR)
			{
				if ((bool)item)
				{
					item.enabled = flag;
				}
			}
			foreach (Renderer item2 in bar.ownB)
			{
				if ((bool)item2)
				{
					item2.enabled = flag2;
				}
			}
			bar.litNow = flag || flag2;
			bar.rNow = flag;
			bar.bNow = flag2;
		}
		foreach (Bar bar2 in bars)
		{
			if ((bool)bar2.car)
			{
				continue;
			}
			Light[] array = new Light[2] { bar2.red, bar2.blue };
			foreach (Light light in array)
			{
				if ((bool)light)
				{
					light.intensity = 0f;
					light.transform.position = Vector3.down * 500f;
				}
			}
		}
		bars.RemoveAll((Bar b) => !b.car);
	}

	public static bool Alive(Bar b)
	{
		if (b != null)
		{
			return bars.Contains(b);
		}
		return false;
	}

	public static void Reset()
	{
		foreach (Bar item in bars.ToList())
		{
			Detach(item);
		}
		bars.Clear();
	}

	public static bool Lit(FBCar u)
	{
		return bars.Any((Bar b) => b.car == u && b.litNow);
	}

	public static int OwnCount(FBCar u)
	{
		return bars.Where((Bar b) => b.car == u).Sum((Bar b) => b.ownR.Count + b.ownB.Count);
	}
}
}
