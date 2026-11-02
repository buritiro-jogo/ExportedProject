using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace FreeBala
{
public class FBLoading : MonoBehaviour
{
	private static readonly string[] TIPS = new string[15]
	{
		"Segure SHIFT para correr; na corrida automática, basta empurrar o analógico até a borda.", "C agacha e Z deita: menos silhueta e mira mais firme.", "Q e E inclinam o corpo para espiar quinas sem se expor inteiro.", "Na frente de uma janela ou murinho, ESPAÇO pula por cima.", "H usa o kit médico. Procure cobertura antes de se curar.", "Caído? Um aliado segura F (ou ↑ no controle) para te levantar.", "Y marca um ponto no mapa para o seu time.", "T inspeciona a arma; dirigindo, V muda a distância da câmera.", "No 4V4 ZONA, quem morre perde o equipamento: compre com calma.", "No FARINHA × BURITI, leve o saco ou o cacho até a SUA base (verde).",
		"No DE ROLÊ, K abre a central multimídia e o paredão toca para todo mundo perto.", "Segure B para abrir a roda de emotes. Monte a sua em EMOTES no lobby.", "Headshot rende mais XP no passe do que o abate comum.", "Personalize pele, cabelo e roupas na tela PERSONAGEM.", "No Battle Royale, comece sem arma: ache uma antes de entrar em briga."
	};

	private static readonly Color Txt = new Color(0.925f, 0.91f, 0.863f);

	private static readonly Color Dim = new Color(0.925f, 0.91f, 0.863f, 0.6f);

	private static readonly Color Gold2 = new Color(1f, 0.77f, 0f);

	private string scene;

	private string mapId;

	private string modeId;

	private bool preview;

	private bool simulate;

	private float shown;

	private float target;

	private string stage = "";

	private Canvas canvas;

	private CanvasGroup group;

	private RawImage bg;

	private Image fill;

	private Text pctT;

	private Text stageT;

	private Text tipT;

	private RectTransform tipBox;

	private float t0;

	private float tipT0;

	private int tipI;

	private Vector2Int screen;

	private Rect kb0;

	private Rect kb1;

	private static Texture2D gradV;

	private static Texture2D gradH;

	private bool fading;

	public static string LastTimes;

	public static float LastTotal;

	private readonly Stopwatch clk = new Stopwatch();

	private readonly StringBuilder laps = new StringBuilder();

	private double lapT;

	private ThreadPriority prio0;

	private int up0;

	private int buf0;

	private bool boosted;

	private readonly List<Canvas> hidden = new List<Canvas>();

	public static FBLoading I { get; private set; }

	public static bool Busy => I;

	public float Progress => shown;

	public string Stage => stage;

	public float Cover
	{
		get
		{
			if (!group)
			{
				return 0f;
			}
			return group.alpha;
		}
	}

	public static bool Old => Array.IndexOf(Environment.GetCommandLineArgs(), "-fbcarregalento") >= 0;

	public static void Load(string scene, string mapId, string modeId)
	{
		if (!I)
		{
			GameObject obj = new GameObject("FB_Carregamento");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			I = obj.AddComponent<FBLoading>();
			I.scene = scene;
			I.mapId = mapId;
			I.modeId = modeId;
		}
	}

	public static FBLoading Preview(string mapId, string modeId)
	{
		if ((bool)I)
		{
			UnityEngine.Object.Destroy(I.gameObject);
		}
		GameObject obj = new GameObject("FB_Carregamento");
		UnityEngine.Object.DontDestroyOnLoad(obj);
		I = obj.AddComponent<FBLoading>();
		I.mapId = mapId;
		I.modeId = modeId;
		I.preview = true;
		I.simulate = true;
		return I;
	}

	private void OnDestroy()
	{
		Boost(on: false);
		if (I == this)
		{
			I = null;
		}
	}

	private void Start()
	{
		tipI = UnityEngine.Random.Range(0, TIPS.Length);
		Build();
		if (!preview)
		{
			StartCoroutine(Run());
		}
	}

	private static Texture2D Grad(bool vertical)
	{
		Texture2D texture2D = new Texture2D(vertical ? 1 : 64, (!vertical) ? 1 : 64, TextureFormat.RGBA32, mipChain: false)
		{
			wrapMode = TextureWrapMode.Clamp
		};
		for (int i = 0; i < 64; i++)
		{
			float a = Mathf.SmoothStep(0f, 1f, (float)i / 63f);
			texture2D.SetPixel((!vertical) ? i : 0, vertical ? i : 0, new Color(0f, 0f, 0f, a));
		}
		texture2D.Apply();
		return texture2D;
	}

	private void Build()
	{
		if ((bool)canvas)
		{
			UnityEngine.Object.Destroy(canvas.gameObject);
		}
		screen = new Vector2Int(Screen.width, Screen.height);
		bool flag = Screen.height > Screen.width;
		canvas = FBUI.Canvas("Carregamento", 1000);
		canvas.transform.SetParent(base.transform, worldPositionStays: false);
		CanvasScaler component = canvas.GetComponent<CanvasScaler>();
		component.referenceResolution = (flag ? new Vector2(900f, 1600f) : new Vector2(1600f, 900f));
		component.matchWidthOrHeight = 0.5f;
		group = canvas.gameObject.AddComponent<CanvasGroup>();
		group.blocksRaycasts = true;
		if (shown <= 0f && t0 <= 0f)
		{
			group.alpha = 0f;
		}
		RectTransform parent = (RectTransform)canvas.transform;
		FBUI.Panel(parent, "fundo", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.02f, 0.025f, 0.03f, 1f));
		bg = FBUI.Rect(parent, "arte", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>();
		bg.raycastTarget = false;
		bg.texture = Resources.Load<Texture2D>("MapShots/" + mapId) ?? Resources.Load<Texture2D>("UI/Lobby/lobby_bg");
		bg.color = new Color(0.82f, 0.82f, 0.82f);
		KenBurnsSetup();
		if ((object)gradV == null)
		{
			gradV = Grad(vertical: true);
		}
		if ((object)gradH == null)
		{
			gradH = Grad(vertical: false);
		}
		RawImage rawImage = FBUI.Rect(parent, "escuro_baixo", Vector2.zero, new Vector2(1f, flag ? 0.62f : 0.55f), Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>();
		rawImage.texture = gradV;
		rawImage.uvRect = new Rect(0f, 1f, 1f, -1f);
		rawImage.raycastTarget = false;
		RawImage rawImage2 = FBUI.Rect(parent, "escuro_topo", new Vector2(0f, 0.8f), Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>();
		rawImage2.texture = gradV;
		rawImage2.color = new Color(1f, 1f, 1f, 0.7f);
		rawImage2.raycastTarget = false;
		if (!flag)
		{
			RawImage rawImage3 = FBUI.Rect(parent, "escuro_esq", Vector2.zero, new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>();
			rawImage3.texture = gradH;
			rawImage3.uvRect = new Rect(1f, 0f, -1f, 1f);
			rawImage3.color = new Color(1f, 1f, 1f, 0.55f);
			rawImage3.raycastTarget = false;
		}
		Rect safeArea = Screen.safeArea;
		float num = Mathf.Max(1, Screen.width);
		float num2 = Mathf.Max(1, Screen.height);
		RectTransform rectTransform = FBUI.Rect(parent, "seguro", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
		if (safeArea.width > 10f)
		{
			rectTransform.anchorMin = new Vector2(safeArea.x / num, safeArea.y / num2);
			rectTransform.anchorMax = new Vector2(safeArea.xMax / num, safeArea.yMax / num2);
			Vector2 offsetMin = (rectTransform.offsetMax = Vector2.zero);
			rectTransform.offsetMin = offsetMin;
		}
		Texture2D texture2D = Resources.Load<Texture2D>("UI/Lobby/logo");
		if ((bool)texture2D)
		{
			RawImage rawImage4 = FBUI.Rect(rectTransform, "logo", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(179f, -80f), new Vector2(278f, 104f)).gameObject.AddComponent<RawImage>();
			rawImage4.texture = texture2D;
			rawImage4.raycastTarget = false;
		}
		(string id, string name, string tag, string desc) tuple = FBMenu.MODES.FirstOrDefault(((string id, string name, string tag, string desc) m) => m.id == modeId);
		string text = tuple.name ?? (modeId ?? "").ToUpper();
		string text2 = tuple.tag ?? "";
		MapDef value;
		string text3 = (GameData.Maps.TryGetValue(mapId ?? "", out value) ? value.name : mapId);
		(string, string) tuple2 = FBSession.WeatherFor(mapId ?? "");
		string[] array = new string[3] { "FÁCIL", "NORMAL", "REALISTA" };
		string text4 = text + ((text2 != "") ? (" · " + text2) : "") + " · " + array[Mathf.Clamp(FBSession.diff, 0, 2)] + ((tuple2.Item1 == "noite") ? " · NOITE" : ((tuple2.Item1 == "anoitecer") ? " · ANOITECER" : "")) + ((tuple2.Item2 == "chuva") ? " · CHUVA" : "");
		TextAnchor al = (flag ? TextAnchor.LowerCenter : TextAnchor.LowerLeft);
		Vector2 aMin = (flag ? new Vector2(0f, 0f) : new Vector2(0f, 0f));
		Vector2 aMax = (flag ? new Vector2(1f, 0f) : new Vector2(0.62f, 0f));
		float num3 = (flag ? 300 : 150);
		Off(FBUI.Label(rectTransform, "MODO · " + text, 16, aMin, aMax, new Vector2(flag ? 0 : 0, num3 + 92f), new Vector2(flag ? (-80) : (-100), 22f), al, title: true, Gold2).rectTransform, flag);
		Text text5 = FBUI.Label(rectTransform, (text3 ?? "").ToUpper(), flag ? 52 : 64, aMin, aMax, new Vector2(0f, num3 + 40f), new Vector2(flag ? (-80) : (-100), 80f), al, title: true, Txt);
		text5.resizeTextForBestFit = true;
		text5.resizeTextMinSize = 28;
		text5.resizeTextMaxSize = (flag ? 52 : 64);
		text5.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0f, -3f);
		Off(text5.rectTransform, flag);
		Off(FBUI.Label(rectTransform, text4, 15, aMin, aMax, new Vector2(0f, num3 - 6f), new Vector2(flag ? (-80) : (-100), 22f), al, title: false, Dim).rectTransform, flag);
		tipBox = FBUI.Rect(rectTransform, "dica", flag ? new Vector2(0.5f, 0f) : new Vector2(1f, 0f), flag ? new Vector2(0.5f, 0f) : new Vector2(1f, 0f), flag ? new Vector2(0f, 165f) : new Vector2(-300f, 150f), new Vector2(520f, 84f));
		Image image = tipBox.gameObject.AddComponent<Image>();
		image.color = new Color(0.03f, 0.035f, 0.04f, 0.55f);
		image.raycastTarget = false;
		FBUI.Panel(tipBox, "borda", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(2f, 0f), new Vector2(4f, 0f), Gold2).raycastTarget = false;
		FBUI.Label(tipBox, "DICA", 12, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -16f), new Vector2(-36f, 18f), TextAnchor.MiddleLeft, title: true, Gold2);
		tipT = FBUI.Label(tipBox, TIPS[tipI], 15, Vector2.zero, Vector2.one, new Vector2(0f, -8f), new Vector2(-36f, -32f), TextAnchor.UpperLeft, title: false, Txt);
		Image image2 = FBUI.Panel(rectTransform, "barra", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, flag ? 70 : 64), new Vector2(-80f, 6f), new Color(1f, 1f, 1f, 0.12f));
		image2.raycastTarget = false;
		fill = FBUI.Panel(image2.transform, "progresso", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero, Gold2);
		fill.raycastTarget = false;
		fill.rectTransform.pivot = new Vector2(0f, 0.5f);
		Shadow shadow = fill.gameObject.AddComponent<Shadow>();
		shadow.effectColor = new Color(1f, 0.6f, 0.1f, 0.5f);
		shadow.effectDistance = new Vector2(0f, -2f);
		stageT = FBUI.Label(rectTransform, stage, 14, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, flag ? 92 : 86), new Vector2(-80f, 20f), TextAnchor.MiddleLeft, title: true, Dim);
		pctT = FBUI.Label(rectTransform, "0%", 18, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, flag ? 92 : 88), new Vector2(-80f, 24f), TextAnchor.MiddleRight, title: true, Txt);
		FBUI.Label(rectTransform, "BURITIRO · BETA", 11, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, flag ? 40 : 34), new Vector2(-80f, 16f), TextAnchor.MiddleLeft, title: false, new Color(1f, 1f, 1f, 0.45f));
		Paint();
	}

	private static void Off(RectTransform r, bool portrait)
	{
		if (!portrait)
		{
			r.anchoredPosition = new Vector2(r.anchoredPosition.x + 50f, r.anchoredPosition.y);
		}
	}

	private void KenBurnsSetup()
	{
		float ar;
		float sr;
		if ((bool)bg && (bool)bg.texture)
		{
			ar = (float)bg.texture.width / (float)bg.texture.height;
			sr = (float)Screen.width / (float)Mathf.Max(1, Screen.height);
			Vector2 vector = new Vector2(0.5f + UnityEngine.Random.Range(-0.06f, 0.06f), 0.5f + UnityEngine.Random.Range(-0.04f, 0.04f));
			kb0 = Fit(1.04f, vector);
			kb1 = Fit(1.09f, vector + new Vector2(UnityEngine.Random.Range(-0.05f, 0.05f), UnityEngine.Random.Range(-0.03f, 0.03f)));
			bg.uvRect = kb0;
		}
		Rect Fit(float zoom, Vector2 c)
		{
			float num = 1f;
			float num2 = 1f;
			if (ar > sr)
			{
				num = sr / ar;
			}
			else
			{
				num2 = ar / sr;
			}
			num /= zoom;
			num2 /= zoom;
			float x = Mathf.Clamp(c.x - num / 2f, 0f, 1f - num);
			float y = Mathf.Clamp(c.y - num2 / 2f, 0f, 1f - num2);
			return new Rect(x, y, num, num2);
		}
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBLoading.Update");

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
		float num = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
		t0 += num;
		if (Screen.width != screen.x || Screen.height != screen.y)
		{
			Build();
		}
		if ((bool)bg && (bool)bg.texture)
		{
			float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t0 / 16f));
			bg.uvRect = new Rect(Vector2.Lerp(kb0.position, kb1.position, t), Vector2.Lerp(kb0.size, kb1.size, t));
		}
		if (simulate)
		{
			target = Mathf.Repeat(t0 / 9f, 1.02f);
		}
		shown = Mathf.Max(shown, Mathf.MoveTowards(shown, target, num * 0.9f));
		Paint();
		if (t0 - tipT0 > 5.5f && (bool)tipT)
		{
			tipT0 = t0;
			tipI = (tipI + 1) % TIPS.Length;
			tipT.text = TIPS[tipI];
			FBCustomizeFx.Fade(tipT, 0f, 1f, 0.4f);
			FBCustomizeFx.SlideIn(tipT.rectTransform, new Vector2(0f, -8f), 0.4f);
		}
		if ((bool)group && !fading && group.alpha < 1f)
		{
			group.alpha = Mathf.MoveTowards(group.alpha, 1f, num * 4f);
		}
	}

	private void Paint()
	{
		if ((bool)fill)
		{
			fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(shown), 1f);
		}
		if ((bool)pctT)
		{
			pctT.text = Mathf.RoundToInt(Mathf.Clamp01(shown) * 100f) + "%";
		}
		if ((bool)stageT)
		{
			stageT.text = stage + new string('.', 1 + (int)(t0 * 2.5f) % 3);
		}
	}

	public void SetPreviewStage(string s, float p)
	{
		stage = s;
		target = p;
		shown = p;
		preview = true;
		simulate = false;
		t0 = Mathf.Max(t0, 6f);
		Paint();
	}

	private void Lap(string n)
	{
		double totalSeconds = clk.Elapsed.TotalSeconds;
		laps.Append($"{n} {totalSeconds - lapT:0.00}s · ");
		lapT = totalSeconds;
	}

	private void Boost(bool on)
	{
		if (on != boosted && !Old)
		{
			boosted = on;
			if (on)
			{
				prio0 = Application.backgroundLoadingPriority;
				up0 = QualitySettings.asyncUploadTimeSlice;
				buf0 = QualitySettings.asyncUploadBufferSize;
				Application.backgroundLoadingPriority = ThreadPriority.High;
				QualitySettings.asyncUploadTimeSlice = (Application.isMobilePlatform ? 24 : 33);
				QualitySettings.asyncUploadBufferSize = Mathf.Max(buf0, Application.isMobilePlatform ? 64 : 128);
			}
			else
			{
				Application.backgroundLoadingPriority = prio0;
				QualitySettings.asyncUploadTimeSlice = up0;
				QualitySettings.asyncUploadBufferSize = buf0;
			}
		}
	}

	private IEnumerator Run()
	{
		clk.Start();
		stage = "Preparando";
		target = 0.02f;
		Boost(on: true);
		while (group.alpha < 0.99f)
		{
			yield return null;
		}
		Canvas[] array = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
		foreach (Canvas canvas in array)
		{
			if (canvas != this.canvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
			{
				canvas.enabled = false;
			}
		}
		stage = "Carregando o mapa";
		AsyncOperation op = SceneManager.LoadSceneAsync(scene);
		if (op == null)
		{
			Debug.LogError("[FreeBala] cena não existe: " + scene);
			UnityEngine.Object.Destroy(base.gameObject);
			yield break;
		}
		op.allowSceneActivation = false;
		Lap("cobrir a tela");
		while (op.progress < 0.9f)
		{
			target = 0.05f + op.progress / 0.9f * 0.5f;
			yield return null;
		}
		Lap("ler a cena");
		target = 0.56f;
		op.allowSceneActivation = true;
		while (!op.isDone)
		{
			yield return null;
		}
		Lap("ativar a cena");
		stage = "Montando a partida";
		float w3 = 0f;
		while ((!FBMatch.I || FBMatch.I.state == "loading" || !FBMatch.Player) && w3 < 20f)
		{
			w3 += Time.unscaledDeltaTime;
			target = Mathf.Min(0.68f, 0.56f + w3 * 0.02f);
			yield return null;
		}
		Lap("montar a partida");
		float ts = Time.timeScale;
		Time.timeScale = 0f;
		HideOtherUI(hide: true);
		stage = "Preparando efeitos";
		target = 0.72f;
		yield return null;
		try
		{
			FBBloodFx.Warm();
			FBDecalFx.Warm();
			FBImpactFx.Warm();
			FBShotFx.Warm();
			FBExplosionFx.Warm();
			FBWoundFx.Warm();
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[FreeBala] aquecimento de FX: " + ex.Message);
		}
		yield return null;
		Lap("efeitos");
		stage = "Aquecendo gráficos";
		yield return WarmViews(0.74f, 0.92f);
		Lap("aquecer gráficos");
		stage = "Carregando áudio";
		target = 0.94f;
		w3 = 0f;
		while (w3 < 4f && UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any((AudioSource a) => (bool)a && (bool)a.clip && a.clip.loadState == AudioDataLoadState.Loading))
		{
			w3 += Time.unscaledDeltaTime;
			yield return null;
		}
		Lap("áudio");
		stage = "Quase lá";
		int ok = 0;
		w3 = 0f;
		while (ok < 10 && w3 < 4f)
		{
			yield return null;
			w3 += Time.unscaledDeltaTime;
			ok = ((Time.unscaledDeltaTime < 0.05f) ? (ok + 1) : 0);
			target = Mathf.Min(0.99f, 0.95f + w3 * 0.01f + (float)ok * 0.004f);
		}
		Lap("quadros estáveis");
		stage = "Pronto";
		target = 1f;
		while (shown < 0.995f)
		{
			yield return null;
		}
		yield return new WaitForSecondsRealtime(0.25f);
		Lap("barra até 100%");
		LastTotal = (float)clk.Elapsed.TotalSeconds;
		LastTimes = laps.ToString();
		Debug.Log(string.Format("[FreeBala][carregamento] {0}:{1} · {2} · total {3:0.0} s · {4}{5}", mapId, modeId, Old ? "MODO ANTIGO" : "prioridade alta", LastTotal, LastTimes, FBMatch.BuildTimes));
		Boost(on: false);
		Time.timeScale = ((ts <= 0f) ? 1f : ts);
		HideOtherUI(hide: false);
		fading = true;
		for (float f = 0f; f < 0.55f; f += Time.unscaledDeltaTime)
		{
			group.alpha = 1f - Mathf.SmoothStep(0f, 1f, f / 0.55f);
			yield return null;
		}
		UnityEngine.Object.Destroy(base.gameObject);
	}

	private void HideOtherUI(bool hide)
	{
		if (hide)
		{
			Canvas[] array = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
			foreach (Canvas canvas in array)
			{
				if (canvas != this.canvas && canvas.enabled && canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.sortingOrder >= 1000)
				{
					canvas.enabled = false;
					hidden.Add(canvas);
				}
			}
			return;
		}
		foreach (Canvas item in hidden)
		{
			if ((bool)item)
			{
				item.enabled = true;
			}
		}
		hidden.Clear();
	}

	private IEnumerator WarmViews(float p0, float p1)
	{
		Camera main = Camera.main;
		if (!main)
		{
			target = p1;
			yield break;
		}
		GameObject go = new GameObject("cam_aquecimento");
		Camera cam = go.AddComponent<Camera>();
		cam.CopyFrom(main);
		cam.enabled = false;
		RenderTexture rt = (cam.targetTexture = new RenderTexture(Application.isMobilePlatform ? 256 : 384, Application.isMobilePlatform ? 144 : 216, 24)
		{
			name = "rt_aquecimento"
		});
		Vector3 pos = (FBMatch.Player ? (FBMatch.Player.transform.position + Vector3.up * 1.6f) : main.transform.position);
		Quaternion[] dirs = new Quaternion[6]
		{
			Quaternion.Euler(8f, 0f, 0f),
			Quaternion.Euler(8f, 90f, 0f),
			Quaternion.Euler(8f, 180f, 0f),
			Quaternion.Euler(8f, 270f, 0f),
			Quaternion.Euler(45f, 45f, 0f),
			Quaternion.Euler(-20f, 225f, 0f)
		};
		for (int j = 0; j < dirs.Length; j++)
		{
			cam.transform.SetPositionAndRotation(pos, dirs[j]);
			try
			{
				RenderPipeline.StandardRequest data = new RenderPipeline.StandardRequest
				{
					destination = rt
				};
				if (RenderPipeline.SupportsRenderRequest(cam, data))
				{
					RenderPipeline.SubmitRenderRequest(cam, data);
				}
				else
				{
					cam.Render();
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[FreeBala] aquecimento de shaders: " + ex.Message);
			}
			target = Mathf.Lerp(p0, p1, ((float)j + 1f) / (float)dirs.Length);
			yield return null;
		}
		Shader.EnableKeyword("LOD_FADE_CROSSFADE");
		for (int j = 0; j < dirs.Length; j += 2)
		{
			cam.transform.SetPositionAndRotation(pos, dirs[j]);
			try
			{
				RenderPipeline.StandardRequest data2 = new RenderPipeline.StandardRequest
				{
					destination = rt
				};
				if (RenderPipeline.SupportsRenderRequest(cam, data2))
				{
					RenderPipeline.SubmitRenderRequest(cam, data2);
				}
				else
				{
					cam.Render();
				}
			}
			catch (Exception ex2)
			{
				Debug.LogWarning("[FreeBala] aquecimento de shaders (LOD): " + ex2.Message);
			}
			yield return null;
		}
		Shader.DisableKeyword("LOD_FADE_CROSSFADE");
		cam.targetTexture = null;
		UnityEngine.Object.Destroy(go);
		rt.Release();
		UnityEngine.Object.Destroy(rt);
	}

	public static IEnumerator Shots(string dir, string mapId, string modeId)
	{
		Directory.CreateDirectory(dir);
		FBLoading L = Preview(mapId, modeId);
		yield return null;
		L.SetPreviewStage("Carregando o mapa", 0.32f);
		yield return Shot("16x9_mapa");
		L.SetPreviewStage("Aquecendo gráficos", 0.81f);
		yield return Shot("16x9_graficos");
		if (!Application.isMobilePlatform)
		{
			int w0 = Screen.width;
			int h0 = Screen.height;
			FullScreenMode fs = Screen.fullScreenMode;
			Screen.SetResolution(2560, 1080, FullScreenMode.Windowed);
			for (int k = 0; k < 20; k++)
			{
				yield return null;
			}
			L.SetPreviewStage("Montando a partida", 0.63f);
			yield return Shot("21x9");
			Screen.SetResolution(720, 1280, FullScreenMode.Windowed);
			for (int k = 0; k < 20; k++)
			{
				yield return null;
			}
			L.SetPreviewStage("Quase lá", 0.97f);
			yield return Shot("retrato");
			Screen.SetResolution(w0, h0, fs);
			for (int k = 0; k < 10; k++)
			{
				yield return null;
			}
		}
		if ((bool)L)
		{
			UnityEngine.Object.Destroy(L.gameObject);
		}
		IEnumerator Shot(string n)
		{
			for (int l = 0; l < 8; l++)
			{
				yield return null;
			}
			ScreenCapture.CaptureScreenshot(Path.Combine(dir, "carregamento_" + n + ".png"));
			yield return null;
			yield return null;
			Debug.Log("[FreeBala][carregamento] print " + n);
		}
	}
}
}
