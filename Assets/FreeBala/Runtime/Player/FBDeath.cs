// MORTE (rodada 42) — pedido do usuário: "as animações de morte atuais estão muito ruins, artificiais e repetitivas ... diversas
// variações, considerando a posição do personagem, a direção do impacto e o movimento que ele estava executando antes de morrer ...
// avalie combinar animações com ragdoll físico".
// Antes: 3 clipes de fuzil do Complexo (Rifle_Death_3/L/R) sorteados sem olhar de onde veio o tiro; o corpo terminava "deitado no ar"
// em escada, meio-fio e rampa, e atravessava parede quando a queda batia nela.
//
// Agora, em duas fases:
//  1) CLIPE DIRECIONAL — biblioteca de 49 quedas do 171 (Damage/Death + Shooted, mesmo uso condicionado à autorização da Betagames
//     dos outros X171_*), cada uma medida no próprio clipe (Resources/FBDeathLib.json: para onde o quadril vai — frente/direita em
//     metros no referencial do corpo —, quando chega ao chão, de barriga para cima ou para baixo). A escolha:
//       · empurrão = direção da bala no referencial da vítima (tiro de frente -> cai para trás; pelas costas -> para a frente; de lado ->
//         para o outro lado), pontuação = alinhamento da queda com o empurrão + sorteio, sem repetir os últimos clipes usados;
//       · correndo (> 4,5 m/s para a frente): quedas longas para a frente (embalo) e as duas "Correndo_tiro";
//       · agachado: só quedas curtas (< 0,8 m) — desaba onde está;  · tiro na cabeça: quedas que chegam ao chão em < 1,3 s;
//       · espaço livre: a queda inteira é testada contra o cenário (esfera no peito ao longo do caminho + chão no ponto de chegada);
//         clipe que bateria em parede/carro ou cairia de degrau é descartado.
//  2) RAGDOLL (FBRagdoll) — física de verdade a partir da pose em que o clipe está: quando o clipe termina (o corpo se acomoda no
//     chão real — degrau, rampa, meio-fio), quando o corpo encosta em algo no meio da queda, ou direto, sem clipe, quando a morte é por
//     explosão/carro/queda, no ar, caído (já no chão) ou quando nenhum clipe cabe no espaço. O impulso vai no osso atingido, na
//     direção da bala. Corpo parado por 0,7 s (ou 6 s no máximo) vira estátua: sem física, sem custo.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public static class FBDeath
    {
        public class Clip { public string id, face, kind; public float fwd, right, dur, land; public float Len => Mathf.Sqrt(fwd * fwd + right * right); }
        static List<Clip> lib; static readonly List<string> recent = new();
        public static string LastPick, LastWhy;                                                       // diagnóstico (autoteste)

        static List<Clip> Lib()
        {
            if (lib != null) return lib; lib = new List<Clip>();
            var ta = Resources.Load<TextAsset>("FBDeathLib"); if (!ta) return lib;
            foreach (var o in Json.L(Json.Parse(ta.text), "clips") ?? new List<object>())
            {
                var c = new Clip { id = Json.S(o, "id"), fwd = Json.F(o, "fwd"), right = Json.F(o, "right"), dur = Json.F(o, "dur"), land = Json.F(o, "land"), face = Json.S(o, "face"), kind = Json.S(o, "kind", "stand") };
                if (FBRegistry.I && FBRegistry.I.Clip(c.id)) lib.Add(c);
            }
            return lib;
        }

        /// <summary>Começa a morte de 'ch' (chamado pelo FBCharacter.Die). Devolve o clipe tocado (null = ragdoll direto ou clipe antigo).</summary>
        public static string Begin(FBCharacter ch)
        {
            var rd = FBRagdoll.Of(ch); var hit = ch.lastHit; string why = ch.lastWhy;
            Vector3 dir = hit.dir.sqrMagnitude > .01f ? hit.dir : ch.lastHitFrom is Vector3 f ? (ch.Pos - f) : -ch.transform.forward;
            dir.y = 0; if (dir.sqrMagnitude < 1e-4f) dir = -ch.transform.forward; dir.Normalize();
            float imp = why == "explosion" || why == "gunner" || why == "heli" ? 14 : why == "car" ? 9 : ch.lastHitHead ? 4.5f : 3f;
            // sem clipe: explosão/carro/queda, no ar, caído no chão, dentro de veículo
            bool physical = why == "explosion" || why == "gunner" || why == "heli" || why == "car" || why == "fall";
            bool lying = ch.prone || ch.knocked; bool air = !ch.grounded && ch.velocity.y < -2f;
            if (physical || lying || air)
            {
                LastPick = null; LastWhy = physical ? why : lying ? "caido" : "no_ar";
                if (rd) rd.Go(dir * imp + Vector3.up * (physical ? imp * .35f : 0), hit.valid ? hit.point : ch.Pos + Vector3.up, physical ? 0 : .05f);
                return null;
            }
            var L = Lib(); if (L.Count == 0) { LastWhy = "sem biblioteca"; return null; }
            var tf = ch.transform; var push = tf.InverseTransformDirection(dir); var pushV = new Vector2(push.x, push.z).normalized;   // (direita, frente)
            var vel = tf.InverseTransformDirection(ch.velocity); bool running = vel.z > 4.5f && !ch.crouch;
            bool crouched = ch.crouch, head = ch.lastHitHead; float s = Mathf.Max(.5f, ch.ScaleK);
            var chest = ch.Chest ? ch.Chest.position : ch.Pos + Vector3.up * 1.2f * s; float gy = ch.Pos.y;
            Clip best = null; float bestS = -1e9f; int fits = 0;
            foreach (var c in L)
            {
                if (running ? !(c.kind == "run" || c.fwd > 1.2f) : c.kind == "run") continue;
                if (crouched && c.Len > .8f) continue;
                if (head && c.land > 1.3f) continue;
                // espaço: caminho da queda (peito) e o chão no ponto de chegada
                var wv = tf.TransformDirection(new Vector3(c.right, 0, c.fwd)) * s; float d = wv.magnitude;
                if (d > .05f && Physics.SphereCast(chest, .22f * s, wv / d, out _, d + .45f * s, FBWorld.SolidMask, QueryTriggerInteraction.Ignore)) continue;
                var land = ch.Pos + wv; if (!(FBWorld.GroundY(land.x, gy + .6f, land.z, .6f, 1.2f) is float ly) || Mathf.Abs(ly - gy) > .3f) continue;
                fits++;
                var fv = new Vector2(c.right, c.fwd); float align = fv.sqrMagnitude > .01f ? Vector2.Dot(fv.normalized, pushV) : .2f;
                float sc = (running ? .3f : 1f) * align + Random.value * .45f - (recent.Contains(c.id) ? 1.2f : 0);
                if (sc > bestS) { bestS = sc; best = c; }
            }
            if (best == null)
            {   // nenhuma queda cabe (parede colada, beira de degrau): física desde já
                LastPick = null; LastWhy = $"sem espaço ({fits})";
                if (rd) rd.Go(dir * imp, hit.valid ? hit.point : chest, .05f);
                return null;
            }
            recent.Add(best.id); if (recent.Count > 6) recent.RemoveAt(0);
            LastPick = best.id; LastWhy = $"empurrão ({pushV.x:0.0};{pushV.y:0.0}){(running ? " correndo" : "")}{(crouched ? " agachado" : "")}{(head ? " cabeça" : "")}";
            if (rd) rd.After(best.dur - .05f, best.land, dir * imp * .25f);
            return best.id;
        }
    }

    // ---- ragdoll montado na hora a partir do esqueleto Humanoid (mesmo desenho do RagdollBuilder do Unity: quadril, meio da coluna,
    // cabeça, braço/antebraço, coxa/canela), com os limites das juntas RELATIVOS à pose do instante da morte — joelho e cotovelo só
    // dobram para o lado anatômico (o sentido é testado na própria pose, não suposto) até 150°/145° no total.
    public class FBRagdoll : MonoBehaviour
    {
        public static int FlexSign = 1;                                                                 // ver Limb / FBDeathTest (-fbflexsign: força os dois)
        public static int FlexSignArm = -1, FlexSignLeg = 1, FlexForce;                                            // r43: medido por articulação (ver Limb)
        public const int Layer = 15;                                                                   // "Corpo": colide com mapa/veículo/props, não com gente
        static bool layerSet;
        public static FBRagdoll Of(FBCharacter ch)
        {
            if (!ch) return null; var an = ch.GetComponent<Animator>(); if (!an || !an.isHuman) return null;
            if (!layerSet) { layerSet = true; for (int l = 0; l < 32; l++) Physics.IgnoreLayerCollision(Layer, l, !(l == 0 || l == FBWorld.LMap || l == FBWorld.LVehicle || l == Layer)); }
            var r = ch.GetComponent<FBRagdoll>(); if (!r) { r = ch.gameObject.AddComponent<FBRagdoll>(); r.enabled = false; } r.ch = ch; r.an = an; return r;
        }
        public static readonly List<FBRagdoll> Active = new();
        const int MAX_SIM = 8;
        public static int Built, Settled;                                                              // autoteste

        FBCharacter ch; Animator an;
        class Part { public Transform t; public Rigidbody rb; public Collider col; public Joint j; public int layer; public Vector3 prev; public Part up; public Vector3 restL; }   // up/restL: encaixe na peça de cima (r46m, Project)
        readonly List<Part> parts = new(); readonly List<Behaviour> paused = new();
        float waitT = -1, landT, simT, restT; Vector3 waitImp; bool sim, done; Vector3 lastImpPoint;
        public bool Simulating => sim; public bool Done => done;
        // r46 — RAGDOLL VIVO (queda da moto, atropelamento): o personagem continua vivo enquanto a física manda no corpo; não vira estátua
        // e não é desmontado pelo "está vivo -> Clear" do LateUpdate. Quem pediu (FBFall) lê RestTime/SimTime, guarda a pose e chama Clear.
        // Antes o FBKnockdown chamava Go() num personagem vivo: o LateUpdate seguinte já desmontava tudo (o corpo nunca caía de verdade).
        [System.NonSerialized] public bool live; Vector3 liveVel, liveSpin; readonly List<Collider> hitOff = new();
        // [parceiro] fisica 31/10: ragdoll de personagem VIVO (atropelo, eletricista caindo): o LateUpdate nao desfaz no mesmo quadro.
        // Complementa o 'live' (r46): cobre quem chama Go() direto num vivo (FBEletricista.Fall) sem passar pelo GoLive do FBFall
        [System.NonSerialized] public bool aliveHold;
        public float RestTime => restT;
        public void GoLive(Vector3 vel, Vector3 spin, Vector3 imp, Vector3 at, IEnumerable<Collider> ignore)
        {
            // hitboxes dos ossos (e o colisor da arma) desligadas antes de montar, como o FBCharacter.Die faz na morte: ligadas, elas passam
            // para a camada "Corpo" junto com o osso e empurram as peças vizinhas do próprio ragdoll (r46c: osso esticado 54%, 1 m num quadro)
            foreach (var c in GetComponentsInChildren<Collider>()) if (c.enabled && !(c is CharacterController)) { c.enabled = false; hitOff.Add(c); }
            live = true; liveVel = vel; liveSpin = spin; Go(imp, at, 0);
            if (ignore != null) foreach (var c in ignore) if (c && !c.isTrigger) { foreach (var p in parts) if (p.col) Physics.IgnoreCollision(p.col, c, true); foreach (var (fc, _) in feet) if (fc) Physics.IgnoreCollision(fc, c, true); }   // a moto em que estava sentado
        }

        Transform B(HumanBodyBones b) => an.GetBoneTransform(b);
        static readonly HumanBodyBones[] TRACK = { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Head, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg };

        /// <summary>Ragdoll quando o clipe de morte acabar (ou antes, se o corpo encostar em algo depois de começar a cair).</summary>
        public void After(float secs, float land, Vector3 imp) { waitT = secs; landT = land; waitImp = imp; enabled = true; }
        /// <summary>Ragdoll agora (delay = espera mínima para pegar a velocidade dos ossos do quadro anterior).</summary>
        public void Go(Vector3 imp, Vector3 at, float delay = 0) { aliveHold = ch && ch.alive; /* [parceiro] fisica 31/10: Go em vivo e intencional (FBAtropelo/FBEletricista); FBDeath so chama depois do Die */ waitT = delay; landT = 0; waitImp = imp; lastImpPoint = at; enabled = true; if (delay <= 0) Start2(); }

        // r43 — TRAVA ANATÔMICA: joelho e cotovelo nunca passam do reto ao contrário nem do máximo. O limite de torção do CharacterJoint
        // é medido a partir da pose de criação e o sentido dele não fechou igual nas duas articulações (-fbmorte: cotovelo a −52° com um
        // sinal, joelho a −38° com o outro; explosão com a perna reta: −147°). Aqui, antes de cada passo de física: dobra fora da faixa
        // [−2°, máx] -> gira o osso de baixo de volta em torno da dobradiça (pivô no joelho/cotovelo) e corta a velocidade que empurra para fora.
        public static int AnatomyFixes;
        public static int AnatAxes;
        // r46g: chaves de experimento do laboratório (sem recompilar): -fbrd_fine (passo de 12 ms no ragdoll vivo — piorou, só para reteste), -fbrd_nofoot (sem colisor de pé)
        static readonly bool UseFine = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbrd_fine") >= 0, NoFoot = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbrd_nofoot") >= 0;
        // r43b: só antes do passo (FixedUpdate) o próprio passo ainda deixava o joelho passar do reto até o seguinte — o autoteste via −17°
        // a −38° na pose desenhada. Agora também DEPOIS da física (início do LateUpdate, pela Transform: o que vai para a tela; o PhysX
        // recebe a Transform corrigida no passo seguinte)
        // [parceiro] v1 (thread de física): tempo do FixedUpdate medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
        static readonly int __fbt_FixedUpdate = FBTempo.Id("FBRagdoll.FixedUpdate");
        void FixedUpdate()
        {
            if (!FBTempo.On) { FixedUpdate__fbt(); return; }
            long t0 = FBTempo.T0(); FixedUpdate__fbt(); FBTempo.Soma(__fbt_FixedUpdate, t0);
        }
        void FixedUpdate__fbt() { if (sim) { if (live && !NoProj) Project(true); Anatomy(true); } }
        // r46m — PROJEÇÃO FEITA AQUI: a do PhysX (enableProjection, 2 cm) não segurou a junta no impacto — sonda r46l/r46m: mulher 171 caindo da
        // xj6, lombar a 214–368% do comprimento (quadril e tronco separados), pescoço 164%, coxa 146%. Antes de cada passo: a peça de baixo que
        // saiu mais de 2 cm do encaixe na peça de cima volta para o encaixe e perde a velocidade que a afastava (pais antes dos filhos)
        public static int ProjFixes; public static float ProjMax;
        static readonly bool NoProj = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbrd_noproj") >= 0;
        static readonly bool NoPost = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbrd_nopost") >= 0;   // experimento: só a projeção antes do passo
        // r46n: também DEPOIS da física (LateUpdate, pela Transform, como a anatomia): o impacto separa as peças DENTRO do passo e a tela
        // mostrava o passo separado (sonda r46n2, só antes do passo: braço da r_estrela 165%, pescoço 158%)
        void Project(bool physics)
        {
            float tol = .02f * Mathf.Max(.5f, ch ? ch.ScaleK : 1);
            foreach (var p in parts)
            {
                if (p.up == null || !p.rb || p.rb.isKinematic || !p.up.rb) continue; var prb = p.up.rb;
                var mine = physics ? p.rb.position : p.t.position;   // encaixe = pivô do osso de baixo (anchor zero), guardado no referencial de rotação da peça de cima
                var want = physics ? prb.position + prb.rotation * p.restL : p.up.t.position + p.up.t.rotation * p.restL;
                var e = want - mine; float d = e.magnitude; if (d <= tol) continue;
                var n = e / d; ProjFixes++; ProjMax = Mathf.Max(ProjMax, d);
                if (!physics) { p.t.position += n * (d - tol); continue; }
                p.rb.position += n * (d - tol);
                float away = Vector3.Dot(p.rb.linearVelocity - prb.linearVelocity, -n); if (away > 0) p.rb.linearVelocity += n * away;
            }
        }
        void Anatomy(bool physics)
        {
            foreach (var h in hinges)
            {
                if (!h.up || !h.low || !h.end || !h.rbL || h.rbL.isKinematic) continue;
                var nn = h.up.TransformDirection(h.nLocal); var dU = (h.low.position - h.up.position).normalized; var dL = (h.end.position - h.low.position).normalized;
                float f = Vector3.SignedAngle(dU, dL, nn), fix = f < -2 ? -2 - f : f > h.max ? h.max - f : 0;
                if (fix == 0) continue;
                var q = Quaternion.AngleAxis(fix, nn); var piv = h.low.position;
                if (physics) { h.rbL.position = piv + q * (h.rbL.position - piv); h.rbL.rotation = q * h.rbL.rotation; }
                else h.low.SetPositionAndRotation(piv + q * (h.low.position - piv), q * h.low.rotation);
                var rel = h.rbL.angularVelocity - (h.rbU ? h.rbU.angularVelocity : Vector3.zero); float w = Vector3.Dot(rel, nn);
                if ((fix > 0 && w < 0) || (fix < 0 && w > 0)) h.rbL.angularVelocity -= nn * w;
                AnatomyFixes++;
            }
        }
        // [parceiro] v1 (thread de física): tempo do LateUpdate medido pelo FBTempo
        static readonly int __fbt_LateUpdate = FBTempo.Id("FBRagdoll.LateUpdate");
        void LateUpdate()
        {
            if (!FBTempo.On) { LateUpdate__fbt(); return; }
            long t0 = FBTempo.T0(); LateUpdate__fbt(); FBTempo.Soma(__fbt_LateUpdate, t0);
        }
        void LateUpdate__fbt()
        {
            if (sim) { if (live && !NoProj && !NoPost) Project(false); Anatomy(false); }
            if (!ch) return;
            if (!ch.alive) live = false;                                                             // morreu no meio da queda: segue como corpo de morte
            // [parceiro] fisica 31/10: so limpa no Revive; ragdoll vivo intencional (aliveHold) segue simulando ate quem pediu levantar (Clear)
            if (ch.alive && !live && !aliveHold) { if (parts.Count > 0 || paused.Count > 0) Clear(); enabled = false; return; }
            float dt = Mathf.Max(Time.deltaTime, 1e-3f);
            if (!sim && !done && waitT >= 0)
            {
                waitT -= dt; landT -= dt;
                // encostou no meio da queda (parede/carro que o teste de espaço não pegou, outro corpo): física já
                bool touch = false; var head = B(HumanBodyBones.Head);
                if (landT > 0 && head && Physics.CheckSphere(head.position, .1f * ch.ScaleK, (1 << FBWorld.LVehicle) | 1, QueryTriggerInteraction.Ignore)) touch = true;   // antes de chegar ao chão: carro/prop no caminho
                if (waitT <= 0 || touch) Start2();
                else TrackPrev();
                return;
            }
            if (!sim) return;
            simT += dt;
            float vmax = 0; foreach (var p in parts) if (p.rb) vmax = Mathf.Max(vmax, p.rb.linearVelocity.magnitude);
            // r46d: ragdoll vivo "assentou" = tronco parado (quadril, coluna, cabeça); mão/pé tremendo no chão não seguram o levantar até o limite
            float vcore = 0; for (int i = 0; i < parts.Count && i < 3; i++) if (parts[i].rb) vcore = Mathf.Max(vcore, parts[i].rb.linearVelocity.magnitude);
            restT = (live ? vcore < .3f : vmax < .18f) ? restT + dt : 0;
            if (!live && (restT > .7f || simT > 6f || (Active.Count > MAX_SIM && Active[0] == this && simT > 1.5f))) Freeze();
        }

        void TrackPrev() { foreach (var b in TRACK) { var t = B(b); if (t) prevPos[b] = t.position; } }
        readonly Dictionary<HumanBodyBones, Vector3> prevPos = new();

        void Start2()
        {
            if (sim || done || !an) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-3f);
            Build();
            // pára quem escreve nos ossos depois do Animator (o próprio Animator, IK dos pés, mira, ator de rua, lobby)
            an.enabled = false;
            // [parceiro] fisica 31/10: em ragdoll VIVO tambem pausa os controladores de NPC/bot (Clear() religa; no GoLive o FBFall já pausou antes)
            foreach (var bh in GetComponents<Behaviour>()) if (bh != this && bh.enabled && (bh is FBFootIK || bh is FBStreetActor || bh is FBLobbyDirector || (ch.alive && (bh is FBCivilian || bh is FBRoleNpc || bh is FBBot)))) { bh.enabled = false; paused.Add(bh); }
            if (live)
            {   // r46d: impacto a 35 km/h — a junta do PhysX deixa separar até a projectionDistance (padrão 10 cm) antes de corrigir (sonda r46d:
                // bacia 152%, lombar 138% no 1º contato com o chão); vivo: 2 cm e mais iterações
                float s = Mathf.Max(.5f, ch.ScaleK);
                // r46g: passo de física de 12 ms aqui foi testado e TIRADO (sem ganho no osso esticado: 13,3% × 13,1%). A diferença na dobra ao
                // contrário daquele teste (7.819 × 42 quadros) era a variância entre execuções (Animator parado fora da câmera, RODADA46 §6)
                if (UseFine) Fine(true);
                foreach (var p in parts) { if (p.j is CharacterJoint cj) { cj.projectionDistance = .02f * s; cj.projectionAngle = 20; } p.rb.solverIterations = 24; p.rb.solverVelocityIterations = 10; p.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; }
            }
            foreach (var p in parts)
            {
                p.rb.isKinematic = false;
                var v = prevPos.TryGetValue(BoneOf(p.t), out var pp) ? (p.t.position - pp) / dt : Vector3.zero;
                if (v.magnitude > 9) v = v.normalized * 9; p.rb.linearVelocity = v + ch.velocity * .6f;
                if (live) { p.rb.linearVelocity = liveVel + Vector3.Cross(liveSpin, p.t.position - parts[0].t.position); p.rb.angularVelocity = liveSpin; }   // corpo inteiro sai com a moto, girando
            }
            if (waitImp.sqrMagnitude > 1e-4f)
            {   // impulso no osso mais perto do ponto atingido
                Part best = parts[0]; float bd = 1e9f; var ip = lastImpPoint != Vector3.zero ? lastImpPoint : ch.lastHit.valid ? ch.lastHit.point : parts[0].t.position;
                foreach (var p in parts) { float d = (p.t.position - ip).sqrMagnitude; if (d < bd) { bd = d; best = p; } }
                // [parceiro] fisica 31/10: limita o braco de alavanca: o ponto guardado pode ser um lastHit.point velho (pose em pe antes do clipe de morte,
                // ou tiro de antes nesta vida em morte por carro/queda/zona/sangramento), a metros ou dezenas de metros do corpo
                var bp = best.t.position; var ipc = bp + Vector3.ClampMagnitude(ip - bp, .25f * Mathf.Max(.5f, ch.ScaleK));
                best.rb.AddForceAtPosition(waitImp * best.rb.mass * .8f, ipc, ForceMode.Impulse);
                parts[0].rb.AddForce(waitImp * parts[0].rb.mass * .35f, ForceMode.Impulse);
            }
            sim = true; simT = 0; restT = 0; Active.Add(this); Built++;
        }
        HumanBodyBones BoneOf(Transform t) { foreach (var b in TRACK) if (B(b) == t) return b; return HumanBodyBones.LastBone; }

        void Freeze()
        {
            if (!sim) return; sim = false; done = true; Active.Remove(this); Settled++;
            Fine(false);
            foreach (var p in parts) { if (p.j) Destroy(p.j); }
            foreach (var p in parts) { if (p.rb) { p.rb.isKinematic = true; } if (p.col) p.col.enabled = false; }
            foreach (var (fc, _) in feet) if (fc) fc.enabled = false;
        }

        bool fine; void Fine(bool on) { if (on == fine) return; fine = on; FBMxBike.FineStep(on); }
        void OnDestroy() => Fine(false);

        /// <summary>Desmonta tudo (renascer/reviver): ossos voltam à camada original, Animator e scripts religados.</summary>
        public void Clear()
        {
            Active.Remove(this);
            foreach (var p in parts) { if (p.j) Destroy(p.j); }
            foreach (var p in parts) { if (p.col) Destroy(p.col); if (p.rb) Destroy(p.rb); if (p.t) p.t.gameObject.layer = p.layer; }
            foreach (var (fc, ly) in feet) if (fc) { fc.gameObject.layer = ly; Destroy(fc); } feet.Clear();
            parts.Clear(); prevPos.Clear(); hinges.Clear();
            foreach (var c in hitOff) if (c) c.enabled = true; hitOff.Clear();                        // r46: hitboxes de volta (ragdoll vivo)
            foreach (var bh in paused) if (bh) bh.enabled = true; paused.Clear();
            if (an) { an.enabled = true; an.Rebind(); }
            sim = done = live = false; aliveHold = false; /* [parceiro] fisica 31/10 */ waitT = -1; restT = simT = 0; lastImpPoint = Vector3.zero;
            Fine(false);
        }

        // ---------------- montagem
        void Build()
        {
            if (parts.Count > 0) return;
            float s = Mathf.Max(.5f, ch.ScaleK);
            Transform hips = B(HumanBodyBones.Hips), spine = B(HumanBodyBones.Spine) ?? B(HumanBodyBones.Chest), chest = B(HumanBodyBones.UpperChest) ?? B(HumanBodyBones.Chest) ?? spine,
                neck = B(HumanBodyBones.Neck), head = B(HumanBodyBones.Head);
            if (!hips || !spine || !head) return;
            var lU = B(HumanBodyBones.LeftUpperArm); var lL = B(HumanBodyBones.LeftLowerArm); var lH = B(HumanBodyBones.LeftHand);
            var rU = B(HumanBodyBones.RightUpperArm); var rL = B(HumanBodyBones.RightLowerArm); var rH = B(HumanBodyBones.RightHand);
            var lT = B(HumanBodyBones.LeftUpperLeg); var lS = B(HumanBodyBones.LeftLowerLeg); var lF = B(HumanBodyBones.LeftFoot);
            var rT = B(HumanBodyBones.RightUpperLeg); var rS = B(HumanBodyBones.RightLowerLeg); var rF = B(HumanBodyBones.RightFoot);
            // referencial do corpo AGORA (pode estar deitado): direita = quadril esq.->dir., cima = quadril->pescoço
            var right = (rT && lT ? rT.position - lT.position : hips.right).normalized; var up = ((neck ? neck.position : head.position) - hips.position).normalized;
            var fwd = Vector3.Cross(right, up).normalized;

            var pH = Add(hips, null, 12 * s); CapsuleBetween(pH, lT ? lT.position : hips.position - right * .1f * s, rT ? rT.position : hips.position + right * .1f * s, .11f * s, .06f * s);
            var pS = Add(spine, pH, 12 * s); CapsuleBetween(pS, spine.position, neck ? neck.position : head.position, .13f * s, 0);
            Ball(pS, pH, up, right, 22, 22, 18);
            var pHd = Add(head, pS, 5 * s); var hc = head.gameObject.AddComponent<SphereCollider>(); hc.radius = .1f * s / Sc(head); hc.center = head.InverseTransformPoint(head.position + up * .08f * s); pHd.col = hc;
            Ball(pHd, pS, up, right, 35, 30, 25);
            if (lU && lL && lH) Limb(pS, lU, lL, lH, 2.5f * s, 1.5f * s, .055f * s, .045f * s, fwd, 145, 60, 40);
            if (rU && rL && rH) Limb(pS, rU, rL, rH, 2.5f * s, 1.5f * s, .055f * s, .045f * s, fwd, 145, 60, 40);
            if (lT && lS && lF) Limb(pH, lT, lS, lF, 7 * s, 4 * s, .085f * s, .06f * s, -fwd, 150, 45, 30);
            if (rT && rS && rF) Limb(pH, rT, rS, rF, 7 * s, 4 * s, .085f * s, .06f * s, -fwd, 150, 45, 30);
            // r46f: PÉ — só a canela (joelho->tornozelo) tinha colisor; o pé (tornozelo->ponta) entrava até 28 cm no chão no ragdoll (sonda r46e).
            // Cápsula no próprio osso do pé (filho da canela, sem Rigidbody): vira parte do colisor composto da canela
            if (!NoFoot) foreach (var (f, toe, sh) in new[] { (lF, B(HumanBodyBones.LeftToes), lS), (rF, B(HumanBodyBones.RightToes), rS) })
            {
                if (!f || !sh || f.parent != sh || !parts.Exists(q => q.t == sh)) continue;
                var dir = toe ? (toe.position - f.position) : Vector3.ProjectOnPlane(f.forward, (f.position - sh.position).normalized) * .12f * s; if (dir.sqrMagnitude < 1e-6f) continue;
                var tip = f.position + dir + dir.normalized * .05f * s; var la = f.InverseTransformPoint(f.position); var lb = f.InverseTransformPoint(tip); var d = lb - la; float sc = Sc(f);
                var c = f.gameObject.AddComponent<CapsuleCollider>(); int ax = Mathf.Abs(d.x) > Mathf.Abs(d.y) ? (Mathf.Abs(d.x) > Mathf.Abs(d.z) ? 0 : 2) : (Mathf.Abs(d.y) > Mathf.Abs(d.z) ? 1 : 2);
                c.direction = ax; c.center = (la + lb) * .5f; c.radius = .045f * s / sc; c.height = Mathf.Abs(d[ax]) + 2 * c.radius;
                feet.Add((c, f.gameObject.layer)); f.gameObject.layer = Layer;
            }
            foreach (var (fc, _) in feet) foreach (var p in parts) if (p.col) Physics.IgnoreCollision(fc, p.col, true);
            foreach (var p in parts) { p.rb.isKinematic = true; p.rb.solverIterations = 14; p.rb.solverVelocityIterations = 6; p.rb.interpolation = RigidbodyInterpolation.Interpolate;
                p.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; p.rb.maxDepenetrationVelocity = 2.5f; p.rb.angularDamping = .6f; p.rb.linearDamping = .05f; }
            foreach (var a in parts) foreach (var b in parts) if (a != b && a.col && b.col) Physics.IgnoreCollision(a.col, b.col, Linked(a, b));   // vizinhos não se empurram
        }
        static bool Linked(Part a, Part b) => (a.j && a.j.connectedBody == b.rb) || (b.j && b.j.connectedBody == a.rb);
        static float Sc(Transform t) => Mathf.Max(1e-4f, t.lossyScale.x);
        Part Add(Transform t, Part parent, float mass)
        {
            var p = new Part { t = t, layer = t.gameObject.layer }; t.gameObject.layer = Layer;
            p.rb = t.gameObject.GetComponent<Rigidbody>(); if (!p.rb) p.rb = t.gameObject.AddComponent<Rigidbody>(); p.rb.mass = mass; parts.Add(p); return p;
        }
        static void CapsuleBetween(Part p, Vector3 a, Vector3 b, float r, float extra)
        {
            var t = p.t; var la = t.InverseTransformPoint(a); var lb = t.InverseTransformPoint(b); var d = lb - la; float sc = Sc(t);
            var c = t.gameObject.AddComponent<CapsuleCollider>(); int ax = Mathf.Abs(d.x) > Mathf.Abs(d.y) ? (Mathf.Abs(d.x) > Mathf.Abs(d.z) ? 0 : 2) : (Mathf.Abs(d.y) > Mathf.Abs(d.z) ? 1 : 2);
            c.direction = ax; c.center = (la + lb) * .5f; c.radius = r / sc; c.height = Mathf.Abs(d[ax]) + 2 * c.radius + extra / sc; p.col = c;
        }
        // braço/perna: rótula (ombro/quadril) + dobradiça (cotovelo/joelho) que só dobra para o lado 'flexTo'
        void Limb(Part root, Transform up, Transform low, Transform end, float m1, float m2, float r1, float r2, Vector3 flexTo, float flexMax, float sw1, float sw2)
        {
            var pu = Add(up, root, m1); CapsuleBetween(pu, up.position, low.position, r1, 0);
            var dirU = (low.position - up.position).normalized; var perp = Vector3.Cross(dirU, flexTo).sqrMagnitude > .01f ? flexTo : Vector3.Cross(dirU, Vector3.up);
            Ball(pu, root, dirU, Vector3.Cross(dirU, perp).normalized, 30, sw1, sw2);
            var pl = Add(low, pu, m2); CapsuleBetween(pl, low.position, end.position, r2, r2 * .5f);
            var dirL = (end.position - low.position).normalized;
            // eixo da dobradiça: normal do plano braço×antebraço se já dobrado; reto: perpendicular ao osso e ao lado da flexão
            var n = Vector3.Cross(dirU, dirL); if (n.sqrMagnitude < .02f) n = Vector3.Cross(dirU, flexTo); if (n.sqrMagnitude < 1e-4f) n = Vector3.Cross(dirU, Vector3.forward); n.Normalize();
            float a0 = Vector3.Angle(dirU, dirL);
            // sentido: girar +20° em torno de n tem de DOBRAR mais (afastar do prolongamento) e, reto, ir para 'flexTo'
            var test = Quaternion.AngleAxis(20, n) * dirL; bool more = Vector3.Angle(dirU, test) > a0 + 1; if (a0 < 8) more = Vector3.Dot(test - dirL, flexTo) > 0; if (!more) n = -n;
            // r46h: eixo ANATÔMICO quando o IK já aprendeu a dobra deste osso nas animações (FBIK.TryHinge). Pela pose do instante, braço quase reto
            // ou dobrado um pouco ao contrário (pose de pilotar) dava o eixo de lado ou invertido — e a trava anatômica passava a DEFENDER a dobra
            // ao contrário (sonda r46g/h: cotovelo com eixo −1,00 por centenas de quadros). Com ele, a0 pode sair negativo: a faixa abaixo só deixa
            // voltar para o lado certo
            if (FBIK.TryHinge(up, out var ha)) { ha -= dirU * Vector3.Dot(ha, dirU); if (ha.sqrMagnitude > 1e-4f) { n = ha.normalized; a0 = Vector3.SignedAngle(dirU, dirL, n); AnatAxes++; } }
            var j = low.gameObject.AddComponent<CharacterJoint>(); j.connectedBody = pu.rb; j.enableProjection = true; j.enablePreprocessing = false;
            j.axis = low.InverseTransformDirection(n); j.swingAxis = low.InverseTransformDirection(Vector3.Cross(n, dirL).normalized);
            // CharacterJoint: torção em torno de 'axis' medida A PARTIR desta pose. FlexSign = sentido da torção positiva do joint em relação
            // ao AngleAxis do Unity — medido no autoteste (FBRagdollTest), não suposto: a faixa permitida desdobra até reto e dobra até o máximo
            float more2 = Mathf.Clamp(flexMax - a0, 0, 177), back2 = Mathf.Clamp(a0, 0, 177);
            // r43: o sentido saiu diferente no cotovelo e no joelho (autoteste r42c com os dois sinais) — por articulação; e a trava anatômica
            // (Anatomy, a cada passo de física) segura o que o limite do PhysX deixar passar
            int fs = FlexForce != 0 ? FlexForce : flexTo == Vector3.zero ? FlexSign : (Vector3.Dot(flexTo, transform.forward) < 0 ? FlexSignLeg : FlexSignArm);
            float lowL = fs > 0 ? -back2 : -more2, highL = fs > 0 ? more2 : back2;
            j.lowTwistLimit = new SoftJointLimit { limit = lowL }; j.highTwistLimit = new SoftJointLimit { limit = highL };
            j.swing1Limit = new SoftJointLimit { limit = 3 }; j.swing2Limit = new SoftJointLimit { limit = 3 };
            pl.j = j; pl.up = pu; pl.restL = Quaternion.Inverse(pu.t.rotation) * (low.position - pu.t.position); hinges.Add(new Hinge { name = low.name, up = up, low = low, end = end, nLocal = up.InverseTransformDirection(n), max = flexMax, a0 = Vector3.SignedAngle(dirU, dirL, n), rbU = pu.rb, rbL = pl.rb });
        }
        // ---- conferência (autoteste FBDeathTest): dobra atual de cada joelho/cotovelo no sentido anatômico (negativo = dobrado ao contrário)
        class Hinge { public string name; public Transform up, low, end; public Vector3 nLocal; public float max, a0; public Rigidbody rbU, rbL; }
        readonly List<Hinge> hinges = new();
        readonly List<(Collider c, int layer)> feet = new();
        public IEnumerable<(string name, float flex, float max)> Hinges()
        {
            foreach (var h in hinges) if (h.up && h.low && h.end)
                    yield return (h.name, Vector3.SignedAngle((h.low.position - h.up.position).normalized, (h.end.position - h.low.position).normalized, h.up.TransformDirection(h.nLocal)), h.max);
        }
        public float SimTime => simT;
        /// <summary>Dobra de cada joelho/cotovelo no instante em que a física assumiu (pose final do clipe).</summary>
        public IEnumerable<(string name, float a0)> HingesAtStart() { foreach (var h in hinges) yield return (h.name, h.a0); }
        // r46i: connectedMassScale = massa do pai / massa do filho foi testado e TIRADO; a "explosão" daquele teste (pescoço 3.718%) era a
        // variância entre execuções (RODADA46 §6), não a escala — sem ganho medido depois disso. Quadril e tronco têm a mesma massa (não ajudaria na lombar)
        static void Ball(Part p, Part parent, Vector3 twistAxisW, Vector3 swingAxisW, float twist, float sw1, float sw2)
        {
            var j = p.t.gameObject.AddComponent<CharacterJoint>(); j.connectedBody = parent.rb; j.enableProjection = true; j.enablePreprocessing = false;
            j.axis = p.t.InverseTransformDirection(twistAxisW.normalized); j.swingAxis = p.t.InverseTransformDirection(swingAxisW.normalized);
            j.lowTwistLimit = new SoftJointLimit { limit = -twist }; j.highTwistLimit = new SoftJointLimit { limit = twist };
            j.swing1Limit = new SoftJointLimit { limit = sw1 }; j.swing2Limit = new SoftJointLimit { limit = sw2 };
            var sp = new SoftJointLimitSpring { spring = 0, damper = 0 }; j.twistLimitSpring = sp; j.swingLimitSpring = sp; p.j = j; p.up = parent; p.restL = Quaternion.Inverse(parent.t.rotation) * (p.t.position - parent.t.position);
        }
    }
}
