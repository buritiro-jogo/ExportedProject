// Feed de abates (canto superior direito), refeito: cada linha = [atacante] [imagem da arma | ZONA/QUEDA] [⊕ cabeça] [vítima], cores por
// time (aliado azul, inimigo vermelho, você dourado), faixa dourada quando você participa, entra deslizando da direita, some com fade
// em 5 s, no máximo 5 linhas. Mensagens de texto (reforços, chat) usam a mesma pilha com estilo neutro.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FreeBala
{
    public class FBKillFeed : MonoBehaviour
    {
        class Row { public RectTransform rt; public CanvasGroup cg; public float t; public float w; }
        readonly List<Row> rows = new(); RectTransform box; static FBKillFeed I;
        static readonly Color Ally = new(.4f, .68f, 1), Enemy = new(1, .4f, .36f), Me = new(1, .78f, .25f);
        const float Life = 5, H = 30, Gap = 4;

        public static FBKillFeed Attach(Transform canvasRoot)
        {
            var g = new GameObject("FeedAbates", typeof(RectTransform)); g.transform.SetParent(canvasRoot, false); var rt = (RectTransform)g.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 1); rt.anchoredPosition = new Vector2(-18, -180); rt.sizeDelta = new Vector2(520, 200);   // abaixo do HUD de armas (canto superior direito)
            I = g.AddComponent<FBKillFeed>(); I.box = rt; return I;
        }
        static Color ColorOf(FBCharacter a) => !a ? Color.white : a.isPlayer ? Me : FBMatch.FFA ? Color.white : a.team == FBSession.team ? Ally : Enemy;

        Text Txt(Transform p, string s, Color c, int size = 17)
        { var t = new GameObject("t", typeof(RectTransform), typeof(Text)).GetComponent<Text>(); t.transform.SetParent(p, false); t.font = FBUI.Title; t.fontSize = size; t.color = c; t.text = s; t.raycastTarget = false; t.alignment = TextAnchor.MiddleCenter; t.horizontalOverflow = HorizontalWrapMode.Overflow; var sh = t.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .8f); t.gameObject.AddComponent<LayoutElement>(); return t; }

        Row NewRow(bool me)
        {
            var g = new GameObject("linha", typeof(RectTransform), typeof(Image), typeof(CanvasGroup)); g.transform.SetParent(box, false); var rt = (RectTransform)g.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 1); var bg = g.GetComponent<Image>(); bg.color = me ? new Color(.18f, .13f, .03f, .78f) : new Color(.03f, .04f, .05f, .66f); bg.raycastTarget = false;
            var hl = g.AddComponent<HorizontalLayoutGroup>(); hl.padding = new RectOffset(12, 12, 2, 2); hl.spacing = 8; hl.childAlignment = TextAnchor.MiddleCenter; hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = false;
            g.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            if (me) { var e = new GameObject("fio", typeof(RectTransform), typeof(Image), typeof(LayoutElement)); e.transform.SetParent(g.transform, false); e.GetComponent<Image>().color = Me; var le = e.GetComponent<LayoutElement>(); le.ignoreLayout = true; var er = (RectTransform)e.transform; er.anchorMin = new Vector2(1, 0); er.anchorMax = new Vector2(1, 1); er.pivot = new Vector2(1, .5f); er.sizeDelta = new Vector2(3, 0); er.anchoredPosition = Vector2.zero; }
            var r = new Row { rt = rt, cg = g.GetComponent<CanvasGroup>(), t = 0 }; rows.Insert(0, r);
            while (rows.Count > 5) { Destroy(rows[^1].rt.gameObject); rows.RemoveAt(rows.Count - 1); }
            return r;
        }

        public static void Kill(FBCharacter src, FBCharacter victim, string why, bool head, string weaponId)
        {
            if (!I || !victim) return; bool me = (src && src.isPlayer) || victim.isPlayer; var r = I.NewRow(me);
            if (src && src != victim) I.Txt(r.rt, src.Name.ToUpperInvariant(), ColorOf(src));
            var tex = why == null && !string.IsNullOrEmpty(weaponId) ? FBIcons.Weapon(weaponId, 128, 56) : null;
            if (tex) { var ri = new GameObject("arma", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement)); ri.transform.SetParent(r.rt, false); ri.GetComponent<RawImage>().texture = tex; ri.GetComponent<RawImage>().raycastTarget = false; var le = ri.GetComponent<LayoutElement>(); le.preferredWidth = 64; le.preferredHeight = 28; }
            else I.Txt(r.rt, why == "zone" ? "☁ ZONA" : why == "fall" ? "QUEDA" : why == "car" ? "ATROPELADO" : why == "bleed" ? "SANGROU" : "✖", new Color(1, 1, 1, .7f), 13);
            { var sk = new GameObject("caveira", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement)); sk.transform.SetParent(r.rt, false); var ri = sk.GetComponent<RawImage>(); ri.texture = Skull(); ri.color = head ? new Color(1, .28f, .22f) : new Color(1, 1, 1, .9f); ri.raycastTarget = false; var le = sk.GetComponent<LayoutElement>(); le.preferredWidth = 22; le.preferredHeight = 22; }
            if (head) I.Txt(r.rt, "CABEÇA", new Color(1, .3f, .25f), 12);
            I.Txt(r.rt, victim.Name.ToUpperInvariant(), ColorOf(victim));
        }
        public static void Message(string text, bool me) { if (!I) return; var r = I.NewRow(me); I.Txt(r.rt, text, me ? Me : new Color(1, 1, 1, .85f), 15); }

        // caveira desenhada em código (crânio, olhos, nariz e dentes) — branca no abate, vermelha no tiro na cabeça
        static Texture2D skull;
        static Texture2D Skull()
        {
            if (skull) return skull; const int N = 48; skull = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float u = (x + .5f) / N * 2 - 1, v = (y + .5f) / N * 2 - 1;                 // v para cima
                bool cranio = u * u / .62f + (v - .18f) * (v - .18f) / .5f < 1, maxila = Mathf.Abs(u) < .42f && v > -.78f && v < -.2f;
                bool olho = ((u + .3f) * (u + .3f) + (v - .08f) * (v - .08f) < .045f) || ((u - .3f) * (u - .3f) + (v - .08f) * (v - .08f) < .045f);
                bool nariz = Mathf.Abs(u) < .09f - (v + .3f) * .2f && v > -.38f && v < -.16f, dente = v < -.5f && v > -.74f && Mathf.Abs(u) < .38f && Mathf.Repeat((u + .38f) / .19f, 1) < .16f;
                float a = (cranio || maxila) && !olho && !nariz && !dente ? 1 : 0; skull.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            skull.Apply(); return skull;
        }
        // [parceiro 01/11] cronômetro por script (FBTempo, ligado só pelo teste do .exe); desligado custa só um teste de bool
        static readonly int __fbt_Update = FBTempo.Id("FBKillFeed.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            float y = 0, dt = Time.deltaTime;
            for (int i = rows.Count - 1; i >= 0; i--) { var r = rows[i]; r.t += dt; if (r.t > Life) { Destroy(r.rt.gameObject); rows.RemoveAt(i); } }
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i]; float slide = Mathf.Clamp01(r.t / .18f), fade = 1 - Mathf.Clamp01((r.t - (Life - .6f)) / .6f);
                r.cg.alpha = Mathf.Min(slide, fade); var target = new Vector2((1 - Mathf.SmoothStep(0, 1, slide)) * 80, -y);
                r.rt.anchoredPosition = Vector2.Lerp(r.rt.anchoredPosition, target, 1 - Mathf.Exp(-dt * 18)); if (r.t < dt * 1.5f) r.rt.anchoredPosition = target;
                y += H + Gap; r.rt.sizeDelta = new Vector2(r.rt.sizeDelta.x, H);
            }
        }
    }
}
