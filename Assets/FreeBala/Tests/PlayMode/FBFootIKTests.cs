// Foot IK e física de chão na pista de teste (FBFootIKTestBed — a mesma da cena IKTest), a 60 quadros fixos (Time.captureFramerate):
//  - FootIK_Pista: caminhantes em todas as rotas por 14 s; mede pé apoiado × chão (penetração/flutuação < 3 cm), deslizamento do pé
//    travado e tremor do quadril (2ª diferença da altura por quadro); falha fora do limite e loga o relatório por rota.
//  - Motor_*: cápsula sem modelo (não precisa de personagens importados) descendo/subindo escada e rampa com o FBMotor — não pode
//    "flutuar" degrau abaixo (perder o chão) nem travar no degrau.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FreeBala.Tests
{
    public class FBFootIKTests
    {
        // limites (metros, por quadro a 60 q/s)
        const float ContactMax = .03f, SlipMax = .15f, JitterP95 = .008f, JitterRouteP95 = .015f;

        Scene scene;
        IEnumerator Setup()
        {
            scene = SceneManager.CreateScene("IKTest_auto_" + Random.Range(0, 99999)); SceneManager.SetActiveScene(scene);
            Time.captureFramerate = 60; FBFootIK.Q.IgnoreVisibility = true; FBFootIK.Q.NearDist = FBFootIK.Q.FarDist = 999;   // todas as rotas no nível perto (a câmera segue só uma)
            FBFootIK.Q.Lock = FBFootIK.Q.Toe = FBFootIK.Q.Tilt = true; FBQuality.FootIK = true;
            yield return null;
        }
        IEnumerator TearDown()
        {
            Time.captureFramerate = 0; FBFootIK.Q.IgnoreVisibility = false; FBFootIK.Q.NearDist = FBFootIK.Q.FarDist = -1;
            if (scene.IsValid() && SceneManager.sceneCount > 1) { var op = SceneManager.UnloadSceneAsync(scene); while (op != null && !op.isDone) yield return null; }
        }
        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        [UnityTest, Timeout(180000)]
        public IEnumerator FootIK_Pista()
        {
            if (!FBRegistry.I || FBRegistry.I.charIds.Count == 0) Assert.Ignore("sem personagens importados (FBRegistry)");
            yield return Setup();
            var bed = new GameObject("TesteFootIK").AddComponent<FBFootIKTestBed>();
            yield return Frames(3);
            Assert.Greater(bed.actors.Count, 0, "nenhum caminhante nasceu");
            var p0 = bed.actors.ToDictionary(a => a, a => a.Pos); var ikOn = bed.actors.ToDictionary(a => a, a => false); var far = bed.actors.ToDictionary(a => a, a => 0f);
            yield return Frames(120);                                                    // aterrissar, aprender altura de descanso
            foreach (var m in bed.meters.Values) m.Reset();
            for (int f = 0; f < 14 * 60; f++) { yield return null; foreach (var a in bed.actors) { if (!a) continue; if (a.feet && a.feet.Weight > .9f) ikOn[a] = true; far[a] = Mathf.Max(far[a], (a.Pos - p0[a]).Flat().magnitude); } }

            var all = new FBFootIKMeter(); var bad = new List<string>();
            foreach (var a in bed.actors)
            {
                if (!a) continue; var m = bed.meters[a];
                Debug.Log($"[FreeBala][ik] {a.displayName}: {m.Report()} · {a.feet?.DebugText()}");
                all.contact.AddRange(m.contact); all.slip.AddRange(m.slip); all.jitter.AddRange(m.jitter); all.edgeSkips += m.edgeSkips;
                if (a.Pos.y < -1) bad.Add($"{a.displayName}: caiu do chão (y {a.Pos.y:0.00})");
                if (far[a] < 1.5f) bad.Add($"{a.displayName}: não andou");
                if (!ikOn[a]) bad.Add($"{a.displayName}: IK nunca ligou");
                if (m.jitter.Count > 30 && FBFootIKMeter.P(m.jitter, .95f) > JitterRouteP95) bad.Add($"{a.displayName}: tremor do quadril p95 {FBFootIKMeter.P(m.jitter, .95f) * 1000:0.0} mm/q²");
            }
            Debug.Log("[FreeBala][ik] TOTAL: " + all.Report());
            Assert.Greater(all.contact.Count, 200, "poucas amostras de pé apoiado");
            if (FBFootIKMeter.P(all.contact, .05f) < -ContactMax) bad.Add($"pé entra no chão: p05 {FBFootIKMeter.P(all.contact, .05f) * 100:0.0} cm");
            if (FBFootIKMeter.P(all.contact, .95f) > ContactMax) bad.Add($"pé flutua: p95 {FBFootIKMeter.P(all.contact, .95f) * 100:0.0} cm");
            if (all.slip.Count > 0 && FBFootIKMeter.P(all.slip, .95f) > SlipMax) bad.Add($"pé travado desliza: p95 {FBFootIKMeter.P(all.slip, .95f) * 100:0.0} cm/s");
            if (FBFootIKMeter.P(all.jitter, .95f) > JitterP95) bad.Add($"tremor do quadril: p95 {FBFootIKMeter.P(all.jitter, .95f) * 1000:0.0} mm/q²");
            var dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../logs")); System.IO.Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "print_footik.png")); yield return null; yield return null;
            yield return TearDown();
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        // ---- física de chão sem personagem
        static CharacterController Capsule(Vector3 at)
        {
            var g = new GameObject("CapsulaTeste"); g.layer = FBWorld.LChar; g.transform.position = at;
            var cc = g.AddComponent<CharacterController>(); cc.height = 1.8f; cc.radius = .32f; cc.center = new Vector3(0, .9f, 0); cc.stepOffset = .45f; cc.slopeLimit = 50; cc.skinWidth = .04f; cc.minMoveDistance = 0;
            return cc;
        }
        // anda em linha reta com gravidade; devolve (quadros sem chão depois de pousar, y final, maior sequência sem chão)
        static IEnumerator Walk(CharacterController cc, Vector3 vel, float secs, System.Action<int, float, int> done)
        {
            bool ground = true, landed = false; float vy = 0, slide = 0; int air = 0, run = 0, maxRun = 0; float step = cc.stepOffset;
            for (float t = 0; t < secs; t += Time.deltaTime)
            {
                yield return null; float dt = Time.deltaTime;
                vy = ground && vy < 0 ? -2 : vy - 35 * dt;
                FBMotor.Move(cc, (vel + Vector3.up * vy) * dt, ground, vy > 0, step, .45f, ref slide, out ground, out _);
                if (ground) { landed = true; run = 0; } else if (landed) { air++; run++; maxRun = Mathf.Max(maxRun, run); }
            }
            done(air, cc.transform.position.y, maxRun);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Motor_EscadaERampa()
        {
            yield return Setup();
            FBFootIKTestBed.BuildCourse(); Physics.SyncTransforms(); yield return null;
            var bad = new List<string>();
            // desce a escada correndo (5 m/s): do patamar (1,8 m) até o chão
            var cc = Capsule(new Vector3(6.2f, 1.85f, 8.5f)); int air = 0, maxRun = 0; float y = 0;
            yield return Walk(cc, new Vector3(0, 0, 5), 2.2f, (a, yy, m) => { air = a; y = yy; maxRun = m; });
            Debug.Log($"[FreeBala][ik] motor desce escada: quadros sem chão {air}, maior sequência {maxRun}, y final {y:0.00}");
            if (maxRun > 2) bad.Add($"desce escada flutuando ({maxRun} quadros seguidos sem chão)"); if (Mathf.Abs(y) > .1f) bad.Add($"não chegou ao chão (y {y:0.00})");
            Object.Destroy(cc.gameObject); yield return null;
            // sobe a escada andando (1,6 m/s): tem que chegar ao patamar
            cc = Capsule(new Vector3(6.2f, .05f, 2)); yield return Walk(cc, new Vector3(0, 0, 1.6f), 4.2f, (a, yy, m) => { y = yy; });
            Debug.Log($"[FreeBala][ik] motor sobe escada: y final {y:0.00} (patamar 1,80)");
            if (y < 1.7f) bad.Add($"travou subindo a escada (y {y:0.00})");
            Object.Destroy(cc.gameObject); yield return null;
            // desce a rampa de 22° correndo: sem perder o chão
            cc = Capsule(new Vector3(12, 1.75f, 12.3f)); yield return Walk(cc, new Vector3(0, 0, 5), 1.6f, (a, yy, m) => { air = a; y = yy; maxRun = m; });
            Debug.Log($"[FreeBala][ik] motor desce rampa 22°: quadros sem chão {air}, maior sequência {maxRun}, y final {y:0.00}");
            if (maxRun > 1) bad.Add($"desce rampa flutuando ({maxRun} quadros seguidos sem chão)");
            Object.Destroy(cc.gameObject); yield return null;
            // obstáculo de 60 cm (acima do stepOffset): não pode subir
            cc = Capsule(new Vector3(42.4f, .05f, 10.5f)); yield return Walk(cc, new Vector3(0, 0, 2), 1.5f, (a, yy, m) => { y = yy; });
            if (y > .3f) bad.Add($"subiu obstáculo de 60 cm (y {y:0.00})");
            Object.Destroy(cc.gameObject);
            yield return TearDown();
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }
    }
}
