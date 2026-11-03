// Diagnóstico (não falha): mede no jogo o que só dá para ver rodando — locomoção (velocidade no chão, clipes tocando, ritmo) desarmado e armado,
// andando e correndo; e o tamanho dos colisores dos veículos/props em relação ao modelo visível. Saída: linhas [FreeBala][diag] no log.
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FreeBala.Tests
{
    public class FBDiagTests
    {
        [UnityTest, Timeout(240000), Category("Diag")]
        public IEnumerator Diag_Locomocao_Colisores()
        {
            LogAssert.ignoreFailingMessages = true;
            FBSession.Load(); FBSession.map = "vila_jardim"; FBSession.mode = "treino"; FBSession.view = "tps"; FBSession.holdRun = true; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_vila_jardim"); while (!op.isDone) yield return null;
            for (float t = 0; t < 3; t += Time.unscaledDeltaTime) yield return null;
            var P = FBMatch.Player; Assert.IsNotNull(P); var an = P.an; an.cullingMode = AnimatorCullingMode.AlwaysAnimate; var pl = FBPlayer.I;
            IEnumerator Measure(string tag, bool armed, bool run)
            {
                if (!armed) pl.wu.Equip(FBWeaponUser.HANDS, true); else pl.wu.Equip(0, true);
                FBInput.TestMove = new Vector2(0, 1); FBInput.TestSprint = run; var p0 = P.Pos; float t0 = Time.time;
                for (float t = 0; t < 2.5f; t += Time.deltaTime) yield return null;
                float g = (P.Pos - p0).Flat().magnitude / (Time.time - t0);
                var ci = an.GetCurrentAnimatorClipInfo(0); string clips = string.Join(", ", ci.Select(c => $"{c.clip.name}×{c.weight:0.00}(rs {FBCharacter.RootSpeedOf(c.clip):0.00})"));
                Debug.Log($"[FreeBala][diag] {tag}: chão {g:0.00} m/s · ritmo {P.LocoRate:0.00} · sprint {P.sprint} · clipes {clips} · humanScale {an.humanScale:0.00} escala {P.transform.lossyScale.y:0.00}");
                FBInput.TestMove = null; FBInput.TestSprint = false; for (float t = 0; t < .8f; t += Time.deltaTime) yield return null;
            }
            yield return Measure("desarmado andando", false, false); yield return Measure("desarmado correndo", false, true);
            yield return Measure("armado andando", true, false); yield return Measure("armado correndo", true, true);
            foreach (var car in FBVehicles.list.Where(c => c).Take(6))
            {
                var vb = new Bounds(car.transform.position, Vector3.zero); bool f = true; foreach (var r in car.GetComponentsInChildren<Renderer>()) { if (!r.enabled || r.name.Contains("sombra")) continue; if (f) { vb = r.bounds; f = false; } else vb.Encapsulate(r.bounds); }
                var cb = new Bounds(car.transform.position, Vector3.zero); f = true; foreach (var c in car.GetComponentsInChildren<Collider>()) { if (!c.enabled) continue; if (f) { cb = c.bounds; f = false; } else cb.Encapsulate(c.bounds); }
                var kinds = string.Join(",", car.GetComponentsInChildren<Collider>().GroupBy(c => c.GetType().Name).Select(g2 => g2.Key + "×" + g2.Count()));
                Debug.Log($"[FreeBala][diag] veículo {car.id}: visual {vb.size.x:0.00}×{vb.size.y:0.00}×{vb.size.z:0.00} · colisor {cb.size.x:0.00}×{cb.size.y:0.00}×{cb.size.z:0.00} · {kinds}");
            }
            int n = 0; foreach (var c in Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None).Where(c => c.name.StartsWith("FBCol_")).Take(400))
            {
                var p = c.transform.parent; if (!p) continue; var rs = p.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue; var vb = rs[0].bounds; foreach (var r in rs) vb.Encapsulate(r.bounds);
                float ratio = c.bounds.size.magnitude / Mathf.Max(.01f, vb.size.magnitude); if (ratio > 1.3f && n++ < 15) Debug.Log($"[FreeBala][diag] prop {p.name}: colisor {ratio:0.00}× o visual ({c.bounds.size} vs {vb.size})");
            }
            Debug.Log($"[FreeBala][diag] props com colisor > 1,3× o visual: {n}");
        }
    }
}
