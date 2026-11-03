// Placas dos carros do REB (porte do splitPlates/plateTexture do vehicles.js). No REB a placa é parte da malha do atlas com UV própria
// (u 0..1, v 0,34..0,66) que recebia a RenderTexture do PlacaBaker; exportada junto com o atlas ela amostrava uma faixa qualquer da
// textura — placa com pedaço do atlas. Aqui: os triângulos da placa (virados para frente/trás, na ponta do carro, perto do centro, com UV
// larga dentro da faixa) viram um submesh com material próprio, e a textura Mercosul é desenhada por um TextMesh numa RenderTexture.
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public static class FBPlates
    {
        static readonly string[] POOL = Pool();
        static string[] Pool()
        {
            var r = new System.Random(7919); var o = new string[10]; const string L = "ABCDEFGHIJKLMNPQRSTUVWXYZ";
            for (int i = 0; i < o.Length; i++) o[i] = $"{"NOQR"[r.Next(4)]}{L[r.Next(25)]}{L[r.Next(25)]}{r.Next(10)}{L[r.Next(25)]}{r.Next(10)}{r.Next(10)}";
            return o;
        }
        public static string Of(Color paint) => POOL[Mathf.Abs((int)(paint.r * 255) * 31 + (int)(paint.g * 255) * 7 + (int)(paint.b * 255)) % POOL.Length];

        // separa os triângulos da placa das malhas 'atlas*' do carro (raiz = shell do LOD)
        public static void Split(Transform root, string text)
        {
            var inv = root.worldToLocalMatrix; var box = new Bounds(); bool first = true;
            var mfs = root.GetComponentsInChildren<MeshFilter>(true);
            // caixa no espaço da RAIZ pelos limites locais das malhas (r.bounds é a caixa do MUNDO: com o carro girado — peças do kit entram
            // com o carro já no lugar — ela inchava e a placa deixava de estar "na ponta do carro")
            foreach (var mf in mfs)
            {
                if (!mf.sharedMesh) continue; var lb = mf.sharedMesh.bounds; var M0 = inv * mf.transform.localToWorldMatrix;
                for (int k = 0; k < 8; k++) { var q = M0.MultiplyPoint3x4(new Vector3((k & 1) == 0 ? lb.min.x : lb.max.x, (k & 2) == 0 ? lb.min.y : lb.max.y, (k & 4) == 0 ? lb.min.z : lb.max.z)); if (first) { box = new Bounds(q, Vector3.zero); first = false; } else box.Encapsulate(q); }
            }
            foreach (var mf in mfs)
            {
                var mr = mf.GetComponent<MeshRenderer>(); var m = mf.sharedMesh; if (!mr || !m || !m.isReadable) continue;
                var mats = mr.sharedMaterials; int atlasSub = -1;
                for (int k = 0; k < Mathf.Min(mats.Length, m.subMeshCount); k++) if (mats[k] && mats[k].name.StartsWith("atlas")) { atlasSub = k; break; }   // carroceria do REB: vários submeshes (pintura, vidro, atlas)
                if (atlasSub < 0) continue;
                var P = m.vertices; var U = new List<Vector2>(); m.GetUVs(0, U); if (U.Count != P.Length) continue;
                var I = m.GetTriangles(atlasSub); var M = inv * mf.transform.localToWorldMatrix; var keep = new List<int>(I.Length); var plate = new List<int>();
                for (int i = 0; i < I.Length; i += 3)
                {
                    Vector3 a = M.MultiplyPoint3x4(P[I[i]]), b = M.MultiplyPoint3x4(P[I[i + 1]]), c = M.MultiplyPoint3x4(P[I[i + 2]]);
                    var n = Vector3.Cross(b - a, c - a); float L = n.magnitude; if (L < 1e-9f) { keep.Add(I[i]); keep.Add(I[i + 1]); keep.Add(I[i + 2]); continue; }
                    float nz = n.z / L, cz = (a.z + b.z + c.z) / 3, cx = (a.x + b.x + c.x) / 3;
                    float v0 = U[I[i]].y, v1 = U[I[i + 1]].y, v2 = U[I[i + 2]].y, u0 = U[I[i]].x, u1 = U[I[i + 1]].x, u2 = U[I[i + 2]].x;
                    float vmin = Mathf.Min(v0, Mathf.Min(v1, v2)), vmax = Mathf.Max(v0, Mathf.Max(v1, v2)), ext = Mathf.Max(Mathf.Max(u0, Mathf.Max(u1, u2)) - Mathf.Min(u0, Mathf.Min(u1, u2)), vmax - vmin);
                    bool isP = Mathf.Abs(nz) > .75f && vmin > .28f && vmax < .72f && ext > .15f && Mathf.Abs(cx) < .45f && (cz > box.max.z - .6f || cz < box.min.z + .6f);
                    (isP ? plate : keep).AddRange(new[] { I[i], I[i + 1], I[i + 2] });
                }
                if (plate.Count == 0) continue;
                var nm = Object.Instantiate(m); nm.name = m.name + "_placa"; int sc = m.subMeshCount; nm.subMeshCount = sc + 1;
                for (int k = 0; k < sc; k++) nm.SetTriangles(k == atlasSub ? keep.ToArray() : m.GetTriangles(k), k);
                plate = GeoUV(nm, plate, M);                                                                // UV da placa pela geometria (não a do atlas)
                nm.SetTriangles(plate, sc); mf.sharedMesh = nm;
                var nmats = new Material[sc + 1]; for (int k = 0; k < sc; k++) nmats[k] = k < mats.Length ? mats[k] : mats[mats.Length - 1]; nmats[sc] = Mat(text); mr.sharedMaterials = nmats;
                Debug.Log($"[FreeBala] placa: {mf.name} → {plate.Count / 3} triângulos");
            }
        }

        // troca o texto das placas do carro: submesh separado pelo Split ("veh_plate") e a malha "placa" dos carros portados (Kadett/Clio:
        // a UV deles já é a faixa v 0,339–0,661 da textura). Antes os portados ficavam com a placa lisa (material "placa" sem textura).
        // false = o modelo não tem placa própria
        public static bool SetText(Transform root, string text)
        {
            if (!root || string.IsNullOrEmpty(text)) return false; bool any = false; var m = Mat(text);
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var ms = mr.sharedMaterials; bool ch = false;
                for (int k = 0; k < ms.Length; k++) if (ms[k] && (ms[k].name.StartsWith("veh_plate") || ms[k].name.StartsWith("placa")) && ms[k] != m) { ms[k] = m; ch = true; }
                if (ch) mr.sharedMaterials = ms;
                any |= ch || System.Array.IndexOf(ms, m) >= 0;
            }
            return any;
        }

        // A UV que chega no .glb é a do ATLAS (o exportador usa a UV1 do REB para tudo que não é lataria): a placa Mercosul aparecia como um
        // pedaço ampliado das letras ("mosaico" preto). Aqui cada placa (frente / trás, pela normal) ganha vértices próprios com UV pela
        // GEOMETRIA: u de um lado ao outro como quem olha a placa de fora, v na faixa 0,339–0,661 da textura.
        static List<int> GeoUV(Mesh nm, List<int> plate, Matrix4x4 M)
        {
            var V = new List<Vector3>(); nm.GetVertices(V); int n0 = V.Count;
            var Nn = new List<Vector3>(); nm.GetNormals(Nn); var Tg = new List<Vector4>(); nm.GetTangents(Tg); var Cl = new List<Color32>(); nm.GetColors(Cl);
            bool hn = Nn.Count == n0, ht = Tg.Count == n0, hc = Cl.Count == n0;
            var uvs = new List<Vector4>[4]; var has = new bool[4]; for (int ch = 0; ch < 4; ch++) { uvs[ch] = new List<Vector4>(); nm.GetUVs(ch, uvs[ch]); has[ch] = uvs[ch].Count == n0; }
            var outIdx = new List<int>(plate.Count);
            for (int side = 0; side < 2; side++)
            {
                var tris = new List<int>();
                for (int i = 0; i + 2 < plate.Count; i += 3)
                {
                    Vector3 a = M.MultiplyPoint3x4(V[plate[i]]), b = M.MultiplyPoint3x4(V[plate[i + 1]]), c = M.MultiplyPoint3x4(V[plate[i + 2]]);
                    float nz = Vector3.Cross(b - a, c - a).z; if ((nz > 0) == (side == 0)) { tris.Add(plate[i]); tris.Add(plate[i + 1]); tris.Add(plate[i + 2]); }
                }
                if (tris.Count == 0) continue;
                float x0 = 1e9f, x1 = -1e9f, y0 = 1e9f, y1 = -1e9f; foreach (var v in tris) { var p = M.MultiplyPoint3x4(V[v]); x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); y0 = Mathf.Min(y0, p.y); y1 = Mathf.Max(y1, p.y); }
                float w = Mathf.Max(1e-4f, x1 - x0), h = Mathf.Max(1e-4f, y1 - y0); bool front = side == 0;
                foreach (var v in tris)
                {
                    var p = M.MultiplyPoint3x4(V[v]); float u = front ? (x1 - p.x) / w : (p.x - x0) / w, vv = .339f + (p.y - y0) / h * .322f;
                    V.Add(V[v]); if (hn) Nn.Add(Nn[v]); if (ht) Tg.Add(Tg[v]); if (hc) Cl.Add(Cl[v]);
                    for (int ch = 0; ch < 4; ch++) if (has[ch]) uvs[ch].Add(ch == 0 ? new Vector4(u, vv, 0, 0) : uvs[ch][v]);
                    outIdx.Add(V.Count - 1);
                }
            }
            if (V.Count > 65000 && nm.indexFormat == UnityEngine.Rendering.IndexFormat.UInt16) nm.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            nm.SetVertices(V); if (hn) nm.SetNormals(Nn); if (ht) nm.SetTangents(Tg); if (hc) nm.SetColors(Cl);
            for (int ch = 0; ch < 4; ch++) if (has[ch]) nm.SetUVs(ch, uvs[ch]);
            return outIdx;
        }

        static readonly Dictionary<string, Material> MATS = new();
        public static Material Mat(string text)
        {
            if (MATS.TryGetValue(text, out var m) && m) return m;
            m = new Material(FBShaders.Lit) { name = "veh_plate" }; m.SetTexture("_BaseMap", Bake(text)); m.mainTexture = m.GetTexture("_BaseMap"); m.SetFloat("_Smoothness", .35f);
            return MATS[text] = m;
        }

        // textura 512² com a placa Mercosul na faixa v 33,9 %–66,1 % (proporções do plateTexture do web), montada em PIXELS a partir de
        // Resources/UI/Placa (placa_base + placa_fonte, geradas por tools/placa_assets.py). Antes: câmera + TextMesh numa RenderTexture —
        // no build a fonte 3D/RT saíam vazias e a placa ficava sem textura.
        const string CH = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"; static Texture2D baseTx, fontTx; static bool dumped;
        static Texture2D Load(string n) { var ta = Resources.Load<TextAsset>("UI/Placa/" + n); if (!ta) return null; var t = new Texture2D(2, 2, TextureFormat.RGBA32, false); t.LoadImage(ta.bytes); return t; }
        static Texture Bake(string text)
        {
            baseTx ??= Load("placa_base.png"); fontTx ??= Load("placa_fonte.png");
            const int N = 512; var px = new Color32[N * N]; var bg = new Color32(240, 240, 235, 255); for (int i = 0; i < px.Length; i++) px[i] = bg;
            int y0 = Mathf.RoundToInt(N * .339f), y1 = Mathf.RoundToInt(N * .661f), bh = y1 - y0;
            if (baseTx)
                for (int y = y0; y < y1; y++) for (int x = 0; x < N; x++) px[y * N + x] = baseTx.GetPixelBilinear((x + .5f) / N, (y - y0 + .5f) / bh);
            if (fontTx)
            {
                var code = text.ToUpperInvariant(); if (code.Length > 7) code = code.Substring(0, 7);
                float gh = bh * .52f, gw = gh * 56f / 96f, total = gw * code.Length, xs = (N - total) / 2 + N * .02f, yc = y0 + bh * .36f;   // linha do código: abaixo da faixa azul
                for (int k = 0; k < code.Length; k++)
                {
                    int ci = CH.IndexOf(code[k]); if (ci < 0) continue; float gx0 = xs + k * gw;
                    for (int y = Mathf.FloorToInt(yc - gh / 2); y < yc + gh / 2; y++) for (int x = Mathf.FloorToInt(gx0); x < gx0 + gw; x++)
                    {
                        if (x < 0 || x >= N || y < 0 || y >= N) continue; float u = (x - gx0) / gw, v = (y - (yc - gh / 2)) / gh;
                        var g = fontTx.GetPixelBilinear((ci + u) / CH.Length, v); if (g.a <= .01f) continue;
                        px[y * N + x] = Color32.Lerp(px[y * N + x], new Color32(18, 18, 18, 255), g.a);
                    }
                }
            }
            var t = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "placa_" + text, anisoLevel = 4, wrapMode = TextureWrapMode.Clamp }; t.SetPixels32(px); t.Apply(true, false);
            if (!dumped && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbshow") >= 0)   // conferência: amostra da placa em <build>/autoteste
            { dumped = true; try { var dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste"); System.IO.Directory.CreateDirectory(dir); System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "placa_amostra.png"), t.EncodeToPNG()); } catch { } }
            t.Apply(false, true); return t;
        }
    }
}
