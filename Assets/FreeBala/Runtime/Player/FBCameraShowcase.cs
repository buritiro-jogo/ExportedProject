// Vitrine da câmera em 3ª pessoa (-fbcam), para validar câmera × retículo × cano × impacto sem jogar:
//   FreeBala.exe -fbauto praca_aguas:treino 150 -fbcam        (os segundos do -fbauto precisam cobrir a vitrine, ~90 s)
// Numa partida carregada pelo FBAutoTest, põe o jogador em 3ª pessoa e, para cada classe de arma (pistola, submetralhadora, fuzil, sniper,
// escopeta), grava a visão do próprio jogador (HUD junto, para ver o retículo) em <build>/autoteste/cam_<classe>_<estado>.png:
// parado, quadril (tiro sem mirar), mira (ADS), tiro mirando (ponto de impacto marcado: esfera vermelha = onde a bala parou), correndo,
// agachado, inclinando Q/E, encostado na parede e encostado num carro. Cada tiro registra no log a distância entre o ponto do retículo e o impacto.
// FBCamLab: controle sintético (FBPlayer.TestInput), arena flutuante e marcador — usados também por Tests/PlayMode/FBCameraTests.cs.
using System.Collections;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public static class FBCamLab
    {
        // ---- controle sintético: o que o "jogador" está apertando neste quadro
        public static bool aim, fire, leanL, leanR; public static Vector2 move; static bool firePulse, runPulse, crouchPulse;
        public static void Install()
        {
            aim = fire = leanL = leanR = firePulse = runPulse = crouchPulse = false; move = Vector2.zero;
            FBPlayer.TestInput = _ =>
            {
                var s = new FBInputState { slot = -1, aim = aim, aimPressed = false, fire = fire || firePulse, firePressed = firePulse, leanL = leanL, leanR = leanR, move = move,
                    sprintToggle = runPulse, crouchToggle = crouchPulse };
                firePulse = runPulse = crouchPulse = false; return s;
            };
        }
        public static void Remove() { FBPlayer.TestInput = null; FBRecoil.TestNoSpread = false; }
        public static void Shoot() => firePulse = true;
        public static void ToggleRun() => runPulse = true;
        public static void ToggleCrouch() => crouchPulse = true;

        // uma arma de cada classe (preferência pelas do jogo base; senão a primeira com modelo)
        public static readonly (string cls, string[] pref)[] CLASSES = {
            ("pistola", new[] { "g18", "38" }), ("submetralhadora", new[] { "mp5", "p90" }), ("fuzil", new[] { "ak", "ar15_new", "rpk" }),
            ("sniper", new[] { "barret_50", "sniper_hunting" }), ("escopeta", new[] { "remington_870", "m31" }) };
        public static WeaponDef Pick(string cls, string[] pref)
        {
            bool Ok(WeaponDef d) => d != null && d.cls == cls && FBRegistry.I && FBRegistry.I.Weapon(d.id);
            foreach (var id in pref) if (GameData.Weapons.TryGetValue(id, out var d) && Ok(d)) return d;
            return GameData.Weapons.Values.Where(Ok).OrderBy(d => d.id).FirstOrDefault();
        }

        public static void Teleport(FBCharacter ch, Vector3 p, float yaw)
        {
            var cc = ch.GetComponent<CharacterController>(); if (cc) cc.enabled = false; ch.transform.position = p; ch.Yaw = yaw; if (cc) cc.enabled = true;
            if (FBPlayer.I) FBPlayer.I.OnRespawn(yaw);
        }

        // ---- arena flutuante (chão + parede móvel), longe do mapa: medição sem nada no caminho
        static Material matA, matB;
        public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, bool alt = false, int layer = FBWorld.LMap)
        {
            matA ??= MakeMat(new Color(.55f, .56f, .58f)); matB ??= MakeMat(new Color(.78f, .62f, .34f));
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name; g.layer = layer; if (parent) g.transform.SetParent(parent, true);
            g.transform.position = center; g.transform.localScale = size; g.GetComponent<Renderer>().sharedMaterial = alt ? matB : matA; return g;
        }
        static Material MakeMat(Color c) { var m = new Material(FBShaders.Lit); m.SetColor("_BaseColor", c); m.color = c; return m; }

        // marcador de impacto (sem colisor; não atrapalha raios)
        static Material mark;
        public static GameObject Mark(Vector3 p, float distToCam, Color? c = null)
        {
            mark ??= new Material(FBShaders.Unlit); var g = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.Destroy(g.GetComponent<Collider>()); g.name = "MarcaImpacto";
            var m = new Material(mark); m.SetColor("_BaseColor", c ?? Color.red); m.color = c ?? Color.red; g.GetComponent<Renderer>().sharedMaterial = m;
            g.transform.position = p; g.transform.localScale = Vector3.one * (.05f + distToCam * .004f); return g;
        }

        // deriva do corpo: quanto o quadril (o que se vê) se afasta, no plano, do objeto do personagem (onde ficam a física e o pivô da câmera).
        // Loop de locomoção com a translação da raiz embutida faz o corpo "fugir" da câmera até o tamanho do passo do ciclo (até 2,6 m) e voltar.
        public static float HipsDrift(FBCharacter ch)
        {
            var h = ch && ch.an ? ch.an.GetBoneTransform(HumanBodyBones.Hips) : null; return h ? Vector3.ProjectOnPlane(h.position - ch.Pos, Vector3.up).magnitude : 0;
        }
        // movimento sintético medido: deriva máxima do quadril e variação da distância câmera→quadril (câmera para trás/corpo fugindo)
        public static IEnumerator Move(FBCharacter ch, Camera cam, Vector2 mv, bool run, bool aimOn, bool fireOn, float secs, System.Action<float, float> done, System.Func<float, IEnumerator> mid = null, float midAt = -1)
        {
            move = mv; if (run) ToggleRun(); float worst = 0, dmin = 1e9f, dmax = 0; bool midDone = false;
            for (float t = 0; t < secs; t += Time.deltaTime)
            {
                if (t > .35f) { aim = aimOn; fire = fireOn; }                     // corre primeiro, depois mira/atira (como o jogador faz)
                yield return null;
                if (t > .6f)
                {
                    worst = Mathf.Max(worst, HipsDrift(ch)); var h = ch.an ? ch.an.GetBoneTransform(HumanBodyBones.Hips) : ch.transform;
                    float d = Vector3.Distance(cam.transform.position, h.position); dmin = Mathf.Min(dmin, d); dmax = Mathf.Max(dmax, d);
                }
                if (mid != null && !midDone && midAt >= 0 && t >= midAt) { midDone = true; yield return mid(t); }
            }
            aim = fire = false; move = Vector2.zero; if (run && FBPlayer.I && ch.sprint) ToggleRun();
            done?.Invoke(worst, dmax - dmin);
        }

        // ponto sob o centro da tela (o retículo) agora
        public static bool CrosshairHit(Camera cam, out RaycastHit h) => Physics.Raycast(cam.transform.position, cam.transform.forward, out h, 400, FBWorld.ShotMask, QueryTriggerInteraction.Ignore);
    }

    public class FBCameraShowcase : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbcam") < 0) return;
            var g = new GameObject("FB_CamVitrine"); DontDestroyOnLoad(g); g.AddComponent<FBCameraShowcase>();
        }

        string dir; int shots;
        IEnumerator Start()
        {
            dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste"); System.IO.Directory.CreateDirectory(dir);
            while (!(FBMatch.I && FBMatch.Player && FBMatch.Player.alive && FBPlayer.I && FBMatch.I.state == "play")) yield return null;
            yield return Wait(6);                                                    // aquecimento (o FBAutoTest espera 4 s)
            var me = FBMatch.Player; var pl = FBPlayer.I; FBMatch.God = true;
            if (pl.FirstPerson) pl.ForceThirdPerson();
            FBCamLab.Install();
            Debug.Log("[FreeBala][cam] vitrine 3ª pessoa começando");
            // lugar: direção com parede/prédio a ~20 m (o alvo dos tiros é o que estiver no centro da tela)
            var p0 = me.Pos; float yaw = TargetYaw(p0);
            foreach (var (cls, pref) in FBCamLab.CLASSES)
            {
                var w = FBCamLab.Pick(cls, pref); if (w == null) { Debug.Log($"[FreeBala][cam] sem arma da classe {cls}"); continue; }
                me.GetComponent<FBWeaponUser>().Give(w.id); yield return Wait(1.2f);
                yield return Pose(me, p0, yaw);
                yield return Snap(cls, "parado", 1.0f);
                // quadril: tiro sem mirar
                FBRecoil.TestNoSpread = true; yield return ShotAndMark(pl, cls, "quadril"); FBRecoil.TestNoSpread = false;
                FBCamLab.aim = true; yield return Snap(cls, "mira", 1.0f);
                FBRecoil.TestNoSpread = true; yield return ShotAndMark(pl, cls, "mira_tiro"); FBRecoil.TestNoSpread = false;
                // rajada com recuo de verdade (coice da câmera aparece no print do meio)
                if (w.auto) { FBCamLab.fire = true; yield return Wait(.35f); yield return Snap(cls, "rajada", 0); FBCamLab.fire = false; }
                FBCamLab.aim = false; yield return Wait(.8f);
                // correndo (corrida automática, como o Shift do web)
                yield return Pose(me, p0, yaw); FBCamLab.move = new Vector2(0, 1); FBCamLab.ToggleRun(); yield return Snap(cls, "correndo", .9f); FBCamLab.move = Vector2.zero; FBCamLab.ToggleRun(); yield return Wait(.6f);
                // correr + mirar / correr + atirar / mirando em diagonal / mirando de costas: o corpo não pode fugir da câmera
                foreach (var (nm, mv, run, am, fr) in new[] { ("corre_mira", new Vector2(0, 1), true, true, false), ("corre_tiro", new Vector2(0, 1), true, false, true),
                    ("mira_diagonal", new Vector2(.71f, .71f), false, true, false), ("mira_costas", new Vector2(0, -1), false, true, false), ("andando", new Vector2(0, 1), false, false, false) })
                {
                    yield return Pose(me, p0, yaw); string tag = nm;
                    yield return FBCamLab.Move(me, pl.cam, mv, run, am, fr, 1.6f, (dr, dd) => Debug.Log($"[FreeBala][cam] {cls} {tag}: quadril até {dr * 100:0} cm do personagem · distância câmera→quadril variou {dd * 100:0} cm · sprint {me.sprint}"),
                        _ => Snap(cls, tag, 0), 1.1f);
                    yield return Wait(.5f);
                }
                yield return Pose(me, p0, yaw); FBCamLab.ToggleCrouch(); yield return Snap(cls, "agachado", 1.0f); FBCamLab.aim = true; yield return Snap(cls, "agachado_mira", .8f); FBCamLab.aim = false; FBCamLab.ToggleCrouch(); yield return Wait(.6f);
                FBCamLab.leanL = true; yield return Snap(cls, "inclina_q", 1.0f); FBCamLab.aim = true; yield return Snap(cls, "inclina_q_mira", .8f); FBCamLab.aim = FBCamLab.leanL = false;
                FBCamLab.leanR = true; yield return Snap(cls, "inclina_e", 1.0f); FBCamLab.aim = true; yield return Snap(cls, "inclina_e_mira", .8f); FBCamLab.aim = FBCamLab.leanR = false; yield return Wait(.5f);
                // encostado na parede: a parede fica às costas (câmera não pode atravessar)
                var arena = new GameObject("ArenaCamera").transform;
                var fw = Quaternion.Euler(0, yaw, 0) * Vector3.forward; var rt = Quaternion.Euler(0, yaw, 0) * Vector3.right;
                var wall = FBCamLab.Box(arena, "ParedeCostas", me.Pos - fw * (.55f + .2f) + Vector3.up * 2f, new Vector3(6, 4, .4f), true); wall.transform.rotation = Quaternion.Euler(0, yaw, 0);
                yield return Pose(me, p0, yaw); yield return Snap(cls, "parede", 1.0f); FBCamLab.aim = true; yield return Snap(cls, "parede_mira", .8f); FBCamLab.aim = false;
                Debug.Log($"[FreeBala][cam] {cls} parede: câmera {Vector3.Dot(pl.cam.transform.position - wall.transform.position, fw) - .2f:0.00} m à frente da parede (≥ 0 = não atravessou)");
                Destroy(wall); yield return null;
                // encostado num carro (S10 atrás e ao lado)
                var car = FBVehicles.Spawn("S10_2017", me.Pos - fw * 2.6f + rt * .8f, yaw + 90);
                if (car)
                {
                    yield return Wait(.3f); yield return Pose(me, p0, yaw); yield return Snap(cls, "carro", 1.0f); FBCamLab.aim = true; yield return Snap(cls, "carro_mira", .8f); FBCamLab.aim = false;
                    bool inside = Physics.CheckSphere(pl.cam.transform.position, .05f, 1 << FBWorld.LVehicle, QueryTriggerInteraction.Ignore);
                    Debug.Log($"[FreeBala][cam] {cls} carro: câmera dentro do veículo {inside}");
                    FBVehicles.list.Remove(car); Destroy(car.gameObject); yield return null;
                }
                Destroy(arena.gameObject);
            }
            FBCamLab.Remove();
            Debug.Log($"[FreeBala][cam] vitrine fim: {shots} prints");
        }

        IEnumerator Pose(FBCharacter me, Vector3 p, float yaw) { FBCamLab.Teleport(me, p, yaw); FBPlayer.I.camPitch = 2; yield return Wait(.5f); }

        IEnumerator ShotAndMark(FBPlayer pl, string cls, string name)
        {
            yield return Wait(.6f);
            FBCamLab.CrosshairHit(pl.cam, out var ch0); int n = pl.ShotCount; FBCamLab.Shoot();
            for (int k = 0; k < 4 && pl.ShotCount == n; k++) yield return null;
            if (pl.ShotCount == n) { Debug.Log($"[FreeBala][cam] {cls} {name}: o tiro não saiu"); yield break; }
            float dCam = Vector3.Distance(pl.cam.transform.position, pl.LastShotHit);
            var m1 = FBCamLab.Mark(pl.LastShotHit, dCam); var m2 = FBCamLab.Mark(pl.LastShotAim, dCam * .6f, Color.green);
            Debug.Log($"[FreeBala][cam] {cls} {name}: distância {dCam:0.0} m · impacto × ponto mirado {Vector3.Distance(pl.LastShotHit, pl.LastShotAim) * 100:0.0} cm · impacto × retículo (antes do tiro) {(ch0.collider ? Vector3.Distance(pl.LastShotHit, ch0.point) * 100 : -1):0.0} cm · mira bloqueada {pl.AimBlocked}");
            yield return Wait(.25f); yield return Snap(cls, name, 0);
            Destroy(m1); Destroy(m2);
        }

        IEnumerator Snap(string cls, string name, float wait)
        {
            if (wait > 0) yield return Wait(wait);
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"cam_{cls}_{name}.png")); shots++; yield return null; yield return null;
        }
        static IEnumerator Wait(float s) { for (float t = 0; t < s; t += Time.deltaTime) yield return null; }
        static float TargetYaw(Vector3 p)
        {
            float best = 0, bs = -1e9f; var o = p + Vector3.up * 1.5f;
            for (int i = 0; i < 36; i++) { float a = i * 10; float sc = FBWorld.RayHit(o, Quaternion.Euler(0, a, 0) * Vector3.forward, 80, out var h) ? -Mathf.Abs(h.distance - 20) : -100; if (sc > bs) { bs = sc; best = a; } }
            return best;
        }
    }
}
