using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public static class FBCarDamage
{
	private class Glass
	{
		public int sub = -1;

		public int crackSub = -1;

		public int[] orig;

		public List<int[]> panes;

		public List<Vector3> centers;

		public List<int> state;
	}

	private class St
	{
		public float hp = 100f;

		public float lastDent = -9f;

		public float smokeT;

		public float lastShot = -9f;

		public float pull;

		public float front;

		public float rear;

		public float left;

		public float right;

		public bool dead;

		public int light;

		public int moderate;

		public int severe;

		public readonly Dictionary<Mesh, (Vector3[] p, Vector3[] n)> rest = new Dictionary<Mesh, (Vector3[], Vector3[])>();

		public readonly HashSet<Mesh> own = new HashSet<Mesh>();

		public readonly Dictionary<MeshFilter, (Mesh m, Glass g)> glass = new Dictionary<MeshFilter, (Mesh, Glass)>();

		public Vector3 shove;

		public float spin;
	}

	private class Runner : MonoBehaviour
	{
		private static readonly int __fbt_Update = FBTempo.Id("FBCarDamage.Runner.Update");

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
			float deltaTime = Time.deltaTime;
			List<FBCar> list = null;
			foreach (KeyValuePair<FBCar, St> item in all)
			{
				FBCar key = item.Key;
				St value = item.Value;
				if (!key)
				{
					(list ?? (list = new List<FBCar>())).Add(key);
					continue;
				}
				if (value.hp < 45f && (value.smokeT -= deltaTime) <= 0f)
				{
					value.smokeT = ((value.hp < 20f) ? 0.07f : 0.16f);
					Bounds lbox = key.lbox;
					Transform transform = key.transform;
					Vector3 p = transform.TransformPoint(new Vector3(lbox.center.x + (UnityEngine.Random.value - 0.5f) * lbox.size.x * 0.5f, lbox.max.y * 0.82f, lbox.max.z - 0.5f - UnityEngine.Random.value * 0.5f));
					Vector3 vel = Vector3.up * (0.7f + UnityEngine.Random.value * 0.5f) + transform.forward * (key.v * 0.55f) + new Vector3(UnityEngine.Random.value - 0.5f, 0f, UnityEngine.Random.value - 0.5f) * 0.3f;
					float num = ((value.hp < 20f) ? 0.16f : 0.62f);
					FBCarFX.Puff(p, vel, new Color(num, num, num), (value.hp < 20f) ? 0.5f : 0.3f, 0.4f, (value.hp < 20f) ? 2.6f : 1.9f, 1.8f + UnityEngine.Random.value * 0.8f);
				}
				if (value.shove.sqrMagnitude > 0.01f || Mathf.Abs(value.spin) > 0.01f)
				{
					Vector3 vector = value.shove * deltaTime;
					Transform transform2 = key.transform;
					key.SetColliders(on: false);
					bool num2 = vector.sqrMagnitude > 1E-06f && Physics.BoxCast(transform2.TransformPoint(key.lbox.center) + Vector3.up * 0.2f, key.lbox.extents * key.scale * 0.85f, vector.normalized, transform2.rotation, vector.magnitude + 0.05f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore);
					key.SetColliders(on: true);
					if (num2)
					{
						value.shove = Vector3.zero;
					}
					else
					{
						transform2.position += vector;
					}
					transform2.rotation = Quaternion.Euler(0f, value.spin * deltaTime, 0f) * transform2.rotation;
					key.yaw = (0f - transform2.eulerAngles.y) * ((float)Math.PI / 180f);
					float magnitude = value.shove.magnitude;
					value.shove = ((magnitude > 0f) ? (value.shove * Mathf.Max(0f, magnitude - 6f * deltaTime) / magnitude) : Vector3.zero);
					value.spin = Mathf.MoveTowards(value.spin, 0f, 120f * deltaTime);
					key.SetColliders(on: false);
					float? num3 = FBWorld.GroundY(transform2.position.x, transform2.position.y + 1f, transform2.position.z, 0.3f, 3f);
					if (num3.HasValue)
					{
						float valueOrDefault = num3.GetValueOrDefault();
						transform2.position = new Vector3(transform2.position.x, valueOrDefault, transform2.position.z);
					}
					key.SetColliders(on: true);
				}
			}
			if (list == null)
			{
				return;
			}
			foreach (FBCar item2 in list)
			{
				all.Remove(item2);
			}
		}
	}

	private static readonly Dictionary<FBCar, St> all = new Dictionary<FBCar, St>();

	private static Material crackMat;

	private static Runner runner;

	private static float evT;

	private static St Get(FBCar c)
	{
		if (!all.TryGetValue(c, out var value))
		{
			value = (all[c] = new St());
		}
		if (!runner)
		{
			GameObject gameObject = new GameObject("FB_DanoVeiculos");
			UnityEngine.Object.DontDestroyOnLoad(gameObject);
			runner = gameObject.AddComponent<Runner>();
		}
		return value;
	}

	public static float Hp(FBCar c)
	{
		if (!c || !all.TryGetValue(c, out var value))
		{
			return 100f;
		}
		return value.hp;
	}

	public static bool EngineDead(FBCar c)
	{
		if ((bool)c && all.TryGetValue(c, out var value))
		{
			return value.dead;
		}
		return false;
	}

	public static float PowerK(FBCar c)
	{
		if (!c || !all.TryGetValue(c, out var value))
		{
			return 1f;
		}
		if (!value.dead)
		{
			return Mathf.Lerp(0.55f, 1f, Mathf.InverseLerp(12f, 70f, value.hp));
		}
		return 0f;
	}

	public static float TopK(FBCar c)
	{
		if (!c || !all.TryGetValue(c, out var value))
		{
			return 1f;
		}
		return Mathf.Lerp(0.7f, 1f, Mathf.InverseLerp(10f, 80f, value.hp));
	}

	public static float GripK(FBCar c)
	{
		if (!c || !all.TryGetValue(c, out var value))
		{
			return 1f;
		}
		return 1f - Mathf.Clamp01(value.front / 100f) * 0.2f;
	}

	public static float Pull(FBCar c)
	{
		if (!c || !all.TryGetValue(c, out var value))
		{
			return 0f;
		}
		return value.pull;
	}

	public static float Resist(FBCar c)
	{
		string text = (c ? (c.id ?? "") : "");
		if (text.Contains("Caveir"))
		{
			return 0.25f;
		}
		if ((bool)c && c.heavy)
		{
			return 0.6f;
		}
		if (text.Contains("Viatura"))
		{
			return 0.8f;
		}
		if (!c || !FBCarSound.Sporty(c.engineProf ?? FBCarSound.ProfileFor(c.id, c)))
		{
			return 1f;
		}
		return 1.15f;
	}

	public static string Severity(float imp)
	{
		if (!(imp < 9f))
		{
			if (!(imp < 17f))
			{
				return "grave";
			}
			return "moderada";
		}
		return "leve";
	}

	public static Vector3[] RestOf(Mesh m)
	{
		foreach (St value2 in all.Values)
		{
			if (value2.rest.TryGetValue(m, out (Vector3[], Vector3[]) value))
			{
				return value.Item1;
			}
		}
		return null;
	}

	public static void Reset()
	{
		all.Clear();
	}

	public static void SetHpTest(FBCar c, float hp)
	{
		if ((bool)c)
		{
			Get(c).hp = hp;
		}
	}

	public static string DebugInfo(FBCar car)
	{
		if (!car || !all.TryGetValue(car, out var value))
		{
			return "sem dano";
		}
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		foreach (KeyValuePair<MeshFilter, (Mesh, Glass)> item2 in value.glass)
		{
			Glass item = item2.Value.Item2;
			if (item == null)
			{
				continue;
			}
			num3 += item.panes.Count;
			foreach (int item3 in item.state)
			{
				if (item3 == 1)
				{
					num++;
				}
				else if (item3 >= 2)
				{
					num2++;
				}
			}
		}
		return string.Format("vida {0:0} · batidas {1} leves/{2} moderadas/{3} graves · zonas F{4:0} T{5:0} E{6:0} D{7:0} · puxa {8:+0.00;-0.00} · potência {9:0.00} máx {10:0.00} aderência {11:0.00} · malhas amassadas {12} · vidros {13} painéis ({14} trincados, {15} quebrados) · peças soltas {16} · motor {17}", value.hp, value.light, value.moderate, value.severe, value.front, value.rear, value.left, value.right, value.pull, PowerK(car), TopK(car), GripK(car), value.rest.Count, num3, num, num2, FBDoors.GoneCount(car), value.dead ? "morto" : "ok");
	}

	public static void OnImpact(FBCar car, Vector3 pW, Vector3 nW, float imp)
	{
		if (imp > 9f && Time.time - evT > 4f)
		{
			evT = Time.time;
			FBNpcRotina.Event("acidente", pW, 34f);
		}
		if (!car || car.kind == "moto" || imp < 3f)
		{
			return;
		}
		St st = Get(car);
		if (Time.time - st.lastDent < 0.18f)
		{
			return;
		}
		st.lastDent = Time.time;
		imp *= Resist(car);
		if (imp < 3f)
		{
			return;
		}
		string text = Severity(imp);
		if (text == "leve")
		{
			st.light++;
		}
		else if (text == "moderada")
		{
			st.moderate++;
		}
		else
		{
			st.severe++;
		}
		float num = Mathf.Clamp01((imp - 3f) / 22f);
		Dent(car, st, pW, nW, 0.035f + 0.17f * num, 0.32f + 0.6f * num);
		if (imp > 6f)
		{
			FBCarLights.BreakNear(car, pW, 0.45f + 0.45f * num);
		}
		if (imp > 11f)
		{
			GlassHit(car, st, pW, (!(imp > 18f)) ? 1 : 2, 1.3f + num, -nW);
		}
		Vector3 vector = car.transform.InverseTransformPoint(pW);
		Bounds lbox = car.lbox;
		float num2 = (vector.z - lbox.center.z) / Mathf.Max(0.1f, lbox.extents.z);
		float num3 = (vector.x - lbox.center.x) / Mathf.Max(0.1f, lbox.extents.x);
		float num4 = num * 40f;
		if (num2 > 0.55f)
		{
			st.front += num4;
		}
		else if (num2 < -0.55f)
		{
			st.rear += num4;
		}
		else if (num3 < 0f)
		{
			st.left += num4;
		}
		else
		{
			st.right += num4;
		}
		if (num2 > 0.25f && Mathf.Abs(num3) > 0.35f && imp > 8f)
		{
			st.pull = Mathf.Clamp(st.pull + (float)((num3 < 0f) ? 1 : (-1)) * num * 0.07f, -0.2f, 0.2f);
		}
		if (imp > 12f)
		{
			int num5 = ((num2 > 0.55f) ? 5 : ((num2 < -0.55f) ? 4 : ((Mathf.Abs(num3) > 0.5f) ? (((!(num3 < 0f)) ? 1 : 0) + ((num2 < -0.05f) ? 2 : 0)) : (-1))));
			if (num5 >= 0)
			{
				float num6 = FBDoors.BentOf(car, num5);
				FBDoors.Bend(car, num5, num5 switch
				{
					4 => Mathf.Lerp(0.05f, 0.22f, num), 
					5 => Mathf.Lerp(0.08f, 0.3f, num), 
					_ => Mathf.Lerp(0.04f, 0.14f, num), 
				});
				if (imp > 19f || (imp > 14f && num6 > 0.1f))
				{
					FBDoors.Detach(car, num5, car.transform.forward * car.v * 0.6f - nW * Mathf.Min(6f, imp * 0.25f) + Vector3.up * 1.5f);
				}
			}
		}
		Hurt(car, st, Mathf.Pow(Mathf.Max(0f, imp - 4f), 2f) * 0.3f);
	}

	public static void OnShot(FBCar car, Vector3 pW, Vector3 dirW, float dmg)
	{
		if ((bool)car && !(car.kind == "moto"))
		{
			St s = Get(car);
			FBCarLights.BreakNear(car, pW, 0.2f);
			GlassHit(car, s, pW, 1, 0.55f, dirW);
			Hurt(car, s, dmg * 0.12f);
		}
	}

	private static void Hurt(FBCar car, St s, float d)
	{
		if (d <= 0f || s.dead)
		{
			return;
		}
		s.hp = Mathf.Max(0f, s.hp - d);
		if (s.hp <= 0f)
		{
			s.dead = true;
			FBAudio.PlayAt("reb_carimpact2", car.transform.position, 0.5f, 6f);
			if ((bool)car.driver && car.driver.isPlayer)
			{
				FBHUD.I?.Center("MOTOR QUEBRADO · LEVE NA OFICINA");
			}
		}
	}

	public static void Shove(FBCar car, Vector3 v, float spin)
	{
		if ((bool)car && !car.driver && car.remoteDriver < 0)
		{
			St st = Get(car);
			v.y = 0f;
			st.shove += v;
			st.spin += spin;
		}
	}

	private static float H01(int i)
	{
		int num = i * -1640531535;
		return (float)(((uint)num ^ ((uint)num >> 15)) & 0x3FFu) / 1023f;
	}

	private static void Dent(FBCar car, St s, Vector3 pW, Vector3 dirW, float depth, float radius)
	{
		if (!car.shell)
		{
			return;
		}
		Bounds bounds = new Bounds(pW, Vector3.one * radius * 2.2f);
		MeshFilter[] componentsInChildren = car.shell.GetComponentsInChildren<MeshFilter>(includeInactive: true);
		foreach (MeshFilter meshFilter in componentsInChildren)
		{
			MeshRenderer component = meshFilter.GetComponent<MeshRenderer>();
			Mesh mesh = meshFilter.sharedMesh;
			if (!component || !mesh || !mesh.isReadable)
			{
				continue;
			}
			string name = meshFilter.name;
			if (name.StartsWith("lente_") || name.StartsWith("brilho") || name.StartsWith("wheel") || name.Contains("roda") || name == "sombra_contato" || name.StartsWith("body_lod2") || !component.bounds.Intersects(bounds))
			{
				continue;
			}
			if (!s.own.Contains(mesh))
			{
				Mesh mesh2 = UnityEngine.Object.Instantiate(mesh);
				mesh2.name = mesh.name;
				meshFilter.sharedMesh = mesh2;
				mesh = mesh2;
				s.own.Add(mesh);
				RebindGlass(s, meshFilter);
			}
			if (!s.rest.TryGetValue(mesh, out (Vector3[], Vector3[]) value))
			{
				value = (s.rest[mesh] = (mesh.vertices, mesh.normals));
			}
			Matrix4x4 worldToLocalMatrix = meshFilter.transform.worldToLocalMatrix;
			Vector3 vector = worldToLocalMatrix.MultiplyPoint3x4(pW);
			Vector3 vector2 = worldToLocalMatrix.MultiplyVector(dirW);
			float magnitude = vector2.magnitude;
			if (magnitude < 1E-06f)
			{
				continue;
			}
			vector2 /= magnitude;
			float num = radius * magnitude;
			float num2 = 0.22f * magnitude;
			float num3 = depth * magnitude;
			Vector3[] vertices = mesh.vertices;
			float[] array = new float[vertices.Length];
			bool flag = false;
			for (int j = 0; j < vertices.Length; j++)
			{
				float sqrMagnitude = (vertices[j] - vector).sqrMagnitude;
				if (!(sqrMagnitude > num * num))
				{
					float num4 = 1f - Mathf.Sqrt(sqrMagnitude) / num;
					num4 *= num4;
					Vector3 vector3 = vertices[j] + vector2 * (num3 * num4 * (0.7f + 0.6f * H01(j)));
					Vector3 vector4 = vector3 - value.Item1[j];
					if (vector4.sqrMagnitude > num2 * num2)
					{
						vector3 = value.Item1[j] + vector4.normalized * num2;
					}
					vertices[j] = vector3;
					array[j] = num4;
					flag = true;
				}
			}
			if (flag)
			{
				mesh.vertices = vertices;
				LocalNormals(mesh, vertices, array);
				mesh.RecalculateBounds();
			}
		}
	}

	private static void LocalNormals(Mesh m, Vector3[] V, float[] w)
	{
		Vector3[] normals = m.normals;
		if (normals == null || normals.Length != V.Length)
		{
			m.RecalculateNormals();
			return;
		}
		Dictionary<Vector3Int, Vector3> dictionary = new Dictionary<Vector3Int, Vector3>();
		for (int i = 0; i < m.subMeshCount; i++)
		{
			int[] triangles = m.GetTriangles(i);
			for (int j = 0; j + 2 < triangles.Length; j += 3)
			{
				int num = triangles[j];
				int num2 = triangles[j + 1];
				int num3 = triangles[j + 2];
				if (!(w[num] <= 0f) || !(w[num2] <= 0f) || !(w[num3] <= 0f))
				{
					Vector3 vector = Vector3.Cross(V[num2] - V[num], V[num3] - V[num]);
					int[] array = new int[3] { num, num2, num3 };
					foreach (int num4 in array)
					{
						Vector3Int key = K(V[num4]);
						dictionary[key] = (dictionary.TryGetValue(key, out var value) ? value : Vector3.zero) + vector;
					}
				}
			}
		}
		for (int l = 0; l < V.Length; l++)
		{
			if (!(w[l] <= 0f) && dictionary.TryGetValue(K(V[l]), out var value2) && !(value2.sqrMagnitude < 1E-12f))
			{
				value2.Normalize();
				if (Vector3.Dot(value2, normals[l]) < 0f)
				{
					value2 = -value2;
				}
				normals[l] = Vector3.Slerp(normals[l], value2, Mathf.Clamp01(w[l] * 1.6f + 0.25f)).normalized;
			}
		}
		m.normals = normals;
		static Vector3Int K(Vector3 p)
		{
			return new Vector3Int(Mathf.RoundToInt(p.x * 2000f), Mathf.RoundToInt(p.y * 2000f), Mathf.RoundToInt(p.z * 2000f));
		}
	}

	private static Material CrackMat(Material glass)
	{
		if ((bool)crackMat)
		{
			return crackMat;
		}
		crackMat = (glass ? new Material(glass)
		{
			name = "vidro_estilhacado"
		} : new Material(FBShaders.Lit));
		if (crackMat.HasProperty("_BaseColor"))
		{
			Color color = crackMat.GetColor("_BaseColor");
			crackMat.SetColor("_BaseColor", new Color(0.78f, 0.82f, 0.84f, Mathf.Max(0.82f, color.a)));
		}
		if (crackMat.HasProperty("_Smoothness"))
		{
			crackMat.SetFloat("_Smoothness", 0.35f);
		}
		return crackMat;
	}

	private static bool IsGlass(Material m)
	{
		if ((bool)m && (m.name.Contains("vidro") || m.name.Contains("glass")))
		{
			return !m.name.Contains("estilha");
		}
		return false;
	}

	private static void RebindGlass(St s, MeshFilter mf)
	{
		s.glass.Remove(mf);
	}

	private static Glass GlassOf(St s, MeshFilter mf)
	{
		Mesh mesh = mf.sharedMesh;
		if (s.glass.TryGetValue(mf, out (Mesh, Glass) value) && value.Item1 == mesh)
		{
			return value.Item2;
		}
		Material[] sharedMaterials = mf.GetComponent<MeshRenderer>().sharedMaterials;
		Glass glass = new Glass();
		for (int i = 0; i < Mathf.Min(sharedMaterials.Length, mesh.subMeshCount); i++)
		{
			if (IsGlass(sharedMaterials[i]))
			{
				glass.sub = i;
				break;
			}
		}
		for (int j = 0; j < Mathf.Min(sharedMaterials.Length, mesh.subMeshCount); j++)
		{
			if ((bool)sharedMaterials[j] && sharedMaterials[j].name.StartsWith("vidro_estilhacado"))
			{
				glass.crackSub = j;
			}
		}
		if (glass.sub < 0)
		{
			s.glass[mf] = (mesh, null);
			return null;
		}
		if (!s.own.Contains(mesh))
		{
			Mesh mesh2 = UnityEngine.Object.Instantiate(mesh);
			mesh2.name = mesh.name;
			mf.sharedMesh = mesh2;
			mesh = mesh2;
			s.own.Add(mesh);
		}
		List<int> list = new List<int>(mesh.GetTriangles(glass.sub));
		int count = list.Count;
		if (glass.crackSub >= 0)
		{
			list.AddRange(mesh.GetTriangles(glass.crackSub));
		}
		Glass glass2 = glass;
		if (glass2.orig == null)
		{
			glass2.orig = mesh.GetTriangles(glass.sub);
		}
		Vector3[] V = mesh.vertices;
		Dictionary<Vector3Int, int> pid = new Dictionary<Vector3Int, int>();
		List<int> par = new List<int>();
		int[,] array = new int[list.Count / 3, 3];
		for (int k = 0; k < list.Count / 3; k++)
		{
			int num = Id(list[k * 3]);
			int num2 = Id(list[k * 3 + 1]);
			int num3 = Id(list[k * 3 + 2]);
			array[k, 0] = num;
			array[k, 1] = num2;
			array[k, 2] = num3;
			par[F(num2)] = F(num);
			par[F(num3)] = F(num);
		}
		Dictionary<int, List<int>> dictionary = new Dictionary<int, List<int>>();
		HashSet<int> hashSet = new HashSet<int>();
		for (int l = 0; l < list.Count / 3; l++)
		{
			int num4 = F(array[l, 0]);
			if (!dictionary.TryGetValue(num4, out var value2))
			{
				value2 = (dictionary[num4] = new List<int>());
			}
			value2.Add(list[l * 3]);
			value2.Add(list[l * 3 + 1]);
			value2.Add(list[l * 3 + 2]);
			if (l * 3 >= count)
			{
				hashSet.Add(num4);
			}
		}
		glass.panes = new List<int[]>();
		glass.centers = new List<Vector3>();
		glass.state = new List<int>();
		foreach (KeyValuePair<int, List<int>> item in dictionary)
		{
			List<int> value3 = item.Value;
			Vector3 zero = Vector3.zero;
			foreach (int item2 in value3)
			{
				zero += V[item2];
			}
			zero /= (float)value3.Count;
			glass.panes.Add(value3.ToArray());
			glass.centers.Add(zero);
			glass.state.Add(hashSet.Contains(item.Key) ? 1 : 0);
		}
		s.glass[mf] = (mesh, glass);
		return glass;
		int F(int x)
		{
			while (par[x] != x)
			{
				int num6 = (par[x] = par[par[x]]);
				x = num6;
			}
			return x;
		}
		int Id(int v)
		{
			Vector3Int key = Vector3Int.RoundToInt(V[v] * 1000f);
			if (!pid.TryGetValue(key, out var value4))
			{
				value4 = (pid[key] = par.Count);
				par.Add(value4);
			}
			return value4;
		}
	}

	private static void GlassHit(FBCar car, St s, Vector3 pW, int dmg, float radius, Vector3 dirW)
	{
		if (!car.shell)
		{
			return;
		}
		MeshFilter meshFilter = null;
		int index = -1;
		float num = radius;
		Transform transform = null;
		foreach (Transform item in car.shell)
		{
			string name = item.name;
			if (name.StartsWith("body") && !name.StartsWith("body_lod"))
			{
				transform = item;
				break;
			}
		}
		if (!transform)
		{
			return;
		}
		MeshFilter[] componentsInChildren = transform.GetComponentsInChildren<MeshFilter>(includeInactive: true);
		foreach (MeshFilter meshFilter2 in componentsInChildren)
		{
			if (!meshFilter2.sharedMesh || !meshFilter2.sharedMesh.isReadable || !meshFilter2.GetComponent<MeshRenderer>())
			{
				continue;
			}
			Glass glass = GlassOf(s, meshFilter2);
			if (glass == null)
			{
				continue;
			}
			for (int j = 0; j < glass.panes.Count; j++)
			{
				if (glass.state[j] < 2)
				{
					float num2 = Vector3.Distance(meshFilter2.transform.TransformPoint(glass.centers[j]), pW);
					if (num2 < num)
					{
						num = num2;
						index = j;
						meshFilter = meshFilter2;
					}
				}
			}
		}
		if ((bool)meshFilter)
		{
			Glass glass2 = GlassOf(s, meshFilter);
			int num3 = glass2.state[index];
			glass2.state[index] = Mathf.Min(2, num3 + dmg);
			Vector3 vector = meshFilter.transform.TransformPoint(glass2.centers[index]);
			if (glass2.state[index] >= 2)
			{
				FBImpactFx.Glass(vector, -dirW, dirW);
				FBAudio.PlayAt("glass_break", vector, 0.8f, 6f);
			}
			else
			{
				FBAudio.PlayAt("glass_break", vector, 0.35f, 3f);
			}
			ApplyGlass(meshFilter, glass2);
		}
	}

	private static void ApplyGlass(MeshFilter mf, Glass g)
	{
		Mesh sharedMesh = mf.sharedMesh;
		MeshRenderer component = mf.GetComponent<MeshRenderer>();
		List<Material> list = new List<Material>(component.sharedMaterials);
		if (g.crackSub < 0)
		{
			g.crackSub = sharedMesh.subMeshCount;
			sharedMesh.subMeshCount = g.crackSub + 1;
			while (list.Count < sharedMesh.subMeshCount)
			{
				list.Add(null);
			}
			list[g.crackSub] = CrackMat(list[g.sub]);
			component.sharedMaterials = list.ToArray();
		}
		List<int> list2 = new List<int>();
		List<int> list3 = new List<int>();
		for (int i = 0; i < g.panes.Count; i++)
		{
			if (g.state[i] == 0)
			{
				list2.AddRange(g.panes[i]);
			}
			else if (g.state[i] == 1)
			{
				list3.AddRange(g.panes[i]);
			}
		}
		sharedMesh.SetTriangles(list2, g.sub, calculateBounds: false);
		sharedMesh.SetTriangles(list3, g.crackSub, calculateBounds: false);
	}

	public static void Repair(FBCar car)
	{
		if (!car || !all.TryGetValue(car, out var value))
		{
			return;
		}
		foreach (KeyValuePair<Mesh, (Vector3[], Vector3[])> item2 in value.rest)
		{
			if ((bool)item2.Key)
			{
				item2.Key.vertices = item2.Value.Item1;
				if (item2.Value.Item2 != null && item2.Value.Item2.Length == item2.Value.Item1.Length)
				{
					item2.Key.normals = item2.Value.Item2;
				}
				item2.Key.RecalculateBounds();
			}
		}
		foreach (KeyValuePair<MeshFilter, (Mesh, Glass)> item3 in value.glass)
		{
			Glass item = item3.Value.Item2;
			if (item != null && (bool)item3.Key && !(item3.Key.sharedMesh != item3.Value.Item1))
			{
				for (int i = 0; i < item.state.Count; i++)
				{
					item.state[i] = 0;
				}
				ApplyGlass(item3.Key, item);
			}
		}
		value.rest.Clear();
		value.hp = 100f;
		value.dead = false;
		value.pull = (value.front = (value.rear = (value.left = (value.right = 0f))));
		value.light = (value.moderate = (value.severe = 0));
		FBCarLights.Repair(car);
		FBDoors.Unbend(car);
	}
}
}
