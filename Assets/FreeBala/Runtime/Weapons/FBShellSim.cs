// Cartuchos ejetados: simulação própria e leve (pool fixo, sem Rigidbody). Sai pela janela de ejeção, cai com gravidade, quica no chão
// com perda de energia, o giro amortece a cada toque e o cartucho termina DEITADO e parado no piso (o ParticleSystem anterior seguia girando
// e às vezes ficava parado no ar sobre colisores). Some depois de alguns segundos.
using UnityEngine;

namespace FreeBala
{
    public class FBShellSim : MonoBehaviour
    {
        class S { public Transform t; public MeshRenderer r; public Vector3 v, w; public float life, len; public bool rest; public MaterialPropertyBlock mpb; }
        static FBShellSim I; S[] pool; int next; static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        static FBShellSim Ensure(int n)
        {
            if (I) return I;
            var g = new GameObject("FB_Cartuchos"); I = g.AddComponent<FBShellSim>(); I.pool = new S[n];
            var mesh = FBFxLib.Shell(); var mat = FBFxLib.Mat("Lit_Clip", null, Color.white, "CartuchoSim");
            for (int i = 0; i < n; i++)
            {
                var o = new GameObject("c" + i); o.transform.SetParent(g.transform, false); o.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = o.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; o.SetActive(false);
                I.pool[i] = new S { t = o.transform, r = r, mpb = new MaterialPropertyBlock() };
            }
            return I;
        }

        public static void Spawn(Vector3 p, Vector3 v, Quaternion rot, Vector3 size, Color col, int poolSize)
        {
            var sim = Ensure(poolSize); var s = sim.pool[sim.next]; sim.next = (sim.next + 1) % sim.pool.Length;
            s.t.gameObject.SetActive(true); s.t.SetPositionAndRotation(p, rot); s.t.localScale = size; s.len = size.y;
            s.v = v; s.w = new Vector3(Random.Range(-12f, 12f), Random.Range(-20f, 20f), Random.Range(-12f, 12f));   // rad/s, bem menor que antes
            s.life = 4.5f; s.rest = false; s.mpb.SetColor(ColorId, col); s.r.SetPropertyBlock(s.mpb);
        }

        static readonly int __fbt_Update = FBTempo.Id("FBShellSim.Update");   // [parceiro 01/11] medidor FBTempo
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            float dt = Mathf.Min(Time.deltaTime, .05f);
            foreach (var s in pool)
            {
                if (!s.t.gameObject.activeSelf) continue;
                s.life -= dt; if (s.life <= 0) { s.t.gameObject.SetActive(false); continue; }
                if (s.rest) continue;
                s.v += Physics.gravity * dt; var p = s.t.position; var step = s.v * dt;
                if (Physics.Raycast(p, step.normalized, out var h, step.magnitude + .01f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore))
                {
                    p = h.point + h.normal * .006f;
                    var vn = Vector3.Dot(s.v, h.normal) * h.normal; var vt = s.v - vn;
                    s.v = vt * .55f - vn * .3f; s.w *= .45f;                                     // quica perdendo energia; giro amortece no toque
                    if (h.normal.y > .6f && s.v.magnitude < .45f)
                    {
                        // assenta deitado: eixo do cartucho no plano do piso, rumo aleatório
                        var axis = Vector3.ProjectOnPlane(s.t.up, h.normal); if (axis.sqrMagnitude < 1e-4f) axis = Vector3.ProjectOnPlane(Random.onUnitSphere, h.normal);
                        s.t.SetPositionAndRotation(h.point + h.normal * .005f, Quaternion.LookRotation(axis.normalized, h.normal) * Quaternion.Euler(90, 0, 0));
                        s.rest = true; continue;
                    }
                }
                else p += step;
                s.t.position = p; s.t.rotation = Quaternion.Euler(s.w * Mathf.Rad2Deg * dt) * s.t.rotation;
            }
        }
    }
}
