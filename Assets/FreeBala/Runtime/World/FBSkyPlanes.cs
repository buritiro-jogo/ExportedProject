// AVIÕES NO CÉU (porte do skyplanes.js): o som de avião do ambiente tem avião de verdade passando lá em cima — avião de linha a 450–750 m,
// ~140 m/s, em ~45 % das passagens com dois rastros de condensação que alargam e somem devagar. A câmera só desenha até ~1,4 km: avião e
// rastro são PROJETADOS numa casca de R m em volta da câmera (mesma direção e tamanho angular). O som acompanha o avião, baixo e atenuado.
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public class FBSkyPlanes : MonoBehaviour
    {
        const float R = 270, TRAIL_DT = .2f, TRAIL_LIFE = 75; const int MAXF = 2, TRAIL_N = 380;
        static FBSkyPlanes I; static GameObject proto; static Material trailMat;
        class Trail { public Mesh mesh; public GameObject go; public float seed; public readonly List<(Vector3 p, float t, float u)> pts = new(); public Vector3[] P = new Vector3[TRAIL_N * 2]; public Color[] C = new Color[TRAIL_N * 2]; public Vector2[] UV = new Vector2[TRAIL_N * 2]; }
        class Flight { public Vector3 pos, dir; public float speed, t, len, tt; public GameObject plane; public readonly List<Trail> trails = new(); public bool smoke, done; public AudioSource snd; }
        readonly List<Flight> flights = new();
        // Boeing 787 do Battle Royale (Acc/boeing787, o mesmo do br.js): nariz em +Z, centrado, 'len' m de ponta a ponta (57 m × 1,2 do mundo)
        public static GameObject Boeing(float len = 68)
        {
            var p = FBRegistry.I ? FBRegistry.I.Acc("boeing787") : null; if (!p) return null;
            var root = new GameObject("Boeing787"); var g = Instantiate(p, root.transform); g.transform.localPosition = Vector3.zero; g.transform.localRotation = Quaternion.identity;
            foreach (var c in g.GetComponentsInChildren<Collider>(true)) Destroy(c);
            var b = new Bounds(); bool any = false; foreach (var r in g.GetComponentsInChildren<Renderer>(true)) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            float k = any && b.size.z > .01f ? len / b.size.z : 1; g.transform.localScale *= k; g.transform.localPosition = -b.center * k;
            return root;
        }
        static GameObject MakePlane()
        {
            // pedido 26/10: o avião provisório (peças primitivas) vira o 787 do Battle Royale, com as luzes de navegação nas pontas das asas
            var bo = Boeing();
            if (bo)
            {
                bo.name = "aviao_proto"; DontDestroyOnLoad(bo);                                       // ativo até medir (Renderer.bounds de inativo = vazio)
                var rs = bo.GetComponentsInChildren<Renderer>(true); var bb = new Bounds(); bool any = false;
                foreach (var r in rs) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; if (!any) { bb = r.bounds; any = true; } else bb.Encapsulate(r.bounds); }
                foreach (var (x, c) in new[] { (bb.max.x * .98f, new Color(.13f, 1, .25f)), (bb.min.x * .98f, new Color(1, .13f, .13f)) })
                {   // verde à direita, vermelha à esquerda (+X do Unity = direita de quem está no avião olhando para +Z)
                    var o = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(o.GetComponent<Collider>()); o.transform.SetParent(bo.transform, false); o.transform.localPosition = new Vector3(x, bb.center.y, bb.center.z); o.transform.localScale = Vector3.one * 1.2f;
                    var m = new Material(FBShaders.Unlit); m.SetColor("_BaseColor", c); var mr = o.GetComponent<Renderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                bo.SetActive(false); return bo;
            }
            var g = new GameObject("aviao_proto"); g.SetActive(false); DontDestroyOnLoad(g);
            Material M(Color c, bool em = false) { var m = new Material(em ? FBShaders.Unlit : FBShaders.Lit); m.SetColor("_BaseColor", c); if (!em) m.SetFloat("_Smoothness", .35f); return m; }
            Material white = M(new Color(.91f, .93f, .945f)), grey = M(new Color(.6f, .64f, .68f)), dark = M(new Color(.22f, .25f, .29f));
            void Add(PrimitiveType t, Material m, Vector3 p, Vector3 s, Vector3 r) { var o = GameObject.CreatePrimitive(t); Destroy(o.GetComponent<Collider>()); o.transform.SetParent(g.transform, false); o.transform.localPosition = p; o.transform.localScale = s; o.transform.localEulerAngles = r; var mr = o.GetComponent<Renderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            Add(PrimitiveType.Cylinder, white, Vector3.zero, new Vector3(4.2f, 17, 4.2f), new Vector3(90, 0, 0));           // fuselagem (Z = frente)
            Add(PrimitiveType.Sphere, white, new Vector3(0, 0, 17), new Vector3(4.2f, 4.2f, 8), Vector3.zero);              // nariz
            Add(PrimitiveType.Capsule, white, new Vector3(0, .5f, -20), new Vector3(3.2f, 5, 3.2f), new Vector3(90, 0, 0));  // cauda
            Add(PrimitiveType.Cube, grey, new Vector3(0, -.6f, 1.5f), new Vector3(36, .5f, 6.5f), Vector3.zero);            // asas
            Add(PrimitiveType.Cube, grey, new Vector3(0, .8f, -21), new Vector3(13, .35f, 3.2f), Vector3.zero);             // estabilizador
            Add(PrimitiveType.Cube, white, new Vector3(0, 4, -21.5f), new Vector3(.4f, 7, 4.5f), new Vector3(-20, 0, 0));   // deriva
            foreach (var x in new[] { -7.5f, 7.5f }) Add(PrimitiveType.Cylinder, dark, new Vector3(x, -2, 3.5f), new Vector3(2.4f, 2.5f, 2.4f), new Vector3(90, 0, 0));   // motores
            Add(PrimitiveType.Sphere, M(new Color(1, .13f, .13f), true), new Vector3(18, -.6f, 1.5f), Vector3.one, Vector3.zero); Add(PrimitiveType.Sphere, M(new Color(.13f, 1, .25f), true), new Vector3(-18, -.6f, 1.5f), Vector3.one, Vector3.zero);   // luzes de navegação (vermelha à esquerda)
            return g;
        }
        static void Ensure()
        {
            if (I) return; var go = new GameObject("FB_AvioesCeu"); I = go.AddComponent<FBSkyPlanes>();
            if (!proto) proto = MakePlane();
            if (!trailMat)
            {
                // rastro de condensação (FreeBala/Contrail): ruído de algodão tileável que se erode com a idade; sem o shader, volta à fita simples
                var sh = FBShaders.Contrail;
                if (sh) { trailMat = new Material(sh) { name = "rastro_aviao" }; trailMat.SetTexture("_NoiseTex", NoiseTex()); }
                else { trailMat = FBShaders.Transparent(new Material(FBShaders.ParticlesUnlit)); trailMat.SetFloat("_Cull", 0); }
            }
        }
        // ruído fbm tileável 128×64 (valor) para o rastro
        static Texture2D NoiseTex()
        {
            const int W = 128, H = 64; var t = new Texture2D(W, H, TextureFormat.R8, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "ruido_rastro" };
            float Hs(int x, int y, int p) { x = ((x % p) + p) % p; y = ((y % p) + p) % p; uint h = (uint)(x * 374761393 + y * 668265263 + p * 982451653); h = (h ^ (h >> 13)) * 1274126177; return (h & 0xffff) / 65535f; }
            float Vn(float x, float y, int p)
            {
                int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y); float fx = x - xi, fy = y - yi; fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
                return Mathf.Lerp(Mathf.Lerp(Hs(xi, yi, p), Hs(xi + 1, yi, p), fx), Mathf.Lerp(Hs(xi, yi + 1, p), Hs(xi + 1, yi + 1, p), fx), fy);
            }
            var px = new byte[W * H];
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W, v = y / (float)H, a = 0, amp = .55f, sum = 0; int f = 4;
                    for (int o = 0; o < 5; o++) { a += Vn(u * f, v * f, f) * amp; sum += amp; amp *= .5f; f *= 2; }
                    px[y * W + x] = (byte)(Mathf.Clamp01(a / sum) * 255);
                }
            t.SetPixelData(px, 0); t.Apply(true, true); return t;
        }
        Trail MakeTrail()
        {
            var tr = new Trail { seed = Random.value }; tr.mesh = new Mesh(); tr.mesh.MarkDynamic(); tr.mesh.vertices = tr.P; tr.mesh.colors = tr.C; tr.mesh.uv = tr.UV; var idx = new int[(TRAIL_N - 1) * 6];
            for (int i = 0; i < TRAIL_N - 1; i++) { int a = i * 2; idx[i * 6] = a; idx[i * 6 + 1] = a + 1; idx[i * 6 + 2] = a + 2; idx[i * 6 + 3] = a + 2; idx[i * 6 + 4] = a + 1; idx[i * 6 + 5] = a + 3; }
            tr.mesh.triangles = idx; tr.mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e5f);
            tr.go = new GameObject("rastro"); tr.go.transform.SetParent(transform, false); tr.go.AddComponent<MeshFilter>().sharedMesh = tr.mesh; var mr = tr.go.AddComponent<MeshRenderer>(); mr.sharedMaterial = trailMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return tr;
        }
        // meio do rastro do último voo (vitrine -fbgfx: aponta a câmera para ele)
        public static Vector3? TrailMid()
        {
            if (!I) return null;
            for (int k = I.flights.Count - 1; k >= 0; k--) foreach (var tr in I.flights[k].trails) if (tr.pts.Count > 4) return tr.pts[tr.pts.Count / 2].p;
            return null;
        }
        public static Transform Current { get { if (!I) return null; var f = I.flights.Find(x => !x.done && x.plane); return f != null ? f.plane.transform : null; } }   // autoteste
        public static void Reset() { if (!I) return; foreach (var f in I.flights) I.Drop(f); I.flights.Clear(); }
        void Drop(Flight f) { if (f.plane) Destroy(f.plane); foreach (var t in f.trails) if (t.go) Destroy(t.go); if (f.snd) Destroy(f.snd.gameObject); }
        // um avião passando sobre 'at' (posição do ouvinte); baseY = chão do mapa
        public static void Flyover(Vector3 at, float baseY = 0, bool? smoke = null)
        {
            Ensure(); int active = 0; foreach (var f0 in I.flights) if (!f0.done) active++; if (active >= MAXF) return;
            float head = Random.value * Mathf.PI * 2; var dir = new Vector3(Mathf.Sin(head), 0, Mathf.Cos(head)); var side = new Vector3(dir.z, 0, -dir.x);
            float alt = baseY + 450 + Random.value * 300, off = (Random.value - .5f) * 700, speed = 120 + Random.value * 45, half = 2600;
            var f = new Flight { pos = new Vector3(at.x, alt, at.z) + side * off - dir * half, dir = dir, speed = speed, len = half * 2 / speed, smoke = smoke ?? Random.value < .45f };
            f.plane = Instantiate(proto, I.transform); f.plane.SetActive(true); f.plane.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 0, (Random.value - .5f) * 4.6f);
            if (f.smoke) for (int i = 0; i < 2; i++) f.trails.Add(I.MakeTrail());
            var clip = FBAudio.Clip("amb_plane"); if (clip) { var sg = new GameObject("som_aviao"); sg.transform.SetParent(I.transform, false); sg.transform.position = f.pos; var s = sg.AddComponent<AudioSource>(); s.clip = clip; s.loop = true; s.spatialBlend = 1; s.dopplerLevel = 0; s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 220; s.maxDistance = 3200; s.volume = 0; s.pitch = .85f + Random.value * .2f; s.Play(); f.snd = s; }
            I.flights.Add(f);
        }
        // [parceiro] v1 (thread de física): tempo do LateUpdate medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
        static readonly int __fbt_LateUpdate = FBTempo.Id("FBSkyPlanes.LateUpdate");
        void LateUpdate()
        {
            if (!FBTempo.On) { LateUpdate__fbt(); return; }
            long t0 = FBTempo.T0(); LateUpdate__fbt(); FBTempo.Soma(__fbt_LateUpdate, t0);
        }
        void LateUpdate__fbt()
        {
            var cam = UnityEngine.Camera.main; if (!cam || flights.Count == 0) return; float dt = Time.deltaTime; var cp = cam.transform.position;
            if (trailMat && trailMat.HasProperty("_SunCol"))                                      // cor pela luz do sol/lua do momento
            {
                var sun = RenderSettings.sun; float si = sun ? Mathf.Clamp01(sun.intensity / 1.25f) : .5f; var sc = sun ? sun.color : Color.white;
                var amb = RenderSettings.ambientSkyColor * RenderSettings.ambientIntensity;
                trailMat.SetColor("_SunCol", Color.Lerp(amb * 1.1f, sc * 1.05f, Mathf.Lerp(.15f, 1, si)) + amb * .25f);
                trailMat.SetColor("_ShadeCol", Color.Lerp(amb * .55f, amb * .85f + sc * .15f, si));
                trailMat.SetFloat("_MaxAlpha", Mathf.Lerp(.35f, .85f, si));                      // de noite quase não se vê (só o luar)
            }
            for (int k = flights.Count - 1; k >= 0; k--)
            {
                var f = flights[k];
                if (!f.done)
                {
                    f.t += dt; f.pos += f.dir * (f.speed * dt);
                    if (f.snd) { f.snd.transform.position = f.pos; float d0 = Vector3.Distance(f.pos, cp); f.snd.volume = Mathf.Lerp(f.snd.volume, .13f * Mathf.Clamp01((2200 - d0) / 900) * FBAudio.AmbVol, 1 - Mathf.Exp(-dt / .3f)); }   // entra e sai de mansinho; pedido 26/10: bem mais baixo (era 0,3)
                    if (f.smoke && (f.tt += dt) >= TRAIL_DT)
                    {
                        f.tt = 0; var sd = new Vector3(f.dir.z, 0, -f.dir.x);
                        for (int i = 0; i < f.trails.Count; i++) { var tr = f.trails[i]; tr.pts.Add((f.pos - f.dir * 8 + sd * (i == 1 ? 7.5f : -7.5f) + Vector3.down * 2, 0, f.t * f.speed / 55f)); if (tr.pts.Count > TRAIL_N) tr.pts.RemoveAt(0); }
                    }
                    if (f.t >= f.len) { f.done = true; if (f.plane) Destroy(f.plane); if (f.snd) Destroy(f.snd.gameObject); }
                }
                if (!f.done && f.plane) { var dd = f.pos - cp; float D = dd.magnitude, k2 = R / D; f.plane.transform.position = cp + dd * k2; f.plane.transform.localScale = Vector3.one * k2; f.plane.SetActive(D < 3200); }
                bool alive = false;
                var wv = Shader.GetGlobalVector("_FBWind"); var drift = new Vector3(wv.x, 0, wv.y) * (2.5f + 2 * wv.z) + Vector3.down * .35f;   // vento de altitude + o rastro desce devagar
                var sd2 = new Vector3(f.dir.z, 0, -f.dir.x);
                for (int ti = 0; ti < f.trails.Count; ti++)
                {
                    var tr = f.trails[ti]; float toward = ti == 1 ? -1 : 1;              // os dois rastros se aproximam (vórtices das asas)
                    for (int i = 0; i < tr.pts.Count; i++) { var q0 = tr.pts[i]; float conv = q0.t < 30 ? .16f : 0; tr.pts[i] = (q0.p + (drift + sd2 * (toward * conv)) * dt, q0.t + dt, q0.u); }
                    while (tr.pts.Count > 0 && tr.pts[0].t > TRAIL_LIFE) tr.pts.RemoveAt(0);
                    int n = tr.pts.Count; for (int i = 0; i < tr.C.Length; i++) tr.C[i] = default;
                    if (n >= 2)
                    {
                        alive = true;
                        for (int i = 0; i < n; i++)
                        {
                            var q = tr.pts[i]; var nx = tr.pts[Mathf.Min(n - 1, i + 1)].p; var pv = tr.pts[Mathf.Max(0, i - 1)].p; var dd = q.p - cp; float D = dd.magnitude, k2 = R / D;
                            var along = (nx - pv).normalized; var side = Vector3.Cross(along, dd).normalized; if (Vector3.Dot(side, Vector3.up) < 0) side = -side;   // y=1 = lado de cima
                            float w = (1.4f + Mathf.Sqrt(q.t) * 3.1f + q.t * .25f) * k2; var c = cp + dd * k2;   // alarga rápido no começo, depois devagar
                            tr.P[i * 2] = c - side * w; tr.P[i * 2 + 1] = c + side * w;
                            // nasce ~1 s atrás do motor (o vapor condensa depois de esfriar), some no fim da vida e nas pontas
                            float a = Mathf.SmoothStep(0, 1, (q.t - .6f) * 1.4f) * Mathf.Max(0, 1 - q.t / TRAIL_LIFE) * (i == n - 1 || i == 0 ? 0 : 1);
                            tr.C[i * 2] = tr.C[i * 2 + 1] = new Color(Mathf.Clamp01(q.t / TRAIL_LIFE), tr.seed, 0, a);
                            tr.UV[i * 2] = new Vector2(q.u, 0); tr.UV[i * 2 + 1] = new Vector2(q.u, 1);
                        }
                        for (int i = n; i < TRAIL_N; i++) { tr.P[i * 2] = tr.P[(n - 1) * 2]; tr.P[i * 2 + 1] = tr.P[(n - 1) * 2 + 1]; tr.UV[i * 2] = tr.UV[(n - 1) * 2]; tr.UV[i * 2 + 1] = tr.UV[(n - 1) * 2 + 1]; }
                    }
                    tr.mesh.vertices = tr.P; tr.mesh.colors = tr.C; tr.mesh.uv = tr.UV;
                }
                if (f.done && !alive) { Drop(f); flights.RemoveAt(k); }
            }
        }
    }
}
