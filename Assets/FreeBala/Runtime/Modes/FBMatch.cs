// Partida (porte do bloco "partida" do main.js + o objeto G que os modos usam): monta o jogador e os bots com os personagens/armas
// importados, bases distantes com o esquadrão nascendo em formação (lineSpots), papéis táticos dos bots (SQUAD_ROLES: faixa de flanco,
// ritmo), reforços do lado inimigo, abates (placar, killfeed, recompensa, economia, sequência), morte do jogador (renascer / espectar),
// rodadas (resetRound), proteção de spawn (CF), knockdown (IE) e fim de partida com dança da vitória. As regras de cada modo ficam em FBModes.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBMatch : MonoBehaviour
    {
        public string mapId; static FBMatch inst; public static FBMatch I { get => inst ? inst : null; set => inst = value; }
        public static readonly List<FBCharacter> All = new(); public static readonly List<FBCharacter> Bots = new(), Civs = new(), Remotes = new();
        public static bool FFA, Paused, God; public static FBCharacter Player;
        public class StatsT { public int kills, deaths, shots, hits, heads; } public static readonly StatsT Stats = new();
        // placar por nome (tela de fim: MVP e destaques) — os bots do Tático/Clássico são destruídos e recriados, então o FBCharacter.kills se perde
        public static readonly Dictionary<string, (int team, int k, int d)> Tally = new();
        public float RespawnLeft => respawnT;
        public MapDef Map => GameData.Maps.TryGetValue(mapId, out var m) ? m : null;
        public Dictionary<string, object> Level => Map?.level;
        public FBMode mode; public string state = "loading"; public bool freeze; public string MapName => (Map?.name ?? mapId).ToUpper();
        public bool FFDamage => mode != null && mode.ffDamage;
        public static readonly string[] TEAM_NAME = { "TIME AZUL", "TIME VERMELHO" }, TEAM_SHORT = { "AZUL", "VERMELHO" };
        public List<string>[] typePool = { new(), new() };
        float respawnT = -1; FBCharacter spect;

        public static bool Hostile(FBCharacter a, FBCharacter b) => a && b && (FFA ? a != b : a.team != b.team);
        public static bool KnockOn => I && I.state == "play" && FBMechanics.KnockMode(FBSession.mode) && !FBSession.online;
        public static IEnumerable<FBCharacter> TeamOf(FBCharacter a) => All.Where(o => o && o != a && !o.civ && !o.isRemote && o.team == a.team);
        public static bool HasReviver(FBCharacter a) => TeamOf(a).Any(o => o.alive && !o.knocked);
        public static void Feed(string t, bool me = false) => FBHUD.I?.Feed(t, me);
        public static IEnumerable<FBCharacter> Actors() => All.Where(a => a && !a.civ);

        void Awake() { I = this; All.Clear(); Bots.Clear(); Civs.Clear(); Remotes.Clear(); GameData.Load(); FBSession.Load(); Paused = false; FFA = false; FBNav.Reset(); FBFx.Warm(); }

        // r43: tempos dos passos da montagem (FBLoading mostra junto no log de carregamento)
        public static string BuildTimes = "";
        void Start()
        {
            var bsw = System.Diagnostics.Stopwatch.StartNew(); var bl = new System.Text.StringBuilder("· montagem: "); double bt = 0;
            void B(string n) { double t = bsw.Elapsed.TotalSeconds; if (t - bt >= .05) bl.Append($"{n} {t - bt:0.00}s "); bt = t; }
            try { Start0(B); } finally { B("resto"); BuildTimes = bl.ToString(); }
        }
        void Start0(System.Action<string> B)
        {
            new GameObject("HUD").AddComponent<FBHUD>(); FBElenco.LogOnce();                                          // r52: categorias do elenco + auditoria no log
            var m = Map; if (m == null) { Debug.LogError("[FreeBala] mapa sem dados: " + mapId); return; }
            FBWorld.SetWater(Level); FBEconomy.ResetMatch(); FBBotBrain.Reset(); FBLoot.Clear(); FBMarks.Clear();
            B("HUD/limpeza"); FBVehicles.SpawnParked(this, FBSession.mode == "role" ? .3f : 0); new GameObject("FB_Veiculos").AddComponent<FBVehiclesTicker>(); FBDoors.Reset(); if (FBVehicles.Allowed) FBDoors.Boot();
            var pb = FBWorld.GetPlayableBounds(mapId);
            var initAnchor = m.spawnA.Count > 0 && pb.Contains(m.spawnA[0]) ? m.spawnA[0] : (m.spawnB.Count > 0 && pb.Contains(m.spawnB[0]) ? m.spawnB[0] : pb.center);
            FBNav.SetAnchor(initAnchor);
            B("carros estacionados"); EnsureLevel(); B("pontos do nível");
            FBWeather.Apply(FBSession.WeatherFor(mapId)); B("clima");
            // pool de personagens (bots nunca usam os jogáveis do De Rolê); Polícia × Ladrão: cada lado só com os seus
            if (FBSession.mode == "pvl")
            {
                List<string> Side(int t) => GameData.Skins.Where(k => !k.role && (k.team < 0 ? 1 : k.team) == t && FBRegistry.I.HasChar(k.id)).Select(k => k.id).OrderBy(_ => Random.value).ToList();
                var mine = Side(FBSession.team); if (!mine.Contains(FBSession.skin) && mine.Count > 0) FBSession.skin = mine[0];
                typePool = new[] { Side(0).Where(id => id != FBSession.skin).Take(6).ToList(), Side(1).Where(id => id != FBSession.skin).Take(6).ToList() };
            }
            else
            {
                var pool = GameData.Skins.Where(k => !k.role && k.id != FBSession.skin && FBRegistry.I.HasChar(k.id)).Select(k => k.id).OrderBy(_ => Random.value).ToList();
                typePool = new[] { pool.Take(6).ToList(), pool.Skip(6).Take(6).ToList() };
            }
            for (int t = 0; t < 2; t++) if (typePool[t].Count == 0) typePool[t].Add(FBRegistry.I.charIds[0]);
            // jogador
            Stats.kills = Stats.deaths = Stats.shots = Stats.hits = Stats.heads = 0; Tally.Clear();
            arena = FBArena.For(FBSession.mode) ? FBArena.PickFor(this, FBSession.mode) : null; B("área de combate");
            PickBases(); var sp = NextSpot(FBSession.team);
            B("bases"); Player = Spawn(FBSession.skin, FBSession.look, FBSession.team, sp, true); B("jogador");
            Player.transform.rotation = Quaternion.Euler(0, teamLine[FBSession.team].yaw, 0);
            bool econ = FBEconomy.EconMode(FBSession.mode) || FBSession.mode == "br" || FBSession.mode == "role";
            var wu = Player.GetComponent<FBWeaponUser>(); if (FBSession.mode == "role") wu.Strip(); else if (!econ) wu.Give(FBSession.primary, FBSession.smg, FBSession.secondary); else wu.Give();   // DE ROLÊ: ninguém anda armado
            FBMed.ResetPlayer(econ ? 1 : FBMed.Start, FBSession.mode == "treino");
            if (FBSession.online) { new GameObject("Rede").AddComponent<FBNet>(); mode = new FBModes.Online(this); }
            else mode = FBModes.Create(FBSession.mode, this);
            B("jogador (armas/rede)"); mode.Setup(); B("modo (bots)");
            if (mode.ffa) { FFA = true; var p = mode.SpawnPoint(); Teleport(Player, p, SpawnYaw(p)); }
            bool noCivs = FBEconomy.EconMode(FBSession.mode) || FBSession.mode == "treino" || FBSession.mode == "ffa" || FBSession.online || FBSession.mode == "role" || FBSession.mode == "br";
            if (!noCivs) SpawnCivilians(10); B("civis");
            if (FBSession.map != "estande" && FBSession.map != "bermuda") FBGraffiti.Place(FBSession.map);   // grafites v4 (todos os mapas, cada arte uma vez)
            B("grafites"); if (FBSession.mode == "role") FBRole.Setup(this); else { FBAmbientScenery.Spawn(this); FBTactics.Spawn(); }
            B(FBSession.mode == "role" ? "rolê (trânsito/NPCs)" : "cenário/táticas"); FBStreetEvents.Boot(this); B("eventos de rua");
            if (FBSession.mode != "role" && FBSession.map != "estande" && FBSession.map != "bermuda") { FBVestirRua.Dress(); B("objetos do 171"); }   // r44                                                                   // carro do açaí e baculejo (rodada 39)
            state = "play"; FBEconomy.Hud();
            FBAudio.SetLoop("amb", "amb", .45f); FBAmbience.Start(mapId);
            foreach (var root in gameObject.scene.GetRootGameObjects()) if (root.name.StartsWith("Map_")) Debug.Log($"[FreeBala] postes na pista escondidos: {FBStreetPoles.Clean(root)} · destrutíveis: {FBDestructible.Setup(root)} objetos");
            // sem fala de rádio nem rojão no início (pedidos do usuário; rojão removido em 26/10)
        }

        // ---- dados do nível que faltam (ensureLevel): pontos gerados pela navmesh
        void EnsureLevel()
        {
            var m = Map;
            var pb = FBWorld.GetPlayableBounds(mapId);
            m.spawnA = m.spawnA.Where(p => pb.Contains(p) && FBNav.Inside(p)).ToList();
            m.spawnB = m.spawnB.Where(p => pb.Contains(p) && FBNav.Inside(p)).ToList();
            m.points = m.points.Where(p => pb.Contains(p) && FBNav.Inside(p)).ToList();
            m.waypoints = m.waypoints.Where(p => pb.Contains(p) && FBNav.Inside(p)).ToList();
            m.bots = m.bots.Where(p => pb.Contains(p) && FBNav.Inside(p)).ToList();
            if (m.spawnA.Count > 0 && m.spawnB.Count > 0 && m.points.Count > 0 && m.waypoints.Count > 0 && m.bots.Count > 0) return;
            var raw = new List<Vector3>(); for (int i = 0; i < 900; i++) if (FBNav.RandomPoint() is Vector3 p && pb.Contains(p) && FBNav.Inside(p) && !FBWorld.RayHit(p + Vector3.up * .5f, Vector3.up, 2.5f)) raw.Add(p + Vector3.up * .1f);
            if (raw.Count < 8) return; var c = raw.Aggregate(Vector3.zero, (a, b) => a + b) / raw.Count; var ds = raw.Select(p => (p - c).Flat().magnitude).OrderBy(x => x).ToList(); float lim = ds[ds.Count / 2];
            var ground = raw.Where(p => (p - c).Flat().magnitude <= lim).OrderBy(p => p.y).ToList(); ground = ground.Take(Mathf.Max(8, ground.Count * 4 / 5)).ToList();
            float D2(Vector3 a, Vector3 b) => (a - b).Flat().sqrMagnitude;
            var e1 = ground[0]; foreach (var p in ground) if (D2(p, ground[0]) > D2(e1, ground[0])) e1 = p; var e2 = ground[0]; foreach (var p in ground) if (D2(p, e1) > D2(e2, e1)) e2 = p;
            List<Vector3> Near(Vector3 cc, int n) => ground.OrderBy(a => D2(a, cc)).Take(n).ToList();
            if (m.spawnA.Count == 0) m.spawnA = Near(e1, 5); if (m.spawnB.Count == 0) m.spawnB = Near(e2, 5);
            List<Vector3> Fps(int n, List<Vector3> avoid) { var cand = ground.Where(p => avoid.All(a => D2(p, a) > 400)).ToList(); if (cand.Count == 0) cand = ground; var o = new List<Vector3> { cand[cand.Count / 2] }; while (o.Count < n) { var best = cand.OrderByDescending(p => o.Min(q => D2(q, p))).First(); o.Add(best); } return o; }
            if (m.points.Count == 0) m.points = Fps(5, m.spawnA.Concat(m.spawnB).ToList()); if (m.waypoints.Count == 0) m.waypoints = Fps(Mathf.Min(20, ground.Count), new List<Vector3>()); if (m.bots.Count == 0) m.bots = Fps(Mathf.Min(18, ground.Count), m.spawnA);
        }

        // ---- bases e formação
        public List<Vector3> TeamSpawns(int t)
        {
            var m = Map; var pol = Json.L(Level, "spawnPolice"); string ps = Json.S(Level, "policeSpawn");
            List<Vector3> side;
            if (FBSession.mode == "pvl" && pol != null && pol.Count > 0) { int p = ps == "B" ? 0 : 1; side = t == p ? pol.Select(o => Coords.FromWeb(o as List<object>)).ToList() : m.spawnA; }
            else side = ((FBSession.mode == "pvl" && ps == "B" ? 1 - t : t) == 0) ? m.spawnA : m.spawnB;
            return side.Count > 0 ? side : m.bots;
        }
        public class Line { public List<Vector3> spots = new(); public float yaw; public int next; }
        public readonly Line[] teamLine = new Line[2];
        public FBArena arena;                                                          // r47: área de combate do Clássico/4X4 (null nos outros modos)
        public void PickBases()
        {
            int my = FBSession.team, en = 1 - my;
            if (arena != null) { teamLine[my] = LineSpots(arena.a, 8, arena.b); teamLine[en] = LineSpots(arena.b, 8, arena.a); return; }   // bases opostas DENTRO da área
            var A = TeamSpawns(my); var B = TeamSpawns(en); if (A.Count == 0 || B.Count == 0) return;
            float best = -1; Vector3 ba = A[0], bb = B[0]; foreach (var a in A) foreach (var b in B) { float d = (a - b).Flat().magnitude; if (d > best) { best = d; ba = a; bb = b; } }
            var good = new List<(Vector3, Vector3)>(); foreach (var a in A) foreach (var b in B) if ((a - b).Flat().magnitude >= Mathf.Max(35, best * .82f)) good.Add((a, b));
            var ch = good.Count > 0 ? good[Random.Range(0, good.Count)] : (ba, bb);
            teamLine[my] = LineSpots(ch.Item1, 8, ch.Item2); teamLine[en] = LineSpots(ch.Item2, 8, ch.Item1);
        }

        // Validação completa de pontos de nascimento (física, terreno, limites do mapa, área de combate e separação)
        public static bool ValidateSpawnSpot(Vector3 cand, out Vector3 valid, FBArena ar = null, List<Vector3> avoid = null, float minSep = 1.35f)
        {
            valid = cand;
            string curMap = I ? I.mapId : FBSession.map;
            var pb = FBWorld.GetPlayableBounds(curMap);

            bool Test(Vector3 p, out Vector3 res)
            {
                res = p;
                if (p.x < pb.min.x + 2.2f || p.x > pb.max.x - 2.2f || p.z < pb.min.z + 2.2f || p.z > pb.max.z - 2.2f) return false;
                if (ar != null && !ar.Inside(p, 3.2f)) return false;
                if (I != null && I.mode != null && I.mode.Zone is FBZone.Z z && z.r > 2f)
                {
                    if ((p - z.c).Flat().magnitude > z.r - 2.5f) return false;
                }
                if (!UnityEngine.AI.NavMesh.SamplePosition(p, out var nh, 4f, UnityEngine.AI.NavMesh.AllAreas)) return false;
                if (!FBNav.Inside(nh.position)) return false;
                var gy = FBWorld.GroundY(nh.position.x, nh.position.y + 1.8f, nh.position.z, .35f, 3.6f);
                if (gy == null || Mathf.Abs(gy.Value - nh.position.y) > 1.8f) return false;
                var pt = new Vector3(nh.position.x, gy.Value, nh.position.z);
                if (Physics.CheckCapsule(pt + Vector3.up * .35f, pt + Vector3.up * 1.5f, .34f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore)) return false;
                if (Physics.Raycast(pt + Vector3.up * .4f, Vector3.up, 1.45f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore)) return false;
                if (avoid != null)
                {
                    foreach (var a in avoid) if ((a - pt).Flat().magnitude < minSep) return false;
                }
                res = pt; return true;
            }

            if (Test(cand, out valid)) return true;
            float[] radii = { 1.2f, 2.2f, 3.5f, 5.0f };
            for (int rIdx = 0; rIdx < radii.Length; rIdx++)
            {
                float rad = radii[rIdx]; int steps = 8 + rIdx * 4;
                for (int i = 0; i < steps; i++)
                {
                    float ang = (i / (float)steps) * Mathf.PI * 2f;
                    var testP = cand + new Vector3(Mathf.Cos(ang) * rad, 0, Mathf.Sin(ang) * rad);
                    if (Test(testP, out valid)) return true;
                }
            }
            if (ar != null && Test(ar.Home(FBSession.team), out valid)) return true;
            if (avoid != null && avoid.Any(a => (a - cand).Flat().magnitude < minSep))
            {
                var dir = (cand - avoid[0]).Flat();
                if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
                valid = avoid[0] + dir.normalized * (minSep + 0.2f);
                return false;
            }
            valid = cand; return false;
        }

        static readonly Vector2[] OFFS = { new(0, 0), new(1.8f, -.4f), new(-1.8f, -.4f), new(0, -1.6f), new(3.2f, -.8f), new(-3.2f, -.8f), new(1.8f, -2.2f), new(-1.8f, -2.2f) };
        Line LineSpots(Vector3 b, int n, Vector3? lookAt)
        {
            ValidateSpawnSpot(b, out b, arena);
            float yaw = lookAt is Vector3 la ? Quaternion.LookRotation((la - b).Flat()).eulerAngles.y : SpawnYaw(b);
            var q = Quaternion.Euler(0, yaw, 0); var r = q * Vector3.right; var f = q * Vector3.forward; var L = new Line { yaw = yaw };
            foreach (var off2 in OFFS)
            {
                if (L.spots.Count >= n) break;
                var cand = b + r * off2.x + f * off2.y;
                if (ValidateSpawnSpot(cand, out var validSpot, arena, L.spots, 1.35f))
                    L.spots.Add(validSpot);
            }
            if (L.spots.Count < n)
            {
                float[] radii = { 1.5f, 2.8f, 4.2f, 5.8f };
                foreach (var rad in radii)
                {
                    if (L.spots.Count >= n) break;
                    for (int k = 0; k < 12 && L.spots.Count < n; k++)
                    {
                        float ang = (k / 12f) * Mathf.PI * 2f;
                        var dir = (r * Mathf.Cos(ang) + f * Mathf.Sin(ang)).normalized;
                        if (Vector3.Dot(dir, f) > .4f) continue;
                        var cand = b + dir * rad;
                        if (ValidateSpawnSpot(cand, out var validSpot, arena, L.spots, 1.35f))
                            L.spots.Add(validSpot);
                    }
                }
            }
            while (L.spots.Count < n) L.spots.Add(L.spots.Count > 0 ? L.spots[0] : b);
            return L;
        }
        public Vector3 NextSpot(int team)
        {
            var L = teamLine[team] ??= LineSpots(FBUtil.Pick(TeamSpawns(team)), 8, null);
            var p = L.spots[L.next % L.spots.Count];
            L.next++;
            if (ValidateSpawnSpot(p, out var valid, arena)) return valid;
            return p;
        }
        public float SpawnYaw(Vector3 sp)
        {
            var m = Map; var aim = m.points.Count > 0 ? m.points[0] : m.bots.Count > 0 ? m.bots[0] : Vector3.zero; float goal = FBUtil.YawOf(aim - sp); var o = sp + Vector3.up * 1.5f; float best = goal, bs = -1e9f;
            for (int i = 0; i < 32; i++) { float a = i / 32f * Mathf.PI * 2; float d = FBWorld.RayHit(o, new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)), 30, out var h) ? h.distance : 30; float sc = Mathf.Min(d, 20) + Mathf.Cos(FBUtil.WrapA(a - goal)) * 4; if (sc > bs) { bs = sc; best = a; } }
            return best * Mathf.Rad2Deg;
        }

        // ---- personagens
        public FBCharacter Spawn(string skin, string look, int team, Vector3 pos, bool player, bool bot = false, bool civ = false)
        {
            var prefab = FBRegistry.I ? FBRegistry.I.Char(skin) : null; if (!prefab) prefab = FBRegistry.I.Char(FBRegistry.I.charIds[0]);
            var go = Instantiate(prefab, pos + Vector3.up * .05f, Quaternion.Euler(0, Random.Range(0, 360), 0)); go.name = (player ? "Jogador_" : civ ? "Civil_" : "Bot_") + skin;
            SetLayer(go, FBWorld.LChar);
            var ch = go.GetOrAdd<FBCharacter>(); ch.team = team; ch.skinId = skin; ch.isPlayer = player; ch.isBot = bot; ch.civ = civ;
            FBWardrobe.ApplyTo(go, player ? look : null, !player && !GameData.Skins.Exists(s => s.id == skin && !string.IsNullOrEmpty(s.npc2)));   // NPCs do pacote novo: visual ORIGINAL (sem sortear tom de pele/cor)
            var cc = go.AddComponent<CharacterController>(); float s = 1f / go.transform.lossyScale.y;
            float H = Json.F(GameData.Section("controller"), "m_Height", 1.8f), R = Json.F(GameData.Section("controller"), "m_Radius", .3f);
            cc.height = H * s; cc.radius = Mathf.Max(R, .32f) * s; cc.center = new Vector3(0, H * .5f * s, 0); cc.stepOffset = .45f * s; cc.slopeLimit = 50; cc.skinWidth = .04f * s; cc.minMoveDistance = 0;
            var wu = go.AddComponent<FBWeaponUser>();
            FBHitbox.Build(ch);
            if (player) go.AddComponent<FBPlayer>(); else if (bot) go.AddComponent<FBBot>(); else if (civ) go.AddComponent<FBCivilian>();
            ch.onDeath = OnDeathAnim; All.Add(ch); if (bot) Bots.Add(ch); if (civ) Civs.Add(ch);
            return ch;
        }
        static void SetLayer(GameObject g, int l) { foreach (var t in g.GetComponentsInChildren<Transform>(true)) if (!t.GetComponent<FBHitbox>()) t.gameObject.layer = l; }
        public static void Teleport(FBCharacter c, Vector3 p, float yawDeg)
        {
            var cc = c.GetComponent<CharacterController>(); if (cc) cc.enabled = false; c.transform.SetPositionAndRotation(p + Vector3.up * .05f, Quaternion.Euler(0, yawDeg, 0)); if (cc) cc.enabled = true; c.velocity = Vector3.zero;
        }

        // makeBot: papel tático (faixa de flanco, ritmo, colete do "barril"), arma do time, proteção de spawn no meio da partida
        static readonly string[] SQUAD_ROLES = { "rusher", "flanker_left", "flanker_right", "tactical", "striker", "baianogmbarril", "halemph", "halemph2" };
        public FBCharacter MakeBot(string type, int team, Vector3 p, string ai, string forcedRole = null)
        {
            var c = Spawn(type, null, team, p, false, true); c.transform.rotation = Quaternion.Euler(0, Random.Range(-170f, 170f), 0);
            var b = c.GetComponent<FBBot>(); string role = forcedRole ?? SQUAD_ROLES[Random.Range(0, SQUAD_ROLES.Length)];
            float lane = role switch { "rusher" => FBUtil.Rnd(-4, 4), "striker" => FBUtil.Rnd(-3, 3), "baianogmbarril" => FBUtil.Rnd(-10, 10), "halemph" => FBUtil.Rnd(-20, 20), "halemph2" => FBUtil.Rnd(-26, 26), "flanker_left" => FBUtil.Rnd(-22, -14), "flanker_right" => FBUtil.Rnd(14, 22), _ => FBUtil.Rnd(-8, 8) };
            b.Setup(ai ?? "patrol", role, lane, p); c.kits = FBEconomy.EconMode(FBSession.mode) ? 1 : FBMed.Start;
            if (role == "baianogmbarril") { c.armor = 60; c.helmet = true; }
            if (team != 2) FBEconomy.ArmBot(c); c.combat = c.weapon != null;
            if (state == "play") Protect(c);
            return c;
        }
        public void SpawnSquad(int team, int n, string ai)
        {
            for (int i = 0; i < n; i++) { var c = MakeBot(typePool[team][i % typePool[team].Count], team, NextSpot(team), ai, SQUAD_ROLES[i % SQUAD_ROLES.Length]); c.transform.rotation = Quaternion.Euler(0, teamLine[team].yaw, 0); }
        }
        List<Vector3> EnemySpawns(int en)
        {
            Vector3 Cen(List<Vector3> a) => a.Aggregate(Vector3.zero, (s, p) => s + p.Flat()) / Mathf.Max(1, a.Count);
            var mine = Cen(TeamSpawns(1 - en)); var theirs = Cen(TeamSpawns(en)); var friends = All.Where(a => a && a.alive && a.team != en).ToList();
            bool Ok(Vector3 p) { var q = p.Flat(); return (q - mine).magnitude > (q - theirs).magnitude * 1.35f + 20 && (q - mine).magnitude > 60 && friends.All(f => (f.Pos.Flat() - q).magnitude > 45); }
            var bs = TeamSpawns(en).Where(Ok).ToList(); var ex = Map.bots.Where(Ok).ToList();
            return bs.Count > 0 ? bs.Concat(ex).ToList() : ex.Count > 0 ? ex : TeamSpawns(en);
        }
        public void Reinforce(int n)
        {
            int en = 1 - FBSession.team; var sp = EnemySpawns(en);
            for (int i = 0; i < n && sp.Count > 0; i++) { var b = MakeBot(FBUtil.Pick(typePool[en]), en, FBUtil.Pick(sp), "hunt"); b.GetComponent<FBBot>().lastSeen = Player.Pos; }
            Feed($"Reforços ({TEAM_NAME[en]}) chegando!");
        }
        public void Protect(FBCharacter a) { if (mode != null && mode.respawn && FBSession.mode != "treino") a.protUntil = Time.time + FBMechanics.RollProtection(); }

        // civis (PedestrianManager do original)
        void SpawnCivilians(int n)
        {
            var types = FBNpcAnims.Roster(FBSession.skin).OrderBy(_ => Random.value).ToList(); if (types.Count == 0) return;   // civis = pacote novo de NPCs, cada um uma vez só
            for (int i = 0; i < Mathf.Min(n, types.Count); i++) { if (!(FBNav.RandomPoint() is Vector3 p)) continue; var c = Spawn(types[i], null, 2, p, false, false, true); c.hp = 60; c.maxHp = 60; }
        }

        // ---- abates
        void OnDeathAnim(FBCharacter dead, FBCharacter by) { Invoke2(1.1f, () => { if (dead && dead.Hips && FBWorld.GroundY(dead.Hips.position.x, dead.Hips.position.y + .5f, dead.Hips.position.z, .3f, 3) is float gy) FBFx.BloodPool(new Vector3(dead.Hips.position.x, gy, dead.Hips.position.z)); }); }
        public void OnKill(FBCharacter a, FBCharacter src, bool head, string why)
        {
            if (a.weapon != null && !a.isPlayer) { FBLoot.DropWeapon(a.weapon.id, a.Pos + new Vector3(Random.Range(-.5f, .5f), 0, Random.Range(-.5f, .5f))); a.SetWeaponVisible(false); if (Random.value < .4f) FBLoot.DropRandomAtt(a.weapon, a.Pos); }
            if (mode != null && mode.econ && src && src != a && Hostile(src, a)) FBEconomy.Give(src, FBEconomy.KILL + (head ? FBEconomy.HEAD : 0), head ? "HEADSHOT" : "ABATE");
            if (src && src.isPlayer && a != src) RewardKill(a.Name, head);
            FBKillFeed.Kill(src, a, why, head, src && src.weapon != null ? src.weapon.id : null);
            if (!a.civ) { Tally.TryGetValue(a.Name, out var ta); Tally[a.Name] = (a.team, ta.k, ta.d + 1); }
            if (src && src != a && !src.civ) { Tally.TryGetValue(src.Name, out var ts); Tally[src.Name] = (src.team, ts.k + 1, ts.d); }
            mode?.OnKill(a, src, head); TeamWipeCheck(a);
            if (a.isPlayer) PlayerDie(src, why, head);
            FBNet.I?.SendDie(a, src, head);
        }
        int streakN; float streakT; public FBKillstreak ks = new();
        void RewardKill(string name, bool head)
        {
            Stats.kills++; if (head) Stats.heads++; ks.Kill(Time.time); FBPass.AddXP(head ? 150 : 100, head ? "HEADSHOT" : "ABATE");
            var wu = Player.GetComponent<FBWeaponUser>(); var cw = wu.Cur; string wn = cw != null ? cw.name.ToUpper() : "MÃOS LIVRES"; if (cw != null) wu.reserve[wu.cur] += Mathf.Max(10, cw.mag);
            FBHUD.I?.Toast(head ? "HEADSHOT" : "ELIMINADO", name.ToUpper(), cw != null ? $"{wn} · +MUNIÇÃO" : wn, head ? new Color(1, .3f, .25f) : new Color(.94f, .71f, .16f));
            streakN = streakT > 0 ? streakN + 1 : 1; streakT = 4.5f;
            string[] SN = { "", "ABATE", "ABATE DUPLO", "ABATE TRIPLO", "ABATE QUÁDRUPLO", "MASSACRE" };
            FBHUD.I?.KillBanner(Stats.kills, streakN >= 2 ? (streakN < SN.Length ? SN[streakN] : "IMPARÁVEL") : head ? "HEADSHOT!" : "ABATE", $"{name.ToUpper()} · {wn}", streakN >= 2 ? new Color(.96f, .67f, .09f) : head ? new Color(1, .35f, .29f) : Color.white);
            // sem fala de rádio a cada 5 abates (pedido do usuário: nada de voz de locutor/rádio)
        }
        void TeamWipeCheck(FBCharacter victim)
        {
            if (FBSession.mode == "treino" || FFA) return;
            var foes = Bots.Where(b => b && b.team != FBSession.team).ToList(); var mates = All.Where(x => x && !x.civ && x.team == FBSession.team).ToList();
            if (victim.team != FBSession.team && foes.Count >= 2 && foes.All(b => !b.alive)) { FBHUD.I?.WipeBanner("EQUIPE INIMIGA ELIMINADA", "TODOS OS INIMIGOS FORAM ABATIDOS", new Color(.96f, .67f, .09f)); }
            else if (victim.team == FBSession.team && mates.All(m => !m.alive)) FBHUD.I?.WipeBanner("SUA EQUIPE FOI ELIMINADA", "NENHUM ALIADO DE PÉ", new Color(1, .29f, .23f));
        }
        void PlayerDie(FBCharacter src, string why, bool head = false)
        {
            ks.Died(); Stats.deaths++; FBAudio.Play2D("ie_knocked", .45f); FBVehicles.ExitIfDriving(Player);
            FBHUD.I?.Killcam(src ? src.Name.ToUpper() : why == "zone" ? "ZONA" : why == "fall" ? "QUEDA" : "—", true);
            // r49: espectador (FBSpectator) em todos os modos menos o De Rolê — sem renascer: depois do "ABATIDO POR"; com renascer: junto da tela de abate
            if (mode != null && !mode.respawn) { state = "spect"; spect = null; Invoke2(2f, () => { if (state == "spect" && !Player.alive) FBSpectator.Begin(src); }); Invoke2(3.5f, () => FBHUD.I?.Killcam("", false)); return; }
            state = "dead"; respawnT = mode != null && mode.autoRespawn > 0 ? mode.autoRespawn + 2.2f : -1;
            Invoke2(2.2f, () => { if (state == "dead") { FBHUD.I?.Killcam("", false); FBHUD.I?.DeathScreen(respawnT > 0, Respawn, ToMenu, src, why, head); FBSpectator.Begin(src); } });
        }
        public void Respawn()
        {
            if (state != "dead") return; var b = mode?.SpawnPoint() ?? (teamLine[FBSession.team] != null ? teamLine[FBSession.team].spots[0] : FBUtil.Pick(TeamSpawns(FBSession.team)));
            ValidateSpawnSpot(b, out var sp, arena); float yaw = SpawnYaw(sp);
            Player.Revive(); Teleport(Player, sp, yaw); Player.hp = 100; FBMed.ResetPlayer(FBMed.Start, FBSession.mode == "treino");
            var wu = Player.GetComponent<FBWeaponUser>(); wu.RefillAll(3); wu.Equip(wu.cur, true); FBPlayer.I?.OnRespawn(yaw);
            state = "play"; FBSpectator.End(); FBHUD.I?.CloseCards(); FBCombat.EndKnock(Player); Protect(Player); FBNet.I?.SendSpawn(Player);
        }
        // rodada nova (4V4, Farinha × Buriti): todos vivos e cheios nas bases; quem morreu no 4V4 perde tudo
        public void ResetRound()
        {
            PickBases(); FBLoot.Clear(); bool econ = mode != null && mode.econ, playerDead = !Player.alive;
            // [parceiro] fisica 31/10: x.team < 2 deixa de fora atores de evento de rua (atirador do baculejo, time 2, civ=false); teamLine[2] lancava IndexOutOfRange
            foreach (var a in new[] { Player }.Concat(All.Where(x => x && x != Player && !x.civ && x.team < 2)).ToList())
            {
                if (a.knocked || a.getUpT > 0 || a.sys != null) { FBCombat.EndKnock(a); a.getUpT = 0; a.bleedAcc = 0; }
                if (a.isRemote) continue;
                var p = NextSpot(a.team);
                if (econ && !a.alive) { a.armor = 0; a.helmet = false; if (!a.isPlayer) FBEconomy.UnarmBot(a); }
                if (econ && !a.isPlayer) a.kits = Mathf.Max(a.alive ? a.kits : 0, 1);
                if (!a.alive || a.hp < 100) { if (!a.alive) a.Revive(); a.alive = true; a.hp = 100; }
                Teleport(a, p, teamLine[a.team].yaw);
                if (!a.isPlayer) a.GetComponent<FBBot>()?.ResetRound();
            }
            FBPlayer.I?.OnRespawn(teamLine[FBSession.team].yaw);
            var wu = Player.GetComponent<FBWeaponUser>();
            if (econ && playerDead) wu.Strip();
            if (econ) FBMed.ResetPlayer(Mathf.Max(playerDead ? 0 : FBMed.kits, 1), false);
            wu.RefillAll(4); spect = null; FBSpectator.End(); if (state == "spect" || state == "dead") state = "play";
            FBHUD.I?.Killcam("", false); FBHUD.I?.CloseCards(); wu.Equip(wu.cur, true);
        }
        public void OnLive()
        {
            if (mode == null || !mode.econ || !Player.alive || !Player.GetComponent<FBWeaponUser>().Unarmed) return;
            FBHUD.I?.Toast("VOCÊ ESTÁ DESARMADO", "PEGUE UMA ARMA DE UM INIMIGO ABATIDO", "Na próxima rodada, compre na loja (B)", new Color(1, .3f, .25f)); FBAudio.Synth("deny");
        }
        public void EndMatch(bool win, string modeName, int a, int b, int place = 0, int total = 0)
        {
            if (state == "end") return; state = "end"; freeze = true; FBSpectator.End();
            if (win && Player.alive) { FBPlayer.I?.ForceThirdPerson(); Player.PlayDance(FBSession.victory != null && FBRegistry.I.Clip(FBSession.victory) ? FBSession.victory : FBEmotes.RandomVictory()); }
            foreach (var bt in Bots) if (bt && bt.alive && (bt.team == FBSession.team) == win && Random.value < .85f) bt.PlayDance(FBEmotes.RandomVictory(bt.IsFemale));
            int reward = Stats.kills * 60 + (win ? 500 : 150); FBSession.money += reward; FBSession.xp += Stats.kills * 10 + (win ? 60 : 20); FBSession.Save(); FBPass.AddXP(win ? 500 : 200, win ? "VITÓRIA" : "PARTIDA");
            // sem falas de rádio no fim da partida ("vo_win"/"vo_done", pedido do usuário) — fica só a vinheta do placar
            Invoke2(win ? 4.2f : 1.8f, () => FBHUD.I?.VictoryCard(win, modeName, MapName, a, b, place, total, reward, () => SceneManager.LoadScene(SceneManager.GetActiveScene().name), ToMenu));
        }
        public void ToMenu() { FBAudio.StopAllLoops(); FBAmbience.Stop(); FBNet.I?.Leave(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; SceneManager.LoadScene("Menu"); }

        // [parceiro 01/11] cronômetro por script (FBTempo, ligado só pelo teste do .exe); desligado custa só um teste de bool
        static readonly int __fbt_Update = FBTempo.Id("FBMatch.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            float dt = Time.deltaTime;
            if (FBInput.PausePressed()) { Paused = !Paused; FBHUD.I?.Pause(Paused, ToMenu); Cursor.lockState = Paused || FBGarage.Open_ ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = Paused || FBGarage.Open_; }
            if (state != "loading" && (FBInput.Read().map || (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.mKey.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.M))) { FBMatchMap.Toggle(); }
            if (state == "loading" || mode == null) return;
            if (streakT > 0) streakT -= dt;
            if (state == "play" || state == "dead" || state == "spect") { mode.Update(dt); mode.Hud(); }
            if (state == "dead" && respawnT > 0) { respawnT -= dt; if (respawnT <= 0) Respawn(); }
            if (Player && Player.knocked) FBCombat.KnockTick(Player, dt);
            var k = ks.Tick(Time.time); if (k != null) FBHUD.I?.Toast("KILLSTREAK", FBKillstreak.STREAK[k].name + " DISPONÍVEL", "APERTE G PARA CHAMAR", new Color(.96f, .67f, .09f));
            if (Player && Player.alive && Player.Pos.y < FBMechanics.VoidY) FBCombat.DamageActor(Player, 999, false, null, "fall");
            for (int i = timers.Count - 1; i >= 0; i--) { var (t, f) = timers[i]; t -= dt; if (t <= 0) { timers.RemoveAt(i); f(); } else timers[i] = (t, f); }
        }
        public FBCharacter Spectating => FBSpectator.On ? FBSpectator.Target : spect;
        readonly List<(float, System.Action)> timers = new();
        public void Invoke2(float t, System.Action f) => timers.Add((t, f));
        void OnDestroy() { mode?.Dispose(); FBSpectator.End(); if (I == this) I = null; }
    }
}
