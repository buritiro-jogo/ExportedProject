// DIRIGIR (porte do drive.js) — entrar (F) em qualquer veículo estacionado (carros do REB, Caveirão do IE, motos do Grau) e pilotar.
// Física arcade do original: aceleração/freio/ré (KIND em m/s do mundo, ~1,2× o real), esterço do RCC (40° parado -> 5° a 200 km/h),
// aderência lateral ~1,2 g, freio de mão (Espaço) derrapando, burnout/arrancada, rampa e meio-fio por sondas de chão nas rodas, queda/
// solavanco, batida por raios no para-choque (o próprio veículo é ignorado), atropelamento. Moto: grau (pêndulo no eixo traseiro, moto.js),
// inclinação na curva, motor de 5 marchas com curva de torque, corpo do piloto (MotoProceduralIKHandler). Som: Realistic Engine Sound + câmbio
// do RCC (FBCarSound), vento, freio, ré, troca de marcha, turbo, escapamento, derrapagem, buzina por modelo (H); motos do Grau com os clipes
// originais (parado/acelerando/reduzindo/corte). Pose: quadril no banco, cabeça no teto (raio mede a folga), mãos no aro do volante que gira
// no eixo real da coluna (PCA do aro), cotovelos pelos HandPole e pés nos pedais (rig do REB); moto: mãos no guidão e pés na pedaleira.
// Ângulos guardados na convenção do web (yaw/roll trocam de sinal no Unity: CarRot); vetores já no espaço do Unity.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace FreeBala
{
    public static class FBDrive
    {
        public struct K { public float vmax, acc, brake, rev, steer, steerDeg, grip; public Vector2 cam; }
        public static readonly Dictionary<string, K> KIND = new()
        {
            ["car"] = new K { vmax = 33, acc = 8.5f, brake = 20, rev = 9, steer = .62f, steerDeg = 40, grip = 1, cam = new Vector2(4.6f, 1.95f) },
            ["heavy"] = new K { vmax = 25, acc = 5.2f, brake = 14, rev = 6, steer = .5f, steerDeg = 34, grip = .9f, cam = new Vector2(6.4f, 2.6f) },
            ["moto"] = new K { vmax = 36, acc = 11, brake = 22, rev = 3, steer = .7f, steerDeg = 30, grip = 1.1f, cam = new Vector2(3.4f, 1.65f) },
        };
        // VELOCIDADE MÁXIMA POR VEÍCULO (pedido 26/10: "não quero limitar a 99 … cada veículo pode alcançar uma velocidade maior"): até o
        // joelho (o vmax do tipo, 99 km/h no carro) a aceleração é a de sempre; dali para cima cresce cada vez mais devagar até a máxima do
        // modelo (km/h de jogo, perto da ficha real). Sem ficha: pela família do ronco de fábrica do REB.
        static readonly Dictionary<string, float> TOP = new()
        {
            ["Fusca"] = 125, ["Kombi"] = 110, ["Chevett"] = 150, ["Kadett"] = 170, ["OpalaDiplomata85"] = 180, ["OmegaCD1997"] = 200, ["Santana2002"] = 175, ["ParatiSurf1994"] = 165,
            ["GolQuadrado"] = 160, ["GolG2"] = 170, ["GolG5"] = 175, ["Up"] = 160, ["Celta"] = 155, ["CorsaClassic"] = 160, ["Palio2010"] = 160, ["UnoMille"] = 150, ["UnoWay"] = 160,
            ["Clio2003"] = 165, ["HB20"] = 170, ["Fiorino"] = 140, ["CrossFox"] = 170, ["SaveiroQuadrada"] = 160, ["SaveiroG4"] = 165, ["SaveiroCross"] = 170, ["Montana"] = 165,
            ["Nivus2024"] = 190, ["Jetta2012"] = 210, ["Golf"] = 200, ["GolfGtiMk7"] = 245, ["i30"] = 200, ["Corolla2018"] = 195, ["CivicLXR_2014"] = 195, ["CivicG10"] = 205,
            ["Bmw320i"] = 230, ["AudioS8"] = 250, ["Camaro"] = 270, ["MustangBlackShadow"] = 260, ["Porsche911CarreraS"] = 300, ["PorscheBoxster"] = 280, ["McLarenArtura"] = 320,
            ["LamboHuracanEvo"] = 325, ["MacanGTS_2024"] = 260, ["Evoque2020"] = 215, ["RangerRover"] = 230, ["S10_2017"] = 180, ["Amarok"] = 190, ["Toro"] = 185, ["L200"] = 175,
            ["Hilux2023Conquest"] = 180, ["Sw4"] = 180, ["SW4_Diamond"] = 185, ["RAM3500"] = 170, ["F250"] = 160, ["ViaturaPM"] = 175, ["ViaturaBM"] = 175, ["ViaturaPMAzul"] = 175,
        };
        public static float TopKmh(FBCar c)
        {
            if (!c) return 99; if (c.kind == "moto") return KIND["moto"].vmax * 3; if (c.ie) return c.heavy ? 115 : 150;
            if (!TOP.TryGetValue(c.id ?? "", out var t))
            {
                var pr = c.engineProf ?? FBCarSound.ProfileFor(c.id, c) ?? "";
                t = System.Text.RegularExpressions.Regex.IsMatch(pr, "V12|V10|v10|v12") ? 300 : System.Text.RegularExpressions.Regex.IsMatch(pr, "V8|v8|GTR|Rotary|Flat6") ? 250 : FBCarSound.Sporty(pr) ? 215
                    : pr.Contains("diesel") || c.heavy ? 170 : pr.Contains("boxer") ? 125 : 170;
            }
            return t * (c.turbo ? 1.07f : 1);
        }
        public static float TopV(FBCar c) => TopKmh(c) * 1.2f / 3.6f;                                   // m/s do jogo (km/h = v·3,6/1,2)
        // aceleração do motor a 'v' com acelerador cheio: a curva de sempre até o joelho, depois cai suave até zerar na máxima do carro
        public static float PowerAcc(FBCar car, K Kd, float v, float accK)
        {
            float vT = TopV(car), knee = Mathf.Min(Kd.vmax, vT * .9f), a0 = Kd.acc * accK;
            if (v <= knee) return a0 * (1 - Mathf.Max(0, v) / Kd.vmax * .55f);
            float aK = a0 * (1 - knee / Kd.vmax * .55f), perf = Mathf.Lerp(1, Mathf.Clamp(TopKmh(car) / 190f, .75f, 1.5f), Mathf.Clamp01((v - knee) / 8));
            return aK * perf * Mathf.Pow(Mathf.Clamp01((vT * 1.03f - v) / (vT * 1.03f - knee)), 1.35f);
        }
        public static K KindOf(FBCar c) => KIND[c.kind == "moto" ? "moto" : c.heavy ? "heavy" : "car"];
        public class Rider { public float v, lean, sp, a; }
        public class VolSetup { public Vector3 axis, pivot, baseP; public Quaternion baseQ; }
        const string REB_SIT = "FF_SYS_REB_Dirigir";
        static readonly string[] SG_CAR = { "FF_SYS_SG_Driver_Idle", "FF_SYS_SG_Driver_IdleL", "FF_SYS_SG_Driver_IdleR" }, SG_MOTO = { "FF_SYS_SG_Vehicle_MOTO_Driver_Idle", "FF_SYS_SG_Vehicle_MOTO_Driver_IdleL", "FF_SYS_SG_Vehicle_MOTO_Driver_IdleR" };
        static FBCar cur; static FBCharacter curActor; static bool prevWeaponVisible; static float lastCarYaw, motoYawLag; public static bool hold, rev;
        public static FBCar Cur => cur;
        public static Quaternion CarRot(float pitch, float yaw, float roll) => Quaternion.Euler(pitch * Mathf.Rad2Deg, -yaw * Mathf.Rad2Deg, -roll * Mathf.Rad2Deg);
        public static Vector3 Fwd(float yaw) => new(-Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
        public static Vector3 Left(float yaw) => new(-Mathf.Cos(yaw), 0, -Mathf.Sin(yaw));          // +X local do web (lado do motorista)
        static float shake; static void Shake(float k) => shake = Mathf.Max(shake, k);
        public static void ResetState() { if (cur) StopAudio(cur); cur = null; curActor = null; FBGrauHud.Hide(); }

        // r43 — MOTO: poses do piloto do 171 (Characters/Animations/Vehicle/Motorcycle; usuário: "aproveitar todas as animações completas das
        // motocicletas ... posicionamento das mãos, pernas e corpo"). Parado: pé no chão; andando: pilotando; curva: corpo para dentro (L/R);
        // rápido: abaixado no vento; grau: puxando. Troca com transição (PlayFull 0,3 s) e no mínimo 0,35 s entre trocas; mãos/pés seguem no IK.
        // Sem os clipes (grupo do 171 removido) volta à pose antiga do Sigma.
        public const string MOTO_RIDE = "X171_Idle_on_bike_driving";
        // r43: subir/descer da moto do 171 (pelo lado esquerdo). Subir: começa de frente para a moto e o corpo gira 90° até olhar para a frente;
        // descer: do banco, passa a perna e fica em pé do lado esquerdo olhando para a moto. -fbmotodireto volta ao 'direto no banco'.
        public const string MOTO_CRASH = "X171_colision_on_bike_driving", MOTO_MOUNT = "X171_Go_to_bike_L", MOTO_DISMOUNT = "X171_Bike_Go_to_idle_L__Renato"; public const float MOUNT_SPEED = 1.5f;
        public static bool MotoAnims = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbmotodireto") < 0;
        // r45: entrar/sair do CARRO pelos clipes do 171 (Characters/Animations/Vehicle/Car): no Entrar_Motorista o corpo anda 1,23 m para a
        // frente da raiz e 0,22 m para a direita dela — com a raiz girada +90° (a frente dela = direita do carro, como no montar da moto) sai
        // de fora da porta esquerda e chega no banco; no Sair_Motorista anda 1,22 m para −X com a raiz alinhada ao carro = sai pela esquerda.
        // A porta do motorista (FBDoors portaL) abre no começo e fecha no fim. Porta bloqueada / carro sem os clipes: entra/sai direto.
        public const string CAR_IN = "X171_Entrar_Motorista_SK_Mannequin_Anim1", CAR_OUT = "X171_Sair_Motorista_Anim2";
        public static bool CarAnims = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbcarrodireto") < 0;
        // r45e: o lado da porta ignora as peças do próprio veículo (portas do FBDoors, rodas/corpo da MX) e quem está em pé ali; DoorDiag = motivo
        public static string DoorDiag;
        static bool SideHit(FBCar car, Vector3 o, Vector3 dir, float far)
        {
            foreach (var h in Physics.RaycastAll(o, dir, far, FBWorld.SolidMask, QueryTriggerInteraction.Ignore))
            {
                var c = h.collider; if (!c || c.GetComponentInParent<FBCar>() == car || (car.mx && c.transform.IsChildOf(car.mx.transform)) || c.GetComponentInParent<FBCharacter>()) continue;
                DoorDiag = $"bloqueado por {c.name} a {h.distance:0.00}"; return true;
            }
            return false;
        }
        static bool DoorSideFree(FBCar car, float k)
        {
            var seatW = car.L2W(car.seat); var left = -car.transform.right; var o = seatW + Vector3.up * .55f; DoorDiag = "livre";
            car.SetColliders(false);
            try { if (SideHit(car, o, left, 1.35f * k)) return false; var p = seatW + left * 1.25f * k; if (FBWorld.GroundY(p.x, seatW.y + 1, p.z, .4f, 2.5f) == null) { DoorDiag = "sem chão do lado"; return false; } return true; }
            finally { car.SetColliders(true); }
        }
        // r48: o começo de entrar/sair do CARRO trocava a pose de uma vez (camada cheia no 1º quadro, sem a mistura que a moto já tinha):
        // laboratório r48c — cabeça girando 1.900–3.000°/s em t≈0,05–0,17 s em 3 de 4 personagens; na saída, da pose ao volante (REB, reclinada)
        // para o 1º quadro do clipe do 171. Agora guarda a pose do instante da troca e mistura até o clipe (FBPoseBlend), como no subir da moto.
        const float CarBlend = .3f;
        public static bool StartCarIn(FBCharacter p, FBCar car)
        {
            if (!CarAnims || !car || car.kind == "moto" || car.mx || !FBRegistry.I || !FBRegistry.I.Clip(CAR_IN)) return false; var dp = p.GetComponent<FBDriverPose>(); if (!dp) return false;
            float k = p.ScaleK; if (!DoorSideFree(car, k)) return false;
            var seatW = car.L2W(car.seat); var root = seatW - car.transform.right * 1.235f * k - car.transform.forward * .218f * k;
            car.SetColliders(false); root.y = FBWorld.GroundY(root.x, seatW.y + 1, root.z, .5f, 3) ?? car.transform.position.y; car.SetColliders(true);
            var clip = FBRegistry.I.Clip(CAR_IN); FBPoseBlend.Capture(p, CarBlend); dp.trans = "mount"; dp.transClip = CAR_IN; dp.carAnim = true; dp.transT = 0; dp.transLen = clip.length; dp.baseL = car.transform.InverseTransformPoint(root); PathInit(p, car, dp, true, CAR_IN); p.sys = CAR_IN;
            if (FBDoors.HasPart(car, 1)) FBDoors.SetMask(car, FBDoors.Mask(car) | 1, true); else FBAudio.PlayAt("veh_door_open", car.transform.position, .6f, 5);
            return true;
        }
        public static bool StartCarOut(FBCharacter p, FBCar car, bool needFree = true)
        {
            if (!CarAnims || !car || car.kind == "moto" || car.mx || Mathf.Abs(car.v) > 1.5f || !FBRegistry.I || !FBRegistry.I.Clip(CAR_OUT)) return false; var dp = p.GetComponent<FBDriverPose>(); if (!dp || dp.trans != null) return false;
            if (needFree && !DoorSideFree(car, p.ScaleK)) return false;
            var clip = FBRegistry.I.Clip(CAR_OUT); FBPoseBlend.Capture(p, CarBlend); dp.trans = "dismount"; dp.transClip = CAR_OUT; dp.carAnim = true; dp.transT = 0; dp.transLen = clip.length; dp.baseL = car.transform.InverseTransformPoint(p.transform.position); PathInit(p, car, dp, false, CAR_OUT); p.sys = CAR_OUT;
            if (FBDoors.HasPart(car, 1)) FBDoors.SetMask(car, FBDoors.Mask(car) | 1, true); else FBAudio.PlayAt("veh_door_open", car.transform.position, .6f, 5);
            if (car == cur) { car.v = 0; StopAudio(car); FBAudio.PlayAt("veh_stop", car.transform.position, .45f, 5); }
            return true;
        }
        // onde o clipe de sair deixou o corpo: no chão, embaixo do quadril, olhando para a frente do corpo
        public static bool OutPoint(FBCharacter p, FBCar car, out Vector3 at, out float yaw)
        {
            at = p.transform.position; yaw = p.Yaw; var an = p.an; var hips = an ? an.GetBoneTransform(HumanBodyBones.Hips) : null; if (!hips) return false;
            car.SetColliders(false); var gy = FBWorld.GroundY(hips.position.x, hips.position.y, hips.position.z, .3f, 2.5f); car.SetColliders(true);
            /* r45e: sem chão embaixo do quadril -> chão da porta (antes voltava a posição da raiz = meio do carro e o NPC aparecia no teto) */
            float g = gy ?? DoorOut(car, p.ScaleK).y; at = new Vector3(hips.position.x, g, hips.position.z);
            var lU = an.GetBoneTransform(HumanBodyBones.LeftUpperLeg); var rU = an.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            if (lU && rU) { var bf = Vector3.Cross(rU.position - lU.position, Vector3.up).Flat(); if (bf.sqrMagnitude > 1e-6f) yaw = Quaternion.LookRotation(bf).eulerAngles.y; }
            return true;
        }
        static readonly Dictionary<FBCar, (string clip, float t)> motoClip = new();
        public static string MotoClipOf(FBCar car) => car && motoClip.TryGetValue(car, out var c) ? c.clip : null;
        static void MotoRiderClip(FBCar car, FBCharacter a, float vmax, float dt)
        {
            if (!FBRegistry.I || !FBRegistry.I.Clip(MOTO_RIDE)) return;
            if (Time.time < car.riderHoldT) return;                                              // tranco da batida tocando (colision_on_bike_driving)
            float vk = Mathf.Abs(car.v) / Mathf.Max(1, vmax); string want;
            if (car.wh != null && car.wh.a > .15f) want = "X171_Moto_PuxandoGrau_Pose";
            else if (Mathf.Abs(car.v) < .6f) want = "X171_14_AA_MOTO_MOTOIdle01_Loop";
            else if (Mathf.Abs(car.lean) > .2f) want = car.lean > 0 ? MotoLeanPos : MotoLeanNeg;
            else if (vk > .62f) want = "X171_Idle_on_bike_driving_wind";
            else want = MOTO_RIDE;
            if (!FBRegistry.I.Clip(want)) want = MOTO_RIDE;
            motoClip.TryGetValue(car, out var cur); cur.t += dt;
            if (cur.clip != want && (cur.clip == null || cur.t > .35f)) { cur = (want, 0); a.sys = want; }
            motoClip[car] = cur;
        }
        // lado da inclinação (car.lean > 0) -> clipe; conferido na folha de poses / -fbmotolab
        public static string MotoLeanPos = "X171_Pose_on_bike_R", MotoLeanNeg = "X171_Pose_on_bike_L";
        public static string SitClip(FBCar u)
        {
            if (u && u.kind == "moto" && FBRegistry.I && FBRegistry.I.Clip(MOTO_RIDE)) return MOTO_RIDE;
            if (u && u.rigHands != null && FBRegistry.I.Clip(REB_SIT)) return REB_SIT;
            var c = (u && u.kind == "moto" ? SG_MOTO : SG_CAR)[0]; if (FBRegistry.I.Clip(c)) return c;
            return FBRegistry.I.Clip(REB_SIT) ? REB_SIT : null;
        }
        // ---- entrar / sair (usuário: direto no banco, sem animação de transição)
        public static bool Enter(FBCharacter p, FBCar car, bool instant = false)
        {
            if (!p || !car || car.driver || car.remoteDriver >= 0 || car.netOwner >= 0 || cur || !FBVehicles.Allowed) return false; CancelApproach(p);
            p.StopDance(true); prevWeaponVisible = p.weapon != null;
            // [parceiro] Correção: o carro parado em rampa ou meio-fio guarda a inclinação ao entrar (antes nivelava de uma vez
            // e balançava de volta). Moto continua saindo do descanso em pé.
            var eu = car.transform.eulerAngles; bool upright = car.kind == "moto" || car.mx;
            car.yaw = -eu.y * Mathf.Deg2Rad;
            car.pitch = upright ? 0 : Mathf.Clamp(Mathf.DeltaAngle(0, eu.x), -28, 28) * Mathf.Deg2Rad;
            car.roll = upright ? 0 : Mathf.Clamp(-Mathf.DeltaAngle(0, eu.z), -22, 22) * Mathf.Deg2Rad;
            car.lean = car.v = car.steer = car.vy = car.stIn = 0; car.air = false; car.gb = null; car.wh = FBMoto.NewWheelie(); car.eng = FBMoto.NewEngine(); car.shift = Vector3.zero;
            if (car.mx) car.mx.Park(0, -car.yaw * Mathf.Rad2Deg); else car.transform.rotation = CarRot(car.pitch, car.yaw, car.roll);   // r49i: MX de pé e nivelada (sem o pulo de 10° ao ligar a física)
            car.driver = p; FBVehicles.SetDriving(p, car); cur = car; curActor = p; lastCarYaw = car.transform.eulerAngles.y; motoYawLag = 0; exitPend = -1;
            var cc = p.GetComponent<CharacterController>(); if (cc) cc.enabled = false;
            p.vehiclePose = true; p.sys = SitClip(car); p.aiming = p.shooting = p.sprint = p.crouch = false; p.combat = false; p.SetWeaponVisible(false); p.velocity = Vector3.zero;
            p.gameObject.GetOrAdd<FBDriverPose>().Set(car);
            if (car.mx) { car.mx.Recover(); car.mx.SetActive(true); car.mx.StartBlip(); car.wh = FBMoto.NewWheelie(); if (!instant) StartMount(p, car); foreach (var c in p.GetComponentsInChildren<Collider>()) foreach (var bc in car.cols) if (c && bc) Physics.IgnoreCollision(c, bc); }
            else if (instant) { }                                                              // r50: garagem do rolê — nasce sentado (antes tocava o clipe de entrar saindo do meio do carro)
            else if (car.kind == "moto") { FBAudio.PlayAt("ie_moto_start", car.transform.position, .55f, 6); StartMount(p, car); }
            else if (StartCarIn(p, car)) { float tl = p.GetComponent<FBDriverPose>().transLen; car.StartCoroutineSafe(tl + .25f, () => { if (cur == car) FBAudio.PlayAt("veh_start", car.transform.position, .5f, 6); }); }
            else { FBAudio.PlayAt("veh_door_open", car.transform.position, .6f, 5); FBAudio.PlayAt("veh_start", car.transform.position, .5f, 6); car.StartCoroutineSafe(.52f, () => { if (cur == car) FBAudio.PlayAt("veh_door_close", car.transform.position, .6f, 5); }); }
            car.wallOn = FBParedao.State(car)?.on ?? false;
            if (p == FBMatch.Player) FBMirrors.OnEnter(car);                                    // retrovisores com a câmera de ré (como o REB)
            // câmera do veículo começa atrás e um pouco acima (como o De Rolê): herdava a inclinação da câmera a pé/1ª pessoa e podia nascer
            // rente ao chão, dentro da moto (22/10)
            if (p == FBMatch.Player && FBPlayer.I) { FBPlayer.I.camYaw = car.transform.eulerAngles.y; FBPlayer.I.camPitch = -7; }
            return true;
        }
        // ---- r50: IR ATÉ A PORTA (pedido de 31/10: "as correções continuam sem funcionar na Development Build"). Causa: o jogador aperta F a até
        // 2,2 m da lataria, de QUALQUER lado (FBVehicles.Nearest), mas os clipes do 171 começam do lado ESQUERDO (porta do motorista / lado de
        // subir na moto). O PathInit levava o quadril em LINHA RETA da posição do F até a porta: do lado direito, da frente ou de trás o corpo
        // atravessava a moto (print do PM no meio da XRE) e, a mais de 1,6 m da porta, pulava para ela num quadro. Os laboratórios teleportavam o
        // personagem exatamente na porta (FBVeicAnimLab) e nunca passaram por esse caminho. Agora o jogador ANDA (locomoção normal, colisão do
        // CharacterController) por fora da caixa do veículo até a porta e só então começa o clipe; mexer, pular, mirar, atirar ou F de novo
        // cancela; travado ou demorando, entra como antes. ApproachOn = false = como antes (teste -fbjogo compara os dois no mesmo build).
        public static bool ApproachOn = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbsemporta") < 0;
        public class Approach { public FBCar car; public readonly List<Vector3> pts = new(); public int i; public float t, tMax, stuckT; public Vector3 lastP; public bool ready, armed; public string diag; }
        static readonly Dictionary<FBCharacter, Approach> appr = new();
        public static int Approaches, ApproachFallbacks; public static string ApproachDiag;
        public static Vector3 WarpHips; public static int WarpFrame; public static float WarpK;
        public static int SysRestores; public static string SysRestoreLast;                         // r50: FBDriverPose repôs o clipe de entrar/sair apagado por outro sistema
        public static bool SysGuard = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbsemclipe") < 0;   // r50: false = F direto apaga o clipe (como antes)
        public static Approach ApproachOf(FBCharacter p) => p && appr.TryGetValue(p, out var a) ? a : null;
        public static bool Approaching(FBCharacter p) => ApproachOf(p) != null;
        public static void CancelApproach(FBCharacter p) { if (p) appr.Remove(p); }
        // rota no plano do veículo: direto se a reta não corta a caixa; senão pelos cantos (1 ou 2), a mais curta
        static bool SegHitsRect(Vector2 a, Vector2 b, Rect r)
        {
            if (r.Contains(a) || r.Contains(b)) return true; float t0 = 0, t1 = 1; var d = b - a;
            bool Clip(float p, float q) { if (Mathf.Abs(p) < 1e-6f) return q >= 0; float t = q / p; if (p < 0) { if (t > t1) return false; if (t > t0) t0 = t; } else { if (t < t0) return false; if (t < t1) t1 = t; } return true; }
            return Clip(-d.x, a.x - r.xMin) && Clip(d.x, r.xMax - a.x) && Clip(-d.y, a.y - r.yMin) && Clip(d.y, r.yMax - a.y) && t0 < t1;
        }
        public static List<Vector3> ApproachRoute(FBCar car, Vector3 from, Vector3 door, float k)
        {
            var T = car.transform; float ls = Mathf.Max(1e-4f, Mathf.Abs(T.lossyScale.x)); var b = car.lbox;
            Vector2 L(Vector3 w) { var q = T.InverseTransformPoint(w); return new Vector2(q.x, q.z); }
            Vector2 P = L(from), D = L(door); var body = new Rect(b.min.x, b.min.z, b.size.x, b.size.z);
            float dDoor = Mathf.Max(Mathf.Max(body.xMin - D.x, D.x - body.xMax), Mathf.Max(body.yMin - D.y, D.y - body.yMax));      // porta fora da caixa (local)
            float mo = Mathf.Clamp(dDoor - .04f / ls, 0, .2f * k / ls), mc = Mathf.Max(mo + .14f * k / ls, .36f * k / ls);
            var obst = new Rect(body.xMin - mo, body.yMin - mo, body.width + 2 * mo, body.height + 2 * mo);
            var C = new[] { new Vector2(body.xMin - mc, body.yMin - mc), new Vector2(body.xMin - mc, body.yMax + mc), new Vector2(body.xMax + mc, body.yMax + mc), new Vector2(body.xMax + mc, body.yMin - mc) };
            List<Vector2> best = null; float bl = float.MaxValue;
            void Try(List<Vector2> w) { float l = 0; for (int j = 1; j < w.Count; j++) { if (SegHitsRect(w[j - 1], w[j], obst)) return; l += (w[j] - w[j - 1]).magnitude; } if (l < bl) { bl = l; best = w; } }
            if (obst.Contains(P)) { var e = P; e.x = P.x < body.center.x ? obst.xMin - .02f : obst.xMax + .02f; P = e; }      // começou encostado: sai pelo lado mais perto
            Try(new List<Vector2> { P, D });
            for (int a = 0; a < 4; a++) { Try(new List<Vector2> { P, C[a], D }); Try(new List<Vector2> { P, C[a], C[(a + 1) % 4], D }); Try(new List<Vector2> { P, C[a], C[(a + 3) % 4], D }); }
            var res = new List<Vector3>(); if (best == null) return res;
            for (int j = 1; j < best.Count; j++) { var w = T.TransformPoint(new Vector3(best[j].x, b.center.y, best[j].y)); w.y = from.y; res.Add(w); }
            return res;
        }
        // F no jogo (FBVehicles.Toggle): perto da porta entra na hora; longe anda até ela
        public static bool BeginApproach(FBCharacter p, FBCar car)
        {
            if (!ApproachOn || !p || !car || p != FBMatch.Player || !FBRegistry.I) return false;
            bool anim = car.kind == "moto" || car.mx ? MotoAnims && FBRegistry.I.Clip(MOTO_MOUNT) : CarAnims && FBRegistry.I.Clip(CAR_IN) && DoorSideFree(car, p.ScaleK);
            if (!anim) return false; float k = p.ScaleK; var door = DoorOut(car, k);
            if ((door - p.Pos).Flat().magnitude < .32f * k) return false;
            var pts = ApproachRoute(car, p.Pos, door, k); if (pts.Count == 0) return false;
            float len = 0; var q = p.Pos; foreach (var w in pts) { len += (w - q).Flat().magnitude; q = w; }
            var a = new Approach { car = car, tMax = len / Mathf.Max(.5f, 1.3f * k) + 1.5f, lastP = p.Pos }; a.pts.AddRange(pts);
            a.diag = $"{car.id}: {pts.Count} trecho(s), {len:0.00} m até a porta"; appr[p] = a; Approaches++; ApproachDiag = a.diag; return true;
        }
        // FBPlayer: velocidade de andar até o próximo ponto (null = sem rota; cancela se o jogador mexer)
        public static Vector3? ApproachStep(FBCharacter p, FBInputState inp, float walk, float dt)
        {
            var a = ApproachOf(p); if (a == null) return null;
            // quem chega andando segura W ao apertar F: o movimento só cancela depois de soltar o direcional e mexer de novo
            if (inp.move.sqrMagnitude < .01f) a.armed = true;
            if (!a.car || a.car.driver || !p.alive || p.knocked || (a.armed && inp.move.sqrMagnitude > .12f) || inp.jump || inp.fire || inp.aim || inp.crouchToggle) { CancelApproach(p); return null; }
            if (a.ready) return Vector3.zero;
            a.t += dt; float k = p.ScaleK; var to = (a.pts[a.i] - p.Pos).Flat(); bool last = a.i == a.pts.Count - 1;
            if (to.magnitude < (last ? .14f : .3f) * k) { if (last) { a.ready = true; return Vector3.zero; } a.i++; to = (a.pts[a.i] - p.Pos).Flat(); last = a.i == a.pts.Count - 1; }
            if ((p.Pos - a.lastP).Flat().magnitude > .06f * k) { a.lastP = p.Pos; a.stuckT = 0; } else a.stuckT += dt;
            if (a.t > a.tMax || a.stuckT > .6f) { a.ready = true; ApproachFallbacks++; ApproachDiag = a.diag + (a.stuckT > .6f ? " · travou no caminho: entrou de onde estava" : " · demorou: entrou de onde estava"); return Vector3.zero; }
            float sp = walk; if (last) sp = Mathf.Min(walk, Mathf.Max(.6f * k, to.magnitude * 3.2f));                      // chega devagar na porta (sem passar)
            return to.normalized * sp;
        }
        // começo do Update do FBPlayer: chegou (ou desistiu) -> entra
        public static void ApproachTick(FBCharacter p)
        {
            var a = ApproachOf(p); if (a == null || !a.ready) return; CancelApproach(p);
            if (a.car && !a.car.driver && a.car.remoteDriver < 0) Enter(p, a.car);
        }
        // r46k: o começo de subir/descer da moto ligava a camada do clipe em peso 1 de uma vez — a pose saltava do parado (ou do pilotar) para o
        // 1º quadro do clipe num quadro só (sonda r46j: pé E 4.900°/s, mãos e cabeça juntos, sempre em t=0,03). Mistura a pose de antes até o clipe.
        const float MountBlend = .25f;
        public static void StartMount(FBCharacter p, FBCar car)
        {
            if (!MotoAnims || !FBRegistry.I || !FBRegistry.I.Clip(MOTO_MOUNT)) return; var dp = p.GetComponent<FBDriverPose>(); if (!dp) return;
            var seatW = car.L2W(car.seat + new Vector3(0, .42f, -.02f)); var root = seatW - car.transform.right * .36f * p.ScaleK;
            car.SetColliders(false); root.y = FBWorld.GroundY(root.x, root.y, root.z, .5f, 3) ?? car.transform.position.y; car.SetColliders(true);
            FBPoseBlend.Capture(p, MountBlend); dp.trans = "mount"; dp.transClip = MOTO_MOUNT; dp.carAnim = false; dp.transT = 0; dp.transLen = FBRegistry.I.Clip(MOTO_MOUNT).length / MOUNT_SPEED; dp.baseL = car.transform.InverseTransformPoint(root); PathInit(p, car, dp, true, MOTO_MOUNT); p.sys = MOTO_MOUNT;
        }
        public static bool StartDismount(FBCharacter p, FBCar car)
        {
            if (!MotoAnims || !FBRegistry.I || !FBRegistry.I.Clip(MOTO_DISMOUNT) || Mathf.Abs(car.v) > 1.5f) return false; var dp = p.GetComponent<FBDriverPose>(); if (!dp) return false;
            var seatW = car.mx ? SeatHips(car) : car.L2W(car.seat + new Vector3(0, .42f, -.02f)); var side = seatW - car.transform.right * .55f; DoorDiag = "livre";
            car.SetColliders(false); bool blocked = SideHit(car, seatW, -car.transform.right, .75f); if (!blocked && FBWorld.GroundY(side.x, seatW.y, side.z, .3f, 2.5f) == null) { blocked = true; DoorDiag = "sem chão do lado"; } car.SetColliders(true);
            if (blocked) return false;                                                       // lado esquerdo tomado: sai do jeito antigo
            FBPoseBlend.Capture(p, MountBlend); dp.trans = "dismount"; dp.transClip = MOTO_DISMOUNT; dp.carAnim = false; dp.transT = 0; dp.transLen = FBRegistry.I.Clip(MOTO_DISMOUNT).length / MOUNT_SPEED; dp.baseL = car.transform.InverseTransformPoint(p.transform.position); PathInit(p, car, dp, false, MOTO_DISMOUNT); p.sys = MOTO_DISMOUNT;
            car.rider = null; return true;
        }
        // ---- r45d: estilo do piloto NPC (pedido: "diferentes poses"): cada piloto pega um jeito de andar do 171 no lugar do MX_Ride/MX_Idle
        //  (tronco deitado, relaxado, de pé no pedal…); o IK do MX continua levando mãos/pés/quadril aos pontos do RigData
        static readonly string[] RIDE_STY = { null, "X171_Idle_on_bike_driving", "X171_Pose_on_bike_Fwd", "X171_Pose_on_bike_Fwd_adt", "X171_exported_root_idle", "X171_pose_moto_exported_root_pose_f_2" },
            // r46k: saiu o "X171_13_AA_MOTO_Idle_MOTOIdle03" — com a mão esquerda no punho do guidão (IK) o ombro passava do limite do avatar em
            // 8 de 10 subidas (braço E "Down-Up" até 2,6 e torção 2,0; os outros três estilos: 0 de 32) e a queda nascia com o braço quebrado
            IDLE_STY = { null, "X171_14_AA_MOTO_MOTOIdle01_Loop", "X171_16_AA_MOTO_MOTOIdle02_Loop" };
        public static string RiderStyle(FBCharacter a, FBCar u, string want)
        {
            var L = want == "MX_Ride" ? RIDE_STY : want == "MX_Idle" ? IDLE_STY : null; if (L == null) return want;
            int h = ((a.skinId ?? a.name).GetHashCode() * 31 + (u.syncId ?? u.id ?? "").GetHashCode()) & 0x7fffffff; var s = L[h % L.Length];
            return s != null && FBRegistry.I && FBRegistry.I.Clip(s) ? s : want;
        }
        // ---- r45d: caminho do quadril nas transições (FBAnimPaths): reta "em pé do lado de fora" <-> "banco" com o ritmo/forma do clipe
        static readonly Dictionary<FBCharacter, float> hipStand = new();
        public static int PathInits, PathFallback; public static string PathDiag;
        // altura do quadril em pé (medida quando o personagem começa a subir em pé; senão pelas pernas)
        public static float HipStandH(FBCharacter p)
        {
            var an = p.an; float est = .92f * p.ScaleK;
            if (an && an.isHuman)
            {
                Transform lU = an.GetBoneTransform(HumanBodyBones.LeftUpperLeg), lL = an.GetBoneTransform(HumanBodyBones.LeftLowerLeg), lF = an.GetBoneTransform(HumanBodyBones.LeftFoot), h = an.GetBoneTransform(HumanBodyBones.Hips);
                if (lU && lL && lF && h) est = (lU.position - lL.position).magnitude + (lL.position - lF.position).magnitude + .075f * p.ScaleK + Mathf.Max(0, Vector3.Dot(h.position - lU.position, p.transform.up));
            }
            return hipStand.TryGetValue(p, out var m) && Mathf.Abs(m - est) < est * .2f ? m : est;
        }
        // onde o quadril fica no veículo (MX: pose de pilotar do RigData; carro/moto: banco)
        public static Vector3 SeatHips(FBCar u) => u.mx ? u.mx.CurHipsW : u.L2W(u.seat + (u.kind == "moto" ? new Vector3(0, .42f, -.02f) : Vector3.zero));
        // ponto no chão do lado do motorista (esquerdo) onde a pessoa fica em pé para entrar/sair
        public static Vector3 DoorOut(FBCar u, float k)
        {
            var T = u.transform; var left = -T.right; var seat = u.mx ? SeatHips(u) : u.L2W(u.seat); Vector3 p;
            if (u.kind == "moto") p = seat + left * .62f * k;
            else { float side = Mathf.Abs(Vector3.Dot(T.TransformPoint(new Vector3(u.lbox.min.x, u.seat.y, u.seat.z)) - T.TransformPoint(u.seat), T.right)); p = seat + left * (side + .38f * k) - T.forward * .17f * k; }
            u.SetColliders(false); var gy = FBWorld.GroundY(p.x, seat.y + .5f, p.z, .4f, 3); u.SetColliders(true);
            p.y = gy ?? T.position.y; return p;
        }
        static void PathInit(FBCharacter p, FBCar car, FBDriverPose dp, bool mount, string clip)
        {
            dp.path = false; dp.pathClip = clip; var an = p.an; var hips = an ? an.GetBoneTransform(HumanBodyBones.Hips) : null; if (!hips || !FBAnimPaths.Has(clip)) { PathFallback++; return; }
            if (mount) { float m = hips.position.y - p.transform.position.y; if (m > .4f * p.ScaleK) hipStand[p] = m; }
            var stand = DoorOut(car, p.ScaleK) + Vector3.up * HipStandH(p); var T = car.transform;
            dp.pathS = T.InverseTransformPoint(stand); dp.pathE = dp.pathS;
            dp.pathS0 = mount && (hips.position - stand).magnitude < 1.6f ? T.InverseTransformPoint(hips.position) : dp.pathS;
            // r47c: a ida até a porta (e o giro para ela) usava sempre os primeiros 15 % do clipe (~0,39 s): começando até 1,6 m da porta o
            // corpo deslizava a 6–11 m/s (laboratório: raiz andando 11–21 cm num quadro em t≈0,12–0,19 s em 20 de 20 entradas, igual na r46).
            // Agora a janela cresce com a distância — pico do SmoothStep (1,5 × média) até ~2,1 m/s —, entre 15 % e 35 % do clipe
            float dA = (T.TransformPoint(dp.pathS0) - stand).Flat().magnitude; dp.pathK = Mathf.Clamp(1.5f * dA / (2.1f * Mathf.Max(.5f, dp.transLen)), .15f, .35f);
            dp.yaw0 = Mathf.DeltaAngle(T.eulerAngles.y, p.transform.eulerAngles.y); dp.path = true; PathInits++;
            PathDiag = $"{clip} · em pé {HipStandH(p):0.00} · porta {(stand - SeatHips(car)).Flat().magnitude:0.00} m do banco · desnível {stand.y - SeatHips(car).y:0.00} · ida à porta {dA:0.00} m em {dp.pathK * 100:0}% do clipe";
        }
        // fim do clipe de descer: fica em pé onde o clipe deixou o quadril, virado para onde o corpo olha
        public static void FinishDismount(FBCharacter p, FBCar car)
        {
            var an = p.an; var hips = an ? an.GetBoneTransform(HumanBodyBones.Hips) : null; Vector3? at = null; float? yaw = null;
            if (hips)
            {
                car.SetColliders(false); var gy = FBWorld.GroundY(hips.position.x, hips.position.y, hips.position.z, .3f, 2.5f); car.SetColliders(true);
                at = new Vector3(hips.position.x, gy ?? DoorOut(car, p.ScaleK).y, hips.position.z);
                var lU = an.GetBoneTransform(HumanBodyBones.LeftUpperLeg); var rU = an.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                if (lU && rU) { var bf = Vector3.Cross(rU.position - lU.position, Vector3.up).Flat(); if (bf.sqrMagnitude > 1e-6f) yaw = Quaternion.LookRotation(bf).eulerAngles.y; }
            }
            Exit(p, true, at, yaw);
        }
        // teste (FBR37Test): senta QUALQUER personagem no veículo (o Enter só aceita um motorista local) — mesma pose do jogador
        public static void SeatTest(FBCharacter a, FBCar car)
        {
            a.StopDance(true); var cc = a.GetComponent<CharacterController>(); if (cc) cc.enabled = false;
            a.vehiclePose = true; a.sys = SitClip(car); a.aiming = a.shooting = a.sprint = a.crouch = false; a.combat = false; a.SetWeaponVisible(false); a.velocity = Vector3.zero;
            car.driver = a; if (car.kind == "moto") car.rider = new Rider(); a.gameObject.GetOrAdd<FBDriverPose>().Set(car);
        }
        // sai pela porta do motorista; bloqueada -> pela direita, por trás ou (forçado) por cima
        static float scrapeT, squealT, brkP, surfT, skidK; static int surf; static bool hardOn;
        public static bool Exit(FBCharacter p, bool force, Vector3? at = null, float? atYaw = null)
        {
            var car = FBVehicles.CarOf(p); if (!car) return false;
            var dpx = p.GetComponent<FBDriverPose>(); if (!force && dpx && dpx.trans != null) return false;          // no meio de subir/descer
            if (!force && Mathf.Abs(car.v) > 6) { FBHUD.I?.Center("REDUZA PARA SAIR", 1); return false; }
            // r47: entre 1,5 e 6 m/s o jogador saía SEM animação (o descer/sair animado só aceita |v| ≤ 1,5 e a saída direta aceita até 6): era o
            // "a animação não carrega" ao apertar F rodando devagar. Agora o pedido fica guardado, o veículo freia sozinho e o clipe começa ao parar.
            if (!force && at == null && !exitDirect && p == FBMatch.Player && Mathf.Abs(car.v) > 1.5f && AnimExit(car)) { if (exitPend < 0) { exitPend = 3; FBHUD.I?.Center("PARANDO PARA SAIR", 1.2f); } return true; }
            if (!force && at == null && car.kind == "moto" && StartDismount(p, car)) return true;
            if (!force && at == null && car.kind != "moto" && StartCarOut(p, car)) return true;
            bool fromAnim = dpx && dpx.carAnim;
            FpHead(p, false); float s = car.scale, half = car.lbox.extents.x * s;
            var cands = new List<Vector3>(); if (car.rigOut is Vector3 ro) cands.Add(ro * s);
            cands.Add(new Vector3(-(half + .75f), 0, car.seat.z * s)); cands.Add(new Vector3(half + .75f, 0, car.seat.z * s)); cands.Add(new Vector3(0, 0, car.lbox.min.z * s - 1.1f));
            Vector3? outP = at; var cp = car.transform.position; car.SetColliders(false);
            try
            {
                foreach (var c in at != null ? new List<Vector3>() : cands)
                {
                    var wp = Quaternion.Euler(0, -car.yaw * Mathf.Rad2Deg, 0) * c + cp; if (!(FBWorld.GroundY(wp.x, cp.y + 1.5f, wp.z, .5f, 4) is float gy)) continue;
                    // [parceiro] fisica 31/10: rejeita ponto de saida mais de 0,6 m acima da base do carro (teto do carro vizinho, topo de muro)
                    var to = (wp - cp).Flat(); bool blocked = gy - cp.y >= .6f || FBWorld.RayHit(new Vector3(wp.x, gy + .3f, wp.z), Vector3.up, 1.6f) || FBWorld.RayHit(new Vector3(cp.x, gy + 1, cp.z), to.normalized, to.magnitude);
                    if (!blocked || force) { outP = new Vector3(wp.x, gy, wp.z); break; }
                }
            }
            finally { car.SetColliders(true); }
            if (outP == null) { if (!force) { FBHUD.I?.Center("SEM ESPAÇO PARA SAIR", 1); return false; } outP = cp + Vector3.up * (car.lbox.max.y * s + .1f); }
            StopAudio(car); FBGrauHud.Hide(); if (p == FBMatch.Player) FBMirrors.OnExit(car);
            if (car.kind != "moto" && fromAnim) { var c2 = car; car.StartCoroutineSafe(.25f, () => { if (c2 && !c2.driver) { if (FBDoors.HasPart(c2, 1)) FBDoors.SetMask(c2, FBDoors.Mask(c2) & ~1, true); else FBAudio.PlayAt("veh_door_close", cp, .55f, 5); } }); }   // saiu pelo clipe: fecha a porta
            else if (car.kind != "moto") { FBAudio.PlayAt("veh_stop", cp, .45f, 5); FBAudio.PlayAt("veh_door_open", cp, .6f, 5); car.StartCoroutineSafe(.5f, () => FBAudio.PlayAt("veh_door_close", cp, .55f, 5)); }   // motor desligando (171)
            // [parceiro] fisica 31/10: saida forcada (piloto morreu) com a moto andando ou no ar: ela fica na fisica e cai sozinha,
            // como no caminho de queda, em vez de virar cinematica e parar na hora (ou ficar parada no ar)
            else if (car.mx && force && at == null && car.mx.active && !car.mx.crashed && car.mx.vmag * 1.2f > 1.5f) car.mx.Abandon();
            else if (car.mx && car.mx.crashed) { }                                                   // caída: a física continua até ela parar (FBMxBike)
            else if (car.mx) { car.mx.SetActive(false); car.mx.Park(.1f, null, .45f); }                  // r49i: nivelada nas rodas, deita no descanso em 0,45 s
            else car.transform.rotation = CarRot(0, car.yaw, .1f);
            car.v = car.steer = 0; FBCarPhys.Reset(car); car.driver = null; FBVehicles.SetDriving(p, null); if (cur == car) { cur = null; curActor = null; }
            var dp = p.GetComponent<FBDriverPose>(); if (dp) Object.Destroy(dp);
            float oy = atYaw ?? -car.yaw * Mathf.Rad2Deg; p.transform.SetPositionAndRotation(outP.Value, Quaternion.Euler(0, oy, 0)); p.Yaw = oy;
            var cc = p.GetComponent<CharacterController>(); if (cc) cc.enabled = true;
            p.sys = null; p.vehiclePose = false; p.velocity = Vector3.zero; p.SetWeaponVisible(prevWeaponVisible && p.weapon != null); car.wallOn = FBParedao.State(car)?.on ?? false;
            return true;
        }
        // garagem do rolê (rodada 38): o jogador fica sentado no carro em prévia; trocar de carro solta o assento sem porta nem lugar de saída
        // (a prévia vai ser destruída). O FBDriverPose fica — o Enter do próximo carro o reaproveita (Destroy só valeria no fim do quadro)
        public static void ReleaseSeat(FBCharacter p)
        {
            var car = FBVehicles.CarOf(p); if (!car) return;
            StopAudio(car); FBGrauHud.Hide(); if (p == FBMatch.Player) FBMirrors.OnExit(car);
            car.v = car.steer = 0; FBCarPhys.Reset(car); car.driver = null; FBVehicles.SetDriving(p, null); if (cur == car) { cur = null; curActor = null; }
            p.sys = null; p.vehiclePose = false; p.velocity = Vector3.zero;
        }
        static void StopAudio(FBCar car)
        {
            foreach (var k in new[] { "veh_engine", "veh_moto", "veh_skid", "veh_skid_dirt", "veh_horn", "veh_wind", "veh_brake", "veh_rev", "veh_gb_par", "veh_gb_ace", "veh_gb_cut" }) FBAudio.StopLoop(k);
            FBCarSound.StopEngine("veh_e_");
        }
        public static bool ToggleSoundWall(FBCar car) { if (!car || !FBParedao.Has(car)) return false; bool ok = FBParedao.Toggle(car); car.wallOn = FBParedao.State(car)?.on ?? false; return ok; }

        // r47: saída pedida em movimento (Exit): exitPend = segundos que ainda espera o veículo parar (−1 = nenhuma); exitDirect = já esperou, sai direto
        static float exitPend = -1; static bool exitDirect;
        static bool AnimExit(FBCar car) => FBRegistry.I && (car.kind == "moto" ? MotoAnims && FBRegistry.I.Clip(MOTO_DISMOUNT) : CarAnims && !car.mx && FBRegistry.I.Clip(CAR_OUT));
        public static bool ExitPending => exitPend >= 0;
        // ---- entrada do jogador (no lugar do updatePlayer enquanto dirige)
        public static void PlayerInput(FBCharacter ch, FBInputState inp, float dt)
        {
            var car = FBVehicles.CarOf(ch); if (!car) return;
            if (FBMultimidia.Open) { if (inp.multimidia || inp.pause) FBMultimidia.Toggle(); Step(car, ch, 0, 0, false, false, false, dt); return; }
            if (FBGarage.Open_) { Step(car, ch, 0, 0, false, false, false, dt); return; }      // rolê: sentado no carro em prévia, sem controle até PEGAR
            if (exitPend >= 0)
            {   // freia sozinho; parou (ou 3 s) -> sai pelo clipe (ou direto, se ainda rodando); F de novo cancela
                exitPend -= dt; float av = Mathf.Abs(car.v);
                if (av <= 1.4f || exitPend < 0) { exitPend = -1; exitDirect = av > 1.5f; try { Exit(ch, false); } finally { exitDirect = false; } return; }
                if (inp.interact) { exitPend = -1; FBHUD.I?.Center("SAÍDA CANCELADA", .8f); return; }
                CamZoomInput(inp); Step(car, ch, car.v > 0 ? -1 : 1, 0, false, false, false, dt); return;
            }
            if (inp.interact) { Exit(ch, false); return; }
            FBVehicles.fp = false;   // 1ª pessoa nos veículos removida (pedido do usuário)
            CamZoomInput(inp);
            if (inp.multimidia) FBMultimidia.Toggle();
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.nKey.wasPressedThisFrame) ToggleSoundWall(car);
            bool horn = kb != null && kb.hKey.isPressed; FBCarLights.PlayerControl(car); FBSiren.PlayerControl(car);   /* L faróis · Q/E setas · X pisca-alerta · B sirene (viatura) */
            FBDoors.Keys(car);                                                                 // U portas · J capô · P porta-malas
            float thr = Mathf.Clamp(inp.move.y * (FBInput.Touch ? 1.3f : 1), -1, 1), st = Mathf.Clamp(-inp.move.x * (FBInput.Touch ? 1.3f : 1), -1, 1);
            if (LabDrive != null) { var ld = LabDrive(); thr = ld.x; st = -ld.y; }
            mxStand = inp.sprintHeld; mxTrickA = inp.fire; mxTrickB = inp.aim;                            // r44 (MX Grau): SHIFT em pé · no grau: botão esq./dir. = manobras
            Step(car, ch, thr, st, inp.jumpHeld, inp.jumpHeld, horn, dt);
        }
        // ---- r44: moto do MX Grau — a física roda no FixedUpdate do FBMxBike; aqui só entrada, HUD e eventos
        static string Fmt(float d) => d.ToString("0.0").Replace('.', ',') + " m";
        static void MxStep(FBCar car, FBCharacter player, float thr, float st, bool wheelKey, bool horn, float dt)
        {
            var mx = car.mx; var dp = player ? player.GetComponent<FBDriverPose>() : null; bool trans = dp && dp.trans != null, alive = player && player.alive;
            if (!alive || trans) { thr = mx.speedKmh > 1 ? -1 : 0; st = 0; wheelKey = false; }
            bool rev = alive && !trans && mxStand && Mathf.Abs(mx.speedKmh) < 3 && thr > 0;                  // SHIFT + W parado: giro em ponto morto
            mx.Input(-st, rev ? 0 : thr, wheelKey, rev, mxStand && !rev, mxTrickA, mxTrickB, dt);
            if (horn && Time.time > car.hornT) car.Horn();
            if (mx.crashed) return;
            // placar do grau (mesmo HUD das motos do Grau Brasil): distância com a frente acima de 17°, recorde da sessão
            var W = car.wh ??= FBMoto.NewWheelie(); float a = mx.onWheelie ? Mathf.Max(0, mx.wheelieAngle) : 0; W.a = a * Mathf.Deg2Rad; W.trick = mx.trickA || mx.trickB ? 1 : 0; float v = Mathf.Abs(car.v);
            if (a > 17) { W.dist += v / 1.2f * dt; W.t += dt; W.peak = Mathf.Max(W.peak, W.a); }
            else if (a < 5 && (W.dist > 0 || W.t > 0)) { if (W.dist > 4) { W.last = W.dist; W.best = Mathf.Max(W.best, W.dist); car.grauMsg = ("GRAU " + Fmt(W.dist), W.dist >= W.best ? "NOVO RECORDE" : "RECORDE " + Fmt(W.best), false, Time.time + 2.2f); } W.dist = 0; W.t = 0; W.peak = 0; }
            if (player == FBMatch.Player) { FBGrauHud.Show(car); }
            if (Mathf.Abs(car.v) > 3) RunOver(car, car.v, car.transform.forward);
            if (mx.rabeta && Time.time > scrapeT) { scrapeT = Time.time + .25f; FBAudio.PlayAt("veh_scrape", car.transform.position - car.transform.forward * 1.1f, .5f, 5); }
        }
        // queda do MX (fallAngle, batida forte, empinada parada): o piloto é jogado para fora e a moto tomba sozinha
        public static void MxCrash(FBCar car, string why)
        {
            var p = car.driver; var at = car.transform.position; var vel = car.transform.forward * car.v;
            FBAudio.PlayAt("reb_carimpact3", at, .8f, 6); FBNpcRotina.Event("caimoto", at, 28, 1);
            if (!p) return; if (p == FBMatch.Player) Shake(1);
            var keepP = car.transform.position; var keepR = car.transform.rotation;
            var side = at - car.transform.right * .9f + Vector3.up * .2f; FBFall.Grab(p, car); Exit(p, true, side, car.transform.eulerAngles.y);   /* r46: pose de pilotar guardada antes de soltar (Exit teleporta a raiz) */
            car.transform.SetPositionAndRotation(keepP, keepR);
            if (!FBFall.FromBike(p, car, why)) { p.velocity = vel * .55f + Vector3.up * 2.5f; p.grounded = false; }   /* r45d: clipe de queda do 171 */
            FBCombat.DamageActor(p, why == "batida" ? Mathf.Clamp(Mathf.Abs(car.v) * 2.2f, 8, 45) : 6, false, null, "fall");
            car.grauMsg = ("CAIU DA MOTO", why == "loop" ? "PASSOU DO PONTO DE EQUILÍBRIO" : why == "batida" ? "BATIDA" : "", true, Time.time + 2.2f);
        }
        // ---- física (drive.js update)
        static bool mxStand, mxTrickA, mxTrickB;
        public static System.Func<Vector2> LabDrive;                                                       // laboratório (r49i): (acelerador, esterço p/ direita) no lugar do controle
        static void Step(FBCar car, FBCharacter player, float thr, float st, bool space, bool wheelSpace, bool horn, float dt)
        {
            if (car.mx) { MxStep(car, player, thr, st, wheelSpace, horn, dt); return; }
            if (player && player.GetComponent<FBDriverPose>() is { trans: not null }) { space = wheelSpace = false; thr = car.v > .3f ? -1 : 0; st = 0; }   // subindo/descendo da moto
            var Kd = KindOf(car); bool moto = car.kind == "moto", alive = player && player.alive, hb = !moto && space, wheelKey = moto && wheelSpace && alive; if (!alive) { thr = 0; st = 0; }
            if (hold) { HoldStep(car, player, dt); return; }
            bool phys = FBCarPhys.On && !moto;                                                    // r43: física de pneus no estilo do FGear do 171
            if (!moto) { float pull = FBCarDamage.Pull(car); if (pull != 0) st = Mathf.Clamp(st + pull * Mathf.Clamp01(Mathf.Abs(car.v) / 10), -1, 1); Kd.grip *= FBCarDamage.GripK(car); }   // r45: dano puxa a direção / tira aderência
            if (phys) { car.stIn = FBCarPhys.SteerIn(car, st, Mathf.Abs(car.v) * 3.6f / 1.2f, dt); st = car.stIn; }
            else { float vk = Mathf.Clamp01(Mathf.Abs(car.v) / 25), rin = (moto ? 2.6f : 4.2f) - vk * 1.2f; car.stIn = Mathf.Lerp(car.stIn, st, 1 - Mathf.Exp(-dt * (st != 0 && Mathf.Sign(st) == Mathf.Sign(car.stIn == 0 ? st : car.stIn) ? rin : 7))); st = car.stIn; }
            var pos = car.transform.position; if (moto && car.shift.sqrMagnitude > 0) pos -= car.shift;
            car.burn = alive && !car.air && thr > .5f && hb && Mathf.Abs(car.v) < 5;
            if (thr > .6f && !hb && Mathf.Abs(car.v) < 1 && !car.air && car.prevThr <= .6f) car.launchT = .9f;
            car.launchT = thr > .3f ? Mathf.Max(0, car.launchT - dt) : 0; car.prevThr = thr;
            if (!moto) { float pk = FBCarDamage.PowerK(car); if (pk <= 0 && thr > 0) thr = 0; else thr *= thr > 0 ? pk : 1; }   // motor fraco / quebrado (dano)
            float v0 = car.v, accK = (car.turbo ? 1.12f : 1) * (car.tracao == "4x4" ? 1.06f : car.tracao == "FWD" ? .97f : 1);   // oficina: turbo / tração
            float ea = moto ? FBMoto.EngineStep(car.eng, Mathf.Max(0, car.v), thr > 0 ? thr : 0, dt, Kd.vmax, Kd.acc * 1.3f) : 0;
            float cut = !moto && car.gb != null && car.gb.shiftT > 0 ? .3f : 1;                       // troca de marcha: embreagem, a força some por um instante
            if (thr > 0) car.v += moto ? (car.v < -.5f ? Kd.brake * thr : ea) * dt : (car.v < -.5f ? Kd.brake : PowerAcc(car, Kd, car.v, accK) * cut) * thr * dt;
            else if (thr < 0) car.v += (car.v > .5f ? -Kd.brake : -Kd.acc * .7f) * -thr * dt;
            else car.v -= JSign(car.v) * Mathf.Min(Mathf.Abs(car.v), (1.2f + Mathf.Abs(car.v) * .05f) * dt);
            if (hb) car.v -= JSign(car.v) * Mathf.Min(Mathf.Abs(car.v), (phys ? 5 : 9) * dt);   // física nova: a traseira desliza de lado em vez de só frear
            if (car.air) car.v = v0;
            car.v = Mathf.Clamp(car.v, -Kd.rev, moto ? Kd.vmax : TopV(car) * FBCarDamage.TopK(car));
            float kmh = Mathf.Abs(car.v) * 3.6f / 1.2f, maxSt = moto ? FBMoto.SteerAngle(car.v / 1.2f) : Mathf.Lerp(Kd.steerDeg, 5, Mathf.Min(1, kmh / 200)) * Mathf.Deg2Rad;
            float axL = (car.v - v0) / Mathf.Max(dt, 1e-4f);                                       // aceleração do motor/freio (carga nos eixos)
            if (phys) { FBCarPhys.Lateral(car, Kd, hb, axL, dt); maxSt = Mathf.Max(car.lim, .05f); car.steerN = Mathf.Clamp(car.steer / maxSt, -1, 1); }
            else
            {
                car.steer = Mathf.Lerp(car.steer, st * maxSt, 1 - Mathf.Exp(-dt * 7)); car.steerN = Mathf.Clamp(car.steer / Mathf.Max(.05f, maxSt), -1, 1);
                float yawRate = car.v / car.wheelbase * Mathf.Tan(car.steer) * (hb && Mathf.Abs(car.v) > 6 ? 1.55f : 1);
                if (moto) yawRate = FBMoto.YawRate(car.v, car.steer, car.lean, car.wheelbase) * (car.wh.a > .15f ? FBMoto.STEER_IN_WHEELIE : 1);
                if (!moto && Mathf.Abs(car.v) > 1) { float lat = 14 * Kd.grip * (hb ? 1.55f : 1) / Mathf.Abs(car.v); yawRate = Mathf.Clamp(yawRate, -lat, lat); }
                if (!car.air) car.yaw += yawRate * dt;
                car.vl = car.yr = 0;
            }
            // movimento + batida (raios no para-choque na direção do movimento) — o próprio veículo fora das consultas
            car.SetColliders(false);
            try
            {
                var L = car.lbox; float s = car.scale, sgn = JSign(car.v) == 0 ? 1 : JSign(car.v), step = Mathf.Abs(car.v) * dt;
                var f = Fwd(car.yaw); var r = Left(car.yaw);
                if (step > 1e-4f)
                {
                    // caixa com a LARGURA inteira do carro varrendo o caminho (antes: 3 raios — poste/árvore fino passava entre eles);
                    // objetos pequenos destrutíveis no caminho quebram (FBDestructible) e não param o carro
                    float zEdge = (sgn > 0 ? L.max.z : L.min.z) * s, cx = -L.center.x * s; RaycastHit? hit = null;
                    var half = new Vector3(moto ? .3f : L.extents.x * s * .94f, .38f, .12f); var o = pos + f * (zEdge - sgn * .3f) + r * cx + Vector3.up * .9f;
                    foreach (var h in Physics.BoxCastAll(o, half, f * sgn, Quaternion.LookRotation(f), step + .32f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore))
                    {
                        if (h.distance <= 0 && h.point == Vector3.zero) continue;                      // já encostado no início: ignora (sai de ré/gira)
                        if (Mathf.Abs(car.v) > 2 && FBDestructible.TryHit(h.collider, 9999, h.point, f * sgn)) continue;
                        { float lose = FBFisica.Knock(h.collider, f * sgn, Mathf.Abs(car.v), h.point); if (lose >= 0) { car.v *= 1 - lose; FBCarDamage.OnImpact(car, h.point, -f * sgn, Mathf.Abs(car.v) * lose * 2); Shake(.25f); continue; } }   // r45: placa/orelhão/ponto do 171 cedem
                        var pole = FBStreetPoleFx.PoleOf(h.collider); if (pole && (pole.down || pole.falling)) continue;   // poste caído não segura o carro
                        if (pole && !moto && Mathf.Abs(car.v) > 3.5f && car.crashT <= 0)
                        {
                            float pv = Mathf.Abs(car.v); FBCarDamage.OnImpact(car, h.point, -f * sgn, pv * .8f); car.crashT = .35f; Shake(Mathf.Min(1, pv / 16));
                            if (pole.Hit(f * sgn, pv, h.point)) { car.v *= .55f; continue; }                       // cedeu: o carro passa por cima com o tranco
                        }
                        if (hit == null || h.distance < hit.Value.distance) hit = h;
                    }
                    if (hit != null)
                    {
                        // velocidade CONTRA a superfície (de frente = tudo; de raspão = pouco): batida de raspão desliza e vira para a tangente
                        var hitN = hit.Value.normal; hitN.y = 0; hitN = hitN.sqrMagnitude > 1e-4f ? hitN.normalized : -f * sgn; float cosA = Mathf.Abs(Vector3.Dot(f * sgn, hitN));
                        float imp = Mathf.Abs(car.v) * Mathf.Max(.25f, cosA); var other = hit.Value.collider.GetComponentInParent<FBCar>();
                        if (cosA < .55f && !moto)
                        {   // raspão: mantém a parte tangente da velocidade, gira o carro para longe da parede
                            var tan = (f * sgn - hitN * Vector3.Dot(f * sgn, hitN)).normalized; float turn = Vector3.SignedAngle(f * sgn, tan, Vector3.up) * Mathf.Deg2Rad;
                            car.yaw -= turn * Mathf.Min(1, dt * 7); car.v *= 1 - cosA * .7f; pos += hitN * .02f;
                            if (Mathf.Abs(car.v) > 4 && Time.time - scrapeT > 1.2f) { scrapeT = Time.time; FBAudio.PlayAt("veh_scrape", hit.Value.point, Mathf.Min(.8f, .3f + Mathf.Abs(car.v) / 30), 5); FBFx.Sparks(hit.Value.point, hit.Value.normal); }   // lataria raspando no muro (171)
                        }
                        else car.v = -car.v * .28f;
                        if (phys) FBCarPhys.OnHit(car, cosA < .55f ? .6f : .3f);
                        if (imp > 3 && car.crashT <= 0)
                        {
                            if (moto && car.driver && Time.time > car.riderHoldT && FBRegistry.I && FBRegistry.I.Clip(MOTO_CRASH)) { car.driver.sys = MOTO_CRASH; car.riderHoldT = Time.time + .9f; }   // r43: tranco do piloto (171)
                            FBAudio.PlayAt("reb_carimpact" + (2 + Mathf.Min(3, Mathf.FloorToInt(imp / 6))), pos, Mathf.Min(1, .35f + imp / 20), 6); car.crashT = .4f; Shake(Mathf.Min(1, imp / 18)); FBFx.Sparks(hit.Value.point, hit.Value.normal);
                            FBCarDamage.OnImpact(car, hit.Value.point, hitN, imp);                                  // amassa, quebra luz/vidro, empena peça
                            if (other && other != car)
                            {
                                FBCarDamage.OnImpact(other, hit.Value.point, -hitN, imp * .85f);
                                FBCarDamage.Shove(other, f * sgn * imp * .35f, Random.Range(-1f, 1f) * imp * 4);   // parado: desliza com o tranco
                            }
                        }
                    }
                    else pos += f * (car.v * dt);
                }
                if (phys && Mathf.Abs(car.vl) * dt > 1e-4f) pos = SlideSide(car, pos, f, r, dt);
                car.crashT -= dt;
                // chão: sondas nas rodas dianteira/traseira (e laterais no carro) -> altura, arfagem e rolagem
                float hb2 = car.wheelbase / 2, ht = car.track / 2;
                float? G(float fz, float lx) { var p = pos + f * fz + r * lx; return FBWorld.GroundY(p.x, pos.y + .9f, p.z, .3f, 2.5f); }
                float? yF = G(hb2, 0), yB = G(-hb2, 0), yL = moto ? null : G(0, ht), yR = moto ? null : G(0, -ht);
                var ys = new[] { yF, yB, yL, yR }.Where(y => y != null).Select(y => y.Value).ToList();
                if (ys.Count > 0)
                {
                    float gy = ys.Average();
                    // [parceiro] fisica 31/10: na descida o Lerp a 25/s atrasa ~inclinacao*v/25 do chao; com so 5 cm o carro caia e pousava com reb_bump + Shake
                    // a cada ~0,2 s e em ladeira forte ficava "no ar". Ja apoiado (vy==0) a tolerancia cobre esse atraso com folga de 2,5x (max 0,45 m,
                    // dentro da janela de 1,6 m do G()). A descida e medida pelo pitch que o carro ja segue (>0 = nariz para baixo; de re com pitch<0
                    // da o mesmo sinal), nao pela velocidade: no plano, na crista de lombada e na borda de rampa/meio-fio (nariz ainda reto ou para
                    // cima) fica 5 cm e o carro voa como antes. Atraso na forma discreta do Lerp, para nao depender do FPS. Pouso (vy!=0) mantem 5 cm.
                    float desce = Mathf.Max(0, car.v * Mathf.Tan(car.pitch));
                    float atraso = desce * dt / Mathf.Max(1e-4f, 1 - Mathf.Exp(-dt * 25));
                    float tolChao = car.vy == 0 ? .05f + Mathf.Min(.4f, atraso * 2.5f) : .05f;
                    if (gy >= pos.y - tolChao) { pos.y = Mathf.Lerp(pos.y, gy, 1 - Mathf.Exp(-dt * 25)); car.vy = 0; car.air = false; }
                    else
                    {
                        car.vy -= 22 * dt; pos.y += car.vy * dt;
                        if (pos.y <= gy) { if (phys) car.bHvV += Mathf.Max(-.6f, car.vy * .03f); if (car.vy < -4) { FBAudio.PlayAt(car.vy < -9 ? "reb_carimpact2" : "reb_bump", pos, Mathf.Min(.8f, .3f - car.vy / 20), 5); Shake(Mathf.Min(.6f, -car.vy / 18)); } pos.y = gy; car.vy = 0; }
                        car.air = pos.y > gy + .25f;
                    }
                    float pt = yF != null && yB != null ? -Mathf.Atan2(yF.Value - yB.Value, hb2 * 2) : 0, rl = yL != null && yR != null ? Mathf.Atan2(yL.Value - yR.Value, ht * 2) : 0;
                    car.pitch = Mathf.Lerp(car.pitch, Mathf.Clamp(pt, -.5f, .5f), 1 - Mathf.Exp(-dt * 10)); car.roll = Mathf.Lerp(car.roll, Mathf.Clamp(rl, -.4f, .4f), 1 - Mathf.Exp(-dt * 10));
                }
                else { car.vy -= 22 * dt; pos.y += car.vy * dt; car.air = true; }
                if (pos.y < -150) { pos.y = 50; car.v = 0; }
                // moto: grau + inclinação na curva
                if (moto)
                {
                    foreach (var e in FBMoto.StepWheelie(car.wh, wheelKey && !car.air, Mathf.Max(0, thr), thr < 0 ? 1 : 0, car.v / 1.2f, dt)) WheelieEvent(car, e);
                    car.lean = Mathf.Lerp(car.lean, -FBMoto.LeanTarget(st, car.v / 1.2f, car.wh.a > .15f), 1 - Mathf.Exp(-dt * 3.5f)); car.trick = car.wh.trick; car.wheelie = car.wh.a;
                    car.rider = new Rider { v = Mathf.Clamp01(Mathf.Abs(car.v) / Kd.vmax), lean = car.lean, sp = car.sp, a = car.wh.a };
                    if (car.driver && car.driver.GetComponent<FBDriverPose>()?.trans == null) MotoRiderClip(car, car.driver, Kd.vmax, dt);   // r43: poses de pilotar do 171 (não no subir/descer)
                    if (car.wh.a > .3f && Mathf.Abs(car.v) > 4) FBNpcRotina.Grau(car);                // r43: pedestre comenta o grau
                    float ax = (car.v - v0) / Mathf.Max(dt, 1e-3f); car.sp = Mathf.Lerp(car.sp, Mathf.Clamp(-ax * .0065f, -.1f, .1f), 1 - Mathf.Exp(-dt * 9));
                }
                float wa = moto ? car.wh.a : 0; car.shift = Vector3.zero;
                if (wa > 1e-4f) { var pr = new Vector3(0, 0, -car.wheelbase / 2); car.shift = CarRot(car.pitch, car.yaw, 0) * pr - CarRot(car.pitch - wa, car.yaw, 0) * pr; pos += car.shift; }
                car.transform.SetPositionAndRotation(pos, CarRot(car.pitch - wa + (moto ? car.sp : 0), car.yaw, car.roll + car.lean));
                if (phys) FBCarPhys.Body(car, axL, car.air ? 0 : car.v * car.yr, dt);
                if (moto) FBGrauHud.Show(car);
                // rodas girando e esterçando, guidão
                car.spin += car.v * dt; car.rspin += (car.burn ? 26 : car.launchT > 0 ? 9 * car.launchT : 0) * dt;
                PoseWheels(car, car.steer);
                if (Mathf.Abs(car.v) > 3.5f) RunOver(car, car.v, f);
                // ---- som
                var cpos = car.transform.position; float rel = Mathf.Abs(car.v) / Kd.vmax;
                bool engineOff = player && player.GetComponent<FBDriverPose>() is { trans: "dismount", carAnim: true };   // desceu: motor já desligado
                if (moto && Json.O(car.meta, "sound") != null) GbMotoAudio(car, car.v, alive && thr > 0, car.air, car.wh.a > .08f || (wheelKey && thr > 0 && car.v > 3), dt, car.eng.rpm, cpos, "veh_gb_");
                else if (moto) { var (n, rt) = EngineSample(car, Mathf.Clamp01(.18f + rel * .78f + Mathf.Max(0, thr) * .08f)); FBAudio.SetLoop("veh_engine", n, .42f, cpos, rt, 3, .75f); }
                else
                {
                    var prof = car.engineProf ??= FBCarSound.ProfileFor(car.id, car); float gas = alive ? Mathf.Max(0, thr) + (thr < 0 && car.v < -.5f ? -thr : 0) : 0;
                    car.gb ??= new FBCarSound.Gearbox(TopV(car)); var g = car.gb.Step(dt, car.v, gas);
                    if (g.shifted != 0) FBAudio.PlayAt("reb_gearshifting" + (1 + Random.Range(0, 5)), cpos, g.shifted > 0 ? .45f : .28f, 4);
                    if (car.gb.shiftT > 0) gas *= .2f;                                                // pé fora durante a troca: o ronco cai e volta
                    if (car.gas > .5f && gas < .2f) { FBCarSound.TurboBlow(prof, g.rpm, cpos, car.turbo); if (FBCarSound.Sporty(prof) && g.rpm > 4800 && Random.value < .6f) FBAudio.PlayAt("reb_exhaustfire" + new[] { 1, 2, 4, 6 }[Random.Range(0, 4)], cpos, .45f, 4); }
                    car.gas = gas; car.gear = g.gear; car.rpm = g.rpm / 7000;
                    if (!engineOff) FBCarSound.EngineAudio("veh_e_", prof, g.rpm, gas, cpos, turbo: car.turbo);
                    FBAudio.SetLoop("veh_wind", "reb_wind", Mathf.Clamp((Mathf.Abs(car.v) - 10) / 40, 0, .35f));
                    FBAudio.SetLoop("veh_rev", "reb_reverse", car.v < -.5f ? .22f : 0, cpos, .8f + Mathf.Min(1, -car.v / Kd.rev) * .5f);
                }
                // pneus: freada forte, derrapagem, burnout e arrancada
                // r45: "pedal" do freio só para som/marcas (a física continua com o thr): toque curto no S = freada normal (silenciosa, sem marca);
                // segurar ~0,3 s acima de ~33 km/h = freada BRUSCA (pneu no limite: guincho do 171 + atrito + marca); no toque é o próprio valor
                brkP = Mathf.MoveTowards(brkP, !car.air && thr < 0 && car.v > .5f ? -thr : 0, dt / (FBInput.Touch ? .12f : .3f));
                float av = Mathf.Abs(car.v), brakeSlip = !car.air && brkP > .7f && car.v > 8 ? Mathf.Clamp((car.v - 8) / 14, .25f, 1) * Mathf.InverseLerp(.7f, 1, brkP) : 0;
                float driftSlip = car.air || phys ? 0 : hb && av > 6 ? Mathf.Clamp(av / 20, .35f, 1) : Mathf.Abs(car.steer) > maxSt * .8f && av > 18 ? Mathf.Clamp((av - 18) / 10, .3f, .9f) : 0;
                float spinSlip = car.burn ? 1 : car.launchT > 0 ? .5f + car.launchT * .5f : 0; var vel = f * car.v + (phys ? r * car.vl : Vector3.zero);
                foreach (var w in car.wheels)
                {
                    bool rear = !w.front; float sl = Mathf.Max(brakeSlip * (rear ? .85f : 1), rear ? Mathf.Max(driftSlip, spinSlip) : driftSlip * .3f); if (moto) sl *= .6f;
                    if (phys) sl = Mathf.Max(sl, rear ? car.slipR : car.slipF);
                    if (w.obj) FBCarFX.Wheel(w, w.obj.position, w.r, sl, vel, r, dt, car.burn && rear, moto && !car.burn && brakeSlip <= 0);   /* r45d: moto não solta nuvem na arrancada (parecia escapamento) */
                }
                float pSlip = phys ? Mathf.Max(car.slipF, car.slipR) : 0;
                if (!moto) TireAudio(car, cpos, brakeSlip, Mathf.Max(driftSlip, pSlip), spinSlip, dt); else FBAudio.SetLoop("veh_skid", "veh_skid", 0, cpos);
                FBAudio.SetLoop("veh_horn", FBCarSound.HornFor(car), horn && alive ? .5f : 0);
               
            }
            finally { car.SetColliders(true); }
            if (player) { player.velocity = Fwd(car.yaw) * car.v + Left(car.yaw) * car.vl; player.grounded = true; }
        }
        // PNEUS (r45, sons do 171): freada normal = sem som de pneu (o carro de verdade quase não faz barulho); freada brusca no asfalto/concreto/
        // metal/madeira = guincho FreioAsfalto1–4 no começo + atrito AtritoPneuAsfaltoCurva enquanto o pneu desliza (volume e tom pela
        // derrapagem e pela velocidade) + um guincho curto na parada se veio derrapando; terra/grama/areia/lama = atrito AtritoPneuTerra (sem
        // guincho). Curva no limite, freio de mão, arrancada e burnout usam o mesmo atrito, pela quantidade de deslize.
        static void TireAudio(FBCar car, Vector3 cpos, float brake, float drift, float spin, float dt)
        {
            if ((surfT -= dt) <= 0) { surfT = .2f; surf = FBWorld.SurfaceBelow(car.transform.position + Vector3.up * .3f); }
            bool soft = surf == 1 || surf == 2 || surf == 3, air = car.air; float av = Mathf.Abs(car.v);
            float slide = air ? 0 : Mathf.Max(brake, drift, car.burn ? 1 : spin * .8f);
            skidK = Mathf.MoveTowards(skidK, slide, dt * (slide > skidK ? 8 : 4));
            float pitch = Mathf.Lerp(.86f, 1.08f, Mathf.Clamp01(av / 30)) * (car.burn ? .95f : 1);
            float vHard = !soft && skidK > .05f ? Mathf.Lerp(.12f, .55f, skidK) * Mathf.Clamp01(.35f + av / 25) : 0, vSoft = soft && skidK > .05f ? Mathf.Lerp(.15f, .6f, skidK) * Mathf.Clamp01(.3f + av / 18) : 0;
            FBAudio.SetLoop("veh_skid", "veh_skid", vHard, cpos, pitch, 7);
            FBAudio.SetLoop("veh_skid_dirt", "veh_skid_dirt", vSoft, cpos, pitch * .95f, 7);
            bool hard = brake > .2f && !soft && av > 11;
            if (hard && !hardOn && Time.time - squealT > 1.2f) { squealT = Time.time; FBAudio.PlayAt("veh_brake_squeal", cpos, Mathf.Clamp(av / 28, .35f, .8f), 7, .05f); }
            if (!hard && hardOn && !soft && av < 3.5f && brkP > .5f && Time.time - squealT > .5f) { squealT = Time.time; FBAudio.PlayAt("veh_brake_squeal", cpos, .3f, 6, .1f); }   // parou derrapando
            if (hard) hardOn = true; else if (av < 3.5f || brkP < .3f) hardOn = false;
        }
        // deslize lateral (física nova): caixa do comprimento do carro varrendo de lado; muro/carro de lado segura e amassa
        static Vector3 SlideSide(FBCar car, Vector3 pos, Vector3 f, Vector3 r, float dt)
        {
            var L = car.lbox; float s = car.scale, sg = Mathf.Sign(car.vl), ls = Mathf.Abs(car.vl) * dt, cx = -L.center.x * s; var dir = r * sg; RaycastHit? hit = null;
            var half = new Vector3(.1f, .38f, L.extents.z * s * .88f); var o = pos + f * (L.center.z * s) + r * (cx + sg * (L.extents.x * s - .3f)) + Vector3.up * .9f;
            foreach (var h in Physics.BoxCastAll(o, half, dir, Quaternion.LookRotation(f), ls + .32f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore))
            {
                if (h.distance <= 0 && h.point == Vector3.zero) continue;
                if (Mathf.Abs(car.vl) > 2 && FBDestructible.TryHit(h.collider, 9999, h.point, dir)) continue;
                var pole = FBStreetPoleFx.PoleOf(h.collider); if (pole && (pole.down || pole.falling)) continue;
                if (hit == null || h.distance < hit.Value.distance) hit = h;
            }
            if (hit == null) return pos + dir * ls;
            float imp = Mathf.Abs(car.vl); var hn = hit.Value.normal; hn.y = 0; hn = hn.sqrMagnitude > 1e-4f ? hn.normalized : -dir;
            if (imp > 3 && car.crashT <= 0)
            {
                FBAudio.PlayAt("reb_carimpact" + (2 + Mathf.Min(3, Mathf.FloorToInt(imp / 6))), hit.Value.point, Mathf.Min(1, .35f + imp / 20), 6); car.crashT = .4f; Shake(Mathf.Min(1, imp / 18)); FBFx.Sparks(hit.Value.point, hit.Value.normal);
                FBCarDamage.OnImpact(car, hit.Value.point, hn, imp); var other = hit.Value.collider.GetComponentInParent<FBCar>();
                if (other && other != car) { FBCarDamage.OnImpact(other, hit.Value.point, -hn, imp * .85f); FBCarDamage.Shove(other, dir * imp * .35f, Random.Range(-1f, 1f) * imp * 4); }
            }
            car.vl = -car.vl * .15f; car.yr *= .5f; return pos + hn * .01f;
        }
        public static void Simulate(FBCar car, FBCharacter p, float thr, float st, bool space, float dt) => Step(car, p, thr, st, space, space, false, dt);   // testes automatizados
        static float JSign(float x) => x > 0 ? 1 : x < 0 ? -1 : 0;
        static (string, float) EngineSample(FBCar u, float rpm)
        {
            float b = u.heavy ? .82f : 1;
            if (u.kind == "moto") return ("ie_moto_idle", b * (.75f + rpm * 1.1f));
            if (u.ie) { if (rpm < .24f) return ("veh_eng_idle", b * (.9f + rpm * .3f)); return (rpm < .72f ? "ie_eng_low" : "ie_eng_high", b * (.78f + rpm * .5f)); }
            return (rpm < .24f ? "veh_eng_idle" : rpm < .48f ? "veh_eng_low" : rpm < .74f ? "veh_eng_med" : "veh_eng_high", b * (.78f + rpm * .5f));
        }
        // motos do Grau: parado em loop; acelerando em loop (tom sobe); ao soltar toca "reduzindo"; no grau, "corte de giro" por cima
        static readonly Dictionary<string, (bool acc, float accT, float rv)> gbState = new();
        static void GbMotoAudio(FBCar u, float v, bool acc, bool air, bool cut, float dt, float? rpm, Vector3 pos, string keys)
        {
            var S = Json.O(u.meta, "sound"); string C(string k) => Json.S(Json.O(S, k), "clip"); float rel = Mathf.Clamp01(Mathf.Abs(v) / KIND["moto"].vmax); bool a = acc && !air;
            gbState.TryGetValue(keys, out var st);
            if (st.acc && !a && st.accT > .5f && Mathf.Abs(v) > 2 && !cut && C("reduzindo") != null) FBAudio.PlayAt(C("reduzindo"), pos, .62f, 4);
            st.accT = a ? st.accT + dt : 0; st.acc = a; gbState[keys] = st;
            if (C("parado") != null) FBAudio.SetLoop(keys + "par", C("parado"), a || cut ? 0 : .34f, pos, 1, 3.5f, .75f);
            if (C("acelerando") != null) FBAudio.SetLoop(keys + "ace", C("acelerando"), a && !cut ? .56f : 0, pos, rpm != null ? .84f + rpm.Value * .5f : .88f + rel * .42f, 3.5f, .75f);
            if (C("corte") != null) FBAudio.SetLoop(keys + "cut", C("corte"), cut ? .62f : 0, pos, .92f + rel * .2f, 3.5f, .75f);
        }
        static void WheelieEvent(FBCar car, FBMoto.Ev e)
        {
            var p = car.transform.position; string Fmt(float d) => d.ToString("0.0").Replace('.', ',') + " m";
            if (e.kind == "start") FBAudio.PlayAt("ie_moto_start", p, .5f, 4);
            else if (e.kind == "land") { FBAudio.PlayAt("reb_bump", p, Mathf.Clamp(.25f + e.power * .12f, .25f, .7f), 5); Shake(Mathf.Clamp(e.power * .08f, .1f, .5f)); }
            else if (e.kind == "loop") { FBNpcRotina.Event("caimoto", p, 28, 1); FBAudio.PlayAt("reb_carimpact3", p, .7f, 5); Shake(.8f); car.v *= .2f; car.grauMsg = ("PERDEU O EQUILÍBRIO", e.dist > 4 ? "GRAU ANULADO · " + Fmt(e.dist) : "", true, Time.time + 2.2f); }
            else if (e.kind == "end") car.grauMsg = ("GRAU " + Fmt(e.dist), (e.dist >= car.wh.best - 1e-6f ? "NOVO RECORDE · " : "RECORDE " + Fmt(car.wh.best) + " · ") + e.time.ToString("0.0").Replace('.', ',') + " s", false, Time.time + 3);
        }
        // oficina (REB TestarRoncoCarro): segurar = acelera em ponto morto até o corte; soltar = blow-off
        static void HoldStep(FBCar car, FBCharacter p, float dt)
        {
            car.v = 0; car.steer *= .9f; var prof = car.engineProf ??= FBCarSound.ProfileFor(car.id, car); float gas = rev ? 1 : 0;
            car.revRpm = Mathf.Min(7000, Mathf.Max(900, (car.revRpm == 0 ? 900 : car.revRpm) + (gas > 0 ? 5200 : -3800) * dt * (gas > 0 && car.revRpm > 6600 ? .2f : 1)));
            if (gas > 0 && car.revRpm > 6750) car.revRpm -= 380 * Random.value;
            if (car.gas > .5f && gas == 0) FBCarSound.TurboBlow(prof, car.revRpm, car.transform.position);
            car.gas = gas; car.rpm = car.revRpm / 7000; if (car.kind != "moto") FBCarSound.EngineAudio("veh_e_", prof, car.revRpm, gas, car.transform.position, .5f);
            foreach (var w in car.wheels) if (w.obj) w.obj.localRotation = w.bas;
        }
        public static void PoseWheels(FBCar u, float steer)
        {
            foreach (var w in u.wheels)
            {
                if (!w.obj) continue; float spinDeg = (u.spin + (w.front ? 0 : u.rspin)) / (w.r <= 0 ? .4f : w.r) * w.sgn * Mathf.Rad2Deg, stDeg = w.front && !w.noSteer ? steer * (u.kind == "moto" ? .8f : 1) * Mathf.Rad2Deg : 0;
                w.obj.localRotation = Quaternion.AngleAxis(-stDeg, Vector3.up) * w.bas * Quaternion.AngleAxis(spinDeg, Vector3.right);
            }
            foreach (var (o, b) in u.steerNodes) if (o) o.localRotation = Quaternion.AngleAxis(-steer * .8f * Mathf.Rad2Deg, u.steerAxis ?? Vector3.up) * b;
            u.steerAng = steer * .8f;
            if (u.volante) TurnVolante(u, u.steerN);
        }
        // atropelamento: quem estiver na frente do para-choque leva dano e é jogado
        static readonly Dictionary<FBCharacter, float> runT = new();
        static void RunOver(FBCar car, float v, Vector3 f)
        {
            var L = car.lbox;
            foreach (var a in FBMatch.All.ToList())
            {
                if (!a || !a.alive || a == car.driver || a.isRemote || a.vehiclePose || (runT.TryGetValue(a, out var t0) && Time.time - t0 < .7f)) continue;
                var lp = car.transform.InverseTransformPoint(a.Pos + Vector3.up);
                if (lp.x < L.min.x - .35f || lp.x > L.max.x + .35f || lp.z < L.min.z - .35f || lp.z > L.max.z + .35f || lp.y < L.min.y - 1 || lp.y > L.max.y + 1) continue;
                runT[a] = Time.time;
                // r44: atropelamento com ragdoll (impulso do carro), sons do 171 e levantar (FBAtropelo); no DE ROLÊ sem dano
                if (car.driver && a != car.driver && !FBMatch.FFA && a.team == car.driver.team && !a.civ && FBSession.mode != "role") { a.velocity += f * v * .25f; continue; }   // aliado: empurra, sem dano
                // [parceiro] fisica 31/10: de re (v<0) a vitima e lancada para tras do carro, nao para o capo contra a carroceria
                FBAtropelo.Hit(a, car, v, v < 0 ? -f : f, FBSession.mode != "role" || a.isPlayer); car.v *= car.mx ? .9f : .82f;
            }
        }
        // ---- VOLANTE: eixo real = normal do disco do aro (menor variância dos vértices, no espaço do pai), virado para o motorista
        static Vector3 Eig3Min(double[] C)
        {
            var a = new double[3, 3] { { C[0], C[1], C[2] }, { C[1], C[3], C[4] }, { C[2], C[4], C[5] } }; var V = new double[3, 3] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
            for (int it = 0; it < 30; it++)
            {
                int p = 0, q = 1; if (System.Math.Abs(a[0, 2]) > System.Math.Abs(a[p, q])) { p = 0; q = 2; } if (System.Math.Abs(a[1, 2]) > System.Math.Abs(a[p, q])) { p = 1; q = 2; }
                if (System.Math.Abs(a[p, q]) < 1e-12) break;
                double th = .5 * System.Math.Atan2(2 * a[p, q], a[q, q] - a[p, p]), c = System.Math.Cos(th), sn = System.Math.Sin(th);
                for (int k = 0; k < 3; k++) { double akp = a[k, p], akq = a[k, q]; a[k, p] = c * akp - sn * akq; a[k, q] = sn * akp + c * akq; }
                for (int k = 0; k < 3; k++) { double apk = a[p, k], aqk = a[q, k]; a[p, k] = c * apk - sn * aqk; a[q, k] = sn * apk + c * aqk; }
                for (int k = 0; k < 3; k++) { double vkp = V[k, p], vkq = V[k, q]; V[k, p] = c * vkp - sn * vkq; V[k, q] = sn * vkp + c * vkq; }
            }
            int i = new[] { 0, 1, 2 }.OrderBy(x => a[x, x]).First(); return new Vector3((float)V[0, i], (float)V[1, i], (float)V[2, i]).normalized;
        }
        static VolSetup Vol(FBCar u)
        {
            if (u.volTried) return u.vol; u.volTried = true; var vo = u.volante; if (!vo) return null;
            var par = vo.parent; var saved = vo.localRotation; vo.localRotation = u.volBase; var inv = par.worldToLocalMatrix; var pts = new List<Vector3>();
            foreach (var mf in vo.GetComponentsInChildren<MeshFilter>()) { var m = mf.sharedMesh; if (!m || !m.isReadable) continue; var M = inv * mf.transform.localToWorldMatrix; var vs = m.vertices; int stp = Mathf.Max(1, vs.Length / 1500); for (int k = 0; k < vs.Length; k += stp) pts.Add(M.MultiplyPoint3x4(vs[k])); }
            vo.localRotation = saved; if (pts.Count < 8) return null;
            var c = Vector3.zero; foreach (var q in pts) c += q; c /= pts.Count; var C = new double[6];
            foreach (var q in pts) { double x = q.x - c.x, y = q.y - c.y, z = q.z - c.z; C[0] += x * x; C[1] += x * y; C[2] += x * z; C[3] += y * y; C[4] += y * z; C[5] += z * z; }
            var axis = Eig3Min(C); var seatP = par.InverseTransformPoint(u.L2W(u.seat)); if (Vector3.Dot(axis, seatP - c) < 0) axis = -axis;
            return u.vol = new VolSetup { axis = axis, pivot = c, baseP = vo.localPosition, baseQ = u.volBase };
        }
        static void TurnVolante(FBCar u, float steerN)
        {
            var vo = u.volante; var S = Vol(u);
            if (S == null) { vo.localRotation = u.volBase * Quaternion.AngleAxis(steerN * 1.25f * Mathf.Rad2Deg, Vector3.forward); return; }
            var q = Quaternion.AngleAxis(-steerN * 1.25f * Mathf.Rad2Deg, S.axis); vo.localRotation = q * S.baseQ; vo.localPosition = q * (S.baseP - S.pivot) + S.pivot;
        }
        // ---- pose do motorista (depois da animação): quadril no banco, cabeça no teto, IK das mãos/pés.
        // w = peso do IK (o FBDriverPose sobe em 0,2 s ao entrar). Devolve os alvos (teste FBVehicleRigTests / diagnóstico).
        public struct PoseTargets { public bool hands, feet; public Vector3 hL, hR, fL, fR; }
        // r46g: o alinhamento anatômico do FBIK (torção limitada) vale só dentro do PoseActor/PoseRider/garupa — fora dos veículos (mão na arma,
        // pé na escada, eletricista, baculejo) o IK volta a ser o de antes da rodada 46 (sonda r46g: 453 torções > 90° do braço esquerdo na arma)
        public static PoseTargets PoseActor(FBCharacter a, FBCar u, float w = 1, FBDriverPose dp = null)
        { bool was = FBIK.Anat; FBIK.Anat = true; try { return PoseActorCore(a, u, w, dp); } finally { FBIK.Anat = was; if (!was) FBIK.Where = "-"; } }
        static PoseTargets PoseActorCore(FBCharacter a, FBCar u, float w, FBDriverPose dp)
        {
            var T = new PoseTargets();
            var an = a.an; if (!an || !an.isHuman) return T; var hips = an.GetBoneTransform(HumanBodyBones.Hips); if (!hips) return T;
            string trn = dp ? dp.trans : null; float k = dp && dp.transLen > 0 ? Mathf.Clamp01(dp.transT / dp.transLen) : 1, tw = 1, iw = 1;
            FBIK.Where = (trn ?? "sentado") + (u.mx ? "/mx" : u.kind == "moto" ? "/moto" : "/carro");
            if (trn == "mount") { tw = Mathf.SmoothStep(0, 1, (k - .35f) / .65f); iw = Mathf.SmoothStep(0, 1, (k - .6f) / .4f); }        // o quadril vai ao banco na 2.ª metade
            else if (trn == "dismount") { tw = 1 - Mathf.SmoothStep(0, 1, (k - .12f) / .58f); iw = 1 - Mathf.SmoothStep(0, 1, k / .3f); }   // larga o guidão, sai do banco
            // r47: o blend começava com o IK em 0,6 — o subir/entrar termina com ele em 1 (SmoothStep até k = 1), então as mãos saíam do volante/guidão
            // num quadro e voltavam (sonda r46: giro de mão/antebraço 2.400–3.200°/s em t≈2,33, o 1º quadro do blend, em todos os carros). Fica em 1.
            else if (trn == "blend") iw = 1;
            w *= iw;
            var rot = trn == "mount" ? u.transform.rotation * Quaternion.Euler(0, 90, 0) : u.transform.rotation;      // no clipe de subir o corpo gira −90° até a frente
            bool trans = trn == "mount" || trn == "dismount", warp = false; float wh = 0, wv = 0, lat = 0, bump = 0;
            if (trans && dp.path) warp = FBAnimPaths.Eval(dp.pathClip, k, out wh, out wv, out lat, out bump);
            if (warp && trn == "mount") rot = Quaternion.Slerp(u.transform.rotation * Quaternion.Euler(0, dp.yaw0, 0), rot, Mathf.SmoothStep(0, 1, k / dp.pathK));   // vira para a porta sem estalo
            a.transform.rotation = rot;
            var seatW = SeatHips(u);
            if (warp)
            {   // r45d: quadril pela reta porta<->banco do veículo real, no ritmo do clipe (FBAnimPaths)
                var seatP = seatW - u.transform.up * (a.fitCar == u ? a.seatDrop : 0); var T0 = u.transform; Vector3 S, E;
                if (trn == "mount") { S = Vector3.Lerp(T0.TransformPoint(dp.pathS0), T0.TransformPoint(dp.pathS), Mathf.SmoothStep(0, 1, k / dp.pathK)); E = seatP; }
                else { S = seatP; E = T0.TransformPoint(dp.pathE); }
                var d = E - S; var dh = d.Flat(); var ld = dh.sqrMagnitude > 1e-6f ? Vector3.Cross(Vector3.up, dh.normalized) : Vector3.zero; float sc = an.humanScale * a.transform.lossyScale.y;
                a.transform.position += S + dh * wh + Vector3.up * (d.y * wv + bump * sc) + ld * lat * sc - hips.position;
                if (a.isPlayer) { WarpHips = hips.position; WarpFrame = Time.frameCount; WarpK = k; }      // r50: diagnóstico do teste -fbjogo (quadril logo depois do caminho)
            }
            else if (trans) { a.transform.position = u.transform.TransformPoint(dp.baseL); a.transform.position += (seatW - hips.position) * tw; }
            else a.transform.position += seatW - hips.position;
            if (trn == "blend")
            {   // emenda do fim de subir (corpo girado no clipe) com o pilotar: gira a raiz em volta do quadril até o corpo olhar para a frente da moto
                var lU = an.GetBoneTransform(HumanBodyBones.LeftUpperLeg); var rU = an.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                // r46c: a correção SOME junto com o blend (sonda r46c: no quadro em que o blend acabava ela era cortada inteira — raiz girando de uma vez,
                // canela/antebraço presos no IK a 9.000°/s). Começa inteira (emenda com o fim do subir) e chega a zero no fim (emenda com o pilotar)
                // r46d: e PARTE de onde o corpo estava no último quadro de subir (dp.blendYaw0, frente das coxas no referencial do veículo) — o fim do
                // clipe de entrar não deixa o corpo exatamente de frente; alinhar à frente no 1º quadro girava a raiz até 120° de uma vez
                if (lU && rU) { var bf = Vector3.Cross(rU.position - lU.position, Vector3.up).Flat(); if (bf.sqrMagnitude > 1e-6f) { var cf = u.transform.forward.Flat(); float cyaw = Vector3.SignedAngle(cf, bf, Vector3.up), from = dp && dp.blendYaw0 is float y0 ? y0 : 0;
                    float ang = Mathf.DeltaAngle(cyaw, from) * (1 - Mathf.SmoothStep(0, 1, (k - .3f) / .7f)); a.transform.RotateAround(hips.position, Vector3.up, ang); } }
                a.transform.position += seatW - hips.position;
            }
            if (trans) FeetAboveGround(a, an, dp); else if (dp) dp.feetLift = 0;
            var up = u.transform.up;
            // r49: cabeça no teto MEDIDA COM O CORPO NO BANCO. Antes media uma vez, no 1º quadro de entrar: o personagem ainda estava do lado de
            // fora, o raio não achava o teto e seatDrop ficava 0 em todos os carros (laboratório r49a: 0,00 nos 10 casos) — carro rebaixado na
            // oficina ou personagem alto = cabeça atravessando o teto. Agora mede sentado (a cada 0,25 s) e o banco desce/sobe suave
            if (a.fitCar != u) { a.fitCar = u; a.seatDrop = a.seatDropT = 0; a.fitT = 0; }
            if (u.kind != "moto" && !u.mx && (trn == null || trn == "blend")) FitRoof(a, an, u, up);
            if (a.seatDrop > 0 && !warp) a.transform.position -= up * a.seatDrop;
            if (trans && u.kind != "moto" && !u.mx) HeadDuck(a, an, u, up, dp); else if (dp) dp.duck = 0;
            if (u.mx && trn == null)
            {   // r44: MX Grau — estado do MXPlayer (clipe humanoide MX_*) + IK nos pontos do RigData
                var want = u.mx.RiderClip(Time.deltaTime); if (!a.isPlayer) want = RiderStyle(a, u, want); if (a.sys != want && FBRegistry.I && FBRegistry.I.Clip(want)) { if (FBMxBike.SmoothRider) a.sysFade = .45f; a.sys = want; }   // r49i: mistura mais longa entre as poses de pilotar
                return u.mx.PoseRider(a, w);
            }
            if (u.kind == "moto" && u.rider != null) RiderBody(a, u, u.rider, w);
            Transform B(HumanBodyBones b) => an.GetBoneTransform(b);
            Transform lA = B(HumanBodyBones.LeftUpperArm), lF = B(HumanBodyBones.LeftLowerArm), lH = B(HumanBodyBones.LeftHand), rA = B(HumanBodyBones.RightUpperArm), rF = B(HumanBodyBones.RightLowerArm), rH = B(HumanBodyBones.RightHand);
            var back = -u.transform.forward;
            if (u.mx && lA && rA)
            {   // r46d: MX nas transições = os mesmos alvos e dicas do pilotar (FBMxBike.PoseRider)
                Vector3 gL = u.mx.HandGoal(0, an, lH, out var qL, out var okL), gR = u.mx.HandGoal(1, an, rH, out var qR, out var okR);   // r49m: palma no punho + rotação do RigData
                Reach(a, u, dp, lA, lF, lH, rA, rF, rH, gL, gR, w);
                FBMxBike.ChestClear(a, u.mx.transform, gL, gR, w * iw);                                   // r49: o mesmo do pilotar, entrando com o peso do IK
                FBIK.TwoBonePole(lA, lF, lH, gL, TPole(lA, lF, lH, u.mx.ElbowHint(0), trans, iw), w); FBIK.TwoBonePole(rA, rF, rH, gR, TPole(rA, rF, rH, u.mx.ElbowHint(1), trans, iw), w);
                FBMxBike.SetHand(an, 0, lF, lH, qL, okL, w); FBMxBike.SetHand(an, 1, rF, rH, qR, okR, w);
                T.hands = true; T.hL = gL; T.hR = gR;
            }
            else if (u.rigHands == null && u.grip is Vector3 grip && lA && rA)                 // guidão (moto) / ponto de esterço (IE); o rig do REB tem prioridade
            {
                float side = u.gripSide > 0 ? u.gripSide : (u.kind == "moto" ? .34f : .19f), tr = u.kind == "moto" ? u.trick : 0;
                // web: +X = esquerda; Unity: -X
                var gL = u.L2W(grip + new Vector3(-(side + tr * .1f), tr * .5f, -tr * .22f)); var gR = u.L2W(grip + new Vector3(side + tr * .1f, tr * .5f, -tr * .22f));
                if (u.kind == "moto" && u.steerNodes.Count > 0 && u.steerAng != 0)
                {
                    var piv = u.steerNodes[0].obj.position; var ax = u.transform.TransformDirection(u.steerAxis ?? Vector3.up); var q = Quaternion.AngleAxis(-u.steerAng * (1 - tr) * Mathf.Rad2Deg, ax);
                    gL = piv + q * (gL - piv); gR = piv + q * (gR - piv);
                }
                Reach(a, u, dp, lA, lF, lH, rA, rF, rH, gL, gR, w);
                // r46c: polo do cotovelo no guidão = para FORA e para baixo (antes: cotovelo + 40 cm para trás, quase na reta ombro→mão, que aponta para
                // a frente — plano da dobra indefinido, o cotovelo virava para o lado que o clipe deixasse: 112 quadros "ao contrário" na Pop parada)
                var sideW = u.transform.right; var pL = lF.position + (-sideW * .35f - up * .22f + back * .12f) * a.ScaleK; var pR = rF.position + (sideW * .35f - up * .22f + back * .12f) * a.ScaleK;
                FBIK.TwoBonePole(lA, lF, lH, gL, TPole(lA, lF, lH, pL, trans, iw), w); FBIK.TwoBonePole(rA, rF, rH, gR, TPole(rA, rF, rH, pR, trans, iw), w);
                T.hands = true; T.hL = gL; T.hR = gR;
            }
            if (u.rigHands != null && lA && rA)
            {
                float sn = u.steerN; var VS = Vol(u); Vector3 hub, axis;
                if (VS != null) { var par = u.volante.parent; hub = par.TransformPoint(VS.pivot); axis = -par.TransformDirection(VS.axis).normalized; }
                else { hub = u.L2W(u.rigWheel ?? Vector3.Lerp(u.rigHands[0], u.rigHands[1], .5f)); axis = (hub - (u.L2W(u.seat) + up * .45f)).normalized; }
                Vector3 Hand(Vector3 p) => hub + Quaternion.AngleAxis(sn * 1.25f * Mathf.Rad2Deg, axis) * (u.L2W(p) - hub);
                Vector3 Pole(int i) => u.rigPoles != null ? u.L2W(u.rigPoles[i]) : (i == 1 ? rF : lF).position - Vector3.up * .3f + u.transform.right * (i == 1 ? .25f : -.25f);
                T.hands = true; T.hL = Hand(u.rigHands[0]); T.hR = Hand(u.rigHands[1]);
                Reach(a, u, dp, lA, lF, lH, rA, rF, rH, T.hL, T.hR, w);
                FBIK.TwoBonePole(lA, lF, lH, T.hL, TPole(lA, lF, lH, Pole(0), trans, iw), w); FBIK.TwoBonePole(rA, rF, rH, T.hR, TPole(rA, rF, rH, Pole(1), trans, iw), w);
            }
            var legs = new[] { (B(HumanBodyBones.LeftUpperLeg), B(HumanBodyBones.LeftLowerLeg), B(HumanBodyBones.LeftFoot)), (B(HumanBodyBones.RightUpperLeg), B(HumanBodyBones.RightLowerLeg), B(HumanBodyBones.RightFoot)) };
            // pés: pedais (rig do REB) ou pedaleiras (moto/IE) — um só dos dois; a rotação do pé fica a do clipe sentado
            for (int i = 0; i < 2; i++)
            {
                var (lg, lo, ft) = legs[i]; if (!ft) continue; Vector3 tg, pole;
                if (u.mx) { tg = u.mx.FootTarget(i); pole = u.mx.KneePole(i, lg, lo, ft, tg); }
                else if (u.rigFeet != null) { tg = u.L2W(u.rigFeet[i]); pole = lo.position + u.transform.forward * .6f + up * .35f; }
                else if (u.feet is Vector3 fe) { tg = u.L2W(fe + new Vector3(i == 0 ? -u.footSide : u.footSide, 0, 0)); pole = lo.position + u.transform.forward * .5f + up * .2f; }
                else continue;
                var fr = ft.rotation; FBIK.TwoBonePole(lg, lo, ft, tg, TPole(lg, lo, ft, pole, trans, iw), w); ft.rotation = fr;
                T.feet = true; if (i == 0) T.fL = tg; else T.fR = tg;
            }
            if (trans || trn == "blend") FootGround(a, an, legs);
            if (trans && (u.mx || u.kind == "moto")) FBLegClear.Apply(a, u, 1 - iw);          // r48: perna que passa por cima da moto não atravessa banco/rabeta
            return T;
        }
        // r46: pé/dedo abaixo do chão (sonda r46a: até 11 cm na entrada/saída — o quadril segue a reta porta↔banco com a altura de quadril
        // estimada e o clipe do 171 dobra a perna de apoio no carro dele): sobe o corpo o que faltar. Só o chão do MAPA conta (dentro do carro
        // o raio acha a rua embaixo do assoalho e o pé fica acima dela).
        public static int FeetLifts; public static float FeetLiftMax;
        // r46d: a subida é SUAVE (sobe rápido, desce devagar, estado no FBDriverPose) — aplicada inteira num quadro ela era o próprio "salto do
        // quadril" (sonda r46d: até 0,40 m de uma vez ao passar a perna na moto; salto do quadril subindo na moto 13 -> 36 depois do r46b)
        static void FeetAboveGround(FBCharacter a, Animator an, FBDriverPose dp)
        {
            // r46f: o corpo só sobe o que os DOIS pés pedem (o menor); o resto é por perna no fim do PoseActor (FootGround, IK da perna, quadril parado).
            // Com o máximo dos dois, a perna que passa por cima do banco/vai ao chão levantava o corpo inteiro (sonda r46f: suavizar a subida deixava
            // a ponta do pé 13 cm no chão; subir de uma vez era o salto do quadril)
            float k = a.ScaleK, need = Mathf.Min(FootNeed(an, 0, k), FootNeed(an, 1, k));
            need = Mathf.Min(need, .3f * k); if (need > .002f) { FeetLifts++; FeetLiftMax = Mathf.Max(FeetLiftMax, need); }
            float dt = Time.deltaTime, lift = dp ? dp.feetLift : need;
            lift = need > lift ? Mathf.Lerp(lift, need, 1 - Mathf.Exp(-dt * 12)) : Mathf.MoveTowards(lift, need, dt * .6f);
            if (dp) dp.feetLift = lift; if (lift > .001f) a.transform.position += Vector3.up * lift;
        }
        // quanto falta para o pé (tornozelo/ponta) do lado i ficar no chão do MAPA
        static float FootNeed(Animator an, int i, float k)
        {
            float need = 0; int mask = 1 << FBWorld.LMap;
            foreach (var (bb, sole) in i == 0 ? new[] { (HumanBodyBones.LeftFoot, .07f), (HumanBodyBones.LeftToes, .018f) } : new[] { (HumanBodyBones.RightFoot, .07f), (HumanBodyBones.RightToes, .018f) })
            {
                var b = an.GetBoneTransform(bb); if (!b) continue;
                if (Physics.Raycast(b.position + Vector3.up * .6f * k, Vector3.down, out var h, 1.2f * k, mask, QueryTriggerInteraction.Ignore)) need = Mathf.Max(need, h.point.y + sole * k - b.position.y);
            }
            return need;
        }
        // r46f: pé que ainda ficou no chão depois dos alvos (pedaleira/chão da MX, clipe de entrar): IK só daquela perna, rotação do pé mantida
        public static int FootFixes; public static float FootFixMax;
        static void FootGround(FBCharacter a, Animator an, (Transform lg, Transform lo, Transform ft)[] legs)
        {
            float k = a.ScaleK;
            for (int i = 0; i < 2; i++)
            {
                var (lg, lo, ft) = legs[i]; if (!lg || !lo || !ft) continue;
                float need = Mathf.Min(FootNeed(an, i, k), .35f * k); if (need < .003f * k) continue;
                var fr = ft.rotation; FBIK.TwoBonePole(lg, lo, ft, ft.position + Vector3.up * need, FBIK.NaturalPole(lg, lo, ft, Mathf.Max(.2f, (lo.position - lg.position).magnitude)), 1); ft.rotation = fr;
                FootFixes++; FootFixMax = Mathf.Max(FootFixMax, need);
            }
        }
        // r46: polo nas transições — começa na dobra que o clipe já tem (FBIK.NaturalPole) e chega ao polo do banco/guidão junto com o peso do
        // IK; antes o polo do banco valia desde o 1º quadro (cotovelo puxado para dentro do carro com a pessoa ainda do lado de fora)
        static Vector3 TPole(Transform u, Transform m, Transform e, Vector3 rigPole, bool trans, float iw)
        {
            if (!trans || !u || !m || !e) return rigPole; float L = (m.position - u.position).magnitude;
            return Vector3.Lerp(FBIK.NaturalPole(u, m, e, Mathf.Max(.2f, L)), rigPole, Mathf.SmoothStep(0, 1, iw));
        }
        public static int RoofFits; public static float RoofDropMax;                                 // laboratório
        static void FitRoof(FBCharacter a, Animator an, FBCar u, Vector3 up)
        {
            var head = an.GetBoneTransform(HumanBodyBones.Head); if (!head) return;
            if ((a.fitT -= Time.deltaTime) <= 0)
            {
                a.fitT = .25f; float k = a.transform.lossyScale.y / FBConst.ModelScale, best = 9, target = 0;
                // o corpo está no banco SEM a descida deste quadro (ela vem depois): a medida é absoluta
                foreach (var h in Physics.RaycastAll(head.position + up * 1.4f, -up, 1.4f + .45f * k, 1 << FBWorld.LVehicle, QueryTriggerInteraction.Ignore))
                    if (h.collider.GetComponentInParent<FBCar>() == u && h.distance < best) best = h.distance;
                if (best < 9) { float roof = 1.4f - best - .035f, clear = roof - .22f * k * 1.25f; target = Mathf.Clamp(.04f - clear, 0, .35f); }
                if (target > a.seatDropT + .005f) { RoofFits++; RoofDropMax = Mathf.Max(RoofDropMax, target); }
                a.seatDropT = target;
            }
            a.seatDrop = Mathf.MoveTowards(a.seatDrop, a.seatDropT, Time.deltaTime * .6f);
        }
        // r49: entrar/sair de carro BAIXO (rebaixado na oficina, Saveiro/Gol) ou com personagem alto: o clipe do 171 foi gravado num carro mais
        // alto e a cabeça passava pelo teto/coluna da porta (fotos do usuário, 30/10). Com o topo da cabeça debaixo da carroceria (o raio de cima
        // acha o casco do carro) e acima da face de dentro do teto, curva coluna, peito e pescoço para DENTRO do carro só o que falta — como quem
        // abaixa a cabeça para entrar. Sobe rápido, volta devagar (sem tranco).
        // r49i (prints do usuário, 31/10: PM Almeida de boné entrando na SaveiroCross rebaixada — "toda a animação está quebrada"; o boné e a
        // cabeça passavam pela BORDA do teto). Causa (laboratório r49h, mesma dupla): o raio de cima só achava o teto com a cabeça já DEBAIXO
        // dele e exigia face quase horizontal — na porta a cabeça atravessa a quina/calha do teto (face inclinada ou nenhuma) e o abaixar não
        // ligava (máx 4–7 cm, e só depois de a cabeça entrar); o topo era cabeça + 0,20 (o FitRoof usa 0,275: boné/cabelo). Agora o perfil do
        // teto é medido uma vez por entrada no referencial do banco (altura do teto e até onde ele vai para o lado da porta, raios de cima a
        // cada 4 cm) e o limite do topo da cabeça é a face de dentro do teto debaixo dele e uma rampa (1,6 m/m) fora da borda: a cabeça já
        // vem abaixando ANTES de chegar na quina. Sobe a 2,5 m/s, até 0,4 × escala, com segunda passada se a curva não bastou.
        public static int Ducks; public static float DuckMax, DuckLeft;                               // laboratório (DuckLeft: maior sobra do topo acima do limite depois da curva)
        static bool RoofProfile(FBCar u, FBDriverPose dp, Vector3 seatW, Vector3 up, Vector3 side)
        {
            if (dp.roofCar == u) return dp.roofOk;
            dp.roofCar = u; dp.roofOk = false; float hTop = float.NaN, edge = 0;
            for (float o = 0; o <= 1.3f; o += .04f)
            {
                var p0 = seatW + side * o + up * 2.5f; float best = 9, hb = 0;
                foreach (var h in Physics.RaycastAll(p0, -up, 3.2f, 1 << FBWorld.LVehicle, QueryTriggerInteraction.Ignore))
                    if (h.collider.GetComponentInParent<FBCar>() == u && Vector3.Dot(h.normal, up) > .5f && h.distance < best) { best = h.distance; hb = Vector3.Dot(h.point - seatW, up); }
                if (best >= 9) { if (o < .2f) continue; break; }
                if (float.IsNaN(hTop)) { if (hb < .25f) return false; hTop = hb; }                 // o 1º acerto acima do quadril sentado é o teto (sem teto = conversível/caçamba)
                if (hb < hTop - .12f) break; hTop = Mathf.Max(hTop, hb); edge = o;                   // desceu mais de 12 cm: acabou o teto (porta/janela/caixa de roda)
            }
            if (float.IsNaN(hTop)) return false; dp.roofH = hTop; dp.roofEdge = edge + .02f; dp.roofOk = true; return true;
        }
        static void HeadDuck(FBCharacter a, Animator an, FBCar u, Vector3 up, FBDriverPose dp)
        {
            var head = an.GetBoneTransform(HumanBodyBones.Head); var sp = an.GetBoneTransform(HumanBodyBones.Spine); if (!head || !sp || !dp) return;
            float k = a.transform.lossyScale.y / FBConst.ModelScale; var seatW = SeatHips(u);
            var side = Vector3.ProjectOnPlane(head.position - seatW, up); side = Vector3.Dot(side, u.transform.right) >= 0 ? u.transform.right : -u.transform.right;   // lado da porta = lado em que a cabeça está
            if (dp.roofCar != u) dp.roofCar = null; if (!RoofProfile(u, dp, seatW, up, side)) { dp.duck = 0; return; }
            float Over(Vector3 top)
            {
                float lat = Vector3.Dot(top - seatW, side), fz = Mathf.Abs(Vector3.Dot(top - seatW, u.transform.forward)); if (fz > 1.1f * k) return 0;
                float d = lat - dp.roofEdge, allowed = dp.roofH - .055f + Mathf.Max(0, d) * 1.6f; return Vector3.Dot(top - seatW, up) - allowed;
            }
            var top = head.position + up * .275f * k; float need = Mathf.Max(0, Over(top));
            dp.duck = Mathf.MoveTowards(dp.duck, Mathf.Min(need, .4f * k), Time.deltaTime * (need > dp.duck ? 2.5f : .6f));
            if (dp.duck < .003f) return;
            var dir = (seatW - head.position).Flat(); if (dir.sqrMagnitude < 1e-4f) dir = a.transform.forward.Flat(); dir.Normalize();
            var ax = Vector3.Cross(up, dir).normalized; float done = 0;
            for (int it = 0; it < 2; it++)
            {
                var v = top - sp.position; float L = Mathf.Max(.3f, v.magnitude), phi = Vector3.Angle(up, v) * Mathf.Deg2Rad;
                float want = it == 0 ? dp.duck : Mathf.Min(Mathf.Max(0, Over(top)), .4f * k - dp.duck); if (want < .005f) break;
                float th = (Mathf.Acos(Mathf.Clamp(Mathf.Cos(phi) - want / L, -1, 1)) - phi) * Mathf.Rad2Deg; if (th < .1f) break; th = Mathf.Min(th * 1.1f, 60 - done); if (th <= 0) break;
                foreach (var (bb, w) in new[] { (HumanBodyBones.Spine, .3f), (HumanBodyBones.Chest, .3f), (HumanBodyBones.UpperChest, .15f), (HumanBodyBones.Neck, .25f) })
                { var b = an.GetBoneTransform(bb); if (b) b.rotation = Quaternion.AngleAxis(th * w, ax) * b.rotation; }
                done += th; top = head.position + up * .275f * k;
            }
            Ducks++; DuckMax = Mathf.Max(DuckMax, dp.duck); DuckLeft = Mathf.Max(DuckLeft, Over(top));
        }
        // alcance: personagem de tronco alto / braço curto não chegava ao volante (braço esticado, mão 8–17 cm antes do aro no S10 — validação 4).
        // Inclina o tronco à frente só o necessário (até 25°, suavizado) e, se ainda faltar, escorrega o corpo no banco (até 10 cm).
        public static void Reach(FBCharacter a, FBCar u, FBDriverPose dp, Transform lA, Transform lF, Transform lH, Transform rA, Transform rF, Transform rH, Vector3 tL, Vector3 tR, float w)
        {
            float Need() { float n = 0; foreach (var (A, F, H, t) in new[] { (lA, lF, lH, tL), (rA, rF, rH, tR) }) { if (!A || !F || !H) continue; float len = (F.position - A.position).magnitude + (H.position - F.position).magnitude; n = Mathf.Max(n, (t - A.position).magnitude - len * .97f); } return n; }
            var an = a.an; var sp = an.GetBoneTransform(HumanBodyBones.Spine); var ch = an.GetBoneTransform(HumanBodyBones.Chest) ?? an.GetBoneTransform(HumanBodyBones.UpperChest);
            float need = Need(), want = 0; LastNeed = need;
            if (need > 0 && sp && lA) { float Ls = Mathf.Max(.2f, (lA.position - sp.position).magnitude); want = Mathf.Min(u && u.mx ? 42 : 25, Mathf.Asin(Mathf.Clamp01(need / Ls)) * Mathf.Rad2Deg * 1.15f); }   // r44: moto MX deita mais o tronco
            float lean = want; if (dp) { dp.reachLean = Mathf.Lerp(dp.reachLean, want, 1 - Mathf.Exp(-Time.deltaTime * 6)); lean = dp.reachLean; }
            lean *= w; var right = u.transform.right;
            if (lean > .05f) { if (sp) sp.rotation = Quaternion.AngleAxis(lean * (ch ? .55f : 1), right) * sp.rotation; if (ch) ch.rotation = Quaternion.AngleAxis(lean * .45f, right) * ch.rotation; }
            float rest = Need(); LastRest = rest; if (rest > .002f) a.transform.position += u.transform.forward * Mathf.Min(rest, u && u.mx ? .2f : .1f) * w;
        }
        public static float LastNeed, LastRest;                                                  // laboratório (r49: GS 1250 deitada no tanque)
        // corpo do piloto: tronco deita com a velocidade e na frenagem, vai para trás no grau, acompanha a inclinação; cabeça olha adiante
        static void RiderBody(FBCharacter a, FBCar car, Rider r, float wt = 1)
        {
            var an = a.an; var right = car.transform.right; var fwd = car.transform.forward;
            float fwdLean = ((.04f + r.v * .30f + Mathf.Clamp(-r.sp * 3, 0, .2f)) * (1 - Mathf.Clamp01(r.a / .5f)) - r.a * .55f) * wt, side = Mathf.Clamp(r.lean * .55f, -.4f, .4f) * wt; float[] w = { .3f, .4f, .3f }; int i = 0;
            foreach (var bb in new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest })
            {
                var b = an.GetBoneTransform(bb); if (!b) continue; float k = w[i++ % 3];
                b.rotation = Quaternion.AngleAxis(fwdLean * k * Mathf.Rad2Deg, right) * Quaternion.AngleAxis(-side * k * Mathf.Rad2Deg, fwd) * b.rotation;   // + = tronco para a frente
            }
            var hd = an.GetBoneTransform(HumanBodyBones.Neck) ?? an.GetBoneTransform(HumanBodyBones.Head);
            if (hd) hd.rotation = Quaternion.AngleAxis(-fwdLean * .75f * Mathf.Rad2Deg, right) * Quaternion.AngleAxis(side * .8f * Mathf.Rad2Deg, fwd) * hd.rotation;
        }
        public static void FpHead(FBCharacter a, bool on)
        {
            if (!a || !a.an) return; var hb = a.an.GetBoneTransform(HumanBodyBones.Head); if (!hb || a.fpHeadOn == on) return; a.fpHeadOn = on; hb.localScale = Vector3.one * (on ? .001f : 1);
        }
        // ---- zoom da câmera do veículo (pedido 20/10: mais opções e câmeras mais próximas, SEM 1ª pessoa): V (controle: R3, toque: botão
        // de vista) passa pelos níveis, a roda do mouse ajusta fino entre eles; o nível fica salvo. 1 = distância pela caixa do veículo.
        public static readonly float[] ZOOMS = { .34f, .48f, .64f, .82f, 1f, 1.25f };
        static readonly string[] ZOOM_NAMES = { "Câmera colada", "Câmera bem perto", "Câmera perto", "Câmera média", "Câmera padrão", "Câmera longe" };
        static float camZoom = -1, camZoomS = 1;
        public static float CamZoom { get { if (camZoom < 0) camZoom = Mathf.Clamp(PlayerPrefs.GetFloat("fb_vehcam_zoom", .82f), ZOOMS[0], ZOOMS[^1]); return camZoom; } }
        public static void SetZoom(float z, bool label)
        {
            camZoom = Mathf.Clamp(z, ZOOMS[0], ZOOMS[^1]); PlayerPrefs.SetFloat("fb_vehcam_zoom", camZoom);
            if (label) { int k = 0; for (int i = 1; i < ZOOMS.Length; i++) if (Mathf.Abs(ZOOMS[i] - camZoom) < Mathf.Abs(ZOOMS[k] - camZoom)) k = i; FBHUD.I?.Center(ZOOM_NAMES[k], .9f); }
        }
        static void CamZoomInput(FBInputState inp)
        {
            if (inp.toggleView) { int k = System.Array.FindIndex(ZOOMS, z => z > CamZoom + .01f); SetZoom(k < 0 ? ZOOMS[0] : ZOOMS[k], true); }
            var ms = UnityEngine.InputSystem.Mouse.current;
            if (ms != null && FBInput.Locked) { float w = ms.scroll.ReadValue().y; if (Mathf.Abs(w) > .1f) SetZoom(CamZoom * (w > 0 ? .9f : 1 / .9f), false); }
        }
        // ---- câmera de perseguição / 1ª pessoa (olhos do motorista)
        public static void Camera(Camera cam, float dt, float camYaw, float camPitch)
        {
            var me = FBMatch.Player; var car = FBVehicles.CarOf(me); if (!car) return; var Kd = KindOf(car);
            // a câmera acompanha a guinada do veículo (o mouse gira em volta dela)
            float cy = car.transform.eulerAngles.y, d = Mathf.DeltaAngle(lastCarYaw, cy); lastCarYaw = cy;
            // r47: moto (fora o MX, que tem câmera própria) segue o rumo com amortecimento — antes somava a guinada do chassi quadro a
            // quadro (rígido: cada balanço/contra-esterço da moto virava tranco da câmera)
            if (car.kind == "moto" && !car.mx) { motoYawLag += d; float take = motoYawLag * (1 - Mathf.Exp(-dt * 5)); motoYawLag -= take; d = take; } else motoYawLag = 0;
            // r47d: no MX a guinada do chassi NÃO entra no camYaw — a câmera dele tem rumo próprio (velocidade) e lê qualquer mudança no camYaw
            // como mouse (órbita): somar aqui contava a curva duas vezes e a órbita nunca voltava ao centro (motolab r47d: moto 5,7 m fora do
            // centro da tela e salto de 65° num quadro na curva à direita)
            if (FBPlayer.I && !car.mx) FBPlayer.I.camYaw += d;
            var rot = Quaternion.Euler(-camPitch, camYaw + d, 0); var f = rot * Vector3.forward;
            if (FBVehicles.fp)
            {
                FpHead(me, true); var dpo = me.GetComponent<FBDriverPose>(); if (dpo) dpo.PoseNow();     // corpo no banco DESTE quadro antes de ler a cabeça
                var hb = me.an ? me.an.GetBoneTransform(HumanBodyBones.Head) : null;
                cam.transform.position = hb ? hb.position + car.transform.rotation * new Vector3(0, .09f, .11f) : car.L2W(car.seat + new Vector3(0, car.kind == "moto" ? .95f : .62f, car.kind == "moto" ? .05f : .1f));
                cam.transform.rotation = Quaternion.Euler(-camPitch, camYaw + d, -car.transform.eulerAngles.z * .0f); cam.nearClipPlane = .05f; return;
            }
            FpHead(me, false); cam.nearClipPlane = .1f;
            if (car.mx) { car.mx.Camera(cam, dt, camPitch); return; }                                   // r45: câmera do MX Grau (MXCam)
            // pivô e distância pela CAIXA do veículo (pedido 20/10: a câmera se adapta a cada modelo — Up/Fusca perto, picape/Kombi/caminhão longe)
            Vector3 piv; float dist;
            if (car.kind == "moto") { piv = car.transform.position + Vector3.up * Kd.cam.y; dist = Kd.cam.x; }
            else
            {
                var L = car.lbox; float sc = car.scale, len = L.size.z * sc, hgt = L.size.y * sc; var ctr = car.transform.TransformPoint(L.center);
                piv = new Vector3(ctr.x, ctr.y + hgt * .5f + .5f, ctr.z); dist = Mathf.Clamp(len * .95f + hgt * .85f + .6f, 4.6f, 15f);
            }
            // zoom: suavizado; perto, o pivô desce para a altura do teto (a câmera não fica olhando o capô de cima)
            camZoomS = Mathf.Lerp(camZoomS, CamZoom, 1 - Mathf.Exp(-dt * 8)); float zk = camZoomS;
            float minD = car.kind == "moto" ? 1.4f : Mathf.Max(1.7f, car.lbox.size.z * car.scale * .42f);   // o mais perto: logo atrás do teto
            dist = Mathf.Max(minD, dist * zk); piv.y -= Mathf.Clamp01((1 - zk) / .66f) * (car.kind == "moto" ? .25f : .45f);
            dist += Mathf.Abs(car.v) * .025f * Mathf.Min(1, zk + .2f); var dir = -f;
            car.SetColliders(false); float dd = FBWorld.RayHit(piv, dir, dist + .3f, out var h) ? Mathf.Max(.6f, h.distance - .3f) : dist; car.SetColliders(true);
            cam.transform.position = piv + dir * dd; cam.transform.rotation = Quaternion.LookRotation(piv + f * 8 - cam.transform.position);
            if (shake > .01f) { cam.transform.position += Random.insideUnitSphere * shake * .12f; shake = Mathf.Max(0, shake - dt * 2.5f); }
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 70 + Mathf.Min(12, Mathf.Abs(car.v) * .35f), 1 - Mathf.Exp(-dt * 4));
        }
        // ---- rede: estado do veículo que eu dirijo (vehicleState do web) e aplicação no remoto
        public static List<object> VehicleState()
        {
            var D = cur; if (!D || D.syncId == null) return null; var p = Coords.ToWeb(D.transform.position); float R(float x) => Mathf.Round(x * 100) / 100f;
            if (D.mx) { var e = D.transform.eulerAngles; float W(float d) => (d > 180 ? d - 360 : d) * Mathf.Deg2Rad; return new List<object> { D.syncId, R(p[0]), R(p[1]), R(p[2]), R(-W(e.y)), R(W(e.x)), R(-W(e.z)), R(D.mx.in_steering * .55f), R(D.mx.in_acceleration), R(D.v), 0, R(D.wheelie), FBDoors.Mask(D), LightBits(D) }; }
            return new List<object> { D.syncId, R(p[0]), R(p[1]), R(p[2]), R(D.yaw), R(D.pitch), R(D.roll + D.lean), R(D.steer), R(D.spin), R(D.v), D.wallOn ? 1 : 0, R(D.kind == "moto" ? D.wh.a : 0) , FBDoors.Mask(D), LightBits(D) };
        }
        // r45b (pendência da r45: "sirene/giroflex e luzes do jogador são locais"): índice 13 do estado = bits das luzes de quem dirige
        //  0-1 sirene (0 desligada, 1 normal, 2 rápida, 3 wail) · 2 giroflex · 3-4 faróis (0 auto, 1 ligado, 2 desligado) · 5-6 seta (1 esq, 2 dir,
        //  3 pisca-alerta) · 7 freio. O remoto liga a mesma sirene 3D (FBSiren) e o FBCarLights usa os bits em vez de adivinhar pela velocidade.
        public static int LightBits(FBCar car)
        {
            if (!car) return 0; int b = Mathf.Clamp(FBSiren.Mode(car), 0, 3) | (FBSiren.Lights(car) ? 4 : 0) | ((FBCarLights.headMode & 3) << 3);
            int bk = FBCarLights.PlayerBlinkNow; b |= (bk == 2 ? 3 : bk == -1 ? 1 : bk == 1 ? 2 : 0) << 5; if (FBCarLights.PlayerBrakeNow) b |= 128; return b;
        }
        public static int NetLightsApplied;
        static void NetLights(FBCar car, int b)
        {
            car.netLights = b; NetLightsApplied++; if (!FBSiren.IsPolice(car)) return; int sm = b & 3; bool giro = (b & 4) != 0;
            if (FBSiren.Mode(car) != sm) FBSiren.Set(car, sm);
            if (FBSiren.Lights(car) != giro) FBSiren.SetLights(car, giro);
        }
        public static bool ApplyRemote(FBCharacter actor, List<object> state, float dt, bool npc = false)   // npc: motorista dos eventos de rua (vale nos modos sem veículo)
        {
            if (state == null || state.Count < 12) { Release(actor); return false; }
            if (!npc && !FBVehicles.Allowed) { Release(actor); return false; }
            var car = FBVehicles.BySyncId(state[0] as string ?? state[0]?.ToString()); if (!car) { Release(actor); return false; }
            if (state.Count > 12) FBDoors.SetMask(car, Mathf.RoundToInt(Json.Fv(state[12])), true);
            if (state.Count > 13) NetLights(car, Mathf.RoundToInt(Json.Fv(state[13])));
            int nid = actor.GetComponent<FBRemote>()?.netId ?? -2; var prev = FBVehicles.CarOf(actor);
            if (prev && prev != car) Release(actor);
            if (car.driver && car.driver != actor) { Release(actor); return false; }
            bool entering = !prev; car.remoteDriver = nid; car.driver = actor; FBVehicles.SetDriving(actor, car);
            if (entering)
            {
                actor.StopDance(true); actor.vehiclePose = true; actor.sys = SitClip(car); actor.SetWeaponVisible(false); actor.GetComponent<FBDriverPose>()?.Set(car); actor.gameObject.GetOrAdd<FBDriverPose>().Set(car);
                FBAudio.PlayAt(car.kind == "moto" ? "ie_moto_start" : "veh_start", car.transform.position, .35f, 5);
            }
            float F(int i) => Json.Fv(state[i]);
            // r49i: o estado montado aqui (trânsito, eventos, laboratórios: npc) traz arfagem 0 = moto nivelada; o da rede traz os ângulos da raiz física
            float lvl = npc && car.mx ? car.mx.LevelPitch * Mathf.Deg2Rad : 0;
            car.transform.SetPositionAndRotation(Coords.FromWeb(new List<object> { state[1], state[2], state[3] }), CarRot(F(5) + lvl, F(4), F(6)));
            car.yaw = F(4); car.pitch = F(5); car.v = F(9); car.spin = F(8); car.steerN = Mathf.Clamp(F(7) / .55f, -1, 1);
            if (car.mx) { car.wheelie = F(11); car.mx.RemoteState(car.v, F(7) / .55f, F(8), car.wheelie, dt <= 0 ? 1 / 60f : dt); return true; }   // r44: piloto e som da MX pelo estado
            if (car.kind == "moto") { car.wheelie = F(11); car.trick = Mathf.Clamp01((car.wheelie * Mathf.Rad2Deg - FBMoto.TRICK[0]) / 15); car.rider = new Rider { v = Mathf.Clamp01(Mathf.Abs(car.v) / KIND["moto"].vmax), lean = F(6), a = car.wheelie }; }
            PoseWheels(car, F(7));
            var prefix = "veh_remote_" + car.syncId + "_"; float rel = Mathf.Min(1, Mathf.Abs(car.v) / KindOf(car).vmax);
            var prof = car.kind == "moto" ? null : car.engineProf ??= FBCarSound.ProfileFor(car.id, car);
            if (prof != null)
            {   // acelerador estimado pela variação da velocidade (o estado da rede não traz o pedal): acelerando = carga, constante = meio, caindo = solto
                float ddt = dt <= 0 ? 1 / 60f : dt, accl = (car.v - car.vPrev) / ddt; car.vPrev = car.v;
                float gas = Mathf.Abs(car.v) < .5f ? 0 : Mathf.Clamp01(accl > .4f ? .55f + accl / 6 : accl < -.8f ? 0 : .35f); car.gasEst = Mathf.Lerp(car.gasEst, gas, 1 - Mathf.Exp(-ddt / .25f));
                car.gb ??= new FBCarSound.Gearbox(TopV(car)); var g = car.gb.Step(ddt, car.v, car.gasEst); FBCarSound.EngineAudio(prefix + "e_", prof, g.rpm, car.gb.shiftT > 0 ? .15f : car.gasEst, car.transform.position, .32f, 5);
            }
            else if (Json.O(car.meta, "sound") != null) { float pv = car.revRpm; car.revRpm = car.v; GbMotoAudio(car, car.v, car.v > pv + 1e-3f || (rel > .2f && Mathf.Abs(car.v - pv) < 1e-3f), false, car.wheelie > .08f, dt <= 0 ? 1 / 60f : dt, null, car.transform.position, prefix + "gb_"); }
            else { var (n, rt) = EngineSample(car, Mathf.Clamp01(.18f + rel * .78f)); FBAudio.SetLoop(prefix + "engine", n, .36f, car.transform.position, rt, 4, .8f); }
            return true;
        }
        public static void Release(FBCharacter actor)
        {
            var car = FBVehicles.CarOf(actor); if (!car || actor == FBMatch.Player) return;
            var prefix = "veh_remote_" + car.syncId + "_"; FBCarSound.StopEngine(prefix + "e_"); foreach (var k in new[] { "engine", "gb_par", "gb_ace", "gb_cut" }) FBAudio.StopLoop(prefix + k); if (car.mx) car.mx.RemoteStop();
            car.remoteDriver = -1; car.driver = null; car.netLights = -1; FBVehicles.SetDriving(actor, null);
            var dp = actor.GetComponent<FBDriverPose>(); if (dp) Object.Destroy(dp); actor.vehiclePose = false; actor.sys = null;
        }
        public static void Tick(float dt) { }
        // espera simples sem corrotina explícita
        static void StartCoroutineSafe(this MonoBehaviour m, float t, System.Action a) { if (m) m.StartCoroutine(Wait(t, a)); }
        static System.Collections.IEnumerator Wait(float t, System.Action a) { yield return new WaitForSeconds(t); a(); }
    }

    // DONO ÚNICO dos ossos no veículo. Causas do "braço/perna descontrolado" corrigidas aqui:
    //  - a camada base continuava recebendo a velocidade do carro (VelX/VelZ/Speed = corrida) e Upper Body / Emote Upper Body (esta acima
    //    da Emote Full Body na ordem das camadas) podiam tocar reação de dano/gesto por cima do clipe sentado → no Update (depois do
    //    FBCharacter, antes da avaliação do Animator): parâmetros de locomoção zerados, Upper/Emote Upper em 0, Emote Full Body (clipe
    //    sentado) em 1, an.speed = 1;
    //  - IK de mãos/pés entrava com peso 1 no quadro em que o clipe ainda misturava com o andar → peso sobe em 0,2 s (sem estalo);
    //  - FBFootIK fazia fade de 0,4 s raiando o assoalho e a compensação de degrau brigava com o reposicionamento da raiz → agora desliga na hora;
    //  - a câmera 1ª pessoa lia a cabeça ANTES da pose do banco (um quadro atrás do carro: o corpo "pulava" com a velocidade) → PoseNow()
    //    chamado pela câmera, uma vez por quadro (guarda de quadro: os giros relativos do tronco da moto nunca aplicam duas vezes).
    // FBRig (mira/mão na arma), abertura de ombros, inclinação Q/E e coldre já se desligam com vehiclePose no FBCharacter/FBRig.
    [DefaultExecutionOrder(1000)] public class FBDriverPose : MonoBehaviour
    {
        [System.NonSerialized] public float duck;                                                    // r49: cabeça abaixada entrando/saindo (FBDrive.HeadDuck)
        [System.NonSerialized] public FBCar roofCar; [System.NonSerialized] public float roofH, roofEdge; [System.NonSerialized] public bool roofOk;   // r49i: perfil do teto (HeadDuck)
        [System.NonSerialized] public float reachLean, feetLift; [System.NonSerialized] public float? blendYaw0;
        // frente das coxas no referencial do veículo (graus) — pose deste quadro
        float? BodyYaw() { var an = ch ? ch.an : null; var l = an ? an.GetBoneTransform(HumanBodyBones.LeftUpperLeg) : null; var r = an ? an.GetBoneTransform(HumanBodyBones.RightUpperLeg) : null; if (!l || !r || !car) return null; var bf = Vector3.Cross(r.position - l.position, Vector3.up).Flat(); return bf.sqrMagnitude > 1e-6f ? Vector3.SignedAngle(car.transform.forward.Flat(), bf, Vector3.up) : null; }                       // inclinação para alcançar o volante (suavizada)
        FBCar car; FBCharacter ch; Animator an; int posed = -1, LU = -1, LE = -1, LEU = -1, groundedH; bool hasGrounded; float w;
        // r43: subir/descer da moto (FBDrive.StartMount/StartDismount): mount -> blend -> null; dismount -> FBDrive.FinishDismount
        [System.NonSerialized] public string trans; [System.NonSerialized] public float transT, transLen; [System.NonSerialized] public Vector3 baseL;
        // r50: clipe da transição (StartCarIn/StartCarOut/StartMount/StartDismount). Se outro sistema trocar o ch.sys no meio de entrar/sair (o
        // Update a pé do FBPlayer apagava no quadro do F), volta aqui — esta classe roda depois (ordem 1000). FBDrive.SysRestores conta.
        [System.NonSerialized] public string transClip;
        [System.NonSerialized] public bool carAnim; [System.NonSerialized] public System.Action<FBCharacter, FBCar> onOut;   // r45: clipe de carro do 171 / fim do sair de NPC
        [System.NonSerialized] public bool path; [System.NonSerialized] public string pathClip; [System.NonSerialized] public Vector3 pathS0, pathS, pathE; [System.NonSerialized] public float pathK = .15f; [System.NonSerialized] public float yaw0;   // r45d: FBAnimPaths
        public FBDrive.PoseTargets Last { get; private set; }
        public FBCar Car => car; public float Weight => w;
        static readonly string[] ZERO = { "VelX", "VelZ", "Speed", "VerticalVelocity", "CrouchAlpha" }, OFF = { "IsRunning", "IsAiming", "IsShooting", "InCombat", "InAir", "IsCrouching", "Tactical Sprint" };
        readonly List<int> zeroF = new(), offB = new();
        public void Set(FBCar c)
        {
            if (car != c) w = 0; car = c; ch = GetComponent<FBCharacter>(); an = ch ? ch.an : null; if (!an) return;
            ch.HoldAnimate(this, true);                                                               // r46i: Animator reescreve todo quadro (ver FBCharacter.HoldAnimate)
            LU = an.GetLayerIndex("Upper Body"); LE = an.GetLayerIndex("Emote Full Body"); LEU = an.GetLayerIndex("Emote Upper Body");
            zeroF.Clear(); offB.Clear(); hasGrounded = false;
            foreach (var p in an.parameters)
            {
                string n = p.name.Trim();
                if (p.type == AnimatorControllerParameterType.Float && System.Array.IndexOf(ZERO, n) >= 0) zeroF.Add(p.nameHash);
                else if (p.type == AnimatorControllerParameterType.Bool && System.Array.IndexOf(OFF, n) >= 0) offB.Add(p.nameHash);
                else if (p.type == AnimatorControllerParameterType.Bool && n == "IsGrounded") { groundedH = p.nameHash; hasGrounded = true; }
            }
        }
        // [parceiro] v1 (thread de física): tempo de CPU do Update/LateUpdate medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
        static readonly int __fbt_Update = FBTempo.Id("FBDriverPose.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            if (!car || !ch || !an || !an.runtimeAnimatorController) return;
            w = Mathf.MoveTowards(w, 1, Time.deltaTime / .2f);
            if (FBDrive.SysGuard && (trans == "mount" || trans == "dismount") && transClip != null && ch.sys != transClip && ch.alive && !ch.knocked && !FBFall.Busy(ch)) { ch.sys = transClip; FBDrive.SysRestores++; FBDrive.SysRestoreLast = $"{ch.name} {trans} {transClip} t={transT:0.00}"; }
            foreach (var h in zeroF) an.SetFloat(h, 0); foreach (var h in offB) an.SetBool(h, false); if (hasGrounded) an.SetBool(groundedH, true);
            if (ch.alive && !ch.special)
            {
                if (LU >= 0) an.SetLayerWeight(LU, 0); if (LEU >= 0) an.SetLayerWeight(LEU, 0);
                if (LE >= 0 && ch.sys != null) an.SetLayerWeight(LE, Mathf.Max(ch.DanW, trans == "mount" || trans == "dismount" ? 1 : Mathf.SmoothStep(0, 1, w)));
                an.speed = (trans == "mount" || trans == "dismount") && !carAnim ? FBDrive.MOUNT_SPEED : 1;
            }
        }
        public void PoseNow()
        {
            if (posed == Time.frameCount || !car || !ch || FBFall.Busy(ch)) return; posed = Time.frameCount;   // r46: caindo da moto, o corpo é da física/levantar
            Last = FBDrive.PoseActor(ch, car, Mathf.SmoothStep(0, 1, w), this);
        }
        void OnDestroy() { if (ch) ch.HoldAnimate(this, false); }
        // r50 (teste -fbjogo, quadro a quadro): o caminho do quadril (PoseActor, k = transT/transLen) andava com o relógio PRÓPRIO, somado
        // DEPOIS de posar — um quadro atrás do Animator; num quadro longo (GC, foto, espelhos do Enter) o quadril andava o tempo do quadro
        // anterior e compensava no seguinte (XRE descendo: 0,2 m/s no quadro de 73 ms, 3,8 m/s no próximo). E o clipe de entrar no carro
        // começava um quadro depois do Enter (quadros de 87/120 ms) — o Animator ficava 0,09 do clipe (~0,2 s) ATRÁS do caminho: o quadril ia
        // para o banco antes das pernas entrarem (pé abaixo do chão → FeetAboveGround subindo o corpo até 20 cm; perna na soleira; cabeça no
        // teto antes de abaixar). Agora o relógio é o tempo do PRÓPRIO clipe na camada "Emote Full Body" (normalizedTime × transLen, antes de
        // posar); clipe ainda não começou = o caminho espera (até 0,35 s); Animator parado (culling) por 0,25 s = volta ao relógio próprio.
        public static bool ClockSync = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbsemrelogio") < 0;
        public static int ClockSyncs, ClockWaits;
        float clockWait, clockStall, lastNt = -1;
        bool SyncClock()
        {
            if (!ClockSync || (trans != "mount" && trans != "dismount") || transClip == null || !an || LE < 0 || transLen <= 0) { clockWait = clockStall = 0; lastNt = -1; return false; }
            bool next = an.IsInTransition(LE); var ci = next ? an.GetNextAnimatorClipInfo(LE) : an.GetCurrentAnimatorClipInfo(LE);
            if (ci.Length > 0 && ci[0].clip && ci[0].clip.name == transClip)
            {
                float nt = (next ? an.GetNextAnimatorStateInfo(LE) : an.GetCurrentAnimatorStateInfo(LE)).normalizedTime; clockWait = 0;
                if (Mathf.Abs(nt - lastNt) < 1e-5f) { if ((clockStall += Time.deltaTime) > .25f) return false; } else clockStall = 0;
                lastNt = nt; transT = Mathf.Max(transT, Mathf.Clamp01(nt) * transLen); ClockSyncs++; return true;
            }
            if (transT <= 0 && (clockWait += Time.deltaTime) < .35f) { ClockWaits++; return true; }   // o clipe ainda não entrou na camada
            return false;
        }
        static readonly int __fbt_LateUpdate = FBTempo.Id("FBDriverPose.LateUpdate");   // [parceiro] FBTempo (ver Update)
        void LateUpdate()
        {
            if (!FBTempo.On) { LateUpdate__fbt(); return; }
            long t0 = FBTempo.T0(); LateUpdate__fbt(); FBTempo.Soma(__fbt_LateUpdate, t0);
        }
        void LateUpdate__fbt()
        {
            if (!car || !ch) { Destroy(this); return; }
            if (trans != null) FBPoseBlend.Ensure(ch);                                           // r46c: pronto antes da troca do fim da saída
            if (trans != null && (!ch.alive || FBVehicles.CarOf(ch) != car)) trans = null;
            bool synced = SyncClock();
            PoseNow();
            if (trans == "mount" || trans == "dismount") FBPoseBlend.Run(ch);                     // r46k: mistura do começo (StartMount/StartDismount) já no 1º quadro
            if (trans == null) return; if (!synced) transT += Time.deltaTime; if (transT < transLen) return;
            if (trans == "mount") { blendYaw0 = BodyYaw(); trans = "blend"; transT = 0; transLen = .35f; ch.sys = FBDrive.SitClip(car); if (carAnim && FBDoors.HasPart(car, 1)) FBDoors.SetMask(car, FBDoors.Mask(car) & ~1, true); }
            else if (trans == "blend") { trans = null; carAnim = false; }
            else if (trans == "dismount")
            {   // r46: guarda a pose final, troca (raiz para baixo do quadril) e CORTA o clipe de saída — o FBPoseBlend leva a pose ao parado
                trans = null; var c0 = ch; FBPoseBlend.Capture(c0, .3f);
                if (onOut != null) { var f = onOut; onOut = null; f(c0, car); } else FBDrive.FinishDismount(c0, car);
                if (c0 && !FBVehicles.CarOf(c0)) c0.CutDance();
                FBPoseBlend.Pin(c0);                                                             // este quadro já sai com a pose guardada (raiz já trocada)
            }
        }
    }

    // IK de dois ossos com polo (ikTwoBonePole do actor.js)
    // r46 (causa medida das "articulações quebradas" ao entrar/sair/subir/descer): o peso era aplicado só no ALVO (Lerp da mão até o alvo),
    // mas o plano da dobra era sempre recalculado pelo polo inteiro — com w = 0 o IK NÃO era neutro: o cotovelo/joelho era girado para o
    // lado do polo. Na entrada do carro o polo é o do banco (dentro do carro) com a pessoa ainda do lado de fora → cotovelo dobrado ao
    // contrário e o braço torcido ~180° (sonda r46a: 70 quadros "cotovelo ao contrário" com o IK em peso 0; o mesmo clipe tocado sozinho
    // no editor: 0). Agora resolve com o alvo inteiro e mistura as ROTAÇÕES pelo peso: w = 0 não toca no osso, w = 1 é o resultado de antes.
    // Polo quase na reta ombro→alvo (plano indefinido, o cotovelo "pula" de lado a lado): vale a dobra atual do próprio osso.
    // r46c — TORÇÃO DO OSSO DE CIMA: o FromToRotation leva o braço/coxa até o cotovelo/joelho novo pelo giro MÍNIMO, sem cuidar da torção em
    // torno do próprio eixo. O cotovelo ia para o lugar certo, mas a dobradiça do osso ficava virada e o antebraço/canela dobrava para o lado
    // anatômico errado (sonda r46c, piloto MX parado: 112 quadros "cotovelo ao contrário" com o polo do RigData original). O eixo da
    // dobradiça é fixo no referencial do osso de cima (cotovelo/joelho = dobradiça; pronação é torção do antebraço no eixo dele): é aprendido
    // da pose animada quando ela está bem dobrada (os clipes nunca dobram ao contrário — FBClipAudit) e, depois de posicionar, o osso de
    // cima gira no próprio eixo até a dobradiça coincidir com o plano da dobra pedida. A posição do cotovelo/joelho (polo) não muda.
    public static class FBIK
    {
        static readonly Dictionary<Transform, Vector3> hinge = new();
        static readonly Dictionary<Transform, Quaternion> wrote = new(); public static int StaleSkips;
        public static bool Anat; public const float TwistLim = 45; public static int PoleClamps, SideHolds; static readonly Dictionary<Transform, float> clampSide = new();
        /// <summary>r46h: dobradiça anatômica aprendida da animação (mundo, normal do plano da dobra; girar o osso de baixo +ângulo em torno dela DOBRA mais).</summary>
        public static bool TryHinge(Transform upper, out Vector3 world) { if (upper && hinge.TryGetValue(upper, out var l)) { world = upper.rotation * l; return true; } world = Vector3.zero; return false; }
        public static int Twists, BigTwists; public static float TwistMax; public static string Where = "-";   // r46g: quem pediu (fase), para a contagem de torções > 90°
        public static readonly Dictionary<string, int> BigBy = new();
        // [parceiro] poleW (v16): quanto o cotovelo vai para o plano do polo. 1 = como sempre foi (todas as chamadas antigas);
        // menor que 1 mistura com o plano atual do braço, para o cotovelo entrar e sair junto com o peso, sem estalo.
        public static void TwoBonePole(Transform upper, Transform fore, Transform hand, Vector3 target, Vector3 pole, float w = 1, float poleW = 1)
        {
            if (!upper || !fore || !hand || w <= 1e-3f) return; w = Mathf.Min(1, w);
            Vector3 a = upper.position, b = fore.position, c = hand.position; float lab = (b - a).magnitude, lbc = (c - b).magnitude;
            // r46i: só aprende a dobradiça de pose que o ANIMATOR escreveu — se o osso está como este IK o deixou no quadro anterior (Animator parado
            // fora da câmera), aprender dela deixava o eixo girar até 45° por quadro e a coxa ia torcendo sem fim
            bool fresh = !wrote.TryGetValue(upper, out var wq) || wq != upper.localRotation;
            if (fresh) { var v1 = b - a; var v2 = c - b; if (Vector3.Angle(v1, v2) > 30) { if (hinge.Count > 6000) { hinge.Clear(); wrote.Clear(); clampSide.Clear(); } hinge[upper] = Quaternion.Inverse(upper.rotation) * Vector3.Cross(v1, v2).normalized; } }
            else StaleSkips++;
            var ad = target - a; float d = ad.magnitude; if (d < 1e-5f) return; d = Mathf.Clamp(d, Mathf.Abs(lab - lbc) + 1e-3f, lab + lbc - 1e-3f); var dir = ad.normalized;
            var pd = pole - a; pd -= dir * Vector3.Dot(pd, dir); var cur = b - a; cur -= dir * Vector3.Dot(cur, dir);
            if (pd.sqrMagnitude < 1e-4f * (lab * lab)) pd = cur; if (pd.sqrMagnitude < 1e-8f) return; pd.Normalize();
            if (poleW < .999f)
            {   // [parceiro] v16: giro atual do cotovelo medido em volta da linha ombro-mão de agora e levado para a linha nova
                // (antes da torção limitada abaixo, que continua valendo sobre o polo misturado)
                var h0 = c - a;
                if (h0.sqrMagnitude > 1e-8f)
                {
                    h0.Normalize(); var cur0 = b - a; cur0 -= h0 * Vector3.Dot(cur0, h0);
                    if (cur0.sqrMagnitude > 1e-8f) { cur0 = Quaternion.FromToRotation(h0, dir) * cur0; pd = Vector3.Slerp(cur0.normalized, pd, Mathf.Clamp01(poleW)).normalized; }
                }
            }
            // r46g: TORÇÃO LIMITADA — com a dobradiça anatômica do osso (h), o cotovelo/joelho "sem torção" fica em dir × h; o polo pedido só
            // pode puxar até TwistLim graus em volta da reta ombro→mão. Antes o osso de cima girava o que fosse preciso (até 180°): coxa na pose de
            // pilotar a MX com torção 2× o máximo do avatar, e o ragdoll da queda nascia dela (sonda r46g, 1º quadro do ragdoll)
            if (Anat && hinge.TryGetValue(upper, out var hl0))
            {
                var hw = upper.rotation * hl0; hw -= dir * Vector3.Dot(hw, dir);
                if (hw.sqrMagnitude > 1e-6f) { var nat = Vector3.Cross(dir, hw).normalized; float ang = Vector3.SignedAngle(nat, pd, dir);
                    if (Mathf.Abs(ang) > TwistLim)
                    {   // r46j: polo quase oposto ao natural (|ang| perto de 180°) — o sinal virava de um quadro para o outro e o cotovelo pulava 2×TwistLim
                        // (braço sentado a 9.800°/s); acima de 135° mantém o lado do quadro anterior
                        float sg = Mathf.Sign(ang); if (Mathf.Abs(ang) > 135 && clampSide.TryGetValue(upper, out var ls)) { if (ls != sg) SideHolds++; sg = ls; }
                        clampSide[upper] = sg; pd = Quaternion.AngleAxis(sg * TwistLim, dir) * nat; PoleClamps++;
                    }
                    else clampSide.Remove(upper); }
            }
            float x = (lab * lab - lbc * lbc + d * d) / (2 * d), y = Mathf.Sqrt(Mathf.Max(0, lab * lab - x * x));
            var elbow = a + dir * x + pd * y; var end = a + dir * d;
            Quaternion u0 = upper.rotation, f0 = fore.rotation;
            upper.rotation = Quaternion.FromToRotation(b - a, elbow - a) * upper.rotation;
            float twW = Mathf.SmoothStep(0, 1, (y / lab - .04f) / .16f);   // r46d: rampa, não degrau — braço quase reto (plano da dobra indefinido) não torce; ligar/desligar num limiar piscava até 179° (sonda r46d: braço a 14.000°/s pilotando parado)
            if (Anat && twW > 1e-3f && hinge.TryGetValue(upper, out var hl))
            {   // dobradiça do osso (mundo) × normal do plano pedido, as duas projetadas no plano perpendicular ao osso
                var ax = (elbow - a).normalized; var want = Vector3.Cross(elbow - a, end - elbow); var have = upper.rotation * hl;
                want -= ax * Vector3.Dot(want, ax); have -= ax * Vector3.Dot(have, ax);
                if (want.sqrMagnitude > 1e-8f && have.sqrMagnitude > 1e-8f) { float tw = Vector3.SignedAngle(have, want, ax) * twW; if (Mathf.Abs(tw) > .5f) { upper.rotation = Quaternion.AngleAxis(tw, ax) * upper.rotation; Twists++; TwistMax = Mathf.Max(TwistMax, Mathf.Abs(tw)); if (Mathf.Abs(tw) > 90) { BigTwists++; var key = upper.name.Replace("mixamorig:", "") + "@" + Where; BigBy[key] = BigBy.TryGetValue(key, out var bn) ? bn + 1 : 1; } } }
            }
            var b2 = fore.position; var c2 = hand.position; fore.rotation = Quaternion.FromToRotation(c2 - b2, end - b2) * fore.rotation;
            if (w < 1) { var u1 = upper.rotation; var f1 = fore.rotation; upper.rotation = Quaternion.Slerp(u0, u1, w); fore.rotation = Quaternion.Slerp(f0, f1, w); }
            wrote[upper] = upper.localRotation;
        }
        // polo "natural": o lado para onde a articulação já dobra na pose animada (meio do osso → cotovelo/joelho), a 'dist' de distância
        public static Vector3 NaturalPole(Transform upper, Transform fore, Transform hand, float dist)
        {
            Vector3 a = upper.position, b = fore.position, c = hand.position; var ac = c - a; float l2 = ac.sqrMagnitude;
            var off = b - (a + ac * (l2 > 1e-8f ? Mathf.Clamp01(Vector3.Dot(b - a, ac) / l2) : 0)); return off.sqrMagnitude > 1e-6f ? b + off.normalized * dist : b;
        }
    }

    // ---- GRAU: medidor de equilíbrio + distância (moto)
    // r45: velocímetro removido a pedido (29/10: "não precisamos mais deles")
    public static class FBGrauHud
    {
        static GameObject root; static Text g1, g2; static RectTransform marker; static GameObject bar;
        public static void Show(FBCar car)
        {
            if (!root)
            {
                var cv = FBUI.Canvas("Grau", 31); root = cv.gameObject;
                g1 = FBUI.Label(cv.transform, "", 30, new Vector2(.5f, .83f), new Vector2(.5f, .83f), Vector2.zero, new Vector2(500, 40), TextAnchor.MiddleCenter, true);
                g2 = FBUI.Label(cv.transform, "", 15, new Vector2(.5f, .83f), new Vector2(.5f, .83f), new Vector2(0, -30), new Vector2(500, 24), TextAnchor.MiddleCenter, false, new Color(1, 1, 1, .85f));
                var b = FBUI.Panel(cv.transform, "barra", new Vector2(.5f, .83f), new Vector2(.5f, .83f), new Vector2(0, -52), new Vector2(190, 9), new Color(0, 0, 0, .55f)); bar = b.gameObject; float max = FBMoto.FAIL * 1.05f;
                var gz = FBUI.Panel(b.transform, "zona", new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, Vector2.zero, new Color(.21f, .82f, .35f, .55f)); gz.rectTransform.anchorMin = new Vector2(FBMoto.BALANCE * .72f / max, 0); gz.rectTransform.anchorMax = new Vector2((FBMoto.BALANCE * .72f + FBMoto.BALANCE * .56f) / max, 1); gz.rectTransform.offsetMin = gz.rectTransform.offsetMax = Vector2.zero;
                var gr = FBUI.Panel(b.transform, "vermelho", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(.94f, .24f, .24f, .6f)); gr.rectTransform.anchorMin = new Vector2(FBMoto.FAIL / max, 0); gr.rectTransform.offsetMin = gr.rectTransform.offsetMax = Vector2.zero;
                marker = FBUI.Panel(b.transform, "m", new Vector2(0, .5f), new Vector2(0, .5f), Vector2.zero, new Vector2(4, 13), Color.white).rectTransform;
            }
            var w = car.wh; bool on = w.a > .08f; var msg = car.grauMsg is { } m && Time.time < m.until ? m : default((string, string, bool, float)?);
            if (!on && msg == null) { root.SetActive(false); return; }
            root.SetActive(true); bar.SetActive(on);
            if (on) { g1.text = "GRAU " + w.dist.ToString("0.0").Replace('.', ',') + " m"; g2.text = w.best > 0 ? "RECORDE " + w.best.ToString("0.0").Replace('.', ',') + " m" : "ESPAÇO + W · FREIE (S) PARA SEGURAR"; float max = FBMoto.FAIL * 1.05f; marker.anchorMin = marker.anchorMax = new Vector2(Mathf.Clamp01(w.a / max) * .98f, .5f); g1.color = w.a > FBMoto.BALANCE * 1.18f ? new Color(1, .42f, .42f) : Color.white; }
            else { var mm = msg.Value; g1.text = mm.Item1; g2.text = mm.Item2 ?? ""; g1.color = mm.Item3 ? new Color(1, .42f, .42f) : new Color(1, .82f, .29f); }
        }
        public static void Hide() { if (root) root.SetActive(false); }
    }
}
