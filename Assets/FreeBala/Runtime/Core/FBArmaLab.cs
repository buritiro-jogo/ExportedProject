// Autoteste de ARMAS E ACESSÓRIOS (rodada 45): FreeBala.exe -fbarmalab [arma]
//  · miras: cada mira da arma em ADS (1ª pessoa) -> distância em pixels do RETÍCULO (malha emissiva/ponto) e do centro da LENTE ao centro
//    da tela + print autoteste/arma/<arma>_<mira>.png;
//  · laser/lanterna: feixes e lanternas acesos sem mirar (tem de ser 0) e mirando (1);
//  · empunhaduras: subida da câmera em 15 tiros (mesma arma, sem dispersão) sem nada, vertical, angulada, bipé em pé e bipé agachado.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBArmaLab : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbarmalab"); if (i < 0) return;
            var g = new GameObject("FB_ArmaLab"); DontDestroyOnLoad(g); var t = g.AddComponent<FBArmaLab>(); if (i + 1 < a.Length && !a[i + 1].StartsWith("-")) t.wid = a[i + 1];
        }
        string wid, dir; bool aim, fire, crouchT;
        static void Log(string s) => Debug.Log("[FreeBala][armalab] " + s);
        IEnumerator Wait(float s) { for (float w = 0; w < s; w += Time.deltaTime) yield return null; }
        static Vector3? TopOf(Transform t) { var rs = t.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return null; var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return new Vector3(b.center.x, b.max.y - b.size.y * .08f, b.center.z); }
        IEnumerator Start()
        {
            dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste", "arma"); System.IO.Directory.CreateDirectory(dir);
            yield return Wait(3); GameData.Load(); FBSession.Load(); var keep = (FBSession.map, FBSession.mode, FBSession.view);
            FBSession.map = "vila_jardim"; FBSession.mode = "classico"; FBSession.online = false; FBSession.view = "fp"; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_vila_jardim"); while (op != null && !op.isDone) yield return null;
            for (float w = 0; w < 25 && !(FBPlayer.I && FBPlayer.I.wu); w += Time.deltaTime) yield return null;
            var P = FBPlayer.I; if (!P) { Log("sem jogador"); yield break; }
            yield return Wait(2); FBMatch.God = true; foreach (var b in FBMatch.Bots.ToList()) if (b) b.gameObject.SetActive(false);
            bool tv = !P.firstPerson;
            FBPlayer.TestInput = inp => { inp.toggleView = tv; tv = false; inp.aim = aim; inp.aimPressed = false; inp.fire = fire; inp.firePressed = fire; inp.move = Vector2.zero; inp.look = Vector2.zero; inp.crouchToggle = crouchT; crouchT = false; inp.reload = false; inp.slot = -1; inp.cycle = 0; return inp; };
            var d = wid != null && GameData.Weapons.TryGetValue(wid, out var dd) ? dd : GameData.Weapons.Values.FirstOrDefault(x => x.cls == "fuzil" && x.auto && FBRegistry.I.Weapon(x.id) && Json.L(Json.O(Json.O(GameData.WeaponsRaw, x.id), "att"), "scopes")?.Count >= 6);
            if (d == null) { Log("sem arma"); yield break; }
            IEnumerator Equip(FBWeaponModel.Choice c) { FBWeaponModel.Set(d, c); P.wu.Strip(); P.wu.Give(d.id); int sl = System.Array.FindIndex(P.wu.slots, x => x != null && x.id == d.id); P.wu.Equip(sl >= 0 ? sl : 0, true); yield return Wait(1.4f); }
            var att = Json.O(Json.O(GameData.WeaponsRaw, d.id), "att"); var scopes = Json.L(att, "scopes") ?? new List<object>();
            Log($"arma {d.id} ({d.name}) · {scopes.Count} miras · {Json.L(att, "grips")?.Count ?? 0} empunhaduras · {Json.L(att, "lasers")?.Count ?? 0} lasers");
            // ---- 1. miras no ADS
            var cam = P.cam ? P.cam : Camera.main;
            for (int si = -1; si < scopes.Count; si++)
            {
                var c = FBWeaponModel.Default(d); c.scope = si; yield return Equip(c);
                aim = true; yield return Wait(1.3f);
                string sn = si < 0 ? "ferro" : Json.S(scopes[si], "node"); var fp = P.GetComponentInChildren<FBFpRig>(true) ?? FindAnyObjectByType<FBFpRig>();
                Transform sight = null; if (fp) foreach (var t in fp.GetComponentsInChildren<Transform>(true)) if (t.name == sn && t.gameObject.activeInHierarchy) { sight = t; break; }
                string res = "sem mira medida";
                if (sight)
                {
                    Vector2 C = new(Screen.width / 2f, Screen.height / 2f); Vector3? ret = null, lens = null;
                    foreach (var r in sight.GetComponentsInChildren<Renderer>())
                    {
                        var m = r.sharedMaterial; string nm = (r.name + " " + (m ? m.name : "")).ToLowerInvariant();
                        bool reticle = System.Text.RegularExpressions.Regex.IsMatch(nm, "reticle|retic|dot|ponto|red|holo|mark|cross") || (m && m.IsKeywordEnabled("_EMISSION"));
                        bool alpha = m && (m.renderQueue >= 2450 || m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"));
                        if (reticle && ret == null) ret = r.bounds.center; else if (alpha && lens == null) lens = r.bounds.center;
                    }
                    string Px(Vector3? w) { if (w == null) return "-"; var sp = cam.WorldToScreenPoint(w.Value); return $"{Vector2.Distance(new Vector2(sp.x, sp.y), C):0.0} px ({sp.x - C.x:+0;-0},{sp.y - C.y:+0;-0})"; }
                    res = $"eixo óptico (SOCKET_Scope) {Px(fp ? fp.SightWorld : null)} · retículo {Px(ret)} · lente {Px(lens)} · topo da peça {Px(TopOf(sight))}";
                }
                Log($"mira {sn}: ADS {res} · zoom {FBScopes.Zoom(d):0.0}x · luneta do HUD {FBScopes.Scoped}");
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{d.id}_{si}_{sn}.png")); yield return null; yield return null;
                aim = false; yield return Wait(.4f);
            }
            // ---- 2. laser / lanterna só mirando
            var lasers = Json.L(att, "lasers") ?? new List<object>();
            for (int li = 0; li < lasers.Count; li++)
            {
                var c = FBWeaponModel.Default(d); c.laser = li; yield return Equip(c); string ln = Json.S(lasers[li], "node");
                aim = false; yield return Wait(.6f); int b0 = FBLaser.LitBeams, l0 = FBLaser.LitLamps;
                aim = true; yield return Wait(1.2f); int b1 = FBLaser.LitBeams, l1 = FBLaser.LitLamps;
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{d.id}_laser_{ln}.png")); yield return null; yield return null;
                aim = false; yield return Wait(.6f); int b2 = FBLaser.LitBeams;
                Log($"laser {ln}: sem mirar feixes {b0} lanternas {l0} · mirando feixes {b1} lanternas {l1} · soltou a mira feixes {b2}");
            }
            // ---- 3. empunhaduras: subida real da câmera em 15 tiros
            FBRecoil.TestNoSpread = true; var grips = Json.L(att, "grips") ?? new List<object>();
            var cases = new List<(string n, int g, bool crouch)> { ("nenhuma", -1, false) };
            for (int gi = 0; gi < grips.Count; gi++) { var gn = Json.S(grips[gi], "node"); cases.Add((gn, gi, false)); if (gn.Contains("Bipod")) cases.Add((gn + " agachado", gi, true)); }
            float baseRise = 0;
            foreach (var (n, g, cr) in cases)
            {
                var c = FBWeaponModel.Default(d); c.grip = g; yield return Equip(c);
                if (cr) { crouchT = true; yield return Wait(.6f); }
                aim = true; yield return Wait(1); float p0 = P.camPitch, y0 = P.camYaw; int a0 = P.wu.Ammo; fire = true;
                float maxRise = 0, maxSide = 0; for (float t = 0; t < 5 && a0 - P.wu.Ammo < 15; t += Time.deltaTime) { maxRise = Mathf.Max(maxRise, Mathf.Abs(Mathf.DeltaAngle(p0, P.camPitch))); maxSide = Mathf.Max(maxSide, Mathf.Abs(Mathf.DeltaAngle(y0, P.camYaw))); yield return null; }
                fire = false; int shots = a0 - P.wu.Ammo; yield return Wait(.2f); maxRise = Mathf.Max(maxRise, Mathf.Abs(Mathf.DeltaAngle(p0, P.camPitch)));
                if (g < 0) baseRise = maxRise;
                var md = FBWeaponModel.ModsOf(d, c, cr);
                Log($"empunhadura {n}: {shots} tiros · subida máx {maxRise:0.00}° ({(baseRise > 0 ? maxRise / baseRise * 100 : 100):0}% da sem nada) · lateral máx {maxSide:0.00}° · esperado recuo V×{md.recV:0.00} H×{md.recH:0.00}");
                aim = false; if (cr) { crouchT = true; yield return Wait(.6f); } yield return Wait(.8f);
            }
            FBRecoil.TestNoSpread = false; FBPlayer.TestInput = null; FBWeaponModel.Set(d, FBWeaponModel.Default(d));
            (FBSession.map, FBSession.mode, FBSession.view) = keep; FBSession.Save(); Log("fim"); yield return Wait(1); Application.Quit();
        }
    }
}
