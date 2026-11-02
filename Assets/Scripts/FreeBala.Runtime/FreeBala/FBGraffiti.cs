using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public static class FBGraffiti
{
	private class Wall
	{
		public string k;

		public string bk;

		public Collider col;

		public bool bld;

		public Vector3 n;

		public Vector3 t;

		public Vector3 hp;

		public float d;

		public float gy;

		public float s0;

		public float s1;

		public float top;

		public int vis;

		public int nx;

		public int nz;

		public byte[] mask;

		public bool analysed;

		public List<(int a, int b)> used = new List<(int, int)>();

		public List<(int j0, int i0, int i1, int h)> rects;
	}

	private static Material mat;

	private static GameObject go;

	private static Dictionary<string, object> meta;

	private static Texture2D atlas;

	private static Color32[] alpha;

	private static int aw;

	private static int ah;

	private static readonly Regex BUILDING = new Regex("VJ_Bloco_\\d+_(blue|orange|red|yellow|green|salmon)|VJ_Leste_\\d|VJ_Comercio|VJ_Bat_(Administrativo|Comando|Deposito|Reserva|Predio|Galpao)");

	private static readonly Regex NOT_WALL = new Regex("car(paint|metal|glass)|veh|viatura|caveir|poste|pole|arvore|tree|palm|capim|grama|grass|lamp|glass|vidro|water|jet|rio|agua|cont[eê]i?ner|conteiner|container|BRP_|onibus|moto|lixeira|placa|sign_|let_|heart_mosaic|base_green|letreiro|tower|antena|mirante|kiosk_roof|roof|telha|rust|cobertura", RegexOptions.IgnoreCase);

	private static readonly Regex PAINT_NO = new Regex("iron|sill|glass|vidro|(^|_)ac$|roof|telha|lamp|tile|int_|steel|yellow_paint|fabric|numbers|wood|door|grime|alum|plastic|granite|stairs|sidewalk|concrete$|court|dirt|grass|road|asphalt|tapume|concertina|galv", RegexOptions.IgnoreCase);

	private static readonly Regex BLD = new Regex("VJ_Bloco_\\d+_(blue|orange|red|yellow|green|salmon)$");

	private static readonly Regex SKIPSEG = new Regex("^mesh_|^_instance|^Scene$");

	private const float C = 0.2f;

	private static readonly Dictionary<Collider, string> bkeyC = new Dictionary<Collider, string>();

	public static bool BakeToAssets;

	public static string LastSource = "";

	private static bool WallOk(string name)
	{
		if (!BUILDING.IsMatch(name))
		{
			return !NOT_WALL.IsMatch(name);
		}
		return true;
	}

	private static Func<float> Seeded(string s)
	{
		uint h = 2166136261u;
		foreach (char c in s)
		{
			h = (h ^ c) * 16777619;
		}
		return delegate
		{
			h = (h ^ (h >> 15)) * 2246822507u;
			h = (h ^ (h >> 13)) * 3266489909u;
			h ^= h >> 16;
			return (float)h / 4.2949673E+09f;
		};
	}

	private static bool Ensure()
	{
		if ((bool)mat)
		{
			return true;
		}
		meta = Json.O(GameData.Root, "graffiti");
		atlas = Resources.Load<Texture2D>("Graffiti/atlas");
		if (meta == null || !atlas)
		{
			return false;
		}
		mat = new Material(FBShaders.Lit)
		{
			name = "grafite"
		};
		mat.SetTexture("_BaseMap", atlas);
		mat.mainTexture = atlas;
		mat.SetColor("_BaseColor", new Color(0.94f, 0.925f, 0.894f));
		mat.SetFloat("_Smoothness", 0.05f);
		mat.SetFloat("_Metallic", 0f);
		mat = FBShaders.Transparent(mat, additive: false, clip: true);
		mat.SetFloat("_Cutoff", 0.03f);
		mat.renderQueue = 2995;
		return true;
	}

	private static bool MapRay(Vector3 o, Vector3 d, float far, out RaycastHit h)
	{
		if (Physics.Raycast(o, d, out h, far, 256, QueryTriggerInteraction.Ignore) && h.collider is MeshCollider)
		{
			return true;
		}
		return false;
	}

	private static string BKey(Collider c)
	{
		if (bkeyC.TryGetValue(c, out var value))
		{
			return value;
		}
		List<Transform> list = new List<Transform>();
		Transform transform = c.transform;
		while ((bool)transform)
		{
			list.Add(transform);
			transform = transform.parent;
		}
		list.Reverse();
		value = (from t in list.Skip(1)
			select t.name).FirstOrDefault((string n) => !SKIPSEG.IsMatch(n)) ?? c.name;
		bkeyC[c] = value;
		return value;
	}

	private static string MatOf(RaycastHit h)
	{
		Renderer component = h.collider.GetComponent<Renderer>();
		MeshFilter component2 = h.collider.GetComponent<MeshFilter>();
		if (!component)
		{
			return "";
		}
		Material[] sharedMaterials = component.sharedMaterials;
		string result = ((sharedMaterials.Length != 0 && (bool)sharedMaterials[0]) ? sharedMaterials[0].name : "");
		if ((bool)component2 && (bool)component2.sharedMesh && component2.sharedMesh.isReadable && component2.sharedMesh.subMeshCount > 1 && h.triangleIndex >= 0)
		{
			Mesh sharedMesh = component2.sharedMesh;
			int num = h.triangleIndex * 3;
			for (int i = 0; i < sharedMesh.subMeshCount; i++)
			{
				SubMeshDescriptor subMesh = sharedMesh.GetSubMesh(i);
				if (num >= subMesh.indexStart && num < subMesh.indexStart + subMesh.indexCount)
				{
					if (i >= sharedMaterials.Length || !sharedMaterials[i])
					{
						return result;
					}
					return sharedMaterials[i].name;
				}
			}
		}
		return result;
	}

	private static bool WallHit(Vector3 o, Vector3 d, float far, out RaycastHit h)
	{
		if (MapRay(o, d, far, out h) && FBWorld.SurfaceAt(h) == 0)
		{
			return Mathf.Abs(h.normal.y) <= 0.18f;
		}
		return false;
	}

	private static string Signature()
	{
		long num = 0L;
		long num2 = 17L;
		MeshCollider[] array = UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
		foreach (MeshCollider meshCollider in array)
		{
			if (meshCollider.gameObject.layer == 8 && meshCollider.enabled)
			{
				num++;
				Vector3 center = meshCollider.bounds.center;
				long num3 = ((long)Mathf.RoundToInt(center.x * 4f) * 73856093L) ^ ((long)Mathf.RoundToInt(center.y * 4f) * 19349663L) ^ ((long)Mathf.RoundToInt(center.z * 4f) * 83492791L);
				num2 += num3 * 2654435761u;
			}
		}
		return num + "-" + (num2 & 0x7FFFFFFFFFFFL).ToString("x");
	}

	private static string CachePath(string key)
	{
		return Path.Combine(Application.persistentDataPath, "grafites", key + ".txt");
	}

	private static List<(string k, Vector3 c, Vector3 n, float w, float h)> ReadCache(string text, string sig)
	{
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		string[] array = text.Split('\n');
		if (array.Length == 0 || array[0].Trim() != sig)
		{
			return null;
		}
		CultureInfo ic = CultureInfo.InvariantCulture;
		List<(string, Vector3, Vector3, float, float)> list = new List<(string, Vector3, Vector3, float, float)>();
		for (int i = 1; i < array.Length; i++)
		{
			string[] f = array[i].Trim().Split('|');
			if (f.Length >= 9)
			{
				list.Add((f[0], new Vector3(F(1), F(2), F(3)), new Vector3(F(4), F(5), F(6)), F(7), F(8)));
			}
			float F(int j)
			{
				return float.Parse(f[j], ic);
			}
		}
		return list;
	}

	private static string WriteCache(string sig, List<(string k, Vector3 c, Vector3 n, float w, float h)> l)
	{
		CultureInfo invariantCulture = CultureInfo.InvariantCulture;
		StringBuilder stringBuilder = new StringBuilder(sig + "\n");
		foreach (var (text, vector, vector2, num, num2) in l)
		{
			stringBuilder.Append(string.Join("|", text, vector.x.ToString("0.###", invariantCulture), vector.y.ToString("0.###", invariantCulture), vector.z.ToString("0.###", invariantCulture), vector2.x.ToString("0.####", invariantCulture), vector2.y.ToString("0.####", invariantCulture), vector2.z.ToString("0.####", invariantCulture), num.ToString("0.###", invariantCulture), num2.ToString("0.###", invariantCulture))).Append('\n');
		}
		return stringBuilder.ToString();
	}

	public static int Place(string key)
	{
		Clear();
		if (!Ensure())
		{
			return 0;
		}
		Dictionary<string, object> tiles = Json.O(meta, "tiles");
		if (tiles == null)
		{
			return 0;
		}
		string text = Signature();
		List<(string, Vector3, Vector3, float, float)> list = (BakeToAssets ? null : ReadCache(Resources.Load<TextAsset>("Graffiti/cache_" + key)?.text, text));
		LastSource = ((list != null) ? "assado" : "");
		if (list == null && !BakeToAssets)
		{
			try
			{
				string path = CachePath(key);
				if (File.Exists(path))
				{
					list = ReadCache(File.ReadAllText(path), text);
					if (list != null)
					{
						LastSource = "cache local";
					}
				}
			}
			catch
			{
			}
		}
		if (list != null && list.All<(string, Vector3, Vector3, float, float)>(((string k, Vector3 c, Vector3 n, float w, float h) x) => tiles.ContainsKey(x.k)))
		{
			Build(list, tiles);
			Debug.Log($"[FreeBala] grafites v4: {key} {list.Count} artes ({LastSource})");
			return list.Count;
		}
		LastSource = "calculado";
		List<Vector3> list2 = FBNav.Fixed();
		if (list2.Count == 0)
		{
			return 0;
		}
		Func<float> rnd = Seeded("grafite3|" + key);
		Dictionary<string, Wall> dictionary = new Dictionary<string, Wall>();
		Vector3[] array = Enumerable.Range(0, 16).Select(delegate(int i)
		{
			float f = (float)i / 16f * (float)Math.PI * 2f;
			return new Vector3(Mathf.Sin(f), 0f, Mathf.Cos(f));
		}).ToArray();
		foreach (Vector3 item4 in list2)
		{
			Vector3[] array2 = array;
			foreach (Vector3 vector in array2)
			{
				if (!WallHit(item4 + Vector3.up * 1.6f, vector, 30f, out var h) || h.distance < 1.8f)
				{
					continue;
				}
				string text2 = BKey(h.collider);
				string text3 = MatOf(h);
				if (!PAINT_NO.IsMatch(text3) && WallOk(text2 + " " + h.collider.name + " " + text3))
				{
					Vector3 vector2 = new Vector3(h.normal.x, 0f, h.normal.z).normalized;
					if (Vector3.Dot(vector2, vector) > 0f)
					{
						vector2 = -vector2;
					}
					float num = vector2.x * h.point.x + vector2.z * h.point.z;
					string text4 = text2 + "|" + Mathf.RoundToInt(Mathf.Atan2(vector2.x, vector2.z) * 40f) + "|" + Mathf.RoundToInt(num * 4f);
					if (dictionary.TryGetValue(text4, out var value))
					{
						value.vis++;
						continue;
					}
					dictionary[text4] = new Wall
					{
						k = text4,
						bk = text2,
						col = h.collider,
						bld = BLD.IsMatch(text2),
						n = vector2,
						d = num,
						hp = h.point,
						vis = 1
					};
				}
			}
		}
		Dictionary<string, Vector2> LIM = new Dictionary<string, Vector2>
		{
			["mural"] = new Vector2(14f, 7.6f),
			["peca"] = new Vector2(9.5f, 5.2f),
			["tag"] = new Vector2(6.5f, 3.2f)
		};
		Dictionary<string, Vector2> MIN = new Dictionary<string, Vector2>
		{
			["mural"] = new Vector2(4.2f, 2.4f),
			["peca"] = new Vector2(3.4f, 2f),
			["tag"] = new Vector2(2.8f, 1.7f)
		};
		Dictionary<string, int> order = new Dictionary<string, int>
		{
			["mural"] = 0,
			["peca"] = 1,
			["tag"] = 2
		};
		int value8;
		List<(string, Dictionary<string, object>)> list3 = (from k in tiles.Keys
			select (k: k, tl: tiles[k] as Dictionary<string, object>) into x
			where x.tl != null
			select (k: x.k, tl: x.tl, r: rnd()) into x
			orderby (!order.TryGetValue(Json.S(x.tl, "kind") ?? "", out value8)) ? 1 : value8, x.r
			select (k: x.k, tl: x.tl)).ToList();
		List<Wall> list4 = (from w in dictionary.Values
			orderby w.bld descending, w.vis descending
			select w).ThenBy((Wall w) => w.k, StringComparer.Ordinal).ToList();
		List<Vector3> list5 = new List<Vector3>();
		Dictionary<string, int> dictionary2 = new Dictionary<string, int>();
		List<(string, Vector3, Vector3, float, float)> list6 = new List<(string, Vector3, Vector3, float, float)>();
		for (int m = 0; m < 2; m++)
		{
			if (list3.Count <= 0)
			{
				break;
			}
			foreach (Wall item5 in list4)
			{
				if (list3.Count == 0)
				{
					break;
				}
				if (m == 0 && !item5.bld)
				{
					continue;
				}
				dictionary2.TryGetValue(item5.bk, out var value2);
				if (value2 >= (item5.bld ? 2 : 4) || !Analyse(item5))
				{
					continue;
				}
				for (int n = 0; n < 3; n++)
				{
					if (list3.Count <= 0)
					{
						break;
					}
					(float, float, float, int, int, int, int)? tuple = null;
					int index = -1;
					for (int num2 = 0; num2 < list3.Count; num2++)
					{
						(float, float, float, int, int, int, int)? tuple2 = Fit(item5, list3[num2].Item2);
						if (tuple2.HasValue && (!tuple.HasValue || tuple2.Value.Item1 > tuple.Value.Item1))
						{
							tuple = tuple2;
							index = num2;
						}
					}
					if (!tuple.HasValue)
					{
						break;
					}
					(float, float, float, int, int, int, int) value3 = tuple.Value;
					float num3 = item5.s0 + ((float)value3.Item6 + (float)value3.Item4 / 2f) * 0.2f;
					float y = item5.gy + 0.1f + (float)value3.Item7 * 0.2f + value3.Item3 / 2f;
					Vector3 c = new Vector3(item5.hp.x + item5.t.x * num3, y, item5.hp.z + item5.t.z * num3);
					if (list5.Any((Vector3 p) => (p - c).magnitude < 7f))
					{
						item5.used.Add((value3.Item6, value3.Item6 + value3.Item4));
						continue;
					}
					list6.Add((list3[index].Item1, c, item5.n, value3.Item2, value3.Item3));
					list5.Add(c);
					item5.used.Add((value3.Item6 - 4, value3.Item6 + value3.Item4 + 4));
					list3.RemoveAt(index);
					dictionary2[item5.bk] = (dictionary2.TryGetValue(item5.bk, out var value4) ? value4 : 0) + 1;
					if (dictionary2[item5.bk] >= (item5.bld ? 2 : 4))
					{
						break;
					}
				}
			}
		}
		Debug.Log($"[FreeBala] grafites v4: {key} {list6.Count} artes, paredes vistas {dictionary.Count} (calculado; assinatura {text})");
		string contents = WriteCache(text, list6);
		try
		{
			string path2 = CachePath(key);
			Directory.CreateDirectory(Path.GetDirectoryName(path2));
			File.WriteAllText(path2, contents);
		}
		catch
		{
		}
		Build(list6, tiles);
		return list6.Count;
		static bool Analyse(Wall wl)
		{
			if (wl.analysed)
			{
				return wl.mask != null;
			}
			wl.analysed = true;
			Vector3 n2 = wl.n;
			wl.t = new Vector3(n2.z, 0f, 0f - n2.x);
			float? num20 = FBWorld.GroundY(wl.hp.x + n2.x * 0.5f, wl.hp.y + 1f, wl.hp.z + n2.z * 0.5f, 0.5f);
			if (!num20.HasValue)
			{
				return false;
			}
			float gy = num20.GetValueOrDefault();
			wl.gy = gy;
			if (Probe(wl, 0f, gy + 0.45f) != 1 && Probe(wl, 0f, gy + 2.5f) != 1)
			{
				return false;
			}
			float s2 = 0f;
			float s3 = 0f;
			while (s2 > -40f)
			{
				if (Col(s2 - 0.2f))
				{
					s2 -= 0.2f;
				}
				else
				{
					if (!Col(s2 - 0.4f))
					{
						break;
					}
					s2 -= 0.4f;
				}
			}
			while (s3 < 40f)
			{
				if (Col(s3 + 0.2f))
				{
					s3 += 0.2f;
				}
				else
				{
					if (!Col(s3 + 0.4f))
					{
						break;
					}
					s3 += 0.4f;
				}
			}
			if (s3 - s2 < 2.2f)
			{
				return false;
			}
			float[] source = (from i in Enumerable.Range(0, 7)
				select s2 + (s3 - s2) * ((float)i + 0.5f) / 7f).ToArray();
			float num21 = gy + 0.4f;
			int num22 = 0;
			float z2;
			for (z2 = gy + 0.4f; z2 < gy + 9f; z2 += 0.2f)
			{
				if (num22 >= 5)
				{
					break;
				}
				if (source.Any((float s) => Probe(wl, s, z2) == 1))
				{
					num21 = z2;
					num22 = 0;
				}
				else
				{
					num22++;
				}
			}
			if (num21 - gy < 1.6f)
			{
				return false;
			}
			float num23 = (s2 + s3) / 2f;
			Vector3 vector5 = new Vector3(wl.hp.x + wl.t.x * num23 + n2.x * 0.1f, gy + 1.5f, wl.hp.z + wl.t.z * num23 + n2.z * 0.1f);
			if (MapRay(vector5, n2, 3.2f, out var h4) && h4.distance < 3f)
			{
				return false;
			}
			vector5 += n2 * 1.1f;
			vector5.y = num21 + 0.3f;
			if (Physics.Raycast(vector5, Vector3.up, 40f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore))
			{
				return false;
			}
			wl.s0 = s2;
			wl.s1 = s3;
			wl.top = num21;
			int num24 = Mathf.RoundToInt((s3 - s2) / 0.2f) + 1;
			int num25 = Mathf.RoundToInt((Mathf.Min(num21, gy + 7.9f) - gy - 0.1f) / 0.2f) + 1;
			byte[] array5 = new byte[num24 * num25];
			for (int num26 = 0; num26 < num25; num26++)
			{
				for (int num27 = 0; num27 < num24; num27++)
				{
					array5[num26 * num24 + num27] = (byte)Mathf.Min(255, Probe(wl, s2 + (float)num27 * 0.2f, gy + 0.1f + (float)num26 * 0.2f));
				}
			}
			wl.nx = num24;
			wl.nz = num25;
			wl.mask = array5;
			return true;
			bool Col(float s)
			{
				if (Probe(wl, s, gy + 0.45f) != 1 && Probe(wl, s, gy + 0.9f) != 1 && Probe(wl, s, gy + 2.5f) != 1)
				{
					if (Probe(wl, s, gy + 0.45f) > 1)
					{
						if (Probe(wl, s - 1.2f, gy + 0.45f) != 1 && Probe(wl, s + 1.2f, gy + 0.45f) != 1 && Probe(wl, s - 2.4f, gy + 0.45f) != 1)
						{
							return Probe(wl, s + 2.4f, gy + 0.45f) == 1;
						}
						return true;
					}
					return false;
				}
				return true;
			}
		}
		static int CellOk(Wall wl, int i, int j)
		{
			byte[] mask = wl.mask;
			int nx = wl.nx;
			int num4 = mask[j * nx + i];
			if (num4 <= 0 || mask[j * nx + i + 1] <= 0 || Mathf.Abs(mask[j * nx + i + 1] - num4) > 1 || mask[(j + 1) * nx + i] <= 0 || Mathf.Abs(mask[(j + 1) * nx + i] - num4) > 1 || mask[(j + 1) * nx + i + 1] <= 0 || Mathf.Abs(mask[(j + 1) * nx + i + 1] - num4) > 1)
			{
				return 0;
			}
			return num4;
		}
		(float sc, float w, float h, int ax, int ay, int i0, int j0)? Fit(Wall wl, Dictionary<string, object> tl)
		{
			string key2 = Json.S(tl, "kind") ?? "";
			Vector2 value5;
			Vector2 vector3 = (LIM.TryGetValue(key2, out value5) ? value5 : new Vector2(9f, 5f));
			Vector2 value6;
			Vector2 vector4 = (MIN.TryGetValue(key2, out value6) ? value6 : new Vector2(2.8f, 1.6f));
			float num12 = Json.F(tl, "aspect", 1f);
			(float, float, float, int, int, int)? tuple3 = null;
			foreach (var item6 in Rects(wl))
			{
				List<(int, int)> list8 = new List<(int, int)> { (item6.i0, item6.i1 + 1) };
				foreach (var item7 in wl.used)
				{
					int ua = item7.a;
					int ub = item7.b;
					list8 = list8.SelectMany<(int, int), (int, int)>(((int a, int b) s) => (ub <= s.a || ua >= s.b) ? new(int, int)[1] { s } : new(int, int)[2]
					{
						(s.a, Mathf.Min(s.b, ua)),
						(Mathf.Max(s.a, ub), s.b)
					}.Where(((int, int) x) => x.Item2 > x.Item1).ToArray()).ToList();
				}
				foreach (var item8 in list8)
				{
					int item = item8.Item1;
					int item2 = item8.Item2;
					int num13 = item;
					int num14 = item2;
					if (num13 > 0)
					{
						num13 += 2;
					}
					if (num14 < wl.nx - 1)
					{
						num14 -= 2;
					}
					float num15 = (float)(num14 - num13) * 0.2f;
					float num16 = (float)item6.h * 0.2f;
					if (!(num15 < vector4.x) && !(num16 < vector4.y))
					{
						num15 = Mathf.Min(num15, vector3.x);
						num16 = Mathf.Min(num16, vector3.y);
						if (num15 / num16 > num12 * 1.28f)
						{
							num15 = num16 * num12 * 1.28f;
						}
						else if (num15 / num16 < num12 / 1.28f)
						{
							num16 = num15 * 1.28f / num12;
						}
						if (!(num15 < vector4.x) && !(num16 < vector4.y))
						{
							float num17 = num15 * num16 * (1f - Mathf.Abs(num15 / num16 / num12 - 1f) * 0.25f);
							if (!tuple3.HasValue || num17 > tuple3.Value.Item1)
							{
								tuple3 = (num17, num15, num16, num13, num14 - 1, item6.j0);
							}
						}
					}
				}
			}
			if (!tuple3.HasValue)
			{
				return null;
			}
			(float, float, float, int, int, int) value7 = tuple3.Value;
			float num18 = (float)(value7.Item5 - value7.Item4 + 1) * 0.2f;
			int item3 = Mathf.RoundToInt(value7.Item2 / 0.2f);
			return (value7.Item1, value7.Item2, value7.Item3, item3, Mathf.RoundToInt(value7.Item3 / 0.2f), value7.Item4 + Mathf.RoundToInt((num18 - value7.Item2) / 0.2f / 2f), value7.Item6);
		}
		static bool Flat(Wall wl, int i, int j)
		{
			return CellOk(wl, i, j) == 1;
		}
		static int Probe(Wall wl, float s, float z, bool deep = true)
		{
			if (!MapRay(new Vector3(wl.hp.x + wl.t.x * s + wl.n.x * 0.06f, z, wl.hp.z + wl.t.z * s + wl.n.z * 0.06f), -wl.n, deep ? 2.45f : 0.25f, out var h2))
			{
				return 0;
			}
			if (BKey(h2.collider) != wl.bk || PAINT_NO.IsMatch(MatOf(h2)) || h2.normal.x * wl.n.x + h2.normal.z * wl.n.z < 0.97f * new Vector2(h2.normal.x, h2.normal.z).magnitude)
			{
				return 0;
			}
			float num19 = wl.d - (wl.n.x * h2.point.x + wl.n.z * h2.point.z);
			if (num19 < -0.035f || (num19 > 0.035f && num19 < 0.3f))
			{
				return 0;
			}
			if (MapRay(new Vector3(h2.point.x + wl.n.x * 0.06f, z, h2.point.z + wl.n.z * 0.06f), wl.n, 0.9f, out var _))
			{
				return 0;
			}
			return 1 + Mathf.Max(0, Mathf.RoundToInt(num19 / 0.05f));
		}
		static List<(int j0, int i0, int i1, int h)> Rects(Wall wl)
		{
			if (wl.rects != null)
			{
				return wl.rects;
			}
			int nx2 = wl.nx;
			int nz = wl.nz;
			List<(int, int, int, int)> list7 = (wl.rects = new List<(int, int, int, int)>());
			int[] array3 = new int[4] { 0, 1, 2, 3 };
			foreach (int num6 in array3)
			{
				int[] array4 = new int[nx2 - 1];
				for (int num7 = 0; num7 < nx2 - 1; num7++)
				{
					int num8;
					for (num8 = 0; num6 + num8 < nz - 1 && Flat(wl, num7, num6 + num8); num8++)
					{
					}
					array4[num7] = num8;
				}
				for (int num9 = 0; num9 < nx2 - 1; num9++)
				{
					if (array4[num9] != 0 && (num9 <= 0 || array4[num9 - 1] <= 0 || array4[num9 - 1] < array4[num9]))
					{
						int num10 = array4[num9];
						for (int num11 = num9; num11 < nx2 - 1; num11++)
						{
							if (array4[num11] < num10)
							{
								list7.Add((num6, num9, num11 - 1, num10));
								num10 = array4[num11];
								if (num10 < 6)
								{
									break;
								}
							}
							if (num11 == nx2 - 2 && num10 >= 6)
							{
								list7.Add((num6, num9, num11, num10));
							}
						}
					}
				}
			}
			return list7;
		}
	}

	private static void Build(List<(string k, Vector3 c, Vector3 n, float w, float h)> list, Dictionary<string, object> tiles)
	{
		List<Vector3> list2 = new List<Vector3>();
		List<Vector2> list3 = new List<Vector2>();
		List<Vector3> list4 = new List<Vector3>();
		List<int> list5 = new List<int>();
		foreach (var item6 in list)
		{
			string item = item6.k;
			Vector3 item2 = item6.c;
			Vector3 item3 = item6.n;
			float item4 = item6.w;
			float item5 = item6.h;
			List<object> list6 = Json.L(tiles[item], "uv");
			float num = Json.Fv(list6[0]);
			float num2 = Json.Fv(list6[1]);
			float num3 = Json.Fv(list6[2]);
			float num4 = Json.Fv(list6[3]);
			Vector3 vector = new Vector3(item3.z, 0f, 0f - item3.x);
			int count = list2.Count;
			(float, float)[] array = new(float, float)[4]
			{
				(-0.5f, -0.5f),
				(0.5f, -0.5f),
				(0.5f, 0.5f),
				(-0.5f, 0.5f)
			};
			for (int i = 0; i < array.Length; i++)
			{
				var (num5, num6) = array[i];
				list2.Add(item2 - vector * num5 * item4 + Vector3.up * num6 * item5 + item3 * 0.02f);
				list3.Add(new Vector2(num + (num3 - num) * (num5 + 0.5f), num2 + (num4 - num2) * (num6 + 0.5f)));
				list4.Add(item3);
			}
			list5.AddRange(new int[6]
			{
				count,
				count + 2,
				count + 1,
				count,
				count + 3,
				count + 2
			});
		}
		if (list5.Count != 0)
		{
			Mesh mesh = new Mesh
			{
				name = "grafites",
				indexFormat = IndexFormat.UInt32
			};
			mesh.SetVertices(list2);
			mesh.SetUVs(0, list3);
			mesh.SetNormals(list4);
			mesh.SetTriangles(list5, 0);
			mesh.RecalculateBounds();
			mesh.RecalculateTangents();
			go = new GameObject("Grafites");
			go.AddComponent<MeshFilter>().sharedMesh = mesh;
			MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
			meshRenderer.sharedMaterial = mat;
			meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
		}
	}

	public static void Clear()
	{
		if ((bool)go)
		{
			if (Application.isPlaying)
			{
				UnityEngine.Object.Destroy(go);
			}
			else
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}
		go = null;
		bkeyC.Clear();
	}
}
}
