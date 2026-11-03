// Postura com arma (idle/andar/mirar) por classe de arma × personagens de cada família de rig (mixamorig masc/fem, CC masc/fem, IE).
// Mede pelos MÚSCULOS do Humanoid (HumanPoseHandler: valores normalizados, iguais em qualquer rig; ±1 = limite do avatar, 40° no
// pescoço/cabeça/coluna, 20° no UpperChest), depois de toda a pós-animação:
//  - limites anatômicos de pescoço/cabeça (nod/tilt), coluna/peito (front-back, left-right) e ombros;
//  - fora da mira (descanso, combate parado, andar, relaxado): quanto a pós-animação SOMA à coluna/pescoço em relação à pose pura do
//    Animator (FBRig.bypass) — deve ser ~0 (era aqui o "pescoço jogado para trás": a mira do tronco tentando erguer a arma baixa);
//  - tremor: variação por quadro (60 qps fixos) de pescoço/cabeça/coluna.
// Log uma linha por personagem/classe/estado; prints de lado e de frente em unity/logs/idle_<char>_<classe>_<estado>.png (só r_tutu e r_pm_silva).
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FreeBala.Tests
{
    public class FBIdlePoseTests
    {
        static readonly string[] CHARS = { "r_tutu", "r_maia", "n_chad", "n_alicia", "r_pm_silva", "r_silva" };
        static readonly string[] CLASSES = { "fuzil", "submetralhadora", "pistola", "escopeta", "sniper", "explosivo" };
        static readonly string[] STATES = { "descanso", "combate", "andar", "mirar", "relaxado" };
        // músculo → limite anatômico em GRAUS (o músculo é normalizado pela faixa do avatar, que muda por osso: pescoço/cabeça/coluna ±40°,
        // UpperChest ±20°, ombro −15°/+30° — "1,37 no ombro" são ~21°, não 55°). Os clipes originais passam de 1 no ombro: o "Rifle Idle"
        // tem Left Shoulder Down-Up −1,43 em todos os quadros (assets/anims.json), o ombro abaixado de quem segura o fuzil baixo.
        static readonly (string m, float deg)[] LIMITS = {
            ("Neck Nod Down-Up", 32), ("Neck Tilt Left-Right", 24), ("Head Nod Down-Up", 32), ("Head Tilt Left-Right", 24),
            ("Spine Front-Back", 36), ("Spine Left-Right", 24), ("Chest Front-Back", 36), ("Chest Left-Right", 24),
            ("UpperChest Front-Back", 22), ("UpperChest Left-Right", 20),
            ("Left Shoulder Down-Up", 32), ("Right Shoulder Down-Up", 32), ("Left Shoulder Front-Back", 30), ("Right Shoulder Front-Back", 30) };
        static float Deg(int i, float m) => m >= 0 ? m * HumanTrait.GetMuscleDefaultMax(i) : -m * HumanTrait.GetMuscleDefaultMin(i);
        static readonly string[] AXIAL = { "Neck Nod Down-Up", "Neck Tilt Left-Right", "Head Nod Down-Up", "Head Tilt Left-Right", "Spine Front-Back", "Chest Front-Back", "UpperChest Front-Back" };

        class Probe { public FBCharacter ch; public HumanPoseHandler h; public HumanPose pose; public float[] last; }
        static int[] Idx(string[] names) => names.Select(n => System.Array.IndexOf(HumanTrait.MuscleName, n)).ToArray();

        static void SetState(FBCharacter ch, string st)
        {
            ch.combat = true; ch.relaxed = st == "relaxado"; ch.aiming = st == "mirar"; ch.shooting = false; ch.hasAim = st == "mirar";
            ch.combatHold = st == "combate" ? 1 : 0; if (st != "combate" && st != "mirar") ch.lastShot = 9;
            ch.velocity = st == "andar" ? ch.transform.forward * 1.4f : Vector3.zero;
            if (st == "mirar") { var c = ch.Chest ? ch.Chest.position : ch.transform.position + Vector3.up * 1.4f; ch.aimPoint = c + ch.transform.forward * 25; }
        }
        static float[] Read(Probe p) { p.h.GetHumanPose(ref p.pose); return p.pose.muscles; }

        [UnityTest, Timeout(1200000)]
        public IEnumerator Postura_Armado_Classes_Personagens()
        {
            GameData.Load(); Assert.IsNotNull(FBRegistry.I, "sem FBRegistry");
            int cf = Time.captureFramerate; Time.captureFramerate = 60;
            // câmera principal perto dos personagens: sem ela uma Camera.main de outro teste (longe) deixava o FBRig.Far ligado e a pós-animação
            // (mira, mão esquerda) não rodava — na pipeline 4 o ikW ficou 0 em tudo
            foreach (var c0 in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (c0.CompareTag("MainCamera")) c0.tag = "Untagged";
            var cam = new GameObject("CamIdle") { tag = "MainCamera" }.AddComponent<Camera>(); cam.transform.position = new Vector3(10, 1.5f, -6); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.35f, .38f, .42f);
            var lt = new GameObject("LuzIdle").AddComponent<Light>(); lt.type = LightType.Directional; lt.transform.rotation = Quaternion.Euler(40, 30, 0);
            var probes = new List<Probe>();
            for (int i = 0; i < CHARS.Length; i++)
            {
                var pf = FBRegistry.I.Char(CHARS[i]); if (!pf) { Debug.LogWarning("[FreeBala][idle] sem personagem " + CHARS[i]); continue; }
                var g = Object.Instantiate(pf, new Vector3(i * 4f, 0, 0), Quaternion.identity); var ch = g.GetComponent<FBCharacter>();
                ch.an.cullingMode = AnimatorCullingMode.AlwaysAnimate; ch.isPlayer = false;
                probes.Add(new Probe { ch = ch, h = new HumanPoseHandler(ch.an.avatar, ch.transform), pose = new HumanPose() });
            }
            Assert.Greater(probes.Count, 3, "personagens de teste não carregaram");
            var lim = LIMITS.Select(l => (i: System.Array.IndexOf(HumanTrait.MuscleName, l.m), l.m, l.deg)).Where(l => l.i >= 0).ToArray();
            var axial = Idx(AXIAL).Where(i => i >= 0).ToArray();
            var fails = new List<string>(); var rows = new List<string>();
            foreach (var cls in CLASSES)
            {
                var d = GameData.Weapons.Values.Where(w => w.cls == cls && FBRegistry.I.Weapon(w.id)).OrderBy(w => w.id).FirstOrDefault();
                if (d == null) { Debug.LogWarning("[FreeBala][idle] sem arma da classe " + cls); continue; }
                foreach (var p in probes) p.ch.SetWeapon(d, FBRegistry.I.Weapon(d.id));
                foreach (var st in STATES)
                {
                    foreach (var p in probes) { p.ch.rig.bypass = false; SetState(p.ch, st); }
                    for (int f = 0; f < 70; f++) { foreach (var p in probes) SetState(p.ch, st); yield return null; }   // transição
                    var maxAbs = probes.Select(_ => new float[HumanTrait.MuscleCount]).ToArray(); var sumOn = probes.Select(_ => new float[HumanTrait.MuscleCount]).ToArray();
                    var jit = new float[probes.Count]; var jitMax = new float[probes.Count];
                    for (int f = 0; f < 60; f++)
                    {
                        foreach (var p in probes) SetState(p.ch, st); yield return null;
                        for (int k = 0; k < probes.Count; k++)
                        {
                            var m = Read(probes[k]);
                            foreach (var (i, _, _) in lim) maxAbs[k][i] = Mathf.Max(maxAbs[k][i], Mathf.Abs(Deg(i, m[i])));
                            foreach (var i in axial) sumOn[k][i] += m[i] / 60f;
                            if (probes[k].last != null) { float dm = 0; foreach (var i in axial) dm = Mathf.Max(dm, Mathf.Abs(m[i] - probes[k].last[i])); jit[k] += dm / 59f; jitMax[k] = Mathf.Max(jitMax[k], dm); }
                            probes[k].last = (float[])m.Clone();
                        }
                    }
                    if ((probes[0].ch.skinId == "r_tutu") && (st == "descanso" || st == "mirar" || st == "andar"))
                        foreach (var p in probes.Where(q => q.ch.skinId == "r_tutu" || q.ch.skinId == "r_pm_silva"))
                            foreach (var (lado, off) in new[] { ("lado", p.ch.transform.right * 2.6f), ("frente", p.ch.transform.forward * 2.8f) })
                            {
                                var c = p.ch.Chest ? p.ch.Chest.position : p.ch.transform.position + Vector3.up * 1.4f;
                                cam.transform.position = c + off - Vector3.up * .25f; cam.transform.LookAt(c - Vector3.up * .25f);
                                var dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../logs")); System.IO.Directory.CreateDirectory(dir);
                                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"idle_{p.ch.skinId}_{cls}_{st}_{lado}.png")); yield return null; yield return null;
                            }
                    // pose pura do Animator (sem a pós-animação) no mesmo estado: quanto o rig soma ao clipe
                    var sumOff = probes.Select(_ => new float[HumanTrait.MuscleCount]).ToArray();
                    foreach (var p in probes) { p.ch.rig.bypass = true; p.last = null; }
                    for (int f = 0; f < 20; f++) { foreach (var p in probes) SetState(p.ch, st); yield return null; }
                    for (int f = 0; f < 30; f++)
                    {
                        foreach (var p in probes) SetState(p.ch, st); yield return null;
                        for (int k = 0; k < probes.Count; k++) { var m = Read(probes[k]); foreach (var i in axial) sumOff[k][i] += m[i] / 30f; }
                    }
                    foreach (var p in probes) p.ch.rig.bypass = false;
                    for (int k = 0; k < probes.Count; k++)
                    {
                        string who = $"{probes[k].ch.skinId}/{cls}({d.id})/{st}";
                        foreach (var (i, n, l) in lim) if (maxAbs[k][i] > l) fails.Add($"{who}: {n} {maxAbs[k][i]:0}° > {l:0}°");
                        float added = axial.Max(i => Mathf.Abs(sumOn[k][i] - sumOff[k][i])); string addN = HumanTrait.MuscleName[axial.OrderByDescending(i => Mathf.Abs(sumOn[k][i] - sumOff[k][i])).First()];
                        // andar: on/off pegam fases diferentes do ciclo de passo (deu 0,08–0,14 com o rig parado: aimW 0, ikW 0) — só log
                        if (st != "mirar" && st != "andar" && added > .12f) fails.Add($"{who}: pós-animação soma {added:0.00} em {addN} fora da mira (≈{added * 40:0}°)");
                        if (jitMax[k] > .08f || jit[k] > .025f) fails.Add($"{who}: tremor (média {jit[k]:0.000}/quadro, pico {jitMax[k]:0.000})");
                        int nh = System.Array.IndexOf(HumanTrait.MuscleName, "Neck Nod Down-Up"), hh = System.Array.IndexOf(HumanTrait.MuscleName, "Head Nod Down-Up"), sp = System.Array.IndexOf(HumanTrait.MuscleName, "Spine Front-Back");
                        rows.Add($"{who}: pescoço {sumOn[k][nh]:0.00} cabeça {sumOn[k][hh]:0.00} coluna {sumOn[k][sp]:0.00} · soma do rig {added:0.00} ({addN}) · tremor {jit[k]:0.000}/{jitMax[k]:0.000} · aimW {probes[k].ch.rig.AimW:0.00} ikW {probes[k].ch.rig.IkW:0.00}");
                    }
                }
            }
            Time.captureFramerate = cf;
            foreach (var r in rows) Debug.Log("[FreeBala][idle] " + r);
            foreach (var p in probes) { p.h.Dispose(); Object.Destroy(p.ch.gameObject); }
            Object.Destroy(cam.gameObject); Object.Destroy(lt.gameObject);
            Assert.IsEmpty(fails, $"{fails.Count} falhas de postura:\n" + string.Join("\n", fails.Take(80)));
        }
    }
}
