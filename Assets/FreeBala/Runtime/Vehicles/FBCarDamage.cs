// DANO DOS VEÍCULOS (pedido do usuário 20/10: "deformações, impactos e destruição de componentes; vidros quebráveis; faróis e lanternas
// destruídos individualmente, com falha elétrica antes de apagar").
//  · AMASSADO: no ponto da batida a lataria (LOD0/LOD1 + peças das portas) afunda na direção do impacto — raio e profundidade pela
//    velocidade, ruído por vértice, teto de 22 cm por vértice a partir da forma original; as normais são refeitas SÓ na área atingida
//    (somadas por posição: sem costura de UV aparecendo no resto do carro). Cópia da malha por carro (os outros iguais não amassam).
//  · VIDROS: a submalha de vidro do carro é dividida em PAINÉIS (componente conexo por posição de vértice); tiro/batida trinca
//    (painel passa para uma submalha de vidro estilhaçado, branco e opaco) e na segunda vez estilhaça (sai da malha, cacos + som).
//  · LUZES: farol/lanterna/seta/ré perto do impacto ou do tiro piscam em curto (0,5–1,5 s) e apagam — cada lâmpada (FBCarLights).
//  · PEÇAS: batida forte de frente empena o capô aberto, de trás a mala, de lado a porta (FBDoors.Bend); vida do carro cai com o
//    quadrado da velocidade; abaixo de 45 % sai fumaça do motor, abaixo de 20 % fumaça preta e motor fraco, em 0 o motor morre.
//  · EMPURRÃO: carro parado atingido desliza com atrito (e amassa do lado da batida).
//  · r45: RESISTÊNCIA por tipo (Caveirão 0,25 · pesados 0,6 · viatura 0,8 · comum 1 · esportivo 1,15 do impacto); dano por ZONA (frente,
//    trás, lados) que muda a DIREÇÃO: roda dianteira batida puxa para o lado dela, frente amassada tira aderência, a vida tira potência
//    e velocidade máxima de forma contínua (antes só abaixo de 20 %); batida GRAVE com a peça já empenada SOLTA a peça (porta/capô/mala
//    viram corpo físico, FBDoors.Detach) — nunca explode.
// A oficina (FBOficina) conserta tudo (Repair).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
    public static class FBCarDamage
    {
        class Glass { public int sub = -1, crackSub = -1; public int[] orig; public List<int[]> panes; public List<Vector3> centers; public List<int> state; }   // state: 0 inteiro, 1 trincado, 2 quebrado
        class St
        {
            public float hp = 100, lastDent = -9, smokeT, lastShot = -9, pull, front, rear, left, right; public bool dead; public int light, moderate, severe;
            public readonly Dictionary<Mesh, (Vector3[] p, Vector3[] n)> rest = new(); public readonly HashSet<Mesh> own = new();
            public readonly Dictionary<MeshFilter, (Mesh m, Glass g)> glass = new(); public Vector3 shove; public float spin;
        }
        static readonly Dictionary<FBCar, St> all = new(); static Material crackMat; static Runner runner;
        static St Get(FBCar c) { if (!all.TryGetValue(c, out var s)) all[c] = s = new St(); if (!runner) { var g = new GameObject("FB_DanoVeiculos"); Object.DontDestroyOnLoad(g); runner = g.AddComponent<Runner>(); } return s; }
        public static float Hp(FBCar c) => c && all.TryGetValue(c, out var s) ? s.hp : 100;
        public static bool EngineDead(FBCar c) => c && all.TryGetValue(c, out var s) && s.dead;
        public static float PowerK(FBCar c) { if (!c || !all.TryGetValue(c, out var s)) return 1; return s.dead ? 0 : Mathf.Lerp(.55f, 1, Mathf.InverseLerp(12, 70, s.hp)); }
        public static float TopK(FBCar c) => c && all.TryGetValue(c, out var s) ? Mathf.Lerp(.7f, 1, Mathf.InverseLerp(10, 80, s.hp)) : 1;          // máxima cai com a vida
        public static float GripK(FBCar c) => c && all.TryGetValue(c, out var s) ? 1 - Mathf.Clamp01(s.front / 100f) * .2f : 1;                     // frente destruída: sai de frente
        public static float Pull(FBCar c) => c && all.TryGetValue(c, out var s) ? s.pull : 0;                                                         // + = puxa para a esquerda (como o volante)
        public static float Resist(FBCar c)
        {
            var id = c ? c.id ?? "" : ""; if (id.Contains("Caveir")) return .25f; if (c && c.heavy) return .6f; if (id.Contains("Viatura")) return .8f;
            return c && FBCarSound.Sporty(c.engineProf ?? FBCarSound.ProfileFor(c.id, c)) ? 1.15f : 1;
        }
        public static string Severity(float imp) => imp < 9 ? "leve" : imp < 17 ? "moderada" : "grave";
        // posições originais (para o FBDoors achar as peças numa malha já amassada)
        public static Vector3[] RestOf(Mesh m) { foreach (var s in all.Values) if (s.rest.TryGetValue(m, out var r)) return r.p; return null; }
        public static void Reset() { all.Clear(); }
        public static void SetHpTest(FBCar c, float hp) { if (!c) return; var s = Get(c); s.hp = hp; }   // r45e: teste da fumaça do motor
        public static string DebugInfo(FBCar car)
        {
            if (!car || !all.TryGetValue(car, out var s)) return "sem dano";
            int cr = 0, br = 0, panes = 0; foreach (var kv in s.glass) { var g = kv.Value.g; if (g == null) continue; panes += g.panes.Count; foreach (var x in g.state) { if (x == 1) cr++; else if (x >= 2) br++; } }
            return $"vida {s.hp:0} · batidas {s.light} leves/{s.moderate} moderadas/{s.severe} graves · zonas F{s.front:0} T{s.rear:0} E{s.left:0} D{s.right:0} · puxa {s.pull:+0.00;-0.00} · potência {PowerK(car):0.00} máx {TopK(car):0.00} aderência {GripK(car):0.00} · malhas amassadas {s.rest.Count} · vidros {panes} painéis ({cr} trincados, {br} quebrados) · peças soltas {FBDoors.GoneCount(car)} · motor {(s.dead ? "morto" : "ok")}";
        }

        // ---- batida (FBDrive: para-choque do carro do jogador; o outro carro também recebe)
        static float evT;
        public static void OnImpact(FBCar car, Vector3 pW, Vector3 nW, float imp)
        {
            if (imp > 9 && Time.time - evT > 4) { evT = Time.time; FBNpcRotina.Event("acidente", pW, 34, 1); }   // r43: pedestres do 171 comentam
            if (!car || car.kind == "moto" || imp < 3) return; var s = Get(car); if (Time.time - s.lastDent < .18f) return; s.lastDent = Time.time;
            imp *= Resist(car); if (imp < 3) return;
            var sev = Severity(imp); if (sev == "leve") s.light++; else if (sev == "moderada") s.moderate++; else s.severe++;
            float k = Mathf.Clamp01((imp - 3) / 22f);
            Dent(car, s, pW, nW, .035f + .17f * k, .32f + .6f * k);
            if (imp > 6) FBCarLights.BreakNear(car, pW, .45f + .45f * k);
            if (imp > 11) GlassHit(car, s, pW, imp > 18 ? 2 : 1, 1.3f + k, -nW);
            // peças: frente -> capô, trás -> mala, lado -> porta mais perto
            var lp = car.transform.InverseTransformPoint(pW); var L = car.lbox; float zf = (lp.z - L.center.z) / Mathf.Max(.1f, L.extents.z), xs = (lp.x - L.center.x) / Mathf.Max(.1f, L.extents.x);
            float zd = k * 40; if (zf > .55f) s.front += zd; else if (zf < -.55f) s.rear += zd; else if (xs < 0) s.left += zd; else s.right += zd;
            if (zf > .25f && Mathf.Abs(xs) > .35f && imp > 8) s.pull = Mathf.Clamp(s.pull + (xs < 0 ? 1 : -1) * k * .07f, -.2f, .2f);   // roda dianteira daquele lado desalinhou
            if (imp > 12)
            {
                int part = zf > .55f ? 5 : zf < -.55f ? 4 : Mathf.Abs(xs) > .5f ? (xs < 0 ? 0 : 1) + (zf < -.05f ? 2 : 0) : -1;
                if (part >= 0)
                {
                    float was = FBDoors.BentOf(car, part);
                    FBDoors.Bend(car, part, part == 5 ? Mathf.Lerp(.08f, .3f, k) : part == 4 ? Mathf.Lerp(.05f, .22f, k) : Mathf.Lerp(.04f, .14f, k));
                    if (imp > 19 || (imp > 14 && was > .1f)) FBDoors.Detach(car, part, car.transform.forward * car.v * .6f - nW * Mathf.Min(6, imp * .25f) + Vector3.up * 1.5f);   // r45b: grave arranca a peça; moderada numa já empenada também (antes só grave + empenada: carro novo nunca soltava nada)
                }
            }
            Hurt(car, s, Mathf.Pow(Mathf.Max(0, imp - 4), 2) * .3f);
        }
        // ---- tiro (FBCombat): vidro no ponto, lâmpada no ponto, um pouco de vida
        public static void OnShot(FBCar car, Vector3 pW, Vector3 dirW, float dmg)
        {
            if (!car || car.kind == "moto") return; var s = Get(car);
            FBCarLights.BreakNear(car, pW, .2f);
            GlassHit(car, s, pW, 1, .55f, dirW);
            Hurt(car, s, dmg * .12f);
        }
        static void Hurt(FBCar car, St s, float d)
        {
            if (d <= 0 || s.dead) return; s.hp = Mathf.Max(0, s.hp - d);
            if (s.hp <= 0) { s.dead = true; FBAudio.PlayAt("reb_carimpact2", car.transform.position, .5f, 6); if (car.driver && car.driver.isPlayer) FBHUD.I?.Center("MOTOR QUEBRADO · LEVE NA OFICINA", 2.5f); }
        }
        // empurrão de carro parado (sem motorista)
        public static void Shove(FBCar car, Vector3 v, float spin)
        {
            if (!car || car.driver || car.remoteDriver >= 0) return; var s = Get(car); v.y = 0; s.shove += v; s.spin += spin;
        }

        // ---- amassado
        static float H01(int i) { uint h = (uint)i * 2654435761u; h ^= h >> 15; return (h & 1023) / 1023f; }
        static void Dent(FBCar car, St s, Vector3 pW, Vector3 dirW, float depth, float radius)
        {
            if (!car.shell) return; var probe = new Bounds(pW, Vector3.one * radius * 2.2f);
            foreach (var mf in car.shell.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.GetComponent<MeshRenderer>(); var m = mf.sharedMesh; if (!mr || !m || !m.isReadable) continue;
                var n = mf.name; if (n.StartsWith("lente_") || n.StartsWith("brilho") || n.StartsWith("wheel") || n.Contains("roda") || n == "sombra_contato") continue;
                if (n.StartsWith("body_lod2")) continue;                                                   // LOD2 só de muito longe
                if (!mr.bounds.Intersects(probe)) continue;
                if (!s.own.Contains(m)) { var cp = Object.Instantiate(m); cp.name = m.name; mf.sharedMesh = cp; m = cp; s.own.Add(m); RebindGlass(s, mf); }
                if (!s.rest.TryGetValue(m, out var R0)) s.rest[m] = R0 = (m.vertices, m.normals);
                var W2L = mf.transform.worldToLocalMatrix; var pL = W2L.MultiplyPoint3x4(pW); var dL = W2L.MultiplyVector(dirW); float sc = dL.magnitude; if (sc < 1e-6f) continue; dL /= sc;
                float rL = radius * sc, maxL = .22f * sc, dep = depth * sc; var V = m.vertices; var w = new float[V.Length]; bool ch = false;
                for (int i = 0; i < V.Length; i++)
                {
                    var d = V[i] - pL; float dd = d.sqrMagnitude; if (dd > rL * rL) continue;
                    float f = 1 - Mathf.Sqrt(dd) / rL; f *= f; var nv = V[i] + dL * (dep * f * (.7f + .6f * H01(i)));
                    var tot = nv - R0.p[i]; if (tot.sqrMagnitude > maxL * maxL) nv = R0.p[i] + tot.normalized * maxL;
                    V[i] = nv; w[i] = f; ch = true;
                }
                if (!ch) continue;
                m.vertices = V; LocalNormals(m, V, w); m.RecalculateBounds();
            }
        }
        // normais refeitas só onde amassou: soma das normais de face por POSIÇÃO (vértices duplicados na costura de UV recebem a mesma)
        static void LocalNormals(Mesh m, Vector3[] V, float[] w)
        {
            var N = m.normals; if (N == null || N.Length != V.Length) { m.RecalculateNormals(); return; }
            var acc = new Dictionary<Vector3Int, Vector3>();
            Vector3Int K(Vector3 p) => new(Mathf.RoundToInt(p.x * 2000), Mathf.RoundToInt(p.y * 2000), Mathf.RoundToInt(p.z * 2000));
            for (int s = 0; s < m.subMeshCount; s++)
            {
                var T = m.GetTriangles(s);
                for (int t = 0; t + 2 < T.Length; t += 3)
                {
                    int a = T[t], b = T[t + 1], c = T[t + 2]; if (w[a] <= 0 && w[b] <= 0 && w[c] <= 0) continue;
                    var fn = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
                    foreach (var v in new[] { a, b, c }) { var k = K(V[v]); acc[k] = (acc.TryGetValue(k, out var o) ? o : Vector3.zero) + fn; }
                }
            }
            for (int i = 0; i < V.Length; i++)
            {
                if (w[i] <= 0) continue; if (!acc.TryGetValue(K(V[i]), out var sn) || sn.sqrMagnitude < 1e-12f) continue; sn.Normalize();
                if (Vector3.Dot(sn, N[i]) < 0) sn = -sn;                                                   // face invertida/dupla face: mantém o lado
                N[i] = Vector3.Slerp(N[i], sn, Mathf.Clamp01(w[i] * 1.6f + .25f)).normalized;
            }
            m.normals = N;
        }

        // ---- vidros por painel
        static Material CrackMat(Material glass)
        {
            if (crackMat) return crackMat;
            crackMat = glass ? new Material(glass) { name = "vidro_estilhacado" } : new Material(FBShaders.Lit);
            if (crackMat.HasProperty("_BaseColor")) { var c = crackMat.GetColor("_BaseColor"); crackMat.SetColor("_BaseColor", new Color(.78f, .82f, .84f, Mathf.Max(.82f, c.a))); }
            if (crackMat.HasProperty("_Smoothness")) crackMat.SetFloat("_Smoothness", .35f);
            return crackMat;
        }
        static bool IsGlass(Material m) => m && (m.name.Contains("vidro") || m.name.Contains("glass")) && !m.name.Contains("estilha");
        static void RebindGlass(St s, MeshFilter mf) { s.glass.Remove(mf); }
        static Glass GlassOf(St s, MeshFilter mf)
        {
            var m = mf.sharedMesh; if (s.glass.TryGetValue(mf, out var e) && e.m == m) return e.g;
            var mr = mf.GetComponent<MeshRenderer>(); var mats = mr.sharedMaterials; var g = new Glass();
            for (int i = 0; i < Mathf.Min(mats.Length, m.subMeshCount); i++) if (IsGlass(mats[i])) { g.sub = i; break; }
            for (int i = 0; i < Mathf.Min(mats.Length, m.subMeshCount); i++) if (mats[i] && mats[i].name.StartsWith("vidro_estilhacado")) g.crackSub = i;
            if (g.sub < 0) { s.glass[mf] = (m, null); return null; }
            if (!s.own.Contains(m)) { var cp = Object.Instantiate(m); cp.name = m.name; mf.sharedMesh = cp; m = cp; s.own.Add(m); }
            // painéis = componentes conexos (posição de vértice) dos triângulos de vidro (inteiros + já trincados)
            var T = new List<int>(m.GetTriangles(g.sub)); int intact = T.Count; if (g.crackSub >= 0) T.AddRange(m.GetTriangles(g.crackSub));
            g.orig ??= m.GetTriangles(g.sub);
            var V = m.vertices; var pid = new Dictionary<Vector3Int, int>(); var par = new List<int>();
            int Id(int v) { var k = Vector3Int.RoundToInt(V[v] * 1000); if (!pid.TryGetValue(k, out var id)) { pid[k] = id = par.Count; par.Add(id); } return id; }
            int F(int x) { while (par[x] != x) x = par[x] = par[par[x]]; return x; }
            var tid = new int[T.Count / 3, 3];
            for (int t = 0; t < T.Count / 3; t++) { int a = Id(T[t * 3]), b = Id(T[t * 3 + 1]), c = Id(T[t * 3 + 2]); tid[t, 0] = a; tid[t, 1] = b; tid[t, 2] = c; par[F(b)] = F(a); par[F(c)] = F(a); }
            var groups = new Dictionary<int, List<int>>(); var crackedRoot = new HashSet<int>();
            for (int t = 0; t < T.Count / 3; t++) { int r = F(tid[t, 0]); if (!groups.TryGetValue(r, out var l)) groups[r] = l = new List<int>(); l.Add(T[t * 3]); l.Add(T[t * 3 + 1]); l.Add(T[t * 3 + 2]); if (t * 3 >= intact) crackedRoot.Add(r); }
            g.panes = new List<int[]>(); g.centers = new List<Vector3>(); g.state = new List<int>();
            foreach (var kv in groups)
            {
                var l = kv.Value; var c = Vector3.zero; foreach (var v in l) c += V[v]; c /= l.Count;
                g.panes.Add(l.ToArray()); g.centers.Add(c); g.state.Add(crackedRoot.Contains(kv.Key) ? 1 : 0);
            }
            s.glass[mf] = (m, g); return g;
        }
        static void GlassHit(FBCar car, St s, Vector3 pW, int dmg, float radius, Vector3 dirW)
        {
            if (!car.shell) return; MeshFilter bestF = null; int best = -1; float bd = radius;
            Transform lod0 = null; foreach (Transform c in car.shell) { var n = c.name; if (n.StartsWith("body") && !n.StartsWith("body_lod")) { lod0 = c; break; } }
            if (!lod0) return;
            foreach (var mf in lod0.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh || !mf.sharedMesh.isReadable || !mf.GetComponent<MeshRenderer>()) continue; var g = GlassOf(s, mf); if (g == null) continue;
                for (int i = 0; i < g.panes.Count; i++)
                {
                    if (g.state[i] >= 2) continue; float d = Vector3.Distance(mf.transform.TransformPoint(g.centers[i]), pW);
                    if (d < bd) { bd = d; best = i; bestF = mf; }
                }
            }
            if (!bestF) return; var G = GlassOf(s, bestF); int st0 = G.state[best]; G.state[best] = Mathf.Min(2, st0 + dmg);
            var wp = bestF.transform.TransformPoint(G.centers[best]);
            if (G.state[best] >= 2) { FBImpactFx.Glass(wp, -dirW, dirW); FBAudio.PlayAt("glass_break", wp, .8f, 6); }
            else FBAudio.PlayAt("glass_break", wp, .35f, 3);
            ApplyGlass(bestF, G);
        }
        static void ApplyGlass(MeshFilter mf, Glass g)
        {
            var m = mf.sharedMesh; var mr = mf.GetComponent<MeshRenderer>(); var mats = new List<Material>(mr.sharedMaterials);
            if (g.crackSub < 0) { g.crackSub = m.subMeshCount; m.subMeshCount = g.crackSub + 1; while (mats.Count < m.subMeshCount) mats.Add(null); mats[g.crackSub] = CrackMat(mats[g.sub]); mr.sharedMaterials = mats.ToArray(); }
            var a = new List<int>(); var b = new List<int>();
            for (int i = 0; i < g.panes.Count; i++) { if (g.state[i] == 0) a.AddRange(g.panes[i]); else if (g.state[i] == 1) b.AddRange(g.panes[i]); }
            m.SetTriangles(a, g.sub, false); m.SetTriangles(b, g.crackSub, false);
        }

        // ---- conserto (oficina)
        public static void Repair(FBCar car)
        {
            if (!car || !all.TryGetValue(car, out var s)) return;
            foreach (var kv in s.rest) if (kv.Key) { kv.Key.vertices = kv.Value.p; if (kv.Value.n != null && kv.Value.n.Length == kv.Value.p.Length) kv.Key.normals = kv.Value.n; kv.Key.RecalculateBounds(); }
            foreach (var kv in s.glass) { var g = kv.Value.g; if (g == null || !kv.Key || kv.Key.sharedMesh != kv.Value.m) continue; for (int i = 0; i < g.state.Count; i++) g.state[i] = 0; ApplyGlass(kv.Key, g); }
            s.rest.Clear(); s.hp = 100; s.dead = false; s.pull = s.front = s.rear = s.left = s.right = 0; s.light = s.moderate = s.severe = 0; FBCarLights.Repair(car); FBDoors.Unbend(car);
        }

        class Runner : MonoBehaviour
        {
            // [parceiro] v1 (thread de física): tempo do Update medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
            static readonly int __fbt_Update = FBTempo.Id("FBCarDamage.Runner.Update");
            void Update()
            {
                if (!FBTempo.On) { Update__fbt(); return; }
                long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
            }
            void Update__fbt()
            {
                float dt = Time.deltaTime; List<FBCar> dead = null;
                foreach (var kv in all)
                {
                    var car = kv.Key; var s = kv.Value; if (!car) { (dead ??= new List<FBCar>()).Add(car); continue; }
                    // fumaça do motor
                    if (s.hp < 45 && (s.smokeT -= dt) <= 0)
                    {
                        /* r45d: "fumaça do motor muito feia" — era o disco chapado do tiro (FBFx.Puff). Agora sai pela frestas do capô com a folha
                           animada do 171 (FBCarFX), transparente, crescendo e sendo levada pelo movimento do carro */
                        s.smokeT = s.hp < 20 ? .07f : .16f; var L = car.lbox; var T = car.transform;
                        var p = T.TransformPoint(new Vector3(L.center.x + (Random.value - .5f) * L.size.x * .5f, L.max.y * .82f, L.max.z - .5f - Random.value * .5f));
                        var vel = Vector3.up * (.7f + Random.value * .5f) + T.forward * (car.v * .55f) + new Vector3(Random.value - .5f, 0, Random.value - .5f) * .3f;
                        float g = s.hp < 20 ? .16f : .62f; FBCarFX.Puff(p, vel, new Color(g, g, g), s.hp < 20 ? .5f : .3f, .4f, s.hp < 20 ? 2.6f : 1.9f, 1.8f + Random.value * .8f);
                    }
                    // empurrão (carro parado): desliza com atrito e para em parede
                    if (s.shove.sqrMagnitude > .01f || Mathf.Abs(s.spin) > .01f)
                    {
                        var step = s.shove * dt; var t = car.transform;
                        car.SetColliders(false);
                        bool blocked = step.sqrMagnitude > 1e-6f && Physics.BoxCast(t.TransformPoint(car.lbox.center) + Vector3.up * .2f, car.lbox.extents * car.scale * .85f, step.normalized, t.rotation, step.magnitude + .05f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore);
                        car.SetColliders(true);
                        if (blocked) s.shove = Vector3.zero; else t.position += step;
                        t.rotation = Quaternion.Euler(0, s.spin * dt, 0) * t.rotation; car.yaw = -t.eulerAngles.y * Mathf.Deg2Rad;
                        float sp = s.shove.magnitude; s.shove = sp > 0 ? s.shove * Mathf.Max(0, sp - 6 * dt) / sp : Vector3.zero; s.spin = Mathf.MoveTowards(s.spin, 0, 120 * dt);
                        car.SetColliders(false); if (FBWorld.GroundY(t.position.x, t.position.y + 1, t.position.z, .3f, 3) is float gy) t.position = new Vector3(t.position.x, gy, t.position.z); car.SetColliders(true);
                    }
                }
                if (dead != null) foreach (var c in dead) all.Remove(c);
            }
        }
    }
}
