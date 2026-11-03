// SEMÁFOROS (r45 — pedido: "o trânsito precisa respeitar cruzamentos, semáforos e prioridade"). Do 171 (BP_GerenciadorSemaforo +
// BP_Semaforo, bytecode lido com tools/re/kismet_print.py): estados Vermelho/Amarelo/Verde (ENUM_Semaforo), um gerenciador por cruzamento
// que abre UM grupo por vez em rodízio — verde 8 s, amarelo 3 s, vermelho — e o próximo grupo. Aqui:
//  · cruzamento com 3+ faixas chegando (malha FBRoads) vira semaforizado; grupos = eixo das faixas que chegam (as que vêm de frente uma
//    para a outra abrem juntas); 1,5 s de vermelho geral entre grupos (o cruzamento esvazia); fase inicial diferente por cruzamento;
//  · o modelo do 171 (Placas.glb/Semaforo: poste com cabeça baixa e braço com cabeça sobre a rua, 3 lentes na vertical) na calçada à
//    direita de cada faixa que chega, braço por cima da faixa, lentes viradas para quem vem; as lentes acendem por cima da textura;
//  · o trânsito (FBTransito.Drive) para na linha no vermelho e no amarelo quando ainda dá para parar; com o verde entra pela reserva
//    do cruzamento de sempre (quem já está dentro termina de passar).
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public static class FBSemaforo
    {
        public const float GREEN = 8, YELLOW = 3, ALLRED = 1.5f;
        class Ctl { public int j; public readonly List<int>[] grp = { new(), new() }; public float t; public int cur, phase; }   // phase: 0 verde, 1 amarelo, 2 vermelho geral
        class Head { public Ctl c; public int g; public GameObject go; public readonly List<(MeshRenderer r, int col)> lamps = new(); }
        static readonly Dictionary<int, (Ctl c, int g)> byLane = new(); static readonly List<Ctl> ctls = new(); static readonly List<Head> heads = new();
        static Material mR, mY, mG; static Mesh disc;
        public static int MaxJunctions = 6;   // r45b
        public static int Junctions => ctls.Count; public static int Heads => heads.Count;
        // estado para quem chega pela faixa 'lane': 0 sem semáforo, 1 verde, 2 amarelo, 3 vermelho
        public static int StateOf(int lane)
        {
            if (!byLane.TryGetValue(lane, out var v)) return 0; var c = v.c;
            if (c.phase == 2 || c.cur != v.g) return 3; return c.phase == 0 ? 1 : 2;
        }
        public static float LeftOf(int lane) { if (!byLane.TryGetValue(lane, out var v)) return 0; var c = v.c; return c.cur != v.g || c.phase == 2 ? 0 : c.phase == 0 ? GREEN - c.t : YELLOW - c.t; }
        public static void Clear() { foreach (var h in heads) if (h.go) Object.Destroy(h.go); heads.Clear(); ctls.Clear(); byLane.Clear(); }

        public static void Build()
        {
            Clear(); if (!FBRoads.Ready) return;
            var lanesIn = new Dictionary<int, HashSet<int>>();
            foreach (var S in FBRoads.segs) if (S.kind == 1 && S.junction >= 0) foreach (var p in S.prev) if (p >= 0 && p < FBRoads.segs.Count && FBRoads.segs[p].kind == 0) (lanesIn.TryGetValue(S.junction, out var hs) ? hs : lanesIn[S.junction] = new HashSet<int>()).Add(p);
            // r45b (pedido: "os semáforos são muitos, vamos remover vários"): antes TODO cruzamento com 3+ faixas chegando (31 na Vila Jardim,
            // 103 postes). Agora só os principais: cruzamentos de 4+ faixas (3 se faltar), os de vias mais longas primeiro, no mínimo 230 m um do
            // outro e no máximo MaxJunctions. Os outros cruzamentos ficam com a prioridade de sempre (reserva do FBTransito).
            Vector3 End(int id) { var S = FBRoads.segs[id]; return FBRoads.At(S, S.len, out _); }
            var picked = new List<(int j, Vector3 c)>();
            foreach (int minIn in new[] { 4, 3 })
                foreach (var kv in lanesIn.Where(k => k.Value.Count >= minIn).OrderByDescending(k => k.Value.Sum(id => FBRoads.segs[id].len)))
                {
                    if (picked.Count >= MaxJunctions || picked.Any(q => q.j == kv.Key)) continue;
                    var ctr = Vector3.zero; foreach (var id in kv.Value) ctr += End(id); ctr /= kv.Value.Count;
                    if (picked.Any(q => (q.c - ctr).Flat().magnitude < 230)) continue; picked.Add((kv.Key, ctr));
                }
            foreach (var kv in lanesIn.Where(k => picked.Any(q => q.j == k.Key)).OrderBy(k => k.Key))
            {
                var L = kv.Value.ToList(); if (L.Count < 3) continue;
                Vector3 H(int id) { var S = FBRoads.segs[id]; FBRoads.At(S, S.len, out var t); t.y = 0; return t.normalized; }
                var h0 = H(L[0]); var c = new Ctl { j = kv.Key };
                foreach (var id in L) { float a = Vector3.Angle(h0, H(id)); c.grp[a < 45 || a > 135 ? 0 : 1].Add(id); }
                if (c.grp[1].Count == 0) continue;
                uint hs2 = (uint)(kv.Key * 2654435761u); c.cur = (int)(hs2 & 1); c.t = (hs2 >> 8) % 800 / 100f;   // fase inicial por cruzamento
                ctls.Add(c); for (int g = 0; g < 2; g++) foreach (var id in c.grp[g]) byLane[id] = (c, g);
            }
            Visuals();
            Debug.Log($"[FreeBala][semáforo] {ctls.Count} cruzamentos semaforizados · {byLane.Count} faixas · {heads.Count} postes (171)");
        }
        public static void Tick(float dt)
        {
            foreach (var c in ctls)
            {
                c.t += dt;
                if (c.phase == 0 && c.t >= GREEN) { c.phase = 1; c.t = 0; }
                else if (c.phase == 1 && c.t >= YELLOW) { c.phase = 2; c.t = 0; }
                else if (c.phase == 2 && c.t >= ALLRED) { c.phase = 0; c.t = 0; c.cur = 1 - c.cur; }
            }
            foreach (var h in heads)
            {
                if (!h.go) continue; int st = h.c.phase == 2 || h.c.cur != h.g ? 3 : h.c.phase == 0 ? 1 : 2;
                foreach (var (r, col) in h.lamps) if (r) { bool on = col == st; if (r.enabled != on) r.enabled = on; }
            }
        }

        // ---- modelo do 171 + lentes acesas
        // lentes medidas no modelo (Placas.glb/Semaforo, espaço do glTF; render em F:/FreeBala_re/171/semaforo): cabeça do poste a ~3 m,
        // cabeça do braço a ~5,35 m e 5,19 m para o lado do braço; lentes na frente (+X do glTF), 0,27 m / 0,345 m entre elas
        static readonly (Vector3 p, int col, float r)[] LENS =
        {
            (new(.16f, 3.27f, .03f), 3, .12f), (new(.16f, 3.00f, .03f), 2, .12f), (new(.16f, 2.73f, .03f), 1, .12f),
            (new(.15f, 5.69f, 5.19f), 3, .15f), (new(.15f, 5.35f, 5.19f), 2, .15f), (new(.15f, 5.00f, 5.19f), 1, .15f),
        };
        static Transform model; static int flipX = -1, flipZ = 1; static bool modelTried;                 // sem malha legível: o glTFast espelha o X
        static Transform Model()
        {
            if (modelTried) return model; modelTried = true; var lib = Resources.Load<GameObject>("X171/Urbano/Placas"); if (!lib) return null;
            foreach (Transform t in lib.transform) if (t.name.StartsWith("Semaforo")) { model = t; break; }
            if (!model) return null;
            // o glTFast troca a mão do sistema (espelha um eixo): descobre qual pelos vértices — a lente tem de cair em cima da malha
            var mf = model.GetComponentInChildren<MeshFilter>(); var V = mf && mf.sharedMesh && mf.sharedMesh.isReadable ? mf.sharedMesh.vertices : null;
            if (V != null)
            {
                var M = model.worldToLocalMatrix * mf.transform.localToWorldMatrix; float best = 1e9f;
                foreach (var (fx, fz) in new[] { (-1, 1), (1, -1), (1, 1), (-1, -1) })
                {
                    float e = 0; foreach (var L in LENS) { var q = new Vector3(L.p.x * fx, L.p.y, L.p.z * fz); float m = 1e9f; foreach (var v in V) m = Mathf.Min(m, (M.MultiplyPoint3x4(v) - q).sqrMagnitude); e += m; }
                    if (e < best) { best = e; flipX = fx; flipZ = fz; }
                }
                Debug.Log($"[FreeBala][semáforo] modelo {model.name}: eixos do glTF x{flipX} z{flipZ} (erro {Mathf.Sqrt(best / LENS.Length):0.000} m)");
            }
            return model;
        }
        static void Mats()
        {
            if (mR) return;
            Material M(Color c) { var m = new Material(FBShaders.Unlit); m.SetColor("_BaseColor", c); m.color = c; m.SetFloat("_Cull", 0); return m; }
            mR = M(new Color(4.2f, .25f, .12f)); mY = M(new Color(3.6f, 2.0f, .15f)); mG = M(new Color(.2f, 3.4f, .9f));   // HDR: brilha no bloom
            disc = new Mesh { name = "lente_semaforo" }; var vs = new List<Vector3> { Vector3.zero }; var tr = new List<int>();
            for (int i = 0; i <= 16; i++) { float a = i / 16f * Mathf.PI * 2; vs.Add(new Vector3(0, Mathf.Sin(a), Mathf.Cos(a))); if (i > 0) { tr.Add(0); tr.Add(i); tr.Add(i + 1); } }
            disc.SetVertices(vs); disc.SetTriangles(tr, 0); disc.RecalculateBounds();
        }
        static void Visuals()
        {
            var mdl = Model(); if (!mdl) return; Mats(); int lm = 1 << FBWorld.LMap;
            foreach (var kv in byLane)
            {
                var S = FBRoads.segs[kv.Key]; var end = FBRoads.At(S, S.len, out var t); t.y = 0; t.Normalize(); var r = Vector3.Cross(Vector3.up, t);
                // na calçada à direita, logo antes da linha de parada (fora do asfalto)
                Vector3? at = null;
                for (float o = S.w / 2 + .9f; o < S.w / 2 + 4; o += .5f) { var p = end - t * 1.2f + r * o; if (FBRoads.IsRoad(p)) continue; if (FBWorld.GroundY(p.x, p.y + 3, p.z, .3f, 6) is float gy) { p.y = gy; if (!Physics.CheckCapsule(p + Vector3.up * .4f, p + Vector3.up * 2.4f, .3f, lm, QueryTriggerInteraction.Ignore)) { at = p; break; } } }
                if (at == null) continue;
                // lentes (+X do glTF) para quem vem (−t), braço (+Z do glTF) por cima da faixa (−r)
                var lensDir = -t; var armDir = -r; var lx = new Vector3(flipX, 0, 0); var lz = new Vector3(0, 0, flipZ);
                var q = Quaternion.LookRotation(armDir * flipZ, Vector3.up);                    // +Z local = arm*flipZ
                if (Vector3.Dot(q * lx, lensDir) < 0) { q = Quaternion.LookRotation(-armDir * flipZ, Vector3.up); at = end - t * 1.2f - r * (S.w / 2 + 1.2f); if (FBWorld.GroundY(at.Value.x, at.Value.y + 3, at.Value.z, .3f, 6) is float g2) at = new Vector3(at.Value.x, g2, at.Value.z); }   // modelo espelhado: poste do outro lado
                var go = Object.Instantiate(mdl.gameObject); go.name = "semaforo_" + kv.Key; go.transform.SetPositionAndRotation(at.Value, q); go.transform.localScale = mdl.lossyScale;
                foreach (var tr in go.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = FBWorld.LMap;
                var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0, 1.6f, 0); col.size = new Vector3(.22f, 3.2f, .22f);   // poste (o braço fica acima dos carros)
                var h = new Head { c = kv.Value.c, g = kv.Value.g, go = go };
                foreach (var L in LENS)
                {
                    var lg = new GameObject("lente"); lg.layer = FBWorld.LMap; lg.transform.SetParent(go.transform, false);
                    lg.transform.localPosition = new Vector3((L.p.x + .012f) * flipX, L.p.y, L.p.z * flipZ); lg.transform.localRotation = flipX < 0 ? Quaternion.Euler(0, 180, 0) : Quaternion.identity; lg.transform.localScale = Vector3.one * L.r;
                    lg.AddComponent<MeshFilter>().sharedMesh = disc; var mr = lg.AddComponent<MeshRenderer>(); mr.sharedMaterial = L.col == 3 ? mR : L.col == 2 ? mY : mG;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; mr.enabled = false; h.lamps.Add((mr, L.col));
                }
                heads.Add(h);
            }
        }
    }
}
