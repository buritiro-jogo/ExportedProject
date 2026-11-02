using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FreeBala
{
public class FBLampData : MonoBehaviour
{
	public Vector3[] pos = new Vector3[0];

	public bool[] sodium = new bool[0];

	private static readonly Regex RX = new Regex("poste|lamp|lumin|street_?light", RegexOptions.IgnoreCase);

	private static readonly Regex HEAD = new Regex("(^|_)(lamp|lampada|luminaria)(_on|_sodio|_sodium)?$|street_?light", RegexOptions.IgnoreCase);

	public static List<Vector3> Compute(IEnumerable<MeshRenderer> rs)
	{
		return Compute(rs, null);
	}

	public static List<Vector3> Compute(IEnumerable<MeshRenderer> rs, List<bool> sod)
	{
		List<Vector3> res = new List<Vector3>();
		bool curSod = false;
		foreach (MeshRenderer r in rs)
		{
			if (!r)
			{
				continue;
			}
			Material[] sharedMaterials = r.sharedMaterials;
			string input = r.name + " " + (r.transform.parent ? r.transform.parent.name : "") + " " + string.Join(" ", from m in sharedMaterials
				where m
				select m.name);
			if (!RX.IsMatch(input))
			{
				continue;
			}
			MeshFilter component = r.GetComponent<MeshFilter>();
			Mesh mesh = (component ? component.sharedMesh : null);
			Bounds bounds = r.bounds;
			if ((bool)mesh && mesh.isReadable)
			{
				Vector3[] vertices = mesh.vertices;
				Matrix4x4 localToWorldMatrix = r.transform.localToWorldMatrix;
				bool flag = false;
				for (int i = 0; i < Mathf.Min(mesh.subMeshCount, sharedMaterials.Length); i++)
				{
					if (!sharedMaterials[i] || !HEAD.IsMatch(sharedMaterials[i].name.Replace(" (Instance)", "")))
					{
						continue;
					}
					flag = true;
					curSod = Regex.IsMatch(sharedMaterials[i].name, "sodio|sodium", RegexOptions.IgnoreCase);
					Dictionary<Vector2Int, (Vector3, int, float)> dictionary = new Dictionary<Vector2Int, (Vector3, int, float)>();
					int[] triangles = mesh.GetTriangles(i);
					foreach (int num in triangles)
					{
						Vector3 vector = localToWorldMatrix.MultiplyPoint3x4(vertices[num]);
						Vector2Int key = new Vector2Int(Mathf.FloorToInt(vector.x / 2.5f), Mathf.FloorToInt(vector.z / 2.5f));
						dictionary.TryGetValue(key, out var value);
						dictionary[key] = (value.Item1 + vector, value.Item2 + 1, (value.Item2 == 0) ? vector.y : Mathf.Min(value.Item3, vector.y));
					}
					foreach (var value3 in dictionary.Values)
					{
						Vector3 vector2 = value3.Item1 / value3.Item2;
						Add(new Vector3(vector2.x, value3.Item3 - 0.35f, vector2.z));
					}
				}
				curSod = false;
				if (flag)
				{
					continue;
				}
				Dictionary<Vector2Int, (float, float, List<Vector3>)> dictionary2 = new Dictionary<Vector2Int, (float, float, List<Vector3>)>();
				Vector3[] array = vertices;
				foreach (Vector3 point in array)
				{
					Vector3 item = localToWorldMatrix.MultiplyPoint3x4(point);
					Vector2Int key2 = new Vector2Int(Mathf.FloorToInt(item.x / 3f), Mathf.FloorToInt(item.z / 3f));
					if (!dictionary2.TryGetValue(key2, out var value2))
					{
						value2 = (item.y, item.y, new List<Vector3>());
					}
					value2.Item1 = Mathf.Min(value2.Item1, item.y);
					value2.Item2 = Mathf.Max(value2.Item2, item.y);
					value2.Item3.Add(item);
					dictionary2[key2] = value2;
				}
				foreach (var value4 in dictionary2.Values)
				{
					if (value4.Item2 - value4.Item1 < 3f)
					{
						continue;
					}
					Vector3 zero = Vector3.zero;
					int num2 = 0;
					foreach (Vector3 item2 in value4.Item3)
					{
						if (item2.y > value4.Item2 - 0.5f)
						{
							zero += item2;
							num2++;
						}
					}
					Vector3 vector3 = zero / Mathf.Max(1, num2);
					Add(new Vector3(vector3.x, value4.Item2 - 0.9f, vector3.z));
				}
			}
			else if (!(bounds.size.y < 3f))
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
				sod?.Add(curSod);
			}
		}
	}

	public static int Measure(GameObject map)
	{
		List<bool> list = new List<bool>();
		List<Vector3> list2 = Compute(from r in map.GetComponentsInChildren<MeshRenderer>(includeInactive: true)
			where r.gameObject.layer == 8
			select r, list);
		FBLampData fBLampData = map.GetComponent<FBLampData>();
		if (!fBLampData)
		{
			fBLampData = map.AddComponent<FBLampData>();
		}
		fBLampData.pos = list2.ToArray();
		fBLampData.sodium = list.ToArray();
		Debug.Log($"[FreeBala][luz] luminárias medidas: {list2.Count} ({list.Count((bool x) => x)} de sódio)");
		return list2.Count;
	}
}
}
