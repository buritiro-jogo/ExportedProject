// NAVEGAÇÃO DOS PEDESTRES do De Rolê (como o nav.js do web: "rua custa 6× para pedestre"): A* nos triângulos da NavMesh do mapa com
// custo alto no asfalto (grade de rua do FBRoads) -> o caminho fica na calçada/praça e só cruza a rua onde precisa, o mais curto possível;
// depois o funil (string pulling) estica o caminho dentro do corredor de triângulos (sem zigue-zague de centro em centro).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace FreeBala
{
    public static class FBPedNav
    {
        const float ROAD_COST = 6f, CELL = 4f;
        static Vector3[] V; static int[] I; static Vector3[] C; static float[] W; static int[][] nb; static int[][] nbEdge;   // nb: vizinho por aresta (-1)
        static Dictionary<long, List<int>> grid; static float gx0, gz0; public static bool Ready => C != null && C.Length > 0;
        static string built;

        public static void Build(string key)
        {
            if (built == key && Ready) return; built = key;
            var t = NavMesh.CalculateTriangulation(); V = t.vertices; I = t.indices; int n = I.Length / 3;
            C = new Vector3[n]; W = new float[n]; nb = new int[n][]; grid = new Dictionary<long, List<int>>();
            if (n == 0) return;
            gx0 = float.MaxValue; gz0 = float.MaxValue; foreach (var v in V) { gx0 = Mathf.Min(gx0, v.x); gz0 = Mathf.Min(gz0, v.z); }
            long K(Vector3 p) => ((long)Mathf.RoundToInt(p.x * 50) * 73856093) ^ ((long)Mathf.RoundToInt(p.y * 20) * 19349663) ^ ((long)Mathf.RoundToInt(p.z * 50) * 83492791);
            var edges = new Dictionary<(long, long), (int tri, int e)>();
            for (int i = 0; i < n; i++)
            {
                Vector3 a = V[I[i * 3]], b = V[I[i * 3 + 1]], c = V[I[i * 3 + 2]]; C[i] = (a + b + c) / 3; nb[i] = new[] { -1, -1, -1 };
                W[i] = FBRoads.Ready && FBRoads.IsRoad(C[i]) ? ROAD_COST : 1;
                for (int e = 0; e < 3; e++)
                {
                    var p = V[I[i * 3 + e]]; var q = V[I[i * 3 + (e + 1) % 3]]; long x = K(p), y = K(q); var key2 = x < y ? (x, y) : (y, x);
                    if (edges.TryGetValue(key2, out var o)) { nb[i][e] = o.tri; nb[o.tri][o.e] = i; } else edges[key2] = (i, e);
                }
                float x0 = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), x1 = Mathf.Max(a.x, Mathf.Max(b.x, c.x)), z0 = Mathf.Min(a.z, Mathf.Min(b.z, c.z)), z1 = Mathf.Max(a.z, Mathf.Max(b.z, c.z));
                for (int gx = Mathf.FloorToInt((x0 - gx0) / CELL); gx <= Mathf.FloorToInt((x1 - gx0) / CELL); gx++)
                    for (int gz = Mathf.FloorToInt((z0 - gz0) / CELL); gz <= Mathf.FloorToInt((z1 - gz0) / CELL); gz++)
                    { long gk = ((long)gx << 32) ^ (uint)gz; if (!grid.TryGetValue(gk, out var l)) grid[gk] = l = new List<int>(); l.Add(i); }
            }
        }
        static float Cross(Vector3 a, Vector3 b) => a.x * b.z - a.z * b.x;
        static bool InTri(int t, Vector3 p)
        {
            Vector3 a = V[I[t * 3]], b = V[I[t * 3 + 1]], c = V[I[t * 3 + 2]];
            float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0; return !(neg && pos);
        }
        public static int Locate(Vector3 p)
        {
            if (!Ready) return -1; int gx = Mathf.FloorToInt((p.x - gx0) / CELL), gz = Mathf.FloorToInt((p.z - gz0) / CELL); int best = -1; float bd = float.MaxValue;
            for (int r = 0; r <= 2 && best < 0; r++)
                for (int dx = -r; dx <= r; dx++) for (int dz = -r; dz <= r; dz++)
                    {
                        if (grid.TryGetValue(((long)(gx + dx) << 32) ^ (uint)(gz + dz), out var l))
                            foreach (var t in l)
                            {
                                float dy = Mathf.Abs(C[t].y - p.y); if (dy > 2.5f) continue;
                                if (InTri(t, p)) { if (dy < bd) { bd = dy; best = t; } }
                                else if (best < 0) { float d = (C[t] - p).sqrMagnitude + 1e4f; if (d < bd) { bd = d; best = t; } }
                            }
                    }
            return best;
        }
        static readonly Dictionary<int, float> g = new(); static readonly Dictionary<int, int> from = new();
        // caminho de 'a' até 'b' (cantos do funil, sem o ponto de partida); null se não achou
        public static List<Vector3> Path(Vector3 a, Vector3 b, int maxNodes = 9000)
        {
            int s = Locate(a), t = Locate(b); if (s < 0 || t < 0) return null;
            if (s == t) return new List<Vector3> { b };
            g.Clear(); from.Clear(); var open = new SortedSet<(float f, int id)>(); g[s] = 0; open.Add((H(s, t), s)); int n = 0;
            while (open.Count > 0 && n++ < maxNodes)
            {
                var cur = open.Min; open.Remove(cur); int u = cur.id; if (u == t) break;
                for (int e = 0; e < 3; e++)
                {
                    int v = nb[u][e]; if (v < 0) continue;
                    float ng = g[u] + Vector3.Distance(C[u], C[v]) * (W[u] + W[v]) * .5f;
                    if (g.TryGetValue(v, out var old)) { if (old <= ng) continue; open.Remove((old + H(v, t), v)); }
                    g[v] = ng; from[v] = u; open.Add((ng + H(v, t), v));
                }
            }
            if (!from.ContainsKey(t)) return null;
            var tris = new List<int> { t }; while (tris[^1] != s) tris.Add(from[tris[^1]]); tris.Reverse();
            return Funnel(a, b, tris);
        }
        static float H(int a, int b) => Vector3.Distance(C[a], C[b]);
        // funil: portais (esquerda, direita) entre triângulos vizinhos; esquerda = lado anti-horário (vista de cima)
        static List<Vector3> Funnel(Vector3 start, Vector3 end, List<int> tris)
        {
            var L = new List<Vector3> { start }; var R = new List<Vector3> { start };
            for (int i = 0; i + 1 < tris.Count; i++)
            {
                int u = tris[i], w = tris[i + 1]; int e = System.Array.IndexOf(nb[u], w); if (e < 0) continue;
                var p = V[I[u * 3 + e]]; var q = V[I[u * 3 + (e + 1) % 3]]; var d = C[w] - C[u];
                if (Cross(d, p - C[u]) > 0) { L.Add(p); R.Add(q); } else { L.Add(q); R.Add(p); }
            }
            L.Add(end); R.Add(end);
            var path = new List<Vector3>(); Vector3 apex = start, pl = L[0], pr = R[0]; int ai = 0, li = 0, ri = 0;
            for (int i = 1; i < L.Count; i++)
            {
                var nl = L[i]; var nr = R[i];
                if (Cross(pr - apex, nr - apex) >= 0)
                {
                    if ((apex - pr).sqrMagnitude < 1e-8f || Cross(pl - apex, nr - apex) < 0) { pr = nr; ri = i; }
                    else { path.Add(pl); apex = pl; ai = li; pl = pr = apex; li = ri = ai; i = ai; continue; }
                }
                if (Cross(pl - apex, nl - apex) <= 0)
                {
                    if ((apex - pl).sqrMagnitude < 1e-8f || Cross(pr - apex, nl - apex) > 0) { pl = nl; li = i; }
                    else { path.Add(pr); apex = pr; ai = ri; pl = pr = apex; li = ri = ai; i = ai; continue; }
                }
            }
            if (path.Count == 0 || (path[^1] - end).sqrMagnitude > 1e-4f) path.Add(end);
            return path;
        }
    }
}
