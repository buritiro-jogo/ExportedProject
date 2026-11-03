// PÁSSAROS (20/10, pedido do usuário: "pássaros com movimentações naturais para dar mais vida ao mapa"). Só visual (sem colisor, sem rede):
//  · POMBOS em bandos de 6–12 no chão da praça (pontos abertos da navmesh, sem cobertura): andam com o balanço de cabeça típico do pombo,
//    bicam o chão, viram; alguém a menos de ~5,5 m, tiro a menos de 45 m ou carro passando -> REVOADA em cascata (cada um com um atraso),
//    batida de asa rápida subindo, voo em bando (alvo girando num círculo a 10–16 m + separação), depois escolhem outro ponto longe de
//    quem assustou (chão ou beira de telhado), descem planando e freiam batendo as asas antes de pousar. À noite ficam empoleirados nos
//    telhados; na chuva quase não voam.
//  · URUBUS (marca do céu de Boa Vista) de dia sem chuva: 2–4 planando alto (70–110 m) em círculos de térmica que derivam devagar, asas em
//    V, uma ou duas batidas lentas de vez em quando; pontas das asas claras por baixo (urubu-de-cabeça-preta).
//  · Malha procedural low-poly (corpo, cabeça, asas em 2 segmentos que dobram com atraso = batida natural), materiais URP/Lit (SRP Batcher),
//    sombra projetada. Som da revoada sintetizado (rajadas de ruído filtrado), arrulho do pombo (amb_pigeon0) se existir.
//  · Orçamento: PC 4 bandos + 4 urubus; celular/web 2 bandos + 2 urubus.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
    public class FBBirds : MonoBehaviour
    {
        class Bird
        {
            public Transform root, head, wL, wR, wL2, wR2; public Vector3 pos, vel, off; public float yaw, phase, rate = 6, amp, delay, peckT, stepT, glide;
            public Vector3 walkTo; public float walkWait;
        }
        enum St { Ground, TakeOff, Fly, Land, Roost }
        class Flock { public List<Bird> b = new(); public St st; public Vector3 spot, center, fleeFrom; public float t, ang, radius, alt, flyTime; public bool roof;
            public bool oculto; }   // [parceiro] BURITIRO: bando pousado longe da câmera fica desligado
        class Vulture { public Bird b; public Vector3 c; public float r, a, alt, spd, flapT; public Vector2 drift; }

        readonly List<Flock> flocks = new(); readonly List<Vulture> vultures = new(); readonly List<(Vector3 p, bool roof)> spots = new();
        static Mesh mBody, mHead, mWing, mWing2, vBody, vHead, vWing, vWing2; static Material matP, matPW, matV, matVT, matHead;
        float groundY; bool night, rain, mobile; AudioClip flapClip; float lastNoise = -99;
        public static FBBirds I;

        public static FBBirds Begin(Transform parent, float groundY, bool night, bool rain)
        {
            var go = new GameObject("FB_Passaros"); go.transform.SetParent(parent, false); var b = go.AddComponent<FBBirds>();
            b.groundY = groundY; b.night = night; b.rain = rain; return b;
        }

        void Start()
        {
            I = this; mobile = FBQuality.Mobile; Meshes(); FindSpots(); if (spots.Count == 0) return;
            var rnd = new System.Random(1234); int nf = mobile ? 2 : 4;
            var ground = spots.FindAll(s => !s.roof); var roofs = spots.FindAll(s => s.roof);
            for (int f = 0; f < nf; f++)
            {
                var pool = night && roofs.Count > 0 ? roofs : ground.Count > 0 ? ground : roofs; if (pool.Count == 0) break;
                var sp = pool[(f * 7 + rnd.Next(pool.Count)) % pool.Count]; var fl = new Flock { spot = sp.p, roof = sp.roof, st = night ? St.Roost : St.Ground };
                int n = rnd.Next(mobile ? 5 : 6, mobile ? 8 : 13);
                for (int i = 0; i < n; i++)
                {
                    var bd = NewBird(false); float a = (float)rnd.NextDouble() * 6.28f, r = (float)rnd.NextDouble() * 1.8f;
                    bd.off = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r); bd.pos = Snap(sp.p + bd.off, sp.p.y); bd.yaw = (float)rnd.NextDouble() * 360; bd.walkTo = bd.pos; bd.walkWait = (float)rnd.NextDouble() * 2;
                    fl.b.Add(bd);
                }
                flocks.Add(fl);
            }
            if (!night && !rain)
            {
                var c0 = Vector3.zero; foreach (var s in ground) c0 += s.p; c0 /= Mathf.Max(1, ground.Count);
                int nv = mobile ? 2 : 4;
                for (int i = 0; i < nv; i++)
                {
                    var v = new Vulture { b = NewBird(true), c = c0 + new Vector3((float)rnd.NextDouble() * 120 - 60, 0, (float)rnd.NextDouble() * 120 - 60), r = 25 + (float)rnd.NextDouble() * 22,
                        a = (float)rnd.NextDouble() * 6.28f, alt = groundY + 70 + (float)rnd.NextDouble() * 40, spd = 9 + (float)rnd.NextDouble() * 2.5f, flapT = 5 + (float)rnd.NextDouble() * 20,
                        drift = new Vector2((float)rnd.NextDouble() - .5f, (float)rnd.NextDouble() - .5f) * .6f };
                    if (i % 2 == 1 && vultures.Count > 0) { v.c = vultures[i - 1].c + new Vector3(8, 0, -6); v.drift = vultures[i - 1].drift; }   // dois na mesma térmica
                    vultures.Add(v);
                }
            }
            flapClip = FlapClip();
            Debug.Log($"[FreeBala][passaros] lugares {spots.Count} (telhados {spots.FindAll(s => s.roof).Count}) · bandos {flocks.Count} ({flocks.Sum(f => f.b.Count)} pombos) · urubus {vultures.Count}");
        }
        void OnDestroy() { if (I == this) I = null; }
        // vitrine (-fbgfx): centro do bando no chão mais perto / posição de um urubu
        public Vector3? GroundFlock(Vector3 near) { Vector3? best = null; float bd = 1e9f; foreach (var f in flocks) if (f.st == St.Ground || f.st == St.Roost) { float d = (f.center - near).sqrMagnitude; if (d < bd) { bd = d; best = f.center; } } return best; }
        public Vector3? VulturePos() => vultures.Count > 0 ? vultures[0].b.pos : (Vector3?)null;

        // ---------------------------------------------------------------- malhas e materiais
        static Material Mat(Color c, float sm = .2f)
        {
            var m = new Material(FBShaders.Lit) { name = "FB_passaro", enableInstancing = true }; m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", sm); m.SetFloat("_Metallic", 0); return m;
        }
        static void Meshes()
        {
            if (mBody) return;
            matP = Mat(new Color(.36f, .38f, .44f)); matHead = Mat(new Color(.22f, .26f, .3f), .35f); matPW = Mat(new Color(.55f, .57f, .62f));
            matV = Mat(new Color(.05f, .05f, .055f), .25f); matVT = Mat(new Color(.62f, .62f, .6f));
            mBody = Body(.15f, .11f, .062f, .075f, .05f, .2f, .05f); mHead = Head(.032f, .028f, new Color(.9f, .7f, .2f));
            mWing = Wing(.16f, .11f, .085f); mWing2 = Wing(.17f, .085f, .04f);
            vBody = Body(.3f, .22f, .1f, .12f, .09f, .35f, .1f); vHead = Head(.045f, .05f, Color.gray);
            vWing = Wing(.34f, .3f, .27f); vWing2 = Wing(.4f, .27f, .12f, 5);
        }
        // corpo em fuso (8 faces) + cauda leque; frente = +Z, cima = +Y
        static Mesh Body(float front, float back, float w, float top, float bot, float tail, float tailW)
        {
            var P = new[] { new Vector3(0, .01f, front), new Vector3(0, 0, -back), new Vector3(0, top, 0), new Vector3(0, -bot, .01f), new Vector3(-w, 0, 0), new Vector3(w, 0, 0) };
            var F = new List<int[]> { new[] { 0, 2, 5 }, new[] { 0, 4, 2 }, new[] { 0, 5, 3 }, new[] { 0, 3, 4 }, new[] { 1, 5, 2 }, new[] { 1, 2, 4 }, new[] { 1, 3, 5 }, new[] { 1, 4, 3 } };
            var v = new List<Vector3>(); var t = new List<int>();
            foreach (var f in F) { int b = v.Count; v.Add(P[f[0]]); v.Add(P[f[1]]); v.Add(P[f[2]]); t.AddRange(new[] { b, b + 1, b + 2 }); }
            void Q(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d); t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3, i, i + 2, i + 1, i, i + 3, i + 2 }); }
            Q(new Vector3(-tailW * .5f, .01f, -back * .8f), new Vector3(tailW * .5f, .01f, -back * .8f), new Vector3(tailW, 0, -back - tail * .5f), new Vector3(-tailW, 0, -back - tail * .5f));
            return Make(v, t, "FB_ave_corpo");
        }
        static Mesh Head(float r, float beak, Color _)
        {
            var P = new[] { new Vector3(0, 0, r), new Vector3(0, 0, -r), new Vector3(0, r, 0), new Vector3(0, -r * .8f, 0), new Vector3(-r * .8f, 0, 0), new Vector3(r * .8f, 0, 0), new Vector3(0, -r * .2f, r + beak) };
            var F = new[] { new[] { 0, 2, 5 }, new[] { 0, 4, 2 }, new[] { 0, 5, 3 }, new[] { 0, 3, 4 }, new[] { 1, 5, 2 }, new[] { 1, 2, 4 }, new[] { 1, 3, 5 }, new[] { 1, 4, 3 }, new[] { 6, 0, 5 }, new[] { 6, 4, 0 }, new[] { 6, 3, 4 }, new[] { 6, 5, 3 } };
            var v = new List<Vector3>(); var t = new List<int>();
            foreach (var f in F) { int b = v.Count; v.Add(P[f[0]]); v.Add(P[f[1]]); v.Add(P[f[2]]); t.AddRange(new[] { b, b + 1, b + 2 }); }
            return Make(v, t, "FB_ave_cabeca");
        }
        // segmento de asa: da raiz (x = 0) até x = len, corda rootC -> tipC, levemente varrida para trás; dupla face; 'fingers' = penas da ponta
        static Mesh Wing(float len, float rootC, float tipC, int fingers = 0)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            void Tri(Vector3 a, Vector3 b, Vector3 c) { int i = v.Count; v.Add(a); v.Add(b); v.Add(c); t.AddRange(new[] { i, i + 1, i + 2 }); i = v.Count; v.Add(a); v.Add(c); v.Add(b); t.AddRange(new[] { i, i + 1, i + 2 }); }
            var a0 = new Vector3(0, 0, rootC * .35f); var b0 = new Vector3(0, 0, -rootC * .65f); var a1 = new Vector3(len, 0, tipC * .2f - len * .12f); var b1 = new Vector3(len * .96f, 0, -tipC * .8f - len * .18f);
            Tri(a0, a1, b1); Tri(a0, b1, b0);
            for (int k = 0; k < fingers; k++)                                                 // penas primárias separadas (urubu)
            {
                float z0 = Mathf.Lerp(a1.z, b1.z, (k + .2f) / fingers), z1 = Mathf.Lerp(a1.z, b1.z, (k + .8f) / fingers);
                Tri(new Vector3(len * .97f, 0, z0), new Vector3(len * 1.14f, 0, (z0 + z1) * .5f - .02f), new Vector3(len * .97f, 0, z1));
            }
            return Make(v, t, "FB_ave_asa");
        }
        static Mesh Make(List<Vector3> v, List<int> t, string n) { var m = new Mesh { name = n }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m; }

        Bird NewBird(bool vulture)
        {
            var b = new Bird(); var root = new GameObject(vulture ? "Urubu" : "Pombo").transform; root.SetParent(transform, false); b.root = root;
            // [parceiro] BURITIRO: urubu voa a 70-110 m (sombra não aparece) e a cabeça é pequena demais para sombra
            Part(root, vulture ? vBody : mBody, vulture ? matV : matP, !vulture);
            b.head = new GameObject("cabeca").transform; b.head.SetParent(root, false); b.head.localPosition = vulture ? new Vector3(0, .07f, .3f) : new Vector3(0, .08f, .14f); Part(b.head, vulture ? vHead : mHead, vulture ? matHead : matHead, false);
            Transform W(string n, Transform par, Vector3 lp, Mesh m, Material mt, bool mirror)
            { var w = new GameObject(n).transform; w.SetParent(par, false); w.localPosition = lp; if (mirror) w.localScale = new Vector3(-1, 1, 1); Part(w, m, mt, false); return w; }   // asas sem sombra (a do corpo basta)
            float sx = vulture ? .07f : .04f, sy = vulture ? .06f : .035f, l1 = vulture ? .34f : .16f;
            b.wR = W("asaD", root, new Vector3(sx, sy, .01f), vulture ? vWing : mWing, vulture ? matV : matPW, false); b.wL = W("asaE", root, new Vector3(-sx, sy, .01f), vulture ? vWing : mWing, vulture ? matV : matPW, true);
            b.wR2 = W("pontaD", b.wR, new Vector3(l1, 0, 0), vulture ? vWing2 : mWing2, vulture ? matVT : matPW, false); b.wL2 = W("pontaE", b.wL, new Vector3(l1, 0, 0), vulture ? vWing2 : mWing2, vulture ? matVT : matPW, false);
            b.phase = Random.value * 6.28f; b.rate = vulture ? 2.4f : Random.Range(5.5f, 7.5f);
            if (vulture) root.localScale = Vector3.one * Random.Range(.95f, 1.1f); else root.localScale = Vector3.one * Random.Range(1.05f, 1.25f);   // mapa ampliado 1,2x
            return b;
        }
        static void Part(Transform t, Mesh m, Material mt, bool shadow = true)
        {
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = m; var r = t.gameObject.AddComponent<MeshRenderer>(); r.sharedMaterial = mt;
            r.shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off; r.receiveShadows = true; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        // ---------------------------------------------------------------- lugares: chão aberto (navmesh) e beiras de telhado
        void FindSpots()
        {
            if (!FBNav.Ready) return; var pts = FBNav.Fixed(); int step = Mathf.Max(1, pts.Count / 160);
            for (int i = 0; i < pts.Count; i += step)
            {
                var p = pts[i];
                if (!Physics.Raycast(p + Vector3.up * 2, Vector3.down, out var h, 5, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) || h.normal.y < .95f) continue;
                if (Physics.Raycast(h.point + Vector3.up * .3f, Vector3.up, 25, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) || FBWorld.WaterAt(h.point.x, h.point.z) != null) continue;
                if (Physics.CheckSphere(h.point + Vector3.up * 1.9f, 1.3f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore)) continue;   // longe de parede/banco/poste (a esfera não encosta no chão)
                spots.Add((h.point, false));
                var q = h.point + new Vector3(Mathf.Sin(i * 1.7f), 0, Mathf.Cos(i * 1.7f)) * 18;              // telhado por perto?
                if (Physics.Raycast(q + Vector3.up * 40, Vector3.down, out var hr, 60, 1 << FBWorld.LMap, QueryTriggerInteraction.Ignore) && hr.normal.y > .9f && hr.point.y > h.point.y + 3.2f && hr.point.y < h.point.y + 20)
                    spots.Add((hr.point, true));
            }
        }
        static Vector3 Snap(Vector3 p, float fallback) => Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var h, 4, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) ? h.point : new Vector3(p.x, fallback, p.z);

        // ---------------------------------------------------------------- comportamento
        static readonly int __fbt_Update = FBTempo.Id("FBBirds.Update");   // [parceiro 01/11] medidor FBTempo
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            float dt = Mathf.Min(Time.deltaTime, .05f); var cam = Camera.main;
            bool shot = Time.time - FBBotBrain.lastNoiseT < .25f && FBBotBrain.lastNoiseT != lastNoise; Vector3 shotP = FBBotBrain.lastNoiseP ?? Vector3.zero; if (shot) lastNoise = FBBotBrain.lastNoiseT;
            foreach (var f in flocks) TickFlock(f, dt, shot, shotP);
            foreach (var v in vultures) TickVulture(v, dt);
        }
        Vector3? Threat(Vector3 c, float r)
        {
            foreach (var a in FBMatch.All) { if (!a || !a.alive) continue; var p = a.Pos; if ((p - c).sqrMagnitude < r * r && Mathf.Abs(p.y - c.y) < 4) return p; }
            var cam = Camera.main; if (cam && (cam.transform.position - c).sqrMagnitude < r * r * .6f) return cam.transform.position;
            return null;
        }
        void TickFlock(Flock f, float dt, bool shot, Vector3 shotP)
        {
            f.t += dt; var c = Vector3.zero; foreach (var b in f.b) c += b.pos; c /= f.b.Count; f.center = c;
            // [parceiro] BURITIRO: pombo de 15 cm a mais de 80 m vira 1 ou 2 pixels; pousado assim ele some (menos draw calls e sombras) e volta a menos de 70 m
            bool longe = (f.st == St.Ground || f.st == St.Roost) && cAmTela(c) > (f.oculto ? 70 : 80);
            if (longe != f.oculto) { f.oculto = longe; foreach (var bd in f.b) if (bd.root) bd.root.gameObject.SetActive(!longe); }
            switch (f.st)
            {
                case St.Ground:
                case St.Roost:
                {
                    var th = Threat(c, f.st == St.Roost ? 3.5f : 5.5f);
                    if ((shot && (shotP - c).sqrMagnitude < 45 * 45) || th != null) { TakeOff(f, th ?? shotP); break; }
                    if (f.st == St.Ground && !night && f.t > 25 && Random.value < dt / 40) TakeOff(f, c + Random.onUnitSphere * 3);   // às vezes voam sozinhos
                    if (!f.oculto) foreach (var b in f.b) GroundBird(b, f, dt);   // [parceiro 01/11] escondido: não anda
                    if (!night && Random.value < dt / 9 && cAm(c) < 30) FBAudio.PlayAt("amb_pigeon0", c, .35f, 3, .12f);   // arrulho (se o clipe existir)
                    break;
                }
                case St.TakeOff:
                case St.Fly:
                {
                    f.ang += dt * 9.5f / Mathf.Max(6, f.radius); var tgt = f.spot + new Vector3(Mathf.Cos(f.ang) * f.radius, 0, Mathf.Sin(f.ang) * f.radius); tgt.y = f.alt;
                    if (f.st == St.TakeOff && f.t > 1.2f) f.st = St.Fly;
                    if (f.st == St.Fly && f.t > f.flyTime) { ChooseLanding(f); f.st = St.Land; f.t = 0; }
                    foreach (var b in f.b) FlyBird(b, f, tgt, dt, f.st == St.TakeOff ? 9.5f : 6.2f);
                    break;
                }
                case St.Land:
                {
                    int down = 0;
                    foreach (var b in f.b)
                    {
                        var tp = f.spot + b.off; float hd = new Vector2(tp.x - b.pos.x, tp.z - b.pos.z).magnitude;
                        if (b.vel == Vector3.zero) { down++; GroundBird(b, f, dt); continue; }
                        var aim = tp + Vector3.up * Mathf.Clamp(hd * .35f, 0, 6);
                        FlyBird(b, f, aim, dt, hd < 3 ? 11 : 6.5f, Mathf.Clamp(hd / 9, .25f, 1));
                        if (hd < .35f && b.pos.y - tp.y < .4f) { b.pos = Snap(tp, tp.y); b.vel = Vector3.zero; b.walkTo = b.pos; Fold(b); down++; }
                    }
                    if (down == f.b.Count) { f.st = night || f.roof ? St.Roost : St.Ground; f.t = 0; }
                    if (f.t > 25) foreach (var b in f.b) { b.pos = Snap(f.spot + b.off, f.spot.y); b.vel = Vector3.zero; Fold(b); }   // segurança: não ficam voando para sempre
                    break;
                }
            }
            foreach (var b in f.b) Pose(b, dt, b.vel == Vector3.zero);
        }
        float cAm(Vector3 p) { var cam = Camera.main; return cam ? Vector3.Distance(cam.transform.position, p) : 999; }
        // [parceiro] BURITIRO 01/11: distância equivalente na tela (FOV 55); com luneta o pombo aparece bem maior e não pode sumir
        float cAmTela(Vector3 p)
        {
            var cam = Camera.main; if (!cam) return 999;
            float d = Vector3.Distance(cam.transform.position, p);
            return d * Mathf.Min(1, Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad) / .5206f);
        }
        void TakeOff(Flock f, Vector3 from)
        {
            f.st = St.TakeOff; f.t = 0; f.fleeFrom = from; var away = f.center - from; away.y = 0; if (away.sqrMagnitude < .01f) away = Random.insideUnitSphere; away.y = 0; away.Normalize();
            f.radius = Random.Range(10f, 18f); f.alt = f.spot.y + Random.Range(9f, 15f) + (rain ? -3 : 0); f.flyTime = rain ? Random.Range(4f, 7f) : Random.Range(7f, 14f);
            f.ang = Mathf.Atan2(away.z, away.x); f.spot = f.center + away * 6;
            foreach (var b in f.b)
            {
                b.delay = Random.Range(0, .45f); var d = (away + Random.insideUnitSphere * .5f); d.y = 0;
                b.vel = d.normalized * Random.Range(2.5f, 3.5f) + Vector3.up * Random.Range(3.5f, 4.8f); b.rate = Random.Range(8.5f, 10f);
            }
            if (flapClip && cAm(f.center) < 70) FBAudio.PlayAt(flapClip, f.center, .9f, 6, .1f);
        }
        void ChooseLanding(Flock f)
        {
            var best = f.spot; float bs = -1; bool roof = false;
            for (int k = 0; k < 12 && spots.Count > 0; k++)
            {
                var s = spots[Random.Range(0, spots.Count)]; float d = Vector3.Distance(s.p, f.center); if (d > 70 || d < 8) continue;
                if (night && !s.roof) continue;
                float sc = Vector3.Distance(s.p, f.fleeFrom) + (s.roof ? 6 : 0) + Random.value * 10; if (Threat(s.p, 9) != null) sc -= 50;
                if (sc > bs) { bs = sc; best = s.p; roof = s.roof; }
            }
            f.spot = best; f.roof = roof;
            foreach (var b in f.b) { float a = Random.value * 6.28f, r = Random.value * (roof ? 1.2f : 1.9f); b.off = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r); }
        }
        void GroundBird(Bird b, Flock f, float dt)
        {
            b.walkWait -= dt; b.peckT -= dt;
            var d = b.walkTo - b.pos; d.y = 0;
            if (d.magnitude > .04f)
            {
                var step = d.normalized * Mathf.Min(d.magnitude, .45f * dt); b.pos += step; b.stepT += dt * 7;
                b.yaw = Mathf.MoveTowardsAngle(b.yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 360 * dt);
                if (Random.value < dt * 2) b.pos.y = Snap(b.pos, b.pos.y).y;
            }
            else if (b.walkWait <= 0)
            {
                float a = Random.value * 6.28f, r = Random.Range(.2f, .9f); var w = f.spot + b.off + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                b.walkTo = new Vector3(w.x, b.pos.y, w.z); b.walkWait = Random.Range(.6f, 3.5f);
                if (f.st == St.Roost) b.walkTo = b.pos;
            }
            if (b.peckT <= -.28f) b.peckT = Random.Range(.8f, 4f);
        }
        void FlyBird(Bird b, Flock f, Vector3 tgt, float dt, float speed, float flapK = 1)
        {
            if (b.delay > 0) { b.delay -= dt; return; }
            var want = (tgt + new Vector3(b.off.x * 1.6f, b.off.x * .6f, b.off.z * 1.6f) - b.pos); var dv = want.normalized * speed - b.vel;
            foreach (var o in f.b) { if (o == b) continue; var s = b.pos - o.pos; float m = s.magnitude; if (m < .9f && m > 1e-3f) dv += s / m * (1.2f - m) * 6; }
            b.vel += Vector3.ClampMagnitude(dv, 14 * dt); b.pos += b.vel * dt;
            var hv = new Vector2(b.vel.x, b.vel.z); if (hv.sqrMagnitude > .01f) b.yaw = Mathf.MoveTowardsAngle(b.yaw, Mathf.Atan2(hv.x, hv.y) * Mathf.Rad2Deg, 240 * dt);
            b.glide = Mathf.MoveTowards(b.glide, b.vel.y < -1.2f && flapK > .5f ? 1 : 0, dt * 2);   // descendo: plana
            b.amp = Mathf.Lerp(62, 18, b.glide) * Mathf.Lerp(1.15f, 1, flapK);
        }
        void Fold(Bird b) { b.amp = 0; b.glide = 0; }
        void Pose(Bird b, float dt, bool onGround)
        {
            var r = b.root; r.position = b.pos;
            if (onGround)
            {
                float bob = Mathf.Repeat(b.stepT, 1); r.rotation = Quaternion.Euler(0, b.yaw, 0);
                float peck = b.peckT < 0 ? Mathf.Sin(-b.peckT / .28f * Mathf.PI) : 0;
                b.head.localPosition = new Vector3(0, .08f - peck * .06f, .14f + (bob < .5f ? bob * .05f : (1 - bob) * .01f) + peck * .02f);   // balanço de cabeça do pombo
                b.head.localRotation = Quaternion.Euler(peck * 65, 0, 0);
                b.wR.localRotation = Quaternion.Euler(0, 82, -8); b.wL.localRotation = Quaternion.Euler(0, -82, 8);       // asas fechadas ao longo do corpo
                b.wR2.localRotation = Quaternion.Euler(0, 172, 0); b.wL2.localRotation = Quaternion.Euler(0, 172, 0);
                return;
            }
            float pitch = Mathf.Clamp(-b.vel.y * 4, -35, 35); float yawRate = 0; r.rotation = Quaternion.Euler(pitch, b.yaw, yawRate);
            b.head.localPosition = new Vector3(0, .07f, .15f); b.head.localRotation = Quaternion.identity;
            b.phase += dt * b.rate * 6.2832f; float s1 = Mathf.Sin(b.phase), s2 = Mathf.Sin(b.phase - .9f);
            float a1 = b.amp * s1 + 6, a2 = b.amp * .55f * s2;
            b.wR.localRotation = Quaternion.Euler(0, 0, a1); b.wL.localRotation = Quaternion.Euler(0, 0, -a1);
            b.wR2.localRotation = Quaternion.Euler(0, 0, a2); b.wL2.localRotation = Quaternion.Euler(0, 0, a2);
        }
        void TickVulture(Vulture v, float dt)
        {
            var b = v.b; v.c += new Vector3(v.drift.x, 0, v.drift.y) * dt; v.a += dt * v.spd / v.r;
            float alt = v.alt + Mathf.Sin(v.a * .37f) * 6;
            var p = v.c + new Vector3(Mathf.Cos(v.a) * v.r, 0, Mathf.Sin(v.a) * v.r); p.y = alt;
            var vel = (p - b.pos) / Mathf.Max(dt, 1e-3f); if (b.pos == Vector3.zero) vel = Vector3.zero; b.pos = p;
            float yaw = Mathf.Atan2(-Mathf.Sin(v.a), Mathf.Cos(v.a)) * Mathf.Rad2Deg; b.yaw = yaw;
            b.root.position = p; b.root.rotation = Quaternion.Euler(Mathf.Clamp(-vel.y * 2, -8, 8), yaw, 24);           // inclinado para dentro da curva
            v.flapT -= dt; float amp = 0;
            if (v.flapT < 0) { amp = 38; if (v.flapT < -1.25f) v.flapT = Random.Range(12f, 30f); }                        // 3 batidas lentas
            b.phase += dt * 2.4f * 6.2832f; float s1 = amp > 0 ? Mathf.Sin(b.phase) : 0;
            float dih = 9;                                                                                               // asas em V raso
            b.wR.localRotation = Quaternion.Euler(0, 0, dih + amp * s1); b.wL.localRotation = Quaternion.Euler(0, 0, -dih - amp * s1);
            b.wR2.localRotation = Quaternion.Euler(0, 0, -4 + amp * .5f * Mathf.Sin(b.phase - .9f)); b.wL2.localRotation = b.wR2.localRotation;
        }

        // som da revoada: ~0,9 s de rajadas de ruído (cada batida de asa ~25 ms) de vários pássaros desencontrados, passa-baixa
        static AudioClip FlapClip()
        {
            const int SR = 22050; int n = (int)(SR * .95f); var d = new float[n]; var rnd = new System.Random(7);
            for (int bird = 0; bird < 9; bird++)
            {
                float t0 = (float)rnd.NextDouble() * .35f, rate = 8 + (float)rnd.NextDouble() * 3; float g = .55f + (float)rnd.NextDouble() * .45f;
                for (float t = t0; t < .9f; t += 1f / rate)
                {
                    int s0 = (int)(t * SR), len = (int)(SR * .028f); float lp = 0, env0 = g * Mathf.Clamp01(1.2f - t * 1.1f);
                    for (int i = 0; i < len && s0 + i < n; i++) { float e = Mathf.Sin(Mathf.PI * i / len); float w = (float)(rnd.NextDouble() * 2 - 1); lp += (w - lp) * .35f; d[s0 + i] += lp * e * env0 * .5f; }
                }
            }
            float mx = 1e-4f; foreach (var x in d) mx = Mathf.Max(mx, Mathf.Abs(x)); for (int i = 0; i < n; i++) d[i] = d[i] / mx * .8f;
            var c = AudioClip.Create("revoada", n, 1, SR, false); c.SetData(d, 0); return c;
        }
    }
}
