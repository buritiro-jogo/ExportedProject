// Rodada 57 (05/11/2026) — rig de combate novo: KINEMATION FPS Animation Framework (Assets/KINEMATION) rodando POR CIMA
// do Animator que já existe, em terceira pessoa.
//
// O que o pacote é, de fato: ele NÃO traz animação, arma nem personagem — são 108 scripts de camadas procedurais (mira,
// balanço, coice, IK, olhar, arma encostando na parede). Quem toca os clipes continua sendo o AnimatorOverrideController
// do FBCharacter; aqui só entra o acabamento, depois que o Animator já escreveu a pose.
//
// Por que NÃO usamos o FPSAnimator do pacote: ele exige um IPlayablesController, ou seja, tomaria conta da reprodução dos
// clipes — isso jogaria fora os 56 encaixes do FBAnimProfile, os bots, os modos e os eventos. O FPSBoneController, que é
// quem aplica as camadas, roda sozinho: precisa só de um KRigComponent e de um perfil.
//
// Ordem (a armadilha que derrubou o Animation Rigging na r42, ver o cabeçalho do FBRig): as camadas têm de rodar DEPOIS
// do LateUpdate que posiciona a arma e DEPOIS do FBRig, senão a mão vai para onde a arma ainda não está. Por isso o
// FBCharacter chama Avaliar() no fim do LateUpdate, nunca antes.
//
// Enquanto o perfil não existir (Resources/FBFpsaf/Perfil_TPS), tudo aqui é inerte: o jogo fica exatamente como era.
// A chave está nos ajustes (FBSession.rigNovo) e vale só para quem NÃO é policial — os policiais seguem no rig clássico.
using UnityEngine;
using KINEMATION.KAnimationCore.Runtime.Rig;
using KINEMATION.KAnimationCore.Runtime.Input;
using KINEMATION.FPSAnimationFramework.Runtime.Core;

namespace FreeBala
{
    public class FBFpsaf : MonoBehaviour
    {
        public const string PerfilPath = "FBFpsaf/Perfil_TPS", EntradaPath = "FBFpsaf/Entrada_TPS";

        static FPSAnimatorProfile perfil; static UserInputConfig entrada; static bool procurou;
        /// <summary>true quando os assets do rig novo existem no projeto (sem eles o jogo roda no rig clássico)</summary>
        public static bool Pronto
        {
            get
            {
                if (!procurou)
                {
                    procurou = true;
                    perfil = Resources.Load<FPSAnimatorProfile>(PerfilPath);
                    entrada = Resources.Load<UserInputConfig>(EntradaPath);
                    if (!perfil) Debug.Log("[FreeBala][r57] rig novo sem perfil (" + PerfilPath + "): seguindo no rig clássico");
                }
                return perfil && entrada;
            }
        }
        /// <summary>quem usa o rig novo: a chave dos ajustes, personagem que não é policial e os assets no lugar</summary>
        public static bool Vale(FBCharacter c) => c && FBSession.rigNovo && !c.IsPolice && Pronto;

        FBCharacter ch; KRigComponent rigc; FPSBoneController bones; UserInputController input;
        bool ok; int pIsAiming = -1, pAimW = -1, pMove = -1, pMouse = -1, pMouseD = -1, pLean = -1, pPlayW = -1;
        Vector3 olhoAnt;
        KVirtualElement vLeftIk, vLeftHint, vRightIk, vRightHint, vWeapon, vWeaponIk;
        Transform bLeftHand, bLeftFore, bRightHand, bRightFore;
        Vector3 desvioP; Quaternion desvioQ = Quaternion.identity;   // quanto as camadas de arma mexeram no quadro passado

        public static FBFpsaf Criar(FBCharacter c)
        {
            if (!Vale(c)) return null;
            var f = c.gameObject.AddComponent<FBFpsaf>();
            f.Init(c);
            return f.ok ? f : null;
        }

        void Init(FBCharacter c)
        {
            ch = c;
            if (!ch || !ch.an) return;
            // O KRigComponent varre a hierarquia de ossos, mas o RefreshHierarchy do pacote é #if UNITY_EDITOR: a lista de ossos
            // é serializada no prefab, não montada em tempo de jogo. Por isso o componente vai ter de entrar nos prefabs dos
            // personagens no FBCharImport (junto com o KRig, passo 1 da RODADA57.md). Até lá, sem lista = fica inerte.
            rigc = ch.an.GetComponent<KRigComponent>() ?? ch.an.gameObject.AddComponent<KRigComponent>();
#if UNITY_EDITOR
            rigc.RefreshHierarchy();
#endif
            var ossos = rigc.GetRigTransforms();   // GetHierarchy/RefreshHierarchy são #if UNITY_EDITOR; este é de tempo de jogo
            if (ossos == null || ossos.Length == 0) return;

            input = gameObject.GetComponent<UserInputController>() ?? gameObject.AddComponent<UserInputController>();
            input.inputConfig = entrada;
            input.Initialize();

            bones = gameObject.GetComponent<FPSBoneController>() ?? gameObject.AddComponent<FPSBoneController>();
            bones.Initialize();
            bones.LinkAnimatorProfile(perfil);

            pIsAiming = input.GetPropertyIndex(FPSANames.IsAiming);
            pAimW = input.GetPropertyIndex(FPSANames.AimingWeight);
            pMove = input.GetPropertyIndex(FPSANames.MoveInput);
            pMouse = input.GetPropertyIndex(FPSANames.MouseInput);
            pMouseD = input.GetPropertyIndex(FPSANames.MouseDeltaInput);
            pLean = input.GetPropertyIndex(FPSANames.LeanInput);
            pPlayW = input.GetPropertyIndex(FPSANames.PlayablesWeight);

            KVirtualElement V(string n) { var t = ch.an.transform.Find(n); return t ? t.GetComponent<KVirtualElement>() : null; }
            vLeftIk = V("IK LeftHand"); vLeftHint = V("IK LeftElbow"); vRightIk = V("IK RightHand"); vRightHint = V("IK RightElbow");
            vWeapon = V("WeaponBone"); vWeaponIk = V("IK WeaponBone");
            if (vLeftIk == null || vRightIk == null) return;               // prefab ainda sem os ossos virtuais (FBFpsafSetup)
            bLeftHand = ch.an.GetBoneTransform(HumanBodyBones.LeftHand); bLeftFore = ch.an.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            bRightHand = ch.an.GetBoneTransform(HumanBodyBones.RightHand); bRightFore = ch.an.GetBoneTransform(HumanBodyBones.RightLowerArm);
            Soltar();
            ok = true;
        }

        // sem arma na mão: cada alvo de IK vira o próprio osso, então o IK chega resolvido e não mexe em nada
        void Soltar()
        {
            if (vLeftIk) vLeftIk.targetBone = bLeftHand; if (vLeftHint) vLeftHint.targetBone = bLeftFore;
            if (vRightIk) vRightIk.targetBone = bRightHand; if (vRightHint) vRightHint.targetBone = bRightFore;
            if (vWeapon) vWeapon.targetBone = bRightHand;
            if (vWeaponIk) vWeaponIk.targetBone = bRightHand;
            desvioP = Vector3.zero; desvioQ = Quaternion.identity;
        }

        /// <summary>chamado pelo FBCharacter quando a arma muda</summary>
        public void OnWeapon(GameObject w, Transform g, bool pistola)
        {
            if (vLeftIk == null || vRightIk == null) return;
            Soltar();
            if (!w || !w.activeSelf) return;
            if (vWeapon) vWeapon.targetBone = w.transform;
            if (vWeaponIk) vWeaponIk.targetBone = w.transform;   // as camadas de arma escrevem aqui; o Avaliar copia de volta
            // ATENÇÃO: o SOCKET_Grip da arma é onde vai a mão DIREITA (o cabo). A esquerda vai no guarda-mão, e esse ponto
            // não é um socket: o FBRig calcula ele por quadro (RifleLeftHand, palma por baixo do trilho com recuo). Medido
            // na primeira tentativa: mandar a esquerda para o SOCKET_Grip deixa ela 18 a 24 cm fora do lugar.
        }

        // alvo da mão esquerda = o MESMO ponto do guarda-mão que o FBRig calculou neste quadro (FBRig.LeftTarget). Assim o
        // que muda entre o rig clássico e o novo é só QUEM resolve o braço, não para onde ele vai — é o A/B que interessa.
        Transform alvoEsq;
        void AlvoEsquerda()
        {
            var r = ch.rig;
            bool vale = r != null && r.LeftTargetValid && ch.weaponGO && ch.weaponGO.activeSelf && !ch.IsPistol;
            if (!vale) { if (vLeftIk) vLeftIk.targetBone = bLeftHand; return; }
            if (!alvoEsq) { var go = new GameObject("FB_AlvoMaoEsq"); go.transform.SetParent(ch.an.transform, false); alvoEsq = go.transform; }
            // o FBRig calculou o alvo com a arma AINDA SEM o balanço deste quadro; somo o desvio que as camadas de arma
            // aplicaram no quadro passado, para a mão esquerda acompanhar a arma em vez de ficar parada no ar
            var p = ch.weaponGO.transform.position;
            alvoEsq.SetPositionAndRotation(p + desvioQ * (r.LeftTarget - p) + desvioP,
                                           desvioQ * (bLeftHand ? bLeftHand.rotation : ch.transform.rotation));
            if (vLeftIk) vLeftIk.targetBone = alvoEsq;
        }

        // depois das camadas: a arma vai para onde o "IK WeaponBone" parou, e guarda-se o desvio para a mão esquerda do
        // próximo quadro. Sem arma na mão, nada acontece (o osso virtual segue a própria mão).
        void AplicarArma()
        {
            if (!vWeaponIk || !ch.weaponGO || !ch.weaponGO.activeSelf || ch.IsPistol) { desvioP = Vector3.zero; desvioQ = Quaternion.identity; return; }
            var t = ch.weaponGO.transform; var b = vWeaponIk.transform;
            desvioP = b.position - t.position; desvioQ = b.rotation * Quaternion.Inverse(t.rotation);
            if (desvioP.sqrMagnitude > .25f) { desvioP = Vector3.zero; desvioQ = Quaternion.identity; return; }   // trava: nunca mais de 50 cm
            t.SetPositionAndRotation(b.position, b.rotation);
            Medir();
        }

        // -fbrigdbg: mede de quanto as camadas de arma realmente mexem (sem isso não dá para saber se o balanço faz algo).
        // Some do jogo normal: só liga pela linha de comando.
        static bool? dbg; static float maxCm, maxGrau, somaCm; static int n; static float proxLog;
        void Medir()
        {
            dbg ??= System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbrigdbg") >= 0;
            if (dbg != true) return;
            float cm = desvioP.magnitude * 100, g = Quaternion.Angle(Quaternion.identity, desvioQ);
            maxCm = Mathf.Max(maxCm, cm); maxGrau = Mathf.Max(maxGrau, g); somaCm += cm; n++;
            if (Time.unscaledTime < proxLog) return;
            proxLog = Time.unscaledTime + 5f;
            Debug.Log($"[FreeBala][r57-rig] balanço da arma: máx {maxCm:0.00} cm / {maxGrau:0.00}° · média {(n > 0 ? somaCm / n : 0):0.00} cm · {n} quadros");
            maxCm = maxGrau = somaCm = 0; n = 0;
        }

        /// <summary>chamado no FIM do LateUpdate do FBCharacter, depois da arma e do FBRig</summary>
        public void Avaliar(float dt)
        {
            if (!ok || !ch || !ch.alive || dt <= 0) return;
            AlvoEsquerda();
            Entradas(dt);
            bones.GameThreadUpdate();
            bones.CachePose();
            bones.EvaluatePose();
            bones.ApplyCachedPose();
            bones.PostEvaluatePose();
            AplicarArma();
        }

        // estado do jogo -> propriedades que as camadas leem (o UserInputController do pacote é só um saco de valores
        // com nome; não tem nada a ver com o Input System)
        void Entradas(float dt)
        {
            bool mirando = ch.aiming && ch.weaponGO && ch.weaponGO.activeSelf;
            input.SetValue(pIsAiming, mirando);
            input.SetValue(pAimW, mirando ? 1f : 0f);

            var v = ch.velocity; var loc = ch.transform.InverseTransformDirection(v);
            float vmax = Mathf.Max(.01f, ch.sprint ? 7.4f : 4.2f);
            input.SetValue(pMove, new Vector4(Mathf.Clamp(loc.x / vmax, -1, 1), Mathf.Clamp(loc.z / vmax, -1, 1), 0, 0));

            // "mouse" = para onde a cabeça/tronco está olhando, em graus, que é o que as camadas de olhar usam
            var olho = ch.transform.eulerAngles; float yaw = Mathf.DeltaAngle(olhoAnt.y, olho.y), pitch = Mathf.DeltaAngle(olhoAnt.x, olho.x);
            olhoAnt = olho;
            input.SetValue(pMouse, new Vector4(Mathf.DeltaAngle(0, olho.y), Mathf.DeltaAngle(0, olho.x), 0, 0));
            input.SetValue(pMouseD, new Vector4(yaw / Mathf.Max(dt, 1e-4f) * .01f, pitch / Mathf.Max(dt, 1e-4f) * .01f, 0, 0));
            input.SetValue(pLean, 0f);
            input.SetValue(pPlayW, 1f);
        }

        void OnDestroy() { if (ok && bones) bones.Dispose(); }
    }
}
