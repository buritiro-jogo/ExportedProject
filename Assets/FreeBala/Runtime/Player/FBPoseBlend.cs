// r46: MISTURA DE POSE na troca de controle do esqueleto (pedido 29/10: "transições quebradas", "movimentos bruscos").
// Causa medida (sonda r46a, fim de sair do carro/descer da moto): o clipe de saída tem o deslocamento do corpo "assado" na pose (quadril
// até 1,2 m da raiz); no fim a raiz é levada para baixo do quadril e o mesmo clipe ainda tocava alguns quadros enquanto a camada sumia —
// o corpo saltava 1,2 m de lado e voltava (quadril 70–100 u/s, braço girando 3.700°/s num quadro). Agora: Capture() guarda a pose do
// instante da troca (quadril no MUNDO + giro local de cada osso), quem chamou troca o que quiser (raiz, clipe, ragdoll -> Animator) e,
// por 'len' segundos, cada osso sai da pose guardada e chega à pose nova (suave), depois de tudo que escreve em osso no quadro.
// Também usado ao levantar do chão depois da queda da moto (FBFall: pose do ragdoll -> clipe de levantar).
using UnityEngine;

namespace FreeBala
{
    [DefaultExecutionOrder(1200)]
    public class FBPoseBlend : MonoBehaviour
    {
        Transform[] bones; Quaternion[] loc; Transform hips; Vector3 hipP; Quaternion hipQ; float t, len; bool on;
        public static int Blends;
        // r46c: componente criado no MESMO quadro da troca não recebe LateUpdate nesse quadro (sonda: o quadro da troca saía cru, quadril
        // 1,39 m fora, só no 1º uso de cada personagem). Ensure() cria antes (início da transição/queda) e Pin() aplica a pose guardada já
        public static void Ensure(FBCharacter c) { if (c && !c.TryGetComponent<FBPoseBlend>(out _)) { var b = c.gameObject.AddComponent<FBPoseBlend>(); b.enabled = false; } }
        public static void Pin(FBCharacter c)
        {
            if (!c || !c.TryGetComponent<FBPoseBlend>(out var b) || !b.on || !b.hips) return;
            b.hips.SetPositionAndRotation(b.hipP, b.hipQ); for (int i = 0; i < b.bones.Length; i++) if (b.bones[i]) b.bones[i].localRotation = b.loc[i];
        }
        public static float Progress(FBCharacter c) => c && c.TryGetComponent<FBPoseBlend>(out var b) && b.on ? b.t / b.len : -1;
        public static bool Active(FBCharacter c) => c && c.TryGetComponent<FBPoseBlend>(out var b) && b.on;
        public static void Capture(FBCharacter c, float len)
        {
            var an = c ? c.an : null; if (!an || !an.isHuman) return;
            var b = c.gameObject.GetOrAdd<FBPoseBlend>(); b.hips = an.GetBoneTransform(HumanBodyBones.Hips); if (!b.hips) return;
            if (b.bones == null) { var L = new System.Collections.Generic.List<Transform>(); for (int i = 0; i < (int)HumanBodyBones.LastBone; i++) { var x = an.GetBoneTransform((HumanBodyBones)i); if (x && x != b.hips) L.Add(x); } b.bones = L.ToArray(); b.loc = new Quaternion[b.bones.Length]; }
            for (int i = 0; i < b.bones.Length; i++) b.loc[i] = b.bones[i].localRotation;
            b.hipP = b.hips.position; b.hipQ = b.hips.rotation; b.t = 0; b.len = Mathf.Max(.05f, len); b.on = true; b.enabled = true; Blends++;
        }
        // r46k: aplica já neste quadro (quem chama roda antes do LateUpdate do componente, ou o componente foi ligado no mesmo quadro e não
        // recebe LateUpdate) — no máximo uma vez por quadro
        public static void Run(FBCharacter c) { if (c && c.TryGetComponent<FBPoseBlend>(out var b) && b.on) b.LateUpdate(); }
        int stepped = -1;
        void LateUpdate()
        {
            if (stepped == Time.frameCount) return; stepped = Time.frameCount;
            if (!on || !hips) { enabled = false; return; }
            float k = Mathf.SmoothStep(0, 1, t / len); t += Time.deltaTime;
            hips.SetPositionAndRotation(Vector3.Lerp(hipP, hips.position, k), Quaternion.Slerp(hipQ, hips.rotation, k));
            for (int i = 0; i < bones.Length; i++) if (bones[i]) bones[i].localRotation = Quaternion.Slerp(loc[i], bones[i].localRotation, k);
            if (t >= len) { on = false; enabled = false; }
        }
    }
}
