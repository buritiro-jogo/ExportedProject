using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FreeBala
{
public static class FBMirrors
{
	private static Material chrome;

	private static Material live;

	private static RenderTexture rt;

	private static Camera cam;

	private static FBCar camCar;

	public static Material Chrome
	{
		get
		{
			if ((bool)chrome)
			{
				return chrome;
			}
			Texture2D texture2D = new Texture2D(4, 64, TextureFormat.RGBA32, mipChain: false)
			{
				wrapMode = TextureWrapMode.Clamp,
				name = "reflexo_espelho"
			};
			for (int i = 0; i < 64; i++)
			{
				float num = (float)i / 63f;
				Color color = ((num > 0.45f) ? Color.Lerp(new Color(0.7f, 0.76f, 0.84f), new Color(0.86f, 0.9f, 0.96f), (num - 0.45f) / 0.55f) : Color.Lerp(new Color(0.22f, 0.23f, 0.25f), new Color(0.52f, 0.54f, 0.56f), num / 0.45f));
				for (int j = 0; j < 4; j++)
				{
					texture2D.SetPixel(j, i, color);
				}
			}
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: true);
			chrome = new Material(FBShaders.Unlit)
			{
				name = "veh_espelho"
			};
			chrome.SetTexture("_BaseMap", texture2D);
			chrome.mainTexture = texture2D;
			chrome.SetColor("_BaseColor", Color.white);
			return chrome;
		}
	}

	private static List<(Vector3 min, Vector3 max, Vector3 n)> Boxes(string id)
	{
		List<(Vector3, Vector3, Vector3)> list = new List<(Vector3, Vector3, Vector3)>();
		foreach (object item in Json.L(FBReb.Own(id), "espelhos") ?? new List<object>())
		{
			List<object> list2 = Json.L(item, "min");
			List<object> list3 = Json.L(item, "max");
			List<object> list4 = Json.L(item, "n");
			if (list2 != null && list3 != null)
			{
				list.Add((new Vector3(Json.Fv(list2[0]), Json.Fv(list2[1]), Json.Fv(list2[2])), new Vector3(Json.Fv(list3[0]), Json.Fv(list3[1]), Json.Fv(list3[2])), (list4 != null) ? new Vector3(Json.Fv(list4[0]), Json.Fv(list4[1]), Json.Fv(list4[2])) : Vector3.back));
			}
		}
		return list;
	}

	public static void Split(Transform root, Transform shell, string carId)
	{
		List<(Vector3, Vector3, Vector3)> list = Boxes(carId);
		if (list.Count == 0)
		{
			return;
		}
		MeshFilter[] componentsInChildren = root.GetComponentsInChildren<MeshFilter>(includeInactive: true);
		foreach (MeshFilter meshFilter in componentsInChildren)
		{
			MeshRenderer component = meshFilter.GetComponent<MeshRenderer>();
			Mesh sharedMesh = meshFilter.sharedMesh;
			if (!component || !sharedMesh || !sharedMesh.isReadable)
			{
				continue;
			}
			Material[] sharedMaterials = component.sharedMaterials;
			int num = -1;
			for (int j = 0; j < Mathf.Min(sharedMaterials.Length, sharedMesh.subMeshCount); j++)
			{
				if ((bool)sharedMaterials[j] && sharedMaterials[j].name.StartsWith("atlas"))
				{
					num = j;
					break;
				}
			}
			if (num < 0)
			{
				continue;
			}
			Vector3[] vertices = sharedMesh.vertices;
			Matrix4x4 matrix4x = shell.worldToLocalMatrix * meshFilter.transform.localToWorldMatrix;
			int[] triangles = sharedMesh.GetTriangles(num);
			List<int> list2 = new List<int>(triangles.Length);
			List<(int, int, int, int)> list3 = new List<(int, int, int, int)>();
			for (int k = 0; k + 2 < triangles.Length; k += 3)
			{
				Vector3 p2 = matrix4x.MultiplyPoint3x4(vertices[triangles[k]]);
				Vector3 p3 = matrix4x.MultiplyPoint3x4(vertices[triangles[k + 1]]);
				Vector3 p4 = matrix4x.MultiplyPoint3x4(vertices[triangles[k + 2]]);
				int num2 = -1;
				for (int l = 0; l < list.Count; l++)
				{
					if (num2 >= 0)
					{
						break;
					}
					if (In(p2, list[l]) && In(p3, list[l]) && In(p4, list[l]))
					{
						num2 = l;
					}
				}
				if (num2 < 0)
				{
					list2.Add(triangles[k]);
					list2.Add(triangles[k + 1]);
					list2.Add(triangles[k + 2]);
				}
				else
				{
					list3.Add((triangles[k], triangles[k + 1], triangles[k + 2], num2));
				}
			}
			if (list3.Count == 0)
			{
				continue;
			}
			Mesh mesh = Object.Instantiate(sharedMesh);
			mesh.name = (sharedMesh.name.EndsWith("_espelho") ? sharedMesh.name : (sharedMesh.name + "_espelho"));
			int subMeshCount = sharedMesh.subMeshCount;
			mesh.subMeshCount = subMeshCount + 1;
			for (int m = 0; m < subMeshCount; m++)
			{
				mesh.SetTriangles((m == num) ? list2.ToArray() : sharedMesh.GetTriangles(m), m);
			}
			List<Vector3> list4 = new List<Vector3>();
			mesh.GetVertices(list4);
			List<Vector3> list5 = new List<Vector3>();
			mesh.GetNormals(list5);
			bool flag = list5.Count == list4.Count;
			List<Vector2> list6 = new List<Vector2>();
			mesh.GetUVs(0, list6);
			bool flag2 = list6.Count == list4.Count;
			List<int> list7 = new List<int>();
			List<Vector2>[] array = new List<Vector2>[3];
			for (int n = 1; n < 4; n++)
			{
				array[n - 1] = new List<Vector2>();
				mesh.GetUVs(n, array[n - 1]);
			}
			foreach (var item5 in list3)
			{
				int item = item5.Item1;
				int item2 = item5.Item2;
				int item3 = item5.Item3;
				int item4 = item5.Item4;
				(Vector3, Vector3, Vector3) tuple = list[item4];
				Vector3 vector = ((tuple.Item3.sqrMagnitude > 0.01f) ? tuple.Item3.normalized : Vector3.back);
				Vector3 normalized = Vector3.ProjectOnPlane(Vector3.up, vector).normalized;
				Vector3 rhs = Vector3.Cross(normalized, vector);
				Vector3 vector2 = (tuple.Item1 + tuple.Item2) / 2f;
				float num3 = Mathf.Max(0.02f, Mathf.Abs(Vector3.Dot(tuple.Item2 - tuple.Item1, rhs)));
				float num4 = Mathf.Max(0.02f, Mathf.Abs(Vector3.Dot(tuple.Item2 - tuple.Item1, normalized)));
				int[] array2 = new int[3] { item, item2, item3 };
				foreach (int num6 in array2)
				{
					Vector3 lhs = matrix4x.MultiplyPoint3x4(vertices[num6]) - vector2;
					list4.Add(list4[num6]);
					if (flag)
					{
						list5.Add(list5[num6]);
					}
					if (flag2)
					{
						list6.Add(new Vector2(0.5f - Vector3.Dot(lhs, rhs) / num3, 0.5f + Vector3.Dot(lhs, normalized) / num4));
					}
					for (int num7 = 0; num7 < 3; num7++)
					{
						if (array[num7].Count == list4.Count - 1)
						{
							array[num7].Add(array[num7][num6]);
						}
					}
					list7.Add(list4.Count - 1);
				}
			}
			if (list4.Count > 65000)
			{
				mesh.indexFormat = IndexFormat.UInt32;
			}
			mesh.SetVertices(list4);
			if (flag)
			{
				mesh.SetNormals(list5);
			}
			if (flag2)
			{
				mesh.SetUVs(0, list6);
			}
			for (int num8 = 0; num8 < 3; num8++)
			{
				if (array[num8].Count == list4.Count)
				{
					mesh.SetUVs(num8 + 1, array[num8]);
				}
			}
			mesh.SetTriangles(list7, subMeshCount);
			mesh.RecalculateBounds();
			meshFilter.sharedMesh = mesh;
			Material[] array3 = new Material[subMeshCount + 1];
			for (int num9 = 0; num9 < subMeshCount; num9++)
			{
				int num10 = num9;
				object obj;
				if (num9 >= sharedMaterials.Length)
				{
					obj = sharedMaterials[sharedMaterials.Length - 1];
				}
				else
				{
					obj = sharedMaterials[num9];
				}
				array3[num10] = (Material)obj;
			}
			array3[subMeshCount] = Chrome;
			component.sharedMaterials = array3;
		}
		static bool In(Vector3 p, (Vector3 min, Vector3 max, Vector3 n) b)
		{
			if (p.x > b.min.x - 0.012f && p.x < b.max.x + 0.012f && p.y > b.min.y - 0.012f && p.y < b.max.y + 0.012f && p.z > b.min.z - 0.012f)
			{
				return p.z < b.max.z + 0.012f;
			}
			return false;
		}
	}

	public static void OnEnter(FBCar car)
	{
		if ((bool)car && !car.ie && Boxes(car.id).Count != 0)
		{
			if (!rt)
			{
				rt = new RenderTexture(256, 128, 16)
				{
					name = "retrovisor"
				};
				rt.Create();
			}
			if (!live)
			{
				live = new Material(FBShaders.Unlit)
				{
					name = "veh_espelho_vivo"
				};
				live.SetTexture("_BaseMap", rt);
				live.mainTexture = rt;
				live.SetColor("_BaseColor", new Color(0.92f, 0.94f, 0.96f));
			}
			if (!cam)
			{
				GameObject gameObject = new GameObject("CameraRetrovisor");
				cam = gameObject.AddComponent<Camera>();
				cam.targetTexture = rt;
				cam.fieldOfView = 55f;
				cam.nearClipPlane = 0.3f;
				cam.farClipPlane = 160f;
				cam.depth = -50f;
				UniversalAdditionalCameraData universalAdditionalCameraData = gameObject.AddComponent<UniversalAdditionalCameraData>();
				universalAdditionalCameraData.renderShadows = false;
				universalAdditionalCameraData.renderPostProcessing = false;
				universalAdditionalCameraData.antialiasing = AntialiasingMode.None;
				cam.cullingMask = (Camera.main ? (Camera.main.cullingMask & -33) : (-33));
			}
			(Vector3, Vector3, Vector3) tuple = Boxes(car.id).Find(((Vector3 min, Vector3 max, Vector3 n) b) => Mathf.Abs((b.min.x + b.max.x) / 2f) < 0.35f);
			Vector3 vector = ((tuple.Item2 != Vector3.zero) ? ((tuple.Item1 + tuple.Item2) / 2f) : new Vector3(0f, car.lbox.max.y * 0.8f, car.lbox.center.z + car.lbox.extents.z * 0.3f));
			cam.transform.SetParent(car.shell, worldPositionStays: false);
			cam.transform.localPosition = vector + Vector3.up * 0.05f;
			cam.transform.localRotation = Quaternion.Euler(4f, 180f, 0f);
			cam.gameObject.SetActive(value: true);
			Swap(car, live);
			camCar = car;
		}
	}

	public static void OnExit(FBCar car)
	{
		if ((bool)cam)
		{
			cam.gameObject.SetActive(value: false);
			cam.transform.SetParent(null, worldPositionStays: false);
		}
		if ((bool)car)
		{
			Swap(car, Chrome);
		}
		camCar = null;
	}

	private static void Swap(FBCar car, Material to)
	{
		Renderer[] componentsInChildren = car.GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer renderer in componentsInChildren)
		{
			Material[] sharedMaterials = renderer.sharedMaterials;
			bool flag = false;
			for (int j = 0; j < sharedMaterials.Length; j++)
			{
				if ((bool)sharedMaterials[j] && sharedMaterials[j].name.StartsWith("veh_espelho") && sharedMaterials[j] != to)
				{
					sharedMaterials[j] = to;
					flag = true;
				}
			}
			if (flag)
			{
				renderer.sharedMaterials = sharedMaterials;
			}
		}
	}
}
}
