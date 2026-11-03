// Autoteste dos EVENTOS DE RUA (rodada 39): FreeBala.exe -fbruas <mapa:modo>[,<mapa:modo>...] [-fbnight]
// Em cada mapa/modo força o carro do açaí e o baculejo perto do jogador e mede, gravando no log ([FreeBala][ruas-teste]) e prints em
// autoteste/ruas_*.png (câmera de teste por cima da principal):
//  · açaí: no asfalto, distância da faixa, velocidade, sobreposição com outro veículo, travado; áudio REAL na saída com o ouvido a 5, 15,
//    40 e 90 m (com e sem o som do açaí — a diferença é dele), sem reinício nem fonte duplicada;
//  · baculejo: estados da cena, PM a ~0,44 m atrás do abordado, cápsulas livres (ninguém dentro de parede/carro), pés no chão, viatura na
//    guia, saída pela faixa; FPS antes × durante.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBStreetEventsTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbruas"); if (i < 0 || i + 1 >= a.Length) return;
            var g = new GameObject("FB_RuasTeste"); DontDestroyOnLoad(g); var t = g.AddComponent<FBStreetEventsTest>(); t.runs = a[i + 1].Split(',');
            t.night = System.Array.IndexOf(a, "-fbnight") >= 0;
        }
        string[] runs; bool night; string dir; System.Action restore; Camera cam; int shot;
        void OnApplicationQuit() => restore?.Invoke();
        static void Log(string s) => Debug.Log("[FreeBala][ruas-teste] " + s);
        IEnumerator Wait(float s) { for (float w = 0; w < s; w += Time.unscaledDeltaTime) yield return null; }
        void Shot(string name) { ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"ruas_{name}.png")); shot++; }
        void Look(Vector3 from, Vector3 at, float fov = 55) { if (!cam) return; cam.transform.position = from; cam.transform.rotation = Quaternion.LookRotation(at - from); cam.fieldOfView = fov; }
        // r45j: no rolê o FBGarage.AutoPick senta o jogador no carro dele, e o Teleport puro era desfeito pelo banco (o jogador ficava no
        //  ponto de partida: perseguição acabava "longe do jogador", câmera do jogo longe do racha). Sai do carro antes de levar o jogador.
        // r45k: volta o jogador (a pé) para o lado do carro do rolê — o ponto de partida tem trânsito; o racha deixa o jogador numa ponta do mapa
        static IEnumerator BackToStart() { var rc = FBRole.roleCar; if (!rc) yield break; var T = rc.transform; var pp = T.position - T.right * 5; if (FBWorld.GroundY(pp.x, pp.y + 3, pp.z, .4f, 8) is float gy) pp.y = gy; PlayerTo(pp, T.eulerAngles.y); yield return new WaitForSeconds(1.5f); }
        static void PlayerTo(Vector3 p, float yaw) { var me = FBMatch.Player; if (!me) return; if (FBVehicles.CarOf(me)) FBDrive.Exit(me, true, p, yaw); else FBMatch.Teleport(me, p, yaw); }

        IEnumerator Start()
        {
            dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste"); System.IO.Directory.CreateDirectory(dir);
            FBInput.TestMove = Vector2.zero; yield return Wait(3);
            foreach (var run in runs)
            {
                var mm = run.Split(':'); string map = mm[0], mode = mm.Length > 1 ? mm[1] : "role";
                GameData.Load(); FBSession.Load(); var keepMap = FBSession.map; var keepMode = FBSession.mode; var keepW = FBSession.WeatherFor(map);
                FBSession.map = map; FBSession.mode = mode; FBSession.online = false; FBSession.view = "tps"; if (night) FBSession.SetWeather(map, "noite", "limpo"); FBSession.Save();
                restore = () => { FBSession.SetWeather(map, keepW.time, keepW.weather); FBSession.map = keepMap; FBSession.mode = keepMode; FBSession.Save(); };
                Log($"carregando {map}:{mode}");
                var op = SceneManager.LoadSceneAsync("Map_" + map); if (op == null) { Log("FALHA: cena não existe"); continue; }
                while (!op.isDone) yield return null;
                yield return Wait(5);
                if (mode == "role") { FBGarage.AutoPick(); yield return Wait(2); }
                FBMatch.God = true; if (FBMatch.Player) FBMatch.Player.hp = 100;
                {   // strafes (26/10) e sons do 171
                    var P = FBMatch.Player; var npc = FBMatch.All.FirstOrDefault(a => a && !a.isPlayer && !a.isBot && !a.IsFemale && a.Unarmed);
                    Log($"{map}:{mode}: strafe jogador {P?.OverrideOf("StrafeRightLoop")} / {P?.OverrideOf("RunRtLoop")} · NPC {(npc ? npc.skinId : "-")} andar {npc?.OverrideOf("WalkFwdLoop")} ré {npc?.OverrideOf("WalkBwdLoop")} lado {npc?.OverrideOf("StrafeRightLoop")} diag {npc?.OverrideOf("StrafeRight45Loop")}");
                    var w = P ? P.weapon : null;
                    Log($"{map}:{mode}: sons 171 · porta {FBAudio.Clip("veh_door_open")?.name} · impacto concreto {FBAudio.Clip("impact_concrete")?.name} · arma {w?.id} tiro {(w != null ? FBWeaponSound.Fire(w, false) : "-")} longe {FBWeaponSound.Far(w)} recarga {FBWeaponSound.Reload(w)}");
                }
                if (!FBStreetEvents.I) { Log($"{map}:{mode}: SEM diretor de eventos (mapa sem malha de ruas?)"); restore(); restore = null; continue; }
                // FPS de referência (sem os eventos forçados)
                float fps0 = 0; { int n = 0; for (float w = 0; w < 3; w += Time.unscaledDeltaTime) { fps0 += 1 / Mathf.Max(1e-4f, Time.unscaledDeltaTime); n++; yield return null; } fps0 /= Mathf.Max(1, n); }
                var cg = new GameObject("RuasCam"); cam = cg.AddComponent<Camera>(); cam.depth = 100; cam.cullingMask = Camera.main ? Camera.main.cullingMask : ~0; cam.farClipPlane = 900; cam.nearClipPlane = .1f;
                // r43: -fbsoeventos acai,baculejo,veiculos,eletricista (sem = todos)
                var aa = System.Environment.GetCommandLineArgs(); int so = System.Array.IndexOf(aa, "-fbsoeventos"); var only = so >= 0 && so + 1 < aa.Length ? aa[so + 1].Split(',') : null;
                bool Do(string k) => only == null || System.Array.IndexOf(only, k) >= 0;
                if (Do("eletricista")) yield return Eletricista(map + "_" + mode);
                if (Do("obra")) yield return Obra(map + "_" + mode);
                if (Do("acai")) yield return Acai(map + "_" + mode, fps0);
                if (Do("baculejo")) yield return Baculejo(map + "_" + mode, fps0);
                if (Do("veiculos")) yield return Veiculos(map + "_" + mode);
                if (Do("objetos")) yield return Objetos(map + "_" + mode);
                if (Do("fumaca")) yield return Fumaca(map + "_" + mode);
                if (Do("racha")) yield return Racha(map + "_" + mode);
                if (Do("motos")) yield return MotosTransito(map + "_" + mode);
                if (Do("perseguicao")) yield return Perseguicao(map + "_" + mode);
                if (Do("paredoes")) yield return Paredoes(map + "_" + mode);
                if (Do("camera")) yield return Camera1P(map + "_" + mode);
                Destroy(cg); FBMatch.God = false; restore(); restore = null;
            }
            Log("fim"); Application.Quit();
        }

        // ---------------- eletricista do 171 (rodada 43): monta, fotos, falas com o jogador perto, queda forçada (batida), depois
        IEnumerator Eletricista(string tag)
        {
            // r44: 1) carro com escada — ciclo inteiro (tirar do rack, carregar, erguer, subir, trabalhar, descer, guardar), fotos por etapa
            FBEletricista.ForceVariant = "escada"; FBStreetEvents.ForceNear = 35; FBStreetEvents.ForceEletricista = true; yield return Wait(1.2f); FBStreetEvents.ForceNear = -1;
            if (FBEletricista.Active)
            {
                var b0 = FBEletricista.Base; string last = null; float t0 = 0; var seen = new List<string>();
                for (; t0 < 140 && FBEletricista.Active && FBEletricista.State != "pausa"; t0 += Time.unscaledDeltaTime)
                {
                    var st = FBEletricista.State;
                    if (st != last)
                    {
                        last = st; seen.Add(st); var w1 = FBEletricista.Worker; var lad1 = FBEletricista.Ladder;
                        Look(b0 + new Vector3(7, 4, 7), (w1 ? w1.Pos : b0) + Vector3.up * 1.5f, 55); yield return null; Shot($"{tag}_escada_{st}");
                        Log($"{tag}: escada · etapa {st} aos {t0:0.0} s · eletricista a {(w1 ? (w1.Pos.y - b0.y).ToString("0.0") : "-")} m · escada (topo) {(lad1 ? lad1.GetComponentInChildren<Renderer>().bounds.max.y - b0.y : 0):0.0} m · luminárias cortadas {FBEletricista.LampsCut}");
                    }
                    if (st == "trabalhando" && FBEletricista.Worker) { var hw = FBEletricista.Worker; var ik = hw.GetComponent<FBLadderIK>(); }
                    if (st == "trabalhando" && t0 > 0 && Time.frameCount % 600 == 0) Log($"{tag}: escada trabalhando · faíscas {FBEletricista.Sparks}");
                    yield return null;
                }
                Log($"{tag}: escada · etapas: {string.Join(" > ", seen)} · {t0:0} s · faíscas {FBEletricista.Sparks}");
                FBEletricista.End(true); yield return Wait(1);
            }
            else Log($"{tag}: escada NÃO montou ({FBEletricista.Why})");
            // 2) corte de energia (manutenção): começa no alto, luminárias apagadas; depois o resto do teste antigo (falas, queda, briga/fuga)
            FBEletricista.ForceVariant = "energia"; FBStreetEvents.ForceNear = 35; FBStreetEvents.ForceEletricista = true; yield return Wait(1.2f); FBStreetEvents.ForceNear = -1;
            Log($"{tag}: energia · {FBEletricista.State} · luminárias cortadas {FBEletricista.LampsCut} · ajudante {(FBEletricista.Helper ? FBEletricista.Helper.skinId : "-")}");
            if (!FBEletricista.Active) { Log($"{tag}: eletricista NÃO montou ({FBEletricista.Why} · {FBStreetEvents.Log})"); yield break; }
            var b = FBEletricista.Base; var w = FBEletricista.Worker; var lad = FBEletricista.Ladder; var van = FBEletricista.Van;
            Log($"{tag}: eletricista · {FBEletricista.State} · escada {(lad ? lad.name : "-")} · carro {(van ? van.transform.childCount + " peças" : "-")} · personagem {(w ? w.skinId + " a " + (w.Pos.y - b.y).ToString("0.0") + " m" : "-")} · clipe {(w && w.dance != null ? w.dance.clip : "-")}");
            Log($"{tag}: elenco em cena (r52) · {FBElenco.SceneCheck()}");
            var side = w ? Vector3.Cross(Vector3.up, (w.Pos - b).Flat().normalized) : Vector3.right; if (side.sqrMagnitude < .1f) side = Vector3.right;
            Look(b + side * 9 + Vector3.up * 3.5f + (b - (w ? w.Pos : b)).Flat().normalized * 4, b + Vector3.up * 2.5f, 55); yield return Wait(.6f); Shot($"{tag}_ele_cena");
            if (w) { Look(w.Pos + side * 2.2f + Vector3.up * .2f - (w.Fwd) * 1.2f, w.Pos + Vector3.up * 1.3f, 50); yield return Wait(.4f); Shot($"{tag}_ele_trabalhando"); }
            if (van) { var vb = van.GetComponentsInChildren<Renderer>(); if (vb.Length > 0) { var bb = vb[0].bounds; foreach (var r in vb) bb.Encapsulate(r.bounds); Look(bb.center + side * 6 + Vector3.up * 2.5f, bb.center, 50); yield return Wait(.4f); Shot($"{tag}_ele_carro"); } }
            // jogador encosta na escada: falas
            var P = FBMatch.Player; if (P) { PlayerTo(b - side * 3.5f, 0); }
            int talk0 = 0; for (float t = 0; t < 9; t += Time.unscaledDeltaTime) { yield return null; }
            Log($"{tag}: eletricista com o jogador a {(P ? (P.Pos - b).Flat().magnitude : -1):0.0} m · estado {FBEletricista.State}");
            // batida na escada (o teste força a queda, como o carro encostando na caixa do BP)
            FBEletricista.Fall(side, "teste"); yield return Wait(.7f); Look(b + side * 7 + Vector3.up * 3, b + Vector3.up, 55); Shot($"{tag}_ele_queda");
            yield return Wait(6.5f); w = FBEletricista.Worker; if (w) { Look(w.Pos + side * 5 + Vector3.up * 2.5f, w.Pos + Vector3.up, 55); yield return Wait(.3f); } Shot($"{tag}_ele_depois");
            Log($"{tag}: eletricista depois da queda: {FBEletricista.State} · escada no chão {(lad ? (lad.transform.position.y - b.y).ToString("0.0") : "-")} m · personagem de pé {(w ? (w.Hips ? (w.Hips.position.y - w.Pos.y).ToString("0.00") : "?") : "-")}");
            yield return Wait(4); FBEletricista.End(true);
        }

        // ---------------- r44: objetos do 171 nas calçadas (FBVestirRua) + física (empurrão de carro, tiro) + atropelamento (FBAtropelo)
        IEnumerator Objetos(string tag)
        {
            Log($"{tag}: objetos do 171: {FBVestirRua.Total} · " + string.Join(", ", FBVestirRua.Count.OrderByDescending(kv => kv.Value).Take(10).Select(kv => kv.Key + " " + kv.Value)) + $" · corpos físicos {FBFisica.All.Count} · assentos {FBRole.seats.Count}");
            var P = FBMatch.Player; if (!P) yield break;
            var mesa = FBVestirRua.Root ? FBVestirRua.Root.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.StartsWith("x171_SM_Mesa")) : null;
            if (mesa) { Look(mesa.position + new Vector3(3, 2, 3), mesa.position, 55); yield return Wait(.5f); Shot($"{tag}_obj_boteco"); }
            var lix = FBFisica.All.FirstOrDefault(b => b && b.name.Contains("lixeira"));
            if (lix) { var p0 = lix.transform.position; Look(p0 + new Vector3(3, 1.6f, 3), p0, 55); FBFisica.Shot(lix.GetComponentInChildren<Collider>(), p0 + Vector3.up * .4f, (p0 - P.Pos).Flat().normalized); yield return Wait(2); Shot($"{tag}_obj_lixeira_tiro"); Log($"{tag}: lixeira levou tiro: andou {(lix.transform.position - p0).magnitude:0.00} m · tombou {Vector3.Angle(lix.transform.up, Vector3.up):0}° · impactos {FBFisica.Impacts}"); }
            // atropelamento: carro a ~40 km/h em cima de um NPC parado
            var npc = FBMatch.All.FirstOrDefault(a => a && a.alive && !a.isPlayer && !a.vehiclePose && (a.Pos - P.Pos).magnitude < 120);
            var car = FBVehicles.list.FirstOrDefault(c => c && !c.driver && c.kind != "moto");
            if (npc && car)
            {
                int g0 = FBAtropelo.GotUp; var f = npc.Fwd; car.transform.position = npc.Pos - f * 4 + Vector3.up * .05f; car.transform.rotation = Quaternion.LookRotation(f);
                var hp0 = npc.hp; FBAtropelo.Hit(npc, car, 11, f, FBSession.mode != "role"); Look(npc.Pos + Vector3.Cross(Vector3.up, f) * 5 + Vector3.up * 2, npc.Pos, 55);
                yield return Wait(.6f); Shot($"{tag}_atropelo_1"); yield return Wait(1.5f); Shot($"{tag}_atropelo_2"); yield return Wait(10); Shot($"{tag}_atropelo_levantou");
                Log($"{tag}: atropelamento {FBAtropelo.Last} · vida {hp0:0} -> {npc.hp:0} · vivo {npc.alive} · levantou {FBAtropelo.GotUp - g0} · quadril {(npc.Hips ? (npc.Hips.position.y - npc.Pos.y).ToString("0.00") : "-")} m");
            }
        }

        // ---------------- obra de rua do 171 (rodada 43): monta um dos 3 leiautes, fotos de longe e de perto, tiro perto (correm)
        IEnumerator Obra(string tag)
        {
            FBStreetEvents.ForceNear = 30; FBStreetEvents.ForceObra = true; yield return Wait(1.5f); FBStreetEvents.ForceNear = -1;
            if (!FBObra.Active) { Log($"{tag}: obra NÃO montou ({FBObra.Why} · {FBStreetEvents.Log})"); yield break; }
            var c = FBObra.Center; Log($"{tag}: obra · {FBObra.Workers.Count} pedreiros · clipes {string.Join(",", FBObra.Workers.Where(w => w).Select(w => w.dance != null ? w.dance.clip : "-"))}");
            Log($"{tag}: elenco em cena (r52) · {FBElenco.SceneCheck()}");
            Look(c + new Vector3(9, 6, 9), c + Vector3.up * .5f, 55); yield return Wait(.6f); Shot($"{tag}_obra_geral");
            Look(c + new Vector3(-4, 2.2f, 5), c + Vector3.up * .8f, 55); yield return Wait(.4f); Shot($"{tag}_obra_perto");
            var w0 = FBObra.Workers.FirstOrDefault(w => w && w.dance != null);
            if (w0) { Look(w0.Pos + w0.Fwd * 2.4f + w0.transform.right * 1.2f + Vector3.up * 1.4f, w0.Pos + Vector3.up * 1f, 50); yield return Wait(.4f); Shot($"{tag}_obra_pedreiro"); }
            FBBotBrain.Noise(c + Vector3.up + new Vector3(10, 0, 0)); yield return Wait(3); Look(c + new Vector3(9, 6, 9), c + Vector3.up * .5f, 60); Shot($"{tag}_obra_tiro");
            Log($"{tag}: obra depois do tiro · correndo {FBObra.Workers.Count(w => w && w.dance == null)}/{FBObra.Workers.Count}");
            FBObra.End(true);
        }

        // ---------------- r45: racha de moto (física do MX pilotada por IA) — perseguição de lado, prints da largada e do grau
        // r45b: 3 rachas por mapa (o "exibido" chegou a cair em rodadas intermediárias) — total de quedas/chegadas no fim
        IEnumerator Racha(string tag)
        {
            int c0 = FBRacha.Crashes, r0 = FBRacha.Races, f0 = FBRacha.Finishes, fails = 0;
            for (int k = 0; k < 3; k++) { int rr = FBRacha.Races; yield return RachaOne(k == 0 ? tag : tag + "_" + k, k == 0); if (FBRacha.Races == rr) fails++; yield return Wait(1); }
            Log($"{tag}: RACHAS no mapa: {FBRacha.Races - r0} corridas · chegadas {FBRacha.Finishes - f0} pilotos · quedas {FBRacha.Crashes - c0} · não montou/não terminou {fails}");
        }
        IEnumerator RachaOne(string tag, bool diag)
        {
            FBStreetEvents.ForceNear = 120; FBStreetEvents.ForceRacha = true; yield return Wait(1.5f); FBStreetEvents.ForceNear = -1;
            if (!FBRacha.Active) { Log($"{tag}: racha NÃO montou ({FBRacha.Why})"); yield break; }
            var bikes = FBVehicles.list.Where(c => c && c.mx && c.driver && !c.driver.isPlayer && FBTransito.List.All(t => t.car != c)).ToList();
            { var b1 = bikes.FirstOrDefault(); var me2 = FBMatch.Player; if (b1 && me2) { var p2 = b1.transform.position - b1.transform.right * 9; if (FBWorld.GroundY(p2.x, p2.y + 3, p2.z, .3f, 6) is float gy2) p2.y = gy2; PlayerTo(p2, b1.transform.eulerAngles.y + 90); yield return Wait(.5f); } }   // a câmera do jogo (FBCarCull mede por ela) perto da largada
            { var tb = FBTransito.List.Select(c => c.car).FirstOrDefault(c => c && c.mx); if (tb) bikes.Add(tb); }
            { var b = bikes.FirstOrDefault(); if (b) { var T = b.transform; Look(T.position + T.right * 5 + Vector3.up * 1.6f, T.position + Vector3.up * .8f, 50); } }
            yield return null; yield return null; yield return null;
            if (diag) foreach (var b0 in bikes) { var lgd = b0.GetComponent<LODGroup>(); var rv = b0.GetComponentsInChildren<Renderer>(true); Log($"{tag}: racha diag2 {b0.id} · Camera.main {(Camera.main ? Vector3.Distance(Camera.main.transform.position, b0.transform.position).ToString("0") + " u" : "NULA")} · jogador {Vector3.Distance(FBMatch.Player.Pos, b0.transform.position):0} u · visíveis {rv.Count(r => r.isVisible)}/{rv.Length} · LODGroup ligado {(lgd ? lgd.enabled : false)} lods {(lgd ? lgd.lodCount : 0)} · shaders {string.Join(",", rv.Take(3).Select(r => r.sharedMaterial ? r.sharedMaterial.shader.name : "-"))} · forceOff {rv.Count(r => r.forceRenderingOff)} · sombra {rv.Count(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)} · camadas {string.Join(",", rv.Select(r => r.gameObject.layer).Distinct())} · trânsito {FBTransito.List.Any(c => c.car == b0)}"); }
            if (bikes.Count > 2) bikes.RemoveAt(bikes.Count - 1);
            if (diag) foreach (var b0 in bikes) { var rs = b0.GetComponentsInChildren<Renderer>(true); var hip = b0.driver && b0.driver.an ? b0.driver.an.GetBoneTransform(HumanBodyBones.Hips) : null; var bb = rs.Length > 0 ? rs[0].bounds : default; foreach (var r in rs) bb.Encapsulate(r.bounds);
              Log($"{tag}: racha diag {b0.id} · renderers {rs.Length} ligados {rs.Count(r => r.enabled && r.gameObject.activeInHierarchy)} · malhas vazias {b0.GetComponentsInChildren<MeshFilter>(true).Count(m => !m.sharedMesh || m.sharedMesh.vertexCount == 0)} · caixa {bb.center} tam {bb.size} · grupo {b0.transform.position} escala {b0.transform.lossyScale.x:0.00} · quadril do piloto {(hip ? hip.position.ToString() : "-")} · LODGroup {(b0.GetComponent<LODGroup>()?.size ?? -1):0.00} · ativo {b0.gameObject.activeInHierarchy}"); } string lastSt = null; int shots = 0; float t = 0, maxKmh = 0;
            while (FBRacha.Active && t < 45)
            {
                t += Time.deltaTime; var b = bikes.Where(x => x).OrderByDescending(x => x.mx.speedKmh).FirstOrDefault(); if (!b) break; maxKmh = Mathf.Max(maxKmh, b.mx.speedKmh);
                var T = b.transform; Look(T.position + T.right * 7 + T.forward * 2 + Vector3.up * 2.2f, T.position + Vector3.up * .9f + T.forward * 1.5f, 50);
                if (FBRacha.State != lastSt || (FBRacha.State == "largada" && shots < (diag ? 6 : 2) && t > 1 && Mathf.Repeat(t, 1.6f) < Time.deltaTime)) { lastSt = FBRacha.State; Shot($"{tag}_racha_{shots++}_{FBRacha.State}"); Log($"{tag}: racha {FBRacha.State} · {string.Join(" · ", bikes.Where(x => x).Select(x => $"{x.id} {x.mx.speedKmh:0} km/h grau {x.mx.wheelieAngle:0}° marcha {x.mx.GearLabel} caiu {x.mx.crashed} clipe {x.driver?.sys}"))}"); }
                if (FBRacha.State == "volta") break; yield return null;
            }
            Log($"{tag}: racha fim · freadas por obstáculo {FBRacha.Brakes} · percorrido {FBRacha.Progress:0} m · máxima {maxKmh:0} km/h · grau máx {FBRacha.MaxWheelie:0}° · estado {FBRacha.State}"); FBRacha.End(true);
        }
        // ---------------- r45: motoqueiros MX no trânsito (inclinação na curva, grau dos agressivos)
        IEnumerator MotosTransito(string tag)
        {
            float t = 0, maxLean = 0, maxWh = 0, physLean = 0, tpT = 0; int n = 0, physSeen = 0; FBTransito.Car fol = null; float share0 = FBTransito.MotoShare; FBTransito.MotoShare = .6f; float gs0 = FBTransito.GarupaShare; FBTransito.GarupaShare = 1;   /* r45e: no teste toda moto nova leva garupa */
            yield return BackToStart();
            { int mNow = FBTransito.List.Count(c => c.car && c.car.mx && c.mode == "drive"); if (mNow < 3) Log($"{tag}: {mNow} moto(s) no trânsito agora · pus {FBTransito.TestMotos(3 - mNow)} (o sorteio do começo pode vir sem moto)"); }
            int ups0 = FBTransito.MotoPhysUps, cr0 = FBTransito.MotoCrashes, wh0 = FBTransito.MotoPhysWh; FBTransito.MotoPhysLagMax = FBTransito.MotoPhysLatMax = FBTransito.MotoPhysFrames = FBTransito.MotoPhysWhMax = 0;
            while (t < 70) { t += Time.deltaTime; var ms = FBTransito.List.Where(c => c.car && c.car.mx && c.mode == "drive").ToList(); n = Mathf.Max(n, ms.Count); physSeen = Mathf.Max(physSeen, ms.Count(c => c.phys));
                foreach (var c in ms) { maxLean = Mathf.Max(maxLean, Mathf.Abs(c.roll) * Mathf.Rad2Deg); maxWh = Mathf.Max(maxWh, c.whA * Mathf.Rad2Deg); if (c.phys) physLean = Mathf.Max(physLean, Mathf.Abs(c.car.mx.leanAngle)); }
                if (fol == null || !fol.car || fol.mode != "drive") fol = ms.OrderBy(c => (c.pos - FBStreetEvents.PlayerPos).sqrMagnitude).FirstOrDefault();
                if (fol != null && fol.car)
                {   // r45b: o jogador acompanha a moto a ~12 m (a física do MX liga perto da câmera do jogo; o FBCarCull também mede por ela)
                    var T = fol.car.transform; if ((tpT -= Time.deltaTime) <= 0 && (T.position - FBStreetEvents.PlayerPos).magnitude > 16) { tpT = 1.2f; var pp = T.position - T.forward * 6 + T.right * 10; if (FBWorld.GroundY(pp.x, pp.y + 3, pp.z, .4f, 8) is float gy) pp.y = gy; PlayerTo(pp, T.eulerAngles.y); }
                    Look(T.position + T.right * 6 + Vector3.up * 2, T.position + Vector3.up * .9f, 50); if (Mathf.Repeat(t, 7) < Time.deltaTime) { Shot($"{tag}_moto_transito_{(int)(t / 7)}"); Log($"{tag}: moto {fol.car.id} · física {(fol.phys ? "MX" : "cinemática")} · {(fol.phys ? fol.car.mx.speedKmh : fol.v * 3.6f / 1.2f):0} km/h · incl {(fol.phys ? fol.car.mx.leanAngle : fol.roll * Mathf.Rad2Deg):0}° · grau {(fol.phys ? fol.car.mx.wheelieAngle : fol.whA * Mathf.Rad2Deg):0}°" + ((fol.phys ? fol.car.mx.speedKmh < 1 : fol.v < .3f) ? " · PARADA: " + FBTransito.Chain(fol) : "")); }
                }
                yield return null; }
            {   // r45d: estilos dos pilotos e garupa
                var sty = FBTransito.List.Where(c => c.car && c.car.mx && c.actor).Select(c => c.actor.sys).Distinct().ToList(); Log($"{tag}: estilos dos pilotos MX agora: {string.Join(", ", sty)} · garupas criadas {FBTransito.GarupaSpawned} · desceram com o piloto {FBGarupa.Dismounted}");
                for (float gw = 0; gw < 45 && !FBTransito.List.Any(c => c.garupa && c.car && c.mode == "drive"); gw += .5f) yield return Wait(.5f);
                if (!FBTransito.List.Any(c => c.garupa && c.car && c.mode == "drive"))
                {   // r45j: nenhuma moto andando com garupa (a única nascida pode estar presa na fila): põe uma na moto andando mais perto
                    var cand = FBTransito.List.Where(c => c.car && c.car.mx && c.mode == "drive" && !c.garupa && c.actor).OrderBy(c => (c.pos - FBStreetEvents.PlayerPos).sqrMagnitude).FirstOrDefault();
                    Log($"{tag}: garupa: nenhuma moto com garupa andando em 45 s · sem passageira livre {FBTransito.GarupaSemSkin}x · pondo na {cand?.car?.id}: {(cand != null && FBTransito.ForceGarupa(cand) ? "ok" : "falhou")}");
                }
                var gm = FBTransito.List.Where(c => c.garupa && c.car && c.mode == "drive").OrderBy(c => (c.pos - FBStreetEvents.PlayerPos).sqrMagnitude).FirstOrDefault();
                if (gm != null)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        var T = gm.car.transform; var pp = T.position + T.right * 9; if (FBWorld.GroundY(pp.x, pp.y + 3, pp.z, .4f, 8) is float gy) pp.y = gy; PlayerTo(pp, T.eulerAngles.y); yield return Wait(.6f);
                        if (gm.car == null || !gm.garupa) break; T = gm.car.transform; Look(T.position - T.right * 3.6f + Vector3.up * 1.4f, T.position + Vector3.up * .9f, 45); yield return null; Shot($"{tag}_garupa_{i}");
                        var gh = gm.garupa.an ? gm.garupa.an.GetBoneTransform(HumanBodyBones.Hips) : null; var lh = gm.garupa.an ? gm.garupa.an.GetBoneTransform(HumanBodyBones.LeftHand) : null; var dh = gm.actor && gm.actor.an ? gm.actor.an.GetBoneTransform(HumanBodyBones.Hips) : null;
                        Log($"{tag}: garupa {gm.garupa.skinId} na {gm.car.id} ({(gm.phys ? "física MX" : "cinemática")}) · clipe {gm.garupa.sys} · quadril {(gh && dh ? (gh.position - dh.position).magnitude : -1):0.00} m do piloto · mão E {(lh && dh ? (lh.position - dh.position).magnitude : -1):0.00} m da cintura · piloto {gm.actor?.sys}");
                        { var A = gm.actor; var dpz = A ? A.GetComponent<FBDriverPose>() : null; var C = gm.car; Log($"{tag}: garupa diag · piloto {A?.skinId} pose {(dpz ? (dpz.enabled ? "ligada" : "DESLIGADA") : "SEM")} carro da pose {(dpz && dpz.Car == C ? "o mesmo" : dpz?.Car?.id ?? "-")} · CarOf {(FBVehicles.CarOf(A) == C ? "o mesmo" : FBVehicles.CarOf(A)?.id ?? "nenhum")} · piloto→carro {(A ? (A.transform.position - C.transform.position).magnitude : -1):0.0} u · carro→MX {(C.mx ? (C.transform.position - C.mx.transform.position).magnitude : -1):0.0} u · garupa→carro {(gm.garupa.transform.position - C.transform.position).magnitude:0.0} u · animator {(A && A.an ? A.an.enabled : false)} · vehiclePose {A?.vehiclePose} · modo {gm.mode} · ativo {(A ? A.gameObject.activeInHierarchy : false)}"); }
                        yield return Wait(1.5f);
                    }
                }
                else Log($"{tag}: nenhuma moto com garupa andando agora");
            }
            FBTransito.MotoShare = share0; FBTransito.GarupaShare = gs0;
            Log($"{tag}: motos no trânsito {n} · inclinação máx {maxLean:0}° · grau máx {maxWh:0}° · seguindo {fol?.car?.id} clipe {fol?.actor?.sys}");
            Log($"{tag}: FÍSICA MX nas motos NPC: ligou {FBTransito.MotoPhysUps - ups0}x · até {physSeen} ao mesmo tempo · {FBTransito.MotoPhysFrames:0} s com física · atraso máx do fantasma {FBTransito.MotoPhysLagMax:0.0} u · desvio lateral máx {FBTransito.MotoPhysLatMax:0.0} u · inclinação física máx {physLean:0}° · graus puxados {FBTransito.MotoPhysWh - wh0} (máx {FBTransito.MotoPhysWhMax:0}°) · quedas {FBTransito.MotoCrashes - cr0}");
        }

        // ---------------- r45d: perseguição (viatura atrás de carro/moto do trânsito): o jogador vai junto a ~25 m (o trânsito e a física
        //  vivem perto da câmera do jogo); prints de trás da viatura a cada 5 s; distância, velocidades, sirene/giroflex e o fim
        // r45e: pedido "não precisamos de vários veículos com paredões tocando ao mesmo tempo um próximo ao outro": mede os ligados e liga um
        //  paredão em outro carro perto de um da cidade (como o jogador faria) — o da cidade tem de calar e voltar quando o outro desliga
        // ---------------- r45o: nuvem dos pneus na rua de verdade — queimada com o carro do rolê (o jogador começa nele); foto com a textura
        //  antiga e com a clareada no mesmo quadro (antes × depois)
        IEnumerator Fumaca(string tag)
        {
            var me = FBMatch.Player; var car = FBRole.roleCar; if (!me || !car) { Log($"{tag}: fumaça: sem carro do rolê"); yield break; }
            if (FBVehicles.CarOf(me) != car) { Log($"{tag}: fumaça: jogador fora do carro do rolê"); yield break; }
            yield return Wait(1);
            bool hb = true; float thr = 0; FBPlayer.TestInput = inp => { inp.move = new Vector2(0, thr); inp.jumpHeld = hb; inp.interact = false; inp.multimidia = false; return inp; };
            thr = 1; float burnT = 0; for (float w = 0; w < 2.5f; w += Time.deltaTime) { if (car.burn) burnT += Time.deltaTime; yield return null; }
            var T = car.transform; Look(T.position - T.forward * 7.5f + T.right * 3.2f + Vector3.up * 1.6f, T.position - T.forward * 1.2f + Vector3.up * .7f, 55);
            FBCarFX.SmokeTex(false); yield return null; Shot($"{tag}_fumaca_pneu_antes"); yield return null;
            FBCarFX.SmokeTex(true); yield return null; Shot($"{tag}_fumaca_pneu_depois"); yield return null;
            Log($"{tag}: nuvem dos pneus na rua · {car.id} queimando {burnT:0.0} de 2,5 s · piso {FBCarFX.LastSurf} (0 asfalto, 4 concreto, 1–3 terra) · fotos com a textura antiga e a clareada no mesmo quadro");
            thr = 0; hb = false; yield return Wait(1.5f); FBPlayer.TestInput = null;
        }
        // ---------------- r45j: câmera em 1ª pessoa removida (pedido) — a pé, aperta V três vezes e confere que a câmera do jogo continua atrás
        IEnumerator Camera1P(string tag)
        {
            var me = FBMatch.Player; if (!me || !FBPlayer.I) { Log($"{tag}: câmera: sem jogador"); yield break; }
            var car = FBVehicles.CarOf(me); if (car) { var T = car.transform; var pp = T.position - T.right * 6; if (FBWorld.GroundY(pp.x, pp.y + 3, pp.z, .4f, 8) is float gy) pp.y = gy; PlayerTo(pp, T.eulerAngles.y); }
            yield return Wait(1.5f);
            var gc = Camera.main; var head = me.an ? me.an.GetBoneTransform(HumanBodyBones.Head) : null;
            float D() => gc && head ? Vector3.Distance(gc.transform.position, head.position) : -1;
            bool fp0 = FBPlayer.I.FirstPerson; float d0 = D(); bool tv = false;
            FBPlayer.TestInput = inp => { inp.toggleView = tv; tv = false; return inp; };
            for (int i = 0; i < 3; i++) { tv = true; yield return Wait(.8f); Log($"{tag}: câmera · apertou V {i + 1}x · 1ª pessoa {FBPlayer.I.FirstPerson} · câmera do jogo a {D():0.00} m da cabeça · view {FBSession.view}"); }
            FBPlayer.TestInput = null;
            if (cam) cam.enabled = false; yield return null; Shot($"{tag}_camera_depois_do_V"); yield return null; if (cam) cam.enabled = true;   // foto pela câmera do jogo
            Log($"{tag}: CÂMERA 1ª PESSOA: antes {(fp0 ? "1ª" : "3ª")} pessoa a {d0:0.00} m da cabeça · depois de 3× V: {(FBPlayer.I.FirstPerson ? "1ª PESSOA (ERRO)" : "3ª pessoa")} a {D():0.00} m · laboratório {FBSession.Lab} · no carro {(car ? car.id : "não")}");
        }
        IEnumerator Paredoes(string tag)
        {
            string Pares() { var on = FBParedao.List().Where(w => w.car && w.on).ToList(); int perto = 0; float dmin = 1e9f; for (int i = 0; i < on.Count; i++) for (int j = i + 1; j < on.Count; j++) { float d = (on[i].car.transform.position - on[j].car.transform.position).magnitude; dmin = Mathf.Min(dmin, d); if (d < FBRole.WallGap) perto++; } return $"{on.Count} ligados · pares a menos de {FBRole.WallGap:0} u: {perto} · menor distância {(on.Count > 1 ? dmin.ToString("0") : "-")} u · calados pela regra {FBRole.WallMutes}"; }
            yield return Wait(3); Log($"{tag}: paredões agora: {Pares()} · faixas na biblioteca {FBMediaLib.lib.Count}");
            string Regra() { int bad = 0; foreach (var cw in FBRole.CityWalls) { var S = FBParedao.State(cw); if (!cw || S == null || !S.on) continue; if (FBParedao.List().Any(o => o.car && o.car != cw && o.on && (o.car.transform.position - cw.transform.position).magnitude < FBRole.WallGap)) bad++; } return $"paredões da cidade tocando colados em outro som: {bad}"; }
            Log($"{tag}: paredões · {FBRole.CityWalls.Count} da cidade ({string.Join(", ", FBRole.CityWalls.Where(c => c).Select(c => c.id + (FBParedao.State(c)?.on == true ? " tocando" : " calado")))}) · {Regra()}");
            var city = FBRole.CityWalls.Where(c => c && FBParedao.State(c)?.on == true).Select(FBParedao.State).FirstOrDefault() ?? FBParedao.List().FirstOrDefault(w => w.car && w.on && w.owner >= 0 && !w.local);
            if (city == null) { foreach (var cw in FBRole.CityWalls.Where(c => c)) { var near = FBParedao.List().Where(o => o.car && o.car != cw && o.on).OrderBy(o => (o.car.transform.position - cw.transform.position).sqrMagnitude).FirstOrDefault(); Log($"{tag}: paredões · {cw.id} da cidade calado: som mais perto {(near != null ? $"{near.car.id}{(near.local ? " (som do açaí)" : "")} a {(near.car.transform.position - cw.transform.position).magnitude:0} u" : "nenhum")} · {FBRole.WallDiag}"); } yield break; }
            var cp = city.car.transform.position; var other = FBVehicles.list.Where(c => c && c != city.car && FBParedao.Has(c) && FBVehicles.CarOf(FBMatch.Player) != c && (c.transform.position - cp).magnitude < 150).OrderBy(c => (c.transform.position - cp).sqrMagnitude).FirstOrDefault();
            if (!other || FBMediaLib.lib.Count == 0) { Log($"{tag}: paredões · sem outro carro com som perto ({(other ? "sem faixas" : "nenhum carro")})"); yield break; }
            int m0 = FBRole.WallMutes; FBParedao.Play(other, FBMediaLib.lib[0], 0); Log($"{tag}: paredões · liguei o som do {other.id} a {(other.transform.position - cp).magnitude:0} u do paredão da cidade ({city.car.id})");
            yield return Wait(5); Log($"{tag}: paredões · da cidade ligado {FBParedao.State(city.car)?.on} · {Regra()} · calou {FBRole.WallMutes > m0} · {Pares()} · regra rodou {FBRole.WallTicks}x · {FBRole.WallDiag}");
            { var T = other.transform; var pq = T.position - T.right * 12; if (FBWorld.GroundY(pq.x, pq.y + 3, pq.z, .4f, 8) is float gq) pq.y = gq; PlayerTo(pq, T.eulerAngles.y); yield return Wait(.8f); Look(T.position - T.right * 7 + Vector3.up * 3, T.position + Vector3.up, 55); yield return null; Shot($"{tag}_paredoes"); }   /* r45k: jogador perto (o FBCarCull esconde carro longe) */
            FBParedao.SetOn(other, false); yield return Wait(6); Log($"{tag}: paredões · desliguei o {other.id}: da cidade voltou {FBParedao.State(city.car)?.on} · {Pares()} · {FBRole.WallDiag}");
        }
        IEnumerator Perseguicao(string tag)
        {
            yield return BackToStart();
            for (int run = 0; run < 3; run++)   /* r45q: 3 perseguições (mais amostras) */
            {
                if (run > 0) yield return BackToStart();   /* r45r: cada uma começa do carro do rolê (no fim da anterior o jogador ficava onde a viatura parou, longe do trânsito) */
                bool ok = false; string why = null; for (int k = 0; k < 25 && !ok; k++) { ok = FBTransito.StartChase(true); why = FBTransito.ChaseWhy; if (!ok) yield return Wait(2); }
                if (!ok) { Log($"{tag}: perseguição NÃO começou ({why})"); yield break; }
                var cop = FBTransito.ChaseCop; var f = cop?.fugitive; if (cop == null || f == null) { Log($"{tag}: perseguição sem viatura"); yield break; }
                string fk = f.car.kind == "moto" ? "moto" : "carro"; int e0 = FBTransito.ChaseEnds, c0 = FBTransito.ChaseCaught, k0 = FBTransito.ChaseKTurns; float gMin = 1e9f, gMax = 0, vMax = 0, vCop = 0, tp = 0; int sirOn = 0, n = 0, sig = FBTransito.SigStops, y0 = FBTransito.Car.Yields; float copStop = 0;
                for (float t = 0; t < 80 && FBTransito.ChaseEnds == e0; t += Time.deltaTime)
                {
                    if (!cop.car || f.car == null) break; var T = cop.car.transform; float g = (cop.pos - f.pos).magnitude; gMin = Mathf.Min(gMin, g); gMax = Mathf.Max(gMax, g); vMax = Mathf.Max(vMax, f.v); vCop = Mathf.Max(vCop, cop.v);
                    n++; if (FBSiren.Mode(cop.car) > 0 && FBSiren.Lights(cop.car)) sirOn++; if (cop.v < .5f && !f.caught) copStop += Time.deltaTime;
                    if ((tp -= Time.deltaTime) <= 0) { tp = 1.5f; var pp = T.position - T.forward * 22 + T.right * 10; if (FBWorld.GroundY(pp.x, pp.y + 4, pp.z, .4f, 10) is float gy) pp.y = gy; PlayerTo(pp, T.eulerAngles.y); }
                    { var aim = (f.pos - T.position).magnitude < 45 ? Vector3.Lerp(T.position, f.pos, .5f) : T.position + T.forward * 9; Look(T.position - T.forward * 9 + T.right * 2.5f + Vector3.up * 3.5f, aim + Vector3.up, 55); }   /* r45e: viatura sempre no quadro */
                    if (Mathf.Repeat(t, 5) < Time.deltaTime && (f.v < .6f || cop.v < .5f)) Log($"{tag}: perseguição {run} t={t:0}s PARADO · fugitivo: {FBTransito.Chain(f)} · viatura: {FBTransito.Chain(cop)}");   /* r45n */
                    if (Mathf.Repeat(t, 5) < Time.deltaTime) { Shot($"{tag}_perseguicao{run}_{(int)(t / 5)}"); Log($"{tag}: perseguição {run} t={t:0}s · viatura {cop.car.id} {cop.v * 3.6f / 1.17f:0} km/h · {fk} {f.car.id} {f.v * 3.6f / 1.17f:0} km/h · distância {g:0} m · sirene {FBSiren.Mode(cop.car)} giroflex {FBSiren.Lights(cop.car)} · pegou {f.caught} · frente da viatura: {cop.leadWho} · rota comum {cop.route.Intersect(f.route).Count()}"); }
                    yield return null;
                }
                Log($"{tag}: PERSEGUIÇÃO {run} ({fk}): fim '{FBTransito.ChaseWhy}' · meias-voltas nesta perseguição {FBTransito.ChaseKTurns - k0} · viatura parada {copStop:0} s · carros que abriram caminho {FBTransito.Car.Yields - y0} · empurrões em parados {FBTransito.Car.Pushes} · objetos da rua empurrados {FBTransito.PropPushes} · passou rente por quem encostou {FBTransito.SirenPasses} · pegou {FBTransito.ChaseCaught - c0} · distância {gMin:0}–{gMax:0} m · máx fugitivo {vMax * 3.6f / 1.17f:0} km/h, viatura {vCop * 3.6f / 1.17f:0} km/h · sirene+giroflex {sirOn}/{n} quadros · vermelhos respeitados por outros {FBTransito.SigStops - sig}");
                yield return Wait(2);
            }
        }

        // ---------------- carro do açaí
        IEnumerator Acai(string tag, float fps0)
        {
            FBStreetEvents.ForceNear = 42; FBStreetEvents.ForceAcai = true; yield return Wait(1.5f); FBStreetEvents.ForceNear = -1;
            if (!FBAcai.Active) { Log($"{tag}: açaí NÃO entrou ({FBStreetEvents.Log})"); yield break; }
            var car = FBAcai.Car; var src = FBAcai.Source; var tc = FBAcai.Traffic;
            Log($"{tag}: açaí {car.id} · cor {ColorUtility.ToHtmlStringRGB(car.paint)} · roda {car.wid} (fábrica {Json.S(car.def, "stockWheel")}) · paredão {(car.soundWallNode ? "SIM (erro)" : "não")} · teto {tc?.vCap:0.0} m/s · som {(src ? src.clip.name + " loop=" + src.loop + " 3D=" + src.spatialBlend : "AUSENTE")}");
            int samples = 0, offRoad = 0, overlap = 0, restarts = 0, srcCount = 0; float vSum = 0, vMax = 0, latMax = 0, stuck = 0, lastT = src ? src.time : 0, fpsSum = 0; int fpsN = 0;
            for (float t = 0; t < 40; t += Time.unscaledDeltaTime)
            {
                if (!car) { Log($"{tag}: açaí sumiu aos {t:0} s"); break; }
                fpsSum += 1 / Mathf.Max(1e-4f, Time.unscaledDeltaTime); fpsN++;
                var p = car.transform.position; var f = car.transform.forward;
                Look(p - f * 9 + car.transform.right * 4 + Vector3.up * 3.2f, p + Vector3.up * .8f, 50);
                if (src) { if (src.time + 1 < lastT && lastT < src.clip.length - 1.5f) restarts++; lastT = src.time; }
                if ((int)(t * 2) != (int)((t - Time.unscaledDeltaTime) * 2))
                {
                    samples++; if (!FBRoads.IsRoad(p)) offRoad++;
                    var L = FBRoads.NearestLane(p, 8, out _, out float lat, f); latMax = Mathf.Max(latMax, L != null ? Mathf.Abs(lat) : 9);
                    float v = tc != null ? tc.v : car.v; vSum += v; vMax = Mathf.Max(vMax, v); if (v < .2f) stuck += .5f; else stuck = 0;
                    foreach (var o in FBVehicles.list) if (o && o != car && FBTransito.BoxHit(p, f.Flat().normalized, car.lbox.extents.z * car.scale * .95f, car.lbox.extents.x * car.scale * .95f, o)) { overlap++; break; }
                    srcCount = car.GetComponentsInChildren<AudioSource>().Count(s => s.clip && s.clip.name == "acai_carro");
                }
                if (Mathf.Abs(t - 3) < .02f || (t > 3 && t - Time.unscaledDeltaTime < 3)) Shot($"{tag}_acai_a");
                if (t > 14 && t - Time.unscaledDeltaTime <= 14) Shot($"{tag}_acai_b");
                yield return null;
            }
            Log($"{tag}: açaí 40 s · fora do asfalto {offRoad}/{samples} · longe da faixa máx {latMax:0.00} m · vel média {vSum / Mathf.Max(1, samples):0.0} m/s (máx {vMax:0.0}) · sobreposição {overlap} · parado no fim {stuck:0} s · fontes do jingle {srcCount} · reinícios {restarts} · FPS {fps0:0} -> {fpsSum / Mathf.Max(1, fpsN):0}");
            // áudio REAL na saída: ouvido ao lado do carro (acompanha), com e sem o jingle
            if (car && src)
            {
                var others = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude).Where(l => l.enabled).ToList(); foreach (var l in others) l.enabled = false;
                var eg = new GameObject("OuvidoTeste"); eg.AddComponent<AudioListener>(); var ear = eg.transform; var buf = new float[2048]; yield return null;
                float Rms() { float a = 0; AudioListener.GetOutputData(buf, 0); foreach (var v in buf) a += v * v; AudioListener.GetOutputData(buf, 1); foreach (var v in buf) a += v * v; return Mathf.Sqrt(a / (2 * buf.Length)); }
                var res = new List<string>();
                foreach (var d in new[] { 5f, 15f, 40f, 90f, 160f })
                {
                    float on = 0, off = 0;
                    for (int pass = 0; pass < 2 && car; pass++)
                    {
                        if (FBAcai.Wall != null) FBAcai.Mute = pass == 1; for (float w = 0; w < .8f; w += Time.unscaledDeltaTime) { if (car) ear.position = car.transform.position + car.transform.right * d + Vector3.up * 1.6f; yield return null; }
                        float acc = 0; for (int k = 0; k < 24 && car; k++) { ear.position = car.transform.position + car.transform.right * d + Vector3.up * 1.6f; ear.rotation = Quaternion.LookRotation(-car.transform.right); acc += Rms(); yield return null; }
                        if (pass == 0) on = acc / 24; else off = acc / 24;
                    }
                    FBAcai.Mute = false; res.Add($"{d:0} m: {on:0.0000} (sem {off:0.0000})");
                }
                Log($"{tag}: açaí nível na saída — " + string.Join(" · ", res) + $" · volume do paredão {FBAcai.Wall?.vol:0.00} · prédio na frente {FBAcai.Wall?.occK:0.00}");
                Destroy(eg); foreach (var l in others) if (l) l.enabled = true;
            }
            // uma volta e vai embora (26/10): espera o carro sair de cena (até 150 s)
            float tl = -1, tg = -1; for (float t = 0; t < 150 && FBAcai.Active; t += Time.unscaledDeltaTime) { if (tl < 0 && FBAcai.Leaving) tl = t; if (FBAcai.Car) { var T = FBAcai.Car.transform; Look(T.position - T.forward * 9 + Vector3.up * 3.2f, T.position + Vector3.up, 50); } yield return null; }
            if (!FBAcai.Active) tg = 1;
            Log($"{tag}: açaí indo embora {(tl >= 0 ? $"aos {tl:0} s depois da medição" : "NÃO")} · saiu de cena {(tg > 0 ? "sim" : "NÃO (ainda rodando após 150 s)")}");
            // r42: FUGA sob tiro — disparo a 8 m do carro; mede se entrou em fuga, a aceleração (velocidade máxima em 8 s) e se saiu da faixa.
            // r43: o primeiro carro já foi embora (passa pelo jogador parado e sai) — chama outro e atira 4 s depois que ele entra
            if (!FBAcai.Active) { FBStreetEvents.ForceNear = 42; FBStreetEvents.ForceAcai = true; yield return Wait(1.5f); FBStreetEvents.ForceNear = -1; yield return Wait(4); }
            if (!(FBAcai.Active && FBAcai.Car && FBAcai.Traffic != null)) Log($"{tag}: açaí sob tiro · carro NÃO entrou para o teste ({FBStreetEvents.Log})");
            if (FBAcai.Active && FBAcai.Car && FBAcai.Traffic != null)
            {
                var ac = FBAcai.Car; float v0 = FBAcai.Traffic.v, vmax = v0; int off = 0, n = 0;
                FBBotBrain.Noise(ac.transform.position + ac.transform.right * 8 + Vector3.up);
                for (float w = 0; w < 8 && FBAcai.Car; w += Time.unscaledDeltaTime)
                {
                    if (FBAcai.Traffic != null) vmax = Mathf.Max(vmax, FBAcai.Traffic.v); n++; if (!FBRoads.IsRoad(FBAcai.Car.transform.position)) off++;
                    var T = FBAcai.Car.transform; Look(T.position - T.forward * 9 + Vector3.up * 3.5f, T.position, 55); if (w > 3 && w - Time.unscaledDeltaTime <= 3) Shot($"{tag}_acai_fuga"); yield return null;
                }
                Log($"{tag}: açaí sob tiro · fugindo {FBAcai.Fleeing} · velocidade {v0:0.0} -> máx {vmax:0.0} m/s em 8 s · fora do asfalto {off}/{n}");
            }
        }

        // ---------------- baculejo
        bool shotRoda;
        IEnumerator Baculejo(string tag, float fps0)
        {
            shotRoda = false;
            // r43: bots do modo desligados durante a cena (r42c: um bot atirou num abordado aos 26 s e a cena dispersou — vale no jogo, mas
            // o teste mede as etapas do baculejo, não o tiroteio em volta)
            var botsOff = FBMatch.Bots.Where(b => b && b.gameObject.activeSelf).ToList(); foreach (var b in botsOff) b.gameObject.SetActive(false);
            var aa = System.Environment.GetCommandLineArgs(); int di = System.Array.IndexOf(aa, "-fbbacdesfecho"); if (di >= 0 && di + 1 < aa.Length) FBBaculejo.ForceOutcome = aa[di + 1];
            FBStreetEvents.ForceNear = 30; FBStreetEvents.ForceBaculejo = true; yield return Wait(1.2f); FBStreetEvents.ForceNear = -1;
            if (!FBBaculejo.Active) { Log($"{tag}: baculejo NÃO montou ({FBStreetEvents.Log})"); yield break; }
            var sus = FBBaculejo.Suspects.ToList(); Log($"{tag}: baculejo r42 · desfecho {FBBaculejo.Outcome} · {sus.Count} na roda");
            // r42: a viatura e os PMs só existem depois da roda (chegada) — espera aparecerem para seguir com as medidas de antes
            var c0 = sus.Count > 0 && sus[0].c ? sus[0].c.Pos : FBStreetEvents.PlayerPos;
            for (float w = 0; w < 30 && FBBaculejo.Active && FBBaculejo.State == "roda"; w += Time.unscaledDeltaTime)
            { if (w > 2 && !shotRoda) { shotRoda = true; Look(c0 + new Vector3(3.5f, 2.2f, 3.5f), c0 + Vector3.up, 50); yield return null; Shot($"{tag}_bac_roda"); } yield return null; }
            var car = FBBaculejo.Car; var fr = FBBaculejo.Frisker; var cv = FBBaculejo.Cover;
            if (!car) { Log($"{tag}: baculejo sem viatura depois da roda (estado {FBBaculejo.State})"); yield break; }
            Log($"{tag}: baculejo · viatura {car.id} no asfalto {FBRoads.IsRoad(car.transform.position)} · {sus.Count} abordado(s) ({string.Join(",", sus.Select(s => s.c ? s.c.skinId + (s.borrowed ? "[da rua]" : "[novo]") : "?"))}) · PMs {fr.c?.skinId}/{cv.c?.skinId} · arma da cobertura {cv.c?.weapon?.id ?? "NENHUMA"}");
            Log($"{tag}: elenco em cena (r52) · {FBElenco.SceneCheck()}");
            string last = null; float fpsSum = 0; int fpsN = 0, bad = 0; var shots = new HashSet<string>(); float minGap = 9, maxGap = 0;
            for (float t = 0; t < 200 && FBBaculejo.Active; t += Time.unscaledDeltaTime)
            {
                fpsSum += 1 / Mathf.Max(1e-4f, Time.unscaledDeltaTime); fpsN++;
                var st = FBBaculejo.State;
                if (st != last) { Log($"{tag}: baculejo estado {st} aos {t:0.0} s"); last = st; }
                // câmera: 3/4 da cena (enquanto a viatura está parada); saindo, acompanha a viatura
                var ctr = sus.Where(s => s.c).Select(s => s.c.Pos).DefaultIfEmpty(c0).Aggregate(Vector3.zero, (a, b) => a + b) / Mathf.Max(1, sus.Count(s => s.c));
                if (st == "partiu" && FBBaculejo.Car) { var cp = FBBaculejo.Car.transform; Look(cp.position - cp.forward * 10 + cp.right * 3 + Vector3.up * 3.5f, cp.position + Vector3.up, 50); }
                else if ((st == "chegada" || st == "encosta") && FBBaculejo.Car) { var cp = FBBaculejo.Car.transform; Look(ctr + (cp.position - ctr).Flat().normalized * -4 + Vector3.up * 3, cp.position + Vector3.up, 55); }
                else if (st == "perseguicao" || st == "confronto") { var all = sus.Where(s => s.c).Select(s => s.c.Pos).Concat(new[] { fr?.c, cv?.c }.Where(x => x).Select(x => x.Pos)).ToList(); var m = all.Aggregate(Vector3.zero, (a, b) => a + b) / Mathf.Max(1, all.Count); Look(m + new Vector3(0, 9, -7), m, 60); }
                else if (car) Look(ViewPoint(ctr, car.transform.position), ctr + Vector3.up * 1.0f, 50);
                // medidas a cada 0,5 s
                if ((int)(t * 2) != (int)((t - Time.unscaledDeltaTime) * 2))
                {
                    foreach (var a in new[] { fr, cv }.Concat(sus)) if (a?.c && a.c.alive && !a.c.vehiclePose && !FBStreetEvents.Free(a.c.Pos, .2f)) { bad++; if (bad <= 6) Log($"{tag}: {a.c.skinId} com a cápsula dentro de algo em {a.c.Pos} ({st})"); }
                    if (st == "revista" && fr?.c && fr.c.dance != null) foreach (var s in sus) if (s.c && !s.fled) { float g = (s.c.Pos - fr.c.Pos).Flat().magnitude; if (g < 1) { minGap = Mathf.Min(minGap, g); maxGap = Mathf.Max(maxGap, g); } }
                }
                foreach (var k in new[] { "chegada", "encosta", "desembarque", "abordagem", "enquadro", "revista", "anota", "fala", "libera", "volta", "partiu", "perseguicao", "confronto", "tumulto" })
                    if (st == k && !shots.Contains(k)) { shots.Add(k); StartCoroutine(Later(.9f, $"{tag}_bac_{k}")); }
                if (st == "revista" && t > 6 && !shots.Contains("revista2") && car)
                {   // de perto e de lado (PM atrás do abordado)
                    shots.Add("revista2"); var aw = (ctr - car.transform.position).Flat().normalized; var sd = new Vector3(aw.z, 0, -aw.x);
                    Look(ctr + sd * 2.6f - aw * .6f + Vector3.up * 1.5f, ctr + Vector3.up * 1.05f, 55); yield return null; Shot($"{tag}_bac_revista_perto"); yield return null; yield return null;
                }
                if (st == "anota" && !shots.Contains("giroflex") && car && FBGiroflex.Lit(car))
                {   // giroflex próprio piscando durante a abordagem (de frente, por cima)
                    shots.Add("giroflex"); var T = car.transform; Look(T.position + T.forward * 6.5f + T.right * 2.5f + T.up * 3.2f, T.position + T.up * 1.9f, 42); yield return null;
                    for (int w = 0; w < 60 && !FBGiroflex.Lit(car); w++) yield return null; Shot($"{tag}_bac_giroflex"); yield return null; yield return null;
                }
                if (!shots.Contains("dentro") && FBBaculejo.Car && fr?.c && cv?.c && fr.c.vehiclePose && cv.c.vehiclePose)
                {   // PMs sentados (26/10: "poses bugadas dentro do veículo"): pelas duas janelas e pelo para-brisa, com o giroflex próprio
                    shots.Add("dentro"); yield return Wait(.6f); var u = FBBaculejo.Car; var T = u.transform; var sp = u.L2W(u.seat) + T.up * .35f; var pp = u.L2W(new Vector3(-u.seat.x, u.seat.y, u.seat.z)) + T.up * .35f;
                    float sx = u.lbox.extents.x * u.scale + 1.1f;
                    var an = cv.c.an; int LU = an ? an.GetLayerIndex("Upper Body") : -1;
                    Log($"{tag}: sentados · motorista {fr.c.skinId} clipe {fr.c.sys} · carona {cv.c.skinId} clipe {cv.c.sys} Upper Body {(LU >= 0 ? an.GetLayerWeight(LU) : -1):0.00} · giroflex próprio {FBGiroflex.OwnCount(u)}");
                    foreach (var (n, eye, at) in new[] { ("janela_motorista", sp + T.right * Mathf.Sign(u.seat.x) * sx + T.up * .25f, sp), ("janela_carona", pp - T.right * Mathf.Sign(u.seat.x) * sx + T.up * .25f, pp), ("parabrisa", Vector3.Lerp(sp, pp, .5f) + T.forward * 3.2f + T.up * .5f, Vector3.Lerp(sp, pp, .5f)), ("teto", T.position + T.forward * 7 + T.right * 3 + T.up * 3.5f, T.position + T.up * 1.8f) })
                    { Look(eye, at, n == "teto" ? 45 : 60); yield return null; if (n == "teto") for (int w = 0; w < 90 && !FBGiroflex.Lit(u); w++) { Look(T.position + T.forward * 7 + T.right * 3 + T.up * 3.5f, T.position + T.up * 1.8f, 45); yield return null; } Shot($"{tag}_bac_{n}"); yield return null; yield return null; }
                }
                yield return null;
            }
            Log($"{tag}: baculejo fim · PM atrás do abordado {minGap:0.00}–{maxGap:0.00} m · cápsulas presas {bad} · FPS {fps0:0} -> {fpsSum / Mathf.Max(1, fpsN):0} · ativo ainda {FBBaculejo.Active}");
            foreach (var b in botsOff) if (b) b.gameObject.SetActive(true);
        }
        // ---------------- veículos (26/10): máxima por carro + câmbio, passos dentro do carro, buzinas, velocímetro, avião do céu
        IEnumerator Veiculos(string tag)
        {
            var cars = FBVehicles.list.Where(c => c && c.kind == "car" && !c.driver).GroupBy(c => c.id).Select(g => g.First()).ToList();
            // 1) curva de aceleração (só a conta do motor/câmbio, sem andar no mapa): 0–100, máxima alcançada em 60 s, trocas
            foreach (var c in cars.Take(14))
            {
                var Kd = FBDrive.KindOf(c); var gb = new FBCarSound.Gearbox(FBDrive.TopV(c)); float v = 0, t100 = -1, t = 0; int ups = 0, downs = 0; var gearsAt = new List<string>();
                for (; t < 60; t += 1 / 60f)
                {
                    float cut = gb.shiftT > 0 ? .3f : 1; v += FBDrive.PowerAcc(c, Kd, v, 1) * cut / 60f; v = Mathf.Min(v, FBDrive.TopV(c));
                    var g = gb.Step(1 / 60f, v, 1); if (g.shifted > 0) { ups++; gearsAt.Add($"{g.gear}a@{v * 3:0}"); } else if (g.shifted < 0) downs++;
                    if (t100 < 0 && v * 3 >= 100) t100 = t;
                }
                Log($"{tag}: {c.id} · máxima {FBDrive.TopKmh(c):0} km/h · alcançou {v * 3:0} em 60 s · 0–100 {(t100 < 0 ? "não chega" : t100.ToString("0.0") + " s")} · trocas {string.Join(" ", gearsAt)} · reduções {downs} · rpm final {gb.rpm:0} · buzina {FBCarSound.HornFor(c)}");
            }
            Log($"{tag}: buzinas diferentes em {FBVehicles.list.Count(c => c)} veículos: {FBVehicles.list.Where(c => c).Select(FBCarSound.HornFor).Distinct().Count()} ({string.Join(",", FBVehicles.list.Where(c => c).Select(FBCarSound.HornFor).Distinct())})");
            // 2) dirigindo de verdade: passos do jogador dentro do carro (devem ser 0), velocímetro na tela
            var me = FBMatch.Player; var car = cars.OrderByDescending(c => { c.SetColliders(false); float d = FBWorld.RayHit(c.transform.position + Vector3.up, c.transform.forward, 60, out var h) ? h.distance : 60; c.SetColliders(true); return d; }).FirstOrDefault();
            bool entered = false;
            foreach (var cand in cars.OrderByDescending(c => { c.SetColliders(false); float d = FBWorld.RayHit(c.transform.position + Vector3.up, c.transform.forward, 60, out var h) ? h.distance : 60; c.SetColliders(true); return d; }).Take(5))
            {   // alguns carros do rolê não deixam entrar (trancado/ocupado): tenta os próximos
                if (!me) break; var cc0 = me.GetComponent<CharacterController>(); if (cc0) cc0.enabled = false; me.transform.position = cand.transform.position + cand.transform.right * -(cand.lbox.extents.x * cand.scale + .8f); if (cc0) cc0.enabled = true; yield return null;
                if (FBDrive.Enter(me, cand)) { car = cand; entered = true; break; }
            }
            if (me && car)
            {
                if (entered)
                {
                    int steps = 0; FBFootsteps.trace = r => { if (r.ch == me) steps++; };
                    float vmax = 0; for (float t = 0; t < 4.5f; t += Time.deltaTime) { FBDrive.Simulate(car, me, 1, 0, false, Time.deltaTime); vmax = Mathf.Max(vmax, car.v); var T = car.transform; Look(T.position - T.forward * 7 + Vector3.up * 2.6f, T.position + T.forward * 4, 55); yield return null; }
                    Shot($"{tag}_velocimetro");
                    for (float t = 0; t < 3 && Mathf.Abs(car.v) > .2f; t += Time.deltaTime) { FBDrive.Simulate(car, me, -1, 0, false, Time.deltaTime); yield return null; }
                    FBFootsteps.trace = null; Log($"{tag}: dirigindo {car.id} · {vmax * 3:0} km/h em 4,5 s · marcha {car.gear} · passos do jogador dentro do carro {steps}");
                    FBDrive.Exit(me, true); yield return Wait(.5f);
                }
                else Log($"{tag}: não entrou no {car.id}");
            }
            // 4) foto reels 9:16 (F10) da cena atual
            if (me) { var T = me.transform; Look(T.position - T.forward * 4 + Vector3.up * 2.2f, T.position + Vector3.up * 1.2f, 55); }
            if (Camera.main && cam) { var mc = Camera.main.transform; mc.SetPositionAndRotation(cam.transform.position, cam.transform.rotation); }
            FBScreenshot.CaptureReel(2160); yield return Wait(4);
            var rp = FBScreenshot.LastReel; if (rp != null && System.IO.File.Exists(rp)) { var info = new System.IO.FileInfo(rp); var tx = new Texture2D(2, 2); tx.LoadImage(System.IO.File.ReadAllBytes(rp)); Log($"{tag}: reels 9:16 {tx.width}×{tx.height} · {info.Length / 1048576f:0.0} MB · {rp}"); Destroy(tx); }
            else Log($"{tag}: reels 9:16 NÃO salvou");
            // 3) avião do céu: o 787 do BR passando (e de perto, para ver o modelo)
            if (me)
            {
                var bo = FBSkyPlanes.Boeing(); if (bo) { var pp = me.Pos + me.transform.forward * 70 + Vector3.up * 30; bo.transform.position = pp; bo.transform.rotation = Quaternion.LookRotation(me.transform.right); Look(pp - me.transform.forward * 60 + me.transform.right * 40 + Vector3.up * 15, pp, 55); yield return null; yield return null; Shot($"{tag}_boeing_perto"); yield return null; Destroy(bo); }
                Log($"{tag}: boeing787 {(bo ? "carregou" : "NÃO carregou (sem Acc/boeing787)")}");
                FBSkyPlanes.Flyover(me.Pos, me.Pos.y); yield return Wait(9);
                var pl = FBSkyPlanes.Current; var mc = Camera.main;
                if (pl && mc) { Look(mc.transform.position, pl.position, 30); yield return null; Shot($"{tag}_aviao_ceu"); Log($"{tag}: avião no céu {pl.name} escala {pl.localScale.x:0.00}"); }
                else Log($"{tag}: avião do céu não apareceu");
            }
        }
        IEnumerator Later(float s, string name) { yield return Wait(s); Shot(name); }
        // ponto de câmera com visão livre da cena (sem viatura nem parede no meio), de 5 a 8 m, girando em volta
        Vector3 viewCache; Vector3 viewFor = new(9999, 0, 0);
        Vector3 ViewPoint(Vector3 ctr, Vector3 carP)
        {
            if ((ctr - viewFor).sqrMagnitude < 1 && viewCache != Vector3.zero) return viewCache;
            var tgt = ctr + Vector3.up * 1.1f; var away = (ctr - carP).Flat().normalized; Vector3 best = ctr - away * 7 + Vector3.up * 2.4f;
            for (int k = 0; k < 24; k++)
            {
                float a = k * 15f, r = 5 + (k % 3); var dir = Quaternion.Euler(0, a, 0) * away; var p = ctr + dir * r + Vector3.up * 2.2f;
                var d = tgt - p; if (Physics.Raycast(p, d.normalized, d.magnitude - .4f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.CheckSphere(p, .3f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore)) continue;
                best = p; break;
            }
            viewFor = ctr; viewCache = best; return best;
        }
    }
}
