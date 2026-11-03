// Fumaça do disparo (fiapo que sai do cano e sobe) e cartuchos ejetados (malha de cilindro latão / cartucho vermelho da escopeta)
// que quicam no chão (colisão de partícula; só perto da câmera e com FBFxQuality.Shells). Chamado pelo FBFx.Muzzle.
using UnityEngine;

namespace FreeBala
{
    public static class FBShotFx
    {
        static ParticleSystem psSmoke, psShell; static bool ready;
        public static void Warm()
        {
            if (ready) return; ready = true; FBFxQuality.Ensure(); bool hi = FBFxQuality.PoolDecals > 100;
            psSmoke = FBFxLib.PS("DisparoFumaca", FBFxLib.Mat("Particles_Alpha", FBFxLib.Tex("smoke"), Color.white, "DisparoFumaca"), hi ? 120 : 50,
                new FBFxLib.Opt { gravity = -.03f, drag = .5f, tilesX = 2, tilesY = 2, spin = true, size = FBFxLib.Curve(0, .4f, 1, 2.2f), color = FBFxLib.Fade(.05f, .35f) });
            if (FBFxQuality.PoolDecals > 0)
                psShell = FBFxLib.PS("Cartuchos", FBFxLib.Mat("Particles_Alpha", null, Color.white, "Cartucho"), hi ? 48 : 20,
                    new FBFxLib.Opt { gravity = 1.1f, mode = ParticleSystemRenderMode.Mesh, mesh = FBFxLib.Shell(), size3D = true, collide = true, bounce = .35f, dampen = .45f, color = FBFxLib.Fade(0, .85f) });
        }

        public static void Smoke(Vector3 p, Vector3 fwd, float size, string cls, bool silenced)
        {
            Warm(); float lod = FBFxQuality.Lod(p); if (lod <= 0) return;
            float k = cls == "escopeta" ? 2.2f : cls == "sniper" ? 1.8f : cls == "pistola" ? .7f : 1; if (silenced) k *= .5f;
            int n = FBFxQuality.Count(1.4f * k, lod);
            for (int i = 0; i < n; i++)
                FBFxLib.Emit(psSmoke, p + fwd * Random.Range(.02f, .1f), fwd * Random.Range(.3f, 1.1f) * k + Vector3.up * Random.Range(.1f, .35f), Random.Range(.05f, .12f) * Mathf.Max(.4f, size * 2) * Mathf.Sqrt(k),
                    Random.Range(.5f, 1.2f), new Color(.75f, .74f, .72f, Random.Range(.12f, .26f)), Random.Range(0, 360f));
        }

        public static void Shell(Vector3 port, Vector3 fwd, Vector3 right, string cls)
        {
            if (!FBFxQuality.Shells || cls == "explosivo") return;
            var c = FBFxQuality.Cam; if (!c || (c.transform.position - port).sqrMagnitude > 18 * 18 || FBFxQuality.Take(1) == 0) return;
            bool sg = cls == "escopeta"; float d = sg ? .019f : cls == "pistola" ? .0095f : .0105f, len = sg ? .065f : cls == "pistola" ? .019f : cls == "sniper" ? .07f : .045f;
            var up = Vector3.Cross(fwd, right).normalized; if (up.y < 0) up = -up;
            var v = right * Random.Range(1.4f, 2.6f) + up * Random.Range(.9f, 1.8f) - fwd * Random.Range(.1f, .5f);
            var col = sg ? new Color(.62f, .1f, .08f) : new Color(.8f, .62f, .26f);
            var rot = Quaternion.LookRotation(fwd, up) * Quaternion.Euler(90, 0, 0);    // eixo do cartucho = eixo da arma
            FBShellSim.Spawn(port, v, rot, new Vector3(d, len, d), col, FBFxQuality.PoolDecals > 100 ? 48 : 20);   // cai, quica e assenta deitado
        }
    }
}
