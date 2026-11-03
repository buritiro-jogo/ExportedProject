// Agente 9: regras puras de qualidade (detecção/perfis), rede (conversão de coordenadas, JSON de saída, interpolação de veículo)
// e do passe de otimização (classificação de textura, tamanho por plataforma, tipo de carga de áudio). Sem cena, rodam em segundos.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using FreeBala.EditorTools;

namespace FreeBala.Tests
{
    public class FBPerfTests
    {
        static FBDeviceInfo Dev(FBPlat p, int mem, int gpuMem, int cores, string gpu, bool mobBrowser = false) => new FBDeviceInfo { plat = p, memMB = mem, gpuMB = gpuMem, cores = cores, gpu = gpu, shaderLevel = 50, mobileBrowser = mobBrowser };

        [Test] public void DeteccaoPorPlataforma()
        {
            Assert.AreEqual(FBQLevel.Ultra, FBQuality.Detect(Dev(FBPlat.PC, 32000, 12000, 16, "NVIDIA GeForce RTX 4070")));
            Assert.AreEqual(FBQLevel.Alto, FBQuality.Detect(Dev(FBPlat.PC, 16000, 6000, 6, "NVIDIA GeForce GTX 1660")));
            Assert.AreEqual(FBQLevel.Baixo, FBQuality.Detect(Dev(FBPlat.PC, 8000, 1024, 4, "Intel(R) UHD Graphics 620")));
            Assert.AreEqual(FBQLevel.Medio, FBQuality.Detect(Dev(FBPlat.PC, 16000, 2048, 8, "Intel(R) Iris(R) Xe Graphics")));
            Assert.AreEqual(FBQLevel.Medio, FBQuality.Detect(Dev(FBPlat.PC, 16000, 512, 8, "AMD Radeon 780M Graphics")), "APU forte não é placa dedicada");
            Assert.AreEqual(FBQLevel.Baixo, FBQuality.Detect(Dev(FBPlat.PC, 8000, 512, 8, "AMD Radeon(TM) Graphics")));
            Assert.AreEqual(FBQLevel.Alto, FBQuality.Detect(Dev(FBPlat.PC, 16000, 8000, 6, "AMD Radeon RX 7600")), "RX dedicada não cai como APU");
            Assert.AreEqual(FBQLevel.Baixo, FBQuality.Detect(Dev(FBPlat.PC, 16000, 0, 8, "Microsoft Basic Render Driver")));
            Assert.AreEqual(FBQLevel.Baixo, FBQuality.Detect(Dev(FBPlat.Android, 3000, 0, 8, "Mali-G52")));
            Assert.AreEqual(FBQLevel.Medio, FBQuality.Detect(Dev(FBPlat.Android, 6000, 0, 8, "Adreno (TM) 618")));
            Assert.AreEqual(FBQLevel.Alto, FBQuality.Detect(Dev(FBPlat.Android, 8000, 0, 8, "Adreno (TM) 740")));
            Assert.AreEqual(FBQLevel.Alto, FBQuality.Detect(Dev(FBPlat.iOS, 6000, 0, 6, "Apple A15 GPU")));
            Assert.AreEqual(FBQLevel.Baixo, FBQuality.Detect(Dev(FBPlat.iOS, 2000, 0, 6, "Apple A10 GPU")));
            Assert.AreEqual(FBQLevel.Baixo, FBQuality.Detect(Dev(FBPlat.WebGL, 0, 0, 4, "ANGLE (Apple, Apple M1)", true)));
            Assert.AreEqual(FBQLevel.Alto, FBQuality.Detect(Dev(FBPlat.WebGL, 0, 0, 8, "ANGLE (NVIDIA, NVIDIA GeForce RTX 3060)")));
            Assert.AreEqual(FBQLevel.Medio, FBQuality.Detect(Dev(FBPlat.WebGL, 0, 0, 8, "WebKit WebGL")));
        }

        [Test] public void PerfisCrescemComONivel()
        {
            foreach (var plat in new[] { FBPlat.PC, FBPlat.Android, FBPlat.iOS, FBPlat.WebGL })
            {
                var d = Dev(plat, 8000, 4000, 8, "x"); FBQProfile prev = null;
                for (int i = 0; i < 4; i++)
                {
                    var p = FBQuality.Build((FBQLevel)i, d);
                    Assert.That(p.renderScale, Is.InRange(.5f, 1f), $"{plat} {i}"); Assert.LessOrEqual(p.minRenderScale, p.renderScale);
                    if (prev != null)
                    {
                        Assert.GreaterOrEqual(p.maxParticles, prev.maxParticles, $"{plat} partículas"); Assert.GreaterOrEqual(p.shadowDistance, prev.shadowDistance, $"{plat} sombra");
                        Assert.GreaterOrEqual(p.lodBias, prev.lodBias, $"{plat} lod"); Assert.GreaterOrEqual(p.footIKDistance, prev.footIKDistance); Assert.LessOrEqual(p.smallCasterSize, prev.smallCasterSize);
                        Assert.GreaterOrEqual(p.renderScale, prev.renderScale); Assert.GreaterOrEqual(p.texBudgetMB, prev.texBudgetMB); Assert.GreaterOrEqual(p.vegMaxInstances, prev.vegMaxInstances, $"{plat} vegetação"); Assert.GreaterOrEqual(p.vegDrawDistance, prev.vegDrawDistance); Assert.GreaterOrEqual(p.vegetationDensity, prev.vegetationDensity);
                    }
                    Assert.Less(p.vegLodDistance, p.vegDrawDistance, "LOD da vegetação antes do corte");
                    if (plat != FBPlat.PC) { Assert.IsFalse(p.vegShadows, "sombra de vegetação só no PC"); Assert.IsFalse(p.ssao, "SSAO só no PC"); Assert.AreEqual(1, p.shadowCascades, "1 cascata fora do PC"); Assert.IsTrue(p.dynamicResolution); }
                    prev = p;
                }
            }
            Assert.IsFalse(FBQuality.Build(FBQLevel.Baixo, Dev(FBPlat.Android, 3000, 0, 8, "")).shadows, "Android baixo sem sombra");
            Assert.IsTrue(FBQuality.Build(FBQLevel.Baixo, Dev(FBPlat.PC, 8000, 1000, 4, "")).shadows, "PC baixo com sombra curta");
            Assert.AreEqual(30, FBQuality.Build(FBQLevel.Baixo, Dev(FBPlat.Android, 3000, 0, 8, "")).targetFps);
        }

        [Test] public void FootIKDistanciaEIntervalo()
        {
            var keep = FBQuality.FootIK;
            try
            {
                FBQuality.FootIK = false; Assert.IsFalse(FBQuality.FootIKFor(Vector3.zero, true, 0), "chave geral desliga até o jogador");
                FBQuality.FootIK = true; Assert.IsTrue(FBQuality.FootIKFor(new Vector3(9999, 0, 0), true, 0), "jogador sempre tem IK");
            }
            finally { FBQuality.FootIK = keep; }
        }

        [Test] public void CoordenadasWebIdaEVolta()
        {
            var u = new Vector3(3.5f, 1.2f, -7.25f); var w = Coords.ToWeb(u); var back = Coords.FromWeb(new List<object> { (double)w[0], (double)w[1], (double)w[2] });
            Assert.That((back - u).magnitude, Is.LessThan(1e-4f));
            Assert.AreEqual(90f, Coords.YawFromWeb(Coords.YawToWeb(90)), 1e-3f);
        }

        [Test] public void JsonDeSaidaCompativel()
        {
            var s = FBNet.ToJson(new Dictionary<string, object> { ["t"] = "st", ["p"] = new float[] { 1.5f, -2, 0 }, ["w"] = null, ["n"] = "a\"b", ["ok"] = true, ["f"] = 33 });
            Assert.AreEqual("{\"t\":\"st\",\"p\":[1.5,-2,0],\"w\":null,\"n\":\"a\\\"b\",\"ok\":true,\"f\":33}", s);
            var back = Json.Parse(s) as Dictionary<string, object>; Assert.AreEqual("st", Json.S(back, "t")); Assert.AreEqual(33, Json.I(back, "f"));
            var old = System.Threading.Thread.CurrentThread.CurrentCulture;   // vírgula decimal (pt-BR) não pode vazar no JSON
            try { System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("pt-BR"); Assert.AreEqual("[0.25]", FBNet.ToJson(new float[] { .25f })); }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = old; }
        }

        [Test] public void VeiculoInterpoladoComoNoWeb()
        {
            var a = new List<object> { "c1", 0.0, 0.0, 0.0, 3.0, 0.0, 0.0, 0.0, 0.0, 10.0, 1, 0.0 };
            var b = new List<object> { "c1", 2.0, 0.0, 4.0, -3.0, 0.0, 0.0, 0.0, 0.0, 20.0, 0, 0.0 };
            var m = FBRemote.LerpVe(a, b, .5f);
            Assert.AreEqual("c1", m[0]); Assert.AreEqual(1f, Json.Fv(m[1]), 1e-4f); Assert.AreEqual(2f, Json.Fv(m[3]), 1e-4f); Assert.AreEqual(15f, Json.Fv(m[9]), 1e-4f);
            Assert.AreEqual(0, m[10], "paredão não interpola");
            float yaw = Json.Fv(m[4]); Assert.That(Mathf.Abs(Mathf.Abs(yaw) - Mathf.PI), Is.LessThan(.01f), "guinada pelo caminho curto (passa por ±π, não por 0)");
        }

        [Test] public void ClassificacaoDeTexturaEAudio()
        {
            Assert.AreEqual(FBOptimize.TexKind.UI, FBOptimize.Classify("Assets/FreeBala/Resources/MapShots/orla.jpg", null));
            Assert.AreEqual(FBOptimize.TexKind.Map, FBOptimize.Classify("Assets/FreeBala/Resources/FBImported/Maps/orla_tex/muro.png", null));
            Assert.AreEqual(FBOptimize.TexKind.Normal, FBOptimize.Classify("Assets/FreeBala/Resources/FBImported/Weapons/ak_tex/ak_normal.png", null));
            Assert.AreEqual(FBOptimize.TexKind.Albedo, FBOptimize.Classify("Assets/FreeBala/Resources/FBImported/Chars/c_kaiob2_tex/corpo.png", null));
            Assert.AreEqual(4096, FBOptimize.MaxSize(FBOptimize.TexKind.Map, "Standalone")); Assert.AreEqual(1024, FBOptimize.MaxSize(FBOptimize.TexKind.Albedo, "Android")); Assert.AreEqual(2048, FBOptimize.MaxSize(FBOptimize.TexKind.Map, "WebGL"));
            Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, FBOptimize.LoadTypeFor(.8f)); Assert.AreEqual(AudioClipLoadType.CompressedInMemory, FBOptimize.LoadTypeFor(8)); Assert.AreEqual(AudioClipLoadType.Streaming, FBOptimize.LoadTypeFor(180));
        }

        // materiais-modelo (Resources/FBMats): o build só leva variantes de shader usadas por algum material — sem eles a transparência some no player
        [Test] public void MateriaisModeloDeTransparencia()
        {
            foreach (var sh in new[] { "Lit", "Unlit", "Particles" })
                foreach (var kind in new[] { "Alpha", "Add", "Clip", "AlphaClip" })
                {
                    var m = Resources.Load<Material>($"FBMats/{sh}_{kind}"); Assert.IsNotNull(m, $"{sh}_{kind} faltando");
                    if (kind == "Alpha" || kind == "Add") { Assert.IsTrue(m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), $"{sh}_{kind} sem keyword transparente"); Assert.GreaterOrEqual(m.renderQueue, 2900, $"{sh}_{kind} fila"); }
                    if (kind.Contains("Clip")) Assert.IsTrue(m.IsKeywordEnabled("_ALPHATEST_ON"), $"{sh}_{kind} sem alpha test");
                }
        }
    }
}
