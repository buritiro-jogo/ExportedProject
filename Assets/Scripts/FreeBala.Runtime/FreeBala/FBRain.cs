using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public class FBRain : MonoBehaviour
{
	private const int G = 32;

	private const float C = 1.5f;

	private ParticleSystem drops;

	private ParticleSystem ring;

	private ParticleSystem spray;

	private ParticleSystem.Particle[] buf;

	private readonly float[] top = new float[1024];

	private readonly bool[] water = new bool[1024];

	private readonly int[] stamp = new int[1024];

	private Vector2Int origin = new Vector2Int(-2147483648, 0);

	private int cursor;

	private int raysPerFrame;

	private float fill = 0.7f;

	private float groundFallback;

	private bool mobile;

	private GameObject puddles;

	private static Material puddleMat;

	public static FBRain I;

	private static readonly Regex Hard = new Regex("asphalt|asfalto|road|sidewalk|calcada|paver|pav_|piso|concrete|concreto|curb|slab|laje|stone|pedra|bike", RegexOptions.IgnoreCase);

	public static FBRain Begin(Transform parent, float groundY)
	{
		GameObject obj = new GameObject("FB_Chuva");
		obj.transform.SetParent(parent, worldPositionStays: false);
		FBRain fBRain = obj.AddComponent<FBRain>();
		fBRain.groundFallback = groundY;
		return fBRain;
	}

	private void Start()
	{
		I = this;
		mobile = FBQuality.Mobile;
		raysPerFrame = (mobile ? 32 : 96);
		buf = new ParticleSystem.Particle[mobile ? 2600 : 9200];
		drops = MakeDrops();
		(ring, spray) = MakeSplash();
		Shader.SetGlobalFloat("_FBRainK", 1f);
		Shader.SetGlobalFloat("_FBPuddleFill", 0f);
		BuildPuddles();
	}

	private void OnDestroy()
	{
		if (I == this)
		{
			I = null;
		}
		Shader.SetGlobalFloat("_FBPuddleFill", 0f);
		Shader.SetGlobalFloat("_FBRainK", 0f);
		if ((bool)puddles)
		{
			UnityEngine.Object.Destroy(puddles);
		}
	}

	private static Material Mat(Texture2D t)
	{
		Material material = FBShaders.Transparent(new Material(FBShaders.ParticlesUnlit));
		material.SetTexture("_BaseMap", t);
		material.mainTexture = t;
		material.SetFloat("_Cull", 0f);
		return material;
	}

	private ParticleSystem MakeDrops()
	{
		GameObject obj = new GameObject("Gotas");
		obj.transform.SetParent(base.transform, worldPositionStays: false);
		ParticleSystem particleSystem = obj.AddComponent<ParticleSystem>();
		particleSystem.Stop();
		ParticleSystem.MainModule main = particleSystem.main;
		main.loop = true;
		main.startLifetime = 2.6f;
		main.startSpeed = 0f;
		main.maxParticles = buf.Length;
		main.simulationSpace = ParticleSystemSimulationSpace.World;
		main.startSize3D = true;
		main.startSizeX = new ParticleSystem.MinMaxCurve(0.008f, 0.016f);
		main.startSizeY = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
		main.startSizeZ = 0.01f;
		main.startColor = new Color(0.78f, 0.83f, 0.92f, 0.32f);
		ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = particleSystem.velocityOverLifetime;
		velocityOverLifetime.enabled = true;
		velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
		velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
		velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(-15f, -12f);
		velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
		ParticleSystem.EmissionModule emission = particleSystem.emission;
		emission.rateOverTime = (mobile ? 1900 : 6800);
		ParticleSystem.ShapeModule shape = particleSystem.shape;
		shape.shapeType = ParticleSystemShapeType.Box;
		shape.scale = new Vector3(44f, 1f, 44f);
		ParticleSystemRenderer component = obj.GetComponent<ParticleSystemRenderer>();
		component.renderMode = ParticleSystemRenderMode.Stretch;
		component.velocityScale = 0.028f;
		component.lengthScale = 1.2f;
		component.shadowCastingMode = ShadowCastingMode.Off;
		Texture2D texture2D = new Texture2D(8, 64, TextureFormat.RGBA32, mipChain: false)
		{
			wrapMode = TextureWrapMode.Clamp
		};
		for (int i = 0; i < 64; i++)
		{
			for (int j = 0; j < 8; j++)
			{
				float num = (float)i / 63f;
				float num2 = Mathf.Abs((float)j - 3.5f) / 3.5f;
				float a = Mathf.Sin(num * (float)Math.PI) * Mathf.Pow(num, 0.4f) * (1f - num2 * num2);
				texture2D.SetPixel(j, i, new Color(1f, 1f, 1f, a));
			}
		}
		texture2D.Apply();
		component.material = Mat(texture2D);
		particleSystem.Play();
		return particleSystem;
	}

	private (ParticleSystem, ParticleSystem) MakeSplash()
	{
		GameObject gameObject = new GameObject("Respingos");
		gameObject.transform.SetParent(base.transform, worldPositionStays: false);
		ParticleSystem particleSystem = gameObject.AddComponent<ParticleSystem>();
		particleSystem.Stop();
		ParticleSystem.MainModule main = particleSystem.main;
		main.loop = true;
		main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
		main.startSpeed = 0f;
		main.maxParticles = (mobile ? 400 : 1800);
		main.simulationSpace = ParticleSystemSimulationSpace.World;
		main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
		main.startColor = new Color(0.85f, 0.9f, 1f, 0.45f);
		ParticleSystem.EmissionModule emission = particleSystem.emission;
		emission.rateOverTime = 0f;
		ParticleSystem.ShapeModule shape = particleSystem.shape;
		shape.enabled = false;
		ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
		sizeOverLifetime.enabled = true;
		sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.3f, 1f, 2.6f));
		ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
		colorOverLifetime.enabled = true;
		Gradient gradient = new Gradient();
		gradient.SetKeys(new GradientColorKey[2]
		{
			new GradientColorKey(Color.white, 0f),
			new GradientColorKey(Color.white, 1f)
		}, new GradientAlphaKey[2]
		{
			new GradientAlphaKey(1f, 0f),
			new GradientAlphaKey(0f, 1f)
		});
		colorOverLifetime.color = gradient;
		ParticleSystemRenderer component = gameObject.GetComponent<ParticleSystemRenderer>();
		component.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
		component.shadowCastingMode = ShadowCastingMode.Off;
		Texture2D texture2D = new Texture2D(32, 32, TextureFormat.RGBA32, mipChain: false)
		{
			wrapMode = TextureWrapMode.Clamp
		};
		for (int i = 0; i < 32; i++)
		{
			for (int j = 0; j < 32; j++)
			{
				float num = new Vector2((float)j - 15.5f, (float)i - 15.5f).magnitude / 15.5f;
				float a = Mathf.Clamp01(1f - Mathf.Abs(num - 0.8f) / 0.14f);
				texture2D.SetPixel(j, i, new Color(1f, 1f, 1f, a));
			}
		}
		texture2D.Apply();
		component.material = Mat(texture2D);
		particleSystem.Play();
		GameObject obj = new GameObject("Goticulas");
		obj.transform.SetParent(gameObject.transform, worldPositionStays: false);
		ParticleSystem particleSystem2 = obj.AddComponent<ParticleSystem>();
		particleSystem2.Stop();
		ParticleSystem.MainModule main2 = particleSystem2.main;
		main2.loop = true;
		main2.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
		main2.startSpeed = 0f;
		main2.gravityModifier = 1.2f;
		main2.maxParticles = (mobile ? 300 : 1400);
		main2.simulationSpace = ParticleSystemSimulationSpace.World;
		main2.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.025f);
		main2.startColor = new Color(0.85f, 0.9f, 1f, 0.5f);
		ParticleSystem.EmissionModule emission2 = particleSystem2.emission;
		emission2.rateOverTime = 0f;
		ParticleSystem.ShapeModule shape2 = particleSystem2.shape;
		shape2.enabled = false;
		ParticleSystemRenderer component2 = obj.GetComponent<ParticleSystemRenderer>();
		component2.shadowCastingMode = ShadowCastingMode.Off;
		Texture2D texture2D2 = new Texture2D(16, 16, TextureFormat.RGBA32, mipChain: false);
		for (int k = 0; k < 16; k++)
		{
			for (int l = 0; l < 16; l++)
			{
				float num2 = new Vector2((float)l - 7.5f, (float)k - 7.5f).magnitude / 7.5f;
				texture2D2.SetPixel(l, k, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - num2)));
			}
		}
		texture2D2.Apply();
		component2.material = Mat(texture2D2);
		particleSystem2.Play();
		return (particleSystem, particleSystem2);
	}

	private int Idx(int cx, int cz)
	{
		int num = cx - origin.x;
		int num2 = cz - origin.y;
		if ((uint)num >= 32u || (uint)num2 >= 32u)
		{
			return -1;
		}
		return num2 * 32 + num;
	}

	private void Refresh(Vector3 cam)
	{
		Vector2Int vector2Int = new Vector2Int(Mathf.FloorToInt(cam.x / 1.5f) - 16, Mathf.FloorToInt(cam.z / 1.5f) - 16);
		if (vector2Int != origin)
		{
			origin = vector2Int;
			Array.Clear(stamp, 0, stamp.Length);
			cursor = 0;
		}
		int num = 0;
		for (int i = 0; i < 1024; i++)
		{
			if (num >= raysPerFrame)
			{
				break;
			}
			int num2 = (cursor + i) % 1024;
			if (stamp[num2] != 0 && Time.frameCount - stamp[num2] < 90)
			{
				continue;
			}
			int num3 = num2 % 32;
			int num4 = num2 / 32;
			float x = ((float)(origin.x + num3) + 0.5f) * 1.5f;
			float z = ((float)(origin.y + num4) + 0.5f) * 1.5f;
			RaycastHit hitInfo;
			float num5 = (Physics.Raycast(new Vector3(x, cam.y + 45f, z), Vector3.down, out hitInfo, 140f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) ? hitInfo.point.y : groundFallback);
			bool flag = false;
			float? num6 = FBWorld.WaterAt(x, z);
			if (num6.HasValue)
			{
				float valueOrDefault = num6.GetValueOrDefault();
				if (valueOrDefault >= num5 - 0.05f)
				{
					num5 = valueOrDefault;
					flag = true;
				}
			}
			top[num2] = num5;
			water[num2] = flag;
			stamp[num2] = Mathf.Max(1, Time.frameCount);
			num++;
		}
		cursor = (cursor + raysPerFrame) % 1024;
	}

	private static readonly int __fbt_LateUpdate = FBTempo.Id("FBRain.LateUpdate");

	private void LateUpdate()
	{
		if (!FBTempo.On)
		{
			LateUpdate__fbt();
			return;
		}
		long t0 = FBTempo.T0();
		LateUpdate__fbt();
		FBTempo.Soma(__fbt_LateUpdate, t0);
	}

	private void LateUpdate__fbt()
	{
		Camera main = Camera.main;
		if (!main || !drops)
		{
			return;
		}
		Vector3 position = main.transform.position;
		drops.transform.position = position + main.transform.forward.Flat() * 6f + Vector3.up * 16f;
		Refresh(position);
		fill = Mathf.Min(1f, fill + Time.deltaTime / 75f);
		Shader.SetGlobalFloat("_FBPuddleFill", Mathf.SmoothStep(0.15f, 1f, fill));
		int particles = drops.GetParticles(buf);
		int num = 0;
		int num2 = (mobile ? 14 : 48);
		ParticleSystem.EmitParams emitParams = default(ParticleSystem.EmitParams);
		float num3 = groundFallback;
		for (int i = 0; i < particles; i++)
		{
			Vector3 position2 = buf[i].position;
			int num4 = Idx(Mathf.FloorToInt(position2.x / 1.5f), Mathf.FloorToInt(position2.z / 1.5f));
			float num5 = ((num4 >= 0 && stamp[num4] != 0) ? top[num4] : num3);
			if (position2.y > num5)
			{
				continue;
			}
			buf[i].remainingLifetime = -1f;
			if (num < num2 && (position2 - position).sqrMagnitude < 676f)
			{
				num++;
				emitParams.position = new Vector3(position2.x, num5 + 0.02f, position2.z);
				emitParams.applyShapeToPosition = false;
				ring.Emit(emitParams, 1);
				if (num4 < 0 || !water[num4])
				{
					emitParams.velocity = new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f), UnityEngine.Random.Range(0.9f, 1.6f), UnityEngine.Random.Range(-0.6f, 0.6f));
					spray.Emit(emitParams, 1);
					emitParams.velocity = Vector3.zero;
				}
			}
		}
		drops.SetParticles(buf, particles);
	}

	private void BuildPuddles()
	{
		if (!FBNav.Ready)
		{
			return;
		}
		List<Vector3> list = FBNav.Fixed();
		int num = (mobile ? 45 : 140);
		List<(Vector3, float, float, float)> list2 = new List<(Vector3, float, float, float)>();
		Shader shader = Shader.Find("FreeBala/Puddle");
		FBShaderRefs fBShaderRefs = Resources.Load<FBShaderRefs>("FBShaderRefs");
		if ((bool)fBShaderRefs && (bool)fBShaderRefs.puddle)
		{
			shader = fBShaderRefs.puddle;
		}
		if (!shader)
		{
			return;
		}
		foreach (Vector3 item5 in list)
		{
			if (list2.Count >= num)
			{
				break;
			}
			float num2 = Hash(item5.x, item5.z, 1);
			float num3 = Hash(item5.x, item5.z, 2);
			float num4 = Hash(item5.x, item5.z, 3);
			if (num2 > 0.55f)
			{
				continue;
			}
			Vector3 vector = item5 + new Vector3((num3 - 0.5f) * 3f, 0f, (num4 - 0.5f) * 3f);
			if (Physics.Raycast(vector + Vector3.up * 3f, Vector3.down, out var hit, 8f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) && !(hit.normal.y < 0.985f) && hit.collider.gameObject.layer == 8 && HardFloor(hit) && !FBWorld.WaterAt(hit.point.x, hit.point.z).HasValue && !Physics.Raycast(hit.point + Vector3.up * 0.3f, Vector3.up, 30f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore))
			{
				float num5 = Mathf.Lerp(1.4f, 3.6f, Hash(vector.x, vector.z, 4));
				bool flag = true;
				for (int i = 0; i < 4 && flag; i++)
				{
					flag = Physics.Raycast(hit.point + Quaternion.Euler(0f, i * 90 + 45, 0f) * Vector3.forward * (num5 * 0.45f) + Vector3.up * 0.5f, Vector3.down, out var hitInfo, 1.2f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) && Mathf.Abs(hitInfo.point.y - hit.point.y) < 0.03f;
				}
				if (flag && !list2.Exists(((Vector3 p, float s, float a, float seed) g) => (g.p - hit.point).sqrMagnitude < 36f))
				{
					list2.Add((hit.point, num5, Hash(vector.x, vector.z, 5) * 360f, Hash(vector.x, vector.z, 6)));
				}
			}
		}
		Debug.Log($"[FreeBala][chuva] poças {list2.Count} (de {list.Count} pontos da navmesh)");
		if (list2.Count == 0)
		{
			return;
		}
		List<Vector3> list3 = new List<Vector3>();
		List<Vector2> list4 = new List<Vector2>();
		List<Vector2> list5 = new List<Vector2>();
		List<int> list6 = new List<int>();
		foreach (var item6 in list2)
		{
			Vector3 item = item6.Item1;
			float item2 = item6.Item2;
			float item3 = item6.Item3;
			float item4 = item6.Item4;
			Quaternion quaternion = Quaternion.Euler(0f, item3, 0f);
			int count = list3.Count;
			float num6 = item2;
			float num7 = item2 * Mathf.Lerp(0.55f, 1f, item4);
			(float, float)[] array = new(float, float)[4]
			{
				(0f, 0f),
				(1f, 0f),
				(1f, 1f),
				(0f, 1f)
			};
			for (int j = 0; j < array.Length; j++)
			{
				var (num8, num9) = array[j];
				list3.Add(item + Vector3.up * 0.012f + quaternion * new Vector3((num8 - 0.5f) * num6, 0f, (num9 - 0.5f) * num7));
				list4.Add(new Vector2(num8, num9));
				list5.Add(new Vector2(item4, Hash(item.x, item.z, 7)));
			}
			list6.AddRange(new int[6]
			{
				count,
				count + 2,
				count + 1,
				count,
				count + 3,
				count + 2
			});
		}
		Mesh mesh = new Mesh
		{
			name = "FB_Pocas"
		};
		mesh.SetVertices(list3);
		mesh.SetUVs(0, list4);
		mesh.SetUVs(1, list5);
		mesh.SetTriangles(list6, 0);
		mesh.SetNormals(list3.ConvertAll((Vector3 _) => Vector3.up));
		mesh.RecalculateBounds();
		if ((object)puddleMat == null)
		{
			puddleMat = new Material(shader)
			{
				name = "FB_Poca",
				enableInstancing = false
			};
		}
		puddles = new GameObject("FB_Pocas")
		{
			layer = 8
		};
		puddles.AddComponent<MeshFilter>().sharedMesh = mesh;
		MeshRenderer meshRenderer = puddles.AddComponent<MeshRenderer>();
		meshRenderer.sharedMaterial = puddleMat;
		meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
		meshRenderer.receiveShadows = true;
		puddles.transform.SetParent(base.transform, worldPositionStays: true);
	}

	private static bool HardFloor(RaycastHit h)
	{
		MeshRenderer component = h.collider.GetComponent<MeshRenderer>();
		Mesh mesh = ((h.collider as MeshCollider) ? ((MeshCollider)h.collider).sharedMesh : null);
		if (!component || !mesh)
		{
			return false;
		}
		Material[] sharedMaterials = component.sharedMaterials;
		int num = 0;
		if (mesh.subMeshCount > 1 && h.triangleIndex >= 0)
		{
			int num2 = h.triangleIndex * 3;
			for (int i = 0; i < mesh.subMeshCount; i++)
			{
				if (num2 >= mesh.GetSubMesh(i).indexStart)
				{
					num = i;
				}
			}
		}
		if (num < sharedMaterials.Length && (bool)sharedMaterials[num])
		{
			return Hard.IsMatch(sharedMaterials[num].name);
		}
		return false;
	}

	private static float Hash(float x, float z, int s)
	{
		int num = (int)(Mathf.RoundToInt(x * 10f) * 374761393 + Mathf.RoundToInt(z * 10f) * 668265263 + s * 2246822519u);
		int num2 = (num ^ (int)((uint)num >> 13)) * 1274126177;
		return (float)(((uint)num2 ^ ((uint)num2 >> 16)) & 0xFFFFFFu) / 16777215f;
	}
}
}
