// Autoteste da CURA PARADA e da PROTEÇÃO DE SPAWN (rodada 45): FreeBala.exe -fbcuralab [mapa:modo]
//  · cura parado (3ª e 1ª pessoa): vida a cada 0,25 s, clipe do corpo, kit gasto;
//  · cancelamentos: andar, pular, correr, atirar, mirar, recarregar, agachar — a vida tem de parar NO quadro (mede 0,6 s depois), o clipe
//    de cura sai e o HUD mostra o motivo; antes do início (0,5 s) o kit não é gasto;
//  · tentar curar andando é recusado;
//  · bot curando: não anda e a vida sobe aos poucos;
//  · proteção de spawn: aura no jogador (3ª pessoa), em aliado e inimigo, borda da tela em 1ª pessoa; prints em autoteste/cura_*.png.
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBCuraLab : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbcuralab"); if (i < 0) return;
            var g = new GameObject("FB_CuraLab"); DontDestroyOnLoad(g); var t = g.AddComponent<FBCuraLab>(); if (i + 1 < a.Length && !a[i + 1].StartsWith("-")) t.run = a[i + 1];
        }
        string run = "vila_jardim:tdm", dir; FBInputState press; bool once; Vector2 hold;
        static void Log(string s) => Debug.Log("[FreeBala][curalab] " + s);
        IEnumerator Wait(float s) { for (float w = 0; w < s; w += Time.deltaTime) yield return null; }
        void Shot(string n) => ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "cura_" + n + ".png"));
        IEnumerator Press(System.Action<FBInputState> _ = null) { yield return null; }
        IEnumerator Start()
        {
            dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste"); System.IO.Directory.CreateDirectory(dir);
            yield return Wait(3); var mm = run.Split(':'); GameData.Load(); FBSession.Load(); var keepMap = FBSession.map; var keepMode = FBSession.mode; var keepView = FBSession.view;
            FBSession.map = mm[0]; FBSession.mode = mm.Length > 1 ? mm[1] : "tdm"; FBSession.online = false; FBSession.view = "tps"; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_" + mm[0]); while (op != null && !op.isDone) yield return null; yield return Wait(6);
            var me = FBMatch.Player; if (!me) { Log("sem jogador"); yield break; }
            var bots = FBMatch.Bots.Where(b => b).ToList(); foreach (var b in bots) b.gameObject.SetActive(false);
            FBPlayer.TestInput = inp => { inp.move = hold; if (once) { once = false; var p = press; p.move = hold; p.slot = press.slot; return p; } inp.slot = -1; inp.cycle = 0; return inp; };
            void Do(System.Func<FBInputState, FBInputState> f) { var s = new FBInputState { slot = -1 }; press = f(s); once = true; }
            me.protUntil = 0; yield return Wait(1);
            foreach (var view in new[] { "tps", "fp" })
            {
                if ((view == "fp") != FBPlayer.I.FirstPerson) { Do(s => { s.toggleView = true; return s; }); yield return Wait(1); }
                Log($"--- {(view == "fp" ? "1ª" : "3ª")} pessoa (FirstPerson {FBPlayer.I.FirstPerson})");
                // 1) cura completa parado
                FBMed.ResetPlayer(4, false); me.hp = 20; hold = Vector2.zero; yield return Wait(.6f);
                Do(s => { s.heal = true; return s; }); yield return null; yield return null;
                string row = ""; float t0 = Time.time; string clips = "";
                while (Time.time - t0 < 3.4f) { row += $"{Time.time - t0:0.00}s:{me.hp:0} "; if (me.sys != null && !clips.Contains(me.sys)) clips += me.sys + " "; yield return Wait(.25f); }
                Log($"{view} cura parado: {row}· kits {FBMed.kits} (antes 4) · clipes {clips}· Busy {FBMed.Busy}"); if (view == "tps") Shot("parado_fim_" + view);
                // 2) cancelamentos
                foreach (var (nome, f) in new (string, System.Func<FBInputState, FBInputState>)[] {
                    ("andar", s => s), ("pular", s => { s.jump = true; return s; }), ("correr", s => { s.sprintHeld = true; s.move = new Vector2(0, 1); return s; }),
                    ("atirar", s => { s.fire = s.firePressed = true; return s; }), ("mirar", s => { s.aim = s.aimPressed = true; return s; }), ("recarregar", s => { s.reload = true; return s; }),
                    ("agachar", s => { s.crouchToggle = true; return s; }) })
                {
                    FBMed.ResetPlayer(4, false); me.hp = 20; hold = Vector2.zero; yield return Wait(.8f);
                    Do(s => { s.heal = true; return s; }); yield return Wait(1.2f); if (nome == "agachar" && view == "tps") Shot("curando_" + view);
                    float hpAt = me.hp; int c0 = FBPlayer.HealCancels; string sysAt = me.sys;
                    if (nome == "andar") hold = new Vector2(0, 1); else if (nome == "correr") hold = new Vector2(0, 1);
                    Do(f); yield return null; yield return null; float hpNext = me.hp; hold = Vector2.zero; yield return Wait(.6f);
                    Log($"{view} cancelar por {nome}: vida na hora {hpAt:0.0} -> 2 quadros {hpNext:0.0} -> +0,6 s {me.hp:0.0} · cancelou {FBPlayer.HealCancels > c0} (motivo {FBPlayer.LastHealCancel}) · Busy {FBMed.Busy} · clipe antes {sysAt} depois {me.sys ?? "-"} · kits {FBMed.kits}");
                    if (nome == "agachar") { Do(s => { s.crouchToggle = true; return s; }); yield return Wait(.5f); }
                    if (nome == "mirar") { hold = Vector2.zero; yield return Wait(.3f); }
                }
                // 3) cancelar antes do início: kit não gasto
                FBMed.ResetPlayer(4, false); me.hp = 20; yield return Wait(.6f); Do(s => { s.heal = true; return s; }); yield return Wait(.25f); hold = new Vector2(0, 1); yield return Wait(.3f); hold = Vector2.zero; yield return Wait(.3f);
                Log($"{view} cancelar a 0,25 s (antes de começar a subir): kits {FBMed.kits} (esperado 4) · vida {me.hp:0} (esperado 20)");
                // 4) tentar curar andando
                hold = new Vector2(0, 1); yield return Wait(1); Do(s => { s.heal = true; return s; }); yield return Wait(.3f); Log($"{view} tentar curar andando: Busy {FBMed.Busy} (esperado False)"); hold = Vector2.zero; yield return Wait(.6f);
            }
            if (FBPlayer.I.FirstPerson) { Do(s => { s.toggleView = true; return s; }); yield return Wait(1); }
            // 5) bot curando
            var bot = bots.FirstOrDefault(); if (bot)
            {
                bot.gameObject.SetActive(true); FBMatch.Teleport(bot, me.Pos + me.transform.forward * 4, me.Yaw + 180); bot.hp = 30; bot.kits = 2; yield return Wait(.5f);
                var fb = bot.GetComponent<FBBot>(); var fm = typeof(FBBot).GetField("medT", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); fm?.SetValue(fb, FBMed.Use);
                var p0 = bot.Pos; string row = ""; for (int k = 0; k < 14; k++) { row += $"{bot.hp:0}/{(bot.Pos - p0).Flat().magnitude:0.00}m "; yield return Wait(.25f); }
                Log($"bot curando (vida/deslocamento a cada 0,25 s): {row}· kits {bot.kits} (antes 2) · clipe {bot.sys}");
            }
            // 6) proteção de spawn: eu (3ª pessoa), aliado, inimigo; 1ª pessoa = borda
            var cam = Camera.main; me.protUntil = Time.time + 4; yield return Wait(.2f);
            Log($"proteção (3ª pessoa): aura no jogador {FBTactics.I.AuraCount(me)} malhas · anel {FBTactics.I.GetComponentsInChildren<Transform>(true).Count(t => t.name == "anel_protecao" && t.gameObject.activeSelf)} · borda {GameObject.Find("protecao_borda") != null}");
            yield return Wait(.5f); Shot("protecao_tps");
            var ally = bots.FirstOrDefault(b => b && b.team == me.team && b != bot) ?? bot; var foe = bots.FirstOrDefault(b => b && b.team != me.team);
            foreach (var (b, n) in new[] { (ally, "aliado"), (foe, "inimigo") })
            {
                if (!b) continue; b.gameObject.SetActive(true); FBMatch.Teleport(b, me.Pos + me.transform.forward * 3.2f + me.transform.right * (n == "aliado" ? -1 : 1), me.Yaw + 180); b.protUntil = Time.time + 4; yield return Wait(.6f);
                Log($"proteção {n} (time {b.team}, eu {me.team}): aura {FBTactics.I.AuraCount(b)} malhas");
            }
            yield return Wait(.3f); Shot("protecao_aliado_inimigo");
            Do(s => { s.toggleView = true; return s; }); yield return Wait(.8f); me.protUntil = Time.time + 3; yield return Wait(.6f);
            var edge = GameObject.Find("protecao_borda"); Log($"proteção (1ª pessoa): borda ativa {(edge && edge.activeInHierarchy)} · aura no próprio corpo {FBTactics.I.AuraCount(me)} (esperado 0)"); Shot("protecao_fp");
            me.protUntil = 0; yield return Wait(.5f); Log($"proteção acabou: aura {FBTactics.I.AuraCount(me)} · borda {(edge && edge.activeInHierarchy)}");
            Do(s => { s.toggleView = true; return s; }); yield return Wait(.5f);
            FBPlayer.TestInput = null; Log("fim"); FBSession.map = keepMap; FBSession.mode = keepMode; FBSession.view = keepView; FBSession.Save(); yield return Wait(1); Application.Quit();
        }
    }
}
