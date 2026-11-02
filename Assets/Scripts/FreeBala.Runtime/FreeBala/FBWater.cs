using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public static class FBWater
{
	public enum Kind
	{
		None,
		Pool,
		River,
		Swamp,
		Jet
	}

	public struct Jet
	{
		public Vector3 p;

		public Vector3 dir;

		public float h;

		public float r;
	}

	private class MB
	{
		public readonly List<Vector3> pos = new List<Vector3>();

		public readonly List<Vector4> u0 = new List<Vector4>();

		public readonly List<Vector4> u1 = new List<Vector4>();

		public readonly List<Vector4> u2 = new List<Vector4>();

		public readonly List<int> idx = new List<int>();

		public Bounds b;

		public bool hasB;
	}

	public class State
	{
		public GameObject drops;

		public List<Vector3> clusters = new List<Vector3>();

		public List<Renderer> rivers = new List<Renderer>();

		public float sndT;
	}

	private static readonly Regex rxNot = new Regex("tank|caixa|carmetal|espelhos?(\\.|$|_[^d])|retrovis|glass|vidro|grama|grass|lawn", RegexOptions.IgnoreCase);

	// BURITIRO: nomes que têm "agua", "rio" ou "piscina" mas não são água (cor verde-água, areia do rio, azulejo e tinta azul-piscina, mural, reboco, fibra, piso de borracha).
	// Sem isto a areia da praia da Orla (A2_areia_rio_branco) e as paredes verde-água do Centro viravam água no jogo.
	private static readonly Regex rxNaoAgua = new Regex("verde[_ ]?[aá]gua|azul[_ ]?piscina|azulejo|areia|sand|barro|capim|mural|reboco|plaster|fibra|epdm|tinta|paint", RegexOptions.IgnoreCase);

	// nome original -> material de água trocado, da última troca na cena inteira (para o teste conferir)
	public static readonly Dictionary<string, string> Trocados = new Dictionary<string, string>();

	public static bool NaoEAgua(string nome)
	{
		return !string.IsNullOrEmpty(nome) && rxNaoAgua.IsMatch(nome);
	}

	private static readonly Regex rxJet = new Regex("(^|_)jets?($|_|\\.)|jato", RegexOptions.IgnoreCase);

	private static readonly Regex rxRiver = new Regex("(^|_)rio($|_|\\.)|river|igarap", RegexOptions.IgnoreCase);

	private static readonly Regex rxSwamp = new Regex("brejo|lagoa|lake|pantano|p[aâ]ntano|pond", RegexOptions.IgnoreCase);

	private static readonly Regex rxPool = new Regex("(^|_)water($|_|\\.)|(^|_)agua($|_|\\.)|espelho_d|piscina|pool|chafariz|fonte_agua", RegexOptions.IgnoreCase);

	private static readonly Dictionary<Kind, Material> mats = new Dictionary<Kind, Material>();

	private static readonly int idFlow = Shader.PropertyToID("_Flow");

	private const float G = 9.8f;

	public static List<Jet> LastJets = new List<Jet>();

	public static Scene JetsScene;

	private static readonly int idNight = Shader.PropertyToID("_FBNight");

	public static Kind Classify(string matName, string objName = null)
	{
		if (string.IsNullOrEmpty(matName))
		{
			return Kind.None;
		}
		if (matName.StartsWith("FBWater_"))
		{
			return matName switch
			{
				"FBWater_Rio" => Kind.River, 
				"FBWater_Brejo" => Kind.Swamp, 
				"FBWater_Jato" => Kind.Jet, 
				"FBWater_Fonte" => Kind.Pool, 
				_ => Kind.None, 
			};
		}
		string input = matName.Replace(" (Instance)", "");
		if (rxNot.IsMatch(input) || rxNaoAgua.IsMatch(input))
		{
			return Kind.None;
		}
		if (rxJet.IsMatch(input))
		{
			return Kind.Jet;
		}
		if (rxRiver.IsMatch(input))
		{
			return Kind.River;
		}
		if (rxSwamp.IsMatch(input))
		{
			return Kind.Swamp;
		}
		if (rxPool.IsMatch(input))
		{
			return Kind.Pool;
		}
		return Kind.None;
	}

	public static string MaterialName(Kind k)
	{
		return k switch
		{
			Kind.Pool => "FBWater_Fonte", 
			Kind.River => "FBWater_Rio", 
			Kind.Swamp => "FBWater_Brejo", 
			Kind.Jet => "FBWater_Jato", 
			_ => null, 
		};
	}

	public static Material Template(Kind k)
	{
		if (k == Kind.None)
		{
			return null;
		}
		if (mats.TryGetValue(k, out var value) && (bool)value)
		{
			return value;
		}
		value = Resources.Load<Material>("Water/" + MaterialName(k));
		mats[k] = value;
		return value;
	}

	public static int SwapMaterials(GameObject root, Func<Kind, Material> pick = null)
	{
		if (pick == null)
		{
			pick = Template;
		}
		int num = 0;
		if (!root)
		{
			Trocados.Clear();
		}
		MeshRenderer[] array = (root ? root.GetComponentsInChildren<MeshRenderer>(includeInactive: true) : UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None));
		foreach (MeshRenderer meshRenderer in array)
		{
			Material[] sharedMaterials = meshRenderer.sharedMaterials;
			bool flag = false;
			for (int j = 0; j < sharedMaterials.Length; j++)
			{
				if (!sharedMaterials[j])
				{
					continue;
				}
				Kind kind = Classify(sharedMaterials[j].name, meshRenderer.name);
				if (kind != 0)
				{
					Material material = pick(kind);
					if ((bool)material && !(sharedMaterials[j] == material))
					{
						Trocados[sharedMaterials[j].name] = material.name;
						sharedMaterials[j] = material;
						flag = true;
					}
				}
			}
			if (flag)
			{
				meshRenderer.sharedMaterials = sharedMaterials;
				meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
				meshRenderer.receiveShadows = false;
				num++;
			}
		}
		return num;
	}

	public static Vector2 FlowDir(Renderer r)
	{
		MeshFilter component = r.GetComponent<MeshFilter>();
		Mesh mesh = (component ? component.sharedMesh : null);
		if ((bool)mesh && mesh.isReadable && mesh.vertexCount > 2)
		{
			Vector3[] vertices = mesh.vertices;
			Matrix4x4 localToWorldMatrix = r.transform.localToWorldMatrix;
			int num = Mathf.Max(1, vertices.Length / 4000);
			double num2 = 0.0;
			double num3 = 0.0;
			double num4 = 0.0;
			for (int i = 0; i < vertices.Length; i += num)
			{
				Vector3 vector = localToWorldMatrix.MultiplyPoint3x4(vertices[i]);
				num2 += (double)vector.x;
				num3 += (double)vector.z;
				num4 += 1.0;
			}
			double num5 = num2 / num4;
			double num6 = num3 / num4;
			double num7 = 0.0;
			double num8 = 0.0;
			double num9 = 0.0;
			for (int j = 0; j < vertices.Length; j += num)
			{
				Vector3 vector2 = localToWorldMatrix.MultiplyPoint3x4(vertices[j]);
				double num10 = (double)vector2.x - num5;
				double num11 = (double)vector2.z - num6;
				num7 += num10 * num10;
				num8 += num11 * num11;
				num9 += num10 * num11;
			}
			float f = 0.5f * Mathf.Atan2((float)(2.0 * num9), (float)(num7 - num8));
			return new Vector2(Mathf.Cos(f), Mathf.Sin(f));
		}
		Vector3 size = r.bounds.size;
		if (!(size.x >= size.z))
		{
			return Vector2.up;
		}
		return Vector2.right;
	}

	private static void SetupRivers(List<Renderer> rivers)
	{
		MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
		foreach (Renderer river in rivers)
		{
			Vector2 vector = FlowDir(river);
			Material material = Template(Kind.River);
			Vector4 vector2 = (material ? material.GetVector(idFlow) : new Vector4(0f, 1f, 0.55f, 1f));
			river.GetPropertyBlock(materialPropertyBlock);
			materialPropertyBlock.SetVector(idFlow, new Vector4(vector.x, vector.y, vector2.z, vector2.w));
			river.SetPropertyBlock(materialPropertyBlock);
		}
	}

	public static void ApplyQuality(int level)
	{
		if (level <= 0)
		{
			Shader.EnableKeyword("FB_WATER_LITE");
		}
		else
		{
			Shader.DisableKeyword("FB_WATER_LITE");
		}
		UniversalRenderPipelineAsset universalRenderPipelineAsset = (QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
		if (level > 0 && (bool)universalRenderPipelineAsset && universalRenderPipelineAsset.supportsCameraDepthTexture)
		{
			Shader.EnableKeyword("FB_WATER_DEPTH");
		}
		else
		{
			Shader.DisableKeyword("FB_WATER_DEPTH");
		}
	}

	public static List<Jet> ReadJets(Dictionary<string, object> level)
	{
		List<Jet> list = new List<Jet>();
		List<object> list2 = ((level != null) ? Json.L(level, "fountains") : null);
		if (list2 == null)
		{
			return list;
		}
		foreach (object item in list2)
		{
			List<object> f = item as List<object>;
			if (f != null && f.Count >= 8)
			{
				Vector3 vector = new Vector3(0f - X(4), X(5), X(6));
				if (vector.sqrMagnitude < 1E-06f)
				{
					vector = Vector3.up;
				}
				list.Add(new Jet
				{
					p = new Vector3(0f - X(0), X(1), X(2)),
					h = Mathf.Max(0.2f, X(3)),
					dir = vector.normalized,
					r = X(7)
				});
			}
			float X(int i)
			{
				return Json.Fv(f[i]);
			}
		}
		return list;
	}

	private static void Quad(MB m, Vector3 basePos, int kind, Vector3 dir, float speed, float life, float size, float hue, float reach)
	{
		int count = m.pos.Count;
		float value = UnityEngine.Random.value;
		for (int i = 0; i < 4; i++)
		{
			m.pos.Add(basePos);
			m.u0.Add(new Vector4((i % 2 == 0) ? (-0.5f) : 0.5f, (i < 2) ? (-0.5f) : 0.5f, kind, value));
			m.u1.Add(new Vector4(dir.x, dir.y, dir.z, speed));
			m.u2.Add(new Vector4(life, size, hue, 0f));
		}
		m.idx.AddRange(new int[6]
		{
			count,
			count + 2,
			count + 1,
			count + 2,
			count + 3,
			count + 1
		});
		Bounds bounds = new Bounds(basePos, Vector3.one * (reach * 2f + size * 2f));
		if (m.hasB)
		{
			m.b.Encapsulate(bounds);
			return;
		}
		m.b = bounds;
		m.hasB = true;
	}

	public static Mesh BuildFountains(List<Jet> jets, float mul)
	{
		if (jets.Count == 0)
		{
			return null;
		}
		MB mB = new MB();
		UnityEngine.Random.State state = UnityEngine.Random.state;
		UnityEngine.Random.InitState(4242);
		foreach (Jet jet in jets)
		{
			float num = Mathf.Max(0.2f, jet.dir.y);
			float num2 = Mathf.Sqrt(19.6f * jet.h / (num * num));
			int num3 = Mathf.Max(4, Mathf.RoundToInt(Mathf.Min(70f, 18f + jet.h * 13f) * mul));
			float num4 = 0.03f + jet.r * 0.25f;
			float reach = jet.h + num2;
			for (int i = 0; i < num3; i++)
			{
				Vector3 normalized = (jet.dir + new Vector3((UnityEngine.Random.value - 0.5f) * num4, (UnityEngine.Random.value - 0.5f) * num4 * 0.3f, (UnityEngine.Random.value - 0.5f) * num4)).normalized;
				float num5 = num2 * UnityEngine.Random.Range(0.9f, 1.02f);
				float num6 = normalized.y * num5;
				float life = Mathf.Max(0.25f, (num6 + Mathf.Sqrt(num6 * num6 + 0.98f)) / 9.8f);
				Quad(mB, jet.p, 0, normalized, num5, life, UnityEngine.Random.Range(0.035f, 0.07f) + jet.r * 0.35f, 0f, reach);
			}
			Vector3 vector = ((num > 0.9f) ? jet.p : (jet.p + new Vector3(jet.dir.x, 0f, jet.dir.z).normalized * (num2 * num2 * 2f * num * Mathf.Sqrt(Mathf.Max(0f, 1f - num * num)) / 9.8f)));
			int num7 = Mathf.Max(2, Mathf.RoundToInt((float)num3 * 0.4f));
			for (int j = 0; j < num7; j++)
			{
				float f = UnityEngine.Random.value * (float)Math.PI * 2f;
				float y = UnityEngine.Random.Range(0.5f, 0.95f);
				Vector3 normalized2 = new Vector3(Mathf.Cos(f), y, Mathf.Sin(f)).normalized;
				float num8 = UnityEngine.Random.Range(0.8f, 2.1f);
				float num9 = normalized2.y * num8;
				Quad(mB, vector, 0, normalized2, num8, Mathf.Max(0.15f, 2f * num9 / 9.8f + 0.05f), UnityEngine.Random.Range(0.025f, 0.05f), 0f, 1f);
			}
			int num10 = ((mul < 0.5f) ? 1 : 3);
			for (int k = 0; k < num10; k++)
			{
				Quad(mB, vector + new Vector3(UnityEngine.Random.Range(-0.08f, 0.08f), 0f, UnityEngine.Random.Range(-0.08f, 0.08f)), 1, Vector3.up, 0f, UnityEngine.Random.Range(0.9f, 1.4f), UnityEngine.Random.Range(0.35f, 0.6f) + jet.r * 2f, 0f, 1f);
			}
			if (jet.h > 1.6f && mul >= 0.5f)
			{
				Quad(mB, vector, 2, new Vector3(jet.dir.x, 0f, jet.dir.z) * 0.3f, 0f, UnityEngine.Random.Range(2.2f, 3.2f), Mathf.Min(2.2f, 0.6f + jet.h * 0.25f), 0f, 2f);
			}
		}
		UnityEngine.Random.state = state;
		Mesh mesh = new Mesh();
		mesh.name = "FontesAgua";
		mesh.indexFormat = ((mB.pos.Count > 65000) ? IndexFormat.UInt32 : IndexFormat.UInt16);
		mesh.SetVertices(mB.pos);
		mesh.SetUVs(0, mB.u0);
		mesh.SetUVs(1, mB.u1);
		mesh.SetUVs(2, mB.u2);
		mesh.SetTriangles(mB.idx, 0, calculateBounds: false);
		mesh.bounds = mB.b;
		return mesh;
	}

	public static State Setup(Dictionary<string, object> level)
	{
		FBFxQuality.Ensure();
		ApplyQuality(FBFxQuality.Level);
		State state = new State();
		SwapMaterials(null);
		MeshRenderer[] array = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
		foreach (MeshRenderer meshRenderer in array)
		{
			Material[] sharedMaterials = meshRenderer.sharedMaterials;
			foreach (Material material in sharedMaterials)
			{
				if ((bool)material && Classify(material.name) == Kind.River)
				{
					state.rivers.Add(meshRenderer);
					break;
				}
			}
		}
		SetupRivers(state.rivers);
		List<Jet> list = (LastJets = ReadJets(level));
		JetsScene = SceneManager.GetActiveScene();
		if (list.Count > 0)
		{
			float mul = ((FBFxQuality.Level <= 0) ? 0.35f : ((FBFxQuality.Level == 1) ? 0.6f : 1f));
			Mesh mesh = BuildFountains(list, mul);
			Material material2 = Resources.Load<Material>("Water/FBWater_Gotas");
			if ((bool)mesh && (bool)material2)
			{
				state.drops = new GameObject("FB_FontesAgua");
				state.drops.AddComponent<MeshFilter>().sharedMesh = mesh;
				MeshRenderer meshRenderer2 = state.drops.AddComponent<MeshRenderer>();
				meshRenderer2.sharedMaterial = material2;
				meshRenderer2.shadowCastingMode = ShadowCastingMode.Off;
				meshRenderer2.receiveShadows = false;
				meshRenderer2.lightProbeUsage = LightProbeUsage.Off;
				meshRenderer2.reflectionProbeUsage = ReflectionProbeUsage.Off;
			}
			Dictionary<Vector2Int, (Vector3, int)> dictionary = new Dictionary<Vector2Int, (Vector3, int)>();
			foreach (Jet item in list)
			{
				Vector2Int key = new Vector2Int(Mathf.FloorToInt(item.p.x / 6f), Mathf.FloorToInt(item.p.z / 6f));
				dictionary.TryGetValue(key, out var value);
				dictionary[key] = (value.Item1 + item.p, value.Item2 + 1);
			}
			foreach (var value2 in dictionary.Values)
			{
				state.clusters.Add(value2.Item1 / value2.Item2);
			}
		}
		Debug.Log(string.Format("[FreeBala] água: {0} jatos, {1} rio(s), variante {2}{3} · materiais trocados: {4}", list.Count, state.rivers.Count, Shader.IsKeywordEnabled("FB_WATER_LITE") ? "leve" : "completa", Shader.IsKeywordEnabled("FB_WATER_DEPTH") ? " + profundidade" : "", string.Join("; ", Trocados.Select(t => t.Key + " -> " + t.Value))));
		return state;
	}

	public static void Tick(State s, float dt)
	{
		Shader.SetGlobalFloat(idNight, FBWeather.NightK);
		if (s == null || (s.sndT -= dt) > 0f)
		{
			return;
		}
		s.sndT = 0.4f;
		Camera cam = FBFxQuality.Cam;
		if (!cam)
		{
			return;
		}
		Vector3 position = cam.transform.position;
		if (s.clusters.Count > 0)
		{
			Vector3 vector = s.clusters[0];
			float num = 1E+09f;
			foreach (Vector3 cluster in s.clusters)
			{
				float sqrMagnitude = (cluster - position).sqrMagnitude;
				if (sqrMagnitude < num)
				{
					num = sqrMagnitude;
					vector = cluster;
				}
			}
			FBAudio.SetLoop("agua_fonte", "amb_fonte", Mathf.Clamp01(1.2f - Mathf.Sqrt(num) / 45f) * 0.55f, vector + Vector3.up, null, 5f);
		}
		if (s.rivers.Count <= 0)
		{
			return;
		}
		Vector3 value = position;
		float num2 = 1E+09f;
		foreach (Renderer river in s.rivers)
		{
			if ((bool)river)
			{
				Vector3 vector2 = river.bounds.ClosestPoint(position);
				float sqrMagnitude2 = (vector2 - position).sqrMagnitude;
				if (sqrMagnitude2 < num2)
				{
					num2 = sqrMagnitude2;
					value = vector2;
				}
			}
		}
		FBAudio.SetLoop("agua_rio", "amb_rio", Mathf.Clamp01(1.2f - Mathf.Sqrt(num2) / 60f) * 0.5f, value, null, 8f);
	}

	public static void Stop()
	{
		FBAudio.StopLoop("agua_fonte");
		FBAudio.StopLoop("agua_rio");
	}
}
}
