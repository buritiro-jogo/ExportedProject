using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FreeBala
{
public static class FBImpactFx
{
	public enum Surf
	{
		Concrete,
		Dirt,
		Sand,
		Mud,
		Metal,
		Wood,
		Glass,
		Water,
		Vegetation,
		Plaster
	}

	private static ParticleSystem psDust;

	private static ParticleSystem psChip;

	private static ParticleSystem psSplinter;

	private static ParticleSystem psLeaf;

	private static ParticleSystem psShard;

	private static ParticleSystem psSpark;

	private static ParticleSystem psSplash;

	private static ParticleSystem psRipple;

	private static ParticleSystem psFlash;

	private static bool ready;

	private static readonly Regex rxGlass = new Regex("vidro|glass|janela|window|vitr", RegexOptions.IgnoreCase);

	private static readonly Regex rxVeg = new Regex("folha|leaf|leaves|arvore|árvore|tree|palm|palmeira|bush|arbust|planta|plant|hedge|foliage|vegeta|capim", RegexOptions.IgnoreCase);

	private static readonly Regex rxWater = new Regex("agua|água|water|(^|[_\\s.\\-])(rio|lago)|igarap|mar\\b|ocean", RegexOptions.IgnoreCase);

	private static readonly Regex rxPlaster = new Regex("reboco|plaster|pintura|paint|tinta|azulejo|tile|ceram", RegexOptions.IgnoreCase);

	private static readonly Dictionary<Collider, Surf> byCollider = new Dictionary<Collider, Surf>();

	private static readonly Dictionary<(Collider, int), Surf> bySub = new Dictionary<(Collider, int), Surf>();

	private static readonly Dictionary<Mesh, int[]> subStarts = new Dictionary<Mesh, int[]>();

	private static readonly Vector4[] recent = new Vector4[8];

	private static int recentI;

	private static float Chance
	{
		get
		{
			if (FBFxQuality.Level != 0)
			{
				return 1f;
			}
			return 0.65f;
		}
	}

	public static void Warm()
	{
		if (!ready)
		{
			ready = true;
			FBFxQuality.Ensure();
			bool flag = FBFxQuality.PoolDecals > 100;
			Material m = FBFxLib.Mat("Particles_Alpha", FBFxLib.Tex("smoke"), Color.white, "ImpactoPoeira");
			psDust = FBFxLib.PS("ImpactoPoeira", m, flag ? 220 : 90, new FBFxLib.Opt
			{
				gravity = -0.02f,
				drag = 0.3f,
				tilesX = 2,
				tilesY = 2,
				spin = true,
				size = FBFxLib.Curve(0f, 0.45f, 0.3f, 1f, 1f, 1.9f),
				color = FBFxLib.Fade(0.06f, 0.3f)
			});
			Material m2 = FBFxLib.Mat("Particles_Alpha", FBFxLib.Tex("debris"), Color.white, "ImpactoDetritos");
			psChip = FBFxLib.PS("ImpactoLascas", m2, flag ? 260 : 100, D(0, 1.1f));
			psSplinter = FBFxLib.PS("ImpactoFarpas", m2, flag ? 120 : 50, D(1, 1f));
			FBFxLib.Opt opt = D(2, 0.22f);
			opt.drag = 0.25f;
			psLeaf = FBFxLib.PS("ImpactoFolhas", m2, flag ? 120 : 50, opt);
			psShard = FBFxLib.PS("ImpactoCacos", m2, flag ? 160 : 60, D(3, 1.2f));
			psSpark = FBFxLib.PS("ImpactoFaiscas", FBFxLib.Mat("Particles_Add", FBFxLib.Tex("spark"), Color.white, "ImpactoFaisca"), flag ? 260 : 100, new FBFxLib.Opt
			{
				gravity = 0.7f,
				mode = ParticleSystemRenderMode.Stretch,
				stretch = 0.035f,
				color = FBFxLib.Fade(0f, 0.4f, new Color(1f, 0.45f, 0.15f))
			});
			psFlash = FBFxLib.PS("ImpactoClarao", FBFxLib.Mat("Particles_Add", FBFxLib.Tex("spark"), Color.white, "ImpactoClarao"), 30, new FBFxLib.Opt
			{
				color = FBFxLib.Fade(0f, 0.2f)
			});
			Material m3 = FBFxLib.Mat("Particles_Alpha", FBFxLib.Tex("droplet"), new Color(0.85f, 0.92f, 1f, 1f), "AguaGota");
			psSplash = FBFxLib.PS("AguaRespingo", m3, flag ? 260 : 100, new FBFxLib.Opt
			{
				gravity = 1f,
				mode = ParticleSystemRenderMode.Stretch,
				stretch = 0.02f,
				color = FBFxLib.Fade(0f, 0.7f)
			});
			psRipple = FBFxLib.PS("AguaAnel", FBFxLib.Mat("Particles_Alpha", FBFxLib.Tex("ripple"), Color.white, "AguaAnel"), 24, new FBFxLib.Opt
			{
				mode = ParticleSystemRenderMode.HorizontalBillboard,
				size = FBFxLib.Curve(0f, 0.15f, 1f, 1f),
				color = FBFxLib.Fade(0.05f, 0.2f)
			});
		}
		static FBFxLib.Opt D(int row, float g)
		{
			return new FBFxLib.Opt
			{
				gravity = g,
				tilesX = 4,
				tilesY = 4,
				row = row,
				spin = true,
				color = FBFxLib.Fade(0f, 0.8f)
			};
		}
	}

	public static Surf Classify(Collider c, int triangleIndex = -1)
	{
		if (!c)
		{
			return Surf.Concrete;
		}
		if (c.gameObject.layer == 12)
		{
			return Surf.Glass;
		}
		FBSurface component = c.GetComponent<FBSurface>();
		if ((bool)component)
		{
			return FromKind(component.kind);
		}
		MeshCollider meshCollider = c as MeshCollider;
		Mesh mesh = (meshCollider ? meshCollider.sharedMesh : null);
		if ((bool)mesh && triangleIndex >= 0 && mesh.subMeshCount > 1 && mesh.isReadable)
		{
			int num = SubOf(mesh, triangleIndex);
			(Collider, int) key = (c, num);
			if (bySub.TryGetValue(key, out var value))
			{
				return value;
			}
			Renderer component2 = c.GetComponent<Renderer>();
			Material[] array = (component2 ? component2.sharedMaterials : null);
			value = Name(c.name + " " + ((array != null && num < array.Length && (bool)array[num]) ? array[num].name : ""));
			bySub[key] = value;
			return value;
		}
		if (byCollider.TryGetValue(c, out var value2))
		{
			return value2;
		}
		Renderer component3 = c.GetComponent<Renderer>();
		value2 = Name(c.name + " " + (((bool)component3 && (bool)component3.sharedMaterial) ? component3.sharedMaterial.name : ""));
		byCollider[c] = value2;
		return value2;
	}

	private static Surf FromKind(int k)
	{
		return k switch
		{
			1 => Surf.Dirt, 
			2 => Surf.Sand, 
			3 => Surf.Mud, 
			4 => Surf.Metal, 
			5 => Surf.Wood, 
			_ => Surf.Concrete, 
		};
	}

	private static Surf Name(string n)
	{
		if (rxGlass.IsMatch(n))
		{
			return Surf.Glass;
		}
		if (rxVeg.IsMatch(n))
		{
			return Surf.Vegetation;
		}
		if (rxWater.IsMatch(n) && !FBWater.NaoEAgua(n))
		{
			return Surf.Water;
		}
		int num = FBWorld.SurfOf(n);
		if (num != 0)
		{
			return FromKind(num);
		}
		if (!rxPlaster.IsMatch(n))
		{
			return Surf.Concrete;
		}
		return Surf.Plaster;
	}

	private static int SubOf(Mesh m, int tri)
	{
		if (!subStarts.TryGetValue(m, out var value))
		{
			value = new int[m.subMeshCount];
			for (int i = 0; i < value.Length; i++)
			{
				value[i] = m.GetSubMesh(i).indexStart / 3;
			}
			subStarts[m] = value;
		}
		for (int num = value.Length - 1; num >= 0; num--)
		{
			if (tri >= value[num])
			{
				return num;
			}
		}
		return 0;
	}

	private static bool Dup(Vector3 p)
	{
		float time = Time.time;
		Vector4[] array = recent;
		for (int i = 0; i < array.Length; i++)
		{
			Vector4 vector = array[i];
			if (time - vector.w < 0.1f && (new Vector3(vector.x, vector.y, vector.z) - p).sqrMagnitude < 0.0625f)
			{
				return true;
			}
		}
		recent[recentI = (recentI + 1) % recent.Length] = new Vector4(p.x, p.y, p.z, time);
		return false;
	}

	public static void Bullet(RaycastHit h, Vector3 dir)
	{
		Vector3 vector = h.normal;
		if (Vector3.Dot(vector, dir) > 0f)
		{
			vector = -vector;
		}
		Surface(h.point, vector, h.collider, h.triangleIndex, dir);
	}

	public static void Surface(Vector3 p, Vector3 n, Collider c, int tri = -1, Vector3? dir = null)
	{
		Warm();
		n = ((n.sqrMagnitude > 1E-06f) ? n.normalized : Vector3.up);
		Vector3 d = dir ?? (-n);
		if (tri < 0 && c is MeshCollider && c.Raycast(new Ray(p + n * 0.05f, -n), out var hitInfo, 0.15f))
		{
			tri = hitInfo.triangleIndex;
		}
		Surf s = Classify(c, tri);
		Transform mover = FBBloodFx.Mover(c);
		if ((bool)c)
		{
			Hole(s, p, n, d, mover);
		}
		if (!Dup(p) && !(Random.value > Chance))
		{
			float num = FBFxQuality.Lod(p);
			if (!(num <= 0f))
			{
				Particles(s, p, n, d, num);
			}
		}
	}

	private static void Hole(Surf s, Vector3 p, Vector3 n, Vector3 d, Transform mover)
	{
		int cell;
		float size;
		switch (s)
		{
		default:
			return;
		case Surf.Metal:
			cell = 2;
			size = Random.Range(0.05f, 0.075f);
			break;
		case Surf.Wood:
			cell = 3;
			size = Random.Range(0.07f, 0.1f);
			break;
		case Surf.Glass:
			cell = 4;
			size = Random.Range(0.14f, 0.22f);
			break;
		case Surf.Dirt:
		case Surf.Mud:
			cell = 6;
			size = Random.Range(0.06f, 0.1f);
			break;
		case Surf.Plaster:
			cell = 7;
			size = Random.Range(0.08f, 0.12f);
			break;
		case Surf.Concrete:
			cell = Random.Range(0, 2);
			size = Random.Range(0.08f, 0.13f);
			break;
		case Surf.Sand:
		case Surf.Water:
		case Surf.Vegetation:
			return;
		}
		float num = Mathf.Abs(Vector3.Dot(n, d));
		Vector3 vector = Vector3.ProjectOnPlane(d, n);
		float num2 = ((num < 0.5f && vector.sqrMagnitude > 0.0001f) ? Mathf.Clamp(1f / Mathf.Max(num, 0.4f), 1f, 1.8f) : 1f);
		FBDecalFx.Place(FBDecalFx.Atlas.Holes, cell, p, n, size, (num2 > 1f) ? new Vector3?(Vector3.Cross(vector.normalized, n)) : null, num2, mover);
	}

	private static void Particles(Surf s, Vector3 p, Vector3 n, Vector3 d, float lod)
	{
		Vector3 refl = Vector3.Reflect(d, n);
		Vector3 vector = p + n * 0.03f;
		Color color2;
		Color color;
		Color c;
		int num;
		Color c2;
		switch (s)
		{
		case Surf.Concrete:
			color2 = new Color(0.7f, 0.66f, 0.58f, 0.5f);
			goto IL_0082;
		case Surf.Plaster:
			color2 = new Color(0.82f, 0.8f, 0.76f, 0.5f);
			goto IL_0082;
		case Surf.Metal:
			Sparks(vector, n, refl, 12f, lod);
			FBFxLib.Emit(psFlash, vector, Vector3.zero, Random.Range(0.1f, 0.18f), 0.05f, new Color(1f, 0.85f, 0.6f));
			Dust(vector, n, new Color(0.35f, 0.34f, 0.33f, 0.35f), 1f, lod, 0.08f, 0.18f);
			break;
		case Surf.Wood:
			Chips(psSplinter, vector, n, refl, 7f, lod, new Color(0.85f, 0.75f, 0.62f), 0.02f, 0.05f, 3.5f);
			Dust(vector, n, new Color(0.55f, 0.44f, 0.32f, 0.45f), 2f, lod, 0.1f, 0.25f);
			break;
		case Surf.Dirt:
			color = new Color(0.47f, 0.38f, 0.27f, 0.55f);
			goto IL_0235;
		case Surf.Mud:
			color = new Color(0.27f, 0.22f, 0.16f, 0.45f);
			goto IL_0235;
		case Surf.Sand:
			color = new Color(0.8f, 0.72f, 0.56f, 0.55f);
			goto IL_0235;
		case Surf.Glass:
			Glass(vector, n, d, lod);
			break;
		case Surf.Water:
			Water(p, 1f, lod);
			break;
		case Surf.Vegetation:
			{
				Chips(psLeaf, vector, n, d, 7f, lod, Color.white, 0.025f, 0.06f, 2.2f);
				Dust(vector, n, new Color(0.36f, 0.44f, 0.24f, 0.3f), 1f, lod, 0.08f, 0.18f);
				break;
			}
			IL_0235:
			c = color;
			num = FBFxQuality.Count((s == Surf.Mud) ? 1 : 4, lod);
			for (int i = 0; i < num; i++)
			{
				FBFxLib.Emit(psDust, vector + Random.insideUnitSphere * 0.04f, (n * Random.Range(1.2f, 2.8f) + Vector3.up * Random.Range(0.5f, 1.6f)) * (1f - (float)i * 0.12f), Random.Range(0.12f, 0.3f), Random.Range(0.7f, 1.3f), FBFxLib.Jit(c, 0.1f), Random.Range(0f, 360f));
			}
			Chips(psChip, vector, n, n, (s == Surf.Sand) ? 3 : 7, lod, new Color(c.r * 0.85f, c.g * 0.85f, c.b * 0.85f), 0.012f, 0.03f, 3.5f);
			break;
			IL_0082:
			c2 = color2;
			Dust(vector, n, c2, 3f, lod, 0.12f, 0.32f);
			Chips(psChip, vector, n, refl, 6f, lod, new Color(0.62f, 0.6f, 0.56f), 0.012f, 0.035f, 4f);
			if (Random.value < 0.3f)
			{
				Sparks(vector, n, refl, 2f, lod);
			}
			break;
		}
	}

	private static void Dust(Vector3 o, Vector3 n, Color c, float count, float lod, float s0, float s1)
	{
		int num = FBFxQuality.Count(count, lod);
		if (num == 0 && lod > 0.2f)
		{
			num = FBFxQuality.Take(1);
		}
		for (int i = 0; i < num; i++)
		{
			FBFxLib.Emit(psDust, o + Random.insideUnitSphere * 0.03f, FBFxLib.Cone(n, 0.45f) * Random.Range(0.35f, 1.1f) + Vector3.up * 0.15f, Random.Range(s0, s1), Random.Range(0.6f, 1.3f), FBFxLib.Jit(c, 0.08f), Random.Range(0f, 360f));
		}
	}

	private static void Chips(ParticleSystem ps, Vector3 o, Vector3 n, Vector3 refl, float count, float lod, Color c, float s0, float s1, float speed)
	{
		int num = FBFxQuality.Count(count, lod);
		for (int i = 0; i < num; i++)
		{
			FBFxLib.Emit(ps, o, FBFxLib.Cone((Random.value < 0.5f) ? n : refl, 0.55f) * Random.Range(0.4f, 1f) * speed + Vector3.up * Random.Range(0f, 1.2f), Random.Range(s0, s1), Random.Range(0.5f, 1.1f), FBFxLib.Jit(c, 0.12f), Random.Range(0f, 360f), Random.Range(-12f, 12f));
		}
	}

	private static void Sparks(Vector3 o, Vector3 n, Vector3 refl, float count, float lod)
	{
		int num = FBFxQuality.Count(count, lod);
		for (int i = 0; i < num; i++)
		{
			FBFxLib.Emit(psSpark, o, FBFxLib.Cone((refl + n) * 0.5f, 0.7f) * Random.Range(2.5f, 7f), Random.Range(0.012f, 0.025f), Random.Range(0.12f, 0.38f), new Color(1f, Random.Range(0.75f, 0.95f), Random.Range(0.4f, 0.65f)));
		}
	}

	public static void Glass(Vector3 p, Vector3 n, Vector3 d, float lod = -1f)
	{
		Warm();
		if (lod < 0f)
		{
			lod = FBFxQuality.Lod(p);
		}
		if (!(lod <= 0f))
		{
			int num = FBFxQuality.Count(10f, lod);
			for (int i = 0; i < num; i++)
			{
				FBFxLib.Emit(psShard, p + Random.insideUnitSphere * 0.05f, FBFxLib.Cone(d, 0.6f) * Random.Range(1f, 3.5f) + n * Random.Range(0.2f, 1.5f), Random.Range(0.012f, 0.04f), Random.Range(0.6f, 1.2f), Color.white, Random.Range(0f, 360f), Random.Range(-15f, 15f));
			}
			Dust(p, n, new Color(0.88f, 0.92f, 0.94f, 0.25f), 1f, lod, 0.08f, 0.16f);
		}
	}

	public static void Water(Vector3 p, float k = 1f, float lod = -1f)
	{
		Warm();
		if (lod < 0f)
		{
			lod = FBFxQuality.Lod(p);
		}
		if (!(lod <= 0f))
		{
			int num = FBFxQuality.Count(16f * k, lod);
			for (int i = 0; i < num; i++)
			{
				FBFxLib.Emit(psSplash, p, new Vector3(Random.Range(-0.7f, 0.7f), Random.Range(2f, 4.6f), Random.Range(-0.7f, 0.7f)) * Mathf.Sqrt(k), Random.Range(0.01f, 0.03f) * Mathf.Sqrt(k), Random.Range(0.35f, 0.75f), new Color(0.85f, 0.92f, 1f, 0.7f));
			}
			FBFxLib.Emit(psRipple, p + Vector3.up * 0.01f, Vector3.zero, Random.Range(0.7f, 1.1f) * Mathf.Sqrt(k), Random.Range(0.8f, 1.2f), new Color(0.9f, 0.95f, 1f, 0.55f), Random.Range(0f, 360f));
			if (k > 0.7f)
			{
				FBFxLib.Emit(psDust, p + Vector3.up * 0.1f, Vector3.up * 0.6f, 0.25f * k, 0.5f, new Color(0.92f, 0.96f, 1f, 0.3f), Random.Range(0f, 360f));
			}
		}
	}
}
}
