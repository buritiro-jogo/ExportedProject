using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FreeBala
{
public static class FBShop
{
	private struct WS
	{
		public float dmg;

		public float rpm;

		public float prec;

		public float mob;

		public int mag;
	}

	private static GameObject go;

	private static CanvasGroup cg;

	private static RectTransform frame;

	private static RectTransform tabsR;

	private static RectTransform gridR;

	private static RectTransform detailR;

	private static RectTransform loadR;

	private static ScrollRect gridScroll;

	private static Text moneyT;

	private static Text timeT;

	private static Text phaseT;

	private static RectTransform timeBar;

	private static float timeMax;

	private static float openT;

	private static string selCat;

	private static string selId;

	private static string hoverId;

	private static List<(string cat, string icon, List<FBEconomy.Item> items)> cats;

	private const float FW = 1480f;

	private const float FH = 820f;

	private static readonly Color Green = new Color(0.36f, 0.9f, 0.5f);

	private static readonly Color GreenBg = new Color(0.36f, 0.9f, 0.5f, 0.13f);

	private static readonly Color RedT = new Color(0.95f, 0.33f, 0.3f);

	private static readonly Color Panel = new Color(0f, 0f, 0f, 0.28f);

	private static readonly string[] SLOT = new string[3] { "FUZIL", "SMG", "PISTOLA" };

	private static readonly Dictionary<string, string> CLS = new Dictionary<string, string>
	{
		["pistola"] = "PISTOLAS",
		["submetralhadora"] = "SUBMETRALHADORAS",
		["escopeta"] = "ESCOPETAS",
		["fuzil"] = "FUZIS",
		["sniper"] = "SNIPERS",
		["explosivo"] = "EXPLOSIVOS"
	};

	private static readonly Dictionary<string, string> CLS1 = new Dictionary<string, string>
	{
		["pistola"] = "PISTOLA",
		["submetralhadora"] = "SUBMETRALHADORA",
		["escopeta"] = "ESCOPETA",
		["fuzil"] = "FUZIL",
		["sniper"] = "SNIPER",
		["explosivo"] = "EXPLOSIVO"
	};

	public static bool Open
	{
		get
		{
			if ((bool)go)
			{
				return go.activeSelf;
			}
			return false;
		}
	}

	private static bool Arsenal
	{
		get
		{
			if ((bool)FBMatch.I && FBMatch.I.mode != null)
			{
				return FBMatch.I.mode.arsenal;
			}
			return false;
		}
	}

	public static void Show(bool on)
	{
		if (on && (!FBMatch.Player || !FBMatch.Player.alive))
		{
			return;
		}
		if (on && !go)
		{
			Build();
		}
		if ((bool)go)
		{
			go.SetActive(on);
		}
		if (on)
		{
			timeMax = Mathf.Max(1f, FBEconomy.buyT);
			openT = 0f;
			if ((bool)cg)
			{
				cg.alpha = 0f;
			}
			Refresh();
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
			FBAudio.Synth("hover");
		}
		else if ((bool)FBMatch.I && FBMatch.I.state == "play" && !FBInput.Touch && !FBHUD.CardOpen)
		{
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}
	}

	public static void SetTime(float t)
	{
		if (t > timeMax)
		{
			timeMax = t;
		}
		if ((bool)timeT)
		{
			timeT.text = ((t > 0f) ? $"{Mathf.CeilToInt(t)}s" : "");
			timeT.color = ((t > 0f && t <= 5f) ? RedT : FBKit.Txt);
		}
		if ((bool)timeBar)
		{
			timeBar.anchorMax = new Vector2((t > 0f) ? Mathf.Clamp01(t / timeMax) : 0f, 1f);
			timeBar.GetComponent<Image>().color = ((t <= 5f) ? RedT : FBKit.Gold);
		}
	}

	private static void Build()
	{
		Canvas canvas = FBUI.Canvas("Loja", 30);
		go = canvas.gameObject;
		cg = go.AddComponent<CanvasGroup>();
		go.AddComponent<FBShopTicker>();
		FBUI.Panel(canvas.transform, "fundo", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.62f));
		Image image = FBUI.Panel(canvas.transform, "moldura", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1480f, 820f), FBKit.Bg);
		Outline outline = image.gameObject.AddComponent<Outline>();
		outline.effectColor = FBKit.Line;
		outline.effectDistance = new Vector2(1f, -1f);
		frame = image.rectTransform;
		Image image2 = FBKit.At(frame, "acento", 0f, 0f, 1480f, 3f).gameObject.AddComponent<Image>();
		image2.color = FBKit.Gold;
		image2.raycastTarget = false;
		FBKit.T(frame, "LOJA", 36, 28f, 14f, 300f, 44f, TextAnchor.MiddleLeft, title: true, FBKit.Gold);
		phaseT = FBKit.T(frame, "", 12, 30f, 56f, 420f, 18f, TextAnchor.MiddleLeft, title: true, FBKit.Dim);
		timeT = FBKit.T(frame, "", 28, 590f, 12f, 300f, 38f, TextAnchor.MiddleCenter, title: true);
		timeBar = FBUI.Rect(FBKit.Box(frame, "tempo", 590f, 54f, 300f, 6f, new Color(1f, 1f, 1f, 0.1f), border: false).transform, "barra", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
		timeBar.gameObject.AddComponent<Image>().color = FBKit.Gold;
		timeBar.GetComponent<Image>().raycastTarget = false;
		FBKit.T(frame, "SALDO", 12, 1150f, 12f, 300f, 18f, TextAnchor.MiddleRight, title: true, FBKit.Dim);
		moneyT = FBKit.T(frame, "", 34, 1050f, 28f, 400f, 40f, TextAnchor.MiddleRight, title: true, Green);
		Image image3 = FBKit.At(frame, "linha", 24f, 84f, 1432f, 1f).gameObject.AddComponent<Image>();
		image3.color = FBKit.Line;
		image3.raycastTarget = false;
		tabsR = FBKit.At(frame, "categorias", 24f, 100f, 220f, 590f);
		Image image4 = FBKit.Box(frame, "vitrine", 260f, 100f, 742f, 590f, Panel);
		image4.raycastTarget = true;
		image4.gameObject.AddComponent<RectMask2D>();
		GameObject gameObject = new GameObject("cartas", typeof(RectTransform));
		gridR = (RectTransform)gameObject.transform;
		gridR.SetParent(image4.transform, worldPositionStays: false);
		gridR.anchorMin = new Vector2(0f, 1f);
		gridR.anchorMax = new Vector2(1f, 1f);
		gridR.pivot = new Vector2(0.5f, 1f);
		gridR.sizeDelta = Vector2.zero;
		GridLayoutGroup gridLayoutGroup = gameObject.AddComponent<GridLayoutGroup>();
		gridLayoutGroup.cellSize = new Vector2(232f, 176f);
		gridLayoutGroup.spacing = new Vector2(10f, 10f);
		gridLayoutGroup.padding = new RectOffset(10, 10, 10, 10);
		gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		gridLayoutGroup.constraintCount = 3;
		gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		gridScroll = image4.gameObject.AddComponent<ScrollRect>();
		gridScroll.content = gridR;
		gridScroll.horizontal = false;
		gridScroll.scrollSensitivity = 40f;
		gridScroll.movementType = ScrollRect.MovementType.Clamped;
		detailR = FBKit.At(frame, "detalhes", 1018f, 100f, 438f, 590f);
		loadR = FBKit.At(frame, "equipamento", 24f, 706f, 1112f, 88f);
		FBKit.TextTile(frame, "FECHAR   [B]", 1152f, 706f, 304f, 88f, sel: false, delegate
		{
			Show(on: false);
		}, 18);
		// 10x10 paga diferente do 4X4 (FBModo10x10: vitória 2500, derrota 1400 + 500 por derrota seguida, até 3400)
		string fimRodada = (FBSession.mode == "10x10") ? $"VITÓRIA +${2500}  ·  DERROTA +${1400} A ${3400}" : $"FIM DA RODADA +${1500} OU MAIS";
		FBKit.T(frame, $"ABATE +${250}  ·  HEADSHOT +${350}  ·  {fimRodada}  ·  QUEM SOBREVIVE MANTÉM AS ARMAS  ·  QUEM MORRE PERDE TUDO", 11, 24f, 798f, 1432f, 18f, TextAnchor.MiddleCenter, title: true, FBKit.Dim);
	}

	public static void Refresh()
	{
		if (!go || !FBMatch.Player || FBMatch.I == null)
		{
			return;
		}
		FBCharacter player = FBMatch.Player;
		bool arsenal = Arsenal;
		moneyT.text = (arsenal ? "GRÁTIS" : $"$ {FBEconomy.Money(player)}");
		phaseT.text = (arsenal ? "ARSENAL DO TREINO · PEGUE QUALQUER ARMA" : (FBEconomy.buying ? "FASE DE COMPRA · ESCOLHA SEU EQUIPAMENTO" : "FORA DA FASE DE COMPRA"));
		cats = (List<(string cat, string icon, List<FBEconomy.Item> items)>)(arsenal ? ((IList)(from i in FBEconomy.ArsenalItems()
			group i by i.cls into g
			select (CLS.TryGetValue(g.Key ?? "", out var value) ? value : (g.Key ?? "").ToUpper(), "", g.ToList())).ToList()) : ((IList)FBEconomy.ShopData()));
		if (cats.Count == 0)
		{
			return;
		}
		if (selCat == null || !cats.Any(((string cat, string icon, List<FBEconomy.Item> items) c) => c.cat == selCat))
		{
			selCat = cats[0].cat;
		}
		List<FBEconomy.Item> item = cats.First(((string cat, string icon, List<FBEconomy.Item> items) c) => c.cat == selCat).items;
		if (selId == null || !item.Any((FBEconomy.Item i) => i.id == selId))
		{
			selId = (Best(item) ?? item.FirstOrDefault((FBEconomy.Item i) => !i.own) ?? item[0]).id;
		}
		hoverId = null;
		float y = (gridR ? gridR.anchoredPosition.y : 0f);
		Clear(tabsR);
		Clear(gridR);
		Clear(loadR);
		Tabs(player);
		Grid(player, item);
		Loadout(player);
		Detail(player);
		gridR.anchoredPosition = new Vector2(0f, y);
	}

	private static void Clear(Transform t)
	{
		for (int num = t.childCount - 1; num >= 0; num--)
		{
			UnityEngine.Object.Destroy(t.GetChild(num).gameObject);
		}
	}

	private static FBEconomy.Item Find(string id)
	{
		if (cats == null || id == null)
		{
			return null;
		}
		foreach (var cat in cats)
		{
			foreach (FBEconomy.Item item in cat.items)
			{
				if (item.id == id)
				{
					return item;
				}
			}
		}
		return null;
	}

	private static bool Afford(FBEconomy.Item it)
	{
		if (!Arsenal)
		{
			return FBEconomy.Money(FBMatch.Player) >= it.price;
		}
		return true;
	}

	private static FBEconomy.Item Best(List<FBEconomy.Item> items)
	{
		if (!Arsenal)
		{
			return (from i in items
				where !i.gear && !i.own && Afford(i)
				orderby i.price descending
				select i).FirstOrDefault();
		}
		return null;
	}

	private static void Tabs(FBCharacter p)
	{
		float num = 0f;
		foreach (var cat in cats)
		{
			string item = cat.cat;
			List<FBEconomy.Item> item2 = cat.items;
			bool flag = item == selCat;
			string c2 = item;
			Button button = FBKit.Tile(tabsR, "cat_" + item, 0f, num, 220f, 84f, flag, delegate
			{
				selCat = c2;
				selId = null;
				if ((bool)gridR)
				{
					gridR.anchoredPosition = Vector2.zero;
				}
				Refresh();
			});
			if (flag)
			{
				Image image = FBKit.At(button.transform, "acento", 0f, 0f, 4f, 84f).gameObject.AddComponent<Image>();
				image.color = FBKit.Gold;
				image.raycastTarget = false;
			}
			FBKit.T(button.transform, item, 14, 16f, 12f, 200f, 20f, TextAnchor.MiddleLeft, title: true, flag ? Color.white : FBKit.Txt);
			bool num2 = item2.Any((FBEconomy.Item i) => i.own);
			FBKit.T(button.transform, $"{item2.Count} ITENS", 10, 16f, 32f, 120f, 16f, TextAnchor.MiddleLeft, title: true, FBKit.Dim);
			if (num2)
			{
				FBKit.Box(button.transform, "tem", 196f, 14f, 8f, 8f, Green, border: false).sprite = FBUI.CircleSprite;
			}
			FBEconomy.Item item3 = (from i in item2
				where !i.gear
				orderby i.price descending
				select i).FirstOrDefault();
			Texture2D texture2D = ((item3 != null) ? FBIcons.Weapon(item3.id) : null);
			if ((bool)texture2D)
			{
				RawImage rawImage = FBKit.At(button.transform, "img", 100f, 40f, 110f, 40f).gameObject.AddComponent<RawImage>();
				rawImage.texture = texture2D;
				rawImage.raycastTarget = false;
				rawImage.color = (flag ? Color.white : new Color(1f, 1f, 1f, 0.55f));
			}
			else
			{
				FBKit.T(button.transform, (item == "EQUIPAMENTO") ? "COLETE · CAPACETE · KIT" : "", 10, 16f, 52f, 200f, 16f, TextAnchor.MiddleLeft, title: true, FBKit.Dim);
			}
			num += 92f;
		}
	}

	private static void Grid(FBCharacter p, List<FBEconomy.Item> items)
	{
		FBWeaponUser component = p.GetComponent<FBWeaponUser>();
		FBEconomy.Item item2 = Best(items);
		foreach (FBEconomy.Item item3 in items)
		{
			FBEconomy.Item item = item3;
			bool sel = item3.id == selId;
			bool flag = Afford(item3);
			bool flag2 = !item3.gear && component.Cur != null && component.Cur.id == item3.id;
			GameObject gameObject = new GameObject("carta_" + item3.id, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
			gameObject.transform.SetParent(gridR, worldPositionStays: false);
			Image im = gameObject.GetComponent<Image>();
			Color baseC = (sel ? new Color(1f, 0.74f, 0.12f, 0.14f) : (item3.own ? GreenBg : FBKit.Card));
			im.color = baseC;
			Outline outline = gameObject.AddComponent<Outline>();
			outline.effectColor = (sel ? new Color(1f, 0.74f, 0.12f, 0.95f) : FBKit.Line);
			outline.effectDistance = (sel ? new Vector2(2f, -2f) : new Vector2(1f, -1f));
			gameObject.GetComponent<CanvasGroup>().alpha = ((flag || item3.own) ? 1f : 0.5f);
			Transform transform = gameObject.transform;
			FBKit.T(transform, item3.name, 15, 12f, 10f, 208f, 20f, TextAnchor.MiddleLeft, title: true);
			FBKit.T(transform, item3.gear ? "EQUIPAMENTO" : (CLS1.TryGetValue(item3.cls ?? "", out var value) ? value : item3.sub), 10, 12f, 30f, 140f, 14f, TextAnchor.MiddleLeft, title: true, FBKit.Dim);
			string text = (flag2 ? "NA MÃO" : ((item3.own && !item3.rebuy) ? (item3.gear ? "COMPLETO" : "NO SLOT") : ((item3 == item2) ? "MELHOR P/ SEU SALDO" : ((item3.best && !item3.own) ? "ECONOMIZE" : null))));
			if (text != null)
			{
				Color value2 = (flag2 ? FBKit.Gold : (item3.own ? Green : new Color(0.45f, 0.7f, 1f)));
				FBKit.T(FBKit.Box(transform, "selo", 220f - ((float)text.Length * 6.4f + 14f), 30f, (float)text.Length * 6.4f + 14f, 16f, new Color(value2.r, value2.g, value2.b, 0.18f), border: false).transform, text, 9, 0f, 0f, (float)text.Length * 6.4f + 14f, 16f, TextAnchor.MiddleCenter, title: true, value2);
			}
			Texture2D texture2D = ((!item3.gear) ? FBIcons.Weapon(item3.id) : null);
			if ((bool)texture2D)
			{
				RawImage rawImage = FBKit.At(transform, "img", 16f, 50f, 200f, 86f).gameObject.AddComponent<RawImage>();
				rawImage.texture = texture2D;
				rawImage.raycastTarget = false;
			}
			else
			{
				FBKit.T(transform, GearGlyph(item3.id), 30, 12f, 54f, 208f, 76f, TextAnchor.MiddleCenter, title: true, new Color(1f, 1f, 1f, 0.25f));
			}
			string s = (Arsenal ? "PEGAR" : ((item3.own && !item3.rebuy) ? (item3.gear ? "OK" : "EQUIPAR") : $"$ {item3.price}"));
			FBKit.T(transform, s, 19, 12f, 142f, 120f, 26f, TextAnchor.MiddleLeft, title: true, (Arsenal || (item3.own && !item3.rebuy)) ? FBKit.Gold : (flag ? Green : RedT));
			WeaponDef value3;
			if (item3.note != null)
			{
				FBKit.T(transform, item3.note, 11, 120f, 142f, 100f, 26f, TextAnchor.MiddleRight, title: true, FBKit.Dim);
			}
			else if (!item3.gear && GameData.Weapons.TryGetValue(item3.id, out value3))
			{
				FBKit.T(transform, $"DANO {Mathf.RoundToInt(Stats(item3.id).dmg)}", 11, 120f, 142f, 100f, 26f, TextAnchor.MiddleRight, title: true, FBKit.Dim);
			}
			Button button = gameObject.AddComponent<Button>();
			button.targetGraphic = im;
			button.transition = Selectable.Transition.None;
			button.onClick.AddListener(delegate
			{
				if (selId == item.id)
				{
					DoBuy(item);
				}
				else
				{
					selId = item.id;
					FBAudio.Synth("hover");
					Refresh();
				}
			});
			EventTrigger tr = gameObject.AddComponent<EventTrigger>();
			E(EventTriggerType.PointerEnter, delegate
			{
				if (!sel)
				{
					im.color = FBKit.CardHi;
				}
				hoverId = item.id;
				Detail(FBMatch.Player);
			});
			E(EventTriggerType.PointerExit, delegate
			{
				im.color = baseC;
				if (hoverId == item.id)
				{
					hoverId = null;
					Detail(FBMatch.Player);
				}
			});
			void E(EventTriggerType ty, Action a)
			{
				EventTrigger.Entry entry = new EventTrigger.Entry
				{
					eventID = ty
				};
				entry.callback.AddListener(delegate
				{
					a();
				});
				tr.triggers.Add(entry);
			}
		}
	}

	private static string GearGlyph(string id)
	{
		return id switch
		{
			"medkit" => "+ KIT MÉDICO", 
			"kit" => "COLETE + CAPACETE", 
			"capacete" => "CAPACETE", 
			"colete" => "COLETE", 
			_ => "", 
		};
	}

	private static void DoBuy(FBEconomy.Item it)
	{
		if (it == null)
		{
			return;
		}
		FBCharacter player = FBMatch.Player;
		if ((bool)player && player.alive)
		{
			if (!Arsenal && !FBEconomy.buying)
			{
				FBAudio.Synth("deny");
				return;
			}
			if (!Arsenal && (!it.own || it.rebuy) && !Afford(it))
			{
				FBAudio.Synth("deny");
				return;
			}
			FBEconomy.Buy(it);
			Refresh();
		}
	}

	private static WS Stats(string id)
	{
		Dictionary<string, object> o = Json.O(Json.O(GameData.WeaponsRaw, id), "stats");
		float rpm = Json.F(o, "roundsPerMinutes", 60f / Mathf.Max(0.01f, Json.F(o, "secondsBetweenShots", 0.1f)));
		WS result = default(WS);
		result.dmg = Json.F(o, "baseDamage", 25f) * Mathf.Max(1f, Json.F(o, "shotCount", 1f));
		result.rpm = rpm;
		result.prec = Mathf.Clamp((100f - Json.F(o, "spread", 0.25f) * 60f) / 105f, 0.05f, 1f) * 100f;
		result.mob = Json.F(o, "multiplierMovementSpeed", 1f) * 100f;
		result.mag = (GameData.Weapons.TryGetValue(id, out var value) ? value.mag : 0);
		return result;
	}

	private static void Detail(FBCharacter p)
	{
		if (!detailR || !p)
		{
			return;
		}
		Clear(detailR);
		FBEconomy.Item item = Find(hoverId) ?? Find(selId);
		if (item == null)
		{
			return;
		}
		FBWeaponUser component = p.GetComponent<FBWeaponUser>();
		FBKit.Box(detailR, "fundo", 0f, 0f, 438f, 590f, Panel);
		FBKit.Section(detailR, (hoverId != null && hoverId != selId) ? "PRÉVIA" : "SELECIONADO", 14f, 14f, 300f);
		Image image = FBKit.Box(detailR, "arte", 14f, 42f, 410f, 168f, new Color(1f, 1f, 1f, 0.035f));
		Image image2 = FBKit.At(image.transform, "brilho", 55f, 4f, 300f, 160f).gameObject.AddComponent<Image>();
		image2.sprite = FBUI.CircleSprite;
		image2.color = new Color(1f, 0.74f, 0.12f, 0.07f);
		image2.raycastTarget = false;
		Texture2D texture2D = ((!item.gear) ? FBIcons.Weapon(item.id, 512, 224) : null);
		if ((bool)texture2D)
		{
			RawImage rawImage = FBKit.At(image.transform, "img", 16f, 10f, 378f, 148f).gameObject.AddComponent<RawImage>();
			rawImage.texture = texture2D;
			rawImage.raycastTarget = false;
		}
		else
		{
			FBKit.T(image.transform, GearGlyph(item.id), 30, 0f, 0f, 410f, 168f, TextAnchor.MiddleCenter, title: true, new Color(1f, 1f, 1f, 0.3f));
		}
		FBKit.T(detailR, item.name, 24, 14f, 220f, 410f, 30f, TextAnchor.MiddleLeft, title: true);
		float num = 256f;
		if (!item.gear && GameData.Weapons.TryGetValue(item.id, out var value))
		{
			int num2 = Mathf.Clamp(value.slot, 0, 2);
			WeaponDef weaponDef = component.slots[num2];
			bool flag = weaponDef != null && weaponDef.id == item.id;
			string value2;
			string s = string.Format("{0}  ·  SLOT {1} ({2})  ·  {3}", CLS1.TryGetValue(value.cls ?? "", out value2) ? value2 : value.cls?.ToUpper(), num2 + 1, SLOT[num2], value.auto ? "AUTOMÁTICA" : "SEMIAUTOMÁTICA");
			FBKit.T(detailR, s, 11, 14f, num, 410f, 16f, TextAnchor.MiddleLeft, title: true, FBKit.Dim);
			num += 20f;
			FBKit.T(detailR, flag ? "VOCÊ JÁ TEM ESTA ARMA" : ((weaponDef != null) ? ("SUBSTITUI: " + weaponDef.name.ToUpper()) : "SLOT VAZIO"), 11, 14f, num, 410f, 16f, TextAnchor.MiddleLeft, title: true, flag ? Green : ((weaponDef != null) ? FBKit.Gold : FBKit.Dim));
			num += 28f;
			WS wS = Stats(item.id);
			WS? wS2 = ((weaponDef != null && !flag) ? new WS?(Stats(weaponDef.id)) : null);
			Stat("DANO", wS.dmg, wS2?.dmg, 130f, "0", ref num);
			Stat("CADÊNCIA", wS.rpm, wS2?.rpm, 1000f, "0' RPM'", ref num);
			Stat("PRECISÃO", wS.prec, wS2?.prec, 100f, "0'%'", ref num);
			Stat("MOBILIDADE", wS.mob, wS2?.mob, 115f, "0'%'", ref num);
			Stat("PENTE", wS.mag, wS2?.mag, 100f, "0", ref num);
			if (wS2.HasValue)
			{
				FBKit.Box(detailR, "leg1", 14f, num + 4f, 10f, 4f, new Color(1f, 1f, 1f, 0.35f), border: false);
				FBKit.T(detailR, "= " + weaponDef.name.ToUpper() + " (SUA)", 9, 28f, num - 2f, 300f, 16f, TextAnchor.MiddleLeft, title: true, FBKit.Dim);
			}
		}
		else
		{
			FBKit.T(detailR, item.sub ?? "", 13, 14f, num, 410f, 20f, TextAnchor.MiddleLeft, title: true, FBKit.Dim);
			num += 30f;
			string s2 = item.id switch
			{
				"colete" => "Absorve 45% do dano no corpo até a blindagem acabar. Quem sobrevive à rodada mantém o que sobrou.", 
				"capacete" => "Reduz em 40% o dano de tiros na cabeça. Fica com você enquanto sobreviver.", 
				"kit" => "Colete completo + capacete de uma vez, mais barato do que comprar separado.", 
				"medkit" => $"Cura {75f} de vida (tecla H). Leva alguns segundos e cancela se você correr, pular ou atirar.", 
				_ => "", 
			};
			FBKit.T(detailR, s2, 14, 14f, num, 410f, 80f, TextAnchor.UpperLeft, title: false, FBKit.Txt);
			num += 90f;
			if (item.note != null)
			{
				FBKit.T(detailR, "ATUAL: " + item.note, 12, 14f, num, 410f, 18f, TextAnchor.MiddleLeft, title: true, FBKit.Gold);
			}
		}
		bool flag2 = Afford(item);
		bool flag3 = item.own && !item.rebuy;
		string s3;
		bool flag4;
		if (Arsenal)
		{
			s3 = (item.gear ? "INDISPONÍVEL NO ARSENAL" : "PEGAR ARMA");
			flag4 = !item.gear;
		}
		else if (flag3)
		{
			s3 = (item.gear ? "JÁ EQUIPADO" : "EQUIPAR");
			flag4 = !item.gear;
		}
		else if (!FBEconomy.buying)
		{
			s3 = "FORA DA FASE DE COMPRA";
			flag4 = false;
		}
		else if (!flag2)
		{
			s3 = $"FALTAM $ {item.price - FBEconomy.Money(p)}";
			flag4 = false;
		}
		else
		{
			s3 = $"COMPRAR  ·  $ {item.price}";
			flag4 = true;
		}
		if (!Arsenal && !flag3)
		{
			FBKit.T(detailR, flag2 ? $"SALDO DEPOIS: $ {FBEconomy.Money(p) - item.price}" : "SALDO INSUFICIENTE", 11, 14f, 500f, 410f, 16f, TextAnchor.MiddleRight, title: true, flag2 ? FBKit.Dim : RedT);
		}
		FBEconomy.Item buyIt = item;
		if (flag4)
		{
			FBKit.Primary(detailR, s3, 14f, 520f, 410f, 56f, delegate
			{
				DoBuy(buyIt);
			});
		}
		else
		{
			FBKit.T(FBKit.Box(detailR, "bloq", 14f, 520f, 410f, 56f, new Color(1f, 1f, 1f, 0.05f)).transform, s3, 16, 0f, 0f, 410f, 56f, TextAnchor.MiddleCenter, title: true, flag3 ? Green : RedT);
		}
	}

	private static void Stat(string name, float v, float? cur, float max, string fmt, ref float y)
	{
		FBKit.T(detailR, name, 11, 14f, y, 100f, 18f, TextAnchor.MiddleLeft, title: true, FBKit.Dim);
		Image image = FBKit.Box(detailR, "st_" + name, 120f, y + 6f, 230f, 6f, new Color(1f, 1f, 1f, 0.08f), border: false);
		float num = Mathf.Clamp01(v / max);
		if (cur.HasValue)
		{
			Image image2 = FBKit.At(image.transform, "sua", 0f, 0f, 230f * Mathf.Clamp01(cur.Value / max), 6f).gameObject.AddComponent<Image>();
			image2.color = new Color(1f, 1f, 1f, 0.35f);
			image2.raycastTarget = false;
		}
		Image image3 = FBKit.At(image.transform, "esta", 0f, cur.HasValue ? 2 : 0, 230f * num, cur.HasValue ? 4 : 6).gameObject.AddComponent<Image>();
		image3.color = FBKit.Gold;
		image3.raycastTarget = false;
		string text = v.ToString(fmt);
		if (cur.HasValue)
		{
			float num2 = v - cur.Value;
			if (Mathf.Abs(num2) >= 0.5f)
			{
				text = string.Format("{0}  <color={1}>{2}{3:0}</color>", text, (num2 > 0f) ? "#5ce680" : "#f2554d", (num2 > 0f) ? "+" : "−", Mathf.Abs(num2));
			}
		}
		FBKit.T(detailR, text, 12, 358f, y, 66f, 18f, TextAnchor.MiddleRight, title: true).supportRichText = true;
		y += 26f;
	}

	private static void Loadout(FBCharacter p)
	{
		FBWeaponUser component = p.GetComponent<FBWeaponUser>();
		float x = 0f;
		for (int i = 0; i < 3; i++)
		{
			WeaponDef weaponDef = component.slots[i];
			bool flag = component.cur == i;
			Image image = FBKit.Box(loadR, "slot" + i, x, 0f, 218f, 88f, flag ? new Color(1f, 0.74f, 0.12f, 0.12f) : FBKit.Card);
			if (flag)
			{
				Image image2 = FBKit.At(image.transform, "acento", 0f, 85f, 218f, 3f).gameObject.AddComponent<Image>();
				image2.color = FBKit.Gold;
				image2.raycastTarget = false;
			}
			FBKit.T(image.transform, $"{i + 1}  {SLOT[i]}", 10, 10f, 8f, 120f, 14f, TextAnchor.MiddleLeft, title: true, FBKit.Dim);
			if (weaponDef != null)
			{
				Texture2D texture2D = FBIcons.Weapon(weaponDef.id);
				if ((bool)texture2D)
				{
					RawImage rawImage = FBKit.At(image.transform, "img", 96f, 18f, 114f, 50f).gameObject.AddComponent<RawImage>();
					rawImage.texture = texture2D;
					rawImage.raycastTarget = false;
				}
				FBKit.T(image.transform, weaponDef.name.ToUpper(), 13, 10f, 28f, 130f, 34f, TextAnchor.MiddleLeft, title: true);
				FBKit.T(image.transform, $"{component.ammo[i]} / {component.reserve[i]}", 11, 10f, 66f, 120f, 14f, TextAnchor.MiddleLeft, title: true, FBKit.Dim);
			}
			else
			{
				FBKit.T(image.transform, "VAZIO", 13, 10f, 34f, 200f, 24f, TextAnchor.MiddleLeft, title: true, new Color(1f, 1f, 1f, 0.25f));
			}
			x += 226f;
		}
		Gear("COLETE", (p.armor > 0f) ? $"{Mathf.RoundToInt(p.armor)}%" : "SEM", p.armor / 100f, p.armor > 0f);
		Gear("CAPACETE", p.helmet ? "SIM" : "SEM", p.helmet ? 1 : 0, p.helmet);
		Gear("KIT MÉDICO", FBMed.inf ? "∞" : $"{FBMed.kits}/{4}", FBMed.inf ? 1f : ((float)FBMed.kits / (float)Mathf.Max(1, 4)), FBMed.inf || FBMed.kits > 0);
		void Gear(string title, string val, float fill, bool ok)
		{
			Image image3 = FBKit.Box(loadR, "eq_" + title, x, 0f, 156f, 88f, ok ? GreenBg : FBKit.Card);
			FBKit.T(image3.transform, title, 10, 10f, 8f, 140f, 14f, TextAnchor.MiddleLeft, title: true, FBKit.Dim);
			FBKit.T(image3.transform, val, 20, 10f, 28f, 140f, 30f, TextAnchor.MiddleLeft, title: true, ok ? Green : new Color(1f, 1f, 1f, 0.35f));
			Image image4 = FBKit.At(FBKit.Box(image3.transform, "barra", 10f, 70f, 136f, 4f, new Color(1f, 1f, 1f, 0.08f), border: false).transform, "f", 0f, 0f, 136f * Mathf.Clamp01(fill), 4f).gameObject.AddComponent<Image>();
			image4.color = (ok ? Green : FBKit.Gold);
			image4.raycastTarget = false;
			x += 164f;
		}
	}

	public static void Tick(float dt)
	{
		if (Open && (bool)cg && (bool)frame)
		{
			openT += dt;
			float num = 1f - Mathf.Exp((0f - openT) * 14f);
			cg.alpha = num;
			frame.localScale = Vector3.one * Mathf.Lerp(0.97f, 1f, num);
		}
	}
}
}
