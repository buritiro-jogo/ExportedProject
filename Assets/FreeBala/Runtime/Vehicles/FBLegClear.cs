// r48: PERNA QUE PASSA POR CIMA DA MOTO no subir/descer. O clipe do 171 (agora fiel, retarget nativo — FBRetargetUE) passa a perna direita
// na altura do banco/tanque da moto DELE; na XJ6 (tanque e rabeta mais altos e largos) coxa, canela e pé atravessavam o chassi (sonda r48h:
// subir 55–73 quadros, descer 20–29; com o clipe antigo do JS, errado 7 cm por junta, a perna ia mais alta por acaso: 5–41).
// Aqui: a malha visível da moto (LOD 0) vira colisor (uma vez por moto, solto na cena e ligado só durante a consulta) e, enquanto a perna está
// livre do IK das pedaleiras, cada ponto dela (meio da coxa, joelho, meio da canela, tornozelo, ponta do pé) que estiver abaixo do topo da moto
// (raio de cima para baixo) faz a perna inteira girar no quadril para CIMA E PARA FORA (lado da perna) — o joelho contorna o tanque como
// numa subida de verdade; só para cima ele ia ao peito (fotos r48k). Suave no tempo (sobe rápido, volta devagar) e nos limites: ligar/desligar
// de uma vez dava pé a 3.500°/s (sonda r48j). Ponto bem abaixo do topo (embaixo da pedaleira) e perna de apoio (pé no chão) quase não giram.
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public static class FBLegClear
    {
        public const int Layer = FBPoseProbe.Layer;                                   // desligados fora da consulta: a sonda (mesma camada) não os vê
        class Set { public readonly List<MeshCollider> cols = new(); public readonly List<Transform> src = new(); }
        class St { public float t; public readonly float[] ang = new float[2]; public readonly Vector3[] ax = new Vector3[2]; }
        static readonly Dictionary<FBCar, Set> sets = new();
        static readonly Dictionary<FBCharacter, St> states = new();
        public static int Lifts, Built; public static float LiftMax;
        static readonly (HumanBodyBones u, HumanBodyBones l, HumanBodyBones f, HumanBodyBones t)[] Legs =
            { (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes),
              (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes) };

        static Set Of(FBCar car)
        {
            if (sets.Count > 0)
            {   // moto destruída: os colisores soltos vão junto
                List<FBCar> dead = null; foreach (var kv in sets) if (!kv.Key) (dead ??= new()).Add(kv.Key);
                if (dead != null) foreach (var d in dead) { foreach (var c in sets[d].cols) if (c) Object.Destroy(c.gameObject); sets.Remove(d); }
            }
            if (sets.TryGetValue(car, out var s)) return s;
            s = new Set(); sets[car] = s; var root = car.mx ? car.mx.gameObject : car.gameObject;
            var lodSkip = new HashSet<Renderer>(); foreach (var lg in root.GetComponentsInChildren<LODGroup>(true)) { var L = lg.GetLODs(); for (int i = 1; i < L.Length; i++) foreach (var r in L[i].renderers) if (r) lodSkip.Add(r); }
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r || !r.enabled || !r.gameObject.activeInHierarchy || lodSkip.Contains(r) || r.name.StartsWith("sombra") || r.GetComponentInParent<FBCharacter>()) continue;
                var mf = r.GetComponent<MeshFilter>(); var m = mf ? mf.sharedMesh : null; if (!m || !m.isReadable || m.vertexCount < 3) continue;
                var g = new GameObject("pernas:" + r.name) { layer = Layer }; var mc = g.AddComponent<MeshCollider>(); mc.enabled = false; mc.sharedMesh = m;
                s.cols.Add(mc); s.src.Add(r.transform);
            }
            Built++; return s;
        }

        // r50: consulta avulsa (perna de apoio parada, FBMxBike) — liga os colisores da malha da moto, testa trechos de osso, desliga
        public static bool Arm(FBCar car, bool on)
        {
            if (!car) return false; var s = Of(car); if (s.cols.Count == 0) return false;
            for (int i = 0; i < s.cols.Count; i++) { var c = s.cols[i]; var t = s.src[i]; if (!c || !t) continue; if (on) { c.transform.SetPositionAndRotation(t.position, t.rotation); c.transform.localScale = t.lossyScale; } c.enabled = on; }
            if (on) Physics.SyncTransforms(); return true;
        }
        public static bool Hits(Vector3 a, Vector3 b) { int m = 1 << Layer; return Physics.Linecast(a, b, m, QueryTriggerInteraction.Collide) || Physics.Linecast(b, a, m, QueryTriggerInteraction.Collide); }
        /// <summary>w = quanto a perna está livre (1 − peso do IK das pedaleiras na transição).</summary>
        public static void Apply(FBCharacter a, FBCar u, float w)
        {
            var an = a ? a.an : null; if (!u || !an || !an.isHuman) return;
            if (!states.TryGetValue(a, out var st) || Time.time - st.t > .25f) { if (states.Count > 64) states.Clear(); st = states[a] = new St(); }   // transição nova: começa do zero
            float dt = Mathf.Clamp(Time.time - st.t, 0, .1f); st.t = Time.time;
            var s = Of(u); if (s.cols.Count == 0) return;
            bool any = w > .01f || st.ang[0] > .1f || st.ang[1] > .1f; if (!any) return;
            for (int i = 0; i < s.cols.Count; i++) { var c = s.cols[i]; var t = s.src[i]; if (!c || !t) continue; c.transform.SetPositionAndRotation(t.position, t.rotation); c.transform.localScale = t.lossyScale; c.enabled = true; }
            Physics.SyncTransforms();
            try { for (int i = 0; i < 2; i++) Leg(an, Legs[i], i == 0 ? -u.transform.right : u.transform.right, a.ScaleK, w, st, i, dt); }
            finally { foreach (var c in s.cols) if (c) c.enabled = false; }
        }

        static void Leg(Animator an, (HumanBodyBones u, HumanBodyBones l, HumanBodyBones f, HumanBodyBones t) b, Vector3 side, float k, float w, St st, int li, float dt)
        {
            Transform U = an.GetBoneTransform(b.u), L = an.GetBoneTransform(b.l), F = an.GetBoneTransform(b.f), T = an.GetBoneTransform(b.t); if (!U || !L || !F) return;
            // perna de apoio: tornozelo perto do chão do mapa (10–20 cm, suave) — a canela ao lado da pedaleira levantaria o pé do chão
            float sup = 1; if (Physics.Raycast(F.position + Vector3.up * .3f * k, Vector3.down, out var gh, 1f * k, 1 << FBWorld.LMap, QueryTriggerInteraction.Ignore)) sup = Mathf.SmoothStep(0, 1, (F.position.y - gh.point.y - .10f * k) / (.10f * k));
            var dir = (Vector3.up + side.Flat().normalized).normalized; int mask = 1 << Layer;   // para cima e para fora (45°)
            var q0 = U.rotation; float total = 0; Vector3 axSum = Vector3.zero;
            if (sup > .001f && w > .01f)
                for (int it = 0; it < 3; it++)
                {
                    float best = 0; Vector3 axis = Vector3.zero, hip = U.position;
                    for (int j = 0; j < 5; j++)
                    {
                        Vector3 p; float r;                                                      // ponto do osso e raio da carne em volta dele
                        switch (j) { case 0: p = Vector3.Lerp(U.position, L.position, .5f); r = .08f; break; case 1: p = L.position; r = .06f; break; case 2: p = Vector3.Lerp(L.position, F.position, .5f); r = .05f; break; case 3: p = F.position; r = .07f; break; default: if (!T) continue; p = T.position; r = .03f; break; }
                        r *= k;
                        if (!Physics.Raycast(p + Vector3.up * .8f * k, Vector3.down, out var h, 1.1f * k, mask, QueryTriggerInteraction.Collide)) continue;
                        float below = h.point.y - p.y, need = below + r; if (need <= .002f * k) continue;
                        need *= 1 - Mathf.SmoothStep(0, 1, (below - .25f * k) / (.12f * k));        // bem abaixo do topo (pedaleira, chão): não é perna passando
                        if (need <= .002f * k) continue;
                        // girar em volta de n = cross(v, dir) leva v na direção dir; o ponto anda cross(n, v) por radiano — a parte vertical é o que sobe
                        var v = p - hip; var ax = Vector3.Cross(v, dir); if (ax.sqrMagnitude < 1e-6f) continue; var n = ax.normalized; float rise = Vector3.Cross(n, v).y; if (rise < .05f * k) continue;
                        float ang = Mathf.Asin(Mathf.Clamp01(need / rise)) * Mathf.Rad2Deg; if (ang > best) { best = ang; axis = n; }
                    }
                    if (best <= .2f) break;
                    best = Mathf.Min(best, 45 - total); if (best <= .2f) break;
                    U.rotation = Quaternion.AngleAxis(best, axis) * U.rotation; total += best; axSum += axis * best;
                }
            U.rotation = q0;
            float want = total * sup, cur = st.ang[li];
            cur = Mathf.Lerp(cur, want, 1 - Mathf.Exp(-dt * (want > cur ? 22f : 7f)));       // sobe rápido (passar a tempo), volta devagar
            if (axSum.sqrMagnitude > 1e-8f) st.ax[li] = st.ax[li].sqrMagnitude < 1e-8f ? axSum.normalized : Vector3.Slerp(st.ax[li], axSum.normalized, 1 - Mathf.Exp(-dt * 15f)).normalized;
            st.ang[li] = cur;
            float apply = cur * Mathf.Clamp01(w);                                                  // troca suave com o IK das pedaleiras (peso 1 − w)
            if (apply > .1f && st.ax[li].sqrMagnitude > 1e-8f) { U.rotation = Quaternion.AngleAxis(apply, st.ax[li]) * q0; Lifts++; LiftMax = Mathf.Max(LiftMax, apply); }
        }
    }
}
