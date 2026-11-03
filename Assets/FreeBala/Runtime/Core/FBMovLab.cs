// r49: LABORATÓRIO DE MOVIMENTO — FreeBala.exe -fbmovlab [-fbmov_partes pulo,deitar,espectador] [-fbmov_fotos 0|1]
// Pedido de 30/10 (rodada 49): "apertar o pulo rápido faz flutuar e andar muito mais rápido"; "deitar/levantar respeitando a duração, sem
// cancelar, cura deitado"; "espectador depois da morte em todos os modos menos o De Rolê". Tudo com entrada injetada (FBPlayer.TestInput),
// numa pista montada no céu do mapa (chão, parede, parede com peitoris, mureta, escada, rampa, plataforma de 4 m, parede para deitar):
//  · pulo: cada cenário roda com FBPlayer.JumpFix = false (antes) e true (agora) — pulos, pulos de janela, altura máxima acima do chão da
//    pista (o pulo sozinho sobe 1,5 m × √escala), velocidade horizontal máxima contra a corrida sem pular;
//  · deitar: linha do tempo (etapa, quanto deitado, clipe, altura da cápsula), apertar de novo no meio, tiro/mira/andar durante as
//    transições, cura deitado (clipe continua deitado, vida sobe, mãos), agachar deitado, rampa (corpo inclinado), parede (corpo afastado);
//  · espectador: 4V4 (sem renascer), Tático (com renascer) e De Rolê (nada muda) — alvo vivo, troca, alvo morto passa ao próximo,
//    câmera fora da parede, fim no renascer/rodada. Linhas "[movlab]" e fotos em autoteste/movlab.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    [DefaultExecutionOrder(5000)]
    public class FBMovLab : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); if (System.Array.IndexOf(a, "-fbmovlab") < 0) return;
            var g = new GameObject("FB_MovLab"); DontDestroyOnLoad(g); g.AddComponent<FBMovLab>();
        }
        static string Arg(string k) { var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, k); return i >= 0 && i + 1 < a.Length && !a[i + 1].StartsWith("-") ? a[i + 1] : null; }
        static void Log(string s) => Debug.Log("[FreeBala][movlab] " + s);
        IEnumerator Wait(float s) { for (float w = 0; w < s; w += Time.deltaTime) yield return null; }
        string dir; Camera cam; int fotos = 1;
        IEnumerator Shot(string n) { if (fotos <= 0) yield break; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png")); yield return null; }
        IEnumerator Side(string n, FBCharacter a, float dist = 3.2f, float side = 1)
        {
            if (fotos <= 0) yield break; var hp = a.Hips ? a.Hips.position : a.Pos + Vector3.up * .5f; var r = a.transform.right * side;
            cam.transform.position = hp + r * dist + Vector3.up * .5f + a.transform.forward * .2f; cam.transform.LookAt(hp); cam.fieldOfView = 45;
            cam.enabled = true; yield return Shot(n); cam.enabled = false;
        }

        // ---- entrada injetada
        Vector2 hold; bool sprint, aimHold; System.Func<int, bool> jumpPat; int fr; FBInputState once; bool onceOn;
        void Press(System.Func<FBInputState, FBInputState> f) { once = f(new FBInputState { slot = -1 }); onceOn = true; }
        FBInputState Inject(FBInputState real)
        {
            var s = new FBInputState { slot = -1, move = hold, sprintHeld = sprint, aim = aimHold };
            if (jumpPat != null && jumpPat(fr)) { s.jump = true; s.jumpHeld = true; }
            fr++;
            if (onceOn) { onceOn = false; var o = once; o.move = hold; o.sprintHeld |= sprint; o.aim |= aimHold; o.jump |= s.jump; return o; }
            return s;
        }

        IEnumerator Start()
        {
            dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste", "movlab"); System.IO.Directory.CreateDirectory(dir);
            foreach (var old in System.IO.Directory.GetFiles(dir, "*.png")) System.IO.File.Delete(old);
            if (int.TryParse(Arg("-fbmov_fotos"), out var fo)) fotos = fo;
            var partes = (Arg("-fbmov_partes") ?? "pulo,deitar,espectador").Split(',');
            yield return Wait(3); GameData.Load(); FBSession.Load(); var keep = (FBSession.map, FBSession.mode, FBSession.view);
            cam = new GameObject("MovLabCam").AddComponent<Camera>(); DontDestroyOnLoad(cam.gameObject); cam.depth = 100; cam.enabled = false; cam.nearClipPlane = .05f;
            FBPlayer.TestInput = Inject;
            if (partes.Contains("pulo") || partes.Contains("deitar"))
            {
                yield return Load("tatico"); var me = FBMatch.Player;
                if (me)
                {
                    FBMatch.God = true; foreach (var b in FBMatch.Bots.Where(b => b).ToList()) b.gameObject.SetActive(false);
                    cam.cullingMask = Camera.main ? Camera.main.cullingMask : ~0;
                    var P = BuildTrack(me.Pos + Vector3.up * 150);
                    if (partes.Contains("pulo")) yield return Jumps(me, P);
                    if (partes.Contains("deitar")) yield return Prone(me, P);
                }
                else Log("sem jogador");
            }
            if (partes.Contains("espectador")) yield return Spectator();
            FBPlayer.TestInput = null; FBSession.map = keep.map; FBSession.mode = keep.mode; FBSession.view = keep.view; FBSession.Save();
            Log("FIM"); yield return Wait(1); Application.Quit();
        }
        IEnumerator Load(string mode)
        {
            FBSession.map = "vila_jardim"; FBSession.mode = mode; FBSession.online = false; FBSession.view = "tps"; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_vila_jardim"); while (op != null && !op.isDone) yield return null; yield return Wait(7);
            if (FBGarage.Open_) { FBGarage.AutoPick(); yield return Wait(1.5f); }
            for (float w = 0; w < 20 && FBMatch.I && FBMatch.I.state != "play"; w += Time.deltaTime) { if (FBHUD.CardOpen) FBHUD.I?.CloseCards(); yield return null; }   // compra/contagem do começo da rodada
        }

        // ---- pista
        class Track { public Vector3 o; public float floor; public Dictionary<string, (Vector3 p, float yaw)> start = new(); public float rampDeg = 22; }
        static GameObject Box(string n, Vector3 c, Vector3 size, Quaternion? rot = null, Color? col = null)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = "movlab_" + n; g.layer = FBWorld.LMap; g.transform.SetPositionAndRotation(c, rot ?? Quaternion.identity); g.transform.localScale = size;
            var r = g.GetComponent<Renderer>(); if (r) { var m = new Material(FBShaders.Lit); m.SetColor("_BaseColor", col ?? new Color(.55f, .55f, .58f)); r.sharedMaterial = m; } return g;   // r49f: material URP (o do primitivo não vai no build)
        }
        Track BuildTrack(Vector3 o)
        {
            var T = new Track { o = o, floor = o.y }; var grey = new Color(.55f, .55f, .58f); var dark = new Color(.3f, .32f, .35f);
            Box("chao", o + new Vector3(25, -.5f, 0), new Vector3(150, 1, 60), null, grey);   // x −50…100 (a parede de deitar fica em x 56)
            T.start["plano"] = (o + new Vector3(0, 0, -20), 0);
            Box("parede", o + new Vector3(8, 2, 2), new Vector3(4, 4, .3f), null, dark); T.start["parede"] = (o + new Vector3(8, 0, 1.2f), 0);
            Box("parede_peitoris", o + new Vector3(16, 2.5f, 2.2f), new Vector3(4, 5, .3f), null, dark);
            foreach (var h in new[] { .9f, 1.6f, 2.3f, 3.0f, 3.7f }) Box("peitoril", o + new Vector3(16, h, 2.2f - .15f - .12f), new Vector3(2.4f, .12f, .25f), null, grey);
            T.start["peitoris"] = (o + new Vector3(16, 0, 1.0f), 0);
            Box("mureta", o + new Vector3(24, .5f, 3), new Vector3(3, 1, .25f), null, dark); T.start["mureta"] = (o + new Vector3(24, 0, 0), 0);
            for (int i = 0; i < 10; i++) { float h = .18f * (i + 1); Box("degrau" + i, o + new Vector3(32, h / 2, 2 + i * .3f + .15f), new Vector3(2.4f, h, .3f), null, i % 2 == 0 ? grey : dark); }
            Box("patamar", o + new Vector3(32, 1.8f / 2, 2 + 10 * .3f + 1.5f), new Vector3(2.4f, 1.8f, 3), null, grey); T.start["escada"] = (o + new Vector3(32, 0, 0), 0);
            float rl = 8, ra = T.rampDeg * Mathf.Deg2Rad; Box("rampa", o + new Vector3(40, Mathf.Sin(ra) * rl / 2 - .15f, 2 + Mathf.Cos(ra) * rl / 2), new Vector3(3, .3f, rl), Quaternion.Euler(-T.rampDeg, 0, 0), grey);
            T.start["rampa"] = (o + new Vector3(40, 0, 0.5f), 0);
            Box("plataforma", o + new Vector3(48, 2, 5), new Vector3(3, 4, 10), null, dark); T.start["queda"] = (o + new Vector3(48, 4, 6), 0);
            Box("parede_deitar", o + new Vector3(56, 1, 1.2f), new Vector3(3, 2, .3f), null, dark); T.start["deitar_parede"] = (o + new Vector3(56, 0, 0), 0);
            Physics.SyncTransforms(); return T;
        }
        IEnumerator Place(FBCharacter me, Track T, string k, float? yaw = null)
        {
            var (p, y) = T.start[k]; float yy = yaw ?? y;
            hold = Vector2.zero; sprint = false; aimHold = false; jumpPat = null;
            FBMatch.Teleport(me, p + Vector3.up * .05f, yy); FBPlayer.I.OnRespawn(yy); FBPlayer.I.camYaw = yy; yield return Wait(.6f);
        }

        // ---- pulo
        IEnumerator Jumps(FBCharacter me, Track T)
        {
            float k = me.ScaleK, apex = 1.5f * Mathf.Sqrt(k);
            Log($"--- PULO · escala {k:0.00} · pulo sozinho sobe {apex:0.00} m · corrida {Json.F(GameData.Section("movement"), "speedRunning", 7.4f):0.0} m/s");
            var scen = new (string nome, string lane, bool run, bool fwd, System.Func<int, bool> pat, float dur, float wait)[]
            {
                ("parado, todo quadro", "plano", false, false, f => true, 2.5f, 0f),
                ("parado, quadro sim quadro não", "plano", false, false, f => f % 2 == 0, 2.5f, 0f),
                ("parado, a cada 80 ms", "plano", false, false, f => (f % 5) == 0, 2.5f, 0f),
                ("correndo sem pular", "plano", true, true, null, 2.5f, 0f),
                ("correndo, todo quadro", "plano", true, true, f => true, 3f, 0f),
                ("correndo, quadro sim quadro não", "plano", true, true, f => f % 2 == 0, 3f, 0f),
                ("contra a parede", "parede", false, true, f => true, 3f, 0f),
                ("parede com peitoris", "peitoris", false, true, f => true, 4f, 0f),
                ("mureta (pulo de janela)", "mureta", false, true, f => true, 3f, 0f),
                ("mureta correndo", "mureta", true, true, f => f % 2 == 0, 3f, 0f),
                ("escada", "escada", false, true, f => true, 3.5f, 0f),
                ("rampa 22°", "rampa", true, true, f => true, 3f, 0f),
                ("caindo de 4 m", "queda", false, true, f => true, 3f, 0f),
                // o caso do pedido: chega na mureta/janela e aperta sem parar (pulo de janela seguido de pulo)
                ("mureta: chega andando e aperta sem parar", "mureta", false, true, f => true, 3f, .9f),   // r49f: .42 s apertava a 1,5 m — o pulo comum (1,5 m) passava a mureta antes do alcance do pulo de janela (1,2 m)
                ("mureta: chega correndo e aperta sem parar", "mureta", true, true, f => f % 2 == 0, 3f, .8f),
                ("peitoril baixo: chega e aperta sem parar", "peitoris", false, true, f => true, 3f, .05f),
            };
            foreach (var fix in new[] { false, true })
            {
                FBPlayer.JumpFix = fix; Log($"== JumpFix {(fix ? "LIGADO (r49)" : "DESLIGADO (antes)")}");
                foreach (var sc in scen)
                {
                    yield return Place(me, T, sc.lane); float floor = T.start[sc.lane].p.y;
                    int j0 = FBPlayer.Jumps, v0 = FBPlayer.Vaults, d0 = FBPlayer.JumpsDenied; float maxY = me.Pos.y, maxHs = 0, airT = 0, maxVy = 0; var last = me.Pos; int frames = 0, air = 0;
                    hold = sc.fwd ? new Vector2(0, 1) : Vector2.zero; sprint = sc.run; fr = 0; jumpPat = sc.wait > 0 ? null : sc.pat;
                    for (float t = 0; t < sc.dur; t += Time.deltaTime)
                    {
                        if (jumpPat == null && sc.pat != null && t >= sc.wait) { fr = 0; jumpPat = sc.pat; }
                        yield return null; var p = me.Pos; float dt = Mathf.Max(1e-4f, Time.deltaTime); float hs = (p - last).Flat().magnitude / dt; last = p;
                        if (hs < 30) maxHs = Mathf.Max(maxHs, hs); maxY = Mathf.Max(maxY, p.y); maxVy = Mathf.Max(maxVy, FBPlayer.I.VY); frames++;
                        if (!FBPlayer.I.OnGroundNow) { air++; airT += Time.deltaTime; }
                        if (fotos > 0 && sc.lane == "peitoris" && Mathf.Abs(t - 2f) < .02f) { cam.transform.position = p + new Vector3(3.5f, 1.5f, -2.5f); cam.transform.LookAt(p + Vector3.up); cam.enabled = true; yield return Shot($"pulo_peitoris_{(fix ? "depois" : "antes")}"); cam.enabled = false; }
                    }
                    jumpPat = null; hold = Vector2.zero; sprint = false; yield return Wait(.8f);
                    float rise = maxY - floor; string flag = rise > apex * 1.12f + (sc.lane == "mureta" ? 1.1f : sc.lane == "escada" ? 1.9f : sc.lane == "rampa" ? 3.2f : sc.lane == "queda" ? 0.05f : 0) ? " · SUBIU DEMAIS" : "";
                    Log($"{(fix ? "agora" : "antes")} · {sc.nome}: pulos {FBPlayer.Jumps - j0} · pulos de janela {FBPlayer.Vaults - v0} · recusados {FBPlayer.JumpsDenied - d0} · altura máx {rise:0.00} m · vy máx {maxVy:0.0} · vel. horizontal máx {maxHs:0.00} m/s · no ar {airT:0.00} s ({air}/{frames} quadros){flag}");
                }
            }
            FBPlayer.JumpFix = true;
        }

        // ---- deitar
        IEnumerator Prone(FBCharacter me, Track T)
        {
            var pl = FBPlayer.I; Log($"--- DEITAR · clipe de deitar {(FBRegistry.I && FBRegistry.I.Clip("FF_SYS_Prone_Down") ? FBRegistry.I.Clip("FF_SYS_Prone_Down").length.ToString("0.00") + " s" : "AUSENTE")} · levantar {(FBRegistry.I && FBRegistry.I.Clip("FF_SYS_KO_GetUp") ? FBRegistry.I.Clip("FF_SYS_KO_GetUp").length.ToString("0.00") + " s" : "AUSENTE")}");
            string Row(float t0) => $"{Time.time - t0:0.00}s:{pl.ProneState}/{me.ProneK:0.00}/{(me.sys ?? "-").Replace("FF_SYS_", "")}/{me.DanW:0.0}";
            // 1) deitar (apertar de novo no meio, andar e mirar durante a transição) e levantar pelo pulo (atirar/mirar no meio)
            yield return Place(me, T, "plano"); var p0 = me.Pos; float t0 = Time.time; string row = ""; float tDown = -1, tUp = -1; bool aimedInTr = false, firedInTr = false;
            Press(s => { s.prone = true; return s; }); yield return null;
            for (float t = 0; t < 1.8f; t += Time.deltaTime)
            {
                if (Mathf.Abs(t - .3f) < .01f) Press(s => { s.prone = true; return s; });                   // apertou de novo deitando: tem de ser ignorado
                hold = t > .1f && t < .8f ? new Vector2(0, 1) : Vector2.zero; aimHold = t > .2f && t < .7f;
                if (pl.ProneState == 1 && (me.aiming)) aimedInTr = true;
                if (pl.ProneState == 2 && tDown < 0) tDown = Time.time - t0;
                if (((int)(t * 10)) != ((int)((t - Time.deltaTime) * 10))) row += Row(t0) + " ";
                if (fotos > 0 && Mathf.Abs(t - .55f) < .01f) yield return Side("deitar_meio", me);
                yield return null;
            }
            hold = Vector2.zero; aimHold = false; float moved = (me.Pos - p0).Flat().magnitude;
            Log($"deitar: {row}· chegou deitado em {tDown:0.00} s · andou {moved:0.00} m durante (esperado ~0) · mirou na transição {aimedInTr} · etapa {pl.ProneState} · cápsula {me.GetComponent<CharacterController>().height * me.transform.lossyScale.y:0.00} m");
            yield return Side("deitado", me);
            // atirar deitado (pode) e levantar com o pulo; durante o levantar: atirar e mirar seguram
            t0 = Time.time; row = ""; Press(s => { s.jump = true; return s; }); yield return null;
            for (float t = 0; t < 2.2f; t += Time.deltaTime)
            {
                bool tr = pl.ProneState == 3; aimHold = t > .1f && t < 1.0f;
                if (tr && t > .15f && t < .9f) Press(s => { s.fire = s.firePressed = true; return s; });
                if (tr && me.aiming) aimedInTr = true; if (tr && me.lastShot < .05f) firedInTr = true;
                if (pl.ProneState == 0 && tUp < 0) tUp = Time.time - t0;
                if (((int)(t * 10)) != ((int)((t - Time.deltaTime) * 10))) row += Row(t0) + " ";
                if (fotos > 0 && Mathf.Abs(t - .6f) < .01f) yield return Side("levantar_meio", me);
                yield return null;
            }
            aimHold = false;
            Log($"levantar (pulo): {row}· em pé em {tUp:0.00} s · mirou durante {aimedInTr} · atirou durante {firedInTr} · etapa {pl.ProneState}");
            // 2) cura deitado
            yield return Place(me, T, "plano"); Press(s => { s.prone = true; return s; }); yield return Wait(1.6f);
            FBMed.ResetPlayer(4, false); me.hp = 35; yield return Wait(.3f); Press(s => { s.heal = true; return s; }); yield return null;
            row = ""; t0 = Time.time; string clips = ""; float hb = 0; bool stoodUp = false;
            for (float t = 0; t < 4f; t += Time.deltaTime)
            {
                if (me.sys != null && !clips.Contains(me.sys)) clips += me.sys + " "; hb = Mathf.Max(hb, me.ProneHealBlend); if (pl.ProneState != 2) stoodUp = true;
                if (((int)(t * 4)) != ((int)((t - Time.deltaTime) * 4))) row += $"{Time.time - t0:0.00}s:{me.hp:0}/{pl.ProneState} ";
                if (fotos > 0 && Mathf.Abs(t - 1.2f) < .01f) yield return Side("cura_deitado", me, 2.6f);
                yield return null;
            }
            Log($"cura deitado: {row}· clipes {clips}· mãos (peso máx) {hb:0.00} · levantou sozinho {stoodUp} · kits {FBMed.kits} (antes 4) · Busy {FBMed.Busy}");
            // 3) agachar deitado → levanta até agachado
            t0 = Time.time; Press(s => { s.crouchToggle = true; return s; }); yield return Wait(1.6f);
            Log($"agachar deitado: etapa {pl.ProneState} · agachado {me.crouch} · {Time.time - t0:0.00} s");
            Press(s => { s.crouchToggle = true; return s; }); yield return Wait(.6f);
            // 4) rampa (de frente e de lado) e parede
            foreach (var (nome, yaw) in new[] { ("rampa de frente", 0f), ("rampa de lado", 90f) })
            {
                var (rp, _) = T.start["rampa"]; float ra = T.rampDeg * Mathf.Deg2Rad; var on = rp + new Vector3(0, Mathf.Sin(ra) * 3.5f + .1f, Mathf.Cos(ra) * 3.5f + 1.5f);
                T.start["_r"] = (on, yaw); yield return Place(me, T, "_r"); Press(s => { s.prone = true; return s; }); yield return Wait(1.8f);
                Log($"deitado na {nome} ({T.rampDeg:0}°): corpo inclinado {me.PronePitch:0.0}° frente / {me.ProneRoll:0.0}° lado · etapa {pl.ProneState}");
                yield return Side("deitado_" + nome.Replace(" ", "_"), me); Press(s => { s.jump = true; return s; }); yield return Wait(1.6f);
            }
            yield return Place(me, T, "deitar_parede"); Press(s => { s.prone = true; return s; }); yield return Wait(1.8f);
            Log($"deitado de frente para a parede (1,05 m): corpo deslocado {me.ProneShift:0.00} m (negativo = para trás) · etapa {pl.ProneState}");
            yield return Side("deitado_parede", me); hold = new Vector2(0, 1); var pw = me.Pos; yield return Wait(1f); hold = Vector2.zero;
            Log($"rastejar contra a parede: andou {(me.Pos - pw).Flat().magnitude:0.00} m · deslocado {me.ProneShift:0.00} m");
            Press(s => { s.jump = true; return s; }); yield return Wait(1.6f);
        }

        // ---- espectador
        IEnumerator Spectator()
        {
            foreach (var mode in new[] { "squad", "tatico", "role" })
            {
                yield return Load(mode); var me = FBMatch.Player; if (!me) { Log($"{mode}: sem jogador"); continue; }
                FBMatch.God = false; yield return Wait(1.5f);
                for (float w = 0; w < 30 && FBMatch.I.freeze; w += Time.deltaTime) yield return null;   // r49g: 4V4 — morrer na fase de compra (protegida no jogo) deixava a LOJA por cima do espectador
                yield return Wait(.5f); me.protUntil = 0;                                     // 4V4: proteção de spawn da rodada
                var foe = FBMatch.All.FirstOrDefault(a => a && a != me && a.alive && !a.civ && FBMatch.Hostile(a, me));
                Log($"--- ESPECTADOR · {mode} · estado {FBMatch.I.state} · vivos {FBMatch.All.Count(a => a && a.alive && !a.civ)}");
                FBCombat.DamageActor(me, 9999, true, foe, "lab"); yield return null;
                if (me.knocked) { FBCombat.DamageActor(me, 9999, true, foe, "bleed"); yield return null; }     // 4V4: caído primeiro (r49f: "bleed" — o 2º tiro no mesmo quadro caía na invulnerabilidade do caído e os aliados reanimavam)
                yield return Wait(2.8f);
                var tg = FBSpectator.Target; var camT = Camera.main ? Camera.main.transform : null;
                float dCam = tg && camT ? Vector3.Distance(camT.position, tg.Pos + Vector3.up) : -1; bool inWall = camT && Physics.CheckSphere(camT.position, .08f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore);
                Log($"{mode}: morto · espectador {FBSpectator.On} · alvo {FBSpectator.Diag} vivo {(tg ? tg.alive : false)} aliado {(tg ? tg.team == me.team : false)} · câmera a {dCam:0.0} m do alvo · dentro de parede {inWall} · estado {FBMatch.I.state} · tela de abate aberta {FBHUD.CardOpen}");
                yield return Shot($"espectador_{mode}_1");
                if (mode == "role") { yield return Wait(4); Log($"role: espectador {FBSpectator.On} (esperado False) · vivo {me.alive}"); continue; }
                int sw0 = FBSpectator.Switches; var t1 = FBSpectator.Target; Press(s => { s.fire = s.firePressed = true; return s; }); yield return Wait(.8f);
                Log($"{mode}: trocar (botão esquerdo): {(t1 ? t1.Name : "-")} → {FBSpectator.Diag} · trocas {FBSpectator.Switches - sw0} · candidatos {FBSpectator.Candidates().Count}");
                yield return Shot($"espectador_{mode}_2");
                var t2 = FBSpectator.Target; int au0 = FBSpectator.AutoSwitches;
                if (t2) { FBMatch.God = false; t2.knocked = false; t2.protUntil = 0; FBCombat.DamageActor(t2, 9999, true, foe, "lab"); if (t2.knocked) FBCombat.DamageActor(t2, 9999, true, foe, "bleed"); }
                yield return Wait(2.2f);
                Log($"{mode}: tela de abate aberta {FBHUD.CardOpen} (modos com renascer: recolhe 3 s depois de começar a espectar) · estado {FBMatch.I.state}");
                Log($"{mode}: alvo {(t2 ? t2.Name : "-")} morreu (vivo {(t2 ? t2.alive : false)}) → agora {FBSpectator.Diag} · trocas automáticas {FBSpectator.AutoSwitches - au0} · espectador {FBSpectator.On}");
                for (float w = 0; w < 9 && !me.alive && FBMatch.I.state != "end"; w += Time.deltaTime) yield return null;
                Log($"{mode}: depois: vivo {me.alive} · espectador {FBSpectator.On} · estado {FBMatch.I.state} · câmera segue {(FBSpectator.CamTarget(me) == me ? "o jogador" : "outro")}");
            }
        }
    }
}
