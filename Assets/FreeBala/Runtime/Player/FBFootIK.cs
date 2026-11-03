// IK dos pés (foot placement) reutilizável — jogador, bots, civis e remotos usam o mesmo componente (FBCharacter registra no Awake e chama
// Tick no LateUpdate, depois da inclinação Q/E e antes do coldre).
//
// Por que pós-animação: os clipes convertidos NÃO têm curvas de alvo de IK (GetIKPosition do Animator devolve a posição do corpo e o pé
// subia até a cintura). Então tudo sai do OSSO já animado:
//  - detecção independente por pé: SphereCast fino sob o tornozelo (+ raio na ponta do pé) contra mapa/veículos/props, ignorando o próprio
//    corpo, gatilhos, paredes (normal > 60°) e corpos físicos em movimento (carro andando não "puxa" o pé);
//  - pelve: desce pelo pé que mais precisa descer (degrau, meio-fio, ladeira; também tira a folga do skinWidth do CharacterController),
//    com limites (desce até 0,45 m, sobe até 0,06 m) e suavização; aplicada no OnAnimatorIK (bodyPosition), antes do Animation Rigging;
//    o salto de degrau do CharacterController (sobe/desce num quadro) é absorvido pela pelve e volta com mola amortecida;
//  - joelho: IK de dois ossos com polo na direção em que o clipe já dobra o joelho (perna reta = para a frente do corpo);
//  - pé alinhado à normal do chão (até 35°, mais forte quando o pé está apoiado) e ponta do pé que nunca entra no degrau/pedra;
//  - foot lock: a fase de apoio é derivada do movimento do osso (altura sobre o descanso + velocidade horizontal no mundo); o pé apoiado
//    fica preso no ponto do chão com peso dinâmico e solta suave quando o clipe levanta o pé ou estica demais (0,2 m);
//  - inclinação sutil do tronco em subida/descida (rampa e escada), medida pelo deslocamento real do corpo; some ao mirar/atirar.
// Qualidade/LOD (FBQuality): até FootIKDistance·0,5 (máx. 18 m) = tudo, raios no ritmo do FootIKFor/FootIKInterval; até FootIKDistance =
// raios a cada ≥ 3 quadros, sem trava/ponta/tronco; além disso ou fora da tela = desligado. Jogador sempre completo. FBQuality.FootIK desliga tudo.
using UnityEngine;

namespace FreeBala
{
    [DisallowMultipleComponent]
    public class FBFootIK : MonoBehaviour
    {
        // ---- parâmetros (metros do boneco padrão; multiplicados pela escala do corpo)
        public static class Q
        {
            // distâncias e intervalo vêm do FBQuality (FootIKDistance / FootIKInterval / FootIKFor); estes valem só se o override for > 0
            public static float NearDist = -1, FarDist = -1;            // override de teste (≤ 0 = usar o nível de qualidade)
            public static int MidRayEvery = 3;                          // longe do perto: raios no mínimo a cada N quadros (IK continua por quadro)
            public static float Far => FarDist > 0 ? FarDist : Mathf.Max(8, FBQuality.FootIKDistance);
            public static float Near => NearDist > 0 ? NearDist : Mathf.Min(Far * .5f, 18);
            public static bool Lock = true, Toe = true, Tilt = true, Clear = true;   // Clear = pé em balanço sobe antes da quina do degrau    // recursos do nível perto
            public static bool IgnoreVisibility;                        // testes sem câmera renderizando (batchmode)
        }
        const float CastUp = .62f, CastDown = .6f, CastR = .045f;       // raio começa acima do maior degrau (stepOffset .45)
        const float MaxDown = .45f, MaxUp = .06f, MaxAlign = 35, MaxLock = .2f, WallAngle = 60;
        const float AheadDist = .24f, ClearMin = .03f, ClearMax = .5f, ClearMargin = .035f;   // amostra à frente do pé (m), faixa de degrau que levanta o pé, folga sobre a quina

        // estado de cada pé (público para o teste PlayMode e diagnóstico)
        public class Foot
        {
            public Transform th, kn, ft, toe;
            public bool Hit; public float GroundY, GroundS, Plant, LockW, RestH = -1, ToeRestH = -1, HitW;
            public Vector3 Normal = Vector3.up, NormalS = Vector3.up, Target, Ankle, LockPos, prevAnim; public bool Locked, hasPrev, relockBlock, moving;
            // cache dos raios (nível médio)
            public float aheadY, ClearW; public bool aheadHit;   // chão à frente do pé (degrau) e peso suavizado do levantar do pé
            public Vector3 rayXZ; public int rayFrame = -99; public float rayY, toeY; public bool rayHit, toeHit; public Vector3 rayN = Vector3.up; public Rigidbody rayBody;
            public Collider rayCol;                                     // r51: onde o pé apoiou (diagnóstico da postura ao sair do veículo)
        }
        public readonly Foot Left = new(), Right = new();
        public float HipsOffset => hipsOff; public float Weight => footW; public int Lod { get; private set; }
        public float Tilt => tiltS;

        FBCharacter ch; Animator an; Transform spine; Renderer vis;
        float footW, hipsOff, hipsV, appliedOff, tiltS, slopeS, airT, ikBaseY; bool ikHasBase; Vector3 lastPos; bool hasLast; Foot[] pair;
        float S => ch ? ch.ScaleK : 1;

        public void Init(FBCharacter c)
        {
            ch = c; an = c.an; if (!an || !an.isHuman) return;
            void Leg(Foot f, HumanBodyBones a, HumanBodyBones b, HumanBodyBones cft, HumanBodyBones t) { f.th = an.GetBoneTransform(a); f.kn = an.GetBoneTransform(b); f.ft = an.GetBoneTransform(cft); f.toe = an.GetBoneTransform(t); }
            Leg(Left, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes);
            Leg(Right, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes);
            spine = an.GetBoneTransform(HumanBodyBones.Spine); pair = new[] { Left, Right };
            vis = GetComponentInChildren<SkinnedMeshRenderer>(true); if (!vis) vis = GetComponentInChildren<Renderer>(true);
        }

        // pelve: aplicada durante a avaliação do Animator (o Animation Rigging — mira do tronco, mão no guarda-mão — já vê o corpo no lugar)
        void OnAnimatorIK(int layer)
        {
            if (layer != 0 || !an) return;
            if (HardOff) { appliedOff = 0; ikHasBase = false; return; }       // banco de veículo: o FBDriverPose é o único dono dos ossos
            // o CharacterController sobe/desce o degrau num quadro só: a pelve absorve o salto e volta suave (sem tranco no quadril/câmera 1ª p.)
            float by = transform.position.y, dy = ikHasBase ? by - ikBaseY : 0; ikBaseY = by; ikHasBase = true;
            if (footW > .02f && airT <= 0 && Mathf.Abs(dy) > .02f * S && Mathf.Abs(dy) < .6f * S) hipsOff = Mathf.Clamp(hipsOff - dy, -.7f * S, .55f * S);
            appliedOff = hipsOff; if (hipsOff != 0) an.bodyPosition += Vector3.up * hipsOff;
        }

        bool Active(out int lod)
        {
            lod = 2;
            if (!FBQuality.FootIK || !ch || !Left.ft || !Right.ft) return false;
            bool spc = ch.special || !ch.alive || ch.DanW > .05f || ch.vehiclePose;
            if (spc || airT > .15f) return false;                       // tolera a piscada de 'no ar' ao descer rampa/degrau
            if (vis && !vis.isVisible && !Q.IgnoreVisibility) return false;
            var cam = Camera.main; float d = cam ? (cam.transform.position - transform.position).magnitude : 0;
            if (ch.isPlayer) d = 0;
            lod = d < Q.Near * S ? 0 : d < Q.Far * S ? 1 : 2;
            return lod < 2;
        }

        // desligado na hora (sem fade): no veículo a pelve/pernas são do FBDriverPose. Antes o fade de ~0,4 s raiava o banco/assoalho e a
        // compensação de degrau realimentava com o PoseActor (que move a raiz até o quadril cair no banco) → pernas e quadril pulando ao entrar;
        // na saída o salto banco → chão era "absorvido" pela pelve (corpo ficava no alto e descia)
        bool HardOff => ch && ch.vehiclePose;
        void Reset0() { footW = 0; hipsOff = hipsV = appliedOff = tiltS = 0; ikHasBase = hasLast = false; foreach (var f in pair ?? new Foot[0]) { f.hasPrev = f.Locked = false; f.LockW = f.HitW = f.ClearW = 0; f.aheadHit = false; f.rayFrame = -99; } }
        public void Tick(float dt)
        {
            if (dt <= 0 || !an || !an.isHuman) return;
            if (HardOff) { Reset0(); return; }
            airT = ch && ch.grounded ? 0 : airT + dt;
            bool on = Active(out int lod); Lod = lod;
            footW = Mathf.Lerp(footW, on ? 1 : 0, 1 - Mathf.Exp(-dt * 10));
            TrackSlope(dt);
            if (footW < .02f)
            {
                hipsOff = Mathf.SmoothDamp(hipsOff, 0, ref hipsV, .1f, Mathf.Infinity, dt); tiltS = Mathf.Lerp(tiltS, 0, 1 - Mathf.Exp(-dt * 8));
                Left.hasPrev = Right.hasPrev = false; Left.Locked = Right.Locked = false; Left.LockW = Right.LockW = 0; return;
            }
            float baseY = transform.position.y, need = 1e9f; bool any = false;
            foreach (var f in pair) { if (Solve(f, baseY, lod, dt, out float d)) { need = Mathf.Min(need, d); any = true; } }
            float tgt = any ? Mathf.Clamp(need, -MaxDown * S, MaxUp * S) * footW : 0;
            hipsOff = Mathf.SmoothDamp(hipsOff, tgt, ref hipsV, hipsOff > tgt ? .05f : .085f, Mathf.Infinity, dt);   // mola amortecida: sem tranco (velocidade contínua)
            TorsoTilt(dt, lod);
        }

        // inclinação do terreno ao longo do movimento (rampa e escada): subida real ÷ deslocamento horizontal, suavizada
        void TrackSlope(float dt)
        {
            var p = transform.position;
            if (hasLast && ch.grounded) { float hz = new Vector2(p.x - lastPos.x, p.z - lastPos.z).magnitude; if (hz > 1e-3f) slopeS = Mathf.Lerp(slopeS, Mathf.Clamp((p.y - lastPos.y) / hz, -1.2f, 1.2f), 1 - Mathf.Exp(-dt * 4)); }
            else slopeS = Mathf.Lerp(slopeS, 0, 1 - Mathf.Exp(-dt * 4));
            lastPos = p; hasLast = true;
        }
        void TorsoTilt(float dt, int lod)
        {
            float g = ch.velocity.Flat().magnitude / Mathf.Max(.01f, S); bool busy = ch.aiming || ch.shooting || ch.lastShot < .6f || ch.prone;
            float want = Q.Tilt && lod == 0 && !busy ? Mathf.Clamp(Mathf.Atan(slopeS) * Mathf.Rad2Deg * .18f, -4, 6) * Mathf.Clamp01((g - .4f) / 1.5f) : 0;
            tiltS = Mathf.Lerp(tiltS, want * footW, 1 - Mathf.Exp(-dt * 5));
            if (spine && Mathf.Abs(tiltS) > .05f) spine.Rotate(transform.right, tiltS, Space.World);   // + = tronco à frente (subida)
        }

        // ---- chão sob um ponto (SphereCast fino; raio simples como reserva)
        static readonly RaycastHit[] hits = new RaycastHit[10]; static Collider lastCol;
        static int Mask => (1 << FBWorld.LMap) | (1 << FBWorld.LVehicle) | 1;
        bool Ground(Vector3 at, float baseY, float r, out float y, out Vector3 n, out Rigidbody body)
        {
            y = 0; n = Vector3.up; body = null; float top = baseY + CastUp * S, far = (CastUp + CastDown) * S;
            var o = new Vector3(at.x, top, at.z); float bd = 1e9f;
            for (int pass = 0; pass < 2 && bd >= 1e9f; pass++)
            {
                int cnt = pass == 0 && r > 0 ? Physics.SphereCastNonAlloc(o, r, Vector3.down, hits, far, Mask, QueryTriggerInteraction.Ignore)
                                             : Physics.RaycastNonAlloc(o, Vector3.down, hits, far, Mask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < cnt; i++)
                {
                    var h = hits[i]; if (h.distance <= 0 || h.distance >= bd || !h.collider) continue;          // começou dentro de algo: ignora
                    if (h.collider.transform.IsChildOf(transform)) continue;                                // próprio corpo (arma, mochila, coldre)
                    if (Vector3.Angle(h.normal, Vector3.up) > WallAngle) continue;                          // lateral de parede/meio-fio
                    var rb = h.collider.attachedRigidbody;
                    if (rb && !rb.isKinematic && rb.linearVelocity.sqrMagnitude > .25f) continue;           // carro/objeto andando
                    bd = h.distance; y = h.point.y; n = h.normal; body = rb; lastCol = h.collider;
                }
            }
            return bd < 1e9f;
        }

        // um pé: devolve quanto a pelve precisa descer (d < 0) para este pé alcançar o alvo
        bool Solve(Foot f, float baseY, int lod, float dt, out float need)
        {
            need = 0; if (!f.th || !f.kn || !f.ft) return false;
            float s = S; var fp = f.ft.position;
            var anim = fp - Vector3.up * appliedOff;                                     // pé do clipe sem o ajuste de pelve
            float h = anim.y - baseY;
            if (f.RestH < 0) f.RestH = Mathf.Clamp(h, .02f * s, .2f * s);
            f.RestH = Mathf.Clamp(Mathf.Min(f.RestH + dt * .01f * s, h), .02f * s, .2f * s);   // altura do tornozelo apoiado (aprende do clipe)
            float lift = Mathf.Max(0, h - f.RestH);
            // fase de apoio: pé perto da altura de descanso e quase parado no mundo
            float sp = 0; if (f.hasPrev) sp = new Vector2(anim.x - f.prevAnim.x, anim.z - f.prevAnim.z).magnitude / dt; f.prevAnim = anim; f.hasPrev = true;
            // velocidade relativa ao corpo: com o ritmo do clipe fora do chão (an.speed limitado 0,6–1,9) o pé apoiado escorrega no mundo
            // mais de 1 m/s e o limite absoluto nunca via apoio andando (trava não engatava — exe: "apoio 0,0"); o pé no ar anda ~2× o corpo
            float body = ch ? ch.velocity.Flat().magnitude : 0, v0 = .3f * s + .45f * body, v1 = 1f * s + .8f * body;
            float plantRaw = (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.02f * s, .08f * s, lift))) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(v0, v1, sp)));
            f.Plant = Mathf.Lerp(f.Plant, plantRaw, 1 - Mathf.Exp(-dt * 30));
            // trava do pé apoiado
            bool canLock = Q.Lock && lod == 0 && f.HitW > .5f && !(f.rayBody && !f.rayBody.isKinematic);
            var animXZ = new Vector3(anim.x, 0, anim.z);
            if (f.relockBlock && f.Plant < .35f) f.relockBlock = false;
            if (!f.Locked && canLock && !f.relockBlock && f.Plant > .7f && f.LockW < .05f) { f.Locked = true; f.LockPos = animXZ; }
            if (f.Locked)
            {
                bool far = (animXZ - f.LockPos).magnitude > MaxLock * s;
                if (far || f.Plant < .35f || !canLock) { f.Locked = false; f.relockBlock = far; }
            }
            f.LockW = Mathf.MoveTowards(f.LockW, f.Locked ? 1 : 0, dt * (f.Locked ? 14 : 8));
            var xz = Vector3.Lerp(animXZ, f.LockPos, f.LockW);
            // raios (refeitos a cada quadro de perto; no médio a cada N quadros ou quando o pé andou)
            // FootIKFor = quadro de refazer os raios (escalonado por personagem); o IK em si roda todo quadro (o Animator reescreve os ossos)
            int every = lod == 0 ? Mathf.Max(1, FBQuality.FootIKInterval) : Mathf.Max(Q.MidRayEvery, FBQuality.FootIKInterval);
            bool due = lod == 0 ? FBQuality.FootIKFor(transform.position, ch.isPlayer, System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this)) || Time.frameCount - f.rayFrame >= every : Time.frameCount - f.rayFrame >= every;
            if (due || (xz - f.rayXZ).magnitude > .04f * s)
            {
                lastCol = null; f.rayHit = Ground(xz, baseY, CastR * s, out f.rayY, out f.rayN, out f.rayBody); f.rayCol = f.rayHit ? lastCol : null; f.rayXZ = xz; f.rayFrame = Time.frameCount;
                f.toeHit = false; f.aheadHit = false;
                // degrau à frente: chão na direção do movimento (só perto; o pé em balanço sobe antes da quina em vez de atravessar o espelho do degrau)
                if (Q.Clear && lod == 0 && ch) { var mv = ch.velocity.Flat(); if (mv.magnitude > .3f * s) f.aheadHit = Ground(new Vector3(xz.x, 0, xz.z) + mv.normalized * AheadDist * s, baseY, CastR * s, out f.aheadY, out _, out _); }
                if (Q.Toe && lod == 0 && f.toe) { var tp = f.toe.position - fp + new Vector3(xz.x, 0, xz.z); f.toeHit = Ground(tp, baseY, 0, out f.toeY, out _, out _); }
            }
            f.HitW = Mathf.MoveTowards(f.HitW, f.rayHit ? 1 : 0, dt * 8);
            f.Hit = f.rayHit;
            if (!f.rayHit) { f.Ankle = fp; return false; }
            f.GroundY = f.rayY; f.Normal = f.rayN;
            // suavizado só no ar; pé apoiado usa o chão real (descendo escada correndo o suavizado ficava acima → pé flutuando)
            f.GroundS = Mathf.Abs(f.GroundS - f.GroundY) > .8f * s || f.Plant > .5f ? f.GroundY : Mathf.Lerp(f.GroundS, f.GroundY, 1 - Mathf.Exp(-dt * 22));
            // normal limitada e suavizada
            var n = f.Normal; float ang = Vector3.Angle(n, Vector3.up); if (ang > MaxAlign) n = Vector3.Slerp(Vector3.up, n, MaxAlign / ang);
            f.NormalS = Vector3.Slerp(f.NormalS, n, 1 - Mathf.Exp(-dt * 15)).normalized;
            float cosA = Mathf.Max(.8f, f.NormalS.y);
            float ty = Mathf.Max(f.GroundS + f.RestH / cosA + lift, f.GroundY + f.RestH * .9f);   // nunca abaixo do chão real
            // pé em balanço com degrau à frente: o alvo nunca fica abaixo da quina do próximo degrau (peso sobe rápido e solta devagar, sem pulo)
            float up = f.aheadHit ? f.aheadY - f.GroundY : 0, clearT = Q.Clear && up > ClearMin * s && up < ClearMax * s ? (1 - f.Plant) * footW : 0;
            f.ClearW = Mathf.Lerp(f.ClearW, clearT, 1 - Mathf.Exp(-dt * (clearT > f.ClearW ? 22 : 12)));
            if (f.ClearW > .005f) ty = Mathf.Lerp(ty, Mathf.Max(ty, f.aheadY + f.RestH + ClearMargin * s), f.ClearW);
            var target = new Vector3(xz.x, ty, xz.z);
            // ponta do pé: depois de alinhado, a ponta não pode entrar no degrau/pedra à frente
            float alignW = (.3f + .7f * f.Plant) * footW;
            var alignQ = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, f.NormalS), alignW);
            if (f.toeHit && f.toe)
            {
                var toePred = target + alignQ * (f.toe.position - fp); float th = f.toe.position.y - appliedOff - baseY;
                if (f.ToeRestH < 0) f.ToeRestH = Mathf.Clamp(th, .005f * s, .1f * s); f.ToeRestH = Mathf.Clamp(Mathf.Min(f.ToeRestH + dt * .01f * s, th), .005f * s, .1f * s);
                // só pedrinha/inclinação sob a ponta (até 10 cm acima do chão do tornozelo, sobe no máx. 4 cm); ponta encostada no PRÓXIMO degrau/obstáculo
                // não levanta o pé (antes subia até 15 cm e o pé apoiado flutuava sobre o degrau de baixo)
                float raise = f.toeY + f.ToeRestH * .8f - toePred.y; if (raise > 0 && f.toeY - f.GroundY < .1f * s) target.y += Mathf.Min(raise, .04f * s);
            }
            f.Target = target;
            need = target.y - anim.y;
            // IK: perna dobra até o alvo (pelve deste quadro já aplicada nos ossos); polo = dobra do clipe, reta = frente do corpo
            float w = footW * f.HitW;
            var goal = Vector3.Lerp(fp, target, w);
            if ((goal - fp).sqrMagnitude > 1e-6f)
            {
                var fr = f.ft.rotation;
                var bend = f.kn.position - (f.th.position + fp) * .5f; float bk = Mathf.Clamp01(bend.magnitude / (.06f * s));
                var pdir = (bk > 0 ? bend.normalized * bk : Vector3.zero) + transform.forward * (1 - bk);
                FBIK.TwoBonePole(f.th, f.kn, f.ft, goal, f.kn.position + pdir.normalized * .5f * s);
                f.ft.rotation = fr;
            }
            f.ft.rotation = Quaternion.Slerp(Quaternion.identity, alignQ, w) * f.ft.rotation;
            f.Ankle = f.ft.position;
            return true;
        }

        // diagnóstico (autoteste/HUD de debug)
        public string DebugText() => $"w {footW:0.00} lod {Lod} pelve {hipsOff:+0.000;-0.000} tronco {tiltS:0.0}° · E {F(Left)} · D {F(Right)}";
        static string F(Foot f) => $"chão {f.GroundY:0.00} apoio {f.Plant:0.0} trava {f.LockW:0.0} desc {f.RestH:0.00}";
    }
}
