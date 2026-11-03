// Autoteste do DE ROLÊ (sem clicar): FreeBala.exe -fbrole <mapa>[,<mapa>...] [segundos] [-fbnight]
// Abre o mapa no rolê, pega o carro da garagem sozinho e mede, gravando no log ([FreeBala][rolê-teste]) e prints em autoteste/:
//  · trânsito: carros andando, velocidade média, SOBREPOSIÇÃO entre carros (caixas que se cruzam), carro FORA DO ASFALTO, longe da faixa,
//    travados >20 s, estacionando/saindo de vaga, contêineres de cobertura fora;
//  · pedestres: quantos andando no asfalto (devia ser só atravessando) e parados presos;
//  · luzes (com -fbnight): carros com farol/lanterna acesos, freio aceso parados;
//  · paredão: parâmetros das vozes (colunas, ecos, reverb) e o NÍVEL REAL na saída de áudio (AudioListener.GetOutputData) com o
//    ouvido a 4, 15, 40, 90 e 160 m e de lado (esquerda/direita) — tem que cair com a distância e pender para o lado certo.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBRoleAutoTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbrole"); if (i < 0 || i + 1 >= a.Length) return;
            var g = new GameObject("FB_RoleTeste"); DontDestroyOnLoad(g); var t = g.AddComponent<FBRoleAutoTest>(); t.maps = a[i + 1].Split(',');
            if (i + 2 < a.Length && float.TryParse(a[i + 2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s)) t.secs = s;
            t.night = System.Array.IndexOf(a, "-fbnight") >= 0;
        }
        string[] maps; float secs = 40; bool night; string dir; System.Action restore;
        void OnApplicationQuit() => restore?.Invoke();
        static void Log(string s) => Debug.Log("[FreeBala][rolê-teste] " + s);
        IEnumerator Start()
        {
            dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste"); System.IO.Directory.CreateDirectory(dir);
            FBInput.TestMove = Vector2.zero;                                                    // teclado/controle de quem está no PC não dirige o carro do teste
            yield return new WaitForSeconds(3);
            foreach (var map in maps)
            {
                GameData.Load(); FBSession.Load(); var keepMap = FBSession.map; var keepMode = FBSession.mode; var keepW = FBSession.WeatherFor(map);
                FBSession.map = map; FBSession.mode = "role"; FBSession.online = false; FBSession.view = "tps";
                if (night) FBSession.SetWeather(map, "noite", "limpo"); FBSession.Save();
                restore = () => { FBSession.SetWeather(map, keepW.time, keepW.weather); FBSession.map = keepMap; FBSession.mode = keepMode; FBSession.Save(); };   // não deixa o teste mudar as escolhas do jogador
                Log($"carregando {map}{(night ? " (noite)" : "")}");
                var op = SceneManager.LoadSceneAsync("Map_" + map); if (op == null) { Log("FALHA: cena não existe"); continue; }
                while (!op.isDone) yield return null;
                for (float w = 0; w < 5; w += Time.unscaledDeltaTime) yield return null;
                // rodada 38: nasce DENTRO do carro da prévia, sem controle até escolher, com o cursor visível na escolha
                { var P0 = FBMatch.Player; var c0 = P0 ? FBVehicles.CarOf(P0) : null;
                  Log($"{map}: garagem aberta {FBGarage.Open_} · jogador já dentro do carro {(c0 ? c0.id : "NÃO")} · cursor {(Cursor.visible ? "visível" : "ESCONDIDO")} ({Cursor.lockState})");
                  ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"r38_{map}_garagem.png")); yield return null; yield return null;
                  if (c0) { var p0 = c0.transform.position; FBInput.TestMove = Vector2.up; for (float w = 0; w < 1.5f; w += Time.unscaledDeltaTime) yield return null; FBInput.TestMove = Vector2.zero;
                    Log($"{map}: com a garagem aberta, acelerando 1,5 s o carro andou {(c0 ? (c0.transform.position - p0).Flat().magnitude : -1):0.00} m (esperado ~0)"); } }
                FBGarage.AutoPick(); for (float w = 0; w < 2; w += Time.unscaledDeltaTime) yield return null;
                { var c1 = FBMatch.Player ? FBVehicles.CarOf(FBMatch.Player) : null; Log($"{map}: depois de escolher: dentro do carro {(c1 ? c1.id : "NÃO")} · garagem aberta {FBGarage.Open_} · cursor {(Cursor.visible ? "visível" : "escondido")}"); }
                int cont = 0; foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (t.gameObject.layer == FBWorld.LMap && FBRoads.IsCoverContainer(t)) cont += t.gameObject.activeInHierarchy ? 1000 : 1;
                Log($"{map}: malha {(FBRoads.Ready ? FBRoads.segs.Count + " segmentos, " + FBRoads.spots.Count + " vagas" : "AUSENTE")} · contêineres de cobertura: {cont % 1000} desligados, {cont / 1000} ainda ligados");
                yield return Traffic(map);
                { var fora = FBRole.npcs.Where(n => n && n.alive && !FBNav.InsideNear(n.Pos, 2f)).ToList();
                  Log($"{map}: NPCs fora da área jogável (atrás do muro): {fora.Count} de {FBRole.npcs.Count(n => n)}");
                  var cg2 = new GameObject("ForaCam"); var cam2 = cg2.AddComponent<Camera>(); cam2.depth = 100; cam2.cullingMask = Camera.main ? Camera.main.cullingMask : ~0; cam2.fieldOfView = 60; cam2.farClipPlane = 800; int fi = 0;
                  foreach (var n in fora.Take(4))
                  {
                      var rn = n.GetComponent<FBRoleNpc>(); bool onNav = UnityEngine.AI.NavMesh.SamplePosition(n.Pos, out var nh, 3, UnityEngine.AI.NavMesh.AllAreas);
                      Log($"{map}:   fora #{fi}: {n.skinId} em {n.Pos} · estado {rn?.st} · navmesh a {(onNav ? (nh.position - n.Pos).magnitude.ToString("0.00") + " m" : ">3 m")} · motorista/pedestre do trânsito {(rn == null || !rn.enabled)} · sentado {rn?.seat != null}");
                      cam2.transform.position = n.Pos + new Vector3(0, 14, -9); cam2.transform.LookAt(n.Pos); for (int k = 0; k < 4; k++) yield return null;
                      ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"r35_{map}_fora{fi++}.png")); yield return null; yield return null;
                  }
                  Destroy(cg2); }
                yield return Shots(map);
                yield return Paredao(map);
                yield return Damage(map);
                if (map == maps[0]) yield return Round35(map);
                restore?.Invoke(); restore = null;
            }
            FBInput.TestMove = null; Log("fim"); Application.Quit(0);
        }

        // ---- trânsito e pedestres
        IEnumerator Traffic(string map)
        {
            int samples = 0, overlaps = 0, offroad = 0, offlane = 0, pedRoad = 0, pedTot = 0, moving = 0; float vSum = 0; var stuck = new Dictionary<object, float>(); var seenModes = new Dictionary<string, int>();
            var lit = 0; var brakeStill = 0; var stillCount = 0; var ovKinds = new Dictionary<string, int>();
            for (float t = 0; t < secs; t += .25f)
            {
                yield return new WaitForSeconds(.25f); samples++;
                var tl = FBTransito.List.Where(c => c.car && c.mode != "walk").ToList();
                foreach (var c in tl)
                {
                    seenModes[c.mode] = seenModes.TryGetValue(c.mode, out var k) ? k + 1 : 1;
                    if (c.mode != "drive") continue;
                    vSum += c.v; moving++;
                    if (!FBRoads.IsRoad(c.pos)) offroad++;
                    if (c.ri < c.route.Count) { var S = FBRoads.segs[c.route[c.ri]]; FBRoads.Project(S, c.pos, out var lat, out var dist); if (dist > 1.6f && Mathf.Abs(c.lat) < .3f) offlane++; }
                    if (c.v < .3f) { stuck[c] = (stuck.TryGetValue(c, out var st) ? st : 0) + .25f; stillCount++; if (c.car.lBrake) brakeStill++; } else stuck[c] = 0;
                    if (night && c.car.aiLights) lit++;
                }
                // sobreposição: caixa de cada carro do trânsito contra todos os veículos
                foreach (var c in tl)
                    foreach (var o in FBVehicles.list)
                    {
                        if (!o || o == c.car) continue; if ((o.transform.position - c.car.transform.position).sqrMagnitude > 64) continue;
                        if (Overlap(c.car, o)) { overlaps++; var om = tl.Find(x => x.car == o); var key = c.mode + "×" + (om != null ? om.mode : o.driver ? (o.driver.isPlayer ? "jogador" : "motorista") : "parado"); ovKinds[key] = ovKinds.TryGetValue(key, out var kk) ? kk + 1 : 1; }
                    }
                foreach (var p in FBRole.npcs) { if (!p || !p.alive) continue; pedTot++; if (FBRoads.IsRoad(p.Pos) && p.velocity.sqrMagnitude < .04f) pedRoad++; }
            }
            int st20 = stuck.Values.Count(v => v > 20);
            Log($"{map}: trânsito {FBTransito.List.Count} carros · {seenModes.Select(kv => kv.Key + " " + kv.Value).Aggregate("", (a, b) => a + b + " ")}· v média {(moving > 0 ? vSum / moving * 3.6f / 1.2f : 0):0} km/h reais · " +
                $"sobreposições {overlaps}{(ovKinds.Count > 0 ? " (" + string.Join(", ", ovKinds.Select(kv => kv.Key + " " + kv.Value)) + ")" : "")} · fora do asfalto {offroad}/{moving} · longe da faixa {offlane} · travados >20 s {st20} · parados com freio {brakeStill}/{stillCount}" + (night ? $" · com luz {lit}" : ""));
            Log($"{map}: pedestres amostras {pedTot} · parados no asfalto {pedRoad} ({(pedTot > 0 ? 100f * pedRoad / pedTot : 0):0.0} %)");
        }
        static bool Overlap(FBCar a, FBCar b)
        {
            Vector2[] C(FBCar c) { var L = c.lbox; var e = L.extents * c.scale * .92f; var ctr = c.transform.TransformPoint(L.center); var f = c.transform.forward; f.y = 0; f.Normalize(); var r = new Vector3(f.z, 0, -f.x); var p = new Vector2(ctr.x, ctr.z); var fv = new Vector2(f.x, f.z) * e.z; var rv = new Vector2(r.x, r.z) * e.x; return new[] { p + fv + rv, p + fv - rv, p - fv - rv, p - fv + rv }; }
            var A = C(a); var B = C(b);
            foreach (var poly in new[] { A, B })
                for (int i = 0; i < 4; i++)
                {
                    var e = poly[(i + 1) % 4] - poly[i]; var n = new Vector2(-e.y, e.x);
                    float a0 = float.MaxValue, a1 = float.MinValue, b0 = float.MaxValue, b1 = float.MinValue;
                    foreach (var q in A) { float d = Vector2.Dot(q, n); a0 = Mathf.Min(a0, d); a1 = Mathf.Max(a1, d); }
                    foreach (var q in B) { float d = Vector2.Dot(q, n); b0 = Mathf.Min(b0, d); b1 = Mathf.Max(b1, d); }
                    if (a1 < b0 || b1 < a0) return false;
                }
            return true;
        }

        // ---- prints: de cima do trânsito, atrás de um carro chegando no cruzamento, traseira/frente à noite
        IEnumerator Shots(string map)
        {
            var cg = new GameObject("RoleCam"); var cam = cg.AddComponent<Camera>(); cam.depth = 100; if (Camera.main) cam.cullingMask = Camera.main.cullingMask; cam.fieldOfView = 55; cam.farClipPlane = 1500;
            var data = cg.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); data.renderPostProcessing = true;
            IEnumerator Shot(Vector3 pos, Vector3 look, string name) { cam.transform.position = pos; cam.transform.LookAt(look); for (int k = 0; k < 4; k++) yield return null; ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"role_{map}{(night ? "_noite" : "")}_{name}.png")); yield return null; yield return null; }
            var drive = FBTransito.List.Where(c => c.car && c.mode == "drive").ToList();
            for (int i = 0; i < Mathf.Min(3, drive.Count); i++)
            {
                var c = drive[i * drive.Count / Mathf.Max(1, Mathf.Min(3, drive.Count))]; if (!c.car) continue; var p = c.pos;
                yield return Shot(p + Vector3.up * 38 - c.fwd * 22, p + c.fwd * 12, "cima" + i);
                p = c.pos; yield return Shot(p - c.fwd * 9 + Vector3.up * 3.2f, p + c.fwd * 10, "atras" + i);
                p = c.pos; yield return Shot(p + c.fwd * 8 + Vector3.up * 1.6f + new Vector3(c.fwd.z, 0, -c.fwd.x) * 2, p, "frente" + i);
            }
            // pedestres: de cima de onde tem mais gente
            var ped = FBRole.npcs.Where(n => n && n.alive).ToList();
            if (ped.Count > 0) { var pc = ped[ped.Count / 2].Pos; yield return Shot(pc + new Vector3(14, 22, -14), pc, "pedestres"); }
            Destroy(cg);
        }

        // ---- portas/capô/mala, batida (amassado, farol, vidro, capô empenado), poste derrubado, tiro na luminária, oficina
        IEnumerator Damage(string map)
        {
            var car = FBRole.roleCar; if (!car) { Log($"{map}: dano: sem carro do rolê"); yield break; }
            var me = FBMatch.Player; if (me && FBVehicles.CarOf(me)) { car.v = 0; FBDrive.Exit(me, true); } car.v = 0;
            for (float w = 0; w < 1.2f; w += Time.unscaledDeltaTime) yield return null;
            var cg = new GameObject("DanoCam"); var cam = cg.AddComponent<Camera>(); cam.depth = 100; cam.cullingMask = Camera.main ? Camera.main.cullingMask : ~0; cam.fieldOfView = 50; cam.farClipPlane = 800;
            var data = cg.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); data.renderPostProcessing = true;
            IEnumerator Shot(Vector3 pos, Vector3 look, string name) { cam.transform.position = pos; cam.transform.LookAt(look); for (int k = 0; k < 5; k++) yield return null; ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"role_{map}{(night ? "_noite" : "")}_{name}.png")); yield return null; yield return null; }
            var t = car.transform; var L = car.lbox; var ctr = t.TransformPoint(L.center); var side = t.right;
            // 1) portas, capô e mala abertas (dados do Animator do REB)
            bool has = FBDoors.Has(car); FBDoors.SetMask(car, FBDoors.DOORS | FBDoors.HOOD | FBDoors.TRUNK, true);
            for (float w = 0; w < 1.6f; w += Time.unscaledDeltaTime) yield return null;
            yield return Shot(ctr + t.forward * 5.5f + side * 4 + Vector3.up * 2.2f, ctr, "portas");
            yield return Shot(ctr - t.forward * 6 - side * 3.5f + Vector3.up * 2.4f, ctr, "portas_tras");
            Log($"{map}: portas ({car.id}): dados {(has ? "sim" : "NÃO")} · {FBDoors.DebugInfo(car)}");
            FBDoors.SetMask(car, 0, true); for (float w = 0; w < 1.3f; w += Time.unscaledDeltaTime) yield return null;
            // 2) batida de frente a 16 m/s + dois tiros no para-brisa
            var front = t.TransformPoint(new Vector3(L.center.x - L.extents.x * .45f, L.center.y, L.max.z));
            FBCarDamage.OnImpact(car, front, -t.forward, 16f); yield return null;
            for (float w = 0; w < .6f; w += Time.unscaledDeltaTime) yield return null;
            yield return Shot(t.TransformPoint(new Vector3(L.center.x - L.extents.x * 2.2f, L.max.y * .9f, L.max.z + 2.4f)), front, "dano_quina");
            var shotP = t.TransformPoint(new Vector3(L.center.x, L.max.y - .3f, L.center.z + L.extents.z * .3f));
            FBCarDamage.OnShot(car, shotP, -t.forward, 30); yield return null; FBCarDamage.OnShot(car, shotP, -t.forward, 30);
            for (float w = 0; w < 1.8f; w += Time.unscaledDeltaTime) yield return null;
            yield return Shot(ctr + t.forward * 6.2f + side * 2.6f + Vector3.up * 1.5f, front, "dano_frente");
            Log($"{map}: dano: vida {FBCarDamage.Hp(car):0} · lâmpadas quebradas {FBCarLights.DeadCount(car)} · {FBCarDamage.DebugInfo(car)} · {FBDoors.DebugInfo(car)}");
            // 3) poste mais perto do carro, batida a 10 m/s (quebra e cai)
            var pole = FBPole.All.Where(p => p && p.isActiveAndEnabled).OrderBy(p => (p.basePos - ctr).sqrMagnitude).FirstOrDefault();
            if (pole)
            {
                var d = (pole.basePos - ctr).Flat(); d = d.sqrMagnitude > .01f ? d.normalized : Vector3.forward; pole.Hit(d, 10f, pole.basePos + Vector3.up * .8f);
                for (float w = 0; w < 3f; w += Time.unscaledDeltaTime) yield return null;
                yield return Shot(pole.basePos - d * 10 + new Vector3(d.z, 0, -d.x) * 6 + Vector3.up * 4.5f, pole.basePos + d * 3, "poste");
                Log($"{map}: poste {pole.name}: caiu {pole.down} · inclinação {pole.tilt:0}° · {FBPole.All.Count} postes móveis na cena");
            }
            else Log($"{map}: poste: nenhum FBPole na cena (FBPolePrep não rodou nesta cena?)");
            // 4) tiro passando na luminária mais perto
            int li = FBStreetLights.Nearest(ctr, 120);
            if (li >= 0) { var lp = FBStreetLights.Lamps[li].p; FBStreetPoleFx.Ray(lp + Vector3.down * 3, Vector3.up, 6); Log($"{map}: luminária {li} ({lp}) apagada pelo tiro: {FBStreetLights.IsDead(li)}"); }
            else Log($"{map}: luminária: nenhuma");
            // 5) oficina conserta
            FBCarDamage.Repair(car); for (float w = 0; w < .5f; w += Time.unscaledDeltaTime) yield return null;
            Log($"{map}: oficina: vida {FBCarDamage.Hp(car):0} · lâmpadas quebradas {FBCarLights.DeadCount(car)} · {FBCarDamage.DebugInfo(car)}");
            Destroy(cg);
        }

        // ---- rodada 35: NPCs do pacote novo (clipes por personagem), peças do REB, viaturas/Clio rebaixados, zoom da câmera, oficina
        static FBRoleNpc rn2(FBCharacter n) => n.GetComponent<FBRoleNpc>();
        IEnumerator Round35(string map)
        {
            // NPCs: elenco, clipes de cada um existem? alguém usa clipe do outro sexo?
            var ros = FBNpcAnims.Roster(); var bySkin = FBRole.npcs.Where(n => n).GroupBy(n => n.skinId).ToDictionary(g => g.Key, g => g.Count());
            int miss = 0, mix = 0, foreign = 0; var clipSex = new Dictionary<string, string>();
            foreach (var o in Json.L(GameData.Npc2, "clips") ?? new List<object>()) clipSex[Json.S(o, "id")] = Json.S(o, "sex");
            foreach (var n in FBRole.npcs.Where(n => n && n.npcSet != null))
            {
                var all = n.npcSet.idle.Concat(n.npcSet.talk).Concat(n.npcSet.sit).Concat(n.npcSet.dance).Append(n.npcSet.walk).Where(x => x != null).ToList();
                miss += all.Count(x => !FBRegistry.I.Clip(x)); mix += all.Count(x => clipSex.TryGetValue(x, out var sx) && sx != n.npcSet.sex);
                foreign += all.Count(x => !x.StartsWith("NPC2_"));
            }
            int fora = FBRole.npcs.Count(n => n && n.alive && !FBNav.InsideNear(n.Pos, 2f));
            Log($"{map}: NPCs fora da área jogável (atrás do muro de limite): {fora} de {FBRole.npcs.Count(n => n)}");
            Log($"{map}: NPCs pacote novo: elenco {ros.Count} [{string.Join(", ", ros)}] · em cena {FBRole.npcs.Count(n => n)} ({string.Join(", ", bySkin.Select(kv => kv.Key + " " + kv.Value))}) · clipes faltando {miss} · de outro sexo {mix} · fora do pacote {foreign}");
            Log($"{map}: elenco em cena (r52) · {FBElenco.SceneCheck()} · {FBElenco.Summary()}");
            foreach (var id in ros) { var c = FBRole.npcs.FirstOrDefault(n => n && n.skinId == id); if (c && c.npcSet != null) Log($"{map}: NPC {id} ({c.npcSet.sex}): pés no chão +{c.GroundLift * 100:0.0} cm · andar {c.npcSet.walk} · parados {string.Join("/", c.npcSet.idle)} · conversa {c.npcSet.talk.Count} · senta {c.npcSet.sit.Count} · dança {c.npcSet.dance.Count}"); }
            var cg = new GameObject("R35Cam"); var cam = cg.AddComponent<Camera>(); cam.depth = 100; cam.cullingMask = Camera.main ? Camera.main.cullingMask : ~0; cam.fieldOfView = 45; cam.farClipPlane = 800;
            cg.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
            IEnumerator Shot(Vector3 pos, Vector3 look, string name) { cam.enabled = true; cam.transform.position = pos; cam.transform.LookAt(look); for (int k = 0; k < 5; k++) yield return null; ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"r35_{map}_{name}.png")); yield return null; yield return null; }
            foreach (var id in ros.Take(10)) { var c = FBRole.npcs.FirstOrDefault(n => n && n.skinId == id && n.alive); if (!c) continue; var look = c.Pos + Vector3.up * 1.1f; var best = Quaternion.Euler(0, c.Yaw, 0) * Vector3.forward;
                foreach (var a in new[] { 0, 60, -60, 120, -120, 180 }) { var dd = Quaternion.Euler(0, c.Yaw + a, 0) * Vector3.forward; if (!Physics.Raycast(look, (dd * 3.2f + Vector3.up * .5f).normalized, 3.4f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore)) { best = dd; break; } }   // lado sem parede
                yield return Shot(c.Pos + best * 3.2f + Vector3.up * 1.6f, look, "npc_" + id); }
            foreach (var n in FBRole.npcs.Where(n => n && n.npcSet != null && n.npcSet.sit.Count > 0).Take(2))   // senta dois de propósito (banco mais perto livre)
            { var rn = n.GetComponent<FBRoleNpc>(); var S = FBRole.seats.Where(x => x.busy == null && x.kind == "banco").OrderBy(x => (x.p - n.Pos).sqrMagnitude).FirstOrDefault() ?? FBRole.seats.FirstOrDefault(x => x.busy == null); if (rn && S != null) rn.SeatNow(S, false); }
            for (float w = 0; w < 6f; w += Time.unscaledDeltaTime) yield return null;
            foreach (var n in FBRole.npcs.Where(n => n && n.GetComponent<FBRoleNpc>()?.st == "sit")) { var rn = n.GetComponent<FBRoleNpc>(); var hp = n.an ? n.an.GetBoneTransform(HumanBodyBones.Hips) : null; Log($"{map}: sentado {n.skinId}: assento y {rn.seat.h:0.00} · raiz y {n.transform.position.y:0.00} · quadril {(hp ? hp.position.y - rn.seat.h : 0) * 100:0} cm acima do assento · do ponto {(hp ? (hp.position - rn.seat.p).Flat().magnitude : 0) * 100:0} cm"); }
            int ns = 0; foreach (var n in FBRole.npcs.Where(n => n && n.GetComponent<FBRoleNpc>()?.st == "sit").Take(3)) { var f = Quaternion.Euler(0, n.Yaw, 0) * Vector3.forward; yield return Shot(rn2(n).seat.p + f * 2.6f + Vector3.up * 1.3f + Quaternion.Euler(0, 90, 0) * f * 1.4f, rn2(n).seat.p + Vector3.up * .55f, "sentado" + ns++); }
            // carros de teste lado a lado: kits/peças do REB e os rebaixados
            var me = FBMatch.Player; var rcar = FBRole.roleCar; var basePos = (rcar ? rcar.transform.position : me.Pos) + Vector3.up * .2f; var right = rcar ? rcar.transform.right : Vector3.right; var fwd = rcar ? rcar.transform.forward : Vector3.forward;
            var specs = new (string id, bool kit)[] { ("GolG2", true), ("Golf", true), ("Chevett", true), ("ViaturaPMAzul", false), ("ViaturaBM", false), ("Clio2003", false), ("Celta", true) };
            int slot = 0;
            foreach (var (id, kit) in specs)
            {
                var p = basePos + fwd * 9 + right * (slot++ - 3) * 3.4f; var car = FBVehicles.Make(id, p, Quaternion.LookRotation(fwd).eulerAngles.y, false, null, true); if (!car) { Log($"{map}: carro {id} não montou"); continue; }
                var T = new FBTuneData();
                if (kit) { for (int z = 0; z < FBReb.ZONAS.Length; z++) { var vs = FBReb.ZoneVariants(car, FBReb.ZONAS[z]); T.bk[z] = vs.Count > 0 ? vs[vs.Count - 1] : -1; } T.bandL = T.bandR = true; T.bandeira = slot % 4; T.engate = true; T.forroF = 1; T.forroT = 1; }
                FBTune.Apply(car, T); for (int k = 0; k < 3; k++) yield return null;
                var parts = car.shell.Find("reb_parts"); int act = parts ? parts.GetComponentsInChildren<Renderer>(false).Length : 0;
                Log($"{map}: carro {id}: shell y {car.shell.localPosition.y:+0.000;-0.000} · ronco {car.engineProf} · buzina {FBReb.Buzina(id) ?? "(perfil)"} · tração {car.tracao ?? "-"} · pneus {FBReb.Pneus(id).Length} · bodykit {(FBReb.HasBodykit(car) ? string.Join(",", FBReb.ZONAS.Where(z => FBReb.HasZone(car, z))) : "não")} · peças ativas {act} · bandeira {FBReb.HasBandeira(car)} · engate {FBReb.HasEngate(car)}");
                var c0 = car.transform.TransformPoint(car.lbox.center); var t = car.transform;
                yield return Shot(c0 + t.forward * 5.2f + t.right * 3.6f + Vector3.up * 1.4f, c0, "carro_" + id + "_frente");
                yield return Shot(c0 - t.forward * 5.4f - t.right * 3.4f + Vector3.up * 1.4f, c0, "carro_" + id + "_tras");
                yield return Shot(c0 + t.right * 6.5f + Vector3.up * .55f, c0 + Vector3.down * .2f, "carro_" + id + "_lado");
                // espelho do retrovisor esquerdo de perto (por trás) e placa traseira de perto
                var es = Json.L(FBReb.Own(id), "espelhos"); if (es != null && es.Count > 0) { var mn = Json.L(es[0], "min"); var mx = Json.L(es[0], "max"); var mc = car.shell.TransformPoint(new Vector3((Json.Fv(mn[0]) + Json.Fv(mx[0])) / 2, (Json.Fv(mn[1]) + Json.Fv(mx[1])) / 2, (Json.Fv(mn[2]) + Json.Fv(mx[2])) / 2)); yield return Shot(mc - t.forward * 1.3f + Vector3.up * .25f, mc, "carro_" + id + "_espelho"); }
                yield return Shot(t.TransformPoint(new Vector3(car.lbox.center.x, car.lbox.min.y + .6f, car.lbox.min.z - 1.6f)), t.TransformPoint(new Vector3(car.lbox.center.x, car.lbox.min.y + .4f, car.lbox.min.z)), "carro_" + id + "_placa");
                if (FBDoors.Has(car)) { FBDoors.SetMask(car, FBDoors.DOORS, true); for (float w = 0; w < 1.4f; w += Time.unscaledDeltaTime) yield return null; yield return Shot(c0 - t.right * 3.2f + t.forward * 1.2f + Vector3.up * 1.1f, c0 - t.right * .9f, "carro_" + id + "_porta"); FBDoors.SetMask(car, 0, true); }
            }
            // zoom da câmera do veículo: os 6 níveis pela câmera do jogo
            cam.enabled = false;
            if (rcar && me)
            {
                if (!FBVehicles.CarOf(me)) FBDrive.Enter(me, rcar); for (float w = 0; w < 1.5f; w += Time.unscaledDeltaTime) yield return null;
                foreach (var z in FBDrive.ZOOMS) { FBDrive.SetZoom(z, false); for (float w = 0; w < 1.2f; w += Time.unscaledDeltaTime) yield return null; ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"r35_{map}_zoom_{z:0.00}.png")); yield return null; yield return null; Log($"{map}: zoom {z:0.00}: câmera a {(Camera.main ? Vector3.Distance(Camera.main.transform.position, rcar.transform.position) : -1):0.0} m do carro"); }
                FBDrive.SetZoom(.82f, false); FBDrive.Exit(me, true);
            }
            // oficina com o GolG2 de kit: abas do REB
            var gol = FBVehicles.list.LastOrDefault(c => c && c.id == "GolG2");
            if (gol && me)
            {
                FBOficina.Open(gol);
                foreach (var (tb, sb) in new[] { ("inicio", ""), ("carro", "bodykit"), ("carro", "acess"), ("roda", "rodas"), ("som", "") })
                { FBOficina.TestTab(tb, sb); for (float w = 0; w < 1f; w += Time.unscaledDeltaTime) yield return null; ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"r35_{map}_oficina_{tb}{sb}.png")); yield return null; yield return null; }
                FBOficina.TestClose();
            }
            Destroy(cg);
        }

        // ---- paredão: parâmetros e nível real na saída
        IEnumerator Paredao(string map)
        {
            var W = FBParedao.List().FirstOrDefault(w => w.car && w.on && w.playing);
            if (W == null) { Log($"{map}: nenhum paredão tocando (biblioteca vazia?)"); yield break; }
            var car = W.car; var others = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude).Where(l => l.enabled).ToList(); foreach (var l in others) l.enabled = false;
            var eg = new GameObject("OuvidoTeste"); eg.AddComponent<AudioListener>(); var ear = eg.transform; yield return null;
            var node = car.soundWallNode ? car.soundWallNode : car.transform; var face = node.forward; face.y = 0; face.Normalize(); var side = new Vector3(face.z, 0, -face.x);
            var src = node.position + Vector3.up * .8f; var buf = new float[2048];
            (float l, float r) Rms() { float a = 0, b = 0; AudioListener.GetOutputData(buf, 0); foreach (var v in buf) a += v * v; AudioListener.GetOutputData(buf, 1); foreach (var v in buf) b += v * v; return (Mathf.Sqrt(a / buf.Length), Mathf.Sqrt(b / buf.Length)); }
            foreach (var (name, pos) in new[] { ("frente 4 m", src + face * 4), ("frente 15 m", src + face * 15), ("frente 40 m", src + face * 40), ("frente 90 m", src + face * 90), ("frente 160 m", src + face * 160), ("atrás 15 m", src - face * 15), ("lado 15 m", src + side * 15) })
            {
                if (!ear) break; ear.position = pos + Vector3.up * .9f; ear.rotation = Quaternion.LookRotation((src - ear.position).Flat().sqrMagnitude > .01f ? (src - ear.position).Flat() : Vector3.forward);
                float l = 0, r = 0; for (float w = 0; w < 1.6f; w += Time.unscaledDeltaTime) yield return null;
                for (int k = 0; k < 20; k++) { var (a, b) = Rms(); l += a; r += b; yield return null; }
                Log($"{map}: paredão {name}: saída E {l / 20:0.0000} D {r / 20:0.0000} · {FBParedao.Diag(car)}");
            }
            // de lado: virar a cabeça 90° (fonte à direita) — o canal direito tem que ficar mais forte
            if (ear)
            {
                ear.position = src + face * 12 + Vector3.up * .9f; var toSrc = (src - ear.position).Flat().normalized; ear.rotation = Quaternion.LookRotation(new Vector3(-toSrc.z, 0, toSrc.x));   // olhando 90° à esquerda da fonte -> fonte à direita
                float l = 0, r = 0; for (float w = 0; w < 1.2f; w += Time.unscaledDeltaTime) yield return null;
                for (int k = 0; k < 20; k++) { var (a, b) = Rms(); l += a; r += b; yield return null; }
                Log($"{map}: paredão fonte à DIREITA da cabeça: saída E {l / 20:0.0000} D {r / 20:0.0000}");
            }
            Destroy(eg); foreach (var l in others) if (l) l.enabled = true;
        }
    }
}
