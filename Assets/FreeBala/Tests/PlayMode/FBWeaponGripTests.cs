// Empunhadura em 3ª pessoa: TODAS as armas × personagens de cada família de rig (mixamorig masc/fem do jogo, CC do pacote NPCS masc/fem,
// IE com o osso da mão girado ~90°/120°), mirando para a frente. Mede, depois da pós-animação (mira pelo cano, mão esquerda, dedos):
//  - cano × direção da mira (≤ 20°) e cano à frente do peito;
//  - arma em pé: o lado de cima (SOCKET_Scope, acima do cano em todas as 54 armas) × cima do mundo (> 0,5 — invertida dá < 0);
//  - punho direito × punho medido na animação de 1ª pessoa da arma (tpsGrip.r.w, espaço da arma): ≤ 10 cm;
//  - referencial anatômico da mão direita (dedos e palma) × o da animação de 1ª pessoa: ≤ 45° / 60° (rig sem correção dá ~90°);
//  - fuzil: mão esquerda chegou no alvo do guarda-mão (≤ 4 cm) e a palma (nó do médio) está a ≤ 5 cm da superfície do guarda-mão.
// Grava um print por personagem (lateral, com 3 armas) em unity/logs/grip_<id>.png para conferência visual.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FreeBala.Tests
{
    public class FBWeaponGripTests
    {
        static readonly string[] CHARS = { "r_tutu", "r_maia", "n_chad", "n_alicia", "n_friday", "r_pm_silva", "r_silva" };

        static Transform Node0(GameObject w) { var t = w.transform; return t.childCount == 1 && !t.GetComponent<Renderer>() ? t.GetChild(0) : t; }   // = weaponScene do web
        static Vector3 V(object o) { var l = o as List<object>; return l != null && l.Count >= 3 ? new Vector3(-System.Convert.ToSingle(l[0]), System.Convert.ToSingle(l[1]), System.Convert.ToSingle(l[2])) : Vector3.zero; }   // glTF → Unity (x espelhado)
        static Vector3 V0(object o) => V((o as List<object>)?[0]);

        [UnityTest, Timeout(900000)]
        public IEnumerator Empunhadura_TodasArmas_Personagens()
        {
            GameData.Load(); Assert.IsNotNull(FBRegistry.I, "sem FBRegistry");
            var cam = new GameObject("CamGrip").AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.35f, .38f, .42f);
            var lt = new GameObject("LuzGrip").AddComponent<Light>(); lt.type = LightType.Directional; lt.transform.rotation = Quaternion.Euler(40, 30, 0);
            var chars = new List<FBCharacter>();
            for (int i = 0; i < CHARS.Length; i++)
            {
                var pf = FBRegistry.I.Char(CHARS[i]); if (!pf) { Debug.LogWarning("[FreeBala][grip] sem personagem " + CHARS[i]); continue; }
                var g = Object.Instantiate(pf, new Vector3(i * 4f, 0, 0), Quaternion.identity); var ch = g.GetComponent<FBCharacter>();
                ch.an.cullingMode = AnimatorCullingMode.AlwaysAnimate; ch.isPlayer = true; chars.Add(ch);
            }
            Assert.Greater(chars.Count, 3, "personagens de teste não carregaram");
            var weapons = GameData.Weapons.Values.Where(d => FBRegistry.I.Weapon(d.id)).OrderBy(d => d.cls).ThenBy(d => d.id).ToList();
            Assert.Greater(weapons.Count, 10, "armas não carregaram");
            var fails = new List<string>(); var rows = new List<string>(); var shotIds = new[] { "ak", "g18", "remington_870" };
            float ts = Time.timeScale; Time.timeScale = 2;
            foreach (var ch in chars) Debug.Log($"[FreeBala][grip] {ch.skinId}: osso da mão girado {ch.HandDeg:0}° em relação ao rig do puppet ({(ch.HandDeg < FBHand.SameRigDeg ? "mesmo rig" : "corrigido")})");
            foreach (var d in weapons)
            {
                foreach (var ch in chars) { ch.SetWeapon(d, FBRegistry.I.Weapon(d.id)); ch.combat = true; ch.aiming = true; ch.hasAim = true; }
                for (float t = 0; t < .8f; t += Time.deltaTime)
                {
                    foreach (var ch in chars) { var c = ch.Chest ? ch.Chest.position : ch.transform.position + Vector3.up * 1.4f; ch.aimPoint = new Vector3(c.x, c.y, c.z + 25); }
                    yield return null;
                }
                var grip = Json.O(GameData.TpsGrip, d.id);
                foreach (var ch in chars)
                {
                    string who = $"{ch.skinId}/{d.id}"; var w = ch.weaponGO; if (!w) { fails.Add(who + ": sem arma na mão"); continue; }
                    float s = ch.ScaleK; var bar = ch.BarrelDir(); var mz = ch.MuzzlePos;
                    var aim = (ch.aimPoint - mz).normalized; float aimErr = Vector3.Angle(bar, aim);
                    Transform scope = null; foreach (var t in w.GetComponentsInChildren<Transform>(true)) if (t.name == "SOCKET_Scope") { scope = t; break; }
                    var top = scope ? Vector3.ProjectOnPlane(scope.position - mz, bar) : Vector3.ProjectOnPlane(w.transform.up, bar);
                    float upDot = top.sqrMagnitude > 1e-8f ? Vector3.Dot(top.normalized, Vector3.ProjectOnPlane(Vector3.up, bar).normalized) : 1;
                    float front = Vector3.Dot(mz - (ch.Chest ? ch.Chest.position : ch.transform.position), ch.transform.forward);
                    if (aimErr > 20) fails.Add($"{who}: cano {aimErr:0}° fora da mira");
                    if (upDot < .5f) fails.Add($"{who}: arma {(upDot < 0 ? "DE CABEÇA PARA BAIXO" : "deitada")} (cima·mundo {upDot:0.00})");
                    if (front < .15f * s) fails.Add($"{who}: cano atrás/dentro do corpo ({front:0.00} m)");
                    string extra = "";
                    var n0 = Node0(w); var R = Json.O(grip, "r");
                    if (R != null && ch.RightHand)
                    {
                        var wr = n0.TransformPoint(V(R["w"])); float dw = (ch.RightHand.position - wr).magnitude;
                        if (dw > .10f * s) fails.Add($"{who}: punho direito a {dw * 100:0} cm do punho da animação FP");
                        extra += $" punhoD {dw * 100:0}cm";
                        if (FBHand.Frame(ch.an, true, out var rf, out _))
                        {
                            var hq = ch.RightHand.rotation * rf; Vector3 hf = hq * Vector3.forward, hn = hq * Vector3.up;
                            var ff = n0.TransformDirection(V0(Json.L(R, "m")) - V(R["w"])); var fs = n0.TransformDirection(V0(Json.L(R, "i")) - V0(Json.L(R, "p")));
                            var fn = Vector3.Cross(ff.normalized, Vector3.ProjectOnPlane(fs, ff).normalized);
                            float aF = Vector3.Angle(hf, ff), aN = Vector3.Angle(hn, fn);
                            if (aF > 45) fails.Add($"{who}: dedos da mão direita {aF:0}° fora da pegada FP");
                            if (aN > 60) fails.Add($"{who}: palma direita {aN:0}° fora da pegada FP");
                            extra += $" dedos {aF:0}° palma {aN:0}°";
                        }
                    }
                    if (!d.IsPistol && ch.rig && ch.rig.LeftTargetValid && ch.LeftHand)
                    {
                        float dl = (ch.LeftHand.position - ch.rig.LeftTarget).magnitude;
                        // palma = nó do dedo médio × centro do guarda-mão que o IK usou (o cano fica acima do guarda-mão: medir contra o eixo do
                        // cano dava 6–13 cm mesmo com a mão no lugar; escopeta/RPG têm a telha/tubo bem abaixo do cano)
                        var lm = ch.an.GetBoneTransform(HumanBodyBones.LeftMiddleProximal); var gb = ch.rig.GripAxis.normalized; var v = (lm ? lm.position : ch.rig.LeftTarget) - ch.rig.GripCenter;
                        float axis = Mathf.Max(0, (v - gb * Vector3.Dot(v, gb)).magnitude - ch.rig.GripRadius);
                        if (dl > .04f * s) fails.Add($"{who}: mão esquerda não alcançou o guarda-mão ({dl * 100:0} cm)");
                        if (axis > .05f * s) fails.Add($"{who}: palma esquerda a {axis * 100:0} cm da superfície do guarda-mão");
                        extra += $" maoE {dl * 100:0}cm/palma-eixo {axis * 100:0}cm guarda-mão {(ch.rig.HasSection ? "malha" : "cilindro")}";
                    }
                    else if (!d.IsPistol) fails.Add($"{who}: IK da mão esquerda desligado (peso {(ch.rig ? ch.rig.IkW : 0):0.00})");
                    rows.Add($"{who}: mira {aimErr:0}° cima {upDot:0.00} frente {front:0.00}{extra}");
                }
                if (System.Array.IndexOf(shotIds, d.id) >= 0)
                    foreach (var ch in chars)
                    {
                        var c = ch.Chest ? ch.Chest.position : ch.transform.position + Vector3.up * 1.4f;
                        cam.transform.position = c + ch.transform.right * 1.6f + ch.transform.forward * .5f + Vector3.up * .1f; cam.transform.LookAt(c + ch.transform.forward * .35f);
                        var dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../logs")); System.IO.Directory.CreateDirectory(dir);
                        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"grip_{ch.skinId}_{d.id}.png")); yield return null; yield return null;
                    }
            }
            Time.timeScale = ts;
            foreach (var r in rows) Debug.Log("[FreeBala][grip] " + r);
            foreach (var ch in chars) Object.Destroy(ch.gameObject); Object.Destroy(cam.gameObject); Object.Destroy(lt.gameObject);
            Assert.IsEmpty(fails, $"{fails.Count} falhas de empunhadura:\n" + string.Join("\n", fails.Take(80)));
        }
    }
}
