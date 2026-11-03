// Colisão dos props (Editor/FBPropColliders): em cada mapa, pega até 3 postes/árvores/grades (filhos "FBCol_*"), põe uma cápsula de
// CharacterController do tamanho do jogador a 1,6 m e anda direto contra o colisor a 4 m/s por 1,2 s (mesma física do jogador, FBMotor).
// Falha se a cápsula atravessar (chegar perto do centro do poste/tronco ou passar para o outro lado). Só vale depois de reimportar os mapas
// com o FBImport chamando FBPropColliders.Apply.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FreeBala.Tests
{
    public class FBPropCollisionTests
    {
        static readonly string[] Maps = { "vila_jardim", "praca_aguas", "praca_pt", "orla", "estande" };

        [UnityTest, Timeout(600000)]
        public IEnumerator Props_NaoAtravessa()
        {
            var bad = new List<string>(); int tested = 0;
            foreach (var map in Maps)
            {
                FBSession.Load(); FBSession.map = map; FBSession.mode = "role"; FBSession.online = false; FBSession.Save();
                var op = SceneManager.LoadSceneAsync("Map_" + map); if (op == null) { Debug.Log($"[FreeBala][props] {map}: cena não existe"); continue; }
                while (!op.isDone) yield return null;
                for (float t = 0; t < 1.5f; t += Time.unscaledDeltaTime) yield return null;
                if (FBMatch.Player) FBMatch.Player.gameObject.SetActive(false);                       // o jogador não entra no caminho
                var cols = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Where(c => c.name.StartsWith("FBCol_") && (c is CapsuleCollider || c.name.Contains("fence"))).ToList();
                var pick = new List<Collider>();
                foreach (var kind in new[] { "FBCol_pole", "FBCol_tree", "FBCol_fence" }) { var c = cols.Where(x => x.name == kind).OrderBy(x => x.transform.position.sqrMagnitude).FirstOrDefault(); if (c) pick.Add(c); }
                foreach (var c in cols) { if (pick.Count >= 3) break; if (!pick.Contains(c)) pick.Add(c); }
                Debug.Log($"[FreeBala][props] {map}: {cols.Count} colisores de prop (poste/árvore/grade) · testando {pick.Count}");
                foreach (var col in pick)
                {
                    var b = col.bounds; var center = new Vector3(b.center.x, b.min.y, b.center.z);
                    // lado com chão livre a 1,6 m (+ meia largura do colisor)
                    Vector3? start = null; Vector3 dir = Vector3.zero; float half = Mathf.Max(b.extents.x, b.extents.z);
                    foreach (var d in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
                    {
                        var s = center - d * (half + 1.6f); var gy = FBWorld.GroundY(s.x, center.y + 1.5f, s.z, 1, 4); if (gy == null || Mathf.Abs(gy.Value - center.y) > .6f) continue;
                        s.y = gy.Value + .05f; if (Physics.CheckCapsule(s + Vector3.up * .4f, s + Vector3.up * 1.5f, .33f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore)) continue;
                        start = s; dir = d; break;
                    }
                    if (start == null) { Debug.Log($"[FreeBala][props] {map}: {col.transform.parent?.name} sem lado livre — pulado"); continue; }
                    var g = new GameObject("CapsulaTeste"); g.layer = FBWorld.LChar; g.transform.position = start.Value;
                    var cc = g.AddComponent<CharacterController>(); cc.height = 1.8f; cc.radius = .32f; cc.center = new Vector3(0, .9f, 0); cc.stepOffset = .45f; cc.slopeLimit = 50; cc.skinWidth = .04f; cc.minMoveDistance = 0;
                    bool ground = true; float vy = 0, slide = 0, minD = 1e9f, side0 = Vector3.Dot(start.Value - center, dir);
                    for (float t = 0; t < 1.2f; t += Time.deltaTime)
                    {
                        float dt = Mathf.Min(Time.deltaTime, 1 / 30f); vy = ground && vy < 0 ? -2 : vy - 35 * dt;
                        FBMotor.Move(cc, (dir * 4 + Vector3.up * vy) * dt, ground, vy > 0, .45f, .45f, ref slide, out ground, out _);
                        minD = Mathf.Min(minD, (g.transform.position - center).Flat().magnitude); yield return null;
                    }
                    float side1 = Vector3.Dot(g.transform.position - center, dir); tested++;
                    string who = $"{map}/{col.transform.parent?.name} ({col.name})";
                    Debug.Log($"[FreeBala][props] {who}: menor distância ao centro {minD:0.00} m, lado {side0:0.00} → {side1:0.00}");
                    float lim = col is CapsuleCollider ? .3f : .15f;
                    if (side1 > 0 && side0 < 0) bad.Add($"{who}: atravessou (passou para o outro lado)");
                    else if (col is CapsuleCollider && minD < lim) bad.Add($"{who}: entrou no colisor ({minD:0.00} m do centro)");
                    Object.Destroy(g); yield return null;
                }
            }
            if (tested == 0) Assert.Ignore("nenhum colisor FBCol_* nos mapas — reimportar com FBImport chamando FBPropColliders.Apply");
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }
    }
}
