// LOJA (fase de compra do 4V4 / Farinha × Buriti) e ARSENAL do treino — refeita em 24/10/2026 (retorno do usuário: "a tela de loja é
// muito simples, feia e sem sentido"). Antes: uma lista rolando com todas as categorias empilhadas, cartas pequenas com barrinhas sem
// legenda e sem comparação. Agora, no padrão visual do lobby (FBKit):
//  · topo: título + fase, TEMPO restante com barra que esvazia, SALDO em destaque;
//  · esquerda: categorias (contagem, imagem da arma mais forte, ponto verde quando você já tem algo da categoria);
//  · centro: vitrine em grade — imagem grande, preço (vermelho = sem saldo), selo NA MÃO / NO SLOT / MELHOR P/ SEU SALDO;
//  · direita: detalhes — atributos com NÚMEROS e comparação com a arma que ocupa o mesmo slot (barra clara = a sua, +/− colorido),
//    o que ela substitui e o botão de compra com o estado certo (COMPRAR / EQUIPAR / SALDO INSUFICIENTE / FORA DA FASE);
//  · base: seu equipamento atual (3 slots com munição, colete, capacete, kits médicos) e a regra da economia.
// Clique numa carta = seleciona (passar o mouse já mostra os detalhes); clique de novo na selecionada = compra rápida.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FreeBala
{
    public static class FBShop
    {
        public static bool Open => go && go.activeSelf;
        public static string TimeText => timeT ? timeT.text : ""; public static float BarFill => timeBar ? timeBar.anchorMax.x : -1;   // r51: teste da compra no exe
        static GameObject go; static CanvasGroup cg; static RectTransform frame, tabsR, gridR, detailR, loadR; static ScrollRect gridScroll;
        static Text moneyT, timeT, phaseT; static RectTransform timeBar; static float timeMax, openT;
        static string selCat, selId, hoverId;
        static List<(string cat, string icon, List<FBEconomy.Item> items)> cats;

        const float FW = 1480, FH = 820;
        static readonly Color Green = new(.36f, .9f, .5f), GreenBg = new(.36f, .9f, .5f, .13f), RedT = new(.95f, .33f, .3f), Panel = new(0, 0, 0, .28f);
        static readonly string[] SLOT = { "FUZIL", "SMG", "PISTOLA" };
        static readonly Dictionary<string, string> CLS = new() { ["pistola"] = "PISTOLAS", ["submetralhadora"] = "SUBMETRALHADORAS", ["escopeta"] = "ESCOPETAS", ["fuzil"] = "FUZIS", ["sniper"] = "SNIPERS", ["explosivo"] = "EXPLOSIVOS" };
        static readonly Dictionary<string, string> CLS1 = new() { ["pistola"] = "PISTOLA", ["submetralhadora"] = "SUBMETRALHADORA", ["escopeta"] = "ESCOPETA", ["fuzil"] = "FUZIL", ["sniper"] = "SNIPER", ["explosivo"] = "EXPLOSIVO" };

        static bool Arsenal => FBMatch.I && FBMatch.I.mode != null && FBMatch.I.mode.arsenal;

        public static void Show(bool on)
        {
            if (on && (!FBMatch.Player || !FBMatch.Player.alive)) return;
            if (on && !go) Build();
            if (go) go.SetActive(on);
            if (on)
            {
                timeMax = Mathf.Max(1, FBEconomy.buyTotal > 0 ? FBEconomy.buyTotal : FBEconomy.buyT); openT = 0; if (cg) cg.alpha = 0;
                Refresh(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; FBAudio.Synth("hover");
            }
            else if (FBMatch.I && FBMatch.I.state == "play" && !FBInput.Touch && !FBHUD.CardOpen) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }
        public static void SetTime(float t)
        {
            if (t > timeMax) timeMax = t; if (FBEconomy.buyTotal > 0) timeMax = Mathf.Max(timeMax, FBEconomy.buyTotal);
            if (timeT) { timeT.text = t > 0 ? $"{Mathf.CeilToInt(t)}s" : ""; timeT.color = t > 0 && t <= 5 ? RedT : FBKit.Txt; }
            if (timeBar) { timeBar.anchorMax = new Vector2(t > 0 ? Mathf.Clamp01(t / timeMax) : 0, 1); timeBar.GetComponent<Image>().color = t <= 5 ? RedT : FBKit.Gold; }
        }

        // ---------------------------------------------------------------- moldura fixa (montada uma vez)
        static void Build()
        {
            var c = FBUI.Canvas("Loja", 30); go = c.gameObject; cg = go.AddComponent<CanvasGroup>();
            go.AddComponent<FBShopTicker>();
            FBUI.Panel(c.transform, "fundo", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, .62f));
            var fr = FBUI.Panel(c.transform, "moldura", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(FW, FH), FBKit.Bg);
            var o = fr.gameObject.AddComponent<Outline>(); o.effectColor = FBKit.Line; o.effectDistance = new Vector2(1, -1);
            frame = fr.rectTransform;
            var top = FBKit.At(frame, "acento", 0, 0, FW, 3).gameObject.AddComponent<Image>(); top.color = FBKit.Gold; top.raycastTarget = false;

            // topo
            FBKit.T(frame, "LOJA", 36, 28, 14, 300, 44, TextAnchor.MiddleLeft, true, FBKit.Gold);
            phaseT = FBKit.T(frame, "", 12, 30, 56, 420, 18, TextAnchor.MiddleLeft, true, FBKit.Dim);
            timeT = FBKit.T(frame, "", 28, FW / 2 - 150, 12, 300, 38, TextAnchor.MiddleCenter, true);
            var tb = FBKit.Box(frame, "tempo", FW / 2 - 150, 54, 300, 6, new Color(1, 1, 1, .1f), false);
            timeBar = FBUI.Rect(tb.transform, "barra", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); timeBar.gameObject.AddComponent<Image>().color = FBKit.Gold; timeBar.GetComponent<Image>().raycastTarget = false;
            FBKit.T(frame, "SALDO", 12, FW - 330, 12, 300, 18, TextAnchor.MiddleRight, true, FBKit.Dim);
            moneyT = FBKit.T(frame, "", 34, FW - 430, 28, 400, 40, TextAnchor.MiddleRight, true, Green);
            var sep = FBKit.At(frame, "linha", 24, 84, FW - 48, 1).gameObject.AddComponent<Image>(); sep.color = FBKit.Line; sep.raycastTarget = false;

            // colunas
            tabsR = FBKit.At(frame, "categorias", 24, 100, 220, 590);
            var gbox = FBKit.Box(frame, "vitrine", 260, 100, 742, 590, Panel); gbox.raycastTarget = true; gbox.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("cartas", typeof(RectTransform)); gridR = (RectTransform)content.transform; gridR.SetParent(gbox.transform, false);
            gridR.anchorMin = new Vector2(0, 1); gridR.anchorMax = new Vector2(1, 1); gridR.pivot = new Vector2(.5f, 1); gridR.sizeDelta = Vector2.zero;
            var gl = content.AddComponent<GridLayoutGroup>(); gl.cellSize = new Vector2(232, 176); gl.spacing = new Vector2(10, 10); gl.padding = new RectOffset(10, 10, 10, 10);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 3;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            gridScroll = gbox.gameObject.AddComponent<ScrollRect>(); gridScroll.content = gridR; gridScroll.horizontal = false; gridScroll.scrollSensitivity = 40; gridScroll.movementType = ScrollRect.MovementType.Clamped;
            detailR = FBKit.At(frame, "detalhes", 1018, 100, 438, 590);
            loadR = FBKit.At(frame, "equipamento", 24, 706, 1112, 88);

            FBKit.TextTile(frame, "FECHAR   [B]", 1152, 706, 304, 88, false, () => Show(false), 18);
            // [parceiro] 10x10 paga diferente do 4X4 (FBModo10x10: vitória 2500, derrota 1400 + 500 por derrota seguida, até 3400)
            string fimRodada = FBSession.mode == "10x10" ? $"VITÓRIA +${2500}  ·  DERROTA +${1400} A ${3400}" : $"FIM DA RODADA +${FBEconomy.BASE} OU MAIS";
            FBKit.T(frame, $"ABATE +${FBEconomy.KILL}  ·  HEADSHOT +${FBEconomy.KILL + FBEconomy.HEAD}  ·  {fimRodada}  ·  QUEM SOBREVIVE MANTÉM AS ARMAS  ·  QUEM MORRE PERDE TUDO",
                11, 24, 798, FW - 48, 18, TextAnchor.MiddleCenter, true, FBKit.Dim);
        }

        // ---------------------------------------------------------------- conteúdo (refeito a cada compra / mudança de saldo)
        public static void Refresh()
        {
            if (!go || !FBMatch.Player || FBMatch.I == null) return;
            var p = FBMatch.Player; bool ars = Arsenal;
            moneyT.text = ars ? "GRÁTIS" : $"$ {FBEconomy.Money(p)}";
            phaseT.text = ars ? "ARSENAL DO TREINO · PEGUE QUALQUER ARMA" : FBEconomy.buying ? "FASE DE COMPRA · ESCOLHA SEU EQUIPAMENTO" : "FORA DA FASE DE COMPRA";
            cats = ars ? FBEconomy.ArsenalItems().GroupBy(i => i.cls).Select(g => (CLS.TryGetValue(g.Key ?? "", out var n) ? n : (g.Key ?? "").ToUpper(), "", g.ToList())).ToList() : FBEconomy.ShopData();
            if (cats.Count == 0) return;
            if (selCat == null || !cats.Any(c => c.cat == selCat)) selCat = cats[0].cat;
            var items = cats.First(c => c.cat == selCat).items;
            if (selId == null || !items.Any(i => i.id == selId)) selId = (Best(items) ?? items.FirstOrDefault(i => !i.own) ?? items[0]).id;
            hoverId = null;
            float scroll = gridR ? gridR.anchoredPosition.y : 0;
            Clear(tabsR); Clear(gridR); Clear(loadR);
            Tabs(p); Grid(p, items); Loadout(p); Detail(p);
            gridR.anchoredPosition = new Vector2(0, scroll);
        }
        static void Clear(Transform t) { for (int i = t.childCount - 1; i >= 0; i--) Object.Destroy(t.GetChild(i).gameObject); }
        static FBEconomy.Item Find(string id) { if (cats == null || id == null) return null; foreach (var c in cats) foreach (var i in c.items) if (i.id == id) return i; return null; }
        static bool Afford(FBEconomy.Item it) => Arsenal || FBEconomy.Money(FBMatch.Player) >= it.price;
        // a arma mais cara que cabe no bolso e você ainda não tem (selo "MELHOR P/ SEU SALDO")
        static FBEconomy.Item Best(List<FBEconomy.Item> items) => Arsenal ? null : items.Where(i => !i.gear && !i.own && Afford(i)).OrderByDescending(i => i.price).FirstOrDefault();

        static void Tabs(FBCharacter p)
        {
            float y = 0;
            foreach (var (cat, _, items) in cats)
            {
                bool sel = cat == selCat; string c2 = cat;
                var b = FBKit.Tile(tabsR, "cat_" + cat, 0, y, 220, 84, sel, () => { selCat = c2; selId = null; if (gridR) gridR.anchoredPosition = Vector2.zero; Refresh(); });
                if (sel) { var a = FBKit.At(b.transform, "acento", 0, 0, 4, 84).gameObject.AddComponent<Image>(); a.color = FBKit.Gold; a.raycastTarget = false; }
                FBKit.T(b.transform, cat, 14, 16, 12, 200, 20, TextAnchor.MiddleLeft, true, sel ? Color.white : FBKit.Txt);
                bool has = items.Any(i => i.own);
                FBKit.T(b.transform, $"{items.Count} ITENS", 10, 16, 32, 120, 16, TextAnchor.MiddleLeft, true, FBKit.Dim);
                if (has) { var dot = FBKit.Box(b.transform, "tem", 196, 14, 8, 8, Green, false); dot.sprite = FBUI.CircleSprite; }
                var top = items.Where(i => !i.gear).OrderByDescending(i => i.price).FirstOrDefault();
                var tex = top != null ? FBIcons.Weapon(top.id) : null;
                if (tex) { var ri = FBKit.At(b.transform, "img", 100, 40, 110, 40).gameObject.AddComponent<RawImage>(); ri.texture = tex; ri.raycastTarget = false; ri.color = sel ? Color.white : new Color(1, 1, 1, .55f); }
                else FBKit.T(b.transform, cat == "EQUIPAMENTO" ? "COLETE · CAPACETE · KIT" : "", 10, 16, 52, 200, 16, TextAnchor.MiddleLeft, true, FBKit.Dim);
                y += 92;
            }
        }

        static void Grid(FBCharacter p, List<FBEconomy.Item> items)
        {
            var wu = p.GetComponent<FBWeaponUser>(); var best = Best(items);
            foreach (var it in items)
            {
                var item = it; bool sel = it.id == selId, can = Afford(it), inHand = !it.gear && wu.Cur != null && wu.Cur.id == it.id;
                var g = new GameObject("carta_" + it.id, typeof(RectTransform), typeof(Image), typeof(CanvasGroup)); g.transform.SetParent(gridR, false);
                var im = g.GetComponent<Image>(); Color baseC = sel ? new Color(1, .74f, .12f, .14f) : it.own ? GreenBg : FBKit.Card; im.color = baseC;
                var ol = g.AddComponent<Outline>(); ol.effectColor = sel ? new Color(1, .74f, .12f, .95f) : FBKit.Line; ol.effectDistance = sel ? new Vector2(2, -2) : new Vector2(1, -1);
                g.GetComponent<CanvasGroup>().alpha = can || it.own ? 1 : .5f;
                var t = g.transform;
                FBKit.T(t, it.name, 15, 12, 10, 208, 20, TextAnchor.MiddleLeft, true);
                FBKit.T(t, it.gear ? "EQUIPAMENTO" : CLS1.TryGetValue(it.cls ?? "", out var cn) ? cn : it.sub, 10, 12, 30, 140, 14, TextAnchor.MiddleLeft, true, FBKit.Dim);
                string badge = inHand ? "NA MÃO" : it.own && !it.rebuy ? (it.gear ? "COMPLETO" : "NO SLOT") : it == best ? "MELHOR P/ SEU SALDO" : it.best && !it.own ? "ECONOMIZE" : null;
                if (badge != null)
                {
                    var bc = inHand ? FBKit.Gold : it.own ? Green : new Color(.45f, .7f, 1f);
                    var bb = FBKit.Box(t, "selo", 232 - 12 - (badge.Length * 6.4f + 14), 30, badge.Length * 6.4f + 14, 16, new Color(bc.r, bc.g, bc.b, .18f), false);
                    FBKit.T(bb.transform, badge, 9, 0, 0, badge.Length * 6.4f + 14, 16, TextAnchor.MiddleCenter, true, bc);
                }
                var tex = !it.gear ? FBIcons.Weapon(it.id) : null;
                if (tex) { var ri = FBKit.At(t, "img", 16, 50, 200, 86).gameObject.AddComponent<RawImage>(); ri.texture = tex; ri.raycastTarget = false; }
                else FBKit.T(t, GearGlyph(it.id), 30, 12, 54, 208, 76, TextAnchor.MiddleCenter, true, new Color(1, 1, 1, .25f));
                string price = Arsenal ? "PEGAR" : it.own && !it.rebuy ? (it.gear ? "OK" : "EQUIPAR") : $"$ {it.price}";
                FBKit.T(t, price, 19, 12, 142, 120, 26, TextAnchor.MiddleLeft, true, Arsenal || (it.own && !it.rebuy) ? FBKit.Gold : can ? Green : RedT);
                if (it.note != null) FBKit.T(t, it.note, 11, 120, 142, 100, 26, TextAnchor.MiddleRight, true, FBKit.Dim);
                else if (!it.gear && GameData.Weapons.TryGetValue(it.id, out var d)) FBKit.T(t, $"DANO {Mathf.RoundToInt(Stats(it.id).dmg)}", 11, 120, 142, 100, 26, TextAnchor.MiddleRight, true, FBKit.Dim);

                var b = g.AddComponent<Button>(); b.targetGraphic = im; b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => { if (selId == item.id) DoBuy(item); else { selId = item.id; FBAudio.Synth("hover"); Refresh(); } });
                var tr = g.AddComponent<EventTrigger>();
                void E(EventTriggerType ty, System.Action a) { var e = new EventTrigger.Entry { eventID = ty }; e.callback.AddListener(_ => a()); tr.triggers.Add(e); }
                E(EventTriggerType.PointerEnter, () => { if (!sel) im.color = FBKit.CardHi; hoverId = item.id; Detail(FBMatch.Player); });
                E(EventTriggerType.PointerExit, () => { im.color = baseC; if (hoverId == item.id) { hoverId = null; Detail(FBMatch.Player); } });
            }
        }
        static string GearGlyph(string id) => id == "colete" ? "COLETE" : id == "capacete" ? "CAPACETE" : id == "kit" ? "COLETE + CAPACETE" : id == "medkit" ? "+ KIT MÉDICO" : "";

        static void DoBuy(FBEconomy.Item it)
        {
            if (it == null) return; var p = FBMatch.Player; if (!p || !p.alive) return;
            if (!Arsenal && !FBEconomy.buying) { FBAudio.Synth("deny"); return; }
            if (!Arsenal && !(it.own && !it.rebuy) && !Afford(it)) { FBAudio.Synth("deny"); return; }
            FBEconomy.Buy(it); Refresh();
        }

        // ---------------------------------------------------------------- detalhes (item sob o mouse ou selecionado)
        struct WS { public float dmg, rpm, prec, mob, head, ap; public int mag; }
        static WS Stats(string id)
        {
            // r51: números do modelo de dano que o tiro usa (FBDamageModel) — dano no peito × bagos, cabeça e penetração de colete
            if (GameData.Weapons.TryGetValue(id, out var wd) && FBDamageModel.Of(wd) is FBDamageModel.Stat ms)
                return new WS { dmg = ms.dmg * ms.pellets, rpm = ms.rpm, prec = Mathf.Clamp(125 - ms.acc * 55 - (ms.pellets > 1 ? 35 : 0), 5, 100), mob = ms.move * 100, mag = wd.mag, head = ms.dmg * ms.hs, ap = ms.ap * 100 };
            var st = Json.O(Json.O(GameData.WeaponsRaw, id), "stats");
            float rpm = Json.F(st, "roundsPerMinutes", 60 / Mathf.Max(.01f, Json.F(st, "secondsBetweenShots", .1f)));
            return new WS
            {
                dmg = Json.F(st, "baseDamage", 25) * Mathf.Max(1, Json.F(st, "shotCount", 1)), rpm = rpm,
                prec = Mathf.Clamp((100 - Json.F(st, "spread", .25f) * 60) / 105, .05f, 1) * 100, mob = Json.F(st, "multiplierMovementSpeed", 1) * 100,
                mag = GameData.Weapons.TryGetValue(id, out var d) ? d.mag : 0,
            };
        }
        static void Detail(FBCharacter p)
        {
            if (!detailR || !p) return; Clear(detailR);
            var it = Find(hoverId) ?? Find(selId); if (it == null) return;
            var wu = p.GetComponent<FBWeaponUser>(); const float W = 438;
            FBKit.Box(detailR, "fundo", 0, 0, W, 590, Panel);
            FBKit.Section(detailR, hoverId != null && hoverId != selId ? "PRÉVIA" : "SELECIONADO", 14, 14, 300);
            var art = FBKit.Box(detailR, "arte", 14, 42, W - 28, 168, new Color(1, 1, 1, .035f));
            var glow = FBKit.At(art.transform, "brilho", (W - 28) / 2 - 150, 4, 300, 160).gameObject.AddComponent<Image>(); glow.sprite = FBUI.CircleSprite; glow.color = new Color(1, .74f, .12f, .07f); glow.raycastTarget = false;
            var tex = !it.gear ? FBIcons.Weapon(it.id, 512, 224) : null;
            if (tex) { var ri = FBKit.At(art.transform, "img", 16, 10, W - 60, 148).gameObject.AddComponent<RawImage>(); ri.texture = tex; ri.raycastTarget = false; }
            else FBKit.T(art.transform, GearGlyph(it.id), 30, 0, 0, W - 28, 168, TextAnchor.MiddleCenter, true, new Color(1, 1, 1, .3f));
            FBKit.T(detailR, it.name, 24, 14, 220, W - 28, 30, TextAnchor.MiddleLeft, true);

            float y = 256;
            if (!it.gear && GameData.Weapons.TryGetValue(it.id, out var d))
            {
                int slot = Mathf.Clamp(d.slot, 0, 2); var cur = wu.slots[slot]; bool same = cur != null && cur.id == it.id;
                string line = $"{(CLS1.TryGetValue(d.cls ?? "", out var cn) ? cn : d.cls?.ToUpper())}  ·  SLOT {slot + 1} ({SLOT[slot]})  ·  {(d.auto ? "AUTOMÁTICA" : "SEMIAUTOMÁTICA")}";
                FBKit.T(detailR, line, 11, 14, y, W - 28, 16, TextAnchor.MiddleLeft, true, FBKit.Dim); y += 20;
                FBKit.T(detailR, same ? "VOCÊ JÁ TEM ESTA ARMA" : cur != null ? $"SUBSTITUI: {cur.name.ToUpper()}" : "SLOT VAZIO", 11, 14, y, W - 28, 16, TextAnchor.MiddleLeft, true, same ? Green : cur != null ? FBKit.Gold : FBKit.Dim); y += 28;
                var s = Stats(it.id); WS? c = cur != null && !same ? Stats(cur.id) : null;
                Stat("DANO", s.dmg, c?.dmg, 130, "0", ref y); Stat("CADÊNCIA", s.rpm, c?.rpm, 1000, "0' RPM'", ref y); Stat("PRECISÃO", s.prec, c?.prec, 100, "0'%'", ref y);
                Stat("MOBILIDADE", s.mob, c?.mob, 115, "0'%'", ref y); Stat("PENTE", s.mag, c?.mag, 100, "0", ref y);
                if (s.head > 0) { FBKit.T(detailR, $"CABEÇA {s.head:0}  ·  ATRAVESSA COLETE {s.ap:0}%  ·  DANO NO PEITO, SEM COLETE, PERTO", 10, 14, y, W - 28, 16, TextAnchor.MiddleLeft, true, FBKit.Dim); y += 20; }   // r51
                if (c != null) { FBKit.Box(detailR, "leg1", 14, y + 4, 10, 4, new Color(1, 1, 1, .35f), false); FBKit.T(detailR, $"= {cur.name.ToUpper()} (SUA)", 9, 28, y - 2, 300, 16, TextAnchor.MiddleLeft, true, FBKit.Dim); }
            }
            else
            {
                FBKit.T(detailR, it.sub ?? "", 13, 14, y, W - 28, 20, TextAnchor.MiddleLeft, true, FBKit.Dim); y += 30;
                string desc = it.id switch
                {
                    "colete" => "Protege peito, barriga e braços: a vida leva só a parte do dano que a arma atravessa (pistola ~50%, fuzil 70–80%) e o colete gasta. Pernas nunca são protegidas.",
                    "capacete" => "Protege a cabeça pela mesma regra: tiro de pistola ou M4 na cabeça deixa de matar de uma vez; AK, fuzil de batalha e Desert Eagle ainda matam.",
                    "kit" => "Colete completo + capacete de uma vez, mais barato do que comprar separado.",
                    "medkit" => $"Cura {FBMed.Heal} de vida (tecla H). Leva alguns segundos e cancela se você correr, pular ou atirar.",
                    _ => "",
                };
                FBKit.T(detailR, desc, 14, 14, y, W - 28, 80, TextAnchor.UpperLeft, false, FBKit.Txt); y += 90;
                if (it.note != null) FBKit.T(detailR, "ATUAL: " + it.note, 12, 14, y, W - 28, 18, TextAnchor.MiddleLeft, true, FBKit.Gold);
            }

            // compra
            bool can = Afford(it), owned = it.own && !it.rebuy;
            string label; bool active;
            if (Arsenal) { label = it.gear ? "INDISPONÍVEL NO ARSENAL" : "PEGAR ARMA"; active = !it.gear; }
            else if (owned) { label = it.gear ? "JÁ EQUIPADO" : "EQUIPAR"; active = !it.gear; }
            else if (!FBEconomy.buying) { label = "FORA DA FASE DE COMPRA"; active = false; }
            else if (!can) { label = $"FALTAM $ {it.price - FBEconomy.Money(p)}"; active = false; }
            else { label = $"COMPRAR  ·  $ {it.price}"; active = true; }
            if (!Arsenal && !owned) FBKit.T(detailR, can ? $"SALDO DEPOIS: $ {FBEconomy.Money(p) - it.price}" : "SALDO INSUFICIENTE", 11, 14, 500, W - 28, 16, TextAnchor.MiddleRight, true, can ? FBKit.Dim : RedT);
            var buyIt = it;
            if (active) FBKit.Primary(detailR, label, 14, 520, W - 28, 56, () => DoBuy(buyIt));
            else { var bx = FBKit.Box(detailR, "bloq", 14, 520, W - 28, 56, new Color(1, 1, 1, .05f)); FBKit.T(bx.transform, label, 16, 0, 0, W - 28, 56, TextAnchor.MiddleCenter, true, owned ? Green : RedT); }
        }
        static void Stat(string name, float v, float? cur, float max, string fmt, ref float y)
        {
            const float BX = 120, BW = 230;
            FBKit.T(detailR, name, 11, 14, y, 100, 18, TextAnchor.MiddleLeft, true, FBKit.Dim);
            var bg = FBKit.Box(detailR, "st_" + name, BX, y + 6, BW, 6, new Color(1, 1, 1, .08f), false);
            float k = Mathf.Clamp01(v / max);
            if (cur.HasValue) { var cr = FBKit.At(bg.transform, "sua", 0, 0, BW * Mathf.Clamp01(cur.Value / max), 6).gameObject.AddComponent<Image>(); cr.color = new Color(1, 1, 1, .35f); cr.raycastTarget = false; }
            var f = FBKit.At(bg.transform, "esta", 0, cur.HasValue ? 2 : 0, BW * k, cur.HasValue ? 4 : 6).gameObject.AddComponent<Image>(); f.color = FBKit.Gold; f.raycastTarget = false;
            string txt = v.ToString(fmt);
            if (cur.HasValue) { float dv = v - cur.Value; if (Mathf.Abs(dv) >= .5f) txt = $"{txt}  <color={(dv > 0 ? "#5ce680" : "#f2554d")}>{(dv > 0 ? "+" : "−")}{Mathf.Abs(dv):0}</color>"; }
            var t = FBKit.T(detailR, txt, 12, BX + BW + 8, y, 438 - BX - BW - 22, 18, TextAnchor.MiddleRight, true); t.supportRichText = true;
            y += 26;
        }

        // ---------------------------------------------------------------- seu equipamento
        static void Loadout(FBCharacter p)
        {
            var wu = p.GetComponent<FBWeaponUser>(); float x = 0;
            for (int i = 0; i < 3; i++)
            {
                var d = wu.slots[i]; bool cur = wu.cur == i;
                var b = FBKit.Box(loadR, "slot" + i, x, 0, 218, 88, cur ? new Color(1, .74f, .12f, .12f) : FBKit.Card);
                if (cur) { var a = FBKit.At(b.transform, "acento", 0, 85, 218, 3).gameObject.AddComponent<Image>(); a.color = FBKit.Gold; a.raycastTarget = false; }
                FBKit.T(b.transform, $"{i + 1}  {SLOT[i]}", 10, 10, 8, 120, 14, TextAnchor.MiddleLeft, true, FBKit.Dim);
                if (d != null)
                {
                    var tex = FBIcons.Weapon(d.id); if (tex) { var ri = FBKit.At(b.transform, "img", 96, 18, 114, 50).gameObject.AddComponent<RawImage>(); ri.texture = tex; ri.raycastTarget = false; }
                    FBKit.T(b.transform, d.name.ToUpper(), 13, 10, 28, 130, 34, TextAnchor.MiddleLeft, true);
                    FBKit.T(b.transform, $"{wu.ammo[i]} / {wu.reserve[i]}", 11, 10, 66, 120, 14, TextAnchor.MiddleLeft, true, FBKit.Dim);
                }
                else FBKit.T(b.transform, "VAZIO", 13, 10, 34, 200, 24, TextAnchor.MiddleLeft, true, new Color(1, 1, 1, .25f));
                x += 226;
            }
            void Gear(string title, string val, float fill, bool ok)
            {
                var b = FBKit.Box(loadR, "eq_" + title, x, 0, 156, 88, ok ? GreenBg : FBKit.Card);
                FBKit.T(b.transform, title, 10, 10, 8, 140, 14, TextAnchor.MiddleLeft, true, FBKit.Dim);
                FBKit.T(b.transform, val, 20, 10, 28, 140, 30, TextAnchor.MiddleLeft, true, ok ? Green : new Color(1, 1, 1, .35f));
                var bg = FBKit.Box(b.transform, "barra", 10, 70, 136, 4, new Color(1, 1, 1, .08f), false);
                var f = FBKit.At(bg.transform, "f", 0, 0, 136 * Mathf.Clamp01(fill), 4).gameObject.AddComponent<Image>(); f.color = ok ? Green : FBKit.Gold; f.raycastTarget = false;
                x += 164;
            }
            Gear("COLETE", p.armor > 0 ? $"{Mathf.RoundToInt(p.armor)}%" : "SEM", p.armor / 100f, p.armor > 0);
            Gear("CAPACETE", p.helmet ? "SIM" : "SEM", p.helmet ? 1 : 0, p.helmet);
            Gear("KIT MÉDICO", FBMed.inf ? "∞" : $"{FBMed.kits}/{FBMed.Max}", FBMed.inf ? 1 : FBMed.kits / (float)Mathf.Max(1, FBMed.Max), FBMed.inf || FBMed.kits > 0);
        }

        // abertura suave (fade + leve escala)
        public static void Tick(float dt)
        {
            if (!Open || !cg || !frame) return; openT += dt; float k = 1 - Mathf.Exp(-openT * 14);
            cg.alpha = k; frame.localScale = Vector3.one * Mathf.Lerp(.97f, 1, k);
        }
    }
    public class FBShopTicker : MonoBehaviour
    {
        // [parceiro] v1 (thread de física): tempo do Update medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
        static readonly int __fbt_Update = FBTempo.Id("FBShopTicker.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt() => FBShop.Tick(Time.unscaledDeltaTime);
    }
}
