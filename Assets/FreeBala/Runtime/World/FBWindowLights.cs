// Janelas acesas à noite — v2 (pedido do usuário 20/10: "as fontes de luz devem estar DENTRO dos cômodos, iluminando os ambientes
// internos e projetando luz e sombras para fora através das janelas, em vez de apenas haver luz saindo diretamente das janelas").
//  · cada painel de vidro (FBWindowData, medido na importação) é uma janela; ~70 % acendem (sorteio fixo pela posição);
//  · de longe: brilho de cortina atrás do vidro (uma malha só, ADITIVA — o cômodo continua visível através dela);
//  · de perto: o cômodo é medido por raio (fundo até a parede oposta e teto) e ganha uma LÂMPADA DE TETO de verdade lá dentro:
//      - spot da lâmpada mirando a janela e o chão de fora, com SOMBRA nas mais próximas: o caixilho e a parede recortam a luz e o
//        retângulo da janela aparece projetado na calçada/fachada da frente (e nada vaza pela parede);
//      - preenchimento (ponto curto, sem sombra) que acende paredes/piso do cômodo vistos pela janela;
//    alocação estável com fade (sem pipocar ao girar a câmera), tom por janela (quente / neutra / TV fria).
//  · fachada sem cômodo (raio bate logo atrás do vidro): só o brilho, sem lâmpada. Celular: só o brilho (sem luz real).
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FreeBala
{
    public static class FBWindowLights
    {
        struct Win { public Vector3 c, n; public float w, h; public bool lit; public float cct; }
        struct Room { public bool tested, ok; public Vector3 lp; public float depth; }
        class Slot { public Light spot, fill; public int win = -1; public float wgt, target; }
        static readonly List<Win> wins = new(); static readonly List<Slot> slots = new(); static GameObject root; static Material mat; static float scanT;
        static readonly List<int> litIdx = new(); static Room[] rooms = new Room[0]; static int maxShadow;
        public static int RealLights { get; private set; }

        public static void Build()
        {
            wins.Clear(); litIdx.Clear(); slots.Clear(); root = null; RealLights = 0;
            // dados medidos na importação (FBGlassSplit.Measure) — em jogo as malhas podem não ser legíveis
            foreach (var d in Object.FindObjectsByType<FBWindowData>(FindObjectsSortMode.None))
                for (int i = 0; i < d.center.Length; i++)
                {
                    var c0 = d.center[i]; uint hs = Hash(c0);
                    wins.Add(new Win { c = c0, n = d.normal[i], w = d.size[i].x, h = d.size[i].y, lit = hs % 100 < 70, cct = Tone(hs) });
                }
            if (wins.Count == 0)
                foreach (var g in Object.FindObjectsByType<FBGlass>(FindObjectsSortMode.None))
                {
                    var mf = g.GetComponent<MeshFilter>(); var m = mf ? mf.sharedMesh : null; if (!m || !m.isReadable) continue;
                    var V = m.vertices; var T = m.triangles; var M = mf.transform.localToWorldMatrix; var bc = g.GetComponent<Renderer>().bounds.center;
                    var pid = new Dictionary<Vector3Int, int>(); var vid = new int[V.Length]; var W = new Vector3[V.Length];
                    for (int i = 0; i < V.Length; i++) { W[i] = M.MultiplyPoint3x4(V[i]); var k = Vector3Int.RoundToInt(W[i] * 200); if (!pid.TryGetValue(k, out var id)) pid[k] = id = pid.Count; vid[i] = id; }
                    var par = Enumerable.Range(0, pid.Count).ToArray(); int F(int x) { while (par[x] != x) x = par[x] = par[par[x]]; return x; }
                    for (int t = 0; t < T.Length; t += 3) { int a = F(vid[T[t]]), b = F(vid[T[t + 1]]), c = F(vid[T[t + 2]]); par[b] = a; par[F(c)] = a; }
                    var groups = new Dictionary<int, List<Vector3>>(); for (int i = 0; i < V.Length; i++) { int r = F(vid[i]); if (!groups.TryGetValue(r, out var l)) groups[r] = l = new List<Vector3>(); l.Add(W[i]); }
                    foreach (var pts in groups.Values)
                    {
                        var b = new Bounds(pts[0], Vector3.zero); foreach (var p in pts) b.Encapsulate(p);
                        float sx = b.size.x, sz = b.size.z, h = b.size.y; if (h < .35f || h > 3.5f) continue;
                        Vector3 n = sx < sz ? Vector3.right : Vector3.forward; float w = Mathf.Max(sx, sz); if (w < .3f || w > 4) continue;
                        if (Vector3.Dot(b.center - bc, n) < 0) n = -n;
                        uint hs = Hash(b.center); wins.Add(new Win { c = b.center, n = n, w = w, h = h, lit = hs % 100 < 70, cct = Tone(hs) });
                    }
                }
            rooms = new Room[wins.Count];
            if (wins.Count == 0) return;
            // vidro não projeta sombra (senão a lâmpada de dentro ficaria presa no próprio vidro)
            foreach (var g in Object.FindObjectsByType<FBGlass>(FindObjectsSortMode.None)) foreach (var r in g.GetComponents<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            // brilho de cortina dos interiores acesos (de longe)
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var cols = new List<Color>(); var tris = new List<int>(); var rnd = new System.Random(5);
            for (int i = 0; i < wins.Count; i++)
            {
                var w = wins[i]; if (!w.lit) continue; litIdx.Add(i);
                var right = Vector3.Cross(Vector3.up, w.n).normalized; var c = w.c - w.n * .12f; float hw = w.w * .5f * .96f, hh = w.h * .5f * .96f; int b0 = verts.Count;
                verts.Add(c - right * hw - Vector3.up * hh); verts.Add(c + right * hw - Vector3.up * hh); verts.Add(c - right * hw + Vector3.up * hh); verts.Add(c + right * hw + Vector3.up * hh);
                float u0 = (float)rnd.NextDouble() * .5f; uvs.Add(new Vector2(u0, 0)); uvs.Add(new Vector2(u0 + .5f, 0)); uvs.Add(new Vector2(u0, 1)); uvs.Add(new Vector2(u0 + .5f, 1));
                float k = .55f + (float)rnd.NextDouble() * .7f; var tint = w.cct > 5000 ? new Color(.75f, .85f, 1) : w.cct > 3200 ? new Color(1, .9f, .74f) : Color.Lerp(new Color(1, .72f, .42f), new Color(1, .86f, .66f), (float)rnd.NextDouble());
                for (int q = 0; q < 4; q++) cols.Add(tint * k);
                tris.AddRange(new[] { b0, b0 + 2, b0 + 1, b0 + 1, b0 + 2, b0 + 3 });
            }
            var mesh = new Mesh { name = "janelas_acesas", indexFormat = IndexFormat.UInt32 }; mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetColors(cols); mesh.SetTriangles(tris, 0); mesh.RecalculateBounds();
            root = new GameObject("FB_JanelasAcesas"); root.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = root.AddComponent<MeshRenderer>(); mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            mr.sharedMaterial = mat ??= MakeMat(); root.SetActive(false);
            // lâmpadas de teto (pool fixo: nada é criado no meio da partida)
            bool mob = FBQuality.Mobile; var L = FBQuality.Level;
            int nl = mob ? 0 : L == FBQLevel.Ultra ? 12 : L == FBQLevel.Alto ? 8 : L == FBQLevel.Medio ? 5 : 2;
            // r65: TODA lâmpada de cômodo acesa tem sombra (print do usuário: luz da janela atravessando a parede e acendendo o chão do lado de
            // fora); as mais longe em 256 px. Baixo continua sem (2 lâmpadas, alcance curto).
            maxShadow = mob ? 0 : L == FBQLevel.Baixo ? 0 : L == FBQLevel.Medio ? Mathf.Min(nl, 8) : nl;
            for (int i = 0; i < nl; i++)
            {
                var s = new Slot();
                s.spot = new GameObject("LampadaJanela" + i).AddComponent<Light>(); s.spot.type = LightType.Spot; s.spot.range = 16; s.spot.useColorTemperature = true; s.spot.color = Color.white;
                s.spot.shadows = LightShadows.None; s.spot.shadowStrength = .95f; s.spot.shadowBias = .05f; s.spot.shadowNormalBias = .4f; s.spot.shadowNearPlane = .15f; FBLightUtil.MediumShadows(s.spot); s.spot.enabled = false;
                // r65: preenchimento do cômodo era um PONTO de até 4,2 m sem sombra a ~1 m da janela — a esfera passava a parede da frente e
                // acendia a calçada; agora é um SPOT de 150° voltado para DENTRO (fundo e piso do cômodo, o que se vê pela janela)
                s.fill = new GameObject("LampadaComodo" + i).AddComponent<Light>(); s.fill.type = LightType.Spot; s.fill.spotAngle = 150; s.fill.innerSpotAngle = 90; s.fill.useColorTemperature = true; s.fill.color = Color.white; s.fill.shadows = LightShadows.None; s.fill.enabled = false;
                slots.Add(s); FBVolumetric.Sources.Add(s.spot);
            }
            Debug.Log($"[FreeBala] janelas: {wins.Count} · acesas {litIdx.Count} · lâmpadas de cômodo {nl} ({maxShadow} com sombra)");
        }
        static uint Hash(Vector3 c0) => (uint)(Mathf.RoundToInt(c0.x * 7) * 73856093 ^ Mathf.RoundToInt(c0.y * 7) * 19349663 ^ Mathf.RoundToInt(c0.z * 7) * 83492791);
        static float Tone(uint hs) { uint t = (hs / 100) % 100; return t < 12 ? 6200 : t < 40 ? 3600 : 2700; }   // TV/LED fria · neutra · incandescente

        // brilho de cortina (dobras verticais, mais claro no alto, borda escura) — ADITIVO: o cômodo de verdade aparece por trás
        static Material MakeMat()
        {
            var t = new Texture2D(128, 64, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 128; x++)
                {
                    float u = x / 128f, v = y / 63f; float folds = .78f + .22f * Mathf.Sin(u * Mathf.PI * 18) * Mathf.Sin(u * Mathf.PI * 3.3f + .7f);
                    float edge = Mathf.SmoothStep(0, .18f, Mathf.Min(u % .5f, .5f - u % .5f) * 2) * Mathf.SmoothStep(0, .15f, Mathf.Min(v, 1 - v)), top = Mathf.Lerp(.7f, 1.05f, v);
                    float l = folds * (.35f + .65f * edge) * top; t.SetPixel(x, y, new Color(l, l, l, 1));
                }
            t.Apply(); var m = new Material(FBShaders.ParticlesUnlit) { name = "janela_interior" }; m.SetTexture("_BaseMap", t); m.mainTexture = t; m.SetColor("_BaseColor", Color.black); m.SetFloat("_Cull", 0);
            m = FBShaders.Transparent(m, true); m.renderQueue = 3010; return m;
        }

        // cômodo atrás da janela: fundo (raio até a parede oposta) e teto; sem fundo = fachada (sem lâmpada)
        static Room RoomOf(int i)
        {
            if (rooms[i].tested) return rooms[i];
            var w = wins[i]; var r = new Room { tested = true }; int mask = 1 << FBWorld.LMap;
            var n = w.n; n.y = 0; if (n.sqrMagnitude < .1f) { rooms[i] = r; return r; } n.Normalize();
            var o = w.c - n * .25f;
            float depth = Physics.Raycast(o, -n, out var h, 7, mask, QueryTriggerInteraction.Ignore) ? h.distance : 7;
            if (depth >= .9f && !Physics.CheckSphere(o, .12f, mask, QueryTriggerInteraction.Ignore))
            {
                var p = o - n * Mathf.Clamp(depth * .5f, .5f, 2.4f);
                float up = Physics.Raycast(p, Vector3.up, out var hu, 2.6f, mask, QueryTriggerInteraction.Ignore) ? hu.distance - .28f : w.h * .5f + .35f;
                p += Vector3.up * Mathf.Clamp(up, .25f, 1.9f);
                r.ok = true; r.lp = p; r.depth = depth;
            }
            rooms[i] = r; return r;
        }

        public static void Tick(Camera cam, float night, float dt)
        {
            if (!root || !cam) return; float k = Mathf.InverseLerp(.35f, .9f, night); root.SetActive(k > .01f);
            if (mat) mat.SetColor("_BaseColor", new Color(1, .74f, .44f) * (2.6f * k));   // HDR: passa do limiar do bloom
            RealLights = 0;
            if (slots.Count == 0) return;
            var cp = cam.transform.position; var cf = cam.transform.forward;
            if (k <= .01f) { foreach (var s in slots) { s.target = 0; s.wgt = 0; s.win = -1; s.spot.enabled = s.fill.enabled = false; } return; }
            if ((scanT -= dt) <= 0)
            {
                scanT = .35f;
                // candidatas: acesas até 45 m, na frente primeiro (pouco: girar a câmera não troca as luzes), quem já está acesa fica (histerese)
                var cur = new HashSet<int>(slots.Where(s => s.win >= 0 && s.target > 0).Select(s => s.win));
                var cand = new List<(float sc, int i)>();
                foreach (int i in litIdx)
                {
                    var v = wins[i].c - cp; float d2 = v.sqrMagnitude; if (d2 > 45 * 45) continue; float d = Mathf.Sqrt(d2);
                    if (Vector3.Dot(wins[i].n, -v) < -.05f * d && d > 6) continue;                          // janela de costas para a câmera: não se vê a luz dela
                    float sc = d * (Vector3.Dot(v, cf) < 0 ? 1.5f : 1) * (cur.Contains(i) ? .78f : 1); cand.Add((sc, i));
                }
                cand.Sort((a, b) => a.sc.CompareTo(b.sc));
                var want = new List<int>(); foreach (var (_, i) in cand) { if (want.Count >= slots.Count) break; if (RoomOf(i).ok) want.Add(i); }
                var wantSet = new HashSet<int>(want);
                foreach (var s in slots) if (s.win >= 0 && !wantSet.Contains(s.win)) s.target = 0;     // apaga devagar
                foreach (int i in want)
                {
                    var have = slots.FirstOrDefault(s => s.win == i); if (have != null) { have.target = 1; continue; }
                    var free = slots.FirstOrDefault(s => s.win < 0) ?? slots.Where(s => s.target == 0).OrderBy(s => s.wgt).FirstOrDefault(); if (free == null) break;
                    Assign(free, i);
                }
            }
            // fade e sombra nas mais próximas já acesas
            var shadowedL = slots.Where(s => s.win >= 0).OrderBy(s => (wins[s.win].c - cp).sqrMagnitude).Take(maxShadow).ToList();
            var shadowed = shadowedL.ToHashSet(); var near4 = shadowedL.Take(4).ToHashSet();
            foreach (var s in slots)
            {
                if (s.win < 0) continue;
                s.wgt = Mathf.MoveTowards(s.wgt, s.target, dt * 2f);
                if (s.wgt <= 0 && s.target <= 0) { s.win = -1; s.spot.enabled = s.fill.enabled = false; continue; }
                var w = wins[s.win]; float area = Mathf.Clamp(w.w * w.h, .6f, 3f);
                s.spot.intensity = 9f * k * s.wgt * Mathf.Sqrt(area); s.fill.intensity = 2.4f * k * s.wgt; s.spot.enabled = s.fill.enabled = true; RealLights++;
                var sh = shadowed.Contains(s) ? LightShadows.Soft : LightShadows.None; if (s.spot.shadows != sh) s.spot.shadows = sh;
                if (sh == LightShadows.None) s.spot.intensity *= .25f;                           // sem sombra (fora da cota): fraca, não vaza
                var ad = s.spot.GetUniversalAdditionalLightData(); int tier = near4.Contains(s) ? UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium : UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow;
                if (ad && ad.additionalLightsShadowResolutionTier != tier) ad.additionalLightsShadowResolutionTier = tier;
            }
        }
        static void Assign(Slot s, int i)
        {
            var w = wins[i]; var r = rooms[i]; s.win = i; s.target = 1; s.wgt = 0;
            var n = w.n; n.y = 0; n.Normalize();
            // mira: um pouco além da janela e para baixo (o chão / a fachada da frente), o cone cobre a abertura com folga
            var tgt = w.c + n * 3f + Vector3.down * Mathf.Clamp(w.c.y - r.lp.y + 2.2f, 1.2f, 3.5f);
            var dir = (tgt - r.lp).normalized; float dist = Vector3.Distance(r.lp, w.c);
            float ang = 2 * Mathf.Atan(Mathf.Max(w.w, w.h) * .5f / Mathf.Max(.3f, dist)) * Mathf.Rad2Deg;
            s.spot.transform.SetPositionAndRotation(r.lp, Quaternion.LookRotation(dir)); s.spot.spotAngle = Mathf.Clamp(ang * 1.5f + 25, 70, 135); s.spot.innerSpotAngle = s.spot.spotAngle * .5f;
            s.spot.colorTemperature = w.cct; s.fill.colorTemperature = w.cct;
            // preenchimento: no ponto da lâmpada, olhando para o fundo do cômodo e um pouco para baixo; alcance = fundo do cômodo
            s.fill.transform.SetPositionAndRotation(r.lp, Quaternion.LookRotation((-n + Vector3.down * .55f).normalized)); s.fill.range = Mathf.Clamp(r.depth * .8f + .6f, 2.2f, 4.2f);
        }
    }
}
