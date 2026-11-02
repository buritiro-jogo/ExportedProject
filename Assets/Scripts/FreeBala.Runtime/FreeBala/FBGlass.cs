using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public class FBGlass : MonoBehaviour
{
	public bool broken;

	private Mesh own;

	private MeshCollider mc;

	private MeshFilter mf;

	public void Break(Vector3 p, Vector3 dir, bool fromNet = false)
	{
		if (broken)
		{
			return;
		}
		dir = ((dir.sqrMagnitude > 1E-06f) ? dir.normalized : Vector3.forward);
		if (!mf)
		{
			mf = GetComponent<MeshFilter>();
			mc = GetComponent<MeshCollider>();
		}
		if ((bool)mf && (bool)mf.sharedMesh && mf.sharedMesh.subMeshCount > 1)
		{
			return;
		}
		if ((bool)mf && (bool)mc && (bool)mf.sharedMesh && mf.sharedMesh.isReadable && mf.sharedMesh.subMeshCount == 1)
		{
			if (!RemovePane(p, dir))
			{
				return;
			}
		}
		else
		{
			if ((bool)mf && (bool)mf.sharedMesh && (mf.sharedMesh.subMeshCount > 1 || mf.sharedMesh.triangles.Length > 12))
			{
				return;
			}
			broken = true;
			Renderer[] components = GetComponents<Renderer>();
			for (int i = 0; i < components.Length; i++)
			{
				components[i].enabled = false;
			}
			Collider[] components2 = GetComponents<Collider>();
			for (int i = 0; i < components2.Length; i++)
			{
				components2[i].enabled = false;
			}
		}
		FBImpactFx.Glass(p, -dir, dir);
		FBAudio.PlayAt("glass_break", p, 0.9f, 6f);
		if (!fromNet)
		{
			FBNet.I?.SendGlass(this, p);
		}
	}

	private bool RemovePane(Vector3 p, Vector3 dir)
	{
		RaycastHit hitInfo = default(RaycastHit);
		bool flag = false;
		Vector3[] array = new Vector3[6]
		{
			dir,
			-dir,
			Vector3.right,
			Vector3.left,
			Vector3.forward,
			Vector3.back
		};
		foreach (Vector3 vector in array)
		{
			if (mc.Raycast(new Ray(p - vector * 0.3f, vector), out hitInfo, 0.8f))
			{
				flag = true;
				break;
			}
		}
		if (!flag)
		{
			return false;
		}
		if (!own)
		{
			own = Object.Instantiate(mf.sharedMesh);
			own.name = mf.sharedMesh.name + "_q";
			mf.sharedMesh = own;
		}
		Vector3[] vertices = own.vertices;
		int[] triangles = own.triangles;
		int num = triangles.Length / 3;
		if (hitInfo.triangleIndex < 0 || hitInfo.triangleIndex >= num)
		{
			return false;
		}
		Dictionary<Vector3Int, int> dictionary = new Dictionary<Vector3Int, int>();
		int[] array2 = new int[vertices.Length];
		for (int j = 0; j < vertices.Length; j++)
		{
			Vector3Int key = Vector3Int.RoundToInt(vertices[j] * 500f);
			if (!dictionary.TryGetValue(key, out var value))
			{
				value = (dictionary[key] = dictionary.Count);
			}
			array2[j] = value;
		}
		Dictionary<int, List<int>> dictionary2 = new Dictionary<int, List<int>>();
		for (int k = 0; k < num; k++)
		{
			for (int l = 0; l < 3; l++)
			{
				int key2 = array2[triangles[k * 3 + l]];
				if (!dictionary2.TryGetValue(key2, out var value2))
				{
					value2 = (dictionary2[key2] = new List<int>());
				}
				value2.Add(k);
			}
		}
		HashSet<int> hashSet = new HashSet<int> { hitInfo.triangleIndex };
		Queue<int> queue = new Queue<int>();
		queue.Enqueue(hitInfo.triangleIndex);
		while (queue.Count > 0 && hashSet.Count < 400)
		{
			int num2 = queue.Dequeue();
			for (int m = 0; m < 3; m++)
			{
				foreach (int item in dictionary2[array2[triangles[num2 * 3 + m]]])
				{
					if (hashSet.Add(item))
					{
						queue.Enqueue(item);
					}
				}
			}
		}
		List<int> list2 = new List<int>(triangles.Length);
		for (int n = 0; n < num; n++)
		{
			if (!hashSet.Contains(n))
			{
				list2.Add(triangles[n * 3]);
				list2.Add(triangles[n * 3 + 1]);
				list2.Add(triangles[n * 3 + 2]);
			}
		}
		if (list2.Count == 0)
		{
			broken = true;
			GetComponent<Renderer>().enabled = false;
			mc.enabled = false;
			mc.sharedMesh = null;
			return true;
		}
		own.subMeshCount = 1;
		own.SetTriangles(list2, 0);
		mc.sharedMesh = null;
		mc.sharedMesh = own;
		return true;
	}
}
}
