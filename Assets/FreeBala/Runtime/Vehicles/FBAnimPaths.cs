// r45d: trajetória do corpo (RootT do humanoide) dos clipes de transição do 171 (tools/r45b/anim_paths.py -> Resources/FBAnimPaths.json).
// Pedido: "as animações de subir nas motos, descer das motos, entrar e sair dos carros estão todas quebradas". Causa: o clipe leva o corpo
// 1,23 m e 0,32 m para baixo no carro DO 171 (outra largura e altura de banco); aqui o quadril ia pela trajetória do clipe e depois era
// puxado (lerp) para o banco -> deslizava, afundava ou atravessava a porta. Agora o clipe só dá o RITMO e a FORMA do caminho: progresso
// horizontal (projeção na reta início->fim), progresso vertical e desvio lateral/vertical da reta; o FBDrive leva o quadril pela reta
// "lado de fora da porta (em pé) -> banco" do veículo real com esse ritmo. Começa exatamente em pé e termina exatamente sentado.
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public static class FBAnimPaths
    {
        class P { public Vector3[] t; public Vector3 hd; public float hl, dy; }
        static Dictionary<string, P> map;
        static float N(object o) => o is double d ? (float)d : o is long l ? l : o is int i ? i : 0;
        static void Load()
        {
            map = new Dictionary<string, P>(); var ta = Resources.Load<TextAsset>("FBAnimPaths"); if (!ta) return;
            if (!(Json.Parse(ta.text) is Dictionary<string, object> d)) return;
            foreach (var kv in d)
            {
                var L = Json.L(kv.Value, "t"); if (L == null || L.Count < 2) continue; var t = new Vector3[L.Count];
                for (int i = 0; i < L.Count; i++) { var q = L[i] as List<object>; t[i] = q != null && q.Count >= 3 ? new Vector3(N(q[0]), N(q[1]), N(q[2])) : Vector3.zero; }
                var h = t[^1] - t[0]; h.y = 0; map[kv.Key] = new P { t = t, hd = h.sqrMagnitude > 1e-6f ? h.normalized : Vector3.forward, hl = Mathf.Max(.05f, h.magnitude), dy = t[^1].y - t[0].y };
            }
        }
        // deslocamento do corpo desde o começo do clipe (unidades do humanoide, eixos da raiz do clipe) — raiz manual da queda (FBFall)
        public static bool Offset(string clip, float k, out Vector3 d)
        {
            d = Vector3.zero; if (map == null) Load(); if (clip == null || !map.TryGetValue(clip, out var p)) return false;
            float f = Mathf.Clamp01(k) * (p.t.Length - 1); int i = Mathf.Min((int)f, p.t.Length - 2); d = Vector3.Lerp(p.t[i], p.t[i + 1], f - i) - p.t[0]; return true;
        }
        public static bool Has(string clip) { if (map == null) Load(); return clip != null && map.ContainsKey(clip); }
        // k = tempo normalizado do clipe (0..1). wh = progresso horizontal, wv = progresso vertical (0 no início, 1 no fim);
        // lat = desvio para a direita da reta (unidades do humanoide), bump = desvio vertical quando o clipe quase não sobe/desce (passar a perna)
        public static bool Eval(string clip, float k, out float wh, out float wv, out float lat, out float bump)
        {
            wh = wv = Mathf.Clamp01(k); lat = bump = 0; if (map == null) Load(); if (clip == null || !map.TryGetValue(clip, out var p)) return false;
            float f = Mathf.Clamp01(k) * (p.t.Length - 1); int i = Mathf.Min((int)f, p.t.Length - 2); var d = Vector3.Lerp(p.t[i], p.t[i + 1], f - i) - p.t[0];
            var dh = new Vector3(d.x, 0, d.z); float along = Vector3.Dot(dh, p.hd); wh = along / p.hl;
            lat = Vector3.Dot(dh - p.hd * along, Vector3.Cross(Vector3.up, p.hd));
            // r48: o retarget nativo levou o subir da moto (Go_to_bike_L) de dy +1,7 para −7 cm e ele passou para a razão — testado o
            // critério relativo (lombada): com o banco da XJ6 ~12 cm abaixo do quadril em pé a razão sobe o quadril 3,6 cm ao passar a perna
            // e a lombada o deixava 0,7 cm abaixo (sonda r48h: igual ou pior). Fica a razão; a perna passa por cima pelo FBLegClear.
            if (Mathf.Abs(p.dy) > .04f) { wv = d.y / p.dy; bump = 0; } else { wv = wh; bump = d.y - p.dy * wh; }
            return true;
        }
    }
}
