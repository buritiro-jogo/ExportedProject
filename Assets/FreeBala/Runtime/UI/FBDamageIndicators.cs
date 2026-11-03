// INDICADORES DE DIREÇÃO DO DANO (rodada 37 — "levo tiro sem saber de onde vem"): antes era UMA seta, calculada só no instante do
// acerto (girar a câmera deixava a seta apontando errado) e sobrescrita por qualquer outro atirador.
// Agora: até 6 arcos no anel em volta da mira, um por atacante; cada arco ACOMPANHA o atacante (posição atual enquanto vivo; a última
// conhecida se ele sumir) e a câmera (inclusive a câmera livre), some em 2,2 s e reacende a cada novo acerto. Mais dano = arco mais forte;
// de longe o arco fica mais largo (direção aproximada); vindo de trás o arco ganha uma seta. Dano sem atacante (queda, zona): nada.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FreeBala
{
    public class FBDamageIndicators : MonoBehaviour
    {
        static FBDamageIndicators inst; public static FBDamageIndicators I => inst ? inst : null;
        class Ind { public FBCharacter src; public Vector3 at; public float t, k; public Image arc, tip; public RectTransform rt; }
        readonly List<Ind> list = new(); RectTransform root; const float Life = 2.2f;
        static Sprite arcS, tipS;
        static Sprite Arc => arcS ? arcS : arcS = Gen(256, (x, y) =>
        {   // anel de raio .86..1 com 46° de abertura centrado no topo, bordas suaves
            float dx = x - .5f, dy = y - .5f, r = Mathf.Sqrt(dx * dx + dy * dy) * 2, a = Mathf.Abs(Mathf.Atan2(dx, dy)) * Mathf.Rad2Deg;
            float ring = Mathf.Clamp01(1 - Mathf.Abs(r - .93f) / .07f), ang = Mathf.Clamp01((23 - a) / 6f);
            return ring * ang * (.55f + .45f * Mathf.Clamp01((r - .86f) / .14f));
        });
        static Sprite Tip => tipS ? tipS : tipS = Gen(64, (x, y) => { float X = (x - .5f) * 2, Y = y; return Y > .1f && Y < .9f && Mathf.Abs(X) < (.9f - Y) * .9f ? 1 : 0; });
        static Sprite Gen(int n, System.Func<float, float, float> a)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; var px = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(a((x + .5f) / n, (y + .5f) / n)));
            t.SetPixels(px); t.Apply(); return Sprite.Create(t, new Rect(0, 0, n, n), Vector2.one * .5f);
        }
        public static void Ensure(Transform hudRoot)
        {
            if (I) return; var g = new GameObject("DirecaoDano", typeof(RectTransform)); g.transform.SetParent(hudRoot, false);
            var rt = (RectTransform)g.transform; rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f); rt.sizeDelta = Vector2.zero; inst = g.AddComponent<FBDamageIndicators>(); inst.root = rt;
        }
        public static void Hit(FBCharacter src, Vector3 from, float dmg)
        {
            if (!I) return; var me = FBMatch.Player; if (!me) return;
            Ind ind = null; foreach (var x in I.list) if (src && x.src == src) { ind = x; break; }
            if (ind == null)
            {
                if (I.list.Count >= 6) { var old = I.list[0]; foreach (var x in I.list) if (x.t < old.t) old = x; ind = old; } else { ind = I.Make(); I.list.Add(ind); }
                ind.src = src; ind.k = 0;
            }
            ind.at = from; ind.t = Life; ind.k = Mathf.Clamp01(ind.k * .6f + .45f + dmg / 60f);
        }
        Ind Make()
        {
            var g = new GameObject("arco", typeof(RectTransform), typeof(Image)); g.transform.SetParent(root, false); var im = g.GetComponent<Image>(); im.sprite = Arc; im.raycastTarget = false;
            var rt = (RectTransform)g.transform; rt.sizeDelta = Vector2.one * 300;
            var tg = new GameObject("seta", typeof(RectTransform), typeof(Image)); tg.transform.SetParent(g.transform, false); var ti = tg.GetComponent<Image>(); ti.sprite = Tip; ti.raycastTarget = false;
            var trt = (RectTransform)tg.transform; trt.sizeDelta = new Vector2(18, 16); trt.anchoredPosition = new Vector2(0, 163);
            return new Ind { arc = im, tip = ti, rt = rt };
        }
        // [parceiro] v1 (thread de física): tempo do Update medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
        static readonly int __fbt_Update = FBTempo.Id("FBDamageIndicators.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            var me = FBMatch.Player; var pl = FBPlayer.I; float dt = Time.deltaTime;
            float camYaw = pl ? pl.ViewYaw : Camera.main ? Camera.main.transform.eulerAngles.y : 0;
            float S = Screen.height / 1080f;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var d = list[i]; d.t -= dt;
                if (d.t <= 0 || !me || !me.alive) { d.arc.color = d.tip.color = Color.clear; if (d.t <= 0) { Destroy(d.rt.gameObject); list.RemoveAt(i); } continue; }
                if (d.src && d.src.alive) d.at = d.src.Pos;
                var v = d.at - me.Pos; float dist = v.Flat().magnitude; float a = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg - camYaw;
                d.rt.localRotation = Quaternion.Euler(0, 0, -a); float far = Mathf.InverseLerp(15, 70, dist);
                d.rt.sizeDelta = Vector2.one * 300 * S; d.rt.localScale = new Vector3(1 + far * .55f, 1, 1);                              // longe = arco mais largo
                float alpha = Mathf.Clamp01(d.t / .6f) * (.45f + .55f * d.k); var col = new Color(1, .16f + .2f * (1 - d.k), .1f, alpha);
                d.arc.color = col; bool behind = Mathf.Abs(Mathf.DeltaAngle(a, 0)) > 100; d.tip.color = behind ? col : Color.clear;
                d.tip.rectTransform.anchoredPosition = new Vector2(0, 163 * S); d.tip.rectTransform.sizeDelta = new Vector2(18, 16) * S;
            }
        }
        void OnDestroy() { if (inst == this) inst = null; }
    }
}
