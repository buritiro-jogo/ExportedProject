// Referencial ANATÔMICO das mãos (independe dos eixos do osso de cada rig):
//   +Z = punho → dedo médio (direção dos dedos), +Y = normal da palma (saindo da palma), medidos pelos ossos proximais dos dedos.
// Por que: a pose da arma do PlayerVisualPuppet original é dada no espaço do osso RightHand do rig mixamorig do jogo; os rigs do IE
// (ie_*) têm o osso da mão girado ~90° (ie_soldado ~120°) e alguns do pacote NPCS (n_friday, n_tiffany) 26–44° — com a pose crua
// no osso, a arma ficava deitada de lado / de cabeça para baixo nesses personagens. O Mecanim alinha a ANATOMIA entre rigs (é o que os
// clipes Humanoid transportam), então a arma presa ao referencial anatômico fica igual em todos.
using UnityEngine;

namespace FreeBala
{
    public static class FBHand
    {
        // média dos 116 rigs mixamorig do jogo original (c_*, p_*: desvio ≤ 7°) — onde a pose do puppet foi feita; espaço local do RightHand
        public static readonly Vector3 RefFwd = new(-.0855f, .9963f, -.0096f), RefSide = new(-.9963f, -.0854f, .0024f);
        public const float RefPalm = .0639f;                                         // punho → nó do médio (unidades locais do osso)
        public const float SameRigDeg = 10;                                          // abaixo disso é o mesmo rig: mantém a pose original exata

        public static Quaternion RefRight
        {
            get { var f = RefFwd.normalized; var s = Vector3.ProjectOnPlane(RefSide, f).normalized; return Quaternion.LookRotation(f, Vector3.Cross(f, s)); }
        }

        // referencial anatômico de uma mão no espaço LOCAL do osso da mão (false = rig sem os dedos necessários)
        public static bool Frame(Animator an, bool right, out Quaternion local, out float palm)
        {
            local = Quaternion.identity; palm = RefPalm;
            if (!an || !an.isHuman) return false;
            Transform B(HumanBodyBones r, HumanBodyBones l) => an.GetBoneTransform(right ? r : l);
            var hand = B(HumanBodyBones.RightHand, HumanBodyBones.LeftHand);
            var idx = B(HumanBodyBones.RightIndexProximal, HumanBodyBones.LeftIndexProximal);
            var mid = B(HumanBodyBones.RightMiddleProximal, HumanBodyBones.LeftMiddleProximal);
            var lit = B(HumanBodyBones.RightLittleProximal, HumanBodyBones.LeftLittleProximal);
            if (!lit) lit = B(HumanBodyBones.RightRingProximal, HumanBodyBones.LeftRingProximal);
            if (!hand || !idx || !mid || !lit) return false;
            Vector3 m = hand.InverseTransformPoint(mid.position), i = hand.InverseTransformPoint(idx.position), l = hand.InverseTransformPoint(lit.position);
            if (m.sqrMagnitude < 1e-10f) return false;
            var f = m.normalized; var s = Vector3.ProjectOnPlane(i - l, f); if (s.sqrMagnitude < 1e-12f) return false; s.Normalize();
            // palma: na T-pose (palma para baixo, frente +Z) a mão direita tem f=+X, s=+Z → Cross(f,s) = -Y; a esquerda f=-X → Cross(s,f) = -Y
            var n = right ? Vector3.Cross(f, s) : Vector3.Cross(s, f);
            local = Quaternion.LookRotation(f, n); palm = m.magnitude; return true;
        }

        // correção do encaixe da arma na mão direita: pose do rig de referência → este rig (rotação no espaço do osso) + fator do tamanho da mão
        public static Quaternion RightCorrection(Animator an, out float palmK, out float deg)
        {
            palmK = 1; deg = 0;
            if (!Frame(an, true, out var q, out var palm)) return Quaternion.identity;   // sem dedos (mixamorig simplificado): mesmo rig do puppet
            var c = q * Quaternion.Inverse(RefRight); deg = Quaternion.Angle(c, Quaternion.identity);
            if (deg < SameRigDeg) return Quaternion.identity;
            palmK = Mathf.Clamp(palm / RefPalm, .85f, 1.2f); return c;
        }
    }
}
