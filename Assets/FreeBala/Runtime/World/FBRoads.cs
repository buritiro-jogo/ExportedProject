// MALHA DE RUAS do mapa (trânsito e pedestres do De Rolê). Gerada fora do jogo a partir da colisão real (Editor/FBRoadScan.cs ->
// tools/roads/build_roads.py) e lida aqui de Resources/FBRoads/<mapa>.json:
//  · segmentos = faixas (mão direita; sentido único nas pistas separadas por canteiro e na rotatória) e conexões dentro dos cruzamentos
//    (curvas suaves de faixa a faixa, cada uma com a lista de conexões que se cruzam com ela — base da reserva do cruzamento);
//  · vagas (as do nível) com o caminho de entrada e o de saída (de ré nas inclinadas) ligados à faixa mais próxima;
//  · grade de rua de 1 m para "isto é asfalto?" barato — pedestre paga caro para andar na rua e procura atravessar reto.
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FreeBala
{
    public class FBSeg
    {
        public int id, kind, nl = 1, li, left = -1, right = -1, opp = -1, junction = -1; public bool one, ring; public char turn = 'S';
        public Vector3[] p; public float[] s; public float len, w = 6, vmax = 11; public int[] next = new int[0], conf = new int[0];
        public float[] vcurv;                                         // limite de velocidade pela curva em cada ponto (m/s)
        public readonly List<int> prev = new();
        public Bounds box;
        // ocupação em tempo de execução (FBTransito)
        public readonly List<object> occ = new();
    }
    public class FBSpot
    {
        public Vector3 p; public float yaw, s, sin, sout; public string kind; public int seg; public Vector3[] inp, outp, rev;
        public FBCar car; public object res;                         // carro parado nela / reserva de quem está manobrando
    }

    public static class FBRoads
    {
        public static readonly Regex RoadRx = new("asphalt|asfalto|road|tarmac", RegexOptions.IgnoreCase);
        // contêineres de COBERTURA (combate): somem no De Rolê. O pátio de contêineres da Praça do PT é cenário e fica.
        static readonly Regex CoverRx = new(@"^(PA_Conteiner|VJ_Cob_conteiner)|_Cob_conteiner", RegexOptions.IgnoreCase);
        public static bool IsCoverContainer(Transform t)
        {
            if (!t || !CoverRx.IsMatch(t.name)) return false;
            for (var p = t.parent; p; p = p.parent) if (CoverRx.IsMatch(p.name)) return false;   // só a raiz do grupo
            return true;
        }

        public static readonly List<FBSeg> segs = new(); public static readonly List<FBSpot> spots = new();
        public static readonly Dictionary<int, Vector3> junctions = new();
        static byte[] grid; static float gx0, gz0, gcell = 1; static int gnx, gnz; public static string loaded;
        public static bool Ready => segs.Count > 0;

        public static bool Load(string mapId)
        {
            if (loaded == mapId && Ready) return true;
            segs.Clear(); spots.Clear(); junctions.Clear(); grid = null; loaded = mapId;
            var ta = Resources.Load<TextAsset>("FBRoads/" + mapId); if (!ta) { Debug.Log($"[FreeBala][ruas] {mapId}: sem malha de faixas"); return false; }
            var J = Json.Parse(ta.text) as Dictionary<string, object>; if (J == null) return false;
            gx0 = Json.F(J, "gx0"); gz0 = Json.F(J, "gz0"); gcell = Json.F(J, "cell", 1); gnx = Json.I(J, "gnx"); gnz = Json.I(J, "gnz");
            try { grid = System.Convert.FromBase64String(Json.S(J, "road") ?? ""); } catch { grid = null; }
            foreach (var o in Json.L(J, "segs") ?? new List<object>())
            {
                var d = o as Dictionary<string, object>; var S = new FBSeg { id = Json.I(d, "id"), kind = Json.I(d, "k") };
                S.p = Pts(Json.L(d, "p")); S.next = Ints(Json.L(d, "n"));
                if (S.kind == 0) { S.w = Json.F(d, "w", 6); S.nl = Json.I(d, "nl", 1); S.li = Json.I(d, "li"); S.one = Json.I(d, "one") != 0; S.ring = Json.I(d, "ring") != 0; S.left = Json.I(d, "lf", -1); S.right = Json.I(d, "rt", -1); S.opp = Json.I(d, "op", -1); }
                else { var t = Json.S(d, "t"); S.turn = string.IsNullOrEmpty(t) ? 'S' : t[0]; S.junction = Json.I(d, "j", -1); S.conf = Ints(Json.L(d, "c")); }
                Measure(S); segs.Add(S);
            }
            foreach (var S in segs) foreach (var n in S.next) if (n >= 0 && n < segs.Count) segs[n].prev.Add(S.id);
            foreach (var S in segs) Speeds(S);
            foreach (var o in Json.L(J, "junctions") ?? new List<object>()) { var d = o as Dictionary<string, object>; var c = Json.L(d, "c"); if (c != null) junctions[Json.I(d, "id")] = new Vector3(Json.Fv(c[0]), Json.Fv(c[1]), Json.Fv(c[2])); }
            foreach (var o in Json.L(J, "spots") ?? new List<object>())
            {
                var d = o as Dictionary<string, object>; var p = Json.L(d, "p");
                var sp = new FBSpot { p = new Vector3(Json.Fv(p[0]), Json.Fv(p[1]), Json.Fv(p[2])), yaw = Json.F(d, "yaw"), kind = Json.S(d, "kind"), seg = Json.I(d, "seg"), s = Json.F(d, "s"), sin = Json.F(d, "sin"), sout = Json.F(d, "sout"),
                    inp = Pts(Json.L(d, "inp")), outp = Pts(Json.L(d, "outp")), rev = Pts(Json.L(d, "rev")) };
                if (sp.seg >= 0 && sp.seg < segs.Count && sp.inp.Length > 1 && sp.outp.Length > 1) spots.Add(sp);
            }
            Debug.Log($"[FreeBala][ruas] {mapId}: {segs.Count} segmentos, {spots.Count} vagas, {junctions.Count} cruzamentos");
            return Ready;
        }
        static Vector3[] Pts(List<object> l) { if (l == null) return new Vector3[0]; var r = new Vector3[l.Count / 3]; for (int i = 0; i < r.Length; i++) r[i] = new Vector3(Json.Fv(l[i * 3]), Json.Fv(l[i * 3 + 1]), Json.Fv(l[i * 3 + 2])); return r; }
        static int[] Ints(List<object> l) { if (l == null) return new int[0]; var r = new int[l.Count]; for (int i = 0; i < r.Length; i++) r[i] = (int)Json.Fv(l[i]); return r; }
        static void Measure(FBSeg S)
        {
            S.s = new float[S.p.Length]; float acc = 0; var b = new Bounds(S.p.Length > 0 ? S.p[0] : Vector3.zero, Vector3.zero);
            for (int i = 0; i < S.p.Length; i++) { if (i > 0) acc += Vector3.Distance(S.p[i - 1], S.p[i]); S.s[i] = acc; b.Encapsulate(S.p[i]); }
            S.len = acc; b.Expand(new Vector3(4, 4, 4)); S.box = b;
        }
        // velocidade: rua larga 14 m/s (≈ 42 km/h reais no mapa 1,2×), média 11,5, estreita 8,5; curva limita a √(2,6·R)
        static void Speeds(FBSeg S)
        {
            float w = S.kind == 0 ? S.w : 7; S.vmax = S.kind == 0 ? (S.one && w >= 9 ? 14f : w >= 7 ? 11.5f : 8.5f) : 9f;
            S.vcurv = new float[S.p.Length];
            for (int i = 0; i < S.p.Length; i++)
            {
                int a = Mathf.Max(0, i - 2), c = Mathf.Min(S.p.Length - 1, i + 2); float R = 1e4f;
                if (c - a >= 2) { var A = S.p[a]; var B = S.p[(a + c) / 2]; var C = S.p[c]; float ab = Vector3.Distance(A, B), bc = Vector3.Distance(B, C), ca = Vector3.Distance(C, A); float k = Vector3.Cross(B - A, C - A).magnitude; if (k > 1e-4f) R = ab * bc * ca / (2 * k); }
                S.vcurv[i] = Mathf.Clamp(Mathf.Sqrt(2.6f * R), 3.2f, 30);
            }
        }

        // ---- consultas
        public static bool IsRoad(Vector3 p)
        {
            if (grid == null) return false; int ix = Mathf.FloorToInt((p.x - gx0) / gcell), iz = Mathf.FloorToInt((p.z - gz0) / gcell);
            if (ix < 0 || iz < 0 || ix >= gnx || iz >= gnz) return false; int k = iz * gnx + ix; return (grid[k >> 3] & (1 << (k & 7))) != 0;
        }
        // ponto e tangente a 'd' m do começo do segmento (passa do fim -> extrapola na última direção)
        public static Vector3 At(FBSeg S, float d, out Vector3 tan)
        {
            var p = S.p; if (p.Length == 1) { tan = Vector3.forward; return p[0]; }
            if (d <= 0) { tan = (p[1] - p[0]).normalized; return p[0] + tan * d; }
            if (d >= S.len) { tan = (p[^1] - p[^2]).normalized; return p[^1] + tan * (d - S.len); }
            int lo = 0, hi = p.Length - 1; while (hi - lo > 1) { int m = (lo + hi) >> 1; if (S.s[m] <= d) lo = m; else hi = m; }
            float t = (d - S.s[lo]) / Mathf.Max(1e-5f, S.s[hi] - S.s[lo]); tan = (p[hi] - p[lo]).normalized; return Vector3.LerpUnclamped(p[lo], p[hi], t);
        }
        public static float VCurve(FBSeg S, float d)
        {
            if (S.vcurv == null || S.vcurv.Length == 0) return S.vmax; if (d <= 0) return S.vcurv[0]; if (d >= S.len) return S.vcurv[^1];
            int lo = 0, hi = S.p.Length - 1; while (hi - lo > 1) { int m = (lo + hi) >> 1; if (S.s[m] <= d) lo = m; else hi = m; }
            return Mathf.Min(S.vcurv[lo], S.vcurv[hi]);
        }
        // projeção num segmento: distância ao longo e lateral (+ = direita de quem anda nele)
        public static float Project(FBSeg S, Vector3 q, out float lat, out float dist)
        {
            float best = float.MaxValue, bs = 0, bl = 0; var p = S.p;
            for (int i = 0; i + 1 < p.Length; i++)
            {
                var a = p[i]; var b = p[i + 1]; var ab = b - a; ab.y = 0; float L2 = ab.sqrMagnitude; if (L2 < 1e-6f) continue;
                var aq = q - a; aq.y = 0; float t = Mathf.Clamp01(Vector3.Dot(aq, ab) / L2); var c = a + ab * t; var dq = q - c; dq.y = 0; float d2 = dq.sqrMagnitude;
                if (d2 < best) { best = d2; bs = S.s[i] + Mathf.Sqrt(L2) * t; var n = ab.normalized; bl = dq.x * n.z - dq.z * n.x; }
            }
            lat = bl; dist = Mathf.Sqrt(best); return bs;
        }
        // faixa (só kind 0) mais perto de p; dir: se dado, só as que andam para o mesmo lado (±60°)
        public static FBSeg NearestLane(Vector3 p, float maxD, out float s, out float lat, Vector3? dir = null)
        {
            FBSeg best = null; float bd = maxD; s = 0; lat = 0;
            foreach (var S in segs)
            {
                if (S.kind != 0) continue; var b = S.box; if (p.x < b.min.x - maxD || p.x > b.max.x + maxD || p.z < b.min.z - maxD || p.z > b.max.z + maxD) continue;
                float ss = Project(S, p, out var la, out var d); if (d >= bd) continue;
                if (dir is Vector3 dv) { At(S, ss, out var tn); if (Vector3.Dot(tn, dv.normalized) < .5f) continue; }
                bd = d; best = S; s = ss; lat = la;
            }
            return best;
        }
        // rota A* (segmento -> segmento); custo = tempo na velocidade da via + castigo por curva
        public static List<int> Route(int from, int to, int maxNodes = 20000)
        {
            if (from < 0 || to < 0 || from >= segs.Count || to >= segs.Count) return null;
            var g = new Dictionary<int, float> { [from] = 0 }; var came = new Dictionary<int, int>(); var open = new SortedSet<(float f, int id)> { (0, from) }; var goal = segs[to].p.Length > 0 ? segs[to].p[0] : Vector3.zero;
            int n = 0;
            while (open.Count > 0 && n++ < maxNodes)
            {
                var cur = open.Min; open.Remove(cur); int c = cur.id; if (c == to) break;
                var S = segs[c];
                foreach (var nx in S.next)
                {
                    var N = segs[nx]; float cost = N.len / Mathf.Max(3, N.vmax) + (N.kind == 1 ? (N.turn == 'L' ? 2.2f : N.turn == 'R' ? .9f : N.turn == 'U' ? 6 : .3f) : 0);
                    float ng = g[c] + cost; if (g.TryGetValue(nx, out var og) && og <= ng) continue;
                    if (g.ContainsKey(nx)) open.Remove((og + H(nx, goal), nx));
                    g[nx] = ng; came[nx] = c; open.Add((ng + H(nx, goal), nx));
                }
            }
            if (!came.ContainsKey(to) && from != to) return null;
            var r = new List<int> { to }; while (r[^1] != from) r.Add(came[r[^1]]); r.Reverse(); return r;
        }
        static float H(int id, Vector3 goal) { var S = segs[id]; var p = S.p.Length > 0 ? S.p[^1] : Vector3.zero; return new Vector2(p.x - goal.x, p.z - goal.z).magnitude / 14f; }
    }
}
