// PERFIL DE ANIMAÇÃO (rodada 37) — de onde vem cada clipe, num lugar só (antes: mapas UA_OVR/AR_OVR/HOLST do actor.js do jogo web,
// exportados para o Unity, que trocavam quase toda a locomoção do Complexo por clipes do Invasão Elite).
//  · COMPLEXO FAVELA (base de gameplay): controller original EnemyAnimator_AAA_relaxed (parado/andar de combate/agachar/mira/tiro/
//    recarga/reação/morte); pistola = clipes de fuzil + mãos por IK (SetTwoHandedPistolMode do PuppetAimRig do Complexo).
//  · CORRIDA (22/10, teste do usuário: a corrida do PedestrianAnimator foi rejeitada): volta o conjunto aprovado antes da rodada —
//    corrida/sprint/parado desarmados e trote/sprint armados do Invasão Elite, andar de lado/ré desarmado do pacote NPC.
//  · INVASÃO ELITE: só o posicionamento das armas no corpo (pedido de 21/10: "IE = apenas costas/peito") — sacar/guardar nas costas, no
//    peito (bandoleira) e na coxa. Única exceção: o agachado PARADO desarmado (o Complexo não tem agachado sem arma parado; o jogador do
//    Complexo nunca fica desarmado). Tiro e recarga voltaram a ser os do Complexo para todas as classes (o puppet original usa
//    Rifle_ShootOnce/Rifle_ShootLoop_Additive/Rifle_Reload_2 em todas as armas): os clipes de bomba/ferrolho do IE davam estalos de
//    6.000°/s no braço e tiravam o cano 11–14° do alvo a cada disparo (laboratório da rodada 37).
//  · SIGMA (Free Fire): cura (Stand/Crouch_Self_Rescue) e reanimar aliado (Crouch_Self_Rescue — o FF usa a mesma família CURE).
using System.Collections.Generic;

namespace FreeBala
{
    public static class FBAnimProfile
    {
        // LOCOMOÇÃO (22/10, depois do teste do usuário: "a animação de correr está horrível, a que tínhamos antes era melhor"): volta o
        // conjunto APROVADO antes da rodada 37 — o mesmo mapa de trocas do jogo (unarmed / armed.rifle do actor.js), agora aqui.
        // Desarmado: corrida, sprint, parado e diagonais do IE (ANIM-Unarmed_*), andar de frente do IE, andar de lado/ré do pacote NPC.
        public static readonly Dictionary<string, string> Unarmed = new()
        {
            ["Idle"] = "IE_Unarmed_Idle01", ["WalkFwdLoop"] = "IE_walk_hands_free",
            ["RunFwdLoop"] = "IE_Unarmed_Sprint_F", ["SprintFwdLoop"] = "IE_Unarmed_Sprint_F",
            ["RunStrafeRight45Loop"] = "IE_Unarmed_Run_FR", ["RunRtLoop"] = "IE_Unarmed_Run_R", ["RunStrafeRight135Loop"] = "IE_Unarmed_Run_BR", ["RunBwdLoop"] = "IE_Unarmed_Run_B",
            ["RunStrafeLeft135Loop"] = "IE_Unarmed_Run_BL", ["RunLtLoop"] = "IE_Unarmed_Run_L", ["RunStrafeLeft45Loop"] = "IE_Unarmed_Run_FL",
            ["StrafeRight45Loop"] = "IE_Unarmed_Run_FR", ["StrafeLeft45Loop"] = "IE_Unarmed_Run_FL", ["StrafeRight135Loop"] = "IE_Unarmed_Run_BR", ["StrafeLeft135Loop"] = "IE_Unarmed_Run_BL",
            ["StrafeRightLoop"] = "IE_Unarmed_Run_R", ["StrafeLeftLoop"] = "IE_Unarmed_Run_L", ["WalkBwdLoop"] = "IE_Unarmed_Run_B",
        };
        // andar desarmado para a frente (22/10, pedido do usuário: "o rig/animação de andar sem a arma está errado"): clipes novos, um por
        // sexo (F:\CRIAÇÃO DE GAMES ASSETS\PERSONAGENS REGIONAIS\ANIMAÇÕES → src/tools_retarget.mjs → assets/walk_regional.json)
        public const string WalkMale = "REG_Walk_M", WalkFemale = "REG_Walk_F";
        // velocidade de ANDAR desarmado (m/s): passo natural dos clipes novos ≈ 1,47 m/s × ritmo 1,5 (LocoRate) — a 4,1 m/s de antes qualquer
        // caminhada deslizava ou se misturava com o sprint (era o "andar errado"); correr desarmado = Shift (sprint do IE)
        public const float UnarmedWalkSpeed = 2.2f;
        // andar de lado/ré desarmado (24/10, retorno do usuário: "andando para os lados e para trás sem arma o corpo quebra por completo").
        // Medido nos .anim: NPC_WalkRight/Left/Backward (pacote NPC) saíram de um retarget quebrado — ombros travados em +1,12 (limite do
        // músculo), mão torcida em −1,04, dedos em ±1,6 e os TRÊS clipes praticamente iguais (sem lado nem ré de verdade). Os diagonais Mixamo
        // "Walk Forward/Backward Left/Right" também estão quebrados (ombro até −3,2). Volta aos clipes ORIGINAIS do controller nessas vagas
        // (StrafeLeftLoop/StrafeRightLoop/WalkBwdLoop: andar desarmado, valores sãos, rs ≈ 1,57 como o andar). Valor = chave → sem troca.
        public static readonly Dictionary<string, string> UnarmedWalkNpc = new() { ["StrafeRightLoop"] = "StrafeRightLoop", ["StrafeLeftLoop"] = "StrafeLeftLoop", ["WalkBwdLoop"] = "WalkBwdLoop" };
        // para a DIREITA desarmado (26/10, pedido do usuário): andar = "Right Strafe Walking", correr = "Right Strafe" (Mixamo; músculos
        // dentro do limite — o giro de cabeça do correr é o olhar para onde vai). Vale para o jogador e quem não for NPC.
        public static readonly Dictionary<string, string> UnarmedRight = new() { ["StrafeRightLoop"] = "Right Strafe Walking", ["RunRtLoop"] = "Right Strafe" };
        // NPCs (26/10): andar de lado e nas diagonais com os clipes ORIGINAIS do controller; homens andam para frente/trás com
        // WalkFwdLoop/WalkBwdLoop (no lugar do andar regional/do conjunto). Valor = chave → sem troca.
        public static readonly string[] NpcStrafe = { "StrafeLeft45Loop", "StrafeLeft135Loop", "StrafeLeftLoop", "StrafeRight45Loop", "StrafeRight135Loop", "StrafeRightLoop" };
        public static readonly string[] NpcMaleWalk = { "WalkFwdLoop", "WalkBwdLoop" };
        // armado (fuzil E pistola — a pistola usa as pernas do trote de fuzil, braços pelo IK das duas mãos): trote e sprint do IE nas vagas
        // de corrida da PatrolLocomotion e da CombatLocomotion; o andar de combate continua o do Complexo (como antes, quando "em ação")
        public static readonly Dictionary<string, string> ArmedRun = new()
        {
            ["Walk With Rifle"] = "IE_Rifle_Run_F", ["StrafeRightLoop"] = "IE_Rifle_Run_R", ["StrafeLeftLoop"] = "IE_Rifle_Run_L", ["WalkBwdLoop"] = "IE_Rifle_Run_B",
            ["RunFwdLoop"] = "IE_Rifle_Run_F", ["Run Forward Right"] = "IE_Rifle_Run_FR", ["Run Forward Left"] = "IE_Rifle_Run_FL",
            ["Rifle_SprintLoop"] = "IE_Rifle_Sprint_F", ["Rifle_RunFwdLoop"] = "IE_Rifle_Run_F", ["Rifle_RunBwdLoop"] = "IE_Rifle_Run_B",
            // [parceiro] 31/10/2026: o CombatLocomotion ganhou correr de lado e na diagonal de verdade
            // (antes o "de lado" tocava a corrida na diagonal para frente e o pé escorregava).
            // Estes clipes base só aparecem no RelaxedLocomotion, que só roda desarmado.
            ["RunRtLoop"] = "IE_Rifle_Run_R", ["RunLtLoop"] = "IE_Rifle_Run_L",
            ["RunStrafeRight135Loop"] = "IE_Rifle_Run_BR", ["RunStrafeLeft135Loop"] = "IE_Rifle_Run_BL",
        };
        public const string PistolSprint = "IE_Pistol_Sprint_F";
        // AGACHADO (rodada 42, usuário: "mesmo sem estar segurando uma arma, o personagem permanece numa postura de combate, com os braços
        // posicionados como se estivesse segurando um armamento invisível"). A árvore CrouchLocomotion do Complexo tem 5 vagas: parado
        // (Rifle_CrouchLoop) + 4 direções do andar agachado — e as 4 são do pacote de FUZIL do Mixamo (mãos à frente segurando a arma), por
        // isso o desarmado "segurava o nada" (folha desarmado_agachado_andando do laboratório: os 48 personagens com as mãos no ar).
        // Seis estados, cada um com a própria postura (clipes do Invasão Elite, que tem os três conjuntos completos em 8 direções):
        //   desarmado parado = IE_Unarmed_Crouch_Idle · andando e rápido = IE_Unarmed_Crouch_Run_* (o ritmo segue a velocidade: LocoRate)
        //   armado parado = Rifle_CrouchLoop (Complexo) · andando = andar agachado de fuzil do Complexo · rápido = IE_Rifle_Crouch_Run_*
        //   (pistola usa as pernas do conjunto de fuzil com as mãos no IK de duas mãos, como em pé; o IE_Pistol_Crouch tem ciclo de 1,6 s — câmera lenta)
        public static readonly Dictionary<string, string> UnarmedCrouch = new()
        {
            ["Rifle_CrouchLoop"] = "IE_Unarmed_Crouch_Idle", ["Walk Crouching Forward"] = "IE_Unarmed_Crouch_Run_F", ["Crouch Walk Strafe Right"] = "IE_Unarmed_Crouch_Run_R",
            ["Walk Crouching Backward 1"] = "IE_Unarmed_Crouch_Run_B", ["Crouch Walk Strafe Left"] = "IE_Unarmed_Crouch_Run_L",
        };
        public static readonly Dictionary<string, string> ArmedCrouchFast = new()
        {
            ["Walk Crouching Forward"] = "IE_Rifle_Crouch_Run_F", ["Crouch Walk Strafe Right"] = "IE_Rifle_Crouch_Run_R",
            ["Walk Crouching Backward 1"] = "IE_Rifle_Crouch_Run_B", ["Crouch Walk Strafe Left"] = "IE_Rifle_Crouch_Run_L",
        };
        public const float CrouchFastSpeed = 4.4f;                                                 // agachado rápido (Shift agachado); andar agachado = speedCrouching (3)

        // recarga e tiro: os do Complexo para todas as classes (a duração por classe continua no FBWeaponUser)
        public static string Reload(WeaponDef w) => w == null ? null : "Rifle_Reload_2";
        public static string Fire(WeaponDef w) => w == null ? null : w.auto ? "Rifle_ShootLoop_Additive" : "Rifle_ShootOnce";

        // coldre (Invasão Elite): sacar/guardar e o instante em que a mão pega/solta a arma (medido pela mão direita no clipe).
        // Velocidades balanceadas: ágil e responsivo para trocação de tiro, mas fluido e com peso visual natural.
        public class Holster { public string draw, put; public float grab, rel; public float drawSpeed = 1.45f, putSpeed = 1.55f; }
        public static readonly Dictionary<string, Holster> Holsters = new()
        {
            ["back"] = new Holster { draw = "IE_Rifle_Draw_Back", grab = .52f, put = "IE_Rifle_Unequip_R", rel = .40f, drawSpeed = 1.45f, putSpeed = 1.55f },
            ["sling"] = new Holster { draw = "IE_Rifle_Equip", grab = .18f, put = "IE_Rifle_Unequip", rel = .32f, drawSpeed = 1.35f, putSpeed = 1.50f },
            ["thigh"] = new Holster { draw = "IE_Pistol_Equip", grab = .24f, put = "IE_Pistol_Unequip", rel = .30f, drawSpeed = 1.50f, putSpeed = 1.50f },
        };

        // Sigma (Free Fire): cura em pé, cura agachado e reanimar aliado (mesma família CURE do FF)
        public const string HealStart = "FF_SYS_Heal_Start", HealLoop = "FF_SYS_Heal_Loop", HealEnd = "FF_SYS_Heal_End", HealCrouch = "FF_SYS_KO_Revive";
        // reanimar aliado (rodada 38): o "Crouch_Self_Rescue" do FF era a animação de se AUTO-curar agachado (o usuário: "a animação de reanimar
        // aliados está incorreta"). Agora o par do Invasão Elite feito para isso — anim_Reviver_With_Medic_R2 (quem socorre: ajoelha 0–1,07 s,
        // atende 1,07–5,07 s em laço, levanta 5,07–6,13 s) e anim_Downed_Revive_With_Medic_R2 (quem está caído: deitado sendo atendido, levanta)
        public const string ReviveStart = "IE_Revive_Start", ReviveLoop = "IE_Revive_Loop", ReviveEnd = "IE_Revive_End";
        public const string DownedTreated = "IE_Downed_Treated", DownedGetUp = "IE_Downed_GetUp";
    }
}
