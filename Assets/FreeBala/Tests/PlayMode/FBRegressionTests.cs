// Agente 9: regressões dos bugs já corrigidos (cena carrega, pés no chão, pulo, carro no chão, arma única na mão), troca de qualidade
// sem erro, rede de verdade contra o server.mjs local (dois clientes) e benchmark por mapa × nível (CSV em unity/logs/bench.csv).
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FreeBala.Tests
{
    public class FBRegressionTests
    {
        static string LogDir { get { var d = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../logs")); System.IO.Directory.CreateDirectory(d); return d; } }
        static IEnumerator Load(string map, string mode, float settle, string view = "tps")
        {
            FBSession.Load(); FBSession.map = map; FBSession.mode = mode; FBSession.online = false; FBSession.view = view; FBSession.diff = 1;
            if (FBRegistry.I && FBRegistry.I.HasChar("r_tutu")) FBSession.skin = "r_tutu"; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_" + map); Assert.IsNotNull(op, $"cena Map_{map} não está no build"); while (!op.isDone) yield return null;
            for (float t = 0; t < settle; t += Time.unscaledDeltaTime) yield return null;
        }
        static float Ground(Vector3 p, Transform ignore)
        {
            foreach (var h in Physics.RaycastAll(p + Vector3.up * 1.5f, Vector3.down, 6, FBWorld.SolidMask).OrderBy(h => h.distance)) if (!ignore || !h.transform.IsChildOf(ignore)) return h.point.y;
            return float.NaN;
        }

        // ---- cena carrega + pés no chão + pulo + arma única (bugs de 5b7d761 / 356c467) ----
        static IEnumerator Personagem(string map)
        {
            FBMatch.God = true;                                                          // bots podem atirar: o teste é do corpo, não do combate
            try { yield return PersonagemCorpo(map); } finally { FBMatch.God = false; }
        }
        static IEnumerator PersonagemCorpo(string map)
        {
            yield return Load(map, map == "estande" ? "treino" : "classico", 3);
            var P = FBMatch.Player; Assert.IsNotNull(FBMatch.I, $"{map}: partida não montou"); Assert.IsNotNull(P, $"{map}: sem jogador");
            var an = P.an; Assert.IsTrue(an && an.isHuman, $"{map}: jogador sem humanoide");
            Assert.IsTrue(P.grounded, $"{map}: jogador não está no chão");
            float g = Ground(P.Pos, P.transform); Assert.IsFalse(float.IsNaN(g), $"{map}: sem chão embaixo do jogador");
            float foot = Mathf.Min(an.GetBoneTransform(HumanBodyBones.LeftFoot).position.y, an.GetBoneTransform(HumanBodyBones.RightFoot).position.y) - g;
            float hips = an.GetBoneTransform(HumanBodyBones.Hips).position.y - g;
            Debug.Log($"[FreeBala][regressao] {map}: pé {foot:0.00} m · quadril {hips:0.00} m · escala {P.transform.lossyScale.y:0.00}");
            Assert.That(foot, Is.InRange(-.12f, .3f), $"{map}: pé fora do chão (afundado/flutuando)");
            Assert.Greater(hips, .5f, $"{map}: quadril baixo demais (personagem afundado)");
            // arma única: no máximo um modelo "Arma_" ativo na mão (coldres "Coldre_" são separados) e nenhum duplicado do mesmo id
            var armas = P.GetComponentsInChildren<Transform>(false).Where(t => t.name.StartsWith("Arma_")).ToList();
            Assert.LessOrEqual(armas.Count, 1, $"{map}: {armas.Count} armas na mão: {string.Join(",", armas.Select(a => a.name))}");
            // pulo
            float y0 = P.Pos.y, top = y0; FBPlayer.I.TestJump(); for (float t = 0; t < 1.2f; t += Time.deltaTime) { yield return null; top = Mathf.Max(top, P.Pos.y); }
            Debug.Log($"[FreeBala][regressao] {map}: pulo {top - y0:0.00} m");
            Assert.Greater(top - y0, .35f, $"{map}: pulo não subiu"); for (float t = 0; t < 1.5f && !P.grounded; t += Time.deltaTime) yield return null;
            Assert.IsTrue(P.grounded, $"{map}: não voltou ao chão depois do pulo"); Assert.Less(Mathf.Abs(P.Pos.y - y0), 1.5f, $"{map}: caiu/atravessou o chão no pulo");
        }
        [UnityTest, Timeout(120000)] public IEnumerator Regressao_Personagem_Estande() { yield return Personagem("estande"); }
        [UnityTest, Timeout(120000)] public IEnumerator Regressao_Personagem_VilaJardim() { yield return Personagem("vila_jardim"); }
        [UnityTest, Timeout(120000)] public IEnumerator Regressao_Personagem_PracaAguas() { yield return Personagem("praca_aguas"); }
        [UnityTest, Timeout(120000)] public IEnumerator Regressao_Personagem_PracaPT() { yield return Personagem("praca_pt"); }
        [UnityTest, Timeout(120000)] public IEnumerator Regressao_Personagem_Orla() { yield return Personagem("orla"); }

        // ---- carros no chão (pneu encostando, nem enterrado nem flutuando) ----
        [UnityTest, Timeout(120000)] public IEnumerator Regressao_CarrosNoChao()
        {
            yield return Load("praca_aguas", "role", 3);
            var cars = FBVehicles.list.Where(c => c && !c.driver).Take(12).ToList(); Assert.Greater(cars.Count, 0, "sem carros");
            var bad = new List<string>();
            foreach (var car in cars)
            {
                float low = 1e9f; foreach (var w in car.wheels) if (w.obj) foreach (var r in w.obj.GetComponentsInChildren<Renderer>()) low = Mathf.Min(low, r.bounds.min.y);
                if (low > 1e8f) continue; float g = Ground(car.transform.position, car.transform); if (float.IsNaN(g)) continue;
                float gap = low - g; Debug.Log($"[FreeBala][regressao] carro {car.id}: pneu {gap:0.00} m do chão");
                if (gap > .2f || gap < -.2f) bad.Add($"{car.id} {gap:0.00}");
            }
            Assert.IsEmpty(bad, "carros fora do chão: " + string.Join(", ", bad));
        }

        // ---- troca de qualidade: aplica sem exceção, LOD bias muda, evento dispara, sombras de props pequenos caem ----
        [UnityTest, Timeout(120000)] public IEnumerator Qualidade_TrocaDeNivel()
        {
            yield return Load("vila_jardim", "treino", 2); int keep = FBQuality.Choice, changed = 0; System.Action on = () => changed++; FBQuality.Changed += on;
            try
            {
                var bias = new float[4]; var culled = new int[4];
                for (int i = 0; i < 4; i++) { FBQuality.Set(i); yield return null; yield return null; bias[i] = QualitySettings.lodBias; culled[i] = FBQuality.LastCulledCasters; Assert.AreEqual((FBQLevel)i, FBQuality.Level); }
                Debug.Log($"[FreeBala][regressao] qualidade: lodBias {string.Join("/", bias.Select(b => b.ToString("0.0")))} · props sem sombra {string.Join("/", culled)}");
                Assert.AreEqual(4, changed, "FBQuality.Changed não disparou"); Assert.Less(bias[0], bias[3]); Assert.GreaterOrEqual(culled[0], culled[3], "nível baixo deveria tirar mais sombras");
            }
            finally { FBQuality.Changed -= on; FBQuality.Set(keep); }
        }

        // ---- benchmark por mapa × nível (não falha: grava CSV para comparar antes/depois) ----
        // ---- benchmark por mapa × perfil (PC e Android simulado) × nível; não falha: grava CSV para comparar antes/depois ----
        // Triângulos/batches/SetPass vêm do ProfilerRecorder (editor e build de desenvolvimento); instâncias de vegetação do contador do FBQuality.
        const string BenchHead = "data;plataforma;gpu;mapa;perfil;nivel;fps;p50_ms;p95_ms;p99_ms;pior_ms;memoria_mb;triangulos;batches;setpass;shadow_casters;veg_visiveis_media;veg_visiveis_pico;veg_total;veg_teto";
        [UnityTest, Timeout(1800000), Category("Benchmark")] public IEnumerator Benchmark_Mapas()
        {
            int keep = FBQuality.Choice; var csv = new System.Text.StringBuilder(); var path = System.IO.Path.Combine(LogDir, "bench.csv");
            if (System.IO.File.Exists(path) && System.IO.File.ReadLines(path).FirstOrDefault() != BenchHead) System.IO.File.Move(path, System.IO.Path.Combine(LogDir, $"bench_antigo_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv"));
            if (!System.IO.File.Exists(path)) csv.AppendLine(BenchHead);
            try
            {
                foreach (var map in new[] { "praca_aguas", "vila_jardim", "praca_pt", "orla" })
                    foreach (var plat in new FBPlat?[] { null, FBPlat.Android })
                    {
                        if (plat != null && map != "praca_aguas" && map != "vila_jardim") continue;   // perfil Android só nos mapas com vegetação nova
                        FBQuality.SimulatePlatform(plat);
                        for (int q = 0; q < 4; q++)
                        {
                            FBQuality.Set(q); yield return Load(map, "tatico", 3);
                            var rec = new[] { "Triangles Count", "Batches Count", "SetPass Calls Count", "Shadow Casters Count" }.Select(n => Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, n, 600)).ToArray();
                            var ft = new List<float>(); long veg = 0; int vegPeak = 0;
                            for (float t = 0; t < 8; t += Time.unscaledDeltaTime) { yield return null; ft.Add(Time.unscaledDeltaTime * 1000); veg += FBQuality.VegVisibleInstances; vegPeak = Mathf.Max(vegPeak, FBQuality.VegVisibleInstances); }
                            var avg = rec.Select(r => { double v = 0; for (int k = 0; k < r.Count; k++) v += r.GetSample(k).Value; v /= System.Math.Max(1, r.Count); r.Dispose(); return v; }).ToArray();
                            ft.Sort(); float P(float x) => ft[Mathf.Clamp((int)(x * ft.Count), 0, ft.Count - 1)];
                            long mem = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576;
                            var line = $"{System.DateTime.Now:yyyy-MM-dd HH:mm};{Application.platform};{SystemInfo.graphicsDeviceName};{map};{FBQuality.Device.plat};{FBQuality.Names[q]};{1000f / ft.Average():0};{P(.5f):0.0};{P(.95f):0.0};{P(.99f):0.0};{ft[^1]:0};{mem};{avg[0]:0};{avg[1]:0};{avg[2]:0};{avg[3]:0};{veg / System.Math.Max(1, ft.Count)};{vegPeak};{FBQuality.VegTotalInstances};{FBQuality.VegMaxInstances}";
                            csv.AppendLine(line); Debug.Log("[FreeBala][bench] " + line);
                            if (FBQuality.VegTotalInstances > 0) Assert.LessOrEqual(vegPeak, FBQuality.VegMaxInstances + 64, $"{map} {FBQuality.Device.plat} {FBQuality.Names[q]}: vegetação passou do teto");
                        }
                    }
            }
            finally { FBQuality.SimulatePlatform(null); FBQuality.Set(keep); System.IO.File.AppendAllText(path, csv.ToString()); }
        }

        // ---- vegetação instanciada sem colisor (Agente 5): tufos/arbustos não podem travar o jogador nem as balas ----
        static IEnumerator VegetacaoSemColisor(string map)
        {
            yield return Load(map, "role", 2);
            var roots = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name.StartsWith("Vegetacao") || t.GetComponents<MonoBehaviour>().Any(m => m && m.GetType().Name.StartsWith("FBVeg"))).ToList();
            var foliageSmall = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.sharedMaterials.Any(m => m && m.shader && m.shader.name == "FreeBala/Foliage") && r.bounds.extents.magnitude < .75f).ToList();
            if (roots.Count == 0 && foliageSmall.Count == 0 && FBQuality.VegTotalInstances == 0) { Assert.Ignore($"{map}: vegetação instanciada ainda não existe"); yield break; }
            var bad = roots.SelectMany(r => r.GetComponentsInChildren<Collider>(true)).Concat(foliageSmall.SelectMany(r => r.GetComponents<Collider>())).Where(c => c).Select(c => c.name).Distinct().ToList();
            Debug.Log($"[FreeBala][regressao] {map}: vegetação {roots.Count} raízes · {foliageSmall.Count} tufos com GameObject · instâncias {FBQuality.VegVisibleInstances}/{FBQuality.VegTotalInstances} (teto {FBQuality.VegMaxInstances}) · colisores {bad.Count}");
            Assert.IsEmpty(bad, $"{map}: vegetação com colisor: {string.Join(", ", bad.Take(10))}");
            Assert.LessOrEqual(FBQuality.VegVisibleInstances, FBQuality.VegMaxInstances + 64, $"{map}: instâncias visíveis acima do teto do nível");
            // raio de bala rente ao chão numa área verde não pode bater em tufo (camada de colisão)
            foreach (var r in foliageSmall.Take(20)) { var c = r.bounds.center; Assert.IsFalse(Physics.Raycast(c + Vector3.up * 2, Vector3.down, out var h, 2.2f, FBWorld.SolidMask) && h.collider && h.collider.transform.IsChildOf(r.transform), $"{map}: {r.name} bloqueia tiro/movimento"); }
        }
        [UnityTest, Timeout(120000)] public IEnumerator Regressao_VegetacaoSemColisor_PracaAguas() { yield return VegetacaoSemColisor("praca_aguas"); }
        [UnityTest, Timeout(120000)] public IEnumerator Regressao_VegetacaoSemColisor_VilaJardim() { yield return VegetacaoSemColisor("vila_jardim"); }

        // ---- food truck (Agente 8, 170 mil tris originais): precisa de LODGroup com níveis reduzidos ----
        static readonly System.Text.RegularExpressions.Regex FoodTruck = new("food[ _-]?truck", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        [UnityTest, Timeout(180000)] public IEnumerator Regressao_FoodTruckComLOD()
        {
            int found = 0;
            foreach (var map in new[] { "praca_aguas", "vila_jardim" })
            {
                yield return Load(map, "role", 2);
                var trucks = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => FoodTruck.IsMatch(t.name) && (!t.parent || !FoodTruck.IsMatch(t.parent.name))).ToList();
                foreach (var t in trucks)
                {
                    found++;
                    var lg = t.GetComponentInChildren<LODGroup>(true) ?? t.GetComponentInParent<LODGroup>(); Assert.IsNotNull(lg, $"{map}: {t.name} sem LODGroup");
                    var lods = lg.GetLODs(); Assert.GreaterOrEqual(lods.Length, 2, $"{map}: {t.name} com {lods.Length} nível de LOD");
                    long Tri(LOD l) => l.renderers.Where(r => r).Select(r => r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh).Where(m => m).Sum(m => { long n = 0; for (int k = 0; k < m.subMeshCount; k++) n += m.GetIndexCount(k) / 3; return n; });
                    var tris = lods.Select(Tri).ToArray();
                    Debug.Log($"[FreeBala][regressao] {map}: {t.name} LODs {string.Join(" / ", tris.Select(x => (x / 1000f).ToString("0.0") + "k"))} tri · transições {string.Join(" / ", lods.Select(l => l.screenRelativeTransitionHeight.ToString("0.000")))}");
                    Assert.LessOrEqual(tris[0], 30000, $"{map}: {t.name} LOD0 com {tris[0]} triângulos (máx. 30 mil)");
                    for (int k = 1; k < tris.Length; k++) Assert.Less(tris[k], tris[k - 1] + 1, $"{map}: {t.name} LOD{k} não é mais leve que o LOD{k - 1}");
                }
            }
            if (found == 0) Assert.Ignore("food truck ainda não está nos mapas (procurado: nome contendo 'food truck')");
        }

#if !UNITY_WEBGL
        // ---- rede: dois clientes contra o server.mjs local (porta 8199). Precisa do Node no PATH; sem ele o teste é ignorado. ----
        [UnityTest, Timeout(60000)] public IEnumerator Rede_ServidorLocal_DoisClientes()
        {
            var root = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../..")); var srv = System.IO.Path.Combine(root, "server.mjs");
            if (!System.IO.File.Exists(srv)) { Assert.Ignore("server.mjs não encontrado em " + root); yield break; }
            System.Diagnostics.Process node = null;
            try { node = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("node", $"\"{srv}\" 8199") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, WorkingDirectory = root }); }
            catch (System.Exception e) { Assert.Ignore("Node indisponível: " + e.Message); yield break; }
            try
            {
                for (float t = 0; t < 1.5f; t += Time.unscaledDeltaTime) yield return null;
                var A = new WsClient(); var B = new WsClient();
                yield return A.Open("ws://127.0.0.1:8199/ws"); yield return B.Open("ws://127.0.0.1:8199/ws");
                A.Send(new Dictionary<string, object> { ["t"] = "hello", ["name"] = "A", ["skin"] = "r_tutu", ["team"] = 0 }); B.Send(new Dictionary<string, object> { ["t"] = "hello", ["name"] = "B", ["skin"] = "r_tutu", ["team"] = 1 });
                A.Send(new Dictionary<string, object> { ["t"] = "create", ["name"] = "teste", ["mode"] = "tdm", ["map"] = "vila_jardim", ["max"] = 4 });
                Dictionary<string, object> ja = null; yield return A.Wait("joined", m => ja = m); Assert.IsNotNull(ja, "A não entrou na sala");
                string room = Json.S(Json.O(ja, "room"), "id"); int idA = Json.I(Json.O(ja, "you"), "id");
                B.Send(new Dictionary<string, object> { ["t"] = "join", ["id"] = room }); Dictionary<string, object> jb = null; yield return B.Wait("joined", m => jb = m); Assert.IsNotNull(jb, "B não entrou");
                // estado no formato do FBNet.BuildState: B recebe com o id de A e as coordenadas convertem de volta
                var u = new Vector3(12.5f, 3.1f, -40.25f); var w = Coords.ToWeb(u);
                A.Send(new Dictionary<string, object> { ["t"] = "st", ["p"] = w, ["v"] = new float[] { 0, 0, 0 }, ["y"] = 1.5f, ["ay"] = 1.5f, ["pi"] = .2f, ["f"] = 48, ["ln"] = 0f, ["hp"] = 87, ["ve"] = null, ["vt"] = null, ["w"] = "ak" });
                Dictionary<string, object> st = null; yield return B.Wait("st", m => st = m); Assert.IsNotNull(st, "B não recebeu o estado");
                Assert.AreEqual(idA, Json.I(st, "id")); Assert.Less((Coords.FromWeb(Json.L(st, "p")) - u).magnitude, .02f); Assert.AreEqual(87, Json.I(st, "hp")); Assert.AreEqual("ak", Json.S(st, "w"));
                // evento de efeito de acerto (hfx) passa pelo servidor; 'hit' vai só para o alvo
                A.Send(new Dictionary<string, object> { ["t"] = "ev", ["e"] = "hfx", ["vid"] = 99, ["p"] = w, ["n"] = new float[] { 0, 1, 0 }, ["d"] = new float[] { 0, 0, 1 }, ["pt"] = "head", ["head"] = true, ["dmg"] = 30 });
                Dictionary<string, object> fx = null; yield return B.Wait("ev", m => { if (Json.S(m, "e") == "hfx") fx = m; }, m => Json.S(m, "e") == "hfx"); Assert.IsNotNull(fx, "hfx não chegou");
                int idB = Json.I(Json.O(jb, "you"), "id"); A.Send(new Dictionary<string, object> { ["t"] = "ev", ["e"] = "hit", ["to"] = idB, ["dmg"] = 25, ["head"] = false });
                Dictionary<string, object> hit = null; yield return B.Wait("ev", m => hit = m, m => Json.S(m, "e") == "hit"); Assert.AreEqual(25, Json.I(hit, "dmg"));
                Debug.Log($"[FreeBala][regressao] rede local ok: sala {room}, A={idA}, B={idB}");
                A.Close(); B.Close();
            }
            finally { try { if (node != null && !node.HasExited) node.Kill(); } catch { } }
        }

        // cliente WebSocket mínimo para o teste (mesmo transporte do FBNet fora do WebGL)
        class WsClient
        {
            readonly System.Net.WebSockets.ClientWebSocket ws = new(); readonly System.Collections.Concurrent.ConcurrentQueue<string> inbox = new(); readonly System.Threading.CancellationTokenSource cts = new();
            public IEnumerator Open(string url)
            {
                var t = ws.ConnectAsync(new System.Uri(url), cts.Token); while (!t.IsCompleted) yield return null; Assert.IsFalse(t.IsFaulted, "sem conexão: " + t.Exception?.GetBaseException().Message);
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    var buf = new byte[1 << 16]; var sb = new System.Text.StringBuilder();
                    while (ws.State == System.Net.WebSockets.WebSocketState.Open)
                    {
                        System.Net.WebSockets.WebSocketReceiveResult r; try { r = await ws.ReceiveAsync(new System.ArraySegment<byte>(buf), cts.Token); } catch { break; }
                        sb.Append(System.Text.Encoding.UTF8.GetString(buf, 0, r.Count)); if (r.EndOfMessage) { inbox.Enqueue(sb.ToString()); sb.Clear(); }
                    }
                });
            }
            public void Send(Dictionary<string, object> o) { var b = System.Text.Encoding.UTF8.GetBytes(FBNet.ToJson(o)); ws.SendAsync(new System.ArraySegment<byte>(b), System.Net.WebSockets.WebSocketMessageType.Text, true, cts.Token).Wait(2000); }
            public IEnumerator Wait(string type, System.Action<Dictionary<string, object>> got, System.Func<Dictionary<string, object>, bool> when = null)
            {
                for (float t = 0; t < 5; t += Time.unscaledDeltaTime)
                {
                    while (inbox.TryDequeue(out var s)) { var m = Json.Parse(s) as Dictionary<string, object>; if (m != null && Json.S(m, "t") == type && (when == null || when(m))) { got(m); yield break; } }
                    yield return null;
                }
            }
            public void Close() { try { cts.Cancel(); ws.Dispose(); } catch { } }
        }
#endif
    }
}
