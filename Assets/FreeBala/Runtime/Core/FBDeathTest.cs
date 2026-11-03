// Autoteste das MORTES (rodada 42): FreeBala.exe -fbmorte [mapa:modo] [-fbflexsign 1|-1]
// Mata personagens de verdade (FBCombat.DamageActor com bala, como no jogo) em situações diferentes e mede o corpo depois que assenta:
//  · 8 direções de tiro em pé · agachado · correndo · tiro na cabeça · explosão · encostado em parede (tiro empurrando para ela);
//  · por morte: clipe escolhido e o porquê (FBDeath.LastWhy), tempo até o ragdoll assentar, altura do quadril sobre o chão (negativo =
//    enterrado, > 0,45 m = boiando), pior dobra de joelho/cotovelo ao contrário durante a física (°, negativo = hiperestendido);
//  · prints autoteste/morte_<caso>_{a,b}.png (no meio da queda e assentado). Log: [FreeBala][morte-teste].
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBDeathTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbmorte"); if (i < 0) return;
            var g = new GameObject("FB_MorteTeste"); DontDestroyOnLoad(g); var t = g.AddComponent<FBDeathTest>();
            t.run = i + 1 < a.Length && !a[i + 1].StartsWith("-") ? a[i + 1] : "vila_jardim:classico";
            int k = System.Array.IndexOf(a, "-fbflexsign"); if (k >= 0 && k + 1 < a.Length) FBRagdoll.FlexForce = FBRagdoll.FlexSign = int.Parse(a[k + 1]);
        }
        string run, dir; Camera cam;
        static void Log(string s) => Debug.Log("[FreeBala][morte-teste] " + s);
        IEnumerator Wait(float s) { for (float w = 0; w < s; w += Time.deltaTime) yield return null; }

        class Case { public string id; public Vector3 shotDir; public bool head, crouch, runFwd, wall; public string why; }

        IEnumerator Start()
        {
            dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste"); System.IO.Directory.CreateDirectory(dir);
            yield return Wait(3);
            var mm = run.Split(':'); GameData.Load(); FBSession.Load(); var keepMap = FBSession.map; var keepMode = FBSession.mode;
            FBSession.map = mm[0]; FBSession.mode = mm.Length > 1 ? mm[1] : "classico"; FBSession.online = false; FBSession.view = "tps"; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_" + mm[0]); while (op != null && !op.isDone) yield return null;
            yield return Wait(5);
            FBMatch.God = true; var P = FBMatch.Player;
            foreach (var b in FBMatch.Bots.ToList()) if (b) { b.gameObject.SetActive(false); }   // sem tiroteio em volta
            var cg = new GameObject("MorteCam"); cam = cg.AddComponent<Camera>(); cam.depth = 100; cam.cullingMask = Camera.main ? Camera.main.cullingMask : ~0; cam.nearClipPlane = .05f;
            Log($"mapa {run} · biblioteca {FBRegistry.I.clips.Count(c => c && c.name.StartsWith("X171_death"))} quedas · FlexSign {FBRagdoll.FlexSign}");

            var cases = new List<Case>();
            for (int k = 0; k < 8; k++) { float a = k * 45; cases.Add(new Case { id = $"pe_{a:000}", shotDir = Quaternion.Euler(0, a, 0) * Vector3.forward }); }
            cases.Add(new Case { id = "agachado_frente", shotDir = Vector3.back, crouch = true });
            cases.Add(new Case { id = "agachado_costas", shotDir = Vector3.forward, crouch = true });
            cases.Add(new Case { id = "correndo_costas", shotDir = Vector3.forward, runFwd = true });
            cases.Add(new Case { id = "correndo_frente", shotDir = Vector3.back, runFwd = true });
            cases.Add(new Case { id = "cabeca_frente", shotDir = Vector3.back, head = true });
            cases.Add(new Case { id = "cabeca_lado", shotDir = Vector3.right, head = true });
            cases.Add(new Case { id = "explosao", shotDir = Vector3.left, why = "explosion" });
            cases.Add(new Case { id = "parede_atras", shotDir = Vector3.back, wall = true });
            cases.Add(new Case { id = "parede_frente", shotDir = Vector3.forward, wall = true });
            string[] skins = { "r_tutu", "r_elaine", "r_giga", "r_mikele", "r_pm_silva", "r_vivi" };
            int bad = 0, n = 0, ci = 0; var picks = new Dictionary<string, int>();
            foreach (var c in cases)
            {
                string skin = skins[ci % skins.Length]; Random.InitState(4200 + ci++);   // mesmo sorteio (clipe) nas duas rodadas de comparação
                // lugar: aberto (ou junto de parede) perto do jogador, no navmesh
                Vector3 spot; float yaw;
                if (!Place(P ? P.Pos : Vector3.zero, c.wall, c.shotDir, out spot, out yaw)) { Log($"{c.id}: sem lugar"); continue; }
                var v = FBMatch.I.Spawn(skin, null, 1, spot, false, false, true);
                foreach (var mb in v.GetComponents<MonoBehaviour>()) if (mb is FBCivilian || mb is FBRoleNpc || mb is FBBot) mb.enabled = false;
                FBMatch.Teleport(v, spot, yaw); v.SetUnarmedLocomotion(true, true); yield return Wait(.6f);
                if (c.crouch) { v.crouch = true; yield return Wait(1.2f); }
                if (c.runFwd) { for (float t = 0; t < 1.4f; t += Time.deltaTime) { v.sprint = true; v.velocity = v.transform.forward * 6.5f; var cc = v.GetComponent<CharacterController>(); if (cc && cc.enabled) cc.Move(v.velocity * Time.deltaTime + Vector3.down * .2f); else v.transform.position += v.velocity * Time.deltaTime; yield return null; } }
                var sd = Quaternion.Euler(0, yaw, 0) * c.shotDir; sd.y = 0; sd.Normalize();
                var aim = c.head && v.Head ? v.Head.position : v.Chest ? v.Chest.position : v.Pos + Vector3.up * 1.2f;
                var hit = new FBHitInfo { valid = c.why == null, point = aim, dir = sd, normal = -sd, part = c.head ? "head" : "body", dist = 12 };
                // câmera de lado do caminho do tiro, olhando o corpo
                Look(v.Pos + Quaternion.Euler(0, 90, 0) * sd * 4.2f + Vector3.up * 1.8f, v.Pos + Vector3.up * .6f, 60);
                FBCombat.DamageActor(v, 999, c.head, null, c.why, hit);
                string pick = v.deathClip ?? "(sem clipe)", why = FBDeath.LastWhy; picks[pick] = picks.TryGetValue(pick, out var pn) ? pn + 1 : 1;
                var rd = v.GetComponent<FBRagdoll>(); float worst = 999; string worstJ = "-"; float t0 = Time.time;
                bool shotA = false; float hipsMin = 99;
                for (float t = 0; t < 13 && !(rd && rd.Done); t += Time.deltaTime)   // r43: clipe (até ~3,5 s) + física (até 6 s) passava dos 9 s
                {
                    if (!shotA && t > .7f) { Snap($"morte_{c.id}_a.png"); shotA = true; }
                    yield return new WaitForEndOfFrame();                                                    // r43b: mede a pose DESENHADA (depois do LateUpdate)
                    if (rd && rd.Simulating) foreach (var h in rd.Hinges()) if (h.flex < worst) { worst = h.flex; worstJ = h.name; }
                }
                yield return Wait(.3f);
                float a0min = 999; string a0j = "-"; if (rd) foreach (var h in rd.HingesAtStart()) if (h.a0 < a0min) { a0min = h.a0; a0j = h.name; }
                float gy = v.Hips && FBWorld.GroundY(v.Hips.position.x, v.Hips.position.y + .4f, v.Hips.position.z, .3f, 2) is float g ? g : v.Pos.y;
                float hipH = v.Hips ? v.Hips.position.y - gy : 0;
                Snap($"morte_{c.id}_b.png"); yield return null;
                bool ok = (rd == null || rd.Done) && worst > -12 && hipH > -.05f && hipH < .45f;
                if (!ok) bad++; n++;
                Log($"{c.id} [{skin}]: {pick} · {why} · assentou {(rd && rd.Done ? $"{rd.SimTime:0.0} s de física" : "NÃO")} em {Time.time - t0:0.0} s · quadril {hipH:0.00} m do chão · pior dobra {(worst > 900 ? "-" : $"{worst:0}° ({worstJ})")} · no início da física {(a0min > 900 ? "-" : $"{a0min:0}° ({a0j})")} {(ok ? "ok" : "<< PROBLEMA")}");
                yield return Wait(.5f);
                Destroy(v.gameObject); FBMatch.All.Remove(v); FBMatch.Civs.Remove(v);
            }
            Log($"fim · {n} mortes · {bad} com problema · clipes distintos {picks.Count} · ragdolls montados {FBRagdoll.Built} assentados {FBRagdoll.Settled} · trava anatômica {FBRagdoll.AnatomyFixes} correções");
            FBSession.map = keepMap; FBSession.mode = keepMode; FBSession.Save();
            yield return Wait(1); Application.Quit();
        }

        // lugar livre: chão plano a 6–25 m do jogador; 'wall' = a 0,55 m de uma parede, com o tiro empurrando o corpo para ela
        bool Place(Vector3 near, bool wall, Vector3 shotLocal, out Vector3 spot, out float yaw)
        {
            spot = near; yaw = 0;
            for (int tries = 0; tries < 400; tries++)
            {
                var p = FBNav.RandomPoint(near, 25) ?? near + Random.insideUnitSphere.Flat() * 20; if ((p - near).Flat().magnitude < 5) continue;
                if (!(FBWorld.GroundY(p.x, p.y + 1, p.z, 1, 3) is float gy)) continue; p.y = gy;
                if (!wall)
                {
                    if (Physics.CheckSphere(p + Vector3.up * 1.1f, 3.2f, FBWorld.SolidMask & ~(1 << FBWorld.LMap), QueryTriggerInteraction.Ignore)) continue;
                    bool open = true; for (int k = 0; k < 8 && open; k++) open = !Physics.Raycast(p + Vector3.up * .9f, Quaternion.Euler(0, k * 45, 0) * Vector3.forward, 4f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore);
                    if (!open) continue;
                    yaw = Random.Range(0f, 360f); spot = p; return true;
                }
                for (int k = 0; k < 8; k++)
                {
                    var d = Quaternion.Euler(0, k * 45, 0) * Vector3.forward;
                    if (!Physics.Raycast(p + Vector3.up * 1f, d, out var h, 2.5f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) || Mathf.Abs(h.normal.y) > .3f) continue;
                    var s = h.point - d * .55f; s.y = p.y; if (!(FBWorld.GroundY(s.x, s.y + 1, s.z, 1, 2) is float g2) || Mathf.Abs(g2 - p.y) > .2f) continue;
                    // empurrão (shotLocal no referencial do corpo) tem de apontar para a parede 'd'
                    var sl = shotLocal.Flat().normalized; float a = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg - Mathf.Atan2(sl.x, sl.z) * Mathf.Rad2Deg;
                    yaw = a; spot = s; return true;
                }
            }
            return false;
        }
        void Look(Vector3 from, Vector3 at, float fov) { cam.transform.position = from; cam.transform.rotation = Quaternion.LookRotation(at - from); cam.fieldOfView = fov; }
        void Snap(string f) => ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, f));
    }
}
