using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public class FBVegetation : MonoBehaviour
{
	public FBVegData data;

	public static Func<FBVegConfig> ConfigOverride;

	public static int Visible;

	public static int Total;

	public static FBVegetation I;

	private FBVegConfig cfg;

	private Matrix4x4[][] buf;

	private int[] cnt;

	private Vector3 lastPos;

	private Quaternion lastRot;

	private float rebuildT;

	private readonly Plane[] planes = new Plane[6];

	private readonly List<(float d, FBVegData.Cell c)> vis = new List<(float, FBVegData.Cell)>();

	private Bounds visBounds;

	public static FBVegConfig DefaultConfig()
	{
		FBVegConfig result = default(FBVegConfig);
		result.density = FBQuality.VegetationDensity;
		result.drawDistance = Mathf.Max(15f, FBQuality.VegDrawDistance);
		result.maxInstances = Mathf.Max(500, FBQuality.VegMaxInstances);
		result.shadows = FBQuality.VegShadows;
		return result;
	}

	private void OnEnable()
	{
		I = this;
		FBQuality.Changed += Apply;
		FBGfx.Changed += Apply;
		Apply();
		FBGrassCarpet.Ensure(this);
	}

	private void OnDisable()
	{
		FBQuality.Changed -= Apply;
		FBGfx.Changed -= Apply;
		if (I == this)
		{
			I = null;
			Visible = (Total = 0);
		}
	}

	private void Apply()
	{
		cfg = ((ConfigOverride != null) ? ConfigOverride() : DefaultConfig());
		cfg.density = Mathf.Clamp01(cfg.density);
		if (!data || data.kinds == null)
		{
			return;
		}
		buf = new Matrix4x4[data.kinds.Length][];
		cnt = new int[data.kinds.Length];
		for (int i = 0; i < buf.Length; i++)
		{
			buf[i] = new Matrix4x4[Mathf.Max(1, cfg.maxInstances)];
		}
		int num = 0;
		foreach (FBVegData.Cell cell in data.cells)
		{
			num += Take(cell);
		}
		Total = num;
		FBQuality.VegTotalInstances = num;
		Shader.SetGlobalVector("_FBVegFade", new Vector4(cfg.drawDistance * 0.72f, cfg.drawDistance, 0f, 0f));
		rebuildT = 0f;
		lastPos = new Vector3(1E+09f, 0f, 0f);
	}

	private int Take(FBVegData.Cell c)
	{
		if (c.m != null)
		{
			return Mathf.CeilToInt((float)c.m.Length * cfg.density);
		}
		return 0;
	}

	private void Rebuild(Camera cam)
	{
		GeometryUtility.CalculateFrustumPlanes(cam, planes);
		Vector3 position = cam.transform.position;
		vis.Clear();
		foreach (FBVegData.Cell cell in data.cells)
		{
			float num = Vector3.Distance(cell.center, position) - cell.radius;
			if (!(num > cfg.drawDistance) && cell.m != null && cell.m.Length != 0 && GeometryUtility.TestPlanesAABB(planes, new Bounds(cell.center, Vector3.one * (cell.radius * 2f))))
			{
				vis.Add((num, cell));
			}
		}
		vis.Sort(((float d, FBVegData.Cell c) a, (float d, FBVegData.Cell c) b) => a.d.CompareTo(b.d));
		for (int i = 0; i < cnt.Length; i++)
		{
			cnt[i] = 0;
		}
		int num2 = cfg.maxInstances;
		bool flag = true;
		foreach (var vi in vis)
		{
			FBVegData.Cell item = vi.c;
			int num3 = Mathf.Min(Take(item), num2);
			if (num3 <= 0)
			{
				break;
			}
			Matrix4x4[] array = buf[item.kind];
			num3 = Mathf.Min(num3, array.Length - cnt[item.kind]);
			if (num3 > 0)
			{
				Array.Copy(item.m, 0, array, cnt[item.kind], num3);
				cnt[item.kind] += num3;
				num2 -= num3;
				Bounds bounds = new Bounds(item.center, Vector3.one * (item.radius * 2f + 4f));
				if (flag)
				{
					visBounds = bounds;
					flag = false;
				}
				else
				{
					visBounds.Encapsulate(bounds);
				}
			}
		}
		lastPos = position;
		lastRot = cam.transform.rotation;
		rebuildT = 0.4f;
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBVegetation.Update");

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
		if (!main || !data || buf == null)
		{
			Visible = 0;
			return;
		}
		rebuildT -= Time.deltaTime;
		if (rebuildT <= 0f || (main.transform.position - lastPos).sqrMagnitude > 2.25f || Quaternion.Angle(main.transform.rotation, lastRot) > 4f)
		{
			Rebuild(main);
		}
		int num = 0;
		for (int i = 0; i < cnt.Length; i++)
		{
			FBVegData.Kind kind = data.kinds[i];
			if (cnt[i] != 0 && (bool)kind.mesh && (bool)kind.mat)
			{
				RenderParams renderParams = new RenderParams(kind.mat);
				renderParams.camera = main;
				renderParams.layer = 8;
				renderParams.receiveShadows = true;
				renderParams.worldBounds = visBounds;
				renderParams.lightProbeUsage = LightProbeUsage.Off;
				renderParams.shadowCastingMode = ((kind.shadows && cfg.shadows) ? ShadowCastingMode.On : ShadowCastingMode.Off);
				RenderParams rparams = renderParams;
				for (int j = 0; j < cnt[i]; j += 1023)
				{
					Graphics.RenderMeshInstanced(in rparams, kind.mesh, 0, buf[i], Mathf.Min(1023, cnt[i] - j), j);
				}
				num += cnt[i];
			}
		}
		Visible = num;
		FBQuality.VegVisibleInstances = num;
	}
}
}
