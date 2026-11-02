using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public static class FBStreetPoles
{
	private static readonly Regex POLE = new Regex("poste", RegexOptions.IgnoreCase);

	private static readonly Regex ROAD = new Regex("asphalt|asfalto|road|rua|pista|faixa|lane", RegexOptions.IgnoreCase);

	private static readonly HashSet<GameObject> done = new HashSet<GameObject>();

	public static int Clean(GameObject mapRoot)
	{
		int num = 0;
		if (!mapRoot)
		{
			return 0;
		}
		foreach (Transform item in mapRoot.transform)
		{
			if (!POLE.IsMatch(item.name) || item.name.Contains("Postes_"))
			{
				continue;
			}
			Renderer[] componentsInChildren = item.GetComponentsInChildren<Renderer>();
			if (componentsInChildren.Length == 0)
			{
				continue;
			}
			Bounds bounds = componentsInChildren[0].bounds;
			Renderer[] array = componentsInChildren;
			foreach (Renderer renderer in array)
			{
				bounds.Encapsulate(renderer.bounds);
			}
			if (bounds.size.y < 3f)
			{
				continue;
			}
			HashSet<Collider> hashSet = new HashSet<Collider>(item.GetComponentsInChildren<Collider>());
			bool flag = false;
			RaycastHit[] array2 = Physics.RaycastAll(new Vector3(bounds.center.x, bounds.min.y + 1.5f, bounds.center.z), Vector3.down, 4f, 256, QueryTriggerInteraction.Ignore);
			for (int i = 0; i < array2.Length; i++)
			{
				RaycastHit h = array2[i];
				if (!hashSet.Contains(h.collider))
				{
					string text = MatName(h);
					flag = text != null && ROAD.IsMatch(text);
					break;
				}
			}
			if (!flag)
			{
				continue;
			}
			array = componentsInChildren;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].enabled = false;
			}
			foreach (Collider item2 in hashSet)
			{
				item2.enabled = false;
			}
			num++;
		}
		return num;
	}

	private static int FromList(GameObject mapRoot)
	{
		if (done.Contains(mapRoot))
		{
			return 0;
		}
		done.Add(mapRoot);
		TextAsset textAsset = Resources.Load<TextAsset>("postes_na_rua");
		if (!textAsset)
		{
			return 0;
		}
		string key = mapRoot.name.Replace("Map_", "");
		if (!(Json.Parse(textAsset.text) is Dictionary<string, object> dictionary) || !dictionary.TryGetValue(key, out var value) || !(value is List<object> list))
		{
			return 0;
		}
		int num = 0;
		foreach (object item in list)
		{
			if (!(item is List<object> { Count: >=3 } list2))
			{
				continue;
			}
			string n = list2[0] as string;
			float num2 = Convert.ToSingle(list2[1]);
			float z = Convert.ToSingle(list2[2]);
			Transform transform = FindDeep(mapRoot.transform, n);
			if (!transform)
			{
				continue;
			}
			Vector3[] array = new Vector3[1]
			{
				new Vector3(0f - num2, 0f, z)
			};
			foreach (Vector3 position in array)
			{
				Vector3 wp = mapRoot.transform.TransformPoint(position);
				if (Cut(transform, wp))
				{
					num++;
					break;
				}
			}
		}
		return num;
	}

	private static Transform FindDeep(Transform r, string n)
	{
		Transform[] componentsInChildren = r.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform in componentsInChildren)
		{
			if (transform.name == n)
			{
				return transform;
			}
		}
		return null;
	}

	private static bool Cut(Transform t, Vector3 wp)
	{
		bool result = false;
		MeshFilter[] componentsInChildren = t.GetComponentsInChildren<MeshFilter>(includeInactive: true);
		foreach (MeshFilter meshFilter in componentsInChildren)
		{
			Mesh sharedMesh = meshFilter.sharedMesh;
			if (!sharedMesh || !sharedMesh.isReadable)
			{
				continue;
			}
			Matrix4x4 localToWorldMatrix = meshFilter.transform.localToWorldMatrix;
			Vector3[] vertices = sharedMesh.vertices;
			Vector3[] W = new Vector3[vertices.Length];
			float lo = 3.4028235E+38f;
			for (int k = 0; k < vertices.Length; k++)
			{
				W[k] = localToWorldMatrix.MultiplyPoint3x4(vertices[k]);
				if (new Vector2(W[k].x - wp.x, W[k].z - wp.z).sqrMagnitude < 0.81f)
				{
					lo = Mathf.Min(lo, W[k].y);
				}
			}
			if (lo == 3.4028235E+38f)
			{
				continue;
			}
			Mesh mesh = null;
			for (int l = 0; l < sharedMesh.subMeshCount; l++)
			{
				int[] triangles = sharedMesh.GetTriangles(l);
				List<int> list = new List<int>(triangles.Length);
				bool flag = false;
				for (int m = 0; m < triangles.Length; m += 3)
				{
					if (Near(triangles[m]) && Near(triangles[m + 1]) && Near(triangles[m + 2]))
					{
						flag = true;
						continue;
					}
					list.Add(triangles[m]);
					list.Add(triangles[m + 1]);
					list.Add(triangles[m + 2]);
				}
				if (flag)
				{
					if (!mesh)
					{
						mesh = UnityEngine.Object.Instantiate(sharedMesh);
						mesh.name = sharedMesh.name + "_semposte";
					}
					mesh.SetTriangles(list, l);
				}
			}
			if (!mesh)
			{
				continue;
			}
			meshFilter.sharedMesh = mesh;
			result = true;
			MeshCollider[] components = meshFilter.GetComponents<MeshCollider>();
			foreach (MeshCollider meshCollider in components)
			{
				if (meshCollider.sharedMesh == sharedMesh)
				{
					meshCollider.sharedMesh = null;
					meshCollider.sharedMesh = mesh;
				}
			}
			bool Near(int i)
			{
				if (new Vector2(W[i].x - wp.x, W[i].z - wp.z).sqrMagnitude < 0.81f)
				{
					return W[i].y > lo + 0.05f - 0.2f;
				}
				return false;
			}
		}
		return result;
	}

	private static string MatName(RaycastHit h)
	{
		Renderer component = h.collider.GetComponent<Renderer>();
		if (!component)
		{
			return null;
		}
		Material[] sharedMaterials = component.sharedMaterials;
		MeshCollider meshCollider = h.collider as MeshCollider;
		Mesh mesh = (meshCollider ? meshCollider.sharedMesh : null);
		if (!mesh || mesh.subMeshCount <= 1 || h.triangleIndex < 0)
		{
			if (sharedMaterials.Length == 0 || !sharedMaterials[0])
			{
				return null;
			}
			return sharedMaterials[0].name;
		}
		int num = 0;
		for (int i = 0; i < mesh.subMeshCount; i++)
		{
			SubMeshDescriptor subMesh = mesh.GetSubMesh(i);
			num = (subMesh.indexStart + subMesh.indexCount) / 3;
			if (h.triangleIndex < num)
			{
				if (i >= sharedMaterials.Length || !sharedMaterials[i])
				{
					return null;
				}
				return sharedMaterials[i].name;
			}
		}
		return null;
	}
}
}
