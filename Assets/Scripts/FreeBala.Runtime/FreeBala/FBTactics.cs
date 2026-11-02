using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace FreeBala
{
public class FBTactics : MonoBehaviour
{
	private class Aura
	{
		public readonly List<(SkinnedMeshRenderer src, SkinnedMeshRenderer dst)> r = new List<(SkinnedMeshRenderer, SkinnedMeshRenderer)>();
	}

	private class ShieldSt
	{
		public float start;

		public float until;

		public float broke = -1f;
	}

	public static FBTactics I;

	private const int N_GLINT = 6;

	private const int N_SHIELD = 10;

	private readonly List<Transform> glints = new List<Transform>();

	private readonly List<Transform> rings = new List<Transform>();

	private Material auraMat;

	private Material ringMat;

	private bool shieldNew;

	private MaterialPropertyBlock mpb;

	private readonly Dictionary<FBCharacter, Aura> auras = new Dictionary<FBCharacter, Aura>();

	private Image protBar;

	private RawImage protEdge;

	private float protTotal = 1f;

	private float protSeen;

	private readonly Dictionary<FBCharacter, ShieldSt> shSt = new Dictionary<FBCharacter, ShieldSt>();

	private static readonly int idAlpha = Shader.PropertyToID("_Alpha");

	private static readonly int idIntro = Shader.PropertyToID("_Intro");

	private static readonly int idEnd = Shader.PropertyToID("_End");

	private static readonly int idColor = Shader.PropertyToID("_Color");

	private static readonly int idFeet = Shader.PropertyToID("_Feet");

	private static readonly int idHeight = Shader.PropertyToID("_Height");

	private GameObject knock;

	private GameObject veil;

	private GameObject streak;

	private GameObject gun;

	private GameObject prot;

	private Image knockBar;

	private Image reviveBar;

	private Image gunTime;

	private Image gunArmor;

	private Image gunExit;

	private Text knockSub;

	private Text reviveTxt;

	private Text streakN;

	private Text streakPips;

	private Text gunTitle;

	private Text gunTm;

	private Text gunTl;

	private Text gunAl;

	private Text protT;

	private GameObject reviveBox;

	private readonly List<Text> marks = new List<Text>();

	public static void Spawn()
	{
		if (!I)
		{
			I = new GameObject("FB_Tactics").AddComponent<FBTactics>();
			I.Build();
		}
	}

	private static Texture2D GlintTex()
	{
		int num = 128;
		Texture2D texture2D = new Texture2D(num, num, TextureFormat.RGBA32, mipChain: false);
		for (int i = 0; i < num; i++)
		{
			for (int j = 0; j < num; j++)
			{
				float num2 = ((float)j - 63.5f) / 64f;
				float num3 = ((float)i - 63.5f) / 64f;
				float num4 = Mathf.Sqrt(num2 * num2 + num3 * num3);
				float num5 = ((num4 < 0.12f) ? Mathf.Lerp(1f, 0.95f, num4 / 0.12f) : ((num4 < 0.35f) ? Mathf.Lerp(0.95f, 0.35f, (num4 - 0.12f) / 0.23f) : Mathf.Lerp(0.35f, 0f, Mathf.Clamp01((num4 - 0.35f) / 0.65f))));
				float num6 = Mathf.Max(Mathf.Clamp01(1f - (num2 * num2 / 0.94f + num3 * num3 / 0.0025f)), Mathf.Clamp01(1f - (num2 * num2 / 0.0025f + num3 * num3 / 0.39f))) * 0.9f;
				texture2D.SetPixel(j, i, new Color(1f, Mathf.Lerp(0.86f, 0.98f, 1f - num4), Mathf.Lerp(0.6f, 0.88f, 1f - num4), Mathf.Clamp01(num5 + num6)));
			}
		}
		texture2D.Apply();
		return texture2D;
	}

	private void Build()
	{
		FBLaser.Warm();
		Material material = FBShaders.Transparent(new Material(FBShaders.ParticlesUnlit), additive: true);
		Texture2D texture2D = GlintTex();
		material.SetTexture("_BaseMap", texture2D);
		material.mainTexture = texture2D;
		material.SetFloat("_Cull", 0f);
		for (int i = 0; i < 6; i++)
		{
			GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
			UnityEngine.Object.Destroy(gameObject.GetComponent<Collider>());
			gameObject.name = "brilho_luneta";
			gameObject.transform.SetParent(base.transform);
			gameObject.GetComponent<Renderer>().sharedMaterial = material;
			gameObject.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
			gameObject.SetActive(value: false);
			glints.Add(gameObject.transform);
		}
		Shader shader = Resources.Load<Shader>("FX/FBSpawnAura");
		Shader shader2 = Resources.Load<Shader>("FX/FBSpawnShield");
		shieldNew = (bool)shader && shader.isSupported;
		if (shieldNew)
		{
			auraMat = new Material(shader)
			{
				name = "aura_protecao"
			};
			if ((bool)shader2 && shader2.isSupported)
			{
				ringMat = new Material(shader2)
				{
					name = "anel_protecao"
				};
				ringMat.SetFloat("_Mode", 1f);
			}
		}
		for (int j = 0; j < 10; j++)
		{
			if (!ringMat)
			{
				break;
			}
			GameObject gameObject2 = GameObject.CreatePrimitive(PrimitiveType.Quad);
			UnityEngine.Object.Destroy(gameObject2.GetComponent<Collider>());
			gameObject2.name = "anel_protecao";
			gameObject2.transform.SetParent(base.transform);
			Renderer component = gameObject2.GetComponent<Renderer>();
			component.sharedMaterial = ringMat;
			component.shadowCastingMode = ShadowCastingMode.Off;
			component.receiveShadows = false;
			gameObject2.SetActive(value: false);
			rings.Add(gameObject2.transform);
		}
		Canvas canvas = FBUI.Canvas("Tatico", 29);
		canvas.transform.SetParent(base.transform);
		veil = new GameObject("veu", typeof(RectTransform), typeof(RawImage));
		veil.transform.SetParent(canvas.transform, worldPositionStays: false);
		RectTransform obj = (RectTransform)veil.transform;
		obj.anchorMin = Vector2.zero;
		obj.anchorMax = Vector2.one;
		Vector2 offsetMin = (obj.offsetMax = Vector2.zero);
		obj.offsetMin = offsetMin;
		Texture2D texture2D2 = new Texture2D(64, 64);
		for (int k = 0; k < 64; k++)
		{
			for (int l = 0; l < 64; l++)
			{
				float magnitude = new Vector2(((float)l - 31.5f) / 32f, ((float)k - 31.5f) / 32f).magnitude;
				texture2D2.SetPixel(l, k, new Color(0.43f, 0f, 0f, Mathf.Clamp01((magnitude - 0.55f) / 0.6f) * 0.55f));
			}
		}
		texture2D2.Apply();
		RawImage component2 = veil.GetComponent<RawImage>();
		component2.texture = texture2D2;
		component2.raycastTarget = false;
		veil.SetActive(value: false);
		knock = FBUI.Rect(canvas.transform, "caido", new Vector2(0.5f, 0.22f), new Vector2(0.5f, 0.22f), Vector2.zero, new Vector2(420f, 120f)).gameObject;
		FBUI.Label(knock.transform, "VOCÊ FOI DERRUBADO", 22, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(420f, 30f), TextAnchor.MiddleCenter, title: true, new Color(1f, 0.29f, 0.23f));
		knockBar = Bar(knock.transform, new Vector2(0f, -38f), 420f, new Color(1f, 0.29f, 0.23f));
		knockSub = FBUI.Label(knock.transform, "", 12, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(420f, 18f), TextAnchor.MiddleCenter, title: false, new Color(0.93f, 0.91f, 0.86f, 0.85f));
		reviveBox = FBUI.Rect(knock.transform, "rv", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(420f, 40f)).gameObject;
		reviveTxt = FBUI.Label(reviveBox.transform, "SENDO REANIMADO", 12, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(420f, 18f));
		reviveBar = Bar(reviveBox.transform, new Vector2(0f, -24f), 420f, new Color(0.93f, 0.91f, 0.86f));
		knock.SetActive(value: false);
		streak = FBUI.Rect(canvas.transform, "sequencia", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(112f, 165f), new Vector2(190f, 60f)).gameObject;
		FBUI.Label(streak.transform, "SEQUÊNCIA DE ABATES", 11, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(95f, -8f), new Vector2(190f, 16f), TextAnchor.MiddleLeft, title: false, new Color(1f, 1f, 1f, 0.8f));
		streakN = FBUI.Label(streak.transform, "", 16, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(95f, -28f), new Vector2(190f, 22f), TextAnchor.MiddleLeft, title: true);
		streakPips = FBUI.Label(streak.transform, "", 12, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(95f, -48f), new Vector2(190f, 16f), TextAnchor.MiddleLeft);
		streak.AddComponent<Button>().onClick.AddListener(delegate
		{
			FBKillstreak2.Call();
		});
		streak.SetActive(value: false);
		gun = FBUI.Rect(canvas.transform, "atirador", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(380f, 130f)).gameObject;
		gunTitle = FBUI.Label(gun.transform, "", 11, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(380f, 16f), TextAnchor.MiddleCenter, title: false, new Color(1f, 1f, 1f, 0.8f));
		gunTm = FBUI.Label(gun.transform, "", 28, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(380f, 34f), TextAnchor.MiddleCenter, title: true);
		gunTl = FBUI.Label(gun.transform, "", 10, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(380f, 14f), TextAnchor.MiddleRight);
		FBUI.Label(gun.transform, "TEMPO", 10, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(380f, 14f), TextAnchor.MiddleLeft);
		gunTime = Bar(gun.transform, new Vector2(0f, -68f), 380f, new Color(0.93f, 0.91f, 0.86f));
		gunAl = FBUI.Label(gun.transform, "", 10, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(380f, 14f), TextAnchor.MiddleRight);
		FBUI.Label(gun.transform, "BLINDAGEM", 10, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(380f, 14f), TextAnchor.MiddleLeft);
		gunArmor = Bar(gun.transform, new Vector2(0f, -94f), 380f, new Color(0.93f, 0.91f, 0.86f));
		FBUI.Label(gun.transform, "SEGURE G PARA SAIR", 11, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(380f, 16f), TextAnchor.MiddleCenter, title: false, new Color(1f, 1f, 1f, 0.8f));
		gunExit = Bar(gun.transform, new Vector2(0f, -124f), 120f, new Color(0.93f, 0.91f, 0.86f));
		gun.SetActive(value: false);
		protT = FBUI.Label(canvas.transform, "", 11, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -34f), new Vector2(360f, 18f), TextAnchor.MiddleCenter, title: false, new Color(0.56f, 0.83f, 1f));
		prot = protT.gameObject;
		prot.SetActive(value: false);
		protBar = Bar(prot.transform, new Vector2(0f, -20f), 140f, new Color(0.56f, 0.83f, 1f, 0.9f));
		protBar.transform.parent.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);
		GameObject gameObject3 = new GameObject("protecao_borda", typeof(RectTransform), typeof(RawImage));
		gameObject3.transform.SetParent(canvas.transform, worldPositionStays: false);
		RectTransform obj2 = (RectTransform)gameObject3.transform;
		obj2.anchorMin = Vector2.zero;
		obj2.anchorMax = Vector2.one;
		offsetMin = (obj2.offsetMax = Vector2.zero);
		obj2.offsetMin = offsetMin;
		Texture2D texture2D3 = new Texture2D(64, 64);
		for (int m = 0; m < 64; m++)
		{
			for (int n = 0; n < 64; n++)
			{
				float a = (float)Mathf.Min(n, 63 - n) / 63f;
				float b = (float)Mathf.Min(m, 63 - m) / 63f;
				float num = Mathf.Min(a, b);
				texture2D3.SetPixel(n, m, new Color(0.45f, 0.85f, 1f, Mathf.Pow(Mathf.Clamp01(1f - num / 0.09f), 2f) * 0.5f));
			}
		}
		texture2D3.Apply();
		protEdge = gameObject3.GetComponent<RawImage>();
		protEdge.texture = texture2D3;
		protEdge.raycastTarget = false;
		gameObject3.SetActive(value: false);
		for (int num2 = 0; num2 < 6; num2++)
		{
			Text text = FBUI.Label(canvas.transform, "", 11, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(220f, 36f), TextAnchor.LowerCenter, title: true, new Color(1f, 0.29f, 0.23f));
			text.gameObject.SetActive(value: false);
			marks.Add(text);
		}
	}

	private static Image Bar(Transform p, Vector2 pos, float w, Color c)
	{
		Image image = FBUI.Panel(FBUI.Panel(p, "barra", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), pos, new Vector2(w, 6f), new Color(1f, 1f, 1f, 0.14f)).transform, "fill", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero, c);
		RectTransform rectTransform = image.rectTransform;
		Vector2 offsetMin = (image.rectTransform.offsetMax = Vector2.zero);
		rectTransform.offsetMin = offsetMin;
		return image;
	}

	private static void Fill(Image i, float k)
	{
		i.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(k), 1f);
	}

	private Aura AuraOf(FBCharacter a)
	{
		List<SkinnedMeshRenderer> list = (from x in a.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: false)
			where (bool)x && x.enabled && (bool)x.sharedMesh && !x.name.StartsWith("aura_")
			select x).ToList();
		if (auras.TryGetValue(a, out var value) && value.r.All(((SkinnedMeshRenderer src, SkinnedMeshRenderer dst) p) => (bool)p.src && (bool)p.dst) && value.r.Count == list.Count)
		{
			return value;
		}
		if (value != null)
		{
			foreach (var item2 in value.r)
			{
				SkinnedMeshRenderer item = item2.dst;
				if ((bool)item)
				{
					UnityEngine.Object.Destroy(item.gameObject);
				}
			}
		}
		value = new Aura();
		foreach (SkinnedMeshRenderer item3 in list)
		{
			GameObject obj = new GameObject("aura_" + item3.name);
			obj.layer = item3.gameObject.layer;
			obj.transform.SetParent(item3.transform.parent, worldPositionStays: false);
			obj.transform.localPosition = item3.transform.localPosition;
			obj.transform.localRotation = item3.transform.localRotation;
			obj.transform.localScale = item3.transform.localScale;
			SkinnedMeshRenderer skinnedMeshRenderer = obj.AddComponent<SkinnedMeshRenderer>();
			skinnedMeshRenderer.sharedMesh = item3.sharedMesh;
			skinnedMeshRenderer.bones = item3.bones;
			skinnedMeshRenderer.rootBone = item3.rootBone;
			skinnedMeshRenderer.localBounds = item3.localBounds;
			skinnedMeshRenderer.quality = item3.quality;
			Material[] array = new Material[item3.sharedMesh.subMeshCount];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = auraMat;
			}
			skinnedMeshRenderer.sharedMaterials = array;
			skinnedMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
			skinnedMeshRenderer.receiveShadows = false;
			skinnedMeshRenderer.lightProbeUsage = LightProbeUsage.Off;
			skinnedMeshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
			skinnedMeshRenderer.skinnedMotionVectors = false;
			skinnedMeshRenderer.enabled = false;
			value.r.Add((item3, skinnedMeshRenderer));
		}
		auras[a] = value;
		return value;
	}

	public int AuraCount(FBCharacter a)
	{
		if (!a || !auras.TryGetValue(a, out var value))
		{
			return 0;
		}
		return value.r.Count(((SkinnedMeshRenderer src, SkinnedMeshRenderer dst) p) => (bool)p.dst && p.dst.enabled);
	}

	private void ShowAura(FBCharacter a, float alpha, float intro, float end, Color col)
	{
		Aura aura = AuraOf(a);
		mpb.Clear();
		mpb.SetFloat(idAlpha, alpha);
		mpb.SetFloat(idIntro, intro);
		mpb.SetFloat(idEnd, end);
		mpb.SetColor(idColor, col);
		mpb.SetFloat(idFeet, a.Pos.y);
		mpb.SetFloat(idHeight, 1.8f * a.ScaleK);
		foreach (var (skinnedMeshRenderer, skinnedMeshRenderer2) in aura.r)
		{
			if ((bool)skinnedMeshRenderer && (bool)skinnedMeshRenderer2)
			{
				bool flag = skinnedMeshRenderer.enabled && skinnedMeshRenderer.gameObject.activeInHierarchy && skinnedMeshRenderer.shadowCastingMode != ShadowCastingMode.ShadowsOnly;
				if (skinnedMeshRenderer2.sharedMesh != skinnedMeshRenderer.sharedMesh)
				{
					skinnedMeshRenderer2.sharedMesh = skinnedMeshRenderer.sharedMesh;
				}
				if (skinnedMeshRenderer2.enabled != flag)
				{
					skinnedMeshRenderer2.enabled = flag;
				}
				if (flag)
				{
					skinnedMeshRenderer2.SetPropertyBlock(mpb);
				}
			}
		}
	}

	private void HideAura(FBCharacter a)
	{
		if (!a || !auras.TryGetValue(a, out var value))
		{
			return;
		}
		foreach (var item2 in value.r)
		{
			SkinnedMeshRenderer item = item2.dst;
			if ((bool)item && item.enabled)
			{
				item.enabled = false;
			}
		}
	}

	private void Shields(FBCharacter P, float now)
	{
		int num = 0;
		bool flag = !FBPlayer.I || !FBPlayer.I.FirstPerson;
		if (!shieldNew)
		{
			return;
		}
		if (mpb == null)
		{
			mpb = new MaterialPropertyBlock();
		}
		HashSet<FBCharacter> hashSet = new HashSet<FBCharacter>();
		if (FBLoading.Busy && (bool)P)
		{
			ShowAura(P, 0.002f, 1f, 0f, Color.black);
			hashSet.Add(P);
		}
		Color color = new Color(0.3f, 0.85f, 1.7f) * 0.7f;
		Color color2 = new Color(1.7f, 0.32f, 0.2f) * 0.7f;
		int num2 = 0;
		foreach (FBCharacter item in FBMatch.All)
		{
			if (!item)
			{
				continue;
			}
			shSt.TryGetValue(item, out var value);
			bool flag2 = item.alive && item.protUntil > now;
			if (flag2 && (value == null || value.broke >= 0f || now >= value.until))
			{
				value = (shSt[item] = new ShieldSt
				{
					start = now,
					until = item.protUntil
				});
			}
			else if (flag2)
			{
				value.until = item.protUntil;
			}
			else if (value != null && value.broke < 0f)
			{
				if (!item.alive || !(now < value.until - 0.05f))
				{
					shSt.Remove(item);
					continue;
				}
				value.broke = now;
			}
			if (value == null)
			{
				continue;
			}
			if (value.broke >= 0f && now - value.broke > 0.35f)
			{
				shSt.Remove(item);
			}
			else if (num2 < 10 && (!(item == P) || flag))
			{
				num2++;
				float num3 = Mathf.Clamp01((now - value.start) / 0.45f);
				float num4 = ((value.broke >= 0f) ? Mathf.Clamp01((now - value.broke) / 0.35f) : Mathf.Clamp01(1f - (value.until - now) / 0.6f));
				float num5 = num3 - 1f;
				float t = 1f + 2.2f * num5 * num5 * num5 + 1.2f * num5 * num5;
				Color color3 = ((item == P || ((bool)P && !FBMatch.FFA && item.team == P.team)) ? color : color2);
				float num6 = ((value.broke >= 0f) ? (1.6f - num4) : 1f) * (0.85f + 0.15f * Mathf.Sin(now * 3.2f));
				ShowAura(item, num6, num3, num4, color3);
				hashSet.Add(item);
				if (num < rings.Count)
				{
					Transform obj = rings[num++];
					obj.gameObject.SetActive(value: true);
					float scaleK = item.ScaleK;
					obj.position = item.Pos + Vector3.up * 0.03f * scaleK;
					obj.rotation = Quaternion.Euler(90f, now * 12f, 0f);
					obj.localScale = Vector3.one * 1.45f * scaleK * Mathf.Lerp(0.6f, 1f, Mathf.LerpUnclamped(0.82f, 1f, t));
					mpb.Clear();
					mpb.SetFloat(idAlpha, num6 * 0.45f);
					mpb.SetFloat(idIntro, 1f);
					mpb.SetFloat(idEnd, num4);
					mpb.SetColor(idColor, color3);
					obj.GetComponent<Renderer>().SetPropertyBlock(mpb);
				}
			}
		}
		foreach (FBCharacter item2 in auras.Keys.ToList())
		{
			if (!item2)
			{
				auras.Remove(item2);
			}
			else if (!hashSet.Contains(item2))
			{
				HideAura(item2);
			}
		}
		for (int i = num; i < rings.Count; i++)
		{
			rings[i].gameObject.SetActive(value: false);
		}
		if (shSt.Count <= 48)
		{
			return;
		}
		foreach (FBCharacter item3 in new List<FBCharacter>(shSt.Keys))
		{
			if (!item3)
			{
				shSt.Remove(item3);
			}
		}
	}

	private static float PointRayDist(Vector3 o, Vector3 d, Vector3 p)
	{
		Vector3 vector = p - o;
		float num = Mathf.Max(0f, Vector3.Dot(vector, d));
		return (vector - d * num).magnitude;
	}

	private static readonly int __fbt_LateUpdate = FBTempo.Id("FBTactics.LateUpdate");

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
		FBCharacter player = FBMatch.Player;
		if (!main || !FBMatch.I)
		{
			return;
		}
		float time = Time.time;
		Vector3 position = main.transform.position;
		int num = 0;
		if ((bool)player && player.alive)
		{
			foreach (FBCharacter bot in FBMatch.Bots)
			{
				if (num >= 6)
				{
					break;
				}
				if (!bot || !bot.alive || bot.knocked || bot.weapon == null || bot.weapon.cls != "sniper" || !FBMatch.Hostile(bot, player))
				{
					continue;
				}
				Transform transform = (bot.an ? bot.an.GetBoneTransform(HumanBodyBones.Head) : null);
				Vector3 vector = (transform ? transform.position : (bot.Pos + Vector3.up * 1.7f * bot.ScaleK));
				float num2 = Vector3.Distance(vector, position);
				if (num2 > 900f)
				{
					continue;
				}
				FBBot component = bot.GetComponent<FBBot>();
				float f = (component ? component.AimYawRad : (bot.Yaw * ((float)Math.PI / 180f)));
				float f2 = (component ? component.AimPitch : 0f);
				float num3 = Mathf.Cos(f2);
				Vector3 vector2 = new Vector3(Mathf.Sin(f) * num3, Mathf.Sin(f2), Mathf.Cos(f) * num3);
				if (PointRayDist(vector, vector2, position) > 12f)
				{
					continue;
				}
				float num4 = FBMechanics.GlintScaleFor(num2, main.fieldOfView, FBPlayer.I ? FBPlayer.I.FovBase : 70f);
				if (!(num4 <= 0f))
				{
					Vector3 normalized = (position - vector).normalized;
					if (!FBWorld.RayHit(vector + normalized * 0.4f, normalized, num2 - 0.8f))
					{
						Transform obj = glints[num];
						obj.gameObject.SetActive(value: true);
						obj.position = vector + vector2 * 0.3f;
						obj.rotation = Quaternion.LookRotation(obj.position - position);
						float num5 = 0.85f + Mathf.Sin(time * 25f + (float)num) * 0.15f;
						obj.localScale = Vector3.one * Mathf.Max(0.05f, num4 * 0.45f) * num5;
						num++;
					}
				}
			}
		}
		for (int i = num; i < 6; i++)
		{
			glints[i].gameObject.SetActive(value: false);
		}
		Shields(player, time);
		prot.SetActive((bool)player && player.alive && player.protUntil > time);
		if (prot.activeSelf)
		{
			float num6 = player.protUntil - time;
			if (player.protUntil != protSeen)
			{
				protSeen = player.protUntil;
				protTotal = Mathf.Max(0.5f, num6);
			}
			protT.text = "PROTEÇÃO DE SPAWN · " + num6.ToString("0.0") + " s";
			Fill(protBar, num6 / protTotal);
		}
		bool flag = (bool)FBPlayer.I && FBPlayer.I.FirstPerson;
		protEdge.gameObject.SetActive(prot.activeSelf && flag);
		if (protEdge.gameObject.activeSelf)
		{
			protEdge.color = new Color(1f, 1f, 1f, Mathf.Clamp01((player.protUntil - time) / 0.6f) * (0.75f + 0.25f * Mathf.Sin(time * 3.2f)));
		}
		bool flag2 = (bool)player && player.knocked;
		knock.SetActive(flag2);
		veil.SetActive(flag2);
		if (flag2)
		{
			Fill(knockBar, player.hp / 100f);
			bool flag3 = player.revivedBy != null;
			reviveBox.SetActive(flag3);
			if (flag3)
			{
				Fill(reviveBar, player.reviveP);
				reviveTxt.text = "SENDO REANIMADO" + ((player.revivedBy is FBCharacter fBCharacter) ? (" POR " + fBCharacter.Name.ToUpperInvariant()) : "");
			}
			bool flag4 = !FBMatch.TeamOf(player).Any((FBCharacter o) => (bool)o && o.alive && !o.knocked);
			knockSub.text = (flag4 ? "SEM ALIADOS DE PÉ" : "SANGRANDO · AGUENTE ATÉ UM ALIADO TE LEVANTAR");
		}
		int num7 = 0;
		if ((bool)player && FBMatch.KnockOn)
		{
			foreach (FBCharacter item in FBMatch.TeamOf(player))
			{
				if (num7 >= marks.Count)
				{
					break;
				}
				if (item.alive && item.knocked)
				{
					Vector3 vector3 = main.WorldToScreenPoint(item.Pos + Vector3.up * 0.9f * item.ScaleK);
					if (!(vector3.z < 0f) && !(vector3.x < -50f) && !(vector3.x > (float)(Screen.width + 50)) && !(vector3.y < -50f) && !(vector3.y > (float)(Screen.height + 50)))
					{
						Text text = marks[num7++];
						text.gameObject.SetActive(value: true);
						Canvas canvas = text.canvas;
						float num8 = (canvas ? canvas.scaleFactor : 1f);
						text.rectTransform.anchoredPosition = new Vector2(vector3.x / num8, vector3.y / num8 + 16f);
						text.text = "✚\n" + item.Name + " · " + Mathf.RoundToInt(Vector3.Distance(item.Pos, player.Pos)) + " m";
						text.color = ((item.revivedBy != null) ? new Color(0.93f, 0.91f, 0.86f) : new Color(1f, 0.29f, 0.23f));
					}
				}
			}
		}
		for (int j = num7; j < marks.Count; j++)
		{
			marks[j].gameObject.SetActive(value: false);
		}
		FBKillstreak2.G gunner = FBKillstreak2.gunner;
		FBKillstreak ks = FBMatch.I.ks;
		bool flag5 = FBKillstreak2.On && (bool)player && gunner == null && ks != null && (ks.kills > 0 || ks.reward != null);
		streak.SetActive(flag5);
		if (flag5)
		{
			bool flag6 = ks.Ready(time);
			int? num9 = ks.Next();
			streakN.color = (flag6 ? FBUI.Gold : Color.white);
			streakN.text = (flag6 ? ("[G] " + FBKillstreak.STREAK[ks.reward].name + " PRONTO") : (num9.HasValue ? string.Format("{0} / {1} · {2}", ks.kills, num9, (ks.reward != null) ? FBKillstreak.STREAK["heli"].name : FBKillstreak.STREAK["caveirao"].name) : (((ks.reward != null) ? FBKillstreak.STREAK[ks.reward].name : "") + " PRONTO")));
			int num10 = Mathf.Min(15, num9 ?? Mathf.Max(1, ks.kills));
			streakPips.text = new string('■', Mathf.Min(ks.kills, num10)) + "<color=#ffffff40>" + new string('■', Mathf.Max(0, num10 - ks.kills)) + "</color>";
			streakPips.supportRichText = true;
		}
		gun.SetActive(gunner != null);
		if (gunner != null)
		{
			gunTitle.text = "ATIRADOR · " + FBKillstreak.STREAK[gunner.kind].name;
			float num11 = gunner.total - gunner.t;
			gunTm.text = Mathf.Max(0, Mathf.CeilToInt(num11)) + " s";
			gunTl.text = Mathf.Max(0f, num11).ToString("0") + " s";
			Fill(gunTime, num11 / Mathf.Max(0.01f, gunner.total));
			gunAl.text = Mathf.RoundToInt(gunner.hp * 100f) + "%";
			Fill(gunArmor, gunner.hp);
			gunArmor.color = ((gunner.hp < 0.35f) ? new Color(1f, 0.29f, 0.23f) : new Color(0.93f, 0.91f, 0.86f));
			Fill(gunExit, Mathf.Min(1f, gunner.exitHold / 0.6f));
		}
	}
}
}
