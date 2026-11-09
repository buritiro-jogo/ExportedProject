// TESTE DA ILUMINAÇÃO (rodada 64): BURITIRO.exe -fbluz vila_jardim,praca_pt,... [-fbluzperf] [-fbluzantiga]
// Para cada mapa: carrega no modo treino, esconde o HUD, põe a câmera em 3 poses FIXAS (tiradas dos spawns do fb_data, então são as mesmas
// em qualquer build) e grava autoteste/luz/<mapa>_<pose>_<hhmm>[_antiga].png em cada horário pronto (12:00 17:30 18:00 18:30 20:00 00:00 05:30).
//   pose A = olho de jogador no spawn A olhando para o spawn B (vista de combate)
//   pose B = aérea a 22 m entre os dois spawns, olhando para o B
//   pose C = olho de jogador no meio do mapa olhando para o OESTE (rumo 260°, onde o sol se põe)
// Com -fbluzantiga o sistema antigo (só dia/noite do menu) roda no lugar: grava só 12:00 (dia) e 20:00 (noite) para o ANTES×DEPOIS.
// Com -fbluzperf mede, na pose A, o tempo de quadro (média, p95, p99, FPS e GPU quando o driver informa) em BAIXO/MÉDIO/ALTO/ULTRA e no
// CINEMATIC, ao meio-dia e às 20:00; escreve autoteste/luz/perf.tsv (uma linha por medida). Resultado no log: [FreeBala][luz].
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBLuzTeste : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbluz"); if (i < 0 || i + 1 >= a.Length) return;
            var g = new GameObject("FB_LuzTeste"); DontDestroyOnLoad(g); var t = g.AddComponent<FBLuzTeste>();
            t.maps = a[i + 1].Split(','); t.perf = a.Contains("-fbluzperf");
        }
        string[] maps; bool perf; string dir;
        static void Log(string s) => Debug.Log("[FreeBala][luz] " + s);
        IEnumerator Wait(float s) { for (float w = 0; w < s; w += Time.unscaledDeltaTime) yield return null; }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste", "luz"); System.IO.Directory.CreateDirectory(dir);
            yield return Wait(3);
            GameData.Load(); FBSession.Load(); var keepMap = FBSession.map; var keepMode = FBSession.mode; int keepQ = FBQuality.Choice;
            bool legacy = FBEnv.Legacy; string suf = legacy ? "_antiga" : "";
            Log($"início · mapas {string.Join(",", maps)} · {(legacy ? "SISTEMA ANTIGO" : "BuritiroEnvironmentManager")} · qualidade {FBQuality.Names[(int)FBQuality.Level]} · {SystemInfo.graphicsDeviceName} · {Screen.width}x{Screen.height}");
            foreach (var map in maps)
            {
                if (!GameData.Maps.TryGetValue(map, out var md) || md.spawnA.Count == 0 || md.spawnB.Count == 0) { Log($"{map}: mapa sem spawns no fb_data"); continue; }
                Vector3 sa = md.spawnA[0], sb = md.spawnB[0];                 // copiados ANTES da carga (a cena pode refazer as listas do MapDef)
                var keepWx = FBSession.WeatherFor(map);
                // o antigo só tem dia e noite do menu: uma carga para cada
                var loads = legacy ? new[] { ("dia", 12f), ("noite", 20f) } : new[] { ("dia", -1f) };
                foreach (var (time, legacyHour) in loads)
                {
                    FBSession.map = map; FBSession.mode = "treino"; FBSession.online = false; FBSession.SetWeather(map, time, "limpo");
                    var op = SceneManager.LoadSceneAsync("Map_" + map); while (op != null && !op.isDone) yield return null; yield return Wait(6);
                    FBMatch.God = true; var pl = FBPlayer.I; if (pl) pl.enabled = false; var cam = Camera.main;
                    if (!cam) { Log($"{map}: sem câmera"); continue; }
                    HideUi();
                    float fov0 = cam.fieldOfView; cam.fieldOfView = 60;
                    List<(string n, Vector3 pos, Quaternion rot)> poses;
                    try { poses = Poses(sa, sb); } catch (System.Exception ex) { Log($"{map}: poses falharam ({ex.Message})"); poses = new() { ("A", cam.transform.position, cam.transform.rotation) }; }
                    var hours = legacy ? new[] { (time == "dia" ? "Meio-dia" : "Noite", legacyHour) } : FBEnv.Presets;
                    var env = BuritiroEnvironmentManager.I;
                    foreach (var (nome, h) in hours)
                    {
                        if (!legacy && env) { env.cycleSpeed = 0; env.SetHour(h, true); }
                        yield return Wait(1.4f);
                        foreach (var (pn, pos, rot) in poses)
                        {
                            cam.transform.SetPositionAndRotation(pos, rot); FBDistanceCull.Refresh();
                            if (!legacy && env) env.SetHour(h, true);                           // reaplica (sonda de reflexo do céu na posição nova)
                            yield return Wait(1.0f); HideUi(); yield return null;
                            string hh = FBEnv.HourText(h).Replace(":", "");
                            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{map}_{pn}_{hh}{suf}.png")); yield return null; yield return null;
                        }
                        Log($"{map} {FBEnv.HourText(h)} ({nome}): " + (legacy ? $"antigo noite={FBWeather.Night} sol {RenderSettings.sun?.intensity:0.00} amb {RenderSettings.ambientIntensity:0.00}"
                                                                         : env ? env.state.ToString() : "SEM GERENTE") + $" · postes acesos {FBStreetLights.Lit}");
                    }
                    if (perf) { var e = Perf(map, cam, poses[0], legacy, legacyHour, suf); while (e.MoveNext()) yield return e.Current; }
                    cam.fieldOfView = fov0;
                }
                FBSession.SetWeather(map, keepWx.time, keepWx.weather);
            }
            if (FBEnvQuality.Cinematic) FBEnvQuality.ApplyCinematic(false);
            FBQuality.Set(keepQ);
            FBSession.map = keepMap; FBSession.mode = keepMode; FBSession.Save();
            Log("fim"); Application.Quit();
        }

        // o FBHUD religa o próprio canvas a cada quadro: desliga o componente e todos os canvas antes de cada foto
        static void HideUi() { foreach (var h in FindObjectsByType<FBHUD>(FindObjectsSortMode.None)) h.enabled = false; foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.enabled = false; }

        // 3 poses fixas pelos spawns (iguais em qualquer build e com qualquer sistema de luz)
        static List<(string n, Vector3 pos, Quaternion rot)> Poses(Vector3 a, Vector3 b)
        {
            Vector3 mid = (a + b) * .5f; var l = new List<(string, Vector3, Quaternion)>();
            float G(Vector3 p) => Physics.Raycast(new Vector3(p.x, p.y + 60, p.z), Vector3.down, out var h, 140, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) ? h.point.y : p.y;
            var ab = b - a; ab.y = 0; ab.Normalize();
            var pa = new Vector3(a.x, G(a) + 1.7f, a.z); l.Add(("A", pa, Quaternion.LookRotation(Quaternion.AngleAxis(2, Vector3.Cross(Vector3.up, ab)) * ab)));
            var q = Vector3.Lerp(a, b, .35f); var pb = new Vector3(q.x, G(q) + 22f, q.z); l.Add(("B", pb, Quaternion.LookRotation(Quaternion.AngleAxis(18, Vector3.Cross(Vector3.up, ab)) * ab)));
            var w = FBSolar.Direction(260, 0, 0); w.y = 0; w.Normalize();
            var pc = new Vector3(mid.x, G(mid) + 1.7f, mid.z); l.Add(("C", pc, Quaternion.LookRotation(Quaternion.AngleAxis(-4, Vector3.Cross(Vector3.up, w)) * w)));
            return l;
        }

        // tempo de quadro por nível de qualidade (pose A): meio-dia e 20:00 (no antigo: o horário da carga)
        IEnumerator Perf(string map, Camera cam, (string n, Vector3 pos, Quaternion rot) pose, bool legacy, float legacyHour, string suf)
        {
            cam.transform.SetPositionAndRotation(pose.pos, pose.rot); FBDistanceCull.Refresh();
            var env = BuritiroEnvironmentManager.I; string tsv = System.IO.Path.Combine(dir, "perf.tsv");
            if (!System.IO.File.Exists(tsv)) System.IO.File.WriteAllText(tsv, "sistema\tmapa\thora\tnivel\tms_media\tms_p95\tms_p99\tfps\tgpu_ms\tdraw_calls\ttris_k\n");
            var hours = legacy ? new[] { legacyHour } : new[] { 12f, 20f };
            using var dc = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Draw Calls Count");
            using var tr = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Triangles Count");
            var timings = new FrameTiming[1];
            foreach (var h in hours)
            {
                if (!legacy && env) env.SetHour(h, true);
                for (int lv = 0; lv <= 4; lv++)                                                    // 4 = ULTRA + CINEMATIC
                {
                    if (lv == 4 && legacy) break;
                    if (FBEnvQuality.Cinematic && lv < 4) FBEnvQuality.ApplyCinematic(false);
                    FBQuality.Apply((FBQLevel)Mathf.Min(lv, 3)); if (lv == 4) FBEnvQuality.ApplyCinematic(true);
                    if (!legacy && env) env.SetHour(h, true);
                    yield return Wait(2.5f);
                    var ft = new List<float>(); var gpu = new List<float>();
                    for (float t = 0; t < 4f; t += Time.unscaledDeltaTime)
                    {
                        FrameTimingManager.CaptureFrameTimings(); if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0) gpu.Add((float)timings[0].gpuFrameTime);
                        ft.Add(Time.unscaledDeltaTime * 1000f); yield return null;
                    }
                    ft.RemoveAt(0); ft.Sort(); float avg = ft.Average(), p95 = ft[(int)(ft.Count * .95f)], p99 = ft[(int)(ft.Count * .99f)];
                    string nv = lv == 4 ? "CINEMATIC" : FBQuality.Names[lv]; float g = gpu.Count > 0 ? gpu.Average() : -1;
                    Log($"perf {map} {FBEnv.HourText(h)} {nv}: {avg:0.00} ms (p95 {p95:0.00}, p99 {p99:0.00}) · {1000f / avg:0} FPS · GPU {(g > 0 ? g.ToString("0.00") + " ms" : "n/d")} · dc {(dc.Valid ? dc.LastValue : -1)} · tris {(tr.Valid ? tr.LastValue / 1000 : -1)}k");
                    System.IO.File.AppendAllText(tsv, $"{(legacy ? "antigo" : "novo")}\t{map}\t{FBEnv.HourText(h)}\t{nv}\t{avg:0.00}\t{p95:0.00}\t{p99:0.00}\t{1000f / avg:0}\t{g:0.00}\t{(dc.Valid ? dc.LastValue : -1)}\t{(tr.Valid ? tr.LastValue / 1000 : -1)}\n".Replace(',', '.'));
                    if (lv == 1) { cam.transform.SetPositionAndRotation(pose.pos, pose.rot); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{map}_perf_MEDIO_{FBEnv.HourText(h).Replace(":", "")}{suf}.png")); yield return null; }
                }
                if (FBEnvQuality.Cinematic) FBEnvQuality.ApplyCinematic(false);
            }
        }
    }
}
