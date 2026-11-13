// LUZES DOS VEÍCULOS (refeitas): acendem as LENTES de verdade de cada modelo do REB — os triângulos do farol, da lanterna/freio, da ré
// e das setas foram achados na carroceria pela posição de cada lâmpada (vehicles.json "lights") e pela cor do atlas
// (tools/lamps/build_lamps.py -> Resources/FBLamps/carlamps.json) e viram uma "casca" emissiva por grupo, colada na lente. Por cima:
//  · brilho (corona) no centro de cada lente, só para quem está NA FRENTE dela (farol não brilha visto de trás), tamanho mínimo na tela;
//  · faróis: facho por lado (spot com máscara de farol baixo e corte horizontal) nos carros mais perto, o do jogador primeiro;
//  · lanterna (luz de posição) acesa junto com o farol; FREIO = lanterna forte (também parado com o pé no freio); RÉ = branca + luz
//    no chão atrás; SETAS âmbar piscando 1,4 Hz (NPC antes de virar/estacionar; jogador: Q / E, com cancelamento ao endireitar);
//    PISCA-ALERTA (X no carro; NPC travado/batido); L = faróis automático / ligado / desligado;
//  · à noite o vermelho do freio ilumina o chão atrás dos carros mais perto (pool fixo de luzes, sem criar no meio da partida).
// Carro estacionado sem motorista: tudo apagado. Sem lente achada: cai no brilho no ponto da lâmpada (como antes).
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
    public static class FBCarLights
    {
        enum G { Head, Tail, Rev, IndL, IndR }
        class Lamp { public G g; public Vector3 c, n; public Transform glow; public float size; public int side; public bool dead; public float flickT, flickNext; public bool flickOn; }
        class Rig
        {
            public FBCar car; public Transform shell; public readonly List<Lamp> lamps = new(); public readonly MeshRenderer[] lens = new MeshRenderer[10];   // grupo × lado (E/D)
            public Vector3 hl, hr, bl, br; public bool hasHead, hasTail, headL = true, headR = true, lensOn, hadBeam, hadTail, hadRev; public float lastV, brakeT, accSm;
            public float headSm, highSm, beamK; public Vector3 plate; public bool hasPlate, hadPlate;   // r65: rampa da lâmpada, farol alto, entrada do facho, luz de placa
        }
        static readonly Dictionary<FBCar, Rig> rigs = new(); static readonly Dictionary<string, Mesh[]> meshCache = new();
        static readonly List<Light> beams = new(), tailPool = new(), revPool = new();
        static Material glowMat, lensMat; static Mesh quad; static Texture2D glowTex, cookie; static Dictionary<string, object> data; static bool dataTried;
        static readonly MaterialPropertyBlock mpb = new();
        // carro do jogador
        public static int headMode;                 // 0 automático, 1 ligado, 2 desligado
        public static int LensToggles, BeamSwaps, BeamsOn;   // r45: laboratório (-fbcarrolab)
        static readonly Rig[] beamOwner = new Rig[12];
        static int playerBlink; static bool playerHazard; static float steerPeak;
        public static int PlayerBlinkNow => playerHazard ? 2 : playerBlink; public static bool PlayerBrakeNow;   // r45b: vão pela rede (FBDrive.LightBits)
        public static void TestBlink(int b) { playerHazard = b == 2; playerBlink = b == 2 ? 0 : b; steerPeak = 0; }   // laboratório
        public static bool LensOn(FBCar car, int g, int side) { if (!car || !rigs.TryGetValue(car, out var r)) return false; var mr = g * 2 + side < r.lens.Length ? r.lens[g * 2 + side] : null; return mr && mr.enabled; }
        public static void Reset() { rigs.Clear(); beams.Clear(); tailPool.Clear(); revPool.Clear(); platePool.Clear(); }
        // r65 (fb_visual carLights = 1): reformulação pedida pelo usuário ("duas bolas de luz extremamente artificiais"). Causa raiz das bolas:
        // o pipeline do PC estava com cookies DESLIGADOS (m_SupportsLightCookies 0) — a máscara do farol baixo era ignorada e o facho virava um
        // círculo de 62° — e as coronas (quads somados de 0,5 m crescendo até ×2,6) ficavam por cima da lente até de perto. Agora:
        //  · cookies ligados: farol BAIXO com linha de corte horizontal e degrau de 15° para a direita (mão brasileira), mancha quente abaixo
        //    do eixo; farol ALTO (tecla V) com facho central longo, sem corte; troca com rampa de 0,15 s; lâmpada acende/apaga em rampa;
        //  · corona só de LONGE (18→38 m, onde a lente vira 1–2 px) e pequena; de perto quem brilha é a lente;
        //  · lente com MÁSCARA de emissão: a casca é subdividida e cada vértice recebe o brilho pela distância ao centro da lâmpada (ponto
        //    quente no refletor, borda da lente apagada) — antes o grupo inteiro acendia chapado;
        //  · luz de placa (pool fixo) e fachos entrando em rampa quando trocam de dono (sem pop).
        static bool NewL => FBVisual.C.carLights > .5f;
        static Texture2D hiCookie; static readonly List<Light> platePool = new(); static bool highBeam;
        static readonly Dictionary<Mesh, Vector3?> plateCache = new();
        public static void TestHigh(bool on) => highBeam = on;                                   // laboratório (capturas farol alto)
        public static bool HighBeam => highBeam;
        public static FBCar TestCar;                                                          // laboratório: carro sem motorista tratado como o do jogador no farol alto

        static Material Mat()
        {
            if (glowMat) return glowMat;
            glowTex = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) { float d = new Vector2(x - 31.5f, y - 31.5f).magnitude / 31.5f; float a = Mathf.Clamp01(1 - d); glowTex.SetPixel(x, y, new Color(1, 1, 1, a * a * (1 + 2 * Mathf.Clamp01(1 - d * 3)))); }
            glowTex.Apply(); glowMat = FBShaders.Transparent(new Material(FBShaders.ParticlesUnlit), true); glowMat.SetTexture("_BaseMap", glowTex); glowMat.mainTexture = glowTex; glowMat.SetFloat("_Cull", 0); glowMat.renderQueue = 3050;
            lensMat = FBShaders.Transparent(new Material(FBShaders.ParticlesUnlit), true); lensMat.SetTexture("_BaseMap", Texture2D.whiteTexture); lensMat.mainTexture = Texture2D.whiteTexture; lensMat.SetFloat("_Cull", 0); lensMat.renderQueue = 3020;
            quad = new Mesh { name = "brilho" }; quad.SetVertices(new[] { new Vector3(-.5f, -.5f), new Vector3(.5f, -.5f), new Vector3(-.5f, .5f), new Vector3(.5f, .5f) }); quad.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) }); quad.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0); quad.RecalculateBounds();
            // máscara do farol baixo: forte embaixo/no meio, corte reto em cima (um pouco mais alto à direita — mão brasileira)
            if (NewL)
            {   // r65: 256², corte NÍTIDO (gradiente de ~1°) com degrau inclinado de 15° à direita, mancha quente abaixo do eixo, abertura lateral
                const int N = 256; cookie = new Texture2D(N, N, TextureFormat.R8, false, true) { wrapMode = TextureWrapMode.Clamp, name = "cookie_farol_baixo" };
                hiCookie = new Texture2D(N, N, TextureFormat.R8, false, true) { wrapMode = TextureWrapMode.Clamp, name = "cookie_farol_alto" };
                var lo = new byte[N * N]; var hi = new byte[N * N];
                for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                {
                    float u = (x + .5f) / N * 2 - 1, v = (y + .5f) / N * 2 - 1;
                    float cut = .07f + Mathf.Clamp((u - .04f) * .27f, 0, .1f);                                 // 15° = tan .27
                    float top = Mathf.Clamp01((cut - v) * 26 + .5f);
                    float hot = Mathf.Exp(-(u * u / (.42f * .42f) + (v + .09f) * (v + .09f) / (.2f * .2f)));
                    float spread = Mathf.Clamp01(1 - Mathf.Sqrt(u * u * .55f + v * v * 1.2f));
                    float edge = 1 - Mathf.SmoothStep(.86f, 1, Mathf.Sqrt(u * u + v * v));
                    lo[y * N + x] = (byte)Mathf.RoundToInt(255 * Mathf.Clamp01(top * edge * (.8f * hot + .45f * Mathf.Pow(spread, .8f))));
                    float hr2 = u * u / (.55f * .55f) + (v - .02f) * (v - .02f) / (.32f * .32f);
                    hi[y * N + x] = (byte)Mathf.RoundToInt(255 * Mathf.Clamp01(edge * (.85f * Mathf.Exp(-hr2 * 1.6f) + .35f * Mathf.Pow(spread, 1.2f))));
                }
                cookie.SetPixelData(lo, 0); cookie.Apply(false, true); hiCookie.SetPixelData(hi, 0); hiCookie.Apply(false, true); return glowMat;
            }
            cookie = new Texture2D(128, 128, TextureFormat.R8, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
                {
                    float u = (x + .5f) / 128 * 2 - 1, v = (y + .5f) / 128 * 2 - 1; float r = Mathf.Sqrt(u * u * .55f + v * v);
                    float cut = .12f + (u > .1f ? .1f : 0); float top = Mathf.Clamp01((cut - v) * 9);
                    float k = Mathf.Clamp01(1 - r) * top; k = Mathf.Pow(k, .7f) * (.55f + .45f * Mathf.Clamp01(1 - Mathf.Abs(u) * 1.1f));
                    cookie.SetPixel(x, y, new Color(k, k, k, k));
                }
            cookie.Apply(); return glowMat;
        }
        static Dictionary<string, object> Data()
        {
            if (!dataTried) { dataTried = true; var ta = Resources.Load<TextAsset>("FBLamps/carlamps"); if (ta) data = Json.Parse(ta.text) as Dictionary<string, object>; }
            return data;
        }
        static G GroupOf(string kind, string side) => kind == "head" ? G.Head : kind == "brake" ? G.Tail : kind == "rev" ? G.Rev : side == "L" ? G.IndL : G.IndR;
        static int LensIdx(G g, string side) => (int)g * 2 + (g == G.IndL ? 0 : g == G.IndR ? 1 : side == "R" ? 1 : 0);
        // ---- quebra: lâmpadas perto do ponto (batida/tiro) entram em curto (pisca irregular 0,5–1,5 s) e apagam de vez
        public static int BreakNear(FBCar car, Vector3 wp, float radius)
        {
            if (!car) return 0; if (!rigs.TryGetValue(car, out var r)) { Mat(); rigs[car] = r = Build(car); }
            int n = 0;
            foreach (var lp in r.lamps)
            {
                if (lp.dead) continue; var w = r.shell.TransformPoint(lp.c); if ((w - wp).sqrMagnitude > radius * radius) continue;
                lp.dead = true; lp.flickT = Random.Range(.5f, 1.5f); lp.flickNext = 0; lp.flickOn = true; n++;
                FBFx.Sparks(w, r.shell.TransformDirection(lp.n));
            }
            if (n > 0) FBAudio.PlayAt("glass_break", wp, .3f, 3);
            return n;
        }
        public static int DeadCount(FBCar car) => car && rigs.TryGetValue(car, out var r) ? r.lamps.Count(l => l.dead) : 0;
        public static void Repair(FBCar car) { if (car && rigs.TryGetValue(car, out var r)) foreach (var lp in r.lamps) { lp.dead = false; lp.flickT = 0; } }
        // aceso agora? (inteira, ou em curto no instante "ligado")
        static bool Alive(Lamp lp, float dt)
        {
            if (!lp.dead) return true; if (lp.flickT <= 0) return false;
            lp.flickT -= dt; if (Time.time >= lp.flickNext) { lp.flickOn = !lp.flickOn; lp.flickNext = Time.time + (lp.flickOn ? Random.Range(.02f, .1f) : Random.Range(.03f, .25f)); }
            return lp.flickOn && lp.flickT > 0;
        }
        static Rig Build(FBCar c)
        {
            var r = new Rig { car = c, shell = c.shell ? c.shell : c.transform.Find("body/shell") ?? c.transform }; Mat();
            var D = Data(); var L = D != null ? Json.O(D, c.id) : null;
            if (L != null)
            {
                if (!meshCache.TryGetValue(c.id, out var meshes))
                {   // casca de cada grupo POR LADO (cada farol/lanterna quebra sozinho), 4 mm para fora da lente (um mesh por modelo, dividido entre os carros iguais)
                    meshes = new Mesh[10]; var vs = new List<Vector3>[10]; var cs = new List<Color>[10]; for (int i = 0; i < 10; i++) { vs[i] = new List<Vector3>(); cs[i] = new List<Color>(); }
                    foreach (var kv in L)
                        foreach (var o in kv.Value as List<object>)
                        {
                            var d = o as Dictionary<string, object>; var g = GroupOf(kv.Key, Json.S(d, "s")); var t = Json.L(d, "t"); var n = V3(Json.L(d, "n"));
                            if (t == null) continue; int li = LensIdx(g, Json.S(d, "s")); int st = vs[li].Count;
                            for (int i = 0; i + 8 < t.Count; i += 9)
                            {
                                var a = new Vector3(Json.Fv(t[i]), Json.Fv(t[i + 1]), Json.Fv(t[i + 2])) + n * .004f; var b = new Vector3(Json.Fv(t[i + 3]), Json.Fv(t[i + 4]), Json.Fv(t[i + 5])) + n * .004f; var cc3 = new Vector3(Json.Fv(t[i + 6]), Json.Fv(t[i + 7]), Json.Fv(t[i + 8])) + n * .004f;
                                if (NewL) Sub(vs[li], a, b, cc3, 2); else { vs[li].Add(a); vs[li].Add(b); vs[li].Add(cc3); }
                            }
                            // máscara de emissão (r65): brilho pela distância ao centro da lâmpada (refletor quente, borda da lente apagada)
                            var lc = V3(Json.L(d, "c")); float R = 1e-3f; for (int k = st; k < vs[li].Count; k++) R = Mathf.Max(R, (vs[li][k] - lc).magnitude);
                            for (int k = st; k < vs[li].Count; k++) { float q = 1 - Mathf.Clamp01((vs[li][k] - lc).magnitude / R); float e = NewL ? Mathf.Lerp(.22f, 1, Mathf.Pow(q, 1.3f)) : 1; cs[li].Add(new Color(e, e, e, 1)); }
                        }
                    for (int i = 0; i < 10; i++) if (vs[i].Count >= 3) { var m = new Mesh { name = "lente_" + i }; if (vs[i].Count > 65000) m.indexFormat = IndexFormat.UInt32; m.SetVertices(vs[i]); m.SetColors(cs[i]); var idx = new int[vs[i].Count]; for (int k = 0; k < idx.Length; k++) idx[k] = k; m.SetTriangles(idx, 0); m.RecalculateBounds(); meshes[i] = m; }
                    meshCache[c.id] = meshes;
                }
                for (int i = 0; i < 10; i++) if (meshes[i])
                    {
                        var go = new GameObject("lente_" + i); go.transform.SetParent(r.shell, false); go.layer = c.gameObject.layer; go.AddComponent<MeshFilter>().sharedMesh = meshes[i];
                        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = lensMat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false; mr.lightProbeUsage = LightProbeUsage.Off; mr.enabled = false; r.lens[i] = mr;
                    }
                foreach (var kv in L)
                    foreach (var o in kv.Value as List<object>)
                    {
                        var d = o as Dictionary<string, object>; var g = GroupOf(kv.Key, Json.S(d, "s")); var lp = new Lamp { g = g, c = V3(Json.L(d, "c")), n = V3(Json.L(d, "n")), side = LensIdx(g, Json.S(d, "s")) & 1 };
                        if (lp.n.sqrMagnitude < .1f) lp.n = new Vector3(0, 0, lp.c.z > 0 ? 1 : -1);
                        lp.size = g == G.Head ? .5f : g == G.Tail ? .36f : .3f; lp.glow = Quad(r.shell, lp.c + lp.n * .03f, "brilho_" + g); r.lamps.Add(lp);
                    }
            }
            else
            {   // modelo sem lentes medidas: brilho nos pontos das lâmpadas (vehicles.json)
                var Ls = Json.O(Json.O(GameData.Vehicles, c.id), "lights");
                List<Vector3> Pts(string k) => (Json.L(Ls, k) ?? new List<object>()).Select(o => o as List<object>).Where(p => p != null && p.Count >= 3).Select(p => new Vector3(-Json.Fv(p[0]), Json.Fv(p[1]), Json.Fv(p[2]))).ToList();
                foreach (var p in Pts("HeadLight").Where(p => Mathf.Abs(p.x) > .15f)) r.lamps.Add(new Lamp { g = G.Head, c = p, n = Vector3.forward, size = .5f, glow = Quad(r.shell, p, "brilho_farol"), side = p.x < 0 ? 0 : 1 });
                foreach (var p in Pts("BrakeLight")) r.lamps.Add(new Lamp { g = G.Tail, c = p, n = Vector3.back, size = .36f, glow = Quad(r.shell, p, "brilho_freio"), side = p.x < 0 ? 0 : 1 });
                foreach (var p in Pts("ReverseLight")) r.lamps.Add(new Lamp { g = G.Rev, c = p, n = Vector3.back, size = .3f, glow = Quad(r.shell, p, "brilho_re"), side = p.x < 0 ? 0 : 1 });
                foreach (var p in Pts("IndicatorLight")) r.lamps.Add(new Lamp { g = p.x < 0 ? G.IndL : G.IndR, c = p, n = p.z > 0 ? Vector3.forward : Vector3.back, size = .3f, glow = Quad(r.shell, p, "brilho_seta"), side = p.x < 0 ? 0 : 1 });
            }
            // origens dos fachos (faróis principais: os dois mais altos e afastados do centro) e da luz de freio no chão
            var heads = r.lamps.Where(l => l.g == G.Head).ToList();
            if (heads.Count > 0)
            {
                var main = heads.OrderByDescending(l => l.c.y).Take(Mathf.Max(2, heads.Count / 2)).ToList();
                r.hl = main.OrderBy(l => l.c.x).First().c; r.hr = main.OrderBy(l => l.c.x).Last().c; r.hasHead = true;
            }
            var tails = r.lamps.Where(l => l.g == G.Tail).ToList(); if (tails.Count > 0) { r.bl = tails.OrderBy(l => l.c.x).First().c; r.br = tails.OrderBy(l => l.c.x).Last().c; r.hasTail = true; }
            if (NewL) FindPlate(r);
            return r;
        }
        // r65: triângulo dividido em 4^n (a casca da lente tem poucos triângulos grandes, todos os vértices na borda: sem vértice no meio não
        // há como pôr o ponto quente da lâmpada)
        static void Sub(List<Vector3> o, Vector3 a, Vector3 b, Vector3 c, int lv)
        {
            if (lv <= 0) { o.Add(a); o.Add(b); o.Add(c); return; }
            Vector3 ab = (a + b) * .5f, bc = (b + c) * .5f, ca = (c + a) * .5f;
            Sub(o, a, ab, ca, lv - 1); Sub(o, ab, b, bc, lv - 1); Sub(o, ca, bc, c, lv - 1); Sub(o, ab, bc, ca, lv - 1);
        }
        // placa TRASEIRA (submesh "veh_plate"/"placa" do FBPlates): centro em coordenadas da carroceria; sem placa achada = abaixo das lanternas
        static void FindPlate(Rig r)
        {
            foreach (var mr in r.car.GetComponentsInChildren<MeshRenderer>(true))
            {
                var ms = mr.sharedMaterials; var mf = mr.GetComponent<MeshFilter>(); var mesh = mf ? mf.sharedMesh : null; if (!mesh) continue;
                for (int k = 0; k < ms.Length && k < mesh.subMeshCount; k++)
                {
                    if (!ms[k] || !(ms[k].name.StartsWith("veh_plate") || ms[k].name.StartsWith("placa"))) continue;
                    if (!plateCache.TryGetValue(mesh, out var pc))
                    {
                        pc = null;
                        if (mesh.isReadable)
                        {
                            var tri = mesh.GetTriangles(k); var vv = mesh.vertices; Vector3 sum = Vector3.zero; int n = 0;
                            foreach (var ix in tri) { var p = r.shell.InverseTransformPoint(mr.transform.TransformPoint(vv[ix])); if (p.z < 0) { sum += p; n++; } }
                            if (n > 0) pc = sum / n;
                        }
                        plateCache[mesh] = pc;
                    }
                    if (pc.HasValue) { r.plate = pc.Value; r.hasPlate = true; return; }
                }
            }
            if (r.hasTail) { r.plate = new Vector3(0, Mathf.Min(r.bl.y, r.br.y) - .14f, Mathf.Min(r.bl.z, r.br.z) + .02f); r.hasPlate = true; }
        }
        static Vector3 V3(List<object> l) => l == null || l.Count < 3 ? Vector3.zero : new Vector3(Json.Fv(l[0]), Json.Fv(l[1]), Json.Fv(l[2]));
        static Transform Quad(Transform parent, Vector3 p, string n)
        {
            var g = new GameObject(n); g.transform.SetParent(parent, false); g.transform.localPosition = p; g.AddComponent<MeshFilter>().sharedMesh = quad; var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = glowMat;
            mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false; mr.lightProbeUsage = LightProbeUsage.Off; g.SetActive(false); return g.transform;
        }
        static void Pools()
        {
            if (beams.Count > 0) return; bool mob = FBQuality.Mobile; Mat();
            for (int i = 0; i < (mob ? 2 : 6); i++)
            {
                var l = new GameObject("FacoFarol" + i).AddComponent<Light>(); l.type = LightType.Spot; l.range = 42; l.spotAngle = 62; l.innerSpotAngle = 26; l.useColorTemperature = true; l.colorTemperature = 4700; l.color = Color.white;
                l.cookie = NewL ? cookie : null; l.shadows = i < 2 && !mob ? LightShadows.Soft : LightShadows.None; l.shadowNearPlane = .3f; l.enabled = false; beams.Add(l); FBVolumetric.Sources.Add(l);
            }
            for (int i = 0; i < (mob ? 0 : 3); i++) { var l = new GameObject("LuzFreio" + i).AddComponent<Light>(); l.type = LightType.Spot; l.range = 6.5f; l.spotAngle = 125; l.innerSpotAngle = 45; l.color = new Color(1, .08f, .04f); l.shadows = LightShadows.None; l.enabled = false; tailPool.Add(l); }
            for (int i = 0; i < (mob ? 1 : 2); i++) { var l = new GameObject("LuzRe" + i).AddComponent<Light>(); l.type = LightType.Spot; l.range = 9; l.spotAngle = 100; l.innerSpotAngle = 50; l.color = new Color(1, .97f, .92f); l.shadows = LightShadows.None; l.enabled = false; revPool.Add(l); }
            // r65: luz de placa (lâmpada W5W ~3200 K acima da placa traseira): ponto curto, só nos 3 carros mais perto
            if (NewL) for (int i = 0; i < (mob ? 0 : 3); i++) { var l = new GameObject("LuzPlaca" + i).AddComponent<Light>(); l.type = LightType.Point; l.range = .85f; l.useColorTemperature = true; l.colorTemperature = 3200; l.color = Color.white; l.shadows = LightShadows.None; l.enabled = false; platePool.Add(l); }
        }

        // ---- comandos do jogador no carro (FBDrive.PlayerInput): L faróis, Q/E setas, X pisca-alerta
        public static void PlayerControl(FBCar car)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current; if (kb == null || !car) return;
            if (kb.lKey.wasPressedThisFrame) { headMode = (headMode + 1) % 3; FBHUD.I?.Center(headMode == 0 ? "FARÓIS: AUTOMÁTICO" : headMode == 1 ? "FARÓIS: LIGADOS" : "FARÓIS: DESLIGADOS", 1.1f); }
            if (kb.qKey.wasPressedThisFrame) { playerBlink = playerBlink == -1 ? 0 : -1; steerPeak = 0; }
            if (kb.eKey.wasPressedThisFrame) { playerBlink = playerBlink == 1 ? 0 : 1; steerPeak = 0; }
            if (kb.xKey.wasPressedThisFrame) { playerHazard = !playerHazard; FBHUD.I?.Center(playerHazard ? "PISCA-ALERTA LIGADO" : "PISCA-ALERTA DESLIGADO", 1); }
            if (kb.vKey.wasPressedThisFrame && NewL && FBVisual.C.carHighBeam > .5f) { highBeam = !highBeam; FBHUD.I?.Center(highBeam ? "FAROL ALTO" : "FAROL BAIXO", 1); }   // r65
            // seta cancela sozinha quando o volante volta depois da curva (como no carro de verdade)
            if (playerBlink != 0) { float s = car.steerN * -playerBlink; steerPeak = Mathf.Max(steerPeak, -s); if (steerPeak > .35f && Mathf.Abs(car.steerN) < .08f) playerBlink = 0; }
        }

        public static void Tick(Camera cam, float night, bool rain, float dt)
        {
            if (!cam) return; Pools(); var camPos = cam.transform.position; var camF = cam.transform.forward;
            bool dark = night > .3f || rain; float headK = Mathf.Max(rain ? .75f : 0, Mathf.InverseLerp(.25f, .75f, night)), nightK = Mathf.InverseLerp(.3f, .8f, night);
            bool blinkOn = (Time.time % .72f) < .38f;
            var near = new List<(Rig r, float d2, bool head)>(); var brakeNear = new List<(Rig r, float d2, float k)>(); var revNear = new List<(Rig r, float d2)>(); var plateNear = new List<(Rig r, float d2)>();
            bool NL = NewL;
            var player = FBDrive.Cur;
            foreach (var c in FBVehicles.list)
            {
                if (!c) continue; float d2 = (c.transform.position - camPos).sqrMagnitude;
                bool engine = c.driver || c.remoteDriver >= 0 || c.aiLights && c.driver;
                if (!rigs.TryGetValue(c, out var r)) { if (!engine && d2 > 60 * 60) continue; rigs[c] = r = Build(c); }
                bool vis = d2 < 220 * 220;
                // estados
                float acc = (c.v - r.lastV) / Mathf.Max(1e-3f, dt); r.lastV = c.v; r.accSm = Mathf.Lerp(r.accSm, acc, Mathf.Min(1, dt * 8));
                bool isPlayer = c == player, head, brake, rev; int blink;
                if (!engine) { head = brake = rev = false; blink = 0; }
                else if (c.aiLights && !isPlayer)
                {   // r45: o freio do trânsito segue o sinal da aceleração do IDM (liga/desliga quadro a quadro seguindo outro carro) — segura 0,35 s; parado = pé no freio
                    head = dark && !c.lHeadOff; rev = c.lRev; blink = c.lBlink;
                    if (c.lBrake || Mathf.Abs(c.v) < .15f) r.brakeT = .35f; else r.brakeT -= dt; brake = r.brakeT > 0;
                }
                else if (isPlayer)
                {
                    head = headMode == 1 || (headMode == 0 && dark); rev = c.v < -.3f;
                    if ((c.prevThr < -.05f && c.v > .3f) || (c.prevThr < -.05f && Mathf.Abs(c.v) < .3f && c.v >= -.05f) || (Mathf.Abs(c.v) < .15f && c.prevThr <= .05f) || FBDrive.hold) r.brakeT = .2f; else r.brakeT -= dt;   // parado = pé no freio
                    brake = r.brakeT > 0; blink = playerHazard ? 2 : playerBlink; PlayerBrakeNow = brake;
                }
                else
                {   // carro de outro jogador: pelos bits da rede (r45b); sem eles (versão antiga), pela desaceleração e parado
                    rev = c.v < -.3f;
                    if (c.netLights >= 0) { int hm = (c.netLights >> 3) & 3, bk = (c.netLights >> 5) & 3; head = hm == 1 || (hm == 0 && dark); blink = bk == 3 ? 2 : bk == 1 ? -1 : bk == 2 ? 1 : 0; if ((c.netLights & 128) != 0 || Mathf.Abs(c.v) < .15f) r.brakeT = .25f; else r.brakeT -= dt; brake = r.brakeT > 0; }
                    else { head = dark; if (r.accSm < -2.2f && c.v > .8f || Mathf.Abs(c.v) < .15f) r.brakeT = .25f; else r.brakeT -= dt; brake = r.brakeT > 0; blink = 0; }
                }
                bool tail = head;                                                    // lanterna (luz de posição) junto com o farol
                // r65: lâmpada halógena acende em ~0,15 s e apaga em ~0,25 s; farol alto só no carro do jogador
                r.headSm = NL ? Mathf.MoveTowards(r.headSm, head ? 1 : 0, dt * (head ? 7f : 4f)) : head ? 1 : 0;
                r.highSm = Mathf.MoveTowards(r.highSm, NL && (isPlayer || c == TestCar) && highBeam && head && FBVisual.C.carHighBeam > .5f ? 1 : 0, dt * 6.5f);
                bool headLit = NL ? r.headSm > .01f : head;
                bool bl = blink == 2 ? blinkOn : blink == -1 && blinkOn, br2 = blink == 2 ? blinkOn : blink == 1 && blinkOn;
                // lentes (perto: casca emissiva; longe some — o brilho cobre)
                bool lensVis = r.lensOn = d2 < (r.lensOn ? 100 * 100 : 90 * 90);             // r45: com folga (sem acender/apagar na borda)
                var hc = c.headTint ?? new Color(1, .93f, .8f);
                // lâmpada a lâmpada: quebrada apaga (depois do curto); a lente de cada lado acende se alguma lâmpada dela está viva
                var aliveSide = new bool[10]; foreach (var lp in r.lamps) { lp.flickOn = lp.flickOn; if (Alive(lp, dt)) aliveSide[(int)lp.g * 2 + lp.side] = true; }
                bool anyLamp = r.lamps.Count > 0;
                for (int sd = 0; sd < 2; sd++)
                {
                    bool A(G g) => !anyLamp || aliveSide[(int)g * 2 + sd];
                    if (NL)
                    {   // r65: intensidades na proporção real (freio ≈ 4–5× a lanterna; seta âmbar SAE; ré branca) e sem estourar o bloom
                        SetLens(r, G.Head, sd, lensVis && headLit && A(G.Head), hc * ((1.25f + .85f * headK + .55f * r.highSm) * r.headSm));
                        SetLens(r, G.Tail, sd, lensVis && (brake || tail) && A(G.Tail), new Color(1, .035f, .015f) * (brake ? 2.3f : .5f));
                        SetLens(r, G.Rev, sd, lensVis && rev && A(G.Rev), new Color(1, .98f, .95f) * 1.7f);
                        SetLens(r, G.IndL, sd, lensVis && bl && A(G.IndL), new Color(1, .38f, .02f) * 2.4f);
                        SetLens(r, G.IndR, sd, lensVis && br2 && A(G.IndR), new Color(1, .38f, .02f) * 2.4f);
                        continue;
                    }
                    SetLens(r, G.Head, sd, lensVis && head && A(G.Head), hc * (2.2f + 1.2f * headK));
                    SetLens(r, G.Tail, sd, lensVis && (brake || tail) && A(G.Tail), new Color(1, .04f, .02f) * (brake ? 3.4f : 1.0f) * (dark || brake ? 1 : .8f));
                    SetLens(r, G.Rev, sd, lensVis && rev && A(G.Rev), new Color(1, 1, 1) * 2.4f);
                    SetLens(r, G.IndL, sd, lensVis && bl && A(G.IndL), new Color(1, .42f, .02f) * 3.2f);
                    SetLens(r, G.IndR, sd, lensVis && br2 && A(G.IndR), new Color(1, .42f, .02f) * 3.2f);
                }
                // coronas
                foreach (var lp in r.lamps)
                {
                    if (!lp.glow) continue; bool on; Color col; float sz = lp.size;
                    switch (lp.g)
                    {
                        case G.Head: on = headLit; col = hc * (1.5f * headK + .2f) * (NL ? r.headSm * (1 + .6f * r.highSm) : 1); sz *= 1 + headK * .4f; break;
                        case G.Tail: on = brake || tail; col = new Color(1, .05f, .03f) * (brake ? 2.1f : .75f); sz *= brake ? 1.15f : .8f; break;
                        case G.Rev: on = rev; col = new Color(1, 1, 1) * 1.3f; break;
                        case G.IndL: on = bl; col = new Color(1, .45f, .05f) * 2f; break;
                        default: on = br2; col = new Color(1, .45f, .05f) * 2f; break;
                    }
                    on &= vis && aliveSide[(int)lp.g * 2 + lp.side] && (!lp.dead || lp.flickOn); if (!on) { if (lp.glow.gameObject.activeSelf) lp.glow.gameObject.SetActive(false); continue; }
                    var wp = lp.glow.position; var to = camPos - wp; float dist = to.magnitude; var wn = r.shell.TransformDirection(lp.n);
                    float face = Mathf.Clamp01((Vector3.Dot(wn, to / Mathf.Max(1e-3f, dist)) - .05f) / .55f);   // só de frente para a lente
                    float far = NL ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(18, 38, dist)) : 1;   // r65: corona só de longe (de perto brilha a lente)
                    if (face <= .01f || far <= .01f) { if (lp.glow.gameObject.activeSelf) lp.glow.gameObject.SetActive(false); continue; }
                    if (!lp.glow.gameObject.activeSelf) lp.glow.gameObject.SetActive(true);
                    if (NL) { sz *= .4f; col *= far; }
                    lp.glow.rotation = Quaternion.LookRotation(-to); lp.glow.localScale = Vector3.one * sz * Mathf.Clamp(dist / 16, 1, 2.6f) * (.55f + .45f * face) / Mathf.Max(.01f, r.shell.lossyScale.x);
                    var mr = lp.glow.GetComponent<MeshRenderer>(); mpb.SetColor("_BaseColor", col * face * (dark ? 1 : .55f)); mr.SetPropertyBlock(mpb);
                }
                r.headL = aliveSide[0]; r.headR = aliveSide[1];
                if (headLit && r.hasHead && vis && dark && (r.headL || r.headR)) near.Add((r, isPlayer ? -1 : d2, true));
                if (NL && dark && r.hasPlate && r.headSm > .5f && d2 < 45 * 45) plateNear.Add((r, d2));
                if (dark && r.hasTail && (brake || tail) && d2 < 70 * 70 && (aliveSide[2] || aliveSide[3])) brakeNear.Add((r, d2, brake ? 1 : .25f));
                if (rev && d2 < 60 * 60) revNear.Add((r, d2));
            }
            // fachos: 2 spots por carro, os mais perto (o do jogador primeiro)
            int bi = 0;
            // r45: quem já tinha facho no quadro anterior fica com ele até outro carro chegar 30 % mais perto (antes, carros a distâncias
            // parecidas trocavam os fachos/luzes do pool a cada quadro = farol e freio piscando "sozinhos")
            var beamOrder = near.OrderBy(x => x.d2 * (x.r.hadBeam ? .5f : 1)).ToList(); foreach (var x in rigs.Values) x.hadBeam = false;
            foreach (var (r, _, _) in beamOrder)
            {
                foreach (var (p, okSide) in new[] { (r.hl, r.headL), (r.hr, r.headR) })
                {
                    if (!okSide) continue;                                                       // farol daquele lado quebrado: sem facho
                    if (bi >= beams.Count) break; if (bi < beamOwner.Length) { if (beamOwner[bi] != null && beamOwner[bi] != r) BeamSwaps++; beamOwner[bi] = r; } var b = beams[bi++]; b.enabled = true; r.hadBeam = true; b.transform.position = r.shell.TransformPoint(p) + r.car.transform.forward * .12f;
                    if (NL)
                    {   // r65: baixo ↔ alto (alcance, abertura, inclinação e máscara); entra em rampa quando o facho troca de dono
                        float h = r.highSm; r.beamK = Mathf.MoveTowards(r.beamK, 1, dt * 5);
                        b.transform.rotation = r.car.transform.rotation * Quaternion.Euler(Mathf.Lerp(4.5f, 1.3f, h), p.x < 0 ? Mathf.Lerp(-2.5f, -1, h) : Mathf.Lerp(2.5f, 1, h), 0);
                        b.intensity = Mathf.Lerp(55, 95, h) * headK * r.headSm * Mathf.SmoothStep(0, 1, r.beamK);
                        b.range = Mathf.Lerp(42, 90, h); b.spotAngle = Mathf.Lerp(62, 48, h); b.innerSpotAngle = Mathf.Lerp(26, 18, h); var ck = h > .5f ? hiCookie : cookie; if (b.cookie != ck) b.cookie = ck;
                    }
                    else { b.transform.rotation = r.car.transform.rotation * Quaternion.Euler(4.5f, p.x < 0 ? -2.5f : 2.5f, 0); b.intensity = 55 * headK; }
                    var tint = r.car.headTint; b.useColorTemperature = tint == null; b.color = tint ?? Color.white;
                }
                if (bi >= beams.Count) break;
            }
            BeamsOn = bi; for (; bi < beams.Count; bi++) { if (beams[bi]) beams[bi].enabled = false; if (bi < beamOwner.Length) beamOwner[bi] = null; }
            if (NL) foreach (var x in rigs.Values) if (!x.hadBeam) x.beamK = 0;
            // r65: luz de placa nos 3 mais perto
            if (NL)
            {
                int pi = 0; var plOrder = plateNear.OrderBy(x => x.d2 * (x.r.hadPlate ? .5f : 1)).ToList(); foreach (var x in rigs.Values) x.hadPlate = false;
                foreach (var (r, _) in plOrder)
                {
                    if (pi >= platePool.Count) break; var l = platePool[pi++]; l.enabled = true; r.hadPlate = true;
                    l.transform.position = r.shell.TransformPoint(r.plate + new Vector3(0, .1f, -.07f)); l.intensity = .45f * nightK * r.headSm;
                }
                for (; pi < platePool.Count; pi++) platePool[pi].enabled = false;
            }
            // freio/lanterna iluminando o chão atrás
            int ti = 0;
            var tailOrder = brakeNear.OrderBy(x => x.d2 * (x.r.hadTail ? .5f : 1)).ToList(); foreach (var x in rigs.Values) x.hadTail = false;
            foreach (var (r, _, k) in tailOrder)
            {
                // spot atrás da lente olhando para trás e para baixo: ilumina chão/parede/carro de trás, nunca a própria tampa (o ponto antigo, a 45 cm "locais", tingia a traseira e fazia a mancha branca de reflexo)
                if (ti >= tailPool.Count) break; var l = tailPool[ti++]; l.enabled = true; r.hadTail = true; var mid = (r.bl + r.br) / 2; var ct = r.car.transform;
                l.transform.SetPositionAndRotation(r.shell.TransformPoint(mid) - ct.forward * .14f - ct.up * .04f, ct.rotation * Quaternion.Euler(30, 180, 0)); l.intensity = (k > .5f ? 4.2f : 1.1f) * nightK + .2f;
            }
            for (; ti < tailPool.Count; ti++) tailPool[ti].enabled = false;
            int ri = 0;
            var revOrder = revNear.OrderBy(x => x.d2 * (x.r.hadRev ? .5f : 1)).ToList(); foreach (var x in rigs.Values) x.hadRev = false;
            foreach (var (r, _) in revOrder)
            {
                if (ri >= revPool.Count) break; var l = revPool[ri++]; l.enabled = true; r.hadRev = true; var mid = (r.bl + r.br) / 2; l.transform.position = r.shell.TransformPoint(mid) - r.car.transform.forward * .14f;
                l.transform.rotation = r.car.transform.rotation * Quaternion.Euler(28, 180, 0); l.intensity = 2.6f + 4 * nightK;
            }
            for (; ri < revPool.Count; ri++) revPool[ri].enabled = false;
        }
        static void SetLens(Rig r, G g, int side, bool on, Color col)
        {
            var mr = r.lens[(int)g * 2 + side]; if (!mr) return; if (mr.enabled != on) { mr.enabled = on; LensToggles++; } if (!on) return;
            mpb.SetColor("_BaseColor", col); mr.SetPropertyBlock(mpb);
        }
    }
}
