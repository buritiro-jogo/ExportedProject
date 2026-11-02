using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public static class FBFxLib
{
	public class Opt
	{
		public float gravity;

		public float drag;

		public ParticleSystemRenderMode mode;

		public float stretch;

		public AnimationCurve size;

		public Gradient color;

		public int tilesX = 1;

		public int tilesY = 1;

		public int row = -1;

		public bool spin;

		public Mesh mesh;

		public bool size3D;

		public bool collide;

		public float bounce = 0.3f;

		public float dampen = 0.4f;

		public int sortFudge;
	}

	public static Transform Root;

	public static readonly List<ParticleSystem> Systems = new List<ParticleSystem>();

	private static readonly Dictionary<string, Texture2D> tex = new Dictionary<string, Texture2D>();

	private static ParticleSystem.EmitParams ep;

	public static Texture2D Tex(string name)
	{
		if (tex.TryGetValue(name, out var value) && (bool)value)
		{
			return value;
		}
		value = Resources.Load<Texture2D>("FX/" + name) ?? Fallback(name);
		tex[name] = value;
		return value;
	}

	private static Texture2D Fallback(string name)
	{
		Texture2D texture2D = new Texture2D(32, 32, TextureFormat.RGBA32, mipChain: true)
		{
			name = name + "_reserva",
			wrapMode = TextureWrapMode.Clamp
		};
		Color[] array = new Color[1024];
		for (int i = 0; i < 32; i++)
		{
			for (int j = 0; j < 32; j++)
			{
				float num = ((float)j + 0.5f) / 32f * 2f - 1f;
				float num2 = ((float)i + 0.5f) / 32f * 2f - 1f;
				array[i * 32 + j] = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - Mathf.Sqrt(num * num + num2 * num2)));
			}
		}
		texture2D.SetPixels(array);
		texture2D.Apply(updateMipmaps: true, makeNoLongerReadable: true);
		return texture2D;
	}

	public static Material Mat(string template, Texture tx, Color tint, string name)
	{
		Material material = Resources.Load<Material>("FBMats/" + template);
		Material material2;
		if ((bool)material)
		{
			material2 = new Material(material);
		}
		else
		{
			bool additive = template.EndsWith("_Add");
			material2 = FBShaders.Transparent(new Material(template.StartsWith("Lit") ? FBShaders.Lit : (template.StartsWith("Unlit") ? FBShaders.Unlit : FBShaders.ParticlesUnlit)), additive);
		}
		material2.name = name;
		if ((bool)tx)
		{
			material2.mainTexture = tx;
			if (material2.HasProperty("_BaseMap"))
			{
				material2.SetTexture("_BaseMap", tx);
			}
		}
		if (material2.HasProperty("_BaseColor"))
		{
			material2.SetColor("_BaseColor", tint);
		}
		material2.color = tint;
		return material2;
	}

	public static AnimationCurve Curve(params float[] kv)
	{
		AnimationCurve animationCurve = new AnimationCurve();
		for (int i = 0; i + 1 < kv.Length; i += 2)
		{
			animationCurve.AddKey(kv[i], kv[i + 1]);
		}
		return animationCurve;
	}

	public static Gradient Fade(float inT = 0f, float outT = 1f, Color? end = null)
	{
		Gradient gradient = new Gradient();
		Color col = end ?? Color.white;
		gradient.SetKeys(new GradientColorKey[2]
		{
			new GradientColorKey(Color.white, 0f),
			new GradientColorKey(col, 1f)
		}, (!(inT > 0f)) ? new GradientAlphaKey[3]
		{
			new GradientAlphaKey(1f, 0f),
			new GradientAlphaKey(1f, outT),
			new GradientAlphaKey(0f, 1f)
		} : new GradientAlphaKey[4]
		{
			new GradientAlphaKey(0f, 0f),
			new GradientAlphaKey(1f, inT),
			new GradientAlphaKey(1f, Mathf.Max(inT, outT)),
			new GradientAlphaKey(0f, 1f)
		});
		return gradient;
	}

	public static ParticleSystem PS(string n, Material m, int max, Opt o = null)
	{
		if (o == null)
		{
			o = new Opt();
		}
		GameObject gameObject = new GameObject(n);
		gameObject.transform.SetParent(Root, worldPositionStays: false);
		ParticleSystem particleSystem = gameObject.AddComponent<ParticleSystem>();
		particleSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
		ParticleSystem.MainModule main = particleSystem.main;
		main.playOnAwake = false;
		main.maxParticles = Mathf.Max(1, max);
		main.simulationSpace = ParticleSystemSimulationSpace.World;
		main.gravityModifier = o.gravity;
		main.startSpeed = 0f;
		main.loop = true;
		main.duration = 1f;
		main.scalingMode = ParticleSystemScalingMode.Shape;
		main.startSize3D = o.size3D;
		main.startRotation3D = o.mesh != null;
		main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
		ParticleSystem.EmissionModule emission = particleSystem.emission;
		emission.enabled = false;
		ParticleSystem.ShapeModule shape = particleSystem.shape;
		shape.enabled = false;
		if (o.color != null)
		{
			ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
			colorOverLifetime.enabled = true;
			colorOverLifetime.color = o.color;
		}
		if (o.size != null)
		{
			ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
			sizeOverLifetime.enabled = true;
			sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, o.size);
		}
		if (o.drag > 0f)
		{
			ParticleSystem.LimitVelocityOverLifetimeModule limitVelocityOverLifetime = particleSystem.limitVelocityOverLifetime;
			limitVelocityOverLifetime.enabled = true;
			limitVelocityOverLifetime.limit = 0f;
			limitVelocityOverLifetime.dampen = o.drag;
			limitVelocityOverLifetime.drag = 0f;
		}
		if (o.spin)
		{
			ParticleSystem.RotationOverLifetimeModule rotationOverLifetime = particleSystem.rotationOverLifetime;
			rotationOverLifetime.enabled = true;
			rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
		}
		if (o.tilesX * o.tilesY > 1)
		{
			ParticleSystem.TextureSheetAnimationModule textureSheetAnimation = particleSystem.textureSheetAnimation;
			textureSheetAnimation.enabled = true;
			textureSheetAnimation.mode = ParticleSystemAnimationMode.Grid;
			textureSheetAnimation.numTilesX = o.tilesX;
			textureSheetAnimation.numTilesY = o.tilesY;
			if (o.row >= 0)
			{
				textureSheetAnimation.animation = ParticleSystemAnimationType.SingleRow;
				textureSheetAnimation.rowMode = ParticleSystemAnimationRowMode.Custom;
				textureSheetAnimation.rowIndex = o.row;
			}
			else
			{
				textureSheetAnimation.animation = ParticleSystemAnimationType.WholeSheet;
			}
			textureSheetAnimation.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.999f);
			textureSheetAnimation.cycleCount = 1;
		}
		if (o.collide)
		{
			ParticleSystem.CollisionModule collision = particleSystem.collision;
			collision.enabled = true;
			collision.type = ParticleSystemCollisionType.World;
			collision.mode = ParticleSystemCollisionMode.Collision3D;
			collision.collidesWith = FBWorld.SolidMask;
			collision.bounce = o.bounce;
			collision.dampen = o.dampen;
			collision.lifetimeLoss = 0f;
			collision.quality = ParticleSystemCollisionQuality.Medium;
			collision.maxCollisionShapes = 32;
			collision.enableDynamicColliders = false;
			collision.radiusScale = 0.5f;
		}
		ParticleSystemRenderer component = gameObject.GetComponent<ParticleSystemRenderer>();
		component.sharedMaterial = m;
		component.renderMode = o.mode;
		component.shadowCastingMode = ShadowCastingMode.Off;
		component.receiveShadows = false;
		component.lightProbeUsage = LightProbeUsage.Off;
		component.reflectionProbeUsage = ReflectionProbeUsage.Off;
		component.sortingFudge = o.sortFudge;
		component.minParticleSize = 0f;
		component.maxParticleSize = 2f;
		if (o.mode == ParticleSystemRenderMode.Stretch)
		{
			component.velocityScale = ((o.stretch > 0f) ? o.stretch : 0.03f);
			component.lengthScale = 1.2f;
		}
		if (o.mode == ParticleSystemRenderMode.Mesh)
		{
			if ((bool)o.mesh && o.mesh.isReadable)
			{
				component.mesh = o.mesh;
				component.alignment = ParticleSystemRenderSpace.World;
			}
			else
			{
				Debug.LogWarning("[FreeBala] " + n + ": malha de partícula sem Read/Write (" + (o.mesh ? o.mesh.name : "nula") + ") — usando billboard");
				component.renderMode = ParticleSystemRenderMode.Billboard;
			}
		}
		if (o.mode == ParticleSystemRenderMode.HorizontalBillboard)
		{
			component.alignment = ParticleSystemRenderSpace.World;
		}
		particleSystem.Play();
		Systems.Add(particleSystem);
		return particleSystem;
	}

	public static void Emit(ParticleSystem ps, Vector3 p, Vector3 v, float size, float life, Color c, float rot = 0f, float spin = 0f)
	{
		if ((bool)ps)
		{
			ep = default(ParticleSystem.EmitParams);
			ep.position = p;
			ep.velocity = v;
			ep.startSize = size;
			ep.startLifetime = life;
			ep.startColor = c;
			ep.rotation = rot;
			ep.angularVelocity = spin;
			ep.applyShapeToPosition = false;
			ps.Emit(ep, 1);
		}
	}

	public static void Emit3D(ParticleSystem ps, Vector3 p, Vector3 v, Vector3 size, float life, Color c, Vector3 rot, Vector3 spin)
	{
		if ((bool)ps)
		{
			ep = default(ParticleSystem.EmitParams);
			ep.position = p;
			ep.velocity = v;
			ep.startSize3D = size;
			ep.startLifetime = life;
			ep.startColor = c;
			ep.rotation3D = rot;
			ep.angularVelocity3D = spin;
			ep.applyShapeToPosition = false;
			ps.Emit(ep, 1);
		}
	}

	public static Vector3 Cone(Vector3 dir, float spread)
	{
		Vector3 vector = dir + UnityEngine.Random.insideUnitSphere * spread;
		if (!(vector.sqrMagnitude > 1E-06f))
		{
			return dir;
		}
		return vector.normalized;
	}

	public static Color Jit(Color c, float v, float a = 1f)
	{
		float num = UnityEngine.Random.Range(1f - v, 1f + v);
		return new Color(c.r * num, c.g * num, c.b * num, c.a * a);
	}

	public static void Tangents(Vector3 n, out Vector3 t, out Vector3 b)
	{
		t = Vector3.Cross(n, (Mathf.Abs(n.y) > 0.95f) ? Vector3.forward : Vector3.up).normalized;
		b = Vector3.Cross(t, n);
	}

	public static Mesh Quad(string name)
	{
		Mesh mesh = new Mesh
		{
			name = name
		};
		mesh.vertices = new Vector3[4]
		{
			new Vector3(-0.5f, -0.5f),
			new Vector3(0.5f, -0.5f),
			new Vector3(-0.5f, 0.5f),
			new Vector3(0.5f, 0.5f)
		};
		mesh.uv = new Vector2[4]
		{
			Vector2.zero,
			Vector2.right,
			Vector2.up,
			Vector2.one
		};
		mesh.triangles = new int[6] { 0, 2, 1, 2, 3, 1 };
		mesh.normals = new Vector3[4]
		{
			Vector3.back,
			Vector3.back,
			Vector3.back,
			Vector3.back
		};
		mesh.tangents = new Vector4[4]
		{
			new Vector4(1f, 0f, 0f, 1f),
			new Vector4(1f, 0f, 0f, 1f),
			new Vector4(1f, 0f, 0f, 1f),
			new Vector4(1f, 0f, 0f, 1f)
		};
		mesh.RecalculateBounds();
		mesh.UploadMeshData(markNoLongerReadable: true);
		return mesh;
	}

	public static Mesh Shell()
	{
		List<Vector3> list = new List<Vector3>();
		List<int> list2 = new List<int>();
		List<Vector3> list3 = new List<Vector3>();
		for (int i = 0; i <= 8; i++)
		{
			float f = (float)i * (float)Math.PI * 2f / 8f;
			Vector3 vector = new Vector3(Mathf.Cos(f) * 0.5f, 0f, Mathf.Sin(f) * 0.5f);
			list.Add(vector + Vector3.down * 0.5f);
			list.Add(vector + Vector3.up * 0.5f);
			list3.Add(vector.normalized);
			list3.Add(vector.normalized);
		}
		for (int j = 0; j < 8; j++)
		{
			int num = j * 2;
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
		int count = list.Count;
		list.Add(Vector3.down * 0.5f);
		list3.Add(Vector3.down);
		int count2 = list.Count;
		list.Add(Vector3.up * 0.5f);
		list3.Add(Vector3.up);
		for (int k = 0; k < 8; k++)
		{
			int num2 = k * 2;
			list2.AddRange(new int[6]
			{
				count,
				num2,
				num2 + 2,
				count2,
				num2 + 3,
				num2 + 1
			});
		}
		Mesh mesh = new Mesh();
		mesh.name = "Cartucho";
		mesh.SetVertices(list);
		mesh.SetNormals(list3);
		mesh.SetTriangles(list2, 0);
		mesh.RecalculateBounds();
		return mesh;
	}
}
}
