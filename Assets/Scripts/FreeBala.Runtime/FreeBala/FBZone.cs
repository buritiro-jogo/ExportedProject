using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public class FBZone
{
	public class Z
	{
		public Vector3 c;

		public float r;

		public float t;

		public float dmg;

		public int ph;

		public string st = "wait";

		public (Vector3 c, float r)? from;

		public (Vector3 c, float r)? to;
	}

	private readonly FBMatch G;

	private readonly FBBrCore.Phase[] PH;

	public Z zone;

	private float tick;

	private GameObject wall;

	private GameObject ring;

	private Material wallMat;

	private Material ringMat;

	public FBZone(FBMatch g, FBBrCore.Phase[] ph)
	{
		G = g;
		PH = ph;
	}

	public static List<Vector3> AlleyPoints(FBMatch G)
	{
		List<object> list = Json.L(G.Level, "alleys");
		if (list != null)
		{
			return list.Select((object o) => Coords.FromWeb(o as List<object>)).ToList();
		}
		return new List<Vector3>();
	}

	public void Fx()
	{
		if ((bool)wall)
		{
			return;
		}
		Texture2D texture2D = new Texture2D(64, 256, TextureFormat.RGBA32, mipChain: false)
		{
			wrapMode = TextureWrapMode.Repeat
		};
		for (int i = 0; i < 256; i++)
		{
			for (int j = 0; j < 64; j++)
			{
				float num = (float)i / 255f;
				float num2 = ((num < 0.04f) ? Mathf.Lerp(0.95f, 0.55f, num / 0.04f) : ((num < 0.18f) ? Mathf.Lerp(0.55f, 0.22f, (num - 0.04f) / 0.14f) : ((num < 0.55f) ? Mathf.Lerp(0.22f, 0.06f, (num - 0.18f) / 0.37f) : Mathf.Lerp(0.06f, 0f, (num - 0.55f) / 0.45f))));
				if (j % 8 == 0)
				{
					num2 += 0.05f;
				}
				texture2D.SetPixel(j, i, new Color(1f, 1f, 1f, num2));
			}
		}
		texture2D.Apply();
		wallMat = Additive(texture2D, new Color(0.27f, 0.82f, 1f, 0.75f));
		wallMat.mainTextureScale = new Vector2(48f, 1f);
		wall = Cylinder(128, 36f, wallMat);
		ringMat = Additive(Texture2D.whiteTexture, new Color(1f, 1f, 1f, 0.55f));
		ring = Ring(160, 0.993f, 1f, ringMat);
	}

	private static Material Additive(Texture t, Color c)
	{
		Material material = new Material(FBShaders.Unlit ?? Shader.Find("Unlit/Transparent"))
		{
			mainTexture = t
		};
		material.SetTexture("_BaseMap", t);
		material.SetColor("_BaseColor", c);
		material.SetFloat("_Surface", 1f);
		material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
		material.SetOverrideTag("RenderType", "Transparent");
		if (material.renderQueue < 3000)
		{
			material.renderQueue = 3000;
		}
		material.SetFloat("_Blend", 2f);
		material.SetFloat("_Cull", 0f);
		material.SetFloat("_ZWrite", 0f);
		material.SetInt("_SrcBlend", 5);
		material.SetInt("_DstBlend", 1);
		material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
		material.renderQueue = 3100;
		return material;
	}

	public static GameObject Cylinder(int seg, float h, Material m)
	{
		GameObject gameObject = new GameObject("ZonaParede");
		Mesh mesh = new Mesh();
		List<Vector3> list = new List<Vector3>();
		List<Vector2> list2 = new List<Vector2>();
		List<int> list3 = new List<int>();
		for (int i = 0; i <= seg; i++)
		{
			float f = (float)i / (float)seg * (float)Math.PI * 2f;
			list.Add(new Vector3(Mathf.Cos(f), (0f - h) / 2f, Mathf.Sin(f)));
			list.Add(new Vector3(Mathf.Cos(f), h / 2f, Mathf.Sin(f)));
			list2.Add(new Vector2((float)i / (float)seg, 0f));
			list2.Add(new Vector2((float)i / (float)seg, 1f));
			if (i < seg)
			{
				int num = i * 2;
				list3.AddRange(new int[6]
				{
					num,
					num + 1,
					num + 2,
					num + 1,
					num + 3,
					num + 2
				});
			}
		}
		mesh.SetVertices(list);
		mesh.SetUVs(0, list2);
		mesh.SetTriangles(list3, 0);
		mesh.RecalculateBounds();
		mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
		gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
		MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
		meshRenderer.sharedMaterial = m;
		meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
		return gameObject;
	}

	public static GameObject Ring(int seg, float r0, float r1, Material m)
	{
		GameObject gameObject = new GameObject("Anel");
		Mesh mesh = new Mesh();
		List<Vector3> list = new List<Vector3>();
		List<int> list2 = new List<int>();
		for (int i = 0; i <= seg; i++)
		{
			float f = (float)i / (float)seg * (float)Math.PI * 2f;
			list.Add(new Vector3(Mathf.Cos(f) * r0, 0f, Mathf.Sin(f) * r0));
			list.Add(new Vector3(Mathf.Cos(f) * r1, 0f, Mathf.Sin(f) * r1));
			if (i < seg)
			{
				int num = i * 2;
				list2.AddRange(new int[6]
				{
					num,
					num + 1,
					num + 2,
					num + 1,
					num + 3,
					num + 2
				});
			}
		}
		mesh.SetVertices(list);
		mesh.SetTriangles(list2, 0);
		mesh.RecalculateBounds();
		gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
		MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
		meshRenderer.sharedMaterial = m;
		meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
		return gameObject;
	}

	public Z Start(Vector3 c, float r)
	{
		zone = new Z
		{
			c = c,
			r = r,
			t = PH[0].wait
		};
		tick = 0f;
		return zone;
	}

	public void Plan()
	{
		if (zone.ph >= PH.Length)
		{
			zone.to = null;
			return;
		}
		FBBrCore.Phase phase = PH[zone.ph];
		float num = Mathf.Max(4f, zone.r * phase.k);
		float lim = (zone.r - num) * 0.85f;
		List<Vector3> list = (from p in AlleyPoints(G)
			where XZ(p) <= lim
			select p).ToList();
		List<Vector3> list2 = ((list.Count > 0) ? list : (from p in FBNav.Points()
			where XZ(p) <= lim
			select p).ToList());
		if (list2.Count > 0)
		{
			zone.to = (FBUtil.Pick(list2), num);
			return;
		}
		float f = UnityEngine.Random.value * (float)Math.PI * 2f;
		float num2 = lim * Mathf.Sqrt(UnityEngine.Random.value);
		zone.to = (zone.c + new Vector3(Mathf.Cos(f) * num2, 0f, Mathf.Sin(f) * num2), num);
		float XZ(Vector3 p)
		{
			return (p - zone.c).Flat().magnitude;
		}
	}

	public void Anim(float dt)
	{
		if ((bool)wallMat)
		{
			wallMat.mainTextureOffset += new Vector2(dt * 0.004f, 0f);
		}
	}

	public void Step(float dt)
	{
		zone.t -= dt;
		if (zone.st == "wait" && zone.t <= 0f && zone.to.HasValue)
		{
			zone.st = "shrink";
			zone.from = (zone.c, zone.r);
			zone.t = PH[zone.ph].shrink;
			FBHUD.I?.MatchToast("A ZONA ESTÁ FECHANDO!", new Color(0.35f, 0.65f, 1f), 2.5f);
			FBAudio.Synth("zone");
		}
		else if (zone.st == "shrink")
		{
			FBBrCore.Phase phase = PH[zone.ph];
			float num = 1f - Mathf.Max(0f, zone.t) / phase.shrink;
			(Vector3, float) value = zone.from.Value;
			(Vector3, float) value2 = zone.to.Value;
			zone.c = Vector3.Lerp(value.Item1, value2.Item1, num);
			zone.r = value.Item2 + (value2.Item2 - value.Item2) * num;
			zone.dmg = phase.dmg;
			if (zone.t <= 0f)
			{
				zone.ph++;
				zone.st = "wait";
				zone.t = ((zone.ph < PH.Length) ? PH[zone.ph].wait : 999f);
				Plan();
			}
		}
		tick -= dt;
		if (!(tick <= 0f))
		{
			return;
		}
		tick = 1f;
		float dmg = Mathf.Max(1f, zone.dmg);
		foreach (FBCharacter item in FBMatch.Actors().ToList())
		{
			if (item.alive && (item.Pos - zone.c).Flat().magnitude > zone.r)
			{
				FBCombat.DamageActor(item, dmg, head: false, null, "zone");
				if (item.isPlayer)
				{
					FBHUD.I?.Damaged(0.16f);
				}
			}
		}
	}

	public void Visual()
	{
		FBCharacter player = FBMatch.Player;
		if ((bool)player && player.alive && zone != null && (player.Pos - zone.c).Flat().magnitude > zone.r)
		{
			Vector3 v = zone.c - player.Pos;
			float rel = FBUtil.WrapA(Mathf.Atan2(v.x, v.z) - FBPlayer.CamYawRad);
			FBHUD.I?.ZoneWarning(on: true, rel, Mathf.Max(1f, zone.dmg), Mathf.Max(0f, v.Flat().magnitude - zone.r));
		}
		else
		{
			FBHUD.I?.ZoneWarning(on: false, 0f, 0f, 0f);
		}
		if ((bool)wall && zone != null)
		{
			bool flag = zone.st == "shrink";
			wallMat.SetColor("_BaseColor", flag ? new Color(1f, 0.6f, 0.24f, 0.75f) : new Color(0.27f, 0.82f, 1f, FBBattleRoyale.PlayerHigh ? 0.22f : 0.75f));
			ringMat.SetColor("_BaseColor", flag ? new Color(1f, 0.76f, 0.48f, 0.55f) : new Color(1f, 1f, 1f, 0.55f));
			wall.transform.position = new Vector3(zone.c.x, zone.c.y + 16f, zone.c.z);
			wall.transform.localScale = new Vector3(zone.r, 1f, zone.r);
			ring.SetActive(zone.to.HasValue);
			(Vector3, float)? to = zone.to;
			if (to.HasValue)
			{
				var (vector, num) = to.GetValueOrDefault();
				ring.transform.position = new Vector3(vector.x, zone.c.y + 0.4f, vector.z);
				ring.transform.localScale = new Vector3(num, 1f, num);
			}
		}
	}

	public string Label()
	{
		if (zone != null)
		{
			if (!(zone.st == "shrink"))
			{
				if (!zone.to.HasValue)
				{
					return "ZONA FINAL";
				}
				return $"ZONA EM {Mathf.CeilToInt(Mathf.Max(0f, zone.t))}s";
			}
			return $"ZONA FECHANDO {Mathf.CeilToInt(Mathf.Max(0f, zone.t))}s";
		}
		return "";
	}

	public void Dispose()
	{
		if ((bool)wall)
		{
			UnityEngine.Object.Destroy(wall);
		}
		if ((bool)ring)
		{
			UnityEngine.Object.Destroy(ring);
		}
		zone = null;
		FBHUD.I?.ZoneWarning(on: false, 0f, 0f, 0f);
	}
}
}
