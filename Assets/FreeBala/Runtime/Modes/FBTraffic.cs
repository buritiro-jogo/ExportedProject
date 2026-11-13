// TRÂNSITO DO DE ROLÊ (refeito do zero): carros com motorista NPC andando nas FAIXAS da malha de ruas (FBRoads, gerada da colisão real
// do mapa) — nunca fora do asfalto, nunca por dentro da praça, nunca atravessando objeto ou outro carro:
//  · rota = sequência de segmentos (faixa -> conexão do cruzamento -> faixa ...) sorteada na hora (reto 55 %, direita 25 %, esquerda 20 %,
//    sem ponta sem saída se houver opção) ou por A* até a vaga onde vai estacionar; o carro anda NA faixa (linha reta na reta, curva suave
//    nas conexões) e só sai dela de propósito (ultrapassar, trocar de faixa, manobrar na vaga);
//  · fila com distância segura (modelo IDM: acelera até a velocidade da via e da curva à frente, freia para quem está na frente com
//    folga de 2,2 m + 1,2–1,6 s, conforme o "jeito" de cada motorista);
//  · cruzamento com RESERVA: só entra numa conexão se nenhuma conexão que cruza com ela estiver ocupada/reservada; quem chega antes passa,
//    reto tem preferência sobre virar; não entra se não couber do outro lado (não "fecha o cruzamento"); trava longa = desempate;
//  · sensores para o que não é do trânsito (pedestre, jogador a pé, carro do jogador, carro parado na faixa): para, buzina, e se o
//    obstáculo continuar parado ultrapassa pela faixa da esquerda (pista de mão única) ou pela contramão livre (mão dupla);
//  · setas antes de virar / ao estacionar e sair da vaga, luz de freio (também parado), ré, pisca-alerta quando travado ou batido;
//  · estaciona em vaga livre (entra devagar, de frente; nas inclinadas sai de ré), o motorista desce e vira pedestre; pedestre pega carro
//    estacionado, dá a partida e sai da vaga esperando a faixa ficar livre; ponta sem saída: manobra de três pontos;
//  · nasce longe e fora da vista e é reaproveitado (teleporte) em vez de instanciar no meio do rolê;
//  · CARROS ESPECIAIS (rodada 39, FBStreetEvents): carro do açaí e viatura saindo da abordagem andam neste mesmo trânsito com teto de
//    velocidade, sem estacionar, rota puxada para perto do jogador (açaí) e fim próprio (onDrop); nos modos de combate só eles rodam.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public static class FBTransito
    {
        public class Car
        {
            public int id; public FBCar car; public FBCharacter actor, walker; public string mode = "drive"; public float life, modeT;
            public readonly List<int> route = new(); public int ri; public float s;                    // segmento route[ri], metros nele
            public float v, acc, lat, latT, cruise = 11, T = 1.4f, amax = 2.2f, aggr = .5f;
            public float halfL = 2.3f, halfW = .95f;
            public float yaw, pitch, roll, steer, vPrev, aAcc, sensT, gapObs = 1e9f, vObs, obsStill, stuck, honkT, waitT, shockT, passUntil;
            public object obsWho; public int overSide;
            public FBSpot spot; public bool wantPark; public int blink; public bool brake, reverse, hazard;
            public Vector3[] mp; public float[] ms; public float mlen, mpos; public int mstage; public bool mrev;  // manobra (vaga / três pontos)
            public Vector3 pos, fwd = Vector3.forward; public int segNow = -1; public float eta; public int nextConn = -1;
            public FBCarSound.Gearbox gb; public bool audio; public int overSeg = -1; public float squealT;
            public float whA, whT, whHold, whPeak; public int whSt;
            public bool phys, physIgn; public float prevA;                                             // r45b: moto com a física do MX ligada
            public bool patrol, sigHeld; public float callT, callLeft;                                         // r45: viatura de patrulha / em ocorrência                             // r45: moto MX — grau (0 nada, 1 sobe, 2 segura, 3 desce) e inclinação
            public readonly List<(Vector3[] p, bool rev)> legs = new(); public float kWait;              // meia-volta em N pontos (frente/ré alternadas)
            public string tag; public float vCap = 99, maxLife = 1e9f; public bool lure, leave; public System.Action<Car> onDrop;   // especial (FBStreetEvents)
            public Vector3? fleeFrom;                                                                     // fugindo de tiro (FBTransito.Flee)
            public object lastWho; public float lastGap;                                                   // r45j: o que estava na frente no último passo (diagnóstico)
            public FBCharacter garupa; public bool garupaDown;                                            // r45d: passageira na moto
            public float yieldT, pushT; public bool yielded; public string leadWho; public static int Pushes; public static int Yields;   // r45e: carro que abre caminho para a sirene
            public Car chase, fugitive; public bool runLights; public float chaseT, closeT; public bool caught;   // r45d: perseguição (viatura -> fugitivo)
        }
        static readonly List<Car> cars = new(); static int serial; static float enterCd = 25, spawnCd; static int target = 10;
        // r68: o trânsito roda em cada PC; "npc:car:3" existia em TODOS os jogadores (carros diferentes) e o estado de um jogador dirigindo
        // o dele movia o carro de mesmo id dos outros. Agora o id leva o código do jogador: só bate com a cópia (rcar) que o dono mandou.
        static string Sid(string tag) => "npc:" + FBLobbyNet.Pid + ":" + tag + ":" + (++serial);
        public static bool Running => FBRoads.Ready && occ.Length == FBRoads.segs.Count;
        static List<Car>[] occ = new List<Car>[0];
        static readonly Dictionary<int, HashSet<Car>> res = new();                                          // conexão -> quem reservou
        static readonly string[] EXCL = { "S10_2017", "RAM3500", "F250", "Lambo", "McLaren", "Porsche", "Camaro", "Mustang", "AudioS8", "Bmw", "Macan", "Evoque", "RangerRover", "Viatura" };
        public static IReadOnlyList<Car> List => cars; public static int OutsDone, InsDone;
        public static void TestParking() { foreach (var c in cars) if (c.tag == null && !c.patrol && !(c.car && c.car.mx) && c.mode == "drive") { c.wantPark = true; c.life = 99; } enterCd = 0; }   // laboratório: estacionar/descer e pegar carro já
        static System.Collections.IEnumerator After(float t, System.Action a) { if (t > 0) yield return new WaitForSeconds(t); a(); }
        static FBSeg Seg(int i) => FBRoads.segs[i];
        static Vector3 PlayerPos => FBMatch.Player ? FBMatch.Player.Pos : Vector3.zero;
        static Vector3 CamPos => Camera.main ? Camera.main.transform.position : PlayerPos;

        // ---- compatibilidade com o rolê (FBRole)
        public static void BuildRoads() { FBRoads.Load(FBSession.map); }
        public static bool OnRoad(Vector3 p, FBCar skip = null) => FBRoads.IsRoad(p);
        // alvo de pedestre: ponto da navmesh fora do asfalto (andam na calçada; atravessam a rua só quando precisam)
        public static Vector3? SidewalkPoint(Vector3? near, float r)
        {
            for (int k = 0; k < 10; k++) { var p = r > 0 && near != null ? FBNav.RandomPoint(near, r) : FBNav.RandomPoint(); if (p == null) return null; if (!FBRoads.Ready || !FBRoads.IsRoad(p.Value)) return p; if (k == 9) return p; }
            return null;
        }
        // perigo para quem vai atravessar: algum carro do trânsito vai passar a menos de 'r' m de p nos próximos 't' s?
        public static bool Danger(Vector3 p, float r, float t)
        {
            foreach (var c in cars)
            {
                if (!c.car || c.mode == "walk" || c.mode == "gone") continue; var d = p - c.pos; d.y = 0; float dist = d.magnitude; if (dist > 60) continue;
                if (dist < r + c.halfL) return true;
                float along = Vector3.Dot(d, c.fwd), latd = Mathf.Abs(d.x * c.fwd.z - d.z * c.fwd.x);
                if (along > 0 && latd < r + c.halfW && along - c.halfL < Mathf.Max(0, c.v) * t + 2) return true;
            }
            return false;
        }

        public static void Clear()
        {
            foreach (var c in cars) { StopAudio(c); if (c.garupa && !c.garupaDown) Object.Destroy(c.garupa.gameObject); if (c.actor) { FBDrive.Release(c.actor); if (c.mode != "gone") Object.Destroy(c.actor.gameObject); } if (c.spot != null && c.spot.res == c) c.spot.res = null; }
            cars.Clear(); res.Clear(); FBSemaforo.Clear();
        }
        public static void Start(int n)
        {
            Clear(); if (!FBRoads.Ready) { Debug.Log("[FreeBala][trânsito] mapa sem malha de faixas — sem trânsito"); return; }
            occ = new List<Car>[FBRoads.segs.Count]; for (int i = 0; i < occ.Length; i++) occ[i] = new List<Car>();
            // carros parados nas vagas do mapa ocupam a vaga
            SpotsFromParked();
            foreach (var sp in FBRoads.spots) { sp.car = null; sp.res = null; foreach (var car in FBVehicles.list) if (car && Vector3.Distance(car.transform.position, sp.p) < 2.2f) { sp.car = car; break; } }
            FBSemaforo.Build();
            target = FBQuality.Mobile ? Mathf.Min(n, 6) : Mathf.Max(n, 11);
            float km = FBRoads.segs.Where(s => s.kind == 0).Sum(s => s.len) / 1000f; target = Mathf.Clamp(Mathf.RoundToInt(km * 2.6f), 4, target);
            for (int i = 0; i < target * 3 && cars.Count(c => c.mode != "gone") < target; i++) SpawnDriving(true);
            Debug.Log($"[FreeBala][trânsito] {cars.Count} carros em {km:0.0} km de faixas, {FBRoads.spots.Count} vagas");
        }

        // modos de combate: a malha de faixas sem o trânsito comum — só os carros especiais dos eventos de rua (FBStreetEvents)
        public static void StartSpecialOnly()
        {
            Clear(); if (!FBRoads.Ready) return; occ = new List<Car>[FBRoads.segs.Count]; for (int i = 0; i < occ.Length; i++) occ[i] = new List<Car>(); target = 0; FBSemaforo.Build();
            SpotsFromParked();
            foreach (var sp in FBRoads.spots) { sp.car = null; sp.res = null; foreach (var car in FBVehicles.list) if (car && Vector3.Distance(car.transform.position, sp.p) < 2.2f) { sp.car = car; break; } }
        }
        // carro especial: entra numa faixa longe e fora da vista (ou no ponto dado) com o motorista dado; anda no trânsito até onDrop
        public static Car SpawnSpecial(FBCar car, FBCharacter driver, string tag, float vCap, int seg = -1, float s = 0)
        {
            if (!car || !driver || !Running) return null;
            if (seg < 0 && !LanePoint(out seg, out s, false)) return null;
            var S = Seg(seg); var p = FBRoads.At(S, s, out var tan); var f = tan.Flat().sqrMagnitude > 1e-6f ? tan.Flat().normalized : Vector3.forward;
            car.syncId ??= Sid(tag); car.aiLights = true;
            var c = New(car); c.actor = driver; c.tag = tag; c.vCap = vCap; c.wantPark = false; c.route.Add(seg); c.s = s; c.v = Mathf.Min(vCap, 3); Extend(c);
            c.pos = p; c.fwd = f; c.yaw = Mathf.Atan2(-f.x, f.z); car.transform.SetPositionAndRotation(p, FBDrive.CarRot(0, c.yaw, 0)); car.yaw = c.yaw;
            cars.Add(c); Seat(c); Pose(c, 0); return c;
        }
        // carro parado na guia (viatura da abordagem) volta para o trânsito pela manobra de SAÍDA DE VAGA: espera a faixa ficar livre,
        // seta para a esquerda e sai numa curva suave até a faixa (vaga "de mentira" criada aqui, fora da lista do mapa)
        public static Car LeaveCurb(FBCar car, FBCharacter driver, string tag, float vCap)
        {
            if (!car || !driver || !Running) return null;
            var p = car.transform.position; var f = car.transform.forward.Flat().normalized;
            var S = FBRoads.NearestLane(p, 9, out float s, out _, f); if (S == null) return null;
            float sout = s + 9; if (sout > S.len - 1.5f) { if (S.len - s < 4) return null; sout = S.len - 1.5f; }
            var q = FBRoads.At(S, sout, out var tq); var path = Bez(p, f, q, tq.Flat().normalized);
            var sp = new FBSpot { p = p, yaw = -car.yaw * Mathf.Rad2Deg, kind = "guia", seg = S.id, s = s, sin = s, sout = sout, inp = new Vector3[0], outp = path, rev = null };
            car.syncId ??= Sid(tag); car.aiLights = true;
            var c = New(car); c.actor = driver; c.tag = tag; c.vCap = vCap; c.wantPark = false; c.spot = sp; c.mode = "out_wait"; c.modeT = 1.2f + Random.value;
            c.yaw = car.yaw; c.pos = p; c.fwd = f; cars.Add(c); Seat(c); return c;
        }
        // ponto de faixa livre longe e fora da vista (os eventos usam para decidir onde o carro entra)
        public static bool FarLanePoint(out int seg, out float s) => LanePoint(out seg, out s, false);
        // tira o especial do trânsito sem destruir nada (o dono cuida do carro e do motorista)
        // especial que já fez a visita (carro do açaí deu a volta): para de puxar para o jogador, refaz a rota à frente apontando para longe
        // e sai de cena assim que estiver longe e fora da vista (Recycle)
        public static void Leave(Car c)
        {
            if (c == null || c.leave) return; c.lure = false; c.leave = true; c.maxLife = c.life;
            int keep = c.ri + 2; if (c.route.Count > keep) c.route.RemoveRange(keep, c.route.Count - keep);
        }
        public static bool Visible(Vector3 p) => InView(p);
        // FUGA (rodada 42, usuário: "quando receber disparos, o motorista deve entrar em estado de fuga, acelerar significativamente e tentar
        // escapar ... aceleração progressiva, curvas adequadas, desvio de obstáculos"): continua sendo o MESMO motorista do trânsito — IDM
        // (fila, cruzamento reservado, freia antes da curva pelo raio da via, sensores de obstáculo) — só com outro temperamento: sem teto de
        // velocidade do especial, cruzeiro 30 % acima da via, aceleração de 3,6 m/s² (subida progressiva pela IDM, não salto), distância de
        // segurança menor; as viradas passam a apontar para LONGE do tiro. Um susto de 0,4–0,7 s antes de pisar (tempo de reação).
        public static void Flee(Car c, Vector3 from)
        {
            if (c == null || c.mode == "gone") return;
            bool first = c.fleeFrom == null; c.fleeFrom = from; c.lure = false; c.leave = true; c.maxLife = c.life;
            c.vCap = 99; c.cruise = 1.3f; c.amax = 3.6f; c.T = .95f; c.aggr = 1; c.wantPark = false;
            if (first) { c.shockT = Mathf.Max(c.shockT, .4f + Random.value * .3f); if (c.car) c.car.Horn(); }
            int keep = c.ri + 1; if (c.route.Count > keep) c.route.RemoveRange(keep, c.route.Count - keep);   // refaz as próximas viradas já fugindo
        }
        public static void Forget(Car c) { if (c == null) return; StopAudio(c); ReleaseRes(c); if (c.spot != null && c.spot.res == c) c.spot.res = null; c.onDrop = null; c.mode = "gone"; cars.Remove(c); }

        // ---- nascer / reaproveitar
        static List<string> carIds;
        static bool LanePoint(out int seg, out float s, bool anywhere)
        {
            seg = -1; s = 0; var cam = CamPos; var lanes = FBRoads.segs.Where(x => x.kind == 0 && x.len > 18 && !x.ring).ToList(); if (lanes.Count == 0) return false;
            var cm = Camera.main; var planes = cm ? GeometryUtility.CalculateFrustumPlanes(cm) : null;
            for (int k = 0; k < 60; k++)
            {
                var S = lanes[Random.Range(0, lanes.Count)]; float ss = Random.Range(6f, S.len - 6f); var p = FBRoads.At(S, ss, out _);
                float d = Vector3.Distance(p, cam);
                if (!anywhere && (d < 70 || d > 230)) continue;
                if (anywhere && d < 35) continue;
                if (!anywhere && planes != null && d < 160 && GeometryUtility.TestPlanesAABB(planes, new Bounds(p + Vector3.up, new Vector3(5, 3, 5)))) continue;   // não nasce na frente da câmera
                bool free = true; foreach (var c in cars) if (c.car && c.mode != "gone" && Vector3.Distance(c.pos, p) < 22) { free = false; break; }
                foreach (var car in FBVehicles.list) if (car && Vector3.Distance(car.transform.position, p) < 9) { free = false; break; }
                if (!free) continue; seg = S.id; s = ss; return true;
            }
            return false;
        }
        // r45d: a malha da Vila Jardim veio SEM vagas (0) — sem elas o trânsito nunca estaciona/desce e pedestre nunca pega carro. Cada carro
        // estacionado do nível que está junto a uma faixa, no mesmo sentido dela, vira uma vaga com entrada e saída em curva até a faixa
        // (as mesmas que o gerador fazia), então a rotina inteira roda: estaciona, abre a porta, desce pelo clipe, anda; pedestre volta, entra
        // pelo clipe, liga e sai da vaga esperando a faixa ficar livre.
        public static int SpotsMade;
        static void SpotsFromParked()
        {
            if (FBRoads.spots.Count > 0 || !FBRoads.Ready) return;
            foreach (var car in FBVehicles.list)
            {
                if (!car || car.driver || car.kind == "moto" || car.ie) continue; var p = car.transform.position; var f = car.transform.forward.Flat().normalized;
                var S = FBRoads.NearestLane(p, 9, out float s, out float lat, f); if (S == null || s < 14 || s > S.len - 14) continue;
                float sin = s - 10, sout = s + 9; var a = FBRoads.At(S, sin, out var ta); var b = FBRoads.At(S, sout, out var tb);
                var sp = new FBSpot { p = p, yaw = -car.yaw * Mathf.Rad2Deg, kind = "guia", seg = S.id, s = s, sin = sin, sout = sout, inp = Bez(a, ta.Flat().normalized, p, f), outp = Bez(p, f, b, tb.Flat().normalized), rev = null };
                FBRoads.spots.Add(sp); SpotsMade++;
            }
            // sem carro junto à faixa (Vila Jardim: os estacionados ficam nos pátios dos condomínios) — vagas de rua na beira da faixa, à direita,
            // paralelas, uma a cada ~80 m de faixa reta e larga, onde a beira está livre (sem poste/árvore/objeto)
            if (SpotsMade == 0)
                foreach (var S in FBRoads.segs.Where(x => x.kind == 0 && !x.ring && x.len > 60 && x.w >= 5))
                    for (float s = 25; s < S.len - 25; s += 80)
                    {
                        var c = FBRoads.At(S, s, out var t); t = t.Flat().normalized; var r = new Vector3(t.z, 0, -t.x); var p = c + r * (S.w / 2 - .95f);
                        if (Physics.CheckBox(p + Vector3.up * 1.0f, new Vector3(1.0f, .6f, 2.4f), Quaternion.LookRotation(t), (1 << FBWorld.LMap) | (1 << FBWorld.LVehicle), QueryTriggerInteraction.Ignore)) continue;
                        if (FBWorld.GroundY(p.x, p.y + 1.5f, p.z, .3f, 3) is float gy) p.y = gy; else continue;
                        float sin = s - 11, sout = s + 9; var a2 = FBRoads.At(S, sin, out var ta); var b2 = FBRoads.At(S, sout, out var tb);
                        FBRoads.spots.Add(new FBSpot { p = p, yaw = -Mathf.Atan2(-t.x, t.z) * Mathf.Rad2Deg, kind = "rua", seg = S.id, s = s, sin = sin, sout = sout, inp = Bez(a2, ta.Flat().normalized, p, t), outp = Bez(p, t, b2, tb.Flat().normalized), rev = null }); SpotsMade++;
                    }
            Debug.Log($"[FreeBala][trânsito] vagas geradas: {SpotsMade}");
        }
        static Car SpawnDriving(bool anywhere)
        {
            carIds ??= GameData.Vehicles.Keys.Where(k => !Json.B(GameData.Vehicles[k], "viatura") && FBRegistry.I.Vehicle(k) && !EXCL.Any(x => k.Contains(x))).ToList();
            if (carIds.Count == 0 || !LanePoint(out int seg, out float s, anywhere)) return null;
            var S = Seg(seg); var p = FBRoads.At(S, s, out var tan);
            float yawS = Quaternion.LookRotation(tan.Flat().sqrMagnitude > 1e-6f ? tan.Flat() : Vector3.forward).eulerAngles.y;
            var mxIds = FBMxBike.IDS.Where(FBMxBike.Has).ToList(); bool moto = mxIds.Count > 0 && Random.value < MotoShare && cars.Count(o => o.car && o.car.mx) < 4;
            var car = moto ? FBVehicles.Spawn("mx_" + FBUtil.Pick(mxIds), p, yawS) : FBVehicles.Make(FBUtil.Pick(carIds), p, yawS, true, null, Random.value < .6f); if (!car) return null;
            var actor = Actor(p); if (!actor) { FBVehicles.list.Remove(car); Object.Destroy(car.gameObject); return null; }
            car.syncId = Sid("car"); car.aiLights = true;
            var c = New(car); c.actor = actor; c.route.Add(seg); c.s = s; c.v = Random.Range(4f, 9f); Extend(c);
            c.pos = p; c.fwd = tan.Flat().normalized; c.yaw = Mathf.Atan2(-c.fwd.x, c.fwd.z);
            cars.Add(c); Seat(c); Pose(c, 0); if (moto && Random.value < GarupaShare) AddGarupa(c, p); return c;
        }
        // r45d: garupa — passageira do pacote que ainda não está em cena (as do jogo original andam de garupa nas motos)
        public static float GarupaShare = .4f; public static int GarupaSpawned, GarupaDesceu, GarupaSemSkin;
        // r45k: teste — o sorteio do começo pode vir sem nenhuma moto (12 carros × 18 %); põe n motos do jeito normal (fora da vista, 70–230 m)
        public static int TestMotos(int n) { float s0 = MotoShare; MotoShare = 1; int k = 0; for (int i = 0; i < n * 4 && k < n; i++) { var c = SpawnDriving(false); if (c != null && c.car && c.car.mx) k++; } MotoShare = s0; return k; }
        public static bool ForceGarupa(Car c) { if (c == null || c.garupa || !c.car || !c.car.mx) return false; AddGarupa(c, c.pos); return c.garupa; }   // r45j: teste
        // r45j: diagnóstico — o que está parando este carro e, se for outro carro do trânsito, o que para aquele (até 4 elos)
        public static string Chain(Car c)
        {
            var sb = new System.Text.StringBuilder(); var seen = new HashSet<Car>();
            for (int i = 0; i < 4 && c != null && seen.Add(c); i++)
            {
                sb.Append($"{(i > 0 ? " <- " : "")}{c.car?.id} {c.mode} {c.v * 3.6f:0} km/h preso {c.stuck:0} s espera {c.waitT:0} s{(c.hazard ? " alerta" : "")}{(c.overSide != 0 ? " ultrapassando" : "")} obsParado {c.obsStill:0.0} frente: ");
                Car nx = null;
                switch (c.lastWho)
                {
                    case Car oc: sb.Append($"trânsito a {c.lastGap:0} u"); nx = oc; break;
                    case FBCar fc: sb.Append($"veículo {fc.id} {(fc.driver ? (fc.driver.isPlayer ? "do JOGADOR" : "com motorista") : "parado")} a {c.lastGap:0} u"); break;
                    case FBCharacter ch: sb.Append($"{(ch.isPlayer ? "JOGADOR" : "pedestre " + ch.skinId)} a {c.lastGap:0} u"); break;
                    case string s: sb.Append($"{s} a {c.lastGap:0} u"); break;
                    case null: sb.Append("livre"); break;
                    default: sb.Append($"{c.lastWho.GetType().Name} a {c.lastGap:0} u"); break;
                }
                c = nx;
            }
            return sb.ToString();
        }
        // r45e: o piloto desceu (estacionou/caiu fora do trânsito): a garupa desce também e vira pedestre do rolê
        public static void GarupaDesce(FBCharacter g)
        {
            foreach (var c in cars) if (c.garupa == g) { c.garupa = null; c.garupaDown = false; }
            var rg = g.GetComponent<FBRoleNpc>(); if (rg) { rg.enabled = true; rg.st = "walk"; rg.goalP = null; } if (!FBRole.npcs.Contains(g)) FBRole.npcs.Add(g); GarupaDesceu++;
        }
        static void AddGarupa(Car c, Vector3 p)
        {
            var fem = FBNpcAnims.Roster(FBSession.skin).Where(id => GameData.Skins.Exists(s => s.id == id && s.sex == "f")).ToList();
            var ids = fem.Where(id => !FBMatch.All.Any(a => a && !a.isPlayer && a.skinId == id)).ToList();
            if (ids.Count == 0) ids = fem.Where(id => !FBMatch.All.Any(a => a && !a.isPlayer && a.skinId == id && (a.Pos - p).sqrMagnitude < 150 * 150)).ToList();   // r45j: todas já em cena: repete uma que esteja longe (sem clone à vista)
            if (ids.Count == 0 || FBGarupa.Clip == null) { GarupaSemSkin++; return; }
            var g = FBRole.SpawnNpc(FBUtil.Pick(ids), p, "tg" + serial); if (!g) return; FBRole.npcs.Remove(g); var rn = g.GetComponent<FBRoleNpc>(); if (rn) rn.enabled = false;
            if (FBGarupa.Sit(g, c.car, true)) { c.garupa = g; GarupaSpawned++; } else Object.Destroy(g.gameObject);
        }
        public static float MotoShare = .18f; static float patrolCd = 30; public static int PatrolCalls;
        // ---- r45d: PERSEGUIÇÃO (pedido: "viaturas perseguindo motoristas de motos e carros"). Um carro ou moto do trânsito perto do
        //  jogador (40–170 m) vira FUGITIVO: acelera (cruzeiro +60 %, arranca forte, cola na frente), fura o vermelho e escolhe as viradas para
        //  longe da viatura. Uma S10 da PM (a patrulha mais perto, ou uma nova nascendo atrás dele fora da vista) liga sirene rápida e
        //  giroflex e segue A MESMA ROTA do fugitivo pela malha de ruas (ChaseExtend), colada (IDM com folga curta). Viatura a menos de
        //  12 m por 7 s com os dois devagar = pegou: o fugitivo encosta com pisca-alerta, a viatura para atrás; 8 s depois cada um segue.
        //  Fim também por tempo (75 s), distância do jogador (> 280 m) ou o fugitivo sumir/cair.
        public static int ChaseStarts, ChaseCaught, ChaseEnds; public static float ChaseGapSum, ChaseGapN, ChaseVmax; public static string ChaseWhy; static float chaseCd = 90;
        public static Car ChaseCop => cars.FirstOrDefault(c => c.fugitive != null && c.mode != "gone");
        public static bool StartChase(bool force)
        {
            ChaseWhy = null; if (!Running) { ChaseWhy = "sem trânsito"; return false; } if (ChaseCop != null) { ChaseWhy = "já tem perseguição"; return false; }
            var pp = PlayerPos;
            var f = cars.Where(c => c.mode == "drive" && c.car && c.actor && !c.patrol && c.tag == null && c.chase == null && !c.leave && c.spot == null && !c.phys && c.ri < c.route.Count && Seg(c.route[c.ri]).kind == 0 && !DeadLane(Seg(c.route[c.ri])) && c.v > 3 && c.stuck < 1)   /* r45e: fugitivo andando (antes pegava um parado e já 'pegou' na largada) */
                        .Where(c => { float d = (c.pos - pp).Flat().magnitude; return d > (force ? 15 : 40) && d < (force ? 260 : 170); })
                        .OrderBy(c => Mathf.Abs((c.pos - pp).Flat().magnitude - 90)).FirstOrDefault();
            if (f == null) { ChaseWhy = "nenhum carro/moto bom para fugir"; return false; }
            // viatura: patrulha livre a até 220 m (a rota dela vira a do fugitivo) ou uma nova 28 m atrás dele na mesma faixa
            var cop = cars.Where(c => c.patrol && c.fugitive == null && c.mode == "drive" && c.car && c.callLeft <= 0 && c.stuck < 1 && c.obsStill < 1)   /* r45r: travada não serve (ficou 40 s parada atrás de uma moto presa) */.OrderBy(c => (c.pos - f.pos).sqrMagnitude).FirstOrDefault(c => (c.pos - f.pos).magnitude < 220 && (c.pos - f.pos).magnitude > 22 && !NeedsU(c, f));
            if (cop == null)
            {
                int seg = f.route[f.ri]; float s = f.s - 28;
                if (s < 4 && f.ri > 0) { int pk = f.route[f.ri - 1]; s = Seg(pk).len + s; seg = pk; }
                if (s < 4) { ChaseWhy = "sem espaço atrás do fugitivo"; return false; }
                /* r45q: o fugitivo tinha acabado de fazer meia-volta e o segmento de trás era a própria ligação de meia-volta: a viatura nascia
                   nela, a primeira coisa que fazia era a manobra, não cabia e era reciclada no 1º quadro (ficava de pisca-alerta à vista) */
                if (Seg(seg).kind == 1 && Seg(seg).turn == 'U') { ChaseWhy = "fugitivo acabou de fazer meia-volta"; return false; }
                var at = FBRoads.At(Seg(seg), s, out _);
                if (!force && FBStreetEvents.Visible(at) && (at - pp).magnitude < 90) { ChaseWhy = "nasceria na vista do jogador"; return false; }
                cop = SpawnPatrol(seg, s); if (cop == null) { ChaseWhy = "viatura não nasceu"; return false; }
                cop.v = Mathf.Max(6, f.v);
            }
            cop.fugitive = f; f.chase = cop; cop.chaseT = f.chaseT = 0; cop.closeT = 0; f.caught = false; cop.runLights = f.runLights = true;
            f.cruise = 1.6f; f.amax = 4.2f; f.T = .7f; f.aggr = 1; f.vCap = 99; f.wantPark = false; f.fleeFrom = cop.pos; f.maxLife = 1e9f;
            cop.cruise = 1.62f; cop.amax = 4.4f; cop.T = .55f; cop.wantPark = false; cop.callLeft = 0;
            ReplanFlee(f);
            /* r45t: rota replanejada ainda com meia-volta = beco sem outra saída: o fugitivo manobrava 3 s depois, não cabia e era reciclado
               ('fugitivo reciclado'). Desfaz sem contar como perseguição (o teste/sorteio tenta outro) */
            for (int i = f.ri; i < f.route.Count; i++) if (Seg(f.route[i]).kind == 1 && Seg(f.route[i]).turn == 'U') { EndChase(cop, null, true); ChaseWhy = "fugitivo só sairia com meia-volta"; return false; }
            // rota da viatura até o fugitivo e depois a dele
            int cs = cop.route[cop.ri], fs = f.route[f.ri];
            if (cs != fs && !f.route.Skip(f.ri).Contains(cs)) { var r = FBRoads.Route(cs, fs); if (r == null) { f.chase = null; cop.fugitive = null; ChaseWhy = "sem caminho até o fugitivo"; return false; } cop.route.RemoveRange(cop.ri + 1, cop.route.Count - cop.ri - 1); for (int i = 1; i < r.Count; i++) cop.route.Add(r[i]); }
            else cop.route.RemoveRange(cop.ri + 1, cop.route.Count - cop.ri - 1);
            ChaseExtend(cop);
            FBSiren.Set(cop.car, 2, 1); FBSiren.SetLights(cop.car, true); ChaseStarts++;
            Debug.Log($"[FreeBala][trânsito] PERSEGUIÇÃO: viatura {cop.car.id} atrás de {f.car.id} ({(f.car.kind == "moto" ? "moto" : "carro")}) a {(f.pos - pp).Flat().magnitude:0} m do jogador · distância viatura {(cop.pos - f.pos).magnitude:0} m");
            return true;
        }
        // rota da viatura: emenda o que falta da rota do fugitivo depois do último segmento em comum
        static bool ChaseExtend(Car c)
        {
            var f = c.fugitive; if (f == null || f.mode == "gone" || f.route.Count == 0) return false;
            int last = c.route[^1], j = f.route.LastIndexOf(last);
            if (j < 0)
            {   // ainda não alcançou a rota: caminho até onde o fugitivo está
                var r = FBRoads.Route(last, f.route[Mathf.Min(f.ri, f.route.Count - 1)]); if (r == null) return false;
                for (int i = 1; i < r.Count; i++) c.route.Add(r[i]); j = f.route.LastIndexOf(c.route[^1]); if (j < 0) return true;
            }
            for (int i = j + 1; i < f.route.Count; i++) c.route.Add(f.route[i]);
            if (c.ri > 6) { int cut = c.ri - 2; c.route.RemoveRange(0, cut); c.ri -= cut; }
            return true;
        }
        // r45k: a rota da patrulha até o fugitivo pede meia-volta (vinha na contramão): a meia-volta numa rua estreita não cabia e a viatura era
        //  reciclada 3 s depois de ligar a sirene — nesse caso nasce uma viatura nova atrás do fugitivo (fora da vista)
        //  r45q: confere a rota que o StartChase monta de verdade — do segmento ATUAL da viatura até o do fugitivo (antes conferia a partir do fim
        //  da rota planejada, que o StartChase descarta) — e recusa viatura NA FRENTE do fugitivo (mesma faixa mais adiante ou na rota dele):
        //  ela não persegue, vira bloqueio e o fugitivo 'é pego' parado atrás dela
        static bool NeedsU(Car cop, Car f)
        {
            if (cop.route.Count == 0 || f.route.Count == 0 || cop.ri >= cop.route.Count) return true;
            int cs = cop.route[cop.ri], fs = f.route[Mathf.Min(f.ri, f.route.Count - 1)];
            if (Seg(cs).kind == 1 && Seg(cs).turn == 'U') return true;
            if (cs == fs) return cop.s > f.s;
            if (f.route.Skip(f.ri).Contains(cs)) return true;
            var r = FBRoads.Route(cs, fs); if (r == null) return true;
            foreach (var i in r) if (Seg(i).kind == 1 && Seg(i).turn == 'U') return true;
            return false;
        }
        public static int ChaseKTurns;
        // r45q: a rota do fugitivo já vinha planejada 180 u à frente com os pesos normais (meia-volta, beco): a moto fugia 12 s e fazia meia-volta,
        //  a viatura (mesma rota) também, e travava com quem vinha atrás. Corta depois da faixa atual (mantém a conversão se já está chegando
        //  no cruzamento e não é meia-volta) e replaneja com os pesos de perseguição do Choose
        static void ReplanFlee(Car f)
        {
            if (f.ri >= f.route.Count) return; int keep = f.ri + 1; float left = Seg(f.route[f.ri]).len - f.s;
            if (keep < f.route.Count && left < 10 && !(Seg(f.route[keep]).kind == 1 && Seg(f.route[keep]).turn == 'U')) keep++;
            for (int i = keep; i < f.route.Count; i++) ReleaseRes(f, f.route[i]);
            if (keep < f.route.Count) f.route.RemoveRange(keep, f.route.Count - keep);
            Extend(f);
        }
        static void ChaseTick(float dt)
        {
            var cop = ChaseCop; if (cop == null) return; var f = cop.fugitive; cop.chaseT += dt; if (f != null) f.chaseT += dt;
            string end = f == null || f.mode == "gone" || !f.car ? "fugitivo sumiu" : f.mode == "moto_crash" ? "fugitivo caiu da moto" : cop.mode != "drive" ? "viatura saiu da rota" :
                         cop.chaseT > (f.caught ? 1e9f : 75) ? "tempo" : (f.pos - PlayerPos).Flat().magnitude > 280 && (cop.pos - PlayerPos).Flat().magnitude > 280 ? "longe do jogador" : null;
            if (end == null && f.caught && (f.modeT -= dt) <= 0) end = "pegou: liberado";
            if (end != null) { EndChase(cop, end); return; }
            float gap = (cop.pos - f.pos).magnitude; ChaseGapSum += gap; ChaseGapN++; ChaseVmax = Mathf.Max(ChaseVmax, f.v);
            f.fleeFrom = cop.pos; if (cop.route[^1] != f.route[^1]) ChaseExtend(cop);
            if (!f.caught)
            {
                if (gap < 12 && f.v < 6) cop.closeT += dt; else cop.closeT = Mathf.Max(0, cop.closeT - dt * .5f);
                if (cop.closeT > 7)
                {   // pegou: encosta (para na faixa com pisca-alerta) e a viatura fica atrás
                    f.caught = true; f.vCap = 0; f.hazard = true; f.modeT = 8; cop.vCap = 0; ChaseCaught++; FBSiren.Set(cop.car, 0); FBSiren.SetLights(cop.car, true);
                    Debug.Log($"[FreeBala][trânsito] PERSEGUIÇÃO: pegou {f.car.id} depois de {cop.chaseT:0} s");
                }
            }
            else { f.v = Mathf.MoveTowards(f.v, 0, dt * 6); f.hazard = true; }
        }
        static void EndChase(Car cop, string why, bool quiet = false)   /* r45t: quiet = desfaz uma perseguição que nem começou */
        {
            var f = cop.fugitive; cop.fugitive = null; cop.runLights = false; cop.vCap = 99; cop.cruise = .92f; cop.T = 1.6f; cop.amax = 2.2f; cop.callT = Random.Range(60f, 150f);
            if (cop.car) { FBSiren.Set(cop.car, 0); FBSiren.SetLights(cop.car, false); }
            if (f != null) { f.chase = null; f.runLights = false; f.caught = false; f.vCap = 99; f.hazard = false; f.fleeFrom = null; f.cruise = 1.05f; f.amax = 2.6f; f.T = 1.2f; f.leave = true; }
            if (quiet) return; ChaseEnds++; chaseCd = Random.Range(160f, 320f); ChaseWhy = why; Debug.Log($"[FreeBala][trânsito] PERSEGUIÇÃO terminou: {why} · {cop.chaseT:0} s");
        }
        static Car SpawnPatrol(int seg, float s) => SpawnPatrolAt(seg, s);
        static Car SpawnPatrol()
        {
            if (!LanePoint(out int seg, out float s, false)) return null; return SpawnPatrolAt(seg, s);
        }
        static Car SpawnPatrolAt(int seg, float s)
        {
            var vid = FBVehicles.PickViatura(() => Random.value); if (vid == null) return null;   /* r45b: as S10 do jogo (PM azul / Brigada); a ViaturaPM antiga só se elas faltarem */
            var S = Seg(seg); var p = FBRoads.At(S, s, out var tan);
            var car = FBVehicles.Make(vid, p, Quaternion.LookRotation(tan.Flat().sqrMagnitude > 1e-6f ? tan.Flat() : Vector3.forward).eulerAngles.y, false, () => Random.value); if (!car) return null;
            // r47: os três PMs já em cena (abordagem + outra patrulha) caíam no "actor ??= Actor(p)" = motorista CIVIL sorteado na viatura.
            // Agora repete um dos PMs da viatura; sem nenhum PM importado, a viatura não nasce.
            var cops = FBStreetEvents.FreeIds(FBNpcAnims.Police); var id = cops.FirstOrDefault() ?? FBUtil.Pick(FBNpcAnims.Police); FBCharacter actor = null;
            if (id != null) { actor = FBRole.SpawnNpc(id, p, "tp" + serial); if (actor) { FBRole.npcs.Remove(actor); var rn = actor.GetComponent<FBRoleNpc>(); if (rn) rn.enabled = false; } }
            if (!actor) { FBVehicles.list.Remove(car); Object.Destroy(car.gameObject); return null; }
            car.syncId = Sid("pm"); car.aiLights = true;
            var c = New(car); c.actor = actor; c.patrol = true; c.wantPark = false; c.cruise = .92f; c.T = 1.6f; c.callT = Random.Range(40f, 110f); c.route.Add(seg); c.s = s; c.v = 5; Extend(c);
            c.pos = p; c.fwd = tan.Flat().normalized; c.yaw = Mathf.Atan2(-c.fwd.x, c.fwd.z); cars.Add(c); Seat(c); Pose(c, 0); return c;
        }
        static void PatrolTick(Car c, float dt)
        {
            if (c.fugitive != null) return;                                                          // r45d: perseguindo
            if (c.callLeft > 0)
            {   // em ocorrência: sirene (FBSiren, 3D) + giroflex, cruzeiro 25 % acima, distância menor
                c.callLeft -= dt; if (c.callLeft <= 0) { FBSiren.Set(c.car, 0); FBSiren.SetLights(c.car, false); c.cruise = .92f; c.T = 1.6f; c.amax = 2.2f; c.callT = Random.Range(60f, 150f); }
                return;
            }
            if ((c.callT -= dt) > 0 || c.mode != "drive") return;
            c.callLeft = Random.Range(25f, 45f); c.cruise = 1.25f; c.T = 1.0f; c.amax = 3.2f; PatrolCalls++;
            FBSiren.Set(c.car, Random.value < .5f ? 1 : 2, .95f);
        }
        static Car New(FBCar car)
        {
            var c = new Car { id = ++serial, car = car, aggr = Random.value };
            c.cruise = Mathf.Lerp(.86f, 1.08f, c.aggr); c.T = Mathf.Lerp(1.65f, 1.15f, c.aggr); c.amax = Mathf.Lerp(1.7f, 2.8f, c.aggr); c.wantPark = Random.value < .4f;
            var L = car.lbox; c.halfL = Mathf.Max(1.6f, L.extents.z * car.scale); c.halfW = Mathf.Max(.75f, L.extents.x * car.scale);
            if (car.mx) { c.halfL = 1.15f; c.halfW = .5f; c.cruise = Mathf.Lerp(.95f, 1.18f, c.aggr); c.amax = Mathf.Lerp(2.4f, 3.6f, c.aggr); c.T = Mathf.Lerp(1.3f, .9f, c.aggr); c.wantPark = false; }   // moto: mais curta/estreita, arranca mais
            return c;
        }
        static FBCharacter Actor(Vector3 p)
        {
            var ids = FBNpcAnims.Roster(FBSession.skin).Where(id => !FBMatch.All.Any(a => a && !a.isPlayer && a.skinId == id)).ToList(); if (ids.Count == 0) return null;   // motoristas = NPCs do pacote que ainda não estão em cena (sem repetir)
            var a = FBRole.SpawnNpc(FBUtil.Pick(ids), p, "t" + serial); FBRole.npcs.Remove(a); var rn = a.GetComponent<FBRoleNpc>(); if (rn) rn.enabled = false; return a;
        }
        // motorista no banco (o FBDriverPose cuida do corpo a cada quadro)
        static void Seat(Car c)
        {
            if (!c.actor || !c.car) return;
            FBDrive.ApplyRemote(c.actor, StateOf(c), 0, true); FBCarSound.StopEngine("veh_remote_" + c.car.syncId + "_e_");
        }
        static List<object> StateOf(Car c) { var p = Coords.ToWeb(c.car.transform.position); return new List<object> { c.car.syncId, (double)p[0], (double)p[1], (double)p[2], (double)c.yaw, (double)c.pitch, (double)c.roll, (double)c.steer, (double)c.car.spin, (double)c.v, 0d, 0d }; }

        // ---- rota: sorteio nos cruzamentos (ou A* para a vaga)
        static void Extend(Car c)
        {
            if (c.fugitive != null && ChaseExtend(c)) return;
            float rest = -c.s; for (int k = c.ri; k < c.route.Count; k++) rest += Seg(c.route[k]).len;
            int guard = 0;
            while (rest < 180 && guard++ < 40)
            {
                var L = Seg(c.route[^1]); if (L.next.Length == 0) break;
                int nx = Choose(L, c); c.route.Add(nx); rest += Seg(nx).len;
            }
            if (c.ri > 6) { int cut = c.ri - 2; c.route.RemoveRange(0, cut); c.ri -= cut; }
        }
        static int Choose(FBSeg L, Car c = null)
        {
            if (L.next.Length == 1) return L.next[0];
            // carro do açaí: puxa as viradas para o lado do jogador (passa perto, mas a rota continua sorteada — nunca a mesma volta)
            Vector3? lure = null; if (c != null && c.lure && FBMatch.Player) { var to = (PlayerPos - c.pos).Flat(); if (to.magnitude > 35) lure = to.normalized; }
            if (c != null && c.leave && FBMatch.Player) { var away = (c.pos - PlayerPos).Flat(); if (away.sqrMagnitude > 1) lure = away.normalized; }   // indo embora: viradas para longe do jogador
            if (c != null && c.fleeFrom is Vector3 ff) { var away = (c.pos - ff).Flat(); if (away.sqrMagnitude > 1) lure = away.normalized; }   // fugindo: para longe do tiro
            float tot = 0; var w = new float[L.next.Length];
            for (int i = 0; i < w.Length; i++)
            {
                var N = Seg(L.next[i]); char t = N.kind == 1 ? N.turn : 'S';
                float wi = t == 'S' ? .55f : t == 'R' ? .25f : t == 'L' ? .2f : .02f;
                bool chaseCar = c != null && (c.runLights || c.chase != null || c.fugitive != null); if (chaseCar && t == 'U') wi *= .01f;   /* r45k: na perseguição a fuga "para longe da viatura" vencia o peso do beco: o fugitivo entrava na rua sem saída, a meia-volta não cabia e o carro era reciclado (fim da perseguição) */
                int after = N.kind == 1 && N.next.Length > 0 ? N.next[0] : N.id; if (Seg(after).kind == 0 && DeadLane(Seg(after))) wi *= chaseCar ? .005f : .15f;   // rua sem saída: quase nunca
                if (lure is Vector3 lv) { var A = Seg(after); var d = ((A.p.Length > 0 ? A.p[^1] : N.p[^1]) - N.p[0]).Flat(); if (d.sqrMagnitude > 1) wi *= Mathf.Lerp(.35f, 2.6f, (Vector3.Dot(d.normalized, lv) + 1) / 2); }
                w[i] = wi; tot += wi;
            }
            float r = Random.value * tot; for (int i = 0; i < w.Length; i++) { r -= w[i]; if (r <= 0) return L.next[i]; }
            return L.next[^1];
        }
        static readonly Dictionary<int, bool> dead = new();
        static bool DeadLane(FBSeg S)
        {
            if (dead.TryGetValue(S.id, out var d)) return d;
            d = S.next.Length > 0 && S.next.All(n => Seg(n).kind == 1 && Seg(n).turn == 'U'); dead[S.id] = d; return d;
        }

        // ---- pedestre que chegou na porta (estado 'gocar' do FBRoleNpc): entra e sai da vaga
        public static void Arrive(FBCharacter ch)
        {
            var c = cars.Find(x => x.walker == ch); var rn = ch.GetComponent<FBRoleNpc>(); if (c == null) { if (rn) rn.st = "walk"; return; }
            var car = c.car; if (!car || car.driver || car.remoteDriver >= 0) { c.mode = "gone"; if (rn) rn.st = "walk"; return; }
            FBRole.npcs.Remove(ch); if (rn) rn.enabled = false; c.actor = ch; c.walker = null; car.aiLights = true; car.syncId ??= "npc:" + FBLobbyNet.Pid + ":car:c" + c.id;
            c.yaw = car.yaw; c.pos = car.transform.position; c.fwd = car.transform.forward;
            Seat(c); float tin = 0;
            if (car.kind == "moto") { FBDrive.StartMount(ch, car); var dpm = ch.GetComponent<FBDriverPose>(); if (dpm && dpm.trans == "mount") { tin = dpm.transLen + .4f; InsDone++; } }   /* r45d: NPC sobe na moto pelo clipe */
            else if (FBDrive.StartCarIn(ch, car)) { tin = ch.GetComponent<FBDriverPose>().transLen + .4f; InsDone++; } else FBAudio.PlayAt("veh_door_open", car.transform.position, .4f, 4);   // r45: entra pelo clipe do 171
            car.StartCoroutine(After(tin, () => { if (car) FBAudio.PlayAt("veh_start", car.transform.position, .35f, 5); }));
            var sp = c.spot; if (sp == null) { c.mode = "gone"; return; }
            c.mode = "out_wait"; c.modeT = tin + 1.2f + Random.value;                           // entra, liga, espera um pouco e sai
        }

        // ---- quadro
        public static int SigStops, PropStops, PropPushes, SirenPasses;
        public static void Update(float dt)
        {
            if (!FBRoads.Ready || occ.Length != FBRoads.segs.Count) return;
            FBSemaforo.Tick(dt);
            foreach (var l in occ) l.Clear();
            foreach (var c in cars) if (c.car && (c.mode == "drive" || c.mode == "kturn") && c.ri < c.route.Count) occ[c.route[c.ri]].Add(c);
            foreach (var l in occ) if (l.Count > 1) l.Sort((a, b) => a.s.CompareTo(b.s));
            Requests();
            PedestrianTakesCar(dt);
            var cam = CamPos; int aud = 0; MotoPhysOn = 0; foreach (var c in cars) if (c.phys) MotoPhysOn++;
            foreach (var c in cars.OrderBy(c => c.car ? (c.pos - cam).sqrMagnitude : 1e12f))
            {
                c.life += dt; if (c.mode == "gone") continue;
                if (c.mode == "walk") { var rn = c.walker ? c.walker.GetComponent<FBRoleNpc>() : null; if (!c.walker || !c.walker.alive || rn == null || rn.st != "gocar" || c.life > 50) { if (rn && rn.st == "gocar") rn.st = "walk"; if (c.spot != null && c.spot.res == c) c.spot.res = null; c.mode = "gone"; } continue; }
                if (!c.car || !c.actor || (c.car.driver && c.car.driver != c.actor)) { Drop(c); continue; }
                switch (c.mode)
                {
                    case "drive": Drive(c, dt); break;
                    case "kturn": KTurn(c, dt); break;
                    case "park_in": ParkIn(c, dt); break;
                    case "parked": Parked(c, dt); break;
                    case "exiting": if ((c.modeT -= dt) <= 0) OutDone(c); break;
                    case "moto_crash": MotoCrashTick(c, dt); break;
                    case "wait_vanish": c.v = 0; c.brake = true; c.hazard = true; if (CanVanish(c)) Respawn(c); break;                       // segurança: o clipe não terminou
                    case "out_wait": OutWait(c, dt); break;
                    case "out_rev": case "out_fwd": ParkOut(c, dt); break;
                }
                if (c.mode == "gone" || !c.car) continue;
                bool near = (c.pos - cam).sqrMagnitude < 50 * 50 && aud < 3; if (near) aud++;               // motor só dos 3 mais perto (6 camadas cada)
                Pose(c, dt, near);
                Recycle(c);
            }
            cars.RemoveAll(c => c.mode == "gone");
            if ((spawnCd -= dt) <= 0 && cars.Count(c => c.mode != "walk" && c.tag == null) < target) { spawnCd = 4; SpawnDriving(false); }
            if (target > 0 && (chaseCd -= dt) <= 0) { chaseCd = 45; if (!FBRacha.Active) StartChase(false); }   // r45d: perseguição de vez em quando
            if (target > 0 && (patrolCd -= dt) <= 0) { patrolCd = 20; if (cars.Count(c => c.patrol) < (FBQuality.Mobile ? 1 : 2)) SpawnPatrol(); }
            foreach (var c in cars) if (c.patrol && c.car) PatrolTick(c, dt);
            ChaseTick(dt);
        }
        static void Drop(Car c)
        {
            if (c.onDrop != null) { var f = c.onDrop; c.onDrop = null; StopAudio(c); ReleaseRes(c); c.mode = "gone"; f(c); return; }   // especial: o dono decide
            if (c.phys && c.car && !c.car.driver) PhysOff(c); c.phys = false;
            StopAudio(c); if (c.actor) { FBDrive.Release(c.actor); Object.Destroy(c.actor.gameObject); } if (c.garupa) Object.Destroy(c.garupa.gameObject); ReleaseRes(c); if (c.spot != null && c.spot.res == c) c.spot.res = null; c.mode = "gone";
        }

        // ---- dirigir na faixa
        static void Drive(Car c, float dt)
        {
            Extend(c); var S = Seg(c.route[c.ri]);
            if (S.kind == 1 && S.turn == 'U') { StartKTurn(c); return; }
            // velocidade livre: via + curvas à frente (freia antes da curva)
            float vFree = Mathf.Min(S.vmax, 14) * c.cruise, d = -c.s, look = 12 + c.v * c.v / 4.5f;
            for (int k = c.ri; k < c.route.Count && d < look; k++)
            {
                var Sk = Seg(c.route[k]);
                for (int i = 0; i < Sk.p.Length; i += 2) { float di = d + Sk.s[i]; if (di < 0 || di > look) continue; float va = Mathf.Sqrt(Sk.vcurv[i] * Sk.vcurv[i] + 2 * 2.4f * di); if (va < vFree) vFree = va; }
                float vm = Sk.vmax * c.cruise; if (d > 0) vm = Mathf.Sqrt(vm * vm + 2 * 2.4f * d); vFree = Mathf.Min(vFree, vm);
                d += Sk.len;
            }
            vFree = Mathf.Min(vFree, c.vCap);
            if (c.shockT > 0) { c.shockT -= dt; vFree = 0; }
            // quem está na frente na rota (fila)
            float gap = 1e9f, vLead = 0; d = -c.s; object who = null;
            for (int k = c.ri; k < c.route.Count && d < 80; k++)
            {
                int id = c.route[k];
                foreach (var o in occ[id]) { if (o == c) continue; if (c.overSide != 0 && id == c.overSeg && Mathf.Abs(c.lat - o.lat) > 1.9f) continue; /* r45e: ultrapassando pela contramão, o da faixa não é mais fila */ float so = o.s; if (k == c.ri && so <= c.s) continue; float g = d + so - c.halfL - o.halfL; if (g < gap) { gap = g; vLead = Mathf.Max(0, o.v); who = o; } }
                d += Seg(id).len;
            }
            // fim da rota (faixa sem saída que escapou do sorteio): para antes da ponta
            { float rest = -c.s; for (int k = c.ri; k < c.route.Count; k++) rest += Seg(c.route[k]).len; if (Seg(c.route[^1]).next.Length == 0) { float g = rest - c.halfL - 1; if (g < gap) { gap = g; vLead = 0; who = "fim"; } } }
            // cruzamento: linha de parada antes da conexão que não pode entrar ainda
            c.nextConn = -1; d = -c.s; c.blink = 0;
            for (int k = c.ri; k < c.route.Count && d < 60; k++)
            {
                var Sk = Seg(c.route[k]); if (Sk.kind != 1) { d += Sk.len; continue; }
                if (k == c.ri) { d += Sk.len; continue; }                                         // já está dentro
                c.nextConn = Sk.id; c.eta = d / Mathf.Max(2.5f, c.v);
                if (d < 42 && (Sk.turn == 'L' || Sk.turn == 'R')) c.blink = Sk.turn == 'L' ? -1 : 1;    // seta antes de virar
                bool mine = res.TryGetValue(Sk.id, out var hs) && hs.Contains(c); if (c.v > 2) c.sigHeld = false;
                // r45: semáforo (FBSemaforo, ciclo do 171): vermelho = linha de parada; amarelo = para se ainda dá (senão passa)
                int sig = !mine && k > 0 && !c.runLights ? FBSemaforo.StateOf(c.route[k - 1]) : 0;
                if (sig >= 2)
                {
                    float g = d - 1.2f - c.halfL, canStop = c.v * c.v / (2 * 3.2f) + 1.5f;
                    if (sig == 3 || g > canStop) { if (g < gap) { gap = g; vLead = 0; who = "semaforo"; } c.waitT = 0; if (!c.sigHeld && c.v < .5f) { c.sigHeld = true; SigStops++; } break; }
                }
                if (!mine)
                {
                    float stopD = c.v * c.v / (2 * 3.2f) + 5;
                    if (CanEnter(c, Sk, k) || c.runLights) { if (d < stopD + 4) Reserve(c, Sk.id); }   /* r45e: perseguição não espera a vez no cruzamento */
                    else { float g = d - 1.2f - c.halfL; if (g < gap) { gap = g; vLead = 0; who = "linha"; } c.waitT += dt; }
                }
                break;
            }
            // sensores (pedestre, jogador, carro fora do trânsito)
            if ((c.sensT -= dt) <= 0) { c.sensT = .1f + Random.value * .05f; Sense(c); }
            if (c.gapObs < gap) { gap = c.gapObs; vLead = c.vObs; who = c.obsWho; }
            c.lastWho = who; c.lastGap = gap;
            // r45e: sirene/fuga — quem está na frente da viatura (ou do fugitivo) encosta e para; a viatura ultrapassa (antes ficava 40 s parada na fila)
            if (c.runLights) leadWho_(c, who, gap);
            /* r45s: também quando o sensor vê o carro como obstáculo físico (FBCar, já encostado de lado) — antes a passagem acabava em 3 s, ele
               voltava para a faixa, a sirene o fazia encostar de novo, e a viatura ficava no anda-e-para atrás dele */
            var ycar = who as Car ?? (c.runLights && who is FBCar ywf ? cars.Find(x => x.car == ywf) : null);
            if (c.runLights && ycar != null && ycar != c.fugitive && ycar != c.chase && ycar.mode == "drive" && gap < 35) { if (ycar.yieldT <= 0) Car.Yields++; ycar.yieldT = 3; }
            if (c.yieldT > 0) { c.yieldT -= dt; vFree = 0; if (c.overSide == 0) c.latT = 1.1f; c.yielded = true; } else if (c.yielded) { c.yielded = false; if (c.overSide == 0) c.latT = 0; }
            // ultrapassagem de obstáculo parado
            Overtake(c, dt, gap, vLead, who);
            // r45e: viatura/fugitivo colado num carro parado fora do trânsito (estacionado invadindo a faixa) sem conseguir ultrapassar: empurra
            //  o carro para o lado (o mesmo empurrão de batida do FBCarDamage) — antes a viatura ficava parada a perseguição inteira
            if (c.runLights && who is FBCar pc && !cars.Exists(x => x.car == pc) && c.v < .6f && gap < 4)
            {
                if ((c.pushT += dt) > 1.5f) { c.pushT = 0; var sd = Vector3.Cross(Vector3.up, c.fwd).normalized; float sg = Vector3.Dot(pc.transform.position - c.pos, sd) >= 0 ? 1 : -1; FBCarDamage.Shove(pc, sd * sg * 4.5f + c.fwd * 2f, sg * 25); FBAudio.PlayAt("reb_carimpact3", pc.transform.position, .45f, 5); Car.Pushes++; }
            }
            else c.pushT = 0;
            // r45r: objeto de física (lixeira/cone/placa caída) colado na frente de carro do trânsito parado há 2,5 s (0,6 s na perseguição),
            //  sem estar ultrapassando: o carro encosta e empurra para fora da faixa. Antes esperava para sempre quando não dava para desviar
            //  (rua estreita, mão única, perto do cruzamento) e travava a fila inteira — a viatura ficou 40 s atrás de uma moto parada numa lixeira
            if (who is FBPropBody pbody && pbody.rb && !pbody.rb.isKinematic && c.v < .6f && gap < 3 && c.overSide == 0 && c.obsStill > (c.runLights ? .6f : 2.5f))
            {
                var pfw = c.fwd.Flat().normalized; var psd = new Vector3(pfw.z, 0, -pfw.x); if (Vector3.Dot(pbody.transform.position - c.pos, psd) < 0) psd = -psd;
                pbody.rb.WakeUp(); pbody.rb.AddForce((psd * 3.2f + pfw * 1.6f + Vector3.up * .9f) * pbody.rb.mass, ForceMode.Impulse); c.obsStill = 0; PropPushes++;
            }
            // IDM
            float s0 = 2.2f, sStar = s0 + Mathf.Max(0, c.v * c.T + c.v * (c.v - vLead) / (2 * Mathf.Sqrt(c.amax * 3.2f)));
            float a = c.amax * (1 - Mathf.Pow(Mathf.Clamp01(c.v / Mathf.Max(.5f, vFree)), 4));
            if (gap < 1e8f) a -= c.amax * (sStar / Mathf.Max(.25f, gap)) * (sStar / Mathf.Max(.25f, gap));
            if (vFree < c.v) a = Mathf.Min(a, (vFree - c.v) * 1.6f);
            a = Mathf.Clamp(a, -9f, c.amax); c.acc = a;
            c.v = Mathf.Max(0, c.v + a * dt); if (gap < .15f) c.v = 0;
            c.brake = a < -1.0f || (c.v < .3f && (gap < 6 || vFree < .5f));
            c.hazard = c.stuck > 14 || c.shockT > 0;
            // travado: buzina, e se continuar (impasse) recicla longe da vista
            if (c.v < .3f) { c.stuck += dt; if (who is Car || who is FBCharacter || who is FBCar) { if ((c.honkT += dt) > 2.4f + 2 * (1 - c.aggr)) { c.honkT = -3 - Random.value * 4; c.car.Horn(); } } }
            else { c.stuck = Mathf.Max(0, c.stuck - dt * 3); c.honkT = 0; c.waitT = 0; }
            if (c.stuck > 45) { Respawn(c); return; }                                                  // Respawn só tira longe e fora da vista
            // anda
            c.s += c.v * dt;
            while (c.ri < c.route.Count - 1 && c.s > Seg(c.route[c.ri]).len)
            {
                c.s -= Seg(c.route[c.ri]).len; var left = Seg(c.route[c.ri]); if (left.kind == 1) ReleaseRes(c, left.id);
                c.ri++; Extend(c);
            }
            c.lat = Mathf.MoveTowards(c.lat, c.latT, dt * (1.2f + c.v * .06f));
            // estacionar: na faixa da vaga, perto do começo da entrada
            if (c.wantPark && c.spot == null && c.life > 35 && Random.value < dt * (c.life > 90 ? 2 : .2f)) PickSpot(c);
            if (c.spot != null && c.route[c.ri] == c.spot.seg && c.s >= c.spot.sin - .5f && Mathf.Abs(c.lat) < .3f) BeginParkIn(c);
            else if (c.spot != null) c.blink = c.route[c.ri] == c.spot.seg && c.spot.sin - c.s < 30 ? (SideOf(c.spot) > 0 ? 1 : -1) : c.blink;
        }
        static float SideOf(FBSpot sp) { var S = Seg(sp.seg); FBRoads.At(S, sp.s, out var t); var q = FBRoads.At(S, sp.s, out _); var d = sp.p - q; return d.x * t.z - d.z * t.x; }

        // ---- reservas dos cruzamentos
        static readonly List<(Car c, int conn, float eta, int pri)> reqs = new();
        static int Pri(char t) => t == 'S' ? 3 : t == 'R' ? 2 : t == 'L' ? 1 : 0;
        static void Requests() { reqs.Clear(); foreach (var c in cars) if (c.mode == "drive" && c.nextConn >= 0 && c.eta < 12) reqs.Add((c, c.nextConn, c.eta, Pri(Seg(c.nextConn).turn))); }
        static bool Conflict(FBSeg A, int b) { foreach (var x in A.conf) if (x == b) return true; return false; }
        static bool CanEnter(Car c, FBSeg C, int k)
        {
            foreach (var cf in C.conf)
            {
                if (res.TryGetValue(cf, out var hs) && hs.Count > 0 && !hs.Contains(c)) return Deadlock(c);
                if (occ[cf].Count > 0) return Deadlock(c);
            }
            // não fecha o cruzamento: precisa caber na faixa de saída
            if (k + 1 < c.route.Count) { int ex = c.route[k + 1]; var q = occ[ex]; if (q.Count > 0 && q[0].s < c.halfL * 2 + 3.5f && q[0].v < 2.5f) return false; }
            float my = c.eta; int mp = Pri(C.turn);
            foreach (var (o, conn, eta, pri) in reqs)
            {
                if (o == c || !Conflict(C, conn)) continue;
                if (eta < my - .6f || (Mathf.Abs(eta - my) <= .6f && (pri > mp || (pri == mp && o.id < c.id)))) return c.waitT > 9 && o.v < .2f && o.waitT > 4 && o.id > c.id;   // desempate de impasse
            }
            return true;
        }
        static bool Deadlock(Car c) => c.waitT > 14 && c.v < .2f && Random.value < .02f;             // trava muito longa: um passa
        static void Reserve(Car c, int conn) { if (!res.TryGetValue(conn, out var hs)) res[conn] = hs = new HashSet<Car>(); hs.Add(c); }
        static void ReleaseRes(Car c, int conn = -1)
        {
            if (conn >= 0) { if (res.TryGetValue(conn, out var hs)) hs.Remove(c); return; }
            foreach (var hs in res.Values) hs.Remove(c);
        }

        // ---- sensores: o que está no corredor à frente (rota + desvio lateral)
        static readonly Vector3[] SP = new Vector3[10], ST = new Vector3[10]; static readonly float[] SD = new float[10];
        static int Corridor(Car c, float look)
        {
            int n = 0; float[] at = { .5f, 2, 4, 6.5f, 9.5f, 13, 17, 22, 28, 35 };
            bool man = c.mp != null && c.mp.Length > 1 && (c.mode == "park_in" || c.mode == "out_fwd" || c.mode == "out_rev");
            foreach (var a in at)
            {
                if (a > look) break; Vector3 p, t; float lat = c.lat;
                if (man) { p = PathAt(c, c.mpos + c.halfL + a, out t); lat = 0; }                    // manobra (vaga): corredor = a trajetória
                else p = Ahead(c, c.halfL + a, out t);
                var r = new Vector3(t.z, 0, -t.x); SP[n] = p + r * lat; ST[n] = t; SD[n] = a; n++;
            }
            return n;
        }
        static void Sense(Car c)
        {
            float look = Mathf.Clamp(6 + c.v * 2.4f, 10, 35); int n = Corridor(c, look); c.gapObs = 1e9f; c.vObs = 0; c.obsWho = null; if (n == 0) return;
            void Hit(float d, float v, object who) { if (d < c.gapObs) { c.gapObs = d; c.vObs = v; c.obsWho = who; } }
            // gente: NPCs do rolê, jogador e remotos a pé
            foreach (var p in FBMatch.All)
            {
                if (!p || !p.alive || p.vehiclePose || p == c.actor) continue; var q = p.Pos; if ((q - c.pos).sqrMagnitude > 45 * 45) continue;
                var fut = q + p.velocity.Flat() * .8f;
                for (int i = 0; i < n; i++)
                {
                    float w = c.halfW + .65f; if (PtHit(SP[i], ST[i], q, w, 1.4f) || PtHit(SP[i], ST[i], fut, w, 1.4f)) { Hit(Mathf.Max(0, SD[i] - 1.2f), 0, p); break; }
                }
            }
            // r45: objetos físicos na faixa (lixeira/cone/cerca/placa que caiu ou foi empurrada para a rua): para e desvia como de carro parado
            foreach (var pb in FBFisica.All)
            {
                if (!pb || !pb.rb || pb.rb.mass < 1.5f) continue; var q = pb.transform.position; if ((q - c.pos).sqrMagnitude > 40 * 40) continue;
                for (int i = 0; i < n; i++) if (PtHit(SP[i], ST[i], q, c.halfW + .45f, 1.0f)) { Hit(Mathf.Max(0, SD[i] - .8f), 0, pb); PropStops++; break; }
            }
            // veículos: carro do jogador, remotos, estacionado invadindo, carro do trânsito manobrando/ultrapassando
            foreach (var car in FBVehicles.list)
            {
                if (!car || car == c.car) continue; var cp = car.transform.position; if ((cp - c.pos).sqrMagnitude > 50 * 50) continue;
                var o = cars.Find(x => x.car == car);
                if (o != null && o.mode == "drive" && Mathf.Abs(o.lat) < .4f && occ.Length > 0)
                {   // do trânsito, andando na faixa: só conta se NÃO for da minha rota (esses já entram na fila)
                    bool onRoute = false; for (int k = c.ri; k < c.route.Count && k < c.ri + 6; k++) if (c.route[k] == o.route[o.ri]) { onRoute = true; break; }
                    if (onRoute) continue;
                }
                var tf = car.transform; var L = car.lbox; var ext = L.extents * car.scale; var ctr = L.center * car.scale;
                for (int i = 0; i < n; i++)
                {
                    var lp = tf.InverseTransformPoint(SP[i]) * car.scale; lp -= ctr;    // InverseTransformPoint já tira a escala: volta ao metro
                    var dl = Quaternion.Inverse(tf.rotation) * ST[i];
                    float ex = ext.x + c.halfW * Mathf.Abs(dl.z) + c.halfL * .15f * Mathf.Abs(dl.x) + .3f, ez = ext.z + c.halfW * Mathf.Abs(dl.x) + .35f;
                    if (Mathf.Abs(lp.x) <= ex && Mathf.Abs(lp.z) <= ez)
                    {
                        float vv = o != null ? o.v * Vector3.Dot(o.fwd, ST[i]) : car.driver ? Mathf.Max(0, car.v * Vector3.Dot(tf.forward, ST[i])) : 0;
                        Hit(Mathf.Max(0, SD[i] - .6f), Mathf.Max(0, vv), car); break;
                    }
                }
                // batida do jogador: susto, pisca-alerta e buzina
                if (car.driver && car.driver.isPlayer && Mathf.Abs(car.v) > 3 && (cp - c.pos).magnitude < c.halfL + 2.6f && c.shockT <= 0) { c.shockT = 2.5f + Random.value * 2; c.car.Horn(); }
            }
        }
        static bool PtHit(Vector3 a, Vector3 t, Vector3 q, float w, float half)
        {
            var d = q - a; d.y = 0; float along = d.x * t.x + d.z * t.z, latd = Mathf.Abs(d.x * t.z - d.z * t.x);
            return Mathf.Abs(along) <= half && latd <= w;
        }
        // ponto a 'dist' m à frente do centro, seguindo a rota
        static Vector3 Ahead(Car c, float dist, out Vector3 tan)
        {
            if (c.route.Count == 0) { tan = c.fwd.Flat().normalized; if (tan.sqrMagnitude < .5f) tan = Vector3.forward; return c.pos + tan * dist; }   // sem rota (saindo da vaga): reto à frente
            float s = c.s + dist; int k = Mathf.Clamp(c.ri, 0, c.route.Count - 1);
            while (k < c.route.Count - 1 && s > Seg(c.route[k]).len) { s -= Seg(c.route[k]).len; k++; }
            var p = FBRoads.At(Seg(c.route[k]), s, out tan); tan.y = 0; tan.Normalize(); return p;
        }

        // ---- ultrapassar obstáculo parado (faixa da esquerda livre, ou contramão sem ninguém vindo)
        static void leadWho_(Car c, object who, float gap) => c.leadWho = who == null ? "livre" : (who is Car ? "carro" : who is string w ? w : who is FBCar fc ? $"{fc.id} ({(cars.Exists(x => x.car == fc) ? "trânsito" : fc.driver ? "com motorista" : "parado")})" : who.GetType().Name) + (gap < 1e8f ? $" a {gap:0} u" : "");
        static void Overtake(Car c, float dt, float gap, float vLead, object who)
        {
            var S = Seg(c.route[c.ri]);
            bool blocker = (who is FBCar || who is FBPropBody || (who is Car oc && (oc.mode != "drive" || oc.hazard || oc.stuck > 6 || oc.yieldT > 0))) && vLead < .6f && gap < 18;
            c.obsStill = blocker ? c.obsStill + dt : 0;
            if (c.overSide != 0)
            {   // ultrapassando: volta para a faixa quando passar (ou ao chegar no cruzamento)
                if (c.s > c.passUntil || S.id != c.overSeg) { c.latT = 0; c.overSide = 0; }
                return;
            }
            // r45s: sirene/fuga com carro do trânsito ENCOSTADO dando passagem colado na frente: passa rente pela esquerda dele em qualquer rua,
            //  só conferindo a contramão por 25 m. Antes só ultrapassava com a contramão livre por 70 m e longe do cruzamento: a viatura ficou
            //  20 s atrás de um i30 encostado no meio-fio com a rua vazia (r45r)
            if (c.runLights && S.kind == 0 && gap < 6 && c.v < 7)
            {
                var yc = who as Car ?? (who is FBCar wf ? cars.Find(x => x.car == wf) : null);
                if (yc != null && yc != c.fugitive && yc != c.chase && (yc.yielded || yc.yieldT > 0))
                {
                    if (S.opp >= 0 && !S.one)
                    {
                        var Op = Seg(S.opp);
                        foreach (var o in cars) { if (o == c || o.mode != "drive" || o.ri >= o.route.Count || o.route[o.ri] != Op.id) continue; float so = FBRoads.Project(S, o.pos, out _, out _); if (so > c.s - 3 && so < c.s + 25) return; }
                    }
                    c.overSide = -1; c.overSeg = S.id; c.latT = Mathf.Max(-2.6f, yc.lat - (yc.halfW + c.halfW + .45f));
                    c.passUntil = c.s + gap + 2 * (yc.halfL + c.halfL) + 2; c.obsStill = 0; SirenPasses++; return;
                }
            }
            if (c.obsStill < (c.runLights ? .5f : 2.5f) || S.kind != 0 || c.v > (c.runLights ? 7 : 4)) return;
            if (S.left >= 0 && S.one)
            {   // pista larga: muda para a faixa da esquerda (se não tiver ninguém do lado)
                var Lft = Seg(S.left); float sl = FBRoads.Project(Lft, c.pos, out _, out float dd); if (dd > 6) return;
                foreach (var o in occ[Lft.id]) if (o.s > sl - 12 && o.s < sl + 14) return;
                var p = FBRoads.At(Lft, sl, out var tn); var r = new Vector3(tn.z, 0, -tn.x); float off = Vector3.Dot(c.pos - p, r);
                ReleaseRes(c); c.route.Clear(); c.route.Add(Lft.id); c.ri = 0; c.s = sl; c.lat = off; c.latT = 0; Extend(c); c.obsStill = 0; c.blink = -1;
                return;
            }
            if (S.opp >= 0 && !S.one)
            {   // mão dupla: contramão livre por 70 m
                var O = Seg(S.opp);
                foreach (var o in cars)
                {
                    if (o == c || o.mode != "drive" || o.ri >= o.route.Count) continue; int oseg = o.route[o.ri];
                    if (oseg != O.id) continue; float so = FBRoads.Project(S, o.pos, out _, out _); if (so > c.s - 5 && so < c.s + 75) return;
                }
                float off = -(2 * Mathf.Clamp(S.w / 4, 1.3f, 2.7f)) - .2f;
                if (c.s + gap + 11 > S.len - 4) return;                                         // não termina antes do cruzamento: espera
                c.overSide = -1; c.overSeg = S.id; c.latT = off; c.passUntil = c.s + gap + 11; c.obsStill = 0;
            }
        }

        // ---- vaga: escolher, entrar, ficar, sair
        static void PickSpot(Car c)
        {
            int cur = c.route[c.ri]; FBSpot best = null; List<int> bestR = null; float bd = 1e9f;
            foreach (var sp in FBRoads.spots)
            {
                if (sp.car || sp.res != null || sp.inp.Length < 2) continue; float d = Vector3.Distance(sp.p, c.pos); if (d > 220 || d > bd) continue;
                bool taken = false; foreach (var car in FBVehicles.list) if (car && Vector3.Distance(car.transform.position, sp.p) < 2.4f) { taken = true; break; }
                if (taken) continue;
                var r = cur == sp.seg && c.s < sp.sin - 6 ? new List<int> { cur } : FBRoads.Route(cur, sp.seg); if (r == null) continue;
                bd = d; best = sp; bestR = r;
            }
            if (best == null) { c.wantPark = Random.value < .5f; return; }
            ReleaseRes(c); best.res = c; c.spot = best; c.route.Clear(); c.route.AddRange(bestR); c.ri = 0;
        }
        static void BeginParkIn(Car c)
        {
            var sp = c.spot; ReleaseRes(c); c.mode = "park_in"; SetPath(c, sp.inp, false); c.blink = SideOf(sp) > 0 ? 1 : -1; c.latT = c.lat = 0;
        }
        static void SetPath(Car c, Vector3[] p, bool rev)
        {
            c.mp = p; c.ms = new float[p.Length]; float acc = 0; for (int i = 0; i < p.Length; i++) { if (i > 0) acc += Vector3.Distance(p[i - 1], p[i]); c.ms[i] = acc; } c.mlen = acc; c.mpos = 0; c.mrev = rev;
        }
        static Vector3 PathAt(Car c, float d, out Vector3 tan)
        {
            var p = c.mp; if (p.Length < 2) { tan = c.fwd; return p.Length > 0 ? p[0] : c.pos; }
            d = Mathf.Clamp(d, 0, c.mlen); int i = 1; while (i < p.Length - 1 && c.ms[i] < d) i++;
            float t = (d - c.ms[i - 1]) / Mathf.Max(1e-4f, c.ms[i] - c.ms[i - 1]); tan = (p[i] - p[i - 1]).Flat().normalized; return Vector3.Lerp(p[i - 1], p[i], t);
        }
        // anda numa trajetória de manobra; devolve true no fim
        static bool FollowPath(Car c, float dt, float vmax, bool stopAtEnd)
        {
            float rest = c.mlen - c.mpos; float vt = stopAtEnd ? Mathf.Min(vmax, Mathf.Sqrt(2 * 1.6f * Mathf.Max(0, rest)) + .25f) : vmax;
            c.v = Mathf.MoveTowards(c.v, vt, dt * 2.2f); c.mpos += c.v * dt; c.acc = 0; c.brake = c.v < vt - .3f || rest < 1.5f;
            var p = PathAt(c, c.mpos, out var t); c.pos = p; c.fwd = c.mrev ? -t : t; if (c.fwd.sqrMagnitude < 1e-6f) c.fwd = c.car.transform.forward;
            c.reverse = c.mrev; c.yaw = Mathf.Atan2(-c.fwd.x, c.fwd.z);
            return c.mpos >= c.mlen - .05f;
        }
        static void ParkIn(Car c, float dt)
        {
            // alguém (jogador) atravessou na frente: espera
            Sense(c); if (c.gapObs < 1.2f) { c.v = 0; c.brake = true; return; }
            if (!FollowPath(c, dt, 2.6f, true)) return;
            var sp = c.spot; c.pos = sp.p; c.yaw = -sp.yaw * Mathf.Deg2Rad; c.fwd = FBDrive.Fwd(c.yaw); c.v = 0; c.mode = "parked"; c.modeT = 1 + Random.value; sp.car = c.car; sp.res = null; c.blink = 0; c.reverse = false;
        }
        static void Parked(Car c, float dt)
        {
            c.brake = false; c.blink = 0; if ((c.modeT -= dt) > 0) return;
            // r45: desliga o carro e desce pelo clipe do 171 (porta abre, sai, porta fecha) — o fim chama OutDone; sem clipe/porta tomada: como antes
            var car0 = c.car; var a0 = c.actor; var dp0 = a0 ? a0.GetComponent<FBDriverPose>() : null;
            if (c.mode == "parked" && dp0 && (car0.kind == "moto" ? FBDrive.StartDismount(a0, car0) : FBDrive.StartCarOut(a0, car0))) { StopAudio(c); FBAudio.PlayAt("veh_stop", car0.transform.position, .35f, 5); c.mode = "exiting"; c.modeT = dp0.transLen + 2; dp0.onOut = (ch, cr) => OutDone(c); return; }
            OutDone(c);
        }
        static void OutDone(Car c)
        {
            if (c.mode == "gone") return;
            // o motorista vira pedestre do rolê; o carro fica na vaga (outro pedestre pode pegar)
            var car = c.car; var a = c.actor; if (!car || !a) { c.mode = "gone"; return; } StopAudio(c); var outP = car.L2W(car.rigOut ?? new Vector3(-1.6f, 0, 0)); float oyaw = a.Yaw;
            bool anim = c.mode == "exiting" && FBDrive.OutPoint(a, car, out outP, out oyaw);
            FBDrive.Release(a); if (!anim && FBWorld.GroundY(outP.x, outP.y + 2, outP.z, .5f, 4) is float gy) outP.y = gy;
            var cc = a.GetComponent<CharacterController>(); if (cc) cc.enabled = false; a.transform.position = outP; if (cc) cc.enabled = true; a.transform.rotation = Quaternion.Euler(0, anim ? oyaw : a.Yaw, 0); if (anim) a.Yaw = oyaw;
            if (anim && FBDoors.HasPart(car, 1)) FBDoors.SetMask(car, FBDoors.Mask(car) & ~1, true);
            var rn = a.GetComponent<FBRoleNpc>(); if (rn) { rn.enabled = true; rn.st = "walk"; rn.goalP = null; } FBRole.npcs.Add(a); a.SetWeaponVisible(false);
            if (!anim) FBAudio.PlayAt("veh_door_close", car.transform.position, .4f, 4); car.aiLights = false; car.v = 0; OutsDone++;
            c.actor = null; c.mode = "gone";
        }
        static void OutWait(Car c, float dt)
        {
            c.brake = true; c.blink = -SideOfSign(c.spot); if ((c.modeT -= dt) > 0) return;
            var sp = c.spot; if (sp.rev != null && sp.rev.Length > 1) { SetPath(c, sp.rev, true); c.mode = "out_rev"; }
            else { SetPath(c, sp.outp, false); c.mode = "out_fwd"; }
            if (sp.car == c.car) sp.car = null;
        }
        static int SideOfSign(FBSpot sp) => sp != null && SideOf(sp) > 0 ? 1 : -1;
        static void ParkOut(Car c, float dt)
        {
            var sp = c.spot;
            if (c.mode == "out_rev") { if (FollowPath(c, dt, 1.5f, true)) { SetPath(c, sp.outp, false); c.mode = "out_fwd"; c.v = 0; } return; }
            // espera a faixa ficar livre (ninguém chegando até 30 m atrás da saída)
            if (c.mpos < .5f)
            {
                bool clear = true; var S = Seg(sp.seg);
                foreach (var o in cars) if (o != c && o.mode == "drive" && o.ri < o.route.Count && o.route[o.ri] == sp.seg && o.s > sp.sout - 32 && o.s < sp.sout + 6) { clear = false; break; }
                Sense(c); if (c.gapObs < 3) clear = false;
                if (!clear) { c.v = 0; c.brake = true; c.blink = SideOfSign(sp) > 0 ? -1 : 1; return; }
            }
            c.blink = SideOfSign(sp) > 0 ? -1 : 1;
            if (!FollowPath(c, dt, 4f, false)) return;
            // de volta ao trânsito
            c.mode = "drive"; c.route.Clear(); c.route.Add(sp.seg); c.ri = 0; c.s = sp.sout; c.lat = c.latT = 0; c.reverse = false; c.spot = null; sp.res = null; c.life = 0; c.wantPark = Random.value < .25f; Extend(c);
        }
        // pedestre do rolê vai pegar um carro estacionado numa vaga (de vez em quando, 25–160 m do jogador; paredão tocando não)
        static void PedestrianTakesCar(float dt)
        {
            if (FBSession.online) return;                                                       // r68: online os estacionados são iguais em todos os PCs: NPC local não leva nenhum
            if (target <= 0 || (enterCd -= dt) > 0 || cars.Count(t => t.mode == "drive" && t.tag == null) >= target + 2) return;
            enterCd = 25 + Random.value * 35; var pl = PlayerPos;
            var cand = FBRoads.spots.Where(sp => sp.car && sp.res == null && !sp.car.ie && sp.car != FBRole.roleCar && !sp.car.driver && sp.car.remoteDriver < 0 && !(FBParedao.State(sp.car)?.on ?? false)
                && !(sp.car.syncId ?? "").StartsWith("role:") && !cars.Any(t => t.car == sp.car)).Where(sp => { float d = Vector3.Distance(sp.p, pl); return d > 25 && d < 160; }).ToList();
            if (cand.Count == 0) return;
            var spot = FBUtil.Pick(cand); var car = spot.car; var door = car.L2W(car.rigOut ?? new Vector3(-1.6f, 0, 0));
            var ch = FBRole.npcs.Where(o => o && o.alive && o.GetComponent<FBRoleNpc>()?.st == "walk" && (o.Pos - door).magnitude < 60).OrderBy(o => (o.Pos - door).sqrMagnitude).FirstOrDefault();
            if (!ch) return;
            var rn = ch.GetComponent<FBRoleNpc>(); rn.st = "gocar"; rn.goalP = door; rn.stT = 45;
            var c = New(car); c.walker = ch; c.mode = "walk"; c.spot = spot; spot.res = c; c.yaw = car.yaw; c.pos = car.transform.position; c.fwd = car.transform.forward; cars.Add(c);
        }

        // ---- ponta sem saída: manobra de três pontos (frente virando, ré virando ao contrário, frente para a faixa de volta)
        // ---- meia-volta em rua estreita / sem saída: manobra em N pontos planejada no ASFALTO de verdade (os cantos do carro ficam na pista)
        // e sem encostar em outro veículo; cada perna vai o mais longe que cabe (frente virando à esquerda, ré virando à direita...) e a
        // última é uma curva suave até a faixa de volta. Sem plano possível (beco apertado): o carro some e reaparece longe.
        static void StartKTurn(Car c)
        {
            if (c.runLights) ChaseKTurns++;   // r45k: diagnóstico (perseguição não devia precisar de meia-volta)
            if (c.ri + 1 >= c.route.Count) Extend(c);
            var outLane = Seg(c.route[Mathf.Min(c.route.Count - 1, c.ri + 1)]);
            var qq = FBRoads.At(outLane, Mathf.Min(6, outLane.len), out var tq); var ft = tq.Flat().normalized;
            ReleaseRes(c); c.v = Mathf.Min(c.v, 2); c.kWait = 0;
            if (!PlanTurn(c, c.pos, c.fwd.Flat().normalized, qq, ft)) { Respawn(c); return; }
            c.mode = "kturn"; c.mstage = 0; SetPath(c, c.legs[0].p, c.legs[0].rev);
        }
        static bool PlanTurn(Car c, Vector3 p, Vector3 f, Vector3 qq, Vector3 ft)
        {
            c.legs.Clear(); float R = Mathf.Max(4.6f, c.car ? c.car.wheelbase * 1.85f : 4.8f); bool rev = false; int fails = 0;
            for (int leg = 0; leg < 8 && fails < 2; leg++)
            {
                float rem = -Vector3.SignedAngle(f, ft, Vector3.up); if (rem < -5) rem += 360;            // quanto falta girar para a esquerda
                if (!rev && rem < 85) { var C = Bez(p, f, qq, ft); if (LegFits(c, C, f, 0, true)) { c.legs.Add((C, false)); return true; } }
                Vector3[] best = null; float bestA = 0;
                for (float a = 8; a <= Mathf.Clamp(rem, 8, 100); a += 6) { var A = Arc(p, f, R, a, !rev); if (!LegFits(c, A, f, a, false)) break; best = A; bestA = a; }
                if (best == null) { fails++; rev = !rev; continue; }                                         // não coube nem um pouco: tenta o outro sentido
                fails = 0; c.legs.Add((best, rev)); p = best[^1]; f = Rot(f, bestA); rev = !rev;
            }
            return false;
        }
        // perna inteira cabe? (arco: rumo gira 'deg' para a esquerda ao longo dele; curva: rumo = tangente)
        static bool LegFits(Car c, Vector3[] P, Vector3 f0, float deg, bool bez)
        {
            for (int i = 1; i < P.Length; i++) { var f = bez ? P[i] - P[i - 1] : Rot(f0, deg * i / (P.Length - 1)); if (!Fits(c, P[i], f)) return false; }
            return true;
        }
        // caixa do carro (com folga) inteira no asfalto e sem bater em outro veículo
        static bool Fits(Car c, Vector3 p, Vector3 f)
        {
            f.y = 0; if (f.sqrMagnitude < 1e-6f) return true; f.Normalize(); var r = new Vector3(f.z, 0, -f.x);
            float hl = Mathf.Max(.8f, c.halfL - .3f), hw = Mathf.Max(.4f, c.halfW - .25f);
            for (int i = 0; i < 6; i++) { float a = i < 2 ? hl : i < 4 ? -hl : 0, b = (i & 1) == 0 ? hw : -hw; if (!FBRoads.IsRoad(p + f * a + r * b)) return false; }
            foreach (var o in FBVehicles.list)
            {
                if (!o || o == c.car) continue; if ((o.transform.position - p).sqrMagnitude > 14 * 14) continue;
                if (BoxHit(p, f, c.halfL + .3f, c.halfW + .25f, o)) return false;
            }
            return true;
        }
        // retângulo (centro p, rumo f, meias medidas) contra a caixa de um veículo (SAT 2D)
        static readonly Vector2[] bxA = new Vector2[4], bxB = new Vector2[4];
        static void FillBox(Vector2[] q, Vector3 ctr, Vector3 fw, float l, float w)
        {
            var rr = new Vector3(fw.z, 0, -fw.x); var P = new Vector2(ctr.x, ctr.z); var fv = new Vector2(fw.x, fw.z) * l; var rv = new Vector2(rr.x, rr.z) * w;
            q[0] = P + fv + rv; q[1] = P + fv - rv; q[2] = P - fv - rv; q[3] = P - fv + rv;
        }
        public static bool BoxHit(Vector3 p, Vector3 f, float hl, float hw, FBCar o)
        {
            FillBox(bxA, p, f, hl, hw);
            var L = o.lbox; var e = L.extents * o.scale; var of = o.transform.forward; of.y = 0; of = of.sqrMagnitude > 1e-6f ? of.normalized : Vector3.forward;
            FillBox(bxB, o.transform.TransformPoint(L.center), of, e.z, e.x);
            for (int pi = 0; pi < 2; pi++)
            {
                var poly = pi == 0 ? bxA : bxB;
                for (int i = 0; i < 4; i++)
                {
                    var ed = poly[(i + 1) % 4] - poly[i]; var n = new Vector2(-ed.y, ed.x); float a0 = 1e9f, a1 = -1e9f, b0 = 1e9f, b1 = -1e9f;
                    foreach (var q in bxA) { float d = Vector2.Dot(q, n); a0 = Mathf.Min(a0, d); a1 = Mathf.Max(a1, d); }
                    foreach (var q in bxB) { float d = Vector2.Dot(q, n); b0 = Mathf.Min(b0, d); b1 = Mathf.Max(b1, d); }
                    if (a1 < b0 || b1 < a0) return false;
                }
            }
            return true;
        }
        static Vector3 Rot(Vector3 f, float deg) => Quaternion.Euler(0, -deg, 0) * f;           // + = gira à esquerda (vista de cima)
        // arco girando o carro para a ESQUERDA (vista de cima): de frente o centro fica à esquerda; de ré (volante para a direita) à direita
        static Vector3[] Arc(Vector3 p, Vector3 f, float R, float deg, bool centerLeft)
        {
            var left = new Vector3(-f.z, 0, f.x); var ctr = centerLeft ? p + left * R : p - left * R; const int n = 10; var o = new Vector3[n + 1];
            for (int i = 0; i <= n; i++) { o[i] = ctr + Quaternion.Euler(0, -deg * i / n, 0) * (p - ctr); o[i].y = p.y; }
            return o;
        }
        static Vector3[] Bez(Vector3 a, Vector3 da, Vector3 b, Vector3 db)
        {
            float L = Vector3.Distance(a, b) * .4f; var o = new Vector3[14];
            for (int i = 0; i < o.Length; i++) { float t = i / (o.Length - 1f), u = 1 - t; o[i] = u * u * u * a + 3 * u * u * t * (a + da * L) + 3 * u * t * t * (b - db * L) + t * t * t * b; }
            return o;
        }
        // execução: perna por perna; se algo entra no caminho (carro do jogador, pedestre), espera; parado 6 s -> replaneja de onde está
        static void KTurn(Car c, float dt)
        {
            c.blink = 2;
            if (c.legs.Count == 0 || c.mstage >= c.legs.Count) { Replan(c); return; }
            if (KBlocked(c)) { c.v = 0; c.brake = true; if ((c.kWait += dt) > 6) { c.kWait = 0; Replan(c); } return; }
            c.kWait = 0; var cur = c.legs[c.mstage];
            if (!FollowPath(c, dt, cur.rev ? 1.4f : 2.0f, true)) return;
            c.mstage++; c.v = 0;
            if (c.mstage < c.legs.Count) { SetPath(c, c.legs[c.mstage].p, c.legs[c.mstage].rev); return; }
            // de volta na faixa de saída (depois da conexão de meia-volta)
            int k = Mathf.Min(c.route.Count - 1, c.ri + 1); var L = Seg(c.route[k]); c.ri = k; c.s = Mathf.Min(6, L.len); c.mode = "drive"; c.reverse = false; c.blink = 0; c.lat = c.latT = 0; Extend(c);
        }
        // o próximo 1,5 m da manobra bate em alguém?
        static bool KBlocked(Car c)
        {
            float d = c.mpos + 1.5f; if (d > c.mlen + .5f) return false; var q = PathAt(c, d, out var t); var f = c.mrev ? -t : t;
            foreach (var o in FBVehicles.list) { if (!o || o == c.car) continue; if ((o.transform.position - q).sqrMagnitude > 12 * 12) continue; if (BoxHit(q, f, c.halfL + .15f, c.halfW + .1f, o)) return true; }
            foreach (var p in FBMatch.All) { if (!p || !p.alive || p.vehiclePose || p == c.actor) continue; var pp = p.Pos; if ((pp - q).sqrMagnitude > 9) continue; if (PtHit(q, f, pp, c.halfW + .5f, c.halfL + .5f)) return true; }
            return false;
        }
        static void Replan(Car c)
        {
            int k = Mathf.Min(c.route.Count - 1, c.ri + 1); var outLane = Seg(c.route[k]); var qq = FBRoads.At(outLane, Mathf.Min(6, outLane.len), out var tq);
            if (!PlanTurn(c, c.pos, c.fwd.Flat().normalized, qq, tq.Flat().normalized)) { Respawn(c); return; }
            c.mstage = 0; SetPath(c, c.legs[0].p, c.legs[0].rev);
        }

        // ---- pose do carro, luzes, som
        static void Pose(Car c, float dt, bool audio = false)
        {
            var car = c.car;
            if (c.mode == "drive")
            {
                var S = Seg(c.route[c.ri]); var p = FBRoads.At(S, c.s, out var t0); var ah = Ahead(c, 1.6f, out var t1);
                var dir = (ah - p).Flat(); dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : t0.Flat().normalized;
                var r = new Vector3(dir.z, 0, -dir.x); p += r * c.lat;
                if (Mathf.Abs(c.lat - c.latT) > .02f) dir = (dir + r * Mathf.Clamp((c.latT - c.lat) * .25f, -.3f, .3f)).normalized;   // apontando para onde está indo de lado
                c.pos = p; c.fwd = dir; c.yaw = Mathf.Atan2(-dir.x, dir.z); c.reverse = false;
                // esterço pela curvatura (entre-eixos)
                var ah2 = Ahead(c, 4.5f, out var t2); float k = Vector3.SignedAngle(t0.Flat(), t2.Flat(), Vector3.up) * Mathf.Deg2Rad / 4.5f;
                c.steer = Mathf.Lerp(c.steer, Mathf.Clamp(-Mathf.Atan(car.wheelbase * k), -.6f, .6f), Mathf.Min(1, dt * 6));
            }
            else if (c.mode == "kturn" || c.mode == "park_in" || c.mode == "out_rev" || c.mode == "out_fwd")
            {
                var ah = PathAt(c, c.mpos + (c.mrev ? -1.5f : 1.5f), out var tt); float k = Vector3.SignedAngle(c.fwd, (c.mrev ? -tt : tt), Vector3.up) * Mathf.Deg2Rad / 1.5f;
                c.steer = Mathf.Lerp(c.steer, Mathf.Clamp((c.mrev ? 1 : -1) * Mathf.Atan(car.wheelbase * k), -.6f, .6f), Mathf.Min(1, dt * 6));
            }
            // chão: altura da faixa; perto da câmera mede com raio (degrau, lombada) e inclina a carroceria
            var pos = c.pos; float camD = (pos - CamPos).magnitude;
            if (car.mx && MotoPhys(c, car, camD, dt))
            {   // r45b: a física do MX conduz a moto (som = MXBikeSound da moto ativa); aqui só luzes e o motorista
                car.lBrake = c.brake; car.lRev = false; car.lBlink = c.hazard ? 2 : c.blink; car.aiLights = true;
                ActorVis(c, car, camD);
                return;
            }
            if (camD < 70)
            {
                car.SetColliders(false); if (FBWorld.GroundY(pos.x, pos.y + 1.2f, pos.z, .4f, 3) is float gy) pos.y = Mathf.Lerp(car.transform.position.y, gy, Mathf.Min(1, dt * 12)); car.SetColliders(true);
                if (!car.mx) BodyDyn(c, dt);                                                     // moto: a rolagem é a inclinação da curva (MotoPose)
            }
            else { c.pitch = Mathf.Lerp(c.pitch, 0, dt); c.roll = Mathf.Lerp(c.roll, 0, dt); }
            c.pos = pos;
            float sv = c.reverse ? -c.v : c.v;
            if (car.mx) { MotoPose(c, car, ref pos, sv, dt); }
            else car.transform.SetPositionAndRotation(pos, FBDrive.CarRot(c.pitch, c.yaw, c.roll));
            car.yaw = c.yaw; car.pitch = c.pitch; car.v = sv; car.spin += sv * dt;
            car.steerN = Mathf.Clamp(c.steer / .55f, -1, 1); FBDrive.PoseWheels(car, c.steer);
            car.lBrake = c.brake; car.lRev = c.reverse && c.v > .05f; car.lBlink = c.hazard ? 2 : c.blink; car.aiLights = true;
            // motorista: some longe (desenho) — o FBDriverPose senta o corpo
            ActorVis(c, car, camD);
            // motor: só os 4 mais perto
            var key = "veh_npc_" + c.id + "_";
            if (audio && !car.mx)
            {
                var prof = car.engineProf ??= FBCarSound.ProfileFor(car.id, car); c.gb ??= new FBCarSound.Gearbox(FBDrive.TopV(car));
                var g = c.gb.Step(Mathf.Max(dt, 1e-3f), sv, c.acc > .2f ? .75f : .25f); FBCarSound.EngineAudio(key, prof, g.rpm, c.gb.shiftT > 0 ? .15f : c.acc > .2f ? .7f : c.acc < -.5f ? .1f : .3f, pos, .3f, 5); c.audio = true;
                if (c.acc < -5.5f && c.v > 7 && Time.time > c.squealT) { c.squealT = Time.time + 4; FBAudio.PlayAt("veh_brake_squeal", pos, Mathf.Clamp(c.v / 25, .3f, .7f), 7, .06f); }   /* r45: freada de emergência do trânsito (171) */
            }
            else if (c.audio) { StopAudio(c, false); }
        }
        // r45n: siren=false no corte de som por distância (só os 4 motores mais perto tocam) — antes desligava junto a sirene e o giroflex da
        //  viatura (perseguição perdia a sirene no meio quando outros carros chegavam mais perto da câmera)
        static void StopAudio(Car c, bool siren = true) { if (c.audio) { FBCarSound.StopEngine("veh_npc_" + c.id + "_"); c.audio = false; } if (c.car && c.car.mx) c.car.mx.RemoteStop(); if (siren && c.patrol && c.car) FBSiren.Stop(c.car); }
        // r45: moto MX no trânsito — a física completa do MX só roda para quem pilota (e no racha); aqui a moto segue a faixa e recebe do MX:
        //  · inclinação física da curva (tan θ = v²·curvatura / g), limitada pela leanCurve do modelo, com a inércia de ~0,25 s;
        //  · guidão pelo steerAngle do modelo, rodas, som próprio (MXBikeSound) e as poses do piloto do MX (RemoteState -> RiderClip/PoseRider);
        //  · motoqueiro agressivo em reta livre puxa o grau (sobe 0,7 s até 22–40°, segura 1,5–4 s, desce 0,6 s; a frente pousa suave), girando
        //    em volta do eixo traseiro como a moto do jogador.
        static void MotoPose(Car c, FBCar car, ref Vector3 pos, float sv, float dt)
        {
            var mx = car.mx; float k = 0;                                                                  // curvatura (+ = curva à direita)
            if (c.mode == "drive" && c.route.Count > 0) { Ahead(c, 5f, out var t2); FBRoads.At(Seg(c.route[Mathf.Min(c.ri, c.route.Count - 1)]), c.s, out var t0); k = Vector3.SignedAngle(t0.Flat(), t2.Flat(), Vector3.up) * Mathf.Deg2Rad / 5f; }
            float lean = Mathf.Atan(sv * sv * k / (9.8f * 1.2f)) * Mathf.Rad2Deg; lean = Mathf.Clamp(lean, -mx.MaxLeanDeg, mx.MaxLeanDeg);
            c.roll = Mathf.Lerp(c.roll, -lean * Mathf.Deg2Rad, Mathf.Min(1, dt * 4));                         // CarRot: rolagem + = inclina para a esquerda
            // grau
            bool straight = Mathf.Abs(k) < .006f && c.mode == "drive" && c.gapObs > 50 && sv > 7 && sv < 18;
            if (c.whSt == 0 && c.aggr > .7f && straight && Random.value < dt * .12f) { c.whSt = 1; c.whPeak = Random.Range(22f, 40f) * Mathf.Deg2Rad; c.whHold = Random.Range(1.5f, 4f); c.whT = 0; }
            if (c.whSt > 0)
            {
                c.whT += dt; if (!straight && c.whSt < 3) { c.whSt = 3; c.whT = 0; }
                if (c.whSt == 1) { c.whA = Mathf.SmoothStep(0, c.whPeak, c.whT / .7f); if (c.whT >= .7f) { c.whSt = 2; c.whT = 0; } }
                else if (c.whSt == 2) { c.whA = c.whPeak + Mathf.Sin(c.whT * 2.3f) * .035f; if (c.whT >= c.whHold) { c.whSt = 3; c.whT = 0; } }
                else { c.whA = Mathf.MoveTowards(c.whA, 0, dt * c.whPeak / .6f); if (c.whA <= 0) { c.whSt = 0; c.whA = 0; } }
            }
            float wb = mx.Wheelbase, lv = mx.LevelPitch * Mathf.Deg2Rad; var rotN = FBDrive.CarRot(lv, c.yaw, c.roll); var rotW = FBDrive.CarRot(lv - c.whA, c.yaw, c.roll); var pr = new Vector3(0, 0, -wb / 2);
            var shift = rotN * pr - rotW * pr; car.transform.SetPositionAndRotation(pos + shift - Vector3.up * mx.LevelBottom * FBMxBike.S, rotW);   // r49i: nivelada, rodas no chão
            float st = Mathf.Clamp(Mathf.Atan(wb * k) * Mathf.Rad2Deg / Mathf.Max(5, mx.SteerDeg(Mathf.Abs(sv) / 1.2f)), -1, 1);
            if ((pos - CamPos).sqrMagnitude < 80 * 80) mx.RemoteState(sv, st, c.acc > .3f || c.whSt > 0 ? 1 : c.acc < -.5f ? -1 : .2f, c.whA, dt); else mx.RemoteStop();
        }
        // r45b (pedido: física completa do MX também nas motos NPC): perto da câmera (< 55 m; até 2 motos, celular 1) a moto do trânsito
        // liga o corpo rígido do MX (torque, marchas, aderência, grau por torque, queda) e um piloto IA a conduz atrás do "fantasma" da faixa
        // (o carro lógico do trânsito continua decidindo rota, IDM, semáforo e reserva de cruzamento):
        //  · esterço = perseguição pura num ponto da faixa 6–16 m à frente; acelerador/freio = P na velocidade do fantasma + distância até ele;
        //  · o fantasma não foge: se a moto ficou para trás ele espera (v limitada à da moto);
        //  · motoqueiro agressivo em reta livre puxa o grau pelo torque do MX, com o piloto preditivo do racha (35–80 km/h);
        //  · caiu: o piloto desce, levanta e vira pedestre; a moto fica na rua. Longe (> 85 m) ou fora do "drive" volta ao cinemático (MotoPose).
        public static int MotoPhysMax => FBQuality.Mobile ? 1 : 2; public static int MotoPhysOn, MotoPhysUps, MotoCrashes, MotoPhysWh; public static float MotoPhysLagMax, MotoPhysLatMax, MotoPhysFrames, MotoPhysWhMax;
        // motorista some longe (desenho). r45e: na moto o piloto e a garupa ficam até a distância em que a própria moto some (FBCarCull) —
        //  antes o piloto sumia a 110 m e a moto (e a garupa) continuavam visíveis até 260 m: "moto andando sozinha"
        static void ActorVis(Car c, FBCar car, float camD)
        {
            bool hide = camD > (car.mx || car.kind == "moto" ? FBCarCull.HideDist : 110);
            if (c.actor && c.actor.gameObject.activeSelf == hide) c.actor.gameObject.SetActive(!hide);
            if (c.garupa && !c.garupaDown && c.garupa.gameObject.activeSelf == hide) c.garupa.gameObject.SetActive(!hide);
        }
        static bool MotoPhys(Car c, FBCar car, float camD, float dt)
        {
            if (c.mode == "moto_crash") return true;
            var mx = car.mx;
            if (c.phys)
            {
                if (mx.crashed) { MotoCrash(c); return true; }
                if (camD > 85 || (c.mode != "drive" && c.mode != "wait_vanish")) { PhysOff(c); return false; }
            }
            else
            {
                if (c.mode != "drive" || camD > 55 || MotoPhysOn >= MotoPhysMax || c.whSt != 0 || Mathf.Abs(c.roll) > 4 * Mathf.Deg2Rad) return false;
                car.transform.SetPositionAndRotation(c.pos + Vector3.up * (.05f - mx.LevelBottom * FBMxBike.S), FBDrive.CarRot(mx.LevelPitch * Mathf.Deg2Rad, c.yaw, 0));   // liga na posição do fantasma, de pé (r49i: nivelada), já andando
                mx.RemoteStop(); mx.SetActive(true); mx.Launch(c.fwd * c.v); c.phys = true; MotoPhysOn++; MotoPhysUps++; c.prevA = 0; c.whA = 0; c.roll = 0;
                if (!c.physIgn && c.actor) { c.physIgn = true; foreach (var a in c.actor.GetComponentsInChildren<Collider>()) foreach (var b in car.cols) if (a && b) Physics.IgnoreCollision(a, b); }
            }
            MotoPhysFrames += dt;
            var T = car.transform; var bp = T.position; float vB = mx.speedKmh / 3.6f * FBMxBike.S;
            var d = c.pos - bp; d.y = 0; float gap = Vector3.Dot(d, c.fwd), latE = (d - c.fwd * gap).magnitude;
            MotoPhysLagMax = Mathf.Max(MotoPhysLagMax, Mathf.Abs(gap)); MotoPhysLatMax = Mathf.Max(MotoPhysLatMax, latE);
            // perseguição pura no ponto da faixa (com o deslocamento lateral) à frente da moto
            float look = Mathf.Clamp(vB * .9f, 6, 16);
            var q = Ahead(c, Mathf.Max(2.5f, look - gap), out var tan); q += new Vector3(tan.z, 0, -tan.x) * c.lat;
            float ang = Vector3.SignedAngle(T.forward.Flat(), (q - bp).Flat(), Vector3.up), steer = Mathf.Clamp(ang / 22f, -1, 1);   // + = alvo à direita
            float vT = Mathf.Max(0, c.v + Mathf.Clamp(gap * .8f, -4, 6)), thr = Mathf.Clamp((vT - vB) * .35f, -1, 1);
            if (c.v < .2f && vB < 1.2f) thr = vB > .3f ? -1 : 0;                                             // parado no semáforo / atrás de alguém
            if (gap > 3) c.v = Mathf.Min(c.v, vB + .6f);                                                      // o fantasma espera a moto
            // grau pelo torque (motoqueiro agressivo, reta livre)
            float k = 0; if (c.route.Count > 0) { Ahead(c, 5f, out var t2); FBRoads.At(Seg(c.route[Mathf.Min(c.ri, c.route.Count - 1)]), c.s, out var t0); k = Vector3.SignedAngle(t0.Flat(), t2.Flat(), Vector3.up) * Mathf.Deg2Rad / 5f; }
            bool straight = Mathf.Abs(k) < .006f && c.mode == "drive" && c.gapObs > 50 && latE < 1.5f;
            if (c.whSt == 0 && c.aggr > .7f && straight && mx.speedKmh > 35 && mx.speedKmh < 70 && Random.value < dt * .12f) { c.whSt = 1; c.whPeak = Random.Range(22f, 38f) * Mathf.Deg2Rad; c.whHold = Random.Range(1.5f, 4f); c.whT = 0; MotoPhysWh++; }
            bool wh = false; float a0 = mx.wheelieAngle, rate = (a0 - c.prevA) / Mathf.Max(dt, 1e-3f); c.prevA = a0; float an = a0 + Mathf.Max(0, rate) * .3f;
            if (mx.onWheelie) MotoPhysWhMax = Mathf.Max(MotoPhysWhMax, a0);
            if (c.whSt > 0)
            {
                c.whT += dt; if ((!straight || c.whT > .7f + c.whHold || mx.speedKmh > 80) && c.whSt < 3) c.whSt = 3;
                if (c.whSt < 3) { float pk = c.whPeak * Mathf.Rad2Deg; wh = true; thr = an > pk + 10 ? -1 : an > pk ? 0 : an > pk - 12 ? .35f : 1; steer *= .35f; }
                else if (!mx.onWheelie) c.whSt = 0;
            }
            mx.Input(steer, thr, wh, false, false, false, false, dt);
            c.roll = 0; c.whA = 0;
            return true;
        }
        static void PhysOff(Car c) { c.phys = false; if (c.car && c.car.mx) { c.car.mx.SetActive(false); c.car.mx.RemoteStop(); } }
        static void MotoCrash(Car c)
        {
            MotoCrashes++; c.phys = false; var a = c.actor; var car = c.car; ReleaseRes(c); c.mode = "moto_crash"; c.modeT = 0;
            Debug.Log($"[FreeBala][trânsito] moto {car.id} caiu ({car.mx.wheelieAngle:0}° de grau a {car.mx.speedKmh:0} km/h)");
            if (!a) return; FBFall.Grab(a, car); if (c.garupa) FBFall.Grab(c.garupa, car); FBDrive.Release(a); var p = car.transform.position - car.transform.right * 1.1f; if (FBWorld.GroundY(p.x, p.y + 2, p.z, .3f, 4) is float gy) p.y = gy;
            if (!a.gameObject.activeSelf) a.gameObject.SetActive(true);
            string why = car.mx && car.mx.wheelieAngle > 45 ? "loop" : "batida";
            if (c.garupa) { var g = c.garupa; var gs = g.GetComponent<FBGarupa>(); if (gs) gs.Leave(); c.garupaDown = true; var dir = FBFall.DirFor(car, why); string gd = dir == "Left" ? "Right" : dir == "Right" ? "Left" : dir; FBFall.Begin(g, FBFall.FallPoint(car, gd), car.transform.eulerAngles.y, car.transform.forward * car.v * .8f, gd); }
            FBFall.FromBike(a, car, why);   /* r45d: cai com o clipe de queda do 171 */
        }
        static void MotoCrashTick(Car c, float dt)
        {   // levantou: vira pedestre do rolê (a moto caída fica na rua; o FBMxBike a desliga quando para)
            c.modeT += dt; c.v = 0; if (c.modeT < 3.5f || !c.actor || FBFall.Busy(c.actor)) return;
            var a = c.actor; c.actor = null; a.StopDance(true); var rn = a.GetComponent<FBRoleNpc>(); if (rn) { rn.enabled = true; rn.st = "walk"; rn.goalP = null; } FBRole.npcs.Add(a); a.SetWeaponVisible(false);
            if (c.garupa && c.garupaDown && !FBFall.Busy(c.garupa)) { var g = c.garupa; c.garupa = null; var rg = g.GetComponent<FBRoleNpc>(); if (rg) { rg.enabled = true; rg.st = "walk"; rg.goalP = null; } FBRole.npcs.Add(g); }
            c.mode = "gone";
        }
        static void BodyDyn(Car c, float dt)
        {
            var car = c.car; var L = car.lbox; float half = Mathf.Max(1.1f, L.size.z * car.scale * .36f), tr = Mathf.Max(.6f, L.size.x * car.scale * .38f);
            var f = FBDrive.Fwd(c.yaw); var l = FBDrive.Left(c.yaw); var p = c.pos; car.SetColliders(false);
            float? G(Vector3 q) => FBWorld.GroundY(q.x, p.y + 1.2f, q.z, .3f, 3);
            float? yF = G(p + f * half), yB = G(p - f * half), yL = G(p + l * tr), yR = G(p - l * tr); car.SetColliders(true);
            float acc = c.acc, yawRate = c.steer * c.v / Mathf.Max(1.5f, car.wheelbase);
            float gP = yF != null && yB != null ? Mathf.Atan2(yB.Value - yF.Value, 2 * half) : 0, gR = yL != null && yR != null ? Mathf.Atan2(yL.Value - yR.Value, 2 * tr) : 0;
            // [parceiro] fisica 31/10: pitch positivo = nariz desce (Euler X): freando (acc<0) mergulha ate 0,045 rad, acelerando agacha a traseira ate 0,03 rad
            // [parceiro] fisica 31/10: CarRot nega o roll (c.roll>0 baixa o lado direito); curva a esquerda (steer>0) rola para fora, como o FBCarPhys.Body
            float wantP = gP + Mathf.Clamp(-acc * .008f, -.03f, .045f), wantR = gR + Mathf.Clamp(c.v * yawRate * .01f, -.05f, .05f); float k = Mathf.Min(1, dt * 6);
            c.pitch += (wantP - c.pitch) * k; c.roll += (wantR - c.roll) * k;
        }

        // ---- reaproveitar: longe demais ou fora da vista por muito tempo -> teleporta para outro lugar longe da câmera
        static void Recycle(Car c)
        {
            if (c.mode != "drive") return; float d = (c.pos - CamPos).magnitude;
            if (c.tag != null && c.life > c.maxLife && d > 120 && !InView(c.pos)) { Drop(c); return; }   // especial: acabou o tempo, longe e fora da vista -> sai de cena
            if (d > 290 && !InView(c.pos)) Respawn(c);
        }
        static bool InView(Vector3 p) { var cm = Camera.main; return cm && GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(cm), new Bounds(p + Vector3.up, new Vector3(5, 3, 5))); }
        // r45 (pedido: "veículos desaparecendo... só remover quando fora da visão"): sumir/teleportar SÓ longe e fora da tela. Se o carro
        // precisa sair (travado, sem manobra de meia-volta) e está à vista, ele fica parado com pisca-alerta ("wait_vanish") e sai quando
        // ninguém estiver olhando. Antes: meia-volta impossível ou 45 s travado a 60 m já teleportava na frente do jogador.
        public static int Vanished, VanishWaits; static bool CanVanish(Car c) { float d = (c.pos - CamPos).magnitude; return d > 45 && !InView(c.pos) && (c.pos - PlayerPos).magnitude > 35; }
        static void Respawn(Car c)
        {
            if (c.fugitive != null) EndChase(c, "viatura reciclada"); else if (c.chase != null) EndChase(c.chase, "fugitivo reciclado");   // r45d
            if (!CanVanish(c)) { if (c.mode != "wait_vanish") { c.mode = "wait_vanish"; VanishWaits++; } c.v = 0; c.brake = true; c.hazard = true; ReleaseRes(c); return; }
            Vanished++;
            if (!LanePoint(out int seg, out float s, false)) { Drop(c); return; }
            if (c.phys) PhysOff(c); ReleaseRes(c); if (c.spot != null && c.spot.res == c) c.spot.res = null; c.spot = null; c.mode = "drive"; c.reverse = false; c.legs.Clear(); c.blink = 0;
            c.route.Clear(); c.route.Add(seg); c.ri = 0; c.s = s; c.v = 6; c.lat = c.latT = 0; c.stuck = 0; c.overSide = 0; c.waitT = 0; c.shockT = 0; Extend(c);
            var p = FBRoads.At(Seg(seg), s, out var t); c.pos = p; c.fwd = t.Flat().normalized; c.yaw = Mathf.Atan2(-c.fwd.x, c.fwd.z);
            c.car.transform.position = p;
        }
    }
}
