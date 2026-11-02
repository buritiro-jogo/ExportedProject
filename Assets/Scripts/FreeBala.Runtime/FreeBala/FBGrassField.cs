using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public class FBGrassField : MonoBehaviour
{
	private const float CELL = 6f;

	private static readonly Regex GRASS = new Regex("grass|grama|lawn|gramado|canteiro", RegexOptions.IgnoreCase);

	private readonly Dictionary<Vector2Int, Matrix4x4[]> cells = new Dictionary<Vector2Int, Matrix4x4[]>();

	private readonly Dictionary<Collider, bool> greenCol = new Dictionary<Collider, bool>();

	private readonly List<Matrix4x4> batch = new List<Matrix4x4>(1023);

	private Mesh tuft;

	private Material mat;

	private float spacing;

	private float radius;

	private readonly Dictionary<Mesh, (int[] end, bool[] green)> subs = new Dictionary<Mesh, (int[], bool[])>();

	public static void Ensure()
	{
		if (!UnityEngine.Object.FindAnyObjectByType<FBGrassField>())
		{
			new GameObject("FB_CampoGrama").AddComponent<FBGrassField>();
		}
	}

	private void Start()
	{
		base.enabled = false;
	}

	private static Mesh BuildTuft()
	{
		List<Vector3> list = new List<Vector3>();
		List<Vector2> list2 = new List<Vector2>();
		List<int> list3 = new List<int>();
		List<Vector3> list4 = new List<Vector3>();
		System.Random random = new System.Random(11);
		for (int i = 0; i < 7; i++)
		{
			float f = (float)i / 7f * (float)Math.PI * 2f + (float)random.NextDouble() * 0.6f;
			float num = 0.22f + (float)random.NextDouble() * 0.2f;
			float num2 = 0.022f + (float)random.NextDouble() * 0.012f;
			float num3 = 0.06f + (float)random.NextDouble() * 0.1f;
			Vector3 vector = new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f));
			Vector3 vector2 = new Vector3(0f - vector.z, 0f, vector.x);
			Vector3 vector3 = vector * 0.03f;
			int count = list.Count;
			list.Add(vector3 - vector2 * num2);
			list.Add(vector3 + vector2 * num2);
			list.Add(vector3 + vector * num3 * 0.5f + Vector3.up * num * 0.55f - vector2 * num2 * 0.6f);
			list.Add(vector3 + vector * num3 * 0.5f + Vector3.up * num * 0.55f + vector2 * num2 * 0.6f);
			list.Add(vector3 + vector * num3 + Vector3.up * num);
			list2.Add(new Vector2(0f, 0f));
			list2.Add(new Vector2(1f, 0f));
			list2.Add(new Vector2(0f, 0.55f));
			list2.Add(new Vector2(1f, 0.55f));
			list2.Add(new Vector2(0.5f, 1f));
			for (int j = 0; j < 5; j++)
			{
				list4.Add(Vector3.up);
			}
			list3.AddRange(new int[9]
			{
				count,
				count + 2,
				count + 1,
				count + 1,
				count + 2,
				count + 3,
				count + 2,
				count + 4,
				count + 3
			});
		}
		Mesh mesh = new Mesh();
		mesh.name = "tufo_grama";
		mesh.SetVertices(list);
		mesh.SetUVs(0, list2);
		mesh.SetNormals(list4);
		mesh.SetTriangles(list3, 0);
		mesh.RecalculateBounds();
		return mesh;
	}

	private static Material BuildMat()
	{
		Texture2D texture2D = new Texture2D(4, 64, TextureFormat.RGB24, mipChain: true)
		{
			wrapMode = TextureWrapMode.Clamp
		};
		for (int i = 0; i < 64; i++)
		{
			float f = (float)i / 63f;
			Color color = Color.Lerp(new Color(0.11f, 0.2f, 0.05f), new Color(0.42f, 0.55f, 0.2f), Mathf.Pow(f, 0.8f));
			for (int j = 0; j < 4; j++)
			{
				texture2D.SetPixel(j, i, color);
			}
		}
		texture2D.Apply();
		Material material = Resources.Load<Material>("FBMats/Lit_Clip");
		Material obj = (material ? new Material(material) : new Material(FBShaders.Lit));
		obj.name = "grama_campo";
		obj.SetTexture("_BaseMap", texture2D);
		obj.SetColor("_BaseColor", Color.white);
		obj.SetFloat("_Smoothness", 0.05f);
		obj.SetFloat("_Cull", 0f);
		obj.enableInstancing = true;
		return obj;
	}

	private bool Green(RaycastHit h)
	{
		Collider collider = h.collider;
		if (!collider)
		{
			return false;
		}
		Renderer component = collider.GetComponent<Renderer>();
		if (!component)
		{
			return false;
		}
		Material[] sharedMaterials = component.sharedMaterials;
		MeshCollider meshCollider = collider as MeshCollider;
		Mesh mesh = (meshCollider ? meshCollider.sharedMesh : null);
		if (!mesh || mesh.subMeshCount <= 1 || h.triangleIndex < 0)
		{
			if (greenCol.TryGetValue(collider, out var value))
			{
				return value;
			}
			bool flag = sharedMaterials.Length == 1 && (bool)sharedMaterials[0] && GRASS.IsMatch(sharedMaterials[0].name);
			return greenCol[collider] = flag;
		}
		if (!subs.TryGetValue(mesh, out (int[], bool[]) value2))
		{
			int[] array = new int[mesh.subMeshCount];
			bool[] array2 = new bool[mesh.subMeshCount];
			int num = 0;
			for (int i = 0; i < mesh.subMeshCount; i++)
			{
				SubMeshDescriptor subMesh = mesh.GetSubMesh(i);
				num = subMesh.indexStart / 3 + subMesh.indexCount / 3;
				array[i] = num;
				array2[i] = i < sharedMaterials.Length && (bool)sharedMaterials[i] && GRASS.IsMatch(sharedMaterials[i].name);
			}
			value2 = (subs[mesh] = (array, array2));
		}
		for (int j = 0; j < value2.Item1.Length; j++)
		{
			if (h.triangleIndex < value2.Item1[j])
			{
				return value2.Item2[j];
			}
		}
		return false;
	}

	private Matrix4x4[] Generate(Vector2Int key)
	{
		List<Matrix4x4> list = new List<Matrix4x4>();
		System.Random random = new System.Random((key.x * 73856093) ^ (key.y * 19349663));
		float num = (float)key.x * 6f;
		float num2 = (float)key.y * 6f;
		int num3 = Mathf.CeilToInt(6f / spacing);
		for (int i = 0; i < num3; i++)
		{
			for (int j = 0; j < num3; j++)
			{
				float x = num + ((float)i + (float)random.NextDouble()) * spacing;
				float z = num2 + ((float)j + (float)random.NextDouble()) * spacing;
				if (Physics.Raycast(new Vector3(x, 400f, z), Vector3.down, out var hitInfo, 800f, 256, QueryTriggerInteraction.Ignore) && !(hitInfo.normal.y < 0.75f) && Green(hitInfo))
				{
					float num4 = 0.45f + (float)random.NextDouble() * 0.4f;
					Quaternion q = Quaternion.FromToRotation(Vector3.up, hitInfo.normal) * Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
					list.Add(Matrix4x4.TRS(hitInfo.point - hitInfo.normal * 0.01f, q, new Vector3(num4, num4 * (0.8f + (float)random.NextDouble() * 0.5f), num4)));
				}
			}
		}
		return list.ToArray();
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBGrassField.Update");

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
		Camera main = Camera.main;
		if (!main || !tuft)
		{
			return;
		}
		Vector3 position = main.transform.position;
		int num = Mathf.CeilToInt(radius / 6f);
		int num2 = Mathf.FloorToInt(position.x / 6f);
		int num3 = Mathf.FloorToInt(position.z / 6f);
		int num4 = 0;
		RenderParams renderParams = new RenderParams(mat);
		renderParams.shadowCastingMode = ShadowCastingMode.Off;
		renderParams.receiveShadows = true;
		renderParams.layer = 0;
		RenderParams rparams = renderParams;
		Plane[] planes = GeometryUtility.CalculateFrustumPlanes(main);
		batch.Clear();
		for (int i = -num; i <= num; i++)
		{
			for (int j = -num; j <= num; j++)
			{
				Vector2Int key = new Vector2Int(num2 + i, num3 + j);
				Vector3 center = new Vector3(((float)key.x + 0.5f) * 6f, position.y, ((float)key.y + 0.5f) * 6f);
				if (new Vector2(center.x - position.x, center.z - position.z).sqrMagnitude > (radius + 6f) * (radius + 6f))
				{
					continue;
				}
				Matrix4x4[] array2;
				if (!cells.TryGetValue(key, out var value))
				{
					if (num4 >= 3)
					{
						continue;
					}
					array2 = (cells[key] = Generate(key));
					value = array2;
					num4++;
				}
				if (value.Length == 0 || !GeometryUtility.TestPlanesAABB(planes, new Bounds(center, new Vector3(6f, 400f, 6f))))
				{
					continue;
				}
				array2 = value;
				foreach (Matrix4x4 item in array2)
				{
					batch.Add(item);
					if (batch.Count == 1023)
					{
						Graphics.RenderMeshInstanced(in rparams, tuft, 0, batch);
						batch.Clear();
					}
				}
			}
		}
		if (batch.Count > 0)
		{
			Graphics.RenderMeshInstanced(in rparams, tuft, 0, batch);
		}
		if (cells.Count > 900)
		{
			cells.Clear();
		}
	}
}
}
