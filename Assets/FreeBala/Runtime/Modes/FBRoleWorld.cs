// DE ROLÊ (porte do roleSetup / updateRoleNPC / findSeats / transito.js do main.js): modo livre sem armas nem HUD de combate.
//  · gente do rolê: os personagens novos do pacote NPCS (menos o do jogador) + pedestres p_*, cada um com o próprio andar/parado/conversa;
//    andam pela cidade (preferem calçada: alvo fora do asfalto), param para conversar (aperto de mão, rodas de até 4), sentam nos bancos e nas
//    cadeiras das mesas de plástico (achadas pela geometria do prop), vão curtir o paredão ligado (até 14 por paredão, dançando), saem da
//    frente do carro do jogador e às vezes pegam um carro estacionado;
//  · trânsito: NPCs dirigindo pelas ruas (navmesh cujo chão é asfalto), A* + Chaikin + mão direita, olham à frente, freiam para jogador,
//    pedestres, carros e paredes, desviam de estacionados, dão ré quando travam, estacionam em vaga livre e descem viram pedestres;
//  · helicóptero de passagem, duas picapes já tocando (músicas do próprio jogador), garagem para escolher o carro do rolê.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace FreeBala
{
    public class FBSeat { public Vector3 p; public float yaw; public string kind; public FBCharacter busy; public object grp; public float h = float.NaN; }

    public static class FBRole
    {
        public static readonly List<FBCharacter> npcs = new(); public static readonly List<FBSeat> seats = new(); static readonly List<string> types = new();
        static readonly List<FBCar> walls = new(); static float wallT; static FBMatch G;
        public static FBCar roleCar; public static (Vector3 p, float yaw)? spot;
        public static void Setup(FBMatch g)
        {
            G = g; npcs.Clear(); seats.Clear(); walls.Clear(); wallMuted.Clear(); types.Clear(); roleCar = null;
            // pacote NOVO de NPCs (20/10/2026): os antigos (n_* e pedestres p_*) saíram; cada um com os próprios clipes (FBNpcAnims.SetFor)
            types.AddRange(FBNpcAnims.Roster(FBSession.skin).OrderBy(_ => Random.value));
            if (FBMatch.Player) FBNav.SetAnchor(FBMatch.Player.Pos);                                            // NPC só nasce/anda do lado de DENTRO do muro de limite if (FBMatch.Player) FBNpcAnims.Apply(FBMatch.Player, "", true);
            // contêineres de COBERTURA (combate) saem do rolê — no tiroteio continuam; o pátio de contêineres da Praça do PT é cenário e fica
            int nct = 0; foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) if (t.gameObject.layer == FBWorld.LMap && FBRoads.IsCoverContainer(t)) { t.gameObject.SetActive(false); nct++; }
            if (nct > 0) { Physics.SyncTransforms(); Debug.Log($"[FreeBala][rolê] {nct} contêineres de cobertura fora do De Rolê"); }
            FBTransito.BuildRoads(); FBPedNav.Build(FBSession.map);
            FindSeats();
            { int n0 = seats.Count; seats.RemoveAll(S => !FBNav.InsideNear(S.p, 2f)); if (seats.Count < n0) Debug.Log($"[FreeBala][rolê] {n0 - seats.Count} assentos fora da área jogável ignorados"); }   // banco/cadeira atrás do muro de limite
            // SEM REPETIÇÃO (pedido 20/10: "não quero npcs repetidos"): cada personagem do pacote existe uma vez só — uns andando, os outros
            // dirigindo no trânsito (FBTransito.Actor pega quem ainda não está em cena)
            FBVestirRua.Dress();                                                                                  // r44: objetos do 171 nas calçadas (botecos viram assentos)
            // r44: com os personagens do 171 o elenco passou de 100 — limite por qualidade (o resto aparece no trânsito/eventos)
            int cap = FBQuality.Mobile ? 18 : FBQuality.Level >= FBQLevel.Alto ? 46 : 32; if (types.Count > cap + 4) types.RemoveRange(cap + 4, types.Count - cap - 4);
            int drivers = Mathf.Min(4, types.Count / 2);
            for (int i = 0; i < types.Count - drivers; i++) { var p = FBTransito.SidewalkPoint(null, 0); if (p != null) SpawnNpc(types[i], p.Value, "n" + i); }
            // um terço das mesas já começa ocupado: sentados conversando
            var grps = seats.Where(s => s.kind == "cadeira").Select(s => s.grp).Distinct().OrderBy(_ => Random.value).ToList();
            foreach (var gp in grps.Take((grps.Count + 2) / 3)) foreach (var S in seats.Where(s => s.grp == gp)) { var c = npcs.FirstOrDefault(o => o.GetComponent<FBRoleNpc>().st == "walk" && o.GetComponent<FBRoleNpc>().seat == null && o.npcSet?.sit.Count > 0); if (c) c.GetComponent<FBRoleNpc>().SeatNow(S, false); }
            FBRoleHeli.Spawn();
            // vaga do carro do jogador: o estacionado mais perto do spawn sai e o escolhido na garagem nasce ali
            var me = FBMatch.Player; var near = FBVehicles.list.Where(c => c && !c.ie).OrderBy(c => me ? (c.transform.position - me.Pos).sqrMagnitude : 0).FirstOrDefault();
            if (me && near && Vector3.Distance(near.transform.position, me.Pos) < 60) { spot = (near.transform.position, near.transform.eulerAngles.y); FBVehicles.list.Remove(near); Object.Destroy(near.gameObject); }
            else if (me) spot = (me.Pos + me.transform.forward * 5, me.Yaw + 90);
            // a cidade viva: duas picapes estacionadas já tocando (músicas do próprio jogador)
            FBMediaLib.Refresh(); var tracks = FBMediaLib.lib.Where(t => t.s <= FBParedao.MAX_SHARE).ToList();
            // r45b (pedido: "não precisamos de vários veículos com paredões tocando ao mesmo tempo um próximo ao outro"): no máximo 2, a 260 m+ um do outro
            if (tracks.Count > 0) foreach (var car in FBVehicles.list.Where(c => c && c.soundWallNode).OrderBy(_ => Random.value))
            { if (walls.Count >= 2) break; if (walls.Any(w => (w.transform.position - car.transform.position).magnitude < WallGap)) continue; walls.Add(car); FBParedao.Play(car, FBUtil.Pick(tracks), Random.Range(0, 40f)); }
            FBTransito.Start(12);
            FBGarage.Open();
        }
        // r45b: paredão da cidade (os 2 do começo) desliga se outro paredão ligado (do jogador, de outro jogador ou o outro da cidade que veio
        // antes) está a menos de WallGap; volta a tocar quando o outro desliga ou fica longe. Nunca dois tocando um do lado do outro.
        public const float WallGap = 260; static readonly HashSet<FBCar> wallMuted = new(); public static int WallMutes, WallTicks; public static string WallDiag; public static IReadOnlyList<FBCar> CityWalls => walls;
        static void WallSpacing(List<FBTrack> tracks)
        {
            WallTicks++; var dg = new System.Text.StringBuilder();
            for (int i = 0; i < walls.Count; i++)
            {
                var car = walls[i]; if (!car || !FBParedao.CanControl(car)) continue; var p = car.transform.position;
                bool conflict = FBParedao.List().Any(o => o.car && o.car != car && o.on && (o.car.transform.position - p).magnitude < WallGap && (!walls.Contains(o.car) || walls.IndexOf(o.car) < i));
                var W = FBParedao.State(car); bool on = W != null && W.on; dg.Append($"{car.id}[{i}] ligado {on} conflito {conflict} · ");
                if (conflict && on) { FBParedao.SetOn(car, false, true); wallMuted.Add(car); WallMutes++; }
                else if (!conflict && !on && wallMuted.Contains(car) && tracks.Count > 0) { wallMuted.Remove(car); FBParedao.Play(car, FBUtil.Pick(tracks), Random.Range(0, 40f)); }
            }
            WallDiag = $"{walls.Count} da cidade · " + dg;
        }
        public static FBCharacter SpawnNpc(string id, Vector3 p, string seed)
        {
            var c = G.Spawn(id, null, 2, p, false, false, true); var cv = c.GetComponent<FBCivilian>(); if (cv) Object.Destroy(cv);
            c.hp = c.maxHp = 60; FBNpcAnims.Apply(c, seed); c.SetWeaponVisible(false); c.combat = false; c.Yaw = Random.Range(0, 360f);
            c.gameObject.AddComponent<FBRoleNpc>(); npcs.Add(c); return c;
        }
        public static void Tick(float dt)
        {
            FBTransito.Update(dt); FBRoleHeli.Tick(dt);
            var kb = UnityEngine.InputSystem.Keyboard.current; if (kb != null && kb.oKey.wasPressedThisFrame && !FBGarage.Open_) FBOficina.Toggle();   // oficina do REB
            if ((wallT += dt) > .5f) { wallT = 0; var tracks = FBMediaLib.lib;   /* r45n: 0,5 s (era 2 s: um som que acabou de ligar ficava até 2 s colado no paredão) */ foreach (var car in walls) { var W = FBParedao.State(car); if (car && W != null && W.on && W.ended && tracks.Count > 0 && FBParedao.CanControl(car)) FBParedao.Play(car, FBUtil.Pick(tracks), 0); } WallSpacing(tracks); }
            npcs.RemoveAll(n => !n);
            if ((recycleT -= dt) <= 0) { recycleT = 1.2f; Recycle(); }
        }
        static float recycleT;
        static void Recycle()
        {
            var me = FBMatch.Player; var cam = Camera.main; if (!me || !cam || npcs.Count == 0) return; var pp = me.Pos; var cf = cam.transform.forward.Flat().normalized; int moved = 0;
            foreach (var c in npcs.Where(o => o && o.alive && !o.vehiclePose).OrderByDescending(o => (o.Pos - pp).sqrMagnitude).Take(4))
            {
                var rn = c.GetComponent<FBRoleNpc>(); if (!rn || rn.st != "walk" || (c.Pos - pp).magnitude < 130) continue;
                for (int k = 0; k < 6; k++)
                {
                    var q = FBTransito.SidewalkPoint(pp, 110); if (q == null) break; var d = (q.Value - pp).Flat(); float dm = d.magnitude;
                    if (dm < 55) continue; if (Vector3.Dot(d / dm, cf) > .45f && dm < 85) continue;                   // nasce fora da vista (atrás / longe)
                    rn.Relocate(q.Value); moved++; break;
                }
                if (moved >= 2) break;
            }
        }
        // ---- assentos: bancos (2 lugares de cada lado, de costas para o meio) e cadeiras das mesas de plástico
        static void FindSeats()
        {
            var rxProp = new System.Text.RegularExpressions.Regex("banco|mesas_plastico", System.Text.RegularExpressions.RegexOptions.IgnoreCase); var rxNo = new System.Text.RegularExpressions.Regex("sign|placa", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            bool IsProp(Transform t) => t && rxProp.IsMatch(t.name) && !rxNo.IsMatch(t.name);
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.gameObject.layer != FBWorld.LMap || !IsProp(t) || IsProp(t.parent)) continue;
                var rs = t.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue; var box = rs[0].bounds; foreach (var r in rs) box.Encapsulate(r.bounds);
                var c = box.center; var sz = box.size; var ax = (t.rotation * Vector3.right).Flat().normalized; var az = (t.rotation * Vector3.forward).Flat().normalized;
                float Ext(Vector3 d) => Mathf.Abs(sz.x * d.x) + Mathf.Abs(sz.z * d.z);
                if (t.name.ToLowerInvariant().Contains("banco"))
                {
                    var lng = Ext(ax) >= Ext(az) ? ax : az; var side = lng == ax ? az : ax; float depth = Mathf.Min(.7f, Ext(side)) / 2;
                    foreach (var sd in new[] { -1, 1 }) foreach (var k in new[] { -.45f, .45f }) { var f = side * sd; var p = c + lng * k + f * (depth * .35f); p.y = box.min.y; seats.Add(new FBSeat { p = p, yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, kind = "banco", grp = t }); }
                }
                else FindChairs(t);
            }
        }
        // cadeiras: assentos = triângulos para cima a 33–60 cm do chão do prop, agrupados no plano; a frente sai do encosto (76–100 cm)
        static void FindChairs(Transform prop)
        {
            var L = new List<(Vector3 c, float ny, float ar)>(); float gy = 1e9f;
            foreach (var mf in prop.GetComponentsInChildren<MeshFilter>())
            {
                var m = mf.sharedMesh; if (!m || !m.isReadable) continue; var M = mf.transform.localToWorldMatrix; var v = m.vertices; var tri = m.triangles;
                for (int i = 0; i + 2 < tri.Length; i += 3)
                {
                    Vector3 a = M.MultiplyPoint3x4(v[tri[i]]), b = M.MultiplyPoint3x4(v[tri[i + 1]]), cc = M.MultiplyPoint3x4(v[tri[i + 2]]); var n = Vector3.Cross(b - a, cc - a); float ar = n.magnitude; if (ar < 1e-7f) continue; n /= ar;
                    var ce = (a + b + cc) / 3; L.Add((ce, n.y, ar)); gy = Mathf.Min(gy, ce.y);
                }
            }
            if (L.Count == 0) return; const float C = .05f; var cells = new Dictionary<(int, int), List<int>>();
            for (int k = 0; k < L.Count; k++) { float h = L[k].c.y - gy; if (Mathf.Abs(L[k].ny) < .85f || h < .33f || h > .6f) continue; var key = (Mathf.FloorToInt(L[k].c.x / C), Mathf.FloorToInt(L[k].c.z / C)); if (!cells.TryGetValue(key, out var e)) cells[key] = e = new List<int>(); e.Add(k); }
            var seen = new HashSet<(int, int)>(); var found = new List<Vector3>();
            foreach (var ck in cells.Keys)
            {
                if (seen.Contains(ck)) continue; var st = new Stack<(int, int)>(); st.Push(ck); seen.Add(ck); var ks = new List<int>();
                while (st.Count > 0) { var q = st.Pop(); ks.AddRange(cells[q]); for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++) { var nk = (q.Item1 + dx, q.Item2 + dz); if (cells.ContainsKey(nk) && seen.Add(nk)) st.Push(nk); } }
                float x0 = 1e9f, x1 = -1e9f, z0 = 1e9f, z1 = -1e9f, sy = 0, w = 0; foreach (var k in ks) { var p = L[k].c; x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); z0 = Mathf.Min(z0, p.z); z1 = Mathf.Max(z1, p.z); sy += p.y * L[k].ar; w += L[k].ar; }
                float sx = x1 - x0, sz = z1 - z0; if (sx < .25f || sz < .25f || sx > .7f || sz > .7f) continue;
                found.Add(new Vector3((x0 + x1) / 2, sy / w, (z0 + z1) / 2));
            }
            foreach (var S in found)
            {
                bool stacked = L.Any(l => { float h = l.c.y - S.y; return h > .08f && h < .4f && Mathf.Abs(l.ny) > .85f && new Vector2(l.c.x - S.x, l.c.z - S.z).magnitude < .15f; }); if (stacked) continue;
                float bx = 0, bz = 0, m = 0; foreach (var l in L) { float h = l.c.y - gy; if (h < .76f || h > 1 || Mathf.Abs(l.ny) > .5f) continue; if (new Vector2(l.c.x - S.x, l.c.z - S.z).magnitude > .45f) continue; bx += l.c.x * l.ar; bz += l.c.z * l.ar; m += l.ar; }
                if (m <= 0) continue; var f = new Vector3(S.x - bx / m, 0, S.z - bz / m); if (f.sqrMagnitude < 1e-4f) continue; f.Normalize();
                seats.Add(new FBSeat { p = new Vector3(S.x, gy, S.z), yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, kind = "cadeira", grp = prop });
            }
        }
        public static Vector3 SeatApproach(FBSeat S)
        {
            var f = Quaternion.Euler(0, S.yaw, 0) * Vector3.forward; if (S.kind != "cadeira") return S.p + f * .6f;
            var r = new Vector3(f.z, 0, -f.x); var others = seats.Where(o => o.grp == S.grp && o != S).ToList();
            float Clear(int sd) => others.Count == 0 ? 9 : Mathf.Min(9, others.Min(o => new Vector2(S.p.x + r.x * sd * .62f - o.p.x, S.p.z + r.z * sd * .62f - o.p.z).magnitude));
            return S.p + r * (Clear(1) >= Clear(-1) ? .62f : -.62f) - f * .12f;
        }
        public static IEnumerable<string> Dances() { var l = FBEmotes.DanceIds().Where(n => FBRegistry.I.Clip(n) && !System.Text.RegularExpressions.Regex.IsMatch(n, "cut|throat|kill|bow|apocalypse", System.Text.RegularExpressions.RegexOptions.IgnoreCase)).ToList(); return l.Count > 0 ? (IEnumerable<string>)l : new List<string> { "NPC_Dougie" }; }
    }

    // ---- um NPC do rolê (updateRoleNPC)
    public class FBRoleNpc : MonoBehaviour
    {
        public FBCharacter c; CharacterController cc; bool cvGround = true; float cvSlide, cvStep = -1; FBMotor.Ground cvG; public string st = "walk", prevSt; public float stT, partyCd, danceT, vy, stkT, hornT; public Vector3? goalP, dodgeP, stkP; public bool dancer, atParty, greet; public int stkN;
        // r43 rotina do 171 (FBNpcRotina): olhar/tropeço/susto/mira/fuga; carro do desvio (para a distância de passagem)
        public Vector3? lookP, fleeP; Vector3 dodgeFrom, dodgeFwd; float reactT;
        public bool Free => st == "walk" || st == "pause" || st == "talk";
        // autoteste (FBNpcLab): põe os dois numa conversa agora, como o Social faria
        public void TalkWith(FBRoleNpc mate, float T = 25)
        {
            var C = (c.Pos + mate.c.Pos) / 2; var d = (mate.c.Pos - c.Pos).Flat(); d = d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;
            foreach (var (a, sg) in new[] { (this, -1), (mate, 1) }) { a.c.StopDance(true); a.LeaveSeat(); a.Teleport(new Vector3(C.x + d.x * .55f * sg, a.c.Pos.y, C.z + d.z * .55f * sg)); a.JoinTalk(C, T); }
        }
        public void LookAt(Vector3 p, float T) { if (!Free) return; c.StopDance(true); c.talking = false; talkC = null; st = "look"; stT = T; lookP = p; vel = Vector3.zero; }
        void Flee(Vector3 from) { c.StopDance(true); c.talking = false; talkC = null; c.surrender = false; LeaveSeat(); var away = (c.Pos - from).Flat().normalized * 28; fleeP = FBNav.RandomPoint(c.Pos + away, 10) ?? c.Pos + away; st = "flee"; stT = Random.Range(5f, 9f); }
        bool Aimed(FBCharacter p)
        {
            var cam = Camera.main; if (!cam || !(p.aiming || p.lastShot < 1.2f) || p.Unarmed) return false;
            var f = cam.transform.forward; var to = c.Pos + Vector3.up * 1.2f * c.ScaleK - cam.transform.position; float al = Vector3.Dot(to, f); return al > 0 && (to - f * al).magnitude < .9f;
        }
        // reações do 171: devolve true quando o NPC está parado num clipe de reação (o resto do Update não roda neste quadro)
        bool React(float dt)
        {
            var p = FBMatch.Player;
            if (st == "react" || st == "look" || st == "scared")
            {
                vel = Vector3.zero; c.velocity = Vector3.zero;
                if (st == "look" && lookP is Vector3 lp && (lp - c.Pos).Flat().sqrMagnitude > .01f) c.Yaw = Mathf.LerpAngle(c.Yaw, Quaternion.LookRotation((lp - c.Pos).Flat()).eulerAngles.y, Mathf.Min(1, dt * 4));
                if (st == "scared" && p) { if ((p.Pos - c.Pos).Flat().sqrMagnitude > .01f) c.Yaw = Mathf.LerpAngle(c.Yaw, Quaternion.LookRotation((p.Pos - c.Pos).Flat()).eulerAngles.y, Mathf.Min(1, dt * 4)); if (Aimed(p)) stT = Mathf.Max(stT, 1); }
                if (stT <= 0)
                {
                    if (st == "scared") { c.surrender = false; if (p) Flee(p.Pos); else st = "walk"; return false; }
                    if (st == "react" && c.dance != null && FBNpcRotina.Moves(c.dance.clip) && c.an && c.an.GetBoneTransform(HumanBodyBones.Hips) is Transform hp)
                    {   // o clipe levou o corpo para longe: a raiz vai até ele (e vira para onde o corpo olha) e o clipe é cortado no mesmo quadro
                        var lU = c.an.GetBoneTransform(HumanBodyBones.LeftUpperLeg); var rU = c.an.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                        var to = (hp.position - c.Pos).Flat(); Teleport(c.Pos + to);
                        if (lU && rU) { var bf = Vector3.Cross(rU.position - lU.position, Vector3.up).Flat(); if (bf.sqrMagnitude > 1e-6f) c.Yaw = Quaternion.LookRotation(bf).eulerAngles.y; }
                        c.CutDance();
                    }
                    else c.StopDance(true);
                    st = "walk"; goalP = null; stT = 0; lookP = null; return false;
                }
                return true;
            }
            if (!Free || (reactT -= dt) > 0) return false; reactT = .25f + Random.value * .1f;
            if (p && p.alive && !p.vehiclePose)
            {
                float d = (p.Pos - c.Pos).Flat().magnitude;
                if (d < .8f && p.velocity.Flat().magnitude > 1.2f) { c.StopDance(true); c.talking = false; talkC = null; float L = FBNpcRotina.Bump(c, p.Pos); if (L > 0) { st = "react"; stT = L; return true; } }
                if (d < 25 && Aimed(p)) { c.StopDance(true); c.talking = false; talkC = null; st = "scared"; stT = Random.Range(3f, 5f); c.surrender = true; FBNpcVoz.Say(c, "namira", 1, 4.5f, 5, .5f); return true; }
                if (d < 12 && !p.Unarmed && p.weaponGO && p.weaponGO.activeInHierarchy && Random.value < .2f) FBNpcVoz.Say(c, "armado", .95f, 4, 30, 4);
            }
            if (FBBotBrain.lastNoiseP is Vector3 np && Time.time - FBBotBrain.lastNoiseT < 2f && (np - c.Pos).sqrMagnitude < 35 * 35) { Flee(np); FBNpcVoz.Say(c, "perigo", 1, 5, 6, .5f, Random.Range(.1f, .6f)); }
            return false;
        }
        public FBSeat seat; public Vector3? talkC; public FBParedao.Wall partyW; Vector3 vel; List<Vector3> path; float navT, crossWait; Vector3? pathGoal; string seatClip;
        static bool PlayerCarComing(Vector3 p)
        {
            var D = FBDrive.Cur; if (!D || Mathf.Abs(D.v) < 2) return false; var to = (p - D.transform.position).Flat(); var f = FBDrive.Fwd(D.yaw) * Mathf.Sign(D.v);
            float ahead = Vector3.Dot(to, f), lat = Mathf.Abs(to.x * f.z - to.z * f.x); return ahead > -2 && ahead < Mathf.Abs(D.v) * 4 + 4 && lat < 3.5f;
        }
        float pace, phase, seatY, seatYaw; Vector3 seatXZ;
        void Awake()
        {
            c = GetComponent<FBCharacter>(); cc = GetComponent<CharacterController>(); c.civ = true; c.combat = false; stT = Random.Range(2f, 8f); partyCd = Random.Range(2f, 25f); dancer = Random.value < .7f;
            float r = Random.value; pace = r < .25f ? Random.Range(.85f, 1.05f) : r < .8f ? Random.Range(1.15f, 1.42f) : Random.Range(1.5f, 1.8f); phase = Random.value * 10; c.walkSpeed = pace;
        }
        // ponto 'L' m à frente seguindo o caminho (perseguição pura): quina vira curva
        Vector3 Carrot(float L)
        {
            if (path == null || path.Count == 0) return c.Pos; var a = c.Pos; float rem = L;
            for (int i = 0; i < path.Count; i++) { var b = path[i]; var ab = (b - a).Flat(); float len = ab.magnitude; if (len >= rem) return a + ab / Mathf.Max(1e-4f, len) * rem; rem -= len; a = b; }
            return path[^1];
        }
        public void Relocate(Vector3 p) { Teleport(p); path = null; pathGoal = null; goalP = null; stT = 0; vel = Vector3.zero; LeaveSeat(); }
        void OnDisable() { if (seat != null && seat.busy == c) seat.busy = null; }
        public bool SeatNow(FBSeat S, bool enter)
        {
            var lp = c.npcSet?.sit.Where(n => FBRegistry.I.Clip(n)).ToList(); if (lp == null || lp.Count == 0) return false; var sx = c.npcSet.sex ?? "m";
            var f = Quaternion.Euler(0, S.yaw, 0) * Vector3.forward; Teleport(new Vector3(S.p.x + f.x * .3f, S.p.y, S.p.z + f.z * .3f)); c.Yaw = S.yaw; vel = Vector3.zero; seat = S; S.busy = c; c.seated = true; seatClip = null; seatY = 0; seatYaw = 0; seatXZ = Vector3.zero; seatBase = null;
            if (float.IsNaN(S.h))
            {   // superfície do assento: raio de cima no ponto do quadril (35–65 cm acima do chão do assento); sem achar = altura padrão 45 cm
                // chão e assento medidos pelo raio no ponto do lugar (o fundo da caixa do banco não é o piso em todo mapa: NPC sentava alto)
                var hs = Physics.RaycastAll(new Vector3(S.p.x, S.p.y + 1.6f, S.p.z), Vector3.down, 4f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore).Where(h => h.normal.y > .7f).Select(h => h.point.y).OrderBy(y => y).ToList();
                if (hs.Count > 0)
                {
                    float gnd = hs[0]; var top = hs.Where(y => y - gnd > .28f && y - gnd < .75f).DefaultIfEmpty(float.NaN).Max();
                    S.p = new Vector3(S.p.x, gnd, S.p.z); S.h = float.IsNaN(top) ? gnd + .45f : top;
                }
                else S.h = S.p.y + .45f;
            }
            var sit = FBNpcAnims.SIT[sx]; var e2 = FBNpcAnims.SitEnter(c); if (c.npcSet != null && GameData.Skins.Exists(s => s.id == c.skinId && !string.IsNullOrEmpty(s.npc2))) sit = (FBRegistry.I.Clip(e2) ? e2 : null, null);   // pacote novo: só a transição do próprio conjunto
            c.StopDance(true); if (!c.PlayDance(FBUtil.Pick(lp), enter ? sit.enter : null, sit.outC)) return false;
            st = "sit"; stT = Random.Range(30f, 90f); return true;
        }
        void Teleport(Vector3 p) { if (cc) cc.enabled = false; transform.position = p; if (cc) cc.enabled = true; }
        void Social()
        {
            var near = FBRole.npcs.Where(o => o && o != c && o.alive && (o.Pos - c.Pos).magnitude < 6).Select(o => o.GetComponent<FBRoleNpc>()).Where(o => o).ToList();
            var roda = near.FirstOrDefault(o => o.st == "talk" && o.talkC != null && FBRole.npcs.Count(x => x && x.GetComponent<FBRoleNpc>()?.talkC == o.talkC && x.GetComponent<FBRoleNpc>().st == "talk") < 4);
            if (roda && Random.value < .45f) { JoinTalk(roda.talkC.Value, Mathf.Max(4, roda.stT + Random.Range(-2f, 4f))); return; }
            var mate = near.FirstOrDefault(o => o.st == "walk" && (o.c.Pos - c.Pos).magnitude < 4.5f);
            if (mate && Random.value < .55f)
            {
                var C = (c.Pos + mate.c.Pos) / 2; var d = (mate.c.Pos - c.Pos).Flat().normalized; float T = Random.Range(10f, 24f);
                bool shake = (c.npcSet?.sex == "m" || mate.c.npcSet?.sex == "m") && Random.value < .55f && FBRegistry.I.Clip(FBNpcAnims.HANDSHAKE);
                foreach (var (a, sg) in new[] { (this, -1), (mate, 1) }) { a.Teleport(new Vector3(C.x + d.x * .55f * sg, a.c.Pos.y, C.z + d.z * .55f * sg)); a.JoinTalk(C, T, shake); }
                return;
            }
            if (FBRole.seats.Count > 0 && Random.value < .3f)
            {
                var S = FBRole.seats.Where(s => s.busy == null && (s.p - c.Pos).magnitude < 70).OrderBy(s => (s.p - c.Pos).sqrMagnitude).Skip(Random.Range(0, 3)).FirstOrDefault();
                if (S != null) { S.busy = c; seat = S; st = "gosit"; stT = 45; }
            }
        }
        void JoinTalk(Vector3 C, float T, bool shake = false)
        {
            c.StopDance(true); st = "talk"; stT = T; c.talking = true; talkC = C; vel = Vector3.zero; c.Yaw = Mathf.Atan2(C.x - c.Pos.x, C.z - c.Pos.z) * Mathf.Rad2Deg;
            if (shake && c.PlayDance(FBNpcAnims.HANDSHAKE)) { greet = true; if (c.dance != null) c.dance.len = Mathf.Min(c.dance.len, 6); return; }
            var tk = c.npcSet?.talk.Where(n => FBRegistry.I.Clip(n)).ToList(); if (tk != null && tk.Count > 0) c.PlayDance(FBUtil.Pick(tk));
        }
        // [parceiro] v1 (thread de física): tempo do Update medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
        static readonly int __fbt_Update = FBTempo.Id("FBRoleNpc.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            float dt = Time.deltaTime; if (dt <= 0 || !c) return;
            if (!c.alive) { vel = Vector3.zero; c.velocity = vel; return; }
            if (c.vehiclePose) return;                                                // dirigindo (trânsito)
            stT -= dt; float speed = 0; Vector3? desired = null;
            if (FBNpcRotina.On) { FBNpcVoz.Tick(); if (React(dt)) return; }
            c.fleeing = st == "flee";
            // carro do jogador vindo para cima: sai da frente
            var D = FBDrive.Cur;
            if (st != "dodge" && st != "sit" && st != "gocar")
            {
                // carro do jogador ou do trânsito vindo para cima: sai da frente para o lado mais perto
                void Check(Vector3 cp, Vector3 f, float v)
                {
                    if (st == "dodge" || v < 3.5f) return; var to = (c.Pos - cp).Flat(); if (to.sqrMagnitude > 14 * 14) return;
                    float ahead = Vector3.Dot(to, f), lat = to.x * f.z - to.z * f.x;
                    if (ahead > 0 && ahead < 4 + v * 1.1f && Mathf.Abs(lat) < 2.6f) { float s = lat == 0 ? 1 : Mathf.Sign(lat); c.StopDance(); prevSt = st == "party" ? "party" : "walk"; st = "dodge"; stT = 1.3f; dodgeP = new Vector3(c.Pos.x + f.z * s * 4.5f, c.Pos.y, c.Pos.z - f.x * s * 4.5f); LeaveSeat(); dodgeFrom = cp; dodgeFwd = f; c.talking = false; talkC = null; }
                }
                if (D) Check(D.transform.position, FBDrive.Fwd(D.yaw) * Mathf.Sign(D.v), Mathf.Abs(D.v));
                foreach (var tc in FBTransito.List) if (tc.car && tc.mode == "drive") Check(tc.pos, tc.fwd, tc.v);
            }
            if (st == "gocar")
            {
                float dg = goalP != null ? (c.Pos - goalP.Value).Flat().magnitude : 0;
                if (goalP != null && dg < 1.2f) { FBTransito.Arrive(c); return; } if (stT <= 0) st = "walk";
                if (dg < 4 && goalP != null) { var dir = (goalP.Value - c.Pos).Flat().normalized; Move(dir * 1.4f, dt); return; }
                desired = goalP; speed = 1.5f;
            }
            else if (st == "dodge")
            {
                desired = dodgeP; speed = 4.6f;
                if (stT <= 0)
                {
                    st = prevSt == "party" && partyW != null && partyW.on ? "party" : "walk"; stT = st == "party" ? Random.Range(10f, 30f) : Random.Range(4f, 10f);
                    if (FBNpcRotina.On && st == "walk")
                    {   // passou raspando: xinga, gesticula ou cai de susto (171)
                        var rel = (c.Pos - dodgeFrom).Flat(); float pass = Mathf.Abs(rel.x * dodgeFwd.z - rel.z * dodgeFwd.x);
                        float L = FBNpcRotina.NearMiss(c, c.Pos - Vector3.Cross(Vector3.up, dodgeFwd) * Mathf.Sign(rel.x * dodgeFwd.z - rel.z * dodgeFwd.x) * pass, pass);
                        if (L > 0) { st = "react"; stT = L; }
                    }
                }
            }
            else if (st == "pause") { vel = Vector3.zero; if (stT <= 0 || c.dance == null) { c.StopDance(true); st = "walk"; goalP = FBTransito.SidewalkPoint(c.Pos, 50) ?? goalP; stT = Random.Range(10f, 22f); } }
            else if (st == "flee") { desired = fleeP; speed = 3.8f; if (stT <= 0 || fleeP == null || (c.Pos - fleeP.Value).Flat().magnitude < 1.5f) { st = "walk"; goalP = null; stT = 0; c.fleeing = false; } }
            else if (st == "party")
            {
                var W = partyW; var car = W?.car; bool live = W != null && W.on && W.playing && car;
                if (!live || stT <= 0) { c.StopDance(); st = "walk"; partyCd = Random.Range(25f, 70f); goalP = FBTransito.SidewalkPoint(c.Pos, 60); stT = Random.Range(10f, 20f); }
                else
                {
                    var src = car.soundWallNode ? car.soundWallNode.position : car.transform.position;
                    if (c.dance == null && goalP != null && (c.Pos - goalP.Value).magnitude > 1.1f && !atParty) { desired = goalP; speed = 1.5f; }
                    else { atParty = true; c.Yaw = Mathf.LerpAngle(c.Yaw, Mathf.Atan2(src.x - c.Pos.x, src.z - c.Pos.z) * Mathf.Rad2Deg, Mathf.Min(1, dt * 3)); if (c.dance == null && dancer && (danceT -= dt) <= 0) { var own = c.npcSet?.dance.Where(n => FBRegistry.I.Clip(n)).ToList(); c.PlayDance(FBUtil.Pick(own != null && own.Count > 0 ? own : FBRole.Dances().ToList())); danceT = Random.Range(.6f, 3.5f); } }   // dança do próprio pacote quando tem
                }
            }
            else if (st == "gosit" || st == "sit" || st == "standup")
            {
                var S = seat;
                if (S == null) st = "walk";
                else if (st == "gosit")
                {
                    var app = FBRole.SeatApproach(S); float dd = (c.Pos - app).Flat().magnitude;
                    if (stT <= 0 || (S.busy && S.busy != c)) { st = "walk"; seat = null; if (S.busy == c) S.busy = null; }
                    else if (dd < .7f) { if (!SeatNow(S, true)) { st = "walk"; S.busy = null; seat = null; } }
                    else { desired = app; speed = c.walkSpeed; }
                }
                else if (st == "sit") { vel = Vector3.zero; c.Yaw = S.yaw + seatYaw; if (stT <= 0 || c.dance == null) { c.StopDance(); st = "standup"; stT = 8; } }
                else { vel = Vector3.zero; if (c.dance == null || stT <= 0) { c.StopDance(true); LeaveSeat(); st = "walk"; stT = 0; goalP = null; } }
            }
            else
            {
                if (st == "talk")
                {
                    if (talkC != null) c.Yaw = Mathf.LerpAngle(c.Yaw, Mathf.Atan2(talkC.Value.x - c.Pos.x, talkC.Value.z - c.Pos.z) * Mathf.Rad2Deg, Mathf.Min(1, dt * 4));
                    if (talkC != null && !greet) FBNpcRotina.TalkTick(c, talkC.Value, dt);
                    if (greet && c.dance == null) { greet = false; var tk = c.npcSet?.talk.Where(n => FBRegistry.I.Clip(n)).ToList(); if (tk != null && tk.Count > 0) c.PlayDance(FBUtil.Pick(tk)); }
                    if (stT <= 0) { st = "walk"; c.talking = false; talkC = null; c.StopDance(true); }
                }
                if ((partyCd -= dt) <= 0)
                {
                    partyCd = 2.5f;
                    foreach (var W in FBParedao.List())
                    {
                        if (!W.on || !W.playing || !W.car || W.local) continue; if ((W.car.transform.position - c.Pos).magnitude > 95) continue;
                        if (FBRole.npcs.Count(o => o && o.GetComponent<FBRoleNpc>() is { } r && r.partyW == W && r.st == "party") >= 14) continue;
                        var car = W.car; var back = -car.transform.forward.Flat().normalized; var side = new Vector3(back.z, 0, -back.x); float rear = -car.lbox.min.z * car.scale;
                        var spot = car.transform.position + back * (rear + Random.Range(3f, 9f)) + side * Random.Range(-5f, 5f);
                        goalP = FBNav.RandomPoint(spot, 2.5f) ?? spot; st = "party"; partyW = W; atParty = false; stT = Random.Range(20f, 60f); danceT = Random.Range(.5f, 4f); c.talking = false; LeaveSeat(); break;
                    }
                }
                if (st == "walk")
                {
                    if (goalP == null || (c.Pos - goalP.Value).magnitude < 1.5f || stT <= 0)
                    {
                        if (goalP != null) Social();                                                     // conversar/sentar primeiro; sozinho, às vezes pausa (171)
                        if (goalP != null && st == "walk" && FBNpcRotina.On) { float T = FBNpcRotina.Pause(c); if (T > 0) { st = "pause"; stT = T; vel = Vector3.zero; } }
                        if (st == "walk") { goalP = FBTransito.SidewalkPoint(c.Pos, 50) ?? goalP; stT = Random.Range(10f, 22f); }
                    }
                    desired = goalP; speed = c.walkSpeed; FBNpcRotina.WalkTick(c, dt);
                }
            }
            var tv = Vector3.zero;
            if (desired is Vector3 dsd)
            {
                // caminho pela calçada/praça (asfalto custa 6×: só atravessa onde precisa); refeito quando o alvo muda, travou ou de tempos em tempos
                navT -= dt;
                if (path == null || pathGoal == null || (pathGoal.Value - dsd).sqrMagnitude > 4 || navT <= 0)
                { navT = 9 + Random.value * 5; pathGoal = dsd; path = (FBPedNav.Ready ? FBPedNav.Path(c.Pos, dsd) : null) ?? FBNav.Path(c.Pos, dsd) ?? new List<Vector3> { dsd }; }
                while (path.Count > 1 && (path[0] - c.Pos).Flat().magnitude < 1.0f) path.RemoveAt(0);
                var t = (Carrot(1.7f) - c.Pos).Flat();
                if (vel.sqrMagnitude > .04f && st == "walk")
                {
                    float ang = Vector3.Angle(vel.Flat(), t); if (ang > 45) speed *= Mathf.Lerp(1, .5f, Mathf.InverseLerp(45, 140, ang));   // quina fechada: diminui o passo
                    speed *= 1 + .06f * Mathf.Sin(Time.time * .31f + phase);                                                         // ritmo não é de robô
                }
                if (t.magnitude > .3f)
                {
                    var dir = t.normalized; bool wait = false;
                    if (FBRoads.Ready)
                    {
                        bool onRoad = FBRoads.IsRoad(c.Pos);
                        if (!onRoad && (FBRoads.IsRoad(c.Pos + dir * 1.1f) || FBRoads.IsRoad(c.Pos + dir * 2.1f)))
                        {   // vai descer da guia: olha os carros; vindo carro, espera (até 14 s; depois só espera se estiver bem perto)
                            var cross = c.Pos + dir * 4f;
                            bool risk = FBTransito.Danger(cross, 3.2f, crossWait < 14 ? 5.5f : 2f) || PlayerCarComing(cross);
                            if (risk) { crossWait += dt; wait = true; } else crossWait = 0;
                        }
                        else if (onRoad) { crossWait = 0; speed *= 1.3f; }                     // no meio da rua: apressa o passo
                    }
                    if (!wait) { tv = dir * speed; c.Yaw = Mathf.LerpAngle(c.Yaw, Quaternion.LookRotation(t).eulerAngles.y, Mathf.Min(1, dt * 5)); }
                    else c.Yaw = Mathf.LerpAngle(c.Yaw, Quaternion.LookRotation(t).eulerAngles.y, Mathf.Min(1, dt * 3));
                }
            }
            // desvio antecipado: quem vem no rumo de encontro em até 2,5 s (e passaria a menos de 0,9 m) -> abre para a direita aos poucos
            if (tv.sqrMagnitude > .04f)
            {
                var side0 = new Vector3(tv.z, 0, -tv.x).normalized; float steer = 0;
                void Avoid(Vector3 op, Vector3 ov, float rad)
                {
                    var dp = (op - c.Pos).Flat(); float d2 = dp.sqrMagnitude; if (d2 > 36 || d2 < 1e-4f) return; var dv = (ov - tv).Flat(); float dvv = dv.sqrMagnitude; if (dvv < 1e-4f) return;
                    float tt = -Vector3.Dot(dp, dv) / dvv; if (tt <= 0 || tt > 2.5f) return; var closest = dp + dv * tt; if (closest.magnitude > rad) return;
                    float lat = Vector3.Dot(dp, side0); steer += (lat > .15f ? -1 : 1) * (1 - tt / 2.5f) * (rad - closest.magnitude) / rad;   // ele à minha direita -> vou para a esquerda; senão, direita
                }
                foreach (var o in FBRole.npcs) { if (!o || o == c || !o.alive) continue; Avoid(o.Pos, o.velocity, .9f); }
                var me0 = FBMatch.Player; if (me0 && !me0.vehiclePose) Avoid(me0.Pos, me0.velocity, 1.1f);
                if (Mathf.Abs(steer) > .01f) tv = (tv + side0 * Mathf.Clamp(steer, -1, 1) * tv.magnitude * .8f).normalized * tv.magnitude;
            }
            // não atropela os outros: afasta de quem está a menos de 1,1 m (e do jogador) — desvia andando, sem empurrar parado
            if (tv.sqrMagnitude > 0)
            {
                Vector3 push = Vector3.zero;
                foreach (var o in FBRole.npcs) { if (!o || o == c) continue; var d = (c.Pos - o.Pos).Flat(); float m = d.sqrMagnitude; if (m < 1.21f && m > 1e-6f) { m = Mathf.Sqrt(m); push += d / m * (1.1f - m); } }
                var me = FBMatch.Player; if (me && !me.vehiclePose) { var d = (c.Pos - me.Pos).Flat(); float m = d.magnitude; if (m < 1.3f && m > 1e-3f) push += d / m * (1.3f - m) * 1.6f; }
                if (push.sqrMagnitude > 1e-6f) { var side = new Vector3(tv.z, 0, -tv.x).normalized; tv += side * Vector3.Dot(push, side) * 2.2f + push * .6f; }
            }
            if (c.dance != null) tv = Vector3.zero;
            // travou num prop novo (fora da navmesh): troca de rota em vez de andar parado
            if (tv.sqrMagnitude > 0)
            {
                if ((stkT += dt) > 1.5f)
                {
                    float moved = stkP != null ? (c.Pos - stkP.Value).Flat().magnitude : 9; stkT = 0; stkP = c.Pos;
                    if (moved < .45f)
                    {
                        stkN++; path = null; if (st == "gosit") { LeaveSeat(); st = "walk"; }
                        if (st == "walk" || stkN > 1) { goalP = FBTransito.SidewalkPoint(c.Pos, 35) ?? goalP; stT = Random.Range(8f, 16f); }
                        c.Yaw += (Random.value < .5f ? 1 : -1) * 68; tv = Quaternion.Euler(0, c.Yaw, 0) * Vector3.forward * 1.2f;
                    }
                    else stkN = 0;
                }
            }
            else { stkT = 0; stkP = null; }
            if (seat != null && (st == "sit" || st == "standup")) { var p = transform.position; p.y = seat.p.y + seatY; if (cc) cc.enabled = false; transform.position = p; vel = Vector3.zero; c.velocity = Vector3.zero; c.animVel = Vector2.zero; c.grounded = true; return; }
            if (cc && !cc.enabled) cc.enabled = true;
            Move(tv, dt);
        }
        // [parceiro] v1 (thread de física): tempo do LateUpdate medido pelo FBTempo
        static readonly int __fbt_LateUpdate = FBTempo.Id("FBRoleNpc.LateUpdate");
        void LateUpdate()
        {
            if (!FBTempo.On) { LateUpdate__fbt(); return; }
            long t0 = FBTempo.T0(); LateUpdate__fbt(); FBTempo.Soma(__fbt_LateUpdate, t0);
        }
        void LateUpdate__fbt()
        {
            if (seat == null || st != "sit" || !c || !c.an || float.IsNaN(seat.h)) return; var d = c.dance; if (d == null || d.t < .35f) return;
            if (System.Text.RegularExpressions.Regex.IsMatch(d.clip ?? "", "stand_to_sit|sit_to_stand", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return;
            var hips = c.an.GetBoneTransform(HumanBodyBones.Hips); var lt = c.an.GetBoneTransform(HumanBodyBones.LeftUpperLeg); var rt = c.an.GetBoneTransform(HumanBodyBones.RightUpperLeg); if (!hips) return;
            float k = transform.lossyScale.y; float butt = Mathf.Min(hips.position.y, lt && rt ? (lt.position.y + rt.position.y) / 2 : hips.position.y) - .085f * k;   // osso do quadril -> apoio no assento
            // clipes de sentar do pacote novo vêm com o corpo virado/deslocado em relação à raiz: a FRENTE DA BACIA (coxas) vai para a frente do
            // assento e o quadril para o ponto do assento, aos poucos e a cada quadro (antes: só a posição, uma vez, desistindo acima de 60 cm)
            float kk = Mathf.Min(1, Time.deltaTime * 6);
            if (lt && rt)
            {
                var fw = Vector3.Cross(rt.position - lt.position, Vector3.up); fw.y = 0; var want = Quaternion.Euler(0, seat.yaw, 0) * Vector3.forward;
                if (fw.sqrMagnitude > 1e-6f) { float da = Vector3.SignedAngle(fw, want, Vector3.up); seatYaw = Mathf.Clamp(seatYaw + da * kk, -180, 180); transform.rotation = Quaternion.Euler(0, seat.yaw + seatYaw, 0); }
                float back = seat.kind == "cadeira" ? .07f : .02f; var tgt = seat.p - want * back; var e = new Vector3(tgt.x - hips.position.x, 0, tgt.z - hips.position.z);
                seatXZ = Vector3.ClampMagnitude(seatXZ + e * kk, 1.2f);
            }
            float err = seat.h - butt; float step = Mathf.Abs(err) < .006f ? 0 : Mathf.Clamp(err, -.6f, .6f) * Mathf.Min(1, Time.deltaTime * 10); seatY = Mathf.Clamp(seatY + step, -1.1f, .4f);   // clipes do pacote novo sentam com o quadril até ~1 m acima da raiz
            seatBase ??= transform.position - seatXZ; transform.position = new Vector3(seatBase.Value.x + seatXZ.x, seat.p.y + seatY, seatBase.Value.z + seatXZ.z);
        }
        Vector3? seatBase;
        void LeaveSeat() { if (c) c.seated = false; if (seat != null && seat.busy == c) seat.busy = null; seat = null; seatY = 0; seatYaw = 0; seatXZ = Vector3.zero; seatBase = null; if (cc && !cc.enabled) cc.enabled = true; }
        void Move(Vector3 tv, float dt)
        {
            {
                float cur = vel.Flat().magnitude, want = tv.Flat().magnitude; bool rush = st == "dodge";
                float acc = rush ? 7 : 1.5f, dec = rush ? 8 : 2.8f; float ns = Mathf.MoveTowards(cur, want, (want > cur ? acc : dec) * dt);
                Vector3 hd = cur > .05f ? vel.Flat() / cur : (want > .01f ? tv.Flat() / want : transform.forward.Flat().normalized);
                if (want > .01f) { var td = tv.Flat() / want; float maxTurn = (rush ? 540 : cur < .4f ? 260 : 170) * dt; hd = Vector3.RotateTowards(hd, td, maxTurn * Mathf.Deg2Rad, 0); }
                vel = hd * ns;
            }
            if (cc && cc.enabled) { if (cvStep < 0) cvStep = cc.stepOffset; vy = cvGround ? -2 : vy - 19 * dt; FBMotor.Move(cc, (vel + Vector3.up * vy) * dt, cvGround, vy > 0, cvStep, .45f * c.ScaleK, ref cvSlide, out cvGround, out cvG); }   // mesma física de chão do jogador (degrau/escada)
            else vy = cc && cc.isGrounded ? -2 : vy - 19 * dt;
            // árvore desarmada: andar = VelZ 1,57 (passeio lento ainda é o ciclo de andar)
            float g = vel.magnitude, sp = g * 1.12f; var lv = Quaternion.Inverse(Quaternion.Euler(0, c.Yaw, 0)) * vel; float lm = lv.magnitude, sw = g > .35f && g < 2.2f ? Mathf.Max(sp, 1.57f) : sp;
            c.animVel = g < .05f ? Vector2.zero : new Vector2(lv.x / lm * sw, lv.z / lm * sw); c.velocity = vel; c.grounded = !cc || (cc.enabled ? cvGround : cc.isGrounded);
            if (tv.sqrMagnitude > 0 && vel.sqrMagnitude > .01f) c.Yaw = Mathf.LerpAngle(c.Yaw, Quaternion.LookRotation(vel.Flat().sqrMagnitude > 1e-4f ? vel.Flat() : transform.forward).eulerAngles.y, Mathf.Min(1, dt * 6));
        }
    }

    // ---- TRÂNSITO: Runtime/Modes/FBTraffic.cs (FBTransito, faixas de Runtime/World/FBRoads.cs)

    // ---- helicóptero só de passagem: rotas fixas cruzando o mapa, alto, de tempos em tempos
    public static class FBRoleHeli
    {
        static GameObject g; static List<(Transform t, Quaternion b, bool main)> rotors = new(); static (Vector3 a, Vector3 b, float alt)[] routes; static int ri; static float t = -1, wait, len, ang; static Vector3 dir;
        public static void Spawn()
        {
            var pf = FBRegistry.I.Vehicle("heli"); if (!pf) return; g = Object.Instantiate(pf); g.name = "Heli_Passagem"; foreach (var c in g.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            rotors.Clear(); foreach (var tr in g.GetComponentsInChildren<Transform>()) { var n = tr.name; if (n == "Helices" || n == "Rotor") rotors.Add((tr, tr.localRotation, true)); else if (n == "Helices-02" || n == "Rotor-02") rotors.Add((tr, tr.localRotation, false)); }
            Bounds box = new(Vector3.zero, new Vector3(300, 30, 300)); bool any = false; foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)) if (r.gameObject.layer == FBWorld.LMap) { if (!any) { box = r.bounds; any = true; } else box.Encapsulate(r.bounds); }
            var c0 = box.center; float R = Mathf.Max(box.size.x, box.size.z) / 2 + 160;
            routes = new[] { .4f, 2.1f, 3.7f, 5.2f }.Select((a, i) => { var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)); var off = new Vector3(d.z, 0, -d.x) * new[] { -30f, 25, 0, 45 }[i]; return (c0 + off - d * R, c0 + off + d * R, box.max.y + 45 + i * 6); }).ToArray();
            ri = Random.Range(0, 4); t = -1; wait = Random.Range(25f, 50f); g.transform.position = c0 + Vector3.down * 400;
        }
        public static void Tick(float dt)
        {
            if (!g) return;
            if (t < 0) { if ((wait -= dt) > 0) return; t = 0; ri = (ri + 1) % routes.Length; var r0 = routes[ri]; len = Vector3.Distance(r0.a, r0.b); dir = (r0.b - r0.a).normalized; }
            var r = routes[ri]; t += dt * 26;
            if (t > len) { t = -1; wait = Random.Range(70f, 150f); g.transform.position += Vector3.down * 400; var hs0 = g.GetComponent<FBHeliSound>(); if (hs0) hs0.vol = 0; return; }
            var p = r.a + dir * t; p.y = r.alt + Mathf.Sin(t * .02f) * 2; g.transform.SetPositionAndRotation(p, Quaternion.LookRotation(dir) * Quaternion.Euler(5.7f, 0, 0));
            ang += dt * 1500; foreach (var (tr, b, main) in rotors) if (tr) tr.localRotation = b * Quaternion.AngleAxis(main ? ang : ang * 1.6f, main ? Vector3.up : Vector3.right);
            FBHeliSound.Attach(g, .95f);                                                           // som na própria aeronave (volume, alcance e ar)
        }
    }

    // ---- garagem do rolê: escolhe o carro (prévia na vaga) e entra direto
    public static class FBGarage
    {
        static GameObject ui; static List<string> ids; static int idx; static FBCar prev; public static bool Open_ => ui;
        public static void Open()
        {
            if (FBRole.spot == null || !FBMatch.Player) return;
            ids = GameData.Vehicles.Keys.Where(k => FBRegistry.I.Vehicle(k) && (Json.L(GameData.Vehicles[k], "wheels")?.Count ?? 0) > 0 && !k.ToLowerInvariant().Contains("viatura")).OrderBy(k => Json.S(GameData.Vehicles[k], "name") ?? k).ToList(); if (ids.Count == 0) return;
            var last = PlayerPrefs.GetString("fb_role_car", "S10_2017"); idx = Mathf.Max(0, ids.IndexOf(last));
            anchor = FreeAnchor(FBRole.spot.Value.p, FBRole.spot.Value.yaw);                     // um ponto só para todos os carros (cabe o maior)
            var cv = FBUI.Canvas("Garagem", 34); ui = cv.gameObject; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            var p = FBUI.Panel(cv.transform, "barra", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 90), new Vector2(720, 130), FBUI.Dark).transform;
            var nm = FBUI.Label(p, "", 26, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -32), new Vector2(520, 36), TextAnchor.MiddleCenter, true, FBUI.Gold);
            FBUI.Label(p, "GARAGEM · ESCOLHA O CARRO DO ROLÊ", 12, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -8), new Vector2(520, 18), TextAnchor.MiddleCenter, false, FBUI.Mut);
            void Show() { nm.text = (Json.S(GameData.Vehicles[ids[idx]], "name") ?? ids[idx]).Replace('_', ' ').ToUpperInvariant(); Preview(ids[idx]); }
            FBUI.Button(p, "◀", new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(50, -10), new Vector2(70, 56), () => { idx = (idx - 1 + ids.Count) % ids.Count; Show(); });
            FBUI.Button(p, "▶", new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-50, -10), new Vector2(70, 56), () => { idx = (idx + 1) % ids.Count; Show(); });
            FBUI.Button(p, "PEGAR", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 34), new Vector2(240, 50), Pick, true);
            // portas / capô / porta-malas (dados do Animator do REB)
            var q = FBUI.Panel(cv.transform, "pecas", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 186), new Vector2(520, 50), FBUI.Dark).transform;
            FBUI.Button(q, "PORTAS (U)", new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(92, 0), new Vector2(166, 40), () => FBDoors.Toggle(prev, FBDoors.DOORS));
            FBUI.Button(q, "CAPÔ (J)", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 0), new Vector2(166, 40), () => FBDoors.Toggle(prev, FBDoors.HOOD));
            FBUI.Button(q, "MALA (P)", new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-92, 0), new Vector2(166, 40), () => FBDoors.Toggle(prev, FBDoors.TRUNK));
            Show();
        }
        static void Preview(string id)
        {
            var me = FBMatch.Player;
            if (prev) { if (me && FBVehicles.CarOf(me) == prev) FBDrive.ReleaseSeat(me); FBVehicles.list.Remove(prev); Object.Destroy(prev.gameObject); }
            var s = FBRole.spot.Value; var d = GameData.Vehicles[id]; var pc = Json.L(d, "paint"); uint paint = pc != null ? ((uint)(Json.Fv(pc[0]) * 255) << 16) | ((uint)(Json.Fv(pc[1]) * 255) << 8) | (uint)(Json.Fv(pc[2]) * 255) : 0xc9cbcd;
            var fit = (Json.L(Json.O(d, "som"), "fit") ?? new List<object>()).Select(x => x as string).Where(x => FBRegistry.I.Som(x)).ToList();
            var A = anchor ?? (s.p, s.yaw);
            prev = FBVehicles.Make(id, A.p, A.yaw, true, () => .5f, true, paint, fit.Count > 0, fit.FirstOrDefault(m => m.Contains("Grave") || m.Contains("Trio")) ?? fit.FirstOrDefault());   // caixa grave / trio: o paredão de respeito
            if (prev) { prev.syncId = "role:" + id; FBTune.Apply(prev, FBTune.Load(id)); }   // configuração salva da oficina
            if (prev)
            {   // alinhamento padrão: o CENTRO da caixa do carro no ponto (a origem de cada modelo do REB fica num lugar diferente)
                var c = prev.transform.TransformPoint(prev.lbox.center); var dlt = A.p - c; dlt.y = 0; prev.transform.position += dlt; Physics.SyncTransforms();
                FBRole.spot = (A.p, A.yaw);
                // rodada 38 (pedido 22/10): o jogador já nasce SENTADO no carro em prévia — sem controle até escolher (PEGAR)
                if (me && me.alive) { FBVehicles.fp = false; FBDrive.Enter(me, prev, true); }   // r50: sentado na hora (o clipe de entrar começava do meio do carro)
            }
        }
        static (Vector3 p, float yaw)? anchor;
        // vaga da garagem: a primeira em volta do ponto pedido onde cabe uma caixa de 6,8 × 2,7 m (o maior carro do REB) virada no mesmo rumo
        static (Vector3 p, float yaw) FreeAnchor(Vector3 p0, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0); var ext = new Vector3(1.35f, .6f, 3.4f);
            bool Free(Vector3 q) { foreach (var h in Physics.OverlapBox(q + Vector3.up * .95f, ext, rot, FBWorld.SolidMask, QueryTriggerInteraction.Ignore)) if (!h.GetComponentInParent<FBCar>() && !h.GetComponentInParent<FBCharacter>()) return false; return true; }
            if (Free(p0)) return (p0, yaw);
            for (int k = 0; k < 64; k++)
            {
                float a = k * 137.5f * Mathf.Deg2Rad, r = 2.5f + k * .35f; var q = p0 + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                if (!(FBWorld.GroundY(q.x, p0.y + 1.5f, q.z, .3f, 4) is float gy) || Mathf.Abs(gy - p0.y) > 1.2f) continue; q.y = gy;
                if (Free(q)) return (q, yaw);
            }
            return (p0, yaw);
        }
        static bool Blocked(FBCar c, Vector3 p, Quaternion r)
        {
            var b = c.lbox; var ext = Vector3.Scale(b.extents, Vector3.one * c.scale) * .9f; ext.y = Mathf.Max(.1f, ext.y - .35f);
            var ctr = p + r * (b.center * c.scale) + Vector3.up * .3f;
            foreach (var h in Physics.OverlapBox(ctr, ext, r, FBWorld.SolidMask, QueryTriggerInteraction.Ignore)) if (!h.transform.IsChildOf(c.transform)) return true;
            return false;
        }
        // câmera da garagem: 3/4 de frente, como a vitrine do web (o jogador não comanda a câmera enquanto escolhe)
        public static bool Camera(Camera cam, float dt)
        {
            if (!ui || !prev || !cam) return false;
            if (!FBInput.Touch && (Cursor.lockState != CursorLockMode.None || !Cursor.visible)) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }   // escolhendo: cursor sempre à mostra
            var b = prev.lbox; float r = Mathf.Max(b.size.x, b.size.z) * prev.scale * .52f + b.size.y * prev.scale * .45f;
            var c = prev.transform.TransformPoint(b.center); var want = c + prev.transform.rotation * (Quaternion.Euler(0, 36, 0) * new Vector3(0, r * .32f, r * 1.9f));
            cam.transform.position = Vector3.Lerp(cam.transform.position, want, 1 - Mathf.Exp(-dt * 6)); cam.transform.rotation = Quaternion.LookRotation(c - Vector3.up * b.size.y * prev.scale * .06f - cam.transform.position); cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 40, 1 - Mathf.Exp(-dt * 6));
            return true;
        }
        public static void AutoPick() { if (ui) Pick(); }                                         // autoteste do rolê
        static void Pick()
        {
            if (!prev) return; PlayerPrefs.SetString("fb_role_car", prev.id); FBRole.roleCar = prev; var car = prev; prev = null; Object.Destroy(ui); ui = null;
            if (!FBInput.Touch) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            FBVehicles.fp = false; if (FBVehicles.CarOf(FBMatch.Player) != car) FBDrive.Enter(FBMatch.Player, car, true); if (FBPlayer.I) { FBPlayer.I.camYaw = car.transform.eulerAngles.y; FBPlayer.I.camPitch = -7; }
            FBHUD.I?.Center("BOM ROLÊ! · G = GPS · K = SOM · N = LIGA O PAREDÃO", 3.5f);
        }
    }
}
