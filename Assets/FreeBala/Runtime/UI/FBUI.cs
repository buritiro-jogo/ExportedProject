// Construtores de interface em código (uGUI): o menu, a HUD e os controles de toque são montados por script, com as fontes do jogo
// (RussoOne / LiberationSans copiadas para Resources/Fonts) e a paleta do Free Bala (dourado/laranja sobre fundo escuro).
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FreeBala
{
    public static class FBUI
    {
        public static readonly Color Gold = new Color(.96f, .67f, .09f), Red = new Color(.93f, .23f, .2f), Dark = new Color(.04f, .05f, .06f, .92f), Mut = new Color(.62f, .66f, .7f);
        static Font font, title;
        public static Font Font => font ? font : font = Resources.Load<Font>("Fonts/LiberationSans") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        public static Font Title => title ? title : title = Resources.Load<Font>("Fonts/RussoOne-Regular") ?? Font;

        static Sprite circle, hit, arrow, white;
        static Sprite Make(int n, System.Func<float, float, float> a)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear }; var px = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(a((x + .5f) / n * 2 - 1, (y + .5f) / n * 2 - 1)));
            t.SetPixels(px); t.Apply(); return Sprite.Create(t, new Rect(0, 0, n, n), Vector2.one * .5f);
        }
        public static Sprite CircleSprite => circle ? circle : circle = Make(64, (x, y) => (1 - Mathf.Sqrt(x * x + y * y)) * 32);
        public static Sprite WhiteSprite => white ? white : white = Make(4, (x, y) => 1);
        // r47: retângulo arredondado 9-slice (raio r px, borda antisserrilhada) para os cartões e botões do lobby; um por raio
        static readonly System.Collections.Generic.Dictionary<int, Sprite> rounds = new();
        public static Sprite Round(int r)
        {
            if (rounds.TryGetValue(r, out var sp) && sp) return sp;
            int n = r * 2 + 2; var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear }; var px = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float fx = x + .5f, fy = y + .5f, cx = Mathf.Clamp(fx, r, n - r), cy = Mathf.Clamp(fy, r, n - r), d = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d + .5f) * 255));
            }
            t.SetPixels32(px); t.Apply(false, true);
            sp = Sprite.Create(t, new Rect(0, 0, n, n), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r)); rounds[r] = sp; return sp;
        }
        // marcador de acerto: quatro traços em X (girado 45° na HUD vira +)
        public static Sprite HitSprite => hit ? hit : hit = Make(64, (x, y) => { float ax = Mathf.Abs(x), ay = Mathf.Abs(y); bool arm = (ax < .1f && ay > .35f) || (ay < .1f && ax > .35f); return arm ? 1 : 0; });
        public static Sprite ArrowSprite => arrow ? arrow : arrow = Make(64, (x, y) => { float yy = (y + 1) / 2; return Mathf.Abs(x) < (1 - yy) * .9f && y > -.8f ? 1 : 0; });

        public static Canvas Canvas(string name, int order)
        {
            var g = new GameObject(name); var c = g.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = order;
            var s = g.AddComponent<CanvasScaler>(); s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; s.referenceResolution = new Vector2(1600, 900); s.matchWidthOrHeight = .5f;
            g.AddComponent<GraphicRaycaster>();
            if (!Object.FindAnyObjectByType<EventSystem>()) { var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(); }
            return c;
        }
        public static RectTransform Rect(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
        {
            var g = new GameObject(name, typeof(RectTransform)); var rt = (RectTransform)g.transform; rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(.5f, .5f); rt.anchoredPosition = pos; rt.sizeDelta = size; return rt;
        }
        public static Image Panel(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Color c)
        { var im = Rect(parent, name, aMin, aMax, pos, size).gameObject.AddComponent<Image>(); im.color = c; return im; }
        public static Text Label(Transform parent, string text, int size, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 box, TextAnchor al = TextAnchor.MiddleCenter, bool title = false, Color? col = null)
        {
            var t = Rect(parent, "txt", aMin, aMax, pos, box).gameObject.AddComponent<Text>();
            t.text = text; t.font = title ? Title : Font; t.fontSize = size; t.alignment = al; t.color = col ?? Color.white; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; return t;
        }
        public static Button Button(Transform parent, string text, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, System.Action onClick, bool primary = false)
        {
            var im = Panel(parent, "btn_" + text, aMin, aMax, pos, size, primary ? Gold : new Color(1, 1, 1, .08f));
            var b = im.gameObject.AddComponent<Button>(); b.onClick.AddListener(() => onClick?.Invoke());
            var cb = b.colors; cb.highlightedColor = primary ? new Color(1, .8f, .3f) : new Color(1, 1, 1, .18f); cb.pressedColor = Gold; b.colors = cb;
            Label(im.transform, text, primary ? 26 : 18, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, true, primary ? new Color(.07f, .07f, .07f) : Color.white);
            return b;
        }
    }
}
