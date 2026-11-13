// Multiplayer: o MESMO protocolo do jogo web (server.mjs / net.js), então jogadores do Unity e do navegador entram na mesma sala.
// hello -> list -> join/create; 'st' ~20x/s com posição/velocidade/yaw/pitch/flags/arma/vida (coordenadas convertidas web<->Unity);
// eventos 'shot', 'hit' (quem leva o tiro aplica o dano em si), 'die' (placar no servidor). Remotos interpolados ~110 ms no passado.
// Transporte: System.Net.WebSockets (Windows/Android/iOS) ou a ponte JS (Plugins/WebGL/FBWebSocket.jslib) no WebGL.
//
// r68 (10/11/2026, "corrija definitivamente o multiplayer em LAN"):
//  - conexão com prazo (6 s) e reconexão automática (até 6 tentativas) quando o servidor/Host cai; ao voltar, entra na mesma sala;
//  - sala escolhida por MAPA + MODO (antes só pelo mapa: o De Rolê caía num mata-mata do mesmo mapa e vice-versa); De Rolê cria sala 'role';
//  - o time que o servidor dá vira o time da partida (FBSession.team + volta para a base desse time; antes o placar e o "venceu?" usavam o
//    time do menu);
//  - De Rolê online: sem placar/fim; carteira da loja (FBGunShop) informada ao servidor, que confere toda compra ('wallet'/'buy');
//  - 'rcar': o carro que só existe no PC do dono (garagem do rolê, carro do trânsito local) é recriado nos outros PCs com o mesmo syncId,
//    então o estado 've' de quem dirige acha o carro (antes o remoto aparecia deslizando a pé). Cópia de outro jogador: só o dono dirige.
// r68b (revisão de 11/11): caiu = sai da sala na hora (sem loja até entrar de novo); nunca desiste de reconectar (3 s nas 6 primeiras,
//  depois a cada 10 s); sala recusada ("cheia", "não encontrada") = procura/cria outra em vez de ficar parado; avisos de rede aparecem
//  também no De Rolê (lá o HUD fica apagado); mensagem e queda de conexão velha (depois de reconectar) são ignoradas.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
        // r68: o que o servidor sabe fazer (servidor antigo não manda 'caps') e a sala em que estou
        public bool capRole, capLoja, capRcar, capVoz, capX2, lan; public string host, roomId, roomMode = "";
        string wantMode, rejoin; int attempts, joinErr; float retryT = -1; bool everOpen, inRoom;
        public bool RoleRoom => roomMode == "role" || roomMode == "rolex";                        // r69: rolê armado também é cidade livre (loja, sem placar)
        public bool TeamRoom => roomMode == "tdm" || roomMode == "x2";
        public bool InRoom => open && inRoom;                                                      // r68b: conectado E já dentro da sala
        public bool ShopAuthority => InRoom && capLoja && RoleRoom;                                // compra conferida pelo servidor
        public static bool Online => I && I.open;
        // r68b: no De Rolê o HUD fica apagado (só acende armado ou na loja): aviso de rede acende o HUD pelo tempo do aviso
        static void Aviso(string s, float ttl) { FBHUD.I?.Center(s, ttl); if (FBSession.mode == "role") FBGunShop.Flash(ttl); }
        void Lost() { open = false; inRoom = false; roomMode = ""; FBGunShop.OnDisconnect(); }

#if !UNITY_WEBGL || UNITY_EDITOR
        ClientWebSocket ws; CancellationTokenSource cts;
        async void Connect(string url)
        {
            status = "CONECTANDO…";
            try { cts?.Cancel(); ws?.Dispose(); } catch { }
            var w = new ClientWebSocket(); var c = new CancellationTokenSource(); ws = w; cts = c;
            try
            {
                var t = w.ConnectAsync(new Uri(url), c.Token);
                if (await Task.WhenAny(t, Task.Delay(6000)) != t) { try { c.Cancel(); } catch { } throw new Exception("o servidor não respondeu em 6 s"); }
                await t; if (w != ws) { try { w.Dispose(); } catch { } return; }                // conexão velha (já existe outra): descarta
                inbox.Enqueue("{\"t\":\"_open\"}"); _ = Task.Run(() => Receive(w, c));
            }
            catch (Exception e) { if (w == ws) inbox.Enqueue("{\"t\":\"_fail\",\"msg\":" + ToJson((e.InnerException ?? e).Message) + "}"); }
        }
        async Task Receive(ClientWebSocket w, CancellationTokenSource c)
        {
            var buf = new byte[1 << 16]; var sb = new StringBuilder();
            while (w.State == WebSocketState.Open)
            {
                WebSocketReceiveResult r; try { r = await w.ReceiveAsync(new ArraySegment<byte>(buf), c.Token); } catch { break; }
                if (r.MessageType == WebSocketMessageType.Close) break;
                sb.Append(Encoding.UTF8.GetString(buf, 0, r.Count)); if (r.EndOfMessage) { if (w == ws) inbox.Enqueue(sb.ToString()); sb.Clear(); }
            }
            if (w == ws) inbox.Enqueue("{\"t\":\"_close\"}");
        }
        public void SendRaw(string s) { if (ws == null || ws.State != WebSocketState.Open) return; var b = Encoding.UTF8.GetBytes(s); try { _ = ws.SendAsync(new ArraySegment<byte>(b), WebSocketMessageType.Text, true, cts.Token); } catch { } }
        void OnDestroy() { if (I == this) I = null; FBLanHost.Fundo(); try { var w = ws; ws = null; cts?.Cancel(); w?.Dispose(); } catch { } }   // r68e: Fundo = não pausar sem foco só em rede
#else
        [DllImport("__Internal")] static extern void FBWS_Connect(string url, string goName);
        [DllImport("__Internal")] static extern void FBWS_Send(string msg);
        void Connect(string url) { status = "CONECTANDO…"; FBWS_Connect(url, gameObject.name); }
        public void SendRaw(string s) { if (open) FBWS_Send(s); }
        public void WSOpen(string _) { inbox.Enqueue("{\"t\":\"_open\"}"); }
        public void WSMessage(string m) { inbox.Enqueue(m); }
        public void WSClose(string _) { inbox.Enqueue(open ? "{\"t\":\"_close\"}" : "{\"t\":\"_fail\",\"msg\":\"sem resposta\"}"); }
        void OnDestroy() { if (I == this) I = null; }
#endif

        void Awake() { I = this; gameObject.name = "FBNet"; FBDamageEvents.OnDamage += OnDamage; FBLanHost.Fundo(); FBVoiceChat.Ensure(); }   // r68e · r69: voz: partida online não pausa sem foco
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
        // r69: o modo da sala vem do modo ONLINE do squad: rolê (desarmado/armado), 2 × 2 (sala privada criada pelo squad) ou times
        void Start() { wantMode = FBSession.mode == "role" ? (FBSession.RoleArmed ? "rolex" : "role") : FBSession.Duel2x2 ? "x2" : "tdm"; Connect(FBSession.Server); }
        static int MaxFor(string m) => m == "role" || m == "rolex" ? 16 : 10;
        void OnOpen()
        {
            // "unity": o server.mjs aceita os mapas que só o Unity tem (orla, centro_civico, morro_sacola, bv_city) e esconde essas salas do jogo web;
            // a praca_pt é outro mapa em cada jogo, então a sala dela só aparece para quem é do mesmo jogo de quem a criou
            Send(new Dictionary<string, object> { ["t"] = "hello", ["name"] = FBSession.name, ["skin"] = FBSession.FullSkin, ["team"] = FBSession.team, ["code"] = "", ["unity"] = true, ["pid"] = FBLobbyNet.Pid });   // r62: pid = código de jogador (squad: mesmo time na sala reservada)
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
            if (retryT > 0 && (retryT -= Time.unscaledDeltaTime) <= 0) { retryT = -1; attempts++; Connect(FBSession.Server); }
            if (!open) return;
            sendT -= Time.deltaTime; pingT -= Time.deltaTime;
            if (pingT <= 0) { pingT = 2; Send(new Dictionary<string, object> { ["t"] = "ping", ["ts"] = Time.realtimeSinceStartup * 1000 }); }
            var p = FBPlayer.I; if (sendT > 0 || !p) return;
            var st = BuildState(p, Time.realtimeSinceStartup); if (st != null) Send(st);
        }
        // Estado 'st' (mesmos campos do main.js netSendState). Compressão compatível com o server.mjs (que só repassa):
        //  · 20 Hz mexendo, 5 Hz parado (nada mudou além de ruído) — o net.js/FBRemote seguram o último estado;
        //  · arma/skin/acessórios/dança/pose só quando mudam e em todo pacote "completo" (1x/s, para quem acabou de entrar).
        float fullT; string sentW = "", sentDn = ""; Vector3 sentP; float sentYaw, sentPi; int sentF = -1, sentHp = -1; bool sentVe;
        public Dictionary<string, object> BuildState(FBPlayer p, float now)
        {
            var ch = p.ch; int f = (ch.sprint ? F_SPRINT : 0) | (ch.crouch ? F_CROUCH : 0) | (ch.aiming ? F_AIM : 0) | (ch.shooting ? F_SHOOT : 0) | (ch.grounded ? F_GROUND : 0) | (ch.alive ? F_ALIVE : 0);
            var ve = FBDrive.VehicleState(); int hp = Mathf.Max(0, Mathf.RoundToInt(ch.hp)); var wid = p.wu.Cur?.id; var dn = ch.dance != null && !ch.dance.leaving ? ch.dance.clip ?? "" : "";
            if (ve != null) { var mine = FBVehicles.CarOf(ch); if (mine && LocalOnly(mine.syncId) && !shared.Contains(mine)) ShareCar(mine); }   // r68: carro que só existe aqui: os outros recebem uma cópia antes do estado
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
                case "_open": open = true; inRoom = false; everOpen = true; attempts = 0; joinErr = 0; status = "CONECTADO"; OnOpen(); break;
                case "_fail":
                    {
                        if (inRoom) rejoin = roomId; Lost(); status = "SEM CONEXÃO"; var why = Json.S(m, "msg") ?? "";
                        Debug.Log($"[FreeBala][rede] falha ao conectar em {FBSession.Server} (tentativa {attempts + 1}): {why}");
                        // r68b: nunca desiste (antes parava na 6ª tentativa e a partida ficava offline para sempre): depois da 6ª, a cada 10 s
                        if (attempts < 6) { retryT = 3; Aviso($"SEM CONEXÃO COM O SERVIDOR · TENTANDO DE NOVO ({attempts + 1}/6)", 3); }
                        else { retryT = 10; Aviso("SEM CONEXÃO: " + why.ToUpper() + " · TENTANDO A CADA 10 S · NO MENU: SQUAD → SERVIDOR / REDE LOCAL", 6); }
                        break;
                    }
                case "_close":
                    {
                        if (!open) break; if (inRoom) rejoin = roomId; Lost(); status = "RECONECTANDO…"; Debug.Log("[FreeBala][rede] conexão perdida; reconectando na sala " + roomId);
                        ClearRemotes(); Aviso("CONEXÃO PERDIDA · RECONECTANDO…", 3); attempts = 0; retryT = 1.5f; break;
                    }
                case "hi":
                    {
                        myId = Json.I(m, "id"); var caps = Json.L(m, "caps") ?? new List<object>(); capRole = caps.Contains("role"); capLoja = caps.Contains("loja"); capRcar = caps.Contains("rcar"); capVoz = caps.Contains("voz"); capX2 = caps.Contains("x2");
                        lan = Json.B(m, "lan"); host = Json.S(m, "host"); break;
                    }
                case "pong": ping = Time.realtimeSinceStartup * 1000 - Json.F(m, "ts"); break;
                case "rooms":
                    {
                        var list = Json.L(m, "rooms") ?? new List<object>(); string join = null;
                        if (!string.IsNullOrEmpty(FBSession.room)) { join = FBSession.room; FBSession.room = null; }   // r62: sala escolhida pelo squad / r68: sala escolhida na REDE LOCAL
                        else if (rejoin != null && list.Any(r => Json.S(r, "id") == rejoin)) join = rejoin;          // r68: volta para a mesma sala depois de cair
                        else foreach (var r in list) if (Json.S(r, "map") == FBSession.map && (Json.S(r, "mode") ?? "tdm") == wantMode && Json.I(r, "players") < Json.I(r, "max")) { join = Json.S(r, "id"); break; }
                        rejoin = null;
                        if (join != null) Send(new Dictionary<string, object> { ["t"] = "join", ["id"] = join });
                        else Send(new Dictionary<string, object> { ["t"] = "create", ["name"] = "Sala de " + FBSession.name, ["mode"] = wantMode, ["map"] = FBSession.map, ["max"] = MaxFor(wantMode) });
                        break;
                    }
                case "joined":
                    {
                        var you = Json.O(m, "you"); myId = Json.I(you, "id"); var room = Json.O(m, "room"); roomId = Json.S(room, "id"); roomMode = Json.S(room, "mode") ?? "tdm"; inRoom = true; joinErr = 0;
                        int tm = Json.I(you, "team"); var me = FBPlayer.I?.ch;
                        if (me)
                        {
                            bool moved = me.team != tm; me.team = tm; FBSession.team = tm;                  // r68: o time da sala (placar e "venceu?" leem FBSession.team)
                            if (moved && TeamRoom && FBMatch.I && me.alive) { var sp = FBMatch.I.NextSpot(tm); FBMatch.Teleport(me, sp, FBMatch.I.SpawnYaw(sp)); FBPlayer.I.OnRespawn(me.transform.eulerAngles.y); }
                        }
                        foreach (var p in Json.L(m, "players") ?? new List<object>()) AddRemote(p as Dictionary<string, object>);
                        status = "SALA " + roomId; Aviso((roomMode == "rolex" ? "ROLÊ ARMADO ONLINE · " : RoleRoom ? "DE ROLÊ ONLINE · " : roomMode == "x2" ? "2 × 2 · " : "ONLINE · ") + status + (lan ? " · REDE LOCAL" : ""), 3);
                        if ((wantMode == "role" || wantMode == "rolex") && !RoleRoom) Aviso("SERVIDOR ANTIGO: SEM SALA DE DE ROLÊ · ATUALIZE O SERVER.MJS (OU USE A REDE LOCAL)", 6);
                        else if (wantMode == "rolex" && roomMode != "rolex") Aviso("SERVIDOR ANTIGO: SEM ROLÊ ARMADO (A SALA É DE ROLÊ DESARMADO) · ATUALIZE O SERVER.MJS (OU USE A REDE LOCAL)", 7);
                        else if (wantMode == "x2" && roomMode != "x2") Aviso("SERVIDOR SEM 2 × 2 (TIMES DO SERVIDOR) · ATUALIZE O SERVER.MJS (OU USE A REDE LOCAL)", 7);
                        else if (RoleRoom && !capLoja) Aviso("ESTE SERVIDOR NÃO CONFERE A LOJA (SEM TABELA DE PREÇOS) · COMPRAS SÓ NO SEU PERFIL", 6);
                        Debug.Log($"[FreeBala][rede] na sala {roomId} ({roomMode}, mapa {Json.S(room, "map")}) · eu {myId} time {tm} · {remotes.Count} outro(s) · caps {(capRole ? "role " : "")}{(capLoja ? "loja " : "")}{(capRcar ? "rcar" : "")}{(lan ? " · LAN " + host : "")}");
                        if (ShopAuthority) Send(new Dictionary<string, object> { ["t"] = "wallet", ["v"] = FBSession.money, ["owned"] = FBGunShop.OwnedList() });
                        FBGunShop.OnRoomJoined();
                        ShareAll(); break;
                    }
                case "peer+": AddRemote(Json.O(m, "p")); ShareAll(); break;                          // r68: quem chegou recebe os carros que só existem aqui
                case "peer-": { int id = Json.I(m, "id"); FBParedao.PeerLeft(id); DropRemote(id); break; }
                case "st": { if (remotes.TryGetValue(Json.I(m, "id"), out var r) && r) r.Push(m); break; }
                case "ev": Event(m); break;
                case "score": { if (RoleRoom) break; var t = Json.L(m, "teams"); if (t != null && t.Count > 1) { score[0] = (int)Json.Fv(t[0]); score[1] = (int)Json.Fv(t[1]); } int tl = Json.I(m, "tl"); status = $"{tl / 60}:{tl % 60:00}"; break; }
                case "end": if (RoleRoom) break; FBHUD.I?.Center("FIM DA PARTIDA", 6); FBMatch.I?.EndMatch(score[FBSession.team] >= score[1 - FBSession.team], "ONLINE", score[0], score[1]); break;
                case "chat": FBMatch.Feed((Json.S(m, "name") ?? "") + ": " + (Json.S(m, "msg") ?? ""), false); break;
                case "wallet": FBGunShop.OnWallet(m); break;
                case "buy": FBGunShop.OnBuy(m); break;
                case "werr": Debug.Log("[FreeBala][loja] servidor: " + Json.S(m, "msg")); break;
                case "err":
                    {
                        var why = Json.S(m, "msg") ?? ""; Aviso("SERVIDOR: " + why.ToUpper(), 4);
                        // r68b: entrada recusada ("sala cheia", "não encontrada"): antes o jogo ficava conectado e fora de qualquer sala.
                        // Agora procura outra sala do mesmo mapa e modo (2 vezes) e depois cria a própria.
                        if (open && !inRoom)
                        {
                            FBSession.room = null; rejoin = null; joinErr++;
                            Debug.Log($"[FreeBala][rede] entrada na sala recusada ({why}): {(joinErr <= 2 ? "procurando outra" : "criando uma sala")}");
                            if (joinErr <= 2) Send(new Dictionary<string, object> { ["t"] = "list" });
                            else Send(new Dictionary<string, object> { ["t"] = "create", ["name"] = "Sala de " + FBSession.name, ["mode"] = wantMode, ["map"] = FBSession.map, ["max"] = MaxFor(wantMode) });
                        }
                        break;
                    }
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
            else if (e == "vc") FBVoiceChat.OnRoomVoice(id, r ? r.displayName : NameOf(id), Json.I(m, "q"), Json.S(m, "d"));   // r69: microfone (o servidor já filtrou por time)
            else if (e == "stopdance" && r) r.ch.StopDance(true);
            else if (e == "glass") { var p = Coords.FromWeb(Json.L(m, "p")); foreach (var c in Physics.OverlapSphere(p, .6f, 1 << FBWorld.LGlass)) c.GetComponent<FBGlass>()?.Break(p, Vector3.forward, true); }
            else if (e == "rcar" && r) RemoteCar(id, m);
        }
        public void SendHit(FBCharacter target, float dmg, bool head, float ap = 0, string part = null)
        {
            if (!open) return; var r = target ? target.GetComponent<FBRemote>() : null; if (!r) return;
            Send(new Dictionary<string, object> { ["t"] = "ev", ["e"] = "hit", ["to"] = r.netId, ["dmg"] = Mathf.RoundToInt(dmg), ["head"] = head, ["w"] = FBPlayer.I?.wu.Cur?.name ?? "", ["ap"] = Mathf.Round(ap * 1000) / 1000f, ["pt"] = part ?? "" });
        }
        // voz de dano (agente de áudio): o dono do personagem manda banco/variação/intensidade; quem recebe toca a mesma no remoto
        public void SendVoice(int bank, int variation, float intensity) { if (open) Send(new Dictionary<string, object> { ["t"] = "ev", ["e"] = "vo", ["b"] = bank, ["v"] = variation, ["i"] = Mathf.RoundToInt(Mathf.Clamp01(intensity) * 100) }); }
        public void SendMic(int q, string d) { if (InRoom) Send(new Dictionary<string, object> { ["t"] = "ev", ["e"] = "vc", ["q"] = q, ["d"] = d }); }   // r69: microfone na partida
        public void SendShot(FBCharacter c = null) { if (open) Send(new Dictionary<string, object> { ["t"] = "ev", ["e"] = "shot" }); }
        public void SendEv(string e, Dictionary<string, object> data) { if (!open) return; data["t"] = "ev"; data["e"] = e; Send(data); }
        public void SendEvent(string e) { if (open) Send(new Dictionary<string, object> { ["t"] = "ev", ["e"] = e }); }
        public void SendDie(FBCharacter a, FBCharacter by, bool head) { if (!open || !a || !a.isPlayer) return; var r = by ? by.GetComponent<FBRemote>() : null; Send(new Dictionary<string, object> { ["t"] = "ev", ["e"] = "die", ["by"] = r ? r.netId : 0, ["head"] = head, ["w"] = by && by.weapon != null ? by.weapon.name : "" }); }
        public void SendSpawn(FBCharacter a) { if (open && a) Send(new Dictionary<string, object> { ["t"] = "ev", ["e"] = "spawn", ["p"] = R(Coords.ToWeb(a.Pos)), ["prot"] = Mathf.Max(0, a.protUntil - Time.time) }); }
        public void SendGlass(FBGlass g, Vector3 p) { if (open && g) Send(new Dictionary<string, object> { ["t"] = "ev", ["e"] = "glass", ["p"] = R(Coords.ToWeb(p)) }); }
        public string NameOf(int id) => id == myId ? FBSession.name : remotes.TryGetValue(id, out var r) && r ? r.displayName : null;
        public int RemoteCount => remotes.Count;
        public void Leave() { if (open) Send(new Dictionary<string, object> { ["t"] = "leave" }); inRoom = false; }

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
        void DropRemote(int id)
        {
            if (remotes.TryGetValue(id, out var r)) { if (r) { if (FBVehicles.CarOf(r.ch)) FBDrive.Release(r.ch); FBMatch.All.Remove(r.ch); FBMatch.Remotes.Remove(r.ch); Destroy(r.gameObject); } remotes.Remove(id); }
            if (rcars.TryGetValue(id, out var cars)) { foreach (var c in cars) if (c) { if (c.driver && c.driver != FBMatch.Player) FBDrive.Release(c.driver); FBVehicles.list.Remove(c); Destroy(c.gameObject); } rcars.Remove(id); }
        }
        void ClearRemotes() { foreach (var id in remotes.Keys.Concat(rcars.Keys).Distinct().ToList()) DropRemote(id); }

        // ---------------------------------------------------------------- r68: carros que só existem no PC do dono ('rcar')
        readonly List<FBCar> shared = new(); readonly Dictionary<int, List<FBCar>> rcars = new();
        public static bool LocalOnly(string sid) => sid != null && (sid.StartsWith("role:") || sid.StartsWith("npc:"));
        public void ShareCar(FBCar car, bool force = false)
        {
            if (!car || !LocalOnly(car.syncId) || car.netOwner >= 0) return;
            if (!shared.Contains(car)) { shared.Add(car); var extra = shared.Where(c => !c || (c != FBRole.roleCar && c != car)).ToList(); while (shared.Count > 3 && extra.Count > 0) { shared.Remove(extra[0]); extra.RemoveAt(0); } }
            SendCar(car);
        }
        void ShareAll() { shared.RemoveAll(c => !c); foreach (var c in shared) SendCar(c); }
        void SendCar(FBCar car)
        {
            if (!open || !car) return; var t = car.transform; var q = t.rotation; uint pc = ((uint)Mathf.Clamp(Mathf.RoundToInt(car.paint.r * 255), 0, 255) << 16) | ((uint)Mathf.Clamp(Mathf.RoundToInt(car.paint.g * 255), 0, 255) << 8) | (uint)Mathf.Clamp(Mathf.RoundToInt(car.paint.b * 255), 0, 255);
            var tune = car == FBRole.roleCar || car.syncId.StartsWith("role:") ? FBTune.Load(car.id) : null;
            SendEv("rcar", new Dictionary<string, object> { ["sid"] = car.syncId, ["vid"] = car.id, ["pu"] = new float[] { t.position.x, t.position.y, t.position.z }, ["q"] = new float[] { q.x, q.y, q.z, q.w }, ["pc"] = (double)pc, ["som"] = car.soundWallNode != null, ["sm"] = car.soundMod ?? "", ["tune"] = tune != null ? JsonUtility.ToJson(tune) : "" });
        }
        void RemoteCar(int owner, Dictionary<string, object> m)
        {
            var sid = Json.S(m, "sid"); var vid = Json.S(m, "vid"); if (!LocalOnly(sid) || string.IsNullOrEmpty(vid) || !FBVehicles.Allowed) return;
            var pu = Json.L(m, "pu"); var q = Json.L(m, "q"); if (pu == null || pu.Count < 3 || q == null || q.Count < 4) return;
            var pos = new Vector3(Json.Fv(pu[0]), Json.Fv(pu[1]), Json.Fv(pu[2])); var rot = new Quaternion(Json.Fv(q[0]), Json.Fv(q[1]), Json.Fv(q[2]), Json.Fv(q[3])); var tune = Json.S(m, "tune");
            var ex = FBVehicles.BySyncId(sid);
            if (ex && ex.netOwner != owner) return;                                                  // id de outro dono: nunca acontece (o id leva o código do jogador)
            if (ex && ex.id == vid)
            {
                if (ex.remoteDriver < 0 && !ex.driver) { ex.transform.SetPositionAndRotation(pos, rot); Physics.SyncTransforms(); }
                if (!string.IsNullOrEmpty(tune)) ApplyTune(ex, tune); return;
            }
            if (ex) { if (ex.driver && ex.driver != FBMatch.Player) FBDrive.Release(ex.driver); FBVehicles.list.Remove(ex); Destroy(ex.gameObject); }
            if (!GameData.Vehicles.ContainsKey(vid) || !FBRegistry.I.Vehicle(vid)) { Debug.Log("[FreeBala][rede] carro de outro jogador fora deste jogo: " + vid); return; }
            bool som = Json.B(m, "som"); var sm = Json.S(m, "sm");
            var car = FBVehicles.Make(vid, pos, rot.eulerAngles.y, true, () => .5f, true, (uint)Json.F(m, "pc"), som, string.IsNullOrEmpty(sm) ? null : sm);
            if (!car) return;
            car.transform.SetPositionAndRotation(pos, rot); car.syncId = sid; car.netOwner = owner; Physics.SyncTransforms();
            if (!string.IsNullOrEmpty(tune)) ApplyTune(car, tune);
            if (!rcars.TryGetValue(owner, out var l)) rcars[owner] = l = new List<FBCar>();
            l.RemoveAll(c => !c); l.Add(car);
            while (l.Count > 3) { var old = l.FirstOrDefault(c => c.remoteDriver < 0 && !c.syncId.StartsWith("role:")) ?? l[0]; l.Remove(old); if (old.driver && old.driver != FBMatch.Player) FBDrive.Release(old.driver); FBVehicles.list.Remove(old); Destroy(old.gameObject); }
            Debug.Log($"[FreeBala][rede] carro de {NameOf(owner)}: {vid} ({sid})");
        }
        static void ApplyTune(FBCar car, string json) { try { var T = JsonUtility.FromJson<FBTuneData>(json); if (T != null) FBTune.Apply(car, T); } catch (Exception e) { Debug.LogWarning("[FreeBala][rede] oficina do carro remoto: " + e.Message); } }

        // JSON de saída (pequeno e sem dependências). r68: escapa caracteres de controle e troca NaN/infinito por 0 (o servidor descartava
        // a mensagem inteira — JSON.parse não aceita NaN) e aceita listas de texto.
        public static string ToJson(object o)
        {
            switch (o)
            {
                case null: return "null";
                case string s: return Esc(s);
                case bool b: return b ? "true" : "false";
                case float f: return float.IsNaN(f) || float.IsInfinity(f) ? "0" : f.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case double d: return double.IsNaN(d) || double.IsInfinity(d) ? "0" : d.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case int i: return i.ToString();
                case long l: return l.ToString();
                case System.Collections.Generic.List<object> lo: { var sb = new StringBuilder("["); for (int k = 0; k < lo.Count; k++) { if (k > 0) sb.Append(','); sb.Append(ToJson(lo[k])); } return sb.Append(']').ToString(); }
                case float[] fa: { var sb = new StringBuilder("["); for (int k = 0; k < fa.Length; k++) { if (k > 0) sb.Append(','); sb.Append(ToJson(fa[k])); } return sb.Append(']').ToString(); }
                case Dictionary<string, object> dd: { var sb = new StringBuilder("{"); bool first = true; foreach (var kv in dd) { if (!first) sb.Append(','); first = false; sb.Append(ToJson(kv.Key)).Append(':').Append(ToJson(kv.Value)); } return sb.Append('}').ToString(); }
                case System.Collections.IEnumerable e: { var sb = new StringBuilder("["); bool first = true; foreach (var x in e) { if (!first) sb.Append(','); first = false; sb.Append(ToJson(x)); } return sb.Append(']').ToString(); }
                default: return ToJson(o.ToString());
            }
        }
        static string Esc(string s)
        {
            var sb = new StringBuilder(s.Length + 2).Append('"');
            foreach (var ch in s) { if (ch == '"') sb.Append("\\\""); else if (ch == '\\') sb.Append("\\\\"); else if (ch < ' ') sb.Append("\\u").Append(((int)ch).ToString("x4")); else sb.Append(ch); }
            return sb.Append('"').ToString();
        }
    }
}
