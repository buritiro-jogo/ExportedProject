// r46: SONDA DE POSE dos laboratórios. Pedido de 29/10: "esqueletos deformados, ossos deslocados, articulações quebradas" ao entrar/sair
// do carro, subir/descer da moto e nas quedas. Até a r45 os testes só mediam quadril × banco e onde o corpo terminava, nunca a FORMA do
// corpo. A sonda mede, quadro a quadro, durante uma transição:
//  · osso esticado: comprimento de cada segmento × repouso (humanoide sem translação não muda; se mudar, alguém escreveu posição de osso
//    por cima do Animator);
//  · joelho/cotovelo dobrando ao contrário ou de lado: eixo real da dobra × eixo anatômico (tirado da pose T do avatar = músculos em 0,
//    preso na coxa/braço);
//  · músculo do humanoide fora do limite do avatar (|valor| > 1,15; dedos de fora);
//  · mão dentro do tronco;
//  · membro atravessando o veículo: MeshCollider temporário (camada 30, ligado só durante a consulta) nas malhas do veículo + Linecast
//    osso a osso nos dois sentidos;
//  · parte do corpo abaixo do chão e corpo no ar (quedas);
//  · tranco: giro de osso por quadro (no espaço do quadril) e salto do quadril (no espaço do veículo; na queda, no da raiz).
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HB = UnityEngine.HumanBodyBones;

namespace FreeBala
{
    public class FBPoseProbe
    {
        public const int Layer = 30;
        public class Cal { public float[] len; public Vector3 kL, kR, eL, eR; public bool ok; public string diag; }
        static readonly Dictionary<FBCharacter, Cal> cals = new();
        static readonly (HB a, HB b, string n)[] Seg = {
            (HB.LeftUpperArm, HB.LeftLowerArm, "braço E"), (HB.LeftLowerArm, HB.LeftHand, "antebraço E"), (HB.RightUpperArm, HB.RightLowerArm, "braço D"), (HB.RightLowerArm, HB.RightHand, "antebraço D"),
            (HB.LeftUpperLeg, HB.LeftLowerLeg, "coxa E"), (HB.LeftLowerLeg, HB.LeftFoot, "canela E"), (HB.RightUpperLeg, HB.RightLowerLeg, "coxa D"), (HB.RightLowerLeg, HB.RightFoot, "canela D"),
            (HB.LeftFoot, HB.LeftToes, "pé E"), (HB.RightFoot, HB.RightToes, "pé D"), (HB.Hips, HB.Spine, "lombar"), (HB.Spine, HB.Chest, "tronco"), (HB.Chest, HB.Neck, "peito"), (HB.Neck, HB.Head, "pescoço"),
            (HB.LeftShoulder, HB.LeftUpperArm, "ombro E"), (HB.RightShoulder, HB.RightUpperArm, "ombro D"), (HB.Hips, HB.LeftUpperLeg, "bacia E"), (HB.Hips, HB.RightUpperLeg, "bacia D") };
        static readonly HB[] Rot = { HB.Spine, HB.Chest, HB.Neck, HB.Head, HB.LeftUpperArm, HB.LeftLowerArm, HB.LeftHand, HB.RightUpperArm, HB.RightLowerArm, HB.RightHand,
            HB.LeftUpperLeg, HB.LeftLowerLeg, HB.LeftFoot, HB.RightUpperLeg, HB.RightLowerLeg, HB.RightFoot };
        static readonly HB[] Low = { HB.LeftFoot, HB.RightFoot, HB.LeftToes, HB.RightToes, HB.LeftHand, HB.RightHand, HB.Head, HB.Hips, HB.LeftLowerLeg, HB.RightLowerLeg };
        static bool[] fingerMus;

        // ---- calibração: comprimentos de repouso + eixos de joelho/cotovelo na pose T do avatar (Animator parado por um quadro)
        public static Cal CalOf(FBCharacter a) => a && cals.TryGetValue(a, out var c) ? c : null;
        public static IEnumerator Calibrate(FBCharacter a)
        {
            var an = a ? a.an : null; if (!an || !an.isHuman || !an.avatar) yield break;
            var c = new Cal { len = new float[Seg.Length] };
            for (int i = 0; i < Seg.Length; i++) { var x = an.GetBoneTransform(Seg[i].a); var y = an.GetBoneTransform(Seg[i].b); c.len[i] = x && y ? (y.position - x.position).magnitude : 0; }
            var hp = new HumanPoseHandler(an.avatar, an.transform); var pose = new HumanPose(); hp.GetHumanPose(ref pose);
            // r46o: a pose volta pelos PRÓPRIOS ossos — SetHumanPose(pose guardada) com o NPC fora da câmera (Animator em Cull Update
            // Transforms) punha o quadril a (86; 300; −25) da raiz (a altura da pista), e o Animator parado não reescrevia: o começo de subir
            // da moto misturava a partir do céu (sonda r46n/r46o2: "quadril 3.400–20.000 u/s" em todas as subidas de NPC; só no laboratório)
            var keep = an.GetComponentsInChildren<Transform>(true); var keepP = new Vector3[keep.Length]; var keepQ = new Quaternion[keep.Length];
            for (int i = 0; i < keep.Length; i++) { keepP[i] = keep[i].localPosition; keepQ[i] = keep[i].localRotation; }
            bool was = an.enabled; an.enabled = false;
            var z = pose; z.muscles = new float[pose.muscles.Length]; hp.SetHumanPose(ref z);
            Transform G(HB b) => an.GetBoneTransform(b);
            Transform lU = G(HB.LeftUpperArm), rU = G(HB.RightUpperArm), lL = G(HB.LeftLowerArm), rL = G(HB.RightLowerArm), lH = G(HB.LeftHand), rH = G(HB.RightHand);
            Transform lT = G(HB.LeftUpperLeg), rT = G(HB.RightUpperLeg), lK = G(HB.LeftLowerLeg), rK = G(HB.RightLowerLeg), lF = G(HB.LeftFoot), rF = G(HB.RightFoot);
            Transform hips = G(HB.Hips), neck = G(HB.Neck) ? G(HB.Neck) : G(HB.Head);
            if (lU && rU && lL && rL && lH && rH && lT && rT && lK && rK && lF && rF && hips && neck)
            {
                var up = (neck.position - hips.position).normalized; var right = Vector3.ProjectOnPlane(rU.position - lU.position, up).normalized; var fwd = Vector3.Cross(right, up);
                // pose T: perna reta para baixo e o joelho dobra para trás -> eixo = direita do corpo; braço aberto e o cotovelo dobra para a frente
                //  -> eixo = cima (esquerdo) / baixo (direito). Se a pose T do avatar já vem dobrada (> 12°), vale a dobra real dela.
                Vector3 Ax(Transform u, Transform l, Transform e, Vector3 anat) { var v1 = l.position - u.position; var v2 = e.position - l.position; var cr = Vector3.Cross(v1, v2); return Vector3.Angle(v1, v2) > 12 && Vector3.Dot(cr, anat) > 0 ? cr.normalized : anat; }
                c.kL = Quaternion.Inverse(lT.rotation) * Ax(lT, lK, lF, right); c.kR = Quaternion.Inverse(rT.rotation) * Ax(rT, rK, rF, right);
                c.eL = Quaternion.Inverse(lU.rotation) * Ax(lU, lL, lH, up); c.eR = Quaternion.Inverse(rU.rotation) * Ax(rU, rL, rH, -up); c.ok = true;
                float armH = Vector3.Angle(lL.position - lU.position, -right), legV = Vector3.Angle(lK.position - lT.position, -up);
                c.diag = $"pose T: braço E {armH:0}° da horizontal · coxa E {legV:0}° da vertical · joelho {Vector3.Angle(lK.position - lT.position, lF.position - lK.position):0}° · cotovelo {Vector3.Angle(lL.position - lU.position, lH.position - lL.position):0}° · frente×raiz {Vector3.Angle(fwd.Flat(), a.transform.forward.Flat()):0}°";
            }
            else c.diag = "pose T: faltam ossos do humanoide";
            for (int i = 0; i < keep.Length; i++) if (keep[i]) keep[i].SetLocalPositionAndRotation(keepP[i], keepQ[i]); an.enabled = was; hp.Dispose();
            cals[a] = c; yield return null; yield return null;
        }

        // ---- colisores temporários nas malhas do veículo (LOD 0); ficam desligados fora da consulta da sonda
        public static List<MeshCollider> AddVehicleColliders(FBCar car, out int skipped)
        {
            skipped = 0; var list = new List<MeshCollider>(); if (!car) return list; var root = car.mx ? car.mx.gameObject : car.gameObject;
            var lodSkip = new HashSet<Renderer>(); foreach (var lg in root.GetComponentsInChildren<LODGroup>(true)) { var L = lg.GetLODs(); for (int i = 1; i < L.Length; i++) foreach (var r in L[i].renderers) if (r) lodSkip.Add(r); }
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!r || !r.enabled || !r.gameObject.activeInHierarchy || lodSkip.Contains(r) || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer || r.name.StartsWith("sombra")) continue;   // r46: sombra de contato = quadrado no chão, não é peça
                Mesh m = null;
                if (r is MeshRenderer) { var mf = r.GetComponent<MeshFilter>(); m = mf ? mf.sharedMesh : null; if (m && !m.isReadable) { skipped++; continue; } }
                else if (r is SkinnedMeshRenderer s) { m = new Mesh(); s.BakeMesh(m, true); }
                if (!m || m.vertexCount < 3) continue;
                // r48: SOLTO na cena e levado até a peça a cada consulta (Follow) — filho da peça, na MX ativa (corpo rígido dinâmico) a malha
                // côncava não entrava na consulta: o jogador dava "atravessou 0" com os ossos no mesmo lugar (±3 cm) dos NPCs que davam 48–60
                var g = new GameObject("sonda:" + r.name) { layer = Layer }; g.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation); g.transform.localScale = r.transform.lossyScale;
                var mc = g.AddComponent<MeshCollider>(); mc.sharedMesh = m; mc.enabled = false; list.Add(mc); follow[mc] = r.transform;
            }
            return list;
        }
        public static void RemoveColliders(List<MeshCollider> l) { if (l != null) foreach (var c in l) { if (c) Object.Destroy(c.gameObject); follow.Remove(c); } }
        static readonly Dictionary<MeshCollider, Transform> follow = new();
        // r49: liga/desliga um conjunto para consultas de fora da sonda (laboratório: teto × cabeça sentado)
        public static void Arm(List<MeshCollider> l, bool on) { if (l == null) return; foreach (var mc in l) if (mc) { if (on) Follow(mc); mc.enabled = on; } if (on) Physics.SyncTransforms(); }
        static void Follow(MeshCollider mc) { if (mc && follow.TryGetValue(mc, out var src) && src) { mc.transform.SetPositionAndRotation(src.position, src.rotation); mc.transform.localScale = src.lossyScale; } }

        // ---- uma medição (uma transição)
        readonly FBCharacter a; readonly Animator an; readonly Cal c; readonly FBCar car; readonly bool fall; readonly List<MeshCollider> cols;
        readonly Transform[] sa, sb, rt, low; readonly Quaternion[] lastRot; readonly Transform hips, neck, lHand, rHand;
        readonly HumanPoseHandler hp; HumanPose pose = new HumanPose(); readonly float[] musMax; readonly string[] musWhere; readonly float torsoR;
        Vector3 lastHip, lastRoot, lastCarP; bool first = true; public float t; public int frames; bool flag; public string flagWhy;
        public float stretch; string stretchW = "-"; public int kneeInv, elbowInv, kneeSide, elbowSide; float worstInv = 1, worstSide = 1; string invW = "-", sideW = "-";
        public int handIn; string handW = "-"; public int pierceFrames; readonly Dictionary<string, int> pierce = new(); string pierceFirst = "-"; readonly Dictionary<string, float> pierceDeep = new(); public int pierceDeepFrames;
        public float below; string belowW = "-"; public float air, airRun; public float spin; string spinW = "-"; public float hipJump; string hipW = "-"; public float ragJump; string ragW = "-"; Vector3 lastHipW;
        public int musFrames;

        public FBPoseProbe(FBCharacter a, FBCar car, bool fall, List<MeshCollider> cols)
        {
            this.a = a; an = a ? a.an : null; c = CalOf(a); this.car = car; this.fall = fall; this.cols = cols;
            sa = new Transform[Seg.Length]; sb = new Transform[Seg.Length]; rt = new Transform[Rot.Length]; low = new Transform[Low.Length]; lastRot = new Quaternion[Rot.Length];
            if (an) { for (int i = 0; i < Seg.Length; i++) { sa[i] = an.GetBoneTransform(Seg[i].a); sb[i] = an.GetBoneTransform(Seg[i].b); } for (int i = 0; i < Rot.Length; i++) rt[i] = an.GetBoneTransform(Rot[i]); for (int i = 0; i < Low.Length; i++) low[i] = an.GetBoneTransform(Low[i]);
                hips = an.GetBoneTransform(HB.Hips); neck = an.GetBoneTransform(HB.Neck) ? an.GetBoneTransform(HB.Neck) : an.GetBoneTransform(HB.Head); lHand = an.GetBoneTransform(HB.LeftHand); rHand = an.GetBoneTransform(HB.RightHand); }
            torsoR = hips && neck ? .18f * (neck.position - hips.position).magnitude : .09f;
            if (an && an.avatar && an.isHuman) { hp = new HumanPoseHandler(an.avatar, an.transform); hp.GetHumanPose(ref pose); }
            int nm = HumanTrait.MuscleCount; musMax = new float[nm]; musWhere = new string[nm];
            if (fingerMus == null) { fingerMus = new bool[nm]; for (int i = 0; i < nm; i++) { var n = HumanTrait.MuscleName[i]; fingerMus[i] = n.Contains("Thumb") || n.Contains("Index") || n.Contains("Middle") || n.Contains("Ring") || n.Contains("Little") || n.Contains("Eye") || n.Contains("Jaw"); } }
        }
        public void Dispose() { hp?.Dispose(); }
        public bool TakeFlag(out string why) { why = flagWhy; bool f = flag; flag = false; return f; }
        void Flag(string why) { if (!flag) { flag = true; flagWhy = why; } }

        // r46c: contexto do quadro (para achar a CAUSA de giro/salto): fase do FBDriverPose, peso da camada de corpo inteiro, clipe, mistura
        // de pose ativa, queda, e quanto a raiz girou (no referencial do veículo) neste quadro
        string lastPh = "-", ragStart; public string PreFall;
        void TwistStr(System.Text.StringBuilder sb) { for (int m = 0; m < pose.muscles.Length; m++) { var mn = HumanTrait.MuscleName[m]; if (mn.Contains("Twist") && (mn.Contains("Arm") || mn.Contains("Leg") || mn.Contains("Forearm"))) sb.Append($" {mn.Replace(" Twist In-Out", "")} {pose.muscles[m]:+0.00;-0.00}"); else if (mn.StartsWith("Left Arm") || mn.StartsWith("Right Arm")) sb.Append($" {mn} {pose.muscles[m]:+0.00;-0.00}"); } }
        /// <summary>r46j: torções da pose AGORA (antes de começar a queda) — separa o que vem da pose de pilotar do que o ragdoll faz no 1º passo.</summary>
        public void MarkPreFall() { if (hp == null) return; hp.GetHumanPose(ref pose); var sb = new System.Text.StringBuilder(); TwistStr(sb); PreFall = sb.ToString(); } static string V(Vector3 v) => $"({v.x:0.00};{v.y:0.00};{v.z:0.00})";
        float lastYaw; string Ctx(float yawStep) { var dp = a.GetComponent<FBDriverPose>(); return $"[dp {dp?.trans ?? (dp ? "nulo" : "sem")} danW {a.DanW:0.00} sys {a.sys ?? "-"} dança {a.dance?.clip ?? "-"} mistura {(FBPoseBlend.Active(a) ? "sim" : "não")} queda {FBFall.ClipOf(a) ?? "-"} raiz girou {yawStep:0.0}°{Bodies()}]"; }
        string Bodies() { int n = 0, dyn = 0; foreach (var rb in a.GetComponentsInChildren<Rigidbody>()) { n++; if (!rb.isKinematic) dyn++; } var hl = a.transform.InverseTransformPoint(hips.position); return $" rb {n}/{dyn} din animator {(an.enabled ? "lig" : "desl")} quadril local ({hl.x:0.00};{hl.y:0.00};{hl.z:0.00})"; }
        public void Sample(float dt, string phase)
        {
            if (!a || !an || !hips) return; t += dt; frames++;
            float yawNow = car ? Mathf.DeltaAngle(car.transform.eulerAngles.y, a.transform.eulerAngles.y) : a.transform.eulerAngles.y, yawStep = first ? 0 : Mathf.Abs(Mathf.DeltaAngle(lastYaw, yawNow)); lastYaw = yawNow;
            // 1. ossos esticados
            if (c != null) for (int i = 0; i < Seg.Length; i++)
            {
                if (!sa[i] || !sb[i] || c.len[i] < 1e-4f) continue; float r = (sb[i].position - sa[i].position).magnitude / c.len[i]; float e = Mathf.Abs(r - 1);
                if (e > stretch) { stretch = e; stretchW = $"{Seg[i].n} {r * 100:0}% t={t:0.00} {phase}"; if (e > .05f) Flag("osso esticado " + Seg[i].n); }
            }
            // 2. joelhos e cotovelos
            if (c != null && c.ok)
            {
                Hinge(HB.LeftUpperLeg, HB.LeftLowerLeg, HB.LeftFoot, c.kL, "joelho E", phase, ref kneeInv, ref kneeSide);
                Hinge(HB.RightUpperLeg, HB.RightLowerLeg, HB.RightFoot, c.kR, "joelho D", phase, ref kneeInv, ref kneeSide);
                Hinge(HB.LeftUpperArm, HB.LeftLowerArm, HB.LeftHand, c.eL, "cotovelo E", phase, ref elbowInv, ref elbowSide);
                Hinge(HB.RightUpperArm, HB.RightLowerArm, HB.RightHand, c.eR, "cotovelo D", phase, ref elbowInv, ref elbowSide);
            }
            // 3. músculos fora do limite do avatar
            if (hp != null)
            {
                hp.GetHumanPose(ref pose); bool any = false;
                for (int m = 0; m < pose.muscles.Length && m < musMax.Length; m++) { if (fingerMus[m]) continue; float v = Mathf.Abs(pose.muscles[m]); if (v > musMax[m]) { musMax[m] = v; musWhere[m] = $"t={t:0.00} {phase}"; } if (v > 1.15f) any = true; }
                if (any) musFrames++;
                // r46g: pose no 1º quadro do ragdoll (torção dos membros e sentido das dobras) — ele nasce da pose de pilotar e guarda o que vier nela
                if (fall && ragStart == null && FBFall.Ragdolling(a))
                {
                    var sb = new System.Text.StringBuilder((PreFall != null ? "pose antes da queda:" + PreFall + " · " : "") + "1º quadro do ragdoll:"); TwistStr(sb);
                    if (c != null && c.ok) { sb.Append(" · dobras"); foreach (var (ub, lb, eb, ax, nm) in new[] { (HB.LeftUpperLeg, HB.LeftLowerLeg, HB.LeftFoot, c.kL, "jE"), (HB.RightUpperLeg, HB.RightLowerLeg, HB.RightFoot, c.kR, "jD"), (HB.LeftUpperArm, HB.LeftLowerArm, HB.LeftHand, c.eL, "cE"), (HB.RightUpperArm, HB.RightLowerArm, HB.RightHand, c.eR, "cD") })
                        { Transform u = an.GetBoneTransform(ub), l = an.GetBoneTransform(lb), e = an.GetBoneTransform(eb); if (!u || !l || !e) continue; var v1 = l.position - u.position; var v2 = e.position - l.position; float fx = Vector3.Angle(v1, v2); sb.Append($" {nm} {fx:0}° eixo {Vector3.Dot(Vector3.Cross(v1, v2).normalized, (u.rotation * ax).normalized):+0.00;-0.00}"); } }
                    ragStart = sb.ToString();
                }
            }
            // 4. mão dentro do tronco
            foreach (var h in new[] { lHand, rHand })
            {
                if (!h || !neck) continue; float d = DistSeg(h.position, hips.position, neck.position);
                if (d < torsoR) { handIn++; if (handW == "-") { handW = $"{(h == lHand ? "mão E" : "mão D")} a {d:0.00} do eixo do tronco t={t:0.00} {phase}"; Flag("mão no tronco"); } }
            }
            // 5. membro atravessando a malha do veículo
            if (cols != null && cols.Count > 0)
            {
                foreach (var mc in cols) if (mc) { Follow(mc); mc.enabled = true; } Physics.SyncTransforms(); bool hit = false; int mask = 1 << Layer; float deepF = 0;
                for (int i = 0; i < Seg.Length; i++)
                {
                    if (!sa[i] || !sb[i]) continue; Vector3 p = sa[i].position, q = sb[i].position;
                    bool a1 = Physics.Linecast(p, q, out var h1, mask, QueryTriggerInteraction.Collide), a2 = Physics.Linecast(q, p, out var h2, mask, QueryTriggerInteraction.Collide);
                    if (a1 || a2)
                    {
                        if (!a1) h1 = h2; hit = true; var k = Seg[i].n + "×" + h1.collider.name.Replace("sonda:", "") + "@" + (phase ?? "-").Split('/')[0];   /* r46: por fase (subir/sentado/descer) */ pierce[k] = pierce.TryGetValue(k, out var n0) ? n0 + 1 : 1;
                        if (pierceFirst == "-") { pierceFirst = $"{k} t={t:0.00} {phase}"; Flag("atravessou " + k); }
                        // r48: FUNDO = trecho do osso DENTRO da malha (raspão de 1 mm contava igual a joelho dentro do tanque): entrou e saiu = corda
                        // entre os dois pontos; só um lado acha = a outra ponta está dentro (corda até ela)
                        float dpt = a1 && a2 ? Vector3.Distance(h1.point, h2.point) : a1 ? Vector3.Distance(h1.point, q) : Vector3.Distance(h2.point, p);
                        if (!pierceDeep.TryGetValue(k, out var d0) || dpt > d0) pierceDeep[k] = dpt; if (dpt > deepF) deepF = dpt;
                    }
                }
                foreach (var mc in cols) if (mc) mc.enabled = false; if (hit) pierceFrames++; if (deepF > .05f * (a ? a.ScaleK : 1)) pierceDeepFrames++;
            }
            // 6. abaixo do chão / no ar
            {
                float lowest = float.MaxValue; string lowN = null;
                foreach (var b in low)
                {
                    if (!b) continue; if (!Physics.Raycast(b.position + Vector3.up * 1.5f, Vector3.down, out var gh, 4f, 1 << FBWorld.LMap, QueryTriggerInteraction.Ignore)) continue;
                    float hgt = b.position.y - gh.point.y; if (hgt < lowest) { lowest = hgt; lowN = b.name; }
                    if (-hgt > below) { below = -hgt; belowW = $"{b.name} {-hgt:0.00} abaixo t={t:0.00} {phase}"; if (-hgt > .05f) Flag("abaixo do chão " + b.name); }
                }
                if (fall && t > .4f && lowest != float.MaxValue) { if (lowest > .25f) { airRun += dt; if (airRun > air) air = airRun; } else airRun = 0; }
            }
            // 7. trancos: giro de osso (espaço do quadril) e salto do quadril (espaço do veículo; queda = espaço da raiz)
            {
                var hq = Quaternion.Inverse(hips.rotation);
                for (int i = 0; i < rt.Length; i++)
                {
                    if (!rt[i]) continue; var q = hq * rt[i].rotation;
                    if (!first && dt > 1e-4f) { float w = Quaternion.Angle(lastRot[i], q) / dt; if (w > spin) { spin = w; spinW = $"{Rot[i]} {w:0}°/s t={t:0.00} {phase} {Ctx(yawStep)}"; if (w > 1440) Flag("tranco " + Rot[i]); } }
                    lastRot[i] = q;
                }
                // r46: queda = ragdoll vivo — a raiz fica parada e o quadril anda pela física; ali o "salto" é teleporte (> 0,4 m num quadro),
                // e fora dele (levantar, com as duas trocas de raiz) o salto é medido no mundo, que é o que se vê
                bool rag = fall && FBFall.Ragdolling(a);
                if (!first && rag && dt > 1e-4f) { float dw = (hips.position - lastHipW).magnitude / dt; if (dw > ragJump) { ragJump = dw; ragW = $"{dw:0.0} u/s t={t:0.00} ({dw * dt:0.00} m em {dt * 1000:0} ms)"; if (dw > 25) Flag("teleporte no ragdoll"); } }   /* r46d: velocidade — a 35 km/h o corpo anda 0,8 m num quadro de 70 ms de verdade */
                var hl = fall ? hips.position : !car ? a.transform.InverseTransformPoint(hips.position) : car.transform.InverseTransformPoint(hips.position);
                var rp = a.transform.position; var cp = car ? car.transform.position : Vector3.zero;
                if (!first && !rag && dt > 1e-4f) { float v = (hl - lastHip).magnitude / dt; if (v > hipJump) { hipJump = v; hipW = $"{v:0.0} u/s t={t:0.00} {phase} (quadril {(hl - lastHip).magnitude:0.000}, raiz {(rp - lastRoot).magnitude:0.000}, veículo {(cp - lastCarP).magnitude:0.000} no quadro de {dt * 1000:0} ms; Δquadril na raiz {V(a.transform.InverseTransformVector(hl - lastHip))} Δraiz {V(rp - lastRoot)} mistura {FBPoseBlend.Progress(a):0.00} quadro anterior {lastPh}) {Ctx(yawStep)}"; } }   /* r46f: direção do salto + quadro anterior */
                lastHip = hl; lastRoot = rp; lastCarP = cp; lastHipW = hips.position; lastPh = $"{phase} queda {(fall ? FBFall.ClipOf(a) ?? "-" : "-")} mistura {FBPoseBlend.Progress(a):0.00}";
            }
            first = false;
        }
        void Hinge(HB ub, HB lb, HB eb, Vector3 axisLocal, string n, string phase, ref int inv, ref int side)
        {
            Transform u = an.GetBoneTransform(ub), l = an.GetBoneTransform(lb), e = an.GetBoneTransform(eb); if (!u || !l || !e) return;
            var v1 = l.position - u.position; var v2 = e.position - l.position; float flex = Vector3.Angle(v1, v2); if (flex < 20) return;
            var ax = Vector3.Cross(v1, v2).normalized; var exp = (u.rotation * axisLocal).normalized; float d = Vector3.Dot(ax, exp);
            if (d < -.3f) { inv++; if (d < worstInv) { worstInv = d; invW = $"{n} ao contrário (dobra {flex:0}°, eixo {d:+0.00;-0.00}) t={t:0.00} {phase}"; } Flag(n + " ao contrário"); }
            else if (d < .5f && flex > 35) { side++; if (d < worstSide) { worstSide = d; sideW = $"{n} de lado (dobra {flex:0}°, eixo {d:+0.00;-0.00}) t={t:0.00} {phase}"; } }
        }
        static float DistSeg(Vector3 p, Vector3 a, Vector3 b) { var ab = b - a; float k = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude)); return (p - (a + ab * k)).magnitude; }

        // ---- veredito: falhas graves (0 = ok) e a linha do relatório
        public List<string> Fails()
        {
            var f = new List<string>();
            if (stretch > .03f) f.Add("osso esticado"); if (kneeInv + elbowInv > 0) f.Add("articulação ao contrário"); if (kneeSide + elbowSide > 2) f.Add("articulação de lado");
            if (handIn > 2) f.Add("mão no tronco"); if (pierceFrames > 2) f.Add("atravessou o veículo"); if (below > .03f) f.Add("abaixo do chão");
            if (spin > 1440) f.Add("tranco de osso"); if (hipJump > (fall ? 6 : 4)) f.Add("salto do quadril"); if (fall && air > .5f) f.Add("no ar"); if (ragJump > 25) f.Add("teleporte no ragdoll");
            return f;
        }
        public string Report()
        {
            var mus = Enumerable.Range(0, musMax.Length).Where(i => !fingerMus[i] && musMax[i] > 1.15f).OrderByDescending(i => musMax[i]).Take(4).Select(i => $"{HumanTrait.MuscleName[i]} {musMax[i]:0.00} ({musWhere[i]})");
            var pc = pierce.OrderByDescending(kv => kv.Value).Take(5).Select(kv => $"{kv.Key} {kv.Value}");
            var pd = pierceDeep.OrderByDescending(kv => kv.Value).Take(4).Select(kv => $"{kv.Key} {kv.Value * 100:0}");
            return $"{frames} quadros {t:0.0} s · esticado máx {stretch * 100:0.0}% ({stretchW}) · ao contrário joelho {kneeInv}/cotovelo {elbowInv} ({invW}) · de lado joelho {kneeSide}/cotovelo {elbowSide} ({sideW})" +
                   $" · mão no tronco {handIn} ({handW}) · atravessou {pierceFrames} quadros [{string.Join(", ", pc)}] 1º {pierceFirst} · fundo >5 cm {pierceDeepFrames} quadros, máx cm [{string.Join(", ", pd)}] · abaixo do chão {below:0.00} ({belowW})" + (fall ? $" · no ar {air:0.00} s · ragdoll passo máx {ragW} · {ragStart ?? "-"} · {FBFall.ClipOf(a) ?? FBFall.Diag}" : "") +
                   $" · giro máx {spinW} · quadril {hipW} · músculos >1,15 em {musFrames} quadros [{string.Join("; ", mus)}]";
        }
    }
}
