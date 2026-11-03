// Caminhante de rota da cena de teste de foot IK: anda pelos pontos (ida e volta), para um pouco nas pontas, usa a MESMA física do
// jogador (FBMotor) e alimenta o FBCharacter como os bots (velocidade remapeada para os clipes da IA). Não depende de FBMatch.
using UnityEngine;

namespace FreeBala
{
    public class FBIKWalker : MonoBehaviour
    {
        public Vector3[] route; public float speed = 1.6f, pause = 1.2f;
        [System.NonSerialized] public FBCharacter ch; CharacterController cc;
        int i = 1, dir = 1; float vy, slide, wait, stepLocal = -1, stuckT; Vector3 hv, lastP; bool onGround = true; FBMotor.Ground ground;
        public bool OnGround => onGround; public int Leg => i;

        void Awake() { ch = GetComponent<FBCharacter>(); cc = GetComponent<CharacterController>(); }

        // [parceiro 01/11] cronômetro por script (FBTempo, ligado só pelo teste do .exe); desligado custa só um teste de bool
        static readonly int __fbt_Update = FBTempo.Id("FBIKWalker.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            float dt = Time.deltaTime; if (dt <= 0 || !ch || !cc || route == null || route.Length < 2) return; float S = ch.ScaleK;
            if (stepLocal < 0) stepLocal = cc.stepOffset;
            var to = (route[i] - transform.position).Flat(); Vector3 want = Vector3.zero;
            if (wait > 0) wait -= dt;
            else if (to.magnitude < .35f * S) { bool end = i + dir < 0 || i + dir >= route.Length; if (end) { dir = -dir; wait = pause; } i += dir; }
            else want = to.normalized * speed * S;
            // travou (obstáculo alto): volta pela rota
            stuckT = want.sqrMagnitude > .01f && (transform.position - lastP).Flat().magnitude < .2f * speed * dt ? stuckT + dt : 0; lastP = transform.position;
            if (stuckT > 2.5f) { stuckT = 0; dir = -dir; i = Mathf.Clamp(i + dir, 0, route.Length - 1); }
            hv = Vector3.Lerp(hv, want, 1 - Mathf.Exp(-dt * (onGround ? 9 : 1.7f)));
            vy = onGround && vy < 0 ? -2 : vy - 35 * Mathf.Sqrt(S) * dt;
            var flags = FBMotor.Move(cc, (hv + Vector3.up * vy) * dt, onGround, vy > 0, stepLocal, .45f * S, ref slide, out onGround, out ground);
            if ((flags & CollisionFlags.Above) != 0 && vy > 0) vy = 0;
            ch.velocity = new Vector3(hv.x, vy, hv.z); ch.grounded = onGround;
            if (hv.Flat().magnitude > .3f) ch.Yaw = Mathf.LerpAngle(ch.Yaw, Mathf.Atan2(hv.x, hv.z) * Mathf.Rad2Deg, 1 - Mathf.Exp(-dt * 8));
            // velocidade de animação remapeada (igual aos bots)
            var P = GameData.Section("puppet"); float pw = Json.F(P, "playerWalkSpeed", 4), pr = Json.F(P, "playerRunSpeed", 6.8f), aw = Json.F(P, "aiWalkAnimSpeed", 2), ar = Json.F(P, "aiRunAnimSpeed", 5);
            float s = hv.Flat().magnitude / Mathf.Max(.01f, S), sp = s <= pw ? s / pw * aw : aw + (s - pw) / (pr - pw) * (ar - aw);
            var lv = Quaternion.Inverse(Quaternion.Euler(0, ch.Yaw, 0)) * hv.Flat(); float lm = lv.magnitude;
            ch.animVel = lm > 1e-3f ? new Vector2(lv.x / lm * sp, lv.z / lm * sp) : Vector2.zero;
            ch.sprint = s > 5.5f;
        }
    }
}
