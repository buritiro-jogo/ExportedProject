// Autoteste da LOJA DE ARMAS do De Rolê (r68): BURITIRO.exe -fblojas [<mapa>[,<mapa>...]|todos]
// Em cada mapa abre o De Rolê offline, escolhe o carro da garagem, desce e, em cada loja:
//  · confere: nada solto dentro da caixa da loja (personagem, carro, objeto), frente do balcão na NavMesh, caminho a pé desde a base;
//  · prints (autoteste/loja_<mapa>_<n>_*.png): de longe, fachada, por dentro, o jogador no balcão (câmera do jogo, aviso "B"), GPS,
//    tela da loja, compra e o jogador armado na frente da loja; r68c: _antes_longe/_antes_fachada = a mesma câmera sem a loja;
//    r68e: a câmera de longe e a da fachada ficam na 1ª vista em volta da loja que a mostra de verdade (sem muro/árvore na frente) e as
//    fotos de cenário saem sem a interface;
//  · compra de teste com saldo de teste: a arma mais cara que o perfil não tem (desconta, fica sua, vai para a mão), comprar a mesma de
//    novo (= equipar, grátis), munição, colete e uma compra SEM saldo (tem que recusar).
// Grava loja_precos.json ao lado do exe (a tabela que o server.mjs lê) e o resumo em autoteste/lojas.txt. No fim devolve o saldo, as
// armas do perfil, o mapa e o modo do jogador (o teste não deixa nada no perfil).
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBLojaTeste : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fblojas"); if (i < 0) return;
            var g = new GameObject("FB_LojasTeste"); DontDestroyOnLoad(g); var t = g.AddComponent<FBLojaTeste>();
            t.arg = i + 1 < a.Length && !a[i + 1].StartsWith("-") ? a[i + 1] : "todos";
        }
        string arg, dir; System.Action restore; Camera cam; readonly List<string> rel = new(); int falhas;
        void OnApplicationQuit() => restore?.Invoke();
        void Log(string s) { Debug.Log("[FreeBala][lojas-teste] " + s); rel.Add(s); if (s.Contains("FALHA")) falhas++; }
        static IEnumerator Wait(float s) { for (float w = 0; w < s; w += Time.unscaledDeltaTime) yield return null; }
        IEnumerator Shot(string name) { yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "loja_" + name + ".png")); yield return null; yield return null; }
        void Look(Vector3 from, Vector3 at, float fov = 55) { cam.enabled = true; cam.transform.position = from; cam.transform.rotation = Quaternion.LookRotation(at - from); cam.fieldOfView = fov; }

        // r68e: fotos de cenário (antes × depois, por dentro) sem a interface: o aviso "B LOJA DE ARMAS" da loja anterior aparecia na foto ANTES.
        // r68f: desliga o OBJETO de cada tela (FBUI.Canvas: só Canvas + escala + raycaster); o Canvas.enabled sozinho não serve, o FBHUD o religa
        // a cada quadro no rolê (canvas.enabled = ... || FBGunShop.HudOn)
        List<GameObject> hud;
        void Hud(bool on)
        {
            if (!on) { hud = FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.activeSelf).Select(c => c.gameObject).ToList(); foreach (var g in hud) g.SetActive(false); }
            else if (hud != null) { foreach (var g in hud) if (g) g.SetActive(true); hud = null; }
        }
        // r68e: a câmera fixa (15 m à frente, 6 m de lado) caía atrás de muro ou de árvore em 6 de 28 fotos (antes e depois iguais). Agora cada
        // foto testa vistas em volta da loja (distância × ângulo × altura, a original primeiro) e fica com a 1ª que MOSTRA a loja: dentro do
        // retângulo da loja na tela, a fração de pixels que muda entre a loja escondida e à mostra (folha de árvore na frente = quase nada).
        float vis; Vector3 pick; float pickVis; string pickInfo;
        IEnumerator Muda(Vector3 from, Vector3 at, float fov, List<Renderer> predio, Bounds bb)
        {
            Look(from, at, fov); vis = 0; float x0 = 1, y0 = 1, x1 = 0, y1 = 0;
            for (int i = 0; i < 8; i++)
            {
                var v = cam.WorldToViewportPoint(bb.center + Vector3.Scale(bb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                if (v.z <= .1f) yield break;
                x0 = Mathf.Min(x0, v.x); y0 = Mathf.Min(y0, v.y); x1 = Mathf.Max(x1, v.x); y1 = Mathf.Max(y1, v.y);
            }
            x0 = Mathf.Clamp01(x0); y0 = Mathf.Clamp01(y0); x1 = Mathf.Clamp01(x1); y1 = Mathf.Clamp01(y1);
            if (x1 - x0 < .02f || y1 - y0 < .02f) yield break;
            foreach (var r in predio) if (r) r.enabled = false;
            yield return null; yield return null; yield return new WaitForEndOfFrame(); var a = ScreenCapture.CaptureScreenshotAsTexture();
            foreach (var r in predio) if (r) r.enabled = true;
            yield return null; yield return null; yield return new WaitForEndOfFrame(); var b = ScreenCapture.CaptureScreenshotAsTexture();
            var pa = a.GetPixels32(); var pb = b.GetPixels32(); int w = a.width, h = a.height, n = 0, tot = 0;
            for (int y = (int)(y0 * h); y < (int)(y1 * h); y += 3)
                for (int x = (int)(x0 * w); x < (int)(x1 * w); x += 3)
                {
                    int k = y * w + x; if (k >= pa.Length || k >= pb.Length) continue; var c0 = pa[k]; var c1 = pb[k]; tot++;
                    if (Mathf.Abs(c0.r - c1.r) + Mathf.Abs(c0.g - c1.g) + Mathf.Abs(c0.b - c1.b) > 48) n++;
                }
            Destroy(a); Destroy(b); vis = tot > 0 ? n / (float)tot : 0;
        }
        IEnumerator Escolhe(FBGunShop.Spot s, Vector3 alvo, float fov, float d0, float a0, float h0, float[] ds, float[] angs, float[] hs, List<Renderer> predio, Bounds bb)
        {
            const int M = 1 << FBWorld.LMap;
            var cands = new List<(float d, float a, float h)> { (d0, a0, h0) };
            foreach (var d in ds) foreach (var a in angs) foreach (var h in hs) if (!cands.Contains((d, a, h))) cands.Add((d, a, h));
            pick = s.pos + Quaternion.Euler(0, s.yaw + a0, 0) * Vector3.forward * d0 + Vector3.up * h0; pickVis = -1; pickInfo = "nenhuma vista livre (ficou a original)";
            int i = 0, testadas = 0;
            foreach (var (d, a, h) in cands)
            {
                i++; var p = s.pos + Quaternion.Euler(0, s.yaw + a, 0) * Vector3.forward * d + Vector3.up * h;
                if (Physics.CheckSphere(p, .35f, M, QueryTriggerInteraction.Ignore)) continue;                                                 // câmera dentro de parede
                if (Physics.Linecast(p, alvo, out var hit, M, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(s.go.transform)
                    && hit.distance < Vector3.Distance(p, alvo) - 3f) continue;                                                                  // muro na frente
                testadas++; yield return Muda(p, alvo, fov, predio, bb);
                if (vis > pickVis) { pickVis = vis; pick = p; pickInfo = $"vista {i} de {cands.Count} ({d:0.#} m, {a:0}°, {h:0.#} m de altura)"; }
                if (vis >= .35f || testadas >= 14) break;
            }
            if (pickVis < 0) pickVis = 0;
        }

        IEnumerator Start()
        {
            var root = System.IO.Path.GetDirectoryName(Application.dataPath);
            dir = System.IO.Path.Combine(root, "autoteste"); System.IO.Directory.CreateDirectory(dir);
            FBInput.TestMove = Vector2.zero; yield return Wait(3);
            GameData.Load(); FBSession.Load();
            int keepMoney = FBSession.money; string keepOwned = PlayerPrefs.GetString(FBGunShop.PREF, ""), keepMap = FBSession.map, keepMode = FBSession.mode;
            restore = () => { FBSession.money = keepMoney; PlayerPrefs.SetString(FBGunShop.PREF, keepOwned); FBGunShop.ReloadOwned(); FBSession.map = keepMap; FBSession.mode = keepMode; FBSession.Save(); };
            string[] order = { "vila_jardim", "praca_aguas", "praca_pt", "orla", "centro_civico", "morro_sacola", "bv_city" };
            var maps = arg == "todos" ? order.Where(m => GameData.AllowedMaps.Contains(m) && Application.CanStreamedLevelBeLoaded("Map_" + m)).ToArray() : arg.Split(',');
            Log($"mapas: {string.Join(", ", maps)} · saldo do perfil guardado $ {keepMoney} · armas do perfil guardadas: {(keepOwned == "" ? "nenhuma" : keepOwned)}");
            bool precos = false;
            foreach (var map in maps)
            {
                FBSession.map = map; FBSession.mode = "role"; FBSession.online = false; FBSession.view = "tps"; FBSession.Save();
                Log($"carregando {map}");
                var op = SceneManager.LoadSceneAsync("Map_" + map); if (op == null) { Log($"{map}: FALHA: cena não existe"); continue; }
                while (!op.isDone) yield return null;
                yield return Wait(5);
                FBGarage.AutoPick(); yield return Wait(2);
                FBMatch.God = true;
                if (!precos && FBGunShop.Catalog().Count > 0)
                {
                    var f = System.IO.Path.Combine(root, "loja_precos.json"); System.IO.File.WriteAllText(f, FBGunShop.PriceJson()); precos = true;
                    Log($"tabela de preços gravada em {f}: {FBGunShop.PriceTable().Count} itens · catálogo: {string.Join(", ", FBGunShop.Catalog().Select(d => d.id + " $" + FBEconomy.PriceOf(d.id)))}");
                }
                yield return Map(map);
            }
            restore?.Invoke(); restore = null;
            Log($"fim · {falhas} falha(s) · saldo e armas do perfil devolvidos ($ {FBSession.money})");
            try { System.IO.File.WriteAllLines(System.IO.Path.Combine(dir, "lojas.txt"), rel); } catch { }
            FBInput.TestMove = null; Application.Quit(0);
        }

        IEnumerator Map(string map)
        {
            var me = FBMatch.Player; if (!me) { Log($"{map}: FALHA: sem jogador"); yield break; }
            if (FBVehicles.CarOf(me)) { FBDrive.Exit(me, true); yield return Wait(1.2f); }
            var cat = FBGunShop.Catalog();
            Log($"{map}: {FBGunShop.shops.Count} loja(s) · catálogo {cat.Count} armas ({string.Join(", ", cat.GroupBy(d => d.cls).Select(g => g.Key + " " + g.Count()))}) · fora por modelo/nome repetido: {(FBGunShop.Duplicates.Count == 0 ? "nenhuma" : string.Join("; ", FBGunShop.Duplicates))}");
            if (FBGunShop.shops.Count == 0) { Log($"{map}: FALHA: nenhuma loja no mapa"); yield break; }
            var cg = new GameObject("LojaCam"); cam = cg.AddComponent<Camera>(); cam.depth = 100; cam.cullingMask = Camera.main ? Camera.main.cullingMask : ~0; cam.farClipPlane = 900; cam.nearClipPlane = .1f;
            cg.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
            int si = 0;
            foreach (var s in FBGunShop.shops.ToList())
            {
                si++; string tag = $"{map}_{si}"; if (!s.go) { Log($"{tag}: FALHA: loja sem objeto"); continue; }
                var q = Quaternion.Euler(0, s.yaw, 0); var F = q * Vector3.forward; var R = q * Vector3.right;
                var dentro = Physics.OverlapBox(s.pos + Vector3.up * (FBGunShop.H * .5f + .15f), new Vector3(FBGunShop.W * .5f - .2f, FBGunShop.H * .5f - .2f, FBGunShop.D * .5f - .2f), q,
                        (1 << FBWorld.LChar) | (1 << FBWorld.LVehicle) | 1, QueryTriggerInteraction.Ignore)
                    .Where(h => !h.transform.IsChildOf(s.go.transform)).Select(h => h.transform.root.name).Distinct().ToList();
                bool nav = NavMesh.SamplePosition(s.front, out _, .8f, NavMesh.AllAreas);
                bool modelo = s.go.transform.Find("modelo");
                Log($"{tag}: loja em ({s.pos.x:0.0}, {s.pos.y:0.00}, {s.pos.z:0.0}) rumo {s.yaw:0}° · {(s.wall ? "encostada na parede" : "solta")} · caminho a pé da base {s.path:0} m · balcão na NavMesh {nav} · modelo {(modelo ? "Blender (loja_armas.glb)" : "BLOCOS (sem o .glb)")}" +
                    $" · dentro da caixa: {(dentro.Count == 0 ? "nada" : "FALHA " + string.Join(", ", dentro))}");
                if (!nav) Log($"{tag}: FALHA: frente do balcão fora da NavMesh");
                var c = s.pos + Vector3.up * 1.7f; var fAlvo = s.pos + Vector3.up * 1.9f;
                // r68c: o marcador (feixe + "LOJA") se ajusta à câmera do JOGADOR (longe, na base) e saía gigante na frente da fachada;
                // no jogo ele some a menos de 22 m, então fica escondido em todas as fotos da câmera do teste
                var mk = s.mk ? s.mk.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToList() : new List<Renderer>();
                foreach (var r in mk) r.enabled = false;
                // r68c: ANTES (a mesma câmera, sem o prédio da loja) × DEPOIS
                var predio = s.go.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToList();
                var bb = new Bounds(s.pos + Vector3.up * FBGunShop.H * .5f, new Vector3(FBGunShop.W, FBGunShop.H, FBGunShop.D)); foreach (var r in predio) bb.Encapsulate(r.bounds);
                Hud(false);
                // r68e: vista que mostra a loja (original = 15 m à frente do balcão e 6 m de lado, a 5,5 m; fachada a 7,2 m e 4,4 m de lado, a 2,3 m)
                float dl = new Vector2(FBGunShop.D * .5f + 16.1f, 6).magnitude, al = Mathf.Atan2(6, FBGunShop.D * .5f + 16.1f) * Mathf.Rad2Deg;
                yield return Escolhe(s, c, 50, dl, al, 5.5f, new[] { dl, 13f, 24f, 9f }, new[] { al, -al, 0, 40, -40, 65, -65 }, new[] { 5.5f, 9f }, predio, bb);
                var longe = pick; Log($"{tag}: foto de longe: {pickInfo} · a loja aparece em {pickVis * 100:0}% do retângulo dela");
                float df = new Vector2(7.2f, 4.4f).magnitude, af = Mathf.Atan2(4.4f, 7.2f) * Mathf.Rad2Deg;
                yield return Escolhe(s, fAlvo, 55, df, af, 2.3f, new[] { df, 6.5f, 11f }, new[] { af, -af, 0, 50, -50 }, new[] { 2.3f, 3.6f }, predio, bb);
                var fachada = pick; Log($"{tag}: foto da fachada: {pickInfo} · a loja aparece em {pickVis * 100:0}% do retângulo dela");
                foreach (var r in predio) if (r) r.enabled = false;
                Look(longe, c, 50); yield return Wait(.5f); yield return Shot(tag + "_antes_longe");
                Look(fachada, fAlvo, 55); yield return Wait(.3f); yield return Shot(tag + "_antes_fachada");
                foreach (var r in predio) if (r) r.enabled = true;
                Look(longe, c, 50); yield return Wait(.5f); yield return Shot(tag + "_longe");
                Look(fachada, fAlvo, 55); yield return Wait(.3f); yield return Shot(tag + "_fachada");
                Look(s.pos + F * (FBGunShop.D * .5f + .5f) + Vector3.up * 1.62f, s.pos - F * 1.7f + Vector3.up * 1.75f, 64); yield return Wait(.2f); yield return Shot(tag + "_dentro");
                Hud(true);
                foreach (var r in mk) if (r) r.enabled = true;
                cam.enabled = false;
                float yaw = s.yaw + 180; FBMatch.Teleport(me, s.front, yaw); FBPlayer.I?.OnRespawn(yaw); yield return Wait(1.6f);
                bool near = FBGunShop.Near;
                Log($"{tag}: jogador no balcão: loja perto {near} · a {(s.front - me.Pos).Flat().magnitude:0.00} m da frente");
                if (!near) Log($"{tag}: FALHA: o balcão não reconhece o jogador");
                yield return Shot(tag + "_balcao");
                if (si == 1) { FBGps.ToggleBig(); yield return Wait(.5f); yield return Shot(tag + "_gps"); FBGps.ToggleBig(); yield return Wait(.3f); }
                if (near) yield return Compras(tag, me, s);
                // r68e: "saiu armado" de frente: o jogador vira para a rua, 1,2 m fora do balcão (antes ficava de costas para a câmera, com a
                // arma escondida na frente do corpo); câmera dentro de parede = volta para a vista antiga
                FBMatch.Teleport(me, s.front + F * 1.2f, s.yaw); FBPlayer.I?.OnRespawn(s.yaw); yield return Wait(1.2f);
                var ca = me.Pos + F * 4.6f + R * 1.8f + Vector3.up * 1.75f;
                if (Physics.CheckSphere(ca, .35f, 1 << FBWorld.LMap, QueryTriggerInteraction.Ignore)) ca = s.front + F * 4.2f + R * 2.6f + Vector3.up * 1.9f;
                Look(ca, me.Pos + Vector3.up * 1.25f, 50); yield return Wait(.4f); yield return Shot(tag + "_armado_lado");
                cam.enabled = false;
            }
            Destroy(cg);
        }

        IEnumerator Compras(string tag, FBCharacter me, FBGunShop.Spot s)
        {
            var wu = me.GetComponent<FBWeaponUser>();
            FBSession.money = 60000;                                                                 // saldo de teste (o do perfil volta no fim)
            FBGunShop.OpenStore(); yield return Wait(.7f);
            if (!FBShop.Open || !FBShop.Store) { Log($"{tag}: FALHA: a tela da loja não abriu"); yield break; }
            yield return Shot(tag + "_tela");
            var data = FBGunShop.ShopData(me);
            var weapons = data.Where(c => c.cat != "EQUIPAMENTO").SelectMany(c => c.items.Select(i => (cat: c.cat, it: i))).ToList();
            Log($"{tag}: vitrine: {string.Join(" · ", data.Select(c => c.cat + " " + c.items.Count))}");
            var pick = weapons.Where(x => !x.it.own).OrderByDescending(x => x.it.price).FirstOrDefault();
            if (pick.it == null) { Log($"{tag}: todas as armas já são do perfil (nada para comprar)"); FBShop.Show(false); yield break; }
            FBShop.TestSelect(pick.cat, pick.it.id); yield return Wait(.4f); yield return Shot(tag + "_tela_arma");
            int m0 = FBSession.money; FBGunShop.Buy(pick.it); yield return Wait(.6f);
            int m1 = FBSession.money; bool owns = FBGunShop.Owns(pick.it.id); string cur = me.weapon?.id;
            bool ok1 = m1 == m0 - pick.it.price && owns && cur == pick.it.id;
            Log($"{tag}: {(ok1 ? "OK" : "FALHA")} comprou {pick.it.id} ({pick.it.name}) por $ {pick.it.price}: saldo {m0} → {m1} (esperado {m0 - pick.it.price}) · é sua {owns} · na mão {cur}");
            yield return Shot(tag + "_comprou");
            // a mesma de novo = equipar (grátis); antes troca para outro slot se tiver
            int m2 = FBSession.money; FBGunShop.Buy(pick.it); yield return Wait(.3f);
            Log($"{tag}: {(FBSession.money == m2 ? "OK" : "FALHA")} comprar a mesma arma de novo = equipar: saldo {m2} → {FBSession.money} (esperado sem mudar)");
            // munição (esvazia o pente e a reserva antes)
            var gear = data.First(c => c.cat == "EQUIPAMENTO").items;
            int sl = wu.cur; if (sl >= 0 && wu.slots[sl] != null)
            {
                wu.ammo[sl] = 1; wu.reserve[sl] = 0; int m3 = FBSession.money; FBGunShop.Buy(gear.First(i => i.id == "ammo")); yield return Wait(.3f);
                bool ok3 = FBSession.money == m3 - FBGunShop.AMMO_PRICE && wu.ammo[sl] == wu.slots[sl].mag && wu.reserve[sl] > 0;
                Log($"{tag}: {(ok3 ? "OK" : "FALHA")} munição $ {FBGunShop.AMMO_PRICE}: saldo {m3} → {FBSession.money} · pente {wu.ammo[sl]}/{wu.slots[sl].mag} · reserva {wu.reserve[sl]}");
            }
            me.armor = 0; int m4 = FBSession.money; FBGunShop.Buy(gear.First(i => i.id == "colete")); yield return Wait(.3f);
            Log($"{tag}: {(FBSession.money == m4 - FBGunShop.GearPrice("colete") && me.armor >= 100 ? "OK" : "FALHA")} colete: saldo {m4} → {FBSession.money} · blindagem {me.armor:0}");
            // sem saldo: tem que recusar e não dar nada
            var dear = weapons.Where(x => !FBGunShop.Owns(x.it.id) && x.it.price > 10).OrderByDescending(x => x.it.price).FirstOrDefault();
            if (dear.it != null)
            {
                int mAntes = FBSession.money; FBSession.money = 10; FBGunShop.Buy(dear.it); yield return Wait(.3f);
                Log($"{tag}: {(FBSession.money == 10 && !FBGunShop.Owns(dear.it.id) ? "OK" : "FALHA")} sem saldo ({dear.it.id} $ {dear.it.price} com $ 10): saldo {FBSession.money} · ficou sua {FBGunShop.Owns(dear.it.id)} (esperado False)");
                FBSession.money = mAntes;                                                                // r68e: volta o saldo de antes (a foto do equipamento mostrava $ 60000 com o colete já comprado)
            }
            FBShop.TestSelect("EQUIPAMENTO", "kit"); yield return Wait(.4f); yield return Shot(tag + "_tela_equip");
            FBShop.Show(false); yield return Wait(4.2f);                                              // r68d: o aviso da compra SEM saldo some antes das fotos de armado
            Log($"{tag}: depois de fechar: armado {FBGunShop.Armed} · HUD do rolê ligado {FBGunShop.HudOn}");
            yield return Shot(tag + "_armado");
        }
    }
}
