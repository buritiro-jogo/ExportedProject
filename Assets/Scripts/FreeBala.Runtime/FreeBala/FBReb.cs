using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public static class FBReb
{
	private class Rig
	{
		public Transform root;

		public readonly Dictionary<string, Transform> node = new Dictionary<string, Transform>();

		public readonly Dictionary<MeshFilter, int[][]> orig = new Dictionary<MeshFilter, int[][]>();

		public readonly Dictionary<string, HashSet<Vector3Int>> zoneKeys = new Dictionary<string, HashSet<Vector3Int>>();

		public Material flag;
	}

	public static readonly string[] ZONAS = new string[5] { "aerofolio", "capo", "frente", "lateral", "traseira" };

	public static readonly string[] ZONA_NOMES = new string[5] { "AEROFÓLIO", "CAPÔ", "PARA-CHOQUE DIANTEIRO", "SAIAS LATERAIS", "PARA-CHOQUE TRASEIRO" };

	public static readonly string[] BANDEIRAS = new string[4] { "BRASIL", "RUMO AO HEXA", "FREE BALA", "REB" };

	private static readonly Vector2[] FLAG_OFS = new Vector2[4]
	{
		new Vector2(0f, 0f),
		new Vector2(0.5f, 0f),
		new Vector2(0f, -0.35f),
		new Vector2(0.5f, -0.35f)
	};

	private static readonly Dictionary<string, string> ALIAS = new Dictionary<string, string>
	{
		["Clio2003"] = "GolG2",
		["Kadett"] = "Chevett",
		["ViaturaPMAzul"] = "S10_2017",
		["ViaturaBM"] = "S10_2017",
		["ViaturaPM"] = "S10_2017"
	};

	private static readonly Dictionary<FBCar, Rig> rigs = new Dictionary<FBCar, Rig>();

	private static Dictionary<string, object> Carros => Json.O(GameData.RebOficina, "carros");

	public static List<string> Roncos => (from o in Json.L(GameData.RebOficina, "roncos") ?? new List<object>()
		select o as string into x
		where x != null && Json.O(Json.O(GameData.RES, "profiles"), x) != null
		select x).ToList();

	public static Dictionary<string, object> Own(string id)
	{
		return Json.O(Carros, id);
	}

	public static Dictionary<string, object> Car(string id)
	{
		Dictionary<string, object> dictionary = Own(id);
		if (dictionary == null)
		{
			if (id == null || !ALIAS.TryGetValue(id, out var value))
			{
				return null;
			}
			dictionary = Own(value);
		}
		return dictionary;
	}

	public static string RoncoNome(string k)
	{
		return (k ?? "").Replace('_', ' ');
	}

	public static string Ronco(string id)
	{
		string text = Json.S(Car(id), "ronco");
		if (text == null || Json.O(Json.O(GameData.RES, "profiles"), text) == null)
		{
			return null;
		}
		return text;
	}

	public static string Buzina(string id)
	{
		string text = Json.S(Car(id), "buzina");
		if (text != null)
		{
			return "reb_horn_" + (text.StartsWith("B_") ? text.Substring(2) : text).ToLowerInvariant().Replace('-', '_');
		}
		return null;
	}

	public static string Tracao(string id)
	{
		Dictionary<string, object> dictionary = Car(id);
		if (dictionary == null || !dictionary.ContainsKey("tracao"))
		{
			return null;
		}
		return Json.I(dictionary, "tracao") switch
		{
			2 => "4x4", 
			1 => "RWD", 
			_ => "FWD", 
		};
	}

	public static float[] Pneus(string id)
	{
		List<object> list = Json.L(Car(id), "pneus");
		if (list != null)
		{
			return list.Select((object o) => Json.Fv(o)).ToArray();
		}
		return new float[13]
		{
			0.8f, 0.85f, 0.9f, 0.95f, 1f, 1.05f, 1.1f, 1.15f, 1.2f, 1.25f,
			1.3f, 1.4f, 1.5f
		};
	}

	public static bool HasZone(FBCar car, string z)
	{
		if (Json.O(Json.O(Own(car.id), "bodykit"), z) != null)
		{
			return Parts(car);
		}
		return false;
	}

	public static List<int> ZoneVariants(FBCar car, string z)
	{
		return (Json.L(Json.O(Json.O(Own(car.id), "bodykit"), z), "var") ?? new List<object>()).Select((object o) => (int)Json.Fv(o)).ToList();
	}

	public static int Forros(FBCar car, string side)
	{
		return (Json.L(Json.O(Own(car.id), "forros"), side) ?? new List<object>()).Count;
	}

	public static bool HasEngate(FBCar car)
	{
		Dictionary<string, object> dictionary = Own(car.id);
		if (dictionary != null && dictionary.ContainsKey("engate"))
		{
			return Parts(car);
		}
		return false;
	}

	public static bool HasBandeira(FBCar car)
	{
		if (Json.O(Own(car.id), "bandeiras") != null)
		{
			return Parts(car);
		}
		return false;
	}

	public static bool HasBodykit(FBCar car)
	{
		return ZONAS.Any((string z) => HasZone(car, z));
	}

	private static bool Parts(FBCar car)
	{
		if ((bool)car)
		{
			return FBRegistry.I.Parts(car.id);
		}
		return false;
	}

	private static Rig RigOf(FBCar car)
	{
		if (rigs.TryGetValue(car, out var value) && (bool)value.root)
		{
			return value;
		}
		GameObject gameObject = FBRegistry.I.Parts(car.id);
		if (!gameObject || !car.shell)
		{
			return null;
		}
		GameObject gameObject2 = Object.Instantiate(gameObject, car.shell, worldPositionStays: false);
		gameObject2.name = "reb_parts";
		value = new Rig
		{
			root = gameObject2.transform
		};
		Transform[] componentsInChildren = gameObject2.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform in componentsInChildren)
		{
			transform.gameObject.layer = 11;
			if (transform != gameObject2.transform && !value.node.ContainsKey(transform.name))
			{
				value.node[transform.name] = transform;
			}
		}
		FBVehicles.DressParts(gameObject2, car);
		FBPlates.Split(gameObject2.transform, FBPlates.Of(car.paint));
		Renderer[] componentsInChildren2 = gameObject2.GetComponentsInChildren<Renderer>(includeInactive: true);
		for (int i = 0; i < componentsInChildren2.Length; i++)
		{
			componentsInChildren2[i].shadowCastingMode = ShadowCastingMode.On;
		}
		foreach (KeyValuePair<string, Transform> item in value.node)
		{
			if (item.Key != "parts")
			{
				item.Value.gameObject.SetActive(value: false);
			}
		}
		rigs[car] = value;
		return value;
	}

	private static void Show(Rig r, string n, bool on)
	{
		if (r != null && r.node.TryGetValue(n, out var value) && (bool)value)
		{
			value.gameObject.SetActive(on);
		}
	}

	public static void Apply(FBCar car, FBTuneData T)
	{
		if (!car || car.ie || T == null || !Parts(car))
		{
			return;
		}
		Rig rig = RigOf(car);
		if (rig == null)
		{
			return;
		}
		if (T.bk == null)
		{
			T.bk = new int[0];
		}
		if (T.bk.Length < ZONAS.Length)
		{
			int[] array = Enumerable.Repeat(-1, ZONAS.Length).ToArray();
			for (int i = 0; i < T.bk.Length; i++)
			{
				array[i] = T.bk[i];
			}
			T.bk = array;
		}
		List<string> list = new List<string>();
		for (int j = 0; j < ZONAS.Length; j++)
		{
			string text = ZONAS[j];
			List<int> list2 = ZoneVariants(car, text);
			int num = (list2.Contains(T.bk[j]) ? T.bk[j] : (-1));
			foreach (int item3 in list2)
			{
				Show(rig, $"bk_{text}_{item3}", item3 == num);
			}
			if (num >= 0)
			{
				list.Add(text);
			}
		}
		CutBody(car, rig, list);
		(string, int)[] array2 = new(string, int)[4]
		{
			("L", T.forroF),
			("R", T.forroF),
			("L02", T.forroT),
			("R02", T.forroT)
		};
		for (int k = 0; k < array2.Length; k++)
		{
			(string, int) tuple = array2[k];
			string item = tuple.Item1;
			int item2 = tuple.Item2;
			int num2 = Forros(car, item);
			if (num2 == 0)
			{
				continue;
			}
			int num3 = Mathf.Clamp(item2, 0, num2 - 1);
			for (int l = 0; l < num2; l++)
			{
				Show(rig, $"forro_{item}_{l}", l == num3);
			}
			Transform[] componentsInChildren = car.shell.GetComponentsInChildren<Transform>(includeInactive: true);
			foreach (Transform transform in componentsInChildren)
			{
				if (transform.name == "forro_" + item && !transform.IsChildOf(rig.root))
				{
					transform.gameObject.SetActive(value: false);
				}
			}
		}
		Show(rig, "engate", T.engate && HasEngate(car));
		bool flag = T.bandL && HasBandeira(car);
		bool flag2 = T.bandR && HasBandeira(car);
		Show(rig, "bandeira_L", flag);
		Show(rig, "mastro_L", flag);
		Show(rig, "bandeira_R", flag2);
		Show(rig, "mastro_R", flag2);
		if (flag || flag2)
		{
			Flags(car, rig, T.bandeira);
		}
		bool flag3 = T.somSet && T.som != null && T.som.Count > 0 && Json.O(Json.O(car.def, "som"), "bed") == null;
		Show(rig, "forro_som", flag3 && (Own(car.id)?.ContainsKey("forroSom") ?? false));
		AddToLod0(car, rig);
	}

	private static Vector3Int Key(Vector3 p)
	{
		return new Vector3Int(Mathf.RoundToInt(p.x * 500f), Mathf.RoundToInt(p.y * 500f), Mathf.RoundToInt(p.z * 500f));
	}

	private static HashSet<Vector3Int> ZoneKeys(FBCar car, Rig r, string z)
	{
		if (r.zoneKeys.TryGetValue(z, out var value))
		{
			return value;
		}
		value = new HashSet<Vector3Int>();
		r.zoneKeys[z] = value;
		if (!r.node.TryGetValue("bk_" + z + "_base", out var value2) || !value2)
		{
			return value;
		}
		MeshFilter[] componentsInChildren = value2.GetComponentsInChildren<MeshFilter>(includeInactive: true);
		foreach (MeshFilter meshFilter in componentsInChildren)
		{
			if ((bool)meshFilter.sharedMesh && meshFilter.sharedMesh.isReadable)
			{
				Matrix4x4 matrix4x = car.shell.worldToLocalMatrix * meshFilter.transform.localToWorldMatrix;
				Vector3[] vertices = meshFilter.sharedMesh.vertices;
				foreach (Vector3 point in vertices)
				{
					value.Add(Key(matrix4x.MultiplyPoint3x4(point)));
				}
			}
		}
		return value;
	}

	private static IEnumerable<MeshFilter> BodyLod0(FBCar car)
	{
		foreach (Transform item in car.shell)
		{
			if (item.name.StartsWith("body") && !item.name.Contains("lod"))
			{
				MeshFilter[] componentsInChildren = item.GetComponentsInChildren<MeshFilter>(includeInactive: true);
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					yield return componentsInChildren[i];
				}
			}
		}
	}

	private static void CutBody(FBCar car, Rig r, List<string> zones)
	{
		if (zones.Count == 0 && r.orig.Count == 0)
		{
			return;
		}
		HashSet<Vector3Int> hashSet = new HashSet<Vector3Int>();
		foreach (string zone in zones)
		{
			hashSet.UnionWith(ZoneKeys(car, r, zone));
		}
		foreach (MeshFilter item in BodyLod0(car))
		{
			Mesh mesh = item.sharedMesh;
			if (!mesh || !mesh.isReadable)
			{
				continue;
			}
			if (!r.orig.ContainsKey(item))
			{
				if (zones.Count == 0)
				{
					continue;
				}
				if (!mesh.name.EndsWith("_kit"))
				{
					Mesh mesh2 = Object.Instantiate(mesh);
					mesh2.name = mesh.name + "_kit";
					item.sharedMesh = mesh2;
					mesh = mesh2;
				}
				int[][] array = new int[mesh.subMeshCount][];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = mesh.GetTriangles(i);
				}
				r.orig[item] = array;
			}
			int[][] array2 = r.orig[item];
			Matrix4x4 matrix4x = car.shell.worldToLocalMatrix * item.transform.localToWorldMatrix;
			Vector3[] array3 = mesh.vertices;
			Vector3[] array4 = FBCarDamage.RestOf(mesh);
			if (array4 != null && array4.Length == array3.Length)
			{
				array3 = array4;
			}
			bool[] array5 = new bool[array3.Length];
			if (hashSet.Count > 0)
			{
				for (int j = 0; j < array3.Length; j++)
				{
					array5[j] = hashSet.Contains(Key(matrix4x.MultiplyPoint3x4(array3[j])));
				}
			}
			for (int k = 0; k < array2.Length && k < mesh.subMeshCount; k++)
			{
				int[] array6 = array2[k];
				if (hashSet.Count == 0)
				{
					mesh.SetTriangles(array6, k, calculateBounds: false);
					continue;
				}
				List<int> list = new List<int>(array6.Length);
				for (int l = 0; l + 2 < array6.Length; l += 3)
				{
					if (!array5[array6[l]] || !array5[array6[l + 1]] || !array5[array6[l + 2]])
					{
						list.Add(array6[l]);
						list.Add(array6[l + 1]);
						list.Add(array6[l + 2]);
					}
				}
				mesh.SetTriangles(list, k, calculateBounds: false);
			}
			mesh.RecalculateBounds();
		}
	}

	private static void AddToLod0(FBCar car, Rig r)
	{
		LODGroup component = car.shell.GetComponent<LODGroup>();
		if (!component)
		{
			return;
		}
		LOD[] lODs = component.GetLODs();
		if (lODs.Length != 0)
		{
			Renderer[] componentsInChildren = r.root.GetComponentsInChildren<Renderer>(includeInactive: true);
			lODs[0].renderers = lODs[0].renderers.Where((Renderer x) => (bool)x && !x.transform.IsChildOf(r.root)).Concat(componentsInChildren).ToArray();
			component.SetLODs(lODs);
		}
	}

	private static void Flags(FBCar car, Rig r, int idx)
	{
		if (!r.flag)
		{
			Texture2D texture2D = Resources.Load<Texture2D>("CarFX/reb_bandeiras");
			if (!texture2D)
			{
				return;
			}
			r.flag = new Material(FBShaders.Lit)
			{
				name = "bandeira"
			};
			r.flag.SetTexture("_BaseMap", texture2D);
			r.flag.SetFloat("_Smoothness", 0.15f);
			r.flag.SetFloat("_Cull", 0f);
			Material material = new Material(FBShaders.Lit)
			{
				name = "mastro"
			};
			material.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.13f));
			material.SetFloat("_Metallic", 0.6f);
			material.SetFloat("_Smoothness", 0.6f);
			string[] array = new string[2] { "L", "R" };
			foreach (string text in array)
			{
				if (r.node.TryGetValue("bandeira_" + text, out var value) && (bool)value)
				{
					Renderer[] componentsInChildren = value.GetComponentsInChildren<Renderer>(includeInactive: true);
					foreach (Renderer renderer in componentsInChildren)
					{
						renderer.sharedMaterials = Enumerable.Repeat(r.flag, renderer.sharedMaterials.Length).ToArray();
					}
				}
				if (r.node.TryGetValue("mastro_" + text, out var value2) && (bool)value2)
				{
					Renderer[] componentsInChildren = value2.GetComponentsInChildren<Renderer>(includeInactive: true);
					foreach (Renderer renderer2 in componentsInChildren)
					{
						renderer2.sharedMaterials = Enumerable.Repeat(material, renderer2.sharedMaterials.Length).ToArray();
					}
				}
				List<object> list = Json.L(Json.O(Own(car.id), "bandeiras"), text);
				if ((bool)value && list != null && !value.GetComponent<FBRebFlag>())
				{
					FBRebFlag fBRebFlag = value.gameObject.AddComponent<FBRebFlag>();
					fBRebFlag.car = car;
					fBRebFlag.pivotLocal = new Vector3(0f - Json.Fv(list[0]), Json.Fv(list[1]), Json.Fv(list[2]));
				}
			}
		}
		Vector2 vector = FLAG_OFS[Mathf.Clamp(idx, 0, FLAG_OFS.Length - 1)];
		r.flag.SetTextureOffset("_BaseMap", vector);
		r.flag.mainTextureOffset = vector;
	}

	public static void Forget(FBCar car)
	{
		rigs.Remove(car);
	}
}
}
