using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace FreeBala
{
public class FBMenu : MonoBehaviour
{
	public static readonly (string id, string name, string tag, string desc)[] MODES = new(string, string, string, string)[4]
	{
		("role", "DE ROLÊ", "ROLE PLAY", "Modo livre: sem armas e sem inimigos. Carros liberados, paredão de som (K) e NPCs que curtem perto do som."),
		("classico", "CLÁSSICO", "5 × 5", "Mata-mata 5×5. Primeiro time a 40 abates ou quem liderar após 8 minutos."),
		("squad", "4X4", "RODADAS", "Estilo Clash Squad: compra no início da rodada, zona que fecha, primeiro a vencer 4 rodadas. Morreu, perde o equipamento."),
		("10x10", "10X10", "RODADAS · COMPRA", "Dois times de 10 nascem juntos, cada um do seu lado. Compre no começo da rodada; abates e rodadas dão dinheiro. Primeiro a vencer 7 rodadas. Morreu, perde o equipamento.")
	};

	private static readonly Dictionary<string, string> MAP_INFO = new Dictionary<string, string>
	{
		["vila_jardim"] = "Favela · becos, lajes e telhados · combate de perto",
		["praca_aguas"] = "Centro de Boa Vista · praça aberta e prédios · longa distância",
		["praca_pt"] = "Parque Germano Sampaio · lagoa, mata, quadras · média distância",
		["orla"] = "Parque do Rio Branco · Mirante, praia, calçadão · média/longa",
		["estande"] = "Estande de tiro · alvos de 10 a 100 m"
	};

	private static readonly string[] DIFFS = new string[3] { "FÁCIL", "NORMAL", "REALISTA" };

	private List<MapDef> maps;

	private List<SkinDef> skins;

	private Transform root;

	private Transform page;

	private GameObject preview;

	private GameObject wpPreview;

	private Camera cam;

	private Text moneyT;

	private Text nameT;

	private Text modeT;

	private Text mapT;

	private Text charT;

	private RawImage modeArt;

	private static readonly Vector3 CamHome = new Vector3(0f, 1.18f, -6.3f);

	private static readonly string[] CLASS_ORDER = new string[5] { "fuzil", "submetralhadora", "sniper", "escopeta", "pistola" };

	private static readonly Dictionary<string, string> CLASS_N = new Dictionary<string, string>
	{
		["fuzil"] = "Fuzis",
		["submetralhadora"] = "Submetralhadoras",
		["sniper"] = "Snipers",
		["escopeta"] = "Escopetas",
		["pistola"] = "Pistolas"
	};

	private static readonly (string k, string n)[] LOADOUT = new(string, string)[3]
	{
		("primary", "PRINCIPAL"),
		("smg", "SMG"),
		("secondary", "PISTOLA")
	};

	private static readonly (string kind, string l)[] PARTS = new(string, string)[5]
	{
		("scopes", "MIRA"),
		("muzzles", "CANO"),
		("magazines", "CARREGADOR"),
		("lasers", "LASER"),
		("grips", "EMPUNHADURA")
	};

	private string arCls;

	private string arSel;

	private string arOpen;

	private int arPage = -1;

	private float arYaw;

	private bool askedName;

	private GameObject nameDlg;

	public static FBMenu I { get; private set; }

	public FBCustomize Customize { get; private set; }

	private void Start()
	{
		GameData.Load();
		FBSession.Load();
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
		Time.timeScale = 1f;
		maps = GameData.Maps.Values.Where((MapDef m) => !m.disabled && GameData.AllowedMaps.Contains(m.id) && Application.CanStreamedLevelBeLoaded("Map_" + m.id)).ToList();
		if (maps.Count == 0)
		{
			maps = GameData.Maps.Values.Where((MapDef m) => !m.disabled && (m.id == "vila_jardim" || m.id == "praca_aguas")).ToList();
		}
		skins = GameData.Skins.Where((SkinDef s) => !s.role && (bool)FBRegistry.I && FBRegistry.I.HasChar(s.id)).ToList();
		if (FBSession.mode == "4v4")
		{
			FBSession.mode = "squad";
		}
		if (MODES.All(((string id, string name, string tag, string desc) m) => m.id != FBSession.mode))
		{
			FBSession.mode = "classico";
		}
		if (!maps.Any((MapDef m) => m.id == FBSession.map))
		{
			FBSession.map = "vila_jardim";
		}
		if (FBSession.pickRandom)
		{
			bool fem = FBCustomizeData.Find(FBSession.skin)?.fem ?? false;
			List<FBCustomizeData.Entry> list = FBCustomizeData.Roster.Where((FBCustomizeData.Entry e) => e.fem == fem).ToList();
			if (list.Count > 0)
			{
				FBSession.skin = list[UnityEngine.Random.Range(0, list.Count)].id;
				FBSession.look = FBCustomizeData.LookFor(FBSession.skin);
			}
		}
		cam = Camera.main;
		if ((bool)cam)
		{
			cam.transform.SetPositionAndRotation(CamHome, Quaternion.identity);
			cam.fieldOfView = 30f;
		}
		BuildStage();
		root = FBUI.Canvas("Lobby", 5).transform;
		Home();
		FBAudio.SetLoop("menu_music", "music", 0.35f * FBSession.music / 0.35f);
		I = this;
		if (Array.IndexOf(Environment.GetCommandLineArgs(), "-fbcustom") >= 0)
		{
			StartCoroutine(CustomShots());
		}
		if (Array.IndexOf(Environment.GetCommandLineArgs(), "-fblobby") >= 0)
		{
			StartCoroutine(LobbyShots());
		}
		if (Array.IndexOf(Environment.GetCommandLineArgs(), "-fbchars") >= 0)
		{
			StartCoroutine(CharShots());
		}
		if (Array.IndexOf(Environment.GetCommandLineArgs(), "-fbload") >= 0)
		{
			StartCoroutine(LoadShots());
		}
		if (Array.IndexOf(Environment.GetCommandLineArgs(), "-fbmenu") >= 0)
		{
			StartCoroutine(MenuShots());
		}
	}

	private void OnDestroy()
	{
		FBAudio.StopLoop("menu_music");
	}

	private static Texture2D T(string n)
	{
		return Resources.Load<Texture2D>("UI/Lobby/" + n);
	}

	private void BuildStage()
	{
		FBWeather.ResetGlobals();
		FBVolumetric.Clear();
		RenderSettings.ambientMode = AmbientMode.Trilight;
		RenderSettings.ambientSkyColor = new Color(0.42f, 0.4f, 0.4f);
		RenderSettings.ambientEquatorColor = new Color(0.3f, 0.25f, 0.22f);
		RenderSettings.ambientGroundColor = new Color(0.15f, 0.1f, 0.06f);
		RenderSettings.fog = false;
		if ((bool)cam)
		{
			cam.clearFlags = CameraClearFlags.Color;
			cam.backgroundColor = new Color(0.03f, 0.04f, 0.05f);
		}
		GameObject gameObject = new GameObject("LobbyFundo");
		Canvas canvas = gameObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceCamera;
		canvas.worldCamera = cam;
		canvas.planeDistance = 60f;
		canvas.sortingOrder = -100;
		CanvasScaler canvasScaler = gameObject.AddComponent<CanvasScaler>();
		canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		canvasScaler.referenceResolution = new Vector2(1600f, 900f);
		canvasScaler.matchWidthOrHeight = 0.5f;
		GameObject obj = new GameObject("foto", typeof(RectTransform), typeof(RawImage));
		obj.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RawImage component = obj.GetComponent<RawImage>();
		component.texture = T("lobby_bg");
		component.raycastTarget = false;
		AspectRatioFitter aspectRatioFitter = obj.AddComponent<AspectRatioFitter>();
		aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
		aspectRatioFitter.aspectRatio = 1.7777778f;
		GameObject obj2 = new GameObject("luz_chao", typeof(RectTransform), typeof(RawImage));
		obj2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform obj3 = (RectTransform)obj2.transform;
		Vector2 anchorMin = (obj3.anchorMax = new Vector2(0.5f, 0f));
		obj3.anchorMin = anchorMin;
		obj3.anchoredPosition = new Vector2(0f, 119f);
		obj3.sizeDelta = new Vector2(820f, 140f);
		RawImage component2 = obj2.GetComponent<RawImage>();
		component2.texture = T("floor_glow");
		component2.raycastTarget = false;
		Light light = new GameObject("LuzChave").AddComponent<Light>();
		light.type = LightType.Directional;
		light.intensity = 1.25f;
		light.color = new Color(1f, 0.9f, 0.78f);
		light.transform.rotation = Quaternion.Euler(24f, 20f, 0f);
		light.shadows = LightShadows.None;
		Light light2 = new GameObject("Recorte").AddComponent<Light>();
		light2.type = LightType.Spot;
		light2.range = 12f;
		light2.spotAngle = 55f;
		light2.intensity = 26f;
		light2.color = new Color(1f, 0.62f, 0.22f);
		light2.transform.SetPositionAndRotation(new Vector3(-2.2f, 3.2f, 2.4f), Quaternion.LookRotation(new Vector3(2.2f, -2.2f, -2.4f)));
		Light light3 = new GameObject("Recorte2").AddComponent<Light>();
		light3.type = LightType.Spot;
		light3.range = 12f;
		light3.spotAngle = 55f;
		light3.intensity = 18f;
		light3.color = new Color(1f, 0.35f, 0.12f);
		light3.transform.SetPositionAndRotation(new Vector3(2.2f, 3f, 2.2f), Quaternion.LookRotation(new Vector3(-2.2f, -2f, -2.2f)));
		Light light4 = new GameObject("Preenchimento").AddComponent<Light>();
		light4.type = LightType.Point;
		light4.range = 9f;
		light4.intensity = 3f;
		light4.color = new Color(1f, 0.78f, 0.5f);
		light4.transform.position = new Vector3(0f, 0.4f, -2.2f);
	}

	private void Preview(bool withWeapon = true, string dance = null)
	{
		if ((bool)preview)
		{
			UnityEngine.Object.Destroy(preview);
		}
		GameObject gameObject = (FBRegistry.I ? FBRegistry.I.Char(FBSession.skin) : null);
		if ((bool)gameObject)
		{
			preview = UnityEngine.Object.Instantiate(gameObject, Vector3.zero, Quaternion.Euler(0f, 180f, 0f));
			preview.name = "Previa";
			FBWardrobe.ApplyTo(preview, FBSession.look);
			FBCharacter component = preview.GetComponent<FBCharacter>();
			component.relaxed = true;
			component.isPlayer = false;
			component.skinId = FBSession.skin;
			if (withWeapon && GameData.Weapons.TryGetValue(FBSession.primary, out var value))
			{
				component.SetWeapon(value, FBRegistry.I.Weapon(value.id));
			}
			FBLobbyDirector fBLobbyDirector = preview.AddComponent<FBLobbyDirector>();
			fBLobbyDirector.cam = cam;
			if (dance != null)
			{
				fBLobbyDirector.home = false;
				component.PlayDance(dance);
			}
		}
	}

	private void Clear()
	{
		foreach (Transform item in root)
		{
			UnityEngine.Object.Destroy(item.gameObject);
		}
		if ((bool)wpPreview)
		{
			UnityEngine.Object.Destroy(wpPreview);
		}
	}

	private Transform Page(string title, bool dimStage = false)
	{
		Clear();
		Transform obj = FBUI.Panel(root, "pagina", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, dimStage ? FBKit.Bg : new Color(0f, 0f, 0f, 0f)).transform;
		Transform parent = FBUI.Panel(obj, "topo", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -32f), new Vector2(0f, 64f), new Color(0.02f, 0.025f, 0.03f, 0.92f)).transform;
		FBUI.Panel(parent, "fio", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0.5f), new Vector2(0f, 1f), FBKit.Line).raycastTarget = false;
		FBKit.TextTile(FBUI.Rect(parent, "voltar", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(84f, 0f), new Vector2(120f, 38f)), "‹  VOLTAR", 0f, 0f, 120f, 38f, sel: false, Home, 14);
		FBUI.Label(parent, title, 22, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(470f, 0f), new Vector2(600f, 40f), TextAnchor.MiddleLeft, title: true, FBKit.Txt);
		moneyT = FBUI.Label(parent, $"$ {FBSession.money:N0}", 18, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-134f, 0f), new Vector2(220f, 36f), TextAnchor.MiddleRight, title: true, new Color(0.45f, 0.95f, 0.55f));
		return obj;
	}

	private void Home()
	{
		Clear();
		Preview();
		if ((bool)cam)
		{
			cam.transform.SetPositionAndRotation(CamHome, Quaternion.identity);
		}
		Transform transform = root;
		Color value = new Color(0.925f, 0.91f, 0.863f);
		Color gold2 = new Color(1f, 0.77f, 0f);
		Color dim = new Color(0.925f, 0.91f, 0.863f, 0.55f);
		int num = 1 + (int)Mathf.Sqrt((float)FBSession.xp / 120f);
		RawImage rawImage = Img(transform, "prof_bg", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(204f, -55f), new Vector2(360f, 70f));
		Image image = FBUI.Panel(rawImage.transform, "moldura", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(37f, 0f), new Vector2(60f, 60f), gold2);
		image.raycastTarget = false;
		Texture2D texture2D = FBCustomizeThumbs.Get(FBSession.skin) ?? T("avatar");
		RawImage rawImage2 = FBUI.Rect(image.transform, "avatar", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-5f, -5f)).gameObject.AddComponent<RawImage>();
		rawImage2.texture = texture2D;
		rawImage2.raycastTarget = false;
		rawImage2.uvRect = ((texture2D == T("avatar")) ? new Rect(0f, 0.25f, 1f, 0.75f) : new Rect(0f, 0f, 1f, 1f));
		nameT = FBUI.Label(rawImage.transform, (FBSession.name ?? "JOGADOR").ToUpper(), 19, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(224f, 10f), new Vector2(280f, 30f), TextAnchor.MiddleLeft, title: true, value);
		FBUI.Label(rawImage.transform, $"NÍVEL {num}", 13, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(224f, -14f), new Vector2(280f, 20f), TextAnchor.MiddleLeft, title: false, gold2);
		Button button = rawImage.gameObject.AddComponent<Button>();
		rawImage.raycastTarget = true;
		button.onClick.AddListener(NameDialog);
		FBUI.Label(rawImage.transform, "✎", 15, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, 10f), new Vector2(24f, 24f), TextAnchor.MiddleCenter, title: false, dim);
		if (!FBSession.nameChosen && !askedName && Array.IndexOf(Environment.GetCommandLineArgs(), "-batchmode") < 0 && !Environment.GetCommandLineArgs().Any((string a) => a.StartsWith("-fb")))
		{
			askedName = true;
			NameDialog();
		}
		Img(transform, "logo", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(14f, -60f), new Vector2(240f, 80f));
		Image image2 = FBUI.Panel(transform, "beta_tag", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(14f, -106f), new Vector2(140f, 20f), new Color(0.82f, 0.06f, 0f, 0.85f));
		image2.raycastTarget = false;
		FBUI.Label(image2.transform, "VERSÃO BETA", 11, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, title: true, Color.white);
		Image image3 = FBUI.Panel(transform, "dinheiro", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-104f, -50f), new Vector2(152f, 44f), new Color(0f, 0f, 0f, 0.6f));
		image3.raycastTarget = false;
		RawImage rawImage3 = FBUI.Rect(image3.transform, "cash", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(34f, 34f)).gameObject.AddComponent<RawImage>();
		rawImage3.texture = T("cash");
		rawImage3.raycastTarget = false;
		moneyT = FBUI.Label(image3.transform, FBSession.money.ToString("N0", new CultureInfo("pt-BR")), 25, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, TextAnchor.MiddleLeft, title: true, value);
		moneyT.rectTransform.offsetMin = new Vector2(52f, 0f);
		moneyT.rectTransform.offsetMax = new Vector2(-8f, 0f);
		(string, string, Action)[] array = new(string, string, Action)[7]
		{
			("PASSE", "ic_ticket", Pass),
			("ARMAS", "ic_rifle", Arsenal),
			("PERSONAGEM", "ic_user", Characters),
			("EMOTES", "ic_dance", Emotes),
			("CONTROLES", "ic_keyboard", Controls),
			("AJUSTES", "ic_gear", delegate
			{
				FBSettings.Open(root);
			}),
			("AMIGOS", "ic_users", Online)
		};
		float num2 = (float)((array.Length - 1) * 44) / 2f;
		for (int j = 0; j < array.Length; j++)
		{
			(string label, string ic, Action act) i = array[j];
			RectTransform row = FBUI.Rect(transform, "nav_" + i.label, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(138f, num2 - (float)(j * 44)), new Vector2(220f, 40f));
			RawImage bgi = row.gameObject.AddComponent<RawImage>();
			bgi.texture = T("nav_bg");
			Image bar = FBUI.Panel(row, "borda", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(2f, 0f), new Vector2(4f, 0f), gold2);
			bar.raycastTarget = false;
			bar.enabled = false;
			RawImage ico = FBUI.Rect(row, "ic", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(20f, 20f)).gameObject.AddComponent<RawImage>();
			ico.texture = T(i.ic);
			ico.color = dim;
			ico.raycastTarget = false;
			Text lb = FBUI.Label(row, i.label, 14, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(136f, 0f), new Vector2(180f, 24f), TextAnchor.MiddleLeft, title: true, new Color(0.925f, 0.91f, 0.863f, 0.85f));
			if (i.label == "PASSE")
			{
				int num3 = FBPass.Claimable();
				Image image4 = FBUI.Panel(row, "tag", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-42f, 0f), new Vector2(68f, 16f), new Color(0.82f, 0.06f, 0f));
				image4.raycastTarget = false;
				FBUI.Label(image4.transform, (num3 > 0) ? (num3 + " PRÊMIO" + ((num3 > 1) ? "S" : "")) : "NOVO", 9, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, title: true);
			}
			Button button2 = row.gameObject.AddComponent<Button>();
			button2.targetGraphic = bgi;
			button2.transition = Selectable.Transition.None;
			button2.onClick.AddListener(delegate
			{
				FBAudio.Synth("hover");
				i.act();
			});
			EventTrigger eventTrigger = row.gameObject.AddComponent<EventTrigger>();
			float x0 = row.anchoredPosition.x;
			EventTrigger.Entry entry = new EventTrigger.Entry
			{
				eventID = EventTriggerType.PointerEnter
			};
			entry.callback.AddListener(delegate
			{
				Hover(on: true);
			});
			eventTrigger.triggers.Add(entry);
			EventTrigger.Entry entry2 = new EventTrigger.Entry
			{
				eventID = EventTriggerType.PointerExit
			};
			entry2.callback.AddListener(delegate
			{
				Hover(on: false);
			});
			eventTrigger.triggers.Add(entry2);
			void Hover(bool on)
			{
				bgi.texture = T(on ? "nav_hover" : "nav_bg");
				bar.enabled = on;
				ico.color = (on ? gold2 : dim);
				lb.color = (on ? Color.white : new Color(0.925f, 0.91f, 0.863f, 0.85f));
				row.anchoredPosition = new Vector2(x0 + (float)(on ? 7 : 0), row.anchoredPosition.y);
			}
		}
		(string, string, string, string) tuple = MODES.FirstOrDefault(((string id, string name, string tag, string desc) m) => m.id == FBSession.mode);
		if (tuple.Item1 == null)
		{
			tuple = MODES[0];
		}
		Image image5 = FBUI.Panel(transform, "modo", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-188f, -168f), new Vector2(320f, 152f), new Color(0.04f, 0.047f, 0.055f, 0.82f));
		image5.gameObject.AddComponent<Outline>().effectColor = new Color(0.925f, 0.91f, 0.863f, 0.12f);
		image5.gameObject.AddComponent<Button>().onClick.AddListener(MatchPicker);
		modeArt = FBUI.Rect(image5.transform, "arte", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -50f), new Vector2(0f, 100f)).gameObject.AddComponent<RawImage>();
		modeArt.raycastTarget = false;
		Texture2D texture2D2 = Resources.Load<Texture2D>("MapShots/" + FBSession.map);
		modeArt.texture = (texture2D2 ? texture2D2 : Resources.Load<Texture2D>("Modes/" + tuple.Item1));
		if ((bool)modeArt.texture)
		{
			float num4 = (float)modeArt.texture.width / (float)modeArt.texture.height;
			float num5 = 3.2f;
			modeArt.uvRect = ((num4 > num5) ? new Rect((1f - num5 / num4) / 2f, 0f, num5 / num4, 1f) : new Rect(0f, (1f - num4 / num5) / 2f, 1f, num4 / num5));
		}
		RawImage rawImage4 = FBUI.Rect(image5.transform, "fade", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -50f), new Vector2(0f, 100f)).gameObject.AddComponent<RawImage>();
		rawImage4.texture = T("card_fade");
		rawImage4.raycastTarget = false;
		Image image6 = FBUI.Panel(image5.transform, "trocar", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-57f, -25f), new Vector2(84f, 24f), new Color(0.82f, 0.06f, 0f));
		image6.raycastTarget = false;
		FBUI.Label(image6.transform, "TROCAR", 13, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, title: true);
		FBUI.Label(image5.transform, "MODO · " + tuple.Item3, 11, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 66f), new Vector2(-36f, 16f), TextAnchor.MiddleLeft, title: false, gold2);
		modeT = FBUI.Label(image5.transform, tuple.Item2, 22, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 44f), new Vector2(-32f, 28f), TextAnchor.MiddleLeft, title: true, value);
		(string, string) tuple2 = FBSession.WeatherFor(FBSession.map);
		MapDef value2;
		string text = (GameData.Maps.TryGetValue(FBSession.map, out value2) ? value2.name : FBSession.map);
		mapT = FBUI.Label(image5.transform, text + " · " + DIFFS[Mathf.Clamp(FBSession.diff, 0, 2)] + ((tuple2.Item1 == "noite") ? " · NOITE" : ((tuple2.Item1 == "anoitecer") ? " · ANOITECER" : "")) + ((tuple2.Item2 == "chuva") ? " · CHUVA" : ""), 12, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 20f), new Vector2(-32f, 18f), TextAnchor.MiddleLeft, title: false, new Color(0.925f, 0.91f, 0.863f, 0.75f));
		Image image7 = FBUI.Panel(transform, "amigos", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-188f, 140f), new Vector2(320f, 44f), new Color(0.04f, 0.047f, 0.055f, 0.6f));
		image7.gameObject.AddComponent<Outline>().effectColor = new Color(1f, 1f, 1f, 0.22f);
		image7.gameObject.AddComponent<Button>().onClick.AddListener(Online);
		RawImage rawImage5 = FBUI.Rect(image7.transform, "ic", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-100f, 0f), new Vector2(18f, 18f)).gameObject.AddComponent<RawImage>();
		rawImage5.texture = T("ic_users");
		rawImage5.raycastTarget = false;
		FBUI.Label(image7.transform, "JOGAR COM AMIGOS", 14, Vector2.zero, Vector2.one, new Vector2(14f, 0f), Vector2.zero, TextAnchor.MiddleCenter, title: true, value);
		RawImage rawImage6 = Img(transform, "btn_start", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-188f, 68f), new Vector2(320f, 80f));
		rawImage6.raycastTarget = true;
		Button button3 = rawImage6.gameObject.AddComponent<Button>();
		button3.targetGraphic = rawImage6;
		ColorBlock colors = button3.colors;
		colors.highlightedColor = new Color(1f, 1f, 0.85f);
		colors.pressedColor = new Color(0.9f, 0.85f, 0.7f);
		button3.colors = colors;
		button3.onClick.AddListener(Play);
		FBUI.Label(rawImage6.transform, "COMEÇAR", 32, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(8f, 7f), new Vector2(0f, 40f), TextAnchor.MiddleCenter, title: true, new Color(0.165f, 0.082f, 0f)).fontStyle = FontStyle.Italic;
		FBUI.Label(rawImage6.transform, tuple.Item2 + " · " + text.ToUpper(), 10, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(8f, -21f), new Vector2(0f, 16f), TextAnchor.MiddleCenter, title: false, new Color(0.165f, 0.082f, 0f, 0.8f));
		RectTransform rectTransform = FBUI.Rect(transform, "nome", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(520f, 56f));
		rectTransform.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
		rectTransform.gameObject.AddComponent<Button>().onClick.AddListener(Characters);
		SkinDef skinDef = FBCustomizeData.Find(FBSession.skin)?.def;
		string text2 = ((skinDef?.sex == "f") ? "FEMININO" : "MASCULINO");
		FBUI.Label(rectTransform, (skinDef?.name ?? FBSession.skin).ToUpper(), 24, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(520f, 34f), TextAnchor.MiddleCenter, title: true, value).gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0f, -3f);
		FBUI.Label(rectTransform, (FBSession.pickRandom ? ("ALEATÓRIO · " + text2) : "PERSONAGEM FIXO") + " · TROCAR ›", 13, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(520f, 18f), TextAnchor.MiddleCenter, title: false, gold2);
		static RawImage Img(Transform par, string n, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
		{
			RawImage rawImage7 = FBUI.Rect(par, n, aMin, aMax, pos, size).gameObject.AddComponent<RawImage>();
			rawImage7.texture = T(n);
			rawImage7.raycastTarget = false;
			return rawImage7;
		}
	}

	private static string WName(string id)
	{
		if (!GameData.Weapons.TryGetValue(id ?? "", out var value))
		{
			return "—";
		}
		return value.name;
	}

	private void MatchPicker()
	{
		Transform transform = Page("ESCOLHER PARTIDA", dimStage: true);
		if ((bool)preview)
		{
			preview.SetActive(value: false);
		}
		RectTransform b = FBKit.Body(transform);
		FBKit.Section(b, "MODO DE JOGO", 0f, 18f, 400f);
		for (int l = 0; l < MODES.Length; l++)
		{
			(string id, string name, string tag, string desc) k = MODES[l];
			float x = (float)(l % 4) * 198f;
			float y = 46f + (float)(l / 4) * 112f;
			bool flag = FBSession.mode == k.id;
			Button button = FBKit.Tile(b, "modo_" + k.id, x, y, 186f, 100f, flag, delegate
			{
				FBSession.mode = k.id;
				FBSession.Save();
				MatchPicker();
			});
			Texture2D texture2D = Resources.Load<Texture2D>("Modes/" + k.id);
			if (!texture2D)
			{
				texture2D = Resources.Load<Texture2D>("MapShots/" + FBSession.map);
			}
			FBKit.Cover(button.transform, texture2D, 1f, 1f, 184f, 98f).color = (flag ? Color.white : new Color(0.62f, 0.62f, 0.62f));
			FBKit.Shade(button.transform, 1f, 30f, 184f, 69f);
			FBKit.T(button.transform, k.name, 15, 12f, 54f, 166f, 22f, TextAnchor.MiddleLeft, title: true, Color.white);
			FBKit.T(button.transform, k.tag, 10, 12f, 76f, 166f, 16f, TextAnchor.MiddleLeft, title: true, flag ? FBKit.Gold : FBKit.Dim);
		}
		FBKit.Section(b, "MAPA", 0f, 176f, 400f);
		for (int n = 0; n < maps.Count; n++)
		{
			MapDef j = maps[n];
			float x2 = (float)(n % 4) * 198f;
			float y2 = 204f + (float)(n / 4) * 136f;
			bool flag2 = FBSession.map == j.id;
			Button button2 = FBKit.Tile(b, "mapa_" + j.id, x2, y2, 186f, 124f, flag2, delegate
			{
				FBSession.map = j.id;
				FBSession.Save();
				MatchPicker();
			});
			FBKit.Cover(button2.transform, Resources.Load<Texture2D>("MapShots/" + j.id), 1f, 1f, 184f, 122f).color = (flag2 ? Color.white : new Color(0.62f, 0.62f, 0.62f));
			FBKit.Shade(button2.transform, 1f, 64f, 184f, 59f);
			FBKit.T(button2.transform, j.name.ToUpper(), 14, 12f, 96f, 166f, 22f, TextAnchor.MiddleLeft, title: true, Color.white);
		}
		(string, string, string, string) tuple = MODES.FirstOrDefault(((string id, string name, string tag, string desc) m) => m.id == FBSession.mode);
		if (tuple.Item1 == null)
		{
			tuple = MODES[0];
		}
		Transform p = FBKit.Box(b, "resumo", 812f, 18f, 428f, 236f).transform;
		FBKit.Cover(p, Resources.Load<Texture2D>("MapShots/" + FBSession.map), 1f, 1f, 426f, 120f);
		FBKit.Shade(p, 1f, 50f, 426f, 71f);
		FBKit.T(p, tuple.Item3, 11, 16f, 86f, 396f, 16f, TextAnchor.MiddleLeft, title: true, FBKit.Gold);
		FBKit.T(p, tuple.Item2, 24, 16f, 98f, 396f, 30f, TextAnchor.MiddleLeft, title: true, Color.white);
		FBKit.T(p, tuple.Item4, 13, 16f, 132f, 396f, 72f, TextAnchor.UpperLeft, title: false, FBKit.Dim);
		MapDef value;
		string text = (GameData.Maps.TryGetValue(FBSession.map, out value) ? value.name : FBSession.map);
		FBKit.T(p, text.ToUpper() + (MAP_INFO.TryGetValue(FBSession.map, out var value2) ? ("  ·  " + value2) : ""), 11, 16f, 206f, 396f, 20f, TextAnchor.MiddleLeft, title: true, FBKit.Txt);
		float y0 = 272f;
		(string time, string weather) wx = FBSession.WeatherFor(FBSession.map);
		Seg("DIFICULDADE DOS BOTS", DIFFS, FBSession.diff, delegate(int i)
		{
			FBSession.diff = i;
		});
		Seg("HORÁRIO", new string[3] { "DIA", "ANOITECER", "NOITE" }, (wx.time == "anoitecer") ? 1 : ((wx.time == "noite") ? 2 : 0), delegate(int i)
		{
			FBSession.SetWeather(FBSession.map, (new string[3] { "dia", "anoitecer", "noite" })[i], wx.weather);
		});
		Seg("CLIMA", new string[2] { "LIMPO", "CHUVA" }, (wx.weather == "chuva") ? 1 : 0, delegate(int i)
		{
			FBSession.SetWeather(FBSession.map, wx.time, (i == 1) ? "chuva" : "limpo");
		});
		if (FBSession.mode == "pvl")
		{
			Seg("SEU LADO", new string[2] { "FARINHA · POLÍCIA", "BURITI · LADRÃO" }, FBSession.team, delegate(int i)
			{
				FBSession.team = i;
			});
		}
		FBKit.Primary(b, "CONFIRMAR", 812f, Mathf.Max(y0 + 12f, 600f), 428f, 54f, delegate
		{
			if ((bool)preview)
			{
				preview.SetActive(value: true);
			}
			Home();
		});
		void Seg(string title, string[] opts, int cur, Action<int> set)
		{
			FBKit.Section(b, title, 812f, y0, 428f);
			float num = (428f - (float)((opts.Length - 1) * 8)) / (float)opts.Length;
			for (int num2 = 0; num2 < opts.Length; num2++)
			{
				int k2 = num2;
				FBKit.TextTile(b, opts[num2], 812f + (float)num2 * (num + 8f), y0 + 26f, num, 38f, num2 == cur, delegate
				{
					set(k2);
					FBSession.Save();
					MatchPicker();
				}, 13);
			}
			y0 += 80f;
		}
	}

	private static string Slot(string k)
	{
		if (!(k == "primary"))
		{
			if (!(k == "smg"))
			{
				return FBSession.secondary;
			}
			return FBSession.smg;
		}
		return FBSession.primary;
	}

	private static void SetSlot(string k, string id)
	{
		if (k == "primary")
		{
			FBSession.primary = id;
		}
		else if (k == "smg")
		{
			FBSession.smg = id;
		}
		else
		{
			FBSession.secondary = id;
		}
	}

	private static string SlotOf(WeaponDef d)
	{
		if (Mathf.Clamp(d.slot, 0, 2) != 0)
		{
			if (d.slot != 1)
			{
				return "secondary";
			}
			return "smg";
		}
		return "primary";
	}

	private static string ClsOf(WeaponDef d)
	{
		if (Array.IndexOf(CLASS_ORDER, d.cls) < 0)
		{
			return "fuzil";
		}
		return d.cls;
	}

	private static int AttSel(FBWeaponModel.Choice c, string kind)
	{
		return kind switch
		{
			"lasers" => c.laser, 
			"magazines" => c.magazine, 
			"muzzles" => c.muzzle, 
			"scopes" => c.scope, 
			_ => c.grip, 
		};
	}

	private static void AttSet(FBWeaponModel.Choice c, string kind, int i)
	{
		switch (kind)
		{
		case "scopes":
			c.scope = i;
			break;
		case "muzzles":
			c.muzzle = i;
			break;
		case "magazines":
			c.magazine = i;
			break;
		case "lasers":
			c.laser = i;
			break;
		default:
			c.grip = i;
			break;
		}
	}

	private void Arsenal()
	{
		Dictionary<string, WeaponDef> W = GameData.Weapons;
		List<WeaponDef> source = W.Values.Where((WeaponDef w) => (bool)FBRegistry.I && (bool)FBRegistry.I.Weapon(w.id)).ToList();
		if (arCls == null && W.TryGetValue(FBSession.primary, out var value))
		{
			arCls = ClsOf(value);
		}
		if (arCls == null)
		{
			arCls = "fuzil";
		}
		List<WeaponDef> list = (from w in source
			where ClsOf(w) == arCls
			orderby w.name
			select w).ToList();
		if (list.Count == 0)
		{
			arCls = "fuzil";
			list = (from w in source
				where ClsOf(w) == arCls
				orderby w.name
				select w).ToList();
		}
		if (!list.Any((WeaponDef w) => w.id == arSel))
		{
			WeaponDef d2 = list.FirstOrDefault();
			arSel = ((d2 != null && list.Any((WeaponDef w) => w.id == Slot(SlotOf(d2)))) ? Slot(SlotOf(d2)) : d2?.id);
		}
		if (arSel == null || !W.TryGetValue(arSel, out var d))
		{
			return;
		}
		Transform parent = Page("ARSENAL");
		if ((bool)preview)
		{
			preview.SetActive(value: false);
		}
		RectTransform rectTransform = FBUI.Rect(parent, "corpo", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
		rectTransform.offsetMin = new Vector2(26f, 18f);
		rectTransform.offsetMax = new Vector2(-26f, -74f);
		float num = 1006f;
		float num2 = 236f;
		float num3 = num2 + num + 16f;
		FBWeaponModel.Choice c = FBWeaponModel.Of(d);
		Dictionary<string, object> raw = Json.O(Json.O(GameData.WeaponsRaw, d.id), "att");
		string slotK = SlotOf(d);
		bool flag = Slot(slotK) == d.id;
		FBKit.Box(rectTransform, "fundoL", -10f, -8f, 240f, 684f, new Color(0.02f, 0.025f, 0.03f, 0.82f), border: false);
		FBKit.Box(rectTransform, "fundoR", num3 - 10f, -8f, 310f, 684f, new Color(0.02f, 0.025f, 0.03f, 0.82f), border: false);
		FBKit.Box(rectTransform, "fundoB", -10f, 680f, 1568f, 142f, new Color(0.02f, 0.025f, 0.03f, 0.82f), border: false);
		Hdr(rectTransform, "EQUIPAMENTO", 0f, 0f, 220f);
		for (int i = 0; i < 3; i++)
		{
			(string k, string n) tuple = LOADOUT[i];
			string item = tuple.k;
			string item2 = tuple.n;
			string id = Slot(item);
			bool flag2 = id == arSel;
			Button button = FBKit.Tile(rectTransform, "slot_" + item, 0f, 24 + i * 76, 220f, 68f, sel: false, delegate
			{
				if (W.TryGetValue(id ?? "", out var value2))
				{
					arCls = ClsOf(value2);
					arSel = id;
					arPage = -1;
					arOpen = null;
					Arsenal();
				}
			});
			button.GetComponent<Image>().color = (flag2 ? new Color(1f, 0.74f, 0.12f, 0.14f) : new Color(1f, 1f, 1f, 0.04f));
			FBKit.Box(button.transform, "borda", 0f, 0f, 3f, 68f, flag2 ? FBKit.Gold : new Color(0.33f, 0.33f, 0.33f), border: false);
			FBKit.T(button.transform, item2, 9, 12f, 8f, 120f, 14f, TextAnchor.MiddleLeft, title: true, FBKit.Dim);
			FBKit.T(button.transform, WName(id).ToUpper(), 14, 12f, 24f, 200f, 20f, TextAnchor.MiddleLeft, title: true, Color.white);
			Texture2D texture2D = FBIcons.Weapon(id, 200, 70);
			if ((bool)texture2D)
			{
				RawImage rawImage = FBKit.At(button.transform, "ic", 116f, 30f, 96f, 34f).gameObject.AddComponent<RawImage>();
				rawImage.texture = texture2D;
				rawImage.raycastTarget = false;
				rawImage.color = new Color(1f, 1f, 1f, 0.85f);
			}
		}
		Hdr(rectTransform, "CATEGORIAS", 0f, 262f, 220f);
		for (int j = 0; j < CLASS_ORDER.Length; j++)
		{
			string cl = CLASS_ORDER[j];
			bool flag3 = cl == arCls;
			int num4 = source.Count((WeaponDef w) => ClsOf(w) == cl);
			Button button2 = FBKit.Tile(rectTransform, "cat_" + cl, 0f, 286 + j * 40, 220f, 36f, sel: false, delegate
			{
				arCls = cl;
				arSel = null;
				arPage = 0;
				arOpen = null;
				Arsenal();
			});
			button2.GetComponent<Image>().color = (flag3 ? new Color(1f, 0.74f, 0.12f, 0.16f) : new Color(0f, 0f, 0f, 0f));
			button2.GetComponent<Outline>().enabled = false;
			if (flag3)
			{
				FBKit.Box(button2.transform, "borda", 0f, 0f, 2f, 36f, FBKit.Gold, border: false);
			}
			FBKit.T(button2.transform, CLASS_N[cl], 13, 12f, 0f, 160f, 36f, TextAnchor.MiddleLeft, title: true, flag3 ? Color.white : FBKit.Dim);
			FBKit.T(button2.transform, num4.ToString(), 11, 180f, 0f, 28f, 36f, TextAnchor.MiddleRight, title: true, FBKit.Dim);
		}
		List<(string, string)> list2 = PARTS.Where(((string kind, string l) x) => Json.L(raw, x.kind)?.Any((object o) => o != null) ?? false).ToList();
		bool flag4 = arOpen != null && list2.Any<(string, string)>(((string kind, string l) x) => x.kind == arOpen);
		float num5 = 602f;
		float num6 = 104f;
		float num7 = num5 - 16f - (flag4 ? (num6 + 8f) : 0f);
		Image image = FBKit.Box(rectTransform, "vitrine", num2, 0f, num, num7, new Color(0f, 0f, 0f, 0f));
		image.raycastTarget = true;
		FBKit.T(image.transform, CLASS_N[ClsOf(d)].ToUpper(), 11, 18f, 14f, 400f, 16f, TextAnchor.MiddleLeft, title: true, FBKit.Gold);
		FBKit.T(image.transform, d.name, 42, 18f, 30f, num - 36f, 50f, TextAnchor.MiddleLeft, title: true, Color.white);
		if (flag)
		{
			Image image2 = FBKit.Box(image.transform, "eq", 18f, 86f, 104f, 20f, new Color(0f, 0f, 0f, 0.5f));
			image2.GetComponent<Outline>().effectColor = new Color(0.21f, 0.76f, 0.35f);
			FBKit.T(image2.transform, "✓ EQUIPADA", 10, 0f, 0f, 104f, 20f, TextAnchor.MiddleCenter, title: true, new Color(0.48f, 0.89f, 0.6f));
		}
		FBKit.T(image.transform, "⟲ ARRASTE PARA GIRAR", 10, num - 200f, num7 - 26f, 186f, 16f, TextAnchor.MiddleRight, title: true, new Color(1f, 1f, 1f, 0.35f));
		EventTrigger eventTrigger = image.gameObject.AddComponent<EventTrigger>();
		EventTrigger.Entry entry = new EventTrigger.Entry
		{
			eventID = EventTriggerType.Drag
		};
		entry.callback.AddListener(delegate(BaseEventData e)
		{
			arYaw -= ((PointerEventData)e).delta.x * 0.5f;
			if ((bool)wpPreview)
			{
				wpPreview.transform.rotation = Quaternion.Euler(0f, arYaw, 0f);
			}
		});
		eventTrigger.triggers.Add(entry);
		if (flag4)
		{
			(string, string) tuple2 = list2.First<(string, string)>(((string kind, string l) x) => x.kind == arOpen);
			List<object> list3 = Json.L(raw, arOpen);
			List<int> list4 = new List<int>();
			if (arOpen == "scopes" || arOpen == "lasers" || arOpen == "grips")
			{
				list4.Add(-1);
			}
			for (int l = 0; l < list3.Count; l++)
			{
				if (list3[l] != null)
				{
					list4.Add(l);
				}
			}
			Image image3 = FBKit.Box(rectTransform, "bandeja", num2, num7 + 8f, num, num6, new Color(0.04f, 0.05f, 0.055f, 0.92f));
			image3.GetComponent<Outline>().effectColor = new Color(1f, 0.74f, 0.12f, 0.35f);
			FBKit.T(image3.transform, tuple2.Item2 + "   <color=#9aa3ab>escolha uma opção</color>", 10, 10f, 6f, 400f, 16f, TextAnchor.MiddleLeft, title: true, FBKit.Gold).supportRichText = true;
			int num8 = AttSel(c, arOpen);
			float num9 = Mathf.Min(150f, (num - 20f - 35f) / 8f);
			for (int m = 0; m < list4.Count && m < 16; m++)
			{
				int idx = list4[m];
				string s = ((idx < 0) ? ((arOpen == "scopes") ? "Mira de ferro" : "Nenhum") : AttLabel(arOpen, list3[idx] as Dictionary<string, object>, idx));
				string kind2 = arOpen;
				FBKit.TextTile(image3.transform, s, 10f + (float)(m % 8) * (num9 + 5f), 26 + m / 8 * 38, num9, 34f, idx == num8, delegate
				{
					AttSet(c, kind2, idx);
					FBWeaponModel.Set(d, c);
					Arsenal();
				}, 11);
			}
		}
		if (list2.Count == 0)
		{
			FBKit.T(rectTransform, "Esta arma não aceita acessórios", 12, num2 + 16f, num5, num, 70f, TextAnchor.MiddleLeft, title: false, FBKit.Dim);
		}
		float num10 = ((list2.Count > 0) ? ((num - (float)((list2.Count - 1) * 8)) / (float)list2.Count) : 0f);
		for (int n = 0; n < list2.Count; n++)
		{
			(string, string) tuple3 = list2[n];
			string kind = tuple3.Item1;
			string item3 = tuple3.Item2;
			List<object> list5 = Json.L(raw, kind);
			int num11 = AttSel(c, kind);
			bool flag5 = arOpen == kind;
			string s2 = ((num11 >= 0 && num11 < list5.Count && list5[num11] != null) ? AttLabel(kind, list5[num11] as Dictionary<string, object>, num11) : ((kind == "scopes") ? "Mira de ferro" : "Nenhum"));
			Button button3 = FBKit.Tile(rectTransform, "peca_" + kind, num2 + (float)n * (num10 + 8f), num5, num10, 70f, sel: false, delegate
			{
				arOpen = ((arOpen == kind) ? null : kind);
				Arsenal();
			});
			button3.GetComponent<Image>().color = (flag5 ? new Color(1f, 0.74f, 0.12f, 0.16f) : new Color(0.055f, 0.067f, 0.08f, 0.85f));
			FBKit.Box(button3.transform, "topo", 0f, 0f, num10, 2f, flag5 ? FBKit.Gold : new Color(1f, 1f, 1f, 0.12f), border: false);
			FBKit.T(button3.transform, item3, 9, 10f, 12f, num10 - 20f, 14f, TextAnchor.MiddleLeft, title: true, FBKit.Dim);
			FBKit.T(button3.transform, s2, 13, 10f, 30f, num10 - 20f, 22f, TextAnchor.MiddleLeft, title: true, Color.white);
		}
		Hdr(rectTransform, "ATRIBUTOS", num3, 0f, 290f);
		List<object> list6 = Json.L(raw, "magazines");
		int magazine = c.magazine;
		int num12 = ((list6 != null && magazine >= 0 && magazine < list6.Count && list6[magazine] != null) ? Json.I(list6[magazine] as Dictionary<string, object>, "ammo", d.mag) : d.mag);
		List<object> list7 = Json.L(raw, "muzzles");
		bool flag6 = list7 != null && c.muzzle >= 0 && c.muzzle < list7.Count && (Json.S(list7[c.muzzle] as Dictionary<string, object>, "node") ?? "").Contains("Silencer");
		float num13 = ((d.spread > 0f) ? d.spread : 0.25f);
		float item4 = d.Damage(0f, head: false) * (float)Mathf.Max(1, d.pellets) * (flag6 ? 0.92f : 1f);
		FBWeaponModel.Mods mods = FBWeaponModel.ModsOf(d, c);
		FBWeaponModel.Mods mods2 = FBWeaponModel.ModsOf(d, c, braced: true);
		(string, float, float, string)[] array = new(string, float, float, string)[9]
		{
			("DANO", item4, 150f, item4.ToString("0")),
			("CADÊNCIA", d.rpm, 1000f, d.rpm.ToString("0") + " rpm"),
			("ALCANCE", 100f / num13, 900f, (100f / num13).ToString("0") + " m"),
			("PRECISÃO", PrecOf(num13, mods.spreadHip), 110f, PrecOf(num13, mods.spreadHip).ToString("0")),
			("PRECISÃO MIRANDO", PrecOf(num13 * 0.45f, mods.spreadAds), 110f, PrecOf(num13 * 0.45f, mods.spreadAds).ToString("0") + (mods.laser ? " · laser" : "")),
			("CONTROLE DE RECUO", 100f / (mods.recV * 0.7f + mods.recH * 0.3f), 200f, (100f / (mods.recV * 0.7f + mods.recH * 0.3f)).ToString("0") + (mods.bipod ? (" · " + (100f / (mods2.recV * 0.7f + mods2.recH * 0.3f)).ToString("0") + " apoiado") : "")),
			("MOBILIDADE", ((d.move > 0f) ? d.move : 1f) * 100f * mods.move - (float)((num12 > 60) ? 6 : 0), 120f, (((d.move > 0f) ? d.move : 1f) * 100f * mods.move - (float)((num12 > 60) ? 6 : 0)).ToString("0") + "%"),
			("MUNIÇÃO", num12, 200f, num12.ToString()),
			("SILÊNCIO", flag6 ? 90 : 20, 100f, flag6 ? "silenciada" : "alta")
		};
		Transform p = FBKit.Box(rectTransform, "atributos", num3, 24f, 290f, 22 + array.Length * 34, new Color(0.04f, 0.05f, 0.055f, 0.6f)).transform;
		for (int num14 = 0; num14 < array.Length; num14++)
		{
			(string, float, float, string) tuple4 = array[num14];
			float num15 = 12 + num14 * 34;
			FBKit.T(p, tuple4.Item1, 10, 12f, num15, 140f, 14f, TextAnchor.MiddleLeft, title: true, FBKit.Dim);
			FBKit.T(p, tuple4.Item4, 12, 158f, num15, 120f, 14f, TextAnchor.MiddleRight, title: true, FBKit.Txt);
			FBKit.Box(FBKit.Box(p, "barra", 12f, num15 + 18f, 266f, 5f, new Color(1f, 1f, 1f, 0.08f), border: false).transform, "f", 0f, 0f, 266f * Mathf.Clamp(tuple4.Item2 / tuple4.Item3, 0.03f, 1f), 5f, new Color(0.93f, 0.91f, 0.86f), border: false);
		}
		float num16 = 46 + array.Length * 34 + 10;
		float num17 = 142f;
		(string, string)[] array2 = new(string, string)[4]
		{
			("TIPO", CLASS_N[ClsOf(d)]),
			("PENTE", num12 + " balas"),
			("DISPARO", (d.pellets > 1) ? $"Chumbo ({d.pellets})" : (d.auto ? "Automático" : (d.bolt ? "Ferrolho" : "Semi"))),
			("SLOT", LOADOUT.First(((string k, string n) x) => x.k == slotK).n)
		};
		for (int num18 = 0; num18 < 4; num18++)
		{
			Transform p2 = FBKit.Box(rectTransform, "info", num3 + (float)(num18 % 2) * (num17 + 6f), num16 + (float)(num18 / 2 * 50), num17, 44f, new Color(1f, 1f, 1f, 0.035f)).transform;
			FBKit.T(p2, array2[num18].Item1, 9, 9f, 6f, num17 - 18f, 12f, TextAnchor.MiddleLeft, title: true, FBKit.Dim);
			FBKit.T(p2, array2[num18].Item2, 13, 9f, 20f, num17 - 18f, 18f, TextAnchor.MiddleLeft, title: true, Color.white);
		}
		float y = 608f;
		if (flag)
		{
			Image image4 = FBKit.Box(rectTransform, "equipada", num3, y, 290f, 64f, new Color(0.21f, 0.76f, 0.35f, 0.16f));
			image4.GetComponent<Outline>().effectColor = new Color(0.21f, 0.76f, 0.35f, 0.5f);
			FBKit.T(image4.transform, "✓ EQUIPADA", 20, 0f, 10f, 290f, 28f, TextAnchor.MiddleCenter, title: true, new Color(0.48f, 0.89f, 0.6f));
			FBKit.T(image4.transform, "SLOT " + LOADOUT.First(((string k, string n) x) => x.k == slotK).n, 10, 0f, 40f, 290f, 14f, TextAnchor.MiddleCenter, title: true, new Color(0.48f, 0.89f, 0.6f, 0.7f));
		}
		else
		{
			Button button4 = FBKit.Primary(rectTransform, "EQUIPAR", num3, y, 290f, 64f, delegate
			{
				SetSlot(slotK, d.id);
				FBSession.Save();
				Arsenal();
			});
			Text componentInChildren = button4.GetComponentInChildren<Text>();
			componentInChildren.fontSize = 22;
			componentInChildren.rectTransform.anchoredPosition += new Vector2(0f, 7f);
			FBKit.T(button4.transform, "NO SLOT " + LOADOUT.First(((string k, string n) x) => x.k == slotK).n, 10, 0f, 42f, 290f, 14f, TextAnchor.MiddleCenter, title: true, new Color(0.16f, 0.1f, 0f, 0.7f));
		}
		float y2 = 688f;
		float num19 = 1464f;
		int num20 = Mathf.Max(1, Mathf.FloorToInt((num19 + 10f) / 166f));
		int num21 = Mathf.CeilToInt((float)list.Count / (float)num20);
		if (arPage < 0)
		{
			arPage = Mathf.Max(0, list.FindIndex((WeaponDef w) => w.id == arSel)) / num20;
		}
		arPage = Mathf.Clamp(arPage, 0, Mathf.Max(0, num21 - 1));
		if (num21 > 1)
		{
			FBKit.TextTile(rectTransform, "‹", 0f, y2, 34f, 120f, sel: false, delegate
			{
				arPage--;
				Arsenal();
			}, 22);
			FBKit.TextTile(rectTransform, "›", 1514f, y2, 34f, 120f, sel: false, delegate
			{
				arPage++;
				Arsenal();
			}, 22);
		}
		for (int num22 = 0; num22 < num20 && arPage * num20 + num22 < list.Count; num22++)
		{
			WeaponDef w2 = list[arPage * num20 + num22];
			bool flag7 = w2.id == arSel;
			bool num23 = FBSession.primary == w2.id || FBSession.smg == w2.id || FBSession.secondary == w2.id;
			Button button5 = FBKit.Tile(rectTransform, "card_" + w2.id, 42f + (float)num22 * 166f, y2, 156f, 120f, sel: false, delegate
			{
				arSel = w2.id;
				arOpen = null;
				Arsenal();
			});
			button5.GetComponent<Image>().color = (flag7 ? new Color(0.16f, 0.14f, 0.07f, 0.95f) : new Color(0.08f, 0.095f, 0.11f, 0.95f));
			if (flag7)
			{
				button5.GetComponent<Outline>().effectColor = new Color(1f, 0.74f, 0.12f, 0.6f);
			}
			FBKit.Box(button5.transform, "base", 0f, 117f, 156f, 3f, flag7 ? FBKit.Gold : new Color(0.2f, 0.2f, 0.2f), border: false);
			Texture2D texture2D2 = FBIcons.Weapon(w2.id, 280, 110);
			if ((bool)texture2D2)
			{
				RawImage rawImage2 = FBKit.At(button5.transform, "ic", 10f, 20f, 136f, 58f).gameObject.AddComponent<RawImage>();
				rawImage2.texture = texture2D2;
				rawImage2.raycastTarget = false;
			}
			FBKit.T(button5.transform, w2.name, 13, 8f, 90f, 140f, 20f, TextAnchor.MiddleLeft, title: true, Color.white);
			if (num23)
			{
				FBKit.T(FBKit.Box(button5.transform, "eq", 92f, 6f, 58f, 14f, new Color(0.21f, 0.76f, 0.35f), border: false).transform, "EQUIPADA", 8, 0f, 0f, 58f, 14f, TextAnchor.MiddleCenter, title: true, new Color(0f, 0.27f, 0.07f));
			}
		}
		if (num21 > 1)
		{
			for (int num24 = 0; num24 < num21; num24++)
			{
				FBKit.Box(rectTransform, "pg", 774f - (float)num21 * 10.5f + (float)(num24 * 21), 814f, 16f, 3f, (num24 == arPage) ? FBKit.Gold : new Color(1f, 1f, 1f, 0.18f), border: false);
			}
		}
		StartCoroutine(PlaceWeapon(d, image.rectTransform));
		static float PrecOf(float sp, float k)
		{
			return Mathf.Clamp(100f - sp * 60f * k, 5f, 110f);
		}
	}

	private static void Hdr(Transform p, string s, float x, float y, float w)
	{
		FBKit.T(p, s, 11, x, y, w, 16f, TextAnchor.MiddleLeft, title: true, FBKit.Dim);
		FBKit.Box(p, "fio", x + (float)(s.Length * 9) + 10f, y + 8f, Mathf.Max(0f, w - (float)(s.Length * 9) - 10f), 1f, FBKit.Line, border: false);
	}

	private IEnumerator PlaceWeapon(WeaponDef d, RectTransform view)
	{
		yield return null;
		if (!view || !cam)
		{
			yield break;
		}
		if ((bool)wpPreview)
		{
			UnityEngine.Object.Destroy(wpPreview);
		}
		wpPreview = FBWeaponModel.Make(d);
		if (!wpPreview)
		{
			yield break;
		}
		Vector3[] array = new Vector3[4];
		view.GetWorldCorners(array);
		Vector3 pos = (array[0] + array[2]) / 2f + new Vector3(0f, (0f - (array[2].y - array[0].y)) * 0.06f, 0f);
		float num = array[2].x - array[0].x;
		Vector3 point = cam.ScreenPointToRay(pos).GetPoint(3.2f);
		wpPreview.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, arYaw, 0f));
		wpPreview.transform.localScale = Vector3.one;
		Renderer[] array2 = (from r in wpPreview.GetComponentsInChildren<Renderer>()
			where r.enabled
			select r).ToArray();
		if (array2.Length != 0)
		{
			Bounds bounds = array2[0].bounds;
			Renderer[] array3 = array2;
			foreach (Renderer renderer in array3)
			{
				bounds.Encapsulate(renderer.bounds);
			}
			float num2 = 6.4f * Mathf.Tan(cam.fieldOfView * 0.5f * ((float)Math.PI / 180f)) * cam.aspect * num / (float)Screen.width * 0.7f;
			float num3 = Mathf.Max(bounds.size.x, bounds.size.z, 0.01f);
			wpPreview.transform.localScale = Vector3.one * (num2 / num3);
			wpPreview.transform.position += point - Center(wpPreview);
			if (!wpPreview.GetComponent<FBSpin>())
			{
				wpPreview.AddComponent<FBSpin>().speed = 12f;
			}
		}
	}

	private static Vector3 Center(GameObject g)
	{
		Renderer[] array = (from r in g.GetComponentsInChildren<Renderer>()
			where r.enabled
			select r).ToArray();
		Bounds bounds = array[0].bounds;
		Renderer[] array2 = array;
		foreach (Renderer renderer in array2)
		{
			bounds.Encapsulate(renderer.bounds);
		}
		return bounds.center;
	}

	private static string AttLabel(string kind, Dictionary<string, object> a, int i)
	{
		if (a == null)
		{
			return "—";
		}
		if (kind == "scopes")
		{
			float num = Json.F(a, "fieldOfViewMultiplierAim", 0.9f);
			if (!(num >= 0.745f))
			{
				if (!(num >= 0.645f))
				{
					if (!(num >= 0.595f))
					{
						if (!(num >= 0.495f))
						{
							return $"Luneta 6x ({i + 1})";
						}
						return $"Luneta 4x ({i + 1})";
					}
					return $"ACOG 2x ({i + 1})";
				}
				return $"Holográfica {i + 1}";
			}
			return $"Red dot {i + 1}";
		}
		string text = Json.S(a, "node") ?? "";
		switch (kind)
		{
		case "muzzles":
			if (!text.Contains("Silencer"))
			{
				return "Cano padrão";
			}
			return "Silenciador";
		case "magazines":
			return string.Format("{0} balas", Json.I(a, "ammo"));
		case "lasers":
			if (!text.Contains("Flash"))
			{
				return "Laser";
			}
			return "Lanterna";
		default:
			if (!text.Contains("Bipod"))
			{
				return "Empunhadura";
			}
			return "Bipé";
		}
	}

	public void Characters()
	{
		Clear();
		if ((bool)preview)
		{
			UnityEngine.Object.Destroy(preview);
			preview = null;
		}
		Customize = FBCustomize.Open(cam, delegate
		{
			Customize = null;
			Home();
		});
	}

	private IEnumerator LoadShots()
	{
		yield return new WaitForSeconds(1.5f);
		yield return FBLoading.Shots(Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste"), FBSession.map, FBSession.mode);
		if (Array.IndexOf(Environment.GetCommandLineArgs(), "-fbauto") < 0)
		{
			Application.Quit(0);
		}
	}

	private IEnumerator MenuShots()
	{
		string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste");
		Directory.CreateDirectory(dir);
		yield return new WaitForSeconds(2.5f);
		(string, Action)[] array = new(string, Action)[10]
		{
			("inicio", Home),
			("partida", MatchPicker),
			("armas", Arsenal),
			("armas_mira", delegate
			{
				arOpen = "scopes";
				Arsenal();
			}),
			("armas_pistola", delegate
			{
				arOpen = null;
				arCls = "pistola";
				arSel = null;
				Arsenal();
			}),
			("emotes", Emotes),
			("passe", Pass),
			("controles", Controls),
			("ajustes", delegate
			{
				Home();
				FBSettings.Open(root);
			}),
			("online", Online)
		};
		(string n, Action a)[] array2 = array;
		for (int j = 0; j < array2.Length; j++)
		{
			(string, Action) tuple = array2[j];
			var (i, _) = tuple;
			tuple.Item2();
			yield return new WaitForSeconds(1.5f);
			ScreenCapture.CaptureScreenshot(Path.Combine(dir, "menu_" + i + ".png"));
			yield return new WaitForSeconds(0.6f);
		}
		Home();
		if (Array.IndexOf(Environment.GetCommandLineArgs(), "-fbauto") < 0)
		{
			Application.Quit(0);
		}
	}

	private IEnumerator CharShots()
	{
		yield return new WaitForSeconds(2f);
		Characters();
		yield return null;
		yield return Customize.CharShots(Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste"));
		if (Array.IndexOf(Environment.GetCommandLineArgs(), "-fbauto") < 0)
		{
			Application.Quit(0);
		}
	}

	private IEnumerator LobbyShots()
	{
		string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste", "lobby");
		Directory.CreateDirectory(dir);
		string keep = FBSession.skin;
		yield return new WaitForSeconds(2f);
		Home();
		yield return new WaitForSeconds(1f);
		NameDialog();
		yield return new WaitForSeconds(0.6f);
		yield return Shot("nome_usuario");
		if ((bool)nameDlg)
		{
			UnityEngine.Object.Destroy(nameDlg);
		}
		string why;
		string text = FBSession.CleanName("  zé_da#vila!!  ", out why);
		string why2;
		string text2 = FBSession.CleanName("ab", out why2);
		Debug.Log("[FreeBala][lobby] nome: 'zé_da#vila!!' -> '" + text + "' (" + why + ") · 'ab' -> " + (text2 ?? "recusado") + " (" + why2 + ")");
		string[] array = new string[8] { "r_tutu", "r_elaine", "r_giga", "r_vivi", "r_miguel", "r_pm_silva", "r_aka_junin", "r_faria" };
		foreach (string id in array)
		{
			if (FBRegistry.I.HasChar(id))
			{
				FBSession.skin = id;
				Home();
				FBCharacter ch = (preview ? preview.GetComponent<FBCharacter>() : null);
				if ((bool)preview)
				{
					preview.GetComponent<FBLobbyDirector>();
				}
				for (int i = 0; i < 3; i++)
				{
					yield return new WaitForSeconds((i == 0) ? 2.5f : 5.5f);
					yield return Shot($"{id}_inicio_{i}");
					Debug.Log(string.Format("[FreeBala][lobby] {0} início {1}: clipe {2}", id, i, ((bool)ch && ch.dance != null) ? ch.dance.clip : (ch ? ch.OverrideOf("Rifle Idle") : "-")));
				}
				Characters();
				yield return null;
				Customize.Select(id);
				yield return new WaitForSeconds(1.6f);
				yield return Shot(id + "_selecao");
				Customize.stage.PlayConfirm();
				yield return new WaitForSeconds(1.4f);
				yield return Shot(id + "_confirmar");
				FBCharacter ch2 = Customize.stage.ch;
				Debug.Log("[FreeBala][lobby] " + id + " (" + (((bool)ch2 && ch2.IsFemale) ? "f" : "m") + ") confirmar: " + (((bool)ch2 && ch2.dance != null) ? ch2.dance.clip : "-"));
			}
		}
		FBSession.skin = keep;
		Debug.Log("[FreeBala][lobby] fim");
		yield return new WaitForSeconds(0.5f);
		Application.Quit(0);
		IEnumerator Shot(string n)
		{
			yield return new WaitForEndOfFrame();
			ScreenCapture.CaptureScreenshot(Path.Combine(dir, n + ".png"));
			yield return null;
			yield return null;
		}
	}

	private IEnumerator CustomShots()
	{
		yield return new WaitForSeconds(2f);
		Characters();
		yield return null;
		string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste");
		yield return Customize.Shots(dir, resolutions: true);
		if (Array.IndexOf(Environment.GetCommandLineArgs(), "-fbauto") < 0)
		{
			Application.Quit(0);
		}
	}

	private void Emotes()
	{
		Transform obj = Page("EMOTES");
		List<string> list = FBEmotes.Equipped();
		RectTransform p = FBKit.Body(obj);
		FBKit.Box(p, "fundoL", 0f, 16f, 380f, 740f, new Color(0f, 0f, 0f, 0.55f));
		FBKit.Section(p, "CATÁLOGO · CLIQUE PARA EQUIPAR", 14f, 30f, 360f);
		RectTransform list2 = FBKit.Scroll(p, 8f, 58f, 364f, 690f);
		foreach (FBEmotes.Def item in from x in FBEmotes.All
			where FBRegistry.I.Clip(x.id)
			orderby x.cat, x.name
			select x)
		{
			FBEmotes.Def ee = item;
			bool flag = list.Contains(item.id);
			Button button = FBKit.Row(list2, "          " + item.name.ToUpper(), flag ? "NA RODA ✓" : item.cat.ToUpper(), flag, delegate
			{
				List<string> list4 = FBEmotes.Equipped();
				if (list4.Contains(ee.id))
				{
					list4.Remove(ee.id);
				}
				else
				{
					if (list4.Count >= 8)
					{
						list4.RemoveAt(0);
					}
					list4.Add(ee.id);
				}
				FBSession.emoteSlots = string.Join("|", list4);
				FBSession.Save();
				Preview(withWeapon: false, ee.id);
				Emotes();
			}, 48f);
			Texture2D texture2D = FBIcons.Emote(item.id, FBSession.skin, 96);
			if ((bool)texture2D)
			{
				RawImage rawImage = FBUI.Rect(button.transform, "ic", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(42f, 42f)).gameObject.AddComponent<RawImage>();
				rawImage.texture = texture2D;
				rawImage.raycastTarget = false;
			}
		}
		Transform p2 = FBKit.Box(p, "roda", 900f, 16f, 340f, 470f, new Color(0f, 0f, 0f, 0.55f)).transform;
		FBKit.Section(p2, $"NA RODA ({list.Count}/8)", 14f, 14f, 300f);
		for (int i = 0; i < 8; i++)
		{
			float x2 = 14 + i % 2 * 160;
			float y = 44 + i / 2 * 104;
			string text = ((i < list.Count) ? list[i] : null);
			Transform p3 = FBKit.Box(p2, "slot", x2, y, 152f, 96f, FBKit.Card).transform;
			if (text == null)
			{
				FBKit.T(p3, "VAZIO", 11, 0f, 0f, 152f, 96f, TextAnchor.MiddleCenter, title: true, FBKit.Dim);
				continue;
			}
			Texture2D texture2D2 = FBIcons.Emote(text, FBSession.skin, 96);
			if ((bool)texture2D2)
			{
				RawImage rawImage2 = FBKit.At(p3, "ic", 46f, 6f, 60f, 60f).gameObject.AddComponent<RawImage>();
				rawImage2.texture = texture2D2;
				rawImage2.raycastTarget = false;
			}
			FBKit.T(p3, FBEmotes.NameOf(text).ToUpper(), 10, 6f, 70f, 140f, 20f, TextAnchor.MiddleCenter, title: true, FBKit.Txt);
		}
		FBKit.Section(p, "DANÇA DA VITÓRIA", 900f, 504f, 340f);
		FBKit.TextTile(p, (FBSession.victory != null) ? (FBEmotes.NameOf(FBSession.victory).ToUpper() + "  ›") : "ALEATÓRIA  ›", 900f, 530f, 340f, 44f, sel: true, delegate
		{
			List<string> list3 = FBEmotes.Equipped();
			int num = list3.IndexOf(FBSession.victory ?? "");
			FBSession.victory = ((num + 1 < list3.Count) ? list3[num + 1] : null);
			FBSession.Save();
			Emotes();
		}, 14);
		FBKit.T(p, "Na partida: segure B para abrir a roda de emotes.", 12, 900f, 590f, 340f, 40f, TextAnchor.UpperLeft, title: false, FBKit.Dim);
	}

	private void Pass()
	{
		RectTransform p = FBKit.Body(Page("PASSE DE BATALHA", dimStage: true));
		int tier = FBPass.Tier;
		FBPass.Claimed = Mathf.Min(30, tier + 1);
		int num = FBSession.passXp % 1000;
		FBKit.T(p, "TEMPORADA 1", 12, 0f, 22f, 400f, 18f, TextAnchor.MiddleLeft, title: true, FBKit.Gold);
		FBKit.T(p, $"NÍVEL {tier}", 34, 0f, 40f, 400f, 44f, TextAnchor.MiddleLeft, title: true, Color.white);
		FBKit.Box(FBKit.Box(p, "barra", 0f, 96f, 1240f, 8f, new Color(1f, 1f, 1f, 0.1f), border: false).transform, "f", 0f, 0f, 1240f * (float)num / 1000f, 8f, FBKit.Gold, border: false);
		FBKit.T(p, $"{num} / {1000} XP PARA O PRÓXIMO NÍVEL", 12, 740f, 70f, 500f, 18f, TextAnchor.MiddleRight, title: true, FBKit.Dim);
		for (int i = 0; i < 30; i++)
		{
			bool flag = i < tier;
			bool flag2 = i == tier;
			float x = (float)(i % 10) * 124f;
			float num2 = 136f + (float)(i / 10) * 146f;
			Transform transform = FBKit.Box(p, "nivel", x, num2, 112f, 112f, flag ? new Color(1f, 0.74f, 0.12f, 0.2f) : (flag2 ? new Color(1f, 1f, 1f, 0.09f) : FBKit.Card)).transform;
			if (flag || flag2)
			{
				transform.GetComponent<Outline>().effectColor = (flag ? new Color(1f, 0.74f, 0.12f, 0.8f) : new Color(1f, 1f, 1f, 0.4f));
			}
			FBKit.T(transform, (i + 1).ToString(), 30, 0f, 18f, 112f, 50f, TextAnchor.MiddleCenter, title: true, flag ? FBKit.Gold : FBKit.Txt);
			FBKit.T(transform, flag ? "LIBERADO" : (flag2 ? "PRÓXIMO" : "BLOQUEADO"), 10, 0f, 76f, 112f, 18f, TextAnchor.MiddleCenter, title: true, flag ? FBKit.Gold : FBKit.Dim);
			FBKit.T(p, (i % 5 == 4) ? "★ PRÊMIO" : ("$ " + (100 + i * 20)), 11, x, num2 + 112f + 6f, 112f, 16f, TextAnchor.MiddleCenter, title: true, FBKit.Dim);
		}
		FBKit.T(p, "Abate +100 XP  ·  Tiro na cabeça +150  ·  Partida +200  ·  Vitória +500", 13, 0f, 590f, 1240f, 20f, TextAnchor.MiddleCenter, title: false, FBKit.Dim);
	}

	private void Controls()
	{
		RectTransform p = FBKit.Body(Page("CONTROLES", dimStage: true));
		(string, string[])[] array = new(string, string[])[3]
		{
			("TECLADO E MOUSE", new string[21]
			{
				"W A S D|mover", "SHIFT|corrida automática", "ESPAÇO|pular / pular janela", "C / Z|agachar / deitar", "Q / E|inclinar", "R|recarregar", "H|kit médico (C alterna em pé/agachado)", "X|pegar do chão", "F|veículo · segurar: reanimar", "1–4 · RODA|trocar arma",
				"I|mochila", "B|emotes (segure) / loja", "T|inspecionar", "V|distância da câmera (no veículo)", "ALT|olhar livre", "Y|marcar", "G|GPS / killstreak", "K|central multimídia", "TAB|placar", "ENTER|chat",
				"ESC|pausa"
			}),
			("CONTROLE", new string[16]
			{
				"ANALÓGICOS|mover / olhar", "RT|atirar", "LT|mirar", "A|pular", "B|agachar", "X|recarregar", "Y|trocar arma", "LB / RB|inclinar", "L3|correr", "R3|câmera",
				"D-PAD ↑|veículo / reanimar", "D-PAD ↓|pegar", "D-PAD ←|kit médico", "D-PAD →|loja / emotes", "START|pausa", "SELECT|placar"
			}),
			("TOQUE", new string[9] { "ESQUERDA|joystick (borda = correr)", "DIREITA|arrastar olha", "ATIRAR|arraste para mirar junto", "MIRA · PULO|botões à direita", "AGACHAR · DEITAR|postura", "RECARREGAR|", "TROCAR · CURAR|", "PEGAR · AÇÃO|", "LOJA · CÂMERA|" })
		};
		for (int i = 0; i < array.Length; i++)
		{
			float x = (float)i * 420f;
			Transform p2 = FBKit.Box(p, "col", x, 18f, 400f, 700f, new Color(0f, 0f, 0f, 0.45f)).transform;
			FBKit.Section(p2, array[i].Item1, 16f, 16f, 400f);
			for (int j = 0; j < array[i].Item2.Length; j++)
			{
				string[] array2 = array[i].Item2[j].Split('|');
				float y = 50 + j * 30;
				FBKit.T(FBKit.Box(p2, "k", 16f, y, 150f, 24f, new Color(1f, 1f, 1f, 0.08f)).transform, array2[0], 11, 0f, 0f, 150f, 24f, TextAnchor.MiddleCenter, title: true, FBKit.Txt);
				FBKit.T(p2, array2[1], 13, 180f, y, 204f, 24f, TextAnchor.MiddleLeft, title: false, FBKit.Dim);
			}
		}
	}

	private void NameDialog()
	{
		if ((bool)nameDlg)
		{
			UnityEngine.Object.Destroy(nameDlg);
		}
		Image image = FBUI.Panel(root, "nome_usuario", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.72f));
		nameDlg = image.gameObject;
		image.raycastTarget = true;
		Transform p = FBUI.Panel(image.transform, "caixa", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 300f), new Color(0.06f, 0.065f, 0.075f, 0.97f)).transform;
		FBKit.T(p, "SEU NOME DE JOGADOR", 22, 28f, 22f, 464f, 30f, TextAnchor.MiddleLeft, title: true, Color.white);
		FBKit.T(p, "Aparece no placar, no feed de abates e no online — não precisa ser o nome do personagem.", 13, 28f, 56f, 464f, 36f, TextAnchor.UpperLeft, title: false, FBKit.Dim);
		Image image2 = FBKit.Box(p, "campo", 28f, 100f, 464f, 48f, new Color(1f, 1f, 1f, 0.07f));
		image2.raycastTarget = true;
		Text textComponent = FBKit.T(image2.transform, "", 20, 14f, 0f, 436f, 48f, TextAnchor.MiddleLeft, title: true, Color.white);
		InputField f = image2.gameObject.AddComponent<InputField>();
		f.textComponent = textComponent;
		f.characterLimit = 16;
		f.text = (FBSession.nameChosen ? FBSession.name : "");
		Text msg = FBKit.T(p, "", 12, 28f, 152f, 464f, 22f, TextAnchor.MiddleLeft, title: false, new Color(1f, 0.55f, 0.4f));
		FBKit.Primary(p, "SALVAR", 28f, 196f, 314f, 52f, Save);
		FBKit.TextTile(p, "CANCELAR", 352f, 196f, 140f, 52f, sel: false, delegate
		{
			UnityEngine.Object.Destroy(nameDlg);
		}, 14);
		f.onEndEdit.AddListener(delegate
		{
			Keyboard current = Keyboard.current;
			if (current != null && (current.enterKey.wasPressedThisFrame || current.numpadEnterKey.wasPressedThisFrame))
			{
				Save();
			}
		});
		f.ActivateInputField();
		void Save()
		{
			string why;
			string text = FBSession.CleanName(f.text, out why);
			if (text == null)
			{
				msg.text = why;
			}
			else
			{
				FBSession.SetName(text.ToUpper());
				if ((bool)nameT)
				{
					nameT.text = FBSession.name;
				}
				UnityEngine.Object.Destroy(nameDlg);
			}
		}
	}

	private void Online()
	{
		Transform obj = Page("JOGAR ONLINE", dimStage: true);
		if ((bool)preview)
		{
			preview.SetActive(value: false);
		}
		RectTransform p = FBKit.Body(obj);
		float x = 340f;
		Transform box = FBKit.Box(p, "online", x, 90f, 560f, 420f, new Color(0f, 0f, 0f, 0.55f)).transform;
		FBKit.T(box, "SALA ONLINE", 22, 28f, 24f, 504f, 30f, TextAnchor.MiddleLeft, title: true, Color.white);
		FBKit.T(box, "Mesmo servidor do jogo web: navegador e app entram na mesma sala.", 13, 28f, 58f, 504f, 36f, TextAnchor.UpperLeft, title: false, FBKit.Dim);
		InputField nameF = F("SEU NOME", FBSession.name, 112f);
		InputField serverF = F("SERVIDOR (ws://IP:8123/ws)", FBSession.server, 200f);
		FBKit.Primary(box, "ENTRAR NA SALA DO MAPA", 28f, 320f, 504f, 56f, delegate
		{
			string why;
			string text = FBSession.CleanName(nameF.text, out why);
			if (text != null)
			{
				FBSession.SetName(text.ToUpper());
			}
			FBSession.server = serverF.text.Trim();
			FBSession.online = true;
			FBSession.Save();
			Go();
		});
		InputField F(string label, string val, float y)
		{
			FBKit.Section(box, label, 28f, y, 560f);
			Image image = FBKit.Box(box, "campo", 28f, y + 26f, 504f, 44f, new Color(1f, 1f, 1f, 0.06f));
			image.raycastTarget = true;
			Text textComponent = FBKit.T(image.transform, val, 17, 14f, 0f, 476f, 44f, TextAnchor.MiddleLeft, title: false, Color.white);
			InputField inputField = image.gameObject.AddComponent<InputField>();
			inputField.textComponent = textComponent;
			inputField.text = val;
			return inputField;
		}
	}

	private void Play()
	{
		FBSession.online = false;
		FBSession.Save();
		Go();
	}

	private void Go()
	{
		string text = "Map_" + ((FBSession.mode == "treino" && Application.CanStreamedLevelBeLoaded("Map_estande") && FBSession.map == "estande") ? "estande" : FBSession.map);
		if (!Application.CanStreamedLevelBeLoaded(text))
		{
			text = "Map_" + maps[0].id;
			FBSession.map = maps[0].id;
		}
		if (!FBLoading.Busy)
		{
			Clear();
			if ((bool)preview)
			{
				preview.SetActive(value: false);
			}
			FBLoading.Load(text, FBSession.map, FBSession.mode);
		}
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBMenu.Update");

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
		if (!Customize && (bool)preview && preview.activeSelf)
		{
			preview.transform.rotation = Quaternion.Euler(0f, 180f + Mathf.Sin(Time.time * 0.4f) * 8f, 0f);
		}
	}
}
}
