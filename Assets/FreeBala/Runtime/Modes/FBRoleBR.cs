// BATTLE ROYALE (porte do br.js com as regras do brcore.js): avião cruza a ilha numa rota reta perto do 1º círculo (airline), todos a
// bordo; Espaço salta; queda livre (dive) com mergulho acelerado (surf, Shift) acima de 160 m; paraquedas abre sozinho em 10 s ou perto do
// chão (35 m) e controla pela frente/trás; pouso com tremor; bots saltam no ponto da rota mais perto do destino. Loot de armas/coletes
// espalhado pelo mapa; bot sem arma vai pegar a mais perto.
// DE ROLÊ: modo livre (sem armas e sem HUD): carros estacionados liberados, NPCs andando, sentando e curtindo perto dos paredões.
// CIVIS (PedestrianManager/PedestrianAimWatcher do original): andam pela navmesh, conversam em dupla, fogem de tiro, se rendem sob mira.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public static class FBBattleRoyale
    {
        class S { public string st = "plane"; public Vector2 hv; public float fallT, jumpAt = -1; }
        static readonly Dictionary<FBCharacter, S> st = new(); static FBBrCore.Route route; static float planeT, planeSpeed = 60, alt = 220; static GameObject plane; static bool on; public static bool PlayerHigh => on && FBMatch.Player && st.TryGetValue(FBMatch.Player, out var s) && s.st != "land";
        public static bool Active(FBCharacter c) => on && c && st.TryGetValue(c, out var s) && s.st != "land";
        public static bool InPlane(FBCharacter c) => on && c && st.TryGetValue(c, out var s) && s.st == "plane";
        public static bool Airborne() => on && st.Any(kv => kv.Key && kv.Key.alive && kv.Value.st != "land");
        static Vector3 PlanePos => new Vector3(route.a.x + route.dir.x * planeT, alt, route.a.y + route.dir.y * planeT);
        public static void Setup(FBMatch G, FBZone.Z zone)
        {
            on = true; st.Clear(); route = FBBrCore.Airline(zone.c.x, zone.c.z, zone.r, null, .25f); planeT = 0; alt = zone.c.y + 180;
            plane = GameObject.CreatePrimitive(PrimitiveType.Capsule); Object.Destroy(plane.GetComponent<Collider>()); plane.name = "Aviao"; plane.transform.localScale = new Vector3(4, 14, 4);
            var p787 = FBSkyPlanes.Boeing(); if (p787) { Object.Destroy(plane); plane = p787; }   // Boeing 787 do br.js (Acc/boeing787)
            foreach (var a in FBMatch.Actors()) { var s = new S(); if (!a.isPlayer) s.jumpAt = FBBrCore.JumpPointFor(route, a.Pos.x + Random.Range(-60f, 60f), a.Pos.z + Random.Range(-60f, 60f)) + Random.Range(-20f, 20f); st[a] = s; Hide(a, true); }
            FBLoot.Clear(); var pts = FBNav.Points(); var ids = GameData.Weapons.Keys.Where(id => FBRegistry.I.Weapon(id) && GameData.Weapons[id].cls != "explosivo").ToList();
            for (int i = 0; i < Mathf.Min(140, pts.Count); i++) { var p = pts[(i * 7) % pts.Count] + new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f)); if (Random.value < .72f) FBLoot.DropWeapon(FBUtil.Pick(ids), p); else FBLoot.DropArmor(FBUtil.Pick(new[] { "vest1", "vest2", "vest3", "helmet1", "helmet2", "helmet3" }), p); }
        }
        static void Hide(FBCharacter a, bool h) { foreach (var r in a.GetComponentsInChildren<Renderer>(true)) r.enabled = !h; var cc = a.GetComponent<CharacterController>(); if (cc) cc.enabled = !h; }
        public static void Tick(float dt)
        {
            if (!on) return; planeT += planeSpeed * dt; if (plane) { plane.transform.position = PlanePos; plane.transform.rotation = Quaternion.LookRotation(new Vector3(route.dir.x, 0, route.dir.y)) * (plane.name == "Aviao" ? Quaternion.Euler(90, 0, 0) : Quaternion.identity); }
            if (planeT > route.len) foreach (var kv in st.ToList()) if (kv.Key && kv.Value.st == "plane") Jump(kv.Key);           // fim da rota: todo mundo pula
            if (planeT > route.len + 400 && plane) Object.Destroy(plane);
        }
        static void Jump(FBCharacter a) { var s = st[a]; s.st = "dive"; s.fallT = 0; s.hv = Vector2.zero; FBMatch.Teleport(a, PlanePos - new Vector3(route.dir.x, 0, route.dir.y) * 12 + Vector3.down * 4, a.Yaw); Hide(a, false); var cc = a.GetComponent<CharacterController>(); if (cc) cc.enabled = false; a.sys = FBRegistry.I.Clip("FF_SYS_BR_Fall") ? "FF_SYS_BR_Fall" : "FF_SYS_Prone"; }
        static bool Fall(FBCharacter a, float dt, float fwd, float side, bool surf, bool open, float lookYawDeg)
        {
            var s = st[a]; if (s.st == "plane") { a.transform.position = PlanePos; return true; }
            float hAbove = FBWorld.RayHit(a.Pos, Vector3.down, 2000, out var g) ? g.distance : 999;
            if (s.st == "dive" || s.st == "surf") { s.fallT += dt; s.st = surf && FBBrCore.CanSurf(hAbove) ? "surf" : "dive"; if (open || FBBrCore.ShouldOpen(s.fallT, hAbove)) { s.st = "para"; FBAudio.PlayAt("ie_slide", a.Pos, .6f, 8); } }
            var vel = FBBrCore.SkyVelocity(s.st, fwd, s.hv, dt); s.hv = vel; float turn = FBBrCore.SkyTurnRate(s.st, vel.x) * Mathf.Rad2Deg * dt;
            a.Yaw = Mathf.MoveTowardsAngle(a.Yaw, lookYawDeg, turn * 4 + Mathf.Abs(side) * turn);
            var f = Quaternion.Euler(0, a.Yaw, 0) * Vector3.forward; a.transform.position += (f * vel.x + Vector3.up * vel.y) * dt;
            a.sys = s.st == "para" ? (FBRegistry.I.Clip("FF_SYS_BR_Parachute") ? "FF_SYS_BR_Parachute" : "FF_SYS_Prone") : FBRegistry.I.Clip("FF_SYS_BR_Fall") ? "FF_SYS_BR_Fall" : "FF_SYS_Prone";
            a.velocity = f * vel.x + Vector3.up * vel.y; a.grounded = false;
            if (hAbove < .6f) { s.st = "land"; a.sys = null; var cc = a.GetComponent<CharacterController>(); if (cc) cc.enabled = true; FBAudio.PlayAt("ie_land", a.Pos, .7f, 6); if (a.isPlayer) FBHUD.I?.Damaged(.1f); }
            return true;
        }
        public static bool StepPlayer(FBCharacter p, float dt, FBInputState inp, float camYaw)
        {
            if (!st.TryGetValue(p, out var s) || s.st == "land") return false;
            if (s.st == "plane") { p.transform.position = PlanePos; if (inp.jump) Jump(p); FBHUD.I?.SetBox("ESPAÇO = SALTAR DO AVIÃO", true); return true; }
            FBHUD.I?.SetBox(s.st == "para" ? "PARAQUEDAS · W/S CONTROLA" : "QUEDA LIVRE · ESPAÇO ABRE O PARAQUEDAS · SHIFT MERGULHA", true);
            return Fall(p, dt, inp.move.y, inp.move.x, inp.sprintHeld, inp.jump, camYaw);
        }
        public static bool StepBot(FBCharacter b, float dt)
        {
            if (!st.TryGetValue(b, out var s) || s.st == "land") return false;
            if (s.st == "plane") { b.transform.position = PlanePos; if (planeT >= s.jumpAt) Jump(b); return true; }
            return Fall(b, dt, s.st == "para" ? .4f : 1, 0, false, false, b.Yaw);
        }
        public static bool Camera(Camera cam, float dt, float yaw, float pitch, FBCharacter p)
        {
            if (!Active(p)) return false; var s = st[p]; float up = s.st == "para" ? FBBrCore.CamParaUp : FBBrCore.CamDiveUp, back = s.st == "para" ? FBBrCore.CamParaBack : s.st == "plane" ? 30 : FBBrCore.CamDiveBack + 4;
            var rot = Quaternion.Euler(-pitch + 15, yaw, 0); var piv = (s.st == "plane" ? PlanePos : p.Pos) + Vector3.up * up; cam.transform.position = piv - rot * Vector3.forward * back; cam.transform.rotation = Quaternion.LookRotation(piv - cam.transform.position); return true;
        }
        // bot sem arma: vai até o saque mais perto e pega
        public static bool BotLoot(FBCharacter b, FBBot bot, float dt)
        {
            FBLoot.L best = null; float bd = 60; foreach (var l in FBLoot.loots) { if (l.id == null || !l.obj) continue; float d = (l.pos - b.Pos).magnitude; if (d < bd) { bd = d; best = l; } }
            if (best == null) return false; if (bd < 1.5f) { Object.Destroy(best.obj); FBLoot.loots.Remove(best); b.GetComponent<FBWeaponUser>().Give(best.id); return false; }
            bot.goalP = best.pos; return false;
        }
        public static void End() { on = false; st.Clear(); if (plane) Object.Destroy(plane); }
    }

    public class FBCivilian : MonoBehaviour
    {
        FBCharacter c; CharacterController cc; bool cvGround = true; float cvSlide, cvStep = -1; FBMotor.Ground cvG; string st = "walk"; float stT, vy; Vector3? goal; Vector3 vel; List<Vector3> path; float navT;
        void Awake() { c = GetComponent<FBCharacter>(); cc = GetComponent<CharacterController>(); c.civ = true; c.combat = false; stT = Random.Range(2f, 6f); }
        public void OnHit() { c.Trigger("hit"); st = "flee"; stT = 6; goal = FBNav.RandomPoint(c.Pos, 30) ?? c.Pos; }
        bool AimedAt()
        {
            var p = FBMatch.Player; var cam = UnityEngine.Camera.main; if (!p || !p.alive || !cam || !(p.aiming || p.lastShot < 1.2f)) return false;
            if ((c.Pos - p.Pos).magnitude > 32) return false; var f = cam.transform.forward; var to = c.Pos + Vector3.up * 1.2f * c.ScaleK - cam.transform.position; float al = Vector3.Dot(to, f); return al > 0 && (to - f * al).magnitude < 1.3f;
        }
        // [parceiro 01/11] cronômetro por script (FBTempo, ligado só pelo teste do .exe); desligado custa só um teste de bool
        static readonly int __fbt_Update = FBTempo.Id("FBCivilian.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            float dt = Time.deltaTime; if (dt <= 0) return;
            if (!c.alive) { vel = Vector3.zero; c.velocity = vel; return; }
            stT -= dt; bool noise = FBBotBrain.lastNoiseP is Vector3 np && Time.time - FBBotBrain.lastNoiseT < 2.5f && (np - c.Pos).magnitude < 40;
            float speed = 0; Vector3? desired = null;
            if (noise && st != "flee") { FBNpcVoz.Say(c, "perigo", 1, 5, 6, .5f, Random.Range(0f, .6f)); st = "flee"; stT = Random.Range(5f, 9f); var away = (c.Pos - FBBotBrain.lastNoiseP.Value).Flat().normalized * 30; goal = FBNav.RandomPoint(c.Pos + away, 15) ?? c.Pos + away; c.talking = false; }
            else if (st != "flee" && AimedAt()) { if (st != "surrender") FBNpcVoz.Say(c, "namira", 1, 4.5f, 5, .5f); st = "surrender"; stT = 3; c.talking = false; }
            if (st == "flee") { desired = goal; speed = 3.6f; if (stT <= 0) st = "walk"; }
            else if (st == "surrender") { if (stT <= 0) st = "walk"; var p = FBMatch.Player; if (p) c.Yaw = Mathf.LerpAngle(c.Yaw, Quaternion.LookRotation((p.Pos - c.Pos).Flat()).eulerAngles.y, Mathf.Min(1, dt * 4)); }
            else if (st == "talk") { if (stT <= 0) { st = "walk"; c.talking = false; } }
            else
            {
                if (goal == null || (c.Pos - goal.Value).magnitude < 1.5f || stT <= 0)
                {
                    var mate = FBMatch.Civs.FirstOrDefault(o => o && o != c && o.alive && (o.Pos - c.Pos).magnitude < 4);
                    if (goal != null && mate && Random.value < .6f) { st = "talk"; stT = Random.Range(6f, 12f); c.talking = true; c.Yaw = Quaternion.LookRotation((mate.Pos - c.Pos).Flat()).eulerAngles.y; }
                    goal = FBNav.RandomPoint(c.Pos, 45) ?? goal; stT = Random.Range(10f, 20f);
                }
                if (st == "walk") { desired = goal; speed = 1.3f; }
            }
            c.surrender = st == "surrender"; c.fleeing = st == "flee";
            Vector3 tv = Vector3.zero;
            if (desired is Vector3 d)
            {
                navT -= dt; if (path == null || navT <= 0) { navT = 2; path = FBNav.Path(c.Pos, d) ?? new List<Vector3> { d }; }
                while (path.Count > 1 && (path[0] - c.Pos).Flat().magnitude < 1.3f) path.RemoveAt(0);
                var t = (path[0] - c.Pos).Flat(); if (t.magnitude > .3f) { tv = t.normalized * speed * c.ScaleK; c.Yaw = Mathf.LerpAngle(c.Yaw, Quaternion.LookRotation(t).eulerAngles.y, Mathf.Min(1, dt * 5)); }
            }
            vel = Vector3.Lerp(vel, tv, Mathf.Min(1, dt * 5));
            if (cc.enabled) { if (cvStep < 0) cvStep = cc.stepOffset; vy = cvGround ? -2 : vy - 19 * dt; FBMotor.Move(cc, (vel + Vector3.up * vy) * dt, cvGround, vy > 0, cvStep, .45f * c.ScaleK, ref cvSlide, out cvGround, out cvG); }   // mesma física de chão do jogador (degrau/escada)
            else vy = cc.isGrounded ? -2 : vy - 19 * dt;
            float g = vel.magnitude / c.ScaleK, sp = g <= 1.3f ? g / 1.3f : 1 + (g - 1.3f) / 2.3f * 1.5f; var lv = Quaternion.Inverse(Quaternion.Euler(0, c.Yaw, 0)) * vel; float lm = lv.magnitude;
            c.animVel = lm > 1e-3f ? new Vector2(lv.x / lm * sp, lv.z / lm * sp) : Vector2.zero; c.velocity = vel; c.grounded = cc.enabled ? cvGround : cc.isGrounded;
        }
    }

    // variedade dos NPCs (npcanims.js npcSetFor): cada personagem sorteia, fixo pelo id + semente, o próprio andar, correr, parados e conversa
    public static class FBNpcAnims
    {
        static readonly Dictionary<string, Dictionary<string, string[]>> POOL = new()
        {
            ["f"] = new()
            {
                ["walk"] = new[] { "NPC_Elegant_Walk_2L", "NPC_Walk_relaxed_loop", "NPC_Walk_F", "NPC_Walk_relaxed_loop" }, ["walkPlayer"] = new[] { "NPC_Walk_relaxed_loop", "NPC_Walk_F", "NPC_Elegant_Walk_2L" },
                ["run"] = new string[] { null, null }, ["idle"] = new[] { "NPC_Idle01_F", "NPC_Idle02_F", "NPC_Idle03_F", "NPC_Female_Idle_1", "NPC_Female_Idle_2", "NPC_Stand_Looking_F", "NPC_Tiffany_Idle" },
                ["talk"] = new[] { "NPC_Talk_F", "NPC_Stand_talk", "NPC_Stand_Talk", "NPC_Chat_Relax_F", "NPC_Argument_Bossy_F", "NPC_Ambitious_Speech", "NPC_Emote_F" }, ["sit"] = new[] { "NPC_Sit_talk", "NPC_Sit_Talk_2", "NPC_Sit_Look_Up_Laugh" },
            },
            ["m"] = new()
            {
                ["walk"] = new[] { "NPC_Walk_2Loop", "NPC_Walk01_loop", "NPC_Walk_Relaxed_2Loop", "NPC_Walk_Relaxed_loop", "NPC_Walk_M" }, ["walkPlayer"] = new[] { "NPC_Walk_M", "NPC_Walk_2Loop", "NPC_Walk01_loop" },
                ["run"] = new string[] { null }, ["idle"] = new[] { "NPC_Idle01_M", "NPC_Idle02_M", "NPC_Idle03_M" },   // só parados masculinos (Breathe/Relax tinham postura feminina)
                ["talk"] = new[] { "NPC_Talk_M", "NPC_Talk_Serious", "NPC_Meeting_Presenter_M", "NPC_Emote_M" }, ["sit"] = new[] { "NPC_Relax_Sit", "NPC_Sit_Talk" },
            },
        };
        public const string HANDSHAKE = "NPC_Greeting_Handshakes_M";
        public static readonly Dictionary<string, (string enter, string outC)> SIT = new() { ["m"] = ("NPC_Stand_to_Sit", "NPC_Sit_to_Stand"), ["f"] = ("NPC_Stand_to_sit", "NPC_Sit_to_Stand_2") };
        static readonly HashSet<string> FRIDAY_GIRLS = new() { "n_jennifer", "n_alicia", "n_margie", "n_cat", "n_latoya", "n_mona", "n_torrance", "n_yasmine", "n_maggy" };
        static uint Hash(string s) { uint h = 2166136261; foreach (var ch in s) h = (h ^ ch) * 16777619; return h; }
        static float Rs(string id) { foreach (var o in GameData.NpcAnims ?? new List<object>()) if (Json.S(o, "id") == id) return Json.F(o, "rs"); return 0; }
        // pacote NOVO (20/10/2026): elenco = skins com npc2 (n2_*); cada personagem anda/para/conversa/senta com os PRÓPRIOS clipes e só o
        // que falta vem de um doador do mesmo sexo (tools/npcs/npc2_anims.py) — nada de misturar clipes de personagens nem masculino com feminino
        // rodada 38: o pacote n2_* saiu (pedido do usuário) — sem ele, os pedestres são os regionais (r_*) com os clipes genéricos de pedestre
        // r52: o elenco sai das CATEGORIAS do skins.json (FBElenco) — os personagens do 171 (x171_*) saíram e nenhum id fica no código.
        // Fardados (policia) não andam à paisana como civis/motoristas — aparecem nas abordagens/patrulha (FBBaculejo, FBTransito)
        public static bool IsPolice(string id) => FBElenco.In(id, FBElenco.POLICIA);
        // policiais de VIATURA (patrulha, perseguição, abordagem) = func pm_viatura (r47: PM ALMEIDA, PM ELAINE e PM LUIS); guarda municipal,
        // choque e BOPE são policiais mas não dirigem viatura
        public static List<string> Police => FBElenco.Ids(FBElenco.POLICIA, "pm_viatura");
        // eventos: "eletricista" = a categoria inteira; o resto = NPC de função específica ("motoboy" = entregador, "pedreiro", "gari"...).
        // Sem ninguém na função, a lista vem vazia — o evento não põe um civil no lugar
        public static List<string> ByCls(string cls) => cls == FBElenco.ELETRICISTA ? FBElenco.Ids(FBElenco.ELETRICISTA) : FBElenco.Ids(FBElenco.FUNCAO, cls == "motoboy" ? "entregador" : cls);
        // civis: pedestres, motoristas, caronas, abordados e figurantes = categoria civil (NPCs próprios + jogáveis moradores)
        public static List<string> Roster(string except = null)
        {
            var l = GameData.Skins.Where(s => !string.IsNullOrEmpty(s.npc2) && s.id != except && FBRegistry.I.HasChar(s.id)).Select(s => s.id).ToList();
            return l.Count > 0 ? l : FBElenco.Ids(FBElenco.CIVIL, null, except);
        }
        static Dictionary<string, object> Npc2Of(string id) => Json.O(Json.O(GameData.Npc2, "chars"), GameData.Skins.Find(s => s.id == id)?.npc2 is string k && k != "" ? k : id);
        static List<string> Npc2Cat(Dictionary<string, object> ch, string cat) => (Json.L(Json.O(Json.O(ch, "sets"), cat), "clips") ?? new List<object>()).Select(o => o as string).Where(x => x != null).ToList();
        static float Rs2(string id) { foreach (var o in Json.L(GameData.Npc2, "clips") ?? new List<object>()) if (Json.S(o, "id") == id) return Json.F(o, "rs"); return 0; }
        public static string SitEnter(FBCharacter c) { var ch = Npc2Of(c ? c.skinId : null); if (ch != null) return Npc2Cat(ch, "sit").FirstOrDefault(n => System.Text.RegularExpressions.Regex.IsMatch(n, "Stand_to_sit", System.Text.RegularExpressions.RegexOptions.IgnoreCase)); return null; }
        public static FBCharacter.NpcSet SetFor(string id, string sex, string seed, bool player = false)
        {
            uint h = Hash(id + "|" + seed); string At(IList<string> l, int k) => l[(int)((h >> k) % (uint)l.Count)];
            var ch = player ? null : Npc2Of(id);
            if (ch != null)
            {
                var set2 = new FBCharacter.NpcSet { sex = Json.S(ch, "sex") == "f" ? "f" : "m" };
                var walk = Npc2Cat(ch, "walk"); if (walk.Count > 0) { set2.walk = walk[(int)(h % (uint)Mathf.Min(2, walk.Count))]; set2.ovr["WalkFwdLoop"] = set2.walk; }   // os loops vêm primeiro
                set2.idle.AddRange(Npc2Cat(ch, "idle")); set2.talk.AddRange(Npc2Cat(ch, "talk"));
                set2.sit.AddRange(Npc2Cat(ch, "sit").Where(n => !System.Text.RegularExpressions.Regex.IsMatch(n, "_to_|Stand_to", System.Text.RegularExpressions.RegexOptions.IgnoreCase)));
                set2.dance.AddRange(Npc2Cat(ch, "dance"));
                float r2 = set2.walk != null ? Rs2(set2.walk) : 0; set2.walkRs = r2 > 0 ? r2 : 1;
                return set2;
            }
            var P = POOL[sex == "f" ? "f" : "m"];
            var set = new FBCharacter.NpcSet { sex = sex == "f" ? "f" : "m" }; set.walk = At(player ? P["walkPlayer"] : P["walk"], 0); var run = At(P["run"], 5);
            if (player) { var reg = set.sex == "f" ? FBAnimProfile.WalkFemale : FBAnimProfile.WalkMale; if (FBRegistry.I && FBRegistry.I.Clip(reg)) set.walk = reg; }   // jogador: andar novo do sexo dele (rodada 38)
            if (player && set.sex == "m") { set.idle.Add("IE_Unarmed_Idle01"); set.idle.Add("Idle"); }   // jogador homem: parado masculino do Invasão Elite (os NPC_Idle_M do pacote ficavam com quadril/mão de postura feminina)
            else if (FRIDAY_GIRLS.Contains(id)) set.idle.Add("NPC_Friday13_Idle"); else foreach (var k in new[] { 0, 7, 13 }) { var i = At(P["idle"], k); if (!set.idle.Contains(i)) set.idle.Add(i); }
            set.ovr["WalkFwdLoop"] = set.walk; if (run != null) set.ovr["RunFwdLoop"] = run;
            set.talk.AddRange(P["talk"]); set.sit.AddRange(P["sit"]); float rs = Rs(set.walk); set.walkRs = rs > 0 ? rs : 1;
            return set;
        }
        public static void Apply(FBCharacter c, string seed, bool player = false)
        {
            var sex = GameData.Skins.Find(s => s.id == c.skinId)?.sex ?? "m";
            var set = SetFor(c.skinId, sex, seed, player); if (!player) FBNpcRotina.Decorate(set, c); c.SetNpcSet(set); c.walkSpeed = Mathf.Clamp(set.walkRs * 1.25f * c.ScaleK * Random.Range(.86f, 1.18f) * (System.Text.RegularExpressions.Regex.IsMatch(c.skinId ?? "", "idos|veio|mendigo") ? .78f : 1), .62f, 1.9f);   // r44: passo de cada um (idosos mais devagar)   // worldScale do web ~1,25
        }
    }
    // paredão (paredao.js): som por carro, ligado/desligado (K abre a central multimídia) — estado e música
    // oficina/garagem do De Rolê (oficina.js): câmera própria quando aberta
}
