// Vitrine automática dos efeitos de combate (-fbfx), para validar sangue/decals/impactos sem jogar:
//   FreeBala.exe -fbauto vila_jardim:treino 60 -fbfx
// Quando a partida carregada pelo FBAutoTest está pronta, acha uma parede perto do jogador, põe um boneco (sem IA) na frente dela,
// dispara acertos sintéticos pelo barramento (FBDamageEvents — o mesmo caminho do FBCombat), cria peças de teste por material e grava
// <build>/autoteste/fx_*.png com uma câmera própria (HUD escondida). Só efeito visual; o boneco morre no print da poça.
using System.Collections;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public class FBFxShowcase : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbfx") < 0) return;
            var g = new GameObject("FB_FxVitrine"); DontDestroyOnLoad(g); g.AddComponent<FBFxShowcase>();
        }

        string dir; Camera cam; int shots;
        IEnumerator Start()
        {
            dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste"); System.IO.Directory.CreateDirectory(dir);
            while (!(FBMatch.I && FBMatch.Player && FBMatch.Player.alive)) yield return null;
            for (float w = 0; w < 6; w += Time.unscaledDeltaTime) yield return null;          // aquecimento (o FBAutoTest espera 4 s)
            Debug.Log("[FreeBala][fx] vitrine começando · projetor URP " + FBDecalFx.Projector + " · nível FX " + FBFxQuality.Level);
            var canv = FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.enabled).ToList(); foreach (var c in canv) c.enabled = false;
            var cg = new GameObject("FxCam"); cam = cg.AddComponent<Camera>(); cam.depth = 100; cam.fieldOfView = 50; cam.nearClipPlane = .03f; if (Camera.main) cam.cullingMask = Camera.main.cullingMask;
            yield return Run();
            foreach (var c in canv) if (c) c.enabled = true; Destroy(cg);
            Debug.Log($"[FreeBala][fx] vitrine fim: {shots} prints · decals {FBDecalFx.ActiveCount}/{FBDecalFx.Capacity} · ferimentos {FBWoundFx.ActiveCount}/{FBWoundFx.Capacity}");
        }

        IEnumerator Snap(string name)
        {
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "fx_" + name + ".png")); shots++; yield return null; yield return null;
        }
        void Look(Vector3 from, Vector3 at) { cam.transform.position = from; cam.transform.LookAt(at); }
        static IEnumerator Wait(float s) { for (float t = 0; t < s; t += Time.deltaTime) yield return null; }

        // parede vertical a 2–6 m de um ponto aberto (jogador, depois pontos do mapa), com chão na frente
        static bool FindWall(out Vector3 wp, out Vector3 n)
        {
            var origins = new System.Collections.Generic.List<Vector3> { FBMatch.Player.Pos }; if (FBMatch.I.Map != null) origins.AddRange(FBMatch.I.Map.points.Take(12));
            foreach (var o in origins)
                for (int i = 0; i < 24; i++)
                {
                    float a = i * Mathf.PI * 2 / 24; var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                    if (!Physics.Raycast(o + Vector3.up * 1.3f, d, out var h, 6, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) || h.distance < 2.2f || Mathf.Abs(h.normal.y) > .25f) continue;
                    var nn = new Vector3(h.normal.x, 0, h.normal.z).normalized; var stand = h.point + nn * 1.3f;
                    if (FBWorld.GroundY(stand.x, stand.y, stand.z, .5f, 2.5f) is float gy && Mathf.Abs(gy - o.y) < 1.2f && !FBWorld.RayHit(stand + Vector3.up * 1.3f, nn, 7))
                    { wp = new Vector3(h.point.x, gy + 1.3f, h.point.z); n = nn; return true; }
                }
            wp = default; n = default; return false;
        }
        static string Wpn(string cls) => GameData.Weapons.Values.FirstOrDefault(w => w.cls == cls)?.id;

        static Vector3 Hit(FBCharacter v, string hbName, string cls, Vector3 dir, float dmg = 30)
        {
            var hb = v.GetComponentsInChildren<FBHitbox>(true).FirstOrDefault(h => h.name == hbName); if (!hb) return v.Pos;
            var col = hb.GetComponent<Collider>(); var p = col.ClosestPoint(col.bounds.center - dir * 2);
            FBDamageEvents.Raise(new FBDamage { victim = v, source = FBMatch.Player, type = FBDamageType.Bullet, amount = dmg, applied = dmg, hpBefore = v.hp, hpAfter = v.hp, head = hb.head,
                hit = new FBHitInfo { valid = true, point = p, normal = -dir, dir = dir, part = hb.head ? "head" : hb.part, weaponId = Wpn(cls), dist = 10 } });
            return p;
        }

        IEnumerator Run()
        {
            if (!FindWall(out var wall, out var n)) { Debug.Log("[FreeBala][fx] FALHA: nenhuma parede livre perto do jogador/pontos do mapa"); yield break; }
            var t = Vector3.Cross(Vector3.up, n).normalized; var gy = wall.y - 1.3f;
            var spot = new Vector3(wall.x, gy, wall.z) + n * 1.3f;
            var ids = FBRegistry.I.charIds; var v = FBMatch.I.Spawn(ids[Mathf.Min(1, ids.Count - 1)], null, 1, spot, false);
            FBMatch.Teleport(v, spot, Quaternion.LookRotation(n).eulerAngles.y); v.combat = false;
            for (int i = 0; i < 20; i++) yield return null;
            var chest = v.Chest ? v.Chest.position : spot + Vector3.up * 1.3f; var dirIn = -n;          // bala vem de frente e segue para a parede

            // 1. fuzil no tronco: jato saindo pelas costas e mancha na parede
            Look(chest + t * 2.4f + n * .6f + Vector3.up * .15f, (chest + wall) * .5f);
            Hit(v, "HB_Spine", "fuzil", dirIn, 34); yield return Wait(.05f); yield return Snap("1_tronco_jato");
            yield return Wait(1f); yield return Snap("1_tronco_parede");
            // 2. headshot de sniper
            var head = v.Head ? v.Head.position : chest + Vector3.up * .5f;
            Look(head + t * 1.6f + n * .7f, head - n * .3f);
            Hit(v, "HB_Cabeca", "sniper", dirIn, 95); yield return Wait(.06f); yield return Snap("2_sniper_cabeca");
            yield return Wait(1.2f); Look(head + n * 1.4f + t * .5f, head); yield return Snap("2_sniper_ferimento");
            // 3. braço e perna: ferimento preso no membro (dois quadros diferentes)
            Hit(v, "HB_LeftUpperArm", "pistola", dirIn, 20); Hit(v, "HB_RightUpperLeg", "submetralhadora", dirIn, 20);
            yield return Wait(.6f); Look(chest + n * 1.5f + Vector3.down * .35f, chest + Vector3.down * .45f); yield return Snap("3_membros_a");
            yield return Wait(1.3f); yield return Snap("3_membros_b");
            // 4. rajada rasante na parede: respingo alongado + escorrido
            var w4 = wall + t * 2.2f;
            for (int i = 0; i < 5; i++) FBBloodFx.SceneSplat(w4 + n * .7f + t * (i * .35f - .7f) + Vector3.up * (i % 2 * .25f), (t * .85f - n * .5f).normalized, 1.2f, 1.2f);
            for (int i = 0; i < 2; i++) FBBloodFx.SceneSplat(w4 + n * .9f + t * (.9f + i * .4f), (-n + Vector3.down * .1f).normalized, 1.3f, 1.4f);
            yield return Wait(.3f); Look(w4 + n * 2.4f + Vector3.up * .2f, w4); yield return Snap("4_parede_rasante");
            // 5. impacto por material: peças de teste na frente da parede (nome = material) + água no chão
            var w5 = wall - t * 2.4f; string[] mats = { "fx_concreto", "fx_metal_chapa", "fx_madeira", "fx_terra", "fx_vidro" };
            var pieces = mats.Select((m, i) => { var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = m; g.layer = FBWorld.LMap; g.transform.position = w5 + n * .45f + t * ((i - 2) * .6f) + Vector3.down * .3f; g.transform.rotation = Quaternion.LookRotation(n); g.transform.localScale = new Vector3(.5f, .9f, .4f); return g; }).ToList();
            yield return null; Physics.SyncTransforms();
            Look(w5 + n * 2.6f + Vector3.up * .25f, w5 + n * .4f + Vector3.down * .3f);
            for (int k = 0; k < 3; k++)
            {
                foreach (var g in pieces) { var col = g.GetComponent<Collider>(); var face = g.transform.position + n * .2f + g.transform.right * Random.Range(-.15f, .15f) + Vector3.up * Random.Range(-.3f, .3f); FBImpactFx.Surface(face, n, col, -1, -n); }
                yield return Wait(k == 2 ? .07f : .2f);
            }
            yield return Snap("5_materiais_particulas"); yield return Wait(1.4f); yield return Snap("5_materiais_buracos");
            var wpos = new Vector3(w5.x, gy, w5.z) + n * 1.8f; FBFx.Splash(wpos + Vector3.up * .02f, 1); yield return Wait(.12f);
            Look(wpos + n * 2 + Vector3.up * 1.1f, wpos); yield return Snap("5_agua");
            // 6. corpo caído: poça crescida e escurecendo
            FBCombat.DamageActor(v, 999, false, FBMatch.Player); yield return Wait(7f);
            var hips = v.Hips ? v.Hips.position : spot; Look(hips + n * 2.2f + t * 1.2f + Vector3.up * 1.8f, hips); yield return Snap("6_poca");
            // 7. explosão a ~9 m
            var ep = new Vector3(spot.x, gy, spot.z) + n * 5 - t * 3; var eye = ep + n * 9 + Vector3.up * 2.5f;
            FBFx.Explosion(ep + Vector3.up * .3f, 4); Look(eye, ep + Vector3.up * 1.5f); yield return Wait(.12f); yield return Snap("7_explosao_fogo");
            yield return Wait(1.6f); yield return Snap("7_explosao_fumaca"); yield return Wait(3f); Look(ep + n * 3 + Vector3.up * 3, ep); yield return Snap("7_queimado");
            // 8. cartuchos e fumaça do disparo (jogador em 3ª pessoa; 1ª pessoa: conferir jogando)
            var pl = FBMatch.Player; var mz = pl.MuzzlePos;
            Look(mz + pl.transform.right * 1.3f + pl.transform.forward * .4f + Vector3.up * .15f, mz - pl.transform.forward * .3f);
            for (int i = 0; i < 6; i++) { FBFx.Muzzle(pl.MuzzlePos, .55f, "fuzil"); yield return Wait(.09f); if (i == 3) yield return Snap("8_cartuchos"); }
            yield return Wait(.5f); yield return Snap("8_fumaca_cano");
            // 9. quina: decal de sangue no canto da parede (quadrado encolhe/desiste; projetor dobra a quina)
            var edge = FindEdge(wall, n, t);
            if (edge is Vector3 e) { FBBloodFx.SceneSplat(e + n * .8f, -n, 1.2f, 1.6f); FBDecalFx.Place(FBDecalFx.Atlas.Blood, FBDecalFx.CellRound, e, n, .6f); yield return Wait(.2f); Look(e + n * 1.3f + t * .8f, e); yield return Snap("9_quina"); }
            else Debug.Log("[FreeBala][fx] sem quina perto da parede para o print 9");
        }
        // ponto da parede a até 4 m para os lados onde ela acaba (quina/beirada)
        static Vector3? FindEdge(Vector3 wall, Vector3 n, Vector3 t)
        {
            foreach (var s in new[] { 1f, -1f })
                for (float d = .2f; d < 4; d += .1f)
                    if (!Physics.Raycast(wall + t * s * d + n * .3f, -n, .6f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore)) return wall + t * s * (d - .15f);
            return null;
        }
    }
}
