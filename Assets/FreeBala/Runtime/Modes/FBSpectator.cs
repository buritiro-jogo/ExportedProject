// r49: MODO ESPECTADOR depois da morte (pedido de 30/10: "acompanhar outros jogadores vivos e alternar entre eles, câmera estável e
// suave, sem atravessar objetos, sem conflito com a câmera original; integrado com morte, eliminação, reanimação e renascimento de cada
// modo; o De Rolê continua como está").
//  · Modos sem renascer (4V4, Farinha × Buriti, Battle Royale): depois do "ABATIDO POR" (2 s) segue os vivos até a rodada nova (ResetRound)
//    ou o fim da partida. Modos com renascer (Tático, Clássico, Todos × Todos, Treino, Online): a tela de abate abre como antes e, ao mesmo
//    tempo, a câmera passa para um vivo; 3 s depois a tela recolhe numa faixa (contagem de renascer + ESPAÇO renasce) e o espectador continua
//    até renascer. De Rolê (mode.role): nada disto — a morte segue o fluxo de antes.
//  · Quem: em time, só os aliados vivos (se não sobrar nenhum, quem abateu — a câmera não fica parada no corpo); Todos × Todos e Battle
//    Royale: todos os vivos, começando por quem abateu. Alvo que morre: 1,5 s vendo a queda e passa ao próximo.
//  · Trocar: botão esquerdo / D / → / roda para baixo = próximo; botão direito / A / ← / roda para cima = anterior.
//  · Câmera: a 3ª pessoa original do FBPlayer (braço, pivô, colisão com recuperação) com o alvo trocado; na troca o pivô viaja do alvo
//    antigo ao novo em 0,45 s (sem corte seco) e o rumo da câmera vira para as costas do novo alvo.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace FreeBala
{
    public static class FBSpectator
    {
        public static bool On { get; private set; }
        public static FBCharacter Target { get; private set; }
        public static int Switches, AutoSwitches; public static string Diag = "-";     // laboratório
        static FBCharacter killer, lastCamT; static float onT, deadT = -1, swT = 9; static Vector3 fromPiv, lastPiv; static bool pivOk, collapsed;
        static GameObject ui; static Text nameT, infoT, hintT, topT; static Image hpFill, panel;

        public static bool Allowed => FBMatch.I && FBMatch.I.mode != null && !FBMatch.I.mode.role && FBMatch.Player && !FBMatch.Player.alive;
        static bool TeamMode => !FBMatch.FFA && !(FBMatch.I && FBMatch.I.mode != null && (FBMatch.I.mode.ffa || FBMatch.I.mode.br));

        /// <summary>Vivos que podem ser seguidos, na ordem de troca (estável: por nome).</summary>
        public static List<FBCharacter> Candidates()
        {
            var me = FBMatch.Player; var all = FBMatch.All.Where(a => a && a != me && a.alive && !a.civ && a.gameObject.activeInHierarchy).OrderBy(a => a.Name).ToList();
            if (!TeamMode || !me) return all;
            var mates = all.Where(a => a.team == me.team).ToList(); if (mates.Count > 0) return mates;
            return killer && killer.alive && killer != me ? new List<FBCharacter> { killer } : new List<FBCharacter>();
        }
        public static void Begin(FBCharacter by)
        {
            if (!Allowed) return; killer = by; onT = 0; deadT = -1; collapsed = false;
            var c = Candidates(); var first = !TeamMode && by && c.Contains(by) ? by : c.FirstOrDefault();
            On = true; Set(first, false); Ui(true);
        }
        public static void End()
        {
            On = false; Target = null; killer = null; deadT = -1; pivOk = false; lastCamT = null; if (ui) ui.SetActive(false);
        }
        static void Set(FBCharacter t, bool user)
        {
            if (t == Target) return; Target = t; deadT = -1; if (user) Switches++; else if (t) AutoSwitches++;
            if (t && FBPlayer.I) FBPlayer.I.SpectateLook(t.Yaw);                           // câmera vira para as costas do novo alvo
            Diag = t ? $"{t.Name} ({t.skinId})" : "ninguém";
        }
        static void Step(int dir)
        {
            var c = Candidates(); if (c.Count == 0) { Set(null, true); return; }
            int i = Target ? c.IndexOf(Target) : -1; i = i < 0 ? (dir > 0 ? 0 : c.Count - 1) : (i + dir + c.Count) % c.Count; Set(c[i], true);
        }
        static float lastMoveX;
        /// <summary>Uma vez por quadro (FBPlayer.Update) enquanto o jogador está morto.</summary>
        public static void Tick(FBInputState inp, float dt)
        {
            if (!On) return; if (!Allowed) { End(); return; }
            onT += dt;
            // tela de abate dos modos com renascer: recolhe numa faixa depois de 3 s (a contagem continua na faixa)
            if (!collapsed && onT > 3f && FBMatch.I.state == "dead") { collapsed = true; FBHUD.I?.CloseCards(); }
            if (collapsed || FBMatch.I.state == "spect") LockCursor();
            bool cardOpen = FBHUD.CardOpen;
            if (!cardOpen)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current; float mx = inp.move.x; int dir = 0;
                if (inp.firePressed || inp.cycle > 0 || (kb != null && kb.rightArrowKey.wasPressedThisFrame) || (mx > .6f && lastMoveX <= .6f)) dir = 1;
                else if (inp.aimPressed || inp.cycle < 0 || (kb != null && kb.leftArrowKey.wasPressedThisFrame) || (mx < -.6f && lastMoveX >= -.6f)) dir = -1;
                lastMoveX = mx; if (dir != 0) Step(dir);
                if (FBMatch.I.state == "dead" && kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) { FBMatch.I.Respawn(); return; }
            }
            // alvo morreu / sumiu: 1,5 s vendo a queda e passa ao próximo; sem alvo, pega o primeiro que aparecer
            if (Target && (!Target.alive || !Target.gameObject.activeInHierarchy)) { if (deadT < 0) deadT = 0; deadT += dt; if (deadT > 1.5f) { var c = Candidates(); Set(c.FirstOrDefault(), false); } }
            else if (!Target) { var c = Candidates(); if (c.Count > 0) Set(c[0], false); }
            Hud();
        }
        static void LockCursor() { if (!FBInput.Touch && !FBMatch.Paused && !FBHUD.CardOpen && Cursor.lockState != CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } }
        /// <summary>Quem a câmera de 3ª pessoa segue: o alvo vivo do espectador (ou que acabou de cair, até a troca), senão o próprio jogador.</summary>
        public static FBCharacter CamTarget(FBCharacter me) => On && Target && Target.gameObject.activeInHierarchy ? Target : me;
        /// <summary>Pivô suavizado na troca de alvo (0,45 s, sem corte seco).</summary>
        public static Vector3 Pivot(FBCharacter t, Vector3 piv, float dt)
        {
            if (t != lastCamT) { fromPiv = pivOk ? lastPiv : piv; swT = 0; lastCamT = t; }
            swT += dt; float k = Mathf.SmoothStep(0, 1, swT / .45f); var p = Vector3.Lerp(fromPiv, piv, k); lastPiv = p; pivOk = true; return p;
        }

        // ---- faixa do espectador (embaixo) e da contagem de renascer (em cima)
        static void Ui(bool on)
        {
            if (!ui)
            {
                var cv = FBUI.Canvas("FB_Espectador", 40); ui = cv.gameObject; Object.DontDestroyOnLoad(ui); var r = cv.transform;
                panel = FBUI.Panel(r, "faixa", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 92), new Vector2(560, 104), new Color(.03f, .04f, .05f, .78f));
                var p = panel.transform;
                FBUI.Label(p, "ESPECTANDO", 13, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -14), new Vector2(520, 18), TextAnchor.MiddleCenter, false, FBUI.Mut);
                nameT = FBUI.Label(p, "", 30, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -42), new Vector2(520, 36), TextAnchor.MiddleCenter, true, Color.white);
                var bar = FBUI.Panel(p, "vida", new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-60, -70), new Vector2(220, 8), new Color(1, 1, 1, .12f));
                hpFill = FBUI.Panel(bar.transform, "fill", new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, Vector2.zero, new Color(.35f, .9f, .45f)); hpFill.rectTransform.pivot = new Vector2(0, .5f);
                infoT = FBUI.Label(p, "", 14, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(120, -70), new Vector2(200, 18), TextAnchor.MiddleLeft, false, new Color(.86f, .88f, .9f));
                hintT = FBUI.Label(r, "", 14, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 26), new Vector2(900, 20), TextAnchor.MiddleCenter, false, new Color(.86f, .88f, .9f));
                topT = FBUI.Label(r, "", 24, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -96), new Vector2(900, 32), TextAnchor.MiddleCenter, true, FBUI.Gold);
                foreach (var g in ui.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            }
            ui.SetActive(on);
        }
        static void Hud()
        {
            if (!ui) return; bool show = !FBHUD.CardOpen && FBMatch.I && FBMatch.I.state != "end"; ui.SetActive(show); if (!show) return;
            var t = Target; var c = Candidates();
            if (t)
            {
                bool mate = FBMatch.Player && t.team == FBMatch.Player.team && TeamMode;
                nameT.text = t.Name.ToUpper(); nameT.color = !TeamMode ? Color.white : mate ? new Color(.45f, .72f, 1) : new Color(1, .42f, .38f);
                hpFill.rectTransform.sizeDelta = new Vector2(220 * Mathf.Clamp01(t.hp / Mathf.Max(1, t.maxHp)), 0);
                infoT.text = !t.alive ? "ABATIDO" : t.knocked ? "CAÍDO" : t.weapon != null ? t.weapon.name.ToUpper() : "SEM ARMA";
            }
            else { nameT.text = "NINGUÉM VIVO"; nameT.color = FBUI.Mut; hpFill.rectTransform.sizeDelta = Vector2.zero; infoT.text = ""; }
            int idx = t ? c.IndexOf(t) + 1 : 0;
            hintT.text = c.Count > 1 ? $"◀  BOTÃO DIREITO · A · ←      {idx}/{c.Count}      BOTÃO ESQUERDO · D · →  ▶" : c.Count == 1 ? "ÚNICO JOGADOR VIVO PARA ACOMPANHAR" : "";
            var M = FBMatch.I; if (M.state == "dead") topT.text = M.RespawnLeft > 0 ? $"RENASCENDO EM {Mathf.CeilToInt(M.RespawnLeft)}   ·   [ESPAÇO] RENASCER AGORA" : "[ESPAÇO] RENASCER";
            else topT.text = M.mode != null && M.mode.rounds ? "AGUARDANDO A PRÓXIMA RODADA" : "VOCÊ FOI ELIMINADO";
        }
    }
}
