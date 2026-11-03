// Hitboxes nos ossos do Humanoid (porte do Actor.hitTest): cabeça = esfera de 0,17 m um pouco acima do osso; r51: barriga (quadril→peito)
// e peito (peito→pescoço) separados, braços e pernas — as regiões do dano do CS (FBDamageModel). Camada "Hitbox": só os tiros a enxergam
// (sem colisão física, são gatilhos).
using UnityEngine;

namespace FreeBala
{
    public class FBHitbox : MonoBehaviour
    {
        public FBCharacter owner; public bool head; public string part = "body"; public float mult = 1;
        public static bool Torso(string part) => part == "body" || part == "chest" || part == "stomach";   // r51: peito/barriga = "corpo" para os efeitos
        [System.NonSerialized] public Transform lo, mid, hi;                                     // tronco: quadril, divisa peito/barriga, pescoço
        // r51: peito × barriga pelo PONTO do impacto. A cápsula do peito (raio 0,28, ponta redonda) desce até o meio da barriga e é mais larga
        // que a dela — de frente, cobria a barriga inteira: no exe TODO tiro mirado na barriga contava peito (×1 em vez de ×1,25)
        public string PartAt(Vector3 p)
        {
            if (!lo || !mid || !hi || (part != "chest" && part != "stomach")) return part;
            var ax = hi.position - lo.position; float L2 = ax.sqrMagnitude; if (L2 < 1e-6f) return part;
            float k = Vector3.Dot(p - lo.position, ax) / L2, ks = Mathf.Clamp(Vector3.Dot(mid.position - lo.position, ax) / L2, .38f, .55f);
            return k < ks ? "stomach" : "chest";
        }
        // r51d: ponto acima do osso do pescoço (ao longo quadril → pescoço) = ponta redonda da cápsula do peito, que não é corpo (ver FBCombat)
        public bool AboveNeck(Vector3 p)
        {
            if (!lo || !hi) return false; var ax = hi.position - lo.position; float L2 = ax.sqrMagnitude;
            return L2 > 1e-6f && Vector3.Dot(p - lo.position, ax) / L2 > 1;
        }

        public static void Build(FBCharacter ch)
        {
            var an = ch.GetComponent<Animator>(); if (!an || !an.isHuman) return;
            foreach (var old in ch.GetComponentsInChildren<FBHitbox>(true)) Object.Destroy(old.gameObject);
            float s = ch.transform.lossyScale.y / FBConst.ModelScale * FBConst.ModelScale / 2.2f;
            T Add<T>(Transform parent, string n, string part, bool head) where T : Collider
            {
                var g = new GameObject("HB_" + n); g.layer = FBWorld.LHitbox; g.transform.SetParent(parent, false);
                var c = g.AddComponent<T>(); c.isTrigger = true; var h = g.AddComponent<FBHitbox>(); h.owner = ch; h.head = head; h.part = part; return c;
            }
            var headB = an.GetBoneTransform(HumanBodyBones.Head);
            if (headB) { var c = Add<SphereCollider>(headB, "Cabeca", "head", true); c.radius = .17f * s / headB.lossyScale.x; c.center = headB.InverseTransformVector(Vector3.up * .07f * s); }
            FBHitbox Capsule(HumanBodyBones a, HumanBodyBones b, float r, string part, bool topAtB = false)
            {
                var ta = an.GetBoneTransform(a); var tb = an.GetBoneTransform(b); if (!ta || !tb) return null;
                var c = Add<CapsuleCollider>(ta, a.ToString(), part, false);
                var lb = ta.InverseTransformPoint(tb.position); float sc = ta.lossyScale.x;
                c.center = lb * .5f; c.radius = r * s / sc; c.height = lb.magnitude + c.radius * 2;
                if (topAtB)
                {   // [parceiro] fisica 31/10: a superficie de cima termina no osso b (Neck) em vez de a meia-esfera ficar centrada nele; a base (a - r) fica onde estava
                    c.height -= c.radius; c.center -= lb.normalized * (c.radius * .5f);
                }
                c.direction = Mathf.Abs(lb.x) > Mathf.Abs(lb.y) ? (Mathf.Abs(lb.x) > Mathf.Abs(lb.z) ? 0 : 2) : (Mathf.Abs(lb.y) > Mathf.Abs(lb.z) ? 1 : 2);
                return c.GetComponent<FBHitbox>();
            }
            if (!FBDamageModel.On)
            {   // -fbdanoantigo: as 3 regiões da capa do Free Fire (antes da r51)
                Capsule(HumanBodyBones.Hips, HumanBodyBones.Neck, .28f * .62f, "body");
                // [parceiro] fisica 31/10: topo da capsula do tronco termina no Neck (antes a meia-esfera r 0,28 centrada no Neck engolia a esfera da
                // cabeca e headshot virava body). Só no dano antigo: no do CS a cápsula do peito fica inteira e o r51d (FBCombat, AboveNeck) separa
                // o que passou do pescoço — medido no exe, e sem perder a cobertura dos ombros
                Capsule(HumanBodyBones.Spine, HumanBodyBones.Neck, .28f, "body", topAtB: true);
                Capsule(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftFoot, .11f, "limb"); Capsule(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightFoot, .11f, "limb");
                Capsule(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftHand, .075f, "limb"); Capsule(HumanBodyBones.RightUpperArm, HumanBodyBones.RightHand, .075f, "limb");
                return;
            }
            // r51 (dano do CS): regiões do CS — barriga (quadril → peito, ×1,25), peito (peito → pescoço, ×1), braços (×1), pernas (×0,75)
            var chestB = an.GetBoneTransform(HumanBodyBones.Chest) ? HumanBodyBones.Chest : HumanBodyBones.Spine;
            var hbS = Capsule(HumanBodyBones.Hips, chestB, .25f, "stomach"); var hbC = Capsule(chestB, HumanBodyBones.Neck, .28f, "chest");
            foreach (var t in new[] { hbS, hbC }) if (t) { t.lo = an.GetBoneTransform(HumanBodyBones.Hips); t.mid = an.GetBoneTransform(chestB); t.hi = an.GetBoneTransform(HumanBodyBones.Neck); }
            Capsule(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftFoot, .11f, "leg"); Capsule(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightFoot, .11f, "leg");
            Capsule(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftHand, .075f, "arm"); Capsule(HumanBodyBones.RightUpperArm, HumanBodyBones.RightHand, .075f, "arm");
        }
    }

    // alvo de papel do estande (modo TREINO): conta acertos e headshots pela altura do impacto
    public class FBTarget : MonoBehaviour
    {
        public int hits, heads; public float headY = .78f; public System.Action<FBTarget, bool> onHit;
        public bool IsHead(Vector3 p) => transform.InverseTransformPoint(p).y > headY;
        public void Hit(Vector3 p, float dist) { bool h = IsHead(p); hits++; if (h) heads++; FBMatch.Stats.hits++; onHit?.Invoke(this, h); }
    }
}
