// Pós-animação de tronco e mãos (porte do actor.js, na MESMA ORDEM do web; chamado do LateUpdate do FBCharacter, depois do Animator):
//  1) Aim(): mira do tronco — Spine/Chest/UpperChest giram até o CANO apontar para o ponto mirado (yaw + no máximo ~22° de pitch na
//     coluna); o resto do pitch sai dos ombros (os dois braços sobem/descem com a arma).
//  2) Hands(): mão esquerda (WeaponGripPoints/TwoBoneIK do original): fuzil → palma por baixo do guarda-mão, recuando pelo trilho até o
//     braço alcançar e, se ainda faltar, erguendo a arma em volta da mão direita (≤ 14°); pistola → mão direita recua e a esquerda apoia
//     por baixo/ao lado. Dedos: direita fechada no punho com o indicador no gatilho; esquerda abraçando o guarda-mão.
// Causas corrigidas (antes era Animation Rigging: MultiAimConstraint + TwoBoneIKConstraint):
//  - o MultiAim apontava o EIXO DO PEITO para o alvo; o clipe de mira gira o tronco para segurar o fuzil → o cano saía desviado do alvo;
//  - o rig rodava ANTES do LateUpdate que baixa o fuzil no descanso / alinha a pistola → a mão esquerda ficava segurando o nada;
//  - a rotação da mão esquerda era aplicada aos eixos crus do osso (cada rig tem os seus: IE/CC giravam a mão 90°) e os dedos usavam
//    Cross(cano, palma) copiado do three.js sem trocar a ordem (espelho → dedos para cima/esquerda). Agora a mão vai pelo referencial
//    anatômico (FBHand) e os produtos vetoriais estão no espaço do Unity.
// [parceiro] v14–v17 (31/10–01/11/2026): arma fora do peito pela forma da malha da arma contra o corpo medido (KeepOutOfTorso + Carry),
// patrulha em movimento (cano baixo e coronha na frente do bolso do ombro), cabeça olhando pela mira também com fuzil, desvirar o tronco
// parado mirando (desligado: Desvirar = 0) e contadores para o laboratório (FBLabFisica/FBTesteExe).
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public class FBRig : MonoBehaviour
    {
        FBCharacter ch; Animator an; bool built;
        Transform[] spine; Transform lArm, lFore, lHand, rArm, rFore, rHand, rMid;
        GameObject weapon; Transform grip, mag, muzzle; bool pistol;
        Quaternion lPalm = Quaternion.identity, rPalm = Quaternion.identity; bool lPalmOk, rPalmOk; float armL, meta;
        float aimW, ikW, fingW;
        FBRemote remote; float farCheck; bool far;
        // longe da câmera (FBQuality.AnimCullDistance; o "far" do actor.js): sem pós-animação de tronco/mãos; o jogador local nunca
        public bool Far
        {
            get
            {
                if (ch.isPlayer) return false;
                if ((farCheck -= Time.deltaTime) <= 0) { farCheck = .25f + Random.value * .1f; var cam = Camera.main; far = cam && (cam.transform.position - transform.position).sqrMagnitude > FBQuality.AnimCullDistance * FBQuality.AnimCullDistance; }
                return far;
            }
        }
        class Finger { public Transform[] b; public Quaternion[] rest; public bool right; public bool index; public float sg = 1; }
        readonly List<Finger> fingers = new();
        static readonly float[] K = { .85f, 1, .8f };                       // dobra relativa por falange (proximal, média, distal)
        public float AimW => aimW; public float IkW => ikW;
        public float Retreat => retreatS; public float BarrelErr => ErrNow(); public float LeftGap { get; private set; } public bool LeftSec { get; private set; }   // r51: laboratório da pegada (-fbjogo pegada)
        [System.NonSerialized] public bool bypass;                             // testes: só a pose do Animator (mede quanto a pós-animação soma)
        [System.NonSerialized] public Vector3 LeftTarget;                    // último alvo do punho esquerdo (testes / depuração)
        [System.NonSerialized] public Vector3 GripCenter, GripAxis; [System.NonSerialized] public float GripRadius;   // guarda-mão usado (centro, eixo, raio) — testes
        [System.NonSerialized] public bool LeftTargetValid;

        public void Init(FBCharacter c)
        {
            ch = c; an = c.an; if (built || !an || !an.isHuman) return; remote = GetComponent<FBRemote>();
            var sp = new List<Transform>(); foreach (var b in new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest }) { var t = an.GetBoneTransform(b); if (t) sp.Add(t); }
            spine = sp.ToArray();
            Transform B(HumanBodyBones b) => an.GetBoneTransform(b);
            lArm = B(HumanBodyBones.LeftUpperArm); lFore = B(HumanBodyBones.LeftLowerArm); lHand = B(HumanBodyBones.LeftHand);
            rArm = B(HumanBodyBones.RightUpperArm); rFore = B(HumanBodyBones.RightLowerArm); rHand = B(HumanBodyBones.RightHand); rMid = B(HumanBodyBones.RightMiddleProximal);
            lPalmOk = FBHand.Frame(an, false, out lPalm, out _); rPalmOk = FBHand.Frame(an, true, out rPalm, out _);
            // dedos: rotação de descanso = pose do prefab (T-pose do avatar, dedos esticados) — a dobra é absoluta a partir dela, como no web
            foreach (var right in new[] { true, false })
            {
                var palmOk = right ? rPalmOk : lPalmOk; if (!palmOk) continue;
                var hand = right ? rHand : lHand; var N = hand.rotation * ((right ? rPalm : lPalm) * Vector3.up);
                var A = FingerAxis(right); if (A.sqrMagnitude < 1e-8f) continue;
                for (int f = 1; f < 5; f++)
                {
                    var bs = new Transform[3]; bool ok = true;
                    for (int j = 0; j < 3; j++) { bs[j] = B((HumanBodyBones)((int)(right ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal) + 3 * f + j)); ok &= bs[j]; }
                    if (!ok) continue;
                    var fg = new Finger { b = bs, rest = new Quaternion[3], right = right, index = f == 1 };
                    for (int j = 0; j < 3; j++) fg.rest[j] = bs[j].localRotation;
                    // sentido que FECHA para a palma (cada rig tem o seu): dobra um pouco e vê para onde a ponta foi
                    var t0 = Tip(fg); Bend(fg, .3f, A); var t1 = Tip(fg); fg.sg = Vector3.Dot(t1 - t0, N) > 0 ? 1 : -1;
                    for (int j = 0; j < 3; j++) bs[j].localRotation = fg.rest[j];
                    fingers.Add(fg);
                }
            }
            built = true;
        }
        static Vector3 Tip(Finger f) { var d = f.b[2]; return d.position + (d.position - f.b[1].position) * .8f; }
        // eixo de dobra dos dedos: do mínimo para o indicador (nós dos dedos)
        Vector3 FingerAxis(bool right)
        {
            var i = an.GetBoneTransform(right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
            var l = an.GetBoneTransform(right ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal) ?? an.GetBoneTransform(right ? HumanBodyBones.RightRingProximal : HumanBodyBones.LeftRingProximal);
            return i && l ? (i.position - l.position).normalized : Vector3.zero;
        }
        static void Bend(Finger f, float th, Vector3 A)
        {
            for (int j = 0; j < 3; j++) { var b = f.b[j]; b.localRotation = f.rest[j]; b.rotation = Quaternion.AngleAxis(th * K[j] * f.sg * Mathf.Rad2Deg, A) * b.rotation; }
        }
        void Curl(bool right, float th, float thIndex, float w)
        {
            var A = FingerAxis(right); if (A.sqrMagnitude < 1e-8f) return;
            foreach (var f in fingers)
            {
                if (f.right != right) continue; float t = f.index ? thIndex : th; if (t < 0) continue;
                Quaternion a0 = f.b[0].localRotation, a1 = f.b[1].localRotation, a2 = f.b[2].localRotation;
                Bend(f, t, A);
                if (w < .999f) { f.b[0].localRotation = Quaternion.Slerp(a0, f.b[0].localRotation, w); f.b[1].localRotation = Quaternion.Slerp(a1, f.b[1].localRotation, w); f.b[2].localRotation = Quaternion.Slerp(a2, f.b[2].localRotation, w); }
            }
        }

        // r51 (pedido: "alinhamento correto das mãos com o cabo, gatilho e apoio frontal... evite correções genéricas"): indicador no gatilho
        // POR ARMA — a dobra que leva a ponta do indicador ao gatilho medido na animação de 1ª pessoa daquela arma (tpsGrip.r.i[3], espaço do
        // nó da arma). Antes: 0,45 rad fixo em todas (o dedo ficava antes/depois do gatilho conforme a distância cabo→gatilho de cada modelo).
        // A arma é filha da mão, então a dobra é constante por arma e personagem: calcula uma vez (busca de 0 a 1,6 rad) e guarda.
        public static bool TriggerFit = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbgatilhoantigo") < 0;
        readonly Dictionary<string, float> trigBend = new(); public readonly Dictionary<string, float> TrigErr = new();
        float TriggerBend()
        {
            var id = ch.weapon?.id; if (!TriggerFit || id == null || !weapon || pistol && PistolLowReady) return .45f;
            if (trigBend.TryGetValue(id, out var v)) return v;
            Finger fi = null; foreach (var f in fingers) if (f.right && f.index) fi = f;
            var il = Json.L(Json.O(Json.O(GameData.TpsGrip, id), "r"), "i"); var A = FingerAxis(true);
            if (il == null || il.Count < 4 || fi == null || A.sqrMagnitude < 1e-8f) return trigBend[id] = .45f;
            var tgt = Node0(weapon).TransformPoint(GV(il[3]));
            Quaternion a0 = fi.b[0].localRotation, a1 = fi.b[1].localRotation, a2 = fi.b[2].localRotation; float best = .45f, bd = float.MaxValue;
            for (float th = 0; th <= 1.6f; th += .04f) { Bend(fi, th, A); float d = (Tip(fi) - tgt).sqrMagnitude; if (d < bd) { bd = d; best = th; } }
            fi.b[0].localRotation = a0; fi.b[1].localRotation = a1; fi.b[2].localRotation = a2;
            TrigErr[id] = Mathf.Sqrt(bd); return trigBend[id] = best;
        }

        // troca de arma: o IK das mãos recomeça do zero (fuzil → pistola trocava de alvo de uma vez: braço direito girava ~150° num quadro)
        float retreatS;                                                                                   // recuo suavizado da arma longa (RifleLeftHand)
        public void OnWeapon(GameObject w, Transform g, Transform m, Transform mz, bool isPistol)
        {
            // [parceiro 01/11] patrulha e olhar da cabeça também recomeçam do zero na troca de arma
            if (w != weapon || isPistol != pistol) { ikW = 0; retreatS = 0; patW = 0; headW = 0; }
            weapon = w; grip = g; mag = m; muzzle = mz; pistol = isPistol;
        }

        bool Hd => ch.isPlayer || !FBQuality.HdOnlyLocal;                  // FF: só o jogador local com detalhe fino (dedos)
        float S => ch.ScaleK * FBConst.ModelScale / 1.25f;                  // worldScale/1.25 do actor.js

        // ---- 1) mira do tronco
        public void Aim(float dt) { AimSpine(dt); DesvirarTronco(dt); HeadLook(dt); }   // [parceiro] DesvirarTronco depois do AimSpine
        void AimSpine(float dt)
        {
            if (!built || !ch || spine == null || spine.Length == 0 || bypass) return;
            bool spc = ch.Special || !ch.alive || ch.vehiclePose;
            // só na postura de combate (InCombat → CombatLocomotion, arma à frente). Parado fora de combate a base toca a PatrolLocomotion
            // ("Rifle Idle", arma baixa): mirar ali girava a coluna até 22° para trás e os braços até 46° para levantar o cano — o
            // "pescoço jogado para trás" e os ombros/braços torcidos no idle armado
            float want = spc || ch.sprint || !ch.grounded || ch.IsRelaxedPose ? 0 : ch.CombatStance ? 1 : 0;
            aimW = Mathf.Lerp(aimW, want * (ch.HandsBusy ? .3f : 1), 1 - Mathf.Exp(-dt * 8));
            // "low ready" fora de ação (rodada 37; LowerWeapon do LPSP / postura relaxada do puppet): os braços giram para baixo no ombro —
            // o cano desce, a coronha quase não sai do lugar e a mão esquerda continua no guarda-mão (IK depois). Sobe rápido ao entrar em ação.
            float lw = ch.Hot || !ch.CombatStance || spc ? 0 : ch.Resting ? 36 : 20;
            lowerS = Mathf.Lerp(lowerS, lw, 1 - Mathf.Exp(-dt * (lw < lowerS ? 16 : 4)));
            // r51 (pedido: "após sair do veículo o personagem permanece com o corpo inclinado"; exe -fbjogo, armado e câmera alta como na foto do
            // usuário: tronco +17° à frente e ombros rolando +22° com o Animator puro IGUAL ao de antes de entrar). Parado em postura de combate
            // SEM ação (não mirou/atirou/recarregou há 3 s) a coluna seguia o pitch da câmera INTEIRO (até 22° na coluna + 46° nos braços): olhar
            // para baixo — a câmera do veículo fica alta e continua assim na saída — dobrava o corpo, e com o tronco de lado do clipe de arma o giro
            // em volta da direita da câmera virava rolagem dos ombros. Fora de ação o pitch entra 20 % (o rumo do tronco continua inteiro); em
            // ação, inteiro em ~0,1 s. O tiro sai da câmera (FBPlayer.Fire), então a mira não muda. -fbcolunaantiga = antes.
            hotS = Mathf.MoveTowards(hotS, ch.Hot ? 1 : 0, dt * (ch.Hot ? 12 : 2.5f)); float pk = RelaxPitchFix ? Mathf.Lerp(RelaxPitchK, 1, hotS) : 1; PitchK = pk;
            if (aimW < .01f || !ch.weaponGO || !ch.weaponGO.activeSelf || Far) return;
            const float SPINE_PITCH = .38f;
            // eixo do pitch = direita da MIRA (web: (−cos aimYaw, 0, sin aimYaw)); no jogador a câmera vira antes do corpo (0,04–0,14 s) e
            // com a direita do corpo o pitch saía torto durante a virada. Bots/remotos: o corpo já está virado para a mira
            var right = ch.isPlayer && FBPlayer.I ? Quaternion.Euler(0, FBPlayer.I.camYaw, 0) * Vector3.right : transform.right; float spSum = 0, dp0 = float.NaN; int n = spine.Length;
            // pistola (rodada 38): a coluna alinha a FRENTE DO TRONCO com o alvo (postura isósceles) e os braços levam a arma pelo IK
            // (PistolHands). Antes a coluna girava até o CANO apontar — com o cano de lado do clipe de fuzil ela torcia ~50° e a pistola
            // ficava cruzada no peito, com o braço esquerdo atravessado
            for (int it = 0; it < (ch.isPlayer ? 3 : 1); it++)
            {
                if (!(pistol ? TorsoErr(out float dy, out float dp) : AimErr(out dy, out dp))) break;
                dy = Mathf.Clamp(dy, -.87f, .87f) * aimW;
                // r51d: o pk vale sobre o pitch TOTAL — o jogador repete 3×, e 20 % do erro que sobra a cada passada somava ~49 % (exe r51d: tronco
                // +16° e ombros +16° ainda). O erro que deve SOBRAR é (1 − pk) do erro da 1ª passada; com pk = 1 a conta é a de antes.
                if (float.IsNaN(dp0)) dp0 = dp; float dpE = dp - (1 - pk) * dp0;
                float dpS = Mathf.Clamp(Mathf.Clamp(dpE, -.7f, .7f) * aimW, -SPINE_PITCH - spSum, SPINE_PITCH - spSum); spSum += dpS;
                // yaw em volta do "cima" do mundo; pitch para CIMA = ângulo negativo em volta da direita (no Unity +X inclina a frente para baixo)
                var q = Quaternion.AngleAxis(dy / n * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(-dpS / n * Mathf.Rad2Deg, right);
                foreach (var b in spine) b.rotation = q * b.rotation;
            }
            if (!pistol && AimErr(out _, out float dp2) && rArm && lArm)
            {
                float rest = Mathf.Clamp(float.IsNaN(dp0) ? dp2 * pk : dp2 - (1 - pk) * dp0, -.8f, .8f) * aimW;      // braços: o que falta do total permitido
                if (Mathf.Abs(rest) > .01f) { var q = Quaternion.AngleAxis(-rest * Mathf.Rad2Deg, right); rArm.rotation = q * rArm.rotation; lArm.rotation = q * lArm.rotation; }
                if (lowerS > .2f) { var q = Quaternion.AngleAxis(lowerS * aimW, right); rArm.rotation = q * rArm.rotation; lArm.rotation = q * lArm.rotation; }
            }
        }
        float lowerS; public float LowerDeg => lowerS;
        float hotS = 1; public float PitchK { get; private set; } = 1;
        public static bool RelaxPitchFix = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbcolunaantiga") < 0; public static float RelaxPitchK = .2f;
        // olhar com pistola (rodada 39, retorno "na pistola em 3ª pessoa o player fica olhando para o lado"): os clipes de pistola do IE
        // trazem o tronco de lado e a cabeça virada para a frente da arma; depois que a coluna alinha o tronco com o alvo (TorsoErr) a
        // virada do clipe sobrava e a cabeça ia ~45° para o lado. Pescoço e cabeça (metade cada) levam a frente da cabeça (eixo de bind) ao
        // ponto mirado, sem rolar (o "cima" da cabeça fica perto do cima do mundo).
        float headW;
        void HeadLook(float dt)
        {
            var hd = ch ? ch.Head : null; Vector3 hf = default, hu = default;
            // [parceiro] v16: com fuzil também olha (peso CabecaFuzil sem mirar, CabecaMira mirando); pistola continua com peso 1.
            // "quer": com CabecaFuzil e CabecaMira em 0, fuzil não olha nada (igual à v15).
            // [parceiro] v17: só arma de fuzil (Fuzil = não pistola e não explosivo); no RPG a cabeça girava para dentro do tubo
            bool quer = pistol || ((CabecaFuzil > 0 || CabecaMira > 0) && Fuzil);
            bool baseOk = built && !bypass && quer && hd && ch.weaponGO && ch.weaponGO.activeSelf && ch.alive && !ch.Special && !ch.vehiclePose && !Far;
            float alvoW = !baseOk ? 0 : pistol ? 1 : Fuzil ? (ch.aiming ? CabecaMira : CabecaFuzil) : 0;
            bool want = baseOk && (alvoW > .001f || headW >= .01f) && ch.HeadAxes(out hf, out hu);
            headW = Mathf.Lerp(headW, want ? alvoW : 0, 1 - Mathf.Exp(-dt * 8)); if (!want || headW < .01f) return;
            Vector3 dir; var eye = hd.position;
            if (ch.hasAim && (ch.aimPoint - eye).sqrMagnitude > 1) dir = (ch.aimPoint - eye).normalized;
            else if (remote || (remote = GetComponent<FBRemote>())) dir = Quaternion.Euler(-remote.aimPitch, remote.aimYaw, 0) * Vector3.forward;
            else dir = transform.forward;
            var q = Quaternion.FromToRotation(hd.TransformDirection(hf), dir); float a = Quaternion.Angle(Quaternion.identity, q); if (a > 85) q = Quaternion.Slerp(Quaternion.identity, q, 85 / a);
            var half = Quaternion.Slerp(Quaternion.identity, q, .5f * headW); var nk = ch.Neck;
            if (nk) { nk.rotation = half * nk.rotation; hd.rotation = half * hd.rotation; } else hd.rotation = Quaternion.Slerp(Quaternion.identity, q, headW) * hd.rotation;
            var f2 = hd.TransformDirection(hf); var wantUp = Vector3.ProjectOnPlane(Vector3.up, f2); var curUp = Vector3.ProjectOnPlane(hd.TransformDirection(hu), f2);
            if (wantUp.sqrMagnitude > 1e-4f && curUp.sqrMagnitude > 1e-4f) hd.rotation = Quaternion.AngleAxis(Mathf.Clamp(Vector3.SignedAngle(curUp, wantUp, f2), -30, 30) * headW, f2) * hd.rotation;
        }
        // erro de mira do cano (yaw, pitch em rad): alvo = ponto sob a mira; sem ele, a frente do corpo na horizontal
        // punho direito mira (como o bl_PlayerIK do IE, que leva a mão direita ao ponto de mira): o que a coluna não fechou (limite de
        // 22° de pitch, clipe de pistola com o cano de lado) o punho corrige até 60°, e a arma volta a ficar em pé (rolamento até 60°).
        // Antes da mão esquerda — ela pega a arma já no lugar final.
        void AimWrist()
        {
            if (aimW < .01f || !rHand || !ch.weaponGO || !ch.weaponGO.activeSelf) return;
            var bar = ch.BarrelDir(); var mz = ch.MuzzlePos; Vector3 want;
            if (ch.hasAim && (ch.aimPoint - mz).sqrMagnitude > 1) want = (ch.aimPoint - mz).normalized;
            else if (remote) want = Quaternion.Euler(-remote.aimPitch, remote.aimYaw, 0) * Vector3.forward;
            else want = transform.forward;
            if (lowerS > .2f) { var ax = Vector3.Cross(Vector3.up, want); if (ax.sqrMagnitude > 1e-4f) want = Quaternion.AngleAxis(lowerS * aimW, ax.normalized) * want; }   // arma baixa: o punho segue o cano baixado
            var q1 = Quaternion.FromToRotation(bar, want); float a1 = Quaternion.Angle(Quaternion.identity, q1);
            if (a1 > 60) { q1 = Quaternion.Slerp(Quaternion.identity, q1, 60 / a1); DbgClamp++; }   // ie_soldado com pistola sobrava 21–26° com o limite de 35°
            var bar2 = q1 * bar; var top = Vector3.ProjectOnPlane(q1 * TopDir(), bar2); var up = Vector3.ProjectOnPlane(Vector3.up, bar2);
            var q2 = Quaternion.identity;
            if (top.sqrMagnitude > 1e-6f && up.sqrMagnitude > 1e-6f) { float r = Vector3.SignedAngle(top, up, bar2); q2 = Quaternion.AngleAxis(Mathf.Clamp(r, -60, 60), bar2); }
            rHand.rotation = Quaternion.Slerp(Quaternion.identity, q2 * q1, aimW) * rHand.rotation;
        }
        // lado de cima da arma: SOCKET_Scope (acima do cano nas 54 armas) ou o +Y do nó da arma
        Vector3 TopDir()
        {
            if (!scopeT || scopeOf != weapon) { scopeOf = weapon; scopeT = null; if (weapon) foreach (var t in weapon.GetComponentsInChildren<Transform>(true)) if (t.name == "SOCKET_Scope") { scopeT = t; break; } }
            if (scopeT) return scopeT.position - ch.MuzzlePos;
            return FBCharacter.GunAxes(weapon, out var gs, out _, out var gt) ? gs.TransformDirection(gt) : Node0(weapon).up;   // topo pelos eixos certos da arma
        }
        Transform scopeT; GameObject scopeOf;
        // erro da frente do tronco (eixos do próprio tronco, FBCharacter.TorsoAxes) até o ponto mirado — usado com pistola
        bool TorsoErr(out float dy, out float dp)
        {
            dy = dp = 0; ch.TorsoAxes(out _, out var g, out _); if (g.sqrMagnitude < 1e-6f) return false;
            float ty = Mathf.Atan2(transform.forward.x, transform.forward.z), tp = 0; var o = ch.Chest ? ch.Chest.position : transform.position;
            if (ch.hasAim) { var d = ch.aimPoint - o; if (d.sqrMagnitude > 1) { ty = Mathf.Atan2(d.x, d.z); tp = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude); } }
            else if (remote || (remote = GetComponent<FBRemote>())) { ty = remote.aimYaw * Mathf.Deg2Rad; tp = remote.aimPitch * Mathf.Deg2Rad; }
            dy = Mathf.DeltaAngle(Mathf.Atan2(g.x, g.z) * Mathf.Rad2Deg, ty * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            dp = tp - Mathf.Atan2(g.y, new Vector2(g.x, g.z).magnitude); return true;
        }
        bool AimErr(out float dy, out float dp)
        {
            dy = dp = 0; var g = ch.BarrelDir(); if (g.sqrMagnitude < 1e-6f) return false;
            float ty = Mathf.Atan2(transform.forward.x, transform.forward.z), tp = 0;
            if (ch.hasAim) { var d = ch.aimPoint - ch.MuzzlePos; if (d.sqrMagnitude > 1) { ty = Mathf.Atan2(d.x, d.z); tp = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude); } }
            else if (remote || (remote = GetComponent<FBRemote>())) { ty = remote.aimYaw * Mathf.Deg2Rad; tp = remote.aimPitch * Mathf.Deg2Rad; }   // remoto: câmera do dono (FBRemote, graus Unity, + = para cima)
            dy = Mathf.DeltaAngle(Mathf.Atan2(g.x, g.z) * Mathf.Rad2Deg, ty * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            dp = tp - Mathf.Atan2(g.y, new Vector2(g.x, g.z).magnitude); return true;
        }

        // ---- 2) mãos (depois de inclinar/recuo/descanso/deitado: a mão esquerda pega a arma onde ela realmente está)
        public void Hands(float dt)
        {
            LeftTargetValid = false; if (bypass) return;
            if (!built || !ch || !lArm || !lFore || !lHand || !rHand) return;
            if (Far) { DbgFar++; return; }
            bool spc = ch.Special || !ch.alive || ch.vehiclePose, busy = ch.HandsBusy;
            bool wpVis = weapon && weapon.activeSelf;
            bool ikOn = ch.alive && !spc && !busy && !ch.UnarmedLocomotion && wpVis;
            bool wantIK = ikOn && (pistol ? !ch.sprint : true);   // pistola: duas mãos sempre (descanso = low ready); só o sprint fica numa mão
            ikW = Mathf.Lerp(ikW, wantIK ? 1 : 0, 1 - Mathf.Exp(-dt * 12));
            DbgHandsN++; if (ikW > .02f && wpVis) DbgIkN++;                                         // [parceiro] contadores do laboratório (FBLabFisica)
            fingW = Mathf.Lerp(fingW, wpVis && ch.alive && !spc && !busy ? 1 : 0, 1 - Mathf.Exp(-dt * 12));
            if (armL <= 0) { armL = (lFore.position - lArm.position).magnitude + (lHand.position - lFore.position).magnitude; var lm = an.GetBoneTransform(HumanBodyBones.LeftMiddleProximal); meta = lm ? (lm.position - lHand.position).magnitude : .09f * S; }
            // [parceiro] v16: peso da patrulha em todo quadro (some junto com a IK no reload, saque, troca e reação a tiro)
            PatrolPeso(dt, wantIK);
            if (ikW > .02f && wpVis)
            {
                if (pistol) PistolHands(ikW);
                else if (ch.hasAim && aimW > .9f)
                {
                    DbgN++; DbgRaw += ErrNow(); DbgLower += lowerS; AimWrist(); DbgWrist += ErrNow(); Patrol(); ClearBody(ikW); DbgClear += ErrNow(); RifleLeftHand(ikW); DbgLeft += ErrNow();
                    if (TorsoClear && KeepOutOfTorso(ikW)) RifleLeftHand(ikW, follow: true);   // [parceiro] arma saiu do peito: a esquerda volta para ela
                }
                else
                {
                    AimWrist(); Patrol(); ClearBody(ikW); RifleLeftHand(ikW);
                    if (TorsoClear && KeepOutOfTorso(ikW)) RifleLeftHand(ikW, follow: true);
                }
            }
            // mão direita fechada no punho, indicador no gatilho (os clipes de 3ª pessoa trazem a mão aberta)
            if (fingW > .02f && wpVis && Hd) Curl(true, 1f, TriggerBend(), fingW);
        }

        public bool PistolLowReady { get; private set; }
        // pistola com as duas mãos (antes: âncora do SetTwoHandedPistolMode do PuppetAimRig do Complexo, Chest + raiz·(0,10; 0,05; 0,40))
        // RODADA 38 (usuário: "na terceira pessoa as mãos ficam posicionadas incorretamente, causando distorções"): medido na vitrine
        // (-fbpistola) — a âncora do Complexo (Chest + 0,10/0,05/0,40 × humanScale) deixava a pistola colada no esterno, braços dobrados com o
        // antebraço direito atravessado, e a mão esquerda nem alcançava a arma (ficava aberta embaixo). Agora a geometria sai do PRÓPRIO
        // esqueleto: meio dos ombros, comprimento real do braço direito e referencial da mira (baixado no low ready):
        //   mira: isósceles — pistola a 88 % do braço à frente, na linha dos olhos (≈ 12 % do braço acima dos ombros), 4 % à direita;
        //   low ready: 60 % do braço à frente com o referencial 40° para baixo (pistola à frente da barriga, cano para o chão à frente);
        //   mão esquerda envolvendo a direita pelo lado esquerdo do punho; cotovelos para fora e para baixo.
        float armR;
        void PistolHands(float w)
        {
            float lowDeg = ch.CombatStance ? lowerS * aimW : 40; PistolLowReady = lowDeg > 8;
            if (!rArm || !rFore || !lArm) return;
            if (armR <= 0) armR = (rFore.position - rArm.position).magnitude + (rHand.position - rFore.position).magnitude;
            var shM = (rArm.position + lArm.position) * .5f;
            Vector3 aim;
            if (ch.hasAim && (ch.aimPoint - ch.MuzzlePos).sqrMagnitude > 1) aim = (ch.aimPoint - shM).normalized;
            else if (remote) aim = Quaternion.Euler(-remote.aimPitch, remote.aimYaw, 0) * Vector3.forward;
            else aim = transform.forward;
            var flat = Vector3.ProjectOnPlane(aim, Vector3.up); if (flat.sqrMagnitude < 1e-4f) flat = transform.forward; flat.Normalize();
            var rtA = Vector3.Cross(Vector3.up, flat).normalized;
            if (lowDeg > .2f) aim = Quaternion.AngleAxis(lowDeg, rtA) * aim;           // arma baixa: o referencial desce junto com o cano
            var frame = Quaternion.LookRotation(aim, Vector3.ProjectOnPlane(Vector3.up, aim).sqrMagnitude > 1e-4f ? Vector3.up : -flat);
            Vector3 F = frame * Vector3.forward, U = frame * Vector3.up, R = frame * Vector3.right;
            float up = Mathf.Clamp01(1 - lowDeg / 40f);                                   // 1 = mirando, 0 = low ready
            float ext = Mathf.Lerp(.6f, .88f, up) * armR, hgt = Mathf.Lerp(0, .12f, up) * armR, lat = Mathf.Lerp(.02f, .04f, up) * armR;   // low ready no centro: a mão esquerda alcança
            var rT = shM + F * ext + U * hgt + R * lat;
            var rHint = (rArm.position + rT) * .5f + (R * .55f - U * .45f).normalized * .3f * armR;   // cotovelo direito para fora e para baixo
            FBIK.TwoBonePole(rArm, rFore, rHand, rT, rHint, w);
            // rodada 38: a pistola SEMPRE orientada pelo rig — cano no referencial (mira, baixado no low ready), topo para cima. Antes só o
            // AimWrist (que depende do aimW, zero fora de combate): andando, a mão ia ao lugar pelo IK mas a rotação era a do clipe de trote
            // de FUZIL e a pistola apontava para o chão, na altura da virilha, com o pulso torcido
            PistolWrist(F, w);
            AimWrist();                                                                                   // refino no alvo (pelo punho)
            // mão esquerda envolvendo a direita: pelo lado esquerdo do punho, um pouco abaixo e atrás
            var lT = rHand.position - R * .07f * armR - U * .04f * armR - F * .02f * armR;
            var lHint = (lArm.position + lT) * .5f + (-R * .55f - U * .45f).normalized * .3f * armR;
            FBIK.TwoBonePole(lArm, lFore, lHand, lT, lHint, w);
            LeftTarget = lT; LeftTargetValid = true;
            if (lPalmOk)
            {   // palma de apoio: virada para a pistola (lado esquerdo da arma), dedos para baixo/frente abraçando os dedos da mão direita
                var bar = ch.BarrelDir(); var rtB = Vector3.Cross(Vector3.up, bar); if (rtB.sqrMagnitude < 1e-4f) rtB = rtA; rtB.Normalize();
                var N = (rtB * .85f + Vector3.up * .25f - bar * .1f).normalized; var Fg = Vector3.ProjectOnPlane(bar * .45f - Vector3.up * .85f + rtB * .25f, N).normalized;
                if (Fg.sqrMagnitude > .5f) lHand.rotation = Quaternion.Slerp(lHand.rotation, Quaternion.LookRotation(Fg, N) * Quaternion.Inverse(lPalm), w);
                if (Hd) Curl(false, .95f, .9f, w);
            }
        }

        void PistolWrist(Vector3 want, float w)
        {
            if (!rHand || !ch.weaponGO || w < .01f) return;
            var bar = ch.BarrelDir(); var q1 = Quaternion.FromToRotation(bar, want); var bar2 = q1 * bar;
            var top = Vector3.ProjectOnPlane(q1 * TopDir(), bar2); var up = Vector3.ProjectOnPlane(Vector3.up, bar2); var q2 = Quaternion.identity;
            if (top.sqrMagnitude > 1e-6f && up.sqrMagnitude > 1e-6f) q2 = Quaternion.AngleAxis(Vector3.SignedAngle(top, up, bar2), bar2);
            rHand.rotation = Quaternion.Slerp(Quaternion.identity, q2 * q1, w) * rHand.rotation;
        }
        // fuzil fora do tronco: a linha da arma (coronha à boca) não pode entrar na frente do corpo; se entra, a mão direita vai para a frente
        // com IK do braço (a arma é filha da mão) e a esquerda pega o guarda-mão já na posição nova. Corpos largos (AKA JUNIN) e coletes.
        string wbId; Vector3 wbC; float wbHalf, wbThick;
        public static double ClearMs; public static long ClearN;                                          // custo (autoteste)
        void ClearBody(float w) { var sw = System.Diagnostics.Stopwatch.StartNew(); try { ClearBody0(w); } finally { ClearMs += sw.Elapsed.TotalMilliseconds; ClearN++; } }
        void ClearBody0(float w)
        {
            if (!rArm || !rFore || !rHand || !weapon) return; float s = S;
            if (wbId != ch.weapon?.id)
            {
                wbId = ch.weapon?.id; var inv = weapon.transform.worldToLocalMatrix; var bb = new Bounds(); bool first = true;
                // [parceiro] Correção: caixa da arma calculada pelos 8 cantos no espaço da arma (antes a medida podia sair
                // negativa e desligava o afastamento da arma do corpo em algumas armas).
                foreach (var r in weapon.GetComponentsInChildren<Renderer>()) { if (System.Text.RegularExpressions.Regex.IsMatch(r.name, "laser|beam|flash", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) continue; if (!FBCharacter.LocalBoundsOf(r, inv, out var lb)) continue; if (first) { bb = lb; first = false; } else bb.Encapsulate(lb); }
                wbC = bb.center; var barL = weapon.transform.InverseTransformDirection(ch.BarrelDir()).normalized;
                wbHalf = Mathf.Abs(bb.extents.x * barL.x) + Mathf.Abs(bb.extents.y * barL.y) + Mathf.Abs(bb.extents.z * barL.z);
                wbThick = Mathf.Min(bb.extents.x, Mathf.Min(bb.extents.y, bb.extents.z));
            }
            var bar = ch.BarrelDir(); if (bar.sqrMagnitude < 1e-6f) return; float sc = weapon.transform.lossyScale.x;
            var c = weapon.transform.TransformPoint(wbC); float half = wbHalf * sc, pad = wbThick * sc + .015f * s, push = 0;
            // [parceiro] coronha no ombro (31/10, ver OmbroLivre) e pegada da patrulha/desvirar (v16): os pontos da linha da arma no bolso do
            // ombro (ou na frente dele, a 10 cm vezes ScaleK, enquanto a patrulha/desvirar leva a soleira para lá) não contam como peito
            bool ombroOk = Bolso(out var bolsoC, out var maoC, out var barC, out var r2C);
            bool pegOk = (patW > .01f || desvW > .01f) && rArm; var fbC = pegOk ? FrenteBolso() : Vector3.zero; float rk = .1f * ch.ScaleK;
            for (int k = -8; k <= 8; k++)
            {
                var pc = c + bar * (half * k / 8f);
                if (ombroOk && NoBolso(pc, bolsoC, maoC, barC, r2C)) continue;
                if (pegOk && NoBolso(pc, fbC, rHand.position, bar, rk * rk)) continue;
                push = Mathf.Max(push, ch.FrontPenetration(pc, pad));
            }
            // limite (rodada 37): sem teto, arma longa cruzando o corpo empurrava o braço até esticar inteiro ("braços de zumbi", mão
            // esquerda a 40 cm do guarda-mão); o resto da folga sai pela mira/low ready, que já afastam a arma do tronco
            push = Mathf.Min(push, .07f * s); DbgPush += push / s;
            if (push <= .002f * s) return;
            var fw = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            // r43 — CAUSA do cano fora do alvo (laboratório com diagnóstico por etapa, -fbanimlab: cru 0,2° -> punho 0,0° -> ClearBody 15,2°
            // no fuzil e na sniper, 9,4° na SMG): o IK do braço levava a mão para a frente mas a mão (e a arma, filha dela) girava junto com o
            // antebraço — o AimWrist acabava de alinhar o cano e o ClearBody desalinhava. Agora a arma só TRANSLADA: a rotação da mão volta.
            var rq = rHand.rotation;
            FBIK.TwoBonePole(rArm, rFore, rHand, rHand.position + fw * push, rFore.position + Vector3.down * .3f * s + transform.right * .2f * s, w);
            rHand.rotation = rq;
        }

        // ---- [parceiro] arma fora do peito, patrulha, coronha no bolso do ombro, desvirar o tronco (v14–v17, 31/10–01/11/2026)
        Vector3[] wbPts; Renderer wbRend; string wbPtsId;                                     // forma da arma (WeaponPoints), por arma
        public static float DbgTorso;
        // contadores do teste de laboratório (FBLabFisica lê e zera; não mudam o jogo)
        [System.NonSerialized] public int DbgHandsN, DbgIkN, DbgTorsoN, DbgTorsoHit;
        [System.NonSerialized] public float DbgTorsoPen, DbgTorsoResid;
        // só para o laboratório: na 1ª passada, quanto a mão devia andar e quanto andou, para onde (frente/direita/cima do
        // personagem) e onde fica o ponto mais fundo em relação à mão; DbgPhaseKept conta as passadas mantidas por jeito
        [System.NonSerialized] public int DbgPushN;
        [System.NonSerialized] public float DbgPushReq, DbgPushGot;
        [System.NonSerialized] public Vector3 DbgPushDir, DbgDeepRel;
        [System.NonSerialized] public int[] DbgPhaseKept = new int[2];

        // giro extra da arma em volta da mão, no espaço do personagem (ver Carry); DbgCarryAng = maior giro usado
        Quaternion carry = Quaternion.identity; int carryEixo; string carryId; float lastPen; bool carryOk;
        [System.NonSerialized] public float DbgCarryAng;
        [System.NonSerialized] public int DbgCarryN;
        // Carry: fundo (m, vezes ScaleK) a partir do qual a arma começa a girar, e abaixo do qual o giro volta a zero.
        // Só para as classes de CarryClasses (arma comprida que o braço não consegue tirar do peito); fuzil, escopeta e
        // submetralhadora o empurrão da mão já resolve, e girar a arma delas só entortava a pose.
        public static float CarryAlto = .035f, CarryBaixo = .02f;
        public static string CarryClasses = "sniper;explosivo";
        public static float CarryPasso = 2.5f, CarryMax = 50f;                                // graus por quadro e giro máximo

        // Correção: com a arma na mão sem mirar (parado, descansando, andando, correndo), partes da arma ficavam dentro do
        // peito ou da cabeça (o tubo do RPG). O empurrão para a frente do ClearBody tem limite de 7 cm e só olha o eixo do cano.
        // Com TorsoClear, pontos da forma da arma são conferidos contra o corpo medido da malha (FBCharacter.BodyDepth): se
        // algum está dentro do tronco ou da cabeça, a mão direita leva a arma para fora pelo lado mais perto (frente ou lado).
        public static bool TorsoClear = true;
        public static float TorsoMax = .3f;                                                  // maior deslocamento da mão, em metros vezes a escala do personagem (ScaleK)
        public static float TorsoDist = 30f;                                                 // distância da câmera (m) até onde os bots também tiram a arma do peito (e só se aparecem na tela); 0 = todos
        public static double TorsoMs; public static long TorsoCalls;                         // custo (teste do exe)

        // Coronha no ombro (31/10/2026, Ruan: "o jeito que ele segura o fuzil tá estranho ainda"): o ClearBody e o
        // KeepOutOfTorso tratavam a coronha encostada no ombro como arma dentro do peito e levavam a arma para a frente;
        // o boneco ficava segurando o fuzil longe do corpo, com os braços esticados. Agora os pontos da arma que ficam
        // atrás da mão direita e a menos de OmbroLivre (m vezes ScaleK) do "bolso" do ombro direito não contam.
        // 0 = como era (o FBTesteExe compara os dois).
        public static float OmbroLivre = 0f;   // v15: desligado (o teste mostrou só 1-3 cm de ganho e piorou escopeta e RPG)

        // v16, pegada do fuzil (teste "arma" da v15: a pose não mudou; 3 problemas nas fotos e nos números).
        // CabecaFuzil: sem mirar, o clipe deixa o pescoço dobrado e o boneco "olhando a arma" (cano -20°). Com peso > 0,
        // cabeça e pescoço olham para a mira/câmera com esse peso (o mesmo olhar que a pistola já usa). CabecaMira = peso
        // mirando (0 = o clipe manda; o queixo fica em cima da coronha).
        // v17: ligados no jogo (teste "arma" do exe 1033: o olhar para baixo caiu de -36° para -11° e o olhar fora do cano
        // de 13° para 4°, sem a arma entrar mais no corpo). Não vale para o RPG (a cabeça entrava no tubo: 2 -> 69 quadros).
        public static float CabecaFuzil = .7f, CabecaMira = .25f;

        // Patrulha: sem mirar, os clipes de andar/correr levam o fuzil esticado à frente do rosto (cotovelo 161-173°, mão
        // 45-55 cm à frente do peito, coronha solta). Com peso > 0 o cano desce até PatrulhaCano graus abaixo da horizontal
        // (só se estiver acima disso) e a coronha vai para a frente do bolso do ombro direito: a mão direita gira e anda
        // por IK de dois ossos (a arma vai junto), com o cotovelo para baixo e para fora. Não vale para o RPG (vai em cima
        // do ombro). PatrulhaMax = maior deslocamento da mão (m vezes ScaleK).
        // v17: ligada só em movimento (velocidade no chão acima de PatrulhaVel; desliga abaixo de PatrulhaVelFim; o andar
        // normal do jogo é 5 m/s e o analógico pela metade dá 2,5 m/s) e fora da escopeta. No exe 1033: M4 correndo 34 de 75
        // quadros com a arma dentro do peito -> 0, P90 4 -> 0, cano de +3° para -15°. Parado ela esticava o braço (cotovelo
        // 94° -> 156°) e na escopeta a arma entrava no corpo (3 -> 31 quadros).
        public static float Patrulha = 1f, PatrulhaCano = 15f, PatrulhaMax = .3f, PatrulhaVel = 1.5f, PatrulhaVelFim = 1.0f;
        bool patCorre; float patW;

        // Desvirar: parado e mirando, o tronco fica muito de lado em relação ao cano. Gira a coluna até Desvirar graus na
        // direção do cano e devolve a mão direita (e a arma) para onde estava, por IK; a coronha volta para o bolso com o
        // mesmo cano (DesvirarBolso = peso desse ajuste). 0 = desligado.
        public static float Desvirar = 0f, DesvirarBolso = 1f;
        float desvW; bool desvOk; Vector3 desvPos; Quaternion desvRot;
        [System.NonSerialized] public float DbgTronco, DbgPatrulhaMov;

        bool Fuzil => !pistol && ch && ch.weapon != null && ch.weapon.cls != "explosivo";

        // forma da arma carregada uma vez por arma (wbPtsId); false = sem arma ou sem pontos
        bool CarregarPontos()
        {
            if (!weapon) return false;
            if (wbPtsId != ch.weapon?.id || wbPts == null) { wbPtsId = ch.weapon?.id; wbPts = WeaponPoints(weapon); wbRend = weapon.GetComponentInChildren<Renderer>(); }
            return wbPts.Length > 0;
        }

        // ponta de trás da arma (soleira da coronha): o ponto da malha mais para trás ao longo do cano
        Vector3 Soleira(Vector3 bar)
        {
            var m = weapon.transform.localToWorldMatrix; var melhor = weapon.transform.position; float menor = float.MaxValue;
            for (int i = 0; i < wbPts.Length; i++) { var p = m.MultiplyPoint3x4(wbPts[i]); float d = Vector3.Dot(p, bar); if (d < menor) { menor = d; melhor = p; } }
            return melhor;
        }

        // frente do bolso do ombro direito: o mesmo bolso do Bolso()/FBTesteExe (na profundidade da junta do ombro) levado
        // 7 cm para a frente do peito, para a soleira encostar na roupa e não entrar no corpo
        Vector3 FrenteBolso()
        {
            float k = ch.ScaleK; var ombro = rArm.position; var peito = ch.Chest ? ch.Chest.position : ombro;
            ch.TorsoAxes(out _, out var fw, out _); if (fw.sqrMagnitude < 1e-6f) fw = transform.forward;
            return ombro + (peito - ombro) * .3f + Vector3.down * .04f * k + fw.normalized * .07f * k;
        }

        // leva a mão direita (e a arma) para que a soleira fique na frente do bolso do ombro, sem mudar o cano
        void SoleiraNoBolso(float w)
        {
            var bar = ch.BarrelDir(); if (w < .01f || bar.sqrMagnitude < 1e-6f || !CarregarPontos()) return;
            float s = S; var delta = FrenteBolso() - Soleira(bar); float max = PatrulhaMax * ch.ScaleK;
            if (delta.sqrMagnitude > max * max) delta = delta.normalized * max;
            var alvo = rHand.position + delta; var rq = rHand.rotation; var antes = rHand.position;
            FBIK.TwoBonePole(rArm, rFore, rHand, alvo, rFore.position + Vector3.down * .3f * s + transform.right * .2f * s, w, w);
            rHand.rotation = rq;
            DbgPatrulhaMov = Mathf.Max(DbgPatrulhaMov, (rHand.position - antes).magnitude / Mathf.Max(.01f, ch.ScaleK));
        }

        // peso da patrulha, atualizado em todo quadro no Hands() (mesmo quando o ramo da IK não roda): só no "pronto
        // baixo" de verdade (sem mirar, sem atirar e sem os 3 s de combate quente), com as mãos livres (ikOk); no tiro some
        // na hora, para o cano voltar para a linha do tiro
        void PatrolPeso(float dt, bool ikOk)
        {
            float vel = ch.alive ? new Vector2(ch.velocity.x, ch.velocity.z).magnitude : 0;
            patCorre = vel > (patCorre ? PatrulhaVelFim : PatrulhaVel);
            bool on = Patrulha > 0 && Fuzil && ch.weapon.cls != "escopeta" && patCorre && ikOk && !ch.Hot;
            if (ch.shooting) patW = 0;
            patW = Mathf.Lerp(patW, on ? Patrulha : 0, 1 - Mathf.Exp(-dt * (on ? 6 : 14)));
        }

        void Patrol()
        {
            float w = patW * ikW; if (w < .01f || !rArm || !rFore || !rHand || !weapon) return;
            var bar = ch.BarrelDir(); if (bar.sqrMagnitude < 1e-6f) return;
            // cano para baixo até -PatrulhaCano (gira em volta da mão; a soleira sobe e o passo seguinte a leva ao bolso)
            float pitch = Mathf.Asin(Mathf.Clamp(bar.y, -1, 1)) * Mathf.Rad2Deg, desce = pitch + PatrulhaCano;
            var eixo = Vector3.Cross(Vector3.up, bar);
            if (desce > .5f && eixo.sqrMagnitude > 1e-6f) rHand.rotation = Quaternion.AngleAxis(desce * w, eixo.normalized) * rHand.rotation;
            SoleiraNoBolso(w);
        }

        // chamado no Aim(), depois do AimSpine: gira a coluna na direção do cano e guarda onde a mão direita estava
        void DesvirarTronco(float dt)
        {
            desvOk = false;
            // pode = dá para mexer com segurança (some na hora); on = parado/mirando (entra e sai devagar)
            bool pode = Desvirar > 0 && Fuzil && built && !bypass && spine != null && spine.Length > 0 && rArm && rFore && rHand && ch.alive && !ch.Special && !ch.vehiclePose && ch.weaponGO && ch.weaponGO.activeSelf && !Far;
            bool on = pode && !ch.HandsBusy && aimW > .5f && !ch.sprint;
            desvW = pode ? Mathf.Lerp(desvW, on ? 1 : 0, 1 - Mathf.Exp(-dt * 8)) : 0;
            if (!pode || desvW < .01f) return;
            ch.TorsoAxes(out _, out var fw, out _); var bar = ch.BarrelDir();
            var a = Vector3.ProjectOnPlane(fw, Vector3.up); var b = Vector3.ProjectOnPlane(bar, Vector3.up);
            if (a.sqrMagnitude < 1e-6f || b.sqrMagnitude < 1e-6f) return;
            float ang = Vector3.SignedAngle(a, b, Vector3.up); DbgTronco = ang;
            float gira = Mathf.Sign(ang) * Mathf.Min(Desvirar, Mathf.Abs(ang)) * desvW;
            if (Mathf.Abs(gira) < .5f) return;
            desvPos = rHand.position; desvRot = rHand.rotation;
            bool esq = lArm && lFore && lHand; var lPos = esq ? lHand.position : Vector3.zero; var lRot = esq ? lHand.rotation : Quaternion.identity;
            var q = Quaternion.AngleAxis(gira / spine.Length, Vector3.up);
            foreach (var t in spine) t.rotation = q * t.rotation;
            // o pescoço (e a cabeça) giraram junto: volta o giro todo nele, para a cabeça continuar olhando pela arma
            var nk = ch.Neck ? ch.Neck : ch.Head;
            if (nk) nk.rotation = Quaternion.AngleAxis(-gira, Vector3.up) * nk.rotation;
            // e a mão direita volta para onde estava (a arma é filha dela), no mesmo passo: lean, coice e o resto entram
            // por cima depois, pela hierarquia normal. Polo = cotovelo atual (mantém o plano do braço do clipe).
            FBIK.TwoBonePole(rArm, rFore, rHand, desvPos, rFore.position, 1); rHand.rotation = desvRot;
            // a mão esquerda também volta (na mira o RifleLeftHand a recoloca na arma; no reload ela fica no clipe)
            if (esq) { FBIK.TwoBonePole(lArm, lFore, lHand, lPos, lFore.position, 1); lHand.rotation = lRot; }
            // bolso pesado pelo ikW (do quadro anterior): some e volta junto com a IK das mãos, sem salto
            SoleiraNoBolso(DesvirarBolso * desvW * ikW);
            desvOk = true;
        }

        // bolso do ombro: um pouco para dentro (rumo ao peito) e para baixo da junta do ombro direito
        bool Bolso(out Vector3 bolso, out Vector3 mao, out Vector3 bar, out float r2)
        {
            bolso = mao = bar = Vector3.zero; r2 = 0;
            if (OmbroLivre <= 0 || !rArm || !rHand || !ch) return false;
            float k = ch.ScaleK; var ombro = rArm.position; var peito = ch.Chest ? ch.Chest.position : ombro;
            bolso = ombro + (peito - ombro) * .3f + Vector3.down * .04f * k; mao = rHand.position; bar = ch.BarrelDir();
            r2 = OmbroLivre * k * OmbroLivre * k;
            return bar.sqrMagnitude > 1e-6f;
        }
        static bool NoBolso(Vector3 p, Vector3 bolso, Vector3 mao, Vector3 bar, float r2) => Vector3.Dot(p - mao, bar) <= 0 && (p - bolso).sqrMagnitude < r2;

        // depois da mão esquerda: se algum ponto da arma está dentro do tronco ou da cabeça, leva a mão direita (e a arma)
        // para fora pelo lado mais perto. Devolve true se mexeu (aí a mão esquerda é recolocada na arma).
        bool KeepOutOfTorso(float w)
        {
            if (!rArm || !rFore || !rHand || !weapon) return false;
            // longe da câmera ninguém vê a arma encostando no peito: só o jogador e quem está a menos de TorsoDist
            if (!ch.isPlayer && TorsoDist > 0) { var cam = Camera.main; if (cam && (cam.transform.position - rHand.position).sqrMagnitude > TorsoDist * TorsoDist) return false; }
            CarregarPontos();
            // bot fora da tela também não precisa
            if (!ch.isPlayer && TorsoDist > 0 && wbRend && !wbRend.isVisible) return false;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try { return KeepOutOfTorso0(w); } finally { TorsoMs += sw.Elapsed.TotalMilliseconds; TorsoCalls++; }
        }

        // Arma comprida sem mirar (Barrett, RPG correndo): a coronha fica atrás da mão, dentro do peito, e o braço não
        // alcança levar a mão para a frente o bastante. Então a arma gira devagar em volta da mão (alguns graus por quadro,
        // num eixo do personagem por vez: em pé, lado, frente), ficando com o giro que diminui o fundo, até ele cair abaixo
        // de CarryAlto; o empurrão da mão resolve o resto. Abaixo de CarryBaixo o giro volta aos poucos para zero, e
        // mirando ele some rápido para não tirar a mira do lugar. Devolve true se a mão girou.
        bool Carry(bool mira, float k)
        {
            if (carryId != wbPtsId)
            {
                carryId = wbPtsId; carry = Quaternion.identity; var cls = ch.weapon?.cls;
                carryOk = !string.IsNullOrEmpty(cls) && (";" + CarryClasses + ";").Contains(";" + cls + ";");
            }
            if (!carryOk) return false;
            bool zero = carry == Quaternion.identity;
            if (zero && (mira || lastPen < CarryAlto)) return false;
            if (mira) carry = Quaternion.RotateTowards(carry, Quaternion.identity, 400 * Time.deltaTime);
            var rc = transform.rotation; var rc1 = Quaternion.Inverse(rc); var semGiro = rHand.rotation;
            rHand.rotation = rc * carry * rc1 * semGiro;
            if (mira) return carry != Quaternion.identity;
            float d0 = Fundo(); var novo = carry; bool achou = false;
            if (d0 > CarryAlto * k)
            {
                var eixo = carryEixo == 0 ? Vector3.up : carryEixo == 1 ? Vector3.right : Vector3.forward;
                for (int sinal = 1; sinal >= -1 && !achou; sinal -= 2)
                {
                    var q = Quaternion.AngleAxis(sinal * CarryPasso, eixo) * carry;
                    if (Quaternion.Angle(q, Quaternion.identity) > CarryMax) continue;
                    rHand.rotation = rc * q * rc1 * semGiro;
                    if (Fundo() < d0 - .003f * k) { novo = q; achou = true; }
                }
                if (!achou) carryEixo = (carryEixo + 1) % 3;
            }
            else if (d0 < CarryBaixo * k && !zero)
            {
                var q = Quaternion.RotateTowards(carry, Quaternion.identity, CarryPasso);
                rHand.rotation = rc * q * rc1 * semGiro;
                if (Fundo() <= CarryBaixo * k) { novo = q; achou = true; }
            }
            if (achou) carry = novo;
            rHand.rotation = rc * carry * rc1 * semGiro;
            float ang = Quaternion.Angle(carry, Quaternion.identity);
            DbgCarryAng = Mathf.Max(DbgCarryAng, ang); if (ang > .5f) DbgCarryN++;
            return ang > .01f;
        }

        // maior fundo de um ponto da arma dentro do tronco ou da cabeça, do jeito que a arma está agora
        float Fundo()
        {
            Transform hips = ch.Hips, neck = ch.Neck; bool eixoOk = hips && neck; float k = ch.ScaleK, r2 = .45f * k * .45f * k;
            var a = eixoOk ? hips.position : Vector3.zero; var b = eixoOk ? neck.position - a : Vector3.zero; float bb = Mathf.Max(b.sqrMagnitude, 1e-6f);
            var m = weapon.transform.localToWorldMatrix; float best = 0;
            bool ombroOk = Bolso(out var bolsoF, out var maoF, out var barF, out var r2F);
            for (int j = 0; j < wbPts.Length; j++)
            {
                var p = m.MultiplyPoint3x4(wbPts[j]);
                if (eixoOk && (p - (a + b * Mathf.Clamp(Vector3.Dot(p - a, b) / bb, 0, 1.35f))).sqrMagnitude > r2) continue;
                if (ombroOk && NoBolso(p, bolsoF, maoF, barF, r2F)) continue;
                float d = ch.BodyDepthFast(p, out _); if (d > best) best = d;
            }
            return best;
        }

        bool KeepOutOfTorso0(float w)
        {
            if (wbPts.Length == 0 || !ch.BodyFastBegin()) return false;
            float s = S, k = ch.ScaleK; var t = weapon.transform;
            Transform hips = ch.Hips, neck = ch.Neck; bool eixoOk = hips && neck; float r2Eixo = .45f * k * .45f * k;
            bool result = false; DbgTorsoN++;
            bool mira = ch.hasAim && aimW > .5f;
            if (Carry(mira, k)) result = true;
            // cada passada mede todos os pontos e mexe a arma; a passada seguinte confere: se não melhorou, desfaz.
            // Primeiro leva a mão (e a arma) pela direção média dos pontos que entraram (pesada pela profundidade);
            // se o braço não alcança, gira a arma em volta da mão (só sem mirar, para não tirar a mira do lugar).
            int fase = 0; bool conferir = false; float penAnt = 0;
            Vector3 dirAnt = Vector3.zero, somaAnt = Vector3.zero, fundoAnt = Vector3.zero;
            Quaternion qArm = Quaternion.identity, qFore = Quaternion.identity, qHand = Quaternion.identity;
            for (int i = 0; i < 7; i++)
            {
                float pen = 0; Vector3 dir = Vector3.zero, soma = Vector3.zero, fundoP = Vector3.zero;
                var a = eixoOk ? hips.position : Vector3.zero; var b = eixoOk ? neck.position - a : Vector3.zero; float bb = Mathf.Max(b.sqrMagnitude, 1e-6f);
                var m = t.localToWorldMatrix;
                bool ombroOk = Bolso(out var bolsoK, out var maoK, out var barK, out var r2K);
                for (int j = 0; j < wbPts.Length; j++)
                {
                    var p = m.MultiplyPoint3x4(wbPts[j]);
                    // longe do eixo quadril-pescoço não precisa conferir (a cabeça fica dentro desse raio)
                    if (eixoOk && (p - (a + b * Mathf.Clamp(Vector3.Dot(p - a, b) / bb, 0, 1.35f))).sqrMagnitude > r2Eixo) continue;
                    // coronha encostada no ombro é o jeito certo de segurar: não conta como dentro do peito
                    if (ombroOk && NoBolso(p, bolsoK, maoK, barK, r2K)) continue;
                    float d = ch.BodyDepthFast(p, out var outDir);
                    if (d > 0) soma += outDir * d;
                    if (d > pen) { pen = d; dir = outDir; fundoP = p; }
                }
                if (i == 0) { lastPen = pen / k; DbgTorsoPen = Mathf.Max(DbgTorsoPen, pen / k); if (pen > .01f * k) DbgTorsoHit++; }
                if (conferir)
                {
                    conferir = false;
                    if (pen > penAnt - .002f * k)
                    {
                        // não melhorou: volta ao que era e passa para o próximo jeito
                        rArm.rotation = qArm; rFore.rotation = qFore; rHand.rotation = qHand;
                        pen = penAnt; dir = dirAnt; soma = somaAnt; fundoP = fundoAnt;
                        fase++; if (fase == 1 && mira) fase = 2;
                        if (fase >= 2) { DbgTorsoResid = Mathf.Max(DbgTorsoResid, pen / k); return result; }
                    }
                    else { result = true; DbgPhaseKept[Mathf.Min(fase, 1)]++; }
                }
                if (pen <= .005f * k) return result;
                if (i == 6) { DbgTorsoResid = Mathf.Max(DbgTorsoResid, pen / k); return result; }
                var saida = soma.sqrMagnitude > 1e-8f ? soma.normalized : dir;
                penAnt = pen; dirAnt = dir; somaAnt = soma; fundoAnt = fundoP;
                qArm = rArm.rotation; qFore = rFore.rotation; qHand = rHand.rotation;
                if (fase == 0)
                {
                    float passo = Mathf.Min(pen / Mathf.Max(.5f, Vector3.Dot(saida, dir)) + .02f * k, TorsoMax * k);
                    DbgTorso += passo / k; var h0 = rHand.position;
                    FBIK.TwoBonePole(rArm, rFore, rHand, rHand.position + saida * passo, rFore.position + Vector3.down * .3f * s + transform.right * .2f * s, w);
                    rHand.rotation = qHand;
                    if (i == 0)
                    {
                        var tr = transform; DbgPushN++; DbgPushReq += passo / k; DbgPushGot += Vector3.Dot(rHand.position - h0, saida) / k;
                        DbgPushDir += new Vector3(Vector3.Dot(saida, tr.right), Vector3.Dot(saida, tr.up), Vector3.Dot(saida, tr.forward));
                        var rel = fundoP - h0; DbgDeepRel += new Vector3(Vector3.Dot(rel, tr.right), Vector3.Dot(rel, tr.up), Vector3.Dot(rel, tr.forward)) / k;
                    }
                }
                else
                {
                    var rel = fundoP - rHand.position; var axis = Vector3.Cross(rel, saida);
                    if (axis.sqrMagnitude < 1e-6f * k * k) { DbgTorsoResid = Mathf.Max(DbgTorsoResid, pen / k); return result; }
                    float ang = Mathf.Min(Mathf.Atan2(pen + .02f * k, Mathf.Max(rel.magnitude, .05f * k)) * Mathf.Rad2Deg, 30) * w;
                    Quaternion qa = Quaternion.AngleAxis(ang, axis), qb = Quaternion.AngleAxis(-ang, axis);
                    // gira para o lado que mais leva o ponto fundo para fora
                    rHand.rotation = (Vector3.Dot(qa * rel - rel, saida) >= Vector3.Dot(qb * rel - rel, saida) ? qa : qb) * qHand;
                }
                conferir = true;
            }
            return result;
        }

        // pontos que representam a forma da arma (no espaço da arma): os vértices das malhas agrupados numa grade grossa,
        // um ponto por célula ocupada (uns 60 a 150 por arma). Sem malha legível, usa uma grade 3x3x3 de cada caixa.
        static Vector3[] WeaponPoints(GameObject w)
        {
            var list = new List<Vector3>(); var inv = w.transform.worldToLocalMatrix;
            foreach (var r in w.GetComponentsInChildren<Renderer>())
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(r.name, "laser|beam|flash", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) continue;
                Mesh mesh = null;
                if (r is SkinnedMeshRenderer smr) mesh = smr.sharedMesh; else if (r.TryGetComponent<MeshFilter>(out var mf)) mesh = mf.sharedMesh;
                if (!mesh) continue;
                var M = inv * r.transform.localToWorldMatrix;
                if (mesh.isReadable) { foreach (var v in mesh.vertices) list.Add(M.MultiplyPoint3x4(v)); continue; }
                var mb = mesh.bounds;
                for (int i = 0; i < 27; i++) list.Add(M.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3(i % 3 - 1, i / 3 % 3 - 1, i / 9 - 1))));
            }
            if (list.Count == 0) return System.Array.Empty<Vector3>();
            var bb = new Bounds(list[0], Vector3.zero); foreach (var p in list) bb.Encapsulate(p);
            float cel = Mathf.Max(1e-5f, Mathf.Max(bb.size.x, Mathf.Max(bb.size.y, bb.size.z)) / 20f);
            var grade = new Dictionary<Vector3Int, Vector4>();
            foreach (var p in list) { var key = Vector3Int.FloorToInt(p / cel); grade.TryGetValue(key, out var acc); grade[key] = acc + new Vector4(p.x, p.y, p.z, 1); }
            var pts = new Vector3[grade.Count]; int n = 0;
            foreach (var acc in grade.Values) pts[n++] = new Vector3(acc.x, acc.y, acc.z) / acc.w;
            return pts;
        }

        // mão esquerda no guarda-mão do fuzil (rifleLeftHand do actor.js; o corte da malha do guarda-mão vira o cilindro de 2,4 cm do SOCKET_Grip)
        float centerS; public float CenterShift { get; private set; }                       // arma curta trazida para o meio (m, por escala)
        // r42 — chaves de diagnóstico (-fbrigmodo N, bits): 1 = centralizar arma curta, 2 = mão corre até 20 cm, 4 = erguer ≤ 4°. 0 = como era
        public static int Mode = 7, LiftN, CenterN; public static float LiftSum;
        // r43 — diagnóstico do erro de mira (laboratório): ângulo do cano até o alvo antes do punho, depois do punho, depois do ClearBody e no fim
        public static int DbgN, DbgClamp, DbgFar; public static float DbgRaw, DbgWrist, DbgClear, DbgLeft, DbgLower, DbgPush;
        float ErrNow() { var bar = ch.BarrelDir(); var mz = ch.MuzzlePos; return ch.hasAim && (ch.aimPoint - mz).sqrMagnitude > 1 ? Vector3.Angle(bar, ch.aimPoint - mz) : 0; }
        [RuntimeInitializeOnLoadMethod] static void ModeArg() { var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbrigmodo"); if (i >= 0 && i + 1 < a.Length) Mode = int.Parse(a[i + 1]); }
        // [parceiro] follow: segunda passada depois do KeepOutOfTorso, só leva a mão esquerda até a arma (sem recuar, centralizar
        // nem girar a arma, que puxariam a arma de volta para dentro do corpo)
        void RifleLeftHand(float w, bool follow = false)
        {
            float s = S; var wt = weapon.transform; var bar = ch.BarrelDir();
            // referencial da própria arma (o "cima" da arma, não o do mundo), no nó da arma (weaponScene do web)
            var n0 = Node0(weapon); var barL = n0.InverseTransformDirection(bar).normalized;
            var upL = Vector3.ProjectOnPlane(Vector3.up, barL).normalized; var rightL = Vector3.Cross(upL, barL).normalized;   // Unity: Cross(cima, frente) = direita
            var upG = n0.TransformDirection(upL).normalized; var right = n0.TransformDirection(rightL).normalized;
            var N = (upG * .72f + right * .69f).normalized;                                          // normal da palma: 45° para cima e para a direita
            var F = (Vector3.Cross(N, bar) + bar * .25f).normalized;                                 // dedos (web: cross(bar, N) no three → ordem trocada)
            // guarda-mão: corte real da malha onde a mão esquerda fica na animação de 1ª pessoa (elipse); sem ele, cilindro de 2,4 cm no SOCKET_Grip
            Vector3 C0; float e, kMax; System.Func<Vector3, bool> inside;
            var hg = Section(n0, barL, upL, rightL);
            if (hg != null)
            {
                float sc = n0.lossyScale.x, hx = hg.hx * sc + .003f * s, hy = hg.hy * sc + .003f * s;
                C0 = n0.TransformPoint(barL * hg.s + rightL * hg.cx + upL * hg.cy); e = 1 / Mathf.Sqrt((.69f / hx) * (.69f / hx) + (.72f / hy) * (.72f / hy)); kMax = (Mode & 2) != 0 ? .2f * s : .05f * s;   // r42: era 5 cm — ver abaixo
                var c0 = C0; var ri = right; var up = upG;
                inside = p => { var d = p - c0; float x = Vector3.Dot(d, ri) / hx, y = Vector3.Dot(d, up) / hy; return x * x + y * y <= 1; };
            }
            else
            {
                float R = .024f * s;
                var gp = grip ? grip.position : muzzle ? muzzle.position - upG * .035f * s : rHand.position + bar * .35f * s;
                float back = .3f * s; if (mag) back = Mathf.Max(0, Vector3.Dot(gp - mag.position, bar) - .1f * s);
                C0 = gp + upG * R - bar * (back * .35f); e = R; kMax = (Mode & 2) != 0 ? Mathf.Max(back * .65f, .12f * s) : back * .65f;   // r42: SMG segura no carregador se precisar
                var c0 = C0; var b0 = bar; float lim = R + .004f * s;
                inside = p => { var d = p - c0; return (d - b0 * Vector3.Dot(d, b0)).magnitude <= lim; };
            }
            Vector3 WristAt(float k) => C0 - bar * k - N * (e + .016f * s) - F * (.72f * meta);   // palma (perto dos nós) encostada
            var sh = lArm.position; float reach = armL * .93f;                                        // cotovelo sempre um pouco dobrado
            var W = WristAt(0);
            float need = 0;
            if (!follow && (W - sh).magnitude > reach && (WristAt(kMax) - sh).magnitude > reach)
            {
                float rMax0 = .1f * s; need = rMax0;
                if ((WristAt(kMax) - bar * rMax0 - sh).magnitude <= reach) { float lo = 0, hi = rMax0; for (int i = 0; i < 8; i++) { float m = (lo + hi) / 2; if ((WristAt(kMax) - bar * m - sh).magnitude <= reach) hi = m; else lo = m; } need = hi; }
            }
            // suavizado: ligar/desligar o recuo quadro a quadro estalava o braço (escopeta atirando: 6.000°/s)
            if (!follow) retreatS = Mathf.Lerp(retreatS, need, 1 - Mathf.Exp(-Time.deltaTime * (need > retreatS ? 20 : 6)));
            if (!follow && retreatS > .001f * s && rArm && rFore)
            {
                // arma longa (escopeta, sniper): antes de qualquer giro, RECUA a arma ao longo do cano (a mão direita por IK, a arma é filha
                // dela) até 10 cm — a coronha encosta mais no ombro e o cano continua no alvo. Antes o único recurso era erguer a arma até 14°
                // em volta da mão direita: a escopeta ficava 14,7° fora da mira e a sniper 4–13° (laboratório da rodada 37).
                float rr = retreatS; var h0 = rHand.position; var rq = rHand.rotation;
                FBIK.TwoBonePole(rArm, rFore, rHand, h0 - bar * rr, rFore.position + Vector3.down * .3f * s + transform.right * .2f * s, w);
                rHand.rotation = rq; var shift = rHand.position - h0;
                if (shift.sqrMagnitude > 1e-8f) { C0 += shift; var inn = inside; inside = p => inn(p - shift); }
                W = WristAt(0);
            }
            // ARMA CURTA (rodada 42 — laboratório: SMG/P90 com o cano 14° acima do alvo, cotovelos para o alto e a mão esquerda na altura do
            // rosto em TODOS os personagens; o usuário: "ao segurar determinadas armas, os braços ficam completamente deformados"). Na P90 o
            // guarda-mão fica a 15 cm da mão direita (AK: 31 cm), do lado direito do peito: a mão esquerda tinha de cruzar o corpo, não
            // alcançava, e o único recurso que sobrava era ERGUER a arma até 14°. Quem atira de verdade com arma compacta faz o contrário:
            // traz a arma para o meio do peito. Aqui a mão direita (IK do braço; a arma é filha dela) leva a arma em direção à linha do
            // ombro esquerdo, perpendicular ao cano (o cano continua no alvo), até 14 cm e suavizado como o recuo. Erguer fica de último recurso.
            float needC = 0; var latC = Vector3.zero;
            if (!follow && (Mode & 1) != 0 && (W - sh).magnitude > reach && rArm && rFore)
            {
                latC = Vector3.ProjectOnPlane(sh - W, bar); latC -= Vector3.up * Vector3.Dot(latC, Vector3.up) * .7f;   // quase só de lado
                if (latC.sqrMagnitude > 1e-8f) { latC.Normalize(); needC = Mathf.Min(((W - sh).magnitude - reach) * 1.15f + .01f * s, .14f * s); }
            }
            if (!follow) centerS = Mathf.Lerp(centerS, needC, 1 - Mathf.Exp(-Time.deltaTime * (needC > centerS ? 18 : 6)));
            if (!follow && centerS > .001f * s && latC.sqrMagnitude > .5f)
            {
                var h0 = rHand.position; var rq = rHand.rotation;
                CenterN++; FBIK.TwoBonePole(rArm, rFore, rHand, h0 + latC * centerS, rFore.position + Vector3.down * .3f * s + transform.right * .2f * s, w);
                rHand.rotation = rq; var shift = rHand.position - h0;
                if (shift.sqrMagnitude > 1e-8f) { C0 += shift; var inn = inside; inside = p => inn(p - shift); }
                W = WristAt(0);
            }
            CenterShift = centerS / Mathf.Max(1e-4f, s);
            if ((W - sh).magnitude > reach)
            {
                var Wb = WristAt(kMax);
                if ((Wb - sh).magnitude <= reach) { float lo = 0, hi = kMax; for (int i = 0; i < 8; i++) { float m = (lo + hi) / 2; if ((WristAt(m) - sh).magnitude <= reach) hi = m; else lo = m; } W = WristAt(hi); }
                else
                {
                    // nem recuando alcança: ergue a arma em volta da palma direita só o necessário — no máximo 4° (r42). Laboratório da rodada 42:
                    // escopeta 10–13°, sniper 13–17° e SMG 10–15° de erro de mira em TODOS os personagens (fuzil e pistola: 0°): o ponto de apoio
                    // dessas armas fica além do braço dos r_* e a mão só podia correr 5 cm pelo guarda-mão — sobrava erguer a arma 14° (cano
                    // fora do alvo, cotovelo esquerdo lá em cima). Agora a mão corre até 20 cm para trás pelo guarda-mão (como quem segura
                    // arma longa) e a arma quase não sobe.
                    W = Wb; var c = rMid ? Vector3.Lerp(rHand.position, rMid.position, .5f) : rHand.position;
                    Vector3 v = W - c, u = sh - c, ax = Vector3.Cross(v, u);
                    if (!follow && ax.sqrMagnitude > 1e-8f)
                    {
                        ax.Normalize(); float lo = 0, hi = Mathf.Min((Mode & 4) != 0 ? .07f : .25f, Vector3.Angle(v, u) * Mathf.Deg2Rad);
                        for (int i = 0; i < 10; i++) { float m = (lo + hi) / 2; if ((c + Quaternion.AngleAxis(m * Mathf.Rad2Deg, ax) * v - sh).magnitude <= reach) hi = m; else lo = m; }
                        var q = Quaternion.AngleAxis(hi * w * Mathf.Rad2Deg, ax); LiftN++; LiftSum += hi * Mathf.Rad2Deg;
                        wt.SetPositionAndRotation(c + q * (wt.position - c), q * wt.rotation);
                        W = c + q * v; bar = q * bar; F = q * F; N = q * N; C0 = c + q * (C0 - c);
                        var inner = inside; var cq = c; var qi = Quaternion.Inverse(q); inside = p => inner(cq + qi * (p - cq));   // o guarda-mão subiu junto
                    }
                }
            }
            // cotovelo para baixo e para fora no referencial do CORPO
            var pole = sh - transform.right * .35f * s + Vector3.down * .6f * s - transform.forward * .05f * s;
            FBIK.TwoBonePole(lArm, lFore, lHand, W, pole, w);
            LeftGap = (lHand.position - W).magnitude; LeftSec = hg != null;
            LeftTarget = W; LeftTargetValid = true; GripCenter = C0; GripAxis = bar; GripRadius = e;
            if (lPalmOk)
            {
                // palma pelo referencial anatômico; metade da torção vai para o antebraço (sem "embrulho de bala" no punho)
                var before = lHand.rotation; var after = Quaternion.Slerp(before, Quaternion.LookRotation(F, N) * Quaternion.Inverse(lPalm), w);
                var axis = (lHand.position - lFore.position).normalized; var d = after * Quaternion.Inverse(before);
                var proj = Vector3.Project(new Vector3(d.x, d.y, d.z), axis); var tw = new Quaternion(proj.x, proj.y, proj.z, d.w);
                float mag2 = Mathf.Sqrt(tw.x * tw.x + tw.y * tw.y + tw.z * tw.z + tw.w * tw.w);
                if (mag2 > 1e-6f) { tw = new Quaternion(tw.x / mag2, tw.y / mag2, tw.z / mag2, tw.w / mag2); lFore.rotation = Quaternion.Slerp(Quaternion.identity, tw, .5f) * lFore.rotation; }
                lHand.rotation = after;
            }
            if (Hd) Wrap(inside, w);                                                                  // dedos abraçando o guarda-mão
        }

        // dedos da mão esquerda (indicador..mínimo): cada um parte reto e fecha junta por junta até a ponta encostar no guarda-mão
        // (wrapFingers do actor.js); sem contato fica a pegada natural (~63° por junta); polegar como na animação
        void Wrap(System.Func<Vector3, bool> inside, float w)
        {
            var A = FingerAxis(false); if (A.sqrMagnitude < 1e-8f) return;
            foreach (var f in fingers)
            {
                if (f.right) continue;
                Quaternion a0 = f.b[0].localRotation, a1 = f.b[1].localRotation, a2 = f.b[2].localRotation;
                bool Touch(float th) { Bend(f, th, A); return inside(Tip(f)); }
                float lo = 0, hi = 1.1f;
                for (float th = .15f; th <= 1.5f; th += .15f) if (Touch(th)) { hi = th; lo = th - .15f; break; }
                for (int i = 0; i < 4; i++) { float m = (lo + hi) / 2; if (Touch(m)) hi = m; else lo = m; }
                Bend(f, Mathf.Max(.8f, Mathf.Min(1.25f, hi)), A);
                if (w < .999f) { f.b[0].localRotation = Quaternion.Slerp(a0, f.b[0].localRotation, w); f.b[1].localRotation = Quaternion.Slerp(a1, f.b[1].localRotation, w); f.b[2].localRotation = Quaternion.Slerp(a2, f.b[2].localRotation, w); }
            }
        }
        static Transform Node0(GameObject w) { var t = w.transform; return t.childCount == 1 && !t.GetComponent<Renderer>() ? t.GetChild(0) : t; }   // weaponScene do web
        // corte transversal da arma na altura da mão esquerda (vértices numa fatia de ~1,5 cm), no espaço do nó da arma — uma vez por arma.
        // Estação = centro da palma esquerda na animação de 1ª pessoa (tpsGrip.l, espaço do glTF → x espelhado). Malha não legível → cilindro.
        class Sec { public float s, cx, cy, hx, hy; }
        static readonly Dictionary<string, Sec> sections = new();
        static readonly System.Text.RegularExpressions.Regex SKIP = new("magazine|^mag$|pmag|balas|drum|_mag|Mag_|P_LPSP_WEP_ATT|SM_ATT_|Scope|Laser|Flashlight|Silencer|Grip_|Bipod|holosun|calimator|Render_Camera|lens|glass", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        static Vector3 GV(object o) { var l = o as List<object>; return l != null && l.Count >= 3 ? new Vector3(-Json.Fv(l[0]), Json.Fv(l[1]), Json.Fv(l[2])) : Vector3.zero; }
        Sec Section(Transform n0, Vector3 barL, Vector3 upL, Vector3 rightL)
        {
            var id = ch.weapon?.id; if (id == null) return null; if (sections.TryGetValue(id, out var v)) return v;
            var L = Json.O(Json.O(GameData.TpsGrip, id), "l"); var ml = Json.L(L, "m"); if (L == null || ml == null || ml.Count == 0) return sections[id] = null;
            var S = Vector3.Lerp(GV(L["w"]), GV(ml[0]), .6f); float sS = Vector3.Dot(S, barL), sx = Vector3.Dot(S, rightL), sy = Vector3.Dot(S, upL);
            float x0 = 1e9f, x1 = -1e9f, y0 = 1e9f, y1 = -1e9f; int n = 0; var inv = n0.worldToLocalMatrix;
            foreach (var mf in n0.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = mf.sharedMesh; if (!mesh || !mesh.isReadable) continue; bool skip = false;
                for (var q = mf.transform; q && q != n0.parent; q = q.parent) if (SKIP.IsMatch(q.name)) { skip = true; break; }
                var mr = mf.GetComponent<Renderer>(); if (skip || !mr || (mr.sharedMaterial && mr.sharedMaterial.renderQueue >= 3000)) continue;
                var M = inv * mf.transform.localToWorldMatrix;
                foreach (var vv in mesh.vertices)
                {
                    var p = M.MultiplyPoint3x4(vv); float x = Vector3.Dot(p, rightL), y = Vector3.Dot(p, upL);
                    if (Mathf.Abs(Vector3.Dot(p, barL) - sS) > .012f || new Vector2(x - sx, y - sy).magnitude > .09f) continue;   // só perto da mão
                    if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; n++;
                }
            }
            return sections[id] = n >= 6 ? new Sec { s = sS, cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, hx = Mathf.Max(.01f, (x1 - x0) / 2), hy = Mathf.Max(.01f, (y1 - y0) / 2) } : null;
        }
        public bool HasSection => ch && ch.weapon != null && sections.TryGetValue(ch.weapon.id, out var v) && v != null;
    }
}
