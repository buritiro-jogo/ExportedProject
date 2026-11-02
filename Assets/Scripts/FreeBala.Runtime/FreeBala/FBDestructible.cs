using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public class FBDestructible : MonoBehaviour
{
	private class Cleaner : MonoBehaviour
	{
		private static readonly int __fbt_Update = FBTempo.Id("FBDestructible.Cleaner.Update");

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
			for (int num = debris.Count - 1; num >= 0; num--)
			{
				(GameObject, float) tuple = debris[num];
				if (!tuple.Item1 || Time.time > tuple.Item2)
				{
					if ((bool)tuple.Item1)
					{
						Object.Destroy(tuple.Item1);
					}
					debris.RemoveAt(num);
				}
				else
				{
					float num2 = tuple.Item2 - Time.time;
					if (num2 < 1f)
					{
						tuple.Item1.transform.localScale = Vector3.one * Mathf.Max(0.01f, num2);
					}
				}
			}
		}
	}

	private static readonly Regex SMALL = new Regex("cadeira|mesas?_|mesa$|lixeira|lixo|cone|caixote|caixa_feira|tambor|banco_plast|engradado|balde|vaso|placa_rua|cavalete", RegexOptions.IgnoreCase);

	private static readonly Regex PLASTIC = new Regex("plast|cadeira|cone|balde|lixeira|engradado", RegexOptions.IgnoreCase);

	private static readonly Regex WOOD = new Regex("madeira|caixote|cavalete|mesa_bar", RegexOptions.IgnoreCase);

	public float hp;

	public float max;

	private Renderer[] rends;

	private Collider[] cols;

	private bool broken;

	private Vector3 size;

	private string kind;

	private static readonly List<(GameObject g, float t)> debris = new List<(GameObject, float)>();

	private const int MaxDebris = 90;

	private static Cleaner cleaner;

	public static int Setup(GameObject mapRoot)
	{
		int num = 0;
		if (!mapRoot)
		{
			return 0;
		}
		BoxCollider[] componentsInChildren = mapRoot.GetComponentsInChildren<BoxCollider>(includeInactive: true);
		foreach (BoxCollider boxCollider in componentsInChildren)
		{
			if (boxCollider.name.StartsWith("FBCol_box"))
			{
				Transform parent = boxCollider.transform.parent;
				if ((bool)parent && SMALL.IsMatch(parent.name) && !parent.GetComponent<FBDestructible>())
				{
					FBDestructible fBDestructible = parent.gameObject.AddComponent<FBDestructible>();
					Vector3 vector = (fBDestructible.size = Vector3.Scale(boxCollider.size, boxCollider.transform.lossyScale));
					fBDestructible.kind = (PLASTIC.IsMatch(parent.name) ? "plastico" : (WOOD.IsMatch(parent.name) ? "madeira" : "metal"));
					float num2 = Mathf.Clamp(vector.x * vector.y * vector.z, 0.02f, 2f);
					fBDestructible.max = (fBDestructible.hp = (float)((fBDestructible.kind == "plastico") ? 35 : ((fBDestructible.kind == "madeira") ? 70 : 120)) * Mathf.Lerp(0.7f, 1.6f, num2 / 2f));
					fBDestructible.rends = parent.GetComponentsInChildren<Renderer>();
					fBDestructible.cols = parent.GetComponentsInChildren<Collider>();
					num++;
				}
			}
		}
		return num;
	}

	public static bool TryHit(Collider c, float dmg, Vector3 point, Vector3 dir)
	{
		FBDestructible fBDestructible = (c ? c.GetComponentInParent<FBDestructible>() : null);
		if (!fBDestructible || fBDestructible.broken)
		{
			return false;
		}
		fBDestructible.hp -= dmg;
		fBDestructible.transform.position += dir.normalized * 0.015f;
		if (fBDestructible.hp <= 0f)
		{
			fBDestructible.Break(point, dir);
		}
		return true;
	}

	private void Break(Vector3 point, Vector3 dir)
	{
		broken = true;
		Bounds bounds = new Bounds(base.transform.position, Vector3.zero);
		bool flag = true;
		Renderer[] array = rends;
		foreach (Renderer renderer in array)
		{
			if ((bool)renderer)
			{
				if (flag)
				{
					bounds = renderer.bounds;
					flag = false;
				}
				else
				{
					bounds.Encapsulate(renderer.bounds);
				}
			}
		}
		Collider[] array2 = cols;
		foreach (Collider collider in array2)
		{
			if ((bool)collider)
			{
				collider.enabled = false;
			}
		}
		int num = 0;
		array = rends;
		foreach (Renderer renderer2 in array)
		{
			if (!renderer2 || !renderer2.enabled || num >= 14)
			{
				continue;
			}
			MeshFilter component = renderer2.GetComponent<MeshFilter>();
			Mesh mesh = (component ? component.sharedMesh : null);
			if (!mesh || !mesh.isReadable)
			{
				continue;
			}
			Vector3[] V = mesh.vertices;
			Vector3[] N = mesh.normals;
			Vector2[] U = mesh.uv;
			Matrix4x4 M = renderer2.transform.localToWorldMatrix;
			Vector3[] W = new Vector3[V.Length];
			for (int k = 0; k < V.Length; k++)
			{
				W[k] = M.MultiplyPoint3x4(V[k]);
			}
			Bounds bounds2 = renderer2.bounds;
			float magnitude = bounds2.size.magnitude;
			int num2 = ((!(bounds2.size.x > magnitude * 0.3f)) ? 1 : 3);
			int num3 = ((!(bounds2.size.y > magnitude * 0.3f)) ? 1 : 2);
			int num4 = ((!(bounds2.size.z > magnitude * 0.3f)) ? 1 : 3);
			Dictionary<int, List<(int, int, int, int)>> dictionary = new Dictionary<int, List<(int, int, int, int)>>();
			for (int l = 0; l < mesh.subMeshCount; l++)
			{
				int[] triangles = mesh.GetTriangles(l);
				for (int m = 0; m < triangles.Length; m += 3)
				{
					Vector3 vector = (W[triangles[m]] + W[triangles[m + 1]] + W[triangles[m + 2]]) / 3f;
					int num5 = Mathf.Clamp((int)((vector.x - bounds2.min.x) / Mathf.Max(0.001f, bounds2.size.x) * (float)num2), 0, num2 - 1);
					int num6 = Mathf.Clamp((int)((vector.y - bounds2.min.y) / Mathf.Max(0.001f, bounds2.size.y) * (float)num3), 0, num3 - 1);
					int num7 = Mathf.Clamp((int)((vector.z - bounds2.min.z) / Mathf.Max(0.001f, bounds2.size.z) * (float)num4), 0, num4 - 1);
					int key = (num5 * 4 + num6) * 4 + num7;
					if (!dictionary.TryGetValue(key, out var value))
					{
						value = (dictionary[key] = new List<(int, int, int, int)>());
					}
					value.Add((l, triangles[m], triangles[m + 1], triangles[m + 2]));
				}
			}
			foreach (List<(int, int, int, int)> value3 in dictionary.Values)
			{
				if (num >= 14)
				{
					break;
				}
				Dictionary<int, int> map = new Dictionary<int, int>();
				List<Vector3> pv = new List<Vector3>();
				List<Vector3> pn = new List<Vector3>();
				List<Vector2> pu = new List<Vector2>();
				List<int>[] array3 = new List<int>[mesh.subMeshCount];
				for (int n = 0; n < array3.Length; n++)
				{
					array3[n] = new List<int>();
				}
				Vector3 c0 = Vector3.zero;
				foreach (var item in value3)
				{
					c0 += W[item.Item2];
				}
				c0 /= (float)value3.Count;
				foreach (var item2 in value3)
				{
					array3[item2.Item1].Add(Vtx(item2.Item2));
					array3[item2.Item1].Add(Vtx(item2.Item3));
					array3[item2.Item1].Add(Vtx(item2.Item4));
				}
				Mesh mesh2 = new Mesh
				{
					name = "pedaco"
				};
				if (pv.Count > 65000)
				{
					mesh2.indexFormat = IndexFormat.UInt32;
				}
				mesh2.SetVertices(pv);
				mesh2.SetNormals(pn);
				mesh2.SetUVs(0, pu);
				mesh2.subMeshCount = array3.Length;
				for (int num8 = 0; num8 < array3.Length; num8++)
				{
					mesh2.SetTriangles(array3[num8], num8);
				}
				mesh2.RecalculateBounds();
				GameObject obj = new GameObject("pedaco");
				obj.transform.position = c0;
				obj.AddComponent<MeshFilter>().sharedMesh = mesh2;
				MeshRenderer meshRenderer = obj.AddComponent<MeshRenderer>();
				meshRenderer.sharedMaterials = renderer2.sharedMaterials;
				meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
				BoxCollider boxCollider = obj.AddComponent<BoxCollider>();
				boxCollider.center = mesh2.bounds.center;
				boxCollider.size = Vector3.Max(mesh2.bounds.size, Vector3.one * 0.04f);
				Fly(obj, c0, point, dir, Mathf.Clamp(mesh2.bounds.size.magnitude * 4f, 0.3f, 3f));
				num++;
				int Vtx(int i)
				{
					if (map.TryGetValue(i, out var value2))
					{
						return value2;
					}
					map[i] = pv.Count;
					pv.Add(W[i] - c0);
					pn.Add((N.Length == V.Length) ? M.MultiplyVector(N[i]).normalized : Vector3.up);
					pu.Add((U.Length == V.Length) ? U[i] : Vector2.zero);
					return pv.Count - 1;
				}
			}
		}
		if (num == 0)
		{
			Material material = ((rends.Length != 0 && (bool)rends[0]) ? rends[0].sharedMaterial : null);
			Color color = (((bool)material && material.HasProperty("_BaseColor")) ? material.GetColor("_BaseColor") : new Color(0.6f, 0.6f, 0.6f));
			int num9 = Mathf.Clamp(Mathf.RoundToInt(bounds.size.magnitude * 10f), 8, 16);
			for (int num10 = 0; num10 < num9; num10++)
			{
				GameObject obj2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
				obj2.name = "lasca";
				Vector3 vector2 = new Vector3(Random.Range(bounds.min.x, bounds.max.x), Random.Range(bounds.min.y, bounds.max.y), Random.Range(bounds.min.z, bounds.max.z));
				float num11 = Mathf.Min(0.09f, bounds.size.magnitude * 0.06f) * Random.Range(0.6f, 1.3f);
				obj2.transform.SetPositionAndRotation(vector2, Random.rotation);
				obj2.transform.localScale = new Vector3(num11, num11 * Random.Range(0.2f, 0.5f), num11 * Random.Range(0.6f, 1.4f));
				Renderer component2 = obj2.GetComponent<Renderer>();
				component2.sharedMaterial = new Material(FBShaders.Lit);
				component2.sharedMaterial.SetColor("_BaseColor", color * Random.Range(0.75f, 1.05f));
				component2.shadowCastingMode = ShadowCastingMode.Off;
				Fly(obj2, vector2, point, dir, 0.3f);
			}
		}
		array = rends;
		foreach (Renderer renderer3 in array)
		{
			if ((bool)renderer3)
			{
				renderer3.enabled = false;
			}
		}
		while (debris.Count > 90)
		{
			if ((bool)debris[0].g)
			{
				Object.Destroy(debris[0].g);
			}
			debris.RemoveAt(0);
		}
		FBFx.Puff(bounds.center, (kind == "plastico") ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.62f, 0.55f, 0.45f), 0.35f, 0.6f, 0.4f);
		FBAudio.PlayAt((kind == "metal") ? "impact3" : ((kind == "madeira") ? "impact2" : "impact1"), bounds.center, 0.9f, 6f);
		if (!cleaner)
		{
			cleaner = new GameObject("FB_Cacos").AddComponent<Cleaner>();
		}
	}

	private static void Fly(GameObject g, Vector3 p, Vector3 point, Vector3 dir, float mass)
	{
		Rigidbody rigidbody = g.AddComponent<Rigidbody>();
		rigidbody.mass = mass;
		rigidbody.linearDamping = 0.3f;
		rigidbody.angularDamping = 0.6f;
		rigidbody.sleepThreshold = 0.2f;
		Vector3 vector = p - point;
		vector.y = Mathf.Max(0f, vector.y);
		rigidbody.AddForce(dir.normalized * Random.Range(1.5f, 3.5f) + vector.normalized * Random.Range(0.8f, 2f) + Vector3.up * Random.Range(1f, 2.5f), ForceMode.VelocityChange);
		rigidbody.AddTorque(Random.insideUnitSphere * 5f, ForceMode.VelocityChange);
		debris.Add((g, Time.time + Random.Range(6f, 9f)));
	}
}
}
