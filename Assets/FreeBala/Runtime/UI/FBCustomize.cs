// TELA DE PERSONAGEM (reconstruída, estilo do lobby: dourado/laranja sobre escuro, RussoOne/LiberationSans): vitrine 3D no centro
// (FBCustomizeStage), painel esquerdo com duas abas — PERSONAGENS (grade com miniaturas renderizadas do modelo, filtros de sexo e
// origem, favoritos, busca sem acento) e VISUAL (guarda-roupa por categoria: cabeça, cabelo, tronco, pernas, pés, acessórios, pele,
// com as cores de cada peça) — e painel direito com o personagem em destaque (nome, etiquetas, USAR, sortear, dançar, fixo/aleatório).
// Trocar peça/cor muda o modelo NA HORA (mesmo esqueleto, sem recarregar o prefab) e já salva o visual daquele personagem; USAR fixa o
// personagem (FBSession.skin/look → partida e rede). Layout responsivo: 16:9, 21:9, 4:3 e celular em retrato (palco em cima, gaveta
// embaixo); toque: arrastar gira, pinça dá zoom. Teclado: ← → navega, ENTER usa, ESC volta.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace FreeBala
{
    public class FBCustomize : MonoBehaviour
    {
        public static FBCustomize I { get; private set; }
        // grupos do guarda-roupa: categorias da ficha + enquadramento da câmera
        public static readonly (string key, string label, string[] cats, FBCustomizeStage.Frame frame)[] GROUPS = {
            ("cabeca", "CABEÇA", new[] { "hat", "face" }, FBCustomizeStage.Frame.Face),
            ("cabelo", "CABELO", new[] { "head" }, FBCustomizeStage.Frame.Bust),
            ("tronco", "TRONCO", new[] { "top", "vest" }, FBCustomizeStage.Frame.Torso),
            ("pernas", "PERNAS", new[] { "bottom" }, FBCustomizeStage.Frame.Legs),
            ("pes", "PÉS", new[] { "shoes" }, FBCustomizeStage.Frame.Feet),
            ("acess", "ACESSÓRIOS", new[] { "acc", "hands" }, FBCustomizeStage.Frame.Torso),
            ("pele", "PELE", new string[0], FBCustomizeStage.Frame.Bust),
        };
        static readonly Dictionary<string, string> CAT_NAMES = new() { ["hat"] = "Chapéu/Capacete", ["face"] = "Rosto", ["head"] = "Cabeça/Cabelo", ["top"] = "Camisa", ["vest"] = "Colete", ["bottom"] = "Calça/Bermuda", ["shoes"] = "Calçado", ["acc"] = "Acessórios", ["hands"] = "Luvas" };
        static readonly Color Txt = new(.96f, .97f, 1f), Dim = new(.8f, .87f, 1f, .6f), Gold2 = new(.36f, .66f, 1f), Orange = new(.13f, .62f, .3f), PanelC = new(.02f, .055f, .12f, .88f), Card = new(.05f, .11f, .23f, .92f), CardHi = new(.1f, .27f, .56f, .95f),   // r62: cores da bandeira (azul/branco/verde), sem cinza
            Navy = new(.07f, .15f, .3f, .85f), NavyHi = new(.1f, .22f, .42f, .95f);

        // ---- estado
        public FBCustomizeStage stage; public FBCustomizeThumbs thumbs; public string sel; public string tab = "chars", group = "tronco";
        public string sexF = "all"; public FBCustomizeData.Origin originF = FBCustomizeData.Origin.All; public string query = "";
        Vector2 logical = new(1600, 900); System.Action onClose; Camera cam; Canvas canvas; RectTransform root, left, right, top, body; FBCustomizeFx.Aspect aspect; Vector2Int screen;
        readonly Dictionary<string, (RectTransform rt, Image bg, RawImage img, Text initials, Outline ring, GameObject fav, GameObject used)> cards = new();
        readonly HashSet<string> asked = new(); RectTransform gridView, gridContent; Text countT, toastT; float visT, toastUntil; InputField search;
        List<FBCustomizeData.Entry> list = new();

        public static FBCustomize Open(Camera cam, System.Action onClose, string tab = "chars")
        {
            if (I) Destroy(I.gameObject);
            var g = new GameObject("FB_Personagem"); var c = g.AddComponent<FBCustomize>(); c.cam = cam; c.onClose = onClose; c.tab = tab; I = c;
            c.stage = FBCustomizeStage.Create(cam); c.stage.transform.SetParent(g.transform, false);
            c.thumbs = FBCustomizeThumbs.Create(g.transform);
            c.sel = FBCustomizeData.Find(FBSession.skin) != null ? FBSession.skin : FBCustomizeData.Roster.FirstOrDefault()?.id;
            c.stage.Show(c.sel, FBCustomizeData.LookFor(c.sel));
            c.Build(true); return c;
        }
        void OnDestroy() { if (I == this) I = null; if (canvas) Destroy(canvas.gameObject); }

        public void Close()
        {
            FBAudio.Synth("hover");
            FBCustomizeFx.SlideOut(left, aspect == FBCustomizeFx.Aspect.Portrait ? new Vector2(0, -120) : new Vector2(-80, 0));
            FBCustomizeFx.SlideOut(right, aspect == FBCustomizeFx.Aspect.Portrait ? new Vector2(0, -60) : new Vector2(80, 0));
            FBCustomizeFx.SlideOut(top, new Vector2(0, 40), .2f, () => { var cb = onClose; Destroy(gameObject); cb?.Invoke(); });
        }

        // ================================================================ MONTAGEM
        void Build(bool animate)
        {
            if (canvas) Destroy(canvas.gameObject); cards.Clear(); asked.Clear();
            aspect = FBCustomizeFx.Current; screen = new Vector2Int(Screen.width, Screen.height); bool portrait = aspect == FBCustomizeFx.Aspect.Portrait;
            canvas = FBUI.Canvas("Personagem", 6); var sc = canvas.GetComponent<CanvasScaler>(); sc.referenceResolution = portrait ? new Vector2(900, 1600) : new Vector2(1600, 900); sc.matchWidthOrHeight = .5f;
            root = (RectTransform)canvas.transform;
            // área segura (entalhe/câmera/barra de gestos do celular): tudo da tela fica dentro dela; o palco transparente continua na tela toda
            var sa = SafeRect(); var safe = FBUI.Rect(root, "area_segura", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); safe.anchorMin = sa.min; safe.anchorMax = sa.max; safe.offsetMin = safe.offsetMax = Vector2.zero;
            // palco: área transparente que recebe arrastar/roda/clique duplo (atrás dos painéis)
            var st = FBUI.Panel(root, "palco", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0)); st.gameObject.AddComponent<FBCustomizeStageInput>().stage = stage;
            st.transform.SetAsFirstSibling(); root = safe;
            float lw = aspect == FBCustomizeFx.Aspect.Narrow ? 430 : 480, rw = aspect == FBCustomizeFx.Aspect.Narrow ? 330 : 380, topH = 72;
            // topo
            top = FBUI.Panel(root, "topo", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -topH / 2), new Vector2(0, topH), new Color(.015f, .03f, .07f, .93f)).rectTransform;
            var bk = Btn(top, "‹ VOLTAR", new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(98, 0), new Vector2(164, 46), Close);
            FBUI.Label(top, "PERSONAGEM", portrait ? 30 : 34, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 6), new Vector2(600, 40), TextAnchor.MiddleCenter, true, Color.white);
            FBUI.Label(top, $"{FBCustomizeData.Roster.Count} OPERADORES · {FBCustomizeData.Roster.Count(e => e.fem)} FEMININAS · {FBCustomizeData.Roster.Count(e => !e.fem)} MASCULINOS", 12, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -22), new Vector2(700, 16), TextAnchor.MiddleCenter, false, Dim);
            var line = FBUI.Panel(top, "linha", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(0, 2), new Color(Gold2.r, Gold2.g, Gold2.b, .35f)); line.raycastTarget = false;
            // painéis
            if (portrait)
            {
                left = FBUI.Panel(root, "gaveta", new Vector2(0, 0), new Vector2(1, .44f), Vector2.zero, Vector2.zero, PanelC).rectTransform;
                right = FBUI.Panel(root, "destaque", new Vector2(0, .44f), new Vector2(1, .44f), new Vector2(0, 74), new Vector2(0, 148), new Color(.015f, .04f, .09f, .8f)).rectTransform;
            }
            else
            {
                left = FBUI.Panel(root, "lista", new Vector2(0, 0), new Vector2(0, 1), new Vector2(lw / 2 + 16, -topH / 2 + 2), new Vector2(lw, -topH - 28), PanelC).rectTransform;
                right = FBUI.Panel(root, "destaque", new Vector2(1, 0), new Vector2(1, 1), new Vector2(-rw / 2 - 16, -topH / 2 + 2), new Vector2(rw, -topH - 28), PanelC).rectTransform;
            }
            left.gameObject.AddComponent<Outline>().effectColor = FBKit.Line; right.gameObject.AddComponent<Outline>().effectColor = FBKit.Line; FBKit.Round(left.GetComponent<Image>(), 12); FBKit.Round(right.GetComponent<Image>(), 12);
            // dica do palco
            var hint = FBUI.Label(root, Application.isMobilePlatform ? "ARRASTE PARA GIRAR · PINÇA PARA ZOOM · TOQUE DUPLO DANÇA" : "ARRASTE PARA GIRAR · RODA = ZOOM · CLIQUE DUPLO DANÇA · ← → NAVEGAR · ENTER USAR", 12,
                portrait ? new Vector2(.5f, .44f) : new Vector2(.5f, 0), portrait ? new Vector2(.5f, .44f) : new Vector2(.5f, 0), portrait ? new Vector2(0, 160) : new Vector2((lw - rw) / 2, 22), new Vector2(760, 18), TextAnchor.MiddleCenter, false, Dim);
            toastT = FBUI.Label(root, "", 18, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(portrait ? 0 : (lw - rw) / 2, -topH - 34), new Vector2(700, 30), TextAnchor.MiddleCenter, true, Gold2); toastT.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0, -2);
            // retângulo livre para o personagem (em fração da tela)
            var size = logical = Vector2.Scale(Logical(sc), sa.size);
            stage.view = portrait ? new Rect(.08f, .44f + 160 / size.y, .84f, 1 - .44f - (160 + topH + 30) / size.y)
                                  : new Rect((lw + 30) / size.x, 50 / size.y, 1 - (lw + rw + 60) / size.x, 1 - (topH + 50 + 40) / size.y);
            stage.view = new Rect(sa.xMin + stage.view.x * sa.width, sa.yMin + stage.view.y * sa.height, stage.view.width * sa.width, stage.view.height * sa.height);
            BuildLeft(); BuildRight();
            if (animate)
            {
                FBCustomizeFx.SlideIn(top, new Vector2(0, 50), .3f);
                FBCustomizeFx.SlideIn(left, portrait ? new Vector2(0, -160) : new Vector2(-90, 0), .38f, .05f);
                FBCustomizeFx.SlideIn(right, portrait ? new Vector2(0, -80) : new Vector2(90, 0), .38f, .1f);
                FBCustomizeFx.Fade(hint, 0, 1, .5f, .4f);
            }
        }

        /// <summary>Screen.safeArea normalizada (0..1); no PC é a tela toda.</summary>
        static Rect SafeRect()
        {
            float w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height); var r = Screen.safeArea;
            if (r.width < 10 || r.height < 10) return new Rect(0, 0, 1, 1);
            return new Rect(r.x / w, r.y / h, r.width / w, r.height / h);
        }
        static Vector2 Logical(CanvasScaler sc)
        {
            float w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height), r = sc.referenceResolution.x, q = sc.referenceResolution.y;
            float k = sc.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand ? Mathf.Min(w / r, h / q) : Mathf.Pow(2, Mathf.Lerp(Mathf.Log(w / r, 2), Mathf.Log(h / q, 2), sc.matchWidthOrHeight)); return new Vector2(w / k, h / k);   // r62: modo EXPAND
        }
        // ---- helpers de interface (mesma linguagem visual do lobby)
        static Button Btn(Transform p, string text, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, System.Action act, bool primary = false, int fs = 0)
        {
            var b = FBUI.Button(p, text, aMin, aMax, pos, size, () => { FBAudio.Synth("hover"); act?.Invoke(); }, primary);
            var t = b.GetComponentInChildren<Text>(); if (fs > 0) t.fontSize = fs; else if (!primary) t.fontSize = 16; t.supportRichText = true;
            if (!primary) b.GetComponent<Image>().color = Navy; else b.GetComponent<Image>().color = FBKit.Flag; FBKit.Round(b.GetComponent<Image>(), 8);
            return b;
        }
        /// <summary>Linha de layout (Vertical/Horizontal) com altura fixa.</summary>
        static RectTransform Row(Transform p, string n, float h, bool horiz = true, float spacing = 6)
        {
            var r = new GameObject(n, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(p, false);
            var le = r.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = h; le.minHeight = h; le.flexibleHeight = 0;   // sem isso o HorizontalLayoutGroup (force expand) se diz flexível e a sobra do painel vai para as linhas
            if (horiz) { var hl = r.gameObject.AddComponent<HorizontalLayoutGroup>(); hl.spacing = spacing; hl.childControlWidth = hl.childControlHeight = true; hl.childForceExpandWidth = true; hl.childForceExpandHeight = true; }
            return r;
        }
        static Button Chip(Transform p, string text, bool on, System.Action act, int fs = 14)
        {
            var im = new GameObject("chip_" + text, typeof(RectTransform), typeof(Image)).GetComponent<Image>(); im.transform.SetParent(p, false);
            im.color = on ? FBKit.BlueDeep : Navy; FBKit.Round(im, 6);
            var b = im.gameObject.AddComponent<Button>(); b.targetGraphic = im; var cb = b.colors; cb.highlightedColor = new Color(1.35f, 1.35f, 1.35f); cb.pressedColor = new Color(.8f, .9f, 1f); b.colors = cb;
            b.onClick.AddListener(() => { FBAudio.Synth("hover"); act?.Invoke(); });
            var t = FBUI.Label(im.transform, text, fs, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-8, 0), TextAnchor.MiddleCenter, true, Color.white); t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.resizeTextForBestFit = true; t.resizeTextMinSize = 9; t.resizeTextMaxSize = fs;
            return b;
        }
        static ScrollRect Scroll(Transform p, out RectTransform content, bool grid, Vector2 cell = default, int cols = 3)
        {
            var sv = new GameObject("rolagem", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect)); var r = (RectTransform)sv.transform; r.SetParent(p, false);
            sv.GetComponent<Image>().color = new Color(0, 0, 0, .18f); var le = sv.AddComponent<LayoutElement>(); le.flexibleHeight = 1; le.minHeight = 80;
            var c = new GameObject("conteudo", typeof(RectTransform)); content = (RectTransform)c.transform; content.SetParent(r, false);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            if (grid) { var g = c.AddComponent<GridLayoutGroup>(); g.cellSize = cell; g.spacing = new Vector2(8, 8); g.padding = new RectOffset(8, 8, 8, 8); g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = cols; g.childAlignment = TextAnchor.UpperCenter; }
            else { var v = c.AddComponent<VerticalLayoutGroup>(); v.spacing = 6; v.padding = new RectOffset(10, 10, 10, 14); v.childControlHeight = true; v.childControlWidth = true; v.childForceExpandHeight = false; v.childForceExpandWidth = true; }
            c.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var s = sv.GetComponent<ScrollRect>(); s.content = content; s.viewport = r; s.horizontal = false; s.scrollSensitivity = 40; s.movementType = ScrollRect.MovementType.Clamped; s.inertia = true; s.decelerationRate = .12f;
            return s;
        }
        void Toast(string s) { if (!toastT) return; toastT.text = s; toastUntil = Time.unscaledTime + 2.4f; FBCustomizeFx.Fade(toastT, 0, 1, .2f); FBCustomizeFx.Pop(toastT.transform, .85f); }

        // ================================================================ PAINEL ESQUERDO
        void BuildLeft()
        {
            foreach (Transform t in left) Destroy(t.gameObject); cards.Clear(); asked.Clear(); gridView = null; gridContent = null;   // cartões são recriados pela aba PERSONAGENS
            var vl = left.GetComponent<VerticalLayoutGroup>(); if (!vl) vl = left.gameObject.AddComponent<VerticalLayoutGroup>();   // (?? não serve: no Editor o GetComponent devolve um "null falso")
            vl.padding = new RectOffset(12, 12, 12, 12); vl.spacing = 8; vl.childControlHeight = vl.childControlWidth = true; vl.childForceExpandHeight = false; vl.childForceExpandWidth = true;
            // abas
            var tabs = Row(left, "abas", 42, true, 6); var w = stage.wardrobe;
            Tab(tabs, "PERSONAGENS", tab == "chars", () => SetTab("chars"));
            Tab(tabs, w && w.Customizable ? "VISUAL" : "VISUAL <size=11>(—)</size>", tab == "look", () => SetTab("look"));
            body = new GameObject("corpo", typeof(RectTransform)).GetComponent<RectTransform>(); body.SetParent(left, false);
            body.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            var bl = body.gameObject.AddComponent<VerticalLayoutGroup>(); bl.spacing = 8; bl.childControlHeight = bl.childControlWidth = true; bl.childForceExpandHeight = false; bl.childForceExpandWidth = true;
            if (tab == "look") BuildLook(); else BuildChars();
        }
        void Tab(Transform p, string text, bool on, System.Action act)
        {
            var b = Chip(p, text, false, act, 16); var im = b.GetComponent<Image>(); im.color = on ? NavyHi : new Color(.03f, .08f, .17f, .7f);
            b.GetComponentInChildren<Text>().color = on ? Gold2 : Dim;
            if (on) { var u = FBUI.Panel(b.transform, "sublinhado", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 2), new Vector2(0, 4), Gold2); u.raycastTarget = false; }
        }
        public void SetTab(string t)
        {
            if (tab == t) return; tab = t;
            stage.SetFrame(t == "look" ? GroupDef(group).frame : FBCustomizeStage.Frame.Full);
            if (t == "look" && !Avail(group)) { var g = GROUPS.FirstOrDefault(x => Avail(x.key)); if (g.key != null) { group = g.key; stage.SetFrame(g.frame); } }
            BuildLeft(); FBCustomizeFx.Fade(body, 0, 1, .22f); FBCustomizeFx.SlideIn(body, new Vector2(0, -16), .26f);
        }

        // ---------------------------------------------------------------- PERSONAGENS
        void BuildChars()
        {
            var R = FBCustomizeData.Roster;
            var r1 = Row(body, "sexo", 32);
            foreach (var (k, t, n) in new[] { ("all", "TODOS", R.Count), ("m", "MASCULINO", R.Count(e => !e.fem)), ("f", "FEMININO", R.Count(e => e.fem)) })
            { var kk = k; Chip(r1, $"{t} <size=11><color=#{(sexF == k ? "ffffff" : "5aa8ff")}>{n}</color></size>", sexF == k, () => { sexF = kk; BuildLeft(); }, 14); }
            var r2 = Row(body, "origem", 28);
            foreach (var (o, t) in new[] { (FBCustomizeData.Origin.All, "TODOS"), (FBCustomizeData.Origin.Original, "REGIONAIS"), (FBCustomizeData.Origin.Npc, "NPC"), (FBCustomizeData.Origin.Fav, "★ FAVORITOS") })
            { var oo = o; Chip(r2, t, originF == o, () => { originF = oo; BuildLeft(); }, 13); }
            // busca
            var sr = Row(body, "busca", 36, false); var sbg = sr.gameObject.AddComponent<Image>(); sbg.color = new Color(.01f, .04f, .1f, .9f); FBKit.Round(sbg, 8);
            FBUI.Label(sr, "⌕", 22, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(20, 0), new Vector2(30, 40), TextAnchor.MiddleCenter, false, Dim);
            var tx = FBUI.Label(sr, query, 18, Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-52, 0), TextAnchor.MiddleLeft, false, Txt); tx.raycastTarget = false; tx.supportRichText = false;
            var ph = FBUI.Label(sr, "Buscar pelo nome…", 17, Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-52, 0), TextAnchor.MiddleLeft, false, new Color(1, 1, 1, .3f)); ph.fontStyle = FontStyle.Italic;
            search = sr.gameObject.AddComponent<InputField>(); search.textComponent = tx; search.placeholder = ph; search.text = query; search.targetGraphic = sbg;
            search.onValueChanged.AddListener(v => { query = v; ApplyFilter(); });
            countT = FBUI.Label(body, "", 12, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, TextAnchor.MiddleLeft, false, Dim); countT.gameObject.AddComponent<LayoutElement>().preferredHeight = 16;
            // grade
            float pw = aspect == FBCustomizeFx.Aspect.Portrait ? logical.x : aspect == FBCustomizeFx.Aspect.Narrow ? 430 : 480; int cols = aspect == FBCustomizeFx.Aspect.Portrait ? 5 : 4;   // 4 colunas: várias linhas visíveis
            float cw = (pw - 24 - 16 - (cols - 1) * 8) / cols; var cell = new Vector2(cw, cw * 1.22f);
            var sv = Scroll(body, out gridContent, true, cell, cols); gridView = (RectTransform)sv.transform;
            foreach (var e in R) MakeCard(e, cell);
            ApplyFilter(); StartCoroutine(CenterSoon());
        }
        IEnumerator CenterSoon() { yield return null; Canvas.ForceUpdateCanvases(); CenterOn(sel); }
        void MakeCard(FBCustomizeData.Entry e, Vector2 cell)
        {
            var bg = new GameObject("c_" + e.id, typeof(RectTransform), typeof(Image)).GetComponent<Image>(); bg.transform.SetParent(gridContent, false); bg.color = Card;
            var rt = (RectTransform)bg.transform;
            var ini = FBUI.Label(rt, Initials(e.name), 30, Vector2.zero, Vector2.one, new Vector2(0, 12), Vector2.zero, TextAnchor.MiddleCenter, true, new Color(1, 1, 1, .12f));
            var img = new GameObject("mini", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>(); img.transform.SetParent(rt, false); var ir = img.rectTransform;
            ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one; ir.offsetMin = new Vector2(2, 2); ir.offsetMax = new Vector2(-2, -2); img.raycastTarget = false; img.enabled = false;
            // faixa do nome
            var fx = FBUI.Panel(rt, "faixa", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 14), new Vector2(0, 28), new Color(0, 0, 0, .62f)); fx.raycastTarget = false;
            var nt = FBUI.Label(fx.transform, e.name.ToUpper(), 12, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-8, 0), TextAnchor.MiddleCenter, true, Txt); nt.resizeTextForBestFit = true; nt.resizeTextMinSize = 8; nt.resizeTextMaxSize = 13;
            var sx = FBUI.Panel(rt, "sexo", new Vector2(0, 1), new Vector2(0, 1), new Vector2(9, -9), new Vector2(8, 8), e.fem ? new Color(1, .42f, .7f) : new Color(.35f, .65f, 1)); sx.sprite = FBUI.CircleSprite; sx.raycastTarget = false;
            var fav = FBUI.Label(rt, "★", 16, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-11, -11), new Vector2(20, 20), TextAnchor.MiddleCenter, false, Gold2).gameObject;
            fav.SetActive(FBCustomizeData.IsFav(e.id));
            var used = FBUI.Panel(rt, "emuso", new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -10), new Vector2(58, 16), Orange); used.raycastTarget = false;
            FBUI.Label(used.transform, "EM USO", 10, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, true);
            used.gameObject.SetActive(!FBSession.pickRandom && e.id == FBSession.skin);
            var ring = bg.gameObject.AddComponent<Outline>(); ring.effectDistance = new Vector2(3, -3); ring.effectColor = Gold2; ring.enabled = e.id == sel;
            if (e.id == sel) bg.color = CardHi;
            var b = bg.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; var id = e.id; float lastClick = -9;
            b.onClick.AddListener(() => { bool dbl = Time.unscaledTime - lastClick < .35f; lastClick = Time.unscaledTime; if (dbl) { Select(id); Use(); } else Select(id); });
            cards[e.id] = (rt, bg, img, ini, ring, fav, used.gameObject);
            if (FBCustomizeThumbs.Cached(e.id)) SetThumb(e.id, FBCustomizeThumbs.Get(e.id), false);
        }
        static string Initials(string n) { var p = (n ?? "?").Split(new[] { ' ', '_' }, System.StringSplitOptions.RemoveEmptyEntries); return string.Concat(p.Take(2).Select(x => char.ToUpperInvariant(x[0]))); }
        void SetThumb(string id, Texture2D t, bool fade)
        {
            if (!t || !cards.TryGetValue(id, out var c) || !c.img) return; c.img.texture = t; c.img.enabled = true; c.initials.enabled = false;
            if (fade) FBCustomizeFx.Fade(c.img, 0, 1, .3f);
        }
        void ApplyFilter()
        {
            var favs = FBCustomizeData.Favs; var q = FBCustomizeData.Norm(query).Trim(); list.Clear();
            foreach (var e in FBCustomizeData.Roster) { bool ok = FBCustomizeData.Match(e, sexF, originF, q, favs); if (ok) list.Add(e); if (cards.TryGetValue(e.id, out var c)) c.rt.gameObject.SetActive(ok); }
            if (countT) countT.text = list.Count == 0 ? "Nenhum personagem com esse filtro." : $"{list.Count} / {FBCustomizeData.Roster.Count} · arraste a grade, clique para ver, duplo clique para usar";
            visT = 0;
        }
        void CenterOn(string id)
        {
            if (!gridView || !cards.TryGetValue(id ?? "", out var c) || !c.rt.gameObject.activeInHierarchy) return;
            float y = -c.rt.anchoredPosition.y - gridView.rect.height / 2 + c.rt.rect.height / 2;
            gridContent.anchoredPosition = new Vector2(0, Mathf.Clamp(y, 0, Mathf.Max(0, gridContent.rect.height - gridView.rect.height)));
        }
        // pede só as miniaturas dos cartões visíveis (+ meia tela de folga)
        void LazyThumbs()
        {
            if (!gridView || tab != "chars") return; var vc = new Vector3[4]; gridView.GetWorldCorners(vc); float m = (vc[1].y - vc[0].y) * .5f, lo = vc[0].y - m, hi = vc[1].y + m; var cc = new Vector3[4];
            foreach (var e in list)
            {
                if (asked.Contains(e.id) || !cards.TryGetValue(e.id, out var c)) continue; c.rt.GetWorldCorners(cc); if (cc[1].y < lo || cc[0].y > hi) continue;
                asked.Add(e.id); var id = e.id; thumbs.Request(id, t => SetThumb(id, t, true));
            }
        }

        // ---------------------------------------------------------------- VISUAL (guarda-roupa)
        bool Avail(string g)
        {
            var w = stage.wardrobe; if (!w) return false; var G = GroupDef(g);
            if (G.cats.Any(c => w.Parts.Any(p => p.cat == c))) return true;
            return g == "cabelo" && w.HasHairMats || g == "tronco" && w.HasClothMats || g == "pele" && (w.HasSheet || w.HasSkinMats || w.HasSkinMask);
        }
        static (string key, string label, string[] cats, FBCustomizeStage.Frame frame) GroupDef(string k) { var g = GROUPS.FirstOrDefault(x => x.key == k); return g.key != null ? g : GROUPS[2]; }
        void BuildLook()
        {
            var w = stage.wardrobe;
            if (!w || !w.Customizable)
            {
                var m = FBUI.Label(body, "Este personagem é uma peça única do jogo original (malha e textura inteiras) e a pele dele não pôde ser separada com segurança (roupa camuflada ou marrom parecida com a pele).\n\nOs personagens <color=#5aa8ff>IE</color> têm guarda-roupa completo, os <color=#5aa8ff>NPCs</color> trocam pele, cabelo e roupa, e a maioria dos originais troca o <color=#5aa8ff>tom de pele</color>.", 16, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, TextAnchor.UpperLeft, false, Txt);
                m.supportRichText = true; m.gameObject.AddComponent<LayoutElement>().preferredHeight = 200;
                var ir = Row(body, "ir", 50); Chip(ir, "VER PERSONAGENS PERSONALIZÁVEIS", false, () => { query = ""; originF = FBCustomizeData.Origin.All; tab = "chars"; BuildLeft(); var e = FBCustomizeData.Roster.FirstOrDefault(x => x.id.StartsWith("ie_")); if (e != null) Select(e.id); }, 15);
                return;
            }
            // categorias (duas linhas no painel estreito)
            var av = GROUPS.Where(g => Avail(g.key)).ToList(); if (!av.Any(g => g.key == group) && av.Count > 0) group = av[0].key;
            int perRow = aspect == FBCustomizeFx.Aspect.Portrait ? 7 : 4;
            for (int i = 0; i < av.Count; i += perRow)
            {
                var r = Row(body, "cats" + i, 34);
                foreach (var g in av.Skip(i).Take(perRow)) { var gg = g; Chip(r, g.label, group == g.key, () => { group = gg.key; stage.SetFrame(gg.frame); BuildLeft(); FBCustomizeFx.Fade(body, .4f, 1, .18f); }, 13); }
            }
            var sv = Scroll(body, out var content, false); var L = w.look ?? w.Default(); var G = GroupDef(group);
            // peças da ficha, por categoria
            foreach (var cat in G.cats)
            {
                var idx = Enumerable.Range(0, w.Parts.Count).Where(i => w.Parts[i].cat == cat).ToList(); if (idx.Count == 0) continue;
                var cn = w.Cats.FirstOrDefault(c => c.key == cat).name ?? (CAT_NAMES.TryGetValue(cat, out var nm) ? nm : cat);
                Section(content, cn.ToUpper());
                bool any = idx.Any(i => L.on.Contains(i));
                PartRow(content, "Nenhum", !any, () => { w.ClearCat(L, cat); Commit(L); });
                foreach (var i in idx) { var ii = i; PartRow(content, Pretty(w.Parts[i].name), L.on.Contains(i), () => { w.Toggle(L, ii); Commit(L); }); }
                if (System.Array.IndexOf(FBWardrobe.TINTABLE, cat) >= 0) Swatches(content, "COR · " + cn.ToUpper(), FBWardrobe.TINTS, FBWardrobe.TINT_NAMES, L.Tint(cat), k => { L.tint[cat] = k; Commit(L); });
            }
            if (group == "cabelo" && w.HasHairMats) Swatches(content, "COR DO CABELO", FBWardrobe.HAIR, FBWardrobe.HAIR_NAMES, L.Tint("hair"), k => { L.tint["hair"] = k; Commit(L); });
            if (group == "tronco" && !w.HasSheet && w.HasClothMats) Swatches(content, "COR DA ROUPA", FBWardrobe.TINTS, FBWardrobe.TINT_NAMES, L.Tint("roupa"), k => { L.tint["roupa"] = k; Commit(L); });
            if (group == "pele")
            {
                var tones = new Color?[FBWardrobe.TONES.Length + 1]; var names = new string[tones.Length]; tones[0] = null; names[0] = "Original";
                for (int i = 0; i < FBWardrobe.TONES.Length; i++) { var t = FBWardrobe.TONES[i]; tones[i + 1] = w.HasSkinMask ? FBWardrobe.TONE_SKIN[i] : new Color(t.r * .78f, t.g * .78f, t.b * .78f); names[i + 1] = FBWardrobe.TONE_NAMES[i]; }
                Swatches(content, "TOM DE PELE", tones, names, L.tone + 1, k => { L.tone = k - 1; Commit(L); });
                if (w.HasSkinMask) { var n = FBUI.Label(content, "Tom aproximado: a pele deste personagem foi separada por uma máscara automática da textura (detalhes cor-de-pele da roupa podem mudar junto).", 13, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, TextAnchor.UpperLeft, false, Dim); n.gameObject.AddComponent<LayoutElement>().preferredHeight = 54; }
            }
            var acts = Row(body, "acoes", 40);
            Chip(acts, "ALEATÓRIO", false, () => { Commit(w.RandomLook()); BuildLeft(); }, 15);
            Chip(acts, "ORIGINAL", false, () => { Commit(w.Default()); BuildLeft(); }, 15);
        }
        static string Pretty(string n) { if (string.IsNullOrEmpty(n)) return "Peça"; n = n.Replace("_", " ").Trim(); return n.Length > 1 ? char.ToUpperInvariant(n[0]) + n.Substring(1) : n; }
        void Section(Transform p, string t) { var l = FBUI.Label(p, t, 13, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, TextAnchor.LowerLeft, true, Gold2); l.gameObject.AddComponent<LayoutElement>().preferredHeight = 26; }
        void PartRow(Transform p, string name, bool on, System.Action act)
        {
            var r = Row(p, "peca", 40, false); var im = r.gameObject.AddComponent<Image>(); im.color = on ? new Color(Gold2.r, Gold2.g, Gold2.b, .22f) : new Color(.05f, .11f, .23f, .8f);
            var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = im; b.onClick.AddListener(() => { FBAudio.Synth("hover"); act(); BuildLeftKeepScroll(); });
            var ck = FBUI.Panel(r, "marca", new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(20, 0), new Vector2(16, 16), on ? Gold2 : new Color(1, 1, 1, .15f)); ck.raycastTarget = false;
            if (on) FBUI.Label(ck.transform, "✓", 13, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, true, Color.black);
            FBUI.Label(r, name, 16, Vector2.zero, Vector2.one, new Vector2(24, 0), new Vector2(-60, 0), TextAnchor.MiddleLeft, false, on ? Color.white : Txt);
            if (on) { var bar = FBUI.Panel(r, "borda", new Vector2(0, 0), new Vector2(0, 1), new Vector2(2, 0), new Vector2(4, 0), Gold2); bar.raycastTarget = false; }
        }
        void Swatches(Transform p, string title, Color?[] cols, string[] names, int cur, System.Action<int> set)
        {
            Section(p, title);
            var r = new GameObject("cores", typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(p, false);
            var g = r.gameObject.AddComponent<GridLayoutGroup>(); g.cellSize = new Vector2(44, 44); g.spacing = new Vector2(8, 8); g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 8;
            r.gameObject.AddComponent<LayoutElement>().preferredHeight = 44 * Mathf.CeilToInt(cols.Length / 8f) + 8 * (Mathf.CeilToInt(cols.Length / 8f) - 1);
            Text tip = null;
            for (int i = 0; i < cols.Length; i++)
            {
                int k = i; var im = new GameObject("cor_" + i, typeof(RectTransform), typeof(Image)).GetComponent<Image>(); im.transform.SetParent(r, false);
                im.color = cols[i].HasValue ? new Color(Mathf.Min(1, cols[i].Value.r), Mathf.Min(1, cols[i].Value.g), Mathf.Min(1, cols[i].Value.b)) : new Color(.3f, .3f, .32f);
                if (!cols[i].HasValue) FBUI.Label(im.transform, "ORIG", 10, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, true, Txt);
                var o = im.gameObject.AddComponent<Outline>(); o.effectColor = i == cur ? Gold2 : new Color(0, 0, 0, .5f); o.effectDistance = i == cur ? new Vector2(3, -3) : new Vector2(1, -1);
                var b = im.gameObject.AddComponent<Button>(); b.targetGraphic = im; b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => { FBAudio.Synth("hover"); set(k); Toast(names[k]); BuildLeftKeepScroll(); });
            }
        }
        void BuildLeftKeepScroll()
        {
            var sr = body ? body.GetComponentInChildren<ScrollRect>() : null; float y = sr ? sr.content.anchoredPosition.y : 0;
            BuildLeft(); StartCoroutine(RestoreScroll(y));
        }
        IEnumerator RestoreScroll(float y) { yield return null; var sr = body ? body.GetComponentInChildren<ScrollRect>() : null; if (sr) sr.content.anchoredPosition = new Vector2(0, y); }
        /// <summary>Aplica no modelo do palco (na hora) e salva o visual deste personagem.</summary>
        public void Commit(FBWardrobe.Look L)
        {
            var w = stage.wardrobe; if (!w) return; w.ApplyLook(L);
            FBCustomizeData.SetLook(sel, w.IsDefault(L) ? "" : w.Encode(L));
            FBCustomizeThumbs.Invalidate(sel);
            thumbs.Request(sel, t => SetThumb(sel, t, true));
            BuildRight();
        }

        // ================================================================ PAINEL DIREITO (destaque)
        void BuildRight()
        {
            if (!right) return; foreach (Transform t in right) Destroy(t.gameObject);
            var e = FBCustomizeData.Find(sel); if (e == null) return; bool portrait = aspect == FBCustomizeFx.Aspect.Portrait;
            bool fixedMode = !FBSession.pickRandom, using_ = fixedMode && e.id == FBSession.skin; var w = stage.wardrobe;
            if (portrait)
            {
                FBUI.Label(right, e.name.ToUpper(), 30, new Vector2(0, 1), new Vector2(.6f, 1), new Vector2(18, -26), new Vector2(-20, 40), TextAnchor.MiddleLeft, true, Txt).resizeTextForBestFit = true;
                FBUI.Label(right, Tags(e, using_), 12, new Vector2(0, 1), new Vector2(.6f, 1), new Vector2(18, -54), new Vector2(-20, 18), TextAnchor.MiddleLeft, false, Gold2).supportRichText = true;
                var row = FBUI.Rect(right, "botoes", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 40), new Vector2(-24, 62)); var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>(); hl.spacing = 8; hl.childControlWidth = hl.childControlHeight = true; hl.childForceExpandWidth = true;
                Chip(row, FBCustomizeData.IsFav(e.id) ? "★" : "☆", false, ToggleFav, 24).gameObject.AddComponent<LayoutElement>().preferredWidth = 50;
                Chip(row, "SORTEAR", false, Shuffle, 13).gameObject.AddComponent<LayoutElement>().preferredWidth = 90;
                var ub = Chip(row, using_ ? "✓ EM USO" : "USAR", using_, Use, 22); ub.gameObject.AddComponent<LayoutElement>().flexibleWidth = 3;
                var mb = Chip(row, fixedMode ? "FIXO" : "ALEATÓRIO", false, ToggleMode, 13); mb.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1.4f;
                FBUI.Label(right, $"#{e.index + 1:00} / {FBCustomizeData.Roster.Count}", 14, new Vector2(.6f, 1), new Vector2(1, 1), new Vector2(-18, -26), new Vector2(-20, 30), TextAnchor.MiddleRight, true, Dim);
                return;
            }
            var vl = right.GetComponent<VerticalLayoutGroup>(); if (!vl) vl = right.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(18, 18, 16, 16); vl.spacing = 8; vl.childControlHeight = vl.childControlWidth = true; vl.childForceExpandHeight = false; vl.childForceExpandWidth = true;
            Text L(string t, int fs, bool title, Color c, float h, TextAnchor a = TextAnchor.MiddleLeft) { var x = FBUI.Label(right, t, fs, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, a, title, c); x.supportRichText = true; x.gameObject.AddComponent<LayoutElement>().preferredHeight = h; return x; }
            var idx = Row(right, "indice", 26, true, 4); FBUI.Label(idx, $"<color=#5aa8ff>#{e.index + 1:00}</color> <size=12>/ {FBCustomizeData.Roster.Count} OPERADORES</size>", 20, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, TextAnchor.MiddleLeft, true, Dim).supportRichText = true;
            var fv = Chip(idx, FBCustomizeData.IsFav(e.id) ? "★ FAVORITO" : "☆ FAVORITAR", FBCustomizeData.IsFav(e.id), ToggleFav, 12); fv.gameObject.AddComponent<LayoutElement>().preferredWidth = 120;
            var nt = L(e.name.ToUpper(), 38, true, Txt, 54); nt.resizeTextForBestFit = true; nt.resizeTextMinSize = 20; nt.resizeTextMaxSize = 38; nt.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0, -3);
            L(Tags(e, using_), 13, true, Gold2, 20);
            var desc = using_ ? "É o seu personagem: você entra com ele em todas as partidas, até trocar."
                : fixedMode ? "Clique em USAR para trocar o seu personagem por este." : "O modo aleatório está ligado (sorteia um do mesmo sexo a cada partida). USAR fixa este personagem e desliga o sorteio.";
            L(desc, 15, false, Dim, 62, TextAnchor.UpperLeft);
            var custom = w && w.Customizable ? (w.HasSheet ? $"GUARDA-ROUPA: {w.Parts.Count(p => p.cat != "body")} PEÇAS" : (w.HasSkinMask ? "TOM DE PELE (APROXIMADO)" : "PERSONALIZÁVEL: PELE, CABELO E ROUPA")) : "PEÇA ÚNICA (SEM TROCA DE ROUPA)";
            var cr = Row(right, "visual", 40); var vb = Chip(cr, (w && w.Customizable ? "✂ PERSONALIZAR · " : "") + custom, false, () => SetTab("look"), 12); vb.interactable = w && w.Customizable;
            var use = Row(right, "usar", 78, false); var ub2 = FBUI.Button(use, "", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Use, !using_);
            ub2.GetComponentInChildren<Text>().text = ""; ub2.GetComponent<Image>().color = using_ ? new Color(.08f, .45f, .22f, .95f) : FBKit.Flag;
            FBUI.Label(ub2.transform, using_ ? "✓ EM USO" : "USAR ESTE PERSONAGEM", using_ ? 28 : 24, Vector2.zero, Vector2.one, new Vector2(0, 9), new Vector2(-10, -24), TextAnchor.MiddleCenter, true, using_ ? Color.white : Color.white).fontStyle = FontStyle.Italic;
            FBUI.Label(ub2.transform, using_ ? "FIXO EM TODAS AS PARTIDAS" : fixedMode ? "TROCA O SEU PERSONAGEM" : "DESLIGA O MODO ALEATÓRIO", 11, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 14), new Vector2(0, 16), TextAnchor.MiddleCenter, false, using_ ? new Color(1, 1, 1, .8f) : new Color(1, 1, 1, .8f));
            var ar = Row(right, "acoes", 44); Chip(ar, "SORTEAR", false, Shuffle, 14); Chip(ar, "♪ DANÇAR", false, () => stage.PlayDance(), 14); Chip(ar, "CENTRALIZAR", false, () => stage.ResetView(), 12);
            L("COMO VOCÊ ENTRA NA PARTIDA", 12, true, Dim, 22, TextAnchor.LowerLeft);
            var mr = Row(right, "modo", 52);
            Chip(mr, "FIXO<size=10>\nSEMPRE ESTE</size>", fixedMode, () => { if (!fixedMode) ToggleMode(); }, 15);
            var sxT = FBCustomizeData.Find(FBSession.skin)?.fem == true ? "FEMININAS" : "MASCULINOS";
            Chip(mr, $"ALEATÓRIO<size=10>\nSÓ {sxT}</size>", !fixedMode, () => { if (fixedMode) ToggleMode(); }, 15);
            var fill = new GameObject("folga", typeof(RectTransform)); fill.transform.SetParent(right, false); fill.AddComponent<LayoutElement>().flexibleHeight = 1;
            var nav = Row(right, "nav", 40); Chip(nav, "‹ ANTERIOR", false, () => Step(-1), 13); Chip(nav, "PRÓXIMO ›", false, () => Step(1), 13);
        }
        static string Tags(FBCustomizeData.Entry e, bool using_) => $"{(e.fem ? "FEMININO" : "MASCULINO")} · {(e.npc ? "NPC" : "REGIONAL")}{(e.id.StartsWith("ie_") ? " · INVASÃO ELITE" : "")}{(using_ ? "  <color=#7be08a>✓ EM USO</color>" : "")}";

        // ================================================================ AÇÕES
        public void Select(string id)
        {
            if (id == null || FBCustomizeData.Find(id) == null) return;
            if (id != sel)
            {
                if (cards.TryGetValue(sel ?? "", out var o)) { o.ring.enabled = false; o.bg.color = Card; }
                sel = id; stage.Show(id, FBCustomizeData.LookFor(id));
                if (cards.TryGetValue(id, out var c)) { c.ring.enabled = true; c.bg.color = CardHi; FBCustomizeFx.Pop(c.rt, .9f); }
            }
            if (tab == "look") BuildLeft();
            else { var t = left ? left.Find("abas") : null; if (t) BuildLeftTabsOnly(t); }   // a aba VISUAL mostra "—" quando não há o que trocar
            BuildRight(); FBCustomizeFx.Fade(right, .3f, 1, .2f);
        }
        void BuildLeftTabsOnly(Transform tabs) { foreach (Transform x in tabs) Destroy(x.gameObject); var w = stage.wardrobe; Tab(tabs, "PERSONAGENS", tab == "chars", () => SetTab("chars")); Tab(tabs, w && w.Customizable ? "VISUAL" : "VISUAL <size=11>(—)</size>", tab == "look", () => SetTab("look")); }
        public void Step(int d)
        {
            var l = list.Count > 0 ? list : FBCustomizeData.Roster; if (l.Count == 0) return; int i = l.FindIndex(x => x.id == sel);
            Select(l[((i < 0 ? 0 : i + d) % l.Count + l.Count) % l.Count].id); CenterOn(sel); FBAudio.Synth("hover");
        }
        public void Use()
        {
            var e = FBCustomizeData.Find(sel); if (e == null) return; bool was = !FBSession.pickRandom && FBSession.skin == sel;
            FBCustomizeData.Use(sel); stage.PlayConfirm();
            foreach (var kv in cards) kv.Value.used.SetActive(kv.Key == sel);
            if (!was) { Toast("PERSONAGEM DEFINIDO: " + e.name.ToUpper()); FBAudio.Synth("buy"); }
            BuildRight();
        }
        void ToggleFav() { bool on = FBCustomizeData.ToggleFav(sel); if (cards.TryGetValue(sel, out var c)) c.fav.SetActive(on); Toast(on ? "★ FAVORITO" : "Removido dos favoritos"); if (originF == FBCustomizeData.Origin.Fav) ApplyFilter(); BuildRight(); }
        void Shuffle() { var l = (list.Count > 0 ? list : FBCustomizeData.Roster).Where(x => x.id != sel).ToList(); if (l.Count == 0) return; Select(l[Random.Range(0, l.Count)].id); CenterOn(sel); }
        void ToggleMode()
        {
            FBSession.pickRandom = !FBSession.pickRandom;
            if (!FBSession.pickRandom) FBCustomizeData.Use(sel); else FBSession.Save();
            foreach (var kv in cards) kv.Value.used.SetActive(!FBSession.pickRandom && kv.Key == FBSession.skin);
            Toast(FBSession.pickRandom ? "MODO ALEATÓRIO: sorteia a cada partida" : "MODO FIXO: " + (FBCustomizeData.Find(sel)?.name ?? sel).ToUpper()); BuildRight();
        }

        // ================================================================ QUADRO
        // [parceiro] v1 (thread de física): tempo do Update medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
        static readonly int __fbt_Update = FBTempo.Id("FBCustomize.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            if (Screen.width != screen.x || Screen.height != screen.y) Build(false);   // girou o celular / mudou a janela: remonta mantendo o estado
            if ((visT -= Time.unscaledDeltaTime) <= 0) { visT = .12f; LazyThumbs(); }
            if (toastT && toastT.text != "" && Time.unscaledTime > toastUntil) { toastT.text = ""; }
            var kb = UnityEngine.InputSystem.Keyboard.current; if (kb == null || (search && search.isFocused)) return;
            if (kb.rightArrowKey.wasPressedThisFrame) Step(1); else if (kb.leftArrowKey.wasPressedThisFrame) Step(-1);
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Use();
            else if (kb.escapeKey.wasPressedThisFrame) Close();
        }

        // ================================================================ PRINTS (-fbchars): cada jogável na vitrine, corpo inteiro e rosto
        /// <summary>Grava &lt;build&gt;/autoteste/char_, rosto_ e arma_&lt;id&gt;.png de todos os jogáveis (não-NPC), já animados e com a arma na mão.</summary>
        public IEnumerator CharShots(string dir)
        {
            System.IO.Directory.CreateDirectory(dir);
            foreach (var e in FBCustomizeData.Roster.Where(x => !x.npc).ToList())
            {
                Select(e.id); stage.yaw = 180; stage.zoom = 1; stage.SetFrame(FBCustomizeStage.Frame.Full);
                for (float t = 0; t < 1.5f; t += Time.unscaledDeltaTime) yield return null;   // modelo carregado e idle assentado
                FBCustomizeFx.Finish(); stage.Snap(); for (int i = 0; i < 4; i++) yield return null;
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "char_" + e.id + ".png")); yield return null; yield return null;
                stage.SetFrame(FBCustomizeStage.Frame.Face); for (float t = 0; t < .6f; t += Time.unscaledDeltaTime) yield return null;
                stage.Snap(); for (int i = 0; i < 4; i++) yield return null;
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "rosto_" + e.id + ".png")); yield return null; yield return null;
                stage.SetFrame(FBCustomizeStage.Frame.Torso); stage.yaw = 145; for (float t = 0; t < .6f; t += Time.unscaledDeltaTime) yield return null;   // empunhadura: tronco de 3/4
                stage.Snap(); for (int i = 0; i < 4; i++) yield return null;
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "arma_" + e.id + ".png")); yield return null; yield return null;
                Debug.Log("[FreeBala][personagem] prints de " + e.id);
            }
        }

        // ================================================================ PRINTS (-fbcustom): o coordenador roda o executável e confere
        /// <summary>Percorre a tela e grava prints em &lt;build&gt;/autoteste/personagem_*.png (grade, cada aba do visual, close, 21:9, retrato).</summary>
        public IEnumerator Shots(string dir, bool resolutions)
        {
            System.IO.Directory.CreateDirectory(dir);
            IEnumerator Shot(string n) { FBCustomizeFx.Finish(); stage.Snap(); for (int i = 0; i < 6; i++) yield return null; ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "personagem_" + n + ".png")); yield return null; yield return null; Debug.Log("[FreeBala][personagem] print " + n); }
            var keep = new[] { "ie_bandido", "c_helena", FBCustomizeData.Roster.FirstOrDefault(e => e.id.StartsWith("n2_"))?.id }.Where(x => x != null).ToDictionary(x => x, FBCustomizeData.LookFor);   // os prints não mudam o visual salvo do jogador
            for (float t = 0; t < 4 && thumbs.Pending > 0; t += Time.unscaledDeltaTime) yield return null;   // primeiras miniaturas
            yield return Shot("grade");
            sexF = "f"; BuildLeft(); yield return Shot("filtro_feminino"); sexF = "all"; query = "ie"; BuildLeft(); yield return Shot("busca_ie"); query = ""; BuildLeft();
            var ie = FBCustomizeData.Roster.FirstOrDefault(e => e.id == "ie_bandido") ?? FBCustomizeData.Roster.FirstOrDefault(e => e.id.StartsWith("ie_"));
            if (ie != null)
            {
                Select(ie.id); SetTab("look");
                foreach (var g in GROUPS) if (Avail(g.key)) { group = g.key; stage.SetFrame(g.frame); BuildLeft(); yield return Shot("visual_" + g.key); }
                Commit(stage.wardrobe.RandomLook()); BuildLeft(); yield return Shot("visual_aleatorio");
                stage.zoom = FBCustomizeStage.ZoomMax; yield return Shot("zoom_max"); stage.zoom = 1;
            }
            var npc = FBCustomizeData.Roster.FirstOrDefault(e => e.id.StartsWith("n2_")); if (npc != null) { SetTab("chars"); Select(npc.id); SetTab("look"); yield return Shot("visual_npc"); }
            var atl = FBCustomizeData.Find("c_helena") ?? FBCustomizeData.Roster.FirstOrDefault(e => e.id.StartsWith("p_"));
            if (atl != null) { SetTab("chars"); Select(atl.id); SetTab("look"); var aw = stage.wardrobe; if (aw && aw.HasSkinMask) { var AL = aw.look ?? aw.Default(); AL.tone = 4; Commit(AL); BuildLeft(); yield return Shot("visual_atlas_tom"); stage.SetFrame(FBCustomizeStage.Frame.Full); yield return Shot("atlas_tom_corpo"); } }
            SetTab("chars"); stage.yaw = 120; yield return Shot("girado");
            if (resolutions && !Application.isMobilePlatform)
            {
                int w0 = Screen.width, h0 = Screen.height; var fs = Screen.fullScreenMode;
                Screen.SetResolution(2560, 1080, FullScreenMode.Windowed); for (int i = 0; i < 20; i++) yield return null; yield return Shot("21x9");
                Screen.SetResolution(720, 1280, FullScreenMode.Windowed); for (int i = 0; i < 20; i++) yield return null; yield return Shot("retrato");
                SetTab("look"); yield return Shot("retrato_visual"); SetTab("chars");
                Screen.SetResolution(w0, h0, fs); for (int i = 0; i < 10; i++) yield return null;
            }
            foreach (var kv in keep) FBCustomizeData.SetLook(kv.Key, kv.Value);
            Debug.Log($"[FreeBala][personagem] roster {FBCustomizeData.Roster.Count} · miniaturas pendentes {thumbs.Pending} · visual salvo {FBSession.FullSkin}");
        }
    }
}
