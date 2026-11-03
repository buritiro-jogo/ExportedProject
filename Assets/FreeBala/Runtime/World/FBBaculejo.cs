// BACULEJO (abordagem policial) — rodada 42, refeito em ETAPAS. Pedido do usuário (27/10): "a cena de baculejo continua extremamente
// diferente da versão original. Faltam comportamentos, sequências, interações e principalmente animações corretas", com as etapas:
// NPCs reunidos conversando/fumando -> chega a viatura, PMs desembarcam e abordam -> mãos na cabeça e revista -> liberação; e variações
// (fuga com perseguição, confronto armado com morte possível).
// r44 (pedido 28/10: "policiais masculinos e femininos, civis, diferentes rigs ... a cena original também possui diálogos entre os
// policiais. Extraia e implemente esses áudios, preservando as comunicações, a sequência das ações e a sincronização com as animações"):
//  • ELENCO (r52: categorias do skins.json, FBElenco; os x171_* saíram): PMs = categoria policia, função pm_viatura; uma das duplas pode
//    ter policial MULHER; abordados = categoria civil, homens e mulheres;
//  • FALAS do 171 (Dublagens/Policial e Bandido/Enquadro, tools/re/policia171.py -> Resources/Audio/171/policia): cada PM tem UMA voz do
//    jogo (Homem1, Homem3 ou Mulher2) e fala nas etapas — chegada (rádio), abordagem ("Polícia! Levanta a mão", "Parado aí"), enquadro
//    ("Mão na cabeça e abre as pernas"), revista ("Tem droga aí né?", "Escondeu alguma coisa aí?", "E esse pacotão aqui?"), resposta do
//    abordado ("Calma aê seu polícia", "Sou trabalhador, senhor", "Pode olhar à vontade"), jogador perto ("Mantenha distância, por favor"),
//    fuga/confronto ("Parado aí, não vou falar de novo", "Larga a arma e deita no chão") e saída (rádio). Uma fala por vez (fila), o
//    seguinte só depois do fim da anterior + pausa; a boca (talking) acompanha a duração da fala.
//
// O que o 171 faz de verdade (reanálise do bytecode do BP_EnquadroPolicial, tools/re/kismet_print.py -> tools/re/out/171/*.txt,
// docs/re/171_BACULEJO.md): a cena NASCE pronta (viatura tática parada, 2 PMs + 75 % de chance de um 3.º, classe Patrulha/Tática/Elite
// sorteada, F_SetTipoIdle(2) = pose de abordagem do PM), os abordados nascem DESARMADOS (IA_DesarmarBandido) numa das DUAS poses de
// enquadro (EnquadroPolicial = mãos na nuca e pernas abertas / EnquadroPolicial2 = mãos para o alto, 50/50) com falas de enquadro
// (FalasEvento 9); quando o evento acaba ou o jogador entra num carro, os PMs entram em combate (procurado nível 2) e os bandidos FOGEM
// (IA Bandido Fugir). A revista em si (ALS_RevistandoLoop) é do sistema de procurado. Aqui a cena começa ANTES (a roda na calçada) e
// segue até o fim, com as poses e o laço de revista do próprio 171 (X171_*, mesmo uso condicionado das outras) e os nossos personagens.
//
// Etapas (st): roda -> chegada -> encosta -> desembarque -> abordagem -> enquadro -> revista -> anota -> fala -> libera -> volta -> partiu
// Variações (sorteadas no começo): fuga (o abordado corre quando o PM encosta; o PM persegue — alcançou: o fugitivo deita no chão e é
// revistado ali; senão volta) e confronto (o abordado saca uma pistola e atira nos PMs; os PMs revidam com bala de verdade — o
// FBCombat decide quem acerta; quem morre cai pelo FBDeath — e os outros abordados deitam no chão). Tiro de fora (jogador) dispersa.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public static class FBBaculejo
    {
        // ---- clipes (vetados na folha de poses r42: pés no chão, sem membro quebrado no esqueleto dos r_*)
        static bool Has(string c) => FBRegistry.I && FBRegistry.I.Clip(c);
        static string Pick(params string[] l) => l.FirstOrDefault(Has);
        static string REVISTA => Pick("X171_ALS_RevistandoLoop", "FF_REB_Policia_Revista");                      // laço de revista do 171
        static string PM_ESPERA => Pick("X171_EnquadroPolicial1_root_Policial1", "X171_PosePolicial_root_Pose3");
        const string ANOTA = "FF_REB_Policia_Multa", FALA = "NPC_Talk_Serious", MAOS = "SCR_Gunpoint_Hands_Up_Loop", SAIR = "REB_CHAR_SairCarroEsquerda";
        static readonly string[] ENQUADRO = { "X171_EnquadroPolicial", "X171_EnquadroPolicial2_root_Enquadrado2" };   // 50/50 como no BP
        static readonly string[] RODA = { "X171_055_Mocap101_SL_STATIC_B_Smoke_UE41", "X171_fumando_looped", "X171_051_Mocap101_SL_STATIC_B_WriteMobile_UE4",
            "X171_004_Mocap101_SL_IDLE_B_Armscross_UE4", "NPC_Talk_Serious", "NPC_Talk_M", "NPC_Stand_Talk", "X171_Idle_Male", "X171_053_Mocap101_SL_STATIC_B_Watch1_UE4" };
        const string DEITA = "X171_Rendido_Start", DEITADO = "X171_Rendido_Loop";
        static readonly string[] VIATURAS = { "ViaturaPMAzul", "ViaturaBM" }, FUZIS = { "ia2", "parafal", "m4", "ar15" }, PISTOLAS = { "g18", "pistola_308", "38" };

        // ---- r44: falas do 171 (uma por vez, na voz fixa de cada participante)
        static class Voz
        {
            class Line { public string c, t; public float d; }
            static Dictionary<string, Dictionary<string, List<Line>>> cats; static bool tried; public static int Said; public static string Last;
            static readonly List<(float at, Act who, string cat, string rx)> q = new(); static float busyT;
            static void Load()
            {
                if (tried) return; tried = true; var ta = Resources.Load<TextAsset>("Audio/171/policia/policia"); if (!ta) return; cats = new();
                foreach (var kv in Json.O(Json.Parse(ta.text), "cats") ?? new Dictionary<string, object>())
                {
                    var byV = new Dictionary<string, List<Line>>();
                    foreach (var v in kv.Value as Dictionary<string, object> ?? new()) byV[v.Key] = (v.Value as List<object> ?? new()).Select(o => new Line { c = Json.S(o, "c"), t = Json.S(o, "t") ?? "", d = Json.F(o, "d", 1.5f) }).Where(l => l.c != null).ToList();
                    cats[kv.Key] = byV;
                }
            }
            public static bool Ready { get { Load(); return cats != null && cats.Count > 0; } }
            /// <summary>Enfileira uma fala (cat; rx = trecho do texto preferido, null = qualquer). delay a partir de agora.</summary>
            public static void Say(Act who, string cat, string rx = null, float delay = 0) { if (who?.c && Ready) q.Add((Time.time + delay, who, cat, rx)); }
            public static void Clear() { q.Clear(); busyT = 0; }
            public static void Tick()
            {
                if (q.Count == 0 || Time.time < busyT) return; int k = q.FindIndex(x => Time.time >= x.at); if (k < 0) return; var (_, who, cat, rx) = q[k]; q.RemoveAt(k);
                if (!Ok(who) || !cats.TryGetValue(cat, out var byV)) return;
                var voz = who.voz ?? "Homem1"; if (!byV.TryGetValue(voz, out var l) || l.Count == 0) l = byV.Where(kv => kv.Key.StartsWith(voz.StartsWith("M") ? "Mulher" : "Homem")).SelectMany(kv => kv.Value).ToList();
                if (l == null || l.Count == 0) return;
                var pick = rx != null ? l.Where(x => System.Text.RegularExpressions.Regex.IsMatch(x.t, rx, System.Text.RegularExpressions.RegexOptions.IgnoreCase)).ToList() : l; if (pick.Count == 0) pick = l;
                var fresh = pick.Where(x => !who.said.Contains(x.c)).ToList(); if (fresh.Count == 0) fresh = l.Where(x => !who.said.Contains(x.c)).ToList(); /* o trecho pedido já foi dito: outra fala da mesma situação */ var ln = (fresh.Count > 0 ? fresh : pick)[Random.Range(0, (fresh.Count > 0 ? fresh : pick).Count)]; who.said.Add(ln.c);
                var clip = Resources.Load<AudioClip>("Audio/171/policia/" + ln.c); if (!clip) return;
                var head = who.c.Pos + Vector3.up * 1.62f * who.c.ScaleK; FBAudio.PlayAt(clip, head, 1, 7, .02f, 0, 1.1f, FBAudio.Cat.Voice);
                who.c.talking = true; who.talkUntil = Time.time + ln.d; busyT = Time.time + ln.d + Random.Range(.25f, .55f); Said++; Last = cat + ":" + ln.t;
                Log($"fala ({who.c.skinId}, {voz}): \"{ln.t}\"");
            }
            public static void Radio(Vector3 p, bool fim) { var c = FBAudio.Clip(fim ? "p_radio_fim" : "p_radio_loop"); if (!c) c = Resources.Load<AudioClip>("Audio/171/policia/" + (fim ? "p_radio_fim" : "p_radio_loop")); if (c) FBAudio.PlayAt(c, p + Vector3.up * 1.2f, fim ? .55f : .35f, 5, 0, fim ? 0 : 2.4f); }
        }
        static string VozAbordado(FBCharacter c) { if (!c) return "Homem1"; uint h = (uint)((c.skinId ?? "").GetHashCode() * 31 + FBNpcVoz.Id(c)); return "Homem" + (1 + h % 3); }

        public class Act
        {
            public string voz; public float talkUntil; public readonly HashSet<string> said = new();
            public FBCharacter c; public FBStreetActor a; public Vector3 spot, ring; public float yaw, idleT, shotT; public string idle, pose;
            public bool spawned, fled, gone, armed, down, inPlace; public MonoBehaviour borrowed; public int ammo;
        }
        static Act frisker, cover; static readonly List<Act> sus = new(); static float afastaT;
        public static IReadOnlyList<Act> Suspects => sus; public static int Falas => Voz.Said; public static string UltimaFala => Voz.Last; public static Act Frisker => frisker; public static Act Cover => cover;
        static FBCar car; static FBTransito.Car tc; static string st; static float stT, life, quiet, talkT; static int cur, segId = -1, speaker;
        static Vector3 F, along, center, carP, carF, ringC; static float segS; static bool wall; static FBGiroflex.Bar bar;
        static string outcome; public static string Outcome => outcome; public static string ForceOutcome;   // autoteste
        static List<Vector3> path; static float pathLen, pathPos, pathV;                                       // encostar na guia
        public static bool Active => sus.Count > 0 || car || frisker != null;
        public static string State => st; public static FBCar Car => car;
        public static float Cooldown() => Random.Range(200f, 420f);
        static float Yaw(Vector3 f) => Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        static bool Ok(Act a) => a != null && a.c && a.c.alive;
        static void Pose(FBCharacter c, string clip) { if (c && clip != null && c.PlayDance(clip) && c.dance != null) c.dance.len = 1e9f; }
        static void Log(string s) => Debug.Log("[FreeBala][ruas-eventos] baculejo: " + s);

        // ================================================================ etapa 0: montar a roda (fora da vista; autoteste: perto e à vista)
        public static bool Start()
        {
            if (!FBTransito.Running || !FBRegistry.I || !FBMatch.I) return false;
            if (!VIATURAS.Any(v => FBRegistry.I.Vehicle(v) && GameData.Vehicles.ContainsKey(v))) return false;
            int n = FBQuality.Mobile ? 2 : Random.value < .5f ? 3 : 2;
            if (!Plan(n, out var pts)) { FBStreetEvents.L("baculejo: nenhum trecho de guia/calçada livre"); return false; }
            sus.Clear();
            // roda na calçada: em volta de um ponto entre a linha do muro e a guia, cada um virado para o meio
            ringC = center - F * 1.0f;
            for (int i = 0; i < n; i++)
            {
                float a = (i / (float)n) * Mathf.PI * 2 + Random.Range(-.3f, .3f); var q = ringC + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * Random.Range(.75f, 1.0f);
                if (!FBStreetEvents.Ground(ref q, center.y, .5f) || !FBStreetEvents.Free(q, .26f)) q = pts[i] - F * 1.2f;
                var ac = Recruit(q, i); if (ac == null) continue; ac.spot = pts[Mathf.Min(i, pts.Count - 1)]; ac.ring = q; sus.Add(ac);
            }
            if (sus.Count == 0) { FBStreetEvents.L("baculejo: nenhum abordado disponível"); End(true); return false; }
            var used = new HashSet<string>();
            foreach (var s in sus)
            {
                s.a = s.c.gameObject.GetOrAdd<FBStreetActor>(); s.a.Init(s.c); s.a.life = true;
                s.yaw = Yaw((ringC - s.c.Pos).Flat()); s.c.Yaw = s.yaw; s.a.faceYaw = s.yaw;
                s.idle = RODA.Where(Has).Where(x => !used.Contains(x)).OrderBy(_ => Random.value).FirstOrDefault() ?? RODA.FirstOrDefault(Has); used.Add(s.idle);
                Pose(s.c, s.idle); s.idleT = Random.Range(9f, 18f);
            }
            FBStreetEvents.NoCollide(sus.Select(a => a.c));
            outcome = ForceOutcome ?? (Random.value < .62f ? "normal" : Random.value < .58f ? "fuga" : "confronto"); ForceOutcome = null; fugaFeita = false;
            if (outcome == "confronto") { var arm = sus[Random.Range(0, sus.Count)]; arm.armed = true; }
            st = "roda"; stT = FBStreetEvents.ForceNear > 0 ? 7 : Random.Range(18f, 40f); life = 0; quiet = 0; cur = 0; speaker = 0; talkT = 0;
            FBDamageEvents.OnDamage -= OnDamage; FBDamageEvents.OnDamage += OnDamage;
            Log($"roda com {sus.Count} na calçada ({string.Join(", ", sus.Select(s => s.idle))}), desfecho sorteado: {outcome}, a {(ringC - FBStreetEvents.PlayerPos).Flat().magnitude:0} m do jogador");
            return true;
        }

        // trecho de faixa reta com guia e calçada; linha de enquadro na calçada (de cara para o muro se houver), viatura encostando na guia
        static bool Plan(int n, out List<Vector3> pts)
        {
            pts = new List<Vector3>(); var pp = FBStreetEvents.PlayerPos; bool force = FBStreetEvents.ForceNear > 0;
            var lanes = FBRoads.segs.Where(S => S.kind == 0 && !S.ring && S.len > 40).ToList(); if (lanes.Count == 0) lanes = FBRoads.segs.Where(S => S.kind == 0 && !S.ring && S.len > 26).ToList();
            if (lanes.Count == 0) return false;
            for (int k = 0; k < (force ? 600 : 200); k++)
            {
                var S = lanes[Random.Range(0, lanes.Count)]; float s = Random.Range(Mathf.Min(34f, S.len * .6f), S.len - 10f); if (s < 10) continue;
                var p = FBRoads.At(S, s, out var tan); tan = tan.Flat().normalized; float dp = (p - pp).Flat().magnitude;
                if (force ? dp < 14 || dp > 70 : dp < 18 || dp > (k < 120 ? 50 : 70)) continue;
                FBRoads.At(S, Mathf.Max(0, s - 10), out var t0); FBRoads.At(S, Mathf.Min(S.len, s + 8), out var t1); if (Vector3.Angle(t0.Flat(), t1.Flat()) > 12) continue;
                var r = new Vector3(tan.z, 0, -tan.x); float curb = -1; for (float o = .75f; o < 7.5f; o += .25f) if (!FBRoads.IsRoad(p + r * o)) { curb = o; break; }
                if (curb < 1.2f) continue;
                var cp = p + r * Mathf.Max(0, curb - 1.35f); if (!FBStreetEvents.Ground(ref cp, p.y, .5f)) continue;
                if (FBVehicles.list.Any(c => c && (c.transform.position - cp).Flat().sqrMagnitude < 10 * 10) || FBTransito.List.Any(c => c.car && (c.pos - cp).Flat().sqrMagnitude < 16 * 16)) continue;
                if (Physics.OverlapBox(cp + Vector3.up * 1.05f, new Vector3(1.05f, .6f, 2.7f), Quaternion.LookRotation(tan), FBWorld.SolidMask, QueryTriggerInteraction.Ignore).Any(c => !FBStreetEvents.Loose(c))) continue;
                var sw = p + r * (curb + 1.1f); if (!FBStreetEvents.Ground(ref sw, p.y, .6f) || FBRoads.IsRoad(sw)) continue;
                var o1 = sw + Vector3.up * 1.2f; bool hasWall = Physics.Raycast(o1, r, out var wh, 4.5f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) && wh.distance > 1.6f && Mathf.Abs(wh.normal.y) < .3f;
                Vector3 f, line; if (hasWall) { f = r; line = sw + r * (wh.distance - .62f); } else { f = -r; line = sw + r * .35f; }
                line.y = sw.y; if (!FBStreetEvents.Ground(ref line, sw.y, .5f)) continue;
                var l = new List<Vector3>(); bool ok = true;
                for (int i = 0; i < n && ok; i++) { var q = line + tan * ((i - (n - 1) / 2f) * 1.15f); if (!FBStreetEvents.Ground(ref q, line.y, .45f) || !FBStreetEvents.Free(q, .26f) || FBRoads.IsRoad(q)) ok = false; else l.Add(q); }
                if (!ok) continue;
                var ring = line - f * 1.0f; if (!FBStreetEvents.Ground(ref ring, line.y, .5f) || !FBStreetEvents.Free(ring, .5f)) continue;
                if (!FBNav.InsideNear(line, 2f)) continue;
                if (!force && k < 140 && (FBStreetEvents.Visible(line, 2) || FBStreetEvents.Visible(ring, 2))) continue;   // a roda pode nascer fora da vista
                carP = cp; carF = tan; pts = l; F = f; along = tan; center = line; wall = hasWall; segId = S.id; segS = s;
                return true;
            }
            return false;
        }
        static Act Recruit(Vector3 p, int i)
        {
            var pp = FBStreetEvents.PlayerPos; bool force = FBStreetEvents.ForceNear > 0;
            bool OkBorrow(FBCharacter o) => o && o.alive && !o.vehiclePose && (o.Pos - pp).magnitude > 30 && (force || !FBStreetEvents.Visible(o.Pos));
            if (FBSession.mode == "role")
                foreach (var o in FBRole.npcs.Where(OkBorrow).OrderBy(o => (o.Pos - p).sqrMagnitude))
                {
                    var rn = o.GetComponent<FBRoleNpc>(); if (!rn || !rn.enabled || rn.st != "walk" || rn.seat != null || (o.IsFemale && Random.value < .6f)) continue;
                    rn.st = "evento"; rn.enabled = false; o.StopDance(true); o.talking = false; FBMatch.Teleport(o, p, 0); return new Act { c = o, borrowed = rn };
                }
            var used = sus.Where(x => x.c).Select(x => x.c.skinId).ToList();
            var id = FBStreetEvents.FreeCivilian();
            if (id == null || used.Contains(id) || (GameData.Skins.Find(s => s.id == id)?.sex == "f" && Random.value < .6f))
            {
                var pool = FBNpcAnims.Roster(FBSession.skin).Where(r => FBRegistry.I.HasChar(r) && !used.Contains(r) && !FBNpcAnims.IsPolice(r)).ToList();
                if (pool.Count == 0) return null; id = pool[Random.Range(0, pool.Count)];
            }
            var c = FBStreetEvents.Spawn(id, p, 0, "abordado" + i); return c ? new Act { c = c, spawned = true } : null;
        }

        // ================================================================ andamento
        static readonly List<Act> strays = new();
        public static void Tick(float dt)
        {
            var pp = FBStreetEvents.PlayerPos; SirenTick(dt);
            for (int i = strays.Count - 1; i >= 0; i--)
            {
                var a = strays[i]; if (!a.c || !a.a) { strays.RemoveAt(i); continue; }
                if (!a.a.Arrived) continue;
                if ((a.c.Pos - pp).magnitude > 35 && !FBStreetEvents.Visible(a.c.Pos)) { FBStreetEvents.Despawn(a.c); strays.RemoveAt(i); }
                else a.a.Go(FBTransito.SidewalkPoint(a.c.Pos, 40) ?? a.c.Pos + a.c.Fwd * 20, 1.3f, 1.5f);
            }
            if (!Active) return; life += dt; stT -= dt;
            Voz.Tick(); foreach (var a in new[] { frisker, cover }.Concat(sus)) if (a?.c && a.talkUntil > 0 && Time.time > a.talkUntil) { a.c.talking = false; a.talkUntil = 0; }
            // jogador colado na cena: quem cobre manda afastar (falas de interação com o jogador do 171)
            if (Ok(cover) && (st == "enquadro" || st == "revista" || st == "anota" || st == "fala") && FBMatch.Player && FBMatch.Player.alive && (FBMatch.Player.Pos - center).Flat().magnitude < 4.2f && Time.time > afastaT)
            { afastaT = Time.time + Random.Range(7f, 11f); Voz.Say(cover, "pm_afasta", "dist|longe|afast|perto|aproxima|segura"); }
            if (car && bar != null) bar.on = st != "partiu" && st != "fim" && st != "roda";
            // tiro de fora (jogador/bot) perto da cena: dispersa (o confronto do próprio evento não conta)
            if (st != "roda" && st != "tumulto" && st != "confronto" && st != "abandono" && st != "volta" && st != "partiu" && st != "fim" && FBBotBrain.lastNoiseP is Vector3 np && Time.time - FBBotBrain.lastNoiseT < 1.5f
                && (np - center).magnitude < 45 && !IsOurs(np)) Disperse(np);
            if (st != "roda" && st != "chegada" && st != "encosta" && (frisker != null || cover != null) && (!Ok(frisker) || !Ok(cover)) && st != "tumulto" && st != "confronto" && st != "abandono" && st != "partiu" && st != "fim" && st != "volta")
            { Disperse(null); }
            switch (st)
            {
                case "roda": Roda(dt); if (stT <= 0) Chegada(); break;
                case "chegada": Roda(dt); TickChegada(); break;
                case "encosta": TickEncosta(dt); break;
                case "desembarque": if (stT <= 0) Abordagem(); break;
                case "abordagem": TickAbordagem(); break;
                case "enquadro": TickEnquadro(); break;
                case "revista": TickRevista(dt); break;
                case "anota": if (stT <= 0) { st = "fala"; stT = Random.Range(4f, 6.5f); Pose(frisker.c, FALA); } break;
                case "fala": if (stT <= 0) Release(); break;
                case "libera": if (stT <= 0) { st = "volta"; stT = 25; Board(); } break;
                case "volta":
                    if (!Ok(frisker) || !Ok(cover) || !car) { st = "abandono"; break; }
                    if (!Seated(cover) && cover.a.Arrived) SitPassenger();
                    if ((frisker.a.Arrived && Seated(cover)) || stT <= 0) Depart();
                    break;
                case "perseguicao": TickPerseguicao(dt); break;
                case "confronto": TickConfronto(dt); break;
                case "tumulto": if ((quiet -= dt) <= 0) { if (Ok(frisker) && Ok(cover) && car) { st = "volta"; stT = 20; Board(true); } else st = "abandono"; } break;
                case "abandono": if ((center - pp).magnitude > 40 && !FBStreetEvents.Visible(center, 4) && (!car || !FBStreetEvents.Visible(car.transform.position, 3))) Cleanup(true); break;
                case "partiu": if (tc == null || tc.mode == "gone" || life > 400) Cleanup(false); break;
            }
            foreach (var a in sus) if (!a.gone && a.c && a.fled && a.a && a.a.Arrived && (a.c.Pos - pp).magnitude > 35 && !FBStreetEvents.Visible(a.c.Pos)) Return(a);
            // cobertura: mira na roda; jogador colado nos abordados -> vira o fuzil para ele ("afasta!")
            if (Ok(cover) && (st == "enquadro" || st == "revista" || st == "anota" || st == "fala"))
            {
                bool close = FBMatch.Player && FBMatch.Player.alive && !FBMatch.Player.vehiclePose && sus.Any(s => s.c && (s.c.Pos - pp).Flat().magnitude < 2.6f);
                Aim(cover, true, close ? pp : null);
            }
        }
        static bool IsOurs(Vector3 p) => new[] { frisker, cover }.Concat(sus).Any(a => a?.c && (a.c.MuzzlePos - p).sqrMagnitude < 1.5f);

        // ---- etapa 1: a roda. Cada um no próprio laço (fumando, celular, braços cruzados, conversando), trocando de vez em quando sem
        // repetir o do vizinho; quem "fala" vira o olhar dos outros; o que tem arma escondida fica mais olhando em volta
        static void Roda(float dt)
        {
            if ((talkT -= dt) <= 0) { speaker = Random.Range(0, sus.Count); talkT = Random.Range(4f, 8f); }
            for (int i = 0; i < sus.Count; i++)
            {
                var s = sus[i]; if (!Ok(s) || s.fled) continue;
                if ((s.idleT -= dt) <= 0)
                {
                    var taken = sus.Where(o => o != s).Select(o => o.idle).ToHashSet();
                    var nx = RODA.Where(Has).Where(x => x != s.idle && !taken.Contains(x)).OrderBy(_ => Random.value).FirstOrDefault();
                    if (nx != null) { s.idle = nx; Pose(s.c, nx); } s.idleT = Random.Range(10f, 20f);
                }
                var sp = sus[Mathf.Clamp(speaker, 0, sus.Count - 1)];
                s.a.faceYaw = sp != s && sp.c ? Mathf.LerpAngle(Yaw((ringC - s.c.Pos).Flat()), Yaw((sp.c.Pos - s.c.Pos).Flat()), .5f) : Yaw((ringC - s.c.Pos).Flat());
                s.c.talking = sp == s;
            }
        }

        // ---- etapa 2: a viatura vem pela faixa (trânsito especial, giroflex aceso), a ~24 m sai do trânsito e encosta na guia por um
        // caminho próprio (curva suave até o ponto de parada, frenagem progressiva); motorista = PM que revista, carona = cobertura
        static void Chegada()
        {
            var vid = VIATURAS.FirstOrDefault(v => FBRegistry.I.Vehicle(v) && GameData.Vehicles.ContainsKey(v));
            // r52: o elenco tem só 3 PMs de viatura e as viaturas do trânsito ocupam os outros — os livres vêm primeiro e a dupla completa
            // com quem está longe, nunca o MESMO PM duas vezes (r52a: sobrava só a r_elaine livre e ela era motorista e carona)
            var free = FBStreetEvents.FreeIds(FBNpcAnims.Police);
            var cops = free.Concat(FBNpcAnims.Police.Where(i => FBRegistry.I.HasChar(i) && !free.Contains(i)).OrderBy(_ => Random.value)).ToList();
            // dupla: homem + (35 %) mulher — a voz feminina do 171 (Mulher2) só sai de policial mulher
            bool Fem(string id) => GameData.Skins.Find(s => s.id == id)?.sex == "f";
            var men = cops.Where(i => !Fem(i)).ToList(); var wom = cops.Where(Fem).ToList();
            if (men.Count > 0)
            {
                var a0 = men[0]; bool woman = wom.Count > 0 && (Random.value < .35f || men.Count < 2 || free.Contains(wom[0]) && !free.Contains(men[1]));
                var b0 = woman ? wom[0] : men.Count > 1 ? men[1] : a0; cops = Random.value < .5f ? new List<string> { a0, b0 } : new List<string> { b0, a0 };
            }
            var S = FBRoads.segs.FirstOrDefault(x => x.id == segId);
            if (vid == null || cops.Count == 0 || S == null) { Log("sem viatura/PM/faixa — a roda se desfaz"); Release(); return; }
            float s0 = Mathf.Max(3, segS - Random.Range(45f, 60f)); var p0 = FBRoads.At(S, s0, out var t0);
            var d = Json.O(GameData.Vehicles, vid); var pc = Json.L(d, "paint"); uint? paint = pc != null ? ((uint)(Json.Fv(pc[0]) * 255) << 16) | ((uint)(Json.Fv(pc[1]) * 255) << 8) | (uint)(Json.Fv(pc[2]) * 255) : null;
            car = FBVehicles.Make(vid, p0, Mathf.Atan2(t0.x, t0.z) * Mathf.Rad2Deg, false, () => .5f, true, paint, false); if (!car) { Release(); return; }
            car.name = "Viatura_Abordagem"; car.syncId = "evt:viatura";
            var drv = FBStreetEvents.Spawn(cops[0], p0, 0, "pm1"); var pas = FBStreetEvents.Spawn(cops.Count > 1 ? cops[1] : cops[0], p0 + Vector3.up * .1f, 0, "pm2");
            if (!drv || !pas) { Log("PM não nasceu"); Cleanup(true); return; }
            frisker = new Act { c = drv, spawned = true }; cover = new Act { c = pas, spawned = true };
            { var mv = Random.value < .5f ? new[] { "Homem1", "Homem3" } : new[] { "Homem3", "Homem1" }; int k = 0; foreach (var a in new[] { frisker, cover }) a.voz = a.c.IsFemale ? "Mulher2" : mv[k++]; }
            foreach (var s0_ in sus) if (s0_.c) s0_.voz = VozAbordado(s0_.c);
            Voz.Clear(); Voz.Radio(p0, false); Voz.Say(cover, "pm_radio", null, 1.2f);
            foreach (var a in new[] { frisker, cover }) { a.a = a.c.gameObject.GetOrAdd<FBStreetActor>(); a.a.Init(a.c); }
            tc = FBTransito.SpawnSpecial(car, drv, "viatura", Random.Range(8.5f, 10.5f), segId, s0);
            if (tc == null) { Log("viatura sem faixa"); Cleanup(true); return; }
            tc.lure = false; tc.maxLife = 60; tc.onDrop = _ => { tc = null; };
            pas.gameObject.GetOrAdd<FBPassengerSeat>().Sit(car);
            bar = FBGiroflex.Attach(car); Siren(true, 2.2f);
            st = "chegada"; stT = 40; Log($"viatura {vid} vindo pela faixa a {segS - s0:0} m da parada");
        }
        static void TickChegada()
        {
            if (!car) { Cleanup(true); return; }
            float left = tc != null && tc.ri < tc.route.Count && tc.route[tc.ri] == segId ? segS - tc.s : -1;
            if (tc != null && left >= 0 && left < 24)
            {   // sai do trânsito e encosta
                var p = car.transform.position; var f = car.transform.forward.Flat().normalized; float v = Mathf.Max(3, tc.v);
                tc.onDrop = null; FBTransito.Forget(tc); tc = null;
                float L = (carP - p).Flat().magnitude; path = new List<Vector3>();
                for (int i = 0; i <= 40; i++) { float u = i / 40f, a = 1 - u; var q = a * a * a * p + 3 * a * a * u * (p + f * L * .45f) + 3 * a * u * u * (carP - carF * L * .4f) + u * u * u * carP; path.Add(q); }
                pathLen = 0; for (int i = 1; i < path.Count; i++) pathLen += (path[i] - path[i - 1]).Flat().magnitude;
                pathPos = 0; pathV = v; st = "encosta";
                foreach (var s in sus.Where(Ok)) { s.c.talking = false; s.a.faceYaw = Yaw((car.transform.position - s.c.Pos).Flat()); }   // a roda vira para a viatura
                Log("encostando na guia"); return;
            }
            if (stT <= 0 || tc == null)
            {   // a viatura se perdeu no trânsito: encosta direto se ninguém estiver olhando, senão desfaz
                if (!FBStreetEvents.Visible(carP, 3)) { if (tc != null) { tc.onDrop = null; FBTransito.Forget(tc); tc = null; } car.transform.SetPositionAndRotation(carP, FBDrive.CarRot(0, Mathf.Atan2(-carF.x, carF.z), 0)); car.yaw = Mathf.Atan2(-carF.x, carF.z); Desembarque(); }
                else { Log("viatura não chegou"); Cleanup(false); }
            }
        }
        static void TickEncosta(float dt)
        {
            if (!car || path == null) { Cleanup(true); return; }
            // freia a 2,2 m/s² até parar. r43: o teto zerava a 20 cm do fim (rest − 0,2) e a chegada exigia 2 cm — a viatura parava para
            // sempre a 20 cm do ponto (autoteste: fuga/confronto presos em "encosta" até o fim; o normal só saiu porque dispersou).
            // Agora o teto vai a zero NO ponto e há um mínimo de 0,25 m/s até lá (a última fração de segundo é o "assentar" do carro).
            float rest = pathLen - pathPos; float vmax = Mathf.Sqrt(2 * 2.2f * Mathf.Max(0, rest));
            pathV = Mathf.Max(Mathf.Min(pathV, vmax), .25f); pathPos = Mathf.Min(pathLen, pathPos + pathV * dt);
            float acc = 0; Vector3 pos = path[^1], dir = carF;
            for (int i = 1; i < path.Count; i++) { float seg = (path[i] - path[i - 1]).Flat().magnitude; if (acc + seg >= pathPos) { float u = seg > 1e-4f ? (pathPos - acc) / seg : 0; pos = Vector3.Lerp(path[i - 1], path[i], u); dir = (path[i] - path[i - 1]).Flat().normalized; break; } acc += seg; }
            var gp = pos; FBStreetEvents.Ground(ref gp, pos.y, 1.2f); float yw = Mathf.Atan2(-dir.x, dir.z);
            car.transform.SetPositionAndRotation(new Vector3(pos.x, gp.y, pos.z), FBDrive.CarRot(0, yw, 0)); car.yaw = yw;
            if (pathPos >= pathLen - .02f) Desembarque();
        }

        // ---- etapa 3: desembarque — portas abrem, motorista sai pelo clipe de descer do carro (REB), carona desce do outro lado;
        // sacam (cobertura: fuzil; revista: pistola) e vão para a roda já mirando
        public static int SaiuClipe;
        static void Desembarque()
        {
            st = "desembarque"; stT = 2.1f;
            if (FBDoors.HasPart(car, 1 | 2)) FBDoors.SetMask(car, 1 | 2);
            var fdp = frisker?.c ? frisker.c.GetComponent<FBDriverPose>() : null;
            if (fdp && FBDrive.StartCarOut(frisker.c, car, false))
            {   /* r45d: motorista sai pelo clipe do 171 com o caminho porta<->banco (FBAnimPaths) em vez de teleportar para a porta */
                stT = Mathf.Max(stT, fdp.transLen + .3f); SaiuClipe++;
                fdp.onOut = (ch, cr) => { FBDrive.OutPoint(ch, cr, out var at, out var yw); FBDrive.Release(ch); FBMatch.Teleport(ch, at, yw); var c2 = ch.GetComponent<CharacterController>(); if (c2) c2.enabled = true; };
            }
            else if (frisker?.c) { FBDrive.Release(frisker.c); var dp = DoorPoint(true); FBMatch.Teleport(frisker.c, dp, Yaw(-carF) + 90); var cc = frisker.c.GetComponent<CharacterController>(); if (cc) cc.enabled = true; if (Has(SAIR)) frisker.c.PlayDance(SAIR); }
            if (cover?.c) { var ps = cover.c.GetComponent<FBPassengerSeat>(); if (ps) ps.Leave(); FBMatch.Teleport(cover.c, DoorPoint(false), Yaw(-carF) - 90); }
            Siren(false); Log("desembarque");
        }
        static void Abordagem()
        {
            Give(cover, FUZIS); Give(frisker, PISTOLAS);
            foreach (var a in new[] { frisker, cover }) { if (!a?.c) continue; a.c.StopDance(true); a.c.combat = true; }
            var near = sus.Where(Ok).Select(s => s.c.Pos).DefaultIfEmpty(ringC).Aggregate(Vector3.zero, (x, y) => x + y) / Mathf.Max(1, sus.Count(Ok));
            // cobertura fica a ~3,5 m na diagonal (vê todos); quem revista chega pela frente da roda
            var cv = center - F * 3.4f + along * (Random.value < .5f ? 1.6f : -1.6f); FBStreetEvents.Ground(ref cv, center.y, .8f);
            cover.spot = cv; cover.a.Go(cv, 1.7f, .35f); Aim(cover, true, near + Vector3.up * .2f);
            frisker.spot = near - F * 2.2f; frisker.a.Go(frisker.spot, 1.6f, .4f); Aim(frisker, true, near);
            // a roda reage: para, vira para os PMs e levanta as mãos (quem vai fugir hesita)
            foreach (var s in sus.Where(Ok)) { s.c.talking = false; s.c.StopDance(true); s.a.faceYaw = Yaw((frisker.c.Pos - s.c.Pos).Flat()); Pose(s.c, Has(MAOS) ? MAOS : ENQUADRO[1]); s.inPlace = false; }
            st = "abordagem"; stT = 3.5f; Log("abordagem");
            Voz.Say(frisker, "pm_render", "pol[ií]cia|levanta|parado|m[aã]o"); Voz.Say(frisker, "pm_enquadro", "m[aã]o na cabe|abre as pernas|parado|de boa", .3f);
            var s1 = sus.FirstOrDefault(s => Ok(s) && !s.c.IsFemale); if (s1 != null) Voz.Say(s1, "ab_enquadro", "calma|trabalhador|tranquil", .6f);
        }
        static void TickAbordagem()
        {
            if (!Ok(frisker) || !Ok(cover)) return;
            var near = sus.Where(s => Ok(s) && !s.fled).Select(s => s.c.Pos).DefaultIfEmpty(ringC).Aggregate(Vector3.zero, (x, y) => x + y) / Mathf.Max(1, sus.Count(s => Ok(s) && !s.fled));
            Aim(frisker, true, near); if (cover.a.Arrived) Aim(cover, true, near);
            if (outcome == "confronto" && stT < 1.5f) { Confronto(); return; }
            if (stT > 0) return;
            // vão para o muro (ou de frente para a viatura) e assumem a posição de enquadro do 171 (metade com cada pose)
            int i = 0;
            foreach (var s in sus.Where(s => Ok(s) && !s.fled))
            {
                s.c.StopDance(true); var g = s.spot; var ss = s; s.pose = ENQUADRO[i++ % 2]; if (!Has(s.pose)) s.pose = ENQUADRO.FirstOrDefault(Has) ?? "FF_REB_Rendido";
                s.a.Go(g, 1.15f, .15f); s.a.onArrive = () => { ss.a.faceYaw = Yaw(F); ss.c.Yaw = Yaw(F); Pose(ss.c, ss.pose); ss.inPlace = true; };
            }
            st = "enquadro"; stT = 12;
        }
        static void TickEnquadro()
        {
            var live = sus.Where(s => Ok(s) && !s.fled).ToList();
            if (live.Count == 0) { Release(); return; }
            if (live.All(s => s.inPlace) || stT <= 0)
            {
                foreach (var s in live.Where(s => !s.inPlace)) { FBMatch.Teleport(s.c, s.spot, Yaw(F)); s.a.Stop(); Pose(s.c, s.pose); s.inPlace = true; }
                Aim(frisker, false); frisker.c.SetWeaponVisible(false); frisker.c.combat = false;               // guarda a arma para revistar
                cur = sus.IndexOf(live[0]); GoFrisk(); st = "revista";
            }
        }

        // ---- etapa 4: revista, um por vez, PM a 0,44 m atrás no laço do 171; a cobertura mira a roda
        static void GoFrisk()
        {
            var s = sus[cur]; frisker.c.StopDance(true); frisker.a.Go(s.c.Pos - F * .44f, 1.1f, .12f); stT = Random.Range(7f, 10f);
            Voz.Say(frisker, "pm_enquadro", Random.value < .15f && !frisker.said.Any(x => x.Contains("pm_enquadro")) ? "pacot" : "droga|escondeu|BO|quieto", 1.6f);
            if (s.c && !s.c.IsFemale) Voz.Say(s, "ab_enquadro", "nada|olhar|n[aã]o [eé] meu|bunda|sozinho|uso", 2.4f);
        }
        static void TickRevista(float dt)
        {
            var s = cur < sus.Count ? sus[cur] : null;
            if (s == null || !Ok(s) || s.fled) { NextSuspect(); return; }
            var at = s.c.Pos - F * .44f;
            if (!frisker.a.Arrived && stT < -6) { if (!FBStreetEvents.Visible(frisker.c.Pos, .6f) || (frisker.c.Pos - at).Flat().magnitude < .8f) { FBMatch.Teleport(frisker.c, at, Yaw(F)); frisker.a.Stop(); } else { NextSuspect(); return; } }
            if (!frisker.a.Arrived || (frisker.c.Pos - at).Flat().magnitude > .25f)
            {
                if (frisker.c.dance != null && (frisker.c.Pos - at).Flat().magnitude > .3f) { frisker.c.StopDance(true); frisker.a.Go(at, 1.1f, .12f); }
                if (frisker.a.Arrived) { frisker.c.Yaw = Yaw(F); frisker.a.faceYaw = Yaw(F); Pose(frisker.c, REVISTA); }
                return;
            }
            // r43: só UMA fuga por cena (autoteste: o alcançado voltava à fila com fled = false e a regra disparava de novo — 15 fugas seguidas)
            if (outcome == "fuga" && !fugaFeita && sus.All(x => !x.fled) && stT < 5 && sus.Count(Ok) > 0) { var f = sus.Where(x => Ok(x) && x != s).DefaultIfEmpty(s).First(); Flee(f); return; }
            if (outcome == "confronto" && stT < 4) { Confronto(); return; }
            if (stT <= 0) NextSuspect();
        }
        static void NextSuspect()
        {
            cur++; while (cur < sus.Count && (!Ok(sus[cur]) || sus[cur].fled || sus[cur].down)) cur++;
            if (cur < sus.Count) { GoFrisk(); return; }
            st = "anota"; stT = Random.Range(6f, 9f); frisker.c.StopDance(true); Voz.Say(frisker, "pm_enquadro", "de boa|n[oó]ia|vagabundo", .8f);
            var back = center - F * 1.5f + along * Random.Range(-.6f, .6f);
            if (FBStreetEvents.Ground(ref back, center.y, .5f) && FBStreetEvents.Free(back, .26f)) { frisker.a.Go(back, 1f, .15f); frisker.a.onArrive = () => { frisker.a.faceYaw = Yaw(F); Pose(frisker.c, ANOTA); }; }
            else Pose(frisker.c, ANOTA);
        }

        // ---- variação: fuga — corre para longe da viatura; o PM da revista persegue a pé (sprint); alcançou (< 1,4 m): o fugitivo deita no
        // chão (rendido do 171) e é revistado ali; perdeu de vista ou 14 s: volta e segue a revista dos outros (que levantaram as mãos)
        static Act runner; static float chaseT; static bool fugaFeita;
        static void Flee(Act s)
        {
            runner = s; s.fled = true; fugaFeita = true; s.c.StopDance(true); s.a.life = s.a.flatFeet = false; s.c.fleeing = true;
            var away = (s.c.Pos - (car ? car.transform.position : frisker.c.Pos)).Flat(); away = (away.sqrMagnitude > .01f ? away.normalized : along) + along * (Random.value < .5f ? 1 : -1);
            var g = FBNav.RandomPoint(s.c.Pos + away.normalized * 60, 12) ?? s.c.Pos + away.normalized * 45; s.a.run = true; s.a.Go(g, 5.0f, 1.2f);
            frisker.c.StopDance(true); frisker.c.SetWeaponVisible(true); frisker.c.combat = true; frisker.a.run = true;
            foreach (var o in sus.Where(x => x != s && Ok(x) && !x.fled)) Pose(o.c, Has(MAOS) ? MAOS : o.pose);
            st = "perseguicao"; chaseT = 0; Log("abordado correu — perseguição"); Voz.Clear(); Voz.Say(frisker, "pm_render", "parado|pinote|n[aã]o vou falar"); Voz.Say(cover, "pm_radio", null, 1.5f);
        }
        static void TickPerseguicao(float dt)
        {
            chaseT += dt; var r = runner;
            if (r == null || !Ok(r)) { BackToFrisk(); return; }
            if (r.down)
            {   // deitado: PM revista ali e volta
                if (chaseT > 7) { frisker.c.StopDance(true); r.c.StopDance(true); r.fled = false; r.down = false; r.a.Go(r.spot, 1.1f, .15f); var rr = r; r.a.onArrive = () => { rr.a.faceYaw = Yaw(F); Pose(rr.c, rr.pose ?? ENQUADRO[0]); }; BackToFrisk(); }
                return;
            }
            float d = (r.c.Pos - frisker.c.Pos).Flat().magnitude;
            frisker.a.Go(r.c.Pos, d > 3 ? 5.4f : 3.5f, 1.0f); Aim(frisker, d < 9, r.c.Pos + Vector3.up * .2f);
            // r43b: o PM estava revistando ao lado — "alcançado" 0,1 s depois de correr (autoteste). Vantagem de 2,5 s antes de poder alcançar
            if (d < 1.4f && chaseT > 2.5f)
            {   // alcançou: rendido no chão
                r.a.Stop(); r.a.run = false; r.c.fleeing = false; r.down = true; r.c.PlayDance(Has(DEITA) ? DEITA : MAOS); r.a.faceYaw = Yaw((r.c.Pos - frisker.c.Pos).Flat());
                frisker.a.Stop(); frisker.a.run = false; Aim(frisker, false); chaseT = 0; Log("fugitivo alcançado — rendido no chão"); Voz.Say(frisker, "pm_render", "deita|ch[aã]o|perdeu");
                return;
            }
            if (chaseT > 14 || d > 28) { r.gone = false; Log("fugitivo escapou"); BackToFrisk(); }
        }
        static void BackToFrisk()
        {
            runner = null; frisker.a.run = false; frisker.c.SetWeaponVisible(false); frisker.c.combat = false; Aim(frisker, false);
            foreach (var o in sus.Where(x => Ok(x) && !x.fled && !x.down)) Pose(o.c, o.pose ?? ENQUADRO[0]);
            cur = -1; NextSuspect(); if (st != "anota") st = "revista";
        }

        // ---- variação: confronto — o abordado armado saca (pistola), vira e atira nos PMs; os PMs revidam com bala de verdade; os outros
        // abordados se jogam no chão. Termina quando o atirador cai (FBDeath) ou foge; PM ferido = a cena desmancha
        static Act shooter;
        static void Confronto()
        {
            shooter = sus.FirstOrDefault(s => s.armed && Ok(s)) ?? sus.FirstOrDefault(Ok); if (shooter == null) { Release(); return; }
            shooter.fled = true; shooter.c.StopDance(true); shooter.a.Stop(); shooter.a.life = shooter.a.flatFeet = false; shooter.c.civ = false;
            var wu = shooter.c.GetComponent<FBWeaponUser>(); if (wu) { wu.Give("38"); int sl = System.Array.FindIndex(wu.slots, x => x != null && x.id == "38"); wu.Equip(sl >= 0 ? sl : 0, true); }
            shooter.c.SetWeaponVisible(true); shooter.c.combat = true; shooter.shotT = Random.Range(.5f, .9f); shooter.ammo = Random.Range(4, 7);
            foreach (var o in sus.Where(x => x != shooter && Ok(x) && !x.fled)) { o.c.StopDance(true); o.down = true; o.c.PlayDance(Has(DEITA) ? DEITA : MAOS); }
            foreach (var a in new[] { frisker, cover }) if (Ok(a)) { a.c.StopDance(true); a.c.SetWeaponVisible(true); a.c.combat = true; a.a.Stop(); a.shotT = Random.Range(.25f, .6f); }
            st = "confronto"; stT = 18; Log($"CONFRONTO: {shooter.c.skinId} sacou"); Voz.Clear(); Voz.Say(cover, "pm_render", "larga a arma|abaixa a arma|deita"); Voz.Say(frisker, "pm_render", "atirar|ch[aã]o|encosta", .4f);
        }
        static void TickConfronto(float dt)
        {
            bool sAlive = Ok(shooter);
            if (sAlive)
            {   // atirador: mira no PM mais perto, dispara com mira ruim, recua de lado; acabou a munição: corre
                var tgt = new[] { frisker, cover }.Where(Ok).OrderBy(a => (a.c.Pos - shooter.c.Pos).sqrMagnitude).FirstOrDefault();
                if (tgt != null)
                {
                    Aim(shooter, true, tgt.c.Pos + Vector3.up * .25f); shooter.a.faceYaw = Yaw((tgt.c.Pos - shooter.c.Pos).Flat());
                    if ((shooter.shotT -= dt) <= 0 && shooter.ammo > 0) { Shoot(shooter, tgt.c, 9); shooter.ammo--; shooter.shotT = Random.Range(.35f, .8f); }
                    if (shooter.ammo <= 0 && shooter.a.Arrived) { var away = (shooter.c.Pos - tgt.c.Pos).Flat().normalized; shooter.a.run = true; shooter.a.Go(FBNav.RandomPoint(shooter.c.Pos + away * 40, 10) ?? shooter.c.Pos + away * 30, 5f, 1.5f); }
                }
            }
            foreach (var a in new[] { frisker, cover })
            {
                if (!Ok(a)) continue;
                if (sAlive) { Aim(a, true, shooter.c.Pos + Vector3.up * .2f); a.a.faceYaw = Yaw((shooter.c.Pos - a.c.Pos).Flat()); if ((a.shotT -= dt) <= 0) { Shoot(a, shooter.c, 3.5f); a.shotT = a == cover ? Random.Range(.12f, .2f) : Random.Range(.3f, .55f); } }
                else Aim(a, true, shooter != null && shooter.c ? shooter.c.Pos : center);
            }
            if (!sAlive || stT <= 0 || (shooter != null && shooter.c && (shooter.c.Pos - center).Flat().magnitude > 35))
            {
                Log(sAlive ? "confronto terminou (atirador fugiu)" : "confronto terminou (atirador abatido)");
                foreach (var a in new[] { frisker, cover }) if (Ok(a)) { a.c.shooting = false; }
                st = "tumulto"; quiet = Random.Range(10f, 14f);                                           // ficam guardando a cena e saem
            }
        }
        static void Shoot(Act a, FBCharacter t, float spreadDeg)
        {
            var wu = a.c.GetComponent<FBWeaponUser>(); if (!wu || wu.Cur == null || !t) return;
            var tp = t.Chest ? t.Chest.position : t.Pos + Vector3.up * 1.2f * t.ScaleK; a.c.aimPoint = tp; a.c.hasAim = a.c.aiming = true;
            var mz = a.c.MuzzlePos; var dir = FBBotSense.Scatter((tp - mz).normalized, spreadDeg);
            wu.Fire(mz, dir, new FBRecoil.State { aiming = true, zoom = 1, grip = 1 }, out _, 0); a.c.lastShot = 0; a.c.shooting = true;
        }

        // ---- etapa 5: liberação — abaixam as mãos e seguem cada um para um lado (alguns ainda ficam conversando um pouco); PMs embarcam
        static void Release()
        {
            st = "libera"; stT = 2.2f; if (frisker?.c) frisker.c.StopDance(true); Aim(cover, false); Aim(frisker, false);
            int i = 0;
            foreach (var s in sus.Where(x => Ok(x) && !x.fled))
            {
                s.c.StopDance(true); s.down = false; s.a.life = false; s.a.flatFeet = false; var dir = along * (i++ % 2 == 0 ? 1 : -1);
                var g = FBTransito.SidewalkPoint(s.c.Pos + dir * 45, 15) ?? s.c.Pos + dir * 30;
                if (s.borrowed is FBRoleNpc rn) { Return(s); rn.goalP = g; rn.stT = Random.Range(12f, 20f); }
                else if (s.borrowed) Return(s);
                else { s.a.Go(g, Random.Range(1.2f, 1.45f), 1.2f); s.gone = true; strays.Add(s); }
            }
            if (frisker == null) { st = "fim"; Cleanup(false); }
        }
        static Vector3 DoorPoint(bool driver)
        {
            var o = car.rigOut ?? new Vector3(-1.6f, 0, 0); if (!driver) o = new Vector3(-o.x, o.y, o.z);
            var p = car.L2W(o); FBStreetEvents.Ground(ref p, car.transform.position.y, .8f); return p;
        }
        static void Board(bool hurry = false)
        {
            if (!car) { Cleanup(false); return; }
            foreach (var a in new[] { frisker, cover }) if (a?.c) { a.c.StopDance(true); Aim(a, false); a.c.SetWeaponVisible(false); a.a.run = hurry; a.a.Go(DoorPoint(a == frisker), hurry ? 3.6f : 1.35f, .35f); }
        }
        static bool Seated(Act a) => a != null && a.c && (a.c.vehiclePose || !a.c.alive);
        static void SitPassenger() { if (!cover?.c || !car) return; cover.c.aiming = cover.c.hasAim = false; cover.c.gameObject.GetOrAdd<FBPassengerSeat>().Sit(car); cover.a.enabled = false; }
        static void Depart()
        {
            if (!car) { Cleanup(false); return; }
            if (FBDoors.HasPart(car, 1 | 2)) FBDoors.SetMask(car, 0, true);
            if (cover?.c && !cover.c.vehiclePose && cover.c.alive) SitPassenger();
            if (frisker?.c && frisker.c.alive)
            {
                frisker.a.enabled = false; frisker.c.StopDance(true);
                tc = FBTransito.LeaveCurb(car, frisker.c, "viatura", Random.Range(7f, 9.5f));
                if (tc != null) { tc.maxLife = 60; tc.onDrop = _ => { tc = null; Cleanup(false); }; }
            }
            st = tc != null ? "partiu" : "abandono"; stT = 0; Log($"viatura saindo ({(tc != null ? "trânsito" : "sem faixa")})"); if (car) Voz.Radio(car.transform.position, true);
        }
        static void Disperse(Vector3? from)
        {
            if (st == "tumulto" || st == "volta" || st == "partiu" || st == "fim" || st == "confronto") return;
            st = "tumulto"; quiet = Random.Range(6f, 9f);
            foreach (var s in sus.Where(x => Ok(x) && !x.fled))
            {
                s.fled = true; s.c.StopDance(true); s.a.life = s.a.flatFeet = false; s.c.fleeing = true;
                var away = from is Vector3 f ? (s.c.Pos - f).Flat().normalized : along; s.a.run = true; s.a.Go(FBNav.RandomPoint(s.c.Pos + away * 45, 12) ?? s.c.Pos + away * 35, 5f, 1.5f);
            }
            if (frisker?.c) { frisker.c.StopDance(true); frisker.a.Stop(); }
            if (cover?.c && from is Vector3 f2) { cover.c.combat = true; Aim(cover, true, f2); }
            Log("dispersou (tiro/ferido)");
        }
        static void OnDamage(FBDamage d)
        {
            if (!Active || !d.victim || st == "confronto") return;
            bool ours = d.victim == frisker?.c || d.victim == cover?.c || sus.Any(s => s.c == d.victim);
            if (ours && !(d.source && IsOursChar(d.source))) Disperse(d.source ? d.source.Pos : d.victim.Pos);
        }
        static bool IsOursChar(FBCharacter c) => c == frisker?.c || c == cover?.c || sus.Any(s => s.c == c);
        static void Aim(Act a, bool on, Vector3? at = null)
        {
            var c = a?.c; if (!c) return; c.aiming = on; c.hasAim = on; if (!on) return;
            var t = at ?? sus.Where(s => s.c && !s.fled).Select(s => s.c.Pos).DefaultIfEmpty(center).Aggregate(Vector3.zero, (x, y) => x + y) / Mathf.Max(1, sus.Count(s => s.c && !s.fled));
            c.aimPoint = t + Vector3.up * 1.15f; if (a.a && a.a.Arrived) a.a.faceYaw = Yaw((t - c.Pos).Flat());
        }
        static void Give(Act a, string[] ids)
        {
            if (!a?.c) return; var wu = a.c.GetComponent<FBWeaponUser>(); var g = ids.FirstOrDefault(x => GameData.Weapons.ContainsKey(x) && FBRegistry.I.Weapon(x)); if (!wu || g == null) return;
            wu.Give(g); int sl = System.Array.FindIndex(wu.slots, x => x != null && x.id == g); wu.Equip(sl >= 0 ? sl : 0, true); a.c.SetWeaponVisible(true);
        }
        static void Return(Act a)
        {
            a.gone = true; if (!a.c) return; a.c.fleeing = false; a.c.surrender = false; a.c.talking = false;
            if (a.a) Object.Destroy(a.a);
            if (a.spawned) { FBStreetEvents.Despawn(a.c); return; }
            if (a.borrowed is FBRoleNpc rn) { rn.st = "walk"; rn.goalP = null; rn.stT = 0; rn.enabled = true; }
            else if (a.borrowed) a.borrowed.enabled = true;
        }
        // sirene da viatura: fonte 3D própria (FBSiren, sons do 171) — r45: antes era a acústica do paredão, ouvida "do lado" mesmo longe.
        // Chegada: modo rápido curto; saída: padrão ~9 s sumindo no último 1,2 s. O giroflex (bar) continua aceso a abordagem inteira.
        static float sirenT; const float SIREN_VOL = .85f;
        static void Siren(bool on, float secs = 9f)
        {
            if (!on || !car) { if (car) FBSiren.Set(car, 0); sirenT = 0; return; }
            FBSiren.Set(car, secs < 4 ? 2 : 1, SIREN_VOL); sirenT = secs;
        }
        static void SirenTick(float dt) { if (sirenT <= 0) return; if (!car) { sirenT = 0; return; } sirenT -= dt; FBSiren.Volume(car, SIREN_VOL * Mathf.Clamp01(sirenT / 1.2f)); if (sirenT <= 0) Siren(false); }
        static void Cleanup(bool now)
        {
            Siren(false);
            if (st == "fim" && !now) return; st = "fim";
            foreach (var a in sus) if (!a.gone)
                {
                    if (now || !a.c || a.borrowed || !a.c.alive || !FBStreetEvents.Visible(a.c.Pos)) Return(a);
                    else { a.c.StopDance(true); a.a.life = a.a.flatFeet = false; a.a.Go(FBTransito.SidewalkPoint(a.c.Pos, 40) ?? a.c.Pos, 1.3f, 1.5f); a.gone = true; strays.Add(a); }
                }
            if (now) { foreach (var a in strays) if (a.c) FBStreetEvents.Despawn(a.c); strays.Clear(); }
            foreach (var a in new[] { frisker, cover }) if (a?.c && a.spawned) FBStreetEvents.Despawn(a.c);
            if (tc != null) { tc.onDrop = null; FBTransito.Forget(tc); tc = null; }
            FBGiroflex.Detach(bar); bar = null;
            if (car) { FBVehicles.list.Remove(car); Object.Destroy(car.gameObject); } car = null;
            frisker = cover = null; runner = shooter = null; fugaFeita = false; sus.Clear(); path = null; FBDamageEvents.OnDamage -= OnDamage; Voz.Clear();
        }
        public static void End(bool now) { if (Active) Cleanup(true); st = null; }
    }
}
