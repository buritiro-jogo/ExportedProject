// Transições da tela de personagem (sem pacote de tween): fade por CanvasGroup, slide por anchoredPosition, escala com leve "pop".
// Rodam em tempo não escalado, uma corrida por alvo (pedir de novo substitui a anterior) e com curva suave (easeOutCubic / easeOutBack).
// Também guarda a classe de proporção da tela (paisagem larga 21:9, 16:9, estreita 4:3, retrato do celular) para o layout responsivo.
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public class FBCustomizeFx : MonoBehaviour
    {
        public enum Aspect { Portrait, Narrow, Wide, Ultra }
        /// <summary>Retrato (&lt;1), estreito (4:3…3:2), 16:9 e ultralargo (21:9).</summary>
        public static Aspect Classify(float w, float h) { float a = w / Mathf.Max(1, h); return a < 1 ? Aspect.Portrait : a < 1.55f ? Aspect.Narrow : a < 2.05f ? Aspect.Wide : Aspect.Ultra; }
        public static Aspect Current => Classify(Screen.width, Screen.height);

        static FBCustomizeFx inst;
        static FBCustomizeFx I { get { if (!inst) { var g = new GameObject("FB_CustomizeFx"); inst = g.AddComponent<FBCustomizeFx>(); } return inst; } }
        class Tw { public Object key; public float t, dur, delay; public System.Action<float> step; public System.Action done; public bool back; }
        readonly Dictionary<(Object, int), Tw> runs = new(); readonly List<(Object, int)> dead = new();

        static float OutCubic(float x) => 1 - Mathf.Pow(1 - x, 3);
        static float OutBack(float x) { const float c1 = 1.70158f, c3 = c1 + 1; return 1 + c3 * Mathf.Pow(x - 1, 3) + c1 * Mathf.Pow(x - 1, 2); }
        static void Run(Object key, int ch, float dur, float delay, System.Action<float> step, System.Action done = null, bool back = false)
        {
            if (!key) return; step(0); I.runs[(key, ch)] = new Tw { key = key, dur = Mathf.Max(.01f, dur), delay = delay, step = step, done = done, back = back };
        }
        // [parceiro 01/11] cronômetro por script (FBTempo, ligado só pelo teste do .exe); desligado custa só um teste de bool
        static readonly int __fbt_Update = FBTempo.Id("FBCustomizeFx.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f); dead.Clear();
            foreach (var kv in runs)
            {
                var w = kv.Value; if (!w.key) { dead.Add(kv.Key); continue; }
                if (w.delay > 0) { w.delay -= dt; continue; }
                w.t += dt; float x = Mathf.Clamp01(w.t / w.dur); w.step(w.back ? OutBack(x) : OutCubic(x));
                if (x >= 1) dead.Add(kv.Key);
            }
            foreach (var k in dead) { if (runs.TryGetValue(k, out var w)) { runs.Remove(k); w.done?.Invoke(); } }
        }
        /// <summary>Termina na hora todas as transições (teste/prints).</summary>
        public static void Finish() { if (!inst) return; foreach (var w in inst.runs.Values) if (w.key) { w.step(1); w.done?.Invoke(); } inst.runs.Clear(); }

        public static CanvasGroup Group(Component c) { var g = c.GetComponent<CanvasGroup>(); return g ? g : c.gameObject.AddComponent<CanvasGroup>(); }
        public static void Fade(Component c, float from, float to, float dur = .22f, float delay = 0, System.Action done = null)
        {
            if (!c) return; var g = Group(c); Run(g, 0, dur, delay, k => { if (g) g.alpha = Mathf.LerpUnclamped(from, to, k); }, done);
        }
        /// <summary>Entra deslizando de (posição + offset) até a posição atual, com fade.</summary>
        public static void SlideIn(RectTransform r, Vector2 offset, float dur = .32f, float delay = 0)
        {
            if (!r) return; var end = r.anchoredPosition; var g = Group(r);
            Run(r, 1, dur, delay, k => { if (!r) return; r.anchoredPosition = Vector2.LerpUnclamped(end + offset, end, k); g.alpha = Mathf.Clamp01(k * 1.4f); });
        }
        public static void SlideOut(RectTransform r, Vector2 offset, float dur = .2f, System.Action done = null)
        {
            if (!r) return; var start = r.anchoredPosition; var g = Group(r);
            Run(r, 1, dur, 0, k => { if (!r) return; r.anchoredPosition = Vector2.LerpUnclamped(start, start + offset, k); g.alpha = 1 - k; }, done);
        }
        /// <summary>Escala com "pop" (easeOutBack) — seleção de cartão, troca de personagem.</summary>
        public static void Pop(Transform t, float from = .9f, float dur = .28f, float delay = 0, Vector3? to = null)
        {
            if (!t) return; var end = to ?? Vector3.one; Run(t, 2, dur, delay, k => { if (t) t.localScale = Vector3.LerpUnclamped(end * from, end, k); }, null, true);
        }
        public static void ScaleTo(Transform t, Vector3 to, float dur = .25f)
        {
            if (!t) return; var from = t.localScale; Run(t, 2, dur, 0, k => { if (t) t.localScale = Vector3.LerpUnclamped(from, to, k); });
        }
    }
}
