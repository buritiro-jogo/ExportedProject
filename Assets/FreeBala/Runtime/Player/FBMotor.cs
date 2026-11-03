// Física de chão do CharacterController (jogador, bots, civis e a cena de teste de IK): o CC sozinho "flutua" degrau abaixo (perde o chão a cada degrau
// e cai com a gravidade), pisca isGrounded em ladeira descendo e deixa subir parede íngreme pulando. Aqui:
//  - Probe: SphereCast da esfera de baixo da cápsula para baixo (chão robusto). A normal vem da FACE sob o pé: raio na quina do contato e raio
//    no eixo da cápsula — na quina de um degrau a esfera toca a aresta e a normal sai inclinada (falso "rampa íngreme" = chão perdido = travado);
//  - Move: stepOffset só no chão (no ar não engancha em beirada; chão = estava no chão OU há chão firme a até 15 cm: a piscada de 'no ar' no
//    meio da escada não derruba o stepOffset), aderência (estava no chão, não pulou, não está subindo → desce até o chão até o tamanho de um
//    degrau), rampa acima do slopeLimit escorrega e não conta como chão para pular;
//  - StepUp: degrau de reserva. O stepOffset do CC levanta a cápsula INTEIRA até o limite (0,45 m) antes de andar: em vão de escada com o lance
//    de cima por cima, bate no teto e desiste mesmo para degrau de 18 cm; também falha com degrau na altura do limite e entrada de lado em
//    sprint. Aqui mede primeiro onde está o piso depois da quina e levanta SÓ o necessário (altura real do degrau), com caminho livre e piso
//    andável — nunca sobe parede mais alta que o stepOffset.
// Posição real = física; o FBFootIK só ajusta o visual (pelve/pernas) em cima disso.
using UnityEngine;

namespace FreeBala
{
    public static class FBMotor
    {
        public struct Ground { public bool ok, steep; public float dist, angle; public Vector3 point, normal; public Collider col; }
        static readonly RaycastHit[] hits = new RaycastHit[8];
        static readonly Collider[] overlaps = new Collider[8];
        /// <summary>Liga/desliga o degrau de reserva (testes).</summary>
        public static bool StepAssist = true;

        // chão sob a cápsula até 'extra' metros abaixo da pele
        public static Ground Probe(CharacterController cc, float extra)
        {
            var g = new Ground(); var t = cc.transform; float sc = t.lossyScale.y, r = cc.radius * Mathf.Max(t.lossyScale.x, t.lossyScale.z), hgt = Mathf.Max(cc.height * sc, 2 * r);
            var c = t.TransformPoint(cc.center); var bottom = c - Vector3.up * (hgt * .5f - r);
            float skin = cc.skinWidth * sc, rr = r * .9f, up = r * .1f + skin, far = up + skin + extra;
            int n = Physics.SphereCastNonAlloc(bottom + Vector3.up * up, rr, Vector3.down, hits, far, FBWorld.SolidMask, QueryTriggerInteraction.Ignore);
            float bd = 1e9f;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i]; if (h.distance <= 0 || h.distance >= bd || !h.collider || h.collider.transform.IsChildOf(t)) continue;
                bd = h.distance; g.ok = true; g.point = h.point; g.normal = h.normal; g.col = h.collider;
                g.dist = Mathf.Max(0, h.distance - up - (r - rr));        // quanto falta da cápsula até o chão
            }
            if (!g.ok) return g;
            // normal da face (o SphereCast numa quina devolve a normal arredondada): raio no ponto do contato...
            if (Physics.Raycast(g.point + Vector3.up * .1f, Vector3.down, out var fh, .2f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) && fh.collider == g.col && fh.normal.y > g.normal.y) g.normal = fh.normal;
            // ...e no eixo da cápsula: o piso sob o centro do pé (na quina o contato é a aresta; o eixo acha o degrau liso). Só vale se está no mesmo nível.
            if (Physics.Raycast(bottom + Vector3.up * (up + .05f), Vector3.down, out var ah, up + .05f + r + skin + extra, FBWorld.SolidMask, QueryTriggerInteraction.Ignore)
                && ah.collider && !ah.collider.transform.IsChildOf(t) && ah.normal.y > g.normal.y && Mathf.Abs(ah.point.y - g.point.y) < .1f * sc + skin) g.normal = ah.normal;
            g.angle = Vector3.Angle(g.normal, Vector3.up); g.steep = g.angle > cc.slopeLimit + 2;
            return g;
        }

        // degrau de reserva: tenta subir até 'maxStep' metros no sentido de 'flat' (deslocamento horizontal pedido). rise = quanto subir.
        static bool StepUp(CharacterController cc, Vector3 flat, float maxStep, out float rise)
        {
            rise = 0; var t = cc.transform; float sc = t.lossyScale.y, r = cc.radius * Mathf.Max(t.lossyScale.x, t.lossyScale.z), hgt = Mathf.Max(cc.height * sc, 2 * r), skin = cc.skinWidth * sc;
            if (maxStep <= skin * 2 || flat.sqrMagnitude < 1e-8f) return false;
            var c = t.TransformPoint(cc.center); float half = hgt * .5f - r; var dir = flat.normalized;
            float rr = r * .95f, hMax = maxStep + skin, fwd = Mathf.Max(flat.magnitude, r * .6f) + skin;
            var b0 = c - Vector3.up * half; var t0 = c + Vector3.up * half;               // centros das esferas de baixo e de cima
            // 1) onde está o piso depois da quina (cast de cima do degrau máximo para baixo)
            var from = b0 + Vector3.up * hMax + dir * fwd; Collider col = null; float bd = 1e9f; Vector3 nrm = Vector3.up, pt = Vector3.zero;
            int n = Physics.SphereCastNonAlloc(from, rr, Vector3.down, hits, hMax + skin * 2, FBWorld.SolidMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var hh = hits[i]; if (hh.distance <= 0 || hh.distance >= bd || !hh.collider || hh.collider.transform.IsChildOf(t)) continue;
                bd = hh.distance; col = hh.collider; nrm = hh.normal; pt = hh.point;
            }
            if (!col) return false;
            if (Physics.Raycast(pt + Vector3.up * .1f, Vector3.down, out var fh, .2f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) && fh.collider == col && fh.normal.y > nrm.y) nrm = fh.normal;
            if (Vector3.Angle(nrm, Vector3.up) > cc.slopeLimit + 2) return false;                 // íngreme demais: não sobe (escorrega)
            float need = hMax - bd + (r - rr);                                                     // quanto o pé do CC sobe até o piso encontrado
            if (need <= skin || need > maxStep + skin) return false;
            // 2) levanta só o necessário (não os 0,45 m inteiros): vão de escada baixo continua passando
            float lift = need + skin; var p0 = b0 + Vector3.up * lift; var p1 = t0 + Vector3.up * lift;
            n = Physics.OverlapCapsuleNonAlloc(p0, p1, rr, overlaps, FBWorld.SolidMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (overlaps[i] && !overlaps[i].transform.IsChildOf(t)) return false;   // teto/parede em cima: não é degrau
            // 3) caminho livre para a frente na altura do degrau (parede mais alta que o degrau)
            n = Physics.CapsuleCastNonAlloc(p0, p1, rr, dir, hits, fwd, FBWorld.SolidMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) { var hh = hits[i]; if (hh.collider && hh.distance > 0 && !hh.collider.transform.IsChildOf(t)) return false; }
            rise = need; return true;
        }

        // um passo de movimento: devolve as flags do CC; grounded = chão firme depois do passo; slide = velocidade de escorregar (estado).
        // stepLocal = stepOffset configurado no CC (FBMatch.Spawn); snapWorld = maior degrau que a aderência desce (metros)
        public static CollisionFlags Move(CharacterController cc, Vector3 motion, bool wasGrounded, bool ascending, float stepLocal, float snapWorld, ref float slide, out bool grounded, out Ground g)
        {
            if (!cc || !cc.enabled || !cc.gameObject.activeInHierarchy) { grounded = wasGrounded; g = default; return CollisionFlags.None; }   // no carro/caído o CC fica desligado ("Move called on inactive controller")
            var t = cc.transform; float sc = t.lossyScale.y, stepWorld = snapWorld;
            // "no chão" para o degrau: estava no chão OU há chão firme logo abaixo (a piscada de 'no ar' no meio da escada não pode derrubar o stepOffset)
            bool stepOk = wasGrounded;
            if (!stepOk && !ascending) { var pre = Probe(cc, .15f); stepOk = pre.ok && !pre.steep && pre.dist <= .15f; }
            cc.stepOffset = stepOk ? stepLocal : stepLocal * .18f;
            var p0 = t.position;
            var flags = cc.Move(motion);
            // degrau de reserva: o CC bateu de lado e quase não andou (subindo/pulando e no ar não vale)
            if (StepAssist && stepOk && !ascending && (flags & CollisionFlags.Sides) != 0)
            {
                var flat = new Vector3(motion.x, 0, motion.z); var moved = t.position - p0; moved.y = 0;
                if (flat.magnitude > 1e-4f && moved.magnitude < flat.magnitude * .35f && StepUp(cc, flat, stepLocal * sc, out float rise))
                {
                    flags |= cc.Move(Vector3.up * rise);
                    flags |= cc.Move(flat - moved);
                }
            }
            g = Probe(cc, stepWorld + .05f);
            // aderência: desce o degrau / acompanha a ladeira em vez de sair voando
            if (!cc.isGrounded && stepOk && !ascending && g.ok && !g.steep && g.dist > .001f && g.dist <= stepWorld + .05f)
            { flags |= cc.Move(Vector3.down * (g.dist + cc.skinWidth * sc * .5f)); g = Probe(cc, .1f); }
            bool near = g.ok && g.dist <= cc.skinWidth * sc + .03f;
            grounded = !ascending && (cc.isGrounded || near) && !(g.ok && g.steep && near);   // subindo (pulo) nunca conta como chão
            // rampa íngreme demais: escorrega ladeira abaixo (não sobe pulando)
            if (g.ok && g.steep && near) { slide = Mathf.Min(slide + 18 * Time.deltaTime, 9); var dir = Vector3.ProjectOnPlane(Vector3.down, g.normal).normalized; flags |= cc.Move(dir * slide * Time.deltaTime); }
            else slide = 0;
            return flags;
        }
    }
}
