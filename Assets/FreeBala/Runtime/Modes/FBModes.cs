// Modos de jogo (porte 1:1 do src/modes.js): TÁTICO (domínio dos pontos estratégicos gerados pela navmesh), CLÁSSICO (mata-mata 40
// abates / 8 min), 4V4 ZONA (rodadas estilo Free Fire/Clash Squad: compra 15 s, zona que fecha em 4 fases, primeiro a 4), FARINHA ×
// BURITI (Polícia × Ladrão: capturar o item na base inimiga), TODOS × TODOS (25 abates / 10 min), TREINO (estande com alvos de papel que
// tombam, alvo móvel e bonecos), BATTLE ROYALE (avião/paraquedas, loot, zona do FF, vida 200) e DE ROLÊ (livre).
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public abstract class FBMode
    {
        protected FBMatch G; public string name; public bool respawn, rounds, econ, ffa, br, ffDamage, arsenal, role; public float autoRespawn;
        protected FBMode(FBMatch g) { G = g; }
        public virtual void Setup() { }
        public virtual void Update(float dt) { }
        public virtual void Hud() { }
        public virtual void OnKill(FBCharacter victim, FBCharacter killer, bool head) { }
        public virtual Vector3? Goal(FBCharacter b) => null;
        public virtual bool Urgent(FBCharacter b) => false;
        public virtual Vector3? UrgentGoal(FBCharacter b) => null;
        public virtual Vector3 SpawnPoint() => G.teamLine[FBSession.team] != null ? G.teamLine[FBSession.team].spots[0] : G.TeamSpawns(FBSession.team)[0];
        public virtual void Dispose() { }
        public virtual (FBTarget tg, float t, bool head)? TargetRay(Vector3 o, Vector3 d, float max) => null;
        public virtual List<Vector4> Points => null;                    // marcadores do minimapa (x,y,z, estado)
        public virtual FBZone.Z Zone => null;
        public virtual int Round => 1;
        protected static string Mmss(float t) { int s = Mathf.Max(0, Mathf.CeilToInt(t)); return $"{s / 60}:{s % 60:00}"; }
        protected static readonly Color BLUE = new(.18f, .48f, 1), RED = new(.95f, .23f, .27f), GOLD = new(.96f, .67f, .09f), DIM = new(1, 1, 1, .16f), GREEN = new(.2f, .8f, .35f);
        protected IEnumerable<FBCharacter> Alive(int t) => FBMatch.Actors().Where(a => a.alive && a.team == t);
    }

    public static class FBModes
    {
        public static FBMode Create(string id, FBMatch g) => id switch
        {
            "tatico" => new Tatico(g), "classico" => new Classico(g), "squad" or "4v4" => new Squad(g), "pvl" => new PoliciaLadrao(g),
            "10x10" => new FBModo10x10(g),   // [parceiro 30/10] 10x10 por rodadas com compra (FBModo10x10.cs)
            "ffa" => new FFA(g),
            "treino" => new Treino(g), "br" => new BattleRoyale(g), "role" => new DeRole(g), _ => new Tatico(g),
        };

        // ---------------------------------------------------------------- TÁTICO (rodada 38: CONQUISTA)
        // Pedido do usuário (22/10): "as áreas de conquista estão muito próximas umas das outras e são excessivamente simples; redistribua os
        // pontos de forma estratégica e torne a disputa mais dinâmica". Antes: 5 pontos em fila a 18–28 m, só o jogador capturava, em ordem,
        // sem perder; aliados no estado "escort" (colados no jogador); inimigos parados de guarda em cima de cada ponto.
        // Agora: 5 pontos A–E em FAIXAS do mapa — A perto da nossa base, E perto da base inimiga, C no meio e B/D nos flancos esquerdo e
        // direito do meio-campo —, separados por ≥ 22 % da distância entre as bases (mínimo 34 m), em chão aberto com alguma cobertura em volta.
        // Os DOIS times capturam e recapturam (neutraliza primeiro, depois domina); mais gente no ponto captura mais rápido; ponto disputado
        // (gente dos dois lados) congela. Placar: +1 a cada 2 s por ponto dominado, +1 por abate; vence quem chegar a 250 ou liderar em 10 min.
        // Bots dos dois times recebem objetivo individual (atacar ponto que não é nosso, defender ponto nosso sob ataque), cada um com o seu
        // lugar dentro do ponto, rota de flanco própria e prazo próprio para reavaliar — nada de seguir o jogador.
        public class TacPt { public Vector3 pos; public float r = 7, cap; public int owner = -1; public string id; public FBMarker mk; public int n0, n1; public bool contested; }
        public static List<TacPt> TacticalLayout(FBMatch G)
        {
            Vector3 Cen(List<Vector3> a) => a.Aggregate(Vector3.zero, (s, p) => s + p) / Mathf.Max(1, a.Count);
            var A = Cen(G.TeamSpawns(0)); var B = Cen(G.TeamSpawns(1)); var AB = (B - A).Flat(); float L = AB.magnitude;
            var nav = FBNav.Fixed(); if (nav.Count < 20) nav = FBNav.Points();
            var outL = new List<TacPt>(); string[] IDS = { "A", "B", "C", "D", "E" };
            List<TacPt> Fallback() { var l = new List<TacPt>(); foreach (var p in G.Map.points) { if (l.Count >= 5) break; if (l.All(q => (q.pos - p).Flat().magnitude > 15)) l.Add(new TacPt { pos = p, id = IDS[l.Count] }); } return l; }
            if (L < 30 || nav.Count < 10) return Fallback();
            var dir = AB / L; var side = new Vector3(dir.z, 0, -dir.x);
            // candidatos: chão aberto (sem teto em 40 m), não colado nas bases; nota de "estratégia" = cobertura parcial em volta (3–6 de 8 raios
            // batem em até 7 m: tem onde se esconder mas não é corredor fechado) + espaço livre para o círculo
            var cands = new List<(Vector3 p, float t, float lat, float cover, float free)>();
            foreach (var p in nav)
            {
                var rel = (p - A).Flat(); float t = Vector3.Dot(rel, dir) / L, lat = Vector3.Dot(rel, side);
                if (t < .08f || t > .92f) continue; if ((p - A).Flat().magnitude < 26 || (p - B).Flat().magnitude < 26) continue;
                if (FBWorld.RayHit(p + Vector3.up * 1.2f, Vector3.up, 40)) continue;
                int hits = 0; float free = 12; var o = p + Vector3.up * 1.1f;
                for (int k = 0; k < 8; k++) { float a = k * Mathf.PI / 4; var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)); if (FBWorld.RayHit(o, d, 12, out var h)) { if (h.distance < 7) hits++; free = Mathf.Min(free, h.distance); } }
                float cover = hits >= 2 && hits <= 6 ? 1 - Mathf.Abs(hits - 4) * .2f : .15f;
                cands.Add((p, t, lat, cover, free));
            }
            if (cands.Count < 5) return Fallback();
            var mid = cands.Where(c => c.t > .3f && c.t < .7f).Select(c => Mathf.Abs(c.lat)).OrderBy(x => x).ToList();
            float W = Mathf.Min(L * .45f, mid.Count > 0 ? mid[Mathf.Clamp((int)(mid.Count * .85f), 0, mid.Count - 1)] : L * .3f);
            // faixas: A (nossa) · B flanco esq. · C meio · D flanco dir. · E (deles) — ordem em que o time do jogador avança
            int my = FBSession.team; float tA = my == 0 ? .2f : .8f, tE = 1 - tA, sgn = my == 0 ? 1 : -1;
            var slots = new[] { (t: tA, lat: 0f), (t: .5f - .1f * sgn, lat: -.62f * sgn), (t: .5f, lat: 0f), (t: .5f + .1f * sgn, lat: .62f * sgn), (t: tE, lat: 0f) };
            for (float sep = Mathf.Max(34, L * .22f); sep >= 20; sep *= .88f)
            {
                outL.Clear();
                for (int k = 0; k < slots.Length; k++)
                {
                    var st = slots[k]; var ideal = A + dir * (st.t * L) + side * (st.lat * W);
                    (Vector3 p, float t, float lat, float cover, float free)? best = null; float bs = -1e9f;
                    foreach (var c in cands)
                    {
                        if (outL.Any(q => (q.pos - c.p).Flat().magnitude < sep)) continue;
                        float d = (c.p - ideal).Flat().magnitude, sc = -d + c.cover * 9 + Mathf.Min(c.free, 8) * .6f - Mathf.Abs(c.p.y - A.y) * .8f;
                        if (sc > bs) { bs = sc; best = c; }
                    }
                    if (best == null) break;
                    outL.Add(new TacPt { pos = best.Value.p, id = IDS[k], r = Mathf.Clamp(best.Value.free * .85f, 5.5f, 9) });
                }
                if (outL.Count == 5) return outL;
            }
            return outL.Count >= 3 ? outL : Fallback();
        }
        public class Tatico : FBMode
        {
            const int LIMIT = 250; const float TIME = 600, CAP_T = 8, TICK = 2;
            readonly List<TacPt> pts = new(); readonly int[] score = new int[2]; float t = TIME, tick;
            readonly List<(float t, int team, string type)> q = new();
            // objetivo de cada bot: ponto, lugar dentro dele (ângulo próprio) e quando reavaliar
            class Order { public TacPt pt; public float ang, rad, until; public bool defend; }
            readonly Dictionary<FBCharacter, Order> orders = new();
            public Tatico(FBMatch g) : base(g) { name = "TÁTICO"; respawn = true; autoRespawn = 6; }
            static Color TeamCol(int team) => team < 0 ? new Color(.85f, .85f, .85f) : team == FBSession.team ? BLUE : RED;
            public override void Setup()
            {
                foreach (var p in TacticalLayout(G)) { p.mk = FBMarker.Make(p.pos, TeamCol(-1), p.r, 60); p.mk.Label(p.id); pts.Add(p); }
                int my = FBSession.team, en = 1 - my;
                G.SpawnSquad(my, 4, "patrol"); G.SpawnSquad(en, 5, "patrol");   // 5 × 5 (o jogador + 4 aliados)
                FBHUD.I?.MatchToast($"CONQUISTA · DOMINE OS PONTOS {string.Join(" ", pts.Select(p => p.id))} · {LIMIT} PONTOS", GOLD, 4);
                Debug.Log("[FreeBala][tatico] pontos: " + string.Join(" · ", pts.Select(p => $"{p.id} {p.pos.x:0},{p.pos.z:0} r{p.r:0.0}")) + $" · menor distância entre pontos {MinSep():0} m");
            }
            float MinSep() { float m = 1e9f; for (int i = 0; i < pts.Count; i++) for (int j = i + 1; j < pts.Count; j++) m = Mathf.Min(m, (pts[i].pos - pts[j].pos).Flat().magnitude); return m; }
            static bool Inside(FBCharacter a, TacPt p) => (a.Pos - p.pos).Flat().magnitude < p.r && Mathf.Abs(a.Pos.y - p.pos.y) < 4;
            // ---- objetivo individual do bot (FBBot.PickPatrolGoal pergunta isto antes de qualquer outra coisa)
            public override Vector3? Goal(FBCharacter b)
            {
                if (b.team > 1 || pts.Count == 0) return null;
                orders.TryGetValue(b, out var o);
                if (o == null || Time.time > o.until || (!o.defend && o.pt.owner == b.team && o.pt.cap * Sgn(b.team) >= .999f && !o.pt.contested) || (o.defend && !Threatened(o.pt, b.team)))
                    orders[b] = o = Assign(b, o);
                if (o == null) return null;
                var spot = o.pt.pos + new Vector3(Mathf.Sin(o.ang), 0, Mathf.Cos(o.ang)) * o.rad;
                return FBNav.Find(spot, 3, out var at) ? at : o.pt.pos;
            }
            public override bool Urgent(FBCharacter b) => orders.TryGetValue(b, out var o) && o != null && o.defend && (b.Pos - o.pt.pos).Flat().magnitude > o.pt.r;
            public override Vector3? UrgentGoal(FBCharacter b) => Goal(b);
            static float Sgn(int team) => team == 0 ? 1 : -1;
            bool Threatened(TacPt p, int team) => p.owner == team && (p.contested || p.cap * Sgn(team) < .98f);
            /// <summary>Ponto e papel de um bot dentro do ponto: diagnóstico do autoteste (quantos de cada time vão para cada ponto).</summary>
            public string OrdersDiag() => string.Join(" · ", pts.Select(p => $"{p.id}[{(p.owner < 0 ? "-" : p.owner == FBSession.team ? "nosso" : "deles")}{(p.contested ? ",disputa" : "")}] {orders.Count(kv => kv.Key && kv.Key.alive && kv.Key.team == FBSession.team && kv.Value?.pt == p)}×{orders.Count(kv => kv.Key && kv.Key.alive && kv.Key.team != FBSession.team && kv.Value?.pt == p)}")) + $" · placar {score[FBSession.team]}×{score[1 - FBSession.team]}";
            Order Assign(FBCharacter b, Order old)
            {
                // carga: quantos do time já vão para cada ponto (o próprio bot não conta)
                var load = pts.ToDictionary(p => p, p => orders.Count(kv => kv.Key && kv.Key != b && kv.Key.alive && kv.Key.team == b.team && kv.Value != null && kv.Value.pt == p));
                TacPt best = null; float bs = -1e9f; bool def = false;
                foreach (var p in pts)
                {
                    float d = (b.Pos - p.pos).Flat().magnitude; bool mine = p.owner == b.team, threat = Threatened(p, b.team);
                    if (mine && !threat) continue;                                                   // nosso e tranquilo: ninguém precisa ir
                    float pr = threat ? 70 : p.owner < 0 ? 55 : 40;                                  // defender > neutro > tomar do inimigo
                    float sc = pr - d * .35f - load[p] * (threat ? 10 : 18) + Random.Range(-8f, 8f); // espalha o time; sorteio = cada bot decide diferente
                    if (old != null && old.pt == p) sc += 6;                                         // inércia: não fica trocando de ideia
                    if (sc > bs) { bs = sc; best = p; def = threat; }
                }
                if (best == null) best = pts.OrderBy(p => (b.Pos - p.pos).Flat().magnitude + Random.Range(0f, 25f)).First();   // tudo nosso: segura um deles
                return new Order { pt = best, defend = def, ang = Random.Range(0, Mathf.PI * 2), rad = Random.Range(.25f, .8f) * best.r, until = Time.time + Random.Range(12f, 22f) };
            }
            public override Vector3 SpawnPoint()
            {
                // renasce no ponto dominado mais avançado sem inimigo em 35 m; senão na base
                int my = FBSession.team; var en = FBMatch.Actors().Where(a => a.alive && a.team == 1 - my).ToList();
                var ok = pts.Where(p => p.owner == my && !p.contested && en.All(e => (e.Pos - p.pos).Flat().magnitude > 35)).ToList();
                if (ok.Count > 0)
                {
                    var eb = G.TeamSpawns(1 - my); var ec = eb.Aggregate(Vector3.zero, (s2, p) => s2 + p) / Mathf.Max(1, eb.Count);
                    var p0 = ok.OrderBy(p => (p.pos - ec).Flat().magnitude).First(); if (FBNav.RandomPoint(p0.pos, p0.r + 6) is Vector3 sp) return sp;
                }
                return base.SpawnPoint();
            }
            Vector3 BotSpawn(int team)
            {
                var en = FBMatch.Actors().Where(a => a.alive && a.team == 1 - team).ToList();
                var ok = pts.Where(p => p.owner == team && !p.contested && en.All(e => (e.Pos - p.pos).Flat().magnitude > 40)).ToList();
                if (ok.Count > 0 && Random.value < .6f && FBNav.RandomPoint(FBUtil.Pick(ok).pos, 12) is Vector3 sp) return sp;
                return G.NextSpot(team);
            }
            public override void OnKill(FBCharacter v, FBCharacter k, bool head)
            {
                if (k && k != v && k.team < 2 && k.team != v.team) score[k.team]++;
                orders.Remove(v);
                if (!v.isPlayer && v.team < 2) { q.Add((7, v.team, v.skinId)); G.Invoke2(6, () => { if (v) { FBMatch.All.Remove(v); FBMatch.Bots.Remove(v); Object.Destroy(v.gameObject); } }); }
            }
            public override void Update(float dt)
            {
                t -= dt;
                for (int i = q.Count - 1; i >= 0; i--) { var r = q[i]; r.t -= dt; if (r.t <= 0) { G.MakeBot(r.type, r.team, BotSpawn(r.team), "patrol"); q.RemoveAt(i); } else q[i] = r; }
                var actors = FBMatch.Actors().Where(a => a.alive && !a.knocked && a.team < 2).ToList(); var player = FBMatch.Player; TacPt cur = null;
                foreach (var p in pts)
                {
                    p.mk.Spin(dt * .3f);
                    p.n0 = actors.Count(a => a.team == 0 && Inside(a, p)); p.n1 = actors.Count(a => a.team == 1 && Inside(a, p)); p.contested = p.n0 > 0 && p.n1 > 0;
                    int was = p.owner;
                    if (!p.contested && (p.n0 > 0 || p.n1 > 0))
                    {
                        int n = Mathf.Max(p.n0, p.n1); float rate = (n == 1 ? 1 : n == 2 ? 1.5f : 1.8f) / CAP_T; p.cap = Mathf.MoveTowards(p.cap, p.n0 > 0 ? 1 : -1, rate * dt);
                    }
                    else if (!p.contested) p.cap = Mathf.MoveTowards(p.cap, p.owner < 0 ? 0 : Sgn(p.owner), dt / 30);          // vazio: volta devagar ao dono
                    if (p.cap >= 1) p.owner = 0; else if (p.cap <= -1) p.owner = 1; else if (p.owner >= 0 && p.cap * Sgn(p.owner) <= 0) p.owner = -1;   // passou do meio: neutralizado
                    if (p.owner != was) Changed(p, was);
                    p.mk.SetColor(p.contested ? GOLD : p.owner >= 0 ? TeamCol(p.owner) : Color.Lerp(TeamCol(-1), TeamCol(p.cap > 0 ? 0 : 1), Mathf.Abs(p.cap)));
                    if (player && player.alive && Inside(player, p)) cur = p;
                }
                tick += dt; if (tick >= TICK) { tick -= TICK; foreach (var p in pts) if (p.owner >= 0) score[p.owner]++; }
                int my = FBSession.team;
                if (score[0] >= LIMIT || score[1] >= LIMIT || t <= 0) { G.EndMatch(score[my] > score[1 - my], "CONQUISTA", score[my], score[1 - my]); return; }
                if (cur != null)
                {
                    float mineCap = cur.cap * Sgn(my); string st = cur.contested ? "PONTO " + cur.id + " · DISPUTADO" : cur.owner == my && mineCap >= .999f ? "PONTO " + cur.id + " · DOMINADO" : cur.owner == 1 - my ? $"NEUTRALIZANDO {cur.id} {Mathf.RoundToInt((1 - Mathf.Abs(cur.cap)) * 100)}%" : $"DOMINANDO {cur.id} {Mathf.RoundToInt(Mathf.Max(0, mineCap) * 100)}%";
                    FBHUD.I?.SetBox(st, true); if (cur.contested) FBHUD.I?.MatchToast("ELIMINE OS INIMIGOS NO PONTO " + cur.id, RED, .4f);
                }
                else FBHUD.I?.SetBox("", false);
            }
            void Changed(TacPt p, int was)
            {
                int my = FBSession.team; foreach (var k in orders.Where(kv => kv.Value != null && kv.Value.pt == p).Select(kv => kv.Key).ToList()) orders.Remove(k);   // quem ia para lá reavalia
                if (p.owner == my) { FBAudio.Play2D("click", .9f); FBHUD.I?.Toast("PONTO " + p.id + " DOMINADO", "SUA EQUIPE CONTROLA O PONTO", "CONQUISTA", BLUE); }
                else if (p.owner == 1 - my) FBHUD.I?.Toast("PONTO " + p.id + " PERDIDO", "O INIMIGO DOMINOU O PONTO", "CONQUISTA", RED);
                else if (was == my) FBHUD.I?.MatchToast("PONTO " + p.id + " NEUTRALIZADO PELO INIMIGO", RED, 2.5f);
                else if (was == 1 - my) FBHUD.I?.MatchToast("PONTO " + p.id + " NEUTRALIZADO", BLUE, 2f);
            }
            static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
            public override List<Vector4> Points => pts.Select(p => new Vector4(p.pos.x, p.pos.y, p.pos.z, p.contested ? 3 : p.owner < 0 ? 0 : p.owner == FBSession.team ? 1 : 2)).ToList();
            public override void Hud()
            {
                int my = FBSession.team; string letters = string.Join(" ", pts.Select(p => $"<color=#{Hex(p.contested ? GOLD : p.owner >= 0 ? TeamCol(p.owner) : new Color(.75f, .75f, .75f))}>{p.id}</color>"));
                FBHUD.I?.SetMatch($"CONQUISTA · {G.MapName} · {Mmss(t)}", score[my].ToString(), letters, score[1 - my].ToString(), score[my] / (float)LIMIT, score[1 - my] / (float)LIMIT, null);
            }
            public override void Dispose() { foreach (var p in pts) if (p.mk) Object.Destroy(p.mk.gameObject); pts.Clear(); orders.Clear(); }
        }

        // ---------------------------------------------------------------- CLÁSSICO
        public class Classico : FBMode
        {
            const int LIMIT = 40; const float TIME = 480; readonly int[] score = new int[2]; float t = TIME; readonly List<(float t, int team, string type)> q = new();
            // r47: área de combate fixa (FBArena): parede da zona parada marcando o limite, dano progressivo fora, bots presos à área,
            // renascimento no ponto da área mais longe dos inimigos
            static readonly FBBrCore.Phase[] FIXA = { new() { wait = 1e6f, shrink = 1, k = 1, dmg = 3 } }; FBZone Z;
            public Classico(FBMatch g) : base(g) { name = "CLÁSSICO"; respawn = true; autoRespawn = 5; }
            public override FBZone.Z Zone => Z?.zone;
            public override void Setup()
            {
                if (G.arena != null) { Z = new FBZone(G, FIXA) { warn = "FORA DA ÁREA DE COMBATE" }; Z.Fx(); Z.Start(G.arena.c, G.arena.r).dmg = FIXA[0].dmg; }
                G.SpawnSquad(FBSession.team, 3, "hunt"); G.SpawnSquad(1 - FBSession.team, 4, "hunt"); FBHUD.I?.MatchToast($"MATA-MATA · {LIMIT} ABATES", GOLD, 3.5f);
            }
            public override Vector3? Goal(FBCharacter b) => G.arena != null && !G.arena.Inside(b.Pos, 10) ? G.arena.Keep(b.Pos) : null;
            public override bool Urgent(FBCharacter b) => G.arena != null && !G.arena.Inside(b.Pos, 3);
            public override Vector3? UrgentGoal(FBCharacter b) => G.arena?.Keep(b.Pos);
            public override Vector3 SpawnPoint() => G.arena != null ? G.arena.SafeSpot(FBSession.team) : base.SpawnPoint();
            public override void Dispose() => Z?.Dispose();
            public override void OnKill(FBCharacter v, FBCharacter k, bool head)
            {
                if (k && k.team != v.team && k.team < 2) score[k.team]++;
                if (!v.isPlayer) { q.Add((4, v.team, v.skinId)); G.Invoke2(6, () => { if (v) { FBMatch.All.Remove(v); FBMatch.Bots.Remove(v); Object.Destroy(v.gameObject); } }); }
                if (score[0] >= LIMIT || score[1] >= LIMIT) G.EndMatch(score[FBSession.team] >= LIMIT, "MATA-MATA", score[0], score[1]);
            }
            public override void Update(float dt)
            {
                t -= dt; if (t <= 0) { G.EndMatch(score[FBSession.team] > score[1 - FBSession.team], "MATA-MATA", score[0], score[1]); return; }
                for (int i = q.Count - 1; i >= 0; i--) { var r = q[i]; r.t -= dt; if (r.t <= 0) { G.MakeBot(r.type, r.team, G.arena != null ? G.arena.SafeSpot(r.team) : G.NextSpot(r.team), "hunt"); q.RemoveAt(i); } else q[i] = r; }
                if (Z != null) { Z.Anim(dt); Z.Step(dt); Z.Visual(); }
                FBHUD.I?.SetBox($"ABATES {score[FBSession.team]}/{LIMIT}", true);
            }
            public override void Hud() => FBHUD.I?.SetMatch($"MATA-MATA · {G.MapName}", score[0].ToString(), Mmss(t), score[1].ToString(), score[0] / (float)LIMIT, score[1] / (float)LIMIT, null);
        }

        // ---------------------------------------------------------------- 4V4 ZONA
        public class Squad : FBMode
        {
            // r51 (pedido 31/10): compra 15 → 30 s ("tempo suficiente para analisar e comprar sem pressa"); zona: a área é maior (FBArena.SIZE)
            // e cada fase fecha menos — antes .6/.5/.4/.01 de 55 m levava a 6,6 m na 3ª fase e a ~0 na última; agora .78/.68/.58/.45 de
            // 100–145 m termina com ~15–22 m de raio no fim da rodada (soma das fases = 146 s ≈ 150 s da rodada)
            const int WIN = 4; const float ROUND_T = 150; static float BUY_T => FBEconomy.LongBuy ? 30 : 15;
            static readonly FBBrCore.Phase[] PH0 = { new() { wait = 22, shrink = 22, k = .6f, dmg = 2 }, new() { wait = 14, shrink = 18, k = .5f, dmg = 4 }, new() { wait = 12, shrink = 15, k = .4f, dmg = 8 }, new() { wait = 10, shrink = 14, k = .01f, dmg = 14 } };
            static readonly FBBrCore.Phase[] PH1 = { new() { wait = 25, shrink = 22, k = .78f, dmg = 2 }, new() { wait = 18, shrink = 20, k = .68f, dmg = 4 }, new() { wait = 15, shrink = 18, k = .58f, dmg = 8 }, new() { wait = 12, shrink = 16, k = .45f, dmg = 14 } };
            static FBBrCore.Phase[] PH => FBArena.Bigger ? PH1 : PH0;
            readonly int[] wins = new int[2]; int round; string phase = "pre"; float pt, rt = ROUND_T; readonly List<int> results = new(); int lastSec; FBZone Z;
            public Squad(FBMatch g) : base(g) { name = "4V4 ZONA"; respawn = false; rounds = true; econ = true; }
            public override FBZone.Z Zone => Z?.zone; public override int Round => round;
            (Vector3 c, float d) Center()
            {
                Vector3 Cen(List<Vector3> a) => a.Aggregate(Vector3.zero, (s, p) => s + p) / Mathf.Max(1, a.Count);
                var ca = Cen(G.TeamSpawns(0)); var cb = Cen(G.TeamSpawns(1)); var mid = (ca + cb) * .5f; var al = FBZone.AlleyPoints(G); var nodes = al.Count > 0 ? al : FBNav.Points();
                var c = nodes.Count > 0 ? nodes.OrderBy(p => (p - mid).magnitude).First() : mid; return (c, (ca - cb).magnitude + (mid - c).magnitude);
            }
            void StartRound()
            {
                round++; phase = "buy"; pt = BUY_T; rt = ROUND_T;
                // r47: com área de combate a zona começa nela (bases dentro) e fecha para dentro dela; sem área (online), como antes
                if (G.arena != null) { Z.Start(G.arena.c, G.arena.r); G.ResetRound(); }
                else { var (c, d) = Center(); Z.Start(c, Mathf.Max(45, d * .75f)); G.ResetRound(); foreach (var a in FBMatch.Actors()) Z.zone.r = Mathf.Max(Z.zone.r, (a.Pos - Z.zone.c).Flat().magnitude + 15); }
                Z.Plan();
                G.freeze = true; FBHUD.I?.MatchToast($"RODADA {round} · FASE DE COMPRA", GOLD, 3); lastSec = 0;
                FBAudio.Synth(wins[0] == WIN - 1 || wins[1] == WIN - 1 ? "lastround" : "round"); FBEconomy.BuyPhase(true, BUY_T);
            }
            void EndRound(int winner, string why)
            {
                if (phase != "live") return; phase = "post"; pt = 4.5f; results.Add(winner); wins[winner]++;
                int my = FBSession.team; bool ok = winner == my; FBEconomy.Round(winner, round);
                FBHUD.I?.Toast(ok ? "RODADA VENCIDA" : "RODADA PERDIDA", $"{wins[my]} × {wins[1 - my]}", why, ok ? GREEN : RED);
                FBHUD.I?.MatchToast(ok ? "VITÓRIA NA RODADA" : "DERROTA NA RODADA", ok ? GREEN : RED, 4); FBAudio.Synth(ok ? "win" : "lose");
            }
            public override void Setup() { Z = new FBZone(G, PH); Z.Fx(); G.SpawnSquad(FBSession.team, 3, "squad"); G.SpawnSquad(1 - FBSession.team, 4, "squad"); FBEconomy.Reset(); StartRound(); }
            public override Vector3? Goal(FBCharacter b) { var z = Z?.zone; if (z == null) return null; float d = (b.Pos - z.c).Flat().magnitude; var tc = z.to != null && z.st == "shrink" ? z.to.Value.c : z.c; return d > z.r * .75f ? tc : (Vector3?)null; }
            public override bool Urgent(FBCharacter b) { var z = Z?.zone; return z != null && (b.Pos - z.c).Flat().magnitude > z.r * .92f; }
            public override Vector3? UrgentGoal(FBCharacter b) => Z?.zone?.c;
            public override void OnKill(FBCharacter v, FBCharacter k, bool head) { if (phase != "live") return; for (int t = 0; t < 2; t++) if (!Alive(t).Any()) EndRound(1 - t, "TIME ELIMINADO"); }
            public override void Update(float dt)
            {
                Z.Anim(dt);
                if (phase == "buy")
                {
                    pt -= dt; FBEconomy.ShopTime(pt); FBHUD.I?.SetBox(pt > 3.5f ? $"FASE DE COMPRA · {Mathf.CeilToInt(pt)}s · B = LOJA" : $"COMEÇA EM {Mathf.CeilToInt(pt)}", true);
                    int sec = Mathf.CeilToInt(pt);
                    if (sec != lastSec && pt > 0) { lastSec = sec; if (sec <= 10 && sec > 5) FBAudio.Synth("tick"); if (sec == 5) FBAudio.Synth("count5"); if (sec <= 3 && sec >= 1) { FBAudio.Synth("beep", sec); FBHUD.I?.Countdown(sec.ToString()); } if (sec == 3) FBAudio.Synth("riser", 3); }
                    if (pt <= 0) { phase = "live"; FBEconomy.BuyPhase(false); G.freeze = false; FBHUD.I?.SetBox("", false); FBHUD.I?.MatchToast("VALENDO!", GREEN, 1.5f); FBAudio.Synth("go"); FBHUD.I?.Countdown("VALENDO!"); G.OnLive(); }
                }
                else if (phase == "post") { pt -= dt; if (pt <= 0) { int my = FBSession.team; if (wins[0] >= WIN || wins[1] >= WIN) { G.EndMatch(wins[my] >= WIN, "4V4 ZONA", wins[0], wins[1]); return; } StartRound(); } }
                else
                {
                    rt -= dt; Z.Step(dt);
                    if (rt <= 0) { float H(int t) => Alive(t).Sum(a => Mathf.Max(0, a.hp)); float h0 = H(0), h1 = H(1); EndRound(h0 == h1 ? (Alive(0).Count() >= Alive(1).Count() ? 0 : 1) : h0 > h1 ? 0 : 1, "TEMPO ESGOTADO"); }
                }
                Z.Visual();
            }
            public override void Hud()
            {
                var z = Z?.zone; string zs = z == null || phase != "live" ? "" : Z.Label();
                float A(int t) => Alive(t).Count() / (float)Mathf.Max(4, FBMatch.Actors().Count(x => x.team == t));
                FBHUD.I?.SetMatch($"4V4 · RODADA {round} · {zs}", wins[0].ToString(), phase == "buy" ? $"{Mathf.CeilToInt(pt)}" : Mmss(rt), wins[1].ToString(), A(0), A(1), Enumerable.Range(0, 7).Select(i => i >= results.Count ? DIM : results[i] == 0 ? BLUE : RED).ToArray());
            }
            public override void Dispose() { Z?.Dispose(); FBEconomy.BuyPhase(false); }
        }

        // ---------------------------------------------------------------- FARINHA × BURITI (Polícia × Ladrão)
        public class PoliciaLadrao : FBMode
        {
            const int WIN = 4; const float ROUND_T = 150; static float BUY_T => FBEconomy.LongBuy ? 25 : 12; readonly int[] wins = new int[2]; int round; string phase = "pre"; float pt, rt = ROUND_T; readonly List<int> results = new(); int lastSec;   // r51: compra 12 → 25 s (mesma loja do 4V4)
            FBFarinhaBuriti.Board B; FBMarker[] hm, zm; GameObject[] meshes;
            static readonly Color[] COL = { new(.95f, .87f, .63f), new(.85f, .31f, .11f) };
            public PoliciaLadrao(FBMatch g) : base(g) { name = "FARINHA × BURITI"; respawn = false; rounds = true; econ = true; }
            public override int Round => round;
            Vector3 Base(int t) { var s = G.TeamSpawns(t); return s.Aggregate(Vector3.zero, (a, p) => a + p) / Mathf.Max(1, s.Count); }
            Vector3 NearestNav(Vector3 p) { var n = FBNav.Points(); return n.Count > 0 ? n.OrderBy(q => (q - p).Flat().magnitude).First() : p; }
            void StartRound()
            {
                round++; phase = "buy"; pt = BUY_T; rt = ROUND_T; G.ResetRound(); FBFarinhaBuriti.Reset(B); G.freeze = true; lastSec = 0; FBEconomy.BuyPhase(true, BUY_T);
                FBAudio.Synth(wins[0] == WIN - 1 || wins[1] == WIN - 1 ? "lastround" : "round"); int my = FBSession.team;
                FBHUD.I?.MatchToast($"RODADA {round} · CAPTURE {FBFarinhaBuriti.ART[my]} · DEFENDA {FBFarinhaBuriti.ART[1 - my]}", GOLD, 3);
            }
            void EndRound(int winner, string why)
            {
                if (phase != "live") return; phase = "post"; pt = 4.5f; results.Add(winner); wins[winner]++; int my = FBSession.team; bool ok = winner == my; FBEconomy.Round(winner, round);
                FBHUD.I?.Toast(ok ? "RODADA VENCIDA" : "RODADA PERDIDA", $"{wins[my]} × {wins[1 - my]}", why, ok ? GREEN : RED); FBHUD.I?.MatchToast(ok ? "VITÓRIA NA RODADA" : "DERROTA NA RODADA", ok ? GREEN : RED, 4);
                FBAudio.Synth(ok ? "win" : "lose");
            }
            string Who(FBFarinhaBuriti.IActor a) { var c = a as FBCharacter; return c && c.isPlayer ? "VOCÊ" : (c ? c.Name : FBFarinhaBuriti.TEAMS[a.Team]).ToUpper(); }
            void Events(List<FBFarinhaBuriti.Ev> ev)
            {
                int my = FBSession.team;
                foreach (var e in ev)
                {
                    int k = e.item.team; var nm = FBFarinhaBuriti.ITEMS[k]; var ac = e.actor as FBCharacter;
                    if (e.t == "pickup") { FBAudio.Play2D("click", .9f); if (ac && ac.isPlayer) FBHUD.I?.Toast($"VOCÊ PEGOU {FBFarinhaBuriti.ART[k]}", "LEVE ATÉ A SUA BASE (VERDE)", "FARINHA × BURITI", GOLD); else FBHUD.I?.Toast($"{Who(e.actor)} PEGOU {FBFarinhaBuriti.ART[k]}", e.actor.Team == my ? "ESCOLTE ATÉ A BASE" : "DERRUBE QUEM ESTÁ COM ELE", "FARINHA × BURITI", e.actor.Team == my ? GOLD : RED); }
                    else if (e.t == "drop") FBHUD.I?.Toast($"{nm} NO CHÃO", e.item.team == my ? "PEGUE DE VOLTA!" : "DEVOLVA AO PEDESTAL!", "FARINHA × BURITI", GOLD);
                    else if (e.t == "return") { FBAudio.Play2D("click", .8f); FBHUD.I?.Toast($"{nm} DEVOLVID{(k == 0 ? "A" : "O")}", "VOLTOU PARA O PEDESTAL", "FARINHA × BURITI", BLUE); }
                    else if (e.t == "expire") FBHUD.I?.Toast($"{nm} VOLTOU AO PEDESTAL", "", "FARINHA × BURITI", BLUE);
                    else if (e.t == "deliver") EndRound(k, k == 0 ? "FARINHA ENTREGUE · AGORA É SÓ COMER COM BURITI" : "BURITI ENTREGUE · AGORA É SÓ COMER COM FARINHA");
                }
            }
            public override void Setup()
            {
                FBMatch.TEAM_NAME[0] = FBMatch.TEAM_SHORT[0] = "FARINHA"; FBMatch.TEAM_NAME[1] = FBMatch.TEAM_SHORT[1] = "BURITI";
                var bp = Base(0); var bl = Base(1); var mid = (bp + bl) * .5f;
                var zones = new[] { NearestNav(Vector3.Lerp(bp, mid, .12f)), NearestNav(Vector3.Lerp(bl, mid, .12f)) }; var homes = new[] { NearestNav(Vector3.Lerp(bl, mid, .28f)), NearestNav(Vector3.Lerp(bp, mid, .28f)) };
                B = FBFarinhaBuriti.Create(homes, zones);
                hm = new[] { FBMarker.Make(homes[0], COL[0], 2.6f, 70), FBMarker.Make(homes[1], COL[1], 2.6f, 70) }; zm = new[] { FBMarker.Make(zones[0], new Color(.21f, .82f, .35f), FBFarinhaBuriti.DELIVER, 14), FBMarker.Make(zones[1], new Color(.21f, .82f, .35f), FBFarinhaBuriti.DELIVER, 14) };
                meshes = new[] { FBProps.FarinhaSaco(), FBProps.BuritiCacho() };
                G.SpawnSquad(FBSession.team, 3, "squad"); G.SpawnSquad(1 - FBSession.team, 4, "squad"); FBEconomy.Reset(); StartRound();
            }
            public override Vector3? Goal(FBCharacter b) { if (B == null || phase != "live") return null; return FBFarinhaBuriti.BotGoal(B, b).goal; }
            public override bool Urgent(FBCharacter b) => B != null && FBFarinhaBuriti.BotGoal(B, b, () => 1).urgent;
            public override Vector3? UrgentGoal(FBCharacter b) => FBFarinhaBuriti.BotGoal(B, b, () => 0).goal ?? b.Pos;
            public override void OnKill(FBCharacter v, FBCharacter k, bool head) { if (phase != "live") return; var ev = new List<FBFarinhaBuriti.Ev>(); FBFarinhaBuriti.Drop(B, v, ev); Events(ev); for (int t = 0; t < 2; t++) if (!Alive(t).Any()) EndRound(1 - t, $"{FBFarinhaBuriti.TEAMS[t]} ELIMINADA"); }
            public override void Update(float dt)
            {
                for (int k = 0; k < 2; k++) { hm[k].Spin(dt * .4f * (k == 1 ? -1 : 1)); zm[k].Spin(-dt * .4f); }
                int my = FBSession.team;
                if (phase == "buy")
                {
                    pt -= dt; FBEconomy.ShopTime(pt); FBHUD.I?.SetBox(pt > 3.5f ? $"FASE DE COMPRA · {Mathf.CeilToInt(pt)}s · B = LOJA" : $"COMEÇA EM {Mathf.CeilToInt(pt)}", true);
                    int sec = Mathf.CeilToInt(pt); if (sec != lastSec && pt > 0) { lastSec = sec; if (sec == 5) FBAudio.Synth("count5"); if (sec <= 3 && sec >= 1) { FBAudio.Synth("beep", sec); FBHUD.I?.Countdown(sec.ToString()); } }
                    if (pt <= 0) { phase = "live"; FBEconomy.BuyPhase(false); G.freeze = false; FBHUD.I?.Countdown("VALENDO!"); FBAudio.Synth("go"); G.OnLive(); }
                }
                else if (phase == "post") { pt -= dt; if (pt <= 0) { if (wins[0] >= WIN || wins[1] >= WIN) { G.EndMatch(wins[my] >= WIN, "FARINHA × BURITI", wins[0], wins[1]); return; } StartRound(); } }
                else if (phase == "live")
                {
                    rt -= dt; Events(FBFarinhaBuriti.Step(B, FBMatch.Actors().Cast<FBFarinhaBuriti.IActor>(), dt));
                    if (rt <= 0 && phase == "live") EndRound(FBFarinhaBuriti.TimeoutWinner(B, new[] { Alive(0).Count(), Alive(1).Count() }), "TEMPO ESGOTADO · CHEGOU MAIS PERTO");
                    var mine = B.items[my]; var foe = B.items[1 - my]; bool meCarry = mine.carrier as FBCharacter == FBMatch.Player; string A = FBFarinhaBuriti.ART[my], FA = FBFarinhaBuriti.ART[1 - my], N = FBFarinhaBuriti.ITEMS[my], F = FBFarinhaBuriti.ITEMS[1 - my];
                    FBHUD.I?.SetBox(meCarry ? $"LEVE {A} ATÉ A SUA BASE (VERDE)" : mine.st == "carried" ? $"ESCOLTE QUEM ESTÁ COM {A}" : mine.st == "dropped" ? $"PEGUE {A} DO CHÃO · {Mathf.CeilToInt(mine.dropT)}s"
                        : foe.st == "carried" ? $"DERRUBE QUEM LEVA {FA}!" : foe.st == "dropped" ? $"ENCOSTE {(FA == "A FARINHA" ? "NA" : "NO")} {F} PARA DEVOLVER · {Mathf.CeilToInt(foe.dropT)}s" : $"CAPTURE {A} ({(N == "FARINHA" ? "CREME" : "LARANJA")}) · DEFENDA {FA}", true);
                }
                if (B != null) for (int k = 0; k < 2; k++)
                    {
                        var it = B.items[k]; var c = it.carrier as FBCharacter; var p = it.st == "carried" && c ? c.Pos + Vector3.up * (2.25f + k * .25f) : it.pos + Vector3.up * .05f;
                        meshes[k].transform.position = p; meshes[k].transform.Rotate(0, dt * 1.6f * Mathf.Rad2Deg, 0); meshes[k].SetActive(phase != "pre");
                    }
            }
            public override List<Vector4> Points => B == null ? null : B.items.Select(it => new Vector4(it.pos.x, it.pos.y, it.pos.z, 0)).Concat(B.zones.Select(z => new Vector4(z.x, z.y, z.z, 1))).ToList();
            public override void Hud()
            {
                string st = B == null ? "" : string.Join(" · ", new[] { 0, 1 }.Select(k => B.items[k].st == "carried" ? $"{FBFarinhaBuriti.ITEMS[k]} EM FUGA" : B.items[k].st == "dropped" ? $"{FBFarinhaBuriti.ITEMS[k]} NO CHÃO" : "").Where(s => s != "")); if (st == "") st = "FARINHA × BURITI";
                float A(int t) => Alive(t).Count() / (float)Mathf.Max(4, FBMatch.Actors().Count(x => x.team == t));
                FBHUD.I?.SetMatch($"RODADA {round} · {st}", wins[0].ToString(), phase == "buy" ? $"{Mathf.CeilToInt(pt)}" : Mmss(rt), wins[1].ToString(), A(0), A(1), Enumerable.Range(0, 7).Select(i => i >= results.Count ? DIM : results[i] == 0 ? BLUE : RED).ToArray());
            }
            public override void Dispose()
            {
                foreach (var m in (hm ?? new FBMarker[0]).Concat(zm ?? new FBMarker[0])) if (m) Object.Destroy(m.gameObject); if (meshes != null) foreach (var m in meshes) if (m) Object.Destroy(m);
                FBEconomy.BuyPhase(false); FBMatch.TEAM_NAME[0] = "TIME AZUL"; FBMatch.TEAM_NAME[1] = "TIME VERMELHO"; FBMatch.TEAM_SHORT[0] = "AZUL"; FBMatch.TEAM_SHORT[1] = "VERMELHO";
            }
        }

        // ---------------------------------------------------------------- TODOS × TODOS
        public class FFA : FBMode
        {
            const int LIMIT = 25, BOTS = 7; const float TIME = 600; float t = TIME; readonly List<(float t, string type, int team, string name, int k, int d)> q = new();
            public FFA(FBMatch g) : base(g) { name = "TODOS × TODOS"; ffa = true; respawn = true; autoRespawn = 3; }
            List<Vector3> All() { var m = G.Map; return m.spawnA.Concat(m.spawnB).Concat(m.bots).Concat(m.points).ToList(); }
            public override Vector3 SpawnPoint()
            {
                var all = All(); if (all.Count == 0) return Vector3.zero; var alive = FBMatch.Actors().Where(a => a.alive).ToList(); if (alive.Count == 0) return FBUtil.Pick(all);
                var sorted = all.OrderByDescending(p => alive.Min(a => (a.Pos - p).Flat().magnitude)).ToList(); return sorted[Random.Range(0, Mathf.Max(1, Mathf.CeilToInt(sorted.Count / 4f)))];
            }
            public override void Setup()
            {
                FBMatch.FFA = true; var types = G.typePool[0].Concat(G.typePool[1]).ToList();
                for (int i = 0; i < BOTS; i++) { var b = G.MakeBot(types[i % types.Count], i % 2, SpawnPoint(), "patrol"); b.kills = b.deaths = 0; }
                FBHUD.I?.MatchToast($"TODOS × TODOS · PRIMEIRO A {LIMIT} ABATES", GOLD, 3.5f);
            }
            int Top => Mathf.Max(FBMatch.Stats.kills, FBMatch.Bots.Where(b => b).Select(b => b.kills).DefaultIfEmpty(0).Max());
            public override void OnKill(FBCharacter v, FBCharacter k, bool head)
            {
                if (v && !v.isPlayer) { q.Add((3, v.skinId, v.team, v.displayName, v.kills, v.deaths)); G.Invoke2(5, () => { if (v) { FBMatch.All.Remove(v); FBMatch.Bots.Remove(v); Object.Destroy(v.gameObject); } }); }
                if (FBMatch.Stats.kills >= LIMIT || Top >= LIMIT) G.EndMatch(FBMatch.Stats.kills >= Top, "TODOS × TODOS", FBMatch.Stats.kills, Top);
            }
            public override void Update(float dt)
            {
                t -= dt; if (t <= 0) { G.EndMatch(FBMatch.Stats.kills >= Top, "TODOS × TODOS", FBMatch.Stats.kills, Top); return; }
                for (int i = q.Count - 1; i >= 0; i--) { var r = q[i]; r.t -= dt; if (r.t <= 0) { var b = G.MakeBot(r.type, r.team, SpawnPoint(), "patrol"); b.displayName = r.name; b.kills = r.k; b.deaths = r.d; q.RemoveAt(i); } else q[i] = r; }
                FBHUD.I?.SetBox($"ABATES {FBMatch.Stats.kills}/{LIMIT}", true);
            }
            public override void Hud() => FBHUD.I?.SetMatch($"TODOS × TODOS · {G.MapName}", FBMatch.Stats.kills.ToString(), Mmss(t), $"{Top}/{LIMIT}", FBMatch.Stats.kills / (float)LIMIT, -1, null);
            public override void Dispose() { FBMatch.FFA = false; }
        }

        // ---------------------------------------------------------------- TREINO (estande de tiro / bonecos)
        public class Treino : FBMode
        {
            readonly List<float> q = new(); float t; readonly List<FBRangeTarget> tgs = new(); int hits, heads; float? best, last;
            public Treino(FBMatch g) : base(g) { name = "TREINO"; respawn = true; autoRespawn = 2; arsenal = true; }
            // rodada 38 (pedido 22/10): mais bonecos e, no estande, nascendo DENTRO das pistas (areia entre as baias e o barranco, pela faixa dos
            // alvos de papel) — antes 5 bonecos em pontos de navegação a 10–45 m, inclusive atrás da linha de tiro
            const int DUMMIES = 12;
            Rect lanes; bool hasLanes;
            Vector3? Spot()
            {
                if (hasLanes)
                    for (int k = 0; k < 16; k++)
                    {
                        float x = Random.Range(lanes.xMin, lanes.xMax), z = Random.Range(lanes.yMin, lanes.yMax);
                        if (FBWorld.GroundY(x, 3, z, 2, 8) is float gy && !FBMatch.Bots.Any(b => b && b.alive && (b.Pos - new Vector3(x, gy, z)).sqrMagnitude < 4)) return new Vector3(x, gy, z);
                    }
                var all = FBNav.Points(); var near = all.Where(p => { float d = (p - FBMatch.Player.Pos).magnitude; return d > 10 && d < 45; }).ToList(); var l = near.Count >= 4 ? near : all; return l.Count > 0 ? FBUtil.Pick(l) : (Vector3?)null;
            }
            void Dummy()
            {
                int en = 1 - FBSession.team; if (!(Spot() is Vector3 p)) return; var b = G.MakeBot(FBUtil.Pick(G.typePool[en]), en, p, "dummy");
                var d = (FBMatch.Player.Pos - b.Pos).Flat().normalized; b.GetComponent<FBBot>().SetDummy(new Vector3(-d.z, 0, d.x), Random.value < .6f ? FBUtil.Rnd(1.5f, 3) : 0, FBUtil.Rnd(.6f, 1.1f)); b.tagT = 1e9f;
            }
            public override void Setup()
            {
                var L = Json.L(G.Level, "targets");
                if (L != null && L.Count > 0)
                {
                    foreach (var o in L) { var tg = FBRangeTarget.Make(o as Dictionary<string, object>); tg.onHit = (x, h) => { hits++; if (h) heads++; float r = Time.time - x.upAt; if (r < 6) { last = r; if (best == null || r < best) best = r; } FBAudio.PlayAt("impact" + Random.Range(1, 5), x.transform.position, .6f, 6); if (h) FBHUD.I?.MatchToast($"CABEÇA! · {x.dist} M", GOLD, .9f); }; tgs.Add(tg); }
                    // pistas = caixa dos alvos de papel (x) de 12 m até um pouco além do alvo mais longe que ainda se vê bem (70 m)
                    float x0 = 1e9f, x1 = -1e9f, z0 = 1e9f; foreach (var tg in tgs) { var tp = tg.transform.position; x0 = Mathf.Min(x0, tp.x); x1 = Mathf.Max(x1, tp.x); z0 = Mathf.Min(z0, tp.z); }
                    if (x1 > x0) { hasLanes = true; lanes = Rect.MinMaxRect(x0 - 1.2f, Mathf.Max(z0, -70), x1 + 1.2f, -12); }
                    for (int i = 0; i < DUMMIES; i++) Dummy(); FBHUD.I?.MatchToast("ESTANDE DE TIRO · TECLA N = ARSENAL (QUALQUER ARMA E ACESSÓRIO)", GOLD, 5);
                }
                else { for (int i = 0; i < DUMMIES; i++) Dummy(); FBHUD.I?.MatchToast("TREINO LIVRE · OS ALVOS NÃO ATIRAM", GOLD, 3.5f); }
            }
            public override void OnKill(FBCharacter v, FBCharacter k, bool head) { if (!v.isPlayer) { q.Add(3); G.Invoke2(4, () => { if (v) { FBMatch.All.Remove(v); FBMatch.Bots.Remove(v); Object.Destroy(v.gameObject); } }); } }
            public override void Update(float dt)
            {
                t += dt; for (int i = q.Count - 1; i >= 0; i--) { q[i] -= dt; if (q[i] <= 0) { q.RemoveAt(i); Dummy(); } }
                FBMatch.Player.GetComponent<FBWeaponUser>().RefillAll(4);
                int acc = Mathf.RoundToInt(100f * FBMatch.Stats.hits / Mathf.Max(1, FBMatch.Stats.shots));
                FBHUD.I?.SetBox(tgs.Count > 0 ? $"N = ARSENAL · ACERTOS {hits} · CABEÇA {heads} · PRECISÃO {acc}%{(last != null ? $" · REAÇÃO {last:0.00}s · MELHOR {best:0.00}s" : "")}" : "TREINO · MUNIÇÃO INFINITA · N = ARSENAL", true);
            }
            public override void Hud() { int acc = Mathf.RoundToInt(100f * FBMatch.Stats.hits / Mathf.Max(1, FBMatch.Stats.shots)); FBHUD.I?.SetMatch($"TREINO · {G.MapName}", $"{(tgs.Count > 0 ? hits + FBMatch.Stats.kills : FBMatch.Stats.kills)}", Mmss(t), $"{acc}%", -1, -1, null); }
            public override void Dispose() { foreach (var tg in tgs) if (tg) Object.Destroy(tg.gameObject); tgs.Clear(); }
        }

        // ---------------------------------------------------------------- BATTLE ROYALE
        public class BattleRoyale : FBMode
        {
            const int BOTS = 15, BOTS_BIG = 49; FBZone Z; string phase = "air"; float t, clock; bool over; int total = BOTS + 1;
            public BattleRoyale(FBMatch g) : base(g) { name = "BATTLE ROYALE"; br = true; ffa = true; respawn = false; ffDamage = true; }
            public override FBZone.Z Zone => Z?.zone;
            public override Vector3 SpawnPoint() { var p = FBNav.Points(); return p.Count > 0 ? FBUtil.Pick(p) : Vector3.zero; }
            void Finish(bool win, int place) { if (over) return; over = true; G.EndMatch(win, "BATTLE ROYALE", place, FBMatch.Stats.kills, place, total); }
            public override void Setup()
            {
                FBMatch.FFA = true; over = false; phase = "air"; Z = new FBZone(G, FBBrCore.PHASES); Z.Fx();
                var pts = FBNav.Points(); var c = pts.Count > 0 ? pts.Aggregate(Vector3.zero, (a, p) => a + p) / pts.Count : Vector3.zero;
                float r = 60; foreach (var p in pts) r = Mathf.Max(r, (p - c).Flat().magnitude); var cc = pts.Count > 0 ? pts.OrderBy(p => (p - c).magnitude).First() : c;
                Z.Start(cc, r * 1.05f); Z.Plan();
                var types = G.typePool[0].Concat(G.typePool[1]).ToList(); int nb = r > 500 ? BOTS_BIG : BOTS; total = nb + 1;
                for (int i = 0; i < nb; i++) { var b = G.MakeBot(types[i % types.Count], i % 2, pts.Count > 0 ? FBUtil.Pick(pts) : c, "patrol"); b.kills = b.deaths = 0; b.maxHp = b.hp = 200; }
                FBMatch.Player.maxHp = FBMatch.Player.hp = 200;
                FBBattleRoyale.Setup(G, Z.zone); FBHUD.I?.MatchToast("BATTLE ROYALE · SALTE DO AVIÃO", GOLD, 4);
            }
            public override Vector3? Goal(FBCharacter b) { var z = Z?.zone; if (z == null || phase == "air") return null; float d = (b.Pos - z.c).Flat().magnitude; var tc = z.to != null && z.st == "shrink" ? z.to.Value.c : z.c; return d > z.r * .75f ? tc : (Vector3?)null; }
            public override bool Urgent(FBCharacter b) { var z = Z?.zone; return z != null && phase != "air" && (b.Pos - z.c).Flat().magnitude > z.r * .92f; }
            public override Vector3? UrgentGoal(FBCharacter b) => Z?.zone?.c;
            public override void OnKill(FBCharacter v, FBCharacter k, bool head)
            {
                int n = FBMatch.Actors().Count(a => a.alive);
                if (v.isPlayer) { FBHUD.I?.Toast("VOCÊ FOI ELIMINADO", $"#{n + 1}", $"{FBMatch.Stats.kills} ABATES", RED); G.Invoke2(3.5f, () => Finish(false, n + 1)); return; }
                if (FBMatch.Player.alive && n == 1) { FBHUD.I?.MatchToast("BOOYAH!", GOLD, 4); FBAudio.Synth("win"); G.Invoke2(2.5f, () => Finish(true, 1)); }
            }
            public override void Update(float dt)
            {
                Z.Anim(dt); clock += dt;
                if (phase == "air") { t += dt; if (!FBBattleRoyale.Airborne() || t > 75) { phase = "live"; FBHUD.I?.MatchToast("A ZONA VAI COMEÇAR A FECHAR", new Color(.35f, .65f, 1), 3); } }
                else if (!over) Z.Step(dt);
                Z.Visual(); FBBattleRoyale.Tick(dt);
            }
            public override void Hud() { int n = FBMatch.Actors().Count(a => a.alive); FBHUD.I?.SetMatch($"BATTLE ROYALE · {(phase == "air" ? "SALTO" : Z.Label())}", $"{n}", Mmss(clock), $"{FBMatch.Stats.kills}", 1 - n / (float)total, -1, null); FBHUD.I?.SetBox($"{n} VIVOS · {FBMatch.Stats.kills} ABATES", true); }
            public override void Dispose() { Z?.Dispose(); FBMatch.FFA = false; FBBattleRoyale.End(); }
        }

        // ---------------------------------------------------------------- DE ROLÊ
        public class DeRole : FBMode
        {
            public DeRole(FBMatch g) : base(g) { name = "DE ROLÊ"; respawn = true; autoRespawn = 3; role = true; }
            public override void Setup() => FBHUD.I?.MatchToast("DE ROLÊ · G = GPS · K = SOM · E NA OFICINA", GOLD, 4);
            public override void Update(float dt) => FBRole.Tick(dt);
        }

        // ---------------------------------------------------------------- ONLINE (placar do servidor)
        public class Online : FBMode
        {
            public Online(FBMatch g) : base(g) { name = "ONLINE"; respawn = true; autoRespawn = 3; }
            public override void Setup() => FBHUD.I?.MatchToast("ONLINE · " + (FBNet.I ? FBNet.I.status : "CONECTANDO…"), GOLD, 3);
            public override void Hud() { var n = FBNet.I; if (n) FBHUD.I?.SetMatch($"ONLINE · {G.MapName}", n.score[0].ToString(), n.status, n.score[1].ToString(), -1, -1, null); }
            public override void OnKill(FBCharacter v, FBCharacter k, bool head) { }
        }
    }
}
