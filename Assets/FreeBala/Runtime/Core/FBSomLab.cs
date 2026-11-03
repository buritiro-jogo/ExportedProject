// Autoteste do SOM DOS VEÍCULOS (rodada 45): FreeBala.exe -fbsomlab [mapa:modo]
// Mede o que sai de verdade no ouvinte (AudioListener.GetOutputData/GetSpectrumData), sem confiar no código:
//  · sirene (FBSiren) a 3–400 m: nível em dB acima do fundo, canal E/D (lado certo), prédio no meio (oclusão), desliga (some em < 0,2 s);
//  · giroflex de 5 viaturas ao mesmo tempo a 20 qps: lampejos vermelhos/azuis contados por viatura (esperado 2 + 2 por ciclo de 0,9 s);
//  · buzinas: cada buzina do jogo (carro, polícia, moto) tocada sozinha -> pico e centróide do espectro (a campainha de bicicleta tinha
//    pico em 8,3 kHz) e o nome do clipe;
//  · pneus: freada leve (toque de 0,15 s), freada brusca a ~70 km/h no asfalto e na terra — quais sons tocaram e o nível dos loops.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBSomLab : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbsomlab"); if (i < 0) return;
            var g = new GameObject("FB_SomLab"); DontDestroyOnLoad(g); var t = g.AddComponent<FBSomLab>(); if (i + 1 < a.Length && !a[i + 1].StartsWith("-")) t.run = a[i + 1];
        }
        string run = "vila_jardim:classico"; float cThr, cSt; bool cHb;
        static void Log(string s) => Debug.Log("[FreeBala][somlab] " + s);
        IEnumerator Wait(float s) { for (float w = 0; w < s; w += Time.unscaledDeltaTime) yield return null; }
        static readonly float[] bufL = new float[4096], bufR = new float[4096], spec = new float[4096];
        // potência média (L, R) ao longo de secs
        IEnumerator Measure(float secs, float[] res)
        {
            double sl = 0, sr = 0; int n = 0;
            for (float t = 0; t < secs; t += Time.unscaledDeltaTime)
            {
                AudioListener.GetOutputData(bufL, 0); AudioListener.GetOutputData(bufR, 1);
                double a = 0, b = 0; for (int i = 0; i < bufL.Length; i++) { a += bufL[i] * bufL[i]; b += bufR[i] * bufR[i]; }
                sl += a / bufL.Length; sr += b / bufR.Length; n++; yield return null;
            }
            res[0] = (float)(sl / Mathf.Max(1, n)); res[1] = (float)(sr / Mathf.Max(1, n));
        }
        static float Db(float p) => 10 * Mathf.Log10(Mathf.Max(1e-12f, p));
        static string Above(float[] m, float[] bg) { float p = m[0] + m[1] - bg[0] - bg[1]; return p <= 0 ? "≈ fundo" : $"{Db(p / 2):0.0} dB (fundo {Db((bg[0] + bg[1]) / 2):0.0})"; }

        IEnumerator Start()
        {
            yield return Wait(3); var mm = run.Split(':'); GameData.Load(); FBSession.Load(); var keepMap = FBSession.map; var keepMode = FBSession.mode;
            FBSession.map = mm[0]; FBSession.mode = mm.Length > 1 ? mm[1] : "classico"; FBSession.online = false; FBSession.view = "tps"; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_" + mm[0]); while (op != null && !op.isDone) yield return null; yield return Wait(5);
            FBMatch.God = true; foreach (var b in FBMatch.Bots.ToList()) if (b) b.gameObject.SetActive(false);
            var me = FBMatch.Player; if (!me) { Log("sem jogador"); yield break; }
            // pista isolada 300 m acima do mapa (sem prédio, trânsito ou ambiente por perto)
            var p0 = new Vector3(me.Pos.x, me.Pos.y + 300, me.Pos.z);
            var bc = new GameObject("FB_SomPad").AddComponent<BoxCollider>(); bc.gameObject.layer = FBWorld.LMap; bc.size = new Vector3(3000, 2, 3000); bc.transform.position = p0 - Vector3.up; Physics.SyncTransforms();
            // ouvinte do laboratório (a câmera do jogo e o ouvinte dela saem)
            foreach (var l in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude)) l.enabled = false;
            var ear = new GameObject("SomLabEar"); ear.AddComponent<AudioListener>(); var cam = ear.AddComponent<Camera>(); cam.depth = 100;
            var cc = me.GetComponent<CharacterController>(); if (cc) cc.enabled = false; me.transform.position = p0 + new Vector3(0, 0, -600); if (cc) cc.enabled = true;
            var played = new List<string>(); FBAudio.Played = (n, p, v) => played.Add(n);
            float[] bg = new float[2], m = new float[2];
            ear.transform.SetPositionAndRotation(p0 + new Vector3(0, 1.6f, -40), Quaternion.identity); yield return Wait(1); yield return Measure(1, bg);
            Log($"fundo (pista vazia): {Db((bg[0] + bg[1]) / 2):0.0} dB");

            // ---- 1. sirene por distância (viatura parada em p0; ouvinte olhando para +z, viatura à DIREITA)
            string vid = new[] { "ViaturaPMAzul", "ViaturaBM", "ViaturaPM" }.FirstOrDefault(v => FBRegistry.I.Vehicle(v) && GameData.Vehicles.ContainsKey(v));
            var pol = vid != null ? FBVehicles.Spawn(vid, p0 + Vector3.up * .3f, 0) : null; yield return Wait(1);
            if (!pol) Log("viatura não nasceu");
            else
            {
                Log($"viatura {pol.id} · polícia {FBSiren.IsPolice(pol)} · buzina {FBCarSound.HornFor(pol)}");
                foreach (int mode in new[] { 1, 2, 3 })
                {
                    FBSiren.Set(pol, mode); yield return Wait(.5f);
                    ear.transform.SetPositionAndRotation(p0 + new Vector3(-10, 1.6f, 0), Quaternion.identity); yield return Measure(2, m);
                    Log($"sirene modo {mode} ({FBSiren.MODES[mode]}) a 10 m: {Above(m, bg)} · {FBSiren.Diag(pol)}");
                }
                FBSiren.Set(pol, 1); yield return Wait(.5f);
                float near = 0;
                foreach (float d in new[] { 3f, 10, 30, 60, 120, 200, 300, 400 })
                {
                    ear.transform.SetPositionAndRotation(p0 + new Vector3(-d, 1.6f, 0), Quaternion.identity); yield return Wait(.4f); yield return Measure(1.6f, m);
                    float p = (m[0] + m[1] - bg[0] - bg[1]) / 2; if (d == 3) near = p;
                    Log($"sirene a {d:0} m: {Above(m, bg)} · relativo a 3 m {(p > 0 && near > 0 ? Db(p / near) : -99):0.0} dB · E/D {Db(m[0] / Mathf.Max(1e-12f, m[1])):+0.0;-0.0} dB (viatura à direita: E < D) · {FBSiren.Diag(pol)}");
                }
                // prédio no meio a 40 m
                ear.transform.SetPositionAndRotation(p0 + new Vector3(-40, 1.6f, 0), Quaternion.identity); yield return Wait(.5f); yield return Measure(1.5f, m); var free = (float[])m.Clone();
                var wall = new GameObject("FB_SomParede").AddComponent<BoxCollider>(); wall.gameObject.layer = FBWorld.LMap; wall.size = new Vector3(2, 14, 30); wall.transform.position = p0 + new Vector3(-20, 6, 0); Physics.SyncTransforms();
                yield return Wait(1.2f); yield return Measure(1.5f, m);
                Log($"oclusão a 40 m: livre {Above(free, bg)} · com prédio no meio {Above(m, bg)} · {FBSiren.Diag(pol)}"); Destroy(wall.gameObject); yield return Wait(.3f);
                // desligar
                ear.transform.SetPositionAndRotation(p0 + new Vector3(-10, 1.6f, 0), Quaternion.identity); yield return Wait(.5f);
                FBSiren.Set(pol, 0); yield return Wait(.2f); yield return Measure(.6f, m);
                Log($"sirene desligada (0,2 s depois): {Above(m, bg)} · {FBSiren.Diag(pol)} · fontes 'sirene' na cena {FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Count(s => s && s.name == "sirene")}");
                FBSiren.SetLights(pol, false);
                // passagem (Doppler leve): viatura a 25 m/s cruzando a 8 m do ouvinte
                FBSiren.Set(pol, 2); ear.transform.SetPositionAndRotation(p0 + new Vector3(-8, 1.6f, 0), Quaternion.identity);
                var rb0 = pol.transform.position; float[] pitchLog = new float[3]; int k = 0;
                for (float t = 0; t < 4; t += Time.deltaTime) { pol.transform.position = rb0 + new Vector3(0, 0, -50 + t * 25); var src = pol.GetComponentsInChildren<AudioSource>().FirstOrDefault(s => s.name == "sirene"); if (k < 3 && t > k * 1.6f) { pitchLog[k++] = src ? src.dopplerLevel : -1; } yield return null; }
                Log($"passagem a 90 km/h: Doppler da fonte {pitchLog[0]:0.00} (0 = desligado, 1 = físico)"); FBSiren.Set(pol, 0); pol.transform.position = rb0; yield return Wait(.5f);
            }

            // ---- 2. giroflex de 5 viaturas a 20 qps
            var cars = new List<FBCar>(); if (pol) cars.Add(pol);
            for (int i = 1; i < 5 && vid != null; i++) { var c = FBVehicles.Spawn(vid, p0 + new Vector3(i * 8, .3f, 30), 0); if (c) cars.Add(c); }
            yield return Wait(.5f); foreach (var c in cars) FBSiren.SetLights(c, true);
            int keepFps = Application.targetFrameRate; int keepV = QualitySettings.vSyncCount; QualitySettings.vSyncCount = 0; Application.targetFrameRate = 20; yield return Wait(.5f);
            var redE = new int[cars.Count]; var bluE = new int[cars.Count]; var lastR = new bool[cars.Count]; var lastB = new bool[cars.Count]; var lights = new int[cars.Count]; int frames = 0;
            for (float t = 0; t < 9; t += Time.unscaledDeltaTime)
            {
                frames++;
                for (int i = 0; i < cars.Count; i++)
                {
                    var b = FBGiroflex.Find(cars[i]); if (b == null) continue; if (b.red) lights[i] = 1;
                    bool r = b.rNow, bl = b.bNow;
                    if (r && !lastR[i]) redE[i]++; if (bl && !lastB[i]) bluE[i]++; lastR[i] = r; lastB[i] = bl;
                }
                yield return null;
            }
            Application.targetFrameRate = keepFps; QualitySettings.vSyncCount = keepV;
            for (int i = 0; i < cars.Count; i++) Log($"giroflex viatura {i + 1}: {redE[i]} lampejos vermelhos e {bluE[i]} azuis em 9 s a {frames / 9f:0} qps (esperado 20 + 20 — os dois de cada cor podem fundir num só a 20 qps: mín. 10) · luz pontual própria {(lights[i] == 1 ? "sim" : "não (só as lentes)")} · lentes próprias {FBGiroflex.OwnCount(cars[i])}");
            foreach (var c in cars) FBSiren.SetLights(c, false); yield return Wait(.3f);
            foreach (var c in cars.Skip(1)) if (c) Destroy(c.gameObject);

            // ---- 3. buzinas: cada clipe sozinho, ouvinte a 5 m
            ear.transform.SetPositionAndRotation(p0 + new Vector3(-5, 1.6f, 0), Quaternion.identity); yield return Wait(.5f);
            var horns = new[] { "reb_horn_gol_saveiro", "reb_horn_palio", "horn_sedan", "horn_sedan_grave", "horn_palio_grave", "horn_gol_agudo", "reb_horn_chevette", "reb_horn_fusca", "reb_horn_caminhonetes", "reb_horn_caminhonetesv2", "horn_caminhonete_grave", "horn_navio", "horn_policia1", "horn_policia2", "horn_policia3", "horn_moto", "horn_moto_agudo", "horn_moto_grave" };
            foreach (var h in horns)
            {
                var clip = FBAudio.Clip(h); if (!clip) { Log($"buzina {h}: SEM CLIPE"); continue; }
                FBAudio.SetLoop("lab_horn", h, .6f, p0 + Vector3.up); yield return Wait(.35f);
                var acc = new float[spec.Length]; int n = 0; for (float t = 0; t < .5f; t += Time.unscaledDeltaTime) { AudioListener.GetSpectrumData(spec, 0, FFTWindow.BlackmanHarris); for (int i = 0; i < spec.Length; i++) acc[i] += spec[i]; n++; yield return null; }
                FBAudio.StopLoop("lab_horn"); float hz = AudioSettings.outputSampleRate / 2f / spec.Length, sum = 0, cen = 0; int pk = 0;
                for (int i = 2; i < acc.Length; i++) { sum += acc[i]; cen += acc[i] * i * hz; if (acc[i] > acc[pk]) pk = i; }
                Log($"buzina {h} (clipe {clip.name}, {clip.length:0.00} s): pico {pk * hz:0} Hz · centróide {cen / Mathf.Max(1e-9f, sum):0} Hz"); yield return Wait(.25f);
            }
            if (pol) Destroy(pol.gameObject);

            // ---- 4. pneus: carro comum na pista (asfalto) e depois na terra
            string cid = GameData.Vehicles.Keys.FirstOrDefault(v => !v.Contains("Viatura") && FBRegistry.I.Vehicle(v) && Json.S(GameData.Vehicles[v] as Dictionary<string, object>, "kind") != "moto" && new[] { "GolG5", "Up", "GolG2", "CivicG10" }.Contains(v))
                ?? GameData.Vehicles.Keys.FirstOrDefault(v => !v.Contains("Viatura") && FBRegistry.I.Vehicle(v));
            foreach (var chao in new[] { "asfalto", "terra" })
            {
                bc.gameObject.name = chao == "terra" ? "FB_SomPad_terra" : "FB_SomPad"; Physics.SyncTransforms();
                var car = FBVehicles.Spawn(cid, p0 + new Vector3(0, .3f, -300), 0); yield return Wait(1); if (!car) { Log("carro não nasceu " + cid); break; }
                if (cc) cc.enabled = false; me.transform.position = car.transform.position - car.transform.right * 1.5f; if (cc) cc.enabled = true; yield return Wait(.3f);
                if (!FBDrive.Enter(me, car)) { Log("não entrou"); break; }
                yield return Wait(1.5f); ear.SetActive(false); var gcam = Camera.main; foreach (var l in FindObjectsByType<AudioListener>(FindObjectsInactive.Include)) if (l.gameObject != ear) l.enabled = true;
                FBPlayer.TestInput = inp => { inp.move = new Vector2(-cSt, cThr); inp.jumpHeld = cHb; inp.interact = false; inp.multimidia = false; return inp; };
                cThr = 1; for (float t = 0; t < 12 && car.v * 3 < 70; t += Time.deltaTime) yield return null; cThr = 0; yield return Wait(.3f);
                played.Clear(); cThr = -1; yield return Wait(.15f); cThr = 0; float maxSk = 0; for (float t = 0; t < .8f; t += Time.deltaTime) { maxSk = Mathf.Max(maxSk, FBAudio.LoopLevel("veh_skid"), FBAudio.LoopLevel("veh_skid_dirt")); yield return null; }
                Log($"{chao}: freada LEVE (toque 0,15 s) a {car.v * 3:0} km/h -> sons {(played.Count == 0 ? "nenhum" : string.Join(",", played.Distinct()))} · atrito máx {maxSk:0.00}");
                cThr = 1; for (float t = 0; t < 8 && car.v * 3 < 70; t += Time.deltaTime) yield return null;
                float v0 = car.v * 3; played.Clear(); cThr = -1; float tb = 0, skA = 0, skD = 0; string skC = null;
                while (car.v > .3f && tb < 8) { tb += Time.deltaTime; skA = Mathf.Max(skA, FBAudio.LoopLevel("veh_skid")); skD = Mathf.Max(skD, FBAudio.LoopLevel("veh_skid_dirt")); skC ??= FBAudio.LoopLevel("veh_skid") > 0 ? FBAudio.LoopClip("veh_skid") : FBAudio.LoopLevel("veh_skid_dirt") > 0 ? FBAudio.LoopClip("veh_skid_dirt") : null; yield return null; }
                yield return Wait(.6f);
                Log($"{chao}: freada BRUSCA de {v0:0} km/h até parar em {tb:0.0} s -> sons {(played.Count == 0 ? "nenhum" : string.Join(",", played))} · atrito asfalto {skA:0.00} · atrito terra {skD:0.00} · loop {skC ?? "-"} · loop agudo antigo reb_brakes {FBAudio.LoopLevel("veh_brake"):0.00}");
                cThr = 0; FBPlayer.TestInput = null; FBDrive.Exit(me, true); yield return Wait(1); if (car) Destroy(car.gameObject); ear.SetActive(true); foreach (var l in FindObjectsByType<AudioListener>(FindObjectsInactive.Include)) if (l.gameObject != ear) l.enabled = false;
            }
            FBAudio.Played = null; Log("fim"); FBSession.map = keepMap; FBSession.mode = keepMode; FBSession.Save(); yield return Wait(1); Application.Quit();
        }
    }
}
