// Kit visual das telas do lobby (padrão único): fundo escuro translúcido, cartões com borda fina, acento dourado em 3 px, títulos
// pequenos em caixa alta com espaçamento, texto 13–18 px, conteúdo centrado com largura máxima (não estica em telas largas).
// Usado por FBMenu (início, partida, armas, emotes, passe, controles, online) e FBSettings (ajustes).
// r47 (pedido 30/10: modernizar a tela inicial sem perder a identidade): cantos arredondados (sprite 9-slice gerado, FBUI.Round),
// realce animado no hover/toque (FBHover: brilho + escala com amortecimento, em vez de trocar a cor na hora), texto corrido em
// Open Sans Semibold (títulos continuam Russo One), entrada das páginas com fade + subida curta (FBFadeIn) e área segura do
// celular (FBSafeArea: entalhe/cantos arredondados do Android e iOS).
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FreeBala
{
    public static class FBKit
    {
        public static readonly Color Bg = new(.035f, .04f, .05f, .94f), Card = new(1, 1, 1, .045f), CardHi = new(1, 1, 1, .09f), Line = new(1, 1, 1, .1f),
            Txt = new(.93f, .92f, .88f), Dim = new(.93f, .92f, .88f, .55f), Gold = new(1, .74f, .12f), GoldDark = new(.16f, .1f, 0),
            Glass = new(.05f, .058f, .07f, .74f), Red = new(.86f, .12f, .06f), Green = new(.45f, .95f, .55f);
        public const float MaxW = 1240;
        public const int R = 6;   // raio padrão dos cartões
        static Font body;
        public static Font BodyFont => body ? body : body = Resources.Load<Font>("Fonts/OpenSans-Semibold") ?? FBUI.Font;

        // área de conteúdo centrada (largura máx. MaxW), abaixo do topo de 64 px
        public static RectTransform Body(Transform page, float top = 64, float bottom = 0)
        {
            var r = FBUI.Rect(page, "corpo", new Vector2(.5f, 0), new Vector2(.5f, 1), Vector2.zero, Vector2.zero);
            r.offsetMin = new Vector2(-MaxW / 2, bottom); r.offsetMax = new Vector2(MaxW / 2, -top); return r;
        }
        // retângulo pelo canto superior esquerdo (x, y para baixo) dentro de um pai com âncora no topo
        public static RectTransform At(Transform p, string n, float x, float y, float w, float h)
        { var r = FBUI.Rect(p, n, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(w, h)); r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); return r; }

        // cantos arredondados (só em caixas com tamanho para isso: fio de 1 px e acento de 3 px continuam retos)
        public static Image Round(Image im, int r = R) { im.sprite = FBUI.Round(r); im.type = Image.Type.Sliced; im.pixelsPerUnitMultiplier = 1; return im; }
        public static Image Pill(Image im) => Round(im, 16);
        // painel principal: arredondado, fio claro e sombra curta embaixo
        public static Image Frame(Image im, int r = 12)
        {
            Round(im, r); if (!im.TryGetComponent<Outline>(out var o)) o = im.gameObject.AddComponent<Outline>(); o.effectColor = Line; o.effectDistance = new Vector2(1, -1);
            var sh = im.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .45f); sh.effectDistance = new Vector2(0, -6); return im;
        }

        public static Image Box(Transform p, string n, float x, float y, float w, float h, Color? c = null, bool border = true)
        {
            var im = At(p, n, x, y, w, h).gameObject.AddComponent<Image>(); im.color = c ?? Card; im.raycastTarget = false;
            if (w >= 12 && h >= 12) Round(im);
            if (border) { var o = im.gameObject.AddComponent<Outline>(); o.effectColor = Line; o.effectDistance = new Vector2(1, -1); }
            return im;
        }
        public static Text T(Transform p, string s, int size, float x, float y, float w, float h, TextAnchor al = TextAnchor.MiddleLeft, bool title = false, Color? c = null)
        {
            var t = FBUI.Label(p, s, size, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(w, h), al, title, c ?? Txt); if (!title) t.font = BodyFont;
            var r = t.rectTransform; r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); return t;
        }
        // título de seção: texto pequeno espaçado + acento dourado arredondado
        public static void Section(Transform p, string s, float x, float y, float w)
        {
            T(p, s, 13, x + 12, y, w, 20, TextAnchor.MiddleLeft, true, Dim);
            var a = At(p, "acento", x, y + 3, 3, 14).gameObject.AddComponent<Image>(); a.color = Gold; a.raycastTarget = false; Pill(a);
        }

        // botão/cartão selecionável: fundo neutro, realce animado no hover, selecionado = borda + fio dourado embaixo
        public static Button Tile(Transform p, string n, float x, float y, float w, float h, bool sel, System.Action click)
        {
            var im = Box(p, n, x, y, w, h, sel ? new Color(1, .74f, .12f, .14f) : Card); im.raycastTarget = true;
            if (sel) { im.GetComponent<Outline>().effectColor = new Color(1, .74f, .12f, .9f); var ln = At(im.transform, "sel", 6, h - 3, w - 12, 3).gameObject.AddComponent<Image>(); ln.color = Gold; ln.raycastTarget = false; Pill(ln); }
            var b = im.gameObject.AddComponent<Button>(); b.targetGraphic = im; b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { FBAudio.Synth("hover"); click?.Invoke(); });
            FBHover.Add(im, .07f, w * h > 60000 ? 1.008f : 1.02f);
            return b;
        }
        public static Button TextTile(Transform p, string s, float x, float y, float w, float h, bool sel, System.Action click, int size = 15)
        { var b = Tile(p, "b_" + s, x, y, w, h, sel, click); T(b.transform, s, size, 0, 0, w, h, TextAnchor.MiddleCenter, true, sel ? Color.white : Txt); return b; }
        public static Button Primary(Transform p, string s, float x, float y, float w, float h, System.Action click)
        {
            var im = Box(p, "b_" + s, x, y, w, h, Gold, false); im.raycastTarget = true; Round(im, 8); var b = im.gameObject.AddComponent<Button>(); b.targetGraphic = im; b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { FBAudio.Synth("hover"); click?.Invoke(); });
            var sh = im.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(1, .6f, 0, .25f); sh.effectDistance = new Vector2(0, -4);
            T(im.transform, s, 18, 0, 0, w, h, TextAnchor.MiddleCenter, true, GoldDark); FBHover.Add(im, .2f, 1.03f); return b;
        }
        // seta ‹ › clicável dentro de um seletor (cada lado com o próprio clique)
        public static Button Arrow(Transform p, bool right, float x, float w, float h, System.Action click)
        {
            var im = At(p, right ? "seta_d" : "seta_e", x, 0, w, h).gameObject.AddComponent<Image>(); im.color = new Color(1, 1, 1, 0); Round(im);
            var b = im.gameObject.AddComponent<Button>(); b.targetGraphic = im; b.transition = Selectable.Transition.None; b.onClick.AddListener(() => { FBAudio.Synth("hover"); click?.Invoke(); });
            T(im.transform, right ? "›" : "‹", 22, 0, -1, w, h, TextAnchor.MiddleCenter, true, right ? Gold : Txt); FBHover.Add(im, .1f, 1.1f); return b;
        }

        // imagem que PREENCHE o retângulo sem distorcer (corta o excesso pelo centro — nunca sobra área cinza)
        public static RawImage Cover(Transform p, Texture tex, float x, float y, float w, float h, float yAlign = 0.5f)
        {
            var ri = At(p, "img", x, y, w, h).gameObject.AddComponent<RawImage>(); ri.raycastTarget = false; ri.texture = tex;
            if (tex) { float ar = (float)tex.width / tex.height, want = w / h; ri.uvRect = ar > want ? new Rect((1 - want / ar) / 2, 0, want / ar, 1) : new Rect(0, (1 - ar / want) * Mathf.Clamp01(yAlign), 1, ar / want); }
            else ri.color = new Color(.12f, .13f, .15f);
            return ri;
        }
        // degradê escuro na base de uma imagem (legibilidade do texto por cima)
        public static void Shade(Transform p, float x, float y, float w, float h)
        {
            var tx = grad ? grad : grad = Grad(); var ri = At(p, "sombra", x, y, w, h).gameObject.AddComponent<RawImage>(); ri.texture = tx; ri.raycastTarget = false;
        }
        static Texture2D grad;
        static Texture2D Grad() { var t = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; for (int i = 0; i < 64; i++) t.SetPixel(0, i, new Color(0, 0, 0, Mathf.Pow(1 - i / 63f, 1.6f) * .85f)); t.Apply(); return t; }
        public static Texture2D GradTex => grad ? grad : grad = Grad();

        // lista com rolagem vertical (conteúdo com VerticalLayoutGroup)
        public static RectTransform Scroll(Transform p, float x, float y, float w, float h, float spacing = 6)
        {
            var bg = Box(p, "lista", x, y, w, h, new Color(0, 0, 0, .25f)); bg.raycastTarget = true; bg.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("c", typeof(RectTransform)); var cr = (RectTransform)content.transform; cr.SetParent(bg.transform, false); cr.anchorMin = new Vector2(0, 1); cr.anchorMax = new Vector2(1, 1); cr.pivot = new Vector2(.5f, 1); cr.sizeDelta = Vector2.zero;
            var vl = content.AddComponent<VerticalLayoutGroup>(); vl.spacing = spacing; vl.padding = new RectOffset(8, 8, 8, 8); vl.childControlHeight = true; vl.childForceExpandHeight = false; vl.childControlWidth = true; vl.childForceExpandWidth = true;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var s = bg.gameObject.AddComponent<ScrollRect>(); s.content = cr; s.horizontal = false; s.scrollSensitivity = 30; s.movementType = ScrollRect.MovementType.Clamped; return cr;
        }
        // linha de lista (dentro de Scroll): título + subtítulo à direita
        public static Button Row(Transform list, string title, string sub, bool sel, System.Action click, float h = 40)
        {
            var g = new GameObject("linha", typeof(RectTransform), typeof(Image), typeof(LayoutElement)); g.transform.SetParent(list, false); g.GetComponent<LayoutElement>().preferredHeight = h;
            var im = g.GetComponent<Image>(); Color baseC = sel ? new Color(1, .74f, .12f, .16f) : new Color(1, 1, 1, .035f); im.color = baseC; Round(im);
            if (sel) { var a = FBUI.Panel(g.transform, "acento", new Vector2(0, 0), new Vector2(0, 1), new Vector2(3.5f, 0), new Vector2(3, -12), Gold); a.raycastTarget = false; Pill(a); }
            var t = FBUI.Label(g.transform, title, 15, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleLeft, true, sel ? Color.white : Txt); t.rectTransform.offsetMin = new Vector2(16, 0); t.rectTransform.offsetMax = new Vector2(-110, 0);
            if (!string.IsNullOrEmpty(sub)) { var s = FBUI.Label(g.transform, sub, 11, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleRight, true, Dim); s.rectTransform.offsetMin = new Vector2(0, 0); s.rectTransform.offsetMax = new Vector2(-14, 0); }
            var b = g.AddComponent<Button>(); b.targetGraphic = im; b.transition = Selectable.Transition.None; b.onClick.AddListener(() => { FBAudio.Synth("hover"); click?.Invoke(); });
            FBHover.Add(im, .06f, 1);
            return b;
        }
    }

    // Realce de hover/toque: um brilho branco arredondado por cima (alfa sobe com amortecimento) e escala leve; pressionado encolhe um
    // pouco. Não mexe na cor do botão (as páginas recolorem cartões depois de criar — o realce antigo devolvia a cor velha ao sair).
    // slide: deslocamento (px) no hover, ex. a barra lateral do início. onHover: callback (true/false) para trocar ícone/texto.
    public class FBHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public Image glow; public float hoverA = .07f, pressA = .14f, grow = 1.02f; public Vector2 slide; public System.Action<bool> onHover;
        bool over, down; float a, sc = 1, sl; Vector2 p0; bool hasP0;
        public static FBHover Add(Graphic g, float hoverA = .07f, float grow = 1.02f)
        {
            var h = g.gameObject.AddComponent<FBHover>(); h.hoverA = hoverA; h.pressA = Mathf.Max(hoverA * 1.8f, hoverA + .05f); h.grow = grow;
            var gl = FBUI.Panel(g.transform, "realce", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(1, 1, 1, 0)); gl.raycastTarget = false;
            if (g is Image im && im.sprite) { gl.sprite = im.sprite; gl.type = im.type; gl.pixelsPerUnitMultiplier = im.pixelsPerUnitMultiplier; }
            h.glow = gl; return h;
        }
        public void OnPointerEnter(PointerEventData e) { over = true; if (glow) glow.transform.SetAsLastSibling(); onHover?.Invoke(true); }
        public void OnPointerExit(PointerEventData e) { over = false; down = false; onHover?.Invoke(false); }
        public void OnPointerDown(PointerEventData e) { down = true; if (glow) glow.transform.SetAsLastSibling(); }
        public void OnPointerUp(PointerEventData e) { down = false; }
        void Update()
        {
            float ta = down ? pressA : over ? hoverA : 0, ts = down ? Mathf.Min(1, grow) * .985f : over ? grow : 1, tl = over || down ? 1 : 0;
            if (Mathf.Abs(a - ta) < .002f && Mathf.Abs(sc - ts) < .0005f && Mathf.Abs(sl - tl) < .002f) return;
            float k = 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime); a = Mathf.Lerp(a, ta, k); sc = Mathf.Lerp(sc, ts, k); sl = Mathf.Lerp(sl, tl, k);
            if (glow) glow.color = new Color(1, 1, 1, a); transform.localScale = new Vector3(sc, sc, 1);
            if (slide != Vector2.zero && transform is RectTransform rt) { if (!hasP0) { p0 = rt.anchoredPosition; hasP0 = true; } rt.anchoredPosition = p0 + slide * sl; }
        }
        void OnDisable() { over = down = false; a = 0; sc = 1; sl = 0; if (glow) glow.color = new Color(1, 1, 1, 0); transform.localScale = Vector3.one; if (hasP0 && transform is RectTransform rt) rt.anchoredPosition = p0; }
    }

    // entrada de página: transparência 0 -> 1 com subida curta (ease-out cúbico), em tempo real (funciona com o jogo pausado)
    public class FBFadeIn : MonoBehaviour
    {
        public float dur = .22f, dy = 14, delay; CanvasGroup g; RectTransform rt; Vector2 p0; float t;
        public static FBFadeIn Add(Component c, float delay = 0, float dy = 14) { var f = c.gameObject.AddComponent<FBFadeIn>(); f.delay = delay; f.dy = dy; return f; }
        void Awake() { if (!TryGetComponent(out g)) g = gameObject.AddComponent<CanvasGroup>(); g.alpha = 0; rt = transform as RectTransform; if (rt) p0 = rt.anchoredPosition; }
        void Update()
        {
            t += Time.unscaledDeltaTime; float k = Mathf.Clamp01((t - delay) / dur), e = 1 - (1 - k) * (1 - k) * (1 - k);
            g.alpha = e; if (rt) rt.anchoredPosition = p0 + new Vector2(0, -dy * (1 - e));
            if (k >= 1) { g.alpha = 1; if (rt) rt.anchoredPosition = p0; Destroy(this); }
        }
    }

    // área segura (entalhe, câmera furada, cantos arredondados, barra de gestos): o retângulo acompanha Screen.safeArea
    public class FBSafeArea : MonoBehaviour
    {
        Rect last; Vector2Int res;
        void Update()
        {
            var sa = Screen.safeArea; var r = new Vector2Int(Screen.width, Screen.height); if (sa == last && r == res) return; last = sa; res = r;
            if (r.x <= 0 || r.y <= 0) return; var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(sa.xMin / r.x, sa.yMin / r.y); rt.anchorMax = new Vector2(sa.xMax / r.x, sa.yMax / r.y); rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }

    // brilho que atravessa um botão de tempos em tempos (COMEÇAR): faixa diagonal recortada pela máscara do próprio botão
    public class FBGlint : MonoBehaviour
    {
        public RectTransform band; public float every = 3.6f, dur = .9f, width = 320; float t;
        void Update()
        {
            t += Time.unscaledDeltaTime; if (t > every) t -= every; float k = Mathf.Clamp01(t / dur);
            if (band) { band.anchoredPosition = new Vector2(Mathf.Lerp(-width * .75f, width * .75f, k * k * (3 - 2 * k)), 0); band.gameObject.SetActive(k < 1); }
        }
    }
}
