// Instância de arma com os acessórios escolhidos (porte do makeWeapon/defaultAtt/scopeZoom do weapons.js): esconde os nós "off" do GLB
// (acessórios alternativos — o glTFast não importa extras, a lista vem do fb_data.json), liga só a mira/cano/carregador/laser/empunhadura
// escolhidos, mira padrão de ferro quando não há luneta, lente das lunetas transparente; devolve a mira (para o ADS) e o soquete do cano.
// + aumento de cada mira (2× = metade da tangente) e o estado da mira óptica (r51: imagem ampliada dentro da lente, FBOptic).
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public static class FBWeaponModel
    {
        public class Choice { public int scope = -1, muzzle, magazine, laser = -1, grip = -1, skin = -1; }
        static readonly Dictionary<string, Choice> chosen = new();
        // arma "virgem" (pedido de 21/10/2026): toda arma começa sem luneta, laser, empunhadura/bipé e sem silenciador/freio de boca — só
        // o cano de fábrica (nó "…Default" quando o modelo tem um) e o carregador padrão; a mira de ferro da própria arma (scopeDefault) fica.
        // Os acessórios entram pelo menu de armas (FBMenu) e valem só na sessão. Antes: 30 de 54 armas vinham com laser/empunhadura, P90 com silenciador.
        public static Choice Default(WeaponDef d)
        {
            var t = Json.O(Json.O(GameData.WeaponsRaw, d.id), "att"); var mz = Json.L(t, "muzzles"); int m0 = -1;
            if (mz != null) for (int i = 0; i < mz.Count; i++) if (Json.S(mz[i], "node", "").IndexOf("Default", System.StringComparison.OrdinalIgnoreCase) >= 0) { m0 = i; break; }
            // r51: sniper cuja luneta de fábrica não tem malha (sniper_hunting: nó vazio) sai com a 6x do LPSP — sniper sem luneta não tem mira óptica
            int sc = -1; var sl = Json.L(t, "scopes");
            if (d.IsSniper && FBOptic.On && FBOptic.Find(Json.S(t, "scopeDefault")) == null && sl != null) for (int i = 0; i < sl.Count; i++) if (Json.S(sl[i], "node") == "P_LPSP_WEP_ATT_Scope_06") sc = i;
            return new Choice { scope = sc, muzzle = m0, magazine = 0, laser = -1, grip = -1 };
        }
        public static Choice Of(WeaponDef d) => chosen.TryGetValue(d.id, out var c) ? c : Default(d);
        public static void Set(WeaponDef d, Choice c) { chosen[d.id] = c; }
        public static Dictionary<string, object> Att(WeaponDef d, string kind, int i) { var l = Json.L(Json.O(Json.O(GameData.WeaponsRaw, d.id), "att"), kind); return l != null && i >= 0 && i < l.Count ? l[i] as Dictionary<string, object> : null; }
        public static Dictionary<string, object> Scope(WeaponDef d) => Att(d, "scopes", Of(d).scope);
        // r45 (pedido: "empunhaduras e acessórios precisam ter efeito real no recuo e na dispersão; os atributos da tela iguais ao efeito"):
        // no pacote LPSP só as miras têm números (multiplierSpread/FOV); empunhadura, laser e cano eram só enfeite. Efeito por tipo de peça:
        //  · empunhadura vertical (Grip_01/Vertical): recuo vertical ×0,82, lateral ×0,90, dispersão sem mirar ×0,95
        //  · angulada: lateral ×0,78, vertical ×0,94
        //  · bipé: apoiado (agachado/deitado) recuo ×0,55/×0,60 e dispersão mirando ×0,70; em pé recuo ×0,95; mobilidade ×0,95
        //  · laser (só aceso mirando): dispersão mirando ×0,90; laser + lanterna ×0,93
        //  · freio de boca/compensador: vertical ×0,90, lateral ×0,88; silenciador: vertical ×0,95 (dano ×0,92 no FBWeaponUser)
        public struct Mods { public float recV, recH, spreadHip, spreadAds, move; public bool laser, flash, bipod; }
        public static Mods ModsOf(WeaponDef d, Choice c = null, bool braced = false)
        {
            var m = new Mods { recV = 1, recH = 1, spreadHip = 1, spreadAds = 1, move = 1 }; if (d == null) return m; c ??= Of(d);
            var g = Json.S(Att(d, "grips", c.grip), "node") ?? "";
            if (g.Contains("Bipod")) { m.bipod = true; m.move = .95f; if (braced) { m.recV = .55f; m.recH = .6f; m.spreadAds = .7f; } else m.recV = .95f; }
            else if (g.Contains("Angled")) { m.recH = .78f; m.recV = .94f; }
            else if (g.Length > 0) { m.recV = .82f; m.recH = .9f; m.spreadHip = .95f; }
            var l = Json.S(Att(d, "lasers", c.laser), "node") ?? "";
            if (l.Length > 0 && l != "None") { m.laser = true; m.flash = l.Contains("Flash") || l.Contains("Tactical"); m.spreadAds *= m.flash ? .93f : .9f; }
            var mz = Json.S(Att(d, "muzzles", c.muzzle), "node") ?? "";
            if (System.Text.RegularExpressions.Regex.IsMatch(mz, "Silencer|Supress", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) m.recV *= .95f;
            else if (System.Text.RegularExpressions.Regex.IsMatch(mz, "Brake|Comp|Freio", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) { m.recV *= .9f; m.recH *= .88f; }
            return m;
        }

        public static GameObject Make(WeaponDef d, Transform parent = null, Choice c = null)
        {
            var model = FBRegistry.I ? FBRegistry.I.Weapon(d.id) : null; if (!model) return null; c ??= Of(d);
            var go = parent ? Object.Instantiate(model, parent) : Object.Instantiate(model); go.name = "Arma_" + d.id;
            Apply(go, d, c); return go;
        }
        public static void Apply(GameObject go, WeaponDef d, Choice c)
        {
            var byName = new Dictionary<string, Transform>(); foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (!byName.ContainsKey(t.name)) byName[t.name] = t;
            Transform Get(string n) => n != null && byName.TryGetValue(n, out var t) ? t : null;
            var off = Json.L(Json.O(GameData.Root, "weaponOff"), d.id); if (off != null) foreach (var n in off) { var t = Get(n as string); if (t) t.gameObject.SetActive(false); }
            var att = Json.O(Json.O(GameData.WeaponsRaw, d.id), "att");
            void Pick(string kind, int idx) { var l = Json.L(att, kind); if (l == null) return; for (int i = 0; i < l.Count; i++) { var t = Get(Json.S(l[i], "node")); if (t) t.gameObject.SetActive(i == idx); } }
            Pick("scopes", c.scope); Pick("muzzles", c.muzzle); Pick("magazines", c.magazine); Pick("lasers", c.laser); Pick("grips", c.grip);
            var sd = Get(Json.S(att, "scopeDefault")); if (sd) sd.gameObject.SetActive(c.scope < 0 && Json.B(att, "scopeDefaultShow"));
            // laser: o traço fixo do modelo (SM_Laser_Beam) some — o feixe de verdade sai do SOCKET_Laser_Sight (FBLaser)
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (System.Text.RegularExpressions.Regex.IsMatch(t.name, "Beam", System.Text.RegularExpressions.RegexOptions.IgnoreCase) && t.GetComponent<Renderer>()) t.gameObject.SetActive(false);
            // lente transparente na luneta
            var sc = c.scope >= 0 ? Get(Json.S(Json.L(att, "scopes")?[c.scope], "node")) : null;
            if (sc) foreach (var r in sc.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials; bool ch = false;
                    for (int i = 0; i < mats.Length; i++) if (mats[i] && System.Text.RegularExpressions.Regex.IsMatch(r.name + " " + mats[i].name, "lens|glass|vidro", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        { var m = new Material(mats[i]); if (m.HasProperty("_BaseColor")) { var col = m.GetColor("_BaseColor"); col.a = .2f; m.SetColor("_BaseColor", col); } m.SetFloat("_Surface", 1); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent"); if (m.renderQueue < 3000) m.renderQueue = 3000; m.SetFloat("_ZWrite", 0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3000; mats[i] = m; ch = true; }
                    if (ch) r.sharedMaterials = mats;
                }
        }
        public static float Zoom(Dictionary<string, object> sc) { float f = Json.F(sc, "fieldOfViewMultiplierAim", .9f); return f >= .845f ? 1.12f : f >= .745f ? 1.3f : f >= .645f ? 1.5f : f >= .595f ? 2 : f >= .495f ? 4 : 6; }
    }

    // lunetas (r51): a luneta 2D preta do HUD saiu — mirar com mira óptica vai para a 1ª pessoa com a imagem ampliada dentro da lente
    // (FBOptic + FBFpRig.PipUpdate). Aqui ficam o aumento de cada mira e o estado da mira óptica para câmera, HUD, coice e tremor.
    public static class FBScopes
    {
        public static bool Scoped, Optic; public static float W;          // Optic = câmera já na mira óptica (≥ metade da transição); Scoped = e com luneta ≥ 2x
        static bool SniperFactory(WeaponDef w) => w.IsSniper && FBOptic.Find(Json.S(Json.O(Json.O(GameData.WeaponsRaw, w.id), "att"), "scopeDefault")) != null;
        public static float Zoom(WeaponDef w) => w == null ? 1 : FBWeaponModel.Zoom(FBWeaponModel.Scope(w) ?? (SniperFactory(w) ? new Dictionary<string, object> { ["fieldOfViewMultiplierAim"] = .45 } : null));
        // aumento da visão normal mirando: mira óptica = FBOptic.MainZoom (luneta: leve, o aumento real fica na lente); mira de ferro = o da arma
        public static float AdsZoom(WeaponDef w) { float z = Zoom(w); return FBOptic.Has(w) ? FBOptic.MainZoom(z) : z; }
        public static Vector3 AdsOffset(GameObject vm, Camera cam)
        {
            // alinha a mira (ou o topo da arma) ao eixo da câmera: o ponto de mira fica no centro da tela
            Transform sight = null; foreach (var t in vm.GetComponentsInChildren<Transform>()) if (t.name.Contains("Scope") && t.gameObject.activeInHierarchy) { sight = t; break; }
            var bounds = new Bounds(vm.transform.position, Vector3.zero); foreach (var r in vm.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            var top = sight ? sight.position : bounds.center + Vector3.up * bounds.extents.y * .55f; var local = cam.transform.InverseTransformPoint(top) - vm.transform.localPosition;
            return new Vector3(-local.x, -local.y, .3f);
        }
        public static void Set(bool optic, float w, float zoom) { W = w; Optic = optic; Scoped = optic && zoom >= 2; }
    }

    // braços de 1ª pessoa (fparms.js): por enquanto a arma de visão aparece sozinha; o porte do rig FP (wanims) entra aqui
    public static class FBFpArms { public static void Attach(GameObject vm, FBCharacter ch, WeaponDef w) { } }
}
