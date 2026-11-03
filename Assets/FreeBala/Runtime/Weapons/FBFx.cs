// Efeitos de tiro (porte do world.js): clarão estelar + núcleo, luz inteligente do disparo (pool fixo de 6 luzes + 2 de explosão —
// regra anti-travada do jogo web: nenhuma luz/material é criado nem destruído na partida), traçante que voa, fumaça genérica e faísca.
// Fachada dos efeitos de combate: sangue (FBBloodFx/FBWoundFx), decals (FBDecalFx), impacto por material (FBImpactFx), explosão e
// fumaça de granada (FBExplosionFx), fumaça do disparo e cartuchos (FBShotFx). A API antiga (BulletHole, BloodSplat, BloodSpray,
// BloodPool, Splash, Explosion, Impact) continua valendo e delega para eles. Tudo pré-aquecido no carregamento (Warm).
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public static class FBFx
    {
        public static bool BloodOn = true;
        static bool ready; static GameObject root;
        static ParticleSystem psFlash, psCore, psPuff, psSpark;
        class FL { public Light l; public float i, life, t; public bool on; }
        static readonly List<FL> flash = new(); static FL[] boom;
        class Tr { public LineRenderer lr; public Vector3 a, dir; public float len, streak, t, life; public bool on; }
        static readonly List<Tr> tracers = new();

        // ---- texturas (canvas do web)
        static Texture2D Tex(int n, System.Func<float, float, Color> f)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp }; var px = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) px[y * n + x] = f((x + .5f) / n * 2 - 1, (y + .5f) / n * 2 - 1);
            t.SetPixels(px); t.Apply(true, true); return t;
        }
        static Color Grad(float d, params (float s, Color c)[] st) { if (d <= st[0].s) return st[0].c; for (int i = 1; i < st.Length; i++) if (d <= st[i].s) return Color.Lerp(st[i - 1].c, st[i].c, (d - st[i - 1].s) / (st[i].s - st[i - 1].s)); return st[^1].c; }
        public static Texture2D FlashTex() => Tex(64, (x, y) =>
        {
            float a = Mathf.Atan2(y, x), r = Mathf.Sqrt(x * x + y * y), star = Mathf.Lerp(12f / 32, 31f / 32, .5f + .5f * Mathf.Cos(a * 6));    // estrela de 6 pontas
            var c = Grad(r / .94f, (0, new Color(1, 1, .9f, 1)), (.25f, new Color(1, .78f, .31f, .9f)), (1, new Color(1, .47f, 0, 0))); if (r > star) c.a = 0; return c;
        });
        static Texture2D CoreTex() => Tex(64, (x, y) => { float r = Mathf.Sqrt(x * x + y * y) / .875f; var c = Grad(r, (0, Color.white), (.25f, new Color(1, .96f, .78f, .95f)), (.65f, new Color(1, .67f, .2f, .45f)), (1, new Color(1, .4f, 0, 0))); if (r > 1) c.a = 0; return c; });
        static Texture2D PuffTex() => Tex(64, (x, y) => { float r = Mathf.Sqrt(x * x + y * y) / .94f; return new Color(1, 1, 1, Mathf.Lerp(.9f, 0, Mathf.Clamp01(r))); });
        // materiais clonados dos modelos de Resources/FBMats (variante transparente garantida no build)
        static Material Mat(Texture2D t, bool additive, Color tint) => FBFxLib.Mat(additive ? "Particles_Add" : "Particles_Alpha", t, tint, "Tiro");
        static ParticleSystem PS(string n, Material m, float gravity, int max = 600) => FBFxLib.PS(n, m, max, new FBFxLib.Opt { gravity = gravity, color = FBFxLib.Fade() });

        public static void Warm()
        {
            if (ready) return; ready = true;
            root = new GameObject("FB_FX"); Object.DontDestroyOnLoad(root); FBFxLib.Root = root.transform; FBFxQuality.Ensure();
            var ft = FlashTex(); var pt = PuffTex();
            psFlash = PS("Clarao", Mat(ft, true, Color.white), 0); psCore = PS("Nucleo", Mat(CoreTex(), true, Color.white), 0);
            psPuff = PS("Fumaca", Mat(pt, false, Color.white), -.06f); psSpark = PS("Faisca", Mat(ft, true, Color.white), 0);
            // sizeOverLifetime da fumaça: cresce (0,4 → 1,6)
            var so = psPuff.sizeOverLifetime; so.enabled = true; so.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .4f, 1, 1.6f));
            for (int i = 0; i < 6; i++) flash.Add(new FL { l = Light("LuzTiro" + i, 22) });
            boom = new[] { new FL { l = Light("LuzExplosao0", 12) }, new FL { l = Light("LuzExplosao1", 12) } }; foreach (var b in boom) b.l.color = new Color(1, .63f, .25f);
            var trMat = Mat(Tex(8, (x, y) => Color.white), true, new Color(1, .91f, .63f));
            for (int i = 0; i < 48; i++) { var g = new GameObject("Tracante" + i); g.transform.SetParent(root.transform); var lr = g.AddComponent<LineRenderer>(); lr.sharedMaterial = trMat; lr.positionCount = 2; lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.numCapVertices = 2; lr.enabled = false; tracers.Add(new Tr { lr = lr }); }
            FBDecalFx.Warm(); FBWoundFx.Warm(); FBBloodFx.Warm(); FBImpactFx.Warm(); FBExplosionFx.Warm(); FBShotFx.Warm();
            root.AddComponent<FBFxTicker>();
        }
        static Light Light(string n, float range) { var g = new GameObject(n); g.transform.SetParent(root.transform); var l = g.AddComponent<Light>(); l.type = LightType.Point; l.range = range; l.intensity = 0; l.shadows = LightShadows.None; l.color = new Color(1, .89f, .58f); return l; }

        static void Emit(ParticleSystem ps, Vector3 p, Vector3 v, float size, float life, Color c, float rot = 0) => FBFxLib.Emit(ps, p, v, size, life, c, rot);

        // ---- clarão do cano + luz inteligente (intensidade/alcance/duração por classe) + fumaça do disparo + cartucho
        public static void Muzzle(Vector3 p, float size, string cls, bool silenced = false)
        {
            Warm(); float eff = size * (silenced ? .45f : 1);
            Emit(psFlash, p, Vector3.zero, eff * Random.Range(.8f, 1.3f), silenced ? .035f : .05f, silenced ? new Color(.8f, .6f, .4f) : Color.white, Random.Range(0, 360f));
            if (!silenced) Emit(psCore, p, Vector3.zero, eff * .62f * Random.Range(.9f, 1.2f), .04f, Color.white);
            float I = 22, D = 22, T = .065f;
            if (silenced) { I = 4; D = 7; T = .045f; } else if (cls == "pistola") { I = 13; D = 15; T = .055f; } else if (cls == "submetralhadora") { I = 17; D = 18; T = .05f; }
            else if (cls == "escopeta") { I = 30; D = 26; T = .08f; } else if (cls == "sniper") { I = 38; D = 32; T = .09f; }
            var col = silenced ? new Color(1, .77f, .44f) : Random.value < .4f ? new Color(1, .95f, .75f) : Random.value < .6f ? new Color(1, .87f, .47f) : new Color(1, .75f, .27f);
            FL s = flash.Find(f => !f.on); if (s == null) { s = flash[0]; foreach (var f in flash) if (f.t / f.life > s.t / s.life) s = f; }
            s.l.transform.position = p; s.l.color = col; s.l.range = D; s.i = I * Random.Range(.95f, 1.15f) * .12f; s.l.intensity = s.i; s.life = T; s.t = 0; s.on = true;   // ×0,12: intensidade física do URP
            Shooter(p, out var fwd, out var right, out var port);
            FBShotFx.Smoke(p, fwd, size, cls, silenced); FBShotFx.Shell(port, fwd, right, cls);
        }
        public static void Muzzle(Transform at, string cls) { if (at) Muzzle(at.position, .55f, cls); }

        // Efeito de luz ambiente + fumaça + cartucho no mundo para tiro em 1ª pessoa / mira óptica
        // (o clarão do cano fica no viewmodel na camada LFirstPerson sem distorção nem paralaxe de FOV)
        public static void MuzzleWorldLight(Vector3 p, string cls, bool silenced = false)
        {
            Warm();
            float I = 22, D = 22, T = .065f;
            if (silenced) { I = 4; D = 7; T = .045f; } else if (cls == "pistola") { I = 13; D = 15; T = .055f; } else if (cls == "submetralhadora") { I = 17; D = 18; T = .05f; }
            else if (cls == "escopeta") { I = 30; D = 26; T = .08f; } else if (cls == "sniper") { I = 38; D = 32; T = .09f; }
            var col = silenced ? new Color(1, .77f, .44f) : Random.value < .4f ? new Color(1, .95f, .75f) : Random.value < .6f ? new Color(1, .87f, .47f) : new Color(1, .75f, .27f);
            FL s = flash.Find(f => !f.on); if (s == null) { s = flash[0]; foreach (var f in flash) if (f.t / f.life > s.t / s.life) s = f; }
            s.l.transform.position = p; s.l.color = col; s.l.range = D; s.i = I * Random.Range(.95f, 1.15f) * .12f; s.l.intensity = s.i; s.life = T; s.t = 0; s.on = true;
            Shooter(p, out var fwd, out var right, out var port);
            FBShotFx.Smoke(p, fwd, silenced ? .15f : .3f, cls, silenced);
            FBShotFx.Shell(port, fwd, right, cls);
        }
        // quem atirou (sentido da fumaça e janela de ejeção): personagem com o cano neste ponto; em 1ª pessoa, a câmera
        static void Shooter(Vector3 p, out Vector3 fwd, out Vector3 right, out Vector3 port)
        {
            var pl = FBPlayer.I;
            if (pl && pl.firstPerson && pl.cam && (pl.cam.transform.position - p).sqrMagnitude < 2.5f)
            { var t = pl.cam.transform; fwd = t.forward; right = t.right; port = t.position + t.forward * .32f + t.right * .1f - t.up * .1f; return; }
            FBCharacter best = null; float bd = .16f;
            foreach (var c in FBMatch.All) { if (!c) continue; float d = (c.MuzzlePos - p).sqrMagnitude; if (d < bd) { bd = d; best = c; } }
            if (best) { fwd = best.transform.forward; right = best.transform.right; port = p - fwd * .38f + Vector3.up * .03f; return; }
            fwd = Vector3.forward; right = Vector3.right; port = p;
        }

        public static void Tracer(Vector3 a, Vector3 b, bool heavy = false) => Tracer(a, b, new Color(1, .91f, .63f), heavy);
        public static void Tracer(Vector3 a, Vector3 b, Color col, bool heavy = false)
        {
            Warm(); float len = Vector3.Distance(a, b); if (len < .5f) return;
            var t = tracers.Find(x => !x.on) ?? tracers[0];
            t.a = a; t.dir = (b - a) / len; t.len = len; t.streak = Mathf.Min(len, heavy ? 6.5f : 4); t.life = Mathf.Clamp(len / (heavy ? 280 : 360), .06f, .28f); t.t = 0; t.on = true;
            t.lr.enabled = true; t.lr.startWidth = heavy ? .038f : .024f; t.lr.endWidth = .006f; t.lr.startColor = t.lr.endColor = col;
        }

        // FBCombat.Impact chama BulletHole → Puff → Sparks no mesmo ponto: o impacto por material (FBImpactFx) já fez poeira/faísca,
        // então os dois genéricos logo depois, no mesmo quadro e lugar, são ignorados (até o coordenador trocar por FBImpactFx.Bullet).
        static Vector3 lastHole; static int lastHoleFrame = -1;
        static bool AfterHole(Vector3 p) => lastHoleFrame == Time.frameCount && (p - lastHole).sqrMagnitude < .04f;
        public static void Puff(Vector3 p, Color c, float size = .35f, float life = .5f, float rise = .6f) { Warm(); if (AfterHole(p)) return; Emit(psPuff, p, new Vector3(Random.Range(-.3f, .3f), rise, Random.Range(-.3f, .3f)), size, life, new Color(c.r, c.g, c.b, .8f)); }
        public static void Sparks(Vector3 p, Vector3 n) { Warm(); if (AfterHole(p)) return; Emit(psSpark, p + n * .03f, Vector3.zero, .12f, .06f, Color.white); }
        public static void Splash(Vector3 p, float k = 1) { Warm(); FBImpactFx.Water(p, k); }
        public static void BulletHole(Vector3 p, Vector3 n, Collider on = null) { Warm(); FBImpactFx.Surface(p, n, on); lastHole = p; lastHoleFrame = Time.frameCount; }
        public static Transform BloodSplat(Vector3 p, Vector3 n, float size = .6f)
        {
            if (!BloodOn) return null; Warm(); return FBDecalFx.Place(FBDecalFx.Atlas.Blood, FBDecalFx.CellRound + Random.Range(0, 6), p, n, size * Random.Range(.75f, 1.25f));
        }
        public static void BloodSpray(Vector3 p, Vector3 dir, float amount = 1) { if (!BloodOn) return; Warm(); FBBloodFx.Spray(p, dir, amount); }
        public static void BloodPool(Vector3 p) { if (!BloodOn) return; Warm(); FBBloodFx.Pool(p); }
        public static void Explosion(Vector3 p, float r)
        {
            Warm(); FBExplosionFx.Explode(p, r);
            var l = boom[0].t < boom[1].t ? boom[0] : boom[1]; l.l.transform.position = p; l.l.range = r * 6; l.t = .3f; l.on = true; l.l.intensity = 6;
        }
        public static void SmokeGrenade(Vector3 p, float seconds = 14) { Warm(); FBExplosionFx.SmokeGrenade(p, seconds); }
        public static void Impact(Vector3 p, Vector3 n) { Warm(); FBImpactFx.Surface(p, n, null); }
        public static void Blood(Vector3 p, Vector3 n) => BloodSpray(p, -n, 1);

        public static void Tick(float dt)
        {
            if (!ready) return;
            foreach (var s in flash) if (s.on) { s.t += dt; float k = s.t / s.life; if (k >= 1) { s.l.intensity = 0; s.on = false; } else s.l.intensity = s.i * (1 - k) * (1 - k); }
            foreach (var b in boom) if (b.on) { b.t -= dt; b.l.intensity = b.t > 0 ? 6 * b.t / .3f : 0; if (b.t <= 0) b.on = false; }
            foreach (var t in tracers) if (t.on)
                {
                    t.t += dt; float k = t.t / t.life; if (k >= 1) { t.on = false; t.lr.enabled = false; continue; }
                    float head = t.len * Mathf.Min(1, k * 1.5f), tail = Mathf.Max(0, head - t.streak); float al = 1 - Mathf.Max(0, (k - .5f) * 2);
                    t.lr.SetPosition(0, t.a + t.dir * head); t.lr.SetPosition(1, t.a + t.dir * tail); var c = t.lr.startColor; c.a = .95f * al; t.lr.startColor = c; t.lr.endColor = new Color(c.r, c.g, c.b, 0);
                }
        }
        public static void Clear() { FBDecalFx.Clear(); FBWoundFx.Clear(); }
    }
    public class FBFxTicker : MonoBehaviour
    {
        // [parceiro 01/11] medidor de tempo por sistema (FBTempo): com FBTempo.On soma o custo dos efeitos (Update) e das feridas (LateUpdate)
        static readonly int __fbt_Update = FBTempo.Id("FBFxTicker.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt() { float dt = Time.deltaTime; FBFx.Tick(dt); FBDecalFx.Tick(dt); FBExplosionFx.Tick(dt); }
        static readonly int __fbt_LateUpdate = FBTempo.Id("FBFxTicker.LateUpdate");
        void LateUpdate()
        {
            if (!FBTempo.On) { LateUpdate__fbt(); return; }
            long t0 = FBTempo.T0(); LateUpdate__fbt(); FBTempo.Soma(__fbt_LateUpdate, t0);
        }
        void LateUpdate__fbt() => FBWoundFx.Tick(Time.deltaTime);
    }
}
