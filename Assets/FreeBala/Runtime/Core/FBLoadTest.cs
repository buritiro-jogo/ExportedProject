// Autoteste do CARREGAMENTO da partida (rodada 43, usuário: "o carregamento das partidas demora muito"):
//   FreeBala.exe -fbcarrega [mapa:modo] [vezes]
// Entra na partida pelo MESMO caminho do botão COMEÇAR (FBLoading: tela de carregamento, aquecimento), espera a tela sumir, joga 3 s,
// volta ao menu pelo caminho do SAIR (FBMatch.ToMenu) e repete — a 1ª vez é a fria (nada em memória), as outras mostram o que fica.
// Log: [FreeBala][carregamento] (fases, do FBLoading) e [FreeBala][carrega-teste] (relógio de parede do clique até jogar).
using System.Collections;
using UnityEngine;

namespace FreeBala
{
    public class FBLoadTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbcarrega"); if (i < 0) return;
            var g = new GameObject("FB_TesteCarrega"); DontDestroyOnLoad(g); var t = g.AddComponent<FBLoadTest>();
            if (i + 1 < a.Length && !a[i + 1].StartsWith("-")) { var kv = a[i + 1].Split(':'); t.map = kv[0]; t.mode = kv.Length > 1 ? kv[1] : "mata"; }
            if (i + 2 < a.Length && int.TryParse(a[i + 2], out var n)) t.times = Mathf.Clamp(n, 1, 6);
        }
        string map = "vila_jardim", mode = "role"; int times = 2;
        static void Log(string s) => Debug.Log("[FreeBala][carrega-teste] " + s);
        static IEnumerator Wait(float s) { for (float t = 0; t < s; t += Time.unscaledDeltaTime) yield return null; }
        IEnumerator Start()
        {
            while (!FBMenu.I) yield return null; yield return Wait(3);
            GameData.Load(); FBSession.Load(); var keepMap = FBSession.map; var keepMode = FBSession.mode;
            FBSession.map = map; FBSession.mode = mode; FBSession.online = false; FBSession.Save();
            var res = new System.Text.StringBuilder();
            for (int k = 1; k <= times; k++)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew(); long mem0 = System.GC.GetTotalMemory(false);
                FBLoading.Load("Map_" + map, map, mode);
                while (!FBLoading.Busy) yield return null; while (FBLoading.Busy) yield return null;
                float wall = (float)sw.Elapsed.TotalSeconds;
                Log($"{k}ª entrada {map}:{mode}: {wall:0.0} s do clique até jogar (fases somam {FBLoading.LastTotal:0.0} s) · memória gerenciada {(System.GC.GetTotalMemory(false) - mem0) / 1048576f:+0;-0} MB");
                res.Append($"{k}ª {wall:0.0} s · ");
                yield return Wait(3);
                if (k < times) { var menu0 = FBMenu.I; FBMatch.I.ToMenu(); while (!FBMenu.I || FBMenu.I == menu0) yield return null; yield return Wait(3); }
            }
            Log("fim · " + res);
            FBSession.map = keepMap; FBSession.mode = keepMode; FBSession.Save();
            yield return Wait(1); Application.Quit(0);
        }
    }
}
