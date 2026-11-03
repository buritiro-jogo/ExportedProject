// Vitrine das PISTOLAS (rodada 38, pedido "corrigir completamente as animações e os rigs de segurar pistolas, em primeira e terceira
// pessoa"): FreeBala.exe -fbauto estande:treino 240 -fbpistola
// Numa partida carregada pelo FBAutoTest, para cada pistola: 1ª pessoa (parado, mirando, andando, recarregando) com a visão do próprio
// jogador, e 3ª pessoa de perto por uma câmera extra (frente 3/4 e lado esquerdo; parado, mirando, andando). Prints em
// <build>/autoteste/pist_<arma>_<vista>_<estado>.png e no log a posição das mãos em relação à arma (mão esquerda × punho da pistola).
using System.Collections;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public class FBPistolShowcase : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbpistola") < 0) return;
            var g = new GameObject("FB_VitrinePistolas"); DontDestroyOnLoad(g); g.AddComponent<FBPistolShowcase>();
        }
        string dir; Camera extra;
        static IEnumerator Wait(float s) { for (float t = 0; t < s; t += Time.unscaledDeltaTime) yield return null; }
        IEnumerator Snap(string name) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name + ".png")); yield return null; yield return null; }
        IEnumerator Start()
        {
            dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste"); System.IO.Directory.CreateDirectory(dir);
            while (!(FBMatch.I && FBMatch.Player && FBMatch.Player.alive && FBPlayer.I && FBMatch.I.state == "play")) yield return null;
            yield return Wait(6);
            var me = FBMatch.Player; var pl = FBPlayer.I; FBMatch.God = true; FBCamLab.Install(); var wu = me.GetComponent<FBWeaponUser>();
            var p0 = me.Pos; float yaw = me.Yaw;
            var ids = GameData.Weapons.Values.Where(w => w.cls == "pistola" && FBRegistry.I.Weapon(w.id)).Select(w => w.id).ToList();
            Debug.Log("[FreeBala][pistola] vitrine: " + string.Join(", ", ids));
            // rodada 39: fuzil em 1ª pessoa — mãos do rig original e espreitar Q/E (a arma tem que inclinar junto com a câmera)
            var rifle = GameData.Weapons.Values.Where(w => w.cls == "fuzil" && FBRegistry.I.Weapon(w.id)).Select(w => w.id).FirstOrDefault();
            if (rifle != null)
            {
                wu.Strip(); wu.Give(rifle); yield return Wait(1.4f); pl.ForceFirstPerson(); FBCamLab.Teleport(me, p0, yaw); pl.camPitch = 0; yield return Wait(3.0f);
                yield return Snap("fp_fuzil_parado");
                FBCamLab.leanL = true; yield return Wait(.9f); yield return Snap("fp_fuzil_inclina_q"); FBCamLab.leanL = false; yield return Wait(.6f);
                FBCamLab.leanR = true; yield return Wait(.9f); yield return Snap("fp_fuzil_inclina_e"); FBCamLab.aim = true; yield return Wait(.8f); yield return Snap("fp_fuzil_inclina_e_mira"); FBCamLab.aim = FBCamLab.leanR = false; yield return Wait(.6f);
            }
            foreach (var id in ids)
            {
                wu.Strip(); wu.Give(id); yield return Wait(1.4f);
                // ---- 1ª pessoa
                pl.ForceFirstPerson(); FBCamLab.Teleport(me, p0, yaw); pl.camPitch = 0; yield return Wait(3.0f);   // o saque do revólver (RC425) dura 2,3 s
                yield return Snap($"pist_{id}_fp_parado");
                if (id == ids[0]) { FBCamLab.leanL = true; yield return Wait(.9f); yield return Snap($"pist_{id}_fp_inclina_q"); FBCamLab.leanL = false; yield return Wait(.6f); }
                FBCamLab.aim = true; yield return Wait(.9f); yield return Snap($"pist_{id}_fp_mira"); FBCamLab.aim = false; yield return Wait(.5f);
                FBCamLab.move = new Vector2(0, 1); yield return Wait(.8f); yield return Snap($"pist_{id}_fp_andando"); FBCamLab.move = Vector2.zero; yield return Wait(.4f);
                // ---- 3ª pessoa de perto (câmera extra; a do jogador continua mandando na mira)
                pl.ForceThirdPerson(); FBCamLab.Teleport(me, p0, yaw); yield return Wait(1.2f);
                if (!extra) { extra = new GameObject("CamPistola").AddComponent<Camera>(); extra.depth = 50; extra.fieldOfView = 32; extra.nearClipPlane = .05f; }
                extra.cullingMask = Camera.main ? Camera.main.cullingMask & ~(1 << FBWorld.LFirstPerson) : ~0; extra.enabled = true;
                foreach (var (st, aim, move) in new[] { ("parado", false, Vector2.zero), ("mira", true, Vector2.zero), ("andando", false, new Vector2(0, 1)) })
                {
                    FBCamLab.Teleport(me, p0, yaw); FBCamLab.aim = aim; FBCamLab.move = move; yield return Wait(move.sqrMagnitude > 0 ? .7f : 1.1f);
                    foreach (var (vw, ang) in new[] { ("frente", 40f), ("lado", -90f), ("costas", 160f) })
                    {
                        var c = me.Chest ? me.Chest.position : me.Pos + Vector3.up * 1.3f * me.ScaleK;
                        extra.transform.position = c + Quaternion.Euler(0, me.Yaw + ang, 0) * Vector3.forward * 1.9f * me.ScaleK + Vector3.up * .1f * me.ScaleK; extra.transform.LookAt(c - Vector3.up * .05f * me.ScaleK);
                        yield return Snap($"pist_{id}_tps_{vw}_{st}");
                    }
                    var rig = me.rig; var lh = me.LeftHand; var gp = me.weaponGO ? me.weaponGO.transform.position : Vector3.zero;
                    Debug.Log($"[FreeBala][pistola] {id} 3ª pessoa {st}: mão esquerda a {(lh && rig && rig.LeftTargetValid ? (lh.position - rig.LeftTarget).magnitude * 100 : -1):0.0} cm do alvo · mãos separadas {(lh && me.RightHand ? (lh.position - me.RightHand.position).magnitude * 100 : -1):0.0} cm · erro do cano {(me.hasAim ? Vector3.Angle(me.BarrelDir(), me.aimPoint - me.MuzzlePos) : -1):0.0}°");
                }
                FBCamLab.aim = false; FBCamLab.move = Vector2.zero; extra.enabled = false; yield return Wait(.3f);
            }
            FBCamLab.Remove(); if (extra) Destroy(extra.gameObject);
            Debug.Log("[FreeBala][pistola] vitrine fim");
        }
    }
}
