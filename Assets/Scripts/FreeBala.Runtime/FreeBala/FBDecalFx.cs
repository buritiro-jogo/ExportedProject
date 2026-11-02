using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FreeBala
{
public static class FBDecalFx
{
	public enum Atlas
	{
		Blood,
		Holes
	}

	private class Slot
	{
		public GameObject go;

		public MeshRenderer mr;

		public DecalProjector proj;

		public Transform tr;

		public bool on;

		public Atlas atlas;

		public float born;

		public float life;

		public float fade = 1f;

		public float size;

		public float grow0;

		public float grow1;

		public float growT;

		public float growDur;

		public Vector2 aspect = Vector2.one;

		public int seq;

		public bool blood;

		public Vector4 st;
	}

	public const int CellRound = 0;

	public const int CellDir = 6;

	public const int CellDrip = 10;

	public const int CellPool = 12;

	public const int CellDrops = 14;

	public const int HoleConcrete = 0;

	public const int HoleMetal = 2;

	public const int HoleWood = 3;

	public const int HoleGlass = 4;

	public const int Scorch = 5;

	public const int HoleDirt = 6;

	public const int HolePlaster = 7;

	private static Slot[] slots;

	private static int seq;

	private static bool ready;

	private static Mesh quad;

	private static Material matBlood;

	private static Material matHoles;

	private static Material projBloodFresh;

	private static Material projBloodDry;

	private static Material projHoles;

	private static MaterialPropertyBlock mpb;

	private static readonly int idBaseColor = Shader.PropertyToID("_BaseColor");

	private static readonly int idSmooth = Shader.PropertyToID("_Smoothness");

	private static readonly int idST = Shader.PropertyToID("_BaseMap_ST");

	private static float propT;

	public static bool Projector { get; private set; }

	public static int Capacity
	{
		get
		{
			Slot[] array = slots;
			if (array == null)
			{
				return 0;
			}
			return array.Length;
		}
	}

	public static int ActiveCount
	{
		get
		{
			int num = 0;
			if (slots != null)
			{
				Slot[] array = slots;
				for (int i = 0; i < array.Length; i++)
				{
					if (array[i].on)
					{
						num++;
					}
				}
			}
			return num;
		}
	}

	public static int SlotObjects
	{
		get
		{
			int num = 0;
			if (slots != null)
			{
				Slot[] array = slots;
				for (int i = 0; i < array.Length; i++)
				{
					if ((bool)array[i].go)
					{
						num++;
					}
				}
			}
			return num;
		}
	}

	public static void Warm()
	{
		if (!ready)
		{
			ready = true;
			FBFxQuality.Ensure();
			mpb = new MaterialPropertyBlock();
			quad = FBFxLib.Quad("DecalQuad");
			matBlood = FBFxLib.Mat("Lit_Alpha", FBFxLib.Tex("blood_splats"), Color.white, "DecalSangue");
			Wet(matBlood, 0.55f);
			matHoles = FBFxLib.Mat("Lit_Alpha", FBFxLib.Tex("bullet_holes"), Color.white, "DecalBuraco");
			Wet(matHoles, 0.15f);
			Projector = FBFxQuality.UseProjector && ProjectorSupported();
			if (Projector)
			{
				Material source = Resources.Load<Material>("FX/DecalTemplate");
				projBloodFresh = new Material(source)
				{
					name = "ProjSangue"
				};
				projBloodFresh.SetTexture("Base_Map", FBFxLib.Tex("blood_splats"));
				projBloodDry = new Material(source)
				{
					name = "ProjSangueSeco"
				};
				projBloodDry.SetTexture("Base_Map", FBFxLib.Tex("blood_splats_dry"));
				projHoles = new Material(source)
				{
					name = "ProjBuraco"
				};
				projHoles.SetTexture("Base_Map", FBFxLib.Tex("bullet_holes"));
			}
			slots = new Slot[FBFxQuality.PoolDecals];
			for (int i = 0; i < slots.Length; i++)
			{
				slots[i] = Make(i);
			}
		}
	}

	private static void Wet(Material m, float smooth)
	{
		if (m.HasProperty("_Smoothness"))
		{
			m.SetFloat("_Smoothness", smooth);
		}
		if (m.HasProperty("_Metallic"))
		{
			m.SetFloat("_Metallic", 0f);
		}
	}

	private static bool ProjectorSupported()
	{
		try
		{
			Material material = Resources.Load<Material>("FX/DecalTemplate");
			if (!material || !material.shader || !material.shader.isSupported)
			{
				return false;
			}
			UniversalRenderPipelineAsset universalRenderPipelineAsset = (QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
			if (!universalRenderPipelineAsset)
			{
				return false;
			}
			ReadOnlySpan<ScriptableRendererData> rendererDataList = universalRenderPipelineAsset.rendererDataList;
			for (int i = 0; i < rendererDataList.Length; i++)
			{
				ScriptableRendererData scriptableRendererData = rendererDataList[i];
				if (!scriptableRendererData)
				{
					continue;
				}
				foreach (ScriptableRendererFeature rendererFeature in scriptableRendererData.rendererFeatures)
				{
					if (rendererFeature is DecalRendererFeature && rendererFeature.isActive)
					{
						return true;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[FreeBala] decal URP indisponível: " + ex.Message);
		}
		return false;
	}

	private static Slot Make(int i)
	{
		Slot slot = new Slot();
		GameObject gameObject = new GameObject("Decal" + i);
		gameObject.transform.SetParent(FBFxLib.Root, worldPositionStays: false);
		slot.go = gameObject;
		slot.tr = gameObject.transform;
		if (Projector)
		{
			slot.proj = gameObject.AddComponent<DecalProjector>();
			slot.proj.material = projBloodFresh;
			slot.proj.drawDistance = 60f;
			slot.proj.fadeScale = 0.9f;
			slot.proj.startAngleFade = 70f;
			slot.proj.endAngleFade = 88f;
			slot.proj.scaleMode = DecalScaleMode.ScaleInvariant;
			slot.proj.enabled = false;
		}
		else
		{
			gameObject.AddComponent<MeshFilter>().sharedMesh = quad;
			slot.mr = gameObject.AddComponent<MeshRenderer>();
			slot.mr.sharedMaterial = matBlood;
			slot.mr.shadowCastingMode = ShadowCastingMode.Off;
			slot.mr.receiveShadows = true;
			slot.mr.lightProbeUsage = LightProbeUsage.Off;
			slot.mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
			slot.mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
			slot.mr.enabled = false;
		}
		gameObject.SetActive(value: false);
		return slot;
	}

	private static Slot Take()
	{
		Slot slot = null;
		int num = 0;
		Slot[] array = slots;
		foreach (Slot slot2 in array)
		{
			if (slot2.on)
			{
				num++;
			}
			else if (slot == null)
			{
				slot = slot2;
			}
		}
		if (slot == null || num >= FBFxQuality.MaxDecals)
		{
			slot = null;
			array = slots;
			foreach (Slot slot3 in array)
			{
				if (slot3.on && (slot == null || slot3.seq < slot.seq))
				{
					slot = slot3;
				}
			}
		}
		int num2 = Array.IndexOf(slots, slot);
		if (!slot.go)
		{
			slot = (slots[num2] = Make(num2));
		}
		return slot;
	}

	public static Transform Place(Atlas atlas, int cell, Vector3 p, Vector3 n, float size, Vector3? up = null, float aspect = 1f, Transform parent = null, float life = -1f, bool checkEdges = true)
	{
		if ((bool)parent && (bool)parent.GetComponentInParent<FBCharacter>())
		{
			return null;
		}
		if (!ready)
		{
			Warm();
		}
		if (n.sqrMagnitude < 1E-06f)
		{
			return null;
		}
		n.Normalize();
		if (atlas == Atlas.Blood && !FBFx.BloodOn)
		{
			return null;
		}
		FBFxLib.Tangents(n, out var t, out var _);
		Vector3 vector = up ?? (Quaternion.AngleAxis(UnityEngine.Random.Range(0f, 360f), n) * t);
		vector = Vector3.ProjectOnPlane(vector, n);
		if (vector.sqrMagnitude < 1E-06f)
		{
			vector = t;
		}
		vector.Normalize();
		float num = size * aspect;
		float num2 = size;
		if (!Projector && checkEdges)
		{
			Vector3 r = Vector3.Cross(n, vector);
			int num3 = 0;
			while (!CornersOn(p, n, r, vector, num * 0.42f, num2 * 0.42f))
			{
				if (num3 == 1)
				{
					return null;
				}
				num *= 0.55f;
				num2 *= 0.55f;
				num3++;
			}
		}
		Slot slot = Take();
		slot.on = true;
		slot.atlas = atlas;
		slot.blood = atlas == Atlas.Blood;
		slot.born = Time.time;
		slot.life = ((life > 0f) ? life : (FBFxQuality.DecalLife * UnityEngine.Random.Range(0.85f, 1.15f)));
		slot.fade = 1f;
		slot.size = num2;
		slot.aspect = new Vector2(num / num2, 1f);
		slot.growDur = 0f;
		slot.seq = ++seq;
		int num4 = ((atlas == Atlas.Blood) ? 4 : 4);
		int num5 = ((atlas == Atlas.Blood) ? 4 : 2);
		int num6 = cell % num4;
		int num7 = cell / num4;
		slot.st = new Vector4(1f / (float)num4, 1f / (float)num5, (float)num6 / (float)num4, 1f - ((float)num7 + 1f) / (float)num5);
		slot.go.SetActive(value: true);
		if ((bool)parent)
		{
			slot.tr.SetParent(parent, worldPositionStays: true);
		}
		else if (slot.tr.parent != FBFxLib.Root)
		{
			slot.tr.SetParent(FBFxLib.Root, worldPositionStays: true);
		}
		Quaternion rotation = Quaternion.LookRotation(-n, vector);
		if (Projector)
		{
			slot.tr.SetPositionAndRotation(p + n * 0.05f, rotation);
			slot.proj.material = (slot.blood ? projBloodFresh : projHoles);
			slot.proj.uvScale = new Vector2(slot.st.x, slot.st.y);
			slot.proj.uvBias = new Vector2(slot.st.z, slot.st.w);
			slot.proj.size = new Vector3(num, num2, 0.2f);
			slot.proj.pivot = new Vector3(0f, 0f, 0.05f);
			slot.proj.fadeFactor = 1f;
			slot.proj.enabled = true;
		}
		else
		{
			float num8 = 0.004f + (float)(slot.seq % 16) * 0.0004f;
			slot.tr.SetPositionAndRotation(p + n * num8, rotation);
			SetScale(slot, num, num2);
			slot.mr.sharedMaterial = (slot.blood ? matBlood : matHoles);
			slot.mr.sortingOrder = slot.seq % 30000;
			slot.mr.enabled = true;
			Props(slot, 0f);
		}
		return slot.tr;
	}

	private static void SetScale(Slot s, float w, float h)
	{
		float num = Mathf.Max(0.0001f, Mathf.Abs((s.tr.parent ? s.tr.parent.lossyScale : Vector3.one).x));
		s.tr.localScale = new Vector3(w / num, h / num, 1f / num);
	}

	private static bool CornersOn(Vector3 p, Vector3 n, Vector3 r, Vector3 u, float hw, float hh)
	{
		for (int i = 0; i < 4; i++)
		{
			if (!Physics.Raycast(p + r * ((i % 2 == 0) ? (0f - hw) : hw) + u * ((i < 2) ? (0f - hh) : hh) + n * 0.06f, -n, out var hitInfo, 0.14f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore))
			{
				return false;
			}
			if (Vector3.Dot(hitInfo.normal, n) < 0.9f || Mathf.Abs(Vector3.Dot(hitInfo.point - p, n)) > 0.025f)
			{
				return false;
			}
		}
		return true;
	}

	public static Transform Pool(Vector3 p, float to, float seconds = 5f)
	{
		Transform transform = Place(Atlas.Blood, 12 + UnityEngine.Random.Range(0, 2), p, Vector3.up, to, null, UnityEngine.Random.Range(0.9f, 1.15f), null, FBFxQuality.DecalLife * 1.5f, checkEdges: false);
		if (!transform)
		{
			return null;
		}
		Slot slot = Find(transform);
		slot.grow0 = to * 0.15f;
		slot.grow1 = to;
		slot.growT = 0f;
		slot.growDur = seconds;
		Size(slot, slot.grow0);
		return transform;
	}

	private static Slot Find(Transform t)
	{
		Slot[] array = slots;
		foreach (Slot slot in array)
		{
			if (slot.tr == t)
			{
				return slot;
			}
		}
		return null;
	}

	private static void Size(Slot s, float h)
	{
		float num = h * s.aspect.x;
		s.size = h;
		if (Projector)
		{
			s.proj.size = new Vector3(num, h, s.proj.size.z);
		}
		else
		{
			SetScale(s, num, h);
		}
	}

	private static void Props(Slot s, float age)
	{
		float fade = s.fade;
		if (s.blood)
		{
			float num = Mathf.Clamp01(age / 40f);
			Color value = Color.Lerp(Color.white, new Color(0.55f, 0.42f, 0.38f), num * num * (3f - 2f * num));
			value.a = fade;
			mpb.SetColor(idBaseColor, value);
			mpb.SetFloat(idSmooth, Mathf.Lerp(0.62f, 0.18f, num));
		}
		else
		{
			mpb.SetColor(idBaseColor, new Color(1f, 1f, 1f, fade));
			mpb.SetFloat(idSmooth, 0.12f);
		}
		mpb.SetVector(idST, s.st);
		s.mr.SetPropertyBlock(mpb);
	}

	public static void Tick(float dt)
	{
		if (!ready)
		{
			return;
		}
		bool flag = (propT -= dt) <= 0f;
		if (flag)
		{
			propT = 0.5f;
		}
		float time = Time.time;
		Slot[] array = slots;
		foreach (Slot slot in array)
		{
			if (!slot.on)
			{
				continue;
			}
			if (!slot.go)
			{
				slot.on = false;
				continue;
			}
			if (slot.growDur > 0f)
			{
				slot.growT += dt;
				float num = Mathf.Clamp01(slot.growT / slot.growDur);
				Size(slot, Mathf.Lerp(slot.grow0, slot.grow1, 1f - (1f - num) * (1f - num)));
				if (num >= 1f)
				{
					slot.growDur = 0f;
				}
			}
			float num2 = time - slot.born;
			if (num2 > slot.life)
			{
				slot.fade -= dt / 2.5f;
				if (slot.fade <= 0f)
				{
					Free(slot);
					continue;
				}
				flag = true;
			}
			if (Projector)
			{
				slot.proj.fadeFactor = slot.fade;
				if (slot.blood && num2 > 30f && slot.proj.material != projBloodDry)
				{
					slot.proj.material = projBloodDry;
				}
			}
			else if (flag || slot.growDur > 0f)
			{
				Props(slot, num2);
			}
		}
	}

	private static void Free(Slot s)
	{
		s.on = false;
		if ((bool)s.go)
		{
			if ((bool)s.proj)
			{
				s.proj.enabled = false;
			}
			if ((bool)s.mr)
			{
				s.mr.enabled = false;
			}
			if (s.tr.parent != FBFxLib.Root)
			{
				s.tr.SetParent(FBFxLib.Root, worldPositionStays: false);
			}
			s.go.SetActive(value: false);
		}
	}

	public static void Clear()
	{
		if (slots == null)
		{
			return;
		}
		Slot[] array = slots;
		foreach (Slot slot in array)
		{
			if (slot.on)
			{
				Free(slot);
			}
		}
	}

	public static Vector3 FacingOf(Transform t)
	{
		if (!t)
		{
			return Vector3.zero;
		}
		return -t.forward;
	}
}
}
