// Explosões (granada, carro, caveirão, helicóptero): clarão + bola de fogo curta, fumaça escura subindo, onda de poeira rente ao chão,
// detritos, brasas e marca de queimado no chão. Fumaça de granada (cortina): emissores fixos (3) que soltam fumaça por alguns segundos.
// Tudo em pools fixos (FBFxLib.PS); a luz da explosão continua no FBFx (2 luzes pré-criadas).
using UnityEngine;

namespace FreeBala
{
    public static class FBExplosionFx
    {
        static ParticleSystem psFire, psSmoke, psWave, psEmber, psFlash, psCurtain; static bool ready;
        class Curtain { public Vector3 p; public float left, acc, radius; public Color col; }
        static readonly Curtain[] curtains = { new(), new(), new() };

        public static void Warm()
        {
            if (ready) return; ready = true; FBFxQuality.Ensure(); bool hi = FBFxQuality.PoolDecals > 100;
            var fire = FBFxLib.Mat("Particles_Add", FBFxLib.Tex("fire"), Color.white, "ExplosaoFogo");
            var fg = new Gradient(); fg.SetKeys(new[] { new GradientColorKey(new Color(1, .95f, .8f), 0), new GradientColorKey(new Color(1, .55f, .18f), .35f), new GradientColorKey(new Color(.5f, .12f, .03f), 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.8f, .4f), new GradientAlphaKey(0, 1) });
            psFire = FBFxLib.PS("ExplosaoFogo", fire, hi ? 90 : 40, new FBFxLib.Opt { gravity = -.15f, drag = .5f, tilesX = 2, tilesY = 2, spin = true, size = FBFxLib.Curve(0, .5f, .25f, 1, 1, 1.35f), color = fg });
            var smoke = FBFxLib.Mat("Particles_Alpha", FBFxLib.Tex("smoke"), Color.white, "ExplosaoFumaca");
            psSmoke = FBFxLib.PS("ExplosaoFumaca", smoke, hi ? 160 : 60, new FBFxLib.Opt { gravity = -.05f, drag = .4f, tilesX = 2, tilesY = 2, spin = true, size = FBFxLib.Curve(0, .5f, .4f, 1.4f, 1, 2.4f), color = FBFxLib.Fade(.05f, .55f), sortFudge = 10 });
            psWave = FBFxLib.PS("ExplosaoPoeira", smoke, hi ? 90 : 36, new FBFxLib.Opt { drag = .9f, tilesX = 2, tilesY = 2, spin = true, size = FBFxLib.Curve(0, .6f, 1, 1.8f), color = FBFxLib.Fade(.04f, .4f), sortFudge = 5 });
            psEmber = FBFxLib.PS("ExplosaoBrasas", FBFxLib.Mat("Particles_Add", FBFxLib.Tex("spark"), Color.white, "ExplosaoBrasa"), hi ? 120 : 40,
                new FBFxLib.Opt { gravity = .35f, mode = ParticleSystemRenderMode.Stretch, stretch = .02f, color = FBFxLib.Fade(0, .6f, new Color(1, .3f, .1f)) });
            psFlash = FBFxLib.PS("ExplosaoClarao", fire, 8, new FBFxLib.Opt { tilesX = 2, tilesY = 2, color = FBFxLib.Fade(0, .15f) });
            psCurtain = FBFxLib.PS("FumacaGranada", smoke, hi ? 220 : 80, new FBFxLib.Opt { gravity = -.01f, drag = .6f, tilesX = 2, tilesY = 2, spin = true, size = FBFxLib.Curve(0, .35f, .3f, 1, 1, 1.5f), color = FBFxLib.Fade(.12f, .7f), sortFudge = 20 });
        }

        /// <summary>Explosão em p com raio r (m). Escala tudo pelo raio; LOD reduz partículas de longe.</summary>
        public static void Explode(Vector3 p, float r)
        {
            Warm(); float lod = Mathf.Max(FBFxQuality.Lod(p), .35f);          // explosão é evento grande: nunca some por inteiro
            FBFxLib.Emit(psFlash, p, Vector3.zero, r * 1.6f, .18f, Color.white, Random.Range(0, 360f));
            int n = FBFxQuality.Count(14, lod);
            for (int i = 0; i < n; i++) FBFxLib.Emit(psFire, p + Random.insideUnitSphere * r * .2f, Random.insideUnitSphere * r * 1.6f + Vector3.up * r * .6f, r * Random.Range(.35f, .65f), Random.Range(.3f, .65f), Color.white, Random.Range(0, 360f));
            n = FBFxQuality.Count(14, lod);
            for (int i = 0; i < n; i++)
                FBFxLib.Emit(psSmoke, p + Random.insideUnitSphere * r * .3f, Vector3.up * Random.Range(1, 3.2f) + Random.insideUnitSphere * 1.6f, r * Random.Range(.45f, .9f), Random.Range(2.6f, 5f),
                    FBFxLib.Jit(new Color(.17f, .16f, .15f, .78f), .2f), Random.Range(0, 360f));
            float gy = FBWorld.GroundY(p.x, p.y, p.z, 1, 4) ?? p.y - .5f;
            n = FBFxQuality.Count(22, lod);
            for (int i = 0; i < n; i++)                                       // onda de poeira rente ao chão (abre rápido e para)
            {
                float a = i * Mathf.PI * 2 / Mathf.Max(1, n) + Random.Range(-.15f, .15f); var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                FBFxLib.Emit(psWave, new Vector3(p.x, gy + .25f, p.z) + d * .3f, d * Random.Range(7, 12) * Mathf.Sqrt(r / 4) + Vector3.up * .3f, r * Random.Range(.3f, .5f), Random.Range(1.1f, 2f), new Color(.55f, .5f, .42f, .55f), Random.Range(0, 360f));
            }
            n = FBFxQuality.Count(18, lod);
            for (int i = 0; i < n; i++) FBFxLib.Emit(psEmber, p, (Random.onUnitSphere + Vector3.up * .8f) * Random.Range(4, 11), Random.Range(.015f, .03f), Random.Range(.6f, 1.4f), new Color(1, Random.Range(.5f, .8f), .25f));
            FBImpactFx.Warm(); FBImpactFx.Surface(new Vector3(p.x, gy, p.z), Vector3.up, null);                  // lascas/poeira do chão
            if (Mathf.Abs(p.y - gy) < r) FBDecalFx.Place(FBDecalFx.Atlas.Holes, FBDecalFx.Scorch, new Vector3(p.x, gy, p.z), Vector3.up, r * Random.Range(.8f, 1.05f), null, 1, null, FBFxQuality.DecalLife * 1.5f, false);
        }

        /// <summary>Granada de fumaça: cortina por 'seconds' segundos (até 3 ao mesmo tempo; a mais antiga é substituída).</summary>
        public static void SmokeGrenade(Vector3 p, float seconds = 14, float radius = 3.5f, Color? color = null)
        {
            Warm(); Curtain c = curtains[0]; foreach (var x in curtains) if (x.left < c.left) c = x;
            c.p = p; c.left = seconds; c.acc = 0; c.radius = radius; c.col = color ?? new Color(.78f, .78f, .76f, .85f);
        }
        public static void Tick(float dt)
        {
            if (!ready) return;
            foreach (var c in curtains)
            {
                if (c.left <= 0) continue; c.left -= dt; c.acc += dt * (FBFxQuality.Level == 0 ? 3 : 6);
                while (c.acc >= 1)
                {
                    c.acc -= 1; if (FBFxQuality.Take(1) == 0) break; var o = Random.insideUnitCircle * c.radius * .5f;
                    FBFxLib.Emit(psCurtain, c.p + new Vector3(o.x, .4f, o.y), new Vector3(o.x, 0, o.y) * .35f + Vector3.up * Random.Range(.15f, .5f), c.radius * Random.Range(.7f, 1.1f), Random.Range(6, 9), FBFxLib.Jit(c.col, .06f), Random.Range(0, 360f));
                }
            }
        }
    }
}
