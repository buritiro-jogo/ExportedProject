// Autoteste de CARROS E TRÂNSITO (rodada 45): FreeBala.exe -fbcarrolab [mapa:modo]   (padrão vila_jardim:role)
//  1) vagas: o que a validação fez (ok/movidos/sem vaga), parados no meio da faixa e sobrepostos que sobraram, erro de chão das rodas;
//  2) semáforos: cruzamentos/postes e um print do poste do 171 aceso;
//  3) trânsito por 120 s (tempo ×2): contramão, fora da faixa, sumiço À VISTA (salto > 15 m de um carro que estava na tela), paradas
//     no vermelho, saídas da vaga pelo clipe e entradas de pedestre pelo clipe;
//  4) entrar e sair do carro com os clipes do 171: fase, porta, quadril × banco, lado em que desceu, prints de lado;
//  5) dano numa pista isolada: batida leve (20 km/h), moderada (45) e grave (75) num muro inclinado, estado do dano depois de cada uma,
//     e o carro batido andando 4 s em linha reta sem tocar no volante (puxa para o lado batido?) + velocidade máxima.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBCarroLab : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbcarrolab"); if (i < 0) return;
            var g = new GameObject("FB_CarroLab"); DontDestroyOnLoad(g); var t = g.AddComponent<FBCarroLab>(); if (i + 1 < a.Length && !a[i + 1].StartsWith("-")) t.run = a[i + 1];
        }
        string run = "vila_jardim:role", dir; float cThr, cSt; Camera cam;
        static void Log(string s) => Debug.Log("[FreeBala][carrolab] " + s);
        IEnumerator Wait(float s) { for (float w = 0; w < s; w += Time.unscaledDeltaTime) yield return null; }
        IEnumerator Shot(string n) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png")); yield return null; yield return null; }
        void Look(Vector3 from, Vector3 at, float fov = 50) { cam.transform.position = from; cam.transform.LookAt(at); cam.fieldOfView = fov; }
        IEnumerator Reta(FBCar dc, string quando)
        {   // reta sem volante: o carro batido puxa para o lado batido? e a máxima
            var wl = GameObject.Find("FB_Muro"); if (wl) wl.SetActive(false); var T = dc.transform; dc.v = 0; yield return Wait(.5f); float yaw0 = T.eulerAngles.y; cThr = 1; cSt = 0; float vm = 0;
            for (float t = 0; t < 7; t += Time.deltaTime) { vm = Mathf.Max(vm, dc.v * 3); yield return null; }
            cThr = 0; float drift = Mathf.DeltaAngle(yaw0, T.eulerAngles.y);
            Log($"reta de 7 s sem volante {quando}: desvio {drift:+0.0;-0.0}° (− = esquerda) · máxima {vm:0} km/h · {FBCarDamage.DebugInfo(dc)}");
            cThr = -1; yield return Wait(2); cThr = 0; yield return Wait(1); dc.v = 0;
        }
        IEnumerator Start()
        {
            dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste", "carro"); System.IO.Directory.CreateDirectory(dir);
            yield return Wait(3); var mm = run.Split(':'); GameData.Load(); FBSession.Load(); var keep = (FBSession.map, FBSession.mode, FBSession.view);
            FBSession.map = mm[0]; FBSession.mode = mm.Length > 1 ? mm[1] : "role"; FBSession.online = false; FBSession.view = "tps"; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_" + mm[0]); while (op != null && !op.isDone) yield return null; yield return Wait(8);
            var me = FBMatch.Player; if (!me) { Log("sem jogador"); yield break; } FBMatch.God = true;
            if (FBGarage.Open_) { FBGarage.AutoPick(); yield return Wait(1.5f); }                       // rolê começa na garagem: pega o carro e desce
            if (FBVehicles.CarOf(me)) { FBDrive.Exit(me, true); yield return Wait(1); }
            cam = new GameObject("CarroLabCam").AddComponent<Camera>(); cam.depth = 100; cam.cullingMask = Camera.main ? Camera.main.cullingMask : ~0; cam.enabled = false;
            // ---- 1. vagas
            int inLane = 0, overlap = 0, parked = 0; var P0 = FBVehicles.list.Where(c => c && !c.driver && c.kind != "moto" && FBTransito.List.All(t => t.car != c)).ToList();
            for (int ci = 0; ci < P0.Count; ci++)
            {
                var c = P0[ci]; parked++; float lat = 0; var S = FBRoads.Ready ? FBRoads.NearestLane(c.transform.position, 6, out _, out lat) : null; if (S != null && Mathf.Abs(lat) < S.w / 2 * .55f) inLane++;
                for (int oi = ci + 1; oi < P0.Count; oi++) { var o = P0[oi]; if ((o.transform.position - c.transform.position).sqrMagnitude < 64)
                    { var L = c.lbox; var e = L.extents * c.scale; if (FBTransito.BoxHit(c.transform.TransformPoint(L.center), c.transform.forward, e.z * .95f, e.x * .95f, o)) overlap++; } }
            }
            Log($"vagas: {FBVehicles.ParkOk} ok · {FBVehicles.ParkMoved} movidos · {FBVehicles.ParkDropped} sem lugar · erro do chão nas rodas até {FBVehicles.SettleErr:0.000} m · agora {parked} estacionados, {inLane} no meio da faixa, {overlap} pares sobrepostos");
            foreach (var l in FBVehicles.ParkLog.Take(12)) Log("  vaga: " + l);
            // ---- 1b. escala: altura da cabeça do personagem × objetos do 171 × carro (medidas reais de referência)
            { var head = me.an ? me.an.GetBoneTransform(HumanBodyBones.Head) : null; float hh = head ? head.position.y - me.Pos.y : -1; float k = hh / 1.62f;
              string H(string n, float real) { var o = FBVestirRua.Root ? FBVestirRua.Root.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.parent == FBVestirRua.Root.transform && t.name.StartsWith("x171_" + n)) : null; if (!o) return n + " -"; var rs = o.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return $"{n} {b.size.y:0.00} u ({b.size.y / k:0.00} m pela escala do corpo; real ~{real:0.00} m)"; }
              var pc = FBVehicles.list.FirstOrDefault(c => c && c.kind != "moto" && !c.heavy); float ch = pc ? pc.lbox.size.y * pc.scale : -1;
              Log($"escala: cabeça do jogador a {hh:0.00} u do chão (real ~1,62 m -> {k:0.00} u/m) · carro {pc?.id} {ch:0.00} u ({ch / k:0.00} m; real ~1,45 m) · {H("SM_lixeiras", 1.0f)} · {H("SM_Orelhao", 2.3f)} · {H("SM_Mesa1", .72f)} · {H("SM_Cadeira1", .8f)} · {H("Placas", 2.6f)} · {H("PontoOnibus", 2.7f)}"); }
            // ---- 2. semáforos
            Log($"semáforos: {FBSemaforo.Junctions} cruzamentos · {FBSemaforo.Heads} postes");
            var heads = FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name.StartsWith("semaforo_")).OrderBy(t => (t.position - me.Pos).sqrMagnitude).Take(2).ToList();
            foreach (var h in heads)
            {
                var f = -h.right; cam.enabled = true; Look(h.position + Vector3.up * 3.5f - h.forward * 1 + h.right * 9, h.position + Vector3.up * 3.6f + h.forward * 1.5f, 45);
                for (int k = 0; k < 3; k++) { yield return Shot($"semaforo_{h.name}_{k}"); yield return Wait(4); }
                cam.enabled = false;
            }
            // ---- 3. trânsito
            if (FBTransito.Running)
            {
                FBTransito.TestParking();
                Time.timeScale = 2; float t0 = Time.unscaledTime; int samples = 0, wrong = 0, offLane = 0, vanishSeen = 0, fastSeen = 0; var last = new Dictionary<FBCar, (Vector3 p, bool vis, float t, float v)>();
                int sig0 = FBTransito.SigStops, out0 = FBTransito.OutsDone, in0 = FBTransito.InsDone, van0 = FBTransito.Vanished;
                var mc = Camera.main;
                while (Time.unscaledTime - t0 < 60)
                {
                    yield return Wait(.5f); samples++; var planes = mc ? GeometryUtility.CalculateFrustumPlanes(mc) : null;
                    foreach (var c in FBTransito.List)
                    {
                        if (!c.car) continue; var p = c.car.transform.position; bool vis = planes != null && GeometryUtility.TestPlanesAABB(planes, new Bounds(p + Vector3.up, new Vector3(4, 2, 4))) && (p - mc.transform.position).magnitude < 120;
                        if (last.TryGetValue(c.car, out var lp) && lp.vis && (p - lp.p).magnitude > 15)
                        {   /* r45q: cada amostra tem ~1 s de jogo (tempo x2): moto em fuga e viatura passam de 15 u sem sumir. Separa o salto de verdade
                               (sumiu/reapareceu: andou mais do que a velocidade permite) do veículo rápido */
                            float dg = Time.time - lp.t, mv = (p - lp.p).magnitude, vv = Mathf.Max(c.v, lp.v); bool drove = mv < vv * dg * 1.3f + 4;
                            if (drove) fastSeen++; else vanishSeen++;
                            Log($"  salto à vista: {(c.car.mx ? "moto" : "carro")} {c.car.id} modo {c.mode} · {mv:0} u em {dg:0.0} s de jogo · {vv / 1.17f * 3.6f:0} km/h · {(drove ? "andou (rápido)" : "SUMIU/REAPARECEU")}{(c.runLights || c.fugitive != null || c.chase != null ? " · perseguição" : "")}");
                        }
                        last[c.car] = (p, vis, Time.time, c.v);
                        if (c.mode != "drive" || c.route.Count == 0 || c.ri >= c.route.Count) continue;
                        var S = FBRoads.segs[c.route[c.ri]]; FBRoads.At(S, c.s, out var tan); tan.y = 0; var cf = c.car.transform.forward; cf.y = 0;
                        if (S.kind == 0 && c.overSide == 0 && Vector3.Dot(tan.normalized, cf.normalized) < 0) wrong++;
                        if (S.kind == 0 && c.overSide == 0) { FBRoads.Project(S, p, out _, out float dd); if (dd > 2.5f) offLane++; }
                    }
                }
                Time.timeScale = 1;
                Log($"trânsito 120 s de jogo: {FBTransito.List.Count} carros · amostras {samples} · contramão {wrong} · fora da faixa {offLane} · sumiu À VISTA {vanishSeen} (rápidos à vista, não somem: {fastSeen}) · reaparecidos longe {FBTransito.Vanished - van0} · esperando sair da vista {FBTransito.VanishWaits} · paradas no semáforo {FBTransito.SigStops - sig0} · desceram pelo clipe {FBTransito.OutsDone - out0} · entraram pelo clipe {FBTransito.InsDone - in0}");
            }
            else Log("trânsito não está rodando neste modo");
            // ---- 3b. luzes à noite com o trânsito: lentes que acendem/apagam por segundo por carro e fachos que trocam de dono
            FBWeather.ForceNight(true); yield return Wait(3); { int l0 = FBCarLights.LensToggles, b0 = FBCarLights.BeamSwaps; float t0 = Time.unscaledTime; int frames = 0; while (Time.unscaledTime - t0 < 20) { frames++; yield return null; }
              int cars = FBVehicles.list.Count(c => c && (c.driver || c.aiLights)); Log($"luzes à noite (20 s, {cars} carros com motor ligado): lentes trocando {(FBCarLights.LensToggles - l0) / 20f:0.0}/s no total ({(FBCarLights.LensToggles - l0) / 20f / Mathf.Max(1, cars):0.00}/s por carro; freio/seta legítimos entram aqui) · fachos trocando de carro {(FBCarLights.BeamSwaps - b0) / 20f:0.00}/s · fachos acesos {FBCarLights.BeamsOn}");
              var nc = FBTransito.List.Where(c => c.car && c.mode == "drive").OrderBy(c => (c.pos - me.Pos).sqrMagnitude).FirstOrDefault();
              if (nc != null) { cam.enabled = true; for (int i = 0; i < 3; i++) { var T = nc.car.transform; Look(T.position - T.forward * 7 + T.right * 2.5f + Vector3.up * 2, T.position + Vector3.up * .7f, 55); yield return Shot($"luz_noite_tras_{i}"); Look(T.position + T.forward * 9 + T.right * 2 + Vector3.up * 1.6f, T.position + Vector3.up * .7f, 55); yield return Shot($"luz_noite_frente_{i}"); yield return Wait(1.5f); } cam.enabled = false; } }
            FBWeather.ForceNight(false); yield return Wait(1);
            // ---- 4. entrar / sair do carro (171)
            var car = FBVehicles.list.Where(c => c && !c.driver && c.kind != "moto" && !c.ie && FBTransito.List.All(t => t.car != c)).OrderBy(c => (c.transform.position - me.Pos).sqrMagnitude).FirstOrDefault();
            if (car)
            {
                var door = car.L2W(car.seat) - car.transform.right * 1.6f; FBMatch.Teleport(me, door, car.transform.eulerAngles.y + 90); yield return Wait(1);
                if (FBVehicles.CarOf(me)) { FBDrive.Exit(me, true); yield return Wait(.5f); }
                bool ok = FBDrive.Enter(me, car); var dp = me.GetComponent<FBDriverPose>(); cam.enabled = true; Log($"entrar em {car.id}: pedido {ok} · portas no modelo {FBDoors.HasPart(car, 1)} · caminho {dp?.path} · {FBDrive.PathDiag}");
                var side = car.transform.position - car.transform.right * 5.5f + Vector3.up * 1.6f;
                foreach (var at in new[] { .3f, .8f, 1.4f, 2.0f, 2.6f })
                {
                    float t1 = Time.time; while (Time.time - t1 < (at == .3f ? .3f : .6f)) yield return null;
                    Look(side, car.transform.position + Vector3.up * .9f, 50); yield return Shot($"entrar_{at:0.0}".Replace(',', '_'));
                    var hips = me.an ? me.an.GetBoneTransform(HumanBodyBones.Hips) : null;
                    Log($"entrar t={at:0.0}s: pedido {ok} · fase {dp?.trans ?? "-"} · clipe {me.sys} · porta {FBDoors.Mask(car)} · quadril×banco {(hips ? (hips.position - car.L2W(car.seat)).magnitude : -1):0.00} m");
                }
                yield return Wait(1); bool ex = FBDrive.Exit(me, false);
                foreach (var at in new[] { .4f, 1.0f, 1.6f, 2.4f, 3.2f })
                {
                    float t1 = Time.time; while (Time.time - t1 < (at == .4f ? .4f : .6f + (at > 2 ? .2f : 0))) yield return null;
                    Look(side, car.transform.position + Vector3.up * .9f, 50); yield return Shot($"sair_{at:0.0}".Replace(',', '_'));
                    Log($"sair t={at:0.0}s: pedido {ex} · no carro {FBVehicles.CarOf(me) != null} · fase {me.GetComponent<FBDriverPose>()?.trans ?? "-"} · clipe {me.sys ?? "-"} · porta {FBDoors.Mask(car)}");
                }
                var rel = car.transform.InverseTransformPoint(me.transform.position); Log($"depois de sair: x {rel.x:+0.00;-0.00} (− = lado do motorista) z {rel.z:+0.00;-0.00} · de pé no chão {me.GetComponent<CharacterController>()?.isGrounded}");
                cam.enabled = false;
            }
            // ---- 5. dano: pista isolada com muro inclinado
            var p0 = me.Pos + Vector3.up * 300; var pad = new GameObject("FB_CarroPad").AddComponent<BoxCollider>(); pad.gameObject.layer = FBWorld.LMap; pad.size = new Vector3(3000, 2, 3000); pad.transform.position = p0 - Vector3.up;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "FB_Muro"; wall.layer = FBWorld.LMap; wall.transform.localScale = new Vector3(14, 3, 1); Physics.SyncTransforms();
            string cid = new[] { "GolG5", "Up", "GolG2", "CivicG10", "Corolla2018" }.FirstOrDefault(v => FBRegistry.I.Vehicle(v) && GameData.Vehicles.ContainsKey(v));
            var dc = cid != null ? FBVehicles.Spawn(cid, p0 + Vector3.up * .3f, 0) : null; yield return Wait(1);
            if (dc)
            {
                if (FBVehicles.CarOf(me)) { FBDrive.Exit(me, true); yield return Wait(.5f); } FBMatch.Teleport(me, dc.transform.position - dc.transform.right * 2, 0); yield return Wait(.5f); FBDrive.CarAnims = false; bool en = FBDrive.Enter(me, dc); yield return Wait(.5f); Log($"pista de dano: {dc.id} · entrou {en} · dirigindo {FBDrive.Cur == dc}");
                FBPlayer.TestInput = inp => { inp.move = new Vector2(-cSt, cThr); inp.jumpHeld = false; inp.interact = false; inp.multimidia = false; return inp; };
                float vNew = 0;
                foreach (var (nome, kmh, xo) in new[] { ("leve", 20f, 0f), ("moderada", 45f, -1.2f), ("grave", 75f, -1.2f) })
                {
                    if (nome == "grave") { yield return Reta(dc, "depois da leve + moderada"); FBCarDamage.Repair(dc); Log("oficina antes da grave (carro novo para medir a grave sozinha)"); }
                    // muro 40 m à frente, girado 25° (bate primeiro o canto dianteiro esquerdo)
                    wall.SetActive(true); var T = dc.transform; wall.transform.SetPositionAndRotation(T.position + T.forward * 90 + T.right * xo + Vector3.up * 1.5f, Quaternion.LookRotation(T.forward) * Quaternion.Euler(0, xo != 0 ? -25 : 0, 0)); Physics.SyncTransforms();
                    cThr = 1; cSt = 0; float vMax = 0, tt = 0;
                    while (tt < 15 && dc.v * 3 < kmh) { tt += Time.deltaTime; yield return null; }
                    float hitV = dc.v * 3; bool hit = false; for (float w = 0; w < 25 && !hit; w += Time.deltaTime) { cThr = dc.v * 3 < kmh ? .7f : 0; /* r45d: mantém a velocidade até o muro */ if (dc.crashT > 0) hit = true; hitV = Mathf.Max(hitV, dc.v * 3); vMax = Mathf.Max(vMax, dc.v * 3); yield return null; }
                    cThr = 0; yield return Wait(1.2f);
                    cam.enabled = true; Look(dc.transform.position + dc.transform.forward * 4.5f - dc.transform.right * 3 + Vector3.up * 1.8f, dc.transform.position + Vector3.up * .6f, 50); yield return Shot("dano_" + nome); cam.enabled = false;
                    Log($"batida {nome}: a {hitV:0} km/h · bateu {hit} · {FBCarDamage.DebugInfo(dc)} · peças do REB no modelo: capô {FBDoors.HasPart(dc, 32)} mala {FBDoors.HasPart(dc, 16)} portas {FBDoors.HasPart(dc, 15)} · soltas no total {FBDoors.Detached} · {FBDoors.DebugInfo(dc)}");
                    if (FBDoors.GoneCount(dc) > 0) { var deb = FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name.StartsWith("peca_solta_")).ToList(); foreach (var dt in deb) { var rb = dt.GetComponent<Rigidbody>(); Log($"peça solta {dt.name}: a {(dt.position - dc.transform.position).magnitude:0.0} u do carro · {(rb ? (rb.isKinematic ? "parada" : rb.linearVelocity.magnitude.ToString("0.0") + " u/s") : "-")} · altura {dt.position.y - p0.y:0.00} u"); }
                      if (deb.Count > 0) { cam.enabled = true; Look(deb[0].position + Vector3.up * 2.5f + dc.transform.right * 3, deb[0].position, 50); yield return Shot("dano_" + nome + "_peca_solta"); cam.enabled = false; } }
                    // volta para longe do muro
                    cThr = -1; yield return Wait(2.2f); cThr = 0; yield return Wait(1); var T2 = dc.transform; dc.v = 0;
                    T2.position = new Vector3(T2.position.x, p0.y, T2.position.z) - T2.forward * 20; yield return Wait(.5f);
                }
                FBCarDamage.Repair(dc); yield return Wait(.3f);
                // placa do 171 no caminho a 35 km/h: cede, tomba e o carro segue
                { wall.SetActive(false); var lib = Resources.Load<GameObject>("X171/Urbano/Placas"); var src = lib ? lib.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.parent == lib.transform && t.name.StartsWith("PlacasRegulamentacao_Pare")) : null;
                  if (src) { var T = dc.transform; var pl = Instantiate(src.gameObject); pl.name = "x171_PlacasRegulamentacao_Pare"; pl.transform.position = T.position + T.forward * 40 + T.right * -.4f; pl.layer = FBWorld.LMap; var rs = pl.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); var bc = pl.AddComponent<BoxCollider>(); bc.center = pl.transform.InverseTransformPoint(b.center); bc.size = new Vector3(Mathf.Max(.15f, b.size.x), b.size.y, Mathf.Max(.15f, b.size.z)); Physics.SyncTransforms();
                    int k0 = FBFisica.Knocked; cThr = 1; for (float w = 0; w < 10 && dc.v * 3 < 35; w += Time.deltaTime) yield return null; float v0 = dc.v * 3; cThr = .3f; for (float w = 0; w < 4 && FBFisica.Knocked == k0; w += Time.deltaTime) yield return null; float v1 = dc.v * 3; yield return Wait(1.2f);
                    float tilt = Vector3.Angle(pl.transform.up, Vector3.up); cam.enabled = true; Look(pl.transform.position + Vector3.up * 2 + T.right * 4, pl.transform.position, 50); yield return Shot("placa_derrubada"); cam.enabled = false;
                    Log($"placa do 171 a {v0:0} km/h: cedeu {FBFisica.Knocked > k0} · carro de {v0:0} para {v1:0} km/h · placa inclinada {tilt:0}° · física {(pl.GetComponent<Rigidbody>() ? "sim" : "não")}"); cThr = 0; yield return Wait(1); dc.v = 0; T.position = new Vector3(T.position.x, p0.y, T.position.z) - T.forward * 30; yield return Wait(.5f); } }
                FBCarDamage.Repair(dc); yield return Wait(.3f); Log($"oficina: {FBCarDamage.DebugInfo(dc)}");
                FBPlayer.TestInput = null; FBDrive.Exit(me, true); FBDrive.CarAnims = true;
            }
            // ---- 6. r45b: sirene/giroflex/luzes pela rede — o estado do veículo que EU dirijo (FBDrive.VehicleState, o mesmo do FBNet) vira
            //      JSON (FBNet.ToJson), volta (Json.Parse) e é aplicado numa segunda viatura com um motorista "remoto" (FBDrive.ApplyRemote, o mesmo
            //      do FBRemote). Sem servidor: o servidor (server.mjs 'st') repassa a mensagem sem mexer.
            {
                string vid = FBVehicles.PickViatura(() => .1f);
                var p1 = me.Pos + me.Fwd * 12; if (FBWorld.GroundY(p1.x, p1.y + 3, p1.z, .4f, 8) is float g1) p1.y = g1;
                var mine = vid != null ? FBVehicles.Spawn(vid, p1 + Vector3.up * .3f, 0) : null; var rc = vid != null ? FBVehicles.Spawn(vid, p1 + Vector3.right * 6 + Vector3.up * .3f, 0) : null;
                var rid = FBStreetEvents.FreeCivilian(true); var ra = rid != null ? FBStreetEvents.Spawn(rid, p1 + Vector3.right * 9, 0, "netlab") : null;
                if (mine && rc && ra)
                {
                    mine.syncId = "netlab:a"; rc.syncId = "netlab:b"; yield return Wait(1);
                    FBMatch.Teleport(me, mine.transform.position - mine.transform.right * 2, 0); yield return Wait(.4f); FBDrive.CarAnims = false; FBDrive.Enter(me, mine); yield return Wait(.6f);
                    FBWeather.ForceNight(true); yield return Wait(1.5f);
                    string Step(string what)
                    {
                        var st = FBDrive.VehicleState(); if (st == null) return what + ": sem estado";
                        var js = FBNet.ToJson(st); var back = Json.Parse(js) as List<object>; back[0] = rc.syncId; FBDrive.ApplyRemote(ra, back, Time.deltaTime);
                        return $"{what}: bits {(back.Count > 13 ? Json.Fv(back[13]) : -1):0} · remoto sirene {FBSiren.Mode(rc)} giroflex {FBSiren.Lights(rc)} netLights {rc.netLights} · local sirene {FBSiren.Mode(mine)} giroflex {FBSiren.Lights(mine)} · json {js.Length} bytes";
                    }
                    IEnumerator Hold(string what, float s) { string last = null; for (float w = 0; w < s; w += Time.unscaledDeltaTime) { last = Step(what); yield return null; } Log("rede · " + last); }
                    yield return Hold("antes (tudo desligado)", 1);
                    FBSiren.Set(mine, 2); yield return Hold("sirene rápida", 1.5f);
                    { cam.enabled = true; var T = rc.transform; Look(T.position + T.forward * 8 + T.right * 3 + Vector3.up * 2.2f, T.position + Vector3.up * 1f, 55); yield return Shot("rede_sirene_giroflex"); cam.enabled = false; }
                    { int on = 0, n = 0; float lv = 0; var src = rc.GetComponentsInChildren<AudioSource>().FirstOrDefault(a => a.isPlaying && a.clip && a.clip.name.ToLower().Contains("sirene")); for (float w = 0; w < 3; w += Time.unscaledDeltaTime) { Step("x"); n++; if (FBSiren.Lights(rc)) on++; yield return null; } Log($"rede · 3 s com a sirene: remoto ligado {on}/{n} quadros · fonte 3D no remoto {(src ? src.clip.name + " espacial " + src.spatialBlend : "-")} · {FBSiren.Diag(rc)}"); }
                    FBSiren.Set(mine, 3); yield return Hold("troca para wail", 1);
                    FBSiren.Set(mine, 0); FBSiren.SetLights(mine, true); yield return Hold("só giroflex (SHIFT+B)", 1);
                    FBSiren.SetLights(mine, false); { float t0 = Time.unscaledTime, off = -1; for (float w = 0; w < 2; w += Time.unscaledDeltaTime) { Step("x"); if (off < 0 && FBSiren.Mode(rc) == 0 && !FBSiren.Lights(rc)) off = Time.unscaledTime - t0; yield return null; } Log($"rede · desligou no local -> remoto desligou em {off:0.00} s"); }
                    // luzes: pisca-alerta, seta e faróis (lente do remoto pelo FBCarLights) — conta quadros com a lente da seta acesa
                    FBCarLights.TestBlink(2); { int lOn = 0, rOn = 0, n = 0; for (float w = 0; w < 3; w += Time.unscaledDeltaTime) { Step("x"); n++; if (FBCarLights.LensOn(rc, 3, 0) || FBCarLights.LensOn(rc, 3, 1)) lOn++; if (FBCarLights.LensOn(rc, 4, 0) || FBCarLights.LensOn(rc, 4, 1)) rOn++; yield return null; } Log($"rede · pisca-alerta: lente seta esq acesa {lOn}/{n} quadros, dir {rOn}/{n} (piscando ~50%) · bits {FBDrive.LightBits(mine)}"); }
                    { cam.enabled = true; var T = rc.transform; Look(T.position - T.forward * 8 + T.right * 2.5f + Vector3.up * 2f, T.position + Vector3.up * .8f, 55); for (int i = 0; i < 2; i++) { Step("x"); yield return Shot($"rede_pisca_alerta_{i}"); yield return Wait(.36f); } cam.enabled = false; }
                    FBCarLights.TestBlink(-1); { int lOn = 0, rOn = 0, n = 0; for (float w = 0; w < 3; w += Time.unscaledDeltaTime) { Step("x"); n++; if (FBCarLights.LensOn(rc, 3, 0) || FBCarLights.LensOn(rc, 3, 1)) lOn++; if (FBCarLights.LensOn(rc, 4, 0) || FBCarLights.LensOn(rc, 4, 1)) rOn++; yield return null; } Log($"rede · seta esquerda: esq {lOn}/{n}, dir {rOn}/{n}"); }
                    FBCarLights.TestBlink(0); int hm0 = FBCarLights.headMode;
                    FBCarLights.headMode = 1; { int on = 0, n = 0; for (float w = 0; w < 1.5f; w += Time.unscaledDeltaTime) { Step("x"); n++; if (FBCarLights.LensOn(rc, 0, 0) || FBCarLights.LensOn(rc, 0, 1)) on++; yield return null; } Log($"rede · faróis ligados: farol do remoto aceso {on}/{n}"); }
                    FBCarLights.headMode = 2; { int on = 0, n = 0; for (float w = 0; w < 1.5f; w += Time.unscaledDeltaTime) { Step("x"); n++; if (FBCarLights.LensOn(rc, 0, 0) || FBCarLights.LensOn(rc, 0, 1)) on++; yield return null; } Log($"rede · faróis desligados (noite): farol do remoto aceso {on}/{n}"); }
                    FBCarLights.headMode = hm0; FBWeather.ForceNight(false);
                    FBDrive.Release(ra); FBDrive.Exit(me, true); FBDrive.CarAnims = true; Log($"rede · motorista remoto saiu: netLights {rc.netLights}");
                }
                else Log($"rede: não montou (viatura {vid} · remoto {rid})");
            }
            // ---- 7. r45d: NPC entrando/saindo do carro e subindo/descendo da moto (comum e MX) pelo caminho do quadril (FBAnimPaths),
            //      e a queda da moto (FBFall) do jogador e de NPC — prints de lado em 5 tempos de cada transição
            {
                List<object> State(FBCar c) { var w = Coords.ToWeb(c.transform.position); return new List<object> { c.syncId, (double)w[0], (double)w[1], (double)w[2], (double)c.yaw, 0d, 0d, 0d, 0d, 0d, 0d, 0d }; }
                FBCar Put(string vid, Vector3 at, float yaw) { if (FBWorld.GroundY(at.x, at.y + 3, at.z, .4f, 8) is float g) at.y = g; return FBVehicles.Spawn(vid, at + Vector3.up * .3f, yaw); }
                void Kill(FBCar c) { if (!c) return; if (c.mx) c.mx.SetActive(false); FBVehicles.list.Remove(c); Destroy(c.gameObject); }
                string Hip(FBCharacter a, FBCar c) { var h = a.an ? a.an.GetBoneTransform(HumanBodyBones.Hips) : null; if (!h) return "-"; var gy = FBWorld.GroundY(h.position.x, h.position.y, h.position.z, .3f, 3); return $"quadril {(h.position - FBDrive.SeatHips(c)).magnitude:0.00} m do banco, {(h.position - FBDrive.DoorOut(c, a.ScaleK)).Flat().magnitude:0.00} m da porta, {(gy is float g ? h.position.y - g : -1):0.00} m do chão"; }
                IEnumerator Shots(FBCar c, string n, float len, FBCharacter a)
                {
                    var T = c.transform; var from = T.position - T.right * 4.2f + T.forward * .8f + Vector3.up * 1.4f; float t0 = Time.time;
                    for (int i = 0; i < 5; i++) { while (Time.time - t0 < len * (i + .5f) / 5) yield return null; cam.enabled = true; Look(from, T.position + Vector3.up * .8f, 50); yield return Shot($"{n}_{i}"); Log($"  {n} {i}: fase {a.GetComponent<FBDriverPose>()?.trans ?? "-"} · clipe {a.sys ?? "-"} · {Hip(a, c)}"); }
                }
                string carId = FBVehicles.list.Where(c => c && c.kind != "moto" && !c.ie && !c.mx).Select(c => c.id).FirstOrDefault(v => FBRegistry.I.Vehicle(v));
                string motoId = GameData.Vehicles.Keys.FirstOrDefault(v => FBRegistry.I.Vehicle(v) && Json.S(GameData.Vehicles[v], "kind") == "moto" && !v.StartsWith("mx_"));
                string mxId = FBMxBike.IDS.Where(FBMxBike.Has).Select(i => "mx_" + i).FirstOrDefault();
                { if (FBVehicles.CarOf(me)) FBDrive.Exit(me, true); FBMatch.Teleport(me, p0 + Vector3.right * 60, 0); yield return Wait(.5f); Log($"7: jogador no centro da pista · chão {FBWorld.GroundY(me.Pos.x, me.Pos.y + 1, me.Pos.z, .5f, 3)?.ToString("0.00") ?? "NENHUM"} (pista {p0.y:0.00})"); }
                foreach (var (vid, tag) in new[] { (carId, "carro"), (motoId, "moto"), (mxId, "mx") })
                {
                    if (vid == null) { Log($"7 {tag}: sem veículo"); continue; }
                    var vc = Put(vid, me.Pos + me.Fwd * 9, me.Yaw); if (!vc) { Log($"7 {tag}: {vid} não nasceu"); continue; }
                    vc.syncId = "lab7:" + tag; yield return Wait(1.5f);
                    var a = FBStreetEvents.Spawn(FBStreetEvents.FreeCivilian(true), FBDrive.DoorOut(vc, 1), vc.transform.eulerAngles.y + 90, "lab7"); if (!a) { Kill(vc); continue; }
                    yield return Wait(1.2f);
                    FBDrive.ApplyRemote(a, State(vc), 0, true); bool moto = vc.kind == "moto", ok;
                    if (moto) { FBDrive.StartMount(a, vc); ok = a.GetComponent<FBDriverPose>()?.trans == "mount"; } else ok = FBDrive.StartCarIn(a, vc);
                    var dp = a.GetComponent<FBDriverPose>(); Log($"7 {tag} ({vid}) NPC sobe/entra: {ok} · porta {FBDrive.DoorDiag} · caminho {dp?.path} · {FBDrive.PathDiag}");
                    if (ok) yield return Shots(vc, $"npc_{tag}_entra", dp.transLen, a); yield return Wait(1.2f); Log($"  {tag} sentado: {Hip(a, vc)}");
                    bool outDone = false; bool ex = moto ? FBDrive.StartDismount(a, vc) : FBDrive.StartCarOut(a, vc, false);
                    if (dp) dp.onOut = (ch, cr) => { FBDrive.OutPoint(ch, cr, out var at, out var yw); FBDrive.Release(ch); FBMatch.Teleport(ch, at, yw); var c2 = ch.GetComponent<CharacterController>(); if (c2) c2.enabled = true; outDone = true; };
                    Log($"7 {tag} NPC desce/sai: {ex} · porta {FBDrive.DoorDiag} · {FBDrive.PathDiag}");
                    if (ex) yield return Shots(vc, $"npc_{tag}_sai", dp.transLen, a); yield return Wait(.8f);
                    var rel = vc.transform.InverseTransformPoint(a.transform.position); Log($"  {tag} depois de sair: terminou {outDone} · x {rel.x:+0.00;-0.00} z {rel.z:+0.00;-0.00} · no veículo {FBVehicles.CarOf(a) != null}");
                    { cam.enabled = true; var T = vc.transform; Look(T.position - T.right * 4.2f + T.forward * .8f + Vector3.up * 1.4f, T.position + Vector3.up * .8f, 50); yield return Shot($"npc_{tag}_fora"); }
                    FBStreetEvents.Despawn(a); Kill(vc); yield return Wait(.5f);
                }
                // r45e: fumaça do motor (pedido: "a fumaça do motor é muito feia") — parado e andando, vida 35 (cinza clara) e 12 (preta)
                if (carId != null)
                {
                    var sc = Put(carId, me.Pos + me.Fwd * 9, me.Yaw); yield return Wait(1);
                    foreach (var hp in new[] { 35f, 12f })
                    {
                        FBCarDamage.SetHpTest(sc, hp); yield return Wait(2.5f); var T = sc.transform; cam.enabled = true;
                        Look(T.position + T.forward * 5 - T.right * 3.5f + Vector3.up * 2f, T.position + T.forward * .8f + Vector3.up * 1.2f, 50); yield return Shot($"fumaca_motor_{hp:0}");
                        Log($"fumaça do motor: vida {FBCarDamage.Hp(sc):0}");
                    }
                    Kill(sc); yield return Wait(.5f);
                }
                // r45m: nuvem dos pneus atrás do veículo (pedido: "fumaça do motor e do escapamento muito feias" — a do "escapamento" era esta
                //  nuvem, não há partícula de escapamento): carro queimando pneu parado (freio de mão + acelerador) e MX arrancando (desde a r45d
                //  a moto não solta nuvem na arrancada; a nuvem do carro ficou menor e mais leve)
                if (carId != null)
                {
                    var bc = Put(carId, me.Pos + me.Fwd * 9, me.Yaw); yield return Wait(1);
                    if (bc && FBDrive.Enter(me, bc))
                    {
                        var dpc = me.GetComponent<FBDriverPose>(); for (float w = 0; w < 4 && dpc && dpc.trans != null; w += Time.deltaTime) yield return null; yield return Wait(.6f);
                        bool hbk = true; cThr = 0; cSt = 0;
                        FBPlayer.TestInput = inp => { inp.move = new Vector2(-cSt, cThr); inp.jumpHeld = hbk; inp.interact = false; inp.multimidia = false; return inp; };
                        cThr = 1; float burnT = 0; for (float w = 0; w < 2.5f; w += Time.deltaTime) { if (bc.burn) burnT += Time.deltaTime; yield return null; }
                        { var T = bc.transform; cam.enabled = true; Look(T.position - T.forward * 7.5f + T.right * 3.2f + Vector3.up * 1.6f, T.position - T.forward * 1.2f + Vector3.up * .7f, 55); yield return Shot("fumaca_pneu_queimada"); }
                        Log($"nuvem dos pneus · queimada parado: queimando {burnT:0.0} de 2,5 s · {bc.v * 3:0} km/h");
                        cThr = 0; hbk = false; yield return Wait(1.5f); FBPlayer.TestInput = null;
                    }
                    else Log("nuvem dos pneus: não entrou no carro");
                    if (FBVehicles.CarOf(me)) FBDrive.Exit(me, true); Kill(bc); yield return Wait(.5f);
                }
                if (mxId != null)
                {
                    var lb = Put(mxId, me.Pos + me.Fwd * 6, me.Yaw); yield return Wait(1.5f);
                    if (lb && FBDrive.Enter(me, lb))
                    {
                        var dpl = me.GetComponent<FBDriverPose>(); for (float w = 0; w < 4 && dpl && dpl.trans != null; w += Time.deltaTime) yield return null; yield return Wait(.5f);
                        cThr = 0; cSt = 0; FBPlayer.TestInput = inp => { inp.move = new Vector2(-cSt, cThr); inp.jumpHeld = false; inp.interact = false; inp.multimidia = false; return inp; };
                        cThr = 1; yield return Wait(.9f);
                        { var T = lb.mx ? lb.mx.transform : lb.transform; cam.enabled = true; Look(T.position - T.forward * 6.5f + T.right * 3f + Vector3.up * 1.5f, T.position - T.forward * 1f + Vector3.up * .6f, 55); yield return Shot("fumaca_moto_arrancada"); }
                        Log($"nuvem dos pneus · arrancada da MX: {(lb.mx ? lb.mx.speedKmh : 0):0} km/h depois de 0,9 s");
                        cThr = -1; yield return Wait(1.8f); cThr = 0; FBPlayer.TestInput = null;
                    }
                    else Log("nuvem dos pneus: não montou na MX");
                    if (FBVehicles.CarOf(me)) FBDrive.Exit(me, true); Kill(lb); yield return Wait(.5f);
                }
                // queda do jogador: sobe na MX, ganha 40 km/h e bate (o mesmo FBDrive.MxCrash que o FBMxBike chama)
                if (mxId != null)
                {
                    var bike = Put(mxId, me.Pos + me.Fwd * 6, me.Yaw); yield return Wait(1.5f);
                    if (bike && FBDrive.Enter(me, bike))
                    {
                        var dp = me.GetComponent<FBDriverPose>(); Log($"7 queda: montou {bike.id} · caminho {dp?.path} · {FBDrive.PathDiag}");
                        yield return Shots(bike, "jog_mx_monta", dp ? dp.transLen : 1, me); yield return Wait(1.5f);
                        bike.mx.Launch(bike.mx.transform.forward * 40 / 3.6f * FBMxBike.S); yield return Wait(.35f);
                        var pq = me.Pos; FBDrive.MxCrash(bike, "batida"); Log($"7 queda do jogador: {FBFall.Diag} · caindo {FBFall.Busy(me)}");
                        foreach (var at in new[] { .25f, .7f, 1.3f, 2.1f, 3.0f, 4.2f })
                        {
                            float t1 = Time.time; while (Time.time - t1 < (at == .25f ? .25f : .45f)) yield return null;
                            cam.enabled = true; var f = me.transform.forward.Flat().normalized; var r = Vector3.Cross(Vector3.up, f);
                            Look(me.Pos - f * 3.5f + r * 3.2f + Vector3.up * 1.8f, me.Pos + Vector3.up * .6f, 55); yield return Shot($"jog_queda_{at:0.0}".Replace(',', '_'));
                            Log($"  queda t={at:0.0}s: clipe {me.sys ?? "-"} · caindo {FBFall.Busy(me)} · andou {(me.Pos - pq).Flat().magnitude:0.0} m · {Hip(me, bike)}");
                        }
                        yield return Wait(1); Log($"  queda fim: caindo {FBFall.Busy(me)} · no chão {me.GetComponent<CharacterController>()?.isGrounded} · quedas {FBFall.Started}/{FBFall.Finished} · levantou aos {FBFall.LastUp:0.00} s {(FBFall.LastTimeout ? "(pelo limite de 5 s)" : "(assentou)")}");
                    }
                    else Log("7 queda: não montou na MX");
                    if (FBVehicles.CarOf(me)) FBDrive.Exit(me, true); Kill(bike); yield return Wait(.5f);
                    // NPC: os outros três clipes (esquerda/direita/de costas)
                    foreach (var d in new[] { "Left", "Right", "Back" })
                    {
                        var a = FBStreetEvents.Spawn(FBStreetEvents.FreeCivilian(true), me.Pos + me.Fwd * 6, me.Yaw, "lab7f"); if (!a) continue; yield return Wait(1);
                        var q0 = a.Pos; bool b = FBFall.Begin(a, a.Pos, me.Yaw, a.transform.forward * 7, d); Log($"7 queda NPC {d}: {b} · {FBFall.Diag}");
                        for (int i = 0; i < 3; i++) { yield return Wait(.9f); cam.enabled = true; var f = a.transform.forward.Flat().normalized; var r = Vector3.Cross(Vector3.up, f); Look(a.Pos - f * 4 + r * 3 + Vector3.up * 1.8f, a.Pos + Vector3.up * .6f, 55); yield return Shot($"npc_queda_{d}_{i}"); Log($"  NPC {d} {i}: clipe {a.sys ?? "-"} · andou {(a.Pos - q0).Flat().magnitude:0.0} m"); }
                        yield return Wait(1.5f); Log($"  NPC {d} fim: caindo {FBFall.Busy(a)}"); FBStreetEvents.Despawn(a);
                    }
                }
                cam.enabled = false;
            }
            (FBSession.map, FBSession.mode, FBSession.view) = keep; FBSession.Save(); Log("fim"); yield return Wait(1); Application.Quit();
        }
    }
}
