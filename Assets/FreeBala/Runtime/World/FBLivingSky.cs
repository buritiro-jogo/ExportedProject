// CÉU VIVO (porte do LivingSky do sky.js): skybox FreeBala/Sky (atmosfera pela elevação do sol, nuvens em camadas, noite com estrelas/
// Via Láctea/lua, chuva) + nuvens-sprite baixas que derivam em volta da câmera (só nos níveis sem nuvem volumétrica e sem céu leve) +
// 5 beija-flores voando em elipse sobre o mapa (o pássaro do pacote 'birds', clipe de voo do próprio GLB). Pássaros recolhidos à noite/chuva.
// Nível gráfico (FBGfx): _FB_SKY_LITE no mobile/web, _FB_VOLCLOUDS no PC alto (ruído 3D tileável gerado aqui, 32³, uma vez por sessão).
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace FreeBala
{
    public class FBLivingSky : MonoBehaviour
    {
        public static FBLivingSky I; Material sky; float time; readonly List<(Transform t, Material m, float vx, float vz, float op0)> clouds = new();
        class Bird { public Transform root; public PlayableGraph graph; public float phase, rx, rz, yOff, speed, bob; }
        readonly List<Bird> birds = new(); (float x, float z, float rx, float rz, float y) flight = (0, 0, 70, 58, 40); bool hideBirds;
        public static void Start(Bounds? map)
        {
            if (I) Destroy(I.gameObject); var go = new GameObject("FB_CeuVivo"); I = go.AddComponent<FBLivingSky>(); I.Build(map);
        }
        // nuvem-sprite: aglomerado de bolhas com borda recortada por fbm e base mais escura (sombra própria) — antes eram elipses lisas
        static Texture2D cloudTex; static Texture2D CloudTex()
        {
            if (cloudTex) return cloudTex; int S = 256; var t = new Texture2D(S, S, TextureFormat.RGBA32, true); var px = new Color[S * S]; var R = new System.Random(7);
            var blobs = new List<(float x, float y, float rx, float ry)>();
            for (int i = 0; i < 15; i++) blobs.Add((48 + (float)R.NextDouble() * 160, 70 + (float)R.NextDouble() * 110, 22 + (float)R.NextDouble() * 38, 16 + (float)R.NextDouble() * 26));
            for (int yy = 0; yy < S; yy++) for (int xx = 0; xx < S; xx++)
                {
                    float d = 0; foreach (var b in blobs) { float dx = (xx - b.x) / b.rx, dy = (yy - b.y) / b.ry; d = Mathf.Max(d, 1 - Mathf.Sqrt(dx * dx + dy * dy)); }
                    float n = Fbm(xx / 38f, yy / 38f) - .5f; float a = Mathf.Clamp01((d + n * .55f) * 2.2f); a = a * a * (3 - 2 * a);
                    float shade = Mathf.Lerp(.66f, 1f, Mathf.Clamp01((yy - 60) / 120f + n * .4f));        // V cresce para cima: base mais escura
                    px[yy * S + xx] = new Color(shade, shade, Mathf.Min(1, shade * 1.03f), a * .9f);
                }
            t.SetPixels(px); t.Apply(true); t.wrapMode = TextureWrapMode.Clamp; return cloudTex = t;
        }
        static float VN(float x, float y) { int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y); float fx = x - ix, fy = y - iy; fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy); return Mathf.Lerp(Mathf.Lerp(H(ix, iy), H(ix + 1, iy), fx), Mathf.Lerp(H(ix, iy + 1), H(ix + 1, iy + 1), fx), fy); }
        static float H(int x, int y) { uint h = (uint)(x * 374761393 + y * 668265263); h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xffffff) / 16777215f; }
        static float Fbm(float x, float y) { float s = 0, a = .5f; for (int i = 0; i < 4; i++) { s += a * VN(x, y); x *= 2.03f; y *= 2.03f; a *= .5f; } return s / .9375f; }

        // ruído 3D TILEÁVEL das nuvens volumétricas (R8, 32³): fbm de valor periódico + Worley invertido (bolhas)
        static Texture3D volTex; static Texture3D VolTex()
        {
            if (volTex) return volTex; const int N = 32, C = 4; var v = new byte[N * N * N]; var rnd = new System.Random(11);
            var pts = new Vector3[C * C * C]; for (int i = 0; i < pts.Length; i++) pts[i] = new Vector3((float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble());
            static int W(int a, int per) => ((a % per) + per) % per;
            static float P(int x, int y, int z, int per) { uint h = (uint)(W(x, per) * 73856093 ^ W(y, per) * 19349663 ^ W(z, per) * 83492791 ^ per * 40503); h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xffff) / 65535f; }
            static float VN3(float x, float y, float z, int per)
            {
                int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y), iz = Mathf.FloorToInt(z); float fx = x - ix, fy = y - iy, fz = z - iz; fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy); fz = fz * fz * (3 - 2 * fz);
                float L(int a, int b, int c) => P(ix + a, iy + b, iz + c, per);
                return Mathf.Lerp(Mathf.Lerp(Mathf.Lerp(L(0, 0, 0), L(1, 0, 0), fx), Mathf.Lerp(L(0, 1, 0), L(1, 1, 0), fx), fy), Mathf.Lerp(Mathf.Lerp(L(0, 0, 1), L(1, 0, 1), fx), Mathf.Lerp(L(0, 1, 1), L(1, 1, 1), fx), fy), fz);
            }
            for (int z = 0; z < N; z++) for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                    {
                        float fb = 0, a = .5f; for (int o = 0; o < 3; o++) { int per = 4 << o; float k = per / (float)N; fb += a * VN3(x * k, y * k, z * k, per); a *= .5f; }
                        fb /= .875f;
                        var p = new Vector3(x, y, z) * (C / (float)N); float wd = 9;
                        for (int dz = -1; dz <= 1; dz++) for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                                {
                                    int cx = Mathf.FloorToInt(p.x) + dx, cy = Mathf.FloorToInt(p.y) + dy, cz = Mathf.FloorToInt(p.z) + dz;
                                    var f = pts[W(cx, C) + W(cy, C) * C + W(cz, C) * C * C]; wd = Mathf.Min(wd, (new Vector3(cx, cy, cz) + f - p).sqrMagnitude);
                                }
                        float w = 1 - Mathf.Clamp01(Mathf.Sqrt(wd) * 1.15f);
                        v[x + y * N + z * N * N] = (byte)(Mathf.Clamp01(fb * .55f + w * .55f) * 255);
                    }
            volTex = new Texture3D(N, N, N, TextureFormat.R8, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "fb_nuvem3d" };
            volTex.SetPixelData(v, 0); volTex.Apply(false, true); return volTex;
        }
        void ApplyTier()
        {
            var f = FBGfx.F;
            if (sky)
            {
                sky.DisableKeyword("_FB_SKY_LITE"); sky.DisableKeyword("_FB_VOLCLOUDS");
                if (f.volClouds && SystemInfo.supports3DTextures) { sky.SetTexture("_FBCloudVol", VolTex()); sky.EnableKeyword("_FB_VOLCLOUDS"); }
                else if (f.skyLite) sky.EnableKeyword("_FB_SKY_LITE");
            }
            foreach (var c in clouds) if (c.t) c.t.gameObject.SetActive(f.cloudSprites);
        }
        void OnEnable() => FBGfx.Changed += ApplyTier;
        void OnDisable() => FBGfx.Changed -= ApplyTier;
        void Build(Bounds? map)
        {
            var sh = FBShaders.Sky; if (sh) { sky = new Material(sh) { name = "ceu_freebala" }; RenderSettings.skybox = sky; DynamicGI.UpdateEnvironment(); }
            var cm = FBShaders.Transparent(new Material(FBShaders.ParticlesUnlit)); cm.SetTexture("_BaseMap", CloudTex()); cm.mainTexture = CloudTex(); cm.SetFloat("_Cull", 0);
            for (int i = 0; i < 12; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>()); q.name = "nuvem"; q.transform.SetParent(transform, false); float sc = 20 + Random.value * 34;
                q.transform.localScale = new Vector3(sc * (1.2f + Random.value), sc * (.52f + Random.value * .3f), 1); q.transform.localPosition = new Vector3((Random.value - .5f) * 310, 38 + Random.value * 25, (Random.value - .5f) * 310);
                var m = new Material(cm); float op = .48f + Random.value * .24f; m.SetColor("_BaseColor", new Color(1, 1, 1, op)); var mr = q.GetComponent<Renderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                clouds.Add((q.transform, m, .18f + Random.value * .45f, (Random.value - .5f) * .08f, op));
            }
            ApplyTier();
            if (map is Bounds b && b.size.x > 0) flight = (b.center.x, b.center.z, Mathf.Max(34, b.size.x * .34f), Mathf.Max(30, b.size.z * .34f), Mathf.Max(28, b.center.y + b.size.y * .5f + 14));
            var pf = Resources.Load<GameObject>("FBImported/Birds/hummingbird");
            if (pf)
            {
                var rends = pf.GetComponentsInChildren<Renderer>(true); var bb = rends.Length > 0 ? rends[0].bounds : new Bounds(Vector3.zero, Vector3.one); foreach (var r in rends) bb.Encapsulate(r.bounds);
                float span = Mathf.Max(bb.size.x, bb.size.y, bb.size.z, .001f), scale = .42f / span * FBVehicles.SCALE;
#if UNITY_EDITOR
                var clips = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(UnityEditor.AssetDatabase.GetAssetPath(pf)).OfType<AnimationClip>().ToList();
#else
                var clips = new List<AnimationClip>();
#endif
                var clip = clips.FirstOrDefault(c => System.Text.RegularExpressions.Regex.IsMatch(c.name, "02_Forward_Flight_Loop|01_Hover_Idle_Loop")) ?? clips.FirstOrDefault() ?? FBRegistry.I?.birdClip;
                for (int i = 0; i < 5; i++)
                {
                    var root = new GameObject("beija_flor").transform; root.SetParent(transform, false); var bd = Instantiate(pf, root, false); root.localScale = Vector3.one * scale;
                    foreach (var r in bd.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true; }
                    var B = new Bird { root = root, phase = i * (Mathf.PI * 2 / 5) + (Random.value - .5f) * .42f, rx = .55f + Random.value * .45f, rz = .55f + Random.value * .45f, yOff = i * 1.8f, speed = .035f + Random.value * .012f, bob = Random.value * Mathf.PI * 2 };
                    if (clip) { var an = bd.GetOrAdd<Animator>(); B.graph = PlayableGraph.Create("passaro"); var pl = AnimationClipPlayable.Create(B.graph, clip); var outp = AnimationPlayableOutput.Create(B.graph, "a", an); outp.SetSourcePlayable(pl); B.graph.Play(); }
                    birds.Add(B);
                }
            }
        }
        void OnDestroy() { foreach (var b in birds) if (b.graph.IsValid()) b.graph.Destroy(); }
        // clima (FBWeather): night/dusk 0..1, chuva, direções do sol e da lua (Unity, apontando PARA o astro; o sol pode estar abaixo do
        // horizonte), cobertura de nuvens 0..1 e vento (XZ) -> uniforms no espaço do web (X espelhado)
        public void SetWeather(float night, bool rain, float dusk, Vector3 sunDirUnity, Vector3 moonDirUnity, float cover, Vector2 wind)
        {
            if (sky)
            {
                sky.SetFloat("_Night", night); sky.SetFloat("_Rain", rain ? 1 : 0); sky.SetFloat("_Dusk", dusk); sky.SetFloat("_Cover", rain ? 1 : cover);
                static Vector3 W(Vector3 v) => new Vector3(-v.x, v.y, v.z).normalized;
                sky.SetVector("_SunDir", W(sunDirUnity)); sky.SetVector("_MoonDir", W(moonDirUnity)); sky.SetVector("_WindSky", new Vector4(-wind.x, wind.y, 0, 0));
            }
            Color day = Color.white, dk = new(1, .69f, .54f), nt = new(.114f, .145f, .2f), rn = new(.486f, .518f, .545f);
            foreach (var c in clouds) { var col = rain ? rn : Color.Lerp(Color.Lerp(day, dk, dusk * (1 - night)), nt, night); col.a = rain ? Mathf.Min(1, c.op0 + .3f) : c.op0 * (1 - .4f * night); c.m.SetColor("_BaseColor", col); }
            hideBirds = night > .5f || rain; foreach (var b in birds) b.root.gameObject.SetActive(!hideBirds);
        }
        // r64: céu FÍSICO (Rayleigh/Mie no FBSky.shader) guiado pelo BuritiroEnvironmentManager: exposição do céu (adaptação do olho), tinta
        // do sol que chega às nuvens (dourada/rosada no pôr do sol), fase da lua com lado (crescente/minguante) e poluição luminosa.
        public void SetPhysical(float skyExposure, Color sunTint, float moonPhase, float lightPollution)
        {
            if (sky)
            {
                sky.SetFloat("_FBPhys", 1); sky.SetFloat("_FBSkyExposure", skyExposure); sky.SetColor("_FBSunTint", sunTint);
                sky.SetFloat("_MoonPhase", Mathf.Clamp(moonPhase, -1, 1)); sky.SetFloat("_FBLightPol", lightPollution);
            }
            // nuvens-sprite (níveis sem nuvem do céu): mesma tinta do sol e mesmo brilho do céu
            float b = Mathf.Clamp(skyExposure / 6f, .08f, 1f); float m = Mathf.Max(1e-3f, sunTint.maxColorComponent); var tn = sunTint / m;
            foreach (var c in clouds) { var col = Color.Lerp(Color.white, tn, .55f) * b; col.a = c.m.GetColor("_BaseColor").a; c.m.SetColor("_BaseColor", col); }
        }
        // [parceiro] v1 (thread de física): tempo do LateUpdate medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
        static readonly int __fbt_LateUpdate = FBTempo.Id("FBLivingSky.LateUpdate");
        void LateUpdate()
        {
            if (!FBTempo.On) { LateUpdate__fbt(); return; }
            long t0 = FBTempo.T0(); LateUpdate__fbt(); FBTempo.Soma(__fbt_LateUpdate, t0);
        }
        void LateUpdate__fbt()
        {
            var cam = UnityEngine.Camera.main; if (!cam) return; float dt = Time.deltaTime; time += dt; if (sky) sky.SetFloat("_SkyTime", time);
            transform.position = cam.transform.position;
            foreach (var (t, _, vx, vz, _) in clouds)
            {
                var p = t.localPosition; p.x += vx * dt; p.z += vz * dt; if (p.x > 190) p.x = -190; if (p.z > 190) p.z = -190; else if (p.z < -190) p.z = 190; t.localPosition = p;
                t.rotation = Quaternion.LookRotation(t.position - cam.transform.position);
            }
            foreach (var b in birds)
            {
                b.phase += b.speed * dt; float x = flight.x + Mathf.Cos(b.phase) * flight.rx * b.rx, z = flight.z + Mathf.Sin(b.phase) * flight.rz * b.rz;
                b.root.position = new Vector3(x, flight.y + b.yOff + Mathf.Sin(b.phase * 2 + b.bob) * .8f, z);
                b.root.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(b.phase), 0, Mathf.Cos(b.phase)));   // tangente da elipse
            }
        }
    }
}
