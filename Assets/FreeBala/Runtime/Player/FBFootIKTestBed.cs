// Cena de teste do foot IK (menu "FreeBala/Testes/Cena de Foot IK" gera Scenes/IKTest.unity; o teste PlayMode monta a mesma pista
// em tempo de execução). Pista em faixas paralelas, todas em metros reais e na camada do mapa:
//   meio-fio (andar com um pé em cada altura e cruzar) · escada (degrau 18 cm) · rampas 12°/22° · morro · terreno inclinado de lado ·
//   pedras · obstáculos de 8 a 40 cm (os de 60/90 cm ficam ao lado da rota).
// Caminhantes em rota (FBIKWalker, mesma física do jogador) com velocidades de bot e de jogador. Câmera segue um deles.
// Teclas: Tab = próximo personagem · F = liga/desliga foot IK (comparar) · L = liga/desliga foot lock · T = tronco.
// FBFootIKMeter mede contato pé × chão, deslizamento na fase de apoio e tremor do quadril (mostrado na tela e usado pelo teste).
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public class FBFootIKTestBed : MonoBehaviour
    {
        public string skin; public bool spawnOnStart = true;
        [System.NonSerialized] public readonly List<FBCharacter> actors = new();
        [System.NonSerialized] public readonly Dictionary<FBCharacter, FBFootIKMeter> meters = new();
        int focus; Camera cam; float camYaw;
        [System.NonSerialized] public bool sideView;                     // prints: câmera de lado, parada no personagem, altura das pernas
        public int Focus { get => focus; set => focus = Mathf.Clamp(value, 0, Mathf.Max(0, actors.Count - 1)); }

        public class Route { public string name, slug; public Vector2[] pts; public float speed, pause = 1.2f; }
        public static readonly Route[] Routes = {
            new Route { slug = "meiofio", name = "meio-fio (um pé em cada altura)", pts = new[] { new Vector2(.06f, 3), new Vector2(.06f, 36) }, speed = 1.6f },
            new Route { slug = "cruza_meiofio", name = "cruza meio-fio", pts = new[] { new Vector2(-2, 5), new Vector2(2, 9), new Vector2(-2, 13), new Vector2(2, 17), new Vector2(-2, 21) }, speed = 3f },
            new Route { slug = "escada", name = "escada andando", pts = new[] { new Vector2(6, 1), new Vector2(6, 19.5f) }, speed = 1.6f, pause = 2 },
            new Route { slug = "escada_correndo", name = "escada correndo (jogador)", pts = new[] { new Vector2(7.2f, 1), new Vector2(7.2f, 19.5f) }, speed = 5f },
            new Route { slug = "rampas", name = "rampas 12°/22°", pts = new[] { new Vector2(12, 1), new Vector2(12, 22) }, speed = 2.2f },
            new Route { slug = "morro", name = "morro", pts = new[] { new Vector2(18, 4), new Vector2(18, 36) }, speed = 2.5f },
            new Route { slug = "lado15", name = "terreno de lado 15°", pts = new[] { new Vector2(26, 6.5f), new Vector2(26, 18.5f) }, speed = 1.6f, pause = 2 },
            new Route { slug = "lado25", name = "terreno de lado 25°", pts = new[] { new Vector2(26, 22.5f), new Vector2(26, 33) }, speed = 1.4f, pause = 2 },
            new Route { slug = "pedras", name = "pedras", pts = new[] { new Vector2(33, 3), new Vector2(33, 36) }, speed = 1.6f },
            new Route { slug = "obstaculos", name = "obstáculos 8–40 cm (jogador)", pts = new[] { new Vector2(40, 2), new Vector2(40, 36) }, speed = 5f },
        };

        // ---------------- pista
        public static GameObject BuildCourse()
        {
            var old = GameObject.Find("PistaIK"); if (old) { if (Application.isPlaying) Destroy(old); else DestroyImmediate(old); }
            var root = new GameObject("PistaIK"); root.layer = FBWorld.LMap;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mats = new Dictionary<string, Material>();
            Material M(string k, Color c) { if (!mats.TryGetValue(k, out var m)) { m = shader ? new Material(shader) { name = "PistaIK_" + k, color = c } : null; mats[k] = m; } return m; }
            GameObject Prim(PrimitiveType t, string name, Vector3 pos, Vector3 size, Vector3 euler, Color c, Transform parent = null)
            {
                var g = GameObject.CreatePrimitive(t); g.name = name; g.layer = FBWorld.LMap; g.transform.SetParent(parent ? parent : root.transform, false);
                g.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler)); g.transform.localScale = size; g.isStatic = true;
                var r = g.GetComponent<Renderer>(); var m = M(c.ToString(), c); if (r && m) r.sharedMaterial = m;
                return g;
            }
            GameObject Box(string n, Vector3 pos, Vector3 size, Vector3 euler, Color c, Transform p = null) => Prim(PrimitiveType.Cube, n, pos, size, euler, c, p);
            GameObject Lane(string n) { var g = new GameObject(n); g.layer = FBWorld.LMap; g.transform.SetParent(root.transform, false); return g; }
            Color asfalto = new(.22f, .22f, .24f), calcada = new(.62f, .6f, .56f), concreto = new(.55f, .53f, .5f), terra = new(.45f, .36f, .25f), pedra = new(.4f, .39f, .37f), madeira = new(.5f, .36f, .22f);

            Box("chao", new Vector3(20, -.25f, 20), new Vector3(110, .5f, 90), Vector3.zero, asfalto);
            // 0 · meio-fio: calçada de 15 cm à esquerda da linha x = 0
            { var l = Lane("MeioFio").transform; Box("calcada", new Vector3(-1.5f, .075f, 20), new Vector3(3, .15f, 40), Vector3.zero, calcada, l); }
            // 1 · escada: 10 degraus de 18 × 30 cm, patamar de 3 m, 10 degraus descendo
            {
                var l = Lane("Escada").transform; const float rise = .18f, run = .3f; int n = 10; float z0 = 4, x = 6.6f;
                for (int k = 0; k < n; k++) { float top = rise * (k + 1); Box("sobe" + k, new Vector3(x, top / 2, z0 + run * (k + .5f)), new Vector3(3, top, run), Vector3.zero, concreto, l); }
                float zt = z0 + run * n, ht = rise * n; Box("patamar", new Vector3(x, ht / 2, zt + 1.5f), new Vector3(3, ht, 3), Vector3.zero, concreto, l);
                for (int k = 0; k < n; k++) { float top = ht - rise * (k + 1); if (top <= 0) continue; Box("desce" + k, new Vector3(x, top / 2, zt + 3 + run * (k + .5f)), new Vector3(3, top, run), Vector3.zero, concreto, l); }
            }
            // 2 · rampas: sobe 12°, plano, desce 22°
            {
                var l = Lane("Rampas").transform; float x = 12, a1 = 12, L1 = 8, a2 = 22, th = .4f;
                float h1 = L1 * Mathf.Sin(a1 * Mathf.Deg2Rad), d1 = L1 * Mathf.Cos(a1 * Mathf.Deg2Rad), z0 = 3;
                var r1 = Box("rampa12", Vector3.zero, new Vector3(3, th, L1), new Vector3(-a1, 0, 0), concreto, l);
                r1.transform.position = new Vector3(x, h1 / 2, z0 + d1 / 2) - r1.transform.up * th / 2;
                Box("topo", new Vector3(x, h1 / 2, z0 + d1 + 1.5f), new Vector3(3, h1, 3), Vector3.zero, concreto, l);
                float L2 = h1 / Mathf.Sin(a2 * Mathf.Deg2Rad), d2 = L2 * Mathf.Cos(a2 * Mathf.Deg2Rad), zs = z0 + d1 + 3;
                var r2 = Box("rampa22", Vector3.zero, new Vector3(3, th, L2), new Vector3(a2, 0, 0), concreto, l);
                r2.transform.position = new Vector3(x, h1 / 2, zs + d2 / 2) - r2.transform.up * th / 2;
            }
            // 3 · morro (elipsoide com colisor de malha — esfera não escala desigual)
            {
                var l = Lane("Morro").transform; var g = Prim(PrimitiveType.Sphere, "morro", new Vector3(18, -1.2f, 20), new Vector3(9, 7, 30), Vector3.zero, terra, l);
                var sc = g.GetComponent<SphereCollider>(); if (sc) { if (Application.isPlaying) Destroy(sc); else DestroyImmediate(sc); }
                g.AddComponent<MeshCollider>().sharedMesh = g.GetComponent<MeshFilter>().sharedMesh;
            }
            // 4 · terreno inclinado de lado (15° e 25°)
            {
                var l = Lane("Inclinado").transform;
                Box("lado15", new Vector3(26, .55f, 12.5f), new Vector3(6, .6f, 15), new Vector3(0, 0, 15), terra, l);
                Box("lado25", new Vector3(26, 1.1f, 27.5f), new Vector3(6, .6f, 13), new Vector3(0, 0, 25), terra, l);
            }
            // 5 · pedras (semente fixa)
            {
                var l = Lane("Pedras").transform; var rnd = new System.Random(7); float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
                for (int k = 0; k < 40; k++)
                {
                    float s = R(.18f, .55f); var p = new Vector3(33 + R(-1.1f, 1.1f), R(-.12f, .02f) * s / .3f, R(4, 35));
                    if (k % 3 == 0) Prim(PrimitiveType.Sphere, "pedra" + k, p, Vector3.one * s, Vector3.zero, pedra, l);
                    else Box("pedra" + k, p, new Vector3(s, s * R(.35f, .7f), s * R(.6f, 1.2f)), new Vector3(R(-25, 25), R(0, 360), R(-25, 25)), pedra, l);
                }
            }
            // 6 · obstáculos: na rota 8–40 cm (sobe pelo stepOffset), ao lado 60 e 90 cm
            {
                var l = Lane("Obstaculos").transform; float[] hs = { .08f, .15f, .25f, .32f, .4f };
                for (int k = 0; k < hs.Length; k++) Box("obst" + (int)(hs[k] * 100) + "cm", new Vector3(40, hs[k] / 2, 6 + k * 6), new Vector3(2.2f, hs[k], k % 2 == 0 ? .5f : 1.6f), Vector3.zero, madeira, l);
                Box("lado60cm", new Vector3(42.4f, .3f, 12), new Vector3(1.2f, .6f, 1.2f), Vector3.zero, madeira, l);
                Box("lado90cm", new Vector3(42.4f, .45f, 24), new Vector3(1.2f, .9f, 1.2f), Vector3.zero, madeira, l);
            }
            if (!Object.FindAnyObjectByType<Light>()) { var lg = new GameObject("Sol"); var li = lg.AddComponent<Light>(); li.type = LightType.Directional; li.intensity = 1.2f; lg.transform.rotation = Quaternion.Euler(50, -30, 0); }
            return root;
        }

        // ---------------- personagens
        public static FBCharacter SpawnWalker(string skin, Route r)
        {
            var reg = FBRegistry.I; if (!reg) return null;
            if (string.IsNullOrEmpty(skin) || !reg.HasChar(skin)) skin = reg.HasChar(FBSession.DefaultSkin) ? FBSession.DefaultSkin : reg.charIds.Count > 0 ? reg.charIds[0] : null;
            var pf = skin != null ? reg.Char(skin) : null; if (!pf) return null;
            var pts = new Vector3[r.pts.Length];
            for (int k = 0; k < pts.Length; k++) { float x = r.pts[k].x, z = r.pts[k].y; pts[k] = new Vector3(x, FBWorld.GroundY(x, 6, z, 2, 12) ?? 0, z); }
            var fw = (pts[1] - pts[0]).Flat(); var go = Instantiate(pf, pts[0] + Vector3.up * .1f, Quaternion.LookRotation(fw.sqrMagnitude > 1e-4f ? fw : Vector3.forward));
            go.name = "IK_" + r.name;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (!t.GetComponent<FBHitbox>()) t.gameObject.layer = FBWorld.LChar;
            var ch = go.GetOrAdd<FBCharacter>(); ch.skinId = skin; ch.displayName = r.name;
            // mesmo CharacterController do FBMatch.Spawn
            var cc = go.AddComponent<CharacterController>(); float s = 1f / go.transform.lossyScale.y;
            float H = Json.F(GameData.Section("controller"), "m_Height", 1.8f), R = Json.F(GameData.Section("controller"), "m_Radius", .3f);
            cc.height = H * s; cc.radius = Mathf.Max(R, .32f) * s; cc.center = new Vector3(0, H * .5f * s, 0); cc.stepOffset = .45f * s; cc.slopeLimit = 50; cc.skinWidth = .04f * s; cc.minMoveDistance = 0;
            // sem câmera renderizando (teste em lote) o Animator em modo de culling não reescreve os ossos e o IK realimenta a própria saída
            var an = go.GetComponent<Animator>(); if (an) an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var w = go.AddComponent<FBIKWalker>(); w.route = pts; w.speed = r.speed; w.pause = r.pause;
            return ch;
        }
        public void SpawnAll()
        {
            foreach (var a in actors) if (a) Destroy(a.gameObject); actors.Clear(); meters.Clear();
            Physics.SyncTransforms();                                                     // pista recém-criada já responde aos raios do chão
            foreach (var r in Routes) { var c = SpawnWalker(skin, r); if (c) { actors.Add(c); meters[c] = new FBFootIKMeter(); } }
            if (actors.Count == 0) Debug.LogWarning("[FreeBala][ik] sem personagens importados (FBRegistry) — rode Importar tudo no projeto principal");
        }

        // ---------------- -fbiktest: FreeBala.exe -fbiktest [segundos por rota] → <build>/autoteste/ik_<rota>_on_1.png, _on_2.png, _off.png
        // e ik_relatorio.txt (medições por rota). Carrega a cena IKTest se estiver no build; senão monta a pista numa cena vazia. Fecha no fim.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbiktest"); if (i < 0) return;
            float secs = 4; if (i + 1 < a.Length) float.TryParse(a[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out secs);
            var g = new GameObject("FB_IKAutoTeste"); DontDestroyOnLoad(g); g.AddComponent<AutoShots>().secs = Mathf.Max(2, secs);
        }
        class AutoShots : MonoBehaviour
        {
            public float secs = 4;
            System.Collections.IEnumerator Start()
            {
                var dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste"); System.IO.Directory.CreateDirectory(dir);
                if (Application.CanStreamedLevelBeLoaded("IKTest")) { var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("IKTest"); while (!op.isDone) yield return null; }
                else { var sc = UnityEngine.SceneManagement.SceneManager.CreateScene("IKTest_auto"); UnityEngine.SceneManagement.SceneManager.SetActiveScene(sc); foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false; foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.enabled = false; }   // menu continua carregado: some da tela
                var bed = FindAnyObjectByType<FBFootIKTestBed>(); if (!bed) bed = new GameObject("TesteFootIK").AddComponent<FBFootIKTestBed>();
                yield return null; yield return null;
                var log = new System.Text.StringBuilder(); Debug.Log($"[FreeBala][iktest] {bed.actors.Count} caminhantes · nível {FBQuality.Level}");
                for (float w = 0; w < 3; w += Time.unscaledDeltaTime) yield return null;               // aterrissar, aquecer shaders
                bed.sideView = true;
                for (int k = 0; k < bed.actors.Count && k < Routes.Length; k++)
                {
                    var a = bed.actors[k]; if (!a) continue; bed.Focus = k; var r = Routes[k]; bed.meters[a].Reset();
                    FBQuality.FootIK = true;
                    for (float w = 0; w < secs * .5f; w += Time.unscaledDeltaTime) yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"ik_{r.slug}_on_1.png"));
                    for (float w = 0; w < secs * .5f; w += Time.unscaledDeltaTime) yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"ik_{r.slug}_on_2.png"));
                    string rep = $"{r.slug} ({r.name}): {bed.meters[a].Report()} · {(a.feet ? a.feet.DebugText() : "-")}";
                    FBQuality.FootIK = false; for (int f = 0; f < 20; f++) yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"ik_{r.slug}_off.png"));
                    FBQuality.FootIK = true; Debug.Log("[FreeBala][iktest] " + rep); log.AppendLine(rep);
                }
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "ik_relatorio.txt"), log.ToString());
                yield return null; yield return null;
                if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbauto") < 0) Application.Quit(0);
            }
        }

        void Start()
        {
            GameData.Load();
            if (!GameObject.Find("PistaIK")) BuildCourse();
            cam = Camera.main; if (!cam) { cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; }
            if (spawnOnStart) SpawnAll();
        }
        // [parceiro] v1 (thread de física): tempo do Update/LateUpdate medido pelo FBTempo (desligado no jogo: só um teste de bool)
        static readonly int __fbt_Update = FBTempo.Id("FBFootIKTestBed.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.tabKey.wasPressedThisFrame && actors.Count > 0) focus = (focus + 1) % actors.Count;
                if (kb.fKey.wasPressedThisFrame) FBQuality.FootIK = !FBQuality.FootIK;
                if (kb.lKey.wasPressedThisFrame) FBFootIK.Q.Lock = !FBFootIK.Q.Lock;
                if (kb.tKey.wasPressedThisFrame) FBFootIK.Q.Tilt = !FBFootIK.Q.Tilt;
            }
            foreach (var kv in meters) kv.Value.Sample(kv.Key, Time.deltaTime);
        }
        static readonly int __fbt_LateUpdate = FBTempo.Id("FBFootIKTestBed.LateUpdate");   // [parceiro] FBTempo (ver Update)
        void LateUpdate()
        {
            if (!FBTempo.On) { LateUpdate__fbt(); return; }
            long t0 = FBTempo.T0(); LateUpdate__fbt(); FBTempo.Soma(__fbt_LateUpdate, t0);
        }
        void LateUpdate__fbt()
        {
            if (!cam || actors.Count == 0) return; var a = actors[Mathf.Clamp(focus, 0, actors.Count - 1)]; if (!a) return;
            if (sideView) { var sp = a.Pos + Vector3.up * .6f; cam.transform.position = sp + Quaternion.Euler(4, a.Yaw - 90, 0) * new Vector3(0, 0, -3f); cam.transform.LookAt(sp); return; }
            camYaw = Mathf.LerpAngle(camYaw, a.Yaw + 60, 1 - Mathf.Exp(-Time.deltaTime * 2));     // de lado, 3/4, na altura das pernas
            var piv = a.Pos + Vector3.up * .7f; var off = Quaternion.Euler(12, camYaw, 0) * new Vector3(0, 0, -3.4f);
            cam.transform.position = Vector3.Lerp(cam.transform.position, piv + off, 1 - Mathf.Exp(-Time.deltaTime * 6)); cam.transform.LookAt(piv);
        }
        void OnGUI()
        {
            if (actors.Count == 0) { GUI.Label(new Rect(10, 10, 800, 24), "Sem personagens: rode FreeBala/Importar tudo no projeto principal."); return; }
            var a = actors[Mathf.Clamp(focus, 0, actors.Count - 1)]; if (!a) return;
            string m = meters.TryGetValue(a, out var mt) ? mt.Report() : "";
            GUI.Box(new Rect(8, 8, 760, 92), "");
            GUI.Label(new Rect(14, 12, 750, 22), $"[{focus + 1}/{actors.Count}] {a.displayName} · IK {(FBQuality.FootIK ? "ON" : "OFF")} (F) · trava {(FBFootIK.Q.Lock ? "ON" : "OFF")} (L) · tronco {(FBFootIK.Q.Tilt ? "ON" : "OFF")} (T) · Tab = próximo");
            GUI.Label(new Rect(14, 34, 750, 22), a.feet ? a.feet.DebugText() : "sem FBFootIK");
            GUI.Label(new Rect(14, 56, 750, 40), m);
        }
    }

    // medição: contato pé × chão (pé apoiado), deslizamento com o pé travado e tremor do quadril (2ª diferença da altura por quadro)
    public class FBFootIKMeter
    {
        public readonly List<float> contact = new(), slip = new(), jitter = new(); public int edgeSkips;
        readonly Vector3[] prevA = new Vector3[2]; readonly float[] prevLock = new float[2]; float y1, y2; int hipsN;
        static readonly RaycastHit[] hits = new RaycastHit[6];
        public void Reset() { contact.Clear(); slip.Clear(); jitter.Clear(); edgeSkips = 0; hipsN = 0; prevLock[0] = prevLock[1] = 0; }
        public void Sample(FBCharacter ch, float dt)
        {
            if (!ch || !ch.feet || dt <= 0) return; var ik = ch.feet;
            if (ik.Weight < .9f) { hipsN = 0; prevLock[0] = prevLock[1] = 0; return; }
            for (int k = 0; k < 2; k++)
            {
                var f = k == 0 ? ik.Left : ik.Right; var a = f.Ankle;
                if (f.Hit && f.Plant > .85f && f.RestH > 0)
                {
                    // chão medido por conta própria (raio fino no XZ do tornozelo); quina de degrau/pedra (raio ≠ SphereCast do IK) não entra
                    if (Ray(ch.transform, a, out float gy)) { if (Mathf.Abs(gy - f.GroundY) > .04f * ch.ScaleK) edgeSkips++; else contact.Add(a.y - f.RestH / Mathf.Max(.8f, f.NormalS.y) - gy); }   // mesma altura de sola do alvo (inclinação)
                }
                if (f.LockW >= .99f && prevLock[k] >= .99f) slip.Add((a - prevA[k]).Flat().magnitude / dt);
                prevA[k] = a; prevLock[k] = f.LockW;
            }
            if (ch.Hips) { float y = ch.Hips.position.y; if (hipsN >= 2) jitter.Add(Mathf.Abs(y - 2 * y1 + y2)); y2 = y1; y1 = y; hipsN++; }
        }
        static bool Ray(Transform self, Vector3 a, out float y)
        {
            y = 0; int n = Physics.RaycastNonAlloc(new Vector3(a.x, a.y + .5f, a.z), Vector3.down, hits, 1.5f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore); float bd = 1e9f;
            for (int i = 0; i < n; i++) { var h = hits[i]; if (!h.collider || h.collider.transform.IsChildOf(self) || h.distance >= bd) continue; bd = h.distance; y = h.point.y; }
            return bd < 1e9f;
        }
        public static float P(List<float> l, float q, bool abs = false)
        {
            if (l.Count == 0) return 0; var c = new List<float>(l.Count); foreach (var v in l) c.Add(abs ? Mathf.Abs(v) : v); c.Sort();
            return c[Mathf.Clamp(Mathf.RoundToInt(q * (c.Count - 1)), 0, c.Count - 1)];
        }
        public string Report() =>
            $"contato p95 {P(contact, .95f, true) * 100:0.0} cm (pen. p05 {P(contact, .05f) * 100:+0.0;-0.0} cm, flut. p95 {P(contact, .95f) * 100:+0.0;-0.0} cm, n {contact.Count}, quinas {edgeSkips}) · " +
            $"desliza p95 {P(slip, .95f) * 100:0.0} cm/s (n {slip.Count}) · tremor quadril p95 {P(jitter, .95f) * 1000:0.0} mm/q² máx {P(jitter, 1) * 1000:0.0}";
    }
}
