// EVENTOS DE RUA (rodada 39, 26/10/2026): situações que acontecem sozinhas no mundo, sem o jogador pedir, em todos os modos offline
// dos mapas com malha de ruas (Vila Jardim, Praça das Águas, Praça do PT, Orla). Um diretor (FBStreetEvents) sorteia quando cada uma
// aparece, longe e fora da vista; nos modos de combate ele mesmo roda o trânsito (só com os carros especiais).
//  · CARRO DO AÇAÍ: Saveiro G4 branca de fábrica (sem rebaixar, roda original, sem paredão) andando devagar no TRÂNSITO que já existe
//    (FBTransito: faixas, fila IDM, reserva de cruzamento, desvio de obstáculo) com o jingle em loop numa fonte 3D presa ao carro (mono,
//    curva de distância do jogo, abafa atrás de parede). A rota é sorteada a cada cruzamento puxando para o lado do jogador: passa perto
//    por alguns minutos sem repetir a volta e vai embora (sai de cena longe da vista).
//  · BACULEJO — cena estudada no 171 (Unreal 5.4, BP_EnquadroPolicial: viatura tática parada, 2–3 PMs, 1–2 abordados com as mãos na
//    cabeça, chance de fuga, falas, ligado ao sistema de procurado; docs/re/171_BACULEJO.md) e refeita com NOSSOS personagens e clipes
//    (o DefaultGame.ini do 171 proíbe usar o material dele): viatura na guia com giroflex e portas abertas, um PM revista cada abordado
//    por trás (REB_Policia_Revista), o outro dá cobertura com o fuzil apontado; abordados em REB_Rendido (mãos atrás da cabeça), de cara
//    para o muro quando há muro. Depois o PM anota (REB_Policia_Multa), fala (NPC_Talk_Serious), libera — os abordados seguem a vida — e
//    a viatura volta para o trânsito pela manobra de saída de vaga. Às vezes um abordado corre e o da cobertura vai atrás; tiro perto
//    dispersa todo mundo. Ninguém do evento atira nem entra no combate (são civis: bots ignoram, bala acerta).
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FreeBala
{
    public class FBStreetEvents : MonoBehaviour
    {
        public static FBStreetEvents I; bool ownTraffic; float acaiT, bacT, eleT, obraT, rachaT; public static string Log;
        public static bool ForceAcai, ForceBaculejo, ForceEletricista, ForceObra, ForceRacha; public static float ForceNear = -1;      // autoteste (-fbruas)
        public static void L(string s) { Log = s; Debug.Log("[FreeBala][ruas-eventos] " + s); }

        public static void Boot(FBMatch g)
        {
            if (I || FBSession.online || !g) return;
            if (g.mapId == "estande" || g.mapId == "bermuda") return;
            if (!FBRoads.Load(g.mapId)) return;
            I = new GameObject("FB_EventosRua").AddComponent<FBStreetEvents>();
        }
        void Start()
        {
            ownTraffic = FBSession.mode != "role";
            if (ownTraffic) FBTransito.StartSpecialOnly();
            FBGiroflex.Warm();
            // primeira aparição: nunca logo no começo (o jogador ainda está se situando)
            acaiT = Random.Range(75f, 200f); bacT = Random.Range(60f, 170f); eleT = Random.Range(90f, 240f); obraT = Random.Range(30f, 150f); rachaT = Random.Range(90f, 240f);   // r43: eletricista e obra do 171 · r45: racha de moto (MX)
            L($"{FBSession.map}/{FBSession.mode}: açaí em {acaiT:0} s, baculejo em {bacT:0} s, eletricista em {eleT:0} s");
        }
        // [parceiro] v1 (thread de física): tempo do Update medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
        static readonly int __fbt_Update = FBTempo.Id("FBStreetEvents.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            float dt = Time.deltaTime; var M = FBMatch.I; if (dt <= 0 || !M || M.state == "loading") return;
            if (ownTraffic) FBTransito.Update(dt);
            if (ForceAcai) { ForceAcai = false; acaiT = 0; FBAcai.End(true); }
            if (ForceBaculejo) { ForceBaculejo = false; bacT = 0; FBBaculejo.End(true); }
            if (ForceEletricista) { ForceEletricista = false; eleT = 0; FBEletricista.End(true); }
            if (ForceObra) { ForceObra = false; obraT = 0; FBObra.End(true); }
            if (ForceRacha) { ForceRacha = false; rachaT = 0; FBRacha.End(true); }
            if (!FBAcai.Active && (acaiT -= dt) <= 0) acaiT = FBAcai.Start() ? FBAcai.Cooldown() : 12;
            if (!FBBaculejo.Active && (bacT -= dt) <= 0) bacT = FBBaculejo.Start() ? FBBaculejo.Cooldown() : 15;
            if (!FBEletricista.Active && (eleT -= dt) <= 0) eleT = FBEletricista.Start() ? FBEletricista.Cooldown() : 20;
            if (!FBObra.Active && (obraT -= dt) <= 0) obraT = FBObra.Start() ? FBObra.Cooldown() : 25;
            if (!FBRacha.Active && FBSession.mode == "role" && (rachaT -= dt) <= 0) rachaT = FBRacha.Start() ? FBRacha.Cooldown() : 30;
            FBAcai.Tick(dt); FBBaculejo.Tick(dt); FBEletricista.Tick(dt); FBObra.Tick(dt); FBRacha.Tick(dt);
        }
        void OnDestroy() { FBAcai.End(true); FBBaculejo.End(true); FBEletricista.End(true); FBObra.End(true); FBRacha.End(true); FBGiroflex.Reset(); if (ownTraffic) FBTransito.Clear(); if (I == this) I = null; }

        // ---- utilidades comuns
        public static Vector3 PlayerPos => FBMatch.Player ? FBMatch.Player.Pos : Camera.main ? Camera.main.transform.position : Vector3.zero;
        public static bool Visible(Vector3 p, float r = 1.2f)
        {
            var cam = Camera.main; if (!cam) return false; var o = cam.transform.position; var d = p + Vector3.up * 1.2f - o; float m = d.magnitude; if (m > 260) return false;
            if (!GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(cam), new Bounds(p + Vector3.up, new Vector3(r * 2, 2.4f, r * 2)))) return false;
            return !Physics.Raycast(o, d / m, m - 1f, 1 << FBWorld.LMap, QueryTriggerInteraction.Ignore);
        }
        // pé livre: cápsula de pessoa sem bater em parede, prop ou veículo
        // r44: objeto solto com física (lixeira, cone, cadeira — FBFisica) não conta: é empurrado
        static readonly Collider[] hitsF = new Collider[16];
        public static bool Free(Vector3 feet, float r = .3f)
        {
            int n = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * (r + .3f), feet + Vector3.up * 1.7f, r, hitsF, FBWorld.SolidMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!Loose(hitsF[i])) return false; return true;
        }
        public static bool Loose(Collider c) => c && c.attachedRigidbody && !c.attachedRigidbody.isKinematic && c.GetComponentInParent<FBPropBody>();
        public static bool Ground(ref Vector3 p, float refY, float tol = .7f)
        {
            if (FBWorld.GroundY(p.x, refY + 1.1f, p.z, .2f, 3) is float gy && Mathf.Abs(gy - refY) < tol) { p.y = gy; return true; }
            return false;
        }
        // figurante novo (civil para o combate: bots ignoram; bala acerta) — sem o FBCivilian (o evento comanda)
        public static FBCharacter Spawn(string id, Vector3 p, float yaw, string seed)
        {
            var M = FBMatch.I; if (!M || string.IsNullOrEmpty(id)) return null;
            var c = M.Spawn(id, null, 2, p, false, false, true); if (!c) return null;
            var cv = c.GetComponent<FBCivilian>(); if (cv) { cv.enabled = false; Destroy(cv); }
            FBMatch.Civs.Remove(c); c.hp = c.maxHp = 60; FBNpcAnims.Apply(c, seed); c.SetWeaponVisible(false); c.combat = false; c.civ = true;
            FBMatch.Teleport(c, p, yaw); return c;
        }
        public static void Despawn(FBCharacter c)
        {
            if (!c) return; if (FBVehicles.CarOf(c)) FBDrive.Release(c);
            FBMatch.All.Remove(c); FBMatch.Civs.Remove(c); Destroy(c.gameObject);
        }
        // personagens do pacote que não estão em cena (sem repetir rosto) — PMs nunca viram civis
        public static List<string> FreeIds(IEnumerable<string> pool) => pool.Where(id => FBRegistry.I.HasChar(id) && !FBMatch.All.Any(a => a && a.skinId == id)).OrderBy(_ => Random.value).ToList();
        public static string FreeCivilian(bool allowRepeat = false)
        {
            var l = FreeIds(FBNpcAnims.Roster(FBSession.skin)); if (l.Count > 0) return l[0];
            if (!allowRepeat) return null;                                                          // elenco todo em cena (rolê): repete um rosto
            var all = FBNpcAnims.Roster(FBSession.skin).Where(id => FBRegistry.I.HasChar(id)).ToList(); return all.Count > 0 ? all[Random.Range(0, all.Count)] : null;
        }
        // figurantes da mesma cena não se empurram (o PM encosta 0,44 m atrás do abordado; as cápsulas somadas passam disso)
        public static void NoCollide(IEnumerable<FBCharacter> who)
        {
            var cs = who.Where(c => c).Select(c => c.GetComponent<CharacterController>()).Where(c => c).ToList();
            for (int i = 0; i < cs.Count; i++) for (int j = i + 1; j < cs.Count; j++) Physics.IgnoreCollision(cs[i], cs[j], true);
        }
    }

    // ================================================================ CARRO DO AÇAÍ
    public static class FBAcai
    {
        static readonly string[] MODELS = { "SaveiroG4", "SaveiroQuadrada", "SaveiroCross" };
        const uint WHITE = 0xf3f3ef;
        static FBTransito.Car tc; static FBCar car; static FBCharacter driver; static AudioSource src; static float fadeOut = -1, life, visit, leaveT = -1, carHp0, drvHp0, noiseSeen; static bool passed;
        public static bool Fleeing { get; private set; }
        public static bool Active => car; public static bool Leaving => leaveT >= 0;
        public static FBCar Car => car; public static AudioSource Source => src; public static FBTransito.Car Traffic => tc;
        public static float Cooldown() => Random.Range(240f, 460f);

        public static bool Start()
        {
            if (!FBTransito.Running || !FBRegistry.I) return false;
            var id = MODELS.FirstOrDefault(m => FBRegistry.I.Vehicle(m) && GameData.Vehicles.ContainsKey(m)); if (id == null) return false;
            // onde entra: faixa longe e fora da vista (autoteste: perto)
            int seg; float s;
            if (FBStreetEvents.ForceNear > 0) { if (!NearLane(FBStreetEvents.ForceNear, out seg, out s)) return false; }
            else if (!FBTransito.FarLanePoint(out seg, out s)) return false;
            var S = FBRoads.segs[seg]; var p = FBRoads.At(S, s, out var tan);
            // Saveiro de vendedor: SEMPRE branca, altura e roda de fábrica (rnd fixo = sorteio "de fábrica"), sem som/paredão
            car = FBVehicles.Make(id, p, Mathf.Atan2(tan.x, tan.z) * Mathf.Rad2Deg, true, () => .5f, true, WHITE, false); if (!car) return false;
            car.name = "Carro_Acai";
            // r56: quem dirige o carro do açaí é o VENDEDOR DE AÇAÍ (elenco "funcao"/vendedor_acai) e mais ninguém.
            // Só cai num civil qualquer se ele ainda não estiver registrado (projeto sem a importação da r56).
            var did = FBElenco.Ids(FBElenco.FUNCAO, "vendedor_acai").FirstOrDefault() ?? FBStreetEvents.FreeCivilian(true);
            driver = did != null ? FBStreetEvents.Spawn(did, p, 0, "acai") : null;
            if (!driver) { Kill(); return false; }
            tc = FBTransito.SpawnSpecial(car, driver, "acai", Random.Range(4.2f, 5.4f), seg, s);   // 15–19 km/h: velocidade de quem vende na rua
            if (tc == null) { Kill(); return false; }
            tc.lure = true; visit = Random.Range(150f, 240f); passed = false; leaveT = -1; tc.aggr = .1f; tc.T = 1.9f; tc.onDrop = _ => End(false);
            Fleeing = false; carHp0 = FBCarDamage.Hp(car); drvHp0 = driver.hp; noiseSeen = FBBotBrain.lastNoiseT;
            Speaker(); life = 0; fadeOut = -1;
            Debug.Log($"[FreeBala][ruas-eventos] carro do açaí ({id}) entrou a {(p - FBStreetEvents.PlayerPos).Flat().magnitude:0} m do jogador (visita de até {visit:0} s)");
            return true;
        }
        // faixa a 'd' m do jogador (autoteste)
        static bool NearLane(float d, out int seg, out float s)
        {
            seg = -1; s = 0; var pp = FBStreetEvents.PlayerPos; float best = 1e9f;
            foreach (var S in FBRoads.segs) { if (S.kind != 0 || S.len < 20 || S.ring) continue; for (float x = 6; x < S.len - 6; x += 4) { var q = FBRoads.At(S, x, out _); float e = Mathf.Abs((q - pp).Flat().magnitude - d); if (e < best && FBVehicles.list.All(c => !c || (c.transform.position - q).sqrMagnitude > 64)) { best = e; seg = S.id; s = x; } } }
            return seg >= 0;
        }
        // som do vendedor: a MESMA acústica do paredão (FBParedao.PlayLocal — colunas, ecos nas fachadas, reverb da rua, atraso pela
        // distância, prédio na frente abafa) e o mesmo volume de um paredão no padrão; em loop do começo ao fim da visita
        const float VOL = .85f;
        public static FBParedao.Wall Wall { get; private set; }
        public static bool Mute;                                                                  // autoteste: mede a saída com e sem o som
        static void Speaker()
        {
            var clip = FBAudio.Clip("acai_carro"); if (!clip) { Debug.LogWarning("[FreeBala][ruas-eventos] sem o áudio acai_carro"); return; }
            Wall = FBParedao.PlayLocal(car, clip, 0, Random.value * clip.length * .8f);   // entra longe: já tocando, no meio da música
            src = Wall?.src;
        }
        public static void Tick(float dt)
        {
            if (!car) { if (tc != null || driver) End(true); return; }
            life += dt;
            if (Wall != null)
            {
                float k = fadeOut >= 0 ? Mathf.Clamp01(1 - fadeOut / 1.6f) : Mathf.Clamp01(life / 2.5f);   // entrada/saída suaves
                Wall.vol = Mute ? 0 : VOL * Mathf.Sqrt(k); if (Wall.src) src = Wall.src;
                if (fadeOut >= 0 && (fadeOut += dt) > 1.6f) { Kill(); return; }
            }
            // FUGA: tiro no carro (perde vida no FBCarDamage), no motorista, ou disparo a menos de 30 m do carro -> FBTransito.Flee (o mesmo
            // motorista do trânsito, com pressa: ver lá). Motorista morto: o carro para onde está (não foge sozinho).
            if (tc != null && fadeOut < 0 && driver && driver.alive)
            {
                Vector3? from = null; var cp = car.transform.position;
                if (FBCarDamage.Hp(car) < carHp0 - .01f) from = FBBotBrain.lastNoiseP ?? cp - car.transform.forward * 10;
                else if (driver.hp < drvHp0 - .01f) from = FBBotBrain.lastNoiseP ?? cp;
                else if (FBBotBrain.lastNoiseT > noiseSeen && FBBotBrain.lastNoiseP is Vector3 np && (np - cp).Flat().magnitude < 30) from = np;
                noiseSeen = FBBotBrain.lastNoiseT; carHp0 = FBCarDamage.Hp(car); drvHp0 = driver.hp;
                if (from is Vector3 f)
                {
                    if (!Fleeing) Debug.Log($"[FreeBala][ruas-eventos] carro do açaí FUGINDO (tiro a {(f - cp).Flat().magnitude:0} m)");
                    Fleeing = true; leaveT = Mathf.Max(leaveT, 0); FBTransito.Flee(tc, f);
                }
            }
            else if (tc != null && driver && !driver.alive && tc.vCap > 0) tc.vCap = 0;
            // UMA VOLTA e vai embora (pedido 26/10: "não fique pra sempre"): passou perto do jogador (≤ 45 m) e se afastou (> 75 m), ou
            // acabou o tempo da visita -> FBTransito.Leave (rota para longe; o trânsito o tira de cena longe e fora da vista). Se ainda
            // estiver por perto 2 min depois (jogador seguindo o carro), some com o som em fade quando ninguém estiver olhando — ou aos 3 min
            if (tc != null && fadeOut < 0)
            {
                float dP = (car.transform.position - FBStreetEvents.PlayerPos).Flat().magnitude;
                if (leaveT < 0) { if (dP < 45) passed = true; if ((passed && dP > 75) || life > visit) { leaveT = 0; FBTransito.Leave(tc); Debug.Log($"[FreeBala][ruas-eventos] carro do açaí indo embora ({(passed ? "já passou pelo jogador" : "fim da visita")}, {life:0} s)"); } }
                else if ((leaveT += dt) > 120 && (!FBTransito.Visible(car.transform.position) || leaveT > 180)) End(false);
            }
            // longe e sem ninguém vendo: some sem cortar o som (já inaudível)
            if (tc != null && tc.mode == "gone" && fadeOut < 0) End(false);
        }
        public static void End(bool now)
        {
            if (!car && tc == null && !driver) return;
            if (!now && Wall != null && fadeOut < 0) { fadeOut = 0; if (tc != null) { tc.onDrop = null; FBTransito.Forget(tc); tc = null; } return; }
            Kill();
        }
        static void Kill()
        {
            if (tc != null) { tc.onDrop = null; FBTransito.Forget(tc); tc = null; }
            if (driver) FBStreetEvents.Despawn(driver); driver = null;
            if (car) { FBParedao.StopLocal(car); FBVehicles.list.Remove(car); Object.Destroy(car.gameObject); } car = null; src = null; Wall = null; fadeOut = -1;
        }
    }

    // ================================================================ FIGURANTE (anda, para, vira; pés no chão nos clipes do REB)
    [DefaultExecutionOrder(1002)]
    public class FBStreetActor : MonoBehaviour
    {
        FBCharacter c; CharacterController cc; bool gnd = true; float slide, step = -1, vy, navT, breath; FBMotor.Ground g; Vector3 vel; List<Vector3> path; Vector3? pathGoal;
        public Vector3? goal; public float speed = 1.3f, arriveR = .3f; public float? faceYaw; public bool run, flatFeet, life, ready; public System.Action onArrive;
        Transform lFoot, rFoot, lToe, rToe, neck, spine; float seed;
        public bool Arrived => goal == null;
        public void Init(FBCharacter ch)
        {
            c = ch; cc = GetComponent<CharacterController>(); seed = Random.value * 10; var an = c.an;
            if (an && an.isHuman) { lFoot = an.GetBoneTransform(HumanBodyBones.LeftFoot); rFoot = an.GetBoneTransform(HumanBodyBones.RightFoot); lToe = an.GetBoneTransform(HumanBodyBones.LeftToes); rToe = an.GetBoneTransform(HumanBodyBones.RightToes); neck = an.GetBoneTransform(HumanBodyBones.Neck); spine = an.GetBoneTransform(HumanBodyBones.Spine); }
        }
        public void Go(Vector3 p, float spd, float r = .3f) { goal = p; speed = spd; arriveR = r; path = null; pathGoal = null; }
        public void Stop() { goal = null; }
        // [parceiro] v1 (thread de física): tempo do Update medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
        static readonly int __fbt_Update = FBTempo.Id("FBStreetActor.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            float dt = Time.deltaTime; if (dt <= 0 || !c || c.vehiclePose) return;
            if (!c.alive) { vel = Vector3.zero; c.velocity = vel; c.animVel = Vector2.zero; return; }
            Vector3 tv = Vector3.zero;
            if (goal is Vector3 G && c.dance == null)
            {
                float dg = (G - c.Pos).Flat().magnitude;
                if (dg <= arriveR) { goal = null; var cb = onArrive; onArrive = null; cb?.Invoke(); }
                else
                {
                    navT -= dt;
                    if (path == null || pathGoal == null || (pathGoal.Value - G).sqrMagnitude > 1 || navT <= 0)
                    { navT = 3; pathGoal = G; path = dg > 5 ? (FBPedNav.Ready ? FBPedNav.Path(c.Pos, G) : null) ?? FBNav.Path(c.Pos, G) : null; path ??= new List<Vector3> { G }; }
                    while (path.Count > 1 && (path[0] - c.Pos).Flat().magnitude < .8f) path.RemoveAt(0);
                    var t = (path[0] - c.Pos).Flat(); float sp = Mathf.Min(speed, Mathf.Max(.5f, dg * 1.4f));
                    if (t.sqrMagnitude > 1e-4f) tv = t.normalized * sp;
                }
            }
            float cur = vel.magnitude, want = tv.magnitude; float ns = Mathf.MoveTowards(cur, want, (want > cur ? (run ? 7 : 2.4f) : 3.4f) * dt);
            var hd = cur > .05f ? vel / cur : want > .01f ? tv / want : transform.forward.Flat().normalized;
            if (want > .01f) hd = Vector3.RotateTowards(hd, tv / want, (cur < .4f ? 320 : 210) * Mathf.Deg2Rad * dt, 0);
            vel = hd * ns; vel.y = 0;
            if (cc && cc.enabled) { if (step < 0) step = cc.stepOffset; vy = gnd ? -2 : vy - 19 * dt; FBMotor.Move(cc, (vel + Vector3.up * vy) * dt, gnd, vy > 0, step, .45f * c.ScaleK, ref slide, out gnd, out g); }
            // animação: armado = remapeado como o bot; desarmado = conversão do rolê (andar lento ainda é o ciclo de andar)
            float gg = vel.magnitude; var lv = Quaternion.Inverse(Quaternion.Euler(0, c.Yaw, 0)) * vel; float lm = lv.magnitude, sw;
            if (!c.Unarmed) { var P = GameData.Section("puppet"); float s = gg / Mathf.Max(.01f, c.ScaleK), pw = Json.F(P, "playerWalkSpeed", 4), pr = Json.F(P, "playerRunSpeed", 6.8f), aw = Json.F(P, "aiWalkAnimSpeed", 2), ar = Json.F(P, "aiRunAnimSpeed", 5); sw = s <= pw ? s / pw * aw : aw + (s - pw) / (pr - pw) * (ar - aw); }
            else sw = gg > .35f && gg < 2.2f ? Mathf.Max(gg * 1.12f, 1.57f) : gg * 1.12f;
            c.animVel = gg < .05f ? Vector2.zero : new Vector2(lv.x / lm * sw, lv.z / lm * sw); c.velocity = vel; c.grounded = !cc || gnd; c.sprint = run && gg > 3;
            if (gg > .2f) c.Yaw = Mathf.LerpAngle(c.Yaw, Quaternion.LookRotation(vel).eulerAngles.y, Mathf.Min(1, dt * 7));
            else if (faceYaw is float fy) c.Yaw = Mathf.LerpAngle(c.Yaw, fy, Mathf.Min(1, dt * 4));
        }
        // depois da animação e do rig: pés planos (os clipes do REB vêm com o pé "em ponta") e respiração/olhar de quem está rendido
        // [parceiro] v1 (thread de física): tempo do LateUpdate medido pelo FBTempo
        static readonly int __fbt_LateUpdate = FBTempo.Id("FBStreetActor.LateUpdate");
        void LateUpdate()
        {
            if (!FBTempo.On) { LateUpdate__fbt(); return; }
            long t0 = FBTempo.T0(); LateUpdate__fbt(); FBTempo.Soma(__fbt_LateUpdate, t0);
        }
        void LateUpdate__fbt()
        {
            if (!c || !c.alive || c.vehiclePose) return; float t = Time.time + seed;
            if (flatFeet && c.dance != null) { Flat(lFoot, lToe); Flat(rFoot, rToe); }
            if (life && c.dance != null)
            {
                breath = Mathf.Lerp(breath, 1, Time.deltaTime * 2);
                if (spine) spine.rotation = Quaternion.AngleAxis(Mathf.Sin(t * 1.7f) * 1.2f * breath, transform.right) * Quaternion.AngleAxis(Mathf.Sin(t * .37f) * 2.2f * breath, transform.up) * spine.rotation;
                if (neck) neck.rotation = Quaternion.AngleAxis((Mathf.PerlinNoise(t * .18f, 3) - .5f) * 38 * breath, Vector3.up) * Quaternion.AngleAxis((Mathf.PerlinNoise(t * .13f, 7) - .35f) * 14 * breath, transform.right) * neck.rotation;
            }
        }
        void Flat(Transform foot, Transform toe)
        {
            if (!foot) return; var tip = toe ? toe.position : foot.position + foot.rotation * Vector3.forward * .12f; var d = tip - foot.position; float m = d.magnitude; if (m < 1e-4f) return; d /= m;
            float pitch = Mathf.Asin(Mathf.Clamp(d.y, -1, 1)) * Mathf.Rad2Deg;                          // em pé, do tornozelo à ponta do pé desce ~25–40°
            if (pitch > -48) return; var flat = d.Flat(); if (flat.sqrMagnitude < 1e-4f) flat = transform.forward; flat.Normalize();
            var want = (flat * Mathf.Cos(32 * Mathf.Deg2Rad) + Vector3.down * Mathf.Sin(32 * Mathf.Deg2Rad)).normalized;
            foot.rotation = Quaternion.FromToRotation(d, want) * foot.rotation;
        }
    }

    // ================================================================ CARONA (PM da cobertura sentado ao lado do motorista)
    // carona da viatura. Pose quebrada (pedido 26/10) = o mesmo que o FBDriverPose resolve no motorista: o fuzil da cobertura seguia
    // tocando na camada Upper Body por cima do clipe sentado e a camada base recebia o andar → aqui, depois do FBCharacter e antes do
    // Animator avaliar: locomoção zerada, Upper Body/Emote Upper em 0 e o clipe sentado (Emote Full Body) em 1
    [DefaultExecutionOrder(1001)] public class FBPassengerSeat : MonoBehaviour
    {
        FBCharacter c; FBCar car; CharacterController cc; Animator an; int LU = -1, LE = -1, LEU = -1; readonly List<int> zeroF = new(), offB = new();
        static readonly string[] ZERO = { "VelX", "VelZ", "Speed", "VerticalVelocity", "CrouchAlpha" }, OFF = { "IsRunning", "IsAiming", "IsShooting", "InCombat", "InAir", "IsCrouching", "Tactical Sprint" };
        public void Sit(FBCar u)
        {
            c = GetComponent<FBCharacter>(); cc = GetComponent<CharacterController>(); car = u; if (!c || !u) return;
            if (cc) cc.enabled = false; c.aiming = c.hasAim = false; c.SetWeaponVisible(false); c.vehiclePose = true; c.velocity = Vector3.zero; c.animVel = Vector2.zero;
            // clipe de banco de carro (o mesmo do motorista do REB: corpo encaixado no assento); os "sentar" do NPC são de banco de praça
            // (pernas esticadas, braços abertos) e atravessavam o painel
            var sit = FBDrive.SitClip(u) ?? c.npcSet?.sit.FirstOrDefault(n => FBRegistry.I.Clip(n) && !System.Text.RegularExpressions.Regex.IsMatch(n, "_to_|Stand_to", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
            c.StopDance(true); c.sys = sit;
            an = c.an; zeroF.Clear(); offB.Clear(); if (!an) return;
            LU = an.GetLayerIndex("Upper Body"); LE = an.GetLayerIndex("Emote Full Body"); LEU = an.GetLayerIndex("Emote Upper Body");
            foreach (var p in an.parameters)
            {
                string n = p.name.Trim();
                if (p.type == AnimatorControllerParameterType.Float && System.Array.IndexOf(ZERO, n) >= 0) zeroF.Add(p.nameHash);
                else if (p.type == AnimatorControllerParameterType.Bool && System.Array.IndexOf(OFF, n) >= 0) offB.Add(p.nameHash);
            }
        }
        /// <summary>Desce do banco (baculejo r42: PM carona desembarca na abordagem).</summary>
        public void Leave() { if (c) { c.vehiclePose = false; c.sys = null; if (cc) cc.enabled = true; } car = null; Destroy(this); }
        // [parceiro] v1 (thread de física): tempo do Update medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
        static readonly int __fbt_Update = FBTempo.Id("FBPassengerSeat.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            if (!car || !c || !an || !an.runtimeAnimatorController || !c.alive) return;
            foreach (var h in zeroF) an.SetFloat(h, 0); foreach (var h in offB) an.SetBool(h, false);
            if (LU >= 0) an.SetLayerWeight(LU, 0); if (LEU >= 0) an.SetLayerWeight(LEU, 0); if (LE >= 0 && c.sys != null) an.SetLayerWeight(LE, 1); an.speed = 1;
        }
        // [parceiro] v1 (thread de física): tempo do LateUpdate medido pelo FBTempo
        static readonly int __fbt_LateUpdate = FBTempo.Id("FBPassengerSeat.LateUpdate");
        void LateUpdate()
        {
            if (!FBTempo.On) { LateUpdate__fbt(); return; }
            long t0 = FBTempo.T0(); LateUpdate__fbt(); FBTempo.Soma(__fbt_LateUpdate, t0);
        }
        void LateUpdate__fbt()
        {
            if (!car || !c) { if (c) { c.vehiclePose = false; c.sys = null; if (cc) cc.enabled = true; } Destroy(this); return; }
            var an = c.an; var hips = an ? an.GetBoneTransform(HumanBodyBones.Hips) : null; if (!hips) return;
            transform.rotation = car.transform.rotation;
            var seat = car.L2W(new Vector3(-car.seat.x, car.seat.y, car.seat.z)); transform.position += seat - hips.position;
            // depois do clipe: pés no assoalho do lado do carona (pedais do REB espelhados) e mãos sobre as coxas (o clipe é de quem dirige)
            var up = car.transform.up; var fw = car.transform.forward; var rt = car.transform.right;
            if (c.fitCar != car)
            {   // cabeça no teto: o mesmo encaixe do motorista (FBDrive.PoseActor) — PM alto desce no banco em vez de atravessar a capota
                c.fitCar = car; c.seatDrop = 0; var head = an.GetBoneTransform(HumanBodyBones.Head);
                if (head) { float best = 9; foreach (var h in Physics.RaycastAll(head.position + up * 1.4f, -up, 1.4f, 1 << FBWorld.LVehicle, QueryTriggerInteraction.Ignore)) if (h.collider.GetComponentInParent<FBCar>() == car && h.distance < best) best = h.distance;
                    if (best < 9) { float roof = 1.4f - best - .035f, clear = roof - .22f * (transform.lossyScale.y / FBConst.ModelScale * 1.25f); c.seatDrop = Mathf.Min(.35f, Mathf.Max(0, .04f - clear)); } }
            }
            if (c.seatDrop > 0) transform.position -= up * c.seatDrop;
            Transform B(HumanBodyBones x) => an.GetBoneTransform(x);
            if (car.rigFeet != null && car.rigFeet.Length >= 2)
                for (int i = 0; i < 2; i++)
                {
                    var (lg, lo, ft) = i == 0 ? (B(HumanBodyBones.LeftUpperLeg), B(HumanBodyBones.LeftLowerLeg), B(HumanBodyBones.LeftFoot)) : (B(HumanBodyBones.RightUpperLeg), B(HumanBodyBones.RightLowerLeg), B(HumanBodyBones.RightFoot));
                    if (!lg || !lo || !ft) continue; var pf = car.rigFeet[1 - i]; var fr = ft.rotation;
                    FBIK.TwoBonePole(lg, lo, ft, car.L2W(new Vector3(-pf.x, pf.y, pf.z)), lo.position + fw * .6f + up * .35f); ft.rotation = fr;
                }
            for (int i = 0; i < 2; i++)
            {
                var (ua, la, hd, ul, ll) = i == 0 ? (B(HumanBodyBones.LeftUpperArm), B(HumanBodyBones.LeftLowerArm), B(HumanBodyBones.LeftHand), B(HumanBodyBones.LeftUpperLeg), B(HumanBodyBones.LeftLowerLeg))
                                                  : (B(HumanBodyBones.RightUpperArm), B(HumanBodyBones.RightLowerArm), B(HumanBodyBones.RightHand), B(HumanBodyBones.RightUpperLeg), B(HumanBodyBones.RightLowerLeg));
                if (!ua || !la || !hd || !ul || !ll) continue; float side = Vector3.Dot(ua.position - hips.position, rt) > 0 ? 1 : -1;
                var tg = Vector3.Lerp(ul.position, ll.position, .55f) + up * .09f - rt * side * .02f;
                FBIK.TwoBonePole(ua, la, hd, tg, la.position - fw * .35f + rt * side * .25f - up * .1f);
            }
            bool hide = (car.transform.position - FBStreetEvents.PlayerPos).sqrMagnitude > 110 * 110; foreach (var r in c.GetComponentsInChildren<Renderer>()) r.enabled = !hide;
        }
    }

    // ================================================================ GIROFLEX (luzes criadas no começo da partida e reaproveitadas)
    public static class FBGiroflex
    {
        public class Bar { public FBCar car; public Light red, blue; public Renderer gr, gb; public bool on = true; public readonly List<Renderer> ownR = new(), ownB = new(); public bool litNow, rNow, bNow; public float phase; public Vector3 lr, lb; }
        // r45: 4 pares de luz (antes 2 luzes para TODAS as viaturas: a segunda roubava as da primeira) — viatura além disso só acende as lentes
        const int PAIRS = 4; static Light[] pool; static Material mR, mB, mOff, gR, gB; static readonly List<Bar> bars = new(); static float t;
        // sem criar luz/material no meio da partida (compilação de shader = engasgo): tudo nasce aqui, apagado e longe
        public static void Warm()
        {
            if (pool != null && System.Array.TrueForAll(pool, l => l)) return; pool ??= new Light[PAIRS * 2];
            for (int i = 0; i < pool.Length; i++) if (!pool[i]) { var g = new GameObject("Giroflex_" + i); Object.DontDestroyOnLoad(g); var l = g.AddComponent<Light>(); l.type = LightType.Point; l.range = 22; l.intensity = 0; l.shadows = LightShadows.None; l.color = i % 2 == 0 ? new Color(1, .08f, .04f) : new Color(.1f, .35f, 1); g.transform.position = Vector3.down * 500; pool[i] = l; }
            Material M(Color c) { var m = new Material(FBShaders.Unlit); m.SetColor("_BaseColor", c); m.color = c; return m; }
            mR = M(new Color(1, .12f, .08f)); mB = M(new Color(.15f, .4f, 1)); mOff = M(new Color(.12f, .12f, .14f));
            gR = M(new Color(5, .45f, .3f)); gB = M(new Color(.35f, 1.1f, 6));                 // lente acesa: HDR > 1 = brilha no bloom
            if (FBVisual.C.giroflex > .5f)
            {   // r65: lente de LED sem estourar o bloom (antes 5–6× = mancha branca); luz com sombra disponível (o par mais perto, à noite)
                gR.SetColor("_BaseColor", new Color(2.6f, .2f, .12f)); gR.color = new Color(2.6f, .2f, .12f); gB.SetColor("_BaseColor", new Color(.15f, .55f, 3.2f)); gB.color = new Color(.15f, .55f, 3.2f);
                foreach (var l in pool) { l.shadowNearPlane = .12f; l.shadowBias = .03f; l.shadowNormalBias = .25f; l.shadowStrength = .95f; var ad = l.GetUniversalAdditionalLightData(); if (ad) ad.additionalLightsShadowResolutionTier = UnityEngine.Rendering.Universal.UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow; }
            }
        }
        // giroflex PRÓPRIO do modelo (pedido 26/10: "ela já tem a sirene dela"): nas viaturas é um material do corpo, não um nó —
        // giroflexbypabblo (barra do teto), generic_lightbar (strobos da grade) e a lente cbf0507a (PM). As lentes têm textura escura
        // (acender a textura não aparece): recorta os triângulos dessa parte da malha em metade esquerda/direita e cobre com uma
        // película vermelha/azul 3 mm para fora (sem brigar com a lente), ligada no pisca. Malha recortada fica em cache por modelo.
        static readonly System.Text.RegularExpressions.Regex LENS = new("giroflex|lightbar|sirene|siren|strobo|cbf0507a", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        static readonly Dictionary<(Mesh, int), (Mesh l, Mesh r)> cut = new();
        static (Mesh l, Mesh r) Cut(Mesh m, int sub)
        {
            if (cut.TryGetValue((m, sub), out var have)) return have;
            if (!m.isReadable) return cut[(m, sub)] = (null, null);
            var V = m.vertices; var N = m.normals; var T = m.GetTriangles(sub); bool hasN = N != null && N.Length == V.Length;
            Mesh Half(bool left)
            {
                var map = new Dictionary<int, int>(); var v = new List<Vector3>(); var idx = new List<int>();
                for (int k = 0; k + 2 < T.Length; k += 3)
                {
                    float cx = (V[T[k]].x + V[T[k + 1]].x + V[T[k + 2]].x) / 3; if ((cx < 0) != left) continue;
                    for (int j = 0; j < 3; j++) { int o = T[k + j]; if (!map.TryGetValue(o, out var n)) { n = map[o] = v.Count; v.Add(V[o] + (hasN ? N[o] * .003f : Vector3.zero)); } idx.Add(n); }
                }
                if (idx.Count == 0) return null;
                var h = new Mesh { name = m.name + (left ? "_giro_E" : "_giro_D") }; if (v.Count > 65000) h.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                h.SetVertices(v); h.SetTriangles(idx, 0); h.RecalculateBounds(); return h;
            }
            return cut[(m, sub)] = (Half(true), Half(false));
        }
        public static Bar Attach(FBCar car)
        {
            Warm(); if (!car) return null; var have = Find(car); if (have != null) return have;
            int pair = -1; for (int k = 0; k < PAIRS && pair < 0; k++) if (!bars.Any(o => o.red == pool[k * 2])) pair = k;
            uint hs = 2166136261; foreach (char ch in (car.syncId ?? car.name + car.transform.position.x.ToString("0"))) hs = (hs ^ ch) * 16777619;
            var b = new Bar { car = car, red = pair >= 0 ? pool[pair * 2] : null, blue = pair >= 0 ? pool[pair * 2 + 1] : null, phase = (hs % 1000) / 1000f * CYC };
            // barra de luz: dois blocos sobre o teto (no meio do comprimento da cabine) — só para carro sem giroflex nenhum
            var lb = car.lbox; float top = lb.max.y, zc = lb.center.z + lb.size.z * .05f;
            Renderer Block(float x, string n)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.Destroy(g.GetComponent<Collider>()); g.name = n; g.transform.SetParent(car.transform, false);
                g.transform.localPosition = new Vector3(x, top + .04f, zc); g.transform.localScale = new Vector3(lb.size.x * .2f, .07f, .16f); g.layer = FBWorld.LVehicle;
                var r = g.GetComponent<Renderer>(); r.sharedMaterial = mOff; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return r;
            }
            // por nome de material, só a malha mais detalhada (LOD0) — as dos LODs ficariam em cima uma da outra
            var best = new Dictionary<string, (MeshRenderer r, Mesh m, int sub)>();
            foreach (var r in car.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>(); var m = mf ? mf.sharedMesh : null; if (!m) continue; var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length && i < m.subMeshCount; i++)
                    if (ms[i] && LENS.IsMatch(ms[i].name)) { var key = ms[i].name; if (!best.TryGetValue(key, out var cur) || m.vertexCount > cur.m.vertexCount) best[key] = (r, m, i); }
            }
            foreach (var (r, m, sub) in best.Values)
            {
                var (hl, hr) = Cut(m, sub);
                foreach (var (h, red) in new[] { (hl, true), (hr, false) })
                {
                    if (!h) continue; var g = new GameObject(red ? "giroflex_vermelho" : "giroflex_azul"); g.transform.SetParent(r.transform, false); g.layer = r.gameObject.layer;
                    g.AddComponent<MeshFilter>().sharedMesh = h; var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = red ? gR : gB; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; mr.enabled = false;
                    (red ? b.ownR : b.ownB).Add(mr);
                }
            }
            bool own = b.ownR.Count + b.ownB.Count > 0 || best.Count > 0 || car.GetComponentsInChildren<Transform>(true).Any(x => LENS.IsMatch(x.name));
            if (!own) { b.gr = Block(lb.size.x * .17f, "giro_vermelho"); b.gb = Block(-lb.size.x * .17f, "giro_azul"); }
            b.lr = new Vector3(lb.size.x * .2f, top + .25f, zc); b.lb = new Vector3(-lb.size.x * .2f, top + .25f, zc);   // luzes do pool NÃO são filhas do carro (carro destruído levaria o pool junto): seguem no Tick
            bars.Add(b); return b;
        }
        public static void Detach(Bar b)
        {
            if (b == null) return; bars.Remove(b); foreach (var r in b.ownR.Concat(b.ownB)) if (r) Object.Destroy(r.gameObject);
            foreach (var l in new[] { b.red, b.blue }) if (l) { l.intensity = 0; l.transform.position = Vector3.down * 500; }
        }
        public static Bar Find(FBCar car) => bars.FirstOrDefault(b => b.car == car);
        // pisca duplo alternado (vermelho-vermelho, azul-azul) em 0,9 s. r45: cada lampejo de 0,1 s é testado contra o INTERVALO do quadro
        // (t-dt, t] — antes era o instante t, e com 25–40 qps a janela de 0,1 s caía entre dois quadros e o lampejo sumia (pisca "aleatório")
        const float CYC = .9f; static readonly (float a, float b)[] FR = { (0, .1f), (.2f, .3f) }, FB = { (.45f, .55f), (.65f, .75f) };
        // r65: padrão de LED das viaturas daqui (triplo alternado, 0,8 s — ~75 ciclos/min): 3 lampejos de 60 ms por cor
        const float CYC3 = .8f; static readonly (float a, float b)[] FR3 = { (0, .06f), (.1f, .16f), (.2f, .26f) }, FB3 = { (.4f, .46f), (.5f, .56f), (.6f, .66f) };
        static bool Flash((float a, float b)[] w, float t0, float t1, float cyc = 0)
        {
            float C = cyc > 0 ? cyc : CYC;
            if (t1 - t0 >= C) return true; float a = t0 % C, b = t1 % C; if (a < 0) a += C; if (b < 0) b += C;
            foreach (var (x, y) in w) if (b >= a ? (x < b && y > a) : (y > a || x < b)) return true;
            return false;
        }
        public static void Tick(float dt)       // chamado pelo FBSiren (todo quadro, em qualquer modo)
        {
            t += dt; float day = FBQuality.Mobile ? 0 : 1;                                       // celular: só as lentes acendem (sem luz pontual extra)
            bool nv = FBVisual.C.giroflex > .5f; Bar shB = null;
            if (nv && !FBQuality.Mobile && (FBWeather.NightK > .3f || FBWeather.Rain))
            {   // r65: o par da viatura mais perto da câmera projeta SOMBRA — a luz não atravessa a própria carroceria nem pinta o chão embaixo
                // dela; só no escuro (com os postes já usando sombra: sem variante de shader nova no meio da partida)
                var cam = Camera.main; float best = 60 * 60;
                if (cam) foreach (var b in bars) { if (!b.car || !b.on) continue; float d2 = (b.car.transform.position - cam.transform.position).sqrMagnitude; if (d2 < best) { best = d2; shB = b; } }
            }
            foreach (var b in bars)
            {
                if (!b.car) continue; bool on = b.on; float t1 = t + b.phase, t0 = t1 - Mathf.Max(dt, 1e-4f);
                bool rOn = on && (nv ? Flash(FR3, t0, t1, CYC3) : Flash(FR, t0, t1)), bOn = on && (nv ? Flash(FB3, t0, t1, CYC3) : Flash(FB, t0, t1));
                if (nv) { var sh = b == shB ? LightShadows.Soft : LightShadows.None; foreach (var l in new[] { b.red, b.blue }) if (l && l.shadows != sh) l.shadows = sh; }
                var T = b.car.transform; if (b.red) { b.red.transform.position = T.TransformPoint(b.lr); b.red.intensity = rOn ? 4.5f * day : 0; } if (b.blue) { b.blue.transform.position = T.TransformPoint(b.lb); b.blue.intensity = bOn ? 4.5f * day : 0; }
                if (b.gr) b.gr.sharedMaterial = rOn ? mR : mOff; if (b.gb) b.gb.sharedMaterial = bOn ? mB : mOff;
                foreach (var r in b.ownR) if (r) r.enabled = rOn; foreach (var r in b.ownB) if (r) r.enabled = bOn;
                b.litNow = rOn || bOn; b.rNow = rOn; b.bNow = bOn;
            }
            foreach (var b in bars) if (!b.car) foreach (var l in new[] { b.red, b.blue }) if (l) { l.intensity = 0; l.transform.position = Vector3.down * 500; }
            bars.RemoveAll(b => !b.car);
        }
        public static bool Alive(Bar b) => b != null && bars.Contains(b);
        public static void Reset() { foreach (var b in bars.ToList()) Detach(b); bars.Clear(); }
        public static bool Lit(FBCar u) => bars.Any(b => b.car == u && b.litNow);
        public static int OwnCount(FBCar u) => bars.Where(b => b.car == u).Sum(b => b.ownR.Count + b.ownB.Count);
    }
}
