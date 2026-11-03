// Água dos mapas: troca os materiais de água importados (lâmina "PA_water", rio "OR_rio", brejo "PT_brejo", jatos "PA_jet") pelos
// materiais-modelo de Resources/Water (shaders FreeBala/Water, WaterJet, WaterDrops), dá FLUXO ao rio (direção pelo eixo maior da
// malha), monta as FONTES de verdade a partir de level.fountains (jatos, coroa de respingos, anéis na lâmina e névoa — uma malha
// calculada na GPU), liga a variante leve/profundidade conforme a qualidade, a luz noturna das fontes e o som de água.
// Chamado pelo FBWaterRuntime (componente posto na cena pelo FBWaterSetup no import, ou criado sozinho ao carregar o mapa).
// Coordenadas de level.* estão no espaço do web: x invertido (como em FBWorld.SetWater).
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FreeBala
{
    public static class FBWater
    {
        public enum Kind { None, Pool, River, Swamp, Jet }

        // ---- classificação pelo nome do material (e do objeto): exclui caixa d'água, espelho de carro, vidro, grama da lagoa
        // [parceiro 01/11] "(?<!agua_)": "agua_espelho..." (espelho d'água) é lâmina de água, não espelho de carro
        static readonly Regex rxNot = new(@"tank|caixa|carmetal|(?<!agua_)espelhos?(\.|$|_[^d])|retrovis|glass|vidro|grama|grass|lawn", RegexOptions.IgnoreCase);
        // [parceiro] BURITIRO: nomes que têm "agua", "rio" ou "piscina" mas não são água (cor verde-água, areia do rio, azulejo e tinta
        // azul-piscina, mural, reboco, fibra, piso de borracha). Sem isto a areia da praia da Orla (A2_areia_rio_branco) e as paredes
        // verde-água do Centro viravam água no jogo.
        static readonly Regex rxNaoAgua = new(@"verde[_ ]?[aá]gua|azul[_ ]?piscina|azulejo|areia|sand|barro|capim|mural|reboco|plaster|fibra|epdm|tinta|paint", RegexOptions.IgnoreCase);
        // [parceiro] nome original -> material de água trocado, da última troca na cena inteira (para o teste conferir)
        public static readonly Dictionary<string, string> Trocados = new();
        /// <summary>[parceiro] true se o nome tem palavra de água mas é outra coisa (verde-água, areia do rio...). Usado também pelo FBImpactFx.</summary>
        public static bool NaoEAgua(string nome) => !string.IsNullOrEmpty(nome) && rxNaoAgua.IsMatch(nome);
        static readonly Regex rxJet = new(@"(^|_)jets?($|_|\.)|jato", RegexOptions.IgnoreCase);
        static readonly Regex rxRiver = new(@"(^|_)rio($|_|\.)|river|igarap", RegexOptions.IgnoreCase);
        static readonly Regex rxSwamp = new(@"brejo|lagoa|lake|pantano|p[aâ]ntano|pond", RegexOptions.IgnoreCase);
        static readonly Regex rxPool = new(@"(^|_)water($|_|\.)|(^|_)agua($|_|\.)|espelho_d|piscina|pool|chafariz|fonte_agua", RegexOptions.IgnoreCase);
        public static Kind Classify(string matName, string objName = null)
        {
            if (string.IsNullOrEmpty(matName)) return Kind.None;
            if (matName.StartsWith("FBWater_")) return matName switch { "FBWater_Rio" => Kind.River, "FBWater_Brejo" => Kind.Swamp, "FBWater_Jato" => Kind.Jet, "FBWater_Fonte" => Kind.Pool, _ => Kind.None };
            var n = matName.Replace(" (Instance)", "");
            if (rxNot.IsMatch(n) || rxNaoAgua.IsMatch(n)) return Kind.None;   // [parceiro] + falsos "agua/rio/piscina"
            if (rxJet.IsMatch(n)) return Kind.Jet; if (rxRiver.IsMatch(n)) return Kind.River; if (rxSwamp.IsMatch(n)) return Kind.Swamp; if (rxPool.IsMatch(n)) return Kind.Pool;
            return Kind.None;
        }
        public static string MaterialName(Kind k) => k switch { Kind.Pool => "FBWater_Fonte", Kind.River => "FBWater_Rio", Kind.Swamp => "FBWater_Brejo", Kind.Jet => "FBWater_Jato", _ => null };
        static readonly Dictionary<Kind, Material> mats = new();
        public static Material Template(Kind k)
        {
            if (k == Kind.None) return null; if (mats.TryGetValue(k, out var m) && m) return m;
            m = Resources.Load<Material>("Water/" + MaterialName(k)); mats[k] = m; return m;
        }

        /// <summary>Troca os materiais de água dos renderers sob root (null = cena inteira). Devolve quantos trocou.</summary>
        public static int SwapMaterials(GameObject root, System.Func<Kind, Material> pick = null)
        {
            pick ??= Template; int n = 0;
            if (!root) Trocados.Clear();   // [parceiro] a lista vale para a última troca na cena inteira
            var rs = root ? root.GetComponentsInChildren<MeshRenderer>(true) : Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var r in rs)
            {
                var sm = r.sharedMaterials; bool ch = false;
                for (int i = 0; i < sm.Length; i++)
                {
                    if (!sm[i]) continue; var k = Classify(sm[i].name, r.name); if (k == Kind.None) continue;
                    var t = pick(k); if (!t || sm[i] == t) continue;
                    Trocados[sm[i].name] = t.name;   // [parceiro] registra a troca para o teste/log
                    sm[i] = t; ch = true;
                }
                if (!ch) continue; r.sharedMaterials = sm; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; n++;
            }
            return n;
        }

        // ---- rio: direção do fluxo pelo eixo maior (PCA dos vértices no XZ; sem malha legível, pela caixa)
        static readonly int idFlow = Shader.PropertyToID("_Flow");
        public static Vector2 FlowDir(Renderer r)
        {
            var mf = r.GetComponent<MeshFilter>(); var m = mf ? mf.sharedMesh : null;
            if (m && m.isReadable && m.vertexCount > 2)
            {
                var v = m.vertices; var M = r.transform.localToWorldMatrix; int step = Mathf.Max(1, v.Length / 4000); double sx = 0, sz = 0, n = 0;
                for (int i = 0; i < v.Length; i += step) { var w = M.MultiplyPoint3x4(v[i]); sx += w.x; sz += w.z; n++; }
                double cx = sx / n, cz = sz / n, xx = 0, zz = 0, xz = 0;
                for (int i = 0; i < v.Length; i += step) { var w = M.MultiplyPoint3x4(v[i]); double dx = w.x - cx, dz = w.z - cz; xx += dx * dx; zz += dz * dz; xz += dx * dz; }
                float ang = .5f * Mathf.Atan2((float)(2 * xz), (float)(xx - zz)); return new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            }
            var b = r.bounds.size; return b.x >= b.z ? Vector2.right : Vector2.up;
        }
        static void SetupRivers(List<Renderer> rivers)
        {
            var mpb = new MaterialPropertyBlock();
            foreach (var r in rivers)
            {
                var d = FlowDir(r); var tpl = Template(Kind.River); var f = tpl ? tpl.GetVector(idFlow) : new Vector4(0, 1, .55f, 1);
                r.GetPropertyBlock(mpb); mpb.SetVector(idFlow, new Vector4(d.x, d.y, f.z, f.w)); r.SetPropertyBlock(mpb);
            }
        }

        // ---- qualidade: variante leve (sem 2ª camada, agitação e espuma) e profundidade (só com depth texture no URP)
        public static void ApplyQuality(int level)
        {
            if (level <= 0) Shader.EnableKeyword("FB_WATER_LITE"); else Shader.DisableKeyword("FB_WATER_LITE");
            var a = (QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            bool depth = level > 0 && a && a.supportsCameraDepthTexture;
            if (depth) Shader.EnableKeyword("FB_WATER_DEPTH"); else Shader.DisableKeyword("FB_WATER_DEPTH");
        }

        // ---- fontes (level.fountains = [x, y, z, altura, dx, dy, dz, raio] no espaço do web)
        const float G = 9.8f;
        public struct Jet { public Vector3 p, dir; public float h, r; }
        public static List<Jet> LastJets = new(); public static UnityEngine.SceneManagement.Scene JetsScene;          // jatos da cena atual (refletores coloridos do FBAccentLights)
        public static List<Jet> ReadJets(Dictionary<string, object> level)
        {
            var list = new List<Jet>(); var F = level != null ? Json.L(level, "fountains") : null; if (F == null) return list;
            foreach (var o in F)
            {
                if (o is not List<object> f || f.Count < 8) continue;
                float X(int i) => Json.Fv(f[i]);
                var d = new Vector3(-X(4), X(5), X(6)); if (d.sqrMagnitude < 1e-6f) d = Vector3.up;
                list.Add(new Jet { p = new Vector3(-X(0), X(1), X(2)), h = Mathf.Max(.2f, X(3)), dir = d.normalized, r = X(7) });
            }
            return list;
        }

        class MB { public readonly List<Vector3> pos = new(); public readonly List<Vector4> u0 = new(), u1 = new(), u2 = new(); public readonly List<int> idx = new(); public Bounds b; public bool hasB; }
        static void Quad(MB m, Vector3 basePos, int kind, Vector3 dir, float speed, float life, float size, float hue, float reach)
        {
            int v0 = m.pos.Count; float seed = Random.value;
            for (int i = 0; i < 4; i++)
            {
                m.pos.Add(basePos); m.u0.Add(new Vector4(i % 2 == 0 ? -.5f : .5f, i < 2 ? -.5f : .5f, kind, seed));
                m.u1.Add(new Vector4(dir.x, dir.y, dir.z, speed)); m.u2.Add(new Vector4(life, size, hue, 0));
            }
            m.idx.AddRange(new[] { v0, v0 + 2, v0 + 1, v0 + 2, v0 + 3, v0 + 1 });
            var bb = new Bounds(basePos, Vector3.one * (reach * 2 + size * 2)); if (m.hasB) m.b.Encapsulate(bb); else { m.b = bb; m.hasB = true; }
        }
        /// <summary>Malha das fontes (gotas, coroa, anéis, névoa). mul = densidade pela qualidade (0,35 … 1).</summary>
        public static Mesh BuildFountains(List<Jet> jets, float mul)
        {
            if (jets.Count == 0) return null; var m = new MB(); var st = Random.state; Random.InitState(4242);
            foreach (var j in jets)
            {
                float ay = Mathf.Max(.2f, j.dir.y), v = Mathf.Sqrt(2 * G * j.h / (ay * ay)); int n = Mathf.Max(4, Mathf.RoundToInt(Mathf.Min(70, 18 + j.h * 13) * mul));
                float spr = .03f + j.r * .25f, reach = j.h + v;
                for (int i = 0; i < n; i++)                                      // gotas do jato
                {
                    var d = (j.dir + new Vector3((Random.value - .5f) * spr, (Random.value - .5f) * spr * .3f, (Random.value - .5f) * spr)).normalized;
                    float sp = v * Random.Range(.9f, 1.02f), vy = d.y * sp, life = Mathf.Max(.25f, (vy + Mathf.Sqrt(vy * vy + 2 * G * .05f)) / G);
                    Quad(m, j.p, 0, d, sp, life, Random.Range(.035f, .07f) + j.r * .35f, 0, reach);
                }
                // ponto de queda: jato vertical cai na base; arco cai à frente
                var land = ay > .9f ? j.p : j.p + new Vector3(j.dir.x, 0, j.dir.z).normalized * (v * v * 2 * ay * Mathf.Sqrt(Mathf.Max(0, 1 - ay * ay)) / G);
                int nc = Mathf.Max(2, Mathf.RoundToInt(n * .4f));
                for (int i = 0; i < nc; i++)                                     // coroa de respingos
                {
                    float a = Random.value * Mathf.PI * 2, e = Random.Range(.5f, .95f); var d = new Vector3(Mathf.Cos(a), e, Mathf.Sin(a)).normalized;
                    float sp = Random.Range(.8f, 2.1f), vy = d.y * sp; Quad(m, land, 0, d, sp, Mathf.Max(.15f, 2 * vy / G + .05f), Random.Range(.025f, .05f), 0, 1);
                }
                int nr = mul < .5f ? 1 : 3;
                for (int i = 0; i < nr; i++) Quad(m, land + new Vector3(Random.Range(-.08f, .08f), 0, Random.Range(-.08f, .08f)), 1, Vector3.up, 0, Random.Range(.9f, 1.4f), Random.Range(.35f, .6f) + j.r * 2, 0, 1);
                if (j.h > 1.6f && mul >= .5f) Quad(m, land, 2, new Vector3(j.dir.x, 0, j.dir.z) * .3f, 0, Random.Range(2.2f, 3.2f), Mathf.Min(2.2f, .6f + j.h * .25f), 0, 2);
            }
            Random.state = st;
            var mesh = new Mesh { name = "FontesAgua", indexFormat = m.pos.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(m.pos); mesh.SetUVs(0, m.u0); mesh.SetUVs(1, m.u1); mesh.SetUVs(2, m.u2); mesh.SetTriangles(m.idx, 0, false); mesh.bounds = m.b;
            return mesh;
        }

        // ---- montagem no mapa
        public class State { public GameObject drops; public List<Vector3> clusters = new(); public List<Renderer> rivers = new(); public float sndT; }
        public static State Setup(Dictionary<string, object> level)
        {
            FBFxQuality.Ensure(); ApplyQuality(FBFxQuality.Level);
            var s = new State(); SwapMaterials(null);
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                foreach (var m in r.sharedMaterials) if (m && Classify(m.name) == Kind.River) { s.rivers.Add(r); break; }
            SetupRivers(s.rivers);
            var jets = ReadJets(level); LastJets = jets; JetsScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (jets.Count > 0)
            {
                float mul = FBFxQuality.Level <= 0 ? .35f : FBFxQuality.Level == 1 ? .6f : 1;
                var mesh = BuildFountains(jets, mul); var mat = Resources.Load<Material>("Water/FBWater_Gotas");
                if (mesh && mat)
                {
                    s.drops = new GameObject("FB_FontesAgua"); s.drops.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = s.drops.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
                    mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false; mr.lightProbeUsage = LightProbeUsage.Off; mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
                // grupos (6 m) para o som
                var cells = new Dictionary<Vector2Int, (Vector3 sum, int n)>();
                foreach (var j in jets) { var k = new Vector2Int(Mathf.FloorToInt(j.p.x / 6), Mathf.FloorToInt(j.p.z / 6)); cells.TryGetValue(k, out var c); cells[k] = (c.sum + j.p, c.n + 1); }
                foreach (var c in cells.Values) s.clusters.Add(c.sum / c.n);
            }
            Debug.Log($"[FreeBala] água: {jets.Count} jatos, {s.rivers.Count} rio(s), variante {(Shader.IsKeywordEnabled("FB_WATER_LITE") ? "leve" : "completa")}{(Shader.IsKeywordEnabled("FB_WATER_DEPTH") ? " + profundidade" : "")}"
                + $" · materiais trocados: {string.Join("; ", System.Linq.Enumerable.Select(Trocados, t => t.Key + " -> " + t.Value))}");   // [parceiro] lista das trocas no log
            return s;
        }

        static readonly int idNight = Shader.PropertyToID("_FBNight");
        public static void Tick(State s, float dt)
        {
            Shader.SetGlobalFloat(idNight, FBWeather.NightK);
            if (s == null || (s.sndT -= dt) > 0) return; s.sndT = .4f;
            var cam = FBFxQuality.Cam; if (!cam) return; var cp = cam.transform.position;
            // som: grupo de jatos mais perto e ponto mais perto do rio (clipes amb_fonte / amb_rio — mudos até existirem)
            if (s.clusters.Count > 0)
            {
                Vector3 best = s.clusters[0]; float bd = 1e9f; foreach (var c in s.clusters) { float d = (c - cp).sqrMagnitude; if (d < bd) { bd = d; best = c; } }
                FBAudio.SetLoop("agua_fonte", "amb_fonte", Mathf.Clamp01(1.2f - Mathf.Sqrt(bd) / 45) * .55f, best + Vector3.up, null, 5);
            }
            if (s.rivers.Count > 0)
            {
                Vector3 best = cp; float bd = 1e9f; foreach (var r in s.rivers) { if (!r) continue; var q = r.bounds.ClosestPoint(cp); float d = (q - cp).sqrMagnitude; if (d < bd) { bd = d; best = q; } }
                FBAudio.SetLoop("agua_rio", "amb_rio", Mathf.Clamp01(1.2f - Mathf.Sqrt(bd) / 60) * .5f, best, null, 8);
            }
        }
        public static void Stop() { FBAudio.StopLoop("agua_fonte"); FBAudio.StopLoop("agua_rio"); }
    }
}
