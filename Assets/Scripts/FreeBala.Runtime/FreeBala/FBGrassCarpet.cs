using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public class FBGrassCarpet : MonoBehaviour
{
	public FBVegData data;

	public static int Cells;

	private const float TS = 4f;

	private const int MaxTiles = 256;

	private Mesh near;

	private Mesh far;

	private MaterialPropertyBlock mpN;

	private MaterialPropertyBlock mpF;

	private Material mat;

	private bool[] tileHas;

	private int tnx;

	private int tnz;

	private FBVegData built;

	private readonly Vector4[] tilesN = new Vector4[256];

	private readonly Vector4[] tilesF = new Vector4[256];

	private readonly Plane[] planes = new Plane[6];

	public static void Ensure(FBVegetation veg)
	{
		FBGrassCarpet fBGrassCarpet = veg.GetComponentInChildren<FBGrassCarpet>(includeInactive: true);
		FBVegData fBVegData = veg.data;
		if (!fBVegData || !fBVegData.grassMask || !fBVegData.grassMat)
		{
			if ((bool)fBGrassCarpet)
			{
				fBGrassCarpet.enabled = false;
			}
			return;
		}
		if (!fBGrassCarpet)
		{
			GameObject obj = new GameObject("FB_GramaTapete");
			obj.transform.SetParent(veg.transform, worldPositionStays: false);
			fBGrassCarpet = obj.AddComponent<FBGrassCarpet>();
		}
		fBGrassCarpet.data = fBVegData;
		fBGrassCarpet.enabled = true;
	}

	private void OnEnable()
	{
		if ((object)near == null)
		{
			near = Clump(12, 1);
		}
		if ((object)far == null)
		{
			far = Clump(7, 2);
		}
		if (mpN == null)
		{
			mpN = new MaterialPropertyBlock();
		}
		if (mpF == null)
		{
			mpF = new MaterialPropertyBlock();
		}
	}

	private void OnDestroy()
	{
		if ((bool)near)
		{
			UnityEngine.Object.Destroy(near);
		}
		if ((bool)far)
		{
			UnityEngine.Object.Destroy(far);
		}
		Cells = 0;
	}

	private static Mesh Clump(int n, int seed)
	{
		System.Random rnd = new System.Random(seed * 7919);
		Vector3[] array = new Vector3[n * 5];
		Vector3[] array2 = new Vector3[n * 5];
		Vector2[] array3 = new Vector2[n * 5];
		Vector2[] array4 = new Vector2[n * 5];
		int[] array5 = new int[n * 9];
		for (int i = 0; i < n; i++)
		{
			Vector3 vector = new Vector3(R() - 0.5f, 0f, R() - 0.5f);
			float f = R() * (float)Math.PI * 2f;
			Vector3 vector2 = new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f));
			Vector2 vector3 = new Vector2(R(), (R() * 2f - 1f) * 0.8f);
			(float, float)[] array6 = new(float, float)[5]
			{
				(-1f, 0f),
				(1f, 0f),
				(-1f, 0.55f),
				(1f, 0.55f),
				(0f, 1f)
			};
			for (int j = 0; j < 5; j++)
			{
				int num = i * 5 + j;
				array[num] = vector;
				array2[num] = vector2;
				array3[num] = new Vector2(array6[j].Item1, array6[j].Item2);
				array4[num] = vector3;
			}
			int num2 = i * 5;
			int num3 = i * 9;
			array5[num3] = num2;
			array5[num3 + 1] = num2 + 2;
			array5[num3 + 2] = num2 + 1;
			array5[num3 + 3] = num2 + 1;
			array5[num3 + 4] = num2 + 2;
			array5[num3 + 5] = num2 + 3;
			array5[num3 + 6] = num2 + 2;
			array5[num3 + 7] = num2 + 4;
			array5[num3 + 8] = num2 + 3;
		}
		Mesh mesh = new Mesh();
		mesh.name = "FB_touceira_" + n;
		mesh.vertices = array;
		mesh.normals = array2;
		mesh.uv = array3;
		mesh.uv2 = array4;
		mesh.triangles = array5;
		mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
		mesh.UploadMeshData(markNoLongerReadable: true);
		return mesh;
		float R()
		{
			return (float)rnd.NextDouble();
		}
	}

	private void BuildTiles()
	{
		built = data;
		tnx = Mathf.CeilToInt(data.grassRect.z / 4f);
		tnz = Mathf.CeilToInt(data.grassRect.w / 4f);
		tileHas = new bool[tnx * tnz];
		Texture2D grassMask = data.grassMask;
		Color32[] pixels;
		try
		{
			pixels = grassMask.GetPixels32();
		}
		catch
		{
			for (int i = 0; i < tileHas.Length; i++)
			{
				tileHas[i] = true;
			}
			return;
		}
		int width = grassMask.width;
		int height = grassMask.height;
		float num = (float)width / data.grassRect.z;
		float num2 = (float)height / data.grassRect.w;
		for (int j = 0; j < height; j++)
		{
			for (int k = 0; k < width; k++)
			{
				if (pixels[j * width + k].a != 0)
				{
					int num3 = Mathf.Min(tnx - 1, (int)((float)k / num / 4f));
					int num4 = Mathf.Min(tnz - 1, (int)((float)j / num2 / 4f));
					tileHas[num4 * tnx + num3] = true;
				}
			}
		}
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBGrassCarpet.Update");

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
		Cells = 0;
		Camera main = Camera.main;
		FBGfxFlags f = FBGfx.F;
		if ((bool)main && (bool)data && (bool)data.grassMask && f.grass)
		{
			if (built != data || tileHas == null)
			{
				BuildTiles();
			}
			mat = data.grassMat;
			Vector3 position = main.transform.position;
			GeometryUtility.CalculateFrustumPlanes(main, planes);
			Vector4 rect = new Vector4(data.grassRect.x, data.grassRect.y, 1f / Mathf.Max(1f, data.grassRect.z), 1f / Mathf.Max(1f, data.grassRect.w));
			Cells += Ring(main, position, near, mpN, tilesN, rect, f.grassNearSpacing, f.grassNear, 0f, f.grassNear, f.grassNear, 1.7f, 0f, f.grassDensity, 0);
			Cells += Ring(main, position, far, mpF, tilesF, rect, f.grassFarSpacing, f.grassFar, f.grassNear, f.grassFar * 0.72f, f.grassFar, 1.6f, 0.012f, f.grassDensity, 1);
		}
	}

	private int Ring(Camera cam, Vector3 cp, Mesh mesh, MaterialPropertyBlock mp, Vector4[] tiles, Vector4 rect, float sp0, float R, float r0, float r1, float r2, float clumpK, float extraW, float dens, int outer)
	{
		if (sp0 <= 0f || R <= 0f)
		{
			return 0;
		}
		int num = Mathf.Max(2, Mathf.RoundToInt(4f / sp0));
		float num2 = 4f / (float)num;
		float x = data.grassRect.x;
		float y = data.grassRect.y;
		float grassY = data.grassY0;
		int num3 = 0;
		int num4 = Mathf.Max(0, Mathf.FloorToInt((cp.x - R - x) / 4f));
		int num5 = Mathf.Min(tnx - 1, Mathf.FloorToInt((cp.x + R - x) / 4f));
		int num6 = Mathf.Max(0, Mathf.FloorToInt((cp.z - R - y) / 4f));
		int num7 = Mathf.Min(tnz - 1, Mathf.FloorToInt((cp.z + R - y) / 4f));
		for (int i = num6; i <= num7; i++)
		{
			if (num3 >= 256)
			{
				break;
			}
			for (int j = num4; j <= num5 && num3 < 256; j++)
			{
				if (!tileHas[i * tnx + j])
				{
					continue;
				}
				float num8 = x + (float)j * 4f;
				float num9 = y + (float)i * 4f;
				float num10 = Mathf.Max(num8 - cp.x, 0f, cp.x - (num8 + 4f));
				float num11 = Mathf.Max(num9 - cp.z, 0f, cp.z - (num9 + 4f));
				if (num10 * num10 + num11 * num11 > R * R)
				{
					continue;
				}
				if (outer == 1)
				{
					float num12 = Mathf.Max(Mathf.Abs(num8 - cp.x), Mathf.Abs(num8 + 4f - cp.x));
					float num13 = Mathf.Max(Mathf.Abs(num9 - cp.z), Mathf.Abs(num9 + 4f - cp.z));
					if (num12 * num12 + num13 * num13 < (r0 - 2f) * (r0 - 2f))
					{
						continue;
					}
				}
				if (GeometryUtility.TestPlanesAABB(planes, new Bounds(new Vector3(num8 + 2f, grassY + 3f, num9 + 2f), new Vector3(5f, 12f, 5f))))
				{
					tiles[num3++] = new Vector4(num8, num9, 0f, 0f);
				}
			}
		}
		if (num3 == 0)
		{
			return 0;
		}
		mp.SetVector("_GC_Grid", new Vector4(0f, 0f, num2, num));
		mp.SetVector("_GC_Ring", new Vector4(r0, r1, r2, num2 * clumpK));
		mp.SetVectorArray("_GC_Tiles", tiles);
		mp.SetVector("_GC_Rect", rect);
		mp.SetVector("_GC_Misc", new Vector4(grassY, dens, extraW, outer));
		mp.SetTexture("_GC_Mask", data.grassMask);
		mp.SetTexture("_GC_HeightTex", data.grassHeight);
		RenderParams renderParams = new RenderParams(mat);
		renderParams.camera = cam;
		renderParams.layer = 8;
		renderParams.matProps = mp;
		renderParams.receiveShadows = true;
		renderParams.shadowCastingMode = ShadowCastingMode.Off;
		renderParams.lightProbeUsage = LightProbeUsage.Off;
		renderParams.reflectionProbeUsage = ReflectionProbeUsage.Off;
		renderParams.worldBounds = new Bounds(new Vector3(cp.x, grassY + 2f, cp.z), new Vector3(2f * R + 8f, 60f, 2f * R + 8f));
		RenderParams rparams = renderParams;
		int num14 = num3 * num * num;
		Graphics.RenderMeshPrimitives(in rparams, mesh, 0, num14);
		return num14;
	}
}
}
