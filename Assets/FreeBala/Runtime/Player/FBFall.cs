// r45d: QUEDA DA MOTO — clipe de susto do 171 (SCR_Stand_Spooked_Fall_*) com o deslocamento virando raiz.
// r46 (pedido 29/10: "rig totalmente distorcido, poses impossíveis e movimentos artificiais" nas quedas): REFEITO com física.
// Causas medidas (FBClipAudit -fbposture, logs/r46b_posture.log): os Spooked_Fall são de quem leva um susto EM PÉ — o quadril nunca
// passa de 0,4 m do chão nos de frente/de costas (o corpo "senta no ar" agachado) e os de lado caem sentados (tronco 0,8 em pé);
// tocados a partir da pose de pilotar, com o piloto teleportado para o lado da moto, davam o rig torto e a queda sem peso nenhum.
// Agora, em três fases:
//  1) FÍSICA — o corpo sai DA POSE EM QUE ESTAVA NA MOTO (Grab antes de soltar o piloto: soltar teleporta a raiz) como ragdoll vivo
//     (FBRagdoll.GoLive: mesmas juntas com trava anatômica da morte), com a velocidade da moto, empurrão para o lado em que ela tomba
//     e giro do corpo inteiro; não colide com a própria moto. Raiz parada, CharacterController desligado, IA pausada, câmera no quadril.
//  2) ASSENTOU (parado 0,4 s, ou 5 s no máximo) — de costas ou de bruços pelo referencial do corpo (coxas × coluna, não o eixo local
//     do osso do quadril, que muda de rig para rig): de costas = IE_Downed_GetUp (começa deitado, peito para cima), de bruços =
//     FF_SYS_KO_GetUp (começa deitado, peito para baixo). A raiz vai para o chão debaixo do quadril, virada para que a cabeça do clipe
//     aponte para onde a cabeça do corpo está, e o clipe entra com peso cheio no mesmo quadro.
//  3) LEVANTAR — FBPoseBlend leva cada osso da pose do ragdoll à do clipe em 0,5 s; no fim, mesma troca da saída do veículo (pose
//     guardada, raiz para baixo do quadril, clipe cortado) para não haver estalo ao voltar ao parado.
// Também usado pelo atropelamento (FBAtropelo), que sofria do mesmo defeito de desmontar o ragdoll no quadro seguinte.
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    [DefaultExecutionOrder(1100)]
    public class FBFall : MonoBehaviour
    {
        public static int Started, Finished, GotUpBack, GotUpFront; public static string Diag;
        FBCharacter ch; CharacterController cc; FBRagdoll rd; string clip, kind; float t, len, simT; bool on, rising; float minT;
        float gy0, lift, liftSet;   // r46f: chão do levantar + quanto a raiz subiu para apoiar mãos/joelhos/pés
        readonly List<Behaviour> paused = new();
        public static bool Busy(FBCharacter c) => c && c.TryGetComponent<FBFall>(out var f) && f.on;
        public static bool Ragdolling(FBCharacter c) => c && c.TryGetComponent<FBFall>(out var f) && f.on && !f.rising;
        public static string ClipOf(FBCharacter c) => c && c.TryGetComponent<FBFall>(out var f) && f.on ? (f.rising ? f.clip : "ragdoll") : null;
        // câmera: durante a física a raiz fica parada e o corpo vai embora — o pivô segue o quadril
        public static Vector3 CamPos(FBCharacter c)
        {
            if (!Ragdolling(c) || !c.Hips) return c.Pos; var h = c.Hips.position; return new Vector3(h.x, Mathf.Min(c.Pos.y, h.y - .12f * c.ScaleK), h.z);
        }
        // para onde o piloto vai: loop do grau -> de costas; batida forte -> para a frente; senão para o lado em que a moto tomba
        public static string DirFor(FBCar car, string why)
        {
            if (why == "loop" || (car && car.mx && car.mx.wheelieAngle > 45)) return "Back";
            if (why == "batida" && car && Mathf.Abs(car.mx ? car.mx.speedKmh / 3.6f : car.v) > 6) return "Fwd";
            if (!car) return "Left"; var T = car.mx ? car.mx.transform : car.transform; var right = Vector3.Cross(Vector3.up, T.forward.Flat().normalized);
            return Vector3.Dot(T.up, right) > 0 ? "Right" : "Left";
        }
        // ponto no chão do lado em que cai (perto da moto) — usado quando não há pose guardada (personagem em pé)
        public static Vector3 FallPoint(FBCar car, string dir)
        {
            var T = car.mx ? car.mx.transform : car.transform; var f = T.forward.Flat().normalized; var r = Vector3.Cross(Vector3.up, f);
            var p = T.position + (dir == "Right" ? r * .8f : dir == "Left" ? -r * .8f : dir == "Fwd" ? f * 1.2f : -f * 1.1f);
            car.SetColliders(false); var gy = FBWorld.GroundY(p.x, p.y + 2, p.z, .3f, 4); car.SetColliders(true); if (gy is float g) p.y = g; return p;
        }

        // ---- pose do instante antes de soltar o piloto (soltar = Exit/Release/Leave teleporta a raiz para o lado da moto)
        class Snap { public int frame; public Vector3 hipP; public Quaternion hipQ; public Transform[] b; public Quaternion[] q; public FBCar car; }
        static readonly Dictionary<FBCharacter, Snap> snaps = new();
        public static void Grab(FBCharacter c, FBCar car = null)
        {
            var an = c ? c.an : null; if (!an || !an.isHuman) return; var hips = an.GetBoneTransform(HumanBodyBones.Hips); if (!hips) return;
            var L = new List<Transform>(); for (int i = 0; i < (int)HumanBodyBones.LastBone; i++) { var x = an.GetBoneTransform((HumanBodyBones)i); if (x && x != hips) L.Add(x); }
            var s = new Snap { frame = Time.frameCount, hipP = hips.position, hipQ = hips.rotation, b = L.ToArray(), car = car }; s.q = new Quaternion[s.b.Length];
            for (int i = 0; i < s.b.Length; i++) s.q[i] = s.b[i].localRotation; snaps[c] = s;
        }
        static bool Restore(FBCharacter c, out FBCar car)
        {
            car = null; if (!snaps.TryGetValue(c, out var s)) return false; snaps.Remove(c); if (s.frame != Time.frameCount) return false;
            var hips = c.Hips; if (!hips) return false; hips.SetPositionAndRotation(s.hipP, s.hipQ); for (int i = 0; i < s.b.Length; i++) if (s.b[i]) s.b[i].localRotation = s.q[i];
            car = s.car; return true;
        }

        public static bool FromBike(FBCharacter c, FBCar car, string why)
        {
            var dir = DirFor(car, why); var T = car.mx ? car.mx.transform : car.transform; var v = car.mx ? T.forward * car.mx.speedKmh / 3.6f * FBMxBike.S : T.forward * car.v;
            return Begin(c, FallPoint(car, dir), T.eulerAngles.y, v, dir, car);
        }
        // dir: Left / Right / Fwd / Back (lado do piloto); vel = velocidade da moto na hora da queda
        public static bool Begin(FBCharacter c, Vector3 at, float yaw, Vector3 vel, string dir) => Begin(c, at, yaw, vel, dir, null);
        public static bool Begin(FBCharacter c, Vector3 at, float yaw, Vector3 vel, string dir, FBCar car)
        {
            if (!c || !c.alive) return false;
            bool grabbed = Restore(c, out var gcar); car ??= gcar;
            if (!grabbed) { FBMatch.Teleport(c, at, yaw); c.StopDance(true); }
            var rd = FBRagdoll.Of(c);
            if (!rd)
            {   // sem esqueleto humanoide: como antes (levanta do chão)
                if (grabbed) FBMatch.Teleport(c, at, yaw); var cc0 = c.GetComponent<CharacterController>(); if (cc0) cc0.enabled = true;
                if (FBRegistry.I && FBRegistry.I.Clip(FBCombat.GetUpClip)) c.PlayDance(FBCombat.GetUpClip); return false;
            }
            var R = Quaternion.Euler(0, yaw, 0); Vector3 f = R * Vector3.forward, r = R * Vector3.right; vel = Vector3.ClampMagnitude(vel.Flat(), 16);
            float sp = vel.magnitude;
            // empurrão e giro de cada tipo de queda (u/s e rad/s; o giro é do corpo inteiro em torno do quadril)
            Vector3 v, spin;
            switch (dir)
            {
                case "Fwd": v = vel * .95f + Vector3.up * (1.6f + sp * .08f); spin = r * Mathf.Clamp(sp * .45f, 2, 6); break;            // batida: passa por cima do guidão
                case "Back": v = vel * .55f - f * 1.2f + Vector3.up * 1.4f; spin = -r * 3.2f; break;                                    // loop do grau: cai de costas
                case "Right": v = vel * .85f + r * (1.6f + sp * .06f) + Vector3.up * .8f; spin = -f * Mathf.Clamp(1.5f + sp * .2f, 1.5f, 4.5f); break;
                default: v = vel * .85f - r * (1.6f + sp * .06f) + Vector3.up * .8f; spin = f * Mathf.Clamp(1.5f + sp * .2f, 1.5f, 4.5f); break;   // Left
            }
            var fall = c.gameObject.GetOrAdd<FBFall>(); fall.Stop(true);
            fall.ch = c; fall.cc = c.GetComponent<CharacterController>(); fall.rd = rd; fall.kind = car ? "moto" : "queda"; fall.clip = null;
            fall.t = 0; fall.simT = 0; fall.minT = 1.0f; fall.rising = false; fall.on = true; fall.enabled = true;
            fall.Prepare();
            rd.GoLive(v, spin, Vector3.zero, Vector3.zero, car ? car.GetComponentsInChildren<Collider>() : null);
            Started++; Diag = $"ragdoll {dir} · {sp * 3.6f / 1.17f:0} km/h"; return true;
        }
        /// <summary>Atropelamento: ragdoll vivo com o impulso do carro; levanta quando o corpo assentar (mínimo 'minSecs').</summary>
        public static bool Knock(FBCharacter c, Vector3 imp, Vector3 at, float minSecs, FBCar car)
        {
            var rd = c && c.alive ? FBRagdoll.Of(c) : null; if (!rd) return false;
            c.StopDance(true); var fall = c.gameObject.GetOrAdd<FBFall>(); fall.Stop(true);
            fall.ch = c; fall.cc = c.GetComponent<CharacterController>(); fall.rd = rd; fall.kind = "atropelo"; fall.clip = null;
            fall.t = 0; fall.simT = 0; fall.minT = minSecs; fall.rising = false; fall.on = true; fall.enabled = true;
            fall.Prepare(); c.fleeing = true;
            rd.GoLive(c.velocity * .5f, Vector3.zero, imp, at, car ? car.GetComponentsInChildren<Collider>() : null);
            Started++; Diag = $"atropelo · {imp.magnitude:0.0}"; return true;
        }
        void Prepare()
        {
            FBPoseBlend.Ensure(ch); if (!TryGetComponent<FBFallSupport>(out _)) gameObject.AddComponent<FBFallSupport>().f = this;   // r49: criado antes (não recebe LateUpdate no quadro em que nasce)
            if (cc) cc.enabled = false; ch.sys = null; ch.velocity = Vector3.zero; ch.CutDance();
            foreach (var bh in GetComponents<Behaviour>()) if (bh && bh.enabled && (bh is FBRoleNpc || bh is FBBot)) { bh.enabled = false; paused.Add(bh); }
        }
        void Stop(bool resume)
        {
            if (resume) foreach (var bh in paused) if (bh) bh.enabled = true; paused.Clear();
            if (ch) ch.HoldAnimate(this, false);
            if (on && rising && cc && ch && ch.alive) cc.enabled = true;                         // interrompido levantando (r46g: o CC só volta no fim)
            if (on && kind == "atropelo" && ch) ch.fleeing = false; on = false; rising = false;
        }

        public static float RestSum, LastUp; public static int RestN, RestTimeouts; public static bool LastTimeout;   // r46p: tempo no chão até levantar (assentou ou limite de 5 s); Last* = a queda mais recente
        // [parceiro] v1 (thread de física): tempo do Update medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
        static readonly int __fbt_Update = FBTempo.Id("FBFall.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            if (!on) return;
            if (!ch || !ch.alive) { Stop(true); return; }                                      // morreu: o FBDeath/ragdoll de morte segue com o corpo
            if (!rising)
            {
                ch.sys = null; simT += Time.deltaTime;
                if (!rd || !rd.Simulating) { if (simT > .2f) GetUp(); return; }
                if ((simT > minT && rd.RestTime > .4f) || simT > 5f) { RestSum += simT; RestN++; LastUp = simT; LastTimeout = simT > 5f; if (LastTimeout) RestTimeouts++; GetUp(); }
                return;
            }
            ch.sys = clip;                                                                       // depois do FBPlayer/IA: o clipe de levantar manda
        }
        // ---- fase 2: assentou -> pose do clipe de levantar no lugar do corpo
        static readonly HumanBodyBones[] SUP = { HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftToes, HumanBodyBones.RightToes, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg };
        static readonly float[] SUPM = { .03f, .03f, .015f, .015f, .05f, .05f, .05f, .05f };   // altura do centro do osso sobre o chão quando apoiado (m)
        public static int Lifts; public static float LiftMax, OffDrift;   // OffDrift: alguém mexeu na altura da raiz entre dois quadros do levantar (diagnóstico)
        static readonly Dictionary<string, float> HEAD0 = new() { { "IE_Downed_GetUp", 180 }, { "FF_SYS_KO_GetUp", 4 } };   // para onde a cabeça aponta no 1º quadro (graus, referencial da raiz; -fbposture)
        void GetUp()
        {
            var an = ch.an; var hips = ch.Hips; Transform neck = an ? (an.GetBoneTransform(HumanBodyBones.Neck) ?? an.GetBoneTransform(HumanBodyBones.Head)) : null;
            Transform lT = an ? an.GetBoneTransform(HumanBodyBones.LeftUpperLeg) : null, rT = an ? an.GetBoneTransform(HumanBodyBones.RightUpperLeg) : null;
            bool faceUp = true; float headYaw = ch.Yaw; Vector3 hp = hips ? hips.position : ch.Pos;
            if (hips && neck && lT && rT)
            {
                var up = (neck.position - hips.position).normalized; var right = (rT.position - lT.position).normalized; var fwd = Vector3.Cross(right, up).normalized;
                faceUp = fwd.y > 0; var hd = (neck.position - hips.position).Flat(); if (hd.sqrMagnitude < 1e-4f) hd = faceUp ? -fwd.Flat() : fwd.Flat();
                if (hd.sqrMagnitude > 1e-6f) headYaw = Mathf.Atan2(hd.x, hd.z) * Mathf.Rad2Deg;
            }
            string pick = null; foreach (var x in faceUp ? new[] { "IE_Downed_GetUp", "FF_SYS_KO_GetUp" } : new[] { "FF_SYS_KO_GetUp", "IE_Downed_GetUp" }) if (FBRegistry.I && FBRegistry.I.Clip(x)) { pick = x; break; }
            pick ??= FBRegistry.I && FBRegistry.I.Clip(FBCombat.GetUpClip) ? FBCombat.GetUpClip : null;
            float yaw = headYaw - (pick != null && HEAD0.TryGetValue(pick, out var h0) ? h0 : 0);
            // chão debaixo do quadril (só mapa/padrão: a moto caída e o próprio ragdoll ficam de fora)
            float gy = ch.Pos.y; if (Physics.Raycast(hp + Vector3.up * .6f, Vector3.down, out var hit, 2.5f, (1 << FBWorld.LMap) | 1, QueryTriggerInteraction.Ignore)) gy = hit.point.y;
            FBPoseBlend.Capture(ch, .5f);                                                        // pose do ragdoll (quadril no mundo)
            if (rd) rd.Clear();                                                                  // Animator de volta, ossos sem física
            gy0 = gy; lift = liftSet = 0; transform.SetPositionAndRotation(new Vector3(hp.x, gy, hp.z), Quaternion.Euler(0, yaw, 0));
            Physics.SyncTransforms();
            if (ch.knocked || pick == null) { if (cc) cc.enabled = true; Stop(true); Finished++; return; }   // caído (FF): o próprio estado de caído assume
            clip = pick; len = FBRegistry.I.Clip(pick).length; t = 0; rising = true; ch.sys = clip; ch.ForceFull(clip);
            ch.HoldAnimate(this, true);                                                          // r46i: apoio no chão soma sobre a pose (ver FBCharacter.HoldAnimate)
            if (faceUp) GotUpBack++; else GotUpFront++;
            Diag = $"{Diag} · assentou {simT:0.0} s · {(faceUp ? "de costas" : "de bruços")} · {clip}";
            // r46g: o CharacterController só volta no fim do levantar — ligado, o Move() de quem anda com ele devolvia a raiz à posição interna
            // dele a cada quadro (sonda r46f: o apoio pedia sempre mais e chegou a 3 m)
        }
        // r49: depois do FBPoseBlend (ordem 1200; este passo é o FBFallSupport, 1250). O apoio da raiz (LateUpdate, 1100) mede os ossos do
        // clipe puro; a mistura de 0,5 s a partir da pose do ragdoll, aplicada depois, ainda afundava mão/ponta do pé no começo do levantar
        // (laboratório r49d: pop LeftToeBase 0,43 abaixo, GS mão 0,16–0,19 no FF_SYS_KO_GetUp). Aqui, na pose que vai para a tela, o quadril
        // sobe só o que ainda falta NESTE quadro (sem acumular: a mistura refaz o quadril a cada quadro; mexer na raiz aqui subiria sem parar).
        // O fundo vem da MISTURA: quadril no mundo indo da pose do ragdoll (de costas, rente ao chão) ao clipe (de bruços, flexão) e os giros
        // locais em slerp — no meio do caminho a bacia fica de lado com a perna apontando para baixo, ainda com o quadril baixo.
        public static int PostFixes; public static float PostMax;
        public void SupportPass()
        {
            if (!on || !rising || !ch || !ch.alive) return; var an = ch.an; var hips = ch.Hips; if (!an || !hips) return;
            float sk = Mathf.Max(.5f, ch.ScaleK), deficit = 0;
            for (int i = 0; i < SUP.Length; i++) { var b = an.GetBoneTransform(SUP[i]); if (b) deficit = Mathf.Max(deficit, gy0 + SUPM[i] * sk - b.position.y); }
            deficit = Mathf.Min(deficit, .6f * sk); if (deficit <= .003f) return;   // r49f: teto .35 era atingido (laboratório r49e: máx 0,35, ponta do pé ainda 0,28 abaixo — no meio da mistura de costas → de bruços a perna aponta para baixo)
            hips.position += Vector3.up * deficit; PostFixes++; PostMax = Mathf.Max(PostMax, deficit);
        }
        // ---- fase 3: levantando
        // [parceiro] v1 (thread de física): tempo do LateUpdate medido pelo FBTempo
        static readonly int __fbt_LateUpdate = FBTempo.Id("FBFall.LateUpdate");
        void LateUpdate()
        {
            if (!FBTempo.On) { LateUpdate__fbt(); return; }
            long t0 = FBTempo.T0(); LateUpdate__fbt(); FBTempo.Soma(__fbt_LateUpdate, t0);
        }
        void LateUpdate__fbt()
        {
            if (!on || !rising) return; if (!ch || !ch.alive) { Stop(true); return; }
            t += Time.deltaTime;
            // r46f: APOIO NO CHÃO — os clipes de levantar foram feitos para outro corpo: com a raiz no chão, o FF_SYS_KO_GetUp (de bruços,
            // flexão) põe as mãos 22–30 cm e as pontas dos pés até 34 cm dentro do chão (sonda r46e; braço do rig mais curto que o do FF).
            // A raiz sobe o que faltar para o apoio mais baixo (mão, ponta do pé, tornozelo, joelho) ficar no chão; desce de volta devagar
            // r46g: altura ABSOLUTA (raiz = chão + apoio), osso medido em relação à altura em que a raiz está agora, teto de 45 cm
            { var an = ch.an; float need = 0, sk = Mathf.Max(.5f, ch.ScaleK), off = transform.position.y - gy0;
              if (an) for (int i = 0; i < SUP.Length; i++) { var b = an.GetBoneTransform(SUP[i]); if (b) need = Mathf.Max(need, gy0 + SUPM[i] * sk - (b.position.y - off)); }
              need = Mathf.Min(need, .45f * sk);
              // r49: no 1º quadro do levantar a raiz vai direto à altura do apoio (a mistura de 0,5 s a partir da pose do ragdoll cobre a troca) e
              // depois sobe a 6 m/s — subindo a 3 m/s desde zero, a ponta do pé ficava até 20 cm dentro do chão nos primeiros quadros
              // (laboratório r49a, quedas da GS 1250: LeftToeBase 0,20 abaixo no FF_SYS_KO_GetUp)
              bool first = t <= Time.deltaTime + 1e-4f;
              lift = first ? need : need > lift ? Mathf.MoveTowards(lift, need, Time.deltaTime * 6f) : Mathf.MoveTowards(lift, need, Time.deltaTime * .6f);
              if (Mathf.Abs(off - lift) > 1e-4f) { var p = transform.position; p.y = gy0 + lift; transform.position = p; }
              if (lift > .002f) { Lifts++; LiftMax = Mathf.Max(LiftMax, lift); }
              if (Mathf.Abs(off - liftSet) > .05f) OffDrift = Mathf.Max(OffDrift, Mathf.Abs(off - liftSet)); liftSet = lift; }
            if (t < len - .02f) return;
            // fim: mesma troca da saída do veículo — guarda a pose, raiz para baixo do quadril, corta o clipe
            FBPoseBlend.Capture(ch, .3f); var hips = ch.Hips;
            // r49g: raiz na altura em que o CharacterController descansa (chão + skinWidth = 0,04 × 2,2 = 0,088 m), não rente ao chão: o FBFootIK
            // desconta essa folga da pelve e sobe no máximo 0,06 m — em pé depois da queda a ponta do pé ficava 0,08–0,10 abaixo do chão (laboratório r49f)
            if (hips) { var hp = hips.position; transform.position = new Vector3(hp.x, gy0 + (cc ? cc.skinWidth * transform.lossyScale.y : 0), hp.z); lift = 0; Physics.SyncTransforms(); }
            if (cc) cc.enabled = true;   // r46f: de volta ao chão (a mistura cobre o resto do apoio)
            if (ch.sys == clip) ch.sys = null; ch.CutDance(); ch.getUpT = 0;
            if (kind == "atropelo") FBAtropelo.GotUp++;
            Stop(true); Finished++;
        }
    }
    // r49: segundo passo do levantar depois da mistura de pose (FBPoseBlend = 1200)
    [DefaultExecutionOrder(1250)]
    public class FBFallSupport : MonoBehaviour { public FBFall f; void LateUpdate() { if (f) f.SupportPass(); } }
}
