// r49: LABORATÓRIO DE ESCALA DAS ARMAS — FreeBala.exe -fbescalalab [-fbea_chars r_a,x171_b] [-fbea_armas ak47,38] [-fbea_fotos 0|1|2]
// Pedido de 30/10: "algumas armas estão gigantes nas mãos, nas costas, no peito ou na lateral" — revisar cada arma, achar a CAUSA RAIZ.
// Para cada personagem: altura do corpo (vértices da malha, parado), escala de mundo da raiz × do osso da mão/peito/coxa; para cada arma:
// comprimento de mundo ao longo do cano (caixa da malha no espaço da arma × escala de mundo) na mão e guardada (coldre: costas, peito,
// coxa), em "metros de uma pessoa de 1,75 m" (L ÷ altura × 1,75). A mesma arma tem de dar o mesmo tamanho em todos os corpos; o
// tamanho de cada arma tem de cair na faixa real da classe. Linhas "P ..." (personagem), "A ..." (arma) e "RESUMO" no fim.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    [DefaultExecutionOrder(5000)]
    public class FBEscalaArmaLab : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); if (System.Array.IndexOf(a, "-fbescalalab") < 0) return;
            var g = new GameObject("FB_EscalaArmaLab"); DontDestroyOnLoad(g); g.AddComponent<FBEscalaArmaLab>();
        }
        static string Arg(string k) { var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, k); return i >= 0 && i + 1 < a.Length && !a[i + 1].StartsWith("-") ? a[i + 1] : null; }
        static void Log(string s) => Debug.Log("[FreeBala][escalaarma] " + s);
        IEnumerator Wait(float s) { for (float w = 0; w < s; w += Time.unscaledDeltaTime) yield return null; }
        string dir; Camera cam;
        // faixa real (m) do comprimento total por classe
        static readonly Dictionary<string, (float lo, float hi)> REAL = new() { ["pistola"] = (.15f, .40f), ["submetralhadora"] = (.30f, .80f), ["fuzil"] = (.60f, 1.15f), ["escopeta"] = (.60f, 1.30f), ["sniper"] = (.90f, 1.60f), ["metralhadora"] = (.90f, 1.40f), ["explosivo"] = (.90f, 1.40f) };
        // r50: comprimento real (m) de cada arma conhecida (versão de coronha aberta); as outras caem na faixa da classe. Tolerância ±15 %.
        static readonly Dictionary<string, float> REAL_W = new() { ["ak"] = .88f, ["ak12"] = .945f, ["aks74"] = .94f, ["fal"] = 1.09f, ["g3"] = 1.025f, ["g3_1"] = 1.025f, ["galil_ace"] = .85f, ["parafal"] = 1.095f,
            ["rpk"] = 1.04f, ["sa58_para"] = 1.01f, ["scar"] = .89f, ["m4"] = .84f, ["mp5"] = .68f, ["ump"] = .69f, ["uzi"] = .65f, ["p90"] = .50f, ["czskorpion"] = .52f, ["38"] = .27f, ["1911_gold"] = .22f,
            ["beretta_gold"] = .217f, ["desert_eagle"] = .27f, ["desert_eagle_gold"] = .27f, ["g18"] = .19f, ["barret_50"] = 1.14f, ["sniper_hunting"] = 1.12f, ["remington_870"] = 1.0f, ["m31"] = 1.04f, ["rpg_7"] = 1.2f };

        static readonly List<Vector3> V = new(); static Mesh bake;
        public static (float lo, float hi) MeshY(FBCharacter a)
        {
            // r49: caixa de mundo dos renderers de pele (o BakeMesh(…, true) devolvia ~1,0 para corpos de 1,8 m — não aplicava a escala da raiz)
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var r in a.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!r || !r.enabled || !r.gameObject.activeInHierarchy || Weap(r.transform, a)) continue;
                var b = r.bounds; lo = Mathf.Min(lo, b.min.y); hi = Mathf.Max(hi, b.max.y);
            }
            return (lo, hi);
        }
        static bool Weap(Transform t, FBCharacter a) { for (; t && t != a.transform; t = t.parent) if (t.name.StartsWith("Arma_") || t.name.StartsWith("Coldre_")) return true; return false; }
        // comprimento de mundo da arma ao longo do cano (e o maior lado da caixa, para o caso de o eixo do cano falhar)
        public static (float len, float big) Len(GameObject w)
        {
            if (!w) return (0, 0); var inv = w.transform.worldToLocalMatrix; Bounds b = default; bool first = true;
            foreach (var r in w.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || System.Text.RegularExpressions.Regex.IsMatch(r.name, "laser|beam|flash", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) continue;
                Mesh m = null; if (r is SkinnedMeshRenderer s) m = s.sharedMesh; else { var mf = r.GetComponent<MeshFilter>(); if (mf) m = mf.sharedMesh; } if (!m) continue;
                var M = inv * r.transform.localToWorldMatrix; var mb = m.bounds;
                for (int i = 0; i < 8; i++) { var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)); var p = M.MultiplyPoint3x4(c); if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p); }
            }
            if (first) return (0, 0); float k = Mathf.Abs(w.transform.lossyScale.x);
            Vector3 ax = Vector3.forward; if (FBCharacter.GunAxes(w, out var sp, out var bl, out _)) ax = w.transform.InverseTransformDirection(sp.TransformDirection(bl)).normalized;
            float along = Mathf.Abs(b.size.x * ax.x) + Mathf.Abs(b.size.y * ax.y) + Mathf.Abs(b.size.z * ax.z);
            return (along * k, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) * k);
        }
        void Look(Vector3 from, Vector3 at, float fov = 40) { cam.transform.position = from; cam.transform.LookAt(at); cam.fieldOfView = fov; }
        IEnumerator Shot(string n) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png")); yield return null; }

        IEnumerator Start()
        {
            bool antes = Arg("-fbea_antes") == "1";
            dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste", antes ? "escalaarma_antes" : "escalaarma"); System.IO.Directory.CreateDirectory(dir);
            foreach (var old in System.IO.Directory.GetFiles(dir, "*.png")) System.IO.File.Delete(old);
            int fotos = int.TryParse(Arg("-fbea_fotos"), out var fo) ? fo : 1;
            FBCharacter.WeaponScaleFix = !antes; Log($"ajuste por modelo (ModelFix r50) {(FBCharacter.WeaponScaleFix ? "LIGADO" : "DESLIGADO (antes)")} · medida = comprimento ÷ altura do corpo × 1,75 m");
            yield return Wait(3); GameData.Load(); FBSession.Load(); var keep = (FBSession.map, FBSession.mode, FBSession.view);
            FBSession.map = "vila_jardim"; FBSession.mode = "role"; FBSession.online = false; FBSession.view = "tps"; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_vila_jardim"); while (op != null && !op.isDone) yield return null; yield return Wait(8);
            var me = FBMatch.Player; if (!me) { Log("sem jogador"); yield break; } FBMatch.God = true;
            if (FBGarage.Open_) { FBGarage.AutoPick(); yield return Wait(1.5f); }
            cam = new GameObject("EscalaCam").AddComponent<Camera>(); cam.depth = 100; cam.cullingMask = Camera.main ? Camera.main.cullingMask : ~0; cam.enabled = false; cam.nearClipPlane = .05f;
            var p0 = me.Pos + Vector3.up * 300; var pad = new GameObject("FB_EscalaPad").AddComponent<BoxCollider>(); pad.gameObject.layer = FBWorld.LMap; pad.size = new Vector3(200, 2, 200); pad.transform.position = p0 - Vector3.up;
            var padVis = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(padVis.GetComponent<Collider>()); padVis.transform.localScale = new Vector3(200, .02f, 200); padVis.transform.position = p0 - Vector3.up * .01f;
            { var pm = new Material(FBShaders.Lit); pm.SetColor("_BaseColor", new Color(.46f, .47f, .45f)); padVis.GetComponent<Renderer>().sharedMaterial = pm; }   // r49e: o material padrão do primitivo não vai no build (chão rosa nas fotos)
            Physics.SyncTransforms(); var spot = p0 + new Vector3(10, 0, 10); FBMatch.Teleport(me, spot + new Vector3(-8, 0, -8), 0); yield return Wait(1);

            bool Has(string id) => FBRegistry.I && FBRegistry.I.HasChar(id);
            var chars = Arg("-fbea_chars")?.Split(',').Where(Has).ToList() ?? GameData.Skins.Where(s => Has(s.id)).Select(s => s.id).Distinct().OrderBy(x => x).ToList();
            var weapons = (Arg("-fbea_armas")?.Split(',').Where(GameData.Weapons.ContainsKey).Select(x => GameData.Weapons[x]) ?? GameData.Weapons.Values)
                .Where(d => !GameData.BannedWeapons.Contains(d.id) && FBRegistry.I.Weapon(d.id)).OrderBy(d => d.cls).ThenBy(d => d.id).ToList();
            Log($"personagens {chars.Count} · armas {weapons.Count}: {string.Join(", ", weapons.Select(w => w.id + "/" + w.cls))}");
            var hand = new Dictionary<string, List<(string ch, float n175)>>(); var hol = new Dictionary<string, List<(string ch, float n175, string slot)>>();
            var rows = new List<(string ch, float H, float hk, float ck, float tk)>();
            WeaponDef ByCls(string c) => weapons.FirstOrDefault(w => w.cls == c);
            int done = 0;
            foreach (var id in chars)
            {
                var a = FBStreetEvents.Spawn(id, spot, 0, "ea"); if (!a) { Log($"{id} não nasceu"); continue; }
                yield return Wait(.5f); cam.enabled = true; var hp0 = a.Pos + Vector3.up; Look(hp0 + Vector3.forward * 3, hp0); yield return null; yield return null; cam.enabled = false;   // Animator escreve (não fica na pose do FBX)
                var (lo, hi) = MeshY(a); float H = hi - lo; var an = a.an; float rk = a.transform.lossyScale.y;
                float BK(HumanBodyBones b) { var t = an ? an.GetBoneTransform(b) : null; return t ? Mathf.Abs(t.lossyScale.x) / Mathf.Max(1e-5f, rk) : 0; }
                float hk = BK(HumanBodyBones.RightHand), ck = BK(HumanBodyBones.UpperChest) > 0 ? BK(HumanBodyBones.UpperChest) : BK(HumanBodyBones.Chest), tk = BK(HumanBodyBones.RightUpperLeg);
                rows.Add((id, H, hk, ck, tk));
                var hs = new List<string>();
                foreach (var d in weapons)
                {
                    a.SetWeapon(d, FBRegistry.I.Weapon(d.id)); a.combat = true; a.SetWeaponVisible(true); yield return null;
                    var (L, big) = Len(a.weaponGO); float n = L / Mathf.Max(.5f, H) * 1.75f; hs.Add($"{d.id} {n:0.00}");   // r50: régua = o corpo (a r49 dividia por scaleK e comparava unidades de mundo com metros)
                    (hand.TryGetValue(d.id, out var l) ? l : hand[d.id] = new()).Add((id, n));
                }
                // guardadas: pistola (coxa), SMG (peito), fuzil (costas) com uma escopeta na mão
                var inHand = ByCls("escopeta") ?? weapons[0]; a.SetWeapon(inHand, FBRegistry.I.Weapon(inHand.id)); a.SetWeaponVisible(true);
                var stow = new[] { ByCls("pistola"), ByCls("submetralhadora"), ByCls("fuzil") }.Where(x => x != null && x != inHand).ToList();
                a.SetHolsters(stow.Select(x => (x, FBRegistry.I.Weapon(x.id)))); for (int i = 0; i < 4; i++) yield return null;
                var hh = new List<string>();
                foreach (var x in stow)
                {
                    var go = a.HolsterObjects.FirstOrDefault(g => g && g.name == "Coldre_" + x.id); if (!go || !go.activeInHierarchy) { hh.Add($"{x.id} (oculta)"); continue; }
                    var (L, _) = Len(go); float n = L / Mathf.Max(.5f, H) * 1.75f; string slot = FBCharacter.HolsterSlot(x.cls); hh.Add($"{x.id}@{slot} {n:0.00}");
                    (hol.TryGetValue(x.id, out var l) ? l : hol[x.id] = new()).Add((id, n, slot));
                }
                Log($"P {id} · altura {H:0.00} (escala raiz {rk:0.000}, scaleK {a.ScaleK:0.000}) · osso/raiz: mão {hk:0.000} peito {ck:0.000} coxa {tk:0.000} · mão: {string.Join(" ", hs)} · guardadas: {string.Join(" ", hh)}");
                if (fotos >= 2 || (fotos == 1 && done < 6))
                {
                    var rifle = ByCls("fuzil"); if (rifle != null) { a.SetWeapon(rifle, FBRegistry.I.Weapon(rifle.id)); a.SetWeaponVisible(true); a.SetHolsters(new[] { ByCls("pistola"), ByCls("submetralhadora"), ByCls("sniper") }.Where(x => x != null).Select(x => (x, FBRegistry.I.Weapon(x.id)))); }
                    for (int i = 0; i < 6; i++) yield return null;
                    var hp = (an ? an.GetBoneTransform(HumanBodyBones.Hips).position : a.Pos + Vector3.up) + Vector3.up * .2f; var f = a.transform.forward.Flat().normalized; var r = Vector3.Cross(Vector3.up, f);
                    cam.enabled = true; Look(hp + f * 2.6f + r * .8f + Vector3.up * .3f, hp); yield return Shot($"{id}_frente"); Look(hp - f * 2.6f - r * .9f + Vector3.up * .4f, hp); yield return Shot($"{id}_costas"); Look(hp + r * 2.8f, hp); yield return Shot($"{id}_lado"); cam.enabled = false;
                }
                FBStreetEvents.Despawn(a); done++; yield return Wait(.2f);
            }
            // por arma: tamanho na mão (mediana entre os corpos), dispersão e quem foge da mediana (> 15 %), faixa real da classe
            int bad = 0;
            foreach (var d in weapons)
            {
                if (!hand.TryGetValue(d.id, out var l) || l.Count == 0) continue; var sorted = l.Select(x => x.n175).OrderBy(x => x).ToList(); float med = sorted[sorted.Count / 2];
                var off = l.Where(x => med > 0 && Mathf.Abs(x.n175 / med - 1) > .15f).Select(x => $"{x.ch} {x.n175:0.00}").ToList();
                bool inReal = REAL_W.TryGetValue(d.id, out var rw) ? Mathf.Abs(med / rw - 1) <= .15f : !REAL.TryGetValue(d.cls, out var rr) || (med >= rr.lo && med <= rr.hi); if (!inReal || off.Count > 0) bad++;
                string hs = hol.TryGetValue(d.id, out var hl) && hl.Count > 0 ? $" · guardada méd {hl.Average(x => x.n175):0.00} ({hl[0].slot}) faixa {hl.Min(x => x.n175):0.00}–{hl.Max(x => x.n175):0.00}" : "";
                Log($"A {d.id} ({d.cls}) · na mão méd {med:0.00} m (faixa {sorted[0]:0.00}–{sorted[^1]:0.00}){(REAL_W.TryGetValue(d.id, out var rw2) ? $" real {rw2:0.00} ({(med / rw2 - 1) * 100:+0;-0} %){(inReal ? "" : " FORA DO REAL")}" : REAL.TryGetValue(d.cls, out var r2) ? $" real {r2.lo:0.00}–{r2.hi:0.00}{(inReal ? "" : " FORA DA FAIXA REAL")}" : "")}{hs}{(off.Count > 0 ? " · FOGEM >15 %: " + string.Join(", ", off.Take(12)) : "")}");
            }
            var hkOff = rows.Where(x => Mathf.Abs(x.hk - 1) > .05f).Select(x => $"{x.ch} {x.hk:0.00}").ToList();
            Log($"RESUMO: {weapons.Count - bad}/{weapons.Count} armas no tamanho certo e iguais em todos os corpos · osso da mão com escala ≠ raiz (> 5 %): {hkOff.Count} [{string.Join(", ", hkOff.Take(20))}] · altura {rows.Min(x => x.H):0.00}–{rows.Max(x => x.H):0.00}");
            (FBSession.map, FBSession.mode, FBSession.view) = keep; FBSession.Save(); Log("fim"); yield return Wait(1); Application.Quit();
        }
    }
}
