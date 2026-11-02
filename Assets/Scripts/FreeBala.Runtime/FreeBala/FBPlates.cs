using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public static class FBPlates
{
	private static readonly string[] POOL = Pool();

	private static readonly Dictionary<string, Material> MATS = new Dictionary<string, Material>();

	private const string CH = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

	private static Texture2D baseTx;

	private static Texture2D fontTx;

	private static bool dumped;

	private static string[] Pool()
	{
		System.Random random = new System.Random(7919);
		string[] array = new string[10];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = string.Format("{0}{1}{2}{3}{4}{5}{6}", "NOQR"[random.Next(4)], "ABCDEFGHIJKLMNPQRSTUVWXYZ"[random.Next(25)], "ABCDEFGHIJKLMNPQRSTUVWXYZ"[random.Next(25)], random.Next(10), "ABCDEFGHIJKLMNPQRSTUVWXYZ"[random.Next(25)], random.Next(10), random.Next(10));
		}
		return array;
	}

	public static string Of(Color paint)
	{
		return POOL[Mathf.Abs((int)(paint.r * 255f) * 31 + (int)(paint.g * 255f) * 7 + (int)(paint.b * 255f)) % POOL.Length];
	}

	public static void Split(Transform root, string text)
	{
		Matrix4x4 worldToLocalMatrix = root.worldToLocalMatrix;
		Bounds bounds = default(Bounds);
		bool flag = true;
		MeshFilter[] componentsInChildren = root.GetComponentsInChildren<MeshFilter>(includeInactive: true);
		MeshFilter[] array = componentsInChildren;
		foreach (MeshFilter meshFilter in array)
		{
			if (!meshFilter.sharedMesh)
			{
				continue;
			}
			Bounds bounds2 = meshFilter.sharedMesh.bounds;
			Matrix4x4 matrix4x = worldToLocalMatrix * meshFilter.transform.localToWorldMatrix;
			for (int j = 0; j < 8; j++)
			{
				Vector3 vector = matrix4x.MultiplyPoint3x4(new Vector3(((j & 1) == 0) ? bounds2.min.x : bounds2.max.x, ((j & 2) == 0) ? bounds2.min.y : bounds2.max.y, ((j & 4) == 0) ? bounds2.min.z : bounds2.max.z));
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
		}
		array = componentsInChildren;
		foreach (MeshFilter meshFilter2 in array)
		{
			MeshRenderer component = meshFilter2.GetComponent<MeshRenderer>();
			Mesh sharedMesh = meshFilter2.sharedMesh;
			if (!component || !sharedMesh || !sharedMesh.isReadable)
			{
				continue;
			}
			Material[] sharedMaterials = component.sharedMaterials;
			int num = -1;
			for (int k = 0; k < Mathf.Min(sharedMaterials.Length, sharedMesh.subMeshCount); k++)
			{
				if ((bool)sharedMaterials[k] && sharedMaterials[k].name.StartsWith("atlas"))
				{
					num = k;
					break;
				}
			}
			if (num < 0)
			{
				continue;
			}
			Vector3[] vertices = sharedMesh.vertices;
			List<Vector2> list = new List<Vector2>();
			sharedMesh.GetUVs(0, list);
			if (list.Count != vertices.Length)
			{
				continue;
			}
			int[] triangles = sharedMesh.GetTriangles(num);
			Matrix4x4 m = worldToLocalMatrix * meshFilter2.transform.localToWorldMatrix;
			List<int> list2 = new List<int>(triangles.Length);
			List<int> list3 = new List<int>();
			for (int l = 0; l < triangles.Length; l += 3)
			{
				Vector3 vector2 = m.MultiplyPoint3x4(vertices[triangles[l]]);
				Vector3 vector3 = m.MultiplyPoint3x4(vertices[triangles[l + 1]]);
				Vector3 vector4 = m.MultiplyPoint3x4(vertices[triangles[l + 2]]);
				Vector3 vector5 = Vector3.Cross(vector3 - vector2, vector4 - vector2);
				float magnitude = vector5.magnitude;
				if (magnitude < 1E-09f)
				{
					list2.Add(triangles[l]);
					list2.Add(triangles[l + 1]);
					list2.Add(triangles[l + 2]);
					continue;
				}
				float f = vector5.z / magnitude;
				float num2 = (vector2.z + vector3.z + vector4.z) / 3f;
				float f2 = (vector2.x + vector3.x + vector4.x) / 3f;
				float y = list[triangles[l]].y;
				float y2 = list[triangles[l + 1]].y;
				float y3 = list[triangles[l + 2]].y;
				float x = list[triangles[l]].x;
				float x2 = list[triangles[l + 1]].x;
				float x3 = list[triangles[l + 2]].x;
				float num3 = Mathf.Min(y, Mathf.Min(y2, y3));
				float num4 = Mathf.Max(y, Mathf.Max(y2, y3));
				float num5 = Mathf.Max(Mathf.Max(x, Mathf.Max(x2, x3)) - Mathf.Min(x, Mathf.Min(x2, x3)), num4 - num3);
				((Mathf.Abs(f) > 0.75f && num3 > 0.28f && num4 < 0.72f && num5 > 0.15f && Mathf.Abs(f2) < 0.45f && (num2 > bounds.max.z - 0.6f || num2 < bounds.min.z + 0.6f)) ? list3 : list2).AddRange(new int[3]
				{
					triangles[l],
					triangles[l + 1],
					triangles[l + 2]
				});
			}
			if (list3.Count != 0)
			{
				Mesh mesh = UnityEngine.Object.Instantiate(sharedMesh);
				mesh.name = sharedMesh.name + "_placa";
				int subMeshCount = sharedMesh.subMeshCount;
				mesh.subMeshCount = subMeshCount + 1;
				for (int n = 0; n < subMeshCount; n++)
				{
					mesh.SetTriangles((n == num) ? list2.ToArray() : sharedMesh.GetTriangles(n), n);
				}
				list3 = GeoUV(mesh, list3, m);
				mesh.SetTriangles(list3, subMeshCount);
				meshFilter2.sharedMesh = mesh;
				Material[] array2 = new Material[subMeshCount + 1];
				for (int num6 = 0; num6 < subMeshCount; num6++)
				{
					array2[num6] = ((num6 < sharedMaterials.Length) ? sharedMaterials[num6] : sharedMaterials[sharedMaterials.Length - 1]);
				}
				array2[subMeshCount] = Mat(text);
				component.sharedMaterials = array2;
				Debug.Log($"[FreeBala] placa: {meshFilter2.name} → {list3.Count / 3} triângulos");
			}
		}
	}

	public static bool SetText(Transform root, string text)
	{
		if (!root || string.IsNullOrEmpty(text))
		{
			return false;
		}
		bool flag = false;
		Material material = Mat(text);
		MeshRenderer[] componentsInChildren = root.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
		foreach (MeshRenderer meshRenderer in componentsInChildren)
		{
			Material[] sharedMaterials = meshRenderer.sharedMaterials;
			bool flag2 = false;
			for (int j = 0; j < sharedMaterials.Length; j++)
			{
				if ((bool)sharedMaterials[j] && (sharedMaterials[j].name.StartsWith("veh_plate") || sharedMaterials[j].name.StartsWith("placa")) && sharedMaterials[j] != material)
				{
					sharedMaterials[j] = material;
					flag2 = true;
				}
			}
			if (flag2)
			{
				meshRenderer.sharedMaterials = sharedMaterials;
			}
			flag |= flag2 || Array.IndexOf(sharedMaterials, material) >= 0;
		}
		return flag;
	}

	private static List<int> GeoUV(Mesh nm, List<int> plate, Matrix4x4 M)
	{
		List<Vector3> list = new List<Vector3>();
		nm.GetVertices(list);
		int count = list.Count;
		List<Vector3> list2 = new List<Vector3>();
		nm.GetNormals(list2);
		List<Vector4> list3 = new List<Vector4>();
		nm.GetTangents(list3);
		List<Color32> list4 = new List<Color32>();
		nm.GetColors(list4);
		bool flag = list2.Count == count;
		bool flag2 = list3.Count == count;
		bool flag3 = list4.Count == count;
		List<Vector4>[] array = new List<Vector4>[4];
		bool[] array2 = new bool[4];
		for (int i = 0; i < 4; i++)
		{
			array[i] = new List<Vector4>();
			nm.GetUVs(i, array[i]);
			array2[i] = array[i].Count == count;
		}
		List<int> list5 = new List<int>(plate.Count);
		for (int j = 0; j < 2; j++)
		{
			List<int> list6 = new List<int>();
			for (int k = 0; k + 2 < plate.Count; k += 3)
			{
				Vector3 vector = M.MultiplyPoint3x4(list[plate[k]]);
				Vector3 vector2 = M.MultiplyPoint3x4(list[plate[k + 1]]);
				if (Vector3.Cross(rhs: M.MultiplyPoint3x4(list[plate[k + 2]]) - vector, lhs: vector2 - vector).z > 0f == (j == 0))
				{
					list6.Add(plate[k]);
					list6.Add(plate[k + 1]);
					list6.Add(plate[k + 2]);
				}
			}
			if (list6.Count == 0)
			{
				continue;
			}
			float num = 1E+09f;
			float num2 = -1E+09f;
			float num3 = 1E+09f;
			float num4 = -1E+09f;
			foreach (int item in list6)
			{
				Vector3 vector3 = M.MultiplyPoint3x4(list[item]);
				num = Mathf.Min(num, vector3.x);
				num2 = Mathf.Max(num2, vector3.x);
				num3 = Mathf.Min(num3, vector3.y);
				num4 = Mathf.Max(num4, vector3.y);
			}
			float num5 = Mathf.Max(0.0001f, num2 - num);
			float num6 = Mathf.Max(0.0001f, num4 - num3);
			bool flag4 = j == 0;
			foreach (int item2 in list6)
			{
				Vector3 vector4 = M.MultiplyPoint3x4(list[item2]);
				float x = (flag4 ? ((num2 - vector4.x) / num5) : ((vector4.x - num) / num5));
				float y = 0.339f + (vector4.y - num3) / num6 * 0.322f;
				list.Add(list[item2]);
				if (flag)
				{
					list2.Add(list2[item2]);
				}
				if (flag2)
				{
					list3.Add(list3[item2]);
				}
				if (flag3)
				{
					list4.Add(list4[item2]);
				}
				for (int l = 0; l < 4; l++)
				{
					if (array2[l])
					{
						array[l].Add((l == 0) ? new Vector4(x, y, 0f, 0f) : array[l][item2]);
					}
				}
				list5.Add(list.Count - 1);
			}
		}
		if (list.Count > 65000 && nm.indexFormat == IndexFormat.UInt16)
		{
			nm.indexFormat = IndexFormat.UInt32;
		}
		nm.SetVertices(list);
		if (flag)
		{
			nm.SetNormals(list2);
		}
		if (flag2)
		{
			nm.SetTangents(list3);
		}
		if (flag3)
		{
			nm.SetColors(list4);
		}
		for (int m = 0; m < 4; m++)
		{
			if (array2[m])
			{
				nm.SetUVs(m, array[m]);
			}
		}
		return list5;
	}

	public static Material Mat(string text)
	{
		if (MATS.TryGetValue(text, out var value) && (bool)value)
		{
			return value;
		}
		value = new Material(FBShaders.Lit)
		{
			name = "veh_plate"
		};
		value.SetTexture("_BaseMap", Bake(text));
		value.mainTexture = value.GetTexture("_BaseMap");
		value.SetFloat("_Smoothness", 0.35f);
		return MATS[text] = value;
	}

	private static Texture2D Load(string n)
	{
		TextAsset textAsset = Resources.Load<TextAsset>("UI/Placa/" + n);
		if (!textAsset)
		{
			return null;
		}
		Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
		texture2D.LoadImage(textAsset.bytes);
		return texture2D;
	}

	private static Texture Bake(string text)
	{
		if ((object)baseTx == null)
		{
			baseTx = Load("placa_base.png");
		}
		if ((object)fontTx == null)
		{
			fontTx = Load("placa_fonte.png");
		}
		Color32[] array = new Color32[262144];
		Color32 color = new Color32(240, 240, 235, 255);
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = color;
		}
		int num = Mathf.RoundToInt(173.568f);
		int num2 = Mathf.RoundToInt(338.432f);
		int num3 = num2 - num;
		if ((bool)baseTx)
		{
			for (int j = num; j < num2; j++)
			{
				for (int k = 0; k < 512; k++)
				{
					array[j * 512 + k] = baseTx.GetPixelBilinear(((float)k + 0.5f) / 512f, ((float)(j - num) + 0.5f) / (float)num3);
				}
			}
		}
		if ((bool)fontTx)
		{
			string text2 = text.ToUpperInvariant();
			if (text2.Length > 7)
			{
				text2 = text2.Substring(0, 7);
			}
			float num4 = (float)num3 * 0.52f;
			float num5 = num4 * 56f / 96f;
			float num6 = num5 * (float)text2.Length;
			float num7 = (512f - num6) / 2f + 10.24f;
			float num8 = (float)num + (float)num3 * 0.36f;
			for (int l = 0; l < text2.Length; l++)
			{
				int num9 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".IndexOf(text2[l]);
				if (num9 < 0)
				{
					continue;
				}
				float num10 = num7 + (float)l * num5;
				for (int m = Mathf.FloorToInt(num8 - num4 / 2f); (float)m < num8 + num4 / 2f; m++)
				{
					for (int n = Mathf.FloorToInt(num10); (float)n < num10 + num5; n++)
					{
						if (n >= 0 && n < 512 && m >= 0 && m < 512)
						{
							float num11 = ((float)n - num10) / num5;
							float v = ((float)m - (num8 - num4 / 2f)) / num4;
							Color pixelBilinear = fontTx.GetPixelBilinear(((float)num9 + num11) / (float)"ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".Length, v);
							if (!(pixelBilinear.a <= 0.01f))
							{
								array[m * 512 + n] = Color32.Lerp(array[m * 512 + n], new Color32(18, 18, 18, 255), pixelBilinear.a);
							}
						}
					}
				}
			}
		}
		Texture2D texture2D = new Texture2D(512, 512, TextureFormat.RGBA32, mipChain: true)
		{
			name = "placa_" + text,
			anisoLevel = 4,
			wrapMode = TextureWrapMode.Clamp
		};
		texture2D.SetPixels32(array);
		texture2D.Apply(updateMipmaps: true, makeNoLongerReadable: false);
		if (!dumped && Array.IndexOf(Environment.GetCommandLineArgs(), "-fbshow") >= 0)
		{
			dumped = true;
			try
			{
				string text3 = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste");
				Directory.CreateDirectory(text3);
				File.WriteAllBytes(Path.Combine(text3, "placa_amostra.png"), texture2D.EncodeToPNG());
			}
			catch
			{
			}
		}
		texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: true);
		return texture2D;
	}
}
}
