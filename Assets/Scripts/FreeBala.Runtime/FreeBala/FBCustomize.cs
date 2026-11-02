using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FreeBala
{
public class FBCustomize : MonoBehaviour
{
	public static readonly (string key, string label, string[] cats, FBCustomizeStage.Frame frame)[] GROUPS = new(string, string, string[], FBCustomizeStage.Frame)[7]
	{
		("cabeca", "CABEÇA", new string[2] { "hat", "face" }, FBCustomizeStage.Frame.Face),
		("cabelo", "CABELO", new string[1] { "head" }, FBCustomizeStage.Frame.Bust),
		("tronco", "TRONCO", new string[2] { "top", "vest" }, FBCustomizeStage.Frame.Torso),
		("pernas", "PERNAS", new string[1] { "bottom" }, FBCustomizeStage.Frame.Legs),
		("pes", "PÉS", new string[1] { "shoes" }, FBCustomizeStage.Frame.Feet),
		("acess", "ACESSÓRIOS", new string[2] { "acc", "hands" }, FBCustomizeStage.Frame.Torso),
		("pele", "PELE", new string[0], FBCustomizeStage.Frame.Bust)
	};

	private static readonly Dictionary<string, string> CAT_NAMES = new Dictionary<string, string>
	{
		["hat"] = "Chapéu/Capacete",
		["face"] = "Rosto",
		["head"] = "Cabeça/Cabelo",
		["top"] = "Camisa",
		["vest"] = "Colete",
		["bottom"] = "Calça/Bermuda",
		["shoes"] = "Calçado",
		["acc"] = "Acessórios",
		["hands"] = "Luvas"
	};

	private static readonly Color Txt = new Color(0.925f, 0.91f, 0.863f);

	private static readonly Color Dim = new Color(0.925f, 0.91f, 0.863f, 0.55f);

	private static readonly Color Gold2 = new Color(1f, 0.77f, 0f);

	private static readonly Color Orange = new Color(0.82f, 0.06f, 0f);

	private static readonly Color PanelC = new Color(0.04f, 0.047f, 0.055f, 0.86f);

	private static readonly Color Card = new Color(0.09f, 0.1f, 0.115f, 0.92f);

	private static readonly Color CardHi = new Color(0.16f, 0.14f, 0.1f, 0.95f);

	public FBCustomizeStage stage;

	public FBCustomizeThumbs thumbs;

	public string sel;

	public string tab = "chars";

	public string group = "tronco";

	public string sexF = "all";

	public FBCustomizeData.Origin originF;

	public string query = "";

	private Vector2 logical = new Vector2(1600f, 900f);

	private Action onClose;

	private Camera cam;

	private Canvas canvas;

	private RectTransform root;

	private RectTransform left;

	private RectTransform right;

	private RectTransform top;

	private RectTransform body;

	private FBCustomizeFx.Aspect aspect;

	private Vector2Int screen;

	private readonly Dictionary<string, (RectTransform rt, Image bg, RawImage img, Text initials, Outline ring, GameObject fav, GameObject used)> cards = new Dictionary<string, (RectTransform, Image, RawImage, Text, Outline, GameObject, GameObject)>();

	private readonly HashSet<string> asked = new HashSet<string>();

	private RectTransform gridView;

	private RectTransform gridContent;

	private Text countT;

	private Text toastT;

	private float visT;

	private float toastUntil;

	private InputField search;

	private List<FBCustomizeData.Entry> list = new List<FBCustomizeData.Entry>();

	public static FBCustomize I { get; private set; }

	public static FBCustomize Open(Camera cam, Action onClose, string tab = "chars")
	{
		if ((bool)I)
		{
			UnityEngine.Object.Destroy(I.gameObject);
		}
		GameObject gameObject = new GameObject("FB_Personagem");
		FBCustomize fBCustomize = gameObject.AddComponent<FBCustomize>();
		fBCustomize.cam = cam;
		fBCustomize.onClose = onClose;
		fBCustomize.tab = tab;
		I = fBCustomize;
		fBCustomize.stage = FBCustomizeStage.Create(cam);
		fBCustomize.stage.transform.SetParent(gameObject.transform, worldPositionStays: false);
		fBCustomize.thumbs = FBCustomizeThumbs.Create(gameObject.transform);
		fBCustomize.sel = ((FBCustomizeData.Find(FBSession.skin) != null) ? FBSession.skin : FBCustomizeData.Roster.FirstOrDefault()?.id);
		fBCustomize.stage.Show(fBCustomize.sel, FBCustomizeData.LookFor(fBCustomize.sel));
		fBCustomize.Build(animate: true);
		return fBCustomize;
	}

	private void OnDestroy()
	{
		if (I == this)
		{
			I = null;
		}
		if ((bool)canvas)
		{
			UnityEngine.Object.Destroy(canvas.gameObject);
		}
	}

	public void Close()
	{
		FBAudio.Synth("hover");
		FBCustomizeFx.SlideOut(left, (aspect == FBCustomizeFx.Aspect.Portrait) ? new Vector2(0f, -120f) : new Vector2(-80f, 0f));
		FBCustomizeFx.SlideOut(right, (aspect == FBCustomizeFx.Aspect.Portrait) ? new Vector2(0f, -60f) : new Vector2(80f, 0f));
		FBCustomizeFx.SlideOut(top, new Vector2(0f, 40f), 0.2f, delegate
		{
			Action action = onClose;
			UnityEngine.Object.Destroy(base.gameObject);
			action?.Invoke();
		});
	}

	private void Build(bool animate)
	{
		if ((bool)canvas)
		{
			UnityEngine.Object.Destroy(canvas.gameObject);
		}
		cards.Clear();
		asked.Clear();
		aspect = FBCustomizeFx.Current;
		screen = new Vector2Int(Screen.width, Screen.height);
		bool flag = aspect == FBCustomizeFx.Aspect.Portrait;
		canvas = FBUI.Canvas("Personagem", 6);
		CanvasScaler component = canvas.GetComponent<CanvasScaler>();
		component.referenceResolution = (flag ? new Vector2(900f, 1600f) : new Vector2(1600f, 900f));
		component.matchWidthOrHeight = 0.5f;
		root = (RectTransform)canvas.transform;
		Rect rect = SafeRect();
		RectTransform rectTransform = FBUI.Rect(root, "area_segura", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
		rectTransform.anchorMin = rect.min;
		rectTransform.anchorMax = rect.max;
		Vector2 offsetMin = (rectTransform.offsetMax = Vector2.zero);
		rectTransform.offsetMin = offsetMin;
		Image image = FBUI.Panel(root, "palco", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));
		image.gameObject.AddComponent<FBCustomizeStageInput>().stage = stage;
		image.transform.SetAsFirstSibling();
		root = rectTransform;
		float num = ((aspect == FBCustomizeFx.Aspect.Narrow) ? 430 : 480);
		float num2 = ((aspect == FBCustomizeFx.Aspect.Narrow) ? 330 : 380);
		float num3 = 72f;
		top = FBUI.Panel(root, "topo", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, (0f - num3) / 2f), new Vector2(0f, num3), new Color(0.03f, 0.035f, 0.04f, 0.82f)).rectTransform;
		Btn(top, "‹ VOLTAR", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(98f, 0f), new Vector2(164f, 46f), Close);
		FBUI.Label(top, "PERSONAGEM", flag ? 30 : 34, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(600f, 40f), TextAnchor.MiddleCenter, title: true, FBUI.Gold);
		FBUI.Label(top, $"{FBCustomizeData.Roster.Count} OPERADORES · {FBCustomizeData.Roster.Count((FBCustomizeData.Entry e) => e.fem)} FEMININAS · {FBCustomizeData.Roster.Count((FBCustomizeData.Entry e) => !e.fem)} MASCULINOS", 12, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(700f, 16f), TextAnchor.MiddleCenter, title: false, Dim);
		FBUI.Panel(top, "linha", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(0f, 2f), new Color(Gold2.r, Gold2.g, Gold2.b, 0.35f)).raycastTarget = false;
		if (flag)
		{
			left = FBUI.Panel(root, "gaveta", new Vector2(0f, 0f), new Vector2(1f, 0.44f), Vector2.zero, Vector2.zero, PanelC).rectTransform;
			right = FBUI.Panel(root, "destaque", new Vector2(0f, 0.44f), new Vector2(1f, 0.44f), new Vector2(0f, 74f), new Vector2(0f, 148f), new Color(0.03f, 0.035f, 0.04f, 0.7f)).rectTransform;
		}
		else
		{
			left = FBUI.Panel(root, "lista", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(num / 2f + 16f, (0f - num3) / 2f + 2f), new Vector2(num, 0f - num3 - 28f), PanelC).rectTransform;
			right = FBUI.Panel(root, "destaque", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2((0f - num2) / 2f - 16f, (0f - num3) / 2f + 2f), new Vector2(num2, 0f - num3 - 28f), PanelC).rectTransform;
		}
		left.gameObject.AddComponent<Outline>().effectColor = new Color(1f, 1f, 1f, 0.08f);
		right.gameObject.AddComponent<Outline>().effectColor = new Color(1f, 1f, 1f, 0.08f);
		Text c = FBUI.Label(root, Application.isMobilePlatform ? "ARRASTE PARA GIRAR · PINÇA PARA ZOOM · TOQUE DUPLO DANÇA" : "ARRASTE PARA GIRAR · RODA = ZOOM · CLIQUE DUPLO DANÇA · ← → NAVEGAR · ENTER USAR", 12, flag ? new Vector2(0.5f, 0.44f) : new Vector2(0.5f, 0f), flag ? new Vector2(0.5f, 0.44f) : new Vector2(0.5f, 0f), flag ? new Vector2(0f, 160f) : new Vector2((num - num2) / 2f, 22f), new Vector2(760f, 18f), TextAnchor.MiddleCenter, title: false, Dim);
		toastT = FBUI.Label(root, "", 18, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(flag ? 0f : ((num - num2) / 2f), 0f - num3 - 34f), new Vector2(700f, 30f), TextAnchor.MiddleCenter, title: true, Gold2);
		toastT.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0f, -2f);
		Vector2 vector = (logical = Vector2.Scale(Logical(component), rect.size));
		stage.view = (flag ? new Rect(0.08f, 0.44f + 160f / vector.y, 0.84f, 0.56f - (160f + num3 + 30f) / vector.y) : new Rect((num + 30f) / vector.x, 50f / vector.y, 1f - (num + num2 + 60f) / vector.x, 1f - (num3 + 50f + 40f) / vector.y));
		stage.view = new Rect(rect.xMin + stage.view.x * rect.width, rect.yMin + stage.view.y * rect.height, stage.view.width * rect.width, stage.view.height * rect.height);
		BuildLeft();
		BuildRight();
		if (animate)
		{
			FBCustomizeFx.SlideIn(top, new Vector2(0f, 50f), 0.3f);
			FBCustomizeFx.SlideIn(left, flag ? new Vector2(0f, -160f) : new Vector2(-90f, 0f), 0.38f, 0.05f);
			FBCustomizeFx.SlideIn(right, flag ? new Vector2(0f, -80f) : new Vector2(90f, 0f), 0.38f, 0.1f);
			FBCustomizeFx.Fade(c, 0f, 1f, 0.5f, 0.4f);
		}
	}

	private static Rect SafeRect()
	{
		float num = Mathf.Max(1, Screen.width);
		float num2 = Mathf.Max(1, Screen.height);
		Rect safeArea = Screen.safeArea;
		if (safeArea.width < 10f || safeArea.height < 10f)
		{
			return new Rect(0f, 0f, 1f, 1f);
		}
		return new Rect(safeArea.x / num, safeArea.y / num2, safeArea.width / num, safeArea.height / num2);
	}

	private static Vector2 Logical(CanvasScaler sc)
	{
		float num = Mathf.Max(1, Screen.width);
		float num2 = Mathf.Max(1, Screen.height);
		float x = sc.referenceResolution.x;
		float y = sc.referenceResolution.y;
		float num3 = Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(num / x, 2f), Mathf.Log(num2 / y, 2f), sc.matchWidthOrHeight));
		return new Vector2(num / num3, num2 / num3);
	}

	private static Button Btn(Transform p, string text, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Action act, bool primary = false, int fs = 0)
	{
		Button button = FBUI.Button(p, text, aMin, aMax, pos, size, delegate
		{
			FBAudio.Synth("hover");
			act?.Invoke();
		}, primary);
		Text componentInChildren = button.GetComponentInChildren<Text>();
		if (fs > 0)
		{
			componentInChildren.fontSize = fs;
		}
		else if (!primary)
		{
			componentInChildren.fontSize = 16;
		}
		componentInChildren.supportRichText = true;
		if (!primary)
		{
			button.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.07f);
		}
		return button;
	}

	private static RectTransform Row(Transform p, string n, float h, bool horiz = true, float spacing = 6f)
	{
		RectTransform component = new GameObject(n, typeof(RectTransform)).GetComponent<RectTransform>();
		component.SetParent(p, worldPositionStays: false);
		LayoutElement layoutElement = component.gameObject.AddComponent<LayoutElement>();
		layoutElement.preferredHeight = h;
		layoutElement.minHeight = h;
		layoutElement.flexibleHeight = 0f;
		if (horiz)
		{
			HorizontalLayoutGroup horizontalLayoutGroup = component.gameObject.AddComponent<HorizontalLayoutGroup>();
			horizontalLayoutGroup.spacing = spacing;
			bool childControlWidth = (horizontalLayoutGroup.childControlHeight = true);
			horizontalLayoutGroup.childControlWidth = childControlWidth;
			horizontalLayoutGroup.childForceExpandWidth = true;
			horizontalLayoutGroup.childForceExpandHeight = true;
		}
		return component;
	}

	private static Button Chip(Transform p, string text, bool on, Action act, int fs = 14)
	{
		Image component = new GameObject("chip_" + text, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
		component.transform.SetParent(p, worldPositionStays: false);
		component.color = (on ? FBUI.Gold : new Color(1f, 1f, 1f, 0.07f));
		Button button = component.gameObject.AddComponent<Button>();
		button.targetGraphic = component;
		ColorBlock colors = button.colors;
		colors.highlightedColor = (on ? new Color(1f, 0.85f, 0.4f) : new Color(1.6f, 1.6f, 1.6f));
		colors.pressedColor = Gold2;
		button.colors = colors;
		button.onClick.AddListener(delegate
		{
			FBAudio.Synth("hover");
			act?.Invoke();
		});
		Text text2 = FBUI.Label(component.transform, text, fs, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-8f, 0f), TextAnchor.MiddleCenter, title: true, on ? new Color(0.07f, 0.06f, 0.04f) : Txt);
		text2.supportRichText = true;
		text2.horizontalOverflow = HorizontalWrapMode.Wrap;
		text2.resizeTextForBestFit = true;
		text2.resizeTextMinSize = 9;
		text2.resizeTextMaxSize = fs;
		return button;
	}

	private static ScrollRect Scroll(Transform p, out RectTransform content, bool grid, Vector2 cell = default(Vector2), int cols = 3)
	{
		GameObject gameObject = new GameObject("rolagem", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
		RectTransform rectTransform = (RectTransform)gameObject.transform;
		rectTransform.SetParent(p, worldPositionStays: false);
		gameObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.18f);
		LayoutElement layoutElement = gameObject.AddComponent<LayoutElement>();
		layoutElement.flexibleHeight = 1f;
		layoutElement.minHeight = 80f;
		GameObject gameObject2 = new GameObject("conteudo", typeof(RectTransform));
		content = (RectTransform)gameObject2.transform;
		content.SetParent(rectTransform, worldPositionStays: false);
		content.anchorMin = new Vector2(0f, 1f);
		content.anchorMax = new Vector2(1f, 1f);
		content.pivot = new Vector2(0.5f, 1f);
		content.sizeDelta = Vector2.zero;
		if (grid)
		{
			GridLayoutGroup gridLayoutGroup = gameObject2.AddComponent<GridLayoutGroup>();
			gridLayoutGroup.cellSize = cell;
			gridLayoutGroup.spacing = new Vector2(8f, 8f);
			gridLayoutGroup.padding = new RectOffset(8, 8, 8, 8);
			gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
			gridLayoutGroup.constraintCount = cols;
			gridLayoutGroup.childAlignment = TextAnchor.UpperCenter;
		}
		else
		{
			VerticalLayoutGroup verticalLayoutGroup = gameObject2.AddComponent<VerticalLayoutGroup>();
			verticalLayoutGroup.spacing = 6f;
			verticalLayoutGroup.padding = new RectOffset(10, 10, 10, 14);
			verticalLayoutGroup.childControlHeight = true;
			verticalLayoutGroup.childControlWidth = true;
			verticalLayoutGroup.childForceExpandHeight = false;
			verticalLayoutGroup.childForceExpandWidth = true;
		}
		gameObject2.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		ScrollRect component = gameObject.GetComponent<ScrollRect>();
		component.content = content;
		component.viewport = rectTransform;
		component.horizontal = false;
		component.scrollSensitivity = 40f;
		component.movementType = ScrollRect.MovementType.Clamped;
		component.inertia = true;
		component.decelerationRate = 0.12f;
		return component;
	}

	private void Toast(string s)
	{
		if ((bool)toastT)
		{
			toastT.text = s;
			toastUntil = Time.unscaledTime + 2.4f;
			FBCustomizeFx.Fade(toastT, 0f, 1f, 0.2f);
			FBCustomizeFx.Pop(toastT.transform, 0.85f);
		}
	}

	private void BuildLeft()
	{
		foreach (Transform item in left)
		{
			UnityEngine.Object.Destroy(item.gameObject);
		}
		cards.Clear();
		asked.Clear();
		gridView = null;
		gridContent = null;
		VerticalLayoutGroup verticalLayoutGroup = left.GetComponent<VerticalLayoutGroup>();
		if (!verticalLayoutGroup)
		{
			verticalLayoutGroup = left.gameObject.AddComponent<VerticalLayoutGroup>();
		}
		verticalLayoutGroup.padding = new RectOffset(12, 12, 12, 12);
		verticalLayoutGroup.spacing = 8f;
		VerticalLayoutGroup verticalLayoutGroup2 = verticalLayoutGroup;
		bool childControlHeight = (verticalLayoutGroup.childControlWidth = true);
		verticalLayoutGroup2.childControlHeight = childControlHeight;
		verticalLayoutGroup.childForceExpandHeight = false;
		verticalLayoutGroup.childForceExpandWidth = true;
		RectTransform p = Row(left, "abas", 42f);
		FBWardrobe wardrobe = stage.wardrobe;
		Tab(p, "PERSONAGENS", tab == "chars", delegate
		{
			SetTab("chars");
		});
		Tab(p, ((bool)wardrobe && wardrobe.Customizable) ? "VISUAL" : "VISUAL <size=11>(—)</size>", tab == "look", delegate
		{
			SetTab("look");
		});
		body = new GameObject("corpo", typeof(RectTransform)).GetComponent<RectTransform>();
		body.SetParent(left, worldPositionStays: false);
		body.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
		VerticalLayoutGroup verticalLayoutGroup3 = body.gameObject.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup3.spacing = 8f;
		childControlHeight = (verticalLayoutGroup3.childControlWidth = true);
		verticalLayoutGroup3.childControlHeight = childControlHeight;
		verticalLayoutGroup3.childForceExpandHeight = false;
		verticalLayoutGroup3.childForceExpandWidth = true;
		if (tab == "look")
		{
			BuildLook();
		}
		else
		{
			BuildChars();
		}
	}

	private void Tab(Transform p, string text, bool on, Action act)
	{
		Button button = Chip(p, text, on: false, act, 16);
		button.GetComponent<Image>().color = (on ? new Color(1f, 1f, 1f, 0.1f) : new Color(1f, 1f, 1f, 0.03f));
		button.GetComponentInChildren<Text>().color = (on ? Gold2 : Dim);
		if (on)
		{
			FBUI.Panel(button.transform, "sublinhado", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 2f), new Vector2(0f, 4f), Gold2).raycastTarget = false;
		}
	}

	public void SetTab(string t)
	{
		if (tab == t)
		{
			return;
		}
		tab = t;
		stage.SetFrame((t == "look") ? GroupDef(group).frame : FBCustomizeStage.Frame.Full);
		if (t == "look" && !Avail(group))
		{
			(string, string, string[], FBCustomizeStage.Frame) tuple = GROUPS.FirstOrDefault(((string key, string label, string[] cats, FBCustomizeStage.Frame frame) x) => Avail(x.key));
			if (tuple.Item1 != null)
			{
				(group, _, _, _) = tuple;
				stage.SetFrame(tuple.Item4);
			}
		}
		BuildLeft();
		FBCustomizeFx.Fade(body, 0f, 1f);
		FBCustomizeFx.SlideIn(body, new Vector2(0f, -16f), 0.26f);
	}

	private void BuildChars()
	{
		List<FBCustomizeData.Entry> roster = FBCustomizeData.Roster;
		RectTransform p = Row(body, "sexo", 32f);
		(string, string, int)[] array = new(string, string, int)[3]
		{
			("all", "TODOS", roster.Count),
			("m", "MASCULINO", roster.Count((FBCustomizeData.Entry e) => !e.fem)),
			("f", "FEMININO", roster.Count((FBCustomizeData.Entry e) => e.fem))
		};
		for (int i = 0; i < array.Length; i++)
		{
			(string, string, int) tuple = array[i];
			string item = tuple.Item1;
			string item2 = tuple.Item2;
			int item3 = tuple.Item3;
			string kk = item;
			Chip(p, string.Format("{0} <size=11><color=#{1}>{2}</color></size>", item2, (sexF == item) ? "3a2a08" : "f5ab17", item3), sexF == item, delegate
			{
				sexF = kk;
				BuildLeft();
			});
		}
		RectTransform p2 = Row(body, "origem", 28f);
		(FBCustomizeData.Origin, string)[] array2 = new(FBCustomizeData.Origin, string)[4]
		{
			(FBCustomizeData.Origin.All, "TODOS"),
			(FBCustomizeData.Origin.Original, "REGIONAIS"),
			(FBCustomizeData.Origin.Npc, "NPC"),
			(FBCustomizeData.Origin.Fav, "★ FAVORITOS")
		};
		for (int i = 0; i < array2.Length; i++)
		{
			(FBCustomizeData.Origin, string) tuple2 = array2[i];
			FBCustomizeData.Origin item4 = tuple2.Item1;
			string item5 = tuple2.Item2;
			FBCustomizeData.Origin oo = item4;
			Chip(p2, item5, originF == item4, delegate
			{
				originF = oo;
				BuildLeft();
			}, 13);
		}
		RectTransform rectTransform = Row(body, "busca", 36f, horiz: false);
		Image image = rectTransform.gameObject.AddComponent<Image>();
		image.color = new Color(1f, 1f, 1f, 0.07f);
		FBUI.Label(rectTransform, "⌕", 22, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(30f, 40f), TextAnchor.MiddleCenter, title: false, Dim);
		Text text = FBUI.Label(rectTransform, query, 18, Vector2.zero, Vector2.one, new Vector2(16f, 0f), new Vector2(-52f, 0f), TextAnchor.MiddleLeft, title: false, Txt);
		text.raycastTarget = false;
		text.supportRichText = false;
		Text text2 = FBUI.Label(rectTransform, "Buscar pelo nome…", 17, Vector2.zero, Vector2.one, new Vector2(16f, 0f), new Vector2(-52f, 0f), TextAnchor.MiddleLeft, title: false, new Color(1f, 1f, 1f, 0.3f));
		text2.fontStyle = FontStyle.Italic;
		search = rectTransform.gameObject.AddComponent<InputField>();
		search.textComponent = text;
		search.placeholder = text2;
		search.text = query;
		search.targetGraphic = image;
		search.onValueChanged.AddListener(delegate(string v)
		{
			query = v;
			ApplyFilter();
		});
		countT = FBUI.Label(body, "", 12, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, TextAnchor.MiddleLeft, title: false, Dim);
		countT.gameObject.AddComponent<LayoutElement>().preferredHeight = 16f;
		float num = ((aspect == FBCustomizeFx.Aspect.Portrait) ? logical.x : ((float)((aspect == FBCustomizeFx.Aspect.Narrow) ? 430 : 480)));
		int num2 = ((aspect == FBCustomizeFx.Aspect.Portrait) ? 5 : 4);
		float num3 = (num - 24f - 16f - (float)((num2 - 1) * 8)) / (float)num2;
		Vector2 cell = new Vector2(num3, num3 * 1.22f);
		ScrollRect scrollRect = Scroll(body, out gridContent, grid: true, cell, num2);
		gridView = (RectTransform)scrollRect.transform;
		foreach (FBCustomizeData.Entry item6 in roster)
		{
			MakeCard(item6, cell);
		}
		ApplyFilter();
		StartCoroutine(CenterSoon());
	}

	private IEnumerator CenterSoon()
	{
		yield return null;
		Canvas.ForceUpdateCanvases();
		CenterOn(sel);
	}

	private void MakeCard(FBCustomizeData.Entry e, Vector2 cell)
	{
		Image component = new GameObject("c_" + e.id, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
		component.transform.SetParent(gridContent, worldPositionStays: false);
		component.color = Card;
		RectTransform rectTransform = (RectTransform)component.transform;
		Text item = FBUI.Label(rectTransform, Initials(e.name), 30, Vector2.zero, Vector2.one, new Vector2(0f, 12f), Vector2.zero, TextAnchor.MiddleCenter, title: true, new Color(1f, 1f, 1f, 0.12f));
		RawImage component2 = new GameObject("mini", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
		component2.transform.SetParent(rectTransform, worldPositionStays: false);
		RectTransform rectTransform2 = component2.rectTransform;
		rectTransform2.anchorMin = Vector2.zero;
		rectTransform2.anchorMax = Vector2.one;
		rectTransform2.offsetMin = new Vector2(2f, 2f);
		rectTransform2.offsetMax = new Vector2(-2f, -2f);
		component2.raycastTarget = false;
		component2.enabled = false;
		Image image = FBUI.Panel(rectTransform, "faixa", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 14f), new Vector2(0f, 28f), new Color(0f, 0f, 0f, 0.62f));
		image.raycastTarget = false;
		Text text = FBUI.Label(image.transform, e.name.ToUpper(), 12, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-8f, 0f), TextAnchor.MiddleCenter, title: true, Txt);
		text.resizeTextForBestFit = true;
		text.resizeTextMinSize = 8;
		text.resizeTextMaxSize = 13;
		Image image2 = FBUI.Panel(rectTransform, "sexo", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(9f, -9f), new Vector2(8f, 8f), e.fem ? new Color(1f, 0.42f, 0.7f) : new Color(0.35f, 0.65f, 1f));
		image2.sprite = FBUI.CircleSprite;
		image2.raycastTarget = false;
		GameObject gameObject = FBUI.Label(rectTransform, "★", 16, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-11f, -11f), new Vector2(20f, 20f), TextAnchor.MiddleCenter, title: false, Gold2).gameObject;
		gameObject.SetActive(FBCustomizeData.IsFav(e.id));
		Image image3 = FBUI.Panel(rectTransform, "emuso", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(58f, 16f), Orange);
		image3.raycastTarget = false;
		FBUI.Label(image3.transform, "EM USO", 10, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, title: true);
		image3.gameObject.SetActive(!FBSession.pickRandom && e.id == FBSession.skin);
		Outline outline = component.gameObject.AddComponent<Outline>();
		outline.effectDistance = new Vector2(3f, -3f);
		outline.effectColor = Gold2;
		outline.enabled = e.id == sel;
		if (e.id == sel)
		{
			component.color = CardHi;
		}
		Button button = component.gameObject.AddComponent<Button>();
		button.transition = Selectable.Transition.None;
		string id = e.id;
		float lastClick = -9f;
		button.onClick.AddListener(delegate
		{
			bool num = Time.unscaledTime - lastClick < 0.35f;
			lastClick = Time.unscaledTime;
			if (num)
			{
				Select(id);
				Use();
			}
			else
			{
				Select(id);
			}
		});
		cards[e.id] = (rectTransform, component, component2, item, outline, gameObject, image3.gameObject);
		if (FBCustomizeThumbs.Cached(e.id))
		{
			SetThumb(e.id, FBCustomizeThumbs.Get(e.id), fade: false);
		}
	}

	private static string Initials(string n)
	{
		return string.Concat(from x in (n ?? "?").Split(new char[2] { ' ', '_' }, StringSplitOptions.RemoveEmptyEntries).Take(2)
			select char.ToUpperInvariant(x[0]));
	}

	private void SetThumb(string id, Texture2D t, bool fade)
	{
		if ((bool)t && cards.TryGetValue(id, out (RectTransform, Image, RawImage, Text, Outline, GameObject, GameObject) value) && (bool)value.Item3)
		{
			value.Item3.texture = t;
			value.Item3.enabled = true;
			value.Item4.enabled = false;
			if (fade)
			{
				FBCustomizeFx.Fade(value.Item3, 0f, 1f, 0.3f);
			}
		}
	}

	private void ApplyFilter()
	{
		HashSet<string> favs = FBCustomizeData.Favs;
		string q = FBCustomizeData.Norm(query).Trim();
		list.Clear();
		foreach (FBCustomizeData.Entry item in FBCustomizeData.Roster)
		{
			bool flag = FBCustomizeData.Match(item, sexF, originF, q, favs);
			if (flag)
			{
				list.Add(item);
			}
			if (cards.TryGetValue(item.id, out (RectTransform, Image, RawImage, Text, Outline, GameObject, GameObject) value))
			{
				value.Item1.gameObject.SetActive(flag);
			}
		}
		if ((bool)countT)
		{
			countT.text = ((list.Count == 0) ? "Nenhum personagem com esse filtro." : $"{list.Count} / {FBCustomizeData.Roster.Count} · arraste a grade, clique para ver, duplo clique para usar");
		}
		visT = 0f;
	}

	private void CenterOn(string id)
	{
		if ((bool)gridView && cards.TryGetValue(id ?? "", out (RectTransform, Image, RawImage, Text, Outline, GameObject, GameObject) value) && value.Item1.gameObject.activeInHierarchy)
		{
			float value2 = 0f - value.Item1.anchoredPosition.y - gridView.rect.height / 2f + value.Item1.rect.height / 2f;
			gridContent.anchoredPosition = new Vector2(0f, Mathf.Clamp(value2, 0f, Mathf.Max(0f, gridContent.rect.height - gridView.rect.height)));
		}
	}

	private void LazyThumbs()
	{
		if (!gridView || tab != "chars")
		{
			return;
		}
		Vector3[] array = new Vector3[4];
		gridView.GetWorldCorners(array);
		float num = (array[1].y - array[0].y) * 0.5f;
		float num2 = array[0].y - num;
		float num3 = array[1].y + num;
		Vector3[] array2 = new Vector3[4];
		foreach (FBCustomizeData.Entry item in list)
		{
			if (asked.Contains(item.id) || !cards.TryGetValue(item.id, out (RectTransform, Image, RawImage, Text, Outline, GameObject, GameObject) value))
			{
				continue;
			}
			value.Item1.GetWorldCorners(array2);
			if (!(array2[1].y < num2) && !(array2[0].y > num3))
			{
				asked.Add(item.id);
				string id = item.id;
				thumbs.Request(id, delegate(Texture2D t)
				{
					SetThumb(id, t, fade: true);
				});
			}
		}
	}

	private bool Avail(string g)
	{
		FBWardrobe w = stage.wardrobe;
		if (!w)
		{
			return false;
		}
		if (GroupDef(g).cats.Any((string c) => w.Parts.Any((FBWardrobe.Part p) => p.cat == c)))
		{
			return true;
		}
		if ((!(g == "cabelo") || !w.HasHairMats) && (!(g == "tronco") || !w.HasClothMats))
		{
			if (g == "pele")
			{
				if (!w.HasSheet && !w.HasSkinMats)
				{
					return w.HasSkinMask;
				}
				return true;
			}
			return false;
		}
		return true;
	}

	private static (string key, string label, string[] cats, FBCustomizeStage.Frame frame) GroupDef(string k)
	{
		(string, string, string[], FBCustomizeStage.Frame) result = GROUPS.FirstOrDefault(((string key, string label, string[] cats, FBCustomizeStage.Frame frame) x) => x.key == k);
		if (result.Item1 == null)
		{
			return GROUPS[2];
		}
		return result;
	}

	private void BuildLook()
	{
		FBWardrobe w = stage.wardrobe;
		if (!w || !w.Customizable)
		{
			Text text = FBUI.Label(body, "Este personagem é uma peça única do jogo original (malha e textura inteiras) e a pele dele não pôde ser separada com segurança (roupa camuflada ou marrom parecida com a pele).\n\nOs personagens <color=#f5ab17>IE</color> têm guarda-roupa completo, os <color=#f5ab17>NPCs</color> trocam pele, cabelo e roupa, e a maioria dos originais troca o <color=#f5ab17>tom de pele</color>.", 16, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, TextAnchor.UpperLeft, title: false, Txt);
			text.supportRichText = true;
			text.gameObject.AddComponent<LayoutElement>().preferredHeight = 200f;
			Chip(Row(body, "ir", 50f), "VER PERSONAGENS PERSONALIZÁVEIS", on: false, delegate
			{
				query = "";
				originF = FBCustomizeData.Origin.All;
				tab = "chars";
				BuildLeft();
				FBCustomizeData.Entry entry = FBCustomizeData.Roster.FirstOrDefault((FBCustomizeData.Entry x) => x.id.StartsWith("ie_"));
				if (entry != null)
				{
					Select(entry.id);
				}
			}, 15);
			return;
		}
		List<(string, string, string[], FBCustomizeStage.Frame)> list = GROUPS.Where(((string key, string label, string[] cats, FBCustomizeStage.Frame frame) g) => Avail(g.key)).ToList();
		if (!list.Any<(string, string, string[], FBCustomizeStage.Frame)>(((string key, string label, string[] cats, FBCustomizeStage.Frame frame) g) => g.key == group) && list.Count > 0)
		{
			group = list[0].Item1;
		}
		int num = ((aspect == FBCustomizeFx.Aspect.Portrait) ? 7 : 4);
		for (int j = 0; j < list.Count; j += num)
		{
			RectTransform p = Row(body, "cats" + j, 34f);
			foreach (var item2 in list.Skip(j).Take(num))
			{
				(string key, string label, string[] cats, FBCustomizeStage.Frame frame) gg = item2;
				Chip(p, item2.Item2, group == item2.Item1, delegate
				{
					group = gg.key;
					stage.SetFrame(gg.frame);
					BuildLeft();
					FBCustomizeFx.Fade(body, 0.4f, 1f, 0.18f);
				}, 13);
			}
		}
		Scroll(body, out var content, grid: false);
		FBWardrobe.Look L = w.look ?? w.Default();
		string[] item = GroupDef(group).cats;
		foreach (string cat in item)
		{
			List<int> list2 = (from i in Enumerable.Range(0, w.Parts.Count)
				where w.Parts[i].cat == cat
				select i).ToList();
			if (list2.Count == 0)
			{
				continue;
			}
			string value;
			string text2 = w.Cats.FirstOrDefault(((string key, string name) c) => c.key == cat).name ?? (CAT_NAMES.TryGetValue(cat, out value) ? value : cat);
			Section(content, text2.ToUpper());
			bool flag = list2.Any((int i) => L.on.Contains(i));
			PartRow(content, "Nenhum", !flag, delegate
			{
				w.ClearCat(L, cat);
				Commit(L);
			});
			foreach (int item3 in list2)
			{
				int ii = item3;
				PartRow(content, Pretty(w.Parts[item3].name), L.on.Contains(item3), delegate
				{
					w.Toggle(L, ii);
					Commit(L);
				});
			}
			if (Array.IndexOf(FBWardrobe.TINTABLE, cat) >= 0)
			{
				Swatches(content, "COR · " + text2.ToUpper(), FBWardrobe.TINTS, FBWardrobe.TINT_NAMES, L.Tint(cat), delegate(int k)
				{
					L.tint[cat] = k;
					Commit(L);
				});
			}
		}
		if (group == "cabelo" && w.HasHairMats)
		{
			Swatches(content, "COR DO CABELO", FBWardrobe.HAIR, FBWardrobe.HAIR_NAMES, L.Tint("hair"), delegate(int k)
			{
				L.tint["hair"] = k;
				Commit(L);
			});
		}
		if (group == "tronco" && !w.HasSheet && w.HasClothMats)
		{
			Swatches(content, "COR DA ROUPA", FBWardrobe.TINTS, FBWardrobe.TINT_NAMES, L.Tint("roupa"), delegate(int k)
			{
				L.tint["roupa"] = k;
				Commit(L);
			});
		}
		if (group == "pele")
		{
			Color?[] array = new Color?[FBWardrobe.TONES.Length + 1];
			string[] array2 = new string[array.Length];
			array[0] = null;
			array2[0] = "Original";
			for (int m = 0; m < FBWardrobe.TONES.Length; m++)
			{
				Color color = FBWardrobe.TONES[m];
				array[m + 1] = (w.HasSkinMask ? FBWardrobe.TONE_SKIN[m] : new Color(color.r * 0.78f, color.g * 0.78f, color.b * 0.78f));
				array2[m + 1] = FBWardrobe.TONE_NAMES[m];
			}
			Swatches(content, "TOM DE PELE", array, array2, L.tone + 1, delegate(int k)
			{
				L.tone = k - 1;
				Commit(L);
			});
			if (w.HasSkinMask)
			{
				FBUI.Label(content, "Tom aproximado: a pele deste personagem foi separada por uma máscara automática da textura (detalhes cor-de-pele da roupa podem mudar junto).", 13, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, TextAnchor.UpperLeft, title: false, Dim).gameObject.AddComponent<LayoutElement>().preferredHeight = 54f;
			}
		}
		RectTransform p2 = Row(body, "acoes", 40f);
		Chip(p2, "ALEATÓRIO", on: false, delegate
		{
			Commit(w.RandomLook());
			BuildLeft();
		}, 15);
		Chip(p2, "ORIGINAL", on: false, delegate
		{
			Commit(w.Default());
			BuildLeft();
		}, 15);
	}

	private static string Pretty(string n)
	{
		if (string.IsNullOrEmpty(n))
		{
			return "Peça";
		}
		n = n.Replace("_", " ").Trim();
		if (n.Length <= 1)
		{
			return n;
		}
		return char.ToUpperInvariant(n[0]) + n.Substring(1);
	}

	private void Section(Transform p, string t)
	{
		FBUI.Label(p, t, 13, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, TextAnchor.LowerLeft, title: true, FBUI.Gold).gameObject.AddComponent<LayoutElement>().preferredHeight = 26f;
	}

	private void PartRow(Transform p, string name, bool on, Action act)
	{
		RectTransform rectTransform = Row(p, "peca", 40f, horiz: false);
		Image image = rectTransform.gameObject.AddComponent<Image>();
		image.color = (on ? new Color(Gold2.r, Gold2.g, Gold2.b, 0.22f) : new Color(1f, 1f, 1f, 0.05f));
		Button button = rectTransform.gameObject.AddComponent<Button>();
		button.targetGraphic = image;
		button.onClick.AddListener(delegate
		{
			FBAudio.Synth("hover");
			act();
			BuildLeftKeepScroll();
		});
		Image image2 = FBUI.Panel(rectTransform, "marca", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(16f, 16f), on ? Gold2 : new Color(1f, 1f, 1f, 0.15f));
		image2.raycastTarget = false;
		if (on)
		{
			FBUI.Label(image2.transform, "✓", 13, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, title: true, Color.black);
		}
		FBUI.Label(rectTransform, name, 16, Vector2.zero, Vector2.one, new Vector2(24f, 0f), new Vector2(-60f, 0f), TextAnchor.MiddleLeft, title: false, on ? Color.white : Txt);
		if (on)
		{
			FBUI.Panel(rectTransform, "borda", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(2f, 0f), new Vector2(4f, 0f), Gold2).raycastTarget = false;
		}
	}

	private void Swatches(Transform p, string title, Color?[] cols, string[] names, int cur, Action<int> set)
	{
		Section(p, title);
		RectTransform component = new GameObject("cores", typeof(RectTransform)).GetComponent<RectTransform>();
		component.SetParent(p, worldPositionStays: false);
		GridLayoutGroup gridLayoutGroup = component.gameObject.AddComponent<GridLayoutGroup>();
		gridLayoutGroup.cellSize = new Vector2(44f, 44f);
		gridLayoutGroup.spacing = new Vector2(8f, 8f);
		gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		gridLayoutGroup.constraintCount = 8;
		component.gameObject.AddComponent<LayoutElement>().preferredHeight = 44 * Mathf.CeilToInt((float)cols.Length / 8f) + 8 * (Mathf.CeilToInt((float)cols.Length / 8f) - 1);
		for (int j = 0; j < cols.Length; j++)
		{
			int i = j;
			Image component2 = new GameObject("cor_" + j, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
			component2.transform.SetParent(component, worldPositionStays: false);
			component2.color = (cols[j].HasValue ? new Color(Mathf.Min(1f, cols[j].Value.r), Mathf.Min(1f, cols[j].Value.g), Mathf.Min(1f, cols[j].Value.b)) : new Color(0.3f, 0.3f, 0.32f));
			if (!cols[j].HasValue)
			{
				FBUI.Label(component2.transform, "ORIG", 10, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, title: true, Txt);
			}
			Outline outline = component2.gameObject.AddComponent<Outline>();
			outline.effectColor = ((j == cur) ? Gold2 : new Color(0f, 0f, 0f, 0.5f));
			outline.effectDistance = ((j == cur) ? new Vector2(3f, -3f) : new Vector2(1f, -1f));
			Button button = component2.gameObject.AddComponent<Button>();
			button.targetGraphic = component2;
			button.transition = Selectable.Transition.None;
			button.onClick.AddListener(delegate
			{
				FBAudio.Synth("hover");
				set(i);
				Toast(names[i]);
				BuildLeftKeepScroll();
			});
		}
	}

	private void BuildLeftKeepScroll()
	{
		ScrollRect scrollRect = (body ? body.GetComponentInChildren<ScrollRect>() : null);
		float y = (scrollRect ? scrollRect.content.anchoredPosition.y : 0f);
		BuildLeft();
		StartCoroutine(RestoreScroll(y));
	}

	private IEnumerator RestoreScroll(float y)
	{
		yield return null;
		ScrollRect scrollRect = (body ? body.GetComponentInChildren<ScrollRect>() : null);
		if ((bool)scrollRect)
		{
			scrollRect.content.anchoredPosition = new Vector2(0f, y);
		}
	}

	public void Commit(FBWardrobe.Look L)
	{
		FBWardrobe wardrobe = stage.wardrobe;
		if ((bool)wardrobe)
		{
			wardrobe.ApplyLook(L);
			FBCustomizeData.SetLook(sel, wardrobe.IsDefault(L) ? "" : wardrobe.Encode(L));
			FBCustomizeThumbs.Invalidate(sel);
			thumbs.Request(sel, delegate(Texture2D t)
			{
				SetThumb(sel, t, fade: true);
			});
			BuildRight();
		}
	}

	private void BuildRight()
	{
		if (!right)
		{
			return;
		}
		foreach (Transform item in right)
		{
			UnityEngine.Object.Destroy(item.gameObject);
		}
		FBCustomizeData.Entry entry = FBCustomizeData.Find(sel);
		if (entry == null)
		{
			return;
		}
		bool num = aspect == FBCustomizeFx.Aspect.Portrait;
		bool fixedMode = !FBSession.pickRandom;
		bool flag = fixedMode && entry.id == FBSession.skin;
		FBWardrobe wardrobe = stage.wardrobe;
		bool childControlWidth;
		if (num)
		{
			FBUI.Label(right, entry.name.ToUpper(), 30, new Vector2(0f, 1f), new Vector2(0.6f, 1f), new Vector2(18f, -26f), new Vector2(-20f, 40f), TextAnchor.MiddleLeft, title: true, Txt).resizeTextForBestFit = true;
			FBUI.Label(right, Tags(entry, flag), 12, new Vector2(0f, 1f), new Vector2(0.6f, 1f), new Vector2(18f, -54f), new Vector2(-20f, 18f), TextAnchor.MiddleLeft, title: false, Gold2).supportRichText = true;
			RectTransform rectTransform = FBUI.Rect(right, "botoes", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 40f), new Vector2(-24f, 62f));
			HorizontalLayoutGroup horizontalLayoutGroup = rectTransform.gameObject.AddComponent<HorizontalLayoutGroup>();
			horizontalLayoutGroup.spacing = 8f;
			childControlWidth = (horizontalLayoutGroup.childControlHeight = true);
			horizontalLayoutGroup.childControlWidth = childControlWidth;
			horizontalLayoutGroup.childForceExpandWidth = true;
			Chip(rectTransform, FBCustomizeData.IsFav(entry.id) ? "★" : "☆", on: false, ToggleFav, 24).gameObject.AddComponent<LayoutElement>().preferredWidth = 50f;
			Chip(rectTransform, "SORTEAR", on: false, Shuffle, 13).gameObject.AddComponent<LayoutElement>().preferredWidth = 90f;
			Chip(rectTransform, flag ? "✓ EM USO" : "USAR", flag, Use, 22).gameObject.AddComponent<LayoutElement>().flexibleWidth = 3f;
			Chip(rectTransform, fixedMode ? "FIXO" : "ALEATÓRIO", on: false, ToggleMode, 13).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1.4f;
			FBUI.Label(right, $"#{entry.index + 1:00} / {FBCustomizeData.Roster.Count}", 14, new Vector2(0.6f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -26f), new Vector2(-20f, 30f), TextAnchor.MiddleRight, title: true, Dim);
			return;
		}
		VerticalLayoutGroup verticalLayoutGroup = right.GetComponent<VerticalLayoutGroup>();
		if (!verticalLayoutGroup)
		{
			verticalLayoutGroup = right.gameObject.AddComponent<VerticalLayoutGroup>();
		}
		verticalLayoutGroup.padding = new RectOffset(18, 18, 16, 16);
		verticalLayoutGroup.spacing = 8f;
		VerticalLayoutGroup verticalLayoutGroup2 = verticalLayoutGroup;
		childControlWidth = (verticalLayoutGroup.childControlWidth = true);
		verticalLayoutGroup2.childControlHeight = childControlWidth;
		verticalLayoutGroup.childForceExpandHeight = false;
		verticalLayoutGroup.childForceExpandWidth = true;
		RectTransform rectTransform2 = Row(right, "indice", 26f, horiz: true, 4f);
		FBUI.Label(rectTransform2, $"<color=#f5ab17>#{entry.index + 1:00}</color> <size=12>/ {FBCustomizeData.Roster.Count} OPERADORES</size>", 20, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, TextAnchor.MiddleLeft, title: true, Dim).supportRichText = true;
		Chip(rectTransform2, FBCustomizeData.IsFav(entry.id) ? "★ FAVORITO" : "☆ FAVORITAR", FBCustomizeData.IsFav(entry.id), ToggleFav, 12).gameObject.AddComponent<LayoutElement>().preferredWidth = 120f;
		Text text = L(entry.name.ToUpper(), 38, title: true, Txt, 54f);
		text.resizeTextForBestFit = true;
		text.resizeTextMinSize = 20;
		text.resizeTextMaxSize = 38;
		text.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0f, -3f);
		L(Tags(entry, flag), 13, title: true, Gold2, 20f);
		string t2 = (flag ? "É o seu personagem: você entra com ele em todas as partidas, até trocar." : (fixedMode ? "Clique em USAR para trocar o seu personagem por este." : "O modo aleatório está ligado (sorteia um do mesmo sexo a cada partida). USAR fixa este personagem e desliga o sorteio."));
		L(t2, 15, title: false, Dim, 62f, TextAnchor.UpperLeft);
		string text2 = ((!wardrobe || !wardrobe.Customizable) ? "PEÇA ÚNICA (SEM TROCA DE ROUPA)" : (wardrobe.HasSheet ? string.Format("GUARDA-ROUPA: {0} PEÇAS", wardrobe.Parts.Count((FBWardrobe.Part p) => p.cat != "body")) : (wardrobe.HasSkinMask ? "TOM DE PELE (APROXIMADO)" : "PERSONALIZÁVEL: PELE, CABELO E ROUPA")));
		Chip(Row(right, "visual", 40f), (((bool)wardrobe && wardrobe.Customizable) ? "✂ PERSONALIZAR · " : "") + text2, on: false, delegate
		{
			SetTab("look");
		}, 12).interactable = (bool)wardrobe && wardrobe.Customizable;
		Button button = FBUI.Button(Row(right, "usar", 78f, horiz: false), "", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Use, !flag);
		button.GetComponentInChildren<Text>().text = "";
		if (flag)
		{
			button.GetComponent<Image>().color = new Color(0.2f, 0.55f, 0.25f, 0.85f);
		}
		FBUI.Label(button.transform, flag ? "✓ EM USO" : "USAR ESTE PERSONAGEM", flag ? 28 : 24, Vector2.zero, Vector2.one, new Vector2(0f, 9f), new Vector2(-10f, -24f), TextAnchor.MiddleCenter, title: true, flag ? Color.white : new Color(0.165f, 0.082f, 0f)).fontStyle = FontStyle.Italic;
		FBUI.Label(button.transform, flag ? "FIXO EM TODAS AS PARTIDAS" : (fixedMode ? "TROCA O SEU PERSONAGEM" : "DESLIGA O MODO ALEATÓRIO"), 11, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 14f), new Vector2(0f, 16f), TextAnchor.MiddleCenter, title: false, flag ? new Color(1f, 1f, 1f, 0.8f) : new Color(0.165f, 0.082f, 0f, 0.8f));
		RectTransform p2 = Row(right, "acoes", 44f);
		Chip(p2, "SORTEAR", on: false, Shuffle);
		Chip(p2, "♪ DANÇAR", on: false, delegate
		{
			stage.PlayDance();
		});
		Chip(p2, "CENTRALIZAR", on: false, delegate
		{
			stage.ResetView();
		}, 12);
		L("COMO VOCÊ ENTRA NA PARTIDA", 12, title: true, Dim, 22f, TextAnchor.LowerLeft);
		RectTransform p3 = Row(right, "modo", 52f);
		Chip(p3, "FIXO<size=10>\nSEMPRE ESTE</size>", fixedMode, delegate
		{
			if (!fixedMode)
			{
				ToggleMode();
			}
		}, 15);
		FBCustomizeData.Entry entry2 = FBCustomizeData.Find(FBSession.skin);
		string text3 = ((entry2 != null && entry2.fem) ? "FEMININAS" : "MASCULINOS");
		Chip(p3, "ALEATÓRIO<size=10>\nSÓ " + text3 + "</size>", !fixedMode, delegate
		{
			if (fixedMode)
			{
				ToggleMode();
			}
		}, 15);
		GameObject obj = new GameObject("folga", typeof(RectTransform));
		obj.transform.SetParent(right, worldPositionStays: false);
		obj.AddComponent<LayoutElement>().flexibleHeight = 1f;
		RectTransform p4 = Row(right, "nav", 40f);
		Chip(p4, "‹ ANTERIOR", on: false, delegate
		{
			Step(-1);
		}, 13);
		Chip(p4, "PRÓXIMO ›", on: false, delegate
		{
			Step(1);
		}, 13);
		Text L(string t, int fs, bool title, Color c, float h, TextAnchor a = TextAnchor.MiddleLeft)
		{
			Text text4 = FBUI.Label(right, t, fs, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, a, title, c);
			text4.supportRichText = true;
			text4.gameObject.AddComponent<LayoutElement>().preferredHeight = h;
			return text4;
		}
	}

	private static string Tags(FBCustomizeData.Entry e, bool using_)
	{
		return (e.fem ? "FEMININO" : "MASCULINO") + " · " + (e.npc ? "NPC" : "REGIONAL") + (e.id.StartsWith("ie_") ? " · INVASÃO ELITE" : "") + (using_ ? "  <color=#7be08a>✓ EM USO</color>" : "");
	}

	public void Select(string id)
	{
		if (id == null || FBCustomizeData.Find(id) == null)
		{
			return;
		}
		if (id != sel)
		{
			if (cards.TryGetValue(sel ?? "", out (RectTransform, Image, RawImage, Text, Outline, GameObject, GameObject) value))
			{
				value.Item5.enabled = false;
				value.Item2.color = Card;
			}
			sel = id;
			stage.Show(id, FBCustomizeData.LookFor(id));
			if (cards.TryGetValue(id, out (RectTransform, Image, RawImage, Text, Outline, GameObject, GameObject) value2))
			{
				value2.Item5.enabled = true;
				value2.Item2.color = CardHi;
				FBCustomizeFx.Pop(value2.Item1);
			}
		}
		if (tab == "look")
		{
			BuildLeft();
		}
		else
		{
			Transform transform = (left ? left.Find("abas") : null);
			if ((bool)transform)
			{
				BuildLeftTabsOnly(transform);
			}
		}
		BuildRight();
		FBCustomizeFx.Fade(right, 0.3f, 1f, 0.2f);
	}

	private void BuildLeftTabsOnly(Transform tabs)
	{
		foreach (Transform tab in tabs)
		{
			UnityEngine.Object.Destroy(tab.gameObject);
		}
		FBWardrobe wardrobe = stage.wardrobe;
		Tab(tabs, "PERSONAGENS", this.tab == "chars", delegate
		{
			SetTab("chars");
		});
		Tab(tabs, ((bool)wardrobe && wardrobe.Customizable) ? "VISUAL" : "VISUAL <size=11>(—)</size>", this.tab == "look", delegate
		{
			SetTab("look");
		});
	}

	public void Step(int d)
	{
		List<FBCustomizeData.Entry> list = ((this.list.Count > 0) ? this.list : FBCustomizeData.Roster);
		if (list.Count != 0)
		{
			int num = list.FindIndex((FBCustomizeData.Entry x) => x.id == sel);
			Select(list[(((num >= 0) ? (num + d) : 0) % list.Count + list.Count) % list.Count].id);
			CenterOn(sel);
			FBAudio.Synth("hover");
		}
	}

	public void Use()
	{
		FBCustomizeData.Entry entry = FBCustomizeData.Find(sel);
		if (entry == null)
		{
			return;
		}
		bool flag = !FBSession.pickRandom && FBSession.skin == sel;
		FBCustomizeData.Use(sel);
		stage.PlayConfirm();
		foreach (KeyValuePair<string, (RectTransform, Image, RawImage, Text, Outline, GameObject, GameObject)> card in cards)
		{
			card.Value.Item7.SetActive(card.Key == sel);
		}
		if (!flag)
		{
			Toast("PERSONAGEM DEFINIDO: " + entry.name.ToUpper());
			FBAudio.Synth("buy");
		}
		BuildRight();
	}

	private void ToggleFav()
	{
		bool flag = FBCustomizeData.ToggleFav(sel);
		if (cards.TryGetValue(sel, out (RectTransform, Image, RawImage, Text, Outline, GameObject, GameObject) value))
		{
			value.Item6.SetActive(flag);
		}
		Toast(flag ? "★ FAVORITO" : "Removido dos favoritos");
		if (originF == FBCustomizeData.Origin.Fav)
		{
			ApplyFilter();
		}
		BuildRight();
	}

	private void Shuffle()
	{
		List<FBCustomizeData.Entry> list = ((this.list.Count > 0) ? this.list : FBCustomizeData.Roster).Where((FBCustomizeData.Entry x) => x.id != sel).ToList();
		if (list.Count != 0)
		{
			Select(list[UnityEngine.Random.Range(0, list.Count)].id);
			CenterOn(sel);
		}
	}

	private void ToggleMode()
	{
		FBSession.pickRandom = !FBSession.pickRandom;
		if (!FBSession.pickRandom)
		{
			FBCustomizeData.Use(sel);
		}
		else
		{
			FBSession.Save();
		}
		foreach (KeyValuePair<string, (RectTransform, Image, RawImage, Text, Outline, GameObject, GameObject)> card in cards)
		{
			card.Value.Item7.SetActive(!FBSession.pickRandom && card.Key == FBSession.skin);
		}
		Toast(FBSession.pickRandom ? "MODO ALEATÓRIO: sorteia a cada partida" : ("MODO FIXO: " + (FBCustomizeData.Find(sel)?.name ?? sel).ToUpper()));
		BuildRight();
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBCustomize.Update");

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
		if (Screen.width != screen.x || Screen.height != screen.y)
		{
			Build(animate: false);
		}
		if ((visT -= Time.unscaledDeltaTime) <= 0f)
		{
			visT = 0.12f;
			LazyThumbs();
		}
		if ((bool)toastT && toastT.text != "" && Time.unscaledTime > toastUntil)
		{
			toastT.text = "";
		}
		Keyboard current = Keyboard.current;
		if (current != null && (!search || !search.isFocused))
		{
			if (current.rightArrowKey.wasPressedThisFrame)
			{
				Step(1);
			}
			else if (current.leftArrowKey.wasPressedThisFrame)
			{
				Step(-1);
			}
			else if (current.enterKey.wasPressedThisFrame || current.numpadEnterKey.wasPressedThisFrame)
			{
				Use();
			}
			else if (current.escapeKey.wasPressedThisFrame)
			{
				Close();
			}
		}
	}

	public IEnumerator CharShots(string dir)
	{
		Directory.CreateDirectory(dir);
		foreach (FBCustomizeData.Entry e in FBCustomizeData.Roster.Where((FBCustomizeData.Entry x) => !x.npc).ToList())
		{
			Select(e.id);
			stage.yaw = 180f;
			stage.zoom = 1f;
			stage.SetFrame(FBCustomizeStage.Frame.Full);
			for (float t3 = 0f; t3 < 1.5f; t3 += Time.unscaledDeltaTime)
			{
				yield return null;
			}
			FBCustomizeFx.Finish();
			stage.Snap();
			for (int k = 0; k < 4; k++)
			{
				yield return null;
			}
			ScreenCapture.CaptureScreenshot(Path.Combine(dir, "char_" + e.id + ".png"));
			yield return null;
			yield return null;
			stage.SetFrame(FBCustomizeStage.Frame.Face);
			for (float t3 = 0f; t3 < 0.6f; t3 += Time.unscaledDeltaTime)
			{
				yield return null;
			}
			stage.Snap();
			for (int k = 0; k < 4; k++)
			{
				yield return null;
			}
			ScreenCapture.CaptureScreenshot(Path.Combine(dir, "rosto_" + e.id + ".png"));
			yield return null;
			yield return null;
			stage.SetFrame(FBCustomizeStage.Frame.Torso);
			stage.yaw = 145f;
			for (float t3 = 0f; t3 < 0.6f; t3 += Time.unscaledDeltaTime)
			{
				yield return null;
			}
			stage.Snap();
			for (int k = 0; k < 4; k++)
			{
				yield return null;
			}
			ScreenCapture.CaptureScreenshot(Path.Combine(dir, "arma_" + e.id + ".png"));
			yield return null;
			yield return null;
			Debug.Log("[FreeBala][personagem] prints de " + e.id);
		}
	}

	public IEnumerator Shots(string dir, bool resolutions)
	{
		Directory.CreateDirectory(dir);
		Dictionary<string, string> keep = new string[3]
		{
			"ie_bandido",
			"c_helena",
			FBCustomizeData.Roster.FirstOrDefault((FBCustomizeData.Entry e) => e.id.StartsWith("n2_"))?.id
		}.Where((string x) => x != null).ToDictionary((string x) => x, FBCustomizeData.LookFor);
		for (float t = 0f; t < 4f; t += Time.unscaledDeltaTime)
		{
			if (thumbs.Pending <= 0)
			{
				break;
			}
			yield return null;
		}
		yield return Shot("grade");
		sexF = "f";
		BuildLeft();
		yield return Shot("filtro_feminino");
		sexF = "all";
		query = "ie";
		BuildLeft();
		yield return Shot("busca_ie");
		query = "";
		BuildLeft();
		FBCustomizeData.Entry entry = FBCustomizeData.Roster.FirstOrDefault((FBCustomizeData.Entry e) => e.id == "ie_bandido") ?? FBCustomizeData.Roster.FirstOrDefault((FBCustomizeData.Entry e) => e.id.StartsWith("ie_"));
		if (entry != null)
		{
			Select(entry.id);
			SetTab("look");
			(string key, string label, string[] cats, FBCustomizeStage.Frame frame)[] gROUPS = GROUPS;
			for (int l = 0; l < gROUPS.Length; l++)
			{
				(string, string, string[], FBCustomizeStage.Frame) tuple = gROUPS[l];
				if (Avail(tuple.Item1))
				{
					(group, _, _, _) = tuple;
					stage.SetFrame(tuple.Item4);
					BuildLeft();
					yield return Shot("visual_" + tuple.Item1);
				}
			}
			Commit(stage.wardrobe.RandomLook());
			BuildLeft();
			yield return Shot("visual_aleatorio");
			stage.zoom = 2.6f;
			yield return Shot("zoom_max");
			stage.zoom = 1f;
		}
		FBCustomizeData.Entry entry2 = FBCustomizeData.Roster.FirstOrDefault((FBCustomizeData.Entry e) => e.id.StartsWith("n2_"));
		if (entry2 != null)
		{
			SetTab("chars");
			Select(entry2.id);
			SetTab("look");
			yield return Shot("visual_npc");
		}
		FBCustomizeData.Entry entry3 = FBCustomizeData.Find("c_helena") ?? FBCustomizeData.Roster.FirstOrDefault((FBCustomizeData.Entry e) => e.id.StartsWith("p_"));
		if (entry3 != null)
		{
			SetTab("chars");
			Select(entry3.id);
			SetTab("look");
			FBWardrobe wardrobe = stage.wardrobe;
			if ((bool)wardrobe && wardrobe.HasSkinMask)
			{
				FBWardrobe.Look look = wardrobe.look ?? wardrobe.Default();
				look.tone = 4;
				Commit(look);
				BuildLeft();
				yield return Shot("visual_atlas_tom");
				stage.SetFrame(FBCustomizeStage.Frame.Full);
				yield return Shot("atlas_tom_corpo");
			}
		}
		SetTab("chars");
		stage.yaw = 120f;
		yield return Shot("girado");
		if (resolutions && !Application.isMobilePlatform)
		{
			int l = Screen.width;
			int h0 = Screen.height;
			FullScreenMode fs = Screen.fullScreenMode;
			Screen.SetResolution(2560, 1080, FullScreenMode.Windowed);
			for (int k = 0; k < 20; k++)
			{
				yield return null;
			}
			yield return Shot("21x9");
			Screen.SetResolution(720, 1280, FullScreenMode.Windowed);
			for (int k = 0; k < 20; k++)
			{
				yield return null;
			}
			yield return Shot("retrato");
			SetTab("look");
			yield return Shot("retrato_visual");
			SetTab("chars");
			Screen.SetResolution(l, h0, fs);
			for (int k = 0; k < 10; k++)
			{
				yield return null;
			}
		}
		foreach (KeyValuePair<string, string> item in keep)
		{
			FBCustomizeData.SetLook(item.Key, item.Value);
		}
		Debug.Log($"[FreeBala][personagem] roster {FBCustomizeData.Roster.Count} · miniaturas pendentes {thumbs.Pending} · visual salvo {FBSession.FullSkin}");
		IEnumerator Shot(string n)
		{
			FBCustomizeFx.Finish();
			stage.Snap();
			for (int m = 0; m < 6; m++)
			{
				yield return null;
			}
			ScreenCapture.CaptureScreenshot(Path.Combine(dir, "personagem_" + n + ".png"));
			yield return null;
			yield return null;
			Debug.Log("[FreeBala][personagem] print " + n);
		}
	}
}
}
