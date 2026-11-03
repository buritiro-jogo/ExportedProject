// PARTÍCULAS AMBIENTES do cenário (Agente 5; não são FX de combate — esses são do FBFx/Agente 6). Criadas no início da partida pelo
// FBWeather (nada é criado/compilado no meio da partida: regra anti-travada) e limitadas pelo nível gráfico (FBGfx.F):
//  · insetos voando em volta dos postes acesos (pool fixo que vai para os postes mais perto da câmera; noite sem chuva);
//  · poeira/pólen brilhando no ar em volta da câmera (dia sem chuva, bem sutil);
//  · fumaça de churrasqueira (props Churrasqueira/grill/carvão do mapa) e, no PC, uma QUEIMADA ao longe no horizonte — coluna de fumaça
//    típica da seca em Roraima (dia sem chuva).
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public class FBAmbientFx : MonoBehaviour
    {
        static FBAmbientFx I;
        readonly List<ParticleSystem> bugs = new(); ParticleSystem dust; readonly List<ParticleSystem> smokes = new(); ParticleSystem farSmoke;
        float reassignT;
        static Texture2D dotTex, puffTex;

        public static void Start(Transform parent, Bounds? map)
        {
            if (I) Destroy(I.gameObject);
            var go = new GameObject("FB_AmbienteFX"); go.transform.SetParent(parent, false); I = go.AddComponent<FBAmbientFx>(); I.Build(map);
        }

        static Texture2D Dot()
        {
            if (dotTex) return dotTex; const int S = 32; dotTex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) { float d = new Vector2(x - S / 2 + .5f, y - S / 2 + .5f).magnitude / (S / 2); float a = Mathf.Exp(-d * d * 5); dotTex.SetPixel(x, y, new Color(1, 1, 1, a)); }
            dotTex.Apply(); return dotTex;
        }
        static Texture2D Puff()
        {
            if (puffTex) return puffTex; const int S = 64; puffTex = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
                {
                    float d = new Vector2(x - S / 2 + .5f, y - S / 2 + .5f).magnitude / (S / 2);
                    float n = Mathf.PerlinNoise(x * .11f, y * .11f) * .6f + Mathf.PerlinNoise(x * .27f + 7, y * .27f + 3) * .4f;
                    float a = Mathf.Clamp01(1 - d) * Mathf.Clamp01(.35f + n); a *= a;
                    float s = Mathf.Lerp(.82f, 1f, n); puffTex.SetPixel(x, y, new Color(s, s, s, a));
                }
            puffTex.Apply(true); return puffTex;
        }
        static Material Mat(Texture2D t, bool additive) { var m = FBShaders.Transparent(new Material(FBShaders.ParticlesUnlit), additive); m.SetTexture("_BaseMap", t); m.mainTexture = t; return m; }

        static ParticleSystem MakePS(string name, Transform parent, Material mat)
        {
            var g = new GameObject(name); g.transform.SetParent(parent, false); var ps = g.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.loop = true; m.playOnAwake = false; m.simulationSpace = ParticleSystemSimulationSpace.World; m.scalingMode = ParticleSystemScalingMode.Shape;
            var r = g.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return ps;
        }

        void Build(Bounds? map)
        {
            var F = FBGfx.F; var addMat = Mat(Dot(), true); var smokeMat = Mat(Puff(), false);
            // insetos nos postes
            for (int i = 0; i < F.lampInsects; i++)
            {
                var ps = MakePS("insetos" + i, transform, addMat); var m = ps.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.6f); m.startSpeed = new ParticleSystem.MinMaxCurve(.15f, .55f); m.startSize = new ParticleSystem.MinMaxCurve(.025f, .055f);
                m.startColor = new Color(1, .93f, .74f, .85f); m.maxParticles = 24;
                var e = ps.emission; e.rateOverTime = 8; var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = .75f;
                var nz = ps.noise; nz.enabled = true; nz.strength = .9f; nz.frequency = 1.3f; nz.scrollSpeed = .6f; nz.quality = ParticleSystemNoiseQuality.Low;
                var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(new Color(1, .93f, .74f));
                ps.gameObject.SetActive(false); bugs.Add(ps);
            }
            // poeira no sol em volta da câmera
            if (F.dustMotes > 0)
            {
                dust = MakePS("poeira", transform, addMat); var m = dust.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(5, 8); m.startSpeed = new ParticleSystem.MinMaxCurve(.01f, .06f); m.startSize = new ParticleSystem.MinMaxCurve(.012f, .03f);
                m.startColor = new Color(1, .95f, .82f, .32f); m.maxParticles = F.dustMotes;
                var e = dust.emission; e.rateOverTime = F.dustMotes / 6f; var sh = dust.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(16, 6, 16);
                var nz = dust.noise; nz.enabled = true; nz.strength = .12f; nz.frequency = .4f; nz.quality = ParticleSystemNoiseQuality.Low;
                var col = dust.colorOverLifetime; col.enabled = true; col.color = Fade(new Color(1, .95f, .82f));
            }
            // fumaça de churrasqueira (props do mapa)
            if (F.smokePlumes > 0)
            {
                var grills = new List<Vector3>();
                foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                {
                    if (r.gameObject.layer != FBWorld.LMap) continue; var mn = r.sharedMaterial ? r.sharedMaterial.name : "";
                    if (!System.Text.RegularExpressions.Regex.IsMatch(mn + " " + r.name, "churrasq|grill|carvao", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) continue;
                    var b = r.bounds; if (b.size.magnitude > 6) continue; var top = new Vector3(b.center.x, b.max.y + .1f, b.center.z);
                    if (!grills.Any(p => (p - top).sqrMagnitude < 16)) grills.Add(top);
                }
                var c0 = map?.center ?? Vector3.zero;
                foreach (var p in grills.OrderBy(p => (p - c0).sqrMagnitude).Take(Mathf.Max(1, F.smokePlumes - 1)))
                {
                    var ps = MakePS("fumaca_churrasco", transform, smokeMat); ps.transform.position = p; var m = ps.main;
                    m.startLifetime = new ParticleSystem.MinMaxCurve(4, 6.5f); m.startSpeed = new ParticleSystem.MinMaxCurve(.5f, 1f); m.startSize = new ParticleSystem.MinMaxCurve(.35f, .7f);
                    m.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2); m.startColor = new Color(.78f, .77f, .75f, .32f); m.maxParticles = 40;
                    var e = ps.emission; e.rateOverTime = 5; var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12; sh.radius = .2f; sh.rotation = new Vector3(-90, 0, 0);
                    var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .6f, 1, 3.2f));
                    var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-.4f, .4f);
                    var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(new Color(.78f, .77f, .75f));
                    smokes.Add(ps);
                }
                // queimada no horizonte (PC)
                if (F.smokePlumes >= 3 && map is Bounds mb)
                {
                    float a = Random.Range(0, Mathf.PI * 2); var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    farSmoke = MakePS("queimada_longe", transform, smokeMat); farSmoke.transform.position = new Vector3(mb.center.x, mb.min.y, mb.center.z) + dir * (mb.extents.magnitude + 260);
                    var m = farSmoke.main; m.startLifetime = new ParticleSystem.MinMaxCurve(38, 52); m.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4f); m.startSize = new ParticleSystem.MinMaxCurve(18, 30);
                    m.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2); m.startColor = new Color(.64f, .62f, .6f, .38f); m.maxParticles = 60;
                    var e = farSmoke.emission; e.rateOverTime = .9f; var sh = farSmoke.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 8; sh.radius = 12; sh.rotation = new Vector3(-90, 0, 0);
                    var sz = farSmoke.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .7f, 1, 3.5f));
                    var fo = farSmoke.forceOverLifetime; fo.enabled = true; fo.space = ParticleSystemSimulationSpace.World; fo.x = dir.z * .4f; fo.z = -dir.x * .4f;   // fumaça deitando com o vento lá em cima
                    var col = farSmoke.colorOverLifetime; col.enabled = true; col.color = Fade(new Color(.64f, .62f, .6f));
                    farSmoke.Simulate(40, true, true); farSmoke.Play();                                                         // já aparece formada
                }
            }
        }
        static ParticleSystem.MinMaxGradient Fade(Color c)
        {
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(c, 0), new GradientColorKey(c, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .2f), new GradientAlphaKey(.8f, .7f), new GradientAlphaKey(0, 1) });
            return new ParticleSystem.MinMaxGradient(g);
        }
        static void Run(ParticleSystem ps, bool on) { if (!ps) return; if (on && !ps.isPlaying) ps.Play(); else if (!on && ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting); }

        // [parceiro] v1 (thread de física): tempo do Update medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
        static readonly int __fbt_Update = FBTempo.Id("FBAmbientFx.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            var cam = Camera.main; if (!cam) return; var cp = cam.transform.position; float n = FBWeather.NightK; bool rain = FBWeather.Rain;
            if (dust) { dust.transform.position = cp + Vector3.up * 1.2f; Run(dust, n < .3f && !rain); }
            foreach (var s in smokes) Run(s, !rain);
            if (farSmoke) Run(farSmoke, !rain && n < .55f);
            if ((reassignT -= Time.deltaTime) > 0) return; reassignT = 1;
            var lamps = FBWeather.Lamps; bool bugsOn = n > .5f && !rain && lamps != null && lamps.Count > 0;
            var near = bugsOn ? lamps.OrderBy(p => (p - cp).sqrMagnitude).Take(bugs.Count).ToList() : null;
            for (int i = 0; i < bugs.Count; i++)
            {
                bool on = bugsOn && i < near.Count && (near[i] - cp).sqrMagnitude < 60 * 60;
                if (on && (bugs[i].transform.position - (near[i] + Vector3.down * .6f)).sqrMagnitude > .01f) { bugs[i].Clear(); bugs[i].transform.position = near[i] + Vector3.down * .6f; }
                if (bugs[i].gameObject.activeSelf != on) bugs[i].gameObject.SetActive(on);
                Run(bugs[i], on);
            }
        }
    }
}
