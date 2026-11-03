// Testes dos efeitos de sangue/impacto (agente 6): FBDamage sintéticos pelo barramento (cabeça/tronco/braço/perna × pistola/fuzil/
// escopeta/sniper) → o ferimento fica preso no osso da região certa e acompanha a animação; decals alinhados à normal da superfície;
// pools (decals, ferimentos, partículas) nunca passam do limite; ferimentos somem ao renascer. Efeitos são só visuais.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FreeBala.Tests
{
    public class FBBloodFxTests
    {
        static GameObject Wall(Vector3 pos, Quaternion rot, Vector3 size)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.layer = FBWorld.LMap; g.transform.SetPositionAndRotation(pos, rot); g.transform.localScale = size; return g;
        }

        [UnityTest] public IEnumerator Decal_RespeitaNormal()
        {
            FBFx.Warm(); FBDecalFx.Clear();
            var rot = Quaternion.Euler(0, 35, 0) * Quaternion.Euler(-25, 0, 0);
            var w = Wall(new Vector3(0, 50, 0), rot, new Vector3(4, 4, .2f)); yield return null; Physics.SyncTransforms();
            var n = w.transform.rotation * Vector3.back; var face = w.transform.position + n * .1f;   // face -Z do cubo
            Assert.IsTrue(Physics.Raycast(face + n, -n, out var h, 2, FBWorld.SolidMask), "parede de teste sem colisor");
            foreach (var up in new Vector3?[] { null, Vector3.up })
            {
                var t = FBDecalFx.Place(FBDecalFx.Atlas.Blood, FBDecalFx.CellDrip, h.point, h.normal, .4f, up);
                Assert.IsNotNull(t, "decal não colocado no meio da parede");
                Assert.Greater(Vector3.Dot(FBDecalFx.FacingOf(t), h.normal), .995f, "decal fora da normal da superfície");
                float off = Vector3.Dot(t.position - h.point, h.normal); Assert.That(off, Is.InRange(0f, .06f), "decal afastado/enterrado na parede");
                if (up != null) Assert.Greater(Vector3.Dot(t.up, Vector3.ProjectOnPlane(Vector3.up, h.normal).normalized), .99f, "escorrido não aponta para baixo");
            }
            // piso inclinado: normal do chão
            var fl = Wall(new Vector3(10, 50, 0), Quaternion.Euler(12, 0, 0), new Vector3(4, .2f, 4)); yield return null; Physics.SyncTransforms();
            Assert.IsTrue(Physics.Raycast(new Vector3(10, 52, 0), Vector3.down, out var g, 4, FBWorld.SolidMask));
            var tf = FBDecalFx.Place(FBDecalFx.Atlas.Holes, FBDecalFx.HoleConcrete, g.point, g.normal, .1f);
            Assert.Greater(Vector3.Dot(FBDecalFx.FacingOf(tf), g.normal), .995f, "buraco fora da normal do piso");
            // quadrado (celular/WebGL): na beirada de uma peça pequena, encolhe ou desiste — nunca fica para fora
            if (!FBDecalFx.Projector)
            {
                var small = Wall(new Vector3(20, 50, 0), Quaternion.identity, new Vector3(.25f, .25f, .2f)); yield return null; Physics.SyncTransforms();
                var e = FBDecalFx.Place(FBDecalFx.Atlas.Blood, 0, new Vector3(20.1f, 50, -.1f), Vector3.back, 1f);
                Assert.IsTrue(e == null || e.lossyScale.x <= .35f, "decal de 1 m pendurado numa peça de 25 cm");
                Object.Destroy(small);
            }
            Object.Destroy(w); Object.Destroy(fl); FBDecalFx.Clear();
        }

        [UnityTest] public IEnumerator Pools_NaoCrescem()
        {
            FBFx.Warm(); yield return null;
            int systems = FBFxLib.Systems.Count, children = FBFxLib.Root.childCount, slots = FBDecalFx.SlotObjects;
            foreach (var ps in FBFxLib.Systems)                                     // partícula de malha exige Read/Write (senão erro em toda partida)
            { var r = ps.GetComponent<ParticleSystemRenderer>(); if (r.renderMode == ParticleSystemRenderMode.Mesh) Assert.IsTrue(r.mesh && r.mesh.isReadable, ps.name + ": malha de partícula sem Read/Write"); }
            for (int i = 0; i < 600; i++) FBDecalFx.Place(FBDecalFx.Atlas.Blood, i % 16, new Vector3(i * .3f, 80, 0), Vector3.up, .3f, null, 1, null, -1, false);
            Assert.LessOrEqual(FBDecalFx.ActiveCount, FBFxQuality.MaxDecals, "decals acima do limite"); Assert.AreEqual(slots, FBDecalFx.SlotObjects, "pool de decals cresceu");
            var cam = new GameObject("CamTeste").AddComponent<Camera>(); cam.tag = "MainCamera"; cam.transform.position = new Vector3(0, 81, -3);
            for (int f = 0; f < 10; f++)
            {
                for (int i = 0; i < 60; i++) { FBBloodFx.Spray(new Vector3(0, 81, 0), Vector3.forward, 2, 1.5f, 2); FBFx.BulletHole(new Vector3(i * .1f, 80, 0), Vector3.up); FBFx.Explosion(new Vector3(0, 81, 2), 4); }
                yield return null;
                foreach (var ps in FBFxLib.Systems) Assert.LessOrEqual(ps.particleCount, ps.main.maxParticles, ps.name + " passou do máximo");
            }
            Assert.AreEqual(systems, FBFxLib.Systems.Count, "sistemas de partícula criados na partida"); Assert.AreEqual(children, FBFxLib.Root.childCount, "objetos de FX criados na partida");
            Assert.LessOrEqual(FBDecalFx.ActiveCount, FBFxQuality.MaxDecals);
            Object.Destroy(cam.gameObject); FBDecalFx.Clear();
        }

        // ---- ferimentos num personagem de verdade (estande, terceira pessoa)
        static readonly (string hb, string part, bool head, string region)[] HITS = {
            ("HB_Cabeca", "head", true, "head"), ("HB_Spine", "body", false, "body"), ("HB_LeftUpperArm", "limb", false, "armL"), ("HB_RightUpperLeg", "limb", false, "legR") };

        // região de um osso pelo ancestral humano mais próximo
        static string RegionOf(Animator an, Transform t)
        {
            var map = new Dictionary<Transform, HumanBodyBones>(); for (int i = 0; i < (int)HumanBodyBones.LastBone; i++) { var b = an.GetBoneTransform((HumanBodyBones)i); if (b) map[b] = (HumanBodyBones)i; }
            for (var p = t; p; p = p.parent) if (map.TryGetValue(p, out var hb))
                {
                    var s = hb.ToString();
                    if (hb is HumanBodyBones.Head or HumanBodyBones.Jaw or HumanBodyBones.LeftEye or HumanBodyBones.RightEye) return "head";
                    if (hb == HumanBodyBones.Neck) return "neck";
                    if (hb == HumanBodyBones.LeftShoulder) return "shoulderL"; if (hb == HumanBodyBones.RightShoulder) return "shoulderR";
                    if (s.StartsWith("Left") && (s.Contains("Leg") || s.Contains("Foot") || s.Contains("Toe"))) return "legL";
                    if (s.StartsWith("Right") && (s.Contains("Leg") || s.Contains("Foot") || s.Contains("Toe"))) return "legR";
                    if (s.StartsWith("Left") && !s.Contains("Shoulder")) return "armL";
                    if (s.StartsWith("Right") && !s.Contains("Shoulder")) return "armR";
                    return "body";
                }
            return "?";
        }
        static bool RegionOk(string want, string got) => want == got || got == "neck" && (want == "head" || want == "body")
            || got == "shoulderL" && (want == "armL" || want == "body") || got == "shoulderR" && (want == "armR" || want == "body");

        [TearDown] public void SemFeridasNoCorpo() => FBBloodFx.BodyWounds = false;
        [UnityTest, Timeout(180000)] public IEnumerator Ferimento_NoOssoCerto()
        {
            FBBloodFx.BodyWounds = true;   // marcas no corpo estão desligadas no jogo (pedido); o teste valida o sistema ligado (TearDown desliga)
            FBSession.Load(); FBSession.map = "estande"; FBSession.mode = "treino"; FBSession.online = false; FBSession.view = "tps"; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_estande"); while (!op.isDone) yield return null;
            for (float t = 0; t < 2; t += Time.unscaledDeltaTime) yield return null;
            var v = FBMatch.Player; Assert.IsNotNull(v, "sem personagem"); var an = v.GetComponent<Animator>(); Assert.IsTrue(an && an.isHuman, "personagem sem Humanoid");
            FBFx.BloodOn = true; FBWoundFx.ClearCharacter(v);
            var weapons = new[] { "pistola", "fuzil", "escopeta", "sniper" }.Select(c => GameData.Weapons.Values.FirstOrDefault(w => w.cls == c)?.id).Where(id => id != null).ToList();
            Assert.Greater(weapons.Count, 0, "sem armas no GameData");
            foreach (var wid in weapons)
                foreach (var (hbName, part, head, region) in HITS)
                {
                    var hb = v.GetComponentsInChildren<FBHitbox>(true).FirstOrDefault(h => h.name == hbName); Assert.IsNotNull(hb, "sem hitbox " + hbName);
                    var col = hb.GetComponent<Collider>(); var c = col.bounds.center; var dir = -v.transform.forward;
                    var p = col.ClosestPoint(c - dir * 2);                                      // superfície da hitbox virada para o atirador
                    FBDamageEvents.Raise(new FBDamage { victim = v, type = FBDamageType.Bullet, amount = 30, applied = 30, hpBefore = 100, hpAfter = 100, head = head,
                        hit = new FBHitInfo { valid = true, point = p, normal = -dir, dir = dir, part = head ? "head" : part, weaponId = wid, dist = 12 } });
                    var w = FBWoundFx.WoundsOf(v).Where(x => !x.streak).OrderBy(x => x.seq).LastOrDefault();
                    Assert.IsNotNull(w, $"{wid}/{region}: sem ferimento");
                    Assert.AreEqual(w.bone, w.tr.parent, "ferimento não é filho do osso"); Assert.IsTrue(w.bone.IsChildOf(v.transform), "osso fora do personagem");
                    string got = RegionOf(an, w.bone); Assert.IsTrue(RegionOk(region, got), $"{wid}/{region}: ferimento no osso {w.bone.name} ({got})");
                    Assert.Less(Vector3.Distance(w.tr.position, p), .3f, $"{wid}/{region}: ferimento longe do impacto");
                    var lp = w.tr.localPosition; var bone = w.bone;
                    for (int i = 0; i < 8; i++) yield return null;                             // animação rodando
                    if (w.on) { Assert.AreEqual(bone, w.tr.parent, "ferimento soltou do osso"); Assert.Less((w.tr.localPosition - lp).magnitude, 1e-4f, "ferimento escorregou no osso"); }
                    Assert.LessOrEqual(FBWoundFx.WoundsOf(v).Count, FBFxQuality.WoundsPerChar, "passou do limite por personagem");
                    Assert.LessOrEqual(FBWoundFx.ActiveCount, FBFxQuality.MaxWounds, "passou do limite global");
                }
            // renascer limpa
            Assert.Greater(FBWoundFx.WoundsOf(v).Count, 0);
            v.Die(null); yield return null; yield return null; v.Revive(); yield return null; yield return null;
            Assert.AreEqual(0, FBWoundFx.WoundsOf(v).Count, "ferimentos continuaram depois de renascer");
            var dirLog = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../logs")); System.IO.Directory.CreateDirectory(dirLog);
            Debug.Log($"[FreeBala][teste] sangue: projetor URP {FBDecalFx.Projector} · nível FX {FBFxQuality.Level} · decals {FBDecalFx.ActiveCount}/{FBDecalFx.Capacity} · ferimentos {FBWoundFx.ActiveCount}/{FBWoundFx.Capacity}");
        }
    }
}
