// Entrada unificada (porte do bloco "entrada" do main.js) pelo Input System novo — teclado + mouse, controle (Xbox/PlayStation/genérico)
// e toque (FBTouch). Mesmas teclas do web: WASD, mouse (tiro/mira, roda = troca), Shift = corrida automática (liga/desliga), Espaço pulo
// (buffer), C/Ctrl agachar, Z deitar, R recarregar, H kit médico, X pegar, F veículo/reanimar (segurar), 1-4 armas, I mochila, B loja/
// roda de emotes, T inspecionar, V câmera, Q/E inclinar, Y marcar, G GPS/killstreak, K central multimídia, N arsenal/paredão, Tab placar,
// Esc pausa, Enter chat. Controle: analógicos, RT tiro, LT mira, A pulo, B agachar, X recarregar, Y trocar, LB/RB inclinar, L3 corrida…
using UnityEngine;
using UnityEngine.InputSystem;

namespace FreeBala
{
    public struct FBInputState
    {
        public Vector2 move, look; public bool fire, firePressed, aim, aimPressed, sprintToggle, sprintHeld, crouchToggle, prone, jump, jumpHeld, reload, heal, pickup, interact, interactHeld,
            bag, buy, buyReleased, inspect, toggleView, leanL, leanR, mark, gps, multimidia, arsenal, scoreboard, pause, chat, freeLook, freeLookPressed, mic, map; public int slot, cycle;
    }

    public static class FBInput
    {
        public static bool Touch => FBTouch.Active;
        public static float Sens = .5f, AdsSens = .5f; public static bool InvertY, AimToggle;
        static float r3Down = -1;                                                       // R3 do controle: toque curto = câmera 1ª/3ª, segurar = câmera livre
        static bool aimTog; static float lastPauseFrame = -1;
        static Keyboard K => Keyboard.current; static Mouse Ms => Mouse.current; static Gamepad Gp => Gamepad.current;
        static bool Down(Key k) => K != null && K[k].wasPressedThisFrame; static bool Held(Key k) => K != null && K[k].isPressed; static bool Up(Key k) => K != null && K[k].wasReleasedThisFrame;
        public static bool Locked => Cursor.lockState == CursorLockMode.Locked;
        public static bool PausePressed() { if (Time.frameCount == lastPauseFrame) return false; bool p = Down(Key.Escape) || (Gp != null && Gp.startButton.wasPressedThisFrame); if (p) lastPauseFrame = Time.frameCount; return p; }
        public static bool AnyKeyDown(Key k) => Down(k);

        public static Vector2? TestMove; public static bool TestSprint;
        public static FBInputState Read()
        {
            var s = new FBInputState { slot = -1 };
            bool mapOpen = FBMatchMap.Open;
            bool mPressed = (K != null && K[Key.M].wasPressedThisFrame) || Input.GetKeyDown(KeyCode.M);
            bool escPressed = (K != null && K[Key.Escape].wasPressedThisFrame) || Input.GetKeyDown(KeyCode.Escape);
            if (mPressed || (mapOpen && escPressed)) s.map = true;
            if (Gp != null && Gp.selectButton.wasPressedThisFrame) s.map = true;
            bool ui = FBMatch.Paused || FBShop.Open || FBBag.Open || FBMultimidia.Open || FBOficina.on || mapOpen;
            // teclado
            if (K != null && !ui)
            {
                s.move = new Vector2((Held(Key.D) ? 1 : 0) - (Held(Key.A) ? 1 : 0), (Held(Key.W) ? 1 : 0) - (Held(Key.S) ? 1 : 0));
                // [parceiro] fisica 31/10: no modo SEGURAR SHIFT o Shift só vale enquanto segurado (sprintHeld); o teclado não trava mais o autoRun (L3 e toque continuam alternando)
                s.sprintToggle = !FBSession.holdRun && (Down(Key.LeftShift) || Down(Key.RightShift)) && !Held(Key.LeftMeta) && !Held(Key.RightMeta); s.sprintHeld = Held(Key.LeftShift) || Held(Key.RightShift);
                s.jump = Down(Key.Space); s.jumpHeld = Held(Key.Space); s.crouchToggle = Down(Key.C) || Down(Key.LeftCtrl); s.prone = Down(Key.Z);
                s.reload = Down(Key.R); s.heal = Down(Key.H); s.pickup = Down(Key.X); s.interact = Down(Key.F); s.interactHeld = Held(Key.F); s.bag = Down(Key.I); s.buy = Down(Key.B); s.buyReleased = Up(Key.B);
                s.inspect = Down(Key.T); s.toggleView = Down(Key.V); s.leanL = Held(Key.Q); s.leanR = Held(Key.E); s.mark = Down(Key.Y); s.gps = Down(Key.G); s.multimidia = Down(Key.K); s.arsenal = Down(Key.N);
                s.scoreboard = Held(Key.Tab); s.chat = Down(Key.Enter) || Down(Key.NumpadEnter); s.freeLook = Held(Key.LeftAlt) || Held(Key.RightAlt); s.freeLookPressed = Down(Key.LeftAlt) || Down(Key.RightAlt); s.mic = Down(Key.M);
                if (Down(Key.Digit1)) s.slot = 0; if (Down(Key.Digit2)) s.slot = 1; if (Down(Key.Digit3)) s.slot = 2; if (Down(Key.Digit4)) s.slot = 3; if (Down(Key.Digit5)) s.slot = 4;
            }
            // mouse (só com o cursor preso no jogo)
            if (Ms != null && !ui)
            {
                if (Locked)
                {
                    float k = .0022f * (.4f + Sens * 1.2f) * Mathf.Rad2Deg;
                    var d = Ms.delta.ReadValue(); s.look = new Vector2(d.x, d.y * (InvertY ? -1 : 1)) * k;
                    s.fire = Ms.leftButton.isPressed; s.firePressed = Ms.leftButton.wasPressedThisFrame;
                    if (Ms.rightButton.wasPressedThisFrame) { s.aimPressed = true; if (AimToggle) aimTog = !aimTog; }
                    s.aim = AimToggle ? aimTog : Ms.rightButton.isPressed; if (Ms.middleButton.wasPressedThisFrame) s.mark = true;
                    float w = Ms.scroll.ReadValue().y; s.cycle = w > .1f ? -1 : w < -.1f ? 1 : 0;
                }
                else if (Ms.leftButton.wasPressedThisFrame && !Touch && FBMatch.I && FBMatch.I.state == "play" && !FBHUD.CardOpen && !FBGarage.Open_ && !mapOpen) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            }
            // controle
            if (Gp != null && !ui)
            {
                var ls = Gp.leftStick.ReadValue(); float lm = ls.magnitude; if (lm > .22f) s.move = ls / lm * Mathf.Clamp01((lm - .22f) / .78f);   // zona morta radial 22 % (analógico com folga não anda sozinho)
                var rs = Gp.rightStick.ReadValue(); if (rs.sqrMagnitude > .01f) s.look += new Vector2(rs.x, rs.y * (InvertY ? -1 : 1)) * (140 + Sens * 220) * Time.deltaTime * Mathf.Sign(rs.magnitude) * rs.magnitude;
                s.fire |= Gp.rightTrigger.isPressed; s.firePressed |= Gp.rightTrigger.wasPressedThisFrame; s.aim |= Gp.leftTrigger.isPressed; s.aimPressed |= Gp.leftTrigger.wasPressedThisFrame;
                s.jump |= Gp.buttonSouth.wasPressedThisFrame; s.jumpHeld |= Gp.buttonSouth.isPressed; s.crouchToggle |= Gp.buttonEast.wasPressedThisFrame; s.reload |= Gp.buttonWest.wasPressedThisFrame;
                if (Gp.buttonNorth.wasPressedThisFrame) s.cycle = 1; s.sprintToggle |= Gp.leftStickButton.wasPressedThisFrame; s.leanL |= Gp.leftShoulder.isPressed; s.leanR |= Gp.rightShoulder.isPressed;
                s.interact |= Gp.dpad.up.wasPressedThisFrame; s.interactHeld |= Gp.dpad.up.isPressed; s.pickup |= Gp.dpad.down.wasPressedThisFrame; s.heal |= Gp.dpad.left.wasPressedThisFrame; s.buy |= Gp.dpad.right.wasPressedThisFrame; s.buyReleased |= Gp.dpad.right.wasReleasedThisFrame;
                // R3: segurar (> 0,25 s) = câmera livre; toque curto = troca 1ª/3ª pessoa (ao soltar)
                if (Gp.rightStickButton.wasPressedThisFrame) r3Down = Time.unscaledTime;
                bool r3Hold = Gp.rightStickButton.isPressed && r3Down >= 0 && Time.unscaledTime - r3Down > .25f;
                if (r3Hold) { s.freeLook = true; if (Time.unscaledTime - r3Down - Time.unscaledDeltaTime <= .25f) s.freeLookPressed = true; }
                if (Gp.rightStickButton.wasReleasedThisFrame) { if (r3Down >= 0 && Time.unscaledTime - r3Down <= .25f) s.toggleView = true; r3Down = -1; }
                s.scoreboard |= Gp.selectButton.isPressed; s.prone |= Gp.buttonEast.wasPressedThisFrame && Gp.buttonEast.isPressed && Gp.leftShoulder.isPressed;
            }
            // toque
            if (Touch && !ui)
            {
                var t = FBTouch.I; s.move = t.move; s.look += t.look * .12f; s.fire |= t.fire; s.firePressed |= t.firePressed; s.aim |= t.aim; s.jump |= t.jump; s.crouchToggle |= t.crouchPressed;
                s.reload |= t.reload; s.sprintToggle |= t.sprint; if (t.swap) s.cycle = 1; s.interact |= t.interact; s.interactHeld |= t.interactHeld; s.pickup |= t.pickup; s.heal |= t.heal; s.buy |= t.buy; s.toggleView |= t.view; s.prone |= t.prone; s.freeLook |= t.freeLook; s.freeLookPressed |= t.freeLookPressed; s.map |= t.mapPressed; t.EndFrame();
            }
            // [parceiro] fisica 01/11: com a loja aberta na fase de compra o teclado e o controle eram ignorados, então o B (e o D-pad
            // direito) não fechava a loja apesar do botão "FECHAR [B]". Agora só o B passa, para fechar (FBPlayer: Show(!Open)).
            if (FBShop.Open && (FBEconomy.buying || FBShop.Store) && !FBMatch.Paused && !FBBag.Open && !FBMultimidia.Open && !FBOficina.on && !mapOpen)   // r68: + loja do De Rolê
            {
                if (K != null) { s.buy |= Down(Key.B); s.buyReleased |= Up(Key.B); }
                if (Gp != null) { s.buy |= Gp.dpad.right.wasPressedThisFrame; s.buyReleased |= Gp.dpad.right.wasReleasedThisFrame; }
            }
            if (TestMove is Vector2 tm) { s.move = tm; s.sprintHeld |= TestSprint; }   // testes automatizados
            return s;
        }
    }
}
