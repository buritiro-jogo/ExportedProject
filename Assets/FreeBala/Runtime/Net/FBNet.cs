// Multiplayer: o MESMO protocolo do jogo web (server.mjs / net.js), então jogadores do Unity e do navegador entram na mesma sala.
// hello -> list -> join/create; 'st' ~20x/s com posição/velocidade/yaw/pitch/flags/arma/vida (coordenadas convertidas web<->Unity);
// eventos 'shot', 'hit' (quem leva o tiro aplica o dano em si), 'die' (placar no servidor). Remotos interpolados ~110 ms no passado.
// Transporte: System.Net.WebSockets (Windows/Android/iOS) ou a ponte JS (Plugins/WebGL/FBWebSocket.jslib) no WebGL.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
#else
using System.Runtime.InteropServices;
#endif

namespace FreeBala
{
    public class FBNet : MonoBehaviour
    {
        public static FBNet I; public int myId; public bool open; public float ping = 80; public string status = "CONECTANDO…"; public readonly int[] score = new int[2];
        readonly ConcurrentQueue<string> inbox = new(); readonly Dictionary<int, FBRemote> remotes = new(); float sendT, pingT;
        const int F_SPRINT = 1, F_CROUCH = 2, F_AIM = 4, F_SHOOT = 8, F_GROUND = 16, F_ALIVE = 32;

#if !UNITY_WEBGL || UNITY_EDITOR
        ClientWebSocket ws; CancellationTokenSource cts;
        async void Connect(string url)
        {
            ws = new ClientWebSocket(); cts = new CancellationTokenSource();
            try { await ws.ConnectAsync(new Uri(url), cts.Token); open = true; OnOpen(); _ = Task.Run(Receive); }
            catch (Exception e) { status = "SEM CONEXÃO"; FBHUD.I?.Center("SEM CONEXÃO: " + e.Message, 5); }
        }
        async Task Receive()
        {
            var buf = new byte[1 << 16]; var sb = new StringBuilder();
            while (ws.State == WebSocketState.Open)
            {
                WebSocketReceiveResult r; try { r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token); } catch { break; }
                if (r.MessageType == WebSocketMessageType.Close) break;
                sb.Append(Encoding.UTF8.GetString(buf, 0, r.Count)); if (r.EndOfMessage) { inbox.Enqueue(sb.ToString()); sb.Clear(); }
            }
            open = false;
        }
        public void SendRaw(string s) { if (ws == null || ws.State != WebSocketState.Open) return; var b = Encoding.UTF8.GetBytes(s); _ = ws.SendAsync(new ArraySegment<byte>(b), WebSocketMessageType.Text, true, cts.Token); }
        void OnDestroy() { try { cts?.Cancel(); ws?.Dispose(); } catch { } }
#else
        [DllImport("__Internal")] static extern void FBWS_Connect(string url, string goName);
        [DllImport("__Internal")] static extern void FBWS_Send(string msg);
        void Connect(string url) { FBWS_Connect(url, gameObject.name); }
        public void SendRaw(string s) { if (open) FBWS_Send(s); }
        public void WSOpen(string _) { open = true; OnOpen(); }
        public void WSMessage(string m) { inbox.Enqueue(m); }
        public void WSClose(string _) { open = false; }
#endif

        void Awake() { I = this; gameObject.name = "FBNet"; FBDamageEvents.OnDamage += OnDamage; }
        void OnDisable() { FBDamageEvents.OnDamage -= OnDamage; }

        // Réplica de acertos para os efeitos (sangue/impacto no ponto real): quem atira conhece o ponto exato na hitbox e avisa a sala
        // ('hfx'; o web ignora eventos desconhecidos). Os outros clientes publicam um FBDamage visual (applied 0, why "net") na vítima.
        // A vítima não usa o 'hfx' (já gerou o próprio efeito ao aplicar o dano recebido no 'hit').
        void OnDamage(FBDamage d)
        {
            if (!open || d.why == "net" || !d.hit.valid || !d.victim || !d.victim.isRemote || !d.source || !d.source.isPlayer) return;
            var r = d.victim.GetComponent<FBRemote>(); if (!r) return;
            SendEv("hfx", new Dictionary<string, object> { ["vid"] = r.netId, ["p"] = R(Coords.ToWeb(d.hit.point)), ["n"] = R(Coords.ToWeb(d.hit.normal)), ["d"] = R(Coords.ToWeb(d.hit.dir)), ["pt"] = d.hit.part ?? "", ["head"] = d.head, ["dmg"] = Mathf.RoundToInt(d.amount), ["w"] = d.hit.weaponId ?? "" });
        }
        void HitFx(Dictionary<string, object> m, FBRemote shooter)
        {
            int vid = Json.I(m, "vid"); if (vid == myId || !remotes.TryGetValue(vid, out var vr) || !vr || !vr.ch.alive) return;
            var hit = new FBHitInfo { valid = true, point = Coords.FromWeb(Json.L(m, "p")), normal = Coords.FromWeb(Json.L(m, "n")), dir = Coords.FromWeb(Json.L(m, "d")), part = Json.S(m, "pt"), weaponId = Json.S(m, "w") };
            if (shooter) hit.dist = Vector3.Distance(shooter.ch.Pos, hit.point);
            FBDamageEvents.Raise(new FBDamage { victim = vr.ch, source = shooter ? shooter.ch : null, type = FBDamageType.Bullet, amount = Json.F(m, "dmg"), applied = 0, hpBefore = vr.ch.hp, hpAfter = vr.ch.hp, head = Json.B(m, "head"), hit = hit, why = "net" });
        }
        void Start() { Connect(FBSession.server); }
        void OnOpen()
        {
            Send(new Dictionary<string, object> { ["t"] = "hello", ["name"] = FBSession.name, ["skin"] = FBSession.FullSkin, ["team"] = FBSession.team, ["code"] = "" });
            Send(new Dictionary<string, object> { ["t"] = "list" });
        }
        public void Send(Dictionary<string, object> o) => SendRaw(ToJson(o));

        // [parceiro 01/11] cronômetro por script (FBTempo, ligado só pelo teste do .exe); desligado custa só um teste de bool
        static readonly int __fbt_Update = FBTempo.Id("FBNet.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            while (inbox.TryDequeue(out var s)) { try { Recv(Json.Parse(s) as Dictionary<string, object>); } catch (Exception e) { Debug.LogWarning("[FBNet] " + e.Message); } }
            if (!open) return;
            sendT -= Time.deltaTime; pingT -= Time.deltaTime;
            if (pingT <= 0) { pingT = 2; Send(new Dictionary<string, object> { ["t"] = "ping", ["ts"] = Time.realtimeSinceStartup * 1000 }); }
            var p = FBPlayer.I; if (sendT > 0 || !p) return;
            var st = BuildState(p, Time.realtimeSinceStartup); if (st != null) Send(st);
        }
        // Estado 'st' (mesmos campos do main.js netSendState). Compressão compatível com o server.mjs (que só repassa):
        //  · 20 Hz mexendo, 5 Hz parado (nada mudou além de ruído) — o net.js/FBRemote seguram o último estado;
        //  · arma/skin/acessórios/dança/pose só quando mudam e em todo pacote "completo" (1x/s, para quem acabou de entrar).
        float fullT; string sentW = "", sentDn = ""; Vector3 sentP; float sentYaw, sentPi; int sentF = -1, sentHp = -1; bool sentVe;
        public Dictionary<string, object> BuildState(FBPlayer p, float now)
        {
            var ch = p.ch; int f = (ch.sprint ? F_SPRINT : 0) | (ch.crouch ? F_CROUCH : 0) | (ch.aiming ? F_AIM : 0) | (ch.shooting ? F_SHOOT : 0) | (ch.grounded ? F_GROUND : 0) | (ch.alive ? F_ALIVE : 0);
            var ve = FBDrive.VehicleState(); int hp = Mathf.Max(0, Mathf.RoundToInt(ch.hp)); var wid = p.wu.Cur?.id; var dn = ch.dance != null && !ch.dance.leaving ? ch.dance.clip ?? "" : "";
            float yawU = ch.transform.eulerAngles.y, pitch = p.camPitch;
            bool full = now >= fullT, changed = full || ve != null || sentVe || f != sentF || hp != sentHp || wid != sentW || dn != sentDn || (ch.transform.position - sentP).sqrMagnitude > .0004f || Mathf.Abs(Mathf.DeltaAngle(yawU, sentYaw)) > .5f || Mathf.Abs(pitch - sentPi) > .5f;
            sendT = changed ? .05f : .2f; if (!changed && now - lastSend < .2f) return null;
            var pos = Coords.ToWeb(ch.transform.position); var v = Coords.ToWeb(ch.velocity); float yw = Coords.YawToWeb(yawU), ay = Coords.YawToWeb(p.camYaw);
            var m = new Dictionary<string, object> { ["t"] = "st", ["p"] = R(pos), ["v"] = R(v), ["y"] = Round(yw), ["ay"] = Round(ay), ["pi"] = Round(pitch * Mathf.Deg2Rad), ["f"] = f, ["ln"] = Round(ch.lean), ["hp"] = hp, ["ve"] = ve, ["vt"] = null };
            if (full || wid != sentW) { m["w"] = wid; m["ws"] = -1; m["wa"] = ""; }
            if (full || dn != sentDn) { m["dn"] = dn; m["wp"] = ""; }
            if (full) { m["av"] = new float[] { 0, 0 }; fullT = now + 1; }
            sentW = wid; sentDn = dn; sentP = ch.transform.position; sentYaw = yawU; sentPi = pitch; sentF = f; sentHp = hp; sentVe = ve != null; lastSend = now;
            return m;
        }
        float lastSend;
        static float Round(float x) => Mathf.Round(x * 100) / 100f;
        static float[] R(float[] a) { for (int i = 0; i < a.Length; i++) a[i] = Round(a[i]); return a; }

        void Recv(Dictionary<string, object> m)
        {
            switch (Json.S(m, "t"))
            {
                case "hi": myId = Json.I(m, "id"); break;
                case "pong": ping = Time.realtimeSinceStartup * 1000 - Json.F(m, "ts"); break;
                case "rooms":
                    {
                        string join = null; foreach (var r in Json.L(m, "rooms") ?? new List<object>()) if (Json.S(r, "map") == FBSession.map && Json.I(r, "players") < Json.I(r, "max")) { join = Json.S(r, "id"); break; }
                        if (join != null) Send(new Dictionary<string, object> { ["t"] = "join", ["id"] = join });
                        else Send(new Dictionary<string, object> { ["t"] = "create", ["name"] = "Sala de " + FBSession.name, ["mode"] = "tdm", ["map"] = FBSession.map, ["max"] = 10 });
                        break;
                    }
                case "joined":
                    {
                        var you = Json.O(m, "you"); myId = Json.I(you, "id"); var me = FBPlayer.I?.ch; if (me) me.team = Json.I(you, "team");
                        foreach (var p in Json.L(m, "players")) AddRemote(p as Dictionary<string, object>);
                        status = "SALA " + Json.S(Json.O(m, "room"), "id"); FBHUD.I?.Center("ONLINE · " + status, 3); break;
                    }
                case "peer+": AddRemote(Json.O(m, "p")); break;
                case "peer-": { int id = Json.I(m, "id"); FBParedao.PeerLeft(id); if (remotes.TryGetValue(id, out var r)) { if (r) { FBMatch.All.Remove(r.ch); FBMatch.Remotes.Remove(r.ch); Destroy(r.gameObject); } remotes.Remove(id); } break; }
                case "st": { if (remotes.TryGetValue(Json.I(m, "id"), out var r) && r) r.Push(m); break; }
                case "ev": Event(m); break;
                case "score": { var t = Json.L(m, "teams"); if (t != null && t.Count > 1) { score[0] = (int)Json.Fv(t[0]); score[1] = (int)Json.Fv(t[1]); } int tl = Json.I(m, "tl"); status = $"{tl / 60}:{tl % 60:00}"; break; }
                case "end": FBHUD.I?.Center("FIM DA PARTIDA", 6); FBMatch.I?.EndMatch(score[FBSession.team] >= score[1 - FBSession.team], "ONLINE", score[0], score[1]); break;
                case "chat": FBMatch.Feed((Json.S(m, "name") ?? "") + ": " + (Json.S(m, "msg") ?? ""), false); break;
            }
        }
        void Event(Dictionary<string, object> m)
        {
            int id = Json.I(m, "id"); var e = Json.S(m, "e"); remotes.TryGetValue(id, out var r);
            if (e == "som" || e == "somreq" || e == "somchk") { FBParedao.OnNet(m); return; }   // paredão: estado + pedaços da faixa (server.mjs repassa só ao "to")
            if (e == "hit" && Json.I(m, "to") == myId)
            {
                var me = FBPlayer.I?.ch;
                if (me && me.alive)
                {
                    float dmg = Json.F(m, "dmg"), ap = Json.F(m, "ap"); bool hd = Json.B(m, "head"); var pt = Json.S(m, "pt"); if (string.IsNullOrEmpty(pt)) pt = hd ? "head" : "chest";
                    if (ap > 0) { var res = new FBDamageModel.Result { raw = dmg, health = dmg, part = pt }; FBDamageModel.ApplyArmor(me, ref res, ap); FBCombat.DamageActor(me, FBDamageModel.Whole(res.health), hd, r ? r.ch : null, null, new FBHitInfo { part = pt, armorDone = true }); }   // r51: colete do CS no dono
                    else FBCombat.DamageActor(me, dmg, hd, r ? r.ch : null);
                }
            }
            else if (e == "die" && r) { r.ch.Die(null); var by = Json.I(m, "by"); FBMatch.Feed($"{(by == myId ? FBSession.name : remotes.TryGetValue(by, out var k) && k ? k.displayName : "?")} ✖ {r.displayName}", by == myId); if (by == myId) { FBMatch.Stats.kills++; if (Json.B(m, "head")) FBMatch.Stats.heads++; } }
            else if (e == "shot" && r) r.Shot();
            else if (e == "spawn" && r) { var sp = Json.L(m, "p"); if (sp != null) r.Teleport(Coords.FromWeb(sp)); r.ch.Revive(); }
            else if (e == "dance" && r) { var v = Json.S(m, "v"); if (string.IsNullOrEmpty(v)) r.ch.StopDance(true); else r.ch.PlayDance(v); }
            else if (e == "hfx") HitFx(m, r);
            else if (e == "vo") { }   // voz de dano de outro jogador: não toca aqui (cada um ouve só a própria)
            else if (e == "stopdance" && r) r.ch.StopDance(true);
            else if (e == "glass") { var p = Coords.FromWeb(Json.L(m, "p")); foreach (var c in Physics.OverlapSphere(p, .6f, 1 << FBWorld.LGlass)) c.GetComponent<FBGlass>()?.Break(p, Vector3.forward, true); }
        }
        public void SendHit(FBCharacter target, float dmg, bool head, float ap = 0, string part = null)
        {
            if (!open) return; var r = target ? target.GetComponent<FBRemote>() : null; if (!r) return;
            Send(new Dictionary<string, object> { ["t"] = "ev", ["e"] = "hit", ["to"] = r.netId, ["dmg"] = Mathf.RoundToInt(dmg), ["head"] = head, ["w"] = FBPlayer.I?.wu.Cur?.name ?? "", ["ap"] = Mathf.Round(ap * 1000) / 1000f, ["pt"] = part ?? "" });
        }
        // voz de dano (agente de áudio): o dono do personagem manda banco/variação/intensidade; quem recebe toca a mesma no remoto
        public void SendVoice(int bank, int variation, float intensity) { if (open) Send(new Dictionary<string, object> { ["t"] = "ev", ["e"] = "vo", ["b"] = bank, ["v"] = variation, ["i"] = Mathf.RoundToInt(Mathf.Clamp01(intensity) * 100) }); }
        public void SendShot(FBCharacter c = null) { if (open) Send(new Dictionary<string, object> { ["t"] = "ev", ["e"] = "shot" }); }
        public void SendEv(string e, Dictionary<string, object> data) { if (!open) return; data["t"] = "ev"; data["e"] = e; Send(data); }
        public void SendEvent(string e) { if (open) Send(new Dictionary<string, object> { ["t"] = "ev", ["e"] = e }); }
        public void SendDie(FBCharacter a, FBCharacter by, bool head) { if (!open || !a || !a.isPlayer) return; var r = by ? by.GetComponent<FBRemote>() : null; Send(new Dictionary<string, object> { ["t"] = "ev", ["e"] = "die", ["by"] = r ? r.netId : 0, ["head"] = head, ["w"] = by && by.weapon != null ? by.weapon.name : "" }); }
        public void SendSpawn(FBCharacter a) { if (open && a) Send(new Dictionary<string, object> { ["t"] = "ev", ["e"] = "spawn", ["p"] = R(Coords.ToWeb(a.Pos)), ["prot"] = Mathf.Max(0, a.protUntil - Time.time) }); }
        public void SendGlass(FBGlass g, Vector3 p) { if (open && g) Send(new Dictionary<string, object> { ["t"] = "ev", ["e"] = "glass", ["p"] = R(Coords.ToWeb(p)) }); }
        public string NameOf(int id) => id == myId ? FBSession.name : remotes.TryGetValue(id, out var r) && r ? r.displayName : null;
        public void Leave() { if (open) Send(new Dictionary<string, object> { ["t"] = "leave" }); }

        void AddRemote(Dictionary<string, object> p)
        {
            if (p == null) return; int id = Json.I(p, "id"); if (id == myId || remotes.ContainsKey(id)) return;
            var full = Json.S(p, "skin") ?? ""; var parts = full.Split('~'); var skin = parts[0]; var look = parts.Length > 1 ? parts[1] : "";
            if (!FBRegistry.I.HasChar(skin)) skin = FBRegistry.I.charIds[0];
            var ch = FBMatch.I.Spawn(skin, look, Json.I(p, "team"), Coords.FromWeb(Json.L(p, "p")), false, false); ch.isRemote = true; ch.displayName = Json.S(p, "name") ?? "Jogador"; FBMatch.Remotes.Add(ch);
            var cc = ch.GetComponent<CharacterController>(); if (cc) Destroy(cc);
            FBWardrobe.ApplyTo(ch.gameObject, look);
            var r = ch.gameObject.AddComponent<FBRemote>(); r.netId = id; r.ch = ch; r.displayName = Json.S(p, "name") ?? "Jogador"; remotes[id] = r;
        }

        // JSON de saída (pequeno e sem dependências)
        public static string ToJson(object o)
        {
            switch (o)
            {
                case null: return "null";
                case string s: return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
                case bool b: return b ? "true" : "false";
                case float f: return f.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case double d: return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case int i: return i.ToString();
                case long l: return l.ToString();
                case System.Collections.Generic.List<object> lo: { var sb = new StringBuilder("["); for (int k = 0; k < lo.Count; k++) { if (k > 0) sb.Append(','); sb.Append(ToJson(lo[k])); } return sb.Append(']').ToString(); }
                case float[] fa: { var sb = new StringBuilder("["); for (int k = 0; k < fa.Length; k++) { if (k > 0) sb.Append(','); sb.Append(ToJson(fa[k])); } return sb.Append(']').ToString(); }
                case Dictionary<string, object> dd: { var sb = new StringBuilder("{"); bool first = true; foreach (var kv in dd) { if (!first) sb.Append(','); first = false; sb.Append(ToJson(kv.Key)).Append(':').Append(ToJson(kv.Value)); } return sb.Append('}').ToString(); }
                default: return ToJson(o.ToString());
            }
        }
    }
}
