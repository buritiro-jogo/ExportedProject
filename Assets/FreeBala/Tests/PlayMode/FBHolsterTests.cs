// Armas guardadas no corpo (HOLST do web / coldre do IE): logo no spawn (Give → Equip) as armas dos OUTROS slots aparecem — fuzil nas costas,
// SMG na bandoleira, pistola na coxa —, a da mão nunca duplica, e o tamanho de cada uma é o da mesma arma na mão (escala de mundo e
// caixa das malhas), perto do corpo. Repete depois de trocar de arma. Print de costas em unity/logs/coldre_<char>.png.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FreeBala.Tests
{
    public class FBHolsterTests
    {
        static readonly string[] CHARS = { "r_tutu", "r_maia", "n_alicia", "r_pm_silva" };
        static float Size(GameObject g) { var rs = g.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer)).ToList(); if (rs.Count == 0) return 0; var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b.size.magnitude; }

        [UnityTest, Timeout(300000)]
        public IEnumerator Coldres_NoSpawn_TamanhoDaMao()
        {
            GameData.Load(); Assert.IsNotNull(FBRegistry.I, "sem FBRegistry");
            string Pick(string cls) => GameData.Weapons.Values.Where(w => w.cls == cls && FBRegistry.I.Weapon(w.id)).OrderBy(w => w.id).Select(w => w.id).FirstOrDefault();
            var ids = new[] { Pick("fuzil"), Pick("submetralhadora"), Pick("pistola") }.Where(x => x != null).ToArray();
            Assert.AreEqual(3, ids.Length, "faltam armas de teste");
            foreach (var c0 in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (c0.CompareTag("MainCamera")) c0.tag = "Untagged";
            var cam = new GameObject("CamColdre") { tag = "MainCamera" }.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.35f, .38f, .42f);
            var lt = new GameObject("LuzColdre").AddComponent<Light>(); lt.type = LightType.Directional; lt.transform.rotation = Quaternion.Euler(40, 150, 0);
            // tamanho de referência de cada arma NA MÃO (mesmo personagem de base)
            var fails = new List<string>(); var rows = new List<string>();
            var chars = new List<FBCharacter>();
            for (int i = 0; i < CHARS.Length; i++)
            {
                var pf = FBRegistry.I.Char(CHARS[i]); if (!pf) continue;
                var g = Object.Instantiate(pf, new Vector3(i * 4f, 0, 0), Quaternion.identity); var ch = g.GetComponent<FBCharacter>(); ch.an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                g.GetOrAdd<FBWeaponUser>(); chars.Add(ch);
            }
            Assert.Greater(chars.Count, 1, "personagens de teste não carregaram");
            // referência: cada arma na mão
            var handSize = new Dictionary<(FBCharacter, string), (float scale, float size)>();
            foreach (var id in ids)
            {
                foreach (var ch in chars) ch.GetComponent<FBWeaponUser>().Give(id);
                for (int f = 0; f < 10; f++) yield return null;
                foreach (var ch in chars) handSize[(ch, id)] = (ch.weaponGO ? ch.weaponGO.transform.lossyScale.x : 0, ch.weaponGO ? Size(ch.weaponGO) : 0);
            }
            foreach (var ch in chars) ch.GetComponent<FBWeaponUser>().Strip();
            for (int step = 0; step < 2; step++)
            {
                foreach (var ch in chars) { var wu = ch.GetComponent<FBWeaponUser>(); if (step == 0) wu.Give(ids); else wu.Equip(2, true); }   // spawn com as três; depois troca para a pistola
                for (int f = 0; f < 30; f++) yield return null;
                foreach (var ch in chars)
                {
                    var wu = ch.GetComponent<FBWeaponUser>(); var hand = wu.Cur?.id; var hs = ch.HolsterObjects.Where(o => o.activeInHierarchy).ToList();
                    string who = $"{ch.skinId}/{(step == 0 ? "spawn" : "troca")}(mão {hand})";
                    if (hs.Count != 2) fails.Add($"{who}: {hs.Count} armas no corpo (esperado 2)");
                    foreach (var h in hs)
                    {
                        var id = h.name.Replace("Coldre_", "");
                        if (id == hand) fails.Add($"{who}: a arma da mão ({id}) também está no corpo");
                        if (!handSize.TryGetValue((ch, id), out var refS)) continue;
                        float sc = h.transform.lossyScale.x, sz = Size(h), dist = (h.transform.position - (ch.Chest ? ch.Chest.position : ch.transform.position)).magnitude;
                        if (Mathf.Abs(sc / refS.scale - 1) > .05f) fails.Add($"{who}: {id} guardada com escala {sc:0.000} × na mão {refS.scale:0.000}");
                        if (refS.size > 0 && Mathf.Abs(sz / refS.size - 1) > .2f) fails.Add($"{who}: {id} guardada mede {sz:0.00} m × na mão {refS.size:0.00} m");
                        if (dist > 1.2f * ch.ScaleK) fails.Add($"{who}: {id} a {dist:0.00} m do peito (fora do corpo)");
                        rows.Add($"{who}: {id} escala {sc:0.000}/{refS.scale:0.000} tamanho {sz:0.00}/{refS.size:0.00} m · {dist:0.00} m do peito");
                    }
                }
            }
            foreach (var ch in chars)
            {
                var c = ch.Chest ? ch.Chest.position : ch.transform.position + Vector3.up * 1.4f;
                cam.transform.position = c - ch.transform.forward * 2.2f + ch.transform.right * .8f; cam.transform.LookAt(c - Vector3.up * .3f);
                var dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../logs")); System.IO.Directory.CreateDirectory(dir);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"coldre_{ch.skinId}.png")); yield return null; yield return null;
            }
            foreach (var r in rows) Debug.Log("[FreeBala][coldre] " + r);
            foreach (var ch in chars) Object.Destroy(ch.gameObject); Object.Destroy(cam.gameObject); Object.Destroy(lt.gameObject);
            Assert.IsEmpty(fails, $"{fails.Count} falhas de coldre:\n" + string.Join("\n", fails.Take(60)));
        }
    }
}
