// Testes dos passos (agente de áudio): contato do pé pelos ossos (marcha sintética), alternância esquerdo/direito, nada parado/no ar,
// queda para passadas por distância quando os ossos param (culling), cadência andar × correr, piso pelo nome/marca e pelo raio físico.
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FreeBala.Tests
{
    public class FBFootstepsTests
    {
        // marcha: cada pé sobe 12 cm em meia passada e fica no chão na outra meia (fase oposta entre os pés)
        static (int l, int r, int fb) Walk(FBFootContact c, float secs, float cadence, float speed, bool bones = true, bool ground = true, float slope = 0)
        {
            int l = 0, r = 0, fb = 0; float dt = 1 / 60f;
            for (float t = 0; t < secs; t += dt)
            {
                float ph = t * cadence, y0 = slope * t;
                float hl = y0 + .08f + Mathf.Max(0, Mathf.Sin(ph * Mathf.PI * 2)) * .12f, hr = y0 + .08f + Mathf.Max(0, Mathf.Sin(ph * Mathf.PI * 2 + Mathf.PI)) * .12f;
                int e = c.Update(dt, bones, hl, hr, speed, ground, speed > 3.6f);
                if (e == 0) l++; else if (e == 1) r++; if (e >= 0 && c.Fallback) fb++;
            }
            return (l, r, fb);
        }

        [Test]
        public void Contato_PeloOsso_AlternaEPisaUmaVezPorCiclo()
        {
            var (l, r, fb) = Walk(new FBFootContact(), 6, .9f, 1.5f);   // 0,9 ciclo/s ≈ 1,8 passo/s (andar)
            Assert.That(l, Is.InRange(4, 6)); Assert.That(r, Is.InRange(4, 6)); Assert.AreEqual(0, fb, "com ossos não usa passada por distância");
            Assert.LessOrEqual(Mathf.Abs(l - r), 1);
        }

        [Test]
        public void Contato_CorrerTemCadenciaMaior_E_RampaNaoAtrapalha()
        {
            var w = Walk(new FBFootContact(), 6, .9f, 1.5f); var run = Walk(new FBFootContact(), 6, 1.4f, 5f);
            Assert.Greater(run.l + run.r, w.l + w.r);
            var ramp = Walk(new FBFootContact(), 6, .9f, 1.5f, slope: .3f);    // subindo 0,3 m/s (escada/rampa)
            Assert.That(ramp.l + ramp.r, Is.InRange(w.l + w.r - 2, w.l + w.r + 1));
        }

        [Test]
        public void SemPassos_ParadoOuNoAr()
        {
            Assert.AreEqual((0, 0, 0), Walk(new FBFootContact(), 3, .9f, 0));            // pés mexendo no lugar (idle) sem velocidade
            Assert.AreEqual((0, 0, 0), Walk(new FBFootContact(), 3, .9f, 2, ground: false));
        }

        [Test]
        public void SemOssos_CaiParaPassadaPorDistancia()
        {
            var c = new FBFootContact(); int n = 0; float dt = 1 / 60f;
            for (float t = 0; t < 7.8f; t += dt) if (c.Update(dt, false, 0, 0, 1.56f, true, false) >= 0) n++;   // 12,2 m andando / 0,78 m
            Assert.That(n, Is.InRange(14, 16)); Assert.IsTrue(c.Fallback);
            // ossos congelados (Animator fora da tela) enquanto anda: depois de um tempo também cai para passadas
            c = new FBFootContact(); n = 0; for (float t = 0; t < 6; t += dt) if (c.Update(dt, true, .08f, .08f, 1.5f, true, false) >= 0) n++;
            Assert.Greater(n, 4); Assert.IsTrue(c.Fallback);
        }

        [Test]
        public void Piso_PeloNomeEMarca()
        {
            Assert.AreEqual(FBStepSurf.Asfalto, FBStepSurfaces.FromName("Rua_Asfalto mat_road", 0));
            Assert.AreEqual(FBStepSurf.Grama, FBStepSurfaces.FromName("canteiro grass_01", 1));
            Assert.AreEqual(FBStepSurf.Terra, FBStepSurfaces.FromName("chao", 1));
            Assert.AreEqual(FBStepSurf.Metal, FBStepSurfaces.FromName("qualquer", 4));
            Assert.AreEqual(FBStepSurf.Madeira, FBStepSurfaces.FromName("deck_madeira", 0));
            Assert.AreEqual(FBStepSurf.Agua, FBStepSurfaces.FromName("poça de agua", 0));
            Assert.AreEqual(FBStepSurf.Concreto, FBStepSurfaces.FromName("calcada_cimento", 0));
            foreach (FBStepSurf s in System.Enum.GetValues(typeof(FBStepSurf)))
                Assert.IsTrue(Resources.Load<AudioClip>("Audio/" + FBStepSurfaces.ClipBase(s) + "0"), "sem clipe para " + s);
        }

        [UnityTest]
        public IEnumerator Piso_PeloRaioFisico()
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = "Grama_teste"; g.layer = FBWorld.LMap; g.transform.position = new Vector3(500, -.05f, 500); g.transform.localScale = new Vector3(4, .1f, 4);
            var h = GameObject.CreatePrimitive(PrimitiveType.Cube); h.name = "Placa"; h.layer = FBWorld.LMap; h.AddComponent<FBSurface>().kind = 4; h.transform.position = new Vector3(510, -.05f, 500); h.transform.localScale = new Vector3(4, .1f, 4);
            Physics.SyncTransforms(); yield return null;
            Assert.AreEqual(FBStepSurf.Grama, FBFootsteps.SurfaceAt(new Vector3(500, 0, 500)));
            Assert.AreEqual(FBStepSurf.Metal, FBFootsteps.SurfaceAt(new Vector3(510, 0, 500)));
            Object.Destroy(g); Object.Destroy(h);
        }
    }
}
