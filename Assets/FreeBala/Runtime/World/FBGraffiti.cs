// GRAFITES (porte do graffiti.js, gerador v4): a arte do usuário (assets/graffiti/atlas -> Resources/Graffiti/atlas.png + atlas.json no
// fb_data "graffiti") pintada nas paredes. Paredes vistas dos pontos fixos da navmesh (16 direções na altura do olho); cada parede vira uma
// grade de 20 cm: célula vale se o raio bate no MESMO objeto, no MESMO plano, sem nada colado na frente (janela = buraco; ar-condicionado,
// grade, caixa = na frente); acha os RETÂNGULOS MÁXIMOS de parede lisa que nascem no chão e estica a arte (±28 %) para preencher, com o
// tamanho limitado por tipo (mural / peça / tag). Prédios primeiro (2 por prédio), depois muros/galpões (4), 7 m entre artes, CADA ARTE UMA VEZ,
// sorteio com semente do mapa. Uma malha, um material.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public static class FBGraffiti
    {
        static Material mat; static GameObject go; static Dictionary<string, object> meta; static Texture2D atlas; static Color32[] alpha; static int aw, ah;
        static readonly System.Text.RegularExpressions.Regex BUILDING = new("VJ_Bloco_\\d+_(blue|orange|red|yellow|green|salmon)|VJ_Leste_\\d|VJ_Comercio|VJ_Bat_(Administrativo|Comando|Deposito|Reserva|Predio|Galpao)");
        static readonly System.Text.RegularExpressions.Regex NOT_WALL = new("car(paint|metal|glass)|veh|viatura|caveir|poste|pole|arvore|tree|palm|capim|grama|grass|lamp|glass|vidro|water|jet|rio|agua|cont[eê]i?ner|conteiner|container|BRP_|onibus|moto|lixeira|placa|sign_|let_|heart_mosaic|base_green|letreiro|tower|antena|mirante|kiosk_roof|roof|telha|rust|cobertura", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        static readonly System.Text.RegularExpressions.Regex PAINT_NO = new("iron|sill|glass|vidro|(^|_)ac$|roof|telha|lamp|tile|int_|steel|yellow_paint|fabric|numbers|wood|door|grime|alum|plastic|granite|stairs|sidewalk|concrete$|court|dirt|grass|road|asphalt|tapume|concertina|galv", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        static readonly System.Text.RegularExpressions.Regex BLD = new("VJ_Bloco_\\d+_(blue|orange|red|yellow|green|salmon)$");
        static readonly System.Text.RegularExpressions.Regex SKIPSEG = new("^mesh_|^_instance|^Scene$");
        const float C = .2f;
        static bool WallOk(string name) => BUILDING.IsMatch(name) || !NOT_WALL.IsMatch(name);
        static System.Func<float> Seeded(string s)
        {
            uint h = 2166136261; foreach (var c in s) h = (h ^ c) * 16777619;
            return () => { h = (h ^ (h >> 15)) * 2246822507; h = (h ^ (h >> 13)) * 3266489909; h ^= h >> 16; return h / 4294967296f; };
        }
        static bool Ensure()
        {
            if (mat) return true; meta = Json.O(GameData.Root, "graffiti"); atlas = Resources.Load<Texture2D>("Graffiti/atlas"); if (meta == null || !atlas) return false;
            mat = new Material(FBShaders.Lit) { name = "grafite" }; mat.SetTexture("_BaseMap", atlas); mat.mainTexture = atlas; mat.SetColor("_BaseColor", new Color(.94f, .925f, .894f)); mat.SetFloat("_Smoothness", .05f); mat.SetFloat("_Metallic", 0);
            mat = FBShaders.Transparent(mat, false, true); mat.SetFloat("_Cutoff", .03f); mat.renderQueue = 2995;   // modelo Lit_AlphaClip (variante garantida no build)
            return true;
        }
        // ---- consultas ao mapa (só a geometria do mapa: MeshCollider na camada Mapa)
        static bool MapRay(Vector3 o, Vector3 d, float far, out RaycastHit h) { if (Physics.Raycast(o, d, out h, far, 1 << FBWorld.LMap, QueryTriggerInteraction.Ignore) && h.collider is MeshCollider) return true; return false; }
        static readonly Dictionary<Collider, string> bkeyC = new();
        static string BKey(Collider c)
        {
            if (bkeyC.TryGetValue(c, out var k)) return k; var chain = new List<Transform>(); for (var t = c.transform; t; t = t.parent) chain.Add(t); chain.Reverse();
            k = chain.Skip(1).Select(t => t.name).FirstOrDefault(n => !SKIPSEG.IsMatch(n)) ?? c.name; bkeyC[c] = k; return k;
        }
        static string MatOf(RaycastHit h)
        {
            var mr = h.collider.GetComponent<Renderer>(); var mf = h.collider.GetComponent<MeshFilter>(); if (!mr) return ""; var ms = mr.sharedMaterials; string mat0 = ms.Length > 0 && ms[0] ? ms[0].name : "";
            if (mf && mf.sharedMesh && mf.sharedMesh.isReadable && mf.sharedMesh.subMeshCount > 1 && h.triangleIndex >= 0)
            { var m = mf.sharedMesh; int ti = h.triangleIndex * 3; for (int s = 0; s < m.subMeshCount; s++) { var sm = m.GetSubMesh(s); if (ti >= sm.indexStart && ti < sm.indexStart + sm.indexCount) return s < ms.Length && ms[s] ? ms[s].name : mat0; } }
            return mat0;
        }
        static bool WallHit(Vector3 o, Vector3 d, float far, out RaycastHit h) => MapRay(o, d, far, out h) && FBWorld.SurfaceAt(h) == 0 && Mathf.Abs(h.normal.y) <= .18f;
        class Wall { public string k, bk; public Collider col; public bool bld; public Vector3 n, t, hp; public float d, gy, s0, s1, top; public int vis, nx, nz; public byte[] mask; public bool analysed; public List<(int a, int b)> used = new(); public List<(int j0, int i0, int i1, int h)> rects; }
        // r43 — CACHE (carregamento: o posicionamento custava 5,6 s em TODA partida — milhares de raios dos pontos da navmesh + grade de
        // 20 cm por parede; o resultado é determinístico: só a geometria do mapa (camada Mapa) e sorteio com semente do mapa). Assinatura do
        // mapa = colisores de malha da camada Mapa (quantidade + posições arredondadas, independe da ordem); cache assado no editor
        // (Resources/Graffiti/cache_<mapa>.txt, FBGraffitiBake) ou gravado na 1.ª partida (persistentDataPath). Mapa mudou -> recalcula.
        public static bool BakeToAssets; public static string LastSource = "";
        static string Signature()
        {
            long n = 0, acc = 17; foreach (var c in Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (c.gameObject.layer != FBWorld.LMap || !c.enabled) continue; n++; var b = c.bounds.center;
                long h = (Mathf.RoundToInt(b.x * 4) * 73856093L) ^ (Mathf.RoundToInt(b.y * 4) * 19349663L) ^ (Mathf.RoundToInt(b.z * 4) * 83492791L); acc += h * 2654435761L;
            }
            return n + "-" + (acc & 0x7fffffffffffL).ToString("x");
        }
        static string CachePath(string key) => System.IO.Path.Combine(Application.persistentDataPath, "grafites", key + ".txt");
        static List<(string k, Vector3 c, Vector3 n, float w, float h)> ReadCache(string text, string sig)
        {
            if (string.IsNullOrEmpty(text)) return null; var lines = text.Split((char)10); if (lines.Length == 0 || lines[0].Trim() != sig) return null;
            var ic = System.Globalization.CultureInfo.InvariantCulture; var l = new List<(string, Vector3, Vector3, float, float)>();
            for (int i = 1; i < lines.Length; i++)
            {
                var f = lines[i].Trim().Split('|'); if (f.Length < 9) continue; float F(int j) => float.Parse(f[j], ic);
                l.Add((f[0], new Vector3(F(1), F(2), F(3)), new Vector3(F(4), F(5), F(6)), F(7), F(8)));
            }
            return l;
        }
        static string WriteCache(string sig, List<(string k, Vector3 c, Vector3 n, float w, float h)> l)
        {
            var ic = System.Globalization.CultureInfo.InvariantCulture; var sb = new System.Text.StringBuilder(sig + (char)10);
            foreach (var (k, c, n, w, h) in l) sb.Append(string.Join("|", k, c.x.ToString("0.###", ic), c.y.ToString("0.###", ic), c.z.ToString("0.###", ic), n.x.ToString("0.####", ic), n.y.ToString("0.####", ic), n.z.ToString("0.####", ic), w.ToString("0.###", ic), h.ToString("0.###", ic))).Append((char)10);
            return sb.ToString();
        }
        public static int Place(string key)
        {
            Clear(); if (!Ensure()) return 0; var tiles = Json.O(meta, "tiles"); if (tiles == null) return 0;
            var sig = Signature();
            var cached = BakeToAssets ? null : ReadCache(Resources.Load<TextAsset>("Graffiti/cache_" + key)?.text, sig);
            LastSource = cached != null ? "assado" : "";
            if (cached == null && !BakeToAssets) { try { var cp = CachePath(key); if (System.IO.File.Exists(cp)) { cached = ReadCache(System.IO.File.ReadAllText(cp), sig); if (cached != null) LastSource = "cache local"; } } catch { } }
            if (cached != null && cached.All(x => tiles.ContainsKey(x.k))) { Build(cached, tiles); Debug.Log($"[FreeBala] grafites v4: {key} {cached.Count} artes ({LastSource})"); return cached.Count; }
            LastSource = "calculado"; var points = FBNav.Fixed(); if (points.Count == 0) return 0;
            var rnd = Seeded("grafite3|" + key);
            // ---- 1. paredes vistas dos pontos da navmesh
            var walls = new Dictionary<string, Wall>(); var dirs = Enumerable.Range(0, 16).Select(i => { float a = i / 16f * Mathf.PI * 2; return new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)); }).ToArray();
            foreach (var p in points) foreach (var d in dirs)
                {
                    var o = p + Vector3.up * 1.6f; if (!WallHit(o, d, 30, out var h) || h.distance < 1.8f) continue;
                    var bk = BKey(h.collider); var mt = MatOf(h); if (PAINT_NO.IsMatch(mt) || !WallOk(bk + " " + h.collider.name + " " + mt)) continue;
                    var n = new Vector3(h.normal.x, 0, h.normal.z).normalized; if (Vector3.Dot(n, d) > 0) n = -n;
                    float dd = n.x * h.point.x + n.z * h.point.z; var k = bk + "|" + Mathf.RoundToInt(Mathf.Atan2(n.x, n.z) * 40) + "|" + Mathf.RoundToInt(dd * 4);
                    if (walls.TryGetValue(k, out var wl)) { wl.vis++; continue; }
                    walls[k] = new Wall { k = k, bk = bk, col = h.collider, bld = BLD.IsMatch(bk), n = n, d = dd, hp = h.point, vis = 1 };
                }
            int Probe(Wall wl, float s, float z, bool deep = true)
            {
                var o = new Vector3(wl.hp.x + wl.t.x * s + wl.n.x * .06f, z, wl.hp.z + wl.t.z * s + wl.n.z * .06f);
                if (!MapRay(o, -wl.n, deep ? 2.45f : .25f, out var c)) return 0;
                if (BKey(c.collider) != wl.bk || PAINT_NO.IsMatch(MatOf(c)) || c.normal.x * wl.n.x + c.normal.z * wl.n.z < .97f * new Vector2(c.normal.x, c.normal.z).magnitude) return 0;
                float dep = wl.d - (wl.n.x * c.point.x + wl.n.z * c.point.z); if (dep < -.035f || (dep > .035f && dep < .3f)) return 0;
                if (MapRay(new Vector3(c.point.x + wl.n.x * .06f, z, c.point.z + wl.n.z * .06f), wl.n, .9f, out _)) return 0;   // nada colado na frente
                return 1 + Mathf.Max(0, Mathf.RoundToInt(dep / .05f));
            }
            bool Analyse(Wall wl)
            {
                if (wl.analysed) return wl.mask != null; wl.analysed = true; var n = wl.n; wl.t = new Vector3(n.z, 0, -n.x);
                if (!(FBWorld.GroundY(wl.hp.x + n.x * .5f, wl.hp.y + 1, wl.hp.z + n.z * .5f, .5f, 4) is float gy)) return false; wl.gy = gy;
                bool Col(float s) => Probe(wl, s, gy + .45f) == 1 || Probe(wl, s, gy + .9f) == 1 || Probe(wl, s, gy + 2.5f) == 1 || (Probe(wl, s, gy + .45f) > 1 && (Probe(wl, s - 1.2f, gy + .45f) == 1 || Probe(wl, s + 1.2f, gy + .45f) == 1 || Probe(wl, s - 2.4f, gy + .45f) == 1 || Probe(wl, s + 2.4f, gy + .45f) == 1));
                if (Probe(wl, 0, gy + .45f) != 1 && Probe(wl, 0, gy + 2.5f) != 1) return false;
                float s0 = 0, s1 = 0;
                while (s0 > -40) { if (Col(s0 - C)) s0 -= C; else if (Col(s0 - 2 * C)) s0 -= 2 * C; else break; }
                while (s1 < 40) { if (Col(s1 + C)) s1 += C; else if (Col(s1 + 2 * C)) s1 += 2 * C; else break; }
                if (s1 - s0 < 2.2f) return false;
                var cols = Enumerable.Range(0, 7).Select(i => s0 + (s1 - s0) * (i + .5f) / 7).ToArray(); float top = gy + .4f; int miss = 0;
                for (float z = gy + .4f; z < gy + 9 && miss < 5; z += C) { if (cols.Any(s => Probe(wl, s, z) == 1)) { top = z; miss = 0; } else miss++; }
                if (top - gy < 1.6f) return false;
                float mid = (s0 + s1) / 2; var o = new Vector3(wl.hp.x + wl.t.x * mid + n.x * .1f, gy + 1.5f, wl.hp.z + wl.t.z * mid + n.z * .1f);
                if (MapRay(o, n, 3.2f, out var fh) && fh.distance < 3.0f) return false;                         // espaço na frente
                o += n * 1.1f; o.y = top + .3f; if (Physics.Raycast(o, Vector3.up, 40, FBWorld.SolidMask, QueryTriggerInteraction.Ignore)) return false;   // céu aberto
                wl.s0 = s0; wl.s1 = s1; wl.top = top; int nx = Mathf.RoundToInt((s1 - s0) / C) + 1, nz = Mathf.RoundToInt((Mathf.Min(top, gy + 7.9f) - gy - .1f) / C) + 1;
                var m = new byte[nx * nz]; for (int j = 0; j < nz; j++) for (int i = 0; i < nx; i++) m[j * nx + i] = (byte)Mathf.Min(255, Probe(wl, s0 + i * C, gy + .1f + j * C));
                wl.nx = nx; wl.nz = nz; wl.mask = m; return true;
            }
            int CellOk(Wall wl, int i, int j) { var m = wl.mask; int nx = wl.nx, a = m[j * nx + i]; return a > 0 && m[j * nx + i + 1] > 0 && Mathf.Abs(m[j * nx + i + 1] - a) <= 1 && m[(j + 1) * nx + i] > 0 && Mathf.Abs(m[(j + 1) * nx + i] - a) <= 1 && m[(j + 1) * nx + i + 1] > 0 && Mathf.Abs(m[(j + 1) * nx + i + 1] - a) <= 1 ? a : 0; }
            bool Flat(Wall wl, int i, int j) => CellOk(wl, i, j) == 1;
            List<(int j0, int i0, int i1, int h)> Rects(Wall wl)
            {
                if (wl.rects != null) return wl.rects; int nx = wl.nx, nz = wl.nz; var o = new List<(int, int, int, int)>(); wl.rects = o;
                foreach (var j0 in new[] { 0, 1, 2, 3 })
                {
                    var H = new int[nx - 1]; for (int i = 0; i < nx - 1; i++) { int h = 0; while (j0 + h < nz - 1 && Flat(wl, i, j0 + h)) h++; H[i] = h; }
                    for (int i0 = 0; i0 < nx - 1; i0++)
                    {
                        if (H[i0] == 0 || (i0 > 0 && H[i0 - 1] > 0 && H[i0 - 1] >= H[i0])) continue; int hmin = H[i0];
                        for (int i1 = i0; i1 < nx - 1; i1++)
                        {
                            if (H[i1] < hmin) { o.Add((j0, i0, i1 - 1, hmin)); hmin = H[i1]; if (hmin < 6) break; }
                            if (i1 == nx - 2 && hmin >= 6) o.Add((j0, i0, i1, hmin));
                        }
                    }
                }
                return o;
            }
            var LIM = new Dictionary<string, Vector2> { ["mural"] = new(14, 7.6f), ["peca"] = new(9.5f, 5.2f), ["tag"] = new(6.5f, 3.2f) };
            var MIN = new Dictionary<string, Vector2> { ["mural"] = new(4.2f, 2.4f), ["peca"] = new(3.4f, 2.0f), ["tag"] = new(2.8f, 1.7f) };
            (float sc, float w, float h, int ax, int ay, int i0, int j0)? Fit(Wall wl, Dictionary<string, object> tl)
            {
                var kind = Json.S(tl, "kind") ?? ""; var lim = LIM.TryGetValue(kind, out var l) ? l : new Vector2(9, 5); var mn = MIN.TryGetValue(kind, out var q) ? q : new Vector2(2.8f, 1.6f); float asp = Json.F(tl, "aspect", 1);
                (float sc, float w, float h, int i0, int i1, int j0)? best = null;
                foreach (var r0 in Rects(wl))
                {
                    var segs = new List<(int a, int b)> { (r0.i0, r0.i1 + 1) };
                    foreach (var (ua, ub) in wl.used) segs = segs.SelectMany(s => ub <= s.a || ua >= s.b ? new[] { s } : new[] { (s.a, Mathf.Min(s.b, ua)), (Mathf.Max(s.a, ub), s.b) }.Where(x => x.Item2 > x.Item1).ToArray()).ToList();
                    foreach (var (a0, b0) in segs)
                    {
                        int sa = a0, sb = b0; if (sa > 0) sa += 2; if (sb < wl.nx - 1) sb -= 2;
                        float w = (sb - sa) * C, h = r0.h * C; if (w < mn.x || h < mn.y) continue;
                        w = Mathf.Min(w, lim.x); h = Mathf.Min(h, lim.y);
                        if (w / h > asp * 1.28f) w = h * asp * 1.28f; else if (w / h < asp / 1.28f) h = w * 1.28f / asp;
                        if (w < mn.x || h < mn.y) continue;
                        float sc = w * h * (1 - Mathf.Abs(w / h / asp - 1) * .25f);
                        if (best == null || sc > best.Value.sc) best = (sc, w, h, sa, sb - 1, r0.j0);
                    }
                }
                if (best == null) return null; var B = best.Value; float cw = (B.i1 - B.i0 + 1) * C; int ax = Mathf.RoundToInt(B.w / C);
                return (B.sc, B.w, B.h, ax, Mathf.RoundToInt(B.h / C), B.i0 + Mathf.RoundToInt((cw - B.w) / C / 2), B.j0);
            }
            var order = new Dictionary<string, int> { ["mural"] = 0, ["peca"] = 1, ["tag"] = 2 };
            var arts = tiles.Keys.Select(k => (k, tl: tiles[k] as Dictionary<string, object>)).Where(x => x.tl != null).Select(x => (x.k, x.tl, r: rnd())).OrderBy(x => order.TryGetValue(Json.S(x.tl, "kind") ?? "", out var o) ? o : 1).ThenBy(x => x.r).Select(x => (x.k, x.tl)).ToList();
            var WL = walls.Values.OrderByDescending(w => w.bld).ThenByDescending(w => w.vis).ThenBy(w => w.k, System.StringComparer.Ordinal).ToList();
            var placed = new List<Vector3>(); var perB = new Dictionary<string, int>(); var outL = new List<(string k, Vector3 c, Vector3 n, float w, float h)>();
            for (int pass = 0; pass < 2 && arts.Count > 0; pass++) foreach (var wl in WL)
                {
                    if (arts.Count == 0) break; if (pass == 0 && !wl.bld) continue; perB.TryGetValue(wl.bk, out var nb); if (nb >= (wl.bld ? 2 : 4)) continue;
                    if (!Analyse(wl)) continue;
                    for (int tries = 0; tries < 3 && arts.Count > 0; tries++)
                    {
                        (float sc, float w, float h, int ax, int ay, int i0, int j0)? got = null; int ai = -1;
                        for (int q = 0; q < arts.Count; q++) { var f = Fit(wl, arts[q].tl); if (f != null && (got == null || f.Value.sc > got.Value.sc)) { got = f; ai = q; } }
                        if (got == null) break; var G = got.Value;
                        float sMid = wl.s0 + (G.i0 + G.ax / 2f) * C, zMid = wl.gy + .1f + G.j0 * C + G.h / 2; var c = new Vector3(wl.hp.x + wl.t.x * sMid, zMid, wl.hp.z + wl.t.z * sMid);
                        if (placed.Any(p => (p - c).magnitude < 7)) { wl.used.Add((G.i0, G.i0 + G.ax)); continue; }
                        outL.Add((arts[ai].k, c, wl.n, G.w, G.h)); placed.Add(c); wl.used.Add((G.i0 - 4, G.i0 + G.ax + 4)); arts.RemoveAt(ai);
                        perB[wl.bk] = (perB.TryGetValue(wl.bk, out var v) ? v : 0) + 1; if (perB[wl.bk] >= (wl.bld ? 2 : 4)) break;
                    }
                }
            Debug.Log($"[FreeBala] grafites v4: {key} {outL.Count} artes, paredes vistas {walls.Count} (calculado; assinatura {sig})");
            var txt = WriteCache(sig, outL);
            try { var cp = CachePath(key); System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(cp)); System.IO.File.WriteAllText(cp, txt); } catch { }
#if UNITY_EDITOR
            if (BakeToAssets) { var ap = "Assets/FreeBala/Resources/Graffiti/cache_" + key + ".txt"; System.IO.File.WriteAllText(ap, txt); UnityEditor.AssetDatabase.ImportAsset(ap); }
#endif
            Build(outL, tiles); return outL.Count;
        }
        static void Build(List<(string k, Vector3 c, Vector3 n, float w, float h)> list, Dictionary<string, object> tiles)
        {
            var P = new List<Vector3>(); var UV = new List<Vector2>(); var N = new List<Vector3>(); var I = new List<int>();
            foreach (var (k, c, n, w, h) in list)
            {
                var uv = Json.L(tiles[k], "uv"); float u0 = Json.Fv(uv[0]), v0 = Json.Fv(uv[1]), u1 = Json.Fv(uv[2]), v1 = Json.Fv(uv[3]);
                var t = new Vector3(n.z, 0, -n.x); int o0 = P.Count;   // tangente = up × normal (lado direito de quem olha a parede)
                foreach (var (sx, sy) in new[] { (-.5f, -.5f), (.5f, -.5f), (.5f, .5f), (-.5f, .5f) })
                { P.Add(c - t * sx * w + Vector3.up * sy * h + n * .02f); UV.Add(new Vector2(u0 + (u1 - u0) * (sx + .5f), v0 + (v1 - v0) * (sy + .5f))); N.Add(n); }
                I.AddRange(new[] { o0, o0 + 2, o0 + 1, o0, o0 + 3, o0 + 2 });
            }
            if (I.Count == 0) return;
            var m = new Mesh { name = "grafites", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; m.SetVertices(P); m.SetUVs(0, UV); m.SetNormals(N); m.SetTriangles(I, 0); m.RecalculateBounds(); m.RecalculateTangents();
            go = new GameObject("Grafites"); go.AddComponent<MeshFilter>().sharedMesh = m; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        public static void Clear() { if (go) { if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go); } go = null; bkeyC.Clear(); }
    }
}
