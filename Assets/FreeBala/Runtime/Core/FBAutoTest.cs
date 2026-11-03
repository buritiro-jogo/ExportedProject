// Autoteste do executável (validação dos builds sem clicar): FreeBala.exe -fbauto <mapa> <modo> <segundos> [-logFile arquivo]
// Abre o mapa no modo pedido, deixa a partida rodar, grava no log o estado (jogador, bots, FPS médio) e fecha. Vários pares mapa/modo:
// -fbauto vila_jardim:tatico,praca_aguas:squad 20
// -fbq <0..3|all>: roda cada mapa no nível de qualidade pedido (all = BAIXO, MÉDIO, ALTO, ULTRA) e grava uma linha por rodada em
// autoteste/bench.csv (fps, p50/p95/p99, pior quadro, memória, triângulos, shadow casters, batches, SetPass, casters cortados, occlusion).
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBAutoTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbauto"); if (i < 0 || i + 1 >= a.Length) return;
            var g = new GameObject("FB_AutoTeste"); DontDestroyOnLoad(g); var t = g.AddComponent<FBAutoTest>();
            foreach (var p in a[i + 1].Split(',')) { var kv = p.Split(':'); t.jobs.Add((kv[0], kv.Length > 1 ? kv[1] : "tatico")); }
            t.show = System.Array.IndexOf(a, "-fbshow") >= 0;
            int ski = System.Array.IndexOf(a, "-fbskin"); if (ski >= 0 && ski + 1 < a.Length) t.skins = a[ski + 1].Split(',');   // -fbskin r_bz,r_aka_junin: repete com cada personagem
            int pli = System.Array.IndexOf(a, "-fbsim"); if (pli >= 0 && pli + 1 < a.Length) t.sims = a[pli + 1].Split(',');   // -fbsim pc,android: repete com o perfil de cada plataforma
            int qi = System.Array.IndexOf(a, "-fbq"); if (qi >= 0 && qi + 1 < a.Length) t.levels = a[qi + 1] == "all" ? new[] { 0, 1, 2, 3 } : int.TryParse(a[qi + 1], out var q) ? new[] { Mathf.Clamp(q, 0, 3) } : t.levels;
            if (i + 2 < a.Length && float.TryParse(a[i + 2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s)) t.secs = s;
        }
        readonly List<(string map, string mode)> jobs = new(); float secs = 15; bool show; int[] levels = { -1 }; string[] sims = { "" }, skins = { "" };   // -1 = nível atual (sem -fbq)
        IEnumerator Start()
        {
            string skin0 = skins.Length > 1 || skins[0] != "" ? PlayerPrefs.GetString("fb_skin", FBSession.DefaultSkin) : null;
            Application.logMessageReceived += (c, st, ty) => { if (ty == LogType.Exception || ty == LogType.Error) errors++; };
            var shots = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste"); System.IO.Directory.CreateDirectory(shots);
            yield return new WaitForSeconds(4); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(shots, "menu.png")); yield return null; yield return null;
            Debug.Log("[FreeBala][auto] rig do menu: " + Rig(GameObject.Find("Previa")));
            int keepQ = FBQuality.Choice;
            foreach (var (map, mode) in jobs) foreach (var sk in skins) foreach (var sim in sims) foreach (var q in levels)
            {
                if (sim != "") FBQuality.SimulatePlatform(System.Enum.TryParse<FBPlat>(sim, true, out var sp) && sp != FBPlat.PC ? sp : (FBPlat?)null);
                if (q >= 0) FBQuality.Set(q); var qn = (FBQuality.Simulated ? FBQuality.Device.plat + "_" : "") + FBQuality.Names[(int)FBQuality.Level];
                GameData.Load(); FBSession.Load(); FBSession.map = map; FBSession.mode = mode; FBSession.online = false; if (sk != "") { FBSession.skin = sk; FBSession.look = ""; FBSession.pickRandom = false; } if (show) FBSession.view = "tps"; FBSession.Save(); errors = 0;
                Debug.Log($"[FreeBala][auto] carregando {map}/{mode} · qualidade {qn}");
                var op = SceneManager.LoadSceneAsync("Map_" + map); if (op == null) { Debug.Log($"[FreeBala][auto] FALHA: cena Map_{map} não existe"); continue; }
                while (!op.isDone) yield return null;
                for (float w = 0; w < 4; w += Time.unscaledDeltaTime) yield return null;   // aquecimento (shaders, streaming)
                var rec = new[] { "Main Thread", "Render Thread", "Batches Count", "SetPass Calls Count", "Triangles Count", "Shadow Casters Count", "Visible Skinned Meshes Count" }
                    .Select(n => (n, r: ProfilerRecorder.StartNew(n.Contains("Thread") ? ProfilerCategory.Internal : ProfilerCategory.Render, n, 300))).ToList();
                float t = 0; int frames = 0; float worst = 0; var ft = new List<float>(4096); long vegSum = 0; int vegMax = 0;
                int arIn = 0, arN = 0; float arMax = 0, arT = 0;   // r47: área de combate (Clássico/4X4): vivos dentro dela, amostrados a cada 0,5 s
                Loco.Clear(); while (t < secs) { yield return null; t += Time.unscaledDeltaTime; frames++; LocoSample(); worst = Mathf.Max(worst, Time.unscaledDeltaTime); ft.Add(Time.unscaledDeltaTime * 1000); vegSum += FBQuality.VegVisibleInstances; vegMax = Mathf.Max(vegMax, FBQuality.VegVisibleInstances);
                    if (FBMatch.I && FBMatch.I.arena is FBArena ar0 && (arT += Time.unscaledDeltaTime) > .5f) { arT = 0; foreach (var x in FBMatch.Actors()) { if (!x || !x.alive) continue; arN++; float d = (x.Pos - ar0.c).Flat().magnitude; if (d <= ar0.r) arIn++; arMax = Mathf.Max(arMax, d - ar0.r); } } }
                if (FBMatch.I && FBMatch.I.arena is FBArena ar1) Debug.Log($"[FreeBala][auto] {map}/{mode} área de combate: raio {ar1.r:0} m · {ar1.why} · vivos dentro {100f * arIn / Mathf.Max(1, arN):0.0}% de {arN} amostras · pior fora {Mathf.Max(0, arMax):0.0} m · zona {(FBMatch.I.mode?.Zone is FBZone.Z z1 ? $"centro {(z1.c - ar1.c).Flat().magnitude:0.0} m do da área, raio {z1.r:0} m" : "-")}");
                else if (FBMatch.I) Debug.Log($"[FreeBala][auto] {map}/{mode} área de combate: nenhuma (modo {mode})");
                vegAvg = frames > 0 ? (int)(vegSum / frames) : 0; vegPeak = vegMax;
                var avg = new Dictionary<string, double>();
                var stats = string.Join(" · ", rec.Select(x => { double v = 0; int n = x.r.Count; for (int k = 0; k < n; k++) v += x.r.GetSample(k).Value; v /= System.Math.Max(1, n); x.r.Dispose(); avg[x.n] = v; return x.n.Contains("Thread") ? $"{x.n} {v / 1e6:0.0} ms" : $"{x.n} {v:0}"; }));
                Bench(shots, map, mode, qn, ft, frames / Mathf.Max(.01f, t), avg);
                if (FBPlayer.I && FBMatch.Player) { float y0 = FBMatch.Player.Pos.y, top = y0; FBPlayer.I.TestJump(); for (float w = 0; w < 1.2f; w += Time.deltaTime) { yield return null; top = Mathf.Max(top, FBMatch.Player.Pos.y); } Debug.Log($"[FreeBala][auto] {map}/{mode} pulo: subiu {top - y0:0.00} m"); }
                Debug.Log($"[FreeBala][auto] {map}/{mode} rig do jogador: " + Rig(FBMatch.Player ? FBMatch.Player.gameObject : null) + $" · chão {(FBMatch.Player ? FBMatch.Player.grounded.ToString() : "-")}");
                Debug.Log($"[FreeBala][auto] {map}/{mode} desempenho: {stats}"); LocoReport(map + "/" + mode); Debug.Log($"[FreeBala][auto] {map}/{mode} bots: {FBBot.Diag()} · reanimações iniciadas {FBBot.ReviveStarts} · com inimigo a < 22 m do caído {FBBot.ReviveHot} · adiadas por perigo {FBBot.ReviveUnsafeBlocks} (checagens: tiro recebido {FBBot.ReviveWhy[0]}, trocando tiro {FBBot.ReviveWhy[1]}, disparo perto {FBBot.ReviveWhy[2]}, inimigo < 22 m {FBBot.ReviveWhy[3]}, na mira {FBBot.ReviveWhy[4]})" + (FBMatch.I?.mode is FBModes.Tatico tt ? " · " + tt.OrdersDiag() : "")); FBBot.ReviveStarts = FBBot.ReviveHot = FBBot.ReviveUnsafeBlocks = 0; System.Array.Clear(FBBot.ReviveWhy, 0, 5);
                Debug.Log($"[FreeBala][auto] custo corpo: medição {FBCharacter.MeasureMs:0.0} ms em {FBCharacter.MeasureN}x · afastamento da arma {FBRig.ClearMs / System.Math.Max(1, frames):0.00} ms/quadro ({FBRig.ClearN} chamadas)"); FBCharacter.MeasureMs = 0; FBCharacter.MeasureN = 0; FBRig.ClearMs = 0; FBRig.ClearN = 0;
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(shots, levels[0] >= 0 ? $"{map}_{mode}_{qn}.png" : $"{map}_{mode}.png")); yield return null; yield return null;
                var P = FBMatch.Player; int bots = 0, alive = 0; foreach (var b in FBMatch.Bots) if (b) { bots++; if (b.alive) alive++; }
                Debug.Log($"[FreeBala][auto] {map}/{mode}: partida {(FBMatch.I ? "ok" : "NÃO montou")} · jogador {(P ? P.Pos.ToString() : "nenhum")} · bots {alive}/{bots} · {frames / Mathf.Max(.01f, t):0} FPS (pior quadro {worst * 1000:0} ms) · erros {errors}");
                if (show) yield return Showcase(shots, map + "_" + mode + (sk != "" ? "_" + sk : ""));
            }
            if (sims[0] != "") FBQuality.SimulatePlatform(null);
            if (levels[0] >= 0) FBQuality.Set(keepQ);
            if (skin0 != null) { PlayerPrefs.SetString("fb_skin", skin0); PlayerPrefs.Save(); }   // -fbskin não muda o personagem escolhido pelo jogador
            Debug.Log("[FreeBala][auto] fim"); Application.Quit(0);
        }
        int errors, vegAvg, vegPeak;
        // rodada 38 ("a caminhada com armas está muito lenta, parecendo câmera lenta"): ritmo real do passo (LocoRate = velocidade no chão ÷
        // velocidade natural dos clipes que estão tocando) de todo personagem ARMADO andando, por clipe dominante da camada base; no piso
        // (0,7) o clipe é natural mais rápido do que o boneco anda -> passo arrastado
        static readonly Dictionary<string, (int n, int lo, int hi, double sum, double g)> Loco = new(); static readonly List<AnimatorClipInfo> ci = new();
        static void LocoSample()
        {
            foreach (var c in FBMatch.All)
            {
                if (!c || !c.alive || c.UnarmedLocomotion || !c.grounded || c.vehiclePose || c.sys != null || !c.an) continue; float g = new Vector2(c.velocity.x, c.velocity.z).magnitude; if (g < .6f) continue;
                c.an.GetCurrentAnimatorClipInfo(0, ci); string best = "-"; float bw = 0; foreach (var x in ci) if (x.weight > bw && x.clip) { bw = x.weight; best = x.clip.name; }
                string key = (c.isPlayer ? "jogador " : "bot ") + (c.CombatStance ? "combate " : "patrulha ") + best;
                Loco.TryGetValue(key, out var v); Loco[key] = (v.n + 1, v.lo + (c.LocoRate <= .705f ? 1 : 0), v.hi + (c.LocoRate >= 1.445f ? 1 : 0), v.sum + c.LocoRate, v.g + g);
            }
        }
        static void LocoReport(string tag)
        {
            foreach (var kv in Loco.OrderByDescending(k => k.Value.n).Take(14))
            { var v = kv.Value; Debug.Log($"[FreeBala][auto] {tag} ritmo armado · {kv.Key}: {v.n} amostras · ritmo médio {v.sum / v.n:0.00} · no piso 0,7: {100.0 * v.lo / v.n:0}% · no teto 1,45: {100.0 * v.hi / v.n:0}% · v média {v.g / v.n:0.0} m/s"); }
        }
        // uma linha por rodada em autoteste/bench.csv (mesmas colunas iniciais do teste PlayMode Benchmark_Mapas)
        void Bench(string dir, string map, string mode, string q, List<float> ft, float fps, Dictionary<string, double> avg)
        {
            try
            {
                if (ft.Count == 0) return; ft.Sort(); float P(float x) => ft[Mathf.Clamp((int)(x * ft.Count), 0, ft.Count - 1)];
                double A(string k) => avg.TryGetValue(k, out var v) ? v : 0; var cam = Camera.main; var ic = System.Globalization.CultureInfo.InvariantCulture;
                var path = System.IO.Path.Combine(dir, "bench.csv"); const string H = "data;plataforma;gpu;mapa;modo;nivel;fps;p50_ms;p95_ms;p99_ms;pior_ms;memoria_mb;main_ms;render_ms;triangulos;shadow_casters;batches;setpass;skinned;casters_cortados;occlusion;erros;veg_visiveis_media;veg_visiveis_pico;veg_total;veg_teto;perfil";
                if (System.IO.File.Exists(path) && System.IO.File.ReadLines(path).FirstOrDefault() != H) System.IO.File.Move(path, System.IO.Path.Combine(dir, $"bench_antigo_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv"));   // colunas mudaram
                bool head = !System.IO.File.Exists(path);
                var line = string.Join(";", System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"), Application.platform, SystemInfo.graphicsDeviceName, map, mode, q,
                    fps.ToString("0", ic), P(.5f).ToString("0.0", ic), P(.95f).ToString("0.0", ic), P(.99f).ToString("0.0", ic), ft[^1].ToString("0", ic),
                    UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576, (A("Main Thread") / 1e6).ToString("0.0", ic), (A("Render Thread") / 1e6).ToString("0.0", ic),
                    A("Triangles Count").ToString("0", ic), A("Shadow Casters Count").ToString("0", ic), A("Batches Count").ToString("0", ic), A("SetPass Calls Count").ToString("0", ic),
                    A("Visible Skinned Meshes Count").ToString("0", ic), FBQuality.LastCulledCasters, cam && cam.useOcclusionCulling ? 1 : 0, errors, vegAvg, vegPeak, FBQuality.VegTotalInstances, FBQuality.VegMaxInstances, FBQuality.Device.plat);
                System.IO.File.AppendAllText(path, (head ? H + "\n" : "") + line + "\n");
                Debug.Log("[FreeBala][auto][bench] " + line);
            }
            catch (System.Exception e) { Debug.LogWarning("[FreeBala][auto] bench.csv: " + e.Message); }
        }
        // vitrine (-fbshow): câmera própria de lado/3-4 para conferir arma na mão, coldre, escala e o rig no carro e na moto
        IEnumerator Showcase(string dir, string tag)
        {
            var me = FBMatch.Player; if (!me) yield break;
            var cg = new GameObject("AutoCam"); var cam = cg.AddComponent<Camera>(); cam.depth = 100; if (Camera.main) cam.cullingMask = Camera.main.cullingMask;   // mesma visão da câmera do jogo (sem a camada da 1ª pessoa)
            cam.fieldOfView = 38; cam.nearClipPlane = .05f;                                                // (estava dentro do comentário: a vitrine usava FOV 60)
            IEnumerator Shot(Transform tgt, Vector3 localDir, float dist, float h, string name)
            {
                for (int k = 0; k < 3; k++) yield return null;
                var c = tgt.position + Vector3.up * h; cam.transform.position = c + tgt.rotation * localDir.normalized * dist; cam.transform.LookAt(c);
                yield return null; ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"show_{tag}_{name}.png")); yield return null; yield return null;
            }
            yield return Shot(me.transform, new Vector3(1, .1f, .25f), 3.2f, 1.0f, "jogador_lado");
            yield return Shot(me.transform, new Vector3(-.5f, .15f, -1), 3.2f, 1.0f, "jogador_costas");
            yield return Shot(me.transform, new Vector3(.4f, .1f, 1), 3.2f, 1.0f, "jogador_frente");
            // armas guardadas: pistola na mão → fuzil nas costas, SMG no peito (bandoleira); confere se atravessam o corpo
            var wu = FBPlayer.I ? FBPlayer.I.wu : null;
            if (wu)
            {
                wu.Give(FBSession.primary, FBSession.smg, FBSession.secondary); wu.Equip(2, true);
                for (float t = 0; t < 1.2f; t += Time.unscaledDeltaTime) yield return null;
                yield return Shot(me.transform, new Vector3(-.5f, .15f, -1), 2.6f, 1.15f, "coldre_costas");
                yield return Shot(me.transform, new Vector3(1, .1f, .25f), 2.6f, 1.15f, "coldre_lado");
                yield return Shot(me.transform, new Vector3(.4f, .1f, 1), 2.6f, 1.15f, "coldre_frente");
                wu.Equip(0, true);
            }
            var an = me.an; var head = an ? an.GetBoneTransform(HumanBodyBones.Head) : null;
            foreach (var w in me.GetComponentsInChildren<Transform>(true).Where(x => x.name.StartsWith("Coldre_") || x.name.StartsWith("Arma_")))
            {
                var rs = w.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue; var inv = w.worldToLocalMatrix; var lb = new Bounds(); bool first = true;
                foreach (var r in rs) { var bb = r.bounds; for (int k = 0; k < 8; k++) { var q = inv.MultiplyPoint3x4(new Vector3((k & 1) == 0 ? bb.min.x : bb.max.x, (k & 2) == 0 ? bb.min.y : bb.max.y, (k & 4) == 0 ? bb.min.z : bb.max.z)); if (first) { lb = new Bounds(q, Vector3.zero); first = false; } else lb.Encapsulate(q); } }
                var sz = Vector3.Scale(lb.size, w.lossyScale);
                Debug.Log($"[FreeBala][auto] vitrine {tag}: {w.name} ativo {w.gameObject.activeInHierarchy} · escala {w.lossyScale.x:0.000} · caixa local {sz.x:0.00}×{sz.y:0.00}×{sz.z:0.00} m · cabeça {(head ? head.position.y - me.transform.position.y : 0):0.00} m · peças: {string.Join(",", rs.Select(r => r.name).Distinct().Take(14))}");
            }
            foreach (var car in FBVehicles.list.Where(c => c && Vector3.Distance(c.transform.position, me.Pos) < 30).Take(4))
            {
                float low = 1e9f; foreach (var w in car.wheels) if (w.obj) { var rs = w.obj.GetComponentsInChildren<Renderer>(); foreach (var r in rs) low = Mathf.Min(low, r.bounds.min.y); }
                var c0 = car.transform.position; float g = float.NaN; foreach (var h in Physics.RaycastAll(c0 + Vector3.up * 3, Vector3.down, 10, FBWorld.SolidMask).OrderBy(h => h.distance)) if (!h.transform.IsChildOf(car.transform)) { g = h.point.y; break; }
                Debug.Log($"[FreeBala][auto] vitrine {tag}: carro {car.id} · pneu mais baixo {low - g:0.00} m acima do chão · origem {c0.y - g:0.00} m");
            }
            var bot = FBMatch.Bots.FirstOrDefault(b => b && b.alive);
            if (bot) { var bc = bot.GetComponent<FBCharacter>(); if (bc) { bc.transform.position = me.Pos + me.transform.right * 2.2f; yield return Shot(bc.transform, new Vector3(1, .1f, .3f), 3.2f, 1.0f, "bot_lado"); } }
            foreach (var vid in new[] { "S10_2017", "xj6", "ViaturaPMAzul", "ViaturaBM" })
            {
                var pos = me.Pos + me.transform.forward * 6; var car = FBVehicles.Spawn(vid, pos, me.transform.eulerAngles.y); if (!car) { Debug.Log($"[FreeBala][auto] vitrine: veículo {vid} não nasceu"); continue; }
                yield return null; bool ok = FBDrive.Enter(me, car); for (float w = 0; w < 1.5f; w += Time.deltaTime) yield return null;
                Debug.Log($"[FreeBala][auto] vitrine {tag}: entrou em {vid} {ok}");
                yield return Shot(car.transform, new Vector3(-1, .25f, .15f), 4.2f, .9f, vid + "_lado");
                if (car.kind != "moto") { for (int k = 0; k < 3; k++) yield return null; var seat = car.L2W(car.seat); cam.transform.position = car.L2W(car.seat + new Vector3(1.05f, .45f, .35f)); cam.transform.LookAt(seat + car.transform.up * .35f); cam.fieldOfView = 60; yield return null; ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"show_{tag}_{vid}_cabine.png")); yield return null; yield return null; cam.fieldOfView = 38; }
                yield return Shot(car.transform, new Vector3(-.6f, .5f, 1), 4.6f, .9f, vid + "_frente");
                FBDrive.Exit(me, true); for (float w = 0; w < .6f; w += Time.deltaTime) yield return null;
                FBVehicles.list.Remove(car); Destroy(car.gameObject); yield return null;
            }
            Destroy(cg);
        }
        static string Rig(GameObject g)
        {
            if (!g) return "nenhum"; var an = g.GetComponentInChildren<Animator>(); if (!an || !an.isHuman) return "sem humanoide";
            string B(HumanBodyBones b) { var t = an.GetBoneTransform(b); return t ? (t.position.y - g.transform.position.y).ToString("0.00") : "-"; }
            var ci = an.GetCurrentAnimatorClipInfo(0); var rig = g.GetComponent<UnityEngine.Animations.Rigging.RigBuilder>();
            return $"quadril {B(HumanBodyBones.Hips)} · pé {B(HumanBodyBones.LeftFoot)} · cabeça {B(HumanBodyBones.Head)} · clipe {(ci.Length > 0 ? ci[0].clip.name : "-")} · culling {an.cullingMode} · rigBuilder {(rig ? rig.enabled.ToString() : "-")} · escala {g.transform.lossyScale.y:0.00} · humanScale {an.humanScale:0.00}";
        }
    }
}
