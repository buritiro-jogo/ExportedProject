// PASSEIO PELA VILA JARDIM INTEIRA (rodada 40): FreeBala.exe -fbtour vila_jardim:role
// Visita um ponto de rua de cada condomínio (Ipê Amarelo ... Buriti) e pontos da Avenida Jardim de ponta a ponta (gerados de
// tools/vj_full/layout.json: rua mais perto do centro do condomínio a >= 12 m de bloco), olha para o condomínio em 1ª pessoa (olho a
// 1,6 m) e em 3ª pessoa (atrás e acima) e grava autoteste/tour/<ponto>_<vista>.png; loga chão achado, FPS médio de 1 s e
// quantos renderers estão visíveis. Resultado no log: [FreeBala][tour].
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBTour : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbtour"); if (i < 0 || i + 1 >= a.Length) return;
            var g = new GameObject("FB_Tour"); DontDestroyOnLoad(g); g.AddComponent<FBTour>().run = a[i + 1];
        }
        string run; Unity.Profiling.ProfilerRecorder dc, bt, sp, tr;
        static long Rec(Unity.Profiling.ProfilerRecorder r) => r.Valid ? r.LastValue : -1;
        // (nome, x leste, y norte (m do mapa, antes da escala 1,2), direção do olhar em graus a partir do leste, anti-horário)
        static readonly (string n, float x, float y, float h)[] PTS =
        {
            ("IpeAmarelo", 50.1f, 150.9f, 100f), ("Samauma", 52.4f, 136.0f, -78f), ("Andiroba", 1.3f, 54.0f, -78f), ("Cedro0", 222.7f, 79.7f, -73f), ("Cedro1", 233.1f, 81.5f, 99f),
            ("Inga0", 152.3f, -103.2f, -78f), ("Inga1", 151.3f, -178.1f, 93f), ("Araca", -74.5f, -124.9f, -80f), ("Jatoba", 109.6f, -201.9f, -77f),
            ("Angelim0", 51.9f, -321.6f, -81f), ("Angelim1", 55.2f, -378.2f, -73f), ("Pupunha2", 46.9f, -479.8f, 103f),   // r53: 1.ª–3.ª fileira do antigo "Pupunha" = Condomínio Angelim
            ("Urucum0", -131.3f, -339.0f, -82f), ("Urucum1", -120.3f, -380.9f, -81f), ("Urucum2", -129.0f, -442.0f, -84f),
            ("Acai", -11.4f, -505.5f, -82f), ("Buriti0", -27.9f, -647.9f, -85f), ("Buriti1", -69.5f, -743.8f, 15f), ("Buriti2", -56.7f, -787.9f, 16f),
            ("Bat_Norte", -80.3f, -18.0f, 304f), ("Bat_Norte2", -44.0f, -11.6f, 270f), ("Bat_Sul", -44.8f, -127.2f, 90f), ("Bat_Leste", 28.2f, -59.5f, 188f),   // r41: mesmas posições/direções das fotos do Street View (refs/sv_batalhao)
            // r42: DENTRO do Batalhão (móveis CC0): escritório do administrativo, reserva de armas, oficina, alojamento do prédio C
            ("Int_Escritorio", -93.97f, -60.80f, -133f), ("Int_Reserva", -90.13f, -49.62f, 68f), ("Int_Oficina", -58.71f, -49.12f, 108f), ("Int_Alojamento", -32.68f, -41.06f, 78f),
            ("Avenida_N", 163.3f, 188.7f, 253f), ("Avenida_Andiroba", 107.4f, 53.5f, 253f), ("Avenida_Colegio", 54.6f, -93.9f, 253f), ("Avenida_Jatoba", 0.7f, -243.9f, 253f),
            ("Avenida_Pupunha", -52.9f, -396.4f, 253f), ("Avenida_Acai", -106.5f, -545.2f, 253f), ("Avenida_S", -46.4f, -633.6f, 160f),
        };
        // r53: -fbtourpts <arquivo.json> troca os pontos acima pelas posições/direções das fotos do Street View (tools/vj_full/sv_tour_r53.py):
        // {"pts":[{"n","x","y","h","z","fov","p"}]} — uma vista por ponto, olho a z m do chão, campo de visão vertical fov, olhar p graus para baixo (0 = horizontal).
        [System.Serializable] class SvShot { public string n; public float x, y, h, z = 2.5f, fov = 60f, p; }
        [System.Serializable] class SvList { public SvShot[] pts; }
        static void Log(string s) => Debug.Log("[FreeBala][tour] " + s);
        IEnumerator Wait(float s) { for (float w = 0; w < s; w += Time.unscaledDeltaTime) yield return null; }
        IEnumerator Start()
        {
            string dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste", "tour"); System.IO.Directory.CreateDirectory(dir);
            yield return Wait(3);
            var mm = run.Split(':'); string map = mm[0], mode = mm.Length > 1 ? mm[1] : "role";
            GameData.Load(); FBSession.Load(); var keepMap = FBSession.map; var keepMode = FBSession.mode;
            FBSession.map = map; FBSession.mode = mode; FBSession.online = false; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_" + map); while (op != null && !op.isDone) yield return null; yield return Wait(6);
            if (mode == "role") { FBGarage.AutoPick(); yield return Wait(2); }
            FBMatch.God = true; var pl = FBPlayer.I; if (pl) pl.enabled = false; var cam = Camera.main;
            // contadores de renderização (build de desenvolvimento): decidir Mesh LOD × static batching com número
            dc = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Draw Calls Count");
            bt = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Batches Count");
            sp = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "SetPass Calls Count");
            tr = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Triangles Count");
            // materiais quebrados (magenta no build: shader ausente/não suportado) no mapa inteiro
            { int nb = 0; foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None)) foreach (var m in r.sharedMaterials)
                    if (m && (!m.shader || !m.shader.isSupported || m.shader.name.Contains("Error")) && nb++ < 30) Log($"MATERIAL QUEBRADO: {r.name} ({r.transform.root.name}) mat {m.name} shader {(m.shader ? m.shader.name : "nulo")} em {r.bounds.center}");
              Log($"materiais quebrados: {nb}"); }
            int ok = 0;
            var args = System.Environment.GetCommandLineArgs(); int ia = System.Array.IndexOf(args, "-fbtourpts");
            if (ia >= 0 && ia + 1 < args.Length)
            {
                var sv = JsonUtility.FromJson<SvList>(System.IO.File.ReadAllText(args[ia + 1])); float fov0 = cam.fieldOfView;
                foreach (var p in sv.pts)
                {
                    var flat = new Vector3(-1.2f * p.x, 0, -1.2f * p.y);
                    float gy = Physics.Raycast(flat + Vector3.up * 80, Vector3.down, out var hit, 160, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) ? hit.point.y : float.NaN;
                    if (float.IsNaN(gy)) { Log($"{p.n}: SEM CHÃO em {flat}"); continue; }
                    float hr = p.h * Mathf.Deg2Rad; var dirv = new Vector3(-Mathf.Cos(hr), 0, -Mathf.Sin(hr)).normalized;
                    var olhar = Quaternion.AngleAxis(p.p, Vector3.Cross(Vector3.up, dirv)) * dirv;   // eixo = direita do olhar: p > 0 abaixa a câmera (o Cross(dirv, up) do tour clássico abaixo é a esquerda e levanta)
                    cam.fieldOfView = p.fov; cam.transform.SetPositionAndRotation(new Vector3(flat.x, gy + p.z, flat.z), Quaternion.LookRotation(olhar));
                    FBDistanceCull.Refresh(); yield return Wait(1.6f);
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"sv_{p.n}.png")); yield return null; yield return null;
                    Log($"sv {p.n}: chão {gy:0.00} · tris {Rec(tr) / 1000}k · draw calls {Rec(dc)}"); ok++;
                }
                cam.fieldOfView = fov0; Log($"fim sv · {ok}/{sv.pts.Length} vistas"); FBSession.map = keepMap; FBSession.mode = keepMode; FBSession.Save(); Application.Quit(); yield break;
            }
            foreach (var p in PTS)
            {
                var flat = new Vector3(-1.2f * p.x, 0, -1.2f * p.y);
                float rayUp = p.n.StartsWith("Int_") ? 2.6f : 80;                                        // dentro de prédio: o raio sai abaixo do forro
                float gy = Physics.Raycast(flat + Vector3.up * rayUp, Vector3.down, out var hit, 160, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) ? hit.point.y : float.NaN;
                if (float.IsNaN(gy)) { Log($"{p.n}: SEM CHÃO em {flat}"); continue; }
                float hr = p.h * Mathf.Deg2Rad; var dirv = new Vector3(-Mathf.Cos(hr), 0, -Mathf.Sin(hr)).normalized;   // direção do mapa (leste, norte) no Unity
                var nav = UnityEngine.AI.NavMesh.SamplePosition(new Vector3(flat.x, gy, flat.z), out var nh, 3, UnityEngine.AI.NavMesh.AllAreas);
                for (int v = 0; v < 2; v++)
                {
                    Vector3 eye = v == 0 ? new Vector3(flat.x, gy + 1.6f, flat.z) : new Vector3(flat.x, gy + 2.7f, flat.z) - dirv * 3.6f;
                    cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(Quaternion.AngleAxis(v == 0 ? 3 : 9, Vector3.Cross(dirv, Vector3.up)) * dirv));
                    FBDistanceCull.Refresh(); yield return Wait(1.1f);
                    float fps = 0; int n = 0; for (float t = 0; t < 1; t += Time.unscaledDeltaTime) { fps += 1 / Mathf.Max(1e-4f, Time.unscaledDeltaTime); n++; yield return null; }
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{p.n}_{(v == 0 ? "fp" : "tps")}.png")); yield return null; yield return null;
                    Log($"{p.n} {(v == 0 ? "fp" : "tps")}: chão {gy:0.00} · navmesh {(nav ? "sim" : "NÃO")} · FPS {fps / Mathf.Max(1, n):0} · draw calls {Rec(dc)} · batches {Rec(bt)} · setpass {Rec(sp)} · tris {Rec(tr) / 1000}k · corte dist. {FBDistanceCull.Hidden}/{FBDistanceCull.Count} · carros escondidos {FBCarCull.Hidden}");
                    if (v == 0) Log($"{p.n} quem desenha: {Breakdown()}");
                    if (v == 0) Log($"{p.n} carros: {Cars(cam)}");
                    if (v == 0 && (p.n == "Avenida_S" || p.n == "Andiroba" || p.n == "Jatoba")) { var e = Experiments(); while (e.MoveNext()) yield return e.Current; }
                }
                ok++;
            }
            Log($"fim · {ok}/{PTS.Length} pontos"); FBSession.map = keepMap; FBSession.mode = keepMode; FBSession.Save(); Application.Quit();
        }
        // renderers visíveis (câmera OU sombra) agrupados pela raiz (no mapa: pelo prefixo do filho) -> onde estão os triângulos/draw calls
        static readonly Dictionary<Mesh, long> triCache = new Dictionary<Mesh, long>();
        // por carro: id, distância da câmera, renderers visíveis (por qualquer câmera) e quantos estão ativos; diz quais câmeras existem
        static string Cars(Camera main)
        {
            var cams = string.Join(",", Camera.allCameras.Select(c => c.name + (c.enabled ? "" : "(off)")));
            var l = FindObjectsByType<FBCar>(FindObjectsSortMode.None).Select(c => (c, d: Vector3.Distance(c.transform.position, main.transform.position),
                vis: c.GetComponentsInChildren<Renderer>().Count(r => r.isVisible), all: c.GetComponentsInChildren<Renderer>().Length)).OrderByDescending(e => e.vis).ToList();
            return $"{l.Count} carros, câmeras [{cams}] · " + string.Join(" | ", l.Take(8).Select(e => $"{e.c.id} {e.d:0}m vis {e.vis}/{e.all}"));
        }
        // (r41) o que custa o FPS na VJ v11: mede 1,5 s com cada grupo desligado (mato / árvores / sombras / mapa sem vegetação)
        IEnumerator Experiments()
        {
            var all = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            Renderer[] Sel(string k) => all.Where(r => r && r.transform.parent && (r.name.Contains(k) || r.transform.parent.name.Contains(k))).ToArray();
            var mato = Sel("VJ11_Mato"); var arv = Sel("VJ11_Arvore"); var urp = GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            float sd = urp ? urp.shadowDistance : 0; var res = new List<string>();
            IEnumerator Meas(string tag)
            {
                for (int i = 0; i < 20; i++) yield return null;
                float f = 0; int n = 0; for (float t = 0; t < 1.5f; t += Time.unscaledDeltaTime) { f += Time.unscaledDeltaTime; n++; yield return null; }
                res.Add($"{tag} {n / Mathf.Max(1e-3f, f):0} fps ({Rec(dc)} dc)");
            }
            void Set(Renderer[] rs, bool off) { foreach (var r in rs) if (r) r.forceRenderingOff = off; }
            // tempo de CPU por quadro (build de desenvolvimento): scripts, física, render e quem está pendurado no mapa
            var recs = new[] { ("main", ProfilerCategory.Internal, "Main Thread"), ("render", ProfilerCategory.Internal, "Render Thread"), ("update", ProfilerCategory.Scripts, "BehaviourUpdate"),
                               ("late", ProfilerCategory.Scripts, "LateBehaviourUpdate"), ("fixed", ProfilerCategory.Scripts, "FixedBehaviourUpdate"), ("fisica", ProfilerCategory.Physics, "Physics.Simulate"),
                               ("cull", ProfilerCategory.Render, "CullScriptable"), ("anim", ProfilerCategory.Animation, "PreLateUpdate.DirectorUpdateAnimationEnd") }
                .Select(x => (x.Item1, ProfilerRecorder.StartNew(x.Item2, x.Item3, 30))).ToList();
            // custo por TIPO de script: desliga todas as instâncias de um tipo por ~0,4 s e mede quanto o BehaviourUpdate+Late cai
            using (var bu = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "BehaviourUpdate", 60))
            using (var lu = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "LateBehaviourUpdate", 60))
            {
                double Avg(int frames) { var a1 = new List<ProfilerRecorderSample>(); var a2 = new List<ProfilerRecorderSample>(); bu.CopyTo(a1); lu.CopyTo(a2);
                    return a1.Skip(Mathf.Max(0, a1.Count - frames)).DefaultIfEmpty().Average(x => x.Value) * 1e-6 + a2.Skip(Mathf.Max(0, a2.Count - frames)).DefaultIfEmpty().Average(x => x.Value) * 1e-6; }
                for (int i = 0; i < 30; i++) yield return null;
                double base0 = Avg(25); var gains = new List<(string, int, double)>();
                var byType = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(m => m && m.enabled && m != this && !(m is FBDistanceCull)).GroupBy(m => m.GetType()).ToList();
                foreach (var g in byType)
                {
                    var t = g.Key; if (t.GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) == null
                                     && t.GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) == null) continue;
                    var list = g.ToList(); foreach (var m in list) if (m) m.enabled = false;
                    for (int i = 0; i < 22; i++) yield return null;
                    double d = Avg(18); foreach (var m in list) if (m) m.enabled = true;
                    gains.Add((t.Name, list.Count, base0 - d)); for (int i = 0; i < 6; i++) yield return null;
                }
                Log($"scripts (update+late {base0:0.0} ms) — o que cai ao desligar: " + string.Join(" · ", gains.OrderByDescending(x => x.Item3).Take(10).Select(x => $"{x.Item1}×{x.Item2} {x.Item3:0.00}")));
            }
            Log("cpu (ms/quadro): " + string.Join(" · ", recs.Select(r => $"{r.Item1} {(r.Item2.Valid && r.Item2.Count > 0 ? AvgMs(r.Item2) : -1):0.0}")));
            foreach (var r in recs) r.Item2.Dispose();
            var map = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name.StartsWith("Map_"));
            if (map) Log("componentes no mapa: " + string.Join(", ", map.GetComponentsInChildren<MonoBehaviour>(true).GroupBy(c => c.GetType().Name).OrderByDescending(g => g.Count()).Take(8).Select(g => $"{g.Key} {g.Count()}"))
                         + $" · colisores {map.GetComponentsInChildren<Collider>(true).Length} · renderers {map.GetComponentsInChildren<Renderer>(true).Length} · luzes {FindObjectsByType<Light>(FindObjectsSortMode.None).Length}");
            var e = Meas("tudo"); while (e.MoveNext()) yield return e.Current;
            Set(mato, true); e = Meas($"sem mato ({mato.Length})"); while (e.MoveNext()) yield return e.Current; Set(mato, false);
            Set(arv, true); e = Meas($"sem árvores ({arv.Length})"); while (e.MoveNext()) yield return e.Current; Set(arv, false);
            if (urp) { urp.shadowDistance = 0; e = Meas("sem sombra"); while (e.MoveNext()) yield return e.Current; urp.shadowDistance = sd; }
            Set(mato, true); Set(arv, true); if (urp) urp.shadowDistance = 0; e = Meas("sem vegetação e sem sombra"); while (e.MoveNext()) yield return e.Current;
            Set(mato, false); Set(arv, false); if (urp) urp.shadowDistance = sd; FBDistanceCull.Refresh();
            Log("experimento: " + string.Join(" | ", res));
        }
        static double AvgMs(ProfilerRecorder r) { var a = new System.Collections.Generic.List<ProfilerRecorderSample>(r.Capacity); r.CopyTo(a); return a.Count == 0 ? 0 : a.Average(x => x.Value) * 1e-6; }
        static string Breakdown()
        {
            var g = new Dictionary<string, (int n, long t)>();
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.isVisible || r.forceRenderingOff) continue;
                Mesh m = r is SkinnedMeshRenderer sk ? sk.sharedMesh : r.GetComponent<MeshFilter>() ? r.GetComponent<MeshFilter>().sharedMesh : null;
                // static batching: sharedMesh vira a malha combinada inteira -> contar só as submalhas DESTE renderer
                int nsub = r.sharedMaterials.Length; long t = 0;
                if (m && r.isPartOfStaticBatch && r is MeshRenderer mr0) { for (int i = mr0.subMeshStartIndex; i < Mathf.Min(m.subMeshCount, mr0.subMeshStartIndex + nsub); i++) t += m.GetIndexCount(i) / 3; }
                else if (m) { if (!triCache.TryGetValue(m, out t)) { for (int i = 0; i < m.subMeshCount; i++) t += m.GetIndexCount(i) / 3; triCache[m] = t; } }
                string k = r.transform.root.name; if (k.StartsWith("Map_")) { var tr = r.transform; while (tr.parent && tr.parent.parent) tr = tr.parent; k = "mapa/" + System.Text.RegularExpressions.Regex.Replace(tr.name, @"[\d._]+$", ""); }
                else k = System.Text.RegularExpressions.Regex.Replace(k, @"[\d._ ()]+$", "");
                g.TryGetValue(k, out var a); g[k] = (a.n + Mathf.Max(1, nsub), a.t + t);
            }
            return "por tris: " + string.Join(" | ", g.OrderByDescending(e => e.Value.t).Take(8).Select(e => $"{e.Key} {e.Value.n}dc {e.Value.t / 1000}k"))
                + " ## por draw calls: " + string.Join(" | ", g.OrderByDescending(e => e.Value.n).Take(10).Select(e => $"{e.Key} {e.Value.n}dc {e.Value.t / 1000}k"));
        }
    }
}
