// RODADA 65 — MINIMAPA NEUTRO (estilo PUBG/Free Fire: leitura de PLANTA, não foto). Feito na carga da partida a partir do mundo real:
//  · área = tudo onde se anda (triangulação do NavMesh) com margem — nada fica cortado (antes: caixa das colisões limitada a 900 m,
//    centrada no meio da cidade inteira, que cortava pontas do mapa jogável);
//  · grade de raios de cima (RaycastCommand, em lote nos jobs): altura e normal de cada célula, só mapa + Default (sem veículos);
//  · chão = abertura morfológica das alturas numa grade grossa (tira prédios até ~28 m) → altura RELATIVA classifica prédio
//    (> 2,4 m), obstáculo baixo (muro, contêiner, carro parado: 0,5–2,4 m) e piso;
//  · rua pelo FBRoads (grade de asfalto de 1 m, a mesma do trânsito), água pelo FBWorld.WaterAt, vegetação pelo verde da foto,
//    acessível pelo NavMesh rasterizado (laje andável = prédio mais claro);
//  · paleta cinza neutra: fora do mapa escuro, piso médio, rua clara, prédio claro com contorno e sombra curta, relevo sombreado leve
//    e um pouco da textura da foto por baixo (sem cor saturada — os ícones do HUD é que têm cor).
// Ligado por fb_visual.json "minimap": "neutro" (FBVisual); "" = foto antiga.
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace FreeBala
{
    public static class FBMinimapNeutro
    {
        // área andável (XZ) do NavMesh, recortada pela caixa das colisões; null se não houver NavMesh
        public static Bounds? NavArea(Bounds cols, out NavMeshTriangulation tri)
        {
            tri = NavMesh.CalculateTriangulation(); var v = tri.vertices; if (v == null || v.Length < 3) return null;
            Vector3 mn = new(float.MaxValue, float.MaxValue, float.MaxValue), mx = -mn; int n = 0;
            foreach (var p in v) { if (!cols.Contains(new Vector3(p.x, cols.center.y, p.z))) continue; mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p); n++; }
            if (n < 3) return null; var b = new Bounds(); b.SetMinMax(mn, mx); return b;
        }

        static float Lum(Color32 c) => (c.r * .3f + c.g * .59f + c.b * .11f) / 255f;

        // photo: foto ortográfica (R×R) da mesma área; devolve a textura neutra G×G
        public static Texture2D Build(Texture2D photo, Vector3 center, float size, float yTop, float yDepth, NavMeshTriangulation tri, string mapId)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int G = FBQuality.Mobile ? 768 : 1536; float cell = size / G; float x0 = center.x - size * .5f, z0 = center.z - size * .5f;
            int N = G * G;
            // 1) alturas por raio
            var hy = new float[N]; var ny = new float[N]; var hit = new bool[N];
            var cmds = new NativeArray<RaycastCommand>(N, Allocator.TempJob); var res = new NativeArray<RaycastHit>(N, Allocator.TempJob);
            var qp = new QueryParameters((1 << FBWorld.LMap) | 1, false, QueryTriggerInteraction.Ignore, false);
            for (int j = 0; j < G; j++) for (int i = 0; i < G; i++)
                cmds[j * G + i] = new RaycastCommand(new Vector3(x0 + (i + .5f) * cell, yTop, z0 + (j + .5f) * cell), Vector3.down, qp, yDepth);
            RaycastCommand.ScheduleBatch(cmds, res, 512, 1).Complete();
            for (int k = 0; k < N; k++) { var h = res[k]; if (h.normal.sqrMagnitude > .5f) { hit[k] = true; hy[k] = h.point.y; ny[k] = h.normal.y; } }
            cmds.Dispose(); res.Dispose();
            float tRays = sw.ElapsedMilliseconds;

            // 2) chão: abertura (mín. depois máx.) numa grade grossa de ~2,8 m
            int F = 4, Gc = G / F; var gc = new float[Gc * Gc];
            float hMin = float.MaxValue; for (int k = 0; k < N; k++) if (hit[k] && hy[k] < hMin) hMin = hy[k]; if (hMin == float.MaxValue) hMin = 0;
            for (int j = 0; j < Gc; j++) for (int i = 0; i < Gc; i++)
            {
                float m = float.MaxValue; for (int b = 0; b < F; b++) for (int a = 0; a < F; a++) { int k = (j * F + b) * G + i * F + a; if (hit[k]) m = Mathf.Min(m, hy[k]); }
                gc[j * Gc + i] = m == float.MaxValue ? hMin : m;
            }
            int rc = Mathf.Max(1, Mathf.CeilToInt(14f / (cell * F)));
            var ground = Morph(Morph(gc, Gc, rc, true), Gc, rc, false);

            // 3) acessível: NavMesh rasterizado (altura do nav por célula)
            var navY = new float[N]; for (int k = 0; k < N; k++) navY[k] = float.NaN;
            var tv = tri.vertices; var ti = tri.indices;
            if (tv != null && ti != null)
                for (int t = 0; t + 2 < ti.Length; t += 3)
                {
                    Vector3 A = tv[ti[t]], B = tv[ti[t + 1]], C = tv[ti[t + 2]];
                    float ax = (A.x - x0) / cell, az = (A.z - z0) / cell, bx = (B.x - x0) / cell, bz = (B.z - z0) / cell, cx = (C.x - x0) / cell, cz = (C.z - z0) / cell;
                    int i0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ax, Mathf.Min(bx, cx)))), i1 = Mathf.Min(G - 1, Mathf.CeilToInt(Mathf.Max(ax, Mathf.Max(bx, cx))));
                    int j0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(az, Mathf.Min(bz, cz)))), j1 = Mathf.Min(G - 1, Mathf.CeilToInt(Mathf.Max(az, Mathf.Max(bz, cz))));
                    float den = (bz - cz) * (ax - cx) + (cx - bx) * (az - cz); if (Mathf.Abs(den) < 1e-6f) continue;
                    for (int j = j0; j <= j1; j++) for (int i = i0; i <= i1; i++)
                    {
                        float px = i + .5f, pz = j + .5f;
                        float w1 = ((bz - cz) * (px - cx) + (cx - bx) * (pz - cz)) / den, w2 = ((cz - az) * (px - cx) + (ax - cx) * (pz - cz)) / den, w3 = 1 - w1 - w2;
                        if (w1 < -.08f || w2 < -.08f || w3 < -.08f) continue;
                        float y = w1 * A.y + w2 * B.y + w3 * C.y; int k = j * G + i; if (float.IsNaN(navY[k]) || y > navY[k]) navY[k] = y;
                    }
                }

            // 4) classes
            var isRoad = FBRoads.RoadGridOnly(mapId); bool roads = isRoad != null; var pp = photo ? photo.GetPixels32() : null; int R = photo ? photo.width : 0;
            // 0 fora, 1 piso, 2 piso bloqueado, 3 rua, 4 vegetação, 5 água, 6 obstáculo, 7 prédio, 8 laje andável
            var cls = new byte[N]; var lum = new float[N];
            for (int j = 0; j < G; j++) for (int i = 0; i < G; i++)
            {
                int k = j * G + i; if (!hit[k]) { cls[k] = 0; continue; }
                float wx = x0 + (i + .5f) * cell, wz = z0 + (j + .5f) * cell;
                Color32 pc = pp != null ? pp[Mathf.Min(R - 1, (int)((j + .5f) / G * R)) * R + Mathf.Min(R - 1, (int)((i + .5f) / G * R))] : new Color32(110, 110, 110, 255);
                lum[k] = Lum(pc);
                float gnd = SampleC(ground, Gc, (i + .5f) / F - .5f, (j + .5f) / F - .5f), rel = hy[k] - gnd;
                bool walk = !float.IsNaN(navY[k]) && Mathf.Abs(navY[k] - hy[k]) < 1.2f;
                if (FBWorld.WaterAt(wx, wz) is float wy && hy[k] < wy + .3f) cls[k] = 5;
                else if (rel > 2.4f) cls[k] = (byte)(walk ? 8 : 7);
                else if (rel > .5f && !walk) cls[k] = 6;
                else if (roads && isRoad(new Vector3(wx, hy[k], wz))) cls[k] = 3;
                else if (pc.g > pc.r + 6 && pc.g > pc.b + 10 && !walk) cls[k] = 4;
                else cls[k] = (byte)(walk ? 1 : (pc.g > pc.r + 6 && pc.g > pc.b + 10 ? 4 : 2));
            }
            // 5) cor
            Color[] pal = {
                new(.115f, .12f, .125f), new(.335f, .34f, .345f), new(.285f, .29f, .295f), new(.47f, .47f, .475f), new(.255f, .285f, .26f),
                new(.22f, .285f, .33f), new(.53f, .535f, .54f), new(.69f, .695f, .70f), new(.76f, .765f, .77f) };
            var outp = new Color32[N]; int sh = Mathf.Max(1, Mathf.RoundToInt(1.6f / cell));   // sombra curta dos prédios (~1,6 m para SE)
            float lumMean = 0; int lc = 0; for (int k = 0; k < N; k++) if (cls[k] != 0) { lumMean += lum[k]; lc++; } lumMean = lc > 0 ? lumMean / lc : .4f;
            int nb = 0, nr = 0, nw = 0;
            for (int j = 0; j < G; j++) for (int i = 0; i < G; i++)
            {
                int k = j * G + i; byte c = cls[k]; Color col = pal[c];
                if (c == 7 || c == 8) nb++; else if (c == 3) nr++; if (c == 1 || c == 3 || c == 8) nw++;
                if (c != 0)
                {
                    // relevo (só no piso: prédio fica chapado e limpo)
                    if (c < 6)
                    {
                        float hl = hy[j * G + Mathf.Max(0, i - 1)], hr = hy[j * G + Mathf.Min(G - 1, i + 1)], hd = hy[Mathf.Max(0, j - 1) * G + i], hu = hy[Mathf.Min(G - 1, j + 1) * G + i];
                        float s = ((hl - hr) + (hu - hd)) / (2 * cell); col *= Mathf.Clamp(1 + s * .35f, .86f, 1.1f);
                        col += Color.white * Mathf.Clamp((lum[k] - lumMean) * .14f, -.05f, .05f);   // um pouco da textura da foto
                        // sombra do prédio vizinho (luz de NO)
                        int si = i - sh, sj = j + sh; if (si >= 0 && sj < G && cls[sj * G + si] >= 7) col *= .74f;
                    }
                    else if (c >= 7) col += Color.white * Mathf.Clamp((hy[k] - SampleC(ground, Gc, (i + .5f) / F - .5f, (j + .5f) / F - .5f) - 6f) * .006f, -.02f, .05f);   // prédio alto um tom mais claro
                    // contorno: prédio/obstáculo encostado em classe mais baixa
                    if (c >= 6)
                    {
                        bool edge = false;
                        for (int q = 0; q < 4 && !edge; q++) { int ii = i + (q == 0 ? 1 : q == 1 ? -1 : 0), jj = j + (q == 2 ? 1 : q == 3 ? -1 : 0); if (ii < 0 || jj < 0 || ii >= G || jj >= G) continue; byte o = cls[jj * G + ii]; edge = o < 6 || (c >= 7 && o == 6); }
                        if (edge) col = Color.Lerp(col, new Color(.17f, .175f, .18f), c >= 7 ? .75f : .5f);
                    }
                }
                col.a = 1; outp[k] = col;
            }
            var tex = new Texture2D(G, G, TextureFormat.RGB24, true) { anisoLevel = 4, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, name = "minimapa_neutro" };
            tex.SetPixels32(outp); tex.Apply(true, true);
            Debug.Log($"[FreeBala][minimapa] neutro {G}² ({cell:0.00} m/px) área {size:0} m · raios {tRays:0} ms, total {sw.ElapsedMilliseconds} ms · prédio {nb * 100f / N:0.0}% rua {nr * 100f / N:0.0}% andável {nw * 100f / N:0.0}% · ruas {(roads ? "grade FBRoads" : "sem malha")}");
            return tex;
        }

        // erosão (min) ou dilatação (max) quadrada separável, raio r
        static float[] Morph(float[] a, int n, int r, bool min)
        {
            var t = new float[a.Length]; var o = new float[a.Length];
            for (int j = 0; j < n; j++) for (int i = 0; i < n; i++)
            {
                float m = a[j * n + i]; for (int d = -r; d <= r; d++) { int ii = Mathf.Clamp(i + d, 0, n - 1); float v = a[j * n + ii]; m = min ? Mathf.Min(m, v) : Mathf.Max(m, v); }
                t[j * n + i] = m;
            }
            for (int j = 0; j < n; j++) for (int i = 0; i < n; i++)
            {
                float m = t[j * n + i]; for (int d = -r; d <= r; d++) { int jj = Mathf.Clamp(j + d, 0, n - 1); float v = t[jj * n + i]; m = min ? Mathf.Min(m, v) : Mathf.Max(m, v); }
                o[j * n + i] = m;
            }
            return o;
        }
        static float SampleC(float[] a, int n, float x, float y)
        {
            x = Mathf.Clamp(x, 0, n - 1.001f); y = Mathf.Clamp(y, 0, n - 1.001f); int i = (int)x, j = (int)y; float fx = x - i, fy = y - j;
            return Mathf.Lerp(Mathf.Lerp(a[j * n + i], a[j * n + i + 1], fx), Mathf.Lerp(a[(j + 1) * n + i], a[(j + 1) * n + i + 1], fx), fy);
        }
    }
}
