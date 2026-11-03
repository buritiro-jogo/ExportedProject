// RACHA DE MOTO (r45 — pedido: "rachas entre NPCs ... com a física e animações do MX Grau"). Evento de rua: dois motoqueiros NPC numa
// reta longa (>= 230 m de faixa sem curva) perto do jogador. Usa a FÍSICA COMPLETA do MX (FBMxBike: corpo rígido, torque, grau por torque,
// equilíbrio, queda) pilotada por IA, e as poses do piloto do MX (PoseRider/RiderClip) no nosso personagem:
//  · alinhados lado a lado parados -> 3 s acelerando no ponto morto (giro + estouro do MXBikeSound), buzina -> largada;
//  · acelerador cheio; o "exibido" puxa o grau e o segura no ponto de equilíbrio como o piloto de teste (freio traseiro acima de 50°,
//    solta entre 38–50°, acelera abaixo de 26°); o outro só acelera e troca de marcha; esterço = perseguição pura na linha da sua faixa;
//  · chegada a 220 m: freia, para, e os dois vão embora pela faixa (somem quando ninguém está vendo); caiu: piloto desce e levanta;
//  · fora do -fbruas/eventos: entra no rodízio dos eventos de rua do rolê.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public static class FBRacha
    {
        class R { public FBCar car; public FBCharacter rider; public float lat, best, prevA; public bool show, crashed, done; public float wT; }
        static readonly List<R> rs = new(); static FBSeg seg; static float s0, t, life; static string st; public static string Why;
        public static bool Active => rs.Count > 0; public static int Brakes, Crashes, Races, Finishes; /* r45b: contadores do teste em vários mapas */ public static float Cooldown() => Random.Range(200f, 380f);
        public static string State => st; public static float Progress => rs.Count == 0 ? 0 : rs.Max(r => Along(r) - s0);
        public static float MaxWheelie => rs.Count == 0 ? 0 : rs.Max(r => r.best);
        static float Dist => seg != null ? Mathf.Min(220, seg.len - s0 - 25) : 200;                       // distância da corrida
        static void Log(string s) => Debug.Log("[FreeBala][racha] " + s);
        static float Along(R r) => r.car ? FBRoads.Project(seg, r.car.transform.position, out _, out _) : 0;

        public static bool Start()
        {
            Why = null; if (Active) { Why = "já ativo"; return false; } if (!FBRoads.Ready) { Why = "sem malha de ruas"; return false; }
            var ids = FBMxBike.IDS.Where(FBMxBike.Has).Where(i => i != "at1100" && i != "gs1250" && i != "moby").ToList(); if (ids.Count == 0) { Why = "sem motos MX"; return false; }
            var pp = FBStreetEvents.PlayerPos; float near = FBStreetEvents.ForceNear;
            // reta: faixa de 150 m+ cujo rumo muda menos de 8° do começo ao fim (r45c: "240 m sem curva nenhuma" não existe na Vila Jardim)
            float Turn(FBSeg S) { FBRoads.At(S, 5, out var a0); FBRoads.At(S, S.len - 5, out var a1); return Vector3.Angle(a0.Flat(), a1.Flat()); }
            var cand = FBRoads.segs.Where(S => S.kind == 0 && S.len > 150 && !S.ring && Turn(S) < 8).ToList();
            foreach (var S in cand.OrderBy(_ => Random.value))
            {
                float ss = 12; var p = FBRoads.At(S, ss, out var tan); float d = (p - pp).Flat().magnitude;
                if (near <= 0 && (d < 40 || d > 180)) continue;                                        // teste forçado (-fbruas): qualquer reta do mapa
                if (FBTransito.List.Any(c => c.car && (c.pos - p).magnitude < 25)) continue;
                seg = S; s0 = ss; Build(ids, p, tan.Flat().normalized); if (rs.Count < 2) { End(true); continue; }
                st = "alinhando"; t = 0; life = 0; Log($"racha montado a {d:0} m · faixa {S.id} ({S.len:0} m) · {string.Join(" × ", rs.Select(r => r.car.id))}"); return true;
            }
            Why = $"nenhuma reta boa ({cand.Count} retas longas no mapa)"; return false;
        }
        static void Build(List<string> ids, Vector3 p, Vector3 f)
        {
            var right = Vector3.Cross(Vector3.up, f); float w = Mathf.Clamp(seg.w / 2 - .9f, .9f, 1.6f);
            var pool = FBStreetEvents.FreeIds(FBNpcAnims.ByCls("motoboy")).Concat(FBStreetEvents.FreeIds(FBNpcAnims.Roster(FBSession.skin))).Distinct().ToList();
            for (int k = 0; k < 2; k++)
            {
                float lat = k == 0 ? -w : w; var at = p + right * lat; if (FBWorld.GroundY(at.x, at.y + 2, at.z, .3f, 4) is float gy) at.y = gy;
                var car = FBVehicles.Spawn("mx_" + ids[Random.Range(0, ids.Count)], at + Vector3.up * .25f, Quaternion.LookRotation(f).eulerAngles.y); if (!car || !car.mx) continue;
                var id = pool.Count > k ? pool[k] : FBStreetEvents.FreeCivilian(true); var rider = FBStreetEvents.Spawn(id, at - right * 1.2f, 0, "racha" + k); if (!rider) { Kill(car); continue; }
                Mount(rider, car); rs.Add(new R { car = car, rider = rider, lat = lat, show = k == 0 });
            }
        }
        static void Mount(FBCharacter a, FBCar car)
        {
            a.StopDance(true); var cc = a.GetComponent<CharacterController>(); if (cc) cc.enabled = false;
            a.vehiclePose = true; a.sys = FBDrive.SitClip(car); a.aiming = a.shooting = a.sprint = a.crouch = false; a.combat = false; a.SetWeaponVisible(false); a.velocity = Vector3.zero;
            car.driver = a; FBVehicles.SetDriving(a, car); a.gameObject.GetOrAdd<FBDriverPose>().Set(car);
            foreach (var c in a.GetComponentsInChildren<Collider>()) foreach (var bc in car.cols) if (c && bc) Physics.IgnoreCollision(c, bc);
            car.mx.SetActive(false);                                                                 // física liga na largada (parada com física ela virava no lugar)
        }
        static void Kill(FBCar car) { if (!car) return; if (car.mx) car.mx.SetActive(false); FBVehicles.list.Remove(car); Object.Destroy(car.gameObject); }

        public static void Tick(float dt)
        {
            if (!Active) return; life += dt; t += dt;
            foreach (var r in rs)
            {
                if (!r.car || !r.rider) continue; var mx = r.car.mx; if (!mx) continue;
                if (mx.crashed && !r.crashed) { r.crashed = true; Crashes++; Log($"{r.car.id} caiu ({mx.wheelieAngle:0}° de grau a {mx.speedKmh:0} km/h)"); var rel = r.rider; FBFall.Grab(rel, r.car); FBDrive.Release(rel); FBFall.FromBike(rel, r.car, mx.wheelieAngle > 45 ? "loop" : "batida"); }   /* r45d: clipe de queda */
                if (r.crashed) continue;
                float thr = 0, steer = Steer(r), wTar; bool wh = false, rev = false;
                switch (st)
                {
                    case "alinhando": mx.RemoteState(0, 0, t > .6f ? .35f + .35f * Mathf.PerlinNoise(t * 3, r.lat) : 0, 0, dt); if (t > .6f && Random.value < dt * 2.5f) mx.StartBlip(); continue;   // acelerando no ponto morto (som), parada sem física
                    case "largada":
                        thr = 1; float a0 = mx.wheelieAngle, rate = (a0 - r.prevA) / Mathf.Max(dt, 1e-3f); r.prevA = a0; float a = a0 + Mathf.Max(0, rate) * .3f;   // piloto: ângulo previsto 0,3 s à frente (moto leve sobe rápido)
                        if (r.show && ((mx.speedKmh > 35 && mx.speedKmh < 80) || mx.onWheelie) && Along(r) - s0 < Dist - 30) { wh = mx.speedKmh < 80; thr = !wh ? 0 : a > 40 ? -1 : a > 30 ? 0 : a > 18 ? .35f : 1; steer *= .35f; }   // grau só de 35 a 80 km/h; passou disso, solta e a frente desce
                        r.best = Mathf.Max(r.best, mx.onWheelie ? a0 : 0);
                        if (Along(r) - s0 > Dist) r.done = true;
                        if (r.done) { thr = mx.speedKmh > 3 ? -1 : 0; wh = false; }
                        break;
                    case "volta": thr = mx.speedKmh < 35 ? .55f : 0; break;
                }
                // olha 1,2 s à frente: carro/pessoa/objeto no caminho -> freia e solta o grau (racha de rua não é corrida fechada)
                if (st == "largada" && !r.done) { var T = r.car.transform; float look = Mathf.Max(8, mx.speedKmh / 3.6f * 1.2f * FBMxBike.S);
                    foreach (var h in Physics.SphereCastAll(T.position + Vector3.up * .9f, .55f, T.forward.Flat().normalized, look, FBWorld.SolidMask | (1 << FBWorld.LVehicle) | (1 << FBWorld.LChar), QueryTriggerInteraction.Ignore))
                    { if (h.collider.GetComponentInParent<FBCar>() == r.car || h.collider.GetComponentInParent<FBCharacter>() == r.rider || h.normal.y > .6f) continue; thr = -1; wh = false; Brakes++; break; } }
                mx.Input(steer, thr, wh, rev, false, false, false, dt);
            }
            if (st == "alinhando" && t > 3.2f) { st = "largada"; t = 0; foreach (var r in rs) if (r.car) { r.car.mx.RemoteStop(); r.car.mx.SetActive(true); r.car.Horn(); } Log("largada"); }
            else if (st == "largada" && (rs.All(r => r.done || r.crashed) || t > 30)) { st = "volta"; t = 0; Races++; Finishes += rs.Count(r => r.done && !r.crashed); Log($"chegada · grau máx {MaxWheelie:0}° · percorrido {Progress:0} m · quedas {rs.Count(r => r.crashed)}"); }
            else if (st == "volta" && t > 4)
            {   // vão embora: some quando ninguém está vendo (ou depois de 60 s)
                bool seen = rs.Any(r => r.car && FBStreetEvents.Visible(r.car.transform.position)); if (!seen || t > 60) End(false);
            }
            if (life > 120) End(true);
        }
        // perseguição pura: ponto da linha da faixa (com o deslocamento lateral de cada um) 10–16 m à frente
        static float Steer(R r)
        {
            var T = r.car.transform; float s = FBRoads.Project(seg, T.position, out _, out _), look = Mathf.Clamp(r.car.mx.speedKmh * .18f, 8, 18);
            var q = FBRoads.At(seg, s + look, out var tan); var right = Vector3.Cross(Vector3.up, tan.Flat().normalized); q += right * r.lat;
            float ang = Vector3.SignedAngle(T.forward.Flat(), (q - T.position).Flat(), Vector3.up);   // + = alvo à direita
            return Mathf.Clamp(ang / 22f, -1, 1);
        }
        public static void End(bool now)
        {
            foreach (var r in rs) { if (r.rider) { if (FBVehicles.CarOf(r.rider)) FBDrive.Release(r.rider); FBStreetEvents.Despawn(r.rider); } Kill(r.car); }
            rs.Clear(); st = null;
        }
    }
}
