// Bot (porte do updateBot do main.js — EnemyAI/AITacticalManager do original). RODADA 37: percepção gradual, alcance de combate pela
// arma, mira com erro angular que converge, busca do último contato e agressividade por papel (FBBotSense; detalhes lá). Abaixo, o
// comportamento herdado que continua valendo:
//  percepção individual a cada 0,12–0,22 s (alcance 220 m, 9 m em 360°, periférica 16 m/260°, frontal 144°; em combate 235°; raio fino no
//  peito e na cabeça), estados patrol / guard / escort / hunt / squad / combat / cover / flee / dummy / knocked, cobertura (8 direções × 3
//  distâncias, escondida da ameaça) com espiadas, fuga com pouca vida, papéis táticos (faixa de flanco), inteligência do esquadrão (último
//  contato), alerta por barulho de tiro, revida e chama apoio ao levar tiro, reanima aliado caído, usa kit médico, guarda a arma quando
//  calmo (coldre do IE) e saca ao ver ameaça, tiro com predição (lead), chance de headshot e cadência por distância, pulo de degrau,
//  anti-travamento em 4 níveis, velocidade de animação remapeada (remapSpeed) para os clipes da IA.
// [parceiro] 31/10–02/11: pernas pelo deslocamento real (legVel), limite de velocidade mirando/agachado (bug do deslize), vigia de
//  progresso + ilha do NavMesh + destino seguro (âncora do 10x10), saída a pé de bot travado e realocação só escondida da câmera.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace FreeBala
{
    public static class FBBotBrain
    {
        class Intel { public Vector3 p; public float t; }
        static readonly Intel[] INTEL = new Intel[2];
        public static Vector3? lastNoiseP; public static float lastNoiseT = -99;
        public static void TeamIntel(int team, Vector3 p) { if (team == 0 || team == 1) INTEL[team] = new Intel { p = p, t = Time.time }; }
        public static Vector3? FreshIntel(int team, float s = 9) { if (team < 0 || team > 1) return null; var it = INTEL[team]; return it != null && Time.time - it.t < s ? it.p : (Vector3?)null; }
        public static void Noise(Vector3 p) { lastNoiseP = p; lastNoiseT = Time.time; }
        public static void Alert(Vector3 p, float r, int team)
        {
            foreach (var c in FBMatch.Bots) { if (!c || !c.alive) continue; var b = c.GetComponent<FBBot>(); if (!b) continue;
                if ((FBMatch.FFA || c.team != team) && c.team < 2 && b.ai != "combat" && b.ai != "cover" && b.ai != "dummy" && b.ai != "guard" && (c.Pos - p).magnitude < r)
                { var q = p + Random.insideUnitSphere.Flat() * (c.Pos - p).magnitude * .12f; b.lastSeen = q; b.goalP = b.FlankPoint(q); b.goalT = FBUtil.Rnd(10, 16); b.pauseT = 0; TeamIntel(c.team, q); } }
        }
        public static void Reset() { INTEL[0] = INTEL[1] = null; lastNoiseP = null; }
    }

    public class FBBot : MonoBehaviour
    {
        [System.NonSerialized] public FBCharacter ch; FBWeaponUser wu; CharacterController cc;
        public string ai = "patrol", role = "tactical"; public float laneOffset, gunMul = 1;
        [System.NonSerialized] public FBCharacter target; [System.NonSerialized] public Vector3? lastSeen, goalP, coverPos; [System.NonSerialized] public float goalT, pauseT;
        public float AimYawRad => aimYawRad; public float AimPitch => pitch;   // brilho da luneta (FBTactics)
        Vector3 home, vel; float thinkT, shotT, seeT = 9, strafeT, strafe, coverT, peekT, peekT2, peekDur, fleeT, calmT, drawT, jumpCd, stuckWin, medT, airT, aimYawRad, pitch, healEndT;
        int burst, stuckN; float botSlide, botStep = -1; FBMotor.Ground botGround; bool sees, peeking, holster, onGround = true, rescuing, safeOk; Vector3 lastPos; FBCharacter reviveTgt; float hitT = -99, safeT;
        // percepção (rodada 37): consciência por alvo, quando o alvo atual foi adquirido, pontos do alvo à vista, papel → distância preferida
        readonly Dictionary<FBCharacter, float> aware = new(); float lookT, acquireT, lastThinkDt = .17f, searchT, aggr = .6f, prefDist = 22; int seenMask = 2; FBCharacter acquired;
        List<Vector3> path; Vector3? pathGoal; float navT; Vector3? escOff;
        // boneco do treino
        Vector3 strafeDir; float strafeAmp, strafeW, dT;

        // [parceiro] fisica 31/10: velocidade que as pernas e o LocoRate enxergam: a pedida, limitada ao deslocamento real do CharacterController
        Vector3 legVel;
        // [parceiro] fisica 31/10: parte da velocidade pedida que o CC nao andou (pedida - real), filtrada (~80 ms). Sem o filtro, nos quadros de
        // colisao parcial (bots se rocando na saida da base, quina, degrau) o ritmo das pernas/LocoRate pulava de um quadro para o outro.
        // Filtra so o deficit (nao a velocidade toda): sem colisao o deficit e 0 e legVel = vel, sem atraso na arrancada/parada.
        Vector3 legDef;
        // [parceiro] fisica 31/10: velocidade pedida (sem o y): o FBPropBody empurra objetos com ela, porque ch.velocity agora e o deslocamento
        // real (~0 quando o objeto trava o bot)
        public Vector3 WishVel => new Vector3(vel.x, 0, vel.z);

        // [parceiro] presos 01/11: aplica_presos.py v3. Vigia de progresso e ilha do NavMesh (ver Vigia). Campos próprios: o stuckN de
        // antes continua igual para obstáculo curto.
        float vigUlt = -99, vigT, vigDur, vigQuer, vigBase, vigBloq, vigTotal, vigEstritoAte = -99; int vigNivel;
        bool vigTemDest, vigAcomp; Vector3 vigUltDest, vigPos, vigP0, vigDest;
        // [parceiro] presos v4: saída a pé de bot travado (ver SaidaLivre): ponto livre a 1,4-2,2 m e até quando ele manda no movimento
        Vector3? saidaP; float saidaAte = -99; int saidaGiro;
        Vector3? realocAlvo; Vector3 realocDe; int realocTipo; string realocPorque;
        // [parceiro] revisão 2: realocação pedida pelo stuckN 10 (tipo 3) vale até este tempo, também fora da patrulha
        float realocAte;
        // [parceiro] revisão 2: o destino deste quadro veio do goalP da patrulha (o desvio de lado do stuckN 4 só serve aí)
        bool destDoGoal;
        // [parceiro] revisão 2: o cache destTem/destK/destSub/destFora foi preenchido pela regra da parede da arena (não pela ilha)
        bool destLimite;
        float ilhaT; int ilhaN; Vector3 ilhaOkP; float ilhaOkT = -99;
        bool destTem, destFora; Vector3 destK, destSub; float destT = -99;
        // [parceiro] presos 01/11: destino novo ainda sem classificar neste quadro (célula nova sem vaga ou limite de 1/s): não vale a
        // reta dos 12 m, vai pelo Steer
        bool destPendente;
        // [parceiro] presos 01/11: FindCover vetado até este tempo (uma cobertura fora da ilha principal foi cancelada; ver DestinoSeguro)
        float coverVetoT = -99;
        // [parceiro] presos 01/11: quadro em que a vaga de consulta pesada ao NavMesh já foi usada (ver PodePesado)
        static int pesadoQuadro = -1;
        static NavMeshPath caminho;
        // [parceiro] presos 01/11: quantos bots foram realocados (teleporte) e por quê: [0] ilha do NavMesh, [1] vigia de progresso
        // (preso fora de ilha), [2] teleporte antigo do stuckN 10, [3] realocação escondida pedida pelo stuckN 10 sem ponto livre
        // ao lado (revisão v4). O FBTesteExe zera no começo de cada rodada.
        public static int Realocados;
        public static readonly int[] RealocadosPor = new int[4];
        // [parceiro] presos 01/11: prevenção com âncora: [0] destino de patrulha/busca fora da ilha principal desviado para o ponto da ilha
        // principal mais perto, [1] cobertura fora da ilha principal cancelada. O FBTesteExe zera no começo de cada rodada.
        // presos v4: [2] saída a pé no stuckN 4 fora da patrulha (combate, cobertura, fuga), [3] saída a pé no stuckN 10 no
        // lugar do teleporte antigo que só mexia 0,1 a 0,7 m (bot em cima do NavMesh, travado em mureta, banco ou outro bot).
        public static readonly int[] Evitados = new int[4];

        void Awake() { ch = GetComponent<FBCharacter>(); wu = GetComponent<FBWeaponUser>(); cc = GetComponent<CharacterController>(); ch.isBot = true; }
        public void Setup(string aiMode, string r, float lane, Vector3 p)
        {
            ai = aiMode; role = r; laneOffset = lane; home = p; lastPos = p; thinkT = FBUtil.Rnd(.05f, .35f); shotT = FBUtil.Rnd(.2f, .6f); jumpCd = FBUtil.Rnd(3.5f, 7); pauseT = FBUtil.Rnd(0, 1.5f);
            if (role == "striker") shotT = FBUtil.Rnd(.12f, .28f); else if (role == "baianogmbarril") shotT = FBUtil.Rnd(.2f, .4f); else if (role == "halemph") { shotT = FBUtil.Rnd(.3f, .55f); burst = 3; } else if (role == "halemph2") shotT = FBUtil.Rnd(.25f, .45f);
            aimYawRad = ch.Yaw * Mathf.Deg2Rad;
            // agressividade por papel: rusher encurta (10 m), tático/atirador de longe seguram distância e usam cobertura
            (aggr, prefDist) = role switch { "rusher" => (1f, 10f), "striker" => (.9f, 14f), "flanker_left" or "flanker_right" => (.75f, 18f), "baianogmbarril" => (.8f, 15f), "halemph" => (.4f, 34f), "halemph2" => (.5f, 28f), _ => (.6f, 22f) };
            aware.Clear(); acquired = null;
        }
        // diagnóstico (autoteste -fbr37): estados, quantos veem um alvo, maior consciência e o inimigo mais perto de qualquer bot
        public static string Diag()
        {
            var st = new Dictionary<string, int>(); float awMax = 0, dMin = 1e9f; int seeing = 0, n = 0;
            foreach (var c in FBMatch.Bots)
            {
                if (!c || !c.alive) continue; var b = c.GetComponent<FBBot>(); if (!b) continue; n++; st[b.ai] = st.TryGetValue(b.ai, out var k) ? k + 1 : 1;
                foreach (var kv in b.aware) awMax = Mathf.Max(awMax, kv.Value); if (b.sees) seeing++;
                foreach (var a in FBMatch.Actors()) if (a && a.alive && a != c && FBMatch.Hostile(a, c)) dMin = Mathf.Min(dMin, (a.Pos - c.Pos).magnitude);
            }
            return $"{n} bots · {string.Join(", ", st.Select(kv => kv.Key + " " + kv.Value))} · vendo {seeing} · consciência máx {awMax:0.00} · inimigo mais perto {(dMin < 1e8f ? dMin : 0):0} m";
        }
        public void SetDummy(Vector3 dir, float amp, float w) { ai = "dummy"; strafeDir = dir; strafeAmp = amp; strafeW = w; }
        public void ResetRound()
        {
            ai = "squad"; target = null; lastSeen = null; goalP = null; holster = false; calmT = 0; ch.SetWeaponVisible(true); ch.StowCurrent(false); vel = Vector3.zero;
            // [parceiro] fisica 31/10: rodada nova comeca em pe. Ninguem zerava o agachado (EndKnock so roda para derrubado/getUp/sys e Revive nao mexe):
            // bot que terminou ou morreu agachado na cobertura comecava a compra e a rodada seguinte agachado, preso em aiCrouchMaxSpeed
            ch.crouch = false; ch.sprint = false; legVel = Vector3.zero; legDef = Vector3.zero;
            path = null; aware.Clear(); acquired = null;
        }
        public void OnKnocked() { ai = "knocked"; target = null; coverPos = null; ch.combat = false; }
        public void OnRevived() { ch.combat = wu.Cur != null; ai = "patrol"; goalP = null; thinkT = 0; }
        public void Engage(FBCharacter src) { if (ai == "dummy") return; ai = "combat"; target = src; lastSeen = src.Pos; }
        // levou tiro: vira para o atirador, revida e chama apoio (attackerScanInterval / alertRadius do EnemyAI original)
        public void OnHit(FBCharacter src)
        {
            if (!ch.alive || ch.team > 1 || ai == "dummy" || ai == "victim" || !src) return;
            float dd = (src.Pos - ch.Pos).magnitude; var jit = Random.insideUnitSphere.Flat() * dd * .08f;            // direção aproximada pelo som/impacto
            target = src; lastSeen = src.Pos + jit; seeT = 0; FBBotBrain.TeamIntel(ch.team, src.Pos + jit); medT = 0; hitT = Time.time; AbortRevive();
            aware.TryGetValue(src, out var aw0); aware[src] = Mathf.Max(aw0, .65f);
            if (ai != "cover" && ai != "flee") { if (ai != "combat") shotT = Mathf.Min(shotT, FBUtil.Rnd(.3f, .6f) * FBCombat.DiffReact); ai = "combat"; }
            Turn(Mathf.Atan2(src.Pos.x - ch.Pos.x, src.Pos.z - ch.Pos.z), .7f);
            // apoio (rodada 38): só os 2 aliados livres mais perto (antes: todos num raio de 45 m largavam o que faziam e iam juntos — o time andava em bolo)
            if (FBMatch.FFA) return;
            foreach (var mb in FBMatch.Bots.Where(m => m && m != ch && m.alive && !m.knocked && m.team == ch.team && (m.Pos - ch.Pos).magnitude < 35).Select(m => m.GetComponent<FBBot>()).Where(mb => mb && mb.ai != "combat" && mb.ai != "cover" && mb.ai != "guard" && !mb.reviveTgt).OrderBy(mb => (mb.ch.Pos - ch.Pos).sqrMagnitude).Take(2))
            { mb.goalP = mb.FlankPoint(src.Pos); mb.goalT = 12; mb.pauseT = 0; mb.lastSeen = src.Pos; }
        }
        void Turn(float yawRad, float k) { float y = ch.Yaw * Mathf.Deg2Rad; y += FBUtil.WrapA(yawRad - y) * k; ch.Yaw = y * Mathf.Rad2Deg; }
        float YawRad { get => ch.Yaw * Mathf.Deg2Rad; set => ch.Yaw = value * Mathf.Rad2Deg; }
        Vector3 Fwd => new(Mathf.Sin(YawRad), 0, Mathf.Cos(YawRad));
        float S => ch.ScaleK;

        // [parceiro] Bug do deslize (Ruan, 31/10/2026): bot mirando e andando escorregava os pés.
        // 1) Com "remapSpeedToAI" as pernas do bot animavam com a metade da velocidade real e o
        //    FB_LocoSpeed só acelera a animação até 1,45x, então o pé no chão andava para trás.
        //    Agora as pernas usam a velocidade real ("aiLegsRealSpeed" no fb_data, padrão 1).
        // 2) Bot mirando andava até 4,8 m/s; a animação de correr mirando aguenta ~3,9 m/s.
        //    Agora mirando ele anda no máximo como o jogador mirando (speedAiming) e agachado no
        //    máximo "aiCrouchMaxSpeed" ("aiAimSpeedLikePlayer" no fb_data, padrão 1).
        // PassoTeste/MiraTeste: -1 segue o fb_data; 0/1 forçam (o FBTesteExe mede antes e depois).
        public static int PassoTeste = -1, MiraTeste = -1;
        static bool PassoReal => PassoTeste >= 0 ? PassoTeste == 1 : Json.F(GameData.Section("puppet"), "aiLegsRealSpeed", 1) > .5f;
        static bool LimiteMira => MiraTeste >= 0 ? MiraTeste == 1 : Json.F(GameData.Section("puppet"), "aiAimSpeedLikePlayer", 1) > .5f;

        // ---- percepção: FBBotSense.Visibility (campo de visão, 3 raios a partir do olho DENTRO do corpo, postura, movimento, disparo, noite)
        Vector3? FindCover(Vector3 threat)
        {
            Vector3? best = null; float bs = -1;
            foreach (var dist in new[] { 4f, 6, 8 }) for (int a = 0; a < 360; a += 45)
                {
                    float r = a * Mathf.Deg2Rad, cx = ch.Pos.x + Mathf.Sin(r) * dist, cz = ch.Pos.z + Mathf.Cos(r) * dist;
                    if (!(FBWorld.GroundY(cx, ch.Pos.y + 1, cz, .3f, 3) is float gy)) continue;
                    if (!FBNav.DentroLimite(new Vector3(cx, gy, cz), 1.5f)) continue;     // [parceiro] presos v4 (revisão): atrás da parede invisível da arena não é cobertura (a parede conta como sólida)
                    var cp = new Vector3(cx, gy + 1, cz); var to = (threat - cp).normalized;
                    if (!FBWorld.RayHit(cp, to, (threat - cp).magnitude - .3f, .15f)) continue;
                    float sc = (10 - (cp - ch.Pos).magnitude) * .4f + (cp - threat).magnitude * .15f; if (sc > bs) { bs = sc; best = new Vector3(cx, gy, cz); }
                }
            return best;
        }
        public Vector3 FlankPoint(Vector3 p)
        {
            if (Mathf.Abs(laneOffset) < 2) return p; var to = (p - ch.Pos).Flat(); float d = to.magnitude; if (d < 6) return p; to /= d;
            var q = p + new Vector3(to.z, 0, -to.x) * (-laneOffset) * Mathf.Min(1, d / 40);      // espelho do X do web: o lado do flanco se mantém
            // [parceiro] presos v4 (revisão): o NavMesh a até 8 m pode ser o de fora da parede da arena
            return FBNav.Find(q, 8, out var at) && FBNav.DentroLimite(at, 1.5f) ? at : p;
        }
        // r47: no Clássico/4X4 todo destino de patrulha fica dentro da área de combate (FBArena.Keep)
        Vector3 PickPatrolGoal() { var g = PickPatrolGoal0(); var ar = FBMatch.I ? FBMatch.I.arena : null; return ar != null ? ar.Keep(g) : g; }
        Vector3 PickPatrolGoal0()
        {
            // rodada 38: o OBJETIVO do modo vem primeiro (cada bot tem o seu no Tático); a informação do time (último inimigo visto) só puxa quem
            // não tem objetivo — antes todo o time corria para o mesmo ponto de informação e parecia seguir o jogador
            var mg = FBMatch.I?.mode?.Goal(ch); if (mg is Vector3 g) return FlankPoint(g);
            if (FBBotBrain.FreshIntel(ch.team) is Vector3 ip && (ip - ch.Pos).magnitude < 120) return FlankPoint(ip);
            if (ch.team < 2 && Random.value < .55f && FBMatch.I)
            {
                var es = FBMatch.I.TeamSpawns(1 - ch.team); if (es.Count > 0) { var c = es.Aggregate(Vector3.zero, (a, b) => a + b) / es.Count; var mid = Vector3.Lerp(c, ch.Pos, FBUtil.Rnd(.15f, .55f)); if (FBNav.RandomPoint(mid, FBUtil.Rnd(14, 30)) is Vector3 np) return FlankPoint(np); return FlankPoint(mid); }
            }
            if (FBNav.RandomPoint(ch.Pos, FBUtil.Rnd(30, 85)) is Vector3 p) return p; if (FBNav.RandomPoint() is Vector3 p2) return p2;
            var wp = FBMatch.I?.Map?.waypoints; if (wp != null && wp.Count > 0) { var cand = wp.Where(w => (w - ch.Pos).Flat().magnitude > 18).ToList(); var c = cand.Count > 0 ? FBUtil.Pick(cand) : FBUtil.Pick(wp); return c + new Vector3(FBUtil.Rnd(-2, 2), 0, FBUtil.Rnd(-2, 2)); }
            return home + new Vector3(FBUtil.Rnd(-25, 25), 0, FBUtil.Rnd(-25, 25));
        }
        // próximo ponto rumo a "desired": direto se há linha livre, senão pelo caminho da navmesh
        Vector3? Steer(Vector3? desired, float dt)
        {
            if (desired is not Vector3 d) return null; navT -= dt;
            if (path == null || navT <= 0 || pathGoal == null || (pathGoal.Value - d).magnitude > 3) { navT = 2 + Random.value; pathGoal = d; path = FBNav.Path(ch.Pos, d) ?? new List<Vector3> { d }; }
            while (path.Count > 1 && (path[0] - ch.Pos).Flat().magnitude < 1.3f) path.RemoveAt(0);
            // [parceiro] presos 01/11: no modo estrito (bot que travou há pouco, ver Estrito) não corta canto pelo ClearLine
            if (!Estrito && path.Count > 1 && FBWorld.ClearLine(ch.Pos, path[1])) path.RemoveAt(0);
            return path.Count > 0 ? path[0] : d;
        }

        // ---- presos (parceiro, 01/11 a 02/11): vigia de progresso, ilha do NavMesh, destino seguro, saída a pé e realocação escondida

        // [parceiro] presos 01/11: estados em que o bot anda para um destino dele (patrulha/esquadrão/caça e busca). Combate, cobertura
        // (com o espiar), fuga, guarda e escolta têm regra e tempo próprios e não contam como preso.
        bool Vigiado => ai switch { "combat" or "cover" or "flee" or "guard" or "escort" or "dummy" or "victim" or "knocked" => false, _ => true };

        // [parceiro] presos 01/11: modo estrito, por 8 s depois de cada segundo travado: segue os cantos do caminho do NavMesh, sem
        // cortar canto pelo ClearLine e sem a reta dos 12 m. O ClearLine são 2 raios finos (0,6 e 1,5 m do pé) que passam
        // entre os postes e por baixo do corrimão do deck da R6 e pela face de trás do alambrado; a cápsula de 1,8 m não passa.
        bool Estrito => Time.time < vigEstritoAte && Vigiado;

        static NavMeshPath Caminho => caminho ??= new NavMeshPath();

        // [parceiro] presos 01/11: no máximo 1 consulta pesada ao NavMesh por quadro somando todos os bots: a busca da âncora para célula
        // ainda não vista (com o ponto numa ilha ela varre a ilha principal até acabar os nós) e a escalada do vigia (dezenas de
        // SamplePosition e alguns CalculatePath). Quem não pega a vaga espera 1 quadro (ilha, destino) ou 1 tique (escalada).
        static bool PodePesado()
        {
            int f = Time.frameCount; if (f == pesadoQuadro) return false;
            pesadoQuadro = f; return true;
        }

        // [parceiro] presos 01/11: vigia de progresso. Roda todo quadro no fim do Update normal (compra/freeze, caído, remédio,
        // levantando, dummy e vítima retornam antes e não chegam aqui; um buraco de mais de 2 s zera tudo).
        // - "Vale" = patrulha/esquadrão/caça ou busca, no chão, sem inimigo à vista e sem reanimar (indo até o caído ou
        //   ajoelhado). Só nesses estados o vigia escala e teleporta.
        // - A cada 1 s: se o bot quis andar (vale, velocidade > 1, com destino) em 70% do tique e andou menos de 0,4 m, soma
        //   1 s "travado"; querendo andar, soma 1 s "sem progresso". Progresso de verdade zera tudo: sair 5 m de onde o
        //   episódio começou ou chegar 3 m mais perto do destino que persegue. O desvio de 2,5 m do stuckN 4 não é progresso,
        //   então não zera mais a escalada (antes o desvio "chegava", vinha destino novo com pausa, a pausa zerava o stuckN e
        //   os passos 6 e 10 nunca rodavam).
        // - 4 s travado (ou 15 s sem progresso): em ilha do NavMesh realoca para a saída; senão destino novo que o NavMesh
        //   alcança. 8 s travado (ou 30 s sem progresso): realoca (ilha -> saída; fora do NavMesh -> NavMesh mais perto;
        //   preso com caminho completo -> 3,5 m à frente no caminho; senão ponto do NavMesh livre perto).
        // - Com âncora (10x10), a cada 2 s: fora da ilha principal duas vezes seguidas (cache de células do FBNav, conferido
        //   de perto) -> realoca para o ponto da ilha principal mais perto.
        // - Realocar = teleporte só se a câmera não vê o bot nem o ponto (Escondido); vendo, tenta a cada 1 s. A pendência de
        //   bot travado é descartada se ele entra em combate, cobertura, fuga ou reanimação; a saída de ilha espera.
        // Custo: sem consulta ao NavMesh por quadro. Por bot, 1 SamplePosition e 1 CalculatePath curto a cada 2 s e por
        // destino novo; a busca cara da âncora só para célula nova, com no máximo 1 por quadro somando todos os bots.
        void Vigia(float dt, float quer, Vector3? dest)
        {
            float time = Time.time;
            if (time - vigUlt > 2)
            {
                VigiaZera(); realocAlvo = null; ilhaN = 0;
                vigT = time + Random.Range(.6f, 1.4f); ilhaT = time + Random.Range(1f, 3f);
            }
            vigUlt = time;
            if (ch.isPlayer || ch.special || ch.vehiclePose || !cc || !cc.enabled || FBMatch.I.freeze) return;
            // [parceiro] presos 01/11: reanimando = indo até o caído (o destino é ele) ou ajoelhado ao lado dele neste quadro (rescuing)
            bool reanimando = rescuing || (dest.HasValue && reviveTgt && (reviveTgt.Pos - dest.Value).sqrMagnitude < .04f);
            bool vale = Vigiado && !reanimando && onGround && !sees;
            vigDur += dt;
            if (vale && quer > 1 && dest.HasValue) { vigQuer += dt; vigTemDest = true; vigUltDest = dest.Value; }
            if (FBNav.HasAnchor && time >= ilhaT) { ilhaT = time + 2; Ilha(vale, time); }
            if (time >= vigT) { vigT = time + 1; Tique(time, vale); }
        }

        void VigiaZera()
        {
            var pos = ch.Pos; vigP0 = pos; vigPos = pos;
            vigDur = 0; vigQuer = 0; vigTemDest = false; vigAcomp = false; vigBloq = 0; vigTotal = 0; vigNivel = 0;
        }

        void Tique(float time, bool vale)
        {
            var pos = ch.Pos; float dur = vigDur;
            bool quis = dur > .1f && vigQuer >= .7f * dur;
            float andou = (pos - vigPos).Flat().magnitude;
            bool progresso = (pos - vigP0).Flat().magnitude >= 5;
            bool temDest = vigTemDest; float dDest = 0;
            if (temDest)
            {
                dDest = (vigUltDest - pos).Flat().magnitude;
                if (!vigAcomp || (vigUltDest - vigDest).Flat().magnitude > 3) { vigAcomp = true; vigDest = vigUltDest; vigBase = dDest; }
                else if (dDest <= vigBase - 3) progresso = true;
            }
            vigPos = pos; vigDur = 0; vigQuer = 0; vigTemDest = false;
            if (realocAlvo.HasValue && (pos - realocDe).Flat().magnitude > 3)
            {
                // [parceiro] presos 01/11: saiu de onde o ponto foi calculado; recalcula mais tarde
                realocAlvo = null; vigNivel = Mathf.Min(vigNivel, 1); vigBloq = Mathf.Min(vigBloq, 4); vigTotal = Mathf.Min(vigTotal, 15);
            }
            // [parceiro] revisão 2: a realocação do stuckN 10 (tipo 3) expira em 10 s
            if (realocAlvo.HasValue && realocTipo == 3 && time > realocAte) realocAlvo = null;
            if (realocAlvo.HasValue && realocTipo != 0 && realocTipo != 3 && !vale)
            {
                // [parceiro] presos 01/11: em combate, cobertura, fuga, vendo inimigo ou reanimando, a realocação de bot travado (tipo 1) é
                // descartada (o travado era da patrulha); a saída de ilha (tipo 0) espera ele voltar a patrulhar ou buscar
                realocAlvo = null; vigNivel = Mathf.Min(vigNivel, 1); vigBloq = Mathf.Min(vigBloq, 4);
            }
            if (progresso)
            {
                vigP0 = pos; vigBase = dDest; vigAcomp = vigAcomp && temDest; vigBloq = 0; vigTotal = 0; vigNivel = 0;
                if (realocTipo == 1 || realocTipo == 3) realocAlvo = null;
                // [parceiro] presos 01/11: andar não tira ninguém de ilha: a saída pendente continua sendo tentada (só se vale)
                if (vale) TentaRealocar();
                return;
            }
            if (quis) { vigTotal += dur; if (andou < .4f) { vigBloq += dur; vigEstritoAte = time + 8; } }
            if (!vale)
            {
                // [parceiro] presos 01/11: escalada e teleporte só em patrulha/esquadrão/busca, no chão, sem inimigo à vista e sem reanimar
                // revisão 2: menos a realocação pedida pelo stuckN 10 (combate travado), tentada enquanto a câmera não vê
                if (realocAlvo.HasValue && realocTipo == 3) TentaRealocar();
                return;
            }
            if (realocAlvo.HasValue) TentaRealocar();
            else if (vigNivel == 0 && (vigBloq >= 4 || vigTotal >= 15) && PodePesado())
            {
                vigNivel = 1; vigEstritoAte = time + 12;
                if (IlhaSaida(out var saida)) { Pendente(saida, 0, "ilha do NavMesh (vigia, travado)"); TentaRealocar(); }
                else NovoDestino();
            }
            else if (vigNivel == 1 && (vigBloq >= 8 || vigTotal >= 30) && PodePesado())
            {
                vigNivel = 2; vigEstritoAte = time + 12;
                if (AlvoRealocar(out var alvo, out var tipo, out var porque)) { Pendente(alvo, tipo, porque); TentaRealocar(); }
                else
                {
                    // [parceiro] presos 01/11: sem ponto para realocar: destino novo e tenta de novo depois de mais 4 s travado
                    NovoDestino(); vigNivel = 1; vigBloq = 4; vigTotal = Mathf.Min(vigTotal, 15);
                }
            }
        }

        // [parceiro] presos 01/11: com âncora, a cada 2 s. Pelo cache de células do FBNav (conferido de perto, ver Principal) o bot está
        // fora da ilha principal duas vezes seguidas -> realoca para o ponto da ilha principal mais perto (até 25 m). Célula
        // nova, ou célula que pega a cerca com o bot do outro lado, precisa da busca da âncora: pega a vaga do quadro
        // (PodePesado) ou tenta no quadro seguinte.
        void Ilha(bool vale, float time)
        {
            if (!vale || realocAlvo.HasValue || (time < ilhaOkT && (ch.Pos - ilhaOkP).Flat().magnitude < 3)) return;
            int r = Principal(ch.Pos, 2, out var at, fresco: false, soCache: true);
            if (r == -2)
            {
                if (!PodePesado()) { ilhaT = time + .05f; return; }
                r = Principal(ch.Pos, 2, out at, fresco: false);
            }
            if (r != 0) { ilhaN = 0; return; }
            if (++ilhaN < 2) return;
            ilhaN = 0;
            if ((at - ch.Pos).magnitude <= 25) { Pendente(at, 0, "ilha do NavMesh"); TentaRealocar(); return; }
            // [parceiro] presos 01/11: saída longe demais: não testa de novo aqui por 10 s
            ilhaOkP = ch.Pos; ilhaOkT = time + 10;
        }

        // [parceiro] presos 01/11: com âncora, destino guardado (goalP da patrulha, lastSeen da busca/combate sem ver, coverPos) fora da
        // ilha principal vira o ponto da ilha principal mais perto dele. Nunca devolve destino novo cru: célula já vista pelo
        // FBNav.Inside classifica na hora (1 SamplePosition e 1 CalculatePath curto); célula nova precisa da busca da âncora,
        // no máximo 1 por segundo por bot (cobertura não espera) e 1 por quadro somando os bots. Até lá, perto (8 m) do último
        // destino "fora" usa a mesma saída (o Alert troca o goalP a cada tiro); senão vai pelo Steer, sem a reta dos 12 m.
        // Chegando a 2 m do ponto dá o destino por alcançado (patrulha sorteia outro, busca termina). Cobertura fora da ilha
        // principal é cancelada sempre que aparece (era a armadilha) e o FindCover fica vetado por 4 s.
        Vector3 DestinoSeguro(Vector3 d)
        {
            if (!FBNav.HasAnchor || ch.isPlayer) return d;
            int tipo = 0;   // 1 = goalP da patrulha, 2 = lastSeen da busca/combate sem ver, 3 = coverPos
            if (ai == "cover") { if (coverPos.HasValue && (coverPos.Value - d).sqrMagnitude < .01f) tipo = 3; }
            else if (ai == "search" || (ai == "combat" && !sees)) { if (lastSeen.HasValue && (lastSeen.Value - d).sqrMagnitude < .01f) tipo = 2; }
            else if (Vigiado && goalP.HasValue && (goalP.Value - d).sqrMagnitude < .01f) tipo = 1;
            if (tipo == 0) return d;
            // [parceiro] presos v4 (revisão): destino fora do quadrado da arena é "fora" já (o FBNav.Inside recusa sem guardar célula, então a
            // Celula dava 2 e o destino passava cru; o bot parava a 1,5 m da parede sem nunca "chegar"): o ponto de troca é o
            // NavMesh perto do ponto puxado para dentro, e vale a regra de baixo (cobertura cancelada, patrulha/busca chegam a 2 m)
            if (FBNav.TemLimite && !FBNav.DentroLimite(d, 1))
            {
                if (!destTem || !destLimite || (d - destK).sqrMagnitude > 1)
                {
                    var dentro = FBNav.PraDentro(d, 1.5f);
                    if (NavMesh.SamplePosition(dentro, out var hd, 3, NavMesh.AllAreas) && FBNav.DentroLimite(hd.position, 1)) dentro = hd.position;
                    // [parceiro] revisão 2: o ponto junto da parede pode cair num quintal/quadra fechado (ilha): vai para a saída da ilha
                    // principal mais perto (só com a célula já vista ou com a vaga pesada do quadro)
                    if (Principal(dentro, 3, out var atDentro, fresco: false, soCache: !PodePesado()) == 0 && (atDentro - dentro).Flat().magnitude <= 30 && FBNav.DentroLimite(atDentro, 1)) dentro = atDentro;
                    destTem = true; destLimite = true; destK = d; destSub = dentro; destFora = true;
                    if (tipo != 3) Evitados[0]++;
                }
            }
            else if (!destTem || destLimite || (d - destK).sqrMagnitude > 1)
            {
                int r = Principal(d, 3, out var at, fresco: false, soCache: true);
                if (r == -2)
                {
                    float time = Time.time;
                    if ((time < destT && tipo != 3) || !PodePesado())
                    {
                        // [parceiro] presos 01/11: cobertura só espera a vaga do quadro (pendente 1 quadro), não reaproveita
                        if (tipo == 3 || !destTem || !destFora || destLimite || (d - destK).Flat().magnitude >= 8) { destPendente = true; return d; }
                        r = -3;   // [parceiro] presos 01/11: reaproveita destSub do último destino "fora" (destK fica o mesmo)
                    }
                    else { destT = time + Random.Range(.8f, 1.25f); r = Principal(d, 3, out at, fresco: false); }
                }
                if (r != -3)
                {
                    destTem = true; destLimite = false; destK = d; destSub = at; destFora = r == 0 && (at - d).Flat().magnitude <= 30;
                    if (destFora && tipo != 3) Evitados[0]++;
                }
            }
            if (!destFora) return d;
            if (tipo == 3)
            {
                // [parceiro] presos 01/11: cobertura fora da ilha principal: cancela sempre (também com o ponto já classificado, porque o
                // FindCover devolve o mesmo ponto a cada recarga) e veta o FindCover por 4 s. Antes o bot ficava de pé sem
                // atirar rente à cerca até o coverT acabar, ou alternava cobertura e combate a cada 0,2 s.
                coverT = 0; coverVetoT = Time.time + 4; Evitados[1]++;
                return destSub;
            }
            if ((destSub - ch.Pos).Flat().magnitude < 2) { if (tipo == 1) goalT = 0; else if (tipo == 2 && ai == "search") searchT = 99; }
            return destSub;
        }

        // [parceiro] presos v4: tamanho do desvio de lado do stuckN 4 (passa do raio de chegada de 2,5 m da patrulha)
        const float DesvioLado = 3.2f;

        // [parceiro] presos 01/11: o desvio de lado do stuckN 4 sorteia o lado. Se o lado sorteado bate na borda do NavMesh em 2,5 m
        // e o outro não, vai pelo outro. Os dois livres ou os dois bloqueados: igual a antes. 1 SamplePosition e até
        // 2 NavMesh.Raycast por desvio.
        float LadoLivre(float lado)
        {
            if (!NavMesh.SamplePosition(ch.Pos, out var h, 1, NavMesh.AllAreas)) return lado;
            // [parceiro] presos v4: 3,2 m (era 2,5 m, igual ao raio de chegada da patrulha: o desvio "chegava" na hora) e lado que sai do
            // quadrado da arena conta como bloqueado
            var v = new Vector3(Mathf.Cos(YawRad), 0, -Mathf.Sin(YawRad)) * DesvioLado;
            if (!NavMesh.Raycast(h.position, h.position + v * lado, out _, NavMesh.AllAreas) && FBNav.DentroLimite(h.position + v * lado, .6f)) return lado;
            if (!NavMesh.Raycast(h.position, h.position - v * lado, out _, NavMesh.AllAreas) && FBNav.DentroLimite(h.position - v * lado, .6f)) return -lado;
            return lado;
        }

        // [parceiro] presos v4: ponto livre para o bot travado sair andando, sem teleporte. No NavMesh, ligado ao pé do bot em reta pelo
        // NavMesh (NavMesh.Raycast sem borda no meio), dentro do quadrado da arena, com a cápsula livre (Livre: parede, objeto e
        // outro personagem) e sem nada sólido na reta a 0,4 m e a 1,2 m do chão. Tenta primeiro para trás do que ele vinha
        // tentando andar, depois os lados e as diagonais, a 2,2 m e depois a 1,4 m; a cada vez começa por um lado diferente.
        // No pior caso 16 SamplePosition, 16 NavMesh.Raycast, 16 Livre e 32 raios, só no stuckN 4 (fora da patrulha) e 10.
        static readonly float[] SAIDA_ANG = { 180, 90, -90, 135, -135, 45, -45, 0 };
        bool SaidaLivre(out Vector3 p)
        {
            p = ch.Pos;
            if (!NavMesh.SamplePosition(ch.Pos, out var h, 1, NavMesh.AllAreas)) return false;
            var pe = h.position; var vf = vel.Flat();
            float baseA = vf.sqrMagnitude > .01f ? Mathf.Atan2(vf.x, vf.z) : YawRad, giro = saidaGiro++ % 2 == 0 ? 1 : -1, s = S;
            for (int i = 0; i < 2; i++)
            {
                float r = i == 0 ? 2.2f : 1.4f;
                foreach (var ang in SAIDA_ANG)
                {
                    float f = baseA + ang * giro * Mathf.Deg2Rad;
                    if (!NavMesh.SamplePosition(pe + new Vector3(Mathf.Sin(f), 0, Mathf.Cos(f)) * r, out var h2, .5f, NavMesh.AllAreas)) continue;
                    var q = h2.position; var to = (q - ch.Pos).Flat(); float l = to.magnitude;
                    if (l < .8f || Mathf.Abs(q.y - pe.y) > 1.2f || NavMesh.Raycast(pe, q, out _, NavMesh.AllAreas) || !FBNav.DentroLimite(q, 1.2f) || !Livre(q)) continue;
                    to /= l;
                    if (FBWorld.RayHit(ch.Pos + Vector3.up * .4f * s, to, l, .05f) || FBWorld.RayHit(ch.Pos + Vector3.up * 1.2f * s, to, l, .05f)) continue;
                    p = q; return true;
                }
            }
            return false;
        }

        // [parceiro] presos v4: manda o bot andar até p por até 1,6 s (ver o bloco da saída antes do DestinoSeguro), conta e escreve no log
        void Sai(Vector3 p, int tipo, string porque)
        {
            saidaP = p; saidaAte = Time.time + 1.6f; path = null; navT = 0;
            Evitados[Mathf.Clamp(tipo, 0, Evitados.Length - 1)]++;
            Debug.Log($"[BURITIRO] presos: {ch.name} saída a pé por {porque}: de ({ch.Pos.x:0.0}, {ch.Pos.y:0.00}, {ch.Pos.z:0.0}) para ({p.x:0.0}, {p.y:0.00}, {p.z:0.0}), {Vector3.Distance(ch.Pos, p):0.0} m · ai {ai} · sem teleporte");
        }

        // [parceiro] presos 01/11: 1 = p está na mesma parte do NavMesh que "de" (at = p no NavMesh); 0 = não está (at = ponto da parte
        // de "de" mais perto de p: fim do caminho parcial de "de" até p); -1 = sem NavMesh perto de um dos dois.
        static int MesmaParte(Vector3 de, Vector3 p, float r, out Vector3 at)
        {
            at = p;
            if (!NavMesh.SamplePosition(de, out var h, 3, NavMesh.AllAreas) || !NavMesh.SamplePosition(p, out var h2, r, NavMesh.AllAreas)) return -1;
            at = h2.position; var np = Caminho;
            if (!NavMesh.CalculatePath(h.position, h2.position, NavMesh.AllAreas, np)) return -1;
            if (np.status == NavMeshPathStatus.PathComplete) return 1;
            if (np.status != NavMeshPathStatus.PathPartial) return -1;
            var cs = np.corners; if (cs.Length == 0) return -1;
            at = cs[cs.Length - 1]; return 0;
        }

        // [parceiro] presos 01/11: p na ilha principal (a da âncora)? 1 = sim (at = p no NavMesh); 0 = não (at = ponto da ilha principal
        // mais perto de p); -1 = sem âncora ou sem NavMesh perto; -2 = só com soCache: precisa da busca da âncora (cara).
        // fresco = MesmaParte da âncora até p, sem cache. Sem fresco usa a célula do FBNav (Celula) e confere p de perto,
        // porque a célula de 4x4x6 m vale para o ponto que a decidiu e pode pegar os dois lados da cerca: célula dentro ->
        // caminho de p até esse ponto (curto); célula fora -> caminho de p até o fim guardado (de dentro de uma ilha a busca só
        // anda pela ilha, pequena). Em todo "não" o caminho de p até o ponto achado também tem que dar parcial: se chega lá, o
        // parcial da âncora era falta de nós da busca num caminho longo, não ilha.
        static int Principal(Vector3 p, float r, out Vector3 at, bool fresco = true, bool soCache = false)
        {
            at = p;
            if (FBNav.Anchor is not Vector3 anchor) return -1;
            if (!fresco)
            {
                int c = FBNav.Celula(p, r, soCache, out var hp, out var refP, out var fimP);
                if (c == 3) return -2;
                if (c == -1 || c == 2) { at = hp; return -1; }
                var np = Caminho;
                if (c == 1)
                {
                    if (NavMesh.CalculatePath(hp, fimP, NavMesh.AllAreas, np) && np.status == NavMeshPathStatus.PathComplete) { at = hp; return 1; }
                    at = fimP; return 0;
                }
                if (NavMesh.CalculatePath(hp, refP, NavMesh.AllAreas, np) && np.status == NavMeshPathStatus.PathComplete) { at = hp; return 1; }
                // [parceiro] presos 01/11: célula dentro, mas p não chega no ponto que a decidiu: célula que pega a cerca, p do outro lado
                if (soCache) return -2;
            }
            int m = MesmaParte(anchor, p, r, out at);
            if (m != 0) return m;
            if (NavMesh.SamplePosition(p, out var h, r, NavMesh.AllAreas) && NavMesh.CalculatePath(h.position, at, NavMesh.AllAreas, Caminho) && Caminho.status == NavMeshPathStatus.PathComplete) { at = h.position; return 1; }
            return 0;
        }

        // [parceiro] presos 01/11: caminho completo do NavMesh de a até b (1 CalculatePath)
        static bool Alcanca(Vector3 a, Vector3 b) =>
            NavMesh.SamplePosition(a, out var h, 2, NavMesh.AllAreas) && NavMesh.SamplePosition(b, out var h2, 4, NavMesh.AllAreas)
            && NavMesh.CalculatePath(h.position, h2.position, NavMesh.AllAreas, Caminho) && Caminho.status == NavMeshPathStatus.PathComplete;

        // [parceiro] presos 01/11: bot em ilha do NavMesh? saida = ponto da parte principal mais perto dele (até 25 m). Com âncora:
        // Principal sem cache (pega também célula rotulada "dentro" por engano). Sem âncora (outros modos): ilha = não chega
        // nem em casa (onde nasceu) nem num ponto sorteado do NavMesh.
        bool IlhaSaida(out Vector3 saida)
        {
            var pos = ch.Pos; saida = pos;
            if (FBNav.HasAnchor) return Principal(pos, 2, out saida) == 0 && (saida - pos).magnitude <= 25;
            if (FBNav.RandomPoint() is not Vector3 rp) return false;
            if (MesmaParte(home, pos, 2, out var at) != 0 || MesmaParte(rp, pos, 2, out var at2) != 0) return false;
            saida = (at2 - pos).sqrMagnitude < (at - pos).sqrMagnitude ? at2 : at;
            return (saida - pos).magnitude <= 25 && !Alcanca(pos, saida);
        }

        // [parceiro] presos 01/11: destino novo que o NavMesh alcança daqui (o da patrulha/modo ou um ponto sorteado a 15-35 m). Na
        // busca, encerra a busca (olha em volta e volta à patrulha).
        void NovoDestino()
        {
            path = null; navT = 0; pauseT = 0;
            if (ai == "search") { searchT = 99; return; }
            var g = PickPatrolGoal();
            if (!Alcanca(ch.Pos, g) && FBNav.RandomPoint(ch.Pos, FBUtil.Rnd(15, 35)) is Vector3 rp && Alcanca(ch.Pos, rp)) g = rp;
            goalP = g; goalT = FBUtil.Rnd(8, 16);
        }

        // [parceiro] presos 01/11: ponto a d metros do começo, andando pelos cantos do caminho
        static Vector3 AoLongo(Vector3[] c, float d)
        {
            for (int i = 1; i < c.Length; i++)
            {
                float l = Vector3.Distance(c[i - 1], c[i]);
                if (l >= d) return Vector3.Lerp(c[i - 1], c[i], d / Mathf.Max(l, .001f));
                d -= l;
            }
            return c[c.Length - 1];
        }

        // [parceiro] presos 01/11: para onde realocar um bot travado há 8 s
        bool AlvoRealocar(out Vector3 alvo, out int tipo, out string porque)
        {
            var pos = ch.Pos; tipo = 0; porque = "ilha do NavMesh (vigia, travado)";
            if (IlhaSaida(out alvo)) return true;
            tipo = 1;
            if (!NavMesh.SamplePosition(pos, out var h, .8f, NavMesh.AllAreas))
            {
                porque = "fora do NavMesh (NavMesh mais perto, até 4 m)";
                if (FBNav.Find(pos, 4, out alvo) && (alvo - pos).magnitude >= .4f) return !FBNav.HasAnchor || Principal(alvo, 1, out _) != 0;
                return false;
            }
            if (vigAcomp && NavMesh.SamplePosition(vigDest, out var h2, 4, NavMesh.AllAreas) && NavMesh.CalculatePath(h.position, h2.position, NavMesh.AllAreas, Caminho) && Caminho.status == NavMeshPathStatus.PathComplete)
            {
                var cs = Caminho.corners;
                if (cs.Length > 1)
                {
                    alvo = AoLongo(cs, 3.5f);
                    if ((alvo - pos).Flat().magnitude >= 1.5f) { porque = "travado com caminho completo (3,5 m à frente no caminho do NavMesh)"; return true; }
                }
            }
            if (FBNav.RandomPoint(pos, 4) is Vector3 rp && (rp - pos).Flat().magnitude >= 1.5f && Alcanca(pos, rp)) { alvo = rp; porque = "travado (ponto do NavMesh a até 4 m que o caminho alcança)"; return true; }
            alvo = pos; porque = null; return false;
        }

        void Pendente(Vector3 alvo, int tipo, string porque) { realocAte = Time.time + 10; realocAlvo = alvo; realocDe = ch.Pos; realocTipo = tipo; realocPorque = porque; }

        // [parceiro] presos 01/11: como a câmera vê o ponto p (1,2 m acima do pé). aparente = distância com o zoom: com luneta o FOV da
        // câmera cai (4x, 6x) e 60 m parecem 15 m; referência = FOV normal do jogador (FBPlayer.FovBase), sem ampliar com FOV
        // mais aberto. Devolve se p está na tela (na frente da câmera, com 15% de folga nas bordas).
        static bool Vista(Camera cam, Vector3 p, out float aparente)
        {
            var o = cam.transform.position; float fov0 = FBPlayer.I ? FBPlayer.I.FovBase : 60;
            float k = Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad) / Mathf.Tan(Mathf.Clamp(fov0, 20, 150) * .5f * Mathf.Deg2Rad);
            aparente = (p - o).magnitude * Mathf.Min(1, k);
            var v = cam.WorldToViewportPoint(p + Vector3.up * 1.2f);
            return v.z > 0 && v.x >= -.15f && v.x <= 1.15f && v.y >= -.15f && v.y <= 1.15f;
        }

        // [parceiro] presos 01/11: a câmera não vê o salto de p: sem câmera, p fora da tela, peito E cabeça de p atrás de algo sólido
        // (poste ou tronco fino no meio não basta), ou p longe com salto pequeno para a distância. salto = quanto o bot anda
        // no teleporte. O teleporte antigo do stuckN 10 continua com a regra dele (bloco do stuckN sem mudança).
        static bool Escondido(Vector3 p, float salto)
        {
            var cam = Camera.main; if (!cam) return true;
            // [parceiro] presos 01/11 (v3): longe (40 m aparentes ou mais, com o zoom) só esconde salto até 6,25% da distância (2,5 m a
            // 40 m, como o stuckN 10). Antes os 40 m bastavam para qualquer salto: a saída de ilha do meio da quadra (6 a
            // 25 m) e os 3,5 m do vigia piscavam no meio da tela. Salto maior espera p sair da tela ou ficar atrás de algo.
            if (!Vista(cam, p, out var aparente) || (aparente >= 40 && salto <= aparente * .0625f)) return true;
            return Atras(cam.transform.position, p);
        }

        // [parceiro] presos 01/11: peito (1,0 m) e cabeça (1,7 m) de p atrás de algo sólido vistos de o (a câmera)
        static bool Atras(Vector3 o, Vector3 p)
        {
            float l = (p - o).magnitude;
            return FBWorld.RayHit(o, (p + Vector3.up * 1f - o).normalized, l - .5f) && FBWorld.RayHit(o, (p + Vector3.up * 1.7f - o).normalized, l - .5f);
        }

        // [parceiro] presos 01/11: cabe a cápsula do bot no ponto (de 0,32 m a 1,8 m acima do chão)? Personagens ficam na camada 10,
        // fora da SolidMask: o CheckCapsule não vê nenhum (nem o próprio bot) e eles são conferidos à parte.
        bool Livre(Vector3 p)
        {
            float s = S;
            if (Physics.CheckCapsule(p + Vector3.up * .62f * s, p + Vector3.up * 1.5f * s, .3f * s, FBWorld.SolidMask, QueryTriggerInteraction.Ignore)) return false;
            // [parceiro] presos 01/11 (v3): ponto ocupado por personagem: outro bot, civil ou o jogador com o pé a menos de 0,8 m (os
            // dois raios de 0,32 m, com folga) e a menos de 1,8 m de altura. Sem isto os bots presos na mesma célula recebiam
            // o mesmo ponto de saída (FBNav.Celula) e o segundo caía dentro do primeiro. Morto e caído contam: o
            // CharacterController deles continua ligado no lugar (FBCharacter.Die não desliga). 1 volta em FBMatch.All.
            var all = FBMatch.All; float r = .8f * s;
            for (int i = 0; i < all.Count; i++)
            {
                var o = all[i];
                if (o && o != ch && (o.Pos - p).Flat().sqrMagnitude < r * r && Mathf.Abs(o.Pos.y - p.y) < 1.8f * s) return false;
            }
            return true;
        }

        // [parceiro] presos 01/11: o ponto ou, se ele está apertado (parede a menos de 0,3 m, carro, objeto, personagem), um ponto do
        // NavMesh a 0,45 m dele em que a cápsula cabe. No pior caso 17 Livre, 16 SamplePosition e 8 NavMesh.Raycast, só na
        // hora de realocar.
        Vector3? Folga(Vector3 p)
        {
            if (Livre(p)) return p;
            for (int i = 0; i < 8; i++)
            {
                float f = i * (Mathf.PI / 4);
                if (NavMesh.SamplePosition(p + new Vector3(Mathf.Sin(f), 0, Mathf.Cos(f)) * .45f, out var h, .3f, NavMesh.AllAreas) && Livre(h.position)) return h.position;
            }
            // [parceiro] presos 01/11 (v3): personagem parado no ponto (corpo, caído, outro bot) pega os 8 vizinhos a 0,45 m. A 0,9 m ele
            // não pega mais; sem este anel a saída de ilha esperava o corpo sair, isto é, até o fim da rodada. Só vale vizinho
            // que o NavMesh liga ao ponto em reta (NavMesh.Raycast sem bater na borda): a 0,9 m o SamplePosition poderia pegar
            // o NavMesh do outro lado da cerca, de volta na ilha.
            for (int i = 0; i < 8; i++)
            {
                float f = i * (Mathf.PI / 4);
                if (NavMesh.SamplePosition(p + new Vector3(Mathf.Sin(f), 0, Mathf.Cos(f)) * .9f, out var h, .3f, NavMesh.AllAreas) && !NavMesh.Raycast(p, h.position, out _, NavMesh.AllAreas) && Livre(h.position)) return h.position;
            }
            return null;
        }

        void TentaRealocar()
        {
            if (realocAlvo is not Vector3 alvo) return;
            // [parceiro] presos 01/11 (v3): o tamanho do salto entra no Escondido (longe só esconde salto pequeno)
            float salto = (alvo - ch.Pos).magnitude;
            if (!Escondido(ch.Pos, salto) || !Escondido(alvo, salto)) return;
            if (Folga(alvo) is not Vector3 fim)
            {
                // [parceiro] presos 01/11: ponto ocupado. Saída de ilha espera (tenta a cada 1 s); o resto recalcula mais tarde.
                if (realocTipo != 0) { realocAlvo = null; vigNivel = Mathf.Min(vigNivel, 1); vigBloq = Mathf.Min(vigBloq, 4); }
                return;
            }
            if (fim != alvo)
            {
                // [parceiro] presos 01/11 (v3): a Folga tirou o ponto do lugar (até 0,9 m): confere de novo com o ponto e o salto finais;
                // vendo, espera como acima (tenta a cada 1 s)
                salto = (fim - ch.Pos).magnitude;
                if (!Escondido(ch.Pos, salto) || !Escondido(fim, salto)) return;
            }
            Realocar(fim, realocPorque, realocTipo);
        }

        void Realocar(Vector3 at, string porque, int tipo)
        {
            var de = ch.Pos;
            if (FBWorld.GroundY(at.x, at.y, at.z, .6f, .6f) is float gy) at.y = gy;
            FBMatch.Teleport(ch, at, ch.Yaw); vel = Vector3.zero;
            saidaP = null; saidaAte = -99;   // [parceiro] revisão: a saída a pé era do lugar antigo
            path = null; navT = 0; stuckN = 0; stuckWin = 0; lastPos = ch.Pos; realocAlvo = null;
            Contar(tipo, porque, de, at);
            VigiaZera(); ilhaN = 0; destTem = false;
            destT = -99;   // [parceiro] presos 01/11: o destino novo que vem depois da realocação é classificado já, sem esperar o limite de 1/s
            if (Vigiado) { goalP = PickPatrolGoal(); goalT = FBUtil.Rnd(10, 20); pauseT = 0; }
        }

        // [parceiro] presos 01/11: conta a realocação (Realocados/RealocadosPor) e escreve "[BURITIRO] presos: ..." no log, com o que a
        // câmera via (distância aparente e se estava na tela) do bot e do ponto
        void Contar(int tipo, string porque, Vector3 de, Vector3 para)
        {
            Realocados++; RealocadosPor[Mathf.Clamp(tipo, 0, RealocadosPor.Length - 1)]++;
            Debug.Log($"[BURITIRO] presos: {ch.name} realocado por {porque}: de ({de.x:0.0}, {de.y:0.00}, {de.z:0.0}) para ({para.x:0.0}, {para.y:0.00}, {para.z:0.0}), {Vector3.Distance(de, para):0.0} m · ai {ai} · câmera: {NaCamera(de, para)}");
        }

        static string NaCamera(Vector3 de, Vector3 para)
        {
            var cam = Camera.main; if (!cam) return "sem câmera";
            bool vDe = Vista(cam, de, out var aDe), vPara = Vista(cam, para, out var aPara);
            // [parceiro] presos 01/11 (v3): ponto na tela encoberto (peito e cabeça atrás de algo sólido) sai como "na tela atrás de algo"
            var o = cam.transform.position;
            string Onde(bool v, Vector3 p) => v ? (Atras(o, p) ? "na tela atrás de algo" : "na tela") : "fora da tela";
            return $"bot a {aDe:0} m aparentes {Onde(vDe, de)}, ponto a {aPara:0} m aparentes {Onde(vPara, para)}";
        }
        float RemapSpeed(float s)
        {
            var P = GameData.Section("puppet"); float pw = Json.F(P, "playerWalkSpeed", 4), pr = Json.F(P, "playerRunSpeed", 6.8f), aw = Json.F(P, "aiWalkAnimSpeed", 2), ar = Json.F(P, "aiRunAnimSpeed", 5);
            return s <= pw ? s / pw * aw : aw + (s - pw) / (pr - pw) * (ar - aw);
        }

        // ---- tiro (botFireSmart / botFireAtCadence)
        // tiro: mira no ponto do alvo que está À VISTA (peito, senão cabeça, senão quadril) com antecipação imperfeita, erro angular que
        // diminui com o tempo de mira (FBBotSense.AimErrorDeg) e o cone da própria arma (recuo/abertura). Sem sorteio de acerto: a bala real
        // (FBCombat) decide — de longe erra naturalmente. Antes: "acertou" = bala exatamente no alvo, 16 % de acerto a 100+ m.
        Vector3 AimTarget(FBCharacter t)
        {
            var pts = new Vector3[3]; FBBotSense.TargetPoints(t, pts);
            int k = (seenMask & 2) != 0 ? 1 : (seenMask & 1) != 0 ? 0 : 2;
            if (k == 1 && Time.time - acquireT > .8f && (seenMask & 1) != 0 && Random.value < FBBotSense.HeadAim) k = 0;   // cabeça só com a mira assentada
            float tof = (ch.MuzzlePos - pts[k]).magnitude / 280; float lead = FBCombat.DiffAcc * .85f;
            return pts[k] + t.velocity.Flat() * tof * lead;
        }
        void FireSmart(FBCharacter t)
        {
            ch.protUntil = 0; var mz = ch.MuzzlePos; var aimP = AimTarget(t); var dir0 = (aimP - mz).normalized; FBCombatStats.BotShot((t.Pos - ch.Pos).magnitude);
            bool moving = vel.Flat().magnitude > 1;
            var dir = FBBotSense.Scatter(dir0, FBBotSense.AimErrorDeg(ch, t, Time.time - acquireT, moving));
            wu.Fire(mz, dir, new FBRecoil.State { aiming = true, crouch = ch.crouch, moving = moving, zoom = 1, grip = 1 }, out _, 0);
            ch.lastShot = 0; ch.aimPoint = aimP; FBBotBrain.TeamIntel(ch.team, t.Pos);
        }
        void FireAtCadence(FBCharacter t, int bMin, int bMax, float pMin, float pMax)
        {
            var d = wu.Cur; if (d == null) return; bool auto = d.auto;
            if (auto && burst <= 0) burst = Random.Range(bMin, bMax); else if (!auto) burst = 0;
            FireSmart(t);
            if (auto) { burst = Mathf.Max(0, burst - 1); shotT = burst > 0 ? Mathf.Clamp(d.Interval, .08f, .35f) : FBUtil.Rnd(pMin, pMax) * FBCombat.DiffReact; }
            else shotT = FBUtil.Rnd(Mathf.Max(.55f, pMin), Mathf.Max(.9f, pMax)) * Mathf.Max(.8f, FBCombat.DiffReact);
        }

        // ---- reanimar aliado caído (botRevive)
        (Vector3? desired, float speed, Vector3? face)? Revive(float dt)
        {
            if (!FBMatch.KnockOn || ch.knocked || ch.team == 2) return null; float now = Time.time;
            bool Busy(FBCharacter o) => o.revivedBy != null && o.revivedBy != (object)ch && now - o.reviveTick < .3f;
            var kt = reviveTgt;
            if (!kt || !kt.alive || !kt.knocked || Busy(kt)) { kt = null; float bd = 40; foreach (var o in FBMatch.TeamOf(ch)) if (o.alive && o.knocked && !Busy(o)) { float d = (o.Pos - ch.Pos).magnitude; if (d < bd) { bd = d; kt = o; } } reviveTgt = kt; }
            if (!kt) return null; float dd = (kt.Pos - ch.Pos).magnitude;
            if ((safeT -= dt) <= 0) { safeT = .3f; safeOk = SafeToRevive(kt); }
            if (!safeOk) { if (kt.revivedBy == (object)ch) AbortRevive(); if (dd < 8) ReviveUnsafeBlocks++; return null; }                 // perigoso: luta/se protege primeiro, volta depois
            if (dd > 1.4f) return (kt.Pos, dd > 6 ? 5.6f : 3, kt.Pos);
            if (kt.revivedBy != (object)ch) { ReviveStarts++; float dm = 1e9f; foreach (var a in FBMatch.Actors()) if (a && a.alive && !a.knocked && FBMatch.Hostile(a, ch)) dm = Mathf.Min(dm, (a.Pos - kt.Pos).magnitude); if (dm < 22) ReviveHot++; }
            ch.crouch = false; kt.revivedBy = ch; kt.reviveTick = now; rescuing = true;                     // pose do Sigma (RescuePose no Update)
            if (FBMechanics.ReviveStep(kt, dt, FBSession.mode)) { FBCombat.CompleteRevive(kt, ch); reviveTgt = null; rescuing = false; }
            return (null, 0, kt.Pos);
        }
        /// <summary>Reanimar só com segurança (pedido do usuário, rodada 38: "nunca devem tentar reanimar durante uma troca de tiros próxima ou
        /// enquanto estiverem sob ataque"): sem tiro recebido há 5 s, sem trocar tiro com alvo a < 45 m, nenhum inimigo a < 22 m, ninguém a
        /// < 40 m com linha de tiro para o caído ou para quem socorre, sem disparo inimigo a < 25 m nos últimos 2,5 s.</summary>
        bool SafeToRevive(FBCharacter kt)
        {
            // 1ª versão (autoteste de conquista): "inimigo conhecido a < 60 m" bloqueava 700 vezes em 100 s e 5 de 9 bots ficavam caídos —
            // num mapa de conquista sempre há alguém a 60 m. A regra pedida é "troca de tiros PRÓXIMA ou sob ataque":
            float now = Time.time;
            if (now - hitT < 5 || ai == "flee") { ReviveWhy[0]++; return false; }                                   // levou tiro há pouco
            if ((ai == "combat" || ai == "cover" || sees) && target && target.alive && (target.Pos - ch.Pos).magnitude < 45) { ReviveWhy[1]++; return false; }   // trocando tiro perto
            if (FBBotBrain.lastNoiseP is Vector3 np && now - FBBotBrain.lastNoiseT < 2.5f && ((np - ch.Pos).magnitude < 25 || (np - kt.Pos).magnitude < 25))
            { bool own = FBMatch.TeamOf(ch).Any(o => o && o.alive && (o.Pos - np).magnitude < 1.5f && o.lastShot < 2.7f); if (!own) { ReviveWhy[2]++; return false; } }   // tiro inimigo ali do lado
            foreach (var a in FBMatch.Actors())
            {
                if (!a || !a.alive || a.knocked || !FBMatch.Hostile(a, ch)) continue;
                float d = Mathf.Min((a.Pos - kt.Pos).magnitude, (a.Pos - ch.Pos).magnitude);
                if (d < 22) { ReviveWhy[3]++; return false; }                                                          // inimigo em cima
                if (d < 40 && (FBWorld.ClearLine(a.Pos + Vector3.up * 1.5f * a.ScaleK, kt.Pos + Vector3.up * .5f) || FBWorld.ClearLine(a.Pos + Vector3.up * 1.5f * a.ScaleK, ch.Pos + Vector3.up * 1.2f * ch.ScaleK))) { ReviveWhy[4]++; return false; }   // na mira de alguém
            }
            return true;
        }
        void AbortRevive() { if (reviveTgt && reviveTgt.revivedBy == (object)ch) reviveTgt.revivedBy = null; rescuing = false; }
        public static int ReviveStarts, ReviveUnsafeBlocks, ReviveHot; public static readonly int[] ReviveWhy = new int[5];   // motivos: tiro recebido, trocando tiro, disparo perto, inimigo < 22 m, na mira                                                         // autoteste
        void BotMed(float dt)
        {
            // r45: como o jogador — parado (Update zera o passo), vida sobe durante a animação depois do início, kit gasto ao começar a subir
            if (medT > 0) { float e0 = FBMed.Use - medT; medT -= dt; float e1 = FBMed.Use - Mathf.Max(0, medT); ch.shooting = ch.aiming = false; if (e0 < FBMed.Wind && e1 >= FBMed.Wind) ch.kits--; if (e1 > FBMed.Wind) ch.hp = Mathf.Min(ch.maxHp, ch.hp + FBMed.Heal * (e1 - Mathf.Max(e0, FBMed.Wind)) / FBMed.Hot); if (medT <= 0) healEndT = .35f; }
            if (healEndT > 0) healEndT -= dt;
            string hs = medT > 0 ? (FBMed.Use - medT < .35f ? "FF_SYS_Heal_Start" : "FF_SYS_Heal_Loop") : healEndT > 0 ? "FF_SYS_Heal_End" : null;
            if (hs != null && !FBRegistry.I.Clip(hs)) hs = null;
            // reanimar aliado (Sigma): o Revive() liga 'rescuing' enquanto está ao lado do caído; o fim da animação toca sozinho
            var rp = ch.RescuePose(rescuing && hs == null, dt); rescuing = false;
            if (hs == null) hs = rp;
            if (hs != null || (ch.sys != null && (ch.sys.StartsWith("FF_SYS_Heal") || ch.sys.StartsWith("FF_SYS_Revive") || ch.sys.StartsWith("IE_Revive")))) ch.sys = hs;
            bool busyHands = medT > 0 || healEndT > 0 || ch.Rescuing;
            ch.SetWeaponVisible(!busyHands && !holster && !ch.UnarmedLocomotion && ch.dance == null && !ch.vehiclePose);
            ch.StowCurrent(holster || (busyHands && wu.Cur != null));                                       // curando/reanimando: arma no coldre
            if (medT > 0) return;
            if (ch.team < 2 && ch.hp < 45 && ch.kits > 0 && seeT > 2.5f && ai != "dummy" && ai != "victim" && Random.value < dt * .8f) medT = FBMed.Use;
        }

        // [parceiro 01/11] medidor de tempo por sistema (FBTempo): com FBTempo.On soma o custo do Update de todos os bots
        static readonly int __fbt_Update = FBTempo.Id("FBBot.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            float dt = Time.deltaTime; destDoGoal = false; if (dt <= 0 || !FBMatch.I) return;
            if (!ch.alive) { vel.x = vel.z = 0; Move(dt); return; }
            if (ch.tagT > 0) ch.tagT -= dt;
            if (FBBattleRoyale.Active(ch) && FBBattleRoyale.StepBot(ch, dt)) return;
            if (ch.knocked) { KnockedStep(dt); return; }
            if (FBMatch.I.mode != null && FBMatch.I.mode.br && wu.Cur == null && ch.team != 2 && FBBattleRoyale.BotLoot(ch, this, dt)) return;
            if (ch.getUpT > 0) { ch.getUpT -= dt; ch.sys = FBCombat.GetUpClip; vel.x = vel.z = 0; Move(dt); if (ch.getUpT <= 0) ch.sys = null; return; }
            BotMed(dt);
            if (ai == "victim" || ch.special) { vel = new Vector3(0, vel.y, 0); ch.shooting = false; Move(dt); return; }
            if (FBMatch.I.freeze)
            {
                vel.x = vel.z = 0; ch.shooting = false;
                // [parceiro] fisica 31/10: no freeze (fase de compra) zera sprint/mira da rodada anterior; senao o bot corria parado e mirava no alvo velho
                ch.sprint = false; ch.aiming = false; ch.hasAim = false;
                Move(dt);
                Anim();   // [parceiro] fisica 31/10: Anim() depois do Move zera ch.animVel (vel horizontal 0); antes ficava o animVel velho e o pe patinava
                return;
            }
            if (medT > 0) { vel.x = vel.z = 0; Move(dt); Anim(); return; }                                // curando: parado no lugar
            if (ai == "dummy")
            {
                ch.shooting = false; ch.combat = true; ch.aiming = false; dT += dt; float sv = strafeAmp > 0 ? Mathf.Cos(dT * strafeW) * strafeAmp * strafeW : 0; vel.x = strafeDir.x * sv; vel.z = strafeDir.z * sv;
                if (FBMatch.Player) YawRad = Mathf.Atan2(FBMatch.Player.Pos.x - ch.Pos.x, FBMatch.Player.Pos.z - ch.Pos.z); aimYawRad = YawRad; Move(dt); Anim(); return;
            }
            thinkT -= dt; shotT -= dt; wu.recoil.Step(dt);
            // ---- visão e percepção individual
            if (thinkT <= 0)
            {
                float tdt = lastThinkDt; thinkT = lastThinkDt = FBUtil.Rnd(.12f, .22f); FBCharacter best = null; float bd = 1e9f; int bestMask = 0;
                bool alert = ai == "combat" || ai == "cover" || ai == "search" || ai == "flee";
                foreach (var a in FBMatch.Actors())
                {
                    if (!a.alive || !FBMatch.Hostile(a, ch) || a.special || FBBattleRoyale.InPlane(a)) { aware.Remove(a); continue; }
                    float v = FBBotSense.Visibility(ch, YawRad, alert, a, out float d, out _, out int mask);
                    aware.TryGetValue(a, out var aw);
                    // consciência: sobe com a visibilidade (perto quase na hora; longe só exposto/em movimento/atirando), cai sem contato
                    aw = v > 0 ? Mathf.Min(1.4f, aw + tdt * v / FBBotSense.NoticeTime(d) * (alert ? 1.7f : 1)) : Mathf.Max(0, aw - tdt * .3f);
                    aware[a] = aw;
                    float dp = d + (a.knocked ? 30 : 0) + (a.protUntil > Time.time ? 20 : 0);
                    if (aw >= 1 && v > 0 && dp < bd) { bd = dp; best = a; bestMask = mask; }
                }
                sees = best; if (best) { seenMask = bestMask; if (best != acquired) { acquired = best; acquireT = Time.time; } }
                if (best)
                {
                    if (ai != "combat" && ai != "cover" && ai != "flee")
                    {
                        shotT = FBUtil.Rnd(.14f, .32f) * FBCombat.DiffReact; burst = 0;
                        float cc = role switch { "tactical" => .6f, "halemph" => .55f, "halemph2" => .5f, "flanker_left" => .35f, "flanker_right" => .35f, "baianogmbarril" => .3f, _ => .15f };
                        // [parceiro] presos 01/11: sem cobertura por 4 s depois de uma cancelada fora da ilha principal (ver DestinoSeguro)
                        if (bd > 12 && Random.value < cc && Time.time >= coverVetoT && FindCover(best.Pos) is Vector3 cv) { coverPos = cv; ai = "cover"; coverT = FBUtil.Rnd(3.5f, 6.5f); peekT = FBUtil.Rnd(.3f, .9f); peeking = false; }
                    }
                    FBBotBrain.TeamIntel(ch.team, best.Pos); if (ai != "cover" && ai != "flee") ai = "combat"; target = best; lastSeen = best.Pos; seeT = 0;
                }
                else seeT += .2f;
                if ((ai == "combat" || ai == "cover") && !sees && seeT > 1.4f && lastSeen != null) { ai = "search"; searchT = 0; ch.crouch = false; }
                if ((ai == "combat" || ai == "cover") && !sees && seeT > 3.5f)
                {
                    ai = "patrol"; target = null; goalP = lastSeen is Vector3 ls && (ls - ch.Pos).magnitude > 3 ? FlankPoint(ls) : PickPatrolGoal(); lastSeen = null; goalT = FBUtil.Rnd(10, 20); ch.crouch = false;
                }
                if (target && target.alive && (ai == "combat" || ai == "cover"))
                {
                    float dT2 = (ch.Pos - target.Pos).magnitude;
                    if ((ch.hp < 45 || (wu.reloadT > 0 && dT2 < 26)) && ai != "flee" && ai != "cover")
                    {
                        // [parceiro] presos 01/11: cobertura vetada por 4 s conta como "sem cobertura" (vida < 25 -> foge, como antes)
                        // [parceiro] v15: sai do combate em pé; o agachado que sobrou do combate fazia o bot ir para a cobertura a 1,1 m/s
                        if (Time.time >= coverVetoT && FindCover(target.Pos) is Vector3 cv) { coverPos = cv; ai = "cover"; ch.crouch = false; coverT = FBUtil.Rnd(2.5f, 4.5f); peekT = 0; peeking = false; }
                        else if (ch.hp < 25) { ai = "flee"; fleeT = FBUtil.Rnd(3, 5); }
                    }
                    if (ch.hp < 18 && ai != "flee") { ai = "flee"; fleeT = FBUtil.Rnd(3, 5); ch.crouch = false; }
                }
            }
            if (ai == "cover") { coverT -= dt; peekT -= dt; if (coverT <= 0) { ai = target && target.alive ? "combat" : "patrol"; ch.crouch = false; } }
            if (ai == "flee") { fleeT -= dt; if (fleeT <= 0) { float dist = target ? (ch.Pos - target.Pos).magnitude : 999; fleeT = dist > 24 || ch.hp > 50 ? 0 : FBUtil.Rnd(2, 4); if (fleeT <= 0) ai = target && target.alive ? "combat" : "patrol"; } }

            // ---- planejamento do movimento
            Vector3? desired = null, face = null; float speed = 0; bool urgent = FBMatch.I.mode != null && FBMatch.I.mode.Urgent(ch);
            if (ai != "flee" && Revive(dt) is var rv && rv != null) { desired = rv.Value.desired; speed = rv.Value.speed; face = rv.Value.face; ch.shooting = false; }   // em combate LONGE também socorre (SafeToRevive decide)
            else if (ai == "guard")
            {
                if (goalP == null || (ch.Pos - goalP.Value).magnitude < 1.5f || strafeT <= 0) { goalP = home + new Vector3(FBUtil.Rnd(-5, 5), 0, FBUtil.Rnd(-5, 5)); strafeT = FBUtil.Rnd(3, 6); pauseT = FBUtil.Rnd(1, 2.5f); }
                strafeT -= dt; if (pauseT > 0) { pauseT -= dt; face = goalP; } else { desired = goalP; speed = 2.4f; face = goalP; }
            }
            else if (ai == "escort")
            {
                escOff ??= new Vector3(FBUtil.Rnd(-3, 3), 0, FBUtil.Rnd(-3, 3)).normalized * FBUtil.Rnd(4, 7); var P = FBMatch.Player;
                var tg = P && P.alive ? P.Pos + escOff.Value : home; float d = (ch.Pos - tg).magnitude;
                if (d > 2.5f) { desired = tg; speed = d > 12 ? 5.2f : 4.2f; face = tg; } else if (P) face = P.Pos;
            }
            else if (ai == "cover" && coverPos is Vector3 cpos)
            {
                bool at = (ch.Pos - cpos).magnitude < 2;
                if (!at) { desired = cpos; speed = 5; face = cpos; }
                else
                {
                    ch.crouch = true; face = target ? target.Pos : lastSeen ?? cpos;
                    if (peekT <= 0 && sees && target) { peekT = FBUtil.Rnd(2, 3.8f); peeking = true; peekDur = FBUtil.Rnd(1, 1.6f); peekT2 = 0; }
                    if (peeking) { peekT2 += dt; ch.crouch = peekT2 > peekDur * .65f; if (peekT2 > peekDur) { peeking = false; ch.crouch = true; } if (!ch.crouch && sees && shotT <= 0 && target) FireAtCadence(target, 3, 6, .4f, .9f); }
                    strafeT -= dt; if (strafeT <= 0) { strafeT = FBUtil.Rnd(1, 2.5f); strafe = Random.value < .5f ? -1 : 1; }
                    var to2 = ((target ? target.Pos : ch.Pos) - ch.Pos).Flat().normalized; var side2 = new Vector3(to2.z, 0, -to2.x);
                    desired = ch.Pos + side2 * strafe * 1.5f; speed = 1.6f;
                }
            }
            else if (ai == "flee" && target) { ch.crouch = false; var away = (ch.Pos - target.Pos).Flat().normalized; desired = ch.Pos + away * 18; speed = 5.4f; face = desired; }
            else if (ai == "search")
            {   // perdeu o contato: vai ao último ponto visto (passo cauteloso), olha em volta ~2,5 s e volta a patrulhar
                searchT += dt; var lp = lastSeen ?? ch.Pos; bool there = (lp - ch.Pos).Flat().magnitude <= 2.2f || searchT > 10;
                if (!there) { desired = lp; speed = 3.2f; face = lp; lookT = 0; }
                else
                {
                    lookT += dt; float a = YawRad + Mathf.Sin(lookT * 1.4f) * 1.6f; face = ch.Pos + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 5;
                    if (lookT > 2.6f) { ai = "patrol"; target = null; lastSeen = null; goalP = PickPatrolGoal(); goalT = FBUtil.Rnd(10, 20); }
                }
            }
            else if (ai == "combat" && target)
            {
                var t = target;
                if (!t.alive) { ai = "patrol"; target = null; lastSeen = null; goalP = PickPatrolGoal(); goalT = FBUtil.Rnd(10, 20); ch.crouch = false; }
                else
                {
                    float d = (ch.Pos - t.Pos).magnitude; face = t.Pos; strafeT -= dt;
                    float er = FBBotSense.EngageRange(wu.Cur), pref = Mathf.Min(prefDist, er * .8f);       // alcance da arma e distância preferida do papel
                    if (strafeT <= 0) { strafeT = FBUtil.Rnd(.4f, 1.1f); strafe = new[] { -1, 1, -1, 1, 0 }[Random.Range(0, 5)]; ch.crouch = d > 12 && Random.value < .35f * (1.3f - aggr); }
                    var to = (t.Pos - ch.Pos).Flat().normalized; var side = new Vector3(to.z, 0, -to.x); var mv = Vector3.zero;
                    if (d > er * 1.05f) { mv += to * 1.8f; speed = 4.8f; ch.crouch = false; }                        // fora do alcance: aproxima (não atira)
                    else if (d > pref * 1.3f) { mv += to * (.6f + aggr * .6f) + side * strafe * .8f; speed = ch.crouch ? 1.8f : 3.8f; }
                    else if (d < pref * .5f) { mv += side * strafe * 1.3f; mv -= to * (.9f - aggr * .5f); speed = ch.crouch ? 1.6f : 3; }
                    else { mv += side * strafe * 1.2f; if (Random.value < .25f * aggr) mv += to * .4f; speed = ch.crouch ? 1.8f : 3.6f; }
                    if (mv.sqrMagnitude > .01f) desired = ch.Pos + mv.normalized * 4;
                    if (!sees && lastSeen is Vector3 ls2) { desired = ls2; speed = 4.5f; }
                    if (sees && shotT <= 0 && d <= er * 1.15f) { if (d > 45) FireAtCadence(t, 2, 4, .5f, 1.1f); else if (d > 18) FireAtCadence(t, 3, 6, .32f, .85f); else FireAtCadence(t, 4, 8, .18f, .5f); }
                }
            }
            else
            {
                if (goalP == null || goalT <= 0 || (ch.Pos - goalP.Value).magnitude < 2.5f || urgent)
                {
                    goalP = urgent ? (FBMatch.I.mode.UrgentGoal(ch) ?? FBMatch.I.mode.Zone?.c ?? ch.Pos) : PickPatrolGoal();
                    goalT = FBUtil.Rnd(10, 20); pauseT = urgent ? 0 : FBUtil.Rnd(.6f, 2);
                }
                goalT -= dt;
                if (pauseT > 0) { pauseT -= dt; speed = 0; desired = null; }
                else
                {
                    desired = goalP; float dg = goalP != null ? (ch.Pos - goalP.Value).magnitude : 0; speed = dg > 6 ? 6.2f : 4.2f; face = desired;   // 4,2: trote do IE no ritmo natural de passos (3,5 = câmera lenta)
                    destDoGoal = !urgent;   // [parceiro] revisão 2: patrulha andando pelo goalP (urgente troca o goalP todo quadro: não conta)
                }
            }

            // ---- arma guardada × sacada (como o jogador)
            bool threat = sees || ai == "combat" || ai == "cover" || ai == "flee" || ai == "search" || reviveTgt;
            calmT = threat ? 0 : calmT + dt;
            bool hasGun = wu.Cur != null;
            if (hasGun && !(FBMatch.I.mode != null && FBMatch.I.mode.br))
            {
                if (!holster && calmT > 2.5f && !(medT > 0) && ch.dance == null) { holster = true; float ht = ch.HolsterAnim(); ch.SetUnarmedLocomotion(true); ch.StowCurrent(true); if (ht <= 0) ch.SetWeaponVisible(false); }
                else if (holster && threat)
                {
                    holster = false; ch.SetUnarmedLocomotion(false); ch.StowCurrent(false); if (!(medT > 0)) { ch.SetWeaponVisible(true); ch.DrawAnim(); }
                    shotT = Mathf.Max(shotT, .3f); drawT = .3f; FBAudio.PlayAt(FBWeaponSound.Draw(ch.weapon) ?? "unholster", ch.Pos + Vector3.up, .5f, 5);   // sacar da própria arma
                }
                if (holster && !ch.UnarmedLocomotion) ch.SetUnarmedLocomotion(true);
            }
            else if (holster) { holster = false; ch.SetUnarmedLocomotion(!hasGun); }
            if (drawT > 0) drawT -= dt;
            if (speed > 0) speed *= holster ? 1.2f : wu.Cur != null ? wu.Cur.move : 1.15f;
            float bhs = vel.Flat().magnitude;
            ch.combat = hasGun && !holster;
            ch.shooting = (ai == "combat" || ai == "cover") && ch.lastShot < Json.F(GameData.Section("puppet"), "shootingFlagDuration", .15f);
            ch.aiming = (ai == "combat" || ai == "cover") && sees;
            // [parceiro] bug do deslize (31/10): mirando anda no máximo como o jogador mirando, agachado no máximo aiCrouchMaxSpeed (ver LimiteMira)
            if (speed > 0 && LimiteMira && !holster)
            {
                if (ch.crouch) speed = Mathf.Min(speed, Json.F(GameData.Section("puppet"), "aiCrouchMaxSpeed", 1.1f) * S);
                else if (ch.aiming) speed = Mathf.Min(speed, GameData.Move("speedAiming", 3) * S);
            }
            // [parceiro] fisica 31/10: sprint pelo que o corpo anda (legVel do Move anterior), nao pela velocidade pedida: bot travado em poste/carro/outro
            // bot com vel > 3,6 ficava com IsRunning=true e Speed~0 (corria parado). Sem colisao legVel == vel e nada muda.
            ch.sprint = legVel.magnitude > 3.6f && !ch.crouch && !ch.shooting && !ch.aiming && ai != "combat";
            bool waitingUnarmed = FBMatch.I.mode != null && FBMatch.I.mode.econ && !hasGun && ch.alive && onGround && bhs < .18f && !ch.shooting;
            var wp = waitingUnarmed ? FBEmotes.WaitPose(ch.IsFemale) : null; if (wp != null) ch.PlayWaitPose(wp); else ch.StopWaitPose();   // r47: sem emote FF
            ch.relaxed = false;

            // ---- navegação
            float vx = 0, vz = 0;
            // [parceiro] presos 01/11: presoDest = para onde o bot quer ir (antes do Steer), para o vigia de progresso. Com âncora, destino
            // de patrulha/busca/cobertura fora da ilha principal do NavMesh vira o ponto da ilha principal mais perto dele
            // (DestinoSeguro): a reta até ele (regra dos 12 m) e o fim do caminho parcial (FBNav.Path devolve o destino cru)
            // levavam o bot para dentro da quadra cercada, cujo alambrado é painel de um lado só (de fora o ClearLine não vê).
            // No modo estrito (bot que travou há pouco) e com destino ainda sem classificar sempre vai pelo Steer.
            // presos v4: saída a pé de bot travado (stuckN 4 fora da patrulha, stuckN 10): por até 1,6 s ele anda até o ponto livre,
            // em qualquer estado (no combate continua mirando e atirando no alvo); chegando a 0,5 m, acaba
            if (saidaP is Vector3 sp)
            {
                if (Time.time < saidaAte && (sp - ch.Pos).Flat().magnitude > .5f && !ch.vehiclePose)
                {
                    desired = sp; speed = Mathf.Max(speed, 2.6f);
                    ch.crouch = false;   // agachado o corpo não passa de ~1,1 m/s e o pé escorregaria
                }
                else saidaP = null;
            }
            Vector3? presoDest = desired; destPendente = false;
            if (desired is Vector3 dw && speed > 0) desired = DestinoSeguro(dw);
            // [parceiro] presos v4: destino fora do quadrado da arena (parede invisível do 10x10) vem para dentro, a 1,5 m da parede; antes o
            // bot empurrava a parede (o strafe do combate e o desvio do stuckN 4 não olham o quadrado)
            if (desired is Vector3 dl0 && FBNav.TemLimite && !FBNav.DentroLimite(dl0, 1)) desired = FBNav.PraDentro(dl0, 1.5f);
            if (desired is Vector3 de && (Estrito || destPendente || !FBWorld.ClearLine(ch.Pos, de) || (ch.Pos - de).magnitude > 12)) desired = Steer(desired, dt);
            if (desired is Vector3 dz)
            {
                var tt = (dz - ch.Pos).Flat(); float l = tt.magnitude;
                if (l > .25f)
                {
                    tt /= l;
                    if (ch.crouch && speed > 0 && LimiteMira && !holster)
                    {
                        // [parceiro] v15: agachado de lado o clipe só chega a ~0,82 m/s (0,567 x 1,45); de frente/trás chega a ~1,67
                        var frente = Quaternion.Euler(0, ch.Yaw, 0) * Vector3.forward;
                        if (face is Vector3 fo) { var olha = (fo - ch.Pos).Flat(); if (olha.sqrMagnitude > .01f) frente = olha.normalized; }
                        float lado = Mathf.Abs(Vector3.Cross(frente, tt).y);
                        var P = GameData.Section("puppet");
                        float limLado = Mathf.Lerp(Json.F(P, "aiCrouchMaxSpeed", 1.1f), Json.F(P, "aiCrouchSideMaxSpeed", .8f), lado) * S;
                        if (speed > limLado) speed = limLado;
                    }
                    vx = tt.x * speed; vz = tt.z * speed; face ??= dz;
                }
            }
            float accel = ch.sprint ? 9 : ai == "combat" ? 6.5f : 5;
            // [parceiro] fisica 31/10: agachado freia rapido ate o limite do agachado (antes o corpo levava ~0,3 s no combate e ~0,6 s chegando na cobertura
            // para cair de 2,8-3,9 m/s para ~0,8 m/s com as pernas ja no clipe agachado, e o pe escorregava)
            // (puppet.aiCrouchBrake no fb_data, padrao 20 m/s2; 12-14 se o strafe de media distancia ficar seco)
            if (ch.crouch && LimiteMira && !holster && bhs > Json.F(GameData.Section("puppet"), "aiCrouchMaxSpeed", 1.1f) * S) accel = Mathf.Max(accel, Json.F(GameData.Section("puppet"), "aiCrouchBrake", 20));
            float dvx = vx - vel.x, dvz = vz - vel.z, dl = Mathf.Sqrt(dvx * dvx + dvz * dvz), mx = accel * dt;
            if (dl > mx) { vel.x += dvx / dl * mx; vel.z += dvz / dl * mx; } else { vel.x = vx; vel.z = vz; }
            // pulo só com degrau baixo à frente
            jumpCd -= dt;
            if (onGround && jumpCd <= 0 && speed > 1.2f && bhs > 1)
            {
                var fd = vel.Flat().normalized;
                if (FBWorld.RayHit(ch.Pos + Vector3.up * .35f * S, fd, .85f * S, .05f) && !FBWorld.RayHit(ch.Pos + Vector3.up * 1.1f * S, fd, .85f * S, .05f)) { vel.y = Mathf.Sqrt(2 * 19 * 1.25f); onGround = false; airT = .08f; jumpCd = FBUtil.Rnd(3.5f, 6.5f); }
            }
            // rotação suave
            // [parceiro] fisica 31/10: fora de combate/cobertura e andando, o corpo segue a velocidade (linha abaixo); girar aqui para o
            // destino final (face) brigava com isso e deixava o corpo ~20 graus de lado nas curvas do caminho
            if (face is Vector3 fc && (ai == "combat" || ai == "cover" || bhs <= .6f)) { float ty = Mathf.Atan2(fc.x - ch.Pos.x, fc.z - ch.Pos.z), rate = (ai == "combat" || ai == "cover" ? 8 : 4.5f) * dt; YawRad += Mathf.Clamp(FBUtil.WrapA(ty - YawRad), -rate, rate); }
            if (ai != "combat" && ai != "cover" && bhs > .6f) YawRad += FBUtil.WrapA(Mathf.Atan2(vel.x, vel.z) - YawRad) * (1 - Mathf.Exp(-dt * 12));
            // mira com lead e pitch
            if ((ai == "combat" || ai == "cover") && target)
            {
                var t = target; float tof = (ch.Pos - t.Pos).magnitude / 280, acc = FBCombat.DiffAcc; float px = t.Pos.x + t.velocity.x * tof * acc, pz = t.Pos.z + t.velocity.z * tof * acc;
                float ty = Mathf.Atan2(px - ch.Pos.x, pz - ch.Pos.z), ar = (8 + acc * 5) * dt; aimYawRad += Mathf.Clamp(FBUtil.WrapA(ty - aimYawRad), -ar, ar);
                float eye = ch.Pos.y + 1.7f * S, tp = t.Pos.y + (t.prone ? .45f : t.crouch ? 1 : 1.4f) * t.ScaleK; pitch = Mathf.Lerp(pitch, Mathf.Clamp(Mathf.Atan2(tp - eye, (ch.Pos - t.Pos).magnitude), -.65f, .65f), 1 - Mathf.Exp(-dt * 9));
                ch.hasAim = true; ch.aimPoint = t.Pos + Vector3.up * (t.crouch ? 1 : 1.35f) * t.ScaleK;
            }
            else { aimYawRad = YawRad; pitch = Mathf.Lerp(pitch, 0, 1 - Mathf.Exp(-dt * 4)); ch.hasAim = false; }
            // anti-travamento: janela de 0,6 s; 1,2 s refaz caminho; 2,4 s passo lateral; 3,6 s outro destino; ~6 s fora da vista: encaixa na navmesh
            stuckWin += dt;
            if (stuckWin >= .6f)
            {
                float moved = (ch.Pos - lastPos).Flat().magnitude;
                if (speed > 1 && desired != null && moved < .35f && !ch.special)
                {
                    stuckN++;
                    if (stuckN == 2) { path = null; navT = 0; }
                    else if (stuckN == 4)
                    {
                        // [parceiro] presos v4: fora da patrulha (combate, cobertura, fuga) o goalP não move o bot (o destino vem do strafe, da
                        // cobertura ou da fuga): sai andando para um ponto livre
                        // revisão: busca (lastSeen), reanimar (vai até o caído) e patrulha urgente (goalP trocado todo quadro) também
                        // não andam pelo goalP; o desvio de lado só serve quando o destino deste quadro é o próprio goalP
                        // revisão 2: pela origem do destino (busca com lastSeen igual ao goalP do alerta enganava a comparação de posição)
                        bool peloGoal = Vigiado && destDoGoal && presoDest is Vector3 pd && goalP is Vector3 gp && (pd - gp).sqrMagnitude < .01f;
                        if (!peloGoal) { if (SaidaLivre(out var saida2)) Sai(saida2, 2, Vigiado ? "stuckN 4 (destino que não é o da patrulha)" : "stuckN 4 fora da patrulha"); }
                        else
                        {
                            float sd = Random.value < .5f ? 1 : -1;
                            sd = LadoLivre(sd);   // [parceiro] presos 01/11: lado sorteado batendo na borda do NavMesh (cerca, parede, quadra) e o outro livre -> vai pelo outro
                            goalP = ch.Pos + new Vector3(Mathf.Cos(YawRad), 0, -Mathf.Sin(YawRad)) * DesvioLado * sd; goalT = 1.5f; path = null;
                        }
                    }
                    else if (stuckN == 6) { path = null; goalP = PickPatrolGoal(); goalT = FBUtil.Rnd(8, 16); }
                    else if (stuckN >= 10)
                    {
                        // [parceiro] presos v4: o teleporte antigo só serve para bot fora do NavMesh (ponto do NavMesh a 0,75 m ou mais). Em cima
                        // do NavMesh ele mexia 0,1 a 0,7 m e o bot travava de novo no mesmo lugar (C1 01/11: 29 vezes no Centro, bots
                        // em combate; PT, borda da arena). Nesse caso sai andando para um ponto livre; sem ponto livre, usa a
                        // realocação do vigia (teleporte só escondido da câmera).
                        var presoDe = ch.Pos; bool achou = FBNav.Find(ch.Pos, 2.5f, out var at), espera = false, feito = false;
                        bool longe = achou && (at - presoDe).Flat().magnitude >= .75f;
                        if (longe)
                        {
                            // [parceiro] revisão: a regra antiga (câmera a 40 m ou 1 raio no peito) deixava o salto aparecer na luneta; agora é a
                            // mesma do vigia (com zoom e tamanho do salto). Vendo, segue para a saída a pé / realocação abaixo.
                            float salto0 = (at - presoDe).magnitude;
                            if (Escondido(presoDe, salto0) && Escondido(at, salto0))
                            {
                                // [parceiro] presos 01/11: mesmo teleporte de antes; só conta e registra no log
                                FBMatch.Teleport(ch, at, ch.Yaw); vel = Vector3.zero;
                                Contar(2, "stuckN 10 (teleporte antigo para o NavMesh a até 2,5 m)", presoDe, at);
                                // [parceiro] presos v4: a realocação pendente do vigia e a saída a pé eram do lugar antigo
                                realocAlvo = null; saidaP = null; saidaAte = -99; VigiaZera();
                                feito = true;
                            }
                        }
                        if (!feito && SaidaLivre(out var saida3)) { Sai(saida3, 3, longe ? "stuckN 10 (salto para o NavMesh à vista da câmera)" : "stuckN 10 (em cima do NavMesh)"); feito = true; }
                        if (!feito && !realocAlvo.HasValue)
                        {
                            // [parceiro] revisão: a busca da realocação é pesada (caminho da âncora); 1 por quadro somando os bots, como no vigia.
                            // Sem vaga, tenta de novo na próxima janela de 0,6 s.
                            if (!PodePesado()) espera = true;
                            else if (AlvoRealocar(out var alvo2, out var tipo2, out var porque2))
                            {
                                // [parceiro] tipo 3 = realocação pedida pelo stuckN 10 (conta à parte; a saída de ilha continua tipo 0)
                                Pendente(alvo2, tipo2 == 1 ? 3 : tipo2, porque2 + " (stuckN 10 sem ponto livre ao lado)");
                                TentaRealocar();
                            }
                        }
                        if (espera) stuckN = 9;
                        else { stuckN = 0; path = null; goalP = PickPatrolGoal(); }
                    }
                }
                else stuckN = 0;
                stuckWin = 0; lastPos = ch.Pos;
            }
            Vigia(dt, speed, presoDest);   // [parceiro] presos 01/11: vigia de progresso e ilha do NavMesh (ver Vigia); o stuckN acima continua como era
            Move(dt); Anim();
            if (ch.Pos.y < -120) { ch.alive = false; Destroy(gameObject, .1f); }
        }
        void Anim()
        {
            // [parceiro] fisica 31/10: pernas usam legVel (deslocamento real limitado a vel), nao a velocidade pedida; bot travado nao corre parado.
            // Bug do deslize (31/10): com PassoReal as pernas usam a velocidade real, sem o remapSpeed (ver PassoReal)
            float bhs = legVel.magnitude, bsp = PassoReal ? bhs / Mathf.Max(.01f, S) : RemapSpeed(bhs / Mathf.Max(.01f, S)); var lv = Quaternion.Inverse(Quaternion.Euler(0, ch.Yaw, 0)) * legVel; float lm = lv.magnitude;
            ch.animVel = lm > 1e-3f ? new Vector2(lv.x / lm * bsp, lv.z / lm * bsp) : Vector2.zero; ch.lean = 0;
        }
        // bot caído: rasteja (1,5 m/s) até o aliado de pé mais próximo
        void KnockedStep(float dt)
        {
            FBCombat.KnockTick(ch, dt);
            if (ch.alive && ch.knocked)
            {
                FBCharacter best = null; float bd = 1e9f; foreach (var o in FBMatch.TeamOf(ch)) if (o.alive && !o.knocked) { float d = (o.Pos - ch.Pos).magnitude; if (d < bd) { bd = d; best = o; } }
                float vx = 0, vz = 0;
                if (best && bd > 2.5f && ch.revivedBy == null)
                {
                    Vector3? des = best.Pos; if (!FBWorld.ClearLine(ch.Pos, best.Pos) || bd > 12) des = Steer(des, dt) ?? des;
                    var tt = (des.Value - ch.Pos).Flat(); float l = tt.magnitude; if (l > .2f) { vx = tt.x / l * FBMechanics.KNOCK.moveSpeed; vz = tt.z / l * FBMechanics.KNOCK.moveSpeed; YawRad += FBUtil.WrapA(Mathf.Atan2(vx, vz) - YawRad) * Mathf.Min(1, dt * 4); }
                }
                if (ch.revivedBy is FBCharacter rb && rb && Time.time - ch.reviveTick < .35f) { float want = rb.Yaw * Mathf.Deg2Rad + Mathf.PI * .5f; YawRad += FBUtil.WrapA(want - YawRad) * Mathf.Min(1, dt * 3); }
                vel.x = vx; vel.z = vz; aimYawRad = YawRad; ch.animVel = Vector2.zero; ch.sys = FBCombat.KoPose(ch, new Vector2(vx, vz).magnitude > .3f); ch.prone = true; ch.shooting = false;
            }
            Move(dt);
        }
        // moveBody: gravidade 19 (bots), chão firme, CharacterController contra o mapa
        void Move(float dt)
        {
            if (!cc || !cc.enabled) { legVel = vel.Flat(); legDef = Vector3.zero; ch.velocity = vel; return; }   // [parceiro] fisica 31/10: sem CC, pernas seguem a velocidade pedida
            vel.y += (onGround ? -2 : -19) * dt;
            if (botStep < 0) botStep = cc.stepOffset;   // mesma física de chão do jogador: degrau, aderência ao descer escada, rampa íngreme
            var p0 = transform.position;
            FBMotor.Move(cc, vel * dt, onGround, vel.y > 0, botStep, .45f * ch.ScaleK, ref botSlide, out onGround, out botGround);
            // [parceiro] fisica 31/10: legVel = velocidade pedida limitada ao deslocamento real do CC (bot travado em outro/poste/carro nao anima corrida parado)
            var pedida = vel.Flat(); var bruta = pedida;
            if (dt > 1e-5f)
            {
                var real = (transform.position - p0) / dt; real.y = 0;
                if (real.sqrMagnitude < bruta.sqrMagnitude) bruta = real;
                // deficit filtrado (ver legDef); legVel nunca passa da pedida nem aponta contra ela (bot travado freando nao anda de re)
                legDef = Vector3.Lerp(legDef, pedida - bruta, 1 - Mathf.Exp(-dt / .08f));
            }
            legVel = Vector3.ClampMagnitude(pedida - legDef, pedida.magnitude);
            if (Vector3.Dot(legVel, pedida) < 0) legVel = Vector3.zero;
            if (onGround && vel.y < 0) vel.y = 0; airT = onGround ? 0 : airT + dt;
            ch.velocity = new Vector3(legVel.x, vel.y, legVel.z); ch.grounded = onGround;   // [parceiro] fisica 31/10: publica o deslocamento real (limitado a vel)
        }
    }
}
