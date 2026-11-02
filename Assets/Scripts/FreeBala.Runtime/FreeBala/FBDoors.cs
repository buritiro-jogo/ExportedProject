using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace FreeBala
{
public static class FBDoors
{
	private class PartDef
	{
		public Vector3 axis;

		public Vector3 pivot;

		public Vector3 t;

		public float ang;

		public HashSet<long> keys;
	}

	private class CarDef
	{
		public PartDef[] p = new PartDef[6];

		public HashSet<long> shared = new HashSet<long>();
	}

	private class Rig
	{
		public FBCar car;

		public bool split;

		public bool failed;

		public readonly Transform[] piv = new Transform[6];

		public readonly float[] k = new float[6];

		public int target;

		public readonly List<Renderer> extra = new List<Renderer>();

		public LODGroup lg;

		public bool forced;

		public readonly float[] bent = new float[6];

		public readonly bool[] gone = new bool[6];

		public float detachV;

		public int detachPart = -1;

		public Vector3 detachVel;
	}

	private class Runner : MonoBehaviour
	{
		private static readonly int __fbt_LateUpdate = FBTempo.Id("FBDoors.Runner.LateUpdate");

		private void LateUpdate()
		{
			if (!FBTempo.On)
			{
				LateUpdate__fbt();
				return;
			}
			long t0 = FBTempo.T0();
			LateUpdate__fbt();
			FBTempo.Soma(__fbt_LateUpdate, t0);
		}

		private void LateUpdate__fbt()
		{
			Tick(Time.deltaTime);
			FBCharacter player = FBMatch.Player;
			if (!player || !player.alive || (bool)FBVehicles.CarOf(player) || FBGarage.Open_ || !FBVehicles.Allowed)
			{
				return;
			}
			Keyboard current = Keyboard.current;
			if (current != null && (current.uKey.wasPressedThisFrame || current.jKey.wasPressedThisFrame || current.pKey.wasPressedThisFrame))
			{
				FBCar fBCar = NearCar(player.Pos, 3.5f);
				if ((bool)fBCar)
				{
					Keys(fBCar);
				}
			}
		}
	}

	public static readonly string[] NAMES = new string[6] { "portaL", "portaR", "portaL02", "portaR02", "mala", "capo" };

	public const int DOORS = 15;

	public const int TRUNK = 16;

	public const int HOOD = 32;

	private static readonly Dictionary<string, CarDef> defs = new Dictionary<string, CarDef>();

	private static readonly HashSet<string> missing = new HashSet<string>();

	private static readonly Dictionary<FBCar, Rig> rigs = new Dictionary<FBCar, Rig>();

	private static Runner runner;

	public static int Detached;

	private static long Pack(int x, int y, int z)
	{
		return ((long)(x & 0x1FFFF) << 34) | ((long)(y & 0x1FFFF) << 17) | (z & 0x1FFFF);
	}

	private static long Key(Vector3 v)
	{
		return Pack(Mathf.RoundToInt(v.x * 1000f), Mathf.RoundToInt(v.y * 1000f), Mathf.RoundToInt(v.z * 1000f));
	}

	private static void AddDilated(HashSet<long> s, int x, int y, int z)
	{
		s.Add(Pack(x, y, z));
		s.Add(Pack(x + 1, y, z));
		s.Add(Pack(x - 1, y, z));
		s.Add(Pack(x, y + 1, z));
		s.Add(Pack(x, y - 1, z));
		s.Add(Pack(x, y, z + 1));
		s.Add(Pack(x, y, z - 1));
	}

	private static void Decode(string b64, HashSet<long> into)
	{
		if (!string.IsNullOrEmpty(b64))
		{
			byte[] array = Convert.FromBase64String(b64);
			for (int i = 0; i + 5 < array.Length; i += 6)
			{
				AddDilated(into, (short)(array[i] | (array[i + 1] << 8)), (short)(array[i + 2] | (array[i + 3] << 8)), (short)(array[i + 4] | (array[i + 5] << 8)));
			}
		}
	}

	private static CarDef Def(string id)
	{
		if (string.IsNullOrEmpty(id) || missing.Contains(id))
		{
			return null;
		}
		if (defs.TryGetValue(id, out var value))
		{
			return value;
		}
		TextAsset textAsset = Resources.Load<TextAsset>("FBDoors/" + id);
		if (!textAsset)
		{
			missing.Add(id);
			return null;
		}
		Dictionary<string, object> o2 = Json.Parse(textAsset.text) as Dictionary<string, object>;
		Dictionary<string, object> dictionary = Json.O(o2, "parts");
		value = new CarDef();
		if (dictionary == null)
		{
			missing.Add(id);
			return null;
		}
		for (int i = 0; i < NAMES.Length; i++)
		{
			Dictionary<string, object> o = Json.O(dictionary, NAMES[i]);
			if (o != null)
			{
				PartDef partDef = new PartDef
				{
					axis = V("axis").normalized,
					pivot = V("pivot"),
					t = V("t"),
					ang = Json.F(o, "ang"),
					keys = new HashSet<long>()
				};
				Decode(Json.S(o, "p"), partDef.keys);
				if (partDef.keys.Count > 0 && partDef.axis.sqrMagnitude > 0.5f)
				{
					value.p[i] = partDef;
				}
			}
			Vector3 V(string k)
			{
				List<object> list = Json.L(o, k);
				if (list == null || list.Count < 3)
				{
					return Vector3.zero;
				}
				return new Vector3(Json.Fv(list[0]), Json.Fv(list[1]), Json.Fv(list[2]));
			}
		}
		Decode(Json.S(o2, "shared"), value.shared);
		defs[id] = value;
		return value;
	}

	public static bool Has(FBCar car)
	{
		if ((bool)car)
		{
			return Def(car.id) != null;
		}
		return false;
	}

	public static bool HasPart(FBCar car, int bits)
	{
		CarDef carDef = (car ? Def(car.id) : null);
		if (carDef == null)
		{
			return false;
		}
		for (int i = 0; i < 6; i++)
		{
			if ((bits & (1 << i)) != 0 && carDef.p[i] != null)
			{
				return true;
			}
		}
		return false;
	}

	public static int Mask(FBCar car)
	{
		if (!car || !rigs.TryGetValue(car, out var value))
		{
			return 0;
		}
		return value.target;
	}

	public static void Toggle(FBCar car, int bits)
	{
		if (HasPart(car, bits))
		{
			int num = Mask(car);
			bool flag = (num & bits) == 0;
			SetMask(car, flag ? (num | bits) : (num & ~bits), sound: true);
		}
	}

	public static void SetMask(FBCar car, int mask, bool sound = false)
	{
		if (!car || Def(car.id) == null)
		{
			return;
		}
		if (!rigs.TryGetValue(car, out var value))
		{
			if (mask == 0)
			{
				return;
			}
			value = (rigs[car] = new Rig
			{
				car = car
			});
		}
		int target = value.target;
		value.target = mask;
		Ensure();
		if (sound && target != mask)
		{
			bool flag = (mask & ~target) != 0;
			Vector3 position = car.transform.position;
			if (((uint)(mask ^ target) & 0xFu) != 0)
			{
				FBAudio.PlayAt(flag ? "veh_door_open" : "veh_door_close", position, 0.55f, 6f);
			}
			if (((uint)(mask ^ target) & 0x30u) != 0)
			{
				FBAudio.PlayAt(flag ? "veh_door_open" : "veh_door_close", position + car.transform.forward * ((((uint)(mask ^ target) & 0x20u) != 0) ? 1.8f : (-1.8f)), 0.45f, 6f);
			}
		}
	}

	private static void Ensure()
	{
		if (!runner)
		{
			GameObject gameObject = new GameObject("FB_Portas");
			UnityEngine.Object.DontDestroyOnLoad(gameObject);
			runner = gameObject.AddComponent<Runner>();
		}
	}

	private static bool Split(Rig r)
	{
		FBCar car = r.car;
		CarDef carDef = Def(car.id);
		if (carDef == null || !car.shell)
		{
			return false;
		}
		Transform transform = null;
		foreach (Transform item2 in car.shell)
		{
			string name = item2.name;
			if (name.StartsWith("body") && !name.StartsWith("body_lod"))
			{
				transform = item2;
				break;
			}
		}
		if (!transform)
		{
			return false;
		}
		int num = 0;
		MeshFilter[] componentsInChildren = transform.GetComponentsInChildren<MeshFilter>(includeInactive: true);
		foreach (MeshFilter meshFilter in componentsInChildren)
		{
			MeshRenderer component = meshFilter.GetComponent<MeshRenderer>();
			Mesh sharedMesh = meshFilter.sharedMesh;
			if (!component || !sharedMesh || !sharedMesh.isReadable)
			{
				continue;
			}
			Vector3[] array = FBCarDamage.RestOf(sharedMesh) ?? sharedMesh.vertices;
			int[] array2 = new int[array.Length];
			Matrix4x4 m = transform.worldToLocalMatrix * meshFilter.transform.localToWorldMatrix;
			bool isIdentity = m.isIdentity;
			for (int j = 0; j < array.Length; j++)
			{
				long item = Key(isIdentity ? array[j] : m.MultiplyPoint3x4(array[j]));
				int num2 = -1;
				for (int k = 0; k < 6; k++)
				{
					if (carDef.p[k] != null && carDef.p[k].keys.Contains(item))
					{
						num2 = k;
						break;
					}
				}
				if (num2 >= 0 && carDef.shared.Contains(item))
				{
					num2 = -2;
				}
				array2[j] = num2;
			}
			int subMeshCount = sharedMesh.subMeshCount;
			List<int>[] array3 = new List<int>[subMeshCount];
			List<int>[,] array4 = new List<int>[6, subMeshCount];
			bool flag = false;
			for (int l = 0; l < subMeshCount; l++)
			{
				array3[l] = new List<int>();
				int[] triangles = sharedMesh.GetTriangles(l);
				for (int n = 0; n + 2 < triangles.Length; n += 3)
				{
					int num3 = array2[triangles[n]];
					int num4 = array2[triangles[n + 1]];
					int num5 = array2[triangles[n + 2]];
					int num6 = -1;
					if (num3 != -1 && num4 != -1 && num5 != -1)
					{
						int num7 = -1;
						int num8 = 0;
						for (int num9 = 0; num9 < 6; num9++)
						{
							int num10 = ((num3 == num9) ? 1 : 0) + ((num4 == num9) ? 1 : 0) + ((num5 == num9) ? 1 : 0);
							if (num10 > num8)
							{
								num8 = num10;
								num7 = num9;
							}
						}
						num6 = num7;
					}
					if (num6 < 0)
					{
						array3[l].Add(triangles[n]);
						array3[l].Add(triangles[n + 1]);
						array3[l].Add(triangles[n + 2]);
						continue;
					}
					List<int>[,] array5 = array4;
					int num11 = num6;
					int num12 = l;
					(array5[num11, num12] ?? (array5[num11, num12] = new List<int>())).AddRange(new int[3]
					{
						triangles[n],
						triangles[n + 1],
						triangles[n + 2]
					});
					flag = true;
				}
			}
			if (!flag)
			{
				continue;
			}
			Mesh mesh = UnityEngine.Object.Instantiate(sharedMesh);
			mesh.name = sharedMesh.name + "_portas";
			for (int num13 = 0; num13 < subMeshCount; num13++)
			{
				mesh.SetTriangles(array3[num13], num13, calculateBounds: false);
			}
			mesh.RecalculateBounds();
			meshFilter.sharedMesh = mesh;
			for (int num14 = 0; num14 < 6; num14++)
			{
				bool flag2 = false;
				for (int num15 = 0; num15 < subMeshCount; num15++)
				{
					flag2 |= array4[num14, num15] != null && array4[num14, num15].Count > 0;
				}
				if (flag2)
				{
					PartDef partDef = carDef.p[num14];
					Mesh mesh2 = Compact(sharedMesh, num14, array4, subMeshCount);
					if (!r.piv[num14])
					{
						Transform transform3 = new GameObject("peca_" + NAMES[num14]).transform;
						transform3.SetParent(transform, worldPositionStays: false);
						transform3.localPosition = partDef.pivot;
						r.piv[num14] = transform3;
						AttachLining(car, num14, transform3);
					}
					GameObject gameObject = new GameObject("malha");
					gameObject.layer = meshFilter.gameObject.layer;
					gameObject.transform.SetParent(r.piv[num14], worldPositionStays: false);
					gameObject.transform.localPosition = -partDef.pivot;
					if (!isIdentity)
					{
						gameObject.transform.localPosition = -partDef.pivot;
					}
					gameObject.AddComponent<MeshFilter>().sharedMesh = (isIdentity ? mesh2 : Transformed(mesh2, m));
					MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
					meshRenderer.sharedMaterials = component.sharedMaterials;
					meshRenderer.shadowCastingMode = component.shadowCastingMode;
					meshRenderer.receiveShadows = component.receiveShadows;
					meshRenderer.lightProbeUsage = component.lightProbeUsage;
					meshRenderer.reflectionProbeUsage = component.reflectionProbeUsage;
					meshRenderer.motionVectorGenerationMode = component.motionVectorGenerationMode;
					MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
					if (component.HasPropertyBlock())
					{
						component.GetPropertyBlock(materialPropertyBlock);
						meshRenderer.SetPropertyBlock(materialPropertyBlock);
					}
					r.extra.Add(meshRenderer);
					num++;
				}
			}
		}
		if (num == 0)
		{
			return false;
		}
		r.lg = car.shell.GetComponent<LODGroup>();
		if ((bool)r.lg)
		{
			LOD[] lODs = r.lg.GetLODs();
			if (lODs.Length != 0)
			{
				List<Renderer> list = new List<Renderer>(lODs[0].renderers);
				list.AddRange(r.extra);
				lODs[0].renderers = list.ToArray();
				r.lg.SetLODs(lODs);
			}
		}
		return true;
	}

	private static void AttachLining(FBCar car, int j, Transform piv)
	{
		if (j > 3 || !car.shell)
		{
			return;
		}
		string text = NAMES[j].Substring(5);
		List<Transform> list = new List<Transform>();
		Transform transform = car.shell.Find("interior");
		if ((bool)transform)
		{
			Transform transform2 = transform.Find("forro_" + text);
			if ((bool)transform2)
			{
				list.Add(transform2);
			}
		}
		Transform transform3 = car.shell.Find("reb_parts");
		if ((bool)transform3)
		{
			Transform[] componentsInChildren = transform3.GetComponentsInChildren<Transform>(includeInactive: true);
			foreach (Transform transform4 in componentsInChildren)
			{
				if (transform4.name.StartsWith("forro_" + text + "_"))
				{
					list.Add(transform4);
				}
			}
		}
		foreach (Transform item in list)
		{
			item.SetParent(piv, worldPositionStays: true);
		}
	}

	private static Mesh Compact(Mesh m, int j, List<int>[,] partTris, int sub)
	{
		Dictionary<int, int> dictionary = new Dictionary<int, int>();
		List<int>[] array = new List<int>[sub];
		for (int i = 0; i < sub; i++)
		{
			array[i] = new List<int>();
			List<int> list = partTris[j, i];
			if (list == null)
			{
				continue;
			}
			foreach (int item in list)
			{
				if (!dictionary.TryGetValue(item, out var value))
				{
					value = (dictionary[item] = dictionary.Count);
				}
				array[i].Add(value);
			}
		}
		int[] array2 = new int[dictionary.Count];
		foreach (KeyValuePair<int, int> item2 in dictionary)
		{
			array2[item2.Value] = item2.Key;
		}
		Mesh mesh = new Mesh
		{
			name = m.name + "_" + NAMES[j]
		};
		if (array2.Length > 65000)
		{
			mesh.indexFormat = IndexFormat.UInt32;
		}
		Vector3[] vertices = m.vertices;
		Vector3[] array3 = new Vector3[array2.Length];
		for (int k = 0; k < array2.Length; k++)
		{
			array3[k] = vertices[array2[k]];
		}
		mesh.vertices = array3;
		if (m.HasVertexAttribute(VertexAttribute.Normal))
		{
			Vector3[] normals = m.normals;
			Vector3[] array4 = new Vector3[array2.Length];
			for (int l = 0; l < array2.Length; l++)
			{
				array4[l] = normals[array2[l]];
			}
			mesh.normals = array4;
		}
		if (m.HasVertexAttribute(VertexAttribute.Tangent))
		{
			Vector4[] tangents = m.tangents;
			Vector4[] array5 = new Vector4[array2.Length];
			for (int n = 0; n < array2.Length; n++)
			{
				array5[n] = tangents[array2[n]];
			}
			mesh.tangents = array5;
		}
		if (m.HasVertexAttribute(VertexAttribute.Color))
		{
			Color32[] colors = m.colors32;
			Color32[] array6 = new Color32[array2.Length];
			for (int num = 0; num < array2.Length; num++)
			{
				array6[num] = colors[array2[num]];
			}
			mesh.colors32 = array6;
		}
		for (int num2 = 0; num2 < 4; num2++)
		{
			if (m.HasVertexAttribute((VertexAttribute)(4 + num2)))
			{
				List<Vector4> list2 = new List<Vector4>();
				m.GetUVs(num2, list2);
				List<Vector4> list3 = new List<Vector4>(array2.Length);
				for (int num3 = 0; num3 < array2.Length; num3++)
				{
					list3.Add(list2[array2[num3]]);
				}
				mesh.SetUVs(num2, list3);
			}
		}
		mesh.subMeshCount = sub;
		for (int num4 = 0; num4 < sub; num4++)
		{
			mesh.SetTriangles(array[num4], num4, calculateBounds: false);
		}
		mesh.RecalculateBounds();
		return mesh;
	}

	private static Mesh Transformed(Mesh pm, Matrix4x4 M)
	{
		Vector3[] vertices = pm.vertices;
		for (int i = 0; i < vertices.Length; i++)
		{
			vertices[i] = M.MultiplyPoint3x4(vertices[i]);
		}
		pm.vertices = vertices;
		if (pm.HasVertexAttribute(VertexAttribute.Normal))
		{
			Vector3[] normals = pm.normals;
			for (int j = 0; j < normals.Length; j++)
			{
				normals[j] = M.MultiplyVector(normals[j]).normalized;
			}
			pm.normals = normals;
		}
		pm.RecalculateBounds();
		return pm;
	}

	private static float Ease(float x)
	{
		return x * x * (3f - 2f * x);
	}

	private static void Tick(float dt)
	{
		List<FBCar> list = null;
		foreach (KeyValuePair<FBCar, Rig> rig in rigs)
		{
			Rig value = rig.Value;
			if (!rig.Key)
			{
				(list ?? (list = new List<FBCar>())).Add(rig.Key);
				continue;
			}
			bool flag = false;
			bool flag2 = false;
			for (int i = 0; i < 6; i++)
			{
				float num = (((value.target & (1 << i)) != 0) ? 1 : 0);
				if (value.k[i] != num)
				{
					flag = true;
				}
				if (value.k[i] > 0f || num > 0f || value.bent[i] > 0f || value.gone[i])
				{
					flag2 = true;
				}
			}
			if (value.detachPart >= 0)
			{
				flag2 = true;
			}
			if (!flag && !flag2)
			{
				if (value.forced && (bool)value.lg)
				{
					value.lg.ForceLOD(-1);
					value.forced = false;
				}
				continue;
			}
			if (!value.split && !value.failed)
			{
				value.split = Split(value);
				value.failed = !value.split;
			}
			if (!value.split)
			{
				continue;
			}
			if (value.detachPart >= 0 && (bool)value.piv[value.detachPart])
			{
				int detachPart = value.detachPart;
				value.detachPart = -1;
				Detach(value.car, detachPart, value.detachVel);
			}
			if ((bool)value.lg && flag2 && !value.forced)
			{
				value.lg.ForceLOD(0);
				value.forced = true;
			}
			CarDef carDef = Def(value.car.id);
			for (int j = 0; j < 6; j++)
			{
				if ((bool)value.piv[j] && carDef.p[j] != null)
				{
					float target = (((value.target & (1 << j)) != 0) ? 1 : 0);
					float num2 = ((j >= 4) ? 0.9f : 0.7f);
					value.k[j] = Mathf.MoveTowards(value.k[j], target, dt / num2);
					float num3 = Mathf.Max(Ease(value.k[j]), value.bent[j]);
					PartDef partDef = carDef.p[j];
					value.piv[j].localRotation = Quaternion.AngleAxis(partDef.ang * num3, partDef.axis);
					value.piv[j].localPosition = partDef.pivot + partDef.t * num3;
				}
			}
		}
		if (list == null)
		{
			return;
		}
		foreach (FBCar item in list)
		{
			rigs.Remove(item);
		}
	}

	public static void Keys(FBCar car)
	{
		Keyboard current = Keyboard.current;
		if (current == null || !car)
		{
			return;
		}
		if (current.uKey.wasPressedThisFrame)
		{
			if (HasPart(car, 15))
			{
				Toggle(car, 15);
			}
			else
			{
				FBHUD.I?.Center("ESTE VEÍCULO NÃO ABRE AS PORTAS", 1.2f);
			}
		}
		if (current.jKey.wasPressedThisFrame && HasPart(car, 32))
		{
			Toggle(car, 32);
		}
		if (current.pKey.wasPressedThisFrame && HasPart(car, 16))
		{
			Toggle(car, 16);
		}
	}

	private static FBCar NearCar(Vector3 p, float maxD)
	{
		FBCar result = null;
		float num = maxD;
		foreach (FBCar item in FBVehicles.list)
		{
			if ((bool)item && Has(item) && (!item.driver || item.driver.isPlayer) && item.remoteDriver < 0)
			{
				Bounds lbox = item.lbox;
				Vector3 vector = item.transform.InverseTransformPoint(p);
				Vector3 position = new Vector3(Mathf.Clamp(vector.x, lbox.min.x, lbox.max.x), Mathf.Clamp(vector.y, lbox.min.y, lbox.max.y), Mathf.Clamp(vector.z, lbox.min.z, lbox.max.z));
				float num2 = Vector3.Distance(item.transform.TransformPoint(position), p);
				if (num2 < num)
				{
					num = num2;
					result = item;
				}
			}
		}
		return result;
	}

	public static void Reset()
	{
		rigs.Clear();
	}

	public static string DebugInfo(FBCar car)
	{
		if (!car)
		{
			return "sem carro";
		}
		CarDef carDef = Def(car.id);
		if (carDef == null)
		{
			return "sem dados de portas";
		}
		if (!rigs.TryGetValue(car, out var r))
		{
			return $"{carDef.p.Count((PartDef x) => x != null)} peças no arquivo · não separado";
		}
		int num = r.piv.Count((Transform x) => x);
		string text = string.Join(" ", from j in Enumerable.Range(0, 6)
			where r.piv[j]
			select $"{NAMES[j]}={Quaternion.Angle(Quaternion.identity, r.piv[j].localRotation):0}°");
		return $"{carDef.p.Count((PartDef x) => x != null)} peças · separado {r.split} (falhou {r.failed}) · {num} dobradiças · {r.extra.Count} malhas · máscara {r.target} · empeno capô {r.bent[5]:0.00} · ângulos {text}";
	}

	public static void Bend(FBCar car, int part, float k)
	{
		CarDef carDef = (car ? Def(car.id) : null);
		if (carDef != null && part >= 0 && part <= 5 && carDef.p[part] != null)
		{
			if (!rigs.TryGetValue(car, out var value))
			{
				Dictionary<FBCar, Rig> dictionary = rigs;
				Rig obj = new Rig
				{
					car = car
				};
				value = obj;
				dictionary[car] = obj;
			}
			value.bent[part] = Mathf.Clamp01(Mathf.Max(value.bent[part], k));
			Ensure();
		}
	}

	public static void Unbend(FBCar car)
	{
		if (!car || !rigs.TryGetValue(car, out var value))
		{
			return;
		}
		for (int i = 0; i < 6; i++)
		{
			value.bent[i] = 0f;
			if (value.gone[i] && (bool)value.piv[i])
			{
				Renderer[] componentsInChildren = value.piv[i].GetComponentsInChildren<Renderer>(includeInactive: true);
				for (int j = 0; j < componentsInChildren.Length; j++)
				{
					componentsInChildren[j].enabled = true;
				}
			}
			value.gone[i] = false;
		}
	}

	public static float BentOf(FBCar car, int part)
	{
		if (!car || !rigs.TryGetValue(car, out var value) || part < 0 || part >= 6)
		{
			return 0f;
		}
		return value.bent[part];
	}

	public static int GoneCount(FBCar car)
	{
		if (!car || !rigs.TryGetValue(car, out var value))
		{
			return 0;
		}
		return value.gone.Count((bool g) => g);
	}

	public static bool Detach(FBCar car, int part, Vector3 vel)
	{
		CarDef carDef = (car ? Def(car.id) : null);
		if (carDef == null || part < 0 || part > 5 || carDef.p[part] == null)
		{
			return false;
		}
		if (!rigs.TryGetValue(car, out var value))
		{
			Dictionary<FBCar, Rig> dictionary = rigs;
			Rig obj = new Rig
			{
				car = car
			};
			value = obj;
			dictionary[car] = obj;
		}
		if (value.gone[part])
		{
			return false;
		}
		if (!value.split || !value.piv[part])
		{
			value.detachPart = part;
			value.detachVel = vel;
			Ensure();
			return true;
		}
		Transform transform = value.piv[part];
		MeshRenderer[] array = (from x in transform.GetComponentsInChildren<MeshRenderer>(includeInactive: true)
			where x.enabled && (bool)x.GetComponent<MeshFilter>()?.sharedMesh
			select x).ToArray();
		if (array.Length == 0)
		{
			return false;
		}
		GameObject gameObject = new GameObject("peca_solta_" + NAMES[part]);
		gameObject.layer = 8;
		gameObject.transform.SetPositionAndRotation(transform.position, transform.rotation);
		Bounds bounds = default(Bounds);
		bool flag = true;
		MeshRenderer[] array2 = array;
		foreach (MeshRenderer meshRenderer in array2)
		{
			GameObject gameObject2 = new GameObject("malha");
			gameObject2.layer = gameObject.layer;
			gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
			gameObject2.transform.SetPositionAndRotation(meshRenderer.transform.position, meshRenderer.transform.rotation);
			gameObject2.transform.localScale = meshRenderer.transform.lossyScale;
			MeshFilter component = meshRenderer.GetComponent<MeshFilter>();
			gameObject2.AddComponent<MeshFilter>().sharedMesh = component.sharedMesh;
			gameObject2.AddComponent<MeshRenderer>().sharedMaterials = meshRenderer.sharedMaterials;
			Bounds bounds2 = component.sharedMesh.bounds;
			Vector3[] array3 = new Vector3[4]
			{
				bounds2.min,
				bounds2.max,
				new Vector3(bounds2.min.x, bounds2.max.y, bounds2.min.z),
				new Vector3(bounds2.max.x, bounds2.min.y, bounds2.max.z)
			};
			foreach (Vector3 position in array3)
			{
				Vector3 vector = gameObject.transform.InverseTransformPoint(gameObject2.transform.TransformPoint(position));
				if (flag)
				{
					bounds = new Bounds(vector, Vector3.zero);
					flag = false;
				}
				else
				{
					bounds.Encapsulate(vector);
				}
			}
			meshRenderer.enabled = false;
		}
		BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
		boxCollider.center = bounds.center;
		boxCollider.size = Vector3.Max(bounds.size, Vector3.one * 0.04f);
		foreach (Collider col in car.cols)
		{
			if ((bool)col)
			{
				Physics.IgnoreCollision(boxCollider, col);
			}
		}
		FBPropBody fBPropBody = FBFisica.Make(gameObject, "metal", (part >= 4) ? 14 : 22);
		fBPropBody.rb.WakeUp();
		fBPropBody.rb.linearVelocity = vel;
		fBPropBody.rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 5f;
		FBFisica.Debris(gameObject, 45f);
		FBFisica.Sound("metal_pesado", transform.position, 0.8f, 7f);
		FBFx.Sparks(transform.position, Vector3.up);
		value.gone[part] = true;
		value.bent[part] = 0f;
		Detached++;
		return true;
	}

	public static void Boot()
	{
		Ensure();
	}
}
}
