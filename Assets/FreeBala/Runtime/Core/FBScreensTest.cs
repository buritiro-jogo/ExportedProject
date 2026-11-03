// Autoteste das telas novas (rodada 38): FreeBala.exe -fbtelas [mapa:modo]
// Abre a partida, o jogador é abatido por um bot inimigo (tela de ABATE: quem abateu, arma, distância, anel de renascer) e depois a partida
// termina em vitória e em derrota (tela de FIM: placar, desempenho, destaques, recompensa). Prints em autoteste/tela_*.png.
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBScreensTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbtelas"); if (i < 0) return;
            var g = new GameObject("FB_TesteTelas"); DontDestroyOnLoad(g); var t = g.AddComponent<FBScreensTest>();
            if (i + 1 < a.Length && !a[i + 1].StartsWith("-")) { var kv = a[i + 1].Split(':'); t.map = kv[0]; t.mode = kv.Length > 1 ? kv[1] : "tatico"; }
        }
        string map = "vila_jardim", mode = "tatico";
        static void Log(string s) => Debug.Log("[FreeBala][telas] " + s);
        IEnumerator Shot(string dir, string name) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name)); yield return null; yield return null; Log("print " + name); }
        IEnumerator Start()
        {
            var dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste"); System.IO.Directory.CreateDirectory(dir);
            yield return new WaitForSeconds(3);
            GameData.Load(); FBSession.Load(); var keepMap = FBSession.map; var keepMode = FBSession.mode;
            FBSession.map = map; FBSession.mode = mode; FBSession.online = false; FBSession.view = "tps"; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_" + map); while (op != null && !op.isDone) yield return null;
            for (float w = 0; w < 8; w += Time.unscaledDeltaTime) yield return null;
            var P = FBMatch.Player; var killer = FBMatch.Bots.Where(b => b && b.alive && b.team != P.team).OrderBy(b => (b.Pos - P.Pos).sqrMagnitude).FirstOrDefault();
            Log($"jogador {(P ? P.Name : "-")} · quem vai abater: {(killer ? killer.Name : "ninguém")}");
            // alguns abates para os números da tela não ficarem zerados
            FBMatch.Stats.kills = 7; FBMatch.Stats.deaths = 2; FBMatch.Stats.heads = 3; FBMatch.Stats.shots = 140; FBMatch.Stats.hits = 61;
            for (int k = 0; k < 6 && P.alive; k++) { FBCombat.DamageActor(P, 999, k == 0, killer, "gun"); yield return new WaitForSeconds(.3f); }
            Log($"jogador vivo {P.alive} · estado {FBMatch.I.state}");
            for (float w = 0; w < 4.2f; w += Time.unscaledDeltaTime) yield return null;
            yield return Shot(dir, "tela_abate.png");
            for (float w = 0; w < 1.5f; w += Time.unscaledDeltaTime) yield return null;
            yield return Shot(dir, "tela_abate_2.png");
            FBMatch.I.Respawn();
            // rodada 39: bolha de proteção nova — entrada, meio e fim, na câmera do jogo e de perto pela frente
            var cam2 = new GameObject("CamBolha").AddComponent<Camera>(); cam2.depth = 50; cam2.fieldOfView = 40; cam2.nearClipPlane = .05f; cam2.enabled = false;
            IEnumerator Both(string n)
            {
                yield return Shot(dir, n + "_jogo.png");
                var c = P.Pos + Vector3.up * 1.1f * P.ScaleK; cam2.transform.position = c + Quaternion.Euler(0, P.Yaw + 25, 0) * Vector3.forward * 3.4f * P.ScaleK + Vector3.up * .5f * P.ScaleK; cam2.transform.LookAt(c);
                cam2.enabled = true; yield return Shot(dir, n + "_perto.png"); cam2.enabled = false;
            }
            for (float w = 0; w < .18f; w += Time.unscaledDeltaTime) yield return null; yield return Both("bolha_1_entrada");
            for (float w = 0; w < .9f; w += Time.unscaledDeltaTime) yield return null; yield return Both("bolha_2_meio");
            Log($"proteção até {P.protUntil - Time.time:0.00} s");
            while (P.protUntil - Time.time > .3f) yield return null; yield return Both("bolha_3_fim");
            Log($"renasceu: vivo {P.alive} · estado {FBMatch.I.state} · tela aberta {FBHUD.CardOpen}");
            // rodada 39: sacar o fuzil das costas — a cópia das costas some quando a mão pega, nunca duas ao mesmo tempo
            var wu = P.GetComponent<FBWeaponUser>(); int rs = -1; for (int k = 0; k < 3; k++) if (wu.slots[k] != null && wu.slots[k].cls == "fuzil") rs = k;
            if (rs >= 0)
            {
                wu.Equip(FBWeaponUser.HANDS, true); for (float w = 0; w < 1.2f; w += Time.unscaledDeltaTime) yield return null;
                string rid = wu.slots[rs].id; wu.SwitchTo(rs); int dup = 0, frames = 0;
                for (float w = 0; w < 1.6f; w += Time.unscaledDeltaTime)
                {
                    yield return null; frames++;
                    bool hand = P.weaponGO && P.weaponGO.activeInHierarchy && P.weapon != null && P.weapon.id == rid; bool back = P.HolsterObjects.Any(o => o && o.activeInHierarchy && o.name == "Coldre_" + rid);
                    if (hand && back) dup++;
                    if (frames % 12 == 0) { var c = P.Pos + Vector3.up * 1.2f * P.ScaleK; cam2.transform.position = c + Quaternion.Euler(0, P.Yaw + 150, 0) * Vector3.forward * 2.6f * P.ScaleK; cam2.transform.LookAt(c); cam2.enabled = true; yield return Shot(dir, $"saque_{frames:000}.png"); cam2.enabled = false; }
                }
                Log($"saque do fuzil: {dup} de {frames} quadros com DUAS armas (mão + costas)");
            }
            Destroy(cam2.gameObject);
            FBMatch.I.EndMatch(true, "CONQUISTA", 250, 187);
            for (float w = 0; w < 8; w += Time.unscaledDeltaTime) yield return null;
            yield return Shot(dir, "tela_fim_vitoria.png");
            FBSession.map = keepMap; FBSession.mode = keepMode; FBSession.Save();
            Log("fim"); Application.Quit(0);
        }
    }
}
