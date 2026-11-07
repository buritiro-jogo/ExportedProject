// LABORATÓRIO DA 1ª PESSOA (rodada 42): FreeBala.exe -fbfplab [skins|padrao] [-fbfpold]
// O -fbanimlab só olha o corpo em 3ª pessoa; os braços da 1ª pessoa são outra montagem (FBCharArms: malha do braço do personagem
// seguindo o rig LPSP). Aqui, no estande, para cada personagem e cada classe de arma (fuzil, SMG, pistola, escopeta, sniper):
//   parado · mirando · recarregando · correndo — print da câmera do jogo + VISTA DE LADO só da camada da 1ª pessoa (o braço de fora,
//   onde cotovelo/punho/ombro torcidos aparecem) em autoteste/fplab/<skin>_<arma>_<estado>_{cam,lado}.png
// e mede por braço, em graus, QUANTO A VERSÃO ANTIGA DO FBCharArms TORCIA cada osso em relação à atual
// (FBCharArms.Step: Quaternion.Angle(qUold, qU) e (qFold, qF)) -> [FreeBala][fplab].
// ATENÇÃO (r60): isto NÃO é a torção anatômica do braço, e o cabeçalho antigo dizia que era ("o anatômico é 0 no braço e até
// ~90 no antebraço"). Número ALTO aqui significa que a correção da r42 está trabalhando MUITO naquele caso — não que o braço
// esteja torto. Para julgar se o braço está certo, olhe a VISTA DE LADO (*_lado.png); foi assim que se viu, em 07/11, que os
// "piores casos" (r_mikele/barret_50 102°, r_pm_silva/38 175°) estão anatomicamente corretos.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBFpLab : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbfplab"); if (i < 0) return;
            var g = new GameObject("FB_FpLab"); DontDestroyOnLoad(g); var t = g.AddComponent<FBFpLab>();
            var s = i + 1 < a.Length && !a[i + 1].StartsWith("-") ? a[i + 1] : "padrao";
            t.skins = s == "padrao" ? new[] { "r_tutu", "r_elaine", "r_giga", "r_pm_silva", "r_vivi", "r_miguel", "r_aka_junin", "r_mikele" } : s.Split(',');
            FBCharArms.Legacy = System.Array.IndexOf(a, "-fbfpold") >= 0;
        }
        string[] skins; string dir;
        static void Log(string s) => Debug.Log("[FreeBala][fplab] " + s);
        IEnumerator Wait(float s) { for (float w = 0; w < s; w += Time.deltaTime) yield return null; }

        IEnumerator Start()
        {
            dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste", "fplab"); System.IO.Directory.CreateDirectory(dir);
            yield return Wait(3);
            GameData.Load(); FBSession.Load(); var keep = (FBSession.map, FBSession.mode, FBSession.skin, FBSession.view);
            var ids = new List<string>();
            foreach (var cls in new[] { "fuzil", "submetralhadora", "pistola", "escopeta", "sniper" })
            { var id = GameData.Weapons.Values.Where(x => x.cls == cls && FBRegistry.I.Weapon(x.id)).Select(x => x.id).FirstOrDefault(); if (id != null) ids.Add(id); }
            Log($"armas {string.Join(",", ids)} · modo {(FBCharArms.Legacy ? "ANTIGO (torção pela mão)" : "novo")}");
            foreach (var sk in skins)
            {
                FBSession.map = "vila_jardim"; FBSession.mode = "classico"; FBSession.skin = sk; FBSession.online = false; FBSession.view = "fp"; FBSession.Save();
                var op = SceneManager.LoadSceneAsync("Map_vila_jardim"); while (op != null && !op.isDone) yield return null;
                for (float w = 0; w < 25 && !(FBPlayer.I && FBPlayer.I.wu); w += Time.deltaTime) yield return null;
                var P = FBPlayer.I; if (!P || !P.wu) { Log($"{sk}: sem jogador"); continue; }
                yield return Wait(2); FBMatch.God = true; foreach (var b in FBMatch.Bots.ToList()) if (b) b.gameObject.SetActive(false);
                bool aim = false, run = false, rel = false, tv = !P.firstPerson;
                FBPlayer.TestInput = inp => { inp.toggleView = tv; tv = false; inp.aim = aim; inp.aimPressed = false; inp.fire = inp.firePressed = false; inp.move = run ? new Vector2(0, 1) : Vector2.zero; inp.sprintHeld = run; inp.look = Vector2.zero; inp.reload = rel; rel = false; return inp; };
                yield return Wait(.5f);
                var side = new GameObject("FpSide").AddComponent<Camera>(); side.depth = 200; side.cullingMask = 1 << FBWorld.LFirstPerson; side.clearFlags = CameraClearFlags.SolidColor;
                side.backgroundColor = new Color(.35f, .38f, .42f); side.nearClipPlane = .01f; side.fieldOfView = 40; side.enabled = false;
                foreach (var id in ids)
                {
                    P.wu.Strip(); P.wu.Give(id); int sl = System.Array.FindIndex(P.wu.slots, x => x != null && x.id == id); P.wu.Equip(sl >= 0 ? sl : 0, true);
                    yield return Wait(1.3f);
                    foreach (var st in new[] { "parado", "mirando", "recarga", "correndo" })
                    {
                        aim = st == "mirando"; run = st == "correndo"; if (st == "recarga") rel = true;
                        yield return Wait(st == "recarga" ? .9f : 1.1f);
                        var arms = P.GetComponentInChildren<FBCharArms>(true) ?? FindAnyObjectByType<FBCharArms>();
                        string tw = arms ? arms.TwistReport() : "sem braços do personagem";
                        Log($"{sk} {id} {st}: {tw}");
                        yield return Snap($"{sk}_{id}_{st}_cam.png", null);
                        // vista de lado (de fora do braço direito), só a camada da 1ª pessoa
                        var mc = P.cam ? P.cam.transform : Camera.main.transform;
                        side.transform.position = mc.position + mc.right * .95f + mc.forward * .25f + mc.up * .05f; side.transform.LookAt(mc.position + mc.forward * .35f - mc.up * .12f);
                        yield return Snap($"{sk}_{id}_{st}_lado.png", side);
                        run = false; aim = false;
                    }
                }
                Destroy(side.gameObject); FBPlayer.TestInput = null;
            }
            (FBSession.map, FBSession.mode, FBSession.skin, FBSession.view) = keep; FBSession.Save();
            Log("fim"); yield return Wait(1); Application.Quit();
        }
        IEnumerator Snap(string f, Camera only)
        {
            if (only) only.enabled = true; yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, f)); yield return null; yield return null;
            if (only) only.enabled = false;
        }
    }
}
