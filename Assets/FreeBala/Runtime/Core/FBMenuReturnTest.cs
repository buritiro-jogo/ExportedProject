// Autoteste "voltar ao menu" (rodada 39, retorno do usuário: depois de jogar uma partida e voltar à tela inicial os personagens ganhavam
// bordas): FreeBala.exe -fbvoltamenu [mapa:modo]. Print do menu na abertura, partida de verdade por 15 s, FBMatch.ToMenu (o mesmo caminho
// do botão SAIR) e print do menu de novo; no log o estado global de render (névoa, volumétrica, escala, AA) nas duas vezes.
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBMenuReturnTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbvoltamenu"); if (i < 0) return;
            var g = new GameObject("FB_TesteVoltaMenu"); DontDestroyOnLoad(g); var t = g.AddComponent<FBMenuReturnTest>();
            if (i + 1 < a.Length && !a[i + 1].StartsWith("-")) { var kv = a[i + 1].Split(':'); t.map = kv[0]; t.mode = kv.Length > 1 ? kv[1] : "mata"; }
        }
        string map = "vila_jardim", mode = "mata";
        static void Log(string s) => Debug.Log("[FreeBala][voltamenu] " + s);
        static IEnumerator Wait(float s) { for (float t = 0; t < s; t += Time.unscaledDeltaTime) yield return null; }
        IEnumerator Shot(string dir, string name) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name)); yield return null; yield return null; Log("print " + name); }
        static void State(string when)
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset; var cam = Camera.main; var cd = cam ? cam.GetUniversalAdditionalCameraData() : null;
            Log($"{when}: névoa {Shader.GetGlobalVector("_FBFogParams")} · volumétrica cena {FBVolumetric.InScene} · escala {(urp ? urp.renderScale : -1):0.00} · msaa {(urp ? urp.msaaSampleCount : -1)} · AA câmera {(cd ? cd.antialiasing.ToString() : "-")} · fog Unity {RenderSettings.fog} · LOD_FADE {Shader.IsKeywordEnabled("LOD_FADE_CROSSFADE")}");
        }
        IEnumerator Start()
        {
            var dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste"); System.IO.Directory.CreateDirectory(dir);
            while (!FBMenu.I) yield return null; yield return Wait(4);
            State("menu na abertura"); yield return Shot(dir, "volta_menu_1_abertura.png");
            GameData.Load(); FBSession.Load(); var keepMap = FBSession.map; var keepMode = FBSession.mode;
            FBSession.map = map; FBSession.mode = mode; FBSession.online = false; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_" + map); while (op != null && !op.isDone) yield return null;
            while (!(FBMatch.I && FBMatch.Player && FBMatch.I.state == "play")) yield return null;
            yield return Wait(15); State("na partida");
            var menu0 = FBMenu.I; FBMatch.I.ToMenu();
            while (!FBMenu.I || FBMenu.I == menu0) yield return null; yield return Wait(4);
            State("menu depois da partida"); yield return Shot(dir, "volta_menu_2_depois.png");
            FBSession.map = keepMap; FBSession.mode = keepMode; FBSession.Save();
            Log("fim"); Application.Quit(0);
        }
    }
}
