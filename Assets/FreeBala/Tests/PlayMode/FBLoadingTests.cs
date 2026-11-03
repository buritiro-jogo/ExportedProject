// Tela de carregamento (UI/FBLoading): cobre a tela ANTES de trocar de cena, progresso só sobe, passa pelas etapas de aquecimento,
// some só com a partida montada e devolve o tempo do jogo. Prints em unity/logs/print_carregamento_*.png.
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FreeBala.Tests
{
    public class FBLoadingTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator Carregamento_CobreEsoSaiPronto()
        {
            FBSession.Load(); FBSession.map = "estande"; FBSession.mode = "treino"; FBSession.online = false; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Menu"); while (!op.isDone) yield return null;
            for (float t = 0; t < 1.5f; t += Time.unscaledDeltaTime) yield return null;
            var menuScene = SceneManager.GetActiveScene().name;
            FBLoading.Load("Map_estande", "estande", "treino"); yield return null;
            var L = FBLoading.I; Assert.IsNotNull(L, "tela de carregamento não abriu");
            float last = 0, w = 0; bool coveredBeforeSwap = false, sawWarm = false, shot1 = false; var dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../logs")); System.IO.Directory.CreateDirectory(dir);
            while (FBLoading.I && w < 120)
            {
                yield return null; w += Time.unscaledDeltaTime; if (!FBLoading.I) break;
                Assert.GreaterOrEqual(L.Progress + 1e-4f, last, "progresso voltou"); last = L.Progress;
                if (SceneManager.GetActiveScene().name == menuScene && L.Cover >= .99f) coveredBeforeSwap = true;
                if (SceneManager.GetActiveScene().name != menuScene) Assert.IsTrue(coveredBeforeSwap, "a cena trocou com a tela ainda não coberta");
                if (L.Stage == "Aquecendo gráficos") { sawWarm = true; if (!shot1) { shot1 = true; ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "print_carregamento_aquecendo.png")); } }
                if (L.Stage != "Pronto" && SceneManager.GetActiveScene().name != menuScene && FBMatch.I && FBMatch.I.state != "loading") Assert.Greater(L.Cover, .98f, "partida aparecendo antes de pronta");
            }
            Assert.IsNull(FBLoading.I, "tela de carregamento não saiu (travou?)");
            Assert.IsTrue(sawWarm, "não passou pelo aquecimento de gráficos");
            Assert.IsNotNull(FBMatch.I); Assert.AreEqual("play", FBMatch.I.state); Assert.AreEqual(1f, Time.timeScale, 1e-4f, "tempo do jogo ficou parado");
            Debug.Log($"[FreeBala][teste] carregamento do estande: {w:0.0} s até a partida");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "print_carregamento_partida.png")); yield return null; yield return null;
        }
    }
}
