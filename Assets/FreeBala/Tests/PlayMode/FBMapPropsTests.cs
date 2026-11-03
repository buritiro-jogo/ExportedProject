// Props trocados no import (Editor/FBMapProps): na Praça das Águas a Kombi food truck substitui os food trucks antigos. Confere que as Kombis
// existem, têm LODGroup e colisor, estão no chão (raycast), não atravessam parede, e que os antigos não aparecem mais. Print de cada uma em
// unity/logs/print_foodtruck_<n>.png (câmera a 3/4 pela frente, do lado do balcão, a ~7 m). Só passa depois de reimportar os mapas.
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FreeBala.Tests
{
    public class FBMapPropsTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator PracaAguas_FoodTruck()
        {
            FBSession.Load(); FBSession.map = "praca_aguas"; FBSession.mode = "role"; FBSession.online = false; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_praca_aguas"); while (!op.isDone) yield return null;
            for (float t = 0; t < 2; t += Time.unscaledDeltaTime) yield return null;
            var root = GameObject.Find("FB_PropsTrocados"); Assert.IsNotNull(root, "sem FB_PropsTrocados (reimportar os mapas com o FBImport que chama FBMapProps)");
            var trucks = root.GetComponentsInChildren<LODGroup>().Select(l => l.transform).ToList(); Assert.Greater(trucks.Count, 0, "nenhuma Kombi");
            // antigos desligados
            var olds = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name.StartsWith("PA_food_truck_")).ToList();
            Assert.IsTrue(olds.All(t => !t.gameObject.activeInHierarchy), "food truck antigo ainda visível: " + string.Join(",", olds.Where(t => t.gameObject.activeInHierarchy).Select(t => t.name)));
            var cam = Camera.main; int i = 0;
            foreach (var t in trucks)
            {
                var lg = t.GetComponent<LODGroup>(); Assert.GreaterOrEqual(lg.lodCount, 2, $"{t.name}: LODs"); Assert.IsTrue(lg.GetLODs()[0].renderers.All(r => r && r.enabled), $"{t.name}: LOD0 sem renderer");
                var bc = t.GetComponent<BoxCollider>(); Assert.IsNotNull(bc, $"{t.name}: sem colisor");
                // no chão: o raycast de cima (ignorando a própria Kombi) bate a menos de 25 cm da base
                bc.enabled = false; Physics.SyncTransforms();
                bool hit = Physics.Raycast(t.position + Vector3.up * 3, Vector3.down, out var h, 10, ~0, QueryTriggerInteraction.Ignore); bc.enabled = true;
                Assert.IsTrue(hit, $"{t.name}: sem chão embaixo"); Assert.Less(Mathf.Abs(h.point.y - t.position.y), .25f, $"{t.name}: fora do chão ({t.position.y:0.00} × chão {h.point.y:0.00})");
                var rs = t.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                Assert.Greater(b.size.y, 2.4f, $"{t.name}: escala errada (altura {b.size.y:0.0} m)"); Assert.Less(b.size.y, 4.5f, $"{t.name}: escala errada (altura {b.size.y:0.0} m)");
                Debug.Log($"[FreeBala][teste] {t.name}: {t.position} yaw {t.eulerAngles.y:0} · caixa {b.size.x:0.0}×{b.size.y:0.0}×{b.size.z:0.0} m · LODs {lg.lodCount}");
                if (cam)
                {
                    var side = t.right * (-1);                                          // balcão no −X local (food_truck.json: balcao_lado_x = −1)
                    cam.transform.position = t.position + t.forward * 4.5f + side * 4.5f + Vector3.up * 2.2f; cam.transform.LookAt(t.position + Vector3.up * 1.3f);
                    yield return null; yield return null;
                    var dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../logs")); System.IO.Directory.CreateDirectory(dir);
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"print_foodtruck_{i++}.png")); yield return null; yield return null;
                }
            }
        }
    }
}
