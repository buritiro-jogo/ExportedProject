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
            // r60b: ANDAR armado usava o clipe de CORRER (IE_Rifle_Run_F) — era a causa do braço esticado atravessando o peito ao
            // caminhar com a arma (Ruan, 07/11). A build de navegador, que o Ruan diz estar certa, usa IE_x_walk_forward nesta mesma
            // vaga (src/actor.js, AR_OVR.rifle). O clipe já estava em Anim/Clips e nunca tinha sido ligado aqui. Comparando os dois
            // mapas inteiros, esta era a ÚNICA diferença fora o sprint.
            ["Walk With Rifle"] = "IE_x_walk_forward", ["StrafeRightLoop"] = "IE_Rifle_Run_R", ["StrafeLeftLoop"] = "IE_Rifle_Run_L", ["WalkBwdLoop"] = "IE_Rifle_Run_B",
            ["RunFwdLoop"] = "IE_Rifle_Run_F", ["Run Forward Right"] = "IE_Rifle_Run_FR", ["Run Forward Left"] = "IE_Rifle_Run_FL",
            // r60b (pedido do Ruan: correr com a arma em um braço só) — sprint do COMPLEXO FAVELA, que estava importado e sem uso.
            // Caminho até aqui, porque dois candidatos falharam e vale não repetir:
            //  · IE_Rifle_Sprint_F .................. as duas mãos na arma;
            //  · anim_rifle_ip_strafe_supersprint_ar_f (RE do APK): animado SEM ARMA, é corrida livre de braços bombeando — com o
            //    fuzil preso à mão direita ele fica na VERTICAL atrás da cabeça (só aparece na vista de LADO);
            //  · IE_Pistol_Sprint_F / POL_PI_pistol_run: mãos juntas no queixo / correr mirando.
            // Este tem as mãos BAIXAS e estáveis na cintura, então a arma fica à frente e para baixo. Vale junto com o IK solto no
            // sprint (FBRig): sem isso a esquerda é puxada de volta para o guarda-mão.
            ["Rifle_SprintLoop"] = "Rifle_SprintLoop", ["Rifle_RunFwdLoop"] = "IE_Rifle_Run_F", ["Rifle_RunBwdLoop"] = "IE_Rifle_Run_B",
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

        // POLICIAIS TÁTICOS (rodada 55): pacotes Mixamo do usuário (F:\...\ANIMAÇÕES\MIXAMO), retarget nativo (FBPoliciaMixamo), SÓ em quem
        // está na categoria "policia" do elenco (FBElenco.POLICIA) — o resto do elenco não muda. LIGA com -fbpoliciatatica (r56: saiu do padrão, ver FBAnimProfile.PoliciaTatica).
        // Velocidades medidas (rs → m/s no corpo de jogo ≈ ×1,86): o andar do Pro Rifle (≈3,3 m/s) casa com o andar armado do jogo (3,4 em
        // ação / 4,2 fora); a corrida (≈8,1) com o sprint (7,4); o "sprint" do pacote (≈12,2 m/s) passa do sprint do jogo e escorregaria —
        // fica fora. Por isso o anel de "corrida" das árvores (onde o andar de 3,4–4,2 m/s cai) recebe o ANDAR tático, e o sprint a corrida.
        // Rodada 56 (05/11/2026): o pacote tático do Mixamo saiu do padrão. Medido no MESMO laboratório, com civil e
        // policial lado a lado (logs/r56_lab_mix), os clipes POL_* de locomoção estalam de 1.000 a 2.000 °/s a mais
        // que os do jogo (correr desarmado +2.042, andar de lado +1.027, andar em combate +1.118) — é o "travado,
        // robótico" que o Ruan viu. Mira, mão na arma, cotovelo, joelho e estiramento dão IGUAL ao civil, então o
        // problema é só o encaixe dos clipes. Fica atrás de -fbpoliciatatica até os clipes serem reciclados.
        public static bool PoliciaTatica => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbpoliciatatica") >= 0;
        // ===================== RODADA 58: animação de COMBATE nova para quem NÃO é policial =====================
        // Mesma biblioteca do Mixamo da r55 (os clipes POL_* já estão importados), mas SÓ mira, tiro, recarga, granada
        // e reação a tiro. A LOCOMOÇÃO fica de fora de propósito: foi exatamente ela que quebrou nos policiais — medido
        // na r57, com civil e policial na mesma corrida do laboratório, os clipes de andar/correr do Mixamo estalavam
        // de 1.000 a 2.000 °/s a mais que os do jogo. Andar, correr, agachar e pular continuam os aprovados.
        public static readonly Dictionary<string, string> NovoRifle = new()
        {
            // r60b: a MIRA VOLTOU para ca, e o motivo esta medido. Tirei esta linha para a vaga Rifle_Idle cair no clipe do proprio
            // Animator (o do Complexo Favela), como o Ruan pediu e como a build de navegador faz. Resultado em jogo: o corpo passou a
            // INCLINAR ao mirar sem luneta. A causa esta no aviso que ja existia no FBRig (linha ~142): o clipe para onde ela cai e de
            // ARMA BAIXA, e para por o cano no alvo a partir dali o rig deita a coluna ate 22 graus para tras e levanta os bracos ate
            // 46 graus. Os clipes de mira DE VERDADE do Complexo Favela ("Aiming" e "Idle Aiming") NAO estao importados no projeto:
            // so veio o "Rifle Idle", que e o de arma baixa. Para fazer como o web e preciso importar aqueles dois primeiro.
            // r60b: MIRA DO COMPLEXO FAVELA, enfim. O "Idle Aiming" (2,1 s em laco, com respiracao) veio do datapack do jogo e e a
            // pose de mira parada DE VERDADE. Ele nunca tinha sido importado porque o tools/anims.py filtra so resources.assets e
            // este clipe esta em sharedassets5.assets; extraido agora pelo FBAnimImport.ImportMiraCf.
            // Antes passou por aqui: POL_PR_idle_aiming (Mixamo da r55, servia mas nao era a fonte pedida) e, por um commit, a vaga
            // caiu no "Rifle Idle" de ARMA BAIXA, que fazia o rig deitar a coluna 22 graus para levantar o cano.
            ["Rifle_Idle"] = "Idle Aiming",                               // parado de mira
            ["Rifle_Reload_2"] = "POL_BS_reloading_fit",                 // recarga
            ["Rifle_Grenade_Throw_Single"] = "POL_BS_toss_grenade_fit",  // granada
            ["Rifle_Hit_C_1"] = "POL_BS_hit_reaction",                   // levou tiro
        };
        public static readonly Dictionary<string, string> NovoPistol = new()
        {
            ["Rifle_Idle"] = "POL_PI_pistol_idle",
        };
        public const string NovoCrouchAim = "POL_PR_idle_crouching_aiming", NovoPistolCrouch = "POL_PI_pistol_kneeling_idle";

        /// <summary>chave dos ajustes; -fbanimnova id1,id2 limita a estes personagens (laboratório: comparar na MESMA corrida)</summary>
        static string[] soEstes; static bool leuSoEstes;
        public static bool CombateNovo(string skinId)
        {
            if (!FBSession.animNova) return false;
            if (!leuSoEstes)
            {
                leuSoEstes = true;
                var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbanimnova");
                if (i >= 0 && i + 1 < a.Length) soEstes = a[i + 1].Split(',');
            }
            return soEstes == null || System.Array.IndexOf(soEstes, skinId) >= 0;
        }

        // fuzil/SMG/escopeta/sniper — CombatLocomotion (em ação) e PatrolLocomotion (fora de ação) nas 8 direções do andar do Pro Rifle
        public static readonly Dictionary<string, string> PolRifle = new()
        {
            ["Rifle_Idle"] = "POL_PR_idle_aiming", ["Rifle Idle"] = "POL_PR_idle",
            ["Rifle_WalkFwdLoop"] = "POL_PR_walk_forward", ["Walk Forward Right"] = "POL_PR_walk_forward_right", ["Rifle_StrafeRightLoop"] = "POL_PR_walk_right",
            ["Walk Backward Right"] = "POL_PR_walk_backward_right", ["Rifle_WalkBwdLoop"] = "POL_PR_walk_backward", ["Walk Backward Left"] = "POL_PR_walk_backward_left",
            ["Rifle_StrafeLeftLoop"] = "POL_PR_walk_left", ["Walk Forward Left"] = "POL_PR_walk_forward_left",
            ["Rifle_RunFwdLoop"] = "POL_PR_walk_forward", ["Run Forward Right"] = "POL_PR_walk_forward_right", ["Run Forward Left"] = "POL_PR_walk_forward_left",
            ["Rifle_RunBwdLoop"] = "POL_PR_walk_backward", ["RunRtLoop"] = "POL_PR_walk_right", ["RunLtLoop"] = "POL_PR_walk_left",
            ["RunStrafeRight135Loop"] = "POL_PR_walk_backward_right", ["RunStrafeLeft135Loop"] = "POL_PR_walk_backward_left",
            ["Walk With Rifle"] = "POL_PR_walk_forward", ["RunFwdLoop"] = "POL_PR_walk_forward",
            ["StrafeRightLoop"] = "POL_PR_walk_right", ["StrafeLeftLoop"] = "POL_PR_walk_left", ["WalkBwdLoop"] = "POL_PR_walk_backward",
            ["Rifle_SprintLoop"] = "POL_PR_run_forward",
            ["Rifle_CrouchLoop"] = "POL_PR_idle_crouching", ["Walk Crouching Forward"] = "POL_PR_walk_crouching_forward",
            ["Crouch Walk Strafe Right"] = "POL_PR_walk_crouching_right", ["Walk Crouching Backward 1"] = "POL_PR_walk_crouching_backward",
            ["Crouch Walk Strafe Left"] = "POL_PR_walk_crouching_left",
            ["Jump"] = "POL_PR_jump_loop", ["Landing"] = "POL_PR_jump_down", ["Rifle_Hit_C_1"] = "POL_BS_hit_reaction",
            ["Rifle_Grenade_Throw_Single"] = "POL_BS_toss_grenade_fit", ["Rifle_Reload_2"] = "POL_BS_reloading_fit",
            // 2ª passada (pedido do usuário: "use todas as animações novas"): as vagas do controller que ainda caíam nos clipes
            // antigos — ré do anel de corrida e as 4 diagonais (45° e 135°) dos dois anéis.
            ["RunBwdLoop"] = "POL_PR_walk_backward",
            ["StrafeRight45Loop"] = "POL_PR_walk_forward_right", ["StrafeLeft45Loop"] = "POL_PR_walk_forward_left",
            ["StrafeRight135Loop"] = "POL_PR_walk_backward_right", ["StrafeLeft135Loop"] = "POL_PR_walk_backward_left",
            ["RunStrafeRight45Loop"] = "POL_PR_walk_forward_right", ["RunStrafeLeft45Loop"] = "POL_PR_walk_forward_left",
        };
        // sprint (Shift): a corrida tática nas vagas que a árvore usa a 7,4 m/s em cada direção (sem isto o lado/ré tocava o andar a 1,45×)
        public static readonly Dictionary<string, string> PolRifleSprint = new()
        {
            ["Run Forward Right"] = "POL_PR_run_forward_right", ["Run Forward Left"] = "POL_PR_run_forward_left", ["RunFwdLoop"] = "POL_PR_run_forward",
            ["StrafeRightLoop"] = "POL_PR_run_right", ["StrafeLeftLoop"] = "POL_PR_run_left", ["WalkBwdLoop"] = "POL_PR_run_backward",
            ["RunRtLoop"] = "POL_PR_run_right", ["RunLtLoop"] = "POL_PR_run_left", ["Rifle_RunBwdLoop"] = "POL_PR_run_backward",
            ["RunStrafeRight135Loop"] = "POL_PR_run_backward_right", ["RunStrafeLeft135Loop"] = "POL_PR_run_backward_left",
            ["RunBwdLoop"] = "POL_PR_run_backward",
            ["StrafeRight45Loop"] = "POL_PR_run_forward_right", ["StrafeLeft45Loop"] = "POL_PR_run_forward_left",
            ["StrafeRight135Loop"] = "POL_PR_run_backward_right", ["StrafeLeft135Loop"] = "POL_PR_run_backward_left",
            ["RunStrafeRight45Loop"] = "POL_PR_run_forward_right", ["RunStrafeLeft45Loop"] = "POL_PR_run_forward_left",
        };
        // SMG e escopeta: postura de arma curta do Basic Shooter (parado de mira e pulo próprios); a locomoção continua a do Pro Rifle
        // porque a do Basic Shooter anda a ≈1,6 m/s e correria a 2,4× de ritmo nos anéis do jogo (pernas girando).
        public static readonly Dictionary<string, string> PolCompact = new()
        {
            ["Rifle_Idle"] = "POL_BS_rifle_aiming_idle", ["Jump"] = "POL_BS_rifle_jump",
        };
        public const string PolCrouchAim = "POL_PR_idle_crouching_aiming";                  // mira agachado (Upper Body "Idle" enquanto agachado)
        public const string PolFireAuto = "POL_SH_firing_rifle", PolFireSemi = "POL_BS_firing_rifle";
        // pistola — Pistol Handgun Locomotion Pack (lado medido no FBX: "pistol strafe" vai para a ESQUERDA, "(2)" para a direita);
        // diagonais = andar de frente/ré do pacote (os "arc" do pacote são curvas de 25–45°, sem vaga: o giro é do controle)
        public static readonly Dictionary<string, string> PolPistol = new()
        {
            ["Rifle_Idle"] = "POL_PI_pistol_idle", ["Rifle Idle"] = "POL_PI_pistol_idle",
            ["Rifle_WalkFwdLoop"] = "POL_PI_pistol_walk", ["Walk Forward Right"] = "POL_PI_pistol_walk", ["Walk Forward Left"] = "POL_PI_pistol_walk",
            ["Rifle_StrafeRightLoop"] = "POL_PI_pistol_strafe_2", ["Rifle_StrafeLeftLoop"] = "POL_PI_pistol_strafe",
            ["Rifle_WalkBwdLoop"] = "POL_PI_pistol_walk_backward", ["Walk Backward Right"] = "POL_PI_pistol_walk_backward", ["Walk Backward Left"] = "POL_PI_pistol_walk_backward",
            ["Rifle_RunFwdLoop"] = "POL_PI_pistol_walk", ["Run Forward Right"] = "POL_PI_pistol_walk", ["Run Forward Left"] = "POL_PI_pistol_walk",
            ["Rifle_RunBwdLoop"] = "POL_PI_pistol_walk_backward", ["RunRtLoop"] = "POL_PI_pistol_strafe_2", ["RunLtLoop"] = "POL_PI_pistol_strafe",
            ["RunStrafeRight135Loop"] = "POL_PI_pistol_walk_backward", ["RunStrafeLeft135Loop"] = "POL_PI_pistol_walk_backward",
            ["Walk With Rifle"] = "POL_PI_pistol_walk", ["RunFwdLoop"] = "POL_PI_pistol_walk",
            ["StrafeRightLoop"] = "POL_PI_pistol_strafe_2", ["StrafeLeftLoop"] = "POL_PI_pistol_strafe", ["WalkBwdLoop"] = "POL_PI_pistol_walk_backward",
            ["Rifle_SprintLoop"] = "POL_PI_pistol_run", ["Rifle_CrouchLoop"] = "POL_PI_pistol_kneeling_idle",
            // 2ª passada: ré do anel de corrida, as 4 diagonais e o pulo/queda. O pacote de pistola não tem andar agachado nem
            // pulo em laço — entram os do Pro Rifle (as pernas são as mesmas; as mãos da pistola vêm do IK de duas mãos, como em pé).
            ["RunBwdLoop"] = "POL_PI_pistol_walk_backward",
            ["StrafeRight45Loop"] = "POL_PI_pistol_walk", ["StrafeLeft45Loop"] = "POL_PI_pistol_walk",
            ["StrafeRight135Loop"] = "POL_PI_pistol_walk_backward", ["StrafeLeft135Loop"] = "POL_PI_pistol_walk_backward",
            ["RunStrafeRight45Loop"] = "POL_PI_pistol_walk", ["RunStrafeLeft45Loop"] = "POL_PI_pistol_walk",
            ["Walk Crouching Forward"] = "POL_PR_walk_crouching_forward", ["Crouch Walk Strafe Right"] = "POL_PR_walk_crouching_right",
            ["Walk Crouching Backward 1"] = "POL_PR_walk_crouching_backward", ["Crouch Walk Strafe Left"] = "POL_PR_walk_crouching_left",
            ["Jump"] = "POL_PR_jump_loop", ["Landing"] = "POL_PR_jump_down",
        };
        public static readonly Dictionary<string, string> PolPistolSprint = new()
        {
            ["Run Forward Right"] = "POL_PI_pistol_run", ["Run Forward Left"] = "POL_PI_pistol_run", ["RunFwdLoop"] = "POL_PI_pistol_run",
            ["WalkBwdLoop"] = "POL_PI_pistol_run_backward", ["Rifle_RunBwdLoop"] = "POL_PI_pistol_run_backward",
        };
        // desarmado — Locomotion Pack (parado, andar, andar/correr de lado, correr); a policial mantém o andar de frente por sexo (REG_Walk_F)
        public static readonly Dictionary<string, string> PolUnarmed = new()
        {
            ["Idle"] = "POL_LO_idle", ["WalkFwdLoop"] = "POL_LO_walking",
            ["StrafeLeftLoop"] = "POL_LO_left_strafe_walking", ["StrafeRightLoop"] = "POL_LO_right_strafe_walking",
            ["RunLtLoop"] = "POL_LO_left_strafe", ["RunRtLoop"] = "POL_LO_right_strafe", ["RunFwdLoop"] = "POL_LO_running", ["SprintFwdLoop"] = "POL_LO_running",
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
